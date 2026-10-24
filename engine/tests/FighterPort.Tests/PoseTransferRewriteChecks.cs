using System.Numerics;
using FighterPort.Core;

// These expectations are authored Cartesian coordinates and independent
// quarter-turn arithmetic, not the transfer's own transform formula.
internal static class PoseTransferRewriteChecks
{
    public static void Run(Action<bool,string> check)
    {
        var source=Fixture("original",10,10);var target=Fixture("destination",20,10);
        var clip=Motion(source,90,new(0,10,0));
        var transferred=Transfer(source,target,clip);var pose=Pose(target,transferred);
        check(Vector3.Distance(pose.World[1].Translation,new(-20,0,0))<.00005f&&
              Vector3.Distance(transferred[1].Translation,new(0,20,0))<.00005f,
            "An actor quarter-turn rotates the target's own twenty-unit body radius rigidly; source and target torso lengths remain ten");

        var moving=Motion(source,90,new(3,12,0),new(4,0,0));
        var movingPose=Pose(target,Transfer(source,target,moving));
        // Rest extents20/30 set the existing actor-travel ratio to1.5.
        // Authored body delta(3,2) is unchanged because both torsos are10.
        // Body(3,22) rotated90 gives(-22,3), then actor travel adds(6,0).
        check(Vector3.Distance(movingPose.World[0].Translation,new(6,0,0))<.00005f&&
              Vector3.Distance(movingPose.World[1].Translation,new(-16,3,0))<.00005f,
            "Actor travel and body-local motion remain independent while the actor turns; target body offset is not scaled as world motion");
        var inPlace=Pose(target,Transfer(source,target,moving,RootMotionPolicy.InPlace));
        check(Vector3.Distance(inPlace.World[0].Translation,Vector3.Zero)<.00005f&&
              Vector3.Distance(inPlace.World[1].Translation,new(-22,3,0))<.00005f,
            "In-place policy removes actor travel without removing its orientation or the authored actor-relative body motion");

        var sourceActorRest=Fixture("first-rest",10,10,90);var targetActorRest=Fixture("second-rest",20,10,-90);
        // Both rest bodies are upright in model coordinates. Their native
        // actor axes/rests differ; a+90 actor turn must move target body to-X.
        var actorRestMotion=Motion(sourceActorRest,180,new(10,0,0));
        var differentRest=Pose(targetActorRest,Transfer(sourceActorRest,targetActorRest,actorRestMotion));
        check(Vector3.Distance(differentRest.World[1].Translation,new(-20,0,0))<.00008f,
            "Different native actor rest frames transport the same quarter-turn onto the unchanged upright target rest geometry");

        var rested=Transfer(source,target,Motion(source,0,new(0,10,0)));
        check(rested.Zip(target.Scene.Bones).All(p=>Vector3.Distance(p.First.Translation,p.Second.Rest.Translation)<.00001f&&
              Vector3.Distance(p.First.Scale,p.Second.Rest.Scale)<.00001f&&
              PoseGeometry.RotationDifference(PoseGeometry.Rotation(p.First.Matrix),PoseGeometry.Rotation(p.Second.Rest.Matrix))<.001f),
            "Source rest maps to every unchanged target local rest before any contact or reach fitting");

        var identityMotion=Motion(source,450,new(3,12,0),new(4,2,1));
        var identity=Transfer(source,source,identityMotion);
        check(identity.SequenceEqual(AnimationService.Evaluate(source.Scene,identityMotion,0).Local)&&identity[0].Rotation.Z==450,
            "Same native graph preserves exact sampled local values and intentional Euler winding under Preserve");

        // A physical duplicate in centimetres and a model frame rotated+90
        // aboutZ has explicit coordinate map(x,y,z)->(-100y,100x,100z).
        var native=Fixture("metric",10,10);var nativeMotion=Motion(native,90,new(3,12,0),new(4,2,1));
        var convertedScene=native.Scene with
        {
            Name="centimetre-frame",SkeletonRevision=Guid.NewGuid(),
            Bones=native.Scene.Bones.Select((b,i)=>b with{Name="different_"+i,Rest=b.Rest with
            {Translation=b.Rest.Translation*100,Rotation=i==0?new(0,0,90):b.Rest.Rotation}}).ToArray()
        };
        var convertedRig=Rig(convertedScene,new(Vector3.UnitY,-Vector3.UnitX,Vector3.UnitZ),100);
        var converted=native with{Scene=convertedScene,ControlRigs=[convertedRig],Clips=[]};
        var originalPose=AnimationService.Evaluate(native.Scene,nativeMotion,0);
        var convertedPose=Pose(converted,Transfer(native,converted,nativeMotion));
        check(originalPose.World.Zip(convertedPose.World).All(p=>Vector3.Distance(p.Second.Translation,
              new(-100*p.First.Translation.Y,100*p.First.Translation.X,100*p.First.Translation.Z))<.001f),
            "Actor-frame body transport preserves independent rotated-coordinate and centimetre Cartesian expectations for every bone");

        var inverseClip=Motion(target,-90,new(0,20,0));
        var inversePose=Pose(source,Transfer(target,source,inverseClip));
        check(Vector3.Distance(inversePose.World[1].Translation,new(10,0,0))<.00005f,
            "The independent reverse transfer rotates the smaller target's own body radius without a character or action rule");

        var scaledSource=source with{Scene=source.Scene with{Bones=source.Scene.Bones.Select((b,i)=>i==0?b with{Rest=b.Rest with{Scale=new(2,1,1)}}:b).ToArray(),SkeletonRevision=Guid.NewGuid()}};
        var scaledTarget=target with{Scene=target.Scene with{Bones=target.Scene.Bones.Select((b,i)=>i==0?b with{Rest=b.Rest with{Scale=new(.5f,2,1)}}:b).ToArray(),SkeletonRevision=Guid.NewGuid()}};
        scaledSource=scaledSource with{ControlRigs=[Rig(scaledSource.Scene,new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),1)]};
        scaledTarget=scaledTarget with{ControlRigs=[Rig(scaledTarget.Scene,new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),1)]};
        var scaledMotion=Motion(scaledSource,90,new(0,10,0));
        var scaleTracks=scaledMotion.Tracks.ToArray();var scaleChannels=scaleTracks[0].Channels.ToArray();
        scaleChannels[0]=new([new(0,4)]);scaleChannels[1]=new([new(0,3)]);scaleTracks[0]=scaleTracks[0] with{Channels=scaleChannels};
        scaledMotion=scaledMotion with{Tracks=scaleTracks};
        var scaledLocals=Transfer(scaledSource,scaledTarget,scaledMotion);var scaledPose=Pose(scaledTarget,scaledLocals);
        check(scaledLocals[0].Scale==new Vector3(1,6,1)&&Vector3.Distance(scaledPose.World[1].Translation,new(-120,0,0))<.00015f,
            "Authored actor relative scale composes once with different positive native rest scales and a quarter-turn; it is not anatomy stretch");

        var relocatedSource=source with{Scene=source.Scene with{Bones=source.Scene.Bones.Select((b,i)=>i==0?b with{Rest=b.Rest with{Translation=new(4,7,2),Rotation=new(0,0,25),Scale=new(2,1,1)}}:b).ToArray(),SkeletonRevision=Guid.NewGuid()}};
        var relocatedTarget=target with{Scene=target.Scene with{Bones=target.Scene.Bones.Select((b,i)=>i==0?b with{Rest=b.Rest with{Translation=new(-3,11,-4),Rotation=new(0,0,-35),Scale=new(.5f,2,1)}}:b).ToArray(),SkeletonRevision=Guid.NewGuid()}};
        relocatedSource=relocatedSource with{ControlRigs=[Rig(relocatedSource.Scene,new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),1)]};
        relocatedTarget=relocatedTarget with{ControlRigs=[Rig(relocatedTarget.Scene,new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),1)]};
        var relocatedRest=new AnimationClip(Guid.NewGuid(),"Independent rest",2,new(60),false,[]);
        check(Transfer(relocatedSource,relocatedTarget,relocatedRest).Zip(relocatedTarget.Scene.Bones).All(p=>
              Vector3.Distance(p.First.Translation,p.Second.Rest.Translation)<.00003f&&Vector3.Distance(p.First.Scale,p.Second.Rest.Scale)<.00001f&&
              PoseGeometry.RotationDifference(PoseGeometry.Rotation(p.First.Matrix),PoseGeometry.Rotation(p.Second.Rest.Matrix))<.001f),
            "Nonidentity actor rest translation, rotation and positive scale preserve every target rest without double-composing body offsets");

        var unrelated=target with{ControlRigs=[target.ControlRigs[0] with{BoneRoles=[new("RootMotion",2,"Deliberately incompatible actor",true),new("Hips",1,"Reviewed body",true)],BodyBone=1}]};
        bool rejected=false;try{Transfer(source,unrelated,clip);}catch(NotSupportedException){rejected=true;}
        check(rejected,"An unrelated actor/body binding is reported precisely rather than reading an unprepared descendant transform");
        NestedActorFrames(check);
    }

    static void NestedActorFrames(Action<bool,string> check)
    {
        FighterProject FixtureWithHelper(string prefix,float height)
        {
            var scene=new Scene(prefix,
            [new(prefix+"_route",-1,Transform.Identity),new(prefix+"_orientation",0,Transform.Identity),
             new(prefix+"_lower",1,Transform.Identity with{Translation=new(0,height,0)}),
             new(prefix+"_upper",2,Transform.Identity with{Translation=new(0,10,0)})],[]);
            var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
            {
                BodyBone=2,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Independent fixture units"),
                BoneRoles=[new("RootMotion",0,"Reviewed route",true),new("Channel.Orientation",1,"Reviewed native placement helper",true),new("Hips",2,"Reviewed body",true),new("Chest",3,"Reviewed upper body",true)]
            };
            return ProceduralFixture.Create() with{Scene=scene,ControlRigs=[rig],Clips=[],Contacts=[],MotionCoverage=[]};
        }
        var a=FixtureWithHelper("first",10);var b=FixtureWithHelper("second",20);
        var clip=new AnimationClip(Guid.NewGuid(),"Independent helper rotation",2,new(60),false,
            [new(a.Scene.Bones[1].Name,[null,null,null,null,null,new([new(0,90)]),null,null,null])]);
        var pose=Pose(b,Transfer(a,b,clip));
        check(Vector3.Distance(pose.World[2].Translation,new(-20,0,0))<.00005f,
            "A corresponding native orientation helper below RootMotion rotates the target body radius without becoming anatomical displacement");
        var reverse=new AnimationClip(Guid.NewGuid(),"Independent reverse helper rotation",2,new(60),false,
            [new(b.Scene.Bones[1].Name,[null,null,null,null,null,new([new(0,-90)]),null,null,null])]);
        var reversePose=Pose(a,Transfer(b,a,reverse));
        check(Vector3.Distance(reversePose.World[2].Translation,new(10,0,0))<.00005f,
            "The reverse native helper transfer retains the smaller target body radius independently of root travel");
        var combined=clip with{Tracks=[new(a.Scene.Bones[1].Name,[null,null,null,null,null,new([new(0,90)]),null,new([new(0,4)]),null])]};
        var combinedPose=Pose(b,Transfer(a,b,combined));
        check(Vector3.Distance(combinedPose.World[1].Translation,new(0,6,0))<.00005f&&Vector3.Distance(combinedPose.World[2].Translation,new(-20,6,0))<.00005f,
            "Native placement-helper translation and rotation compose once before body-local motion; the target helper keeps the existing zero-length-channel transfer policy");

        var sharedSource=a with{ControlRigs=[a.ControlRigs[0] with{BodyBone=0,BoneRoles=[new("RootMotion",0,"Shared native actor",true),new("Hips",0,"Shared native body",true),new("Chest",3,"Upper reference",true)]}]};
        var sharedTarget=b with{ControlRigs=[b.ControlRigs[0] with{BodyBone=0,BoneRoles=[new("RootMotion",0,"Shared native actor",true),new("Hips",0,"Shared native body",true),new("Chest",3,"Upper reference",true)]}]};
        var sharedClip=new AnimationClip(Guid.NewGuid(),"Shared actor/body channel",2,new(60),false,
            [new(a.Scene.Bones[0].Name,[null,null,null,null,null,new([new(0,90)]),new([new(0,4)]),null,null])]);
        var shared=Pose(sharedTarget,Transfer(sharedSource,sharedTarget,sharedClip));
        check(Vector3.Distance(shared.World[0].Translation,new(6,0,0))<.00005f&&PoseGeometry.Finite(shared.World[3].Translation),
            "A shared native actor/body bone has one root-policy owner and never reads an unprepared self frame");
    }

    static FighterProject Fixture(string prefix,float height,float torso,float actorRest=0)
    {
        // Keep the body upright in model space under the independently chosen
        // native actor frame;±90 use explicit axis values rather than deriving
        // fixture expectations with production coordinate helpers.
        var body=actorRest==90?new Vector3(height,0,0):actorRest==-90?new Vector3(-height,0,0):new Vector3(0,height,0);
        var scene=new Scene(prefix,
        [new(prefix+"_actor",-1,Transform.Identity with{Rotation=new(0,0,actorRest)}),
         new(prefix+"_body",0,Transform.Identity with{Translation=body,Rotation=new(0,0,-actorRest)}),
         new(prefix+"_upper",1,Transform.Identity with{Translation=new(0,torso,0)})],[]);
        var rig=Rig(scene,new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),1);
        return ProceduralFixture.Create() with{Scene=scene,ControlRigs=[rig],Clips=[],Contacts=[],MotionCoverage=[]};
    }
    static ControlRigSetup Rig(Scene scene,RigAxes axes,float units)=>new(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
    {
        BodyBone=1,Axes=axes,Coordinates=new(units,"Independent Cartesian fixture units"),
        BoneRoles=[new("RootMotion",0,"Reviewed native actor",true),new("Hips",1,"Reviewed native body",true),new("Chest",2,"Reviewed native upper body",true)]
    };
    static AnimationClip Motion(FighterProject source,float actorRotation,Vector3 body,Vector3 actor=default)
    {
        var local=source.Scene.Bones.Select(b=>b.Rest).ToArray();
        local[0]=local[0] with{Rotation=new(0,0,actorRotation),Translation=actor};local[1]=local[1] with{Translation=body};
        return new(Guid.NewGuid(),"Independent native motion",2,new(60),false,
            local.Select((t,i)=>new BoneTrack(source.Scene.Bones[i].Name,t.Channels().Select(v=>(Curve?)new Curve([new(0,v)])).ToArray())).ToArray());
    }
    static Transform[] Transfer(FighterProject source,FighterProject target,AnimationClip clip,RootMotionPolicy root=RootMotionPolicy.Preserve)
    {
        var a=source.ControlRigs[0];var b=target.ControlRigs[0];
        var request=new RetargetRequest(source with{Clips=[clip]},clip,a,target,b,root,false,default,null,null,null,null,null,null,null);
        return new BaselineMotionTransfer(request,RigDescriptor.Prepare(source.Scene,a),RigDescriptor.Prepare(target.Scene,b)).Evaluate(0);
    }
    static EvaluatedPose Pose(FighterProject project,Transform[] local)=>AnimationService.Evaluate(project.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
}
