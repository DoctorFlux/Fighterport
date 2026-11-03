using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    public static partial class BrawlRetargeter
    {
        // The original Next pose solver and native curve baker consume a
        // verified BT3 sampler directly. The source stays pmdl/BPE; this
        // method creates only an independent Brawl target CHR0 candidate.
        internal static RetargetResult BakeBt3(Bt3SourceLease lease,BrawlRig target,
            CancellationToken cancellation=default,Action<int,string> progress=null)
        {
            if(lease==null||target?.Rig==null||target.Bones==null)throw new ArgumentNullException(lease==null?nameof(lease):nameof(target));
            lease.VerifyUnchanged(cancellation);
            var source=lease.Source;int last=source.Motion.Length;
            if(source.Motion.Tracks.Any(track=>track.Keys.Any(key=>key.Frame>last)))
                throw new InvalidDataException("This BT3 action has an authored key beyond its sampled endpoint. Its terminal-frame behavior needs review before a Brawl draft can be made.");
            if(last<0||last>=4096||9L*(last+1)*target.Bones.Length>2000000)
                throw new InvalidDataException("This BT3 action exceeds the qualified native target bake bounds.");
            var bindings=Bt3TrajectoryBinding.Prepare(source,target.Rig);
            var details=Bt3DetailBinding.Prepare(source,target.Rig);
            var plan=new HumanoidRetargeter(source.Rig,target.Rig,bindings,details);
            var mapped=new HashSet<int>(plan.Mapping.Select(m=>m.TargetIndex));
            var carriedLegJoints=new HashSet<int>();
            foreach(string side in new[]{"left","right"})
            {
                if(!source.Rig.Roles.TryGetValue(side+".upperLeg",out int thigh)||
                    !source.Rig.Roles.TryGetValue(side+".lowerLeg",out int knee))continue;
                for(int joint=source.Rig.Joints[knee].Parent;joint>=0&&joint!=thigh;
                    joint=source.Rig.Joints[joint].Parent)
                    if(source.Rig.Ancestors[joint][thigh])carriedLegJoints.Add(joint);
            }
            var output=new CHR0Node{Name="BT3 Action "+lease.Action.ToString("D3"),FrameCount=last+1,Loop=false};
            var result=new RetargetResult{Animation=output,Plan=plan,SourceRig=source.Scene.Skeleton.SourceSha256,
                TargetRig=target.Fingerprint,KeyframeMode=RetargetKeyframeMode.Dense,
                IgnoredSourceBones=source.Rig.Joints.Where((joint,index)=>source.Motion.Tracks.Any(t=>t.Bone==source.Scene.Skeleton.Bones[index].Id)&&
                    !plan.Mapping.Any(m=>m.SourceIndex==index)&&!carriedLegJoints.Contains(index)).Select(joint=>joint.Name).ToArray()};
            result.IgnoredSourceTracks=result.IgnoredSourceBones.Length;
            try
            {
                bool floorReady=Bt3StaticStance(source,target.Rig,last,cancellation);
                var standing=floorReady?new Bt3StandingCorrection(plan,source.Sample(0)):null;
                var support=GroundRelativeHeightTransfer.Available(source.Rig,plan.Target)?
                    new GroundRelativeHeightTransfer(source.Rig,plan.Target,plan.LegScale):null;
                // A cheap geometry-only preflight uses the same palm-plane
                // admission band as the full contact detector. Most motions
                // never bring a palm near the floor; avoid running the coupled
                // source analysis for those clips. Both run only during Bake.
                bool palmCandidate=Bt3HasPalmFloorCandidate(source,plan.Target,last,cancellation);
                var detectedContacts=palmCandidate?
                    new ContactMotion(plan,source.Sample,last,true,cancellation,
                        requireFloorEvidence:true,detectSuspended:false):null;
                // A shin or clothing patch crossing the inferred plane must
                // not silently turn an ordinary stance into a body-contact
                // solve. Admit this branch only with actual palm support;
                // body-only and ledge contexts need separate review.
                bool bodySupported=detectedContacts!=null&&detectedContacts.FloorEnabled&&
                    detectedContacts.PlantedHandSamples>0;
                result.Contacts=bodySupported?detectedContacts:
                    new ContactMotion(plan,source.Sample,last,false,cancellation);
                float maximumDecodedContactError=0;
                var parents=target.Rig.Joints.Select(j=>j.Parent).ToArray();
                var editable=Enumerable.Range(0,parents.Length).Select(mapped.Contains).ToArray();
                var originalTimes=source.Motion.Tracks.SelectMany(t=>t.Keys).Select(k=>(float)k.Frame)
                    .Where(t=>t>=0&&t<=last).Append(0f).Append((float)last).Distinct().OrderBy(t=>t).ToArray();
                var bindEuler=target.Bones.Select(b=>Euler(BrawlRetargeter.Rotation(b.BindState.Rotate))).ToArray();
                var countedTimes=new HashSet<int>();
                AdaptiveRotationPose Master(float time)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var sourcePose=source.Sample(time);
                    var posed=bodySupported?result.Contacts.Evaluate(time):plan.Evaluate(sourcePose,true);
                    Vector3 beforeSupport=posed.Pose.Point("pelvis");
                    if(!bodySupported){support?.Apply(sourcePose,posed.Pose);standing?.Apply(posed);}
                    result.MaximumLimbResidual=Math.Max(result.MaximumLimbResidual,posed.MaximumLimbResidual);
                    result.MaximumContactResidual=Math.Max(result.MaximumContactResidual,posed.MaximumContactResidual);
                    result.MaximumPelvisCorrection=Math.Max(result.MaximumPelvisCorrection,
                        Math.Max(posed.PelvisCorrection.Length(),
                            Vector3.Distance(beforeSupport,posed.Pose.Point("pelvis"))));
                    if(countedTimes.Add(BitConverter.ToInt32(BitConverter.GetBytes(time),0)))
                        result.ClampedLimbSamples+=posed.ClampedLimbs;
                    return new AdaptiveRotationPose{
                        EulerDegrees=posed.Pose.Local.Select((value,i)=>EulerNear(value.Rotation,bindEuler[i])).ToArray(),
                        WorldRotations=(Quaternion[])posed.Pose.Orientation.Clone(),
                        LocalRotations=posed.Pose.Local.Select(value=>value.Rotation).ToArray(),
                        Translations=posed.Pose.Local.Select(value=>value.Translation).ToArray(),
                        Scales=posed.Pose.Local.Select(value=>value.Scale).ToArray()};
                }
                progress?.Invoke(10,"Baking native BT3 source poses into target curves...");
                var bake=WindowedNativeCurveBaker.Bake(parents,editable,last,Master,
                    (bone,time)=>Rotation(target.Bones[bone].BindState.Rotate),cancellation,originalTimes,null,
                    (window,total)=>progress?.Invoke(10+window*80/total,"Checking target curves: section "+(window+1)+" of "+total+"..."),
                    translationTolerance:.0001);
                if(!bake.Converged)
                    throw new InvalidDataException("BT3 target curves did not meet the native fractional bake bound. First unresolved bone "+
                        bake.FirstUnresolvedBone+"/"+bake.FirstUnresolvedChannel+" at "+bake.FirstUnresolvedFirstFrame+".."+
                        bake.FirstUnresolvedLastFrame+"; work "+bake.Work+"; no animation was imported.");
                foreach(var track in bake.Tracks)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var bone=target.Bones[track.Bone];var entry=output.CreateEntry(bone.Name);
                    entry.Keyframes.PreserveInterpolation=true;
                    entry.ScaleCompensateApply=bone.SegScaleCompApply;
                    entry.ScaleCompensateParent=bone.SegScaleCompParent;
                    entry.ClassicScaleOff=!bone.ClassicScale;
                    void Write(AdaptiveVectorKey[] keys,int first)
                    {
                        if(keys==null)return;
                        for(int axis=0;axis<3;axis++)foreach(var key in keys)
                        {
                            float Component(Vector3 value)=>axis==0?value.X:axis==1?value.Y:value.Z;
                            var array=entry.KeyArrays[first+axis];
                            var incoming=array.SetFrameValue(key.Time,Component(key.Value),true);
                            incoming._tangent=Component(key.IncomingSlope);
                            if(BitConverter.ToInt32(BitConverter.GetBytes(Component(key.IncomingSlope)),0)!=
                                BitConverter.ToInt32(BitConverter.GetBytes(Component(key.OutgoingSlope)),0))
                                array.SetFrameValue(key.Time,Component(key.Value),true)._tangent=Component(key.OutgoingSlope);
                        }
                    }
                    Write(track.Scale,0);Write(track.Rotation,3);Write(track.Translation,6);
                    entry.UseModelScale=track.Scale==null;
                    entry.UseModelRotate=track.Rotation==null;
                    entry.UseModelTranslate=track.Translation==null;
                }
                PreciseCHR0EntryNode.Promote(output);
                progress?.Invoke(92,"Reopening and comparing the target animation...");
                WindowedNativeCurveBaker.VerifyDecoded(target,output,bake,Master,cancellation,
                    (actual,time)=>{
                        if(bodySupported)
                            maximumDecodedContactError=Math.Max(maximumDecodedContactError,
                                VerifyBt3DecodedContacts(result.Contacts,target.Rig,actual,time));
                        else if(standing!=null)standing.VerifyDecoded(actual,time);
                        else support?.VerifyDecoded(source.Sample(time),actual,time);
                    });
                if(bodySupported)
                {
                    BrawlCrate.Studio.NativeSupportEvidence.Freeze(result);
                    if(result.FrozenSupportEvidence?.Json==null)
                        throw new InvalidDataException("BT3 hand/body support cannot be published without complete bounded review context: "+
                            result.FrozenSupportEvidence?.UnavailableReason);
                }
                lease.VerifyUnchanged(cancellation);
                result.OptimizationReport="BT3 native source sampled directly; "+bake.ValidationTimes.Length+
                    " decoded target witnesses; maximum sampled world orientation "+bake.MaximumOrientationDegrees+
                    " degrees. Source-local actor controls: "+Bt3TrajectoryBinding.Policy+
                    ". Weighted hand/body support "+(bodySupported?
                        "verified; planted hand/body samples "+result.Contacts.PlantedHandSamples+"/"+
                        result.Contacts.PlantedBodySamples+"; maximum decoded surface/lock error "+maximumDecodedContactError:
                        "unavailable")+
                    ". Static weighted-foot correction "+(standing!=null&&!bodySupported?
                        "verified; maximum master/decoded patch error "+standing.MaximumError+"/"+
                        standing.MaximumDecodedError:"unavailable")+
                    "; source-proportional foot height "+(support!=null&&!bodySupported&&standing==null?
                        "verified; maximum decoded difference "+support.MaximumDecodedError:
                        "unavailable")+
                    "; game acceptance remains unreviewed.";
                progress?.Invoke(100,"Verified target animation ready for review");
                return result;
            }
            catch{result.Dispose();throw;}
        }

        private static bool Bt3HasPalmFloorCandidate(Bt3SourceRig source,Rig target,int last,CancellationToken cancellation)
        {
            foreach(string side in new[]{"left","right"})
            {
                string role=side+".hand";
                if(!source.Rig.Surfaces.ContainsKey(role)||!target.Surfaces.ContainsKey(role))continue;
                float length=Vector3.Distance(source.Rig.Rest.Point(side+".upperArm"),
                    source.Rig.Rest.Point(side+".lowerArm"))+
                    Vector3.Distance(source.Rig.Rest.Point(side+".lowerArm"),source.Rig.Rest.Point(role));
                if(!RigMath.Finite(length)||length<=0)continue;
                for(int quarter=0;quarter<=last*4;quarter++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var pose=source.Sample(quarter*.25f);
                    float clearance=source.Rig.Surfaces[role].Sample(pose).Clearance;
                    float jointHeight=Vector3.Dot(pose.Point(role),source.Rig.GroundNormal)-source.Rig.GroundHeight;
                    if(RigMath.Finite(clearance)&&RigMath.Finite(jointHeight)&&
                        clearance>=-.03f*length&&clearance<=.02f*length&&
                        Math.Abs(jointHeight)<.3f*length)return true;
                }
            }
            return false;
        }

        private static float VerifyBt3DecodedContacts(ContactMotion contacts,Rig target,RigPose pose,float time)
        {
            float maximum=0;
            Dictionary<string,Quaternion> endOrientations=null;
            foreach(var surface in target.Surfaces)
            {
                var goal=contacts.At(surface.Key,time);
                if(goal.Support<=.99f)continue;
                string side=surface.Key.StartsWith("left.",StringComparison.Ordinal)?"left":
                    surface.Key.StartsWith("right.",StringComparison.Ordinal)?"right":null;
                bool hand=surface.Key.EndsWith(".hand",StringComparison.Ordinal);
                bool foot=surface.Key.EndsWith(".foot",StringComparison.Ordinal);
                float length=(hand||foot)&&side!=null&&target.Roles.ContainsKey(side+(hand?".upperArm":".upperLeg"))?
                    Vector3.Distance(target.Rest.Point(side+(hand?".upperArm":".upperLeg")),
                        target.Rest.Point(side+(hand?".lowerArm":".lowerLeg")))+
                    Vector3.Distance(target.Rest.Point(side+(hand?".lowerArm":".lowerLeg")),
                        target.Rest.Point(surface.Key)):
                    Vector3.Distance(target.Rest.Point("left.upperLeg"),target.Rest.Point("left.foot"));
                float bound=.001f*length;
                float error=Math.Abs(surface.Value.Sample(pose).Clearance);
                if(goal.Lock>.99f&&goal.Marker!=null)
                {
                    Vector3 drift=goal.Marker.Evaluate(pose)-goal.Anchor;
                    drift-=target.GroundNormal*Vector3.Dot(drift,target.GroundNormal);
                    error=Math.Max(error,drift.Length());
                }
                maximum=Math.Max(maximum,error);
                if(!RigMath.Finite(error)||error>bound)
                    throw new InvalidDataException("Decoded BT3 target lost reviewed "+surface.Key+
                        " support at frame "+time+"; error "+error+" exceeds "+bound+".");
                if(hand||foot)
                {
                    if(endOrientations==null)endOrientations=contacts.EndOrientationReference(time);
                    if(endOrientations.TryGetValue(surface.Key,out Quaternion orientation)&&
                        ContactMotion.EndOrientationDegrees(pose.Orientation[target.Roles[surface.Key]],
                            orientation)>.25)
                        throw new InvalidDataException("Decoded BT3 target lost reviewed "+surface.Key+
                            " orientation at frame "+time+".");
                }
            }
            return maximum;
        }

        // Static bilateral stance has a stricter anchor gate than source
        // palm support. Full action/contact and game-floor acceptance still
        // need separate review; crossing the bind plane is not itself support.
        private static bool Bt3StaticStance(Bt3SourceRig source,Rig target,int last,CancellationToken cancel)
        {
            var roles=new[]{"left.foot","right.foot"};
            if(roles.Any(role=>!source.Rig.Surfaces.ContainsKey(role)||!target.Surfaces.ContainsKey(role)))return false;
            var first=source.Sample(0);
            var anchors=roles.ToDictionary(role=>role,role=>source.Rig.Surfaces[role].Sample(first).Point);
            for(int quarter=0;quarter<=last*4;quarter++)
            {
                cancel.ThrowIfCancellationRequested();
                var pose=source.Sample(quarter*.25f);
                foreach(string role in roles)
                {
                    string side=role.Split('.')[0];
                    float length=Vector3.Distance(source.Rig.Rest.Point(side+".upperLeg"),source.Rig.Rest.Point(side+".lowerLeg"))+
                        Vector3.Distance(source.Rig.Rest.Point(side+".lowerLeg"),source.Rig.Rest.Point(role));
                    var patch=source.Rig.Surfaces[role].Sample(pose);
                    Vector3 drift=patch.Point-anchors[role];
                    drift-=source.Rig.GroundNormal*Vector3.Dot(drift,source.Rig.GroundNormal);
                    float hipHeight=Vector3.Dot(pose.Point("pelvis"),source.Rig.GroundNormal)-source.Rig.GroundHeight;
                    if(!RigMath.Finite(patch.Clearance)||patch.Clearance<-.015f*length||
                        patch.Clearance>.015f*length||drift.Length()>.01f*length||
                        hipHeight<.6f*length)return false;
                }
            }
            return true;
        }

        private sealed class Bt3StandingCorrection
        {
            private readonly HumanoidRetargeter plan;
            private readonly Vector3 up;
            private readonly Dictionary<string,Vector3> anchors=new Dictionary<string,Vector3>();
            private readonly Dictionary<string,SkinPoint> markers=new Dictionary<string,SkinPoint>();
            internal float MaximumError {get;private set;}
            internal float MaximumDecodedError {get;private set;}
            internal Bt3StandingCorrection(HumanoidRetargeter plan,RigPose first)
            {
                this.plan=plan;up=plan.Target.GroundNormal;
                var posed=plan.Evaluate(first,true).Pose;
                string[] roles={"left.foot","right.foot"};
                float shift=-roles.Average(role=>plan.Target.Surfaces[role].Sample(posed).Clearance);
                posed.SetWorldPoint(plan.Target.Roles["pelvis"],posed.Point("pelvis")+up*shift);
                foreach(string role in roles)
                {
                    var marker=plan.Target.Surfaces[role].Marker(posed);
                    markers.Add(role,marker);
                    Vector3 point=marker.Evaluate(posed);
                    anchors.Add(role,point+up*(plan.Target.GroundHeight-Vector3.Dot(point,up)));
                }
            }
            internal void Apply(RetargetPose result)
            {
                var pose=result.Pose;
                string[] roles={"left.foot","right.foot"};
                float shift=-roles.Average(role=>plan.Target.Surfaces[role].Sample(pose).Clearance);
                pose.SetWorldPoint(plan.Target.Roles["pelvis"],pose.Point("pelvis")+up*shift);
                foreach(string role in new[]{"left.foot","right.foot"})
                {
                    string side=role.Split('.')[0];
                    float length=Vector3.Distance(plan.Target.Rest.Point(side+".upperLeg"),plan.Target.Rest.Point(side+".lowerLeg"))+
                        Vector3.Distance(plan.Target.Rest.Point(side+".lowerLeg"),plan.Target.Rest.Point(role));
                    for(int pass=0;pass<5;pass++)
                    {
                        var surface=plan.Target.Surfaces[role].Sample(pose);
                        Vector3 planar=anchors[role]-markers[role].Evaluate(pose);
                        planar-=up*Vector3.Dot(planar,up);
                        Vector3 delta=planar-up*surface.Clearance;
                        if(delta.Length()>length*.02f)
                            throw new InvalidDataException("The static BT3 foot would need an unreviewed target-limb stretch.");
                        pose.SetWorldPoint(plan.Target.Roles[role],pose.Point(role)+delta);
                    }
                    var final=plan.Target.Surfaces[role].Sample(pose);
                    Vector3 remaining=anchors[role]-markers[role].Evaluate(pose);
                    remaining-=up*Vector3.Dot(remaining,up);
                    float error=Math.Max(Math.Abs(final.Clearance),remaining.Length());
                    MaximumError=Math.Max(MaximumError,error);
                    result.MaximumContactResidual=Math.Max(result.MaximumContactResidual,error);
                    if(error>length*.001f)
                        throw new InvalidDataException("The static BT3 foot anchor did not meet the reviewed target contact bound.");
                    result.Goals[role]=pose.Point(role);
                }
            }
            internal void VerifyDecoded(RigPose pose,float time)
            {
                foreach(string role in new[]{"left.foot","right.foot"})
                {
                    string side=role.Split('.')[0];
                    float length=Vector3.Distance(plan.Target.Rest.Point(side+".upperLeg"),plan.Target.Rest.Point(side+".lowerLeg"))+
                        Vector3.Distance(plan.Target.Rest.Point(side+".lowerLeg"),plan.Target.Rest.Point(role));
                    Vector3 planar=anchors[role]-markers[role].Evaluate(pose);
                    planar-=up*Vector3.Dot(planar,up);
                    float error=Math.Max(Math.Abs(plan.Target.Surfaces[role].Sample(pose).Clearance),planar.Length());
                    MaximumDecodedError=Math.Max(MaximumDecodedError,error);
                    if(error>length*.001f)
                        throw new InvalidDataException("Decoded BT3 target lost its static foot contact at frame "+time+", "+role+"; error "+error+".");
                }
            }
        }
    }
}
