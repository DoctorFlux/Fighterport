using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        // An observation of the existing native document, never another owner.
        private Panel _loopInspectionPane;
        private Label _loopInspectionSummary, _loopInspectionStatus;
        private ComboBox _loopInspectionPairs;
        private DataGridView _loopInspectionBones;
        private TextBox _loopInspectionDetails;
        private FlowLayoutPanel _loopInspectionActions;
        private NativeLoopReport _loopInspectionReport;
        private MDL0Node _loopInspectionModel;
        private CHR0Node _loopInspectionClip;
        private FighterProject _loopInspectionProject;
        private readonly HashSet<ResourceNode> _loopInspectionWatches = new HashSet<ResourceNode>();
        private object _loopInspectionOpener;
        private bool _loopInspectionUpdating, _loopInspectionPreviewLoop, _loopInspectionSummaryDirty = true;
        private bool _loopInspectionLastBusy;
        private int _loopInspectionLastFrames = -1;
        private bool _loopInspectionLastStoredLoop;
        private string _loopInspectionProjectLabel;
        private int _loopInspectionBindingInvalidEpoch = -1;
        internal int LoopInspectionAnalyses { get; private set; }

        private void BuildLoopInspection()
        {
            if (_loopInspectionPane != null) return;
            _loopInspectionPane = new Panel { Name = "workspaceLoopInspection", Visible = false, AutoScroll = true, Padding = new Padding(8),
                AccessibleName = "Read-only loop and seam inspection", AccessibleDescription = "Read-only native local transforms. F6 moves regions; Escape closes this drawer. Only First, Last and Closing frame seek the viewport." };
            var layout = new TableLayoutPanel { Name = "loopInspectionLayout", Dock = DockStyle.Top, ColumnCount = 1, RowCount = 7 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int row = 0; row < 7; row++) layout.RowStyles.Add(new RowStyle(row == 4 || row == 5 ? SizeType.Percent : SizeType.AutoSize, row == 4 ? 55 : row == 5 ? 45 : 0));
            layout.Controls.Add(new Label { Text = "Inspect loop / seam", Font = WorkspaceAppearance.HeadingFont, AutoSize = true, Dock = DockStyle.Fill }, 0, 0);
            _loopInspectionSummary = new Label { Name = "loopInspectionSummary", AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false,
                AccessibleName = "Exact animation and target identities, stored loop and preview loop" };
            layout.Controls.Add(_loopInspectionSummary, 0, 1);
            _loopInspectionStatus = new Label { Name = "loopInspectionStatus", AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false,
                AccessibleName = "Inspection freshness and sampling notices" };
            layout.Controls.Add(_loopInspectionStatus, 0, 2);
            _loopInspectionPairs = new ComboBox { Name = "loopInspectionPairs", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
                AccessibleName = "Boundary pair", AccessibleDescription = "Select First to Last or Closing to First measurements. This changes only the table, without seeking." };
            _loopInspectionPairs.SelectedIndexChanged += (s, e) => PopulateLoopInspectionRows();
            layout.Controls.Add(_loopInspectionPairs, 0, 3);
            _loopInspectionBones = new DataGridView { Name = "loopInspectionBones", Dock = DockStyle.Fill, ReadOnly = true,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                AutoGenerateColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None, RowHeadersVisible = false,
                MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EditMode = DataGridViewEditMode.EditProgrammatically, AccessibleName = "Read-only local boundary differences by exact bone",
                AccessibleDescription = "Translation uses native units, rotation is shortest orientation difference in degrees, and scale is the largest absolute local component difference. Values are observations, not error scores." };
            string[] names = { "Bone", "Classification", "Translation", "Rotation °", "Scale", "Notice" };
            for (int index = 0; index < names.Length; index++)
                _loopInspectionBones.Columns.Add(new DataGridViewTextBoxColumn { Name = "loopInspectionColumn" + index, HeaderText = names[index], SortMode = DataGridViewColumnSortMode.NotSortable });
            // SelectionChanged can precede CurrentCell being updated. Read the
            // row only after native DataGridView has committed its current cell.
            _loopInspectionBones.CurrentCellChanged += (s, e) => ShowLoopInspectionDetails();
            layout.Controls.Add(_loopInspectionBones, 0, 4);
            _loopInspectionDetails = new TextBox { Name = "loopInspectionDetails", Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
                ScrollBars = ScrollBars.Both, WordWrap = false, AccessibleName = "Exact local samples, raw Euler differences and authored boundary keys and tangents" };
            layout.Controls.Add(_loopInspectionDetails, 0, 5);
            _loopInspectionActions = new FlowLayoutPanel { Name = "loopInspectionActions", Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            _loopInspectionActions.Controls.Add(LibraryButton("loopInspectionFirst", "First · 1", () => JumpLoopInspection(0)));
            _loopInspectionActions.Controls.Add(LibraryButton("loopInspectionLast", "Last", () => JumpLoopInspection(1)));
            _loopInspectionActions.Controls.Add(LibraryButton("loopInspectionClosing", "Closing frame", () => JumpLoopInspection(2)));
            _loopInspectionActions.Controls.Add(LibraryButton("loopInspectionRefresh", "Refresh", RefreshLoopInspection));
            _loopInspectionActions.Controls.Add(LibraryButton("loopInspectionClose", "Close", CloseLoopInspection));
            foreach (Control control in _loopInspectionActions.Controls)
                control.AccessibleDescription = control.Name == "loopInspectionRefresh" ? "Pause and recompute from already-owned native data without seeking or changing the document." :
                    control.Name == "loopInspectionClose" ? "Close inspection and restore the invoking control. F6 moves between regions; Escape closes when focus is in this drawer." :
                    "Explicitly pause and seek the existing native viewport. No animation keys or loop settings are changed.";
            var hints = new ToolTip();
            foreach (Control control in _loopInspectionActions.Controls) hints.SetToolTip(control, control.AccessibleDescription);
            hints.SetToolTip(_loopInspectionPairs, _loopInspectionPairs.AccessibleDescription);
            hints.SetToolTip(_loopInspectionBones, _loopInspectionBones.AccessibleDescription);
            _loopInspectionPane.Disposed += (s, e) => hints.Dispose();
            _loopInspectionBones.Enter += (s, e) => _loopInspectionPane.ScrollControlIntoView(_loopInspectionBones);
            layout.Controls.Add(_loopInspectionActions, 0, 6);
            _loopInspectionPane.Controls.Add(layout); _simpleWorkspaceBody.Controls.Add(_loopInspectionPane);
            PrepareWorkspaceTabOrder(_loopInspectionPane); DarkTheme.Apply(_loopInspectionPane);
        }

        private bool HasPendingLoopInspectionText
        {
            get
            {
                if (!modelEditControl1.ContainsFocus) return false;
                var numeric = WorkspaceFocusedControl(modelEditControl1) as BrawlLib.Internal.Windows.Controls.NumericInputBox;
                // NumericInputBox applies Text from OnLostFocus. Do not let an
                // inspection focus change implicitly commit an unfinished edit.
                return numeric != null && numeric.Text != numeric._value.ToString();
            }
        }
        private bool CanCaptureLoopInspection => modelEditControl1.Enabled && !SimpleWorkspaceBusy && !_controlSetupQueued && !_curvePending && !_restoringCurve &&
            !_fighterRestoring && !modelEditControl1.AwaitingRedoSave && !HasPendingLoopInspectionText;
        private bool OwnsLoopInspectionClip(CHR0Node clip) => clip != null && OwnsFighterRoot(clip) &&
            RegisteredFighterAssets().Any(asset => _fighterRoots.TryGetValue(asset.Id, out var root) && ReferenceEquals(root, clip.RootNode) &&
                DiscoverFighterAnimations(root).Any(animation => ReferenceEquals(animation, clip)));

        private void OpenLoopInspection()
        {
            if (!CanCaptureLoopInspection) throw new InvalidOperationException("Finish the current native edit before inspecting.");
            if (!(modelEditControl1.TargetModel is MDL0Node) || !OwnsLoopInspectionClip(modelEditControl1.SelectedCHR0))
                throw new InvalidOperationException("Select a project-owned native CHR0 animation on the current native target.");
            _loopInspectionOpener = _workspaceInvoker ?? CurrentWorkspaceFocus();
            BuildLoopInspection();
            if (_comparisonPane != null) _comparisonPane.Visible = false;
            HideBatchReview(); SetSimpleWorkspaceLibraryVisible(false);
            _loopInspectionPane.Visible = true;
            RefreshLoopInspection(); UpdateSimpleWorkspace(); FocusWorkspaceRegion(_loopInspectionPane, remember: false);
        }
        private void HideLoopInspection() { if (_loopInspectionPane != null) _loopInspectionPane.Visible = false; }
        private void CloseLoopInspection() { HideLoopInspection(); UpdateSimpleWorkspace(); RestoreWorkspaceFocus(_loopInspectionOpener); }

        private void RefreshLoopInspection()
        {
            if (!CanCaptureLoopInspection) throw new InvalidOperationException("Finish the current native edit before refreshing inspection.");
            var model = modelEditControl1.TargetModel as MDL0Node;
            var clip = modelEditControl1.SelectedCHR0;
            if (model == null || !OwnsLoopInspectionClip(clip)) throw new InvalidOperationException("Select a project-owned CHR0 on the editable native target.");
            // StopAnim is the ordinary native pause path. In particular, do not
            // flush a curve edit or select/rebuild anything for diagnostics.
            modelEditControl1.StopAnim();
            InvalidateLoopInspection("Refresh needed."); DetachLoopInspectionWatches();
            _loopInspectionModel = model; _loopInspectionClip = clip; _loopInspectionProject = _fighterProject;
            WatchLoopInspectionNode(model); WatchLoopInspectionNode(clip);
            WatchLoopInspectionAncestors(model); WatchLoopInspectionAncestors(clip);
            bool reviewed = _bodyBindingReady && ReferenceEquals(_loadedControlModel, model) && ReferenceEquals(_loadedControlClip, clip) &&
                _loadedControlKey == ControlKey() && _controlEpoch > _loopInspectionBindingInvalidEpoch;
            try
            {
                LoopInspectionAnalyses++;
                _loopInspectionReport = NativeLoopInspection.Capture(model, clip, reviewed ? new HashSet<string>(_protectedMotionBones, StringComparer.Ordinal) : null);
            }
            catch (Exception error) when (error is InvalidOperationException || error is InvalidDataException || error is ArgumentException || error is OverflowException)
            {
                _loopInspectionStatus.Text = "Inspection unavailable: " + error.Message + Environment.NewLine + "Native editing remains available. Correct the data and choose Refresh.";
                SyncLoopInspection(); LayoutSimpleWorkspace(); return;
            }
            _loopInspectionUpdating = true;
            try
            {
                int pair = _loopInspectionPairs.SelectedIndex;
                _loopInspectionPairs.Items.Clear(); _loopInspectionPairs.Items.Add("First → Last · local transforms");
                if (_loopInspectionReport.Loop) _loopInspectionPairs.Items.Add("Closing → First · local transforms");
                _loopInspectionPairs.SelectedIndex = pair == 1 && _loopInspectionReport.Loop ? 1 : 0;
            }
            finally { _loopInspectionUpdating = false; }
            _loopInspectionStatus.Text = "Current native data · " + _loopInspectionReport.Notices.Length + " notices in Details." + Environment.NewLine +
                "Closing is an explicit sample; ordinary playback is unchanged.";
            PopulateLoopInspectionRows(); SyncLoopInspection(); LayoutSimpleWorkspace();
        }

        private void InvalidateLoopInspection(string reason = "Native content changed. Refresh to inspect again.")
        {
            _loopInspectionReport = null;
            _loopInspectionSummaryDirty = true;
            if (_loopInspectionBones != null && !_loopInspectionBones.IsDisposed)
            {
                _loopInspectionBones.Rows.Clear(); _loopInspectionDetails.Clear();
                _loopInspectionStatus.Text = reason;
                _loopInspectionPairs.Enabled = false;
            }
            UpdateLoopInspectionActions();
        }
        private void LoopInspectionAuthored(CHR0Node clip)
        { if (ReferenceEquals(clip, _loopInspectionClip)) InvalidateLoopInspection(); }
        private void WatchLoopInspectionNode(ResourceNode node, bool descendants = true)
        {
            if (!_loopInspectionWatches.Add(node)) return;
            node.PropertyChanged += LoopInspectionResourceChanged; node.Renamed += LoopInspectionResourceChanged;
            node.Replaced += LoopInspectionResourceChanged; node.Restored += LoopInspectionResourceChanged; node.Disposing += LoopInspectionResourceChanged;
            node.ChildAdded += LoopInspectionChildChanged; node.ChildRemoved += LoopInspectionChildChanged; node.ChildInserted += LoopInspectionChildInserted;
            if (descendants) foreach (ResourceNode child in node.Children) WatchLoopInspectionNode(child);
        }
        private void WatchLoopInspectionAncestors(ResourceNode node)
        { for (ResourceNode parent = node.Parent; parent != null; parent = parent.Parent) WatchLoopInspectionNode(parent, false); }
        private void LoopInspectionChildInserted(int index, ResourceNode parent, ResourceNode child) => LoopInspectionChildChanged(parent, child);
        private void LoopInspectionChildChanged(ResourceNode parent, ResourceNode child)
        {
            LoopInspectionResourceChanged(parent);
            if (ReferenceEquals(child.Parent, parent) && (ReferenceEquals(parent, _loopInspectionClip) || ReferenceEquals(parent, _loopInspectionModel) ||
                _loopInspectionClip != null && ReviewContains(_loopInspectionClip, parent) || _loopInspectionModel != null && ReviewContains(_loopInspectionModel, parent)))
                WatchLoopInspectionNode(child);
        }
        private void LoopInspectionResourceChanged(ResourceNode node)
        {
            if (ReferenceEquals(node, _loopInspectionModel) || _loopInspectionModel != null && ReviewContains(_loopInspectionModel, node))
                _loopInspectionBindingInvalidEpoch = _controlEpoch;
            InvalidateLoopInspection();
        }
        private void DetachLoopInspectionWatches()
        {
            foreach (ResourceNode node in _loopInspectionWatches)
            {
                node.PropertyChanged -= LoopInspectionResourceChanged; node.Renamed -= LoopInspectionResourceChanged;
                node.Replaced -= LoopInspectionResourceChanged; node.Restored -= LoopInspectionResourceChanged; node.Disposing -= LoopInspectionResourceChanged;
                node.ChildAdded -= LoopInspectionChildChanged; node.ChildRemoved -= LoopInspectionChildChanged; node.ChildInserted -= LoopInspectionChildInserted;
            }
            _loopInspectionWatches.Clear();
        }
        private void ResetLoopInspection()
        {
            DetachLoopInspectionWatches(); InvalidateLoopInspection("Select an owned native animation, then Refresh.");
            _loopInspectionModel = null; _loopInspectionClip = null; _loopInspectionProject = null;
            _loopInspectionBindingInvalidEpoch = -1;
            if (_loopInspectionPane != null && !_loopInspectionPane.IsDisposed) HideLoopInspection();
        }

        private void SyncLoopInspection()
        {
            if (_loopInspectionPane == null || _loopInspectionPane.IsDisposed || _loopInspectionUpdating) return;
            var model = modelEditControl1.TargetModel as MDL0Node; var clip = modelEditControl1.SelectedCHR0;
            if (!ReferenceEquals(_loopInspectionModel, model) || !ReferenceEquals(_loopInspectionClip, clip) || !ReferenceEquals(_loopInspectionProject, _fighterProject))
            {
                DetachLoopInspectionWatches(); InvalidateLoopInspection("Selection changed. Refresh to inspect the current native animation.");
                _loopInspectionModel = model; _loopInspectionClip = clip; _loopInspectionProject = _fighterProject;
                if (model != null) { WatchLoopInspectionNode(model); WatchLoopInspectionAncestors(model); }
                if (clip != null) { WatchLoopInspectionNode(clip); WatchLoopInspectionAncestors(clip); }
            }
            bool preview = modelEditControl1.PlaybackPanel?.chkLoop.Checked == true;
            if (_loopInspectionSummaryDirty || _loopInspectionProjectLabel != _fighterProject?.Name || _loopInspectionPreviewLoop != preview || _loopInspectionLastFrames != (clip?.FrameCount ?? -1) || _loopInspectionLastStoredLoop != (clip?.Loop ?? false))
            {
                string key = clip == null ? "No CHR0 selected" : SelectedFighterAnimationKey() ?? "Not project-owned";
                _loopInspectionSummary.Text = "Project: " + (_fighterProject?.Name ?? "Untitled") + Environment.NewLine +
                    "Target: " + (model?.Name ?? "None") + " · " + (_fighterProject?.Model?.Id ?? "—") + Environment.NewLine +
                    "Target resource: " + (model == null ? "—" : WorkspaceNativeIdentity(model)) + Environment.NewLine +
                    "Clip: " + (clip?.Name ?? "None") + " · " + key + Environment.NewLine +
                    (clip == null ? "" : clip.FrameCount + " frames · stored loop " + (clip.Loop ? "On" : "Off") + " · preview loop " + (preview ? "On" : "Off"));
                _loopInspectionSummaryDirty = false; _loopInspectionPreviewLoop = preview;
                _loopInspectionProjectLabel = _fighterProject?.Name;
                _loopInspectionLastFrames = clip?.FrameCount ?? -1; _loopInspectionLastStoredLoop = clip?.Loop ?? false;
                UpdateLoopInspectionActions();
            }
            if (_loopInspectionLastBusy != !CanCaptureLoopInspection)
            { _loopInspectionLastBusy = !CanCaptureLoopInspection; UpdateLoopInspectionActions(); }
        }
        private void UpdateLoopInspectionActions()
        {
            if (_loopInspectionActions == null || _loopInspectionActions.IsDisposed) return;
            bool available = CanCaptureLoopInspection;
            for (int index = 0; index < 3; index++)
            {
                var button = _loopInspectionActions.Controls[index];
                button.Enabled = available && _loopInspectionReport != null && _loopInspectionReport.SafeJumps[index];
            }
            _loopInspectionActions.Controls[1].Text = "Last" + (_loopInspectionClip == null ? "" : " · " + _loopInspectionClip.FrameCount);
            _loopInspectionActions.Controls[2].Text = "Closing frame" + (_loopInspectionClip?.Loop != true ? "" : " · " + (_loopInspectionClip.FrameCount + 1));
            _loopInspectionActions.Controls[3].Enabled = available && _loopInspectionModel != null && _loopInspectionClip != null;
            _loopInspectionActions.Controls[4].Enabled = true;
            _loopInspectionPairs.Enabled = _loopInspectionReport != null;
        }
        private void PopulateLoopInspectionRows()
        {
            if (_loopInspectionUpdating || _loopInspectionBones == null) return;
            string selected = (_loopInspectionBones.CurrentRow?.Tag as NativeLoopBone)?.Identity;
            _loopInspectionUpdating = true;
            try
            {
                _loopInspectionBones.Rows.Clear();
                if (_loopInspectionReport == null) return;
                foreach (NativeLoopBone bone in _loopInspectionReport.Bones)
                {
                    var difference = _loopInspectionPairs.SelectedIndex == 1 ? bone.Closing : bone.Endpoints;
                    int index = _loopInspectionBones.Rows.Add(bone.Name, bone.Classification,
                        difference.Valid ? difference.TranslationDistance.ToString("G6", CultureInfo.InvariantCulture) : "Unavailable",
                        difference.Valid ? difference.RotationDegrees.ToString("G6", CultureInfo.InvariantCulture) : "Unavailable",
                        difference.Valid ? difference.ScaleMaximum.ToString("G6", CultureInfo.InvariantCulture) : "Unavailable", bone.Issue);
                    _loopInspectionBones.Rows[index].Tag = bone;
                    if (bone.Identity == selected) _loopInspectionBones.CurrentCell = _loopInspectionBones.Rows[index].Cells[0];
                }
            }
            finally { _loopInspectionUpdating = false; ShowLoopInspectionDetails(); }
        }
        private void ShowLoopInspectionDetails()
        {
            if (_loopInspectionUpdating || _loopInspectionDetails == null) return;
            if (_loopInspectionReport == null) { _loopInspectionDetails.Clear(); return; }
            var bone = _loopInspectionBones.CurrentRow?.Tag as NativeLoopBone;
            var difference = bone == null ? null : _loopInspectionPairs.SelectedIndex == 1 ? bone.Closing : bone.Endpoints;
            _loopInspectionDetails.Text = bone == null ? "Select a bone to read exact native samples and authored boundary keys." :
                "Exact bone: " + bone.Identity + Environment.NewLine + bone.Classification + Environment.NewLine +
                (string.IsNullOrEmpty(bone.Issue) ? "" : bone.Issue + Environment.NewLine) + difference.Details + Environment.NewLine + bone.Details +
                Environment.NewLine + "Inspection notices:" + Environment.NewLine + string.Join(Environment.NewLine, _loopInspectionReport.Notices);
        }
        private void JumpLoopInspection(int endpoint)
        {
            SyncLoopInspection();
            if (!CanCaptureLoopInspection || _loopInspectionReport == null || endpoint < 0 || endpoint > 2 || !_loopInspectionReport.SafeJumps[endpoint])
                throw new InvalidOperationException("Refresh inspection before seeking a safe boundary sample.");
            modelEditControl1.StopAnim();
            int frame = endpoint == 0 ? 1 : endpoint == 1 ? _loopInspectionReport.Frames : _loopInspectionReport.Frames + 1;
            modelEditControl1.SetFrame(frame);
        }

        private void LayoutLoopInspection(int width, int height)
        {
            if (_loopInspectionPane == null) return;
            int paneWidth = Math.Min(WorkspaceAppearance.Pixels(440), Math.Max(280, width - 120));
            bool overlay = width - paneWidth < NativeWorkspaceMinimumWidth;
            _loopInspectionPane.Font = WorkspaceAppearance.Font; _loopInspectionPane.Dock = overlay ? DockStyle.None : DockStyle.Left;
            _loopInspectionPane.Width = paneWidth;
            if (overlay) _loopInspectionPane.Bounds = new Rectangle(0, 0, paneWidth, height);
            var layout = (TableLayoutPanel)_loopInspectionPane.Controls[0];
            layout.Controls[0].Font = WorkspaceAppearance.HeadingFont;
            int contentWidth = Math.Max(240, paneWidth - 42);
            _loopInspectionSummary.MaximumSize = _loopInspectionStatus.MaximumSize = new Size(contentWidth, 0);
            _loopInspectionPairs.Font = WorkspaceAppearance.Font;
            _loopInspectionPairs.ItemHeight = WorkspaceAppearance.Font.Height + 6;
            _loopInspectionPairs.MinimumSize = Size.Empty;
            int pairHeight = Math.Max(_loopInspectionPairs.PreferredHeight, WorkspaceAppearance.Font.Height + 18);
            _loopInspectionPairs.MinimumSize = new Size(0, pairHeight);
            // Owner-drawn ComboBox preferred heights can remain cached at the
            // previous text size. Reserve the measured selection height before
            // the grid is positioned, rather than relying on that cached row.
            layout.RowStyles[3].SizeType = SizeType.Absolute;
            layout.RowStyles[3].Height = pairHeight + _loopInspectionPairs.Margin.Vertical;
            _loopInspectionPairs.DropDownWidth = Math.Max(contentWidth, TextRenderer.MeasureText("Closing → First · local transforms", WorkspaceAppearance.Font).Width + 28);
            _loopInspectionBones.RowTemplate.Height = WorkspaceAppearance.Font.Height + 8;
            foreach (DataGridViewRow row in _loopInspectionBones.Rows) row.Height = _loopInspectionBones.RowTemplate.Height;
            _loopInspectionBones.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            for (int index = 0; index < _loopInspectionBones.Columns.Count; index++)
                _loopInspectionBones.Columns[index].Width = WorkspaceAppearance.Pixels(index == 0 ? 115 : index == 1 || index == 5 ? 150 : 90);
            foreach (Button button in _loopInspectionActions.Controls)
            { button.AutoSize = false; button.Size = new Size(TextRenderer.MeasureText(button.Text, button.Font).Width + 22, WorkspaceAppearance.Font.Height + 18); }
            layout.Height = Math.Max(height - 20, _loopInspectionSummary.PreferredHeight + _loopInspectionStatus.PreferredHeight +
                _loopInspectionActions.GetPreferredSize(new Size(contentWidth, 0)).Height + WorkspaceAppearance.Pixels(300));
        }
    }
}
