using System.Numerics;

namespace FighterPort.Core;

public record BodySceneSensor(SkinSurfacePoint Point,double Area,float BodyInfluence);
public record BodySceneRegion(int Bone,BodySceneSensor[] Sensors,float Radius,double Area)
{
    // Unpooled material vertices retain their own rest-area mass. Evaluating
    // these posed positions avoids assigning an extremum its omitted neighbors'
    // source proximity, but does not measure actual posed contact-patch area.
    public BodySceneSensor[] OriginalSensors {get;init;}=[];
    public int OriginalPointCount {get;init;}
    public float MaximumOmittedRestDistance {get;init;}
}

// Offline quadrature on the retained native skin. Regions are direct native
// influence measures, not a list of humanoid roles or inferred contacts. Sensor
// identities remain material vertices so prepared morph positions can be used
// by ContactGeometry.Point without changing the original rig or skin weights.
public static class BodySceneRegions
{
    public const string Version="native-weighted-skin-regions-2";
    readonly record struct Influence(int Rank,double Weight);
    sealed record Sample(SkinSurfacePoint Point,Vector3 Rest,Vector3 Coordinates,double Area,float BodyInfluence,Influence[] Influences);

    public static BodySceneRegion[] Prepare(RigDescriptor rig,int body,CancellationToken cancellation=default)
    {
        ArgumentNullException.ThrowIfNull(rig);
        var scene=rig.Scene;int bones=scene.Bones.Length;
        if(body<0||body>=bones)throw new ArgumentOutOfRangeException(nameof(body),"The permitted body must reference an existing native bone.");
        if(rig.CanonicalBoneRanks.Length!=bones||rig.CanonicalBoneRanks.Distinct().Count()!=bones)
            throw new InvalidDataException("Body-scene quadrature requires the prepared rig's unique canonical native-bone ranks.");
        var inverseBasis=Matrix4x4.Transpose(rig.Basis);
        var descendants=Enumerable.Range(0,bones).Select(b=>PoseGeometry.Ancestor(scene,body,b)).ToArray();
        var groups=Enumerable.Range(0,bones).Select(_=>new List<Sample>()).ToArray();
        for(int meshIndex=0;meshIndex<scene.Meshes.Length;meshIndex++)
        {
            cancellation.ThrowIfCancellationRequested();
            var mesh=scene.Meshes[meshIndex];
            if(mesh.Indices.Length%3!=0)throw new InvalidDataException("Body-scene area preparation requires complete native triangles.");
            var points=new SkinSurfacePoint[mesh.Vertices.Length];var rest=new Vector3[points.Length];var area=new double[points.Length];
            for(int vertex=0;vertex<points.Length;vertex++)
            {
                if((vertex&255)==0)cancellation.ThrowIfCancellationRequested();
                var v=mesh.Vertices[vertex];
                points[vertex]=new(v.Position,v.Joints,v.Weights){Mesh=meshIndex,Vertex=vertex};
                rest[vertex]=ContactGeometry.Point(points[vertex],rig.Rest);
                if(!PoseGeometry.Finite(rest[vertex]))throw new InvalidDataException("Body-scene area preparation encountered nonfinite native rest skin.");
            }
            for(int triangle=0;triangle<mesh.Indices.Length;triangle+=3)
            {
                if((triangle&255)==0)cancellation.ThrowIfCancellationRequested();
                int a=checked((int)mesh.Indices[triangle]),b=checked((int)mesh.Indices[triangle+1]),c=checked((int)mesh.Indices[triangle+2]);
                double share=TriangleArea(rest[a],rest[b],rest[c])/3;
                area[a]+=share;area[b]+=share;area[c]+=share;
            }
            for(int vertex=0;vertex<points.Length;vertex++)
            {
                if((vertex&255)==0)cancellation.ThrowIfCancellationRequested();
                var point=points[vertex];var weights=new Dictionary<int,double>();double alpha=0;
                for(int slot=0;slot<4;slot++)if(point.Weights[slot]>0)
                {
                    int bone=(int)point.Joints[slot];double weight=point.Weights[slot];
                    weights[bone]=weights.GetValueOrDefault(bone)+weight;
                    if(descendants[bone])alpha+=weight;
                }
                // Repeated native slots on one bone contribute once with their
                // actual combined weight. No native influence is normalized.
                var signature=weights.Select(p=>new Influence(rig.CanonicalBoneRanks[p.Key],p.Value)).OrderBy(p=>p.Rank).ToArray();
                foreach(var influence in weights)
                    groups[influence.Key].Add(new(point,rest[vertex],Vector3.TransformNormal(rest[vertex],inverseBasis),area[vertex]*influence.Value,(float)alpha,signature));
            }
        }
        var basis=rig.Basis;
        var right=new Vector3(basis.M11,basis.M12,basis.M13);var up=new Vector3(basis.M21,basis.M22,basis.M23);var forward=new Vector3(basis.M31,basis.M32,basis.M33);
        var directions=new List<Vector3>();
        for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
            if(x!=0||y!=0||z!=0)directions.Add(right*x+up*y+forward*z);
        var result=new List<BodySceneRegion>();
        foreach(int bone in Enumerable.Range(0,bones).OrderBy(b=>rig.CanonicalBoneRanks[b]))
        {
            cancellation.ThrowIfCancellationRequested();
            var samples=groups[bone];if(samples.Count==0)continue;
            samples.Sort(Compare);double total=samples.Sum(p=>p.Area);
            if(!double.IsFinite(total))throw new NotSupportedException("Native rest surface area exceeds the body-scene numerical range.");
            Vector3 center=Center(samples,total);
            double variance=0,extent=0;
            foreach(var sample in samples)
            {
                double distance=DistanceSquared(sample.Rest,center);variance+=sample.Area*distance;extent=Math.Max(extent,distance);
            }
            double radius=total>0?Math.Sqrt(variance/total):0;
            if(radius==0)radius=Math.Sqrt(extent);
            if(radius==0)radius=rig.CharacteristicLength*.01;
            if(!double.IsFinite(radius)||radius<=0||radius>float.MaxValue||(float)radius<=0)
                throw new NotSupportedException("Native region extent is outside the body-scene numerical range; no rig scale was changed.");

            var originals=samples.Select(s=>new BodySceneSensor(s.Point,s.Area,s.BodyInfluence)).ToArray();
            var kept=new HashSet<int>();
            if(samples.Count<=27)for(int i=0;i<samples.Count;i++)kept.Add(i);
            else
            {
                // Geometry-first canonical order resolves equal extrema. Mesh
                // and vertex IDs are only final ties between equivalent skin
                // samples; no native bone storage index selects an extremum.
                int nearest=0;double nearestDistance=DistanceSquared(samples[0].Rest,center);
                for(int i=1;i<samples.Count;i++)
                {
                    double distance=DistanceSquared(samples[i].Rest,center);
                    if(distance<nearestDistance){nearest=i;nearestDistance=distance;}
                }
                kept.Add(nearest);
                foreach(var direction in directions)
                {
                    int extreme=0;double maximum=Dot(samples[0].Rest,direction);
                    for(int i=1;i<samples.Count;i++)
                    {
                        if((i&255)==0)cancellation.ThrowIfCancellationRequested();
                        double value=Dot(samples[i].Rest,direction);
                        if(value>maximum){maximum=value;extreme=i;}
                    }
                    kept.Add(extreme);
                }
            }
            int[] selected=kept.Order().ToArray();var masses=new double[selected.Length];double omitted=0;
            for(int i=0;i<samples.Count;i++)
            {
                if((i&255)==0)cancellation.ThrowIfCancellationRequested();
                int nearest=0;double distance=DistanceSquared(samples[i].Rest,samples[selected[0]].Rest);
                for(int j=1;j<selected.Length;j++)
                {
                    double candidate=DistanceSquared(samples[i].Rest,samples[selected[j]].Rest);
                    if(candidate<distance){distance=candidate;nearest=j;}
                }
                masses[nearest]+=samples[i].Area;
                if(!kept.Contains(i))omitted=Math.Max(omitted,distance);
            }
            double omittedDistance=Math.Sqrt(omitted);
            if(!double.IsFinite(omittedDistance)||omittedDistance>float.MaxValue)
                throw new NotSupportedException("Native quadrature omission distance exceeds the body-scene numerical range.");
            var sensors=selected.Select((i,j)=>new BodySceneSensor(samples[i].Point,masses[j],samples[i].BodyInfluence)).ToArray();
            result.Add(new(bone,sensors,(float)radius,total){OriginalSensors=originals,OriginalPointCount=samples.Count,MaximumOmittedRestDistance=(float)omittedDistance});
        }
        return result.ToArray();
    }

