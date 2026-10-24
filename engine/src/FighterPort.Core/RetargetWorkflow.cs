using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;

namespace FighterPort.Core;

// A saved fitting tradeoff, never a character/bone exception or contact pass.
public enum BodyVolumePolicy { ReportOverlap, RequireVertexClearance }
public record FittingSettings
{
    public bool AutomaticFitting {get;init;}=true;
    public bool AutomaticContacts {get;init;}=true;
    public BodyVolumePolicy VolumePolicy {get;init;}=BodyVolumePolicy.ReportOverlap;
    public float ArmSpacingDegrees {get;init;}
    public float StanceWidth {get;init;}=1;
    public float PelvisHeight {get;init;}
    public float BodyLeanDegrees {get;init;}
    public float FreeHandReach {get;init;}=1;
    public float MotionFidelity {get;init;}=100;
    public void Validate()
    {
        if(!Enum.IsDefined(VolumePolicy))throw new NotSupportedException("Unknown body-volume fitting policy.");
        static void Range(float x,float min,float max,string name){if(!float.IsFinite(x)||x<min||x>max)throw new InvalidDataException($"{name} must be between {min} and {max}.");}
        Range(ArmSpacingDegrees,-15,30,"Arm spacing (degrees)");Range(StanceWidth,.5f,1.5f,"Stance width");Range(PelvisHeight,-.1f,.1f,"Pelvis height (leg lengths)");Range(BodyLeanDegrees,-20,20,"Body lean (degrees)");Range(FreeHandReach,.8f,1.2f,"Free-hand reach");Range(MotionFidelity,0,100,"Motion fidelity");
        if(MotionFidelity!=100)throw new NotSupportedException("Adjustable motion-fidelity optimization is not calibrated yet. Keep 100; required contact accuracy is independent of preferences.");
    }
}
public record FittingOverrides
{
    public bool? AutomaticFitting {get;init;}
    public bool? AutomaticContacts {get;init;}
    public BodyVolumePolicy? VolumePolicy {get;init;}
    public float? ArmSpacingDegrees {get;init;}
    public float? StanceWidth {get;init;}
    public float? PelvisHeight {get;init;}
    public float? BodyLeanDegrees {get;init;}
    public float? FreeHandReach {get;init;}
    public float? MotionFidelity {get;init;}
    public FittingSettings Resolve(FittingSettings shared)=>new(){AutomaticFitting=AutomaticFitting??shared.AutomaticFitting,AutomaticContacts=AutomaticContacts??shared.AutomaticContacts,VolumePolicy=VolumePolicy??shared.VolumePolicy,ArmSpacingDegrees=ArmSpacingDegrees??shared.ArmSpacingDegrees,StanceWidth=StanceWidth??shared.StanceWidth,PelvisHeight=PelvisHeight??shared.PelvisHeight,BodyLeanDegrees=BodyLeanDegrees??shared.BodyLeanDegrees,FreeHandReach=FreeHandReach??shared.FreeHandReach,MotionFidelity=MotionFidelity??shared.MotionFidelity};
    public static FittingOverrides From(FittingSettings value)=>new(){AutomaticFitting=value.AutomaticFitting,AutomaticContacts=value.AutomaticContacts,VolumePolicy=value.VolumePolicy,ArmSpacingDegrees=value.ArmSpacingDegrees,StanceWidth=value.StanceWidth,PelvisHeight=value.PelvisHeight,BodyLeanDegrees=value.BodyLeanDegrees,FreeHandReach=value.FreeHandReach,MotionFidelity=value.MotionFidelity};
}
public record FittingProfile(string Key,string Name,FittingSettings Settings,RootMotionPolicy RootPolicy);
public record RetargetTiming
{
    public double TrimStartSeconds {get;init;}
    public double? TrimEndSeconds {get;init;}
    public double ExportSpeed {get;init;}=1;
    // Preserve identity frame boundaries exactly instead of round-tripping
    // through seconds (31/60*60 can become 31.000000000000004). Keep the
    // genuine fractional input; never snap artist-authored subframes.
    public double TargetFrame(double sourceFrame,double sourceFps,double targetFps)
        =>(sourceFrame-TrimStartSeconds*sourceFps)/ExportSpeed*(targetFps/sourceFps);
    public double SourceFrame(double targetFrame,double sourceFps,double targetFps)
        =>TrimStartSeconds*sourceFps+ExportSpeed*targetFrame*(sourceFps/targetFps);
    public void Validate(AnimationClip clip)
    {
        double end=TrimEndSeconds??clip.DurationSeconds;
        if(!double.IsFinite(TrimStartSeconds)||!double.IsFinite(end)||!double.IsFinite(ExportSpeed)||TrimStartSeconds<0||end>clip.DurationSeconds+1e-9||end<=TrimStartSeconds||ExportSpeed<.25||ExportSpeed>2)throw new InvalidDataException("Trim must select a nonempty source-seconds interval within the authentic clip; export speed must be 0.25–2.");
    }
}

