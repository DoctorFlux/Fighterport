using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        // Navigation/preferences only. All authored data and Undo stay native.
        private sealed class BatchReviewRow
        {
            internal string Key, AssetId, NativeId, Content, TargetContent, SourceLabel;
            internal BatchReviewRecipe Recipe;
            internal CHR0Node Clip;
            internal MDL0Node Target;
            internal bool Approved;
            public override string ToString() => (Approved ? "Reviewed · " : "Review needed · ") + Clip.Name + " · " + Clip.FrameCount + " frames · " + SourceLabel + " · " + AssetId.Substring(0, 8) + "/" + NativeId;
        }
        private readonly List<BatchReviewRow> _batchReviewRows = new List<BatchReviewRow>();
        private readonly HashSet<ResourceNode> _batchReviewWatches = new HashSet<ResourceNode>();
        private FighterProject _batchReviewProject;
        private Panel _batchReviewPane;
        private ListBox _batchReviewList;
        private TextBox _batchReviewDetails;
        private FlowLayoutPanel _batchReviewActions;
        private object _batchReviewOpener;
        private bool _batchReviewOpenRequested;
        private bool _batchReviewUpdating, _batchReviewQueued, _batchReviewCapturing, _batchReviewDirty = true;
        private CHR0Node _batchReviewSelected;
        private object _batchReviewLastTarget;
        private bool _batchReviewLastBusy;

        // Large receipts and frozen native snapshots are immutable strings.
        // Retain their exact values instead of escaping/copying them through
        // JSON on every approval dependency check. Small mutable metadata is
        // serialized afresh, so direct in-place recipe changes remain visible.
        private sealed class BatchReviewRecipe
        {
            internal string Scalars;
            internal string[] Text;
            public static bool operator ==(BatchReviewRecipe a,BatchReviewRecipe b)=>ReferenceEquals(a,b)||
                !ReferenceEquals(a,null)&&!ReferenceEquals(b,null)&&string.Equals(a.Scalars,b.Scalars,StringComparison.Ordinal)&&a.Text.SequenceEqual(b.Text,StringComparer.Ordinal);
            public static bool operator !=(BatchReviewRecipe a,BatchReviewRecipe b)=>!(a==b);
            public override bool Equals(object value)=>this==value as BatchReviewRecipe;
            public override int GetHashCode()=>Scalars.GetHashCode();
        }
        internal int BatchReviewRecipeCaptures { get; private set; }
        internal long BatchReviewRecipeScalarCharacters { get; private set; }
        internal long BatchReviewRecipeRetainedCharacters { get; private set; }

        private FighterAsset ReviewAsset(BatchReviewRow row) => RegisteredFighterAssets().FirstOrDefault(a => a.Id == row.AssetId);
        private BatchReviewRecipe ReviewRecipe(FighterAsset asset)=>ReviewRecipeCore(asset,asset.TransitionFit);
        private BatchReviewRecipe ReviewClipRecipe(FighterAsset asset,CHR0Node clip)=>ReviewRecipeCore(asset,TransitionRecipeFor(asset,clip));
        private BatchReviewRecipe ReviewRecipeCore(FighterAsset asset,NativeTransitionFit currentFit)
        {
            var json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };
            var text=new List<string>();long retained=0;
            int Text(string value)
            {
                retained+=value?.Length??0;
                if(retained>32L*1024*1024)throw new InvalidOperationException("The review recipe exceeds the 32 MiB text budget.");
                text.Add(value);return text.Count-1;
            }
            object Retarget(RetargetProvenance recipe)
            {
                if(recipe==null)return null;
                var library=recipe.Library?.Copy();
                if(library!=null)library.LastExportSha256=library.LastExportFormat=null;
                return new { recipe.Timing,recipe.ReviewDraft,EngineReceipt=Text(recipe.EngineReceipt),Library=library,
                    recipe.Algorithm,recipe.MotionControlPolicy,recipe.SourceModelPath,recipe.SourceModelSha256,recipe.SourceRig,recipe.SourceAnimationId,recipe.SourceClip,
                    recipe.FromProfile,recipe.ToProfile,recipe.LegScale,recipe.ArmScale,recipe.MaximumLimbResidual,recipe.ClampedLimbSamples,
                    recipe.FloorContactsEnabled,recipe.SuspendedSupportEnabled,recipe.GroundMode,recipe.PlantedFootSamples,recipe.PlantedHandSamples,recipe.PlantedBodySamples,
                    recipe.LockedContactSamples,recipe.MaximumContactResidual,recipe.MaximumPelvisCorrection,recipe.IgnoredSourceBones,recipe.Mapping };
            }
            object Transition(NativeTransitionFit fit)
            {
                if(fit==null)return null;
                return new { fit.Algorithm,fit.ModelSha256,Options=Text(fit.Options),Receipt=Text(fit.Receipt),fit.InitialResultSha256,
                    Inputs=fit.Inputs?.Select(input=>input==null?null:new { input.Role,input.AssetId,input.Entry,input.Sha256,Snapshot=Text(input.Snapshot),input.Name,input.Samples }).ToArray() };
            }
            string scalars=json.Serialize(new { asset.RigIdentity,Recipe=Retarget(asset.Retarget),TransitionFit=Transition(currentFit) });
            if(retained+scalars.Length>32L*1024*1024)throw new InvalidOperationException("The review recipe exceeds the 32 MiB text budget.");
            BatchReviewRecipeCaptures++;BatchReviewRecipeScalarCharacters+=scalars.Length;BatchReviewRecipeRetainedCharacters+=retained;
            return new BatchReviewRecipe { Scalars=scalars,Text=text.ToArray() };
        }
        private bool ReviewRowPresent(BatchReviewRow row) => ReviewAsset(row) != null &&
            _fighterRoots.TryGetValue(row.AssetId, out var root) && ReferenceEquals(root, row.Clip.RootNode) &&
            DiscoverFighterAnimations(root).Any(c => ReferenceEquals(c, row.Clip) && WorkspaceNativeIdentity(c) == row.NativeId);

        private void AddBatchReviewClip(FighterAsset asset, CHR0Node clip)
        {
            SyncBatchReview();
            if (clip == null || asset == null || !OwnsFighterRoot(clip)) throw new InvalidOperationException("Select a project-owned CHR0 animation.");
            string key = FighterAnimationKey(asset, clip);
            if (_batchReviewRows.All(r => r.Key != key))
            {
                if (_batchReviewRows.Count >= 64) throw new InvalidOperationException("Review at most 64 animations in one set.");
                _batchReviewRows.Add(new BatchReviewRow { Key = key, AssetId = asset.Id, NativeId = WorkspaceNativeIdentity(clip), SourceLabel = asset.Retarget?.SourceClip?.Split('|')[0] ?? "Native", Clip = clip });
            }
            WatchBatchReviewNode(clip);
            if (modelEditControl1.TargetModel is ResourceNode target) WatchBatchReviewNode(target);
            _batchReviewDirty = true; SyncBatchReview();
        }
        private void AddSelectedBatchReviewClip() => AddBatchReviewClip(SelectedFighterAsset(), modelEditControl1.SelectedCHR0);

        private void WatchBatchReviewNode(ResourceNode node)
        {
            if (!_batchReviewWatches.Add(node)) return;
            node.PropertyChanged += BatchReviewResourceChanged; node.Renamed += BatchReviewResourceChanged;
            node.Replaced += BatchReviewResourceChanged; node.Restored += BatchReviewResourceChanged;
            node.Disposing += BatchReviewResourceChanged;
            node.ChildAdded += BatchReviewChildChanged; node.ChildRemoved += BatchReviewChildChanged; node.ChildInserted += BatchReviewChildInserted;
            foreach (var child in node.Children) WatchBatchReviewNode(child);
        }
        private void BatchReviewChildInserted(int index, ResourceNode parent, ResourceNode child) => BatchReviewChildChanged(parent, child);
        private void BatchReviewChildChanged(ResourceNode parent, ResourceNode child)
        {
            BatchReviewResourceChanged(parent);
            // New tracks/keys must also invalidate subsequent approval.
            if (ReferenceEquals(child.Parent, parent)) WatchBatchReviewNode(child);
        }
        private static bool ReviewContains(ResourceNode ancestor, ResourceNode node)
        { for (var parent = node.Parent; parent != null; parent = parent.Parent) if (ReferenceEquals(parent, ancestor)) return true; return false; }
        private void BatchReviewAuthored(CHR0Node clip)
        { foreach (var row in _batchReviewRows) if (ReferenceEquals(row.Clip, clip)) row.Approved = false; QueueBatchReviewSync(); }
        private void BatchReviewResourceChanged(ResourceNode node)
        {
            if (_batchReviewCapturing) return;
            foreach (var row in _batchReviewRows)
                if (ReferenceEquals(node, row.Clip) || ReviewContains(row.Clip, node) ||
                    row.Target != null && (ReferenceEquals(node, row.Target) || ReviewContains(row.Target, node))) row.Approved = false;
            QueueBatchReviewSync();
        }
        private void QueueBatchReviewSync()
        {
            _batchReviewDirty = true;
            if (_batchReviewQueued || !IsHandleCreated || IsDisposed || Disposing) return;
            _batchReviewQueued = true;
            BeginInvoke(new Action(() => { _batchReviewQueued = false; if (!IsDisposed && !Disposing) SyncBatchReview(); }));
        }
        private void DetachBatchReviewWatches()
        {
            foreach (var node in _batchReviewWatches)
            {
                node.PropertyChanged -= BatchReviewResourceChanged; node.Renamed -= BatchReviewResourceChanged;
                node.Replaced -= BatchReviewResourceChanged; node.Restored -= BatchReviewResourceChanged; node.Disposing -= BatchReviewResourceChanged;
                node.ChildAdded -= BatchReviewChildChanged; node.ChildRemoved -= BatchReviewChildChanged; node.ChildInserted -= BatchReviewChildInserted;
            }
            _batchReviewWatches.Clear();
        }
        private void RefreshBatchReviewWatches()
        {
            DetachBatchReviewWatches();
            foreach (var row in _batchReviewRows) WatchBatchReviewNode(row.Clip);
            if (_batchReviewRows.Count > 0 && modelEditControl1.TargetModel is ResourceNode target) WatchBatchReviewNode(target);
        }
        private void ResetBatchReview()
        {
            DetachBatchReviewWatches(); _batchReviewRows.Clear();
            _batchReviewOpenRequested = false;
            _batchReviewProject = _fighterProject; _batchReviewDirty = true;
        }
        private void SyncBatchReview()
        {
            if (_batchReviewUpdating || _fighterProject == null || _fighterRestoring) return;
            if (!_batchReviewDirty && ReferenceEquals(_batchReviewProject, _fighterProject) && ReferenceEquals(_batchReviewSelected, modelEditControl1.SelectedCHR0) &&
                ReferenceEquals(_batchReviewLastTarget, modelEditControl1.TargetModel) && _batchReviewLastBusy == SimpleWorkspaceBusy) return;
            _batchReviewUpdating = true;
            try
            {
                if (!ReferenceEquals(_batchReviewProject, _fighterProject)) ResetBatchReview();
                int removed = _batchReviewRows.RemoveAll(r => !ReviewRowPresent(r));
                if (removed > 0 || !ReferenceEquals(_batchReviewLastTarget, modelEditControl1.TargetModel)) RefreshBatchReviewWatches();
                foreach (var row in _batchReviewRows)
                    if (row.Approved && (!ReferenceEquals(row.Target, modelEditControl1.TargetModel) || row.Recipe != ReviewClipRecipe(ReviewAsset(row),row.Clip))) row.Approved = false;
                _batchReviewDirty = false; _batchReviewSelected = modelEditControl1.SelectedCHR0;
                _batchReviewLastTarget = modelEditControl1.TargetModel; _batchReviewLastBusy = SimpleWorkspaceBusy;
                if (_batchReviewList == null) return;
                var selected = _batchReviewRows.FirstOrDefault(r => ReferenceEquals(r.Clip, modelEditControl1.SelectedCHR0));
                _batchReviewList.BeginUpdate();
                try { _batchReviewList.Items.Clear(); _batchReviewList.Items.AddRange(_batchReviewRows.Cast<object>().ToArray()); _batchReviewList.SelectedItem = selected; }
                finally { _batchReviewList.EndUpdate(); }
                _batchReviewDetails.Text = selected == null ? "Select a native CHR0 animation and choose Add selected. Review approval is session-only." :
                    selected.Clip.Name + " · " + selected.Clip.FrameCount + " frames · " + (selected.Clip.Loop ? "Loop" : "No loop") + Environment.NewLine +
                    "Exact result: " + selected.Key + Environment.NewLine + ReviewDescription(ReviewAsset(selected)) + Environment.NewLine +
                    (selected.Approved ? "Reviewed for this exact content, target and recipe." : "Review needed. Inspect the motion and listed losses before marking reviewed.");
                foreach (Control control in _batchReviewActions.Controls) control.Enabled = !SimpleWorkspaceBusy;
                foreach (string name in new[] { "reviewPrevious", "reviewNext", "reviewMark", "reviewRemove", "reviewCompare" })
                    _batchReviewActions.Controls[name].Enabled = !SimpleWorkspaceBusy && selected != null;
                _batchReviewActions.Controls["reviewAdd"].Enabled = !SimpleWorkspaceBusy && modelEditControl1.SelectedCHR0 != null;
            }
            finally { _batchReviewUpdating = false; }
        }
        private static string ReviewDescription(FighterAsset asset)
        {
            var recipe = asset.Retarget;
            return recipe == null ? "Native animation · no recorded comparison source" :
                "Source: " + recipe.SourceClip + " · " + recipe.Library?.ClipId + Environment.NewLine +
                (recipe.Timing == null ? "Original timing" : "Source trim " + (recipe.Timing.Start + 1) + "–" + (recipe.Timing.End + 1) + " → " + recipe.Timing.Frames + " frames; non-looping") + Environment.NewLine +
                "Omitted tracks: " + (recipe.IgnoredSourceBones == null || recipe.IgnoredSourceBones.Length == 0 ? "none" : string.Join(", ", recipe.IgnoredSourceBones)) + Environment.NewLine +
                "Contact residual: " + recipe.MaximumContactResidual.ToString("G4") + "; visual review does not establish game/contact acceptance.";
        }
        private void MarkBatchReview()
        {
            if (SimpleWorkspaceBusy) throw new InvalidOperationException("Finish the current operation before reviewing.");
            modelEditControl1.StopAnim(); FinishControlCurveEdit(); SyncBatchReview();
            var row = _batchReviewRows.SingleOrDefault(r => ReferenceEquals(r.Clip, modelEditControl1.SelectedCHR0));
            var target = modelEditControl1.TargetModel as MDL0Node;
            if (row == null || target == null || ReviewAsset(row).Retarget?.ReviewDraft == true)
                throw new InvalidOperationException("Choose an owned CHR0 on the editable target; blocked drafts cannot be approved.");
            if (ReviewAsset(row).Retarget != null && ReviewAsset(row).RigIdentity != BrawlRetargeter.SkeletonFingerprint(target))
                throw new InvalidOperationException("The result belongs to a different target rig. Review it on its original target.");
            _batchReviewCapturing = true;
            try
            {
                row.Content = ExportContent(row.Clip); row.TargetContent = NativeControlHash(target, ".mdl0");
                row.Recipe = ReviewClipRecipe(ReviewAsset(row),row.Clip); row.Target = target; row.Approved = true;
                WatchBatchReviewNode(target); WatchBatchReviewNode(row.Clip);
            }
            finally { _batchReviewCapturing = false; }
            _batchReviewDirty = true; SyncBatchReview();
        }
        private BatchReviewRow[] ValidateBatchReviewRows()
        {
            if (SimpleWorkspaceBusy) throw new InvalidOperationException("Finish the current operation before exporting.");
            modelEditControl1.StopAnim(); FinishControlCurveEdit(); _batchReviewDirty = true; SyncBatchReview();
            var rows = _batchReviewRows.ToArray();
            if (rows.Length == 0) throw new InvalidOperationException("Add animations to Review results first.");
            var target = modelEditControl1.TargetModel as MDL0Node;
            _batchReviewCapturing = true;
            try
            {
                string targetContent = target == null ? null : NativeControlHash(target, ".mdl0");
                foreach (var row in rows)
                    if (!ReferenceEquals(row.Target, target) || row.TargetContent != targetContent || row.Content != ExportContent(row.Clip) || row.Recipe != ReviewClipRecipe(ReviewAsset(row),row.Clip)) row.Approved = false;
            }
            finally { _batchReviewCapturing = false; _batchReviewDirty = true; SyncBatchReview(); }
            if (rows.Any(r => !r.Approved)) throw new InvalidOperationException("Review every included animation's current content and target before exporting.");
            return rows;
        }
        private void RestoreBatchReviewAfterSave(BatchReviewRow[] prior)
        {
            if (prior.Length == 0) return;
            _batchReviewCapturing = true;
            try
            {
                string targetContent = modelEditControl1.TargetModel is MDL0Node target ? NativeControlHash(target, ".mdl0") : null;
                foreach (var row in prior)
                    row.Approved = ReviewRowPresent(row) && ReferenceEquals(row.Target, modelEditControl1.TargetModel) &&
                        row.TargetContent == targetContent && row.Content == ExportContent(row.Clip) && row.Recipe == ReviewClipRecipe(ReviewAsset(row),row.Clip);
            }
            catch (Exception error) when (error is System.IO.IOException || error is UnauthorizedAccessException || error is InvalidOperationException)
            { foreach (var row in prior) row.Approved = false; }
            finally { _batchReviewCapturing = false; RefreshBatchReviewWatches(); _batchReviewDirty = true; }
        }
        private void SelectBatchReview(BatchReviewRow row)
        {
            if (_batchReviewUpdating || SimpleWorkspaceBusy || row == null || !ReviewRowPresent(row)) return;
            modelEditControl1.StopAnim(); FinishControlCurveEdit();
            int frame = modelEditControl1.CurrentFrame;
            CloseLinkedComparison();
            modelEditControl1.TargetAnimType = BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase.NW4RAnimType.CHR;
            foreach (ListViewItem item in modelEditControl1.leftPanel.listAnims.Items) item.Selected = ReferenceEquals(item.Tag, row.Clip);
            modelEditControl1.TargetAnimation = row.Clip;
            modelEditControl1.SetFrame(Math.Max(0, Math.Min(frame, row.Clip.FrameCount)));
            _fighterProject.SelectedAnimation = row.Key; _fighterProject.Frame = modelEditControl1.CurrentFrame;
            UpdateFighterStatus();
        }
        private void MoveBatchReview(int offset)
        {
            int index = _batchReviewRows.FindIndex(r => ReferenceEquals(r.Clip, modelEditControl1.SelectedCHR0));
            if (index >= 0 && _batchReviewRows.Count > 0) SelectBatchReview(_batchReviewRows[(index + offset + _batchReviewRows.Count) % _batchReviewRows.Count]);
        }
        private void RemoveBatchReview()
        {
            _batchReviewRows.RemoveAll(r => ReferenceEquals(r.Clip, modelEditControl1.SelectedCHR0)); RefreshBatchReviewWatches(); _batchReviewDirty = true; SyncBatchReview();
        }
        private void OpenBatchReview()
        {
            if (SimpleWorkspaceBusy) { _batchReviewOpenRequested = true; return; }
            _batchReviewOpenRequested = false;
            HideLoopInspection();
            _batchReviewOpener = _workspaceInvoker ?? CurrentWorkspaceFocus();
            if (_batchReviewPane == null)
            {
                _batchReviewPane = new Panel { Name = "workspaceBatchReview", AutoScroll = true, Visible = false, Padding = new Padding(8) };
                var layout = new TableLayoutPanel { Name = "reviewLayout", Dock = DockStyle.Top, ColumnCount = 1, RowCount = 4 };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(new Label { Text = "Review results", AutoSize = true, Dock = DockStyle.Fill }, 0, 0);
                _batchReviewList = new ListBox { Name = "reviewResults", Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true, AccessibleName = "Exact target results awaiting review" };
                _batchReviewList.SelectedIndexChanged += (s, e) => SelectBatchReview(_batchReviewList.SelectedItem as BatchReviewRow);
                _batchReviewDetails = new TextBox { Name = "reviewDetails", Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, AccessibleName = "Exact result identity, timing, losses and review status" };
                _batchReviewActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
                _batchReviewActions.Controls.Add(LibraryButton("reviewAdd", "Add selected", AddSelectedBatchReviewClip));
                _batchReviewActions.Controls.Add(LibraryButton("reviewPrevious", "Previous", () => MoveBatchReview(-1)));
                _batchReviewActions.Controls.Add(LibraryButton("reviewNext", "Next", () => MoveBatchReview(1)));
                _batchReviewActions.Controls.Add(LibraryButton("reviewMark", "Mark reviewed", MarkBatchReview));
                _batchReviewActions.Controls.Add(LibraryButton("reviewRemove", "Remove from list", RemoveBatchReview));
                _batchReviewActions.Controls.Add(LibraryButton("reviewCompare", "Compare source", OpenLinkedComparison));
                _batchReviewActions.Controls.Add(LibraryButton("reviewClose", "Close", CloseBatchReview));
                layout.Controls.Add(_batchReviewList, 0, 1); layout.Controls.Add(_batchReviewDetails, 0, 2); layout.Controls.Add(_batchReviewActions, 0, 3);
                _batchReviewPane.Controls.Add(layout); _simpleWorkspaceBody.Controls.Add(_batchReviewPane);
                _batchReviewPane.AccessibleDescription = "Session-only review list. F6 moves regions; Escape closes this drawer. Selecting a row opens its exact native animation.";
                PrepareWorkspaceTabOrder(_batchReviewPane); DarkTheme.Apply(_batchReviewPane);
            }
            if (_comparisonPane != null) _comparisonPane.Visible = false;
            SetSimpleWorkspaceLibraryVisible(false); _batchReviewPane.Visible = true;
            _batchReviewDirty = true; SyncBatchReview(); UpdateSimpleWorkspace(); FocusWorkspaceRegion(_batchReviewPane, remember: false);
        }
        private void HideBatchReview() { if (_batchReviewPane != null) _batchReviewPane.Visible = false; }
        private void CloseBatchReview() { HideBatchReview(); UpdateSimpleWorkspace(); RestoreWorkspaceFocus(_batchReviewOpener); }
        private void LayoutBatchReview(int width, int height)
        {
            if (_batchReviewPane == null) return;
            int paneWidth = Math.Min(WorkspaceAppearance.Pixels(350), Math.Max(280, width - 120));
            bool overlay = width - paneWidth < NativeWorkspaceMinimumWidth;
            _batchReviewPane.Font = WorkspaceAppearance.Font;
            _batchReviewPane.Dock = overlay ? DockStyle.None : DockStyle.Left;
            _batchReviewPane.Width = paneWidth;
            if (overlay) _batchReviewPane.Bounds = new Rectangle(0, 0, paneWidth, height);
            _batchReviewList.ItemHeight = WorkspaceAppearance.Font.Height + 6;
            var layout = (TableLayoutPanel)_batchReviewPane.Controls[0];
            layout.RowStyles[1].Height = Math.Min(12, Math.Max(3, _batchReviewRows.Count)) * _batchReviewList.ItemHeight + 10;
            layout.Height = Math.Max(height - 20, WorkspaceAppearance.Pixels(360));
            foreach (Button button in _batchReviewActions.Controls)
            { button.AutoSize = false; button.Size = new Size(TextRenderer.MeasureText(button.Text, button.Font).Width + 22, WorkspaceAppearance.Font.Height + 18); }
        }
    }
}
