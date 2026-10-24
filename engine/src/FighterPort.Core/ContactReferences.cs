using System.Numerics;

namespace FighterPort.Core;

public record SupportMotionKey(double Seconds,Vector3 Position,Quaternion Rotation);
public record GripFrame(string Socket,Vector3 Position,Quaternion Rotation);
// One frame shared by every participant. Motion is in clip seconds; native-bone
// attachments and deforming-surface tripods remain separate reviewed bindings.
public record ContactReference(Guid Id,string Name,string Evidence)
{
    public SupportMotionKey[] Motion {get;init;}=[];
    public int? Bone {get;init;}
    public SkinSurfacePoint[] SurfaceTripod {get;init;}=[];
    public GripFrame[] Grips {get;init;}=[];
    public bool Held {get;init;}
    public bool GeometryKnown {get;init;}
    public Vector3 SurfaceAnchor {get;init;}=Vector3.UnitX;
    public void Validate(int bones)
    {
        if(Id==Guid.Empty||string.IsNullOrWhiteSpace(Name)||string.IsNullOrWhiteSpace(Evidence)||Bone is {} bone&&(bone<0||bone>=bones)||SurfaceTripod.Length is not (0 or 3)||Bone is not null&&SurfaceTripod.Length>0||SurfaceTripod.Any(p=>!ContactGeometry.Valid(p,bones)))throw new InvalidDataException("Invalid reviewed support/object/surface reference.");
        if(Motion.Any(k=>!double.IsFinite(k.Seconds)||!PoseGeometry.Finite(k.Position)||!float.IsFinite(k.Rotation.LengthSquared())||Math.Abs(k.Rotation.LengthSquared()-1)>.001f)||!Motion.Select(k=>k.Seconds).SequenceEqual(Motion.Select(k=>k.Seconds).Distinct().Order()))throw new InvalidDataException("Support motion requires sorted distinct seconds and finite unit quaternions.");
        if(Grips.Select(g=>g.Socket).Distinct(StringComparer.Ordinal).Count()!=Grips.Length||Grips.Any(g=>string.IsNullOrWhiteSpace(g.Socket)||!PoseGeometry.Finite(g.Position)||!float.IsFinite(g.Rotation.LengthSquared())||Math.Abs(g.Rotation.LengthSquared()-1)>.001f))throw new InvalidDataException("Invalid fixed grip frames.");
        if(!PoseGeometry.Finite(SurfaceAnchor)||SurfaceAnchor.X<0||SurfaceAnchor.Y<0||SurfaceAnchor.Z<0||Math.Abs(SurfaceAnchor.X+SurfaceAnchor.Y+SurfaceAnchor.Z-1)>.00001f)throw new InvalidDataException("Deforming surface anchor requires barycentric coordinates inside the calibrated triangle.");
    }
    public Matrix4x4 Evaluate(EvaluatedPose pose)
    {
        var frame=Matrix4x4.Identity;
        if(Motion.Length>0)
        {
            int next=Array.FindIndex(Motion,k=>k.Seconds>pose.Seconds);var a=next==0?Motion[0]:next<0?Motion[^1]:Motion[next-1];var b=next<=0?a:Motion[next];float u=Held||a.Seconds==b.Seconds?0:(float)Math.Clamp((pose.Seconds-a.Seconds)/(b.Seconds-a.Seconds),0,1);
            frame=Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(a.Rotation,b.Rotation,u))*Matrix4x4.CreateTranslation(Vector3.Lerp(a.Position,b.Position,u));
        }
        if(Bone is {} bone)
        {
            if(!Matrix4x4.Decompose(pose.World[bone],out var scale,out _,out _)||Vector3.Distance(scale,Vector3.One)>.0001f)throw new NotSupportedException("A scaled/singular prop attachment requires a reviewed frame policy; native binds are not normalized.");
            frame*=pose.World[bone];
        }
        if(SurfaceTripod.Length==3)
        {
            var a=ContactGeometry.Point(SurfaceTripod[0],pose);var b=ContactGeometry.Point(SurfaceTripod[1],pose);var c=ContactGeometry.Point(SurfaceTripod[2],pose);var x=Vector3.Normalize(b-a);var z=Vector3.Normalize(Vector3.Cross(x,c-a));var y=Vector3.Normalize(Vector3.Cross(z,x));
            if(!PoseGeometry.Finite(x)||!PoseGeometry.Finite(y)||!PoseGeometry.Finite(z))throw new InvalidOperationException("The reviewed deforming support tripod is degenerate at this sample.");
            var anchor=a*SurfaceAnchor.X+b*SurfaceAnchor.Y+c*SurfaceAnchor.Z;
            frame*=new Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,anchor.X,anchor.Y,anchor.Z,1);
        }
        return frame;
    }
}
public record ReviewedTargetPatch(string Role,SkinSurfacePoint[] Points);
public record ReviewedTargetAnchor(string Role,Vector3 Position,Quaternion Rotation);
public record RetargetSupportBinding(Guid SourceReference,ContactReference Target,ContactTimeDomain MotionTimeDomain,string Evidence)
{
    public ReviewedTargetPatch[] TargetPatches {get;init;}=[];
    public ReviewedTargetAnchor[] TargetAnchors {get;init;}=[];
    public string? TargetDeformChecksum {get;init;}
}
