namespace FighterPort.Core;

// Read compatibility for rejected experiment receipts. New normal results do
// not run whole-clip rotation initialization or relabel historical evidence.
public record CoherentCurveReceipt(string Version,int PoseEvaluations,int Iterations,int AcceptedUpdates,bool BudgetExhausted,
    double InitialMaximumViolation,double FinalMaximumViolation,string Coverage);
public record TrajectorySolveReceipt(string Version,string Initialization,int PoseEvaluations,int EvaluationBudget,bool BudgetExhausted,
    double MaximumNormalizedConstraint,double ActionCostBeforeActionPass,double MeanActionCost,CurveActionReceipt? Action,string Coverage)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CoherentCurveReceipt? CoherentInitialization {get;init;}
    public FreeChannelRecoveryReceipt? FreeChannelRecovery {get;init;}
    public NativeCurveFitReceipt? FinalConstraintRestoration {get;init;}
    public double? ActionCostAfterActionPass {get;init;}
    public double? MeanGeometricActionCost {get;init;}
    public double? MeanInferredContactCost {get;init;}
    public double? DiagnosticNativeLinkFrameCost {get;init;}
}
public record PreparedTargetTrajectory(BoneTrack[] Tracks,double[] Frames,Transform[][] Poses,ContactClearance? EffectiveClearance,
    NativeCurveFitReceipt NativeCurves,TrajectorySolveReceipt Receipt)
{
    public BoneTrack[] InitialTracks {get;init;}=[];
}

