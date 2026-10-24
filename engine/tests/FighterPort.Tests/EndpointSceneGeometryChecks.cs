using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using FighterPort.Core;

// Original Cartesian material triangles. The expected coverage and below-floor
// witnesses are independent of the detector/quadrature and optimizer output.
internal static class EndpointSceneGeometryChecks
{
    sealed record Fixture(FighterProject Project,ControlRigSetup Rig);
    sealed record Prepared(Fixture Source,Fixture Target,Transform[] Input,EvaluatedPose Authentic,
        RigDescriptor TargetRig,SceneBodyFitting Fitting,Func<EvaluatedPose,NativePoseDifferentials?,ArticulatedResiduals>? Objective);

    public static void Run(Action<bool,string> check)
    {
        BodyBranches(check);
        Footprint(check);
        foreach(int count in new[]{3,63,4096})
        {
            var distances=Enumerable.Range(0,count).Select(i=>i==0?0d:1d).ToArray();
            var temperature=SceneBodyFitting.EndpointTemperature(2,2,count);
            var accurate=SceneBodyFitting.MaterialMinimum(distances,temperature);
            double old=temperature*Math.Log(count/(1+(count-1)*Math.Exp(-1/temperature)));
            check(old>.01&&accurate.Value>=0&&accurate.Value<1e-30,
                $"An independently known zero-distance support region loses the old log-count offset {old:G9} across {count} material samples while distant original geometry is retained");
            var scaledMinimum=SceneBodyFitting.MaterialMinimum(distances.Select(v=>v*1000).ToArray(),SceneBodyFitting.EndpointTemperature(2000,2000,count));
            check(Math.Abs(scaledMinimum.Value/1000-accurate.Value)<1e-12&&accurate.Weights.Sum()>1-1e-12,
                "Full-material weighted distance commutes with file-unit scaling and preserves translation-gradient mass");
        }
        double[] probeField=[0,.0015,.006],slopes=[-.3,.7,1.2];const double step=1e-8;
        var mean=SceneBodyFitting.MaterialMinimum(probeField,.01);
        double plus=SceneBodyFitting.MaterialMinimum(probeField.Select((v,i)=>v+step*slopes[i]).ToArray(),.01).Value;
        double minus=SceneBodyFitting.MaterialMinimum(probeField.Select((v,i)=>v-step*slopes[i]).ToArray(),.01).Value;
        check(Math.Abs((plus-minus)/(2*step)-mean.Weights.Zip(slopes).Sum(p=>p.First*p.Second))<1e-8,
            "Independent scalar finite differences verify the material-distance probability derivative, including unequal near-surface distances and motion directions");
        var source=Make("source-opaque",.5f);var target=Make("target-opaque",-.2f);
        string immutable=RetargetDependencies.Hash(new{source,target});var p=Prepare(source,target);
        var coverage=p.Fitting.Receipt().EndpointGeometry!.Single();
        check(p.Objective is not null&&p.Fitting.Enabled&&coverage.SourceEndpoint==5&&coverage.TargetEndpoint==5&&
            coverage.SourceVertices==3&&coverage.TargetVertices==3&&coverage.SourceWeightedBones==1&&coverage.TargetWeightedBones==1&&
            !source.Project.Scene.Meshes.SelectMany(m=>m.Vertices).Any(v=>Enumerable.Range(0,4).Any(s=>v.Weights[s]>0&&(int)v.Joints[s]==5)),
            "A reviewed endpoint with no direct native skin still includes all three original child-weighted material vertices in both source and target");
        double initial=LastCost(p.Objective!,Pose(target,p.Input));var raised=p.Input.ToArray();raised[1]=raised[1] with{Translation=raised[1].Translation+new Vector3(0,.3f,0)};
        var before=Pose(target,p.Input);var after=Pose(target,raised);
        check(Math.Abs(Min(target,before)+.2f)<.000001f&&Math.Abs(Min(target,after)-.1f)<.000001f&&
            initial>0&&LastCost(p.Objective!,after)==0,
            "Independent child-only geometry starts at Y=-.2 and a known +.3 native body move clears it to Y=.1; the prepared endpoint objective observes both poses without running a fit");
        check(p.Fitting.Receipt() is{PreparedObjectives:1,Evaluations:0,AppliedProposals:0,FullSkinClearanceEvaluations:0}&&
            immutable==RetargetDependencies.Hash(new{source,target}),
            "Freezing and evaluating a spatial objective performs no pose solve, published-pose receipt or source/target mutation");
        var differential=new NativePoseDifferentials(p.TargetRig,before,1,[]);
        // A binary translation step stays exact through this fixture's native
        // float rest/body additions. A decimal .001 step quantizes those FK
        // additions before the double residual probe can measure a derivative.
        var residual=p.Objective!(before,differential);const float h=1f/256;
        var up=p.Input.ToArray();var down=p.Input.ToArray();float length=p.TargetRig.CharacteristicLength;
        up[1]=up[1] with{Translation=up[1].Translation+Vector3.UnitY*h*length};down[1]=down[1] with{Translation=down[1].Translation-Vector3.UnitY*h*length};
        double derivative=(p.Objective!(Pose(target,up),null).Values.Last()-p.Objective!(Pose(target,down),null).Values.Last())/(2*h);
        check(Math.Abs(residual.Jacobian[residual.Values.Length-1,1]-derivative)<.00003&&residual.Jacobian.GetLength(1)==3,
            "The full child-only active material vertex has the independently sampled native world-body derivative using every violating original material vertex without choosing one arbitrary tie");
        // Coplanar points have different angular lever arms. A hard first
        // minimum is not the derivative of a permutation-symmetric aggregate.
        var angular=new NativePoseDifferentials(p.TargetRig,before,1,[5]);var tied=p.Objective!(before,angular);
        const float radians=1f/1024;var clockwise=p.Input.ToArray();var anticlockwise=p.Input.ToArray();
        clockwise[5]=clockwise[5] with{Rotation=clockwise[5].Rotation+new Vector3(0,0,radians*180/MathF.PI)};
        anticlockwise[5]=anticlockwise[5] with{Rotation=anticlockwise[5].Rotation-new Vector3(0,0,radians*180/MathF.PI)};
        double angularProbe=(p.Objective!(Pose(target,clockwise),null).Values.Last()-p.Objective!(Pose(target,anticlockwise),null).Values.Last())/(2*radians);
        // Endpoint pivot X=1.5 and material X=(-.5,.5,0) give the
        // independent radian height levers (-2,-1,-1.5). Equal hinges have
        // equal L4 coefficients 3^(-3/4), and the endpoint residual scale is .5.
        double cartesianAngular=2.25/Math.Pow(3,.75);
        check(Math.Abs(tied.Jacobian[tied.Values.Length-1,5]-cartesianAngular)<1e-12&&
            Math.Abs(tied.Jacobian[tied.Values.Length-1,5]-angularProbe)<.0002,
            "All equal-height original vertices contribute their independent Cartesian angular lever arms exactly and agree with two-sided native FK instead of a first-vertex minimum gradient");
        var reordered=Prepare(source,MaterialPermutation(target));var reorderedPose=Pose(reordered.Target,reordered.Input);
        var reorderedResidual=reordered.Objective!(reorderedPose,new NativePoseDifferentials(reordered.TargetRig,reorderedPose,1,[5]));
        check(Math.Abs(reorderedResidual.Values.Last()-tied.Values.Last())<.000002&&
            Enumerable.Range(0,6).All(v=>Math.Abs(reorderedResidual.Jacobian[reorderedResidual.Values.Length-1,v]-tied.Jacobian[tied.Values.Length-1,v])<.000002),
            "Permuting the triangle's material storage order preserves the tied endpoint residual and every analytic native column without an active-vertex tie branch");
        var converted=Prepare(Convert(source,100,90),Convert(target,100,90));var convertedPose=Pose(converted.Target,converted.Input);
        var convertedResidual=converted.Objective!(convertedPose,new NativePoseDifferentials(converted.TargetRig,convertedPose,1,[5]));
        check(Math.Abs(convertedResidual.Values.Last()-tied.Values.Last())<.00001&&
            Math.Abs(convertedResidual.Jacobian[convertedResidual.Values.Length-1,5]-tied.Jacobian[tied.Values.Length-1,5])<.00002&&
            Math.Abs(convertedResidual.Jacobian[convertedResidual.Values.Length-1,0]+tied.Jacobian[tied.Values.Length-1,1])<.00002,
            "A hundred-unit independently quarter-turned Cartesian rig preserves the full tied material aggregate, native angular derivative and rotated physical body-translation derivative");
        // The thousand-unit fixture makes epsilon=1, half epsilon=.5 and
        // all corresponding Cartesian heights exactly representable in float.
        // At t=1/2 the prescribed C2 bridge has value11/32 and slope23/16.
        var transitionSource=Convert(source,1000,0);
        var transitionHalf=Prepare(transitionSource,Convert(Make("transition-half",.0495f),1000,0));
        var transitionFull=Prepare(transitionSource,Convert(Make("transition-full",.049f),1000,0));
        var transitionZero=Prepare(transitionSource,Convert(Make("transition-zero",.05f),1000,0));
        var transitionAbove=Prepare(transitionSource,Convert(Make("transition-above",.0505f),1000,0));
        ArticulatedResiduals Transition(Prepared f)
        {
            var pose=Pose(f.Target,f.Input);return f.Objective!(pose,new NativePoseDifferentials(f.TargetRig,pose,1,[]));
        }
        var halfResidual=Transition(transitionHalf);var fullResidual=Transition(transitionFull);
        var zeroResidual=Transition(transitionZero);var aboveResidual=Transition(transitionAbove);
        double materialScale=.5*Math.Pow(3,.25);
        check(transitionHalf.Fitting.Receipt().EndpointHingeTransitionWidth==1&&
            Math.Abs(Min(transitionHalf.Target,Pose(transitionHalf.Target,transitionHalf.Input))-49.5)<1e-12&&
            Math.Abs(halfResidual.Values.Last()-materialScale*(11d/32)/1000)<1e-12&&
            Math.Abs(halfResidual.Jacobian[halfResidual.Values.Length-1,1]+materialScale*(23d/16))<1e-12,
            "The globally fixed C2 soft hinge reproduces independent exact half-width value11/32 and slope23/16 at epsilon=1 native unit without changing the actual49.5 skin height");
        check(Math.Abs(fullResidual.Values.Last()-materialScale/1000)<1e-12&&
            Math.Abs(fullResidual.Jacobian[fullResidual.Values.Length-1,1]+materialScale)<1e-12&&
            zeroResidual.Values.Last()==0&&aboveResidual.Values.Last()==0&&
            Enumerable.Range(0,3).All(v=>zeroResidual.Jacobian[zeroResidual.Values.Length-1,v]==0&&aboveResidual.Jacobian[aboveResidual.Values.Length-1,v]==0),
            "The C2 bridge is exactly the original hinge at its upper boundary and exactly zero with zero columns at and above clearance; deeper and isolated-orphan expectations remain unchanged");
        const float signProbe=1f/1048576;
        var nearSource=Make("floor-sign-source",0);var barelyBelow=Prepare(nearSource,Make("floor-sign-negative",-signProbe));
        var nearerBelow=Prepare(nearSource,Make("floor-sign-half-negative",-signProbe/2));
        var barelyAbove=Prepare(nearSource,Make("floor-sign-positive",signProbe));
        var belowResidual=Transition(barelyBelow);var nearerResidual=Transition(nearerBelow);var positiveResidual=Transition(barelyAbove);
        double belowColumn=Math.Abs(belowResidual.Jacobian[belowResidual.Values.Length-1,1]);
        double nearerColumn=Math.Abs(nearerResidual.Jacobian[nearerResidual.Values.Length-1,1]);
        check(positiveResidual.Values.Last()==0&&positiveResidual.Jacobian[positiveResidual.Values.Length-1,1]==0&&
            belowColumn>0&&nearerColumn>0&&nearerColumn<3e-6&&nearerColumn<belowColumn*.26&&
            belowResidual.Values.Last()>nearerResidual.Values.Last()&&nearerResidual.Values.Last()>0,
            "Independent binary sub-micron floor-sign perturbations have vanishing native GN columns with quadratic slope decay instead of a constant negative-side column jump");
        barelyBelow.Fitting.RecordPreparedPose(barelyBelow.Authentic,barelyBelow.Input,barelyBelow.Input);
        check(Math.Abs(Min(barelyBelow.Target,Pose(barelyBelow.Target,barelyBelow.Input))+signProbe)<1e-12&&
            Math.Abs(barelyBelow.Fitting.Receipt().MaximumInitialPenetration-signProbe)<1e-12&&
            Math.Abs(barelyBelow.Fitting.Receipt().MaximumFinalPenetration-signProbe)<1e-12,
            "Soft hinge conditioning leaves the independent actual negative floor distance and full-skin overlap receipt unchanged rather than introducing an acceptance allowance");
        var oldDistantGap=Prepare(source,Make("positive-standoff-target",.2f));
        var positiveSource=Make("near-standoff-source",.02f);
        var positiveGap=Prepare(positiveSource,Make("near-standoff-target",.03f));var positivePose=Pose(positiveGap.Target,positiveGap.Input);
        var positiveRows=positiveGap.Objective!(positivePose,new NativePoseDifferentials(positiveGap.TargetRig,positivePose,1,[]));
        var positiveGeometry=positiveGap.Fitting.Receipt().EndpointGeometry!.Single();double radius=2d/3;
        check(positiveRows.Values.Length==2&&Math.Abs(positiveGeometry.SourceRestRadius!.Value-radius)<1e-12&&
            Math.Abs(positiveGeometry.TargetRestRadius!.Value-radius)<1e-12&&
            positiveGeometry.SourceEffectiveRadius==positiveGeometry.SourceRestRadius&&positiveGeometry.TargetEffectiveRadius==positiveGeometry.TargetRestRadius,
            "The complete Cartesian triangle has independently known centroid/max-extent radius2/3 in each native rig; a standoff row precedes the unchanged final one-sided row");
        var matching=positiveGap.Input.ToArray();matching[1]=matching[1] with{Translation=matching[1].Translation+new Vector3(0,-.01f,0)};
        var onFloor=positiveGap.Input.ToArray();onFloor[1]=onFloor[1] with{Translation=onFloor[1].Translation-new Vector3(0,.03f,0)};
        check(Math.Abs(Min(positiveGap.Target,Pose(positiveGap.Target,matching))-.02f)<.000001&&
            Math.Abs(positiveGap.Objective!(Pose(positiveGap.Target,matching),null).Values[0])<.000001&&
            Math.Abs(positiveGap.Objective!(Pose(positiveGap.Target,onFloor),null).Values[0])>.01&&positiveRows.Values.Last()==0,
            "A known -.01 body move reproduces the authentic scene-near positiveY=.02 standoff, while moving that region onto the floor is penalized even though the independent lower-clearance row is already zero");
        double affinity=.05;double expectedStandoffColumn=Math.Sqrt(.25*Math.Exp(-.5*Math.Pow((double).02f/affinity,2)))/radius;
        var farSource=Prepare(Make("far-standoff-source",3),positiveGap.Target);var farPose=Pose(farSource.Target,farSource.Input);
        var farRows=farSource.Objective!(farPose,new NativePoseDifferentials(farSource.TargetRig,farPose,1,[]));
        check(Math.Abs(positiveRows.Jacobian[0,1]-expectedStandoffColumn)<1e-12&&Math.Abs(farRows.Jacobian[0,1])<expectedStandoffColumn*.02,
            "The standoff gradient has the independent local-radius normalized value and a far-away authentic source region receives weak scene confidence instead of an automatic floor-plant interpretation");
        var convertedGap=Prepare(Convert(positiveSource,100,90),Convert(positiveGap.Target,1,-90));var convertedGapPose=Pose(convertedGap.Target,convertedGap.Input);
        var convertedGapRows=convertedGap.Objective!(convertedGapPose,new NativePoseDifferentials(convertedGap.TargetRig,convertedGapPose,1,[]));
        check(Math.Abs(convertedGapRows.Values[0]-positiveRows.Values[0])<.000002&&
            Math.Abs(convertedGapRows.Jacobian[0,0]-positiveRows.Jacobian[0,1])<.000002&&
            Math.Abs(convertedGap.Fitting.Receipt().EndpointGeometry!.Single().SourceRestRadius!.Value/100-radius)<.000002,
            "Source centimetres and independently quarter-turned native source/target axes preserve the same actual standoff and rotated world-body derivative with radius metadata in their own native units");
        var distantPose=Pose(oldDistantGap.Target,oldDistantGap.Input);
        var distantRows=oldDistantGap.Objective!(distantPose,new NativePoseDifferentials(oldDistantGap.TargetRig,distantPose,1,[]));
        double distantBound=.5/radius*Math.Exp(-25);
        check(Math.Abs(Min(oldDistantGap.Source,oldDistantGap.Authentic)-.5f)<1e-12&&
            oldDistantGap.Fitting.Receipt().EndpointGeometry!.Single().SourceAffinityRadius==affinity&&
            Math.Abs(distantRows.Jacobian[0,1])<=distantBound*(1+1e-12),
            "The retained old source-gap.5/target-gap.2 fixture is now correctly a distant negative: equality influence is suppressed by the global source band, without changing either authentic geometry");
        var broadSource=Volumetric(Make("large-volume-distant-source",.5f),15,.5f);
        var broadCases=new[]{Volumetric(Make("small-volume-free-target",.03f),.2f,.03f),Volumetric(Make("large-volume-free-target",.03f),2,.03f)}
            .Select(t=>Prepare(broadSource,t)).ToArray();
        check(broadCases.All(f=>
        {
            var geometry=f.Fitting.Receipt().EndpointGeometry!.Single();var pose=Pose(f.Target,f.Input);
            var actual=f.Objective!(pose,new NativePoseDifferentials(f.TargetRig,pose,1,[]));
            double bound=.5/geometry.TargetEffectiveRadius!.Value*Math.Exp(-25);
            return geometry.SourceRestRadius>10&&geometry.SourceAffinityRadius==affinity&&Min(f.Source,f.Authentic)>=.5f&&
                Math.Abs(actual.Jacobian[0,1])<=bound*(1+1e-10)&&Math.Abs(actual.Jacobian[0,1])<1e-9;
        })&&broadCases[1].Fitting.Receipt().EndpointGeometry!.Single().TargetRestRadius>9*broadCases[0].Fitting.Receipt().EndpointGeometry!.Single().TargetRestRadius,
            "An actual closed tetrahedral endpoint with radius above10 and source clearance.5 receives nearly zero scene equality influence for two tenfold-different target body volumes; broad free geometry is not mistaken for a floor attachment");
        var sourceDefect=Prepare(Make("defect-standoff-source",-.2f),positiveGap.Target);var defectPose=sourceDefect.Input.ToArray();
        defectPose[1]=defectPose[1] with{Translation=defectPose[1].Translation-new Vector3(0,.03f,0)};
        check(Min(sourceDefect.Source,sourceDefect.Authentic)<-.19f&&Math.Abs(Min(sourceDefect.Target,Pose(sourceDefect.Target,defectPose)))<.000001&&
            Math.Abs(sourceDefect.Objective!(Pose(sourceDefect.Target,defectPose),null).Values[0])<.000001,
            "An independently penetrating authentic source is recorded as a defect and gives zero projected standoff, rather than copying its negative floor distance into the target goal");
        var historical=JsonNode.Parse(JsonSerializer.Serialize(positiveGap.Fitting.Receipt(),ProjectStore.Json))!.AsObject();
        historical.Remove("EndpointDistanceVersion");historical.Remove("EndpointDistancePolicy");historical.Remove("EndpointSourceAffinityVersion");
        foreach(var geometry in historical["EndpointGeometry"]!.AsArray())foreach(string field in new[]{"SourceRestRadius","TargetRestRadius","SourceEffectiveRadius","TargetEffectiveRadius","SourceAffinityRadius","SourceSoftMinimumBiasBound","TargetSoftMinimumBiasBound"})geometry!.AsObject().Remove(field);
        string historicalJson=historical.ToJsonString(ProjectStore.Json);var restored=JsonSerializer.Deserialize<SceneBodyFitReceipt>(historicalJson,ProjectStore.Json)!;
        check(restored.EndpointDistanceVersion is null&&restored.EndpointDistancePolicy is null&&restored.EndpointSourceAffinityVersion is null&&
            restored.EndpointGeometry!.All(g=>g.SourceRestRadius is null&&g.TargetRestRadius is null&&g.SourceEffectiveRadius is null&&g.TargetEffectiveRadius is null&&g.SourceAffinityRadius is null&&g.SourceSoftMinimumBiasBound is null&&g.TargetSoftMinimumBiasBound is null)&&
            JsonSerializer.Serialize(restored,ProjectStore.Json)==historicalJson,
            "Historical scene receipts with no standoff/radius metadata remain unknown and reproduce their exact JSON without fabricating radius evidence or rewriting source-proof bytes");
        p.Fitting.RecordPreparedPose(p.Authentic,p.Input,raised);var receipt=p.Fitting.Receipt();
        check(receipt is{Evaluations:1,AppliedProposals:1,TranslationProposals:1,FullSkinClearanceEvaluations:1,ClearanceVertices:3}&&
            Math.Abs(receipt.MaximumInitialPenetration-.2f)<.000001f&&receipt.MaximumFinalPenetration==0&&Math.Abs(receipt.MaximumCorrection-.3f)<.000002f&&
            raised[0]==p.Input[0]&&raised.Select((v,i)=>v.Rotation==p.Input[i].Rotation&&v.Scale==p.Input[i].Scale).All(v=>v),
            "Recording a stored trajectory collocation measures actual initial/final full skin and the real body translation once, preserving actor, native rotations/scales and all input poses");

        var blended=Prepare(Make("mixed-source",.5f,mixed:true),Make("mixed-target",-.2f,mixed:true));
        var mixed=blended.Fitting.Receipt().EndpointGeometry!.Single();
        check(mixed.SourceVertices==3&&mixed.TargetVertices==3&&mixed.SourceWeightedBones==2&&mixed.TargetWeightedBones==2&&
            Math.Abs(mixed.SourceInfluenceArea-.5)<.000001&&Math.Abs(mixed.TargetInfluenceArea-.5)<.000001,
            "Repeated .125+.125 endpoint slots plus a .75 descendant slot retain three material vertices and exactly one half-unit triangle area, without double-counting native influence regions");
        check(Cost(blended.Objective!,Pose(blended.Target,blended.Input))>0&&blended.Target.Project.Scene.Meshes[0].Vertices.All(v=>v.Weights==new Vector4(.125f,.125f,.75f,0)),
            "The prepared full descendant screen evaluates original mixed native weights without normalization, replacement or duplicate motion");
        var separate=Prepare(Make("separate-source",.5f,copies:2),Make("separate-target",-.2f,copies:2));
        var material=separate.Fitting.Receipt().EndpointGeometry!.Single();
        check(material.SourceVertices==6&&material.TargetVertices==6&&Math.Abs(material.SourceInfluenceArea-1)<.000001&&Math.Abs(material.TargetInfluenceArea-1)<.000001,
            "Two meshes with geometrically coincident triangles remain six distinct original material vertices rather than collapsing by position");

        double single=p.Objective!(before,null).Values.Last(),doubled=separate.Objective!(Pose(separate.Target,separate.Input),null).Values.Last();
        check(Math.Abs(doubled/single-Math.Pow(2,.25))<.000002,
            "Duplicating the coplanar material samples has the explicitly declared L4 density response, rather than a false tessellation-invariance or point-count-dilution claim");
        var orphan=Prepare(Make("zero-area-source",.5f,orphan:.5f),Make("zero-area-target",.2f,orphan:-.4f));
        var zeros=orphan.Fitting.Receipt().EndpointGeometry!.Single();
        check(zeros.SourceVertices==4&&zeros.TargetVertices==4&&zeros.SourceZeroAreaVertices==1&&zeros.TargetZeroAreaVertices==1&&
            Math.Abs(zeros.SourceInfluenceArea-.5)<.000001&&Math.Abs(zeros.TargetInfluenceArea-.5)<.000001&&
            Min(orphan.Target,Pose(orphan.Target,orphan.Input))<-.39f&&Cost(orphan.Objective!,Pose(orphan.Target,orphan.Input))>0,
            "An original unreferenced zero-area vertex remains visible to full endpoint clearance screening while contributing no invented source/target area mass");

        check(Math.Abs(orphan.Objective!(Pose(orphan.Target,orphan.Input),null).Values.Last()-.225)<.000002,
            "One isolated zero-area original vertex retains its full independently known .45-unit hinge and .225 dimensionless residual, undiluted by the three safe triangle vertices");
        var hidden=Prepare(source,target,visible:_=>false);
        check(hidden.Objective is null&&hidden.Fitting.Receipt().PreparedObjectives==0,
            "An explicitly hidden authentic source subtree supplies no endpoint objective; cached rest visibility does not fabricate source evidence");
        var morphed=Prepare(source,target,morphHeight:-.1f);
        double normalClearance=p.Objective!(before,null).Values.Last();double morphClearance=morphed.Objective!(Pose(target,morphed.Input),null).Values.Last();
        check(morphClearance>0&&morphClearance<normalClearance,
            "Actual authentic GeometryPositions change the full source descendant clearance independently of its preserved rest positions");
        var permuted=Prepare(Permute(source),Permute(target));var mapped=permuted.Fitting.Receipt().EndpointGeometry!.Single();
        check(mapped.SourceEndpoint==6&&mapped.TargetEndpoint==6&&mapped.SourceVertices==3&&mapped.TargetVertices==3&&
            Math.Abs(LastCost(permuted.Objective!,Pose(permuted.Target,permuted.Input))-initial)<.000001,
            "Unrelated native names and a valid branch storage permutation preserve equivalent supplied-binding full endpoint material coverage and numerical residual");
        var strict=Prepare(source,target,strict:true);
        check(strict.Objective is null&&strict.Fitting.Receipt().VolumePolicy==BodyVolumePolicy.RequireVertexClearance,
            "Strict translation-only volume fitting is an explicit separate capability and is not silently routed through the prepared articulated objective");
        var preflight=Prepare(source,target);preflight.Fitting.ValidateEvaluationBudget(1,0);var bounded=preflight.Fitting.Receipt();bool rejected=false;
        try{preflight.Fitting.ValidateEvaluationBudget(int.MaxValue,int.MaxValue);}catch(NotSupportedException){rejected=true;}
        check(bounded.EstimatedSensorWork is >0&&bounded.EstimatedSensorWork<bounded.SensorWorkBudget&&
            bounded.Articulated?.EstimatedResidualWork is >0&&bounded.Articulated?.EstimatedSkinColumns is >0&&rejected&&preflight.Fitting.Receipt().Evaluations==0,
            "Full source preparation, every target endpoint screen and active endpoint derivative columns have finite preflight work under unchanged budgets; excessive requests fail before any solve");
        bool cancelled=false;using(var cancellation=new CancellationTokenSource())
        {
            var interrupted=Prepare(source,target,token:cancellation.Token);cancellation.Cancel();
            try{interrupted.Fitting.PrepareObjective(interrupted.Authentic,interrupted.Input);}catch(OperationCanceledException){cancelled=true;}
        }
        check(cancelled&&immutable==RetargetDependencies.Hash(new{source,target}),
            "Preparing a cancelled full descendant objective stops before sampling and preserves immutable native graphs, original skin weights and geometry");
    }
    static void BodyBranches(Action<bool,string> check)
    {
        Fixture Branch(Fixture f,bool skin=true)
        {
            var scene=f.Project.Scene with{SkeletonRevision=Guid.NewGuid(),Bones=f.Project.Scene.Bones.Concat(new Bone[]{
                new("opaque-alpha",2,Transform.Identity with{Translation=new(.25f,.5f,0)}),
                new("opaque-beta",7,Transform.Identity with{Translation=new(.25f,.5f,0)})}).ToArray()};
            Vector3[] points=[new(.75f,3,0),new(1.25f,3,0),new(1,3,.5f)];
            if(skin)scene=scene with{Meshes=scene.Meshes.Append(new Mesh("Original body-region material",points.Select(p=>new Vertex(p,Vector3.UnitY,Vector2.Zero,new(8,0,0,0),new(1,0,0,0),Vector4.One)).ToArray(),[0,1,2])).ToArray()};
            var rig=f.Rig with{Id=Guid.NewGuid(),SkeletonFingerprint=AnimationControls.Fingerprint(scene),BoneRoles=f.Rig.BoneRoles.Concat(new RigMapping[]{
                new("Reviewed.Region.A",7,"Independent supplied branch binding",true),new("Reviewed.Region.B",8,"Independent supplied branch binding",true)}).ToArray()};
            return new(f.Project with{Scene=scene,ControlRigs=[rig]},rig);
        }
        var a=Branch(Make("body-source",.5f));var b=Branch(Make("body-target",-.2f));
        string immutable=RetargetDependencies.Hash(new{a,b});var p=Prepare(a,b);var selected=p.Fitting.Receipt().Articulated!.SelectedRotationBones!;
        check(selected.Contains(7)&&selected.Contains(8)&&!selected.Contains(0)&&p.Fitting.Articulation!.Variables==3+selected.Length*3,
            "Reviewed weighted non-limb native branches and their unweighted trusted ancestors participate in the same coordinated variables while actor channels stay excluded");
        var pose=Pose(b,p.Input);var differential=new NativePoseDifferentials(p.TargetRig,pose,1,selected);
        int column=3+3*Array.IndexOf(selected,8)+2;
        var point=b.Project.Scene.Meshes[1].Vertices[0];var limb=b.Project.Scene.Meshes[0].Vertices[0];
        Vector3 D(Vertex v)=>differential.Point(new(v.Position,v.Joints,v.Weights),column);
        check(Vector3.Distance(D(point),new(0,.25f,0))<1e-6f&&D(limb)==Vector3.Zero,
            "An independently known quarter-unit Cartesian lever has the real new body-branch skin derivative and zero effect on an unrelated native limb");
        var permuted=Prepare(Permute(a),Permute(b));var changed=permuted.Fitting.Receipt().Articulated!.SelectedRotationBones!;
        check(changed.Contains(8)&&changed.Contains(9)&&changed.Length==selected.Length&&
            Math.Abs(Cost(permuted.Objective!,Pose(permuted.Target,permuted.Input))-Cost(p.Objective!,pose))<1e-6,
            "Opaque renamed and valid storage-permuted branch bindings preserve selected body degrees of freedom and the same native scene objective");
        var noSkin=Prepare(Branch(Make("unweighted-source",.5f),false),Branch(Make("unweighted-target",-.2f),false));
        check(!noSkin.Fitting.Receipt().Articulated!.SelectedRotationBones!.Contains(7)&&!noSkin.Fitting.Receipt().Articulated!.SelectedRotationBones!.Contains(8),
            "Reviewed branches with no actual skin influence or weighted descendants are not added merely because a role or name exists");
        check(immutable==RetargetDependencies.Hash(new{a,b}),"Preparing coordinated body branches preserves every source and target native rest, hierarchy, original weight and animation value");
        RetargetBake Bake(Fixture x,Fixture y)=>RetargetService.Bake(x.Project,x.Project.Clips[0],x.Rig,y.Project,y.Rig,useContacts:false,
            ground:new(){ExplicitSurface=true,SourceFloor=0,TargetFloor=0},fitting:new(){AutomaticFitting=true,AutomaticContacts=false}).Value
            ??throw new InvalidOperationException("Independent body-branch temporal fixture could not be prepared.");
        var first=Bake(a,b);var convertedA=Convert(a,100,90);var convertedB=Convert(b,100,90);var convertedBake=Bake(convertedA,convertedB);
        var turn=Matrix4x4.CreateRotationZ(MathF.PI/2);double maximum=0;
        foreach(double time in new[]{0d,.137,.593,1,1.371,1.9999})
        {
            var before=AnimationService.Evaluate(b.Project.Scene,first.Clip,time);var after=AnimationService.Evaluate(convertedB.Project.Scene,convertedBake.Clip,time);
            for(int mesh=0;mesh<b.Project.Scene.Meshes.Length;mesh++)for(int v=0;v<b.Project.Scene.Meshes[mesh].Vertices.Length;v++)
            {
                var expected=Vector3.Transform(AnimationService.Skin(b.Project.Scene.Meshes[mesh].Vertices[v],before),turn);
                var actual=AnimationService.Skin(convertedB.Project.Scene.Meshes[mesh].Vertices[v],after)/100;
                maximum=Math.Max(maximum,Vector3.Distance(expected,actual));
            }
        }
        check(maximum<.00008&&first.Clip.Retarget!.Pipeline!.SceneBody!.Articulated!.SelectedRotationBones!.Contains(8)&&
            immutable==RetargetDependencies.Hash(new{a,b}),
            $"Actual whole-clip preparation with extra weighted body branches preserves the existing unit/axis full-skin bound at withheld times without source/rig mutation (max{maximum:G9})");
    }

