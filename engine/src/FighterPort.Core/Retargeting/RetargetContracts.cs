namespace FighterPort.Core;

// Historical identity is read-only project provenance, never an executable backend.
public static class RetargetBackendIdentity
{
    public const string HistoricalId="retarget.legacy";
    // Keep the persisted identity of existing replacement results stable.
    public const string CurrentId="retarget.temporal-v2";
}
public record RetargetRequest(FighterProject Source,AnimationClip Clip,ControlRigSetup SourceRig,FighterProject Target,ControlRigSetup TargetRig,
    RootMotionPolicy RootPolicy,bool UseContacts,CancellationToken Cancellation,RationalRate? TargetRate,GroundContactSettings? Ground,
    FittingSettings? Fitting,RetargetTiming? Timing,RetargetSourceAnalysis? Analysis,ContactIntervalEdit[]? ContactEdits,RetargetSupportBinding[]? Supports);

public record RetargetStageReceipt(string Stage,string Version,double Milliseconds);
public record RetargetPipelineReceipt(string Backend,int Schema,RetargetStageReceipt[] Stages,string[] Capabilities,string[] OpenGates)
{
    public TemporalSolveReceipt? Temporal {get;init;}
    public NativeCurveFitReceipt? NativeCurves {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TrajectorySolveReceipt? Trajectory {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PoseMotionReceipt? Reconstruction {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public BodyReachReceipt? BodyReach {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SceneBodyFitReceipt? SceneBody {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TemporalBodyFittingReceipt? BodyTrajectory {get;init;}
    public MotionContactCoverage ContactCoverage {get;init;}
}
