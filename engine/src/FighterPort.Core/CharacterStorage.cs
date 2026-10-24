using System.Text.Json;
using System.Text.Json.Serialization;

namespace FighterPort.Core;

public sealed class ProjectFolders(string descriptor)
{
    public string Root {get;}=Path.GetDirectoryName(Path.GetFullPath(descriptor))!;
    public string Resolve(string relative)
    {
        if(Path.IsPathRooted(relative))throw new InvalidDataException("Managed paths must be project relative.");
        var full=Path.GetFullPath(Path.Combine(Root,relative));
        string prefix=Path.EndsInDirectorySeparator(Root)?Root:Root+Path.DirectorySeparatorChar;
        if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("A managed path escaped the character folder.");
        return full;
    }
    public string Category(string relative){var path=Resolve(relative);Directory.CreateDirectory(path);return path;}
    public void Initialize(){foreach(var folder in new[]{"Character","Sources","ProjectSettings","Targets","Build","Reports","Cache",".fighterport",".local"})Category(folder);}
}
public static class CharacterStore
{
    public static async Task<CharacterWorkspace> CreateAsync(string name,string folder,CancellationToken cancellation=default)
    {
        CharacterWorkspace.ValidateName(name);folder=Path.GetFullPath(folder);
        if(Directory.Exists(folder)&&Directory.EnumerateFileSystemEntries(folder).Any())throw new IOException("Choose an empty new project folder, or open the character already stored there.");
        Directory.CreateDirectory(folder);var workspace=CharacterWorkspace.Empty(name);await workspace.SaveAsync(Path.Combine(folder,name+".fighterport"),cancellation);return workspace;
    }
    public static async Task<CharacterWorkspace> LoadAsync(string path,CancellationToken cancellation=default)
    {
        path=Path.GetFullPath(path);return await Task.Run(async()=>
        {
            using var stream=File.OpenRead(path);using var json=await JsonDocument.ParseAsync(stream,cancellationToken:cancellation);
            int schema=json.RootElement.GetProperty("SchemaVersion").GetInt32();
            if(schema==1)return CharacterWorkspace.FromLegacy(ProjectStore.ReadJson(json.RootElement.GetRawText()),path);
            if(schema!=2)throw new InvalidDataException($"Unsupported character schema {schema}; the original was not rewritten.");
            var project=json.RootElement.Deserialize<CharacterProject>(ProjectStore.Json)??throw new InvalidDataException("Empty character descriptor.");var folders=new ProjectFolders(path);
            if(project.MinimumReader>CharacterProject.CurrentSchema)throw new InvalidDataException("This character requires a newer reader; its files were not rewritten.");
            foreach(var file in project.NativeFiles){cancellation.ThrowIfCancellationRequested();var input=folders.Resolve(file.RelativePath);if(!File.Exists(input)||ProjectStore.Hash(input)!=file.Sha256)throw new InvalidDataException("Missing/changed preserved native input: "+file.RelativePath);}
            var content=new Dictionary<Guid,FighterProject>();var options=new JsonSerializerOptions(ProjectStore.Json);var native=project.NativeFiles.ToDictionary(f=>f.Sha256);options.Converters.Insert(0,new NativeResolver(native,folders));options.Converters.Insert(0,new ProofResolver(native,folders));
            foreach(var scene in project.Scenes)
            {
                cancellation.ThrowIfCancellationRequested();var input=folders.Resolve(scene.RevisionPath??throw new InvalidDataException("Missing committed scene revision."));
                if(!File.Exists(input)||ProjectStore.Hash(input)!=scene.Sha256)throw new InvalidDataException("Missing/changed committed scene revision: "+scene.Name);
                content.Add(scene.Id,await ProjectStore.LoadResolvedAsync(input,options,cancellation));
            }
            return new CharacterWorkspace(project,content,path,ProjectStore.Hash(path));
        },cancellation);
    }
    // Originals retain their provenance while runtime paths resolve through the
    // committed hash ledger. Rebased paths never authorize changed source bytes.
    sealed class NativeResolver(Dictionary<string,ManagedNativeFile> files,ProjectFolders folders):JsonConverter<NativeResourceRef>
    {
        public override NativeResourceRef Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
        {
            var native=JsonSerializer.Deserialize<NativeResourceRef>(ref reader,ProjectStore.Json)??throw new JsonException("Empty native identity.");
            return files.TryGetValue(native.Sha256,out var file)?native with{OriginalPath=native.OriginalPath??native.Path,Path=folders.Resolve(file.RelativePath)}:native;
        }
        public override void Write(Utf8JsonWriter writer,NativeResourceRef value,JsonSerializerOptions options)=>JsonSerializer.Serialize(writer,value,ProjectStore.Json);
    }
    sealed class ProofResolver(Dictionary<string,ManagedNativeFile> files,ProjectFolders folders):JsonConverter<SourceFileProof>
    {
        public override SourceFileProof Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
        {
            var proof=JsonSerializer.Deserialize<SourceFileProof>(ref reader,ProjectStore.Json)??throw new JsonException("Empty source proof.");return files.TryGetValue(proof.Sha256,out var file)?proof with{CachePath=folders.Resolve(file.RelativePath)}:proof;
        }
        public override void Write(Utf8JsonWriter writer,SourceFileProof value,JsonSerializerOptions options)=>JsonSerializer.Serialize(writer,value,ProjectStore.Json);
    }
    internal static async Task<CharacterProject> SaveAsync(WorkspaceSnapshot snapshot,string path,string? expectedHash,CancellationToken cancellation)
    {
        var folders=new ProjectFolders(path);folders.Initialize();string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        // Separate lock and immutable revision files: descriptor replacement never
        // exposes partially overwritten child records. Keep prior revisions intact.
        using var writeLock=new FileStream(folders.Resolve(".fighterport/write.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        return await Task.Run(async()=>
        {
            try
            {
                _=new CharacterWorkspace(snapshot.Project,snapshot.Scenes);
                if(File.Exists(path)&&(expectedHash is null||ProjectStore.Hash(path)!=expectedHash))throw new IOException("The destination already exists or changed outside this session. Save Copy or reopen to reconcile it.");
                var native=new Dictionary<string,ManagedNativeFile>(StringComparer.Ordinal);
                foreach(var group in snapshot.Scenes.Values.SelectMany(ProjectStore.SourceReferences).GroupBy(r=>r.Sha256))
                {
                    cancellation.ThrowIfCancellationRequested();var first=group.First();string source=group.Select(r=>r.Path).FirstOrDefault(File.Exists)??throw new FileNotFoundException("Preserved native input is unavailable.",first.Path);
                    if(ProjectStore.Hash(source)!=group.Key)throw new IOException("Native input changed; original hash does not match: "+Path.GetFileName(source));
                    string relative="Sources/Originals/"+group.Key+"/"+Path.GetFileName(source);string destination=folders.Resolve(relative);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    if(!File.Exists(destination))
                    {
                        string stage=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
                        try{await using(var input=File.OpenRead(source))await using(var output=new FileStream(stage,FileMode.CreateNew,FileAccess.Write,FileShare.None)){await input.CopyToAsync(output,cancellation);output.Flush(true);}if(ProjectStore.Hash(stage)!=group.Key)throw new IOException("Staged native hash mismatch.");File.Move(stage,destination,false);}
                        finally{if(File.Exists(stage))File.Delete(stage);}
                    }
                    if(ProjectStore.Hash(destination)!=group.Key)throw new IOException("Managed original was externally changed.");
                    native.Add(group.Key,new(group.Key,relative,group.Select(r=>r.OriginalPath??r.Path).Concat(snapshot.Project.NativeFiles.Where(f=>f.Sha256==group.Key).SelectMany(f=>f.OriginalPaths)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
                }
                string revision=".fighterport/revisions/"+snapshot.Project.Revision.ToString("N");folders.Category(revision);var scenes=new List<CharacterScene>();
                foreach(var scene in snapshot.Project.Scenes)
                {
                    cancellation.ThrowIfCancellationRequested();string relative=revision+"/"+scene.Id.ToString("N")+".fighterport";string child=folders.Resolve(relative);
                    using(var output=new FileStream(child,FileMode.CreateNew,FileAccess.Write,FileShare.None)){ProjectStore.Validate(snapshot.Scenes[scene.Id]);ProjectStore.WriteProject(output,snapshot.Scenes[scene.Id],cancellation);output.Flush(true);}
                    // Reopen the actual persisted representation, including pooled
                    // authentic contexts, before publishing its descriptor.
                    await ProjectStore.LoadAsync(child,cancellation);scenes.Add(scene with{RevisionPath=relative,Sha256=ProjectStore.Hash(child)});
                }
                var committed=snapshot.Project with{Scenes=scenes.ToArray(),NativeFiles=native.Values.ToArray()};
                using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(output,committed,ProjectStore.Json);output.Flush(true);}
                cancellation.ThrowIfCancellationRequested();
                if(expectedHash is not null&&(!File.Exists(path)||ProjectStore.Hash(path)!=expectedHash))throw new IOException("Character changed during save. The previous committed revision remains available.");
                File.Move(temp,path,true);return committed;
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        },cancellation);
    }
}
