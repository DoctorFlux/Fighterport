using System.Numerics;

namespace FighterPort.Core;

public static partial class ReviewedContactSets
{
    static Vector3 ContactEulerNear(Vector3 value,Vector3 near)
    {
        // Choose native-equivalent Euler coordinates without converting a
        // scaled local matrix through a quaternion or normalizing authored turns.
        Vector3 Align(Vector3 v)=>new(v.X+360*MathF.Round((near.X-v.X)/360),
            v.Y+360*MathF.Round((near.Y-v.Y)/360),v.Z+360*MathF.Round((near.Z-v.Z)/360));
        var a=Align(value);var b=Align(new(value.X+180,180-value.Y,value.Z+180));
        return Vector3.DistanceSquared(a,near)<=Vector3.DistanceSquared(b,near)?a:b;
    }
    // Scoped to this native contact operation: retain the existing generic Core
    // curve evaluator for other tools. CHR0 uses the offset/difference form of
    // Hermite, whose floating-point behavior differs from a basis-weighted sum.
    internal static float NativeContactSample(Curve curve,double frame)
    {
        frame=(float)frame;
        if(curve.Interpolation!=Interpolation.Hermite||curve.Keys.Length<2)return curve.Sample(frame);
        if(curve.LoopFrameLimit is not null)throw new NotSupportedException("Looped native contact sampling is not qualified.");
        var keys=curve.Keys;int lo=0,hi=keys.Length;
        while(lo<hi){int mid=(lo+hi)/2;if(keys[mid].Frame<=frame)lo=mid+1;else hi=mid;}
        if(lo>0&&keys[lo-1].Frame==frame){int at=lo-1;while(at>0&&keys[at-1].Frame==frame)at--;return keys[at].Value;}
        if(lo==0)return keys[0].Value;if(lo==keys.Length)return keys[^1].Value;
        var a=keys[lo-1];var b=keys[lo];float offset=(float)(frame-a.Frame),span=(float)(b.Frame-a.Frame);
        float time=offset/span,inv=time-1,diff=b.Value-a.Value;
        return a.Value+offset*inv*(inv*a.OutTangent+time*b.InTangent)+time*time*(3-2*time)*diff;
    }
    static Transform ContactLocal(Bone bone,BoneTrack? track,double frame)
    {
        if(track?.Rotation is not null)throw new NotSupportedException("Native contact sampling requires Euler tracks.");
        var values=bone.Rest.Channels();if(track!=null)for(int c=0;c<9;c++)if(track.Channels[c] is {Keys.Length:>0} curve)values[c]=NativeContactSample(curve,frame);
        return Transform.From(values);
    }
    static EvaluatedPose ContactPose(Scene scene,AnimationClip clip,double frame)
    {
        var tracks=clip.Tracks.ToDictionary(t=>t.Bone,StringComparer.Ordinal);
        return AnimationService.Evaluate(scene,null,0,scene.Bones.Select((b,i)=>(i,Value:ContactLocal(b,tracks.GetValueOrDefault(b.Name),frame))).ToDictionary(p=>p.i,p=>p.Value));
    }
}