public record RetargetSourceAnalysis(string Key,string DetectorVersion,Guid Clip,Guid ClipRevision,GroundContactAnalysis Ground);
public static class RetargetDependencies
{
    // Offline only. Never called from a clock tick or renderer.
    static readonly JsonSerializerOptions fingerprintJson=new(ProjectStore.Json){IgnoreReadOnlyProperties=false};
    public static string Hash<T>(T data){var options=new JsonSerializerOptions(fingerprintJson);options.Converters.Insert(0,new ProjectStore.SnapshotFingerprintConverter());return ProjectStore.StreamHash(data,options);}
    public static string ProfileKey(Scene source,Scene target,RootMotionPolicy root,Guid? sourceRig=null,Guid? targetRig=null,RetargetSupportBinding[]? supports=null,string? sourceControls=null,string? targetControls=null)=>Hash(new{Source=AnimationControls.DeformChecksum(source),Target=AnimationControls.DeformChecksum(target),sourceRig,targetRig,sourceControls,targetControls,root,Supports=supports??[]});
    public static string SourceKey(FighterProject source,AnimationClip clip,ControlRigSetup rig,GroundContactSettings settings)=>Hash(new{source.Scene,clip,rig,source.SourceProvider,source.SourceBuild,source.SourceBuildVerified,source.SourceModels,source.SourceFiles,source.NativeLayers,Contacts=source.Contacts.Where(c=>c.Clip==clip.Id).ToArray(),Settings=settings with{TargetFloor=null,TargetEdge=null},Detector=SurfaceContacts.Version});
    public static RetargetSourceAnalysis Analyze(FighterProject source,AnimationClip clip,ControlRigSetup rig,GroundContactSettings settings,CancellationToken cancellation=default)
    {
        return new(SourceKey(source,clip,rig,settings),SurfaceContacts.Version,clip.Id,clip.Revision,SurfaceContacts.Detect(source,clip,rig,settings,cancellation));
    }
}

