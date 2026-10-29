using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // A native-adapter fixture removes a non-anatomical leaf from the source
    // rig data while preserving the complete real target and all its weights.
    // No source asset/model is edited, and no role is guessed from a bone name.
    static void SharedRetargetTrackChecks(MDL0Node model,string output)
    {
        var target=BrawlRetargeter.Capture(model);
        var identity=new HumanoidRetargeter(target.Rig,target.Rig);
        int excluded=Enumerable.Range(0,target.Bones.Length).First(i=>!identity.Mapping.Any(m=>m.TargetIndex==i)
            && !target.Rig.Joints.Any(j=>j.Parent==i));
        int[] order=Enumerable.Range(0,target.Bones.Length).Where(i=>i!=excluded).ToArray();
        var remap=order.Select((old,index)=>new {old,index}).ToDictionary(p=>p.old,p=>p.index);
        var sourceBones=order.Select((old,index)=> {var bone=target.Bones[old].Clone();bone.Name="donor_joint_"+(index*37+19);return bone;}).ToArray();
        var joints=order.Select((old,index)=>new RigJoint(sourceBones[index].Name,
            target.Rig.Joints[old].Parent<0?-1:remap[target.Rig.Joints[old].Parent],target.Rig.Joints[old].Bind)).ToArray();
        var source=new BrawlRig {Bones=sourceBones,Fingerprint="procedural-source-with-one-absent-leaf",
            Rig=new Rig("Independent source subset","reviewed original roles",joints,
                target.Rig.Roles.ToDictionary(p=>p.Key,p=>remap[p.Value]),target.Rig.HeadRadius,target.Rig.CoordinateUp,target.Rig.CoordinateFrame)};
        var plan=new HumanoidRetargeter(source.Rig,target.Rig);
        string excludedName=target.Bones[excluded].Name;
        Check(!source.Bones.Any(b=>b.Name==excludedName)&&!plan.Mapping.Any(m=>m.Target==excludedName),
            "Shared-track fixture has a real native target leaf absent from the source adapter hierarchy and every reviewed mapping");
        Check(plan.Mapping.All(m=>m.Source!=m.Target),
            "Shared-track fixture preserves reviewed equivalent roles despite every corresponding donor bone having a different anonymous name");
        string fingerprint=BrawlRetargeter.SkeletonFingerprint(model);
        var observations=new List<object>();
        foreach(var mode in new[]{RetargetKeyframeMode.Dense,RetargetKeyframeMode.SourceBoneTimes,RetargetKeyframeMode.SourceChannelTimes})
        using(var clip=new CHR0Node{Name="Independent shared-bone fixture",FrameCount=2})
        {
            var bodyMap=plan.Mapping.Single(m=>m.Role=="pelvis");
            var body=clip.CreateEntry(bodyMap.Source);body.Keyframes.PreserveInterpolation=true;
            foreach(int at in new[]{0,1})body.Keyframes.SetFrameValue(5,at,source.Bones[bodyMap.SourceIndex].BindState[5]+at,true)._tangent=1;
            var motionMap=plan.Mapping.First(m=>plan.IsTrajectory(m));
            var motion=clip.CreateEntry(motionMap.Source);motion.Keyframes.PreserveInterpolation=true;
            foreach(int at in new[]{0,1})motion.Keyframes.SetFrameValue(6,at,source.Bones[motionMap.SourceIndex].BindState[6]+at*.125f,true)._tangent=.125f;
            // The native trajectory adapter represents an authored translation
            // group with explicit fixed Y/Z axes even for identity conversion.
            // Author that canonical group here so the strict tuple comparison
            // tests preservation rather than absent-vs-fixed representation.
            foreach(int channel in new[]{7,8})motion.Keyframes.SetFrameValue(channel,0,source.Bones[motionMap.SourceIndex].BindState[channel],true)._tangent=0;
            // Even a stale source entry with the same target-only name cannot
            // authorize a new target binding when its source model has no bone.
            var orphan=clip.CreateEntry(excludedName);orphan.SetKeyframe(3,0,80,true)._tangent=0;
            string content=NativeAnimationOptimization.SourceContentKey(clip);
            using(var result=BrawlRetargeter.Bake(source,target,clip,false,CancellationToken.None,null,GroundContactMode.Airborne,mode))
            {
                Check(result.Animation.Children.All(e=>result.Plan.Mapping.Any(m=>m.Target==e.Name))&&
                    result.Animation.FindChild(excludedName,false)==null&&result.OmittedTargetBones.Contains(excludedName)&&result.IgnoredSourceBones.Contains(excludedName),
                    mode+": every retargeted track has a source/target correspondence; a target-only helper and orphan donor entry stay unanimated");
                Check(result.Animation.FrameCount==clip.FrameCount&&result.Animation.Loop==clip.Loop&&
                    result.Animation.FindChild(bodyMap.Target,false)!=null,
                    mode+": shared body correction, duration and loop state survive target-only omission");
                SourceKeysSameTrack(motion,(CHR0EntryNode)result.Animation.FindChild(motionMap.Target,false),
                    mode+": shared source-owned motion retains its exact original times, values, sides and tangents");
                string path=Path.Combine(output,"shared-bones-"+mode+".chr0");
                SourceKeyArtifactExport(result.Animation,path);
                using(var reopened=NodeFactory.FromFile(null,path) as CHR0Node)
                {
                    Check(reopened!=null&&reopened.Children.All(e=>result.Plan.Mapping.Any(m=>m.Target==e.Name))&&reopened.FindChild(excludedName,false)==null,
                        mode+": fresh native serialization does not recreate omitted target-only entries");
                    foreach(float time in new[]{0f,.125f,.25f,.5f,.75f,1f})
                    {
                        model.ApplyCHR(reopened,time+1);
                        Check(Enumerable.Range(0,9).All(c=>target.Bones[excluded].FrameState[c]==target.Bones[excluded].BindState[c]),
                            mode+": omitted bone retains native local rest while its parent hierarchy plays, sample "+time);
                    }
                }
                Check(NativeAnimationOptimization.SourceContentKey(clip)==content&&BrawlRetargeter.SkeletonFingerprint(model)==fingerprint,
                    mode+": source content and complete native target binds remain unchanged");
                observations.Add(new {Mode=mode.ToString(),Policy=BrawlRetargeter.TrackMembershipPolicy,AbsentSourceBone=excludedName,
                    Omitted=result.OmittedTargetBones,Tracks=result.Animation.Children.Select(e=>e.Name).ToArray(),Bytes=new FileInfo(path).Length});
            }
        }
        // A non-bind target-only curve must fail before any entry is removed;
        // this catches equal endpoint values with moving Hermite tangents too.
        using(var result=new RetargetResult{Animation=new CHR0Node{Name="Non-bind target-only rejection",FrameCount=2},Plan=plan})
        {
            var clip=result.Animation;
            var track=clip.CreateEntry(excludedName);
            track.ScaleCompensateApply=target.Bones[excluded].SegScaleCompApply;
            track.ScaleCompensateParent=target.Bones[excluded].SegScaleCompParent;
            track.ClassicScaleOff=!target.Bones[excluded].ClassicScale;
            foreach(int at in new[]{0,1})track.SetKeyframe(3,at,target.Bones[excluded].BindState[3],true)._tangent=1;
            string before=NativeAnimationOptimization.SourceContentKey(clip);bool rejected=false;
            try{BrawlRetargeter.RestrictOutputToSharedBones(result,target,CancellationToken.None);}catch(InvalidDataException){rejected=true;}
            Check(rejected&&NativeAnimationOptimization.SourceContentKey(clip)==before,
                "Target-only equal knot values with nonzero tangents reject preparation atomically rather than silently discarding motion");
            foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue),0)})
            {
                foreach(var key in track.KeyArrays[3].Entries)key._tangent=invalid;
                rejected=false;
                try{BrawlRetargeter.RestrictOutputToSharedBones(result,target,CancellationToken.None);}catch(InvalidDataException){rejected=true;}
                Check(rejected&&ReferenceEquals(clip.FindChild(excludedName,false),track),
                    "Target-only non-finite or negative-zero tangent evidence rejects omission before modifying native track membership");
            }
        }
        model.ApplyCHR(null,0);
        File.WriteAllText(Path.Combine(output,"shared-retarget-track-policy.json"),new JavaScriptSerializer{MaxJsonLength=1024*1024}.Serialize(
            new {Policy=BrawlRetargeter.TrackMembershipPolicy,NativeFixture="Source-adapter leaf omission; complete target model retained",Cases=observations}));
        foreach(var bone in sourceBones)bone.Dispose();
    }
}
