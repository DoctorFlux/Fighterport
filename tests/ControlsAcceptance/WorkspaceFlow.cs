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
    // First-use/resume handlers run on the actual private native desktop. The
    // harness types paths into application-owned dialogs; it never drives OS input.
    static async Task WorkspaceFlow(string[] args, string output)
    {
        if (!PrivateDesktop || !args.Contains("--retarget-source"))
            throw new InvalidOperationException("Workspace flow acceptance requires the private desktop and an authentic donor model.");
        string donor = args[Array.IndexOf(args, "--retarget-source") + 1];
        string clipName = args[Array.IndexOf(args, "--clip") + 1];
        var originals = new[] { args[0], args[1], donor }.Distinct().ToDictionary(p => p, FighterProject.HashFile);
        string copies = Path.Combine(output, "registered-originals");
        Directory.CreateDirectory(copies);
        string donorCopy = Path.Combine(copies, "source-model" + Path.GetExtension(donor));
        string motionCopy = Path.Combine(copies, "source-motion" + Path.GetExtension(args[1]));
        string alternateMotion = Path.Combine(copies, "alternate-motion" + Path.GetExtension(args[1]));
        string alternateModel = Path.Combine(copies, "alternate-model" + Path.GetExtension(args[0]));
        File.Copy(donor, donorCopy);
        File.Copy(args[1], motionCopy);
        File.Copy(args[1], alternateMotion);
        File.Copy(args[0], alternateModel);
        string priorLibrary = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "isolated-library"));
        ModelForm form = null;
        try
        {
            using (var startup = MainForm.StartViewerApplication())
            {
                form = (ModelForm)Field(MainForm.Instance, "_fighterForm");
                form.WindowState = FormWindowState.Normal;
                form.ClientSize = new Size(1600, 950);
                var editor = form.FighterEditor;
                Check(editor.TargetModel == null && FlowStage(form, "Source"),
                    "First-use workspace begins at Source without inventing a target or donor");
                Check(!(bool)Field(form, "_fighterDirty"),
                    "Untouched first-use startup keeps the empty native project clean");
                var originalViewport = editor.ModelPanel;
                form.ClientSize = new Size(1320, 850);
                form.PerformLayout();
                await Task.Delay(100);
                Check(new[] { "workspaceMoreTools", "workspaceOpenProject", "workspaceNewProject" }
                    .Select(name => SimpleToolbarItem(form, name)).All(item => item.Available && item.Visible &&
                        item.Placement == ToolStripItemPlacement.Main && item.Owner.ClientRectangle.Contains(item.Bounds)) &&
                    ReferenceEquals(form.FighterEditor, editor) && ReferenceEquals(editor.ModelPanel, originalViewport),
                    "At 1320 width More tools, Open project and New project stay visible with the same complete native editor");
                await CaptureSimpleWorkspace(form, Path.Combine(output, "flow-toolbar-1320.png"));
                form.ClientSize = new Size(1600, 950);
                form.PerformLayout();
                var more = SimpleToolbarItem(form, "workspaceMoreTools");
                Check(more.Available && more.Owner.Visible && more.Owner.ClientRectangle.Contains(more.Bounds),
                    "More tools is a visible named top-toolbar action");
                more.PerformClick();
                var toolbar = (ToolStrip)Field(form, "_simpleWorkspaceToolbar");
                var overflow = toolbar.OverflowButton.DropDown;
                await Wait(() => overflow.Visible);
                var originalControlsMenu = toolbar.Items.Cast<ToolStripItem>().Single(i => i.Name == "fighterAnimationControls");
                Check(overflow.Visible && originalControlsMenu.Placement == ToolStripItemPlacement.Overflow &&
                    ReferenceEquals(originalControlsMenu.GetCurrentParent(), overflow),
                    "More tools opens the actual retained native expert commands without proxy items");
                await CaptureCheckpoint();
                using (var image = new Bitmap(overflow.Width, overflow.Height))
                {
                    overflow.DrawToBitmap(image, new Rectangle(Point.Empty, overflow.Size));
                    image.Save(Path.Combine(output, "flow-more-tools.png"));
                }
                overflow.Close();

                bool registrationSeen = false;
                using (FlowModal("libraryModelFile", dialog =>
                {
                    registrationSeen = true;
                    var modelFile = SimpleControl<ComboBox>(dialog, "libraryModelFile");
                    var motionFile = SimpleControl<ComboBox>(dialog, "libraryMotionFile");
                    var modelList = SimpleControl<ComboBox>(dialog, "librarySourceModel");
                    var approved = SimpleControl<CheckBox>(dialog, "libraryApproveAssociation");
                    var register = SimpleControl<Button>(dialog, "libraryRegisterSource");
                    FlowPathChoice(modelFile, donorCopy);
                    FlowPathChoice(motionFile, motionCopy);
                    Check(modelList.SelectedIndex >= 0 && modelList.Items.Count > 0 && !approved.Checked,
                        "The real Add Source dialog inspects the original model and starts with an unapproved association");
                    approved.Checked = true;
                    FlowPathChoice(motionFile, alternateMotion);
                    Check(!approved.Checked, "Changing the chosen motion package resets source ownership approval");
                    FlowPathChoice(motionFile, motionCopy);
                    approved.Checked = true;
                    FlowPathChoice(modelFile, Path.Combine(copies, "missing-model.pac"));
                    Check(modelList.Items.Count == 0 && modelList.SelectedIndex == -1 && !approved.Checked && !register.Enabled &&
                        !string.IsNullOrWhiteSpace(SimpleControl<Label>(dialog, "librarySourceValidation").Text),
                        "An unavailable model selection clears stale inspected identity and approval with an inline explanation");
                    FlowPathChoice(modelFile, donorCopy);
                    SimpleControl<TextBox>(dialog, "libraryGameVersion").Text = "Local supplied fixture; game build unverified";
                    Check(!approved.Checked && register.Text.Contains("review"),
                        "Unapproved source indexing is visibly distinguished from reviewed association");
                    approved.Checked = true;
                    Check(register.Enabled, "Explicit source association approval enables real motion-set registration");
                    CaptureDialog(dialog, output, "flow-register-source.png");
                    register.PerformClick();
                })) SimpleControl<Button>(form, "workspaceAddSourceAction").PerformClick();
                Check(registrationSeen, "Workspace Add source opens the actual whole-character registration dialog");
                var library = (AnimationLibrary)Field(form, "_library");
                await Wait(() => !(bool)Field(form, "_libraryReplacingTree") && ((TreeView)Field(form, "_libraryList")).SelectedNode?.Tag is LibrarySearchItem);
                Check(library.ClipCount > 0 && ((LibrarySearchItem)((TreeView)Field(form, "_libraryList")).SelectedNode.Tag).State == LibrarySourceState.Available,
                    "Actual registration publishes the catalogue and automatically selects an available source clip");
                var firstApproved = (LibrarySearchItem)((TreeView)Field(form, "_libraryList")).SelectedNode.Tag;
                int approvedCount = library.ClipCount;
                long approvedGeneration = library.Read().Generation;
                bool repeatSeen = FlowRegisterSource(form, donorCopy, motionCopy, true);
                await FlowLibrarySettled(form);
                var repeatSelected = (LibrarySearchItem)((TreeView)Field(form, "_libraryList")).SelectedNode.Tag;
                Check(repeatSeen && library.ClipCount == approvedCount && library.Read().Generation == approvedGeneration &&
                    repeatSelected.Clip.Id == firstApproved.Clip.Id && repeatSelected.Model.Id == firstApproved.Model.Id,
                    "Repeating actual approved registration preserves catalogue generation, clip count and exact committed source selection");
                SimpleControl<Button>(form, "workspacePrimaryAction").PerformClick();
                Check(FlowStage(form, "Target") && editor.TargetModel == null,
                    "Continue still validates and advances the source after duplicate registration");
                SimpleToolbarItem(form, "workspaceStageSource").PerformClick();
                bool unapprovedSeen = FlowRegisterSource(form, alternateModel, motionCopy, false);
                await FlowLibrarySettled(form);
                var unapproved = (LibrarySearchItem)((TreeView)Field(form, "_libraryList")).SelectedNode.Tag;
                Check(unapprovedSeen && unapproved.Model == null && unapproved.State == LibrarySourceState.NeedsModelAssociation &&
                    (string)SimpleControl<Button>(form, "workspacePrimaryAction").Tag != "continue" &&
                    FlowReadiness(form).IndexOf("association", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Unapproved registration of the same motions with another model selects its own unresolved entry and blocks Continue");
                var preservedApproved = library.Browse("").Single(item => item.Clip.Id == firstApproved.Clip.Id);
                Check(preservedApproved.Model.Id == firstApproved.Model.Id && preservedApproved.State == LibrarySourceState.Available &&
                    preservedApproved.Clip.Id != unapproved.Clip.Id,
                    "Unapproved alternate-model indexing leaves the previous approved source identity distinct and available");
                var selected = await FlowSelectSource(form, library, clipName);
                Check(library.Read().Models.Single(m => m.Id == selected.Model.Id).MotionMountIds.Contains(selected.Motion.Id),
                    "Registered motion-set entries retain the approved original model association");
                var sourceContinue = SimpleControl<Button>(form, "workspacePrimaryAction");
                Check((string)sourceContinue.Tag == "continue" && sourceContinue.Text.Contains("target"),
                    "A verified source offers Continue to target without hidden commands");
                sourceContinue.PerformClick();
                Check(FlowStage(form, "Target") && editor.TargetModel == null,
                    "Continue validates the original source and reaches Target without changing the editable document");

                bool cancelled = false;
                using (FlowModal("workspaceFilePath", dialog =>
                {
                    SimpleControl<TextBox>(dialog, "workspaceFilePath").Text = Path.Combine(output, "absent-target.pac");
                    SimpleControl<Button>(dialog, "workspaceFileAccept").PerformClick();
                    Check(dialog.Visible && !string.IsNullOrWhiteSpace(SimpleControl<Label>(dialog, "workspaceFileValidation").Text) && editor.TargetModel == null,
                        "Target path validation remains in the actual dialog and leaves the target unchanged");
                    CaptureDialog(dialog, output, "flow-target-invalid.png");
                    cancelled = true;
                    SimpleControl<Button>(dialog, "workspaceFileCancel").PerformClick();
                })) SimpleControl<Button>(form, "workspacePrimaryAction").PerformClick();
                Check(cancelled && editor.TargetModel == null && form.FighterProject.Animations.Count == 0 && FlowStage(form, "Target"),
                    "Cancelling target file choice preserves the existing source stage and empty target document");
                using (FlowModal("workspaceFilePath", dialog => FlowAcceptFile(dialog, args[0])))
                    SimpleControl<Button>(form, "workspacePrimaryAction").PerformClick();
                await SimpleControlsReady(form);
                var target = editor.TargetModel as MDL0Node;
                Check(target != null && editor.SelectedCHR0 == null && form.FighterProject.Animations.Count == 0,
                    "The real target-open action imports the selected native model without requiring a donor clip on the target");
                string modelHash = (string)Call(form, "NativeControlHash", target, ".mdl0");
                Check((string)SimpleControl<Button>(form, "workspacePrimaryAction").Tag == "continue",
                    "An open target offers Continue to preview instead of reopening the target chooser");
                string modelOnly = Path.Combine(output, "flow-model-only.fpstudio");
                target.Export(Path.Combine(output, "flow-model-before-save.mdl0"));
                form.SaveFighterProject(modelOnly);
                var prior = form;
                using (FlowModal("workspaceFilePath", dialog => FlowAcceptFile(dialog, modelOnly)))
                    SimpleToolbarItem(form, "workspaceOpenProject").PerformClick();
                await Wait(() => prior.IsDisposed);
                form = (ModelForm)Field(MainForm.Instance, "_fighterForm");
                form.WindowState = FormWindowState.Normal;
                form.ClientSize = new Size(1600, 950);
                editor = form.FighterEditor;
                // A form opened by the real project command raises OnShown on
                // the next message turn. Wait for actual native scene restoration.
                await Wait(() => editor.TargetModel != null && FlowStage(form, "Target"));
                await SimpleControlsReady(form);
                target = (MDL0Node)editor.TargetModel;
                target.Export(Path.Combine(output, "flow-model-after-reopen.mdl0"));
                string reopenedModelHash = (string)Call(form, "NativeControlHash", target, ".mdl0");
                var reopenDiagnostic = new
                {
                    PriorFormIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(prior),
                    NewFormIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(form),
                    SameForm = ReferenceEquals(form, prior), PriorDisposed = prior.IsDisposed,
                    TargetStage = FlowStage(form, "Target"), Stage = Field(form, "_simpleWorkspaceStage"),
                    SelectedCHR0 = editor.SelectedCHR0?.Name, TargetAnimation = editor.TargetAnimation?.Name,
                    AnimationType = editor.TargetAnimType.ToString(), Animations = form.FighterProject.Animations.Count,
                    TargetModel = target.Name, TargetDirty = target.IsDirty,
                    BeforeNativeControlHash = modelHash, AfterNativeControlHash = reopenedModelHash
                };
                string reopenJson = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(reopenDiagnostic);
                Console.WriteLine("TRACE actual model-only reopen " + reopenJson);
                File.WriteAllText(Path.Combine(output, "flow-model-reopen-diagnostic.json"), reopenJson);
                Check(!ReferenceEquals(form, prior) && FlowStage(form, "Target") && editor.SelectedCHR0 == null && form.FighterProject.Animations.Count == 0 &&
                    reopenedModelHash == modelHash,
                    "Actual Open project restores the exact model-only document and resumes at Target");
                editor.FrameFighterModel();
                await CaptureSimpleWorkspace(form, Path.Combine(output, "flow-model-only-resume.png"));

                SimpleToolbarItem(form, "workspaceStageSource").PerformClick();
                SimpleToolbarItem(form, "workspaceSourceLibrary").PerformClick();
                library = (AnimationLibrary)Field(form, "_library");
                selected = await FlowSelectSource(form, library, clipName);
                SimpleControl<Button>(form, "workspaceAuditionAction").PerformClick();
                var sourceFrame = (TrackBar)Field(form, "_libraryFrame");
                sourceFrame.Value = Math.Min(8, sourceFrame.Maximum);
                Check(ReferenceEquals(editor.TargetModel, target) && editor.SelectedCHR0 == null && form.FighterProject.Animations.Count == 0 &&
                    (string)Call(form, "NativeControlHash", target, ".mdl0") == modelHash,
                    "Read-only source audition leaves the reopened native target and all target model bytes unchanged");
                await CaptureSimpleWorkspace(form, Path.Combine(output, "flow-source-audition.png"));
                SimpleControl<Button>(form, "workspacePrimaryAction").PerformClick();
                Check(FlowStage(form, "Preview") && ReferenceEquals(editor.TargetModel, target),
                    "Source Continue with an existing target reaches Preview while retaining target ownership");

                // Fail only isolated original copies. The supplied game files are read-only.
                string missingReplacement = Path.Combine(copies, "relocated-motion" + Path.GetExtension(args[1]));
                File.Move(motionCopy, missingReplacement);
                SimpleToolbarItem(form, "workspaceStageSource").PerformClick();
                Check((string)SimpleControl<Button>(form, "workspacePrimaryAction").Tag == "continue",
                    "Missing-source test begins with a cached available selection, before explicit revalidation");
                long generation = library.Read().Generation;
                SimpleControl<Button>(form, "workspacePrimaryAction").PerformClick();
                await FlowLibrarySettled(form);
                Check(FlowStage(form, "Source") && (string)SimpleControl<Button>(form, "workspacePrimaryAction").Tag != "continue" &&
                    FlowReadiness(form).IndexOf("Missing", StringComparison.OrdinalIgnoreCase) >= 0 && library.Read().Generation == generation,
                    "Continue revalidates a stale selection, explains its missing original and preserves the catalogue");
                Check(ReferenceEquals(editor.TargetModel, target) && editor.SelectedCHR0 == null,
                    "Unavailable original source leaves the accepted model-only target unchanged");
                await CaptureSimpleWorkspace(form, Path.Combine(output, "flow-missing-original.png"));

                string wrong = Path.Combine(copies, "wrong-original.pac");
                File.WriteAllText(wrong, "Original acceptance fixture with deliberately different bytes.");
                await FlowRepairSource(form, library, selected, wrong, missingReplacement, output);
                selected = await FlowSelectSource(form, library, clipName);
                Check(selected.State == LibrarySourceState.Available && selected.Clip.Id == library.Read().Clips.Single(c => c.Id == selected.Clip.Id).Id,
                    "Exact-byte repair restores the same immutable clip identity and source readiness");

                // Preserve size and timestamp to prove explicit Continue checks bytes,
                // beyond the cheap browse metadata used for interactive search.
                var repairedMount = library.Read().Mounts.Single(m => m.Id == selected.Motion.Id);
                string repairedPath = repairedMount.LocalPath;
                DateTime repairedStamp = File.GetLastWriteTimeUtc(repairedPath);
                byte[] changed = File.ReadAllBytes(repairedPath);
                changed[changed.Length - 1] ^= 1;
                File.WriteAllBytes(repairedPath, changed);
                File.SetLastWriteTimeUtc(repairedPath, repairedStamp);
                Check(library.Search(clipName, 0, out _).Single(i => i.Clip.Id == selected.Clip.Id).State == LibrarySourceState.Available,
                    "Changed-byte test preserves browse metadata so its cached selection remains apparently available");
                SimpleControl<Button>(form, "workspacePrimaryAction").PerformClick();
                await FlowLibrarySettled(form);
                Check(FlowStage(form, "Source") && FlowReadiness(form).IndexOf("changed", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (string)SimpleControl<Button>(form, "workspacePrimaryAction").Tag != "continue" &&
                    SimpleControl<Button>(form, "workspaceRepairSourceAction").Visible &&
                    form.FighterProject.Animations.Count == 0 && ReferenceEquals(editor.TargetModel, target),
                    "Explicit Continue rejects changed original bytes even when size and timestamp still match");
                string exactAgain = Path.Combine(copies, "verified-motion" + Path.GetExtension(args[1]));
                File.Copy(args[1], exactAgain);
                await FlowRepairSource(form, library, selected, null, exactAgain, output);
                selected = await FlowSelectSource(form, library, clipName);
                SimpleControl<Button>(form, "workspacePrimaryAction").PerformClick();
                Check(FlowStage(form, "Preview") && ReferenceEquals(editor.TargetModel, target),
                    "Repaired source Continue returns to Preview on the same target document");
                SimpleToolbarItem(form, "workspaceStageTarget").PerformClick();
                SimpleControl<Button>(form, "workspacePrimaryAction").PerformClick();
                Check(FlowStage(form, "Preview"), "Target Continue returns to Preview through the visible primary action");

                bool solverSeen = false;
                using (OnRetargetReady(form, dialog => solverSeen = true))
                    SimpleControl<Button>(form, "workspacePrimaryAction").PerformClick();
                await SimpleControlsReady(form);
                var project = form.FighterProject;
                var clip = editor.SelectedCHR0;
                Check(solverSeen && project.Animations.Count == 1 && clip != null &&
                    project.Animations.Single().Retarget.Algorithm == HumanoidRetargeter.Algorithm &&
                    project.Animations.Single().Retarget.Library.ClipId == selected.Clip.Id,
                    "The first-use registration and repair flow reaches original Next v7 with exact source provenance");
                var rig = BrawlRetargeter.Capture(target);
                var body = rig.Bones[rig.Rig.Roles["pelvis"]];
                editor.SetFrame(1);
                editor.SelectedBone = body;
                float angle = body.FrameState._rotate._x;
                var field = editor.CHR0Editor._transBoxes[3];
                field.Text = (angle + 2).ToString(System.Globalization.CultureInfo.CurrentCulture);
                Call(field, "Apply");
                await Wait(() => Field(form, "_pinGesture") == null);
                Check(Math.Abs(body.FrameState._rotate._x - angle - 2) < .0002f,
                    "A retained native body transform field authors a measured correction on the selected target result");
                editor.Undo();
                await SimpleControlsReady(form);
                Check(Math.Abs(body.FrameState._rotate._x - angle) < .0002f, "Native Undo restores the task-flow body correction");
                editor.Redo();
                await SimpleControlsReady(form);
                Check(Math.Abs(body.FrameState._rotate._x - angle - 2) < .0002f, "Native Redo restores the task-flow body correction");
                editor.FrameFighterModel();
                await CaptureSimpleWorkspace(form, Path.Combine(output, "flow-native-result.png"));
                string export = Path.Combine(output, "flow-result.chr0");
                SimpleToolbarItem(form, "workspaceStageExport").PerformClick();
                Call(form, "ExportSimpleWorkspaceResult", export);
                using (var fresh = NodeFactory.FromFile(null, export) as CHR0Node)
                    Check(NativeDifference(clip, fresh) == null, "Task-flow native export freshly decodes with every authored key, tangent and transform flag unchanged");
                string finalProject = Path.Combine(output, "flow-result.fpstudio");
                form.SaveFighterProject(finalProject);
                using (var fresh = FighterProject.Open(finalProject))
                    Check(fresh.Warnings.Count == 0 && fresh.Animations.Single().Retarget.Library.ClipId == selected.Clip.Id,
                        "The completed task-flow project freshly resolves its protected target and authentic source identity");
                Check((string)Call(form, "NativeControlHash", target, ".mdl0") == modelHash && originals.All(p => FighterProject.HashFile(p.Key) == p.Value),
                    "Task-flow registration, repair, transfer and native edits preserve all supplied originals and every target model byte");
                File.WriteAllText(Path.Combine(output, "flow-source-hashes.json"),
                    new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(originals));
                File.WriteAllText(Path.Combine(output, "flow-task-scope.txt"),
                    "Programmatic native handler/task flow only. Application-owned path dialogs, actual registration/repair, Continue, native edit/Undo and shared export exercised. Physical input, common OS Browse/Save dialogs, DPI, task timings and first-time human usability are not qualified.");
                Call(form, "DisposeAnimationLibrary");
                SimpleDisposeForm(form);
                project.Dispose();
                form = null;
            }
        }
        finally { Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", priorLibrary); }
    }

    static bool FlowStage(ModelForm form, string stage) => ((ToolStripButton)SimpleToolbarItem(form, "workspaceStage" + stage)).Checked;
    static string FlowReadiness(ModelForm form) => SimpleControl<Label>(form, "workspaceReadiness").Text;
    static void FlowPathChoice(ComboBox box, string path)
    {
        if (!box.Items.Contains(path)) box.Items.Add(path);
        box.SelectedItem = path;
    }
    static void FlowAcceptFile(Form dialog, string path)
    {
        SimpleControl<TextBox>(dialog, "workspaceFilePath").Text = path;
        SimpleControl<Button>(dialog, "workspaceFileAccept").PerformClick();
    }
    static bool FlowRegisterSource(ModelForm form, string modelPath, string motionPath, bool approved)
    {
        bool seen = false;
        using (FlowModal("libraryModelFile", dialog =>
        {
            seen = true;
            FlowPathChoice(SimpleControl<ComboBox>(dialog, "libraryModelFile"), modelPath);
            FlowPathChoice(SimpleControl<ComboBox>(dialog, "libraryMotionFile"), motionPath);
            SimpleControl<TextBox>(dialog, "libraryGameVersion").Text = "Local supplied fixture; game build unverified";
            SimpleControl<CheckBox>(dialog, "libraryApproveAssociation").Checked = approved;
            SimpleControl<Button>(dialog, "libraryRegisterSource").PerformClick();
        })) SimpleControl<Button>(form, "workspaceAddSourceAction").PerformClick();
        return seen;
    }
    sealed class ModalAssertionTimer : Timer
    {
        internal Exception Failure;
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if(disposing && Failure!=null)throw new InvalidOperationException("Modal acceptance failed",Failure);
        }
    }
    static Timer FlowModal(string childName, Action<Form> action)
    {
        var timer = new ModalAssertionTimer { Interval = 60 };
        timer.Tick += (sender, args) =>
        {
            var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Visible && f.Controls.Find(childName, true).Length != 0);
            if (dialog == null) return;
            timer.Stop();
            try { action(dialog); }
            catch(Exception error) { timer.Failure=error;dialog.DialogResult=DialogResult.Cancel;dialog.Close(); }
        };
        timer.Start();
        return timer;
    }
    static Task FlowLibrarySettled(ModelForm form) => Wait(() => !(bool)Field(form, "_libraryReplacingTree") &&
        !((Timer)Field(form, "_librarySearchTimer")).Enabled &&
        ((TreeView)Field(form, "_libraryList")).SelectedNode?.Tag is LibrarySearchItem);
    static async Task<LibrarySearchItem> FlowSelectSource(ModelForm form, AnimationLibrary library, string clipName)
    {
        var item = library.Search(clipName, 0, out _).Single(i => i.Clip.Name == clipName && i.Model != null);
        var search = (TextBox)Field(form, "_librarySearch");
        search.Text = search.Text == clipName ? "" : clipName;
        if (search.Text != clipName) search.Text = clipName;
        var tree = (TreeView)Field(form, "_libraryList");
        await Wait(() => !(bool)Field(form, "_libraryReplacingTree") && !((Timer)Field(form, "_librarySearchTimer")).Enabled &&
            AllNodes(tree.Nodes).Any(n => (n.Tag as LibrarySearchItem)?.Clip.Id == item.Clip.Id));
        tree.SelectedNode = AllNodes(tree.Nodes).Single(n => (n.Tag as LibrarySearchItem)?.Clip.Id == item.Clip.Id);
        return item;
    }
    static async Task FlowRepairSource(ModelForm form, AnimationLibrary library, LibrarySearchItem item, string wrong, string exact, string output)
    {
        string before = FighterProject.HashFile(library.IndexPath);
        if (wrong != null)
        {
            bool cancelled = false;
            using (var cancellation = new System.Threading.CancellationTokenSource())
            {
                cancellation.Cancel();
                try { library.Repair(item.Clip.MountId, exact, cancellation.Token); }
                catch (OperationCanceledException) { cancelled = true; }
            }
            Check(cancelled && FighterProject.HashFile(library.IndexPath) == before,
                "A pre-cancelled exact-source repair leaves the catalogue and original association unchanged");
        }
        bool repairSeen = false;
        using (FlowModal("libraryRepairPath", dialog =>
        {
            repairSeen = true;
            var choices = SimpleControl<ComboBox>(dialog, "libraryRepairMount");
            var data = library.Read();
            var mount = data.Mounts.Single(m => m.Id == item.Clip.MountId);
            int index = choices.Items.Cast<object>().ToList().FindIndex(v => v.ToString().Contains(Path.GetFileName(mount.LocalPath)));
            if (index < 0) throw new InvalidOperationException("Actual repair dialog omitted the selected motion source.");
            choices.SelectedIndex = index;
            var path = SimpleControl<TextBox>(dialog, "libraryRepairPath");
            var apply = SimpleControl<Button>(dialog, "libraryRepairApply");
            if (wrong != null)
            {
                path.Text = wrong;
                apply.PerformClick();
                Check(dialog.Visible && !string.IsNullOrWhiteSpace(SimpleControl<Label>(dialog, "libraryRepairValidation").Text) && FighterProject.HashFile(library.IndexPath) == before,
                    "Actual repair rejects different bytes inline and leaves source association/catalogue generation unchanged");
                CaptureDialog(dialog, output, "flow-repair-wrong-bytes.png");
            }
            path.Text = exact;
            apply.PerformClick();
        })) SimpleControl<Button>(form, "workspaceRepairSourceAction").PerformClick();
        Check(repairSeen, "Workspace Repair opens the actual exact-source repair dialog");
        await FlowLibrarySettled(form);
        var repaired = library.Read().Mounts.Single(m => m.Id == item.Clip.MountId);
        Check(repaired.LocalPath == exact && repaired.ContainerSha256 == FighterProject.HashFile(exact),
            "Actual repair accepts only exact indexed bytes and records their new ordinary source path");
    }
}
