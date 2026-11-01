using System.Numerics;

namespace FighterPort.Core;

public static partial class ReviewedContactSets
{
    // Solver regions retain the existing 512-point budget. Acceptance also
    // visits the complete native foot influence region, including vertices
    // whose foot contribution is below the detector's ownership threshold.
    static SkinSurfacePoint[] FullFootRegion(Scene scene,int foot)=>scene.Meshes.SelectMany((mesh,m)=>mesh.Vertices.Select((v,i)=>(v,m,i)))
        .Where(p=>Enumerable.Range(0,p.v.InfluenceCount).Any(j=>p.v.WeightAt(j)>0&&PoseGeometry.Ancestor(scene,foot,(int)p.v.JointAt(j))))
        .Select(p=>new SkinSurfacePoint(p.v.Position,p.v.Joints,p.v.Weights){Influences=p.v.Influences,Mesh=p.m,Vertex=p.i}).ToArray();
    static string[] FullFootDependencies(Scene scene,int foot)
    {
        var bones=new HashSet<int>();
        foreach(int index in FullFootRegion(scene,foot).SelectMany(p=>Enumerable.Range(0,p.InfluenceCount).Where(j=>p.WeightAt(j)>0).Select(j=>(int)p.JointAt(j))).Append(foot))
            for(int i=index;i>=0;i=scene.Bones[i].Parent)bones.Add(i);
        return bones.Select(i=>scene.Bones[i].Name).Order(StringComparer.Ordinal).ToArray();
    }
    static SkinSurfacePoint[] ContactSolverRegion(Scene scene,ReviewedContactRange row)
    {
        // Triangle face storage can repeat the SAME skinned point many times.
        // Deduplicate identical equations, not skin coefficients or geometry.
        // Full acceptance still visits every original face/sole witness.
        string Key(SkinSurfacePoint p)=>ContentKey(new{Position=new[]{p.Position.X,p.Position.Y,p.Position.Z}.Select(BitConverter.SingleToInt32Bits).ToArray(),
            Influences=Enumerable.Range(0,p.InfluenceCount).Select(i=>new{Joint=(int)p.JointAt(i),Weight=BitConverter.SingleToInt32Bits(p.WeightAt(i))}).ToArray()});
        SkinSurfacePoint[] Unique(SkinSurfacePoint[] points)=>points.GroupBy(Key,StringComparer.Ordinal).OrderBy(g=>g.Key,StringComparer.Ordinal).Select(g=>g.First()).ToArray();
        var complete=Unique(FullFootRegion(scene,row.Bone));
        return complete.Length<=512?complete:Unique(row.Region);
    }
    sealed record FullFootWitness(SkinSurfacePoint[] Region,SkinSurfacePoint[] Sole,Vector3[] Anchors);
    // Keep every original witness and every ordered skin coefficient. Repeated
    // face-storage points share only an identical position equation at one pose.
    // Dynamic geometry must retain its mesh/vertex-specific identity.
    internal sealed class CompleteSkinSamples
    {
        readonly Dictionary<SkinSurfacePoint,(int Equation,int Member)> members=new();
        public CompleteSkinSamples(IEnumerable<SkinSurfacePoint> points)
        {
            var equations=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var point in points)
            {
                if(members.ContainsKey(point))continue;
                string key=string.Join(",",new[]{BitConverter.SingleToInt32Bits(point.Position.X),BitConverter.SingleToInt32Bits(point.Position.Y),BitConverter.SingleToInt32Bits(point.Position.Z)}
                    .Concat(Enumerable.Range(0,point.InfluenceCount).SelectMany(i=>new[]{(int)point.JointAt(i),BitConverter.SingleToInt32Bits(point.WeightAt(i))})));
                if(!equations.TryGetValue(key,out int equation)){equation=equations.Count;equations.Add(key,equation);}
                members.Add(point,(equation,members.Count));
            }
        }
        public AtPose At(EvaluatedPose pose)=>new(this,pose);
        public sealed class AtPose(CompleteSkinSamples owner,EvaluatedPose pose)
        {
            readonly Dictionary<int,Vector3> evaluated=new();
            public Vector3 Point(SkinSurfacePoint point,ref long work)
            {
                var member=owner.members[point];int identity=pose.GeometryPositions is null?member.Equation:member.Member;
                if(evaluated.TryGetValue(identity,out var value))return value;
                if(++work>MaximumKeys)throw new InvalidOperationException("Complete skin evaluation exhausted its unchanged two-million-work budget.");
                value=ContactGeometry.Point(point,pose);evaluated.Add(identity,value);return value;
            }
        }
    }
    static FullFootWitness FullFootWitnesses(Scene scene,AnimationClip master,ReviewedContactRange row,Vector3 normal)
    {
        var region=FullFootRegion(scene,row.Bone);
        if(region.Length==0||region.Any(p=>!ContactGeometry.Valid(p,scene.Bones.Length)))throw new InvalidDataException("Complete foot geometry is missing or invalid.");
        var pose=ContactPose(scene,master,row.AnchorFrame);
        float low=region.Min(p=>Vector3.Dot(ContactGeometry.Point(p,pose),normal));
        var sole=region.Where(p=>Vector3.Dot(ContactGeometry.Point(p,pose),normal)<=low+.00001f).ToArray();
        var center=row.Patch.Select(p=>ContactGeometry.Point(p,pose)).Aggregate(Vector3.Zero,(a,p)=>a+p)/row.Patch.Length;
        var shift=row.Anchor-center;
        return new(region,sole,sole.Select(p=>ContactGeometry.Point(p,pose)+shift).ToArray());
    }
}
