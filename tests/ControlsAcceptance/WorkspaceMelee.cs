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
        MeleeMasterBridgeChecks(output);
        if(args.Contains("--melee-native-project"))
        {
            string path=args[Array.IndexOf(args,"--melee-native-project")+1];
            using(var project=FighterProject.Open(path))
            using(var modelRoot=NodeFactory.FromFile(null,project.Model.WorkingPath))
            using(var clipRoot=NodeFactory.FromFile(null,project.Animations.Single(a=>a.Retarget!=null).WorkingPath))
            {
                var target=BrawlLibrarySource.Resources(modelRoot).Select(r=>r.Item1).OfType<MDL0Node>().Single();
                var clip=BrawlLibrarySource.Resources(clipRoot).Select(r=>r.Item1).OfType<CHR0Node>().Single();
                var member=project.NativeTarget.Members.Single(m=>m.Name=="Wait1");
                Check(project.NativeTarget.Member(clip)==null,"Cross-game result has no native Melee action association");
                var writer=(INativeActionDestinationExportAdapter)NativeGameAdapters.ForNative(project.NativeTarget.Provider);
                string destination=Path.Combine(output,"cross-game-native-action.dat");
                int fractional=clip.Children.OfType<CHR0EntryNode>().Sum(e=>e.KeyArrays.Sum(array=>array.Entries.Count(key=>key._index!=(float)Math.Floor(key._index))));
                Console.WriteLine("TRACE cross-game native writer fractional authored keys="+fractional+" frames="+clip.FrameCount);
                bool guarded=false;
                try
                {
                    var receipt=writer.ExportAnimationToAction(project,target,clip,member,destination,CancellationToken.None,()=>{});
                    Check(receipt.ResourceKey==member.MotionKey&&receipt.SampleTimes>clip.FrameCount,"Cross-game target result writes a verified native Melee action member");
                }
                catch(NotSupportedException error)when(error.Message.Contains("integer frame waits"))
                {guarded=true;Console.WriteLine("OPEN cross-game native output: "+error.Message);}
                Check(!guarded||fractional>0&&!File.Exists(destination),"Unrepresentable fractional native times reject without a published DAT");
            }
            return;
        }
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
            Check(catalogue.GameLocations.Single().InventoryIssues.SequenceEqual(plan.Issues),"Complete source inventory diagnostics survive catalogue Save/reopen");
            Check(added==plan.Clips.Count&&catalogue.GameLocations.Single().Provider==MeleeSource.DiscProvider,"Melee inventory publishes and fresh catalogue load validates its native provider");
            if(args.Contains("--melee-shared-bridge")){await MeleeSharedBridge(args,output,library);return;}
            string actorArg=args.Contains("--melee-target-character")?args[Array.IndexOf(args,"--melee-target-character")+1]:"Mario";
            string nextIdleArg=args.Contains("--melee-target-next")?args[Array.IndexOf(args,"--melee-target-next")+1]:"Wait2";
            string idleArg=args.Contains("--melee-target-idle")?args[Array.IndexOf(args,"--melee-target-idle")+1]:"Wait1";
            var wait=library.Browse(idleArg).First(i=>i.Model?.Character==actorArg&&i.Clip.Name==idleArg);
            Check(wait.State==LibrarySourceState.Available&&library.GetPreviewState(wait.Clip.Id)==LibrarySourceState.Available,"Actual "+actorArg+" "+idleArg+" is available through native association and source state routing");
            var costumes=library.GetPreviewCostumes(wait.Clip.Id);
            int expectedCostumes=catalogue.Models.Count(m=>m.SkeletonSha256==wait.Model.SkeletonSha256&&m.MotionMountIds.Contains(wait.Clip.MountId));
            Check(costumes.Length==expectedCostumes&&expectedCostumes>0&&costumes.Count(c=>c.IsCanonical)==1,"Costume choices include every exact compatible native rig inside this source instance");
            using(var lease=library.OpenPreview(wait.Clip.Id))
            {
                Check(lease.MeleePlayback.JointCount==lease.Model.AllBones.Count,"Native Melee model and display projection preserve every authentic joint identity for "+actorArg);
                Console.WriteLine("TRACE MESH "+lease.Model.Objects.Length+" vertices "+lease.Model.PolygonList.OfType<MDL0ObjectNode>().Sum(o=>o.Vertices?.Count??0)+" bounds "+lease.Model.GetBox());
                Check(lease.Model.PolygonList.OfType<MDL0ObjectNode>().Sum(o=>o.Vertices?.Count??0)>0,"Original Melee source model includes decoded native geometry in the retained viewport");
                if(args.Contains("--melee-target-prepare"))
                {
                    using(var prepared=MeleeTargetDocument.Prepare(lease,library.Read(),CancellationToken.None))
                    {ModelForm.ValidateFighterProjectAssets(prepared);prepared.Save(Path.Combine(output,"prepared-melee.fpstudio"));Check(prepared.NativeTarget!=null,"Prepared native target owns native snapshots and editable masters");}
                    return;
                }
                string rig=MeleeNative.RigHash(lease.MeleeModel);var first=lease.MeleePlayback.Sample(0,0);
                MeleeLibraryPreview.Apply(lease,1);MeleeLibraryPreview.Apply(lease,12.25f);
                Check(MeleeNative.RigHash(lease.MeleeModel)==rig,"Fractional source playback never changes the authentic native rig");
                var next=library.Browse(nextIdleArg).FirstOrDefault(i=>i.Model?.Id==wait.Model.Id&&i.Clip.Name==nextIdleArg);
                if(next==null)throw new InvalidDataException("Exact held-out "+nextIdleArg+" fixture is missing for "+actorArg+"; no replacement was chosen.");
                var model=lease.Model;
                Check(library.TrySelectPreview(lease,next.Clip.Id,wait.Model.Id)&&ReferenceEquals(model,lease.Model),"Same-model source selection prepares the next native motion without scene/model reconstruction");
            }
            using(var startup=MainForm.StartViewerApplication())
            {
                var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");form.WindowState=FormWindowState.Normal;form.ClientSize=new Size(1700,950);
                if(!args.Contains("--melee-target-only"))
                {
                    using(var manager=new SavedGameSourcesDialog(library))
                    {
                        manager.Show();Exception failure=null;
                        using(var timer=new System.Windows.Forms.Timer{Interval=150})
                        {
                            timer.Tick+=async(sender,eventArgs)=>{timer.Stop();var dialog=Application.OpenForms.Cast<Form>().Single(f=>f.Name=="sourceInventoryIssues");try{Check(((TextBox)dialog.Controls.Find("sourceIssueDetails",true).Single()).Lines.SequenceEqual(plan.Issues),"On-demand source issues show every retained diagnostic without truncation");await CaptureWindow(dialog,Path.Combine(output,"source-issues.png"));((Button)dialog.Controls.Find("sourceIssuesClose",true).Single()).PerformClick();}catch(Exception error){failure=error;dialog.Close();}};
                            timer.Start();((Button)manager.Controls.Find("savedSourceIssues",true).Single()).PerformClick();
                        }
                        if(failure!=null)throw failure;manager.Close();
                    }
                }
                Call(form,"OpenSavedGameLibrary",library,catalogue.GameLocations.Single());await GameLibrarySelect(form,wait.Clip.Id,idleArg);
                await GameLibraryWait(()=>Field(form,"_libraryLease") is LibrarySourceLease lease&&lease.Reference.ClipId==wait.Clip.Id,"Automatic Melee source preview did not settle.");
                var active=(LibrarySourceLease)Field(form,"_libraryLease");Console.WriteLine("TRACE RENDER "+active.Model.IsRendering+" draws "+active.Model.DrawCalls.Count+" box "+active.Model.GetBox());
                foreach(var o in active.Model.PolygonList.OfType<MDL0ObjectNode>().Take(3))Console.WriteLine("TRACE OBJECT "+o.Name+" vertices "+o.Vertices.Count+" visible "+o.IsRendering+" calls "+o._drawCalls.Count);
                Check(active.MeleeRenderer!=null,"Melee uses native HSD material draw calls in the existing native ModelPanel");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"melee-source-wide.png"));
                Check(active.MeleeRenderer.RenderCount>0,"Native HSD shader and mesh draw calls execute in the shared offscreen viewport");
                form.ClientSize=new Size(1050,900);await Task.Delay(100);await CaptureSimpleWorkspace(form,Path.Combine(output,"melee-source-compact.png"));
                Call(form,"SetWorkspaceTextSize",175);await Task.Delay(100);await CaptureSimpleWorkspace(form,Path.Combine(output,"melee-source-compact-175.png"));Call(form,"SetWorkspaceTextSize",100);
                if(!args.Contains("--melee-target-only"))foreach(var fixture in new[]{Tuple.Create("Fox","Wait1"),Tuple.Create("Samus","Wait"),Tuple.Create("Peach","Wait1"),Tuple.Create("Donkey","Wait")})
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
                if(args.Contains("--melee-editable-target"))
                {
                    await GameLibrarySelect(form,wait.Clip.Id,idleArg);
                    await GameLibraryWait(()=>Field(form,"_libraryLease") is LibrarySourceLease ready&&ready.Reference.ClipId==wait.Clip.Id,"Exact editable source did not settle.");
                    Check((bool)form.GetType().GetProperty("LibraryPreviewReady",Private).GetValue(form),"Exact native target selection passes normal readiness guards");
                    Call(form,"OpenLibraryAsTarget");
                    Check(ReferenceEquals(form,Field(MainForm.Instance,"_fighterForm")),"Melee Open as target retains the existing full native viewer window");
                    Check(form.FighterProject.TargetGame=="Melee"&&form.FighterProject.NativeTarget!=null&&form.FighterEditor.TargetModel is MDL0Node&&form.FighterEditor.SelectedCHR0!=null,"Melee model and selected motion are owned editable target masters");
                    var target=(MDL0Node)form.FighterEditor.TargetModel;var clip=form.FighterEditor.SelectedCHR0;var nativeBackend=target.NativeEditingBackend as MeleeModelEditingBackend;
                    Check(nativeBackend!=null,"Main target uses authentic native Melee skinning instead of the projection skinning path");
                    if(args.Contains("--melee-native-destinations"))
                    {
                        var binding=form.FighterProject.NativeTarget;
                        var own=binding.Member(clip);
                        var other=binding.Members.FirstOrDefault(m=>m.Name==nextIdleArg&&m.MotionKey!=own.MotionKey);
                        Check(other!=null,"Another exact native action is available for explicit destination review");
                        using(var review=new NativeActionDestinationDialog(clip.Name,binding.Members))
                        {
                            Check(review.SelectedAction==null,"Native destination review never preselects a possibly unrelated action");
                            using(FlowModal("nativeActionChoice",dialog=>
                            {
                                var choice=SimpleControl<ComboBox>(dialog,"nativeActionChoice");
                                choice.SelectedIndex=Array.FindIndex(choice.Items.Cast<string>().ToArray(),name=>name.Contains("["+other.MotionKey+"]"));
                                CaptureDialog(dialog,output,"review-native-destination.png");
                                SimpleControl<Button>(dialog,"nativeActionConfirm").PerformClick();
                            }))Check(review.ShowDialog(form)==DialogResult.OK&&review.SelectedAction.MotionKey==other.MotionKey,"Native action review confirms only the exact selected member");
                        }
                        using(var cancelled=new NativeActionDestinationDialog(clip.Name,binding.Members))
                        using(FlowModal("nativeActionCancel",dialog=>SimpleControl<Button>(dialog,"nativeActionCancel").PerformClick()))
                            Check(cancelled.ShowDialog(form)==DialogResult.Cancel&&cancelled.SelectedAction==null,"Cancelling native action review leaves no destination selected");
                        string originalContent=LifecycleContent(clip),modelHash=MeleeNative.RigHash(MeleeTargetDocument.OriginalModel(form.FighterProject));
                        int originalFrame=form.FighterEditor.CurrentFrame;
                        string outputPath=Path.Combine(output,"explicit-native-destination.dat");
                        var writer=(INativeActionDestinationExportAdapter)NativeGameAdapters.ForNative(binding.Provider);
                        var receipt=writer.ExportAnimationToAction(form.FighterProject,target,clip,other,outputPath,CancellationToken.None,()=>
                        {
                            if(!ReferenceEquals(form.FighterEditor.SelectedCHR0,clip)||LifecycleContent(clip)!=originalContent)throw new InvalidOperationException("Editable clip changed during native output.");
                        });
                        var saved=MeleeNative.ReadMembers(File.ReadAllBytes(outputPath)).SelectMany(MeleeNative.Motions).Single();
                        Check(saved.Symbol==MeleeTargetDocument.OriginalMotion(form.FighterProject,other).Symbol&&receipt.ResourceKey==other.MotionKey&&receipt.SampleTimes>clip.FrameCount,"Explicit destination writes and freshly reopens the chosen native action member");
                        string copyPath=Path.Combine(output,"independent-authoring-copy.chr0"),copyOutput=Path.Combine(output,"independent-native-destination.dat");
                        clip.ExportChecked(copyPath);
                        using(var independent=NodeFactory.FromFile(null,copyPath) as CHR0Node)
                        {
                            Check(independent!=null&&binding.Member(independent)==null,"A separate authored result has no implicit Melee action association");
                            bool missingRejected=false;string missingPath=Path.Combine(output,"unreviewed-destination.dat");
                            try{writer.ExportAnimation(form.FighterProject,target,independent,missingPath,CancellationToken.None,()=>{});}catch(InvalidOperationException){missingRejected=true;}
                            Check(missingRejected&&!File.Exists(missingPath),"An unassociated result cannot silently choose a native action destination");
                            var separateReceipt=writer.ExportAnimationToAction(form.FighterProject,target,independent,other,copyOutput,CancellationToken.None,()=>{});
                            Check(separateReceipt.ResourceKey==other.MotionKey&&MeleeNative.ReadMembers(File.ReadAllBytes(copyOutput)).SelectMany(MeleeNative.Motions).Single().Symbol==saved.Symbol,"Unassociated animation exports through reviewed exact action destination and native reopen");
                        }
                        Check(LifecycleContent(clip)==originalContent&&form.FighterEditor.CurrentFrame==originalFrame&&MeleeNative.RigHash(MeleeTargetDocument.OriginalModel(form.FighterProject))==modelHash,"Native output preserves exact current master, frame and authentic rig");
                        return;
                    }
                    Check(MeleeLibraryPreview.Report(((LibrarySourceLease)Field(form,"_libraryLease")).Model).Meshes.All(mesh=>((LibrarySourceLease)Field(form,"_libraryLease")).MeleeRenderer.Parts.IsNormalDetail(mesh.DisplayObject)),"Native authoring projection contains normal-detail geometry only");
                    form.FighterEditor.SetFrame(12);form.FighterEditor.FrameFighterModel();await CaptureSimpleWorkspace(form,Path.Combine(output,"melee-editable-main.png"));
                    if(actorArg=="Mario")foreach(var dimensions in new[]{new Size(1700,950),new Size(1050,900)})foreach(int textSize in new[]{100,175})
                    {
                        form.ClientSize=dimensions;Call(form,"SetWorkspaceTextSize",textSize);await Task.Delay(80);
                        await CaptureSimpleWorkspace(form,Path.Combine(output,"melee-target-"+dimensions.Width+"-"+textSize+".png"));
                        Check(form.FighterEditor.SelectedCHR0==clip&&form.FighterEditor.CurrentFrame==12,"Native target layout retains exact animation and playhead at "+dimensions.Width+" / "+textSize);
                    }
                    Call(form,"SetWorkspaceTextSize",100);
                    Check(nativeBackend.EditPolicy.Available,"Authentic native common bone metadata authorizes body SRT editing without a projected rig");
                    string content=LifecycleContent(clip);
                    foreach(int protectedJoint in nativeBackend.EditPolicy.ProtectedJoints)
                    {
                        form.FighterEditor.SelectedBone=target.AllBones.Single(b=>b.Name==MeleeProjection.JointName(protectedJoint));
                        var protectedBox=form.FighterEditor.chr0Editor._transBoxes[6];protectedBox.Value+=7;form.FighterEditor.chr0Editor.BoxChangedCreateUndo(protectedBox,EventArgs.Empty);
                        Check(LifecycleContent(clip)==content,"Real native transform fields cannot synthesize protected motion-control keys: "+protectedJoint);
                    }
                    var entry=clip.Children.OfType<CHR0EntryNode>().First(e=>nativeBackend.CanEditAnimationBone(e.Name)&&e.KeyArrays[3].KeyCount>0);
                    float editTime=entry.KeyArrays[3].Entries.Last()._index,before=entry.KeyArrays[3].GetFrameValue(editTime);
                    form.FighterEditor.SetFrame(checked((int)editTime+1));form.FighterEditor.SelectedBone=target.AllBones.Single(b=>b.Name==entry.Name);int frame=form.FighterEditor.CurrentFrame;
                    var protectedNames=new System.Collections.Generic.HashSet<string>(nativeBackend.EditPolicy.ProtectedJoints.Select(MeleeProjection.JointName));
                    string ProtectedCurves()=>new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(clip.Children.OfType<CHR0EntryNode>().Where(e=>protectedNames.Contains(e.Name)).Select(e=>new{e.Name,Keys=e.KeyArrays.Select(a=>a.Entries.Select(k=>new{k._index,k._value,k._tangent}).ToArray()).ToArray()}).ToArray());
                    string protectedContent=ProtectedCurves();
                    string nativeOriginal=Path.Combine(output,"owned-original-action.dat");form.ExportFighterResource(nativeOriginal);
                    Check(FighterProject.HashFile(nativeOriginal)==form.FighterProject.NativeTarget.NativeMotionSha256,"Unedited target native export preserves the exact original action-member bytes before project Save");
                    var bodyBox=form.FighterEditor.chr0Editor._transBoxes[3];bodyBox.Value=before+5;form.FighterEditor.chr0Editor.BoxChangedCreateUndo(bodyBox,EventArgs.Empty);await Task.Delay(60);
                    Check(Math.Abs(entry.KeyArrays[3].GetFrameValue(editTime)-(before+5))<.00001,"Actual native rotation field edits the current eligible body bone");
                    Check(ProtectedCurves()==protectedContent,"Body SRT editing preserves every protected source-owned motion curve");
                    form.FighterEditor.FrameFighterModel();await CaptureSimpleWorkspace(form,Path.Combine(output,"melee-body-edited.png"));
                    string staleOutput=Path.Combine(output,"stale-native-action.dat");int costumeBefore=form.FighterProject.NativeTarget.CostumeIndex;bool staleRejected=false;
                    try
                    {
                        var adapter=(INativeTargetExportAdapter)NativeGameAdapters.ForNative(form.FighterProject.NativeTarget.Provider);
                        adapter.ExportAnimation(form.FighterProject,target,clip,staleOutput,CancellationToken.None,()=>form.FighterProject.NativeTarget.CostumeIndex++);
                    }
                    catch(InvalidOperationException){staleRejected=true;}
                    finally{form.FighterProject.NativeTarget.CostumeIndex=costumeBefore;}
                    Check(staleRejected&&!File.Exists(staleOutput),"Native export rejects in-place member or costume dependency changes without publishing output");
                    string nativeEdited=Path.Combine(output,"owned-edited-action.dat");form.ExportFighterResource(nativeEdited);
                    File.WriteAllText(Path.Combine(output,"native-edited-receipt.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(form.LastNativeTargetExport));
                    Check(form.LastNativeTargetExport.SampleTimes>clip.FrameCount&&form.LastNativeTargetExport.NativeVertices>0,"Native export receipt records subframe and complete surface coverage after successful publication");
                    var savedMotion=MeleeNative.ReadMembers(File.ReadAllBytes(nativeEdited)).SelectMany(MeleeNative.Motions).Single();
                    var savedPlayer=new MeleePlayback(MeleeTargetDocument.OriginalModel(form.FighterProject),savedMotion);
                    int editedJoint=Array.IndexOf(target.AllBones.Select(b=>b.Name).ToArray(),entry.Name);
                    Check(Math.Abs(savedPlayer.Sample(editedJoint,editTime)[3]-(before+5)*Math.PI/180)<.000006,"Actual native export reopens the current edited target rotation, without replacing original resources");
                    Check(form.FighterEditor.CurrentFrame==frame,"Native Melee export preserves the current playhead");
                    Check(LifecycleContent(clip)!=content,"Melee target animation edits change the current owned master");
                    var sparseEntry=clip.Children.OfType<CHR0EntryNode>().FirstOrDefault(e=>nativeBackend.CanEditAnimationBone(e.Name)&&Enumerable.Range(6,3).All(a=>e.KeyArrays[a].KeyCount==0)&&target.AllBones.Any(b=>b.Name==e.Name&&(Math.Abs(b.BindState.Translate._y)>.00001||Math.Abs(b.BindState.Translate._z)>.00001)));
                    Check(sparseEntry!=null,"Real native fixture includes a sparse translation group with nonzero bind siblings");
                    string beforeSparse=LifecycleContent(clip);var sparseBone=target.AllBones.Single(b=>b.Name==sparseEntry.Name);form.FighterEditor.SelectedBone=sparseBone;
                    var sparseBox=form.FighterEditor.chr0Editor._transBoxes[6];sparseBox.Value=sparseBone.BindState.Translate._x+.125f;form.FighterEditor.chr0Editor.BoxChangedCreateUndo(sparseBox,EventArgs.Empty);await Task.Delay(60);
                    Check(sparseEntry.KeyArrays[7].GetFrameValue(0)==sparseBone.BindState.Translate._y&&sparseEntry.KeyArrays[8].GetFrameValue(0)==sparseBone.BindState.Translate._z,"Activating a native translation axis preserves the other native bind components");
                    form.ExportFighterResource(Path.Combine(output,"new-translation-group.dat"));
                    form.FighterEditor.Undo();Check(LifecycleContent(clip)==beforeSparse,"Sparse group activation and bind preservation share one native Undo command");
                    using(var curveEditor=new BrawlLib.Internal.Windows.Controls.InterpolationEditor(form.FighterEditor))
                    {
                        curveEditor.SetTarget(sparseEntry);sparseEntry.SetKeyframe(6,0,sparseBone.BindState.Translate._x+.25f);Call(curveEditor,"SignalCurveChange");await Task.Delay(60);
                        Check(sparseEntry.KeyArrays[7].GetFrameValue(0)==sparseBone.BindState.Translate._y&&sparseEntry.KeyArrays[8].GetFrameValue(0)==sparseBone.BindState.Translate._z,"Native interpolation-editor callbacks preserve unanimated bind siblings in their existing transaction");
                        form.FighterEditor.Undo();Check(LifecycleContent(clip)==beforeSparse,"Curve-group activation and bind preservation undo together exactly");
                    }
                    form.FighterEditor.SelectedBone=target.AllBones.Single(b=>b.Name==entry.Name);form.FighterEditor.SetFrame(frame);
                    form.FighterEditor.Undo();Check(LifecycleContent(clip)==content&&form.FighterEditor.CurrentFrame==frame,"Existing native Undo restores the exact Melee animation and frame");
                    form.FighterEditor.Redo();Check(LifecycleContent(clip)!=content,"Existing native Redo reapplies Melee animation editing");
                    string editedContent=LifecycleContent(clip),editedProject=Path.Combine(output,"edited-melee.fpstudio");form.SaveFighterProject(editedProject);form.FighterEditor.Undo();
                    string projectPath=Path.Combine(output,"editable-melee.fpstudio");form.SaveFighterProject(projectPath);
                    using(var reopened=FighterProject.Open(projectPath))
                    {
                        ModelForm.ValidateFighterProjectAssets(reopened);
                        Check(reopened.TargetGame=="Melee"&&reopened.NativeSources.Count>=3,"Project Save/reopen retains owned Melee source snapshots and exact editable masters");
                        using(var reopenedRoot=NodeFactory.FromFile(null,reopened.Animations[0].WorkingPath))
                        {var reopenedClip=BrawlLibrarySource.Resolve(reopenedRoot,form.FighterProject.NativeTarget.Member(clip).EntryKey) as CHR0Node;Check(reopenedClip!=null&&LifecycleContent(reopenedClip)==content,"Project reopen preserves exact authored Melee animation curves");}
                    }
                    var loaded=FighterProject.Open(projectPath);bool adopted=false;
                    try{adopted=form.ReplaceLibraryTarget(loaded);Check(adopted,"Saved Melee project reopens into the existing editable main viewer");}finally{if(!adopted)loaded.Dispose();}
                    await SimpleControlsReady(form);
                    Check(form.FighterEditor.TargetModel is MDL0Node loadedModel&&loadedModel.NativeEditingBackend!=null&&LifecycleContent(form.FighterEditor.SelectedCHR0)==content,"Full native target backend and exact curves recover after actual project reopen");
                    string reopenedOutput=Path.Combine(output,"reopened-native-action.dat");form.ExportFighterResource(reopenedOutput);
                    Check(FighterProject.HashFile(reopenedOutput)==form.FighterProject.NativeTarget.NativeMotionSha256,"Native action export remains byte-exact after project Save/reopen");
                    await CaptureSimpleWorkspace(form,Path.Combine(output,"melee-editable-reopened.png"));
                    var editedLoaded=FighterProject.Open(editedProject);bool editedAdopted=false;
                    try
                    {
                        using(FlowModal("unsavedProjectDiscard",dialog=>SimpleControl<Button>(dialog,"unsavedProjectDiscard").PerformClick()))
                            editedAdopted=form.ReplaceLibraryTarget(editedLoaded);
                        Check(editedAdopted,"Saved edited Melee project reopens into the same main viewer");
                    }
                    finally{if(!editedAdopted)editedLoaded.Dispose();}
                    await SimpleControlsReady(form);
                    Check(LifecycleContent(form.FighterEditor.SelectedCHR0)==editedContent,"Actual main-view reopen retains exact body edits and protected curves");
                    string editedReopenedOutput=Path.Combine(output,"reopened-edited-native-action.dat");form.ExportFighterResource(editedReopenedOutput);
                    Check(FighterProject.HashFile(editedReopenedOutput)==FighterProject.HashFile(nativeEdited),"Edited native output remains deterministic after project Save and actual reopen");
                    await CaptureSimpleWorkspace(form,Path.Combine(output,"melee-body-edited-reopened.png"));
                    var currentMaster=form.FighterEditor.SelectedCHR0;int currentFrame=form.FighterEditor.CurrentFrame;
                    var nextSource=library.Browse(nextIdleArg).First(i=>i.Model?.Id==wait.Model.Id&&i.Clip.Name==nextIdleArg);
                    await GameLibrarySelect(form,nextSource.Clip.Id,nextIdleArg);
                    await GameLibraryWait(()=>Field(form,"_libraryLease") is LibrarySourceLease selected&&selected.Reference.ClipId==nextSource.Clip.Id,"Same-model source change did not settle.");
                    Check(ReferenceEquals(currentMaster,form.FighterEditor.SelectedCHR0)&&form.FighterEditor.CurrentFrame==currentFrame&&LifecycleContent(currentMaster)==editedContent,"Browsing a different original native action never replaces or edits the owned target master");
                    form.SaveFighterProject(projectPath);
                }


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
