using System.Numerics;
using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class MorphChecks
{
    public static async Task CoreAsync(FighterProject original,string directory,Action<bool,string> check)
    {
        var scene=original.Scene;var vertices=scene.Meshes[0].Vertices;var a=vertices.Select(v=>new Vector4(v.Position+new Vector3(.4f,0,0),0)).ToArray();var b=vertices.Select(v=>new Vector4(v.Position+new Vector3(0,-.2f,0),0)).ToArray();
        var layer=new NativeAnimationLayer(Guid.NewGuid(),"Original morph","SHP0",20,true,new(30),new("original-only","shape","fixture","SHP0",3),[],"Original fixture")
        {Morphs=[new("Positions",0,MorphAttribute.Position,[new("A",new([new(0,0),new(10,1)],Interpolation.Linear),a),new("B",new([new(0,.2f)]),b)]),new("Normals",0,MorphAttribute.Normal,[new("Tilt",new([new(0,.5f)]),vertices.Select(v=>new Vector4(Vector3.UnitX,0)).ToArray())]),new("Color",0,MorphAttribute.Color0,[new("Tint",new([new(0,.25f)]),vertices.Select(v=>new Vector4(.1f,.2f,.3f,.4f)).ToArray())]){ByteColors=true}]};
        string before=JsonSerializer.Serialize(original,ProjectStore.Json);var doc=new EditorDocument(original with{NativeLayers=[layer]});doc.SetLayer("SHP0",layer.Id);doc.Seek(6);var morph=doc.EvaluateMorph()!;var v=morph.Vertex(0,vertices[0]);
        check(Vector3.Distance(v.Position,vertices[0].Position+new Vector3(.12f,-.04f,0))<.000001f,"Morph blend uses independent residual-base and simultaneous target weights at the shared fractional time");
        var expectedNormal=Vector3.Normalize(vertices[0].Normal*.5f+Vector3.UnitX*.5f);check(Vector3.Distance(v.Normal,expectedNormal)<.000001f,"Morph normals interpolate before normalization without changing rest/skin data");
        var tint=vertices[0].Color*.75f+new Vector4(.1f,.2f,.3f,.4f)*.25f;check(v.Color.X==unchecked((byte)(int)(tint.X*255))/255f&&v.Color.W==unchecked((byte)(int)(tint.W*255))/255f,"Native byte-color morphs truncate RGBA after interpolation");
        long revision=morph.Revision;doc.EvaluateMorph();check(morph.Revision==revision,"Unchanged morph time reuses prepared output without per-refresh blending");
        var pose=doc.Evaluate();var mixed=v with{Joints=new(1,2,0,0),Weights=new(.35f,.65f,0,0)};var independent=Vector3.Transform(v.Position,pose.Skin[1])*.35f+Vector3.Transform(v.Position,pose.Skin[2])*.65f;check(Vector3.Distance(AnimationService.Skin(mixed,pose),independent)<.000001f,"Prepared morph positions compose with independent multi-weight bone skinning");
        doc.Seek(46.5);check(doc.LayerFrame(layer)==3.25,"Morph looping converts authentic time independently from the bone sample count/rate");var once=layer with{Id=Guid.NewGuid(),Loop=false};var onceDoc=new EditorDocument(original with{NativeLayers=[once]});onceDoc.SetLayer("SHP0",once.Id);onceDoc.Seek(59);check(onceDoc.LayerFrame(once)==19,"Non-looping morph playback clamps to its own last native sample");
        doc.Seek(6);check(Vector3.Distance(doc.EvaluateMorph()!.Vertex(0,vertices[0]).Position,v.Position)<.000001f,"Scrubbing a morph out of order is deterministic and does not accumulate deformation");
        string path=Path.Combine(directory,"morph-core.fighterport");await ProjectStore.SaveAsync(doc.Project,path);var reopened=new EditorDocument(await ProjectStore.LoadAsync(path));reopened.SetLayer("SHP0",layer.Id);reopened.Seek(6);check(reopened.EvaluateMorph()!.Vertex(0,vertices[0])==v,"Save/reopen retains typed morph targets, weight curves and byte-color policy");
        doc.AutoMatchNativeLayers=false;doc.Transaction("Replace prepared morph revision",p=>p with{NativeLayers=[layer with{Morphs=[]}]});check(doc.EvaluateMorph() is null,"Replacing a manually selected morph revision invalidates its prepared stream");doc.Undo();check(doc.EvaluateMorph()!.Vertex(0,vertices[0])==v,"Undo restores prepared morphs with automatic matching disabled");doc.Redo();check(doc.EvaluateMorph() is null,"Redo invalidates the restored morph stream without stale deformation");doc.Undo();
        doc.SetLayer("SHP0",null);check(doc.EvaluateMorph() is null&&doc.Evaluate().Local.SequenceEqual(pose.Local),"Disabling a morph restores original geometry without disabling bone animation");
        check(before==JsonSerializer.Serialize(original,ProjectStore.Json),"Morph playback never changes original positions, skeleton, weights, colors or source curves");
        try{new MorphEvaluation(scene,layer with{Morphs=[layer.Morphs[0],layer.Morphs[0]]});check(false,"Duplicate binding rejection");}catch(InvalidDataException){check(true,"Conflicting morph bindings are rejected instead of depending on evaluation order");}
        try{new MorphEvaluation(scene,layer with{Morphs=[layer.Morphs[0] with{Targets=[layer.Morphs[0].Targets[0] with{Values=[]}]}]});check(false,"Index validation");}catch(InvalidDataException){check(true,"Mismatched morph facepoint arrays fail explicitly before playback");}
    }
    public static async Task NativeAsync(BrawlAdapter adapter,string directory,Action<bool,string> check,Action<float,float,float,string> near,string? privateModel=null,int version=3,string? privateMotion=null)
    {
        string tag=privateModel is null?"original-v"+version:Path.GetFileNameWithoutExtension(privateModel)+"-authored";string work=Path.Combine(directory,tag+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        string input=privateModel??Path.Combine(work,"original.dae");if(privateModel is null)FixtureDae.Write(ProceduralFixture.Create().Scene,input,attributes:true);
        string hash=ProjectStore.Hash(input);string model=Path.Combine(work,privateModel is null?"morph.mdl0":"morph-model.brres"),motion=Path.Combine(work,"morph.brres");await adapter.RunAsync(new(privateModel is null?"morph-fixture":"morph-copy",input,motion,null,model,[],[],OutputSampleCount:version));
        var project=await adapter.ImportAsync(model,motion);var doc=new EditorDocument(project);var shape=project.NativeLayers.Single(l=>l.Kind=="SHP0"&&l.Name=="OriginalMorph");
        await ProjectStore.SaveAsync(project,Path.Combine(work,"imported.fighterport"));Console.WriteLine(tag+": "+shape.PreviewStatus);
        check(shape.Resource.Version==version&&shape.Morphs.Length>0&&shape.Diagnostics.All(d=>!d.Error),$"{tag}: genuine native SHP0 decodes resolved position/normal/color bindings with no unsupported active targets");
        check(shape.Diagnostics.Any(d=>d.Code=="MORPH_INACTIVE_TARGET")&&shape.Tracks.Any(t=>t.MorphTarget=="Unused_Zero_Target"),$"{tag}: missing zero-weight arrays retain original channels without disabling valid sibling morph targets");
        check(new[]{"SHP0","SRT0","VIS0"}.All(k=>doc.Layer(k)?.Name=="OriginalMorph")&&doc.Layer("SCN0") is null,$"{tag}: same-name morph, texture and visibility companions run with CHR0 while real SCN0 stays inactive");
        check(shape.Tracks.Any(t=>(t.NativeFlags&1)==0)&&shape.Tracks.All(t=>t.MorphTarget is not null&&t.Curves.Length==1),$"{tag}: native disabled-entry flags and authentic Hermite weight channels remain preserved");
        check(shape.Morphs.Any(m=>m.Attribute==MorphAttribute.Normal)&&shape.Morphs.Any(m=>m.Attribute==MorphAttribute.Color0),$"{tag}: fixture exercises actual native normal and color target arrays");
        double[] times=[0,.125,7.75,15,23.375,30,59,59.75,60,60.125,75,119.75,7.75];
        var native=await adapter.RunAsync(new("sample-morph",model,motion,doc.Clip.Source!.Resource.Entry,null,[],times,MorphEntry:shape.Resource.Entry));
        var vertices=project.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();float positionError=0,normalError=0,colorError=0,color1Error=0;string worst="";
        for(int sample=0;sample<times.Length;sample++)
        {
            doc.Seek(times[sample]); // Seek clamps beyond last displayed sample; samples below count preserve fractions via the clock.
            if(times[sample]>doc.Clip.LastSampleFrame){doc.Clock.Playing=true;doc.Clock.Seek(doc.Clip.LastSampleFrame,doc.Clip);doc.Clock.Advance((times[sample]-doc.Clip.LastSampleFrame)/60,doc.Clip);doc.Clock.Playing=false;}
            var pose=doc.Evaluate();var morph=doc.EvaluateMorph()!;
            for(int i=0;i<vertices.Length;i++)
            {
                var v=morph.Vertex(i,vertices[i]);var expected=native.ExpectedVertices![sample][i];positionError=Math.Max(positionError,Vector3.Distance(AnimationService.Skin(v,pose),new(expected[0],expected[1],expected[2])));
                Vector3 n=Vector3.Zero;for(int j=0;j<4;j++)n+=Vector3.TransformNormal(v.Normal,pose.Skin[(int)v.Joints[j]])*v.Weights[j];if(n.LengthSquared()>0)n=Vector3.Normalize(n);var en=native.ExpectedNormals![sample][i];float error=Vector3.Distance(n,new(en[0],en[1],en[2]));if(error>normalError){normalError=error;worst=$"frame {times[sample]}, vertex {i}, joints {v.Joints}, weights {v.Weights}, expected {string.Join(",",en)}, actual {n}";}
                var ec=native.ExpectedColors![sample][i];var ed=native.ExpectedColors1![sample][i];colorError=Math.Max(colorError,Vector4.Distance(v.Color,new(ec[0],ec[1],ec[2],ec[3])));color1Error=Math.Max(color1Error,Vector4.Distance(v.Color1,new(ed[0],ed[1],ed[2],ed[3])));
            }
        }
        if(normalError>.0002f)Console.WriteLine("Worst native morph normal: "+worst);
        near(positionError,0,.001f,$"{tag}: CHR0 plus SHP0 positions match independent native skinning at fractional/loop/reset samples");near(normalError,0,.0002f,$"{tag}: morphed normals match independent native evaluation across facepoint seams");near(colorError,0,1.01f/255,$"{tag}: morphed color0 follows native byte quantization");near(color1Error,0,1.01f/255,$"{tag}: morphed color1 follows native byte quantization");
        var missing=project.NativeLayers.Single(l=>l.Name=="MissingTarget");check(missing.Diagnostics.Any(d=>d.Code=="MORPH_BINDING"&&d.Message.Contains("Unresolved_Original_Array"))&&missing.Morphs.All(m=>m.Attribute!=MorphAttribute.Position),$"{tag}: a missing target reports its exact native entry instead of collapsing vertices");
        check(project.NativeLayers.Single(l=>l.Name=="MissingBase").Diagnostics.Any(d=>d.Code=="MORPH_BASE_MISSING"),$"{tag}: incompatible source-model morph arrays are reported explicitly");
        try{doc.SetLayer("SCN0",project.NativeLayers.Single(l=>l.Kind=="SCN0").Id);check(false,"Scene remains skipped");}catch(NotSupportedException){check(true,$"{tag}: scene animation cannot be manually activated");}
        string saved=Path.Combine(work,"morph.fighterport");await ProjectStore.SaveAsync(project,saved);var reloaded=new EditorDocument(await ProjectStore.LoadAsync(saved));reloaded.Seek(15.25);doc.Seek(15.25);check(reloaded.EvaluateMorph()!.VertexData.SequenceEqual(doc.EvaluateMorph()!.VertexData),$"{tag}: native prepared morph geometry deterministically survives project reopen");
        check(ProjectStore.Hash(input)==hash&&ProjectStore.ValidateSources(project).Length==0,$"{tag}: original input and all native source hashes remain unchanged");
        if(privateModel is not null){var sourceModel=await adapter.ImportModelAsync(privateModel);check(AnimationControls.DeformChecksum(sourceModel.Scene)==AnimationControls.DeformChecksum(project.Scene),$"{tag}: authored morph arrays preserve every existing deform bone, bind, vertex position and skin weight");check(sourceModel.Scene.Textures.Length>0&&sourceModel.Scene.Textures.All(t=>project.Scene.Textures.Any(p=>p.Name==t.Name&&p.Width==t.Width&&p.Height==t.Height&&p.Rgba.SequenceEqual(t.Rgba))),$"{tag}: authored native package retains authentic decoded textures and palette pixels");}
        else check(shape.Morphs.Any(b=>b.Attribute==MorphAttribute.Color1),$"{tag}: original native fixture exercises actual second-color morph targets");
        await File.WriteAllTextAsync(Path.Combine(work,"morph-errors.json"),JsonSerializer.Serialize(new{Position=positionError,Normal=normalError,Color0=colorError,Color1=color1Error,Shape=shape.Resource,Bindings=shape.Morphs.Length},new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false}));
        await File.WriteAllTextAsync(Path.Combine(directory,tag+"-project-path.txt"),saved);
        await MorphEditingChecks.NativeAsync(adapter,project,work,check,privateMotion);
        await MorphGeometryChecks.NativeAsync(adapter,project,work,check);
    }
}
