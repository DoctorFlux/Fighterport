using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FighterPort.Core;

public static class OriginalSupportEvidence
{
    // Verify the original native evidence independently of the application's
    // JavaScriptSerializer. Canonical header bytes are hashed verbatim.
    public static (string Key,InferredSupportWindow[] Ranges) Read(Scene scene,int frames,string envelope)
    {
        if(envelope.Length>20*1024*1024)throw new InvalidDataException("Original support envelope exceeds its bounded read limit.");
        using var outer=JsonDocument.Parse(envelope);var root=outer.RootElement;
        string Text(string name)=>root.GetProperty(name).GetString()??throw new InvalidDataException("Missing original support identity: "+name);
        if(Text("Version")!="original-next-support-context-1")throw new InvalidDataException("Unsupported original support evidence version.");
        string header=Text("CanonicalHeader"),payload=Text("Json"),key=Text("Sha256");
        if(header.Length>1024*1024||payload.Length>8*1024*1024||key.Length!=64)throw new InvalidDataException("Original support payload or hash exceeds its bounds.");
        string actual=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(header+"\n"+payload)));
        if(!string.Equals(actual,key,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The complete original support payload changed.");
        using var canonical=JsonDocument.Parse(header);var h=canonical.RootElement;
        if(h.GetProperty("Version").GetString()!=Text("Version")||h.GetProperty("Frames").GetInt32()!=frames||root.GetProperty("Frames").GetInt32()!=frames||
            h.GetProperty("SourceRig").GetString()!=Text("SourceRig")||h.GetProperty("TargetRig").GetString()!=Text("TargetRig")||
            h.GetProperty("InitialResultHash").GetString()!=Text("InitialResultHash"))throw new InvalidDataException("Original support identities or native timing changed.");
        using var data=JsonDocument.Parse(payload);
        if(data.RootElement.GetProperty("Version").GetString()!=Text("Version")||data.RootElement.GetProperty("LastFrame").GetInt32()!=frames-1||
            data.RootElement.GetProperty("SampleStep").GetSingle()!=.25f)
            throw new InvalidDataException("Original support sample/version semantics changed.");
        if(data.RootElement.TryGetProperty("Suspended",out var suspended)&&suspended.ValueKind!=JsonValueKind.Null)
            throw new NotSupportedException("Original hanging support requires a reviewed finite ledge; it cannot be replaced by static ground correction.");
        var target=data.RootElement.GetProperty("Target").GetProperty("Bones").EnumerateArray().ToArray();
        if(target.Length!=scene.Bones.Length||target.Where((bone,i)=>bone.GetProperty("Name").GetString()!=scene.Bones[i].Name||bone.GetProperty("Parent").GetInt32()!=scene.Bones[i].Parent).Any())
            throw new InvalidDataException("Original support evidence belongs to another native hierarchy.");
        var ranges=JsonSerializer.Deserialize<InferredSupportWindow[]>(h.GetProperty("Ranges").GetRawText(),ProjectStore.Json)??[];
        if(ranges.Length>20000)throw new InvalidDataException("Original support timeline exceeds its review budget.");
        var payloadRanges=JsonSerializer.Deserialize<InferredSupportWindow[]>(data.RootElement.GetProperty("Ranges").GetRawText(),ProjectStore.Json)??[];
        var outerRanges=JsonSerializer.Deserialize<InferredSupportWindow[]>(root.GetProperty("Ranges").GetRawText(),ProjectStore.Json)??[];
        if(ReviewedContactSets.ContentKey(ranges)!=ReviewedContactSets.ContentKey(payloadRanges)||ReviewedContactSets.ContentKey(ranges)!=ReviewedContactSets.ContentKey(outerRanges))
            throw new InvalidDataException("Original support ranges differ between the complete payload and its identity envelope.");
        int count=checked((frames-1)*4+1),sourceBones=data.RootElement.GetProperty("Source").GetProperty("Bones").GetArrayLength();
        var samples=data.RootElement.GetProperty("SourceFrames");
        if(sourceBones<1||sourceBones>4096||samples.GetArrayLength()!=count)
            throw new InvalidDataException("Complete authentic source poses are missing from original support evidence.");
        int index=0;foreach(var frame in samples.EnumerateArray())
        {
            if(frame.GetProperty("Frame").GetInt32()!=BitConverter.SingleToInt32Bits(index++/4f)||frame.GetProperty("Local").GetArrayLength()!=sourceBones||frame.GetProperty("World").GetArrayLength()!=sourceBones)
                throw new InvalidDataException("Original support pose/timing evidence is incomplete.");
        }
        foreach(var track in data.RootElement.GetProperty("Tracks").EnumerateArray())if(track.GetProperty("Samples").GetArrayLength()!=count)
            throw new InvalidDataException("Original support establishment/hold/release tracks are incomplete.");
        return(key,ranges);
    }
}
