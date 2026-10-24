using System.Numerics;
using FighterPort.Core;

// Independent Cartesian triangles exercise the geometric seed without deriving
// expected joints from the solver's law-of-cosines implementation. These checks
// establish native-path kinematics, not contact or source-action acceptance.
internal static class ReachSeedChecks
{
    public static void Run(Action<bool,string> check)
    {
        var deep=RunTriangle(new(.5f,5,0),new(1,0,0),1,false,false);
        Verify(deep,"A deeply folded two-segment path",check);
        var near=RunTriangle(new(4.999f,.1f,0),new(9.998f,0,0),1,false,false,true);
        Verify(near,"A nearly extended two-segment path",check);
        check(deep.Result.Iterations<=NativeChainReach.MaximumIterations&&near.Result.Iterations<=NativeChainReach.MaximumIterations,
            "Deep-fold and near-extension seeds fit within the existing bounded projection iterations");

        var helpers=RunTriangle(new(.5f,5,0),new(1,0,0),1,true,false);
        Verify(helpers,"Two geometric segments separated by zero-length native helpers",check);
        check(Vector3.Distance(helpers.Pose.World[helpers.Path[0]].Translation,helpers.Pose.World[helpers.Path[1]].Translation)<.00001f&&
              Vector3.Distance(helpers.Pose.World[helpers.Path[2]].Translation,helpers.Pose.World[helpers.Path[3]].Translation)<.00001f&&
              helpers.Path.Length==5&&helpers.Scene.Bones.Length==7,
            "Zero-length native helpers remain distinct bones at their preserved path positions rather than being removed from the rig");

        var mirror=RunTriangle(new(.5f,5,0),new(1,0,0),1,false,true);
        Verify(mirror,"The reflected deep-fold Cartesian fixture",check);
        var mirroredMiddle=mirror.Pose.World[mirror.Middle].Translation-mirror.Root;
        var originalMiddle=deep.Pose.World[deep.Middle].Translation-deep.Root;
        check(Vector3.Distance(mirroredMiddle,new(-originalMiddle.X,originalMiddle.Y,originalMiddle.Z))<.00004f,
            "Reflecting the native rest geometry and desired path reflects the fitted joint without a world-side numerical exception");

        foreach(float units in new[]{.01f,100f})
        {
            var scaled=RunTriangle(new(.5f,5,0),new(1,0,0),units,false,false);
            Verify(scaled,$"The deep-fold Cartesian fixture at {units:G} native unit scale",check);
            check(deep.Path.All(b=>Vector3.Distance(deep.Result.Local[b].Rotation,scaled.Result.Local[b].Rotation)<.003f),
                "Changing file units preserves the native angular solution without a scale-dependent bend branch");
        }

        // A rigid actor turn/translation transports the independently specified
        // triangle. The numerical reach stage is not allowed a world-up rule.
        var movedInput=deep.Input.ToArray();
        movedInput[0]=movedInput[0] with{Rotation=new(20,65,-15),Translation=new(4,-3,2)};
        var actor=movedInput[0].Matrix;
        var movedGoal=Vector3.Transform(deep.Goal,actor);
        var movedBend=Vector3.TransformNormal(Vector3.UnitY,actor);
        var moved=NativeChainReach.Solve(deep.Rig,movedInput,deep.Path,movedGoal,movedBend);
        var movedPose=Pose(deep.Scene,moved.Local);
        check(moved.Status=="Solved"&&Vector3.Distance(movedPose.World[deep.Middle].Translation,Vector3.Transform(deep.ExpectedMiddle,actor))<.00005f&&
              Vector3.Distance(movedPose.World[deep.Path[^1]].Translation,movedGoal)<.00005f,
            "A rotated and translated actor rigidly transports a deep-fold triangle instead of changing its bend or reach");
        var beforePose=Pose(deep.Scene,movedInput);
        check(RotationError(beforePose.World[deep.Path[^1]],movedPose.World[deep.Path[^1]])<.001f,
            "Deep-fold seeding retains the complete incoming distal world orientation under a rotated actor");

        var fixedRoot=NativeChainReach.Solve(deep.Rig,deep.Input,deep.Path,deep.Goal,Vector3.UnitY,
            [new(deep.Path[0],deep.Input[deep.Path[0]].Rotation,deep.Input[deep.Path[0]].Rotation,"Independent fixed native root rotation",true)]);
        check(fixedRoot.Status=="LimitConflict"&&fixedRoot.Local.SequenceEqual(deep.Input),
            "An exact geometric seed cannot bypass reviewed native joint limits or publish a partially corrected pose");
        var unavailable=deep.Input.ToArray();unavailable[0]=unavailable[0] with{Scale=new(1,1.1f,1)};
        var affine=NativeChainReach.Solve(deep.Rig,unavailable,deep.Path,deep.Goal,Vector3.UnitY);
        check(affine.Status=="Unsupported"&&affine.Local.SequenceEqual(unavailable),
            "A geometric seed does not normalize unverified nonuniform native ancestor scale");
        var tooFar=NativeChainReach.Solve(deep.Rig,deep.Input,deep.Path,deep.Root+new Vector3(20,0,0),Vector3.UnitY);
        check(tooFar.Status=="Unreachable"&&tooFar.Local.SequenceEqual(deep.Input),
            "An out-of-reach goal remains explicit and unchanged rather than being clamped by the geometric seed");
        bool cancelled=false;var cancellationInput=deep.Input.ToArray();
        try{NativeChainReach.Solve(deep.Rig,deep.Input,deep.Path,deep.Goal,Vector3.UnitY,cancellation:new(true));}
        catch(OperationCanceledException){cancelled=true;}
        check(cancelled&&deep.Input.SequenceEqual(cancellationInput),
            "Cancellation before geometric seeding leaves every supplied native transform unchanged");
    }

