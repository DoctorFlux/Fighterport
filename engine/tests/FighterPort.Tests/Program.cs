using System.Numerics;
using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;
using FighterPort.Rendering;

var checks=new List<string>();
void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);checks.Add(message);Console.WriteLine("PASS: "+message);}
void Near(float actual,float expected,float tolerance,string message)=>Check(Math.Abs(actual-expected)<=tolerance,$"{message} ({actual:G7} / {expected:G7})");
var dir=Path.GetFullPath(args.FirstOrDefault()??"artifacts/tests");Directory.CreateDirectory(dir);
try
{
    if(args.Contains("--complete-skin")){await CompleteSkinChecks.Run(args.Skip(Array.IndexOf(args,"--complete-skin")+1).ToArray(),Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--animation-optimization")){AnimationOptimizationChecks.Run(Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--rotation-path")){RotationPathChecks.Run(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--rotation-native")){int i=Array.IndexOf(args,"--rotation-native");RotationPathChecks.Native(args[i+1],args[i+2],dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--contact-authoring")){await ContactAuthoringChecks.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--contact-slots")){await ContactSlotChecks.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--boundary-fitting")){await BoundaryFittingChecks.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--viewer-controls")){await EditorControlChecks.RunAsync(dir,Check);int nativeControls=Array.IndexOf(args,"--native-controls");if(nativeControls>=0)await EditorControlChecks.NativeAsync(args[nativeControls+1],dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--viewer-editing")){await FrameEditingChecks.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--viewer-native")){await NativeFrameEditingChecks.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--body-surface")){BodySurfaceReachChecks.Run(Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--temporal-body")){await NativeSceneObjectiveChecks.RunAsync(Path.Combine(dir,"objective"),Check);await TemporalActionFittingChecks.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--receipt-compatibility")){await ReceiptCompatibilityChecks.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--endpoint-geometry")){EndpointSceneGeometryChecks.Run(Check);FractionalDerivedChannelChecks.Run(Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--articulated-body")){NativePoseDifferentialChecks.Run(Check);await ArticulatedBodyFittingChecks.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--scene-body")){SceneBodyFittingChecks.Run(Check);BodySceneRegionChecks.Run(Check);await BodyVolumePolicyChecks.RunAsync(Path.Combine(dir,"volume-policy"),Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--body-reach")){BodyRelativeReachChecks.Run(Check);NativeChainReachChecks.Run(Check);ReachSeedChecks.Run(Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--body-reach-workflow")){await BodyReachWorkflowChecks.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    int poseNative=Array.IndexOf(args,"--pose-native-revalidate");if(poseNative>=0){if(poseNative+1>=args.Length)throw new ArgumentException("Supply the native input manifest after --pose-native-revalidate.");await PoseNativeRevalidationChecks.RunAsync(dir,args[poseNative+1],Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--pose-rewrite-workflow")){await PoseRewriteWorkflowChecks.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--pose-transfer-rewrite")){PoseTransferRewriteChecks.Run(Check);PoseMotionReconstructionChecks.Run(Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--prepared-trajectory")){await PreparedTrajectoryChecks.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--trajectory-workflow")){await RetargetV2Checks.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--surface-contacts")){await SurfaceContactChecks.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    int semanticControls=Array.IndexOf(args,"--semantic-controls");
    if(semanticControls>=0)
    {
        var fixtures=new List<FighterProject>();
        foreach(string fixturePath in args.Skip(semanticControls+1))fixtures.Add(await ProjectStore.LoadAsync(fixturePath));
        await SemanticControlChecks.RunAsync(dir,Check,fixtures.ToArray());
        await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;
    }
    if(args.Contains("--native-coefficients")){await NativeCoefficientChecks.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--native-release-stages")){await NativeReleaseProbe.RunAsync(dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks,Claim="Diagnostic invariants only; measured contact/action failures remain failures."}));return 0;}
    if(args.Contains("--retarget-contracts")){ChannelTransferChecks.Run(Check);ActionObjectiveChecks.Run(Check);ConstraintProjectionChecks.Run(Check);ContactMotionPriorChecks.Run(Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    int sourceWitnesses=Array.IndexOf(args,"--source-support-witnesses");if(sourceWitnesses>=0){await RetargetStageProbe.SourceWitnesses(args[sourceWitnesses+1],dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    int generalization=Array.IndexOf(args,"--generalization");if(generalization>=0){await GeneralizationChecks.Run(args[generalization+1],args[generalization+2],dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    int models=Array.IndexOf(args,"--library-models");if(models>=0){await MultiModelLibraryChecks.Run(args[models+1],args[models+2],dir,Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    int stages=Array.IndexOf(args,"--retarget-stages");if(stages>=0){await RetargetStageProbe.Run(args[stages+1],args[stages+2],args[stages+3],dir,args.Contains("--ledge"),Check,args.Contains("--plan-only"),args.Contains("--temporal-only"));return 0;}
    int workspace=Array.IndexOf(args,"--workspace-only");if(workspace>=0){var project=workspace+1<args.Length?await ProjectStore.LoadAsync(args[workspace+1]):ProceduralFixture.Create();var bridge=Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER") is {} worker?new BrawlAdapter(worker):null;await CharacterWorkspaceChecks.RunAsync(dir,project,Check,bridge);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--v2-native-fixtures")||args.Contains("--v2-native-moving")){var bridge=new BrawlAdapter(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER")??throw new Exception("Worker env var required."));await NativeRetargetV2Checks.RunAsync(bridge,dir,Check,args.Contains("--v2-native-moving"));await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    if(args.Contains("--backend"))throw new ArgumentException("Retargeting uses one engine; remove --backend.");
    if(args.Contains("--support-geometry-only")){SupportSurfaceChecks.Run(Check);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    int exact=Array.IndexOf(args,"--native-exact");if(exact>=0){var bridge=new BrawlAdapter(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER")??throw new Exception("Worker env var required."));await NativeExactClipChecks.RunAsync(bridge,args[exact+1],dir,Check);return 0;}
    int matrix=Array.IndexOf(args,"--retarget-matrix");if(matrix>=0){var bridge=new BrawlAdapter(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER")??throw new Exception("Worker env var required."));await NativeRetargetMatrix.RunAsync(bridge,args[matrix+1],args[matrix+2],dir,Check,args.Contains("--floor"),args.Contains("--ledge"));return 0;}
    int libraryOnly=Array.IndexOf(args,"--library-native");if(libraryOnly>=0){var bridge=new BrawlAdapter(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER")??throw new Exception("Worker env var required."));await NativeLibraryChecks.RunAsync(bridge,args[libraryOnly+1],args[libraryOnly+2],dir,Check,Near);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    int focused=Array.IndexOf(args,"--retarget-only");if(focused>=0){var bridge=new BrawlAdapter(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER")??throw new Exception("Worker env var required."));await NativeRetargetChecks.RunAsync(bridge,await ProjectStore.LoadAsync(args[focused+1]),await ProjectStore.LoadAsync(args[focused+2]),dir,Check,Near);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    int containerOnly=Array.IndexOf(args,"--container-only");if(containerOnly>=0){var bridge=new BrawlAdapter(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER")??throw new Exception("Worker env var required."));await NativeContainerChecks.RunAsync(bridge,await ProjectStore.LoadAsync(args[containerOnly+1]),dir,Check,Near);await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    int morphOnly=Array.IndexOf(args,"--morph-only");if(morphOnly>=0){var bridge=new BrawlAdapter(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER")??throw new Exception("Worker env var required."));await MorphChecks.NativeAsync(bridge,dir,Check,Near,version:3);await MorphChecks.NativeAsync(bridge,dir,Check,Near,version:4);for(int i=morphOnly+1;i<args.Length;i++){if(args[i]=="--morph-private")await MorphChecks.NativeAsync(bridge,dir,Check,Near,args[++i]);else if(args[i]=="--morph-private-pac"){string model=args[++i],motion=args[++i];await MorphChecks.NativeAsync(bridge,dir,Check,Near,model,privateMotion:motion);}}await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks}));return 0;}
    var original=ProceduralFixture.Create();var doc=new EditorDocument(original);var scene=original.Scene;
    var authored=new EditorDocument(original);authored.SelectClip(1);var prior=authored.Clip;authored.CreateAnimation("Original new move",24,new(60),true);Check(authored.Clip.Source is null&&authored.Clip.Id!=prior.Id&&authored.Clip.Tracks.Length==scene.Bones.Length,"New animation authors real existing-bone keys independently of an imported source clip");authored.Undo();Check(authored.Clip.Id==prior.Id,"Undoing new animation restores the previous selection without a stale clip index");authored.Redo();Check(authored.Clip.Name=="Original new move","Redo restores the created clip and its selection");authored.CreateAnimation("Original copy",24,new(60),true,true);authored.RenameAnimation("Original renamed copy");Check(authored.Clip.NativeWriteName=="Original renamed copy"&&prior.Name==original.Clips[1].Name,"Duplicate and rename create independent native output names without renaming source identities");
    CompanionLayerChecks.Run(original,dir,Check);
    await MorphChecks.CoreAsync(original,dir,Check);
    await MorphEditingChecks.CoreAsync(original,dir,Check);
    await MorphGeometryChecks.CoreAsync(original,dir,Check);
    var translated=BrawlTextureTransform.Evaluate(1,1,0,.25f,-.125f,0);Near(translated.M41,-.25f,0,"Maya SRT translation has native inverse-S convention");Near(translated.M42,-.125f,0,"Maya SRT translation retains native T convention");
    var centered=Vector3.Transform(Vector3.Zero,BrawlTextureTransform.Evaluate(1,1,90,0,0,2));Near(centered.Y,1,.000001f,"Max texture rotation preserves the native half-texel-space center");
    var identity=AnimationService.Evaluate(scene,null,0);Near(scene.Meshes.SelectMany(m=>m.Vertices).Max(v=>Vector3.Distance(v.Position,AnimationService.Skin(v,identity))),0,1e-5f,"Bind and inverse bind preserve geometry");
    var a=AnimationService.Evaluate(scene,doc.Clip,0);var b=AnimationService.Evaluate(scene,doc.Clip,15);Check(scene.Meshes[0].Vertices.Any(v=>Vector3.Distance(AnimationService.Skin(v,a),AnimationService.Skin(v,b))>.2f),"Animation moves weighted mesh vertices");
    var c=new Curve([new(0,0,2,2),new(10,20,2,2)]);Near(c.Sample(2.375),4.75f,1e-6f,"Fractional native Hermite reproduces independent linear expectation");
    var cubic=new Curve([new(0,0),new(2,8)]);Near(cubic.Sample(.5),1.25f,1e-6f,"Cubic sample matches hand-derived Hermite polynomial");
    Near(new Curve([new(0,1),new(2,7)],Interpolation.Step).Sample(1.5),1,0,"Stepped channels retain held values");
    var paired=new Curve([new(0,0),new(1,3),new(1,7),new(2,9)]);Near(paired.Sample(1),3,0,"Equal-time native key chooses incoming exact sample");Check(paired.Sample(1.01)>7,"Equal-time outgoing key drives following interval");
    var loop=new Curve([new(2,2,1,1),new(8,8,1,1)],LoopFrameLimit:11);Near(loop.Sample(9),7.856f,.0001f,"Native loop gap uses frame limit independently of sample count");
    var spin=new Curve([new(0,0,30,30),new(24,720,30,30)]);Near(spin.Sample(12.5),375,1e-4f,"Full turns survive Euler winding");
    var clock=new AnimationClock{Playing=true};clock.Advance(1.25/60,doc.Clip);Check(Math.Abs(clock.Frame-1.25)<1e-9,"Clock retains fractional frame");clock.Advance(1,doc.Clip);Check(Math.Abs(clock.Frame-1.25)<1e-9,"Loop uses end-exclusive sample count");
    var attack=original.Clips[2];Check(attack.SampleCount==24 && attack.LastKeyFrame==23 && Math.Abs(attack.DurationSeconds-.4)<1e-9,"Sample count, last key and duration stay distinct");
    var replay=new AnimationClock();replay.Play(attack);replay.Advance(1,attack);Check(!replay.Playing&&replay.Frame==attack.PreviewEndFrame,"One-shot playback preserves its full end-exclusive native duration without displaying an extra endpoint");replay.Play(attack);Check(replay.Playing&&replay.Frame==0,"Play restarts a completed one-shot from its first sample");replay.Advance(.1,attack);replay.Playing=false;replay.Play(attack);Near((float)replay.Frame,6,0,"Play resumes a paused unfinished clip");replay.LoopOverride=true;replay.Seek(23,attack);replay.Advance(1.25/60,attack);Near((float)replay.Frame,.25f,.00001f,"Preview loop wraps at native sample count without repeating an endpoint");Check(!attack.Loop,"Preview looping leaves native clip/export loop metadata unchanged");replay.LoopOverride=false;replay.Seek(59,original.Clips[0]);replay.Playing=true;replay.Advance(.1,original.Clips[0]);Check(!replay.Playing&&replay.Frame==original.Clips[0].PreviewEndFrame,"Preview can play a natively looping animation once through its final fractional segment");
    Check(original.Clips[0].LastSampleFrame==59 && original.Clips[0].LastKeyFrame==60,"Loop closure key time is distinct from last displayed sample");
    var before=doc.Evaluate().Local[0];doc.Seek(7.25);doc.KeyTransform(0,before with{Translation=new(1,2,3)});Near(doc.Evaluate().Local[0].Translation.X,1,0,"Actual key edit changes pose");doc.Undo();Near(doc.Evaluate().Local[0].Translation.X,0,0,"Undo restores source-independent revision");doc.Redo();Near(doc.Evaluate().Local[0].Translation.Y,2,0,"Redo restores key transaction");Check(original.Clips[0].Tracks.All(t=>t.Bone!="Root"),"Target edits leave shared source revision unchanged");
    var path=Path.Combine(dir,"roundtrip.fighterport");await ProjectStore.SaveAsync(doc.Project,path);var reopened=await ProjectStore.LoadAsync(path);Near(AnimationService.Evaluate(reopened.Scene,reopened.Clips[0],7.25).Local[0].Translation.Z,3,0,"Save/reopen retains actual fractional edit");
    foreach(var f in new[]{15.75,0,.125,45.5,15.75}){var p=AnimationService.Evaluate(scene,original.Clips[0],f);var q=AnimationService.Evaluate(scene,original.Clips[0],f);Check(p.World.SequenceEqual(q.World),$"Scrub deterministic at {f}");}
    var sourcePath=Path.Combine(dir,"sources.json");var s1=SourceRegistry.Create("PM training","Project M","Wii","3.6-training",SourceKind.ModBuild,dir);var s2=SourceRegistry.Create("PM tournament","Project M","Wii","3.6-tournament",SourceKind.ModBuild,dir);await SourceRegistry.SaveAsync([s1,s2],sourcePath);var sources=await SourceRegistry.LoadAsync(sourcePath);Check(sources.Length==2 && sources[0].Id!=sources[1].Id && sources[0].Build!=sources[1].Build,"Multiple Project M builds persist with distinct identities");
    var layerDocument=new EditorDocument(original);layerDocument.Seek(30);
    var timedLayer=new NativeAnimationLayer(Guid.NewGuid(),"Different timebase","SRT0",20,true,new(30),new("unresolved","entry","hash","SRT0",0),[],"test");
    Near((float)layerDocument.LayerFrame(timedLayer),15,0,"Companion animation converts shared native time between 60 and 30 fps");
    layerDocument.Seek(50);Near((float)layerDocument.LayerFrame(timedLayer),5,0,"Companion animation loops at its own end-exclusive sample count");
    Near((float)layerDocument.LayerFrame(timedLayer with{Loop=false}),19,0,"Non-looping companion animation clamps to its own final sample");
    var alternate=ProceduralFixture.Create(1.4f);var altBones=alternate.Scene.Bones.Select((bone,i)=>bone with{Name=$"Joint_{i:00}"}).ToArray();
    altBones[4]=altBones[4] with{Rest=altBones[4].Rest with{Rotation=new(0,0,25)}};
    altBones[5]=altBones[5] with{Rest=altBones[5].Rest with{Rotation=new(0,0,-25),Translation=Vector3.Transform(altBones[5].Rest.Translation,Matrix4x4.CreateRotationZ(-25*MathF.PI/180))}};
    altBones[7]=altBones[7] with{Rest=altBones[7].Rest with{Rotation=new(0,0,90)}};
    altBones[8]=altBones[8] with{Rest=altBones[8].Rest with{Rotation=new(0,0,-90),Translation=Vector3.Transform(altBones[8].Rest.Translation,Matrix4x4.CreateRotationZ(-MathF.PI/2))}};
    var altScene=alternate.Scene with{Bones=altBones,SkeletonRevision=Guid.NewGuid()};
    BodyRelativeReachChecks.Run(Check);NativeChainReachChecks.Run(Check);ReachSeedChecks.Run(Check);
    await BodyReachWorkflowChecks.RunAsync(Path.Combine(dir,"body-reach"),Check);
    SceneBodyFittingChecks.Run(Check);
    BodySceneRegionChecks.Run(Check);
    await BodyVolumePolicyChecks.RunAsync(Path.Combine(dir,"volume-policy"),Check);
    NativePoseDifferentialChecks.Run(Check);
    EndpointSceneGeometryChecks.Run(Check);FractionalDerivedChannelChecks.Run(Check);
    await NativeSceneObjectiveChecks.RunAsync(Path.Combine(dir,"scene-objective"),Check);
    await ArticulatedBodyFittingChecks.RunAsync(Path.Combine(dir,"articulated-body"),Check);
    await ReceiptCompatibilityChecks.RunAsync(Path.Combine(dir,"receipt-compatibility"),Check);
    await TemporalActionFittingChecks.RunAsync(Path.Combine(dir,"temporal-body"),Check);
    await ContactChecks.RunAsync(original,altScene,dir,Check);
    await RetargetChecks.RunAsync(original,altScene,dir,Check);
    await RetargetV2Checks.RunAsync(dir,Check);
    await SurfaceContactChecks.RunAsync(dir,Check);await RetargetWorkflowChecks.RunAsync(dir,Check);await ContactReferenceChecks.RunAsync(dir,Check);
    await DetailedBoneChecks.RunAsync(dir,Check);
    await LibraryChecks.RunAsync(original,altScene,dir,Check);
    await QuaternionChecks.RunAsync(original,altScene,dir,Check);
    foreach(var (rigScene,label) in new[]{(scene,"named rig"),(altScene,"renamed unequal-proportion/axis rig")})
    {
        var checksum=AnimationControls.DeformChecksum(rigScene);var setup=AnimationControls.SetUp(rigScene);
        Check(setup.Chains.Length==4,$"Automatic control setup identifies four symmetric weighted limbs on {label}");Check(checksum==AnimationControls.DeformChecksum(rigScene),$"Setup preserves deform bones/rest/weights on {label}");
        var chain=setup.Chains.Single(c=>c.Role=="Hand.R");var rest=AnimationService.Evaluate(rigScene,null,0);var origin=rest.World[chain.Root].Translation;var end=rest.World[chain.Effector].Translation;
        var goal=origin+(end-origin)*.82f+new Vector3(0,.04f,.12f);var pole=origin+Vector3.UnitZ;
        var solved=AnimationControls.Solve(rigScene,rest,setup,chain,goal,pole);Check(solved.Value is not null,$"IK works independently without any imported clip on {label}");Near(solved.Value!.Error,0,.0001f,$"IK reaches independent world-space effector goal on {label}");
        var solvedPose=AnimationService.Evaluate(rigScene,null,0,solved.Value.Transforms);
        Near(Vector3.Distance(solvedPose.World[chain.Root].Translation,solvedPose.World[chain.Middle].Translation),Vector3.Distance(origin,rest.World[chain.Middle].Translation),.0001f,$"IK preserves native upper limb length on {label}");
        Near(Vector3.Distance(solvedPose.World[chain.Middle].Translation,solvedPose.World[chain.Effector].Translation),Vector3.Distance(rest.World[chain.Middle].Translation,end),.0001f,$"IK preserves native lower limb length on {label}");
        Check((solvedPose.World[chain.Middle].Translation-origin).Z>0,$"Elbow pole chooses requested bend half-plane on {label}");
        var docWithoutMotion=new EditorDocument(original with{Scene=rigScene,Clips=[]});docWithoutMotion.Seek(12);docWithoutMotion.Transaction("Set up controls",p=>p with{ControlRigs=[setup]});docWithoutMotion.KeyTransforms(solved.Value.Transforms);
        Near(Vector3.Distance(docWithoutMotion.Evaluate().World[chain.Effector].Translation,goal),0,.0001f,$"Baking controls creates editable keys without a donor on {label}");
        docWithoutMotion.Undo();Check(docWithoutMotion.Project.Clips.Length==0,$"Whole IK bake undoes as one transaction on {label}");docWithoutMotion.Redo();
        var rigFile=Path.Combine(dir,label.StartsWith("named")?"rig-named.fighterport":"rig-renamed.fighterport");await ProjectStore.SaveAsync(docWithoutMotion.Project,rigFile);var loaded=await ProjectStore.LoadAsync(rigFile);var reused=AnimationControls.SetUp(loaded.Scene,loaded.ControlRigs);Check(reused.Id==setup.Id,$"Exact rig setup persists and reuses across reopen on {label}");
        var unreachable=AnimationControls.Solve(rigScene,rest,setup,chain,origin+new Vector3(1000,0,0),pole);Check(unreachable.Value is null&&unreachable.Diagnostics.Single().Code=="UNREACHABLE_GOAL",$"Unreachable IK reports exact range and preserves original on {label}");
        var changed=rigScene with{Bones=rigScene.Bones.Select((bone,i)=>i==chain.Middle?bone with{Rest=bone.Rest with{Translation=bone.Rest.Translation*1.01f}}:bone).ToArray()};Check(AnimationControls.Solve(changed,rest,setup,chain,goal,pole).Diagnostics.Any(d=>d.Code=="RIG_CHANGED"),$"Changed skeleton/rest invalidates saved setup on {label}");
        foreach(var offset in new[]{0f,.01f,-.015f,0f}){var solution=AnimationControls.Solve(rigScene,rest,setup,chain,goal+new Vector3(0,offset,0),pole);Check(solution.Value is not null,$"Deterministic stateless IK solve {offset} on {label}");}
        foreach(var slack in new[]{.001f,.0001f,.00001f}){float length=Vector3.Distance(origin,rest.World[chain.Middle].Translation)+Vector3.Distance(rest.World[chain.Middle].Translation,end);var nearStraight=AnimationControls.Solve(rigScene,rest,setup,chain,origin+Vector3.Normalize(end-origin)*(length-slack),origin+Vector3.UnitZ);Check(nearStraight.Value is not null,$"Near-straight pole remains solvable at slack {slack} on {label}");}
        var leg=setup.Chains.Single(c=>c.Role=="Foot.R");var hip=rest.World[leg.Root].Translation;var footGoal=hip+(rest.World[leg.Effector].Translation-hip)*.88f+Vector3.UnitZ*.08f;var foot=AnimationControls.Solve(rigScene,rest,setup,leg,footGoal,hip+Vector3.UnitZ);Check(foot.Value is not null&&foot.Value.Error<.0001f,$"Knee/foot IK reaches a free airborne goal without a floor constraint on {label}");
        int spineIndex=2;var fk=new Dictionary<int,Transform>{{spineIndex,rest.Local[spineIndex] with{Rotation=new(0,12,0)}}};var fkPose=AnimationService.Evaluate(rigScene,null,0,fk);var fkRoot=fkPose.World[chain.Root].Translation;var comboGoal=fkRoot+(fkPose.World[chain.Effector].Translation-fkRoot)*.86f+Vector3.UnitZ*.07f;var combination=AnimationControls.Solve(rigScene,fkPose,setup,chain,comboGoal,fkRoot+Vector3.UnitZ);Check(combination.Value is not null,$"Independent FK and IK compose on the existing rig on {label}");foreach(var pair in combination.Value!.Transforms)fk[pair.Key]=pair.Value;var combined=AnimationService.Evaluate(rigScene,null,0,fk);Near(combined.Local[spineIndex].Rotation.Y,12,0,$"IK retains independent chest FK correction on {label}");
        Check(checksum==AnimationControls.DeformChecksum(rigScene),$"FK/IK editing never replaces the deform skeleton on {label}");
    }
    var suggested=AnimationControls.SetUp(scene);var selected=suggested.Chains.Single(c=>c.Role=="Hand.R");
    var manual=AnimationControls.ReviewChain(scene,suggested,selected.Role,selected.Root,selected.Middle,selected.Effector,-Vector3.UnitZ,"Original fixture manual review");
    Check(manual.Value is not null&&manual.Value.Chains.Single(c=>c.Role==selected.Role).Reviewed&&manual.Value.BoneRoles.Any(m=>m.SemanticRole=="Wrist.R"&&m.Bone==selected.Effector),"Manual review stores calibrated existing-bone roles without a game profile");
    Check(AnimationControls.ReviewChain(scene,suggested,selected.Role,selected.Root,selected.Effector,selected.Middle,Vector3.UnitZ,"Invalid hierarchy").Diagnostics.Any(d=>d.Code=="CHAIN_HELPERS"),"Mapping review rejects a non-parent chain without reparenting bones");
    Check(AnimationControls.ReviewChain(scene,suggested,selected.Role,selected.Root,selected.Middle,selected.Effector,Vector3.Zero,"Invalid pole").Diagnostics.Any(d=>d.Code=="INVALID_POLE"),"Mapping review rejects undefined pole calibration");
    var helperScene=scene with{Bones=scene.Bones.Append(new("OriginalHelper",selected.Middle,Transform.Identity with{Translation=new(.12f,.07f,.02f)})).ToArray()};
    var helperSetup=AnimationControls.SetUp(helperScene);var helperChain=helperSetup.Chains.Single(c=>c.Role==selected.Role);int helperIndex=helperScene.Bones.Length-1;
    var helperOverride=new Dictionary<int,Transform>{{helperIndex,helperScene.Bones[helperIndex].Rest with{Rotation=new(15,20,25)}}};var helperPose=AnimationService.Evaluate(helperScene,null,0,helperOverride);var helperRoot=helperPose.World[helperChain.Root].Translation;var helperPole=AnimationControls.DefaultPole(helperScene,helperPose,helperChain);
    var helperSolve=AnimationControls.Solve(helperScene,helperPose,helperSetup,helperChain,helperRoot+(helperPose.World[helperChain.Effector].Translation-helperRoot)*.8f,helperPole);
    Check(helperSolve.Value is not null&&!helperSolve.Value.Transforms.ContainsKey(helperIndex),"Existing sibling helper needs no replacement joint or IK-authored helper key");
    foreach(var pair in helperSolve.Value!.Transforms)helperOverride[pair.Key]=pair.Value;var helperResult=AnimationService.Evaluate(helperScene,null,0,helperOverride);
    Check(helperResult.Local[helperIndex]==helperPose.Local[helperIndex]&&helperResult.World[helperIndex]!=helperPose.World[helperIndex],"Sibling helper retains authored local rotation and follows its native parent");
    if(args.Contains("--native"))
    {
        var adapter=new BrawlAdapter(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER")??throw new Exception("Worker env var required."));
        await MorphChecks.NativeAsync(adapter,dir,Check,Near);
        var matrixCases=(from mode in Enumerable.Range(0,3) from values in new float[][]{[1,1,0,0,0],[2,.75f,0,0,0],[1,1,90,0,0],[1,1,0,.25f,-.125f],[1.2f,.8f,37,.15f,-.2f],[-.5f,2.1f,-175,-.12f,.3f],[1.3f,.6f,360,.1f,.2f]} select new TextureMatrixCase(mode,values)).ToArray();
        var nativeMatrices=await adapter.RunAsync(new("texture-matrices","","",null,null,[],[],TextureMatrices:matrixCases));
        for(int mi=0;mi<matrixCases.Length;mi++){var input=matrixCases[mi];var f=input.Values;var m=BrawlTextureTransform.Evaluate(f[0],f[1],f[2],f[3],f[4],input.Mode);float[] actual=[m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];Near(actual.Zip(nativeMatrices.TextureMatrixSamples![mi]).Max(p=>Math.Abs(p.First-p.Second)),0,.000002f,$"Native texture matrix mode {input.Mode}, case {mi%7} matches independent worker");}
        var nd=Path.Combine(dir,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(nd);var dae=Path.Combine(nd,"fixture.dae");FixtureDae.Write(scene,dae);var mdl=Path.Combine(nd,"fixture.mdl0");var chr=Path.Combine(nd,"fixture.chr0");
        await adapter.RunAsync(new("fixture",dae,chr,null,mdl,[],[]));
        double[] times=[0,.125,.5,7.25,11,11.125,15.75,23];
        var response=await adapter.RunAsync(new("import",mdl,chr,null,null,[],times));var project=response.Project!;
        await NativeFloatChecks.RunAsync(adapter,project,nd,Check);await NativeTimingChecks.RunAsync(adapter,project,nd,Check);
        Check(project.Clips[0].SampleCount==24 && !project.Clips[0].Loop,"Original real native CHR0 carries 24-sample timing");
        float Compare(FighterProject p,float[][][] expected){float max=0;var vertices=p.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();for(int i=0;i<times.Length;i++){var pose=AnimationService.Evaluate(p.Scene,p.Clips[0],times[i]);for(int j=0;j<vertices.Length;j++)max=Math.Max(max,Vector3.Distance(AnimationService.Skin(vertices[j],pose),new(expected[i][j][0],expected[i][j][1],expected[i][j][2])));}return max;}
        Near(Compare(project,response.ExpectedVertices!),0,.002f,"Owned native evaluator/skinning agrees with independent BrawlLib at withheld subframes");
        float Analytic(FighterProject p,bool edited)
        {
            var vertices=p.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();int arm=Array.FindIndex(p.Scene.Bones,x=>x.Name=="Arm.R");var pivot=Vector3.Zero;for(int k=arm;k>=0;k=p.Scene.Bones[k].Parent)pivot+=p.Scene.Bones[k].Rest.Translation;
            float max=0;
            foreach(double t in times)
            {
                var pose=AnimationService.Evaluate(p.Scene,p.Clips[0],t);
                // Independent reference: known fixture is rigid Z rotation plus actor X movement.
                float angle=(float)t*4;
                if(edited){double l=t<=11?11:12,u=t<=11?t/11:(t-11)/12;double av=t<=11?0:60,bv=t<=11?60:92,at=t<=11?4:0,bt=t<=11?0:4;angle=(float)((2*u*u*u-3*u*u+1)*av+(u*u*u-2*u*u+u)*l*at+(-2*u*u*u+3*u*u)*bv+(u*u*u-u*u)*l*bt);}
                float rad=angle*MathF.PI/180,cos=MathF.Cos(rad),sin=MathF.Sin(rad);
                foreach(var v in vertices){int k=(int)v.Joints.X;bool affected=false;while(k>=0){if(k==arm)affected=true;k=p.Scene.Bones[k].Parent;}var expected=v.Position;if(affected){var rel=expected-pivot;expected=pivot+new Vector3(cos*rel.X-sin*rel.Y,sin*rel.X+cos*rel.Y,rel.Z);}expected.X+=(float)t*.05f;max=Math.Max(max,Vector3.Distance(expected,AnimationService.Skin(v,pose)));}
            }
            return max;
        }
        Near(Analytic(project,false),0,.002f,"Native geometry agrees with independent trigonometric fixture expectations");
        var hashes=new[]{ProjectStore.Hash(mdl),ProjectStore.Hash(chr)};var edit=new EditorDocument(project);edit.Seek(11);int armIndex=Array.FindIndex(project.Scene.Bones,x=>x.Name=="Arm.R");var armPose=edit.Evaluate().Local[armIndex];edit.KeyTransform(armIndex,armPose with{Rotation=armPose.Rotation with{Z=60}});
        var output=Path.Combine(nd,"edited.chr0");var exported=await adapter.ExportAsync(edit.Project,edit.Clip,output);
        var fresh=await adapter.RunAsync(new("import",mdl,output,null,null,[],times));var native=fresh.Project!;
        Near(Compare(native,fresh.ExpectedVertices!),0,.003f,"Fresh edited native decode matches separate native skinning at subframes");Near(Analytic(native,true),0,.003f,"Fresh edited geometry matches independent edited Hermite/trigonometric expectations");
        Check(native.Clips[0].SampleCount==24 && native.Clips[0].Source!.Resource.Sha256==ProjectStore.Hash(output),"Native duration and reopened output hash retained");Check(hashes.SequenceEqual(new[]{ProjectStore.Hash(mdl),ProjectStore.Hash(chr)}),"Original native source hashes unchanged after edit/export/reopen");
        await ProjectStore.SaveAsync(edit.Project,Path.Combine(dir,"native-edited.fighterport"));
        var modelOnly=await adapter.ImportModelAsync(mdl);Check(modelOnly.Clips.Length==0&&modelOnly.SourceModels.Length==1,"Native already-skinned model imports without any motion");
        var made=new EditorDocument(modelOnly);made.CreateAnimation("OriginalCreated18",18,new(60),true);made.Seek(12);var createdRoot=made.Evaluate().Local[0];made.KeyTransform(0,createdRoot with{Translation=createdRoot.Translation+new Vector3(.2f,0,0)});var created=await adapter.ExportAsync(made.Project,made.Clip,Path.Combine(nd,"created18.chr0"));Check(created.Project!.Clips[0].SampleCount==18&&created.Project.Clips[0].Source?.NativeName=="OriginalCreated18","Native model-only workflow creates a genuine new named CHR0 without requiring a donor animation");Near(AnimationService.Evaluate(created.Project.Scene,created.Project.Clips[0],12).Local[0].Translation.X,createdRoot.Translation.X+.2f,.0001f,"Newly authored native animation keys survive independent decode");
        await NativeContainerChecks.RunAsync(adapter,await adapter.ImportAsync(mdl,mdl+".motion.brres"),nd,Check,Near);
        Check(AnimationControls.Fingerprint(modelOnly.Scene)==AnimationControls.Fingerprint(project.Scene),"Model-only import preserves exact source skeleton identity");
        var nativeSetup=AnimationControls.SetUp(project.Scene);var nativeChain=nativeSetup.Chains.Single(c=>c.Role=="Hand.R");var nativeDoc=new EditorDocument(project);nativeDoc.Seek(12);var initialPose=nativeDoc.Evaluate();var nativeOrigin=initialPose.World[nativeChain.Root].Translation;var nativeGoal=nativeOrigin+(initialPose.World[nativeChain.Effector].Translation-nativeOrigin)*.82f+new Vector3(0,0,.12f);
        var nativeSolve=AnimationControls.Solve(project.Scene,initialPose,nativeSetup,nativeChain,nativeGoal,nativeOrigin+Vector3.UnitZ);Check(nativeSolve.Value is not null,"Same-rig IK operates on a native clip independently of retargeting");
        nativeDoc.KeyTransforms(nativeSolve.Value!.Transforms);var ikOutput=Path.Combine(nd,"ik-baked.chr0");await adapter.ExportAsync(nativeDoc.Project,nativeDoc.Clip,ikOutput);var ikFresh=await adapter.RunAsync(new("import",mdl,ikOutput,null,null,[],times));
        Near(Compare(ikFresh.Project!,ikFresh.ExpectedVertices!),0,.003f,"IK baked native animation fresh-decodes with independent geometry agreement");
        var nativePose=AnimationService.Evaluate(ikFresh.Project!.Scene,ikFresh.Project.Clips[0],12);Near(Vector3.Distance(nativePose.World[nativeChain.Effector].Translation,nativeGoal),0,.003f,"Baked IK effector reaches its goal after native quantization and reopen");
        Check(AnimationControls.DeformChecksum(project.Scene)==AnimationControls.DeformChecksum(ikFresh.Project.Scene),"IK native bake preserves original deform skeleton and skin weights");
        var privateProjects=new List<FighterProject>();
        for(int ai=0;ai<args.Length;ai++)if(args[ai]=="--private")
        {
            string model=args[ai+1],motion=args[ai+2];var catalogue=await adapter.RunAsync(new("import",model,motion,null,null,[],[]));var allGame=catalogue.Project!;
            NativeMaterialChecks.Run(allGame,Check);
            privateProjects.Add(allGame);
            await NativeContainerChecks.RunAsync(adapter,allGame,nd,Check,Near);
            var gameBones=allGame.Scene.Bones.Select(b=>b.Name).ToHashSet();
            int MovingChannels(AnimationClip clip)=>clip.Tracks.Where(t=>gameBones.Contains(t.Bone)).SelectMany(t=>t.Channels.OfType<Curve>()).Count(c=>c.Keys.Any(k=>Math.Abs(k.Value-c.Keys[0].Value)>.01f));
            var moving=allGame.Clips.Where(c=>c.SampleCount>=24).OrderByDescending(MovingChannels).First();Check(MovingChannels(moving)>3,$"Private fixture selects actual multi-bone character motion: {moving.Name}");
            var privateResponse=await adapter.RunAsync(new("import",model,motion,moving.Source!.Resource.Entry,null,[],times));var game=privateResponse.Project!;
            Near(Compare(game,privateResponse.ExpectedVertices!),0,.025f,"Private native model + separate motion evaluated against BrawlLib");
            var firstPose=AnimationService.Evaluate(game.Scene,game.Clips[0],0);var laterPose=AnimationService.Evaluate(game.Scene,game.Clips[0],15.75);Check(game.Scene.Meshes.SelectMany(m=>m.Vertices).Any(v=>Vector3.Distance(AnimationService.Skin(v,firstPose),AnimationService.Skin(v,laterPose))>.1f),"Private verification samples a genuinely deforming character mesh");
            Check(game.NativeResources.Length>0,"Native archive identities retained alongside editable scene");
            var gameHashes=new[]{ProjectStore.Hash(model),ProjectStore.Hash(motion)};
            var gameEdit=new EditorDocument(game);gameEdit.Seek(7);var rootPose=gameEdit.Evaluate().Local[0];gameEdit.KeyTransform(0,rootPose with{Translation=rootPose.Translation+new Vector3(.35f,0,0)});
            var privateOutput=Path.Combine(nd,Path.GetFileNameWithoutExtension(model)+"-edited.chr0");
            var nativeGame=await adapter.ExportAsync(gameEdit.Project,gameEdit.Clip,privateOutput);
            var verify=await adapter.RunAsync(new("import",model,privateOutput,null,null,[],times));
            Near(Compare(verify.Project!,verify.ExpectedVertices!),0,.025f,"Private edited CHR0 fresh native skinning agrees with owned evaluator");
            var exportedPose=AnimationService.Evaluate(verify.Project!.Scene,verify.Project.Clips[0],7);Near(exportedPose.Local[0].Translation.X,rootPose.Translation.X+.35f,.001f,"Private actual native edited key survives fresh process");
            Check(gameHashes.SequenceEqual(new[]{ProjectStore.Hash(model),ProjectStore.Hash(motion)}),"Private model/motion hashes unchanged");
            var controlClip=allGame.Clips.Single(c=>c.Name.StartsWith("Wait1 [",StringComparison.Ordinal));
            var controlResponse=await adapter.RunAsync(new("import",model,motion,controlClip.Source!.Resource.Entry,null,[],[]));
            var nativeControls=await NativeControlChecks.RunAsync(adapter,controlResponse.Project!,model,motion,nd,Check,Near);
            var scaledPose=AnimationService.Evaluate(game.Scene,game.Clips[0],12);
            foreach(var chain in nativeControls.Chains)
            {
                bool nonuniform=false;for(int i=chain.Effector;i>=0;i=game.Scene.Bones[i].Parent){var scale=scaledPose.Local[i].Scale;nonuniform|=Math.Abs(scale.X-scale.Y)>.0001f||Math.Abs(scale.Y-scale.Z)>.0001f;}
                if(nonuniform){var declined=AnimationControls.Solve(game.Scene,scaledPose,nativeControls,chain,scaledPose.World[chain.Effector].Translation,AnimationControls.DefaultPole(game.Scene,scaledPose,chain));Check(declined.Value is null&&declined.Diagnostics.Any(d=>d.Code=="NONUNIFORM_SCALE"),"Native squash/stretch pose reports unsupported IK scale without normalizing channels");break;}
            }
            await ProjectStore.SaveAsync(allGame with{ControlRigs=[nativeControls]},Path.Combine(dir,Path.GetFileNameWithoutExtension(model)+"-"+Path.GetFileNameWithoutExtension(motion)+".fighterport"));
        }
        if(privateProjects.Count>=2)await NativeRetargetChecks.RunAsync(adapter,privateProjects[0],privateProjects[1],nd,Check,Near);
    }
    await File.WriteAllTextAsync(Path.Combine(dir,"results.json"),JsonSerializer.Serialize(new{Passed=true,Count=checks.Count,Checks=checks},new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine($"{checks.Count} meaningful checks passed.");return 0;
}
catch(Exception e){Console.Error.WriteLine(e);await File.WriteAllTextAsync(Path.Combine(dir,"failure.txt"),e.ToString());return 1;}
