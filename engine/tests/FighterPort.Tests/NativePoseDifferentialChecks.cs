using System.Numerics;
using FighterPort.Core;

// Closed-form Cartesian expectations plus an independent central difference of
// the existing native evaluator. These are derivative checks, not contact or
// motion-quality acceptance and do not change any retarget acceptance bound.
internal static class NativePoseDifferentialChecks
{
    public static void Run(Action<bool,string> check)
    {
        var (rig,pose)=Make();int[] variables=[1,2,3,0];
        string original=RetargetDependencies.Hash(new{rig.Scene,Pose=pose});
        var differential=new NativePoseDifferentials(rig,pose,1,variables);
        check(differential.Variables==15,"Native analytic differential columns retain three dimensionless world translations and three radian variables for every supplied native rotation bone");
        var point=Point(rig.Scene.Meshes[0].Vertices[0],0);
        float worldError=0,jointError=0,skinError=0,orientationError=0;
        var orientation=typeof(ArticulatedSceneFitting).GetMethod("OrientationDerivative",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
        const double h=.005;
        for(int variable=0;variable<differential.Variables;variable++)
        {
            var plus=Perturb(rig,pose,variables,variable,h);var minus=Perturb(rig,pose,variables,variable,-h);
            for(int bone=0;bone<rig.Scene.Bones.Length;bone++)
            {
                var expected=(plus.World[bone]-minus.World[bone])*(float)(.5/h);var actual=differential.World(bone,variable);
                worldError=Math.Max(worldError,Maximum(actual-expected)/Math.Max(1,Maximum(expected)));
                jointError=Math.Max(jointError,Vector3.Distance(differential.Joint(bone,variable),expected.Translation)/Math.Max(1,expected.Translation.Length()));
                var orientationRows=(Vector3[])orientation.Invoke(null,[pose.World[bone],actual])!;
                var orientationProbe=(PoseGeometry.RotationMatrix(plus.World[bone])-PoseGeometry.RotationMatrix(minus.World[bone]))*(float)(.5/h);
                var analytic=new Matrix4x4(orientationRows[0].X,orientationRows[0].Y,orientationRows[0].Z,0,
                    orientationRows[1].X,orientationRows[1].Y,orientationRows[1].Z,0,orientationRows[2].X,orientationRows[2].Y,orientationRows[2].Z,0,0,0,0,0);
                orientationError=Math.Max(orientationError,Maximum(analytic-orientationProbe)/Math.Max(1,Maximum(orientationProbe)));
            }
            foreach(int vertex in Enumerable.Range(0,rig.Scene.Meshes[0].Vertices.Length))
            {
                var surface=Point(rig.Scene.Meshes[0].Vertices[vertex],vertex);
                var expected=(ContactGeometry.Point(surface,plus)-ContactGeometry.Point(surface,minus))/(float)(2*h);
                skinError=Math.Max(skinError,Vector3.Distance(differential.Point(surface,variable),expected)/Math.Max(1,expected.Length()));
            }
        }
        check(worldError<.0002f,$"Analytic full world-matrix derivatives agree with independent central native FK under rotated positive nonuniform rest and authored ancestor scales; maximum relative-entry error {worldError:G9}");
        check(jointError<.0002f,$"Analytic native joint-position derivatives agree with central native FK including actual parent translation/scale; maximum normalized error {jointError:G9}");
        check(skinError<.0002f,$"All four actual skin influences and morphed material coordinates agree with independent central evaluated skin derivatives; maximum normalized error {skinError:G9}");
        check(orientationError<.0002f,$"Analytic normalized Gram-Schmidt orientation rows agree with the actual native orientation evaluator under nonuniform scale/shear, including final cross normalization; maximum normalized error {orientationError:G9}");

        float length=rig.CharacteristicLength;
        for(int axis=0;axis<3;axis++)
        {
            var direction=axis switch{0=>Vector3.UnitX,1=>Vector3.UnitY,_=>Vector3.UnitZ};
            check(Vector3.Distance(differential.Point(point,axis),direction*(length*.9f))<length*.000002f&&
                Vector3.Distance(differential.Joint(1,axis),direction*length)<length*.000002f&&differential.Joint(0,axis)==Vector3.Zero,
                $"World body-translation derivative axis {axis} has independently known .9 blended descendant response through the nonuniform parent, leaving the actor fixed");
        }
        var unnormalized=Point(rig.Scene.Meshes[0].Vertices[1],1);
        check(Vector3.Distance(differential.Point(unnormalized,0),Vector3.UnitX*(length*.7f))<length*.000002f,
            "Analytic skin derivatives retain the actual .7 descendant mass of a 1.2-total-weight vertex without normalization or dropping an influence");
        var plain=new NativePoseDifferentials(rig,pose with{GeometryPositions=null},1,variables);
        check(Vector3.Distance(plain.Point(point,6),differential.Point(point,6))>.01f,
            "Morphed GeometryPositions are selected by their authentic mesh/vertex identity rather than the cached rest point");

        var native=new Bone("arbitrary",-1,new(new(2,3,4),new(0,0,90),new(7,8,9)));
        var scene=new Scene("closed form",[native],[]);var setup=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
            {BodyBone=0,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),BoneRoles=[new("RootMotion",0,"Independent Cartesian root",true),new("Hips",0,"Independent Cartesian body",true)]};
        var single=RigDescriptor.Prepare(scene,setup);var quarter=new NativePoseDifferentials(single,single.Rest,0,[0]);
        var material=new SkinSurfacePoint(new(7,10,9),Vector4.Zero,new(1,0,0,0));
        check(Vector3.Distance(quarter.Point(material,5),new(-2,0,0))<.000002f&&quarter.Joint(0,5)==Vector3.Zero,
            "Closed-form nonuniform S*Rz90*T point has radian derivative (-2,0,0) while the native joint origin does not rotate around the world origin");
        var dz=quarter.World(0,5);
        check(Math.Abs(dz.M11+2)<.000002f&&Math.Abs(dz.M22+3)<.000002f&&dz.M44==0&&dz.Translation==Vector3.Zero,
            "Native rotation derivative keeps nonuniform row scales and zero homogeneous derivative instead of adding an identity matrix");

        var (scaledRig,scaledPose)=Make(100);var centimetres=new NativePoseDifferentials(scaledRig,scaledPose,1,variables);float unitError=0;
        for(int variable=0;variable<differential.Variables;variable++)
            unitError=Math.Max(unitError,Vector3.Distance(differential.Point(point,variable),centimetres.Point(Point(scaledRig.Scene.Meshes[0].Vertices[0],0),variable)/100));
        check(unitError<.00002f,$"Declared centimetre/native-unit fixture retains equivalent physical analytic derivatives across every variable; maximum error {unitError:G9}");
        check(original==RetargetDependencies.Hash(new{rig.Scene,Pose=pose}),"Preparing and querying analytic native derivatives preserves every input transform, bind, skin weight, geometry point and native identity");
        bool cancelled=false;using(var source=new CancellationTokenSource()){source.Cancel();try{_ =new NativePoseDifferentials(rig,pose,1,variables,source.Token);}catch(OperationCanceledException){cancelled=true;}}
        check(cancelled,"Analytic native preparation respects cancellation before allocating or propagating derivative graphs");
        bool duplicate=false;try{_ =new NativePoseDifferentials(rig,pose,1,[1,1]);}catch(ArgumentException){duplicate=true;}
        check(duplicate,"A duplicate native rotation variable is rejected explicitly rather than double-applying a joint correction");
        GuardChecks(check,rig,pose);
    }

