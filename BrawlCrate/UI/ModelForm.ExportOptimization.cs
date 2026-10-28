using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private sealed class FrozenExportOptimizationContext
        {
            internal MDL0Node Target;
            internal string Controls, ModelContent, Review;
            internal string[] ExactBones;
            internal string[] ProtectedBones;
            internal int[] MandatoryFrames;
            internal TransitionProtected[] ProtectedRanges;
            internal bool ProtectionKnown;
            internal string BoundedUnavailable;
            internal string ReferenceOnlyReason;
            internal string SourceMotionBone;
            internal NativeTransitionFit PlainFit;
            internal bool ConstraintsAbsent;
            internal NativeGeneratedPairSite[] GeneratedPairSites;
            internal string GeneratedPairReport;
            internal Action Validate;
        }
        private Chr0ExportSnapshot _activeSingleExport;

        // The legacy model hash serializes native nodes. Reject an unsaved
        // model before any hash/snapshot/status query can rebuild the live tree
        // and clear its dirty flags. Unsaved animation curves remain supported.
        private static void RequireSavedAnimationExportTarget(MDL0Node target)
        {
            if (target?.IsDirty == true)
                throw new InvalidOperationException("Save target model changes before optimizing/exporting animation; animation edits may remain unsaved.");
        }

        private static bool ReadExportProtectionContext(MDL0Node model, string controls, out bool constraints, out string[] exactBones)
            => ReadExportProtectionDetails(model, controls, out constraints, out exactBones, out _);

        private static bool ReadExportProtectionDetails(MDL0Node model, string controls, out bool constraints, out string[] exactBones, out string sourceMotionBone)
        {
            constraints = false; exactBones = new string[0]; sourceMotionBone = null;
            if (string.IsNullOrWhiteSpace(controls)) return false;
            Dictionary<string, object> Object(object value) => value is Dictionary<string, object> item
                ? new Dictionary<string, object>(item, StringComparer.OrdinalIgnoreCase) : null;
            object Value(Dictionary<string, object> item, string name) => item != null && item.TryGetValue(name, out var value) ? value : null;
            bool Collection(Dictionary<string, object> item, string name, bool allowNull, out object[] entries)
            {
                entries = null;
                if (item == null || !item.TryGetValue(name, out var value)) return false;
                if (value == null) { entries = new object[0]; return allowNull; }
                if (!(value is IEnumerable sequence) || value is string || value is IDictionary) return false;
                entries = sequence.Cast<object>().ToArray(); return true;
            }
            bool Index(object value, int count, out int index)
            {
                index = -1;
                if (!(value is byte || value is short || value is int || value is long || value is double || value is decimal)) return false;
                double number = Convert.ToDouble(value);
                if (double.IsNaN(number) || double.IsInfinity(number) || number != Math.Floor(number) || number < 0 || number >= count) return false;
                index = (int)number; return true;
            }
            try
            {
                var state = Object(new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 200 }.DeserializeObject(controls));
                var setup = Object(Value(state, "Setup"));
                string fingerprint = EngineRigReview.FingerprintOf(model);
                if (!string.Equals(Convert.ToString(Value(state, "Fingerprint")), fingerprint, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Convert.ToString(Value(setup, "SkeletonFingerprint")), fingerprint, StringComparison.OrdinalIgnoreCase) ||
                    !Guid.TryParse(Convert.ToString(Value(state, "Clip")), out var clipId) || clipId == Guid.Empty) return false;
                if (!Collection(state, "BonePins", true, out var pins) || !Collection(state, "Contacts", false, out var contacts) ||
                    !Collection(state, "AuthoringContacts", false, out var authored) || !Collection(setup, "BoneRoles", false, out var roles)) return false;
                constraints = pins.Length > 0 || contacts.Length > 0 || authored.Length > 0;
                var bones = new List<MDL0BoneNode>(); Action<MDL0BoneNode> add = null;
                add = bone => { if (bones.Contains(bone)) return; if (bone.Parent is MDL0BoneNode parent) add(parent); bones.Add(bone); };
                foreach (var bone in model.AllBones) add(bone);
                var reviewed = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var roleValue in roles)
                {
                    var role = Object(roleValue);
                    if (!Index(Value(role, "Bone"), bones.Count, out int index)) return false;
                    string semantic = Convert.ToString(Value(role, "SemanticRole"));
                    if (!(Value(role, "Reviewed") is bool approved) || !approved) continue;
                    if (string.IsNullOrWhiteSpace(semantic) || string.IsNullOrWhiteSpace(Convert.ToString(Value(role, "Evidence"))) || reviewed.ContainsKey(semantic)) return false;
                    reviewed.Add(semantic, index);
                }
                if (!reviewed.TryGetValue("Hips", out int body)) return false;
                if (Value(setup, "BodyBone") != null && (!Index(Value(setup, "BodyBone"), bones.Count, out int explicitBody) || body != explicitBody)) return false;
                if (Collection(setup, "UnassignedBoneRoles", true, out var unassigned) && unassigned.Any(role => Convert.ToString(role) == "Hips")) return false;
                var exact = new HashSet<string>(StringComparer.Ordinal);
                for (var bone = bones[body].Parent as MDL0BoneNode; bone != null; bone = bone.Parent as MDL0BoneNode) exact.Add(bone.Name);
                if (exact.Count > 0 && !reviewed.ContainsKey("RootMotion")) return false;
                foreach (string role in new[] { "SceneRoot", "RootMotion", "RootOrientation", "BodyOrientation" })
                    if (reviewed.TryGetValue(role, out int index)) exact.Add(bones[index].Name);
                if (reviewed.TryGetValue("RootMotion", out int sourceMotion)) sourceMotionBone = bones[sourceMotion].Name;
                exactBones = bones.Where(b => exact.Contains(b.Name)).Select(b => b.Name).ToArray(); return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is InvalidDataException || error is OverflowException)
            { return false; }
        }

        private string ExportControlState(FighterAsset asset, CHR0Node clip)
        {
            string prefix = (_fighterProject.Model?.Id ?? "model") + "|" + (_fighterProject.ModelNodePath ?? "") + "|";
            string key = prefix + FighterAnimationKey(asset, clip);
            var exact = _fighterProject.AnimationControls.FirstOrDefault(s => s.Key == key);
            if (exact != null) return exact.Json;
            // Legacy identities are accepted only for a unique exact member.
            if (DiscoverFighterAnimations(clip.RootNode).Count(n => n.TreePath == clip.TreePath) != 1) return null;
            return _fighterProject.AnimationControls.FirstOrDefault(s => s.Key == prefix + asset.Id + "|" + clip.TreePath)?.Json;
        }

        private static string[] ExportNativeSemanticLimitations(MDL0Node model, CHR0Node clip)
        {
            var reasons = new List<string>();
            var bones = model.AllBones.GroupBy(b => b.Name, StringComparer.Ordinal).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);
            var tracks = clip.Children.OfType<CHR0EntryNode>().GroupBy(e => e.Name, StringComparer.Ordinal).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);
            foreach (var entry in clip.Children.OfType<CHR0EntryNode>())
            {
                if (!bones.TryGetValue(entry.Name, out var bone)) continue;
                if (entry.ScaleCompensateApply != bone.SegScaleCompApply || entry.ScaleCompensateParent != bone.SegScaleCompParent || entry.ClassicScaleOff != !bone.ClassicScale)
                    reasons.Add("animation/model scale-compensation flags differ; the native viewport is not an independent runtime-flags evaluator");
                if (entry.UseModelScale && entry.KeyArrays.Take(3).Any(a => a.KeyCount > 0) || entry.UseModelRotate && entry.KeyArrays.Skip(3).Take(3).Any(a => a.KeyCount > 0) ||
                    entry.UseModelTranslate && entry.KeyArrays.Skip(6).Take(3).Any(a => a.KeyCount > 0))
                    reasons.Add("Use model S/R/T flags coexist with authored keys; native viewport/runtime flag equivalence is unqualified");
            }
            bool NonUnit(MDL0BoneNode bone) => Enumerable.Range(0, 3).Any(c => bone.BindState[c] != 1) ||
                tracks.TryGetValue(bone.Name, out var entry) && entry.KeyArrays.Take(3).Any(a => a.Entries.Any(k => k._value != 1 || k._tangent != 0));
            bool Compensation(MDL0BoneNode bone) => bone.SegScaleCompApply || bone.SegScaleCompParent || !bone.ClassicScale ||
                tracks.TryGetValue(bone.Name, out var entry) && (entry.ScaleCompensateApply || entry.ScaleCompensateParent || entry.ClassicScaleOff);
            foreach (var leaf in model.AllBones)
            {
                bool nonUnit = false, compensation = false;
                for (var bone = leaf; bone != null; bone = bone.Parent as MDL0BoneNode) { nonUnit |= NonUnit(bone); compensation |= Compensation(bone); }
                if (nonUnit && compensation) { reasons.Add("non-unit bind/animation scale and scale-compensation flags share a hierarchy path; bounded runtime world/skin acceptance is unqualified"); break; }
            }
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private FrozenExportOptimizationContext FreezeExportOptimizationContext(MDL0Node model, FighterAsset asset, CHR0Node clip)
        {
            RequireSavedAnimationExportTarget(model);
            var json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 200 };
            string controls = ExportControlState(asset, clip), modelContent = NativeControlHash(model, ".mdl0");
            string Review() => json.Serialize(ReviewedFighterCoordinates(model));
            string review = Review();
            bool known = ReadExportProtectionDetails(model, controls, out bool constrained, out var exactBones, out string sourceMotionBone);
            var reasons = new List<string>();
            reasons.AddRange(ExportNativeSemanticLimitations(model, clip));
            if (!known) reasons.Add("complete persisted controls, exact native fingerprints and reviewed body/motion roles are unavailable");
            // Absolute pin/contact acceptance is distinct from small relative
            // curve error. Until replayed independently, keep the reference exact.
            if (constrained) reasons.Add("authored pin/contact absolute acceptance has not been independently revalidated for compressed export");
            string referenceOnly = null; var protectedBones = new string[0];
            bool bakedSupport = NativeTransitionContacts.HasBakedSupport(asset);
            if (bakedSupport)
            {
                reasons.Add("baked support contact absolute acceptance has not been independently revalidated for compressed export");
                try { protectedBones = NativeTransitionContacts.Resolve(model, asset, true); }
                catch (Exception error) when (error is InvalidDataException || error is InvalidOperationException || error is ArgumentException || error is NotSupportedException)
                { referenceOnly = "Complete native support dependencies could not be verified: " + error.Message; }
            }
            var mandatory = new HashSet<int>(); var ranges = new TransitionProtected[0]; NativeTransitionFit plainFit = null;
            if (TransitionRecipeFor(asset, clip) is NativeTransitionFit fit)
            {
                fit.Validate(); var options = json.Deserialize<TransitionFitOptions>(fit.Options);
                if (options.Entry != null) { mandatory.Add(0); mandatory.Add(options.EntrySamples - 1); }
                if (options.Exit != null) { mandatory.Add(clip.FrameCount - options.ExitSamples); mandatory.Add(clip.FrameCount - 1); }
                ranges = options.Protected ?? new TransitionProtected[0];
                if (options.ExitContactRecovery || options.BoundaryContactRecovery || options.PreventFootDipping || options.FloorReviewed)
                    reasons.Add("fit/contact/floor absolute acceptance has not been independently revalidated for compressed export");
                else plainFit = new NativeTransitionFit { Algorithm = fit.Algorithm, ModelSha256 = fit.ModelSha256, Options = fit.Options, Receipt = fit.Receipt, InitialResultSha256 = fit.InitialResultSha256,
                    Inputs = fit.Inputs.Select(input => new TransitionNativeInput { Role = input.Role, AssetId = input.AssetId, Entry = input.Entry, Sha256 = input.Sha256,
                        Snapshot = input.Snapshot, Name = input.Name, Samples = input.Samples }).ToArray() };
            }
            var generatedPairs=NativeGeneratedPairRegistry.Session.Freeze(clip,NativeGeneratedPairRegistry.TryRigKey(model),
                exactBones.Concat(protectedBones),mandatory,out string generatedPairReport);
            return new FrozenExportOptimizationContext { Target = model, Controls = controls, ModelContent = modelContent, Review = review,
                ExactBones = exactBones, ProtectedBones = protectedBones, MandatoryFrames = mandatory.OrderBy(f => f).ToArray(),
                ProtectedRanges = ranges, ProtectionKnown = known, BoundedUnavailable = string.Join("; ", reasons.Distinct()), ReferenceOnlyReason = referenceOnly, Validate = () => {
                    RequireSavedAnimationExportTarget(model);
                    if (NativeControlHash(model, ".mdl0") != modelContent || ExportControlState(asset, clip) != controls || Review() != review || _curvePending)
                        throw new InvalidOperationException("Export target content, controls or reviewed protections changed. Open Export again.");
                }, SourceMotionBone = sourceMotionBone, PlainFit = plainFit, ConstraintsAbsent = known && !constrained && !bakedSupport,
                GeneratedPairSites=generatedPairs,GeneratedPairReport=generatedPairReport };
        }

        // One backend for explicit current-clip and reviewed-set exports. No
        // selected animation is swapped and the editable master is never written.
        private async Task PrepareOptimizedExportSnapshot(Chr0ExportSnapshot frozen, NativeExportOptimizationSettings requested,
            CancellationToken token, IProgress<string> progress = null)
        {
            var settings = (requested ?? new NativeExportOptimizationSettings()).Clone(); settings.Validate();
            token.ThrowIfCancellationRequested(); frozen.Validate();
            if (ExportContent(frozen.Master) != frozen.MasterContent) throw new InvalidDataException("The frozen finished-target master changed. No export copy was prepared.");
            if (frozen.PreparedPolicyKey == settings.StableKey) return;
            var preparationTime = System.Diagnostics.Stopwatch.StartNew();
            var context = frozen.Optimization;
            string stage = Path.Combine(_controlStage, "export-optimization-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            string referencePath = Path.Combine(stage, "finished-target-master.chr0");
            frozen.Master.ExportChecked(referencePath);
            using (var reference = NodeFactory.FromFile(null, referencePath) as CHR0Node)
                if (reference == null || ExportContent(reference) != frozen.MasterContent) throw new InvalidDataException("The full-quality target reference did not independently reopen exactly. No output was published.");
            var masterBytes = NativeChr0SizeReport.Read(referencePath);
            frozen.MasterBytes = masterBytes.FileBytes;
            var reasons = new List<string>();
            bool bounded = settings.Policy == NativeExportOptimizationPolicy.ErrorBounded;
            if (bounded && !string.IsNullOrWhiteSpace(context.BoundedUnavailable)) { reasons.Add(context.BoundedUnavailable); bounded = false; }
            var effective = settings.Clone(); if (!bounded) effective.Policy = NativeExportOptimizationPolicy.Exact;
            CHR0Node candidate = null;
            string report = "Policy requested: " + settings.Policy + "; effective: " + effective.Policy + ". Search: " + settings.SearchEffort + ".";
            report += "\r\n" + context.GeneratedPairReport;
            report += "\r\nFrozen cumulative ceilings: raw Euler/local orientation " + settings.RotationDegrees.ToString("G", System.Globalization.CultureInfo.InvariantCulture) + "°, translation " + settings.Translation.ToString("G", System.Globalization.CultureInfo.InvariantCulture) +
                ", scale " + settings.Scale.ToString("G", System.Globalization.CultureInfo.InvariantCulture) + ", full native bone/skin " + settings.GeometryRelative.ToString("G", System.Globalization.CultureInfo.InvariantCulture) +
                " × target bind diagonal. Derivative/frame: rotation " + settings.RotationDerivative.ToString("G", System.Globalization.CultureInfo.InvariantCulture) + ", translation " + settings.TranslationDerivative.ToString("G", System.Globalization.CultureInfo.InvariantCulture) +
                ", scale " + settings.ScaleDerivative.ToString("G", System.Globalization.CultureInfo.InvariantCulture) + ". Exact protections and mandatory fitting boundaries override approximation.";
            if (reasons.Count > 0) report += "\r\nExact fallback: " + string.Join("; ", reasons) + ".";
            bool supported = context.ReferenceOnlyReason == null && (frozen.Master.Version == 4 || frozen.Master.Version == 5) && NativeAnimationOptimization.SourceScalingRule(frozen.Master) == 0 &&
                context.Target.AllBones.Count <= 128 && frozen.Master.Children.OfType<CHR0EntryNode>().Sum(e => e.KeyArrays.Sum(c => (long)c.KeyCount)) <= 2000000;
            try
            {
                if (supported)
                {
                    progress?.Report("Analyzing independent export copy: " + frozen.Clip.Name + " (" + effective.Policy + ")");
                    RequireSavedAnimationExportTarget(context.Target);
                    string modelPath = Path.Combine(stage, "target.mdl0"); context.Target.Export(modelPath);
                    string frozenModelHash = FighterProject.HashFile(modelPath);
                    Action current = () => {
                        frozen.Validate();
                        if (ExportContent(frozen.Master) != frozen.MasterContent || FighterProject.HashFile(modelPath) != frozenModelHash)
                            throw new InvalidOperationException("Frozen export master or target snapshot changed. Prepare the export again.");
                    };
                    using (var modelRoot = NodeFactory.FromFile(null, modelPath))
                    {
                        var validationModel = modelRoot as MDL0Node ?? throw new InvalidDataException("The independent export target could not be reopened.");
                        if (NativeControlHash(validationModel, ".mdl0") != context.ModelContent)
                            throw new InvalidDataException("The independent export target differs from its frozen native geometry. Reference output is retained.");
                        if (bounded && !(NativeAnimationOptimization.GetBindBoundsDiagonal(validationModel) > 0))
                        { bounded = false; effective.Policy = NativeExportOptimizationPolicy.Exact; report += "\r\nExact fallback: finite positive target bind geometry is unavailable."; }
                        using (var fitAcceptance = bounded && context.PlainFit != null ? await Task.Run(() => NativeExportFitAcceptance.Prepare(modelPath, frozen.Master, context.PlainFit,
                            context.ExactBones, context.SourceMotionBone, context.ProtectionKnown, context.ConstraintsAbsent, stage, token), token) : null)
                        {
                        if (fitAcceptance != null && !fitAcceptance.Eligible)
                        {
                            bounded = false; effective.Policy = NativeExportOptimizationPolicy.Exact;
                            report += "\r\nExact/reference fallback: " + fitAcceptance.Reason;
                        }
                        else
                        {
                        if (fitAcceptance != null) report += "\r\n" + fitAcceptance.Report;
                        var protectedBones = fitAcceptance == null ? context.ProtectedBones : context.ProtectedBones.Concat(context.ExactBones).Distinct(StringComparer.Ordinal).ToArray();
                        using (var before = NativeAnimationOptimization.CopySource(frozen.Master))
                        using (var baseline = NativeAnimationOptimization.CopySource(frozen.Master))
                        using (var analysis = await PrepareFrozenOptimization(validationModel, before, baseline, modelPath, context.Controls, stage,
                            context.ExactBones, protectedBones, context.MandatoryFrames, context.ProtectedRanges, context.ProtectionKnown,
                            bounded, bounded, true, null, null, current, token, generatedPairSites:context.GeneratedPairSites,exportSettings: effective))
                        {
                            report += "\r\n" + analysis.Choice.Report;
                            var result = analysis.Choice.Result;
                            if (result?.Candidate != null)
                            {
                                candidate = result.Candidate; result.Candidate = null;
                                effective.Policy = analysis.Choice.HighQuality ? NativeExportOptimizationPolicy.ErrorBounded : NativeExportOptimizationPolicy.Exact;
                                report += "\r\n" + result.Report;
                                if (fitAcceptance != null && !fitAcceptance.CheckCandidate(candidate, token, out string reason))
                                {
                                    candidate.Dispose(); candidate = null; effective.Policy = NativeExportOptimizationPolicy.Exact;
                                    report += "\r\nExact/reference fallback: " + reason;
                                }
                            }
                        }
                        }
                        }
                    }
                }
                else report += "\r\nReference fallback: " + (context.ReferenceOnlyReason ?? "this resource exceeds the qualified CHR0 v4/v5, scaling-rule-zero, 128-bone or two-million-key optimization adapter. Complete native content is retained.");
            }
            catch (Exception error) when (!(error is OperationCanceledException) && (error is InvalidDataException || error is NotSupportedException || error is InvalidOperationException || error is IOException))
            {
                candidate?.Dispose(); candidate = null;
                token.ThrowIfCancellationRequested(); frozen.Validate();
                report += "\r\nReference fallback: optimization was not fully qualified: " + error.Message;
            }
            catch { candidate?.Dispose(); throw; }
            try
            {
                token.ThrowIfCancellationRequested(); frozen.Validate();
                if (candidate == null) { candidate = NativeAnimationOptimization.CopySource(frozen.Master); effective.Policy = NativeExportOptimizationPolicy.Exact; }
                string outputPath = Path.Combine(stage, "verified-export-copy.chr0"); candidate.ExportChecked(outputPath);
                using (var decoded = NodeFactory.FromFile(null, outputPath) as CHR0Node)
                    if (decoded == null || ExportContent(decoded) != ExportContent(candidate)) throw new InvalidDataException("Optimized export copy did not independently reopen exactly. No output was published.");
                var actual = NativeChr0SizeReport.Read(outputPath);
                string qualifiedArtifact = outputPath;
                if (actual.FileBytes > masterBytes.FileBytes)
                {
                    candidate.Dispose(); candidate = NativeAnimationOptimization.CopySource(frozen.Master); actual = masterBytes;
                    qualifiedArtifact = referencePath;
                    effective.Policy = NativeExportOptimizationPolicy.Exact;
                    report += "\r\nReference fallback: the fully encoded candidate was larger than the same finished target master.";
                }
                token.ThrowIfCancellationRequested(); frozen.Validate();
                string encodedHash = FighterProject.HashFile(qualifiedArtifact);
                if (new FileInfo(qualifiedArtifact).Length != actual.FileBytes) throw new InvalidDataException("The prepared export artifact changed during qualification. No output was published.");
                if (!ReferenceEquals(frozen.Candidate, frozen.Master)) frozen.Candidate.Dispose();
                frozen.Candidate = candidate; candidate = null;
                frozen.ExpectedContent = ExportContent(frozen.Candidate); frozen.PreparedPolicyKey = settings.StableKey;
                frozen.RequestedExportPolicy = settings.Policy;
                frozen.EffectiveExportPolicy = effective.Policy.ToString();
                frozen.ExportBytes = actual.FileBytes; frozen.ByteReport = actual.Describe();
                frozen.PreparedEncodedSha256 = encodedHash; frozen.PreparedEncodedBytes = actual.FileBytes;
                preparationTime.Stop(); frozen.PreparationMilliseconds = preparationTime.ElapsedMilliseconds;
                frozen.OptimizationReport = report + "\r\nPublished-copy policy: " + effective.Policy + ".\r\nFinished target master " + masterBytes.FileBytes + " → export copy " + actual.FileBytes + " bytes.\r\n" + actual.Describe() +
                    "\r\nPreparation wall time: " + frozen.PreparationMilliseconds + " ms, including search, encoding and independent validation. Cached unchanged exports retain this duration." +
                    "\r\nMaster dependency content and export-candidate content are separate identities. Original Next/fitting provenance remains historical evidence. Continuous scalar bounds and sampled full native geometry are separate measurements.";
                progress?.Report(frozen.OptimizationReport);
            }
            finally { candidate?.Dispose(); }
        }

        private WorkspaceExportOptimizationOptions AttachExportOptimizationOptions(WorkspaceFileDialog dialog)
        {
            var preferences = new NativeExportOptimizationPreferences();
            var options = new WorkspaceExportOptimizationOptions(preferences.Read()) { TabIndex = 1 };
            var layout = dialog.Controls.OfType<TableLayoutPanel>().Single();
            foreach (Control child in layout.Controls) { int row = layout.GetRow(child); if (row >= 1) layout.SetRow(child, row + 1); }
            layout.RowCount++; layout.RowStyles.Insert(1, new RowStyle(SizeType.AutoSize)); layout.Controls.Add(options, 0, 1);
            foreach (Control child in layout.Controls) child.TabIndex = layout.GetRow(child);
            dialog.ClientSize = new Size(dialog.ClientSize.Width, dialog.ClientSize.Height + WorkspaceAppearance.Pixels(100));
            options.SizeChanged += (s, e) => { if (!dialog.IsDisposed) dialog.ClientSize = new Size(dialog.ClientSize.Width, Math.Max(370, layout.PreferredSize.Height + 24)); };
            return options;
        }

        private void PrepareWorkspaceExportWithProgress(Chr0ExportSnapshot frozen, NativeExportOptimizationSettings settings, IWin32Window owner)
        {
            frozen.Validate(); var preferences = new NativeExportOptimizationPreferences(); preferences.Save(settings); frozen.PreferenceWarning = preferences.LastError;
            using (var dialog = new Form { Name = "workspaceExportPreparation", Text = "Preparing verified export copy", StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(650, 360), ShowInTaskbar = false, MinimizeBox = false, MaximizeBox = false, FormBorderStyle = FormBorderStyle.FixedDialog })
            using (var cancellation = new CancellationTokenSource())
            {
                var status = new TextBox { Name = "exportOptimizationProgress", Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, TabIndex = 0,
                    Text = "The editable target master is retained. Preparing an independent export copy…" };
                var cancel = new WorkspaceButton { Text = "Cancel", AutoSize = true, Dock = DockStyle.Right, TabIndex = 1 };
                var actions = new Panel { Dock = DockStyle.Bottom, Height = 38 }; actions.Controls.Add(cancel); dialog.Controls.Add(status); dialog.Controls.Add(actions);
                bool running = true; Exception failure = null;
                cancel.Click += (s, e) => { cancellation.Cancel(); cancel.Enabled = false; };
                dialog.FormClosing += (s, e) => { if (running) { e.Cancel = true; cancellation.Cancel(); cancel.Enabled = false; } };
                dialog.Shown += async (s, e) => {
                    _activeSingleExport = frozen; _workspaceBusy = true; UpdateSimpleWorkspace();
                    try { await PrepareOptimizedExportSnapshot(frozen, settings, cancellation.Token, new Progress<string>(text => { if (!status.IsDisposed) status.Text = text; })); }
                    catch (Exception error) { failure = error; }
                    finally { _activeSingleExport = null; _workspaceBusy = false; running = false; UpdateSimpleWorkspace(); dialog.Close(); }
                };
                DarkTheme.Apply(dialog); WorkspaceAppearance.SizeDialog(dialog); dialog.ShowDialog(owner);
                if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        // Standalone Analyze already ran the shared proof pipeline. Publish its
        // prepared copy through the same exact-member and destination guards,
        // without selecting it, reoptimizing it or changing the master/Undo.
        private void ExportPreparedOptimization(CHR0Node candidate, NativeAnimationOptimizationResult result)
            => ExportPreparedOptimizationWithPolicy(candidate, result, null);

        private void ExportPreparedOptimizationWithPolicy(CHR0Node candidate, NativeAnimationOptimizationResult result, NativeExportOptimizationSettings settings, bool? candidateWasBounded = null)
        {
            if (candidate == null) throw new InvalidOperationException("Analyze the current animation before exporting its optimized copy.");
            using (var frozen = FreezeWorkspaceExport())
            {
                frozen.Candidate = NativeAnimationOptimization.CopySource(candidate);
                frozen.ExpectedContent = ExportContent(frozen.Candidate);
                frozen.OptimizationReport = result?.Report ?? "Prepared independent export copy. Editable target master retained.";
                frozen.PreparedPolicyKey = "standalone-prepared-copy";
                frozen.RequestedExportPolicy = settings?.Policy ?? NativeExportOptimizationPolicy.Exact;
                frozen.EffectiveExportPolicy = candidateWasBounded.HasValue ? candidateWasBounded.Value ? "ErrorBounded" : "Exact" : (settings?.Policy.ToString() ?? "Exact");
                Directory.CreateDirectory(_controlStage); frozen.MasterBytes = GeneratedAnimationBytes(frozen.Master, _controlStage);
                frozen.ExportBytes = result?.AfterBytes ?? 0;
                ExportFrozenPreparedOptimization(frozen, this);
            }
        }

        private void ExportFrozenPreparedOptimization(Chr0ExportSnapshot frozen, IWin32Window owner)
        {
            if (frozen?.Candidate == null) throw new InvalidOperationException("Analyze the current animation before exporting its prepared copy.");
            frozen.Validate();
            using (var dialog = new WorkspaceFileDialog("Export optimized animation copy",
                "Target: " + frozen.Optimization.Target.Name + "\nAnimation: " + frozen.Clip.Name + " · " + frozen.Clip.FrameCount + " frames\n" +
                "Independent prepared copy; the editable master and ordinary Save stay unchanged.\n" + frozen.MasterBytes + " → " + frozen.ExportBytes + " bytes · " + frozen.EffectiveExportPolicy,
                "Native Brawl animation (*.chr0)|*.chr0", "Export copy", frozen.Clip.Name + ".chr0", true, extension: "chr0"))
            {
                dialog.CommitSelection = path => {
                    frozen.Validate();
                    if (File.Exists(path)) using (var confirm = new WorkspaceOverwriteDialog(path))
                        if (confirm.ShowDialog(dialog) != DialogResult.Yes) throw new OperationCanceledException("Export cancelled. Existing destination was kept.");
                    CaptureWorkspaceExportDestination(frozen, path);
                    ExportWorkspaceSnapshot(frozen, path);
                };
                dialog.ShowDialog(owner ?? this);
            }
        }

        private bool TryExportOwnedAnimationCopy(ResourceNode node, string destination)
        {
            if (!(node is CHR0Node clip) || !string.Equals(Path.GetExtension(destination), ".chr0", StringComparison.OrdinalIgnoreCase) ||
                !OwnsFighterRoot(clip)) return false;
            var model = modelEditControl1.TargetModel as MDL0Node;
            var asset = RegisteredFighterAssets().SingleOrDefault(a => _fighterRoots.TryGetValue(a.Id, out var owner) && ReferenceEquals(owner, clip.RootNode));
            // A generic resource-tree export cannot invent the intended model.
            // Sibling retarget results have their exact target rig recorded.
            bool qualified = model != null && (ReferenceEquals(clip, modelEditControl1.TargetAnimation) ||
                asset?.Retarget != null && asset.RigIdentity == BrawlCrate.Studio.Retargeting.BrawlRetargeter.SkeletonFingerprint(model));
            if (qualified) RequireSavedAnimationExportTarget(model);
            modelEditControl1.StopAnim(); FinishControlCurveEdit();
            using (var frozen = qualified ? FreezeOwnedWorkspaceExport(asset, clip, false) : FreezeOwnedReferenceExport(asset, clip))
            {
                ProtectVerifiedExport(Path.GetFullPath(destination));
                CaptureWorkspaceExportDestination(frozen, destination);
                var settings = new NativeExportOptimizationPreferences().Read();
                if (qualified) PrepareWorkspaceExportWithProgress(frozen, settings, this);
                else PrepareReferenceExportSnapshot(frozen, settings);
                ExportWorkspaceSnapshot(frozen, destination);
            }
            return true;
        }

        private Chr0ExportSnapshot FreezeOwnedReferenceExport(FighterAsset asset, CHR0Node clip)
        {
            bool Busy() => _controlBusy || _retargetBusy || _pinGesture != null || _curvePending || _workspaceBusy;
            bool Registered() => asset != null && RegisteredFighterAssets().Contains(asset) && _fighterRoots.TryGetValue(asset.Id, out var owner) &&
                ReferenceEquals(owner, clip?.RootNode) && DiscoverFighterAnimations(owner).Any(node => ReferenceEquals(node, clip));
            if (Busy() || clip == null || !Registered()) throw new InvalidOperationException("Finish the current edit and choose an exactly owned native animation.");
            if (asset.Retarget?.ReviewDraft == true) throw new InvalidOperationException("An unvalidated retarget draft cannot be promoted by reference export.");
            var project = _fighterProject; var root = clip.RootNode; string content = ExportContent(clip);
            var provenance = ReviewClipRecipe(asset, clip);
            var master = NativeAnimationOptimization.CopySource(clip); string masterContent = ExportContent(master);
            return new Chr0ExportSnapshot { Clip = clip, Master = master, Candidate = master, MasterContent = masterContent, ExpectedContent = masterContent,
                Validate = () => {
                    if (Busy() || !ReferenceEquals(project, _fighterProject) || !Registered() || !ReferenceEquals(root, clip.RootNode) ||
                        ExportContent(clip) != content || ReviewClipRecipe(asset, clip) != provenance)
                        throw new InvalidOperationException("Owned reference animation or project dependencies changed. Open Export again.");
                } };
        }

        private void PrepareReferenceExportSnapshot(Chr0ExportSnapshot frozen, NativeExportOptimizationSettings requested)
        {
            var settings = (requested ?? new NativeExportOptimizationSettings()).Clone(); settings.Validate(); frozen.Validate();
            var preparationTime = System.Diagnostics.Stopwatch.StartNew();
            Directory.CreateDirectory(_controlStage); string path = Path.Combine(_controlStage, "reference-export-" + Guid.NewGuid().ToString("N") + ".chr0");
            try
            {
                frozen.Master.ExportChecked(path);
                using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
                    if (decoded == null || ExportContent(decoded) != frozen.MasterContent) throw new InvalidDataException("The independent owned reference did not reopen exactly. No output was published.");
                var actual = NativeChr0SizeReport.Read(path); string hash = FighterProject.HashFile(path); frozen.Validate();
                frozen.MasterBytes = frozen.ExportBytes = frozen.PreparedEncodedBytes = actual.FileBytes; frozen.PreparedEncodedSha256 = hash;
                frozen.RequestedExportPolicy = settings.Policy; frozen.EffectiveExportPolicy = "Exact/reference"; frozen.PreparedPolicyKey = settings.StableKey;
                preparationTime.Stop(); frozen.PreparationMilliseconds = preparationTime.ElapsedMilliseconds;
                frozen.ByteReport = actual.Describe(); frozen.OptimizationReport = "Requested " + settings.Policy + ". Exact/reference fallback: no verified intended target/protection context for this exact owned member. " +
                    "No model was inferred and no reduction or bounded packing was attempted. Full native coefficients, flags, metadata, timing and stored payloads are retained.\r\n" + actual.Describe() +
                    "\r\nPreparation wall time: " + frozen.PreparationMilliseconds + " ms, including reference encoding and independent validation.";
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        private void PrepareSlotMetadataExport(Chr0ExportSnapshot frozen, string name, int version)
        {
            frozen.Validate(); Directory.CreateDirectory(_controlStage);
            var preparationTime = System.Diagnostics.Stopwatch.StartNew();
            string referencePath = Path.Combine(_controlStage, "slot-reference-" + Guid.NewGuid().ToString("N") + ".chr0"),
                candidatePath = Path.Combine(_controlStage, "slot-candidate-" + Guid.NewGuid().ToString("N") + ".chr0");
            CHR0Node reference = null, candidate = null;
            try
            {
                reference = NativeAnimationOptimization.CopySource(frozen.Master); candidate = NativeAnimationOptimization.CopySource(frozen.Candidate);
                reference.Name = candidate.Name = name; reference.Version = candidate.Version = version;
                // CopySource's metadata policy refuses conversions which cannot
                // preserve original data (for example v5 user data in v4).
                string referenceContent = ExportContent(reference), candidateContent = ExportContent(candidate);
                reference.ExportChecked(referencePath); candidate.ExportChecked(candidatePath);
                using (var decoded = NodeFactory.FromFile(null, referencePath) as CHR0Node)
                    if (decoded == null || ExportContent(decoded) != referenceContent) throw new InvalidDataException("The same-slot native reference did not reopen exactly.");
                using (var decoded = NodeFactory.FromFile(null, candidatePath) as CHR0Node)
                    if (decoded == null || ExportContent(decoded) != candidateContent) throw new InvalidDataException("The converted slot export candidate did not reopen exactly.");
                var before = NativeChr0SizeReport.Read(referencePath); var after = NativeChr0SizeReport.Read(candidatePath); string qualified = candidatePath;
                if (after.FileBytes > before.FileBytes)
                {
                    candidate.Dispose(); candidate = NativeAnimationOptimization.CopySource(reference); candidateContent = referenceContent; after = before; qualified = referencePath;
                    frozen.EffectiveExportPolicy = "Exact/reference"; frozen.OptimizationReport += "\r\nSlot metadata reference fallback: the converted candidate would grow.";
                }
                string hash = FighterProject.HashFile(qualified); frozen.Validate();
                if (!ReferenceEquals(frozen.Candidate, frozen.Master)) frozen.Candidate.Dispose(); frozen.Master.Dispose();
                frozen.Master = reference; reference = null; frozen.Candidate = candidate; candidate = null;
                frozen.MasterContent = referenceContent; frozen.ExpectedContent = candidateContent;
                frozen.MasterBytes = before.FileBytes; frozen.ExportBytes = frozen.PreparedEncodedBytes = after.FileBytes; frozen.PreparedEncodedSha256 = hash;
                preparationTime.Stop(); frozen.PreparationMilliseconds += preparationTime.ElapsedMilliseconds;
                frozen.ByteReport = after.Describe(); frozen.OptimizationReport += "\r\nSlot name/version conversion uses its own exact same-slot reference: " + before.FileBytes + " → " + after.FileBytes + " bytes. " + after.Describe() +
                    "\r\nTotal preparation wall time: " + frozen.PreparationMilliseconds + " ms, including slot metadata encoding and independent validation.";
            }
            finally { candidate?.Dispose(); reference?.Dispose(); foreach (string path in new[] { referencePath, candidatePath }) if (File.Exists(path)) File.Delete(path); }
        }
    }
}
