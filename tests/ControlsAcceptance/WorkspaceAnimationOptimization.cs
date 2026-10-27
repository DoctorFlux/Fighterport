using BrawlCrate.Studio;
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
    static int OptimizationKeyCount(CHR0Node clip) => clip.Children.OfType<CHR0EntryNode>().Sum(t => t.KeyArrays.Sum(c => c.KeyCount));
    static string OptimizationNativeIdentity(CHR0Node clip) => "root" + BrawlLibrarySource.Resources(clip.RootNode).Single(p => ReferenceEquals(p.Item1, clip)).Item2.Substring(1);

    // Use the same offscreen ModelPanel capture as the other native dialogs.
    // DrawToBitmap alone does not include the OpenGL model surfaces.
    static async Task CaptureOptimization(AnimationOptimizationDialog window, string path)
    {
        await CaptureCheckpoint();
        using (var bitmap = new Bitmap(window.Width, window.Height))
        {
            window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, window.Size));
            foreach (var view in new[] { window.SourceView, window.TargetView })
            {
                var panel = view.Viewport;
                panel.Capture(); panel.Invalidate(); panel.Update();
                using (var image = panel.GetScreenshot(panel.ClientRectangle, false))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    var point = window.PointToClient(panel.PointToScreen(Point.Empty));
                    int border = (window.Width - window.ClientSize.Width) / 2;
                    graphics.DrawImageUnscaled(image, point.X + border, point.Y + window.Height - window.ClientSize.Height - border);
                }
            }
            bitmap.Save(path);
        }
    }

    static async Task WorkspaceAnimationOptimization(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Animation optimization checks require the private desktop.");
        NativeOptimizationChecks(args[0], Path.Combine(output, "native-adapter"));
        string previousLibrary = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "isolated-library"));
        var originals = new[] { args[0], args[1] }.Distinct().ToDictionary(p => p, FighterProject.HashFile);
        var project = FighterProject.Create("Donor-free native animation optimization");
        var form = new ModelForm();
        try
        {
            form.InitializeFighterProject(project); form.Show(); form.ClientSize = new Size(1700, 950);
            Call(form, "SetWorkspaceTextSize", 100);
            Call(form, "ImportFighterModel", args[0]);
            Call(form, "ImportFighterAnimations", new[] { new[] { args[1] } });
            await SimpleControlsReady(form);
            var editor = form.FighterEditor;
            var nativeClips = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().ToArray();
            var clip = nativeClips.FirstOrDefault(c => c.Name == "AttackS3S" && c.FrameCount >= 12)
                ?? nativeClips.First(c => !c.Loop && c.FrameCount >= 12);
            editor.SelectedCHR0 = clip;
            await SimpleControlsReady(form);
            var setup = await Await<ControlReply>(Call(form, "LoadControls"));
            var model = (MDL0Node)editor.TargetModel;
            string bodyName = setup.BoneNames[setup.Roles.Single(r => r.Role == "Hips").Bone.Value];
            var bodyBone = model.AllBones.Single(b => b.Name == bodyName);
            var body = clip.Children.OfType<CHR0EntryNode>().SingleOrDefault(t => t.Name == bodyName) ?? clip.CreateEntry(bodyName);
            // This is an original procedural edit of the owned working member.
            // Dense, zero-slope constant channels have independent expectations.
            for (int channel = 0; channel < 9; channel++)
            {
                while (body.KeyArrays[channel].KeyCount > 0) body.KeyArrays[channel].Remove(body.KeyArrays[channel].Entries.First()._index);
                for (int at = 0; at < clip.FrameCount; at++) body.SetKeyframe(channel, at, bodyBone.BindState[channel], true, true)._tangent = 0;
            }
            body.UseModelScale = body.UseModelRotate = body.UseModelTranslate = false;
            body.Keyframes.PreserveInterpolation = true;
            const string foreign = "OptimizationForeignTailFixture";
            var orphan = clip.CreateEntry(foreign);
            // Native CHR0 encodes the complete rotation group. Author all three
            // axes explicitly so the fixture's sparse semantics are canonical.
            for (int channel = 3; channel < 6; channel++)
                for (int at = 0; at < clip.FrameCount; at++) orphan.SetKeyframe(channel, at, 0, true, true)._tangent = 0;
            PreciseCHR0EntryNode.Promote(clip); clip.SignalPropertyChange(); Call(form, "MarkFighterChanged");
            editor.SelectedBone = bodyBone; editor.SetFrame(3); editor.UpdateModel(); editor.FrameFighterModel();
            await SimpleControlsReady(form);
            var owner = (FighterAsset)Call(form, "SelectedFighterAsset");
            Check(owner.Retarget == null && Field(form, "_library") == null, "Optimization starts from an owned native current clip without a donor or library source");
            var animationMenu = (ToolStripDropDownButton)SimpleToolbarItem(form, "nextAnimationMenu");
            Check(animationMenu.DropDownItems.Cast<ToolStripItem>().Single(item => item.Name == "nextOptimizeAnimation").Enabled, "The ordinary native Animation menu exposes Optimize for the current owned clip");
            var siblings = Walk(clip.RootNode).OfType<CHR0Node>().Where(c => !ReferenceEquals(c, clip)).ToDictionary(c => c, SetNativeContent);
            string before = SetNativeContent(clip), roots = RecoveryKeys(clip, (setup.ProtectedMotionBones ?? new int[0]).Select(i => setup.BoneNames[i]).ToArray());
            int keysBefore = OptimizationKeyCount(clip), undoBefore = editor._undoSaves.Count, assetsBefore = project.Animations.Count, frameBefore = editor.CurrentFrame;
            string identity = OptimizationNativeIdentity(clip);
            Call(form, "AddSelectedBatchReviewClip"); Call(form, "MarkBatchReview");
            var review = ReviewRows(form).Single(row => ReferenceEquals(Field(row, "Clip"), clip));
            Check(ReviewApproved(review), "The exact current native member is reviewed before optimization");
            var witness = new GameLibraryDocument(form);

            async Task Open(Func<AnimationOptimizationDialog, Task> exercise)
            {
                bool finished = false; Exception failure = null;
                form.OptimizationReady = async window =>
                {
                    try { await exercise(window); }
                    catch (Exception error) { failure = error; }
                    finally { if (!window.IsDisposed) window.Close(); finished = true; }
                };
                Call(form, "ShowAnimationOptimization");
                await GameLibraryWait(() => finished, "Animation optimization dialog did not complete.", 120);
                form.OptimizationReady = null;
                if (failure != null) throw failure;
                await SimpleControlsReady(form);
            }

            await Open(async window =>
            {
                var mode = SimpleControl<ComboBox>(window, "optimizationMode");
                Check(mode.Items.Count == 1 && mode.SelectedIndex == 0 && Convert.ToString(mode.SelectedItem) == "Exact" && !window.ReadHighQuality(), "Exact is the default and the unqualified high-quality option is absent");
                Check(!SimpleDescendants(window).OfType<CheckBox>().Any() && !SimpleControl<Button>(window, "optimizeApply").Enabled, "Optimization has explicit Analyze and Apply without an extra required review checkbox");
                Check(window.Candidate == null && !ReferenceEquals(window.SourceView.Model, model) && !ReferenceEquals(window.TargetView.Model, model) && !ReferenceEquals(window.SourceView.Model, window.TargetView.Model), "Opening optimization owns independent native preview models and no prepared result");
                Check(Flow39Pose(window.SourceView.Model).SequenceEqual(Flow39Pose(window.TargetView.Model)), "An unprepared result displays the frozen original animation rather than a bind-pose result");
                Check(SimpleControl<Label>(window, "optimizationIdentity").Text.Contains(clip.Name), "The dialog identifies the exact frozen current native animation");

                var realPrepare = window.Prepare;
                var pending = new TaskCompletionSource<CHR0Node>();
                window.Prepare = quality => pending.Task;
                var cancelled = window.Analyze();
                Check(window.Busy && SimpleControl<Button>(window, "optimizationCancel").Enabled && !SimpleControl<Button>(window, "optimizeApply").Enabled, "Analysis exposes cancellation and disables Apply while work is pending");
                SimpleControl<Button>(window, "optimizationCancel").PerformClick();
                pending.SetResult(new CHR0Node { Name = clip.Name, FrameCount = clip.FrameCount, Loop = clip.Loop });
                await cancelled;
                Check(!window.Busy && window.Candidate == null && SimpleControl<Button>(window, "optimizeAnalyze").Focused, "Cancelled analysis rejects late publication and restores Analyze focus");
                window.Prepare = realPrepare;
                witness.CheckPreserved("cancelled optimization analysis");

                await window.Analyze();
                Check(window.Candidate != null, "Exact optimization prepares a native candidate: " + SimpleControl<Label>(window, "optimizationSummary").Text);
                Check(window.Candidate.Children.All(t => model.AllBones.Any(b => b.Name == t.Name)) && !window.Candidate.Children.Any(t => t.Name == foreign), "Prepared target output contains no tracks outside the actual target skeleton");
                Check(SimpleControl<TextBox>(window, "optimizationReport").Text.Contains(foreign), "Analysis lists the absent-target procedural tail track before Apply");
                Check(OptimizationKeyCount(window.Candidate) < keysBefore, "Exact analysis reduces the independently authored dense constant native keys");
                witness.CheckPreserved("independent optimization preparation");
                Check(ReviewApproved(review), "Preparing and comparing a candidate preserves the current member's session approval");
                foreach (int at in new[] { 1, 2, clip.FrameCount / 2, clip.FrameCount }.Distinct())
                {
                    SimpleControl<TrackBar>(window, "optimizationFrame").Value = at;
                    Check(Flow39Pose(window.SourceView.Model).SequenceEqual(Flow39Pose(window.TargetView.Model)), "Exact prepared preview retains the original native pose at display frame " + at);
                }
                int hashes = FighterProject.DiagnosticFileHashes;
                SimpleControl<Button>(window, "optimizationPlay").PerformClick(); await Task.Delay(130); SimpleControl<Button>(window, "optimizationPlay").PerformClick();
                Check(FighterProject.DiagnosticFileHashes == hashes, "Prepared comparison playback performs no file hashing");

                foreach (int percent in new[] { 100, 175 }) foreach (var size in new[] { new Size(980, 720), new Size(1600, 960) })
                {
                    Call(form, "SetWorkspaceTextSize", percent); window.ApplyTextSize(); window.ClientSize = size; window.PerformLayout();
                    foreach (string name in new[] { "optimizeAnalyze", "optimizeApply", "optimizationClose", "optimizationFrame", "optimizationIdentity", "optimizationSummary" })
                    {
                        var control = SimpleControl<Control>(window, name);
                        Check(control.Visible && window.ClientRectangle.Contains(window.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))), "Optimization control fits the client area: " + name + " / " + size.Width + " / " + percent + "%");
                    }
                    Check(window.SourceView.Viewport.Width > 0 && window.SourceView.Viewport.Height > 0 && window.TargetView.Viewport.Width > 0 && window.TargetView.Viewport.Height > 0, "Both native preview surfaces remain available at " + size.Width + " / " + percent + "%");
                    await CaptureOptimization(window, Path.Combine(output, "optimization-" + size.Width + "-" + percent + ".png"));
                }
                Call(form, "SetWorkspaceTextSize", 100); window.ApplyTextSize(); window.ClientSize = new Size(1060, 730);
                SimpleControl<Button>(window, "optimizationDetails").PerformClick();
                Check(SimpleControl<TextBox>(window, "optimizationReport").Visible, "Optimization measurements and absent-target track list expand on request");
                SimpleControl<TextBox>(window, "optimizationReport").Focus(); SimpleControl<Button>(window, "optimizationDetails").PerformClick();
                Check(!SimpleControl<TextBox>(window, "optimizationReport").Visible && SimpleControl<Button>(window, "optimizationDetails").Focused, "Collapsing optimization details restores focus to their disclosure");
                Call(window, "Changed");
                Check(window.Candidate == null && !window.Busy && !SimpleControl<Button>(window, "optimizeApply").Enabled, "Settings invalidation discards the prepared result without automatically analyzing again");
                witness.CheckPreserved("discarded optimization preview");

                await window.Analyze(); Check(window.Candidate != null, "Exact optimization can be prepared again after invalidation");
                var changed = body.KeyArrays[0].Entries.First(); float old = changed._value;
                try
                {
                    changed._value = old + .125f; clip.SignalPropertyChange();
                    SimpleControl<Button>(window, "optimizeApply").PerformClick();
                    Check(window.Candidate == null && changed._value == old + .125f && editor._undoSaves.Count == undoBefore, "Apply rejects changed current-clip content without replacing the user's edit or adding Undo");
                }
                finally { changed._value = old; clip.SignalPropertyChange(); }
            });
            Check(SetNativeContent(clip) == before && siblings.All(p => SetNativeContent(p.Key) == p.Value), "Cancel, invalidation and stale Apply preserve the original current member and every sibling");

            string optimized = null;
            await Open(async window =>
            {
                await window.Analyze(); Check(window.Candidate != null, "A fresh current-clip optimization prepares after the stale operation is closed");
                optimized = SetNativeContent(window.Candidate);
                SimpleControl<Button>(window, "optimizationDetails").PerformClick();
                await CaptureOptimization(window, Path.Combine(output, "optimization-ready-details.png"));
                SimpleControl<Button>(window, "optimizeApply").PerformClick();
            });
            Check(ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.TargetModel, model) && ReferenceEquals(editor.SelectedBone, bodyBone) && editor.CurrentFrame == frameBefore && project.Animations.Count == assetsBefore && SetNativeContent(clip) == optimized,
                "Apply optimizes the exact current member while retaining the target, selected clip/bone/frame and asset count");
            Check(editor._undoSaves.Count == undoBefore + 1 && siblings.All(p => SetNativeContent(p.Key) == p.Value), "Optimization records one native Undo command and preserves all sibling animations");
            Check(RecoveryKeys(clip, (setup.ProtectedMotionBones ?? new int[0]).Select(i => setup.BoneNames[i]).ToArray()) == roots, "Optimization preserves the exact source-owned protected motion-control keys");
            Call(form, "SyncBatchReview"); Check(!ReviewApproved(review), "Applying optimization invalidates approval of the changed native member");
            editor.Undo(); await SimpleControlsReady(form); Check(SetNativeContent(clip) == before && siblings.All(p => SetNativeContent(p.Key) == p.Value), "Native Undo restores all pre-optimization keys and the removed foreign track");
            editor.Redo(); await SimpleControlsReady(form); Check(SetNativeContent(clip) == optimized && !clip.Children.Any(t => t.Name == foreign), "Native Redo restores the same optimized current member without analyzing again");

            string export = Path.Combine(output, "optimized-current.chr0"); form.ExportSimpleWorkspaceResult(export);
            using (var decoded = NodeFactory.FromFile(null, export) as CHR0Node) Check(decoded != null && SetNativeContent(decoded) == optimized, "Independent native export/reopen retains the optimized exact current clip");
            string saved = Path.Combine(output, "optimized-current.fpstudio"); form.SaveFighterProject(saved);
            using (var fresh = FighterProject.Open(saved))
            using (var reopened = new ModelForm())
            {
                reopened.InitializeFighterProject(fresh); reopened.Show();
                await GameLibraryWait(() => reopened.FighterEditor.TargetModel is MDL0Node && reopened.FighterEditor.SelectedCHR0 != null && OptimizationNativeIdentity(reopened.FighterEditor.SelectedCHR0) == identity,
                    "Fresh optimized project did not finish restoring its exact native member.", 60);
                await SimpleControlsReady(reopened);
                var restored = reopened.FighterEditor.SelectedCHR0;
                File.WriteAllText(Path.Combine(output, "optimization-reopen-diagnostic.json"), new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 16000000 }.Serialize(new {
                    ExpectedIdentity = identity, StoredSelection = fresh.SelectedAnimation, ActualIdentity = restored == null ? null : OptimizationNativeIdentity(restored),
                    ActualName = restored?.Name, Expected = optimized, Actual = restored == null ? null : SetNativeContent(restored),
                    Matches = reopened.FighterEditor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Where(c => OptimizationNativeIdentity(c) == identity).Select(c => new { c.Name, Content = SetNativeContent(c) }).ToArray()
                }));
                Check(restored != null && OptimizationNativeIdentity(restored) == identity && SetNativeContent(restored) == optimized, "Project Save/fresh reopen preserves the exact optimized native member and identity");
                foreach (var sibling in siblings)
                {
                    string siblingId = OptimizationNativeIdentity(sibling.Key);
                    var restoredSibling = Walk(restored.RootNode).OfType<CHR0Node>().Single(c => OptimizationNativeIdentity(c) == siblingId);
                    Check(SetNativeContent(restoredSibling) == sibling.Value, "Project reopening preserves sibling member " + siblingId);
                }
                reopened.FighterEditor.FrameFighterModel(); await CaptureSimpleWorkspace(reopened, Path.Combine(output, "optimization-reopened.png"));
                SimpleDisposeForm(reopened);
            }
            Check(originals.All(p => FighterProject.HashFile(p.Key) == p.Value), "Donor-free optimization, Undo, Save and export preserve every original model and motion file byte-for-byte");
        }
        finally
        {
            Call(form, "SetWorkspaceTextSize", 100);
            form.OptimizationReady = null;
            if (!form.IsDisposed) SimpleDisposeForm(form);
            project.Dispose();
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previousLibrary);
        }
    }
}
