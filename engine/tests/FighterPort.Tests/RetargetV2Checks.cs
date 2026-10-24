using System.Numerics;
using System.Text.Json;
using FighterPort.Core;

internal static class RetargetV2Checks
{
    public static async Task RunAsync(string directory,Action<bool,string> check)
    {
        RigTransferV2Checks.Run(check);
        var original=ProceduralFixture.Create();var profile=SurfaceContactChecks.Profile(original.Scene) with{Coordinates=new(1,"Independently authored fixture units")};
        var spin=new AnimationClip(Guid.NewGuid(),"Authored two-sample four-turn",2,new(60),false,[new(original.Scene.Bones[0].Name,[null,null,null,null,new([new(0,0,1440,1440),new(1,1440,1440,1440)]),null,null,null,null])]);
        var source=original with{Clips=[spin]};string checksum=AnimationControls.DeformChecksum(source.Scene);var identity=RetargetService.Bake(source,spin,profile,source with{Clips=[]},profile,useContacts:false).Value??throw new Exception("V2 identity failed");
        check(identity.Clip.Retarget?.Backend==RetargetBackendIdentity.CurrentId&&identity.Clip.Retarget.BackendSchema==2,"V2 identity goes through the selected backend and stores its real versioned identity");
        check(new[]{0d,.125,1d/3,.8,1d}.All(f=>AnimationService.Evaluate(source.Scene,spin,f).World.SequenceEqual(AnimationService.Evaluate(source.Scene,identity.Clip,f).World))&&identity.Clip.Tracks[0].Channels[4]!.Keys[^1].Value==1440,"Same-rig V2 transfer preserves exact native curves, tangents and four intentional turns without resampling");
        check(identity.Clip.Id!=spin.Id&&identity.Clip.Revision!=spin.Revision&&!ReferenceEquals(identity.Clip.Tracks[0].Channels[4]!.Keys,spin.Tracks[0].Channels[4]!.Keys),"Identity reuse creates independent target IDs and key arrays while retaining authentic source ancestry");
        var rest=new AnimationClip(Guid.NewGuid(),"Authored rest",5,new(60),false,[]);var restSource=original with{Clips=[rest]};var other=ProceduralFixture.Create(1.4f);var otherRig=SurfaceContactChecks.Profile(other.Scene) with{Coordinates=new(1,"Authored target units")};
        foreach(var (donor,rig,target,targetRig) in new[]{(restSource,profile,other,otherRig),(other with{Clips=[rest]},otherRig,original,profile)})
        {
            var transferred=RetargetService.Bake(donor,rest,rig,target,targetRig,useContacts:false);check(transferred.Value is not null,"V2 proportion transfer is executable in each independent source/target direction: "+string.Join("; ",transferred.Diagnostics.Where(d=>d.Error).Select(d=>d.Message)));
            var actual=AnimationService.Evaluate(target.Scene,transferred.Value!.Clip,2.375);var expected=AnimationService.Evaluate(target.Scene,null,0);
            check(actual.World.Zip(expected.World).All(p=>Vector3.Distance(p.First.Translation,p.Second.Translation)<.00001f&&PoseGeometry.RotationDifference(PoseGeometry.Rotation(p.First),PoseGeometry.Rotation(p.Second))<.001f),"Source rest maps to the target's unchanged native rest with unequal proportions");
        }
        var partial=Chain(false);var unequal=Chain(true);var a=partial.ControlRigs.Single();var b=unequal.ControlRigs.Single();
        var analysisRequest=new RetargetRequest(partial,partial.Clips[0],a,unequal,b,RootMotionPolicy.Preserve,true,default,null,null,null,null,null,null,null);
        var authenticAnalysis=SourceMotionAnalysis.Analyze(analysisRequest);
        check(authenticAnalysis.Revision==SourceMotionAnalysis.Analyze(analysisRequest with{UseContacts=false,Fitting=new(){AutomaticContacts=false,AutomaticFitting=false}}).Revision,"Target contact preferences do not rewrite authentic source interaction evidence");
        var gap=partial with{Contacts=[partial.Contacts[0] with{EndFrame=2}]};
        check(!SourceMotionAnalysis.Analyze(analysisRequest with{Source=gap}).CoverageReviewed,"One short detected contact cannot satisfy an independently labeled full required-contact interval");
        check(RigDescriptor.Prepare(unequal.Scene,b).Binding.Chains.Single().Bones.Length==5,"Rig preparation retains a five-bone virtual chain without requiring two legs or three adjacent joints");
        string beforeSource=RetargetDependencies.Hash(partial),beforeTarget=AnimationControls.DeformChecksum(unequal.Scene);
        var output=RetargetService.Bake(partial,partial.Clips[0],a,unequal,b,ground:null);
        check(output.Value is not null,"New temporal solver fits the reviewed fixed contact on an unequal five-bone chain: "+string.Join("; ",output.Diagnostics.Where(d=>d.Error).Select(d=>d.Message)));
        var bake=output.Value!;var validation=RetargetAcceptance.Measure(unequal,b,bake);
        await File.WriteAllTextAsync(Path.Combine(directory,"initial-chain-validation.json"),JsonSerializer.Serialize(new{validation,output.Diagnostics,Receipt=bake.Clip.Retarget?.Pipeline?.Trajectory},new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false,WriteIndented=true}));
        check(validation.Passed&&validation.RequiredContactSamples>0&&validation.Separation<=.0001f,"Independent baked fractional samples meet the existing required-contact bound on a non-humanoid partial rig");
        var wall=partial with{Contacts=[partial.Contacts[0] with{Pins=[partial.Contacts[0].Pins[0] with{Clearance=new(Vector3.UnitX,3,partial.Scene.Meshes.SelectMany(m=>m.Vertices).Select(v=>new SkinSurfacePoint(v.Position,v.Joints,v.Weights)).ToArray())}]}]};
        var wallBake=RetargetService.Bake(wall,wall.Clips[0],a,unequal,b);
        check(wallBake.Value is not null&&RetargetAcceptance.Measure(unequal,b,wallBake.Value).Passed,"Explicit wall clearance uses its actual reviewed X normal rather than inventing a Y floor at the same height");
        var repeated=RetargetService.Bake(partial,partial.Clips[0],a,unequal,b).Value!;
        check(new[]{0d,.2,1d/3,3.375,6.8,7d}.All(f=>AnimationService.Evaluate(unequal.Scene,bake.Clip,f).World.SequenceEqual(AnimationService.Evaluate(unequal.Scene,repeated.Clip,f).World)),"Repeated V2 solving is deterministic; arbitrary scrubbing samples prepared curves only");
        var changing=Chain(false,delayedContact:true);var moving=RetargetService.Bake(changing,changing.Clips[0],changing.ControlRigs[0],unequal,b);
        check(moving.Value is not null,"A release-to-fixed-contact action runs through the coupled solver: "+string.Join("; ",moving.Diagnostics.Where(d=>d.Error).Select(d=>d.Message)));
        var trajectory=moving.Value!.Clip.Retarget!.Pipeline!.Trajectory!;
        await File.WriteAllTextAsync(Path.Combine(directory,"moving-trajectory.json"),JsonSerializer.Serialize(new{trajectory,moving.Diagnostics,Clip=moving.Value.Clip,Contacts=moving.Value.Contacts},new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false,WriteIndented=true}));
        check(trajectory.Action is {AcceptedUpdates:>0}&&trajectory.ActionCostAfterActionPass<trajectory.ActionCostBeforeActionPass-1e-7,"Connected coefficient action pass reduces independently evaluated full-trajectory geometric cost before the separate required-constraint restoration");
        check(trajectory.MaximumNormalizedConstraint<=1,"The coupled curve solve cannot buy a smoother correction by weakening required contact constraints");
        var preparation=analysisRequest with{Source=changing,Clip=changing.Clips[0]};var sourceDescriptor=RigDescriptor.Prepare(changing.Scene,a);var targetDescriptor=RigDescriptor.Prepare(unequal.Scene,b);
        var preparedPlan=TargetMotionPlan.Build(preparation,sourceDescriptor,targetDescriptor,new(preparation,sourceDescriptor,targetDescriptor),SourceMotionAnalysis.Analyze(preparation));
        int minimumWork=4*((preparedPlan.Samples-(preparedPlan.Closing?0:1))*4+1);var limited=RetargetTrajectorySolver.Solve(preparedPlan,false,evaluationBudget:minimumWork+1).Value!;
        check(limited.Receipt.BudgetExhausted&&limited.Receipt.PoseEvaluations<=minimumWork+1&&limited.Tracks.All(t=>t.Channels.All(c=>c is not null&&c.Keys.Length==8)),"Trajectory refinement respects its declared work budget while retaining an ordinary independent draft with unchanged timing");
        check(!RetargetAcceptance.Ready(validation,[new("RETARGET_NATIVE_FIT_BUDGET_REVIEW","Explicit exhausted native-fit budget",false)],[]),"Exhausted native refinement cannot earn Apply-ready status from passing contact samples alone");
        check(!RetargetAcceptance.Ready(validation,[new("RETARGET_TRAJECTORY_BUDGET_REVIEW","Explicit exhausted trajectory budget",false)],[]),"Exhausted trajectory optimization cannot earn Apply-ready status from passing contact samples alone");
        bool cancelled=false;using(var stop=new CancellationTokenSource()){stop.Cancel();try{RetargetTrajectorySolver.Solve(preparedPlan,false,stop.Token);}catch(OperationCanceledException){cancelled=true;}}
        check(cancelled,"Native fitting observes cancellation before preparing further curves");
        var doc=new EditorDocument(unequal);using var batch=new RetargetBatch(doc,partial,a,b,[partial.Clips[0].Id]);await batch.RunAsync();
        await File.WriteAllTextAsync(Path.Combine(directory,"single-trajectory-batch.json"),JsonSerializer.Serialize(batch.Receipt(),new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false,WriteIndented=true}));
        check(batch.Rows[0].State==RetargetRowState.Validated&&batch.Receipt().Backend==RetargetBackendIdentity.CurrentId,"The actual selected-batch workflow validates V2 output and reports its selected backend");batch.Apply();
        check(doc.Project.Clips.Length==1&&doc.Project.Clips[0].Retarget?.Backend==RetargetBackendIdentity.CurrentId,"V2 Apply commits ordinary editable native-bone keys through the shared document command");doc.Undo();check(doc.Project.Clips.Length==0,"One Undo restores the complete pre-V2 target");doc.Redo();check(doc.Project.Clips[0].Id==batch.Rows[0].Bake!.Clip.Id,"Redo restores stored V2 output without rerunning the solver");
        string file=Path.Combine(directory,"retarget-v2-persisted.fighterport");await ProjectStore.SaveAsync(doc.Project,file);var reopened=await ProjectStore.LoadAsync(file);
        check(reopened.Clips[0].Retarget?.BackendSchema==2&&reopened.Clips[0].Retarget?.Pipeline?.Trajectory?.EvaluationBudget==NativeCurveBaker.DefaultBudget(unequal.Scene.Bones.Length)&&reopened.Clips[0].Retarget?.Pipeline?.Trajectory?.FreeChannelRecovery is not null&&reopened.LibrarySources[0].MotionAnalysis?.Interactions.Coverage==MotionContactCoverage.Required&&reopened.ControlRigs[0].VirtualChains?.Single().Bones.Length==5,"Project save/reopen retains normal trajectory identity, total bounded-work and free-channel receipts, source interaction evidence and native-referencing virtual chains");
        var oldRecipe=JsonSerializer.Deserialize<RetargetRecipe>("{\"SourceRevision\":\""+Guid.NewGuid()+"\",\"TargetSkeletonRevision\":\""+Guid.NewGuid()+"\",\"SourceRig\":\""+Guid.NewGuid()+"\",\"TargetRig\":\""+Guid.NewGuid()+"\",\"RootPolicy\":0,\"SolverVersion\":\"historical-bake6\"}",ProjectStore.Json)!;
        check(oldRecipe.Backend==RetargetBackendIdentity.HistoricalId&&oldRecipe.SolverVersion=="historical-bake6"&&oldRecipe.BackendSchema==1,"Legacy project migration identifies the legacy family without relabeling its historical solver version as V2");
        var empty=partial with{Contacts=[],MotionCoverage=[]};var unknown=RetargetService.Bake(empty,empty.Clips[0],a,unequal,b).Value!;check(!RetargetAcceptance.Measure(unequal,b,unknown).Passed,"Empty V2 detection remains unknown coverage and cannot earn a clean zero-error contact pass");
        var free=empty with{MotionCoverage=[new(empty.Clips[0].Revision,0,empty.Clips[0].DurationSeconds,false,"Independently authored free chain motion with no support",true)]};var freeBake=RetargetService.Bake(free,free.Clips[0],a,unequal,b).Value!;
        check(RetargetAcceptance.Measure(unequal,b,freeBake).Applicability==ContactApplicability.NotApplicable&&freeBake.Contacts.Length==0,"Reviewed full free-motion coverage produces NotApplicable contacts without fabricating a ground lock");
        var reflected=partial.Scene with{Bones=partial.Scene.Bones.Select((bone,i)=>i==1?bone with{Rest=bone.Rest with{Scale=new(-1,1,1)}}:bone).ToArray()};var reflectedSource=partial with{Scene=reflected};var reflectedRig=a with{SkeletonFingerprint=AnimationControls.Fingerprint(reflected)};
        var unsupported=RetargetService.Bake(reflectedSource,partial.Clips[0],reflectedRig,unequal,b);check(unsupported.Value is null&&unsupported.Diagnostics.Any(d=>d.Code=="RETARGET_UNSUPPORTED")&&unsupported.Diagnostics.Any(d=>d.Code=="RETARGET_BACKEND"),"Reflected native rest scales report an explicit V2 capability failure without normalizing binds or falling back");
        check(beforeSource==RetargetDependencies.Hash(partial)&&beforeTarget==AnimationControls.DeformChecksum(unequal.Scene)&&checksum==AnimationControls.DeformChecksum(source.Scene),"V2 transfer, optimization, Apply, Undo and persistence preserve authentic source data and target deform checksums");
        var second=partial.Clips[0] with{Id=Guid.NewGuid(),Revision=Guid.NewGuid()};var bad=second with{Id=Guid.NewGuid(),Revision=Guid.NewGuid(),Name="Independently unreachable pin"};
        var multiSource=partial with{Clips=[partial.Clips[0],second,bad],Contacts=partial.Contacts.Concat([partial.Contacts[0] with{Id=Guid.NewGuid(),Clip=second.Id},partial.Contacts[0] with{Id=Guid.NewGuid(),Clip=bad.Id,Pins=[partial.Contacts[0].Pins[0] with{Position=new(1000,1000,1000)}]}]).ToArray(),MotionCoverage=partial.MotionCoverage.Concat([partial.MotionCoverage[0] with{ClipRevision=second.Revision},partial.MotionCoverage[0] with{ClipRevision=bad.Revision}]).ToArray()};
        var multiDoc=new EditorDocument(unequal);using var multi=new RetargetBatch(multiDoc,multiSource,a,b,multiSource.Clips.Select(c=>c.Id));await multi.RunAsync();
        await File.WriteAllTextAsync(Path.Combine(directory,"mixed-trajectory-batch.json"),JsonSerializer.Serialize(multi.Receipt(),new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false,WriteIndented=true}));
        check(multi.Rows.Count(r=>r.State==RetargetRowState.Validated)==2&&multi.Rows[2].State==RetargetRowState.Unresolved,"V2 mixed batch retains two independently validated same-name motions and a precise unresolved row");
        check(multi.Rows.Select(r=>r.TargetName).Distinct(StringComparer.OrdinalIgnoreCase).Count()==3&&multi.Rows[0].Source.Id!=multi.Rows[1].Source.Id,"V2 selected batches distinguish stable IDs and resolve output-name collisions before commit");
        var multiFirst=multi.Rows[0].Bake!.Clip;check(new[]{0d,.2,3.375,7d}.All(f=>AnimationService.Evaluate(unequal.Scene,multiFirst,f).World.SequenceEqual(AnimationService.Evaluate(unequal.Scene,bake.Clip,f).World)),"Same-source selected-batch V2 keys equal the deterministic single-motion result");
        bool blocked=false;try{multi.Apply();}catch(InvalidOperationException){blocked=true;}check(blocked&&multiDoc.Project.Clips.Length==0,"Complete-set V2 Apply refuses mixed failures and leaves the target unchanged");
        multi.Apply(true);check(multiDoc.Project.Clips.Length==2&&multiDoc.Project.RetargetBatches[^1].Rows.Count(r=>r.State==RetargetRowState.Applied)==2,"Explicit V2 passed-subset Apply commits exactly two validated motions and their receipt");
        var savedIds=multiDoc.Project.Clips.Select(c=>c.Id).ToArray();multiDoc.Undo();check(multiDoc.Project.Clips.Length==0,"One Undo restores the complete target before a selected V2 batch");multiDoc.Redo();check(multiDoc.Project.Clips.Select(c=>c.Id).SequenceEqual(savedIds),"V2 batch Redo restores stored independent outputs without solving again");
        await File.WriteAllTextAsync(Path.Combine(directory,"retarget-v2-temporal.json"),JsonSerializer.Serialize(new{Static=validation,Trajectory=trajectory,Backend=RetargetBackendIdentity.CurrentId},new JsonSerializerOptions(ProjectStore.Json){WriteIndented=true,IgnoreReadOnlyProperties=false}));
    }
    internal static FighterProject Chain(bool unequal,bool delayedContact=false)
    {
        var lengths=unequal?new[]{1.8f,.5f,.8f,.6f}:new[]{1f,1f,1f,1f};
        var bones=new List<Bone>{new(unequal?"TargetOrigin":"SourceOrigin",-1,Transform.Identity),new(unequal?"OpaqueBase":"NativeBase",0,Transform.Identity with{Translation=Vector3.UnitY})};
        for(int i=0;i<4;i++)bones.Add(new((unequal?"TargetPart_":"DonorJoint_")+i,i+1,Transform.Identity with{Translation=new(lengths[i],0,0)}));
        var scene=new Scene("Independent five-bone limb",bones.ToArray(),[]);var rest=AnimationService.Evaluate(scene,null,0);var at=rest.World[^1].Translation;
        var vertices=new[]{new Vector3(-.04f,0,0),new(.04f,0,0),new(0,.04f,0)}.Select(p=>new Vertex(at+p,Vector3.UnitZ,Vector2.Zero,new(5,0,0,0),new(1,0,0,0),Vector4.One)).ToArray();scene=scene with{Meshes=[new("Authored tip skin",vertices,[0,1,2])]};
        var angles=delayedContact?new[]{new Key(0,0),new Key(3,30),new Key(7,30)}:new[]{new Key(0,30),new Key(7,30)};
        var clip=new AnimationClip(Guid.NewGuid(),"Authored reach",8,new(60),false,[new(scene.Bones[2].Name,[null,null,null,null,null,new(angles,Interpolation.Linear),null,null,null])]);
        var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[]){Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Authored metre fixture"),VirtualChains=[new("Appendage",[1,2,3,4,5],"Independently authored complete native chain",true)],BoneRoles=Enumerable.Range(0,6).Select(i=>new RigMapping(i==0?"RootMotion":"Part."+i,i,"Reviewed fixture identity",true)).ToArray(),JointLimits=Enumerable.Range(1,5).Select(i=>new NativeJointLimit(i,new(-170),new(170),"Authored mechanical joint range, not source extrema",true)).ToArray()};
        var goal=AnimationService.Evaluate(scene,clip,delayedContact?3:0).World[5].Translation;var contact=new ContactFrame(Guid.NewGuid(),clip.Id,delayedContact?3:0,rig.SkeletonFingerprint,[new(Guid.NewGuid(),"Reviewed tip",5,Vector3.Zero,goal,Quaternion.Identity,false,"Independently specified fixed anchor")]){EndFrame=8,EndExclusive=true};
        return new(1,Guid.NewGuid(),scene.Name,scene,unequal?[]:[clip]){ControlRigs=[rig],Contacts=unequal?[]:[contact],MotionCoverage=unequal?[]:delayedContact?[new(clip.Revision,0,3d/60,false,"Authored free reach before fixed contact",true),new(clip.Revision,3d/60,clip.DurationSeconds,true,"Authored fixed anchor interval",true)]:[new(clip.Revision,0,clip.DurationSeconds,true,"Authored fixed anchor throughout",true)]};
    }
}