    static Vector3 Center(List<Sample> samples,double area)
    {
        if(area>0)
        {
            double x=0,y=0,z=0;
            foreach(var sample in samples){x+=sample.Rest.X*sample.Area;y+=sample.Rest.Y*sample.Area;z+=sample.Rest.Z*sample.Area;}
            return new((float)(x/area),(float)(y/area),(float)(z/area));
        }
        // A degenerate/unreferenced region has zero area and no fabricated
        // vertex-count measure. Its geometry still supplies an honest extent.
        var min=samples.Select(p=>p.Rest).Aggregate(Vector3.Min);var max=samples.Select(p=>p.Rest).Aggregate(Vector3.Max);
        return new((float)(((double)min.X+max.X)*.5),(float)(((double)min.Y+max.Y)*.5),(float)(((double)min.Z+max.Z)*.5));
    }
    static double TriangleArea(Vector3 a,Vector3 b,Vector3 c)
    {
        double x=(double)b.X-a.X,y=(double)b.Y-a.Y,z=(double)b.Z-a.Z;
        double u=(double)c.X-a.X,v=(double)c.Y-a.Y,w=(double)c.Z-a.Z;
        double cx=y*w-z*v,cy=z*u-x*w,cz=x*v-y*u;
        double area=.5*Math.Sqrt(cx*cx+cy*cy+cz*cz);
        if(!double.IsFinite(area))throw new NotSupportedException("Native triangle area exceeds the body-scene numerical range.");
        return area;
    }
    static double Dot(Vector3 a,Vector3 b)=>(double)a.X*b.X+(double)a.Y*b.Y+(double)a.Z*b.Z;
    static double DistanceSquared(Vector3 a,Vector3 b)
    {
        double x=(double)a.X-b.X,y=(double)a.Y-b.Y,z=(double)a.Z-b.Z;return x*x+y*y+z*z;
    }
    static int Compare(Sample a,Sample b)
    {
        int order=a.Coordinates.X.CompareTo(b.Coordinates.X);if(order!=0)return order;
        order=a.Coordinates.Y.CompareTo(b.Coordinates.Y);if(order!=0)return order;order=a.Coordinates.Z.CompareTo(b.Coordinates.Z);if(order!=0)return order;
        int length=Math.Min(a.Influences.Length,b.Influences.Length);
        for(int i=0;i<length;i++)
        {
            order=a.Influences[i].Rank.CompareTo(b.Influences[i].Rank);if(order!=0)return order;
            order=a.Influences[i].Weight.CompareTo(b.Influences[i].Weight);if(order!=0)return order;
        }
        order=a.Influences.Length.CompareTo(b.Influences.Length);if(order!=0)return order;
        order=a.Point.Mesh.CompareTo(b.Point.Mesh);return order!=0?order:a.Point.Vertex.CompareTo(b.Point.Vertex);
    }
}
