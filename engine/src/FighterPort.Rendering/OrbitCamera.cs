using System.Numerics;

namespace FighterPort.Rendering;

public sealed class OrbitCamera
{
    public Vector3 Target {get;set;}=new(0,1,0);
    public float Distance {get;set;}=4.3f;
    public float Yaw {get;set;}=.35f;
    public float Pitch {get;set;}=.15f;
    public float Roll {get;set;}
    public float OrthographicScale {get;set;}=1;
    public bool Orthographic {get;set;}
    public Matrix4x4 Orientation=>Matrix4x4.CreateRotationX(-Pitch)*Matrix4x4.CreateRotationY(Yaw)*Matrix4x4.CreateRotationZ(Roll);
    public Vector3 Back=>Vector3.TransformNormal(Vector3.UnitZ,Orientation);
    public Vector3 Eye=>Target+Back*Distance;
    public Matrix4x4 View=>Matrix4x4.CreateLookAt(Eye,Target,Vector3.TransformNormal(Vector3.UnitY,Orientation));
    public void TranslateLocal(Vector3 delta)=>Target+=Vector3.TransformNormal(delta*(Orthographic?OrthographicScale:1),Orientation);
    public void RotateDegrees(float x,float y,float z,float radius=0)
    {
        var pivot=Eye-Back*radius;
        if(Pitch < -MathF.PI/2 || Pitch > MathF.PI/2)y=-y;
        const float radians=MathF.PI/180;
        Pitch=Wrap(Pitch-x*radians);Yaw=Wrap(Yaw+y*radians);Roll=Wrap(Roll+z*radians);
        Target=pivot+Back*(radius-Distance);
        static float Wrap(float angle)=>MathF.IEEERemainder(angle,MathF.Tau);
    }
    public void Zoom(float amount)
    {
        if(!Orthographic){TranslateLocal(new(0,0,amount));return;}
        if(amount==0)return;
        OrthographicScale=Math.Clamp(OrthographicScale*(amount>=0?amount/2:2/-amount),.000001f,1000000);
    }
    public Matrix4x4 ViewProjection(float aspect)
    {
        var view=View;
        // Explicit OpenGL -1..1 depth convention.
        float near=.005f,far=10000,f=1/MathF.Tan(MathF.PI/8);
        float span=Distance*OrthographicScale;
        var projection=Orthographic?new Matrix4x4(2/(span*aspect),0,0,0,0,2/span,0,0,0,0,-2/(far-near),0,0,0,-(far+near)/(far-near),1)
            :new Matrix4x4(f/aspect,0,0,0,0,f,0,0,0,0,(far+near)/(near-far),-1,0,0,2*far*near/(near-far),0);
        return view*projection;
    }
    public Vector2 Project(Vector3 p,float width,float height)
    {
        var v=Vector4.Transform(new Vector4(p,1),ViewProjection(width/Math.Max(height,1)));
        return new((v.X/v.W*.5f+.5f)*width,(.5f-v.Y/v.W*.5f)*height);
    }
}
