using System.Numerics;

namespace FighterPort.Core;

public static partial class ReviewedContactSets
{
    static int[] ReviewedContactShapeBones(Scene scene,ControlRigSetup rig,ReviewedContactRange row)=>rig.BoneRoles
        .Where(r=>r.Reviewed&&r.SemanticRole=="Toe."+row.Role.Substring("Foot.".Length)&&PoseGeometry.Ancestor(scene,row.Bone,r.Bone))
        .Select(r=>r.Bone).Distinct().ToArray();
    static int ReviewedContactBody(Scene scene,ControlRigSetup rig)
    {
        int body=AnimationControls.ResolveBodyBone(scene,rig);
        return body>=0&&rig.BoneRoles.Any(r=>r.Reviewed&&r.SemanticRole=="Hips"&&r.Bone==body)?body:-1;
    }
    // A deterministic starting pose, not a replacement animation or a relaxed
    // solve. Reviewed anchor poses supply a consistent geometric branch for large
    // lying-to-standing changes. Every result still solves the SAME contacts,
    // preserves the SAME locks, and uses the original pose fidelity objective.
    static EvaluatedPose ContactAnchorSeed(Scene scene,AnimationClip master,ControlRigSetup rig,ReviewedContactRange[] rows,
        double frame,EvaluatedPose original,IReadOnlyDictionary<int,Transform> locks,ContactPin[] goals,bool fullAnchor=false)
    {
        float Weight(ReviewedContactRange r)
        {
            float t=frame<r.Start?(float)((frame-r.Start+r.Blend)/r.Blend):frame>r.End?(float)((r.End+r.Blend-frame)/r.Blend):1;
            t=Math.Clamp(t,0,1);return t*t*(3-2*t);
        }
        var active=rows.Select(r=>(Row:r,Weight:fullAnchor?1:Weight(r))).Where(p=>p.Weight>0).OrderBy(p=>p.Row.AnchorFrame).ToArray();
        if(active.Length==0)return original;
        double time=active.Sum(p=>p.Row.AnchorFrame*(double)p.Weight)/active.Sum(p=>(double)p.Weight);
        var reference=ContactPose(scene,master,time);var local=original.Local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t);
        var pose=original;
        void Rotate(int bone,float weight)
        {
            if(locks.ContainsKey(bone))return;
            local[bone]=PoseGeometry.WorldRotation(scene,pose,bone,Quaternion.Slerp(PoseGeometry.Rotation(original.World[bone]),PoseGeometry.Rotation(reference.World[bone]),weight));
            pose=AnimationService.Evaluate(scene,null,0,local);
        }
        int body=ReviewedContactBody(scene,rig);if(body>=0)Rotate(body,active.Max(p=>p.Weight));
        foreach(var chain in rig.Chains.Where(c=>c.Reviewed&&active.Any(p=>p.Row.Bone==c.Effector)).OrderBy(c=>c.Root))
        {
            float weight=active.Single(p=>p.Row.Bone==chain.Effector).Weight;
            foreach(int bone in new[]{chain.Root,chain.Middle,chain.Effector})Rotate(bone,weight);
        }
        if(body>=0&&!locks.ContainsKey(body))
        {
            var correction=goals.Where(p=>active.Any(a=>a.Row.Bone==p.Bone)).Select(p=>ContactGeometry.Target(p,pose)-ContactGeometry.Point(p,pose)).ToArray();
            var shift=correction.Aggregate(Vector3.Zero,(a,p)=>a+p)/correction.Length;
            int parent=scene.Bones[body].Parent;
            if(parent>=0){if(!Matrix4x4.Invert(pose.World[parent],out var inverse))throw new InvalidDataException("Singular reviewed body parent.");shift=Vector3.TransformNormal(shift,inverse);}
            local[body]=local[body] with{Translation=local[body].Translation+shift};pose=AnimationService.Evaluate(scene,null,0,local);
        }
        return pose;
    }
}
