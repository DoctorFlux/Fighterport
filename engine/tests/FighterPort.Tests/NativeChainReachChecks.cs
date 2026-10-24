using System.Numerics;
using FighterPort.Core;

internal static class NativeChainReachChecks
{
    public static void Run(Action<bool,string> check)
    {
        var (scene,rig)=Fixture(1);var input=scene.Bones.Select(b=>b.Rest).ToArray();
        input[2]=input[2] with{Rotation=new(0,720,0)};
        int[] path=[1,2,3,4];var descriptor=RigDescriptor.Prepare(scene,rig);string before=AnimationControls.DeformChecksum(scene);
        var goal=new Vector3(2,2,0);
        var result=NativeChainReach.Solve(descriptor,input,path,goal,Vector3.UnitY);
        var output=Pose(scene,result.Local);
        check(result.Status=="Solved"&&Vector3.Distance(output.World[4].Translation,goal)<.00002f,
            "An arbitrary four-joint native path reaches an independent Cartesian goal with fixed root and native lengths");
        check(path.Zip(path.Skip(1),(a,b)=>Math.Abs(Vector3.Distance(output.World[a].Translation,output.World[b].Translation)-1)).Max()<.00001f&&
              Vector3.Distance(output.World[1].Translation,new(0,2,0))<.00001f,
            "The solved native path keeps each independently authored unit segment and its fixed root without solver stretch");
        check(output.World[2].Translation.Y>2&&output.World[3].Translation.Y>2,
            "A source-derived transverse bend side survives shortening a previously straight native path");
        var skin=AnimationService.Skin(scene.Meshes[0].Vertices[0],output);
        check(Vector3.Distance(skin,new(2.2f,2.3f,0))<.00003f&&result.DistalOrientationError<.001f,
            "Preserving distal world orientation places actual weighted skin at the independently expected goal plus authored surface offset");
        check(result.Local.Select((t,i)=>(t,i)).All(p=>p.t.Translation==input[p.i].Translation&&p.t.Scale==input[p.i].Scale)&&
              result.Local[5]==input[5]&&AnimationControls.DeformChecksum(scene)==before,
            "Reach changes only existing path rotations and preserves unrelated detail channels, translations, scales, hierarchy, binds and weights");
        check(Math.Abs(result.Local[2].Rotation.Y-720)<.001f,
            "The reach swing retains an authored two-turn local winding branch rather than reducing it to zero");
        var unreachable=NativeChainReach.Solve(descriptor,input,path,new(4,2,0),Vector3.UnitY);
        check(unreachable.Status=="Unreachable"&&unreachable.Local.SequenceEqual(input)&&unreachable.PositionError>0,
            "A goal beyond independent total length is explicitly unreachable and neither moved nor made feasible by stretching");
        var locked=NativeChainReach.Solve(descriptor,input,path,goal,Vector3.UnitY,[new(1,Vector3.Zero,Vector3.Zero,"Independently fixed native rotation",true)]);
        check(locked.Status=="LimitConflict"&&locked.Local.SequenceEqual(input),
            "A proposed correction that violates supplied reviewed native limits reports a conflict and does not relax the limit");
        var outOfBounds=input.ToArray();outOfBounds[1]=outOfBounds[1] with{Rotation=new(0,0,30)};
        var matchingGoal=Pose(scene,outOfBounds).World[4].Translation;
        var matchedLimit=NativeChainReach.Solve(descriptor,outOfBounds,path,matchingGoal,Vector3.UnitY,[new(1,new(-180,-180,-10),new(180,180,10),"Independently reviewed ten-degree native Z bound",true)]);
        check(matchedLimit.Status=="LimitConflict"&&matchedLimit.PositionError==0&&matchedLimit.Local.SequenceEqual(outOfBounds),
            "An exactly matched endpoint cannot conceal an existing thirty-degree pose outside a reviewed ten-degree joint limit");
        var affine=input.ToArray();affine[0]=affine[0] with{Scale=new(1,2,1)};
        var unsupported=NativeChainReach.Solve(descriptor,affine,path,goal,Vector3.UnitY);
        check(unsupported.Status=="Unsupported"&&unsupported.Local.SequenceEqual(affine),
            "Unverified nonuniform ancestor kinematics reports a precise capability result without normalizing native scales");
        var (longNative,longRig)=Fixture(1e7f);var longDescriptor=RigDescriptor.Prepare(longNative,longRig);
        foreach(var tinyScale in new[]{new Vector3(1e-7f,-1e-7f,1e-7f),new Vector3(1e-7f,9e-7f,1e-7f)})
        {
            var tiny=longNative.Bones.Select(b=>b.Rest).ToArray();tiny[0]=tiny[0] with{Scale=tinyScale};
            var tinyPose=Pose(longNative,tiny);var tinyGoal=tinyPose.World[path[0]].Translation+new Vector3(2,.4f,.3f);
            var tinyResult=NativeChainReach.Solve(longDescriptor,tiny,path,tinyGoal,Vector3.UnitY);
            check(path.Zip(path.Skip(1),(a,b)=>Math.Abs(Vector3.Distance(tinyPose.World[a].Translation,tinyPose.World[b].Translation)-1)).Max()<.00001f&&
                tinyResult.Status=="Unsupported"&&tinyResult.Local.SequenceEqual(tiny),
                $"Tiny authored scale {tinyScale} with independent unit world segments rejects reflected or nine-to-one affine reach without mutation; an absolute scale epsilon cannot admit it");
        }
        var tinyUniform=longNative.Bones.Select(b=>b.Rest).ToArray();tinyUniform[0]=tinyUniform[0] with{Scale=new(1e-7f)};
        var tinyUniformGoal=Pose(longNative,tinyUniform).World[path[0]].Translation+new Vector3(2,.4f,.3f);
        var uniformResult=NativeChainReach.Solve(longDescriptor,tinyUniform,path,tinyUniformGoal,Vector3.UnitY);
        check(uniformResult.Status=="Solved"&&Vector3.Distance(Pose(longNative,uniformResult.Local).World[path[^1]].Translation,tinyUniformGoal)<.00003f&&
            uniformResult.Local.Select((t,i)=>t.Scale==tinyUniform[i].Scale&&t.Translation==tinyUniform[i].Translation).All(v=>v),
            "A positive uniform tiny actor scale with ordinary Cartesian world reach stays supported without changing authored scale or translation");
        foreach(var other in new[]{new Vector3(2.2f,2.2f,0),new(1.8f,2.1f,.3f),goal})
            _=NativeChainReach.Solve(descriptor,input,path,other,Vector3.UnitY);
        var repeated=NativeChainReach.Solve(descriptor,input,path,goal,Vector3.UnitY);
        check(repeated.Local.SequenceEqual(result.Local)&&repeated.PositionError==result.PositionError,
            "Reach is deterministic under random evaluation order and does not use previous playback or solver history");
        var moved=input.ToArray();moved[0]=moved[0] with{Rotation=new(0,90,0),Translation=new(5,1,2)};
        var actor=moved[0].Matrix;var covariant=NativeChainReach.Solve(descriptor,moved,path,Vector3.Transform(goal,actor),Vector3.TransformNormal(Vector3.UnitY,actor));
        var movedPose=Pose(scene,covariant.Local);
        check(covariant.Status=="Solved"&&Vector3.Distance(movedPose.World[4].Translation,Vector3.Transform(goal,actor))<.00003f&&
              Vector3.Distance(AnimationService.Skin(scene.Meshes[0].Vertices[0],movedPose),Vector3.Transform(new(2.2f,2.3f,0),actor))<.00005f,
            "Actor rotation/translation equivalence holds for actual reach and skin without a world-axis pole or floor rule");
        var (large,largeRig)=Fixture(10);var largeInput=large.Bones.Select(b=>b.Rest).ToArray();largeInput[2]=largeInput[2] with{Rotation=new(0,720,0)};
        var scaled=NativeChainReach.Solve(RigDescriptor.Prepare(large,largeRig),largeInput,path,goal*10,Vector3.UnitY);
        check(scaled.Status=="Solved"&&Vector3.Distance(Pose(large,scaled.Local).World[4].Translation,goal*10)<.0002f&&
              path.All(b=>Vector3.Distance(result.Local[b].Rotation,scaled.Local[b].Rotation)<.002f),
            $"File-unit scaling preserves the independently checked reach and local angular solution in either numeric scale ({scaled.Status}, residual {scaled.PositionError:G9}, angles {string.Join(';',path.Select(b=>Vector3.Distance(result.Local[b].Rotation,scaled.Local[b].Rotation).ToString("G9")))})");
        bool cancelled=false;try{NativeChainReach.Solve(descriptor,input,path,goal,Vector3.UnitY,cancellation:new(true));}catch(OperationCanceledException){cancelled=true;}
        check(cancelled&&input[2].Rotation.Y==720&&AnimationControls.DeformChecksum(scene)==before,
            "Pre-cancelled reach produces no partial result or mutation of authentic native inputs");
        Vector3[] shape=[new(0,2,0),new(1,2,0),new(1,3,0),new(1,3,1)];
        var shaped=NativeChainReach.Solve(descriptor,input,path,shape[^1],new(1,0,-1),shapeHints:shape);
        var shapedPose=Pose(scene,shaped.Local);
        check(shaped.Status=="Solved"&&path.Select((b,i)=>Vector3.Distance(shapedPose.World[b].Translation,shape[i])).Max()<.00003f,
            $"Independent nonplanar source shape hints preserve the full multi-joint Cartesian path instead of satisfying only its endpoint ({shaped.Status}; endpoint {shaped.PositionError:G9}; shape {shaped.MaximumShapeHintError:G9}; points {string.Join(';',path.Select(b=>shapedPose.World[b].Translation))})");
        var sameEndInput=input.ToArray();sameEndInput[2]=sameEndInput[2] with{Rotation=new(0,0,90)};sameEndInput[3]=sameEndInput[3] with{Rotation=new(0,0,-90)};
        Vector3[] alternateShape=[new(0,2,0),new(0,3,0),new(1,3,0),new(2,3,0)];
        var initialSameEnd=Pose(scene,sameEndInput);
        var sameEnd=NativeChainReach.Solve(descriptor,sameEndInput,path,alternateShape[^1],new(-1,2,0),shapeHints:alternateShape);
        var fittedSameEnd=Pose(scene,sameEnd.Local);
        check(Vector3.Distance(initialSameEnd.World[4].Translation,new(2,3,0))<.00001f&&Vector3.Distance(initialSameEnd.World[2].Translation,new(1,2,0))<.00001f&&
            sameEnd.Status=="Solved"&&path.Select((b,i)=>Vector3.Distance(fittedSameEnd.World[b].Translation,alternateShape[i])).Max()<.00003f&&sameEnd.InitialShapeHintError>1.4f&&sameEnd.MaximumShapeHintError<.00003f,
            "A matching endpoint with a different feasible interior path is fitted to independent Cartesian joint hints rather than incorrectly returned AlreadyMatched");
        check(sameEnd.Local.Select((t,i)=>t.Scale==sameEndInput[i].Scale&&t.Translation==sameEndInput[i].Translation).All(v=>v)&&sameEnd.DistalOrientationError<.001f&&!sameEnd.Local.SequenceEqual(sameEndInput),
            "Interior-shape correction changes native rotations while preserving translations, scale and the already correct distal world orientation");
        foreach(float y in new[]{90f,-90f,89.999f,-89.999f,89.99f,-89.99f})
        {
            var near=new Vector3(757,720+y,697);var authored=(Transform.Identity with{Rotation=near}).Matrix;
            // Native swing reconstruction supplies a float quaternion-derived
            // matrix, not exact symbolic sine/cosine values. That independent
            // representation must round-trip even when its near-pole entries
            // have different float rounding errors.
            var rotation=Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(authored)));
            var decoded=AnimationControls.Euler(rotation,near);var rebuilt=(Transform.Identity with{Rotation=decoded}).Matrix;
            var basisError=new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ}.Max(axis=>Vector3.Distance(Vector3.TransformNormal(axis,rotation),Vector3.TransformNormal(axis,rebuilt)));
            var point=new Vector3(1.2f,-.7f,.4f);float pointError=Vector3.Distance(Vector3.TransformNormal(point,rotation),Vector3.TransformNormal(point,rebuilt));
            check(basisError<.000005f&&pointError<.000005f&&Math.Abs(decoded.Y-near.Y)<.001f&&Vector3.Distance(decoded,near)<180,
                $"XYZ rotation round-trip preserves independent basis/skin directions near Y={y:G9} degrees with nontrivial X/Z and two-turn winding (basis {basisError:G9}, point {pointError:G9}, Euler {decoded})");
        }
    }
    static EvaluatedPose Pose(Scene scene,Transform[] local)=>AnimationService.Evaluate(scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
    static (Scene,ControlRigSetup) Fixture(float scale)
    {
        Bone[] bones=[new("actor",-1,Transform.Identity),new("a",0,Transform.Identity with{Translation=new(0,2*scale,0)}),
            new("b",1,Transform.Identity with{Translation=new(scale,0,0)}),new("c",2,Transform.Identity with{Translation=new(scale,0,0)}),
            new("d",3,Transform.Identity with{Translation=new(scale,0,0)}),new("detail",4,Transform.Identity with{Translation=new(.1f*scale,.1f*scale,0)})];
        var vertex=new Vertex(new(3.2f*scale,2.3f*scale,0),Vector3.UnitZ,Vector2.Zero,new(4,0,0,0),new(1,0,0,0),Vector4.One);
        var scene=new Scene("Original native-path fixture",bones,[new("Weighted surface",[vertex,vertex with{Position=vertex.Position+Vector3.UnitX*.1f*scale},vertex with{Position=vertex.Position+Vector3.UnitY*.1f*scale}],[0,1,2])]);
        var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
        {Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Independent Cartesian units"),BoneRoles=[new("RootMotion",0,"Supplied actor",true),new("Hips",1,"Supplied body",true)]};
        return(scene,rig);
    }
}
