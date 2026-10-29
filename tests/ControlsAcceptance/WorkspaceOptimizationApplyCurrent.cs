using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    // Appended after the independent-export corpus. This deliberately changes
    // one project-owned animation and tests the normal native command stack;
    // it does not reinterpret export-copy preservation as permission to Apply.
    static async Task WorkspaceOptimizationApplyCurrent(ModelForm form, CHR0Node clip, string output,
        Dictionary<string, string> originalInputs)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Current-animation Apply qualification requires a private desktop.");
        var editor = form.FighterEditor;
        var model = (MDL0Node)editor.TargetModel;
        var project = form.FighterProject;
        var setup = await Await<ControlReply>(Call(form, "LoadControls"));
        string bodyName = setup.BoneNames[setup.Roles.Single(role => role.Role == "Hips").Bone.Value];
        var bodyBone = model.AllBones.Single(bone => bone.Name == bodyName);
        var body = clip.Children.OfType<CHR0EntryNode>().SingleOrDefault(entry => entry.Name == bodyName) ?? clip.CreateEntry(bodyName);
        // Original procedural redundancy, independent of the reducer: nine
        // genuinely constant zero-slope scalar streams have one-value storage.
        for (int channel = 0; channel < 9; channel++)
        {
            while (body.KeyArrays[channel].KeyCount > 0) body.KeyArrays[channel].Remove(body.KeyArrays[channel].Entries.First()._index);
            float value = bodyBone.BindState[channel] + (channel == 3 ? .125f : 0);
            for (int at = 0; at < clip.FrameCount; at++) body.SetKeyframe(channel, at, value, true, true)._tangent = 0;
        }
        body.UseModelScale = body.UseModelRotate = body.UseModelTranslate = false;
        body.Keyframes.PreserveInterpolation = true;
        const string absent = "ProceduralOptimizationAbsentTargetTrack";
        Check(!model.AllBones.Any(bone => bone.Name == absent), "Apply fixture's explicitly reviewed unmatched track is absent from the real target model");
        var foreign = clip.CreateEntry(absent);
        for (int channel = 3; channel < 6; channel++)
            for (int at = 0; at < clip.FrameCount; at++) foreign.SetKeyframe(channel, at, channel == 3 ? 3 : 0, true, true)._tangent = 0;
        foreign.UseModelRotate = false; foreign.Keyframes.PreserveInterpolation = true;
        PreciseCHR0EntryNode.Promote(clip); clip.SignalPropertyChange(); Call(form, "MarkFighterChanged");
        editor.SelectedBone = bodyBone; editor.SetFrame(Math.Min(3, clip.FrameCount)); editor.UpdateModel();
        await SimpleControlsReady(form); await Await<ControlReply>(Call(form, "LoadControls"));
        Call(form, "AddSelectedBatchReviewClip"); Call(form, "MarkBatchReview");
        var review = ReviewRows(form).Single(row => ReferenceEquals(Field(row, "Clip"), clip));
        Check(ReviewApproved(review), "Current-animation Apply starts with approval of the exact procedural full-quality member");
        Call(form, "CloseBatchReview"); await SimpleControlsReady(form);

        var captures = new List<string>();
        string before = SetNativeContent(clip), beforeStorage = NativeAnimationOptimization.StorageKey(clip);
        string identity = OptimizationNativeIdentity(clip), camera = CompactCamera(form), modelContent = (string)Call(form, "NativeControlHash", model, ".mdl0");
        int frame = editor.CurrentFrame, undo = editor._undoSaves.Count, cursor = editor._saveIndex, assetCount = project.Animations.Count;
        var siblings = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Where(item => !ReferenceEquals(item, clip)).ToDictionary(item => item, SetNativeContent);
        var untouchedBytes = OptimizationUntouchedBytes(form, clip);
        var asset = (FighterAsset)Call(form, "SelectedFighterAsset");
        string recipe = LegacyReviewRecipe(asset);
        using (var master = NativeAnimationOptimization.CopySource(clip))
        {
            void IdentityAndDependencies(string phase)
            {
                Check(ReferenceEquals(editor.TargetModel, model) && ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.TargetAnimation, clip) &&
                    ReferenceEquals(editor.SelectedBone, bodyBone) && editor.CurrentFrame == frame && OptimizationNativeIdentity(clip) == identity &&
                    CompactCamera(form) == camera && project.Animations.Count == assetCount,
                    "Current Apply " + phase + " preserves exact target/clip/bone identity, frame, camera, ownership and asset count");
                Check(siblings.All(item => SetNativeContent(item.Key) == item.Value) && (string)Call(form, "NativeControlHash", model, ".mdl0") == modelContent && LegacyReviewRecipe(asset) == recipe,
                    "Current Apply " + phase + " preserves every sibling, authentic model/weights and historical provenance");
            }
            async Task Open(Func<AnimationOptimizationDialog, Task> exercise)
            {
                bool finished = false; Exception failure = null;
                form.OptimizationReady = async window =>
                {
                    try { await exercise(window); }
                    catch (Exception error) { failure = error; }
                    finally { if (!window.IsDisposed) window.Close(); finished = true; }
                };
                try
                {
                    Call(form, "ShowAnimationOptimization");
                    await GameLibraryWait(() => finished, "Current-animation Apply modal did not complete.", 180);
                }
                finally { form.OptimizationReady = null; }
                if (failure != null) throw failure;
                await SimpleControlsReady(form);
            }
            await Open(async window =>
            {
                var action = SimpleControl<Button>(window, "optimizeApplyCurrent");
                Check(action.Visible && !action.Enabled && action.Text.Contains("current"), "Current-animation Apply is an explicit, initially unavailable action rather than automatic optimization");
                var realPrepare = window.Prepare; var pending = new TaskCompletionSource<CHR0Node>(); window.Prepare = quality => pending.Task;
                var work = window.Analyze();
                Check(window.Busy && !action.Enabled && !SimpleControl<Button>(window, "optimizeApply").Enabled, "Pending optimization disables both current Apply and independent export");
                SimpleControl<Button>(window, "optimizationCancel").PerformClick();
                pending.SetResult(NativeAnimationOptimization.CopySource(clip)); await work; window.Prepare = realPrepare;
                Check(!window.Busy && window.Candidate == null && !action.Enabled && SetNativeContent(clip) == before && editor._saveIndex == cursor,
                    "Cancelled current preparation rejects a late candidate without changing the animation or native command cursor");
                SimpleControl<ComboBox>(window, "exportOptimizationPolicy").SelectedIndex = 0;
                await window.Analyze(); Check(window.Candidate != null && action.Enabled, "Exact current Apply prepares through the existing independently decoded native optimizer");
                using (var tampered = NativeAnimationOptimization.CopySource(window.Candidate))
                {
                    tampered.Name += " changed"; bool rejected = false;
                    try { window.ApplyCurrent(window.ReadHighQuality(), tampered); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected && SetNativeContent(clip) == before && editor._saveIndex == cursor, "Current Apply rejects a changed prepared animation identity before adding a native command");
                }
                var changed = body.KeyArrays[3].Entries.First(); float original = changed._value;
                try
                {
                    changed._value += .25f; clip.SignalPropertyChange(); action.PerformClick();
                    Check(window.Candidate == null && changed._value == original + .25f && editor._saveIndex == cursor,
                        "Current Apply rejects stale authored content while preserving the user's intervening edit and native history");
                }
                finally { changed._value = original; clip.SignalPropertyChange(); }
            });
            IdentityAndDependencies("cancelled/stale preparation");
            Check(SetNativeContent(clip) == before && NativeAnimationOptimization.StorageKey(clip) == beforeStorage && editor._undoSaves.Count == undo,
                "Failed current Apply leaves exact original content/storage and native Undo unchanged");

            string optimized = null, optimizedStorage = null; long originalKeys = OptimizationKeyCount(clip), optimizedKeys = 0;
            await Open(async window =>
            {
                SimpleControl<ComboBox>(window, "exportOptimizationPolicy").SelectedIndex = 0;
                await window.Analyze(); Check(window.Candidate != null && SimpleControl<Button>(window, "optimizeApplyCurrent").Enabled, "Fresh current Apply remains available after stale work was discarded");
                Check(SimpleControl<Button>(window, "optimizeApplyCurrent").TabStop && !string.IsNullOrWhiteSpace(SimpleControl<Button>(window, "optimizeApplyCurrent").AccessibleDescription),
                    "Current Apply is keyboard-focusable and describes its explicit live animation/Undo behavior accessibly");
                optimized = SetNativeContent(window.Candidate); optimizedStorage = NativeAnimationOptimization.StorageKey(window.Candidate); optimizedKeys = OptimizationKeyCount(window.Candidate);
                Check(optimizedKeys < originalKeys && !window.Candidate.Children.Any(entry => entry.Name == absent), "Prepared current optimization reviews/removes the unmatched track and reduces genuine procedural redundancy");
                ReplayRootsMatch(master, window.Candidate, model, (setup.ProtectedMotionBones ?? new int[0]).Select(index => setup.BoneNames[index]).ToArray());
                foreach (int text in new[] { 100, 175 }) foreach (int width in new[] { 1050, 1700 })
                {
                    Call(form, "SetWorkspaceTextSize", text); window.ApplyTextSize(); window.ClientSize = new Size(width, width == 1050 ? 850 : 1000); window.PerformLayout();
                    foreach (string name in new[] { "optimizeAnalyze", "optimizeApply", "optimizeApplyCurrent", "optimizationClose" })
                        Check(StandaloneControlReachable(SimpleControl<Button>(window, name)), "Explicit current Apply/footer survives ancestor clipping and managed hit test: " + name + "/" + width + "/" + text);
                    string capture = "current-apply-" + width + "-" + text + ".png";
                    await CaptureOptimization(window, Path.Combine(output, capture)); captures.Add(capture);
                }
                Call(form, "SetWorkspaceTextSize", 100); window.ApplyTextSize();
                File.WriteAllText(Path.Combine(output, "current-apply-analysis.txt"), window.PreparedReport);
                SimpleControl<Button>(window, "optimizeApplyCurrent").PerformClick();
                Check(window.IsDisposed || window.DialogResult == DialogResult.OK, "Apply to current completes the prepared native operation without opening an export chooser");
            });
            IdentityAndDependencies("successful one-command Apply");
            Check(SetNativeContent(clip) == optimized && NativeAnimationOptimization.StorageKey(clip) == optimizedStorage && editor._saveIndex == cursor + 1 && editor._undoSaves.Count == undo + 1,
                "Current Apply replaces exactly the selected native member including encoding/storage through one existing Undo command");
            Call(form, "SyncBatchReview"); Check(!ReviewApproved(review), "Current optimization invalidates approval bound to the earlier exact animation content");
            Check(OptimizationRetainedBaseline(form) == before, "Explicit Apply retains its original full-quality session baseline rather than rebasing to the reduced animation");
            editor.Undo(); await SimpleControlsReady(form);
            Check(SetNativeContent(clip) == before && NativeAnimationOptimization.StorageKey(clip) == beforeStorage && editor._saveIndex == cursor,
                "Native Undo restores all original values, tangents, flags, unmatched track and encoding/storage");
            IdentityAndDependencies("native Undo");
            editor.Redo(); await SimpleControlsReady(form);
            Check(SetNativeContent(clip) == optimized && NativeAnimationOptimization.StorageKey(clip) == optimizedStorage && editor._saveIndex == cursor + 1,
                "Native Redo restores the same optimized coefficients/storage without running optimization again");
            IdentityAndDependencies("native Redo");
            await Open(async window =>
            {
                SimpleControl<ComboBox>(window, "exportOptimizationPolicy").SelectedIndex = 0; await window.Analyze();
                Check(window.Candidate != null && SetNativeContent(window.Candidate) == optimized && NativeAnimationOptimization.StorageKey(window.Candidate) == optimizedStorage,
                    "Repeated current optimization offers the same verified result instead of accumulating reduction or quantization");
                SimpleControl<Button>(window, "optimizeApplyCurrent").PerformClick();
            });
            Check(SetNativeContent(clip) == optimized && editor._saveIndex == cursor + 1 && editor._undoSaves.Count == undo + 1 && OptimizationRetainedBaseline(form) == before,
                "Reapplying an unchanged result is a no-op for document history and retains the full-quality baseline");

            string exported = Path.Combine(output, "current-applied.chr0"); form.ExportSimpleWorkspaceResult(exported);
            using (var decoded = NodeFactory.FromFile(null, exported) as CHR0Node)
                Check(decoded != null && SetNativeContent(decoded) == optimized && NativeAnimationOptimization.StorageKey(decoded) == optimizedStorage,
                    "Ordinary native export/reopen preserves the applied current animation including its verified compact storage");
            string saved = Path.Combine(output, "current-applied.fpstudio"); form.SaveFighterProject(saved);
            OptimizationCheckUntouchedBytes(form, clip, untouchedBytes, "current Apply/project Save", Path.Combine(output, "current-apply-unchanged-siblings.json"));
            var fresh = FighterProject.Open(saved); var reopened = new ModelForm();
            try
            {
                reopened.InitializeFighterProject(fresh); reopened.Show();
                await GameLibraryWait(() => reopened.FighterEditor.TargetModel is MDL0Node && reopened.FighterEditor.SelectedCHR0 != null && OptimizationNativeIdentity(reopened.FighterEditor.SelectedCHR0) == identity,
                    "Saved applied current animation did not restore its exact selected member.", 60);
                await SimpleControlsReady(reopened);
                Check(SetNativeContent(reopened.FighterEditor.SelectedCHR0) == optimized && NativeAnimationOptimization.StorageKey(reopened.FighterEditor.SelectedCHR0) == optimizedStorage,
                    "Project Save/reopen retains applied keys/metadata/flags/storage while preserving native clip identity");
                Check(reopened.OptimizationCachedClips == 0, "Reopen creates no persisted optimization baseline or competing project-format state");
            }
            finally { SimpleDisposeForm(reopened); fresh.Dispose(); }
            Check(originalInputs.All(item => FighterProject.HashFile(item.Key) == item.Value), "Current Apply, Undo/Redo and Save/export preserve every original model and animation source byte");
            File.WriteAllText(Path.Combine(output, "current-apply-evidence.json"), new JavaScriptSerializer().Serialize(new {
                Identity = identity, BeforeKeys = originalKeys, AfterKeys = optimizedKeys, NativeCommandsAdded = editor._saveIndex - cursor,
                AppliedStorage = optimizedStorage, Export = exported, Project = saved, Captures = captures,
                FullQualityBaselineRetained = OptimizationRetainedBaseline(form) == before, OriginalInputHashes = originalInputs,
                SharedOffscreenRenderer = true, ActiveDesktopCapture = false, OSInputInjected = false,
                PhysicalInputAndDpiVerified = false, GameAcceptance = false }));

            // Exercise the production eviction operation without allocating
            // another 256 MiB fixture. Losing a snapshot must not silently turn
            // an already-reduced member into a fresh approximation baseline.
            var marker = Field(form, "_optimizationBaseline");
            Check(marker != null && Field(marker, "Baseline") != null, "Eviction fixture starts with the genuine applied member's retained original snapshot");
            string originalBaselineHash = (string)Field(marker, "BaselineHash");
            Call(marker, "Evict");
            Check(OptimizationRetainedBaseline(form) == null && (string)Field(marker, "BaselineHash") == originalBaselineHash,
                "Production cache eviction releases the full snapshot while retaining its exact original identity marker");
            foreach (int policy in new[] { 0, 1 })
                await Open(async window =>
                {
                    SimpleControl<ComboBox>(window, "exportOptimizationPolicy").SelectedIndex = policy;
                    await window.Analyze();
                    Check(window.Candidate != null && window.PreparedReport.Contains("Published-copy policy: Exact") &&
                        (policy == 0 || window.PreparedReport.Contains("evicted")),
                        "Evicted original retains independently verified Exact preparation and explicitly blocks repeated approximation: policy " + policy);
                    Check(SetNativeContent(clip) == optimized && NativeAnimationOptimization.StorageKey(clip) == optimizedStorage &&
                        editor._saveIndex == cursor + 1 && editor._undoSaves.Count == undo + 1 &&
                        Field(marker, "Baseline") == null && (string)Field(marker, "BaselineHash") == originalBaselineHash,
                        "Exact/evicted-fallback analysis preserves the current animation/history and never replaces the original marker with a lossy baseline");
                    File.WriteAllText(Path.Combine(output, "current-apply-evicted-" + policy + ".txt"), window.PreparedReport);
                });
            IdentityAndDependencies("evicted Exact/fallback preparation");
            Check(originalInputs.All(item => FighterProject.HashFile(item.Key) == item.Value), "Evicted-baseline fallback preserves every immutable original asset");
        }
    }
}