// One authoritative destination representation. Initialization, constraints,
// temporal preferences, playback and native encoding all consume these curves.
// There is no early return because an independent pose lattice failed to solve,
// and no later bake that can invalidate accepted coefficient-space constraints.
public static class RetargetTrajectorySolver
{
    public const string Version="native-curve-trajectory-21";
    public static OperationResult<PreparedTargetTrajectory> Solve(TargetMotionPlan plan,bool held,CancellationToken cancellation=default,
        int? evaluationBudget=null,SolvedTargetMotion? initialization=null,Action<string,BoneTrack[]>? trace=null)
    {
        cancellation.ThrowIfCancellationRequested();
        int keys=plan.Samples+(plan.Closing?1:0),count=(keys-1)*4+1;
        if(count>4096)return new(null,[new("TRAJECTORY_CAPACITY","The prepared curve trajectory currently supports at most 4096 quarter samples; no animation was truncated.")],[]);
        int budget=evaluationBudget??NativeCurveBaker.DefaultBudget(plan.Rig.Scene.Bones.Length);
        int validationBudget=count*4;
        if(budget<validationBudget)return new(null,[new("TRAJECTORY_BUDGET",$"The requested budget {budget} cannot evaluate the complete {count}-sample output and action reference (minimum {validationBudget}).")],[]);
        var frames=Enumerable.Range(0,count).Select(i=>i/4d).ToArray();
        var references=new Transform[count][];System.Numerics.Vector3[]? winding=null;
        for(int i=0;i<count;i++){cancellation.ThrowIfCancellationRequested();references[i]=plan.Reference(frames[i],winding);winding=references[i].Select(t=>t.Rotation).ToArray();}
        var seed=initialization??new(frames,references,new("Source-derived curve initialization",count,0,0,0,0,0,0,"Not independently solved poses"));
        var baseline=NativeCurveBaker.Initialize(plan,new(frames,references,new("Baseline curve reference",count,0,0,0,0,0,0,"Source-derived action")),held,cancellation);
        var tracks=NativeCurveBaker.Initialize(plan,seed,held,cancellation);
        var initialTracks=tracks;
        void Trace(string stage,BoneTrack[] value)
        {
            // Optional offline diagnostics own their arrays. Normal preparation
            // and playback allocate no stage copies or observer work.
            if(trace is null)return;
            trace(stage,value.Select(t=>t with{Channels=t.Channels.Select(c=>c is null?null:c with{Keys=c.Keys.ToArray()}).ToArray(),Rotation=t.Rotation is {} q?q with{Keys=q.Keys.ToArray()}:null}).ToArray());
        }
        Trace("source-derived coefficients",tracks);
        var optimizedPlan=plan.Clearance is {} clearance?plan with{Clearance=clearance with{Points=plan.Rig.SkinPoints}}:plan;
        var actionStart=tracks;var actionEnd=tracks;
        int work=budget-validationBudget,used=0,recoveryBudget=Math.Min(50000,work/4),polishBudget=Math.Min(400000,work/5);CurveActionReceipt? action=null;FreeChannelRecoveryReceipt? recovery=null;NativeCurveFitReceipt? polish=null;
        var native=new NativeCurveFitReceipt(NativeCurveBaker.Version,0,work,0,0,false);
        if(!held&&keys>1)
        {
            var restored=NativeCurveProjection.Refine(optimizedPlan,tracks,cancellation,Math.Max(1,(work-used-recoveryBudget-polishBudget)*3/5),preferred:baseline);
            tracks=restored.Tracks;actionStart=tracks;native=restored.Receipt;used+=native.PoseEvaluations;
            Trace("initial required restoration",tracks);
            if(work-used-recoveryBudget-polishBudget>0)
            {
                var refined=CurveTrajectoryAction.Refine(optimizedPlan,tracks,baseline,cancellation,work-used-recoveryBudget-polishBudget);
                tracks=refined.Tracks;action=refined.Receipt;used+=action.PoseEvaluations;
            }
            actionEnd=tracks;
            Trace("action optimization",tracks);
            // Action minimization may start from an infeasible trajectory. Its
            // finite window passes cannot certify the remaining constraints.
            // Spend a reserved budget restoring the same final coefficients,
            // rather than declaring completion with unused work and a bad pin.
            if(work-used-recoveryBudget>0)
            {
                // The reserve is a minimum, not a reason to abandon unused
                // preliminary work while required constraints still fail.
                var final=NativeCurveProjection.Refine(optimizedPlan,tracks,cancellation,work-used-recoveryBudget,maximumSweeps:64,keyRadius:3,preferred:baseline);
                tracks=final.Tracks;polish=final.Receipt;used+=polish.PoseEvaluations;
                Trace("final required restoration",tracks);
            }
            var recovered=FreeChannelRecovery.Recover(optimizedPlan,tracks,baseline,cancellation,work-used);
            tracks=recovered.Tracks;recovery=recovered.Receipt;used+=recovery.PoseEvaluations;
            Trace("free-channel recovery",tracks);
        }
        var scene=plan.Rig.Scene;var clip=new AnimationClip(Guid.NewGuid(),"Prepared trajectory",plan.Samples,plan.Rate,plan.Loop,tracks);
        var poses=new Transform[count][];double maximum=0,cost=0,initialCost=0,afterActionCost=0,geometryCost=0,observationCost=0,nativeFrameCost=0;bool limitsPassed=true;
        for(int i=0;i<count;i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var evaluated=AnimationService.Evaluate(scene,clip,frames[i]) with{Seconds=frames[i]/plan.Rate.FramesPerSecond};
            poses[i]=evaluated.Local;
            limitsPassed&=(plan.Controls.JointLimits??[]).All(l=>Enumerable.Range(0,3).All(a=>evaluated.Local[l.Bone].Rotation[a]>=l.MinimumDegrees[a]-.0001f&&evaluated.Local[l.Bone].Rotation[a]<=l.MaximumDegrees[a]+.0001f));
            maximum=Math.Max(maximum,TargetMotionConstraints.MaximumNormalizedViolation(optimizedPlan,frames[i],evaluated));
            var reference=AnimationService.Evaluate(scene,null,0,references[i].Select((t,b)=>(t,b)).ToDictionary(p=>p.b,p=>p.t));
            var objective=PreparedTrajectoryAction.Create(plan,frames[i],reference);
            geometryCost+=objective.GeometricCost(evaluated);observationCost+=objective.ObservationCost(evaluated);
            nativeFrameCost+=PreparedArticulationAction.Create(plan.Rig,plan.Baseline.Mapping,reference).NativeLinkFrameCost(evaluated,reference);
            cost+=objective.Cost(evaluated);initialCost+=objective.Cost(AnimationService.Evaluate(scene,clip with{Tracks=actionStart},frames[i]));
            afterActionCost+=objective.Cost(AnimationService.Evaluate(scene,clip with{Tracks=actionEnd},frames[i]));
        }
        bool exhausted=native.BudgetExhausted||action?.BudgetExhausted==true||polish?.BudgetExhausted==true||recovery?.BudgetExhausted==true;
        var receipt=new TrajectorySolveReceipt(Version,initialization is null?"Source-derived baseline coefficients":"Explicit diagnostic coefficient seed",
            used+validationBudget,budget,exhausted,maximum,initialCost/count,cost/count,action,
            "Final curve quarter samples; separate geometry/soft-contact costs, required constraints and unrated native-link frame evidence. Native/display validation remain independent. All retained meshes, no dynamic visibility mask."){FreeChannelRecovery=recovery,FinalConstraintRestoration=polish,ActionCostAfterActionPass=afterActionCost/count,MeanGeometricActionCost=geometryCost/count,MeanInferredContactCost=observationCost/count,DiagnosticNativeLinkFrameCost=nativeFrameCost/count};
        var diagnostics=new List<Diagnostic>();
        if(exhausted)diagnostics.Add(new("RETARGET_TRAJECTORY_BUDGET_REVIEW",$"Curve trajectory used {receipt.PoseEvaluations}/{budget} pose evaluations; bounded optimization stopped before convergence.",false));
        if(maximum>1)diagnostics.Add(new("RETARGET_TRAJECTORY_REVIEW",$"Final curve samples retain normalized required-constraint violation {maximum:G9}; output remains an unvalidated draft, not proven incompatible anatomy.",false));
        if(!limitsPassed)diagnostics.Add(new("JOINT_LIMITS_REVIEW","Final curve samples exceed reviewed native joint limits; successful contact restoration does not establish anatomical feasibility.",false));
        if(held&&(plan.Graph.Relations.Length>0||plan.Clearance is not null))diagnostics.Add(new("RETARGET_HELD_FIT_REVIEW","Held keys retain their native step representation; articulated constraint fitting for held intervals is not yet implemented.",false));
        // Complete skin is transient optimization/acceptance geometry, not a
        // per-pin serialized calibration array. Retain the document's bounded
        // contact schema without discarding any vertices or influences from
        // Rig.Scene / Rig.SkinPoints or the full-skin constraint measurements.
        var storedClearance=plan.Clearance;
        if(storedClearance is {} stored)
            storedClearance=stored with{Points=stored.Points.Length<=512?stored.Points:SurfaceContacts.Clearance(new("Collision calibration",-1,stored.Points)),
                SupportPoints=stored.SupportPoints.Length<=512?stored.SupportPoints:SurfaceContacts.Clearance(new("Support calibration",-1,stored.SupportPoints))};
        return new(new PreparedTargetTrajectory(tracks,frames,poses,storedClearance,native with{PoseEvaluations=used,EvaluationBudget=work},receipt){InitialTracks=initialTracks},diagnostics.ToArray(),[]);
    }
}
