using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class NativeExactClipChecks
{
    public static async Task RunAsync(BrawlAdapter adapter,string expectedFile,string output,Action<bool,string> check)
    {
        var expected=JsonSerializer.Deserialize<AnimationClip>(await File.ReadAllTextAsync(expectedFile),ProjectStore.Json)??throw new InvalidDataException("Expected clip is missing.");
        var native=expected.NativeTarget??throw new InvalidDataException("Expected clip has no target native identity.");var model=native.Model??throw new InvalidDataException("Expected clip has no native model.");
        string modelHash=ProjectStore.Hash(model.Path),motionHash=ProjectStore.Hash(native.Resource.Path);
        float negativeZero=BitConverter.Int32BitsToSingle(int.MinValue);string wire=JsonSerializer.Serialize(new NativeEdit("wire",4,0,negativeZero,negativeZero),ProjectStore.Json);
        using(var data=JsonDocument.Parse(wire))check(data.RootElement.GetProperty("ValueBits").GetInt32()==int.MinValue&&data.RootElement.GetProperty("TangentBits").GetInt32()==int.MinValue,"The actual native request preserves signed-zero value/tangent bits independently of JSON numbers");
        var edits=expected.Tracks.SelectMany(t=>t.Channels.SelectMany((curve,ch)=>curve?.Keys.Select(k=>new NativeEdit(t.Bone,ch,(int)k.Frame,k.Value,k.OutTangent))??[])).ToArray();
        string path=Path.Combine(output,"ExactReopened.chr0");var reopened=await adapter.RunAsync(new("export",model.Path,native.Resource.Path,native.Resource.Entry,path,edits,[0,.125,Math.Min(29.375,expected.LastSampleFrame),expected.LastSampleFrame],model.Entry,OutputSampleCount:expected.SampleCount,OutputLoop:expected.Loop,ReplaceTracks:true,OutputName:expected.NativeWriteName));
        var result=reopened.Project??throw new InvalidDataException("Native output was empty.");var comparison=NativeRetargetMatrix.ExactCurves(expected,result.Clips[0],result.Scene);
        await File.WriteAllTextAsync(Path.Combine(output,"exact-native-receipt.json"),JsonSerializer.Serialize(new{comparison.Exact,comparison.Comparisons,comparison.Differences,Native=Path.GetFileName(path),Expected=Path.GetFileName(expectedFile)},new JsonSerializerOptions(ProjectStore.Json){WriteIndented=true,IgnoreReadOnlyProperties=false}));
        check(comparison.Exact&&comparison.Comparisons>0,$"Fresh native decode retains every supported key/tangent bit, including signed zero ({comparison.Comparisons} comparisons)");
        check(result.Clips[0].SampleCount==expected.SampleCount&&result.Clips[0].Loop==expected.Loop&&result.Clips[0].Rate==expected.Rate,"Signed-bit transport preserves native timing and loop flags");
        check(modelHash==ProjectStore.Hash(model.Path)&&motionHash==ProjectStore.Hash(native.Resource.Path),"Exact native re-encoding preserves both original private source files");
    }
}
