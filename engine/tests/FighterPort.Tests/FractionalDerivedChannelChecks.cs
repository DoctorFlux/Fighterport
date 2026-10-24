using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using FighterPort.Core;

// Original native float/cubic and procedural-work witnesses. No large motion
// evaluation, game fixture, native worker or detector supplies the expected result.
internal static class FractionalDerivedChannelChecks
{
    sealed record Fixture(FighterProject Project,ControlRigSetup Rig);
    sealed record Prepared(Fixture Source,Fixture Target,RetargetRequest Request,BaselineMotionTransfer Baseline,BodyReachFitting Reach,Transform[] Input);
    public static void Run(Action<bool,string> check)
    {
        var p=Prepare(2,true);var articulation=p.Reach.SceneFitting.Articulation!;
        string original=RetargetDependencies.Hash(new{p.Source,p.Target,Input=p.Input});
        float length=RigDescriptor.Prepare(p.Target.Project.Scene,p.Target.Rig).CharacteristicLength;
        var authentic=AnimationService.Evaluate(p.Source.Project.Scene,p.Source.Project.Clips[0],0);
        float nativeUlp=MathF.BitIncrement(1.5f)-1.5f;double[] controls=[-.49,.49,.1,-.49];
        double[] Parameters(double nativeStep){var x=new double[articulation.Variables];x[1]=nativeStep/length;return x;}
        // Open cubic correction: endpoint slopes use one-sided differences,
        // interior slopes use their two neighboring native controls.
        double Cubic(double time)
        {
            int segment=Math.Min(controls.Length-2,(int)Math.Floor(time));double u=time-segment,u2=u*u,u3=u2*u;
            double Slope(int i)=>i==0?controls[1]-controls[0]:i==3?controls[3]-controls[2]:(controls[i+1]-controls[i-1])/2;
            return (2*u3-3*u2+1)*controls[segment]+(u3-2*u2+u)*Slope(segment)+
                (-2*u3+3*u2)*controls[segment+1]+(u3-u2)*Slope(segment+1);
        }
        var collocations=Enumerable.Range(0,7).Select(i=>i/2d).ToArray();
        bool unchanged=true;foreach(double time in collocations)
        {
            var actual=articulation.ApplyCorrection(p.Input,Parameters(Cubic(time)*nativeUlp));unchanged&=actual.SequenceEqual(p.Input);
            articulation.RecordTrajectory(p.Input,actual,0,0);p.Reach.SceneFitting.RecordPreparedPose(authentic,p.Input,actual);
        }
        var before=p.Reach.SceneFitting.Receipt();var mapping=p.Baseline.Mapping;
        ChannelTransferLedger Ledger()=>RetargetChannelLedger.Build(p.Source.Project.Scene,p.Source.Project.Clips[0],p.Target.Project.Scene,mapping,RootMotionPolicy.Preserve,sceneBody:p.Reach.SceneFitting.Receipt());
        check(unchanged&&before.TranslationProposals==0&&before.AppliedProposals==0&&before.Articulated?.ActualBodyTranslationDerived==false&&Ledger().SceneBodyDerivedChannels is null,
            "Known sub-half-ULP cubic corrections round away at every integer/midpoint collocation; requested nonzero parameters alone do not fabricate a derived native translation");
        var fractional=articulation.ApplyCorrection(p.Input,Parameters(Cubic(1.125)*nativeUlp));var after=p.Reach.SceneFitting.Receipt();var ledger=Ledger();
        check(Cubic(1.125)>.5&&fractional[1].Translation.Y==MathF.BitIncrement(1.5f)&&after.TranslationProposals==0&&after.AppliedProposals==0&&
            after.Articulated?.ActualBodyTranslationDerived==true&&ledger.SceneBodyDerivedChannels is{} derived&&
            derived.SourceBody.BoneIndex==1&&derived.TargetBody.Bone.BoneIndex==1&&derived.TargetBody.Channels.SequenceEqual(new[]{"Translation.X","Translation.Y","Translation.Z"})&&
            after.FullSkinClearanceEvaluations==before.FullSkinClearanceEvaluations&&after.Evaluations==before.Evaluations,
            "An independently known cubic extremum becomes one actual native body-translation ULP between collocations and receives precise channel ownership without running another fit or full-skin measurement");
        check(fractional[0]==p.Input[0]&&fractional.Select((t,i)=>t.Scale==p.Input[i].Scale&&t.Rotation==p.Input[i].Rotation&&(i==1||t.Translation==p.Input[i].Translation)).All(v=>v)&&
            original==RetargetDependencies.Hash(new{p.Source,p.Target,Input=p.Input}),
            "Fractional ownership recording changes no actor, native rotation/scale, non-body translation, source clip, rest hierarchy, skin weight or incoming pose");
        var json=JsonNode.Parse(JsonSerializer.Serialize(after.Articulated,ProjectStore.Json))!.AsObject();json.Remove("ActualBodyTranslationDerived");
        string historical=json.ToJsonString(ProjectStore.Json);var restored=JsonSerializer.Deserialize<ArticulatedSceneReceipt>(historical,ProjectStore.Json)!;
        check(restored.ActualBodyTranslationDerived is null&&JsonSerializer.Serialize(restored,ProjectStore.Json)==historical,
            "A historical articulated receipt retains unknown fractional-translation evidence and exact JSON bytes instead of inventing a false boolean or changing immutable ancestry");

        var shortPath=Prepare(2,true);shortPath.Reach.PrepareTrajectory(new(),new(30),2,false,false,false);
        long oldShortCost=(long)400000*3*3*(NativeChainReach.MaximumIterations+1);
        bool shortAllowed=Budget(shortPath.Reach,400000) is null;
        check(shortPath.Reach.TemporalReceipt is not null&&oldShortCost>BodyReachFitting.MaximumKinematicSteps&&shortAllowed,
            "An actually prepared short native trajectory accepts hypothetical400000 pure sampling queries under the retained bone cap without charging118.8million IK iterations that it never executes");
        var shortBoneFailure=Budget(shortPath.Reach,600000);
        check(shortBoneFailure is NotSupportedException&&shortBoneFailure.Message.Contains("combined source/target",StringComparison.Ordinal),
            "Prepared-trajectory sampling still rejects600000 queries that exceed the real combined source/target bone-work ceiling");
        var longPath=Prepare(19,false);var beforePreparation=Budget(longPath.Reach,8000);
        longPath.Reach.PrepareTrajectory(new(),new(30),2,false,false,false);var afterPreparation=Budget(longPath.Reach,8000);
        check(beforePreparation is NotSupportedException&&beforePreparation.Message.Contains("native-path projection",StringComparison.Ordinal)&&afterPreparation is null&&
            longPath.Reach.TemporalReceipt is null&&longPath.Reach.Diagnostics.Any(d=>d.Code=="BODY_TRAJECTORY_BINDING_REVIEW")&&longPath.Reach.SceneFitting.Receipt().AppliedProposals==0,
            "An explicit reviewed-body draft lacking reviewed interior articulation keeps its baseline-only diagnostic and stops charging old iterative reach work only after preparation; no hidden pose solver is substituted");
        var longBoneFailure=Budget(longPath.Reach,200000);
        check(longBoneFailure is NotSupportedException&&longBoneFailure.Message.Contains("combined source/target",StringComparison.Ordinal),
            "The explicit baseline-only route also retains its combined native bone-work guard instead of becoming an unbounded sampling exemption");
    }
    static Exception? Budget(BodyReachFitting reach,int references)
    {
        try{typeof(BodyReachFitting).GetMethod("ValidateEvaluationBudget",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(reach,[references,0]);return null;}
        catch(TargetInvocationException ex){return ex.InnerException??ex;}
    }
    static Prepared Prepare(int segments,bool reviewedInterior)
    {
        var source=Make("Original source",segments,reviewedInterior,1,.2f);var target=Make("Original destination",segments,reviewedInterior,1.1f,-.1f);
        var request=new RetargetRequest(source.Project,source.Project.Clips[0],source.Rig,target.Project,target.Rig,RootMotionPolicy.Preserve,false,default,new(30),
            new(){ExplicitSurface=true,SourceFloor=0,TargetFloor=0},new(){AutomaticFitting=true,AutomaticContacts=false,VolumePolicy=BodyVolumePolicy.ReportOverlap},null,null,null,null);
        var a=RigDescriptor.Prepare(source.Project.Scene,source.Rig);var b=RigDescriptor.Prepare(target.Project.Scene,target.Rig);var baseline=new BaselineMotionTransfer(request,a,b);
        var input=baseline.EvaluatePose(AnimationService.Evaluate(source.Project.Scene,source.Project.Clips[0],0));return new(source,target,request,baseline,new(request,a,b,baseline),input);
    }
    static Fixture Make(string label,int segments,bool reviewedInterior,float lastLength,float surfaceY)
    {
        var bones=new List<Bone>{new(label+"0",-1,Transform.Identity),new(label+"1",0,Transform.Identity with{Translation=new(0,1.5f,0)}),
            new(label+"2",1,Transform.Identity with{Translation=Vector3.UnitY})};
        for(int i=0;i<=segments;i++)bones.Add(new(label+(i+3),i==0?2:i+2,Transform.Identity with{Translation=new(i==segments?lastLength:1,0,0)}));
        int end=bones.Count-1;var vertices=new[]{new Vector3(-.5f,surfaceY,0),new(.5f,surfaceY,0),new(0,surfaceY,1)}
            .Select(v=>new Vertex(v,Vector3.UnitY,Vector2.Zero,new(end,0,0,0),new(1,0,0,0),Vector4.One)).ToArray();
        var scene=new Scene(label,bones.ToArray(),[new("Original native material",vertices,[0,1,2])]);
        var roles=new List<RigMapping>{new("RootMotion",0,"Independent native actor",true),new("Hips",1,"Independent native body",true),new("Chest",2,"Independent native torso",true),
            new("Path.Start",3,"Reviewed native path root",true),new("Path.End",end,"Reviewed native endpoint",true)};
        if(reviewedInterior)for(int i=4;i<end;i++)roles.Add(new("Path.Interior."+i,i,"Reviewed existing native path interior",true));
        var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[]){BodyBone=1,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Independent native unit"),
            BoneRoles=roles.ToArray(),VirtualChains=[new("Original native path",Enumerable.Range(3,segments+1).ToArray(),"Complete native ancestry supplied as data",true)]};
        var clip=new AnimationClip(Guid.NewGuid(),"Original native fixture",2,new(30),false,[]);return new(new(1,Guid.NewGuid(),label,scene,[clip]){ControlRigs=[rig]},rig);
    }
}
