using System.Numerics;
using FighterPort.Core;

// Cartesian skin/surface expectations are authored independently of the
// fitter's sample selection, soft minimum, Jacobian and objective equations.
internal static class SceneBodyFittingChecks
{
    sealed record Fixture(FighterProject Project,ControlRigSetup Rig);
    sealed record Case(Fixture Source,Fixture Target,EvaluatedPose Authentic,Transform[] Input,Transform[] Output,
        SceneBodyFitting Fitting,bool Preserved);

    public static void Run(Action<bool,string> check)
    {
        var floor=new GroundContactSettings{ExplicitSurface=true,SourceFloor=0,TargetFloor=0};
        // Source body lowers .5 while its support child rises .5, keeping the
        // actual source triangle on Y=0. The target's independent edge ratio2
        // makes that raw triangle hover at Y=.5; fitting must measure skin.
        var source=Motion(Make("opaque-a",2,0),1.5f,-1.5f);
        var target=Make("opaque-b",4,0);
        var grounded=Fit(source,target,floor);
        var groundedPose=Pose(target.Project.Scene,grounded.Output);
        check(Math.Abs(Min(source.Project.Scene,grounded.Authentic,Vector3.UnitY))<.000001f&&
            Math.Abs(Min(target.Project.Scene,Pose(target.Project.Scene,grounded.Input),Vector3.UnitY)-.5f)<.00001f&&
            Math.Abs(Min(target.Project.Scene,groundedPose,Vector3.UnitY))<.00003f,
            "Measured skinned ground registration removes an independent half-unit target hover while the authentic unequal-proportion source triangle stays on Y=0");
        check(Math.Abs(groundedPose.World[1].Translation.Y-3)<.00003f&&grounded.Fitting.Enabled&&
            grounded.Output.Select((value,index)=>index==1||value==grounded.Input[index]).All(v=>v)&&
            grounded.Output.Select((value,index)=>value.Rotation==grounded.Input[index].Rotation&&value.Scale==grounded.Input[index].Scale).All(v=>v),
            "Scene fitting changes only the reviewed native body's local translation, preserving actor, child offsets, helpers, rotations, winding and authored scale");

        var reverseSource=Motion(Make("reverse-a",4,0),3,-3);
        var reverse=Fit(reverseSource,Make("reverse-b",2,0),floor);
        check(Math.Abs(Min(reverse.Source.Project.Scene,reverse.Authentic,Vector3.UnitY))<.000001f&&
            Math.Abs(Min(reverse.Target.Project.Scene,Pose(reverse.Target.Project.Scene,reverse.Output),Vector3.UnitY))<.00003f,
            "Independent reverse proportion transfer measures the smaller target's actual support geometry instead of relying on a directional character rule");

        var thin=Make("same-graph-source",2,0);var thick=Make("same-graph-target",2,-1);
        var volume=Fit(thin,thick,floor);
        check(volume.Fitting.ChangesGeometryIdentity&&Math.Abs(Min(thick.Project.Scene,Pose(thick.Project.Scene,volume.Input),Vector3.UnitY)+1)<.000001f&&
            Math.Abs(Min(thick.Project.Scene,Pose(thick.Project.Scene,volume.Output),Vector3.UnitY))<.00003f&&
            Math.Abs(volume.Output[1].Translation.Y-3)<.00003f,
            "Identical native skeletons with a one-unit thicker skinned underside receive a geometry-dependent body fit rather than an exact-skeleton bypass");

        // Half the skin follows the body descendant and half follows the
        // actor. Moving the body .5 would leave the point .25 below the floor;
        // an actual one-unit body move is independently necessary here.
        var weighted=Fit(Make("weighted-source",2,0,alpha:.5f),Make("weighted-target",2,-.5f,alpha:.5f),floor);
        check(Math.Abs(weighted.Output[1].Translation.Y-weighted.Input[1].Translation.Y-1)<.00004f&&
            Math.Abs(Min(weighted.Target.Project.Scene,Pose(weighted.Target.Project.Scene,weighted.Output),Vector3.UnitY))<.00003f,
            "Mixed actor/body skin weights require the independently known one-unit body correction, not a mistaken unit skin derivative");

        var airborne=Fit(Motion(Make("air-source",2,0),1.5f,-.5f),Make("air-target",4,0),floor);
        check(Math.Abs(Min(airborne.Source.Project.Scene,airborne.Authentic,Vector3.UnitY)-1)<.000001f&&
            Math.Abs(Min(airborne.Target.Project.Scene,Pose(airborne.Target.Project.Scene,airborne.Output),Vector3.UnitY)-1)<.00004f,
            "A source triangle one unit above the supplied floor retains positive clearance rather than being declared planted or pulled onto the ground");

        var centimetres=Fit(source,Make("native-cm",4,0,units:100),floor);
        check(Math.Abs(Min(centimetres.Target.Project.Scene,Pose(centimetres.Target.Project.Scene,centimetres.Output),Vector3.UnitY))<.003f&&
            Math.Abs(centimetres.Output[1].Translation.Y/100-3)<.00004f,
            "Explicit hundred-unit native coordinates preserve the independent Cartesian floor and body position without double anatomical or file-unit scaling");
        var raised=Fit(source,target,floor with{TargetFloor=3});
        check(Math.Abs(Min(target.Project.Scene,Pose(target.Project.Scene,raised.Output),Vector3.UnitY)-3)<.00004f&&raised.Output[0]==raised.Input[0],
            "Different supplied destination floor height is expressed through the editable body while the native actor/root transform remains unchanged");

        var turned=Fit(Motion(Make("turned-source",2,0,actorDegrees:90),1.5f,-1.5f),Make("turned-target",4,0,actorDegrees:90),floor);
        check(Math.Abs(Min(turned.Target.Project.Scene,Pose(turned.Target.Project.Scene,turned.Output),-Vector3.UnitX))<.00004f&&
            Vector3.Distance(Pose(turned.Target.Project.Scene,turned.Output).World[1].Translation,new(-3,0,0))<.00005f,
            "A native actor quarter-turn and reviewed rotated up-axis preserve the same independent floor registration in world coordinates");

        // This is the currently represented top/wall half-space corner. A
        // genuinely finite ledge remains a separate capability/acceptance gate.
        var edge=floor with{SourceEdge=new(Vector3.UnitZ,0),TargetEdge=new(Vector3.UnitZ,0)};
        var hanging=Fit(Make("outside-source",2,-2,z:-1),Make("outside-target",4,-4,z:-1),edge);
        var hangPose=Pose(hanging.Target.Project.Scene,hanging.Output);
        var hangPoints=hanging.Target.Project.Scene.Meshes.SelectMany(m=>m.Vertices).Select(v=>AnimationService.Skin(v,hangPose)).ToArray();
        check(hangPoints.All(p=>Math.Abs(p.Y+2)<.00005f&&p.Z<-.1f)&&hangPoints.All(p=>new SupportSurface(Vector3.UnitY,0,new(Vector3.UnitZ,0)).Penetration(p)==0),
            "An independently reachable exterior hanging triangle preserves source Y=-2 below the edge instead of a conflicting top-plane clamp; finite ledge support is not claimed");

        // One weighted region contains BOTH near-top and outside/below-top
        // points. Moving the complete target by +2Y exactly reproduces every
        // authentic point, so independent feature selection cannot justify a
        // compromise that changes its shape or shifts it to a different side.
        Vector3[] cornerPoints=[new(-.5f,0,.25f),new(.5f,0,.25f),new(0,-2,-.5f)];
        var mixedSource=WithPoints(Make("mixed-corner-source",2,0),cornerPoints);
        var mixedTarget=WithPoints(Make("mixed-corner-target",2,0),cornerPoints.Select(p=>p-new Vector3(0,2,0)).ToArray());
        var mixed=Fit(mixedSource,mixedTarget,edge);var mixedPose=Pose(mixedTarget.Project.Scene,mixed.Output);
        check(mixedTarget.Project.Scene.Meshes[0].Vertices.Select((v,i)=>Vector3.Distance(AnimationService.Skin(v,mixedPose),cornerPoints[i])).Max()<.00005f&&
            Vector3.Distance(mixed.Output[1].Translation-mixed.Input[1].Translation,new(0,2,0))<.00005f&&
            mixed.Output.Select((value,index)=>value.Rotation==mixed.Input[index].Rotation&&value.Scale==mixed.Input[index].Scale).All(v=>v),
            "A single mixed corner region with known exact +2Y registration reproduces every source surface point without conflicting top/wall observations or added pose distortion");

        // The large free region strongly prefers a downward translation, but
        // its aggregate area cannot purchase penetration of the smaller sole.
        // Source broad skin is atY0 (the source body lowers its authored .5
        // rest position by .5), target broad skin atY3; the independent
        // target support starts atY.5, so the largest safe downward step is.5.
        Vector3[] Free(float y)=>[new(-10,y,-10),new(10,y,-10),new(0,y,10)];
        var broad=Fit(AddTriangle(source,Free(.5f),2),AddTriangle(target,Free(3.5f),2),floor);
        var broadPose=Pose(broad.Target.Project.Scene,broad.Output);var broadCertificate=broad.Fitting.Receipt();
        check(Math.Abs(Min(broad.Target.Project.Scene,Pose(broad.Target.Project.Scene,broad.Input),Vector3.UnitY)-.5f)<.00001f&&
            Math.Abs(Min(broad.Target.Project.Scene,broadPose,Vector3.UnitY))<.00004f&&
            broadCertificate.ClearanceClippedEvaluations>0&&broadCertificate.ClearanceVertices>=6&&broadCertificate.MaximumFinalPenetration<.00004f&&
            broad.Output[0]==broad.Input[0]&&broad.Output.Select((value,index)=>value.Rotation==broad.Input[index].Rotation&&value.Scale==broad.Input[index].Scale).All(v=>v),
            "An area-dominant free body region cannot drive a small initially clear sole below the floor; the actual full-skin clearance projection retains the independent Y=0 limit");

        // Independent wall plane: source broad skin actually touches Z=0
        // below the top, while matching target skin starts at Z=-2. The
        // requested +2Z registration conflicts with the smaller hanging
        // triangle, whose forward-most material point starts at Z=-.5.
        // This evidence is genuinely near the source wall, rather than a
        // distant free region that the source-proximity policy discounts.
        Vector3[] BroadWall(float z)=>[new(-25,-3.9f,z),new(25,-3.9f,z),new(0,-4.2f,z)];
        var edgeClipped=Fit(AddTriangle(Make("edge-clear-source",2,-1,z:-1),BroadWall(0),2),
            AddTriangle(Make("edge-clear-target",4,-1,z:-1),BroadWall(-2),2),edge);
        var edgePose=Pose(edgeClipped.Target.Project.Scene,edgeClipped.Output);var edgeCertificate=edgeClipped.Fitting.Receipt();
        var corner=new SupportSurface(Vector3.UnitY,0,new(Vector3.UnitZ,0));
        var protectedPoints=edgeClipped.Target.Project.Scene.Meshes[0].Vertices.Select(v=>AnimationService.Skin(v,edgePose)).ToArray();
        check(edgeCertificate.ClearanceClippedEvaluations>0&&edgeCertificate.ClearanceVertices>=6&&
            protectedPoints.All(p=>p.Y<-.5f&&p.Z<.00004f)&&
            edgeClipped.Target.Project.Scene.Meshes.SelectMany(m=>m.Vertices).Max(v=>corner.Penetration(AnimationService.Skin(v,edgePose)))<.00004f&&
            edgeCertificate.MaximumInitialPenetration==0&&edgeCertificate.MaximumFinalPenetration<.00004f&&edgeClipped.Output[0]==edgeClipped.Input[0],
            "A genuine broad source wall-support preference cannot push clear hanging target skin through the wall; actual full-skin corner clipping preserves exterior space and native actor placement");

        var defective=Fit(Make("observed-below-floor",2,-.5f),Make("clear-destination",2,.25f),floor);
        check(Math.Abs(Min(defective.Source.Project.Scene,defective.Authentic,Vector3.UnitY)+.5f)<.000001f&&
            Math.Abs(Min(defective.Target.Project.Scene,Pose(defective.Target.Project.Scene,defective.Output),Vector3.UnitY))<.00003f&&
            defective.Fitting.Receipt().SourcePenetrationObservations>0,
            "Source floor intersection is retained as an observation rather than copied into the target or silently rated as accepted source support");

        var hidden=Fit(thin,thick,floor,visible:_=>false);
        check(hidden.Output.SequenceEqual(hidden.Input),
            "An explicitly hidden source triangle supplies no observed surface relationship and cannot manufacture a visible-source body correction");
        var off=Fit(source,target,floor,enabled:false);var absent=Fit(source,target,null);
        check(!off.Fitting.Enabled&&off.Output.SequenceEqual(off.Input)&&!absent.Fitting.Enabled&&absent.Output.SequenceEqual(absent.Input),
            "Disabled automatic fitting and absent explicit scene context preserve the unmodified prepared native pose");
        var identity=Fit(thin,thin,floor);
        check(!identity.Fitting.ChangesGeometryIdentity&&identity.Output.SequenceEqual(identity.Input),
            "Exact native graph, skin and supplied geometry context preserve unchanged local values without inventing a scene correction");
        var identityMap=RetargetBoneMapping.Build(thin.Project.Scene,thin.Rig,thin.Project.Scene,thin.Rig);
        var identityLedger=RetargetChannelLedger.Build(thin.Project.Scene,thin.Project.Clips[0],thin.Project.Scene,identityMap,RootMotionPolicy.Preserve,true,sceneBody:identity.Fitting.Receipt());
        check(identityLedger.SceneBodyDerivedChannels is null,
            "An unchanged geometry-identity result does not falsely report derived body channels merely because scene fitting was enabled");
        var changedMap=RetargetBoneMapping.Build(source.Project.Scene,source.Rig,target.Project.Scene,target.Rig);
        var changedLedger=RetargetChannelLedger.Build(source.Project.Scene,source.Project.Clips[0],target.Project.Scene,changedMap,RootMotionPolicy.Preserve,sceneBody:grounded.Fitting.Receipt());
        check(changedLedger.SceneBodyDerivedChannels is {SourceBody.BoneIndex:1,TargetBody.Bone.BoneIndex:1} derived&&
            derived.TargetBody.Channels.SequenceEqual(new[]{"Translation.X","Translation.Y","Translation.Z"}),
            "An actually applied body fit records its exact native source/target identities and three derived translation channels separately from unchanged rotations/scales");
        var renamed=Fit(thin,Make("renamed-identical",2,0),floor);
        check(renamed.Output.SequenceEqual(renamed.Input),
            "Supplied equal-geometry correspondence retains the same native pose despite unrelated names and a conservative nonidentity preparation path");

        var permuted=Fit(source,Permute(target),floor);
        check(Math.Abs(Min(permuted.Target.Project.Scene,Pose(permuted.Target.Project.Scene,permuted.Output),Vector3.UnitY))<.00003f&&
            Vector3.Distance(Pose(permuted.Target.Project.Scene,permuted.Output).World[1].Translation,groundedPose.World[1].Translation)<.00003f,
            "Reviewed correspondences survive valid renamed sibling storage permutation and produce the same independently measured body and floor placement");
        var repeated=Fit(source,target,floor);
        check(repeated.Output.SequenceEqual(grounded.Output),
            "Repeated preparation of the same source pose and exact rig/scene dependencies produces identical body keys without playback-history seeds");

        var layered=WithSourceLayers(Make("authentic-layered",2,0));var layeredTarget=Make("layer-destination",2,-.5f);
        string layerOriginal=RetargetDependencies.Hash(new{Source=layered.Project,Target=layeredTarget.Project});
        var layerRequest=new RetargetRequest(layered.Project,layered.Project.Clips[0],layered.Rig,layeredTarget.Project,layeredTarget.Rig,RootMotionPolicy.Preserve,false,default,null,floor,new(){AutomaticContacts=false,VolumePolicy=BodyVolumePolicy.RequireVertexClearance},null,null,null,null);
        var layerSourceRig=RigDescriptor.Prepare(layered.Project.Scene,layered.Rig);var layerTargetRig=RigDescriptor.Prepare(layeredTarget.Project.Scene,layeredTarget.Rig);
        var layerBaseline=new BaselineMotionTransfer(layerRequest,layerSourceRig,layerTargetRig);var layerFit=new BodyReachFitting(layerRequest,layerSourceRig,layerTargetRig,layerBaseline);
        var preparedGeometry=new PreparedGeometry(layered.Project,layerRequest.Clip);
        var fractional=preparedGeometry.Evaluate(.5);var nativePoint=layered.Project.Scene.Meshes[0].Vertices[0];
        var observedPoint=ContactGeometry.Point(new SkinSurfacePoint(nativePoint.Position,nativePoint.Joints,nativePoint.Weights){Mesh=0,Vertex=0},fractional);
        var half=layerFit.Evaluate(.5);
        check(Math.Abs(observedPoint.Y-.5f)<.000001f&&
            Math.Abs(Min(layeredTarget.Project.Scene,Pose(layeredTarget.Project.Scene,half),Vector3.UnitY)-.5f)<.00004f,
            "Actual same-name prepared source SHP0 raises an independently authored triangle by half a unit and the normal scene/body path fits its fractional clearance");
        var hiddenFrame=layerFit.Evaluate(1);
        check(preparedGeometry.MeshVisible(0,0)&&!preparedGeometry.MeshVisible(0,1)&&
            hiddenFrame.SequenceEqual(layerBaseline.Evaluate(1))&&Math.Abs(Min(layeredTarget.Project.Scene,Pose(layeredTarget.Project.Scene,hiddenFrame),Vector3.UnitY)+.5f)<.000001f,
            "Actual source VIS0 hides the only observation at its native held boundary, leaving the target baseline unchanged instead of fitting invisible morph geometry");
        var halfAgain=layerFit.Evaluate(.5);
        check(halfAgain.SequenceEqual(half)&&layerOriginal==RetargetDependencies.Hash(new{Source=layered.Project,Target=layeredTarget.Project}),
            "Fractional morph/visibility sampling out of order is deterministic and leaves source channels, native layers, geometry, rigs and target authoring unchanged");
        check(new[]{grounded,reverse,volume,weighted,airborne,centimetres,raised,turned,hanging,mixed,broad,edgeClipped,defective,hidden,off,absent,identity,renamed,permuted,repeated}.All(c=>c.Preserved),
            "All scene/body proposals preserve authentic source channels, input local arrays, native rest hierarchy, geometry, weights, scales and project identities");
    }

