using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Additive overload: the older forward fixture keeps its existing helper.
    // An authentic generic fixture selects exactly the externally sealed rig,
    // rather than relying on a model name, shadow label or container order.
    static LibraryRegistration UseModelRegistration(string model, string motion, string character, string expectedRig)
    {
        var matching = BrawlLibrarySource.InspectModels(model).Where(item => item.HasGeometry &&
            string.Equals(item.SkeletonSha256, expectedRig, StringComparison.OrdinalIgnoreCase)).ToArray();
        Check(matching.Length == 1, "Library registration resolves one exact externally pinned source rig, rejecting ambiguous native model membership");
        return new LibraryRegistration
        {
            ModelPath = model, ModelKey = matching[0].ResourceKey, MotionPath = motion, Character = character,
            GameVersion = "Private authentic Use-button regression; game build unverified",
            BuildEvidence = "Independent fixture container/hash/rig pins; normal native body-track association validation. No runtime acceptance.",
            ApproveAssociation = true
        };
    }

    static async Task WorkspaceAuthenticLibraryFloat(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Authentic library export-copy qualification requires a private desktop.");
        string Required(string key)
        {
            int at = Array.IndexOf(args, key);
            if (at < 0 || at + 1 >= args.Length || args[at + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Missing authentic library fixture argument: " + key);
            return args[at + 1];
        }
        bool SameHash(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        bool Hash(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
        string targetFile = Path.GetFullPath(args[0]), sourceFile = Path.GetFullPath(args[1]);
        string donor = Path.GetFullPath(Required("--retarget-source")), fixturePath = Path.GetFullPath(Required("--authentic-fixture-manifest"));
        string name = Required("--clip"), fixtureJson;
        using (var file = new FileStream(fixturePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (file.Length < 1 || file.Length > 16384) throw new InvalidDataException("The external fixture manifest exceeds its bounded read limit.");
            using (var reader = new StreamReader(file)) fixtureJson = reader.ReadToEnd();
        }
        var fixture = new JavaScriptSerializer { MaxJsonLength = 16384, RecursionLimit = 16 }.Deserialize<AuthenticFloatDenseFixture>(fixtureJson);
        Check(fixture != null && fixture.Schema == "fighterport-authentic-export-fixture-1" &&
            new[] { fixture.SourceClipSha256, fixture.SourceModelSha256, fixture.TargetModelSha256,
                fixture.SourceRigFingerprint, fixture.TargetRigFingerprint }.All(Hash) &&
            fixture.SourceClipName == name && fixture.SourceFrames > 1 && fixture.SourceFrames <= 4096 &&
            (fixture.SourceVersion == 4 || fixture.SourceVersion == 5), "Actual library fixture uses complete independently supplied native identities and timing");
        var originals = new[] { targetFile, sourceFile, donor, fixturePath }.Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(path => path, FighterProject.HashFile, StringComparer.OrdinalIgnoreCase);
        Check(SameHash(originals[targetFile], fixture.TargetModelSha256) && SameHash(originals[donor], fixture.SourceModelSha256) &&
            SameHash(originals[sourceFile], fixture.SourceClipSha256), "Actual library fixture preserves the exact external source/model bytes, including the provided up-air rather than its canonical snapshot");
        string originalSourceContent;
        using (var original = NodeFactory.FromFile(null, sourceFile) as CHR0Node)
        {
            Check(original != null && original.Name == name && original.FrameCount == fixture.SourceFrames &&
                original.Version == fixture.SourceVersion && original.Loop == fixture.SourceLoop, "Actual standalone source independently supplies its exact native clip identity and duration");
            originalSourceContent = NativeAnimationOptimization.SourceContentKey(original);
        }
        var json = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024, RecursionLimit = 100 };
        var previous = new[] { "FIGHTERPORT_LIBRARY_ROOT", "FIGHTERPORT_LIBRARY_TEMP_ROOT", "FIGHTERPORT_EXPORT_PREFERENCES_ROOT" }
            .ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            var modes = new[] { RetargetKeyframeMode.SourceBoneTimes, RetargetKeyframeMode.SourceChannelTimes };
            if (args.Contains("--authentic-key-mode"))
            {
                modes = modes.Where(mode => mode.ToString() == Required("--authentic-key-mode")).ToArray();
                if (modes.Length != 1) throw new ArgumentException("Choose one supported authentic source-key layout.");
            }
            foreach (var mode in modes)
            {
                string directory = Path.Combine(output, mode.ToString()); Directory.CreateDirectory(directory);
                Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(directory, "isolated-shared-library"));
                Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_TEMP_ROOT", Path.Combine(directory, "isolated-source-scratch"));
                Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", Path.Combine(directory, "isolated-export-preferences"));
                using (var project = FighterProject.Create("Actual verified library source to current model — " + mode))
                using (var form = new ModelForm())
                {
                    project.GroundMode = GroundContactMode.Airborne; // No supplied reviewed floor/ledge context.
                    try
                    {
                        form.InitializeFighterProject(project); form.Show(); form.ClientSize = new Size(1700, 950);
                        Call(form, "SetWorkspaceTextSize", 100); Call(form, "ImportFighterModel", targetFile); await SimpleControlsReady(form);
                        var editor = form.FighterEditor; var target = editor.TargetModel as MDL0Node;
                        Check(target != null && SameHash(BrawlRetargeter.SkeletonFingerprint(target), fixture.TargetRigFingerprint) &&
                            editor.SelectedCHR0 == null && project.Animations.Count == 0, "Normal native import selects the exact target rig in a donor-free model-only document");
                        string originalTargetModel = (string)Call(form, "NativeControlHash", target, ".mdl0");
                        SonicReviewNativeMapping(form, target, fixture.TargetRigFingerprint, directory, "target");
                        using (var root = NodeFactory.FromFile(null, donor))
                        {
                            var matching = Walk(root).OfType<MDL0Node>().Where(model => SameHash(BrawlRetargeter.SkeletonFingerprint(model), fixture.SourceRigFingerprint)).ToArray();
                            Check(matching.Length == 1, "Actual library source mapping uses one externally pinned full rig, with no shadow/name/index substitution");
                            SonicReviewNativeMapping(form, matching[0], fixture.SourceRigFingerprint, directory, "source");
                        }
                        // No ReviewEngineCoordinates call: retain the actual fresh
                        // native +Y/anatomical-heading policy of Use on This Model.
                        Check(project.RigReviews.Count == 0, "Fresh library workflow retains native declared-up coordinate policy without adding an identity frame override");
                        UseModelSelectMode(form, mode);
                        var shared = new AnimationLibrary();
                        var registration = UseModelRegistration(donor, sourceFile, "Sealed native source", fixture.SourceRigFingerprint);
                        var plan = BrawlLibrarySource.Prepare(registration);
                        var planned = plan.Clips.Single(clip => clip.Name == name && clip.Channel == "CHR0 skeletal");
                        Check(plan.Model.SkeletonSha256 == fixture.SourceRigFingerprint && planned.ModelId == plan.Model.Id &&
                            planned.FrameCount == fixture.SourceFrames && planned.NativeVersion == fixture.SourceVersion && planned.Loop == fixture.SourceLoop &&
                            !string.IsNullOrWhiteSpace(planned.AssociationEvidence) && string.IsNullOrWhiteSpace(planned.AssociationIssue),
                            "Normal source registration independently verifies exact model/motion body ownership and records the explicit fixture association review");
                        Check(shared.Register(plan) == 1, "One exact supplied source clip is registered into this isolated shared library");
                        string libraryFiles = NextLibraryFiles(shared);
                        Call(form, "UseSharedSourceLibrary"); await LibraryQuerySettled(form);
                        var sourceItem = await UseModelSelectSource(form, (AnimationLibrary)Field(form, "_library"), name, planned.Id);
                        var displayed = (LibrarySourceLease)Field(form, "_libraryLease");
                        Check(displayed != null && displayed.Animation.FrameCount == fixture.SourceFrames &&
                            NativeAnimationOptimization.SourceContentKey(displayed.Animation) == originalSourceContent &&
                            SameHash(BrawlRetargeter.SkeletonFingerprint(displayed.Model), fixture.SourceRigFingerprint) &&
                            SameHash(displayed.Reference.ModelContainerSha256, fixture.SourceModelSha256) &&
                            SameHash(displayed.Reference.MotionContainerSha256, fixture.SourceClipSha256) && editor.SelectedCHR0 == null,
                            "Verified original source preview resolves the exact clip/costume while leaving the target model-only");
                        bool approved = false; Exception reviewFailure = null;
                        form.RetargetKeyframeReviewReady = raw =>
                        {
                            try
                            {
                                var dense = (CHR0Node)Field(raw, "dense"); var candidate = (CHR0Node)Field(raw, "candidate");
                                var targetRig = BrawlRetargeter.Capture(target);
                                var controls = new HumanoidRetargeter(BrawlRetargeter.Capture(displayed.Model).Rig, targetRig.Rig)
                                    .TargetMotionControls.Select(index => targetRig.Bones[index].Name).ToArray();
                                Check(controls.Length > 0 && RecoveryKeys(dense, controls) == RecoveryKeys(candidate, controls),
                                    "Actual source-pane review preserves every protected own-source control track and native transform flag");
                                using (var copy = NativeAnimationOptimization.CopySource(dense)) copy.ExportChecked(Path.Combine(directory, "dense-next-reference.chr0"));
                                File.WriteAllText(Path.Combine(directory, "retarget-review.txt"), SimpleControl<System.Windows.Forms.TextBox>(raw, "sourceKeysReport").Text);
                                Check(!SimpleControl<System.Windows.Forms.Button>(raw, "sourceKeysUse").Enabled, "Actual Use result requires explicit review before import");
                                SimpleControl<System.Windows.Forms.CheckBox>(raw, "sourceKeysReviewed").Checked = true; approved = true;
                                SimpleControl<System.Windows.Forms.Button>(raw, "sourceKeysUse").PerformClick();
                            }
                            catch (Exception error) { reviewFailure = error; raw.Close(); }
                        };
                        // This is the button's exact action. Bypass only its
                        // MessageBox error wrapper so a real failure propagates
                        // without private-desktop input or a hidden modal hang.
                        try { Call(form, "UseLibraryOnTarget"); }
                        catch (TargetInvocationException error)
                        {
                            File.WriteAllText(Path.Combine(directory, "actual-use-failure.txt"), error.InnerException?.ToString() ?? error.ToString());
                            throw new InvalidOperationException("The actual verified source-pane Use operation failed before optimizer qualification.", error.InnerException ?? error);
                        }
                        if (reviewFailure != null) throw reviewFailure;
                        await SimpleControlsReady(form); form.RetargetKeyframeReviewReady = null;
                        var asset = project.Animations.Single(); var clip = editor.SelectedCHR0; var model = editor.TargetModel as MDL0Node;
                        var quality = asset.Retarget?.ReadKeyframeReport();
                        Check(approved && asset.Retarget != null && asset.Retarget.KeyframeMode == mode && quality != null &&
                            quality.ScalarCertified && quality.QualityVerified && quality.ResultContent == NativeAnimationOptimization.ContentKey(clip) &&
                            clip.FrameCount == fixture.SourceFrames && clip.Version == fixture.SourceVersion && clip.Loop == fixture.SourceLoop &&
                            asset.Retarget.SourceRig == fixture.SourceRigFingerprint && asset.RigIdentity == fixture.TargetRigFingerprint,
                            "Actual Use import owns one exact source-key target revision with its original Next scalar/native qualification");
                        Check(asset.Retarget.Library.ClipId == sourceItem.Clip.Id && asset.Retarget.Library.ModelId == displayed.Reference.ModelId &&
                            asset.Retarget.Library.MotionResourceSha256 == displayed.Reference.MotionResourceSha256 &&
                            asset.Retarget.Library.ModelResourceSha256 == displayed.Reference.ModelResourceSha256 &&
                            SameHash(asset.Retarget.Library.MotionContainerSha256, fixture.SourceClipSha256) &&
                            SameHash(asset.Retarget.Library.ModelContainerSha256, fixture.SourceModelSha256) &&
                            NativeAnimationOptimization.SourceContentKey(displayed.Animation) == originalSourceContent &&
                            ReferenceEquals(Field(form, "_libraryLease"), displayed) && NextLibraryFiles(shared) == libraryFiles,
                            "Actual source identity, native source preview and immutable shared library survive target publication");
                        Check(model != null && SameHash(BrawlRetargeter.SkeletonFingerprint(model), fixture.TargetRigFingerprint) &&
                            (string)Call(form, "NativeControlHash", model, ".mdl0") == originalTargetModel && project.RigReviews.Count == 0 &&
                            clip.Children.All(track => asset.Retarget.Mapping.Any(binding => binding.Target == track.Name)),
                            "Actual library transfer retains target binds/all weights, native coordinate policy and only actual shared mapping tracks");
                        var setup = await Await<ControlReply>(Call(form, "LoadControls"));
                        var roots = (setup.ProtectedMotionBones ?? new int[0]).Select(index => setup.BoneNames[index]).ToArray();
                        Check(roots.Length > 0, "Normal target control setup resolves genuine protected motion controls before export");
                        if (args.Contains("--authentic-l4-apply-current"))
                        {
                            Check(mode == RetargetKeyframeMode.SourceChannelTimes,
                                "Explicit current-L4 Apply qualification uses the exact independently pinned source-channel fixture");
                            await NativeL4CurrentApply(form, clip, Path.Combine(directory, "current-l4-apply"), originals);
                            Check(NativeAnimationOptimization.SourceContentKey(displayed.Animation) == originalSourceContent &&
                                ReferenceEquals(Field(form, "_libraryLease"), displayed) && NextLibraryFiles(shared) == libraryFiles,
                                "Explicit current Apply preserves the original verified source preview and immutable shared library");
                            continue; // Authoring is separate from export-only untouched-master assertions below.
                        }
                        string modelPath = Path.Combine(directory, "target-model.mdl0"); model.ExportChecked(modelPath);
                        string masterPath = Path.Combine(directory, "finished-target-master.chr0"); form.ExportSimpleWorkspaceResult(masterPath);
                        var document = new GameLibraryDocument(form); string camera = CompactCamera(form);
                        int cursor = editor._saveIndex; var undo = editor._undoSaves.ToArray(); var redo = editor._redoSaves.ToArray();
                        string content = NativeAnimationOptimization.SourceContentKey(clip), storage = NativeAnimationOptimization.StorageKey(clip);
                        var siblings = OptimizationUntouchedBytes(form, clip);
                        var stages = new List<object> { SonicStage(clip, masterPath, "actual Use finished target master") };
                        stages.Add(await SonicExportCopy(form, clip, model, modelPath, roots, new int[0], masterPath, directory, "exact-copy", false));
                        await NativeDenseFloatAuthenticExportCopy(form, Path.Combine(directory, "bounded-standard"));
                        stages.Add(await SonicExportCopy(form, clip, model, modelPath, roots, new int[0], masterPath, directory,
                            "bounded-thorough", true, NativeExportSearchEffort.Thorough));
                        document.CheckPreserved("actual Use independent export policies retain current native document/model/weights");
                        OptimizationCheckUntouchedBytes(form, clip, siblings, "actual library Use export copies", Path.Combine(directory, "untouched-resources.json"));
                        Check(CompactCamera(form) == camera && editor._saveIndex == cursor && editor._undoSaves.SequenceEqual(undo) && editor._redoSaves.SequenceEqual(redo) &&
                            NativeAnimationOptimization.SourceContentKey(clip) == content && NativeAnimationOptimization.StorageKey(clip) == storage &&
                            NativeAnimationOptimization.SourceContentKey(displayed.Animation) == originalSourceContent && NextLibraryFiles(shared) == libraryFiles,
                            "Actual library export copies retain native camera/Undo, master/source content/storage and all immutable library files");
                        var registered = NativeGeneratedPairRegistry.Session.Resolve(clip, NativeGeneratedPairRegistry.TryRigKey(model));
                        File.WriteAllText(Path.Combine(directory, "actual-library-export-receipt.json"), json.Serialize(new
                        {
                            FixtureManifest = fixturePath, Fixture = fixture, OriginalInputSha256 = originals, SourceNativeContent = originalSourceContent,
                            SourceClipId = sourceItem.Clip.Id, SourceModelId = displayed.Reference.ModelId, SourceModelKey = registration.ModelKey,
                            ActualSourceReference = displayed.Reference, Mode = mode.ToString(), ResultAssetId = asset.Id,
                            NativeResultIdentity = OptimizationNativeIdentity(clip), Quality = quality, RegisteredWriterPairSites = registered.Length,
                            FinishedMasterContent = content, FinishedMasterStorage = storage, FinishedMasterSha256 = FighterProject.HashFile(masterPath),
                            FinishedMasterBytes = new FileInfo(masterPath).Length, StandardExport = Path.Combine(directory, "bounded-standard", "finished-target-export.chr0"), Stages = stages,
                            CoordinatePolicy = "Fresh native declared +Y up/anatomical heading; no explicit frame review override", RigReviewCount = project.RigReviews.Count,
                            GroundContext = "Explicit airborne fixture; reviewed floor/ledge/contact context is absent", Correction = true,
                            Invocation = "Actual UseLibraryOnTarget button action, without only its MessageBox error wrapper",
                            PrivateDesktop, OSInputInjected = false, ScreenCopied = false, HumanReviewClaimed = false,
                            Claim = "Same finished target master is the only compression reference; genuine unavailable contexts/zero L4 acceptance retain normal fallback. No solver/quality gates changed."
                        }));
                        await UseModelRoundtrip(form, asset, directory, "actual-library-current-result");
                    }
                    finally { form.RetargetKeyframeReviewReady = null; if (!form.IsDisposed) SimpleDisposeForm(form); }
                }
            }
            Check(originals.All(pair => FighterProject.HashFile(pair.Key) == pair.Value), "Actual library workflow retains exact original clip/model/fixture bytes");
        }
        finally { foreach (var pair in previous) Environment.SetEnvironmentVariable(pair.Key, pair.Value); }
    }
}
