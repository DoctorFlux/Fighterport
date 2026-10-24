using System.Numerics;

namespace FighterPort.Core;

// The same prepared target relationships constrain temporal poses and native
// curves. Full evaluated skin is still checked independently after fitting.
public static class TargetMotionConstraints
{
    internal readonly record struct ClearanceCut(int Index,Vector3 Normal,float Height,bool Active);
    // Optional source-channel recovery is not a feasibility restoration step.
    // A larger error elsewhere must not conceal a new penetration or a worsened
    // grip at this sample. Compare every independently bounded measurement,
    // retaining the actual vector norm and each material skin point's identity.
    public static bool PreservesRequiredMeasurements(TargetMotionPlan plan,double frame,EvaluatedPose before,EvaluatedPose after)
    {
        using var old=Measurements(before).GetEnumerator();
        using var next=Measurements(after).GetEnumerator();
        while(old.MoveNext())
        {
            if(!next.MoveNext()||!double.IsFinite(old.Current)||!double.IsFinite(next.Current)||next.Current>Math.Max(1,old.Current)+1e-9)return false;
        }
        return !next.MoveNext();

        IEnumerable<double> Measurements(EvaluatedPose pose)
        {
            foreach(var pin in plan.Pins(frame).Where(p=>!p.SoftSupport||p.RequiredSupport))
            {
                yield return ContactGeometry.Residual(pin,pose).Length()/plan.PositionBound;
                if(pin.AllowedSurface is not null)yield return ContactGeometry.AllowedSurfaceResidual(pin,pose).Length()/plan.PositionBound;
                if(pin.HoldRotation)
                {
                    var delta=Quaternion.Normalize(PoseGeometry.Rotation(pose.World[pin.Bone])*Quaternion.Inverse(ContactGeometry.TargetRotation(pin,pose)));
                    yield return 2*Math.Atan2(new Vector3(delta.X,delta.Y,delta.Z).Length(),Math.Abs(delta.W))/RetargetAcceptance.RotationRadians;
                }
            }
            if(!plan.HasGround(frame)||plan.Clearance is not {} plane)yield break;
            var legs=plan.Rig.Binding.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).Select(c=>plan.Rig.ChainLength(c.Role)).ToArray();
            float depthBound=(legs.Length>0?legs.Average():plan.Rig.CharacteristicLength)*RetargetAcceptance.PenetrationLegFraction;
            foreach(var point in plan.Rig.SkinPoints.Concat(plane.Points))yield return plane.Surface.Penetration(ContactGeometry.Point(point,pose))/depthBound;
            if(plan.Grounding is not {} ground||!SurfaceContacts.BodySupportedAt(ground.BodySupportPhases,ground.BodySupportedThroughout,ground.BodySupportedAtClosing,plan.SourceFrame(frame),plan.AuthenticSourceSamples))yield break;
            if(plan.BodySupportPoints.Length==0)throw new NotSupportedException("Body support requires actual reviewed skin geometry.");
            yield return (plane.Edge is not null?plan.BodySupportPoints.Min(p=>plane.Surface.TopOffset(ContactGeometry.Point(p,pose)).Length()):Math.Abs(plan.BodySupportPoints.Min(p=>Vector3.Dot(ContactGeometry.Point(p,pose),plane.Normal))-plane.Height))/plan.PositionBound;
        }
    }
    // A feasibility measurement, independent of the sparse/differentiated rows
    // used to find a pose. A per-component residual test admits diagonal errors
    // larger than the unchanged vector bound; sparse proxies may miss skin.
    // Returning <=1 establishes these sampled constraints only, never native
    // curves, contact-mode coverage, action fidelity or whole-animation acceptance.
    public static double MaximumNormalizedViolation(TargetMotionPlan plan,double frame,EvaluatedPose pose)
    {
        if(!float.IsFinite(plan.PositionBound)||plan.PositionBound<=0)return double.PositiveInfinity;
        double maximum=0;
        void Include(double value)=>maximum=!double.IsFinite(value)?double.PositiveInfinity:Math.Max(maximum,value);
        foreach(var pin in plan.Pins(frame).Where(p=>!p.SoftSupport||p.RequiredSupport))
        {
            Include(ContactGeometry.Residual(pin,pose).Length()/plan.PositionBound);
            if(pin.AllowedSurface is not null)Include(ContactGeometry.AllowedSurfaceResidual(pin,pose).Length()/plan.PositionBound);
            if(pin.HoldRotation)
            {
                var delta=Quaternion.Normalize(PoseGeometry.Rotation(pose.World[pin.Bone])*Quaternion.Inverse(ContactGeometry.TargetRotation(pin,pose)));
                double angle=2*Math.Atan2(new Vector3(delta.X,delta.Y,delta.Z).Length(),Math.Abs(delta.W));
                Include(angle/RetargetAcceptance.RotationRadians);
            }
        }
        if(plan.HasGround(frame)&&plan.Clearance is {} plane)
        {
            var legs=plan.Rig.Binding.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).Select(c=>plan.Rig.ChainLength(c.Role)).ToArray();
            float depthBound=(legs.Length>0?legs.Average():plan.Rig.CharacteristicLength)*RetargetAcceptance.PenetrationLegFraction;
            if(!float.IsFinite(depthBound)||depthBound<=0)return double.PositiveInfinity;
            // Rig.SkinPoints is the full retained mesh, not the active proxy
            // subset. ContactGeometry.Point also reads evaluated morph geometry.
            foreach(var point in plan.Rig.SkinPoints.Concat(plane.Points))
                Include(plane.Surface.Penetration(ContactGeometry.Point(point,pose))/depthBound);
            if(plan.Grounding is {} ground&&SurfaceContacts.BodySupportedAt(ground.BodySupportPhases,ground.BodySupportedThroughout,ground.BodySupportedAtClosing,plan.SourceFrame(frame),plan.AuthenticSourceSamples))
            {
                if(plan.BodySupportPoints.Length==0)throw new NotSupportedException("Body support requires actual reviewed hip/chest skin.");
                if(plane.Edge is not null)
                    Include(plan.BodySupportPoints.Min(p=>plane.Surface.TopOffset(ContactGeometry.Point(p,pose)).Length())/plan.PositionBound);
                else
                    Include(Math.Abs(plan.BodySupportPoints.Min(p=>Vector3.Dot(ContactGeometry.Point(p,pose),plane.Normal))-plane.Height)/plan.PositionBound);
            }
        }
        return maximum;
    }
    // Freeze the actual active inequality and corner face for differentiation.
    // Differentiating clamped penetration after a large perturbation can instead
    // measure a different face or a newly activated point. Candidate acceptance
    // always evaluates the original inequalities against the actual skin.
    internal static ClearanceCut[] LinearizationCuts(TargetMotionPlan plan,EvaluatedPose pose,int[] indices)
    {
        if(plan.Clearance is not {} plane)return [];
        return indices.Select(index=>
        {
            var point=ContactGeometry.Point(plane.Points[index],pose);var normal=plane.Normal;float height=plane.Height,distance=Vector3.Dot(point,normal)-height;
            if(plane.Edge is {} edge&&edge.Offset-Vector3.Dot(point,edge.Inward)>distance){normal=-edge.Inward;height=-edge.Offset;distance=Vector3.Dot(point,normal)-height;}
            return new ClearanceCut(index,normal,height,distance<0);
        }).ToArray();
    }
    internal static int[] ActiveClearance(TargetMotionPlan plan,double frame,EvaluatedPose pose)
        =>!plan.HasGround(frame)||plan.Clearance is not {} plane?[]:plane.Points.Select((p,i)=>(Index:i,Distance:plane.Surface.Clearance(ContactGeometry.Point(p,pose)))).OrderBy(p=>p.Distance).ThenBy(p=>p.Index).Take(24).Select(p=>p.Index).ToArray();
    internal static ContactPin[] LinearizationPins(TargetMotionPlan plan,double frame,EvaluatedPose pose)=>plan.Pins(frame).Select(pin=>
    {
        if(pin.SurfaceRegion.Length>0&&ContactGeometry.AllowedSurface(pin,pose) is {} support)
        {
            var sample=pin.SurfaceRegion.MinBy(p=>support.TopOffset(ContactGeometry.Point(p,pose)).LengthSquared())!;
            return pin with{SurfaceRegion=[],SurfacePoints=[sample]};
        }
        if(pin.EdgeInward is not {} inward||pin.PlaneNormal is not {} up||pin.SurfaceRegion.Length==0)return pin;
        var goal=ContactGeometry.Target(pin,pose);
        var point=pin.SurfaceRegion.MinBy(p=>{var delta=ContactGeometry.Point(p,pose)-goal;return MathF.Pow(Vector3.Dot(delta,up),2)+MathF.Pow(Vector3.Dot(delta,inward),2);})!;
        return pin with{SurfaceRegion=[],SurfacePoints=[point]};
    }).ToArray();
    internal static SkinSurfacePoint? LinearizationBodyPoint(TargetMotionPlan plan,double frame,EvaluatedPose pose)
    {
        if(!plan.HasGround(frame)||plan.Clearance is not {} plane||plan.Grounding is not {} ground||!SurfaceContacts.BodySupportedAt(ground.BodySupportPhases,ground.BodySupportedThroughout,ground.BodySupportedAtClosing,plan.SourceFrame(frame),plan.AuthenticSourceSamples))return null;
        return plane.Edge is not null?plan.BodySupportPoints.MinBy(p=>plane.Surface.TopOffset(ContactGeometry.Point(p,pose)).LengthSquared()):plan.BodySupportPoints.MinBy(p=>Vector3.Dot(ContactGeometry.Point(p,pose),plane.Normal));
    }
    internal static double[] Residual(TargetMotionPlan plan,double frame,EvaluatedPose pose,int[]? clearanceIndices=null,ContactPin[]? pins=null,ClearanceCut[]? cuts=null,SkinSurfacePoint? bodyPoint=null)
    {
        var residual=new List<double>();
        foreach(var pin in (pins??plan.Pins(frame)).Where(p=>!p.SoftSupport||p.RequiredSupport))
        {
            var p=ContactGeometry.Residual(pin,pose)/plan.PositionBound;residual.Add(p.X);residual.Add(p.Y);residual.Add(p.Z);
            if(pin.AllowedSurface is not null&&ContactGeometry.Normal(pin,pose) is null){var bound=ContactGeometry.AllowedSurfaceResidual(pin,pose)/plan.PositionBound;residual.Add(bound.X);residual.Add(bound.Y);residual.Add(bound.Z);}
            if(pin.HoldRotation)
            {
                var delta=Quaternion.Normalize(PoseGeometry.Rotation(pose.World[pin.Bone])*Quaternion.Inverse(ContactGeometry.TargetRotation(pin,pose)));if(delta.W<0)delta=new(-delta.X,-delta.Y,-delta.Z,-delta.W);
                var vector=new Vector3(delta.X,delta.Y,delta.Z);float length=vector.Length(),angle=2*MathF.Atan2(length,Math.Abs(delta.W));var r=length<1e-9f?Vector3.Zero:vector*(angle/length/RetargetAcceptance.RotationRadians);residual.Add(r.X);residual.Add(r.Y);residual.Add(r.Z);
            }
        }
        if(plan.HasGround(frame)&&plan.Clearance is {} plane)
        {
            var legs=plan.Rig.Binding.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).Select(c=>plan.Rig.ChainLength(c.Role)).ToArray();float depthBound=(legs.Length>0?legs.Average():plan.Rig.CharacteristicLength)*RetargetAcceptance.PenetrationLegFraction;
            if(cuts is not null)
                foreach(var cut in cuts)residual.Add(cut.Active?(Vector3.Dot(ContactGeometry.Point(plane.Points[cut.Index],pose),cut.Normal)-cut.Height)/depthBound:0);
            else foreach(int index in clearanceIndices??Enumerable.Range(0,plane.Points.Length).ToArray())residual.Add(-plane.Surface.Penetration(ContactGeometry.Point(plane.Points[index],pose))/depthBound);
            if(plan.Grounding is {} ground&&SurfaceContacts.BodySupportedAt(ground.BodySupportPhases,ground.BodySupportedThroughout,ground.BodySupportedAtClosing,plan.SourceFrame(frame),plan.AuthenticSourceSamples))
            {
                if(plan.BodySupportPoints.Length==0)throw new NotSupportedException("Body support requires actual reviewed hip/chest skin.");
                if(plane.Edge is not null)
                {
                    var offset=(bodyPoint is not null?plane.Surface.TopOffset(ContactGeometry.Point(bodyPoint,pose)):plan.BodySupportPoints.Select(p=>plane.Surface.TopOffset(ContactGeometry.Point(p,pose))).MinBy(v=>v.LengthSquared()))/plan.PositionBound;
                    residual.Add(offset.X);residual.Add(offset.Y);residual.Add(offset.Z);
                }
                else residual.Add(((bodyPoint is not null?Vector3.Dot(ContactGeometry.Point(bodyPoint,pose),plane.Normal):plan.BodySupportPoints.Min(p=>Vector3.Dot(ContactGeometry.Point(p,pose),plane.Normal)))-plane.Height)/plan.PositionBound);
            }
        }
        return residual.ToArray();
    }
}
