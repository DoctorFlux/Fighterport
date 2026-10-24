using System.Numerics;

namespace FighterPort.Core;

// Manual editor posing only. This does not alter the retarget contact solver,
// automatic contact detection, native anatomy or any acceptance tolerance.
public static class EditorControlPosing
{
    public static OperationResult<ContactSolution> Solve(Scene scene,EvaluatedPose input,ControlRigSetup setup,IReadOnlyDictionary<int,Transform> edits,ContactPin[] pins)
    {
        OperationResult<ContactSolution> Fail(string code,string message)=>new(null,[new(code,message)],[]);
        if(AnimationControls.ResolveBodyBone(scene,setup)<0)return Fail("BODY_REVIEW_REQUIRED","Review the body/pelvis binding before posing; motion-control identities are unresolved.");
        if(AnimationControls.ProtectedMotionBones(scene,setup).Any(edits.ContainsKey))
            return Fail("SOURCE_MOTION_CONTROL","Motion controls follow their own source animation. Pose the existing body bones instead.");
        var soft=pins.Where(p=>p.EditorBehavior==EditorPinBehavior.SoftReach).ToArray();var fixedPins=pins.Where(p=>p.EditorBehavior!=EditorPinBehavior.SoftReach).ToArray();
        if(soft.Length==0)return ContactSolver.Solve(scene,input,setup,edits,pins);
        if(setup.SkeletonFingerprint!=AnimationControls.Fingerprint(scene))return Fail("RIG_CHANGED","Editor pins belong to another native rig revision.");
        if(edits.Any(e=>e.Key<0||e.Key>=scene.Bones.Length||!e.Value.Channels().All(float.IsFinite)))return Fail("INVALID_EDIT","Control edits must be finite existing-bone transforms.");
        if(soft.Any(p=>p.Reference is not null||p.Socket is not null||p.LocalPoint!=Vector3.Zero||p.SurfacePoints.Length>0||p.SurfaceRegion.Length>0||p.Clearance is not null||p.PlaneNormal is not null||p.AllowedSurface is not null||!PoseGeometry.Finite(p.Position)))return Fail("SOFT_PIN_CONTEXT","Soft reach applies to editor joint handles. Authored surfaces, sockets and reference-bound locks retain their fixed policy.");
        var chains=soft.Select(p=>setup.Chains.FirstOrDefault(c=>c.Effector==p.Bone)).ToArray();
        if(chains.Any(c=>c is null))return Fail("SOFT_PIN_CHAIN","Review an existing native limb chain before soft pinning this handle.");
        for(int i=0;i<chains.Length;i++)for(int j=i+1;j<chains.Length;j++)if(PoseGeometry.Ancestor(scene,chains[i]!.Root,chains[j]!.Root)||PoseGeometry.Ancestor(scene,chains[j]!.Root,chains[i]!.Root))return Fail("SOFT_PIN_COUPLED","Overlapping soft-pin chains need a reviewed coupled control policy.");
        var local=input.Local.Select((t,i)=>(t,i)).ToDictionary(x=>x.i,x=>x.t);foreach(var edit in edits)local[edit.Key]=edit.Value;
        EvaluatedPose Pose()=>AnimationService.Evaluate(scene,null,0,local) with{Seconds=input.Seconds,GeometryPositions=input.GeometryPositions};
        var diagnostics=new List<Diagnostic>();float maximumError=0,maximumRotation=0;
        if(fixedPins.Length>0){var exact=ContactSolver.Solve(scene,input,setup,edits,fixedPins);if(exact.Value is null)return exact;foreach(var e in exact.Value.Transforms)local[e.Key]=e.Value;}
        for(int index=0;index<soft.Length;index++)
        {
            var pin=soft[index];var chain=chains[index]!;var pose=Pose();
            if(scene.Bones[chain.Middle].Parent!=chain.Root||scene.Bones[chain.Effector].Parent!=chain.Middle)return Fail("SOFT_PIN_HIERARCHY","Soft reach requires the reviewed direct native chain; helper/twist-chain controls remain separate.");
            for(int b=chain.Effector;b>=0;b=scene.Bones[b].Parent){var s=pose.Local[b].Scale;if(s.X<=0||Math.Abs(s.X-s.Y)>.0001f||Math.Abs(s.Y-s.Z)>.0001f)return Fail("SOFT_PIN_SCALE","Soft reach does not normalize nonuniform/native scales. Use FK or a qualified scale-aware control.");}
            var a=pose.World[chain.Root].Translation;var bPoint=pose.World[chain.Middle].Translation;var c=pose.World[chain.Effector].Translation;
            float first=Vector3.Distance(a,bPoint),second=Vector3.Distance(bPoint,c);if(first<1e-6f||second<1e-6f)return Fail("SOFT_PIN_LENGTH","The existing deform chain has no supported finite segment length.");
            bool rootFixed=edits.ContainsKey(chain.Root),middleFixed=edits.ContainsKey(chain.Middle);Vector3 goal;
            Vector3 Project(Vector3 origin,float minimum,float maximum,Vector3 fallback)
            {
                var delta=pin.Position-origin;float distance=delta.Length();var direction=distance>1e-8f?delta/distance:Vector3.Normalize(fallback.LengthSquared()>1e-12f?fallback:bPoint-a);
                return origin+direction*Math.Clamp(distance,minimum,maximum);
            }
            if(rootFixed&&middleFixed)goal=c;
            else if(rootFixed){goal=Project(bPoint,second,second,c-bPoint);local[chain.Middle]=Align(scene,pose,chain.Middle,c-bPoint,goal-bPoint);}
            else if(middleFixed){float radius=Vector3.Distance(a,c);if(radius<1e-6f)return Fail("SOFT_PIN_FOLDED","A fully folded fixed-elbow chain needs a reviewed orientation control.");goal=Project(a,radius,radius,c-a);local[chain.Root]=Align(scene,pose,chain.Root,c-a,goal-a);}
            else
            {
                goal=Project(a,Math.Min(first+second,Math.Abs(first-second)+1.1e-5f),first+second,c-a);
                var solved=AnimationControls.Solve(scene,pose,setup,chain,goal,pin.Pole??AnimationControls.DefaultPole(scene,pose,chain));if(solved.Value is null)return new(null,solved.Diagnostics,solved.LostChannels);
                foreach(var e in solved.Value.Transforms)local[e.Key]=e.Value;
            }
            pose=Pose();if(pin.HoldRotation&&!edits.ContainsKey(chain.Effector))local[chain.Effector]=PoseGeometry.WorldRotation(scene,pose,chain.Effector,pin.Rotation);
            var checkedPose=Pose();float error=Vector3.Distance(checkedPose.World[chain.Effector].Translation,goal);maximumError=Math.Max(maximumError,error);
            if(pin.HoldRotation&&!edits.ContainsKey(chain.Effector))maximumRotation=Math.Max(maximumRotation,PoseGeometry.RotationDifference(PoseGeometry.Rotation(checkedPose.World[chain.Effector]),pin.Rotation)*MathF.PI/180);
            if(error>Math.Max(first+second,1)*.0001f)return Fail("SOFT_PIN_RESIDUAL","The soft control did not reach its permitted adapted goal. No pose was committed.");
            float yielded=Vector3.Distance(goal,pin.Position);if(yielded>(first+second)*1e-5f)diagnostics.Add(new("EDITOR_SOFT_PIN_YIELDED",$"{pin.Role}: soft handle yielded {yielded:G5} native units at the existing limb's reach limit; the authored anchor and native skeleton remain unchanged.",false));
        }
        var changes=local.Where(e=>e.Value!=input.Local[e.Key]).ToDictionary();
        if(fixedPins.Length>0){var checkedLocks=ContactSolver.Solve(scene,input,setup,changes,fixedPins);if(checkedLocks.Value is null)return Fail("SOFT_PIN_FIXED_CONFLICT","The requested edit conflicts with an authored fixed lock. No fixed anchor was weakened.");}
        return new(new(changes,maximumError,maximumRotation,0),diagnostics.ToArray(),[]);
    }
    static Transform Align(Scene scene,EvaluatedPose pose,int bone,Vector3 from,Vector3 to)
    {
        Quaternion delta=AnimationControls.DirectionRotation(from,to);
        return PoseGeometry.WorldRotation(scene,pose,bone,Quaternion.Concatenate(PoseGeometry.Rotation(pose.World[bone]),delta));
    }
}
