using System.Diagnostics;
using System.Text.Json;
using FighterPort.Core;

namespace FighterPort.Adapters.Brawl;

public record NativeEdit(string Bone,int Channel,float Frame,float Value,float Tangent)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
    public int FrameBits=>BitConverter.SingleToInt32Bits(Frame);
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
    public int ValueBits=>BitConverter.SingleToInt32Bits(Value);
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
    public int TangentBits=>BitConverter.SingleToInt32Bits(Tangent);
}
public record TextureMatrixCase(int Mode,float[] Values);
public record NativeMorphChannel(string Entry,string Base,string Target,int Slot,uint Flags,Key[] Keys,bool New=false);
public record NativeMorphChange(string Entry,int Version,int SampleCount,bool Loop,NativeMorphChannel[] Channels);
public record NativeGeometryChange(string? Entry,string Basis,string Name,float[][] Values,string Attribute="Position");
public record NativeRequest(string Operation,string Model,string Motion,string? Entry,string? Output,NativeEdit[] Edits,double[] Samples,string? ModelEntry=null,TextureMatrixCase[]? TextureMatrices=null,int? OutputSampleCount=null,bool? OutputLoop=null,bool ReplaceTracks=false,string? OutputName=null,string? MorphEntry=null,NativeMorphChange[]? MorphChanges=null,string? LogicalOutput=null,NativeGeometryChange[]? GeometryChanges=null,string? ModelOutput=null,string? LogicalModelOutput=null,bool RewriteChannels=false);
public record NativePreservation(string Entry,string Name,string Format,string Sha256,long Length);
public record NativeResponse(FighterProject? Project,string[] Diagnostics,float[][][]? ExpectedVertices=null,float[][][]? ExpectedBonePositions=null,float[][]? TextureMatrixSamples=null,NativePreservation[]? PreservedResources=null,float[][][]? ExpectedNormals=null,float[][][]? ExpectedColors=null,float[][][]? ExpectedColors1=null,string? ModelOutput=null);