    static void GuardChecks(Action<bool,string> check,RigDescriptor rig,EvaluatedPose pose)
    {
        RetargetMappingReport Supplied(RigDescriptor r)=>new(Enumerable.Range(0,r.Scene.Bones.Length).Select(b=>new RetargetBonePair("opaque-role-"+b,b,b,"Independent supplied native identity")
            {Binding=new("supplied","exact",true,true,"Reviewed independent source","Reviewed independent target")}).ToArray(),[],[]);
        var fitting=new ArticulatedSceneFitting(rig,1,Supplied(rig),[],default);
        fitting.ValidateEvaluationBudget(1,1,3,4);var bounded=fitting.Receipt();
        check(bounded.EstimatedResidualWork is >0&&bounded.EstimatedResidualWork<bounded.ResidualWorkBudget&&
            bounded.EstimatedBoneWork is >0&&bounded.EstimatedBoneWork<bounded.BoneWorkBudget&&bounded.EstimatedSkinColumns is >0&&
            bounded.EstimatedPoseEvaluations==156&&bounded.Evaluations==0,
            "A two-query analytic preflight bounds 156 pose evaluations, including prepared/publication measurements, plus independent matrix/skin-column work before sampling without fictitious per-variable perturbed FK");
        fitting.ValidateEvaluationBudget(0,0,3,4);var zero=fitting.Receipt();
        check(zero.EstimatedResidualWork==0&&zero.EstimatedBoneWork==0&&zero.EstimatedPoseEvaluations==0&&zero.EstimatedSkinColumns==0,
            "An empty requested sample set has exactly zero work under the analytic preflight");
        bool negative=false;try{fitting.ValidateEvaluationBudget(-1,0,3,4);}catch(ArgumentOutOfRangeException){negative=true;}
        check(negative,"Negative offline sample counts are rejected before arithmetic or sampling");
        string? excessive=null;try{fitting.ValidateEvaluationBudget(int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue);}catch(NotSupportedException ex){excessive=ex.Message;}
        var failed=fitting.Receipt();
        check(excessive is not null&&excessive.Contains("resource limit",StringComparison.Ordinal)&&
            failed.EstimatedResidualWork is >0&&failed.EstimatedResidualWork>failed.ResidualWorkBudget&&failed.Evaluations==0,
            "Maximum integer sample/sensor/feature inputs saturate safely and fail explicit offline work limits before sampling instead of overflowing into a pass");

        RigDescriptor LongRig(int rotations)
        {
            var bones=Enumerable.Range(0,rotations+1).Select(i=>new Bone("independent-"+i,i-1,Transform.Identity with{Translation=i==0?Vector3.Zero:Vector3.UnitX})).ToArray();
            var scene=new Scene("Independent native resource boundary",bones,[]);
            var setup=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
                {BodyBone=1,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),
                 BoneRoles=[new("RootMotion",0,"Reviewed actor",true),new("Hips",1,"Reviewed body",true)],
                 VirtualChains=[new("Independent complete native path",Enumerable.Range(1,rotations).ToArray(),"Reviewed arbitrary-length native topology",true)]};
            return RigDescriptor.Prepare(scene,setup);
        }
        var atLimit=LongRig(32);var accepted=new ArticulatedSceneFitting(atLimit,1,Supplied(atLimit),[],default);
        check(accepted.Enabled&&accepted.Variables==99,"The declared 32-native-rotation resource boundary retains every requested degree of freedom");
        var overLimit=LongRig(33);string checksum=AnimationControls.DeformChecksum(overLimit.Scene);string? rejected=null;
        try{_ =new ArticulatedSceneFitting(overLimit,1,Supplied(overLimit),[],default);}catch(NotSupportedException ex){rejected=ex.Message;}
        check(rejected is not null&&rejected.Contains("33",StringComparison.Ordinal)&&rejected.Contains("32",StringComparison.Ordinal)&&
            rejected.Contains("No pose was sampled",StringComparison.Ordinal)&&AnimationControls.DeformChecksum(overLimit.Scene)==checksum,
            "A 33-joint request fails with its exact resource boundary before sampling, preserving the rig instead of silently falling back to translation-only fitting");

