using BrawlLib.SSBB.ResourceNodes;
using FighterPort.NativeEvaluation;
using System.Linq;
namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private static NativeRigData NativeEngineRig(MDL0Node model,CHR0Node clip=null)
        {
            var original=model.NativeEditingBackend?.CaptureEvaluation();if(original==null)return null;
            var joints=original.Joints.Select(j=>new JointPolicy{CompensatedScale=j.CompensatedScale,Skeleton=j.Skeleton,SkeletonRoot=j.SkeletonRoot,HasInverseBind=j.HasInverseBind,InverseBind=j.InverseBind}).ToArray();
            var bones=model.AllBones.ToArray();
            if(clip!=null)foreach(var entry in clip.Children.OfType<CHR0EntryNode>())
            {int joint=System.Array.FindIndex(bones,b=>b.Name==entry.Name);if(joint>=0)joints[joint].AnimationCompensatedScale=entry.ClassicScaleOff;}
            return new NativeRigData{Identity=original.Identity,Joints=joints,Roles=BrawlCrate.Studio.Retargeting.BrawlRetargeter.Characterize(model).Roles,Vertices=original.Vertices};
        }
    }
}