    static void Verify(Triangle t,string label,Action<bool,string> check)
    {
        float tolerance=.00004f*t.Units;
        check(t.Result.Status=="Solved"&&Vector3.Distance(t.Pose.World[t.Middle].Translation,t.ExpectedMiddle)<tolerance&&
              Vector3.Distance(t.Pose.World[t.Path[^1]].Translation,t.Goal)<tolerance,
            label+" reaches independently specified Cartesian middle/end points without relaxing the residual bound");
        var expectedLengths=t.Path.Zip(t.Path.Skip(1),(a,b)=>Vector3.Distance(t.Before.World[a].Translation,t.Before.World[b].Translation)).ToArray();
        var actualLengths=t.Path.Zip(t.Path.Skip(1),(a,b)=>Vector3.Distance(t.Pose.World[a].Translation,t.Pose.World[b].Translation)).ToArray();
        check(expectedLengths.Zip(actualLengths,(a,b)=>Math.Abs(a-b)).Max()<tolerance&&
              t.Result.Local.Select((value,i)=>value.Translation==t.Input[i].Translation&&value.Scale==t.Input[i].Scale).All(v=>v)&&
              t.Result.Local[^1]==t.Input[^1]&&t.Input.SequenceEqual(t.InputSnapshot)&&AnimationControls.DeformChecksum(t.Scene)==t.Deform,
            label+" preserves every native segment, helper, translation, scale, unrelated child, bind and skin weight");
        int end=t.Path[^1];
        var offset=new Vector3(.2f,.3f,.4f)*t.Units;
        var expectedSkin=t.Goal+Vector3.TransformNormal(offset,t.Before.World[end]);
        check(RotationError(t.Before.World[end],t.Pose.World[end])<.001f&&
              Vector3.Distance(AnimationService.Skin(t.Scene.Meshes[0].Vertices[0],t.Pose),expectedSkin)<tolerance*2&&
              Math.Abs(t.Result.Local[end].Rotation.Y-745)<180,
            label+" preserves incoming distal orientation, actual weighted skin offsets and the authored two-turn Euler branch");
    }