// Bounds frozen before fitting-control tuning. Preserve the existing solver/floor
// refinement bounds; legacy 0.2% review and 2% rejection are not clean passes.
public record ActionFidelityReport(string Profile,float MaximumDegrees,double WorstFrame,string Bone,int Samples,bool Passed)
{
    public float MaximumWorldDegrees {get;init;}
    public float MaximumBodyAdjustmentDegrees {get;init;}
    public double WorstBodyAdjustmentFrame {get;init;}
    public string BodyAdjustmentBone {get;init;}="";
}
public static class RetargetAcceptance
{
    public const string Version="skinned-contact-strict-2";
    public const float PositionScaleFraction=.0001f,PenetrationLegFraction=.0001f,RotationRadians=.001f;
    // Review guard, not a proof of artistic fidelity. Frozen before private
    // candidate tuning; required contacts are never weakened to meet it.
    public const string ActionProfile="supported-body-relative-articulation-2";
    public const float MaximumAutomaticCorrectionDegrees=45;
    public static bool BodyFittingAt(RetargetBake bake,double frame)
    {
        if(bake.Grounding is not {} ground)return false;
        double seconds=frame/bake.Clip.Rate.FramesPerSecond,sf=(bake.Clip.Retarget?.SourceSecondsAt(seconds)??seconds)*bake.Source.Clip.Rate.FramesPerSecond;
        return SurfaceContacts.BodySupportedAt(ground.BodySupportPhases,ground.BodySupportedThroughout,ground.BodySupportedAtClosing,sf,bake.Source.Clip.SampleCount);
    }
    public static ActionFidelityReport MeasureAction(Scene scene,AnimationClip clip,Func<double,EvaluatedPose> reference,IEnumerable<int> bones,CancellationToken cancellation=default,ControlRigSetup? rig=null,Func<double,bool>? bodyFittingAt=null)
    {
        var indices=bones.Distinct().ToArray();float maximum=0,worldMaximum=0,bodyMaximum=0;double worst=0,worstBody=0;string name="",bodyName="";int count=0;var bodyBones=rig is null?[]:PoseGeometry.BodyAdjustmentBones(rig);
        double last=clip.LastKeyFrame>=clip.SampleCount?clip.SampleCount:clip.PreviewEndFrame;
        var times=new SortedSet<double>();for(double f=0;f<=last;f+=.25)times.Add(f);times.Add(last);
        foreach(double frame in times)
        {
            cancellation.ThrowIfCancellationRequested();var baseline=reference(frame);var result=AnimationService.Evaluate(scene,clip,frame);count++;
            bool bodyFitting=bodyFittingAt?.Invoke(frame)==true;
            foreach(int bone in indices)
            {
                float world=PoseGeometry.RotationDifference(PoseGeometry.Rotation(baseline.World[bone]),PoseGeometry.Rotation(result.World[bone]));worldMaximum=Math.Max(worldMaximum,world);
                if(bodyFitting&&bodyBones.Contains(bone)){if(world>bodyMaximum){bodyMaximum=world;worstBody=frame;bodyName=scene.Bones[bone].Name;}continue;}
                float degrees=PoseGeometry.RotationDifference(PoseGeometry.FittingRotation(scene,baseline,bone,bodyBones,bodyFitting),PoseGeometry.FittingRotation(scene,result,bone,bodyBones,bodyFitting));if(degrees>maximum){maximum=degrees;worst=frame;name=scene.Bones[bone].Name;}
            }
        }
        return new(ActionProfile,maximum,worst,name,count,maximum<=MaximumAutomaticCorrectionDegrees){MaximumWorldDegrees=worldMaximum,MaximumBodyAdjustmentDegrees=bodyMaximum,WorstBodyAdjustmentFrame=worstBody,BodyAdjustmentBone=bodyName};
    }
    public static bool Ready(ContactValidation validation,Diagnostic[] diagnostics,string[] losses,bool channelLossesReviewed=false)=>validation.Passed&&!diagnostics.Any(d=>d.Error||d.Code is "UNSUPPORTED_SURFACE_CONTEXT" or "EDGE_EXTENT_REVIEW" or "MIXED_HOLDS" or "CONTACT_PATTERN_REVIEW" or "INTERACTION_CONTEXT_REVIEW" or "SOURCE_ASSOCIATION_REVIEW" or "INFERRED_CLEARANCE_CONTEXT" or "ACTION_FIDELITY_REVIEW" or "LOW_BODY_CONTACT_ADAPTATION" or "JOINT_LIMITS_REVIEW" or "SOURCE_MODE_REVIEW" or "RIG_BINDING_REVIEW" or "RETARGET_COLLISION_REVIEW" or "RETARGET_NATIVE_FIT_BUDGET_REVIEW" or "RETARGET_TRAJECTORY_BUDGET_REVIEW" or "RETARGET_TRAJECTORY_REVIEW" or "RETARGET_HELD_FIT_REVIEW" or "RETARGET_ACTOR_CONTEXT_REVIEW" or "INFERRED_BODY_SUPPORT_ADAPTATION_REVIEW" or "RETARGET_CONTACTS_DISABLED_REVIEW" or "AUTHORED_CONSTRAINTS_NOT_SOLVED" or "RETARGET_POSE_REWRITE_REVIEW" or "RETARGET_RECONSTRUCTION_REVIEW" or "BODY_REACH_REVIEW" or "BODY_REACH_TOPOLOGY_REVIEW" or "BODY_TRAJECTORY_BINDING_REVIEW")&&(losses.Length==0||channelLossesReviewed);
    public static ContactValidation Measure(FighterProject target,ControlRigSetup rig,RetargetBake bake,CancellationToken cancellation=default)
        =>RetargetValidation.Measure(target,rig,bake,cancellation);

}
public record ContactValidation(string Profile,bool Passed,bool Measured,float PositionBound,float PenetrationBound,float Separation,float Penetration,float OrientationRadians,float AdaptedSeparation,double WorstContactFrame,double WorstFloorFrame,int Samples,int RequiredContactSamples,int GroundSamples)
{
    public ContactApplicability Applicability {get;init;}=ContactApplicability.Required;
    public ContactRegionMeasurement[] Regions {get;init;}=[];
    public float PlantDrift {get;init;}
    public float MaximumAffectedVertexFraction {get;init;}
    public int PenetratingGroundSamples {get;init;}
    public Guid ClipRevision {get;init;}
    public string? TargetDeformChecksum {get;init;}
    public bool BodySupportMeasured {get;init;}
    public float BodySupportSeparation {get;init;}
    public double WorstBodySupportFrame {get;init;}
}
public record ContactRegionMeasurement(Guid Pin,string Role,ContactMode Mode,string Support,bool Required,float Position,double WorstFrame,float OrientationRadians,int Samples);

