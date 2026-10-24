using System.Numerics;

namespace FighterPort.Core;

public record BodyReachChainReceipt(string Role,int[] SourceBones,int[] TargetBones,int SourceAnchor,int TargetAnchor,
    int Evaluations,int Solved,int AlreadyMatched,int Unreachable,int Unsupported,int LimitConflicts,int NonConverged,
    int MissingBendEvidence,float MaximumUnfittedError,float MaximumFittedError,double WorstSourceFrame,
    float MaximumCorrectionDegrees,float MaximumDistalOrientationError)
{
    public int ObservedSourceBendEvaluations {get;init;}
    public int TargetRestBendPriorEvaluations {get;init;}
    public string[] BendProvenance {get;init;}=[];
    public float MaximumUnfittedShapeHintError {get;init;}
    public float MaximumFittedShapeHintError {get;init;}
}
public record BodyReachReceipt(string Version,string GoalVersion,string SolverVersion,bool Enabled,bool ExactRig,
    BodyReachChainReceipt[] Chains,int Evaluations,bool PositionalGoalsSatisfied,string[] OpenGates)
{
    public string PositionUnits {get;init;}="Target native model units";
    public string OrientationUnits {get;init;}="Degrees";
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? SurfaceGoalVersion {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? SurfaceRelationships {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? SurfaceReachAdaptations {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public float? MaximumRequestedSurfaceReachExcess {get;init;}
}

// One offline preparation stage in the normal pipeline. Source-relative reach
// objectives are not world pins or contact evidence. Evaluation is a pure
// function of the exact source pose/rig pair; aggregate receipts never seed it.
public sealed class BodyReachFitting
{
    public const string Version="native-body-relative-reach-5";
    public const long MaximumKinematicSteps=100000000;
    readonly RetargetRequest request;
    readonly RigDescriptor source,target;
    readonly BaselineMotionTransfer baseline;
    readonly BodyRelativeReachGoals goals;
    readonly HashSet<string> overlapping=[];
    readonly Dictionary<string,Measurements> measurements=new(StringComparer.Ordinal);
    readonly bool enabled;
    readonly PreparedGeometry geometry;
    public SceneBodyFitting SceneFitting {get;}
    int evaluations;
    NativeMotionTrajectory? trajectory;
    RetargetTiming? preparedTiming;
    RationalRate preparedRate;
    int preparedKnots;
    bool preparationComplete,preparedBaselineOnly;
    int? surfaceRelationships;
    int? surfaceReachAdaptations;
    float? maximumRequestedSurfaceReachExcess;
    (int Samples,bool Closing,bool Held,bool Exact) preparedSignature;
    public TemporalBodyFittingReceipt? TemporalReceipt=>trajectory?.Receipt();
    sealed class Measurements(BodyRelativeReachBinding binding)
    {
        public readonly BodyRelativeReachBinding Binding=binding;
        public int Count,Solved,AlreadyMatched,Unreachable,Unsupported,LimitConflicts,NonConverged,MissingBend;
        public int SourceBend,RestBend;
        public readonly HashSet<string> Provenance=new(StringComparer.Ordinal);
        public float Unfitted,Fitted,Correction,Distal,UnfittedShape,FittedShape;public double Worst;
        public BodyReachChainReceipt Receipt()=>new(Binding.Role,Binding.SourceBones,Binding.TargetBones,Binding.SourceAnchor,Binding.TargetAnchor,
            Count,Solved,AlreadyMatched,Unreachable,Unsupported,LimitConflicts,NonConverged,MissingBend,Unfitted,Fitted,Worst,Correction,Distal)
        {ObservedSourceBendEvaluations=SourceBend,TargetRestBendPriorEvaluations=RestBend,BendProvenance=Provenance.Order(StringComparer.Ordinal).ToArray(),
         MaximumUnfittedShapeHintError=UnfittedShape,MaximumFittedShapeHintError=FittedShape};
    }
    public Diagnostic[] Diagnostics {get;private set;}
    public BodyRelativeReachGoals Goals=>goals;
    internal void ValidateEvaluationBudget(int references,int probes)
    {
        if(!preparationComplete)SceneFitting.ValidateEvaluationBudget(references,probes);
        if(!enabled)return;
        // Reach evaluates one extra complete target world pose for each motion
        // query. Keep that work in the shared reconstruction budget as well as
        // the path-specific iterative work below. Validation already counts its
        // independently evaluated reference and baked target worlds.
        long targetBones=target.Scene.Bones.Length,sourceBones=source.Scene.Bones.Length;
        if((long)references*(sourceBones+2*targetBones)>PoseMotionReconstruction.MaximumCombinedBonePoses||
           (long)probes*(sourceBones+3*targetBones)>PoseMotionReconstruction.MaximumCombinedBonePoses)
            throw new NotSupportedException("Body-relative reach exceeds the combined source/target evaluation work budget before sampling.");
        // Sampling a prepared correction field (or an explicitly documented
        // baseline-only draft) never runs native-path iterative projection.
        // Preserve the actual source/target pose budget above, but do not
        // reject that pure path for iterations it does not execute.
        if(trajectory is not null||preparedBaselineOnly||baseline.ExactRig)return;
        long cost=goals.Bindings.Sum(b=>(long)b.TargetBones.Length*b.TargetBones.Length*(NativeChainReach.MaximumIterations+1));
        if(cost>0&&(long)references+probes>MaximumKinematicSteps/cost)
            throw new NotSupportedException("Body-relative reach exceeds its declared offline native-path projection work budget before sampling.");
    }
    public BodyReachFitting(RetargetRequest request,RigDescriptor source,RigDescriptor target,BaselineMotionTransfer baseline)
    {
        this.request=request;this.source=source;this.target=target;this.baseline=baseline;
        enabled=(request.Fitting??new()).AutomaticFitting;
        geometry=new(request.Source,request.Clip);
        SceneFitting=new(request,source,target,baseline);
        goals=new(source,target,baseline.Mapping);
        foreach(var binding in goals.Bindings)measurements.Add(binding.Role,new(binding));
        // Independent branches can be fitted without order dependence. A
        // connected/shared-joint system needs the later coupled objective;
        // don't silently let the final chain overwrite an earlier one.
        foreach(var a in goals.Bindings)foreach(var b in goals.Bindings)
            if(a!=b&&(a.TargetBones.Intersect(b.TargetBones).Any()||a.TargetBones.Any(p=>PoseGeometry.Ancestor(target.Scene,p,b.TargetBones[0])||PoseGeometry.Ancestor(target.Scene,p,b.TargetAnchor))))
            {overlapping.Add(a.Role);overlapping.Add(b.Role);}
        Diagnostics=goals.Diagnostics.Concat(SceneFitting.Diagnostics).Concat(overlapping.Count>0?[new Diagnostic("BODY_REACH_TOPOLOGY_REVIEW","Overlapping native reach paths need coupled fitting; their transferred poses remain unchanged and unvalidated.",false)]:[]).ToArray();
    }
    public Transform[] Evaluate(double sourceFrame,Vector3[]? windingReference=null)
    {
        if(preparedBaselineOnly)
        {
            request.Cancellation.ThrowIfCancellationRequested();evaluations++;
            return baseline.EvaluatePose(geometry.Evaluate(sourceFrame),windingReference);
        }
        if(trajectory is null)return EvaluateCore(sourceFrame,windingReference,true);
        // The prepared path has no fractional IK or scene optimization. Reach
        // goals participated in the joint solve; sampling uses source transfer
        // plus the one immutable correction field.
        var input=baseline.EvaluatePose(geometry.Evaluate(sourceFrame),windingReference);
        double frame=preparedTiming!.TargetFrame(sourceFrame,request.Clip.Rate.FramesPerSecond,preparedRate.FramesPerSecond);
        return SceneFitting.Articulation!.ApplyCorrection(input,trajectory.EvaluateParameters(Math.Clamp(frame,0,preparedKnots-1)));
    }
    public Transform[] EvaluateUnfitted(double sourceFrame,Vector3[]? windingReference=null)=>EvaluateCore(sourceFrame,windingReference,false);
    Transform[] EvaluateCore(double sourceFrame,Vector3[]? windingReference,bool applyScene)
    {
        request.Cancellation.ThrowIfCancellationRequested();
        var authentic=SceneFitting.Enabled?geometry.Evaluate(sourceFrame):AnimationService.Evaluate(source.Scene,request.Clip,sourceFrame);
        var visibility=SceneFitting.Enabled?Enumerable.Range(0,source.Scene.Meshes.Length).Select(m=>geometry.MeshVisible(m,sourceFrame)).ToArray():null;
        Func<int,bool>? visible=visibility is null?null:m=>visibility[m];
        var local=baseline.EvaluatePose(authentic,windingReference);evaluations++;
        if(!enabled)return local;
        if(baseline.ExactRig||goals.Bindings.Length==0)return applyScene?SceneFitting.Fit(authentic,local,visible):local;
        var pose=AnimationService.Evaluate(target.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
        foreach(var goal in goals.Evaluate(authentic,pose))
        {
            request.Cancellation.ThrowIfCancellationRequested();var binding=goal.Binding;var m=measurements[binding.Role];
            float before=Vector3.Distance(pose.World[binding.TargetBones[^1]].Translation,goal.Position);m.Count++;m.Unfitted=Math.Max(m.Unfitted,before);
            m.Provenance.Add(goal.BendProvenance);
            if(goal.SourceBendObserved)m.SourceBend++;else if(goal.HasBendEvidence)m.RestBend++;
            NativeChainReachResult result;
            if(overlapping.Contains(binding.Role))
            {
                float shape=goal.JointHints.Select(h=>Vector3.Distance(pose.World[h.TargetBone].Translation,h.Position)).DefaultIfEmpty(0).Max();
                result=new(local,"Unsupported",before,binding.TargetRestLength,0,0,0,goal.HasBendEvidence,"Shared-joint constraints need a coupled objective.")
                    {InitialShapeHintError=shape,MaximumShapeHintError=shape};
            }
            else
            {
                var hints=binding.TargetBones.Select(b=>pose.World[b].Translation).ToArray();
                foreach(var hint in goal.JointHints)hints[Array.IndexOf(binding.TargetBones,hint.TargetBone)]=hint.Position;
                result=NativeChainReach.SolvePrepared(target,local,binding.TargetBones,goal.Position,
                    goal.HasBendEvidence?goal.BendDirection:null,pose,request.TargetRig.JointLimits,request.Cancellation,hints);
            }
            local=result.Local;
            switch(result.Status)
            {
                case "Solved":m.Solved++;break;case "AlreadyMatched":m.AlreadyMatched++;break;case "Unreachable":m.Unreachable++;break;
                case "LimitConflict":m.LimitConflicts++;break;case "NonConverged":m.NonConverged++;break;default:m.Unsupported++;break;
            }
            if(!result.HasBendEvidence&&result.Status=="Solved")m.MissingBend++;
            if(result.PositionError>m.Fitted){m.Fitted=result.PositionError;m.Worst=sourceFrame;}
            m.Correction=Math.Max(m.Correction,result.MaximumCorrectionDegrees);m.Distal=Math.Max(m.Distal,result.DistalOrientationError);
            m.UnfittedShape=Math.Max(m.UnfittedShape,result.InitialShapeHintError);m.FittedShape=Math.Max(m.FittedShape,result.MaximumShapeHintError);
        }
        return applyScene?SceneFitting.Fit(authentic,local,visible):local;
    }
    public void PrepareTrajectory(RetargetTiming timing,RationalRate rate,int samples,bool closing,bool held,bool exact)
    {
        request.Cancellation.ThrowIfCancellationRequested();
        int keys=checked(samples+(closing?1:0));
        if(preparationComplete)
        {
            if(timing!=preparedTiming||rate!=preparedRate||keys!=preparedKnots||preparedSignature!=(samples,closing,held,exact))throw new InvalidOperationException("Prepared motion belongs to another timing revision.");
            return;
        }
        preparedTiming=timing;preparedRate=rate;preparedKnots=keys;preparedSignature=(samples,closing,held,exact);
        if(exact||!enabled||!SceneFitting.Enabled||!SceneFitting.ChangesGeometryIdentity||
            (request.Fitting??new()).VolumePolicy!=BodyVolumePolicy.ReportOverlap)
        {preparationComplete=true;return;}
        if(SceneFitting.Articulation is not {Enabled:true} articulation)
        {
            // Existing projects may have a reviewed body binding without
            // reviewed virtual chain paths. Preserve a usable transfer preview,
            // explicitly without any reach/scene solve or inferred readiness.
            Diagnostics=[..Diagnostics,new("BODY_TRAJECTORY_BINDING_REVIEW",
                "Coordinated body fitting needs reviewed existing native articulation paths. This draft contains baseline transfer only; no independent pose solver was substituted.",false)];
            preparedBaselineOnly=true;preparationComplete=true;return;
        }
        int subdivisions=held?1:2,count=checked((keys-1)*subdivisions+1);
        bool periodic=request.Clip.Loop&&timing.TrimStartSeconds==0&&timing.TrimEndSeconds is null;
        NativeMotionTrajectory.ValidateBudget(keys,articulation.Variables,count,periodic);
        // Published collocations also re-evaluate their original and final
        // costs for actual receipts; include both full geometry passes.
        SceneFitting.ValidateEvaluationBudget(count,0,extraCosts:2);
        var surface=new BodySurfaceReachGoals(source,target,baseline.Mapping,goals.Bindings,request.Cancellation);
        articulation.ValidateSurfaceBudget(count,surface);surfaceRelationships=surface.Relationships;surfaceReachAdaptations=0;maximumRequestedSurfaceReachExcess=0;
        var frames=new List<NativeTrajectoryFrame>();var inputs=new List<Transform[]>();var sources=new List<EvaluatedPose>();
        Vector3[]? winding=null;
        for(int i=0;i<count;i++)
        {
            request.Cancellation.ThrowIfCancellationRequested();double frame=i/(double)subdivisions;
            double sourceFrame=timing.SourceFrame(frame,request.Clip.Rate.FramesPerSecond,rate.FramesPerSecond);
            var authentic=geometry.Evaluate(sourceFrame);var input=baseline.EvaluatePose(authentic,winding);
            winding=input.Select(t=>t.Rotation).ToArray();
            var visibility=Enumerable.Range(0,source.Scene.Meshes.Length).Select(m=>geometry.MeshVisible(m,sourceFrame)).ToArray();
            var residuals=SceneFitting.PrepareObjective(authentic,input,m=>visibility[m]);
            if(residuals is null)throw new NotSupportedException("A canonical source-time sample has no supported scene observations; it was not replaced by another per-pose fit.");
            // Source-derived reach is an objective in the shared optimization,
            // not another independently solved pose used as its moving base.
            var transferred=AnimationService.Evaluate(target.Scene,null,0,input.Select((t,j)=>(t,j)).ToDictionary(p=>p.j,p=>p.t));
            var reachGoals=goals.Evaluate(authentic,transferred);
            var surfaceGoals=surface.Evaluate(authentic,transferred,m=>visibility[m]);
            surfaceReachAdaptations+=surfaceGoals.Count(g=>g.ReachAdapted);
            maximumRequestedSurfaceReachExcess=Math.Max(maximumRequestedSurfaceReachExcess??0,surfaceGoals.Select(g=>Math.Max(0,g.RequestedReach-g.AvailableReach)).DefaultIfEmpty(0).Max());
            frames.Add(new(frame,articulation.CreateObjective(input,residuals,input,reachGoals,surfaceGoals)));inputs.Add(input);sources.Add(authentic);
        }
        var prepared=new NativeMotionTrajectory(frames.ToArray(),keys,rate.FramesPerSecond,held,request.Cancellation,periodic);
        if(surfaceReachAdaptations>0)Diagnostics=[..Diagnostics,new("BODY_SURFACE_REACH_ADAPTATION_REVIEW",
            $"{surfaceReachAdaptations} soft source-surface observations exceeded the target's current native chain span and were radially planned at its geometric workspace boundary; maximum requested excess {maximumRequestedSurfaceReachExcess:G9} target units. Original requests remain distinct. This is not a joint-limit, collision, support or action-quality certificate; authored pins are unchanged and unsolved.",false)];
        for(int i=0;i<frames.Count;i++)
        {
            request.Cancellation.ThrowIfCancellationRequested();var x=prepared.EvaluateParameters(frames[i].Frame);
            var final=frames[i].Objective.Evaluate(x,false);var initial=frames[i].Objective.Evaluate(new double[articulation.Variables],false);
            articulation.RecordTrajectory(inputs[i],final.Local,initial.Cost,final.Cost);
            SceneFitting.RecordPreparedPose(sources[i],inputs[i],final.Local);
        }
        trajectory=prepared;preparationComplete=true;
    }
    public BodyReachReceipt Receipt()
    {
        var rows=measurements.Values.Select(m=>m.Receipt()).ToArray();
        bool satisfied=enabled&&!baseline.ExactRig&&rows.Length>0&&SceneFitting.Receipt().AppliedProposals==0&&trajectory?.Receipt().AcceptedSteps is not >0&&!Diagnostics.Any(d=>d.Code=="RIG_BINDING_REVIEW")&&
            rows.All(r=>r.Evaluations>0&&r.Unreachable+r.Unsupported+r.LimitConflicts+r.NonConverged+r.MissingBendEvidence==0);
        return new(Version,BodyRelativeReachGoals.Version,NativeChainReach.Version,enabled,baseline.ExactRig,rows,evaluations,satisfied,
            ["source action/style quality","body-volume/clearance and coordinated torso fitting","contact reconstruction unavailable","coupled shared-joint paths","native/subframe/display acceptance"])
            {SurfaceGoalVersion=surfaceRelationships is null?null:BodySurfaceReachGoals.Version,SurfaceRelationships=surfaceRelationships,
             SurfaceReachAdaptations=surfaceReachAdaptations,MaximumRequestedSurfaceReachExcess=maximumRequestedSurfaceReachExcess};
    }
}
