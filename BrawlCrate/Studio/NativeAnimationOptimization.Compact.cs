using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BrawlCrate.Studio
{
    internal sealed class NativeCompactCurveCertificate
    {
        public bool Accepted { get; set; }
        public bool BudgetExhausted { get; set; }
        public string Report { get; set; }
        public double[] ProbeFrames { get; set; } = new double[0];
        public string FailedBone { get; set; }
        public int? FailedChannel { get; set; }
    }

    internal static partial class NativeAnimationOptimization
    {
        // Shared scalar/native budgets remain the governing limits. This extra
        // bound also caps failed scalar-only partition work on large catalogues.
        private const int MaximumCompactQuantizedAttempts = 24;

        private sealed class CompactStoragePartition
        {
            internal NativeCompactStorageGroup[][] Choices;
            internal int Precision;
            internal long Order;
            internal long Savings => Choices.Sum(c => (long)c[Math.Min(Precision, c.Length - 1)].BeforeBytes - c[Math.Min(Precision, c.Length - 1)].AfterBytes);
        }

        // Every returned result is independently encoded/reopened. Quantized
        // proposals need both the complete Core certificate against the retained
        // baseline and collective native hierarchy/skin verification. Intermediate
        // proposals are never document-owned and never eligible for Apply.
        public static Task<NativeAnimationOptimizationResult> PrepareCompact(MDL0Node modelSnapshot,
            CHR0Node retainedBaseline, CHR0Node currentSnapshot, NativeAnimationOptimizationResult verifiedPrecise,
            NativeAnimationOptimizationOptions options, string stageDirectory,
            Func<CHR0Node, CancellationToken, Task<NativeCompactCurveCertificate>> certify,
            CancellationToken token = default(CancellationToken))
            => PrepareCompact(modelSnapshot, retainedBaseline, currentSnapshot, verifiedPrecise, options, stageDirectory,
                certify, new NativeDenseProofBudget(), token);

        public static async Task<NativeAnimationOptimizationResult> PrepareCompact(MDL0Node modelSnapshot,
            CHR0Node retainedBaseline, CHR0Node currentSnapshot, NativeAnimationOptimizationResult verifiedPrecise,
            NativeAnimationOptimizationOptions options, string stageDirectory,
            Func<CHR0Node, CancellationToken, Task<NativeCompactCurveCertificate>> certify,
            NativeDenseProofBudget denseProofBudget, CancellationToken token = default(CancellationToken))
        {
            if (modelSnapshot == null || retainedBaseline == null || currentSnapshot == null || verifiedPrecise == null || options == null || certify == null)
                throw new ArgumentNullException("Compact optimization requires frozen native snapshots, a verified precise result and the independent scalar certifier.");
            token.ThrowIfCancellationRequested(); ValidateMetadata(retainedBaseline); ValidateMetadata(currentSnapshot);
            denseProofBudget = denseProofBudget ?? new NativeDenseProofBudget();
            int attemptLimit = options.CompactCandidateAttemptLimit;
            if (attemptLimit < 1 || attemptLimit > MaximumCompactQuantizedAttempts)
                throw new ArgumentOutOfRangeException(nameof(options.CompactCandidateAttemptLimit));
            if (currentSnapshot.Version != 4 && currentSnapshot.Version != 5) throw new NotSupportedException("Compact native storage qualifies Brawl CHR0 v4/v5 only.");
            var bones = modelSnapshot.AllBones.ToArray(); var targetNames = Set(options.TargetBones);
            if (bones.Length == 0 || bones.Length > MaximumBones || !targetNames.SetEquals(bones.Select(b => b.Name)))
                throw new InvalidDataException("Compact storage's frozen native target membership is invalid.");
            var baselineKey = ContentKey(retainedBaseline); var currentKey = ContentKey(currentSnapshot); var currentStorage = StorageKey(currentSnapshot);
            var budget = options.Budget ?? new NativeAnimationOptimizationBudget();
            var frozenOptions = CompactOptions(options, options.ProbeFrames, budget);
            var reviewedSites = options.AllowGeneratedPairReduction || options.AllowSpanRefitting ?
                options.GeneratedPairSites ?? new NativeGeneratedPairSite[0] : new NativeGeneratedPairSite[0];
            if (reviewedSites.Length > MaximumKeys) throw new InvalidDataException("Generated-pair storage evidence exceeds the native operation limit.");
            var reviewedGroups = reviewedSites.Where(p => p != null && p.Bone != null && p.Channel >= 0 && p.Channel < 9)
                .GroupBy(p => (p.Bone, Group: p.Channel / 3)).ToDictionary(g => g.Key, g => g.ToArray());
            string stage = Path.Combine(Path.GetFullPath(stageDirectory), "compact-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            NativeAnimationOptimizationResult best = null; CHR0Node initial = null;
            int exactGroups = 0, quantizedGroups = 0, attempted = 0, selectiveAttempts = 0; bool stoppedByBudget = false, stoppedByAttemptLimit = false;
            var diagnostics = new StringBuilder();
            try
            {
                long currentBytes, currentBlock;
                using (var currentCopy = CopyForStorageVerification(currentSnapshot))
                {
                    string storedCurrentKey = ContentKey(currentCopy);
                    string path = Path.Combine(stage, "current.chr0"); currentCopy.ExportChecked(path);
                    using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
                    {
                        if (decoded == null || ContentKey(decoded) != storedCurrentKey) throw new InvalidDataException("The current native storage did not independently retain its content.");
                        currentBytes = new FileInfo(path).Length; currentBlock = ResourceBlockBytes(path, decoded);
                    }
                }
                initial = Copy(verifiedPrecise.Candidate ?? retainedBaseline);
                if (ContentKey(initial) != verifiedPrecise.ContentKey)
                    throw new InvalidDataException("The independently prepared precise result no longer identifies its complete candidate content.");
                best = await VerifyCompact(initial, retainedBaseline, bones, modelSnapshot, frozenOptions, stage, certify,
                    verifiedPrecise, currentKey, currentStorage, currentBytes, currentBlock, token, true, denseProofBudget).ConfigureAwait(false);
                if (best == null) throw new InvalidDataException("The previously prepared precise candidate failed the retained-baseline scalar certificate.");
                initial.Dispose(); initial = null;

                using (var exact = Copy(best.Candidate))
                {
                    exactGroups = ApplyExactStorageChoices(exact, options.MandatoryFrames,
                        options.FitJoinFrames, options.LengthScale, token);
                    if (StorageKey(exact) != best.StorageKey)
                    {
                        try
                        {
                            var accepted = await TryVerifyCompact(exact, retainedBaseline, bones, modelSnapshot, frozenOptions,
                                stage, certify, verifiedPrecise, currentKey, currentStorage, currentBytes, currentBlock, diagnostics, token, true, denseProofBudget).ConfigureAwait(false);
                            if (accepted != null && CompactSmaller(accepted, best)) { best.Dispose(); best = accepted; }
                            else accepted?.Dispose();
                        }
                        catch (InvalidDataException error) when (error.Data.Contains("OptimizationBudget"))
                        { diagnostics.AppendLine(error.Message); stoppedByBudget = true; }
                    }
                }

                if (!options.ExactOnly && !stoppedByBudget)
                {
                    var strict = Set(options.ExactBones); strict.UnionWith(options.ProtectedBones ?? new string[0]);
                    if (options.SmallFile)
                    {
                        var baselineEntries = retainedBaseline.Children.OfType<CHR0EntryNode>().ToDictionary(e => e.Name, StringComparer.Ordinal);
                        var dense = new List<NativeCompactStorageGroup[]>(); int remainingKeys = MaximumKeys;
                        foreach (CHR0EntryNode entry in best.Candidate.Children)
                        {
                            token.ThrowIfCancellationRequested();
                            if (!baselineEntries.TryGetValue(entry.Name, out CHR0EntryNode baselineEntry)) continue;
                            var choices = NativeCompactAnimationStorage.CreateDenseAlternatives(entry, baselineEntry,
                                strict.Contains(entry.Name), options.MandatoryFrames, remainingKeys, token, denseProofBudget);
                            if (denseProofBudget.Exhausted) break;
                            if (choices.Length == 0) continue;
                            remainingKeys -= choices[0].Curves.Where(c => c != null).Sum(c => c.EncodedSampleCount);
                            dense.Add(choices);
                        }
                        dense = dense.OrderByDescending(c => c[0].BeforeBytes - c[0].AfterBytes).ToList();
                        var pending = new List<CompactStoragePartition>(); long order = 0; bool denseBatchKept = false;
                        for (int precision = 0; precision < 2 && dense.Count > 0; precision++)
                            pending.Add(new CompactStoragePartition { Choices = dense.ToArray(), Precision = precision, Order = order++ });
                        int denseAttempts = 0;
                        // Dense proposals receive an explicitly bounded prefix
                        // of the same 24 attempts. Indexed fallback still has a
                        // chance; it is not hidden behind a four-format index.
                        while (pending.Count > 0 && !stoppedByBudget && denseAttempts < 8 && attempted < attemptLimit)
                        {
                            var partition = pending.OrderBy(p => p.Order).First(); pending.Remove(partition); bool kept = false;
                            long proposedKeys = CountKeys(best.Candidate);
                            foreach (var choices in partition.Choices)
                            {
                                var proposal = choices[Math.Min(partition.Precision, choices.Length - 1)];
                                var entry = best.Candidate.Children.OfType<CHR0EntryNode>().Single(e => e.Name == proposal.Bone);
                                for (int axis = 0; axis < 3; axis++) if (proposal.Curves[axis] != null)
                                    proposedKeys += proposal.Curves[axis].KeyCount - entry.KeyArrays[proposal.Group * 3 + axis].KeyCount;
                            }
                            if (proposedKeys > MaximumKeys)
                            { diagnostics.AppendLine("Baked proposal exceeds the complete native key budget before allocation; indexed fallback retained."); denseAttempts++; attempted++; continue; }
                            using (var batch = Copy(best.Candidate))
                            {
                                foreach (var choices in partition.Choices)
                                    NativeCompactAnimationStorage.ApplyGroup(batch, choices[Math.Min(partition.Precision, choices.Length - 1)]);
                                attempted++; denseAttempts++;
                                if (CountKeys(batch) > MaximumKeys) { diagnostics.AppendLine("Baked proposal exceeds the complete native key budget; indexed fallback retained."); continue; }
                                try
                                {
                                    var accepted = await TryVerifyCompact(batch, retainedBaseline, bones, modelSnapshot, frozenOptions, stage,
                                        certify, verifiedPrecise, currentKey, currentStorage, currentBytes, currentBlock, diagnostics, token, false, denseProofBudget).ConfigureAwait(false);
                                    if (accepted != null && CompactSmaller(accepted, best))
                                    { best.Dispose(); best = accepted; quantizedGroups += partition.Choices.Length; kept = true; }
                                    else accepted?.Dispose();
                                }
                                catch (InvalidDataException error) when (error.Data.Contains("OptimizationBudget"))
                                { diagnostics.AppendLine(error.Message); stoppedByBudget = true; }
                            }
                            if (kept && partition.Choices.Length == dense.Count) { denseBatchKept = true; pending.Clear(); }
                            else if (!kept && partition.Precision == 1 && partition.Choices.Length > 1)
                            {
                                int middle = partition.Choices.Length / 2;
                                pending.Add(new CompactStoragePartition { Choices = partition.Choices.Take(middle).ToArray(), Precision = 1, Order = order++ });
                                pending.Add(new CompactStoragePartition { Choices = partition.Choices.Skip(middle).ToArray(), Precision = 1, Order = order++ });
                            }
                        }
                        if (dense.Count > 0) diagnostics.AppendLine("Baked rotation search: " + dense.Count + " eligible groups; " + denseAttempts +
                            " complete attempts; complete batch accepted: " + denseBatchKept + ". Every retained dense candidate passed both interpolation gates.");
                    }
                    var alternatives = new List<NativeCompactStorageGroup[]>();
                    foreach (CHR0EntryNode entry in best.Candidate.Children)
                    for (int group = 0; group < 3; group++)
                    {
                        token.ThrowIfCancellationRequested();
                        var storage = PreciseCHR0EntryNode.CaptureStorage(entry);
                        if (group == 1 && storage != null && storage.MatchesGroup(entry.Keyframes, group) &&
                            Enumerable.Range(3, 3).Any(c => storage[c] != null && storage[c].IsDense)) continue;
                        reviewedGroups.TryGetValue((entry.Name, group), out var groupSites);
                        var proposals = NativeCompactAnimationStorage.CreateAlternatives(entry, group, false, strict.Contains(entry.Name),
                            options.MandatoryFrames, options.LengthScale, options.SmallFile, options.FitJoinFrames, token,
                            groupSites).Where(p => !p.IsExact).ToArray();
                        if (proposals.Length > 0) alternatives.Add(proposals);
                    }
                    alternatives = alternatives.OrderByDescending(p => p[0].BeforeBytes - p[0].AfterBytes).ToList();
                    // First try the complete compact batch. A second batch uses
                    // the less quantized indexed alternative wherever available.
                    // This avoids evaluating every frame/vertex per scalar group.
                    int indexedGroups = 0;
                    InvalidDataException identifiedFailure = null;
                    for (int precision = 0; precision < 2 && alternatives.Count > 0 && !stoppedByBudget && attempted < attemptLimit; precision++)
                    {
                        token.ThrowIfCancellationRequested();
                        using (var batch = Copy(best.Candidate))
                        {
                            foreach (var choices in alternatives)
                                NativeCompactAnimationStorage.ApplyGroup(batch, choices[Math.Min(precision, choices.Length - 1)]);
                            attempted++;
                            try
                            {
                                var accepted = await TryVerifyCompact(batch, retainedBaseline, bones, modelSnapshot, frozenOptions, stage,
                                    certify, verifiedPrecise, currentKey, currentStorage, currentBytes, currentBlock, diagnostics, token, false, denseProofBudget, error => identifiedFailure = error).ConfigureAwait(false);
                                if (accepted != null && CompactSmaller(accepted, best))
                                { best.Dispose(); best = accepted; quantizedGroups += alternatives.Count; indexedGroups = alternatives.Count; break; }
                                accepted?.Dispose();
                            }
                            catch (InvalidDataException error) when (error.Data.Contains("OptimizationBudget"))
                            { diagnostics.AppendLine(error.Message); stoppedByBudget = true; break; }
                        }
                    }

                    // A typed failed scalar group identifies exactly which
                    // proposal cannot be retained. Native cumulative failures
                    // identify a bone and its actual ancestor dependencies.
                    // Exclude those proposals before bisecting unrelated groups;
                    // every repaired COMPLETE candidate still uses both gates.
                    if (indexedGroups == 0 && !stoppedByBudget && identifiedFailure != null)
                    {
                        var remaining = alternatives.ToList();
                        for (int repair = 0; repair < 6 && remaining.Count > 0 && attempted < attemptLimit; repair++)
                        {
                            token.ThrowIfCancellationRequested();
                            var excluded = CompactFailedGroups(identifiedFailure, bones, remaining);
                            if (excluded.Count == 0) break;
                            remaining.RemoveAll(choices => excluded.Contains(choices[0].Bone + "\0" + choices[0].Group));
                            if (remaining.Count == 0) break;
                            identifiedFailure = null;
                            using (var batch = Copy(best.Candidate))
                            {
                                foreach (var choices in remaining) NativeCompactAnimationStorage.ApplyGroup(batch, choices[Math.Min(1, choices.Length - 1)]);
                                attempted++; selectiveAttempts++;
                                try
                                {
                                    var accepted = await TryVerifyCompact(batch, retainedBaseline, bones, modelSnapshot, frozenOptions, stage,
                                        certify, verifiedPrecise, currentKey, currentStorage, currentBytes, currentBlock, diagnostics, token, false, denseProofBudget,
                                        error => identifiedFailure = error).ConfigureAwait(false);
                                    if (accepted != null && CompactSmaller(accepted, best))
                                    { best.Dispose(); best = accepted; quantizedGroups += remaining.Count; indexedGroups = remaining.Count; break; }
                                    accepted?.Dispose();
                                }
                                catch (InvalidDataException error) when (error.Data.Contains("OptimizationBudget"))
                                { diagnostics.AppendLine(error.Message); stoppedByBudget = true; break; }
                            }
                            if (identifiedFailure == null) break;
                        }
                    }

                    // A failing finger or ancestor must not discard unrelated
                    // useful storage. Retry deterministic high-saving subsets;
                    // each complete cumulative candidate is still certified and
                    // evaluated against the immutable retained baseline, never
                    // against the last accepted approximation.
                    if (indexedGroups == 0 && !stoppedByBudget && alternatives.Count > 1)
                    {
                        var pending = new List<CompactStoragePartition>(); long order = 0;
                        void Split(NativeCompactStorageGroup[][] choices)
                        {
                            int middle = choices.Length / 2;
                            if (middle == 0) return;
                            pending.Add(new CompactStoragePartition { Choices = choices.Take(middle).ToArray(), Precision = 1, Order = order++ });
                            pending.Add(new CompactStoragePartition { Choices = choices.Skip(middle).ToArray(), Precision = 1, Order = order++ });
                        }
                        Split(alternatives.ToArray());
                        while (pending.Count > 0 && !stoppedByBudget && attempted < attemptLimit)
                        {
                            token.ThrowIfCancellationRequested();
                            var partition = pending.OrderByDescending(p => p.Savings).ThenBy(p => p.Order).First(); pending.Remove(partition);
                            bool kept = false;
                            using (var batch = Copy(best.Candidate))
                            {
                                foreach (var choices in partition.Choices)
                                    NativeCompactAnimationStorage.ApplyGroup(batch, choices[Math.Min(partition.Precision, choices.Length - 1)]);
                                attempted++; selectiveAttempts++;
                                try
                                {
                                    var accepted = await TryVerifyCompact(batch, retainedBaseline, bones, modelSnapshot, frozenOptions, stage,
                                        certify, verifiedPrecise, currentKey, currentStorage, currentBytes, currentBlock, diagnostics, token, false, denseProofBudget).ConfigureAwait(false);
                                    if (accepted != null && CompactSmaller(accepted, best))
                                    { best.Dispose(); best = accepted; quantizedGroups += partition.Choices.Length; kept = true; }
                                    else accepted?.Dispose();
                                }
                                catch (InvalidDataException error) when (error.Data.Contains("OptimizationBudget"))
                                { diagnostics.AppendLine(error.Message); stoppedByBudget = true; }
                            }
                            if (!kept && !stoppedByBudget)
                            {
                                if (partition.Choices.Length > 1) Split(partition.Choices);
                                else if (partition.Precision > 0 && partition.Choices[0].Length > 1)
                                    pending.Add(new CompactStoragePartition { Choices = partition.Choices, Precision = 0, Order = order++ });
                            }
                        }
                        stoppedByAttemptLimit = pending.Count > 0 && !stoppedByBudget && attempted >= attemptLimit;
                    }
                }

                token.ThrowIfCancellationRequested();
                if (ContentKey(retainedBaseline) != baselineKey || ContentKey(currentSnapshot) != currentKey || StorageKey(currentSnapshot) != currentStorage)
                    throw new InvalidOperationException("Compact optimization dependencies changed during preparation.");
                RequireCompactSize(best, options);
                best.UsedApproximateStorage = quantizedGroups > 0;
                best.ReductionSelectionReport = verifiedPrecise.ReductionSelectionReport;
                best.BeforeKeys = CountKeys(currentSnapshot); best.BeforeTracks = currentSnapshot.Children.Count;
                best.BudgetSamples = budget.Samples; best.BudgetPoseEvaluations = budget.PoseEvaluations; best.BudgetBoneVisits = budget.BoneVisits; best.BudgetVertexVisits = budget.VertexVisits;
                string scalarReport = best.Report;
                best.Report = (options.SmallFile && !options.ExactOnly ?
                    "Explicit native storage: guarded L1/L2 rotation and I4/I6 where independently verified; precise I12 fallback. Baked candidates require continuous/native collective gates under both native-Hermite and independent linear interpretations.\r\n" :
                    "Explicit native indexed storage: I4/I6 where independently verified; precise I12 fallback per SRT group. No dense linear codec or automatic tangent regeneration.\r\n") +
                    (string.IsNullOrEmpty(best.ReductionSelectionReport) ? "" : best.ReductionSelectionReport + "\r\n") +
                    "Lossless packing proposals: " + exactGroups + "; accepted quantized groups: " + quantizedGroups + "; complete quantized batch attempts: " + (attempted - selectiveAttempts) +
                    "; selective cumulative attempts: " + selectiveAttempts + "/" + attemptLimit + ".\r\n" +
                    (attempted > 0 && quantizedGroups == 0 ? "Complete quantized batches were not accepted; the verified lossless/precise fallback was retained.\r\n" : "") +
                    BuildReport(best, frozenOptions).Replace("Independent I12 decode", options.SmallFile && !options.ExactOnly ? "Independent L1/L2/I4/I6/I12 decode" : "Independent I4/I6/I12 decode") +
                    "\r\nContinuous scalar certificate: " + scalarReport +
                    (stoppedByBudget ? "\r\nThe shared operation budget stopped further packing; the returned candidate already passed complete verification." : "") +
                    (stoppedByAttemptLimit ? "\r\nThe bounded storage search reached its attempt limit; the returned candidate already passed complete retained-baseline verification." : "") +
                    (options.SmallFile && !options.ExactOnly ? "\r\nDense sampling/scalar proof work: " + denseProofBudget.Work + "/2000000 (reserved from the shared operation scalar work when available)." +
                        (denseProofBudget.Exhausted ? " Further dense work was stopped; only a fully verified fallback is returned." : "") : "") +
                    (diagnostics.Length > 0 ? "\r\nRejected compact attempts (not applied):\r\n" + diagnostics : "");
                var result = best; best = null; return result;
            }
            finally
            {
                initial?.Dispose(); best?.Dispose();
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
            }
        }

        private static async Task<NativeAnimationOptimizationResult> TryVerifyCompact(CHR0Node proposed, CHR0Node baseline,
            MDL0BoneNode[] bones, MDL0Node model, NativeAnimationOptimizationOptions options, string stage,
            Func<CHR0Node, CancellationToken, Task<NativeCompactCurveCertificate>> certify, NativeAnimationOptimizationResult precise,
            string currentKey, string currentStorage, long beforeBytes, long beforeBlock, StringBuilder diagnostics, CancellationToken token, bool carryExactProof = false,
            NativeDenseProofBudget denseProofBudget = null, Action<InvalidDataException> rejected = null)
        {
            try
            {
                var result = await VerifyCompact(proposed, baseline, bones, model, options, stage, certify, precise,
                    currentKey, currentStorage, beforeBytes, beforeBlock, token, carryExactProof && ContentKey(proposed) == precise.ContentKey, denseProofBudget).ConfigureAwait(false);
                if (result == null) diagnostics.AppendLine("The complete decoded candidate did not pass the continuous scalar certificate.");
                return result;
            }
            catch (InvalidDataException error) when (Equals(error.Data["OptimizationQuality"], true))
            { diagnostics.AppendLine(error.Message); rejected?.Invoke(error); return null; }
        }

        private static async Task<NativeAnimationOptimizationResult> VerifyCompact(CHR0Node proposed, CHR0Node baseline,
            MDL0BoneNode[] bones, MDL0Node model, NativeAnimationOptimizationOptions options, string stage,
            Func<CHR0Node, CancellationToken, Task<NativeCompactCurveCertificate>> certify, NativeAnimationOptimizationResult precise,
            string currentKey, string currentStorage, long beforeBytes, long beforeBlock, CancellationToken token, bool reuseVerifiedPrecise = false,
            NativeDenseProofBudget denseProofBudget = null)
        {
            denseProofBudget = denseProofBudget ?? new NativeDenseProofBudget();
            token.ThrowIfCancellationRequested(); string expected = ContentKey(proposed), file = Path.Combine(stage, Guid.NewGuid().ToString("N") + ".chr0");
            CHR0Node decoded = null;
            try
            {
                proposed.ExportChecked(file); decoded = NodeFactory.FromFile(null, file) as CHR0Node;
                if (decoded == null || ContentKey(decoded) != expected)
                {
                    var failure = new InvalidDataException("Explicit indexed encode did not retain every proposed decoded tuple, native flag, metadata field and track order.");
                    if (!reuseVerifiedPrecise) failure.Data["OptimizationQuality"] = true;
                    throw failure;
                }
                bool hasDense = HasDenseStorage(decoded);
                var denseChanges = new HashSet<string>(StringComparer.Ordinal);
                var targetNames = new HashSet<string>(bones.Select(b => b.Name), StringComparer.Ordinal);
                RequireImportedDenseRetention(baseline, decoded, targetNames);
                if (hasDense)
                {
                    if (StorageKey(proposed) != StorageKey(decoded))
                        throw CompactQualityFailure("The complete baked payload, including its encoded terminal sample, failed independent storage identity validation.");
                    var entries = baseline.Children.OfType<CHR0EntryNode>().ToDictionary(e => e.Name, StringComparer.Ordinal);
                    foreach (var entry in decoded.Children.OfType<CHR0EntryNode>())
                    {
                        var storage = PreciseCHR0EntryNode.CaptureStorage(entry);
                        if (storage == null) continue;
                        for (int channel = 3; channel < 6; channel++)
                        {
                            var curve = storage[channel]; if (curve == null || !curve.IsDense) continue;
                            entries.TryGetValue(entry.Name, out CHR0EntryNode original);
                            var previousStorage = original == null ? null : PreciseCHR0EntryNode.CaptureStorage(original);
                            // Imported native baked payloads that are unchanged
                            // remain historical data, not new Small proposals.
                            // Their existing reader semantics are not rewritten.
                            if (previousStorage?[channel]?.ContentKey == curve.ContentKey) continue;
                            denseChanges.Add(entry.Name + "\0" + channel);
                            string proofKey = original == null ? null : denseProofBudget.ProofKey(original, channel, curve, .1, .1, token);
                            if (!SmallProfile(options) || original == null ||
                                proofKey == null ||
                                !NativeCompactAnimationStorage.DenseLinearWithinBounds(original.KeyArrays[channel].Entries.ToArray(),
                                    original.KeyArrays[channel], curve.EncodedValues, .1, .1, token, denseProofBudget, proofKey))
                                throw CompactQualityFailure("A complete baked candidate failed its retained-baseline continuous linear value/derivative gate.");
                        }
                    }
                }
                var certificate = reuseVerifiedPrecise ? new NativeCompactCurveCertificate { Accepted = true,
                    Report = "Previously qualified precise candidate retained; its complete independently decoded coefficient/metadata identity is unchanged." } :
                    await certify(decoded, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (certificate == null) throw new InvalidDataException("The independent compact curve certificate is unavailable.");
                if (certificate.BudgetExhausted)
                {
                    var exhausted = new InvalidDataException(certificate.Report ?? "The shared continuous scalar verification budget is exhausted.");
                    exhausted.Data["OptimizationBudget"] = "ContinuousScalar"; throw exhausted;
                }
                if (!certificate.Accepted)
                {
                    string reason=(certificate.Report??"No scalar reason supplied.").Split('\r','\n')[0];
                    var failed = CompactQualityFailure("The complete decoded candidate did not pass the continuous scalar certificate: "+reason.Substring(0,Math.Min(1024,reason.Length)));
                    if (certificate.FailedBone != null && certificate.FailedChannel >= 0 && certificate.FailedChannel < 9)
                    { failed.Data["ScalarBone"] = certificate.FailedBone; failed.Data["ScalarChannel"] = certificate.FailedChannel.Value; }
                    throw failed;
                }
                var result = new NativeAnimationOptimizationResult
                {
                    BeforeBytes = beforeBytes, BeforeResourceBytes = beforeBlock, AfterBytes = new FileInfo(file).Length,
                    CandidateResourceBytes = ResourceBlockBytes(file, decoded), OriginalInputResourceBytes = options.OriginalStoredResourceBytes,
                    BeforeKeys = precise.BeforeKeys, AfterKeys = CountKeys(decoded),
                    BeforeTracks = precise.BeforeTracks, AfterTracks = decoded.Children.Count, ContentKey = expected, StorageKey = StorageKey(decoded),
                    EncodedSha256 = FighterProject.HashFile(file)
                };
                result.HasChanges = expected != currentKey || result.StorageKey != currentStorage;
                var qualityOptions = CompactOptions(options, (options.ProbeFrames ?? new double[0]).Concat(certificate.ProbeFrames ?? new double[0]).Distinct().ToArray(), options.Budget);
                if (reuseVerifiedPrecise)
                {
                    foreach (var property in typeof(NativeAnimationOptimizationResult).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        if (property.CanRead && property.CanWrite && (property.Name.StartsWith("Maximum", StringComparison.Ordinal) || property.Name.StartsWith("Worst", StringComparison.Ordinal) || property.Name.StartsWith("Budget", StringComparison.Ordinal) || property.Name == "Samples"))
                            property.SetValue(result, property.GetValue(precise));
                }
                else Evaluate(model, bones, baseline, decoded, qualityOptions, result, token);
                // Reports keep the scalar and sampled native claims separate.
                result.Report = certificate.Report;
                if (denseChanges.Count > 0)
                {
                    long projectionKeys = CountKeys(decoded);
                    foreach (var entry in decoded.Children.OfType<CHR0EntryNode>())
                    {
                        var storage = PreciseCHR0EntryNode.CaptureStorage(entry); if (storage == null) continue;
                        for (int channel = 3; channel < 6; channel++) if (denseChanges.Contains(entry.Name + "\0" + channel))
                            projectionKeys += 2L * storage[channel].EncodedSampleCount - 2 - storage[channel].KeyCount;
                    }
                    if (projectionKeys > MaximumKeys) throw CompactQualityFailure("The complete linear-projection verification exceeds the native key budget.");
                    using (var projection = NativeCompactAnimationStorage.CreateLinearProjection(Copy(decoded), denseChanges))
                    {
                        if (CountKeys(projection) > MaximumKeys) throw CompactQualityFailure("The complete linear-projection verification exceeds the native key budget.");
                        var linear = new NativeAnimationOptimizationResult();
                        Evaluate(model, bones, baseline, projection, qualityOptions, linear, token);
                        result.Report += "\r\nSeparate complete LINEAR interpretation: continuous Bernstein value/derivative bounds passed. " +
                            "Collective native FK/skin verification uses the same retained baseline and shared budget; maximum Euler " + G(linear.MaximumEulerError) +
                            "°, orientation " + G(linear.MaximumOrientationError) + "°, local translation " + G(linear.MaximumLocalTranslationError) +
                            ", scale " + G(linear.MaximumScaleError) + ", world/basis " + G(linear.MaximumWorldBoneError) + ", skin " + G(linear.MaximumSkinError) +
                            "; derivative/frame Euler " + G(linear.MaximumEulerDerivativeError) + "°, orientation " + G(linear.MaximumOrientationDerivativeError) +
                            "°, world " + G(linear.MaximumWorldDerivativeError) + ", skin " + G(linear.MaximumSkinDerivativeError) + ". Actual game playback remains unverified.";
                    }
                }
                result.Candidate = decoded; decoded = null; return result;
            }
            finally { decoded?.Dispose(); if (File.Exists(file)) File.Delete(file); }
        }

        private static HashSet<string> CompactFailedGroups(InvalidDataException failure, MDL0BoneNode[] bones,
            List<NativeCompactStorageGroup[]> proposals)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (failure == null) return result;
            if (failure.Data["ScalarBone"] is string scalar && failure.Data["ScalarChannel"] is int channel && channel >= 0 && channel < 9)
            {
                string key = scalar + "\0" + channel / 3;
                if (proposals.Any(p => p[0].Bone + "\0" + p[0].Group == key)) result.Add(key);
                return result;
            }
            if (!(failure.Data["Metric"] is string metric) || !ReductionBoneMetrics.Contains(metric) || !(failure.Data["Bone"] is string name)) return result;
            var failed = bones.SingleOrDefault(b => b.Name == name);
            var seen = new HashSet<MDL0BoneNode>();
            for (var bone = failed; bone != null; bone = bone.Parent as MDL0BoneNode)
            {
                if (!seen.Add(bone)) throw new InvalidDataException("Compact recovery cannot use a cyclic native hierarchy.");
                foreach (var proposal in proposals.Where(p => p[0].Bone == bone.Name))
                    result.Add(bone.Name + "\0" + proposal[0].Group);
            }
            return result;
        }

        private static bool HasDenseStorage(CHR0Node clip) => clip.Children.OfType<CHR0EntryNode>().Any(e =>
        {
            var storage = PreciseCHR0EntryNode.CaptureStorage(e);
            return storage != null && Enumerable.Range(3, 3).Any(c => storage[c] != null && storage[c].IsDense && storage[c].Matches(e.KeyArrays[c]));
        });
        private static InvalidDataException CompactQualityFailure(string message)
        { var error = new InvalidDataException(message); error.Data["OptimizationQuality"] = true; return error; }

        private static NativeAnimationOptimizationOptions CompactOptions(NativeAnimationOptimizationOptions source,
            double[] probes, NativeAnimationOptimizationBudget budget)
        {
            var result = new NativeAnimationOptimizationOptions();
            foreach (var property in typeof(NativeAnimationOptimizationOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (property.CanRead && property.CanWrite) property.SetValue(result, property.GetValue(source));
            result.ProbeFrames = probes ?? new double[0]; result.Budget = budget; return result;
        }
        private static bool CompactSmaller(NativeAnimationOptimizationResult candidate, NativeAnimationOptimizationResult current) =>
            candidate.AfterBytes < current.AfterBytes && candidate.CandidateResourceBytes <= current.CandidateResourceBytes;
        private static void RequireCompactSize(NativeAnimationOptimizationResult result, NativeAnimationOptimizationOptions options)
        {
            if (result.AfterBytes <= result.BeforeBytes && result.CandidateResourceBytes <= result.BeforeResourceBytes &&
                (!options.OriginalStoredBytes.HasValue || result.AfterBytes <= options.OriginalStoredBytes.Value) &&
                (!options.OriginalStoredResourceBytes.HasValue || result.CandidateResourceBytes <= options.OriginalStoredResourceBytes.Value)) return;
            var error = new InvalidDataException("No fully verified compact candidate fits the current and original native resource byte caps. No changes were applied.");
            error.Data["OptimizationSize"] = true; error.Data["BeforeBytes"] = result.BeforeBytes; error.Data["AfterBytes"] = result.AfterBytes;
            error.Data["BeforeResourceBytes"] = result.BeforeResourceBytes; error.Data["CandidateResourceBytes"] = result.CandidateResourceBytes; throw error;
        }
    }
}