    static Case Fit(Fixture source,Fixture target,GroundContactSettings? ground,bool enabled=true,Func<int,bool>? visible=null)
    {
        string before=RetargetDependencies.Hash(new{Source=source.Project,Target=target.Project,SourceRig=source.Rig,TargetRig=target.Rig});
        // These original clearance certificates intentionally exercise the
        // explicit strict policy; the default report-only policy has separate
        // independently authored support/overlap expectations.
        var request=new RetargetRequest(source.Project,source.Project.Clips[0],source.Rig,target.Project,target.Rig,RootMotionPolicy.Preserve,false,default,null,ground,new(){AutomaticFitting=enabled,AutomaticContacts=false,VolumePolicy=BodyVolumePolicy.RequireVertexClearance},null,null,null,null);
        var a=RigDescriptor.Prepare(source.Project.Scene,source.Rig);var b=RigDescriptor.Prepare(target.Project.Scene,target.Rig);
        var baseline=new BaselineMotionTransfer(request,a,b);var authentic=AnimationService.Evaluate(source.Project.Scene,request.Clip,0);
        var local=baseline.EvaluatePose(authentic);string localHash=RetargetDependencies.Hash(local);
        var fitting=new SceneBodyFitting(request,a,b,baseline);var output=fitting.Fit(authentic,local,visible);
        bool preserved=before==RetargetDependencies.Hash(new{Source=source.Project,Target=target.Project,SourceRig=source.Rig,TargetRig=target.Rig})&&localHash==RetargetDependencies.Hash(local);
        return new(source,target,authentic,local,output,fitting,preserved);
    }
    static Fixture Make(string label,float bodyHeight,float underside,float units=1,float alpha=1,float z=0,float actorDegrees=0)
    {
        var bones=new[]{new Bone(label+"_native0",-1,Transform.Identity with{Rotation=new(0,0,actorDegrees)}),
            new(label+"_native1",0,Transform.Identity with{Translation=new(0,bodyHeight*units,0)}),
            new(label+"_native2",1,Transform.Identity with{Translation=new(0,units,0)}),
            new(label+"_native3",1,Transform.Identity with{Translation=new(0,-bodyHeight*units,0)}),
            new(label+"_native4",0,Transform.Identity with{Translation=new(0,.2f*units,.3f*units)})};
        var turn=Matrix4x4.CreateRotationZ(actorDegrees*MathF.PI/180);
        var vertices=new[]{new Vector3(-.5f,underside,z-.25f),new(.5f,underside,z-.25f),new(0,underside,z+.5f)}
            .Select(p=>new Vertex(Vector3.Transform(p*units,turn),Vector3.TransformNormal(Vector3.UnitY,turn),Vector2.Zero,new(3,0,0,0),new(alpha,1-alpha,0,0),Vector4.One)).ToArray();
        var scene=new Scene(label,bones,[new("Original Cartesian triangle",vertices,[0,1,2])]);
        var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[]){BodyBone=1,
            Axes=new(Vector3.TransformNormal(Vector3.UnitX,turn),Vector3.TransformNormal(Vector3.UnitY,turn),Vector3.UnitZ),Coordinates=new(units,"Independent native coordinate units"),
            BoneRoles=new[]{"RootMotion","Hips","Chest","Surface.End","Other.Branch"}.Select((role,index)=>new RigMapping(role,index,"Supplied independent Cartesian binding",true)).ToArray()};
        var clip=new AnimationClip(Guid.NewGuid(),"Independent source",2,new(30),false,[]);
        return new(new(1,Guid.NewGuid(),label,scene,[clip]){ControlRigs=[rig]},rig);
    }
    static Fixture Motion(Fixture fixture,float bodyY,float supportLocalY)
    {
        Curve?[] Position(float y)=>[null,null,null,null,null,null,null,new([new(0,y)]),null];
        var clip=fixture.Project.Clips[0] with{Tracks=[new(fixture.Project.Scene.Bones[1].Name,Position(bodyY)),new(fixture.Project.Scene.Bones[3].Name,Position(supportLocalY)),
            new(fixture.Project.Scene.Bones[4].Name,[new([new(0,1.25f)]),null,null,null,null,new([new(0,720)]),null,null,null])]};
        return fixture with{Project=fixture.Project with{Clips=[clip]}};
    }
    static Fixture WithPoints(Fixture fixture,Vector3[] positions)=>fixture with{Project=fixture.Project with{Scene=fixture.Project.Scene with
    {Meshes=[fixture.Project.Scene.Meshes[0] with{Vertices=fixture.Project.Scene.Meshes[0].Vertices.Select((v,i)=>v with{Position=positions[i]}).ToArray()}]}}};
    static Fixture AddTriangle(Fixture fixture,Vector3[] positions,int bone)
    {
        var mesh=new Mesh("Independent area-dominant free surface",positions.Select(p=>new Vertex(p,Vector3.UnitY,Vector2.Zero,new(bone,0,0,0),new(1,0,0,0),Vector4.One)).ToArray(),[0,1,2]);
        return fixture with{Project=fixture.Project with{Scene=fixture.Project.Scene with{Meshes=fixture.Project.Scene.Meshes.Append(mesh).ToArray()}}};
    }
    static Fixture WithSourceLayers(Fixture fixture)
    {
        // Original procedural prepared data, not a format-reader/native-export
        // fixture or a claim that these synthetic resources exist on disk.
        string path=Path.GetFullPath("original-scene-body-fixture.pac");
        var resource=new NativeResourceRef(path,"root/0/0/0","original-procedural-scene-body","CHR0",4);
        var clip=fixture.Project.Clips[0] with{Name="OriginalSceneGeometry",Source=new(Guid.NewGuid(),resource,null,fixture.Project.Scene.SkeletonRevision,new(30)){NativeName="OriginalSceneGeometry"}};
        var shape=new NativeAnimationLayer(Guid.NewGuid(),clip.Name,"SHP0",2,false,new(30),resource with{Entry="root/0/5/0",Format="SHP0"},[],"Original prepared fixture")
        {Morphs=[new("Independent triangle",0,MorphAttribute.Position,[new("Raise",new([new(0,0),new(1,1)],Interpolation.Linear),fixture.Project.Scene.Meshes[0].Vertices.Select(v=>new Vector4(v.Position+Vector3.UnitY,0)).ToArray())])]};
        string visibilityBone=fixture.Project.Scene.Bones[3].Name;
        var visibility=new NativeAnimationLayer(Guid.NewGuid(),clip.Name,"VIS0",2,false,new(30),resource with{Entry="root/0/3/0",Format="VIS0"},[new(visibilityBone,0,[],[true,false])],"Original prepared fixture");
        return fixture with{Project=fixture.Project with{Clips=[clip],NativeLayers=[shape,visibility],Scene=fixture.Project.Scene with{Meshes=[fixture.Project.Scene.Meshes[0] with{VisibilityBone=visibilityBone}]}}};
    }
    static Fixture Permute(Fixture fixture)
    {
        int[] order=[0,1,3,2,4];var inverse=order.Select((old,index)=>(old,index)).ToDictionary(p=>p.old,p=>p.index);
        var scene=fixture.Project.Scene with{SkeletonRevision=Guid.NewGuid(),Bones=order.Select((old,index)=>fixture.Project.Scene.Bones[old] with{Name="reviewed_opaque_"+index,Parent=fixture.Project.Scene.Bones[old].Parent is var p&&p>=0?inverse[p]:-1}).ToArray(),
            Meshes=fixture.Project.Scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Joints=new(inverse[(int)v.Joints.X],inverse[(int)v.Joints.Y],inverse[(int)v.Joints.Z],inverse[(int)v.Joints.W])}).ToArray()}).ToArray()};
        var rig=fixture.Rig with{Id=Guid.NewGuid(),SkeletonFingerprint=AnimationControls.Fingerprint(scene),BodyBone=inverse[1],BoneRoles=fixture.Rig.BoneRoles.Select(r=>r with{Bone=inverse[r.Bone]}).ToArray()};
        return new(fixture.Project with{Scene=scene,Clips=[],ControlRigs=[rig]},rig);
    }
    static EvaluatedPose Pose(Scene scene,Transform[] local)=>AnimationService.Evaluate(scene,null,0,local.Select((value,index)=>(value,index)).ToDictionary(p=>p.index,p=>p.value));
    static float Min(Scene scene,EvaluatedPose pose,Vector3 up)=>scene.Meshes.SelectMany(m=>m.Vertices).Min(v=>Vector3.Dot(AnimationService.Skin(v,pose),up));
}