    sealed record Triangle(Scene Scene,RigDescriptor Rig,Transform[] Input,Transform[] InputSnapshot,int[] Path,int Middle,Vector3 Root,Vector3 ExpectedMiddle,
        Vector3 Goal,EvaluatedPose Before,EvaluatedPose Pose,NativeChainReachResult Result,string Deform,float Units);
    static Triangle RunTriangle(Vector3 middle,Vector3 end,float units,bool helpers,bool mirror,bool near=false)
    {
        if(mirror){middle.X=-middle.X;end.X=-end.X;}
        // Segment lengths are distances in the supplied Cartesian fixture.
        // No expected output angle is obtained from a reach/IK implementation.
        float first=middle.Length()*units,second=Vector3.Distance(middle,end)*units,side=mirror?-1:1;
        var root=new Vector3(0,2*units,0);var bones=new List<Bone>{new("actor",-1,Transform.Identity),new("path-root",0,Transform.Identity with{Translation=root})};
        if(helpers)bones.Add(new("coincident-root-helper",bones.Count-1,Transform.Identity));
        int firstMover=bones.Count-1;
        bones.Add(new("middle",bones.Count-1,Transform.Identity with{Translation=new(side*first,0,0)}));int mid=bones.Count-1;
        if(helpers)bones.Add(new("coincident-middle-helper",bones.Count-1,Transform.Identity));
        int secondMover=bones.Count-1;
        bones.Add(new("end",bones.Count-1,Transform.Identity with{Translation=new(side*second,0,0)}));int distal=bones.Count-1;
        bones.Add(new("unrelated-native-detail",distal,Transform.Identity with{Translation=new(.1f*units,.2f*units,0),Rotation=new(11,12,13)}));
        var position=root+new Vector3(side*(first+second),0,0)+new Vector3(.2f,.3f,.4f)*units;
        var vertex=new Vertex(position,Vector3.UnitZ,Vector2.Zero,new(distal,0,0,0),new(1,0,0,0),Vector4.One);
        var scene=new Scene("Independent Cartesian reach triangle",bones.ToArray(),[new("Native weighted skin",[vertex,vertex with{Position=position+Vector3.UnitX*.1f*units},vertex with{Position=position+Vector3.UnitY*.1f*units}],[0,1,2])]);
        var setup=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
        {Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Independently supplied native units"),BoneRoles=[new("RootMotion",0,"Supplied actor",true),new("Hips",1,"Supplied body",true)]};
        var descriptor=RigDescriptor.Prepare(scene,setup);var input=scene.Bones.Select(b=>b.Rest).ToArray();
        // Begin from a different nondegenerate articulated pose, not a straight
        // chain that already triggers the existing sinusoidal straight seed.
        input[firstMover]=input[firstMover] with{Rotation=new(0,0,side*(near?5:25))};
        input[secondMover]=input[secondMover] with{Rotation=new(0,0,side*(near?-10:140))};
        input[distal]=input[distal] with{Rotation=new(17,745,-33)};
        int[] path=Enumerable.Range(1,distal).ToArray();var goal=root+end*units;
        string deform=AnimationControls.DeformChecksum(scene);var snapshot=input.ToArray();var before=Pose(scene,input);
        var result=NativeChainReach.Solve(descriptor,input,path,goal,Vector3.UnitY);
        return new(scene,descriptor,input,snapshot,path,mid,root,root+middle*units,goal,before,Pose(scene,result.Local),result,deform,units);
    }
    static EvaluatedPose Pose(Scene scene,Transform[] local)=>AnimationService.Evaluate(scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
    static float RotationError(Matrix4x4 a,Matrix4x4 b)
    {
        var qa=Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(a));var qb=Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(b));
        var delta=Quaternion.Normalize(qb*Quaternion.Inverse(qa));
        return 2*MathF.Atan2(new Vector3(delta.X,delta.Y,delta.Z).Length(),Math.Abs(delta.W))*180/MathF.PI;
    }
}
