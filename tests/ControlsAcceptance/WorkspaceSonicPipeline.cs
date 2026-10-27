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
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    static object SonicStage(CHR0Node clip, string file, string label)
    {
        using (var decoded = NodeFactory.FromFile(null, file) as CHR0Node)
        {
            if (decoded == null) throw new InvalidDataException("The Sonic pipeline stage did not decode: " + label);
            Check(NativeAnimationOptimization.SourceContentKey(decoded) == NativeAnimationOptimization.SourceContentKey(clip), label + ": independently decoded export preserves exact complete native content and source scaling metadata");
            var codeField = typeof(CHR0EntryNode).GetField("_code", BindingFlags.NonPublic | BindingFlags.Instance);
            var groups = decoded.Children.OfType<CHR0EntryNode>().SelectMany(entry => Enumerable.Range(0, 3)
                .Where(g => entry.KeyArrays.Skip(g * 3).Take(3).Any(c => c.KeyCount > 1))
                .Select(g => new { Bone = entry.Name, Group = g, Format = ((AnimationCode)codeField.GetValue(entry)).GetFormat(g).ToString() })).ToArray();
            long preciseBytes;
            using (var precise = NativeAnimationOptimization.CopySource(clip))
            {
                foreach (CHR0EntryNode entry in precise.Children.ToArray()) PreciseCHR0EntryNode.RestoreStorage(entry, null);
                string temporary = Path.ChangeExtension(file, ".i12.chr0"); precise.ExportChecked(temporary);
                preciseBytes = new FileInfo(temporary).Length;
            }
            return new { Stage = label, File = file, Bytes = new FileInfo(file).Length, PreciseI12Bytes = preciseBytes,
                ResourceBytes = decoded.WorkingUncompressed.Length, Keys = OptimizationKeyCount(decoded), Tracks = decoded.Children.Count,
                decoded.FrameCount, decoded.Loop, Content = NativeAnimationOptimization.SourceContentKey(decoded), Sha256 = FighterProject.HashFile(file),
                Formats = groups.GroupBy(g => g.Format).ToDictionary(g => g.Key, g => g.Count()), Groups = groups };
        }
    }

    static void SonicReviewNativeMapping(ModelForm form, MDL0Node model, string expectedFingerprint, string output, string label)
    {
        // These immutable authentic fixtures were already used in the accepted
        // rotation-path replay. Exercise the actual native review rather than
        // leaving a characterization-warning modal awaiting physical input.
        var captured = BrawlRetargeter.Capture(model);
        Check(captured.Fingerprint == expectedFingerprint, label + ": native binding review uses the sealed authentic rig revision");
        var expected = captured.Rig.Roles.ToDictionary(pair => pair.Key, pair => captured.Bones[pair.Value].Name);
        using (var modal = Modal("Skeleton Mapping", window =>
        {
            var grid = SimpleControl<DataGridView>(window, "skeletonMapping"); grid.EndEdit();
            var shown = grid.Rows.Cast<DataGridViewRow>().Where(row => !string.IsNullOrWhiteSpace(Convert.ToString(row.Cells[1].Value)))
                .ToDictionary(row => Convert.ToString(row.Cells[0].Value), row => Convert.ToString(row.Cells[1].Value));
            Check(shown.Count == expected.Count && expected.All(pair => shown.TryGetValue(pair.Key, out string bone) && bone == pair.Value),
                label + ": actual native mapping dialog preserves the frozen fixture's complete structural role bindings");
            CaptureDialog(window, output, label + "-native-mapping.png");
            SimpleControl<Button>(window, "applySkeletonMap").PerformClick();
        })) Call(form, "CaptureFighterRig", model, true);
    }

    static async Task WorkspaceSonicPipeline(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Sonic fitting/size qualification requires a private desktop.");
        string Arg(string key) => args[Array.IndexOf(args, key) + 1];
        bool reciprocal = args.Contains("--sonic-pipeline-reciprocal"), fitOnly = args.Contains("--sonic-pipeline-fit-only");
        string sourceCharacter = reciprocal ? "mario" : "sonic", sourceModelName = reciprocal ? "FitMario00" : "FitSonic00";
        string sourceLabel = reciprocal ? "Mario" : "Sonic", targetLabel = reciprocal ? "Sonic" : "Mario";
        int expectedFrames = reciprocal ? 35 : 44, exitStart = expectedFrames - 9;
        string sourceFingerprint = reciprocal ? "87b3134758d85871c2f5fadb1bd84332fcac9f252df7da82738a024cad68f0e9" : "c3813c2db05d2fee9eb43222432dd6ac62395ffd2d02ade940bf6b81499444aa";
        string targetFingerprint = reciprocal ? "c3813c2db05d2fee9eb43222432dd6ac62395ffd2d02ade940bf6b81499444aa" : "87b3134758d85871c2f5fadb1bd84332fcac9f252df7da82738a024cad68f0e9";
        var json = new JavaScriptSerializer { MaxJsonLength = 256 * 1024 * 1024, RecursionLimit = 200 };
        string sourceFile = Path.GetFullPath(args[1]), providedMario = Path.GetFullPath(Arg("--provided-mario")), targetMotion = Path.GetFullPath(Arg("--target-motion"));
        string libraryRoot = Path.Combine(output, "isolated-library"); Directory.CreateDirectory(libraryRoot);
        File.Copy(Arg("--library-index"), Path.Combine(libraryRoot, "catalogue.xml"));
        string previousLibrary = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", libraryRoot);
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_TEMP_ROOT", Path.Combine(output, "transient-library"));
        var originals = new[] { args[0], sourceFile, providedMario, targetMotion, Arg("--library-index") }.Distinct().ToDictionary(p => p, FighterProject.HashFile);
        string sourceModelPath = Path.Combine(output, "source-model.mdl0"), sourceContainer = Path.Combine(output, "source-root.pac"), targetContainer = Path.Combine(output, "target-root.pac");
        File.Copy(args[0], targetContainer); File.Copy(sourceFile, Path.Combine(output, "source-original-native.chr0")); File.Copy(providedMario, Path.Combine(output, "provided-mario-original.chr0"));
        var library = new AnimationLibrary(libraryRoot);
        var sourceItem = library.Browse(new LibraryQuery { Character = sourceCharacter, Text = "AttackAirLw", Channel = "CHR0 skeletal" })
            .Single(i => i.Clip.Name == "AttackAirLw" && i.Model.Name == sourceModelName);
        using (var lease = library.Open(sourceItem.Clip.Id))
        {
            lease.Model.ExportChecked(sourceModelPath); lease.Model.RootNode.ExportChecked(sourceContainer);
        }
        using (var original = NodeFactory.FromFile(null, sourceFile) as CHR0Node)
        {
            Check(original != null && original.FrameCount == expectedFrames, "Exact " + sourceLabel + " AttackAirLw retains its authentic " + expectedFrames + "-frame timing without substituting the catalogue animation");
            File.WriteAllText(Path.Combine(output, "provided-source-native.json"), json.Serialize(new { File = sourceFile, Bytes = new FileInfo(sourceFile).Length,
                Sha256 = FighterProject.HashFile(sourceFile), original.Name, original.Version, original.FrameCount, original.Loop,
                Keys = OptimizationKeyCount(original), Tracks = original.Children.Count }));
            File.WriteAllText(Path.Combine(output, "source-sizes.json"), json.Serialize(SonicStage(original, Path.Combine(output, "source-original-native.chr0"), "source-original")));
            File.Copy(Path.Combine(output, "source-original-native.i12.chr0"), Path.Combine(output, "source-original-precise.chr0"));
        }
        using (var originalMario = NodeFactory.FromFile(null, providedMario) as CHR0Node)
        {
            Check(originalMario != null, "The exact provided Mario baseline independently decodes before optimization");
            File.WriteAllText(Path.Combine(output, "provided-mario-sizes.json"), json.Serialize(SonicStage(originalMario, Path.Combine(output, "provided-mario-original.chr0"), "provided-mario-stored-original")));
        }
        var allModes = new List<object>();
        var attackTravelControls = new HashSet<string>(StringComparer.Ordinal);
        void WriteManifest() => File.WriteAllText(Path.Combine(output, "stages-manifest.json"), json.Serialize(new {
            SourceAnimation = Path.Combine(output, "source-original-native.chr0"), SourceModel = sourceModelPath, SourceContainer = sourceContainer,
            TargetModel = Path.Combine(output, "target-model.mdl0"), TargetContainer = targetContainer, Fall = Path.Combine(output, "Fall.chr0"),
            ProvidedMario = Path.Combine(output, "provided-mario-original.chr0"), SampleCount = expectedFrames, EntryJoinDisplay = 5, ExitJoinDisplay = exitStart,
            FallSampleDisplay = 1, AttackTravelControls = attackTravelControls.ToArray(), PrivateDesktop, OSInputInjected = false, ScreenCopied = false, Modes = allModes }));
        WriteManifest();
        try
        {
            foreach (var keyMode in new[] { RetargetKeyframeMode.SourceBoneTimes, RetargetKeyframeMode.SourceChannelTimes })
            {
                string modeDirectory = Path.Combine(output, keyMode.ToString()); Directory.CreateDirectory(modeDirectory);
                using (var project = FighterProject.Create(sourceLabel + " AttackAirLw to " + targetLabel + " — " + keyMode))
                using (var form = new ModelForm())
                {
                    try
                    {
                        form.InitializeFighterProject(project); form.Show(); form.ClientSize = new Size(1700, 950); Call(form, "SetWorkspaceTextSize", 100);
                        Call(form, "ImportFighterModel", args[0]); Call(form, "ImportFighterAnimations", new[] { new[] { targetMotion, sourceFile } });
                        await SimpleControlsReady(form);
                        Console.WriteLine("TRACE " + keyMode + " imports and native control setup settled");
                        var editor = form.FighterEditor; var model = (MDL0Node)editor.TargetModel;
                        var inputAsset = project.Animations.Last(a => string.Equals(Path.GetFullPath(a.SourcePath), sourceFile, StringComparison.OrdinalIgnoreCase));
                        var source = Walk((ResourceNode)Call(form, "FighterRoot", inputAsset)).OfType<CHR0Node>().Single();
                        var fall = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c => c.Name == "Fall");
                        if (!File.Exists(Path.Combine(output, "Fall.chr0")))
                        {
                            // A frozen, read-only source reference retains native
                            // metadata (including its scaling rule); it is not an
                            // optimization candidate subject to writer qualification.
                            using (var fallCopy = NativeAnimationOptimization.CopySource(fall)) fallCopy.ExportChecked(Path.Combine(output, "Fall.chr0"));
                            model.ExportChecked(Path.Combine(output, "target-model.mdl0"));
                        }
                        foreach (ListViewItem row in editor.leftPanel.listAnims.Items) row.Selected = ReferenceEquals(row.Tag, source);
                        editor.SelectedCHR0 = source; await SimpleControlsReady(form); await Await<ControlReply>(Call(form, "LoadControls"));
                        Console.WriteLine("TRACE " + keyMode + " exact provided source selected and controls loaded");
                        SonicReviewNativeMapping(form, model, targetFingerprint, modeDirectory, "target");
                        using (var donorRoot = NodeFactory.FromFile(null, sourceModelPath))
                            SonicReviewNativeMapping(form, Walk(donorRoot).OfType<MDL0Node>().Single(), sourceFingerprint, modeDirectory, "source");
                        Console.WriteLine("TRACE " + keyMode + " native source/target mappings reviewed");
                        using (var modal = Modal("Retarget Options", window =>
                        {
                            SimpleControl<ComboBox>(window, "retargetKeyframeMode").SelectedIndex = (int)keyMode;
                            if (reciprocal) SimpleControl<ComboBox>(window, "retargetGroundMode").SelectedIndex = (int)GroundContactMode.Airborne;
                            SimpleControl<Button>(window, "retargetOptionsApply").PerformClick();
                        })) Call(form, "ShowFighterRetargetOptions");
                        Console.WriteLine("TRACE " + keyMode + " retarget options applied");
                        bool reviewed = false;
                        form.RetargetKeyframeReviewReady = raw =>
                        {
                            var window = (RetargetKeyframeReviewDialog)raw;
                            File.WriteAllText(Path.Combine(modeDirectory, "retarget-motion-differences.txt"), SimpleControl<TextBox>(window, "sourceKeysReport").Text);
                            ((CHR0Node)Field(window, "dense")).ExportChecked(Path.Combine(modeDirectory, "dense-next-reference.chr0"));
                            SimpleControl<CheckBox>(window, "sourceKeysReviewed").Checked = true; reviewed = true;
                            SimpleControl<Button>(window, "sourceKeysUse").PerformClick();
                        };
                        Console.WriteLine("TRACE " + keyMode + " retarget preparation started");
                        var resultAsset = form.RetargetFighterAnimation(sourceModelPath); form.RetargetKeyframeReviewReady = null;
                        Console.WriteLine("TRACE " + keyMode + " retarget preparation returned");
                        Check(reviewed && resultAsset != null && resultAsset.Retarget.KeyframeMode == keyMode,
                            keyMode + ": actual retarget progress/review imports the exact provided source animation as an independent revision");
                        var quality = resultAsset.Retarget.ReadKeyframeReport();
                        File.WriteAllText(Path.Combine(modeDirectory, "retarget-quality.json"), json.Serialize(quality));
                        Check(quality.Version == "source-key-quality-v2" && quality.ScalarCertified && quality.QualityVerified &&
                            quality.MaximumOrientationError <= .01 && quality.MaximumEulerError <= .01 &&
                            quality.MaximumWorldBoneError <= .0001 * quality.LengthScale && quality.MaximumSkinError <= .0001 * quality.LengthScale,
                            keyMode + ": guarded source seeds independently pass frozen scalar and cumulative native quality ceilings");
                        await SimpleControlsReady(form);
                        var clip = editor.SelectedCHR0;
                        Check(clip.FrameCount == expectedFrames && !clip.Loop, keyMode + ": source-key retargeting preserves authentic " + expectedFrames + "-frame native timing");
                        var setup = await Await<ControlReply>(Call(form, "LoadControls"));
                        var rootNames = (setup.ProtectedMotionBones ?? new int[0]).Select(i => setup.BoneNames[i]).ToArray();
                        var positionNames = setup.Roles.Where(r => r.Reviewed && r.Role == "RootMotion").Select(r => setup.BoneNames[r.Bone.Value]).ToArray();
                        attackTravelControls.UnionWith(positionNames); WriteManifest();
                        Check(positionNames.Length > 0, keyMode + ": exact native root-motion position binding is reviewed");
                        var stages = new List<object>();
                        object Export(string name)
                        {
                            string file = Path.Combine(modeDirectory, name + ".chr0"); form.ExportSimpleWorkspaceResult(file);
                            var stage = SonicStage(clip, file, name); stages.Add(stage); string saved = Path.Combine(modeDirectory, name + ".fpstudio"); form.SaveFighterProject(saved);
                            using (var reopened = FighterProject.Open(saved)) using (var savedRoot = NodeFactory.FromFile(null, reopened.Animations.Single(a => a.Id == resultAsset.Id).WorkingPath))
                                Check(NativeAnimationOptimization.ContentKey(Walk(savedRoot).OfType<CHR0Node>().Single()) == NativeAnimationOptimization.ContentKey(clip), keyMode + ": " + name + " project Save/reopen preserves exact current owned animation");
                            File.WriteAllText(Path.Combine(modeDirectory, "stage-sizes.json"), json.Serialize(stages)); return stage;
                        }
                        Export("raw-retarget");
                        using (var attack = NativeAnimationOptimization.Copy(clip))
                        {
                            async Task Fit(bool scoped, bool apply)
                            {
                                Exception failure = null; bool finished = false;
                                form.TransitionFitReady = async window =>
                                {
                                    try
                                    {
                                        foreach (string side in new[] { "entry", "exit" })
                                        {
                                            SimpleControl<CheckBox>(window, "transition_" + side + "Enabled").Checked = true;
                                            var choices = SimpleControl<ComboBox>(window, "transition_" + side);
                                            choices.SelectedItem = choices.Items.OfType<TransitionClipChoice>().Single(c => ReferenceEquals(c.Clip, fall));
                                            SimpleControl<CheckBox>(window, "transition_" + side + "Frozen").Checked = true;
                                            SimpleControl<NumericUpDown>(window, "transition_" + side + "Frame").Value = 1;
                                        }
                                        SimpleControl<NumericUpDown>(window, "transition_entrySamples").Value = 5;
                                        SimpleControl<NumericUpDown>(window, "transition_exitStart").Value = exitStart;
                                        SimpleControl<CheckBox>(window, "transition_existingTracksOnly").Checked = scoped;
                                        SimpleControl<CheckBox>(window, "transition_binding").Checked = true;
                                        await (Task)Call(window, "PrepareFit");
                                        File.WriteAllText(Path.Combine(modeDirectory, scoped ? "fit-options.json" : "full-fit-control-options.json"), json.Serialize(window.ReadOptions()));
                                        File.WriteAllText(Path.Combine(modeDirectory, scoped ? "fit-report.txt" : "full-fit-control-report.txt"), SimpleControl<TextBox>(window, "transitionReport").Text);
                                        Check(window.Candidate != null, keyMode + ": " + (scoped ? "existing-track" : "full") + " fitting prepares both frozen first-Fall references: " + SimpleControl<TextBox>(window, "transitionReport").Text);
                                        var fitted = window.Candidate;
                                        Check(RecoveryKeys(fitted, positionNames) == RecoveryKeys(attack, positionNames), keyMode + ": fitting never replaces the attack's own TransN/root-motion curves with Fall");
                                        Check(RecoveryRetainedKeysExact(attack, fitted, 4, exitStart - 1), keyMode + ": both native joins and unchanged attack middle retain exact authored keys/tangents");
                                        var fittedBones = new HashSet<string>(window.ReadOptions().Bones, StringComparer.Ordinal);
                                        foreach (int frame in new[] { 0, expectedFrames - 1 }) Check(model.AllBones.Where(b => fittedBones.Contains(b.Name) && !positionNames.Contains(b.Name))
                                            .All(b => RecoverySamePose(RecoverySample(fitted, b, frame), RecoverySample(fall, b, 0))), keyMode + ": selected body at frame " + (frame + 1) + " reaches only " + targetLabel + " Fall first pose");
                                        string export = Path.Combine(modeDirectory, scoped ? "fitted-candidate.chr0" : "full-fit-control.chr0"); fitted.ExportChecked(export);
                                        stages.Add(SonicStage(fitted, export, scoped ? "fitted-candidate" : "full-fit-control"));
                                        if (apply) SimpleControl<Button>(window, "applyTransition").PerformClick(); else window.Close();
                                    }
                                    catch (Exception error) { failure = error; window.Close(); }
                                    finally { finished = true; }
                                };
                                Call(form, "ShowTransitionFit"); await GameLibraryWait(() => finished, "Sonic fitting did not finish.", 180);
                                form.TransitionFitReady = null; if (failure != null) throw failure; await SimpleControlsReady(form);
                            }
                            await Fit(false, false); await Fit(true, true); Export("fitted");
                            using (var fittedBaseline = NativeAnimationOptimization.Copy(clip))
                            {
                                string baselineContent = NativeAnimationOptimization.ContentKey(clip);
                                foreach (bool highQuality in (fitOnly ? new bool[0] : new[] { false, true }))
                                {
                                    string tag = highQuality ? "optimized-hq" : "optimized-exact";
                                    bool applied = false, finished = false; Exception failure = null;
                                    form.OptimizationReady = async window =>
                                    {
                                        try
                                        {
                                            SimpleControl<ComboBox>(window, "optimizationMode").SelectedIndex = highQuality ? 1 : 0;
                                            SimpleControl<CheckBox>(window, "optimizationCompactStorage").Checked = true; await window.Analyze();
                                            Check(window.Candidate != null, keyMode + ": " + tag + " prepares a fully verified current-animation candidate: " + SimpleControl<Label>(window, "optimizationSummary").Text);
                                            ReplayRootsMatch(fittedBaseline, window.Candidate, model, rootNames);
                                            foreach (int join in new[] { 0, 4, exitStart - 1, expectedFrames - 1 }) Check(ReplayKnotMatches(fittedBaseline, window.Candidate, join, new HashSet<string>(model.AllBones.Select(b => b.Name), StringComparer.Ordinal), model), keyMode + ": optimization retains fitting boundary/join " + join);
                                            OptimizationWithheldGeometry(window.SourceView, window.TargetView, fittedBaseline, window.Candidate, Path.Combine(modeDirectory, tag + "-withheld.json"));
                                            File.WriteAllText(Path.Combine(modeDirectory, tag + "-report.txt"), SimpleControl<TextBox>(window, "optimizationReport").Text);
                                            await CaptureOptimization(window, Path.Combine(modeDirectory, tag + "-review.png"));
                                            applied = window.PreparedCanApply;
                                            if (applied) SimpleControl<Button>(window, "optimizeApply").PerformClick(); else window.Close();
                                        }
                                        catch (Exception error) { failure = error; window.Close(); }
                                        finally { finished = true; }
                                    };
                                    Call(form, "ShowAnimationOptimization"); await GameLibraryWait(() => finished, "Sonic optimization did not finish.", 180);
                                    form.OptimizationReady = null; if (failure != null) throw failure; await SimpleControlsReady(form); Export(tag);
                                    if (applied) { editor.Undo(); await SimpleControlsReady(form); }
                                    Check(NativeAnimationOptimization.ContentKey(clip) == baselineContent, keyMode + ": independent optimization modes both restore the exact fitted baseline with native Undo");
                                }
                            }
                        }
                        File.WriteAllText(Path.Combine(modeDirectory, "stage-sizes.json"), json.Serialize(stages)); allModes.Add(new { Mode = keyMode.ToString(), ClipIdentity = OptimizationNativeIdentity(clip), AttackTravelControls = positionNames, Stages = stages }); WriteManifest();
                    }
                    finally { form.OptimizationReady = null; form.TransitionFitReady = null; form.RetargetKeyframeReviewReady = null; if (!form.IsDisposed) SimpleDisposeForm(form); }
                }
            }
            if (!fitOnly)
            {
            string providedDirectory = Path.Combine(output, "ProvidedMario"); Directory.CreateDirectory(providedDirectory);
            using (var project = FighterProject.Create("Exact provided Mario animation optimization"))
            using (var form = new ModelForm())
            {
                try
                {
                    form.InitializeFighterProject(project); form.Show(); form.ClientSize = new Size(1700, 950);
                    Call(form, "ImportFighterModel", args[0]); Call(form, "ImportFighterAnimations", new[] { new[] { providedMario } }); await SimpleControlsReady(form);
                    var editor = form.FighterEditor; var clip = editor.SelectedCHR0; var model = (MDL0Node)editor.TargetModel;
                    var setup = await Await<ControlReply>(Call(form, "LoadControls")); var roots = (setup.ProtectedMotionBones ?? new int[0]).Select(i => setup.BoneNames[i]).ToArray();
                    var stages = new List<object>();
                    using (var baseline = NativeAnimationOptimization.Copy(clip))
                    {
                        string content = NativeAnimationOptimization.ContentKey(clip);
                        string file = Path.Combine(providedDirectory, "provided-original.chr0"); form.ExportSimpleWorkspaceResult(file); stages.Add(SonicStage(clip, file, "provided-original"));
                        foreach (bool quality in new[] { false, true })
                        {
                            string tag = quality ? "optimized-hq" : "optimized-exact"; bool applied = false, finished = false; Exception failure = null;
                            form.OptimizationReady = async window =>
                            {
                                try
                                {
                                    SimpleControl<ComboBox>(window, "optimizationMode").SelectedIndex = quality ? 1 : 0;
                                    SimpleControl<CheckBox>(window, "optimizationCompactStorage").Checked = true; await window.Analyze();
                                    Check(window.Candidate != null, "Provided Mario " + tag + ": prepares from exact current-animation data: " + SimpleControl<Label>(window, "optimizationSummary").Text);
                                    ReplayRootsMatch(baseline, window.Candidate, model, roots);
                                    OptimizationWithheldGeometry(window.SourceView, window.TargetView, baseline, window.Candidate, Path.Combine(providedDirectory, tag + "-withheld.json"));
                                    File.WriteAllText(Path.Combine(providedDirectory, tag + "-report.txt"), SimpleControl<TextBox>(window, "optimizationReport").Text);
                                    applied = window.PreparedCanApply; if (applied) SimpleControl<Button>(window, "optimizeApply").PerformClick(); else window.Close();
                                }
                                catch (Exception error) { failure = error; window.Close(); }
                                finally { finished = true; }
                            };
                            Call(form, "ShowAnimationOptimization"); await GameLibraryWait(() => finished, "Provided Mario optimization did not complete.", 180);
                            form.OptimizationReady = null; if (failure != null) throw failure; await SimpleControlsReady(form);
                            file = Path.Combine(providedDirectory, tag + ".chr0"); form.ExportSimpleWorkspaceResult(file); stages.Add(SonicStage(clip, file, tag)); form.SaveFighterProject(Path.Combine(providedDirectory, tag + ".fpstudio"));
                            if (applied) { editor.Undo(); await SimpleControlsReady(form); }
                            Check(NativeAnimationOptimization.ContentKey(clip) == content, "Provided Mario " + tag + ": native Undo returns to the identical independent original baseline");
                        }
                    }
                    File.WriteAllText(Path.Combine(providedDirectory, "stage-sizes.json"), json.Serialize(stages)); allModes.Add(new { Mode = "ProvidedMario", Stages = stages });
                }
                finally { form.OptimizationReady = null; if (!form.IsDisposed) SimpleDisposeForm(form); }
            }
            }
            Check(originals.All(pair => FighterProject.HashFile(pair.Key) == pair.Value), sourceLabel + " pipeline preserves every provided original source/target file and source catalogue");
            WriteManifest();
        }
        finally { Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previousLibrary); }
    }
}
