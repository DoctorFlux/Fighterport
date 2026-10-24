using System.Numerics;
using System.Text.Json;
using FighterPort.Core;

// Original unequal-proportion geometry. Expected support comes from authored
// Cartesian poses, independently of any detector, objective or fitted result.
internal static class TemporalActionFittingChecks
{
    sealed record Fixture(FighterProject Project,ControlRigSetup Rig,float Torso,float Arm,float Units,float Axis);
    sealed record Fit(Fixture Source,Fixture Target,RetargetBake Bake,RetargetBake Raw);
    static readonly FittingSettings On=new(){AutomaticFitting=true,AutomaticContacts=false,VolumePolicy=BodyVolumePolicy.ReportOverlap};
    static readonly GroundContactSettings Floor=new(){ExplicitSurface=true,SourceFloor=0,TargetFloor=0};
    static readonly JsonSerializerOptions ReportJson=new(ProjectStore.Json){IgnoreReadOnlyProperties=false};

    public static async Task RunAsync(string directory,Action<bool,string> check)
    {
        Directory.CreateDirectory(directory);
        var limited=NativeMotionTrajectory.PlanBudget(142,60,283,true);
        // Independent expansion: fixed787416960 +26021760 per inner step.
        // 46=1984417920;47=2010439680, beyond the existing two-billion cap.
        check(limited.Limit==46&&limited.Work==1984417920&&limited.Work<=NativeMotionTrajectory.MaximumLinearWork&&
            787416960L+26021760L*(limited.Limit+1)>NativeMotionTrajectory.MaximumLinearWork,
            "Generic long periodic trajectories retain every native variable and choose the largest inner-step limit under the unchanged independently expanded work bound");
        check(NativeMotionTrajectory.PlanBudget(5,60,9).Limit==NativeMotionTrajectory.MaximumLinearIterations,
            "Small prepared trajectories retain the existing64step maximum rather than losing iterations globally");
        var a=Motion(Make("native-alpha",1,1),[-10,-20,-10]);
        var b=Motion(Make("native-beta",2,.6f),[-30,-35,-30]);
        string originals=RetargetDependencies.Hash(new{A=a,B=b});
        var authored=new[]{(a,new[]{-10f,-20,-10}),(b,new[]{-30f,-35,-30})};
        check(authored.All(f=>f.Item2.Select((angle,i)=>GroundError(f.Item1,Pose(f.Item1,Reference(f.Item1,angle)))).All(v=>v<.00002f)),
            "Independent native rotations demonstrate that all three actual palm/sole triangles can occupy the floor across unequal arm/torso proportions without changing any rest length");
        check(authored.All(f=>new[]{0d,2,4}.All(t=>GroundError(f.Item1,AnimationService.Evaluate(f.Item1.Project.Scene,f.Item1.Project.Clips[0],t))<.00002f)),
            "Authentic source integer landmarks retain independently authored palm-plus-two-sole geometry; no detected contact label supplies the expected height");
        var forward=Run(a,b);var reverse=Run(b,a);var fits=new[]{forward,reverse};
        var times=Enumerable.Range(0,65).Select(i=>i/16d).ToArray();
        var measurements=fits.Select(f=>new{Source=f.Source.Project.Name,Raw=times.Max(t=>GroundError(f.Target,Sample(f.Target,f.Raw.Clip,t))),Fitted=times.Max(t=>GroundError(f.Target,Sample(f.Target,f.Bake.Clip,t))),
            SourceMaximum=times.Max(t=>GroundError(f.Source,Sample(f.Source,f.Source.Project.Clips[0],t))),MaximumActionDegrees=times.Max(t=>ActionAngle(f,t)),
            MaximumCorrectionStep=CorrectionStep(f,times)}).ToArray();
        await File.WriteAllTextAsync(Path.Combine(directory,"independent-trajectory-measurements.json"),JsonSerializer.Serialize(new{Pipeline=RetargetPipeline.Version,Claim="Soft fitting/action evidence; no contact, native or display pass",Measurements=measurements},ReportJson));
        foreach(var m in measurements)
            check(m.Raw>.05f&&m.Fitted<m.Raw*.5f,
                $"Coupled {m.Source} transfer reduces independently measured maximum actual palm/sole floor error by at least half across the action; raw={m.Raw:G9}, fitted={m.Fitted:G9}; frozen contact acceptance remains separate");
        check(measurements.All(m=>m.MaximumActionDegrees<=45.0001f),
            "Source-derived body/interior branch directions remain within the existing 45-degree action review bound at all independent quarter/subframe probes");
        check(fits.All(f=>times.All(t=>FreeHeight(f.Source,Sample(f.Source,f.Source.Project.Clips[0],t))>.2f&&FreeHeight(f.Target,Sample(f.Target,f.Bake.Clip,t))>.1f)),
            "The actual mixed-weight free branch stays above the floor throughout the action rather than being mistaken for supporting geometry or traded into penetration");
        check(fits.All(f=>times.All(t=>
        {
            var raw=Sample(f.Target,f.Raw.Clip,t);var actual=Sample(f.Target,f.Bake.Clip,t);
            return raw.Local[0]==actual.Local[0]&&actual.Local.Select((v,i)=>v.Scale==raw.Local[i].Scale&&(i==f.Target.Rig.BodyBone||v.Translation==raw.Local[i].Translation)).All(v=>v);
        })),"Temporal geometry changes only permitted existing native rotations/body translation, retaining actor channels and all limb translation/scale channels at every probe");
        check(fits.All(f=>times.All(t=>Math.Abs(Helper(f.Bake.Clip,f.Target,t)-180*t)<.06f)),
            "A separate mapped native helper retains its authored two full turns; correction regularization does not smooth the absolute source motion or erase winding");
        var again=Run(a,b);
        check(Keys(again.Bake.Clip)==Keys(forward.Bake.Clip)&&times.Reverse().All(t=>SkinDifference(b,Sample(b,forward.Bake.Clip,t),b,Sample(b,again.Bake.Clip,t))==0),
            "Repeated preparation and reversed random-access evaluation produce identical native keys and skinned poses without history-dependent seeds");
        var fast=a with{Project=a.Project with{Clips=[a.Project.Clips[0] with{SampleCount=10,Rate=new(60),Tracks=a.Project.Clips[0].Tracks.Select(t=>t with{Channels=t.Channels.Select(c=>c is null?null:c with{Keys=c.Keys.Select(k=>k with{Frame=k.Frame*2,InTangent=k.InTangent/2,OutTangent=k.OutTangent/2}).ToArray()}).ToArray()}).ToArray()}]}};
        var rateEquivalent=Run(fast,b);
        check(times.All(t=>SkinDifference(b,Sample(b,forward.Bake.Clip,t),b,Sample(b,rateEquivalent.Bake.Clip,t))<.00008f)&&rateEquivalent.Bake.Clip.SampleCount==forward.Bake.Clip.SampleCount,
            "Equivalent source motion expressed at 30 or 60 rational-rate frames produces the same physical target trajectory and duration; temporal penalties use real seconds");
        var renamed=Permute(b);var renameFit=Run(a,renamed);
        var normalDescriptor=RigDescriptor.Prepare(b.Project.Scene,b.Rig);var renamedDescriptor=RigDescriptor.Prepare(renamed.Project.Scene,renamed.Rig);
        var renameWitnesses=times.Select(t=>new{Frame=t,RawSkin=SkinDifference(b,Sample(b,forward.Raw.Clip,t),renamed,Sample(renamed,renameFit.Raw.Clip,t)),
            FittedSkin=SkinDifference(b,Sample(b,forward.Bake.Clip,t),renamed,Sample(renamed,renameFit.Bake.Clip,t)),
            Normal=Sample(b,forward.Bake.Clip,t).Local.Select(v=>v.Channels()).ToArray(),
            RenamedMapped=b.Rig.BoneRoles.Select(r=>Sample(renamed,renameFit.Bake.Clip,t).Local[renamed.Rig.BoneRoles.Single(m=>m.SemanticRole==r.SemanticRole).Bone].Channels()).ToArray()}).ToArray();
        await File.WriteAllTextAsync(Path.Combine(directory,"rename-trajectory-comparison.json"),JsonSerializer.Serialize(new{Pipeline=RetargetPipeline.Version,
            Normal=forward.Bake.Clip.Retarget?.Pipeline?.BodyTrajectory,Renamed=renameFit.Bake.Clip.Retarget?.Pipeline?.BodyTrajectory,
            NormalScene=forward.Bake.Clip.Retarget?.Pipeline?.SceneBody,RenamedScene=renameFit.Bake.Clip.Retarget?.Pipeline?.SceneBody,
            NormalOrder=b.Rig.BoneRoles.OrderBy(r=>normalDescriptor.CanonicalBoneRanks[r.Bone]).ToArray(),RenamedOrder=renamed.Rig.BoneRoles.OrderBy(r=>renamedDescriptor.CanonicalBoneRanks[r.Bone]).ToArray(),
            Witnesses=renameWitnesses},ReportJson));
        check(renameWitnesses.All(w=>w.FittedSkin<.00008f),
            $"Supplied reviewed bindings survive unrelated native names and a valid branch-storage permutation without selecting a different temporal result; raw={renameWitnesses.Max(w=>w.RawSkin):G9}, fitted={renameWitnesses.Max(w=>w.FittedSkin):G9}");
        var metric=Motion(Make("native-centimetres",1,1,100,90),[-10,-20,-10]);var turned=Motion(Make("native-turned",2,.6f,1,-90),[-30,-35,-30]);var physical=Run(metric,turned);
        var axisWitnesses=times.Select(t=>new{Frame=t,SourceSkin=SkinDifference(a,Sample(a,a.Project.Clips[0],t),metric,Sample(metric,metric.Project.Clips[0],t)),
            RawSkin=SkinDifference(b,Sample(b,forward.Raw.Clip,t),turned,Sample(turned,physical.Raw.Clip,t)),FittedSkin=SkinDifference(b,Sample(b,forward.Bake.Clip,t),turned,Sample(turned,physical.Bake.Clip,t)),
            Normal=Sample(b,forward.Bake.Clip,t).Local.Select(v=>v.Channels()).ToArray(),Converted=Sample(turned,physical.Bake.Clip,t).Local.Select(v=>v.Channels()).ToArray()}).ToArray();
        await File.WriteAllTextAsync(Path.Combine(directory,"axis-unit-trajectory-comparison.json"),JsonSerializer.Serialize(new{Pipeline=RetargetPipeline.Version,
            Normal=forward.Bake.Clip.Retarget?.Pipeline?.BodyTrajectory,Converted=physical.Bake.Clip.Retarget?.Pipeline?.BodyTrajectory,
            NormalKeys=forward.Bake.Clip.Tracks,ConvertedKeys=physical.Bake.Clip.Tracks,Witnesses=axisWitnesses},ReportJson));
        check(axisWitnesses.All(w=>w.FittedSkin<.00008f),
            $"Declared unit conversion and independent native-axis rotation retain equivalent actual skin trajectories under the same temporal objective; source={axisWitnesses.Max(w=>w.SourceSkin):G9}, raw={axisWitnesses.Max(w=>w.RawSkin):G9}, fitted={axisWitnesses.Max(w=>w.FittedSkin):G9}");
        var held=Motion(Make("native-held",1,1),[-10,-20,-10],Interpolation.Step);var heldFit=Run(held,b);
        check(heldFit.Bake.Clip.Tracks.SelectMany(t=>t.Channels.OfType<Curve>()).All(c=>c.Interpolation==Interpolation.Step)&&
            SkinDifference(b,Sample(b,heldFit.Bake.Clip,.25),b,Sample(b,heldFit.Bake.Clip,1.9999))<.00002f&&
            SkinDifference(b,Sample(b,heldFit.Bake.Clip,2),b,Sample(b,heldFit.Bake.Clip,3.75))<.00002f,
            "An authored held boundary remains a discontinuous source hold, with no smoothing or temporal blending across its exact native time");
        var document=new EditorDocument(b.Project);
        using(var batch=new RetargetBatch(document,a.Project,a.Rig,b.Rig,[a.Project.Clips[0].Id],shared:On,ground:Floor,contacts:false))
        {
            await batch.RunAsync();check(batch.Rows.Single().Bake is {} candidate&&Keys(candidate.Clip)==Keys(forward.Bake.Clip)&&batch.Rows.Single().State==RetargetRowState.Review,
                "The same-source batch and single-clip route share identical prepared temporal keys and retain the unresolved review result");
            bool blocked=false;try{batch.Apply();}catch(InvalidOperationException){blocked=true;}
            check(blocked&&!document.CanUndo&&ReferenceEquals(document.Project,b.Project),"Unvalidated temporal/action drafts cannot silently enter target history or become Apply-ready");
        }
        string save=Path.Combine(directory,"temporal-action-review.fighterport");await ProjectStore.SaveAsync(RetargetService.Apply(b.Project,forward.Bake),save);
        var restored=await ProjectStore.LoadAsync(save);var restoredClip=restored.Clips.Single(c=>c.Id==forward.Bake.Clip.Id);
        check(Keys(restoredClip)==Keys(forward.Bake.Clip)&&restoredClip.Retarget?.IsDraft==true&&restored.LibrarySources.Single(s=>s.Id==restoredClip.SourceSnapshot).Clip.Revision==a.Project.Clips[0].Revision&&AnimationControls.DeformChecksum(restored.Scene)==AnimationControls.DeformChecksum(b.Project.Scene),
            "Actual project save/reopen preserves prepared editable temporal keys, authentic source ancestry, unchanged native skin/rig and unresolved draft ownership");
        check(fits.All(f=>f.Bake.Contacts.Length==0&&f.Bake.Clip.Retarget?.IsDraft==true)&&originals==RetargetDependencies.Hash(new{A=a,B=b}),
            "The trajectory increment does not activate contacts or mutate authentic source channels, model identities, native hierarchy/rests, weights or target inputs");
    }

