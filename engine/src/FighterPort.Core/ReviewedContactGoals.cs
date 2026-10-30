using System.Numerics;

namespace FighterPort.Core;

public static partial class ReviewedContactSets
{
    static ContactPin ContactGoal(Scene scene,AnimationClip master,ControlRigSetup rig,ReviewedContactContext context,
        ReviewedContactRange row,double frame,EvaluatedPose original,Vector3 normal,float weight,SkinSurfacePoint[] solverRegion)
    {
        var point=row.Patch.Select(p=>ContactGeometry.Point(p,original)).Aggregate(Vector3.Zero,(a,p)=>a+p)/row.Patch.Length;
        var center=Vector3.Lerp(point,row.Anchor,weight);
        var rotation=Quaternion.Slerp(PoseGeometry.Rotation(original.World[row.Bone]),row.Orientation,weight);
        if(weight<1)
        {
            // Establish/release positions are a clearance-aware approach, not
            // an exact straight line through the floor. Hold anchors are never
            // moved. The same rule is replayed after native serialization.
            var values=original.Local.Select((v,i)=>(v,i)).ToDictionary(p=>p.i,p=>p.v);
            var anchor=ContactPose(scene,master,row.AnchorFrame);
            var fixedBones=OriginalProtection(scene,context,frame,rig);
            var motion=AnimationControls.ProtectedMotionBones(scene,rig).ToHashSet();
            foreach(int bone in ReviewedContactShapeBones(scene,rig,row).Where(b=>!fixedBones.Contains(b)&&!motion.Contains(b)&&!rig.PreservedHelperBones.Contains(b)&&!context.ProtectedBones.Contains(scene.Bones[b].Name)))
            {
                var a=original.Local[bone];var b=anchor.Local[bone];if(a.Rotation==b.Rotation)continue;
                var shape=Quaternion.Slerp(PoseGeometry.Rotation(a.Matrix),PoseGeometry.Rotation(b.Matrix),weight);
                values[bone]=a with{Rotation=AnimationControls.Euler(Matrix4x4.CreateFromQuaternion(shape),a.Rotation)};
            }
            var preview=AnimationService.Evaluate(scene,null,0,values);
            var chain=rig.Chains.Single(c=>c.Reviewed&&c.Effector==row.Bone);
            // Mixed foot/knee skin weights make sole clearance depend on the
            // complete reviewed chain, not only the effector rotation.
            foreach(int bone in new[]{chain.Root,chain.Middle})
            {
                if(fixedBones.Contains(bone)||motion.Contains(bone)||rig.PreservedHelperBones.Contains(bone)||context.ProtectedBones.Contains(scene.Bones[bone].Name))continue;
                var desired=Quaternion.Slerp(PoseGeometry.Rotation(original.World[bone]),PoseGeometry.Rotation(anchor.World[bone]),weight);
                values[bone]=PoseGeometry.WorldRotation(scene,preview,bone,desired);preview=AnimationService.Evaluate(scene,null,0,values);
            }
            values[row.Bone]=PoseGeometry.WorldRotation(scene,preview,row.Bone,rotation);preview=AnimationService.Evaluate(scene,null,0,values);
            var shapeCenter=row.Patch.Select(p=>ContactGeometry.Point(p,preview)).Aggregate(Vector3.Zero,(a,p)=>a+p)/row.Patch.Length;
            float minimum=solverRegion.Min(p=>Vector3.Dot(ContactGeometry.Point(p,preview)-shapeCenter,normal));
            center+=normal*Math.Max(0,context.Surface.SourceFloor!.Value-Vector3.Dot(center,normal)-minimum);
        }
        return new(Guid.ParseExact(row.Id,"N"),row.Role,row.Bone,Vector3.Zero,center,rotation,true,"Reviewed static floor")
            {SurfacePoints=row.Patch,Clearance=new(normal,context.Surface.SourceFloor!.Value,solverRegion)};
    }
}
