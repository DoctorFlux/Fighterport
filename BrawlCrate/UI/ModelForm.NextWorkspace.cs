using BrawlCrate.Studio;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private ToolStripDropDownButton _nextAnimationMenu, _nextViewMenu;
        private bool? _nextToolbarGuide;

        private ToolStripMenuItem NextCommand(ToolStripDropDownItem menu, string name, string text, Action action)
        {
            var item = new ToolStripMenuItem(text) { Name = name, AccessibleName = text };
            item.Click += (s, e) => WithWorkspaceInvoker(item, () => RunFighterAction(action));
            menu.DropDownItems.Add(item);
            return item;
        }

        private void InitializeNextWorkspace()
        {
            _simpleWorkspaceFocused = true;
            _simpleWorkspaceDrawer = false;
            _simpleWorkspaceTask.Visible = _simpleWorkspaceActions.Visible = _simpleWorkspaceNavigation.Visible = false;
            _nextAnimationMenu = new ToolStripDropDownButton("Animation") { Name = "nextAnimationMenu", Overflow = ToolStripItemOverflow.Never };
            _nextViewMenu = new ToolStripDropDownButton("View") { Name = "nextViewMenu", Overflow = ToolStripItemOverflow.Never };
            _simpleWorkspaceToolbar.Items.Insert(2, _nextAnimationMenu);
            _simpleWorkspaceToolbar.Items.Insert(3, _nextViewMenu);
            var load = new ToolStripButton("Load source...") { Name = "nextLoadSourceButton" };
            load.ToolTipText = "Open an original model and animations for this session. Add to library is a separate explicit action.";
            load.Click += (s, e) => WithWorkspaceInvoker(load, () => RunFighterAction(LoadSessionSource));
            _simpleWorkspaceToolbar.Items.Insert(4, load);
            NextCommand(_nextAnimationMenu, "nextLoadSourceFiles", "Load source files...", LoadSessionSource);
            NextCommand(_nextAnimationMenu, "nextSharedLibrary", "Choose from library...", UseSharedSourceLibrary);
            NextCommand(_nextAnimationMenu, "nextAddBrawlDisc", "Add Brawl disc / folder...", ShowBrawlDiscLibrary);
            NextCommand(_nextAnimationMenu, "nextLibrarySources", "Manage library sources...", ShowLibrarySourceSettings);
            NextCommand(_nextAnimationMenu, "nextAddSourceToLibrary", "Add loaded source to library", AddSessionSourceToLibrary);
            NextCommand(_nextAnimationMenu, "nextPreviewLibraryOriginal", "Preview library original", PreviewLibraryOriginal);
            NextCommand(_nextAnimationMenu, "nextOpenLibraryTarget", "Open library model + animation", OpenLibraryAsTarget);
            _nextAnimationMenu.DropDownItems.Add(new ToolStripSeparator());
            NextCommand(_nextAnimationMenu, "nextReviewResults", "Review results...", OpenBatchReview);
            NextCommand(_nextAnimationMenu, "nextCompareSource", "Compare recorded source", OpenLinkedComparison);
            NextCommand(_nextAnimationMenu, "nextLoopInspection", "Inspect loop / seam", OpenLoopInspection);
            _nextAnimationMenu.DropDownItems.Add(new ToolStripSeparator());
            NextCommand(_nextAnimationMenu, "nextFitBoundaries", "Fit start and end...", ShowTransitionFit);
            NextCommand(_nextAnimationMenu, "nextBatchFit", "Source timing / batch...", ShowLibraryBatch);
            NextCommand(_nextAnimationMenu, "nextAnimationControls", "Animation controls...", ShowAnimationControls);
            _nextAnimationMenu.DropDownItems.Add(new ToolStripSeparator());
            NextCommand(_nextAnimationMenu, "nextExportAnimation", "Export verified animation...", ChooseSimpleWorkspaceExport);
            NextCommand(_nextAnimationMenu, "nextExportReviewedSet", "Export reviewed set...", ChooseWorkspaceSetExport);
            NextCommand(_nextViewMenu, "nextLibrary", "Animation library", ToggleAnimationLibrary);
            NextCommand(_nextViewMenu, "nextFocusEditor", "Focus target editor", FocusNextEditor);
            var text = new ToolStripMenuItem("Text size") { Name = "nextTextSize" };
            foreach (int percent in WorkspaceAppearance.TextSizes)
            {
                int value = percent;
                var choice = new ToolStripMenuItem(percent + "%") { Name = "nextText" + percent, Tag = percent };
                choice.Click += (s, e) => SetWorkspaceTextSize(value);
                text.DropDownItems.Add(choice);
            }
            _nextViewMenu.DropDownItems.Add(text);
            _nextViewMenu.DropDownItems.Add(new ToolStripSeparator());
            NextCommand(_nextViewMenu, "nextWorkflowGuide", "Workflow guide", ToggleSimpleWorkspaceWorkflow);
            _nextViewMenu.DropDownOpening += (s, e) =>
            {
                ((ToolStripMenuItem)_nextViewMenu.DropDownItems["nextLibrary"]).Checked = _libraryPane?.Visible == true;
                ((ToolStripMenuItem)_nextViewMenu.DropDownItems["nextWorkflowGuide"]).Checked = _simpleWorkspaceTask.Visible;
                foreach (ToolStripMenuItem choice in text.DropDownItems) choice.Checked = (int)choice.Tag == WorkspaceAppearance.TextPercent;
            };
            ApplyNextToolbarPresentation(false);
        }

        private void ApplyNextToolbarPresentation(bool guide)
        {
            if (_nextToolbarGuide == guide) return;
            _nextToolbarGuide = guide;
            string[] primary = { "fighterProjectMenu", "fighterName", "fighterSave", "fighterUndoImport",
                "fighterOpenModel", "fighterAddAnimations", "fighterRetarget", "fighterLibrary", "fighterTransitionFit", "nextLoadSourceButton" };
            foreach (ToolStripItem item in _simpleWorkspaceToolbar.Items)
            {
                if (item == _nextAnimationMenu || item == _nextViewMenu) continue;
                bool retained = guide ? primary.Take(4).Contains(item.Name) : primary.Contains(item.Name);
                item.Overflow = retained ? ToolStripItemOverflow.AsNeeded : ToolStripItemOverflow.Always;
                if (item.Name == "workspaceOperation" || item.Name == "workspaceTargetGame") item.Available = guide;
            }
            _simpleWorkspaceToolbar.OverflowButton.AccessibleName = "More native tools";
            _simpleWorkspaceToolbar.OverflowButton.ToolTipText = "More native tools: playback, resources, controls and advanced commands";
            _workspaceBarsText = _workspaceLayoutText = -1;
        }

        private void UpdateNextWorkspaceCommands(bool target, bool clip, bool busy)
        {
            if (_nextAnimationMenu == null) return;
            foreach (ToolStripItem item in _nextAnimationMenu.DropDownItems)
            {
                if (item is ToolStripSeparator) continue;
                if (item.Name == "nextAddSourceToLibrary")
                { item.Enabled = !busy && _sessionSourceLibrary != null && ReferenceEquals(_library, _sessionSourceLibrary); continue; }
                if (item.Name == "nextPreviewLibraryOriginal" || item.Name == "nextOpenLibraryTarget")
                { item.Enabled = !busy && SimpleWorkspaceSourceReady; continue; }
                if (item.Name == "nextLoadSourceFiles" || item.Name == "nextSharedLibrary" || item.Name == "nextAddBrawlDisc" || item.Name == "nextLibrarySources")
                { item.Enabled = !busy; continue; }
                item.Enabled = !busy && target && (item.Name == "nextReviewResults" || item.Name == "nextExportReviewedSet" ||
                    item.Name == "nextAnimationControls" || item.Name == "nextBatchFit" && SimpleWorkspaceSourceReady ||
                    item.Name != "nextBatchFit" && clip);
            }
            _nextViewMenu.Enabled = !busy;
            if (_libraryOpenTarget != null) _libraryOpenTarget.Enabled = !busy && SimpleWorkspaceSourceReady;
            if (_libraryCostume != null) _libraryCostume.Enabled = !busy && _libraryCostume.Items.Count > 0;
            if (_sourceFitButton != null) _sourceFitButton.Enabled = !busy && target && clip;
            foreach (string name in new[] { "fighterOpenModel", "fighterAddAnimations", "fighterLibrary", "fighterTransitionFit", "fighterSave", "nextLoadSourceButton" })
            {
                var item = _simpleWorkspaceToolbar.Items[name];
                if (item != null) item.Enabled = !busy && (name != "fighterTransitionFit" || target && clip);
            }
        }

        private void FocusNextEditor()
        {
            if (SimpleWorkspaceBusy) return;
            HideBatchReview(); HideLoopInspection();
            if (_comparisonPane != null) CloseLinkedComparison();
            SetSimpleWorkspaceLibraryVisible(false);
            _simpleWorkspaceFocused = true; _simpleWorkspaceDrawer = false;
            UpdateSimpleWorkspace(); FocusWorkspaceTarget(modelEditControl1.ModelPanel);
        }

        private void LayoutNextWorkspace()
        {
            bool inspecting = _loopInspectionPane?.Visible == true, reviewing = _batchReviewPane?.Visible == true;
            bool comparing = _comparisonPane?.Visible == true, library = _libraryPane?.Visible == true;
            bool guide = !inspecting && !reviewing && !comparing && !library && !_simpleWorkspaceFocused && _simpleWorkspaceDrawer;
            if (_simpleWorkspaceActions.Visible != guide || _simpleWorkspaceNavigation.Visible != guide)
            {
                // Change both bars as one form-level docking operation. A body
                // SizeChanged callback during a partial change otherwise keeps
                // the old guide inset after its bars have disappeared.
                _simpleWorkspaceLayingOut = true; SuspendLayout();
                try
                {
                    _simpleWorkspaceActions.Visible = _simpleWorkspaceNavigation.Visible = guide;
                    ApplyNextToolbarPresentation(guide);
                }
                finally { ResumeLayout(true); _simpleWorkspaceLayingOut = false; }
                _workspaceLayoutText = -1;
            }
            else ApplyNextToolbarPresentation(guide);
            if (guide) { LayoutGuidedWorkspace(); return; }
            int flags = (inspecting ? 1 : 0) | (reviewing ? 2 : 0) | (comparing ? 4 : 0) | (library ? 8 : 0) |
                (_simpleWorkspaceFocused ? 16 : 0) | (_simpleWorkspaceDrawer ? 32 : 0) | (_libraryLease != null ? 64 : 0);
            if (_workspaceLayoutSize == _simpleWorkspaceBody.ClientSize && _workspaceLayoutFlags == flags &&
                _workspaceLayoutText == WorkspaceAppearance.TextPercent && _workspaceLayoutMinimum == NativeWorkspaceMinimumWidth)
            {
                _simpleWorkspaceLayingOut = true;
                try
                {
                    if (library) LayoutSimpleWorkspaceLibraryPreview();
                    if (reviewing) LayoutBatchReview(_workspaceLayoutSize.Width, _workspaceLayoutSize.Height);
                    if (inspecting) LayoutLoopInspection(_workspaceLayoutSize.Width, _workspaceLayoutSize.Height);
                    RestoreWorkspaceOverlayOrder();
                }
                finally { _simpleWorkspaceLayingOut = false; }
                return;
            }
            _simpleWorkspaceLayingOut = true; _simpleWorkspaceBody.SuspendLayout();
            try
            {
                DiagnosticWorkspaceLayouts++;
                LayoutWorkspaceBars();
                int width = _simpleWorkspaceBody.ClientSize.Width, height = _simpleWorkspaceBody.ClientSize.Height;
                _simpleWorkspaceTask.Visible = false;
                _simpleWorkspaceWorkflow.Checked = false;
                if (_libraryPane != null)
                {
                    int paneWidth = Math.Min(WorkspaceAppearance.Pixels(370), Math.Max(300, width * 2 / 5));
                    bool overlay = width - paneWidth < NativeWorkspaceMinimumWidth;
                    _libraryPane.Dock = overlay ? DockStyle.None : DockStyle.Right;
                    _libraryPane.Width = paneWidth;
                    if (overlay) _libraryPane.Bounds = new Rectangle(width - paneWidth, 0, paneWidth, height);
                }
                if (_comparisonPane != null)
                {
                    _comparisonPane.Font = WorkspaceAppearance.Font;
                    _comparisonPane.Width = Math.Min(WorkspaceAppearance.Pixels(360), Math.Max(240, width / 3));
                }
                if (reviewing) LayoutBatchReview(width, height);
                if (inspecting) LayoutLoopInspection(width, height);
                if (library) LayoutSimpleWorkspaceLibraryPreview();
                modelEditControl1.BringToFront();
                _workspaceLayoutSize = _simpleWorkspaceBody.ClientSize; _workspaceLayoutFlags = flags;
                _workspaceLayoutText = WorkspaceAppearance.TextPercent; _workspaceLayoutMinimum = NativeWorkspaceMinimumWidth;
            }
            finally
            {
                _simpleWorkspaceBody.ResumeLayout(true); RestoreWorkspaceOverlayOrder(); _simpleWorkspaceLayingOut = false;
            }
            if (_workspaceLayoutSize != _simpleWorkspaceBody.ClientSize) LayoutSimpleWorkspace();
        }
    }
}
