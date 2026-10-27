using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        // Enabled only by the separately qualified second increment.
        private const bool HighQualityOptimizationQualified = false;
        internal Action<AnimationOptimizationDialog> OptimizationReady;
        internal int OptimizationPrepares { get; private set; }
        internal int OptimizationNativeChecks { get; private set; }
        internal int OptimizationApplies { get; private set; }
        private OptimizationSessionBaseline _optimizationBaseline;
        private sealed class OptimizationSessionBaseline : IDisposable
        {
            internal FighterProject Project; internal MDL0Node Model; internal CHR0Node Clip, Baseline;
            internal string ModelContent, Protection, OutputContent, Report;
            internal bool HighQuality;
            public void Dispose() { Baseline?.Dispose(); Baseline = null; }
        }
        private sealed class OptimizationKeyBits { public int Frame { get; set; } public int ValueBits { get; set; } public int TangentBits { get; set; } }
        private sealed class OptimizationTrackBits
        {
            public string Bone { get; set; } public OptimizationKeyBits[][] Channels { get; set; }
            public int FrameLimit { get; set; } public bool Loop { get; set; }
        }
        private sealed class OptimizationChannelInfo
        {
            public string Bone { get; set; } public int Channel { get; set; } public int BeforeKeys { get; set; } public int AfterKeys { get; set; }
            public string Policy { get; set; } public double CertifiedValueErrorBound { get; set; } public double CertifiedSpeedErrorBound { get; set; }
            public string RetainedReason { get; set; }
        }
        private sealed class OptimizationReportInfo
        {
            public long InputKeys { get; set; } public long OutputKeys { get; set; } public long Work { get; set; }
            public int CandidateSpans { get; set; } public int AcceptedMerges { get; set; }
            public bool WorkLimitReached { get; set; } public bool CandidateLimitReached { get; set; } public bool ProbeLimitReached { get; set; }
            public double[] ProbeFrames { get; set; } public OptimizationChannelInfo[] Channels { get; set; } public string Claim { get; set; }
        }
        private sealed class OptimizationOutput
        {
            public OptimizationTrackBits[] Tracks { get; set; } public string[] RemovedUnmatchedTracks { get; set; }
            public string[] ExactBones { get; set; } public string[] ProtectedBones { get; set; } public OptimizationReportInfo Report { get; set; }
        }
        private static int OptimizationFloatBits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static float OptimizationFloat(int bits) => BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
        private static OptimizationTrackBits[] OptimizationWireTracks(CHR0Node clip) => clip.Children.OfType<CHR0EntryNode>().Select(t => new OptimizationTrackBits {
            Bone = t.Name, FrameLimit = t.Keyframes.FrameLimit, Loop = t.Keyframes.Loop,
            Channels = t.KeyArrays.Select(c => c.Entries.Select(k => new OptimizationKeyBits { Frame = k._index, ValueBits = OptimizationFloatBits(k._value), TangentBits = OptimizationFloatBits(k._tangent) }).ToArray()).ToArray()
        }).ToArray();
        private static IEnumerable<ResourceNode> OptimizationWalk(ResourceNode root)
        { yield return root; foreach (var child in root.Children) foreach (var node in OptimizationWalk(child)) yield return node; }
        private void ClearOptimizationBaseline() { _optimizationBaseline?.Dispose(); _optimizationBaseline = null; }

        private void ShowAnimationOptimization()
        {
            var editor = modelEditControl1;
            var model = editor.TargetModel as MDL0Node; var source = editor.TargetAnimation as CHR0Node;
            if (model == null || source == null || !OwnsFighterRoot(source)) throw new InvalidOperationException("Select the current target model's project-owned CHR0 animation.");
            if (_controlBusy || _controlSetupQueued || _retargetBusy || _workspaceBusy || _pinGesture != null || _curvePending || editor.AwaitingRedoSave)
                throw new InvalidOperationException("Finish the current animation/control edit before optimizing.");
            if (source.Version != 4 && source.Version != 5) throw new NotSupportedException("Optimize animation currently supports Brawl CHR0 v4/v5.");
            var project = _fighterProject;
            var asset = RegisteredFighterAssets().SingleOrDefault(a => _fighterRoots.TryGetValue(a.Id, out var root) && ReferenceEquals(root, source.RootNode))
                ?? throw new InvalidOperationException("Register this native target animation in the project first.");
            if (asset.Retarget?.ReviewDraft == true) throw new InvalidOperationException("An unvalidated advanced retarget draft cannot be promoted by optimization.");
            CancelLibraryAutoPreview(); editor.StopAnim(); _libraryTimer?.Stop(); if (_libraryPlay != null) _libraryPlay.Text = "Play";
            var json = new JavaScriptSerializer { MaxJsonLength = 256 * 1024 * 1024, RecursionLimit = 200 };
            string key = ControlKey(), controls = ControlState(key), modelContent = NativeControlHash(model, ".mdl0"), content = NativeAnimationOptimization.ContentKey(source);
            var pins = (editor.AnimationPins ?? new ControlPin[0]).Where(p => Guid.TryParse(p.Id, out _)).ToArray();
            string Protection() => json.Serialize(new { Controls = ControlState(key), Fit = TransitionRecipeFor(asset, source), asset.Retarget,
                Pins = (editor.AnimationPins ?? new ControlPin[0]).Where(p => Guid.TryParse(p.Id, out _)).ToArray() });
            string protection = Protection();
            string CacheProtection()
            {
                // An export receipt changes provenance, not the animation's
                // quality baseline. Keep strict full provenance for stale Apply,
                // but do not accumulate approximation after an ordinary export.
                var recipe = json.Deserialize<Dictionary<string, object>>(json.Serialize(asset.Retarget));
                if (recipe != null && recipe.TryGetValue("Library", out var value) && value is Dictionary<string, object> library)
                { library.Remove("LastExportSha256"); library.Remove("LastExportFormat"); }
                return json.Serialize(new { Controls = ControlState(key), Fit = TransitionRecipeFor(asset, source), Retarget = recipe,
                    Pins = (editor.AnimationPins ?? new ControlPin[0]).Where(p => Guid.TryParse(p.Id, out _)).ToArray() });
            }
            string cacheProtection = CacheProtection();
            bool BindingReady() => _bodyBindingReady && _loadedControlModel == model && _loadedControlClip == source && _loadedControlKey == key;
            Action current = () => {
                if (IsDisposed || project != _fighterProject || model != editor.TargetModel || source != editor.SelectedCHR0 || source != editor.TargetAnimation ||
                    !OwnsFighterRoot(source) || !_fighterRoots.TryGetValue(asset.Id, out var owner) || owner != source.RootNode ||
                    NativeAnimationOptimization.ContentKey(source) != content || NativeControlHash(model, ".mdl0") != modelContent ||
                    Protection() != protection || _controlBusy || _retargetBusy || _pinGesture != null || _curvePending)
                    throw new InvalidOperationException("The current animation, target or protections changed. Reopen Optimize animation; the stale result was discarded.");
            };
            bool cached = _optimizationBaseline != null && _optimizationBaseline.Project == project && _optimizationBaseline.Model == model &&
                _optimizationBaseline.Clip == source && _optimizationBaseline.ModelContent == modelContent && _optimizationBaseline.Protection == cacheProtection &&
                _optimizationBaseline.OutputContent == content;
            if (!cached) ClearOptimizationBaseline();
            string stage = Path.Combine(_controlStage, "optimization-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            var roots = new List<ResourceNode>(); AnimationControlClient client = null; CancellationTokenSource cancellation = null;
            NativeAnimationOptimizationResult prepared = null; string preparedContent = null; bool preparedQuality = false;
            using (var before = NativeAnimationOptimization.Copy(source))
            using (var qualityBaseline = NativeAnimationOptimization.Copy(cached ? _optimizationBaseline.Baseline : source))
            {
                try
                {
                    string modelPath = Path.Combine(stage, "model.mdl0"); model.Export(modelPath);
                    string rootPath = Path.Combine(stage, "model-root" + (Path.GetExtension(model.RootNode.FilePath) is string ext && !string.IsNullOrEmpty(ext) ? ext : ".brres")); model.RootNode.Export(rootPath);
                    ResourceNode PreviewRoot() { var root = NodeFactory.FromFile(null, rootPath) ?? throw new InvalidDataException("The independent model snapshot could not be opened."); roots.Add(root); return root; }
                    MDL0Node PreviewModel(ResourceNode root) => OptimizationWalk(root).OfType<MDL0Node>().Single(m => NativeControlHash(m, ".mdl0") == modelContent);
                    var originalRoot = PreviewRoot(); var resultRoot = PreviewRoot();
                    var validationRoot = NodeFactory.FromFile(null, modelPath) ?? throw new InvalidDataException("The independent validation model could not be opened."); roots.Add(validationRoot);
                    var validationModel = OptimizationWalk(validationRoot).OfType<MDL0Node>().Single();
                    double length = NativeAnimationOptimization.GetBindBoundsDiagonal(validationModel);
                    string currentEncoded = Path.Combine(stage, "current-export.chr0"); before.ExportChecked(currentEncoded); long currentBytes = new FileInfo(currentEncoded).Length;
                    // Embedded BRES payload lengths exclude standalone strings and
                    // alignment. Compare only an actual clean standalone file.
                    long? storedBytes = ReferenceEquals(source.RootNode, source) && !source.IsDirty && !source.IsBranch && !source.IsCompressed &&
                        string.Equals(Path.GetExtension(asset.WorkingPath), ".chr0", StringComparison.OrdinalIgnoreCase) && File.Exists(asset.WorkingPath)
                        ? (long?)new FileInfo(asset.WorkingPath).Length : null;
                    var exact = new HashSet<string>(BindingReady() ? _protectedMotionBones.ToArray() : new string[0], StringComparer.Ordinal);
                    var protectedBones = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var pin in pins)
                    {
                        var bone = model.AllBones.SingleOrDefault(b => b.Name == pin.BoneName) ?? throw new InvalidDataException("An editor pin no longer belongs to this target.");
                        for (; bone != null; bone = bone.Parent as MDL0BoneNode) protectedBones.Add(bone.Name);
                    }
                    if (NativeTransitionContacts.HasBakedSupport(asset)) foreach (string name in NativeTransitionContacts.Resolve(model, asset, true)) protectedBones.Add(name);
                    foreach (var bone in model.AllBones) if (Enumerable.Range(0, 3).Any(c => bone.BindState[c] == 0)) exact.Add(bone.Name);
                    var mandatory = new HashSet<int>(); var ranges = new TransitionProtected[0];
                    if (TransitionRecipeFor(asset, source) is NativeTransitionFit fit)
                    {
                        fit.Validate(); var options = json.Deserialize<TransitionFitOptions>(fit.Options);
                        if (options.Entry != null) { mandatory.Add(0); mandatory.Add(options.EntrySamples - 1); }
                        if (options.Exit != null) { mandatory.Add(source.FrameCount - options.ExitSamples); mandatory.Add(source.FrameCount - 1); }
                        ranges = options.Protected ?? new TransitionProtected[0];
                    }
                    using (var window = new AnimationOptimizationDialog(originalRoot, PreviewModel(originalRoot), resultRoot, PreviewModel(resultRoot), before,
                        editor.ModelPanel.CurrentViewport, null, project.Name + " · " + model.Name + " · " + source.Name + " · " + WorkspaceNativeIdentity(source), HighQualityOptimizationQualified))
                    {
                        window.CancelWork = () => { cancellation?.Cancel(); client?.Dispose(); client = null; };
                        window.Prepare = async highQuality =>
                        {
                            current(); if (highQuality && !HighQualityOptimizationQualified) throw new InvalidOperationException("High-quality reduction is not qualified in this increment.");
                            prepared?.Dispose(); prepared = null; cancellation?.Dispose(); cancellation = new CancellationTokenSource(); var token = cancellation.Token;
                            if (cached && _optimizationBaseline.HighQuality == highQuality)
                            {
                                window.PreparedCanApply = false; window.PreparedSummary = "This unchanged animation is already optimized in this mode.";
                                window.PreparedReport = _optimizationBaseline.Report + "\r\nSession baseline retained; no cumulative approximation was performed.";
                                return NativeAnimationOptimization.Copy(before);
                            }
                            var baseline = highQuality ? qualityBaseline : before;
                            string inputPath = Path.Combine(stage, "input-" + Guid.NewGuid().ToString("N") + ".json"), outputPath = Path.Combine(stage, "output-" + Guid.NewGuid().ToString("N") + ".json");
                            _workspaceBusy = true;
                            try
                            {
                                var input = new { baseline.Name, SampleCount = baseline.FrameCount, baseline.Loop, HighQuality = highQuality, LengthScale = length,
                                    Tracks = OptimizationWireTracks(baseline), ExactBones = exact.ToArray(), ProtectedBones = protectedBones.ToArray(), MandatoryFrames = mandatory.OrderBy(f => f).ToArray(),
                                    ProtectedRanges = ranges, ProtectionKnown = BindingReady() };
                                await Task.Run(() => { token.ThrowIfCancellationRequested(); File.WriteAllText(inputPath, json.Serialize(input)); }, token);
                                if (client == null) client = new AnimationControlClient(); var session = client; OptimizationPrepares++;
                                var reply = await session.Send(new { Operation = "animation-optimize-prepare", Model = modelPath, State = controls, Optimization = new { InputPath = inputPath, OutputPath = outputPath } });
                                token.ThrowIfCancellationRequested(); current();
                                if (reply.OptimizationPath != outputPath || new FileInfo(outputPath).Length > 256L * 1024 * 1024) throw new InvalidDataException("Invalid bounded optimization reply.");
                                var output = await Task.Run(() => json.Deserialize<OptimizationOutput>(File.ReadAllText(outputPath)), token);
                                if (output?.Tracks == null || output.Report == null || output.Report.ProbeLimitReached) throw new InvalidDataException("Optimization did not retain complete native verification coverage. No result was applied.");
                                var tracks = output.Tracks.Select(t => new ControlTrack { Bone = t.Bone, Channels = t.Channels.Select(c => c.Select(k => new ControlKey { Frame = k.Frame, Value = OptimizationFloat(k.ValueBits), Tangent = OptimizationFloat(k.TangentBits) }).ToArray()).ToArray() }).ToArray();
                                var options = new NativeAnimationOptimizationOptions { ExactOnly = !highQuality, LengthScale = length, OriginalStoredBytes = storedBytes,
                                    TargetBones = model.AllBones.Select(b => b.Name).ToArray(), ProtectedBones = output.ProtectedBones, ExactBones = output.ExactBones, MandatoryFrames = mandatory.ToArray(), ProbeFrames = output.Report.ProbeFrames };
                                OptimizationNativeChecks++;
                                var verified = await Task.Run(() => NativeAnimationOptimization.Prepare(validationModel, baseline, tracks, options, stage, token), token);
                                try
                                {
                                    token.ThrowIfCancellationRequested(); current();
                                    if (verified.AfterBytes > currentBytes) throw new InvalidDataException("The prepared animation would grow relative to the current native export. The current animation is unchanged.");
                                    bool changed = verified.Candidate != null && NativeAnimationOptimization.ContentKey(verified.Candidate) != content;
                                    prepared = verified; preparedContent = verified.ContentKey; preparedQuality = highQuality;
                                    window.PreparedCanApply = changed;
                                    window.PreparedSummary = changed ? "Keys " + before.Children.OfType<CHR0EntryNode>().Sum(t => t.KeyArrays.Sum(a => a.KeyCount)) + " → " + verified.AfterKeys + " · current export " + currentBytes + " → " + verified.AfterBytes + " bytes. Review and Apply optimization."
                                        : "No useful saving for this animation; the current native content is unchanged.";
                                    window.PreparedReport = reply.Algorithm + "\r\n" + verified.Report + "\r\nBaseline: " + (cached && highQuality ? "retained original session animation" : "current animation frozen at Analyze") +
                                        "\r\nAbsent-target tracks to remove: " + (output.RemovedUnmatchedTracks.Length == 0 ? "none" : string.Join(", ", output.RemovedUnmatchedTracks)) +
                                        "\r\nExact-only controls: " + string.Join(", ", output.ExactBones) + "\r\nProtected pin/contact paths: " + string.Join(", ", output.ProtectedBones) +
                                        "\r\nContinuous scalar proof: " + output.Report.Claim + "\r\nWork " + output.Report.Work + "; spans " + output.Report.CandidateSpans + "; merges " + output.Report.AcceptedMerges +
                                        (output.Report.WorkLimitReached || output.Report.CandidateLimitReached ? "; resource-limited, fully verified best candidate only" : "") +
                                        "\r\nNo PAC/PCS or Melee size/game claim. Current clip metadata and native flags retained.\r\n" +
                                        string.Join("\r\n", (output.Report.Channels ?? new OptimizationChannelInfo[0]).Select(c => c.Bone + " / " + c.Channel + ": " + c.BeforeKeys + " → " + c.AfterKeys + " · " + c.Policy + " · " + c.RetainedReason));
                                    return NativeAnimationOptimization.Copy(verified.Candidate ?? before);
                                }
                                catch { verified.Dispose(); throw; }
                            }
                            finally
                            {
                                // Repeated Analyze keeps one operation's model and
                                // prepared native result, not every JSON request.
                                foreach (string generated in new[] { inputPath, outputPath })
                                    try { if (File.Exists(generated)) File.Delete(generated); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                                _workspaceBusy = false; UpdateFighterStatus();
                            }
                        };
                        window.Apply = (highQuality, candidate) =>
                        {
                            current(); if (_workspaceBusy || prepared == null || prepared.Candidate == null || highQuality != preparedQuality ||
                                NativeAnimationOptimization.ContentKey(candidate) != preparedContent) throw new InvalidOperationException("Analyze the current animation again before Apply.");
                            var names = source.Children.Concat(candidate.Children).Select(n => n.Name).Distinct(StringComparer.Ordinal).ToArray();
                            var original = NativeControlSnapshot.Capture(source, names); var optimized = NativeControlSnapshot.Capture(candidate, names);
                            string[] beforeOrder = source.Children.Select(c => c.Name).ToArray(), afterOrder = candidate.Children.Select(c => c.Name).ToArray();
                            bool dirty = _fighterDirty, nativeDirty = source.RootNode.IsDirty; var approvals = _batchReviewRows.Select(r => (Row: r, Approved: r.Approved)).ToArray();
                            void Restore(bool useOptimized)
                            {
                                bool restoring = _restoringCurve; _restoringCurve = true;
                                try
                                {
                                    var previousEntries = source.Children.OfType<CHR0EntryNode>().ToArray();
                                    string curveBone = _curveTarget?.Parent == source ? _curveTarget.Name : null;
                                    (useOptimized ? optimized : original).Restore(source);
                                    foreach (string name in useOptimized ? afterOrder : beforeOrder) { var child = source.Children.Single(c => c.Name == name); child.Remove(); source.AddChild(child); }
                                    PreciseCHR0EntryNode.Promote(source);
                                    editor.RebindSavedCurveTargets(source, previousEntries);
                                    if (editor.SelectedCHR0 == source) { editor.UpdateModel(); editor.RefreshControlCurveTarget(); editor.InterpolationEditor?.RootChanged(); editor.KeyframePanel?.UpdateKeyframes(); }
                                    if (curveBone != null) { _curveTarget = source.Children.OfType<CHR0EntryNode>().SingleOrDefault(t => t.Name == curveBone); _curveBefore = _curveTarget == null ? null : NativeControlSnapshot.Capture(source, new[] { curveBone }); }
                                    BatchReviewAuthored(source); InvalidateControls(); MarkFighterChanged();
                                }
                                finally { _restoringCurve = restoring; }
                            }
                            try { Restore(true); if (NativeAnimationOptimization.ContentKey(source) != preparedContent) throw new InvalidDataException("The native Apply did not retain the independently verified content."); }
                            catch { Restore(false); _fighterDirty = dirty; source.RootNode.IsDirty = nativeDirty; foreach (var approval in approvals) approval.Row.Approved = approval.Approved; throw; }
                            editor.RecordEditorCommand(() => Restore(false), () => Restore(true));
                            ClearOptimizationBaseline(); _optimizationBaseline = new OptimizationSessionBaseline { Project = project, Model = model, Clip = source,
                                ModelContent = modelContent, Protection = cacheProtection, OutputContent = preparedContent, HighQuality = highQuality,
                                Baseline = NativeAnimationOptimization.Copy(qualityBaseline), Report = window.PreparedReport };
                            OptimizationApplies++; editor.ModelPanel.Invalidate(); UpdateFighterStatus(); _fighterStatus.Text = "Optimized " + source.Name + ". Save or export to keep the native result; Undo restores the previous animation.";
                        };
                        window.Shown += (s, e) => OptimizationReady?.Invoke(window);
                        window.ShowDialog(this);
                    }
                }
                finally
                {
                    cancellation?.Cancel(); client?.Dispose(); cancellation?.Dispose(); prepared?.Dispose();
                    foreach (var root in roots) root.Dispose();
                    string resolved = Path.GetFullPath(stage), parent = Path.GetFullPath(_controlStage);
                    if (Path.GetDirectoryName(resolved) == parent && Path.GetFileName(resolved).StartsWith("optimization-", StringComparison.Ordinal))
                        try { Directory.Delete(resolved, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
