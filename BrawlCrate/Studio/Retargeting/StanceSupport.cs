using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio.Retargeting
{
    public sealed partial class ContactMotion
    {
        private readonly Dictionary<string,float[]> stanceTracks=new Dictionary<string,float[]>();
        // Detect quiet upright support within a motion, including after a
        // climb, landing or get-up. An earlier suspended/airborne phase must
        // not prevent a later standing segment from receiving hip/foot fitting.
        private float[] LocalStance(RigPose[] poses,SurfaceSample[] surfaces,float[] hips,string role,float length)
        {
            var flags=new float[poses.Length];
            string other=role=="left.foot"?"right.foot":"left.foot";
            float[] otherHeights=poses.Select(p=>plan.Source.Surfaces[other].Clearance(p)).ToArray();
            string side=role.Split('.')[0],otherSide=other.Split('.')[0];
            float otherLength=Length(plan.Source.Rest,otherSide,false);
            float[] extension=poses.Select(p=>Vector3.Dot(p.Point(side+".upperLeg")-p.Point(role),plan.Source.GroundNormal)/length).ToArray();
            float[] otherExtension=poses.Select(p=>Vector3.Dot(p.Point(otherSide+".upperLeg")-p.Point(other),plan.Source.GroundNormal)/otherLength).ToArray();
            for(int i=0;i<poses.Length;i++)
            {
                if(hips[i]<=length*0.65f)continue;
                // An upright double-support posture need not be stationary:
                // source FK sole drift can rise while the hip/foot travels.
                // Stable vertical leg extension distinguishes that posture
                // from a lifted knee. Opposite-sole evidence establishes the
                // plane; no idle reference or clip boundary is required.
                int before=Math.Max(0,i-1),after=Math.Min(poses.Length-1,i+1);
                float extensionSpeed=Math.Abs(extension[after]-extension[before])/Math.Max(0.25f,(after-before)/4f);
                int pairedStart=Math.Max(0,i-4),pairedEnd=Math.Min(poses.Length-1,i+4);
                bool paired=Enumerable.Range(pairedStart,pairedEnd-pairedStart+1).Count(j=>
                    (otherHeights[j]>=-otherLength*0.1f && otherHeights[j]<=otherLength*0.03f) ||
                    (surfaces[j].Clearance>=-length*0.1f && surfaces[j].Clearance<=length*0.03f))>=(pairedEnd-pairedStart+1)*0.75f;
                if(extension[i]>0.7f && otherExtension[i]>0.7f && extensionSpeed<0.02f &&
                    surfaces[i].Clearance>=-length*0.015f && surfaces[i].Clearance<length*0.08f &&
                    otherHeights[i]>=-otherLength*0.1f && otherHeights[i]<otherLength*0.08f &&
                    Math.Min(surfaces[i].Clearance/length,otherHeights[i]/otherLength)<0.03f && paired)flags[i]=1;
                int start=i,end=i;
                while(start>Math.Max(0,i-24) && hips[start-1]>length*0.65f)start--;
                while(end<Math.Min(poses.Length-1,i+24) && hips[end+1]>length*0.65f)end++;
                if(end-start<8)continue;
                float[] heights=Enumerable.Range(start,end-start+1).Select(j=>surfaces[j].Clearance).OrderBy(h=>h).ToArray();
                float range=heights[(int)((heights.Length-1)*0.95f)]-heights[(int)((heights.Length-1)*0.05f)];
                float hipRange=Enumerable.Range(start,end-start+1).Max(j=>hips[j])-Enumerable.Range(start,end-start+1).Min(j=>hips[j]);
                Vector3 origin=poses[i].Point(role);
                float travel=Enumerable.Range(start,end-start+1).Max(j=>{
                    Vector3 delta=poses[j].Point(role)-origin;delta-=plan.Source.GroundNormal*Vector3.Dot(delta,plan.Source.GroundNormal);return delta.Length();
                });
                bool ownEvidence=heights.Any(h=>h>=-length*0.25f && h<=length*0.015f);
                // A quiet near-floor foot can retain small source FK drift
                // throughout a standing segment, never reaching the strict
                // contact band itself. Sustained support from the opposite
                // sole establishes the common floor without requiring a named
                // idle pose or a clip boundary. Moving/higher lifts still fail
                // the range, travel and near-floor checks.
                bool pairedEvidence=heights[(int)((heights.Length-1)*0.95f)]<length*0.08f &&
                    Enumerable.Range(start,end-start+1).Count(j=>otherHeights[j]>=-length*0.1f && otherHeights[j]<=length*0.015f)>=(end-start+1)*0.75f;
                if((ownEvidence || pairedEvidence) &&
                    heights[(int)((heights.Length-1)*0.05f)]>=-length*0.03f &&
                    heights[(int)((heights.Length-1)*0.95f)]<length*0.12f &&
                    range<Math.Max(length*0.035f,hipRange*0.65f) && travel<length*0.25f)flags[i]=1;
            }
            var weights=(float[])flags.Clone();
            for(int i=0;i<weights.Length;i++)for(int d=1;d<=3;d++)
                weights[i]=Math.Min(weights[i],Math.Min(flags[Math.Max(0,i-d)]+d/4f,flags[Math.Min(flags.Length-1,i+d)]+d/4f));
            return weights;
        }
        private float StanceAt(string role,float time)
        {
            if(!stanceTracks.TryGetValue(role,out float[] weights))return 0;
            float index=RigMath.Clamp(time*4,0,weights.Length-1);int a=(int)index,b=Math.Min(weights.Length-1,a+1);
            return weights[a]+(weights[b]-weights[a])*(index-a);
        }
    }
}
