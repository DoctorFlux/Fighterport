using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static int OptimizationKeyCount(CHR0Node clip) => clip.Children.OfType<CHR0EntryNode>().Sum(t => t.KeyArrays.Sum(c => c.KeyCount));
    static string OptimizationNativeIdentity(CHR0Node clip) => "root" + BrawlLibrarySource.Resources(clip.RootNode).Single(p => ReferenceEquals(p.Item1, clip)).Item2.Substring(1);

    static void OptimizationCacheDiagnostic(ModelForm form, CHR0Node clip, string originalContent, string output, string phase)
    {
        var json = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 256 * 1024 * 1024, RecursionLimit = 200 };
        string Digest(string text)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return text == null ? null : BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        var model = (MDL0Node)form.FighterEditor.TargetModel;
        var asset = (FighterAsset)Call(form, "SelectedFighterAsset");
        string key = (string)Call(form, "ControlKey"), state = (string)Call(form, "ControlState", key);
        var retarget = json.Deserialize<Dictionary<string, object>>(json.Serialize(asset.Retarget));
        if (retarget != null && retarget.TryGetValue("Library", out var value) && value is Dictionary<string, object> library)
        { library.Remove("LastExportSha256"); library.Remove("LastExportFormat"); }
        var pins = (form.FighterEditor.AnimationPins ?? new ControlPin[0]).Where(pin => Guid.TryParse(pin.Id, out _)).ToArray();
        var fit = (NativeTransitionFit)Call(form, "TransitionRecipeFor", asset, clip);
        string currentProtection = json.Serialize(new { Controls = state, Fit = fit, Retarget = retarget, Pins = pins });
        string stableProtection = json.Serialize(new { Controls = state, Fit = fit, Retarget = retarget });
        var cache = Field(form, "_optimizationBaseline");
        string cacheModel = cache == null ? null : (string)Field(cache, "ModelContent"), cacheProtection = cache == null ? null : (string)Field(cache, "Protection"),
            cacheOutput = cache == null ? null : (string)Field(cache, "OutputContent"), currentContent = NativeAnimationOptimization.ContentKey(clip),
            baseline = cache == null || Field(cache, "Baseline") == null ? null : SetNativeContent((CHR0Node)Field(cache, "Baseline")), modelHash = (string)Call(form, "NativeControlHash", model, ".mdl0");
        model.Export(Path.Combine(output, "optimization-cache-" + phase + ".mdl0"));
        File.WriteAllText(Path.Combine(output, "optimization-cache-" + phase + ".json"), json.Serialize(new {
            Phase = phase, ModelName = model.Name, ModelHash = modelHash, ModelSkeleton = BrawlRetargeter.SkeletonFingerprint(model),
            ModelDirty = model.IsDirty, ModelBranch = model.IsBranch, RootDirty = model.RootNode.IsDirty, Frame = form.FighterEditor.CurrentFrame,
            CacheExists = cache != null, CacheModelHash = cacheModel, ModelHashMatches = modelHash == cacheModel,
            CacheProtectionHash = Digest(cacheProtection), CurrentProtectionHash = Digest(currentProtection), StableProtectionHash = Digest(stableProtection),
            FullProtectionMatches = currentProtection == cacheProtection, StableProtectionMatches = stableProtection == cacheProtection,
            CurrentContentHash = Digest(currentContent), CacheOutputHash = Digest(cacheOutput), OutputMatches = currentContent == cacheOutput,
            BaselineHash = Digest(baseline), OriginalContentHash = Digest(originalContent), OriginalBaseline = baseline == originalContent,
            ControlKey = key, ControlStateHash = Digest(state), ControlState = state, CacheProtection = cacheProtection, CurrentProtection = currentProtection,
            CurrentStableProtection = stableProtection, Pins = pins.Length
        }));
    }

    static Dictionary<string, string> OptimizationUntouchedBytes(ModelForm form, CHR0Node selected)
    {
        var resources = BrawlLibrarySource.Resources(selected.RootNode).Where(pair => !ReferenceEquals(pair.Item1, selected) &&
            (pair.Item1 is NW4RAnimationNode || pair.Item1 is MDL0Node || pair.Item1 is TEX0Node || pair.Item1 is PLT0Node)).ToArray();
        if (resources.Length > 4096) throw new InvalidDataException("The untouched-resource fixture exceeds its 4096 first-class native-resource verification budget.");
        return resources.ToDictionary(pair => "root" + pair.Item2.Substring(1) + "|" + pair.Item1.GetType().Name,
            pair => (string)Call(form, "NativeControlHash", pair.Item1, "." + pair.Item1.ResourceFileType.ToString().ToLowerInvariant()), StringComparer.Ordinal);
    }

    static void OptimizationCheckUntouchedBytes(ModelForm form, CHR0Node selected, Dictionary<string, string> expected, string phase, string reportPath)
    {
        var actual = OptimizationUntouchedBytes(form, selected);
        string mismatch = expected.Where(pair => !actual.TryGetValue(pair.Key, out var value) || value != pair.Value).Select(pair => pair.Key).FirstOrDefault();
        Check(actual.Count == expected.Count && mismatch == null, "Fresh export bytes retain all " + expected.Count + " untouched first-class native motion resources at " + phase + (mismatch == null ? "" : "; mismatch " + mismatch));
        File.WriteAllText(reportPath, new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(actual));
    }

    static async Task OptimizationCrossClipCache(ModelForm form, CHR0Node a, MDL0Node model, string expectedBaseline,
        string expectedOutput, string output, Func<Func<AnimationOptimizationDialog, Task>, Task> open)
    {
        var editor = form.FighterEditor; var project = form.FighterProject;
        int aFrame = editor.CurrentFrame; var selectedBone = editor.SelectedBone;
        var aEntry = Field(form, "_optimizationBaseline");
        Check(aEntry != null && (bool)Field(aEntry, "HighQuality") && OptimizationRetainedBaseline(form) == expectedBaseline,
            "Cross-clip fixture starts with an actually applied HQ result and its original retained source");
        string originalHash = (string)Field(aEntry, "BaselineHash"), protection = (string)Field(aEntry, "Protection"), modelContent = (string)Field(aEntry, "ModelContent");
        var b = Walk(a.RootNode).OfType<CHR0Node>().First(clip => !ReferenceEquals(clip, a) && clip.FrameCount >= 12 && clip.FrameCount <= 256 &&
            clip.Children.OfType<CHR0EntryNode>().Any(track => track.Name == selectedBone.Name));
        using (var originalB = NativeAnimationOptimization.Copy(b))
        {
            string bContent = SetNativeContent(b);
            async Task Select(CHR0Node clip, int frame)
            {
                foreach (ListViewItem item in editor.leftPanel.listAnims.Items) item.Selected = ReferenceEquals(item.Tag, clip);
                editor.SelectedCHR0 = clip; await SimpleControlsReady(form);
                editor.SetFrame(Math.Max(1, Math.Min(frame, clip.FrameCount))); editor.UpdateModel();
                await Await<ControlReply>(Call(form, "LoadControls"));
                Check(ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.TargetAnimation, clip), "Cross-clip fixture selects the exact native animation member through its retained editor");
            }
            void DenseB()
            {
                var body = model.AllBones.Single(bone => bone.Name == selectedBone.Name);
                var entry = b.Children.OfType<CHR0EntryNode>().SingleOrDefault(track => track.Name == body.Name) ?? b.CreateEntry(body.Name);
                for (int channel = 0; channel < 9; channel++)
                {
                    while (entry.KeyArrays[channel].KeyCount > 0) entry.KeyArrays[channel].Remove(entry.KeyArrays[channel].Entries.First()._index);
                    for (int at = 0; at < b.FrameCount; at++) entry.Keyframes.SetFrameValue(channel, at, body.BindState[channel], true)._tangent = 0;
                }
                entry.UseModelScale = entry.UseModelRotate = entry.UseModelTranslate = false; entry.Keyframes.PreserveInterpolation = true;
                PreciseCHR0EntryNode.Promote(b); b.SignalPropertyChange(); editor.UpdateModel(); Call(form, "InvalidateControls");
            }
            try
            {
                await Select(b, 3); DenseB(); await SimpleControlsReady(form);
                await open(async window =>
                {
                    await window.Analyze(); Check(window.Candidate != null && window.PreparedCanApply, "A deliberately dense sibling B prepares a real useful Exact optimization");
                    SimpleControl<Button>(window, "optimizeApply").PerformClick();
                });
                Check(form.OptimizationCachedClips >= 2 && form.OptimizationCachedKeys <= 2000000 && form.OptimizationCachedBytes <= 256L * 1024 * 1024,
                    "Independent A and B originals coexist within the shared unchanged key/byte cache bounds");
                await Select(a, aFrame); int prepares = form.OptimizationPrepares;
                await open(async window =>
                {
                    Check(OptimizationRetainedBaseline(form) == expectedBaseline, "A to B to A navigation preserves A's complete original source snapshot");
                    SimpleControl<ComboBox>(window, "optimizationMode").SelectedIndex = 1; await window.Analyze();
                    Check(window.Candidate != null && !window.PreparedCanApply && SetNativeContent(window.Candidate) == expectedOutput && form.OptimizationPrepares == prepares,
                        "Returning to unchanged HQ clip A reuses its exact result without another engine pass or cumulative approximation");
                });
                aEntry = Field(form, "_optimizationBaseline"); long trueKeys = (long)Field(aEntry, "BaselineKeys");
                SetField(aEntry, "BaselineKeys", 2000000L);
                try
                {
                    // Synthetic accounted occupancy exercises the production LRU
                    // planner. The production limits and source curves are not
                    // enlarged or relaxed to force this deterministic eviction.
                    await Select(b, 3); DenseB(); await SimpleControlsReady(form);
                    await open(async window =>
                    {
                        await window.Analyze(); Check(window.Candidate != null && window.PreparedCanApply, "A second real sibling cleanup exercises shared-cache pressure");
                        SimpleControl<Button>(window, "optimizeApply").PerformClick();
                    });
                    Check(Field(aEntry, "Baseline") == null && (string)Field(aEntry, "BaselineHash") == originalHash && (long)Field(aEntry, "BaselineKeys") == 0,
                        "Shared-cap LRU eviction releases A's snapshot while retaining its exact original identity marker");
                }
                finally { if (Field(aEntry, "Baseline") != null) SetField(aEntry, "BaselineKeys", trueKeys); }
                Check(form.OptimizationCachedKeys <= 2000000 && form.OptimizationCachedBytes <= 256L * 1024 * 1024, "Eviction returns all real aggregate cache accounting within frozen limits");
                await Select(a, aFrame); prepares = form.OptimizationPrepares;
                bool exactApplied = false;
                await open(async window =>
                {
                    SimpleControl<ComboBox>(window, "optimizationMode").SelectedIndex = 1; await window.Analyze();
                    Check(window.Candidate != null && !window.PreparedCanApply && SetNativeContent(window.Candidate) == expectedOutput && form.OptimizationPrepares == prepares &&
                        SimpleControl<TextBox>(window, "optimizationReport").Text.Contains("Identity marker retained"), "An evicted same-mode HQ result remains an explicit no-op without losing its original identity");
                    SimpleControl<ComboBox>(window, "optimizationMode").SelectedIndex = 0; await window.Analyze();
                    Check(window.Candidate != null && form.OptimizationPrepares > prepares && (string)Field(Field(form, "_optimizationBaseline"), "BaselineHash") == originalHash,
                        "Exact cleanup remains available after snapshot eviction without silently replacing the original marker hash");
                    exactApplied = window.PreparedCanApply;
                    if (exactApplied) SimpleControl<Button>(window, "optimizeApply").PerformClick();
                });
                if (!exactApplied)
                {
                    // Model the cache transition of a possible Exact cleanup
                    // without changing document data or frozen resource limits.
                    var marker = Field(form, "_optimizationBaseline");
                    var type = typeof(ModelForm).GetNestedType("OptimizationCacheKey", System.Reflection.BindingFlags.NonPublic);
                    var cacheKey = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                        null, new object[] { project, model, a }, null);
                    string stage = Path.Combine(output, "cache-planner"); Directory.CreateDirectory(stage);
                    var commit = Call(form, "PrepareOptimizationCache", cacheKey, marker, (object)null, modelContent, protection,
                        NativeAnimationOptimization.ContentKey(a), NativeAnimationOptimization.StorageKey(a), false, false, "Harness Exact-mode missing-original marker; native curves unchanged.", stage);
                    using ((IDisposable)commit) Call(form, "CommitOptimizationCache", commit);
                }
                var exactMarker = Field(form, "_optimizationBaseline");
                Check(Field(exactMarker, "Baseline") == null && (string)Field(exactMarker, "BaselineHash") == originalHash && !(bool)Field(exactMarker, "HighQuality"),
                    "Exact-mode cache transition carries the evicted original identity without fabricating a replacement snapshot");
                string markerOutput = SetNativeContent(a); prepares = form.OptimizationPrepares;
                await open(async window =>
                {
                    await window.Analyze(); Check(window.Candidate != null && !window.PreparedCanApply && form.OptimizationPrepares == prepares, "Same-mode Exact marker reuse is a no-op without an engine pass");
                    SimpleControl<ComboBox>(window, "optimizationMode").SelectedIndex = 1; await window.Analyze();
                    Check(window.Candidate == null && !window.PreparedCanApply && !SimpleControl<Button>(window, "optimizeApply").Enabled &&
                        SimpleControl<Label>(window, "optimizationSummary").Text.Contains("evicted") && form.OptimizationPrepares == prepares && SetNativeContent(a) == markerOutput,
                        "HQ requiring an evicted original is explicitly blocked before engine work or native changes");
                });
                // Exact bind cleanup may legitimately omit this target track.
                // A later native edit creates it for the same existing bone.
                var authored = a.Children.OfType<CHR0EntryNode>().SingleOrDefault(entry => entry.Name == selectedBone.Name) ?? a.CreateEntry(selectedBone.Name);
                for (int channel = 3; channel < 6; channel++)
                    if (authored.KeyArrays[channel].KeyCount == 0) authored.SetKeyframe(channel, 0, selectedBone.BindState[channel], true, true)._tangent = 0;
                authored.UseModelRotate = false; authored.Keyframes.PreserveInterpolation = true;
                authored.KeyArrays.Skip(3).Take(3).First(array => array.KeyCount > 0).Entries.First()._value += .125f;
                a.SignalPropertyChange(); editor.UpdateModel(); Call(form, "InvalidateControls"); await SimpleControlsReady(form);
                string edited = SetNativeContent(a);
                await open(async window =>
                {
                    Check(Field(form, "_optimizationBaseline") == null, "An actual authored curve edit invalidates only A's stale identity marker");
                    SimpleControl<ComboBox>(window, "optimizationMode").SelectedIndex = 1; await window.Analyze();
                    Check(window.Candidate != null && SetNativeContent(a) == edited && SimpleControl<TextBox>(window, "optimizationReport").Text.Contains("Baseline: current animation frozen at Analyze"),
                        "An explicit authored edit establishes and identifies a new usable HQ baseline instead of bypassing the missing-original guard");
                });
                File.WriteAllText(Path.Combine(output, "optimization-crossclip-cache.txt"), "Real A(HQ) -> B(Exact) -> A reuse; shared production-cap LRU via synthetic accounted occupancy; retained identity markers; same-mode no-op; Exact availability; missing-original HQ block; explicit authored-edit reset.\r\nOriginal A baseline: " + originalHash +
                    "\r\nCached clips: " + form.OptimizationCachedClips + "\r\nRetained keys: " + form.OptimizationCachedKeys + "\r\nRetained bytes: " + form.OptimizationCachedBytes);
            }
            finally
            {
                ReplayRestoreCurrent(b, originalB);
                Check(SetNativeContent(b) == bContent, "The deliberately authored sibling fixture restores its exact original native metadata and semantic curves");
                if (!ReferenceEquals(editor.SelectedCHR0, a)) await Select(a, aFrame);
                editor.SelectedBone = selectedBone;
            }
        }
    }

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
            var untouchedBytes = OptimizationUntouchedBytes(form, clip);
            File.WriteAllText(Path.Combine(output, "optimization-untouched-before.json"), new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(untouchedBytes));
            string before = SetNativeContent(clip);
            using var motionBaseline = NativeAnimationOptimization.Copy(clip);
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
                Check(mode.Items.Count == 3 && mode.SelectedIndex == 0 && Convert.ToString(mode.SelectedItem) == "Exact" && Convert.ToString(mode.Items[1]) == "High quality" && !window.ReadHighQuality() && !window.ReadSmallFile(), "Exact remains the default while the separate High quality and opt-in Small file options are available");
                var optionalStorage=SimpleDescendants(window).OfType<CheckBox>().ToArray();
                Check(optionalStorage.Length==1&&optionalStorage[0].Name=="optimizationCompactStorage"&&!optionalStorage[0].Checked&&
                    !SimpleControl<Button>(window, "optimizeApply").Enabled,
                    "Optimization keeps optional smaller storage off by default and explicit Analyze/Apply without an extra required review checkbox");
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
                if (!window.IsDisposed) File.WriteAllText(Path.Combine(output, "optimization-apply-status.txt"), SimpleControl<Label>(window, "optimizationSummary").Text);
            });
            File.WriteAllText(Path.Combine(output, "optimization-apply-diagnostic.json"), new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 16000000 }.Serialize(new {
                SameClip = ReferenceEquals(editor.SelectedCHR0, clip), SameTarget = ReferenceEquals(editor.TargetModel, model), SameBone = ReferenceEquals(editor.SelectedBone, bodyBone),
                Frame = editor.CurrentFrame, ExpectedFrame = frameBefore, Assets = project.Animations.Count, ExpectedAssets = assetsBefore,
                ContentMatches = SetNativeContent(clip) == optimized, Undo = editor._undoSaves.Count, ExpectedUndo = undoBefore + 1,
                Expected = optimized, Actual = SetNativeContent(clip)
            }));
            Check(ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.TargetModel, model) && ReferenceEquals(editor.SelectedBone, bodyBone) && editor.CurrentFrame == frameBefore && project.Animations.Count == assetsBefore && SetNativeContent(clip) == optimized,
                "Apply optimizes the exact current member while retaining the target, selected clip/bone/frame and asset count");
            Check(editor._undoSaves.Count == undoBefore + 1 && siblings.All(p => SetNativeContent(p.Key) == p.Value), "Optimization records one native Undo command and preserves all sibling animations");
            ReplayRootsMatch(motionBaseline, clip, model, (setup.ProtectedMotionBones ?? new int[0]).Select(i => setup.BoneNames[i]).ToArray());
            Call(form, "SyncBatchReview"); Check(!ReviewApproved(review), "Applying optimization invalidates approval of the changed native member");
            editor.Undo(); await SimpleControlsReady(form); Check(SetNativeContent(clip) == before && siblings.All(p => SetNativeContent(p.Key) == p.Value), "Native Undo restores all pre-optimization keys and the removed foreign track");
            editor.Redo(); await SimpleControlsReady(form); Check(SetNativeContent(clip) == optimized && !clip.Children.Any(t => t.Name == foreign), "Native Redo restores the same optimized current member without analyzing again");

            string export = Path.Combine(output, "optimized-current.chr0"); form.ExportSimpleWorkspaceResult(export);
            using (var decoded = NodeFactory.FromFile(null, export) as CHR0Node) Check(decoded != null && SetNativeContent(decoded) == optimized, "Independent native export/reopen retains the optimized exact current clip");
            string saved = Path.Combine(output, "optimized-current.fpstudio"); form.SaveFighterProject(saved);
            OptimizationCheckUntouchedBytes(form, clip, untouchedBytes, "Exact project Save", Path.Combine(output, "optimization-untouched-after-save.json"));
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
                OptimizationCheckUntouchedBytes(reopened, restored, untouchedBytes, "Exact fresh project reopen", Path.Combine(output, "optimization-untouched-after-reopen.json"));
                foreach (var sibling in siblings)
                {
                    string siblingId = OptimizationNativeIdentity(sibling.Key);
                    var restoredSibling = Walk(restored.RootNode).OfType<CHR0Node>().Single(c => OptimizationNativeIdentity(c) == siblingId);
                    Check(SetNativeContent(restoredSibling) == sibling.Value, "Project reopening preserves sibling member " + siblingId);
                }
                reopened.FighterEditor.FrameFighterModel(); await CaptureSimpleWorkspace(reopened, Path.Combine(output, "optimization-reopened.png"));
                SimpleDisposeForm(reopened);
            }
            Check(OptimizationRetainedBaseline(form) == before, "Exact Apply retains the original independent session baseline before High-quality work");
            OptimizationCacheDiagnostic(form, clip, before, output, "before-hq");
            string targetBeforeManualChange = (string)Call(form, "NativeControlHash", model, ".mdl0");
            var qualityWitness = new GameLibraryDocument(form);
            await Open(async window =>
            {
                var mode = SimpleControl<ComboBox>(window, "optimizationMode"); int prepares = form.OptimizationPrepares;
                mode.SelectedIndex = 1;
                Check(window.ReadHighQuality() && window.Candidate == null && form.OptimizationPrepares == prepares, "Selecting High quality invalidates the preview and does not automatically prepare work");
                var actualPrepare = window.Prepare; var pending = new TaskCompletionSource<CHR0Node>();
                window.Prepare = quality => pending.Task; var cancelled = window.Analyze();
                SimpleControl<Button>(window, "optimizationCancel").PerformClick();
                Check(window.Busy && !SimpleControl<Button>(window, "optimizeAnalyze").Enabled && !mode.Enabled, "HQ cancellation keeps ownership and mode locked until its preparation task actually ends");
                pending.SetResult(new CHR0Node { Name = clip.Name, FrameCount = clip.FrameCount, Loop = clip.Loop }); await cancelled;
                Check(!window.Busy && window.Candidate == null, "HQ cancellation discards a late independent candidate without publishing curves");
                window.Prepare = actualPrepare;
                await window.Analyze();
                Check(window.Candidate != null, "Real High-quality preparation completes its independent native gates: " + SimpleControl<Label>(window, "optimizationSummary").Text);
                Check(OptimizationRetainedBaseline(form) == before, "HQ preparation keeps the original source baseline instead of accumulating on Exact output");
                using (var retained = NativeAnimationOptimization.Copy((CHR0Node)Field(Field(form, "_optimizationBaseline"), "Baseline")))
                {
                    OptimizationWithheldGeometry(window.SourceView, window.TargetView, retained, window.Candidate, Path.Combine(output, "optimization-hq-withheld.json"));
                    ReplayRootsMatch(retained, window.Candidate, model, (setup.ProtectedMotionBones ?? new int[0]).Select(i => setup.BoneNames[i]).ToArray());
                }
                qualityWitness.CheckPreserved("High-quality independent native preparation");
                foreach (int percent in new[] { 100, 175 }) foreach (var size in new[] { new Size(980, 720), new Size(1600, 960) })
                {
                    Call(form, "SetWorkspaceTextSize", percent); window.ApplyTextSize(); window.ClientSize = size; window.PerformLayout();
                    foreach (string name in new[] { "optimizationMode", "optimizeAnalyze", "optimizeApply", "optimizationClose", "optimizationFrame", "optimizationSummary" })
                    {
                        var control = SimpleControl<Control>(window, name);
                        Check(control.Visible && window.ClientRectangle.Contains(window.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))), "HQ control fits the client area: " + name + " / " + size.Width + " / " + percent + "%");
                    }
                    await CaptureOptimization(window, Path.Combine(output, "optimization-hq-" + size.Width + "-" + percent + ".png"));
                }
                Call(form, "SetWorkspaceTextSize", 100); window.ApplyTextSize();
                int afterPrepare = form.OptimizationPrepares;
                mode.SelectedIndex = 0;
                Check(window.Candidate == null && !SimpleControl<Button>(window, "optimizeApply").Enabled && form.OptimizationPrepares == afterPrepare, "Switching HQ to Exact discards the candidate without running another optimizer");
                mode.SelectedIndex = 1; await window.Analyze();
                Check(window.Candidate != null, "HQ can prepare again after a real mode change");
                string nameBefore = model.Name;
                OptimizationCacheDiagnostic(form, clip, before, output, "before-stale-model");
                try
                {
                    model.Name = nameBefore + " changed target"; model.SignalPropertyChange();
                    OptimizationCacheDiagnostic(form, clip, before, output, "renamed-model");
                    SimpleControl<Button>(window, "optimizeApply").PerformClick();
                    // A no-op has no active Apply action. Its preparation still
                    // receives a stale target check if explicitly requested.
                    if (window.Candidate != null) await window.Analyze();
                    Check(window.Candidate == null && SetNativeContent(clip) == optimized, "A changed native target rejects HQ publication/Apply and preserves current animation curves");
                }
                finally { model.Name = nameBefore; model.SignalPropertyChange(); OptimizationCacheDiagnostic(form, clip, before, output, "restored-model-immediate"); }
            });
            OptimizationCacheDiagnostic(form, clip, before, output, "restored-model-settled");
            bool targetBytesChanged = (string)Call(form, "NativeControlHash", model, ".mdl0") != targetBeforeManualChange;
            // A native rename/restore can rebuild a genuinely different MDL0
            // byte revision despite matching decoded bind data. Keep the exact
            // target dependency, and explicitly identify the new clip baseline.
            string qualityBaselineExpected = targetBytesChanged ? optimized : before;
            Check(targetBytesChanged ? SetNativeContent(clip) == optimized : OptimizationRetainedBaseline(form) == before,
                targetBytesChanged ? "The manual target edit changed exact native model bytes; the next operation must identify the current Exact clip as its new baseline" :
                "A byte-identical target restore retains the original optimization baseline");
            Check(SetNativeContent(clip) == optimized && siblings.All(p => SetNativeContent(p.Key) == p.Value), "HQ cancellation, mode switching and stale-target rejection preserve Exact output and siblings");
            string qualityOutput = null; bool qualityApplied = false; int qualityUndo = editor._undoSaves.Count;
            await Open(async window =>
            {
                OptimizationCacheDiagnostic(form, clip, before, output, "fresh-hq-opened");
                if (targetBytesChanged) Check(Field(form, "_optimizationBaseline") == null, "A changed exact target revision invalidates the old baseline before fresh HQ work");
                SimpleControl<ComboBox>(window, "optimizationMode").SelectedIndex = 1; await window.Analyze();
                OptimizationCacheDiagnostic(form, clip, before, output, "fresh-hq-prepared");
                Check(window.Candidate != null, "Fresh HQ operation prepares after stale target restoration");
                qualityOutput = SetNativeContent(window.Candidate); qualityApplied = window.PreparedCanApply;
                if (targetBytesChanged) Check(SimpleControl<TextBox>(window, "optimizationReport").Text.Contains("Baseline: current animation frozen at Analyze"), "HQ explicitly reports the newly frozen current baseline after a manual target revision");
                File.WriteAllText(Path.Combine(output, "optimization-hq-report.txt"), SimpleControl<TextBox>(window, "optimizationReport").Text);
                if (qualityApplied) SimpleControl<Button>(window, "optimizeApply").PerformClick();
                else Check(qualityOutput == optimized, "HQ without further useful savings honestly retains the Exact current clip");
            });
            Check(SetNativeContent(clip) == qualityOutput && editor._undoSaves.Count == qualityUndo + (qualityApplied ? 1 : 0) &&
                ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.TargetModel, model) && ReferenceEquals(editor.SelectedBone, bodyBone) && editor.CurrentFrame == frameBefore,
                "HQ Apply/no-op preserves exact current ownership and records one command only for useful changes");
            if (qualityApplied)
            {
                editor.Undo(); await SimpleControlsReady(form); Check(SetNativeContent(clip) == optimized, "HQ Undo restores the exact preceding Exact result");
                editor.Redo(); await SimpleControlsReady(form); Check(SetNativeContent(clip) == qualityOutput, "HQ Redo restores the same independently verified result");
            }
            OptimizationCacheDiagnostic(form, clip, before, output, "hq-undo-redo");
            bool QualityBaselineRetained() => OptimizationRetainedBaseline(form) == qualityBaselineExpected ||
                !qualityApplied && OptimizationRetainedBaseline(form) == null && qualityOutput == optimized;
            Check(QualityBaselineRetained(), "HQ Apply/Undo/Redo retains its explicitly identified session source instead of the last approximation");
            string qualityExport = Path.Combine(output, "optimized-current-hq.chr0"); form.ExportSimpleWorkspaceResult(qualityExport);
            using (var decoded = NodeFactory.FromFile(null, qualityExport) as CHR0Node) Check(decoded != null && SetNativeContent(decoded) == qualityOutput, "HQ native export independently reopens exactly");
            await Open(async window =>
            {
                Check(QualityBaselineRetained(), "Ordinary export does not replace the HQ session's identified original baseline");
                var mode = SimpleControl<ComboBox>(window, "optimizationMode");
                await window.Analyze(); Check(window.Candidate != null && SetNativeContent(clip) == qualityOutput, "Exact preview after HQ is independent and leaves the current quality result unchanged");
                mode.SelectedIndex = 1; int prepares = form.OptimizationPrepares; await window.Analyze();
                Check(window.Candidate != null && !window.PreparedCanApply && SetNativeContent(window.Candidate) == qualityOutput && QualityBaselineRetained(),
                    "Switching Exact back to HQ after export retains the same result and original baseline without cumulative approximation");
                if (qualityApplied) Check(form.OptimizationPrepares == prepares, "Unchanged applied HQ result is reused without another engine pass");
            });
            string qualitySaved = Path.Combine(output, "optimized-current-hq.fpstudio"); form.SaveFighterProject(qualitySaved);
            OptimizationCheckUntouchedBytes(form, clip, untouchedBytes, "HQ project Save", Path.Combine(output, "optimization-hq-untouched-after-save.json"));
            using (var fresh = FighterProject.Open(qualitySaved)) using (var reopened = new ModelForm())
            {
                reopened.InitializeFighterProject(fresh); reopened.Show();
                await GameLibraryWait(() => reopened.FighterEditor.TargetModel is MDL0Node && reopened.FighterEditor.SelectedCHR0 != null && OptimizationNativeIdentity(reopened.FighterEditor.SelectedCHR0) == identity,
                    "Fresh HQ project did not finish restoring its exact native member.", 60);
                await SimpleControlsReady(reopened);
                Check(SetNativeContent(reopened.FighterEditor.SelectedCHR0) == qualityOutput, "HQ Save/fresh reopen retains the exact native current result");
                OptimizationCheckUntouchedBytes(reopened, reopened.FighterEditor.SelectedCHR0, untouchedBytes, "HQ fresh project reopen", Path.Combine(output, "optimization-hq-untouched-after-reopen.json"));
                SimpleDisposeForm(reopened);
            }
            Check(siblings.All(p => SetNativeContent(p.Key) == p.Value), "HQ mode and baseline reuse retain every sibling animation");
            Check(qualityApplied, "Cross-clip qualification has an actually applied High-quality A result");
            await OptimizationCrossClipCache(form, clip, model, qualityBaselineExpected, qualityOutput, output, Open);

            // Establish a separate real baseline under an authored FK pin, then
            // distinguish transient viewport pose/membership from saved edits.
            int pinFrame = editor.CurrentFrame, otherFrame = 1 + (Math.Max(0, pinFrame - 1) + Math.Max(1, clip.FrameCount / 2)) % clip.FrameCount;
            editor.UpdateModel();
            var originalPositions = model.AllBones.ToDictionary(bone => bone.Name, bone => new[] { bone._frameMatrix[12], bone._frameMatrix[13], bone._frameMatrix[14] });
            editor.SetFrame(otherFrame); editor.UpdateModel();
            var pinBone = model.AllBones.Where(bone => !setup.ProtectedMotionBones.Select(i => setup.BoneNames[i]).Contains(bone.Name) &&
                !setup.Chains.Any(chain => setup.BoneNames[chain.Effector] == bone.Name))
                .Select(bone => new { Bone = bone, Motion = Enumerable.Range(0, 3).Sum(axis => Math.Pow((double)bone._frameMatrix[12 + axis] - originalPositions[bone.Name][axis], 2)) })
                .OrderByDescending(value => value.Motion).First();
            Check(pinBone.Motion > .000001, "Pinned-cache fixture chooses an actual moving existing bone instead of a name heuristic");
            editor.SetFrame(pinFrame); editor.UpdateModel(); editor.SelectedBone = pinBone.Bone; await SimpleControlsReady(form);
            await (Task)Call(form, "AttachControlPin", false); await SimpleControlsReady(form);
            var authoredPin = editor.AnimationPins.Single(pin => pin.BoneName == pinBone.Bone.Name && Guid.TryParse(pin.Id, out _));
            Check(!authoredPin.Soft, "Pinned-cache fixture attaches an authored FK bone pin with a stable identity");
            bool Ancestor(MDL0BoneNode ancestor, MDL0BoneNode bone)
            { for (var current = bone; current != null; current = current.Parent as MDL0BoneNode) if (ReferenceEquals(current, ancestor)) return true; return false; }
            var freeBone = model.AllBones.First(bone => !setup.ProtectedMotionBones.Select(i => setup.BoneNames[i]).Contains(bone.Name) &&
                !Ancestor(bone, pinBone.Bone) && Enumerable.Range(0, 3).All(channel => bone.BindState[channel] != 0));
            var dense = clip.Children.OfType<CHR0EntryNode>().SingleOrDefault(entry => entry.Name == freeBone.Name) ?? clip.CreateEntry(freeBone.Name);
            for (int channel = 0; channel < 9; channel++)
            {
                while (dense.KeyArrays[channel].KeyCount > 0) dense.KeyArrays[channel].Remove(dense.KeyArrays[channel].Entries.First()._index);
                for (int at = 0; at < clip.FrameCount; at++) dense.Keyframes.SetFrameValue(channel, at, freeBone.BindState[channel], true)._tangent = 0;
            }
            dense.UseModelScale = dense.UseModelRotate = dense.UseModelTranslate = false; dense.Keyframes.PreserveInterpolation = true;
            PreciseCHR0EntryNode.Promote(clip); clip.SignalPropertyChange(); editor.UpdateModel(); Call(form, "InvalidateControls"); await SimpleControlsReady(form);
            string pinnedBaseline = SetNativeContent(clip), pinnedOutput = null; int pinnedUndo = editor._undoSaves.Count;
            await Open(async window =>
            {
                await window.Analyze(); Check(window.Candidate != null && window.PreparedCanApply, "A real pinned-source Exact cleanup establishes a new independently verified session baseline");
                pinnedOutput = SetNativeContent(window.Candidate); SimpleControl<Button>(window, "optimizeApply").PerformClick();
            });
            Check(SetNativeContent(clip) == pinnedOutput && OptimizationRetainedBaseline(form) == pinnedBaseline && editor._undoSaves.Count == pinnedUndo + 1,
                "Pinned-source Apply preserves the authenticated pin setup and retains its pre-optimization native source");
            string pinKey = (string)Call(form, "ControlKey"), authoredState = (string)Call(form, "ControlState", pinKey);
            var oldPosition = editor.AnimationPins.Single(pin => pin.Id == authoredPin.Id).Position.ToArray();
            editor.SetFrame(otherFrame); editor.UpdateModel(); await Await<ControlReply>(Call(form, "LoadControls"));
            var runtimePin = editor.AnimationPins.Single(pin => pin.Id == authoredPin.Id);
            Check(runtimePin.Position.Where((value, axis) => Math.Abs((double)value - oldPosition[axis]) > .0001).Any() && (string)Call(form, "ControlState", pinKey) == authoredState,
                "Scrubbing refreshes the pin's actual native runtime pose without changing its authoritative saved state");
            int pinnedPrepares = form.OptimizationPrepares;
            await Open(async window =>
            {
                Check(OptimizationRetainedBaseline(form) == pinnedBaseline, "Scrubbing an authored pin keeps the original optimization baseline");
                await window.Analyze();
                Check(window.Candidate != null && !window.PreparedCanApply && SetNativeContent(window.Candidate) == pinnedOutput && form.OptimizationPrepares == pinnedPrepares,
                    "Pinned preview pose refresh reuses the unchanged prepared result without another approximation");
            });
            var runtimePins = editor.AnimationPins;
            try
            {
                editor.AnimationPins = new ControlPin[0];
                await Open(async window =>
                {
                    Check(OptimizationRetainedBaseline(form) == pinnedBaseline && (string)Call(form, "ControlState", pinKey) == authoredState,
                        "Transient viewport pin membership does not erase the complete authored-constraint baseline");
                    await window.Analyze();
                    Check(window.Candidate != null && !window.PreparedCanApply && form.OptimizationPrepares == pinnedPrepares && SetNativeContent(clip) == pinnedOutput,
                        "A missing runtime pin marker cannot reset the baseline or permit cumulative approximation");
                });
            }
            finally { editor.AnimationPins = runtimePins; }
            editor.SelectedAnimationPin = authoredPin.Id; await (Task)Call(form, "RemoveControlPin"); await SimpleControlsReady(form);
            Check((string)Call(form, "ControlState", pinKey) != authoredState, "Actual authored pin removal changes the authoritative constraint dependency");
            await Open(async window =>
            {
                Check(Field(form, "_optimizationBaseline") == null, "Actual authored pin removal invalidates the old baseline before another operation");
                await window.Analyze();
                Check(window.Candidate != null && SetNativeContent(clip) == pinnedOutput && SimpleControl<TextBox>(window, "optimizationReport").Text.Contains("Baseline: current animation frozen at Analyze"),
                    "A changed authored constraint is reported as a newly frozen current baseline while preparation leaves native curves unchanged");
            });
            Check(siblings.All(p => SetNativeContent(p.Key) == p.Value), "Authored-pin baseline and runtime refresh checks preserve every sibling animation");
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
