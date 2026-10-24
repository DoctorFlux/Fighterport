using System.Numerics;
using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class MorphGeometryChecks
{
    public static async Task CoreAsync(FighterProject fixture,string directory,Action<bool,string> check)
    {
        var basis=new NativeResourceRef("fixture-only","root/0/0","fixture","Positions",0);var entry=new NativeResourceRef("fixture-motion","root/1/0","fixture","SHP0Entry",0);var curve=new Curve([new(0,0)]);var vertices=fixture.Scene.Meshes[0].Vertices;
        var map=new MorphTopology(basis,[new(1,2,3,0)],Enumerable.Repeat(0,vertices.Length).ToArray(),Enumerable.Range(0,vertices.Length).Select(i=>i%2).ToArray(),[Matrix4x4.Identity,Matrix4x4.CreateTranslation(10,0,0)]);
        var layer=new NativeAnimationLayer(Guid.NewGuid(),"Geometry","SHP0",20,true,new(60),new("fixture-motion","root/1","fixture","SHP0",3),[new("Base",0,[curve]){MorphTarget="Existing",Native=entry with{Entry=entry.Entry+"/0"},NativeFlags=3}],"Fixture")
        {Morphs=[new("Base",0,MorphAttribute.Position,[new("Existing",curve,MorphGeometryEditing.Prepare(map,map.BaseValues)){Native=basis with{Entry="root/0/1"},LocalValues=map.BaseValues}]){Native=entry,Topology=map}]};
        var project=fixture with{NativeLayers=[layer]};string original=JsonSerializer.Serialize(project,ProjectStore.Json);var doc=new EditorDocument(project);doc.AutoMatchNativeLayers=false;doc.SetLayer("SHP0",layer.Id);doc.CreateMorphTarget(layer.Id,0,"IndependentShape");int track=doc.SelectedMorphTrack;
        check(doc.Layer("SHP0")!.Tracks[track].NewMorphTarget&&doc.Layer("SHP0")!.Morphs[0].Targets[1].Weight.Sample(0)==0,"New morph target clones native base geometry as an independent zero-weight channel");
        doc.EditMorphPoint(layer.Id,track,0,new(4,5,6));var target=doc.Layer("SHP0")!.Morphs[0].Targets[1];
        check(target.Values[0]==new Vector4(4,5,6,0)&&target.Values[1]==new Vector4(14,5,6,0),"One native point edit updates shared facepoints through their distinct bind transforms");
        check(JsonSerializer.Serialize(project,ProjectStore.Json)==original&&AnimationControls.DeformChecksum(doc.Project.Scene)==AnimationControls.DeformChecksum(project.Scene),"Geometry editing isolates original arrays, deform bones, native binds and skin weights");
        doc.Undo();check(doc.Layer("SHP0")!.Morphs[0].Targets[1].LocalValues![0]==new Vector4(1,2,3,0),"Geometry undo restores actual native coordinates");doc.Redo();
        await ProjectStore.SaveAsync(doc.Project,Path.Combine(directory,"geometry-core.fighterport"));var reopened=await ProjectStore.LoadAsync(Path.Combine(directory,"geometry-core.fighterport"));check(reopened.NativeLayers[0].Morphs[0].Targets[1].LocalValues![0]==new Vector4(4,5,6,0)&&reopened.NativeLayers[0].Tracks[1].NewMorphTarget,"Project reopen retains target creation, native correspondence and edited geometry");
        try{doc.CreateMorphTarget(layer.Id,0,"IndependentShape");check(false,"Duplicate geometry names");}catch(InvalidOperationException){check(true,"Duplicate native target names are rejected before mutating the document");}
        try{doc.EditMorphPoint(layer.Id,track,0,new(float.NaN,0,0));check(false,"Nonfinite geometry");}catch(ArgumentException){check(true,"Nonfinite point coordinates are rejected without changing geometry");}
    }
    public static async Task NativeAsync(BrawlAdapter adapter,FighterProject project,string directory,Action<bool,string> check)
    {
        var doc=new EditorDocument(project);var layer=doc.Layer("SHP0")!;int binding=Array.FindIndex(layer.Morphs,b=>b.Topology is not null);var basis=layer.Morphs[binding];
        check(basis.Topology!.Indices.Length==project.Scene.Meshes[basis.Mesh].Vertices.Length,"Native import retains exact position-array indices and bind transforms for geometry editing");
        doc.CreateMorphTarget(layer.Id,binding,"FP_AuthoredGeometry");int track=doc.SelectedMorphTrack;doc.EditMorphKey(layer.Id,track,0,new(0,0));doc.EditMorphKey(layer.Id,track,15,new(15,.8f));doc.EditMorphKey(layer.Id,track,60,new(60,.2f));
        var original=basis.Topology.BaseValues;float size=Math.Max(.1f,project.Scene.Meshes.SelectMany(m=>m.Vertices).Max(v=>v.Position.Y)*.2f);
        for(int i=0;i<Math.Min(3,original.Length);i++)doc.EditMorphPoint(layer.Id,track,i,new(original[i].X+size,original[i].Y+size*.3f,original[i].Z-size*.2f));
        // A finite coordinate whose byte-reversed bits are a signaling NaN on x86.
        float endianTrap=BitConverter.Int32BitsToSingle(0x3f8cb3ff);doc.EditMorphPoint(layer.Id,track,0,new(endianTrap,original[0].Y+size*.3f,original[0].Z));
        doc.CreateMorphTarget(layer.Id,binding,"FP_SecondGeometry");doc.SelectedMorphTrack=track;
        // Also edit an existing target array. Shared occurrences must all update together.
        int existing=MorphEditing.EditableTracks(layer).First(i=>MorphEditing.Matches(layer.Tracks[i],basis));var old=basis.Targets.Single(t=>t.Name==layer.Tracks[existing].MorphTarget).LocalValues![0];doc.EditMorphPoint(layer.Id,existing,0,new(old.X,old.Y+size*.1f,old.Z));
        string edited=Path.Combine(directory,"geometry-edited.fighterport");await ProjectStore.SaveAsync(doc.Project,edited);
        string output=Path.Combine(directory,"geometry-motion"+Path.GetExtension(doc.Clip.Source!.Resource.Path));var result=await adapter.ExportMotionAsync(doc.Project,doc.Clip,layer.Id,output);string model=result.ModelOutput??throw new Exception("Geometry model output missing.");
        var reopened=await adapter.ImportAsync(model,output);var fresh=new EditorDocument(reopened);var shape=fresh.Layer("SHP0")!;var target=shape.Morphs.First(b=>b.Topology?.Basis.Entry==basis.Topology.Basis.Entry).Targets.Single(t=>t.Name=="FP_AuthoredGeometry");var expected=doc.Layer("SHP0")!.Morphs[binding].Targets.Single(t=>t.Name==target.Name);
        check(target.LocalValues!.SequenceEqual(expected.LocalValues!)&&target.Native is not null,"Fresh native model reopens exact newly created position coordinates with authentic array identity");
        check(BitConverter.SingleToInt32Bits(target.LocalValues![0].X)==0x3f8cb3ff,"Native float coordinates retain exact endian bits even when reversed bits form a signaling NaN");
        check(shape.Tracks.Any(t=>t.MorphTarget=="FP_SecondGeometry")&&shape.Tracks.All(t=>!t.NewMorphTarget)&&shape.Morphs.SelectMany(b=>b.Targets).All(t=>!t.GeometryEdited),"Two appended native targets reopen as ordinary editable source arrays with clean write intent");
        check(AnimationControls.DeformChecksum(reopened.Scene)==AnimationControls.DeformChecksum(project.Scene)&&shape.Morphs.First(b=>b.Topology?.Basis.Entry==basis.Topology.Basis.Entry).Topology!.BaseValues.SequenceEqual(original),"Fresh geometry save preserves all existing deform bones, bind geometry, skin weights and base positions");
        check(fresh.Layer("SRT0") is not null&&fresh.Layer("VIS0") is not null&&fresh.Layer("SCN0") is null&&result.PreservedResources!.Any(p=>p.Format=="SCN0"),"Created targets play with CHR0 and companion layers while SCN0 remains skipped and hash-preserved");
        check(result.Diagnostics.Any(d=>d.Contains("fractional/key samples"))&&result.Diagnostics.Any(d=>d.Contains("Fresh model arrays"))&&ProjectStore.ValidateSources(reopened).Length==0,"Paired native outputs pass independent fractional bone/morph skinning, exact array and source-hash checks");
        await ProjectStore.SaveAsync(reopened,Path.Combine(directory,"geometry-reopened.fighterport"));
        await File.WriteAllTextAsync(Path.Combine(directory,"geometry-receipt.json"),JsonSerializer.Serialize(new{Output=output,Model=model,result.Diagnostics,Target=target.Name,Points=target.LocalValues!.Length,OriginalModel=project.Clips[0].Source!.Model},new JsonSerializerOptions{WriteIndented=true}));
        // Freshly reopened arrays remain editable, without creating another deform skeleton.
        int reopenedTrack=Array.FindIndex(shape.Tracks,t=>t.MorphTarget==target.Name);fresh.EditMorphPoint(shape.Id,reopenedTrack,0,new(target.LocalValues[0].X+.05f,target.LocalValues[0].Y,target.LocalValues[0].Z));string second=Path.Combine(directory,"geometry-second"+Path.GetExtension(output));var again=await adapter.ExportMotionAsync(fresh.Project,fresh.Clip,shape.Id,second);check(File.Exists(again.ModelOutput)&&File.Exists(second),"Reopened target geometry can be edited and saved again into another verified native model/motion pair");
        if(Path.GetExtension(project.Clips[0].Source!.Model!.Path).Equals(".brres",StringComparison.OrdinalIgnoreCase))
        {
            string modelPac=Path.Combine(directory,"geometry-model-input.pac");await adapter.RunAsync(new("wrap-motion","",project.Clips[0].Source!.Model!.Path,null,modelPac,[],[]));
            var pac=await adapter.ImportAsync(modelPac,Path.Combine(directory,"source.pac"));var pacDoc=new EditorDocument(pac);pacDoc.SelectClip(Array.FindIndex(pac.Clips,c=>c.Source?.NativeName=="OriginalMorph"));var pacLayer=pacDoc.Layer("SHP0")!;int b=Array.FindIndex(pacLayer.Morphs,m=>m.Attribute==MorphAttribute.Position&&m.Topology is not null);pacDoc.CreateMorphTarget(pacLayer.Id,b,"FP_PacGeometry");int t=pacDoc.SelectedMorphTrack;var point=pacLayer.Morphs[b].Topology!.BaseValues[0];pacDoc.EditMorphPoint(pacLayer.Id,t,0,new(point.X+size,point.Y,point.Z));pacDoc.EditMorphKey(pacLayer.Id,t,0,new(0,.5f));
            string newPac=Path.Combine(directory,"geometry-pac-motion.pac");var saved=await adapter.ExportMotionAsync(pacDoc.Project,pacDoc.Clip,pacLayer.Id,newPac);var pacFresh=await adapter.ImportAsync(saved.ModelOutput!,newPac);
            check(Path.GetExtension(saved.ModelOutput)==".pac"&&pacFresh.Clips.Length==pac.Clips.Length&&pacFresh.NativeLayers.Any(l=>l.Tracks.Any(t=>t.MorphTarget=="FP_PacGeometry")),"New model PAC and motion PAC reopen created geometry with the complete private animation catalogue and preserved siblings");
        }
    }
}
