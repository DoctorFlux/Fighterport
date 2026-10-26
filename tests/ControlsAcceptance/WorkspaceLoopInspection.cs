using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static Task WorkspaceLoopInspection(string[] args,string output) { WorkspaceLoopMath();return LibraryBatch(args,output); }
    static int LoopAnalyses(ModelForm form) => (int)form.GetType().GetProperty("LoopInspectionAnalyses",Private).GetValue(form);
    static NativeLoopReport LoopReport(ModelForm form) => (NativeLoopReport)Field(form,"_loopInspectionReport");
    static void LoopClear(ModelForm form,string reason)
    {
        Check(LoopReport(form)==null&&SimpleControl<DataGridView>(form,"loopInspectionBones").Rows.Count==0&&SimpleControl<TextBox>(form,"loopInspectionDetails").Text.Length==0&&!SimpleControl<Button>(form,"loopInspectionFirst").Enabled,"Inspection clears stale measurements and jumps until explicit Refresh: "+reason);
    }
    static async Task LoopInspectionCases(ModelForm form,FighterAsset[] assets,string output)
    {
        var editor=form.FighterEditor;await Wait(()=>((Panel)Field(form,"_batchReviewPane"))?.Visible==true);await SimpleControlsReady(form);
        var rows=ReviewRows(form);var clips=rows.Select(r=>(CHR0Node)Field(r,"Clip")).ToArray();
        string baselineA=SetNativeContent(clips[0]),baselineB=SetNativeContent(clips[1]);
        Call(form,"SelectBatchReview",rows[0]);await SimpleControlsReady(form);await ReviewBodyEdit(form,2);
        Call(form,"SelectBatchReview",rows[1]);await SimpleControlsReady(form);await ReviewBodyEdit(form,4);
        string editedA=SetNativeContent(clips[0]),editedB=SetNativeContent(clips[1]);await ApproveSet(form);
        Call(form,"SelectBatchReview",rows[0]);await SimpleControlsReady(form);editor.SetFrame(0);
        var selected=editor.SelectedCHR0;var bone=editor.SelectedBone;var target=(MDL0Node)editor.TargetModel;var viewport=editor.ModelPanel;
        int undo=editor._undoSaves.Count;bool dirty=(bool)Field(form,"_fighterDirty"),targetDirty=target.IsDirty;
        var children=selected.Children.ToArray();var pose=target._linker.BoneCache.Select(b=>b.FrameState).ToArray();
        Call(form,"SetSimpleWorkspaceStage",2);var action=SimpleControl<Button>(form,"workspaceLoopInspectionAction");
        if(!action.Visible)SimpleControl<Button>(form,"workspaceMoreActions").PerformClick();
        Check(action.Visible&&action.Enabled,"Preview and fix exposes Inspect loop / seam for the exact project-owned CHR0");
        Call(form,"RestoreWorkspaceFocus",action);action.PerformClick();var report=LoopReport(form);
        Check(report!=null&&report.Frames==selected.FrameCount&&!report.Loop&&report.Bones.Length==target._linker.BoneCache.Length,"Inspector reads already-owned original Next result and native target without a donor requirement");
        Check(ReferenceEquals(editor.SelectedCHR0,selected)&&ReferenceEquals(editor.SelectedBone,bone)&&ReferenceEquals(editor.ModelPanel,viewport)&&ReferenceEquals(editor.TargetModel,target)&&editor.CurrentFrame==0&&editor._undoSaves.Count==undo,"Opening inspection preserves target/clip/bone/frame zero/native Undo and full viewport");
        Check(SetNativeContent(clips[0])==editedA&&SetNativeContent(clips[1])==editedB&&selected.Children.SequenceEqual(children)&&rows.All(ReviewApproved)&&(bool)Field(form,"_fighterDirty")==dirty&&target.IsDirty==targetDirty,"Opening and sampling preserve exact authored content/children, review approvals and dirty state");
        Check(target._linker.BoneCache.Select((b,i)=>Enumerable.Range(0,9).All(c=>b.FrameState[c]==pose[i][c])).All(v=>v),"Inspection never applies diagnostic samples to the live bind-pose viewport");
        string summary=SimpleControl<Label>(form,"loopInspectionSummary").Text;
        Check(summary.Contains(assets[0].Id)&&summary.Contains(form.FighterProject.Model.Id)&&summary.Contains("stored loop Off")&&summary.Contains("preview loop"),"Inspector summary exposes exact resource identities, native frame count and separate stored/preview loop flags");
        Check(!((Control)Field(form,"_simpleWorkspaceTask")).Visible&&!((Panel)Field(form,"_batchReviewPane")).Visible&&!((Panel)Field(form,"_libraryPane")).Visible&&ReviewRows(form).Length==2,"Inspection hides competing panes while preserving review and library state");
        var pairs=SimpleControl<ComboBox>(form,"loopInspectionPairs");
        Call(form,"RestoreWorkspaceFocus",pairs);
        Check(WorkspaceKey(form,Keys.Tab)&&SimpleControl<DataGridView>(form,"loopInspectionBones").Focused,"Tab moves from the boundary selector to the read-only table");
        Check(WorkspaceKey(form,Keys.Shift|Keys.Tab)&&pairs.Focused,"Shift+Tab returns from the table to the boundary selector");
        Check(WorkspaceKey(form,Keys.Shift|Keys.F6)&&!((Panel)Field(form,"_loopInspectionPane")).ContainsFocus,"Shift+F6 leaves inspection through the existing workspace regions");
        Call(form,"CloseLoopInspection");Check(action.Focused,"Close restores the invoking workspace inspection action");action.PerformClick();

        // Independent endpoint oracle uses normal native evaluation, not the inspector's sampler.
        editor.SetFrame(1);var first=target._linker.BoneCache.Select(b=>b.FrameState).ToArray();editor.SetFrame(selected.FrameCount);
        foreach(var row in report.Bones.Where(r=>r.Endpoints.Valid))
        {
            int i=Array.FindIndex(target._linker.BoneCache,b=>b.Name==row.Name);FrameState a=first[i],b=target._linker.BoneCache[i].FrameState;
            double x=b._translate._x-a._translate._x,y=b._translate._y-a._translate._y,z=b._translate._z-a._translate._z;
            double scale=new[]{Math.Abs(b._scale._x-a._scale._x),Math.Abs(b._scale._y-a._scale._y),Math.Abs(b._scale._z-a._scale._z)}.Max();
            Check(LoopNear(row.Endpoints.TranslationDistance,Math.Sqrt(x*x+y*y+z*z),LoopPositionTolerance)&&LoopNear(row.Endpoints.ScaleMaximum,scale,LoopScaleTolerance),"Independent native viewport endpoint translation/scale match: "+row.Name);
        }
        editor.SetFrame(0);SimpleControl<Button>(form,"loopInspectionRefresh").PerformClick();
        Check(editor.CurrentFrame==0&&!SimpleControl<Button>(form,"loopInspectionClosing").Enabled&&SimpleControl<ComboBox>(form,"loopInspectionPairs").Items.Count==1,"Refresh pauses without seeking and stored non-looping clips have no closing sample");
        SimpleControl<Button>(form,"loopInspectionFirst").PerformClick();Check(editor.CurrentFrame==1,"First explicitly seeks native display frame 1");
        SimpleControl<Button>(form,"loopInspectionLast").PerformClick();Check(editor.CurrentFrame==selected.FrameCount,"Last explicitly seeks native display frame N");
        int analyses=LoopAnalyses(form);int hashes=FighterProject.DiagnosticFileHashes,opens=AnimationLibrary.DiagnosticOpens,bakes=BrawlRetargeter.DiagnosticBakes;
        bool preview=editor.PlaybackPanel.chkLoop.Checked;editor.PlaybackPanel.chkLoop.Checked=!preview;Call(form,"UpdateSimpleWorkspace");
        Check(LoopAnalyses(form)==analyses&&LoopReport(form)!=null&&!selected.Loop&&!SimpleControl<Button>(form,"loopInspectionClosing").Enabled,"Preview-loop changes neither stored loop nor closing availability and never recompute diagnostics");
        editor.PlaybackPanel.chkLoop.Checked=true;editor.SetFrame(2);editor.PlayAnim();await Task.Delay(300);editor.StopAnim();editor.SetFrame(3);editor.PlaybackPanel.chkLoop.Checked=preview;
        Check(LoopAnalyses(form)==analyses&&FighterProject.DiagnosticFileHashes==hashes&&AnimationLibrary.DiagnosticOpens==opens&&BrawlRetargeter.DiagnosticBakes==bakes,"Scrubbing and normal playback perform no inspection recomputation, file hashing, source loading or baking");
        Check(SetNativeContent(clips[0])==editedA&&SetNativeContent(clips[1])==editedB&&editor._undoSaves.Count==undo&&rows.All(ReviewApproved),"Frame jumps, preview-loop changes and playback preserve exact authored keys, native Undo and approvals");
        var grid=SimpleControl<DataGridView>(form,"loopInspectionBones");var nativeBone=editor.SelectedBone;int nativeFrame=editor.CurrentFrame;
        grid.CurrentCell=grid.Rows[grid.Rows.Count-1].Cells[0];
        Check(grid.ReadOnly&&ReferenceEquals(editor.SelectedBone,nativeBone)&&editor.CurrentFrame==nativeFrame&&SimpleControl<TextBox>(form,"loopInspectionDetails").Text.Contains(((NativeLoopBone)grid.CurrentRow.Tag).Identity),"Read-only row navigation exposes the current exact bone details without selecting a native bone or seeking");

        editor.Undo();await SimpleControlsReady(form);Call(form,"UpdateSimpleWorkspace");LoopClear(form,"native body Undo B");
        Check(SetNativeContent(clips[1])==baselineB&&ReferenceEquals(editor.SelectedCHR0,clips[1]),"Native body Undo restores exact clip B and selection while inspection only invalidates");
        int beforeRefresh=LoopAnalyses(form);Call(form,"RefreshLoopInspection");Check(LoopAnalyses(form)==beforeRefresh+1&&LoopReport(form)!=null,"Only explicit Refresh recomputes after native Undo");
        editor.Undo();await SimpleControlsReady(form);Call(form,"UpdateSimpleWorkspace");LoopClear(form,"cross-clip Undo A");
        Check(SetNativeContent(clips[0])==baselineA&&ReferenceEquals(editor.SelectedCHR0,clips[0])&&!rows.Any(ReviewApproved),"Cross-clip Undo and affected review invalidation retain their existing native behavior");
        editor.Redo();await SimpleControlsReady(form);editor.Redo();await SimpleControlsReady(form);Call(form,"UpdateSimpleWorkspace");LoopClear(form,"cross-clip Redo");
        Check(SetNativeContent(clips[0])==editedA&&SetNativeContent(clips[1])==editedB,"Cross-clip native Redo survives all inspection navigation exactly");
        Call(form,"RefreshLoopInspection");await ReviewBodyEdit(form,1);LoopClear(form,"new authored body edit");editor.Undo();await SimpleControlsReady(form);
        Check(SetNativeContent(clips[1])==editedB,"Body edit/Undo during inspection returns exact keys and tangents");
        await ApproveSet(form);Call(form,"RefreshLoopInspection");var current=editor.SelectedCHR0;int history=editor._undoSaves.Count;
        current.Loop=true;LoopClear(form,"stored loop metadata");Call(form,"RefreshLoopInspection");
        Check(LoopReport(form).Loop&&SimpleControl<Button>(form,"loopInspectionClosing").Enabled&&SimpleControl<ComboBox>(form,"loopInspectionPairs").Items.Count==2,"Stored loop metadata alone enables an explicitly evaluated closing frame");
        SimpleControl<Button>(form,"loopInspectionClosing").PerformClick();Check(editor.CurrentFrame==current.FrameCount+1,"Closing frame explicitly seeks N+1 through the existing native frame path");
        int closingFrame=editor.CurrentFrame;SimpleControl<ComboBox>(form,"loopInspectionPairs").SelectedIndex=1;
        Check(editor.CurrentFrame==closingFrame&&SimpleControl<TextBox>(form,"loopInspectionDetails").Text.Contains("Closing"),"Switching boundary pairs uses cached read-only values without seeking");
        current.Loop=false;Call(form,"RefreshLoopInspection");editor.SetFrame(3);
        string targetName=target.Name;target.Name=targetName+" inspection invalidation";LoopClear(form,"target resource rename");target.Name=targetName;await SimpleControlsReady(form);Call(form,"RefreshLoopInspection");
        Check(LoopReport(form)!=null&&editor._undoSaves.Count==history,"Target dependency change requires Refresh without touching native Undo");
        await ApproveSet(form);Call(form,"RefreshLoopInspection");
        form.GetType().GetField("_workspaceBusy",Private).SetValue(form,true);
        try
        {
            bool rejected=false;try{Call(form,"RefreshLoopInspection");}catch(System.Reflection.TargetInvocationException e){rejected=e.InnerException is InvalidOperationException;}
            Check(rejected,"Inspection retains existing workspace busy guards");
        }
        finally{form.GetType().GetField("_workspaceBusy",Private).SetValue(form,false);Call(form,"UpdateSimpleWorkspace");}

        Call(form,"OpenLinkedComparison");Check(Field(form,"_comparisonLease")!=null&&!((Panel)Field(form,"_loopInspectionPane")).Visible,"Recorded source comparison hides inspection while retaining native target editing");
        var lease=Field(form,"_comparisonLease");Call(form,"OpenLoopInspection");
        Check(ReferenceEquals(lease,Field(form,"_comparisonLease"))&&!((Panel)Field(form,"_comparisonPane")).Visible&&ReviewRows(form).Length==2,"Opening inspection retains prepared comparison and review list without competing visible panes");
        Call(form,"OpenBatchReview");Check(!((Panel)Field(form,"_loopInspectionPane")).Visible&&ReviewRows(form).Length==2,"Recovering Review results retains exact rows and hides inspection");
        Call(form,"OpenLoopInspection");Call(form,"ToggleAnimationLibrary");Check(!((Panel)Field(form,"_loopInspectionPane")).Visible&&((Panel)Field(form,"_libraryPane")).Visible,"Recovering the library hides inspection without clearing catalogue state");
        Call(form,"OpenLoopInspection");editor.FrameFighterModel();
        var expected=clips.Select(SetNativeContent).ToArray();var approved=ReviewRows(form).Select(ReviewApproved).ToArray();int nativeUndo=editor._undoSaves.Count;editor.SetFrame(3);
        foreach(int text in new[]{100,175})foreach(int width in new[]{1050,1700})
        {
            form.ClientSize=new Size(width,950);Call(form,"SetWorkspaceTextSize",text);Call(form,"OpenLoopInspection");
            var pane=(Panel)Field(form,"_loopInspectionPane");var body=(Panel)Field(form,"_simpleWorkspaceBody");
            Check(pane.Visible&&ReferenceEquals(body.GetChildAtPoint(new Point(pane.Left+10,pane.Top+45)),pane),"Inspection is the frontmost usable drawer at "+width+"/"+text);
            Check(WorkspaceKey(form,Keys.F6)&&ReferenceEquals(editor.ModelPanel,viewport),"F6 routes the inspection region while keeping the complete native viewport at "+width+"/"+text);
            await CaptureSimpleWorkspace(form,Path.Combine(output,"loop-inspection-"+width+"-"+text+".png"));
        }
        Call(form,"RestoreWorkspaceFocus",SimpleControl<Button>(form,"loopInspectionRefresh"));
        Check(SimpleControl<Button>(form,"loopInspectionRefresh").Focused&&InClient(SimpleControl<Button>(form,"loopInspectionRefresh"),(Panel)Field(form,"_loopInspectionPane")),"Refresh has visible keyboard focus and scrolls into view at 175% text");
        await CaptureSimpleWorkspace(form,Path.Combine(output,"loop-inspection-focused-175.png"));
        Check(clips.Select(SetNativeContent).SequenceEqual(expected)&&ReviewRows(form).Select(ReviewApproved).SequenceEqual(approved)&&editor.CurrentFrame==3&&editor._undoSaves.Count==nativeUndo,"All text/layout/focus cycles preserve exact content, review approvals, frame and native Undo");
        Call(form,"RestoreWorkspaceFocus",viewport);bool escape=WorkspaceKey(form,Keys.Escape);
        Check(!escape&&((Panel)Field(form,"_loopInspectionPane")).Visible,"Escape outside inspection remains available to native editing gestures");
        Call(form,"RestoreWorkspaceFocus",SimpleControl<Button>(form,"loopInspectionRefresh"));Check(WorkspaceKey(form,Keys.Escape)&&!((Panel)Field(form,"_loopInspectionPane")).Visible,"Focused Escape closes only inspection");
        Call(form,"OpenLoopInspection");Call(form,"ToggleSimpleWorkspaceFocus");Check(!((Panel)Field(form,"_loopInspectionPane")).Visible&&editor.ModelPanel.Focused&&Field(form,"_comparisonLease")==null,"Focus editor closes inspection/comparison and restores the original viewport");
        var input=editor.CHR0Editor._transBoxes[3];string ordinaryText=input.Text;input.Focus();input.Text="123.456";
        string unchanged=SetNativeContent(editor.SelectedCHR0);int unchangedUndo=editor._undoSaves.Count;bool pendingRejected=false;
        try{Call(form,"OpenLoopInspection");}catch(System.Reflection.TargetInvocationException e){pendingRejected=e.InnerException is InvalidOperationException;}
        Check(pendingRejected&&input.Text=="123.456"&&SetNativeContent(editor.SelectedCHR0)==unchanged&&editor._undoSaves.Count==unchangedUndo,"Programmatic inspection rejects uncommitted native numeric text without applying it or changing Undo");
        input.Text=ordinaryText;Call(form,"RestoreWorkspaceFocus",viewport);
        await LoopInspectionNative(form,output);
    }
}
