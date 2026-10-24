using System.Numerics;
using FighterPort.Core;

// Independent action-objective expectations. These do not certify contact
// classification, native export, held-out anatomy or complete retarget quality.
internal static class ActionObjectiveChecks
{
    public static void Run(Action<bool,string> check)
    {
        var project=ProceduralFixture.Create();var scene=project.Scene;
        var rig=SurfaceContactChecks.Profile(scene) with{Coordinates=new(1,"Original fixture metres")};
        var descriptor=RigDescriptor.Prepare(scene,rig);var reference=AnimationService.Evaluate(scene,null,0);
        var map=new RetargetMappingReport(Enumerable.Range(0,scene.Bones.Length).Select(i=>new RetargetBonePair("Supplied."+i,i,i,"Independent complete fixture correspondence"){DetailChannels=i==6}).ToArray(),[],[]);
        string before=AnimationControls.DeformChecksum(scene);
        var objective=PreparedMotionAction.Create(descriptor,map,reference);
        EvaluatedPose Pose(params (int Bone,Transform Transform)[] changes)=>AnimationService.Evaluate(scene,null,0,changes.ToDictionary(p=>p.Bone,p=>p.Transform));
        check(objective.Cost(reference)==0&&objective.Measure(reference).Landmarks==scene.Bones.Length,"Prepared action has exact zero baseline cost and retains every supplied mapped landmark");
        var translated=Pose((0,scene.Bones[0].Rest with{Translation=new(5,7,-3)}));
        check(objective.Cost(translated)<1e-12,"Common actor translation does not alter relative action positions or world orientations");
        var bodyTranslated=Pose((1,scene.Bones[1].Rest with{Translation=scene.Bones[1].Rest.Translation+new Vector3(2,-3,4)}));
        check(objective.Cost(bodyTranslated)<1e-12,"Permitted body translation does not acquire an indirect actor-ancestor action penalty");
        var facing=Pose((1,scene.Bones[1].Rest with{Rotation=new(0,90,0)}));var facingError=objective.Measure(facing);
        check(facingError.MeanPositionScaleSquared>0&&Math.Abs(facingError.MaximumOrientationRadians-MathF.PI/2)<.00001f,"A 90-degree body turn changes action even when its origin is unchanged");
        // Hand is a leaf here: changing its orientation moves no joint origin.
        // The detailed-channel flag must not remove this independent evidence.
        var leaf=Pose((6,scene.Bones[6].Rest with{Rotation=new(0,0,60)}));var leafError=objective.Measure(leaf);
        check(leafError.MaximumPosition==0&&Math.Abs(leafError.MaximumOrientationRadians-MathF.PI/3)<.00001f&&leafError.OrientationBone==6,"A detailed leaf's authored orientation remains measured even without positional displacement");
        var bend=Pose((5,scene.Bones[5].Rest with{Rotation=new(0,0,90)}));var bendError=objective.Measure(bend);
        // Rotating the .36-unit terminal segment through 90 degrees moves its
        // endpoint by sqrt(.36²+.36²), independent of objective implementation.
        float expectedDisplacement=MathF.Sqrt(2)*.36f;
        check(Math.Abs(bendError.MaximumPosition-expectedDisplacement)<.00001f&&bendError.PositionBone==6,"Changed limb bend produces the independently expected terminal displacement");
        check(objective.Cost(bend)>0&&Math.Abs(bendError.MaximumOrientationRadians-MathF.PI/2)<.00001f,"Limb bend and compatible world orientation contribute separate nonzero action measurements");

        // Valid parent-before-child reorder swaps complete branches, moves the
        // torso later, and removes every native name. Supplied bindings alone
        // identify equivalent landmarks; automatic discovery is a separate test.
        int[] order=[0,1,13,14,15,10,11,12,2,7,8,9,4,5,6,3];
        var remap=new int[order.Length];for(int i=0;i<order.Length;i++)remap[order[i]]=i;
        var renamed=scene with
        {
            Bones=order.Select((old,i)=>scene.Bones[old] with{Name="Opaque_"+((i*17+11)%31),Parent=scene.Bones[old].Parent<0?-1:remap[scene.Bones[old].Parent]}).ToArray(),
            Meshes=scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Joints=new(remap[(int)v.Joints.X],remap[(int)v.Joints.Y],remap[(int)v.Joints.Z],remap[(int)v.Joints.W])}).ToArray()}).ToArray(),
            SkeletonRevision=Guid.NewGuid()
        };
        var renamedRig=rig with
        {
            SkeletonFingerprint=AnimationControls.Fingerprint(renamed),BodyBone=rig.BodyBone is {} body?remap[body]:null,
            BoneRoles=rig.BoneRoles.Select(r=>r with{Bone=remap[r.Bone]}).ToArray(),
            Chains=rig.Chains.Select(c=>c with{Root=remap[c.Root],Middle=remap[c.Middle],Effector=remap[c.Effector]}).ToArray(),
            PreservedHelperBones=rig.PreservedHelperBones.Select(i=>remap[i]).ToArray()
        };
        var renamedMap=map with{Bones=map.Bones.Select(p=>p with{Target=remap[p.Target]}).Reverse().ToArray()};
        var renamedReference=AnimationService.Evaluate(renamed,null,0);
        var renamedObjective=PreparedMotionAction.Create(RigDescriptor.Prepare(renamed,renamedRig),renamedMap,renamedReference);
        var renamedBend=AnimationService.Evaluate(renamed,null,0,new Dictionary<int,Transform>{{remap[5],scene.Bones[5].Rest with{Rotation=new(0,0,90)}}});
        var renamedError=renamedObjective.Measure(renamedBend);
        check(renamedObjective.Cost(renamedReference)==0&&Math.Abs(renamedObjective.Cost(renamedBend)-objective.Cost(bend))<1e-12,"Equivalent supplied mappings retain action cost after native renaming, branch storage permutation and mapping iteration reorder");
        check(Math.Abs(renamedError.MaximumPosition-bendError.MaximumPosition)<.000001f&&renamedError.PositionBone==remap[bendError.PositionBone],"Action witnesses retain their mapped physical identity after storage permutation");
        var appendage=RetargetV2Checks.Chain(false);var appendageRig=appendage.ControlRigs[0];
        var appendageReference=AnimationService.Evaluate(appendage.Scene,appendage.Clips[0],0);
        var appendageMap=new RetargetMappingReport(Enumerable.Range(0,appendage.Scene.Bones.Length).Select(i=>new RetargetBonePair("Supplied."+i,i,i,"Reviewed non-humanoid fixture graph")).ToArray(),[],[]);
        var appendageObjective=PreparedMotionAction.Create(RigDescriptor.Prepare(appendage.Scene,appendageRig),appendageMap,appendageReference);
        var appendageMoved=AnimationService.Evaluate(appendage.Scene,appendage.Clips[0],0,new Dictionary<int,Transform>{{0,appendage.Scene.Bones[0].Rest with{Translation=new(3,-2,5)}}});
        check(appendageObjective.Cost(appendageReference)==0&&appendageObjective.Cost(appendageMoved)<1e-12&&appendageObjective.LandmarkCount==6,"Action preparation supports a reviewed five-bone non-humanoid appendage without a pelvis or fixed humanoid subset");
        check(AnimationControls.DeformChecksum(scene)==before,"Preparing and measuring action leaves the original native skeleton, rest geometry and skin weights unchanged");
        GeometryChecks(check);
        ArticulationChecks(check);
    }

    static void ArticulationChecks(Action<bool,string> check)
    {
        var scene=new Scene("Original endpoint-ambiguous articulation",[
            new("unrelated_actor",-1,Transform.Identity),new("unrelated_carrier",0,Transform.Identity),
            new("unrelated_middle",1,Transform.Identity with{Translation=Vector3.UnitX}),
            new("unrelated_tip",2,Transform.Identity with{Translation=Vector3.UnitX})],
            [new("Original tip surface",new[]{new Vector3(2,.02f,0),new(2,-.02f,0),new(2,0,.02f)}
                .Select(p=>new Vertex(p,Vector3.UnitX,Vector2.Zero,new(3,0,0,0),new(1,0,0,0),Vector4.One)).ToArray(),[0,1,2])]);
        var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[]){BodyBone=1,
            Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),BoneRoles=[new("RootMotion",0,"Independent actor binding",true),new("Hips",1,"Independent carrier binding",true)]};
        var descriptor=RigDescriptor.Prepare(scene,rig);
        var map=new RetargetMappingReport(Enumerable.Range(0,4).Select(i=>new RetargetBonePair("Supplied."+i,i,i,"Independent exact identity")).ToArray(),[],[]);
        EvaluatedPose Bend(float sign,Vector3 actor)=>AnimationService.Evaluate(scene,null,0,new Dictionary<int,Transform>{
            [0]=scene.Bones[0].Rest with{Translation=actor},[1]=scene.Bones[1].Rest with{Rotation=new(0,0,60*sign)},
            [2]=scene.Bones[2].Rest with{Rotation=new(0,0,-120*sign)},[3]=scene.Bones[3].Rest with{Rotation=new(0,0,60*sign)}});
        var reference=Bend(1,Vector3.Zero);var opposite=Bend(-1,Vector3.Zero);
        var skin=PreparedMotionAction.Create(descriptor,map,reference);var path=PreparedArticulationAction.Create(descriptor,map,reference);
        // Two length-one links have elbows (.5,+/-sqrt(3)/2) and the
        // identical endpoint (1,0). Integrating the squared path difference
        // on both links gives one; normalization by scale²=4 gives .25.
        check(scene.Meshes[0].Vertices.All(v=>Vector3.Distance(AnimationService.Skin(v,reference),AnimationService.Skin(v,opposite))<.000001f)
            &&skin.Cost(opposite)<1e-12&&Math.Abs(path.Cost(opposite)-.25)<.000001,
            "Opposite native bend branches with identical tip skin expose the independently expected .25 articulated path error");
        check(path.EdgeCount==2&&path.Cost(reference)==0&&path.Residuals(opposite).Length==12
            &&Math.Abs(path.Cost(opposite)-path.Residuals(opposite).Sum(x=>x*x))<1e-15,
            "Zero-length actor/helpers add no path mass and articulated residuals exactly represent their declared cost");
        check(path.Cost(Bend(1,new(3,-2,4)))<1e-12,"Common actor translation leaves source-relative articulated path action unchanged");
        var straight=AnimationService.Evaluate(scene,null,0);
        var twist=AnimationService.Evaluate(scene,null,0,new Dictionary<int,Transform>{[1]=scene.Bones[1].Rest with{Rotation=new(90,0,0)},[2]=scene.Bones[2].Rest with{Rotation=new(-90,0,0)}});
        var twistAction=PreparedArticulationAction.Create(descriptor,map,straight);
        check(straight.World.Select(m=>m.Translation).SequenceEqual(twist.World.Select(m=>m.Translation))
            &&PreparedMotionAction.Create(descriptor,map,straight).Cost(twist)<1e-12&&twistAction.Cost(twist)<1e-12
            &&Math.Abs(twistAction.NativeLinkFrameCost(twist,straight)-Math.PI*Math.PI/8)<.00001,
            "Independent pi-squared/8 native-link frame evidence exposes an unweighted90-degree twist hidden from joint positions and tip skin; this diagnostic is not an optimization pass");
        var reversed=map with{Bones=map.Bones.Reverse().ToArray()};
        check(PreparedArticulationAction.Create(descriptor,reversed,reference).Cost(opposite)==path.Cost(opposite),
            "Supplied correspondence iteration order cannot change the source-relative path measure");
        var nativeBefore=AnimationControls.DeformChecksum(scene);
        var renamed=scene with{Bones=scene.Bones.Select((b,i)=>b with{Name="opaque_"+(31-i*7)}).ToArray(),SkeletonRevision=Guid.NewGuid()};
        var renamedRig=rig with{SkeletonFingerprint=AnimationControls.Fingerprint(renamed)};
        check(PreparedArticulationAction.Create(RigDescriptor.Prepare(renamed,renamedRig),map,reference).Cost(opposite)==path.Cost(opposite)
            &&AnimationControls.DeformChecksum(scene)==nativeBefore,"Articulated action uses native geometry and supplied identities without native-name numerical rules or rig mutation");
    }

    static void GeometryChecks(Action<bool,string> check)
    {
        var original=Surface(.5f,1,0);var helpers=Surface(.5f,1,12);var segmented=Surface(.5f,4,0);var broad=Surface(1,1,0);
        EvaluatedPose Turn(SurfaceFixture fixture)=>AnimationService.Evaluate(fixture.Scene,null,0,
            new Dictionary<int,Transform>{{2,fixture.Scene.Bones[2].Rest with{Rotation=new(90,0,0)}}});
        var turn=Turn(original);var measured=original.Action.Measure(turn);
        // All three authored skin vertices have radius .5 about the native X
        // axis. A 90-degree turn moves each by sqrt(2)*.5. Characteristic
        // length is independently two units, giving mean normalized .125.
        check(Math.Abs(measured.MeanPositionScaleSquared-.125)<.000001
            &&Math.Abs(measured.MeanOrientationRadiansSquared-Math.PI*Math.PI/4)<.000001,
            "Area-weighted skin moments match the independently authored .125 displacement and pi²/4 orientation costs");
        check(measured.MaximumPosition==0&&measured.MeanPositionScaleSquared>0,
            "Actual rotating skin changes action when every native joint origin stays fixed");
        check(Math.Abs(original.Action.Cost(turn)-helpers.Action.Cost(Turn(helpers)))<1e-10
            &&helpers.Action.LandmarkCount==original.Action.LandmarkCount+12,
            "Twelve extra coincident unweighted native helpers add diagnostic witnesses without multiplying physical branch cost");
        check(Math.Abs(original.Action.Cost(turn)-segmented.Action.Cost(Turn(segmented)))<1e-10,
            "Splitting the same straight native branch into four twist/helper segments leaves identical physical skin action cost");
        var broadMeasurement=broad.Action.Measure(Turn(broad));
        check(Math.Abs(broadMeasurement.MeanPositionScaleSquared-.5)<.000001
            &&Math.Abs(broadMeasurement.MeanPositionScaleSquared/measured.MeanPositionScaleSquared-4)<.00001
            &&Math.Abs(broadMeasurement.MeanOrientationRadiansSquared-measured.MeanOrientationRadiansSquared)<1e-10,
            "The same native skeleton with twice the radial body extent has independently expected fourfold surface displacement while angular cost is unchanged");
        int extra=helpers.Scene.Bones.Length-1;
        var changedHelper=AnimationService.Evaluate(helpers.Scene,null,0,new Dictionary<int,Transform>{
            {extra,helpers.Scene.Bones[extra].Rest with{Rotation=new(73,0,0)}}});
        var helperWitness=helpers.Action.Measure(changedHelper);
        check(helpers.Action.Cost(changedHelper)==0&&Math.Abs(helperWitness.MaximumOrientationRadians-73*Math.PI/180)<.00001
            &&helperWitness.OrientationBone==extra&&helpers.Action.TargetBones.Contains(extra),
            "A zero-skin helper's independent authored rotation remains a precise native witness even with zero physical objective weight");
        var residual=original.Action.Residuals(turn);
        check(residual.Length==original.Action.ResidualCount&&residual.Length==original.Action.Residuals(original.Reference).Length
            &&residual.All(double.IsFinite)&&original.Action.Cost(turn)==residual.Sum(x=>x*x)
            &&original.Action.Residuals(original.Reference).All(x=>x==0),
            "Prepared action exposes fixed residual dimensions, exact-zero reference and Cost equal to the residual squared norm");

        // Equal and opposite translations of two independently weighted bones
        // cancel in the actual 50/50 skinned surface. Squaring contributions
        // before blending would incorrectly produce positive action error.
        var blended=Surface(.5f,1,0,true);
        int leaf=blended.Leaf,other=blended.Other;
        var cancel=AnimationService.Evaluate(blended.Scene,null,0,new Dictionary<int,Transform>{
            {leaf,blended.Scene.Bones[leaf].Rest with{Translation=blended.Scene.Bones[leaf].Rest.Translation+Vector3.UnitY}},
            {other,blended.Scene.Bones[other].Rest with{Translation=blended.Scene.Bones[other].Rest.Translation-Vector3.UnitY}}});
        var cancellation=blended.Action.Measure(cancel);
        check(cancellation.MeanPositionScaleSquared<1e-12&&cancellation.MaximumPosition==1
            &&blended.Scene.Meshes[0].Vertices.All(v=>Vector3.Distance(AnimationService.Skin(v,cancel),v.Position)<.000001f),
            "Cross-influence skin moments preserve independently verified cancellation while native bone displacement witnesses stay visible");
        var direct=blended.Action.Measure(Turn(blended));
        check(Math.Abs(direct.MeanPositionScaleSquared-.03125)<.000001,
            "One rotating half of the 50/50 skin produces the independently expected quarter squared surface error");

        var noSkin=original.Scene with{Meshes=[]};
        var noSkinRig=original.Rig with{SkeletonFingerprint=AnimationControls.Fingerprint(noSkin)};
        var descriptor=RigDescriptor.Prepare(noSkin,noSkinRig);var reference=AnimationService.Evaluate(noSkin,null,0);
        var fallback=PreparedMotionAction.Create(descriptor,original.Mapping,reference);
        check(fallback.SurfaceArea==0&&fallback.PositionMeasure=="Rest-edge two-point quadrature"
            &&fallback.Cost(reference)==0&&fallback.LandmarkCount==noSkin.Bones.Length,
            "A geometry-free rig declares its rest-edge fallback and retains all native witnesses without invented equal bone masses");
        var morph=original.Scene.Meshes.Select(m=>m.Vertices.Select(v=>v.Position).ToArray()).ToArray();
        morph[0][0]+=Vector3.UnitY;
        bool stale=false;try{original.Action.Cost(turn with{GeometryPositions=morph});}catch(NotSupportedException){stale=true;}
        check(stale,"Changed geometry-layer data cannot silently reuse another frame's prepared action moments");
        var preparedMorph=original.Reference with{GeometryPositions=morph};
        var morphAction=PreparedMotionAction.Create(original.Descriptor,original.Mapping,preparedMorph);
        check(morphAction.Cost(preparedMorph)==0&&morphAction.MomentDiscardedTraceFraction<1e-10,
            "An explicitly prepared morph frame has exact zero reference cost and reports its numerical moment-factor truncation");
        var hiddenScene=original.Scene with{Meshes=original.Scene.Meshes.Select(m=>m with{
            DrawCalls=[new(null,null,false,0,0,0)]}).ToArray()};
        var hiddenReference=AnimationService.Evaluate(hiddenScene,null,0);
        var hiddenAction=PreparedMotionAction.Create(RigDescriptor.Prepare(hiddenScene,original.Rig),original.Mapping,hiddenReference);
        var hiddenTurn=AnimationService.Evaluate(hiddenScene,null,0,new Dictionary<int,Transform>{{2,hiddenScene.Bones[2].Rest with{Rotation=new(90,0,0)}}});
        check(hiddenAction.DefaultHiddenMeshCount==1&&Math.Abs(hiddenAction.Cost(hiddenTurn)-original.Action.Cost(turn))<1e-10
            &&hiddenAction.VisibilityPolicy.Contains("not represented")&&morphAction.GeometryContext=="Prepared geometry-layer vertex data",
            "The action receipt explicitly includes default-hidden native skin and prepared morphs without claiming animated preview-visibility support");
    }

    sealed record SurfaceFixture(Scene Scene,ControlRigSetup Rig,RigDescriptor Descriptor,RetargetMappingReport Mapping,
        EvaluatedPose Reference,PreparedMotionAction Action,int Leaf,int Other);
    static SurfaceFixture Surface(float radius,int segments,int helpers,bool blended=false)
    {
        var bones=new List<Bone>{
            new("opaque_origin",-1,Transform.Identity),new("opaque_carrier",0,Transform.Identity),
            new("opaque_driver",1,Transform.Identity with{Translation=Vector3.UnitX})};
        for(int i=0;i<segments;i++)bones.Add(new("opaque_segment_"+i,bones.Count-1,Transform.Identity with{Translation=Vector3.UnitX/segments}));
        int leaf=bones.Count-1,other=-1;
        if(blended){other=bones.Count;bones.Add(new("opaque_other_region",1,Transform.Identity with{Translation=new(2,0,0)}));}
        for(int i=0;i<helpers;i++)bones.Add(new("opaque_extra_"+i,leaf,Transform.Identity));
        var joints=blended?new Vector4(leaf,other,0,0):new Vector4(leaf,0,0,0);
        var weights=blended?new Vector4(.5f,.5f,0,0):new Vector4(1,0,0,0);
        var vertices=new[]{new Vector3(2,radius,0),new(2,-radius,0),new(2,0,radius)}
            .Select(p=>new Vertex(p,Vector3.UnitX,Vector2.Zero,joints,weights,Vector4.One)).ToArray();
        var scene=new Scene("Original physical-surface objective fixture",bones.ToArray(),[new("Original triangle",vertices,[0,1,2])]);
        var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[]){
            BodyBone=1,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Independent model units"),
            BoneRoles=bones.Select((b,i)=>new RigMapping(i==0?"RootMotion":i==1?"Hips":"Part."+i,i,"Supplied exact correspondence",true)).ToArray()};
        var descriptor=RigDescriptor.Prepare(scene,rig);var reference=AnimationService.Evaluate(scene,null,0);
        var mapping=new RetargetMappingReport(bones.Select((b,i)=>new RetargetBonePair("Supplied."+i,i,i,"Independent exact source correspondence")).ToArray(),[],[]);
        return new(scene,rig,descriptor,mapping,reference,PreparedMotionAction.Create(descriptor,mapping,reference),leaf,other);
    }
}
