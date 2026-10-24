using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private Panel _simpleWorkspaceBody;
        private SimpleWorkspacePanel _simpleWorkspaceTask;
        private ToolStrip _simpleWorkspaceNavigation;
        private ToolStrip _simpleWorkspaceToolbar;
        private ToolStripButton[] _simpleWorkspaceStages;
        private ToolStripButton _simpleWorkspaceLibrary, _simpleWorkspaceFocus;
        private ToolStripLabel _simpleWorkspaceSummary;
        private int _simpleWorkspaceStage;
        private bool _simpleWorkspaceFocused, _simpleWorkspaceLayingOut;
        private bool _simpleWorkspaceLastBusy;

        private bool SimpleWorkspaceBusy =>
            _controlBusy || _retargetBusy || _workspaceBusy || _pinGesture != null;

        private bool SimpleWorkspaceSourceReady =>
            LibrarySelection?.State == LibrarySourceState.Available &&
            LibrarySelection.Clip.Channel == "CHR0 skeletal" &&
            !_libraryReplacingTree && _librarySearchTimer?.Enabled != true;

        private void InitializeSimpleWorkspace(ToolStrip toolbar, StatusStrip status)
        {
            _simpleWorkspaceToolbar = toolbar;
            // Keep the entire original editor, including its own menus and native
            // left/right panels, transform tools, interpolation and playback.
            _simpleWorkspaceBody = new Panel { Name = "workspaceBody", Dock = DockStyle.Fill };
            _simpleWorkspaceTask = new SimpleWorkspacePanel { Name = "workspaceTaskPane" };
            _simpleWorkspaceTask.StageRequested += SetSimpleWorkspaceStage;
            _simpleWorkspaceTask.ActionRequested += action => RunFighterAction(() => RunSimpleWorkspaceAction(action));
            _simpleWorkspaceBody.Controls.Add(_simpleWorkspaceTask);
            _simpleWorkspaceBody.Controls.Add(modelEditControl1);
            modelEditControl1.BringToFront();

            _simpleWorkspaceNavigation = new ToolStrip
            {
                Name = "workspaceNavigation", Dock = DockStyle.Top, AutoSize = false,
                Height = 40, GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(5, 4, 5, 4)
            };
            string[] names = { "Source", "Target", "Preview", "Export" };
            string[] labels = { "1  Source", "2  Target", "3  Preview and fix", "4  Export" };
            _simpleWorkspaceStages = new ToolStripButton[4];
            for (int index = 0; index < names.Length; index++)
            {
                int stage = index;
                var button = new ToolStripButton(labels[index])
                {
                    Name = "workspaceStage" + names[index], AccessibleName = labels[index],
                    DisplayStyle = ToolStripItemDisplayStyle.Text, AutoToolTip = false,
                    Margin = new Padding(2, 0, 7, 0)
                };
                button.Click += (sender, args) => SetSimpleWorkspaceStage(stage);
                _simpleWorkspaceStages[index] = button;
                _simpleWorkspaceNavigation.Items.Add(button);
            }
            _simpleWorkspaceSummary = new ToolStripLabel { Name = "workspaceSelectionSummary" };
            _simpleWorkspaceNavigation.Items.Add(_simpleWorkspaceSummary);

            // Keep existing custom commands in the original toolbar's overflow
            // menu. The actual native editor menus and controls are untouched.
            foreach (ToolStripItem item in toolbar.Items)
                if (item.Name != "fighterProjectMenu" && item.Name != "fighterName" &&
                    item.Name != "fighterSave" && item.Name != "fighterUndoImport")
                    item.Overflow = ToolStripItemOverflow.Always;
            toolbar.OverflowButton.AccessibleName = "More editing tools";
            toolbar.OverflowButton.ToolTipText = "More editing tools";
            toolbar.Items.Add(new ToolStripSeparator());
            toolbar.Items.Add(new ToolStripLabel("Use animations on a model") { Name = "workspaceOperation" });
            toolbar.Items.Add(new ToolStripLabel("Target: Brawl") { Name = "workspaceTargetGame" });
            _simpleWorkspaceLibrary = AddFighterButton(toolbar, "Source library", "workspaceSourceLibrary",
                () => RunFighterAction(ToggleAnimationLibrary));
            _simpleWorkspaceFocus = AddFighterButton(toolbar, "Focus editor", "workspaceFocusEditor",
                ToggleSimpleWorkspaceFocus);

            Controls.Add(_simpleWorkspaceBody);
            Controls.Add(_simpleWorkspaceNavigation);
            _simpleWorkspaceBody.BringToFront();
            Controls.SetChildIndex(toolbar, Controls.Count - 1);
            Controls.SetChildIndex(_simpleWorkspaceNavigation, 1);
            Resize += (sender, args) => LayoutSimpleWorkspace();
            _simpleWorkspaceBody.SizeChanged += (sender, args) => LayoutSimpleWorkspace();
            EventHandler busyChanged = (sender, args) =>
            {
                if (!IsDisposed && _simpleWorkspaceLastBusy != SimpleWorkspaceBusy) UpdateSimpleWorkspace();
            };
            Application.Idle += busyChanged;
            _simpleWorkspaceBody.Disposed += (sender, args) => Application.Idle -= busyChanged;
            DarkTheme.Apply(_simpleWorkspaceBody);
            DarkTheme.Apply(_simpleWorkspaceNavigation);
            UpdateSimpleWorkspace();
        }

        private void SetSimpleWorkspaceStage(int stage)
        {
            if (stage < 0 || stage > 3) throw new ArgumentOutOfRangeException(nameof(stage));
            if (SimpleWorkspaceBusy) return;
            _simpleWorkspaceStage = stage;
            _simpleWorkspaceFocused = false;
            if (stage != 0 && _libraryPane?.Visible == true)
                SetSimpleWorkspaceLibraryVisible(false);
            UpdateSimpleWorkspace();
        }

        private void ResumeSimpleWorkspace()
        {
            _simpleWorkspaceStage = modelEditControl1.TargetModel == null ? 0 :
                modelEditControl1.TargetAnimation == null ? 1 : 2;
            UpdateSimpleWorkspace();
        }

        private void ToggleSimpleWorkspaceFocus()
        {
            _simpleWorkspaceFocused = !_simpleWorkspaceFocused;
            if (_simpleWorkspaceFocused && _libraryPane?.Visible == true)
                SetSimpleWorkspaceLibraryVisible(false);
            UpdateSimpleWorkspace();
        }

        private void SetSimpleWorkspaceLibraryVisible(bool visible)
        {
            if (_libraryPane == null)
            {
                if (!visible) return;
                BuildLibraryPane();
            }
            if (_libraryPane.Visible != visible) ToggleAnimationLibrary();
            LayoutSimpleWorkspace();
        }

        private void RunSimpleWorkspaceAction(string action)
        {
            if (SimpleWorkspaceBusy)
                throw new InvalidOperationException("Finish or cancel the current edit before changing the transfer.");
            switch (action)
            {
                case "source":
                    _simpleWorkspaceFocused = false;
                    SetSimpleWorkspaceLibraryVisible(true);
                    break;
                case "audition":
                    if (!SimpleWorkspaceSourceReady)
                        throw new InvalidOperationException("Select an available source animation with its verified original model.");
                    SetSimpleWorkspaceLibraryVisible(true);
                    PreviewLibraryOriginal();
                    break;
                case "target":
                    ChooseFighterModel();
                    break;
                case "motions":
                    ChooseFighterAnimations();
                    break;
                case "transfer":
                    if (!SimpleWorkspaceSourceReady || !(modelEditControl1.TargetModel is MDL0Node))
                        throw new InvalidOperationException("Choose an available source animation and a target model first.");
                    UseLibraryOnTarget(); // The original Next v7 route and its existing stale/cancel guards.
                    break;
                case "controls":
                    ShowAnimationControls();
                    break;
                case "transitions":
                    ShowTransitionFit();
                    break;
                case "timing":
                    if (!SimpleWorkspaceSourceReady)
                        throw new InvalidOperationException("Choose a source animation before fitting its timing.");
                    ShowLibraryBatch();
                    break;
                case "export":
                    ChooseSimpleWorkspaceExport();
                    break;
                case "save":
                    SaveFighterFromUi();
                    break;
                case "details":
                    _simpleWorkspaceToolbar.OverflowButton.ShowDropDown();
                    break;
            }
            UpdateSimpleWorkspace();
        }

        private void ChooseSimpleWorkspaceExport()
        {
            var selected = modelEditControl1.TargetAnimation as CHR0Node;
            if (selected == null) throw new InvalidOperationException("Select a target skeletal animation to export.");
            using (var dialog = new SaveFileDialog
            {
                Title = "Export Target Animation", Filter = "Native Brawl animation (*.chr0)|*.chr0",
                DefaultExt = "chr0", AddExtension = true, FileName = selected.Name + ".chr0"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!ReferenceEquals(selected, modelEditControl1.TargetAnimation))
                    throw new InvalidOperationException("The selected animation changed. Review the current result before exporting.");
                ExportSimpleWorkspaceResult(dialog.FileName);
            }
        }

        internal void ExportSimpleWorkspaceResult(string destination)
        {
            if (SimpleWorkspaceBusy) throw new InvalidOperationException("Finish the current edit before exporting.");
            var target = modelEditControl1.TargetModel as MDL0Node;
            var clip = modelEditControl1.TargetAnimation as CHR0Node;
            var asset = SelectedFighterAsset();
            if (target == null || clip == null || !OwnsFighterRoot(clip))
                throw new InvalidOperationException("Select an animation on the current target model.");
            if (asset?.Retarget?.ReviewDraft == true)
                throw new InvalidOperationException("This advanced review draft is blocked and cannot be exported as an accepted result.");
            if (!string.Equals(Path.GetExtension(destination), ".chr0", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This workspace action exports native CHR0. Other qualified resource exports remain in the native menus.");
            modelEditControl1.StopAnim();
            if (asset?.Retarget != null && BrawlRetargeter.SkeletonFingerprint(target) != asset.RigIdentity)
                throw new InvalidOperationException("The target rig changed. Review the animation on its original target before export.");
            ExportFighterNode(clip, destination);
        }

        private void UpdateSimpleWorkspace()
        {
            if (_simpleWorkspaceTask == null) return;
            var selectedSource = LibrarySelection;
            bool sourceReady = SimpleWorkspaceSourceReady;
            var targetModel = modelEditControl1.TargetModel as MDL0Node;
            bool hasTarget = targetModel != null;
            var asset = SelectedFighterAsset();
            var clip = modelEditControl1.TargetAnimation as CHR0Node;
            var savedSource = asset?.Retarget?.Library;
            string savedSourceName = asset?.Retarget?.SourceClip?.Split('|')[0];
            string source = selectedSource == null ? (savedSource == null ? null :
                savedSourceName + "\n" + Path.GetFileNameWithoutExtension(asset.Retarget.SourceModelPath) +
                " · " + savedSource.Game + "\nSaved source link") :
                selectedSource.Clip.Name + "\n" + (selectedSource.Model?.Character ?? selectedSource.Model?.Name ?? "Unresolved model") +
                " · " + selectedSource.Motion.Game + "\n" + LibraryStateText(selectedSource.State);
            string target = !hasTarget ? null : targetModel.Name + "\n" +
                Path.GetFileName(_fighterProject.Model?.SourcePath);
            string result = clip == null ? null : clip.Name + " · " + clip.FrameCount + " frames\n" +
                (asset?.Retarget?.ReviewDraft == true ? "Blocked review draft" :
                    asset?.Retarget == null ? "Selected native animation" : "Independent target animation") +
                "\nReview motion before export.";
            bool busy = SimpleWorkspaceBusy;
            _simpleWorkspaceLastBusy = busy;
            _simpleWorkspaceTask.SetStage(_simpleWorkspaceStage);
            _simpleWorkspaceTask.SetState(source, target, result, sourceReady, hasTarget,
                sourceReady && hasTarget, hasTarget && clip != null && OwnsFighterRoot(clip) &&
                    asset?.Retarget?.ReviewDraft != true, busy);
            for (int i = 0; i < _simpleWorkspaceStages.Length; i++)
            {
                _simpleWorkspaceStages[i].Checked = i == _simpleWorkspaceStage;
                _simpleWorkspaceStages[i].Enabled = !busy;
            }
            _simpleWorkspaceSummary.Text = (sourceReady ? selectedSource.Clip.Name :
                savedSource == null ? "Choose a source" : "Saved source: " + savedSourceName) +
                "  →  " + (hasTarget ? targetModel.Name : "Choose a target");
            _simpleWorkspaceLibrary.Checked = _libraryPane?.Visible == true;
            _simpleWorkspaceFocus.Checked = _simpleWorkspaceFocused;
            _simpleWorkspaceFocus.Text = _simpleWorkspaceFocused ? "Show workflow" : "Focus editor";
            LayoutSimpleWorkspace();
        }

        private void LayoutSimpleWorkspace()
        {
            if (_simpleWorkspaceBody == null || _simpleWorkspaceLayingOut) return;
            _simpleWorkspaceLayingOut = true;
            try
            {
                bool libraryVisible = _libraryPane?.Visible == true;
                _simpleWorkspaceTask.Visible = !_simpleWorkspaceFocused &&
                    (!libraryVisible || _simpleWorkspaceBody.ClientSize.Width - _libraryPane.Width -
                        _simpleWorkspaceTask.Width >= 760);
                modelEditControl1.BringToFront();
                LayoutSimpleWorkspaceLibraryPreview();
                _simpleWorkspaceBody.PerformLayout();
            }
            finally { _simpleWorkspaceLayingOut = false; }
        }

        private void LayoutSimpleWorkspaceLibraryPreview()
        {
            if (_libraryPane == null) return;
            var layout = _libraryPane.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
            if (layout == null) return;
            bool preview = _libraryLease != null;
            layout.RowStyles[4].Height = _libraryPane.Height < 650 ? 42 : 70;
            layout.RowStyles[6].Height = preview ? 22 : 0;
            layout.RowStyles[7].Height = preview ? (_libraryPane.Height < 650 ? 120 : 160) : 0;
            layout.RowStyles[8].Height = preview ? 38 : 0;
            for (int row = 6; row < 9; row++)
            {
                var control = layout.GetControlFromPosition(0, row);
                if (control != null) control.Visible = preview;
            }
        }
    }
}
