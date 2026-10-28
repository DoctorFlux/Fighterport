using System.Numerics;

namespace FighterPort.Core;

public enum MotionContactCoverage { Unknown, Required, ConfirmedFree }
public record MotionCoverageLabel(Guid ClipRevision,double StartSeconds,double EndSeconds,bool RequiredContacts,string Evidence,bool Reviewed);
public record InteractionGraph(ContactFrame[] Relations,MotionContactCoverage Coverage,Diagnostic[] Diagnostics);
public record SourceMotionAnalysis(RetargetSourceAnalysis? SurfaceAnalysis,InteractionGraph Interactions,string Revision)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public GroundContactSettings? SourceSupportContext {get;init;}
    public MotionCoverageLabel[] Labels {get;init;}=[];
    public bool CoverageReviewed {get;init;}
    public const string Version="source-interaction-evidence-2";
    public static SourceMotionAnalysis Analyze(RetargetRequest request)
    {
        var source=request.Source;var clip=request.Clip;
        // Authentic evidence is independent of target fitting preferences.
        var ground=request.Ground;
        bool humanoidGround=request.SourceRig.Chains.Any(c=>c.Role=="Foot.L")&&request.SourceRig.Chains.Any(c=>c.Role=="Foot.R");
        bool authoredGround=source.Contacts.Any(c=>c.Clip==clip.Id&&c.Pins.Any(p=>p.Clearance is not null));
        if(ground is not null&&!humanoidGround)
        {
            if(!authoredGround)throw new NotSupportedException("Automatic ground analysis needs a characterized humanoid or an explicit authored support context. Generic chain transfer/constraints remain available.");
            ground=null; // Use the supplied scene geometry, not a fabricated pair of legs.
        }
        var evidence=ground is null?null:request.Analysis??RetargetDependencies.Analyze(source,clip,request.SourceRig,ground,request.Cancellation);
        if(evidence is not null&&(ground is null||evidence.Key!=RetargetDependencies.SourceKey(source,clip,request.SourceRig,ground)))throw new InvalidDataException("Source interaction evidence is stale.");
        var contacts=source.Contacts.Where(c=>c.Clip==clip.Id).Concat(evidence?.Ground.Contacts??[]).ToArray();
        var labels=source.MotionCoverage.Where(l=>l.ClipRevision==clip.Revision&&l.Reviewed&&!string.IsNullOrWhiteSpace(l.Evidence)).OrderBy(l=>l.StartSeconds).ToArray();
        double end=0;bool complete=labels.Length>0,allFree=labels.All(l=>!l.RequiredContacts);
        foreach(var label in labels)
        {
            if(!double.IsFinite(label.StartSeconds)||!double.IsFinite(label.EndSeconds)||label.StartSeconds<0||label.EndSeconds>clip.DurationSeconds+1e-9||label.StartSeconds>end+1e-9||label.EndSeconds<=label.StartSeconds){complete=false;break;}
            if(label.RequiredContacts)
            {
                double covered=label.StartSeconds;
                foreach(var c in contacts.Where(c=>c.Pins.Length>0).OrderBy(c=>c.Frame))
                {
                    double start=c.Frame/clip.Rate.FramesPerSecond,stop=(c.EndFrame??c.Frame)/clip.Rate.FramesPerSecond;
                    if(stop<=covered)continue;if(start>covered+1e-9)break;covered=Math.Max(covered,stop);
                }
                if(covered<label.EndSeconds-1e-9)complete=false;
            }
            end=Math.Max(end,label.EndSeconds);
        }
        complete&=end>=clip.DurationSeconds-1e-9;bool confirmedFree=complete&&allFree;
        if(confirmedFree&&contacts.Length>0)throw new InvalidDataException("Reviewed free-motion coverage conflicts with existing contact relationships; correct the source annotations.");
        var coverage=contacts.Length>0?MotionContactCoverage.Required:confirmedFree?MotionContactCoverage.ConfirmedFree:MotionContactCoverage.Unknown;
        var diagnostics=(evidence?.Ground.Diagnostics??[]).ToList();if(!complete||coverage==MotionContactCoverage.Unknown)diagnostics.Add(new("SOURCE_MODE_REVIEW","Full source-mode coverage has not been independently reviewed. Contact candidates are measurements; empty detection is not evidence of an entirely free animation.",false));
        return new(evidence,new(contacts,coverage,diagnostics.ToArray()),RetargetDependencies.Hash(new{Version,clip.Revision,Surface=evidence?.Key,Contacts=contacts,Labels=labels})){Labels=labels,CoverageReviewed=complete,SourceSupportContext=ground is null?null:ground with{TargetFloor=null,TargetEdge=null}};
    }
}
public record TargetMotionPlan(RigDescriptor Rig,ControlRigSetup Controls,BaselineMotionTransfer Baseline,InteractionGraph Graph,
    GroundContactReport? Grounding,RetargetTiming Timing,RationalRate Rate,int Samples,bool Loop,bool Closing,Diagnostic[] Diagnostics)
{
    public const string Version="target-interaction-plan-13";
    public double SourceFrame(double frame)=>Timing.SourceFrame(frame,BaselineSourceRate,Rate.FramesPerSecond);
    public double BaselineSourceRate {get;init;}
    public int AuthenticSourceSamples {get;init;}
    public SkinSurfacePoint[] BodySupportPoints {get;init;}=[];
    public float PositionBound {get;init;}
    public ContactClearance? Clearance {get;init;}
    internal Vector3[]? BodyGuide {get;init;}
    // Offline reference preparation is separately observable from the
    // immutable contact plan and the later articulated/native solve.
    public TargetMotionPlan PrepareBodyReference(CancellationToken cancellation=default)=>ContactMotionPrior.Prepare(this,cancellation);
    public Transform[] Reference(double frame,Vector3[]? winding=null)
    {
        var local=Baseline.Evaluate(SourceFrame(frame),winding);if(BodyGuide is not {Length:>0} guide)return local;
        double sample=Math.Clamp(frame*TemporalRetargetSolver.Substeps,0,guide.Length-1);int a=(int)sample,b=Math.Min(a+1,guide.Length-1);var shift=Vector3.Lerp(guide[a],guide[b],(float)(sample-a));
        int body=Rig.Binding.Roles["Hips"],parent=Rig.Scene.Bones[body].Parent;
        if(parent>=0)
        {
            var pose=AnimationService.Evaluate(Rig.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
            if(!Matrix4x4.Invert(pose.World[parent],out var inverse))throw new NotSupportedException("A singular animated parent prevents support-relative body translation.");
            shift=Vector3.TransformNormal(shift,inverse);
        }
        local[body]=local[body] with{Translation=local[body].Translation+shift};return local;
    }
    public ContactPin[] Pins(double frame)=>Graph.Relations.Where(c=>c.Contains(frame)).SelectMany(c=>c.Pins).ToArray();
    public bool HasGround(double frame)=>Grounding?.ExplicitSurface==true||Grounding?.Phases.Any(p=>p.Mode==SupportSurfaceMode.Ground&&SourceFrame(frame)>=p.Start&&SourceFrame(frame)<p.End+1)==true;
    public static TargetMotionPlan Build(RetargetRequest request,RigDescriptor source,RigDescriptor target,BaselineMotionTransfer baseline,SourceMotionAnalysis analysis)
    {
        var timing=request.Timing??new();timing.Validate(request.Clip);var rate=request.TargetRate??request.Target.Clips.FirstOrDefault()?.NativeTarget?.Timebase??request.Target.Clips.FirstOrDefault()?.Source?.Timebase??request.Clip.Rate;rate.Validate();
        if(request.Source.Moves.Any(m=>m.Events.Length>0)&&(timing.TrimStartSeconds!=0||timing.TrimEndSeconds is not null||timing.ExportSpeed!=1))throw new NotSupportedException("Protected gameplay event time ownership must be resolved before trim/retiming.");
        double duration=(timing.TrimEndSeconds??request.Clip.DurationSeconds)-timing.TrimStartSeconds;double count=duration/timing.ExportSpeed*rate.FramesPerSecond;
        int samples=checked((int)Math.Round(count));if(samples<1||Math.Abs(samples-count)>1e-7)throw new NotSupportedException($"Native output needs {count:G12} samples; nonintegral duration was not rounded.");
        bool loop=request.Clip.Loop&&timing.TrimStartSeconds==0&&timing.TrimEndSeconds is null;
        bool closing=loop||request.Clip.LastKeyFrame>=request.Clip.SampleCount||timing.TrimStartSeconds!=0||timing.TrimEndSeconds is not null||timing.ExportSpeed!=1;
        var settings=request.Fitting??new();var surface=settings.AutomaticFitting&&settings.AutomaticContacts?analysis.SurfaceAnalysis?.Ground:null;var diagnostics=analysis.Interactions.Diagnostics.ToList();
        var regions=surface is null?[]:SurfaceContacts.Regions(target.Scene,request.TargetRig);var up=new Vector3(target.Basis.M21,target.Basis.M22,target.Basis.M23);
        float floor=surface is null?0:request.Ground?.TargetFloor??SurfaceContacts.Floor(target.Scene,request.TargetRig,regions,up);
        request.Ground?.TargetEdge?.Validate(up);
        var plane=surface is null?null:new ContactClearance(up,floor,regions.SelectMany(SurfaceContacts.Clearance).Distinct().ToArray()){Edge=request.Ground?.TargetEdge};
        var pairs=baseline.Mapping.Bones.ToDictionary(p=>p.Source);var contacts=new List<ContactFrame>();
        var anchorPoses=new Dictionary<double,EvaluatedPose>();
        EvaluatedPose AnchorPose(double frame)
        {
            if(anchorPoses.TryGetValue(frame,out var cached))return cached;
            request.Cancellation.ThrowIfCancellationRequested();
            var local=baseline.Evaluate(timing.SourceFrame(frame,request.Clip.Rate.FramesPerSecond,rate.FramesPerSecond));
            var value=AnimationService.Evaluate(target.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
            anchorPoses.Add(frame,value);return value;
        }
        var sourceRelations=analysis.Interactions.Relations.Select(c=>c with{Pins=c.Pins.Where(p=>p.Automatic?settings.AutomaticFitting&&settings.AutomaticContacts:request.UseContacts).ToArray()}).Where(c=>c.Pins.Length>0).ToArray();
        if((request.ContactEdits?.Length??0)>0)sourceRelations=ContactPolicies.Apply(request.Source,request.Clip,request.SourceRig,sourceRelations,surface,request.ContactEdits!,timing,rate);
        sourceRelations=SurfaceContacts.SplitBodyPhases(sourceRelations,surface);
        var declared=sourceRelations.SelectMany(c=>c.Pins).Where(p=>!p.Automatic&&p.Clearance is not null).Select(p=>p.Clearance!).ToArray();
        if(surface is null&&declared.Length>0)
        {
            var first=declared[0];if(declared.Any(p=>p.Normal!=first.Normal||p.Height!=first.Height))throw new NotSupportedException("Multiple authored clearance surfaces need separate reviewed target geometry bindings.");
            up=Vector3.Normalize(Vector3.TransformNormal(first.Normal,Matrix4x4.Transpose(source.Basis)*target.Basis));floor=first.Height*baseline.FileUnitRatio;
            // Source skin weights are never copied as target collision geometry.
            var points=target.Scene.Meshes.SelectMany((m,mi)=>m.Vertices.Select((v,vi)=>new SkinSurfacePoint(v.Position,v.Joints,v.Weights){Influences=v.Influences,Mesh=mi,Vertex=vi})).ToArray();plane=new(up,floor,points);
        }
        foreach(var interval in sourceRelations)
        {
            double start=timing.TargetFrame(interval.Frame,request.Clip.Rate.FramesPerSecond,rate.FramesPerSecond);
            double end=timing.TargetFrame(interval.EndFrame??interval.Frame,request.Clip.Rate.FramesPerSecond,rate.FramesPerSecond);
            if(end<0||start>=samples)continue;start=Math.Max(0,start);end=Math.Min(samples,end);
            var pose=AnchorPose(start);
            var pins=new List<ContactPin>();
            foreach(var pin in interval.Pins)
            {
                if(!pin.Automatic&&pin.AllowedSurface is not null)throw new NotSupportedException("Authored allowed-support geometry needs an explicit reviewed target-surface binding; it cannot be copied into another rig or silently discarded.");
                if(!pairs.TryGetValue(pin.Bone,out var pair))throw new NotSupportedException($"Contact '{pin.Role}' lacks a unique reviewed target bone binding.");
                if(pin.Reference is {} reference)
                {
                    var binding=request.Supports?.SingleOrDefault(b=>b.SourceReference==reference.Id)??throw new NotSupportedException($"Interaction '{reference.Name}' needs a reviewed target frame/skin binding.");
                    if(binding.TargetDeformChecksum!=target.DeformChecksum)throw new InvalidDataException("Interaction binding targets another deform revision.");binding.Target.Validate(target.Scene.Bones.Length);
                    var frame=binding.Target;if(binding.MotionTimeDomain==ContactTimeDomain.ProtectedGameFrame&&(timing.TrimStartSeconds!=0||timing.ExportSpeed!=1))throw new NotSupportedException("Protected support motion conflicts with retiming.");
                    if(binding.MotionTimeDomain==ContactTimeDomain.SourceSeconds)frame=frame with{Motion=frame.Motion.Select(k=>k with{Seconds=(k.Seconds-timing.TrimStartSeconds)/timing.ExportSpeed}).ToArray()};
                    var patch=binding.TargetPatches.SingleOrDefault(p=>p.Role==pin.Role)?.Points??[];
                    if(pin.SurfacePoints.Length>0&&patch.Length==0)throw new NotSupportedException($"Interaction '{pin.Role}' needs its reviewed target skin patch.");
                    if(pin.Socket is {} socket&&!frame.Grips.Any(g=>g.Socket==socket))throw new InvalidDataException("Target object lacks the fixed grip socket: "+socket);
                    var anchor=binding.TargetAnchors.SingleOrDefault(a=>a.Role==pin.Role);
                    if(pin.Socket is null&&anchor is null)throw new NotSupportedException("Supply a reviewed target support-local anchor for "+pin.Role);
                    pins.Add(pin with{Id=Guid.NewGuid(),Bone=pair.Target,Reference=frame,Position=anchor?.Position??pin.Position,Rotation=anchor?.Rotation??pin.Rotation,SurfacePoints=patch,SurfaceRegion=[],Clearance=null,Provenance=pin.Provenance+"; "+binding.Evidence});
                    if(!frame.GeometryKnown)diagnostics.Add(new("INTERACTION_CONTEXT_REVIEW","The target interaction frame lacks reviewed geometry; this is not native article or scene support.",false));continue;
                }
                if(pin.Automatic)
                {
                    var region=regions.SingleOrDefault(r=>r.Bone==pair.Target)??throw new NotSupportedException($"No target skin calibration for '{pin.Role}'.");
                    // Edge patches retain their source-region correspondence.
                    // The attempted affine rest-box correspondence for every
                    // floor patch failed the independent lying-support check.
                    // Floor calibration therefore uses actual target skin in
                    // the supplied support direction; material correspondence
                    // for arbitrary body patches remains a review requirement.
                    var sourceRegion=pin.EdgeSupport?surface!.Regions.SingleOrDefault(r=>r.Bone==pin.Bone):null;
                    var patch=sourceRegion is not null&&pin.SurfacePoints.Length>0?SurfaceContacts.CorrespondingPatch(sourceRegion,region,pin.SurfacePoints,source.Rest,target.Rest,source.Basis,target.Basis):SurfaceContacts.Patch(region,pose,up,target.CharacteristicLength*.006f);var point=patch.Aggregate(Vector3.Zero,(a,p)=>a+ContactGeometry.Point(p,pose))/patch.Length;
                    var rollingRegion=pin.EdgeInward is not null&&sourceRegion is not null?pin.SurfaceRegion.SelectMany(p=>SurfaceContacts.CorrespondingPatch(sourceRegion,region,[p],source.Rest,target.Rest,source.Basis,target.Basis)).Distinct().ToArray():region.Points;
                    bool plannedAnchor=pin.SurfaceRegion.Length==0;
                    if(plannedAnchor)
                    {
                        // Choose a fixed inferred anchor from the WHOLE
                        // target interval, using actual target proportions.
                        // This is not an authored socket or a per-frame moving
                        // world lock. Roll/slide relationships keep their own
                        // surface freedom and do not use this fixed-anchor fit.
                        var samplesForAnchor=new List<Vector3>();
                        for(double frame=start;frame<end;frame+=1)
                            samplesForAnchor.Add(patch.Aggregate(Vector3.Zero,(sum,p)=>sum+ContactGeometry.Point(p,AnchorPose(frame)))/patch.Length);
                        double last=interval.EndExclusive?Math.BitDecrement(end):end;
                        if(last>=start)samplesForAnchor.Add(patch.Aggregate(Vector3.Zero,(sum,p)=>sum+ContactGeometry.Point(p,AnchorPose(last)))/patch.Length);
                        if(samplesForAnchor.Count>0)point=samplesForAnchor.Aggregate(Vector3.Zero,(sum,p)=>sum+p)/samplesForAnchor.Count;
                    }
                    var goal=point-up*(Vector3.Dot(point,up)-floor);
                    var allowed=!pin.EdgeSupport&&plane?.Edge is not null?plane.Surface:(SupportSurface?)null;
                    if(allowed is {} top)goal=point-top.TopOffset(point);
                    if(pin.EdgeSupport)
                    {
                        if(request.Ground?.TargetEdge is {} targetEdge&&request.Ground.SourceEdge is {} sourceEdge)
                        {
                            var targetAlong=Vector3.Normalize(Vector3.Cross(up,targetEdge.Inward));
                            goal=up*floor+targetEdge.Inward*targetEdge.Offset+targetAlong*Vector3.Dot(point,targetAlong);
                        }
                        else goal=Vector3.TransformNormal(pin.Position,Matrix4x4.Transpose(source.Basis)*target.Basis)*baseline.RootNativeRatio;
                    }
                    bool adaptable=!pin.RequiredSupport&&!pin.EdgeSupport&&surface is not null&&SurfaceContacts.BodySupportedAt(surface.BodySupportPhases,surface.BodySupportedThroughout,surface.BodySupportedAtClosing,Math.Max(interval.Frame,timing.TrimStartSeconds*request.Clip.Rate.FramesPerSecond),request.Clip.SampleCount);
                    pins.Add(pin with{Id=Guid.NewGuid(),Bone=pair.Target,Rotation=baseline.ConvertBoneOrientation(pin.Bone,pair.Target,pin.Rotation),SurfacePoints=patch,SurfaceRegion=pin.SurfaceRegion.Length>0?rollingRegion:[],PlaneNormal=pin.SurfaceRegion.Length>0?up:null,EdgeInward=pin.EdgeInward is not null?request.Ground?.TargetEdge?.Inward??throw new NotSupportedException("Rolling edge contact requires target ledge geometry."):null,AllowedSurface=allowed,Position=goal,Clearance=pin.EdgeSupport?null:plane,SoftSupport=pin.SoftSupport||adaptable,Provenance=pin.Provenance+(plannedAnchor?"; inferred target anchor fitted once across the complete interval in the supplied support region":"")+(adaptable?"; target whole-body support fitting: inferred observation is a soft objective, not an authored world lock":"")});
                }
                else
                {
                    if(pin.SurfacePoints.Length>0)throw new NotSupportedException($"Authored skin contact '{pin.Role}' needs explicit target correspondence; source skin is not scaled onto a new mesh.");
                    var c=Matrix4x4.Transpose(source.Basis)*target.Basis;var offset=Vector3.TransformNormal(pin.LocalPoint,source.Rest.World[pin.Bone]);Matrix4x4.Invert(target.Rest.World[pair.Target],out var inv);
                    pins.Add(pin with{Id=Guid.NewGuid(),Bone=pair.Target,LocalPoint=Vector3.TransformNormal(Vector3.TransformNormal(offset,c)*baseline.RootNativeRatio,inv),Position=Vector3.TransformNormal(pin.Position,c)*baseline.RootNativeRatio,Rotation=baseline.ConvertBoneOrientation(pin.Bone,pair.Target,pin.Rotation),PlaneNormal=pin.PlaneNormal is {} n?Vector3.Normalize(baseline.ConvertDirection(n)):null,Clearance=pin.Clearance is null?null:plane});
                }
            }
            contacts.Add(new(Guid.NewGuid(),Guid.Empty,start,request.TargetRig.SkeletonFingerprint,pins.ToArray()){EndFrame=end,EndExclusive=interval.EndExclusive});
        }
        GroundContactReport? grounding=surface is null?null:new(SurfaceContacts.Version,surface.Floor,floor,surface.Contacts.Length,surface.Contacts.SelectMany(c=>c.Pins).Select(p=>p.Role).Distinct().ToArray(),0){Phases=surface.Phases,BodySupportPhases=surface.BodySupportPhases,BodySupportedThroughout=surface.BodySupportedThroughout,BodySupportedAtClosing=surface.BodySupportedAtClosing};
        if(surface is null&&plane is not null)grounding=new("authored-static-clearance-1",declared[0].Height,floor,sourceRelations.Length,sourceRelations.SelectMany(c=>c.Pins).Select(p=>p.Role).Distinct().ToArray(),0){Phases=sourceRelations.Where(c=>c.Pins.Any(p=>p.Clearance is not null)).Select(c=>new SupportSurfacePhase(c.Frame,Math.Max(c.Frame,(c.EndFrame??c.Frame)-(c.EndExclusive?1:0)),SupportSurfaceMode.Ground)).ToArray()};
        if(grounding is not null)grounding=grounding with{ExplicitSurface=request.Ground?.ExplicitSurface==true,SourceEdge=request.Ground?.SourceEdge,TargetEdge=request.Ground?.TargetEdge};
        if(contacts.SelectMany(c=>c.Pins).Any(p=>p.Automatic&&p.SoftSupport&&!p.RequiredSupport))diagnostics.Add(new("INFERRED_BODY_SUPPORT_ADAPTATION_REVIEW","Source geometry supports the body on the supplied surface. Incidental inferred observations guide a shared soft objective; full target clearance and torso support remain hard constraints. Authored/required locks and grips stay exact. Review measured changes in the target contact pattern.",false));
        var plan=new TargetMotionPlan(target,request.TargetRig,baseline,new(contacts.ToArray(),analysis.Interactions.Coverage,diagnostics.ToArray()),grounding,timing,rate,samples,loop,closing,diagnostics.ToArray()){BaselineSourceRate=request.Clip.Rate.FramesPerSecond,AuthenticSourceSamples=request.Clip.SampleCount,BodySupportPoints=regions.Where(r=>r.Role is "Hips" or "Chest").SelectMany(r=>r.Points).ToArray(),PositionBound=PoseGeometry.Scale(target.Scene,request.TargetRig)*RetargetAcceptance.PositionScaleFraction,Clearance=plane};
        return plan.PrepareBodyReference(request.Cancellation);
    }
}
