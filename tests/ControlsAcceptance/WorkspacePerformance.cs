using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    // Frozen before tuning: use the actual owner-STA routes, eight warmups and
    // 31 observations per workload. Timings describe this workload; they are
    // not display FPS, physical-input latency or a hardware-independent gate.
    // Correctness gates: stable status performs no new geometry/measurement
    // passes, hidden comparison performs no source samples, and navigation and
    // sampling do not load/hash/bake or change authored document/Undo state.
    const int PerformanceWarmups = 8, PerformanceObservations = 31;
    static readonly string[] PerformanceCounters = {
        "DiagnosticWorkspaceLayouts", "DiagnosticWorkspaceBarMeasurements",
        "DiagnosticWorkspaceLibraryMeasurements", "ComparisonSampleCount",
        "ControlSelectionEvents", "ControlSelectionInvalidations", "ControlLoadRequests"
    };

    static long? PerformanceCounter(ModelForm form, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var field = form.GetType().GetField(name, flags);
        if (field != null) return Convert.ToInt64(field.GetValue(form));
        var property = form.GetType().GetProperty(name, flags);
        return property == null ? (long?)null : Convert.ToInt64(property.GetValue(form));
    }

    static Dictionary<string, long?> PerformanceCounts(ModelForm form) =>
        PerformanceCounters.ToDictionary(n => n, n => PerformanceCounter(form, n));

    static Dictionary<string, long?> PerformanceDelta(Dictionary<string, long?> before, Dictionary<string, long?> after) =>
        before.ToDictionary(p => p.Key, p => p.Value.HasValue && after[p.Key].HasValue ? after[p.Key] - p.Value : null);

    static object PerformanceMeasure(string workload, Action action, ModelForm form, Func<int> layoutEvents)
    {
        for (int i = 0; i < PerformanceWarmups; i++) action();
        var before = PerformanceCounts(form); int layouts = layoutEvents();
        var elapsed = new double[PerformanceObservations];
        for (int i = 0; i < elapsed.Length; i++)
        {
            long start = Stopwatch.GetTimestamp(); action();
            elapsed[i] = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
        }
        var sorted = elapsed.OrderBy(x => x).ToArray();
        var delta = PerformanceDelta(before, PerformanceCounts(form));
        if (workload == "stable-workspace-status")
            foreach (string name in PerformanceCounters.Take(3))
                if (delta[name].HasValue) Check(delta[name] == 0, "Stable status performs zero new " + name + " passes");
        return new { Workload = workload, Observations = elapsed.Length, Warmups = PerformanceWarmups,
            Unit = "milliseconds", Median = sorted[sorted.Length / 2],
            P95 = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], Samples = elapsed,
            Counters = delta, OuterLayoutEvents = layoutEvents() - layouts };
    }

    static string PerformancePose(MDL0Node model) => new JavaScriptSerializer().Serialize(
        model._linker.BoneCache.Select(b => Enumerable.Range(0, 9).Select(c => b.FrameState[c]).ToArray()).ToArray());

    static async Task WorkspacePerformance(string[] args, string output)
    {
        if (!PrivateDesktop || !args.Contains("--retarget-source"))
            throw new InvalidOperationException("Workspace performance needs the private desktop and authentic donor model.");
        string donor = args[Array.IndexOf(args, "--retarget-source") + 1];
        string previousLibrary = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "library"));
        var measurements = new List<object>(); var observations = new List<object>();
        var inputHashes = new[] { args[0], args[1], donor }.Distinct().ToDictionary(p => p, FighterProject.HashFile);
        object fixture = null;
        ModelForm form = null;
        bool completed = false;
        try
        {
            using (var startup = MainForm.StartViewerApplication())
            {
                form = (ModelForm)Field(MainForm.Instance, "_fighterForm");
                form.WindowState = FormWindowState.Normal; form.ClientSize = new Size(1700, 950);
                await Task.Delay(100); await SimpleControlsReady(form);
                Call(form, "ImportFighterModel", args[0]); await SimpleControlsReady(form);
                Call(form, "BuildLibraryPane"); var library = (AnimationLibrary)Field(form, "_library");
                var modelInfo = BrawlLibrarySource.InspectModels(donor).First(m => !m.Shadow);
                library.Register(BrawlLibrarySource.Prepare(new LibraryRegistration {
                    ModelPath = donor, ModelKey = modelInfo.ResourceKey, MotionPath = args[1],
                    Character = modelInfo.Name, GameVersion = "Local performance fixture; game build unverified", ApproveAssociation = true
                }));
                Call(form, "ToggleAnimationLibrary");
                await FlowSelectSource(form, library, "Wait1");
                var sourceItem = library.Browse("Wait1").Single(c => c.Clip.Name == "Wait1" && c.Model != null);
                await ComparisonRevision(form, library, sourceItem.Clip.Id, null);
                var editor = form.FighterEditor; var target = (MDL0Node)editor.TargetModel; var clip = editor.SelectedCHR0;
                var setup = await Await<ControlReply>(Call(form, "LoadControls")); editor.SetFrame(3);
                editor.SelectedBone = target._linker.BoneCache.Single(b => b.Name == setup.BoneNames[setup.Roles.Single(r => r.Role == "Hips").Bone.Value]);
                // A genuine native body edit makes the preserved Undo witness nonempty.
                await ReviewBodyEdit(form, 2); editor.SetFrame(3); await SimpleControlsReady(form);
                int probeFrame = editor.CurrentFrame, probeUndo = editor._undoSaves.Count;
                string probeAuthored = SetNativeContent(clip);
                int layoutEvents = 0;
                var outerBody = (Panel)Field(form, "_simpleWorkspaceBody");
                outerBody.Layout += (s, e) => layoutEvents++;

                var observeSelection = form.GetType().GetMethod("ObserveControlSelection", Private);
                if (observeSelection != null)
                {
                    long? eventsBefore = PerformanceCounter(form, "ControlSelectionEvents"),
                        invalidationsBefore = PerformanceCounter(form, "ControlSelectionInvalidations"),
                        loadsBefore = PerformanceCounter(form, "ControlLoadRequests");
                    int epoch = (int)Field(form, "_controlEpoch");
                    measurements.Add(new { Width = form.ClientSize.Width, TextPercent = WorkspaceAppearance.TextPercent,
                        Measurement = PerformanceMeasure("repeated-native-selection-notification",
                            () => observeSelection.Invoke(form, null), form, () => layoutEvents) });
                    await Task.Delay(50); await SimpleControlsReady(form);
                    Check(PerformanceCounter(form, "ControlSelectionEvents") == eventsBefore + PerformanceWarmups + PerformanceObservations &&
                        PerformanceCounter(form, "ControlSelectionInvalidations") == invalidationsBefore &&
                        PerformanceCounter(form, "ControlLoadRequests") == loadsBefore && (int)Field(form, "_controlEpoch") == epoch,
                        "Thirty-nine duplicate native selection notifications cause zero invalidations, epoch changes or engine loads");
                    Call(form, "InvalidateControls");
                    await Wait(() => PerformanceCounter(form, "ControlLoadRequests").GetValueOrDefault() > loadsBefore.GetValueOrDefault());
                    await SimpleControlsReady(form);
                    Check((int)Field(form, "_controlEpoch") > epoch && editor.AnimationHandles.Length > 0 &&
                        setup.ProtectedMotionBones.All(i => !editor.CanEditAnimationBone(setup.BoneNames[i])),
                        "Explicit same-identity control invalidation still reloads reviewed body controls and protected-root guards");
                    long? beforePlayback = PerformanceCounter(form, "ControlLoadRequests");
                    editor.PlayAnim();
                    Check(editor.Playing, "Playback-deferred setup scenario uses ordinary native playback");
                    Call(form, "QueueControlSetup"); await Task.Delay(100);
                    Check(PerformanceCounter(form, "ControlLoadRequests") == beforePlayback && !(bool)Field(form, "_controlSetupQueued"),
                        "Queued control preparation performs no engine load while native playback is running");
                    editor.StopAnim(); editor.SetFrame(probeFrame);
                    // Exercise the same queue once paused as well as any native
                    // Idle resumption; no synthetic input or authored edit occurs.
                    Call(form, "QueueControlSetup");
                    await Wait(() => PerformanceCounter(form, "ControlLoadRequests").GetValueOrDefault() > beforePlayback.GetValueOrDefault());
                    await SimpleControlsReady(form);
                    Check(editor.AnimationHandles.Length > 0 && setup.ProtectedMotionBones.All(i => !editor.CanEditAnimationBone(setup.BoneNames[i])),
                        "Deferred control preparation resumes after pause with reviewed controls and source-local guards");
                    observations.Add(new { Workload = "control-preparation", DuplicateCallbacks = PerformanceWarmups + PerformanceObservations,
                        DuplicateInvalidations = 0, DuplicateLoads = 0, ExplicitReloaded = true, LoadsDuringPlayback = 0,
                        ReloadedAfterPause = true });
                }
                else observations.Add(new { Workload = "repeated-native-selection-notification", BaselineCompatible = true,
                    Reason = "Baseline has no additive selection observer or load counters" });

                Check(SetNativeContent(clip) == probeAuthored && editor._undoSaves.Count == probeUndo &&
                    ReferenceEquals(editor.TargetModel, target) && ReferenceEquals(editor.SelectedCHR0, clip),
                    "Deliberate control setup/reload probes preserve authored target/clip and native Undo");
                // Setup probes intentionally rebuild editor-only handles and may
                // clear their selection. Establish the exact navigation witness
                // once, before any measured workspace route. Never restore or
                // reapprove it after a measured operation.
                editor.SetFrame(probeFrame);
                editor.SelectedBone = target._linker.BoneCache.Single(b => b.Name == setup.BoneNames[setup.Roles.Single(r => r.Role == "Hips").Bone.Value]);
                Call(form, "AddSelectedBatchReviewClip"); Call(form, "MarkBatchReview");
                var row = ReviewRows(form).Single();
                var selectedBone = editor.SelectedBone; int frame = editor.CurrentFrame, undo = editor._undoSaves.Count;
                string authored = SetNativeContent(clip), targetContent = (string)Call(form, "NativeControlHash", target, ".mdl0");
                fixture = new { TargetContent = targetContent, TargetBones = target._linker.BoneCache.Length,
                    Clip = clip.Name, ClipFrames = clip.FrameCount, clip.Loop,
                    ClipContent = PerformanceTextHash(authored), SourceResource = sourceItem.Clip.ResourceSha256,
                    SourceRig = sourceItem.Model.SkeletonSha256 };
                bool dirty = (bool)Field(form, "_fighterDirty"), targetDirty = target.IsDirty, clipDirty = clip.IsDirty;
                var viewport = editor.ModelPanel;
                Action<string> preserve = phase =>
                {
                    var witness = new Dictionary<string, bool> {
                        { "TargetIdentity", ReferenceEquals(editor.TargetModel, target) },
                        { "ClipIdentity", ReferenceEquals(editor.SelectedCHR0, clip) },
                        { "BoneIdentity", ReferenceEquals(editor.SelectedBone, selectedBone) },
                        { "ViewportIdentity", ReferenceEquals(editor.ModelPanel, viewport) },
                        { "Frame", editor.CurrentFrame == frame }, { "Undo", editor._undoSaves.Count == undo },
                        { "ExactAuthoredContent", SetNativeContent(clip) == authored },
                        { "DocumentDirty", (bool)Field(form, "_fighterDirty") == dirty },
                        { "TargetDirty", target.IsDirty == targetDirty }, { "ClipDirty", clip.IsDirty == clipDirty },
                        { "ReviewApproval", ReviewApproved(row) }
                    };
                    observations.Add(new { Workload = "native-state", Phase = phase, Witness = witness,
                        ActualBone = editor.SelectedBone?.Name, ExpectedBone = selectedBone?.Name,
                        ActualFrame = editor.CurrentFrame, ExpectedFrame = frame,
                        ActualUndo = editor._undoSaves.Count, ExpectedUndo = undo,
                        ActualDocumentDirty = (bool)Field(form, "_fighterDirty"), ExpectedDocumentDirty = dirty,
                        ActualTargetDirty = target.IsDirty, ExpectedTargetDirty = targetDirty,
                        ActualClipDirty = clip.IsDirty, ExpectedClipDirty = clipDirty });
                    if (witness.Values.Any(v => !v)) Console.WriteLine("STATE WITNESS " + new JavaScriptSerializer().Serialize(observations.Last()));
                    Check(witness.Values.All(v => v), "Measured routes preserve exact native document, bone/frame, Undo, dirty state and review approval at " + phase);
                };

                foreach (int text in new[] { 100, 175 }) foreach (int width in new[] { 1050, 1700 })
                {
                    form.ClientSize = new Size(width, 950); Call(form, "SetWorkspaceTextSize", text);
                    Call(form, "SetSimpleWorkspaceStage", 2); await Task.Delay(50); await SimpleControlsReady(form);
                    int hashes = FighterProject.DiagnosticFileHashes, opens = AnimationLibrary.DiagnosticOpens,
                        bakes = BrawlRetargeter.DiagnosticBakes, captures = BrawlRetargeter.DiagnosticCaptures,
                        textures = MDL0TextureNode.DiagnosticLoads;
                    measurements.Add(new { Width = width, TextPercent = text, Measurement = PerformanceMeasure(
                        "stable-workspace-status", () => Call(form, "UpdateSimpleWorkspace"), form, () => layoutEvents) });
                    measurements.Add(new { Width = width, TextPercent = text, Measurement = PerformanceMeasure(
                        "stable-document-status", () => Call(form, "UpdateFighterStatus"), form, () => layoutEvents) });
                    int navigation = 0;
                    measurements.Add(new { Width = width, TextPercent = text, Measurement = PerformanceMeasure(
                        "workspace-stage-navigation", () => Call(form, "SetSimpleWorkspaceStage", (navigation++ % 4)), form, () => layoutEvents) });
                    Call(form, "SetSimpleWorkspaceStage", 2);
                    int sample = 0;
                    measurements.Add(new { Width = width, TextPercent = text, Measurement = PerformanceMeasure(
                        "native-target-sample", () => editor.UpdateModel(1.25f + ((sample++ % 2) * .5f)), form, () => layoutEvents) });
                    editor.SetFrame(frame);
                    Check(FighterProject.DiagnosticFileHashes == hashes && AnimationLibrary.DiagnosticOpens == opens &&
                        BrawlRetargeter.DiagnosticBakes == bakes && BrawlRetargeter.DiagnosticCaptures == captures && MDL0TextureNode.DiagnosticLoads == textures,
                        "Status, navigation and native samples perform no source/hash/load/bake/texture preparation at " + width + "/" + text);
                    preserve(width + "/" + text);
                    Check(setup.ProtectedMotionBones.All(i => !editor.CanEditAnimationBone(setup.BoneNames[i])),
                        "Measured routes retain source-local protected motion controls at " + width + "/" + text);
                    editor.FrameFighterModel();
                    await CaptureSimpleWorkspace(form, Path.Combine(output, "performance-workspace-" + width + "-" + text + ".png"));
                    Call(form, "ToggleAnimationLibrary"); await FlowSelectSource(form, library, "Wait1");
                    int libraryHashes = FighterProject.DiagnosticFileHashes, libraryOpens = AnimationLibrary.DiagnosticOpens;
                    var libraryBefore = PerformanceCounts(form);
                    measurements.Add(new { Width = width, TextPercent = text, Measurement = PerformanceMeasure(
                        "stable-library-status", () => Call(form, "UpdateSimpleWorkspace"), form, () => layoutEvents) });
                    var libraryDelta = PerformanceDelta(libraryBefore, PerformanceCounts(form));
                    if (libraryDelta["DiagnosticWorkspaceLibraryMeasurements"].HasValue)
                        Check(libraryDelta["DiagnosticWorkspaceLibraryMeasurements"] == 0,
                            "Visible unchanged library performs zero repeated measurements at " + width + "/" + text);
                    Check(FighterProject.DiagnosticFileHashes == libraryHashes && AnimationLibrary.DiagnosticOpens == libraryOpens,
                        "Visible library status performs no source loading or hashing at " + width + "/" + text);
                    await CaptureSimpleWorkspace(form, Path.Combine(output, "performance-library-" + width + "-" + text + ".png"));
                    Call(form, "SetSimpleWorkspaceStage", 2);
                }

                Call(form, "OpenLinkedComparison");
                var source = (LibrarySourceLease)Field(form, "_comparisonLease");
                Check(source != null, "Performance scenario retains verified source provenance and prepared native renderer");
                editor.UpdateModel(1.25f); float visibleFrame = form.ComparisonSourceFrame;
                Check(visibleFrame == 1.25f, "Visible comparison follows the actual fractional target sample");
                Call(form, "SetSimpleWorkspaceStage", 2);
                Check(!((Panel)Field(form, "_comparisonPane")).Visible && ReferenceEquals(source, Field(form, "_comparisonLease")),
                    "Hiding comparison retains its prepared source lease");
                string sourcePose = PerformancePose(source.Model); float hiddenFrame = form.ComparisonSourceFrame;
                long? hiddenBefore = PerformanceCounter(form, "ComparisonSampleCount");
                int hiddenHashes = FighterProject.DiagnosticFileHashes, hiddenOpens = AnimationLibrary.DiagnosticOpens,
                    hiddenBakes = BrawlRetargeter.DiagnosticBakes;
                int hiddenSample = 0;
                measurements.Add(new { Width = form.ClientSize.Width, TextPercent = WorkspaceAppearance.TextPercent,
                    Measurement = PerformanceMeasure("hidden-comparison-target-sample",
                        () => editor.UpdateModel(2.25f + ((hiddenSample++ % 2) * .5f)), form, () => layoutEvents) });
                bool dormant = sourcePose == PerformancePose(source.Model) && form.ComparisonSourceFrame == hiddenFrame;
                long? hiddenAfter = PerformanceCounter(form, "ComparisonSampleCount");
                observations.Add(new { Workload = "hidden-comparison", SourcePoseAndFrameUnchanged = dormant,
                    AppliedSamples = hiddenBefore.HasValue && hiddenAfter.HasValue ? hiddenAfter - hiddenBefore : null,
                    BaselineCompatible = !hiddenBefore.HasValue });
                if (hiddenBefore.HasValue) Check(dormant && hiddenAfter == hiddenBefore,
                    "Hidden linked comparison performs zero source evaluation and retains its last pose/frame");
                Check(FighterProject.DiagnosticFileHashes == hiddenHashes && AnimationLibrary.DiagnosticOpens == hiddenOpens &&
                    BrawlRetargeter.DiagnosticBakes == hiddenBakes, "Hidden comparison sampling performs no asset hashing/loading or baking");
                editor.SetFrame(frame); Call(form, "OpenLinkedComparison");
                Check(ReferenceEquals(source, Field(form, "_comparisonLease")) && form.ComparisonSourceFrame == frame,
                    "Reopening hidden comparison resumes at the current target frame without preparing another source");
                Call(form, "CloseLinkedComparison");
                preserve("comparison transitions");
                Check((string)Call(form, "NativeControlHash", target, ".mdl0") == targetContent,
                    "All measured transitions retain exact native target content");
                Check(inputHashes.All(p => FighterProject.HashFile(p.Key) == p.Value),
                    "Performance fixture source files remain byte-identical");
                completed = true;
            }
        }
        finally
        {
            File.WriteAllText(Path.Combine(output, "performance.json"), new JavaScriptSerializer { MaxJsonLength = 16000000 }.Serialize(new {
                Completed = completed, Version = typeof(ModelForm).Assembly.GetName().Version.ToString(),
                ProductVersion = FileVersionInfo.GetVersionInfo(typeof(ModelForm).Assembly.Location).ProductVersion,
                MeasurementBoundary = "Owner-STA application methods; no paint/OS input/display latency claim",
                FrozenGates = "31 observations after 8 warmups; no stable-state geometry/measurement passes; hidden comparison dormant; no source I/O/hash/bake; exact native state/Undo/approval",
                StopwatchFrequency = Stopwatch.Frequency, PrivateDesktop, OSInputInjected = false, ScreenCopied = false,
                Stopwatch.IsHighResolution, ProcessorCount = Environment.ProcessorCount,
                Process64Bit = Environment.Is64BitProcess, Fixture = fixture,
                Measurements = measurements, Observations = observations
            }));
            if (form != null && !form.IsDisposed) SimpleDisposeForm(form);
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previousLibrary);
        }
    }

    static string PerformanceTextHash(string value)
    {
        using (var hash = System.Security.Cryptography.SHA256.Create())
            return string.Concat(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value)).Select(b => b.ToString("x2")));
    }
}
