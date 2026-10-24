using System.Numerics;
using FighterPort.Core;

internal static class RigTransferV2Checks
{
    public static void Run(Action<bool,string> check)
    {
        var source=RetargetV2Checks.Chain(false);var rig=source.ControlRigs[0];
        var clip=new AnimationClip(Guid.NewGuid(),"Independent coordinate action",3,new(60),false,
        [new(source.Scene.Bones[0].Name,[null,null,null,null,null,null,new([new(0,2)]),new([new(0,3)]),null]),
         new(source.Scene.Bones[2].Name,[new([new(0,1.2f)]),null,null,null,null,new([new(0,30)]),new([new(0,1.25f)]),null,null])]);
        source=source with{Clips=[clip],Contacts=[],MotionCoverage=[]};
        BaselineMotionTransfer Prepare(FighterProject a,ControlRigSetup ar,FighterProject b,ControlRigSetup br)=>new(new(a,a.Clips[0],ar,b,br,RootMotionPolicy.Preserve,false,default,null,null,null,null,null,null,null),RigDescriptor.Prepare(a.Scene,ar),RigDescriptor.Prepare(b.Scene,br));
        var metres=Prepare(source,rig,source,rig).Evaluate(0);
        foreach(bool reversed in new[]{false,true})
        {
            // The centimetre fixture is a physical duplicate rotated 90 degrees
            // about Z. Expected coordinates are explicitly (-100*y,100*x,z).
            var bones=source.Scene.Bones.Select((b,i)=>b with{Name="Centimetre_"+i,Rest=b.Rest with{Translation=b.Rest.Translation*100,Rotation=i==0?new(0,0,90):b.Rest.Rotation}}).ToArray();
            var scene=source.Scene with{Bones=bones,Meshes=[],SkeletonRevision=Guid.NewGuid()};
            var otherRig=rig with{SkeletonFingerprint=AnimationControls.Fingerprint(scene),Axes=new(Vector3.UnitY,-Vector3.UnitX,Vector3.UnitZ),Coordinates=new(100,"Independently specified centimetres, rotated Z+90")};
            var other=source with{Scene=scene,Clips=[],ControlRigs=[otherRig]};var transfer=Prepare(source,rig,other,otherRig);var converted=transfer.Evaluate(0);
            check(Math.Abs(transfer.FileUnitRatio-100)<.00001f&&Math.Abs(transfer.RootAnatomicalRatio-1)<.00001f,"Physical unit conversion is independent of anatomical travel scaling");
            var evaluated=AnimationService.Evaluate(scene,null,0,converted.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
            var original=AnimationService.Evaluate(source.Scene,clip,0);
            check(evaluated.World.Zip(original.World).All(p=>Vector3.Distance(p.First.Translation,new(-100*p.Second.Translation.Y,100*p.Second.Translation.X,100*p.Second.Translation.Z))<.0003f),"Rotated coordinates and centimetre units preserve every physical native-bone position");
            check(Math.Abs(converted[2].Scale.X-1.2f)<.00001f&&Math.Abs(converted[2].Translation.X-125)<.0001f,"Authored nonuniform scale and local translation survive coordinate/unit conversion without solver stretch");
            if(reversed)
            {
                var tracks=converted.Select((t,i)=>new BoneTrack(scene.Bones[i].Name,t.Channels().Select(v=>(Curve?)new Curve([new Key(0,v)])).ToArray())).ToArray();
                var reverse=Prepare(other with{Clips=[clip with{Tracks=tracks}]},otherRig,source,rig).Evaluate(0);
                check(reverse.Zip(metres).All(p=>Vector3.Distance(p.First.Translation,p.Second.Translation)<.00001f&&Vector3.Distance(p.First.Scale,p.Second.Scale)<.00001f&&PoseGeometry.RotationDifference(PoseGeometry.Rotation(p.First.Matrix),PoseGeometry.Rotation(p.Second.Matrix))<.001f),"Reversing rotated coordinates and file units returns the same authored native local channels");
            }
        }
        var mirroredScene=source.Scene with{Bones=source.Scene.Bones.Select((b,i)=>b with{Name="ReflectedCoordinates_"+i,Rest=b.Rest with{Translation=new(-b.Rest.Translation.X,b.Rest.Translation.Y,b.Rest.Translation.Z)}}).ToArray(),Meshes=[],SkeletonRevision=Guid.NewGuid()};
        var mirroredRig=rig with{SkeletonFingerprint=AnimationControls.Fingerprint(mirroredScene),Axes=new(-Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ)};
        var mirrored=source with{Scene=mirroredScene,Clips=[],ControlRigs=[mirroredRig]};var mirroredPose=Prepare(source,rig,mirrored,mirroredRig).Evaluate(0);
        check(Math.Abs(mirroredPose[2].Rotation.Z+30)<.0001f&&Math.Abs(mirroredPose[0].Translation.X+2)<.00001f&&Math.Abs(mirroredPose[2].Translation.X+1.25f)<.00001f,"Reviewed reflected coordinate frames conjugate rotations without introducing reflected native binds");
        var rotatedBones=source.Scene.Bones.Select((b,i)=>b with{Rest=b.Rest with{Rotation=i==1?new(0,0,20):i==2?new(0,0,-35):Vector3.Zero}}).ToArray();
        var aScene=source.Scene with{Bones=rotatedBones,Meshes=[],SkeletonRevision=Guid.NewGuid()};var aRig=rig with{SkeletonFingerprint=AnimationControls.Fingerprint(aScene)};
        var aClip=clip with{Tracks=[new(aScene.Bones[1].Name,[null,null,null,null,null,new([new(0,45)]),null,null,null]),new(aScene.Bones[2].Name,[null,null,null,null,null,new([new(0,-5)]),null,null,null])]};
        var bScene=aScene with{Bones=aScene.Bones.Select((b,i)=>b with{Name="DifferentRest_"+i,Rest=b.Rest with{Rotation=i==1?new(0,0,-15):i==2?new(0,0,50):Vector3.Zero}}).ToArray(),SkeletonRevision=Guid.NewGuid()};var bRig=rig with{SkeletonFingerprint=AnimationControls.Fingerprint(bScene)};
        var restTransfer=Prepare(source with{Scene=aScene,Clips=[aClip]},aRig,source with{Scene=bScene,Clips=[]},bRig).Evaluate(0);
        check(Math.Abs(restTransfer[1].Rotation.Z-10)<.0001f&&Math.Abs(restTransfer[2].Rotation.Z-80)<.0001f,"Changed parent rests transfer independently authored +25 and +30 degree motion onto the existing target rest");
        var spatialClip=clip with{Tracks=[new(source.Scene.Bones[1].Name,[null,null,null,null,null,new([new(0,30)]),null,null,null]),new(source.Scene.Bones[2].Name,[null,null,null,new([new(0,20)]),null,null,null,null,null])]};
        var spatialScene=source.Scene with{Bones=source.Scene.Bones.Select((b,i)=>b with{Name="SpatialRest_"+i,Rest=b.Rest with{Rotation=i==1?new(90,0,0):i==2?new(0,90,0):Vector3.Zero}}).ToArray(),Meshes=[],SkeletonRevision=Guid.NewGuid()};var spatialRig=rig with{SkeletonFingerprint=AnimationControls.Fingerprint(spatialScene)};
        var spatialLocal=Prepare(source with{Clips=[spatialClip]},rig,source with{Scene=spatialScene,Clips=[]},spatialRig).Evaluate(0);var spatialPose=AnimationService.Evaluate(spatialScene,null,0,spatialLocal.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
        var expectedParent=Matrix4x4.CreateRotationX(MathF.PI/2)*Matrix4x4.CreateRotationZ(MathF.PI/6);var expectedChild=Matrix4x4.CreateRotationY(MathF.PI/2)*Matrix4x4.CreateRotationX(110*MathF.PI/180)*Matrix4x4.CreateRotationZ(MathF.PI/6);
        check(new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ}.All(axis=>Vector3.Distance(Vector3.TransformNormal(axis,spatialPose.World[1]),Vector3.TransformNormal(axis,expectedParent))<.00001f&&Vector3.Distance(Vector3.TransformNormal(axis,spatialPose.World[2]),Vector3.TransformNormal(axis,expectedChild))<.00001f),"Noncommuting X/Y parent rests preserve independently specified world action axes without changing multiplication order");
        var spatialTracks=spatialLocal.Select((t,i)=>new BoneTrack(spatialScene.Bones[i].Name,t.Channels().Select(v=>(Curve?)new Curve([new Key(0,v)])).ToArray())).ToArray();var spatialReverse=Prepare(source with{Scene=spatialScene,Clips=[spatialClip with{Tracks=spatialTracks}]},spatialRig,source,rig).Evaluate(0);var originalSpatial=AnimationService.Evaluate(source.Scene,spatialClip,0);
        check(spatialReverse.Select((t,i)=>(t,i)).All(p=>PoseGeometry.RotationDifference(PoseGeometry.Rotation(p.t.Matrix),PoseGeometry.Rotation(originalSpatial.Local[p.i].Matrix))<.001f),"Reversing noncommuting native parent rests restores the authored local rotation channels");
        var scaledSourceScene=source.Scene with{Bones=source.Scene.Bones.Select((b,i)=>i==1?b with{Rest=b.Rest with{Scale=new(2,1,1)}}:b).ToArray(),Meshes=[],SkeletonRevision=Guid.NewGuid()};
        var scaledTargetScene=source.Scene with{Bones=source.Scene.Bones.Select((b,i)=>b with{Name="NonuniformTarget_"+i,Rest=i==1?b.Rest with{Scale=new(.5f,2,1)}:b.Rest}).ToArray(),Meshes=[],SkeletonRevision=Guid.NewGuid()};
        var scaledSourceRig=rig with{SkeletonFingerprint=AnimationControls.Fingerprint(scaledSourceScene)};var scaledTargetRig=rig with{SkeletonFingerprint=AnimationControls.Fingerprint(scaledTargetScene)};
        var scaledPose=Prepare(source with{Scene=scaledSourceScene},scaledSourceRig,source with{Scene=scaledTargetScene,Clips=[]},scaledTargetRig).Evaluate(0);
        check(Math.Abs(scaledPose[2].Translation.X-1.25f)<.00001f&&Math.Abs(scaledPose[2].Scale.X-1.2f)<.00001f&&scaledPose[1].Scale==new Vector3(.5f,2,1),"Different positive nonuniform parent binds transfer authored relative translation/scale using physical rest segments without rewriting native scales");
        var singular=source.Scene with{Bones=source.Scene.Bones.Select((b,i)=>i==2?b with{Rest=b.Rest with{Scale=new(0,1,1)}}:b).ToArray()};
        bool rejected=false;try{RigDescriptor.Prepare(singular,rig with{SkeletonFingerprint=AnimationControls.Fingerprint(singular)});}catch(NotSupportedException){rejected=true;}
        check(rejected,"Singular native rests require an explicit affine policy rather than bind normalization");
        var extraBones=source.Scene.Bones.Take(3).Concat([new Bone("NativeIntermediateTwist",2,Transform.Identity with{Translation=new(.2f,0,0),Rotation=new(0,0,17)})]).Concat(source.Scene.Bones.Skip(3).Select((b,i)=>b with{Name="Extended_"+i,Parent=b.Parent+1,Rest=i==0?b.Rest with{Translation=new(.8f,0,0)}:b.Rest})).ToArray();
        var extraScene=source.Scene with{Bones=extraBones,Meshes=[],SkeletonRevision=Guid.NewGuid()};
        var extraRig=rig with{SkeletonFingerprint=AnimationControls.Fingerprint(extraScene),BoneRoles=rig.BoneRoles.Select(r=>r with{Bone=r.Bone>=3?r.Bone+1:r.Bone}).ToArray(),VirtualChains=[new("Appendage",[1,2,3,4,5,6],"Reviewed extra intermediate native joint",true)],PreservedHelperBones=[3]};
        var extra=source with{Scene=extraScene,Clips=[],ControlRigs=[extraRig]};var extraTransfer=Prepare(source,rig,extra,extraRig);var local=extraTransfer.Evaluate(0);
        check(RigDescriptor.Prepare(extraScene,extraRig).Binding.Chains.Single().Bones.Length==6&&extraTransfer.Mapping.TargetOnly.Any(p=>p.Bone==3)&&local[3]==extraBones[3].Rest,"Different compatible chain topology retains an unmapped native intermediate twist and its full virtual path");
        var restClip=clip with{Tracks=[]};var mappedRest=Prepare(source with{Clips=[restClip]},rig,extra,extraRig).Evaluate(0);
        check(mappedRest.Zip(extraBones).All(p=>Vector3.Distance(p.First.Translation,p.Second.Rest.Translation)<.00001f&&PoseGeometry.RotationDifference(PoseGeometry.Rotation(p.First.Matrix),PoseGeometry.Rotation(p.Second.Rest.Matrix))<.001f),"Extra target spine/twist topology still maps source rest to unchanged target local rests");
        var turns=clip with{SampleCount=8,Tracks=[new(source.Scene.Bones[2].Name,[null,null,null,null,null,new([new(0,0),new(7,720)],Interpolation.Linear),null,null,null])]};
        var spun=RetargetService.Bake(source with{Clips=[turns]},turns,rig,extra,extraRig,useContacts:false);
        check(spun.Value is not null&&Math.Abs(spun.Value.Clip.Tracks.Single(t=>t.Bone==extraScene.Bones[2].Name).Channels[5]!.Keys[^1].Value-720)<.001f,"Cross-rig native Euler baking preserves two intentional complete turns on a different compatible topology");
    }
}
