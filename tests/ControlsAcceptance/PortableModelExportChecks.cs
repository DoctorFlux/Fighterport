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
        }
        if(File.Exists(args[0]))
        {
            var inventory=Bt3DiscInventory.Inspect(args[0],CancellationToken.None);
            var costume=inventory.Fighters.Single(form=>form.Slot==1).Costumes[0];
            var real=Bt3NativeMesh.Read(inventory.ReadOwned(costume,4*1024*1024));
            using(var renderer=new Bt3NativeRender(real))
            {
                var native=renderer.SnapshotPosedMeshes();
                string bundle=Path.Combine(output,"local-bt3-costume-portable");
                PortableModelExport.Publish(bundle,stage=>PortableModelExport.WriteBt3Snapshot(real,native,stage),null,CancellationToken.None);
                var xml=XDocument.Load(Path.Combine(bundle,"Model.dae"));
                int triangles=xml.Descendants(ns+"triangles").Sum(element=>int.Parse(element.Attribute("count").Value));
                Check(triangles==real.TriangleCount&&xml.Descendants(ns+"image").Count()==real.Textures.Length&&
                    Directory.GetFiles(bundle,"*.png").Length==real.Textures.Length,
                    "Exact local BT3 costume exports every decoded triangle and texture without a disc write");
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
                Console.WriteLine("TRACE local Melee portable images="+images+
                    " DAE bytes="+new FileInfo(Path.Combine(bundle,"Model.dae")).Length);
            }
        }
    }
}
