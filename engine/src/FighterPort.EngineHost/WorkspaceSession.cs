using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

sealed record WorkspaceClip(string Entry,string Name,string Key,string? Id=null);
sealed record WorkspaceMotion(string Path,WorkspaceClip[] Clips);
sealed partial class ControlSession
{
    const string EditorPackageFormat="FighterPort.Next.Project";
    const string EditorClipPrefix="FighterPort.Next.Clip/";
    CharacterWorkspace? workspace;
    async Task<object> Workspace(Request r)
    {
        var diagnostics=new List<string>();
        switch(r.Operation)
        {
            case "workspace-create":workspace=await CharacterStore.CreateAsync(r.Name??"Character",r.Path!);return WorkspaceReply();
            case "workspace-open":workspace=await CharacterStore.LoadAsync(r.Path!);return WorkspaceReply();
        }
        var w=workspace??throw new InvalidOperationException("Create or open a character workspace first.");
        switch(r.Operation)
        {
            case "workspace-target":w.AddTarget(r.Name!,new(r.Game??"Unresolved",r.Platform??"Unresolved",r.Build??"Unresolved"),r.Mode??"Unresolved");break;
            case "workspace-select":w.Activate(Parse(r.TargetId),Parse(r.SceneId));break;
            case "workspace-copy":w.AssignIndependentCopy(Guid.Parse(r.SceneId!),Guid.Parse(r.TargetId!));break;
            case "workspace-save":await w.SaveAsync(r.Path);workspace=await CharacterStore.LoadAsync(w.Path!);break;
            case "workspace-import-project":w.AddScene(await ProjectStore.LoadAsync(r.Path!));break;
            case "workspace-add":
            case "workspace-update":
            {
                if(r.Package is null||!File.Exists(r.Package))throw new FileNotFoundException("The native editor snapshot is missing.");
                var imported=await adapter.ImportModelAsync(r.Model!);var clips=new List<(AnimationClip Clip,string Key,string? Id)>();var resources=imported.NativeResources.ToList();var layers=imported.NativeLayers.ToList();
                if(r.Operation=="workspace-update")
                {
                    var prior=w.Document?.Project??throw new InvalidOperationException("Select a scene before updating its native revision.");
                    if(AnimationControls.DeformChecksum(prior.Scene)!=AnimationControls.DeformChecksum(imported.Scene))
                    {
                        if(AnimationControls.Fingerprint(prior.Scene)!=AnimationControls.Fingerprint(imported.Scene))throw new InvalidOperationException("The native deform rig changed. Add it as an independent scene instead.");
                        // Older cached geometry can differ from current native
                        // matrix evaluation. Never relax a numeric bound or replace
                        // it: prove the new native model against the exact authentic
                        // resource with today's reader and preserve the stored scene.
                        var authentic=prior.SourceModels.FirstOrDefault(m=>m.Resource.Format=="MDL0")?.Resource??throw new InvalidOperationException("Missing authentic native model identity.");
                        if(!File.Exists(authentic.Path)||ProjectStore.Hash(authentic.Path)!=authentic.Sha256)throw new InvalidDataException("The authentic native model changed or is missing.");
                        var reference=await adapter.ImportModelAsync(authentic.Path,modelEntry:authentic.Entry);
                        if(AnimationControls.DeformChecksum(reference.Scene)!=AnimationControls.DeformChecksum(imported.Scene))throw new InvalidOperationException("The native deform geometry changed. Add it as an independent scene instead.");
                        diagnostics.Add("The native model matches its exact authentic resource. Earlier cached Core geometry remains preserved independently; it is not replaced or normalized by this native animation save.");
                    }
                }
                foreach(var motion in r.Motions??[])
                {
                    var content=await adapter.ImportAsync(r.Model!,motion.Path);resources.AddRange(content.NativeResources);layers.AddRange(content.NativeLayers);
                    foreach(var clip in content.Clips)
                    {
                        var candidates=motion.Clips.Where(c=>Relative(c.Entry)==Relative(clip.Source!.Resource.Entry)).ToArray();
                        if(candidates.Length==0)candidates=motion.Clips.Where(c=>c.Name==(clip.Source!.NativeName??clip.Name)).ToArray();
                        if(candidates.Length!=1)throw new InvalidDataException("Review ambiguous native animation identity: "+clip.Name);
                        clips.Add((clip,candidates[0].Key,candidates[0].Id));
                    }
                }
                var package=new NativeResourceEntry("Native editor document",new(System.IO.Path.GetFullPath(r.Package),"project.fpstudio",ProjectStore.Hash(r.Package),EditorPackageFormat,1),"EditorDocument");
                if(r.Operation=="workspace-add")
                {
                    imported=imported with{Name=r.Name??imported.Name,Clips=clips.Select(c=>c.Clip).ToArray(),NativeResources=resources.Concat(clips.Select(c=>new NativeResourceEntry(c.Key,c.Clip.Source!.Resource,EditorClipPrefix+c.Clip.Id))).Append(package).ToArray(),NativeLayers=layers.ToArray()};
                    w.AddScene(imported,r.Name);
                }
                else
                {
                    var doc=w.Document??throw new InvalidOperationException("Select a scene before updating its native revision.");var original=doc.Project;
                    var prior=original.NativeResources.Where(n=>n.NodeType.StartsWith(EditorClipPrefix,StringComparison.Ordinal)).ToDictionary(n=>n.Name,n=>Guid.Parse(n.NodeType[EditorClipPrefix.Length..]));
                    var replacement=new List<AnimationClip>();var identities=new List<NativeResourceEntry>();
                    foreach(var item in clips)
                    {
                        var old=prior.TryGetValue(item.Key,out var id)?original.Clips.SingleOrDefault(c=>c.Id==id):null;
                        if(old is null&&item.Id is not null)
                            old=original.Clips.SingleOrDefault(c=>c.Id==Guid.Parse(item.Id))??throw new InvalidDataException("The saved workspace animation identity belongs to a different scene.");
                        if(old is null)
                        {
                            var matching=original.Clips.Where(c=>c.Name==item.Clip.Name).ToArray();if(matching.Length>1)throw new InvalidDataException("Review duplicate native clip names before updating this legacy scene.");old=matching.SingleOrDefault();
                        }
                        var changed=old is null?item.Clip:old with{Name=item.Clip.Name,Tracks=item.Clip.Tracks,SampleCount=item.Clip.SampleCount,Loop=item.Clip.Loop,Rate=item.Clip.Rate,NativeTarget=item.Clip.Source,NativeWrite=NativeWriteMode.PreserveSource,NativeWriteName=item.Clip.Source?.NativeName??item.Clip.Name,RewriteNativeChannels=true,Revision=Guid.NewGuid(),Retarget=old.Retarget is {} recipe?recipe with{Validation=null,IsDraft=true}:null};
                        replacement.Add(changed);identities.Add(new(item.Key,item.Clip.Source!.Resource,EditorClipPrefix+changed.Id));
                    }
                    // Unrepresented contracts, sources, layers and unsupported
                    // clips survive; native snapshots cannot silently erase them.
                    replacement.AddRange(original.Clips.Where(c=>replacement.All(n=>n.Id!=c.Id)));
                    doc.Transaction("Commit native editor revision",p=>p with{Clips=replacement.ToArray(),NativeResources=p.NativeResources.Where(n=>n.Resource.Format!=EditorPackageFormat&&!n.NodeType.StartsWith(EditorClipPrefix,StringComparison.Ordinal)).Concat(resources).Concat(identities).Append(package).Distinct().ToArray(),SourceModels=p.SourceModels.Concat(imported.SourceModels).Distinct().ToArray()});
                }
                if(w.Document is {} active)
                {
                    if(r.Slots is not null)
                    {
                        var nativeIds=active.Project.NativeResources.Where(n=>n.NodeType.StartsWith(EditorClipPrefix,StringComparison.Ordinal)).ToDictionary(n=>n.Name,n=>Guid.Parse(n.NodeType[EditorClipPrefix.Length..]));
                        active.Transaction("Commit custom native slot assignments",p=>AnimationSlots.Synchronize(p,r.Slots,nativeIds));
                    }
                    var identity=active.Project.NativeResources.SingleOrDefault(n=>n.Name==r.ClipKey&&n.NodeType.StartsWith(EditorClipPrefix,StringComparison.Ordinal));
                    if(identity is not null){var clipId=Guid.Parse(identity.NodeType[EditorClipPrefix.Length..]);int index=Array.FindIndex(active.Project.Clips,c=>c.Id==clipId);if(index>=0)active.SelectClip(index);}
                    active.Seek(r.Frame);
                }
                break;
            }
            case "workspace-export":
            {
                var p=w.Document?.Project??throw new InvalidOperationException("Select a model in the workspace.");string output=System.IO.Path.GetFullPath(r.Output!);
                if(Directory.Exists(output)||File.Exists(output))throw new IOException("Choose a new empty native preview directory.");Directory.CreateDirectory(output);
                var package=p.NativeResources.LastOrDefault(n=>n.Resource.Format==EditorPackageFormat)?.Resource;
                if(package is not null)
                {
                    if(!File.Exists(package.Path)||ProjectStore.Hash(package.Path)!=package.Sha256)throw new InvalidDataException("The saved native editor package changed or is missing.");
                    string copy=System.IO.Path.Combine(output,"editor.zip");File.Copy(package.Path,copy,false);return new{Ok=true,Workspace=WorkspaceInfo(),Package=copy};
                }
                var model=p.SourceModels.FirstOrDefault(m=>m.Resource.Format=="MDL0")?.Resource??throw new NotSupportedException("This scene needs a native model reader before it can open in the Brawl viewer. Its workspace data is preserved.");
                if(!File.Exists(model.Path)||ProjectStore.Hash(model.Path)!=model.Sha256)throw new InvalidDataException("The authentic native model changed or is missing.");
                var motions=new List<string>();var controls=new List<object>();var setup=p.ControlRigs.FirstOrDefault(s=>s.SkeletonFingerprint==AnimationControls.Fingerprint(p.Scene))??BrawlControlProfiles.SetUp(p);
                var originals=new Dictionary<string,FighterProject>(StringComparer.OrdinalIgnoreCase);string? selectedMotion=null,selectedEntry=null;
                foreach(var clip in p.Clips)
                {
                    var basis=clip.NativeTarget??clip.Source;AnimationClip? authentic=null;
                    if(basis is not null)
                    {
                        if(!originals.TryGetValue(basis.Resource.Path,out var original)){original=await adapter.ImportAsync(model.Path,basis.Resource.Path);originals.Add(basis.Resource.Path,original);}
                        authentic=original.Clips.SingleOrDefault(c=>c.Source?.Resource.Entry==basis.Resource.Entry);
                    }
                    string file,entry;
                    if(authentic is not null&&SameNativeCurves(clip,authentic))
                    {
                        file=basis!.Resource.Path;entry=basis.Resource.Entry;if(!motions.Contains(file,StringComparer.OrdinalIgnoreCase))motions.Add(file);
                    }
                    else{file=System.IO.Path.Combine(output,clip.Id.ToString("N")+".chr0");await adapter.ExportAsync(p,clip,file);motions.Add(file);entry="root";}
                    string state=JsonSerializer.Serialize(new ControlState(setup.SkeletonFingerprint,clip.Id,setup,p.Contacts.Where(c=>c.Clip==clip.Id).ToArray(),[],p.BonePins?.Where(b=>b.Clip==clip.Id).ToArray(),p.AuthoringPose),ProjectStore.Json);
                    controls.Add(new{Path=file,Entry=entry,State=state,Id=clip.Id});
                    if(clip.Id==w.Document!.Clip.Id){selectedMotion=file;selectedEntry=entry;}
                }
                string poseState=JsonSerializer.Serialize(new ControlState(setup.SkeletonFingerprint,Guid.NewGuid(),setup,[],p.AuthoringContacts,p.BonePins?.Where(b=>b.Clip is null).ToArray(),p.AuthoringPose),ProjectStore.Json);
                return new{Ok=true,Workspace=WorkspaceInfo(),Model=model.Path,ModelKey=model.Entry,MotionPaths=motions.ToArray(),WorkspaceControls=controls.ToArray(),PoseState=poseState,SelectedMotion=selectedMotion,SelectedEntry=selectedEntry,DisplayFrame=p.Clips.Length==0?0:(int)Math.Floor(w.Document!.Clock.Frame)+1,Diagnostics=p.NativeLayers.Any(l=>l.Edited)?new[]{"Edited companion-layer contracts remain in the character workspace. This legacy preview uses their preserved native resources; use the qualified motion writer for authored layer/geometry revisions."}:Array.Empty<string>()};
            }
            default:throw new NotSupportedException("Unknown workspace operation.");
        }
        return new{Ok=true,Workspace=WorkspaceInfo(),Diagnostics=diagnostics.ToArray()};
        static Guid? Parse(string? id)=>string.IsNullOrEmpty(id)?null:Guid.Parse(id);
        static string Relative(string entry){int slash=entry.IndexOf('/');return slash<0?entry:entry[(slash+1)..];}
    }
    object WorkspaceReply()=>new{Ok=true,Workspace=WorkspaceInfo()};
    static bool SameNativeCurves(AnimationClip a,AnimationClip b)
    {
        if((a.NativeWriteName??a.Source?.NativeName??a.Name)!=(b.NativeWriteName??b.Source?.NativeName??b.Name)||a.SampleCount!=b.SampleCount||a.Loop!=b.Loop||a.Rate!=b.Rate||a.Tracks.Length!=b.Tracks.Length)return false;
        foreach(var track in a.Tracks)
        {
            var other=b.Tracks.SingleOrDefault(t=>t.Bone==track.Bone);if(other is null||track.Rotation is not null||other.Rotation is not null)return false;
            for(int i=0;i<9;i++){var x=track.Channels[i];var y=other.Channels[i];if(x is null&&y is null)continue;if(x is null||y is null||x.Interpolation!=y.Interpolation||x.LoopFrameLimit!=y.LoopFrameLimit||!x.Keys.SequenceEqual(y.Keys))return false;}
        }
        return true;
    }
    object WorkspaceInfo()
    {
        var w=workspace!;return new{w.Project.Id,w.Project.Name,w.Project.ActiveScene,w.Project.ActiveTarget,w.Project.Targets,w.Project.Scenes,w.Path,w.Dirty,w.RequiresUpgradeCopy,ClipIds=w.Document?.Project.Clips.Select(c=>c.Id).ToArray()??[]};
    }
}
