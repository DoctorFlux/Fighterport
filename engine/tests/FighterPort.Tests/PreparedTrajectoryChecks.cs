using System.Numerics;
using System.Text.Json;
using FighterPort.Core;

internal static class PreparedTrajectoryChecks
{
    public static async Task RunAsync(string directory,Action<bool,string> check)
    {
        var rows=new List<object>();
        foreach(bool reverse in new[]{false,true})
        {
            var a=ProceduralFixture.Create(reverse?1.6f:.6f);var b=ProceduralFixture.Create(reverse?.6f:1.6f);
            var ar=SurfaceContactChecks.Profile(a.Scene);var br=SurfaceContactChecks.Profile(b.Scene);
            const int samples=6;
            var clip=new AnimationClip(Guid.NewGuid(),"Independent constant native rest",samples,new(60),false,[]);
            a=a with{Clips=[clip],Contacts=[]};b=b with{Clips=[],Contacts=[]};
            var request=new RetargetRequest(a,clip,ar,b,br,RootMotionPolicy.Preserve,false,default,null,null,null,null,null,null,null);
            var source=RigDescriptor.Prepare(a.Scene,ar);var target=RigDescriptor.Prepare(b.Scene,br);
            var transfer=new BaselineMotionTransfer(request,source,target);
            var plan=TargetMotionPlan.Build(request,source,target,transfer,SourceMotionAnalysis.Analyze(request));
            var rest=AnimationService.Evaluate(b.Scene,null,0);
            var pins=br.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).Select(c=>ContactSolver.Capture(b.Scene,rest,br,c.Role,"Independent authored resting joint contacts")).ToArray();
            plan=plan with{Graph=new([new ContactFrame(Guid.NewGuid(),clip.Id,0,br.SkeletonFingerprint,pins){EndFrame=samples-1}],MotionContactCoverage.Required,[])};
            int chest=br.BoneRoles.Single(r=>r.SemanticRole=="Chest").Bone;
            var frames=Enumerable.Range(0,(samples-1)*4+1).Select(i=>i/4d).ToArray();
            var perturbed=frames.Select(f=>{var local=plan.Reference(f);local[chest]=local[chest] with{Rotation=local[chest].Rotation+new Vector3(0,0,25)};return local;}).ToArray();
            var seed=new SolvedTargetMotion(frames,perturbed,new("Independent wrong torso initializer",frames.Length,0,0,0,0,0,0,"Not accepted motion"));
            string sourceProof=JsonSerializer.Serialize(a,ProjectStore.Json),targetProof=JsonSerializer.Serialize(b,ProjectStore.Json);
            var outcome=RetargetTrajectorySolver.Solve(plan,false,evaluationBudget:160000,initialization:seed);
            var result=outcome.Value??throw new Exception("Trajectory was not prepared: "+string.Join("; ",outcome.Diagnostics.Select(d=>d.Message)));
            var motion=new AnimationClip(Guid.NewGuid(),"Authoritative destination coefficients",samples,new(60),false,result.Tracks);
            await File.WriteAllTextAsync(Path.Combine(directory,$"trajectory-{reverse}.json"),JsonSerializer.Serialize(new{result.Receipt},new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false,WriteIndented=true}));
            check((result.Receipt.Action is {AcceptedUpdates:>0}||result.Receipt.FreeChannelRecovery is {RecoveredBones:>0})&&result.Receipt.MeanActionCost<result.Receipt.ActionCostBeforeActionPass*.99,
                "Prepared destination coefficients reduce independently evaluated geometric action distortion through constrained action or source-driven free-channel recovery in each proportion direction");
            check(result.Receipt.PoseEvaluations<=result.Receipt.EvaluationBudget&&result.Receipt.Initialization=="Explicit diagnostic coefficient seed",
                "Trajectory work is bounded and an explicit test initializer cannot masquerade as a second normal backend");
            check(result.Frames.Select((f,i)=>AnimationService.Evaluate(b.Scene,motion,f).Local.SequenceEqual(result.Poses[i])).All(x=>x),
                "Every prepared trajectory pose comes from its authoritative final output curves without a second bake");
            double maximum=0;
            foreach(double frame in Enumerable.Range(0,(samples-1)*32+1).Select(i=>i/32d).Concat(new[]{.07031,1.3333,4.7777}))
            {
                var pose=AnimationService.Evaluate(b.Scene,motion,frame);
                foreach(var pin in pins)maximum=Math.Max(maximum,Vector3.Distance(pose.World[pin.Bone].Translation,rest.World[pin.Bone].Translation));
            }
            check(maximum<.0001&&result.Receipt.MaximumNormalizedConstraint<=1,
                "Withheld independently authored resting foot positions remain pinned while the torso action is corrected");
            var root=result.Tracks.Single(t=>t.Bone==b.Scene.Bones[0].Name);
            check(Enumerable.Range(0,9).All(ch=>root.Channels[ch]!.Keys.All(k=>k.Value==b.Scene.Bones[0].Rest.Channels()[ch]))&&
                result.Tracks.SelectMany(t=>t.Channels.Take(3)).OfType<Curve>().All(c=>c.Keys.All(k=>k.Value==1)),
                "Trajectory optimization preserves actor channels and native rest scales instead of stretching anatomy");
            var repeat=RetargetTrajectorySolver.Solve(plan,false,evaluationBudget:160000,initialization:seed).Value!;
            check(JsonSerializer.Serialize(result.Tracks,ProjectStore.Json)==JsonSerializer.Serialize(repeat.Tracks,ProjectStore.Json),
                "Repeated trajectory preparation yields identical coefficients independently of preview history");
            check(sourceProof==JsonSerializer.Serialize(a,ProjectStore.Json)&&targetProof==JsonSerializer.Serialize(b,ProjectStore.Json),
                "Trajectory preparation preserves authentic source and target native graphs, rests, weights and authored inputs");
            rows.Add(new{Reverse=reverse,WithheldPositionError=maximum,result.Receipt,Diagnostics=outcome.Diagnostics});
        }
        // An authored required orientation can legitimately exceed the artistic
        // review threshold. Physical restoration must not silently weaken that
        // pin to manufacture an automatic-style pass.
        var requiredScene=new Scene("Original constrained orientation fixture",
            [new("opaque_actor",-1,Transform.Identity),new("opaque_carrier",0,Transform.Identity),new("opaque_surface",1,Transform.Identity with{Translation=Vector3.UnitY})],
            [new("Original surface",new[]{new Vector3(-.1f,1,0),new(.1f,1,0),new(0,1,.1f)}.Select(p=>new Vertex(p,Vector3.UnitY,Vector2.Zero,new(2,0,0,0),new(1,0,0,0),Vector4.One)).ToArray(),[0,1,2])]);
        var requiredRig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(requiredScene),[],[])
        {BodyBone=1,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),BoneRoles=[new("RootMotion",0,"Independent reviewed actor",true),new("Hips",1,"Independent reviewed carrier",true),new("Part",2,"Independent reviewed region",true)]};
        var requiredClip=new AnimationClip(Guid.NewGuid(),"Original neutral preference",3,new(60),false,[]);
        var requiredProject=new FighterProject(1,Guid.NewGuid(),"Required orientation",requiredScene,[requiredClip]);
        var requiredRequest=new RetargetRequest(requiredProject,requiredClip,requiredRig,requiredProject,requiredRig,RootMotionPolicy.Preserve,true,default,null,null,null,null,null,null,null);
        var requiredDescriptor=RigDescriptor.Prepare(requiredScene,requiredRig);var requiredBaseline=new BaselineMotionTransfer(requiredRequest,requiredDescriptor,requiredDescriptor);
        var requiredPlan=TargetMotionPlan.Build(requiredRequest,requiredDescriptor,requiredDescriptor,requiredBaseline,SourceMotionAnalysis.Analyze(requiredRequest));
        var requiredPin=new ContactPin(Guid.NewGuid(),"Authored orientation",2,Vector3.Zero,Vector3.UnitY,Quaternion.CreateFromAxisAngle(Vector3.UnitZ,MathF.PI/2),true,"Independent 90-degree target orientation requirement");
        requiredPlan=requiredPlan with{Graph=new([new ContactFrame(Guid.NewGuid(),requiredClip.Id,0,requiredRig.SkeletonFingerprint,[requiredPin]){EndFrame=2}],MotionContactCoverage.Required,[])};
        var requiredResult=RetargetTrajectorySolver.Solve(requiredPlan,false,evaluationBudget:70000).Value!;
        var requiredMotion=requiredClip with{Tracks=requiredResult.Tracks};var requiredPose=AnimationService.Evaluate(requiredScene,requiredMotion,1.3333);
        double angle=PoseGeometry.RotationDifference(PoseGeometry.Rotation(requiredPose.World[2]),requiredPin.Rotation)*Math.PI/180;
        check(angle<=RetargetAcceptance.RotationRadians&&Vector3.Distance(requiredPose.World[2].Translation,Vector3.UnitY)<=requiredPlan.PositionBound,
            "A required 90-degree orientation is restored independently of a conflicting artistic pose preference");
        var fidelity=RetargetAcceptance.MeasureAction(requiredScene,requiredMotion,_=>AnimationService.Evaluate(requiredScene,null,0),[2],rig:requiredRig);
        check(!fidelity.Passed&&fidelity.MaximumDegrees>RetargetAcceptance.MaximumAutomaticCorrectionDegrees,
            "The unchanged final 45-degree action review still rejects a physically fitted but artistically excessive adaptation");
        rows.Add(new{RequiredOrientationRadians=angle,Action=fidelity,requiredResult.Receipt});
        // An original dense scene crosses the stored-contact point budget.
        // Actual geometry remains complete; a preview/save must not receive
        // the solver's full temporary collision array as each authored pin.
        var originalDense=ProceduralFixture.Create();var firstMesh=originalDense.Scene.Meshes[0];
        var denseScene=originalDense.Scene with{Meshes=[firstMesh with{Vertices=Enumerable.Range(0,3).SelectMany(_=>firstMesh.Vertices).ToArray()}]};
        var denseRig=SurfaceContactChecks.Profile(denseScene);var restClip=new AnimationClip(Guid.NewGuid(),"Original dense calibration",3,new(60),false,[]);
        var denseProject=originalDense with{Scene=denseScene,Clips=[restClip],Contacts=[]};
        var denseRequest=new RetargetRequest(denseProject,restClip,denseRig,denseProject,denseRig,RootMotionPolicy.Preserve,true,default,null,null,null,null,null,null,null);
        var denseDescriptor=RigDescriptor.Prepare(denseScene,denseRig);var denseTransfer=new BaselineMotionTransfer(denseRequest,denseDescriptor,denseDescriptor);
        var densePlan=TargetMotionPlan.Build(denseRequest,denseDescriptor,denseDescriptor,denseTransfer,SourceMotionAnalysis.Analyze(denseRequest));
        var plane=new ContactClearance(Vector3.UnitY,-5,denseDescriptor.SkinPoints);var denseRest=AnimationService.Evaluate(denseScene,null,0);
        var densePin=ContactSolver.Capture(denseScene,denseRest,denseRig,"Foot.L","Independent authored point with supplied clear plane") with{Clearance=plane};
        var denseContact=new ContactFrame(Guid.NewGuid(),restClip.Id,0,denseRig.SkeletonFingerprint,[densePin]){EndFrame=2};
        densePlan=densePlan with{Clearance=plane,Graph=new([denseContact],MotionContactCoverage.Required,[]),Grounding=new("Supplied clear plane",-5,-5,1,["Foot.L"],0){ExplicitSurface=true}};
        var denseResult=RetargetTrajectorySolver.Solve(densePlan,false).Value!;
        check(denseDescriptor.SkinPoints.Length>512&&denseResult.EffectiveClearance is {Points.Length:<=512},"Full dense skin remains available while stored contact calibration respects its separate schema budget");
        var previewProject=denseProject with{Clips=[restClip with{Tracks=denseResult.Tracks}],Contacts=[denseContact with{Pins=[densePin with{Clearance=denseResult.EffectiveClearance}]}]};
        var previewDocument=new EditorDocument(previewProject);previewDocument.Seek(.125);
        check(previewDocument.Clock.Frame==.125&&previewDocument.Evaluate().Local.Length==denseScene.Bones.Length,"A real dense prepared candidate constructs an ordinary editor preview and accepts fractional seeking without invalid contact anchors");
        string densePath=Path.Combine(directory,"dense-calibrated-preview.fighterport");await ProjectStore.SaveAsync(previewProject,densePath);var denseReopen=new EditorDocument(await ProjectStore.LoadAsync(densePath));
        check(denseReopen.Project.Scene.Meshes[0].Vertices.Length==denseScene.Meshes[0].Vertices.Length&&RetargetDependencies.Hash(denseReopen.Project.Scene.Meshes[0].Vertices)==RetargetDependencies.Hash(denseScene.Meshes[0].Vertices)&&denseReopen.Project.Contacts.Single().Pins.Single().Clearance!.Points.Length<=512,"Dense preview save/reopen retains every original serialized vertex/influence and bounded contact records");
        var fixture=ProceduralFixture.Create();var rig=SurfaceContactChecks.Profile(fixture.Scene);var native=fixture.Clips[0];
        var request2=new RetargetRequest(fixture,native,rig,fixture,rig,RootMotionPolicy.Preserve,false,default,null,null,null,null,null,null,null);
        var descriptor=RigDescriptor.Prepare(fixture.Scene,rig);var baseline=new BaselineMotionTransfer(request2,descriptor,descriptor);
        var freePlan=TargetMotionPlan.Build(request2,descriptor,descriptor,baseline,SourceMotionAnalysis.Analyze(request2));
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool observed=false;
        try{RetargetTrajectorySolver.Solve(freePlan,false,cancelled.Token);}catch(OperationCanceledException){observed=true;}
        check(observed&&RetargetTrajectorySolver.Solve(freePlan,false,evaluationBudget:1).Value is null,
            "Cancelled or insufficient-budget trajectories do not publish a partial sample set");
        await File.WriteAllTextAsync(Path.Combine(directory,"trajectory-receipts.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false,WriteIndented=true}));
    }
}
