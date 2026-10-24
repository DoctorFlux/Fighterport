using System.Numerics;
using FighterPort.Core;

internal static class DetailedBoneChecks
{
    public static async Task RunAsync(string directory,Action<bool,string> check)
    {
        var original=ProceduralFixture.Create();var bones=original.Scene.Bones.ToList();var roles=new List<RigMapping>();
        foreach(var (side,palm) in new[]{("L",6),("R",9)})foreach(string finger in new[]{"Index","Middle","Ring","Little","Thumb"})
        {
            int first=bones.Count;bones.Add(new($"{side}_{finger}_A",palm,Transform.Identity with{Translation=new(side=="L"?-.12f:.12f,0,.03f),Rotation=new(0,0,10)}));bones.Add(new($"{side}_{finger}_B",first,Transform.Identity with{Translation=new(side=="L"?-.08f:.08f,0,0),Rotation=new(0,0,-15)}));
            roles.Add(new($"Finger.{finger}.Proximal.{side}",first,"Original reviewed finger branch",true));roles.Add(new($"Finger.{finger}.Distal.{side}",first+1,"Original reviewed distal branch",true));
        }
        int shared=bones.Count;bones.Add(new("SharedAntenna",3,Transform.Identity with{Translation=new(0,.15f,0)}));int misleading=bones.Count;bones.Add(new("SameNameWrongBranch",3,Transform.Identity with{Translation=new(0,.1f,0)}));
        var sourceScene=original.Scene with{Bones=bones.ToArray(),SkeletonRevision=Guid.NewGuid()};var a=SurfaceContactChecks.Profile(sourceScene);a=a with{BoneRoles=a.BoneRoles.Concat(roles).ToArray()};
        var targetBones=bones.Select((bone,i)=>i>=16&&i<shared?bone with{Name="UnrelatedDigit"+i,Rest=bone.Rest with{Translation=bone.Rest.Translation*1.5f,Rotation=bone.Rest.Rotation+new Vector3(0,0,25)}}:bone).ToArray();targetBones[misleading]=targetBones[misleading] with{Parent=12};
        var targetScene=sourceScene with{Bones=targetBones.Append(new("TargetTail",1,Transform.Identity with{Translation=new(0,0,.3f)})).ToArray(),SkeletonRevision=Guid.NewGuid()};var b=SurfaceContactChecks.Profile(targetScene);b=b with{BoneRoles=b.BoneRoles.Concat(roles).ToArray()};
        Scene DigitGeometry(Scene model){var bind=AnimationService.Evaluate(model,null,0);var vertices=new List<Vertex>();var indices=new List<uint>();foreach(var role in roles){uint start=(uint)vertices.Count;foreach(var offset in new[]{new Vector3(.07f,0,0),new(.06f,.01f,0),new(.06f,0,.01f)})vertices.Add(new(Vector3.Transform(offset,bind.World[role.Bone]),Vector3.UnitZ,Vector2.Zero,new(role.Bone,0,0,0),new(1,0,0,0),Vector4.One));indices.AddRange([start,start+1,start+2]);}return model with{Meshes=model.Meshes.Append(new("Original weighted digits",vertices.ToArray(),indices.ToArray())).ToArray()};}
        sourceScene=DigitGeometry(sourceScene);targetScene=DigitGeometry(targetScene);
        var report=RetargetBoneMapping.Build(sourceScene,a,targetScene,b);
        check(report.Bones.Count(m=>m.Role.StartsWith("Finger."))==20,"All twenty reviewed finger joints map across different bone names and local bind axes");
        check(report.Bones.Any(m=>m.Source==shared&&m.Target==shared&&m.DetailChannels),"Compatible shared accessory maps through its parent branch and bind geometry beyond body roles");
        check(report.SourceOnly.Any(m=>m.Bone==misleading&&m.Reason.Contains("parent"))&&report.TargetOnly.Any(m=>m.Name=="TargetTail"),"Identical names on incompatible branches are reported; target-specific bones remain explicit");
        var edits=roles.ToDictionary(r=>r.Bone,r=>sourceScene.Bones[r.Bone].Rest with{Rotation=sourceScene.Bones[r.Bone].Rest.Rotation+new Vector3(0,0,r.SemanticRole.Contains("Proximal")?30:-20)});
        edits[16]=edits[16] with{Translation=edits[16].Translation+new Vector3(.02f,0,0)};edits[17]=edits[17] with{Scale=new(1.2f,1,1)};edits[shared]=sourceScene.Bones[shared].Rest with{Rotation=new(0,20,0)};
        var clip=new AnimationClip(Guid.NewGuid(),"Original complete hand pose",12,new(60),false,edits.Select(p=>new BoneTrack(sourceScene.Bones[p.Key].Name,p.Value.Channels().Select(v=>(Curve?)new Curve([new(0,v),new(11,v)])).ToArray())).ToArray());
        var donor=original with{Scene=sourceScene,Clips=[clip],ControlRigs=[a]};var target=original with{Scene=targetScene,Clips=[],ControlRigs=[b]};var checksum=AnimationControls.DeformChecksum(targetScene);var baked=RetargetService.Bake(donor,clip,a,target,b);
        check(baked.Value is not null,"Detailed finger/accessory rotation, translation and scale bake without requiring IK or donor skeleton replacement: "+string.Join("; ",baked.Diagnostics.Where(d=>d.Error).Select(d=>d.Message)));
        var value=baked.Value!;var pose=AnimationService.Evaluate(targetScene,value.Clip,4.375);
        check(Math.Abs(pose.Local[16].Rotation.Z-65)<.001f&&Math.Abs(pose.Local[17].Rotation.Z-(-10))<.001f,"Independent finger-angle expectations include both altered target bind axes and source joint curls");
        check(Math.Abs(pose.Local[16].Translation.X-(targetBones[16].Rest.Translation.X+.03f))<.00001f&&Math.Abs(pose.Local[17].Scale.X-1.2f)<.00001f,"Detail channels preserve relative scale and proportion-scaled translation instead of dropping finger SRT");
        var point=AnimationService.Skin(targetScene.Meshes[^1].Vertices[0],pose);float radians=65*MathF.PI/180;check(Vector3.Distance(point,new(-1.23f+.07f*MathF.Cos(radians),1.66f+.07f*MathF.Sin(radians),.045f))<.00001f,"Actual weighted finger geometry matches an independent planar rotation/translation expectation");
        check(value.Clip.Tracks.Any(t=>t.Bone=="SharedAntenna")&&!value.Clip.Tracks.Any(t=>t.Bone=="SameNameWrongBranch")&&pose.Local[^1]==targetScene.Bones[^1].Rest,"Shared accessory is keyed while incompatible/target-only bones retain native local channels");
        string path=Path.Combine(directory,"detailed-fingers.fighterport");await ProjectStore.SaveAsync(RetargetService.Apply(target,value),path);var reopened=await ProjectStore.LoadAsync(path);
        check(reopened.Clips[0].Retarget!.BoneMapping!.Bones.Count(m=>m.Role.StartsWith("Finger."))==20&&reopened.LibrarySources[0].Controls.BoneRoles.Count(r=>r.SemanticRole.StartsWith("Finger."))==20,"Saved target recipe and authentic source setup retain full finger mapping evidence through reopen");
        check(checksum==AnimationControls.DeformChecksum(targetScene),"Detailed transfer preserves native bones, hierarchy, bind data and skin weights");
    }
}
