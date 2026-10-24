using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio.Retargeting
{
    public sealed partial class ContactMotion
    {
        internal static bool IsEndContact(string role)=>role.EndsWith(".hand",StringComparison.Ordinal)||role.EndsWith(".foot",StringComparison.Ordinal);

        internal static IEnumerable<int> JointControls(Rig rig,string role)
        {
            string side=role.Split('.')[0];string[] joints;
            if(role.EndsWith(".hand",StringComparison.Ordinal))joints=new[]{side+".upperArm",side+".lowerArm",role};
            else if(role.EndsWith(".foot",StringComparison.Ordinal))joints=new[]{side+".upperLeg",side+".lowerLeg",role};
            else if(role.EndsWith(".lowerArm",StringComparison.Ordinal))joints=new[]{side+".upperArm",role};
            else if(role.EndsWith(".lowerLeg",StringComparison.Ordinal))joints=new[]{side+".upperLeg",role};
            else if(role.EndsWith(".upperLeg",StringComparison.Ordinal))joints=new[]{role};
            else if(role.EndsWith(".upperArm",StringComparison.Ordinal))joints=new[]{side+".shoulder",role};
            else if(role.EndsWith(".shoulder",StringComparison.Ordinal))joints=new[]{"spine","chest",role};
            else if(role=="neck")joints=new[]{"chest","neck"};
            else if(role=="head")joints=new[]{"neck","head"};
            else if(role=="chest")joints=new[]{"pelvis","spine","chest"};
            else if(role=="spine")joints=new[]{"pelvis","spine"};
            else joints=new[]{"pelvis","spine","chest","neck"};
            IEnumerable<string> controls=IsEndContact(role)?joints:joints.Concat(new[]{"pelvis"});
            // Skin contacts can be carried by toes or fingers. Their optional
            // mapped base joints must participate instead of forcing every
            // correction through the ankle/wrist and the rest of the body.
            if(role.EndsWith(".hand",StringComparison.Ordinal))
                controls=controls.Concat(rig.Roles.Keys.Where(r=>r.StartsWith(side+".finger",StringComparison.Ordinal)&&r.EndsWith(".a",StringComparison.Ordinal)));
            else if(role.EndsWith(".foot",StringComparison.Ordinal))controls=controls.Concat(new[]{side+".toe"});
            return controls.Where(rig.Roles.ContainsKey).Select(r=>rig.Roles[r]).Distinct();
        }

        // Establish stationary targets on a reachable corrected pose, rather than
        // forcing the uncorrected donor's placements onto different proportions.
        private void CalibrateBodyAnchors(System.Threading.CancellationToken cancellation,Action<int,int> progress=null)
        {
            var spans=new List<Tuple<string,int,int>>();
            foreach(var track in tracks)
            {
                ContactGoal[] values=track.Value;int start=0;
                while(start<values.Length)
                {
                    if(values[start].Marker==null){start++;continue;}
                    SkinPoint marker=values[start].Marker;int end=start;
                    while(end+1<values.Length && ReferenceEquals(values[end+1].Marker,marker))end++;
                    spans.Add(Tuple.Create(track.Key,start,end));start=end+1;
                }
            }
            // Establish new contacts in time order against contacts already held.
            // Independent per-part calibration can create mutually unreachable
            // anchors when forearm/hand or trunk/head spans begin at different times.
            var boundaries=spans.GroupBy(s=>s.Item2).OrderBy(g=>g.Key).ToArray();int completed=0;
            foreach(var boundary in boundaries)
            {
                progress?.Invoke(++completed,boundaries.Length);
                cancellation.ThrowIfCancellationRequested();float time=boundary.Key/4f;
                if(!tracks.Keys.Any(r=>!IsEndContact(r)&&At(r,time).Support>0.001f))continue;
                var source=sample(time);var calibrated=plan.Evaluate(source);Suspended?.Apply(source,calibrated);var pose=calibrated.Pose;
                var controls=tracks.Keys.ToDictionary(r=>r,r=>At(r,time));
                foreach(var control in controls)
                {
                    bool established=spans.Any(s=>s.Item1==control.Key && s.Item2<boundary.Key && s.Item3>=boundary.Key);
                    if(!established)control.Value.Lock=0;
                }
                FitBodyContacts(pose,controls);
                foreach(var span in boundary)
                {
                    SkinPoint marker=plan.Target.Surfaces[span.Item1].Marker(pose);
                    Vector3 anchor=marker.Evaluate(pose);
                    anchor+=plan.Target.GroundNormal*(plan.Target.GroundHeight-Vector3.Dot(anchor,plan.Target.GroundNormal));
                    for(int i=span.Item2;i<=span.Item3;i++){tracks[span.Item1][i].Marker=marker;tracks[span.Item1][i].Anchor=anchor;}
                }
            }
        }

    }
}
