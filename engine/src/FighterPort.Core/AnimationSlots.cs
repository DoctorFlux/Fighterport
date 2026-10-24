namespace FighterPort.Core;

public sealed record EditorSlotIntent(string Id,string? ResourceKey,bool Required,bool Protected,string ReviewStatus,string? ProfileKey=null);
public static class AnimationSlots
{
    public const string Owner="Next.CustomSlots";
    public static FighterProject Synchronize(FighterProject project,EditorSlotIntent[] slots,IReadOnlyDictionary<string,Guid> nativeClips)
    {
        if(slots.Length>1024||slots.Any(s=>string.IsNullOrWhiteSpace(s.Id)||s.Id.Length>80||s.Id.Any(char.IsControl)||(s.ResourceKey?.Length??0)>16384||(s.ProfileKey?.Length??0)>16500||s.ReviewStatus.Length>256)||slots.Select(s=>s.Id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=slots.Length)throw new InvalidDataException("Invalid custom animation slots.");
        var output=slots.Select(s=>new SlotAssignment("custom:"+s.Id.Trim(),s.ResourceKey is {} key&&nativeClips.TryGetValue(key,out var id)?id:null,s.Protected,(s.Required?"Required":"Optional")+" custom editor slot · "+s.ReviewStatus){EditorOwner=Owner,ResourceKey=s.ResourceKey,ProfileKey=s.ProfileKey}).ToArray();
        if(output.Any(s=>s.Animation is {} id&&!project.Clips.Any(c=>c.Id==id)))throw new InvalidDataException("Slot binding refers to a different scene's animation.");
        var retained=project.Slots.Where(s=>s.EditorOwner!=Owner).ToArray();if(retained.Any(s=>output.Any(o=>o.Id==s.Id)))throw new InvalidDataException("A custom slot conflicts with an existing target-owned slot.");
        return project with{Slots=[..retained,..output]};
    }
}
