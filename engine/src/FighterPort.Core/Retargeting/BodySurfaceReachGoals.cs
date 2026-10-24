using System.Numerics;

namespace FighterPort.Core;

// First moments retain ALL represented influences. No surrogate deform rig or
// four-influence reweighting is introduced by averaging a material region.
public sealed class NativeSurfaceMoment
{
    readonly (int Bone,Vector4 Moment)[] moments;
    public int Terms=>moments.Length;
    public NativeSurfaceMoment(RigDescriptor rig,SkinSurfacePoint[] points)
    {
        if(points.Length==0)throw new ArgumentException("A surface moment needs original material points.");
        var sums=new Dictionary<int,Vector4>();var inverses=rig.Rest.World.Select(w=>Matrix4x4.Invert(w,out var inverse)?inverse:throw new NotSupportedException("Surface moments require invertible native rest transforms.")).ToArray();
        foreach(var p in points)for(int slot=0;slot<4;slot++)if(p.Weights[slot]>0)
        {
            int bone=(int)p.Joints[slot];
            var value=Vector4.Transform(new Vector4(p.Position,1),inverses[bone])*(p.Weights[slot]/points.Length);
            sums[bone]=sums.GetValueOrDefault(bone)+value;
        }
        moments=sums.OrderBy(p=>rig.CanonicalBoneRanks[p.Key]).Select(p=>(p.Key,p.Value)).ToArray();
    }
    public Vector3 Evaluate(EvaluatedPose pose)=>Sum(b=>pose.World[b]);
    public Vector3 Derivative(NativePoseDifferentials differential,int variable)=>Sum(b=>differential.World(b,variable));
    Vector3 Sum(Func<int,Matrix4x4> matrix)
    {
        var value=Vector4.Zero;foreach(var (bone,moment) in moments)value+=Vector4.Transform(moment,matrix(bone));
        return new(value.X,value.Y,value.Z);
    }
}

public record BodySurfaceReachObservation(int SourceEndpoint,int TargetEndpoint,int TargetAnchor,
    NativeSurfaceMoment Endpoint,NativeSurfaceMoment Anchor,Vector3 DesiredLocal,double Weight,float Length)
{
    public Vector3 RequestedLocal {get;init;}
    public float RequestedReach {get;init;}
    public float AvailableReach {get;init;}
    public bool ReachAdapted {get;init;}
}

