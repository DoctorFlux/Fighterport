using BrawlCrate.Studio.Retargeting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio
{
    // BtlObj_GetNodeSide in Tenkaichi3Decomp 1c4d9060 groups native IDs.
    // 0 and 1 are unnamed sides; do not turn them into anatomical left/right
    // or use them as automatic retarget bindings.
    internal static class Bt3JointLabels
    {
        internal static string Describe(int id,int parent)
        {
            string group;
            if(id<8||id>=16&&id<18||id>=46&&id<53)group="centre";
            else if(id>=8&&id<12||id>=18&&id<32)group="side 1";
            else if(id>=12&&id<16||id>=32&&id<46)group="side 0";
            else group="group 3";
            return "Joint "+id.ToString("D2")+(parent<0?" · root":" · "+group);
        }

        // Display-only proposals from the shared structural/geometry analysis.
        // No proposal is a native bone name or an approved retarget binding.
        internal static Dictionary<int,string> SuggestedRoles(Bt3NativeAssetReader.Skeleton skeleton)
        {
            var ids=skeleton.Bones.Select((bone,index)=>new{bone.Id,Index=index})
                .ToDictionary(p=>p.Id,p=>p.Index);
            var joints=skeleton.Bones.Select(b=>new RigJoint("BT3_J"+b.Id.ToString("D2"),
                b.Parent<0?-1:ids[b.Parent],new JointTransform(-b.Rest,Quaternion.Identity,Vector3.One))).ToArray();
            var roles=SkeletonCharacterizer.Analyze(joints).Roles;
            return roles.Where(p=>p.Key!="root").GroupBy(p=>skeleton.Bones[p.Value].Id)
                .ToDictionary(g=>g.Key,g=>string.Join(" / ",g.Select(p=>ShortRole(p.Key))));
        }
        internal static Dictionary<int,string> SuggestedRoles(Bt3NativeMesh.Scene scene,Bt3NativeAssetReader.Motion motion)
        {
            var labels=SuggestedRoles(scene.Skeleton);
            try
            {
                var source=new Bt3SourceRig(scene,motion);
                foreach(var pair in Bt3DetailBinding.SuggestedSourceDigits(source))
                    labels[pair.Key]=pair.Value;
                foreach(string side in new[]{"left","right"})
                {
                    if(!source.Rig.Roles.TryGetValue(side+".upperLeg",out int upper)||
                        !source.Rig.Roles.TryGetValue(side+".lowerLeg",out int lower))continue;
                    for(int joint=source.Rig.Joints[lower].Parent;joint>=0&&joint!=upper;joint=source.Rig.Joints[joint].Parent)
                    {
                        int id=scene.Skeleton.Bones[joint].Id;
                        if(!labels.ContainsKey(id))labels[id]=side+" upper-leg segment";
                    }
                }
            }
            catch(System.IO.InvalidDataException)
            {
                // Advisory labels never gate viewing an otherwise valid model.
            }
            return labels;
        }
        private static string ShortRole(string role)
        {
            string name=role.Replace("left.","L ").Replace("right.","R ");
            return name.Replace("upperArm","upper arm").Replace("lowerArm","forearm")
                .Replace("upperLeg","thigh").Replace("lowerLeg","shin");
        }
    }
}
