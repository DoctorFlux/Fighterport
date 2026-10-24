using System.Numerics;
using System.Text.Json;
using FighterPort.Core;

namespace FighterPort.Adapters.Brawl;

public record NativeBatchFile(string Path,string Sha256,string State,string[] Motions);
public record NativeBatchManifest(Guid Id,string State,NativeBatchFile[] Files,string[] Diagnostics)
{
    public string Output {get;init;}="";
    public string ReceiptPath {get;init;}="";
}
public sealed partial class BrawlAdapter
{
    // One new container, one final rename. Each stage uses a fresh worker and keeps
    // the previously inserted resources in its sibling-preservation comparison.
    public async Task<NativeBatchManifest> ExportBatchAsync(FighterProject project,IReadOnlyList<AnimationClip> clips,string output,CancellationToken cancellation=default,IProgress<NativeBatchManifest>? progress=null)
    {
        if(clips.Count==0)throw new ArgumentException("Select at least one native target animation.");
        if(clips.Select(c=>c.Id).Distinct().Count()!=clips.Count||clips.Any(c=>!project.Clips.Any(p=>p.Id==c.Id&&p.Revision==c.Revision)))throw new InvalidOperationException("Batch motions must be distinct current target revisions.");
        var basis=clips[0].NativeTarget??clips[0].Source??throw new InvalidOperationException("Import a target motion container before multi-motion insertion.");
        if(clips.Any(c=>(c.NativeTarget??c.Source) is not {} native||native.Resource!=basis.Resource||native.Model!=basis.Model))throw new NotSupportedException("This batch must target one exact existing native motion group/model. Multiple files require separate durable per-file commits.");
        output=Path.GetFullPath(output);if(File.Exists(output)||ProjectStore.PreservedSourcePaths(project).Any(p=>string.Equals(Path.GetFullPath(p),output,StringComparison.OrdinalIgnoreCase)))throw new IOException("Choose a NEW output distinct from all original/cache paths.");
        string extension=Path.GetExtension(output);if(!new[]{".pac",".brres"}.Contains(extension,StringComparer.OrdinalIgnoreCase)||!string.Equals(extension,Path.GetExtension(basis.Resource.Path),StringComparison.OrdinalIgnoreCase))throw new NotSupportedException("Multi-motion output currently preserves the existing PAC/BRRES container type; PCS conversion is not accepted.");
        string[] names=clips.Select(c=>c.NativeWriteName??c.Name).ToArray();if(names.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=names.Length)throw new InvalidOperationException("Resolve duplicate native output names before export.");
        var original=await ImportAsync(basis.Model!.Path,basis.Resource.Path,cancellation);if(original.Clips.Any(c=>names.Contains(c.Source?.NativeName,StringComparer.OrdinalIgnoreCase)))throw new InvalidOperationException("An output name collides with an existing native clip; custom resources will not be overwritten.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);string stem=Path.Combine(Path.GetDirectoryName(output)!,".fighterport-"+Guid.NewGuid().ToString("N"));string manifestPath=output+".fighterport-receipt.json";if(File.Exists(manifestPath))throw new IOException("An export receipt already owns this destination. Inspect/recover it before retrying.");
        using(var reservation=new FileStream(manifestPath,FileMode.CreateNew,FileAccess.Write,FileShare.None)){}
        var stages=new List<string>();var id=Guid.NewGuid();string current=basis.Resource.Path;bool committed=false;var diagnostics=new List<string>();
        try
        {
            await Write("Preparing",[]);
            foreach(var clip in clips)
            {
                cancellation.ThrowIfCancellationRequested();string staged=stem+"-"+stages.Count+extension;stages.Add(staged);
                var native=basis with{Resource=basis.Resource with{Path=current,Sha256=await Task.Run(()=>ProjectStore.Hash(current),cancellation)}};
                var response=await ExportAsync(project,clip with{NativeTarget=native},staged,cancellation,insert:true);
                if(response.PreservedResources is not {Length:>0})throw new InvalidDataException("Native stage returned no sibling-preservation evidence.");diagnostics.AddRange(response.Diagnostics.Where(d=>d.Contains("draft",StringComparison.OrdinalIgnoreCase)||d.Contains("Body bone channels only",StringComparison.Ordinal)));
                await File.WriteAllTextAsync(staged+".preservation.json",JsonSerializer.Serialize(response.PreservedResources,ProjectStore.Json),cancellation);current=staged;
                await Write("Staging",[new(staged,ProjectStore.Hash(staged),"Staged",names.Take(stages.Count).ToArray())]);
            }
            // A final NEW worker import, not an in-memory editor node. Compare every
            // emitted value/tangent and withheld actual bone/skin sample independently.
            var fresh=await ImportAsync(basis.Model.Path,current,cancellation);
            foreach(var clip in clips)
            {
                var matches=fresh.Clips.Where(c=>c.Source?.NativeName==(clip.NativeWriteName??clip.Name)).ToArray();if(matches.Length!=1)throw new InvalidDataException("Inserted motion identity is missing/ambiguous after final native reopening: "+clip.Name);
                var reopened=matches[0];if(reopened.SampleCount!=clip.SampleCount||reopened.Rate!=clip.Rate||reopened.Loop!=clip.Loop)throw new InvalidDataException("Multi-motion native timing changed: "+clip.Name);
                foreach(var track in clip.Tracks)for(int ch=0;ch<9;ch++)if(track.Channels[ch] is {} expected)
                {
                    var actual=reopened.Tracks.SingleOrDefault(t=>t.Bone==track.Bone)?.Channels[ch]??throw new InvalidDataException("Reopened native channel missing: "+track.Bone);
                    foreach(var key in expected.Keys)
                    {
                        var decoded=actual.Keys.FirstOrDefault(k=>k.Frame==key.Frame);if(decoded is null&&expected.Keys.All(k=>k.Value==expected.Keys[0].Value)&&actual.Keys.Length==1)decoded=actual.Keys[0];
                        if(decoded is null||BitConverter.SingleToInt32Bits(decoded.Value)!=BitConverter.SingleToInt32Bits(key.Value)||actual.Keys.Length>1&&(BitConverter.SingleToInt32Bits(decoded.OutTangent)!=BitConverter.SingleToInt32Bits(key.OutTangent)))throw new InvalidDataException($"Exact native key/tangent mismatch: {clip.Name}/{track.Bone}/{ch}/{key.Frame}");
                    }
                }
                foreach(double frame in new[]{.125,Math.Min(3.375,clip.LastSampleFrame),Math.Max(0,clip.LastSampleFrame-.125),clip.LastKeyFrame})
                {
                    var expected=AnimationService.Evaluate(project.Scene,clip,frame);var actual=AnimationService.Evaluate(fresh.Scene,reopened,frame);
                    if(expected.World.Zip(actual.World).Any(p=>Vector3.Distance(p.First.Translation,p.Second.Translation)>.005f)||project.Scene.Meshes.SelectMany(m=>m.Vertices).Any(v=>Vector3.Distance(AnimationService.Skin(v,expected),AnimationService.Skin(v,actual))>.0003f))throw new InvalidDataException("Multi-motion fractional pose/skin mismatch: "+clip.Name+" at "+frame);
                }
            }
            cancellation.ThrowIfCancellationRequested();await Write("Validated",[new(current,ProjectStore.Hash(current),"Staged",names)]);
            cancellation.ThrowIfCancellationRequested();
            File.Move(current,output,false);committed=true;return await Write("Committed",[new(output,ProjectStore.Hash(output),"Committed",names)]);
        }
        catch(Exception e)
        {
            diagnostics.Add(e.Message);try{await Write(committed?"CommittedReceiptInterrupted":"Failed",committed?[new(output,ProjectStore.Hash(output),"Committed",names)]:[]);}catch(IOException receiptError){throw new IOException($"Export receipt interrupted. Planned output: {output}. Committed: {committed}. Inspect recovery receipt: {manifestPath}. {receiptError.Message}",e);}throw;
        }
        finally{foreach(string stage in stages)if(File.Exists(stage))File.Delete(stage);}
        async Task<NativeBatchManifest> Write(string state,NativeBatchFile[] files)
        {
            var receipt=new NativeBatchManifest(id,state,files,diagnostics.ToArray()){Output=output,ReceiptPath=manifestPath};string temporary=manifestPath+".tmp";await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(receipt,ProjectStore.Json),CancellationToken.None);File.Move(temporary,manifestPath,true);progress?.Report(receipt);return receipt;
        }
    }
    public static async Task<NativeBatchManifest> InspectBatchRecoveryAsync(string manifestPath,CancellationToken cancellation=default)
    {
        var receipt=JsonSerializer.Deserialize<NativeBatchManifest>(await File.ReadAllTextAsync(manifestPath,cancellation),ProjectStore.Json)??throw new InvalidDataException("Empty native batch receipt.");
        if(string.IsNullOrWhiteSpace(receipt.Output))throw new InvalidDataException("Receipt has no planned output identity.");
        if(File.Exists(receipt.Output))
        {
            if(receipt.Files.LastOrDefault() is not {} expected||ProjectStore.Hash(receipt.Output)!=expected.Sha256)throw new InvalidDataException("Destination exists but does not match the last validated/staged hash. Outcome is unresolved; no overwrite or deletion performed.");
            return receipt with{State="CommittedRecovered",Files=[new(receipt.Output,expected.Sha256,"Committed",expected.Motions)]};
        }
        if(receipt.State.StartsWith("Committed",StringComparison.Ordinal))throw new InvalidDataException("Receipt records a committed output that is now missing.");
        return receipt with{State="NoCommittedOutput"};
    }
}
