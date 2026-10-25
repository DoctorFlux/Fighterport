namespace FighterPort.Core;

// Conservative contact-aware editing. Keeping every ancestor's original S/R/T
// curves preserves the exact evaluated point/orientation at every time, including
// native subframes and loop seams. This deliberately does not invent IK goals.
public static class ContactBoundaryProtection
{
    // Explicit adaptation of reviewed baked hip/leg paths only. An authored
    // constraint is never converted into permission to release its anchor.
    public static string[] ResolveExitRecovery(FighterProject project,AnimationClip clip,BoundaryFitSettings settings,
        ControlRigSetup? controls,ISet<string> protectedMotion,string[] contactBones,int join,int? entryJoin=null)
    {
        if(!settings.ExitContactRecovery&&!settings.BoundaryContactRecovery)return [];
        var scene=project.Scene;string fingerprint=AnimationControls.Fingerprint(scene);
        int body=controls is null?-1:AnimationControls.ResolveBodyBone(scene,controls);
        var legs=controls?.Chains.Where(c=>c.Role is "Foot.L" or "Foot.R").ToArray()??[];
        if(controls is null||controls.SkeletonFingerprint!=fingerprint||body<0||
            !controls.BoneRoles.Any(r=>r.Reviewed&&r.SemanticRole=="Hips"&&r.Bone==body)||legs.Length==0||legs.Select(c=>c.Role).Distinct(StringComparer.Ordinal).Count()!=legs.Length||
            legs.Any(c=>!c.Reviewed||new[]{c.Root,c.Middle,c.Effector}.Any(i=>i<0||i>=scene.Bones.Length)||
                scene.Bones[c.Middle].Parent!=c.Root||scene.Bones[c.Effector].Parent!=c.Middle||!PoseGeometry.Ancestor(scene,body,c.Root)))
            throw new InvalidDataException("Boundary contact recovery requires exact reviewed body and leg bindings for this rig revision.");
        var eligible=Enumerable.Range(0,scene.Bones.Length).Where(i=>i==body||legs.Any(c=>PoseGeometry.Ancestor(scene,c.Root,i)||
            PoseGeometry.Ancestor(scene,body,i)&&PoseGeometry.Ancestor(scene,i,c.Root))).Select(i=>scene.Bones[i].Name).ToHashSet(StringComparer.Ordinal);
        if(settings.MatchReferenceControls)eligible.UnionWith(BoundaryReferenceControls.Resolve(scene,controls));
        var released=contactBones.Where(name=>!protectedMotion.Contains(name)).ToArray();
        if(released.Length==0)throw new InvalidDataException("No reviewed body/leg contact dependency path is available for boundary recovery.");
        if(released.Any(name=>!eligible.Contains(name)))throw new NotSupportedException("Boundary contact recovery is bounded to reviewed hip/leg dependency paths. Hand, surface or other support paths require a separate recovery adapter.");
        if(released.Any(name=>!settings.Bones.Contains(name,StringComparer.Ordinal)))throw new InvalidDataException("Boundary contact recovery requires the complete reviewed contact dependency mask, excluding protected motion controls.");
        var edited=settings.Bones.Where(name=>!protectedMotion.Contains(name)).Select(name=>Array.FindIndex(scene.Bones,b=>b.Name==name)).ToArray();
        bool Affected(int bone)=>edited.Any(ancestor=>PoseGeometry.Ancestor(scene,ancestor,bone));
        foreach(var pin in project.AuthoringContacts)
            if(Affected(pin.Bone))throw new NotSupportedException("An authored fixed/soft contact pin intersects boundary recovery. Remove or explicitly reauthor that constraint first; no anchor is released.");
        foreach(var frame in project.Contacts.Where(c=>c.Clip==clip.Id&&c.Pins.Length>0))
        {
            double end=frame.EndFrame??frame.Frame;
            if(!double.IsFinite(frame.Frame)||!double.IsFinite(end)||frame.Frame<0||end<frame.Frame||end>clip.LastSampleFrame)
                throw new InvalidDataException("An authored contact interval is invalid; it cannot authorize recovery.");
            if((settings.BoundaryContactRecovery?(entryJoin.HasValue&&frame.Frame<entryJoin.Value||settings.ExitSamples>0&&end>join):end>join)&&frame.Pins.Any(pin=>Affected(pin.Bone)))throw new NotSupportedException("A fixed/soft contact interval intersects the boundary recovery window. Its anchor and interval remain unchanged.");
        }
        foreach(var pin in project.BonePins??[])
            if(pin.Clip is null||pin.Clip==clip.Id)
            {
                if(pin.SkeletonFingerprint!=fingerprint||pin.Bone<0||pin.Bone>=scene.Bones.Length)throw new InvalidDataException("An editor pin belongs to a changed rig revision.");
                if(Affected(pin.Bone))throw new NotSupportedException("An existing-bone editor pin intersects boundary recovery. Remove or explicitly reauthor that pin first.");
            }
        return released;
    }

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
