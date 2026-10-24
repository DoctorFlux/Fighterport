using System.Numerics;
using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class SemanticControlChecks
{
    static readonly (string Role,int Bone)[] Roles=
    [
        ("RootMotion",0),("Hips",1),("Spine",2),("Chest",3),("Neck",4),("Head",5),
        ("Clavicle.L",6),("Clavicle.R",10),("Channel.Jaw",20)
    ];
    public static async Task RunAsync(string directory,Action<bool,string> check,FighterProject[]? nativeFixtures=null)
    {
        var source=Fixture("opaque_a_",1);var target=Fixture("different_b_",1.3f);
        string sceneProof=JsonSerializer.Serialize(source,ProjectStore.Json);string deform=AnimationControls.DeformChecksum(source);
        var empty=Bare(source);var setup=Reviewed(source);var other=Reviewed(target);
        check(Roles.All(r=>setup.BoneRoles.Single(m=>m.SemanticRole==r.Role) is {Reviewed:true} map&&map.Bone==r.Bone)&&
            setup.BodyBone==1&&AnimationControls.ResolveBodyBone(source,setup)==1,
            "Reviewed pelvis, waist, bust, neck, head, shoulders and additional channels use existing native bones with one saved mapping");
        var nativePose=AnimationService.Evaluate(source,null,0);
        check(AnimationControls.ProtectedMotionBones(source,setup).SequenceEqual(new[]{0}),
            "Unfamiliar reviewed hierarchy protects strict body ancestors independently of names");
        var prohibited=EditorControlPosing.Solve(source,nativePose,setup,new Dictionary<int,Transform>{{0,source.Bones[0].Rest with{Translation=new(3,2,1)}}},[]);
        check(prohibited.Value is null&&prohibited.Diagnostics.Any(d=>d.Code=="SOURCE_MOTION_CONTROL"),
            "Editor solver rejects root control movement before any contact/FK solve");
        var permitted=EditorControlPosing.Solve(source,nativePose,setup,new Dictionary<int,Transform>{{1,source.Bones[1].Rest with{Translation=new(3,2,1)}}},[]);
        check(permitted.Value is not null&&!permitted.Value.Transforms.ContainsKey(0),
            "Body posing remains available without moving the protected source control");
        var noOp=AnimationControls.ReviewBoneRole(source,setup,"Chest",3,"Reconfirmed by selection").Value;
        check(ReferenceEquals(noOp,setup),"Selecting an already reviewed torso role preserves its setup identity and original evidence");
        var legacyBody=setup with{BodyBone=null};
        check(ReferenceEquals(AnimationControls.ReviewBoneRole(source,legacyBody,"Hips",1,"Reconfirmed existing profile").Value,legacyBody),
            "A saved reviewed Hips role without redundant BodyBone metadata remains a no-op when opened for FK posing");
        var torsoOnly=setup with{Chains=[],VirtualChains=null};
        check(ContactSolver.BodyBone(source,torsoOnly)==1&&RigDescriptor.Prepare(source,torsoOnly).Binding.Roles["Hips"]==1,
            "A reviewed torso-only rig resolves the same body for independent controls and retargeting without inventing leg chains");
        var cleared=AnimationControls.ReviewBoneRole(source,setup,"Hips",null,"Artist explicitly leaves body unresolved").Value!;
        check(cleared.BodyBone is null&&!cleared.BoneRoles.Any(r=>r.SemanticRole=="Hips")&&cleared.UnassignedBoneRoles?.Contains("Hips")==true&&
            ContactSolver.BodyBone(source,cleared)==-1&&!RigDescriptor.Prepare(source,cleared).Binding.Roles.ContainsKey("Hips"),
            "Explicit body unassignment clears both views and blocks shared-leg inference and retarget descriptor reinjection");
        check(ReferenceEquals(AnimationControls.ReviewBoneRole(source,cleared,"Hips",null,"Repeated clear").Value,cleared)&&
            ReferenceEquals(AnimationControls.SetUp(source,[cleared]),cleared),
            "Repeated unassignment and exact-rig setup reuse preserve the recorded decision without creating revisions");
        var restored=AnimationControls.ReviewBoneRole(source,cleared,"Hips",1,"Reviewed existing body again").Value!;
        check(restored.BodyBone==1&&restored.UnassignedBoneRoles is null&&ContactSolver.BodyBone(source,restored)==1,
            "Explicit reassignment restores the same body mapping and clears its unassignment marker");
        var noActor=AnimationControls.ReviewBoneRole(source,setup,"RootMotion",null,"Actor interpretation unresolved").Value!;
        bool actorRejected=false;try{RigDescriptor.Prepare(source,noActor);}catch(InvalidDataException e){actorRejected=e.Message.Contains("unassigned",StringComparison.Ordinal);}
        check(actorRejected,"Explicit root unassignment is a precise retarget capability failure rather than silently restoring bone zero");
        check(AnimationControls.ReviewBoneRole(source,setup,"Hips",9,"Not a leg ancestor").Diagnostics.Any(d=>d.Code=="BODY_HIERARCHY")&&
            AnimationControls.ReviewBoneRole(source,setup,"UpperArm.L",6,"Conflicts with IK ownership").Diagnostics.Any(d=>d.Code=="CHAIN_ROLE_CONFLICT")&&
            AnimationControls.ReviewBoneRole(source,setup,"Wrist.L",null,"Conflicts with IK ownership").Diagnostics.Any(d=>d.Code=="CHAIN_ROLE_CONFLICT"),
            "Conflicting body and chain-role choices fail explicitly before FK/IK and retarget mappings can diverge");
        check(AnimationControls.ReviewBoneRole(target,setup,"Chest",3,"Wrong rest revision").Diagnostics.Any(d=>d.Code=="RIG_CHANGED")&&
            AnimationControls.ReviewBoneRole(source,setup,"Chest",999,"Missing native bone").Diagnostics.Any(d=>d.Code=="INVALID_BONE")&&
            AnimationControls.ReviewBoneRole(source,setup," ",3,"Missing role").Diagnostics.Any(d=>d.Code=="REVIEW_EVIDENCE"),
            "Role review rejects stale rigs, absent bones and empty semantics without editing native data");

        var project=new FighterProject(1,Guid.NewGuid(),"Independent torso-control fixture",source,[]){ControlRigs=[empty]};
        var document=new EditorDocument(project);
        document.Transaction("Review body and shoulders",p=>p with{ControlRigs=[setup]});var reviewedProject=document.Project;
        document.Undo();check(ReferenceEquals(document.Project,project),"One Undo restores the complete prior rig setup without authored motion");
        document.Redo();check(ReferenceEquals(document.Project,reviewedProject),"Redo restores stored reviewed roles without rerunning discovery");
        var fk=Roles.Where(r=>r.Role is "Hips" or "Spine" or "Chest" or "Clavicle.L" or "Clavicle.R")
            .ToDictionary(r=>r.Bone,r=>source.Bones[r.Bone].Rest with{Rotation=new(2,3,4)});
        using(var gesture=document.BeginTransformEdit())
        {
            foreach(var (bone,value) in fk)document.SetPose(bone,value);
            gesture.Commit(document.PoseOverrides.ToDictionary());
        }
        var posedProject=document.Project;var posed=document.Evaluate();
        check(document.Project.Clips.Length==0&&fk.All(p=>posed.Local[p.Key]==p.Value)&&
            Vector3.Distance(AnimationService.Skin(source.Meshes[0].Vertices[9],posed),source.Meshes[0].Vertices[9].Position)>.001f,
            "Existing torso/shoulder FK controls change actual skinned geometry in model-only mode without creating a donor clip or changing rests");
        document.Undo();check(ReferenceEquals(document.Project,reviewedProject)&&document.Evaluate().Local.SequenceEqual(AnimationService.Evaluate(source,null,0).Local),
            "One completed model-only torso gesture undoes all affected bones exactly");
        document.Redo();check(ReferenceEquals(document.Project,posedProject),"Torso FK Redo restores the stored authoring pose");
        var hand=setup.Chains.Single(c=>c.Role=="Hand.R");var pole=AnimationControls.DefaultPole(source,posed,hand);
        var origin=posed.World[hand.Root].Translation;var endpoint=posed.World[hand.Effector].Translation;
        var goal=origin+(endpoint-origin)*.85f+Vector3.Normalize(pole-origin)*.02f;
        var ik=AnimationControls.Solve(source,posed,setup,hand,goal,pole);
        check(ik.Value is not null&&ik.Value.Error<.0001f,"Independent hand IK reaches a prepared goal after waist/bust/shoulder FK with no donor animation");
        var combined=posed.Local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t);
        foreach(var pair in ik.Value!.Transforms)combined[pair.Key]=pair.Value;
        var combinedPose=AnimationService.Evaluate(source,null,0,combined);
        check(fk.All(p=>combinedPose.Local[p.Key]==p.Value)&&Vector3.Distance(combinedPose.World[hand.Effector].Translation,goal)<.0001f,
            "Hand IK composes with the reviewed torso/shoulder pose and preserves their requested FK transforms");
        var bind=AnimationService.Evaluate(source,null,0);
        var pins=setup.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).Select(c=>ContactSolver.Capture(source,bind,setup,c.Role,"Independent fixture floor")).ToArray();
        var bodyEdit=bind.Local[1] with{Translation=bind.Local[1].Translation-new Vector3(0,.1f,0)};
        var supported=ContactSolver.Solve(source,bind,setup,new Dictionary<int,Transform>{{1,bodyEdit}},pins);
        check(supported.Value is not null&&supported.Value.Transforms[1]==bodyEdit&&supported.Value.PositionError<.0002f,
            "The same reviewed Hips binding drives a donor-free body edit with both existing foot contacts retained");

        foreach(var (from,a,to,b) in new[]{(source,setup,target,other),(target,other,source,setup)})
        {
            var tracks=Roles.Where(r=>r.Role!="RootMotion").Select(r=>
            {
                var channels=new Curve?[9];channels[3]=new([new(0,0),new(1,10)]);
                return new BoneTrack(from.Bones[r.Bone].Name,channels);
            }).ToArray();
            var clip=new AnimationClip(Guid.NewGuid(),"Independent authored semantic channels",2,new(60),false,tracks);
            var donor=new FighterProject(1,Guid.NewGuid(),"Source",from,[clip]);var destination=new FighterProject(1,Guid.NewGuid(),"Target",to,[]);
            var request=new RetargetRequest(donor,clip,a,destination,b,RootMotionPolicy.Preserve,false,default,null,null,null,null,null,null,null);
            var transfer=new BaselineMotionTransfer(request,RigDescriptor.Prepare(from,a),RigDescriptor.Prepare(to,b));
            var values=transfer.Evaluate(1);
            check(Roles.All(r=>transfer.Mapping.Bones.Any(p=>p.Source==r.Bone&&p.Target==r.Bone))&&
                Roles.Where(r=>r.Role!="RootMotion").All(r=>Vector3.Distance(values[r.Bone].Rotation,new(10,0,0))<.001f),
                "Both directions transfer reviewed torso, shoulders and shared channels across different native names using the same controls binding");
        }
        var file=Path.Combine(directory,"semantic-controls.fighterport");await ProjectStore.SaveAsync(document.Project,file);
        var reopened=await ProjectStore.LoadAsync(file);var reused=AnimationControls.SetUp(reopened.Scene,reopened.ControlRigs);
        check(JsonSerializer.Serialize(reused,ProjectStore.Json)==JsonSerializer.Serialize(setup,ProjectStore.Json)&&
            fk.All(p=>reopened.AuthoringPose[p.Key]==p.Value),
            "Project save/reopen retains reviewed semantic evidence and donor-free torso FK pose together");
        var clearFile=Path.Combine(directory,"semantic-controls-unassigned.fighterport");await ProjectStore.SaveAsync(project with{ControlRigs=[cleared]},clearFile);
        var reopenClear=await ProjectStore.LoadAsync(clearFile);
        check(ContactSolver.BodyBone(reopenClear.Scene,AnimationControls.SetUp(reopenClear.Scene,reopenClear.ControlRigs))==-1&&
            !JsonSerializer.Serialize(empty,ProjectStore.Json).Contains("UnassignedBoneRoles",StringComparison.Ordinal),
            "Explicit unassignment survives restart while absent optional metadata leaves historical serialization unchanged");
        check(deform==AnimationControls.DeformChecksum(source)&&sceneProof==JsonSerializer.Serialize(source,ProjectStore.Json),
            "Review, FK, IK, retarget transfer and persistence leave native names/order/hierarchy/rests/skin unchanged");

        foreach(var native in nativeFixtures??[])
        {
            string before=AnimationControls.DeformChecksum(native.Scene);var nativeRig=BrawlControlProfiles.SetUp(native);
            var clearedBody=AnimationControls.ReviewBoneRole(native.Scene,nativeRig,"Hips",null,"Explicit native body review").Value!;
            var detailed=nativeRig.BoneRoles.FirstOrDefault(r=>r.SemanticRole.StartsWith("Grip.",StringComparison.Ordinal));
            if(detailed is not null)clearedBody=AnimationControls.ReviewBoneRole(native.Scene,clearedBody,detailed.SemanticRole,null,"Explicit native shared-role review").Value!;
            var reusedNative=BrawlControlProfiles.SetUp(native with{ControlRigs=[clearedBody]});
            check(reusedNative.Id==clearedBody.Id&&ContactSolver.BodyBone(native.Scene,reusedNative)==-1&&
                !RigDescriptor.Prepare(native.Scene,reusedNative).Binding.Roles.ContainsKey("Hips")&&
                (detailed is null||!reusedNative.BoneRoles.Any(r=>r.SemanticRole==detailed.SemanticRole)),
                native.Scene.Name+": exact reviewed Brawl profile reuse respects explicitly cleared body/detailed bindings");
            check(nativeRig.Chains.SequenceEqual(reusedNative.Chains)&&before==AnimationControls.DeformChecksum(native.Scene),
                native.Scene.Name+": native role review retains reviewed limb calibration and all deform data");
            if(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER") is {} worker)
            {
                var adapter=new BrawlAdapter(worker);var original=native.Clips.Single(c=>c.Source?.NativeName=="Wait1");
                var inputs=ProjectStore.SourceReferences(native).DistinctBy(r=>r.Path).Select(r=>(r.Path,Hash:ProjectStore.Hash(r.Path))).ToArray();
                int chest=nativeRig.BoneRoles.Single(r=>r.SemanticRole=="Chest"&&r.Reviewed).Bone;
                var editing=new EditorDocument(native with{Clips=[original],ControlRigs=[nativeRig]});editing.Seek(0);
                var value=editing.Evaluate().Local[chest];
                using(var gesture=editing.BeginTransformEdit())
                {
                    editing.SetPose(chest,value with{Rotation=value.Rotation+new Vector3(0,0,3)});
                    gesture.Commit(editing.PoseOverrides.ToDictionary());
                }
                var output=Path.Combine(directory,native.Scene.Name+"-torso-edit.chr0");
                await adapter.ExportAsync(editing.Project,editing.Clip,output);
                double[] times=[0,.125,.5,1,Math.Min(13.8107,original.LastSampleFrame)];
                var fresh=await adapter.RunAsync(new("import",original.Source!.Model!.Path,output,null,null,[],times));
                var decoded=fresh.Project!;var exact=NativeRetargetMatrix.ExactCurves(editing.Clip,decoded.Clips[0],decoded.Scene);
                check(exact.Exact&&exact.Comparisons>0&&decoded.Clips[0].SampleCount==original.SampleCount&&decoded.Clips[0].Loop==original.Loop,
                    native.Scene.Name+": edited native torso keys/tangents, sample timing and loop flags survive fresh CHR0 reopen exactly");
                float error=0;var vertices=decoded.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();
                for(int t=0;t<times.Length;t++)
                {
                    var pose=AnimationService.Evaluate(decoded.Scene,decoded.Clips[0],times[t]);
                    for(int v=0;v<vertices.Length;v++){var p=fresh.ExpectedVertices![t][v];error=Math.Max(error,Vector3.Distance(AnimationService.Skin(vertices[v],pose),new(p[0],p[1],p[2])));}
                }
                check(error<.003f&&before==AnimationControls.DeformChecksum(decoded.Scene)&&inputs.All(r=>ProjectStore.Hash(r.Path)==r.Hash),
                    native.Scene.Name+": fresh native torso geometry matches independent worker skinning while original native sources and deform rig stay unchanged");
            }
        }
    }
    static ControlRigSetup Bare(Scene scene)=>new(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
    {Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Independent original fixture units")};
    static ControlRigSetup Reviewed(Scene scene)
    {
        var setup=Bare(scene);
        foreach(var (role,root,middle,end) in new[]{("Hand.L",7,8,9),("Hand.R",11,12,13),("Foot.L",14,15,16),("Foot.R",17,18,19)})
            setup=AnimationControls.ReviewChain(scene,setup,role,root,middle,end,Vector3.UnitZ,"Independently authored native chain").Value!;
        foreach(var (role,bone) in Roles)setup=AnimationControls.ReviewBoneRole(scene,setup,role,bone,"Independently reviewed native role").Value!;
        return setup;
    }
    static Scene Fixture(string prefix,float scale)
    {
        Bone B(int i,int parent,float x,float y,float z)=>new(prefix+i,parent,Transform.Identity with{Translation=new Vector3(x,y,z)*scale});
        var scene=new Scene(prefix,
        [
            B(0,-1,0,0,0),B(1,0,0,2,0),B(2,1,0,.5f,0),B(3,2,0,.5f,0),B(4,3,0,.5f,0),B(5,4,0,.5f,0),
            B(6,3,-.3f,.2f,0),B(7,6,-.2f,0,0),B(8,7,-.6f,0,0),B(9,8,-.5f,0,0),
            B(10,3,.3f,.2f,0),B(11,10,.2f,0,0),B(12,11,.6f,0,0),B(13,12,.5f,0,0),
            B(14,1,-.2f,-.1f,0),B(15,14,0,-.9f,0),B(16,15,0,-.8f,0),
            B(17,1,.2f,-.1f,0),B(18,17,0,-.9f,0),B(19,18,0,-.8f,0),B(20,5,0,.05f,.1f)
        ],[]);
        var rest=AnimationService.Evaluate(scene,null,0);
        var vertices=Enumerable.Range(1,20).SelectMany(bone=>new[]{new Vector3(-.03f,0,0),new Vector3(.03f,0,0),new Vector3(0,.06f,0)}
            .Select(offset=>new Vertex(rest.World[bone].Translation+offset*scale,Vector3.UnitZ,Vector2.Zero,new(bone,0,0,0),new(1,0,0,0),Vector4.One))).ToArray();
        return scene with{Meshes=[new("Original weighted triangles",vertices,Enumerable.Range(0,vertices.Length).Select(i=>(uint)i).ToArray())]};
    }
}
