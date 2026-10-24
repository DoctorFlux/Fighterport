namespace FighterPort.Core;

// A character can exist before any scene. Existing FighterProject remains the
// strict, native-backed scene/animation aggregate; no empty skeleton is invented.
public record CharacterScene(Guid Id,string Name,Guid? Target,string? RevisionPath=null,string? Sha256=null)
{
    public Guid? Clip {get;init;}
    public double Seconds {get;init;}
    public int Bone {get;init;}
    public Guid? DerivedFrom {get;init;}
    public Guid AuthoringRevision {get;init;}=Guid.NewGuid();
    public Guid? DerivedRevision {get;init;}
    public bool NeedsSharedReview {get;init;}
    public Dictionary<string,Guid>? ActiveLayers {get;init;}
    public bool AutoMatchLayers {get;init;}=true;
    public bool? PreviewLoop {get;init;}
    public double PreviewSpeed {get;init;}=1;
}
public record ManagedNativeFile(string Sha256,string RelativePath,string[] OriginalPaths);
public record CharacterProject(int SchemaVersion,Guid Id,string Name,Guid Revision,TargetProfile[] Targets,CharacterScene[] Scenes)
{
    public const int CurrentSchema=2;
    public int MinimumReader {get;init;}=2;
    public Guid? ActiveTarget {get;init;}
    public Guid? ActiveScene {get;init;}
    public ManagedNativeFile[] NativeFiles {get;init;}=[];
    public string? MigratedFrom {get;init;}
}
public record WorkspaceSnapshot(CharacterProject Project,Dictionary<Guid,FighterProject> Scenes,long Version,Dictionary<Guid,long> ContentVersions);
public enum DirtyDecision { Save,Discard,Cancel }

