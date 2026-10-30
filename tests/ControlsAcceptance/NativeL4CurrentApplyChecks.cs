using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Models;
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
    // This explicitly authors the current target. It runs only in the separate
    // actual-Library-Use opt-in case, never inside export-copy preservation tests.
    // Analyze obtains its own genuine frozen master/session provenance normally.
    static async Task NativeL4CurrentApply(ModelForm form, CHR0Node clip, string output, Dictionary<string, string> originals)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("New L4 current Apply requires the private desktop.");
        Directory.CreateDirectory(output);
        var editor = form.FighterEditor; var model = (MDL0Node)editor.TargetModel;
        var project = form.FighterProject; var asset = (FighterAsset)Call(form, "SelectedFighterAsset");
        var setup = await Await<ControlReply>(Call(form, "LoadControls")); await SimpleControlsReady(form);
        var roots = (setup.ProtectedMotionBones ?? new int[0]).Select(index => setup.BoneNames[index]).ToArray();
        Check(roots.Length > 0 && asset.Retarget != null && NativeGeneratedPairRegistry.Session.Resolve(clip, NativeGeneratedPairRegistry.TryRigKey(model)).Length > 0,
            "New L4 Apply starts from the actual reviewed Library Use result and its genuine controls/writer provenance");
        editor.SelectedBone = model.AllBones.Single(bone => bone.Name == setup.BoneNames[setup.Roles.Single(role => role.Role == "Hips").Bone.Value]);
        editor.SetFrame(Math.Min(3, clip.FrameCount)); editor.UpdateModel(); await SimpleControlsReady(form);
        string before = SetNativeContent(clip), beforeStorage = NativeAnimationOptimization.StorageKey(clip);
        string identity = OptimizationNativeIdentity(clip), recipe = LegacyReviewRecipe(asset), camera = CompactCamera(form);
        string modelContent = (string)Call(form, "NativeControlHash", model, ".mdl0"), fingerprint = BrawlRetargeter.SkeletonFingerprint(model);
        var boneSelection = editor.SelectedBone; int frame = editor.CurrentFrame, cursor = editor._saveIndex, undoCount = editor._undoSaves.Count, assetCount = project.Animations.Count;
        var history = editor._undoSaves.ToArray(); var siblings = OptimizationUntouchedBytes(form, clip);
        string masterFile = Path.Combine(output, "current-before.chr0"); form.ExportSimpleWorkspaceResult(masterFile);
        Check(new FileInfo(masterFile).Length == 81140 && FighterProject.HashFile(masterFile) == "582c4b69a438cdd8b5dcf59a411c5a6f437acbe575256e6ff4cd7d95dfd75a45",
            "New L4 Apply fixture is the exact actual source-channel master already sealed for preview63/64, not a reimported candidate");
        string optimized = null, optimizedStorage = null; int groups = 0; NativeExportOptimizationSettings settings = null;
        var captures = new List<string>();
        bool complete = false; Exception failure = null;
        using (var baseline = NativeAnimationOptimization.CopySource(clip))
        {
            void Dependencies(string phase)
            {
                Check(ReferenceEquals(editor.TargetModel, model) && ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.TargetAnimation, clip) &&
                    ReferenceEquals(editor.SelectedBone, boneSelection) && editor.CurrentFrame == frame && OptimizationNativeIdentity(clip) == identity &&
                    CompactCamera(form) == camera && project.Animations.Count == assetCount,
                    "New L4 current Apply " + phase + " preserves exact native member/model/bone identity, frame, camera and ownership");
                Check((string)Call(form, "NativeControlHash", model, ".mdl0") == modelContent && BrawlRetargeter.SkeletonFingerprint(model) == fingerprint &&
                    LegacyReviewRecipe(asset) == recipe && originals.All(pair => FighterProject.HashFile(pair.Key) == pair.Value),
                    "New L4 current Apply " + phase + " preserves authentic binds/all weights, historical retarget recipe and original source bytes");
            }
            form.OptimizationReady = async window =>
            {
                try
                {
                    SimpleControl<ComboBox>(window, "exportOptimizationPolicy").SelectedIndex = 1;
                    settings = window.ReadExportSettings();
                    Check(settings.Policy == NativeExportOptimizationPolicy.ErrorBounded && settings.SearchEffort == NativeExportSearchEffort.Standard &&
                        settings.RotationDegrees == .1 && settings.Translation == .002 && settings.Scale == .0001 && settings.GeometryRelative == .001 &&
                        settings.RotationDerivative == .1 && settings.TranslationDerivative == .002 && settings.ScaleDerivative == .0001 && settings.ExactMandatoryFitJoins,
                        "New L4 current Apply uses existing bounded defaults without a relaxed quality/search profile");
                    await window.Analyze();
                    Check(window.Candidate != null && SimpleControl<Button>(window, "optimizeApplyCurrent").Enabled,
                        "The existing optimizer dialog prepares an independently verified current-animation Apply through its normal Analyze contract");
                    groups = window.Candidate.Children.OfType<CHR0EntryNode>().Count(entry => PreciseCHR0EntryNode.CaptureStorage(entry)?.Format(1) == AnimDataFormat.L4);
                    Check(groups > 0 && window.PreparedReport.Contains("Published-copy policy: ErrorBounded") && window.PreparedReport.Contains("L4 phase ledger"),
                        "The genuine fresh prepared Apply candidate contains actual newly accepted L4 groups, not an I12 fallback or imported permission");
                    optimized = SetNativeContent(window.Candidate); optimizedStorage = NativeAnimationOptimization.StorageKey(window.Candidate);
                    ReplayRootsMatch(baseline, window.Candidate, model, roots);
                    Check(SetNativeContent(clip) == before && NativeAnimationOptimization.StorageKey(clip) == beforeStorage && editor._saveIndex == cursor && editor._undoSaves.SequenceEqual(history),
                        "Real bounded Analyze leaves the live authoring master and native history unchanged before explicit current Apply");
                    int originalText = WorkspaceAppearance.TextPercent;
                    Size originalSize = window.ClientSize;
                    var previewFrame = SimpleControl<TrackBar>(window, "optimizationFrame"); int originalPreviewFrame = previewFrame.Value;
                    var cameras = new[] { window.SourceView.Viewport.CurrentViewport.Camera, window.TargetView.Viewport.CurrentViewport.Camera }
                        .Select(camera => new { Camera = camera, Matrix = camera._matrix, Inverse = camera._matrixInverse,
                            Rotation = camera._rotation, Scale = camera._scale, Ortho = camera._ortho,
                            Projection = camera._projectionMatrix, ProjectionInverse = camera._projectionInverse,
                            Width = camera._width, Height = camera._height, Aspect = camera._aspect, Dimensions = camera._orthoDimensions }).ToArray();
                    try
                    {
                        foreach (int text in new[] { 100, 175 }) foreach (int width in new[] { 1050, 1700 })
                        {
                            Call(form, "SetWorkspaceTextSize", text); window.ApplyTextSize(); window.ClientSize = new Size(width, width == 1050 ? 850 : 1000); window.PerformLayout();
                            Check(WorkspaceAppearance.TextPercent == text, "Genuine new L4 dialog capture applies the declared workspace text size: " + text);
                            foreach (string name in new[] { "optimizeAnalyze", "optimizeApply", "optimizeApplyCurrent", "optimizationClose" })
                                Check(StandaloneControlReachable(SimpleControl<Button>(window, name)), "Genuine new L4 Apply/footer remains reachable at " + width + "/" + text + ": " + name);
                            string capture = "new-l4-current-" + width + "-" + text + ".png";
                            await CaptureOptimization(window, Path.Combine(output, capture)); captures.Add(capture);
                        }
                    }
                    finally
                    {
                        Call(form, "SetWorkspaceTextSize", originalText); window.ApplyTextSize(); window.ClientSize = originalSize; window.PerformLayout(); previewFrame.Value = originalPreviewFrame;
                        foreach (var state in cameras)
                        {
                            state.Camera._matrix = state.Matrix; state.Camera._matrixInverse = state.Inverse;
                            state.Camera._rotation = state.Rotation; state.Camera._scale = state.Scale; state.Camera._ortho = state.Ortho;
                            state.Camera._projectionMatrix = state.Projection; state.Camera._projectionInverse = state.ProjectionInverse;
                            state.Camera._width = state.Width; state.Camera._height = state.Height; state.Camera._aspect = state.Aspect;
                            state.Camera._orthoDimensions = state.Dimensions;
                        }
                    }
                    File.WriteAllText(Path.Combine(output, "analyze-report.txt"), window.PreparedReport);
                    SimpleControl<Button>(window, "optimizeApplyCurrent").PerformClick();
                    Check(window.IsDisposed || window.DialogResult == DialogResult.OK, "The real Apply-to-current button completes without an export chooser");
                }
                catch (Exception error) { failure = error; }
                finally { if (!window.IsDisposed) window.Close(); complete = true; }
            };
            try { Call(form, "ShowAnimationOptimization"); await GameLibraryWait(() => complete, "New L4 current Apply dialog did not finish."); }
            finally { form.OptimizationReady = null; }
            if (failure != null) throw failure;
            await SimpleControlsReady(form);
            Check(SetNativeContent(clip) == optimized && NativeAnimationOptimization.StorageKey(clip) == optimizedStorage &&
                editor._saveIndex == cursor + 1 && editor._undoSaves.Count == undoCount + 1,
                "New L4 explicit Apply changes only the current animation including immutable storage through exactly one existing native command");
            Check(OptimizationRetainedBaseline(form) == before, "New L4 authoring Apply retains the original full-quality session baseline");
            Dependencies("after explicit Apply"); ReplayRootsMatch(baseline, clip, model, roots);
            int preparations = form.OptimizationPrepares;
            editor.Undo(); await SimpleControlsReady(form);
            Check(SetNativeContent(clip) == before && NativeAnimationOptimization.StorageKey(clip) == beforeStorage && editor._saveIndex == cursor,
                "Native Undo restores the exact pre-L4 master, flags/tangents and original compact storage");
            Dependencies("after native Undo");
            editor.Redo(); await SimpleControlsReady(form);
            Check(SetNativeContent(clip) == optimized && NativeAnimationOptimization.StorageKey(clip) == optimizedStorage && editor._saveIndex == cursor + 1 &&
                editor._undoSaves.Count == undoCount + 1 && form.OptimizationPrepares == preparations,
                "Native Redo restores the same accepted L4 payload in one command without rerunning optimization");
            Dependencies("after native Redo"); ReplayRootsMatch(baseline, clip, model, roots);
            using (var repeat = (IDisposable)Call(form, "FreezeOwnedWorkspaceExport", asset, clip, true, null))
                Check(SetNativeContent((CHR0Node)Field(repeat, "Master")) == before && SetNativeContent((CHR0Node)Field(repeat, "Candidate")) == optimized &&
                    OptimizationRetainedBaseline(form) == before,
                    "The real subsequent optimization admission freezes the retained original master while ordinary export keeps the applied result; cumulative approximation is not rebased");
            string exported = Path.Combine(output, "current-applied-l4.chr0");
            var nativeExport = editor.FighterNativeExportRequested;
            Check(nativeExport != null, "Applied new L4 retains the actual native animation-list/right-click Export callback");
            int nativeChecks = form.OptimizationNativeChecks;
            nativeExport(clip, exported);
            using (var decoded = NodeFactory.FromFile(null, exported) as CHR0Node)
                Check(decoded != null && SetNativeContent(decoded) == optimized && NativeAnimationOptimization.StorageKey(decoded) == optimizedStorage &&
                    decoded.Children.OfType<CHR0EntryNode>().Count(entry => PreciseCHR0EntryNode.CaptureStorage(entry)?.Format(1) == AnimDataFormat.L4) == groups,
                    "Actual native animation-list Export/fresh reopen preserves the explicitly applied new L4 result and complete storage");
            Check(form.OptimizationPrepares == preparations && form.OptimizationNativeChecks == nativeChecks &&
                SetNativeContent(clip) == optimized && NativeAnimationOptimization.StorageKey(clip) == optimizedStorage && editor._saveIndex == cursor + 1,
                "Actual native animation-list Export performs zero optimization preparation/geometry analysis and preserves applied content/storage/native history");
            Dependencies("after actual native animation-list Export");
            OptimizationCheckUntouchedBytes(form, clip, siblings, "new L4 explicit current Apply/Undo/Redo/export", Path.Combine(output, "unchanged-siblings.json"));
            File.WriteAllText(Path.Combine(output, "current-l4-apply-receipt.json"), new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 }.Serialize(new {
                BeforeFile = masterFile, BeforeSha256 = FighterProject.HashFile(masterFile), BeforeBytes = new FileInfo(masterFile).Length,
                AppliedFile = exported, AppliedSha256 = FighterProject.HashFile(exported), AppliedBytes = new FileInfo(exported).Length,
                NativeIdentity = identity, NativeCommandsAdded = editor._saveIndex - cursor, L4Groups = groups,
                OriginalFullQualityBaselineRetained = OptimizationRetainedBaseline(form) == before, Settings = settings,
                Captures = captures.Select(name => new { Name = name, Sha256 = FighterProject.HashFile(Path.Combine(output, name)) }).ToArray(), SharedOffscreenRenderer = true,
                OriginalInputHashes = originals, PrivateDesktop, OSInputInjected = false, ActiveDesktopCapture = false,
                Scope = "Actual same-session Library Use -> real bounded Analyze -> explicit current authoring Apply/Undo/Redo/actual animation-list Export. No fabricated provenance; no game/physical-input claim." }));
        }
    }
}
