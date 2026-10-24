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
    static Task WorkspaceBatchReview(string[] args,string output) => LibraryBatch(args,output);
    static object[] ReviewRows(ModelForm form) => ((System.Collections.IEnumerable)Field(form,"_batchReviewRows")).Cast<object>().ToArray();
    static bool ReviewApproved(object row) => (bool)Field(row,"Approved");
    static async Task ReviewBodyEdit(ModelForm form,float amount)
    {
        var editor=form.FighterEditor;editor.SetFrame(1);await SimpleControlsReady(form);
        var setup=await Await<ControlReply>(Call(form,"LoadControls"));editor.SetFrame(3);
        editor.SelectedBone=((MDL0Node)editor.TargetModel)._linker.BoneCache.Single(b=>b.Name==setup.BoneNames[setup.Roles.Single(r=>r.Role=="Hips").Bone.Value]);
        var field=editor.CHR0Editor._transBoxes[3];field.Text=(editor.SelectedBone.FrameState._rotate._x+amount).ToString();Call(field,"Apply");await SimpleControlsReady(form);
    }
    static async Task ReviewBatchCases(ModelForm form,FighterAsset[] assets,string output)
    {
        var editor=form.FighterEditor;
        await Wait(()=>((Panel)Field(form,"_batchReviewPane"))?.Visible==true);await SimpleControlsReady(form);
        var target=editor.TargetModel;var viewport=editor.ModelPanel;
        Check(ReviewRows(form).Length==2,"Actual reviewed batch import opens a session queue containing both exact target revisions");
        var rows=ReviewRows(form);var clips=rows.Select(r=>(CHR0Node)Field(r,"Clip")).ToArray();
        Check(rows.Select(r=>(string)Field(r,"Key")).Distinct().Count()==2&&!rows.Any(ReviewApproved),"Review identities are distinct and batch loss review never grants visual export approval");
        var library=(AnimationLibrary)Field(form,"_library");
        foreach(var asset in assets)
        {
            using(var source=library.Open(asset.Retarget.Library.ClipId))
            using(var fit=asset.Retarget.Timing.Resample(source.Animation))
            {
                string fitPath=Path.Combine(output,"reference-fit-"+asset.Id+".chr0"),resultPath=Path.Combine(output,"reference-result-"+asset.Id+".chr0");fit.ExportChecked(fitPath);
                using(var decodedFit=NodeFactory.FromFile(null,fitPath) as CHR0Node)
                using(var expected=BrawlRetargeter.Bake(source.Model,(MDL0Node)target,decodedFit,groundMode:form.FighterProject.GroundMode))expected.Animation.ExportChecked(resultPath);
                using(var expected=NodeFactory.FromFile(null,resultPath) as CHR0Node)
                {
                    string difference=NativeDifference(expected,(CHR0Node)Call(form,"FighterRoot",asset));File.WriteAllText(resultPath+".comparison.txt",difference??"Exact original Next native match");
                    Check(difference==null,"Batch review retains every original Next channel and frozen timing result: "+difference);
                }
            }
        }
        string baselineA=LifecycleContent(clips[0]),baselineB=LifecycleContent(clips[1]);
        Call(form,"MarkBatchReview");Check(ReviewApproved(rows[0])&&!ReviewApproved(rows[1]),"Mark reviewed approves only the exact selected result");
        int hashes=FighterProject.DiagnosticFileHashes,opens=AnimationLibrary.DiagnosticOpens,bakes=BrawlRetargeter.DiagnosticBakes;
        editor.SetFrame(0);editor.SetFrame(2);editor.SetFrame(3);Call(form,"UpdateFighterStatus");editor.PlayAnim();await Task.Delay(300);editor.StopAnim();editor.SetFrame(3);
        Check(ReviewApproved(rows[0])&&FighterProject.DiagnosticFileHashes==hashes&&AnimationLibrary.DiagnosticOpens==opens&&BrawlRetargeter.DiagnosticBakes==bakes,"Scrubbing/navigation retain review approval without hashing, opening or baking");
        await ReviewBodyEdit(form,2);string editedA=LifecycleContent(clips[0]);
        Check(!ReviewApproved(rows[0])&&editedA!=baselineA,"A real native body edit invalidates its previously approved clip");
        Call(form,"MarkBatchReview");Call(form,"MoveBatchReview",1);await SimpleControlsReady(form);
        Check(ReferenceEquals(editor.SelectedCHR0,clips[1])&&ReferenceEquals(viewport,editor.ModelPanel)&&ReferenceEquals(target,editor.TargetModel),"Next selects the exact native result while retaining the complete editor and target");
        await ReviewBodyEdit(form,4);string editedB=LifecycleContent(clips[1]);Call(form,"MarkBatchReview");
        Check(rows.All(ReviewApproved),"Independent native edits can each be reviewed against the same authentic target");
        editor.Undo();await SimpleControlsReady(form);
        Check(LifecycleContent(clips[1])==baselineB&&!ReviewApproved(rows[1])&&ReviewApproved(rows[0]),"Native Undo restores clip B exactly and invalidates only B approval");
        editor.Undo();await SimpleControlsReady(form);
        Check(ReferenceEquals(editor.SelectedCHR0,clips[0])&&LifecycleContent(clips[0])==baselineA&&!rows.Any(ReviewApproved),"Cross-clip native Undo selects clip A and its review row without another Undo stack");
        Check(ReferenceEquals(SimpleControl<ListBox>(form,"reviewResults").SelectedItem,rows[0]),"Review current row follows the native Undo-selected animation");
        editor.Redo();await SimpleControlsReady(form);editor.Redo();await SimpleControlsReady(form);
        Check(LifecycleContent(clips[0])==editedA&&LifecycleContent(clips[1])==editedB&&ReferenceEquals(editor.SelectedCHR0,clips[1]),"Native cross-clip Redo restores both exact authored curves and result selection");
        clips[1].Name=clips[0].Name;Call(form,"SyncBatchReview");
        Check(rows.Select(r=>(string)Field(r,"Key")).Distinct().Count()==2,"Duplicate native clip names retain separate exact review identities");
        Call(form,"OpenLinkedComparison");Check(Field(form,"_comparisonLease")!=null&&!((Panel)Field(form,"_batchReviewPane")).Visible,"Compare source hides the review drawer and resolves the selected result's recorded source");
        Call(form,"OpenBatchReview");Check(ReviewRows(form).Length==2&&Field(form,"_comparisonLease")!=null,"Recovering Review results retains rows and the prepared comparison lease");
        string contentA=LifecycleContent(clips[0]),contentB=LifecycleContent(clips[1]);int undo=editor._undoSaves.Count;
        foreach(int text in new[]{100,200})foreach(int width in new[]{1050,1700})
        {
            form.ClientSize=new Size(width,950);Call(form,"SetWorkspaceTextSize",text);Call(form,"OpenBatchReview");
            var list=SimpleControl<ListBox>(form,"reviewResults");
            Check(list.Visible&&list.Width>160&&!((Control)Field(form,"_simpleWorkspaceTask")).Visible,"Review drawer owns visible task space at "+width+"/"+text);
            var pane=(Panel)Field(form,"_batchReviewPane");var body=(Panel)Field(form,"_simpleWorkspaceBody");
            Check(ReferenceEquals(body.GetChildAtPoint(new Point(pane.Left+10,pane.Top+45)),pane),"Review drawer is the actual frontmost native control at "+width+"/"+text);
            Check(WorkspaceKey(form,Keys.F6)&&ReferenceEquals(viewport,editor.ModelPanel),"F6 handles the review region without replacing the native viewport");
            await CaptureSimpleWorkspace(form,Path.Combine(output,"batch-review-"+width+"-"+text+".png"));
        }
        Call(form,"RestoreWorkspaceFocus",SimpleControl<Button>(form,"reviewMark"));
        Check(SimpleControl<Button>(form,"reviewMark").Focused&&InClient(SimpleControl<Button>(form,"reviewMark"),(Panel)Field(form,"_batchReviewPane")),"Mark reviewed remains focusable and scrolls fully into view at 200% text");
        await CaptureSimpleWorkspace(form,Path.Combine(output,"batch-review-focused-200.png"));
        Check(LifecycleContent(clips[0])==contentA&&LifecycleContent(clips[1])==contentB&&editor._undoSaves.Count==undo,"All review layout/focus cycles preserve exact authored clips and native Undo");
        Call(form,"OpenBatchReview");Check(WorkspaceKey(form,Keys.Escape)&&!((Panel)Field(form,"_batchReviewPane")).Visible,"Escape inside review closes only the drawer");
        Call(form,"OpenBatchReview");Call(form,"ToggleSimpleWorkspaceFocus");Check(!((Panel)Field(form,"_batchReviewPane")).Visible&&editor.ModelPanel.Focused,"Focus editor closes comparison/review and restores the original viewport");
        Call(form,"OpenBatchReview");Call(form,"RemoveBatchReview");
        Check(ReviewRows(form).Length==1&&LifecycleContent(clips[1])==contentB&&form.FighterProject.Animations.Contains(assets[1]),"Remove from list never removes the native project animation");
        Call(form,"AddSelectedBatchReviewClip");Call(form,"MarkBatchReview");
        var current=ReviewRows(form).Single(r=>ReferenceEquals(Field(r,"Clip"),editor.SelectedCHR0));
        assets[1].Retarget.Timing.Frames++;
        bool stale=false;try{Call(form,"ValidateBatchReviewRows");}catch(System.Reflection.TargetInvocationException e){stale=e.InnerException is InvalidOperationException;}
        Check(stale&&!ReviewApproved(current),"Changed recorded recipe invalidates exact review approval before export");assets[1].Retarget.Timing.Frames--;
        Call(form,"MarkBatchReview");
        string saved=Path.Combine(output,"review-session.fpstudio");form.SaveFighterProject(saved);
        Check(ReviewApproved(current),"Saving unchanged exact native content retains its session approval while rebuilding watched tracks");
        using(var fresh=FighterProject.Open(saved))using(var reopened=new ModelForm())
        {
            reopened.InitializeFighterProject(fresh);reopened.Show();await Task.Delay(150);await SimpleControlsReady(reopened);Call(reopened,"OpenBatchReview");
            Check(ReviewRows(reopened).Length==0,"Fresh project reopen retains native animations but no session approval or review list");
            Call(reopened,"AddSelectedBatchReviewClip");Check(ReviewRows(reopened).Length==1&&!ReviewRows(reopened).Any(ReviewApproved),"Reopened project-owned CHR0 joins review without a donor or inherited approval");
            Check(LifecycleContent(reopened.FighterEditor.SelectedCHR0)==contentB,"Session-only review adds no project schema or curve changes at reopen");
            await CaptureSimpleWorkspace(reopened,Path.Combine(output,"batch-review-reopened.png"));
            SimpleDisposeForm(reopened);
        }
    }

    static async Task ReviewBatchFailureCases(ModelForm form,FighterAsset[] assets,string output,string ownedMotion)
    {
        int before=form.FighterProject.Animations.Count;
        foreach(string mode in new[]{"mixed","cancel","stale-source"})
        {
            Exception failure=null;bool finished=false;
            form.LibraryBatchReady=async window=>
            {
                byte[] original=null;
                try
                {
                    var grid=SimpleControl<DataGridView>(window,"batchClips");
                    foreach(DataGridViewRow row in grid.Rows)
                    {row.Cells["Use"].Value=true;row.Cells["Start"].Value=2;row.Cells["End"].Value=5;row.Cells["Frames"].Value=7;}
                    if(mode=="mixed")grid.Rows[0].Cells["Start"].Value=-1;
                    var prepare=SimpleControl<Button>(window,"prepareBatch");
                    if(mode=="cancel")
                    {
                        using(OnRetargetReady(form,dialog=>dialog.Controls.OfType<Button>().Single().PerformClick()))prepare.PerformClick();
                        Check(!SimpleControl<Button>(window,"applyBatch").Enabled&&form.FighterProject.Animations.Count==before,"Actual batch cancellation adds no native revision or approval");
                    }
                    else
                    {
                        prepare.PerformClick();
                        if(mode=="mixed")Check(Convert.ToString(grid.Rows[0].Cells["Result"].Value).StartsWith("Failed:")&&Convert.ToString(grid.Rows[1].Cells["Result"].Value).StartsWith("Review:"),"A failed row remains excluded while another source prepares successfully");
                        else {original=File.ReadAllBytes(ownedMotion);var changed=(byte[])original.Clone();changed[changed.Length-1]^=1;File.WriteAllBytes(ownedMotion,changed);}
                        SimpleControl<CheckBox>(window,"reviewBatch").Checked=true;SimpleControl<Button>(window,"applyBatch").PerformClick();
                        Check(form.FighterProject.Animations.Count==before+(mode=="mixed"?1:0),mode=="mixed"?"Explicit loss review imports only the successful batch row":"Changed original source bytes reject batch Apply without partial project changes");
                        if(mode=="mixed")await Wait(()=>ReviewRows(form).Length==3);
                        if(mode=="stale-source")await CaptureWindow(window,Path.Combine(output,"batch-failure-review-200.png"));
                    }
                }
                catch(Exception error){failure=error;}
                finally{if(original!=null)File.WriteAllBytes(ownedMotion,original);window.Close();finished=true;}
            };
            Call(form,"ShowLibraryBatch");await Wait(()=>finished);if(failure!=null)throw failure;
            if(mode=="mixed")
            {
                Call(form,"UndoFighterImport");await SimpleControlsReady(form);
                Check(form.FighterProject.Animations.Count==before&&ReviewRows(form).Length==2,"One import Undo removes only the successful added batch and its session row");
            }
        }
        string native=Path.Combine(output,"native-without-donor.chr0");File.Copy(assets[0].WorkingPath,native);
        Call(form,"ImportFighterAnimations",new[]{new[]{native}});await SimpleControlsReady(form);Call(form,"OpenBatchReview");Call(form,"AddSelectedBatchReviewClip");Call(form,"MarkBatchReview");
        Check(ReviewRows(form).Any(r=>ReviewApproved(r)&&ReferenceEquals(Field(r,"Clip"),form.FighterEditor.SelectedCHR0)),"Imported native animation is reviewable without donor association or retargeting");
        Call(form,"OpenLinkedComparison");Check(Field(form,"_comparisonLease")==null&&SimpleControl<Label>(form,"workspaceComparisonStatus").Text.Contains("unavailable"),"Donor-free native review reports unavailable comparison while keeping target editing");
        Call(form,"CloseLinkedComparison");Call(form,"UndoFighterImport");await SimpleControlsReady(form);
        Check(form.FighterProject.Animations.Count==before&&ReviewRows(form).Length==2,"Undoing a donor-free import prunes only its review row");
        form.LibraryBatchReady=null;
    }
}
