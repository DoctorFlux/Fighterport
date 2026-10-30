using System.Numerics;

namespace FighterPort.Core;

public static partial class ReviewedContactSets
{
    static HashSet<int> EditableContactBones(Scene scene,ControlRigSetup rig,ReviewedContactContext context)
    {
        var rows=context.Ranges.Where(r=>r.Reviewed).ToArray();
        var result=rows.SelectMany(row=>{var chain=rig.Chains.Single(c=>c.Reviewed&&c.Effector==row.Bone);return new[]{chain.Root,chain.Middle,chain.Effector};}).ToHashSet();
        result.UnionWith(rows.SelectMany(r=>ReviewedContactShapeBones(scene,rig,r)));
        int body=ReviewedContactBody(scene,rig);if(body>=0)result.Add(body);
        result.ExceptWith(AnimationControls.ProtectedMotionBones(scene,rig).Concat(rig.PreservedHelperBones)
            .Concat(context.ProtectedBones.Select(name=>Array.FindIndex(scene.Bones,b=>b.Name==name))));
        return result;
    }
    static bool EarlierBodyActive(ReviewedContactContext context,double frame)=>context.EarlierBodyCorrectionStart is {} start&&frame>start&&
        frame<context.Ranges.Where(r=>r.Reviewed).Max(r=>r.End+r.Blend);
    static Transform[] EarlierBodyPose(Scene scene,AnimationClip master,ControlRigSetup rig,ReviewedContactContext context,
        double frame,EvaluatedPose original,Transform[] entry,double entryFrame)
    {
        double start=context.EarlierBodyCorrectionStart!.Value;
        float weight=(float)Math.Clamp((frame-start)/(entryFrame-start),0,1);weight=weight*weight*(3-2*weight);
        var atEntry=ContactPose(scene,master,entryFrame);var result=original.Local.ToArray();
        var locked=OriginalProtection(scene,context,frame,rig);
        foreach(int bone in EditableContactBones(scene,rig,context).Where(b=>!locked.Contains(b)))
        {
            var rotation=ContactEulerNear(entry[bone].Rotation,atEntry.Local[bone].Rotation)-atEntry.Local[bone].Rotation;
            result[bone]=result[bone] with{Rotation=result[bone].Rotation+rotation*weight,
                Translation=result[bone].Translation+(entry[bone].Translation-atEntry.Local[bone].Translation)*weight};
        }
        return result;
    }
    static EvaluatedPose ContactTemporalSeed(Scene scene,EvaluatedPose original,EvaluatedPose priorOriginal,Transform[] prior,
        HashSet<int> allowed,IReadOnlyDictionary<int,Transform> locks)
    {
        var values=original.Local.Select((value,i)=>(value,i)).ToDictionary(p=>p.i,p=>p.value);
        foreach(int bone in allowed.Where(b=>!locks.ContainsKey(b)))
        {
            var delta=ContactEulerNear(prior[bone].Rotation,priorOriginal.Local[bone].Rotation)-priorOriginal.Local[bone].Rotation;
            values[bone]=original.Local[bone] with{Rotation=original.Local[bone].Rotation+delta,
                Translation=original.Local[bone].Translation+prior[bone].Translation-priorOriginal.Local[bone].Translation};
        }
        return AnimationService.Evaluate(scene,null,0,values);
    }
    static EvaluatedPose? BodyClearanceTrial(Scene scene,ControlRigSetup rig,ReviewedContactContext context,EvaluatedPose pose,
        IReadOnlyDictionary<int,Transform> locks,ContactPin[] goals,double frame,float bound,Vector3 normal,
        IReadOnlyDictionary<string,FullFootWitness> geometry,ref long skinWork)
    {
        int body=ReviewedContactBody(scene,rig);if(body<0||locks.ContainsKey(body))return null;
        double lower=0,upper=double.PositiveInfinity;bool possible=true;
        double Factor(SkinSurfacePoint point)=>Enumerable.Range(0,point.InfluenceCount).Where(i=>point.WeightAt(i)>0&&PoseGeometry.Ancestor(scene,body,(int)point.JointAt(i))).Sum(i=>(double)point.WeightAt(i));
        void Sphere(Vector3 error,double factor)
        {
            double along=Vector3.Dot(error,normal),side=Math.Max(0,(double)error.LengthSquared()-along*along),remaining=(double)bound*bound-side;
            if(remaining<0){possible=false;return;}
            if(factor<=0){if(error.Length()>bound)possible=false;return;}
            double radius=Math.Sqrt(remaining);lower=Math.Max(lower,(-radius-along)/factor);upper=Math.Min(upper,(radius-along)/factor);
        }
        foreach(var goal in goals)
        {
            if(goal.SurfaceRegion.Length>0||goal.PlaneNormal is not null||goal.AllowedSurface is not null)return null;
            double factor=goal.SurfacePoints.Length>0?goal.SurfacePoints.Average(Factor):PoseGeometry.Ancestor(scene,body,goal.Bone)?1:0;
            Sphere(ContactGeometry.Point(goal,pose)-ContactGeometry.Target(goal,pose),factor);
        }
        foreach(var row in context.Ranges.Where(r=>r.Reviewed&&frame>r.Start-r.Blend&&frame<r.End+r.Blend))
        {
            var full=geometry[row.Id];skinWork+=full.Region.Length+full.Sole.Length;
            if(skinWork>MaximumKeys)throw new InvalidOperationException("Body-clearance trial exhausted the unchanged full-skin verification budget.");
            foreach(var point in full.Region)
            {
                double factor=Factor(point),need=context.Surface.SourceFloor!.Value-Vector3.Dot(ContactGeometry.Point(point,pose),normal)-bound;
                if(factor>0)lower=Math.Max(lower,need/factor);else if(need>0)possible=false;
            }
            if(frame>=row.Start&&frame<=row.End)for(int i=0;i<full.Sole.Length;i++)Sphere(ContactGeometry.Point(full.Sole[i],pose)-full.Anchors[i],Factor(full.Sole[i]));
        }
        if(!possible||!double.IsFinite(lower)||lower>upper)return null;
        double amount=lower+Math.Min(bound*.05,(upper-lower)*.5);
        if(!double.IsFinite(amount)||amount<=0)return null;
        var shift=normal*(float)amount;int parent=scene.Bones[body].Parent;
        if(parent>=0){if(!Matrix4x4.Invert(pose.World[parent],out var inverse))return null;shift=Vector3.TransformNormal(shift,inverse);}
        var local=pose.Local.Select((value,i)=>(value,i)).ToDictionary(p=>p.i,p=>p.value);
        local[body]=local[body] with{Translation=local[body].Translation+shift};
        return AnimationService.Evaluate(scene,null,0,local);
    }
}
