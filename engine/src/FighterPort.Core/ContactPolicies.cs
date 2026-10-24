using System.Numerics;

namespace FighterPort.Core;

public enum ContactMode { Free,Approach,Impact,Plant,Pivot,Slide,Roll,Grip,Release }
public enum ContactEditAction { ForceContact,Release,AllowSlide,AllowRoll }
public enum ContactTimeDomain { SourceSeconds,TargetSeconds,ProtectedGameFrame }
public record ContactIntervalEdit(string Role,double Start,double End,ContactTimeDomain TimeDomain,ContactEditAction Action);

public static class ContactPolicies
{
    public static ContactFrame[] Apply(FighterProject source,AnimationClip clip,ControlRigSetup rig,ContactFrame[] observations,GroundContactAnalysis? analysis,ContactIntervalEdit[] edits,RetargetTiming timing,RationalRate targetRate)
    {
        var result=observations.ToList();var regions=analysis?.Regions??SurfaceContacts.Regions(source.Scene,rig);var anatomy=RigCharacterization.Describe(source.Scene,rig);var up=new Vector3(anatomy.Basis.M21,anatomy.Basis.M22,anatomy.Basis.M23);
        foreach(var edit in edits)
        {
            if(!double.IsFinite(edit.Start)||!double.IsFinite(edit.End)||edit.Start<0||edit.End<edit.Start||!Enum.IsDefined(edit.Action)||!Enum.IsDefined(edit.TimeDomain))throw new InvalidDataException("Invalid explicit contact interval.");
            double SourceFrame(double value)=>edit.TimeDomain switch{ContactTimeDomain.SourceSeconds=>value*clip.Rate.FramesPerSecond,ContactTimeDomain.TargetSeconds=>(timing.TrimStartSeconds+timing.ExportSpeed*value)*clip.Rate.FramesPerSecond,_=>value*(clip.Rate.FramesPerSecond/targetRate.FramesPerSecond)};
            if(edit.TimeDomain==ContactTimeDomain.ProtectedGameFrame&&(timing.TrimStartSeconds!=0||timing.ExportSpeed!=1))throw new NotSupportedException($"Protected game-frame contact '{edit.Role}' conflicts with trim/export retiming. Choose an explicit source- or target-seconds override.");
            double start=SourceFrame(edit.Start),end=SourceFrame(edit.End);if(start<0||end>clip.SampleCount+1e-7)throw new InvalidDataException($"Contact edit '{edit.Role}' lies outside the authentic source duration.");
            if(result.Any(c=>c.Frame<=end&&(c.EndFrame??c.Frame)>=start&&c.Pins.Any(p=>p.Role==edit.Role&&!p.Automatic)))throw new InvalidOperationException($"'{edit.Role}' has an authored exact pin in this interval. Edit/release that pin explicitly in the animation editor; an inferred-contact override cannot weaken it.");
            var region=regions.SingleOrDefault(r=>r.Role==edit.Role)??throw new InvalidOperationException("No calibrated skin patch for contact role "+edit.Role);
            // Inclusive interval boundaries are separated by a small exact-time gap;
            // the override owns the boundary, not the adjacent prior observation.
            var next=new List<ContactFrame>();foreach(var c in result)
            {
                double last=c.EndFrame??c.Frame;if(c.Frame>end||last<start||!c.Pins.Any(p=>p.Role==edit.Role)){next.Add(c);continue;}
                var other=c.Pins.Where(p=>p.Role!=edit.Role).ToArray();if(other.Length>0)next.Add(c with{Pins=other});var affected=c.Pins.Where(p=>p.Role==edit.Role).ToArray();
                if(c.Frame<start)next.Add(c with{Id=Guid.NewGuid(),Pins=affected,EndFrame=Math.Max(c.Frame,start-.00001)});
                if(last>end)next.Add(c with{Id=Guid.NewGuid(),Pins=affected,Frame=Math.Min(last,end+.00001)});
                if(edit.Action is ContactEditAction.AllowSlide or ContactEditAction.AllowRoll)next.Add(c with{Id=Guid.NewGuid(),Frame=Math.Max(start,c.Frame),EndFrame=Math.Min(end,last),Pins=affected.Select(p=>p with{Mode=edit.Action==ContactEditAction.AllowSlide?ContactMode.Slide:ContactMode.Roll,SurfaceRegion=region.Points,PlaneNormal=up,Provenance="Reviewed inferred interval; surface travel permitted, no exact world lock"}).ToArray()});
            }
            result=next;
            if(edit.Action!=ContactEditAction.ForceContact)continue;
            if(analysis is null)throw new InvalidOperationException("Force contact needs an analyzed/reviewed support surface. Enable automatic contacts and review its plane/context first.");
            var pose=new PreparedGeometry(source,clip).Evaluate(start);var patch=SurfaceContacts.Patch(region,pose,up,anatomy.LegLength*.006f);var point=patch.Aggregate(Vector3.Zero,(p,s)=>p+ContactGeometry.Point(s,pose))/patch.Length;
            var pin=new ContactPin(Guid.NewGuid(),edit.Role,region.Bone,Vector3.Zero,point-up*(Vector3.Dot(point,up)-analysis.Floor),Quaternion.Identity,false,"Reviewed ground contact"){Automatic=true,RequiredSupport=true,SurfacePoints=patch,SurfaceRegion=region.Points,PlaneNormal=up,Mode=ContactMode.Plant,Provenance="Reviewed required semantic contact; target patch/anchor adaptation allowed, no authored world lock"};
            result.Add(new(Guid.NewGuid(),clip.Id,start,rig.SkeletonFingerprint,[pin]){EndFrame=end});
        }
        return result.OrderBy(c=>c.Frame).ToArray();
    }
}
