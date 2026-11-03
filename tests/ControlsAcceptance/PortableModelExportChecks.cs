using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using System.Windows.Forms;

internal static partial class Program
{
    static void VerifyPortableRiggedGltf(string bundle,int controllers)
    {
        var raw=File.ReadAllBytes(Path.Combine(bundle,"Model.glb"));
        int jsonLength=BitConverter.ToInt32(raw,12);
        var json=(System.Collections.Generic.Dictionary<string,object>)new JavaScriptSerializer{MaxJsonLength=64*1024*1024}
            .DeserializeObject(Encoding.UTF8.GetString(raw,20,jsonLength).TrimEnd());
        var skins=(object[])json["skins"];
        var meshes=(object[])json["meshes"];
        var nodes=(object[])json["nodes"];
        Check(!json.ContainsKey("animations")&&skins.Length==controllers&&meshes.Length>=controllers&&nodes.Length>skins.Length&&
            meshes.Cast<System.Collections.Generic.Dictionary<string,object>>().All(mesh=>
            {
                var primitive=(System.Collections.Generic.Dictionary<string,object>)((object[])mesh["primitives"])[0];
                var attributes=(System.Collections.Generic.Dictionary<string,object>)primitive["attributes"];
                return attributes.ContainsKey("POSITION")&&attributes.ContainsKey("JOINTS_0")&&
                    attributes.ContainsKey("WEIGHTS_0");
            })&&File.Exists(Path.Combine(bundle,"Model.gltf"))&&File.Exists(Path.Combine(bundle,"Model.bin")),
            "Model-only glTF/GLB preserves every controller and skin without animation clips");
    }
    static void PortableModelExportChecks(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Portable model acceptance requires the private desktop.");
        var texture=new Bt3NativeMesh.Texture{Width=2,Height=2,Rgba=new byte[]{255,0,0,255,0,255,0,255,0,0,255,255,255,255,255,255}};
        var scene=new Bt3NativeMesh.Scene{Textures=new[]{texture}};
        var mesh=new Bt3NativeRender.PosedMesh{Texture=0,
            Positions=new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(0,1,0)},
            Normals=Enumerable.Repeat(Vector3.UnitZ,3).ToArray(),
            Uvs=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1)}};
        string destination=Path.Combine(output,"synthetic-bt3-portable");
        PortableModelExport.Publish(destination,stage=>PortableModelExport.WriteBt3Snapshot(scene,new[]{mesh},stage),null,CancellationToken.None);
        Check(File.Exists(Path.Combine(destination,"Model.dae"))&&File.Exists(Path.Combine(destination,"Model.glb"))&&
            File.Exists(Path.Combine(destination,"Model.gltf"))&&File.Exists(Path.Combine(destination,"Model.bin"))&&
            File.Exists(Path.Combine(destination,"Texture-000.png"))&&
            File.Exists(Path.Combine(destination,"Export-info.txt")),"Portable snapshot publishes a complete model, texture and scope receipt");
        byte[] glb=File.ReadAllBytes(Path.Combine(destination,"Model.glb"));
        int jsonLength=BitConverter.ToInt32(glb,12),binLength=BitConverter.ToInt32(glb,20+jsonLength);
        var gltf=(System.Collections.Generic.Dictionary<string,object>)new JavaScriptSerializer()
            .DeserializeObject(Encoding.UTF8.GetString(glb,20,jsonLength).TrimEnd());
        Check(BitConverter.ToUInt32(glb,0)==0x46546C67&&BitConverter.ToInt32(glb,4)==2&&
            BitConverter.ToInt32(glb,8)==glb.Length&&BitConverter.ToUInt32(glb,16)==0x4E4F534A&&
            BitConverter.ToUInt32(glb,24+jsonLength)==0x004E4942&&28+jsonLength+binLength==glb.Length&&
            ((object[])gltf["meshes"]).Length==1&&((object[])gltf["images"]).Length==1&&
            !gltf.ContainsKey("skins"),"glTF 2.0 binary chunks account for the complete static mesh and embedded image");
        var separated=(System.Collections.Generic.Dictionary<string,object>)new JavaScriptSerializer()
            .DeserializeObject(File.ReadAllText(Path.Combine(destination,"Model.gltf")));
        var separateBuffer=(System.Collections.Generic.Dictionary<string,object>)((object[])separated["buffers"])[0];
        Check((string)separateBuffer["uri"]=="Model.bin"&&
            File.ReadAllBytes(Path.Combine(destination,"Model.bin")).SequenceEqual(glb.Skip(28+jsonLength)),
            "Separated glTF references the same verified binary geometry and images as GLB");
        var document=XDocument.Load(Path.Combine(destination,"Model.dae"));var ns=XNamespace.Get("http://www.collada.org/2005/11/COLLADASchema");
        Check(document.Descendants(ns+"triangles").Single().Attribute("count")?.Value=="1"&&
            document.Descendants(ns+"float_array").Count()==3&&
            document.Descendants(ns+"image").Single().Descendants(ns+"init_from").Single().Value=="Texture-000.png",
            "Portable geometry and material reference the actual encoded PNG");
        using(var png=new System.Drawing.Bitmap(Path.Combine(destination,"Texture-000.png")))
            Check(png.Width==2&&png.Height==2&&png.GetPixel(0,0).R==255&&png.GetPixel(0,0).A==255&&
                png.GetPixel(1,0).G==255,"Portable texture preserves RGBA channel order");
        bool collision=false;try{PortableModelExport.Publish(destination,_=>{},null,CancellationToken.None);}catch(IOException){collision=true;}
        Check(collision&&File.Exists(Path.Combine(destination,"Model.dae")),"Existing interchange destination is retained without overwrite");
        string cancelled=Path.Combine(output,"cancelled-bt3-portable");
        using(var token=new CancellationTokenSource())
        {
            token.Cancel();bool stopped=false;
            try{PortableModelExport.Publish(cancelled,_=>{},null,token.Token);}catch(OperationCanceledException){stopped=true;}
            Check(stopped&&!Directory.Exists(cancelled),"Cancellation publishes no incomplete interchange bundle");
        }
        string damaged=Path.Combine(output,"damaged-bt3-portable");bool rejected=false;
        try{PortableModelExport.Publish(damaged,stage=>
        {
            PortableModelExport.WriteBt3Snapshot(scene,new[]{mesh},stage);
            using(var file=File.OpenWrite(Path.Combine(stage,"Model.glb")))file.WriteByte(0);
        },null,CancellationToken.None);}
        catch(InvalidDataException){rejected=true;}
        Check(rejected&&!Directory.Exists(damaged),"Corrupt GLB serialization is rejected before directory publication");
        Check(mesh.Positions[1].X==1&&texture.Rgba[0]==255,
            "Portable export leaves the evaluated model snapshot and original texture bytes unchanged");
        using(var menuProject=FighterProject.Create("Portable model menu"))using(var form=new ModelForm())
        {
            form.InitializeFighterProject(menuProject);
            var menu=(ToolStripDropDownButton)Field(form,"_nextAnimationMenu");
            var item=menu.DropDownItems.Cast<ToolStripItem>().Single(command=>command.Name=="nextExportPortableModel");
            Check(item.Text.Contains("model + textures")&&item.AccessibleDescription.Contains("COLLADA"),
                "The existing Model Previewer menu exposes one discoverable portable model export action");
            var bt3Panel=(Control)Call(form,"BuildBt3NativeEditPanel");
            var bt3Button=bt3Panel.Controls.Cast<Control>().Single(control=>control.Name=="bt3ExportPortableModel");
            Check(bt3Button.Text.Contains("model + textures")&&
                bt3Button.AccessibleDescription.Contains("without animation clips"),
                "BT3 model export is visibly separate from animation clips");
        }
        int riggedArgument=Array.IndexOf(args,"--portable-rigged-input");
        if(riggedArgument>=0)
        {
            string input=args[riggedArgument+1],roundtrip=Path.Combine(output,"rigged-portable-roundtrip");
            if(!File.Exists(Path.Combine(input,"Model.dae")))throw new FileNotFoundException("The exact staged rigged fixture is unavailable.");
            Directory.CreateDirectory(roundtrip);
            foreach(var source in Directory.GetFiles(input,"*.png"))
                File.Copy(source,Path.Combine(roundtrip,Path.GetFileName(source)));
            File.Copy(Path.Combine(input,"Model.dae"),Path.Combine(roundtrip,"Model.dae"));
            PortableRiggedGltf.Write(roundtrip);
            PortableGlbExport.Verify(roundtrip);
            var xml=XDocument.Load(Path.Combine(roundtrip,"Model.dae"));
            VerifyPortableRiggedGltf(roundtrip,xml.Descendants().Count(element=>element.Name.LocalName=="controller"));
        }
        if(File.Exists(args[0]))
        {
            var inventory=Bt3DiscInventory.Inspect(args[0],CancellationToken.None);
            int slotArgument=Array.IndexOf(args,"--portable-bt3-slot");
            int actionArgument=Array.IndexOf(args,"--portable-bt3-action");
            int slot=slotArgument<0?1:int.Parse(args[slotArgument+1]);
            int actionId=actionArgument<0?0:int.Parse(args[actionArgument+1]);
            var form=inventory.Fighters.Single(fighter=>fighter.Slot==slot);
            var costume=form.Costumes[0];
            var real=Bt3NativeMesh.Read(inventory.ReadOwned(costume,4*1024*1024));
            var restWorld=new System.Collections.Generic.Dictionary<int,Matrix4x4>();
            foreach(var bone in real.Skeleton.Bones)
            {
                var local=Matrix4x4.CreateTranslation(bone.Rest);
                restWorld.Add(bone.Id,bone.Parent<0?local:local*restWorld[bone.Parent]);
            }
            double maxBindDifference=0,maxWeightedDifference=0;int blended=0;
            foreach(var batch in real.Batches)foreach(var vertex in batch.Triangles)
            {
                int id=vertex.Bone<0?batch.Bone:vertex.Bone;
                var bone=real.Skeleton.Bones.Single(b=>b.Id==id);
                if(bone.Parent<0||vertex.Weight<=0||vertex.Weight>=1)continue;
                var first=vertex.Bone<0?batch.PivotA:real.Pivots[id].A;
                var second=vertex.Bone<0?batch.PivotB:real.Pivots[id].B;
                var a=Vector3.Transform(vertex.Position-first,restWorld[id]);
                var b=Vector3.Transform(vertex.Position-second,restWorld[bone.Parent]);
                double difference=Vector3.Distance(a,b);
                maxBindDifference=Math.Max(maxBindDifference,difference);
                maxWeightedDifference=Math.Max(maxWeightedDifference,difference*Math.Min(vertex.Weight,1-vertex.Weight));
                blended++;
            }
            Console.WriteLine("TRACE BT3 glTF skin preflight blended="+blended+
                " maximum per-influence bind-point gap="+maxBindDifference+
                " weighted gap="+maxWeightedDifference);
            var action=form.MotionMembers[0].First(member=>member.PackIndex==actionId);
            var clip=Bt3NativeAssetReader.ReadMotion(inventory.ReadOwned(action,4*1024*1024),actionId);
            int full=clip.Tracks.Count(track=>track.Keys.Length>1&&track.Keys[0].Frame==0&&
                track.Keys[track.Keys.Length-1].Frame==clip.Length);
            int clipped=clip.Tracks.Count(track=>track.Keys.Length>1&&
                (track.Keys[0].Frame>0||track.Keys[track.Keys.Length-1].Frame<clip.Length));
            Console.WriteLine("TRACE BT3 glTF action preflight frames="+clip.Length+
                " tracks="+clip.Tracks.Length+" full-range="+full+" clipped-range="+clipped+
                " single-key="+clip.Tracks.Count(track=>track.Keys.Length==1));
            var shortened=new Bt3NativeAssetReader.Motion{Action=clip.Action,Length=clip.Length,
                Tracks=clip.Tracks.Select((track,index)=>index==0?
                    new Bt3NativeAssetReader.Track{Bone=track.Bone,RotationOnly=track.RotationOnly,
                        Keys=new[]{new Bt3NativeAssetReader.Key{Frame=1,
                            Translation=track.Keys[0].Translation,Rotation=track.Keys[0].Rotation},
                            track.Keys[track.Keys.Length-1]}}:track).ToArray()};
            Check(!PortableBt3Rigged.TryWrite(real,shortened,output,out string limit)&&
                limit.Contains("out-of-range fallback"),
                "Clipped native BT3 tracks cannot be advertised as equivalent glTF animation");
            using(var renderer=new Bt3NativeRender(real))
            {
                var native=renderer.SnapshotPosedMeshes();
                string bundle=Path.Combine(output,"local-bt3-costume-portable");
                bool rigged=false;
                PortableModelExport.Publish(bundle,stage=>rigged=PortableModelExport.WriteBt3WithRig(real,native,clip,stage),null,CancellationToken.None);
                var xml=XDocument.Load(Path.Combine(bundle,"Model.dae"));
                int triangles=xml.Descendants(ns+"triangles").Sum(element=>int.Parse(element.Attribute("count").Value));
                Check(triangles==real.TriangleCount&&xml.Descendants(ns+"image").Count()==real.Textures.Length&&
                    Directory.GetFiles(bundle,"*.png").Length==real.Textures.Length,
                    "Exact local BT3 costume exports every decoded triangle and texture without a disc write");
                Check(rigged&&xml.Descendants(ns+"controller").Count()==real.Batches.Length,
                    "Representable BT3 bind and full-range action publish every native skin batch");
                var glbReal=File.ReadAllBytes(Path.Combine(bundle,"Model.glb"));
                int glbJsonLength=BitConverter.ToInt32(glbReal,12);
                var glbJson=(System.Collections.Generic.Dictionary<string,object>)new JavaScriptSerializer()
                    .DeserializeObject(Encoding.UTF8.GetString(glbReal,20,glbJsonLength).TrimEnd());
                Check(((object[])glbJson["animations"]).Length==1&&
                    ((object[])glbJson["skins"]).Length==real.Batches.Length,
                    "BT3 action encoding can preserve the selected native action and complete rigged mesh");
                string modelOnly=Path.Combine(output,"local-bt3-model-only");
                PortableModelExport.Publish(modelOnly,stage=>
                    PortableModelExport.WriteBt3WithRig(real,native,null,stage),null,CancellationToken.None);
                var modelBytes=File.ReadAllBytes(Path.Combine(modelOnly,"Model.glb"));
                int modelJsonLength=BitConverter.ToInt32(modelBytes,12);
                var modelJson=(System.Collections.Generic.Dictionary<string,object>)new JavaScriptSerializer()
                    .DeserializeObject(Encoding.UTF8.GetString(modelBytes,20,modelJsonLength).TrimEnd());
                Check(!modelJson.ContainsKey("animations")&&
                    ((object[])modelJson["skins"]).Length==real.Batches.Length&&
                    File.ReadAllText(Path.Combine(modelOnly,"Export-info.txt")).Contains("without animation clips"),
                    "BT3 model export keeps native skin and textures but contains no animation clips");
                float[] probeFrames={0,.25f,Math.Min(clip.Length,clip.Length/2f+.5f),clip.Length};
                using(var samples=new BinaryWriter(File.Create(Path.Combine(output,"bt3-native-pose-probes.bin"))))
                    foreach(float probe in probeFrames)
                    {
                        renderer.Apply(clip,probe);
                        foreach(var draw in renderer.SnapshotPosedMeshes())
                            foreach(var position in draw.Positions)
                            {samples.Write(position.X);samples.Write(position.Y);samples.Write(position.Z);}
                    }
                File.WriteAllText(Path.Combine(output,"bt3-native-pose-probes.json"),
                    new JavaScriptSerializer().Serialize(new{Frames=probeFrames,Vertices=real.Batches.Sum(b=>b.Triangles.Length)}));
                Console.WriteLine("TRACE local BT3 portable triangles="+triangles+" textures="+real.Textures.Length+
                    " DAE bytes="+new FileInfo(Path.Combine(bundle,"Model.dae")).Length);
            }
        }
        int brawlArgument=Array.IndexOf(args,"--portable-brawl-model");
        if(brawlArgument>=0)
        {
            if(brawlArgument+1>=args.Length||!File.Exists(args[brawlArgument+1]))throw new FileNotFoundException("The exact Brawl model fixture is unavailable.");
            using(var project=FighterProject.Create("Portable Brawl check"))
            {
                project.Model=project.StageAsset(args[brawlArgument+1],"model");
                using(var root=NodeFactory.FromFile(null,project.Model.WorkingPath))
                {
                    var target=ModelPanel.CollectModels(root).OfType<MDL0Node>().First();
                    string bundle=Path.Combine(output,"local-brawl-portable");
                    PortableModelExport.Publish(bundle,stage=>PortableModelExport.WriteBrawl(project,target.TreePath,stage),null,CancellationToken.None);
                    var xml=XDocument.Load(Path.Combine(bundle,"Model.dae"));
                    int decoded=Directory.GetFiles(bundle,"*.png").Length;
                    int missing=File.Exists(Path.Combine(bundle,"Missing-textures.txt"))?
                        File.ReadAllLines(Path.Combine(bundle,"Missing-textures.txt")).Length:0;
                    int referenced=xml.Descendants().Count(element=>element.Name.LocalName=="image");
                    Console.WriteLine("TRACE Brawl embedded TEX0="+root.GetChildrenRecursive().OfType<TEX0Node>().Count()+
                        " referenced="+referenced+" decoded="+decoded+" missing="+missing);
                    Check(xml.Descendants().Any(element=>element.Name.LocalName=="geometry")&&
                        xml.Descendants().Any(element=>element.Name.LocalName=="controller")&&
                        decoded+missing==referenced,
                        "Saved Brawl model exports rigged COLLADA geometry and accounts for every referenced texture");
                    VerifyPortableRiggedGltf(bundle,xml.Descendants().Count(element=>element.Name.LocalName=="controller"));
                    Console.WriteLine("TRACE local Brawl portable model="+target.Name+" decoded="+decoded+" unresolved="+missing+
                        " DAE bytes="+new FileInfo(Path.Combine(bundle,"Model.dae")).Length);
                }
            }
        }
        int meleeArgument=Array.IndexOf(args,"--portable-melee-project");
        if(meleeArgument>=0)
        {
            if(meleeArgument+1>=args.Length||!File.Exists(args[meleeArgument+1]))throw new FileNotFoundException("The exact Melee project fixture is unavailable.");
            using(var project=FighterProject.Open(args[meleeArgument+1]))
            {
                string before=project.Model.ContentSha256,bundle=Path.Combine(output,"local-melee-portable");
                PortableModelExport.Publish(bundle,stage=>PortableModelExport.WriteMelee(project,stage),null,CancellationToken.None);
                var xml=XDocument.Load(Path.Combine(bundle,"Model.dae"));
                int images=xml.Descendants().Count(element=>element.Name.LocalName=="image");
                bool relativeImages=xml.Descendants().Where(element=>element.Name.LocalName=="image")
                    .Select(element=>element.Descendants().Single(source=>source.Name.LocalName=="init_from").Value)
                    .All(file=>file==Path.GetFileName(file)&&File.Exists(Path.Combine(bundle,file)));
                Check(xml.Descendants().Any(element=>element.Name.LocalName=="geometry")&&
                    xml.Descendants().Any(element=>element.Name.LocalName=="controller")&&
                    images>0&&relativeImages&&Directory.GetFiles(bundle,"*.png").Length==images&&project.Model.ContentSha256==before,
                    "Owned Melee costume exports rigged normal-detail COLLADA and diffuse PNGs without changing the model");
                VerifyPortableRiggedGltf(bundle,xml.Descendants().Count(element=>element.Name.LocalName=="controller"));
                Console.WriteLine("TRACE local Melee portable images="+images+
                    " DAE bytes="+new FileInfo(Path.Combine(bundle,"Model.dae")).Length);
            }
        }
    }
}
