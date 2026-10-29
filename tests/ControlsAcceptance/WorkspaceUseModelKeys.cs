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
using System.Windows.Forms;

internal static partial class Program
{
    // Exercise the actual source-pane button, rather than qualifying only the
    // lower-level Bake API or the separate selected-animation retarget command.
    // The original Next dense solver remains its independently checked reference.
    static void UseModelSelectMode(ModelForm form, RetargetKeyframeMode mode)
    {
        using (var modal = Modal("Retarget Options", window =>
        {
            var keys = SimpleControl<ComboBox>(window, "retargetKeyframeMode");
            Check(keys.Items.Count == 4 && keys.SelectedIndex == (int)(RetargetKeyframeMode)Field(form, "_retargetKeyframeMode"),
                "Retarget options reflects the current session representation without silently resetting it");
            keys.SelectedIndex = (int)mode;
            SimpleControl<Button>(window, "retargetOptionsApply").PerformClick();
        })) Call(form, "ShowFighterRetargetOptions");
        Check((RetargetKeyframeMode)Field(form, "_retargetKeyframeMode") == mode,
            "An explicit retarget representation remains selected: " + mode);
    }

    static LibraryRegistration UseModelRegistration(string model, string motion, string character)
    {
        var info = BrawlLibrarySource.InspectModels(model).First(item => !item.Shadow && item.HasGeometry);
        return new LibraryRegistration { ModelPath = model, ModelKey = info.ResourceKey, MotionPath = motion, Character = character,
            GameVersion = "Private native Use-button regression; game build unverified",
            BuildEvidence = "Supplied authentic model; motion identity independently retained. No game-runtime acceptance.", ApproveAssociation = true };
    }

    static async Task<LibrarySearchItem> UseModelSelectSource(ModelForm form, AnimationLibrary library, string name, string exactClipId = null)
    {
        var item = library.Browse(new LibraryQuery { Text = exactClipId == null ? name : null, Channel = "CHR0 skeletal" })
            .Single(row => (exactClipId == null ? row.Clip.Name == name : row.Clip.Id == exactClipId) && row.Model != null);
        await Flow39Select(form, item);
        Call(form, "PauseSourceOperation");
        Check(SimpleControl<Button>(form, "libraryUseOnTarget").Enabled &&
            ((LibrarySourceLease)Field(form, "_libraryLease")).Reference.ClipId == item.Clip.Id,
            "The actual Use on This Model button is enabled for the exact selected original source");
        return item;
    }

    static async Task UseModelRoundtrip(ModelForm form, FighterAsset asset, string output, string label)
    {
        var editor = form.FighterEditor; var clip = editor.SelectedCHR0;
        string content = NativeAnimationOptimization.ContentKey(clip), identity = OptimizationNativeIdentity(clip);
        int frame = editor.CurrentFrame; var target = editor.TargetModel; var bone = editor.SelectedBone;
        int undo = editor._saveIndex;
        string file = Path.Combine(output, label + ".chr0");
        form.ExportSimpleWorkspaceResult(file);
        using (var reopened = NodeFactory.FromFile(null, file) as CHR0Node)
            Check(reopened != null && NativeAnimationOptimization.ContentKey(reopened) == content,
                label + ": ordinary native export/reopen retains the exact chosen retarget layout and source-owned motion");
        string manifest = Path.Combine(output, label + ".fpstudio"); form.SaveFighterProject(manifest);
        using (var project = FighterProject.Open(manifest))
        {
            var saved = project.Animations.Single(item => item.Id == asset.Id);
            saved.Retarget.ValidateKeyframeEvidence();
            using (var root = NodeFactory.FromFile(null, saved.WorkingPath))
                Check(saved.Retarget.KeyframeMode == asset.Retarget.KeyframeMode &&
                    NativeAnimationOptimization.ContentKey(Walk(root).OfType<CHR0Node>().Single()) == content &&
                    saved.Retarget.Library.ClipId == asset.Retarget.Library.ClipId && saved.Retarget.Library.ModelId == asset.Retarget.Library.ModelId,
                    label + ": project Save/reopen retains exact result content, source identity, costume and chosen key policy");
        }
        await SimpleControlsReady(form);
        Check(ReferenceEquals(editor.TargetModel, target) && ReferenceEquals(editor.SelectedCHR0, clip) &&
            ReferenceEquals(editor.SelectedBone, bone) && editor.CurrentFrame == frame && editor._saveIndex == undo &&
            OptimizationNativeIdentity(clip) == identity && NativeAnimationOptimization.ContentKey(clip) == content,
            label + ": export and Save preserve the current native member, target/bone/playhead and existing Undo owner");
    }