        bool cancelled=false;string before=RetargetDependencies.Hash(pose);using(var cancellation=new CancellationTokenSource())
        {
            var interrupted=new ArticulatedSceneFitting(rig,1,Supplied(rig),[],cancellation.Token);
            try{interrupted.Fit(pose.Local,(p,d)=>{cancellation.Cancel();return new([1],new double[1,d?.Variables??0]);});}
            catch(OperationCanceledException){cancelled=true;}
        }
        check(cancelled&&before==RetargetDependencies.Hash(pose),"Cancelling after the first actual residual evaluation stops coordinated fitting before the next iteration and preserves the input pose");
        check(bounded.CorrectionMeasure.Contains("Euler",StringComparison.Ordinal)&&bounded.CorrectionMeasure.Contains("not",StringComparison.Ordinal)&&bounded.RotationOffsetBoundDegrees==45,
            "Correction receipts identify the unchanged 45-degree bound as a local Euler parameter-offset norm rather than measured quaternion/action fidelity");
        StopEvidenceChecks(check,rig,pose,Supplied(rig));
    }

    static void StopEvidenceChecks(Action<bool,string> check,RigDescriptor rig,EvaluatedPose pose,RetargetMappingReport mapping)
    {
        var fitting=new ArticulatedSceneFitting(rig,1,mapping,[],default);string before=RetargetDependencies.Hash(pose);
        var output=fitting.Fit(pose.Local,(_,d)=>new([],new double[0,d?.Variables??0]));var neutral=fitting.Receipt();
        check(output.SequenceEqual(pose.Local)&&neutral.TerminationEvidenceVersion==ArticulatedSceneFitting.TerminationEvidenceVersion&&
            neutral.AcceptedSteps==0&&neutral.RejectedTrials==0&&
            neutral.NoAcceptedLowerCostStops==1&&neutral.IterationLimitStops==0&&neutral.LinearSolveStops==0&&
            neutral.Unsupported==0&&neutral.ConvergenceStatus=="NotCertified",
            "An actually evaluated zero-scene-residual pose stops on its zero direction without claiming unexecuted trial rejections, convergence or incompatibility");
        var broken=new ArticulatedSceneFitting(rig,1,mapping,[],default);
        var unchanged=broken.Fit(pose.Local,(_,d)=>
        {
            var jacobian=new double[1,d?.Variables??0];if(d is not null)jacobian[0,0]=double.NaN;
            return new([1],jacobian);
        });var failed=broken.Receipt();
        check(unchanged.SequenceEqual(pose.Local)&&failed.AcceptedSteps==0&&failed.RejectedTrials==0&&
            failed.LinearSolveStops==1&&failed.NoAcceptedLowerCostStops==0&&failed.IterationLimitStops==0&&
            failed.ConvergenceStatus=="NotCertified"&&before==RetargetDependencies.Hash(pose),
            "A real nonfinite residual derivative stops the linear solve before any trial and preserves the pose; the receipt identifies that stop without calling the anatomy impossible");
    }

    static (RigDescriptor Rig,EvaluatedPose Pose) Make(float units=1)
    {
        Transform T(Vector3 s,Vector3 r,Vector3 p)=>new(s,r,p*units);
        var bones=new[]{new Bone("opaque-a",-1,T(new(1.6f,.8f,1.25f),new(11,-23,37),new(2,-1,3))),
            new("opaque-b",0,T(new(.75f,1.4f,.9f),new(-17,31,-13),new(.5f,2,-.7f))),
            new("opaque-c",1,T(new(1.1f,.9f,1.3f),new(19,-27,41),new(1.7f,.2f,-.4f))),
            new("opaque-d",1,T(new(.9f,1.2f,1),new(-9,14,21),new(-1.1f,.6f,.3f)))};
        Vertex V(Vector3 p,Vector4 w)=>new(p*units,Vector3.UnitY,Vector2.Zero,new(2,3,1,0),w,Vector4.One);
        var mesh=new Mesh("material",[V(new(2,1,-.5f),new(.4f,.3f,.2f,.1f)),V(new(3,-.2f,1),new(.1f,.4f,.2f,.5f)),V(new(.7f,1.9f,.6f),new(.2f,.3f,.4f,.1f))],[0,1,2]);
        var scene=new Scene("independent affine fixture",bones,[mesh]);
        var setup=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
            {BodyBone=1,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(units,"Independent physical units"),
             BoneRoles=[new("RootMotion",0,"Supplied native actor",true),new("Hips",1,"Supplied native body",true)],
             VirtualChains=[new("arbitrary branch",[1,2],"Complete native path",true)]};
        var rig=RigDescriptor.Prepare(scene,setup);var local=bones.Select(b=>b.Rest).ToArray();
        local[0]=local[0] with{Rotation=local[0].Rotation+new Vector3(7,-9,13),Scale=new(1.2f,.7f,1.6f)};
        local[1]=local[1] with{Rotation=local[1].Rotation+new Vector3(-5,8,-11),Scale=new(.8f,1.3f,.65f)};
        local[2]=local[2] with{Rotation=local[2].Rotation+new Vector3(23,-14,9),Scale=new(.9f,1.1f,1.4f)};
        local[3]=local[3] with{Rotation=local[3].Rotation+new Vector3(11,7,-13),Scale=new(1.3f,.8f,.9f)};
        var pose=AnimationService.Evaluate(scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t))
            with{GeometryPositions=[mesh.Vertices.Select((v,i)=>v.Position+new Vector3(.4f,-.3f,.7f)*(units*(i+1))).ToArray()]};
        return(rig,pose);
    }
    static EvaluatedPose Perturb(RigDescriptor rig,EvaluatedPose pose,int[] variables,int variable,double amount)
    {
        var local=pose.Local.ToArray();
        if(variable<3)
        {
            var direction=variable switch{0=>Vector3.UnitX,1=>Vector3.UnitY,_=>Vector3.UnitZ};
            Matrix4x4.Invert(pose.World[0],out var inverse);
            local[1]=local[1] with{Translation=local[1].Translation+Vector3.TransformNormal(direction*(float)(amount*rig.CharacteristicLength),inverse)};
        }
        else
        {
            int bone=variables[(variable-3)/3],axis=(variable-3)%3;var rotation=local[bone].Rotation;rotation[axis]+=(float)(amount*180/Math.PI);
            local[bone]=local[bone] with{Rotation=rotation};
        }
        return AnimationService.Evaluate(rig.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t)) with{GeometryPositions=pose.GeometryPositions};
    }
    static SkinSurfacePoint Point(Vertex v,int vertex)=>new(v.Position,v.Joints,v.Weights){Mesh=0,Vertex=vertex};
    static float Maximum(Matrix4x4 m)=>new[]{m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44}.Max(Math.Abs);
}
