using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio.Retargeting
{
    public sealed class RetargetMap
    {
        public string Role { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        public int SourceIndex { get; set; }
        public int TargetIndex { get; set; }
    }

    public sealed class RetargetPose
    {
        public RigPose Pose { get; internal set; }
        public float MaximumLimbResidual { get; internal set; }
        public int ClampedLimbs { get; internal set; }
        public float MaximumContactResidual { get; internal set; }
        public Vector3 PelvisCorrection { get; internal set; }
        public Dictionary<string, Vector3> Goals { get; } = new Dictionary<string, Vector3>();
    }

    public sealed class HumanoidRetargeter
    {
        public const string Algorithm = "bind-world-fk-body-contact-ik-v10";
        public const string MotionControlPolicy = "source-local-controls-v1";
        public int[] SourceMotionControls { get; }
        public int[] TargetMotionControls { get; }
        public Rig Source { get; }
        public Rig Target { get; }
        public RetargetMap[] Mapping { get; }
        public float LegScale { get; }
        public float ArmScale { get; }
        public float HeadScale { get; }
        public Quaternion Calibration { get; }
        public string CalibrationEvidence => CoordinateEvidence(Source) == CoordinateEvidence(Target)
            ? CoordinateEvidence(Source) : "source: " + CoordinateEvidence(Source) + "; target: " + CoordinateEvidence(Target);
        private static string CoordinateEvidence(Rig rig) => rig.CoordinateFrame.HasValue ? "reviewed-coordinate-frame-v1" :
            rig.CoordinateUp.HasValue ? "declared-coordinate-up/anatomical-heading-v1" : "legacy-anatomical-rest-fallback";
        public string[] UnmappedSource { get; }
        public string[] UnmappedTarget { get; }
        private readonly Dictionary<int, RetargetMap> _byTarget;
        private readonly RetargetMap trajectory;
        private readonly Dictionary<int,Tuple<Quaternion,Quaternion>> localFrames=new Dictionary<int,Tuple<Quaternion,Quaternion>>();

        public HumanoidRetargeter(Rig source, Rig target)
        {
            Source = source; Target = target;
            // Native adapters may map motion pivots, toes, fingers and props,
            // but the solver must not require a Brawl-specific helper chain.
            var required = new List<string> {"pelvis", "spine", "chest", "neck", "head"};
            foreach (string side in new[] {"left", "right"})
                required.AddRange(new[] {"upperArm", "lowerArm", "hand", "upperLeg", "lowerLeg", "foot"}.Select(n => side + "." + n));
            if (required.Any(r => !source.Roles.ContainsKey(r) || !target.Roles.ContainsKey(r)))
                throw new InvalidDataException("This rig is missing a required humanoid role. No motion was generated.");
            Mapping = source.Roles.Keys.Intersect(target.Roles.Keys).Select(role => new RetargetMap {
                Role = role, SourceIndex = source.Roles[role], TargetIndex = target.Roles[role],
                Source = source.Joints[source.Roles[role]].Name, Target = target.Joints[target.Roles[role]].Name
            }).OrderBy(m => m.TargetIndex).ToArray();
            foreach (RetargetMap map in Mapping)
            {
                string a = NearestRole(source, source.Joints[map.SourceIndex].Parent);
                string b = NearestRole(target, target.Joints[map.TargetIndex].Parent);
                if (a != b) throw new InvalidDataException("Incompatible humanoid hierarchy at " + map.Role + ". Review is required.");
            }
            // Matching helper chains can carry trajectory even with anonymous
            // names. Pair only unambiguous equal-depth ancestor paths; anatomy
            // and confirmed semantic mappings always take precedence.
            int[] aPath=AncestorPath(source),bPath=AncestorPath(target);
            SourceMotionControls=aPath;TargetMotionControls=bPath;
            var mapping=Mapping.ToList();
            if(aPath.Length==bPath.Length)for(int i=0;i<aPath.Length;i++)
                if(!mapping.Any(m=>m.SourceIndex==aPath[i] || m.TargetIndex==bPath[i]))
                    mapping.Add(new RetargetMap{Role="trajectory."+i,SourceIndex=aPath[i],TargetIndex=bPath[i],
                        Source=source.Joints[aPath[i]].Name,Target=target.Joints[bPath[i]].Name});
            Mapping=mapping.OrderBy(m=>m.TargetIndex).ToArray();
            _byTarget = Mapping.ToDictionary(m => m.TargetIndex);
            trajectory=Mapping.LastOrDefault(IsTrajectory);
            LegScale = (ChainRatio("left", false) + ChainRatio("right", false)) / 2;
            ArmScale = (ChainRatio("left", true) + ChainRatio("right", true)) / 2;
            HeadScale = target.HeadRadius / source.HeadRadius;
            if (!RigMath.Finite(LegScale) || !RigMath.Finite(ArmScale) || !RigMath.Finite(HeadScale) || LegScale<=0 || ArmScale<=0 || HeadScale<=0)
                throw new InvalidDataException("The rig needs finite positive measured proportions.");
            Calibration = Quaternion.Normalize(RigMath.BodyFrame(target) * Quaternion.Inverse(RigMath.BodyFrame(source)));
            // Factor the same world-bind-relative FK equation into fixed local
            // bases when the actual direct parents correspond. Repeated world
            // quaternion cancellation otherwise adds frame-dependent roundoff to
            // an authored constant finger/helper channel, making it needlessly dense.
            foreach(var map in Mapping)
            {
                int sp=Source.Joints[map.SourceIndex].Parent,tp=Target.Joints[map.TargetIndex].Parent;
                if(sp<0||tp<0||TargetMotionControls.Contains(map.TargetIndex)||TargetMotionControls.Contains(tp)||
                    !_byTarget.TryGetValue(tp,out var parent)||parent.SourceIndex!=sp)continue;
                var left=Quaternion.Normalize(Quaternion.Inverse(Target.Rest.Orientation[tp])*Calibration*Source.Rest.Orientation[sp]);
                var right=Quaternion.Normalize(Quaternion.Inverse(Source.Rest.Orientation[map.SourceIndex])*Quaternion.Inverse(Calibration)*Target.Rest.Orientation[map.TargetIndex]);
                localFrames.Add(map.TargetIndex,Tuple.Create(left,right));
            }
            UnmappedSource = source.Joints.Where((j, i) => !Mapping.Any(m => m.SourceIndex == i)).Select(j => j.Name).ToArray();
            UnmappedTarget = target.Joints.Where((j, i) => !_byTarget.ContainsKey(i)).Select(j => j.Name).ToArray();
        }

        private string NearestRole(Rig rig, int joint)
        {
            while (joint >= 0)
            {
                string role = rig.Roles.Keys.FirstOrDefault(r => rig.Roles[r] == joint && Source.Roles.ContainsKey(r) && Target.Roles.ContainsKey(r));
                if (role != null) return role;
                joint = rig.Joints[joint].Parent;
            }
            return null;
        }
        private float ChainLength(Rig rig, string side, bool arm)
        {
            string a = side + (arm ? ".upperArm" : ".upperLeg"), b = side + (arm ? ".lowerArm" : ".lowerLeg"), c = side + (arm ? ".hand" : ".foot");
            float length = Vector3.Distance(rig.Rest.Point(a), rig.Rest.Point(b)) + Vector3.Distance(rig.Rest.Point(b), rig.Rest.Point(c));
            if (length < 0.01f) throw new InvalidDataException("A required limb has zero length.");
            return length;
        }
        private float ChainRatio(string side, bool arm) => ChainLength(Target, side, arm) / ChainLength(Source, side, arm);
        private static int[] AncestorPath(Rig rig)
        {var path=new List<int>();for(int i=rig.Joints[rig.Roles["pelvis"]].Parent;i>=0;i=rig.Joints[i].Parent)path.Add(i);path.Reverse();return path.ToArray();}
        public bool IsTrajectory(RetargetMap map)=>map.SourceIndex!=Source.Roles["pelvis"] && map.TargetIndex!=Target.Roles["pelvis"] &&
            Source.Ancestors[Source.Roles["pelvis"]][map.SourceIndex] && Target.Ancestors[Target.Roles["pelvis"]][map.TargetIndex];
        public Vector3 TransferTranslationDelta(RetargetMap map,Vector3 delta)
        {
            int parent=Source.Joints[map.SourceIndex].Parent,targetParent=Target.Joints[map.TargetIndex].Parent;
            Vector3 world=parent<0?delta:Vector3.Transform(delta,Source.Rest.Orientation[parent]);
            // Adapters normalize game-space axes/units before this solver.
            // A character's anatomical rest tilt must not rotate the actor's
            // gameplay route (for example mixing vertical travel into Z).
            if(!IsTrajectory(map))world=Convert(world)*(map.Role.Contains("Arm")||map.Role.Contains("hand")||map.Role.Contains("finger")?ArmScale:LegScale);
            return targetParent<0?world:Vector3.Transform(world,Quaternion.Inverse(Target.Rest.Orientation[targetParent]));
        }
        internal Vector3 TrajectoryOffset(RigPose source,RigPose target)=>trajectory==null?Vector3.Zero:
            target.Point(trajectory.TargetIndex)-Target.Rest.Point(trajectory.TargetIndex)-
            Convert(source.Point(trajectory.SourceIndex)-Source.Rest.Point(trajectory.SourceIndex))*LegScale;
        private Vector3 Convert(Vector3 v) => Vector3.Transform(v, Calibration);
        private Quaternion Convert(Quaternion q) => Quaternion.Normalize(Calibration * q * Quaternion.Inverse(Calibration));

        public RetargetPose Evaluate(RigPose source, bool proportionCorrection = true)
        {
            if (!ReferenceEquals(source.Rig, Source)) throw new ArgumentException("Pose does not belong to the source rig.");
            var target = new RigPose(Target);
            // Transfer world-space bind-relative orientation, then solve target-local FK.
            for (int joint = 0; joint < Target.Joints.Length; joint++)
            {
                if (!_byTarget.TryGetValue(joint, out RetargetMap map)) { target.UpdateJoint(joint); continue; }
                int s = map.SourceIndex, t = map.TargetIndex;
                if (TargetMotionControls.Contains(t))
                {
                    // Authored local motion only. A parent's animation is not
                    // an edit of this control, and body calibration cannot
                    // introduce a compensating rotation here.
                    int sp = Source.Joints[s].Parent, tp = Target.Joints[t].Parent;
                    Quaternion basis = Quaternion.Inverse(tp < 0 ? Quaternion.Identity : Target.Rest.Orientation[tp]) *
                        (sp < 0 ? Quaternion.Identity : Source.Rest.Orientation[sp]);
                    target.Local[t].Rotation = Quaternion.Normalize(basis *
                        (source.Local[s].Rotation * Quaternion.Inverse(Source.Joints[s].Bind.Rotation)) *
                        Quaternion.Inverse(basis) * Target.Joints[t].Bind.Rotation);
                    target.Local[t].Scale = Target.Joints[t].Bind.Scale * (source.Local[s].Scale / Source.Joints[s].Bind.Scale);
                    target.Local[t].Translation = Target.Joints[t].Bind.Translation +
                        TransferTranslationDelta(map, source.Local[s].Translation - Source.Joints[s].Bind.Translation);
                    target.UpdateJoint(t);
                    continue;
                }
                if(localFrames.TryGetValue(t,out var localFrame))
                    target.Local[t].Rotation=source.Local[s].Rotation==Source.Joints[s].Bind.Rotation?Target.Joints[t].Bind.Rotation:
                        Quaternion.Normalize(localFrame.Item1*source.Local[s].Rotation*localFrame.Item2);
                else
                {
                    Quaternion delta = Convert(source.Orientation[s] * Quaternion.Inverse(Source.Rest.Orientation[s]));
                    Quaternion desired = Quaternion.Normalize(delta * Target.Rest.Orientation[t]);
                    int parent = Target.Joints[t].Parent;
                    target.Local[t].Rotation = Quaternion.Normalize(parent < 0 ? desired : Quaternion.Inverse(target.Orientation[parent]) * desired);
                }
                target.Local[t].Scale = Target.Joints[t].Bind.Scale * (source.Local[s].Scale / Source.Joints[s].Bind.Scale);
                Vector3 translationDelta = source.Local[s].Translation - Source.Joints[s].Bind.Translation;
                target.Local[t].Translation = Target.Joints[t].Bind.Translation + TransferTranslationDelta(map,translationDelta);
                target.UpdateJoint(t);
            }
            // Scale anatomical displacement about the authored trajectory,
            // rather than multiplying gameplay travel by the leg ratio.
            int pelvis = Target.Roles["pelvis"];
            target.SetWorldPoint(pelvis, Target.Rest.Point("pelvis") + Convert(source.Point("pelvis") - Source.Rest.Point("pelvis")) * LegScale+TrajectoryOffset(source,target));
            var result = new RetargetPose {Pose = target};
            if (!proportionCorrection) return result;
            foreach (string side in new[] {"left", "right"})
            {
                CorrectLimb(source, target, result, side, false);
                CorrectLimb(source, target, result, side, true);
            }
            return result;
        }

        private void CorrectLimb(RigPose source, RigPose target, RetargetPose report, string side, bool arm)
        {
            string rootRole = side + (arm ? ".upperArm" : ".upperLeg");
            string middleRole = side + (arm ? ".lowerArm" : ".lowerLeg");
            string endRole = side + (arm ? ".hand" : ".foot");
            int s = Source.Roles[rootRole], t = Target.Roles[rootRole], m = Target.Roles[middleRole], end = Target.Roles[endRole];
            float ratio = ChainRatio(side, arm);
            Vector3 restResidual = Target.Rest.Point(endRole) - Target.Rest.Point(t) - Convert(Source.Rest.Point(endRole) - Source.Rest.Point(s)) * ratio;
            Quaternion rootDelta = Convert(source.Orientation[s] * Quaternion.Inverse(Source.Rest.Orientation[s]));
            Vector3 goal = target.Point(t) + Convert(source.Point(endRole) - source.Point(s)) * ratio + Vector3.Transform(restResidual, rootDelta);
            if (!arm)
            {
                // Preserve the source ankle's height relative to its own ground/rest level.
                Vector3 up = Vector3.Transform(Vector3.UnitY, RigMath.BodyFrame(Target));
                float height = Vector3.Dot(Target.Rest.Point(endRole) + Convert(source.Point(endRole) - Source.Rest.Point(endRole)) * LegScale+TrajectoryOffset(source,target), up);
                goal += up * (height - Vector3.Dot(goal, up));
            }
            else
            {
                // Close hand/head gestures use the head's proportions rather than long target arms.
                float near = Vector3.Distance(source.Point(endRole), source.Point("head")) / ChainLength(Source, side, true);
                float weight = RigMath.Clamp((0.95f - near) / 0.4f, 0, 1);
                Vector3 headGoal = target.Point("head") + Convert(source.Point(endRole) - source.Point("head")) * HeadScale;
                goal = Vector3.Lerp(goal, headGoal, weight);
            }
            report.Goals[endRole] = goal;
            SolveLimb(target, report, side, arm, goal);
        }

        internal void SolveLimb(RigPose target, RetargetPose report, string side, bool arm, Vector3 goal, Vector3? bendDirection=null)
        {
            int t=Target.Roles[side+(arm?".upperArm":".upperLeg")], m=Target.Roles[side+(arm?".lowerArm":".lowerLeg")], end=Target.Roles[side+(arm?".hand":".foot")];
            if (Vector3.Distance(target.Point(end), goal) < 1e-4f) return;
            Quaternion endOrientation = target.Orientation[end];
            Vector3 start = target.Point(t), elbow = target.Point(m), endpoint = target.Point(end);
            float a = Vector3.Distance(start, elbow), b = Vector3.Distance(elbow, endpoint), wanted = Vector3.Distance(start, goal);
            if (a < 1e-5 || b < 1e-5) throw new InvalidDataException("A posed limb collapsed to zero length.");
            float reach = RigMath.Clamp(wanted, Math.Abs(a - b) + 1e-4f, a + b - 1e-4f);
            if (Math.Abs(reach - wanted) > 0.001f) report.ClampedLimbs++;
            Vector3 direction = wanted < 1e-6 ? RigMath.Unit(endpoint - start) : (goal - start) / wanted;
            Vector3 pole;
            if(bendDirection.HasValue)pole=bendDirection.Value;
            else
            {
                // Preserve the old transverse bend, not the old elbow's long
                // component. A shifted goal can otherwise reverse a nearly
                // straight knee/elbow when that long component is reprojected.
                Vector3 oldAxis=endpoint-start;
                oldAxis=oldAxis.LengthSquared()>1e-10f?Vector3.Normalize(oldAxis):direction;
                pole=elbow-start;pole-=oldAxis*Vector3.Dot(pole,oldAxis);
            }
            pole -= direction * Vector3.Dot(pole, direction);
            if (pole.LengthSquared() < 1e-8f)
            {
                pole = Vector3.Transform(Vector3.UnitZ, RigMath.BodyFrame(Target));
                pole -= direction * Vector3.Dot(pole, direction);
                if (pole.LengthSquared() < 1e-8f) pole = Vector3.Cross(direction, Vector3.UnitX);
            }
            pole = RigMath.Unit(pole);
            float along = (a * a - b * b + reach * reach) / (2 * reach);
            Vector3 solvedMiddle = start + direction * along + pole * (float)Math.Sqrt(Math.Max(0, a * a - along * along));
            Vector3 reachableGoal = start + direction * reach;
            target.SetWorldRotation(t, RigMath.FromTo(elbow - start, solvedMiddle - start) * target.Orientation[t]);
            target.SetWorldRotation(m, RigMath.FromTo(target.Point(end) - target.Point(m), reachableGoal - target.Point(m)) * target.Orientation[m]);
            target.SetWorldRotation(end, endOrientation);
            report.MaximumLimbResidual = Math.Max(report.MaximumLimbResidual, Vector3.Distance(target.Point(end), goal));
        }
    }
}
