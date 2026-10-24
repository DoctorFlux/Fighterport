using System.Numerics;
using System.Text.Json;
using FighterPort.Core;

// Independent finite differences of actual composed residual values. In
// particular these checks cover the moving ancestor's inverse-frame term,
// which an endpoint-only derivative test cannot exercise.
internal static class NativeSceneObjectiveChecks
{
    public static async Task RunAsync(string directory,Action<bool,string> check)
    {
        Directory.CreateDirectory(directory);var (rig,input)=Make(1);int[] rotations=[1,2,3,4],points=[1,2,3,4],ends=[3,4];
        string original=RetargetDependencies.Hash(new{rig.Scene,Input=input});
        var vertex=rig.Scene.Meshes[0].Vertices[0];var material=new SkinSurfacePoint(vertex.Position,vertex.Joints,vertex.Weights){Mesh=0,Vertex=0};
        var before=Pose(rig,input);var desired=ContactGeometry.Point(material,before)+new Vector3(.1f,-.07f,.05f)*rig.CharacteristicLength;
        ArticulatedResiduals Observations(EvaluatedPose pose,NativePoseDifferentials? differential)
        {
            var value=(ContactGeometry.Point(material,pose)-desired)/rig.CharacteristicLength;int n=differential?.Variables??0;var j=new double[3,n];
            for(int c=0;c<n;c++){var v=differential!.Point(material,c)/rig.CharacteristicLength;for(int a=0;a<3;a++)j[a,c]=v[a];}
            return new([value.X,value.Y,value.Z],j);
        }
        var reference=input.ToArray();reference[2]=reference[2] with{Rotation=reference[2].Rotation+new Vector3(-7,11,5)};
        var objective=new NativeSceneObjective(rig,1,rotations,points,ends,[],input,Observations,default,reference,Reach(rig,input));
        double[] x=[.031,-.024,.018,.04,-.03,.02,-.035,.017,.028,.025,-.015,.037,-.027,.023,-.019];
        var evaluated=objective.Evaluate(x,true);const double h=.005;double maximum=0,actionMaximum=0;int worstRow=-1,worstColumn=-1;
        int actionStart=3+objective.Variables+3*points.Length+9*ends.Length;
        for(int c=0;c<x.Length;c++)
        {
            var plus=x.ToArray();var minus=x.ToArray();plus[c]+=h;minus[c]-=h;
            var a=objective.Evaluate(plus,false).Residual;var b=objective.Evaluate(minus,false).Residual;
            for(int r=0;r<a.Length;r++)
            {
                double expected=(a[r]-b[r])/(2*h),error=Math.Abs(evaluated.Jacobian[r,c]-expected)/Math.Max(1,Math.Abs(expected));
                if(error>maximum){maximum=error;worstRow=r;worstColumn=c;}
                if(r>=actionStart)actionMaximum=Math.Max(actionMaximum,error);
            }
        }
        await File.WriteAllTextAsync(Path.Combine(directory,"native-action-jacobian.json"),JsonSerializer.Serialize(new{Version=NativeSceneObjective.Version,Variables=objective.Variables,ResidualRows=evaluated.Residual.Length,ActionStart=actionStart,StepRadians=h,MaximumNormalizedError=maximum,MaximumActionError=actionMaximum,WorstRow=worstRow,WorstColumn=worstColumn},new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false}));
        check(maximum<.0002,$"Every actual skin/prior/relative-action residual derivative agrees with independent central evaluation under rotated positive anisotropic parent and descendant frames; maximum={maximum:G9}, row={worstRow}, column={worstColumn}");
        check(evaluated.Residual.Length>actionStart&&actionMaximum<.0002,
            $"All newly added relative-frame orientation and body-relative interior-shape rows include both moving-frame and moving-point derivatives; maximum={actionMaximum:G9}");
        check(evaluated.Local[0]==input[0]&&evaluated.Local.Select((t,i)=>t.Scale==input[i].Scale&&(i==1||t.Translation==input[i].Translation)).All(v=>v)&&evaluated.Cost==evaluated.Residual.Sum(v=>v*v),
            "Actual objective application leaves the native actor, authored scales and non-body translations intact and reports the squared norm of its actual residual vector");
        var identity=objective.Apply(new double[objective.Variables]);var translationOnly=new double[objective.Variables];translationOnly[0]=.125;
        var translated=Pose(rig,objective.Apply(translationOnly));
        check(identity.SequenceEqual(input)&&Vector3.Distance(translated.World[1].Translation-before.World[1].Translation,Vector3.UnitX*(rig.CharacteristicLength*.125f))<rig.CharacteristicLength*.000002f&&
            Vector3.Distance(ContactGeometry.Point(material,translated)-ContactGeometry.Point(material,before),Vector3.UnitX*(rig.CharacteristicLength*.125f*.9f))<rig.CharacteristicLength*.000002f&&translated.Local[0]==input[0],
            "A dimensionless +1/8 world-X body correction has independently known joint displacement and .9 descendant-weight skin response through the rotated scaled parent; zero correction reproduces exact input channels");
        var rotatedBasis=rig with{Basis=Matrix4x4.CreateRotationZ(MathF.PI/2)};
        var basisObjective=new NativeSceneObjective(rotatedBasis,1,rotations,points,ends,[],input,(_,d)=>new([],new double[0,d?.Variables??0]),default);
        var basisPose=Pose(rotatedBasis,basisObjective.Apply(translationOnly));
        var basisDifferential=new NativePoseDifferentials(rotatedBasis,before,1,rotations,translationBasis:rotatedBasis.Basis);
        check(Vector3.Distance(basisPose.World[1].Translation-before.World[1].Translation,Vector3.UnitY*(rig.CharacteristicLength*.125f))<rig.CharacteristicLength*.000002f&&
            Vector3.Distance(basisDifferential.Joint(1,0),Vector3.UnitY*rig.CharacteristicLength)<rig.CharacteristicLength*.000002f&&
            Vector3.Distance(basisDifferential.Point(material,0),Vector3.UnitY*(rig.CharacteristicLength*.9f))<rig.CharacteristicLength*.000002f,
            "A reviewed 90-degree rig frame maps the same +1/8 correction to world Y with the independently known .9 skin derivative, without changing any native parent or bind");
        var (scaledRig,scaledInput)=Make(100);var scaledReference=scaledInput.ToArray();scaledReference[2]=scaledReference[2] with{Rotation=scaledReference[2].Rotation+new Vector3(-7,11,5)};
        var scaled=new NativeSceneObjective(scaledRig,1,rotations,points,ends,[],scaledInput,(_,d)=>new([],new double[0,d?.Variables??0]),default,scaledReference,Reach(scaledRig,scaledInput)).Evaluate(x,true);
        var plain=new NativeSceneObjective(rig,1,rotations,points,ends,[],input,(_,d)=>new([],new double[0,d?.Variables??0]),default,reference,Reach(rig,input)).Evaluate(x,true);
        check(plain.Residual.Zip(scaled.Residual).All(p=>Math.Abs(p.First-p.Second)<.00008)&&Enumerable.Range(0,plain.Residual.Length).All(r=>Enumerable.Range(0,x.Length).All(c=>Math.Abs(plain.Jacobian[r,c]-scaled.Jacobian[r,c])<.0002)),
            "Normalized native action values and all derivative columns are physically equivalent in authored metres and centimetres without normalizing native transforms");
        var seed=input.ToArray();seed[3]=seed[3] with{Rotation=seed[3].Rotation+new Vector3(0,0,15)};
        NativeSceneObjective Empty(Transform[] starting,Transform[]? actionReference)=>new(rig,1,rotations,points,ends,[],starting,(_,d)=>new([],new double[0,d?.Variables??0]),default,actionReference);
        var anchored=Empty(seed,input);var seedAnchored=Empty(seed,null);int tail=objective.Variables+3*points.Length+9*ends.Length;
        var cancelSeed=new double[objective.Variables];cancelSeed[3+3*2+2]=-15*Math.PI/180;
        var wrong=anchored.Evaluate(new double[objective.Variables],false);var restored=anchored.Evaluate(cancelSeed,false);var seedRestored=seedAnchored.Evaluate(cancelSeed,false);
        check(wrong.Residual.Skip(tail).Max(Math.Abs)>.001&&restored.Residual.Skip(tail).Max(Math.Abs)<.00001&&seedRestored.Residual.Skip(tail).Max(Math.Abs)>.001,
            "Authentic action-reference frames stay separate from a deliberately perturbed reach seed: cancelling its known 15-degree native offset restores the reference action instead of redefining it from the seed");
        bool actorRejected=false;try{_ =new NativeSceneObjective(rig,1,[0,1],points,ends,[],input,(_,d)=>new([],new double[0,d?.Variables??0]),default);}catch(NotSupportedException){actorRejected=true;}
        check(actorRejected,"Actor/ancestor rotation variables are explicitly rejected so the fixed body-parent translation map cannot disagree with current-parent derivatives or rewrite actor travel");
        double[] coupledStep=[2,0,0,.5,0,0];
        double scale=NativeMotionTrajectory.LimitStep(coupledStep,6);
        check(scale==.125&&coupledStep[0]==.25&&coupledStep[3]==.0625&&-coupledStep[0]+2*coupledStep[3]==-.125&&
            -.25+2*ArticulatedSceneFitting.MaximumStepRadians>0,
            "An independently known descent direction for gradient(-1,2) stays descent after a shared 1/8 step scale; separate body/rotation clipping would reverse it to ascent");
        var derivativeOne=NativeMotionTrajectory.CorrectionDerivativeBounds([0,1,1,0],1);
        var derivativeSixty=NativeMotionTrajectory.CorrectionDerivativeBounds([0,1,1,0],60);
        check(Math.Abs(derivativeOne.Velocity-7d/6)<1e-12&&Math.Abs(derivativeOne.Acceleration-2)<1e-12&&
            Math.Abs(derivativeSixty.Velocity-70)<1e-10&&Math.Abs(derivativeSixty.Acceleration-7200)<1e-10,
            "Independent scalar cubic -u^3/2+u^2/2+u reaches velocity 7/6 and acceleration 2 between its knots, becoming 70 and 7200 at 60 Hz; receipts do not under-report knot-difference bounds");
        check(NativeMotionTrajectory.CorrectionDerivativeBounds([2,2,2,2],60)==(0d,0d)&&NativeMotionTrajectory.CorrectionDerivativeBounds([4],60)==(0d,0d),
            "Constant and single-knot correction fields have independently zero continuous velocity and acceleration rather than invented endpoint motion");
        int callbacks=0;NativeSceneObjective At(double time)=>new(rig,1,rotations,points,ends,[],input,(p,d)=>
        {
            callbacks++;double desiredY=before.World[1].Translation.Y+rig.CharacteristicLength*(.1+.05*Math.Sin(2*Math.PI*time/3));
            var jacobian=new double[1,d?.Variables??0];for(int v=0;v<jacobian.GetLength(1);v++)jacobian[0,v]=d!.Joint(1,v).Y/rig.CharacteristicLength;
            return new([(p.World[1].Translation.Y-desiredY)/rig.CharacteristicLength],jacobian);
        },default);
        var periodicFrames=Enumerable.Range(0,7).Select(i=>new NativeTrajectoryFrame(i/2d,At(i/2d))).ToArray();
        var periodic=new NativeMotionTrajectory(periodicFrames,4,30,false,default,periodic:true);double epsilon=.00001;
        var first=periodic.EvaluateParameters(0);var last=periodic.EvaluateParameters(3);
        var nearFirst=periodic.EvaluateParameters(epsilon);var nearLast=periodic.EvaluateParameters(3-epsilon);
        check(periodic.Receipt().AcceptedSteps>0&&periodic.EvaluateParameters(1).Any(v=>Math.Abs(v)>.01)&&first.SequenceEqual(last)&&
            first.Select((v,i)=>Math.Abs((nearFirst[i]-v)/epsilon-(last[i]-nearLast[i])/epsilon)).Max()<.00001,
            "An actually solved nonzero periodic correction shares its closing control and one-sided first derivatives across the seam while the frozen source observations vary over time");
        var periodicBounds=NativeMotionTrajectory.CorrectionDerivativeBounds([0,1,0],1,periodic:true);
        check(periodic.Receipt().BoundaryPolicy?.Contains("Periodic",StringComparison.Ordinal)==true&&periodic.Receipt().MaximumCorrectionVelocity is >0&&
            Math.Abs(periodicBounds.Velocity-25d/18)<1e-12&&Math.Abs(periodicBounds.Acceleration-5)<1e-12,
            "Periodic derivative measurement includes the closing segment and has independent extrema 25/18 velocity and 5 acceleration for scalar keys 0,1,0");
        int beforeGuards=callbacks;bool overBudget=false;try{_ =new NativeMotionTrajectory([new(0,At(0))],int.MaxValue,30,false);}catch(NotSupportedException ex){overBudget=ex.Message.Contains("budget",StringComparison.Ordinal);}
        check(overBudget&&callbacks==beforeGuards,"Excessive native trajectory controls fail their resource preflight before any actual residual callback or pose optimization");
        bool cancelled=false;using(var cancellation=new CancellationTokenSource())
        {cancellation.Cancel();try{_ =new NativeMotionTrajectory(periodicFrames,4,30,false,cancellation.Token,true);}catch(OperationCanceledException){cancelled=true;}}
        check(cancelled&&callbacks==beforeGuards,"Cancellation stops trajectory preparation before source-time callbacks, allocation of its solve graph or publication of a partial correction");
        check(original==RetargetDependencies.Hash(new{rig.Scene,Input=input})&&rig.Scene.Meshes[0].Vertices[0]==vertex,
            "Building and evaluating action objectives at nonzero parameters preserves every original native transform, skin influence, source input array and rest identity");
    }
    static (RigDescriptor Rig,Transform[] Input) Make(float units)
    {
        Transform T(Vector3 scale,Vector3 rotation,Vector3 position)=>new(scale,rotation,position*units);
        var bones=new[]{new Bone("opaque-zero",-1,T(new(1.6f,.8f,1.25f),new(11,-23,37),new(2,-1,3))),
            new("opaque-one",0,T(new(.75f,1.4f,.9f),new(-17,31,-13),new(.5f,2,-.7f))),
            new("opaque-two",1,T(new(1.1f,.9f,1.3f),new(19,-27,41),new(1.7f,.2f,-.4f))),
            new("opaque-three",2,T(new(.9f,1.2f,1),new(-9,14,21),new(-1.1f,.6f,.3f))),
            new("opaque-four",1,T(new(1.2f,.85f,1.1f),new(17,-8,26),new(-.4f,.8f,1.2f)))};
        Vertex V(Vector3 p,Vector4 w)=>new(p*units,Vector3.UnitY,Vector2.Zero,new(3,4,2,0),w,Vector4.One);
        var scene=new Scene("Independent affine native action fixture",bones,[new("Four-influence actual material",[V(new(2,1,-.5f),new(.4f,.3f,.2f,.1f)),V(new(3,-.2f,1),new(.1f,.4f,.2f,.3f)),V(new(.7f,1.9f,.6f),new(.2f,.3f,.4f,.1f))],[0,1,2])]);
        var setup=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[]){BodyBone=1,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(units,"Independent declared units"),BoneRoles=[new("RootMotion",0,"Reviewed actor",true),new("Hips",1,"Reviewed body",true)]};
        var local=bones.Select(b=>b.Rest).ToArray();
        for(int i=0;i<local.Length;i++)local[i]=local[i] with{Rotation=local[i].Rotation+new Vector3(7-i*3,-9+i*2,13-i*5),Scale=local[i].Scale*new Vector3(1.03f,.91f,1.07f)};
        return(RigDescriptor.Prepare(scene,setup),local);
    }
    static EvaluatedPose Pose(RigDescriptor rig,Transform[] local)=>AnimationService.Evaluate(rig.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
    static BodyRelativeReachGoal[] Reach(RigDescriptor rig,Transform[] input)
    {
        var pose=Pose(rig,input);float length=Vector3.Distance(rig.Rest.World[2].Translation,rig.Rest.World[3].Translation);
        var binding=new BodyRelativeReachBinding("Independent reviewed branch",[2,3],[2,3],1,1,length,length,"Original Cartesian goal with moving reviewed ancestor");
        return[new(binding,pose.World[3].Translation+new Vector3(.03f,-.04f,.02f)*rig.CharacteristicLength,pose.World[2].Translation,Vector3.Zero,false,[],1,length)];
    }
}
