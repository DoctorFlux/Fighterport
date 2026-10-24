using System.Numerics;
using System.Text.Json;
using FighterPort.Core;

internal static class BodyReachWorkflowChecks
{
    sealed record Fixture(FighterProject Project,ControlRigSetup Rig);
    public static async Task RunAsync(string directory,Action<bool,string> check)
    {
        Directory.CreateDirectory(directory);string input=Path.Combine(directory,"authentic-reach-fixture.bytes");
        await File.WriteAllBytesAsync(input,[70,80,45,79,82,73,71,73,78,65,76]);string inputHash=ProjectStore.Hash(input);
        var shortRig=Make("opaque-short",1,1,input,inputHash);var longRig=Make("opaque-long",3,1,input,inputHash);
        string original=RetargetDependencies.Hash(new{Short=shortRig.Project,Long=longRig.Project});
        var floor=new GroundContactSettings{ExplicitSurface=true,SurfaceMode=SupportSurfaceMode.Ground,SourceFloor=-10,TargetFloor=-10};
        var outputs=new List<(Fixture Source,Fixture Target,RetargetBake Bake)>();
        foreach(var (source,target,expected,rawExpected) in new[]{(shortRig,longRig,new Vector3(2,2,0),new Vector3(3,1,0)),(longRig,shortRig,new Vector3(1.5f,.5f,0),new Vector3(1,1,0))})
        {
            var clip=source.Project.Clips[0];var request=new RetargetRequest(source.Project,clip,source.Rig,target.Project,target.Rig,RootMotionPolicy.Preserve,false,default,null,null,new(){AutomaticContacts=false},null,null,null,null);
            var a=RigDescriptor.Prepare(source.Project.Scene,source.Rig);var b=RigDescriptor.Prepare(target.Project.Scene,target.Rig);
            var baseline=new BaselineMotionTransfer(request,a,b);var fitting=new BodyReachFitting(request,a,b,baseline);
            var raw=Evaluate(target.Project.Scene,baseline.Evaluate(1));var prepared=Evaluate(target.Project.Scene,fitting.Evaluate(1));
            var result=RetargetService.Bake(source.Project,clip,source.Rig,target.Project,target.Rig,useContacts:false,fitting:new(){AutomaticContacts=false});
            var bake=Require(result);outputs.Add((source,target,bake));var displayed=AnimationService.Evaluate(target.Project.Scene,bake.Clip,1);
            check(Vector3.Distance(raw.World[4].Translation,rawExpected)<.00002f&&Vector3.Distance(prepared.World[4].Translation,expected)<.00004f&&Vector3.Distance(displayed.World[4].Translation,expected)<.00004f,
                "Normal pipeline fits independent unequal-segment Cartesian reach in "+(source==shortRig?"short-to-long":"long-to-short")+" instead of retaining mismatched baseline geometry");
            var turned=AnimationService.Evaluate(target.Project.Scene,bake.Clip,2);var turnExpected=new Vector3(-expected.Y,expected.X,0);
            check(Vector3.Distance(turned.World[4].Translation,turnExpected)<.00005f&&Vector3.Distance(prepared.World[4].Translation,raw.World[4].Translation)>.1f,
                "Source parent rotation follows the fitted native body-relative action, with actual changed target posing in both directions");
            check(prepared.Local.Where((t,i)=>t.Rotation!=raw.Local[i].Rotation).Any()&&prepared.Local.Select((t,i)=>t.Scale==raw.Local[i].Scale&&t.Translation==raw.Local[i].Translation).All(v=>v)&&
                PoseGeometry.RotationDifference(PoseGeometry.Rotation(prepared.World[4]),PoseGeometry.Rotation(raw.World[4]))<.01f,
                "Reach fitting changes only native rotation channels, retains every local translation/scale and preserves distal world orientation");
            var off=Require(RetargetService.Bake(source.Project,clip,source.Rig,target.Project,target.Rig,useContacts:false,fitting:new(){AutomaticContacts=false,AutomaticFitting=false}));
            check(off.Clip.Retarget?.Pipeline?.BodyReach is {Enabled:false}&&Vector3.Distance(AnimationService.Evaluate(target.Project.Scene,off.Clip,1).World[4].Translation,rawExpected)<.00002f&&Keys(off.Clip)!=Keys(bake.Clip),
                "Automatic fitting off returns the raw rest-relative baseline and records disabled reach independently of requested contacts");
        }
        var first=outputs[0];var sourceClip=first.Source.Project.Clips[0];var targetProject=first.Target.Project;
        check(first.Bake.Clip.Retarget?.Pipeline?.BodyReach is {Enabled:true,ExactRig:false,Chains.Length:1} receipt&&receipt.Chains[0].Solved>0&&receipt.GoalVersion==BodyRelativeReachGoals.Version&&receipt.SolverVersion==NativeChainReach.Version&&first.Bake.Clip.Retarget.Pipeline.Trajectory is null,
            "The normal pipeline records actual prepared reach and native-path solving versions without an alternate contact trajectory backend");
        var withFloor=Require(RetargetService.Bake(first.Source.Project,sourceClip,first.Source.Rig,targetProject,first.Target.Rig,ground:floor));
        var withEdge=Require(RetargetService.Bake(first.Source.Project,sourceClip,first.Source.Rig,targetProject,first.Target.Rig,ground:floor with{SurfaceMode=SupportSurfaceMode.Edge,SourceEdge=new(Vector3.UnitX,1),TargetEdge=new(Vector3.UnitX,2)}));
        check(Keys(first.Bake.Clip)==Keys(withFloor.Clip)&&Keys(withFloor.Clip)==Keys(withEdge.Clip)&&new[]{first.Bake,withFloor,withEdge}.All(b=>b.Contacts.Length==0)&&withEdge.Grounding is {DetectedIntervals:0,Phases.Length:0},
            "Floor and optional ledge remain measurement-only geometry: no old contact solve, invented intervals or altered reach coefficients");
        var second=sourceClip with{Id=Guid.NewGuid(),Revision=Guid.NewGuid()};var selectedSource=first.Source.Project with{Clips=[sourceClip,second]};
        var document=new EditorDocument(targetProject);
        using(var batch=new RetargetBatch(document,selectedSource,first.Source.Rig,first.Target.Rig,[sourceClip.Id,second.Id],shared:new(){AutomaticContacts=false},contacts:false))
        {
            await batch.RunAsync();
            check(batch.Rows.All(r=>r.Bake is not null&&Keys(r.Bake.Clip)==Keys(first.Bake.Clip)&&r.Bake.Contacts.Length==0&&r.Bake.Clip.Retarget?.Pipeline?.BodyReach is {Enabled:true})&&batch.Rows.All(r=>r.State==RetargetRowState.Review),
                "Selected same-source batches and single preparation produce identical fitted keys and retain independent review drafts");
            bool rejected=false;try{batch.Apply();}catch(InvalidOperationException){rejected=true;}
            check(rejected&&!document.CanUndo&&ReferenceEquals(document.Project,targetProject),"Unaccepted reach/contact drafts cannot enter target history through batch Apply");
        }
        // Persisting an explicit review draft is low-level project composition,
        // not an Apply-ready claim or a native-format export.
        string save=Path.Combine(directory,"body-reach-review.fighterport");await ProjectStore.SaveAsync(RetargetService.Apply(targetProject,first.Bake),save);
        var reopened=await ProjectStore.LoadAsync(save);var restored=reopened.Clips.Single(c=>c.Id==first.Bake.Clip.Id);var ancestry=reopened.LibrarySources.Single(s=>s.Id==restored.SourceSnapshot);
        check(Keys(restored)==Keys(first.Bake.Clip)&&RetargetDependencies.Hash(restored.Retarget?.Pipeline?.BodyReach)==RetargetDependencies.Hash(first.Bake.Clip.Retarget?.Pipeline?.BodyReach)&&restored.Retarget?.IsDraft==true,
            "Project save/reopen retains editable fitted keys, the measured versioned reach receipt and unaccepted draft state");
        check(ancestry.Clip.Revision==sourceClip.Revision&&Keys(ancestry.Clip)==Keys(sourceClip)&&ancestry.SourceFiles.Single().Sha256==inputHash&&RetargetDependencies.Hash(ancestry.Controls)==RetargetDependencies.Hash(first.Source.Rig)&&
            AnimationControls.DeformChecksum(ancestry.Scene)==AnimationControls.DeformChecksum(first.Source.Project.Scene)&&AnimationControls.DeformChecksum(reopened.Scene)==AnimationControls.DeformChecksum(targetProject.Scene),
            "Saved fitted motion retains authentic source ancestry/proofs and both unchanged native skeleton/skin setups");
        var syntheticPass=new ContactValidation(RetargetAcceptance.Version,true,true,.0001f,.0001f,0,0,0,0,0,0,1,1,1);
        check(new[]{"BODY_REACH_REVIEW","BODY_REACH_TOPOLOGY_REVIEW"}.All(code=>!RetargetAcceptance.Ready(syntheticPass,[new(code,"Independent reach blocker",false)],[],true)),
            "A numerical contact pass cannot bypass unresolved reach or shared-topology review gates");
        check(original==RetargetDependencies.Hash(new{Short=shortRig.Project,Long=longRig.Project})&&ProjectStore.Hash(input)==inputHash,
            "Normal-pipeline reach preparation, batching and persistence preserve original source channels, native rigs and bytes");
        await File.WriteAllTextAsync(Path.Combine(directory,"body-reach-workflow.json"),JsonSerializer.Serialize(new{Version=RetargetPipeline.Version,Claim="Independent reach/workflow only; contacts, native and displayed quality remain unvalidated",Receipts=outputs.Select(o=>o.Bake.Clip.Retarget?.Pipeline?.BodyReach).ToArray()},new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false}));
    }
    static Fixture Make(string label,float first,float second,string input,string hash)
    {
        var bones=new[]{new Bone(label+"_actor",-1,Transform.Identity),new(label+"_frame",0,Transform.Identity),new(label+"_start",1,Transform.Identity),new(label+"_middle",2,Transform.Identity with{Translation=new(first,0,0)}),new(label+"_end",3,Transform.Identity with{Translation=new(second,0,0)}),new(label+"_branch",1,Transform.Identity with{Translation=Vector3.UnitZ})};
        var vertices=new[]{new Vector3(first+second,.1f,0),new(first+second,0,.1f),new(first+second,-.1f,0)}.Select(p=>new Vertex(p,Vector3.UnitZ,Vector2.Zero,new(4,2,0,0),new(.8f,.2f,0,0),Vector4.One)).ToArray();
        var scene=new Scene(label,bones,[new("Independently authored weighted triangle",vertices,[0,1,2])]);
        var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[]){Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Original independent fixture units"),VirtualChains=[new("Reviewed appendage",[2,3,4],"Supplied complete native path",true)],
            BoneRoles=new[]{"RootMotion","Body.Frame","Appendage.Root","Appendage.Middle","Appendage.End","Other.Branch"}.Select((role,i)=>new RigMapping(role,i,"Independent reviewed fixture correspondence",true)).ToArray()};
        var modelResource=new NativeResourceRef(input,"models/"+label,hash,"original-procedural-fixture",1);var model=new SourceModelRevision(Guid.NewGuid(),label,modelResource,bones,"Original +Y-up authored native graph");
        Curve?[] channels(Curve z)=>[null,null,null,null,null,z,null,null,null];
        var clip=new AnimationClip(Guid.NewGuid(),"Independent action",3,new(30),false,[new(bones[1].Name,channels(new([new(0,0),new(1,0),new(2,90)],Interpolation.Linear))),new(bones[3].Name,channels(new([new(0,90),new(2,90)],Interpolation.Linear)))])
        {Source=new(Guid.NewGuid(),modelResource with{Entry="motions/"+label},modelResource,scene.SkeletonRevision,new(30),model.Id,"Independently authored source model/motion association"){NativeName="Original action"}};
        return new(new(1,Guid.NewGuid(),label,scene,[clip]){ControlRigs=[rig],SourceModels=[model],SourceFiles=[new(input,input,hash)],SourceProvider="procedural.body-reach.v1",SourceAssociationEvidence="Original independent fixture"},rig);
    }
    static EvaluatedPose Evaluate(Scene scene,Transform[] local)=>AnimationService.Evaluate(scene,null,0,local.Select((value,index)=>(value,index)).ToDictionary(p=>p.index,p=>p.value));
    static RetargetBake Require(OperationResult<RetargetBake> result)=>result.Value??throw new InvalidOperationException(string.Join("; ",result.Diagnostics.Select(d=>d.Code+": "+d.Message)));
    static string Keys(AnimationClip clip)=>RetargetDependencies.Hash(clip.Tracks);
}
