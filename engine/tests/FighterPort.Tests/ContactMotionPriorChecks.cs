using System.Numerics;
using FighterPort.Core;

internal static class ContactMotionPriorChecks
{
    public static void Run(Action<bool,string> check)
    {
        var fixture=Create();var plan=fixture.Plan;var before=Pose(plan,1);
        string rigProof=AnimationControls.DeformChecksum(plan.Rig.Scene),graphProof=RetargetDependencies.Hash(plan.Graph);
        var prepared=plan.PrepareBodyReference();var after=Pose(prepared,1);
        var shift=after.World[1].Translation-before.World[1].Translation;
        // Independent scalar objective: (2+d)^2+min(0,1+d)^2.
        // Its minimizer is d=-1.5. Selecting only the edge gives d=-2,
        // which unnecessarily moves the second required support off the top.
        check(Vector3.Distance(shift,new(0,0,-1.5f))<.0002f,"The body reference jointly minimizes the independently specified edge and bounded-top relationships");
        float edge=after.World[2].Translation.Z,top=after.World[3].Translation.Z;
        check(Math.Abs(edge-.5)<.0002&&Math.Abs(top+.5)<.0002
            &&edge*edge+MathF.Min(0,top)*MathF.Min(0,top)<.5002f,
            "The joint body reference has independently expected .5/.5 residuals and improves on the edge-only reference cost1");
        check(TargetMotionConstraints.MaximumNormalizedViolation(prepared,1,after)>1,
            "An improved translation-only compromise does not claim frozen required-contact acceptance");
        check(after.Local[0]==before.Local[0]&&after.Local.Skip(2).SequenceEqual(before.Local.Skip(2))
            &&after.Local[1].Rotation==before.Local[1].Rotation&&after.Local[1].Scale==before.Local[1].Scale
            &&AnimationControls.DeformChecksum(plan.Rig.Scene)==rigProof&&RetargetDependencies.Hash(prepared.Graph)==graphProof,
            "Reference preparation changes only the existing permitted body's animation translation, retaining contact anchors, native bones, rests and skin");
        check(new[]{0d,.125,1d,1.875,2d}.All(t=>Vector3.Distance(Pose(prepared,t).World[1].Translation-before.World[1].Translation,shift)<.0002f),
            "Constant independently authored relationships retain a constant guide through unchanged temporal regularization");

        var soft=plan with{Graph=plan.Graph with{Relations=[plan.Graph.Relations[0] with{Pins=[fixture.Edge,fixture.Top with{SoftSupport=true}]}]}};
        var softPose=Pose(soft.PrepareBodyReference(),1);
        check(Math.Abs(softPose.World[1].Translation.Z+2)<.0002,
            "Optional soft observations do not displace the required-contact body reference");
        var required=soft with{Graph=soft.Graph with{Relations=[soft.Graph.Relations[0] with{Pins=[fixture.Edge,fixture.Top with{SoftSupport=true,RequiredSupport=true}]}]}};
        check(Vector3.Distance(Pose(required.PrepareBodyReference(),1).World[1].Translation,after.World[1].Translation)<.0002f,
            "A required relationship stays required independently of its soft-observation flag");

        // The same supplied geometry with a fixed point instead of a corner
        // uses the same numerical algorithm; edge labels do not select it.
        var fixedPoint=plan with{Graph=plan.Graph with{Relations=[plan.Graph.Relations[0] with{Pins=[fixture.Edge with{EdgeSupport=false,EdgeInward=null,PlaneNormal=null},fixture.Top]}]}};
        check(Vector3.Distance(Pose(fixedPoint.PrepareBodyReference(),1).World[1].Translation,after.World[1].Translation)<.0002f,
            "Body-reference preparation is not gated on an edge contact or a particular support classification");

        var blended=fixture.Edge with{SurfacePoints=[fixture.Edge.SurfacePoints[0] with{Joints=new(2,0,0,0),Weights=new(.5f,.5f,0,0)}]};
        var blendPlan=plan with{Graph=plan.Graph with{Relations=[plan.Graph.Relations[0] with{Pins=[blended]}]}};
        var blendPose=Pose(blendPlan.PrepareBodyReference(),1);
        check(Math.Abs(blendPose.World[1].Translation.Z+4)<.0003
            &&Math.Abs(ContactGeometry.Point(blended,blendPose).Z)<.0002,
            "A patch with half actor and half body skin influence needs the independently expected four-unit body shift to close its two-unit gap");

        var rotated=Create(true);var rotatedBefore=Pose(rotated.Plan,1);var rotatedAfter=Pose(rotated.Plan.PrepareBodyReference(),1);
        check(Vector3.Distance(rotatedAfter.World[1].Translation-rotatedBefore.World[1].Translation,new(-1.5f,0,0))<.0003f
            &&rotatedAfter.Local[0]==rotatedBefore.Local[0],
            $"World-space support preparation converts through a rotated native parent without rotating or translating the actor channel; shift={rotatedAfter.World[1].Translation-rotatedBefore.World[1].Translation}");
        var repeated=plan.PrepareBodyReference();
        check(new[]{0d,.375,1d,2d}.All(t=>Pose(repeated,t).World.SequenceEqual(Pose(prepared,t).World)),
            "Repeated body-reference preparation is deterministic and independent of scrub history");
        bool cancelled=false;using(var stop=new CancellationTokenSource()){stop.Cancel();try{plan.PrepareBodyReference(stop.Token);}catch(OperationCanceledException){cancelled=true;}}
        check(cancelled,"Body-reference preparation observes cancellation before preparing samples");
        var noPins=plan with{Graph=new([],MotionContactCoverage.Unknown,[]),Clearance=new(Vector3.UnitY,.3f,plan.Rig.SkinPoints),
            Grounding=new("Independently supplied plane",.3f,.3f,0,[],0){ExplicitSurface=true}};
        var clear=Pose(noPins.PrepareBodyReference(),1);
        check(Math.Abs(clear.World[1].Translation.Y-before.World[1].Translation.Y-.3f)<.0002f
            &&plan.Rig.SkinPoints.All(p=>ContactGeometry.Point(p,clear).Y>=.3f-.0002f),
            "Without an invented point pin, actual penetrating skin gives the independently expected .3-unit permitted body reference lift");
        var alreadyClear=noPins with{Clearance=noPins.Clearance with{Height=-.3f}};
        check(Pose(alreadyClear.PrepareBodyReference(),1).World.SequenceEqual(before.World),
            "An airborne surface-clear reference is preserved rather than planted or shifted to the floor");
        var outside=noPins with{Clearance=noPins.Clearance with{Edge=new(-Vector3.UnitZ,0)}};
        check(Pose(outside.PrepareBodyReference(),1).World.SequenceEqual(before.World),
            "Supplied corner geometry permits a below-top body outside the platform instead of creating an infinite floor");
        var supported=noPins with{BodySupportPoints=[plan.Rig.SkinPoints[0]],Grounding=noPins.Grounding with{BodySupportedThroughout=true}};
        check(Math.Abs(ContactGeometry.Point(plan.Rig.SkinPoints[0],Pose(supported.PrepareBodyReference(),1)).Y-.3f)<.0002f,
            "Required body support participates in the same geometric body reference when semantic point pins are absent");
        var conflictingPlane=plan with{Clearance=noPins.Clearance,Grounding=noPins.Grounding};
        var lockedReference=conflictingPlane.PrepareBodyReference();
        check(Pose(lockedReference,1).World.SequenceEqual(after.World)
            &&TargetMotionConstraints.MaximumNormalizedViolation(lockedReference,1,Pose(lockedReference,1))>1,
            "A conflicting floor cannot move the required-contact reference or silently erase its independently measured unresolved clearance");
        check(AnimationControls.DeformChecksum(plan.Rig.Scene)==rigProof&&RetargetDependencies.Hash(plan.Graph)==graphProof,
            "Preparing floor/body reach references preserves the complete original native rig and authentic interaction anchors");
    }

