using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task<FighterAsset> ComparisonRevision(ModelForm form, AnimationLibrary library, string clipId, NativeTimingFit timing)
    {
        Call(form,"CloseLinkedComparison");
        using(var source=library.Open(clipId))
        using(var resampled=timing?.Resample(source.Animation))
        using(var result=BrawlRetargeter.Bake(source.Model,(MDL0Node)form.FighterEditor.TargetModel,resampled??source.Animation,groundMode:form.FighterProject.GroundMode))
        {
            var asset=form.FighterProject.CreateLibraryRevision(source,result,timing);
            var root=(ResourceNode)Call(form,"FighterRoot",asset);
            form.FighterProject.Animations.Add(asset);
            form.FighterProject.SelectedAnimation=(string)typeof(ModelForm).GetMethod("FighterAnimationKey",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{asset,root});
            form.FighterProject.Frame=1;
            Call(form,"RebuildFighterScene");
            try { await SimpleControlsReady(form); }
            catch { Console.WriteLine("SETUP STATUS "+((ToolStripStatusLabel)Field(form,"_fighterStatus")).Text+" busy="+Field(form,"_controlBusy")+" queued="+Field(form,"_controlSetupQueued"));throw; }
            form.FighterEditor.SetFrame(Math.Min(3,result.Animation.FrameCount));
            return asset;
        }
    }

    static bool ComparisonPoseMatches(ModelForm form,float sample)
    {
        var source=(LibrarySourceLease)Field(form,"_comparisonLease");
        var track=source.Animation.Children.OfType<CHR0EntryNode>().First(e=>!e.UseModelRotate&&e.KeyArrays[3].KeyCount>0&&source.Model._linker.BoneCache.Any(b=>b.Name==e.Name));
        var bone=source.Model._linker.BoneCache.Single(b=>b.Name==track.Name);
        return Math.Abs(bone.FrameState._rotate._x-track.GetFrameValue(3,sample-1))<.00001f;
    }

    static async Task WorkspaceComparison(string[] args,string output)
    {
        if(!PrivateDesktop||!args.Contains("--retarget-source"))throw new InvalidOperationException("Private desktop and source model required.");
        string donor=args[Array.IndexOf(args,"--retarget-source")+1];
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",Path.Combine(output,"library"));
        string modelCopy=Path.Combine(output,"source-model.pac"),motionCopy=Path.Combine(output,"source-motion.pac");
        File.Copy(donor,modelCopy); File.Copy(args[1],motionCopy);
        using(var startup=MainForm.StartViewerApplication())
        {
            var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
            try
            {
                form.ClientSize=new Size(1700,950);await Task.Delay(150);await SimpleControlsReady(form);
                Call(form,"ImportFighterModel",args[0]);await SimpleControlsReady(form);
                Call(form,"BuildLibraryPane");var library=(AnimationLibrary)Field(form,"_library");
                var model=BrawlLibrarySource.InspectModels(modelCopy).First(m=>!m.Shadow);
                library.Register(BrawlLibrarySource.Prepare(new LibraryRegistration{ModelPath=modelCopy,ModelKey=model.ResourceKey,MotionPath=motionCopy,Character=model.Name,GameVersion="Local fixture; build unverified",ApproveAssociation=true}));
                var item=library.Browse("Wait1").Single(c=>c.Clip.Name=="Wait1"&&c.Model!=null);
                var asset=await ComparisonRevision(form,library,item.Clip.Id,null);
                var editor=form.FighterEditor;var clip=editor.SelectedCHR0;var nativeViewport=editor.ModelPanel;
                var setup=await Await<ControlReply>(Call(form,"LoadControls"));editor.SetFrame(3);
                editor.SelectedBone=((MDL0Node)editor.TargetModel)._linker.BoneCache.Single(b=>b.Name==setup.BoneNames[setup.Roles.Single(r=>r.Role=="Hips").Bone.Value]);
                string before=LifecycleContent(clip);
                var field=editor.CHR0Editor._transBoxes[3];field.Text=(editor.SelectedBone.FrameState._rotate._x+2).ToString();Call(field,"Apply");await SimpleControlsReady(form);
                string edited=LifecycleContent(clip);int undo=editor._undoSaves.Count;var bone=editor.SelectedBone;
                Call(form,"SetSimpleWorkspaceStage",2);
                editor.FrameFighterModel();
                Call(form,"OpenLinkedComparison");
                Check(Field(form,"_comparisonLease")!=null,"Recorded source loads and verifies for the selected original Next result");
                Check(ReferenceEquals(editor.ModelPanel,nativeViewport)&&ReferenceEquals(editor.SelectedCHR0,clip)&&ReferenceEquals(editor.SelectedBone,bone)&&editor.CurrentFrame==3&&editor._undoSaves.Count==undo&&LifecycleContent(clip)==edited,"Opening comparison preserves complete editable native target, exact selection/frame/content and Undo");
                Check(!((Control)Field(form,"_simpleWorkspaceTask")).Visible&&!((Panel)Field(form,"_libraryPane")).Visible,"Comparison hides competing workflow/library panes");
                var viewport=(ModelPanel)Field(form,"_comparisonViewport");
                Check(!viewport.CurrentViewport.AllowSelection,"Source renderer disallows selection and has no authoring controls");
                int textureLoads=MDL0TextureNode.DiagnosticLoads;
                int hashes=FighterProject.DiagnosticFileHashes,opens=AnimationLibrary.DiagnosticOpens,bakes=BrawlRetargeter.DiagnosticBakes,captures=BrawlRetargeter.DiagnosticCaptures;
                foreach(float frame in new[]{1f,2.25f,3.5f,(float)clip.FrameCount})
                {editor.UpdateModel(frame);Check(form.ComparisonSourceFrame==frame&&ComparisonPoseMatches(form,frame),"Identity comparison uses the native fractional sample "+frame);}
                editor.SetFrame(0);Check(form.ComparisonSourceFrame==0&&SimpleControl<Label>(form,"workspaceComparisonTime").Text.Contains("Bind"),"Rest timeline displays both bind poses");
                var boundSource=(LibrarySourceLease)Field(form,"_comparisonLease");
                Check(boundSource.Model._linker.BoneCache.Concat(((MDL0Node)editor.TargetModel)._linker.BoneCache).All(b=>b.FrameState._rotate.Equals(b.BindState._rotate)&&b.FrameState._translate.Equals(b.BindState._translate)&&b.FrameState._scale.Equals(b.BindState._scale)),"Rest comparison applies both authentic native local bind transforms");
                editor.SetFrame(3);editor.PlayAnim();await Task.Delay(300);editor.StopAnim();editor.SetFrame(3);
                Check(MDL0TextureNode.DiagnosticLoads==textureLoads&&FighterProject.DiagnosticFileHashes==hashes&&AnimationLibrary.DiagnosticOpens==opens&&BrawlRetargeter.DiagnosticBakes==bakes&&BrawlRetargeter.DiagnosticCaptures==captures,"Linked playback/scrubbing performs no texture loading, asset hashing, opening, capture preparation or bake");
                foreach(int text in new[]{100,175})foreach(int width in new[]{1050,1700})
                {
                    form.ClientSize=new Size(width,950);Call(form,"SetWorkspaceTextSize",text);Call(form,"UpdateSimpleWorkspace");Call(form,"FrameComparisonSource");
                    await CaptureSimpleWorkspace(form,Path.Combine(output,"comparison-"+width+"-"+text+".png"));
                    Check(ReferenceEquals(editor.SelectedCHR0,clip)&&editor.CurrentFrame==3&&editor._undoSaves.Count==undo&&LifecycleContent(clip)==edited,"Comparison layout preserves document at "+width+"/"+text);
                }
                var lease=Field(form,"_comparisonLease");
                Call(form,"ToggleSimpleWorkspaceWorkflow");
                Check(((Control)Field(form,"_simpleWorkspaceTask")).Visible&&ReferenceEquals(lease,Field(form,"_comparisonLease")),"Workflow recovery shows tasks while retaining the active comparison source");
                Call(form,"OpenLinkedComparison");
                Call(form,"ToggleAnimationLibrary");
                await FlowSelectSource(form,library,"Wait2");
                Check(ReferenceEquals(lease,Field(form,"_comparisonLease")),"Browsing another source retains the recorded comparison lease");
                Call(form,"OpenLinkedComparison");
                Check(((LibrarySourceLease)Field(form,"_comparisonLease")).Reference.ClipId==item.Clip.Id,"Comparison never redirects to the library browser selection");
                Check(((ToolStripLabel)Field(form,"_simpleWorkspaceSummary")).Text.Contains("Wait1")&&!((ToolStripLabel)Field(form,"_simpleWorkspaceSummary")).Text.Contains("Wait2"),"Visible comparison summary identifies the result source rather than a newly browsed clip");
                Call(form,"ToggleSimpleWorkspaceFocus");
                Check(Field(form,"_comparisonLease")==null&&!viewport.Visible&&editor.ModelPanel.ContainsFocus,"Focus editor closes comparison and focuses the original native viewport");
                editor.Undo();await SimpleControlsReady(form);Check(LifecycleContent(clip)==before,"Native exact body Undo survives comparison open/close and playback");
                editor.Redo();await SimpleControlsReady(form);Check(LifecycleContent(clip)==edited,"Native exact body Redo survives comparison");
                asset.Retarget.Library.MotionResourceSha256=new string('0',64);
                Call(form,"OpenLinkedComparison");Check(Field(form,"_comparisonLease")==null&&SimpleControl<Label>(form,"workspaceComparisonStatus").Text.Contains("differs"),"Changed recorded source hash reports unavailable without a substitute");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"comparison-unavailable.png"));
                asset.Retarget.Library.MotionResourceSha256=item.Clip.ResourceSha256;
                Call(form,"CloseLinkedComparison");File.Move(motionCopy,motionCopy+".missing");
                Call(form,"OpenLinkedComparison");Check(Field(form,"_comparisonLease")==null&&LifecycleContent(clip)==edited,"Missing original source leaves native target editing intact");
                File.Move(motionCopy+".missing",motionCopy);Call(form,"CloseLinkedComparison");
                var authenticBytes=File.ReadAllBytes(motionCopy);var altered=(byte[])authenticBytes.Clone();altered[altered.Length-1]^=1;File.WriteAllBytes(motionCopy,altered);
                Call(form,"OpenLinkedComparison");Check(Field(form,"_comparisonLease")==null&&LifecycleContent(clip)==edited,"Changed actual source bytes are rejected before native comparison publication");
                Call(form,"CloseLinkedComparison");File.WriteAllBytes(motionCopy,authenticBytes);
                bool cancelled=false;
                LibrarySourceLease cancelledLease=null;
                using(var cancel=FlowModal("libraryWorkCancel",dialog=>{cancelled=true;SimpleControl<Button>(dialog,"libraryWorkCancel").PerformClick();}))
                {
                    cancel.Interval=1;
                    cancelledLease=LibraryWork.Run(form,"Source preparation cancellation fixture",(token,progress)=>
                    {
                        token.WaitHandle.WaitOne(5000);
                        // An uncancellable native decode may finish after Cancel; publication must dispose it.
                        return library.Open(item.Clip.Id);
                    });
                }
                Check(cancelled&&cancelledLease==null&&LifecycleContent(clip)==edited,"Shared source preparation rejects late decode publication after real dialog cancellation and preserves the document");
                using(var unlocked=new FileStream(motionCopy,FileMode.Open,FileAccess.Read,FileShare.None))
                    Check(unlocked.Length>0,"Late cancelled native preparation releases its original source file locks");
                foreach(var timing in new[]{new NativeTimingFit{Start=3,End=13,Frames=7},new NativeTimingFit{Start=5,End=5,Frames=1}})
                {
                    var timedAsset=await ComparisonRevision(form,library,item.Clip.Id,timing);Call(form,"OpenLinkedComparison");
                    Check(Field(form,"_comparisonLease")!=null,"Recorded trim/timing fit resolves for "+timing.Frames+" output frames");
                    foreach(float sample in timing.Frames==1?new[]{1f}:new[]{1f,2.5f,7f})
                    {editor.UpdateModel(sample);float expected=timing.Frames==1?6f:4f+10f*((sample-1f)/6f);Check(Math.Abs(form.ComparisonSourceFrame-expected)<.00001f&&ComparisonPoseMatches(form,expected),"Independent trim/timing expectation at target "+sample+" / "+timing.Frames);}
                    editor.SetFrame(0);Check(form.ComparisonSourceFrame==0,"Trimmed and single-frame results retain genuine bind-pose comparison");
                    Call(form,"CloseLinkedComparison");timedAsset.Retarget.Timing=null;Call(form,"OpenLinkedComparison");
                    Check(Field(form,"_comparisonLease")==null&&SimpleControl<Label>(form,"workspaceComparisonStatus").Text.Contains("timing"),"Unrecorded trim duration never invents comparison alignment");
                    timedAsset.Retarget.Timing=timing;
                }
                Call(form,"CloseLinkedComparison");
                using(var source=library.Open(item.Clip.Id))
                {source.Animation.Loop=true;PreciseCHR0EntryNode.Promote(source.Animation);source.Animation.ExportChecked(Path.Combine(output,"looping.chr0"));}
                library.Register(BrawlLibrarySource.Prepare(new LibraryRegistration{ModelPath=modelCopy,ModelKey=model.ResourceKey,MotionPath=Path.Combine(output,"looping.chr0"),Character=model.Name,GameVersion="Procedural loop; build unverified",ApproveAssociation=true}));
                var loop=library.Browse("Wait1").Single(c=>c.Model!=null&&c.Clip.Loop);
                await ComparisonRevision(form,library,loop.Clip.Id,null);Call(form,"OpenLinkedComparison");
                editor.UpdateModel(editor.SelectedCHR0.FrameCount+.5f);Check(form.ComparisonSourceFrame==editor.SelectedCHR0.FrameCount+.5f,"Identity loop comparison retains the fractional wrap sample");
                Call(form,"CloseLinkedComparison");editor.SetFrame(3);
                string manifest=Path.Combine(output,"comparison.fpstudio");form.SaveFighterProject(manifest);
                Check(MainForm.Instance.TryOpenFighterProject(manifest,out string error),"Comparison result reopens through the ordinary native project lifecycle: "+error);
                form=(ModelForm)Field(MainForm.Instance,"_fighterForm");await Wait(()=>form.FighterEditor.TargetModel!=null);await SimpleControlsReady(form);Call(form,"OpenLinkedComparison");
                Check(Field(form,"_comparisonLease")!=null&&form.FighterEditor.CurrentFrame==3,"Saved provenance restores linked comparison on reopen");
            }
            finally { if(!form.IsDisposed)SimpleDisposeForm(form); }
        }
    }
}
