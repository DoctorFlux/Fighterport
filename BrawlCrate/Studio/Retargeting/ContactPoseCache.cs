using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio.Retargeting
{
    public sealed partial class ContactMotion
    {
        private sealed class CachedContactPose
        {
            internal JointTransform[] Source,Target;
            internal Vector3 Pelvis;
            internal float LimbResidual,ContactResidual;
            internal int Clamped;
            internal Dictionary<string,Vector3> Goals;
        }
        private readonly Dictionary<float,CachedContactPose> poseCache=new Dictionary<float,CachedContactPose>();
        private readonly Queue<float> cachedTimes=new Queue<float>();

        // Bound memory, detect changed source keys, and return independent poses.
        // Caching never changes results or makes evaluation depend on scrub order.
        private bool TryCachedPose(float time,RigPose source,out RetargetPose result)
        {
            result=null;
            if(!poseCache.TryGetValue(time,out CachedContactPose cached) || !cached.Source.SequenceEqual(source.Local))return false;
            result=new RetargetPose{Pose=new RigPose(plan.Target,cached.Target),PelvisCorrection=cached.Pelvis,
                MaximumLimbResidual=cached.LimbResidual,MaximumContactResidual=cached.ContactResidual,ClampedLimbs=cached.Clamped};
            foreach(var goal in cached.Goals)result.Goals.Add(goal.Key,goal.Value);
            return true;
        }
        private void CachePose(float time,RigPose source,RetargetPose result)
        {
            if(time*4!=(int)(time*4))return;
            if(!poseCache.ContainsKey(time))
            {
                if(cachedTimes.Count>=4096)poseCache.Remove(cachedTimes.Dequeue());
                cachedTimes.Enqueue(time);
            }
            poseCache[time]=new CachedContactPose{Source=(JointTransform[])source.Local.Clone(),Target=(JointTransform[])result.Pose.Local.Clone(),
                Pelvis=result.PelvisCorrection,LimbResidual=result.MaximumLimbResidual,ContactResidual=result.MaximumContactResidual,Clamped=result.ClampedLimbs,
                Goals=new Dictionary<string,Vector3>(result.Goals)};
        }
    }
}
