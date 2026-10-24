using System.Numerics;
using FighterPort.Core;

internal static class ConstraintProjectionChecks
{
    public static void Run(Action<bool,string> check)
    {
        double[,] tetherRows={{1,1}};
        var tether=FeasibleCurveDirection.SourceTether(tetherRows,[-1],[0,2],.1);
        check(Math.Abs(tether[0]+10d/21)<1e-9&&Math.Abs(tether[1]-32d/21)<1e-9,
            "Source-tethered restoration matches the independent two-variable proximal solution, retaining the free source direction");
        double[,] scaledRows={{100,100}};
        var scaledTether=FeasibleCurveDirection.SourceTether(scaledRows,[-100],[0,2],.1);
        check(tether.Zip(scaledTether).All(p=>Math.Abs(p.First-p.Second)<1e-9),
            "Equivalent scaling of constraint units leaves the relative-damping correction unchanged");
        var neutral=FeasibleCurveDirection.SourceTether(tetherRows,[0],[0,0],.1);
        check(neutral.All(v=>v==0),"A satisfied source pose receives no source-tether correction");
        double[,] linear={{1,0},{1,1}};double[] error=[.2,2];
        var priority=FeasibleCurveDirection.Solve(linear,error,[0]);
        check(Math.Abs(priority[0]+.2)<1e-12&&Math.Abs(priority[0]+priority[1]+2)<.02,"Feasibility-priority linear step restores the independently specified satisfied coordinate while reducing the other error");
        double[,] repeated={{2,0},{1,0},{1,1}};
        var dependent=FeasibleCurveDirection.Solve(repeated,[.4,.2,2],[0,1]);
        check(priority.Zip(dependent).All(p=>Math.Abs(p.First-p.Second)<1e-12),"Equivalent dependent constraint rows do not change the numerical nullspace correction");
        using(var stop=new CancellationTokenSource()){stop.Cancel();bool observed=false;try{FeasibleCurveDirection.Solve(linear,error,[0],stop.Token);}catch(OperationCanceledException){observed=true;}check(observed,"Feasibility-priority algebra observes cancellation without a playback dependency");}
        // The first coordinate closes the authored relationship. The second
        // is a free articulated direction and must retain its source pose.
        double[,] fixedCoordinate={{2,0},{4,0}};
        var preferred=FeasibleCurveDirection.Toward(fixedCoordinate,[-.25,3],[9,-.7]);
        check(Math.Abs(preferred[0]+.25)<1e-12&&Math.Abs(preferred[1]+.7)<1e-12,
            "Source-preferred correction retains the independently constrained coordinate and recovers only the free articulation");
        double[,] coupled={{1,1,0},{2,2,0}};
        var coordinated=FeasibleCurveDirection.Toward(coupled,[1,0,0],[0,0,4]);
        check(Math.Abs(coordinated[0]+coordinated[1]-1)<1e-12&&Math.Abs(coordinated[0]-.5)<1e-12&&Math.Abs(coordinated[1]-.5)<1e-12&&coordinated[2]==4,
            "A coupled source preference redistributes joint correction in the true required-row nullspace rather than weakening that relationship");
        var locked=FeasibleCurveDirection.Toward(linear,[.2,.3],[100,-100]);
        check(locked.SequenceEqual(new[]{.2,.3}),"A fully constrained source preference cannot move a fixed socket to improve posing");
        using(var stop=new CancellationTokenSource()){stop.Cancel();bool observed=false;try{FeasibleCurveDirection.Toward(coupled,[0,0,0],[0,0,0],stop.Token);}catch(OperationCanceledException){observed=true;}check(observed,"Source-preferred factorization observes cancellation before publishing a direction");}
        var fixture=RetargetV2Checks.Chain(false);var rig=fixture.ControlRigs[0];var descriptor=RigDescriptor.Prepare(fixture.Scene,rig);
        var request=new RetargetRequest(fixture,fixture.Clips[0],rig,fixture,rig,RootMotionPolicy.Preserve,false,default,null,null,null,null,null,null,null);
        var baseline=new BaselineMotionTransfer(request,descriptor,descriptor);
        float bound=PoseGeometry.Scale(fixture.Scene,rig)*RetargetAcceptance.PositionScaleFraction;
        var pose=AnimationService.Evaluate(fixture.Scene,null,0);
        var observation=new ContactPin(Guid.NewGuid(),"Independently supplied optional relationship",0,new(1,2,3),Vector3.Zero,Quaternion.Identity,false,"Fixture evidence")
        {SoftSupport=true,PlaneNormal=Vector3.UnitY,Mode=ContactMode.Slide};
        check(Math.Abs(ContactObservationObjective.Cost([observation],pose,2)-25)<1e-9,
            "A soft slide measures only its independently expected normal gap: (2/2)^2 times the existing weight25");
        check(Math.Abs(ContactObservationObjective.Cost([observation with{PlaneNormal=null}],pose,2)-87.5)<1e-9,
            "A soft fixed-point observation retains all three geometric components without being converted to a required lock");
        check(ContactObservationObjective.Cost([observation with{RequiredSupport=true},observation with{SoftSupport=false}],pose,2)==0,
            "Authored/required constraints are excluded from soft-observation weights and retain their separate hard checks");
        var plan=new TargetMotionPlan(descriptor,rig,baseline,new([],MotionContactCoverage.Unknown,[]),null,new(),new(60),2,false,false,[])
        {PositionBound=bound,BaselineSourceRate=60,AuthenticSourceSamples=2};
        TargetMotionPlan Pin(ContactPin pin)=>plan with{Graph=new([new(Guid.NewGuid(),Guid.Empty,0,rig.SkeletonFingerprint,[pin]){EndFrame=2,EndExclusive=true}],MotionContactCoverage.Required,[])};
        ContactPin At(Vector3 displacement)=>new(Guid.NewGuid(),"Independent positional pin",0,Vector3.Zero,-displacement,Quaternion.Identity,false,"Fixed supplied anchor");
        var diagonal=At(new Vector3(.8f*bound));
        double diagonalViolation=TargetMotionConstraints.MaximumNormalizedViolation(Pin(diagonal),0,pose);
        check(Math.Abs(diagonalViolation-Math.Sqrt(3)*.8)<.000001&&diagonalViolation>1,"Three .8-bound position components violate the frozen vector bound by independently expected sqrt(3)*.8");
        check(Math.Abs(TargetMotionConstraints.MaximumNormalizedViolation(Pin(At(new(.6f*bound,0,0))),0,pose)-.6)<.000001,"A .6-bound residual remains within the full frozen bound even outside the preferred half-bound convergence target");
        var soft=diagonal with{SoftSupport=true};
        check(TargetMotionConstraints.MaximumNormalizedViolation(Pin(soft),0,pose)==0&&TargetMotionConstraints.MaximumNormalizedViolation(Pin(soft with{RequiredSupport=true}),0,pose)>1,"Soft observations are separate from required constraints and cannot weaken required pins");
        var rotation=At(Vector3.Zero) with{HoldRotation=true,Rotation=Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.One),1.1f*RetargetAcceptance.RotationRadians)};
        check(Math.Abs(TargetMotionConstraints.MaximumNormalizedViolation(Pin(rotation),0,pose)-1.1)<.00001,"A diagonal constrained rotation uses total quaternion angle rather than individually passing axis components");
        var allowed=At(Vector3.Zero) with{LocalPoint=new(-.8f*bound,-.8f*bound,0),Position=new(-.8f*bound,-.8f*bound,0),AllowedSurface=new(Vector3.UnitY,0,new(Vector3.UnitX,0))};
        check(Math.Abs(TargetMotionConstraints.MaximumNormalizedViolation(Pin(allowed),0,pose)-Math.Sqrt(2)*.8)<.000001,"An exact fixed anchor outside the allowed top still fails the independent diagonal allowed-region norm");

        // The chain's real triangle rests at y=1,1,1.04. A proxy containing only
        // its high vertex clears y=1.02 while the actual low vertices penetrate.
        var proxy=descriptor.SkinPoints.Single(p=>p.Vertex==2);var floor=new ContactClearance(Vector3.UnitY,1.02f,[proxy]);
        var ground=new GroundContactReport("Independent fixture surface",1.02f,1.02f,0,[],0){ExplicitSurface=true};
        var collision=plan with{Clearance=floor,Grounding=ground};
        float depthBound=descriptor.CharacteristicLength*RetargetAcceptance.PenetrationLegFraction;
        check(floor.Surface.Penetration(ContactGeometry.Point(proxy,pose))==0&&Math.Abs(TargetMotionConstraints.MaximumNormalizedViolation(collision,0,pose)-.02/depthBound)<.003,"Full retained skin exposes the independent .02-unit penetration omitted by a clear sparse proxy");
        // GeometryPositions is the prepared evaluator's actual bind-space morph
        // array. Lower only a vertex absent from the sparse collision proxy.
        var morph=fixture.Scene.Meshes.Select(m=>m.Vertices.Select(v=>v.Position).ToArray()).ToArray();
        morph[0][0]-=new Vector3(0,.03f,0);
        var changed=pose with{GeometryPositions=morph};
        check(Math.Abs(TargetMotionConstraints.MaximumNormalizedViolation(collision,0,changed)-.05/depthBound)<.003,"Full-skin feasibility reads evaluated geometry layers instead of only stored rest positions");
        var hidden=morph.Select(m=>m.ToArray()).ToArray();hidden[0][1]-=new Vector3(0,.01f,0);
        var worse=pose with{GeometryPositions=hidden};
        check(Math.Abs(TargetMotionConstraints.MaximumNormalizedViolation(collision,0,worse)-TargetMotionConstraints.MaximumNormalizedViolation(collision,0,changed))<.000001&&
            !TargetMotionConstraints.PreservesRequiredMeasurements(collision,0,changed,worse),
            "A worsened second skin penetration is rejected even when the independently deeper first vertex keeps the pose maximum unchanged");
        check(TargetMotionConstraints.PreservesRequiredMeasurements(collision,0,changed,pose)&&TargetMotionConstraints.PreservesRequiredMeasurements(collision,0,pose,pose),
            "Optional recovery retains unchanged constraints and independently improving clearance without relaxing the frozen bound");
        var supported=plan with{Clearance=new(Vector3.UnitY,0,[]),Grounding=ground with{TargetFloor=0,BodySupportedThroughout=true},BodySupportPoints=[new(new(0,.8f*bound,0),Vector4.Zero,new(1,0,0,0))]};
        check(Math.Abs(TargetMotionConstraints.MaximumNormalizedViolation(supported,0,pose)-.8)<.000001,"Required body support retains its independent separation bound");
        var cornerBody=supported with{Clearance=supported.Clearance! with{Edge=new(Vector3.UnitX,0)},BodySupportPoints=[new(new(-.8f*bound,.8f*bound,0),Vector4.Zero,new(1,0,0,0))]};
        check(Math.Abs(TargetMotionConstraints.MaximumNormalizedViolation(cornerBody,0,pose)-Math.Sqrt(2)*.8)<.000001,"Bounded ledge body support uses complete geometric separation rather than independent axis tests");
        check(TargetMotionConstraints.MaximumNormalizedViolation(plan,0,pose)==0,"An empty constraint set reports no violation without implying contact coverage or Apply readiness");

        // The tip triangle has y=1,1,1.04. All three points initially penetrate
        // y=1.06, but they are not simultaneous equality contacts. An independent
        // one-degree base turn clears them while preserving the actor pin. Once
        // the high point clears, the next Jacobian must release that inequality
        // rather than flattening the entire triangle onto the plane.
        var slab=collision with{Clearance=new(Vector3.UnitY,1.06f,descriptor.SkinPoints),Grounding=ground with{TargetFloor=1.06f},Graph=Pin(At(new(.6f*bound,0,0))).Graph};
        var feasibleLocal=pose.Local.ToArray();feasibleLocal[1]=feasibleLocal[1] with{Rotation=new(0,0,1)};
        var feasiblePose=AnimationService.Evaluate(fixture.Scene,null,0,feasibleLocal.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
        check(descriptor.SkinPoints.Min(p=>ContactGeometry.Point(p,feasiblePose).Y)>1.069f&&ContactGeometry.Residual(slab.Pins(0)[0],feasiblePose).Length()<=bound,
            "An independently supplied one-degree pose proves the non-coplanar skin can clear y=1.06 without moving the actor pin");
        var stationary=fixture.Scene.Bones.Select(b=>new BoneTrack(b.Name,Enumerable.Range(0,9).Select(ch=>(Curve?)new Curve([new(0,b.Rest.Channels()[ch]),new(1,b.Rest.Channels()[ch])])).ToArray())).ToArray();
        var slabFit=NativeCurveProjection.Refine(slab,stationary,default,50000,maximumSweeps:1,preferred:stationary);
        var slabClip=new AnimationClip(Guid.NewGuid(),"Original inequality-release regression",2,new(60),false,slabFit.Tracks);
        var slabPoses=new[]{0d,.125,.375,.5,.875,1}.Select(f=>AnimationService.Evaluate(fixture.Scene,slabClip,f)).ToArray();
        check(slabPoses.All(p=>descriptor.SkinPoints.All(v=>ContactGeometry.Point(v,p).Y>=1.06f-depthBound)),
            "One connected native window releases cleared skin inequalities and retains clearance at independent fractional samples");
        check(slabPoses.All(p=>Enumerable.Range(0,fixture.Scene.Bones.Length).All(b=>PoseGeometry.RotationDifference(PoseGeometry.Rotation(p.World[b]),PoseGeometry.Rotation(pose.World[b]))<5))
            &&slabPoses.All(p=>ContactGeometry.Residual(slab.Pins(0)[0],p).Length()<=bound),
            "Clearing non-coplanar skin preserves the small source-relative pose and the independently fixed actor, without flattening the body");

        // The immovable actor pin intentionally has a valid .6-bound residual.
        // It cannot reach the preferred .5 target, and the sparse high vertex
        // already clears the floor. Projection therefore stalls before any
        // preferred-convergence refinement. Actual low skin must be added at
        // that stall and raised by existing articulated joints.
        var staticClip=fixture.Clips[0] with{Tracks=[],SampleCount=2};
        var staticSource=fixture with{Clips=[staticClip]};
        var staticRequest=request with{Source=staticSource,Clip=staticClip,Target=staticSource};
        var stalled=collision with
        {
            Baseline=new BaselineMotionTransfer(staticRequest,descriptor,descriptor),
            Graph=Pin(At(new(.6f*bound,0,0))).Graph
        };
        string rigBefore=AnimationControls.DeformChecksum(fixture.Scene);
        var solve=TemporalRetargetSolver.Solve(stalled);
        check(solve.Value is not null,"A sparse projection stall with omitted skin recovers through actual-geometry refinement: "+string.Join("; ",solve.Diagnostics.Select(d=>d.Message)));
        if(solve.Value is {} solved)
        {
            check(solved.Receipt.CollisionStallRefinements>0&&solved.EffectiveClearance!.Points.Any(p=>p.Mesh==0&&p.Vertex==0),"The completed solve records a stall-triggered addition of the independently known low mesh vertex");
            var actual=solved.Poses.Select(local=>AnimationService.Evaluate(fixture.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t))).ToArray();
            check(actual.All(p=>descriptor.SkinPoints.All(v=>ContactGeometry.Point(v,p).Y>=1.02f-depthBound))
                &&actual.All(p=>Math.Abs(ContactGeometry.Residual(stalled.Pins(0)[0],p).Length()/bound-.6)<.001)
                &&actual.All(p=>TargetMotionConstraints.MaximumNormalizedViolation(stalled,0,p)<=1),
                "Every solved sample clears the independently specified floor while retaining the valid .6-bound actor pin under unchanged bounds");
            check(AnimationControls.DeformChecksum(fixture.Scene)==rigBefore,"Refining collision constraints leaves native bones, rest transforms and skin weights unchanged");
        }
    }
}
