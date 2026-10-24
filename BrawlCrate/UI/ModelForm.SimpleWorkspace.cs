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
        private ToolStripButton _simpleWorkspaceNewProject, _simpleWorkspaceOpenProject, _simpleWorkspaceMoreTools;
        private ToolStripLabel _simpleWorkspaceSummary;
        private ToolStrip _simpleWorkspaceActions;
        private ToolStripButton _simpleWorkspaceWorkflow;
        private ToolStripDropDownButton _simpleWorkspaceOptions;
        private bool _simpleWorkspaceDrawer;
        private int _simpleWorkspaceStage;
        private bool _simpleWorkspaceFocused, _simpleWorkspaceLayingOut;
        private bool _simpleWorkspaceLastBusy;
        private string _simpleWorkspaceSourceIssue, _simpleWorkspaceIssueClip;

        private bool SimpleWorkspaceBusy =>
            _controlBusy || _retargetBusy || _workspaceBusy || _pinGesture != null;

        private bool SimpleWorkspaceSourceReady =>
            LibrarySelection?.State == LibrarySourceState.Available &&
            LibrarySelection.Clip.Channel == "CHR0 skeletal" &&
            !SimpleWorkspaceSourceNeedsRepair(LibrarySelection) &&
            !_libraryReplacingTree && _librarySearchTimer?.Enabled != true;

        private bool SimpleWorkspaceSourceNeedsRepair(LibrarySearchItem source) =>
            source != null && (source.State == LibrarySourceState.Missing || source.State == LibrarySourceState.SourceChanged ||
                source.Clip.Id == _simpleWorkspaceIssueClip && _simpleWorkspaceSourceIssue != null);

        private string SimpleWorkspaceSourceStateText(LibrarySearchItem source) =>
            source.Clip.Id == _simpleWorkspaceIssueClip && _simpleWorkspaceSourceIssue != null ?
                "Verification failed — review original source" : LibraryStateText(source.State);

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
                button.Click += (sender, args) => WithWorkspaceInvoker(button, () => SetSimpleWorkspaceStage(stage));
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
            _simpleWorkspaceActions = new ToolStrip { Name="workspaceActions", Dock=DockStyle.Top,
                GripStyle=ToolStripGripStyle.Hidden, CanOverflow=false };
            _simpleWorkspaceWorkflow = AddFighterButton(_simpleWorkspaceActions,"Workflow","workspaceWorkflow",
                ToggleSimpleWorkspaceWorkflow);
            _simpleWorkspaceLibrary = AddFighterButton(_simpleWorkspaceActions, "Source library", "workspaceSourceLibrary",
                () => RunFighterAction(ToggleAnimationLibrary));
            _simpleWorkspaceFocus = AddFighterButton(_simpleWorkspaceActions, "Focus editor", "workspaceFocusEditor",
                ToggleSimpleWorkspaceFocus);
            _simpleWorkspaceMoreTools = AddFighterButton(_simpleWorkspaceActions, "More tools", "workspaceMoreTools",
                () => RunFighterAction(() => RunSimpleWorkspaceAction("details")));
            _simpleWorkspaceOpenProject = AddFighterButton(_simpleWorkspaceActions, "Open project", "workspaceOpenProject",
                () => RunFighterAction(() => RunSimpleWorkspaceAction("open-project")));
            _simpleWorkspaceNewProject = AddFighterButton(_simpleWorkspaceActions, "New project", "workspaceNewProject",
                () => RunFighterAction(() => RunSimpleWorkspaceAction("new-project")));

            _simpleWorkspaceOptions = new ToolStripDropDownButton("Workspace text") { Name="workspaceTextSize", AccessibleName="Workspace text size" };
            foreach(int percent in WorkspaceAppearance.TextSizes)
            {
                int selected=percent;
                var choice=new ToolStripMenuItem(percent+"%") { Name="workspaceText"+percent, Tag=percent };
                choice.Click+=(sender,args)=>SetWorkspaceTextSize(selected);
                _simpleWorkspaceOptions.DropDownItems.Add(choice);
            }
            _simpleWorkspaceActions.Items.Add(_simpleWorkspaceOptions);
            Controls.Add(_simpleWorkspaceActions);
            Controls.Add(_simpleWorkspaceBody);
            Controls.Add(_simpleWorkspaceNavigation);
            _simpleWorkspaceBody.BringToFront();
            Controls.SetChildIndex(toolbar, Controls.Count - 1);
            Controls.SetChildIndex(_simpleWorkspaceNavigation, 1);
            Controls.SetChildIndex(_simpleWorkspaceActions, 2);
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
            ApplyWorkspaceTextSize();
            InitializeWorkspaceKeyboard();
            UpdateSimpleWorkspace();
        }

        private void SetSimpleWorkspaceStage(int stage)
        {
            if (stage < 0 || stage > 3) throw new ArgumentOutOfRangeException(nameof(stage));
            if (SimpleWorkspaceBusy) return;
            _workspaceTaskOpener = _workspaceInvoker ?? CurrentWorkspaceFocus() ?? _simpleWorkspaceWorkflow;
            _simpleWorkspaceStage = stage;
            _simpleWorkspaceDrawer = true;
            _simpleWorkspaceFocused = false;
            if (_libraryPane?.Visible == true)
                SetSimpleWorkspaceLibraryVisible(false);
            UpdateSimpleWorkspace();
            FocusWorkspaceRegion(_simpleWorkspaceTask, remember: false);
        }

        private void ResumeSimpleWorkspace()
        {
            _simpleWorkspaceStage = modelEditControl1.TargetModel == null ? 0 :
                modelEditControl1.TargetAnimation == null ? 1 : 2;
            UpdateSimpleWorkspace();
        }

        private void ToggleSimpleWorkspaceFocus()
        {
            if (SimpleWorkspaceBusy) return;
            _workspaceTaskOpener = _workspaceInvoker ?? CurrentWorkspaceFocus() ?? _simpleWorkspaceFocus;
            _simpleWorkspaceFocused = !_simpleWorkspaceFocused;
            _simpleWorkspaceDrawer = !_simpleWorkspaceFocused;
            if (_simpleWorkspaceFocused && _libraryPane?.Visible == true)
                SetSimpleWorkspaceLibraryVisible(false);
            UpdateSimpleWorkspace();
            if (_simpleWorkspaceFocused) FocusWorkspaceTarget(modelEditControl1.ModelPanel);
            else FocusWorkspaceRegion(_simpleWorkspaceTask, remember: false);
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
                case "close-workflow":
                    _simpleWorkspaceDrawer = false;
                    _simpleWorkspaceFocused = true;
                    break;
                case "source":
                    _simpleWorkspaceFocused = false;
                    _simpleWorkspaceStage = 0;
                    SetSimpleWorkspaceLibraryVisible(true);
                    break;
                case "add-source":
                    _simpleWorkspaceFocused = false;
                    _simpleWorkspaceStage = 0;
                    SetSimpleWorkspaceLibraryVisible(true);
                    AddLibrarySource();
                    break;
                case "repair-source":
                    RepairLibrarySource();
                    break;
                case "continue":
                    ContinueSimpleWorkspace();
                    break;
                case "audition":
                    if (!SimpleWorkspaceSourceReady)
                        throw new InvalidOperationException("Select an available source animation with its verified original model.");
                    SetSimpleWorkspaceLibraryVisible(true);
                    PreviewLibraryOriginal();
                    break;
                case "target":
                    ChooseSimpleWorkspaceTarget();
                    break;
                case "open-project":
                    ChooseSimpleWorkspaceProject();
                    break;
                case "new-project":
                    MainForm.Instance.NewFighterProject();
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
                    // ToolStrip completes the clicked button's menu dismissal
                    // after Click. Open the retained overflow on the next UI turn.
                    BeginInvoke(new Action(() =>
                    {
                        if (!IsDisposed && !SimpleWorkspaceBusy)
                            _simpleWorkspaceToolbar.OverflowButton.ShowDropDown();
                    }));
                    break;
            }
            if (!IsDisposed) UpdateSimpleWorkspace();
        }

        private void ContinueSimpleWorkspace()
        {
            if (_simpleWorkspaceStage == 1)
            {
                if (modelEditControl1.TargetModel == null)
                    throw new InvalidOperationException("Open the target model before continuing.");
                SetSimpleWorkspaceStage(2);
                return;
            }
            var item = LibrarySelection;
            if (!SimpleWorkspaceSourceReady || item == null) return;
            modelEditControl1.StopAnim();
            ReleaseLibraryPreview();
            var target = modelEditControl1.TargetModel;
            var clip = modelEditControl1.TargetAnimation;
            int frame = modelEditControl1.CurrentFrame;
            bool verified = false;
            _workspaceBusy = true;
            UpdateSimpleWorkspace();
            try
            {
                // Explicit preparation only. Reuse the existing offline identity
                // verification; no source decoding or hashing is added to ticks.
                using (var original = LoadLibrarySource(item.Clip.Id))
                {
                    if (original == null) return;
                    if (LibrarySelection?.Clip.Id != item.Clip.Id ||
                        !ReferenceEquals(target, modelEditControl1.TargetModel) ||
                        !ReferenceEquals(clip, modelEditControl1.TargetAnimation) ||
                        modelEditControl1.CurrentFrame != frame)
                        throw new InvalidOperationException("The source or editable target changed. Review the current selection before continuing.");
                    verified = true;
                    _simpleWorkspaceSourceIssue = _simpleWorkspaceIssueClip = null;
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidOperationException ||
                error is UnauthorizedAccessException)
            {
                _simpleWorkspaceSourceIssue = error.Message;
                _simpleWorkspaceIssueClip = item.Clip.Id;
                RefreshLibrary(item.Clip.Id);
            }
            finally
            {
                _workspaceBusy = false;
                UpdateSimpleWorkspace();
                if (_controlReloadRequested) QueueControlSetup();
            }
            if (verified) SetSimpleWorkspaceStage(target == null ? 1 : 2);
        }

        private void ChooseSimpleWorkspaceTarget()
        {
            modelEditControl1.StopAnim();
            var project = _fighterProject;
            using (var choose = new WorkspaceFileDialog("Open Target Model",
                "Choose the existing model you want to animate. Original files remain unchanged.",
                "Brawl model resources (*.pac;*.pcs;*.brres;*.mdl0)|*.pac;*.pcs;*.brres;*.mdl0|All files (*.*)|*.*",
                "Open model"))
            {
                if (choose.ShowDialog(this) != DialogResult.OK) return;
                if (!ReferenceEquals(project, _fighterProject))
                    throw new InvalidOperationException("The project changed. Choose the target again.");
                ImportFighterModel(choose.SelectedPath);
                SetSimpleWorkspaceStage(1);
            }
        }

        private void ChooseSimpleWorkspaceProject()
        {
            modelEditControl1.StopAnim();
            using (var choose = new WorkspaceFileDialog("Open Fighter Project",
                "Resume a saved Fighterport project in the full native Model Previewer.",
                "Fighterport project (*.fpstudio)|*.fpstudio", "Open project", recent: WorkspaceRecentProjects.Current))
            {
                if (choose.ShowDialog(this) == DialogResult.OK)
                    MainForm.Instance.OpenFighterProject(choose.SelectedPath, this);
            }
        }

        private string SimpleWorkspaceReadiness(LibrarySearchItem source, bool hasTarget, CHR0Node clip, bool busy)
        {
            if (busy) return "Finish or cancel the current operation before continuing.";
            if (_simpleWorkspaceStage == 1)
                return hasTarget ? "Target ready. Continue to preview, set up controls or add native animations." :
                    "Open an existing target model. You can edit it without a donor animation.";
            if (_simpleWorkspaceStage == 3)
            {
                if (!hasTarget) return "Open the target model before exporting its animation.";
                if (clip == null) return "Select a target animation in the native list, or add one from Target.";
                if (SelectedFighterAsset()?.Retarget?.ReviewDraft == true)
                    return "This advanced review draft is blocked. Review its unresolved gates in More tools.";
                return "Export the selected native animation, or save the project to continue later.";
            }
            if (_simpleWorkspaceSourceIssue != null &&
                (_libraryReplacingTree || source?.Clip.Id == _simpleWorkspaceIssueClip))
                return _simpleWorkspaceSourceIssue;
            if (_libraryReplacingTree || _librarySearchTimer?.Enabled == true)
                return "Updating source selection. Wait for the current library results.";
            if (source == null)
                return "Choose an animation from Source library, or add an original model and motion set. Existing target animations remain editable.";
            switch (source.State)
            {
                case LibrarySourceState.Missing:
                    return "Original source is missing. Repair source with the exact indexed file.";
                case LibrarySourceState.SourceChanged:
                    return "Original source changed. Locate the exact indexed bytes with Repair source, or add a new source.";
                case LibrarySourceState.NeedsModelAssociation:
                    return "This motion needs its original model association. Add source and review the exact model/motion pair.";
                case LibrarySourceState.UnsupportedChannelVersion:
                    return "This source channel or native version is not supported for transfer. Choose a supported skeletal animation.";
            }
            return !hasTarget ? "Source ready. Continue to choose the target model." :
                _simpleWorkspaceStage == 0 ? "Source and target ready. Continue to preview the transfer." :
                    "Ready to preview transfer with original Next. Review the resulting motion before export.";
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
            if (_simpleWorkspaceTask == null || _simpleWorkspaceMoreTools == null || IsDisposed || Disposing) return;
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
                " · " + selectedSource.Motion.Game + "\n" + SimpleWorkspaceSourceStateText(selectedSource);
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
                    asset?.Retarget?.ReviewDraft != true, busy,
                SimpleWorkspaceReadiness(selectedSource, hasTarget, clip, busy),
                SimpleWorkspaceSourceNeedsRepair(selectedSource));
            for (int i = 0; i < _simpleWorkspaceStages.Length; i++)
            {
                _simpleWorkspaceStages[i].Checked = i == _simpleWorkspaceStage;
                _simpleWorkspaceStages[i].Enabled = !busy;
            }
            _simpleWorkspaceSummary.Text = (sourceReady ? selectedSource.Clip.Name :
                savedSource == null ? "Choose a source" : "Saved source: " + savedSourceName) +
                "  →  " + (hasTarget ? targetModel.Name : "Choose a target");
            _simpleWorkspaceLibrary.Checked = _libraryPane?.Visible == true;
            _simpleWorkspaceWorkflow.Enabled = !busy;
            _simpleWorkspaceOptions.Enabled = !busy;
            _simpleWorkspaceFocus.Checked = _simpleWorkspaceFocused;
            _simpleWorkspaceFocus.Text = _simpleWorkspaceFocused ? "Show workflow" : "Focus editor";
            _simpleWorkspaceNewProject.Enabled = _simpleWorkspaceOpenProject.Enabled =
                _simpleWorkspaceMoreTools.Enabled = _simpleWorkspaceLibrary.Enabled = !busy;
            LayoutSimpleWorkspace();
        }

        private void LayoutSimpleWorkspace()
        {
            if (_simpleWorkspaceBody == null || _simpleWorkspaceLayingOut) return;
            _simpleWorkspaceLayingOut = true;
            try
            {
                LayoutWorkspaceBars();
                int width=_simpleWorkspaceBody.ClientSize.Width;
                int height=_simpleWorkspaceBody.ClientSize.Height;
                bool libraryVisible = _libraryPane?.Visible == true;
                int libraryWidth=Math.Min(WorkspaceAppearance.Pixels(340),Math.Max(280,width-120));
                bool libraryOverlay=libraryVisible && width-libraryWidth<NativeWorkspaceMinimumWidth;
                if(_libraryPane!=null)
                {
                    _libraryPane.Dock=libraryOverlay?DockStyle.None:DockStyle.Left;
                    _libraryPane.Width=libraryWidth;
                    if(libraryOverlay)_libraryPane.Bounds=new Rectangle(0,0,libraryWidth,height);
                }
                int taskWidth=Math.Min(WorkspaceAppearance.Pixels(270),Math.Max(240,(width-120)/2));
                bool fits=!libraryOverlay && width-(libraryVisible?libraryWidth:0)-taskWidth>=NativeWorkspaceMinimumWidth;
                bool showTask=!_simpleWorkspaceFocused && (fits || _simpleWorkspaceDrawer && !libraryVisible);
                _simpleWorkspaceTask.Visible=showTask;
                _simpleWorkspaceTask.Dock=fits?DockStyle.Right:DockStyle.None;
                _simpleWorkspaceTask.Width=taskWidth;
                if(!fits)_simpleWorkspaceTask.Bounds=new Rectangle(width-taskWidth,0,taskWidth,height);
                modelEditControl1.BringToFront();
                LayoutSimpleWorkspaceLibraryPreview();
                _simpleWorkspaceBody.PerformLayout();
                if(libraryOverlay)_libraryPane.BringToFront();
                if(showTask && !fits)_simpleWorkspaceTask.BringToFront();
                _simpleWorkspaceWorkflow.Checked=showTask;
                _simpleWorkspaceWorkflow.ToolTipText=showTask?"Hide workflow tasks to make room for the native editor":"Show the current task and next action";
                _simpleWorkspaceWorkflow.ToolTipText += ". F6 moves between regions; Escape closes tasks when they have focus.";
            }
            finally { _simpleWorkspaceLayingOut = false; }
        }

        private void LayoutSimpleWorkspaceLibraryPreview()
        {
            if (_libraryPane == null) return;
            var layout = _libraryPane.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
            if (layout == null) return;
            ApplyWorkspaceLibraryTextSize(layout);
            bool preview = _libraryLease != null;
            layout.RowStyles[4].Height = WorkspaceAppearance.Pixels(_libraryPane.Height < 650 ? 48 : 70);
            layout.RowStyles[6].Height = preview ? WorkspaceAppearance.Pixels(24) : 0;
            layout.RowStyles[7].Height = preview ? (_libraryPane.Height < 650 ? 120 : 160) : 0;
            layout.RowStyles[8].Height = preview ? WorkspaceAppearance.Pixels(38) : 0;
            int required=(int)layout.RowStyles.Cast<RowStyle>().Where(r=>r.SizeType==SizeType.Absolute).Sum(r=>r.Height)+160;
            layout.Height=Math.Max(_libraryPane.ClientSize.Height-_libraryPane.Padding.Vertical,required);

            for (int row = 6; row < 9; row++)
            {
                var control = layout.GetControlFromPosition(0, row);
                if (control != null) control.Visible = preview;
            }
        }
    }
}
