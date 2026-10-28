using System.Numerics;
using FighterPort.Core;

namespace FighterPort.Adapters.Brawl;

public sealed partial class BrawlAdapter
{
    public static string GeometryModelOutput(AnimationClip clip,string motionOutput)
    {var model=(clip.NativeTarget??clip.Source)!.Model!;return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(motionOutput))!,Path.GetFileNameWithoutExtension(motionOutput)+".model"+Path.GetExtension(model.Path));}
    public async Task<NativeResponse> ExportMotionAsync(FighterProject project,AnimationClip clip,Guid activeMorph,string output,CancellationToken ct=default)
    {
        ProjectStore.Validate(project);var source=clip.NativeTarget??clip.Source??throw new InvalidOperationException("Import the existing target motion container first.");
        if(clip.RewriteNativeChannels)throw new NotSupportedException("This edited skeletal revision contains authoritative key removals/time changes. Save Native Animation writes a new verified skeletal resource; synchronized container/morph rewrite is not qualified for these edits.");
        if(source.Resource.Format!="CHR0"||source.Model is null||clip.NativeWrite==NativeWriteMode.NewResource||clip.Retarget is not null)throw new NotSupportedException("Save motion currently edits an existing CHR0 and its native morphs. New/retargeted resources use the separate insertion workflow.");
        string extension=Path.GetExtension(source.Resource.Path).ToLowerInvariant();output=Path.GetFullPath(output);
        if(extension is not (".pac" or ".brres")||Path.GetExtension(output).ToLowerInvariant()!=extension)throw new NotSupportedException("Save a new copy of the existing PAC or BRRES type. PCS and cross-container conversion remain unsupported.");
        var active=project.NativeLayers.Single(l=>l.Id==activeMorph&&l.Kind=="SHP0");
        bool SameSource(NativeAnimationLayer l)=>l.Resource.Sha256==source.Resource.Sha256&&string.Equals(Path.GetFullPath(l.Resource.Path),Path.GetFullPath(source.Resource.Path),StringComparison.OrdinalIgnoreCase)&&l.Resource.ContainerEntry==source.Resource.ContainerEntry;
        var changed=project.NativeLayers.Where(l=>l.Edited).ToArray();
        if(!SameSource(active)||changed.Any(l=>l.Kind!="SHP0"||!SameSource(l)))throw new InvalidOperationException("Edited layers and the active morph must belong to the selected bone animation's exact source/container. Save other containers separately.");
        if(changed.Any(l=>l.Diagnostics.Any(d=>d.Error)))throw new InvalidOperationException("Resolve the edited morph layer's native array diagnostics before export.");
        if(Math.Abs(clip.Rate.FramesPerSecond-60)>1e-7||changed.Any(l=>Math.Abs(l.Rate.FramesPerSecond-60)>1e-7))throw new NotSupportedException("Native motion output requires the original 60 fps timebase; select an explicit conversion policy first.");
        if(clip.Tracks.Any(t=>t.Rotation is not null))throw new NotSupportedException("Bake quaternion source rotations to native bone channels first.");
        static void CheckCurve(Curve curve,int max,string label,bool fractional=false)
        {
            if(curve.Interpolation!=Interpolation.Hermite&&curve.Keys.Length>1)throw new NotSupportedException(label+": only native Hermite curves can be saved without conversion.");
            if(curve.Keys.Length>32767||curve.Keys.Select(k=>k.Frame).Distinct().Count()!=curve.Keys.Length||curve.Keys.Any(k=>!double.IsFinite(k.Frame)||(fractional?(double)(float)k.Frame!=k.Frame:k.Frame!=Math.Truncate(k.Frame))||k.Frame<0||k.Frame>max||k.InTangent!=k.OutTangent))throw new NotSupportedException(label+": native keys require unique "+(fractional?"exactly representable native float":"integral")+" times in range and one tangent per key.");
        }
        foreach(var curve in clip.Tracks.SelectMany(t=>t.Channels.OfType<Curve>()))CheckCurve(curve,clip.SampleCount,"Bone channel",true);
        var changes=changed.Select(l=>new NativeMorphChange(l.Resource.Entry,l.Resource.Version,l.SampleCount,l.Loop,l.Tracks.Select(t=>
        {
            if(t.Native is null)throw new InvalidOperationException("Reimport this older project to retain exact native morph-channel identities before saving.");
            var c=t.Curves.SingleOrDefault()??new Curve([new(0,0)]);CheckCurve(c,l.Loop?l.SampleCount:l.SampleCount-1,l.Name+" / "+t.Target+" / "+t.MorphTarget);
            if(c.Keys.Length==1&&(c.Keys[0].Frame!=0||c.Keys[0].InTangent!=0))throw new NotSupportedException("A fixed SHP0 channel stores only its value. Use a frame-zero key with zero tangent, or at least two keys.");
            return new NativeMorphChannel(t.Native.Entry,t.Target,t.MorphTarget??throw new InvalidDataException("Missing morph target identity."),t.Slot,t.NativeFlags,c.Keys,t.NewMorphTarget);
        }).ToArray())).ToArray();
        var geometry=project.NativeLayers.SelectMany(l=>l.Morphs).Where(b=>b.Topology is not null).SelectMany(b=>b.Targets.Where(t=>t.GeometryEdited).Select(t=>(Binding:b,Target:t))).GroupBy(x=>(x.Target.Name,Attribute:x.Binding.Attribute>=MorphAttribute.Color0?MorphAttribute.Color0:x.Binding.Attribute)).Select(group=>
        {
            var item=group.First();var map=item.Binding.Topology!;var target=item.Target;
            if(map.Basis.Path!=source.Model.Path||map.Basis.Sha256!=source.Model.Sha256||target.LocalValues is null||group.Any(x=>x.Target.Native!=target.Native||x.Target.LocalValues is null||!x.Target.LocalValues.SequenceEqual(target.LocalValues)))throw new InvalidOperationException("Edited geometry must resolve to one exact target model array across every shared binding.");
            return new NativeGeometryChange(target.Native?.Entry,map.Basis.Entry,target.Name,target.LocalValues.Select(v=>new[]{v.X,v.Y,v.Z,v.W}).ToArray(),group.Key.Attribute.ToString());
        }).ToArray();
        string? modelOutput=geometry.Length>0?GeometryModelOutput(clip,output):null;
        if(modelOutput is not null&&(File.Exists(modelOutput)||string.Equals(modelOutput,output,StringComparison.OrdinalIgnoreCase)||project.NativeResources.Any(r=>string.Equals(Path.GetFullPath(r.Resource.Path),modelOutput,StringComparison.OrdinalIgnoreCase))))throw new IOException("The paired model output must be new and distinct from every input: "+modelOutput);
        var bad=await Task.Run(()=>ProjectStore.ValidateSources(project),ct);if(bad.Length>0)throw new InvalidDataException(string.Join("\n",bad.Select(d=>d.Message)));
        if(File.Exists(output))throw new IOException("Output already exists; choose a NEW output file.");
        if(project.NativeResources.Select(r=>r.Resource.Path).Concat(project.SourceModels.Select(r=>r.Resource.Path)).Append(source.Resource.Path).Append(source.Model.Path).Any(p=>string.Equals(Path.GetFullPath(p),output,StringComparison.OrdinalIgnoreCase)))throw new IOException("Output must not overwrite any original source.");
        string stage=output+"."+Guid.NewGuid().ToString("N")+".staged"+extension;
        string? modelStage=modelOutput is null?null:modelOutput+"."+Guid.NewGuid().ToString("N")+".staged"+Path.GetExtension(modelOutput);bool modelCommitted=false;
        double[] samples=[0,.125,7.75,Math.Min(15.25,clip.LastSampleFrame),Math.Max(0,clip.LastSampleFrame-.25),clip.LastSampleFrame];
        samples=samples.Where(f=>f<=clip.LastSampleFrame).Concat(changed.SelectMany(l=>l.Tracks.SelectMany(t=>t.Curves.OfType<Curve>().SelectMany(c=>c.Keys))).SelectMany(k=>new[]{k.Frame,Math.Max(0,k.Frame-.125),k.Frame+.125}).Where(f=>f<=clip.LastSampleFrame)).Distinct().Order().ToArray();
        if(samples.Length>4096)throw new NotSupportedException("Export verification sample budget exceeded; split this edit into smaller independently verified outputs.");
        try
        {
            var edits=clip.Tracks.SelectMany(t=>t.Channels.SelectMany((c,i)=>c?.Keys.Select(k=>new NativeEdit(t.Bone,i,checked((float)k.Frame),k.Value,k.OutTangent))??[])).ToArray();
            var response=await RunAsync(new("save-motion",source.Model.Path,source.Resource.Path,source.Resource.Entry,stage,edits,samples,source.Model.Entry,OutputSampleCount:clip.SampleCount,OutputLoop:clip.Loop,MorphEntry:active.Resource.Entry,MorphChanges:changes,LogicalOutput:output,GeometryChanges:geometry,ModelOutput:modelStage,LogicalModelOutput:modelOutput),ct).ConfigureAwait(false);
            var fresh=response.Project??throw new InvalidDataException("Missing fresh native motion project.");ProjectStore.Validate(fresh);
            var freshClip=fresh.Clips.Single(c=>c.Source?.Resource.Entry==source.Resource.Entry);
            if(freshClip.SampleCount!=clip.SampleCount||freshClip.Loop!=clip.Loop)throw new InvalidDataException("Native bone timing changed.");
            var doc=new EditorDocument(project);doc.SelectClip(Array.FindIndex(project.Clips,c=>c.Id==clip.Id));doc.SetLayer("SHP0",active.Id);
            var original=project.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();float position=0,normal=0,color=0;
            for(int f=0;f<samples.Length;f++)
            {
                var pose=AnimationService.Evaluate(project.Scene,clip,samples[f]);var morph=doc.EvaluateMorph(pose,samples[f]);
                for(int b=0;b<pose.World.Length;b++){var point=response.ExpectedBonePositions![f][b];position=Math.Max(position,Vector3.Distance(pose.World[b].Translation,new(point[0],point[1],point[2])));}
                for(int i=0;i<original.Length;i++)
                {
                    var v=morph?.Vertex(i,original[i])??original[i];var p=response.ExpectedVertices![f][i];position=Math.Max(position,Vector3.Distance(AnimationService.Skin(v,pose),new(p[0],p[1],p[2])));
                    Vector3 n=Vector3.Zero;for(int j=0;j<v.InfluenceCount;j++)n+=Vector3.TransformNormal(v.Normal,pose.Skin[(int)v.JointAt(j)])*v.WeightAt(j);if(n.LengthSquared()>0)n=Vector3.Normalize(n);var en=response.ExpectedNormals![f][i];normal=Math.Max(normal,Vector3.Distance(n,new(en[0],en[1],en[2])));
                    var ec=response.ExpectedColors![f][i];var ed=response.ExpectedColors1![f][i];color=Math.Max(color,Math.Max(Vector4.Distance(v.Color,new(ec[0],ec[1],ec[2],ec[3])),Vector4.Distance(v.Color1,new(ed[0],ed[1],ed[2],ed[3]))));
                }
            }
            if(!float.IsFinite(position)||!float.IsFinite(normal)||!float.IsFinite(color)||position>.001f||normal>.0002f||color>1.01f/255)throw new InvalidDataException($"Native motion reopen differs from edited preview: position={position:G7}, normal={normal:G7}, color={color:G7}. Output was not committed.");
            bad=await Task.Run(()=>ProjectStore.ValidateSources(project),ct);if(bad.Length>0)throw new InvalidDataException("Original source changed during export.");
            ct.ThrowIfCancellationRequested();if(modelStage is not null){File.Move(modelStage,modelOutput!,false);modelCommitted=true;}try{File.Move(stage,output,false);}catch{if(modelCommitted)File.Delete(modelOutput!);throw;}
            return response with{Diagnostics=response.Diagnostics.Append($"Edited shared preview matches native CHR0+SHP0 at {samples.Length} fractional/key samples: position={position:G7}, normal={normal:G7}, color={color:G7}.").ToArray()};
        }
        finally{if(File.Exists(stage))File.Delete(stage);if(modelStage is not null&&File.Exists(modelStage))File.Delete(modelStage);}
    }
}
