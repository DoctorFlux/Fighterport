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

    // Current standalone workflow: Analyze/preview/export an independent copy.
    // Historical live-Apply/cache fixtures remain separately named helpers.
    static async Task WorkspaceOptimizationExportCopy(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Standalone export-copy qualification requires the private desktop.");
        string previousPreferences = Environment.GetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT");
        string previousLibrary = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", Path.Combine(output, "isolated-export-settings"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "isolated-library"));
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
                    foreach (bool details in new[] { false, true })
                    {
                        var report = SimpleControl<TextBox>(window, "optimizationReport");
                        if (report.Visible != details) SimpleControl<Button>(window, "optimizationDetails").PerformClick();
                        window.PerformLayout(); window.SourceView.ShownFrame(); window.TargetView.ShownFrame();
                        foreach (string name in new[] { "optimizationIdentity", "exportOptimizationPolicy", "exportOptimizationEffort", "optimizeAnalyze", "optimizeApply", "optimizationClose", "optimizationDetails", "optimizationFrame" })
                        {
                            var control = SimpleDescendants(window).Single(c => c.Name == name);
                            var bounds = window.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                            Check(control.Visible && control.ClientSize.Width > 0 && control.ClientSize.Height > 0 && window.ClientRectangle.Contains(bounds),
                                "Standalone control fits current export-copy layout: " + name + " / " + width + " / " + text + " / details=" + details);
                        }
                        foreach (string name in new[] { "exportOptimizationPolicy", "exportOptimizationEffort", "exportOptimizationLimits" })
                            Check(StandaloneControlReachable(SimpleDescendants(window).Single(c => c.Name == name)),
                                "Standalone policy control survives every ancestor clip and managed center hit test: " + name + " / " + width + " / " + text + " / details=" + details);
                        Check(!details || report.Visible && report.ClientSize.Height > 0, "Standalone details remain available without replacing the native previews");
                        string file = "standalone-" + width + "-" + text + (details ? "-details" : "") + ".png";
                        await CaptureOptimization(window, Path.Combine(output, file)); captures.Add(file);
                        if (!details)
                        {
                            var link = SimpleControl<LinkLabel>(window, "exportOptimizationLimits");
                            Call(link, "OnLinkClicked", new LinkLabelLinkClickedEventArgs(link.Links[0])); window.PerformLayout();
                            for (int value = 0; value < 7; value++)
                                Check(StandaloneControlReachable(SimpleControl<NumericUpDown>(window, "exportOptimizationLimit" + value)),
                                    "Standalone expanded saved limit remains visible and reachable: " + value + " / " + width + " / " + text);
                            file = "standalone-" + width + "-" + text + "-limits.png";
                            await CaptureOptimization(window, Path.Combine(output, file)); captures.Add(file);
                            Call(link, "OnLinkClicked", new LinkLabelLinkClickedEventArgs(link.Links[0])); window.PerformLayout();
                        }
                    }
                }
            }
            void CheckOptionsNavigation(AnimationOptimizationDialog window)
            {
                var policyControl = SimpleControl<ComboBox>(window, "exportOptimizationPolicy"); var effortControl = SimpleControl<ComboBox>(window, "exportOptimizationEffort");
                policyControl.Focus(); policyControl.Parent.SelectNextControl(policyControl, true, true, false, false);
                Check(effortControl.Focused, "Standalone ordinary Tab order moves from policy to search effort");
                effortControl.Parent.SelectNextControl(effortControl, false, true, false, false);
                Check(policyControl.Focused, "Standalone ordinary Shift+Tab returns from search effort to policy");
                effortControl.Focus();
                Check((bool)Call(window, "ProcessCmdKey", default(Message), Keys.F6) && window.SourceView.Viewport.ContainsFocus &&
                    (bool)Call(window, "ProcessCmdKey", default(Message), Keys.Shift | Keys.F6) && effortControl.Focused,
                    "Standalone F6 reaches the native source viewport and reverse F6 restores the remembered options control without OS input");
            }
            if (Environment.GetEnvironmentVariable("FIGHTERPORT_STANDALONE_LAYOUT_PROBE") == "1")
            {
                File.WriteAllText(Path.Combine(output, "diagnostic-only.txt"), "Fast layout/focus-only probe. No Analyze/curve qualification is claimed by this run.");
                await Open(async window => { await CaptureLayouts(window); CheckOptionsNavigation(window); });
                Call(form, "SetWorkspaceTextSize", 100); Unchanged("layout-only diagnostic"); return;
            }

            string cancelledDestination = Path.Combine(output, "cancelled-copy.chr0");
            await Open(async window =>
            {
                var policy = SimpleControl<ComboBox>(window, "exportOptimizationPolicy");
                Check(policy.Items.Count == 2 && policy.SelectedIndex == 0 && !window.ReadHighQuality() && window.Candidate == null &&
                    SimpleControl<Button>(window, "optimizeApply").Text.Contains("Export"),
                    "Standalone dialog starts with two explicit export policies, Exact selected and no live Apply action");
                Check(!ReferenceEquals(window.SourceView.Model, model) && !ReferenceEquals(window.TargetView.Model, model) &&
                    !ReferenceEquals(window.SourceView.Model, window.TargetView.Model), "Standalone previews own independent native models");
                var originalPrepare = window.Prepare;
                var delayed = new TaskCompletionSource<CHR0Node>(); window.Prepare = quality => delayed.Task;
                var cancel = window.Analyze();
                Check(window.Busy && SimpleControl<Button>(window, "optimizationCancel").Enabled, "Standalone pending analysis exposes cancellation");
                SimpleControl<Button>(window, "optimizationCancel").PerformClick();
                delayed.SetResult(NativeAnimationOptimization.CopySource(clip)); await cancel; window.Prepare = originalPrepare;
                Check(!window.Busy && window.Candidate == null && !SimpleControl<Button>(window, "optimizeApply").Enabled,
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
                Check(window.Candidate == null && !SimpleControl<Button>(window, "optimizeApply").Enabled,
                    "Changing standalone search effort invalidates the prepared copy without changing quality limits");
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

            string exported = Path.Combine(output, "standalone-verified-copy.chr0");
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

            await Open(async window =>
            {
                SimpleControl<ComboBox>(window, "exportOptimizationPolicy").SelectedIndex = 1;
                var limits = window.ReadExportSettings(); limits.Validate();
                Check(limits.Policy == NativeExportOptimizationPolicy.ErrorBounded && limits.IsApprovedProfile, "Standalone bounded attempt explicitly requests the unchanged approved ceilings");
                await window.Analyze();
                Check(window.Candidate != null && window.PreparedCanApply && window.PreparedReport.Contains("Policy requested: ErrorBounded") &&
                    (window.PreparedReport.Contains("Published-copy policy: ErrorBounded") || window.PreparedReport.Contains("Published-copy policy: Exact")),
                    "Standalone bounded Analyze reports the shared backend's measured policy or explicit conservative Exact/reference fallback");
                File.WriteAllText(Path.Combine(output, "standalone-bounded-analysis.txt"), window.PreparedReport);
            });
            Unchanged("bounded analysis or explicit fallback");
            OptimizationCheckUntouchedBytes(form, clip, siblings, "standalone export-copy workflow", Path.Combine(output, "standalone-unchanged-siblings.json"));
            Check(originals.All(item => FighterProject.HashFile(item.Key) == item.Value), "Standalone Analyze, preview, cancellation and export preserve all supplied original files");
            File.WriteAllText(Path.Combine(output, "standalone-export-copy-evidence.json"), new JavaScriptSerializer().Serialize(new {
                Captures = captures, Export = exported, ExportBytes = new FileInfo(exported).Length, OriginalInputHashes = originals,
                MasterNativeSha256 = content, MasterStorage = storage, Identity = identity, NativeUndo = undo, NativeRedo = redo,
                SharedOffscreenRenderer = true, ActiveDesktopCapture = false, OSInputInjected = false, PhysicalInputAndDpiVerified = false }));
        }
        finally
        {
            form.OptimizationReady = null; SimpleDisposeForm(form);
            Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", previousPreferences);
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previousLibrary);
        }
    }
}