    static void Footprint(Action<bool,string> check)
    {
        var observed=SceneBodyFitting.EndpointEdgeObservation([0,0,0],[.2,.8,1],.5);
        var units=SceneBodyFitting.EndpointEdgeObservation([0,0,0],[20,80,100],50);
        var hanging=SceneBodyFitting.EndpointEdgeObservation([-3,-4,-5],[-.2,-.1,-.3],.5);
        var overhang=SceneBodyFitting.EndpointEdgeObservation([0,0,0],[-.2,.8,1],.5);
        check(observed.Desired==.2&&Math.Abs(observed.Confidence-Math.Exp(-.08))<1e-14&&units.Desired==20&&units.Confidence==observed.Confidence,
            "Independent Cartesian edge extent and source confidence preserve file-unit covariance");
        check(hanging.Confidence<1e-7&&overhang.Desired==-.2,
            "Below-top hanging material supplies negligible top-footprint affinity and authored signed overhang is retained");
        var a=Make("opaque-edge-source",0);var b=Make("opaque-edge-target",0);
        b=b with{Project=b.Project with{Scene=b.Project.Scene with{Meshes=b.Project.Scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Position=v.Position-new Vector3(0,0,.4f)}).ToArray()}).ToArray()}}};
        var p=Prepare(a,b,edge:true);var pose=Pose(b,p.Input);var residual=p.Objective!(pose,new NativePoseDifferentials(p.TargetRig,pose,1,[]));
        var inside=p.Input.ToArray();inside[1]=inside[1] with{Translation=inside[1].Translation+new Vector3(0,0,.4f)};
        double before=residual.Values[^2],after=p.Objective!(Pose(b,inside),null).Values[^2];
        check(before<-.01&&Math.Abs(after)<1e-8&&p.Fitting.Receipt().EndpointFootprintVersion==SceneBodyFitting.EndpointFootprintVersion,
            "The actual prepared native objective observes a complete child-weighted patch outside the edge and its independently known inward correction without locking a hand or foot");
        const float h=1f/256;var plus=p.Input.ToArray();var minus=p.Input.ToArray();
        plus[1]=plus[1] with{Translation=plus[1].Translation+Vector3.UnitZ*h*p.TargetRig.CharacteristicLength};
        minus[1]=minus[1] with{Translation=minus[1].Translation-Vector3.UnitZ*h*p.TargetRig.CharacteristicLength};
        double derivative=(p.Objective!(Pose(b,plus),null).Values[^2]-p.Objective!(Pose(b,minus),null).Values[^2])/(2*h);
        check(Math.Abs(residual.Jacobian[residual.Values.Length-2,2]-derivative)<.00003,
            "The edge footprint's analytic full-skin native derivative agrees with independent two-sided Cartesian translation");
        var converted=Prepare(Convert(a,100,90),Convert(b,100,90),edge:true);
        var reordered=Prepare(Permute(a),Permute(b),edge:true);
        check(Math.Abs(converted.Objective!(Pose(converted.Target,converted.Input),null).Values[^2]-before)<1e-5&&
            Math.Abs(reordered.Objective!(Pose(reordered.Target,reordered.Input),null).Values[^2]-before)<1e-6,
            "The edge material objective commutes with supplied renamed/storage-permuted bindings and independently rotated hundred-unit geometry");
    }

    static double LastCost(Func<EvaluatedPose,NativePoseDifferentials?,ArticulatedResiduals> objective,EvaluatedPose pose)=>Math.Pow(objective(pose,null).Values.Last(),2);
    static double Cost(Func<EvaluatedPose,NativePoseDifferentials?,ArticulatedResiduals> objective,EvaluatedPose pose)=>objective(pose,null).Values.Sum(v=>v*v);
    static EvaluatedPose Pose(Fixture f,Transform[] local)=>AnimationService.Evaluate(f.Project.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
    static float Min(Fixture f,EvaluatedPose pose)=>f.Project.Scene.Meshes.SelectMany(m=>m.Vertices).Min(v=>AnimationService.Skin(v,pose).Y);
    static Prepared Prepare(Fixture source,Fixture target,Func<int,bool>? visible=null,float? morphHeight=null,bool strict=false,CancellationToken token=default,bool edge=false)
    {
        var request=new RetargetRequest(source.Project,source.Project.Clips[0],source.Rig,target.Project,target.Rig,RootMotionPolicy.Preserve,false,token,null,
            new(){ExplicitSurface=true,SourceFloor=0,TargetFloor=0,SourceEdge=edge?new(Vector3.UnitZ,0):null,TargetEdge=edge?new(Vector3.UnitZ,0):null},new(){AutomaticFitting=true,AutomaticContacts=false,
                VolumePolicy=strict?BodyVolumePolicy.RequireVertexClearance:BodyVolumePolicy.ReportOverlap},null,null,null,null);
        var a=RigDescriptor.Prepare(source.Project.Scene,source.Rig);var b=RigDescriptor.Prepare(target.Project.Scene,target.Rig);
        var baseline=new BaselineMotionTransfer(request,a,b);var authentic=AnimationService.Evaluate(source.Project.Scene,request.Clip,0);
        if(morphHeight is{} y)authentic=authentic with{GeometryPositions=source.Project.Scene.Meshes.Select(m=>m.Vertices.Select(v=>v.Position with{Y=y}).ToArray()).ToArray()};
        var input=baseline.EvaluatePose(authentic);var fitting=new SceneBodyFitting(request,a,b,baseline);
        return new(source,target,input,authentic,b,fitting,fitting.PrepareObjective(authentic,input,visible));
    }
    static Fixture Make(string label,float height,bool mixed=false,int copies=1,float? orphan=null)
    {
        Transform T(float x,float y)=>Transform.Identity with{Translation=new(x,y,0)};
        Bone[] bones=[new(label+"-native0",-1,Transform.Identity),new(label+"-native1",0,T(0,1)),new(label+"-native2",1,T(0,1)),
            new(label+"-native3",2,T(.5f,0)),new(label+"-native4",3,T(.5f,0)),new(label+"-native5",4,T(.5f,0)),new(label+"-native6",5,T(.1f,0))];
        Vector3[] points=[new(-.5f,height,0),new(.5f,height,0),new(0,height,1)];
        if(orphan is{} y)points=points.Append(new(0,y,0)).ToArray();
        var vertices=points.Select(p=>new Vertex(p,Vector3.UnitY,Vector2.Zero,mixed?new(5,5,6,0):new(6,0,0,0),
            mixed?new(.125f,.125f,.75f,0):new(1,0,0,0),Vector4.One)).ToArray();
        var scene=new Scene(label,bones,Enumerable.Range(0,copies).Select(i=>new Mesh("Original material "+i,vertices.ToArray(),[0,1,2])).ToArray());
        string[] roles=["RootMotion","Hips","Chest","Opaque.Start","Opaque.Middle","Opaque.End"];
        var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[]){BodyBone=1,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),
            Coordinates=new(1,"Independent Cartesian units"),BoneRoles=roles.Select((r,i)=>new RigMapping(r,i,"Independent supplied native binding",true)).ToArray(),
            VirtualChains=[new("Opaque.Native.Path",[3,4,5],"Independent complete native ancestry",true)]};
        var clip=new AnimationClip(Guid.NewGuid(),"Independent authentic source",2,new(30),false,[]);
        return new(new(1,Guid.NewGuid(),label,scene,[clip]){ControlRigs=[rig]},rig);
    }
    static Fixture Volumetric(Fixture fixture,float extent,float height)
    {
        int bone=6;Vector3[] points=[new(-extent,height,-extent),new(extent,height,-extent),new(0,height,extent),new(0,height+extent*.1f,0)];
        var vertices=points.Select(p=>new Vertex(p,Vector3.UnitY,Vector2.Zero,new(bone,0,0,0),new(1,0,0,0),Vector4.One)).ToArray();
        var scene=fixture.Project.Scene with{Meshes=[new("Independent closed tetrahedral material",vertices,[0,2,1,0,1,3,1,2,3,2,0,3])]};
        return fixture with{Project=fixture.Project with{Scene=scene}};
    }
    static Fixture MaterialPermutation(Fixture fixture)
    {
        var scene=fixture.Project.Scene with{Meshes=fixture.Project.Scene.Meshes.Select(m=>m with
            {Vertices=new[]{m.Vertices[2],m.Vertices[0],m.Vertices[1]},Indices=[0,1,2]}).ToArray()};
        return fixture with{Project=fixture.Project with{Scene=scene}};
    }
    static Fixture Convert(Fixture fixture,float units,float degrees)
    {
        var turn=Matrix4x4.CreateRotationZ(degrees*MathF.PI/180);
        var scene=fixture.Project.Scene with{SkeletonRevision=Guid.NewGuid(),Bones=fixture.Project.Scene.Bones.Select((b,i)=>b with
            {Rest=b.Rest with{Translation=b.Rest.Translation*units,Rotation=i==0?new(0,0,degrees):b.Rest.Rotation}}).ToArray(),
            Meshes=fixture.Project.Scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with
                {Position=Vector3.Transform(v.Position*units,turn),Normal=Vector3.TransformNormal(v.Normal,turn)}).ToArray()}).ToArray()};
        var rig=fixture.Rig with{Id=Guid.NewGuid(),SkeletonFingerprint=AnimationControls.Fingerprint(scene),
            Axes=new(Vector3.TransformNormal(Vector3.UnitX,turn),Vector3.TransformNormal(Vector3.UnitY,turn),Vector3.UnitZ),Coordinates=new(units,"Independent transformed Cartesian units")};
        return new(fixture.Project with{Scene=scene,ControlRigs=[rig]},rig);
    }
    static Fixture Permute(Fixture original)
    {
        // A sibling moves before the reviewed path while each native parent
        // still precedes its children; this is a valid native storage order.
        var scene=original.Project.Scene;var extra=new Bone("old-extra",1,Transform.Identity with{Translation=Vector3.UnitZ});
        int originalCount=scene.Bones.Length;scene=scene with{Bones=scene.Bones.Append(extra).ToArray()};int[] order=[0,1,originalCount,..Enumerable.Range(2,originalCount-2)];
        var inverse=order.Select((old,index)=>(old,index)).ToDictionary(p=>p.old,p=>p.index);
        var changed=scene with{SkeletonRevision=Guid.NewGuid(),Bones=order.Select((old,index)=>scene.Bones[old] with{Name="random-"+index,Parent=scene.Bones[old].Parent<0?-1:inverse[scene.Bones[old].Parent]}).ToArray(),
            Meshes=scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Joints=new(inverse[(int)v.Joints.X],inverse[(int)v.Joints.Y],inverse[(int)v.Joints.Z],inverse[(int)v.Joints.W])}).ToArray()}).ToArray()};
        var rig=original.Rig with{Id=Guid.NewGuid(),SkeletonFingerprint=AnimationControls.Fingerprint(changed),BodyBone=inverse[1],
            BoneRoles=original.Rig.BoneRoles.Select(m=>m with{Bone=inverse[m.Bone]}).ToArray(),
            VirtualChains=original.Rig.VirtualChains!.Select(c=>c with{Bones=c.Bones.Select(b=>inverse[b]).ToArray()}).ToArray()};
        return new(original.Project with{Scene=changed,ControlRigs=[rig]},rig);
    }
}
