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
    static string CompactCamera(ModelForm form)
    {
        var camera = form.FighterEditor.ModelPanel.CurrentViewport.Camera;
        return string.Join(",", Enumerable.Range(0, 16).Select(i => ReplayFloatBits(camera._matrix[i]))) + "/" + camera._ortho;
    }

    static async Task WorkspaceCompactOptimization(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Compact optimization requires a private desktop.");
        string previousLibrary = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "isolated-library"));
        var originals = new[] { args[0], args[1] }.Distinct().ToDictionary(p => p, FighterProject.HashFile);
        var project = FighterProject.Create("Independent compact current-animation qualification");
        var form = new ModelForm();
        try
        {
            form.InitializeFighterProject(project); form.Show(); form.ClientSize = new Size(1700, 950);
            Call(form, "SetWorkspaceTextSize", 100); Call(form, "ImportFighterModel", args[0]);
            Call(form, "ImportFighterAnimations", new[] { new[] { args[1] } }); await SimpleControlsReady(form);
            var editor = form.FighterEditor; var model = (MDL0Node)editor.TargetModel;
            var clip = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First(c => !c.Loop && c.FrameCount >= 12 && c.FrameCount <= 255);
            editor.SelectedCHR0 = clip; await SimpleControlsReady(form);
            var setup = await Await<ControlReply>(Call(form, "LoadControls"));
            string name = setup.BoneNames[setup.Roles.Single(r => r.Role == "Hips").Bone.Value];
            var bone = model.AllBones.Single(b => b.Name == name);

            void Author(bool highQuality, bool bind = false)
            {
                // Original procedural data in the current owned animation. Its
                // nonlinear dyadic knots have no removable exact interior span.
                foreach (var child in clip.Children.ToArray()) { child.Remove(); child.Dispose(); }
                var track = clip.CreateEntry(name);
                track.UseModelScale = track.UseModelRotate = track.UseModelTranslate = false;
                track.ScaleCompensateApply = bone.SegScaleCompApply; track.ScaleCompensateParent = bone.SegScaleCompParent;
                track.ClassicScaleOff = !bone.ClassicScale;
                if (bind)
                {
                    for (int channel = 0; channel < 9; channel++)
                    for (int frame = 0; frame < clip.FrameCount; frame++)
                        track.Keyframes.SetFrameValue(channel, frame, bone.BindState[channel], true)._tangent = 0;
                }
                else
                {
                    for (int channel = 3; channel < 6; channel++) track.Keyframes.SetFrameValue(channel, 0, 0, true)._tangent = 0;
                    var frames = new[] { 0, 2, 5, 9, clip.FrameCount - 1 }.Distinct().OrderBy(f => f).ToArray();
                    var values = new[] { 1.25f, -.5f, .75f, -1.125f, .25f };
                    var tangents = new[] { .125f, -.25f, .375f, -.125f, .25f };
                    while (track.KeyArrays[3].KeyCount > 0) track.KeyArrays[3].Remove(track.KeyArrays[3].Entries.First()._index);
                    for (int i = 0; i < frames.Length; i++)
                        track.Keyframes.SetFrameValue(3, frames[i], values[i] + (highQuality ? .0012345f * (i + 1) : 0), true)._tangent = tangents[i] + (highQuality ? .0000003f : 0);
                }
                track.Keyframes.PreserveInterpolation = true; PreciseCHR0EntryNode.Promote(clip); clip.SignalPropertyChange();
                Call(form, "InvalidateControls"); Call(form, "MarkFighterChanged"); editor.UpdateModel();
            }
            Author(false); editor.SelectedBone = bone; editor.SetFrame(3); editor.UpdateModel(); editor.FrameFighterModel(); await SimpleControlsReady(form);
            var siblings = Walk(clip.RootNode).OfType<CHR0Node>().Where(c => !ReferenceEquals(c, clip)).ToDictionary(c => c, SetNativeContent);
            var untouched = OptimizationUntouchedBytes(form, clip);
            string content = SetNativeContent(clip), originalStorage = NativeAnimationOptimization.StorageKey(clip), camera = CompactCamera(form);
            int undo = editor._undoSaves.Count, frameBefore = editor.CurrentFrame, assets = project.Animations.Count, keys = OptimizationKeyCount(clip);
            string identity = OptimizationNativeIdentity(clip);
            var witness = new GameLibraryDocument(form);

            async Task Open(Func<AnimationOptimizationDialog, Task> exercise)
            {
                bool finished = false; Exception failure = null;
                form.OptimizationReady = async window =>
                {
                    try { await exercise(window); } catch (Exception error) { failure = error; }
                    finally { if (!window.IsDisposed) window.Close(); finished = true; }
                };
                Call(form, "ShowAnimationOptimization");
                await GameLibraryWait(() => finished, "Compact optimization dialog did not finish.", 120);
                form.OptimizationReady = null; if (failure != null) throw failure;
                try { await SimpleControlsReady(form); }
                catch (TimeoutException)
                {
                    Console.WriteLine("TRACE compact post-dialog controls timeout: " +
                        "busy=" + Field(form, "_controlBusy") + ", queued=" + Field(form, "_controlSetupQueued") +
                        ", reload=" + Field(form, "_controlReloadRequested") + ", epoch=" + Field(form, "_controlEpoch") +
                        ", requests=" + form.ControlLoadRequests + ", frame=" + editor.CurrentFrame +
                        ", sameClip=" + ReferenceEquals(editor.SelectedCHR0, clip) + ", tracks=" + clip.Children.Count +
                        ", status=" + ((ToolStripStatusLabel)Field(form, "_fighterStatus")).Text);
                    throw;
                }
            }

            await Open(async window =>
            {
                var compact = SimpleControl<CheckBox>(window, "optimizationCompactStorage");
                Check(!compact.Checked && !window.ReadCompactStorage() && !window.ReadHighQuality(), "Smaller storage is an explicit option; Exact stays the default");
                compact.Checked = true; int prepares = form.OptimizationPrepares;
                Check(window.Candidate == null && form.OptimizationPrepares == prepares && !SimpleControl<Button>(window, "optimizeApply").Enabled,
                    "Selecting smaller storage invalidates preparation without starting work");
                var realPrepare = window.Prepare; var pending = new TaskCompletionSource<CHR0Node>(); window.Prepare = quality => pending.Task;
                var cancelled = window.Analyze(); SimpleControl<Button>(window, "optimizationCancel").PerformClick();
                pending.SetResult(NativeAnimationOptimization.Copy(clip)); await cancelled; window.Prepare = realPrepare;
                Check(!window.Busy && window.Candidate == null && !window.PreparedCanApply && SimpleControl<Button>(window, "optimizeAnalyze").Focused,
                    "Cancelled compact analysis rejects late publication and recovers focus");
                witness.CheckPreserved("cancelled compact preparation");
                await window.Analyze();
                Check(window.Candidate != null && window.PreparedCanApply && SetNativeContent(window.Candidate) == content && OptimizationKeyCount(window.Candidate) == keys &&
                    NativeAnimationOptimization.StorageKey(window.Candidate) != originalStorage,
                    "Exact compact analysis prepares a useful format-only result with every original metadata field, knot, tangent, track and flag unchanged");
                witness.CheckPreserved("format-only compact preparation");
                compact.Checked = false;
                Check(window.Candidate == null && !window.PreparedCanApply && !SimpleControl<Button>(window, "optimizeApply").Enabled,
                    "Changing the storage option discards the candidate and prevents settings-stale Apply");
                compact.Checked = true; await window.Analyze();
                var track = (CHR0EntryNode)clip.Children[0]; var prior = PreciseCHR0EntryNode.CaptureStorage(track);
                var alternative = NativeCompactAnimationStorage.CreateAlternatives(track, 1, true, true, new int[0], 1).Last();
                NativeCompactAnimationStorage.ApplyGroup(clip, alternative); string changedStorage = NativeAnimationOptimization.StorageKey(clip);
                SimpleControl<Button>(window, "optimizeApply").PerformClick();
                Check(window.Candidate == null && SetNativeContent(clip) == content && NativeAnimationOptimization.StorageKey(clip) == changedStorage && editor._undoSaves.Count == undo,
                    "A storage-only dependency change rejects prepared Apply without adding Undo or replacing current data");
                PreciseCHR0EntryNode.RestoreStorage((CHR0EntryNode)clip.Children[0], prior);
            });
            Check(SetNativeContent(clip) == content && NativeAnimationOptimization.StorageKey(clip) == originalStorage && CompactCamera(form) == camera,
                "Cancellation, settings changes and stale-format rejection retain the current animation and native camera");

            string packedStorage = null;
            await Open(async window =>
            {
                SimpleControl<CheckBox>(window, "optimizationCompactStorage").Checked = true; await window.Analyze();
                Check(window.Candidate != null && window.PreparedCanApply, "Fresh exact compact analysis prepares after stale work closes");
                packedStorage = NativeAnimationOptimization.StorageKey(window.Candidate);
                foreach (int text in new[] { 100, 175 }) foreach (var size in new[] { new Size(980, 720), new Size(1600, 960) })
                {
                    Call(form, "SetWorkspaceTextSize", text); window.ApplyTextSize(); window.ClientSize = size; window.PerformLayout();
                    foreach (string controlName in new[] { "optimizationCompactStorage", "optimizationMode", "optimizeAnalyze", "optimizeApply", "optimizationClose", "optimizationFrame" })
                    {
                        var control = SimpleControl<Control>(window, controlName);
                        Check(control.Visible && window.ClientRectangle.Contains(window.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))),
                            "Compact storage control fits " + controlName + " / " + size.Width + " / " + text);
                    }
                    await CaptureOptimization(window, Path.Combine(output, "compact-optimization-" + size.Width + "-" + text + ".png"));
                }
                Call(form, "SetWorkspaceTextSize", 100); window.ApplyTextSize(); window.ClientSize = new Size(1060, 730);
                SimpleControl<Button>(window, "optimizationDetails").PerformClick();
                File.WriteAllText(Path.Combine(output, "compact-optimization-report.txt"), SimpleControl<TextBox>(window, "optimizationReport").Text);
                await CaptureOptimization(window, Path.Combine(output, "compact-optimization-ready.png"));
                SimpleControl<Button>(window, "optimizeApply").PerformClick();
            });
            Check(ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.TargetModel, model) && ReferenceEquals(editor.SelectedBone, bone) &&
                editor.CurrentFrame == frameBefore && project.Animations.Count == assets && CompactCamera(form) == camera && SetNativeContent(clip) == content &&
                NativeAnimationOptimization.StorageKey(clip) == packedStorage && editor._undoSaves.Count == undo + 1,
                "Format-only Apply changes storage through one native Undo while preserving clip/target/bone/frame/camera/asset identities and exact curves");
            Check(siblings.All(p => SetNativeContent(p.Key) == p.Value), "Compact Apply retains every sibling animation");
            editor.Undo(); await SimpleControlsReady(form);
            Check(SetNativeContent(clip) == content && NativeAnimationOptimization.StorageKey(clip) == originalStorage,
                "Native Undo restores original storage as well as the exact unchanged curve data");
            editor.Redo(); await SimpleControlsReady(form);
            Check(SetNativeContent(clip) == content && NativeAnimationOptimization.StorageKey(clip) == packedStorage,
                "Native Redo restores the verified compact storage without analysis");

            string exported = Path.Combine(output, "compact-unsaved-current.chr0"); form.ExportSimpleWorkspaceResult(exported);
            using (var decoded = NodeFactory.FromFile(null, exported) as CHR0Node)
                Check(decoded != null && SetNativeContent(decoded) == content && NativeAnimationOptimization.StorageKey(decoded) == packedStorage,
                    "Export before project Save independently retains all coefficients and the verified smaller storage");
            var frozen = (IDisposable)Call(form, "FreezeWorkspaceExport");
            string existing = Path.Combine(output, "stale-storage-export.chr0"); File.WriteAllText(existing, "Independent existing destination"); string oldHash = FighterProject.HashFile(existing);
            var owned = (CHR0EntryNode)clip.Children[0]; var storageBefore = PreciseCHR0EntryNode.CaptureStorage(owned);
            try
            {
                var changed = NativeCompactAnimationStorage.CreateAlternatives(owned, 1, true, true, new int[0], 1).Last();
                NativeCompactAnimationStorage.ApplyGroup(clip, changed); bool stale = false;
                try { Call(form, "ExportWorkspaceSnapshot", frozen, existing); } catch (System.Reflection.TargetInvocationException error) { stale = error.InnerException is InvalidOperationException; }
                Check(stale && FighterProject.HashFile(existing) == oldHash && SetNativeContent(clip) == content,
                    "Verified export freezes storage identity and rejects a format-stale snapshot while retaining an existing destination");
            }
            finally { PreciseCHR0EntryNode.RestoreStorage((CHR0EntryNode)clip.Children[0], storageBefore); frozen.Dispose(); }
            int repeated = form.OptimizationPrepares;
            await Open(async window =>
            {
                SimpleControl<CheckBox>(window, "optimizationCompactStorage").Checked = true; await window.Analyze();
                Check(window.Candidate != null && !window.PreparedCanApply && NativeAnimationOptimization.StorageKey(window.Candidate) == packedStorage &&
                    SetNativeContent(window.Candidate) == content && form.OptimizationPrepares == repeated && OptimizationRetainedBaseline(form) == content,
                    "Repeated compact optimization uses its retained session baseline/cache without cumulative approximation or another engine pass");
            });
            string saved = Path.Combine(output, "compact-current.fpstudio"); form.SaveFighterProject(saved);
            OptimizationCheckUntouchedBytes(form, clip, untouched, "compact project Save", Path.Combine(output, "compact-siblings-after-save.json"));
            using (var fresh = FighterProject.Open(saved)) using (var reopened = new ModelForm())
            {
                reopened.InitializeFighterProject(fresh); reopened.Show();
                await GameLibraryWait(() => reopened.FighterEditor.TargetModel is MDL0Node && reopened.FighterEditor.SelectedCHR0 != null && OptimizationNativeIdentity(reopened.FighterEditor.SelectedCHR0) == identity,
                    "Compact project did not restore its exact selected member.", 60); await SimpleControlsReady(reopened);
                var restored = reopened.FighterEditor.SelectedCHR0;
                Check(SetNativeContent(restored) == content && NativeAnimationOptimization.StorageKey(restored) == packedStorage,
                    "Project Save/fresh reopen retains the exact animation identity, curves and compact native storage");
                OptimizationCheckUntouchedBytes(reopened, restored, untouched, "compact project reopen", Path.Combine(output, "compact-siblings-after-reopen.json"));
                SimpleDisposeForm(reopened);
            }

            int savedCursor = editor._saveIndex;
            Check(ReferenceEquals(editor.SelectedCHR0, clip) && NativeAnimationOptimization.StorageKey(clip) == packedStorage && savedCursor > 0,
                "The original editable member retains its packed storage and applied native command after project Save/rebuild");
            editor.Undo(); await SimpleControlsReady(form);
            Check(ReferenceEquals(editor.SelectedCHR0, clip) && SetNativeContent(clip) == content && NativeAnimationOptimization.StorageKey(clip) == originalStorage &&
                editor._saveIndex == savedCursor - 1 && PreciseCHR0EntryNode.CaptureStorage((CHR0EntryNode)clip.Children[0]) == null,
                "Format-only Undo after project Save/rebuild restores authoritative I12 without reviving stale matching packed bytes");
            string afterSaveUndo = Path.Combine(output, "precise-after-save-undo.chr0"); form.ExportSimpleWorkspaceResult(afterSaveUndo);
            using (var decoded = NodeFactory.FromFile(null, afterSaveUndo) as CHR0Node)
                Check(decoded != null && SetNativeContent(decoded) == content && NativeAnimationOptimization.StorageKey(decoded) == originalStorage &&
                    new FileInfo(afterSaveUndo).Length > new FileInfo(exported).Length,
                    "Verified export after saved-format Undo independently reopens precise storage and exact unchanged coefficients");
            editor.Redo(); await SimpleControlsReady(form);
            Check(ReferenceEquals(editor.SelectedCHR0, clip) && SetNativeContent(clip) == content && NativeAnimationOptimization.StorageKey(clip) == packedStorage && editor._saveIndex == savedCursor,
                "Format-only Redo after project Save/rebuild restores the same verified packed policy through the existing native command");

            Author(false, true); await SimpleControlsReady(form); int bindUndo = editor._undoSaves.Count;
            await Open(async window =>
            {
                SimpleControl<CheckBox>(window, "optimizationCompactStorage").Checked = true; await window.Analyze();
                Check(window.Candidate != null && window.PreparedCanApply && window.Candidate.Children.Count == 0 && SimpleControl<TextBox>(window, "optimizationReport").Text.Contains("Exact bind-pose tracks to remove: " + name),
                    "The compact tool lists the exact bind-equivalent animation track before removing it; the model bone remains intact");
                SimpleControl<Button>(window, "optimizeApply").PerformClick();
            });
            Check(clip.Children.Count == 0 && model.AllBones.Any(b => ReferenceEquals(b, bone)) && editor._undoSaves.Count == bindUndo + 1,
                "Bind cleanup applies the smaller empty-track animation with one Undo and keeps every target bone");
            editor.Undo(); await SimpleControlsReady(form);
            Check(clip.Children.Count == 1 && ((CHR0EntryNode)clip.Children[0]).Name == name, "Native Undo restores the removed bind-equivalent animation track");

            Author(true); await SimpleControlsReady(form); string qualityBaseline = SetNativeContent(clip), qualityOutput = null, qualityStorage = null;
            string qualityOriginalStorage = NativeAnimationOptimization.StorageKey(clip);
            int qualityUndo = editor._undoSaves.Count, qualityUndoCursor = editor._saveIndex;
            using (var source = NativeAnimationOptimization.Copy(clip))
            await Open(async window =>
            {
                SimpleControl<ComboBox>(window, "optimizationMode").SelectedIndex = 1;
                SimpleControl<CheckBox>(window, "optimizationCompactStorage").Checked = true; await window.Analyze();
                Check(window.Candidate != null && window.PreparedCanApply && window.ReadHighQuality() && window.ReadCompactStorage(),
                    "High-quality compact analysis prepares an explicitly chosen independently bounded candidate");
                qualityOutput = SetNativeContent(window.Candidate); qualityStorage = NativeAnimationOptimization.StorageKey(window.Candidate);
                Check(qualityOutput != qualityBaseline && qualityStorage != NativeAnimationOptimization.StorageKey(clip),
                    "Arbitrary-float fixture exercises accepted quantized native storage rather than only exact packing");
                OptimizationWithheldGeometry(window.SourceView, window.TargetView, source, window.Candidate, Path.Combine(output, "compact-hq-withheld-geometry.json"));
                SimpleControl<Button>(window, "optimizationDetails").PerformClick();
                File.WriteAllText(Path.Combine(output, "compact-hq-report.txt"), SimpleControl<TextBox>(window, "optimizationReport").Text);
                await CaptureOptimization(window, Path.Combine(output, "compact-hq-ready.png"));
                var summary = SimpleControl<Label>(window, "optimizationSummary"); var report = SimpleControl<TextBox>(window, "optimizationReport");
                SimpleControl<Button>(window, "optimizeApply").PerformClick();
                File.WriteAllText(Path.Combine(output, "compact-hq-apply-diagnostic.json"), new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 16000000 }.Serialize(new {
                    DialogResult = window.DialogResult.ToString(), Summary = summary.Text, Report = report.Text,
                    ContentMatches = SetNativeContent(clip) == qualityOutput, StorageMatches = NativeAnimationOptimization.StorageKey(clip) == qualityStorage,
                    OriginalBaselineMatches = OptimizationRetainedBaseline(form) == qualityBaseline,
                    UndoCountBefore = qualityUndo, UndoCursorBefore = qualityUndoCursor, UndoCountAfter = editor._undoSaves.Count,
                    UndoCursorAfter = editor._saveIndex, RedoCountAfter = editor._redoSaves.Count, editor._allowedUndos,
                    ExpectedBaseline = qualityBaseline, ActualBaseline = OptimizationRetainedBaseline(form),
                    ExpectedContent = qualityOutput, ActualContent = SetNativeContent(clip), ExpectedStorage = qualityStorage, ActualStorage = NativeAnimationOptimization.StorageKey(clip)
                }));
                Check(window.DialogResult == DialogResult.OK, "High-quality compact Apply completes successfully instead of closing after a reported error");
            });
            Check(SetNativeContent(clip) == qualityOutput && NativeAnimationOptimization.StorageKey(clip) == qualityStorage,
                "High-quality compact Apply retains the exact independently qualified native content and storage");
            int qualityExpectedCursor = (int)Math.Min(editor._allowedUndos, (uint)qualityUndoCursor + 1);
            Check(editor._saveIndex == qualityExpectedCursor && editor._undoSaves.Count == qualityExpectedCursor && editor._redoSaves.Count == qualityExpectedCursor && !editor.CanRedo,
                "High-quality compact Apply adds one native command at the applied cursor and discards the undone bind-cleanup redo branch");
            Check(OptimizationRetainedBaseline(form) == qualityBaseline, "High-quality compact Apply retains the complete identified original session baseline");
            editor.Undo(); await SimpleControlsReady(form);
            Check(SetNativeContent(clip) == qualityBaseline && NativeAnimationOptimization.StorageKey(clip) == qualityOriginalStorage,
                "One native Undo restores every original high-quality input coefficient and its precise storage");
            editor.Redo(); await SimpleControlsReady(form);
            Check(SetNativeContent(clip) == qualityOutput && NativeAnimationOptimization.StorageKey(clip) == qualityStorage && editor._saveIndex == qualityExpectedCursor,
                "One native Redo restores the exact reviewed high-quality packed result");
            var qualityEntry = (CHR0EntryNode)clip.Children[0]; var savedQualityPolicy = PreciseCHR0EntryNode.CaptureStorage(qualityEntry);
            int beforeUnpackedPrepare = form.OptimizationPrepares, beforeUnpackedCursor = editor._saveIndex;
            try
            {
                PreciseCHR0EntryNode.RestoreStorage(qualityEntry, null);
                Check(SetNativeContent(clip) == qualityOutput && NativeAnimationOptimization.StorageKey(clip) != qualityStorage,
                    "The HQ cache fixture changes only storage, retaining every already optimized coefficient");
                await Open(async window =>
                {
                    Check(OptimizationRetainedBaseline(form) == qualityBaseline,
                        "Removing packed storage alone preserves the original HQ session baseline before reanalysis");
                    SimpleControl<ComboBox>(window, "optimizationMode").SelectedIndex = 1;
                    SimpleControl<CheckBox>(window, "optimizationCompactStorage").Checked = true; await window.Analyze();
                    Check(window.Candidate != null && window.PreparedCanApply && form.OptimizationPrepares > beforeUnpackedPrepare &&
                        SetNativeContent(window.Candidate) == qualityOutput && NativeAnimationOptimization.StorageKey(window.Candidate) == qualityStorage &&
                        OptimizationRetainedBaseline(form) == qualityBaseline && SimpleControl<TextBox>(window, "optimizationReport").Text.Contains("Baseline: retained original session animation"),
                        "Storage-only unpacking reanalyzes against the original HQ baseline and reproduces the same bounded packed result without cumulative approximation");
                });
                Check(SetNativeContent(clip) == qualityOutput && NativeAnimationOptimization.StorageKey(clip) != qualityStorage && editor._saveIndex == beforeUnpackedCursor,
                    "Closing unpacked HQ reanalysis preserves the current semantic result, explicit precise storage and native Undo cursor");
            }
            finally { PreciseCHR0EntryNode.RestoreStorage((CHR0EntryNode)clip.Children[0], savedQualityPolicy); }
            repeated = form.OptimizationPrepares;
            await Open(async window =>
            {
                SimpleControl<ComboBox>(window, "optimizationMode").SelectedIndex = 1; SimpleControl<CheckBox>(window, "optimizationCompactStorage").Checked = true;
                await window.Analyze(); Check(window.Candidate != null && !window.PreparedCanApply && form.OptimizationPrepares == repeated &&
                    SetNativeContent(window.Candidate) == qualityOutput && NativeAnimationOptimization.StorageKey(window.Candidate) == qualityStorage && OptimizationRetainedBaseline(form) == qualityBaseline,
                    "Repeated high-quality compact analysis reuses its exact prior output and original baseline without cumulative approximation");
            });
            string qualityExport = Path.Combine(output, "compact-hq-current.chr0"); form.ExportSimpleWorkspaceResult(qualityExport);
            using (var decoded = NodeFactory.FromFile(null, qualityExport) as CHR0Node)
                Check(decoded != null && SetNativeContent(decoded) == qualityOutput && NativeAnimationOptimization.StorageKey(decoded) == qualityStorage,
                    "Independent export/reopen retains the reviewed high-quality coefficient tuples and compact formats exactly");
            Check(siblings.All(p => SetNativeContent(p.Key) == p.Value) && originals.All(p => FighterProject.HashFile(p.Key) == p.Value),
                "Compact optimization, cache reuse, export, Save, reopen and bind cleanup preserve original files and every sibling animation");
            await WorkspaceImportedDenseOptimization(form, project, model, name, output);
        }
        finally
        {
            Call(form, "SetWorkspaceTextSize", 100); form.OptimizationReady = null;
            if (!form.IsDisposed) SimpleDisposeForm(form); project.Dispose();
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previousLibrary);
        }
    }
}
