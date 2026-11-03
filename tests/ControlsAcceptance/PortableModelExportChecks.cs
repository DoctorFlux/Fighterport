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
            var motionButton=bt3Panel.Controls.Cast<Control>().Single(control=>control.Name=="bt3ExportPortableAnimations");
            Check(motionButton.Text.Contains("animations")&&motionButton.AccessibleDescription.Contains("Maya .anim"),
                "BT3 viewer exposes a separate, discoverable animation export action");
            var importMenu=(ToolStripDropDownButton)Field(form,"_portableImportMenu");
            var exportMenu=(ToolStripDropDownButton)Field(form,"_portableExportMenu");
            Check(importMenu.Text=="Import"&&exportMenu.Text=="Export"&&
                importMenu.DropDownItems.Cast<ToolStripItem>().Any(command=>command.Name=="portableImportAnimations")&&
                exportMenu.DropDownItems.Cast<ToolStripItem>().Any(command=>command.Name=="portableExportAnimations"),
                "Top toolbar keeps visible import and export menus for the active target");
            var left=form.FighterEditor.leftPanel;
            int exportedAction=-1;
            left.ShowNativeViewer(new Panel(),new[]{new BrawlCrate.UI.Model_Previewer.NativeViewerItem(7,"Native action 007")},
                Array.Empty<BrawlCrate.UI.Model_Previewer.NativeViewerItem>(),
                Array.Empty<BrawlCrate.UI.Model_Previewer.NativeViewerItem>(),_=>{},(id,visible)=>{},
                (id,visible)=>{},id=>exportedAction=id);
            left.SelectNativeAnimation(7);
            var nativeExport=left.listAnims.ContextMenuStrip.Items.Cast<ToolStripItem>()
                .Single(command=>command.Name=="nativeExportSelectedAnimation");
            nativeExport.PerformClick();
            Check(exportedAction==7,"Native animation rows expose exact-member export from right click");
            left.HideNativeViewer();
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
                string animationOnly=Path.Combine(output,"bt3-animation-only-draft");
                Directory.CreateDirectory(animationOnly);
                File.Copy(Path.Combine(bundle,"Model.dae"),Path.Combine(animationOnly,"Model.dae"));
                var first=PortableBt3Rigged.ToClip(clip);
                var second=new PortableRiggedGltf.Clip{Name=first.Name+"-second",
                    Identity=first.Identity+"-second",FramesPerSecond=first.FramesPerSecond,Tracks=first.Tracks};
                PortableRiggedGltf.WriteClips(animationOnly,new[]{first,second},false);
                PortableGlbExport.Verify(animationOnly);
                var motionBytes=File.ReadAllBytes(Path.Combine(animationOnly,"Model.glb"));
                int motionJsonLength=BitConverter.ToInt32(motionBytes,12);
                var motionJson=(System.Collections.Generic.Dictionary<string,object>)new JavaScriptSerializer()
                    .DeserializeObject(Encoding.UTF8.GetString(motionBytes,20,motionJsonLength).TrimEnd());
                Check(((object[])motionJson["animations"]).Length==2&&
                    !motionJson.ContainsKey("meshes")&&!motionJson.ContainsKey("skins"),
                    "Independent animation GLB can hold multiple clips without model geometry");
                void PrepareAnimationScene(string stage)
                {
                    File.Copy(Path.Combine(modelOnly,"Model.dae"),Path.Combine(stage,"Model.dae"));
                    foreach(string png in Directory.GetFiles(modelOnly,"*.png"))
                        File.Copy(png,Path.Combine(stage,Path.GetFileName(png)));
                }
                string grouped=Path.Combine(output,"bt3-grouped-animation-export");
                var groupedFiles=PortableAnimationExport.Publish(grouped,PrepareAnimationScene,
                    new[]{first,second},false,false,null,CancellationToken.None);
                Check(groupedFiles.Length==1&&Directory.GetFiles(grouped,"*.glb").Length==1&&
                    !File.Exists(Path.Combine(grouped,"Model.dae"))&&
                    File.ReadAllText(Path.Combine(grouped,"Export-info.txt")).Contains("Clip count: 2"),
                    "Combined animation export publishes one model-free GLB for selected clips");
                string separate=Path.Combine(output,"bt3-separate-animation-export");
                var separateFiles=PortableAnimationExport.Publish(separate,PrepareAnimationScene,
                    new[]{first,second},true,true,null,CancellationToken.None);
                Check(separateFiles.Length==2&&Directory.GetFiles(separate,"*.glb").Length==2&&
                    separateFiles.All(file=>File.Exists(Path.Combine(separate,file)))&&
                    File.ReadAllText(Path.Combine(separate,"Export-info.txt")).Contains("Model included: True"),
                    "One-per-clip export publishes distinct GLBs with rigged model included");
                string maya=Path.Combine(output,"bt3-maya-animation-export");
                var mayaFiles=PortableMayaAnimation.Publish(maya,new[]{first,second},null,CancellationToken.None);
                string mayaText=File.ReadAllText(Path.Combine(maya,mayaFiles[0]));
                Check(mayaFiles.Length==2&&mayaFiles.Distinct(StringComparer.OrdinalIgnoreCase).Count()==2&&
                    Directory.GetFiles(maya,"*.anim").Length==2&&
                    mayaText.StartsWith("animVersion 1.1;")&&mayaText.Contains("timeUnit ntscf;")&&
                    mayaText.Contains("anim rotate.rotateX rotateX BT3_J")&&
                    !File.Exists(Path.Combine(maya,"Model.dae"))&&
                    File.ReadAllText(Path.Combine(maya,"Export-info.txt")).Contains("XYZ local Euler"),
                    "Exact BT3 action selection exports separate portable Maya .anim curves without a CHR0 document");
                bool mayaExistingRejected=false;
                try{PortableMayaAnimation.Publish(maya,new[]{first},null,CancellationToken.None);}
                catch(IOException){mayaExistingRejected=true;}
                Check(mayaExistingRejected&&File.Exists(Path.Combine(maya,mayaFiles[0])),
                    "Existing Maya animation bundle is preserved when a second export is rejected");
                string mayaCancelled=Path.Combine(output,"bt3-maya-cancelled");
                using(var stopMaya=new CancellationTokenSource())
                {
                    stopMaya.Cancel();bool stoppedMaya=false;
                    try{PortableMayaAnimation.Publish(mayaCancelled,new[]{first},null,stopMaya.Token);}
                    catch(OperationCanceledException){stoppedMaya=true;}
                    Check(stoppedMaya&&!Directory.Exists(mayaCancelled),
                        "Cancelled Maya animation export publishes no partial folder");
                }
                string mayaStale=Path.Combine(output,"bt3-maya-stale");
                bool staleMaya=false;
                try{PortableMayaAnimation.Publish(mayaStale,new[]{first},
                    ()=>throw new InvalidOperationException("Source changed"),CancellationToken.None);}
                catch(InvalidOperationException){staleMaya=true;}
                Check(staleMaya&&!Directory.Exists(mayaStale),
                    "Changed Maya animation dependencies block publication");
                var oneFrame=new PortableRiggedGltf.Clip{Name="One frame",Identity="procedural-one-frame",
                    FramesPerSecond=60,Tracks=new[]{new PortableRiggedGltf.Track{Bone="BT3_J0000",
                        Frames=new[]{0f},Translation=new[]{new Vector3(1,2,3)},
                        Rotation=new[]{Quaternion.Identity}}}};
                string mayaOne=Path.Combine(output,"procedural-maya-one-frame");
                var oneFile=PortableMayaAnimation.Publish(mayaOne,new[]{oneFrame},null,CancellationToken.None).Single();
                Check(File.ReadAllText(Path.Combine(mayaOne,oneFile)).Contains("endTime 0;")&&
                    File.ReadAllText(Path.Combine(mayaOne,oneFile)).Contains("translateX BT3_J0000"),
                    "One-frame Maya .anim retains exact native-frame timing and translation without extra frames");
                int otherAction=actionId==10?0:10;
                var otherMember=form.MotionMembers[0].Single(member=>member.PackIndex==otherAction);
                var otherMotion=Bt3NativeAssetReader.ReadMotion(inventory.ReadOwned(otherMember,4*1024*1024),otherAction);
                var otherClip=PortableBt3Rigged.ToClip(otherMotion);
                string realPair=Path.Combine(output,"bt3-distinct-native-action-pair");
                var pairFiles=PortableAnimationExport.Publish(realPair,PrepareAnimationScene,
                    new[]{first,otherClip},false,false,null,CancellationToken.None);
                var pairBytes=File.ReadAllBytes(Path.Combine(realPair,pairFiles.Single()));
                int pairJsonLength=BitConverter.ToInt32(pairBytes,12);
                var pairJson=(System.Collections.Generic.Dictionary<string,object>)new JavaScriptSerializer()
                    .DeserializeObject(Encoding.UTF8.GetString(pairBytes,20,pairJsonLength).TrimEnd());
                var pairAnimations=((object[])pairJson["animations"])
                    .Cast<System.Collections.Generic.Dictionary<string,object>>().ToArray();
                Check(pairAnimations.Length==2&&otherMotion.Action!=clip.Action&&
                    (string)pairAnimations[0]["name"]!=(string)pairAnimations[1]["name"],
                    "Distinct exact native actions share one model-free GLB without substituting the current action");
                bool existingRejected=false;
                try{PortableAnimationExport.Publish(realPair,PrepareAnimationScene,
                    new[]{first},true,false,null,CancellationToken.None);}
                catch(IOException){existingRejected=true;}
                Check(existingRejected&&File.Exists(Path.Combine(realPair,pairFiles.Single())),
                    "Existing animation bundle is never overwritten");
                string cancelledPair=Path.Combine(output,"bt3-cancelled-action-pair");
                using(var stopped=new CancellationTokenSource())
                {
                    stopped.Cancel();bool cancelledExport=false;
                    try{PortableAnimationExport.Publish(cancelledPair,PrepareAnimationScene,
                        new[]{first},true,false,null,stopped.Token);}
                    catch(OperationCanceledException){cancelledExport=true;}
                    Check(cancelledExport&&!Directory.Exists(cancelledPair),
                        "Cancelled portable animation export publishes no partial folder");
                }
                string stalePair=Path.Combine(output,"bt3-stale-action-pair");
                bool staleRejected=false;
                try{PortableAnimationExport.Publish(stalePair,PrepareAnimationScene,
                    new[]{first},true,false,()=>throw new InvalidOperationException("Stale exact action."),CancellationToken.None);}
                catch(InvalidOperationException){staleRejected=true;}
                Check(staleRejected&&!Directory.Exists(stalePair),
                    "Changed target or action dependencies block animation publication");
                byte[] originalMotionBytes=inventory.ReadOwned(action,4*1024*1024);
                var editor=new Bt3NativeMotionEditor(originalMotionBytes,real.Skeleton,actionId);
                var editable=clip.Tracks.First(track=>track.Bone<71&&!track.RotationOnly);
                var firstKey=editable.Keys.First();
                editor.SetKey(editable.Bone,firstKey.Frame,
                    firstKey.Translation+new Vector3(0.01f,0,0),firstKey.Rotation);
                var editedMotion=Bt3NativeAssetReader.ReadMotion(editor.Export(),actionId);
                var editedClip=PortableBt3Rigged.ToClip(editedMotion);
                string editedFolder=Path.Combine(output,"bt3-current-edited-action-export");
                PortableAnimationExport.Publish(editedFolder,PrepareAnimationScene,
                    new[]{editedClip},true,false,null,CancellationToken.None);
                Check(editedMotion.SourceSha256!=clip.SourceSha256&&
                    editedClip.Tracks.Single(track=>track.Bone=="BT3_J"+editable.Bone.ToString("D4"))
                        .Translation[0]!=first.Tracks.Single(track=>track.Bone=="BT3_J"+editable.Bone.ToString("D4"))
                        .Translation[0]&&originalMotionBytes.SequenceEqual(inventory.ReadOwned(action,4*1024*1024)),
                    "Current edited native action exports an independent clip without changing the disc member");
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
