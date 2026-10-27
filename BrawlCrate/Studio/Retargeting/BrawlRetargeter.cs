using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using NativeMatrix = BrawlLib.Internal.Matrix;
using NativeVector = BrawlLib.Internal.Vector3;

namespace BrawlCrate.Studio.Retargeting
{
    public sealed class BrawlRig
    {
        public Rig Rig { get; internal set; }
        public MDL0BoneNode[] Bones { get; internal set; }
        public string Fingerprint { get; internal set; }
    }

    public sealed class RetargetResult : IDisposable
    {
        public CHR0Node Animation { get; internal set; }
        public HumanoidRetargeter Plan { get; internal set; }
        public string SourceRig { get; internal set; }
        public string TargetRig { get; internal set; }
        public float MaximumLimbResidual { get; internal set; }
        public int ClampedLimbSamples { get; internal set; }
        public int IgnoredSourceTracks { get; internal set; }
        public string[] IgnoredSourceBones { get; internal set; } = new string[0];
        public ContactMotion Contacts { get; internal set; }
        public float MaximumContactResidual { get; internal set; }
        public float MaximumPelvisCorrection { get; internal set; }
        public RetargetKeyframeMode KeyframeMode { get; internal set; }
        public RetargetKeyframeReport KeyframeReport { get; internal set; }
        // Kept only for the explicit, read-only sparse-result review. The live
        // target is never selected or replaced while this comparison is open.
        internal CHR0Node DenseAnimation { get; set; }
        public void Dispose() { Animation?.Dispose(); DenseAnimation?.Dispose(); }
    }

