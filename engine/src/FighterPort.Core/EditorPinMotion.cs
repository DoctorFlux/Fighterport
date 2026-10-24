using System.Numerics;
using System.Runtime.CompilerServices;

namespace FighterPort.Core;

// A frozen, source-bound trajectory for manual editing. It contains only the
// effector's existing ancestor curves, never a generated skeleton or solver.
public sealed record EditorPinMotion(Guid Clip,Guid Revision,string SkeletonFingerprint,RationalRate Rate,int SampleCount,double OriginFrame,BoneTrack[] Tracks)
{
    static readonly ConditionalWeakTable<EditorPinMotion,AnimationClip> Prepared=new();
    AnimationClip Animation=>Prepared.GetValue(this,static m=>new(m.Clip,"Editor pin reference",m.SampleCount,m.Rate,false,m.Tracks){Revision=m.Revision});
    public static EditorPinMotion Capture(Scene scene,AnimationClip clip,int bone,double frame)
    {
        var names=new HashSet<string>(StringComparer.Ordinal);for(int b=bone;b>=0;b=scene.Bones[b].Parent)names.Add(scene.Bones[b].Name);
        return new(clip.Id,clip.Revision,AnimationControls.Fingerprint(scene),clip.Rate,clip.SampleCount,frame,clip.Tracks.Where(t=>names.Contains(t.Bone)).ToArray());
    }
    Matrix4x4 World(Scene scene,int bone,double frame)
    {
        var world=Matrix4x4.Identity;
        for(int b=bone;b>=0;b=scene.Bones[b].Parent)world*=Animation.Sample(scene.Bones[b],frame).Matrix;
        return world;
    }
    (Vector3 Translation,Quaternion Origin,Quaternion Current) Delta(Scene scene,int bone,double frame,RationalRate rate)
    {
        var origin=World(scene,bone,OriginFrame);var current=World(scene,bone,frame/rate.FramesPerSecond*Rate.FramesPerSecond);
        return(current.Translation-origin.Translation,PoseGeometry.Rotation(origin),PoseGeometry.Rotation(current));
    }
    public ContactPin Resolve(Scene scene,ContactPin pin,double frame,RationalRate rate)
    {
        var delta=Delta(scene,pin.Bone,frame,rate);
        return pin with{Position=pin.Position+delta.Translation,Rotation=Quaternion.Normalize(Quaternion.Concatenate(Quaternion.Concatenate(pin.Rotation,Quaternion.Inverse(delta.Origin)),delta.Current))};
    }
    public ContactPin Reanchor(Scene scene,ContactPin pin,EditorPinTarget target,double frame,RationalRate rate)
    {
        var delta=Delta(scene,pin.Bone,frame,rate);
        return pin with{Position=target.Position-delta.Translation,Rotation=Quaternion.Normalize(Quaternion.Concatenate(Quaternion.Concatenate(target.Rotation,Quaternion.Inverse(delta.Current)),delta.Origin))};
    }
    public void Validate(Scene scene,ContactPin pin,string fingerprint)
    {
        Rate.Validate();
        if(SkeletonFingerprint!=fingerprint||pin.EditorBehavior!=EditorPinBehavior.SoftReach||pin.Reference is not null||pin.Socket is not null||SampleCount<1||!double.IsFinite(OriginFrame)||OriginFrame<0||OriginFrame>SampleCount||Tracks.Select(t=>t.Bone).Distinct(StringComparer.Ordinal).Count()!=Tracks.Length)throw new InvalidDataException("Invalid or stale editor pin animation reference.");
        // Use the same scalar/quaternion/time validation as editable clips.
        ProjectStore.Validate(new(1,Guid.NewGuid(),"Editor pin reference",scene,[Animation]));
    }
}