public sealed class CharacterWorkspace
{
    readonly Dictionary<Guid,EditorDocument> documents=[];
    readonly Dictionary<Guid,FighterProject> content=[];
    readonly HashSet<Guid> changed=[];
    public CharacterProject Project {get;private set;}
    public string? Path {get;private set;}
    public string? SavedHash {get;private set;}
    public long Version {get;private set;}
    long savedVersion;
    public Guid Session {get;private set;}=Guid.NewGuid();
    public bool RequiresUpgradeCopy {get;private set;}
    public EditorDocument? Document=>Project.ActiveScene is {} id?OpenScene(id):null;
    public bool Dirty=>Version!=savedVersion||documents.Values.Any(d=>d.Dirty)||changed.Count>0;
    public event Action? Changed;
    public CharacterWorkspace(CharacterProject project,IReadOnlyDictionary<Guid,FighterProject>? scenes=null,string? path=null,string? savedHash=null)
    {
        Project=project;Path=path;SavedHash=savedHash;
        if(scenes is not null)foreach(var entry in scenes)content.Add(entry.Key,entry.Value);
        Validate();
    }
    public static CharacterWorkspace Empty(string name)
    {
        ValidateName(name);return new(new(CharacterProject.CurrentSchema,Guid.NewGuid(),name.Trim(),Guid.NewGuid(),[],[]));
    }
    public static CharacterWorkspace FromLegacy(FighterProject scene,string path)
    {
        var id=Guid.NewGuid();var project=new CharacterProject(2,scene.Id,scene.Name,Guid.NewGuid(),scene.Targets.Select(t=>t with{Name=string.IsNullOrWhiteSpace(t.Name)?t.Build.Game+" · "+t.Build.Build:t.Name,SceneIds=[]}).ToArray(),[new(id,"Existing Character",null)]){ActiveScene=id,MigratedFrom=System.IO.Path.GetFullPath(path)};
        var workspace=new CharacterWorkspace(project,new Dictionary<Guid,FighterProject>{{id,scene}},path){RequiresUpgradeCopy=true};return workspace;
    }
    public static void ValidateName(string name)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Trim()!=name||name.Length>100||name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars())>=0||name.EndsWith('.')||name.EndsWith(' '))throw new InvalidDataException("Choose a name without filename separators or trailing dots/spaces.");
    }
    public void Validate()
    {
        if(Project.SchemaVersion!=2||Project.MinimumReader>2)throw new InvalidDataException($"Unsupported character schema/reader {Project.SchemaVersion}/{Project.MinimumReader}; no rewrite was performed.");
        if(string.IsNullOrWhiteSpace(Project.Name)||Project.Name.Contains('\0'))throw new InvalidDataException("A character needs a valid display name.");
        if(Project.Targets.Select(t=>t.Id).Distinct().Count()!=Project.Targets.Length||Project.Scenes.Select(s=>s.Id).Distinct().Count()!=Project.Scenes.Length)throw new InvalidDataException("Duplicate character target or scene IDs.");
        foreach(var scene in Project.Scenes)
        {
            if(!content.ContainsKey(scene.Id))throw new InvalidDataException("Missing committed scene content.");
            if(scene.Target is {} owner&&!Project.Targets.Any(t=>t.Id==owner&&t.SceneIds.Contains(scene.Id)))throw new InvalidDataException("Scene ownership does not match its target.");
            ProjectStore.Validate(content[scene.Id]);
        }
        foreach(var target in Project.Targets)if(target.SceneIds.Distinct().Count()!=target.SceneIds.Length||target.SceneIds.Any(id=>!Project.Scenes.Any(s=>s.Id==id&&s.Target==target.Id)))throw new InvalidDataException("Invalid target-local scene assignment.");
        if(Project.ActiveTarget is {} targetId&&!Project.Targets.Any(t=>t.Id==targetId)||Project.ActiveScene is {} sceneId&&!Project.Scenes.Any(s=>s.Id==sceneId&&s.Target==Project.ActiveTarget))throw new InvalidDataException("Invalid active target/scene.");
    }
    EditorDocument OpenScene(Guid id)
    {
        if(documents.TryGetValue(id,out var existing))return existing;
        var saved=Project.Scenes.Single(s=>s.Id==id);var doc=new EditorDocument(content[id]);
        if(saved.Clip is {} clip){int index=Array.FindIndex(doc.Project.Clips,c=>c.Id==clip);if(index>=0)doc.SelectClip(index);}
        doc.Seek(saved.Seconds*doc.Clip.Rate.FramesPerSecond);doc.SelectedBone=Math.Clamp(saved.Bone,0,doc.Project.Scene.Bones.Length-1);
        doc.AutoMatchNativeLayers=saved.AutoMatchLayers;doc.Clock.LoopOverride=saved.PreviewLoop;doc.Clock.Speed=saved.PreviewSpeed;
        if(saved.ActiveLayers is not null){foreach(var kind in doc.ActiveLayers.Keys.ToArray())doc.SetLayer(kind,null);foreach(var layer in saved.ActiveLayers.Where(p=>p.Key!="SCN0"))doc.SetLayer(layer.Key,layer.Value);}
        doc.Changed+=()=>
        {
            if(!ReferenceEquals(content[id],doc.AuthoringProject))
            {
                content[id]=doc.AuthoringProject;changed.Add(id);var revision=Guid.NewGuid();Project=Project with{Scenes=Project.Scenes.Select(s=>s.Id==id?s with{AuthoringRevision=revision}:s.DerivedFrom==id?s with{NeedsSharedReview=true}:s).ToArray()};
            }
            Changed?.Invoke();
        };documents.Add(id,doc);return doc;
    }
    void Update(CharacterProject project){Project=project;Version++;Changed?.Invoke();}
    public void Rename(string name){ValidateName(name);if(name!=Project.Name)Update(Project with{Name=name});}
    void Remember()
    {
        if(Project.ActiveScene is not {} id||!documents.TryGetValue(id,out var doc))return;
        doc.CancelTransformEdit();doc.Clock.Playing=false;
        Project=Project with{Scenes=Project.Scenes.Select(s=>s.Id==id?s with{Clip=doc.Project.Clips.Length==0?null:doc.Clip.Id,Seconds=doc.Clock.Frame/doc.Clip.Rate.FramesPerSecond,Bone=doc.SelectedBone,ActiveLayers=doc.ActiveLayers.ToDictionary(),AutoMatchLayers=doc.AutoMatchNativeLayers,PreviewLoop=doc.Clock.LoopOverride,PreviewSpeed=doc.Clock.Speed}:s).ToArray()};
    }
    public void Activate(Guid? target,Guid? scene=null)
    {
        if(target is {} t&&!Project.Targets.Any(x=>x.Id==t))throw new ArgumentException("Unknown target.");
        var outgoing=Document;Guid? selected=outgoing?.Project.Clips.Length>0?outgoing.Clip.Id:null;double seconds=outgoing is null?0:outgoing.Clock.Frame/outgoing.Clip.Rate.FramesPerSecond;
        Remember();scene??=Project.Scenes.FirstOrDefault(s=>s.Target==target)?.Id;
        if(scene is {} id&&!Project.Scenes.Any(s=>s.Id==id&&s.Target==target))throw new ArgumentException("The selected scene belongs to another scope.");
        Project=Project with{ActiveTarget=target,ActiveScene=scene};Session=Guid.NewGuid();
        if(Document is {} incoming)
        {
            incoming.Clock.Playing=false;int match=selected is {} clip?Array.FindIndex(incoming.Project.Clips,c=>c.Id==clip):-1;
            if(match>=0){if(incoming.SelectedClip!=match)incoming.SelectClip(match);incoming.Seek(seconds*incoming.Clip.Rate.FramesPerSecond);}
        }
        Changed?.Invoke();
    }
    public Guid AddTarget(string name,SourceBuildIdentity build,string mode,string? framework=null)
    {
        ValidateName(name);if(Project.Targets.Any(t=>string.Equals(t.Name,name,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("A target with this name already exists.");
        var target=new TargetProfile(Guid.NewGuid(),build,mode){Name=name,Framework=framework};Update(Project with{Targets=Project.Targets.Append(target).ToArray()});return target.Id;
    }
    public Guid AddScene(FighterProject project,string? name=null)
    {
        ProjectStore.Validate(project);Remember();var scene=new CharacterScene(Guid.NewGuid(),name??project.Name,Project.ActiveTarget);content.Add(scene.Id,project);changed.Add(scene.Id);
        Update(Project with{Scenes=Project.Scenes.Append(scene).ToArray(),ActiveScene=scene.Id,Targets=Project.Targets.Select(t=>t.Id==scene.Target?t with{SceneIds=t.SceneIds.Append(scene.Id).ToArray()}:t).ToArray()});Session=Guid.NewGuid();return scene.Id;
    }
    public void ReplaceScene(FighterProject replacement)
    {
        var doc=Document??throw new InvalidOperationException("Select a model to replace.");
        if(AnimationControls.DeformChecksum(replacement.Scene)!=AnimationControls.DeformChecksum(doc.Project.Scene))throw new NotSupportedException("This replacement changes the existing deform rig/geometry. Add it as a separate model; compatible replacement requires exact native deform identity.");
        var model=replacement.SourceModels.FirstOrDefault();
        doc.Transaction("Replace compatible model",p=>p with{Scene=replacement.Scene,SourceModels=replacement.SourceModels,NativeResources=p.NativeResources.Concat(replacement.NativeResources).Distinct().ToArray(),Clips=p.Clips.Select(c=>(c.NativeTarget??c.Source) is {} basis&&model is not null?c with{NativeTarget=basis with{Model=model.Resource,SourceModelRevisionId=model.Id}}:c).ToArray()});
    }
    public Guid AssignIndependentCopy(Guid sharedScene,Guid target)
    {
        if(!Project.Scenes.Any(s=>s.Id==sharedScene&&s.Target is null))throw new InvalidOperationException("Choose an existing shared character scene.");
        var original=content[sharedScene];Activate(target);
        // Existing document edits replace immutable curves/records. Serialize to
        // independent arrays as well so later array access cannot cross scopes.
        using var stream=new MemoryStream();ProjectStore.WriteProject(stream,original,default);
        var copy=ProjectStore.ReadJson(System.Text.Encoding.UTF8.GetString(stream.ToArray())) with{Id=Guid.NewGuid()};
        var id=AddScene(copy);Update(Project with{Scenes=Project.Scenes.Select(s=>s.Id==id?s with{DerivedFrom=sharedScene,DerivedRevision=Project.Scenes.Single(source=>source.Id==sharedScene).AuthoringRevision}:s).ToArray()});return id;
    }
    public void ValidateNativeOutput(string path)
    {
        string destination=System.IO.Path.GetFullPath(path);
        if(content.Values.SelectMany(ProjectStore.PreservedSourcePaths).Any(source=>string.Equals(System.IO.Path.GetFullPath(source),destination,StringComparison.OrdinalIgnoreCase)))throw new IOException("Native output overlaps a preserved source owned by this character.");
        if(Path is null)return;var folders=new ProjectFolders(Path);
        if(string.Equals(destination,System.IO.Path.GetFullPath(Path),StringComparison.OrdinalIgnoreCase)||new[]{"Sources",".fighterport"}.Any(category=>destination.StartsWith(folders.Resolve(category)+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))throw new IOException("Native output overlaps the character descriptor or preserved immutable project inputs/revisions.");
        foreach(var other in Project.Targets.Where(t=>t.Id!=Project.ActiveTarget))
        {
            string targetRoot=folders.Resolve("Targets/"+other.Id.ToString("N"));string buildRoot=folders.Resolve("Build/"+other.Id.ToString("N"));
            if(destination.StartsWith(targetRoot+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||destination.StartsWith(buildRoot+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Native output belongs to another target's workspace or build.");
        }
    }
    public WorkspaceSnapshot Snapshot()
    {
        Remember();return new(Project with{Revision=Guid.NewGuid()},content.ToDictionary(),Version,documents.ToDictionary(p=>p.Key,p=>p.Value.ContentVersion));
    }
    public Task SaveAsync(string? path=null,CancellationToken cancellation=default)=>SaveSnapshotAsync(Snapshot(),path,cancellation);
    public async Task SaveSnapshotAsync(WorkspaceSnapshot snapshot,string? path=null,CancellationToken cancellation=default)
    {
        if(snapshot.Project.Id!=Project.Id)throw new InvalidOperationException("The save snapshot belongs to another character session.");
        path=System.IO.Path.GetFullPath(path??Path??throw new InvalidOperationException("Choose a character project path."));
        if(RequiresUpgradeCopy&&string.Equals(path,Path,StringComparison.OrdinalIgnoreCase))throw new IOException("Save an upgraded copy; the original schema-1 file remains unchanged.");
        var committed=await CharacterStore.SaveAsync(snapshot,path,string.Equals(path,Path,StringComparison.OrdinalIgnoreCase)?SavedHash:null,cancellation);
        Path=path;SavedHash=ProjectStore.Hash(path);RequiresUpgradeCopy=false;
        // A completed older save cannot clear a newer edit or target change.
        if(Version==snapshot.Version){Project=Project with{Revision=committed.Revision,NativeFiles=committed.NativeFiles};savedVersion=snapshot.Version;}
        foreach(var entry in snapshot.ContentVersions)if(documents.TryGetValue(entry.Key,out var doc)&&doc.ContentVersion==entry.Value){doc.MarkSaved();changed.Remove(entry.Key);}
        foreach(var entry in snapshot.Scenes)if(!documents.ContainsKey(entry.Key)&&ReferenceEquals(content[entry.Key],entry.Value))changed.Remove(entry.Key);
        Changed?.Invoke();
    }
    public async Task<bool> ResolveDirtyAsync(DirtyDecision decision,Func<Task> save)
    {
        if(!Dirty)return true;if(decision==DirtyDecision.Cancel)return false;if(decision==DirtyDecision.Discard)return true;
        await save();return !Dirty;
    }
}
