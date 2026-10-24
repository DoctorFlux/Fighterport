using System.Numerics;

namespace FighterPort.Core;

public sealed record EditorPinTarget(Vector3 Position,Quaternion Rotation);

public sealed partial class EditorDocument
{
    TransformEdit? activeTransform;
    public TransformEdit BeginTransformEdit()
    {
        RequireEditing();CancelTransformEdit();Clock.Playing=false;
        return activeTransform=new(this);
    }
    public void CancelTransformEdit(){activeTransform?.Cancel();activeTransform=null;}
    internal void FinishTransformEdit(TransformEdit edit){if(ReferenceEquals(activeTransform,edit))activeTransform=null;}
}

// Gesture ownership is the exact document/project/clip/time, not whatever the UI
// happens to select on release. Preview never creates keys or an undo command.
public sealed class TransformEdit:IDisposable
{
    readonly EditorDocument document;
    readonly FighterProject before;
    readonly Guid clip;
    readonly double frame;
    readonly int selectedBone;
    readonly Transform[] initial;
    readonly Dictionary<int,Transform> overrides;
    bool finished;
    internal TransformEdit(EditorDocument owner)
    {
        document=owner;before=owner.Project;clip=owner.Clip.Id;frame=owner.Clock.Frame;selectedBone=owner.SelectedBone;
        initial=owner.Evaluate().Local.ToArray();overrides=owner.PoseOverrides.ToDictionary();
    }
    bool Current=>ReferenceEquals(before,document.Project)&&document.Clip.Id==clip&&document.Clock.Frame==frame&&document.SelectedBone==selectedBone&&!document.IsSourcePreview;
    public bool Commit(IReadOnlyDictionary<int,Transform> values,IReadOnlyDictionary<Guid,EditorPinTarget>? pinTargets=null)
    {
        if(finished)return false;if(!Current){Cancel();return false;}
        var edits=values.Where(p=>p.Key>=0&&p.Key<initial.Length&&p.Value.Channels().Where((v,i)=>v!=initial[p.Key].Channels()[i]).Any()).ToArray();
        if(edits.Any(e=>!e.Value.Channels().All(float.IsFinite)))throw new InvalidDataException("Transforms must be finite.");
        var targets=pinTargets??new Dictionary<Guid,EditorPinTarget>();var pins=new Dictionary<Guid,ContactPin>();
        foreach(var pair in targets)
        {
            var matching=document.CurrentContacts.Where(p=>p.Id==pair.Key).ToArray();
            if(matching.Length!=1||matching[0].EditorBehavior!=EditorPinBehavior.SoftReach||matching[0].Reference is not null||matching[0].Socket is not null||!PoseGeometry.Finite(pair.Value.Position)||!float.IsFinite(pair.Value.Rotation.LengthSquared())||Math.Abs(pair.Value.Rotation.LengthSquared()-1)>.001f)throw new InvalidDataException("Only unambiguous active editor soft-pin handles without a support reference or socket can be moved by this gesture.");
            pins.Add(pair.Key,matching[0]);
        }
        bool movedPins=targets.Any(e=>pins[e.Key].Position!=e.Value.Position||pins[e.Key].Rotation!=e.Value.Rotation);
        ContactPin MovePin(ContactPin pin)=>targets.TryGetValue(pin.Id,out var target)?pin.EditorMotion?.Reanchor(before.Scene,pin,target,frame,document.Clip.Rate)??pin with{Position=target.Position,Rotation=target.Rotation}:pin;
        if(edits.Length==0&&!movedPins){Cancel();return false;}
        finished=true;document.FinishTransformEdit(this);
        if(before.Clips.Length==0)
        {
            document.Transaction("Edit model authoring pose",p=>p with{AuthoringPose=p.AuthoringPose.Concat(edits).GroupBy(e=>e.Key).ToDictionary(g=>g.Key,g=>g.Last().Value),AuthoringContacts=movedPins?p.AuthoringContacts.Select(MovePin).ToArray():p.AuthoringContacts});return true;
        }
        document.Transaction("Edit animation transform",p=>
        {
            var motion=p.Clips[document.SelectedClip];var tracks=motion.Tracks.ToList();
            foreach(var edit in edits)
            {
                var bone=p.Scene.Bones[edit.Key];int index=tracks.FindIndex(t=>t.Bone==bone.Name);var track=index<0?new BoneTrack(bone.Name,new Curve?[9]):tracks[index];var channels=track.Channels.ToArray();var old=initial[edit.Key].Channels();var next=edit.Value.Channels();bool rotation=false;
                for(int c=0;c<9;c++)if(next[c]!=old[c])
                {
                    if(c is >=3 and <6&&track.Rotation is not null){rotation=true;continue;}
                    var curve=channels[c];var existing=curve?.Keys.FirstOrDefault(k=>k.Frame==frame);
                    if(curve is null)
                    {
                        float start=motion.Sample(bone,0).Channels()[c];curve=new(frame==0?[]:[new(0,start)]);
                    }
                    // Existing tangents and neighbouring curve state survive.
                    // New Hermite entries retain the evaluated native derivative;
                    // no blanket tangent regeneration or unrelated channel keys.
                    float tangent=curve.Keys.Length>1&&curve.Interpolation==Interpolation.Hermite?(curve.Sample(frame+.001)-curve.Sample(frame-.001))/.002f:0;
                    // An existing incoming/outgoing pair represents a native
                    // discontinuity. Update the displayed incoming entry only;
                    // retain the authored outgoing side and both tangents.
                    if(existing is null)channels[c]=curve.Put(new(frame,next[c],tangent,tangent));
                    else{var keys=curve.Keys.ToArray();keys[Array.FindIndex(keys,k=>k.Frame==frame)]=existing with{Value=next[c]};channels[c]=curve with{Keys=keys};}
                }
                var quaternion=track.Rotation;
                if(rotation&&quaternion is not null)quaternion=quaternion.Put(new(frame,Quaternion.CreateFromRotationMatrix((Transform.Identity with{Rotation=edit.Value.Rotation}).Matrix)));
                var replacement=track with{Channels=channels,Rotation=quaternion};if(index<0)tracks.Add(replacement);else tracks[index]=replacement;
            }
            var changed=motion with{Tracks=tracks.ToArray(),Revision=Guid.NewGuid(),Retarget=motion.Retarget is {} recipe?recipe with{Validation=null,IsDraft=true}:null};
            return p with{Clips=p.Clips.Select(c=>c.Id==clip?changed:c).ToArray(),Contacts=movedPins?p.Contacts.Select(c=>c.Clip==clip&&c.Contains(frame)?c with{Pins=c.Pins.Select(MovePin).ToArray()}:c).ToArray():p.Contacts};
        });return true;
    }
    public void Cancel()
    {
        if(finished)return;finished=true;
        if(Current){document.PoseOverrides.Clear();foreach(var pair in overrides)document.PoseOverrides.Add(pair.Key,pair.Value);document.Notify();}
        document.FinishTransformEdit(this);
    }
    public void Dispose()=>Cancel();
}
