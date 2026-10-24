using System.Numerics;
using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

// Private metamorphic fixtures derived from the supplied rigs. These are not
// authentic native resources, independent contact labels or held-out families.
internal static class GeneralizationChecks
{
    sealed record Fixture(FighterProject Project,ControlRigSetup Rig,int[] OldToNew);
    static readonly string[] Motions=["Wait1","DownWaitU","CliffClimbSlow"];
    public static async Task Run(string aPath,string bPath,string output,Action<bool,string> check)
    {
        var originals=new[]{await ProjectStore.LoadAsync(aPath),await ProjectStore.LoadAsync(bPath)};
        var sourceProofs=new[]{aPath,bPath}.Concat(originals.SelectMany(ProjectStore.SourceReferences).Select(r=>r.Path)).Distinct(StringComparer.OrdinalIgnoreCase).Select(p=>(Path:p,Hash:ProjectStore.Hash(p))).ToArray();
        var originalChecksums=originals.Select(p=>AnimationControls.DeformChecksum(p.Scene)).ToArray();var rows=new List<object>();
        foreach(var (source,target) in new[]{(originals[0],originals[1]),(originals[1],originals[0])})
        {
            var sr=BrawlControlProfiles.SetUp(source);var tr=BrawlControlProfiles.SetUp(target);var map=RetargetBoneMapping.Build(source.Scene,sr,target.Scene,tr);
            // Supply the same explicit correspondences to both versions. Names
            // are not being tested as an automatic mapping oracle here.
            var sroles=sr.BoneRoles.ToList();var troles=tr.BoneRoles.ToList();
            for(int i=0;i<map.Bones.Length;i++)
            {
                var pair=map.Bones[i];if(sroles.Any(r=>r.Bone==pair.Source&&troles.Any(t=>t.Bone==pair.Target&&t.SemanticRole==r.SemanticRole)))continue;
                string role="Channel.Binding."+i;sroles.Add(new(role,pair.Source,"Equivalent supplied correspondence",true));troles.Add(new(role,pair.Target,"Equivalent supplied correspondence",true));
            }
            sr=sr with{BoneRoles=sroles.ToArray()};tr=tr with{BoneRoles=troles.ToArray()};
            var plainSource=Make(source,sr,false,201);var plainTarget=Make(target,tr,false,907);
            var changedSource=Make(source,sr,true,201);var changedTarget=Make(target,tr,true,907);
            var singles=new Dictionary<string,RetargetBake>();
            var pd=RigDescriptor.Prepare(plainTarget.Project.Scene,plainTarget.Rig);var cd=RigDescriptor.Prepare(changedTarget.Project.Scene,changedTarget.Rig);
            var active=new HashSet<int>();foreach(var vertex in target.Scene.Meshes.SelectMany(m=>m.Vertices))for(int j=0;j<4;j++)if(vertex.Weights[j]>0)for(int bone=(int)vertex.Joints[j];bone>=0;bone=target.Scene.Bones[bone].Parent)if(!pd.IsActorAncestor(bone)&&!tr.PreservedHelperBones.Contains(bone))active.Add(bone);
            var expectedOrder=active.OrderBy(i=>pd.CanonicalBoneRanks[i]).ToArray();var changedOrder=active.OrderBy(i=>cd.CanonicalBoneRanks[changedTarget.OldToNew[i]]).ToArray();
            await File.WriteAllTextAsync(Path.Combine(output,"canonical-order-"+rows.Count+".json"),JsonSerializer.Serialize(new{Expected=expectedOrder.Select(i=>target.Scene.Bones[i].Name),Permuted=changedOrder.Select(i=>target.Scene.Bones[i].Name)}));
            check(expectedOrder.SequenceEqual(changedOrder),"Equivalent supplied bindings preserve geometry-influencing solver traversal under renaming/storage permutation");
            var identityRoles=Enumerable.Range(0,source.Scene.Bones.Length).Select(i=>new RigMapping("Channel.Identity."+i,i,"Complete equivalent fixture binding",true)).ToArray();
            var identitySource=plainSource.Rig with{BoneRoles=plainSource.Rig.BoneRoles.Concat(identityRoles).ToArray()};
            var identityTarget=changedSource.Rig with{BoneRoles=changedSource.Rig.BoneRoles.Concat(identityRoles.Select(r=>r with{Bone=changedSource.OldToNew[r.Bone]})).ToArray()};
            var identityClip=plainSource.Project.Clips[0];var identity=RetargetPipeline.Bake(new(plainSource.Project,identityClip,identitySource,changedSource.Project,identityTarget,RootMotionPolicy.Preserve,false,default,identityClip.Rate,null,null,null,null,null,null));
            check(identity.Value is not null&&identityClip.Tracks.All(t=>
            {
                int old=Array.FindIndex(source.Scene.Bones,b=>b.Name==t.Bone);var match=identity.Value.Clip.Tracks.Single(p=>p.Bone==changedSource.Project.Scene.Bones[changedSource.OldToNew[old]].Name);
                return t.Channels.Zip(match.Channels).All(p=>p.First is null?p.Second is null:p.Second is not null&&p.First.Keys.SequenceEqual(p.Second.Keys));
            }),"Structurally identical renamed/permuted rigs retain original keys and tangents without an asset-identity shortcut");
            foreach(string name in Motions)
            {
                int index=Array.IndexOf(Motions,name);var originalClip=plainSource.Project.Clips[index];var aliasClip=changedSource.Project.Clips[index];bool ledge=name=="CliffClimbSlow";
                var ground=new GroundContactSettings{ExplicitSurface=true,SourceFloor=0,TargetFloor=0,SourceEdge=ledge?new(sr.Axes!.Forward,0):null,TargetEdge=ledge?new(tr.Axes!.Forward,0):null};
                var timing=new RetargetTiming{TrimEndSeconds=3/originalClip.Rate.FramesPerSecond};
                RetargetRequest Request(Fixture s,Fixture t,AnimationClip clip)=>new(s.Project,clip,s.Rig,t.Project,t.Rig,RootMotionPolicy.Preserve,true,default,clip.Rate,ground,null,timing,null,null,null);
                var request=Request(plainSource,plainTarget,originalClip);var changed=Request(changedSource,changedTarget,aliasClip);
                var low=SurfaceContacts.Detect(request.Source,originalClip,sr with{GroundLimbToleranceFraction=.0001f,GroundFootToleranceFraction=.0001f},ground);
                var high=SurfaceContacts.Detect(request.Source,originalClip,sr with{GroundLimbToleranceFraction=.2f,GroundFootToleranceFraction=.2f},ground);
                string Evidence(GroundContactAnalysis analysis)=>JsonSerializer.Serialize(analysis.Contacts.SelectMany(c=>c.Pins.Select(p=>new{c.Frame,c.EndFrame,p.Role,p.Mode,p.Position})).OrderBy(p=>p.Role,StringComparer.Ordinal).ThenBy(p=>p.Frame),new JsonSerializerOptions{IncludeFields=true});
                check(Evidence(low)==Evidence(high),"Historical rig-specific tolerance fields cannot change automatic contact evidence: "+name);
                var baseline=RetargetPipeline.Bake(request);var permuted=RetargetPipeline.Bake(changed);
                check((baseline.Value is null)==(permuted.Value is null),"Renaming/storage permutation preserves solve availability: "+name);
                float maximum=0;
                if(baseline.Value is {} first&&permuted.Value is {} second)
                {
                    singles[name]=first;
                    // Withheld eighths, including closing time: mapped physical
                    // skin is compared, not names, receipts or detector output.
                    for(double frame=0;frame<=3;frame+=.125)
                    {
                        var p=AnimationService.Evaluate(plainTarget.Project.Scene,first.Clip,frame);var q=AnimationService.Evaluate(changedTarget.Project.Scene,second.Clip,frame);
                        for(int mesh=0;mesh<target.Scene.Meshes.Length;mesh++)for(int v=0;v<target.Scene.Meshes[mesh].Vertices.Length;v++)maximum=Math.Max(maximum,Vector3.Distance(AnimationService.Skin(plainTarget.Project.Scene.Meshes[mesh].Vertices[v],p),AnimationService.Skin(changedTarget.Project.Scene.Meshes[mesh].Vertices[v],q)));
                    }
                    check(maximum<=PoseGeometry.Scale(target.Scene,tr)*RetargetAcceptance.PositionScaleFraction,"Mapped skinned results retain the frozen equivalence bound after renaming/permutation: "+name+" ("+maximum+")");
                }
                else check(baseline.Diagnostics.Select(d=>d.Code).SequenceEqual(permuted.Diagnostics.Select(d=>d.Code)),"Unresolved relabelled fixtures report the same capability class: "+name);
                rows.Add(new{Direction=source.Scene.Name+" to "+target.Scene.Name,Motion=name,TestedSourceSeconds=timing.TrimEndSeconds,maximum,Outcome=baseline.Value is null?"Unresolved in both fixtures":"Mapped geometry equivalent; contact/action acceptance remains separate"});
            }
            var flat=new GroundContactSettings{ExplicitSurface=true,SourceFloor=0,TargetFloor=0};var trim=new RetargetTiming{TrimEndSeconds=3/plainSource.Project.Clips[0].Rate.FramesPerSecond};
            var document=new EditorDocument(plainTarget.Project);
            using(var batch=new RetargetBatch(document,plainSource.Project,plainSource.Rig,plainTarget.Rig,plainSource.Project.Clips.Take(2).Select(c=>c.Id),ground:flat,timing:trim))
            {
                await batch.RunAsync();
                foreach(var row in batch.Rows)
                {
                    int index=Array.FindIndex(plainSource.Project.Clips,c=>c.Id==row.Source.Id);string motion=Motions[index];bool available=singles.TryGetValue(motion,out var single);
                    check(available==(row.Bake is not null),"Single/selected-batch solve availability agrees: "+motion);
                    if(available&&row.Bake is {} multiple)
                    {
                        float difference=SkinDifference(plainTarget.Project.Scene,single!.Clip,plainTarget.Project.Scene,multiple.Clip);
                        check(difference<=PoseGeometry.Scale(target.Scene,tr)*RetargetAcceptance.PositionScaleFraction,"Single/selected-batch actual skin agrees at withheld samples: "+motion+" ("+difference+")");
                    }
                }
                check(!document.Dirty&&document.Project.Clips.Length==plainTarget.Project.Clips.Length,"Preparing a selected batch does not commit keys into the target");
            }
            var rest=AnimationService.Evaluate(plainTarget.Project.Scene,null,0);
            var volume=plainTarget.Project.Scene with{Meshes=plainTarget.Project.Scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>
            {
                var center=Vector3.Zero;for(int j=0;j<4;j++)center+=rest.World[(int)v.Joints[j]].Translation*v.Weights[j];
                return v with{Position=center+(v.Position-center)*1.15f};
            }).ToArray()}).ToArray()};
            check(volume.Bones.SequenceEqual(plainTarget.Project.Scene.Bones)&&volume.Meshes.Zip(plainTarget.Project.Scene.Meshes).All(p=>p.First.Vertices.Zip(p.Second.Vertices).All(v=>v.First.Joints==v.Second.Joints&&v.First.Weights==v.Second.Weights)),"Body-volume fixture preserves identical deform skeleton and skin weights");
            var volumeProject=plainTarget.Project with{Scene=volume};var volumeBake=RetargetPipeline.Bake(new(plainSource.Project,plainSource.Project.Clips[0],plainSource.Rig,volumeProject,plainTarget.Rig,RootMotionPolicy.Preserve,true,default,plainSource.Project.Clips[0].Rate,flat,null,trim,null,null,null));
            bool volumeSolved=volumeBake.Value is not null;float poseChange=0;
            if(volumeBake.Value is {} adjusted&&singles.TryGetValue("Wait1",out var ordinary))
            {
                var originalPose=AnimationService.Evaluate(plainTarget.Project.Scene,ordinary.Clip,0);var changedPose=AnimationService.Evaluate(volume,adjusted.Clip,0);
                poseChange=originalPose.World.Zip(changedPose.World).Max(p=>Vector3.Distance(p.First.Translation,p.Second.Translation));
                check(poseChange>1e-6f,"Identical rig transforms with different body volume produce a different fitted motion ("+poseChange+")");
            }
            rows.Add(new{BodyVolumeScale=1.15,volumeSolved,poseChange,Contacts=volumeBake.Value?.Clip.Retarget?.Validation,Outcome=volumeSolved?"Geometry-sensitive prepared fit; full-body/native acceptance remains separate":"Non-convergence; not a volume-fitting pass",volumeBake.Diagnostics});
            var inferred=AnimationControls.SetUp(changedTarget.Project.Scene);var expected=changedTarget.Rig.Chains;
            int correct=inferred.Chains.Count(c=>expected.Any(e=>c.Role==e.Role&&c.Root==e.Root&&c.Middle==e.Middle&&c.Effector==e.Effector));
            rows.Add(new{AutomaticMapping="Names removed; no supplied bindings",CorrectChains=correct,ExpectedChains=expected.Length,Outcome=correct==expected.Length?"Chain identities agree; review/other roles remain separate":"Needs review; not an automatic mapping pass"});
        }
        check(originals.Select(p=>AnimationControls.DeformChecksum(p.Scene)).SequenceEqual(originalChecksums),"All metamorphic fixtures leave both original rigs, body geometry and skin weights unchanged");
        check(sourceProofs.All(p=>ProjectStore.Hash(p.Path)==p.Hash),"Prepared project and referenced native source bytes remain unchanged throughout generalization checks");
        await File.WriteAllTextAsync(Path.Combine(output,"generalization.json"),JsonSerializer.Serialize(new{Pipeline=RetargetPipeline.Version,GlobalEffectorBand=SurfaceContacts.DefaultEffectorBandFraction,Rows=rows,Scope="Mario/Snake-derived skeletal-only three-frame trim fixtures, both directions. No native export, independent contact annotation, new family holdout or complete generalization claim.",OpenGates=new[]{"independent relationship labels","complete body-volume acceptance","continuous acquisition-to-standing finite ledge","held-out model/proportion/motion families","native generalization"}},new JsonSerializerOptions{WriteIndented=true}));
    }
    static float SkinDifference(Scene a,AnimationClip first,Scene b,AnimationClip second)
    {
        float maximum=0;for(double frame=0;frame<=3;frame+=.125)
        {
            var p=AnimationService.Evaluate(a,first,frame);var q=AnimationService.Evaluate(b,second,frame);
            for(int mesh=0;mesh<a.Meshes.Length;mesh++)for(int vertex=0;vertex<a.Meshes[mesh].Vertices.Length;vertex++)maximum=Math.Max(maximum,Vector3.Distance(AnimationService.Skin(a.Meshes[mesh].Vertices[vertex],p),AnimationService.Skin(b.Meshes[mesh].Vertices[vertex],q)));
        }
        return maximum;
    }
    static Fixture Make(FighterProject original,ControlRigSetup rig,bool permute,int seed)
    {
        var random=new Random(seed);int count=original.Scene.Bones.Length;var order=new List<int>();var remaining=Enumerable.Range(0,count).ToList();
        while(remaining.Count>0)
        {
            var available=remaining.Where(b=>original.Scene.Bones[b].Parent<0||order.Contains(original.Scene.Bones[b].Parent)).ToArray();int next=permute?available[random.Next(available.Length)]:available.Min();order.Add(next);remaining.Remove(next);
        }
        var oldToNew=new int[count];for(int i=0;i<count;i++)oldToNew[order[i]]=i;
        var names=Enumerable.Range(0,count).Select(i=>permute?"joint_"+random.NextInt64().ToString("x"):original.Scene.Bones[i].Name).ToArray();var aliases=original.Scene.Bones.Select((b,i)=>(b.Name,Alias:names[i])).ToDictionary(p=>p.Name,p=>p.Alias);
        var scene=original.Scene with{Name=permute?"anonymous_body_"+seed:original.Scene.Name,SkeletonRevision=Guid.NewGuid(),Bones=order.Select(i=>original.Scene.Bones[i] with{Name=names[i],Parent=original.Scene.Bones[i].Parent<0?-1:oldToNew[original.Scene.Bones[i].Parent]}).ToArray(),Meshes=original.Scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Joints=new(oldToNew[(int)v.Joints.X],oldToNew[(int)v.Joints.Y],oldToNew[(int)v.Joints.Z],oldToNew[(int)v.Joints.W])}).ToArray()}).ToArray()};
        var setup=rig with{Id=Guid.NewGuid(),SkeletonFingerprint=AnimationControls.Fingerprint(scene),Provenance=null,BoneRoles=rig.BoneRoles.Select(r=>r with{Bone=oldToNew[r.Bone]}).ToArray(),Chains=rig.Chains.Select(c=>c with{Root=oldToNew[c.Root],Middle=oldToNew[c.Middle],Effector=oldToNew[c.Effector]}).ToArray(),BodyBone=rig.BodyBone is {} body?oldToNew[body]:null,PreservedHelperBones=rig.PreservedHelperBones.Select(b=>oldToNew[b]).ToArray(),VirtualChains=rig.VirtualChains?.Select(c=>c with{Bones=c.Bones.Select(b=>oldToNew[b]).ToArray()}).ToArray(),JointLimits=rig.JointLimits?.Select(l=>l with{Bone=oldToNew[l.Bone]}).ToArray()};
        var clips=Motions.Select(name=>original.Clips.Single(c=>c.Source?.NativeName==name)).Select(c=>c with{Name=permute?"motion_"+random.NextInt64().ToString("x"):c.Name,Source=null,NativeTarget=null,SourceSnapshot=null,Retarget=null,Transition=null,Tracks=c.Tracks.Select(t=>t with{Bone=aliases[t.Bone]}).ToArray()}).ToArray();
        var project=new FighterProject(1,Guid.NewGuid(),"Derived skeletal metamorphic fixture",scene,clips){ControlRigs=[setup]};scene.Validate();return new(project,setup,oldToNew);
    }
}
