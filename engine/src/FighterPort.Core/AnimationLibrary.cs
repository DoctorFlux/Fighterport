using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FighterPort.Core;

public record LibrarySourceDescription(string Provider,SourceBuildIdentity Build,string Character,string Model,string AssociationEvidence,bool Reviewed)
{
    public bool BuildVerified {get;init;}
}
public record LibraryInput(string Role,string Path);
public record LibraryBlob(string OriginalPath,string Sha256,string FileName,long Length);
public record LibraryClip(Guid Id,Guid Revision,string Name,int Samples,RationalRate Rate,bool Loop,NativeResourceRef? Native);
public record UnresolvedLibraryClip(string Name,NativeResourceRef? Native,string[] MissingBones);
public record LibrarySource(Guid Id,string Identity,LibrarySourceDescription Description,string PackageHash,string SkeletonFingerprint,LibraryClip[] Clips,LibraryBlob[] Originals)
{
    public UnresolvedLibraryClip[] Unresolved {get;init;}=[];
}
public record LibraryCatalogue(int Version,LibrarySource[] Sources);

// Prepared packages and content-addressed originals are private data, independent of any fighter.
// Adapters prepare native data; this store has no UI or native node dependencies.
public sealed class AnimationLibraryStore(string root)
{
    public string Root {get;}=Path.GetFullPath(root);
    public static string DefaultRoot=>Path.Combine(Environment.GetEnvironmentVariable("FIGHTERPORT_DATA")??Path.Combine(AppContext.BaseDirectory,"local"),"animation-library");
    string Index=>Path.Combine(Root,"catalogue.json");
    // Offline publication/commit guard. Raw/prepared proofs are verified by
    // OpenClipAsync; a re-registration must also invalidate an in-flight request.
    public bool IsCurrent(LibrarySource registration)
    {
        try{if(!File.Exists(Index))return false;var catalogue=JsonSerializer.Deserialize<LibraryCatalogue>(File.ReadAllText(Index),ProjectStore.Json);var current=catalogue?.Sources.SingleOrDefault(s=>s.Id==registration.Id);return current is not null&&current.PackageHash==registration.PackageHash&&current.Identity==registration.Identity&&current.SkeletonFingerprint==registration.SkeletonFingerprint&&current.Description==registration.Description&&current.Clips.SequenceEqual(registration.Clips)&&current.Originals.SequenceEqual(registration.Originals);}
        catch(IOException){return false;}catch(JsonException){return false;}
    }
    // The already prepared model stays in memory. Apply only needs to prove the
    // frozen registration and its immutable bytes, not decode the whole library again.
    public async Task VerifySourceAsync(LibrarySource registration,CancellationToken ct=default)
    {
        ct.ThrowIfCancellationRequested();
        if(registration.Description.Provider=="native.brawl.v1")throw new InvalidDataException("This prepared Brawl source predates the exact float decoder. Re-register its preserved original model/motion pair; existing target keys and original bytes remain preserved.");
        if(!IsCurrent(registration))throw new InvalidDataException("The library source registration or clip manifest changed. Reprepare the batch.");
        foreach(var blob in registration.Originals)await VerifyBlob(blob,ct);
        await using(var stream=File.OpenRead(Package(registration.PackageHash)))
        {
            if(stream.Length>256L*1024*1024||Convert.ToHexString(await SHA256.HashDataAsync(stream,ct))!=registration.PackageHash)throw new InvalidDataException("Prepared source package hash mismatch.");
        }
        if(!IsCurrent(registration))throw new InvalidDataException("The library source changed during proof verification. Reprepare the batch.");
    }
    static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    static void HashName(string hash){if(hash.Length!=64||hash.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException("Invalid library content hash.");}
    string Package(string hash){HashName(hash);return Path.Combine(Root,"prepared",hash+".fighterport");}
    string Blob(LibraryBlob blob)
    {
        HashName(blob.Sha256);if(string.IsNullOrEmpty(blob.FileName)||Path.GetFileName(blob.FileName)!=blob.FileName||blob.FileName is "." or "..")throw new InvalidDataException("Invalid library blob filename.");
        return Path.Combine(Root,"originals",blob.Sha256,blob.FileName);
    }
    public async Task<LibraryCatalogue> LoadAsync(CancellationToken ct=default)
    {
        if(!File.Exists(Index))return new(1,[]);
        var catalogue=JsonSerializer.Deserialize<LibraryCatalogue>(await File.ReadAllTextAsync(Index,ct),ProjectStore.Json)??throw new InvalidDataException("Empty animation library catalogue.");
        if(catalogue.Version!=1||catalogue.Sources is null||catalogue.Sources.Select(s=>s.Id).Distinct().Count()!=catalogue.Sources.Length)throw new InvalidDataException("Invalid or unsupported animation library catalogue.");
        foreach(var source in catalogue.Sources){HashName(source.PackageHash);if(source.Clips.Select(c=>c.Id).Distinct().Count()!=source.Clips.Length)throw new InvalidDataException("Duplicate clip identity in library source.");foreach(var blob in source.Originals)_=Blob(blob);}
        return catalogue;
    }
    static IEnumerable<JsonObject> NativeReferences(JsonNode? node)
    {
        if(node is JsonObject obj)
        {
            if(obj.ContainsKey("Path")&&obj.ContainsKey("Entry")&&obj.ContainsKey("Sha256")&&obj.ContainsKey("Format")&&obj.ContainsKey("Version"))yield return obj;
            foreach(var value in obj.ToArray())foreach(var item in NativeReferences(value.Value))yield return item;
        }
        else if(node is JsonArray array)foreach(var value in array)foreach(var item in NativeReferences(value))yield return item;
    }
    static IEnumerable<JsonObject> FileProofs(JsonNode? node)
    {
        if(node is JsonObject obj){if(obj.ContainsKey("OriginalPath")&&obj.ContainsKey("CachePath")&&obj.ContainsKey("Sha256"))yield return obj;foreach(var value in obj.ToArray())foreach(var proof in FileProofs(value.Value))yield return proof;}
        else if(node is JsonArray array)foreach(var value in array)foreach(var proof in FileProofs(value))yield return proof;
    }
    async Task VerifyBlob(LibraryBlob blob,CancellationToken ct)
    {
        string path=Blob(blob);if(!File.Exists(path))throw new InvalidDataException($"Missing cached original {blob.Sha256}: {blob.FileName}");
        await using var stream=File.OpenRead(path);if(stream.Length!=blob.Length||Convert.ToHexString(await SHA256.HashDataAsync(stream,ct))!=blob.Sha256)throw new InvalidDataException($"Cached original hash mismatch: {blob.FileName} [{blob.Sha256}]");
    }
    async Task WriteImmutable(string path,byte[] bytes,CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if(File.Exists(path)){if(Hash(await File.ReadAllBytesAsync(path,ct))!=Hash(bytes))throw new InvalidDataException("Library content collision/corruption: "+path);return;}
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{await File.WriteAllBytesAsync(temp,bytes,ct);ct.ThrowIfCancellationRequested();File.Move(temp,path,false);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public async Task<LibrarySource> RegisterAsync(FighterProject project,LibrarySourceDescription description,LibraryInput[] inputs,CancellationToken ct=default)
    {
        if(!description.Reviewed||new[]{description.Provider,description.Build.Game,description.Build.Platform,description.Build.Build,description.Character,description.Model,description.AssociationEvidence}.Any(string.IsNullOrWhiteSpace))throw new InvalidDataException("Review the source model association and provide game/platform/build, character, model and evidence. Unknown versions must be labelled unverified.");
        if(project.Clips.Length>50000)throw new InvalidDataException("A library source is limited to 50000 prepared clips.");
        if(project.Clips.Select(c=>c.Id).Distinct().Count()!=project.Clips.Length)throw new InvalidDataException("Source clip identities must be unique.");
        ProjectStore.Validate(project);ct.ThrowIfCancellationRequested();
        UnresolvedLibraryClip[] unresolved=[];
        if(description.Provider.StartsWith("native.",StringComparison.Ordinal))
        {
            if(project.Clips.Any(c=>c.Retarget is not null||c.SourceSnapshot is not null||c.Source?.Model is null||c.Source.SkeletonRevision!=project.Scene.SkeletonRevision||!project.SourceModels.Any(m=>m.Id==c.Source.SourceModelRevisionId&&m.Resource.Sha256==c.Source.Model.Sha256&&m.Resource.Entry==c.Source.Model.Entry)))throw new InvalidDataException("A native library requires freshly decoded, exactly source-bound clips and their authentic model revision.");
            var bones=project.Scene.Bones.Select(b=>b.Name).ToHashSet(StringComparer.Ordinal);
            unresolved=project.Clips.Where(c=>c.Tracks.Any(t=>t.HasKeys&&!bones.Contains(t.Bone))).Select(c=>new UnresolvedLibraryClip(c.Name,c.Source?.Resource,c.Tracks.Where(t=>t.HasKeys&&!bones.Contains(t.Bone)).Select(t=>t.Bone).Distinct().ToArray())).ToArray();
            var usable=project.Clips.Where(c=>c.Tracks.All(t=>!t.HasKeys||bones.Contains(t.Bone))).ToArray();
            if(project.Clips.Length>0&&usable.Length==0)throw new InvalidDataException("No native clips bind completely to this source model. Choose the authentic model for the unresolved bone channels.");
            project=project with{Clips=usable,Contacts=project.Contacts.Where(c=>usable.Any(clip=>clip.Id==c.Clip)).ToArray()};
        }
        var tree=JsonSerializer.SerializeToNode(project,ProjectStore.Json)!;
        var required=NativeReferences(tree).Select(n=>(Path:n["Path"]!.GetValue<string>(),Hash:n["Sha256"]!.GetValue<string>())).Concat(FileProofs(tree).Select(n=>(Path:n["CachePath"]!.GetValue<string>(),Hash:n["Sha256"]!.GetValue<string>()))).ToList();
        foreach(var input in inputs){string path=Path.GetFullPath(input.Path);required.Add((path,ProjectStore.Hash(path)));}
        if(inputs.Length==0)throw new InvalidDataException("Provide the source project or original model/motion files.");
        var originals=new List<LibraryBlob>();
        // One complete native file may supply many indexed resources; preserve it once, never by name matching.
        long total=0;foreach(var group in required.GroupBy(r=>Path.GetFullPath(r.Path),StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();string path=group.Key;
            long length=new FileInfo(path).Length;total+=length;if(length>512L*1024*1024||total>1024L*1024*1024)throw new InvalidDataException("Library source exceeds the 512 MiB/file or 1 GiB/source cache budget. Disc/archive providers must extract bounded character dependencies first.");
            if(group.Select(x=>x.Hash).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=1)throw new InvalidDataException("Conflicting source hashes: "+path);
            byte[] bytes=await File.ReadAllBytesAsync(path,ct);string hash=Hash(bytes);
            if(!hash.Equals(group.First().Hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Missing or changed source: "+path);
            var blob=new LibraryBlob(path,hash,Path.GetFileName(path),bytes.LongLength);await WriteImmutable(Blob(blob),bytes,ct);originals.Add(blob);
        }
        byte[] package=JsonSerializer.SerializeToUtf8Bytes(project,ProjectStore.Json);if(package.Length>256*1024*1024)throw new InvalidDataException("Prepared source exceeds the 256 MiB package budget.");string packageHash=Hash(package);await WriteImmutable(Package(packageHash),package,ct);
        // Anonymous identity fields are get-only. Project serialization omits
        // get-only convenience properties; using it here hashed every source as
        // "{}", so registering a second model returned the first registration.
        var identityJson=new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false};
        string fingerprint=AnimationControls.Fingerprint(project.Scene);
        string identity=Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{description,Inputs=inputs.Select(i=>new{i.Role,Hash=originals.Single(b=>string.Equals(b.OriginalPath,Path.GetFullPath(i.Path),StringComparison.OrdinalIgnoreCase)).Sha256}),Fingerprint=fingerprint},identityJson)));
        Directory.CreateDirectory(Root);
        await using var lease=new FileStream(Path.Combine(Root,"catalogue.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var catalogue=await LoadAsync(ct);var prior=catalogue.Sources.FirstOrDefault(s=>s.Identity==identity);
        // Preserve historical IDs/proofs; only recognize an old empty-identity
        // registration when its actual rig, description and full inputs agree.
        // Do not relabel old providers or invalidate stored ancestry silently.
        prior??=catalogue.Sources.FirstOrDefault(s=>s.Identity==Hash("{}"u8)&&s.SkeletonFingerprint==fingerprint&&s.Description==description&&s.Originals.Length==originals.Count&&s.Originals.All(b=>originals.Any(o=>o.Sha256==b.Sha256&&o.Length==b.Length&&string.Equals(o.OriginalPath,b.OriginalPath,StringComparison.OrdinalIgnoreCase))));
        if(prior is not null)return prior;
        var source=new LibrarySource(Guid.NewGuid(),identity,description,packageHash,AnimationControls.Fingerprint(project.Scene),project.Clips.Select(c=>new LibraryClip(c.Id,c.Revision,c.Name,c.SampleCount,c.Rate,c.Loop,description.Provider.StartsWith("native.",StringComparison.Ordinal)?c.Source?.Resource:null)).ToArray(),originals.ToArray()){Unresolved=unresolved};
        string temp=Index+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(catalogue with{Sources=catalogue.Sources.Append(source).ToArray()},ProjectStore.Json),ct);ct.ThrowIfCancellationRequested();File.Move(temp,Index,true);}finally{if(File.Exists(temp))File.Delete(temp);}
        return source;
    }
    public async Task<FighterProject> OpenClipAsync(Guid sourceId,Guid clipId,CancellationToken ct=default,bool includeSourceClips=false)
        =>await OpenAsync(sourceId,clipId,includeSourceClips,ct);
    public async Task<FighterProject> OpenSourceAsync(Guid sourceId,CancellationToken ct=default)
        =>await OpenAsync(sourceId,null,true,ct);
    async Task<FighterProject> OpenAsync(Guid sourceId,Guid? clipId,bool includeSourceClips,CancellationToken ct)
    {
        var catalogue=await LoadAsync(ct);var source=catalogue.Sources.SingleOrDefault(s=>s.Id==sourceId)??throw new InvalidDataException("Library source no longer exists.");
        if(clipId is not null&&!source.Clips.Any(c=>c.Id==clipId))throw new InvalidDataException("Clip does not belong to this library source.");
        if(source.Description.Provider=="native.brawl.v1")throw new InvalidDataException("This prepared Brawl source predates the exact float decoder. Re-register its preserved original model/motion pair; existing target keys and original bytes remain preserved.");
        foreach(var original in source.Originals)await VerifyBlob(original,ct);
        byte[] bytes=await File.ReadAllBytesAsync(Package(source.PackageHash),ct);if(Hash(bytes)!=source.PackageHash)throw new InvalidDataException("Prepared source package hash mismatch.");
        var tree=JsonNode.Parse(bytes)??throw new InvalidDataException("Empty prepared package.");
        foreach(var reference in NativeReferences(tree))
        {
            string path=reference["Path"]!.GetValue<string>();string hash=reference["Sha256"]!.GetValue<string>();
            var blob=source.Originals.SingleOrDefault(b=>string.Equals(b.OriginalPath,path,StringComparison.OrdinalIgnoreCase)&&b.Sha256.Equals(hash,StringComparison.OrdinalIgnoreCase))??throw new InvalidDataException("Uncached native source identity: "+path);
            reference["OriginalPath"]??=path;reference["Path"]=Blob(blob);
        }
        foreach(var proof in FileProofs(tree))
        {
            string old=proof["CachePath"]!.GetValue<string>(),hash=proof["Sha256"]!.GetValue<string>();
            var blob=source.Originals.SingleOrDefault(b=>string.Equals(b.OriginalPath,old,StringComparison.OrdinalIgnoreCase)&&b.Sha256==hash)??throw new InvalidDataException("Missing source-ancestry file proof: "+old);proof["CachePath"]=Blob(blob);
        }
        var project=ProjectStore.ReadJson(tree.ToJsonString(ProjectStore.Json));
        if(AnimationControls.Fingerprint(project.Scene)!=source.SkeletonFingerprint||project.Clips.Length!=source.Clips.Length||project.Clips.Any(c=>!source.Clips.Any(e=>e.Id==c.Id&&e.Revision==c.Revision)))throw new InvalidDataException("Library scene/clip revision mismatch.");
        var clips=project.Clips.Where(c=>includeSourceClips||c.Id==clipId).Select(c=>c.Source is {} identity?c with{Source=identity with{AssociationEvidence=source.Description.AssociationEvidence}}:c).ToArray();
        if(clips.Any(c=>!source.Clips.Any(e=>e.Id==c.Id&&e.Revision==c.Revision)))throw new InvalidDataException("A prepared source contains an unregistered clip revision.");
        project=project with{Clips=clips,Contacts=project.Contacts.Where(c=>includeSourceClips||c.Clip==clipId).ToArray(),SourceBuild=source.Description.Build,SourceBuildVerified=source.Description.BuildVerified,SourceProvider=source.Description.Provider,SourceAssociationEvidence=source.Description.AssociationEvidence,LibraryRegistrationId=source.Id,SourceFiles=source.Originals.Select(b=>new SourceFileProof(b.OriginalPath,Blob(b),b.Sha256)).ToArray()};
        ProjectStore.Validate(project);return project;
    }
}
