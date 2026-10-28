using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace FighterPort.Core;

public enum Interpolation { Hermite, Linear, Step }
public enum NativeWriteMode { PreserveSource, NewResource }
public record Key(double Frame, float Value, float InTangent = 0, float OutTangent = 0);
public record Curve(Key[] Keys, Interpolation Interpolation = Interpolation.Hermite, int? LoopFrameLimit = null)
{
    public float Sample(double frame)
    {
        if(Keys.Length==0) throw new InvalidDataException("Empty curve.");
        if(Keys.Length==1) return Keys[0].Value;
        if(LoopFrameLimit is {} limit && (frame<Keys[0].Frame || frame>Keys[^1].Frame))
        {
            var first=Keys[0];var last=Keys[^1];double span=limit-last.Frame+first.Frame;
            return Interpolate(last,first,(float)((frame<first.Frame?limit-last.Frame+frame:frame-last.Frame)/span),(float)span);
        }
        // Native exact-time samples use the first (incoming) key. The following segment uses the outgoing duplicate.
        int lo=0, hi=Keys.Length;
        while(lo<hi) { int m=(lo+hi)/2; if(Keys[m].Frame<=frame) lo=m+1; else hi=m; }
        if(lo>0 && Keys[lo-1].Frame==frame){int exact=lo-1;while(exact>0 && Keys[exact-1].Frame==frame)exact--;return Keys[exact].Value;}
        if(lo==0) return Keys[0].Value;
        if(lo==Keys.Length) return Keys[^1].Value;
        var a=Keys[lo-1]; var b=Keys[lo];
        float dt=(float)(b.Frame-a.Frame), u=(float)((frame-a.Frame)/dt);
        return Interpolate(a,b,u,dt);
    }
    float Interpolate(Key a,Key b,float u,float dt)=> Interpolation switch {
            Interpolation.Step=>a.Value,
            Interpolation.Linear=>a.Value+(b.Value-a.Value)*u,
            _=>(2*u*u*u-3*u*u+1)*a.Value+(u*u*u-2*u*u+u)*dt*a.OutTangent
                +(-2*u*u*u+3*u*u)*b.Value+(u*u*u-u*u)*dt*b.InTangent
        };
    public Curve Put(Key key) => this with { Keys = Keys.Where(x=>x.Frame!=key.Frame).Append(key).OrderBy(x=>x.Frame).ToArray() };
}
public enum QuaternionInterpolation { Spherical, Step }
public record QuaternionKey(double Frame,Quaternion Value);
public record QuaternionCurve(QuaternionKey[] Keys,QuaternionInterpolation Interpolation=QuaternionInterpolation.Spherical)
{
    public Quaternion Sample(double frame)
    {
        if(Keys.Length==0)throw new InvalidDataException("Empty quaternion channel.");int next=Array.FindIndex(Keys,k=>k.Frame>frame);if(next==0)return Quaternion.Normalize(Keys[0].Value);if(next<0)return Quaternion.Normalize(Keys[^1].Value);var a=Keys[next-1];var b=Keys[next];return Interpolation==QuaternionInterpolation.Step?Quaternion.Normalize(a.Value):Quaternion.Normalize(Quaternion.Slerp(a.Value,b.Value,(float)((frame-a.Frame)/(b.Frame-a.Frame))));
    }
    public QuaternionCurve Put(QuaternionKey key)=>this with{Keys=Keys.Where(k=>k.Frame!=key.Frame).Append(key).OrderBy(k=>k.Frame).ToArray()};
}
public record BoneTrack(string Bone, Curve?[] Channels)
{
    public QuaternionCurve? Rotation {get;init;}
    [JsonIgnore] public bool HasKeys=>Rotation is {Keys.Length:>0}||Channels.Any(c=>c is {Keys.Length:>0});
}
public record AnimationClip(Guid Id, string Name, int SampleCount, RationalRate Rate, bool Loop, BoneTrack[] Tracks)
{
    public SourceClipRevision? Source { get; init; }
    public Guid Revision { get; init; } = Guid.NewGuid();
    public TransitionRecipe? Transition { get; init; }
    public RetargetRecipe? Retarget {get;init;}
    public Guid? SourceSnapshot {get;init;}
    public SourceClipRevision? NativeTarget {get;init;}
    public NativeWriteMode NativeWrite {get;init;}
    public string? NativeWriteName {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public bool RewriteNativeChannels {get;init;}
    public double LastSampleFrame => SampleCount-1;
    public double PreviewEndFrame=>SampleCount-.0001;
    public double LastKeyFrame => Tracks.SelectMany(t=>t.Channels.OfType<Curve>().SelectMany(c=>c.Keys).Select(k=>k.Frame).Concat(t.Rotation?.Keys.Select(k=>k.Frame)??[])).DefaultIfEmpty(0).Max();
    public double LastKeySeconds => LastKeyFrame/Rate.FramesPerSecond;
    public double DurationSeconds => SampleCount/Rate.FramesPerSecond;
    public Transform Sample(Bone bone, double frame)
    {
        var track=Tracks.FirstOrDefault(t=>t.Bone==bone.Name);
        if(track is null) return bone.Rest;
        var v=bone.Rest.Channels();
        for(int i=0;i<9;i++) if(track.Channels[i] is {} c) v[i]=c.Sample(frame);
        var value=Transform.From(v);return track.Rotation is {} quaternion?value with{Rotation=AnimationControls.Euler(Matrix4x4.CreateFromQuaternion(quaternion.Sample(frame)),bone.Rest.Rotation)}:value;
    }
}

public sealed record EvaluatedPose(Transform[] Local, Matrix4x4[] World, Matrix4x4[] Skin)
{
    public Vector3[][]? GeometryPositions {get;init;}
    public double Seconds {get;init;}
}
public static class AnimationService
{
    // Scene revisions are immutable. Keep their native inverse binds prepared,
    // rather than rebuilding and inverting them on every pose/solver evaluation.
    // Weak ownership allows closed source/target documents to be collected.
    sealed class BindData(Scene scene)
    {
        public readonly Matrix4x4[] Inverse = Prepare(scene);
        static Matrix4x4[] Prepare(Scene scene)
        {
            var rest=new Matrix4x4[scene.Bones.Length];var inverse=new Matrix4x4[rest.Length];
            for(int i=0;i<rest.Length;i++)
            {
                var b=scene.Bones[i];rest[i]=b.Rest.Matrix*(b.Parent<0?Matrix4x4.Identity:rest[b.Parent]);
                if(!Matrix4x4.Invert(rest[i],out inverse[i]))throw new InvalidDataException($"Singular bind matrix: {b.Name}");
            }
            return inverse;
        }
    }
    static readonly ConditionalWeakTable<Scene,BindData> BindCache=new();
    public static EvaluatedPose Evaluate(Scene scene, AnimationClip? clip, double frame, IReadOnlyDictionary<int,Transform>? overrides=null)
    {
        int n=scene.Bones.Length;
        var inverse=BindCache.GetValue(scene,static s=>new(s)).Inverse;
        var world=new Matrix4x4[n]; var skin=new Matrix4x4[n]; var local=new Transform[n];
        for(int i=0;i<n;i++)
        {
            var b=scene.Bones[i];
            local[i]=overrides?.GetValueOrDefault(i) ?? clip?.Sample(b,frame) ?? b.Rest;
            world[i]=local[i].Matrix * (b.Parent<0?Matrix4x4.Identity:world[b.Parent]);
            skin[i]=inverse[i]*world[i];
        }
        return new(local,world,skin){Seconds=clip is null?0:frame/clip.Rate.FramesPerSecond};
    }
    public static Vector3 Skin(Vertex v, EvaluatedPose pose)
    {
        var p=Vector3.Zero;
        for(int i=0;i<v.InfluenceCount;i++) if(v.WeightAt(i)!=0) p+=Vector3.Transform(v.Position,pose.Skin[(int)v.JointAt(i)])*v.WeightAt(i);
        return p;
    }
}

public sealed class AnimationClock
{
    public double Frame { get; private set; }
    public double Speed { get; set; }=1;
    public bool Playing { get; set; }
    public bool? LoopOverride { get; set; }
    public bool Loops(AnimationClip clip)=>LoopOverride??clip.Loop;
    public void Play(AnimationClip clip)
    {
        if(Speed>=0&&Frame>=clip.PreviewEndFrame)Frame=0;
        else if(Speed<0&&Frame<=0)Frame=clip.LastSampleFrame;
        Playing=true;
    }
    public void Seek(double frame, AnimationClip clip) => Frame=Math.Clamp(frame,0,clip.Loop?clip.SampleCount:clip.PreviewEndFrame);
    public void Advance(double seconds, AnimationClip clip)
    {
        if(!Playing) return;
        var next=Frame+seconds*clip.Rate.FramesPerSecond*Speed;
        if(Loops(clip)) Frame=((next%clip.SampleCount)+clip.SampleCount)%clip.SampleCount;
        else { Frame=Math.Clamp(next,0,clip.PreviewEndFrame); if(next>=clip.SampleCount || next<0) Playing=false; }
    }
}