    static Fit Run(Fixture source,Fixture target)
    {
        RetargetBake Bake(FittingSettings settings)=>RetargetService.Bake(source.Project,source.Project.Clips[0],source.Rig,target.Project,target.Rig,useContacts:false,targetRate:new(30),ground:Floor,fitting:settings).Value??throw new InvalidOperationException("Independent temporal fixture did not produce a review draft.");
        return new(source,target,Bake(On),Bake(On with{AutomaticFitting=false}));
    }
    static Fixture Make(string name,float torso,float arm,float units=1,float axis=0)
    {
        Transform T(float x,float y,float z=0)=>Transform.Identity with{Translation=new Vector3(x,y,z)*units};
        var bones=new[]{new Bone(name+"_0",-1,Transform.Identity with{Rotation=new(0,0,axis)}),new(name+"_1",0,T(0,2)),new(name+"_2",1,T(torso,0)),
            new(name+"_3",2,T(0,0)),new(name+"_4",3,T(.75f*arm,-arm)),new(name+"_5",4,T(-.75f*arm,-arm)),
            new(name+"_6",1,T(0,0)),new(name+"_7",6,T(.5f,-1)),new(name+"_8",7,T(0,-1)),
            new(name+"_9",1,T(0,0)),new(name+"_10",9,T(-.5f,-1)),new(name+"_11",10,T(0,-1)),
            new(name+"_12",2,T(0,0)),new(name+"_13",12,T(.75f*arm,arm)),new(name+"_14",13,T(.75f*arm,arm)),new(name+"_15",0,T(0,3,2))};
        var empty=new Scene(name,bones,[]);var rest=AnimationService.Evaluate(empty,null,0);var turn=Matrix4x4.CreateRotationZ(axis*MathF.PI/180);
        Mesh Patch(string label,int bone,int other=-1)=>new(label,new[]{new Vector3(-.08f,0,-.08f),new(.08f,0,-.08f),new(0,0,.08f)}.Select(p=>new Vertex(rest.World[bone].Translation+Vector3.TransformNormal(p*units,turn),Vector3.TransformNormal(Vector3.UnitY,turn),Vector2.Zero,new(bone,other<0?0:other,0,0),other<0?new(1,0,0,0):new(.8f,.2f,0,0),Vector4.One)).ToArray(),[0,1,2]);
        var scene=empty with{Meshes=[Patch("Authored palm triangle",5),Patch("Authored sole alpha",8),Patch("Authored sole beta",11),Patch("Free weighted triangle",14,13)]};
        string[] roles=["RootMotion","Hips","Chest","Low.Root","Low.Middle","Low.End","SupportA.Root","SupportA.Middle","SupportA.End","SupportB.Root","SupportB.Middle","SupportB.End","Free.Root","Free.Middle","Free.End","Independent.Unweighted.Helper"];
        var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[]){BodyBone=1,Axes=new(Vector3.TransformNormal(Vector3.UnitX,turn),Vector3.TransformNormal(Vector3.UnitY,turn),Vector3.UnitZ),Coordinates=new(units,"Original independent unit/frame data"),BoneRoles=roles.Select((role,i)=>new RigMapping(role,i,"Supplied independent correspondence",true)).ToArray(),
            VirtualChains=[new("branch.low",[3,4,5],"Complete reviewed path",true),new("branch.sole.alpha",[6,7,8],"Complete reviewed path",true),new("branch.sole.beta",[9,10,11],"Complete reviewed path",true),new("branch.free",[12,13,14],"Complete reviewed path",true)]};
        var clip=new AnimationClip(Guid.NewGuid(),"Unlabelled authentic action",5,new(30),false,[]);
        return new(new(1,Guid.NewGuid(),name,scene,[clip]){ControlRigs=[rig]},rig,torso,arm,units,axis);
    }
    static Transform[] Reference(Fixture f,float angle)
    {
        var local=f.Project.Scene.Bones.Select(b=>b.Rest).ToArray();local[0]=local[0] with{Rotation=new(0,0,f.Axis+720)};
        float height=2+f.Torso*MathF.Sin(angle*MathF.PI/180),beta=-MathF.Acos(height/(2*f.Arm))*180/MathF.PI;
        foreach(var (bone,rotation) in new[]{(1,angle),(3,beta-angle),(5,-beta),(6,-angle),(9,-angle)})local[bone]=local[bone] with{Rotation=new(0,0,rotation)};
        return local;
    }
    static Fixture Motion(Fixture f,float[] angles,Interpolation interpolation=Interpolation.Linear)
    {
        var poses=angles.Select(a=>Reference(f,a)).ToArray();var tracks=Enumerable.Range(0,f.Project.Scene.Bones.Length).Select(b=>new BoneTrack(f.Project.Scene.Bones[b].Name,Enumerable.Range(0,9).Select(c=>c<3||c>5?null:new Curve(poses.Select((p,i)=>new Key(i*2,b==15&&c==5?i*360:p[b].Channels()[c])).ToArray(),interpolation)).ToArray())).ToArray();
        return f with{Project=f.Project with{Clips=[f.Project.Clips[0] with{Tracks=tracks}]}};
    }
    static Fixture Permute(Fixture f)
    {
        int[] order=[0,15,1,2,9,10,11,12,13,14,6,7,8,3,4,5];var inverse=order.Select((old,index)=>(old,index)).ToDictionary(p=>p.old,p=>p.index);var old=f.Project.Scene;
        var scene=old with{SkeletonRevision=Guid.NewGuid(),Bones=order.Select((b,i)=>old.Bones[b] with{Name="unrelated_"+i,Parent=old.Bones[b].Parent<0?-1:inverse[old.Bones[b].Parent]}).ToArray(),Meshes=old.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Joints=new(inverse[(int)v.Joints.X],inverse[(int)v.Joints.Y],inverse[(int)v.Joints.Z],inverse[(int)v.Joints.W])}).ToArray()}).ToArray()};
        var names=order.Select((b,i)=>(old.Bones[b].Name,Renamed:scene.Bones[i].Name)).ToDictionary(p=>p.Name,p=>p.Renamed);
        var rig=f.Rig with{Id=Guid.NewGuid(),BodyBone=inverse[f.Rig.BodyBone!.Value],SkeletonFingerprint=AnimationControls.Fingerprint(scene),BoneRoles=f.Rig.BoneRoles.Select(r=>r with{Bone=inverse[r.Bone]}).ToArray(),VirtualChains=f.Rig.VirtualChains!.Select(c=>c with{Bones=c.Bones.Select(b=>inverse[b]).ToArray()}).ToArray()};
        return f with{Project=f.Project with{Scene=scene,Clips=f.Project.Clips.Select(c=>c with{Tracks=c.Tracks.Select(t=>t with{Bone=names[t.Bone]}).ToArray()}).ToArray(),ControlRigs=[rig]},Rig=rig};
    }
    static EvaluatedPose Pose(Fixture f,Transform[] local)=>AnimationService.Evaluate(f.Project.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
    static EvaluatedPose Sample(Fixture f,AnimationClip clip,double time)=>AnimationService.Evaluate(f.Project.Scene,clip,time);
    static Vector3 Canonical(Fixture f,Vector3 p)=>Vector3.TransformNormal(p,Matrix4x4.CreateRotationZ(-f.Axis*MathF.PI/180))/f.Units;
    static float GroundError(Fixture f,EvaluatedPose p)=>f.Project.Scene.Meshes.Take(3).SelectMany(m=>m.Vertices).Max(v=>Math.Abs(Canonical(f,AnimationService.Skin(v,p)).Y));
    static float FreeHeight(Fixture f,EvaluatedPose p)=>f.Project.Scene.Meshes[3].Vertices.Min(v=>Canonical(f,AnimationService.Skin(v,p)).Y);
    static float SkinDifference(Fixture a,EvaluatedPose pa,Fixture b,EvaluatedPose pb)=>a.Project.Scene.Meshes.Zip(b.Project.Scene.Meshes).SelectMany(m=>m.First.Vertices.Zip(m.Second.Vertices).Select(v=>Vector3.Distance(Canonical(a,AnimationService.Skin(v.First,pa)),Canonical(b,AnimationService.Skin(v.Second,pb))))).Max();
    static float ActionAngle(Fit f,double t)
    {
        var original=Sample(f.Source,f.Source.Project.Clips[0],t);var actual=Sample(f.Target,f.Bake.Clip,t);
        return new[]{(1,2),(3,4),(4,5),(6,7),(7,8),(9,10),(10,11)}.Max(pair=>
        {
            var a=Vector3.Normalize(Canonical(f.Source,original.World[pair.Item2].Translation-original.World[pair.Item1].Translation));var b=Vector3.Normalize(Canonical(f.Target,actual.World[pair.Item2].Translation-actual.World[pair.Item1].Translation));
            return MathF.Acos(Math.Clamp(Vector3.Dot(a,b),-1,1))*180/MathF.PI;
        });
    }
    static float CorrectionStep(Fit f,double[] times)
    {
        var values=times.Select(t=>{var a=Sample(f.Target,f.Raw.Clip,t);var b=Sample(f.Target,f.Bake.Clip,t);return f.Target.Project.Scene.Meshes.SelectMany(m=>m.Vertices).Select(v=>Canonical(f.Target,AnimationService.Skin(v,b)-AnimationService.Skin(v,a))).ToArray();}).ToArray();
        return values.Zip(values.Skip(1)).Max(p=>p.First.Zip(p.Second).Max(v=>Vector3.Distance(v.First,v.Second)));
    }
    static float Helper(AnimationClip clip,Fixture f,double time)=>clip.Tracks.Single(t=>t.Bone==f.Project.Scene.Bones[f.Rig.BoneRoles.Single(r=>r.SemanticRole=="Independent.Unweighted.Helper").Bone].Name).Channels[5]!.Sample(time);
    static string Keys(AnimationClip clip)=>RetargetDependencies.Hash(clip.Tracks);
}
