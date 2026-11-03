using BrawlCrate.Studio.Retargeting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio
{
    // Finger branches are matched by skin-supported hand topology and their
    // position across the palm. Native IDs and target digit names are never
    // assumed to be equivalent; an incomplete/ambiguous hand is left unmapped.
    internal static class Bt3DetailBinding
    {
        internal static Dictionary<int,string> SuggestedSourceDigits(Bt3SourceRig source)
        {
            var labels=new Dictionary<int,string>();
            foreach(string side in new[]{"left","right"})
            {
                if(!source.Rig.Roles.TryGetValue(side+".hand",out int hand))continue;
                int[] Children(int parent)=>Enumerable.Range(0,source.Rig.Joints.Length)
                    .Where(i=>source.Rig.Joints[i].Parent==parent).ToArray();
                var skinned=Children(hand).Where(i=>source.Vertices.Any(v=>v.Owner==i&&v.OwnerWeight>0)).ToArray();
                var thumb=skinned.Where(i=>Children(i).Length==0).ToArray();
                var fingers=skinned.Where(i=>Children(i).Length==1&&
                    source.Vertices.Any(v=>v.Owner==Children(i)[0]&&v.OwnerWeight>0)).ToArray();
                if(skinned.Length!=5||thumb.Length!=1||fingers.Length!=4)continue;
                var forward=Vector3.Transform(Vector3.UnitZ,RigMath.BodyFrame(source.Rig));
                var ordered=fingers.OrderByDescending(i=>Vector3.Dot(source.Rig.Rest.Point(i)-source.Rig.Rest.Point(hand),forward)).ToArray();
                labels.Add(source.Scene.Skeleton.Bones[thumb[0]].Id,side+" thumb");
                for(int i=0;i<4;i++)
                {
                    labels.Add(source.Scene.Skeleton.Bones[ordered[i]].Id,side+" finger "+(i+1)+" base");
                    labels.Add(source.Scene.Skeleton.Bones[Children(ordered[i])[0]].Id,side+" finger "+(i+1)+" tip");
                }
            }
            return labels;
        }

        internal static IReadOnlyDictionary<int,int> Prepare(Bt3SourceRig source,Rig target)
        {
            var result=new Dictionary<int,int>();
            foreach(string side in new[]{"left","right"})
            {
                if(!source.Rig.Roles.TryGetValue(side+".hand",out int sourceHand)||
                    !target.Roles.TryGetValue(side+".hand",out int targetHand))continue;
                var sourceChildren=Enumerable.Range(0,source.Rig.Joints.Length)
                    .Where(i=>source.Rig.Joints[i].Parent==sourceHand&&
                        source.Vertices.Any(v=>v.Owner==i&&v.OwnerWeight>0))
                    .ToArray();
                if(sourceChildren.Length!=5)continue;
                int[] Children(int parent)=>Enumerable.Range(0,source.Rig.Joints.Length)
                    .Where(i=>source.Rig.Joints[i].Parent==parent).ToArray();
                var thumbs=sourceChildren.Where(i=>Children(i).Length==0).ToArray();
                var fingers=sourceChildren.Where(i=>Children(i).Length==1&&
                    source.Vertices.Any(v=>v.Owner==Children(i)[0]&&v.OwnerWeight>0)).ToArray();
                if(thumbs.Length!=1||fingers.Length!=4)continue;
                var targetA=Enumerable.Range(0,4).Select(i=>side+".finger"+i+".a")
                    .Where(target.Roles.ContainsKey).Select(role=>target.Roles[role]).ToArray();
                if(targetA.Length!=4||targetA.Distinct().Count()!=4||
                    targetA.Any(i=>target.Joints[i].Parent!=targetHand)||
                    !target.Roles.TryGetValue(side+".finger4.a",out int targetThumb)||
                    target.Joints[targetThumb].Parent!=targetHand)continue;
                Vector3 sourceForward=Vector3.Transform(Vector3.UnitZ,RigMath.BodyFrame(source.Rig));
                Vector3 targetForward=Vector3.Transform(Vector3.UnitZ,RigMath.BodyFrame(target));
                var orderedSource=fingers.OrderByDescending(i=>Vector3.Dot(source.Rig.Rest.Point(i)-source.Rig.Rest.Point(sourceHand),sourceForward)).ToArray();
                var orderedTarget=targetA.OrderByDescending(i=>Vector3.Dot(target.Rest.Point(i)-target.Rest.Point(targetHand),targetForward)).ToArray();
                // Tied transverse positions provide no evidence for which
                // digit is which. No hand-specific exception is invented.
                bool DistinctPositions(Rig rig,int[] ordered,int hand,Vector3 forward)=>ordered.Zip(ordered.Skip(1),(a,b)=>
                    Math.Abs(Vector3.Dot(rig.Rest.Point(a)-rig.Rest.Point(b),forward))>.00001f).All(value=>value);
                if(!DistinctPositions(source.Rig,orderedSource,sourceHand,sourceForward)||
                    !DistinctPositions(target,orderedTarget,targetHand,targetForward))continue;
                result.Add(thumbs[0],targetThumb);
                for(int i=0;i<4;i++)
                {
                    result.Add(orderedSource[i],orderedTarget[i]);
                    int sourceTip=Children(orderedSource[i])[0];
                    var targetTip=Enumerable.Range(0,target.Joints.Length)
                        .Where(j=>target.Joints[j].Parent==orderedTarget[i]).ToArray();
                    if(targetTip.Length==1)result.Add(sourceTip,targetTip[0]);
                }
            }
            return result;
        }
    }
}