public enum RetargetRowState { Queued,Analyzing,Solving,Preview,Validated,Review,Unresolved,Incompatible,Stale,Cancelled,Applied }
public enum NativeExportState { NotRequested,Staged,Verified,Failed,Partial }
public record RetargetBatchRowReceipt(Guid SourceClip,Guid SourceRevision,string? NativeIdentity,string TargetName,FittingSettings Settings,FittingOverrides Overrides,RetargetRowState State,ContactValidation? Contacts,string[] Issues,Guid? TargetClip);
public record RetargetBatchReceipt(Guid Id,string TargetDependency,string SourceDependency,string SolverVersion,string AcceptanceProfile,RetargetBatchRowReceipt[] Rows)
{
    public string Backend {get;init;}=RetargetBackendIdentity.HistoricalId;
    public NativeExportState NativeStatus {get;init;}=NativeExportState.NotRequested;
    public bool ChannelLossesReviewed {get;init;}
    public RootMotionPolicy RootPolicy {get;init;}
    public RetargetTiming Timing {get;init;}=new();
    public RetargetSupportBinding[] SupportBindings {get;init;}=[];
}
public sealed class RetargetJob
{
    public AnimationClip Source {get;}
    public string TargetName {get;internal set;}
    public FittingOverrides Overrides {get;internal set;}=new();
    public FittingSettings Settings {get;internal set;}=new();
    public RetargetRowState State {get;internal set;}=RetargetRowState.Queued;
    public RetargetBake? Bake {get;internal set;}
    public RetargetBake? UnfittedPreview {get;internal set;}
    public ContactValidation? Validation {get;internal set;}
    public Diagnostic[] Diagnostics {get;internal set;}=[];
    public string[] Losses {get;internal set;}=[];
    public ContactIntervalEdit[] ContactEdits {get;internal set;}=[];
    internal long Generation;
    internal RetargetJob(AnimationClip clip,string name){Source=clip;TargetName=name;}
}

