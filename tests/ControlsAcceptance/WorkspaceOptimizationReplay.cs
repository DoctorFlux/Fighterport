using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB;
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
    static int ReplayFloatBits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);

    static string OptimizationRetainedBaseline(ModelForm form)
    {
        var cache = Field(form, "_optimizationBaseline");
        var baseline = cache == null ? null : (CHR0Node)Field(cache, "Baseline");
        return baseline == null ? null : SetNativeContent(baseline);
    }

    // Independent native CPU skin/FK witnesses deliberately use fractions absent
    // from the reducer and adapter's regular probe schedule. Both models are
    // dialog-owned snapshots; the editable native model is never posed here.
    static void OptimizationWithheldGeometry(FittingModelView beforeView, FittingModelView afterView,
        CHR0Node baseline, CHR0Node candidate, string reportPath)
    {
        var original = beforeView.Model; var result = afterView.Model;
        var bones = original.AllBones.ToArray(); var targetBones = result.AllBones.ToDictionary(b => b.Name, StringComparer.Ordinal);
        var originalVertices = original.Objects.OfType<MDL0ObjectNode>().Where(o => o.Vertices != null).SelectMany(o => o.Vertices).ToArray();
        var resultVertices = result.Objects.OfType<MDL0ObjectNode>().Where(o => o.Vertices != null).SelectMany(o => o.Vertices).ToArray();
        Check(originalVertices.Length > 0 && originalVertices.Length == resultVertices.Length && bones.All(b => targetBones.ContainsKey(b.Name)), "Independent HQ witnesses retain the complete native target skeleton and skin");
        original.ApplyCHR(null, 0);
        var bind = originalVertices.Select(v => v.WeightedPosition).ToArray();
        double length = Math.Sqrt(Math.Pow(bind.Max(p => p._x) - bind.Min(p => p._x), 2) + Math.Pow(bind.Max(p => p._y) - bind.Min(p => p._y), 2) + Math.Pow(bind.Max(p => p._z) - bind.Min(p => p._z), 2));
        Check(!double.IsNaN(length) && !double.IsInfinity(length) && length > 0, "Independent HQ bounds use actual native bind-geometry diagonal");
        double worldBound = .0001 * length, localBound = Math.Min(.0002, .0001 * length);
        var times = new SortedSet<float> { 0, baseline.FrameCount - 1, baseline.Loop ? baseline.FrameCount : baseline.FrameCount - .0001f };
        for (int frame = 0; frame < baseline.FrameCount - 1; frame++) foreach (float fraction in new[] { .211f, .457f, .913f }) times.Add(frame + fraction);
        double maximumWorld = 0, maximumSkin = 0, maximumEuler = 0, maximumOrientation = 0, maximumScale = 0, maximumLocal = 0, maximumWorldSpeed = 0, maximumSkinSpeed = 0;
        var discontinuities = baseline.Children.OfType<CHR0EntryNode>().SelectMany(entry => entry.KeyArrays)
            .SelectMany(array => array.Entries.GroupBy(key => key._index).Where(group => group.Count() > 1).Select(group => (float)group.Key)).Distinct().ToArray();
        double[] previousWorld = null, previousSkin = null; float previousTime = 0;
        foreach (float time in times)
        {
            original.ApplyCHR(baseline, time + 1); result.ApplyCHR(candidate, time + 1);
            var worldErrors = new double[bones.Length * 12]; var skinErrors = new double[originalVertices.Length * 3];
            double atWorld = 0, atSkin = 0, atEuler = 0, atOrientation = 0, atScale = 0, atLocal = 0;
            for (int i = 0; i < bones.Length; i++)
            {
                var a = bones[i]; var b = targetBones[a.Name];
                atOrientation = Math.Max(atOrientation, LoopIndependentAngle(Enumerable.Range(3, 3).Select(c => (double)a.FrameState[c]).ToArray(), Enumerable.Range(3, 3).Select(c => (double)b.FrameState[c]).ToArray()));
                atEuler = Math.Max(atEuler, Enumerable.Range(3, 3).Max(c => Math.Abs((double)a.FrameState[c] - b.FrameState[c])));
                atScale = Math.Max(atScale, Enumerable.Range(0, 3).Max(c => Math.Abs((double)a.FrameState[c] - b.FrameState[c])));
                atLocal = Math.Max(atLocal, Math.Sqrt(Enumerable.Range(6, 3).Sum(c => Math.Pow((double)a.FrameState[c] - b.FrameState[c], 2))));
                for (int column = 0; column < 4; column++)
                {
                    double squared = 0;
                    for (int row = 0; row < 3; row++)
                    {
                        double difference = ((double)b._frameMatrix[column * 4 + row] - a._frameMatrix[column * 4 + row]) * (column < 3 ? length : 1);
                        worldErrors[i * 12 + column * 3 + row] = difference; squared += difference * difference;
                    }
                    atWorld = Math.Max(atWorld, Math.Sqrt(squared));
                }
            }
            for (int vertex = 0; vertex < originalVertices.Length; vertex++)
            {
                var a = originalVertices[vertex].WeightedPosition; var b = resultVertices[vertex].WeightedPosition;
                double x = (double)b._x - a._x, y = (double)b._y - a._y, z = (double)b._z - a._z;
                skinErrors[vertex * 3] = x; skinErrors[vertex * 3 + 1] = y; skinErrors[vertex * 3 + 2] = z;
                atSkin = Math.Max(atSkin, Math.Sqrt(x * x + y * y + z * z));
            }
            maximumWorld = Math.Max(maximumWorld, atWorld); maximumSkin = Math.Max(maximumSkin, atSkin); maximumEuler = Math.Max(maximumEuler, atEuler);
            maximumOrientation = Math.Max(maximumOrientation, atOrientation); maximumScale = Math.Max(maximumScale, atScale); maximumLocal = Math.Max(maximumLocal, atLocal);
            Check(atWorld <= worldBound && atSkin <= worldBound && atEuler <= .01 && atOrientation <= .01 && atScale <= .00001 && atLocal <= localBound,
                "Independent held-out HQ native local/FK/full-skin bounds pass at " + time.ToString("G8"));
            if (previousWorld != null && time - previousTime >= 1f / 64 && !discontinuities.Any(knot => knot > previousTime && knot <= time))
            {
                double delta = time - previousTime, worldSpeed = 0, skinSpeed = 0;
                for (int i = 0; i < worldErrors.Length; i += 3) worldSpeed = Math.Max(worldSpeed, Math.Sqrt(Enumerable.Range(i, 3).Sum(j => Math.Pow((worldErrors[j] - previousWorld[j]) / delta, 2))));
                for (int i = 0; i < skinErrors.Length; i += 3) skinSpeed = Math.Max(skinSpeed, Math.Sqrt(Enumerable.Range(i, 3).Sum(j => Math.Pow((skinErrors[j] - previousSkin[j]) / delta, 2))));
                maximumWorldSpeed = Math.Max(maximumWorldSpeed, worldSpeed); maximumSkinSpeed = Math.Max(maximumSkinSpeed, skinSpeed);
                Check(worldSpeed <= worldBound && skinSpeed <= worldBound, "Independent held-out HQ cumulative error-chord bounds pass at " + time.ToString("G8"));
            }
            previousWorld = worldErrors; previousSkin = skinErrors; previousTime = time;
        }
        File.WriteAllText(reportPath, new JavaScriptSerializer().Serialize(new { NativeIndependent = true, Samples = times.Count, Bones = bones.Length, Vertices = originalVertices.Length,
            Length = length, WorldBound = worldBound, LocalBound = localBound, MaximumWorld = maximumWorld, MaximumSkin = maximumSkin,
            MaximumEuler = maximumEuler, MaximumOrientation = maximumOrientation, MaximumScale = maximumScale, MaximumLocal = maximumLocal,
            MaximumWorldErrorChord = maximumWorldSpeed, MaximumSkinErrorChord = maximumSkinSpeed, ContinuousGeometryCertificate = false }));
        beforeView.Pose(baseline, 1); afterView.Pose(candidate, 1);
    }

    static bool ReplayBindTrackExact(CHR0EntryNode entry, MDL0BoneNode bone)
    {
        if (bone == null || entry.ScaleCompensateApply != bone.SegScaleCompApply ||
            entry.ScaleCompensateParent != bone.SegScaleCompParent || entry.ClassicScaleOff != !bone.ClassicScale) return false;
        return Enumerable.Range(0, 9).All(channel =>
        {
            float bind = bone.BindState[channel]; var keys = entry.KeyArrays[channel].Entries.ToArray();
            return !float.IsNaN(bind) && !float.IsInfinity(bind) && ReplayFloatBits(bind) != int.MinValue &&
                keys.Select(key => key._index).Distinct().Count() == keys.Length && keys.All(key =>
                    ReplayFloatBits(key._value) == ReplayFloatBits(bind) && ReplayFloatBits(key._tangent) == 0);
        });
    }

    static bool ReplayKnotMatches(CHR0Node before, CHR0Node after, int frame, ISet<string> targetNames, MDL0Node model = null)
    {
        foreach (var entry in before.Children.OfType<CHR0EntryNode>().Where(t => targetNames.Contains(t.Name)))
        {
            var candidate = after.Children.OfType<CHR0EntryNode>().SingleOrDefault(t => t.Name == entry.Name);
            if (candidate == null)
            {
                if (model == null || !ReplayBindTrackExact(entry, model.AllBones.SingleOrDefault(b => b.Name == entry.Name))) return false;
                continue;
            }
            for (int channel = 0; channel < 9; channel++)
            {
                var original = entry.KeyArrays[channel].Entries.Where(k => k._index == frame).ToArray();
                if (original.Length == 0) continue;
                var actual = candidate.KeyArrays[channel].Entries.Where(k => k._index == frame).ToArray();
                // Independent oracle for an exact mathematical constant, not
                // an exception based on what the optimizer says it removed.
                var all = entry.KeyArrays[channel].Entries.ToArray();
                var fixedKeys = candidate.KeyArrays[channel].Entries.ToArray();
                bool exactConstant = all.Length != 0 && ReplayFloatBits(all[0]._value) != int.MinValue &&
                    !float.IsNaN(all[0]._value) && !float.IsInfinity(all[0]._value) &&
                    all.Select(k => k._index).Distinct().Count() == all.Length &&
                    all.All(k => ReplayFloatBits(k._value) == ReplayFloatBits(all[0]._value) && ReplayFloatBits(k._tangent) == 0) &&
                    fixedKeys.Length == 1 && fixedKeys[0]._index == 0 && ReplayFloatBits(fixedKeys[0]._value) == ReplayFloatBits(all[0]._value) &&
                    ReplayFloatBits(fixedKeys[0]._tangent) == 0;
                if (exactConstant) continue;
                if (actual.Length != original.Length || original.Where((key, index) =>
                    ReplayFloatBits(key._value) != ReplayFloatBits(actual[index]._value) ||
                    ReplayFloatBits(key._tangent) != ReplayFloatBits(actual[index]._tangent)).Any()) return false;
            }
        }
        return true;
    }

    static void ReplayRestoreCurrent(CHR0Node destination, CHR0Node frozen)
    {
        if (destination.Name != frozen.Name || destination.FrameCount != frozen.FrameCount || destination.Loop != frozen.Loop)
            throw new InvalidDataException("The frozen original Next source does not match this current native clip identity and timing.");
        var names = new HashSet<string>(frozen.Children.Select(c => c.Name), StringComparer.Ordinal);
        foreach (var extra in destination.Children.Where(c => !names.Contains(c.Name)).ToArray()) extra.Remove();
        destination.Version = frozen.Version; destination.FrameCount = frozen.FrameCount; destination.Loop = frozen.Loop; destination.OriginalPath = frozen.OriginalPath;
        destination.UserEntries.Clear();
        foreach (UserDataClass user in frozen.UserEntries)
            destination.UserEntries.Add(new UserDataClass { Name = user.Name, DataType = user.DataType, Entries = user.Entries.ToArray() });
        foreach (var original in frozen.Children.OfType<CHR0EntryNode>())
        {
            var entry = destination.Children.OfType<CHR0EntryNode>().SingleOrDefault(t => t.Name == original.Name) ?? destination.CreateEntry(original.Name);
            entry.UseModelScale = original.UseModelScale; entry.UseModelRotate = original.UseModelRotate; entry.UseModelTranslate = original.UseModelTranslate;
            entry.ScaleCompensateApply = original.ScaleCompensateApply; entry.ScaleCompensateParent = original.ScaleCompensateParent; entry.ClassicScaleOff = original.ClassicScaleOff;
            for (int channel = 0; channel < 9; channel++)
            {
                while (entry.KeyArrays[channel].KeyCount > 0) entry.KeyArrays[channel].Remove(entry.KeyArrays[channel].Entries.First()._index);
                foreach (var key in original.KeyArrays[channel].Entries)
                    entry.Keyframes.SetFrameValue(channel, key._index, key._value, true)._tangent = key._tangent;
            }
            entry.Keyframes.FrameLimit = original.Keyframes.FrameLimit; entry.Keyframes.Loop = original.Keyframes.Loop; entry.Keyframes.PreserveInterpolation = true;
        }
        foreach (var name in frozen.Children.Select(c => c.Name)) { var entry = destination.Children.Single(c => c.Name == name); entry.Remove(); destination.AddChild(entry); }
        PreciseCHR0EntryNode.Promote(destination); destination.SignalPropertyChange();
    }

    static void ReplayRootsMatch(CHR0Node before, CHR0Node after, MDL0Node model, string[] roots)
    {
        int last = before.Loop ? before.FrameCount : before.FrameCount - 1;
        var times = new SortedSet<float>(Enumerable.Range(0, last * 4 + 1).Select(i => i / 4f));
        foreach (var entry in before.Children.OfType<CHR0EntryNode>().Where(t => roots.Contains(t.Name, StringComparer.Ordinal)))
        {
            var actual = after.Children.OfType<CHR0EntryNode>().SingleOrDefault(t => t.Name == entry.Name);
            if (actual == null)
            {
                // Independently prove the native bind fallback. Do not ask the
                // optimizer whether its own omission was safe: every original
                // authored tuple, paired side and model flag is checked here.
                bool constant = ReplayBindTrackExact(entry, model.AllBones.Single(b => b.Name == entry.Name));
                Check(constant, "An omitted source control independently proves finite exact bind constants, positive-zero tangents, no paired sides and authentic model flags: " + entry.Name);
            }
            else Check(entry.UseModelScale == actual.UseModelScale && entry.UseModelRotate == actual.UseModelRotate && entry.UseModelTranslate == actual.UseModelTranslate &&
                    entry.ScaleCompensateApply == actual.ScaleCompensateApply && entry.ScaleCompensateParent == actual.ScaleCompensateParent && entry.ClassicScaleOff == actual.ClassicScaleOff,
                    "Replay preserves native semantic flags on source-owned control " + entry.Name);
            foreach (var array in entry.KeyArrays)
            {
                var groups = array.Entries.GroupBy(k => k._index).OrderBy(g => g.Key).ToArray();
                foreach (var group in groups)
                {
                    times.Add(group.Key);
                    if (group.Count() > 1)
                        Check(ReplayKnotMatches(before, after, group.Key, new HashSet<string>(new[] { entry.Name }, StringComparer.Ordinal)), "Replay preserves both paired native control knot sides at " + entry.Name + " / " + group.Key);
                }
                for (int i = 1; i < groups.Length; i++)
                {
                    float first = groups[i - 1].Key, span = groups[i].Key - first;
                    times.Add(first + span * .193f); times.Add(first + span * .631f);
                }
            }
        }
        var bones = model.AllBones.Where(b => roots.Contains(b.Name, StringComparer.Ordinal)).ToArray();
        foreach (float at in times)
            Check(bones.All(b =>
            {
                var a = RecoverySample(before, b, at); var candidate = RecoverySample(after, b, at);
                return RecoverySamePose(a, candidate) && Enumerable.Range(3, 3).All(c => Math.Abs((double)a[c] - candidate[c]) <= LoopOrientationTolerance);
            }), "Replay retains source-owned native control functions and Euler winding at " + at.ToString("G8"));
    }

    static async Task WorkspaceOptimizationReplay(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Optimization project replay requires the private desktop.");
        int projectArg = Array.IndexOf(args, "--optimization-project");
        if (projectArg < 0 || projectArg + 1 >= args.Length) throw new InvalidDataException("Choose an accepted native project for optimization replay.");
        string path = Path.GetFullPath(args[projectArg + 1]);
        bool rawNext = args.Contains("--optimization-raw-next"), highQuality = args.Contains("--optimization-high-quality"), compactStorage = args.Contains("--optimization-compact-storage");
        var originalPaths = new[] { args[0], args[1], path }.Where(File.Exists)
            .Concat(Directory.Exists(path + ".assets") ? Directory.EnumerateFiles(path + ".assets", "*", SearchOption.AllDirectories) : new string[0])
            .Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(file => file, FighterProject.HashFile, StringComparer.OrdinalIgnoreCase);
        var json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024, RecursionLimit = 200 };
        using (var project = FighterProject.Open(path))
        using (var form = new ModelForm())
        {
            try
            {
                string storedSelection = project.SelectedAnimation;
                form.InitializeFighterProject(project); form.Show(); form.ClientSize = new Size(1700, 950);
                Call(form, "SetWorkspaceTextSize", 100);
                await GameLibraryWait(() => form.FighterEditor.TargetModel is MDL0Node && form.FighterEditor.SelectedCHR0 != null &&
                    (storedSelection == null || storedSelection.EndsWith("|@" + OptimizationNativeIdentity(form.FighterEditor.SelectedCHR0), StringComparison.Ordinal)),
                    "Accepted project did not finish restoring its selected native animation.", 60);
                await SimpleControlsReady(form);
                var editor = form.FighterEditor; var clip = editor.SelectedCHR0; var model = editor.TargetModel as MDL0Node;
                if (clip == null || model == null) throw new InvalidDataException("The accepted replay project must retain an exact selected native target and clip.");
                var asset = (FighterAsset)Call(form, "SelectedFighterAsset");
                var fit = (NativeTransitionFit)Call(form, "TransitionRecipeFor", asset, clip);
                string retargetMetadata = json.Serialize(asset.Retarget), fitMetadata = json.Serialize(new { asset.TransitionFit, asset.ClipTransitionFits });
                Check(asset.Retarget != null || fit != null, "Replay uses an accepted native retarget or fit revision with recorded provenance");
                if (rawNext)
                {
                    if (fit == null) throw new InvalidDataException("Raw Next replay needs the original source snapshot retained by an accepted fit recipe.");
                    fit.Validate(); var original = fit.Inputs.Single(input => input.Role == "source");
                    string frozenPath = Path.Combine(output, "accepted-original-next.chr0"); File.WriteAllBytes(frozenPath, Convert.FromBase64String(original.Snapshot));
                    Check(string.Equals(FighterProject.HashFile(frozenPath), original.Sha256, StringComparison.OrdinalIgnoreCase),
                        "Raw Next replay verifies the exact retained source snapshot SHA256 " + original.Sha256);
                    using (var raw = NodeFactory.FromFile(null, frozenPath) as CHR0Node)
                    {
                        if (raw == null) throw new InvalidDataException("The accepted original Next snapshot could not be decoded.");
                        var previous = clip.Children.OfType<CHR0EntryNode>().ToArray();
                        ReplayRestoreCurrent(clip, raw);
                        editor.RebindSavedCurveTargets(clip, previous.Where(entry => clip.Children.Any(c => c.Name == entry.Name)).ToArray());
                        editor.UpdateModel(); editor.RefreshControlCurveTarget(); editor.KeyframePanel?.UpdateKeyframes();
                        Call(form, "InvalidateControls"); Call(form, "MarkFighterChanged");
                        Check(SetNativeContent(clip) == SetNativeContent(raw), "Raw replay restores exact original Next curves, flags and metadata in the same owned native clip");
                    }
                    // Replay the earlier retarget-only workflow. Its later fit
                    // joins are not protections on this independent working
                    // clone; the accepted source project and assets stay intact.
                    Call(form, "StoreTransitionRecipe", asset, clip, (object)null);
                    fit = null;
                    fitMetadata = json.Serialize(new { asset.TransitionFit, asset.ClipTransitionFits });
                    Check(Call(form, "TransitionRecipeFor", asset, clip) == null && json.Serialize(asset.Retarget) == retargetMetadata,
                        "Raw Next replay clears only the working member's later fit receipt and retains original retarget provenance");
                    await SimpleControlsReady(form);
                }
                var setup = await Await<ControlReply>(Call(form, "LoadControls"));
                string[] roots = (setup.ProtectedMotionBones ?? new int[0]).Select(i => setup.BoneNames[i]).ToArray();
                Check(roots.Length > 0 && roots.All(name => model.AllBones.Any(b => b.Name == name)), "Replay retains exact reviewed native motion-control bindings");
                var targetNames = new HashSet<string>(model.AllBones.Select(b => b.Name), StringComparer.Ordinal);
                var siblings = Walk(clip.RootNode).OfType<CHR0Node>().Where(c => !ReferenceEquals(c, clip)).ToDictionary(c => c, SetNativeContent);
                var untouchedBytes = OptimizationUntouchedBytes(form, clip);
                File.WriteAllText(Path.Combine(output, "replay-untouched-before.json"), json.Serialize(untouchedBytes));
                string before = SetNativeContent(clip), beforeStorage = NativeAnimationOptimization.StorageKey(clip), nativeIdentity = OptimizationNativeIdentity(clip), modelContent = (string)Call(form, "NativeControlHash", model, ".mdl0");
                var rootsBefore = editor._openedFiles.Select(r => r.RootNode).Concat(new[] { model.RootNode }).Distinct().ToDictionary(root => root,
                    root => (string)Call(form, "NativeControlHash", root, string.IsNullOrEmpty(Path.GetExtension(root.FilePath)) ? ".bin" : Path.GetExtension(root.FilePath)));
                int undo = editor._undoSaves.Count, frame = editor.CurrentFrame, count = project.Animations.Count;
                var selectedBone = editor.SelectedBone; var witness = new GameLibraryDocument(form);
                string optimized = null, optimizedStorage = null; bool applied = false, finished = false; Exception failure = null;
                var mandatory = new HashSet<int>();
                if (fit != null)
                {
                    fit.Validate(); var settings = json.Deserialize<TransitionFitOptions>(fit.Options);
                    if (settings.Entry != null) { mandatory.Add(0); mandatory.Add(settings.EntrySamples - 1); }
                    if (settings.Exit != null) { mandatory.Add(clip.FrameCount - settings.ExitSamples); mandatory.Add(clip.FrameCount - 1); }
                }
                using (var baseline = NativeAnimationOptimization.Copy(clip))
                {
                    string beforePath = Path.Combine(output, "replay-baseline.chr0"); baseline.ExportChecked(beforePath); long beforeBytes = new FileInfo(beforePath).Length;
                    form.OptimizationReady = async window =>
                    {
                        try
                        {
                            var mode = SimpleControl<ComboBox>(window, "optimizationMode");
                            if (highQuality)
                            {
                                Check(mode.Items.Count > 1 && Convert.ToString(mode.Items[1]) == "High quality", "High-quality replay is enabled only by the qualified application increment");
                                mode.SelectedIndex = 1;
                            }
                            else Check(!window.ReadHighQuality(), "Accepted project replay opens in Exact mode");
                            if (compactStorage)
                            {
                                var compact = SimpleControl<CheckBox>(window, "optimizationCompactStorage");
                                Check(!compact.Checked && !window.ReadCompactStorage(), "Accepted replay requests smaller storage explicitly from its unchanged default");
                                compact.Checked = true;
                                Check(window.ReadCompactStorage(), "The actual smaller-storage checkbox selects compact native storage for accepted replay");
                            }
                            await window.Analyze();
                            Check(window.Candidate != null, "Accepted project produces an independently prepared optimization result: " + SimpleControl<Label>(window, "optimizationSummary").Text);
                            Check(window.Candidate.Children.All(entry => targetNames.Contains(entry.Name)), "Accepted replay candidate contains only bones in the actual target skeleton");
                            string candidatePath = Path.Combine(output, "replay-candidate.chr0"); window.Candidate.ExportChecked(candidatePath);
                            Check(new FileInfo(candidatePath).Length <= beforeBytes, compactStorage ? "Independent native encodings prove compact accepted replay does not grow the current CHR0" : "Independent precise native encodings prove accepted replay does not grow the current CHR0");
                            optimizedStorage = NativeAnimationOptimization.StorageKey(window.Candidate);
                            if (compactStorage)
                            {
                                using (var decoded = NodeFactory.FromFile(null, candidatePath) as CHR0Node)
                                    Check(decoded != null && SetNativeContent(decoded) == SetNativeContent(window.Candidate) && NativeAnimationOptimization.StorageKey(decoded) == optimizedStorage,
                                        "Independently reopening the compact replay candidate retains every native coefficient and storage plan");
                                File.WriteAllText(Path.Combine(output, "replay-native-storage.json"), json.Serialize(new { CompactStorage = true, BeforeBytes = beforeBytes,
                                    CandidateBytes = new FileInfo(candidatePath).Length, BeforeStorage = beforeStorage, CandidateStorage = optimizedStorage }));
                            }
                            foreach (int at in mandatory) Check(ReplayKnotMatches(baseline, window.Candidate, at, targetNames, model), "Accepted fit endpoint/join retains exact native values and tangents, including independently proven constants, at " + at);
                            ReplayRootsMatch(baseline, window.Candidate, model, roots);
                            if (!highQuality)
                            {
                                int last = baseline.Loop ? baseline.FrameCount : baseline.FrameCount - 1;
                                var times = Enumerable.Range(0, last * 4 + 1).Select(i => i / 4f)
                                    .Concat(Enumerable.Range(0, last).SelectMany(i => new[] { i + .193f, i + .631f })).Distinct().OrderBy(at => at);
                                foreach (float at in times)
                                    Check(model.AllBones.All(b => RecoverySamePose(RecoverySample(baseline, b, at), RecoverySample(window.Candidate, b, at))), "Exact accepted replay retains the native full-clip pose at " + at.ToString("G8"));
                            }
                            else OptimizationWithheldGeometry(window.SourceView, window.TargetView, baseline, window.Candidate, Path.Combine(output, "replay-hq-withheld.json"));
                            witness.CheckPreserved("accepted " + (rawNext ? "original Next" : "fitted") + " optimization preparation");
                            Check(rootsBefore.All(proof => (string)Call(form, "NativeControlHash", proof.Key, string.IsNullOrEmpty(Path.GetExtension(proof.Key.FilePath)) ? ".bin" : Path.GetExtension(proof.Key.FilePath)) == proof.Value), "Preparing accepted replay preserves every open native source root");
                            optimized = SetNativeContent(window.Candidate);
                            applied = window.PreparedCanApply;
                            Check(applied ? (optimized != before || compactStorage && optimizedStorage != beforeStorage) && SimpleControl<Button>(window, "optimizeApply").Enabled : optimized == before && (!compactStorage || optimizedStorage == beforeStorage) && !SimpleControl<Button>(window, "optimizeApply").Enabled,
                                "Replay reports a useful applicable change or an honest unchanged no-op");
                            SimpleControl<TrackBar>(window, "optimizationFrame").Value = Math.Max(1, baseline.FrameCount / 2);
                            SimpleControl<Button>(window, "optimizationDetails").PerformClick();
                            await CaptureOptimization(window, Path.Combine(output, "replay-original-result-details.png"));
                            File.WriteAllText(Path.Combine(output, "replay-optimization-report.txt"), SimpleControl<TextBox>(window, "optimizationReport").Text);
                            if (applied) SimpleControl<Button>(window, "optimizeApply").PerformClick();
                            else window.Close();
                        }
                        catch (Exception error) { failure = error; window.Close(); }
                        finally { finished = true; }
                    };
                    Call(form, "ShowAnimationOptimization");
                    await GameLibraryWait(() => finished, "Accepted optimization replay did not complete.", 150);
                    form.OptimizationReady = null;
                    if (failure != null) throw failure;
                    await SimpleControlsReady(form);
                }
                Check(ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.TargetModel, model) && ReferenceEquals(editor.SelectedBone, selectedBone) && editor.CurrentFrame == frame && project.Animations.Count == count && SetNativeContent(clip) == optimized,
                    "Replay Apply/no-op retains the exact native target, clip, bone, frame and project asset count");
                if (compactStorage) Check(NativeAnimationOptimization.StorageKey(clip) == optimizedStorage, "Accepted replay Apply/no-op retains the independently verified compact storage");
                Check(siblings.All(proof => SetNativeContent(proof.Key) == proof.Value) && (string)Call(form, "NativeControlHash", model, ".mdl0") == modelContent,
                    "Accepted replay preserves every sibling animation and the complete native target model");
                Check(rootsBefore.Where(proof => !ReferenceEquals(proof.Key, clip.RootNode)).All(proof => (string)Call(form, "NativeControlHash", proof.Key, string.IsNullOrEmpty(Path.GetExtension(proof.Key.FilePath)) ? ".bin" : Path.GetExtension(proof.Key.FilePath)) == proof.Value), "Applying accepted replay preserves unrelated native source roots");
                Check(json.Serialize(asset.Retarget) == retargetMetadata && json.Serialize(new { asset.TransitionFit, asset.ClipTransitionFits }) == fitMetadata, "Optimization retains the complete original Next provenance and frozen fitting receipts unchanged");
                Check(editor._undoSaves.Count == undo + (applied ? 1 : 0), "Accepted replay creates exactly one native command for a change and none for a no-op");
                if (applied)
                {
                    editor.Undo(); await SimpleControlsReady(form);
                    Check(SetNativeContent(clip) == before && siblings.All(proof => SetNativeContent(proof.Key) == proof.Value), "Accepted replay Undo restores the precise pre-optimization animation and preserves siblings");
                    if (compactStorage) Check(NativeAnimationOptimization.StorageKey(clip) == beforeStorage, "Accepted replay Undo restores the complete original native storage");
                    editor.Redo(); await SimpleControlsReady(form);
                    Check(SetNativeContent(clip) == optimized && json.Serialize(asset.Retarget) == retargetMetadata && json.Serialize(new { asset.TransitionFit, asset.ClipTransitionFits }) == fitMetadata, "Accepted replay Redo restores optimized native curves without changing retarget or fit provenance");
                    if (compactStorage) Check(NativeAnimationOptimization.StorageKey(clip) == optimizedStorage, "Accepted replay Redo restores the exact reviewed compact native storage");
                    Check(OptimizationRetainedBaseline(form) == before, "Accepted replay Apply and native Undo/Redo retain the original session baseline content");
                }
                editor.FrameFighterModel(); await CaptureSimpleWorkspace(form, Path.Combine(output, "replay-native-applied.png"));
                string export = Path.Combine(output, "replay-optimized.chr0"); form.ExportSimpleWorkspaceResult(export);
                using (var decoded = NodeFactory.FromFile(null, export) as CHR0Node)
                {
                    Check(decoded != null && SetNativeContent(decoded) == optimized, "Export before Save independently reopens the accepted replay result exactly");
                    if (compactStorage) Check(NativeAnimationOptimization.StorageKey(decoded) == optimizedStorage, "Export before Save independently retains accepted replay compact storage");
                }
                if (applied)
                {
                    bool repeatDone = false; Exception repeatFailure = null;
                    form.OptimizationReady = async window => {
                        try {
                            Check(OptimizationRetainedBaseline(form) == before, "Ordinary replay export preserves the original native quality baseline including its complete source keys");
                            if (compactStorage) SimpleControl<CheckBox>(window, "optimizationCompactStorage").Checked = true;
                            if (highQuality)
                            {
                                await window.Analyze();
                                Check(window.Candidate != null && SetNativeContent(clip) == optimized && OptimizationRetainedBaseline(form) == before,
                                    "Exact preview after HQ export leaves the current result and original approximation baseline unchanged");
                                SimpleControl<ComboBox>(window, "optimizationMode").SelectedIndex = 1;
                                Check(window.Candidate == null && !window.PreparedCanApply, "Switching the post-export Exact preview back to HQ requires explicit preparation");
                            }
                            int prepares = form.OptimizationPrepares;
                            await window.Analyze();
                            Check(window.Candidate != null && !window.PreparedCanApply && form.OptimizationPrepares == prepares && SetNativeContent(window.Candidate) == optimized && OptimizationRetainedBaseline(form) == before,
                                "Repeating unchanged optimization after ordinary export retains its original session baseline without another approximation or engine pass");
                            if (compactStorage) Check(window.ReadCompactStorage() && NativeAnimationOptimization.StorageKey(window.Candidate) == optimizedStorage,
                                "Repeated accepted replay retains the exact compact storage without cumulative requantization");
                        } catch (Exception error) { repeatFailure = error; }
                        finally { window.Close(); repeatDone = true; }
                    };
                    Call(form, "ShowAnimationOptimization"); await GameLibraryWait(() => repeatDone, "Repeated optimization did not finish.", 60);
                    form.OptimizationReady = null; if (repeatFailure != null) throw repeatFailure;
                }
                string exportedRetargetMetadata = json.Serialize(asset.Retarget);
                string saved = Path.Combine(output, "replay-optimized.fpstudio"); form.SaveFighterProject(saved);
                OptimizationCheckUntouchedBytes(form, clip, untouchedBytes, "accepted replay project Save", Path.Combine(output, "replay-untouched-after-save.json"));
                using (var reopened = FighterProject.Open(saved))
                using (var freshForm = new ModelForm())
                {
                    freshForm.InitializeFighterProject(reopened); freshForm.Show();
                    await GameLibraryWait(() => freshForm.FighterEditor.TargetModel is MDL0Node && freshForm.FighterEditor.SelectedCHR0 != null &&
                        OptimizationNativeIdentity(freshForm.FighterEditor.SelectedCHR0) == nativeIdentity,
                        "Fresh replay did not finish restoring its exact native member.", 60);
                    await SimpleControlsReady(freshForm);
                    var restored = freshForm.FighterEditor.SelectedCHR0; var restoredAsset = (FighterAsset)Call(freshForm, "SelectedFighterAsset");
                    Check(restored != null && OptimizationNativeIdentity(restored) == nativeIdentity && SetNativeContent(restored) == optimized, "Save/fresh reopen retains the accepted replay's exact current native member and curves");
                    if (compactStorage) Check(NativeAnimationOptimization.StorageKey(restored) == optimizedStorage, "Save/fresh reopen retains accepted replay compact storage");
                    OptimizationCheckUntouchedBytes(freshForm, restored, untouchedBytes, "accepted replay fresh reopen", Path.Combine(output, "replay-untouched-after-reopen.json"));
                    Check((string)Call(freshForm, "NativeControlHash", (MDL0Node)freshForm.FighterEditor.TargetModel, ".mdl0") == modelContent, "Fresh replay project retains the complete native target model and binds");
                    Check(json.Serialize(restoredAsset.Retarget) == exportedRetargetMetadata && json.Serialize(new { restoredAsset.TransitionFit, restoredAsset.ClipTransitionFits }) == fitMetadata, "Fresh replay project preserves original retarget provenance, ordinary export evidence and frozen fit recipes");
                    foreach (var proof in siblings)
                    {
                        string sibling = OptimizationNativeIdentity(proof.Key);
                        Check(SetNativeContent(Walk(restored.RootNode).OfType<CHR0Node>().Single(c => OptimizationNativeIdentity(c) == sibling)) == proof.Value, "Fresh replay project preserves sibling " + sibling);
                    }
                    SimpleDisposeForm(freshForm);
                }
                Check(originalPaths.All(proof => FighterProject.HashFile(proof.Key) == proof.Value), "Replay optimization, Undo, export and Save preserve the accepted input project, saved assets and original game inputs byte-for-byte");
            }
            finally
            {
                form.OptimizationReady = null;
                if (!form.IsDisposed) { Call(form, "SetWorkspaceTextSize", 100); SimpleDisposeForm(form); }
            }
        }
    }
}
