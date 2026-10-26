using BrawlCrate;
using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task Flow39Select(ModelForm form,LibrarySearchItem item)
    {
        Console.WriteLine("TRACE automatic selection "+item.Clip.SourceCharacter+" "+item.Clip.Name+" "+item.Clip.Channel);
        SimpleControl<TextBox>(form,"librarySearch").Text=item.Clip.SourceCharacter+" "+item.Clip.Name;
        ((Timer)Field(form,"_librarySearchTimer")).Stop();Call(form,"RefreshLibrary",item.Clip.Id);
        await LibraryQuerySettled(form);
        var tree=SimpleControl<TreeView>(form,"libraryClips");
        tree.SelectedNode=AllNodes(tree.Nodes).Single(n=>(n.Tag as LibrarySearchItem)?.Clip.Id==item.Clip.Id);
        await GameLibraryWait(()=>Field(form,"_libraryLease") is LibrarySourceLease source&&source.Reference.ClipId==item.Clip.Id,"Automatic source selection did not settle.");
    }
    static float[] Flow39Pose(MDL0Node model)=>model.AllBones.SelectMany(b=>new[]{b.FrameState._translate._x,b.FrameState._translate._y,b.FrameState._translate._z,
        b.FrameState._rotate._x,b.FrameState._rotate._y,b.FrameState._rotate._z,b.FrameState._scale._x,b.FrameState._scale._y,b.FrameState._scale._z}).ToArray();
    static async Task WorkspaceLibraryFlow(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Library flow requires the private desktop.");
        string folder=Path.Combine(output,"library");Directory.CreateDirectory(folder);File.Copy(args[Array.IndexOf(args,"--library-index")+1],Path.Combine(folder,"catalogue.xml"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",folder);Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_TEMP_ROOT",Path.Combine(output,"transient"));
        ModelForm form=null;
        try
        {
            Console.WriteLine("TRACE starting library flow viewer");
            using(var startup=MainForm.StartViewerApplication())
            {
                form=(ModelForm)Field(MainForm.Instance,"_fighterForm");form.ClientSize=new Size(1700,950);Console.WriteLine("TRACE empty viewer ready");await SimpleControlsReady(form);Console.WriteLine("TRACE empty controls ready");
                Call(form,"ImportFighterModel",args[0]);Call(form,"ImportFighterAnimations",new[]{new[]{args[1]}});await SimpleControlsReady(form);
                Console.WriteLine("TRACE target imported");Call(form,"UseSharedSourceLibrary");await LibraryQuerySettled(form);Console.WriteLine("TRACE library outline ready");
                var library=(AnimationLibrary)Field(form,"_library");var editor=form.FighterEditor;editor.SetFrame(3);
                var witness=new GameLibraryDocument(form);var tree=SimpleControl<TreeView>(form,"libraryClips");
                tree.SelectedNode=AllNodes(tree.Nodes).Single(n=>n.Level==1&&n.Text=="sonic");
                await GameLibraryWait(()=>Field(form,"_libraryLease") is LibrarySourceLease source&&source.Model.Name=="FitSonic00","Character selection did not load its default original model.");
                Check(((LibrarySourceLease)Field(form,"_libraryLease")).Model.Name=="FitSonic00","Selecting Sonic's character row automatically loads its default verified model");witness.CheckPreserved("automatic character preview");
                var rows=library.Browse(new LibraryQuery{Character="sonic",Channel="CHR0 skeletal"});
                var first=rows.Single(i=>i.Clip.Name=="AttackHi3"&&i.Model?.Name=="FitSonic00");var second=rows.Single(i=>i.Clip.Name=="AttackHi4"&&i.Model?.Name=="FitSonic00");
                await Flow39Select(form,first);var prepared=(LibrarySourceLease)Field(form,"_libraryLease");
                int opens=AnimationLibrary.DiagnosticOpens,reads=BrawlDiscImage.DiagnosticNativeReads,hashes=FighterProject.DiagnosticFileHashes,bakes=BrawlRetargeter.DiagnosticBakes;
                await Flow39Select(form,second);
                Check(ReferenceEquals(prepared,Field(form,"_libraryLease"))&&prepared.PreviewAnimation.Name=="AttackHi4","AttackHi3 to AttackHi4 follows the source selection using the same native lease/model");
                Check(opens==AnimationLibrary.DiagnosticOpens&&reads==BrawlDiscImage.DiagnosticNativeReads&&hashes==FighterProject.DiagnosticFileHashes&&bakes==BrawlRetargeter.DiagnosticBakes,"Same-package animation switching performs no decode, disc reads, file hashes or bake");witness.CheckPreserved("automatic same-model animation switch");
                opens=AnimationLibrary.DiagnosticOpens;reads=BrawlDiscImage.DiagnosticNativeReads;hashes=FighterProject.DiagnosticFileHashes;
                SimpleControl<Button>(form,"libraryOriginalPlay").PerformClick();await Task.Delay(120);Call(form,"PauseSourceOperation");
                Check(opens==AnimationLibrary.DiagnosticOpens&&reads==BrawlDiscImage.DiagnosticNativeReads&&hashes==FighterProject.DiagnosticFileHashes,"Automatic preview playback performs no source loading or file I/O");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"automatic-sonic-attackhi4.png"));
                // Force an actual pending decode while still on this UI turn,
                // then restore Sonic before a completion can be published.
                SimpleControl<TextBox>(form,"librarySearch").Text="";((Timer)Field(form,"_librarySearchTimer")).Stop();Call(form,"RefreshLibrary",(object)null);await LibraryQuerySettled(form);
                tree.SelectedNode=AllNodes(tree.Nodes).Single(n=>n.Level==1&&n.Text=="mario");
                int deferred=AnimationLibrary.DiagnosticOpens;editor.PlayAnim();Call(form,"StartLibraryAutoPreview");
                Check(editor.Playing&&AnimationLibrary.DiagnosticOpens==deferred&&((Timer)Field(form,"_libraryAutoTimer")).Enabled,"Pending automatic source loading waits while native target playback runs");editor.StopAnim();editor.SetFrame(3);
                Call(form,"StartLibraryAutoPreview");
                tree.SelectedNode=AllNodes(tree.Nodes).Single(n=>n.Level==1&&n.Text=="snake");Call(form,"StartLibraryAutoPreview");
                tree.SelectedNode=AllNodes(tree.Nodes).Single(n=>n.Level==1&&n.Text=="sonic");
                await GameLibraryWait(()=>Field(form,"_libraryLease") is LibrarySourceLease pending&&pending.Model.Name=="FitSonic00"&&Field(form,"_libraryAutoCancel")==null,"Stale automatic source work did not settle.");await Task.Delay(300);
                Check(ReferenceEquals(prepared,Field(form,"_libraryLease"))&&prepared.Model.Name=="FitSonic00","Rapid character changes cancel stale source work and retain the final exact prepared source");witness.CheckPreserved("cancelled stale automatic loads");
                await SourceFitPixels(form,Path.Combine(output,"automatic-source-geometry.png"));
                var loopItem=library.Browse(new LibraryQuery{Channel="CHR0 skeletal"}).First(i=>i.Clip.Loop&&i.State==LibrarySourceState.Available);
                await Flow39Select(form,loopItem);
                var loopSource=(LibrarySourceLease)Field(form,"_libraryLease");float fractional=loopSource.Animation.FrameCount+.5f;
                Check(loopSource.Animation.Loop,"Exact native source supplies an authentic stored loop for fractional boundary sampling");
                loopSource.Model.ApplyCHR(loopSource.Animation,fractional);var nativeBoundary=Flow39Pose(loopSource.Model);
                form.GetType().GetField("_libraryTime",Private).SetValue(form,(double)fractional-1);Call(form,"RenderLibraryFrame");
                Check(nativeBoundary.SequenceEqual(Flow39Pose(loopSource.Model)),"Source bundle preserves the native fractional closing interval instead of clamping a looping primary clip to its last frame");
                form.GetType().GetField("_libraryTime",Private).SetValue(form,0d);Call(form,"RenderLibraryFrame");
                foreach(string channel in new[]{"CHR0 skeletal","SRT0Node","PAT0Node","VIS0Node","CLR0Node","SHP0Node","SCN0Node"})
                {
                    var item=library.Browse(new LibraryQuery{Channel=channel}).First(i=>library.GetPreviewState(i.Clip.Id)==LibrarySourceState.Available);
                    await Flow39Select(form,item);var source=(LibrarySourceLease)Field(form,"_libraryLease");
                    Check(source.PreviewAnimation.Name==item.Clip.Name&&source.Reference.Channel==channel&&source.Reference.MotionResourceSha256==item.Clip.ResourceSha256,"Automatic source preview opens exact native channel: "+channel);
                    Check(SimpleControl<Button>(form,"libraryPreviewOriginal").Enabled&&SimpleControl<Button>(form,"libraryOpenTarget").Enabled&&(channel=="CHR0 skeletal"||!SimpleControl<Button>(form,"libraryUseOnTarget").Enabled),"Preview/target opening and skeletal retarget eligibility are separate for "+channel);
                    string content=BrawlLibrarySource.ResourceHash(source.PreviewAnimation);int before=BrawlDiscImage.DiagnosticNativeReads;
                    SimpleControl<TrackBar>(form,"libraryOriginalFrame").Value=Math.Min(2,source.PreviewAnimation.FrameCount);
                    Check(BrawlLibrarySource.ResourceHash(source.PreviewAnimation)==content&&BrawlDiscImage.DiagnosticNativeReads==before,"Native channel sampling preserves source bytes and performs no new loading: "+channel);
                    using(var targetCopy=ModelForm.PrepareLibraryTarget(source))
                    using(var targetRoot=NodeFactory.FromFile(null,targetCopy.Animations.Single().WorkingPath))
                        Check(BrawlLibrarySource.ResourceHash(BrawlLibrarySource.Resolve(targetRoot,source.Reference.MotionKey))==content,"Opening as target prepares the exact independent native channel: "+channel);
                    await CaptureSimpleWorkspace(form,Path.Combine(output,"source-channel-"+channel.Split(' ')[0]+".png"));
                }
                var mario=library.Browse(new LibraryQuery{Character="mario",Channel="CHR0 skeletal",Text="Wait1"}).Single(i=>i.Clip.Name=="Wait1"&&i.Model?.Name=="FitMario00");await Flow39Select(form,mario);
                await GameLibraryCostumeLayouts(form,output);
                var originalForm=form;var originalEditor=editor;var viewport=editor.ModelPanel;var sourceLease=Field(form,"_libraryLease");var worker=Field(form,"_controlEngine");var size=form.ClientSize;
                bool cancelled=false;
                using(FlowModal("unsavedProjectCancel",dialog=>{cancelled=true;SimpleControl<Button>(dialog,"unsavedProjectCancel").PerformClick();}))SimpleControl<Button>(form,"libraryOpenTarget").PerformClick();
                Check(cancelled&&ReferenceEquals(form.FighterEditor,editor)&&ReferenceEquals(sourceLease,Field(form,"_libraryLease")),"Cancelled same-window target opening preserves the editor and prepared source");witness.CheckPreserved("cancelled same-window target replacement");
                using(FlowModal("unsavedProjectDiscard",dialog=>SimpleControl<Button>(dialog,"unsavedProjectDiscard").PerformClick()))SimpleControl<Button>(form,"libraryOpenTarget").PerformClick();
                await SimpleControlsReady(form);
                Check(!form.IsDisposed&&ReferenceEquals(Field(MainForm.Instance,"_fighterForm"),originalForm)&&ReferenceEquals(form.FighterEditor,originalEditor)&&ReferenceEquals(editor.ModelPanel,viewport)&&ReferenceEquals(Field(form,"_controlEngine"),worker)&&form.ClientSize==size,"Open as target retains the same application window, native editor, viewport, worker and size");
                Check(ReferenceEquals(sourceLease,Field(form,"_libraryLease"))&&form.FighterEditor.SelectedCHR0.Name=="Wait1"&&form.FighterProject.Animations.Single().Retarget==null,"Target replacement keeps the prepared source and exact independently owned native animation");
                await ReviewBodyEdit(form,3);string edited=LifecycleContent(editor.SelectedCHR0);editor.Undo();Check(LifecycleContent(editor.SelectedCHR0)!=edited,"Same-window target retains native body Undo");editor.Redo();Check(LifecycleContent(editor.SelectedCHR0)==edited,"Same-window target retains native body Redo");
                string project=Path.Combine(output,"same-window-target.fpstudio");form.SaveFighterProject(project);using(var saved=FighterProject.Open(project))Check(saved.ModelNodePath==form.FighterProject.ModelNodePath,"Same-window target saves and reopens its exact model identity");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"same-window-target-edited.png"));
                // Open directly from the menu with no prepared small preview.
                // Cancel must not publish it; Save then commits both exact views.
                await SimpleControlsReady(form);Console.WriteLine("TRACE cold source release");
                Call(form,"ReleaseLibraryPreview");Call(form,"SetSimpleWorkspaceLibraryVisible",false);Call(form,"MarkFighterChanged");Console.WriteLine("TRACE cold source hidden");
                var cold=new GameLibraryDocument(form);Console.WriteLine("TRACE cold source cancel command");
                using(FlowModal("unsavedProjectCancel",dialog=>SimpleControl<Button>(dialog,"unsavedProjectCancel").PerformClick()))Call(form,"OpenLibraryAsTarget");
                Check(Field(form,"_libraryLease")==null,"Cancelled target opening disposes its uncommitted cold source candidate");cold.CheckPreserved("cancelled cold-source target open");
                using(FlowModal("unsavedProjectSave",dialog=>SimpleControl<Button>(dialog,"unsavedProjectSave").PerformClick()))Call(form,"OpenLibraryAsTarget");
                await SimpleControlsReady(form);
                Check(ReferenceEquals(Field(MainForm.Instance,"_fighterForm"),originalForm)&&Field(form,"_libraryLease") is LibrarySourceLease committed&&committed.Reference.ClipId==mario.Clip.Id&&committed.Model.Name=="FitMario00","Save-confirmed same-window target opening commits its exact cold source preview without restarting");
                using(var saved=FighterProject.Open(project))using(var root=NodeFactory.FromFile(null,saved.Animations.Single().WorkingPath))
                    Check(Walk(root).OfType<CHR0Node>().Any(c=>LifecycleContent(c)==edited),"Save-confirmed target replacement first preserves the previous exact authored body animation");
                Call(form,"SetSimpleWorkspaceLibraryVisible",true);await CaptureSimpleWorkspace(form,Path.Combine(output,"same-window-cold-source-committed.png"));
            }
        }
        finally{if(form!=null&&!form.IsDisposed)SimpleDisposeForm(form);}
    }
}
