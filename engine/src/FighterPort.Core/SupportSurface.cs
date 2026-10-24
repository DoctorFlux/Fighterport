using System.Numerics;

namespace FighterPort.Core;

// The platform occupies the inward half-space below its top. This is supplied
// scene geometry, independent of animation names, native bone names and games.
public record SupportEdge(Vector3 Inward,float Offset)
{
    public void Validate(Vector3 up)
    {
        if(!PoseGeometry.Finite(Inward)||!float.IsFinite(Offset)||Math.Abs(Inward.LengthSquared()-1)>.0001f||Math.Abs(Vector3.Dot(Inward,up))>.0001f)
            throw new InvalidDataException("A 90-degree support edge needs a finite unit inward direction perpendicular to its floor normal and a finite offset.");
    }
}
public readonly record struct SupportSurface(Vector3 Normal,float Height,SupportEdge? Edge=null)
{
    // Separation from the bounded TOP support, distinct from penetration into
    // the solid. Exterior hanging space is clear but is not ground support.
    public Vector3 TopOffset(Vector3 point)=>Normal*(Vector3.Dot(point,Normal)-Height)-(Edge is {} edge?edge.Inward*Math.Max(0,edge.Offset-Vector3.Dot(point,edge.Inward)):Vector3.Zero);
    // Exact signed clearance inside this convex solid; outside a corner this
    // is a half-space clearance, not a claimed Euclidean distance field.
    public float Clearance(Vector3 point)=>Edge is {} edge?Math.Max(Vector3.Dot(point,Normal)-Height,edge.Offset-Vector3.Dot(point,edge.Inward)):Vector3.Dot(point,Normal)-Height;
    public float Penetration(Vector3 point)=>Math.Max(0,-Clearance(point));
    public bool AboveTop(Vector3 point,float margin=0)=>Edge is null||Vector3.Dot(point,Edge.Inward)>=Edge.Offset-margin;
    public Vector3 Correction(Vector3 point)
    {
        float vertical=Height-Vector3.Dot(point,Normal);if(vertical<=0)return Vector3.Zero;
        if(Edge is {} edge){float inward=Vector3.Dot(point,edge.Inward)-edge.Offset;if(inward<=0)return Vector3.Zero;if(inward<vertical)return -edge.Inward*inward;}
        return Normal*vertical;
    }
}