public sealed partial class BrawlAdapter(string worker)
{
    public const string ReaderVersion="native.brawl.v2.integer-floats";
    public async Task<NativeResponse> RunAsync(NativeRequest request,CancellationToken ct=default)
    {
        if(!File.Exists(worker))throw new FileNotFoundException("Configure the audited Brawl compatibility worker first. See scripts/Configure-Brawl.ps1.",worker);
        var dir=Path.Combine(Path.GetTempPath(),"FighterPort",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        var req=Path.Combine(dir,"request.json");var res=Path.Combine(dir,"response.json");
        try
        {
            await File.WriteAllTextAsync(req,JsonSerializer.Serialize(request,ProjectStore.Json),ct);
            var start=new ProcessStartInfo(worker){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
            start.ArgumentList.Add(req);start.ArgumentList.Add(res);
            using var p=Process.Start(start)??throw new IOException("Worker failed to start.");
            var stderr=p.StandardError.ReadToEndAsync(ct);var stdout=p.StandardOutput.ReadToEndAsync(ct);
            try{await p.WaitForExitAsync(ct);}catch{if(!p.HasExited)p.Kill(true);throw;}
            var error=await stderr;await stdout;
            if(p.ExitCode!=0)throw new InvalidDataException($"Brawl worker failed ({p.ExitCode}): {error}");
            var json=await File.ReadAllTextAsync(res,ct).ConfigureAwait(false);
            return await Task.Run(()=>{var options=new JsonSerializerOptions(ProjectStore.Json);options.Converters.Insert(0,new NativeKeyJsonConverter());return JsonSerializer.Deserialize<NativeResponse>(json,options)??throw new InvalidDataException("Empty worker response.");},ct).ConfigureAwait(false);
        }
        finally{Directory.Delete(dir,true);}
    }
    public async Task<FighterProject> ImportAsync(string model,string motion,CancellationToken ct=default)
    {
        var response=await RunAsync(new("import",model,motion,null,null,[],[]),ct);
        var project=(response.Project??throw new InvalidDataException(string.Join("\n",response.Diagnostics))) with{SourceProvider=ReaderVersion};await Task.Run(()=>ProjectStore.Validate(project),ct).ConfigureAwait(false);return project;
    }
    public Task<FighterProject> ImportModelAsync(string model,CancellationToken ct=default)=>ImportModelAsync(model,null,ct);
    public async Task<FighterProject> ImportModelAsync(string model,string? modelEntry,CancellationToken ct=default)
    {
        var response=await RunAsync(new("model",model,"",null,null,[],[],ModelEntry:modelEntry),ct);var project=(response.Project??throw new InvalidDataException(string.Join("\n",response.Diagnostics))) with{SourceProvider=ReaderVersion};await Task.Run(()=>ProjectStore.Validate(project),ct);return project;
    }
    public async Task<NativeResponse> ExportAsync(FighterProject project,AnimationClip clip,string output,CancellationToken ct=default,bool insert=false)
    {
        var source=clip.NativeTarget??(clip.Retarget is null?clip.Source:null);
        if(source is not null&&source.Resource.Format!="CHR0")throw new InvalidOperationException("This writer requires a CHR0 target basis; source formats are never converted implicitly.");
        var model=source?.Model??project.SourceModels.FirstOrDefault(m=>m.Resource.Format=="MDL0")?.Resource??throw new InvalidOperationException("A native MDL0 target model is required for new CHR0 output.");
        if(insert&&source is null)throw new InvalidOperationException("Import a target motion container before adding a new animation to it.");
        if(Math.Abs(clip.Rate.FramesPerSecond-60)>.0000001)throw new InvalidOperationException("CHR0 output uses 60 native frames/second. Select an explicit resampling policy for this source timebase.");
        if(clip.Tracks.Any(t=>t.Rotation is not null))throw new InvalidOperationException("Quaternion source rotations must be baked to the target's native Euler channels before CHR0 export. Retarget to the reviewed Brawl rig first.");
        if(clip.Tracks.SelectMany(t=>t.Channels.OfType<Curve>()).Any(c=>c.Interpolation==Interpolation.Step&&c.Keys.Select(k=>k.Value).Distinct().Count()>1))throw new InvalidOperationException("Held/stepped changes cannot be written as smooth CHR0 curves without an explicit conversion policy.");
        var bad=await Task.Run(()=>ProjectStore.ValidateSources(project),ct);if(bad.Length>0)throw new InvalidDataException(string.Join("\n",bad.Select(x=>x.Message)));
        output=Path.GetFullPath(output);
        if(ProjectStore.PreservedSourcePaths(project).Any(p=>string.Equals(Path.GetFullPath(p),output,StringComparison.OrdinalIgnoreCase)))throw new IOException("Output must be distinct from every original source and target.");
        if(File.Exists(output))throw new IOException("Native output already exists. Choose a new output path.");
        if(clip.Tracks.SelectMany(t=>t.Channels.OfType<Curve>()).Any(c=>c.Keys.Select(k=>k.Frame).Distinct().Count()!=c.Keys.Length || c.Keys.Any(k=>k.InTangent!=k.OutTangent)))throw new InvalidOperationException("Native writer currently requires unique key times and one tangent per key; no duplicate pair or split tangent will be silently lost.");
        if(clip.Tracks.SelectMany(t=>t.Channels.OfType<Curve>()).SelectMany(c=>c.Keys).Any(k=>!double.IsFinite(k.Frame)||(double)(float)k.Frame!=k.Frame||k.Frame<0||k.Frame>clip.SampleCount))throw new InvalidOperationException("CHR0 native key times require finite, nonnegative, exactly representable native float times within the animation. No rounding or resampling is implicit.");
        if(clip.Tracks.Any(t=>Enumerable.Range(0,3).Any(g=>t.Channels.Skip(g*3).Take(3).Any(c=>c is not null)&&t.Channels.Skip(g*3).Take(3).Any(c=>c is null))))throw new NotSupportedException("A partially absent native S/R/T group needs an explicit rest-value materialization policy. Save Project retains the exact edit; native output is blocked rather than inventing fixed keys.");
        var edits=clip.Tracks.SelectMany(t=>t.Channels.SelectMany((c,i)=>c?.Keys.Select(k=>new NativeEdit(t.Bone,i,checked((float)k.Frame),k.Value,k.OutTangent))??[])).ToArray();
        var response=await RunAsync(new(insert?"insert":source is null?"create":"export",model.Path,source?.Resource.Path??"",source?.Resource.Entry,output,edits,[0,.5,Math.Max(0,clip.LastSampleFrame/2),clip.LastSampleFrame],model.Entry,OutputSampleCount:clip.SampleCount,OutputLoop:clip.Loop,ReplaceTracks:source is null||insert||clip.Retarget is not null||clip.NativeWrite==NativeWriteMode.NewResource,OutputName:clip.NativeWriteName??source?.NativeName??(source is null?clip.Name:null),RewriteChannels:clip.RewriteNativeChannels),ct);
        if(clip.RewriteNativeChannels)
        {
            var reopened=response.Project?.Clips.SingleOrDefault()??throw new InvalidDataException("The owned-channel native rewrite did not return a fresh decoded animation.");
            if(reopened.SampleCount!=clip.SampleCount||reopened.Loop!=clip.Loop)throw new InvalidDataException("Fresh native timing differs from the owned animation edit; output is not validated.");
            foreach(string bone in clip.Tracks.Select(t=>t.Bone).Concat(reopened.Tracks.Select(t=>t.Bone)).Distinct())
            for(int c=0;c<9;c++)
            {
                var expected=clip.Tracks.FirstOrDefault(t=>t.Bone==bone)?.Channels[c]?.Keys??[];var actual=reopened.Tracks.FirstOrDefault(t=>t.Bone==bone)?.Channels[c]?.Keys??[];
                if(expected.Length!=actual.Length||!expected.Zip(actual).All(p=>p.First.Frame==p.Second.Frame&&BitConverter.SingleToInt32Bits(p.First.Value)==BitConverter.SingleToInt32Bits(p.Second.Value)&&BitConverter.SingleToInt32Bits(p.First.InTangent)==BitConverter.SingleToInt32Bits(p.Second.InTangent)&&BitConverter.SingleToInt32Bits(p.First.OutTangent)==BitConverter.SingleToInt32Bits(p.Second.OutTangent)))throw new InvalidDataException($"Fresh native owned-channel rewrite differs at {bone}, channel {c}. Deleted keys, values or tangents were not preserved; output is not validated.");
            }
        }
        var notes=new List<string>(response.Diagnostics);if(clip.Retarget?.IsDraft==true)notes.Add("Native data verified; this clip is an explicitly unvalidated contact/context draft.");if(clip.Retarget?.SupportBindings.Length>0)notes.Add("Body bone channels only: scene/object trajectories and native article/attachment output are not written or game-verified by this CHR0 export.");return response with{Diagnostics=notes.ToArray()};
    }
    public static string DefaultWorker => Path.Combine(AppContext.BaseDirectory,"worker","FighterPort.BrawlWorker.exe");
}
