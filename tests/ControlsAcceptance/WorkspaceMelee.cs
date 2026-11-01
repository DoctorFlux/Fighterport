using BrawlCrate.Studio;
using BrawlCrate.UI;
using FighterPort.Adapters.Melee;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
internal static partial class Program
{
    static async Task WorkspaceMelee(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Melee UI acceptance requires the private desktop.");
        string image=args[0],previous=Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",Path.Combine(output,"library"));
        try
        {
            if(args.Contains("--melee-projection-only"))
            {
                using(var raw=new MeleeSource(image))using(var scratch=new NativeSourceScratch())
                {
                    var model=MeleeNative.ReadMembers(raw.Read("PlMrNr.dat")).SelectMany(MeleeNative.Models).Single();
                    string notice;using(var projected=MeleeLibraryPreview.Project(model,scratch,out notice))Check(projected.AllBones.Count==61,"Direct real Melee projection preserves native joints");
                }
                return;
            }
            var library=new AnimationLibrary();var request=new LibraryGameRequest{Game="Melee",GameVersion="NTSC-U 1.02",Path=image,ApproveBuild=true};
            var plan=await Task.Run(()=>MeleeGameSource.Prepare(request,library.Read(),CancellationToken.None,null));
            Check(plan.Source.NativeGameId=="GALE01"&&plan.Source.NativeRevision==2,"Real fixture is independently identified as Melee USA 1.02, not by filename");
            Check(plan.Models.Count==156&&plan.Clips.Count>6000,"Real disc enumerates native costume models and exact motion members without character selection");
            int added=await Task.Run(()=>library.RegisterGame(plan));var catalogue=library.Read();
            Check(added==plan.Clips.Count&&catalogue.GameLocations.Single().Provider==MeleeSource.DiscProvider,"Melee inventory publishes and fresh catalogue load validates its native provider");
            var wait=library.Browse("Wait1").First(i=>i.Model?.Character=="Mario"&&i.Clip.Name=="Wait1");
            Check(wait.State==LibrarySourceState.Available&&library.GetPreviewState(wait.Clip.Id)==LibrarySourceState.Available,"Actual Melee Wait1 is available through native association and source state routing");
            var costumes=library.GetPreviewCostumes(wait.Clip.Id);
            Check(costumes.Length>=5&&costumes.Count(c=>c.IsCanonical)==1,"Costume choices use exact full-rig compatibility inside this source instance");
            using(var lease=library.OpenPreview(wait.Clip.Id))
            {
                Check(lease.MeleePlayback.JointCount==61&&lease.Model.AllBones.Count==61,"Native Melee model and display projection preserve all 61 joint identities");
                Console.WriteLine("TRACE MESH "+lease.Model.Objects.Length+" vertices "+lease.Model.PolygonList.OfType<MDL0ObjectNode>().Sum(o=>o.Vertices?.Count??0)+" bounds "+lease.Model.GetBox());
                Check(lease.Model.PolygonList.OfType<MDL0ObjectNode>().Sum(o=>o.Vertices?.Count??0)>0,"Original Melee source model includes decoded native geometry in the retained viewport");
                string rig=MeleeNative.RigHash(lease.MeleeModel);var first=lease.MeleePlayback.Sample(0,0);
                MeleeLibraryPreview.Apply(lease,1);MeleeLibraryPreview.Apply(lease,12.25f);
                Check(MeleeNative.RigHash(lease.MeleeModel)==rig,"Fractional source playback never changes the authentic native rig");
                var next=library.Browse("Wait2").First(i=>i.Model?.Id==wait.Model.Id&&i.Clip.Name=="Wait2");
                var model=lease.Model;
                Check(library.TrySelectPreview(lease,next.Clip.Id,wait.Model.Id)&&ReferenceEquals(model,lease.Model),"Same-model source selection prepares the next native motion without scene/model reconstruction");
            }
            using(var startup=MainForm.StartViewerApplication())
            {
                var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");form.WindowState=FormWindowState.Normal;form.ClientSize=new Size(1700,950);
                Call(form,"OpenSavedGameLibrary",library,catalogue.GameLocations.Single());await GameLibrarySelect(form,wait.Clip.Id);
                await GameLibraryWait(()=>Field(form,"_libraryLease") is LibrarySourceLease lease&&lease.Reference.ClipId==wait.Clip.Id,"Automatic Melee source preview did not settle.");
                var active=(LibrarySourceLease)Field(form,"_libraryLease");Console.WriteLine("TRACE RENDER "+active.Model.IsRendering+" draws "+active.Model.DrawCalls.Count+" box "+active.Model.GetBox());
                foreach(var o in active.Model.PolygonList.OfType<MDL0ObjectNode>().Take(3))Console.WriteLine("TRACE OBJECT "+o.Name+" vertices "+o.Vertices.Count+" visible "+o.IsRendering+" calls "+o._drawCalls.Count);
                Check(active.MeleeRenderer!=null,"Melee uses native HSD material draw calls in the existing native ModelPanel");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"melee-source-wide.png"));
                Check(active.MeleeRenderer.RenderCount>0,"Native HSD shader and mesh draw calls execute in the shared offscreen viewport");
                form.ClientSize=new Size(1050,900);await Task.Delay(100);await CaptureSimpleWorkspace(form,Path.Combine(output,"melee-source-compact.png"));
                Call(form,"SetWorkspaceTextSize",175);await Task.Delay(100);await CaptureSimpleWorkspace(form,Path.Combine(output,"melee-source-compact-175.png"));Call(form,"SetWorkspaceTextSize",100);
                foreach(var fixture in new[]{Tuple.Create("Fox","Wait1"),Tuple.Create("Samus","Wait"),Tuple.Create("Peach","Wait1"),Tuple.Create("Donkey","Wait")})
                {
                    string actor=fixture.Item1;var item=library.Browse(fixture.Item2).FirstOrDefault(i=>i.Model?.Character==actor&&i.Clip.Name==fixture.Item2);
                    if(item==null){Console.WriteLine("MISSING exact requested native idle fixture: "+actor);continue;}
                    var search=(TextBox)Field(form,"_librarySearch");search.Text=fixture.Item2;((System.Windows.Forms.Timer)Field(form,"_librarySearchTimer")).Stop();Call(form,"RefreshLibrary",item.Clip.Id);
                    var tree=(TreeView)Field(form,"_libraryList");await GameLibraryWait(()=>AllNodes(tree.Nodes).Any(n=>(n.Tag as LibrarySearchItem)?.Clip.Id==item.Clip.Id),"Native exact motion tree item did not settle.");tree.SelectedNode=AllNodes(tree.Nodes).Single(n=>(n.Tag as LibrarySearchItem)?.Clip.Id==item.Clip.Id);
                    await GameLibraryWait(()=>Field(form,"_libraryLease") is LibrarySourceLease ready&&ready.Reference.ClipId==item.Clip.Id,"Melee corpus source preview did not settle: "+actor+"; issue "+Field(form,"_simpleWorkspaceSourceIssue"));
                    var ready=(LibrarySourceLease)Field(form,"_libraryLease");MeleeLibraryPreview.Apply(ready,Math.Min(12.25f,ready.PreviewAnimation.FrameCount));
                    Check(ready.MeleeRenderer.Parts!=null,"Exact same-source fighter data provides unambiguous native visibility tables for "+actor+": "+ready.PreviewNotice);
                    if(actor=="Peach")
                    {
                        int[] choices=Enumerable.Range(0,ready.MeleeRenderer.Parts.GroupCount).Select(ready.MeleeRenderer.Parts.Choice).ToArray();
                        foreach(int textSize in new[]{100,175})
                        {
                            Call(form,"SetWorkspaceTextSize",textSize);Exception failure=null;
                            using(var timer=new System.Windows.Forms.Timer{Interval=150})
                            {
                                timer.Tick+=async(sender,eventArgs)=>
                                {
                                    timer.Stop();var window=Application.OpenForms.Cast<Form>().Single(f=>f.Name=="nativeSourceParts");
                                    try
                                    {
                                        ((ComboBox)window.Controls.Find("nativePartGroup3",true).Single()).SelectedIndex=0;
                                        await CaptureWindow(window,Path.Combine(output,"native-parts-"+textSize+".png"));
                                        ((Button)window.Controls.Find("cancelNativeParts",true).Single()).PerformClick();
                                    }
                                    catch(Exception e){failure=e;window.DialogResult=DialogResult.Cancel;window.Close();}
                                };
                                timer.Start();Call(form,"ShowMeleeSourceParts");
                            }
                            if(failure!=null)throw failure;
                            Check(choices.SequenceEqual(Enumerable.Range(0,ready.MeleeRenderer.Parts.GroupCount).Select(ready.MeleeRenderer.Parts.Choice)),"Cancelling native part review restores source-only visibility at text "+textSize);
                        }
                        Call(form,"SetWorkspaceTextSize",100);
                    }
                    if(args.Contains("--melee-reviewed-parts")&&actor=="Peach")
                    {
                        // Explicit fixture review only: the native table's fourth group is
                        // the held parasol, and its sixth is the alternative dress.
                        // No corresponding character/group rules exist in application code.
                        ready.MeleeRenderer.Parts.Select(3,-1);ready.MeleeRenderer.Parts.Select(5,-1);ready.MeleeRenderer.RefreshParts();
                    }
                    Console.WriteLine("PARTS "+actor+" "+string.Join(",",Enumerable.Range(0,ready.MeleeRenderer.Parts.GroupCount).Select(ready.MeleeRenderer.Parts.Choice)));
                    var view=(BrawlLib.Internal.Windows.Controls.Model_Panel.ModelPanel)Field(form,"_libraryViewport");view.Capture();view.Invalidate();view.Update();
                    using(var bitmap=view.GetScreenshot(view.ClientRectangle,false))bitmap.Save(Path.Combine(output,"melee-"+actor+"-native.png"));
                    if(args.Contains("--melee-detail"))
                    {
                        var originalParent=view.Parent;var originalDock=view.Dock;
                        using(var detail=new Form{ClientSize=new Size(850,850),Text="Background native detail"})
                        {
                            detail.Controls.Add(view);view.Dock=DockStyle.Fill;detail.Show();await Task.Delay(50);
                            foreach(float angle in new[]{0f,90f,180f})
                            {
                                var box=ready.NativePreview.RenderTarget.GetBox();var center=(box.Min+box.Max)*.5f;float radius=(box.Max-box.Min).TrueDistance(new BrawlLib.Internal.Vector3())*1.8f;
                                double radians=angle*Math.PI/180;
                                view.Camera.SetProjectionParams(1,45,200000,.1f);
                                view.Camera.Set(center+new BrawlLib.Internal.Vector3((float)Math.Sin(radians)*radius,0,(float)Math.Cos(radians)*radius),new BrawlLib.Internal.Vector3(0,angle,0),new BrawlLib.Internal.Vector3(1));
                                view.Capture();view.Invalidate();view.Update();await Task.Delay(100);view.Invalidate();view.Update();
                                using(var bitmap=view.GetScreenshot(view.ClientRectangle,false))
                                {
                                    int nonBackground=0;for(int x=0;x<bitmap.Width;x+=4)for(int y=0;y<bitmap.Height;y+=4){var pixel=bitmap.GetPixel(x,y);if(Math.Abs(pixel.R-35)+Math.Abs(pixel.G-39)+Math.Abs(pixel.B-46)>30)nonBackground++;}
                                    Check(nonBackground>250,"Detailed native capture contains model pixels: "+actor+" angle "+angle);
                                    bitmap.Save(Path.Combine(output,"detail-"+actor+"-"+angle+".png"));
                                }
                            }
                            originalParent.Controls.Add(view);view.Dock=originalDock;
                        }
                    }
                    Check(ready.MeleeRenderer.RenderCount>0&&ready.MeleePlayback.JointCount==ready.Model.AllBones.Count,"Actual native HSD rendering/animation succeeds for held-out "+actor+" fixture");
                }
                Check(form.FighterEditor.TargetModel==null,"Library browsing does not replace the target document");

            }
            var modelChoice=catalogue.Models.Single(m=>m.Id==wait.Model.Id);
            string nativeOutput=Path.Combine(output,"native-original-export");
            MeleeGameSource.ExportOriginal(catalogue,wait.Clip,modelChoice,nativeOutput,CancellationToken.None);
            foreach(var mount in new[]{catalogue.Mounts.Single(m=>m.Id==modelChoice.MountId),catalogue.Mounts.Single(m=>m.Id==wait.Clip.MountId)})
                Check(FighterProject.HashFile(Path.Combine(nativeOutput,mount.VirtualPath.Replace('/',Path.DirectorySeparatorChar)))==mount.ContainerSha256,"Exported original native file reopens byte-exact: "+mount.VirtualPath);
            bool rejected=false;try{MeleeGameSource.ExportOriginal(catalogue,wait.Clip,modelChoice,nativeOutput,CancellationToken.None);}catch(IOException){rejected=true;}
            Check(rejected,"Existing native export folder is rejected without replacing its files");
            string overrideA=Path.Combine(output,"mod-a"),overrideB=Path.Combine(output,"mod-b");Directory.CreateDirectory(overrideA);Directory.CreateDirectory(overrideB);
            var modelMount=catalogue.Mounts.Single(m=>m.Id==modelChoice.MountId);byte[] originalModel=MeleeGameSource.Read(modelMount,CancellationToken.None);
            File.WriteAllBytes(Path.Combine(overrideA,modelMount.VirtualPath),originalModel);File.WriteAllBytes(Path.Combine(overrideB,modelMount.VirtualPath),originalModel);
            foreach(string folder in new[]{overrideA,overrideB})
            {
                var mod=await Task.Run(()=>MeleeGameSource.Prepare(new LibraryGameRequest{Game="Melee",GameVersion="procedural linked mod fixture",Path=folder,BaseSourceId=catalogue.GameLocations.Single().Id,ApproveBuild=true},library.Read(),CancellationToken.None,null));
                await Task.Run(()=>library.RegisterGame(mod));
            }
            var instances=library.Read();var mods=instances.GameLocations.Where(l=>l.BaseSourceId!=null).ToArray();
            Check(mods.Length==2&&mods[0].Id!=mods[1].Id,"Two identical-byte native mod folders have independent saved source identities");
            foreach(var mod in mods)
            {
                var own=library.Browse(new LibraryQuery{GameSourceId=mod.Id,Text="Wait1"}).First(i=>i.Model?.Character=="Mario"&&i.Clip.Name=="Wait1");
                Check(library.GetCostumes(own.Clip.Id).All(c=>instances.Mounts.Single(m=>m.Id==instances.Models.Single(model=>model.Id==c.ModelId).MountId).GameSourceId==mod.Id),"Costume choices never borrow another mod instance or vanilla source");
            }
            library.RenameSavedGameSource(mods[0].Id,"My independent Melee pack");
            Check(library.Read().GameLocations.Single(l=>l.Id==mods[0].Id).Label=="My independent Melee pack","Melee mod instance renaming uses existing compact saved-source management");
            Check(FighterProject.HashFile(image)==plan.Source.ContainerSha256,"Real source disc hash is unchanged after index, preview and native sampling");
        }
        finally{Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",previous);}
    }
}
