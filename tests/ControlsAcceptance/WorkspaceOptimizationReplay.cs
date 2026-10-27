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

    static bool ReplayKnotMatches(CHR0Node before, CHR0Node after, int frame, ISet<string> targetNames)
    {
        foreach (var entry in before.Children.OfType<CHR0EntryNode>().Where(t => targetNames.Contains(t.Name)))
        {
            var candidate = after.Children.OfType<CHR0EntryNode>().SingleOrDefault(t => t.Name == entry.Name);
            if (candidate == null) return false;
            for (int channel = 0; channel < 9; channel++)
            {
                var original = entry.KeyArrays[channel].Entries.Where(k => k._index == frame).ToArray();
                if (original.Length == 0) continue;
                var actual = candidate.KeyArrays[channel].Entries.Where(k => k._index == frame).ToArray();
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
            var actual = after.Children.OfType<CHR0EntryNode>().Single(t => t.Name == entry.Name);
            Check(entry.UseModelScale == actual.UseModelScale && entry.UseModelRotate == actual.UseModelRotate && entry.UseModelTranslate == actual.UseModelTranslate &&
                entry.ScaleCompensateApply == actual.ScaleCompensateApply && entry.ScaleCompensateParent == actual.ScaleCompensateParent && entry.ClassicScaleOff == actual.ClassicScaleOff,
                "Replay preserves native semantic flags on source-owned control " + entry.Name);
            foreach (var array in entry.KeyArrays)
            {
                var groups = array.Entries.GroupBy(k => k._index).OrderBy(g => g.Key).ToArray();
                foreach (var group in groups)
                {
                    times.Add(group.Key);
                    if (group.Count() > 1)
                        Check(ReplayKnotMatches(before, after, group.Key, new HashSet<string>(roots, StringComparer.Ordinal)), "Replay preserves both paired native control knot sides at " + group.Key);
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
        bool rawNext = args.Contains("--optimization-raw-next"), highQuality = args.Contains("--optimization-high-quality");
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
                string before = SetNativeContent(clip), nativeIdentity = OptimizationNativeIdentity(clip), modelContent = (string)Call(form, "NativeControlHash", model, ".mdl0");
                var rootsBefore = editor._openedFiles.Select(r => r.RootNode).Concat(new[] { model.RootNode }).Distinct().ToDictionary(root => root,
                    root => (string)Call(form, "NativeControlHash", root, string.IsNullOrEmpty(Path.GetExtension(root.FilePath)) ? ".bin" : Path.GetExtension(root.FilePath)));
                int undo = editor._undoSaves.Count, frame = editor.CurrentFrame, count = project.Animations.Count;
                var selectedBone = editor.SelectedBone; var witness = new GameLibraryDocument(form);
                string optimized = null; bool applied = false, finished = false; Exception failure = null;
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
                            await window.Analyze();
                            Check(window.Candidate != null, "Accepted project produces an independently prepared optimization result: " + SimpleControl<Label>(window, "optimizationSummary").Text);
                            Check(window.Candidate.Children.All(entry => targetNames.Contains(entry.Name)), "Accepted replay candidate contains only bones in the actual target skeleton");
                            string candidatePath = Path.Combine(output, "replay-candidate.chr0"); window.Candidate.ExportChecked(candidatePath);
                            Check(new FileInfo(candidatePath).Length <= beforeBytes, "Independent precise native encodings prove accepted replay does not grow the current CHR0");
                            foreach (int at in mandatory) Check(ReplayKnotMatches(baseline, window.Candidate, at, targetNames), "Accepted fit endpoint/join retains all existing native key and tangent bits at " + at);
                            ReplayRootsMatch(baseline, window.Candidate, model, roots);
                            if (!highQuality)
                            {
                                int last = baseline.Loop ? baseline.FrameCount : baseline.FrameCount - 1;
                                var times = Enumerable.Range(0, last * 4 + 1).Select(i => i / 4f)
                                    .Concat(Enumerable.Range(0, last).SelectMany(i => new[] { i + .193f, i + .631f })).Distinct().OrderBy(at => at);
                                foreach (float at in times)
                                    Check(model.AllBones.All(b => RecoverySamePose(RecoverySample(baseline, b, at), RecoverySample(window.Candidate, b, at))), "Exact accepted replay retains the native full-clip pose at " + at.ToString("G8"));
                            }
                            witness.CheckPreserved("accepted " + (rawNext ? "original Next" : "fitted") + " optimization preparation");
                            Check(rootsBefore.All(proof => (string)Call(form, "NativeControlHash", proof.Key, string.IsNullOrEmpty(Path.GetExtension(proof.Key.FilePath)) ? ".bin" : Path.GetExtension(proof.Key.FilePath)) == proof.Value), "Preparing accepted replay preserves every open native source root");
                            optimized = SetNativeContent(window.Candidate);
                            applied = window.PreparedCanApply;
                            Check(applied ? optimized != before && SimpleControl<Button>(window, "optimizeApply").Enabled : optimized == before && !SimpleControl<Button>(window, "optimizeApply").Enabled,
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
                Check(siblings.All(proof => SetNativeContent(proof.Key) == proof.Value) && (string)Call(form, "NativeControlHash", model, ".mdl0") == modelContent,
                    "Accepted replay preserves every sibling animation and the complete native target model");
                Check(rootsBefore.Where(proof => !ReferenceEquals(proof.Key, clip.RootNode)).All(proof => (string)Call(form, "NativeControlHash", proof.Key, string.IsNullOrEmpty(Path.GetExtension(proof.Key.FilePath)) ? ".bin" : Path.GetExtension(proof.Key.FilePath)) == proof.Value), "Applying accepted replay preserves unrelated native source roots");
                Check(json.Serialize(asset.Retarget) == retargetMetadata && json.Serialize(new { asset.TransitionFit, asset.ClipTransitionFits }) == fitMetadata, "Optimization retains the complete original Next provenance and frozen fitting receipts unchanged");
                Check(editor._undoSaves.Count == undo + (applied ? 1 : 0), "Accepted replay creates exactly one native command for a change and none for a no-op");
                if (applied)
                {
                    editor.Undo(); await SimpleControlsReady(form);
                    Check(SetNativeContent(clip) == before && siblings.All(proof => SetNativeContent(proof.Key) == proof.Value), "Accepted replay Undo restores the precise pre-optimization animation and preserves siblings");
                    editor.Redo(); await SimpleControlsReady(form);
                    Check(SetNativeContent(clip) == optimized && json.Serialize(asset.Retarget) == retargetMetadata && json.Serialize(new { asset.TransitionFit, asset.ClipTransitionFits }) == fitMetadata, "Accepted replay Redo restores optimized native curves without changing retarget or fit provenance");
                }
                editor.FrameFighterModel(); await CaptureSimpleWorkspace(form, Path.Combine(output, "replay-native-applied.png"));
                string export = Path.Combine(output, "replay-optimized.chr0"); form.ExportSimpleWorkspaceResult(export);
                using (var decoded = NodeFactory.FromFile(null, export) as CHR0Node)
                    Check(decoded != null && SetNativeContent(decoded) == optimized, "Export before Save independently reopens the accepted replay result exactly");
                if (applied)
                {
                    int prepares = form.OptimizationPrepares; bool repeatDone = false; Exception repeatFailure = null;
                    form.OptimizationReady = async window => {
                        try {
                            if (highQuality) SimpleControl<ComboBox>(window, "optimizationMode").SelectedIndex = 1;
                            await window.Analyze();
                            Check(window.Candidate != null && !window.PreparedCanApply && form.OptimizationPrepares == prepares && SetNativeContent(window.Candidate) == optimized,
                                "Repeating unchanged optimization after ordinary export retains its original session baseline without another approximation or engine pass");
                        } catch (Exception error) { repeatFailure = error; }
                        finally { window.Close(); repeatDone = true; }
                    };
                    Call(form, "ShowAnimationOptimization"); await GameLibraryWait(() => repeatDone, "Repeated optimization did not finish.", 60);
                    form.OptimizationReady = null; if (repeatFailure != null) throw repeatFailure;
                }
                string exportedRetargetMetadata = json.Serialize(asset.Retarget);
                string saved = Path.Combine(output, "replay-optimized.fpstudio"); form.SaveFighterProject(saved);
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
