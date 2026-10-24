using System.Numerics;
using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class NativeControlChecks
{
    public static async Task<ControlRigSetup> RunAsync(BrawlAdapter adapter,FighterProject game,string model,string motion,string output,
        Action<bool,string> check,Action<float,float,float,string> near)
    {
        string label=Path.GetFileNameWithoutExtension(model);string checksum=AnimationControls.DeformChecksum(game.Scene);
        var originals=new[]{ProjectStore.Hash(model),ProjectStore.Hash(motion)};string sourceClips=JsonSerializer.Serialize(game.Clips,ProjectStore.Json);
        var setup=BrawlControlProfiles.SetUp(game);
        check(setup.Provenance?.ProfileId.StartsWith("brawl.",StringComparison.Ordinal)==true&&setup.Chains.Length==4&&setup.Chains.All(c=>c.Reviewed),$"{label}: exact reviewed native profile attaches four controls");
        foreach(var chain in setup.Chains)
        {
            string side=chain.Role[^1..];string[] names=chain.Role.StartsWith("Hand",StringComparison.Ordinal)?[side+"ShoulderJ",side+"ArmJ",side+"HandN"]:[side+"LegJ",side+"KneeJ",side+"FootJ"];
            check(new[]{chain.Root,chain.Middle,chain.Effector}.Select(i=>game.Scene.Bones[i].Name).SequenceEqual(names),$"{label}: {chain.Role} selects anatomical joints instead of nearby helper endpoints");
            check(chain.Calibration is {UpperLength:>0,LowerLength:>0},$"{label}: {chain.Role} stores bind lengths and local-axis/pole calibration");
            var rest=AnimationService.Evaluate(game.Scene,null,0);var origin=rest.World[chain.Root].Translation;var end=rest.World[chain.Effector].Translation;
            var pole=AnimationControls.DefaultPole(game.Scene,rest,chain);float length=chain.Calibration!.UpperLength+chain.Calibration.LowerLength;
            var goal=origin+(end-origin)*.85f+Vector3.Normalize(pole-origin)*length*.03f;
            var noDonor=AnimationControls.Solve(game.Scene,rest,setup,chain,goal,pole);
            check(noDonor.Value is not null&&noDonor.Value.Error<.0003f,$"{label}: {chain.Role} works without a donor clip on native left/right axes");
        }
        check(checksum==AnimationControls.DeformChecksum(game.Scene),$"{label}: attaching controls preserves every bone/rest/skin checksum");
        var controlsFile=Path.Combine(output,label+"-controls.fighterport");await ProjectStore.SaveAsync(game with{ControlRigs=[setup]},controlsFile);
        var loaded=await ProjectStore.LoadAsync(controlsFile);var reused=BrawlControlProfiles.SetUp(loaded);
        check(reused.Id==setup.Id&&JsonSerializer.Serialize(reused,ProjectStore.Json)==JsonSerializer.Serialize(setup,ProjectStore.Json),$"{label}: saved roles/helpers/calibration reuse exactly after reopen");
        var changed=loaded with{Scene=loaded.Scene with{Bones=loaded.Scene.Bones.Select((b,i)=>i==setup.Chains[0].Middle?b with{Rest=b.Rest with{Translation=b.Rest.Translation*1.01f}}:b).ToArray()}};
        var invalidated=BrawlControlProfiles.SetUp(changed);check(invalidated.Id!=setup.Id&&invalidated.Provenance is null,$"{label}: changed native bind invalidates the reviewed fingerprint");
        var edited=new EditorDocument(loaded);edited.Seek(12);
        var fk=new Dictionary<int,Transform>();
        foreach(string role in new[]{"Chest","Clavicle.L"}){int bone=setup.BoneRoles.Single(m=>m.SemanticRole==role).Bone;var t=edited.Evaluate().Local[bone];fk[bone]=t with{Rotation=t.Rotation+new Vector3(0,role=="Chest"?6:0,role=="Clavicle.L"?4:0)};}
        edited.KeyTransforms(fk,"Independent native FK");var beforeIk=edited.Project;var basePose=edited.Evaluate();
        var keys=new Dictionary<int,Transform>();var goals=new Dictionary<string,Vector3>();var poles=new Dictionary<string,Vector3>();
        foreach(var chain in setup.Chains)
        {
            var origin=basePose.World[chain.Root].Translation;var pole=AnimationControls.DefaultPole(game.Scene,basePose,chain);var end=basePose.World[chain.Effector].Translation;
            float length=Vector3.Distance(origin,basePose.World[chain.Middle].Translation)+Vector3.Distance(basePose.World[chain.Middle].Translation,end);
            var goal=origin+(end-origin)*.86f+Vector3.Normalize(pole-origin)*length*.025f;
            var solve=AnimationControls.Solve(game.Scene,basePose,setup,chain,goal,pole);
            check(solve.Value is not null,$"{label}: animated {chain.Role} composes with chest/clavicle FK ({string.Join("; ",solve.Diagnostics.Select(d=>d.Code+": "+d.Message))})");
            foreach(var pair in solve.Value!.Transforms)keys.Add(pair.Key,pair.Value);goals[chain.Role]=goal;poles[chain.Role]=pole;
        }
        edited.KeyTransforms(keys);edited.Undo();check(ReferenceEquals(edited.Project,beforeIk),$"{label}: four-limb IK bake undoes as one transaction and keeps FK");edited.Redo();
        var posed=edited.Evaluate();foreach(var pair in fk)near(Vector3.Distance(posed.Local[pair.Key].Rotation,pair.Value.Rotation),0,0,$"{label}: independent FK remains keyed");
        var editedFile=Path.Combine(output,label+"-fk-ik.fighterport");await ProjectStore.SaveAsync(edited.Project,editedFile);var persisted=await ProjectStore.LoadAsync(editedFile);
        check(BrawlControlProfiles.SetUp(persisted).Id==setup.Id,$"{label}: edited project retains the original reviewed setup");
        double[] frames=[0,.125,11.5,11.875,12,12.125,12.5,15.75,23];string nativePath=Path.Combine(output,label+"-fk-ik.chr0");
        await adapter.ExportAsync(persisted,persisted.Clips[0],nativePath);
        var fresh=await adapter.RunAsync(new("import",model,nativePath,null,null,[],frames));var native=fresh.Project!;var vertices=native.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();
        float geometryError=0,boneError=0,helperError=0;
        for(int f=0;f<frames.Length;f++)
        {
            var pose=AnimationService.Evaluate(native.Scene,native.Clips[0],frames[f]);var before=AnimationService.Evaluate(game.Scene,game.Clips[0],frames[f]);
            for(int v=0;v<vertices.Length;v++){var p=fresh.ExpectedVertices![f][v];geometryError=Math.Max(geometryError,Vector3.Distance(AnimationService.Skin(vertices[v],pose),new(p[0],p[1],p[2])));}
            for(int b=0;b<native.Scene.Bones.Length;b++){var p=fresh.ExpectedBonePositions![f][b];boneError=Math.Max(boneError,Vector3.Distance(pose.World[b].Translation,new(p[0],p[1],p[2])));}
            foreach(int b in setup.PreservedHelperBones){var a=pose.Local[b].Channels();var z=before.Local[b].Channels();for(int ch=0;ch<9;ch++)helperError=Math.Max(helperError,Math.Abs(a[ch]-z[ch]));}
        }
        near(geometryError,0,.0003f,$"{label}: fresh FK/IK CHR0 geometry matches independent native skinning at fractional samples");
        near(boneError,0,.0003f,$"{label}: fresh FK/IK CHR0 all-bone positions match independent native matrices");
        near(helperError,0,.002f,$"{label}: native helper local channels survive FK/IK export/reopen");
        foreach(var chain in setup.Chains)
        {
            var p=fresh.ExpectedBonePositions![4][chain.Effector];near(Vector3.Distance(new(p[0],p[1],p[2]),goals[chain.Role]),0,.003f,$"{label}: independently reopened native {chain.Role} reaches edited goal");
            var mid=fresh.ExpectedBonePositions[4][chain.Middle];var start=fresh.ExpectedBonePositions[4][chain.Root];var origin=new Vector3(start[0],start[1],start[2]);var axis=Vector3.Normalize(goals[chain.Role]-origin);
            var bend=new Vector3(mid[0],mid[1],mid[2])-origin;var pole=poles[chain.Role]-origin;check(Vector3.Dot(bend-axis*Vector3.Dot(bend,axis),pole-axis*Vector3.Dot(pole,axis))>0,$"{label}: independently reopened {chain.Role} keeps the requested bend half-plane");
        }
        var atKey=AnimationService.Evaluate(native.Scene,native.Clips[0],12);foreach(var pair in fk)near(Vector3.Distance(atKey.Local[pair.Key].Rotation,pair.Value.Rotation),0,.003f,$"{label}: FK rotation survives native quantization and reopen");
        check(native.Clips[0].SampleCount==game.Clips[0].SampleCount&&native.Clips[0].Loop==game.Clips[0].Loop&&native.Clips[0].Rate==game.Clips[0].Rate&&native.Clips[0].LastKeyFrame==game.Clips[0].LastKeyFrame,$"{label}: native sample count/last-key time/rate/loop stay unchanged");
        check(checksum==AnimationControls.DeformChecksum(native.Scene)&&checksum==AnimationControls.DeformChecksum(persisted.Scene),$"{label}: native reopen and saved project retain original skeleton and skin weights");
        check(sourceClips==JsonSerializer.Serialize(game.Clips,ProjectStore.Json)&&originals.SequenceEqual(new[]{ProjectStore.Hash(model),ProjectStore.Hash(motion)}),$"{label}: source motion revisions and original model/motion hashes are isolated");
        var bodyDoc=new EditorDocument(loaded);bodyDoc.Seek(12);bodyDoc.PinControls(setup,["Foot.L","Foot.R"]);var initialBody=bodyDoc.Evaluate();var planted=bodyDoc.CurrentContacts;int pelvis=ContactSolver.BodyBone(game.Scene,setup);var desiredBody=initialBody.Local[pelvis] with{Translation=initialBody.Local[pelvis].Translation-new Vector3(0,PoseGeometry.Scale(game.Scene,setup)*.015f,0)};
        var bodyResult=ContactSolver.Solve(game.Scene,initialBody,setup,new Dictionary<int,Transform>{{pelvis,desiredBody}},planted);check(bodyResult.Value is not null,$"{label}: existing pelvis moves while both native feet remain planted");bodyDoc.KeyTransforms(bodyResult.Value!.Transforms,"Native pelvis with foot contacts");bodyDoc.Undo();check(bodyDoc.Evaluate().Local.SequenceEqual(initialBody.Local),$"{label}: native pelvis and both leg corrections undo together");bodyDoc.Redo();
        string bodyFile=Path.Combine(output,label+"-body-contacts.fighterport");await ProjectStore.SaveAsync(bodyDoc.Project,bodyFile);var bodySaved=await ProjectStore.LoadAsync(bodyFile);var bodyReload=new EditorDocument(bodySaved);bodyReload.Seek(12);check(bodyReload.CurrentContacts.Length==2,$"{label}: planted native support anchors survive project reopen");
        string bodyNative=Path.Combine(output,label+"-body-contacts.chr0");await adapter.ExportAsync(bodySaved,bodySaved.Clips[0],bodyNative);var bodyFresh=await adapter.RunAsync(new("import",model,bodyNative,null,null,[],[11.75,12,12.25]));
        var bodyPose=AnimationService.Evaluate(bodyFresh.Project!.Scene,bodyFresh.Project.Clips[0],12);
        foreach(var pin in planted){var point=bodyFresh.ExpectedBonePositions![1][pin.Bone];near(Vector3.Distance(new(point[0],point[1],point[2]),pin.Position),0,.003f,$"{label}: fresh native {pin.Role} retains its planted world position");near(1-Math.Abs(Quaternion.Dot(PoseGeometry.Rotation(bodyPose.World[pin.Bone]),pin.Rotation)),0,.00001f,$"{label}: fresh native {pin.Role} retains its planted orientation");}
        float bodyGeometry=0;var bodyVertices=bodyFresh.Project.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();for(int f=0;f<3;f++){var p=AnimationService.Evaluate(bodyFresh.Project.Scene,bodyFresh.Project.Clips[0],new[]{11.75,12,12.25}[f]);for(int v=0;v<bodyVertices.Length;v++){var expected=bodyFresh.ExpectedVertices![f][v];bodyGeometry=Math.Max(bodyGeometry,Vector3.Distance(AnimationService.Skin(bodyVertices[v],p),new(expected[0],expected[1],expected[2])));}}near(bodyGeometry,0,.0003f,$"{label}: native body/contact skinning agrees independently at protected fractional samples");
        check(checksum==AnimationControls.DeformChecksum(bodyFresh.Project.Scene)&&originals.SequenceEqual(new[]{ProjectStore.Hash(model),ProjectStore.Hash(motion)}),$"{label}: body/contact native export preserves original skeleton, weights and source hashes");
        return setup;
    }
}
