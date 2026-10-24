namespace FighterPort.Core;

// Conservative contact-aware editing. Keeping every ancestor's original S/R/T
// curves preserves the exact evaluated point/orientation at every time, including
// native subframes and loop seams. This deliberately does not invent IK goals.
public static class ContactBoundaryProtection
{
    public static string[] Resolve(FighterProject project,AnimationClip clip,BoundaryFitSettings settings)
    {
        var frames=project.Contacts.Where(c=>c.Clip==clip.Id&&c.Pins.Length>0).ToArray();
        var pins=frames.SelectMany(c=>c.Pins).Concat(project.AuthoringContacts).ToArray();
        if(!settings.PreserveContacts)
        {
            if(pins.Length>0||settings.ContactBones.Length>0)throw new NotSupportedException("This clip has active contact/soft-pin constraints. Enable existing contact trajectory preservation; repositioning support needs a coupled solver. Native source and pins remain unchanged.");
            return [];
        }
        string fingerprint=AnimationControls.Fingerprint(project.Scene);
        if(frames.Any(c=>c.SkeletonFingerprint!=fingerprint))throw new InvalidDataException("Contact protection belongs to another rig revision.");
        if(pins.Any(p=>p.Bone<0||p.Bone>=project.Scene.Bones.Length||p.Reference is not null||p.Clearance is not null||p.SurfacePoints.Length>0||p.SurfaceRegion.Length>0))throw new NotSupportedException("Surface/companion contact protection needs its complete dependency adapter; these constraints cannot be reduced to a bone trajectory.");
        var names=new HashSet<string>(StringComparer.Ordinal);
        void Add(int index){for(int bone=index;bone>=0;bone=project.Scene.Bones[bone].Parent)names.Add(project.Scene.Bones[bone].Name);}
        foreach(string name in settings.ContactBones)
        {
            int index=Array.FindIndex(project.Scene.Bones,b=>b.Name==name);
            if(index<0)throw new InvalidDataException("A reviewed support bone is absent from the current rig: "+name);
            Add(index);
        }
        foreach(var pin in pins)Add(pin.Bone);
        if(names.Count==0)throw new InvalidDataException("No existing contact trajectory was identified. Use FK fitting or review native support bindings first.");
        return project.Scene.Bones.Where(b=>names.Contains(b.Name)).Select(b=>b.Name).ToArray();
    }
}
