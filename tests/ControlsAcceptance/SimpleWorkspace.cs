using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.Internal.Windows.Controls;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    // This is an integrated native workflow, not a model of the workspace state.
    // All UI work runs on the existing private-desktop STA and message pump.
    static async Task SimpleWorkspace(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Simple workspace acceptance requires the private desktop.");
        if (!args.Contains("--retarget-source")) throw new ArgumentException("Simple workspace acceptance needs the original donor model.");
        string donor = args[Array.IndexOf(args, "--retarget-source") + 1];
        string clipName = args[Array.IndexOf(args, "--clip") + 1];
        var inputHashes = new[] { args[0], args[1], donor }.Distinct().ToDictionary(p => p, FighterProject.HashFile);
        using (var startup = MainForm.StartViewerApplication())
        {
            var form = (ModelForm)Field(MainForm.Instance, "_fighterForm");
            var project = form.FighterProject;
            project.Name = "Simple workspace acceptance";
            form.WindowState = FormWindowState.Normal;
            form.ClientSize = new Size(1600, 950);
            var editor = form.FighterEditor;
            var viewport = editor.ModelPanel;
            var left = editor.leftPanel;
            var right = editor.rightPanel;
            var transforms = editor.CHR0Editor;
            var playback = editor.pnlPlayback;
            Check(form.Visible && !MainForm.Instance.Visible && editor.TargetModel == null,
                "Actual startup opens the simpler workspace with the native editor and no dummy donor");
            Check(SimpleControl<Button>(form, "workspacePrimaryAction").Visible &&
                new[] { "Source", "Target", "Preview", "Export" }.All(s => SimpleToolbarItem(form, "workspaceStage" + s).Available),
                "All four workspace stages and the focused task action are present in actual native controls");
            await CaptureSimpleWorkspace(form, Path.Combine(output, "workspace-empty.png"));

            Call(form, "ImportFighterModel", args[0]);
            await SimpleControlsReady(form);
            Check(editor.TargetModel is MDL0Node && editor.SelectedCHR0 == null && project.Animations.Count == 0,
                "The workspace accepts a model-only target without requiring a source animation");
            var target = (MDL0Node)editor.TargetModel;
            string targetHash = (string)Call(form, "NativeControlHash", target, ".mdl0");
            var targetSnapshots = new Dictionary<string, string>();
            SimpleTargetSnapshot(target, output, "original", targetSnapshots);
            Check(SimpleControl<Label>(form, "workspaceTargetSummary").Text.Contains(target.Name),
                "The task panel identifies the actual editable native target");
            editor.FrameFighterModel();
            await CaptureSimpleWorkspace(form, Path.Combine(output, "workspace-model-only.png"));

            // Register authentic source/model association in an isolated catalogue.
            SimpleToolbarItem(form, "workspaceSourceLibrary").PerformClick();
            if (Field(form, "_libraryPane") == null) throw new InvalidOperationException("Workspace source action did not create the native library.");
            var library = new AnimationLibrary(Path.Combine(output, "isolated-library"));
            var model = BrawlLibrarySource.InspectModels(donor).First(m => !m.Shadow);
            library.Register(BrawlLibrarySource.Prepare(new LibraryRegistration
            {
                ModelPath = donor, ModelKey = model.ResourceKey, MotionPath = args[1], Character = model.Name,
                GameVersion = "Local supplied native fixture",
                BuildEvidence = "Local supplied original pair; no independent game-build or membership verification.",
                ApproveAssociation = true
            }));
            form.GetType().GetField("_library", Private).SetValue(form, library);
            var sourceItem = library.Search(clipName, 0, out _).Single(i => i.Clip.Name == clipName && i.Model != null);
            var search = (TextBox)Field(form, "_librarySearch");
            search.Text = clipName;
            ((Timer)Field(form, "_librarySearchTimer")).Stop();
            Call(form, "RefreshLibrary", sourceItem.Clip.Id);
            var tree = (TreeView)Field(form, "_libraryList");
            await Wait(() => !(bool)Field(form, "_libraryReplacingTree") && AllNodes(tree.Nodes).Any(n => n.Name == sourceItem.Clip.Id));
            var sourceNode = AllNodes(tree.Nodes).Single(n => n.Name == sourceItem.Clip.Id);
            tree.SelectedNode = sourceNode;
            Check(sourceNode.Tag is LibrarySearchItem && sourceNode.Level >= 3,
                "The real hierarchical source library selects an original-model-bound animation");
            Check(SimpleControl<Label>(form, "workspaceSourceSummary").Text.Contains(clipName),
                "The focused workspace source summary follows the actual native library selection");
            SimpleControl<Button>(form, "libraryPreviewOriginal").PerformClick();
            var sourceFrame = (TrackBar)Field(form, "_libraryFrame");
            sourceFrame.Value = Math.Min(8, sourceFrame.Maximum);
            Check(ReferenceEquals(editor.TargetModel, target) && editor.SelectedCHR0 == null && project.Animations.Count == 0,
                "Read-only donor audition and scrubbing leave the editable model-only target unchanged");
            await CaptureSimpleWorkspace(form, Path.Combine(output, "workspace-source-audition.png"));

            foreach (int width in new[] { 1320, 1600 })
            {
                form.ClientSize = new Size(width, width == 1320 ? 850 : 950);
                form.PerformLayout();
                await Task.Delay(100);
                Check(ReferenceEquals(form.FighterEditor, editor) && ReferenceEquals(editor.ModelPanel, viewport) &&
                    ReferenceEquals(editor.leftPanel, left) && ReferenceEquals(editor.rightPanel, right) &&
                    ReferenceEquals(editor.CHR0Editor, transforms) && ReferenceEquals(editor.pnlPlayback, playback),
                    "Responsive workspace at " + width + " keeps the complete original editor and native control instances");
                Check(editor.Visible && viewport.Visible && viewport.Width >= 250 && viewport.Height >= 200 &&
                    left.Parent != null && right.Parent != null && transforms.Parent != null && playback.Parent != null,
                    "Responsive workspace at " + width + " leaves a usable native viewport and original authoring panels mounted");
                foreach (string stage in new[] { "Source", "Target", "Preview", "Export" })
                {
                    var button = SimpleToolbarItem(form, "workspaceStage" + stage);
                    Check(button.Owner.ClientRectangle.Contains(button.Bounds),
                        "Workspace " + stage + " stage remains inside its toolbar at " + width);
                }
                await CaptureSimpleWorkspace(form, Path.Combine(output, "workspace-source-" + width + ".png"));
            }

            // Generate the independent comparison from the authentic selected source.
            string expectedPath = Path.Combine(output, "reference-native.chr0");
            using (var source = library.Open(sourceItem.Clip.Id))
            using (var expected = BrawlRetargeter.Bake(source.Model, target, source.Animation, groundMode: project.GroundMode))
                expected.Animation.Export(expectedPath);
            SimpleToolbarItem(form, "workspaceStagePreview").PerformClick();
            var primary = SimpleControl<Button>(form, "workspacePrimaryAction");
            Check(primary.Visible && primary.Enabled && primary.Text.Contains("Preview"),
                "The Preview stage offers a ready transfer for the selected library source and native target");
            bool progressSeen = false;
            using (OnRetargetReady(form, dialog =>
            {
                progressSeen = true;
                Check(dialog.Controls.OfType<ProgressBar>().Single().Style != ProgressBarStyle.Marquee,
                    "Workspace transfer retains original Next measured progress and cancellation");
                CaptureDialog(dialog, output, "workspace-retarget-progress.png");
            })) primary.PerformClick();
            await SimpleControlsReady(form);
            Check(progressSeen && project.Animations.Count == 1,
                "The workspace primary action adds exactly one independent target animation");
            var generated = project.Animations.Single();
            Check(generated.Retarget.Algorithm == HumanoidRetargeter.Algorithm && generated.Retarget.MotionControlPolicy == HumanoidRetargeter.MotionControlPolicy && string.IsNullOrEmpty(generated.Retarget.EngineReceipt) &&
                generated.Retarget.Library.ClipId == sourceItem.Clip.Id && generated.Retarget.Library.ModelContainerSha256 == inputHashes[donor],
                "Workspace transfer uses original Next v7 with authentic library provenance");
            using (var expected = NodeFactory.FromFile(null, expectedPath) as CHR0Node)
            using (var actual = NodeFactory.FromFile(null, generated.WorkingPath) as CHR0Node)
            {
                string difference = NativeDifference(expected, actual);
                File.WriteAllText(Path.Combine(output, "workspace-native-comparison.txt"), difference ?? "Every native key, tangent, transform flag, name and timebase agrees.");
                Check(difference == null, "Workspace transfer matches an independent original Next bake in every native channel");
            }
            var clip = editor.SelectedCHR0;
            Check(clip != null && ReferenceEquals(Call(form, "SelectedFighterAsset"), generated),
                "The workspace selects the exact generated animation revision for native authoring");
            primary.PerformClick();
            await SimpleControlsReady(form);
            var repeated = editor.SelectedCHR0;
            Check(project.Animations.Count == 2 && !ReferenceEquals(repeated, clip) && repeated.Name == clip.Name &&
                ReferenceEquals(Call(form, "SelectedFighterAsset"), project.Animations.Last()),
                "Repeating workspace transfer selects a separate exact result revision even with the same native animation name");
            editor.leftPanel.UpdateAnimations();
            Check(ReferenceEquals(editor.SelectedCHR0, repeated), "Native list refresh preserves the exact same-named repeated workspace result selection");
            Call(form, "UndoFighterImport");
            await SimpleControlsReady(form);
            Check(project.Animations.Count == 1 && ReferenceEquals(editor.SelectedCHR0, clip) &&
                ReferenceEquals(Call(form, "SelectedFighterAsset"), generated),
                "One native import Undo removes only the repeated transfer and restores the first exact workspace revision");
            int playbackRestoreFrame = editor.CurrentFrame;
            int capturesBeforePlayback = BrawlRetargeter.DiagnosticCaptures, bakesBeforePlayback = BrawlRetargeter.DiagnosticBakes;
            int advancedFrame = playbackRestoreFrame;
            var playbackWatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                editor.SetFrame(1);
                editor.TogglePlay();
                bool playbackStarted = editor.Playing;
                while (editor.CurrentFrame == 1 && editor.Playing && playbackWatch.ElapsedMilliseconds < 2000)
                    await Task.Delay(20);
                advancedFrame = editor.CurrentFrame;
                Check(playbackStarted && advancedFrame > 1,
                    "Original native playback advances the workspace result through its prepared animation curves");
            }
            finally
            {
                editor.StopAnim();
                editor.SetFrame(playbackRestoreFrame);
                playbackWatch.Stop();
            }
            Check(!editor.Playing && editor.CurrentFrame == playbackRestoreFrame && ReferenceEquals(editor.SelectedCHR0, clip),
                "Native Stop restores the exact workspace animation and prior timeline frame for authoring");
            Check(BrawlRetargeter.DiagnosticCaptures == capturesBeforePlayback && BrawlRetargeter.DiagnosticBakes == bakesBeforePlayback,
                "Workspace playback and Stop perform no retarget rig capture or bake");
            File.WriteAllText(Path.Combine(output, "workspace-playback.json"), new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new
            {
                AdvancedFrame = advancedFrame, RestoredFrame = editor.CurrentFrame, ElapsedMilliseconds = playbackWatch.ElapsedMilliseconds,
                CapturesBefore = capturesBeforePlayback, CapturesAfter = BrawlRetargeter.DiagnosticCaptures,
                BakesBefore = bakesBeforePlayback, BakesAfter = BrawlRetargeter.DiagnosticBakes
            }));
            editor.SetFrame(Math.Min(8, clip.FrameCount));
            int savedFrame = editor.CurrentFrame;
            foreach (string stage in new[] { "Source", "Target", "Export", "Preview" })
                SimpleToolbarItem(form, "workspaceStage" + stage).PerformClick();
            Check(ReferenceEquals(editor.TargetModel, target) && ReferenceEquals(editor.SelectedCHR0, clip) && editor.CurrentFrame == savedFrame,
                "Changing all workspace stages preserves the editable target, exact animation and native timeline frame");
            var focus = SimpleToolbarItem(form, "workspaceFocusEditor");
            focus.PerformClick();
            Check(!SimpleControl<Control>(form, "workspaceTaskPane").Visible && editor.Visible && ReferenceEquals(editor.SelectedCHR0, clip),
                "Focus Editor collapses workflow tasks while retaining native authoring and exact result ownership");
            await CaptureSimpleWorkspace(form, Path.Combine(output, "workspace-focus-editor.png"));
            focus.PerformClick();
            editor.SetFrame(1);
            editor.FrameFighterModel();
            await CaptureSimpleWorkspace(form, Path.Combine(output, "workspace-transfer-result.png"));

            // Numeric authoring checks use the retained native command and Undo routes.
            await SimpleControlsReady(form);
            var bodyRig = BrawlRetargeter.Capture(target);
            editor.SelectedBone = bodyRig.Bones[bodyRig.Rig.Roles["pelvis"]];
            await (Task)Call(form, "AttachControlPin", false);
            var loaded = await Await<ControlReply>(Call(form, "LoadControls"));
            var pin = editor.AnimationPins.Single();
            var before = loaded.Pose[pin.Bone];
            Call(form, "BeginControlGesture", pin);
            await Wait(() => Field(form, "_pinGesture") != null && (bool)Field(Field(form, "_pinGesture"), "Ready"));
            var q = new System.Numerics.Quaternion(pin.Rotation[0], pin.Rotation[1], pin.Rotation[2], pin.Rotation[3]);
            q = System.Numerics.Quaternion.Normalize(System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, (float)Math.PI / 12) * q);
            Call(form, "MoveControlGesture", new[] { pin.Position[0] + .5f, pin.Position[1], pin.Position[2], q.X, q.Y, q.Z, q.W, before[0], before[1], before[2] });
            Call(form, "EndControlGesture");
            await Wait(() => Field(form, "_pinGesture") == null);
            var bone = target._linker.BoneCache.Single(b => b.Name == pin.BoneName);
            var moved = bone.FrameState;
            var movedPose = BrawlRetargeter.Sample(bodyRig,clip,0);
            int bodyIndex = Array.FindIndex(bodyRig.Bones,b=>b.Name==pin.BoneName);
            var requestedPosition = new System.Numerics.Vector3(pin.Position[0]+.5f,pin.Position[1],pin.Position[2]);
            var orientationError = System.Numerics.Quaternion.Normalize(movedPose.Orientation[bodyIndex]*System.Numerics.Quaternion.Inverse(q));
            double requestedAngleError = 2*Math.Atan2(new System.Numerics.Vector3(orientationError.X,orientationError.Y,orientationError.Z).Length(),Math.Abs(orientationError.W))*180/Math.PI;
            Check(System.Numerics.Vector3.Distance(movedPose.Point(bodyIndex),requestedPosition)<.0002f && requestedAngleError<.002,
                "Workspace body pin reaches the independently requested world translation and orientation");
            editor.Undo();
            await SimpleControlsReady(form);
            Check(Math.Abs(bone.FrameState._translate._x - before[6]) < .0002f, "Native Undo restores the workspace result before its pin edit");
            editor.Redo();
            await SimpleControlsReady(form);
            Check(Math.Abs(bone.FrameState._rotate._y - moved._rotate._y) < .0002f, "Native Redo restores the workspace pin edit");
            int secondFrame = Math.Min(8, clip.FrameCount);
            if (secondFrame < 2) throw new InvalidOperationException("Workspace curve acceptance needs at least two native samples.");
            editor.SetFrame(secondFrame);
            editor.SelectControlHandle(editor.AnimationPins.Single());
            float secondX = bone.FrameState._translate._x + 1.25f;
            var translation = editor.CHR0Editor._transBoxes[6];
            translation.Text = secondX.ToString(System.Globalization.CultureInfo.CurrentCulture);
            Call(translation, "Apply");
            await Wait(() => Field(form, "_pinGesture") == null);
            var authoredTrack = clip.Children.OfType<CHR0EntryNode>().Single(n => n.Name == pin.BoneName);
            Check(authoredTrack.GetKeyframe(6, secondFrame - 1) != null && Math.Abs(bone.FrameState._translate._x - secondX) < .0002f,
                "The original transform field authors an independent later native translation key for curve editing");
            editor.SetFrame(1);
            Check(Math.Abs(bone.FrameState._translate._x - moved._translate._x) < .0002f,
                "Authoring the later native key preserves the earlier workspace pin translation");
            Call(editor, "ShowControlInterpolation");
            var curves = editor.InterpolationEditor;
            var entry = clip.Children.OfType<CHR0EntryNode>().Single(n => n.Name == pin.BoneName);
            Check(ReferenceEquals(curves._targetNode, entry), "Workspace pin opens its actual native curve in the retained interpolation editor");
            curves.SelectedMode = 6;
            var first = entry.KeyArrays[6].Entries.First();
            var second = entry.KeyArrays[6].Entries.Skip(1).First();
            int firstIndex = first._index;
            curves.SelectedKeyframe = first;
            float oldTangent = first._tangent, newTangent = oldTangent + .125f;
            var tangent = (NumericInputBox)Field(curves, "numInTan");
            tangent.Text = newTangent.ToString(System.Globalization.CultureInfo.CurrentCulture);
            Call(tangent, "Apply");
            Application.DoEvents();
            float interval = second._index - first._index;
            double t = .25, expectedQuarter = (2 * t * t * t - 3 * t * t + 1) * first._value +
                (t * t * t - 2 * t * t + t) * interval * newTangent +
                (-2 * t * t * t + 3 * t * t) * second._value + (t * t * t - t * t) * interval * second._tangent;
            Check(Math.Abs(entry.GetKeyframe(6, firstIndex)._tangent - newTangent) < .00001f && Math.Abs(entry.GetFrameValue(6, firstIndex + interval * .25f) - expectedQuarter) < .0002,
                "Original interpolation field authors a native tangent with an independent subframe Hermite expectation");
            editor.Undo();
            Check(Math.Abs(entry.GetKeyframe(6, firstIndex)._tangent - oldTangent) < .00001f, "Workspace curve Undo restores its exact original tangent");
            editor.Redo();
            Check(Math.Abs(entry.GetKeyframe(6, firstIndex)._tangent - newTangent) < .00001f, "Workspace curve Redo restores its authored tangent");
            editor.SetFrame(Math.Min(8, clip.FrameCount));
            editor.SetFrame(1);
            await CaptureSimpleWorkspace(form, Path.Combine(output, "workspace-native-edit.png"));
            await CaptureWindow(editor._interpolationForm, Path.Combine(output, "workspace-interpolation.png"));
            editor._interpolationForm.Close();

            SimpleToolbarItem(form, "workspaceStageExport").PerformClick();
            string exported = Path.Combine(output, "workspace-result.chr0");
            Call(form, "ExportSimpleWorkspaceResult", exported);
            using (var fresh = NodeFactory.FromFile(null, exported) as CHR0Node)
            {
                Check(NativeDifference(clip, fresh) == null, "The workspace export route freshly decodes with every native edited key, tangent and flag unchanged");
                Check(Math.Abs(((CHR0EntryNode)fresh.FindChild(pin.BoneName, false)).GetFrameValue(6, firstIndex + interval * .25f) - expectedQuarter) < .0002,
                    "Fresh workspace export preserves the independently expected edited subframe pose");
            }
            string manifest = Path.Combine(output, "workspace-result.fpstudio");
            form.SaveFighterProject(manifest);
            using (var fresh = FighterProject.Open(manifest))
            {
                Check(fresh.Warnings.Count == 0 && fresh.Animations.Single().Retarget.Library.ClipId == sourceItem.Clip.Id && fresh.AnimationControls.Any(c => c.Json.Contains(pin.Id)),
                    "Workspace project reopen retains resolved originals, original Next provenance and native control identity");
                var reopened = new ModelForm();
                reopened.InitializeFighterProject(fresh);
                reopened.ClientSize = new Size(1600, 950);
                reopened.Show();
                await SimpleControlsReady(reopened);
                await Wait(() => reopened.FighterEditor.AnimationPins.Any(p => p.Id == pin.Id));
                var freshClip = reopened.FighterEditor.SelectedCHR0;
                Check(freshClip != null && NativeDifference(clip, freshClip) == null,
                    "Actual simpler workspace reopen restores the exact authored native animation in the full previewer");
                Check(SimpleControl<Label>(reopened, "workspaceTargetSummary").Text.Contains(target.Name),
                    "Reopened workspace target summary identifies its restored native model");
                string restoredSource = SimpleControl<Label>(reopened, "workspaceSourceSummary").Text;
                Check(restoredSource.Contains(clipName) && restoredSource.Contains("Saved source link"),
                    "Reopened workspace shows the saved authentic source link without claiming a verified catalogue selection");
                Check(((ToolStripButton)SimpleToolbarItem(reopened, "workspaceStagePreview")).Checked,
                    "Reopened native motion resumes at Preview and fix for the returning workspace user");
                reopened.FighterEditor.FrameFighterModel();
                await CaptureSimpleWorkspace(reopened, Path.Combine(output, "workspace-reopened.png"));
                SimpleDisposeForm(reopened);
            }

            Check(SimpleTargetSnapshot(target, output, "before-dependency-probes", targetSnapshots) == targetHash,
                "Ordinary workspace transfer, native correction and project reopen preserve every target model byte before dependency probes");
            // Guard checks use independent short native clips to bound background work.
            using (var source = library.Open(sourceItem.Clip.Id))
            using (var probe = new CHR0Node { Name = "Original workspace dependency probe", FrameCount = 2 })
            {
                var capture = BrawlRetargeter.Capture(source.Model);
                probe.CreateEntry(capture.Bones[capture.Rig.Roles["pelvis"]].Name).SetKeyframe(3, 0, 0, true, true);
                int accepted = project.Animations.Count;
                string acceptedHash = (string)Call(form, "NativeControlHash", clip, ".chr0");
                using (OnRetargetReady(form, dialog => dialog.Controls.OfType<Button>().Single().PerformClick()))
                    Check(Call(form, "BakeWithProgress", source.Model, target, probe) == null,
                        "Cancelling the workspace's original Next process boundary publishes no result");
                Check(SimpleTargetSnapshot(target, output, "after-cancellation", targetSnapshots) == targetHash,
                    "Cancelled native retargeting preserves every target model byte");
                foreach (string dependency in new[] { "settings", "source", "target" })
                {
                    var oldMode = project.GroundMode;
                    string sourceName = source.Model.Name;
                    int acceptedFrame = editor.CurrentFrame;
                    bool editingAll = editor.EditingAll;
                    bool rejected = false;
                    try
                    {
                        using (OnRetargetReady(form, dialog =>
                        {
                            if (dependency == "settings") project.GroundMode = oldMode == GroundContactMode.Airborne ? GroundContactMode.Auto : GroundContactMode.Airborne;
                            else if (dependency == "source") source.Model.Name += " dependency changed";
                            else editor.TargetModel = null;
                        }))
                        {
                            try { using (var result = (RetargetResult)Call(form, "BakeWithProgress", source.Model, target, probe)) { } }
                            catch (TargetInvocationException error) { rejected = error.InnerException is InvalidOperationException && error.InnerException.Message.Contains("inputs changed"); }
                        }
                    }
                    finally
                    {
                        project.GroundMode = oldMode;
                        source.Model.Name = sourceName;
                        if (dependency == "target")
                        {
                            // Change the real editable target binding, not native model
                            // bytes. Renaming then restoring a model marks it dirty and
                            // exercises MDL0 rebuilding rather than stale selection.
                            editor.TargetModel = target;
                            editor.SelectedCHR0 = clip;
                            editor.EditingAll = editingAll;
                            editor.SetFrame(acceptedFrame);
                        }
                    }
                    await SimpleControlsReady(form);
                    Check(rejected, "Changed workspace " + dependency + " rejects stale original Next publication");
                    Check(SimpleTargetSnapshot(target, output, "after-restored-" + dependency, targetSnapshots) == targetHash,
                        "Restored workspace " + dependency + " probe preserves every target model byte");
                }
                Check(project.Animations.Count == accepted && ReferenceEquals(editor.SelectedCHR0, clip) &&
                    (string)Call(form, "NativeControlHash", clip, ".chr0") == acceptedHash,
                    "Cancellation and changed dependencies preserve accepted workspace results and exact native selection");
            }
            Check((string)Call(form, "NativeControlHash", target, ".mdl0") == targetHash,
                "Workspace transfer and native animation correction preserve target deform hierarchy, binds and weights");
            Check(inputHashes.All(p => FighterProject.HashFile(p.Key) == p.Value),
                "Every original supplied model and animation container remains byte-identical through the integrated workspace workflow");
            Call(form, "DisposeAnimationLibrary");
            SimpleDisposeForm(form);
            project.Dispose();
        }
    }

    static Task SimpleControlsReady(ModelForm form) => Wait(() => !(bool)Field(form, "_controlBusy") && !(bool)Field(form, "_controlSetupQueued"));
    static string SimpleTargetSnapshot(MDL0Node target, string output, string stage, Dictionary<string, string> snapshots)
    {
        string path = Path.Combine(output, "workspace-target-" + stage + ".mdl0");
        target.Export(path);
        snapshots[stage] = FighterProject.HashFile(path);
        File.WriteAllText(Path.Combine(output, "workspace-target-invariance.json"),
            new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(snapshots));
        return snapshots[stage];
    }
    static T SimpleControl<T>(Control parent, string name) where T : Control => parent.Controls.Find(name, true).OfType<T>().Single();
    static IEnumerable<Control> SimpleDescendants(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls) foreach (var nested in SimpleDescendants(child)) yield return nested;
    }
    static ToolStripItem SimpleToolbarItem(Control form, string name) => SimpleDescendants(form).OfType<ToolStrip>().SelectMany(s => s.Items.Cast<ToolStripItem>()).Single(i => i.Name == name);
    static void SimpleDisposeForm(ModelForm form)
    {
        form.FighterEditor._interpolationForm?.Dispose();
        ((AnimationControlClient)Field(form, "_controlEngine")).Dispose();
        BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(form.FighterEditor);
        form.Dispose();
    }
    static async Task CaptureSimpleWorkspace(ModelForm form, string path)
    {
        await CaptureCheckpoint();
        using (var bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            var panels = new[] { form.FighterEditor.ModelPanel, Field(form, "_libraryViewport") as ModelPanel };
            foreach (var panel in panels.Where(p => p != null && p.Visible && p.Width > 0 && p.Height > 0))
            {
                panel.Capture(); panel.Invalidate(); panel.Update();
                using (var view = panel.GetScreenshot(panel.ClientRectangle, false))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    var point = form.PointToClient(panel.PointToScreen(Point.Empty));
                    int border = (form.Width - form.ClientSize.Width) / 2;
                    graphics.DrawImageUnscaled(view, point.X + border, point.Y + form.Height - form.ClientSize.Height - border);
                }
            }
            // Outer drawers occlude native child viewports. Composite their
            // actual controls after offscreen GL, matching the window Z order.
            foreach(string name in new[]{"animationLibraryPane","workspaceTaskPane"})
            {
                var drawer=form.Controls.Find(name,true).SingleOrDefault();
                if(drawer==null||!drawer.Visible||drawer.Dock!=DockStyle.None)continue;
                using(var layer=new Bitmap(drawer.Width,drawer.Height))
                using(var graphics=Graphics.FromImage(bitmap))
                {
                    drawer.DrawToBitmap(layer,new Rectangle(Point.Empty,drawer.Size));
                    var point=form.PointToClient(drawer.PointToScreen(Point.Empty));
                    int border=(form.Width-form.ClientSize.Width)/2;
                    graphics.DrawImageUnscaled(layer,point.X+border,point.Y+form.Height-form.ClientSize.Height-border);
                    var sourceView=Field(form,"_libraryViewport") as ModelPanel;
                    if(sourceView!=null && sourceView.Visible && drawer.Contains(sourceView))
                    {
                        sourceView.Capture();sourceView.Invalidate();sourceView.Update();
                        using(var original=sourceView.GetScreenshot(sourceView.ClientRectangle,false))
                        {
                            var viewPoint=form.PointToClient(sourceView.PointToScreen(Point.Empty));
                            var savedClip=graphics.Save();
                            graphics.SetClip(new Rectangle(point.X+border,point.Y+form.Height-form.ClientSize.Height-border,drawer.ClientSize.Width,drawer.ClientSize.Height));
                            graphics.DrawImageUnscaled(original,viewPoint.X+border,viewPoint.Y+form.Height-form.ClientSize.Height-border);
                            graphics.Restore(savedClip);
                        }
                    }
                }
            }
            bitmap.Save(path);
        }
    }
}