// Original geometry-derived, target-specific action observations. Relative
// surface directions/separations are distinct from scene contacts or world pins.
public sealed class BodySurfaceReachGoals
{
    public const string Version="native-surface-relative-reach-2";
    public const double Prior=.2;
    readonly RigDescriptor source,target;
    readonly Pair[] pairs;
    readonly CancellationToken cancellation;
    sealed record Pair(BodyRelativeReachBinding Chain,int SourceAnchor,int TargetAnchor,SkinSurfacePoint[] SourceEnd,
        SkinSurfacePoint[] SourceBody,SkinSurfacePoint[] TargetEnd,SkinSurfacePoint[] TargetBody,
        NativeSurfaceMoment EndMoment,NativeSurfaceMoment BodyMoment,Matrix4x4 Conversion);
    public int Relationships=>pairs.Length;
    public long MomentTerms=>pairs.Sum(p=>(long)p.EndMoment.Terms+p.BodyMoment.Terms);
    public long MaterialPoints=>pairs.Sum(p=>(long)p.SourceEnd.Length+p.SourceBody.Length+p.TargetEnd.Length+p.TargetBody.Length);
    public long WorkspaceTerms=>pairs.Sum(p=>(long)p.Chain.TargetBones.Length+4);
    public BodySurfaceReachGoals(RigDescriptor source,RigDescriptor target,RetargetMappingReport mapping,
        BodyRelativeReachBinding[] chains,CancellationToken cancellation=default)
    {
        this.source=source;this.target=target;this.cancellation=cancellation;
        int sb=source.Binding.Roles["Hips"],tb=target.Binding.Roles["Hips"];
        var sr=BodySceneRegions.Prepare(source,sb,cancellation).ToDictionary(r=>r.Bone);
        var tr=BodySceneRegions.Prepare(target,tb,cancellation).ToDictionary(r=>r.Bone);
        static bool Visible(Mesh m)=>m.DrawCalls.Length==0||m.DrawCalls.Any(d=>d.DefaultVisible);
        SkinSurfacePoint[] Points(Dictionary<int,BodySceneRegion> regions,int bone,bool subtree,RigDescriptor rig,bool visible)
            =>regions.Values.Where(r=>subtree?PoseGeometry.Ancestor(rig.Scene,bone,r.Bone):r.Bone==bone)
                .SelectMany(r=>r.OriginalSensors).Select(s=>s.Point).Where(p=>!visible||Visible(rig.Scene.Meshes[p.Mesh]))
                .GroupBy(p=>(p.Mesh,p.Vertex)).OrderBy(g=>g.Key.Mesh).ThenBy(g=>g.Key.Vertex).Select(g=>g.First()).ToArray();
        var bindings=mapping.Bones.Where(p=>p.Binding is{Supplied:true,Reviewed:true,RequiresReview:false}&&
            PoseGeometry.Ancestor(source.Scene,sb,p.Source)&&PoseGeometry.Ancestor(target.Scene,tb,p.Target)&&sr.ContainsKey(p.Source)&&tr.ContainsKey(p.Target))
            .OrderBy(p=>source.CanonicalBoneRanks[p.Source]).ToArray();
        var result=new List<Pair>();var conversion=Matrix4x4.Transpose(source.Basis)*target.Basis;
        foreach(var chain in chains)foreach(var anchor in bindings)
        {
            cancellation.ThrowIfCancellationRequested();
            // Central/body branches are topology data, not a list of anatomy
            // names. Do not prescribe contacts between unrelated limb branches.
            if(chains.Any(c=>PoseGeometry.Ancestor(source.Scene,c.SourceBones[0],anchor.Source)||
                PoseGeometry.Ancestor(target.Scene,c.TargetBones[0],anchor.Target)))continue;
            var se=Points(sr,chain.SourceBones[^1],true,source,false);var te=Points(tr,chain.TargetBones[^1],true,target,true);
            var sa=Points(sr,anchor.Source,false,source,false);var ta=Points(tr,anchor.Target,false,target,true);
            if(se.Length==0||te.Length==0||sa.Length==0||ta.Length==0)continue;
            var restConversion=PoseGeometry.RotationMatrix(source.Rest.World[anchor.Source])*conversion*
                Matrix4x4.Transpose(PoseGeometry.RotationMatrix(target.Rest.World[anchor.Target]));
            result.Add(new(chain,anchor.Source,anchor.Target,se,sa,te,ta,new(target,te),new(target,ta),restConversion));
        }
        pairs=result.ToArray();
    }
    public BodySurfaceReachObservation[] Evaluate(EvaluatedPose authentic,EvaluatedPose transferred,Func<int,bool> sourceVisible)
    {
        var result=new List<BodySurfaceReachObservation>();
        foreach(var pair in pairs)
        {
            cancellation.ThrowIfCancellationRequested();
            var end=pair.SourceEnd.Where(p=>sourceVisible(p.Mesh)).Select(p=>ContactGeometry.Point(p,authentic)).ToArray();
            var body=pair.SourceBody.Where(p=>sourceVisible(p.Mesh)).Select(p=>ContactGeometry.Point(p,authentic)).ToArray();
            if(end.Length==0||body.Length==0)continue;
            var ec=Center(end);var bc=Center(body);var ray=ec-bc;float distance=ray.Length();
            if(distance<=source.CharacteristicLength*1e-6f)continue; // Unobserved relative direction, never invent a pole.
            var direction=ray/distance;
            float sourceExtent=body.Max(p=>Vector3.Dot(p-bc,direction))+end.Max(p=>Vector3.Dot(ec-p,direction));
            float gap=distance-sourceExtent;
            var sourceInverse=Matrix4x4.Transpose(PoseGeometry.RotationMatrix(authentic.World[pair.SourceAnchor]));
            var localDirection=Vector3.TransformNormal(Vector3.TransformNormal(direction,sourceInverse),pair.Conversion);
            var targetRotation=PoseGeometry.RotationMatrix(transferred.World[pair.TargetAnchor]);
            var targetDirection=Vector3.Normalize(Vector3.TransformNormal(localDirection,targetRotation));
            var te=pair.TargetEnd.Select(p=>ContactGeometry.Point(p,transferred)).ToArray();var ta=pair.TargetBody.Select(p=>ContactGeometry.Point(p,transferred)).ToArray();
            var targetEnd=pair.EndMoment.Evaluate(transferred);var targetBody=pair.BodyMoment.Evaluate(transferred);
            float extent=ta.Max(p=>Vector3.Dot(p-targetBody,targetDirection))+te.Max(p=>Vector3.Dot(targetEnd-p,targetDirection));
            float desired=Math.Max(0,extent+gap*pair.Chain.NativeLengthRatio);
            double confidence=Math.Exp(-.5*Math.Pow(Math.Max(0,gap)/pair.Chain.SourceRestLength,2));
            if(!PoseGeometry.Finite(localDirection)||!float.IsFinite(desired)||!double.IsFinite(confidence))
                throw new NotSupportedException("Body-surface geometry produced a nonfinite relative observation; no contact, feasible reach or accepted fit was inferred.");
            // Changed body volumes can put an inferred surface-centre goal
            // outside the existing chain's workspace. Bound this SOFT request
            // in its actual anchor frame; retain the original request/excess.
            // This is not a joint-limit or obstacle feasibility certificate,
            // an authored-pin edit, or permission to stretch native bones.
            var targetInverse=Matrix4x4.Transpose(targetRotation);
            var rootLocal=Vector3.TransformNormal(transferred.World[pair.Chain.TargetBones[0]].Translation-targetBody,targetInverse);
            var materialOffset=Vector3.TransformNormal(targetEnd-transferred.World[pair.Chain.TargetBones[^1]].Translation,targetInverse);
            double available=0;for(int i=1;i<pair.Chain.TargetBones.Length;i++)
                available+=Vector3.Distance(transferred.World[pair.Chain.TargetBones[i-1]].Translation,transferred.World[pair.Chain.TargetBones[i]].Translation);
            var requested=Vector3.Normalize(localDirection)*desired;
            var plan=PlanReach(requested,rootLocal,materialOffset,(float)available);
            result.Add(new(pair.Chain.SourceBones[^1],pair.Chain.TargetBones[^1],pair.TargetAnchor,pair.EndMoment,pair.BodyMoment,
                plan.Desired,confidence,pair.Chain.TargetRestLength)
                {RequestedLocal=requested,RequestedReach=plan.Requested,AvailableReach=(float)available,ReachAdapted=plan.Adapted});
        }
        // Normalize by the actual confidence mass, not a native bone count.
        double mass=result.Sum(p=>p.Weight);
        return result.Select(p=>p with{Weight=mass>0?Prior*p.Weight/mass:0}).ToArray();
    }
    internal static (Vector3 Desired,float Requested,bool Adapted) PlanReach(Vector3 desired,Vector3 root,Vector3 materialOffset,float available)
    {
        if(!PoseGeometry.Finite(desired)||!PoseGeometry.Finite(root)||!PoseGeometry.Finite(materialOffset)||!float.IsFinite(available)||available<=0)
            throw new NotSupportedException("Surface reach planning requires finite geometry and a positive actual native chain span; no rest length was changed.");
        var displacement=desired-root-materialOffset;
        double distance=Math.Sqrt((double)displacement.X*displacement.X+(double)displacement.Y*displacement.Y+(double)displacement.Z*displacement.Z);
        if(!PoseGeometry.Finite(displacement)||!double.IsFinite(distance)||distance>float.MaxValue)
            throw new NotSupportedException("Surface reach request exceeds finite native coordinate range; no goal or anatomy was normalized.");
        if(distance<=available)return(desired,(float)distance,false);
        return(root+materialOffset+displacement*(float)(available/distance),(float)distance,true);
    }
    static Vector3 Center(Vector3[] points)
    {double x=0,y=0,z=0;foreach(var p in points){x+=p.X;y+=p.Y;z+=p.Z;}return new((float)(x/points.Length),(float)(y/points.Length),(float)(z/points.Length));}
}