    // Native CHR0 is an adapter, never the solver's intermediate representation.
    public static partial class BrawlRetargeter
    {
        private static int _diagnosticCaptures, _diagnosticBakes;
        public static int DiagnosticCaptures => Volatile.Read(ref _diagnosticCaptures);
        public static int DiagnosticBakes => Volatile.Read(ref _diagnosticBakes);
        public static void ExportMotionPac(string templatePath, string destination, MDL0Node targetModel, CHR0Node animation)
        {
            string path = Path.GetFullPath(destination), template = Path.GetFullPath(templatePath);
            if (!string.Equals(Path.GetExtension(path), ".pac", StringComparison.OrdinalIgnoreCase) || File.Exists(path) || Directory.Exists(path) ||
                string.Equals(path, template, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Choose a new PAC filename. Existing files and the motion template are protected.");
            BrawlRig target = Capture(targetModel);
            if (animation.Children.Any(c => !target.Bones.Any(b => b.Name == c.Name))) throw new InvalidDataException("The animation contains bones outside the target skeleton.");
            string originalHash = FighterProject.HashFile(template);
            string folder = Path.GetDirectoryName(path); Directory.CreateDirectory(folder);
            string replacement = Path.Combine(folder, ".fp-" + Guid.NewGuid().ToString("N") + ".chr0");
            string temporary = Path.Combine(folder, ".fp-" + Guid.NewGuid().ToString("N") + ".pac");
            try
            {
                using (ResourceNode root = NodeFactory.FromFile(null, template))
                {
                    if (!(root is ARCNode)) throw new NotSupportedException("Choose a native Brawl fighter motion PAC template.");
                    List<CHR0Node> clips = Walk(root).ToList().OfType<CHR0Node>().ToList();
                    CHR0Node[] slots = clips.Where(c => c.Name == animation.Name).ToArray();
                    if (slots.Length != 1) throw new InvalidDataException("The template needs exactly one " + animation.Name + " slot. Use a matching target motion PAC.");
                    if (!slots[0].Children.Any(c => c.Name == target.Bones[target.Rig.Roles["spine"]].Name))
                        throw new InvalidDataException("This motion PAC template belongs to a different skeleton.");
                    ValidateSource(target, slots[0]);
                    animation.ExportChecked(replacement);
                    slots[0].Replace(replacement);
                    root.Merge(); root.ExportChecked(temporary);
                    using (ResourceNode reopened = NodeFactory.FromFile(null, temporary))
                    {
                        var decoded = Walk(reopened).ToList().OfType<CHR0Node>().ToList();
                        CHR0Node saved = decoded.SingleOrDefault(c => c.Name == animation.Name);
                        if (!(reopened is ARCNode) || decoded.Count != clips.Count || saved == null || saved.FrameCount != animation.FrameCount ||
                            saved.Loop != animation.Loop || saved.Children.Count != animation.Children.Count ||
                            saved.Children.Any(c => !target.Bones.Any(b => b.Name == c.Name)))
                            throw new InvalidDataException("Fresh native decoding did not validate the target motion PAC.");
                    }
                }
                if (FighterProject.HashFile(template) != originalHash) throw new IOException("The template changed while exporting; the new output was not committed.");
                File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(replacement)) File.Delete(replacement);
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static SkeletonCharacterization Characterize(MDL0Node model)
        {
            Walk(model).ToList();
            MDL0BoneNode[] bones=model.AllBones.ToArray();
            return SkeletonCharacterizer.Analyze(NativeJoints(bones));
        }
        private static RigJoint[] NativeJoints(MDL0BoneNode[] bones)
        {
            var names=bones.Select((b,i)=>new{b.Name,Index=i}).ToDictionary(p=>p.Name,p=>p.Index,StringComparer.Ordinal);
            return bones.Select(b=>new RigJoint(b.Name,b.Parent is MDL0BoneNode parent?names[parent.Name]:-1,Transform(b.BindState))).ToArray();
        }
        public static BrawlRig Capture(MDL0Node model,IDictionary<string,string> reviewedMap=null)
        {
            Interlocked.Increment(ref _diagnosticCaptures);
            if(model==null)throw new InvalidDataException("Choose a loaded native model before creating a contact rig.");
            Walk(model).ToList();
            MDL0BoneNode[] bones=model.AllBones.ToArray();
            var names=bones.Select((b,i)=>new{b.Name,Index=i}).ToDictionary(p=>p.Name,p=>p.Index,StringComparer.Ordinal);
            RigJoint[] joints=NativeJoints(bones);
            var characterization=SkeletonCharacterizer.Analyze(joints);
            var roles=reviewedMap==null?characterization.Roles:reviewedMap.ToDictionary(p=>p.Key,p=>names.TryGetValue(p.Value,out int index)?index:throw new InvalidDataException("Mapped bone is missing: "+p.Value));
            SkeletonCharacterizer.Validate(joints,roles);
            float headRadius = model.BoxMax._y - bones[roles["head"]].BindMatrix.GetPoint()._y;
            if (headRadius < 0.1f) headRadius = (bones[roles["head"]].BindMatrix.GetPoint() - bones[roles["neck"]].BindMatrix.GetPoint()).TrueDistance(new NativeVector()) * 2;
            var rig = new Rig(model.Name, model.Name, joints, roles, headRadius);
            CaptureSurfaces(model, rig, bones, names);
            for (int i = 0; i < bones.Length; i++)
                if (Vector3.Distance(rig.Rest.Point(i), Vector(bones[i].BindMatrix.GetPoint())) > 0.01f)
                    throw new InvalidDataException("The native bind matrices disagree with this rig's local hierarchy at " + bones[i].Name + ".");
            string fingerprint=SkeletonFingerprint(model);
            return new BrawlRig {Rig = rig, Bones = bones, Fingerprint = fingerprint};
        }

        public static string SkeletonFingerprint(MDL0Node model)
        {
            Walk(model).ToList();var bones=model.AllBones.ToArray();
            string identity = string.Join("\n", bones.Select(b => b.Name + ":" + (b.Parent as MDL0BoneNode)?.Name + ":" +
                string.Join(",", Enumerable.Range(0, 9).Select(i => b.BindState[i].ToString("R", CultureInfo.InvariantCulture)))));
            string fingerprint;
            using (var sha = SHA256.Create()) fingerprint = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").ToLowerInvariant();
            return fingerprint;
        }

        private static void CaptureSurfaces(MDL0Node model, Rig rig, MDL0BoneNode[] bones, Dictionary<string,int> names)
        {
            var objects=model.PolygonList?.OfType<MDL0ObjectNode>().ToArray();
            if(objects==null||objects.Any(o=>o.Vertices==null&&o.FaceCount>0))
                throw new InvalidDataException("The model's mesh data is unavailable or was released. Reload the model before creating a contact rig.");
            var vertices=objects.Where(o=>o.Vertices!=null).SelectMany(o=>o.Vertices).Where(v=>v!=null).ToArray();
            if(vertices.Length==0)throw new InvalidDataException("The model has no decoded skinned mesh for ground-contact inspection.");
            foreach (string side in new[] {"left", "right"})
                foreach (string end in new[] {"foot", "hand"})
                {
                    string role = side + "." + end; int joint = rig.Roles[role];
                    var points = new List<SkinPoint>();
                    foreach (Vertex3 vertex in vertices)
                    {
                        var matrix = vertex.GetMatrixNode();
                        if (matrix?.Weights == null) continue;
                        float membership = matrix.Weights.Where(w => w.Bone is MDL0BoneNode && IsDescendant(rig, names[w.Bone.Name], joint)).Sum(w => w.Weight);
                        if (membership < 0.8f) continue;
                        NativeVector bind = matrix is MDL0BoneNode primary ? primary.BindMatrix * vertex.Position : vertex.Position;
                        var weights = matrix.Weights.Where(w => w.Bone is MDL0BoneNode && w.Weight > 0).Select(w => new SkinWeight {
                            Joint = names[w.Bone.Name], Weight = w.Weight, Local = Vector(w.Bone.InverseBindMatrix * bind)
                        }).ToArray();
                        if (weights.Length != 0) points.Add(new SkinPoint {Weights=weights});
                    }
                    if (points.Count < 4) throw new InvalidDataException("A reviewed contact rig needs skinned " + role + " geometry.");
                    Vector3 forward = end == "foot" && rig.Roles.ContainsKey(side+".toe") ? rig.Rest.Point(side + ".toe") - rig.Rest.Point(role) :
                        end=="hand" && rig.Roles.ContainsKey(side+".finger1.a")?rig.Rest.Point(side + ".finger1.a") - rig.Rest.Point(role):
                        Vector3.Transform(Vector3.UnitZ,RigMath.BodyFrame(rig));
                    Vector3 normal = end == "foot" || !rig.Roles.ContainsKey(side+".finger0.a") || !rig.Roles.ContainsKey(side+".finger3.a")?Vector3.UnitY:Vector3.Cross(forward,
                        rig.Rest.Point(side + ".finger0.a") - rig.Rest.Point(side + ".finger3.a"));
                    Quaternion inverse = Quaternion.Inverse(rig.Rest.Orientation[joint]);
                    rig.Surfaces.Add(role, new ContactSurface {Role=role, Points=points.ToArray(), NormalLocal=Vector3.Transform(RigMath.Unit(normal),inverse),
                        ForwardLocal=Vector3.Transform(RigMath.Unit(forward),inverse)});
                }
            rig.GroundHeight = new[] {"left.foot","right.foot"}.Min(role => rig.Surfaces[role].Sample(rig.Rest).Clearance);
            CaptureBodySurfaces(vertices,rig,bones,names);
        }

        private static void CaptureBodySurfaces(Vertex3[] vertices,Rig rig,MDL0BoneNode[] bones,Dictionary<string,int> names)
        {
            string[] regions={"pelvis","spine","chest","neck","head","left.shoulder","right.shoulder","left.upperArm","right.upperArm",
                "left.lowerArm","right.lowerArm","left.upperLeg","right.upperLeg","left.lowerLeg","right.lowerLeg"};
            var groups=regions.ToDictionary(r=>r,r=>new List<SkinPoint>());
            var nearest=new Dictionary<int,string>();
            for(int i=0;i<bones.Length;i++)
            {
                int joint=i;string role=null;
                while(joint>=0 && role==null){role=rig.Roles.Keys.FirstOrDefault(r=>rig.Roles[r]==joint);joint=rig.Joints[joint].Parent;}
                nearest.Add(i,role??"");
            }
            foreach(Vertex3 vertex in vertices)
            {
                var matrix=vertex.GetMatrixNode();if(matrix?.Weights==null)continue;
                // Long unmapped branches outside the head envelope are accessories,
                // independently of character identity and attachment names.
                if(matrix.Weights.Any(w=>w.Weight>0.01f && IsAccessoryBranch(rig,names[w.Bone.Name])))continue;
                var region=matrix.Weights.Where(w=>w.Bone is MDL0BoneNode && w.Weight>0)
                    .GroupBy(w=>nearest[names[w.Bone.Name]]).Select(g=>new{Role=g.Key,Weight=g.Sum(w=>w.Weight)})
                    .OrderByDescending(g=>g.Weight).FirstOrDefault();
                if(region==null || region.Weight<0.35f || !groups.ContainsKey(region.Role))continue;
                NativeVector bind=matrix is MDL0BoneNode primary?primary.BindMatrix*vertex.Position:vertex.Position;
                groups[region.Role].Add(new SkinPoint{Weights=matrix.Weights.Where(w=>w.Bone is MDL0BoneNode && w.Weight>0)
                    .Select(w=>new SkinWeight{Joint=names[w.Bone.Name],Weight=w.Weight,Local=Vector(w.Bone.InverseBindMatrix*bind)}).ToArray()});
            }
            var patches=new Dictionary<string,string[]>{{"pelvis",new[]{"pelvis","spine","chest","neck"}},{"head",new[]{"head"}}};
            foreach(string side in new[]{"left","right"})
            {
                patches.Add(side+".lowerArm",new[]{side+".shoulder",side+".upperArm",side+".lowerArm"});
                patches.Add(side+".lowerLeg",new[]{side+".upperLeg",side+".lowerLeg"});
            }
            foreach(var patch in patches)
            {
                SkinPoint[] points=patch.Value.SelectMany(r=>groups[r]).ToArray();
                if(points.Length>=4)rig.Surfaces.Add(patch.Key,new ContactSurface{Role=patch.Key,Points=points});
            }
        }
        public static bool IsAccessoryBranch(Rig rig,int joint)
        {
            if(rig.Roles.Values.Contains(joint))return false;
            int branch=joint,parent=rig.Joints[branch].Parent;
            while(parent>=0 && !rig.Roles.Values.Contains(parent)){branch=parent;parent=rig.Joints[parent].Parent;}
            if(parent<0 || !new[]{"head","neck"}.Any(r=>rig.Roles.TryGetValue(r,out int i)&&i==parent))return false;
            float radius=Math.Max(rig.HeadRadius,Vector3.Distance(rig.Rest.Point("head"),rig.Rest.Point("neck")));
            return Enumerable.Range(0,rig.Joints.Length).Where(i=>IsDescendant(rig,i,branch)).Any(i=>Vector3.Distance(rig.Rest.Point(i),rig.Rest.Point("head"))>radius*2.5f);
        }
        private static bool IsDescendant(Rig rig, int joint, int ancestor)
        { while (joint >= 0) { if (joint == ancestor) return true; joint=rig.Joints[joint].Parent; } return false; }

        public static RigPose Sample(BrawlRig source, CHR0Node clip, float keyFrame)
        {
            var tracks = clip.Children.OfType<CHR0EntryNode>().ToDictionary(e => e.Name, StringComparer.Ordinal);
            var locals = new JointTransform[source.Bones.Length];
            for (int i = 0; i < locals.Length; i++)
            {
                FrameState state = source.Bones[i].BindState;
                if (tracks.TryGetValue(source.Bones[i].Name, out CHR0EntryNode track))
                    for (int channel = 0; channel < 9; channel++)
                        if (track.Keyframes[channel].KeyCount > 0) state[channel] = track.GetFrameValue(channel, keyFrame);
                locals[i] = Transform(state);
            }
            return new RigPose(source.Rig, locals);
        }

        public static RetargetResult Bake(MDL0Node sourceModel, MDL0Node targetModel, CHR0Node clip, bool correction = true,GroundContactMode groundMode=GroundContactMode.Auto,
            RetargetKeyframeMode keyframeMode=RetargetKeyframeMode.Dense)
        {
            BrawlRig source = Capture(sourceModel), target = Capture(targetModel);
            return Bake(source, target, clip, correction,default,null,groundMode,keyframeMode);
        }

        public static RetargetResult Bake(BrawlRig source, BrawlRig target, CHR0Node clip, bool correction = true,
            CancellationToken cancellation=default,Action<int,string> progress=null,GroundContactMode groundMode=GroundContactMode.Auto,
            RetargetKeyframeMode keyframeMode=RetargetKeyframeMode.Dense)
        {
            Interlocked.Increment(ref _diagnosticBakes);
            cancellation.ThrowIfCancellationRequested();
            if(!Enum.IsDefined(typeof(GroundContactMode),groundMode))throw new ArgumentOutOfRangeException(nameof(groundMode));
            if(!Enum.IsDefined(typeof(RetargetKeyframeMode),keyframeMode))throw new ArgumentOutOfRangeException(nameof(keyframeMode));
            if (clip == null || clip.FrameCount < 1 || clip.FrameCount > 4096) throw new NotSupportedException("Select a CHR0 body animation with 1–4096 frames.");
            var plan = new HumanoidRetargeter(source.Rig, target.Rig);
            ValidateSource(source, clip);
            var output = new CHR0Node {Name = clip.Name, FrameCount = clip.FrameCount, Loop = clip.Loop};
            var result = new RetargetResult {Animation = output, Plan = plan, SourceRig = source.Fingerprint, TargetRig = target.Fingerprint,
                IgnoredSourceTracks = clip.Children.Count(c => !plan.Mapping.Any(m => m.Source == c.Name)),
                IgnoredSourceBones = clip.Children.Where(c => !plan.Mapping.Any(m => m.Source == c.Name)).Select(c=>c.Name).ToArray()};
            try
            {
                // Quarter-frame evaluation unwraps rotations continuously before integer-frame baking.
                int last = clip.Loop ? clip.FrameCount : clip.FrameCount - 1;
                progress?.Invoke(0,"Analyzing source contacts...");
                result.Contacts = new ContactMotion(plan, time=>Sample(source,clip,time),last,
                    correction && groundMode!=GroundContactMode.Airborne,cancellation,groundMode==GroundContactMode.Auto,groundMode!=GroundContactMode.SourceContacts,progress,
                    groundMode==GroundContactMode.Auto,clip.Loop);
                cancellation.ThrowIfCancellationRequested();
                const int samplesPerFrame=4;
                var values = new List<float[][]>();
                Vector3[] previous = null;
                for (int sample = 0; sample <= last * samplesPerFrame; sample++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if(sample%samplesPerFrame==0)progress?.Invoke(15+sample*40/Math.Max(1,last*samplesPerFrame),"Solving mapped motion and ground contacts...");
                    RetargetPose posed = correction ? result.Contacts.Evaluate(sample / (float)samplesPerFrame) : plan.Evaluate(Sample(source, clip, sample / (float)samplesPerFrame),false);
                    result.MaximumContactResidual=Math.Max(result.MaximumContactResidual,posed.MaximumContactResidual);
                    result.MaximumPelvisCorrection=Math.Max(result.MaximumPelvisCorrection,posed.PelvisCorrection.Length());
                    result.MaximumLimbResidual = Math.Max(result.MaximumLimbResidual, posed.MaximumLimbResidual);
                    result.ClampedLimbSamples += posed.ClampedLimbs;
                    float[][] frame = new float[target.Bones.Length][];
                    var angles = new Vector3[target.Bones.Length];
                    for (int bone = 0; bone < frame.Length; bone++)
                    {
                        JointTransform local = posed.Pose.Local[bone];
                        Vector3 euler = Euler(local.Rotation);
                        if (previous != null) euler = ContinuousEuler(euler, previous[bone]);
                        angles[bone] = euler;
                        frame[bone] = new[] {local.Scale.X, local.Scale.Y, local.Scale.Z, euler.X, euler.Y, euler.Z,
                            local.Translation.X, local.Translation.Y, local.Translation.Z};
                        if (frame[bone].Any(v => !RigMath.Finite(v))) throw new InvalidDataException("The retarget result contains non-finite keys.");
                    }
                    previous = angles; values.Add(frame);
                }
                for (int bone = 0; bone < target.Bones.Length; bone++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    progress?.Invoke(55+bone*10/target.Bones.Length,"Fitting native animation curves...");
                    CHR0EntryNode entry = output.CreateEntry(target.Bones[bone].Name);
                    entry.Keyframes.PreserveInterpolation = true;
                    for (int channel = 0; channel < 9; channel++)
                    {
                        float[] tangents = FitTangents(values, bone, channel, last,samplesPerFrame);
                        for (int index = 0; index <= last; index++)
                        {
                            int sample = index * samplesPerFrame;
                            var key = entry.SetKeyframe(channel, index, values[sample][bone][channel], true);
                            key._tangent = tangents[index];
                        }
                    }
                }
                FitContactCurves(target,output,result.Contacts,last,cancellation,progress);
                PreserveMotionControls(plan,clip,output,source,target,last);
                // The original dense solver, contact fit and source-owned
                // controls above remain the reference. Sparse storage is an
                // explicit, separately measured representation of that result.
                if(keyframeMode!=RetargetKeyframeMode.Dense)
                    RetargetKeyframes.Prepare(result,source,target,clip,keyframeMode,cancellation,progress);
                progress?.Invoke(100,"Animation ready");
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        // Native CHR0 has integer key times. Fit shared Hermite tangents to the
        // quarter-frame solver poses, keeping integer poses and original duration.
        private static float[] FitTangents(List<float[][]> values, int bone, int channel, int last,int samplesPerFrame)
        {
            var result = new float[last + 1];
            if (last == 0) return result;
            var diagonal = new double[last + 1]; var off = new double[last]; var rhs = new double[last + 1];
            for (int frame = 0; frame < last; frame++)
                for (int quarter = 1; quarter < samplesPerFrame; quarter++)
                {
                    double t = quarter / (double)samplesPerFrame, t2 = t * t, t3 = t2 * t;
                    double a = t3 - 2 * t2 + t, b = t3 - t2;
                    double residual = values[frame * samplesPerFrame + quarter][bone][channel] -
                        (2 * t3 - 3 * t2 + 1) * values[frame * samplesPerFrame][bone][channel] -
                        (-2 * t3 + 3 * t2) * values[(frame + 1) * samplesPerFrame][bone][channel];
                    diagonal[frame] += a * a; diagonal[frame + 1] += b * b; off[frame] += a * b;
                    rhs[frame] += a * residual; rhs[frame + 1] += b * residual;
                }
            for (int frame = 1; frame <= last; frame++)
            {
                double factor = off[frame - 1] / diagonal[frame - 1];
                diagonal[frame] -= factor * off[frame - 1]; rhs[frame] -= factor * rhs[frame - 1];
            }
            result[last] = (float)(rhs[last] / diagonal[last]);
            for (int frame = last - 1; frame >= 0; frame--) result[frame] = (float)((rhs[frame] - off[frame] * result[frame + 1]) / diagonal[frame]);
            return result;
        }

        private static void ValidateSource(BrawlRig source, CHR0Node clip)
        {
            if (!clip.Children.Any(c => source.Rig.Roles.Values.Any(i=>source.Bones[i].Name==c.Name)))
                throw new InvalidDataException("The selected clip has no mapped skeletal channels for this source model.");
            var errors = new List<float>();
            foreach (CHR0EntryNode track in clip.Children.OfType<CHR0EntryNode>())
            {
                MDL0BoneNode bone = source.Bones.FirstOrDefault(b => b.Name == track.Name);
                if (bone == null || bone.BindState.Translate.TrueDistance(new NativeVector()) < 0.25f) continue;
                Vector3 translation = Vector(bone.BindState.Translate); bool any = false, constant = true;
                for (int channel = 6; channel < 9; channel++)
                {
                    if (track.Keyframes[channel].KeyCount == 0) continue;
                    any = true;
                    float a = track.GetFrameValue(channel, 0), b = track.GetFrameValue(channel, (clip.FrameCount - 1) * 0.5f), c = track.GetFrameValue(channel, clip.FrameCount - 1);
                    if (Math.Max(Math.Abs(a - b), Math.Abs(a - c)) >= 0.001f) constant = false;
                    if (channel == 6) translation.X = a;
                    if (channel == 7) translation.Y = a;
                    if (channel == 8) translation.Z = a;
                }
                if (any && constant) errors.Add(Vector3.Distance(translation, Vector(bone.BindState.Translate)) / bone.BindState.Translate.TrueDistance(new NativeVector()));
            }
            errors.Sort();
            if (errors.Count >= 12 && errors[errors.Count / 2] > 0.12f)
                throw new InvalidDataException("The motion's bind translations do not match the chosen source model. Choose its actual source skeleton.");
        }

        public static JointTransform Transform(FrameState state) => new JointTransform(Vector(state.Translate), Rotation(state.Rotate), Vector(state.Scale));
        public static Vector3 Vector(NativeVector v) => new Vector3(v._x, v._y, v._z);
        public static Quaternion Rotation(NativeVector degrees)
        {
            NativeMatrix m = NativeMatrix.RotationMatrix(degrees);
            return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(new Matrix4x4(
                m[0,0], m[1,0], m[2,0], 0, m[0,1], m[1,1], m[2,1], 0,
                m[0,2], m[1,2], m[2,2], 0, 0, 0, 0, 1)));
        }
        public static Vector3 Euler(Quaternion q)
        {
            Matrix4x4 m = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(q));
            double y = Math.Asin(RigMath.Clamp(-m.M13, -1, 1)), x, z;
            if (Math.PI / 2 - Math.Abs(y) < 1e-6)
            { z = 0; x = y > 0 ? Math.Atan2(m.M21, m.M31) : Math.Atan2(-m.M21, -m.M31); }
            else { x = Math.Atan2(m.M23, m.M33); z = Math.Atan2(m.M12, m.M11); }
            return new Vector3((float)x, (float)y, (float)z) * (180f / (float)Math.PI);
        }
        private static Vector3 ContinuousEuler(Vector3 value, Vector3 previous)
        {
            Vector3 first = Near(value, previous), second = Near(new Vector3(value.X + 180, 180 - value.Y, value.Z + 180), previous);
            return Vector3.DistanceSquared(first, previous) <= Vector3.DistanceSquared(second, previous) ? first : second;
        }
        private static Vector3 Near(Vector3 v, Vector3 p) => new Vector3(Near(v.X,p.X), Near(v.Y,p.Y), Near(v.Z,p.Z));
        private static float Near(float v, float p) => v + 360 * (float)Math.Round((p - v) / 360);
        private static IEnumerable<ResourceNode> Walk(ResourceNode node)
        {
            if(node==null)yield break;
            yield return node;
            foreach (ResourceNode child in node.Children)
                foreach (ResourceNode item in Walk(child)) yield return item;
        }
    }
}
