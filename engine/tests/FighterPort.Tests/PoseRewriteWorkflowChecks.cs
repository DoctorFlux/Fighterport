using System.Numerics;
using System.Text.Json;
using FighterPort.Core;

internal static class PoseRewriteWorkflowChecks
{
    // These checks exercise the replacement through the public workflow. A
    // pose-only draft is deliberately not a successful contact solve or Apply.
    public static async Task RunAsync(string directory,Action<bool,string> check)
    {
        Directory.CreateDirectory(directory);
        var original=ProceduralFixture.Create();
        var other=ProceduralFixture.Create(1.65f);
        var rig=SurfaceContactChecks.Profile(original.Scene) with{Coordinates=new(1,"Original procedural fixture units")};
        var otherRig=SurfaceContactChecks.Profile(other.Scene) with{Coordinates=new(1,"Original procedural fixture units")};
        var clip=original.Clips[2];
        string input=Path.Combine(directory,"authentic-procedural-source.bytes");
        await File.WriteAllBytesAsync(input,[70,80,83,45,79,82,73,71,73,78,65,76]);
        string inputHash=ProjectStore.Hash(input);
        var modelRef=new NativeResourceRef(input,"model/original",inputHash,"original-procedural-fixture",1);
        var motionRef=modelRef with{Entry="motion/original"};
        var model=new SourceModelRevision(Guid.NewGuid(),original.Scene.Name,modelRef,original.Scene.Bones,"Metres; +Y up; original authored SRT");
        clip=clip with{Source=new(Guid.NewGuid(),motionRef,modelRef,original.Scene.SkeletonRevision,clip.Rate,model.Id,"Independently authored model and motion fixture"){NativeName="Original motion"}};
        var second=clip with{Id=Guid.NewGuid(),Revision=Guid.NewGuid()};
        var source=original with{Clips=[clip,second],ControlRigs=[rig],SourceModels=[model],SourceFiles=[new(input,input,inputHash)],SourceProvider="procedural-fixture.v1",SourceAssociationEvidence="Original authored fixture"};
        var target=other with{Clips=[],ControlRigs=[otherRig],Contacts=[]};
        string sourceBefore=RetargetDependencies.Hash(source),rigBefore=AnimationControls.DeformChecksum(target.Scene);
        var floor=new GroundContactSettings{SurfaceMode=SupportSurfaceMode.Ground,SourceFloor=0,TargetFloor=0,ExplicitSurface=true};
        var automaticOutput=RetargetService.Bake(source,clip,rig,target,otherRig,ground:floor);
        var automatic=Require(automaticOutput,"automatic settings draft");
        var disabled=Require(RetargetService.Bake(source,clip,rig,target,otherRig,useContacts:false,ground:floor,fitting:new(){AutomaticContacts=false}),"disabled contacts draft");
        var noScene=Require(RetargetService.Bake(source,clip,rig,target,otherRig,useContacts:false,fitting:new(){AutomaticContacts=false,AutomaticFitting=false}),"explicit raw transfer draft");
        check(rig.Chains.Length>0&&otherRig.Chains.Length>0&&rig.Chains.Concat(otherRig.Chains).All(c=>!c.Reviewed)&&
            !(rig.VirtualChains??[]).Concat(otherRig.VirtualChains??[]).Any(c=>c.Reviewed),
            "This compatibility fixture retains genuinely unreviewed inferred articulation paths; the test does not grant review metadata to make automatic fitting available");
        check(automaticOutput.Diagnostics.Any(d=>d.Code=="BODY_TRAJECTORY_BINDING_REVIEW"&&!d.Error)&&automatic.Clip.Retarget?.Pipeline?.BodyTrajectory is null,
            "An existing unreviewed rig opens a baseline-only review draft with an explicit missing-articulation capability result instead of failing import or pretending that temporal fitting ran");
        check(Keys(automatic.Clip)==Keys(noScene.Clip)&&automatic.Clip.Retarget?.Pipeline?.SceneBody is {Evaluations:0,AppliedProposals:0}&&
            automatic.Clip.Retarget.Fitting is {AutomaticFitting:true}&&automatic.Clip.Retarget.GroundDetection==floor,
            "Unavailable automatic fitting returns exactly the raw transfer keys with zero scene solves while preserving requested settings and explicit geometry; no independent per-pose solver is substituted");
        check(Keys(automatic.Clip)==Keys(disabled.Clip)&&automatic.Contacts.Length==0&&disabled.Contacts.Length==0,"Changing only saved/use-contact preferences cannot change body/scene keys under identical supplied geometry while contacts remain disabled");
        check(automatic.Clip.Retarget?.SolverVersion==RetargetPipeline.Version&&automaticOutput.Diagnostics.Any(d=>d.Code=="RETARGET_BACKEND"&&d.Message.Contains(RetargetPipeline.Version,StringComparison.Ordinal)),"The public retarget entry and backend diagnostic record the actual current body/reach/trajectory rewrite version");
        check(automatic.Clip.Retarget?.Pipeline?.Trajectory is null&&automatic.Clip.Retarget?.Pipeline?.Stages.All(s=>s.Stage is not ("source interactions" or "target plan" or "curve trajectory"))==true,"The replacement workflow does not run the removed interaction/planner/trajectory pipeline");
        check(automatic.Contacts.Length==0&&automatic.Grounding is {Version:"explicit-context-only-1",ExplicitSurface:true,DetectedIntervals:0,Regions.Length:0,Phases.Length:0,BodySupportPhases.Length:0,BodySupportedThroughout:false,BodySupportedAtClosing:false}&&noScene.Grounding is null,"Explicit floor context retains geometry without inventing contacts, phases or validated body support; raw transfer with absent context invents no floor");
        check(automaticOutput.Diagnostics.Any(d=>d.Code=="RETARGET_CONTACTS_DISABLED_REVIEW")&&automaticOutput.Diagnostics.Any(d=>d.Code=="RETARGET_POSE_REWRITE_REVIEW"),"Requested contact fitting and unaccepted pose quality have explicit independent review diagnostics");
        check(automaticOutput.Diagnostics.Any(d=>d.Code=="JOINT_LIMITS_REVIEW")&&automaticOutput.Diagnostics.Any(d=>d.Code=="RETARGET_COLLISION_REVIEW"),"Unrated joint limits and whole-body collisions keep explicit independent review gates instead of inheriting a floor or representation pass");
        var pendingClip=clip with{Source=clip.Source! with{AssociationEvidence="Pending model/motion association"}};
        var pendingSource=source with{Clips=[pendingClip],LibraryRegistrationId=null,SourceAssociationEvidence="Pending review"};
        var pendingOutput=RetargetService.Bake(pendingSource,pendingClip,rig,target,otherRig,useContacts:false,fitting:new(){AutomaticContacts=false});
        check(pendingOutput.Value is not null&&pendingOutput.Diagnostics.Any(d=>d.Code=="SOURCE_ASSOCIATION_REVIEW"),"An unresolved authentic model/motion association survives reconstruction as a precise source-review gate");
        var emptyFrame=new ContactFrame(Guid.NewGuid(),clip.Id,0,rig.SkeletonFingerprint,[]){EndFrame=clip.SampleCount,EndExclusive=true};
        var freeSource=source with{Contacts=[emptyFrame],MotionCoverage=[new(clip.Revision,0,clip.DurationSeconds,false,"Independent fully free-motion label",true)]};
        var emptyOutput=RetargetService.Bake(freeSource,clip,rig,target,otherRig,useContacts:false,fitting:new(){AutomaticContacts=false});
        check(emptyOutput.Value?.Source.MotionAnalysis?.Interactions.Coverage==MotionContactCoverage.ConfirmedFree&&
              emptyOutput.Value.Source.Contacts.Single()==emptyFrame&&!emptyOutput.Diagnostics.Any(d=>d.Code=="AUTHORED_CONSTRAINTS_NOT_SOLVED"),
            "Empty legacy contact frames retain provenance without inventing required constraints or contradicting independently reviewed free motion");
        var changedSourceFloor=Require(RetargetService.Bake(source,clip,rig,target,otherRig,ground:floor with{SourceFloor=1}),"changed source context draft");
        var changedTargetFloor=Require(RetargetService.Bake(source,clip,rig,target,otherRig,ground:floor with{TargetFloor=1}),"changed target context draft");
        check(automatic.Clip.Retarget!.SourceAnalysisKey!=changedSourceFloor.Clip.Retarget!.SourceAnalysisKey&&
              automatic.Clip.Retarget.SourceAnalysisKey==changedTargetFloor.Clip.Retarget!.SourceAnalysisKey&&
              changedSourceFloor.Source.MotionAnalysis?.SourceSupportContext is {SourceFloor:1,TargetFloor:null},
            "Source-evidence identity includes supplied source geometry independently of target context; target scene changes cannot rewrite authentic source analysis");
        var validation=RetargetAcceptance.Measure(target,otherRig,automatic);
        check(!validation.Passed&&!RetargetAcceptance.Ready(validation,automaticOutput.Diagnostics,automaticOutput.LostChannels,true)&&automatic.Clip.Retarget?.IsDraft==true,"Unknown empty contact coverage remains a draft rather than an Apply-ready zero-error pass");
        // Original authored dimensions put the soles at 1.05-.10-.46-.43
        // -.015-.075=-.03. This motion changes the arms/torso, not the legs.
        var rawWithContext=Require(RetargetService.Bake(source,clip,rig,target,otherRig,useContacts:false,ground:floor,fitting:new(){AutomaticContacts=false,AutomaticFitting=false}),"measured raw transfer draft");
        var rawValidation=RetargetAcceptance.Measure(target,otherRig,rawWithContext);
        check(rawValidation.Measured&&rawValidation.GroundSamples>0&&Math.Abs(rawValidation.Penetration-.03f)<.00005f&&!rawValidation.BodySupportMeasured,"Explicit raw-transfer floor validation independently measures the authored 0.03-unit sole penetration without inventing a plant or body-support interval");
        var syntheticPass=new ContactValidation(RetargetAcceptance.Version,true,true,.0001f,.0001f,0,0,0,0,0,0,1,1,1);
        foreach(string code in new[]{"RETARGET_CONTACTS_DISABLED_REVIEW","RETARGET_POSE_REWRITE_REVIEW","AUTHORED_CONSTRAINTS_NOT_SOLVED","RETARGET_RECONSTRUCTION_REVIEW","BODY_REACH_REVIEW","BODY_REACH_TOPOLOGY_REVIEW","BODY_TRAJECTORY_BINDING_REVIEW"})
            check(!RetargetAcceptance.Ready(syntheticPass,[new(code,"Independent rewrite capability blocker",false)],[],true),"Even a numerical contact pass cannot bypass "+code);

        var reverseSource=target with{Clips=[clip with{Source=null}]};
        var reverseTarget=source with{Clips=[],Contacts=[]};
        var reverseClip=reverseSource.Clips[0];
        var reverseAuto=Require(RetargetService.Bake(reverseSource,reverseClip,otherRig,reverseTarget,rig,ground:floor),"reverse automatic draft");
        var reverseOff=Require(RetargetService.Bake(reverseSource,reverseClip,otherRig,reverseTarget,rig,useContacts:false,ground:floor,fitting:new(){AutomaticContacts=false}),"reverse disabled draft");
        check(Keys(reverseAuto.Clip)==Keys(reverseOff.Clip),"Both proportion directions independently ignore disabled automatic-contact numerics");

        var pin=new ContactPin(Guid.NewGuid(),"Reviewed exact support",rig.Chains[0].Effector,Vector3.Zero,new(90,70,-40),Quaternion.Identity,true,"Independently authored fixed socket"){RequiredSupport=true};
        var authored=new ContactFrame(Guid.NewGuid(),clip.Id,0,rig.SkeletonFingerprint,[pin]){EndFrame=clip.SampleCount,EndExclusive=true};
        var lockedSource=source with{Contacts=[authored]};
        string lockBefore=RetargetDependencies.Hash(lockedSource);
        var lockedOutput=RetargetService.Bake(lockedSource,clip,rig,target,otherRig,ground:floor);
        var locked=Require(lockedOutput,"authored-pin draft");
        check(lockedOutput.Diagnostics.Any(d=>d.Code=="AUTHORED_CONSTRAINTS_NOT_SOLVED")&&lockedOutput.Diagnostics.Any(d=>d.Code=="RETARGET_CONTACTS_DISABLED_REVIEW"),"An authored fixed pin reports that its constraint was not solved rather than being silently released");
        check(RetargetDependencies.Hash(locked.Source.Contacts)==RetargetDependencies.Hash(lockedSource.Contacts)&&locked.Source.Contacts.Single().Pins.Single().Position==pin.Position,"The immutable source snapshot preserves exact authored pin identity, anchor, interval and rotation policy");
        check(locked.Contacts.Length==0&&Keys(locked.Clip)==Keys(disabled.Clip),"Unsupported authored constraints do not become invented target locks or distort the prepared pose");
        check(RetargetDependencies.Hash(lockedSource)==lockBefore,"Preparing a draft leaves authored source constraints unchanged");
        var edit=new ContactIntervalEdit("Foot.L",0,clip.DurationSeconds,ContactTimeDomain.SourceSeconds,ContactEditAction.ForceContact);
        var support=new RetargetSupportBinding(Guid.NewGuid(),new(Guid.NewGuid(),"Reviewed fixed support","Independently supplied object frame"){GeometryKnown=true,Motion=[new(0,new(3,2,1),Quaternion.Identity)]},ContactTimeDomain.TargetSeconds,"Explicit support binding retained for a future capable solve");
        var requestedOutput=RetargetService.Bake(source,clip,rig,target,otherRig,useContacts:false,contactEdits:[edit],supportBindings:[support]);
        var requested=Require(requestedOutput,"explicit support policy draft");
        check(requestedOutput.Diagnostics.Any(d=>d.Code=="AUTHORED_CONSTRAINTS_NOT_SOLVED")&&requested.Contacts.Length==0&&Keys(requested.Clip)==Keys(disabled.Clip),"Explicit contact/scene policies are reported as unsolved and never silently executed by disabled contact fitting");
        check(requested.Clip.Retarget?.ContactEdits.Single()==edit&&requested.Clip.Retarget.SupportBindings.Single()==support,"The ordinary draft recipe retains requested contact intervals and fixed support identity for later reviewed solving");

        // Low-level composition is used only to persist an explicitly draft
        // project. The selected-batch Apply path below must reject it.
        var composed=RetargetService.Apply(target,locked) with{FittingProfiles=[new("requested-on","Saved requested contact policy",new(){AutomaticContacts=true},RootMotionPolicy.Preserve),new("requested-off","Saved explicit disabled contact policy",new(){AutomaticContacts=false},RootMotionPolicy.Preserve)]};
        string saved=Path.Combine(directory,"pose-rewrite-draft.fighterport");
        await ProjectStore.SaveAsync(composed,saved);
        var reopened=await ProjectStore.LoadAsync(saved);
        var savedClip=reopened.Clips.Single();var snapshot=reopened.LibrarySources.Single(s=>s.Id==savedClip.SourceSnapshot);
        check(savedClip.Retarget?.IsDraft==true&&savedClip.Retarget.SolverVersion==RetargetPipeline.Version&&savedClip.Retarget.SourceRevision==clip.Revision,"Save/reopen preserves draft status, replacement version and authentic source revision ancestry");
        check(Keys(snapshot.Clip)==Keys(clip)&&snapshot.Clip.Id==clip.Id&&snapshot.Clip.Revision==clip.Revision&&snapshot.Models.Single().Resource==modelRef&&snapshot.SourceFiles.Single().Sha256==inputHash,"Saved ancestry retains exact source keys, model identity and immutable input proofs");
        check(RetargetDependencies.Hash(snapshot.Contacts)==RetargetDependencies.Hash(lockedSource.Contacts)&&AnimationControls.DeformChecksum(snapshot.Scene)==AnimationControls.DeformChecksum(source.Scene),"Pooled snapshot persistence preserves authored constraints and the full authentic native-referencing rig/skin");
        check(snapshot.Controls.Chains.All(c=>!c.Reviewed)&&RetargetDependencies.Hash(snapshot.Controls)==RetargetDependencies.Hash(rig),
            "Saving and reopening a baseline-only review preserves the original unreviewed control profile rather than marking inferred articulation as reviewed or replacing the native setup");
        check(AnimationControls.DeformChecksum(reopened.Scene)==rigBefore&&ProjectStore.Hash(input)==inputHash,"Draft persistence changes neither target deform data nor original source bytes");
        check(reopened.FittingProfiles.Single(p=>p.Key=="requested-on").Settings.AutomaticContacts&&!reopened.FittingProfiles.Single(p=>p.Key=="requested-off").Settings.AutomaticContacts,"Disabled functionality preserves distinct saved true/false requested policies rather than rewriting existing profiles");

        var document=new EditorDocument(target);
        using(var batch=new RetargetBatch(document,source,rig,otherRig,[clip.Id,second.Id],ground:floor))
        {
            await batch.RunAsync();
            check(batch.Rows.All(r=>r.Bake is not null&&r.State is RetargetRowState.Review or RetargetRowState.Unresolved)&&batch.Rows.All(r=>r.Validation?.Passed!=true),"Same-source selected clips prepare independently and remain explicitly unvalidated review rows");
            check(batch.Rows.All(r=>Keys(r.Bake!.Clip)==Keys(automatic.Clip)),"Single and selected-batch paths produce identical ordinary pose keys under identical settings");
            check(batch.Rows.Select(r=>r.TargetName).Distinct(StringComparer.OrdinalIgnoreCase).Count()==2&&batch.Rows[0].Source.Id!=batch.Rows[1].Source.Id,"Same display/native names retain separate stable source identities and collision-free target names");
            batch.SetOverride(second.Id,new(){AutomaticContacts=false});
            check(batch.Rows[0].State is RetargetRowState.Review or RetargetRowState.Unresolved&&batch.Rows[1].State==RetargetRowState.Stale,"One saved requested-policy override invalidates only its own selected row");
            await batch.RunAsync();
            check(batch.Rows[1].Settings.AutomaticContacts==false&&Keys(batch.Rows[1].Bake!.Clip)==Keys(automatic.Clip),"Repreparing an explicit disabled-contact override preserves the same pose rather than invoking legacy fitting");
            bool rejected=false;try{batch.Apply();}catch(InvalidOperationException){rejected=true;}
            bool subsetRejected=false;try{batch.Apply(true);}catch(InvalidOperationException){subsetRejected=true;}
            check(rejected&&subsetRejected&&ReferenceEquals(document.Project,target)&&!document.CanUndo,"Neither complete nor passed-subset Apply can commit pose-only review drafts or create Undo entries");
            await File.WriteAllTextAsync(Path.Combine(directory,"selected-batch-receipt.json"),JsonSerializer.Serialize(batch.Receipt(),ProjectStore.Json));
            string batchSaved=Path.Combine(directory,"pose-rewrite-batch-settings.fighterport");
            await ProjectStore.SaveAsync(composed with{RetargetBatches=[batch.Receipt()]},batchSaved);
            var batchReopened=await ProjectStore.LoadAsync(batchSaved);
            var rows=batchReopened.RetargetBatches.Single().Rows;
            check(rows.Single(r=>r.SourceClip==clip.Id).Overrides.AutomaticContacts is null&&rows.Single(r=>r.SourceClip==second.Id).Overrides.AutomaticContacts==false,"Saved batch receipts distinguish inherited contact policy from an explicit false per-clip override after restart");
        }

        var staleDocument=new EditorDocument(target);
        using(var stale=new RetargetBatch(staleDocument,source,rig,otherRig,[clip.Id],ground:floor))
        {
            bool edited=false;
            stale.Changed+=()=>{if(!edited&&stale.Rows[0].State==RetargetRowState.Solving){edited=true;staleDocument.Transaction("Concurrent target edit",p=>p with{Name="Changed while preparing"});staleDocument.Undo();}};
            await stale.RunAsync();
            check(edited&&stale.Rows[0].State==RetargetRowState.Stale&&stale.Rows[0].Bake is null,"Target edit followed by Undo rejects the obsolete pose result through monotonic dependency ownership");
        }
        var cancelledDocument=new EditorDocument(target);
        using(var cancelled=new RetargetBatch(cancelledDocument,source,rig,otherRig,[clip.Id]))
        {
            cancelled.Changed+=()=>{if(cancelled.Rows[0].State==RetargetRowState.Solving)cancelled.Cancel();};
            try{await cancelled.RunAsync();}catch(OperationCanceledException){}
            check(ReferenceEquals(cancelledDocument.Project,target)&&cancelled.Rows[0].State==RetargetRowState.Cancelled&&cancelled.Rows[0].Bake is null,"Cancellation cannot publish or commit an obsolete prepared pose");
        }
        var sourceChangedDocument=new EditorDocument(target);bool currentSource=true;
        using(var changed=new RetargetBatch(sourceChangedDocument,source,rig,otherRig,[clip.Id],sourceCurrent:()=>currentSource))
        {
            changed.Changed+=()=>{if(changed.Rows[0].State==RetargetRowState.Solving)currentSource=false;};
            await changed.RunAsync();
            check(changed.Rows[0].State==RetargetRowState.Stale&&changed.Rows[0].Bake is null&&ReferenceEquals(sourceChangedDocument.Project,target),"Source re-registration invalidates late pose publication without replacing the editable target");
        }

        var editable=new EditorDocument(composed);editable.Seek(3.375);var prior=editable.Project;
        var local=editable.Evaluate().Local[1];editable.KeyTransform(1,local with{Rotation=local.Rotation+new Vector3(0,7,0)});
        check(editable.Dirty&&editable.Clip.SampleCount==clip.SampleCount&&editable.Clip.Tracks.Single(t=>t.Bone==target.Scene.Bones[1].Name).Channels[4]!.Keys.Count(k=>k.Frame==3.375)==1,"An ordinary replacement draft remains editable through existing FK fractional-frame key creation");
        var keyed=editable.Project;editable.Undo();check(ReferenceEquals(editable.Project,prior),"One FK Undo restores complete previous keys and recipe state");editable.Redo();check(ReferenceEquals(editable.Project,keyed),"FK Redo restores the stored editable motion without rerunning retargeting");
        var poseOnly=target with{Clips=[]};var independent=new EditorDocument(poseOnly);var rest=independent.Evaluate();var chain=otherRig.Chains[0];
        var goal=rest.World[chain.Effector].Translation+Vector3.Normalize(rest.World[chain.Root].Translation-rest.World[chain.Effector].Translation)*.04f+Vector3.UnitZ*.025f;
        var ik=AnimationControls.Solve(target.Scene,rest,otherRig,chain,goal,chain.PoleDirection);
        check(ik.Value is not null&&ik.Value.Error<.001f&&ik.Value.Transforms.Count>0,"Independent existing-bone IK still solves an ordinary authoring pose without a donor animation");
        if(ik.Value is {} solved)independent.PreviewTransforms(solved.Transforms);
        check(independent.Project.Clips.Length==0&&independent.PoseOverrides.Count>0&&AnimationControls.DeformChecksum(independent.Project.Scene)==rigBefore,"Model-only IK preview creates no fake motion and changes no skeleton, binds or weights");
        check(RetargetDependencies.Hash(source)==sourceBefore&&AnimationControls.DeformChecksum(target.Scene)==rigBefore&&ProjectStore.Hash(input)==inputHash,"Pose rewrite, batch guards, persistence and independent controls retain all immutable source/target proofs");
        await File.WriteAllTextAsync(Path.Combine(directory,"pose-rewrite-scope.json"),JsonSerializer.Serialize(new{Pipeline=RetargetPipeline.Version,SourceProof=inputHash,TargetDeform=rigBefore,Automatic=automaticOutput.Diagnostics,Authored=lockedOutput.Diagnostics,ContactValidation=validation,Claim="Pose workflow and safety only. No contact, artistic, native or game success inferred."},new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false}));
    }
    static RetargetBake Require(OperationResult<RetargetBake> result,string stage)=>result.Value??throw new InvalidOperationException(stage+": "+string.Join("; ",result.Diagnostics.Select(d=>d.Code+": "+d.Message)));
    static string Keys(AnimationClip clip)=>RetargetDependencies.Hash(clip.Tracks);
}