// One bounded CPU job at a time. Independent clips, shared immutable preparation;
// result publication and commit both check the monotonic document generation.
public sealed class RetargetBatch : IDisposable
{
    readonly FighterProject source,target;readonly EditorDocument document;readonly ControlRigSetup a,b;readonly long targetVersion;readonly string sourceDependency,targetDependency;
    readonly GroundContactSettings? ground;readonly RootMotionPolicy root;readonly bool contacts;readonly RetargetTiming timing;
    readonly bool channelLossesReviewed;
    public string Backend=>RetargetBackendIdentity.CurrentId;
    readonly Func<bool>? sourceCurrent;bool sourceStale;
    readonly RetargetSupportBinding[] supportBindings;
    CancellationTokenSource lifetime=new();bool disposed,running;
    CancellationTokenSource? activeWork;Guid? activeClip;
    public bool SaveSharedPresetOnApply {get;set;}
    public Guid Id {get;}=Guid.NewGuid();
    public RetargetJob[] Rows {get;}
    public FittingSettings Shared {get;private set;}
    public bool IsCancelled=>lifetime.IsCancellationRequested;
    public bool TargetIsCurrent=>!disposed&&!sourceStale&&!lifetime.IsCancellationRequested&&document.ContentVersion==targetVersion&&ReferenceEquals(document.AuthoringProject,target)&&document.PoseOverrides.Count==0;
    public event Action? Changed;
    public RetargetBatch(EditorDocument document,FighterProject source,ControlRigSetup sourceRig,ControlRigSetup targetRig,IEnumerable<Guid> clips,FittingSettings? shared=null,GroundContactSettings? ground=null,RootMotionPolicy root=RootMotionPolicy.Preserve,bool contacts=true,RetargetTiming? timing=null,bool channelLossesReviewed=false,Func<bool>? sourceCurrent=null,RetargetSupportBinding[]? supportBindings=null)
    {
        if(document.IsSourcePreview||document.PoseOverrides.Count>0)throw new InvalidOperationException("Bake or discard pending target pose edits before starting a transfer.");
        if(sourceRig.Diagnostics.Concat(targetRig.Diagnostics).Any(d=>d.Code=="AMBIGUOUS_CHAIN"))throw new InvalidOperationException("Review ambiguous source/target control mappings before batch retargeting; no bones will be guessed.");
        this.document=document;this.source=source;target=document.AuthoringProject;a=sourceRig;b=targetRig;targetVersion=document.ContentVersion;this.ground=ground;this.root=root;this.contacts=contacts;this.timing=timing??new();this.channelLossesReviewed=channelLossesReviewed;
        this.sourceCurrent=sourceCurrent;
        this.supportBindings=supportBindings??[];
        string key=ProfileKey();Shared=shared??target.FittingProfiles.LastOrDefault(p=>p.Key==key)?.Settings??new();Shared.Validate();
        var ids=clips.Distinct().ToArray();if(ids.Length==0)throw new ArgumentException("Select at least one animation.");
        var names=target.Clips.SelectMany(c=>new[]{c.Name,c.NativeWriteName??c.Source?.NativeName??c.Name}).Concat(target.NativeLayers.Where(l=>l.Kind!="SCN0").Select(l=>l.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Rows=ids.Select(id=>{var clip=source.Clips.Single(c=>c.Id==id);string basis=(clip.Source?.NativeName??clip.Name)+"_Retargeted",name=basis;for(int suffix=2;!names.Add(name);suffix++)name=basis+"_"+suffix;return new RetargetJob(clip,name){Settings=Shared};}).ToArray();
        sourceDependency=SourceFingerprint();targetDependency=TargetFingerprint();
    }
    void Invalidate(RetargetJob row){row.Generation++;row.State=RetargetRowState.Stale;row.Validation=null;row.UnfittedPreview=null;if(activeClip==row.Source.Id)activeWork?.Cancel();}
    public void SetShared(FittingSettings value)
    {
        ObjectDisposedException.ThrowIf(disposed,this);value.Validate();Shared=value;
        foreach(var row in Rows){var effective=row.Overrides.Resolve(value);if(effective!=row.Settings){row.Settings=effective;Invalidate(row);}}Changed?.Invoke();
    }
    public void SetOverride(Guid clip,FittingOverrides value)
    {
        ObjectDisposedException.ThrowIf(disposed,this);var row=Rows.Single(r=>r.Source.Id==clip);var effective=value.Resolve(Shared);effective.Validate();row.Overrides=value;if(effective!=row.Settings){row.Settings=effective;Invalidate(row);}Changed?.Invoke();
    }
    public void Name(Guid clip,string name)
    {
        if(string.IsNullOrWhiteSpace(name)||name.IndexOfAny(['\0','/','\\'])>=0)throw new ArgumentException("Choose a nonempty native animation name without path separators.");
        Rows.Single(r=>r.Source.Id==clip).TargetName=name;Changed?.Invoke();
    }
    public void SetContactEdits(Guid clip,ContactIntervalEdit[] edits){ObjectDisposedException.ThrowIf(disposed,this);var row=Rows.Single(r=>r.Source.Id==clip);row.ContactEdits=(ContactIntervalEdit[])edits.Clone();Invalidate(row);Changed?.Invoke();}
    public async Task RunAsync(CancellationToken cancellation=default)
    {
        ObjectDisposedException.ThrowIf(disposed,this);if(running)throw new InvalidOperationException("A bounded batch job is already running.");running=true;
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,cancellation);
        try
        {
            foreach(var row in Rows.Where(r=>r.State is not (RetargetRowState.Validated or RetargetRowState.Applied)))
            {
                linked.Token.ThrowIfCancellationRequested();if(!DependenciesCurrent()){MarkStale();break;}long generation=row.Generation;var settings=row.Settings;row.State=RetargetRowState.Analyzing;Changed?.Invoke();
                using var work=CancellationTokenSource.CreateLinkedTokenSource(linked.Token);activeWork=work;activeClip=row.Source.Id;
                try
                {
                    // The pose-first pipeline prepares one ordinary draft.
                    // Do not run a second hidden bake/contact-analysis pass.
                    // Only the matching generation may publish its result.
                    row.Bake=null;row.Validation=null;row.UnfittedPreview=null;
                    if(!Current()){row.State=RetargetRowState.Stale;continue;}row.State=RetargetRowState.Solving;Changed?.Invoke();
                    var edits=(ContactIntervalEdit[])row.ContactEdits.Clone();var output=await Task.Run(()=>RetargetService.Bake(source,row.Source,a,target,b,root,contacts,work.Token,ground:ground,fitting:settings,timing:timing,contactEdits:edits,supportBindings:supportBindings),work.Token);
                    if(!Current()){row.State=RetargetRowState.Stale;continue;}
                    bool proofs=await Task.Run(()=>SourceFingerprint()==sourceDependency&&TargetFingerprint()==targetDependency,work.Token);
                    if(!proofs||!Current()){row.State=RetargetRowState.Stale;row.Validation=null;continue;}
                    row.Diagnostics=output.Diagnostics;row.Losses=output.LostChannels;row.Bake=output.Value is {} candidate?candidate with{Clip=candidate.Clip with{Retarget=candidate.Clip.Retarget! with{ChannelLossesReviewed=channelLossesReviewed}}}:null;
                    if(output.Value is not {} bake){row.State=output.Diagnostics.Any(d=>d.Code=="PROVEN_INCOMPATIBLE")?RetargetRowState.Incompatible:RetargetRowState.Unresolved;Changed?.Invoke();continue;}
                    row.State=RetargetRowState.Preview;Changed?.Invoke();var validation=await Task.Run(()=>RetargetAcceptance.Measure(target,b,bake,work.Token),work.Token);
                    if(!Current()){row.State=RetargetRowState.Stale;continue;}row.Validation=validation;
                    // Context review, unsupported channels and failed measurements never become
                    // validated merely because a native file could be written.
                    bool unresolved=output.Diagnostics.Any(d=>d.Code is "RETARGET_TRAJECTORY_REVIEW" or "RETARGET_TRAJECTORY_BUDGET_REVIEW" or "RETARGET_NATIVE_FIT_BUDGET_REVIEW")||
                        validation.Applicability==ContactApplicability.Required&&validation.Measured&&!validation.Passed;
                    row.State=RetargetAcceptance.Ready(validation,output.Diagnostics,output.LostChannels,channelLossesReviewed)?RetargetRowState.Validated:unresolved?RetargetRowState.Unresolved:RetargetRowState.Review;Changed?.Invoke();
                    if(row.Bake is {} stored)row.Bake=stored with{Clip=stored.Clip with{Retarget=stored.Clip.Retarget! with{Validation=validation,IsDraft=row.State!=RetargetRowState.Validated}}};
                }
                catch(OperationCanceledException){row.State=row.Generation==generation?RetargetRowState.Cancelled:RetargetRowState.Stale;if(linked.IsCancellationRequested)throw;}
                catch(Exception e){if(Current()){row.Bake=null;row.Diagnostics=[new("RETARGET_JOB",e.Message)];row.State=RetargetRowState.Unresolved;}else row.State=RetargetRowState.Stale;Changed?.Invoke();}
                finally{activeWork=null;activeClip=null;}
                bool Current()=>!disposed&&!work.IsCancellationRequested&&row.Generation==generation&&DependenciesCurrent();
            }
        }
        finally{running=false;Changed?.Invoke();}
    }
    public void MarkStale(){foreach(var row in Rows.Where(r=>r.State!=RetargetRowState.Applied))Invalidate(row);Changed?.Invoke();}
    bool DependenciesCurrent(){if(sourceCurrent?.Invoke()==false)sourceStale=true;return TargetIsCurrent;}
    string SourceFingerprint()=>RetargetDependencies.Hash(new{Project=source,Rig=a,Detector=SurfaceContacts.Version,Pipeline=RetargetPipeline.Version});
    string TargetFingerprint()=>RetargetDependencies.Hash(new{Project=target,Rig=b,Supports=supportBindings,Ground=ground,Root=root,Acceptance=RetargetAcceptance.Version,Backend,Solver=RetargetPipeline.Version});
    string ProfileKey(){string key=RetargetDependencies.ProfileKey(source.Scene,target.Scene,root,a.Id,b.Id,supportBindings,RetargetDependencies.Hash(a),RetargetDependencies.Hash(b));return RetargetDependencies.Hash(new{key,Backend});}
    public void Cancel(){lifetime.Cancel();foreach(var row in Rows.Where(r=>r.State is RetargetRowState.Queued or RetargetRowState.Analyzing or RetargetRowState.Solving or RetargetRowState.Preview))row.State=RetargetRowState.Cancelled;Changed?.Invoke();}
    public RetargetBatchReceipt Receipt()=>new(Id,targetDependency,sourceDependency,RetargetPipeline.Version,RetargetAcceptance.Version,Rows.Select(r=>new RetargetBatchRowReceipt(r.Source.Id,r.Source.Revision,r.Source.Source?.Resource.Entry,r.TargetName,r.Settings,r.Overrides,r.State,r.Validation,r.Diagnostics.Select(d=>d.Message).Concat(r.Losses).ToArray(),r.Bake?.Clip.Id)).ToArray()){Backend=Backend,ChannelLossesReviewed=channelLossesReviewed,RootPolicy=root,Timing=timing,SupportBindings=supportBindings};
    public void Apply(bool passedRowsOnly=false)
    {
        if(running||!DependenciesCurrent())throw new InvalidOperationException("The source/target changed, work was cancelled, or a job is running. Rebuild this batch before Apply.");
        if(SourceFingerprint()!=sourceDependency||TargetFingerprint()!=targetDependency){MarkStale();throw new InvalidOperationException("Source/target dependencies changed. Reprepare before Apply.");}
        var ready=Rows.Where(r=>r.State==RetargetRowState.Validated&&r.Bake is not null&&r.Validation?.Passed==true).ToArray();
        if(ready.Length==0||!passedRowsOnly&&ready.Length!=Rows.Length)throw new InvalidOperationException($"{ready.Length}/{Rows.Length} rows validate. Review failures or explicitly Apply passed rows ({ready.Length}).");
        var names=target.Clips.SelectMany(c=>new[]{c.Name,c.NativeWriteName??c.Source?.NativeName??c.Name}).Concat(target.NativeLayers.Where(l=>l.Kind!="SCN0").Select(l=>l.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(ready.Any(r=>!names.Add(r.TargetName)))throw new InvalidOperationException("A target animation name collides. Resolve names before Apply; existing clips are protected.");
        var receipt=Receipt();var committed=receipt with{Rows=receipt.Rows.Select(r=>ready.Any(j=>j.Source.Id==r.SourceClip)?r with{State=RetargetRowState.Applied}:r).ToArray()};
        document.Transaction($"Apply {ready.Length} retargeted animation(s)",p=>
        {
            foreach(var row in ready){var bake=row.Bake!;p=RetargetService.Apply(p,bake with{Clip=bake.Clip with{Name=row.TargetName,NativeWriteName=row.TargetName}});}
            string profileKey=ProfileKey();
            return p with{ControlRigs=p.ControlRigs.Where(r=>r.SkeletonFingerprint!=b.SkeletonFingerprint).Append(b).ToArray(),RetargetBatches=p.RetargetBatches.Append(committed).ToArray(),FittingProfiles=SaveSharedPresetOnApply?p.FittingProfiles.Where(p=>p.Key!=profileKey).Append(new(profileKey,"Saved body fitting",Shared,root)).ToArray():p.FittingProfiles};
        });
        document.SelectClip(Array.FindIndex(document.Project.Clips,c=>c.Id==ready[0].Bake!.Clip.Id));foreach(var row in ready)row.State=RetargetRowState.Applied;Changed?.Invoke();
    }
    public void Dispose(){if(disposed)return;disposed=true;Cancel();lifetime.Dispose();}
}
