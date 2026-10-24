using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using FighterPort.Core;

// Independent Cartesian placement: a broad square is on Y=0, while a generic
// octahedral protrusion changes radius around Y=6 without changing the rig.
// No detector, fitted objective, anatomical name or native character is an oracle.
internal static class BodyVolumePolicyChecks
{
    sealed record Fixture(FighterProject Project,ControlRigSetup Rig);
    sealed record Result(Transform[] Before,Transform[] After,EvaluatedPose Pose,SceneBodyFitReceipt Receipt);

    public static async Task RunAsync(string directory,Action<bool,string> check)
    {
        Directory.CreateDirectory(directory);
        var small=Make("opaque-first",.5f);var large=Make("opaque-second",10);
        string original=RetargetDependencies.Hash(new{Small=small,Large=large});
        var report=Fit(small,large,BodyVolumePolicy.ReportOverlap);
        var strict=Fit(small,large,BodyVolumePolicy.RequireVertexClearance);
        check(Math.Abs(SupportHeight(large,report.Pose))<.001f&&Math.Abs(report.After[1].Translation.Y-2)<.001f&&
            Math.Abs(Minimum(large,report.Pose)+4)<.001f,
            "Default volume reporting retains the independently grounded broad support and native body height despite a resized generic protrusion extending four units below the floor");
        check(report.Receipt.VolumePolicy==BodyVolumePolicy.ReportOverlap&&report.Receipt.FullSkinClearanceEvaluations==1&&
            Math.Abs(report.Receipt.MaximumInitialPenetration-4)<.00001f&&Math.Abs(report.Receipt.MaximumFinalPenetration-4)<.001f&&
            report.Receipt.ClearanceClippedEvaluations==0&&report.Receipt.Status=="Review",
            "Permitted incidental overlap is actually measured and retained as a review result, without a fictitious clearance certificate or hidden projection");
        check(Math.Abs(Minimum(large,strict.Pose))<.00004f&&Math.Abs(SupportHeight(large,strict.Pose)-4)<.00004f&&
            strict.Receipt.VolumePolicy==BodyVolumePolicy.RequireVertexClearance&&strict.Receipt.ClearanceClippedEvaluations>0,
            "The same independent geometry under explicit strict clearance raises every body-owned surface by four units and records that distinct tradeoff");
        var reverse=Fit(large,small,BodyVolumePolicy.ReportOverlap);
        check(Math.Abs(SupportHeight(small,reverse.Pose))<.001f&&Math.Abs(reverse.After[1].Translation.Y-2)<.001f&&
            Math.Abs(Minimum(small,reverse.Pose))<.001f&&reverse.Receipt.SourcePenetrationObservations>0&&
            Math.Abs(reverse.Receipt.MaximumSourcePenetration-4)<.00001f,
            "Reverse transfer preserves the independent floor support while reporting the authentic oversized source overlap instead of copying its negative clearance");

        var permutation=Permute(large);var supplied=Fit(small,permutation,BodyVolumePolicy.ReportOverlap);
        var reversePermutation=Fit(permutation,small,BodyVolumePolicy.ReportOverlap);
        check(Math.Abs(SupportHeight(permutation,supplied.Pose))<.001f&&Math.Abs(Minimum(permutation,supplied.Pose)+4)<.001f&&
            Vector3.Distance(supplied.Pose.World[1].Translation,report.Pose.World[1].Translation)<.00003f&&
            Vector3.Distance(reversePermutation.Pose.World[1].Translation,reverse.Pose.World[1].Translation)<.00003f,
            "Reviewed non-anatomical bindings, unrelated names and a valid sibling storage permutation preserve the same measured support placement independently in both directions");
        var again=Fit(small,large,BodyVolumePolicy.ReportOverlap);
        check(again.After.SequenceEqual(report.After)&&new[]{report,strict,reverse,supplied,reversePermutation}.All(r=>
            r.Before.Select((t,i)=>t.Rotation==r.After[i].Rotation&&t.Scale==r.After[i].Scale&&(i==1||t.Translation==r.After[i].Translation)).All(v=>v)),
            "Volume policy is deterministic and changes only the existing body's translation, retaining actor travel, local lengths, rotations, full turns and authored scale");

        var shared=new FittingSettings{AutomaticContacts=true,VolumePolicy=BodyVolumePolicy.RequireVertexClearance,BodyLeanDegrees=3};
        var inherited=new FittingOverrides();var explicitZero=new FittingOverrides{VolumePolicy=BodyVolumePolicy.ReportOverlap,AutomaticContacts=false};
        check(inherited.Resolve(shared).VolumePolicy==BodyVolumePolicy.RequireVertexClearance&&
            explicitZero.Resolve(shared).VolumePolicy==BodyVolumePolicy.ReportOverlap&&!explicitZero.Resolve(shared).AutomaticContacts&&
            explicitZero.Resolve(shared).BodyLeanDegrees==3,
            "An unset volume policy inherits, while explicit enum zero and false contact settings override the shared preset without erasing unrelated values");
        check(FittingOverrides.From(shared).Resolve(new())==shared&&
            (explicitZero with{VolumePolicy=null}).Resolve(shared).VolumePolicy==BodyVolumePolicy.RequireVertexClearance,
            "Copy settings retains volume policy and resetting only that override returns to inheritance without a false/zero sentinel");
        bool unknownRejected=false;try{(shared with{VolumePolicy=(BodyVolumePolicy)731}).Validate();}catch(NotSupportedException){unknownRejected=true;}
        check(unknownRejected&&JsonSerializer.Deserialize<FittingSettings>("{}",ProjectStore.Json)?.VolumePolicy==BodyVolumePolicy.ReportOverlap,
            "Unknown volume-policy values are rejected and old fitting settings without a policy use the newly documented report-overlap default");

        var source=small.Project;var clip=source.Clips[0];var second=clip with{Id=Guid.NewGuid(),Revision=Guid.NewGuid()};source=source with{Clips=[clip,second]};
        var document=new EditorDocument(large.Project);
        using var batch=new RetargetBatch(document,source,small.Rig,large.Rig,[clip.Id,second.Id],shared:shared,contacts:false);
        batch.SetOverride(second.Id,explicitZero);
        batch.SetShared(shared with{BodyLeanDegrees=5});
        check(batch.Rows[0].Settings.VolumePolicy==BodyVolumePolicy.RequireVertexClearance&&batch.Rows[1].Settings.VolumePolicy==BodyVolumePolicy.ReportOverlap&&
            batch.Rows.All(r=>r.Settings.BodyLeanDegrees==5)&&batch.Rows[1].Settings.AutomaticContacts==false&&
            document.Project==large.Project&&!document.CanUndo,
            "Changing one shared field preserves explicit per-clip volume/contact choices, updates inherited values and does not edit target keys or history");
        var saved=large.Project with{FittingProfiles=[new("strict","Explicit strict clearance",shared,RootMotionPolicy.Preserve),
            new("report","Explicit measured overlap",shared with{VolumePolicy=BodyVolumePolicy.ReportOverlap,AutomaticContacts=false},RootMotionPolicy.Preserve)],RetargetBatches=[batch.Receipt()]};
        string file=Path.Combine(directory,"body-volume-policies.fighterport");await ProjectStore.SaveAsync(saved,file);var reopened=await ProjectStore.LoadAsync(file);
        var rows=reopened.RetargetBatches.Single().Rows;
        check(reopened.FittingProfiles.Single(p=>p.Key=="strict").Settings.VolumePolicy==BodyVolumePolicy.RequireVertexClearance&&
            reopened.FittingProfiles.Single(p=>p.Key=="report").Settings.VolumePolicy==BodyVolumePolicy.ReportOverlap&&
            rows.Single(r=>r.SourceClip==clip.Id).Overrides.VolumePolicy is null&&
            rows.Single(r=>r.SourceClip==second.Id).Overrides is{VolumePolicy:BodyVolumePolicy.ReportOverlap,AutomaticContacts:false},
            "Actual project save/reopen preserves strict and report presets, inherited batch policy and explicit per-clip enum-zero/false overrides");
        var historical=JsonNode.Parse(JsonSerializer.Serialize(strict.Receipt,ProjectStore.Json))!.AsObject();historical.Remove("VolumePolicy");
        var oldReceipt=JsonSerializer.Deserialize<SceneBodyFitReceipt>(historical.ToJsonString(),ProjectStore.Json);
        check(oldReceipt?.VolumePolicy is null&&JsonSerializer.Deserialize<SceneBodyFitReceipt>(JsonSerializer.Serialize(report.Receipt,ProjectStore.Json),ProjectStore.Json)?.VolumePolicy==BodyVolumePolicy.ReportOverlap&&
            JsonSerializer.Deserialize<SceneBodyFitReceipt>(JsonSerializer.Serialize(strict.Receipt,ProjectStore.Json),ProjectStore.Json)?.VolumePolicy==BodyVolumePolicy.RequireVertexClearance,
            "A historical measurement receipt without VolumePolicy remains unknown, distinct from both explicit new policies rather than relabeling strict history");
        batch.SetOverride(second.Id,explicitZero with{VolumePolicy=null});
        check(batch.Rows[1].Settings.VolumePolicy==BodyVolumePolicy.RequireVertexClearance&&batch.Rows[1].Settings.AutomaticContacts==false&&
            original==RetargetDependencies.Hash(new{Small=small,Large=large})&&
            AnimationControls.DeformChecksum(reopened.Scene)==AnimationControls.DeformChecksum(large.Project.Scene),
            "Resetting volume policy keeps the explicit contact request, and fitting/settings/persistence preserve original clips, native graphs, rest geometry, skinning and weights");

        var budget=PrepareBudget(small,large);budget.ValidateEvaluationBudget(1,1);var bounded=budget.Receipt();
        check(budget.Enabled&&budget.ChangesGeometryIdentity&&bounded.Evaluations==0&&bounded.SensorWorkBudget is >0&&
            bounded.EstimatedSensorWork is >0&&bounded.EstimatedSensorWork<bounded.SensorWorkBudget,
            "A real prepared unequal-volume scene reports positive bounded offline sensor work before evaluating any pose");
        string? rejectedBudget=null;try{budget.ValidateEvaluationBudget(int.MaxValue,int.MaxValue);}catch(NotSupportedException ex){rejectedBudget=ex.Message;}
        var excessive=budget.Receipt();
        check(rejectedBudget is not null&&rejectedBudget.Contains("resource limit",StringComparison.OrdinalIgnoreCase)&&
            rejectedBudget.Contains("No pose was sampled",StringComparison.Ordinal)&&rejectedBudget.Contains("no anatomy or contact contradiction",StringComparison.Ordinal)&&
            excessive.Evaluations==0&&excessive.EstimatedSensorWork>excessive.SensorWorkBudget&&excessive.EstimatedSensorWork is >0&&
            original==RetargetDependencies.Hash(new{Small=small,Large=large}),
            "Maximum integer reference/probe counts fail the resource preflight without overflow, sampling, mutation or an anatomy/contact contradiction claim");
        var legacyBudget=JsonNode.Parse(JsonSerializer.Serialize(bounded,ProjectStore.Json))!.AsObject();
        legacyBudget.Remove("SensorWorkBudget");legacyBudget.Remove("EstimatedSensorWork");
        var unknownBudget=JsonSerializer.Deserialize<SceneBodyFitReceipt>(legacyBudget.ToJsonString(),ProjectStore.Json);
        var roundTripBudget=JsonSerializer.Deserialize<SceneBodyFitReceipt>(JsonSerializer.Serialize(bounded,ProjectStore.Json),ProjectStore.Json);
        check(unknownBudget is{SensorWorkBudget:null,EstimatedSensorWork:null}&&
            roundTripBudget?.SensorWorkBudget==bounded.SensorWorkBudget&&roundTripBudget?.EstimatedSensorWork==bounded.EstimatedSensorWork,
            "Historical receipts without work-budget evidence retain unknown values while new explicit budget and estimate survive serialization");
        await File.WriteAllTextAsync(Path.Combine(directory,"body-volume-measurements.json"),JsonSerializer.Serialize(new
        {Claim="Independent procedural support/volume policy only; no contact, anatomical action, native codec or game acceptance",Report=report.Receipt,Strict=strict.Receipt,Reverse=reverse.Receipt,
            ReportSupportY=SupportHeight(large,report.Pose),ReportMinimumY=Minimum(large,report.Pose),StrictSupportY=SupportHeight(large,strict.Pose)},new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false}));
    }

    static SceneBodyFitting PrepareBudget(Fixture source,Fixture target)
    {
        var request=new RetargetRequest(source.Project,source.Project.Clips[0],source.Rig,target.Project,target.Rig,RootMotionPolicy.Preserve,false,default,null,
            new(){ExplicitSurface=true,SourceFloor=0,TargetFloor=0},new(){AutomaticContacts=false,VolumePolicy=BodyVolumePolicy.ReportOverlap},null,null,null,null);
        var a=RigDescriptor.Prepare(source.Project.Scene,source.Rig);var b=RigDescriptor.Prepare(target.Project.Scene,target.Rig);
        return new(request,a,b,new BaselineMotionTransfer(request,a,b));
    }
    static Result Fit(Fixture source,Fixture target,BodyVolumePolicy policy)
    {
        var ground=new GroundContactSettings{ExplicitSurface=true,SourceFloor=0,TargetFloor=0};
        var request=new RetargetRequest(source.Project,source.Project.Clips[0],source.Rig,target.Project,target.Rig,RootMotionPolicy.Preserve,false,default,null,ground,
            new(){AutomaticContacts=false,VolumePolicy=policy},null,null,null,null);
        var a=RigDescriptor.Prepare(source.Project.Scene,source.Rig);var b=RigDescriptor.Prepare(target.Project.Scene,target.Rig);
        var baseline=new BaselineMotionTransfer(request,a,b);var authentic=AnimationService.Evaluate(source.Project.Scene,request.Clip,0);var local=baseline.EvaluatePose(authentic);
        var fitter=new SceneBodyFitting(request,a,b,baseline);var after=fitter.Fit(authentic,local);return new(local,after,Pose(target.Project.Scene,after),fitter.Receipt());
    }
    static Fixture Make(string name,float radius)
    {
        var bones=new[]{new Bone(name+"_0",-1,Transform.Identity),new(name+"_1",0,Transform.Identity with{Translation=new(0,2,0)}),
            new(name+"_2",1,Transform.Identity with{Translation=new(0,-2,0)}),new(name+"_3",1,Transform.Identity with{Translation=new(0,4,0)})};
        Mesh Mesh(string label,Vector3[] points,uint[] indices,int bone)=>new(label,points.Select(p=>new Vertex(p,Vector3.UnitY,Vector2.Zero,new(bone,0,0,0),new(1,0,0,0),Vector4.One)).ToArray(),indices);
        Vector3[] square=[new(-4,0,-4),new(4,0,-4),new(4,0,4),new(-4,0,4)];
        Vector3[] directions=[Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY,Vector3.UnitZ,-Vector3.UnitZ];
        var meshes=new[]{Mesh("Original broad surface",square,[0,1,2,0,2,3],2),Mesh("Original generic protrusion",directions.Select(p=>new Vector3(0,6,0)+radius*p).ToArray(),[2,0,4,2,4,1,2,1,5,2,5,0,3,4,0,3,1,4,3,5,1,3,0,5],3)};
        var scene=new Scene(name,bones,meshes);var roles=new[]{"RootMotion","Hips","Region.A","Region.B"};
        var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[]){BodyBone=1,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Independent unit convention"),
            BoneRoles=roles.Select((role,i)=>new RigMapping(role,i,"Supplied Cartesian correspondence",true)).ToArray()};
        var clip=new AnimationClip(Guid.NewGuid(),"Original unnamed action",2,new(30),false,[]);
        return new(new(1,Guid.NewGuid(),name,scene,[clip]){ControlRigs=[rig]},rig);
    }
    static Fixture Permute(Fixture fixture)
    {
        int[] order=[0,1,3,2];var inverse=order.Select((old,index)=>(old,index)).ToDictionary(p=>p.old,p=>p.index);
        var scene=fixture.Project.Scene with{SkeletonRevision=Guid.NewGuid(),Bones=order.Select((old,index)=>fixture.Project.Scene.Bones[old] with{Name="unrelated_"+index,Parent=old==0?-1:inverse[fixture.Project.Scene.Bones[old].Parent]}).ToArray(),
            Meshes=fixture.Project.Scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Joints=new(inverse[(int)v.Joints.X],inverse[(int)v.Joints.Y],inverse[(int)v.Joints.Z],inverse[(int)v.Joints.W])}).ToArray()}).ToArray()};
        var rig=fixture.Rig with{Id=Guid.NewGuid(),SkeletonFingerprint=AnimationControls.Fingerprint(scene),BoneRoles=fixture.Rig.BoneRoles.Select(r=>r with{Bone=inverse[r.Bone]}).ToArray()};
        return new(fixture.Project with{Scene=scene,ControlRigs=[rig]},rig);
    }
    static EvaluatedPose Pose(Scene scene,Transform[] local)=>AnimationService.Evaluate(scene,null,0,local.Select((value,index)=>(value,index)).ToDictionary(p=>p.index,p=>p.value));
    static float SupportHeight(Fixture fixture,EvaluatedPose pose)=>fixture.Project.Scene.Meshes[0].Vertices.Average(v=>AnimationService.Skin(v,pose).Y);
    static float Minimum(Fixture fixture,EvaluatedPose pose)=>fixture.Project.Scene.Meshes.SelectMany(m=>m.Vertices).Min(v=>AnimationService.Skin(v,pose).Y);
}
