using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    // Frozen before running the existing search: 17 identical +zero-slope
    // knots are one fixed value; a line v=t/8 is exactly its endpoint Hermite
    // span with slopes 1/8; v=45*t reaches 720 degrees without normalization.
    // Equal values with alternating nonzero tangents are not a constant.
    // These are scalar identities, not a claim that native float arithmetic
    // produces bit-identical poses at every real-valued time.
    static async Task NativeExactI12WritePath(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Exact I12 write-path qualification requires the private desktop.");
        Directory.CreateDirectory(output);
        var json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024, RecursionLimit = 200 };
        var originalHashes = new[] { args[0], args[1] }.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(path => path, FighterProject.HashFile);
        string previousLibrary = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        string previousPreferences = Environment.GetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "isolated-library"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", Path.Combine(output, "isolated-export-settings"));
        try
        {
            NativeEditedDenseRetentionChecks(Path.Combine(output,"edited-dense-retention"));
            NativeAuthoredStorageChecks(Path.Combine(output,"authored-storage"));
            NativePartialUndoStorageChecks(Path.Combine(output,"partial-undo-storage"));
            NativeReadOnlySiblingAuditChecks(Path.Combine(output,"read-only-sibling-audit"));
            using (var project = FighterProject.Create("Exact I12 cleanup of the finished current animation"))
            using (var form = new ModelForm())
            {
                form.InitializeFighterProject(project); form.Show(); form.ClientSize = new Size(1600, 950);
                Call(form, "ImportFighterModel", args[0]); Call(form, "ImportFighterAnimations", new[] { new[] { args[1] } });
                await GameLibraryWait(() => form.FighterEditor.TargetModel != null &&
                    form.FighterEditor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Any(), "Exact I12 input did not open.");
                await SimpleControlsReady(form);
                var editor = form.FighterEditor;
                string requested = args.FirstOrDefault(arg => arg.StartsWith("--exact-i12-clip=", StringComparison.Ordinal));
                var clips = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().ToArray();
                var clip = requested == null ? clips.Single() : clips.Single(c => c.Name == requested.Substring("--exact-i12-clip=".Length));
                foreach (ListViewItem item in editor.leftPanel.listAnims.Items) item.Selected = ReferenceEquals(item.Tag, clip);
                editor.SelectedCHR0 = clip; await SimpleControlsReady(form);
                var controls = await Await<ControlReply>(Call(form, "LoadControls"));
                await SimpleControlsReady(form);
                var model = (MDL0Node)editor.TargetModel;
                editor.SetFrame(Math.Min(3, clip.FrameCount)); editor.FrameFighterModel(); await SimpleControlsReady(form);
                string modelPath = Path.Combine(output, "frozen-target.mdl0"); model.Export(modelPath);
                string modelHash = FighterProject.HashFile(modelPath);
                string liveModelHash = (string)Call(form, "NativeControlHash", model, ".mdl0");
                string content = NativeAnimationOptimization.SourceContentKey(clip), storage = NativeAnimationOptimization.StorageKey(clip);
                string identity = OptimizationNativeIdentity(clip), camera = CompactCamera(form);
                var selectedBone = editor.SelectedBone; int frame = editor.CurrentFrame, undo = editor._undoSaves.Count, redo = editor._redoSaves.Count;
                bool dirty = clip.IsDirty, rootDirty = clip.RootNode.IsDirty, modelDirty = model.IsDirty, projectDirty = (bool)Field(form, "_fighterDirty");
                var siblings = clips.Where(c => !ReferenceEquals(c, clip)).ToDictionary(c => c, NativeAnimationOptimization.SourceContentKey);
                void Unchanged(string phase)
                {
                    Check(ReferenceEquals(editor.TargetModel, model) && ReferenceEquals(editor.SelectedCHR0, clip) &&
                        ReferenceEquals(editor.TargetAnimation, clip) && ReferenceEquals(editor.SelectedBone, selectedBone) &&
                        NativeAnimationOptimization.SourceContentKey(clip) == content && NativeAnimationOptimization.StorageKey(clip) == storage &&
                        OptimizationNativeIdentity(clip) == identity && CompactCamera(form) == camera && editor.CurrentFrame == frame &&
                        editor._undoSaves.Count == undo && editor._redoSaves.Count == redo && clip.IsDirty == dirty &&
                        clip.RootNode.IsDirty == rootDirty && model.IsDirty == modelDirty && (bool)Field(form, "_fighterDirty") == projectDirty &&
                        siblings.All(pair => NativeAnimationOptimization.SourceContentKey(pair.Key) == pair.Value) &&
                        (string)Call(form, "NativeControlHash", model, ".mdl0") == liveModelHash && FighterProject.HashFile(modelPath) == modelHash,
                        "Exact I12 " + phase + " preserves live master, all model data/weights, siblings, selection, frame, camera, Undo/Redo and dirty state");
                }
                // Use the application's existing fingerprint/review parser. A
                // standalone resource is not evidence for absent project pins
                // or baked contacts. Unknown context therefore stays explicit.
                object[] contextArgs = { model, controls.State, false, null, null };
                bool known = (bool)typeof(ModelForm).GetMethod("ReadExportProtectionDetails", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, contextArgs);
                bool constrained = (bool)contextArgs[2];
                string[] exactBones = known ? (string[])contextArgs[3] : (controls.ProtectedMotionBones ?? new int[0]).Select(i => controls.BoneNames[i]).ToArray();
                var protection = new HashSet<string>(exactBones, StringComparer.Ordinal);
                if (!known || constrained)
                    protection.UnionWith(clip.Children.OfType<CHR0EntryNode>().Where(e => model.AllBones.Any(b => b.Name == e.Name)).Select(e => e.Name));
                using (var modelOwner = NodeFactory.FromFile(null, modelPath))
                {
                    var validationModel = (MDL0Node)modelOwner; validationModel.Populate();
                    using (var before = NativeAnimationOptimization.CopySource(clip))
                    using (var baseline = NativeAnimationOptimization.CopySource(clip))
                    {
                        string baselineContent = NativeAnimationOptimization.SourceContentKey(baseline);
                        Action current = () =>
                        {
                            if (NativeAnimationOptimization.SourceContentKey(clip) != content ||
                                NativeAnimationOptimization.StorageKey(clip) != storage ||
                                NativeAnimationOptimization.SourceContentKey(baseline) != baselineContent ||
                                FighterProject.HashFile(modelPath) != modelHash)
                                throw new InvalidOperationException("The exact cleanup master, storage or target changed.");
                        };
                        await ExactI12AnalyzeAndWrite(form, validationModel, before, baseline, modelPath, controls.State, output,
                            exactBones, protection.ToArray(), known, current, "current", receipt =>
                            {
                                receipt["ProtectionContextKnown"] = known; receipt["ConstraintsPresent"] = constrained;
                                receipt["ProtectionScope"] = !known || constrained ? "Conservative structure retention: absent/complex persisted protection context is not inferred." :
                                    "Exact reviewed source-owned controls retained; imported standalone input has no claimed project fitting/contact acceptance.";
                                receipt["InputSha256"] = FighterProject.HashFile(args[1]);
                                receipt["TargetSha256"] = modelHash;
                            });
                    }
                    Unchanged("current-animation preparation/export");
                    await ExactI12ProceduralChecks(form, validationModel, modelPath, output);
                    Unchanged("controlled synthetic preparation/export");
                }
                Check(originalHashes.All(pair => FighterProject.HashFile(pair.Key) == pair.Value), "Exact I12 qualification never changes original model or animation files");
                File.WriteAllText(Path.Combine(output, "exact-i12-scope.json"), json.Serialize(new
                {
                    Policy = "Exact", Compact = false, HighQuality = false, SmallFile = false,
                    SourceReference = "Same finished target animation; donor motion is not the compression baseline.",
                    Protections = "Source-owned control structures retained; known fixture contact dependencies retained; unknown imported context falls back conservatively.",
                    Claim = "Existing Core rational Exact cleanup plus fresh native serialization/reopen and sampled complete-model evaluation. No new reduction algorithm, compact encoding, in-game, external-editor, physical-input or continuous native geometry claim."
                }));
                SimpleDisposeForm(form);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previousLibrary);
            Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", previousPreferences);
        }
    }

    static async Task ExactI12ProceduralChecks(ModelForm form, MDL0Node model, string modelPath, string output)
    {
        var bones = model.AllBones.ToArray();
        var leaves = bones.Where(b => !b.Children.OfType<MDL0BoneNode>().Any()).ToArray();
        Check(leaves.Length >= 2, "Exact I12 fixture has two independent native leaf bindings");
        var body = leaves[leaves.Length - 1]; var contact = leaves.First(b => !ReferenceEquals(b, body));
        var root = bones.First(b => !(b.Parent is MDL0BoneNode));
        var protection = new HashSet<string>(StringComparer.Ordinal) { root.Name };
        for (var ancestor = contact; ancestor != null; ancestor = ancestor.Parent as MDL0BoneNode) protection.Add(ancestor.Name);
        Check(!protection.Contains(body.Name), "Exact I12 fixture reduces a leaf outside the explicit contact/ancestor dependency set");
        foreach (int version in new[] { 4, 5 })
        using (var fixture = OptimizationFixture(body.Name, "Independent exact I12 identities v" + version))
        {
            fixture.Version = version; fixture.FrameCount = 17; fixture.Loop = false;
            if (version == 4) fixture.UserEntries.Clear();
            var entry = (CHR0EntryNode)fixture.Children[0];
            for (int c = 0; c < 9; c++) OptimizationReplace(entry, c, new ControlKey[0]);
            ExactI12ModelFlags(entry, body);
            OptimizationReplace(entry, 0, Enumerable.Range(0, 17).Select(t => OptimizationKey(t, 1.25f, 0)).ToArray());
            OptimizationReplace(entry, 3, Enumerable.Range(0, 17).Select(t => OptimizationKey(t, 0, t == 0 || t == 16 ? 0 : t % 2 == 0 ? .125f : -.125f)).ToArray());
            OptimizationReplace(entry, 4, Enumerable.Range(0, 17).Select(t => OptimizationKey(t, 45 * t, 45)).ToArray());
            OptimizationReplace(entry, 6, Enumerable.Range(0, 17).Select(t => OptimizationKey(t, t * .125f, .125f)).ToArray());
            // Native groups encode all three component slots when present.
            // Author fixed companion slots explicitly; whole absent groups are
            // tested on the root/contact entries rather than inventing a native
            // per-axis absence representation inside a populated group.
            foreach(int c in new[]{1,2,5,7,8}) OptimizationReplace(entry,c,new[]{OptimizationKey(0,c<3?1:0,0)});
            var rootTrack = fixture.CreateEntry(root.Name); ExactI12ModelFlags(rootTrack, root);
            OptimizationReplace(rootTrack, 6, Enumerable.Range(0, 17).Select(t => OptimizationKey(t, t * .25f, .25f)).ToArray());
            foreach(int c in new[]{7,8}) OptimizationReplace(rootTrack,c,new[]{OptimizationKey(0,0,0)});
            var contactTrack = fixture.CreateEntry(contact.Name); ExactI12ModelFlags(contactTrack, contact);
            OptimizationReplace(contactTrack, 5, Enumerable.Range(0, 17).Select(t => OptimizationKey(t, t % 3 * .125f, 0)).ToArray());
            foreach(int c in new[]{3,4}) OptimizationReplace(contactTrack,c,new[]{OptimizationKey(0,0,0)});
            using (var before = NativeAnimationOptimization.CopySource(fixture))
            using (var baseline = NativeAnimationOptimization.CopySource(fixture))
            {
                string stage = Path.Combine(output, "procedural-v" + version); Directory.CreateDirectory(stage);
                string content = NativeAnimationOptimization.SourceContentKey(baseline);
                await ExactI12AnalyzeAndWrite(form, model, before, baseline, modelPath, null, stage, new[] { root.Name },
                    protection.ToArray(), true, () =>
                    {
                        if (NativeAnimationOptimization.SourceContentKey(baseline) != content) throw new InvalidOperationException("Frozen fixture changed.");
                    }, "controlled-fixture", receipt =>
                    {
                        receipt["IndependentOracle"] = "17 constant keys -> 1; exact line and 720-degree turn -> 2 endpoints with original slopes; sparse channels remain absent; equal values/nonzero alternating slopes retain motion.";
                        receipt["ProtectionContext"] = "Controlled fixture: explicit root and contact leaf/ancestors, no unlisted authored constraints. Not inferred from game/bone names.";
                    }, decoded =>
                    {
                        var result = (CHR0EntryNode)decoded.FindChild(body.Name, false);
                        Check(result.KeyArrays[0].KeyCount == 1 && result.KeyArrays[0].Entries.Single()._index == 0 &&
                            ReplayFloatBits(result.KeyArrays[0].Entries.Single()._value) == ReplayFloatBits(1.25f), "Exact I12 v" + version + ": 17 genuine constants become first-frame fixed storage");
                        Check(new[] { 4, 6 }.All(c => result.KeyArrays[c].KeyCount == 2 && result.KeyArrays[c].Entries.Select(k => k._index).SequenceEqual(new[] { 0f, 16f })),
                            "Exact I12 v" + version + ": exact redundant line and full-turn channels retain only original endpoints");
                        Check(result.KeyArrays[4].Entries.All(k => k._tangent == 45) && result.KeyArrays[4].Entries.Last()._value == 720 &&
                            result.KeyArrays[6].Entries.All(k => k._tangent == .125f), "Exact I12 v" + version + ": original endpoint tangents and 720-degree winding remain authored");
                        Check(result.KeyArrays[3].KeyCount > 1 && Math.Abs(result.GetFrameValue(3, 1.5f)) > .001f,
                            "Exact I12 v" + version + ": equal values with nonzero tangents retain fractional motion");
                        var decodedRoot=(CHR0EntryNode)decoded.FindChild(root.Name,false);
                        var decodedContact=(CHR0EntryNode)decoded.FindChild(contact.Name,false);
                        Check(Enumerable.Range(0,6).All(c=>decodedRoot.KeyArrays[c].KeyCount==0)&&
                            new[]{0,1,2,6,7,8}.All(c=>decodedContact.KeyArrays[c].KeyCount==0),
                            "Exact I12 v"+version+": absent native groups remain absent without materializing companion channels");
                        ExactI12SameFlags(entry, result, "Exact I12 v" + version + ": native transform/compensation flags survive the exact cleanup");
                        SourceKeysSameTrack(rootTrack, (CHR0EntryNode)decoded.FindChild(root.Name, false), "Exact I12 v" + version + ": source-owned motion raw keys/tangents remain exact");
                        SourceKeysSameTrack(contactTrack, (CHR0EntryNode)decoded.FindChild(contact.Name, false), "Exact I12 v" + version + ": explicit contact dependency raw keys/tangents remain exact");
                    });
                Check(NativeAnimationOptimization.SourceContentKey(baseline) == content && NativeAnimationOptimization.SourceContentKey(fixture) == content,
                    "Exact I12 v" + version + ": baseline and original controlled fixture remain immutable");
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel(); bool cancelled = false;
                    try { using (var ignored = (IDisposable)await ExactI12Invoke(form, model, before, baseline, modelPath, null, stage, new[] { root.Name }, protection.ToArray(), true, () => { }, cancellation.Token)) { } }
                    catch (OperationCanceledException) { cancelled = true; }
                    Check(cancelled && NativeAnimationOptimization.SourceContentKey(baseline) == content, "Exact I12 v" + version + ": cancellation cannot publish or change the master");
                }
                bool stale = false;
                try { using (var ignored = (IDisposable)await ExactI12Invoke(form, model, before, baseline, modelPath, null, stage, new[] { root.Name }, protection.ToArray(), true,
                    () => { throw new InvalidOperationException("Frozen exact fixture dependency changed."); }, CancellationToken.None)) { } }
                catch (InvalidOperationException error) { stale = error.Message.Contains("Frozen exact fixture dependency changed"); }
                Check(stale && NativeAnimationOptimization.SourceContentKey(baseline) == content, "Exact I12 v" + version + ": stale preparation is rejected without touching native data");
            }
        }
    }

    static async Task<object> ExactI12Invoke(ModelForm form, MDL0Node model, CHR0Node before, CHR0Node baseline, string modelPath,
        string controls, string stage, string[] exact, string[] protection, bool known, Action current, CancellationToken token)
    {
        // Invoke the existing offline application/Core/native pipeline. Keeping
        // all three switches false excludes curve approximation and new codec
        // selection, while untouched imported plans may be retained unchanged.
        var task = (Task)Call(form, "PrepareFrozenOptimization", model, before, baseline, modelPath, controls, stage,
            exact, protection, new int[0], new TransitionProtected[0], known, false, false, false, null, null, current, token, null, null, null);
        await task;
        return task.GetType().GetProperty("Result").GetValue(task, null);
    }

    static async Task ExactI12AnalyzeAndWrite(ModelForm form, MDL0Node model, CHR0Node before, CHR0Node baseline, string modelPath,
        string controls, string stage, string[] exact, string[] protection, bool known, Action current, string scope,
        Action<Dictionary<string, object>> addReceipt, Action<CHR0Node> inspect = null)
    {
        Directory.CreateDirectory(stage);
        string original = NativeAnimationOptimization.SourceContentKey(baseline);
        string referencePath = Path.Combine(stage, "finished-target-reference.chr0"), resultPath = Path.Combine(stage, "exact-i12-cleaned.chr0");
        SourceKeyArtifactExport(before, referencePath);
        var timer = Stopwatch.StartNew();
        var analysis = await ExactI12Invoke(form, model, before, baseline, modelPath, controls, stage, exact, protection, known, current, CancellationToken.None);
        using ((IDisposable)analysis)
        {
            var choice = (NativeAnimationOptimizationChoice)Field(analysis, "Choice");
            var result = choice.Result;
            using (var fallback = result?.Candidate == null ? NativeAnimationOptimization.CopySource(before) : null)
            {
                var chosen = result?.Candidate ?? fallback;
                Check(!choice.HighQuality && (result == null || !result.UsedApproximateStorage), "Exact I12 " + scope + ": no approximate curve or compact-codec search is admitted");
                SourceKeyArtifactExport(chosen, resultPath);
                timer.Stop();
                using (var reopened = NodeFactory.FromFile(null, resultPath) as CHR0Node)
                {
                    Check(reopened != null && NativeAnimationOptimization.SourceContentKey(reopened) == NativeAnimationOptimization.SourceContentKey(chosen),
                        "Exact I12 " + scope + ": complete result metadata/flags/curves reopen in a fresh native resource");
                    inspect?.Invoke(reopened);
                    foreach (string bone in exact.Concat(protection).Distinct(StringComparer.Ordinal))
                    {
                        var old = (CHR0EntryNode)baseline.FindChild(bone, false); var currentTrack = (CHR0EntryNode)reopened.FindChild(bone, false);
                        if (old == null) { Check(currentTrack == null, "Exact I12 " + scope + ": absent protected track stays absent / " + bone); continue; }
                        if (protection.Contains(bone)) SourceKeysSameTrack(old, currentTrack, "Exact I12 " + scope + ": protected track is unchanged / " + bone);
                        ExactI12SameFlags(old, currentTrack, "Exact I12 " + scope + ": protected native flags remain / " + bone);
                    }
                    var referenceSize = NativeChr0SizeReport.Read(referencePath); var resultSize = NativeChr0SizeReport.Read(resultPath);
                    Check(resultSize.FileBytes <= referenceSize.FileBytes && resultSize.DeclaredResourceBytes <= referenceSize.DeclaredResourceBytes &&
                        referenceSize.AccountedBytes == referenceSize.FileBytes && resultSize.AccountedBytes == resultSize.FileBytes,
                        "Exact I12 " + scope + ": actual standalone and resource-block sizes never grow and every physical byte is accounted");
                    Check(resultSize.Streams.Where(p => !referenceSize.Streams.ContainsKey(p.Key)).All(p => p.Key == "I12" || p.Key == "fixed"),
                        "Exact I12 " + scope + ": no new packed or sampled stream format is introduced");
                    var measured = SourceKeyQualityMeasure(model, baseline, reopened);
                    string savedAgain = Path.Combine(stage, "exact-i12-cleaned-resaved.chr0"); SourceKeyArtifactExport(reopened, savedAgain);
                    using (var resaved = NodeFactory.FromFile(null, savedAgain) as CHR0Node)
                        Check(resaved != null && NativeAnimationOptimization.SourceContentKey(resaved) == NativeAnimationOptimization.SourceContentKey(reopened),
                            "Exact I12 " + scope + ": export/reopen/save/reopen retains complete decoded native content");
                    var receipt = new Dictionary<string, object>
                    {
                        ["Policy"] = "Exact / existing Core rational Hermite identity", ["CompactSearch"] = false,
                        ["Approximation"] = false, ["Reference"] = "Same finished target master", ["InputContent"] = original,
                        ["OutputContent"] = NativeAnimationOptimization.SourceContentKey(reopened),
                        ["InputBytes"] = referenceSize.FileBytes, ["OutputBytes"] = resultSize.FileBytes,
                        ["SavedBytes"] = referenceSize.FileBytes - resultSize.FileBytes,
                        ["InputByteAccounting"] = referenceSize, ["OutputByteAccounting"] = resultSize,
                        ["InputKeys"] = baseline.Children.OfType<CHR0EntryNode>().Sum(e => e.KeyArrays.Sum(a => a.KeyCount)),
                        ["OutputKeys"] = reopened.Children.OfType<CHR0EntryNode>().Sum(e => e.KeyArrays.Sum(a => a.KeyCount)),
                        ["SourceOwnedExactBones"] = exact, ["StructureProtectedBones"] = protection,
                        ["OutputSha256"] = FighterProject.HashFile(resultPath), ["ResavedSha256"] = FighterProject.HashFile(savedAgain),
                        ["ResavedBytes"] = new FileInfo(savedAgain).Length, ["ReopenResaveDecodedContentEqual"] = true,
                        ["PreparationMilliseconds"] = timer.ElapsedMilliseconds,
                        ["CoreWork"] = Field(analysis, "Work"), ["CoreCandidateSpans"] = Field(analysis, "Spans"),
                        ["CoreReport"] = Field(analysis, "Output"), ["ChoiceReport"] = choice.Report,
                        ["NativeReport"] = result?.Report ?? "No smaller complete verified exact candidate. Original master retained.",
                        ["NativeResult"] = result == null ? null : ExactI12NativeReceipt(result), ["IndependentWithheldSamples"] = measured,
                        ["Coverage"] = "Existing continuous exact scalar proof; production fresh serialization/reopen plus integer/quarter/withheld/adaptive native pose/all-skin checks. Extra independent withheld native samples are finite and do not prove continuous geometry equivalence.",
                        ["Limitations"] = "No compact-format search, runtime/editor compatibility, container-byte saving, gameplay contact acceptance or human-input claim. Exact scalar identity does not imply bit-identical floating-point evaluation at every time."
                    };
                    addReceipt(receipt);
                    File.WriteAllText(Path.Combine(stage, "exact-i12-receipt.json"), new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024, RecursionLimit = 200 }.Serialize(receipt));
                    Check(NativeAnimationOptimization.SourceContentKey(baseline) == original, "Exact I12 " + scope + ": serialized analysis preserves its frozen master");
                }
            }
        }
    }

    static object ExactI12NativeReceipt(NativeAnimationOptimizationResult r) => new
    {
        r.BeforeBytes, r.AfterBytes, r.BeforeResourceBytes, r.CandidateResourceBytes, r.BeforeKeys, r.AfterKeys, r.BeforeTracks, r.AfterTracks,
        r.MaximumEulerError, r.MaximumOrientationError, r.MaximumScaleError, r.MaximumLocalTranslationError, r.MaximumWorldBoneError,
        r.MaximumWorldAxisAngleError, r.MaximumSkinError, r.MaximumEulerDerivativeError, r.MaximumOrientationDerivativeError,
        r.MaximumScaleDerivativeError, r.MaximumTranslationDerivativeError, r.MaximumWorldDerivativeError, r.MaximumSkinDerivativeError,
        r.Samples, r.BudgetSamples, r.BudgetPoseEvaluations, r.BudgetBoneVisits, r.BudgetVertexVisits
    };

    static void ExactI12ModelFlags(CHR0EntryNode entry, MDL0BoneNode bone)
    {
        entry.Keyframes.PreserveInterpolation = true;
        entry.ScaleCompensateApply = bone.SegScaleCompApply; entry.ScaleCompensateParent = bone.SegScaleCompParent; entry.ClassicScaleOff = !bone.ClassicScale;
    }

    static void ExactI12SameFlags(CHR0EntryNode a, CHR0EntryNode b, string message) => Check(a != null && b != null &&
        a.ScaleCompensateApply == b.ScaleCompensateApply && a.ScaleCompensateParent == b.ScaleCompensateParent && a.ClassicScaleOff == b.ClassicScaleOff &&
        a.UseModelScale == b.UseModelScale && a.UseModelRotate == b.UseModelRotate && a.UseModelTranslate == b.UseModelTranslate, message);
}
