using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    // Frozen independently of the presentation implementation: the normal
    // workspace has one outer toolbar and no stage/task shell. The library is
    // on the right; its original-source native viewport is read-only, 120..240
    // pixels high at 100% text, and at least 120 pixels at 175%. Compact layouts
    // may use a right overlay and scroll the browser, while the complete source
    // viewport and Play control remain inside the pane at all text sizes.
    // Never replace the target editor or shrink its viewport below 250x200. Navigation changes
    // no exact native content, selected target/clip/bone/frame, Undo or approval.
    static IEnumerable<ToolStripItem> NextItems(ToolStripItem item)
    {
        yield return item;
        if (item is ToolStripDropDownItem menu)
            foreach (ToolStripItem child in menu.DropDownItems)
                foreach (var nested in NextItems(child)) yield return nested;
    }

    static ToolStripItem NextItem(ModelForm form, string name) => SimpleDescendants(form)
        .OfType<ToolStrip>().SelectMany(s => s.Items.Cast<ToolStripItem>())
        .SelectMany(NextItems).Distinct().Single(item => item.Name == name);

    static string NextLibraryFiles(AnimationLibrary library) => !Directory.Exists(library.DirectoryPath) ? "" :
        string.Join("\n", Directory.GetFiles(library.DirectoryPath, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => p.Substring(library.DirectoryPath.Length) + ":" + FighterProject.HashFile(p)));

    static async Task WorkspaceNextLayout(string[] args, string output)
    {
        if (!PrivateDesktop || !args.Contains("--retarget-source"))
            throw new InvalidOperationException("Next-layout acceptance requires the private desktop and an authentic donor model.");
        string donor = args[Array.IndexOf(args, "--retarget-source") + 1];
        string clipName = args.Contains("--clip") ? args[Array.IndexOf(args, "--clip") + 1] : "Wait1";
        string previous = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "library"));
        var originals = new[] { args[0], args[1], donor }.Distinct().ToDictionary(p => p, FighterProject.HashFile);
        string sourceDirectory = Path.Combine(output, "temporary-originals"); Directory.CreateDirectory(sourceDirectory);
        string sourceModel = Path.Combine(sourceDirectory, "source-model" + Path.GetExtension(donor));
        string sourceMotion = Path.Combine(sourceDirectory, "source-motion" + Path.GetExtension(args[1]));
        File.Copy(donor, sourceModel); File.Copy(args[1], sourceMotion);
        const string buildEvidence = "Distinct supplied source evidence: local native pair; game membership and build unverified.";
        var layouts = new List<object>();
        ModelForm form = null;
        try
        {
            using (var startup = MainForm.StartViewerApplication())
            {
                form = (ModelForm)Field(MainForm.Instance, "_fighterForm");
                form.WindowState = FormWindowState.Normal; form.ClientSize = new Size(1700, 950);
                await Task.Delay(100); await SimpleControlsReady(form);
                var editor = form.FighterEditor; var viewport = editor.ModelPanel;
                var left = editor.leftPanel; var right = editor.rightPanel;
                var transforms = editor.CHR0Editor; var playback = editor.pnlPlayback;
                Check(editor.TargetModel == null && !(bool)Field(form, "_fighterDirty"),
                    "Next-style startup opens a clean native editor without a fabricated target or donor");
                int originalText = WorkspaceAppearance.TextPercent; bool rejectedText = false;
                try { WorkspaceAppearance.SetTextPercent(200); }
                catch (ArgumentOutOfRangeException) { rejectedText = true; }
                Check(rejectedText && WorkspaceAppearance.TextPercent == originalText &&
                    WorkspaceAppearance.NormalizeTextPercent(200) == 100 && WorkspaceAppearance.NormalizeTextPercent(175) == 175 &&
                    WorkspaceAppearance.TextSizes.SequenceEqual(new[] { 100, 125, 150, 175 }) &&
                    !NextItems(NextItem(form, "nextViewMenu")).Any(item => item.Name == "nextText200"),
                    "Text presets stop at 175%; saved 200% falls back to 100% and new 200% requests are rejected without applying a layout");
                Check(!((Control)Field(form, "_simpleWorkspaceActions")).Visible &&
                    !((Control)Field(form, "_simpleWorkspaceNavigation")).Visible &&
                    !((Control)Field(form, "_simpleWorkspaceTask")).Visible,
                    "Normal startup has no stacked workspace actions, stages or task drawer");
                Check(new[] { "fighterOpenModel", "fighterAddAnimations", "fighterRetarget", "fighterLibrary", "fighterTransitionFit" }
                    .All(name => { var item = NextItem(form, name); return item.Available && item.Overflow != ToolStripItemOverflow.Always; }),
                    "Original direct model, animation, Next-retarget, library and fitting toolbar commands are retained");
                Check(new[] { "nextAnimationMenu", "nextViewMenu", "nextReviewResults", "nextCompareSource", "nextLoopInspection", "nextExportReviewedSet", "nextFocusEditor", "nextWorkflowGuide", "nextLoadSourceFiles", "nextSharedLibrary", "nextAddSourceToLibrary" }
                    .All(name => NextItem(form, name).Available),
                    "Review, linked comparison, loop inspection, verified set export and view recovery have named native menu commands");
                await CaptureSimpleWorkspace(form, Path.Combine(output, "next-layout-empty.png"));

                Call(form, "ImportFighterModel", args[0]); await SimpleControlsReady(form);
                var target = (MDL0Node)editor.TargetModel;
                string targetContent = (string)Call(form, "NativeControlHash", target, ".mdl0");
                Check(editor.SelectedCHR0 == null && Field(form, "_library") == null &&
                    ReferenceEquals(editor.ModelPanel, viewport),
                    "Donor-free native model editing starts in the same complete target editor");

                // Loading source files is a temporary source-pane operation.
                // An empty, isolated shared catalogue must remain byte-identical
                // until the explicit Add to shared library command below.
                var shared = new AnimationLibrary();
                string sharedBefore = NextLibraryFiles(shared); int sharedCount = shared.ClipCount;
                bool cancelledSource = false;
                using (FlowModal("libraryModelFile", dialog => {
                    cancelledSource = true; dialog.DialogResult = DialogResult.Cancel; dialog.Close();
                })) NextItem(form, "nextLoadSourceFiles").PerformClick();
                Check(cancelledSource && shared.ClipCount == sharedCount && NextLibraryFiles(shared) == sharedBefore &&
                    ReferenceEquals(editor.TargetModel, target) && editor.SelectedCHR0 == null,
                    "Cancelling direct source loading leaves the target and shared library unchanged");
                bool directSource = false;
                using (FlowModal("libraryModelFile", dialog => {
                    directSource = true;
                    FlowPathChoice(SimpleControl<ComboBox>(dialog, "libraryModelFile"), sourceModel);
                    FlowPathChoice(SimpleControl<ComboBox>(dialog, "libraryMotionFile"), sourceMotion);
                    SimpleControl<TextBox>(dialog, "libraryGameVersion").Text = "Local temporary source; game build unverified";
                    SimpleControl<CheckBox>(dialog, "libraryApproveAssociation").Checked = true;
                    SimpleControl<Button>(dialog, "libraryRegisterSource").PerformClick();
                    // BuildEvidence is typed supplied metadata, separate from
                    // the UI's game/build text. Feed its existing request contract
                    // before ShowDialog returns to the inventory/copy boundary.
                    var request=(LibraryRegistration)dialog.GetType().GetProperty("Registration",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(dialog);
                    request.BuildEvidence=buildEvidence;
                })) NextItem(form, "nextLoadSourceFiles").PerformClick();
                var library = (AnimationLibrary)Field(form, "_library");
                Check(directSource && ReferenceEquals(library, Field(form, "_sessionSourceLibrary")) &&
                    !ReferenceEquals(library, Field(form, "_sharedSourceLibrary")) && library.ClipCount > 0 &&
                    shared.ClipCount == sharedCount && NextLibraryFiles(shared) == sharedBefore,
                    "Direct model and motion files populate only the temporary source catalogue, without registering the shared library");
                var sourceItem = await FlowSelectSource(form, library, clipName);
                Check(library.Read().Mounts.All(mount => mount.BuildEvidence == buildEvidence),
                    "Temporary source inventory preserves distinct supplied build evidence without inventing verification");
                SimpleControl<Button>(form, "libraryPreviewOriginal").PerformClick();
                var sourceLease = (LibrarySourceLease)Field(form, "_libraryLease");
                var sourceViewport = (ModelPanel)Field(form, "_libraryViewport");
                Check(sourceLease != null && sourceLease.Reference.ClipId == sourceItem.Clip.Id &&
                    !sourceViewport.CurrentViewport.AllowSelection && ReferenceEquals(editor.TargetModel, target) && editor.SelectedCHR0 == null,
                    "Explicit source audition loads the exact verified original into an independent read-only native viewport");
                Check(SimpleControl<Button>(form, "libraryPreviewOriginal").Text == "Load as source" &&
                    SimpleControl<Button>(form, "libraryUseOnTarget").Text == "Use on This Model",
                    "The source library restores Next's familiar preview and transfer command labels");

                // This layout regression retains its original Dense equality
                // and contact expectations; source-default routing has its own
                // actual-button acceptance case.
                UseModelSelectMode(form, RetargetKeyframeMode.Dense);
                string expectedPath = Path.Combine(output, "independent-next.chr0");
                using (var source = library.Open(sourceItem.Clip.Id))
                using (var expected = BrawlRetargeter.Bake(source.Model, target, source.Animation, groundMode: form.FighterProject.GroundMode))
                    expected.Animation.Export(expectedPath);
                int before = form.FighterProject.Animations.Count; bool progress = false;
                using (OnRetargetReady(form, dialog => { progress = true; }))
                    SimpleControl<Button>(form, "libraryUseOnTarget").PerformClick();
                await SimpleControlsReady(form);
                var clip = editor.SelectedCHR0;
                var asset = form.FighterProject.Animations.Last();
                Check(progress && form.FighterProject.Animations.Count == before + 1 && clip != null &&
                    asset.Retarget.Algorithm == HumanoidRetargeter.Algorithm &&
                    asset.Retarget.MotionControlPolicy == HumanoidRetargeter.MotionControlPolicy &&
                    asset.Retarget.Library.ClipId == sourceItem.Clip.Id,
                    "Use on This Model still creates one independent original Next result with exact source provenance");
                using (var expected = NodeFactory.FromFile(null, expectedPath) as CHR0Node)
                    Check(NativeDifference(expected, clip) == null,
                        "The restored UI preserves every independently baked Next key, tangent, native flag and timing value");
                Check(((Control)Field(form, "_libraryPane")).Visible && sourceViewport.Visible &&
                    ReferenceEquals(Field(form, "_libraryLease"), sourceLease),
                    "After transfer the small original preview remains beside the editable native target");
                Check(shared.ClipCount == sharedCount && NextLibraryFiles(shared) == sharedBefore,
                    "Preview and original Next transfer from temporary source files never write the shared catalogue");

                await ReviewBodyEdit(form, 2); await SimpleControlsReady(form);
                var setup = await Await<ControlReply>(Call(form, "LoadControls"));
                editor.SetFrame(3);
                editor.SelectedBone = target._linker.BoneCache.Single(b => b.Name == setup.BoneNames[setup.Roles.Single(r => r.Role == "Hips").Bone.Value]);
                Call(form, "AddSelectedBatchReviewClip"); Call(form, "MarkBatchReview");
                var reviewed = ReviewRows(form).Single();
                var bone = editor.SelectedBone; int frame = editor.CurrentFrame;
                int undo = editor._undoSaves.Count, redo = editor._redoSaves.Count;
                bool dirty = (bool)Field(form, "_fighterDirty"), clipDirty = clip.IsDirty, modelDirty = target.IsDirty;
                string content = SetNativeContent(clip);
                Action<string> preserve = phase => Check(ReferenceEquals(editor.TargetModel, target) &&
                    ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.SelectedBone, bone) &&
                    ReferenceEquals(editor.ModelPanel, viewport) && ReferenceEquals(editor.leftPanel, left) &&
                    ReferenceEquals(editor.rightPanel, right) && ReferenceEquals(editor.CHR0Editor, transforms) &&
                    ReferenceEquals(editor.pnlPlayback, playback) && editor.CurrentFrame == frame &&
                    editor._undoSaves.Count == undo && editor._redoSaves.Count == redo && SetNativeContent(clip) == content &&
                    (bool)Field(form, "_fighterDirty") == dirty && clip.IsDirty == clipDirty && target.IsDirty == modelDirty &&
                    ReviewApproved(reviewed), "Next-layout navigation preserves exact native state, Undo and approval: " + phase);

                foreach (int text in new[] { 100, 175 }) foreach (int width in new[] { 1050, 1700 })
                {
                    form.ClientSize = new Size(width, 950); Call(form, "SetWorkspaceTextSize", text);
                    Call(form, "UpdateSimpleWorkspace"); await Task.Delay(75);
                    var pane = (Panel)Field(form, "_libraryPane"); var body = (Panel)Field(form, "_simpleWorkspaceBody");
                    var toolbar = (ToolStrip)Field(form, "_simpleWorkspaceToolbar");
                    var corner = body.PointToClient(pane.PointToScreen(Point.Empty));
                    Check(body.Top == toolbar.Bottom,
                        "Normal Next workspace begins immediately below its single toolbar without a hidden-guide gap at " + width + "/" + text);
                    Check(pane.Visible && corner.X >= 0 && Math.Abs(corner.X + pane.Width - body.ClientSize.Width) <= 2,
                        "Source library remains on the right at " + width + "/" + text);
                    var nativeCenter = viewport.PointToScreen(new Point(viewport.ClientSize.Width / 2, viewport.ClientSize.Height / 2));
                    Check(!pane.RectangleToScreen(pane.ClientRectangle).Contains(nativeCenter),
                        "Right source pane leaves the actual native target viewport center uncovered at " + width + "/" + text);
                    Check(sourceViewport.Visible && sourceViewport.Height >= 120 && (text != 100 || sourceViewport.Height <= 240) &&
                        !sourceViewport.CurrentViewport.AllowSelection && InClient(sourceViewport, pane) &&
                        InClient((Control)Field(form, "_libraryPlay"), pane),
                        "Complete original source preview and Play control remain inside the right pane at " + width + "/" + text);
                    var play = (Button)Field(form, "_libraryPlay");
                    var use = SimpleControl<Button>(form, "libraryUseOnTarget");
                    var fit = SimpleControl<Button>(form, "sourceFitTarget");
                    Check(InClient(use, pane) && InClient(fit, pane) && play.Width - 12 >= TextRenderer.MeasureText("Pause", play.Font).Width &&
                        new[] { use, fit }.All(button => button.Width - 12 >= TextRenderer.MeasureText(button.Text, button.Font).Width),
                        "Pinned Play/Pause, Use on This Model and Fit start/end captions fit completely inside the right pane at " + width + "/" + text);
                    Check(editor.Visible && viewport.Visible && viewport.Width >= 250 && viewport.Height >= 200 &&
                        !((Control)Field(form, "_simpleWorkspaceActions")).Visible && !((Control)Field(form, "_simpleWorkspaceNavigation")).Visible &&
                        !((Control)Field(form, "_simpleWorkspaceTask")).Visible,
                        "Complete target editor has no competing stage/task shell at " + width + "/" + text);
                    Check(setup.ProtectedMotionBones.All(i => !editor.CanEditAnimationBone(setup.BoneNames[i])),
                        "Source-authored root-control protection survives restored layout at " + width + "/" + text);
                    preserve(width + "/" + text);
                    layouts.Add(new { Width = width, TextPercent = text, LibraryDock = pane.Dock.ToString(),
                        LibraryBounds = pane.Bounds.ToString(), SourceBounds = sourceViewport.Bounds.ToString(),
                        TargetBounds = viewport.Bounds.ToString() });
                    editor.FrameFighterModel(); Call(form, "FrameLibraryOriginal");
                    await CaptureSimpleWorkspace(form, Path.Combine(output, "next-layout-source-target-" + width + "-" + text + ".png"));
                }

                // Commands dispatch through their real application handlers on
                // the isolated owner STA; no SendKeys/input injection is used.
                NextItem(form, "nextFocusEditor").PerformClick();
                Check(editor.ModelPanel.ContainsFocus && !((Control)Field(form, "_libraryPane")).Visible,
                    "Focus editor menu closes competing panes and restores the original target viewport");
                preserve("focus editor");
                NextItem(form, "nextReviewResults").PerformClick();
                Check(((Control)Field(form, "_batchReviewPane")).Visible && ReviewRows(form).Length == 1,
                    "Native Animation menu recovers the exact session review list");
                Call(form, "CloseBatchReview"); preserve("review menu");
                NextItem(form, "nextLoopInspection").PerformClick();
                Check(((Control)Field(form, "_loopInspectionPane")).Visible,
                    "Native Animation menu opens existing read-only loop inspection");
                Call(form, "CloseLoopInspection"); preserve("inspection menu");
                NextItem(form, "nextCompareSource").PerformClick();
                Check(Field(form, "_comparisonLease") != null && ((LibrarySourceLease)Field(form, "_comparisonLease")).Reference.ClipId == sourceItem.Clip.Id,
                    "Native Animation menu links comparison to recorded result provenance");
                NextItem(form, "nextFocusEditor").PerformClick(); preserve("comparison focus recovery");

                // Relocation changes only verified local mount paths. Explicit
                // Add must use those repaired paths, never the now-missing
                // paths in the original temporary registration request.
                string provenance = new JavaScriptSerializer { MaxJsonLength = 32000000 }.Serialize(asset.Retarget);
                Call(form, "PauseSourceOperation"); Call(form, "ReleaseLibraryPreview");
                string relocatedModel = Path.Combine(sourceDirectory, "relocated-model" + Path.GetExtension(sourceModel));
                string relocatedMotion = Path.Combine(sourceDirectory, "relocated-motion" + Path.GetExtension(sourceMotion));
                File.Move(sourceModel, relocatedModel); File.Move(sourceMotion, relocatedMotion);
                library.Repair(sourceItem.Model.MountId, relocatedModel); library.Repair(sourceItem.Motion.Id, relocatedMotion);
                var repaired = library.Browse(clipName).Single(item => item.Clip.Id == sourceItem.Clip.Id);
                Check(!File.Exists(sourceModel) && !File.Exists(sourceMotion) && repaired.Model.Id == sourceItem.Model.Id &&
                    repaired.Model.MountId == sourceItem.Model.MountId && repaired.Motion.Id == sourceItem.Motion.Id &&
                    repaired.Model.ResourceSha256 == sourceItem.Model.ResourceSha256 && repaired.Model.SkeletonSha256 == sourceItem.Model.SkeletonSha256 &&
                    repaired.Clip.ResourceSha256 == sourceItem.Clip.ResourceSha256 && repaired.Motion.ContainerSha256 == sourceItem.Motion.ContainerSha256 &&
                    FighterProject.HashFile(relocatedModel) == originals[donor] && FighterProject.HashFile(relocatedMotion) == originals[args[1]] &&
                    library.Read().Mounts.All(mount => mount.BuildEvidence == buildEvidence),
                    "Exact-byte temporary model/motion repair retains identities, authentic hashes and supplied evidence while the original paths stay missing");
                Check(shared.ClipCount == sharedCount && NextLibraryFiles(shared) == sharedBefore &&
                    new JavaScriptSerializer { MaxJsonLength = 32000000 }.Serialize(asset.Retarget) == provenance,
                    "Repairing temporary source paths does not register the shared library or rewrite recorded result provenance");
                preserve("temporary source repair");

                NextItem(form, "nextAddSourceToLibrary").PerformClick();
                shared = (AnimationLibrary)Field(form, "_sharedSourceLibrary");
                Check(shared.ClipCount == library.ClipCount && shared.ClipCount > sharedCount && NextLibraryFiles(shared) != sharedBefore,
                    "Only explicit Add to shared library publishes the approved temporary model and motion set");
                Check(shared.Read().Mounts.All(mount => mount.BuildEvidence == buildEvidence) &&
                    new HashSet<string>(shared.Read().Mounts.Select(mount => mount.LocalPath),StringComparer.OrdinalIgnoreCase)
                        .SetEquals(new[] { relocatedModel, relocatedMotion }) &&
                    new JavaScriptSerializer { MaxJsonLength = 32000000 }.Serialize(asset.Retarget) == provenance,
                    "Explicit shared registration follows repaired exact-byte paths and preserves supplied evidence and existing result provenance");
                preserve("manual shared-library registration");
                int registeredCount = shared.ClipCount; string registeredFiles = NextLibraryFiles(shared);
                NextItem(form, "nextAddSourceToLibrary").PerformClick();
                Check(shared.ClipCount == registeredCount && NextLibraryFiles(shared) == registeredFiles,
                    "Repeating explicit source registration preserves exact known shared identities and catalogue files");
                NextItem(form, "nextSharedLibrary").PerformClick();
                Check(ReferenceEquals(Field(form, "_library"), shared) && shared.ClipCount == registeredCount,
                    "Shared library command returns to the shared catalogue independently of the temporary source selection");
                preserve("switch to shared library");
                NextItem(form, "nextCompareSource").PerformClick();
                Check(Field(form, "_comparisonLease") != null && ((LibrarySourceLease)Field(form, "_comparisonLease")).Reference.ClipId == sourceItem.Clip.Id,
                    "Comparison after source-mode changes resolves the result's recorded temporary identity without substituting a shared clip");
                NextItem(form, "nextFocusEditor").PerformClick(); preserve("source-mode comparison recovery");
                Check(WorkspaceKey(form, Keys.F6) && KeyboardRegion(form, "fighterToolbar"),
                    "F6 skips hidden legacy strips and enters the sole native project toolbar");
                Check(WorkspaceKey(form, Keys.F6) && editor.ContainsFocus,
                    "F6 returns directly to the complete native editor when all drawers are closed");
                Check((string)Call(form, "NativeControlHash", target, ".mdl0") == targetContent &&
                    originals.All(p => FighterProject.HashFile(p.Key) == p.Value),
                    "Layout, transfer and native body edit preserve target model content and every immutable original file");
            }
        }
        finally
        {
            File.WriteAllText(Path.Combine(output, "next-layout.json"), new JavaScriptSerializer().Serialize(new {
                PrivateDesktop, OSInputInjected = false, ScreenCopied = false, Layouts = layouts,
                FrozenBounds = "one outer toolbar; right library; complete source viewport and Play inside pane; source >=120px and <=240px at100%; native target >=250x200; no exact authored state changes",
                PhysicalInputVerified = false, PhysicalDpiVerified = false
            }));
            if (form != null && !form.IsDisposed) SimpleDisposeForm(form);
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previous);
        }
    }
}
