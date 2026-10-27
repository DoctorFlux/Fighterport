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
        public static async Task<NativeAnimationOptimizationResult> PrepareCompact(MDL0Node modelSnapshot,
            CHR0Node retainedBaseline, CHR0Node currentSnapshot, NativeAnimationOptimizationResult verifiedPrecise,
            NativeAnimationOptimizationOptions options, string stageDirectory,
            Func<CHR0Node, CancellationToken, Task<NativeCompactCurveCertificate>> certify,
            CancellationToken token = default(CancellationToken))
        {
            if (modelSnapshot == null || retainedBaseline == null || currentSnapshot == null || verifiedPrecise == null || options == null || certify == null)
                throw new ArgumentNullException("Compact optimization requires frozen native snapshots, a verified precise result and the independent scalar certifier.");
            token.ThrowIfCancellationRequested(); ValidateMetadata(retainedBaseline); ValidateMetadata(currentSnapshot);
            if (currentSnapshot.Version != 4 && currentSnapshot.Version != 5) throw new NotSupportedException("Compact native storage qualifies Brawl CHR0 v4/v5 only.");
            var bones = modelSnapshot.AllBones.ToArray(); var targetNames = Set(options.TargetBones);
            if (bones.Length == 0 || bones.Length > MaximumBones || !targetNames.SetEquals(bones.Select(b => b.Name)))
                throw new InvalidDataException("Compact storage's frozen native target membership is invalid.");
            var baselineKey = ContentKey(retainedBaseline); var currentKey = ContentKey(currentSnapshot); var currentStorage = StorageKey(currentSnapshot);
            var budget = options.Budget ?? new NativeAnimationOptimizationBudget();
            var frozenOptions = CompactOptions(options, options.ProbeFrames, budget);
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
                    verifiedPrecise, currentKey, currentStorage, currentBytes, currentBlock, token, true).ConfigureAwait(false);
                if (best == null) throw new InvalidDataException("The previously prepared precise candidate failed the retained-baseline scalar certificate.");
                initial.Dispose(); initial = null;

                using (var exact = Copy(best.Candidate))
                {
                    foreach (CHR0EntryNode entry in exact.Children.ToArray())
                    for (int group = 0; group < 3; group++)
                    {
                        token.ThrowIfCancellationRequested();
                        var proposal = NativeCompactAnimationStorage.CreateAlternatives(entry, group, true, true,
                            options.MandatoryFrames, options.LengthScale, options.SmallFile, options.FitJoinFrames, token).FirstOrDefault();
                        if (proposal == null) continue;
                        NativeCompactAnimationStorage.ApplyGroup(exact, proposal); exactGroups++;
                    }
                    if (StorageKey(exact) != best.StorageKey)
                    {
                        try
                        {
                            var accepted = await TryVerifyCompact(exact, retainedBaseline, bones, modelSnapshot, frozenOptions,
                                stage, certify, verifiedPrecise, currentKey, currentStorage, currentBytes, currentBlock, diagnostics, token, true).ConfigureAwait(false);
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
                    var alternatives = new List<NativeCompactStorageGroup[]>();
                    foreach (CHR0EntryNode entry in best.Candidate.Children)
                    for (int group = 0; group < 3; group++)
                    {
                        token.ThrowIfCancellationRequested();
                        var proposals = NativeCompactAnimationStorage.CreateAlternatives(entry, group, false, strict.Contains(entry.Name),
                            options.MandatoryFrames, options.LengthScale, options.SmallFile, options.FitJoinFrames, token).Where(p => !p.IsExact).ToArray();
                        if (proposals.Length > 0) alternatives.Add(proposals);
                    }
                    alternatives = alternatives.OrderByDescending(p => p[0].BeforeBytes - p[0].AfterBytes).ToList();
                    // First try the complete compact batch. A second batch uses
                    // the less quantized indexed alternative wherever available.
                    // This avoids evaluating every frame/vertex per scalar group.
                    for (int precision = 0; precision < 2 && alternatives.Count > 0; precision++)
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
                                    certify, verifiedPrecise, currentKey, currentStorage, currentBytes, currentBlock, diagnostics, token).ConfigureAwait(false);
                                if (accepted != null && CompactSmaller(accepted, best))
                                { best.Dispose(); best = accepted; quantizedGroups = alternatives.Count; break; }
                                accepted?.Dispose();
                            }
                            catch (InvalidDataException error) when (error.Data.Contains("OptimizationBudget"))
                            { diagnostics.AppendLine(error.Message); stoppedByBudget = true; break; }
                        }
                    }

                    // A failing finger or ancestor must not discard unrelated
                    // useful storage. Retry deterministic high-saving subsets;
                    // each complete cumulative candidate is still certified and
                    // evaluated against the immutable retained baseline, never
                    // against the last accepted approximation.
                    if (quantizedGroups == 0 && !stoppedByBudget && alternatives.Count > 1)
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
                        while (pending.Count > 0 && !stoppedByBudget && attempted < MaximumCompactQuantizedAttempts)
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
                                        certify, verifiedPrecise, currentKey, currentStorage, currentBytes, currentBlock, diagnostics, token).ConfigureAwait(false);
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
                        stoppedByAttemptLimit = pending.Count > 0 && !stoppedByBudget && attempted >= MaximumCompactQuantizedAttempts;
                    }
                }

                token.ThrowIfCancellationRequested();
                if (ContentKey(retainedBaseline) != baselineKey || ContentKey(currentSnapshot) != currentKey || StorageKey(currentSnapshot) != currentStorage)
                    throw new InvalidOperationException("Compact optimization dependencies changed during preparation.");
                RequireCompactSize(best, options);
                best.UsedApproximateStorage = quantizedGroups > 0;
                best.ReductionSelectionReport = verifiedPrecise.ReductionSelectionReport;
                best.BeforeKeys = CountKeys(currentSnapshot); best.BeforeTracks = currentSnapshot.Children.Count;
                best.BudgetSamples = budget.Samples; best.BudgetBoneVisits = budget.BoneVisits; best.BudgetVertexVisits = budget.VertexVisits;
                string scalarReport = best.Report;
                best.Report = "Explicit native indexed storage: I4/I6 where independently verified; precise I12 fallback per SRT group. No dense linear codec or automatic tangent regeneration.\r\n" +
                    (string.IsNullOrEmpty(best.ReductionSelectionReport) ? "" : best.ReductionSelectionReport + "\r\n") +
                    "Lossless packing proposals: " + exactGroups + "; accepted quantized groups: " + quantizedGroups + "; complete quantized batch attempts: " + (attempted - selectiveAttempts) +
                    "; selective cumulative attempts: " + selectiveAttempts + "/" + MaximumCompactQuantizedAttempts + ".\r\n" +
                    (attempted > 0 && quantizedGroups == 0 ? "Complete quantized batches were not accepted; the verified lossless/precise fallback was retained.\r\n" : "") +
                    BuildReport(best, frozenOptions).Replace("Independent I12 decode", "Independent I4/I6/I12 decode") +
                    "\r\nContinuous scalar certificate: " + scalarReport +
                    (stoppedByBudget ? "\r\nThe shared operation budget stopped further packing; the returned candidate already passed complete verification." : "") +
                    (stoppedByAttemptLimit ? "\r\nThe bounded storage search reached its attempt limit; the returned candidate already passed complete retained-baseline verification." : "") +
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
            string currentKey, string currentStorage, long beforeBytes, long beforeBlock, StringBuilder diagnostics, CancellationToken token, bool carryExactProof = false)
        {
            try
            {
                var result = await VerifyCompact(proposed, baseline, bones, model, options, stage, certify, precise,
                    currentKey, currentStorage, beforeBytes, beforeBlock, token, carryExactProof && ContentKey(proposed) == precise.ContentKey).ConfigureAwait(false);
                if (result == null) diagnostics.AppendLine("The complete decoded candidate did not pass the continuous scalar certificate.");
                return result;
            }
            catch (InvalidDataException error) when (Equals(error.Data["OptimizationQuality"], true))
            { diagnostics.AppendLine(error.Message); return null; }
        }

        private static async Task<NativeAnimationOptimizationResult> VerifyCompact(CHR0Node proposed, CHR0Node baseline,
            MDL0BoneNode[] bones, MDL0Node model, NativeAnimationOptimizationOptions options, string stage,
            Func<CHR0Node, CancellationToken, Task<NativeCompactCurveCertificate>> certify, NativeAnimationOptimizationResult precise,
            string currentKey, string currentStorage, long beforeBytes, long beforeBlock, CancellationToken token, bool reuseVerifiedPrecise = false)
        {
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
                if (!certificate.Accepted) return null;
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
                result.Candidate = decoded; decoded = null; return result;
            }
            finally { decoded?.Dispose(); if (File.Exists(file)) File.Delete(file); }
        }

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
