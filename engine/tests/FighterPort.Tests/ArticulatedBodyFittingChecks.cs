using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using FighterPort.Core;

// Original Cartesian rig/skin fixtures. The reference poses below are authored
// rotations with independently known surface heights, not solver/detector output.
internal static class ArticulatedBodyFittingChecks
{
    sealed record Fixture(FighterProject Project,ControlRigSetup Rig,float Hips,float Torso,float ArmVertical,float Units,float AxisDegrees);
    sealed record Result(Fixture Source,Fixture Target,Transform[] Input,Transform[] Output,EvaluatedPose Pose,SceneBodyFitReceipt Receipt,float PalmBefore,float PalmAfter,float SupportError);

    public static async Task RunAsync(string directory,Action<bool,string> check)
    {
        Directory.CreateDirectory(directory);
        var first=Make("opaque-one",2,1,1,1);var second=Make("opaque-two",2,2,.6f,1);
        var reversedSource=WithPose(second,Reference(second,-30));
        var unequalLegs=Make("opaque-third",1.5f,2,.6f,.75f);
        string original=RetargetDependencies.Hash(new{First=first,Second=second,Reverse=reversedSource,Unequal=unequalLegs});
        var forward=Fit(first,second);var reverse=Fit(reversedSource,first);var unequal=Fit(first,unequalLegs);
        check(Math.Abs(forward.PalmBefore-.8f)<.00002f&&Math.Abs(reverse.PalmBefore+1f/6)<.00002f&&
            Math.Abs(unequal.PalmBefore-.3f)<.00002f&&new[]{forward,reverse,unequal}.All(r=>SupportError(r.Target,Pose(r.Target,r.Input))<.00002f)&&
            GroundError(first,AnimationService.Evaluate(first.Project.Scene,first.Project.Clips[0],0))<.00002f&&
            GroundError(reversedSource,AnimationService.Evaluate(reversedSource.Project.Scene,reversedSource.Project.Clips[0],0))<.00002f,
            "Independent unequal arm/torso/leg geometry starts with grounded sole patches but wrong native palm heights +0.8, -1/6 and +0.3 in the two transfer directions");
        var referenceForward=Reference(second,-30,forward.Input);var referenceReverse=Reference(first,-30,reverse.Input);var referenceUnequal=Reference(unequalLegs,-15,unequal.Input);
        var knownForward=Pose(second,referenceForward);var knownReverse=Pose(first,referenceReverse);var knownUnequal=Pose(unequalLegs,referenceUnequal);
        await File.WriteAllTextAsync(Path.Combine(directory,"authored-reference-geometry.json"),JsonSerializer.Serialize(new[]
        {
            (Result:forward,Local:referenceForward,Pose:knownForward),(Result:reverse,Local:referenceReverse,Pose:knownReverse),(Result:unequal,Local:referenceUnequal,Pose:knownUnequal)
        }.Select(r=>new{Target=r.Result.Target.Project.Name,r.Result.PalmBefore,AuthoredPalm=PalmHeight(r.Result.Target,r.Pose),
            AuthoredSoles=SupportError(r.Result.Target,r.Pose),AuthoredMaximumGroundGap=GroundError(r.Result.Target,r.Pose),FreeMinimum=FreeMinimum(r.Result.Target,r.Pose),
            MaximumOffset=MaxOffset(r.Result.Input,r.Local),ActorInput=r.Result.Input[0].Channels(),ActorReference=r.Local[0].Channels()}),ProjectStore.Json));
        check(new[]{(second,knownForward),(first,knownReverse),(unequalLegs,knownUnequal)}.All(p=>
                GroundError(p.Item1,p.Item2)<.00002f&&FreeMinimum(p.Item1,p.Item2)>.2f)&&
            MaxOffset(forward.Input,referenceForward)<=45.0001f&&MaxOffset(reverse.Input,referenceReverse)<=45.0001f&&
            MaxOffset(unequal.Input,referenceUnequal)<=45.0001f,
            "Authored native rotations independently demonstrate reachable palm-plus-two-sole placement with free mixed-weight skin above ground and no required bone offset above 45 degrees");
        foreach(var (result,label) in new[]{(forward,"forward"),(reverse,"reverse"),(unequal,"unequal legs")})
        {
            float bound=RigDescriptor.Prepare(result.Target.Project.Scene,result.Target.Rig).CharacteristicLength*.0001f/result.Target.Units;
            float initial=GroundError(result.Target,Pose(result.Target,result.Input));float after=GroundError(result.Target,result.Pose);
            check(Math.Abs(result.PalmAfter)<Math.Abs(result.PalmBefore)*.5f&&after<initial*.5f,
                $"Coordinated {label} fitting reduces the independent palm gap and maximum error over all three actual skin patches by at least half; before={result.PalmBefore:G9}, after={result.PalmAfter:G9}, all-patch-error={after:G9}, strict-support-bound={bound:G9}; this improvement is not a contact pass");
        }
        check(new[]{forward,reverse,unequal}.All(r=>r.Output[0]==r.Input[0]&&r.Output.Select((t,i)=>
                t.Scale==r.Input[i].Scale&&(i==r.Target.Rig.BodyBone||t.Translation==r.Input[i].Translation)).All(v=>v)&&
                r.Output.Where((t,i)=>t.Rotation!=r.Input[i].Rotation).Any()),
            "Articulated scene fitting actually changes native rotations while preserving the exact incoming actor channels, every scale and all non-body translations instead of stretching or lifting the whole rig");
        check(new[]{forward,reverse,unequal}.All(r=>FreeMinimum(r.Target,r.Pose)>.1f),
            "The independent mixed-weight free extremity stays above the floor while the three low patches are coordinated");
        var repeated=Fit(first,second);var disabled=Fit(first,second,false);
        check(repeated.Output.SequenceEqual(forward.Output)&&disabled.Output.SequenceEqual(disabled.Input),
            "The normal body fitter is deterministic without playback-history seeds, and Automatic fitting off retains the exact baseline local transforms");
        var solvedReceipts=new[]{forward,reverse,unequal}.Select(r=>r.Receipt.Articulated!).ToArray();
        check(solvedReceipts.All(r=>r.TerminationEvidenceVersion==ArticulatedSceneFitting.TerminationEvidenceVersion&&r.ConvergenceStatus=="NotCertified"&&
                r.AcceptedSteps is >0&&r.RejectedTrials is >=0&&r.LinearSolveStops==0&&
                r.NoAcceptedLowerCostStops+r.IterationLimitStops==r.Evaluations)&&
            solvedReceipts.Any(r=>r.IterationLimitStops==1&&r.AcceptedSteps==ArticulatedSceneFitting.IterationLimit),
            "Actual unequal-proportion geometry solves record admissible steps and exclusive stop counts, including a measured eight-step iteration-limit stop; lower cost is never labeled certified convergence");
        string[] stopFields=["TerminationEvidenceVersion","AcceptedSteps","RejectedTrials","NoAcceptedLowerCostStops","IterationLimitStops","LinearSolveStops","ConvergenceStatus"];
        var historical=JsonNode.Parse(JsonSerializer.Serialize(solvedReceipts[0],ProjectStore.Json))!.AsObject();foreach(string field in stopFields)historical.Remove(field);
        string historicalJson=historical.ToJsonString(ProjectStore.Json);var oldReceipt=JsonSerializer.Deserialize<ArticulatedSceneReceipt>(historicalJson,ProjectStore.Json)!;
        check(oldReceipt.TerminationEvidenceVersion is null&&oldReceipt.AcceptedSteps is null&&oldReceipt.RejectedTrials is null&&
            oldReceipt.NoAcceptedLowerCostStops is null&&oldReceipt.IterationLimitStops is null&&oldReceipt.LinearSolveStops is null&&
            oldReceipt.ConvergenceStatus is null&&oldReceipt.Version==solvedReceipts[0].Version&&oldReceipt.Improved==solvedReceipts[0].Improved,
            "Historical articulated receipts without termination fields remain readable with unknown null evidence, retaining their original measurements instead of inventing zero stops or convergence");
        check(JsonSerializer.Serialize(oldReceipt,ProjectStore.Json)==historicalJson,
            "Re-serializing a historical receipt reproduces its exact JSON structure and bytes, omitting all seven absent termination fields so preserved source-proof hashes are not rewritten");

        var permuted=Permute(second);var renamedForward=Fit(first,permuted);var renamedReverse=Fit(Permute(reversedSource),first);
        check(SkinDifference(second,forward.Pose,permuted,renamedForward.Pose)<.00004f&&
            SkinDifference(first,reverse.Pose,first,renamedReverse.Pose)<.00004f,
            "Supplied semantic bindings with unrelated native names and a valid branch-storage permutation retain equivalent actually skinned fits independently in both directions");
        var centimetres=Make("opaque-centimetres",2,1,1,1,100,90);var turnedTarget=Make("opaque-axis",2,2,.6f,1,1,-90);
        var converted=Fit(centimetres,turnedTarget);
        check(SkinDifference(second,forward.Pose,turnedTarget,converted.Pose)<.00008f&&converted.Output[0]==converted.Input[0],
            "Equivalent declared source centimetres and independently rotated source/target frames produce the same physical skin placement without normalizing native axes or actor channels");
        var anisotropic=FitPose(first,second,true,new(1.1f,.9f,1.2f));
        check(anisotropic.Receipt.Articulated is{Improved:>0,Unsupported:0}&&
            GroundError(second,anisotropic.Pose)<.5f*GroundError(second,Pose(second,anisotropic.Input))&&
            anisotropic.Output.Where((t,i)=>t.Rotation!=anisotropic.Input[i].Rotation).Any()&&anisotropic.Output[0]==anisotropic.Input[0]&&
            anisotropic.Output.Select((t,i)=>t.Scale==anisotropic.Input[i].Scale&&(i==second.Rig.BodyBone||t.Translation==anisotropic.Input[i].Translation)&&t.Channels().All(float.IsFinite)).All(v=>v)&&
            second.Project.Scene.Meshes.SelectMany(m=>m.Vertices).All(v=>PoseGeometry.Finite(AnimationService.Skin(v,anisotropic.Pose))),
            "Direct numerical skin fitting improves actual geometry with positive authored anisotropic scale, retaining every native scale/length channel, actor and finite skin without a rigid-IK or contact claim");
        var reflected=FitPose(first,second,true,new(-1.1f,.9f,1.2f));var singular=FitPose(first,second,true,new(0,.9f,1.2f));
        check(new[]{reflected,singular}.All(r=>r.Output.SequenceEqual(r.Input)&&r.Receipt.Articulated is{Unsupported:>0,Improved:0}),
            "Reflected and zero native animated scale remain explicitly unsupported with unchanged input poses rather than normalization, hidden stretching or a successful numerical-fit claim");
        check(new[]{reflected,singular}.All(r=>r.Receipt.Articulated is{AcceptedSteps:0,RejectedTrials:0,NoAcceptedLowerCostStops:0,IterationLimitStops:0,LinearSolveStops:0,ConvergenceStatus:"NotCertified"}),
            "Existing unsupported-scale exits retain their original unsupported result and record no fictitious optimization step or stop");
        check(new[]{forward,reverse,unequal,repeated,renamedForward,renamedReverse,converted,anisotropic,reflected,singular}.All(r=>r.Receipt.Status=="Review"&&
                r.Receipt.VolumePolicy==BodyVolumePolicy.ReportOverlap)&&original==RetargetDependencies.Hash(new{First=first,Second=second,Reverse=reversedSource,Unequal=unequalLegs}),
            "Procedural scene fitting remains a report-overlap review result and preserves immutable clips, native hierarchy/rests/inverse-bind inputs, skin weights and original project identities");
        var audited=new[]{forward,reverse,anisotropic,disabled,reflected,singular}.Select(r=>(Result:r,Ledger:Ledger(r),Raw:Ledger(r,false))).ToArray();
        check(audited.All(a=>
        {
            var expected=a.Result.Output.Select((t,i)=>(t,i)).Where(p=>p.t.Rotation!=a.Result.Input[p.i].Rotation).Select(p=>p.i).Order().ToArray();
            return (a.Ledger.ArticulatedSceneDerivedChannels??[]).Select(c=>c.Bone.BoneIndex).Order().SequenceEqual(expected)&&
                (a.Ledger.ArticulatedSceneDerivedChannels??[]).All(c=>c.Bone.SkeletonRevision==a.Result.Target.Project.Scene.SkeletonRevision&&
                    c.Bone.NativeName==a.Result.Target.Project.Scene.Bones[c.Bone.BoneIndex].Name&&c.Channels.SequenceEqual(new[]{"Rotation.X","Rotation.Y","Rotation.Z"}));
        })&&audited.Any(a=>a.Ledger.ArticulatedSceneDerivedChannels is{Length:>0}),
            "The transfer ledger records exactly the native rotation bones actually changed by coordinated fitting, with real revision/name identities and no unchanged or unsupported branches fabricated as derived channels");
        check(audited.All(a=>
        {
            var translated=a.Result.Output.Select((t,i)=>(t,i)).Where(p=>p.t.Translation!=a.Result.Input[p.i].Translation).Select(p=>p.i).ToArray();
            var derived=a.Ledger.SceneBodyDerivedChannels;
            return translated.Length==0?derived is null:translated.Length==1&&translated[0]==a.Result.Target.Rig.BodyBone&&
                derived is not null&&derived.SourceBody.BoneIndex==a.Result.Source.Rig.BodyBone&&derived.TargetBody.Bone.BoneIndex==translated[0]&&
                derived.TargetBody.Channels.SequenceEqual(new[]{"Translation.X","Translation.Y","Translation.Z"});
        })&&audited.All(a=>RetargetDependencies.Hash(a.Ledger.SourceChannels)==RetargetDependencies.Hash(a.Raw.SourceChannels))&&
            audited.Single(a=>ReferenceEquals(a.Result,reverse)).Ledger.SourceChannels.Length==45,
            "Only an actually translated body receives derived translation ownership; disabled/rejected poses receive none, and all 45 authentic reverse-source rotation channel entries remain retained independently of fitted derivatives");
        var savedLedger=audited[0].Ledger;
        var recipe=new RetargetRecipe(first.Project.Clips[0].Revision,second.Project.Scene.SkeletonRevision,first.Rig.Id,second.Rig.Id,RootMotionPolicy.Preserve,RetargetPipeline.Version)
        {Backend=RetargetBackendIdentity.CurrentId,BackendSchema=2,ChannelLedger=savedLedger,IsDraft=true,
            Pipeline=new(RetargetBackendIdentity.CurrentId,2,[],["Independent procedural geometry fixture"],["No contact/native/action acceptance"]){SceneBody=forward.Receipt}};
        string receiptProject=Path.Combine(directory,"articulated-channel-receipt.fighterport");
        await ProjectStore.SaveAsync(second.Project with{Clips=[second.Project.Clips[0] with{Retarget=recipe}]},receiptProject);
        var reopened=await ProjectStore.LoadAsync(receiptProject);var restored=reopened.Clips.Single().Retarget;
        var legacy=JsonNode.Parse(JsonSerializer.Serialize(savedLedger,ProjectStore.Json))!.AsObject();legacy.Remove("ArticulatedSceneDerivedChannels");
        check(restored is{IsDraft:true}&&RetargetDependencies.Hash(restored.ChannelLedger)==RetargetDependencies.Hash(savedLedger)&&
            RetargetDependencies.Hash(restored.Pipeline?.SceneBody)==RetargetDependencies.Hash(forward.Receipt)&&
            JsonSerializer.Deserialize<ChannelTransferLedger>(legacy.ToJsonString(),ProjectStore.Json) is{ArticulatedSceneDerivedChannels:null} old&&
            RetargetDependencies.Hash(old.SourceChannels)==RetargetDependencies.Hash(savedLedger.SourceChannels),
            "Actual project save/reopen retains fitted channel ownership and the review recipe/measurement receipt; historical ledgers missing the new field retain their source entries without inventing derived rotations");
        await File.WriteAllTextAsync(Path.Combine(directory,"articulated-body-geometry.json"),JsonSerializer.Serialize(new
        {
            Claim="Independent static Cartesian articulated/support counterexamples only; no detected contact, action-style, temporal, native codec, displayed or game acceptance",
            Cases=new[]{forward,reverse,unequal,renamedForward,renamedReverse,converted,anisotropic,reflected,singular}.Select(r=>new
            {Source=r.Source.Project.Name,Target=r.Target.Project.Name,r.PalmBefore,r.PalmAfter,r.SupportError,MaximumSurfaceError=GroundError(r.Target,r.Pose),
                FrozenPositionBound=RigDescriptor.Prepare(r.Target.Project.Scene,r.Target.Rig).CharacteristicLength*.0001f/r.Target.Units,
                StrictAllThreeSurfacesWithinBound=GroundError(r.Target,r.Pose)<=RigDescriptor.Prepare(r.Target.Project.Scene,r.Target.Rig).CharacteristicLength*.0001f/r.Target.Units,
                FreeMinimum=FreeMinimum(r.Target,r.Pose),r.Receipt})
        },new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false}));
    }

    static Result Fit(Fixture source,Fixture target,bool enabled=true)=>FitPose(source,target,enabled,null);
    static ChannelTransferLedger Ledger(Result result,bool includeScene=true)
    {
        var source=result.Source;var target=result.Target;
        var mapping=RetargetBoneMapping.Build(source.Project.Scene,source.Rig,target.Project.Scene,target.Rig);
        return RetargetChannelLedger.Build(source.Project.Scene,source.Project.Clips[0],target.Project.Scene,mapping,RootMotionPolicy.Preserve,sceneBody:includeScene?result.Receipt:null);
    }
    static Result FitPose(Fixture source,Fixture target,bool enabled,Vector3? animatedScale)
    {
        string before=RetargetDependencies.Hash(new{Source=source,Target=target});
        var request=new RetargetRequest(source.Project,source.Project.Clips[0],source.Rig,target.Project,target.Rig,RootMotionPolicy.Preserve,false,default,null,
            new(){ExplicitSurface=true,SourceFloor=0,TargetFloor=0},new(){AutomaticFitting=enabled,AutomaticContacts=false,VolumePolicy=BodyVolumePolicy.ReportOverlap},null,null,null,null);
        var a=RigDescriptor.Prepare(source.Project.Scene,source.Rig);var b=RigDescriptor.Prepare(target.Project.Scene,target.Rig);
        var baseline=new BaselineMotionTransfer(request,a,b);var authentic=AnimationService.Evaluate(source.Project.Scene,request.Clip,0);
        var input=baseline.EvaluatePose(authentic);
        if(animatedScale is {} scale)input[3]=input[3] with{Scale=scale};
        string untouched=RetargetDependencies.Hash(input);var fitter=new SceneBodyFitting(request,a,b,baseline);
        var output=fitter.Fit(authentic,input);var pose=Pose(target,output);
        if(before!=RetargetDependencies.Hash(new{Source=source,Target=target})||untouched!=RetargetDependencies.Hash(input))throw new InvalidOperationException("Body fitting mutated a native source/target or its input local array.");
        return new(source,target,input,output,pose,fitter.Receipt(),PalmHeight(target,Pose(target,input)),PalmHeight(target,pose),SupportError(target,pose));
    }
    static Fixture Make(string label,float hips,float torso,float arm,float legs,float units=1,float axisDegrees=0)
    {
        Transform T(float x,float y)=>Transform.Identity with{Translation=new Vector3(x,y,0)*units};
        var bones=new[]{new Bone(label+"_0",-1,Transform.Identity with{Rotation=new(0,0,axisDegrees)}),new(label+"_1",0,T(0,hips)),
            new(label+"_2",1,T(torso,0)),new(label+"_3",2,T(0,0)),new(label+"_4",3,T(.75f*arm,-arm)),new(label+"_5",4,T(-.75f*arm,-arm)),
            new(label+"_6",1,T(0,0)),new(label+"_7",6,T(.5f*legs,-legs)),new(label+"_8",7,T(0,-legs)),
            new(label+"_9",1,T(0,0)),new(label+"_10",9,T(-.5f*legs,-legs)),new(label+"_11",10,T(0,-legs)),
            new(label+"_12",2,T(0,0)),new(label+"_13",12,T(.75f*arm,arm)),new(label+"_14",13,T(.75f*arm,arm))};
        var empty=new Scene(label,bones,[]);var rest=AnimationService.Evaluate(empty,null,0);var turn=Matrix4x4.CreateRotationZ(axisDegrees*MathF.PI/180);
        Mesh Patch(string name,int bone,int other=-1)
        {
            var centre=rest.World[bone].Translation;
            var points=new[]{new Vector3(-.08f,0,-.08f),new Vector3(.08f,0,-.08f),new Vector3(0,0,.08f)};
            return new(name,points.Select(p=>new Vertex(centre+Vector3.TransformNormal(p*units,turn),Vector3.TransformNormal(Vector3.UnitY,turn),Vector2.Zero,
                new(bone,other<0?0:other,0,0),other<0?new(1,0,0,0):new(.8f,.2f,0,0),Vector4.One)).ToArray(),[0,1,2]);
        }
        var scene=empty with{Meshes=[Patch("Independent low surface",5),Patch("Independent support alpha",8),Patch("Independent support beta",11),Patch("Independent free mixed surface",14,13)]};
        var roles=new[]{"RootMotion","Hips","Chest","Low.Root","Low.Middle","Low.End","SupportA.Root","SupportA.Middle","SupportA.End","SupportB.Root","SupportB.Middle","SupportB.End","Free.Root","Free.Middle","Free.End"};
        var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[]){BodyBone=1,
            Axes=new(Vector3.TransformNormal(Vector3.UnitX,turn),Vector3.TransformNormal(Vector3.UnitY,turn),Vector3.UnitZ),Coordinates=new(units,"Original independent Cartesian unit/frame convention"),
            BoneRoles=roles.Select((role,i)=>new RigMapping(role,i,"Supplied reviewed Cartesian identity",true)).ToArray(),
            VirtualChains=[new("Branch.low",[3,4,5],"Complete existing native path",true),new("Branch.support.alpha",[6,7,8],"Complete existing native path",true),
                new("Branch.support.beta",[9,10,11],"Complete existing native path",true),new("Branch.free",[12,13,14],"Complete existing native path",true)]};
        var clip=new AnimationClip(Guid.NewGuid(),"Original unnamed relationship",2,new(30),false,[new(bones[0].Name,[null,null,null,null,null,new([new(0,axisDegrees+720)]),null,null,null])]);
        return new(new(1,Guid.NewGuid(),label,scene,[clip]){ControlRigs=[rig]},rig,hips,torso,arm,units,axisDegrees);
    }
    static Transform[] Reference(Fixture fixture,float bodyDegrees,Transform[]? incoming=null)
    {
        var result=incoming?.ToArray()??fixture.Project.Scene.Bones.Select(b=>b.Rest).ToArray();
        // A reference to the target preserves its exact incoming actor, even
        // when a direct transfer chose an equivalent Euler winding. The source
        // authored pose independently retains its original720-degree channel.
        if(incoming is null)result[0]=result[0] with{Rotation=new(0,0,fixture.AxisDegrees+720)};
        float height=fixture.Hips+fixture.Torso*MathF.Sin(bodyDegrees*MathF.PI/180);
        float beta=-MathF.Acos(height/(2*fixture.ArmVertical))*180/MathF.PI;
        foreach(var (bone,degrees) in new[]{(1,bodyDegrees),(3,beta-bodyDegrees),(5,-beta),(6,-bodyDegrees),(9,-bodyDegrees)})
            result[bone]=result[bone] with{Rotation=new(0,0,degrees)};
        return result;
    }
    static Fixture WithPose(Fixture fixture,Transform[] pose)
    {
        var clip=fixture.Project.Clips[0] with{Tracks=pose.Select((t,i)=>new BoneTrack(fixture.Project.Scene.Bones[i].Name,
            [null,null,null,new([new(0,t.Rotation.X)]),new([new(0,t.Rotation.Y)]),new([new(0,t.Rotation.Z)]),null,null,null])).ToArray()};
        return fixture with{Project=fixture.Project with{Clips=[clip]}};
    }
    static Fixture Permute(Fixture fixture)
    {
        int[] order=[0,1,2,9,10,11,12,13,14,6,7,8,3,4,5];var inverse=order.Select((old,index)=>(old,index)).ToDictionary(v=>v.old,v=>v.index);
        var original=fixture.Project.Scene;
        var scene=original with{SkeletonRevision=Guid.NewGuid(),Bones=order.Select((old,i)=>original.Bones[old] with{Name="arbitrary_"+i,Parent=original.Bones[old].Parent<0?-1:inverse[original.Bones[old].Parent]}).ToArray(),
            Meshes=original.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Joints=new(inverse[(int)v.Joints.X],inverse[(int)v.Joints.Y],inverse[(int)v.Joints.Z],inverse[(int)v.Joints.W])}).ToArray()}).ToArray()};
        var names=order.Select((old,i)=>(original.Bones[old].Name,Target:scene.Bones[i].Name)).ToDictionary(v=>v.Name,v=>v.Target);
        var rig=fixture.Rig with{Id=Guid.NewGuid(),SkeletonFingerprint=AnimationControls.Fingerprint(scene),BodyBone=inverse[1],BoneRoles=fixture.Rig.BoneRoles.Select(m=>m with{Bone=inverse[m.Bone]}).ToArray(),
            VirtualChains=fixture.Rig.VirtualChains!.Select(c=>c with{Bones=c.Bones.Select(b=>inverse[b]).ToArray()}).ToArray()};
        return fixture with{Project=fixture.Project with{Scene=scene,Clips=fixture.Project.Clips.Select(c=>c with{Tracks=c.Tracks.Select(t=>t with{Bone=names[t.Bone]}).ToArray()}).ToArray(),ControlRigs=[rig]},Rig=rig};
    }
    static EvaluatedPose Pose(Fixture f,Transform[] local)=>AnimationService.Evaluate(f.Project.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
    static float Height(Fixture f,Vertex v,EvaluatedPose pose)=>Vector3.Dot(AnimationService.Skin(v,pose),f.Rig.Axes!.Up)/f.Units;
    static float PalmHeight(Fixture f,EvaluatedPose pose)=>f.Project.Scene.Meshes[0].Vertices.Average(v=>Height(f,v,pose));
    static float SupportError(Fixture f,EvaluatedPose pose)=>f.Project.Scene.Meshes.Skip(1).Take(2).SelectMany(m=>m.Vertices).Max(v=>Math.Abs(Height(f,v,pose)));
    static float GroundError(Fixture f,EvaluatedPose pose)=>f.Project.Scene.Meshes.Take(3).SelectMany(m=>m.Vertices).Max(v=>Math.Abs(Height(f,v,pose)));
    static float FreeMinimum(Fixture f,EvaluatedPose pose)=>f.Project.Scene.Meshes[3].Vertices.Min(v=>Height(f,v,pose));
    static float MaxOffset(Transform[] a,Transform[] b)=>a.Select((t,i)=>Vector3.Distance(t.Rotation,b[i].Rotation)).Max();
    static float SkinDifference(Fixture a,EvaluatedPose pa,Fixture b,EvaluatedPose pb)
    {
        Vector3 Canonical(Fixture f,Vector3 p)=>Vector3.TransformNormal(p,Matrix4x4.CreateRotationZ(-f.AxisDegrees*MathF.PI/180))/f.Units;
        return a.Project.Scene.Meshes.Zip(b.Project.Scene.Meshes).SelectMany(pair=>pair.First.Vertices.Zip(pair.Second.Vertices).Select(v=>
            Vector3.Distance(Canonical(a,AnimationService.Skin(v.First,pa)),Canonical(b,AnimationService.Skin(v.Second,pb))))).Max();
    }
}
