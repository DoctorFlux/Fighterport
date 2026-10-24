namespace FighterPort.Core;

// A selectable editor control, not an inferred contact or a replacement bone.
public sealed record EditorBonePin(Guid Id,int Bone,Guid? Clip,string SkeletonFingerprint);

public sealed partial class EditorDocument
{
    public EditorBonePin[] CurrentBonePins=>(Project.BonePins??[]).Where(p=>p.SkeletonFingerprint==sceneFingerprint&&(Project.Clips.Length==0?p.Clip is null:p.Clip==Clip.Id)).ToArray();
    public Guid AddBonePin(int bone)
    {
        RequireEditing();if(bone<0||bone>=Project.Scene.Bones.Length)throw new ArgumentOutOfRangeException(nameof(bone));Clock.Playing=false;
        if(CurrentBonePins.FirstOrDefault(p=>p.Bone==bone) is {} existing)return existing.Id;
        var pin=new EditorBonePin(Guid.NewGuid(),bone,Project.Clips.Length==0?null:Clip.Id,sceneFingerprint);
        Transaction("Attach existing-bone FK pin",p=>p with{BonePins=[..p.BonePins??[],pin]});return pin.Id;
    }
    public void RemoveEditorPin(Guid id)
    {
        RequireEditing();if(CurrentBonePins.All(p=>p.Id!=id)&&CurrentContacts.All(p=>p.Id!=id||p.EditorBehavior!=EditorPinBehavior.SoftReach))return;
        Transaction("Remove editor pin",p=>p with{BonePins=p.BonePins?.Where(pin=>pin.Id!=id).ToArray(),AuthoringContacts=p.AuthoringContacts.Where(pin=>pin.Id!=id||pin.EditorBehavior!=EditorPinBehavior.SoftReach).ToArray(),Contacts=p.Contacts.Select(c=>c with{Pins=c.Pins.Where(pin=>pin.Id!=id||pin.EditorBehavior!=EditorPinBehavior.SoftReach).ToArray()}).Where(c=>c.Pins.Length>0).ToArray()});
    }
}
