using System.Numerics;
using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class CharacterWorkspaceChecks
{
    public static async Task RunAsync(string output,FighterProject original,Action<bool,string> check,BrawlAdapter? adapter=null)
    {
        string folder=Path.Combine(output,"Character");var workspace=await CharacterStore.CreateAsync("Character",folder);Guid character=workspace.Project.Id;string path=workspace.Path!;
        check(workspace.Document is null&&workspace.Project.Scenes.Length==0&&!workspace.Dirty,"A named empty character saves without a scene, document, model, skeleton or SDK");
        var reopened=await CharacterStore.LoadAsync(path);check(reopened.Project.Id==character&&reopened.Document is null,"The actual empty descriptor reopens with the same project identity");
        var main=workspace.AddTarget("Project+ Main",new("Project+","Wii","Main revision"),"Character replacement","Project+");var practice=workspace.AddTarget("Project+ Practice",new("Project+","Wii","Practice revision"),"Character replacement","Project+");var rivals=workspace.AddTarget("Rivals II",new("Rivals II","Windows","Unresolved"),"Character mod");
        var clip=original.Clips.FirstOrDefault(c=>c.Source?.NativeName=="Wait1")??original.Clips.First();var profile=original.ControlRigs.FirstOrDefault()??new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(original.Scene),[],[]);
        var firstSource=new SourceAssetSnapshot(Guid.NewGuid(),original.Scene,clip,profile,original.SourceModels){Layers=original.NativeLayers,Resources=original.NativeResources,SourceFiles=original.SourceFiles};var secondSource=firstSource with{Id=Guid.NewGuid()};
        var scene=original with{Clips=[clip],Contacts=[],RetargetBatches=[],LibrarySources=[firstSource,secondSource]};string rig=AnimationControls.DeformChecksum(scene.Scene);
        workspace.Activate(null);Guid shared=workspace.AddScene(scene);workspace.AssignIndependentCopy(shared,main);var mainDoc=workspace.Document!;workspace.AssignIndependentCopy(shared,practice);var practiceDoc=workspace.Document!;
        check(!ReferenceEquals(mainDoc,practiceDoc)&&!ReferenceEquals(mainDoc.Project.Clips[0].Tracks,practiceDoc.Project.Clips[0].Tracks),"Project+ Main and Practice own independent document histories and authored key arrays");
        int bone=1;mainDoc.SelectedBone=bone;mainDoc.Seek(0);var before=mainDoc.Project;var local=mainDoc.Evaluate().Local[bone];
        using(var edit=mainDoc.BeginTransformEdit()){var value=local with{Translation=local.Translation+new Vector3(.125f,0,0)};mainDoc.SetPose(bone,value);check(edit.Commit(mainDoc.PoseOverrides.ToDictionary()),"A frame-zero bone edit commits through the ordinary automatic-key gesture");}
        var nativeEdited=mainDoc.Project;var track=mainDoc.Clip.Tracks.Single(t=>t.Bone==scene.Scene.Bones[bone].Name);var previousTrack=before.Clips[0].Tracks.SingleOrDefault(t=>t.Bone==track.Bone);
        check(Enumerable.Range(0,9).Where(c=>c!=6).All(c=>ReferenceEquals(track.Channels[c],previousTrack?.Channels[c])),"Automatic keying preserves every unrelated native transform channel and its curve identity");
        check(ReferenceEquals(mainDoc.Project.NativeLayers,before.NativeLayers)&&ReferenceEquals(practiceDoc.Project,scene)==false&&practiceDoc.Project.Clips[0].Revision==clip.Revision,"Editing one target leaves companion layers and the other target's revision unchanged");
        mainDoc.Undo();check(ReferenceEquals(mainDoc.Project,before),"One Undo restores exact pre-gesture key existence, tangents and project state");mainDoc.Redo();check(ReferenceEquals(mainDoc.Project,nativeEdited),"One Redo restores stored keys without recomputing a gesture");
        for(int repeat=0;repeat<2;repeat++){mainDoc.Seek(.375);using var edit=mainDoc.BeginTransformEdit();var value=mainDoc.Evaluate().Local[bone];mainDoc.SetPose(bone,value with{Translation=value.Translation+new Vector3(0,.05f,0)});edit.Commit(mainDoc.PoseOverrides.ToDictionary());}
        check(mainDoc.Clip.Tracks.Single(t=>t.Bone==track.Bone).Channels[7]!.Keys.Count(k=>k.Frame==.375)==1,"Repeated fractional edits update one existing key instead of duplicating it");
        var unchanged=mainDoc.Project;using(var edit=mainDoc.BeginTransformEdit()){mainDoc.SetPose(bone,mainDoc.Evaluate().Local[bone] with{Translation=new(99)});edit.Cancel();}check(ReferenceEquals(mainDoc.Project,unchanged)&&mainDoc.PoseOverrides.Count==0,"Cancelling a transform restores the preview without authoring keys");
        using(var edit=mainDoc.BeginTransformEdit())check(!edit.Commit(new Dictionary<int,Transform>{{bone,mainDoc.Evaluate().Local[bone]}}),"A no-change gesture creates no key or undo entry");
        var stale=mainDoc.BeginTransformEdit();var oldPose=mainDoc.Evaluate().Local[bone];mainDoc.Seek(1);check(!stale.Commit(new Dictionary<int,Transform>{{bone,oldPose with{Translation=new(99)}}})&&ReferenceEquals(mainDoc.Project,unchanged),"Scrubbing cancels gesture ownership; release cannot write into a later frame");
        var boundaryChannels=new Curve?[9];boundaryChannels[6]=new([new(0,0),new(1,2,3,4),new(1,7,8,9),new(2,10)]);var discontinuity=new EditorDocument(scene with{Clips=[clip with{Tracks=[new(scene.Scene.Bones[bone].Name,boundaryChannels)]}]});discontinuity.SelectedBone=bone;discontinuity.Seek(1);
        using(var edit=discontinuity.BeginTransformEdit()){var value=discontinuity.Evaluate().Local[bone];edit.Commit(new Dictionary<int,Transform>{{bone,value with{Translation=value.Translation+Vector3.UnitX}}});}
        var boundary=discontinuity.Clip.Tracks.Single().Channels[6]!.Keys;check(boundary.Length==4&&boundary[1]==new Key(1,3,3,4)&&boundary[2]==new Key(1,7,8,9),"Editing an existing native discontinuity preserves its outgoing key and exact incoming/outgoing tangents");
        workspace.Activate(main);mainDoc.Seek(3.375);await workspace.SaveAsync();string saved=ProjectStore.Hash(path);var restored=await CharacterStore.LoadAsync(path);
        check(restored.Project.Targets.Length==3&&restored.Project.Targets.Single(t=>t.Id==rivals).SceneIds.Length==0,"Three configurations, including an unresolved non-Brawl target, save without claiming a builder");
        check(restored.Project.Id==character&&restored.Document!.Clock.Frame==3.375&&!restored.Document.Clock.Playing,"Save/reopen restores the exact target and fractional playhead paused");
        check(ReferenceEquals(restored.Document!.Project.LibrarySources[0].Scene,restored.Document.Project.LibrarySources[1].Scene)&&restored.Document.Project.LibrarySources[0].Layers.Length==original.NativeLayers.Length&&restored.Document.Project.LibrarySources[0].Resources.Length==original.NativeResources.Length,"Workspace storage restores pooled full authentic source geometry, layers and resources before evaluation");
        check(AnimationControls.DeformChecksum(restored.Document!.Project.Scene)==rig,"Automatic editing and character persistence preserve native deform/bind/weight data");
        var current=restored.Document;using(var edit=current.BeginTransformEdit()){var value=current.Evaluate().Local[bone];current.SetPose(bone,value with{Translation=value.Translation+Vector3.UnitZ});edit.Commit(current.PoseOverrides.ToDictionary());}
        using(var locked=new FileStream(new ProjectFolders(path).Resolve(".fighterport/write.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)){bool failed=false;try{await restored.SaveAsync();}catch(IOException){failed=true;}check(failed&&restored.Dirty&&ProjectStore.Hash(path)==saved,"A failed save retains the previous coherent descriptor and all live edits");}
        check(!await restored.ResolveDirtyAsync(DirtyDecision.Cancel,()=>throw new Exception("Cancel must not save"))&&restored.Dirty,"Cancel leaves the current dirty workspace intact");
        await restored.SaveAsync();check(!restored.Dirty,"Retry saves the complete revision after a recoverable write failure");
        var older=restored.Snapshot();using(var edit=current.BeginTransformEdit()){var value=current.Evaluate().Local[bone];current.SetPose(bone,value with{Translation=value.Translation+Vector3.UnitZ});edit.Commit(current.PoseOverrides.ToDictionary());}
        await restored.SaveSnapshotAsync(older);check(restored.Dirty&&current.Dirty,"Completion of an older captured save cannot mark newer target edits clean");await restored.SaveAsync();
        bool overlap=false;try{restored.ValidateNativeOutput(new ProjectFolders(path).Resolve("Targets/"+practice.ToString("N")+"/Native/output.chr0"));}catch(IOException){overlap=true;}check(overlap,"A target's native output cannot write into another target's authoring workspace");
        bool immutable=false;try{restored.ValidateNativeOutput(new ProjectFolders(path).Resolve(".fighterport/revisions/overwrite.chr0"));}catch(IOException){immutable=true;}check(immutable,"Native writes cannot overlap immutable committed character revisions");
        string moved=Path.Combine(output,"MovedCharacter");Directory.Move(folder,moved);string movedPath=Path.Combine(moved,"Character.fighterport");var movedProject=await CharacterStore.LoadAsync(movedPath);
        check(movedProject.Project.Id==character&&ProjectStore.ValidateSources(movedProject.Document!.Project).Length==0,"Moving the complete project folder resolves native inputs by their verified managed identities");
        string legacy=Path.Combine(output,"legacy.fighterport");await ProjectStore.SaveAsync(scene with{Targets=[new(main,new("Project+","Wii","Old"),"Replacement")]},legacy);string legacyHash=ProjectStore.Hash(legacy);var migration=await CharacterStore.LoadAsync(legacy);
        check(migration.RequiresUpgradeCopy&&migration.Project.Scenes.Single().Name=="Existing Character"&&migration.Project.Targets.Single().SceneIds.Length==0,"Schema-1 migration retains one Existing Character scene without guessing target ownership");
        bool refused=false;try{await migration.SaveAsync(legacy);}catch(IOException){refused=true;}check(refused&&ProjectStore.Hash(legacy)==legacyHash,"Migration refuses to overwrite its original schema-1 file");
        await migration.SaveAsync(Path.Combine(output,"upgraded.fighterport"));check((await CharacterStore.LoadAsync(migration.Path!)).Document!.Project.Clips[0].Id==clip.Id&&ProjectStore.Hash(legacy)==legacyHash,"An explicit upgraded copy preserves legacy clip/native identities and source bytes");
        workspace=CharacterWorkspace.Empty("ModelOnly");workspace.AddScene(original with{Clips=[],Contacts=[],RetargetBatches=[]});var model=workspace.Document!;using(var edit=model.BeginTransformEdit()){var value=model.Evaluate().Local[bone];model.SetPose(bone,value with{Translation=value.Translation+Vector3.UnitY});edit.Commit(model.PoseOverrides.ToDictionary());}
        check(model.Project.Clips.Length==0&&model.Project.AuthoringPose.ContainsKey(bone)&&AnimationControls.DeformChecksum(model.Project.Scene)==rig,"Model-only edits persist an authoring pose without inventing an animation or changing binds");
        model.PinPoint(new(Guid.NewGuid(),"Authored support",bone,Vector3.Zero,model.Evaluate().World[bone].Translation,Quaternion.Identity,false,"User support"),0);check(model.Project.Clips.Length==0&&model.CurrentContacts.Length==1,"Model-only contact controls retain supports without silently creating a clip");
        await workspace.SaveAsync(Path.Combine(output,"ModelOnly","ModelOnly.fighterport"));var modelRestored=(await CharacterStore.LoadAsync(workspace.Path!)).Document!;
        check(modelRestored.Project.Clips.Length==0&&modelRestored.CurrentContacts.Length==1&&modelRestored.Evaluate().Local[bone]==model.Evaluate().Local[bone],"A real model-only save/reopen restores its authored pose and support without a synthetic clip");
        string newer=Path.Combine(output,"newer.fighterport");await File.WriteAllTextAsync(newer,"{\"SchemaVersion\":3}");string newerHash=ProjectStore.Hash(newer);bool newerRefused=false;try{await CharacterStore.LoadAsync(newer);}catch(InvalidDataException){newerRefused=true;}check(newerRefused&&ProjectStore.Hash(newer)==newerHash,"An unsupported newer schema is refused without rewriting its file");
        var readOnly=new EditorDocument(original);readOnly.BeginSourcePreview(original,clip.Id);bool readOnlyRefused=false;try{readOnly.BeginTransformEdit();}catch(InvalidOperationException){readOnlyRefused=true;}check(readOnlyRefused&&!readOnly.Dirty,"Read-only authentic source previews cannot begin an automatic-key gesture");
        if(adapter is not null)
        {
            string native=Path.Combine(output,"AutoKeyNative.chr0");bool partial=nativeEdited.Clips[0].Tracks.Any(t=>Enumerable.Range(0,3).Any(g=>t.Channels.Skip(g*3).Take(3).Any(c=>c is not null)&&t.Channels.Skip(g*3).Take(3).Any(c=>c is null)));
            if(partial)
            {
                bool blocked=false;try{await adapter.ExportAsync(nativeEdited,nativeEdited.Clips[0],native);}catch(NotSupportedException){blocked=true;}
                check(blocked&&!File.Exists(native),"A newly keyed partial native S/R/T group rejects before output instead of resetting the unkeyed native rest axes");
            }
            // Independently exercise an encodable complete group. This positive
            // case is separate from the preserved partial-group rejection above.
            var full=new EditorDocument(nativeEdited);var pose=full.Evaluate().Local[bone];using(var gesture=full.BeginTransformEdit())gesture.Commit(new Dictionary<int,Transform>{{bone,pose with{Translation=pose.Translation+new Vector3(.125f,.25f,.375f)}}});
            string complete=Path.Combine(output,"AutoKeyNativeComplete.chr0");var export=await adapter.ExportAsync(full.Project,full.Clip,complete);var actual=AnimationService.Evaluate(export.Project!.Scene,export.Project.Clips[0],0);var expected=AnimationService.Evaluate(full.Project.Scene,full.Clip,0);
            check(Vector3.Distance(actual.World[bone].Translation,expected.World[bone].Translation)<.0001f,"A complete automatically keyed native translation exports to NEW CHR0 and fresh-reopens with its independently expected pose");
        }
        await File.WriteAllTextAsync(Path.Combine(output,"workspace-receipt.json"),JsonSerializer.Serialize(new{Project=character,Targets=new[]{main,practice,rivals},Deform=rig,NativeChecked=adapter is not null,Claim="Character lifecycle/editing; not a complete game builder or retarget contact pass."}));
    }
}
