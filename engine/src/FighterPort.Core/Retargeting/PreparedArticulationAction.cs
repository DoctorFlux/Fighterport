using System.Numerics;

namespace FighterPort.Core;

// Surface error alone cannot see an elbow/knee fold that returns the skin's
// endpoint to the same place. Integrate the mapped native articulated paths as
// a separate measure, weighted by actual rest arc length, never bone count or
// a named humanoid subset. Coincident native helper names add no path mass.
public sealed class PreparedArticulationAction
{
    public const string Version="mapped-native-edge-action-1";
    readonly record struct Edge(int Parent,int Child,int Origin,double Weight);
    readonly Edge[] edges;
    readonly Vector3[] reference;
    readonly int count;
    public int EdgeCount=>edges.Length;
    public int ResidualCount=>edges.Length*6;
    PreparedArticulationAction(Edge[] edges,Vector3[] reference,int count)
    {this.edges=edges;this.reference=reference;this.count=count;}
    public static PreparedArticulationAction Create(RigDescriptor rig,RetargetMappingReport mapping,EvaluatedPose reference)
    {
        int count=rig.Scene.Bones.Length;
        if(reference.World.Length!=count)throw new ArgumentException("Articulation reference belongs to a different native graph.");
        var mapped=mapping.Bones.Select(p=>p.Target).ToHashSet();
        if(mapped.Any(b=>b<0||b>=count))throw new ArgumentException("Articulation mapping references a missing native bone.");
        int root=rig.Binding.Roles["RootMotion"],body=rig.Binding.Roles.GetValueOrDefault("Hips",root);
        var paths=new List<(int Parent,int Child,int Origin,double Length)>();
        foreach(int bone in mapped.OrderBy(b=>rig.CanonicalBoneRanks[b]))
        {
            int child=bone,parent=rig.Scene.Bones[child].Parent;double length=0;
            while(parent>=0)
            {
                length+=Vector3.Distance(rig.Rest.World[child].Translation,rig.Rest.World[parent].Translation);
                if(mapped.Contains(parent))break;child=parent;parent=rig.Scene.Bones[parent].Parent;
            }
            if(parent<0||length<=0)continue;
            int origin=PoseGeometry.Ancestor(rig.Scene,body,parent)?body:root;
            paths.Add((parent,bone,origin,length));
        }
        double total=paths.Sum(p=>p.Length);
        var edges=paths.Select(p=>new Edge(p.Parent,p.Child,p.Origin,Math.Sqrt(p.Length/(2*total))/rig.CharacteristicLength)).ToArray();
        return new(edges,reference.World.Select(m=>m.Translation).ToArray(),count);
    }
    public double[] Residuals(EvaluatedPose pose)
    {
        if(pose.World.Length!=count)throw new ArgumentException("Articulation candidate belongs to a different native graph.");
        var result=new double[ResidualCount];int row=0;
        foreach(var edge in edges)
        {
            var movement=pose.World[edge.Origin].Translation-reference[edge.Origin];
            var a=pose.World[edge.Parent].Translation-reference[edge.Parent]-movement;
            var b=pose.World[edge.Child].Translation-reference[edge.Child]-movement;
            foreach(double t in new[]{.5-1/(2*Math.Sqrt(3)),.5+1/(2*Math.Sqrt(3))})
                for(int axis=0;axis<3;axis++)result[row++]=edge.Weight*((1-t)*a[axis]+t*b[axis]);
        }
        if(result.Any(v=>!double.IsFinite(v)))throw new InvalidDataException("Articulation candidate has nonfinite path geometry.");
        return result;
    }
    public double Cost(EvaluatedPose pose)=>Residuals(pose).Sum(x=>x*x);
    // Independent diagnostic: positions/skin alone can miss a native link
    // twist. This is not an accepted optimization metric or an Apply gate;
    // the attempted coupled objective failed existing fractional contacts.
    public double NativeLinkFrameCost(EvaluatedPose pose,EvaluatedPose sourceReference)
    {
        if(pose.World.Length!=count||sourceReference.World.Length!=count)throw new ArgumentException("Native frame evidence belongs to another graph.");
        double measure=edges.Sum(e=>e.Weight*e.Weight);
        return measure==0?0:edges.Sum(e=>e.Weight*e.Weight/measure*
            PreparedMotionAction.RotationVector(PoseGeometry.Rotation(pose.World[e.Parent]),PoseGeometry.Rotation(sourceReference.World[e.Parent])).LengthSquared());
    }
}

// Global design weights are fixed before fixture tuning. Required contacts
// remain separate constraints. This is not an enabled Motion Fidelity slider.
internal sealed class PreparedTrajectoryAction
{
    public const string Version="source-geometry-before-inferred-support-3";
    public const double SkinWeight=1,PathWeight=1;
    readonly PreparedMotionAction skin;
    readonly PreparedArticulationAction path;
    readonly ContactPin[] observations;readonly float scale;
    PreparedTrajectoryAction(PreparedMotionAction skin,PreparedArticulationAction path,ContactPin[] observations,float scale){this.skin=skin;this.path=path;this.observations=observations;this.scale=scale;}
    public string VisibilityPolicy=>skin.VisibilityPolicy+"; mapped native rest-arc quadrature; separate mode-aware soft observations, no authored-lock relaxation";
    public static PreparedTrajectoryAction Create(RigDescriptor rig,RetargetMappingReport mapping,EvaluatedPose reference)
        =>new(PreparedMotionAction.Create(rig,mapping,reference),PreparedArticulationAction.Create(rig,mapping,reference),[],rig.CharacteristicLength);
    public static PreparedTrajectoryAction Create(TargetMotionPlan plan,double frame,EvaluatedPose reference)
        =>new(PreparedMotionAction.Create(plan.Rig,plan.Baseline.Mapping,reference),PreparedArticulationAction.Create(plan.Rig,plan.Baseline.Mapping,reference),
            plan.Pins(frame).Where(p=>p.SoftSupport&&!p.RequiredSupport).ToArray(),plan.Rig.CharacteristicLength);
    public double[] GeometricResiduals(EvaluatedPose pose)=>skin.Residuals(pose).Select(v=>v*Math.Sqrt(SkinWeight))
        .Concat(path.Residuals(pose).Select(v=>v*Math.Sqrt(PathWeight))).ToArray();
    public double[] Residuals(EvaluatedPose pose)=>GeometricResiduals(pose).Concat(ContactObservationObjective.Residuals(observations,pose,scale)).ToArray();
    public double GeometricCost(EvaluatedPose pose)=>SkinWeight*skin.Cost(pose)+PathWeight*path.Cost(pose);
    public double ObservationCost(EvaluatedPose pose)=>ContactObservationObjective.Cost(observations,pose,scale);
    public double Cost(EvaluatedPose pose)=>GeometricCost(pose)+ObservationCost(pose);
}
