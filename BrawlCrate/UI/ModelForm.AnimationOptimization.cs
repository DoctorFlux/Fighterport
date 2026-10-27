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
        private const bool HighQualityOptimizationQualified = true;
        internal Action<AnimationOptimizationDialog> OptimizationReady;
        internal int OptimizationPrepares { get; private set; }
        internal int OptimizationNativeChecks { get; private set; }
        internal int OptimizationApplies { get; private set; }
        private const long OptimizationCacheKeyLimit = 2000000, OptimizationCacheByteLimit = 256L * 1024 * 1024;
        private long _optimizationCacheClock;
        private Dictionary<OptimizationCacheKey, OptimizationSessionBaseline> _optimizationBaselines = new Dictionary<OptimizationCacheKey, OptimizationSessionBaseline>();
        private OptimizationSessionBaseline _optimizationBaseline;
        internal int OptimizationCachedClips => _optimizationBaselines.Count;
        internal long OptimizationCachedKeys => _optimizationBaselines.Values.Sum(b => b.BaselineKeys);
        internal long OptimizationCachedBytes => _optimizationBaselines.Values.Sum(b => b.RetainedBytes);
        private sealed class OptimizationCacheKey : IEquatable<OptimizationCacheKey>
        {
            internal readonly FighterProject Project; internal readonly MDL0Node Model; internal readonly CHR0Node Clip;
            internal OptimizationCacheKey(FighterProject project, MDL0Node model, CHR0Node clip) { Project = project; Model = model; Clip = clip; }
            public bool Equals(OptimizationCacheKey other) => other != null && ReferenceEquals(Project, other.Project) && ReferenceEquals(Model, other.Model) && ReferenceEquals(Clip, other.Clip);
            public override bool Equals(object other) => Equals(other as OptimizationCacheKey);
            public override int GetHashCode() => unchecked((System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Project) * 397 ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Model)) * 397 ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Clip));
        }
        private sealed class OptimizationSessionBaseline : IDisposable
        {
            internal FighterProject Project; internal MDL0Node Model; internal CHR0Node Clip, Baseline;
            internal string ModelContent, Protection, ProtectionHash, OutputContent, OutputStorage, BaselineHash, Report, MarkerReport;
            internal long BaselineKeys, BaselineBytes, LastUse;
            internal bool HighQuality, SmallFile, CompactStorage;
            private static long TextBytes(string value) => value == null ? 0 : checked(2L * value.Length);
            internal long MarkerBytes => TextBytes(ModelContent) + TextBytes(ProtectionHash) + TextBytes(OutputContent) + TextBytes(OutputStorage) + TextBytes(BaselineHash) + TextBytes(MarkerReport);
            internal long RetainedBytes => BaselineBytes + TextBytes(ModelContent) + TextBytes(Protection) + TextBytes(ProtectionHash) + TextBytes(OutputContent) + TextBytes(OutputStorage) + TextBytes(BaselineHash) + TextBytes(Report) + (ReferenceEquals(Report, MarkerReport) ? 0 : TextBytes(MarkerReport));
            internal void Evict()
            {
                Baseline?.Dispose(); Baseline = null; BaselineKeys = BaselineBytes = 0;
                Protection = null; Report = MarkerReport;
            }
            public void Dispose() { Baseline?.Dispose(); Baseline = null; BaselineKeys = BaselineBytes = 0; }
        }
        private sealed class OptimizationCacheCommit : IDisposable
        {
            internal Dictionary<OptimizationCacheKey, OptimizationSessionBaseline> Entries;
            internal OptimizationSessionBaseline Pending, Replaced;
            internal OptimizationSessionBaseline[] Evictions;
            public void Dispose() { Pending?.Dispose(); Pending = null; }
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
            public string Version { get; set; }
            public long InputKeys { get; set; } public long OutputKeys { get; set; } public long Work { get; set; }
            public int CandidateSpans { get; set; } public int AcceptedMerges { get; set; }
            public bool WorkLimitReached { get; set; } public bool CandidateLimitReached { get; set; } public bool ProbeLimitReached { get; set; }
            public double[] ProbeFrames { get; set; } public OptimizationChannelInfo[] Channels { get; set; } public string Claim { get; set; }
        }
        private sealed class OptimizationOutput
        {
            public OptimizationTrackBits[] Tracks { get; set; } public string[] RemovedUnmatchedTracks { get; set; }
            public string[] RemovedBindTracks { get; set; } = new string[0];
            public string[] ExactBones { get; set; } public string[] ProtectedBones { get; set; } public OptimizationReportInfo Report { get; set; }
        }
        private sealed class OptimizationCertificateInfo
        {
            public bool Accepted { get; set; } public long Work { get; set; } public bool WorkLimitReached { get; set; }
            public string Reason { get; set; } public OptimizationChannelInfo[] Channels { get; set; }
            public double[] ProbeFrames { get; set; }
        }
        private sealed class OptimizationCertificateOutput { public OptimizationCertificateInfo Certificate { get; set; } }
        private static int OptimizationFloatBits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static float OptimizationFloat(int bits) => BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
        private static OptimizationTrackBits[] OptimizationWireTracks(CHR0Node clip) => clip.Children.OfType<CHR0EntryNode>().Select(t => new OptimizationTrackBits {
            Bone = t.Name, FrameLimit = t.Keyframes.FrameLimit, Loop = t.Keyframes.Loop,
            Channels = t.KeyArrays.Select(c => c.Entries.Select(k => new OptimizationKeyBits { Frame = k._index, ValueBits = OptimizationFloatBits(k._value), TangentBits = OptimizationFloatBits(k._tangent) }).ToArray()).ToArray()
        }).ToArray();
        private static IEnumerable<ResourceNode> OptimizationWalk(ResourceNode root)
        { yield return root; foreach (var child in root.Children) foreach (var node in OptimizationWalk(child)) yield return node; }
        private static string OptimizationTextHash(string value)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value ?? ""))).Replace("-", "").ToLowerInvariant();
        }
        private void ClearOptimizationBaseline()
        {
            foreach (var entry in _optimizationBaselines.Values) entry.Dispose();
            _optimizationBaselines.Clear(); _optimizationBaseline = null;
        }
        private void RemoveOptimizationBaseline(OptimizationCacheKey key)
        {
            if (!_optimizationBaselines.TryGetValue(key, out var entry)) return;
            _optimizationBaselines.Remove(key); if (ReferenceEquals(_optimizationBaseline, entry)) _optimizationBaseline = null; entry.Dispose();
        }
        private OptimizationCacheCommit PrepareOptimizationCache(OptimizationCacheKey key, OptimizationSessionBaseline previous,
            CHR0Node original, string modelContent, string protection, string outputContent, string outputStorage, bool quality, bool compact, string report, string stage)
            => PrepareOptimizationCacheMode(key, previous, original, modelContent, protection, outputContent, outputStorage, quality, false, compact, report, stage);

        private OptimizationCacheCommit PrepareOptimizationCacheMode(OptimizationCacheKey key, OptimizationSessionBaseline previous,
            CHR0Node original, string modelContent, string protection, string outputContent, string outputStorage, bool quality, bool smallFile, bool compact, string report, string stage)
        {
            var pending = new OptimizationSessionBaseline { Project = key.Project, Model = key.Model, Clip = key.Clip,
                ModelContent = modelContent, Protection = protection, ProtectionHash = OptimizationTextHash(protection), OutputContent = outputContent,
                OutputStorage = outputStorage, HighQuality = quality, SmallFile = smallFile, CompactStorage = compact, Report = report, LastUse = ++_optimizationCacheClock,
                BaselineHash = previous?.BaselineHash ?? NativeAnimationOptimization.ContentKey(original) };
            try
            {
                pending.MarkerReport = "This unchanged clip retains its original session baseline identity, but its native snapshot was evicted under the shared 2000000-key / 256 MiB session cache budget.\r\n" +
                    "Original baseline SHA-256: " + pending.BaselineHash + "\r\nCurrent optimized content SHA-256: " + outputContent +
                    "\r\nRepeated " + (smallFile ? "Small file" : quality ? "High quality" : "Exact") + " optimization is a no-op. Exact cleanup remains available; another quality calculation requires the retained original snapshot or an explicit project reopen/authored edit establishing a new baseline.";
                // Allocate, serialize and measure the new cache before live Apply.
                // An evicted marker must never silently become a new baseline.
                if (original != null)
                {
                    pending.Baseline = NativeAnimationOptimization.Copy(original);
                    pending.BaselineKeys = pending.Baseline.Children.OfType<CHR0EntryNode>().Sum(e => (long)e.KeyArrays.Sum(a => a.KeyCount));
                    string path = Path.Combine(stage, "retained-baseline-" + Guid.NewGuid().ToString("N") + ".chr0");
                    try { pending.Baseline.ExportChecked(path); pending.BaselineBytes = new FileInfo(path).Length; }
                    finally { if (File.Exists(path)) File.Delete(path); }
                }
                else pending.Evict();
                var entries = new Dictionary<OptimizationCacheKey, OptimizationSessionBaseline>(_optimizationBaselines);
                entries.TryGetValue(key, out var replaced); entries[key] = pending;
                // Minimal identity markers are never evicted. Refuse before Apply
                // if even their complete accounting cannot fit the shared cap.
                if (entries.Values.Sum(e => e.MarkerBytes) > OptimizationCacheByteLimit)
                    throw new InvalidOperationException("Optimization session identity tracking exceeds its 256 MiB budget. Reopen the project before applying another optimized result.");
                long keys = entries.Values.Sum(e => e.BaselineKeys), bytes = entries.Values.Sum(e => e.RetainedBytes);
                var evictions = new List<OptimizationSessionBaseline>();
                foreach (var entry in entries.Values.Where(e => !ReferenceEquals(e, pending)).OrderBy(e => e.LastUse))
                {
                    if (keys <= OptimizationCacheKeyLimit && bytes <= OptimizationCacheByteLimit) break;
                    long markerBytes = entry.MarkerBytes;
                    if (entry.Baseline == null && entry.RetainedBytes <= markerBytes) continue;
                    evictions.Add(entry); keys -= entry.BaselineKeys; bytes -= entry.RetainedBytes - markerBytes;
                }
                if (keys > OptimizationCacheKeyLimit || bytes > OptimizationCacheByteLimit)
                {
                    long beforeBytes = pending.RetainedBytes; keys -= pending.BaselineKeys; pending.Evict(); bytes -= beforeBytes - pending.RetainedBytes;
                }
                if (keys > OptimizationCacheKeyLimit || bytes > OptimizationCacheByteLimit) throw new InvalidOperationException("Optimization session cache cannot retain complete identity markers within its existing resource budget.");
                return new OptimizationCacheCommit { Entries = entries, Pending = pending, Replaced = replaced, Evictions = evictions.ToArray() };
            }
            catch { pending.Dispose(); throw; }
        }
        private void CommitOptimizationCache(OptimizationCacheCommit commit)
        {
            foreach (var entry in commit.Evictions) entry.Evict();
            commit.Replaced?.Dispose();
            _optimizationBaselines = commit.Entries; _optimizationBaseline = commit.Pending; commit.Pending = null;
        }

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
            string key = ControlKey(), controls = ControlState(key), modelContent = NativeControlHash(model, ".mdl0"), content = NativeAnimationOptimization.ContentKey(source), storageContent = NativeAnimationOptimization.StorageKey(source);
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
                // Controls contains all authored pin/contact identities, anchors
                // and intervals. Viewport pin poses AND active membership vary
                // with the playhead; they cannot establish a new quality baseline.
                return json.Serialize(new { Controls = ControlState(key), Fit = TransitionRecipeFor(asset, source), Retarget = recipe });
            }
            string cacheProtection = CacheProtection();
            bool BindingReady() => _bodyBindingReady && _loadedControlModel == model && _loadedControlClip == source && _loadedControlKey == key;
            Action current = () => {
                if (IsDisposed || project != _fighterProject || model != editor.TargetModel || source != editor.SelectedCHR0 || source != editor.TargetAnimation ||
                    !OwnsFighterRoot(source) || !_fighterRoots.TryGetValue(asset.Id, out var owner) || owner != source.RootNode ||
                    NativeAnimationOptimization.ContentKey(source) != content || NativeAnimationOptimization.StorageKey(source) != storageContent || NativeControlHash(model, ".mdl0") != modelContent ||
                    Protection() != protection || _controlBusy || _retargetBusy || _pinGesture != null || _curvePending)
                    throw new InvalidOperationException("The current animation, target or protections changed. Reopen Optimize animation; the stale result was discarded.");
            };
            // Opening a sibling clip selects its own session entry. Only a real
            // edit/context change or project reopen establishes a new baseline.
            if (_optimizationBaselines.Values.Any(e => !ReferenceEquals(e.Project, project))) ClearOptimizationBaseline();
            var sessionKey = new OptimizationCacheKey(project, model, source);
            bool cached = _optimizationBaselines.TryGetValue(sessionKey, out var sessionBaseline) &&
                sessionBaseline.ModelContent == modelContent && sessionBaseline.ProtectionHash == OptimizationTextHash(cacheProtection) && sessionBaseline.OutputContent == content;
            if (!cached) { RemoveOptimizationBaseline(sessionKey); sessionBaseline = null; }
            _optimizationBaseline = sessionBaseline;
            if (cached) sessionBaseline.LastUse = ++_optimizationCacheClock;
            string stage = Path.Combine(_controlStage, "optimization-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            var roots = new List<ResourceNode>(); AnimationControlClient client = null; CancellationTokenSource cancellation = null;
            NativeAnimationOptimizationResult prepared = null; string preparedContent = null, preparedStorage = null; bool preparedQuality = false, preparedSmallFile = false, preparedCompact = false;
            using (var before = NativeAnimationOptimization.Copy(source))
            using (var qualityBaseline = cached && sessionBaseline.Baseline == null ? null : NativeAnimationOptimization.Copy(cached ? sessionBaseline.Baseline : source))
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
                    // Embedded block length excludes shared strings. Compare it
                    // only to the independently decoded candidate's block length.
                    long? storedResourceBytes = !source.IsDirty && !source.IsBranch && source.WorkingUncompressed.Length > 0
                        ? (long?)source.WorkingUncompressed.Length : null;
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
                            bool compact = window.ReadCompactStorage();
                            bool smallFile = window.ReadSmallFile();
                            current(); if (highQuality && !HighQualityOptimizationQualified) throw new InvalidOperationException("High-quality reduction is not qualified in this increment.");
                            prepared?.Dispose(); prepared = null; cancellation?.Dispose(); cancellation = new CancellationTokenSource(); var token = cancellation.Token;
                            if (cached && sessionBaseline.HighQuality == highQuality && sessionBaseline.SmallFile == smallFile && sessionBaseline.CompactStorage == compact && sessionBaseline.OutputStorage == storageContent)
                            {
                                window.PreparedCanApply = false; window.PreparedSummary = "This unchanged animation is already optimized in this mode.";
                                window.PreparedReport = sessionBaseline.Report + "\r\nOriginal session baseline SHA-256: " + sessionBaseline.BaselineHash +
                                    (sessionBaseline.Baseline == null ? "\r\nIdentity marker retained; no new approximation was performed." : "\r\nSession baseline retained; no cumulative approximation was performed.");
                                return NativeAnimationOptimization.Copy(before);
                            }
                            if (highQuality && qualityBaseline == null)
                            {
                                var unavailable = new InvalidOperationException("The original session baseline was evicted from memory. High-quality reanalysis is blocked to prevent cumulative approximation. Exact cleanup is available. Save and reopen the project to establish a new baseline.");
                                unavailable.Data["OptimizationDetails"] = sessionBaseline.MarkerReport; throw unavailable;
                            }
                            var baseline = highQuality ? qualityBaseline : before;
                            if (highQuality && (!BindingReady() || length <= 0)) throw new InvalidOperationException("High-quality reduction requires reviewed motion-control bindings and finite positive native bind geometry. Exact cleanup remains available.");
                            bool protectionKnown = BindingReady();
                            long work = 0; int spans = 0;
                            var budget = new NativeAnimationOptimizationBudget();
                            // Dense proposals and Core certificates share the
                            // existing operation work ceiling across all retries.
                            var denseProofBudget = new NativeDenseProofBudget(amount =>
                            {
                                token.ThrowIfCancellationRequested();
                                if (amount < 0 || amount > 2000000 - work) return false;
                                work += amount;
                                return true;
                            });
                            var receipts = new Dictionary<NativeAnimationOptimizationResult, OptimizationOutput>();
                            ControlTrack[] exactReductionTracks = null;
                            var wire = OptimizationWireTracks(baseline);
                            var nativeBones = model.AllBones.ToDictionary(b => b.Name, StringComparer.Ordinal);
                            // An imported baked rotation owns a complete stored
                            // terminal sample and an interpolation context beyond
                            // the native projection. Preserve it as a whole; other
                            // eligible target tracks can still be optimized.
                            var retainedDense = baseline.Children.OfType<CHR0EntryNode>()
                                .Where(entry => nativeBones.ContainsKey(entry.Name))
                                .Where(entry =>
                                {
                                    var storage = PreciseCHR0EntryNode.CaptureStorage(entry);
                                    return storage != null && storage.MatchesGroup(entry.Keyframes, 1) &&
                                        Enumerable.Range(3, 3).Any(channel => storage[channel]?.IsDense == true);
                                }).Select(entry => entry.Name).ToArray();
                            var analysisProtected = new HashSet<string>(protectedBones, StringComparer.Ordinal);
                            analysisProtected.UnionWith(retainedDense);
                            var bindPoses = baseline.Children.OfType<CHR0EntryNode>().Select(entry => new { Entry = entry, Bone = nativeBones.TryGetValue(entry.Name, out var bound) ? bound : null })
                                .Where(pair => NativeAnimationOptimization.CanRemoveBindTrack(pair.Entry, pair.Bone))
                                .Select(pair => new { pair.Bone.Name, Bits = Enumerable.Range(0, 9).Select(c => OptimizationFloatBits(pair.Bone.BindState[c])).ToArray() })
                                .Select(pair => new { Bone = pair.Name, pair.Bits }).ToArray();
                            async Task<NativeAnimationOptimizationResult> Attempt(bool approximate, double precision)
                            {
                                token.ThrowIfCancellationRequested(); current();
                                if (work >= 2000000 || spans >= 100000)
                                {
                                    var exhausted = new InvalidDataException("Aggregate Core proof budget exhausted; only an already verified candidate can be offered.");
                                    exhausted.Data["OptimizationBudget"] = "Core work/candidate spans"; throw exhausted;
                                }
                                string inputPath = Path.Combine(stage, "input-" + Guid.NewGuid().ToString("N") + ".json"), outputPath = Path.Combine(stage, "output-" + Guid.NewGuid().ToString("N") + ".json");
                                try
                                {
                                    var input = new { baseline.Name, SampleCount = baseline.FrameCount, baseline.Loop, HighQuality = approximate, SmallFile = approximate && smallFile, LengthScale = length,
                                        PrecisionScale = precision, MaximumWork = 2000000 - work, MaximumCandidateSpans = 100000 - spans,
                                        Tracks = wire, ExactBones = exact.ToArray(), ProtectedBones = analysisProtected.ToArray(), MandatoryFrames = approximate && smallFile ? new int[0] : mandatory.OrderBy(f => f).ToArray(),
                                        FitJoinFrames = approximate && smallFile ? mandatory.OrderBy(f => f).ToArray() : new int[0],
                                        ProtectedRanges = ranges, ProtectionKnown = protectionKnown, BindPoses = bindPoses };
                                    await Task.Run(() => { token.ThrowIfCancellationRequested(); File.WriteAllText(inputPath, json.Serialize(input)); }, token);
                                    token.ThrowIfCancellationRequested(); current();
                                    if (client == null) client = new AnimationControlClient(); var session = client; OptimizationPrepares++;
                                    var reply = await session.Send(new { Operation = "animation-optimize-prepare", Model = modelPath, State = controls, Optimization = new { InputPath = inputPath, OutputPath = outputPath } });
                                    token.ThrowIfCancellationRequested(); current();
                                    if (reply.OptimizationPath != outputPath || new FileInfo(outputPath).Length > 256L * 1024 * 1024) throw new InvalidDataException("Invalid bounded optimization reply.");
                                    var output = await Task.Run(() => json.Deserialize<OptimizationOutput>(File.ReadAllText(outputPath)), token);
                                    if (output?.Tracks == null || output.Report == null || output.Report.Work < 0 || output.Report.Work > 2000000 - work ||
                                        output.Report.CandidateSpans < 0 || output.Report.CandidateSpans > 100000 - spans) throw new InvalidDataException("Invalid aggregate optimization proof receipt.");
                                    work += output.Report.Work; spans += output.Report.CandidateSpans;
                                    if (output.Report.ProbeLimitReached)
                                    {
                                        var exhausted = new InvalidDataException("Complete Core witness coverage exceeds the operation budget. No partial candidate can be offered.");
                                        exhausted.Data["OptimizationBudget"] = "Core witness coverage"; throw exhausted;
                                    }
                                    var tracks = output.Tracks.Select(t => new ControlTrack { Bone = t.Bone, Channels = t.Channels.Select(c => c.Select(k => new ControlKey { Frame = k.Frame, Value = OptimizationFloat(k.ValueBits), Tangent = OptimizationFloat(k.TangentBits) }).ToArray()).ToArray() }).ToArray();
                                    // Retain proof/protection receipts, not a second
                                    // raw-key object graph for every retry.
                                    output.Tracks = null;
                                    var options = new NativeAnimationOptimizationOptions { ExactOnly = !approximate, SmallFile = approximate && smallFile, LengthScale = length, OriginalStoredBytes = storedBytes, OriginalStoredResourceBytes = storedResourceBytes,
                                        TargetBones = model.AllBones.Select(b => b.Name).ToArray(), ProtectedBones = output.ProtectedBones, ExactBones = output.ExactBones,
                                        BindPoseCleanupBones = output.RemovedBindTracks, DeferSizeValidation = compact,
                                        MandatoryFrames = approximate && smallFile ? new int[0] : mandatory.ToArray(),
                                        FitJoinFrames = approximate && smallFile ? mandatory.ToArray() : new int[0], ProbeFrames = output.Report.ProbeFrames, Budget = budget };
                                    OptimizationNativeChecks++;
                                    var verified = await Task.Run(() => approximate
                                        ? NativeAnimationOptimization.PrepareReduction(validationModel, baseline, tracks, exactReductionTracks, options, stage, token)
                                        : NativeAnimationOptimization.Prepare(validationModel, baseline, tracks, options, stage, token), token);
                                    // Reuse the already allocated, independently
                                    // verified Exact wire for partial HQ fallback.
                                    // Its keys remain immutable within this Analyze.
                                    if (!approximate) exactReductionTracks = tracks;
                                    if (compact)
                                    {
                                        // Packing has its own frozen-baseline
                                        // certificate. A rejected approximate
                                        // reduction must not prevent bounded
                                        // storage of a verified Exact result.
                                        options.ExactOnly = !highQuality;
                                        options.SmallFile = highQuality && smallFile;
                                        options.MandatoryFrames = smallFile ? new int[0] : mandatory.ToArray();
                                        options.FitJoinFrames = smallFile ? mandatory.ToArray() : new int[0];
                                        using (var precise = verified)
                                        {
                                            async Task<NativeCompactCurveCertificate> Certificate(CHR0Node decoded, CancellationToken proofToken)
                                            {
                                                proofToken.ThrowIfCancellationRequested();
                                                if (work >= 2000000) return new NativeCompactCurveCertificate { BudgetExhausted = true, Report = "The shared two-million-work continuous proof budget is exhausted." };
                                                string certificateInput = Path.Combine(stage, "certificate-" + Guid.NewGuid().ToString("N") + ".json"), certificateOutput = Path.Combine(stage, "certificate-" + Guid.NewGuid().ToString("N") + ".json");
                                                try
                                                {
                                                    var certificateRequest = new { baseline.Name, SampleCount = baseline.FrameCount, baseline.Loop, HighQuality = highQuality, SmallFile = smallFile, LengthScale = length,
                                                        PrecisionScale = 1, MaximumWork = 2000000 - work, MaximumCandidateSpans = 100000 - spans,
                                                        Tracks = wire, CandidateTracks = OptimizationWireTracks(decoded), BindPoses = bindPoses,
                                                        ExactBones = output.ExactBones, ProtectedBones = output.ProtectedBones, MandatoryFrames = smallFile ? new int[0] : mandatory.ToArray(),
                                                        FitJoinFrames = smallFile ? mandatory.ToArray() : new int[0], ProtectedRanges = ranges, ProtectionKnown = protectionKnown };
                                                    File.WriteAllText(certificateInput, json.Serialize(certificateRequest));
                                                    var response = await session.Send(new { Operation = "animation-optimize-prepare", Model = modelPath, State = controls, Optimization = new { InputPath = certificateInput, OutputPath = certificateOutput } });
                                                    proofToken.ThrowIfCancellationRequested();
                                                    if (response.OptimizationPath != certificateOutput || new FileInfo(certificateOutput).Length > 256L * 1024 * 1024) throw new InvalidDataException("Invalid compact scalar proof response.");
                                                    var proof = json.Deserialize<OptimizationCertificateOutput>(File.ReadAllText(certificateOutput))?.Certificate;
                                                    if (proof == null || proof.Work < 0 || proof.Work > 2000000 - work) throw new InvalidDataException("Invalid shared compact scalar proof budget receipt.");
                                                    work += proof.Work;
                                                    return new NativeCompactCurveCertificate { Accepted = proof.Accepted, BudgetExhausted = proof.WorkLimitReached,
                                                        ProbeFrames = proof.ProbeFrames ?? new double[0], Report = proof.Reason + "\r\n" + string.Join("\r\n", (proof.Channels ?? new OptimizationChannelInfo[0]).Select(c => c.Bone + " / " + c.Channel + ": continuous value ≤ " + c.CertifiedValueErrorBound.ToString("G6") + "; derivative ≤ " + c.CertifiedSpeedErrorBound.ToString("G6"))) };
                                                }
                                                finally { foreach (string file in new[] { certificateInput, certificateOutput }) if (File.Exists(file)) File.Delete(file); }
                                            }
                                            verified = await Task.Run(() => NativeAnimationOptimization.PrepareCompact(validationModel, baseline, before, precise, options, stage, Certificate, denseProofBudget, token), token);
                                        }
                                    }
                                    try { token.ThrowIfCancellationRequested(); current(); receipts.Add(verified, output); return verified; }
                                    catch { verified.Dispose(); throw; }
                                }
                                finally
                                {
                                    foreach (string generated in new[] { inputPath, outputPath })
                                        try { if (File.Exists(generated)) File.Delete(generated); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                                }
                            }
                            _workspaceBusy = true;
                            try
                            {
                                using (var choice = await NativeAnimationOptimizationPreparation.Prepare(highQuality, currentBytes, Attempt, token))
                                {
                                    token.ThrowIfCancellationRequested(); current();
                                    var verified = choice.Result;
                                    string common = (smallFile ? choice.Report.Replace("High quality", "Small file") : choice.Report) +
                                        (cached && sessionBaseline.SmallFile && !smallFile ? "\r\nThe current result used Small file limits. This stricter analysis still uses the original session baseline; an unchanged result is not a new strict-quality certification." : "") +
                                        "\r\nBaseline: " + (cached && highQuality ? "retained original session animation" : "current animation frozen at Analyze") +
                                        "\r\nBaseline content SHA-256: " + NativeAnimationOptimization.ContentKey(baseline) +
                                        "\r\nProtection context: " + (protectionKnown ? "reviewed exact native rig bindings" : "unavailable; no motion/contact roles inferred, Exact cleanup only") +
                                        "\r\nAggregate scalar proof work " + work + "/2000000; spans " + spans + "/100000; native witnesses " + budget.Samples + "/100000, bone visits " + budget.BoneVisits + "/10000000, skin visits " + budget.VertexVisits + "/1000000000.";
                                    if (verified == null)
                                    {
                                        window.PreparedCanApply = false; window.PreparedSummary = "No useful verified saving for this animation; the current native content is unchanged.";
                                        window.PreparedReport = common; return NativeAnimationOptimization.Copy(before);
                                    }
                                    var output = receipts[verified];
                                    bool changed = verified.Candidate != null && (NativeAnimationOptimization.ContentKey(verified.Candidate) != content || NativeAnimationOptimization.StorageKey(verified.Candidate) != storageContent);
                                    prepared = verified; choice.Result = null; preparedContent = verified.ContentKey; preparedStorage = NativeAnimationOptimization.StorageKey(verified.Candidate); preparedQuality = highQuality; preparedSmallFile = smallFile; preparedCompact = compact;
                                    window.PreparedCanApply = changed;
                                    window.PreparedSummary = changed ? "Keys " + before.Children.OfType<CHR0EntryNode>().Sum(t => t.KeyArrays.Sum(a => a.KeyCount)) + " → " + verified.AfterKeys + " · current export " + currentBytes + " → " + verified.AfterBytes + " bytes. Review and Apply optimization."
                                        : "No useful saving for this animation; the current native content is unchanged.";
                                    window.PreparedSummary += verified.Samples == 0
                                        ? "\r\nExact decoded coefficient identity; storage changes add no motion approximation."
                                        : "\r\nMeasured: Euler " + verified.MaximumEulerError.ToString("G4") + "° · translation " + verified.MaximumLocalTranslationError.ToString("G4") + " · scale " + verified.MaximumScaleError.ToString("G4") + " · skin " + verified.MaximumSkinError.ToString("G4") + ".";
                                    window.PreparedReport = "Algorithm: " + output.Report.Version + "\r\n" + common + "\r\n" + verified.Report +
                                        "\r\nCandidate native CHR0 SHA-256: " + verified.EncodedSha256 +
                                        "\r\nAuthentic bind-geometry diagonal L: " + length.ToString("G9") + ". Frozen " + (smallFile ? "Small file" : "High quality") +
                                        " ceilings: Euler/orientation " + (smallFile ? "0.1°" : "0.01°") + ", scale " + (smallFile ? "0.0001" : "0.00001") +
                                        ", local translation vector " + (smallFile ? .002 : Math.Min(.0002, length > 0 ? length * .0001 : .0002)).ToString("G6") +
                                        "; bone/skin " + (length > 0 ? length * (smallFile ? .001 : .0001) : .0002).ToString("G6") +
                                        "; Euler derivative " + (smallFile ? "0.1°" : "0.05°") + "/frame; translation/scale derivative uses the corresponding value ceiling per frame." +
                                        (smallFile ? "\r\nFitting join poses/tangents may change within these bounds. Source-owned motion controls and authored pin/contact protections remain exact. Timing and stored loop state remain unchanged." : "") +
                                        "\r\nAbsent-target tracks to remove: " + (output.RemovedUnmatchedTracks.Length == 0 ? "none" : string.Join(", ", output.RemovedUnmatchedTracks)) +
                                        "\r\nExact bind-pose tracks to remove: " + (output.RemovedBindTracks.Length == 0 ? "none" : string.Join(", ", output.RemovedBindTracks)) +
                                        "\r\nExact-only controls: " + string.Join(", ", output.ExactBones) + "\r\nProtected tracks and dependency paths: " + string.Join(", ", output.ProtectedBones) +
                                        "\r\nImported rotation tracks retained with their complete stored data: " + (retainedDense.Length == 0 ? "none" : string.Join(", ", retainedDense)) +
                                        "\r\nContinuous scalar proof: " + output.Report.Claim + "\r\nChosen candidate work " + output.Report.Work + "; spans " + output.Report.CandidateSpans + "; merges " + output.Report.AcceptedMerges +
                                        (output.Report.WorkLimitReached || output.Report.CandidateLimitReached ? "; resource-limited, fully verified best candidate only" : "") +
                                        "\r\nNo PAC/PCS or Melee size/game claim. Current clip metadata and native flags retained.\r\n" +
                                        "Core reduction proposal before native per-bone fallback and storage verification (final key/byte counts are above):\r\n" +
                                        string.Join("\r\n", (output.Report.Channels ?? new OptimizationChannelInfo[0]).Select(c => c.Bone + " / " + c.Channel + ": " + c.BeforeKeys + " → " + c.AfterKeys + " · " + c.Policy + " · certified scalar value ≤ " + c.CertifiedValueErrorBound.ToString("G6") + ", derivative/frame ≤ " + c.CertifiedSpeedErrorBound.ToString("G6") + " · " + c.RetainedReason));
                                    return NativeAnimationOptimization.Copy(verified.Candidate ?? before);
                                }
                            }
                            finally
                            {
                                _workspaceBusy = false; UpdateFighterStatus();
                            }
                        };
                        window.Apply = (highQuality, candidate) =>
                        {
                            current(); if (_workspaceBusy || prepared == null || prepared.Candidate == null || highQuality != preparedQuality || window.ReadSmallFile() != preparedSmallFile || window.ReadCompactStorage() != preparedCompact ||
                                NativeAnimationOptimization.ContentKey(candidate) != preparedContent || NativeAnimationOptimization.StorageKey(candidate) != preparedStorage) throw new InvalidOperationException("Analyze the current animation again before Apply.");
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
                            using (var cacheCommit = PrepareOptimizationCacheMode(sessionKey, sessionBaseline, qualityBaseline,
                                modelContent, cacheProtection, preparedContent, preparedStorage, highQuality, preparedSmallFile, preparedCompact, window.PreparedReport, stage))
                            {
                                try
                                {
                                    Restore(true); if (NativeAnimationOptimization.ContentKey(source) != preparedContent || NativeAnimationOptimization.StorageKey(source) != preparedStorage) throw new InvalidDataException("The native Apply did not retain the independently verified content and storage.");
                                    editor.RecordEditorCommand(() => Restore(false), () => Restore(true));
                                }
                                catch { Restore(false); _fighterDirty = dirty; source.RootNode.IsDirty = nativeDirty; foreach (var approval in approvals) approval.Row.Approved = approval.Approved; throw; }
                                CommitOptimizationCache(cacheCommit);
                            }
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
