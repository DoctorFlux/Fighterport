using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    static object FourStageFitEvidence(CHR0Node clip) => new
    {
        clip.Name, clip.Version, clip.FrameCount, clip.Loop,
        Tracks = clip.Children.OfType<CHR0EntryNode>().Select(entry => new
        {
            entry.Name, entry.UseModelScale, entry.UseModelRotate, entry.UseModelTranslate,
            entry.ScaleCompensateApply, entry.ScaleCompensateParent, entry.ClassicScaleOff,
            entry.Keyframes.FrameLimit, EntryLoop = entry.Keyframes.Loop,
            Channels = entry.KeyArrays.Select(array => array.Entries.Select(key => new
            {
                Frame = key._index, FrameBits = ReplayFloatBits(key._index),
                ValueBits = ReplayFloatBits(key._value), TangentBits = ReplayFloatBits(key._tangent)
            }).ToArray()).ToArray()
        }).ToArray()
    };

    // Fitting is authoring. Explicitly reviewed reference controls may acquire
    // authored groups; optimization must subsequently preserve that finished
    // master, rather than restoring the earlier attack's UseModel flags.
    static void FourStageFitOracle(CHR0Node before, CHR0Node after, MDL0Node model, string modelFile,
        TransitionFitOptions options, string[] roots, string[] travel, string[] referenceControls,
        string diagnostics, JavaScriptSerializer json)
    {
        var authored = new HashSet<string>(options.MatchReferenceControls ? referenceControls : new string[0], StringComparer.Ordinal);
        var selected = new HashSet<string>(options.Bones, StringComparer.Ordinal);
        File.WriteAllText(Path.Combine(diagnostics, "fit-native-evidence.json"), json.Serialize(new
        {
            Options = options, ReviewedProtectedControls = roots, SourceTravelControls = travel,
            ReviewedReferenceControls = referenceControls, AuthoredReferenceControls = authored.ToArray(),
            Before = FourStageFitEvidence(before), Candidate = FourStageFitEvidence(after)
        }));
        Check(before.Version == after.Version && before.FrameCount == after.FrameCount && before.Loop == after.Loop,
            "Fitting preserves the native animation version, timing and loop state");
        Check(travel.All(name => !authored.Contains(name)), "Reviewed attack travel is separate from opted-in reference controls");
        Check(RecoveryKeys(after, travel) == RecoveryKeys(before, travel), "Stage 3 keeps exact attack-authored travel instead of substituting Fall travel");
        ReplayRootsMatch(before, after, model, roots.Where(name => !authored.Contains(name)).ToArray());
        foreach (var entry in before.Children.OfType<CHR0EntryNode>())
        {
            var result = after.Children.OfType<CHR0EntryNode>().SingleOrDefault(item => item.Name == entry.Name);
            Check(result != null && entry.Keyframes.FrameLimit == result.Keyframes.FrameLimit && entry.Keyframes.Loop == result.Keyframes.Loop,
                "Fitting retains existing entry identity/timebase: " + entry.Name);
            Check(entry.ScaleCompensateApply == result.ScaleCompensateApply && entry.ScaleCompensateParent == result.ScaleCompensateParent && entry.ClassicScaleOff == result.ClassicScaleOff,
                "Fitting retains every scale-compensation and classic-scale flag: " + entry.Name);
            bool[] oldFlags = { entry.UseModelScale, entry.UseModelRotate, entry.UseModelTranslate };
            bool[] newFlags = { result.UseModelScale, result.UseModelRotate, result.UseModelTranslate };
            for (int group = 0; group < 3; group++)
            {
                bool explicitlyAuthored = authored.Contains(entry.Name) && selected.Contains(entry.Name) && (options.Components & (1 << group)) != 0;
                Check(newFlags[group] == oldFlags[group] || explicitlyAuthored && oldFlags[group] && !newFlags[group],
                    "Only an explicitly authored reviewed reference-control component may clear its UseModel flag: " + entry.Name + " / " + group);
            }
            for (int channel = 0; channel < 9; channel++) if (!selected.Contains(entry.Name) || (options.Components & (1 << (channel / 3))) == 0)
            {
                var original = entry.KeyArrays[channel].Entries.ToArray(); var untouched = result.KeyArrays[channel].Entries.ToArray();
                Check(original.Length == untouched.Length && original.Select((key, index) => ReplayFloatBits(key._index) == ReplayFloatBits(untouched[index]._index) &&
                    ReplayFloatBits(key._value) == ReplayFloatBits(untouched[index]._value) && ReplayFloatBits(key._tangent) == ReplayFloatBits(untouched[index]._tangent)).All(equal => equal),
                    "An unselected native fitting channel retains every key and tangent: " + entry.Name + " / " + channel);
            }
        }
        Check(RecoveryRetainedKeysExact(before, after, 4, 34), "Stage 3 retains every native attack-middle key, paired side and tangent, including both join knots");
        var times = new SortedSet<float>(Enumerable.Range(16, 121).Select(value => value / 4f));
        foreach (int frame in Enumerable.Range(4, 31)) foreach (float part in new[] { .137f, .733f }) if (frame + part <= 34) times.Add(frame + part);
        foreach (var clip in new[] { before, after }) foreach (var array in clip.Children.OfType<CHR0EntryNode>().SelectMany(entry => entry.KeyArrays))
        {
            var knots = array.Entries.Select(key => (float)key._index).Where(time => time >= 4 && time <= 34).Distinct().OrderBy(time => time).ToArray();
            foreach (float time in knots) times.Add(time);
            for (int index = 1; index < knots.Length; index++) foreach (float part in new[] { .193f, .631f }) times.Add(knots[index - 1] + (knots[index] - knots[index - 1]) * part);
        }
        double maximumWorldPosition = 0, maximumWorldBasis = 0;
        using (var firstRoot = NodeFactory.FromFile(null, modelFile))
        using (var secondRoot = NodeFactory.FromFile(null, modelFile))
        {
            firstRoot.Populate(); secondRoot.Populate();
            var firstModel = Walk(firstRoot).OfType<MDL0Node>().Single(); var secondModel = Walk(secondRoot).OfType<MDL0Node>().Single();
            firstModel.Populate(); secondModel.Populate();
            var secondBones = secondModel.AllBones.ToDictionary(bone => bone.Name, StringComparer.Ordinal);
            foreach (float time in times)
            {
                Check(model.AllBones.All(bone =>
                {
                    var a = RecoverySample(before, bone, time); var b = RecoverySample(after, bone, time);
                    return RecoverySamePose(a, b) && Enumerable.Range(3, 3).All(channel => Math.Abs((double)a[channel] - b[channel]) <= LoopOrientationTolerance);
                }), "Stage 3 preserves the native middle pose and raw Euler winding at " + time);
                firstModel.ApplyCHR(before, time + 1); secondModel.ApplyCHR(after, time + 1);
                // Check both the actual native evaluator and the explicit
                // UseModel-group interpretation. The latter must agree in the
                // protected middle even though reference controls are authored.
                foreach (bool respectModelFlags in new[] { false, true })
                {
                    if (respectModelFlags)
                        foreach (var pair in new[] { Tuple.Create(firstModel, before), Tuple.Create(secondModel, after) })
                        {
                            foreach (var bone in pair.Item1.AllBones)
                            {
                                var entry = pair.Item2.Children.OfType<CHR0EntryNode>().SingleOrDefault(item => item.Name == bone.Name);
                                if (entry == null) continue;
                                var pose = bone.FrameState;
                                bool[] useModel = { entry.UseModelScale, entry.UseModelRotate, entry.UseModelTranslate };
                                for (int channel = 0; channel < 9; channel++) if (useModel[channel / 3]) pose[channel] = bone.BindState[channel];
                                pose.CalcTransforms(); bone.FrameState = pose;
                            }
                            foreach (var root in pair.Item1.AllBones.Where(bone => !(bone.Parent is MDL0BoneNode))) root.RecalcFrameState();
                        }
                    foreach (var bone in firstModel.AllBones)
                    {
                        var result = secondBones[bone.Name];
                        if (!RecoverySamePose(bone.FrameState, result.FrameState) || !Enumerable.Range(3, 3).All(channel => Math.Abs((double)bone.FrameState[channel] - result.FrameState[channel]) <= LoopOrientationTolerance))
                            throw new Exception("Stage 3 changed bind-effective local middle motion: " + bone.Name + " / " + time + " / model flags " + respectModelFlags);
                        double position = Math.Sqrt(Enumerable.Range(12, 3).Sum(component => Math.Pow((double)bone._frameMatrix[component] - result._frameMatrix[component], 2)));
                        double basis = Enumerable.Range(0, 3).SelectMany(column => Enumerable.Range(0, 3).Select(row => Math.Abs((double)bone._frameMatrix[column * 4 + row] - result._frameMatrix[column * 4 + row]))).Max();
                        maximumWorldPosition = Math.Max(maximumWorldPosition, position); maximumWorldBasis = Math.Max(maximumWorldBasis, basis);
                        if (!(position <= LoopPositionTolerance && basis <= LoopScaleTolerance))
                            throw new Exception("Stage 3 changed native hierarchy middle motion: " + bone.Name + " / " + time + " / model flags " + respectModelFlags + " / position " + position + " / basis " + basis);
                    }
                }
            }
        }
        Check(true, "Stage 3 preserves all native and UseModel-effective local/hierarchy middle samples, including fractional witnesses and native knots");
        File.WriteAllText(Path.Combine(diagnostics, "fit-middle-witnesses.json"), json.Serialize(new
        {
            FirstNativeFrame = 4, LastNativeFrame = 34, Samples = times.Count, NativeAndModelFlagAware = true,
            MaximumWorldPosition = maximumWorldPosition, MaximumWorldBasisComponent = maximumWorldBasis,
            ContinuousGeometryCertificate = false, ExternalRuntimeAcceptance = false
        }));
    }

    static async Task WorkspaceSonicFourStages(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Four-stage export qualification requires the private desktop.");
        string Arg(string key) => args[Array.IndexOf(args, key) + 1];
        string sonicMotion = Path.GetFullPath(args[1]), sonicModel = Path.GetFullPath(Arg("--retarget-source")), marioModel = Path.GetFullPath(args[0]), marioMotion = Path.GetFullPath(Arg("--target-motion"));
        var originalHashes = new[] { sonicMotion, sonicModel, marioModel, marioMotion }.Distinct().ToDictionary(path => path, FighterProject.HashFile);
        var json = new JavaScriptSerializer { MaxJsonLength = 256 * 1024 * 1024 };
        string previousLibrary = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT"), previousPreferences = Environment.GetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "isolated-library"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", Path.Combine(output, "isolated-export-settings"));
        string exports = Path.Combine(output, "exports"), diagnostics = Path.Combine(output, "diagnostics"), projects = Path.Combine(output, "projects");
        Directory.CreateDirectory(exports); Directory.CreateDirectory(diagnostics); Directory.CreateDirectory(projects);
        var stages = new List<object>();
        string sourceFingerprint = "c3813c2db05d2fee9eb43222432dd6ac62395ffd2d02ade940bf6b81499444aa", targetFingerprint = "87b3134758d85871c2f5fadb1bd84332fcac9f252df7da82738a024cad68f0e9";
        void Manifest() => File.WriteAllText(Path.Combine(output, "four-stage-manifest.json"), json.Serialize(new {
            Recipe = "Original Next Dense, full body first-Fall start/end, Thorough bounded independent export", PrivateDesktop,
            OSInputInjected = false, ScreenCopied = false, SourceModel = sonicModel, SourceAnimation = sonicMotion,
            TargetModel = marioModel, TargetMotion = marioMotion, Frames = 44, EntryJoinDisplay = 5, ExitJoinDisplay = 35, ReferenceFrameDisplay = 1,
            OptimizationReference = "Same full-quality finished Mario fitted master; never the donor or a previous lossy export", Stages = stages }));
        object Stage(CHR0Node clip, string file, string label)
        {
            var result = SonicStage(clip, file, label);
            string expanded = Path.ChangeExtension(file, ".i12.chr0");
            if (File.Exists(expanded)) File.Move(expanded, Path.Combine(diagnostics, Path.GetFileName(expanded)));
            File.WriteAllText(Path.Combine(diagnostics, Path.GetFileNameWithoutExtension(file) + "-bytes.json"), json.Serialize(NativeChr0SizeReport.Read(file)));
            stages.Add(result); Manifest(); return result;
        }
        async Task Select(ModelForm form, CHR0Node clip)
        {
            await SimpleControlsReady(form);
            foreach (ListViewItem row in form.FighterEditor.leftPanel.listAnims.Items) row.Selected = ReferenceEquals(row.Tag, clip);
            form.FighterEditor.SelectedCHR0 = clip;
            await SimpleControlsReady(form); await Await<ControlReply>(Call(form, "LoadControls"));
            Check(ReferenceEquals(form.FighterEditor.SelectedCHR0, clip) && ReferenceEquals(form.FighterEditor.TargetAnimation, clip), "Four-stage workflow selects the exact current native CHR0 through its actual animation list");
        }
        void SaveAndReopen(ModelForm form, FighterAsset asset, CHR0Node clip, string tag)
        {
            string content = NativeAnimationOptimization.SourceContentKey(clip), storage = NativeAnimationOptimization.StorageKey(clip);
            string path = Path.Combine(projects, tag + ".fpstudio"); form.SaveFighterProject(path);
            using (var saved = FighterProject.Open(path))
            using (var savedRoot = NodeFactory.FromFile(null, saved.Animations.Single(item => item.Id == asset.Id).WorkingPath))
            {
                var decoded = Walk(savedRoot).OfType<CHR0Node>().Single(item => item.Name == clip.Name);
                Check(NativeAnimationOptimization.SourceContentKey(decoded) == content && NativeAnimationOptimization.StorageKey(decoded) == storage,
                    tag + ": ordinary project Save/reopen retains the exact full-quality master and native storage");
            }
            Check(NativeAnimationOptimization.SourceContentKey(clip) == content && NativeAnimationOptimization.StorageKey(clip) == storage,
                tag + ": ordinary Save leaves all live master coefficients/metadata unchanged");
        }
        try
        {
            // A real native re-export on its own character comes first. It is
            // measured independently from retargeted/fitted target masters.
            using (var form = new ModelForm())
            {
                try
                {
                    form.InitializeFighterProject(FighterProject.Create("Sonic AttackAirLw native re-export")); form.Show(); form.ClientSize = new Size(1700, 950);
                    Call(form, "ImportFighterModel", sonicModel); Call(form, "ImportFighterAnimations", new[] { new[] { sonicMotion } });
                    var clip = form.FighterEditor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(); await Select(form, clip);
                    var model = (MDL0Node)form.FighterEditor.TargetModel;
                    Check(clip.Name == "AttackAirLw" && clip.FrameCount == 44 && BrawlRetargeter.SkeletonFingerprint(model) == sourceFingerprint,
                        "Stage 1 uses the exact supplied 44-frame Sonic animation on the authentic Sonic rig");
                    string before = NativeAnimationOptimization.SourceContentKey(clip), storage = NativeAnimationOptimization.StorageKey(clip);
                    NativeChr0RetainedDenseAccountingChecks(sonicMotion);
                    string file = Path.Combine(exports, "01-Sonic-original.chr0"); form.ExportSimpleWorkspaceResult(file); Stage(clip, file, "Sonic native re-export");
                    using (var original = NodeFactory.FromFile(null, sonicMotion) as CHR0Node)
                        Check(original != null && NativeAnimationOptimization.SourceContentKey(original) == before && NativeAnimationOptimization.StorageKey(original) == storage,
                            "Stage 1 preserves every source coefficient, paired side, flag, timing field and retained codec payload");
                    File.WriteAllText(Path.Combine(diagnostics, "sonic-reexport.json"), json.Serialize(new { SourceBytes = new FileInfo(sonicMotion).Length, ExportBytes = new FileInfo(file).Length,
                        SourceSha256 = originalHashes[sonicMotion], ExportSha256 = FighterProject.HashFile(file), ByteIdentical = originalHashes[sonicMotion] == FighterProject.HashFile(file) }));
                    SaveAndReopen(form, form.FighterProject.Animations.Single(), clip, "01-Sonic-original");
                    form.FighterEditor.FrameFighterModel(); await CaptureSimpleWorkspace(form, Path.Combine(diagnostics, "01-Sonic-original.png"));
                }
                finally { if (!form.IsDisposed) SimpleDisposeForm(form); }
            }
            using (var form = new ModelForm())
            {
                try
                {
                    form.InitializeFighterProject(FighterProject.Create("Sonic AttackAirLw to Mario four-stage workflow")); form.Show(); form.ClientSize = new Size(1700, 950);
                    Call(form, "ImportFighterModel", marioModel); Call(form, "ImportFighterAnimations", new[] { new[] { marioMotion, sonicMotion } });
                    var sourceAsset = form.FighterProject.Animations.Single(asset => Path.GetFullPath(asset.SourcePath) == sonicMotion);
                    var source = Walk((ResourceNode)Call(form, "FighterRoot", sourceAsset)).OfType<CHR0Node>().Single(); await Select(form, source);
                    var editor = form.FighterEditor; var model = (MDL0Node)editor.TargetModel;
                    var fall = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(clip => clip.Name == "Fall");
                    using (var copy = NativeAnimationOptimization.CopySource(fall)) copy.ExportChecked(Path.Combine(diagnostics, "Mario-Fall-first-pose-reference.chr0"));
                    model.ExportChecked(Path.Combine(diagnostics, "Mario-target.mdl0"));
                    SonicReviewNativeMapping(form, model, targetFingerprint, diagnostics, "target");
                    string donorSnapshot = Path.Combine(diagnostics, "Sonic-source.mdl0");
                    using (var donorRoot = NodeFactory.FromFile(null, sonicModel))
                    {
                        var donor = Walk(donorRoot).OfType<MDL0Node>().Single(item => { item.Populate(); return item.Name == "FitSonic00"; });
                        SonicReviewNativeMapping(form, donor, sourceFingerprint, diagnostics, "source");
                        donor.ExportChecked(donorSnapshot);
                    }
                    Check((RetargetKeyframeMode)Field(form, "_retargetKeyframeMode") == RetargetKeyframeMode.Dense,
                        "Stage 2 retains the normal original Next mode without a source-key option");
                    var resultAsset = form.RetargetFighterAnimation(donorSnapshot);
                    Check(resultAsset != null && resultAsset.Retarget.Algorithm == HumanoidRetargeter.Algorithm && resultAsset.Retarget.KeyframeMode == RetargetKeyframeMode.Dense,
                        "Stage 2 imports the original Next full-quality target result in its existing native transaction");
                    await SimpleControlsReady(form); var clip = editor.SelectedCHR0;
                    Check(clip != null && clip.FrameCount == 44 && !clip.Loop, "Stage 2 preserves the source duration and native loop behavior");
                    var setup = await Await<ControlReply>(Call(form, "LoadControls"));
                    string[] roots = (setup.ProtectedMotionBones ?? new int[0]).Select(index => setup.BoneNames[index]).ToArray();
                    string[] travel = setup.Roles.Where(role => role.Reviewed && role.Role == "RootMotion").Select(role => setup.BoneNames[role.Bone.Value]).ToArray();
                    Check(roots.Length > 0 && travel.Length > 0, "The actual target has reviewed protected motion-control and travel bindings");
                    string rawFile = Path.Combine(exports, "02-Mario-retargeted.chr0"); form.ExportSimpleWorkspaceResult(rawFile); Stage(clip, rawFile, "Mario original Next master");
                    SaveAndReopen(form, resultAsset, clip, "02-Mario-retargeted");
                    editor.FrameFighterModel(); await CaptureSimpleWorkspace(form, Path.Combine(diagnostics, "02-Mario-retargeted.png"));
                    using (var rawMaster = NativeAnimationOptimization.CopySource(clip))
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
                                    choices.SelectedItem = choices.Items.OfType<TransitionClipChoice>().Single(choice => ReferenceEquals(choice.Clip, fall));
                                    SimpleControl<CheckBox>(window, "transition_" + side + "Frozen").Checked = true;
                                    SimpleControl<NumericUpDown>(window, "transition_" + side + "Frame").Value = 1;
                                }
                                SimpleControl<NumericUpDown>(window, "transition_entrySamples").Value = 5;
                                SimpleControl<NumericUpDown>(window, "transition_exitStart").Value = 35;
                                SimpleControl<CheckBox>(window, "transition_existingTracksOnly").Checked = false;
                                SimpleControl<CheckBox>(window, "transition_binding").Checked = true;
                                await (Task)Call(window, "PrepareFit");
                                File.WriteAllText(Path.Combine(diagnostics, "fit-options.json"), json.Serialize(window.ReadOptions()));
                                File.WriteAllText(Path.Combine(diagnostics, "fit-report.txt"), SimpleControl<TextBox>(window, "transitionReport").Text);
                                Check(window.Candidate != null, "Stage 3 prepares the full-body frozen first-Fall transition: " + SimpleControl<TextBox>(window, "transitionReport").Text);
                                File.WriteAllText(Path.Combine(diagnostics, "fit-live-attack-evidence.json"), json.Serialize(FourStageFitEvidence(clip)));
                                FourStageFitOracle(rawMaster, window.Candidate, model, Path.Combine(diagnostics, "Mario-target.mdl0"),
                                    window.ReadOptions(), roots, travel, (string[])Field(form, "_referenceFitControls"), diagnostics, json);
                                var body = new HashSet<string>(window.ReadOptions().Bones, StringComparer.Ordinal);
                                foreach (int frame in new[] { 0, 43 })
                                    Check(model.AllBones.Where(bone => body.Contains(bone.Name) && !travel.Contains(bone.Name)).All(bone => RecoverySamePose(RecoverySample(window.Candidate, bone, frame), RecoverySample(fall, bone, 0))),
                                        "Stage 3 body endpoint " + (frame + 1) + " reaches only Mario Fall's frozen first frame");
                                SimpleControl<Button>(window, "applyTransition").PerformClick();
                            }
                            catch (Exception error) { failure = error; window.Close(); }
                            finally { finished = true; }
                        };
                        Call(form, "ShowTransitionFit"); await GameLibraryWait(() => finished, "Four-stage fitting did not finish.", 180); form.TransitionFitReady = null;
                        if (failure != null) throw failure;
                        await SimpleControlsReady(form);
                        Check(ReferenceEquals(editor.SelectedCHR0, clip), "Stage 3 applies fitting to the same current native animation member");
                        string fittedFile = Path.Combine(exports, "03-Mario-fitted-Fall.chr0"); form.ExportSimpleWorkspaceResult(fittedFile); Stage(clip, fittedFile, "Mario fitted master");
                        SaveAndReopen(form, resultAsset, clip, "03-Mario-fitted-Fall");
                        editor.FrameFighterModel(); await CaptureSimpleWorkspace(form, Path.Combine(diagnostics, "03-Mario-fitted-Fall.png"));
                        // One explicit strongest search; no preset options are
                        // added to retargeting/fitting or ordinary project Save.
                        object optimized = await SonicExportCopy(form, clip, model, Path.Combine(diagnostics, "Mario-target.mdl0"), roots,
                            new[] { 0, 4, 34, 43 }, fittedFile, exports, "04-Mario-optimized", true, NativeExportSearchEffort.Thorough, args.Contains("--span-refit-required"));
                        stages.Add(optimized);
                        string expanded = Path.Combine(exports, "04-Mario-optimized.i12.chr0");
                        if (File.Exists(expanded)) File.Move(expanded, Path.Combine(diagnostics, Path.GetFileName(expanded)));
                        foreach (string suffix in new[] { "-withheld.json", "-report.txt", "-bytes.txt", "-context.json", "-refit-evidence.json" })
                        {
                            string extra = Path.Combine(exports, "04-Mario-optimized" + suffix);
                            if (File.Exists(extra)) File.Move(extra, Path.Combine(diagnostics, Path.GetFileName(extra)));
                        }
                        File.WriteAllText(Path.Combine(diagnostics, "04-Mario-optimized-bytes.json"), json.Serialize(NativeChr0SizeReport.Read(Path.Combine(exports, "04-Mario-optimized.chr0"))));
                        string standard = Path.Combine(diagnostics, "standard-effort-control"); Directory.CreateDirectory(standard);
                        stages.Add(await SonicExportCopy(form, clip, model, Path.Combine(diagnostics, "Mario-target.mdl0"), roots,
                            new[] { 0, 4, 34, 43 }, fittedFile, standard, "same-master-standard-control", true, NativeExportSearchEffort.Standard));
                        SaveAndReopen(form, resultAsset, clip, "04-full-quality-master-after-export");
                    }
                }
                finally { form.TransitionFitReady = null; if (!form.IsDisposed) SimpleDisposeForm(form); }
            }
            Check(Directory.GetFiles(exports, "*.chr0").Length == 4, "The simple export folder contains exactly the four requested sequential native animations");
            Check(originalHashes.All(pair => FighterProject.HashFile(pair.Key) == pair.Value), "All original Sonic/Mario model and animation inputs remain byte-identical after the complete sequence");
            File.WriteAllText(Path.Combine(diagnostics, "input-hashes.json"), json.Serialize(originalHashes)); Manifest();
        }
        finally { Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previousLibrary); Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", previousPreferences); }
    }
}