    static EvaluatedPose Pose(TargetMotionPlan plan,double frame)
        =>AnimationService.Evaluate(plan.Rig.Scene,null,0,plan.Reference(frame).Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t)) with{Seconds=frame/plan.Rate.FramesPerSecond};

    static (TargetMotionPlan Plan,ContactPin Edge,ContactPin Top) Create(bool rotated=false)
    {
        var scene=new Scene("Independent two-region reference",[
            new("unrelated_actor_name",-1,Transform.Identity with{Rotation=rotated?new(0,90,0):Vector3.Zero}),
            new("unrelated_carrier_name",0,Transform.Identity with{Translation=new(0,2,0)}),
            new("unrelated_region_a",1,Transform.Identity with{Translation=new(0,-2,2)}),
            new("unrelated_region_b",1,Transform.Identity with{Translation=new(0,-2,1)})],[]);
        var rest=AnimationService.Evaluate(scene,null,0);var at=rest.World;
        scene=scene with{Meshes=[new("Independent skin",[
            new(at[2].Translation,Vector3.UnitY,Vector2.Zero,new(2,0,0,0),new(1,0,0,0),Vector4.One),
            new(at[3].Translation,Vector3.UnitY,Vector2.Zero,new(3,0,0,0),new(1,0,0,0),Vector4.One),
            new(at[3].Translation+new Vector3(.1f,0,0),Vector3.UnitY,Vector2.Zero,new(3,0,0,0),new(1,0,0,0),Vector4.One)],[0,1,2])]};
        var rig=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[]){
            BodyBone=1,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),Coordinates=new(1,"Independent fixture metre"),
            BoneRoles=[new("RootMotion",0,"Supplied actor",true),new("Hips",1,"Supplied body",true),
                new("RegionA",2,"Supplied region identity",true),new("RegionB",3,"Supplied region identity",true)]};
        var clip=new AnimationClip(Guid.NewGuid(),"Constant supplied relationships",3,new(60),false,[]);
        var project=new FighterProject(1,Guid.NewGuid(),"Independent fixture",scene,[clip]){ControlRigs=[rig]};
        var descriptor=RigDescriptor.Prepare(scene,rig);
        var request=new RetargetRequest(project,clip,rig,project,rig,RootMotionPolicy.Preserve,true,default,null,null,null,null,null,null,null);
        var forward=Vector3.TransformNormal(Vector3.UnitZ,at[0]);
        var edge=new ContactPin(Guid.NewGuid(),"RegionA",2,Vector3.Zero,Vector3.Zero,Quaternion.Identity,false,"Supplied edge"){
            EdgeSupport=true,PlaneNormal=Vector3.UnitY,EdgeInward=forward,SurfacePoints=[descriptor.SkinPoints[0]]};
        var top=new ContactPin(Guid.NewGuid(),"RegionB",3,Vector3.Zero,Vector3.Zero,Quaternion.Identity,false,"Supplied bounded top"){
            PlaneNormal=Vector3.UnitY,AllowedSurface=new(Vector3.UnitY,0,new(forward,0)),SurfacePoints=[descriptor.SkinPoints[1]]};
        var relations=new ContactFrame[]{new(Guid.NewGuid(),clip.Id,0,rig.SkeletonFingerprint,[edge,top]){EndFrame=3,EndExclusive=true}};
        var plan=new TargetMotionPlan(descriptor,rig,new(request,descriptor,descriptor),new(relations,MotionContactCoverage.Required,[]),null,new(),new(60),3,false,false,[]){
            PositionBound=descriptor.CharacteristicLength*RetargetAcceptance.PositionScaleFraction,BaselineSourceRate=60,AuthenticSourceSamples=3};
        return(plan,edge,top);
    }
}
