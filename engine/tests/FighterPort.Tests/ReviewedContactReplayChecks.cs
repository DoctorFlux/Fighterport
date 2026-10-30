using FighterPort.Core;
using FighterPort.Adapters.Brawl;
using System.Numerics;
using System.Text.Json;

internal static class ReviewedContactReplayChecks
{
    public static async Task RunCached(string input,string output)
    {
        var project=await ProjectStore.LoadAsync(Path.Combine(input,"native-replay.fighterport"));
        var context=JsonSerializer.Deserialize<ReviewedContactContext>(await File.ReadAllTextAsync(Path.Combine(input,"reviewed-context.json")),ProjectStore.Json)!;
        var result=ReviewedContactSets.Prepare(project,project.Clips.Single(),project.ControlRigs.Single(),context,progress:Console.WriteLine);
        await File.WriteAllTextAsync(Path.Combine(output,"prepared.json"),JsonSerializer.Serialize(result,ProjectStore.Json));
        Console.WriteLine(JsonSerializer.Serialize(result.Receipt,ProjectStore.Json));
    }
    public static async Task Run(string input,string output,bool prepare)
    {
        using var request=JsonDocument.Parse(await File.ReadAllTextAsync(input));var root=request.RootElement;
        string Text(string key)=>root.GetProperty(key).GetString()!;
        var adapter=new BrawlAdapter(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER")??throw new InvalidOperationException("Set the exact native worker."));
        var project=await adapter.ImportAsync(Text("Model"),Text("Motion"));var master=project.Clips.Single();
        using var state=JsonDocument.Parse(Text("State"));
        var rig=state.RootElement.GetProperty("Setup").Deserialize<ControlRigSetup>(ProjectStore.Json)!;
        master=master with{Id=state.RootElement.GetProperty("Clip").GetGuid()};
        var original=state.RootElement.GetProperty("Contacts").Deserialize<ContactFrame[]>(ProjectStore.Json)!;
        project=project with{Clips=[master],Contacts=original,ControlRigs=[rig]};
        var context=JsonSerializer.Deserialize<ReviewedContactContext>(Text("ReviewedContactsJson"),ProjectStore.Json)!;
        foreach(var edit in root.GetProperty("ReviewedRangeEdits").Deserialize<ReviewedContactRangeEdit[]>(ProjectStore.Json)!)
            context=edit.Remove?context with{Ranges=context.Ranges.Where(r=>r.Id!=edit.Id).ToArray()}:
                ReviewedContactSets.SetRange(project.Scene,master,rig,context,edit.Role,edit.Start,edit.End,edit.Blend,edit.AnchorFrame,new(edit.Offset[0],edit.Offset[1],edit.Offset[2]),edit.Reviewed,edit.Id);
        if(root.TryGetProperty("ReviewedCorrectionStart",out var correction)&&correction.ValueKind!=JsonValueKind.Null)
            context=context with{EarlierBodyCorrectionStart=correction.GetInt32()};
        var anatomy=RigCharacterization.Describe(project.Scene,rig);var normal=new Vector3(anatomy.Basis.M21,anatomy.Basis.M22,anatomy.Basis.M23);
        var measurements=new List<object>();
        foreach(var row in context.Ranges.Where(r=>r.Reviewed))
        foreach(double frame in new[]{(double)row.Start-row.Blend,row.Start-row.Blend+.001,row.Start,row.End,row.End+row.Blend-.001,row.End+row.Blend})
        {
            var pose=AnimationService.Evaluate(project.Scene,master,frame);
            measurements.Add(new{row.Role,Frame=frame,MinimumClearance=row.Region.Min(p=>Vector3.Dot(ContactGeometry.Point(p,pose),normal))-context.Surface.SourceFloor,
                LocalRotation=pose.Local[row.Bone].Rotation,WorldPosition=pose.World[row.Bone].Translation});
        }
        var reportJson=new JsonSerializerOptions(ProjectStore.Json){WriteIndented=true,IgnoreReadOnlyProperties=false};
        await File.WriteAllTextAsync(Path.Combine(output,"boundary-probe.json"),JsonSerializer.Serialize(measurements,reportJson));
        foreach(var value in measurements)Console.WriteLine(JsonSerializer.Serialize(value,new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false}));
        await ProjectStore.SaveAsync(project,Path.Combine(output,"native-replay.fighterport"));
        await File.WriteAllTextAsync(Path.Combine(output,"reviewed-context.json"),JsonSerializer.Serialize(context,ProjectStore.Json));
        if(!prepare)return;
        var result=ReviewedContactSets.Prepare(project,master,rig,context,progress:Console.WriteLine);
        await File.WriteAllTextAsync(Path.Combine(output,"prepared.json"),JsonSerializer.Serialize(result,ProjectStore.Json));
        Console.WriteLine(JsonSerializer.Serialize(result.Receipt,ProjectStore.Json));
    }
}
