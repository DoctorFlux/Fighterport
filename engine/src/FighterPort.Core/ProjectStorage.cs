using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FighterPort.Core;

// Storage-only pooling. The editable model remains SourceAssetSnapshot/Scene;
// loading restores shared immutable objects before any validation or evaluation.
internal record SourceStorageContext(Scene Scene,SourceModelRevision[] Models,NativeAnimationLayer[] Layers,NativeResourceEntry[] Resources);
public static partial class ProjectStore
{
    static readonly PropertyInfo[] projectFields=typeof(FighterProject).GetProperties().Where(p=>p.CanRead&&p.SetMethod is not null&&p.Name!=nameof(FighterProject.LibrarySources)).ToArray();
    internal static readonly PropertyInfo[] snapshotFields=typeof(SourceAssetSnapshot).GetProperties().Where(p=>p.CanRead&&p.SetMethod is not null&&!new[]{"Scene","Models","Layers","Resources"}.Contains(p.Name)).ToArray();
    internal static string StreamHash<T>(T value,JsonSerializerOptions options)
    {
        using var hash=SHA256.Create();using var stream=new CryptoStream(Stream.Null,hash,CryptoStreamMode.Write);JsonSerializer.Serialize(stream,value,options);stream.FlushFinalBlock();return Convert.ToHexString(hash.Hash!);
    }
    static void WriteSnapshotMetadata(Utf8JsonWriter writer,SourceAssetSnapshot snapshot,JsonSerializerOptions options)
    {
        foreach(var property in snapshotFields){writer.WritePropertyName(property.Name);JsonSerializer.Serialize(writer,property.GetValue(snapshot),property.PropertyType,options);}
    }
    internal static void WriteProject(Stream stream,FighterProject project,CancellationToken cancellation)
    {
        using var writer=new Utf8JsonWriter(stream);writer.WriteStartObject();
        foreach(var property in projectFields){cancellation.ThrowIfCancellationRequested();if(property.Name==nameof(FighterProject.BonePins)&&project.BonePins is null)continue;writer.WritePropertyName(property.Name);JsonSerializer.Serialize(writer,property.GetValue(project),property.PropertyType,Json);}
        var contexts=new Dictionary<string,SourceStorageContext>();var references=new Dictionary<(Scene,SourceModelRevision[],NativeAnimationLayer[],NativeResourceEntry[]),string>();
        writer.WritePropertyName("LibrarySources");writer.WriteStartArray();
        foreach(var snapshot in project.LibrarySources)
        {
            cancellation.ThrowIfCancellationRequested();var key=(snapshot.Scene,snapshot.Models,snapshot.Layers,snapshot.Resources);
            if(!references.TryGetValue(key,out var id)){var context=new SourceStorageContext(snapshot.Scene,snapshot.Models,snapshot.Layers,snapshot.Resources);id=StreamHash(context,Json);references.Add(key,id);contexts.TryAdd(id,context);}
            writer.WriteStartObject();writer.WriteString("ContextKey",id);WriteSnapshotMetadata(writer,snapshot,Json);writer.WriteEndObject();
        }
        writer.WriteEndArray();
        if(contexts.Count>0)
        {
            writer.WritePropertyName("LibraryContexts");writer.WriteStartObject();foreach(var context in contexts){cancellation.ThrowIfCancellationRequested();writer.WritePropertyName(context.Key);JsonSerializer.Serialize(writer,context.Value,Json);}writer.WriteEndObject();
        }
        writer.WriteEndObject();writer.Flush();
    }
    public static FighterProject ReadJson(string json)
    {
        using var document=JsonDocument.Parse(json);return ReadProject(document.RootElement);
    }
    static FighterProject ReadProject(JsonElement root,JsonSerializerOptions? readOptions=null)
    {
        var options=readOptions??Json;var project=root.Deserialize<FighterProject>(options)??throw new InvalidDataException("Empty project.");
        if(root.TryGetProperty("LibraryContexts",out var table))
        {
            var contexts=table.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.Deserialize<SourceStorageContext>(options)??throw new InvalidDataException("Empty source context."));
            var entries=root.GetProperty("LibrarySources").EnumerateArray().ToArray();if(entries.Length!=project.LibrarySources.Length)throw new InvalidDataException("Source snapshot/table length mismatch.");
            project=project with{LibrarySources=project.LibrarySources.Select((snapshot,i)=>
            {
                if(!entries[i].TryGetProperty("ContextKey",out var id))return snapshot;
                if(!contexts.TryGetValue(id.GetString()??"",out var context))throw new InvalidDataException("Missing shared authentic source context.");
                if(context.Scene is null||context.Models is null||context.Layers is null||context.Resources is null)throw new InvalidDataException("Incomplete authentic source context.");return snapshot with{Scene=context.Scene,Models=context.Models,Layers=context.Layers,Resources=context.Resources};
            }).ToArray()};
        }
        if(project.LibrarySources.Any(s=>s.Scene is null||s.Models is null))throw new InvalidDataException("Missing authentic source scene/context; this file requires the shared-source-context project reader.");
        Validate(project);return project;
    }
    internal static Task<FighterProject> LoadResolvedAsync(string path,JsonSerializerOptions options,CancellationToken cancellation)
        =>Task.Run(()=>{using var input=File.OpenRead(path);using var json=JsonDocument.Parse(input);cancellation.ThrowIfCancellationRequested();return ReadProject(json.RootElement,options);},cancellation);
    // Fingerprinting also pools repeated source geometry; a selected batch must not
    // allocate/hash hundreds of copies of the same immutable textured model.
    internal sealed class SnapshotFingerprintConverter:JsonConverter<SourceAssetSnapshot>
    {
        readonly Dictionary<(Scene,SourceModelRevision[],NativeAnimationLayer[],NativeResourceEntry[]),string> contexts=[];
        public override SourceAssetSnapshot Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)=>throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer,SourceAssetSnapshot value,JsonSerializerOptions options)
        {
            var key=(value.Scene,value.Models,value.Layers,value.Resources);if(!contexts.TryGetValue(key,out var id))contexts[key]=id=StreamHash(new SourceStorageContext(value.Scene,value.Models,value.Layers,value.Resources),Json);
            writer.WriteStartObject();writer.WriteString("Context",id);WriteSnapshotMetadata(writer,value,options);writer.WriteEndObject();
        }
    }
}