    static async Task WorkspaceUseModelKeys(string[] args, string output)
    {
        if (!PrivateDesktop || !args.Contains("--retarget-source"))
            throw new InvalidOperationException("Use-button key-layout qualification requires a private desktop and authentic source model.");
        string donor = args[Array.IndexOf(args, "--retarget-source") + 1];
        string name = args[Array.IndexOf(args, "--clip") + 1];
        const string sourceFingerprint = "c3813c2db05d2fee9eb43222432dd6ac62395ffd2d02ade940bf6b81499444aa";
        const string targetFingerprint = "87b3134758d85871c2f5fadb1bd84332fcac9f252df7da82738a024cad68f0e9";
        string libraryBefore = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        string exportBefore = Environment.GetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "isolated-shared-library"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", Path.Combine(output, "isolated-export-preferences"));
        var originals = new[] { args[0], args[1], donor }.Distinct().ToDictionary(path => path, FighterProject.HashFile);
        var project = FighterProject.Create("Actual Use on This Model source-key qualification");
        // This is an aerial layout test, with no supplied floor/support context.
        // It retains all existing numerical limits and makes no contact claim.
        project.GroundMode = GroundContactMode.Airborne;
        var form = new ModelForm(); var evidence = new List<object>();
        try
        {
            form.InitializeFighterProject(project); form.Show(); form.ClientSize = new Size(1700, 950);
            Call(form, "ImportFighterModel", args[0]); await SimpleControlsReady(form);
            var editor = form.FighterEditor; var target = (MDL0Node)editor.TargetModel; var viewport = editor.ModelPanel;
            string modelBefore = (string)Call(form, "NativeControlHash", target, ".mdl0");
            Check(editor.SelectedCHR0 == null && (RetargetKeyframeMode)Field(form, "_retargetKeyframeMode") == RetargetKeyframeMode.SourceChannelTimes,
                "Fresh model-only workspace defaults Use on This Model to source channel keys");
            var options = NextItem(form, "nextRetargetOptions");
            Check(options.Available && options.Enabled, "Primary Animation menu exposes retarget options before a target animation exists");
            using (var modal = Modal("Retarget Options", window =>
            {
                Check(SimpleControl<ComboBox>(window, "retargetKeyframeMode").SelectedIndex == (int)RetargetKeyframeMode.SourceChannelTimes,
                    "Model-only retarget options shows the source-channel session default");
                SimpleControl<Button>(window, "retargetOptionsApply").PerformClick();
            })) options.PerformClick();

            SonicReviewNativeMapping(form, target, targetFingerprint, output, "target");
            using (var sourceRoot = NodeFactory.FromFile(null, donor))
            {
                var models = Walk(sourceRoot).OfType<MDL0Node>().Where(model => BrawlRetargeter.SkeletonFingerprint(model) == sourceFingerprint).ToArray();
                Check(models.Length == 1, "Actual Use-button test identifies exactly its sealed authentic source rig");
                SonicReviewNativeMapping(form, models.Single(), sourceFingerprint, output, "source");
            }
            var shared = new AnimationLibrary(); shared.Register(BrawlLibrarySource.Prepare(UseModelRegistration(donor, args[1], "sonic")));
            string sharedFiles = NextLibraryFiles(shared);
            Call(form, "UseSharedSourceLibrary"); await LibraryQuerySettled(form);
            var sourceItem = await UseModelSelectSource(form, (AnimationLibrary)Field(form, "_library"), name);
            var displayed = (LibrarySourceLease)Field(form, "_libraryLease");
            string sourceContent = NativeAnimationOptimization.SourceContentKey(displayed.Animation);
            int initialImports = project.Animations.Count, initialUndo = editor._saveIndex, initialFrame = editor.CurrentFrame;
            var initialBone = editor.SelectedBone; bool initialDirty = (bool)Field(form, "_fighterDirty");
            bool cancelled = false;
            form.RetargetKeyframeReviewReady = window =>
            {
                cancelled = true;
                Check(((RetargetKeyframeReviewDialog)window).ResultView.Heading.Text.Contains("Source channel keys"),
                    "Actual Use button reaches source-channel review instead of silently baking Dense");
                SimpleControl<Button>(window, "sourceKeysCancel").PerformClick();
            };
            SimpleControl<Button>(form, "libraryUseOnTarget").PerformClick(); await SimpleControlsReady(form);
            Check(cancelled && project.Animations.Count == initialImports && editor.SelectedCHR0 == null &&
                ReferenceEquals(editor.TargetModel, target) && ReferenceEquals(editor.ModelPanel, viewport) && ReferenceEquals(editor.SelectedBone, initialBone) &&
                editor._saveIndex == initialUndo && editor.CurrentFrame == initialFrame && (bool)Field(form, "_fighterDirty") == initialDirty &&
                (string)Call(form, "NativeControlHash", target, ".mdl0") == modelBefore,
                "Cancelling the actual model-only Use-button review preserves target, binds/all weights, frame, document and native Undo");

            Exception reviewFailure = null; bool approved = false;
            form.RetargetKeyframeReviewReady = async raw =>
            {
                try
                {
                    var window = (RetargetKeyframeReviewDialog)raw;
                    Check(!SimpleControl<Button>(window, "sourceKeysUse").Enabled,
                        "Actual Use-button candidate requires explicit review approval before publication");
                    var dense = (CHR0Node)Field(raw, "dense"); var candidate = (CHR0Node)Field(raw, "candidate");
                    var targetRig = BrawlRetargeter.Capture(target);
                    var controls = new HumanoidRetargeter(BrawlRetargeter.Capture(displayed.Model).Rig, targetRig.Rig)
                        .TargetMotionControls.Select(index => targetRig.Bones[index].Name).ToArray();
                    Check(controls.Length > 0 && RecoveryKeys(dense, controls) == RecoveryKeys(candidate, controls),
                        "Actual Use-button candidate keeps every original Next own-source control's native keys and transform flags exactly");
                    await CaptureSourceKeys(window, Path.Combine(output, "use-model-source-channel-review.png"));
                    approved = true; SimpleControl<CheckBox>(window, "sourceKeysReviewed").Checked = true;
                    SimpleControl<Button>(window, "sourceKeysUse").PerformClick();
                }
                catch (Exception error) { reviewFailure = error; raw.Close(); }
            };
            SimpleControl<Button>(form, "libraryUseOnTarget").PerformClick(); if (reviewFailure != null) throw reviewFailure;
            await SimpleControlsReady(form); form.RetargetKeyframeReviewReady = null;
            var resultAsset = project.Animations.Last(); var result = editor.SelectedCHR0; var report = resultAsset.Retarget.ReadKeyframeReport();
            Check(approved && project.Animations.Count == initialImports + 1 && resultAsset.Retarget.KeyframeMode == RetargetKeyframeMode.SourceChannelTimes &&
                report.ResultContent == NativeAnimationOptimization.ContentKey(result) && report.ScalarCertified && report.QualityVerified,
                "Actual Use-button approval imports one source-channel revision with continuous scalar and final native quality evidence");
            Check(resultAsset.Retarget.Library.ClipId == sourceItem.Clip.Id && resultAsset.Retarget.Library.ModelId == displayed.Reference.ModelId &&
                resultAsset.Retarget.Library.MotionResourceSha256 == displayed.Reference.MotionResourceSha256 &&
                resultAsset.Retarget.Library.ModelResourceSha256 == displayed.Reference.ModelResourceSha256 &&
                NativeAnimationOptimization.SourceContentKey(displayed.Animation) == sourceContent && ReferenceEquals(Field(form, "_libraryLease"), displayed),
                "The real source-pane button preserves the selected source clip/costume, authentic hashes and original small preview");
            Check(result.Children.All(track => resultAsset.Retarget.Mapping.Any(binding => binding.Target == track.Name)) &&
                result.Children.All(track => track.Name != "WaistNb"),
                "Authentic Sonic-to-Mario Use-button output contains only shared mapped tracks and no target-only WaistNb");
            Check(report.ResultKeys < report.DenseKeys && report.ResultBytes <= report.DenseBytes,
                "Authentic source-channel Use-button output reduces actual keys and does not exceed its same-motion Dense reference bytes");
            evidence.Add(new { Scope = "Actual shared-library Use button", Mode = resultAsset.Retarget.KeyframeMode.ToString(), Report = report,
                SourceClip = sourceItem.Clip.Id, SourceModel = displayed.Reference.ModelId, SourceContent = sourceContent });
            await InterpolatedNativeBodyUndo(form, resultAsset);
            await UseModelRoundtrip(form, resultAsset, output, "authentic-use-model-source-channel");

            // Explicit sparse data on an authentic same-rig source supplies a
            // controlled interval where quality permits interpolation: the two
            // body knots at native 0 and 16 are the only necessary X keys.
            var rig = BrawlRetargeter.Capture(target); var body = rig.Bones[rig.Rig.Roles["pelvis"]];
            var secondBody = rig.Bones[rig.Rig.Roles["spine"]];
            Check(secondBody.Name != body.Name, "Sparse fixture supplies two independent structural body bindings for the existing library admission contract");
            string sparsePath = Path.Combine(output, "procedural-source-0-16.chr0");
            string sparseResourceHash;
            using (var sparse = new CHR0Node { Name = "Original sparse source 0 and 16", FrameCount = 17 })
            {
                var entry = sparse.CreateEntry(body.Name); entry.Keyframes.PreserveInterpolation = true;
                foreach (int at in new[] { 0, 16 }) entry.Keyframes.SetFrameValue(3, at, body.BindState[3] + at * .5f, true)._tangent = .5f;
                foreach (int channel in new[] { 4, 5 }) entry.Keyframes.SetFrameValue(channel, 0, body.BindState[channel], true)._tangent = 0;
                // Library ownership deliberately requires at least two native
                // body tracks. A fixed authentic spine track establishes that
                // association without adding any keys inside the tested span.
                var fixedBody = sparse.CreateEntry(secondBody.Name); fixedBody.Keyframes.PreserveInterpolation = true;
                foreach (int channel in new[] { 3, 4, 5 }) fixedBody.Keyframes.SetFrameValue(channel, 0, secondBody.BindState[channel], true)._tangent = 0;
                using (var snapshot = NativeAnimationOptimization.CopySource(sparse)) snapshot.ExportChecked(sparsePath);
                using (var decoded = NodeFactory.FromFile(null, sparsePath) as CHR0Node)
                {
                    Check(decoded != null && NativeAnimationOptimization.SourceContentKey(decoded) == NativeAnimationOptimization.SourceContentKey(sparse),
                        "Procedural source serialization retains the exact original 0/16 schedule and native Hermite tangents");
                    sparseResourceHash = BrawlLibrarySource.ResourceHash(decoded);
                }
            }
            var request = UseModelRegistration(args[0], sparsePath, "mario");
            string sparseId = (string)typeof(ModelForm).GetMethod("LoadSessionSource", Private, null, new[] { typeof(LibraryRegistration) }, null).Invoke(form, new object[] { request });
            await LibraryQuerySettled(form);
            var temporary = (AnimationLibrary)Field(form, "_library");
            var registeredSparse = temporary.Read().Clips.Single(clip => clip.Id == sparseId);
            Check(registeredSparse.ModelId != null && registeredSparse.ResourceSha256 == sparseResourceHash && registeredSparse.FrameCount == 17,
                "Temporary sparse source has its exact independently hashed CHR0 identity and approved authentic model association");
            var sparseItem = await UseModelSelectSource(form, temporary, "Original sparse source 0 and 16", sparseId);
            Check(ReferenceEquals(temporary, Field(form, "_sessionSourceLibrary")) && !ReferenceEquals(temporary, Field(form, "_sharedSourceLibrary")) &&
                NextLibraryFiles(shared) == sharedFiles,
                "Temporary source loading uses its isolated session catalogue and leaves shared registration unchanged");
            var sparseBefore = new GameLibraryDocument(form); int beforeSparse = project.Animations.Count;
            // Invoke the very same button action without its MessageBox wrapper
            // for the expected error. No desktop input or native message-box
            // manipulation is needed to prove the asynchronous stale guard.
            form.RetargetKeyframeReviewReady = window =>
            {
                typeof(ModelForm).GetField("_retargetKeyframeMode", Private).SetValue(form, RetargetKeyframeMode.Dense);
                SimpleControl<CheckBox>(window, "sourceKeysReviewed").Checked = true;
                SimpleControl<Button>(window, "sourceKeysUse").PerformClick();
            };
            bool stale = false;
            try { Call(form, "UseLibraryOnTarget"); }
            catch (TargetInvocationException error) { stale = error.InnerException is InvalidOperationException && error.InnerException.Message.Contains("stale result"); }
            Check(stale && project.Animations.Count == beforeSparse, "The real library action rejects a candidate when its session key policy changes during review");
            sparseBefore.CheckPreserved("stale actual Use-button action"); await SimpleControlsReady(form);
            UseModelSelectMode(form, RetargetKeyframeMode.SourceChannelTimes);
            bool sparseApproved = false;
            form.RetargetKeyframeReviewReady = window =>
            {
                sparseApproved = true; SimpleControl<CheckBox>(window, "sourceKeysReviewed").Checked = true;
                SimpleControl<Button>(window, "sourceKeysUse").PerformClick();
            };
            SimpleControl<Button>(form, "libraryUseOnTarget").PerformClick(); await SimpleControlsReady(form); form.RetargetKeyframeReviewReady = null;
            var sparseAsset = project.Animations.Last(); var sparseResult = editor.SelectedCHR0;
            var sparseTrack = (CHR0EntryNode)sparseResult.FindChild(body.Name, false);
            Check(sparseApproved && sparseAsset.Retarget.KeyframeMode == RetargetKeyframeMode.SourceChannelTimes && sparseTrack != null &&
                sparseTrack.KeyArrays[3].Entries.Select(key => key._index).SequenceEqual(new[] { 0f, 16f }),
                "Actual temporary-source Use button retains 0/16 body keys and leaves all source-unkeyed frames 1..15 interpolated");
            Check(sparseAsset.Retarget.Library.ClipId == sparseItem.Clip.Id && sparseAsset.Retarget.ReadKeyframeReport().QualityVerified,
                "The sparse temporary result has exact source identity and independently qualified native motion");
            var sparseReport = sparseAsset.Retarget.ReadKeyframeReport();
            evidence.Add(new { Scope = "Actual temporary-source Use button", Mode = sparseAsset.Retarget.KeyframeMode.ToString(), Report = sparseReport,
                Body = body.Name, SourceTimes = new[] { 0, 16 }, ResultTimes = sparseTrack.KeyArrays[3].Entries.Select(key => key._index).ToArray() });
            await UseModelRoundtrip(form, sparseAsset, output, "sparse-use-model-source-channel");

            foreach (int text in new[] { 100, 175 }) foreach (int width in new[] { 1100, 1700 })
            {
                Call(form, "SetWorkspaceTextSize", text); form.ClientSize = new Size(width, 950); await Task.Delay(70);
                var use = SimpleControl<Button>(form, "libraryUseOnTarget");
                var pane = (Control)Field(form, "_libraryPane");
                Check(use.Enabled && InClient(use, pane) && editor.ModelPanel.Visible && editor.ModelPanel.Width >= 250 && editor.ModelPanel.Height >= 200,
                    "Actual source transfer and complete target viewport stay usable at " + width + "/" + text);
                Check(use.AccessibleDescription.Contains("Source channel keys"), "Use-button accessibility explains its actual source-key policy at " + width + "/" + text);
                await CaptureSimpleWorkspace(form, Path.Combine(output, "use-model-source-target-" + width + "-" + text + ".png"));
            }
            Call(form, "SetWorkspaceTextSize", 100); form.ClientSize = new Size(1700, 950);
            UseModelSelectMode(form, RetargetKeyframeMode.Dense); int sparseReviews = 0;
            form.RetargetKeyframeReviewReady = window => { sparseReviews++; SimpleControl<Button>(window, "sourceKeysCancel").PerformClick(); };
            SimpleControl<Button>(form, "libraryUseOnTarget").PerformClick(); await SimpleControlsReady(form); form.RetargetKeyframeReviewReady = null;
            var denseAsset = project.Animations.Last(); var denseResult = editor.SelectedCHR0;
            Check(denseAsset.Retarget.KeyframeMode == RetargetKeyframeMode.Dense && sparseReviews == 0 &&
                ((CHR0EntryNode)denseResult.FindChild(body.Name, false)).KeyArrays[3].KeyCount >= 17,
                "Explicit Dense remains honored by the actual Use button and is the intentional every-frame representation");
            Check((string)Call(form, "NativeControlHash", target, ".mdl0") == modelBefore && originals.All(item => FighterProject.HashFile(item.Key) == item.Value) &&
                NextLibraryFiles(shared) == sharedFiles,
                "All Use-button modes preserve authentic target hierarchy/binds/all weights, immutable originals and shared catalogue");
            int imports = project.Animations.Count; Call(form, "UndoFighterImport"); await SimpleControlsReady(form);
            Check(project.Animations.Count == imports - 1 && project.Animations.Any(asset => asset.Id == sparseAsset.Id),
                "Use-button result import uses the existing one-transaction native project Undo without removing the earlier sparse revision");
            File.WriteAllText(Path.Combine(output, "use-model-key-evidence.json"), new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.Serialize(new {
                PrivateDesktop, GroundMode = project.GroundMode.ToString(), SourceRig = sourceFingerprint, TargetRig = targetFingerprint, Cases = evidence,
                ActualUseButton = true, TemporaryAndSharedSources = true, DenseExplicit = true, PlainExactExport = true, NativeBodyUndo = true,
                OSInputInjected = false, ScreenCopied = false, PhysicalInput = false, GameAcceptance = false,
                Scope = "Native source-key schedule and workflow regression; motion guards may add required keys for other inputs. No universal sparse-match or contact claim." }));
        }
        finally
        {
            form.RetargetKeyframeReviewReady = null;
            if (!form.IsDisposed) { Call(form, "SetWorkspaceTextSize", 100); SimpleDisposeForm(form); }
            project.Dispose(); Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", libraryBefore);
            Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", exportBefore);
        }
    }
}
