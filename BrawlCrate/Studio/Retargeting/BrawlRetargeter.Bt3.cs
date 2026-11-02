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
            var plan=new HumanoidRetargeter(source.Rig,target.Rig,bindings);
            var mapped=new HashSet<int>(plan.Mapping.Select(m=>m.TargetIndex));
            var output=new CHR0Node{Name="BT3 Action "+lease.Action.ToString("D3"),FrameCount=last+1,Loop=false};
            var result=new RetargetResult{Animation=output,Plan=plan,SourceRig=source.Scene.Skeleton.SourceSha256,
                TargetRig=target.Fingerprint,KeyframeMode=RetargetKeyframeMode.Dense,
                IgnoredSourceBones=source.Rig.Joints.Where((joint,index)=>source.Motion.Tracks.Any(t=>t.Bone==source.Scene.Skeleton.Bones[index].Id)&&
                    !plan.Mapping.Any(m=>m.SourceIndex==index)).Select(joint=>joint.Name).ToArray()};
            result.IgnoredSourceTracks=result.IgnoredSourceBones.Length;
            try
            {
                result.Contacts=new ContactMotion(plan,source.Sample,last,false,cancellation);
                var parents=target.Rig.Joints.Select(j=>j.Parent).ToArray();
                var editable=Enumerable.Range(0,parents.Length).Select(mapped.Contains).ToArray();
                var originalTimes=source.Motion.Tracks.SelectMany(t=>t.Keys).Select(k=>(float)k.Frame)
                    .Where(t=>t>=0&&t<=last).Append(0f).Append((float)last).Distinct().OrderBy(t=>t).ToArray();
                var bindEuler=target.Bones.Select(b=>Euler(BrawlRetargeter.Rotation(b.BindState.Rotate))).ToArray();
                var countedTimes=new HashSet<int>();
                AdaptiveRotationPose Master(float time)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var posed=plan.Evaluate(source.Sample(time),true);
                    result.MaximumLimbResidual=Math.Max(result.MaximumLimbResidual,posed.MaximumLimbResidual);
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
                WindowedNativeCurveBaker.VerifyDecoded(target,output,bake,Master,cancellation);
                lease.VerifyUnchanged(cancellation);
                result.OptimizationReport="BT3 native source sampled directly; "+bake.ValidationTimes.Length+
                    " decoded target witnesses; maximum sampled world orientation "+bake.MaximumOrientationDegrees+
                    " degrees. Source-local actor controls: "+Bt3TrajectoryBinding.Policy+
                    ". Contact and game acceptance remain unreviewed.";
                progress?.Invoke(100,"Verified target animation ready for review");
                return result;
            }
            catch{result.Dispose();throw;}
        }
    }
}
