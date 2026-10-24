namespace FighterPort.Core;

// Inferred/optional relationships guide the fit without being promoted into
// authored exact pins. Respect their allowed surface/slide/roll freedoms;
// measuring raw world point distance would incorrectly freeze those freedoms.
public static class ContactObservationObjective
{
    public const string Version="mode-aware-inferred-skin-objective-1";
    public static double[] Residuals(IEnumerable<ContactPin> observations,EvaluatedPose pose,float scale)
    {
        if(!float.IsFinite(scale)||scale<=0)throw new ArgumentException("Contact observations need a finite positive geometry scale.");
        var result=new List<double>();double weight=Math.Sqrt(TemporalRetargetSolver.InferredSupportObjectiveWeight);
        void Position(System.Numerics.Vector3 value){result.Add(value.X/scale*weight);result.Add(value.Y/scale*weight);result.Add(value.Z/scale*weight);}
        foreach(var pin in observations.Where(p=>p.SoftSupport&&!p.RequiredSupport&&!p.ClearanceOnly))
        {
            Position(ContactGeometry.Residual(pin,pose));
            if(pin.AllowedSurface is not null&&ContactGeometry.Normal(pin,pose) is null)Position(ContactGeometry.AllowedSurfaceResidual(pin,pose));
            if(pin.HoldRotation)
            {
                var r=PreparedMotionAction.RotationVector(PoseGeometry.Rotation(pose.World[pin.Bone]),ContactGeometry.TargetRotation(pin,pose));
                result.Add(r.X*weight);result.Add(r.Y*weight);result.Add(r.Z*weight);
            }
        }
        return result.ToArray();
    }
    public static double Cost(IEnumerable<ContactPin> observations,EvaluatedPose pose,float scale)=>Residuals(observations,pose,scale).Sum(v=>v*v);
}
