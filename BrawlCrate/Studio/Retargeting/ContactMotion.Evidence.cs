using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio.Retargeting
{
    public sealed partial class ContactMotion
    {
        static int SupportBits(float value)=>BitConverter.ToInt32(BitConverter.GetBytes(value),0);
        static int[] SupportVector(Vector3 v)=>new[]{SupportBits(v.X),SupportBits(v.Y),SupportBits(v.Z)};
        static int[] SupportQuaternion(Quaternion q)=>new[]{SupportBits(q.X),SupportBits(q.Y),SupportBits(q.Z),SupportBits(q.W)};
        static int[] SupportMatrix(Matrix4x4 m)=>new[]{m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44}.Select(SupportBits).ToArray();
        static object SupportPoint(SkinPoint point)=>point?.Weights.Select(w=>new{w.Joint,Local=SupportVector(w.Local),Weight=SupportBits(w.Weight)}).ToArray();
        static object SupportRig(Rig rig)=>new{
            Bones=rig.Joints.Select(j=>new{j.Name,j.Parent,Translation=SupportVector(j.Bind.Translation),Scale=SupportVector(j.Bind.Scale),Rotation=SupportQuaternion(j.Bind.Rotation)}).ToArray(),
            Roles=rig.Roles.OrderBy(r=>r.Key,StringComparer.Ordinal).ToDictionary(r=>r.Key,r=>r.Value),
            CoordinateUp=rig.CoordinateUp.HasValue?SupportVector(rig.CoordinateUp.Value):null,CoordinateFrame=rig.CoordinateFrame.HasValue?SupportQuaternion(rig.CoordinateFrame.Value):null,
            GroundNormal=SupportVector(rig.GroundNormal),GroundHeight=SupportBits(rig.GroundHeight),
            RestWorld=rig.Rest.World.Select(SupportMatrix).ToArray(),
            Surfaces=rig.Surfaces.OrderBy(s=>s.Key,StringComparer.Ordinal).Select(s=>new{s.Key,Normal=SupportVector(s.Value.NormalLocal),Forward=SupportVector(s.Value.ForwardLocal),
                Points=s.Value.Points.Select(SupportPoint).ToArray()}).ToArray()};
        internal object CaptureOriginalEvidence()
        {
            long coefficientCount=(long)(last*4+1)*plan.Source.Joints.Length*26;
            coefficientCount+=plan.Source.Surfaces.Values.Concat(plan.Target.Surfaces.Values).Sum(s=>s.Points.Sum(p=>(long)p.Weights.Length*5));
            if(coefficientCount>2000000)throw new InvalidOperationException("Complete original support evidence exceeds its two-million-coefficient review budget.");
            // Capture immutable input poses; never re-evaluate the retargeter or
            // its contact solver, and never call this from playback.
            var frames=Enumerable.Range(0,last*4+1).Select(i=>{
                var pose=sample(i/4f);return new{Frame=SupportBits(i/4f),Local=pose.Local.Select(t=>new{Translation=SupportVector(t.Translation),Scale=SupportVector(t.Scale),Rotation=SupportQuaternion(t.Rotation)}).ToArray(),World=pose.World.Select(SupportMatrix).ToArray()};
            }).ToArray();
            return new{Version="original-next-support-context-1",Algorithm=HumanoidRetargeter.Algorithm,EndOrientationPolicy,LastFrame=last,SampleStep=.25f,
                Source=SupportRig(plan.Source),Target=SupportRig(plan.Target),FloorEnabled,
                Suspended=Suspended?.CaptureOriginalEvidence(),SourceFrames=frames,
                Tracks=tracks.OrderBy(t=>t.Key,StringComparer.Ordinal).Select(t=>new{Role=t.Key,Samples=t.Value.Select(g=>new{Support=SupportBits(g.Support),Lock=SupportBits(g.Lock),Anchor=SupportVector(g.Anchor),Marker=SupportPoint(g.Marker)}).ToArray()}).ToArray(),
                Floor=floorTracks.OrderBy(t=>t.Key,StringComparer.Ordinal).ToDictionary(t=>t.Key,t=>t.Value.Select(SupportBits).ToArray()),
                Stance=stanceTracks.OrderBy(t=>t.Key,StringComparer.Ordinal).ToDictionary(t=>t.Key,t=>t.Value.Select(SupportBits).ToArray()),
                GroundEvidence=groundEvidence?.Select(SupportBits).ToArray(),StandingFeet=StabilizedStandingFeet,
                Ranges=CaptureOriginalRanges()};
        }
        internal NativeSupportRange[] CaptureOriginalRanges()
        {
            var result=new List<NativeSupportRange>();
            foreach(var pair in tracks.OrderBy(t=>t.Key,StringComparer.Ordinal))
            {
                if(!plan.Target.Surfaces.TryGetValue(pair.Key,out var surface)||!plan.Target.Roles.TryGetValue(pair.Key,out int bone))continue;
                var dependencies=new HashSet<int>();
                foreach(int joint in surface.Points.SelectMany(p=>p.Weights).Where(w=>w.Weight>0).Select(w=>w.Joint).Append(bone))
                    for(int i=joint;i>=0;i=plan.Target.Joints[i].Parent)dependencies.Add(i);
                int at=0;while(at<pair.Value.Length)
                {
                    if(pair.Value[at].Support<=0){at++;continue;}int first=at;
                    while(at+1<pair.Value.Length&&pair.Value[at+1].Support>0)at++;
                    result.Add(new NativeSupportRange{Role=pair.Key,BoneName=plan.Target.Joints[bone].Name,First=Math.Max(0,(first-1)/4f),Last=Math.Min(last,(at+1)/4f),
                        Dependencies=dependencies.OrderBy(i=>i).Select(i=>plan.Target.Joints[i].Name).ToArray()});at++;
                }
            }
            return result.ToArray();
        }
    }
}
