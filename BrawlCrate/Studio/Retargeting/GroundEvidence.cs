using System;
using System.Collections.Generic;
using System.Linq;

namespace BrawlCrate.Studio.Retargeting
{
    public sealed partial class ContactMotion
    {
        private float[] groundEvidence;

        // Intersecting a reference plane does not establish a supporting floor.
        // Auto requires an aligned sole patch, upright bilateral stance, or an
        // actual palm/body contact. In particular, game-driven flight clips can
        // rotate their toes through Y=0 without storing their gameplay height.
        // Explicit Grounded/Source Contacts remain available for ambiguous data.
        private void InferGroundEvidence(RigPose[] poses,Dictionary<string,SurfaceSample[]> surfaces,bool loop)
        {
            var evidence=new float[poses.Length];
            foreach(var pair in surfaces)
            {
                string role=pair.Key;
                bool foot=role.EndsWith(".foot",StringComparison.Ordinal);
                bool hand=role.EndsWith(".hand",StringComparison.Ordinal);
                string side=role.StartsWith("left",StringComparison.Ordinal)?"left":"right";
                float length=Length(plan.Source.Rest,side,hand);
                for(int i=0;i<poses.Length;i++)
                {
                    SurfaceSample surface=pair.Value[i];
                    bool near=surface.Clearance>=-length*0.03f && surface.Clearance<=length*(foot?0.055f:0.02f);
                    bool standing=foot && stanceTracks.TryGetValue(role,out float[] stance) && stance[i]>0.99f;
                    // A shin skin patch near the plane with the knee high above
                    // it is not knee support. Require anatomical proximity for
                    // non-sole patches instead of mistaking hanging boot/shin
                    // geometry for a supporting body contact.
                    float jointHeight=System.Numerics.Vector3.Dot(poses[i].Point(role),plan.Source.GroundNormal)-plan.Source.GroundHeight;
                    bool patch=foot?surface.Flatness>0.7f:tracks[role][i].Support>0.5f && Math.Abs(jointHeight)<length*0.3f;
                    if(standing || (near && patch))evidence[i]=1;
                }
            }
            // Continue a validated contact through heel/toe roll while the
            // source sole stays in its near-floor band. This continuity ends
            // at a lift or deep crossing; an unseeded flight cannot start it.
            // Native looping motions share evidence across their seam.
            var seeds=(float[])evidence.Clone();
            foreach(string role in new[]{"left.foot","right.foot"})
            {
                float length=Length(plan.Source.Rest,role.Split('.')[0],false);
                bool[] near=surfaces[role].Select(s=>s.Clearance>=-length*0.03f && s.Clearance<=length*0.1f).ToArray();
                var visited=new bool[near.Length];
                for(int i=0;i<near.Length;i++)
                {
                    if(!near[i] || visited[i])continue;
                    var pending=new Queue<int>();var segment=new List<int>();pending.Enqueue(i);visited[i]=true;
                    while(pending.Count>0)
                    {
                        int index=pending.Dequeue();segment.Add(index);
                        foreach(int neighbor in new[]{index-1,index+1})
                        {
                            int j=loop?(neighbor+near.Length)%near.Length:neighbor;
                            if(j<0 || j>=near.Length || !near[j] || visited[j])continue;
                            visited[j]=true;pending.Enqueue(j);
                        }
                    }
                    if(segment.Any(j=>seeds[j]>0))foreach(int j in segment)evidence[j]=1;
                }
            }
            // A short continuous fade accommodates heel/toe roll and fractional
            // native samples. It cannot carry a stationary lock across a flight.
            groundEvidence=(float[])evidence.Clone();
            for(int i=0;i<evidence.Length;i++)for(int d=1;d<=4;d++)
                groundEvidence[i]=Math.Max(groundEvidence[i],Math.Max(evidence[Math.Max(0,i-d)],evidence[Math.Min(evidence.Length-1,i+d)])*(1-d/5f));
            PlantedFootSamples=PlantedHandSamples=PlantedBodySamples=LockedSamples=0;
            foreach(var pair in tracks)for(int i=0;i<pair.Value.Length;i++)
            {
                ContactGoal goal=pair.Value[i];
                goal.Support*=groundEvidence[i];goal.Lock*=groundEvidence[i];
                if(goal.Support>0.99f)
                {
                    if(pair.Key.EndsWith(".foot",StringComparison.Ordinal))PlantedFootSamples++;
                    else if(pair.Key.EndsWith(".hand",StringComparison.Ordinal))PlantedHandSamples++;
                    else PlantedBodySamples++;
                }
                if(goal.Lock>0.99f)LockedSamples++;
            }
        }

        private float GroundEvidenceAt(float time)
        {
            if(groundEvidence==null)return 1;
            float index=RigMath.Clamp(time*4,0,groundEvidence.Length-1);
            int a=(int)index,b=Math.Min(groundEvidence.Length-1,a+1);
            return groundEvidence[a]+(groundEvidence[b]-groundEvidence[a])*(index-a);
        }
    }
}
