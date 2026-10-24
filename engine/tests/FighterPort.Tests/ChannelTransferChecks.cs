using System.Numerics;
using System.Text.Json;
using FighterPort.Core;

internal static class ChannelTransferChecks
{
    public static void Run(Action<bool,string> check)
    {
        var source=new Scene("Original channel fixture",
        [
            new("SourceOrigin",-1,Transform.Identity),
            new("SourceBranch",0,Transform.Identity with{Translation=new(0,1,0)}),
            new("SharedGeometryBranch",1,Transform.Identity with{Translation=Vector3.UnitX}),
            new("SourceOnlyBranch",1,Transform.Identity with{Translation=Vector3.UnitZ}),
            new("UnmappedIntermediate",2,Transform.Identity with{Translation=Vector3.UnitX}),
            new("SourceTip",4,Transform.Identity with{Translation=Vector3.UnitX})
        ],[]);
        var target=new Scene("Independent target channel fixture",
        [
            new("TargetOrigin",-1,Transform.Identity),
            new("TargetBranch",0,Transform.Identity with{Translation=new(0,2,0)}),
            new("SharedGeometryBranch",1,Transform.Identity with{Translation=new(1.5f,0,0)}),
            new("TargetOnlyBranch",1,Transform.Identity with{Translation=Vector3.UnitZ}),
            new("TargetTip",2,Transform.Identity with{Translation=new(3,0,0)})
        ],[]);
        ControlRigSetup Rig(Scene scene,int tip)=>new(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
        {
            Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Independently authored fixture units"),
            BoneRoles=[new("RootMotion",0,"Supplied root correspondence",true),new("Channel.Branch",1,"Supplied branch correspondence",true),
                new("Channel.Tip",tip,"Supplied endpoint correspondence",true),new("Unreviewed.Branch",3,"A suggestion, not an accepted binding",false)]
        };
        var a=Rig(source,5);var b=Rig(target,4);
        var mapping=RetargetBoneMapping.Build(source,a,target,b);
        check(mapping.Bones.Length==4&&mapping.SourceOnly.Any(p=>p.Bone==3)&&mapping.TargetOnly.Any(p=>p.Bone==3),
            "Mapping reports use reviewed descriptor roles on a supplied non-humanoid rig; an unreviewed suggestion does not become a correspondence");
        check(mapping.Bones.Single(p=>p.Source==1).Binding is {Supplied:true,Reviewed:true,Confidence:"Reviewed"}&&
            mapping.Bones.Single(p=>p.Source==2).Binding is {Supplied:false,Reviewed:false,Origin:"Inferred named branch"},
            "Supplied bindings and name/parent/geometry inference have distinct evidence; an uncalibrated inference has no fabricated probability");
        var curves=source.Bones[1].Rest.Channels().Select(v=>(Curve?)new Curve([new(0,v),new(1,v)])).ToArray();
        var clip=new AnimationClip(Guid.NewGuid(),"Original authored channel coverage",2,new(60),false,
        [
            new(source.Bones[1].Name,curves),
            new(source.Bones[2].Name,[null,null,null,null,null,new([new(0,5)]),null,null,null])
                {Rotation=new([new(0,Quaternion.Identity),new(1,Quaternion.CreateFromAxisAngle(Vector3.UnitX,.2f))])},
            new(source.Bones[3].Name,[null,null,null,null,null,null,new([new(0,.2f)]),null,null]),
            new(source.Bones[4].Name,[null,null,null,null,null,new([new(0,20)]),null,null,null])
        ]);
        var request=new RetargetRequest(ProceduralFixture.Create() with{Scene=source,Clips=[clip]},clip,a,
            ProceduralFixture.Create() with{Scene=target,Clips=[]},b,RootMotionPolicy.Preserve,false,default,null,null,null,null,null,null,null);
        var baseline=new BaselineMotionTransfer(request,RigDescriptor.Prepare(source,a),RigDescriptor.Prepare(target,b));
        check(mapping.Bones.Select(p=>(p.Source,p.Target,p.Role)).SequenceEqual(baseline.Mapping.Bones.Select(p=>(p.Source,p.Target,p.Role))),
            "Public mapping reports and the actual baseline consume the identical reviewed correspondence set");
        var ledger=RetargetChannelLedger.Build(source,clip,target,mapping,RootMotionPolicy.Preserve);
        check(ledger.SourceChannels.Length==13&&ledger.SourceChannels.Count(r=>r.TrackIndex==0)==9&&
            ledger.SourceChannels.Select(r=>(r.TrackIndex,r.Channel)).Distinct().Count()==13,
            "Every authored scalar and quaternion source channel gets exactly one ledger entry");
        check(ledger.SourceChannels.Single(r=>r.Channel=="Rotation.Quaternion").Targets.Single().Channels.SequenceEqual(["Rotation.X","Rotation.Y","Rotation.Z"])&&
            ledger.SourceChannels.Single(r=>r.TrackIndex==1&&r.Channel=="Rotation.Z").Status=="PreservedSourceOnly",
            "Quaternion-to-Euler reconstruction is explicit, including native scalar rotations superseded by the source evaluator");
        check(ledger.SourceChannels.Single(r=>r.TrackIndex==2) is {Status:"Unmapped",Targets.Length:0}&&
            ledger.SourceChannels.Single(r=>r.TrackIndex==3) is {Status:"AncestorInfluenceUnreviewed",Targets.Length:1} inherited&&inherited.Targets[0].Bone.BoneIndex==4,
            "Unmapped local translation and implicit ancestor rotation are distinguished; emitted descendant curves are not proof of reviewed chain distribution");
        var withoutHelper=clip with{Tracks=clip.Tracks.Take(3).ToArray()};
        var unchangedHelper=new BaselineMotionTransfer(request with{Source=request.Source with{Clips=[withoutHelper]},Clip=withoutHelper},RigDescriptor.Prepare(source,a),RigDescriptor.Prepare(target,b)).Evaluate(0);
        var withHelper=baseline.Evaluate(0);
        check(Math.Abs(PoseGeometry.RotationDifference(PoseGeometry.Rotation(withHelper[4].Matrix),PoseGeometry.Rotation(unchangedHelper[4].Matrix))-20)<.001f,
            "An independently authored 20-degree unmapped ancestor rotation actually propagates to the mapped descendant; the ledger does not label it fully discarded");
        check(ledger.TargetOnly.Single().Target.BoneIndex==3&&ledger.TargetOnly[0].Policy.Contains("rest",StringComparison.Ordinal),
            "Target-only native branches explicitly retain baseline rests instead of masquerading as transferred source tracks");
        check(ledger.SourceClipRevision==clip.Revision&&ledger.SourceSkeletonRevision==source.SkeletonRevision&&ledger.TargetSkeletonRevision==target.SkeletonRevision&&
            ledger.SourceChannels.All(r=>r.Source?.SkeletonRevision==source.SkeletonRevision&&r.Targets.All(t=>t.Bone.SkeletonRevision==target.SkeletonRevision)),
            "Source and destination native indices are scoped to independent immutable revisions");
        check(ledger.SourceChannels.All(r=>r.ApproximationError is null&&r.ErrorStatus=="NotMeasured"&&r.DestinationEncoding=="NotVerified"),
            "Mapping alone never supplies a measured approximation error or successful destination encoding receipt");
        var identityLedger=RetargetChannelLedger.Build(source,clip,source,RetargetBoneMapping.Build(source,a,source,a),RootMotionPolicy.Preserve,true);
        check(identityLedger.SourceChannels.Single(r=>r.TrackIndex==1&&r.Channel=="Rotation.Z") is {Status:"CopiedInactive",Targets.Length:1}&&
            identityLedger.SourceChannels.Single(r=>r.Channel=="Rotation.Quaternion").Targets[0].Channels.SequenceEqual(["Rotation.Quaternion"]),
            "Exact copies report inactive scalar curves as retained target data while quaternion evaluation remains authoritative");
        var encoded=JsonSerializer.Serialize(ledger,ProjectStore.Json);var restored=JsonSerializer.Deserialize<ChannelTransferLedger>(encoded,ProjectStore.Json)!;
        check(restored.Version==RetargetChannelLedger.Version&&restored.SourceChannels.Length==13&&restored.SourceChannels[0].Targets[0].Bone.NativeName=="TargetBranch"&&!encoded.Contains("ApproximationError",StringComparison.Ordinal),
            "Versioned channel evidence and scoped native identity survive serialization; absent measurements remain omitted");
        var legacy=JsonSerializer.Deserialize<RetargetBonePair>("{\"Role\":\"Channel.Previous\",\"Source\":1,\"Target\":2,\"Evidence\":\"Historical mapping\",\"DetailChannels\":true}",ProjectStore.Json)!;
        check(legacy.Binding is null&&legacy.Aliases is null&&!JsonSerializer.Serialize(legacy,ProjectStore.Json).Contains("Binding",StringComparison.Ordinal)&&
            !JsonSerializer.Serialize(legacy,ProjectStore.Json).Contains("Aliases",StringComparison.Ordinal),
            "Historical mapping records remain readable without inventing reviewed binding provenance or new alias records");
        var renameTarget=target with{Bones=target.Bones.Select((bone,i)=>bone with{Name="Opaque_"+i}).ToArray(),SkeletonRevision=Guid.NewGuid()};
        var renamedRig=b with{SkeletonFingerprint=AnimationControls.Fingerprint(renameTarget)};
        var nameFree=RetargetBoneMapping.Build(source,a,renameTarget,renamedRig);
        check(nameFree.SourceOnly.Any(p=>p.Bone==2&&p.Reason.Contains("skin influence",StringComparison.Ordinal))&&nameFree.Bones.Count(p=>p.Binding?.Supplied==true)==3,
            "Name-free unweighted/unequal-segmentation branches remain unresolved; supplied bindings are separate from structural discovery");
        bool sourceConflict=false,targetConflict=false;
        try{RetargetBoneMapping.Build(source,a with{BoneRoles=a.BoneRoles.Append(new("Channel.Conflict",1,"Conflicting supplied ownership",true)).ToArray()},
            target,b with{BoneRoles=b.BoneRoles.Append(new("Channel.Conflict",3,"Conflicting supplied ownership",true)).ToArray()});}
        catch(InvalidDataException e){sourceConflict=e.Message.Contains("Conflicting bone correspondence",StringComparison.Ordinal);}
        try{RetargetBoneMapping.Build(source,a with{BoneRoles=a.BoneRoles.Append(new("Channel.Conflict",3,"Conflicting supplied ownership",true)).ToArray()},
            target,b with{BoneRoles=b.BoneRoles.Append(new("Channel.Conflict",1,"Conflicting supplied ownership",true)).ToArray()});}
        catch(InvalidDataException e){targetConflict=e.Message.Contains("Conflicting bone correspondence",StringComparison.Ordinal);}
        check(sourceConflict&&targetConflict,"Conflicting supplied source or target ownership is rejected explicitly instead of selecting the first semantic role");
        var aliases=RetargetBoneMapping.Build(source,a with{BoneRoles=a.BoneRoles.Append(new("Channel.Alias",1,"Equivalent alias",true)).ToArray()},
            target,b with{BoneRoles=b.BoneRoles.Append(new("Channel.Alias",1,"Equivalent alias",true)).ToArray()});
        check(aliases.Bones.Length==mapping.Bones.Length&&aliases.Bones.Select(p=>p.Target).Distinct().Count()==aliases.Bones.Length,
            "Equivalent semantic aliases retain one channel owner; no direct/aggregate double application is introduced");
        AliasPolicies(check);
        AncestorPositionChannels(check);
        StructuralBranches(check);
    }
    static void AliasPolicies(Action<bool,string> check)
    {
        // Native body offsets differ (10 versus 20), but both torso segments
        // are exactly 10 units. An authored +20 body displacement therefore
        // ends at target Y=40, not the Y=60 from generic branch scaling.
        Scene Fixture(string prefix,float bodyHeight)=>new(prefix,
        [
            new(prefix+"origin",-1,Transform.Identity),
            new(prefix+"body",0,Transform.Identity with{Translation=new(0,bodyHeight,0)}),
            new(prefix+"upper",1,Transform.Identity with{Translation=new(0,10,0)})
        ],[]);
        ControlRigSetup Rig(Scene scene,bool aliasFirst)
        {
            RigMapping[] roles=
            [
                new("Channel.ActorAlias",0,scene.Name+" explicit actor alias",true),
                new("Channel.BodyAlias",1,scene.Name+" explicit body alias",true),
                new("ArbitraryBodyPurpose",1,scene.Name+" custom purpose retained",true),
                new("RootMotion",0,scene.Name+" reviewed actor policy",true),
                new("Hips",1,scene.Name+" reviewed body policy",true),
                new("Chest",2,scene.Name+" reviewed upper reference",true)
            ];
            return new(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
            {
                Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Independent fixture units"),
                BoneRoles=aliasFirst?roles:roles.Reverse().ToArray()
            };
        }
        var source=Fixture("authentic_",10);var target=Fixture("destination_",20);var prototype=ProceduralFixture.Create();
        var bodyClip=new AnimationClip(Guid.NewGuid(),"Authored body displacement",2,new(60),false,
            [new(source.Bones[1].Name,[null,null,null,null,null,null,null,new Curve([new(0,30)]),null])]);
        var rootClip=new AnimationClip(Guid.NewGuid(),"Authored actor displacement",2,new(60),false,
            [new(source.Bones[0].Name,[null,null,null,null,null,null,new Curve([new(0,20)]),new Curve([new(0,4)]),null])]);
        var reports=new List<string>();
        foreach(bool sourceAliasFirst in new[]{false,true})foreach(bool targetAliasFirst in new[]{false,true})
        {
            var a=Rig(source,sourceAliasFirst);var b=Rig(target,targetAliasFirst);
            BaselineMotionTransfer Baseline(AnimationClip clip,RootMotionPolicy policy)
            {
                var request=new RetargetRequest(prototype with{Scene=source,Clips=[clip]},clip,a,
                    prototype with{Scene=target,Clips=[]},b,policy,false,default,null,null,null,null,null,null,null);
                return new(request,RigDescriptor.Prepare(source,a),RigDescriptor.Prepare(target,b));
            }
            var transfer=Baseline(bodyClip,RootMotionPolicy.Preserve);var mapping=transfer.Mapping;reports.Add(JsonSerializer.Serialize(mapping,ProjectStore.Json));
            var body=mapping.Bones.Single(p=>p.Source==1);var actor=mapping.Bones.Single(p=>p.Source==0);
            check(Math.Abs(transfer.Evaluate(0)[1].Translation.Y-40)<.00001f&&body.Role=="Hips"&&!body.DetailChannels&&
                actor.Role=="RootMotion"&&!actor.DetailChannels&&mapping.Bones.Length==3,
                $"Reviewed body/actor policies survive independent alias ordering ({sourceAliasFirst}/{targetAliasFirst}); +20 body motion ends at target Y=40 with one owner");
            check(body.Aliases is {Length:3}&&body.Aliases.Select(p=>p.Role).SequenceEqual(["Hips","ArbitraryBodyPurpose","Channel.BodyAlias"])&&
                body.Aliases.All(p=>p.Binding is {Supplied:true,Reviewed:true}&&p.Binding.SourceEvidence.StartsWith("authentic_",StringComparison.Ordinal)&&
                    p.Binding.TargetEvidence.StartsWith("destination_",StringComparison.Ordinal))&&actor.Aliases is {Length:2},
                "Canonical root/body ownership retains every custom alias and its separate source/target binding evidence");
            // Rest extents are independently 20 and 30 units. Root travel is
            // consequently (30,6,0); InPlace preserves only the vertical 6,
            // while ActorControlled removes all authored actor displacement.
            var preserved=Baseline(rootClip,RootMotionPolicy.Preserve).Evaluate(0)[0].Translation;
            var inPlace=Baseline(rootClip,RootMotionPolicy.InPlace).Evaluate(0)[0].Translation;
            var actorControlled=Baseline(rootClip,RootMotionPolicy.ActorControlled).Evaluate(0)[0].Translation;
            check(Vector3.Distance(preserved,new(30,6,0))<.00001f&&Vector3.Distance(inPlace,new(0,6,0))<.00001f&&actorControlled.Length()<.00001f,
                "Root aliases cannot bypass Preserve, InPlace or ActorControlled policy under independent source/target binding order");
        }
        check(reports.Distinct(StringComparer.Ordinal).Count()==1,
            "Equivalent role permutations produce identical canonical mapping and complete alias evidence, not just equal pair counts");
        var restored=JsonSerializer.Deserialize<RetargetMappingReport>(reports[0],ProjectStore.Json)!;
        check(restored.Bones.Single(p=>p.Source==1).Aliases is {Length:3} aliases&&
            aliases.Single(a=>a.Role=="ArbitraryBodyPurpose").Binding?.SourceEvidence=="authentic_ custom purpose retained",
            "Saved mapping receipts retain arbitrary custom aliases and exact supplied evidence after round-trip");
        var derivedSource=Rig(source,true);var derivedTarget=Rig(target,false);
        derivedSource=derivedSource with{BodyBone=1,BoneRoles=derivedSource.BoneRoles.Where(r=>r.SemanticRole!="Hips").ToArray()};
        derivedTarget=derivedTarget with{BodyBone=1,BoneRoles=derivedTarget.BoneRoles.Where(r=>r.SemanticRole!="Hips").ToArray()};
        var merged=RetargetBoneMapping.Build(source,derivedSource,target,derivedTarget).Bones.Single(p=>p.Source==1);
        check(merged.Role=="Hips"&&merged.Binding is {Supplied:true,Reviewed:true}&&
            merged.Aliases!.Single(a=>a.Role=="Hips").Binding is {Supplied:false,Reviewed:false}&&
            merged.Aliases!.Single(a=>a.Role=="Channel.BodyAlias").Binding is {Supplied:true,Reviewed:true},
            "A supplied exact-pair alias establishes ownership without falsely upgrading the separately derived BodyBone role evidence");
    }
    static void AncestorPositionChannels(Action<bool,string> check)
    {
        // The independent rest positions are 0, 10, 20, 30. The target omits
        // the helper but retains body/chest heights 20 and 30. Torso and scene
        // extents are equal, so authored helper changes below the actor root
        // move the target body by exactly twenty units without target scaling.
        var source=new Scene("Original ancestor position fixture",
        [
            new("SourceActor",-1,Transform.Identity),
            new("SourceUnmapped",0,Transform.Identity with{Translation=new(0,10,0)}),
            new("SourceBody",1,Transform.Identity with{Translation=new(0,10,0)}),
            new("SourceChest",2,Transform.Identity with{Translation=new(0,10,0)})
        ],[]);
        var target=new Scene("Independent ancestor destination",
        [
            new("TargetActor",-1,Transform.Identity),
            new("TargetBody",0,Transform.Identity with{Translation=new(0,20,0)}),
            new("TargetChest",1,Transform.Identity with{Translation=new(0,10,0)})
        ],[]);
        ControlRigSetup Rig(Scene scene,int body,int chest)=>new(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
        {
            Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Independent fixture units"),
            BoneRoles=[new("RootMotion",0,"Reviewed actor",true),new("Hips",body,"Reviewed body",true),new("Chest",chest,"Reviewed chest",true)]
        };
        var a=Rig(source,2,3);var b=Rig(target,1,2);var mapping=RetargetBoneMapping.Build(source,a,target,b);
        var prototype=ProceduralFixture.Create();var donor=prototype with{Scene=source,Clips=[]};var destination=prototype with{Scene=target,Clips=[]};
        foreach(var (channel,value,label) in new[]{(7,30f,"Translation.Y"),(1,3f,"Scale.Y")})
        {
            var curves=new Curve?[9];curves[channel]=new([new(0,value)]);
            var clip=new AnimationClip(Guid.NewGuid(),"Unmapped ancestor channel",2,new(60),false,[new(source.Bones[1].Name,curves)]);
            var request=new RetargetRequest(donor with{Clips=[clip]},clip,a,destination,b,RootMotionPolicy.Preserve,false,default,null,null,null,null,null,null,null);
            var transferred=new BaselineMotionTransfer(request,RigDescriptor.Prepare(source,a),RigDescriptor.Prepare(target,b)).Evaluate(0);
            var sourcePose=AnimationService.Evaluate(source,clip,0);
            check(Math.Abs(sourcePose.World[2].Translation.Y-40)<.00001f&&Math.Abs(transferred[1].Translation.Y-40)<.00001f&&transferred[1].Scale==Vector3.One,
                "Independent "+label+" witness produces twenty units of target body displacement through an unmapped ancestor without changing target scale");
            var row=RetargetChannelLedger.Build(source,clip,target,mapping,RootMotionPolicy.Preserve).SourceChannels.Single();
            check(row is {Status:"AncestorPositionInfluenceUnreviewed",Targets.Length:1,ApproximationError:null,ErrorStatus:"NotMeasured",DestinationEncoding:"NotVerified"}&&
                row.Targets[0].Bone.BoneIndex==1&&row.Targets[0].Channels.SequenceEqual(["Translation.X","Translation.Y","Translation.Z"])&&row.Policy.Contains("Indirect",StringComparison.Ordinal),
                label+" ledger reports indirect body-position output, not a discarded channel or verified direct scale/translation preservation");
        }
        var rotationClip=new AnimationClip(Guid.NewGuid(),"Rotated ancestor",2,new(60),false,
            [new(source.Bones[1].Name,[null,null,null,null,null,new Curve([new(0,90)]),null,null,null])]);
        var rotationLedger=RetargetChannelLedger.Build(source,rotationClip,target,mapping,RootMotionPolicy.Preserve).SourceChannels.Single();
        check(rotationLedger.Status=="AncestorInfluenceUnreviewed"&&rotationLedger.Targets.Single(t=>t.Bone.BoneIndex==1).Channels.SequenceEqual(
            ["Rotation.X","Rotation.Y","Rotation.Z","Translation.X","Translation.Y","Translation.Z"])&&
            rotationLedger.Targets.Single(t=>t.Bone.BoneIndex==2).Channels.SequenceEqual(["Rotation.X","Rotation.Y","Rotation.Z"]),
            "An unmapped rotation ancestor reports both body translation and descendant orientation dependencies without duplicate target identities");
        var automaticSource=a with{BodyBone=2,BoneRoles=a.BoneRoles.Where(r=>r.SemanticRole!="Hips").ToArray()};
        var automaticTarget=b with{BodyBone=1,BoneRoles=b.BoneRoles.Where(r=>r.SemanticRole!="Hips").ToArray()};
        var derived=RetargetBoneMapping.Build(source,automaticSource,target,automaticTarget).Bones.Single(p=>p.Role=="Hips");
        check(derived.Binding is {Supplied:false,Reviewed:false,Confidence:"Uncalibrated"}&&derived.Binding.SourceEvidence.Contains("BodyBone",StringComparison.Ordinal),
            "An automatically populated BodyBone without reviewed role evidence is reported as derived, not an explicit reviewed user/profile mapping");
    }
    static void StructuralBranches(Action<bool,string> check)
    {
        Scene Fixture(string prefix,float scale)
        {
            var scene=new Scene(prefix,
            [
                new(prefix+"q",-1,Transform.Identity),
                new(prefix+"n",0,Transform.Identity with{Translation=new(0,scale,0)}),
                new(prefix+"v",1,Transform.Identity with{Translation=new(scale,0,0)}),
                new(prefix+"h",2,Transform.Identity with{Translation=new(.6f*scale,0,0)}),
                new(prefix+"z",1,Transform.Identity with{Translation=new(0,0,scale)})
            ],[]);
            return Geometry(scene);
        }
        Scene Geometry(Scene scene,int omit=-1,int displaced=-1)
        {
            // Independently constructed two branches: a two-joint +X chain and
            // a +Z leaf. Their names carry no shared tokens or semantic labels.
            var rest=AnimationService.Evaluate(scene,null,0);var vertices=new List<Vertex>();var indices=new List<uint>();
            for(int bone=2;bone<scene.Bones.Length;bone++)
            {
                if(bone==omit)continue;
                float length=Vector3.Distance(rest.World[bone].Translation,rest.World[scene.Bones[bone].Parent].Translation);
                foreach(var offset in new[]{new Vector3(.1f,.02f,.03f),new(.12f,.01f,.02f),new(.08f,.03f,.01f)})
                {
                    var position=rest.World[bone].Translation+(offset+(bone==displaced?new Vector3(2,0,0):Vector3.Zero))*length;
                    indices.Add((uint)vertices.Count);vertices.Add(new(position,Vector3.UnitY,Vector2.Zero,new(bone,0,0,0),new(1,0,0,0),Vector4.One));
                }
            }
            return scene with{Meshes=[new("Independently weighted geometry",vertices.ToArray(),indices.ToArray())]};
        }
        ControlRigSetup Rig(Scene scene)=>new(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
        {
            Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Original fixture coordinates"),
            BoneRoles=[new("RootMotion",0,"Reviewed root",true),new("Channel.Anchor",1,"Reviewed branch anchor",true)]
        };
        var source=Fixture("a",1);var target=Fixture("b",1.7f);var sourceRig=Rig(source);var targetRig=Rig(target);
        var forward=RetargetBoneMapping.Build(source,sourceRig,target,targetRig);var reverse=RetargetBoneMapping.Build(target,targetRig,source,sourceRig);
        check(forward.Bones.Length==5&&reverse.Bones.Length==5&&forward.Bones.All(p=>p.Source==p.Target)&&reverse.Bones.All(p=>p.Source==p.Target),
            "Name-free detailed topology and weighted geometry find the independently expected three branch correspondences in both directions");
        check(forward.Bones.Where(p=>p.Source>=2).All(p=>p.DetailChannels&&p.Binding is {Origin:"Inferred structural branch",Supplied:false,Reviewed:false,RequiresReview:true,Confidence:"Uncalibrated"}),
            "Unique geometric candidates remain explicit uncalibrated review results, not fabricated semantic confidence");
        int[] order=[0,1,4,2,3];var inverse=order.Select((old,index)=>(old,index)).ToDictionary(p=>p.old,p=>p.index);
        var permuted=target with
        {
            Bones=order.Select((old,index)=>target.Bones[old] with{Name="unrelated_"+index,Parent=target.Bones[old].Parent<0?-1:inverse[target.Bones[old].Parent]}).ToArray(),
            Meshes=target.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Joints=new(inverse[(int)v.Joints.X],0,0,0)}).ToArray()}).ToArray(),
            SkeletonRevision=Guid.NewGuid()
        };
        var permutedMap=RetargetBoneMapping.Build(source,sourceRig,permuted,Rig(permuted));
        var reversePermutedMap=RetargetBoneMapping.Build(permuted,Rig(permuted),source,sourceRig);
        check(permutedMap.Bones.Length==5&&permutedMap.Bones.All(p=>p.Target==inverse[p.Source])&&
            reversePermutedMap.Bones.Length==5&&reversePermutedMap.Bones.All(p=>p.Source==inverse[p.Target]),
            "Automatic detailed correspondence is unchanged by independent renaming and a valid sibling storage permutation in either direction");
        var duplicate=source with{Bones=source.Bones.Append(source.Bones[4] with{Name="extra"}).ToArray(),SkeletonRevision=Guid.NewGuid()};duplicate=Geometry(duplicate);
        var ambiguous=RetargetBoneMapping.Build(duplicate,Rig(duplicate),target,targetRig);
        var ambiguousReverse=RetargetBoneMapping.Build(target,targetRig,duplicate,Rig(duplicate));
        check(ambiguous.SourceOnly.Count(p=>p.Bone is 4 or 5&&p.Reason.Contains("Ambiguous",StringComparison.Ordinal))==2&&
            ambiguous.TargetOnly.Any(p=>p.Bone==4&&p.Reason.Contains("Ambiguous",StringComparison.Ordinal))&&
            ambiguousReverse.SourceOnly.Any(p=>p.Bone==4&&p.Reason.Contains("Ambiguous",StringComparison.Ordinal))&&
            ambiguous.Bones.All(p=>p.Source<4)&&ambiguousReverse.Bones.All(p=>p.Target<4),
            "Symmetric weighted branches remain ambiguous in both directions; mutual ownership never chooses a name, index or first candidate");
        var unweighted=Geometry(target,omit:4);var noWeights=RetargetBoneMapping.Build(source,sourceRig,unweighted,Rig(unweighted));
        check(noWeights.SourceOnly.Any(p=>p.Bone==4)&&noWeights.TargetOnly.Any(p=>p.Bone==4&&p.Reason.Contains("skin influence",StringComparison.Ordinal)),
            "Matching topology and rest direction cannot infer an unweighted selector/socket correspondence");
        var opposite=target with{Bones=target.Bones.Select((b,i)=>i==4?b with{Rest=b.Rest with{Translation=-b.Rest.Translation}}:b).ToArray(),SkeletonRevision=Guid.NewGuid()};opposite=Geometry(opposite);
        var wrongDirection=RetargetBoneMapping.Build(source,sourceRig,opposite,Rig(opposite));
        var displaced=Geometry(target,displaced:4);var wrongSkin=RetargetBoneMapping.Build(source,sourceRig,displaced,Rig(displaced));
        check(wrongDirection.SourceOnly.Any(p=>p.Bone==4)&&wrongSkin.SourceOnly.Any(p=>p.Bone==4),
            "Opposite rest geometry and a distant skin-influence centroid independently reject a superficially equal native branch topology");
        var helpers=RetargetBoneMapping.Build(source,sourceRig,target,targetRig with{PreservedHelperBones=[2]});
        check(helpers.SourceOnly.Any(p=>p.Bone==2)&&helpers.TargetOnly.Any(p=>p.Bone==2&&p.Reason.Contains("helper",StringComparison.Ordinal)),
            "Unreviewed derived helper branches are excluded from automatic one-to-one channel ownership");
        var topology=target with{Bones=target.Bones.Append(new Bone("other",4,Transform.Identity with{Translation=new(0,0,.3f)})).ToArray(),SkeletonRevision=Guid.NewGuid()};topology=Geometry(topology);
        var unequal=RetargetBoneMapping.Build(source,sourceRig,topology,Rig(topology));
        check(unequal.SourceOnly.Any(p=>p.Bone==4),"Unequal native chain segmentation remains explicit instead of silently collapsing or duplicating rotation");
        var unanchored=RetargetBoneMapping.Build(source,sourceRig with{BoneRoles=[]},target,targetRig with{BoneRoles=[]});
        check(unanchored.Bones.Length==1&&unanchored.Bones[0].Source==0,
            "Detailed branch inference does not claim automatic body-rig discovery without a reviewed semantic parent anchor");
        var acceptedSource=sourceRig with{BoneRoles=sourceRig.BoneRoles.Concat(Enumerable.Range(2,3).Select(i=>new RigMapping("Channel.Reviewed."+i,i,"Reviewed candidate on exact fixture rig",true))).ToArray()};
        var acceptedTarget=targetRig with{BoneRoles=targetRig.BoneRoles.Concat(Enumerable.Range(2,3).Select(i=>new RigMapping("Channel.Reviewed."+i,i,"Reviewed candidate on exact fixture rig",true))).ToArray()};
        var accepted=RetargetBoneMapping.Build(source,acceptedSource,target,acceptedTarget);
        check(accepted.Bones.Length==5&&accepted.Bones.All(p=>p.Binding is {Supplied:true,Reviewed:true,RequiresReview:false}),
            "Accepted candidates reuse the existing exact-rig reviewed-role storage instead of a new hidden mapping cache");
        var serialized=JsonSerializer.Serialize(forward,ProjectStore.Json);var restored=JsonSerializer.Deserialize<RetargetMappingReport>(serialized,ProjectStore.Json)!;
        check(restored.Bones.Count(p=>p.Binding?.RequiresReview==true)==3&&!JsonSerializer.Serialize(accepted,ProjectStore.Json).Contains("RequiresReview",StringComparison.Ordinal),
            "Structural review requirements survive saved mapping receipts while false defaults preserve historical proof serialization");
        var clip=new AnimationClip(Guid.NewGuid(),"Independent branch channel",2,new(60),false,
            [new(source.Bones[2].Name,[null,null,null,null,null,new Curve([new(0,0),new(1,10)]),null,null,null])]);
        var prototype=ProceduralFixture.Create();
        var output=RetargetPipeline.Bake(new(prototype with{Scene=source,Clips=[clip]},clip,sourceRig,
            prototype with{Scene=target,Clips=[]},targetRig,RootMotionPolicy.Preserve,false,default,null,null,null,null,null,null,null));
        var contactPass=new ContactValidation(RetargetAcceptance.Version,true,true,.0001f,.0001f,0,0,0,0,0,0,1,1,1);
        check(output.Diagnostics.Any(d=>d.Code=="RIG_BINDING_REVIEW"&&!d.Error)&&
            !RetargetAcceptance.Ready(contactPass,output.Diagnostics.Where(d=>d.Code=="RIG_BINDING_REVIEW").ToArray(),[],true),
            "The actual bake marks inferred branch ownership for review; passing contacts or accepting channel losses cannot bypass Apply's rig gate");
    }
}
