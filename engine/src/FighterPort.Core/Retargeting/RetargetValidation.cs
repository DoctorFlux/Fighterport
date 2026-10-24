using System.Numerics;

namespace FighterPort.Core;

public enum ContactApplicability { Required, NotApplicable, Unknown }
public static class RetargetValidation
{
    public const string Version="skinned-support-contact-strict-5";
    public static ContactValidation Measure(FighterProject target,ControlRigSetup controls,RetargetBake bake,CancellationToken cancellation)
    {
        var rig=RigDescriptor.Prepare(target.Scene,controls);float bound=PoseGeometry.Scale(target.Scene,controls)*RetargetAcceptance.PositionScaleFraction;
        var legs=rig.Binding.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).Select(c=>rig.ChainLength(c.Role)).ToArray();float depthBound=(legs.Length>0?legs.Average():rig.CharacteristicLength)*RetargetAcceptance.PenetrationLegFraction;
        var coverage=bake.Source.MotionAnalysis?.Interactions.Coverage??MotionContactCoverage.Unknown;
        var applicability=coverage switch{MotionContactCoverage.Required=>ContactApplicability.Required,MotionContactCoverage.ConfirmedFree=>ContactApplicability.NotApplicable,_=>ContactApplicability.Unknown};
        double last=bake.Clip.LastKeyFrame>=bake.Clip.SampleCount?bake.Clip.SampleCount:bake.Clip.PreviewEndFrame;var times=new SortedSet<double>{last};
        for(double f=0;f<=last;f+=.25)times.Add(f);
        for(int segment=0;segment<last;segment++)foreach(double u in new[]{.125,.2,1d/3,.381966011250105,.618033988749895,.8,.875})if(segment+u<=last)times.Add(segment+u);
        foreach(var interval in bake.Contacts)foreach(double f in new[]{interval.Frame,interval.EndFrame??interval.Frame})foreach(double delta in new[]{-.125,0,.125})if(f+delta>=0&&f+delta<=last)times.Add(f+delta);
        float separation=0,penetration=0,orientation=0,soft=0,drift=0,bodyGap=0,affected=0;double worst=0,floorFrame=0,bodyFrame=0;int required=0,groundSamples=0,penetrating=0;
        var up=bake.Contacts.SelectMany(c=>c.Pins).Select(p=>p.Clearance).OfType<ContactClearance>().FirstOrDefault()?.Normal??new Vector3(rig.Basis.M21,rig.Basis.M22,rig.Basis.M23);var vertices=target.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();var regions=new Dictionary<Guid,ContactRegionMeasurement>();
        bool bodyRequired=bake.Grounding is {} ground&&(ground.BodySupportedThroughout||ground.BodySupportPhases.Length>0);
        var torso=bodyRequired?SurfaceContacts.Regions(target.Scene,controls).Where(r=>r.Role is "Hips" or "Chest").SelectMany(r=>r.Points).ToArray():[];
        foreach(double f in times)
        {
            cancellation.ThrowIfCancellationRequested();var pose=AnimationService.Evaluate(target.Scene,bake.Clip,f) with{Seconds=f/bake.Clip.Rate.FramesPerSecond};var active=bake.Contacts.Where(c=>c.Contains(f)).SelectMany(c=>c.Pins).ToArray();
            foreach(var pin in active)
            {
                float error=ContactGeometry.SupportSeparation(pin,pose,bound);float angle=pin.HoldRotation?2*MathF.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(PoseGeometry.Rotation(pose.World[pin.Bone]),ContactGeometry.TargetRotation(pin,pose))),0,1)):0;
                regions.TryGetValue(pin.Id,out var previous);regions[pin.Id]=new(pin.Id,pin.Role,pin.Mode,pin.Reference?.Name??pin.Surface,!pin.SoftSupport||pin.RequiredSupport,Math.Max(error,previous?.Position??0),previous is null||error>previous.Position?f:previous.WorstFrame,Math.Max(angle,previous?.OrientationRadians??0),(previous?.Samples??0)+1);
                if(pin.SoftSupport&&!pin.RequiredSupport){soft=Math.Max(soft,error);continue;}required++;if(error>separation){separation=error;worst=f;}orientation=Math.Max(orientation,angle);
                if(pin.Mode==ContactMode.Plant&&pin.PlaneNormal is null){var d=ContactGeometry.Point(pin,pose)-ContactGeometry.Target(pin,pose);drift=Math.Max(drift,(d-up*Vector3.Dot(d,up)).Length());}
            }
            if(bake.Grounding is {} g)
            {
                double sf=(bake.Clip.Retarget?.SourceSecondsAt(f/bake.Clip.Rate.FramesPerSecond)??f/bake.Clip.Rate.FramesPerSecond)*bake.Source.Clip.Rate.FramesPerSecond;
                sf=Math.Min(sf,bake.Source.Clip.SampleCount-1e-8);
                if(g.ExplicitSurface||g.Phases.Any(p=>p.Mode==SupportSurfaceMode.Ground&&sf>=p.Start&&sf<p.End+1))
                {
                    groundSamples++;var surface=new SupportSurface(up,g.TargetFloor,g.TargetEdge);var depths=vertices.Select(v=>surface.Penetration(AnimationService.Skin(v,pose))).ToArray();float depth=depths.DefaultIfEmpty().Max();if(depth>penetration){penetration=depth;floorFrame=f;}if(depth>depthBound)penetrating++;affected=Math.Max(affected,depths.Count(d=>d>depthBound)/(float)Math.Max(1,depths.Length));
                    if(torso.Length>0&&RetargetAcceptance.BodyFittingAt(bake,f))
                    {
                        required++;
                        float gap=g.TargetEdge is {} boundary?torso.Min(p=>{var v=ContactGeometry.Point(p,pose);float vertical=Vector3.Dot(v,up)-g.TargetFloor,lateral=Math.Max(0,boundary.Offset-Vector3.Dot(v,boundary.Inward));return MathF.Sqrt(vertical*vertical+lateral*lateral);}):Math.Abs(torso.Min(p=>Vector3.Dot(ContactGeometry.Point(p,pose),up))-g.TargetFloor);
                        if(gap>bodyGap){bodyGap=gap;bodyFrame=f;}
                    }
                }
            }
        }
        bool pass=applicability!=ContactApplicability.Unknown&&(applicability==ContactApplicability.NotApplicable||required>0)&&separation<=bound&&penetration<=depthBound&&orientation<=RetargetAcceptance.RotationRadians&&(!bodyRequired||torso.Length>0&&bodyGap<=bound);
        return new(Version,pass,required>0||groundSamples>0,bound,depthBound,separation,penetration,orientation,soft,worst,floorFrame,times.Count,required,groundSamples){Applicability=applicability,Regions=regions.Values.ToArray(),PlantDrift=drift,MaximumAffectedVertexFraction=affected,PenetratingGroundSamples=penetrating,ClipRevision=bake.Clip.Revision,TargetDeformChecksum=rig.DeformChecksum,BodySupportMeasured=torso.Length>0,BodySupportSeparation=bodyGap,WorstBodySupportFrame=bodyFrame};
    }
}
