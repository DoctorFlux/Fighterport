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
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    sealed class AuthenticFloatDenseFixture
    {
        public AuthenticFloatDenseFixture() { }
        public string Schema { get; set; }
        public string SourceClipSha256 { get; set; }
        public string SourceClipName { get; set; }
        public int SourceFrames { get; set; }
        public string SourceNativeMember { get; set; }
        public int SourceVersion { get; set; }
        public bool SourceLoop { get; set; }
        public string SourceModelSha256 { get; set; }
        public string TargetModelSha256 { get; set; }
        public string SourceRigFingerprint { get; set; }
        public string TargetRigFingerprint { get; set; }
    }

    // Dedicated background fixture driver, activated after preview.63.
    // args[0]: authentic target model/container; args[1]: exact standalone donor
    // CHR0; args[2]: new output; --retarget-source: authentic donor model.
    // This runner invokes ordinary application review/import. It never creates
    // generated-pair evidence or sets protection-known flags itself.
    static async Task WorkspaceAuthenticFloatDense(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Authentic export-copy qualification requires a private desktop.");
        string Required(string key)
        {
            int at = Array.IndexOf(args, key);
            if (at < 0 || at + 1 >= args.Length || args[at + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Missing authentic fixture argument: " + key);
            return args[at + 1];
        }
        string sourceModelInput = Path.GetFullPath(Required("--retarget-source"));
        string sourceFile = Path.GetFullPath(args[1]), targetFile = Path.GetFullPath(args[0]);
        string fixturePath = Path.GetFullPath(Required("--authentic-fixture-manifest"));
        string fixtureJson;
        using (var file = new FileStream(fixturePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (file.Length < 1 || file.Length > 16384)
                throw new InvalidDataException("The external authentic fixture manifest exceeds its bounded read limit.");
            using (var reader = new StreamReader(file)) fixtureJson = reader.ReadToEnd();
        }
        var json = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024, RecursionLimit = 100 };
        var fixture = new JavaScriptSerializer { MaxJsonLength = 16384, RecursionLimit = 16 }
            .Deserialize<AuthenticFloatDenseFixture>(fixtureJson);
        bool Hash(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
        bool SameHash(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        Check(fixture != null && fixture.Schema == "fighterport-authentic-export-fixture-1" &&
            new[] { fixture.SourceClipSha256, fixture.SourceModelSha256, fixture.TargetModelSha256,
                fixture.SourceRigFingerprint, fixture.TargetRigFingerprint }.All(Hash) &&
            fixture.SourceFrames > 1 && fixture.SourceFrames <= ushort.MaxValue &&
            (fixture.SourceVersion == 4 || fixture.SourceVersion == 5),
            "Authentic fixture has independently supplied bounded source/target byte and rig identities");
        string expectedSourceHash = fixture.SourceClipSha256, clipName = Required("--clip");
        Check(expectedSourceHash.Length == 64 && expectedSourceHash.All(Uri.IsHexDigit) &&
            SameHash(FighterProject.HashFile(sourceFile), expectedSourceHash) && fixture.SourceClipName == clipName &&
            SameHash(FighterProject.HashFile(sourceModelInput), fixture.SourceModelSha256) &&
            SameHash(FighterProject.HashFile(targetFile), fixture.TargetModelSha256),
            "Authentic runner uses the exact externally pinned original CHR0, not a catalogue substitute or previous export");
        var originals = new[] { targetFile, sourceFile, sourceModelInput, fixturePath }.Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(path => path, FighterProject.HashFile, StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(output);
        int expectedFrames, expectedVersion; bool expectedLoop; string sourceContent;
        using (var source = NodeFactory.FromFile(null, sourceFile) as CHR0Node)
        {
            Check(source != null && source.Name == clipName && source.FrameCount == fixture.SourceFrames &&
                source.Version == fixture.SourceVersion && source.Loop == fixture.SourceLoop,
                "The pinned standalone donor supplies its actual supported name, duration, version and loop state");
            expectedFrames = source.FrameCount; expectedVersion = source.Version; expectedLoop = source.Loop;
            sourceContent = NativeAnimationOptimization.SourceContentKey(source);
        }
        string sourceModelPath = Path.Combine(output, "verified-source-model.mdl0");
        string sourceRigFingerprint, targetRigFingerprint;
        var modelResolution = new Dictionary<string, object>();
        MDL0Node ResolveModel(ResourceNode root, string expectedFingerprint, string label)
        {
            var models = Walk(root).OfType<MDL0Node>().Select(model => new
            {
                Model = model, Path = model.TreePath, Name = model.Name,
                Fingerprint = BrawlRetargeter.SkeletonFingerprint(model)
            }).ToArray();
            var matching = models.Where(item => SameHash(item.Fingerprint, expectedFingerprint)).ToArray();
            Check(matching.Length == 1, label + ": exactly one native MDL0 in the sealed container matches the external full-rig fingerprint; ambiguity is rejected");
            modelResolution[label] = new
            {
                ExpectedFingerprint = expectedFingerprint,
                SelectedNativePath = matching[0].Path,
                Candidates = models.Select(item => new { item.Path, item.Name, item.Fingerprint }).ToArray()
            };
            return matching[0].Model;
        }
        using (var root = NodeFactory.FromFile(null, sourceModelInput))
        {
            var model = ResolveModel(root, fixture.SourceRigFingerprint, "source");
            sourceRigFingerprint = BrawlRetargeter.SkeletonFingerprint(model);
            model.ExportChecked(sourceModelPath); // Independent owner; original input remains immutable.
        }
        using (var root = NodeFactory.FromFile(null, targetFile))
            targetRigFingerprint = BrawlRetargeter.SkeletonFingerprint(ResolveModel(root, fixture.TargetRigFingerprint, "target"));
        Check(SameHash(sourceRigFingerprint, fixture.SourceRigFingerprint) && SameHash(targetRigFingerprint, fixture.TargetRigFingerprint),
            "Both imported existing-rig revisions match the independently sealed fixture identities, not a self-derived oracle");
        File.WriteAllText(Path.Combine(output, "model-identity-resolution.json"), json.Serialize(modelResolution));
        File.Copy(sourceFile, Path.Combine(output, "exact-original-source.chr0"));

        var previous = new Dictionary<string, string>();
        foreach (string key in new[] { "FIGHTERPORT_LIBRARY_ROOT", "FIGHTERPORT_LIBRARY_TEMP_ROOT", "FIGHTERPORT_EXPORT_PREFERENCES_ROOT" })
        {
            previous[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, Path.Combine(output, "isolated-" + key.ToLowerInvariant()));
        }
        try
        {
            var modes = new[] { RetargetKeyframeMode.SourceBoneTimes, RetargetKeyframeMode.SourceChannelTimes };
            if (args.Contains("--authentic-key-mode"))
            {
                string chosen = Required("--authentic-key-mode");
                modes = modes.Where(mode => mode.ToString() == chosen).ToArray();
                if (modes.Length != 1) throw new ArgumentException("Choose one supported authentic source-key layout.");
            }
            foreach (var mode in modes)
            {
                string directory = Path.Combine(output, mode.ToString()); Directory.CreateDirectory(directory);
                using (var project = FighterProject.Create("Authentic finished-target export copy — " + mode))
                using (var form = new ModelForm())
                {
                    // The aerial fixture supplies no reviewed floor/support context.
                    // This explicit fixture setting does not qualify contacts or change any numeric gate.
                    project.GroundMode = GroundContactMode.Airborne;
                    try
                    {
                        form.InitializeFighterProject(project); form.Show(); form.ClientSize = new Size(1700, 950);
                        Call(form, "SetWorkspaceTextSize", 100);
                        Call(form, "ImportFighterModel", targetFile);
                        Check(form.FighterEditor.TargetModel is MDL0Node importedTarget &&
                            SameHash(BrawlRetargeter.SkeletonFingerprint(importedTarget), fixture.TargetRigFingerprint),
                            mode + ": ordinary native model import selects the exact externally pinned target rig before animation setup");
                        Call(form, "ImportFighterAnimations", new[] { new[] { sourceFile } });
                        await GameLibraryWait(() => !(bool)Field(form, "_controlBusy") && !(bool)Field(form, "_controlSetupQueued"), "Authentic initial controls did not settle.", 120);
                        var editor = form.FighterEditor;
                        var sourceAsset = project.Animations.Single(asset => string.Equals(Path.GetFullPath(asset.SourcePath), sourceFile, StringComparison.OrdinalIgnoreCase));
                        var source = Walk((ResourceNode)Call(form, "FighterRoot", sourceAsset)).OfType<CHR0Node>().Single();
                        Check(NativeAnimationOptimization.SourceContentKey(source) == sourceContent && source.FrameCount == expectedFrames,
                            mode + ": imported source is exactly the provided native clip, with duration read from that file");
                        foreach (ListViewItem row in editor.leftPanel.listAnims.Items) row.Selected = ReferenceEquals(row.Tag, source);
                        editor.SelectedCHR0 = source;
                        await GameLibraryWait(() => !(bool)Field(form, "_controlBusy") && !(bool)Field(form, "_controlSetupQueued"), "Authentic selected-source controls did not settle.", 120);
                        await Await<ControlReply>(Call(form, "LoadControls"));
                        var target = (MDL0Node)editor.TargetModel;
                        SonicReviewNativeMapping(form, target, targetRigFingerprint, directory, "target");
                        using (var donorRoot = NodeFactory.FromFile(null, sourceModelPath))
                            SonicReviewNativeMapping(form, Walk(donorRoot).OfType<MDL0Node>().Single(), sourceRigFingerprint, directory, "source");
                        var targetRig = BrawlRetargeter.Capture(target);
                        var bindings = targetRig.Rig.Roles.ToDictionary(pair => pair.Key, pair => targetRig.Bones[pair.Value].Name);
                        using (var coordinates = Modal("Rig Coordinates", window => window.Controls.OfType<Button>().Single(button => button.Text == "Use Coordinates").PerformClick()))
                            Check((bool)Call(form, "ReviewEngineCoordinates", target, bindings), mode + ": actual target coordinate review completes through the existing dialog");
                        using (var modal = Modal("Retarget Options", window =>
                        {
                            SimpleControl<ComboBox>(window, "retargetKeyframeMode").SelectedIndex = (int)mode;
                            SimpleControl<ComboBox>(window, "retargetGroundMode").SelectedIndex = (int)GroundContactMode.Airborne;
                            SimpleControl<Button>(window, "retargetOptionsApply").PerformClick();
                        })) Call(form, "ShowFighterRetargetOptions");
                        bool reviewed = false; Exception reviewFailure = null;
                        form.RetargetKeyframeReviewReady = raw =>
                        {
                            try
                            {
                                var window = (RetargetKeyframeReviewDialog)raw;
                                Check(!SimpleControl<Button>(window, "sourceKeysUse").Enabled,
                                    mode + ": application import remains guarded until explicit review approval");
                                File.WriteAllText(Path.Combine(directory, "retarget-review.txt"), SimpleControl<TextBox>(window, "sourceKeysReport").Text);
                                using (var denseCopy = NativeAnimationOptimization.CopySource((CHR0Node)Field(window, "dense")))
                                    denseCopy.ExportChecked(Path.Combine(directory, "dense-next-reference.chr0"));
                                SimpleControl<CheckBox>(window, "sourceKeysReviewed").Checked = true;
                                reviewed = true; SimpleControl<Button>(window, "sourceKeysUse").PerformClick();
                            }
                            catch (Exception error) { reviewFailure = error; raw.Close(); }
                        };
                        var resultAsset = form.RetargetFighterAnimation(sourceModelPath); form.RetargetKeyframeReviewReady = null;
                        if (reviewFailure != null) throw reviewFailure;
                        Check(reviewed && resultAsset?.Retarget != null && project.Animations.Count == 2 && resultAsset.Retarget.KeyframeMode == mode &&
                            resultAsset.Retarget.SourceAnimationId == sourceAsset.Id && resultAsset.Retarget.SourceRig == sourceRigFingerprint &&
                            resultAsset.RigIdentity == targetRigFingerprint && resultAsset.Retarget.SourceModelSha256 == FighterProject.HashFile(sourceModelPath),
                            mode + ": actual original Next review/import owns an independent target revision with authentic provenance");
                        await GameLibraryWait(() => !(bool)Field(form, "_controlBusy") && !(bool)Field(form, "_controlSetupQueued"), "Authentic result controls did not settle.", 120);
                        var setup = await Await<ControlReply>(Call(form, "LoadControls"));
                        var clip = editor.SelectedCHR0; var model = (MDL0Node)editor.TargetModel;
                        var quality = resultAsset.Retarget.ReadKeyframeReport();
                        Check(quality.ScalarCertified && quality.QualityVerified && quality.ResultContent == NativeAnimationOptimization.ContentKey(clip) &&
                            clip.FrameCount == expectedFrames && clip.Loop == expectedLoop && clip.Version == expectedVersion,
                            mode + ": selected finished target retains verified native timing and complete source-key quality evidence");
                        string[] roots = (setup.ProtectedMotionBones ?? new int[0]).Select(index => setup.BoneNames[index]).ToArray();
                        Check(roots.Length > 0, mode + ": normal control setup resolves actual protected motion controls");
                        string modelPath = Path.Combine(directory, "target-model.mdl0"); model.ExportChecked(modelPath);
                        string masterPath = Path.Combine(directory, "finished-target-master.chr0"); form.ExportSimpleWorkspaceResult(masterPath);
                        var registered = NativeGeneratedPairRegistry.Session.Resolve(clip, NativeGeneratedPairRegistry.TryRigKey(model));
                        var frozenMaster = NativeAnimationOptimization.SourceContentKey(clip); var frozenStorage = NativeAnimationOptimization.StorageKey(clip);
                        var bone = editor.SelectedBone; int frame = editor.CurrentFrame, cursor = editor._saveIndex;
                        var undo = editor._undoSaves.ToArray(); var redo = editor._redoSaves.ToArray();
                        var siblings = OptimizationUntouchedBytes(form, clip);
                        var document = new GameLibraryDocument(form); string camera = CompactCamera(form);
                        var stages = new List<object> { SonicStage(clip, masterPath, "finished target master") };
                        // Both calls start from the same unchanged master. Search effort changes work, not quality limits.
                        stages.Add(await SonicExportCopy(form, clip, model, modelPath, roots, new int[0], masterPath, directory, "exact-copy", false));
                        await NativeDenseFloatAuthenticExportCopy(form, Path.Combine(directory, "bounded-standard"));
                        stages.Add(await SonicExportCopy(form, clip, model, modelPath, roots, new int[0], masterPath, directory,
                            "bounded-thorough", true, NativeExportSearchEffort.Thorough));
                        Check(ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.TargetModel, model) && ReferenceEquals(editor.SelectedBone, bone) &&
                            editor.CurrentFrame == frame && editor._saveIndex == cursor && editor._undoSaves.SequenceEqual(undo) && editor._redoSaves.SequenceEqual(redo) &&
                            NativeAnimationOptimization.SourceContentKey(clip) == frozenMaster && NativeAnimationOptimization.StorageKey(clip) == frozenStorage,
                            mode + ": all explicit export policies preserve the same editable master, native ownership, selection and Undo");
                        OptimizationCheckUntouchedBytes(form, clip, siblings, "authentic export copies", Path.Combine(directory, "untouched-resources.json"));
                        document.CheckPreserved(mode + " explicit independent export copies retain native model/binds/all weights and document dirty state");
                        Check(CompactCamera(form) == camera, mode + ": export copies preserve the original native viewport camera");
                        File.WriteAllText(Path.Combine(directory, "authentic-export-receipt.json"), json.Serialize(new
                        {
                            FixtureManifest = fixturePath, Fixture = fixture, OriginalInputSha256 = originals, ModelResolution = modelResolution,
                            StagedSourceModelSha256 = FighterProject.HashFile(sourceModelPath),
                            SourceSha256 = expectedSourceHash, SourceFile = sourceFile, SourceFrames = expectedFrames, SourceVersion = expectedVersion,
                            SourceLoop = expectedLoop, SourceContent = sourceContent, SourceRig = sourceRigFingerprint, TargetRig = targetRigFingerprint,
                            Mode = mode.ToString(), ResultAssetId = resultAsset.Id, ResultIdentity = OptimizationNativeIdentity(clip),
                            FinishedMasterContent = frozenMaster, FinishedMasterStorage = frozenStorage, FinishedMasterSha256 = FighterProject.HashFile(masterPath),
                            FinishedMasterBytes = new FileInfo(masterPath).Length, ActualRegisteredWriterPairSites = registered.Length,
                            StandardExport = Path.Combine(directory, "bounded-standard", "finished-target-export.chr0"), ProtectedMotionControls = roots,
                            Quality = quality, Stages = stages, GroundContext = "Explicit airborne fixture; no floor/ledge/contact acceptance",
                            PrivateDesktop, OSInputInjected = false, ScreenCopied = false, HumanReviewClaimed = false,
                            Claim = "Finished target is the compression reference. Real same-process Next provenance only; zero registered/eligible/accepted L4 sites or Exact fallback remain valid reported outcomes, not a filesize-gap fix."
                        }));
                    }
                    finally { form.RetargetKeyframeReviewReady = null; if (!form.IsDisposed) SimpleDisposeForm(form); }
                }
            }
            Check(originals.All(pair => FighterProject.HashFile(pair.Key) == pair.Value), "Authentic runner preserves all original model/motion bytes");
        }
        finally { foreach (var pair in previous) Environment.SetEnvironmentVariable(pair.Key, pair.Value); }
    }
}
