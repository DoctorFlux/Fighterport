using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;

internal static partial class Program
{
    static bool StandaloneControlReachable(Control control)
    {
        if (!control.Visible || control.ClientSize.Width <= 0 || control.ClientSize.Height <= 0) return false;
        Rectangle bounds = control.RectangleToScreen(control.ClientRectangle);
        for (Control parent = control.Parent; parent != null; parent = parent.Parent)
            if (!parent.Visible || !parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds)) return false;
        Point center = control.PointToScreen(new Point(control.ClientSize.Width / 2, control.ClientSize.Height / 2));
        Control hit = control.FindForm();
        for (int depth = 0; hit != null && depth < 32; depth++)
        {
            Control child = hit.GetChildAtPoint(hit.PointToClient(center), GetChildAtPointSkip.Invisible);
            if (child == null) break;
            hit = child;
        }
        for (Control item = hit; item != null; item = item.Parent) if (ReferenceEquals(item, control)) return true;
        return false;
    }

    static WorkspaceExportOptimizationOptions ExportOptionsOf(Control host) => host as WorkspaceExportOptimizationOptions ??
        SimpleDescendants(host).OfType<WorkspaceExportOptimizationOptions>().Single();

    static void SetExportAdvanced(Control host, bool visible)
    {
        var fields = SimpleControl<TableLayoutPanel>(host, "exportOptimizationAdvancedFields");
        if (fields.Visible == visible) return;
        var link = SimpleControl<LinkLabel>(host, "exportOptimizationLimits");
        Call(link, "OnLinkClicked", new LinkLabelLinkClickedEventArgs(link.Links[0]));
        host.PerformLayout();
    }

    static void CheckCollapsedExportOptions(Control host, string phase)
    {
        var options = ExportOptionsOf(host);
        var policy = SimpleControl<ComboBox>(host, "exportOptimizationPolicy");
        var advanced = SimpleControl<LinkLabel>(host, "exportOptimizationLimits");
        Check(policy.Visible && advanced.Visible && !SimpleControl<ComboBox>(host, "exportOptimizationEffort").Visible &&
            !SimpleControl<TableLayoutPanel>(host, "exportOptimizationAdvancedFields").Visible &&
            Enumerable.Range(0, 7).All(i => !SimpleControl<NumericUpDown>(host, "exportOptimizationLimit" + i).Visible),
            phase + " presents only the explicit policy and Advanced disclosure, with search and all saved limits hidden");
        policy.Focus(); options.SelectNextControl(policy, true, true, true, false);
        Check(advanced.Focused, phase + " Tab moves from policy directly to Advanced without reaching concealed settings");
        options.SelectNextControl(advanced, false, true, true, false);
        Check(policy.Focused, phase + " Shift+Tab returns from Advanced to policy");
    }

    static void CheckExpandedExportOptions(Control host, string phase)
    {
        Check(StandaloneControlReachable(SimpleControl<ComboBox>(host, "exportOptimizationEffort")),
            phase + " advanced search survives every ancestor clip and managed center hit test");
        for (int i = 0; i < 7; i++)
            Check(StandaloneControlReachable(SimpleControl<NumericUpDown>(host, "exportOptimizationLimit" + i)),
                phase + " advanced saved limit survives every ancestor clip and managed center hit test: " + i);
    }

    // Current standalone workflow: Analyze/preview/export an independent copy.
    // Historical live-Apply/cache fixtures remain separately named helpers.
    static async Task WorkspaceOptimizationExportCopy(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Standalone export-copy qualification requires the private desktop.");
        string segment = "Complete";
        int segmentAt = Array.IndexOf(args, "--workspace-optimization-segment");
        if (segmentAt >= 0)
        {
            if (segmentAt + 1 >= args.Length || !new[] { "Exact", "Bounded", "Apply" }.Contains(args[segmentAt + 1]))
                throw new ArgumentException("Choose Exact, Bounded or Apply for the explicit standalone test segment.");
            segment = args[segmentAt + 1];
        }
        bool runExact = segment == "Complete" || segment == "Exact", runBounded = segment == "Complete" || segment == "Bounded",
            runApply = segment == "Complete" || segment == "Apply";
        var completedSegments = new List<string>();
        void SegmentEvidence(bool passed) => File.WriteAllText(Path.Combine(output, "standalone-segment-evidence.json"), new JavaScriptSerializer().Serialize(new {
            Segment = segment, SegmentPassed = passed, CompleteCase = segment == "Complete", CompleteWorkflow = passed && segment == "Complete",
            IncludesExact = runExact, IncludesBounded = runBounded, IncludesApply = runApply, PassedSegments = completedSegments,
            RequiredOtherSegments = segment == "Complete" ? new string[0] : new[] { "Exact", "Bounded", "Apply" }.Where(item => item != segment).ToArray(),
            Claim = segment == "Complete" ? "Original complete standalone workflow." : "Explicit partial workload; all three admitted segments are required for complete workflow qualification."
        }));
        SegmentEvidence(false);
        string previousPreferences = Environment.GetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT");
        string previousLibrary = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", Path.Combine(output, "isolated-export-settings"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "isolated-library"));
        if (segment == "Apply")
        {
            // Complete reaches Apply with the preceding Bounded/Thorough
            // request persisted. Recreate that context for the isolated part.
            var preferences = new NativeExportOptimizationPreferences();
            preferences.Save(new NativeExportOptimizationSettings { Policy = NativeExportOptimizationPolicy.ErrorBounded, SearchEffort = NativeExportSearchEffort.Thorough });
            if (preferences.LastError != null) throw new InvalidOperationException("Could not initialize the isolated Apply segment's original policy/effort context.");
        }
        var originals = new[] { args[0], args[1] }.Distinct().ToDictionary(path => path, FighterProject.HashFile);
        var captures = new List<string>();
        var form = new ModelForm();
        try
        {
            form.InitializeFighterProject(FighterProject.Create("Standalone independent CHR0 export-copy qualification"));
            form.Show(); form.ClientSize = new Size(1700, 1000); Call(form, "SetWorkspaceTextSize", 100);
            Call(form, "ImportFighterModel", args[0]); Call(form, "ImportFighterAnimations", new[] { new[] { args[1] } });
            await GameLibraryWait(() => form.FighterEditor.TargetModel != null && form.FighterEditor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Any(),
                "Standalone target and source motion did not open.");
            var editor = form.FighterEditor;
            var clip = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First(c => c.Name == "Wait1");
            foreach (ListViewItem item in editor.leftPanel.listAnims.Items) item.Selected = ReferenceEquals(item.Tag, clip);
            editor.SelectedCHR0 = clip; await SimpleControlsReady(form); await Await<ControlReply>(Call(form, "LoadControls"));
            editor.SetFrame(Math.Min(3, clip.FrameCount)); editor.FrameFighterModel(); await SimpleControlsReady(form);
            var model = (MDL0Node)editor.TargetModel; var bone = editor.SelectedBone;
            string identity = OptimizationNativeIdentity(clip), content = NativeAnimationOptimization.SourceContentKey(clip), storage = NativeAnimationOptimization.StorageKey(clip);
            string camera = CompactCamera(form); int frame = editor.CurrentFrame, undo = editor._undoSaves.Count, redo = editor._redoSaves.Count;
            bool dirty = clip.IsDirty, rootDirty = clip.RootNode.IsDirty, modelDirty = model.IsDirty, projectDirty = (bool)Field(form, "_fighterDirty");
            int assets = form.FighterProject.Animations.Count;
            string targetContent = (string)Call(form, "NativeControlHash", model, ".mdl0");
            var siblings = OptimizationUntouchedBytes(form, clip);
            void Unchanged(string phase)
            {
                Check(ReferenceEquals(editor.TargetModel, model) && ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.TargetAnimation, clip) &&
                    ReferenceEquals(editor.SelectedBone, bone) && OptimizationNativeIdentity(clip) == identity &&
                    NativeAnimationOptimization.SourceContentKey(clip) == content && NativeAnimationOptimization.StorageKey(clip) == storage &&
                    editor.CurrentFrame == frame && CompactCamera(form) == camera && editor._undoSaves.Count == undo && editor._redoSaves.Count == redo &&
                    clip.IsDirty == dirty && clip.RootNode.IsDirty == rootDirty && model.IsDirty == modelDirty && (bool)Field(form, "_fighterDirty") == projectDirty &&
                    form.FighterProject.Animations.Count == assets && (string)Call(form, "NativeControlHash", model, ".mdl0") == targetContent,
                    "Standalone " + phase + " preserves exact master identity/content/storage, target, bone, frame, camera, native Undo/Redo and dirty state");
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
                    await GameLibraryWait(() => finished, "Standalone optimization modal did not complete.", 180);
                }
                finally { form.OptimizationReady = null; }
                if (failure != null) throw failure;
                await SimpleControlsReady(form);
            }
            async Task CaptureLayouts(AnimationOptimizationDialog window)
            {
                foreach (int text in new[] { 100, 175 }) foreach (int width in new[] { 1050, 1700 })
                {
                    Call(form, "SetWorkspaceTextSize", text); window.ApplyTextSize(); window.ClientSize = new Size(width, width == 1050 ? 850 : 1000); window.PerformLayout();
                    SetExportAdvanced(window, false);
                    foreach (bool details in new[] { false, true })
                    {
                        var report = SimpleControl<TextBox>(window, "optimizationReport");
                        if (report.Visible != details) SimpleControl<Button>(window, "optimizationDetails").PerformClick();
                        window.PerformLayout(); window.SourceView.ShownFrame(); window.TargetView.ShownFrame();
                        foreach (string name in new[] { "optimizationIdentity", "exportOptimizationPolicy", "exportOptimizationLimits", "optimizeAnalyze", "optimizeApply", "optimizeApplyCurrent", "optimizationClose", "optimizationDetails", "optimizationFrame" })
                        {
                            var control = SimpleDescendants(window).Single(c => c.Name == name);
                            var bounds = window.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                            Check(control.Visible && control.ClientSize.Width > 0 && control.ClientSize.Height > 0 && window.ClientRectangle.Contains(bounds),
                                "Standalone control fits current export-copy layout: " + name + " / " + width + " / " + text + " / details=" + details);
                        }
                        foreach (string name in new[] { "exportOptimizationPolicy", "exportOptimizationLimits" })
                            Check(StandaloneControlReachable(SimpleDescendants(window).Single(c => c.Name == name)),
                                "Standalone policy control survives every ancestor clip and managed center hit test: " + name + " / " + width + " / " + text + " / details=" + details);
                        Check(!details || report.Visible && report.ClientSize.Height > 0, "Standalone details remain available without replacing the native previews");
                        CheckCollapsedExportOptions(window, "Standalone " + width + "/" + text + "/details=" + details);
                        Check(SimpleControl<FlowLayoutPanel>(window, "optimizationActions").Controls.OfType<Button>().Where(b => b.Visible).Select(b => b.Name)
                            .SequenceEqual(new[] { "optimizeAnalyze", "optimizeApply", "optimizeApplyCurrent", "optimizationClose" }),
                            "Standalone idle footer exposes Optimize, Export optimized copy, explicit Apply to current animation and Close at " + width + "/" + text);
                        string file = "standalone-" + width + "-" + text + (details ? "-details" : "") + ".png";
                        await CaptureOptimization(window, Path.Combine(output, file)); captures.Add(file);
                        if (!details)
                        {
                            string settings = window.ReadExportSettings().StableKey; var candidate = window.Candidate;
                            SetExportAdvanced(window, true);
                            CheckExpandedExportOptions(window, "Standalone " + width + "/" + text);
                            file = "standalone-" + width + "-" + text + "-limits.png";
                            await CaptureOptimization(window, Path.Combine(output, file)); captures.Add(file);
                            SimpleControl<ComboBox>(window, "exportOptimizationEffort").Focus(); SetExportAdvanced(window, false);
                            Check(SimpleControl<LinkLabel>(window, "exportOptimizationLimits").Focused && window.ReadExportSettings().StableKey == settings &&
                                ReferenceEquals(window.Candidate, candidate), "Collapsing advanced settings restores its disclosure focus and retains the prepared copy and exact saved settings");
                        }
                    }
                }
            }
            void CheckOptionsNavigation(AnimationOptimizationDialog window)
            {
                SetExportAdvanced(window, false); CheckCollapsedExportOptions(window, "Standalone navigation");
                var options = ExportOptionsOf(window);
                var policyControl = SimpleControl<ComboBox>(window, "exportOptimizationPolicy"); var effortControl = SimpleControl<ComboBox>(window, "exportOptimizationEffort");
                var advanced = SimpleControl<LinkLabel>(window, "exportOptimizationLimits"); string settings = window.ReadExportSettings().StableKey;
                SetExportAdvanced(window, true); advanced.Focus(); options.SelectNextControl(advanced, true, true, true, false);
                Check(effortControl.Focused, "Expanded Advanced Tab reaches search before the saved limits");
                if (window.ReadExportSettings().Policy == NativeExportOptimizationPolicy.ErrorBounded)
                {
                    options.SelectNextControl(effortControl, true, true, true, false);
                    var first = SimpleControl<NumericUpDown>(window, "exportOptimizationLimit0");
                    Check(first.ContainsFocus, "Expanded bounded search Tab reaches the first editable limit");
                    options.SelectNextControl(first, false, true, true, false);
                    Check(effortControl.Focused, "Expanded bounded Shift+Tab returns from limits to search");
                }
                else Check(effortControl.Enabled && !SimpleControl<NumericUpDown>(window, "exportOptimizationLimit0").Enabled,
                    "Exact retains its independently selectable search effort while bounded limits stay disabled");
                effortControl.Focus();
                Check((bool)Call(window, "ProcessCmdKey", default(Message), Keys.F6) && window.SourceView.Viewport.ContainsFocus &&
                    (bool)Call(window, "ProcessCmdKey", default(Message), Keys.Shift | Keys.F6) && effortControl.Focused,
                    "Standalone F6 reaches the native source viewport and reverse F6 restores the remembered options control without OS input");
                Call(window, "ProcessCmdKey", default(Message), Keys.F6); SetExportAdvanced(window, false);
                Check((bool)Call(window, "ProcessCmdKey", default(Message), Keys.Shift | Keys.F6) && (policyControl.Focused || advanced.Focused),
                    "Reverse F6 recovers a visible policy action when its remembered advanced control is concealed");
                SetExportAdvanced(window, true); effortControl.Focus(); SetExportAdvanced(window, false);
                Check(advanced.Focused && window.ReadExportSettings().StableKey == settings,
                    "Advanced expand/collapse returns focus and retains the complete policy, effort and cumulative limits");
            }
            if (Environment.GetEnvironmentVariable("FIGHTERPORT_STANDALONE_LAYOUT_PROBE") == "1")
            {
                File.WriteAllText(Path.Combine(output, "diagnostic-only.txt"), "Fast layout/focus-only probe. No Analyze/curve qualification is claimed by this run.");
                await Open(async window => { await CaptureLayouts(window); CheckOptionsNavigation(window); });
                Call(form, "SetWorkspaceTextSize", 100); Unchanged("layout-only diagnostic"); return;
            }

            string cancelledDestination = Path.Combine(output, "cancelled-copy.chr0"), exported = Path.Combine(output, "standalone-verified-copy.chr0");
            if (runExact)
            {
            await Open(async window =>
            {
                var policy = SimpleControl<ComboBox>(window, "exportOptimizationPolicy");
                Check(policy.Items.Count == 2 && policy.SelectedIndex == 0 && !window.ReadHighQuality() && window.Candidate == null &&
                    SimpleControl<Button>(window, "optimizeApply").Text.Contains("Export") && SimpleControl<Button>(window, "optimizeAnalyze").Text == "Optimize" &&
                    SimpleControl<Button>(window, "optimizeAnalyze").Visible && !SimpleControl<Button>(window, "optimizationCancel").Visible,
                    "Standalone dialog starts with two explicit policies, Exact selected, and independently named export/current-animation actions");
                Check(!SimpleControl<Button>(window, "optimizeApplyCurrent").Enabled,
                    "Apply to current animation is disabled until an independent native result is prepared");
                Check(!ReferenceEquals(window.SourceView.Model, model) && !ReferenceEquals(window.TargetView.Model, model) &&
                    !ReferenceEquals(window.SourceView.Model, window.TargetView.Model), "Standalone previews own independent native models");
                var originalPrepare = window.Prepare;
                var delayed = new TaskCompletionSource<CHR0Node>(); window.Prepare = quality => delayed.Task;
                SimpleControl<Button>(window, "optimizeAnalyze").Focus();
                var cancel = window.Analyze();
                Check(window.Busy && SimpleControl<Button>(window, "optimizationCancel").Visible && SimpleControl<Button>(window, "optimizationCancel").Enabled &&
                    SimpleControl<Button>(window, "optimizationCancel").Focused && !SimpleControl<Button>(window, "optimizeAnalyze").Visible,
                    "Standalone pending optimization replaces Optimize with focused cancellation");
                SimpleControl<Button>(window, "optimizationCancel").PerformClick();
                delayed.SetResult(NativeAnimationOptimization.CopySource(clip)); await cancel; window.Prepare = originalPrepare;
                Check(!window.Busy && window.Candidate == null && !SimpleControl<Button>(window, "optimizeApply").Enabled &&
                    SimpleControl<Button>(window, "optimizeAnalyze").Visible && SimpleControl<Button>(window, "optimizeAnalyze").Focused &&
                    !SimpleControl<Button>(window, "optimizationCancel").Visible,
                    "Standalone cancellation discards a late independent preparation without publishing it");
                Unchanged("cancelled analysis");

                await window.Analyze();
                Check(!window.Busy && window.Candidate != null && window.PreparedCanApply && window.PreparedReport.Contains("Finished target master"),
                    "Standalone Exact Analyze uses the shared finished-master export preparation and produces an independent decoded copy");
                Check(NativeAnimationOptimization.SourceContentKey(window.Candidate) != null && window.Candidate.FrameCount == clip.FrameCount && window.Candidate.Loop == clip.Loop,
                    "Standalone candidate retains current native timing and stored loop state");
                int prepares = form.OptimizationPrepares, checks = form.OptimizationNativeChecks;
                SimpleControl<Button>(window, "optimizationPlay").PerformClick(); await Task.Delay(180);
                SimpleControl<Button>(window, "optimizationPlay").PerformClick();
                SimpleControl<TrackBar>(window, "optimizationFrame").Value = Math.Min(4, clip.FrameCount);
                Check(form.OptimizationPrepares == prepares && form.OptimizationNativeChecks == checks,
                    "Standalone native playback and scrubbing do not rerun preparation or verification");
                Unchanged("preview playback");

                await CaptureLayouts(window);
                Call(form, "SetWorkspaceTextSize", 100); window.ApplyTextSize();
                CheckOptionsNavigation(window);
                var effort = SimpleControl<ComboBox>(window, "exportOptimizationEffort"); effort.SelectedIndex = effort.SelectedIndex == 0 ? 1 : 0;
                Check(window.Candidate == null && !SimpleControl<Button>(window, "optimizeApply").Enabled && !SimpleControl<Button>(window, "optimizeApplyCurrent").Enabled,
                    "Changing standalone search effort invalidates both export and current Apply without changing quality limits");
                await window.Analyze(); Check(window.Candidate != null && window.PreparedCanApply, "Standalone reanalysis prepares the new explicit settings from the same master");
                using (var tampered = NativeAnimationOptimization.CopySource(window.Candidate))
                {
                    tampered.Name += " changed"; bool stale = false;
                    try { window.Apply(window.ReadHighQuality(), tampered); } catch (InvalidOperationException) { stale = true; }
                    Check(stale && !File.Exists(cancelledDestination), "Standalone export rejects a stale prepared candidate identity before opening publication");
                }
                using (FlowModal("workspaceFilePath", dialog => {
                    SimpleControl<TextBox>(dialog, "workspaceFilePath").Text = cancelledDestination;
                    SimpleControl<Button>(dialog, "workspaceFileCancel").PerformClick();
                })) SimpleControl<Button>(window, "optimizeApply").PerformClick();
                Check(!File.Exists(cancelledDestination), "Cancelling the actual standalone export chooser creates no file");
            });
            Unchanged("cancelled export chooser");

            await Open(async window =>
            {
                SimpleControl<ComboBox>(window, "exportOptimizationPolicy").SelectedIndex = 0;
                await window.Analyze(); Check(window.Candidate != null && window.PreparedCanApply, "Fresh standalone Exact Analyze remains available after chooser cancellation");
                string expected = NativeAnimationOptimization.SourceContentKey(window.Candidate), expectedStorage = NativeAnimationOptimization.StorageKey(window.Candidate);
                using (FlowModal("workspaceFilePath", dialog => {
                    SimpleControl<TextBox>(dialog, "workspaceFilePath").Text = exported;
                    SimpleControl<Button>(dialog, "workspaceFileAccept").PerformClick();
                })) SimpleControl<Button>(window, "optimizeApply").PerformClick();
                Check(File.Exists(exported), "Standalone Export optimized copy publishes through its actual application-owned chooser");
                using (var decoded = NodeFactory.FromFile(null, exported) as CHR0Node)
                    Check(decoded != null && NativeAnimationOptimization.SourceContentKey(decoded) == expected && NativeAnimationOptimization.StorageKey(decoded) == expectedStorage,
                        "Standalone published CHR0 independently retains every prepared time/value/tangent/flag/metadata/storage identity");
            });
            Unchanged("successful independent export");
            completedSegments.Add("Exact"); SegmentEvidence(false);
            }

            if (runBounded)
            {
            await Open(async window =>
            {
                SimpleControl<ComboBox>(window, "exportOptimizationPolicy").SelectedIndex = 1;
                // Complete inherits Thorough from the preceding Exact effort
                // change. Standalone Bounded recreates that same search choice.
                if (segment == "Bounded") SimpleControl<ComboBox>(window, "exportOptimizationEffort").SelectedIndex = (int)NativeExportSearchEffort.Thorough;
                var limits = window.ReadExportSettings(); limits.Validate();
                Check(limits.Policy == NativeExportOptimizationPolicy.ErrorBounded && limits.IsApprovedProfile, "Standalone bounded attempt explicitly requests the unchanged approved ceilings");
                CheckOptionsNavigation(window);
                await window.Analyze();
                Check(window.Candidate != null && window.PreparedCanApply && window.PreparedReport.Contains("Policy requested: ErrorBounded") &&
                    (window.PreparedReport.Contains("Published-copy policy: ErrorBounded") || window.PreparedReport.Contains("Published-copy policy: Exact")),
                    "Standalone bounded Analyze reports the shared backend's measured policy or explicit conservative Exact/reference fallback");
                File.WriteAllText(Path.Combine(output, "standalone-bounded-analysis.txt"), window.PreparedReport);
            });
            Unchanged("bounded analysis or explicit fallback");
            completedSegments.Add("Bounded"); SegmentEvidence(false);
            }
            OptimizationCheckUntouchedBytes(form, clip, siblings, "standalone export-copy workflow", Path.Combine(output, "standalone-unchanged-siblings.json"));
            Check(originals.All(item => FighterProject.HashFile(item.Key) == item.Value), "Standalone Analyze, preview, cancellation and export preserve all supplied original files");
            File.WriteAllText(Path.Combine(output, "standalone-export-copy-evidence.json"), new JavaScriptSerializer().Serialize(new {
                Captures = captures, Segment = segment, CompleteCase = segment == "Complete", Export = runExact ? exported : null,
                ExportBytes = runExact ? new FileInfo(exported).Length : 0, OriginalInputHashes = originals,
                MasterNativeSha256 = content, MasterStorage = storage, Identity = identity, NativeUndo = undo, NativeRedo = redo,
                SharedOffscreenRenderer = true, ActiveDesktopCapture = false, OSInputInjected = false, PhysicalInputAndDpiVerified = false }));
            if (runApply)
            {
                await WorkspaceOptimizationApplyCurrent(form, clip, output, originals);
                completedSegments.Add("Apply");
            }
            SegmentEvidence(true);
        }
        finally
        {
            form.OptimizationReady = null; SimpleDisposeForm(form);
            Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", previousPreferences);
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previousLibrary);
        }
    }
}
