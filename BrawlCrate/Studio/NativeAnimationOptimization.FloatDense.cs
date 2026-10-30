using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BrawlCrate.Studio
{
    // Immutable local view of already frozen evidence. Duplicate/conflicting
    // claims remain present; unrelated bones merely stop being rescanned for
    // every axis. The complete source list still goes to the Core certificate.
    internal sealed class NativeDensePairEvidenceIndex
    {
        private readonly Dictionary<string, NativeGeneratedPairSite[]> byBone;
        private NativeDensePairEvidenceIndex(Dictionary<string, NativeGeneratedPairSite[]> value) { byBone = value; }
        internal NativeGeneratedPairSite[] ForBone(string bone) => bone != null && byBone.TryGetValue(bone, out var sites) ? sites : new NativeGeneratedPairSite[0];
        internal static NativeDensePairEvidenceIndex Create(NativeGeneratedPairSite[] frozen, NativeDenseProofBudget proof, CancellationToken token)
        {
            frozen = frozen ?? new NativeGeneratedPairSite[0];
            if (frozen.Length > 2000000) throw new InvalidDataException("Generated-pair evidence exceeds its existing global operation bound.");
            if (!proof.Spend(frozen.Length, token))
            { var error = new InvalidDataException("The shared scalar budget cannot freeze complete pair evidence."); error.Data["OptimizationBudget"] = "Pair evidence index"; throw error; }
            var grouped = new Dictionary<string, List<NativeGeneratedPairSite>>(StringComparer.Ordinal);
            foreach (var site in frozen)
            {
                token.ThrowIfCancellationRequested(); if (site?.Bone == null) continue;
                if (!grouped.TryGetValue(site.Bone, out var values)) grouped.Add(site.Bone, values = new List<NativeGeneratedPairSite>());
                values.Add(new NativeGeneratedPairSite { Bone = site.Bone, Channel = site.Channel, Frame = site.Frame });
            }
            return new NativeDensePairEvidenceIndex(grouped.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal));
        }
    }

    internal static partial class NativeAnimationOptimization
    {
        // A new L4 proposal is a claim about actual native bytes, not merely
        // projected key values. Freeze that claim before serialization, then
        // verify it even if the reopened writer output has no dense storage.
        internal sealed class FloatDenseExportPlanSet
        {
            private readonly string completeStorageKey;
            private readonly Dictionary<string, string[]> groups;
            internal FloatDenseExportPlanSet(string key, Dictionary<string, string[]> plans)
            {
                completeStorageKey = key;
                groups = plans.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
            }
            internal bool Matches(CHR0Node decoded)
            {
                if (decoded == null || StorageKey(decoded) != completeStorageKey) return false;
                foreach (var pair in groups)
                {
                    var entries = decoded.Children.OfType<CHR0EntryNode>().Where(entry => entry.Name == pair.Key).ToArray();
                    if (entries.Length != 1) return false;
                    var decodedEntry = entries[0]; var stored = PreciseCHR0EntryNode.CaptureStorage(decodedEntry);
                    if (stored == null || stored.Format(1) != AnimDataFormat.L4 || !stored.MatchesGroup(decodedEntry.Keyframes, 1)) return false;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        var curve = stored[3 + axis];
                        if (curve?.ContentKey != pair.Value[axis] || curve != null && curve.Format != AnimDataFormat.L4) return false;
                    }
                }
                return true;
            }
        }

        internal static FloatDenseExportPlanSet CaptureFloatDenseExportPlans(CHR0Node proposed, CHR0Node baseline)
        {
            var originals = baseline.Children.OfType<CHR0EntryNode>().ToDictionary(entry => entry.Name, StringComparer.Ordinal);
            var plans = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var entry in proposed.Children.OfType<CHR0EntryNode>())
            {
                var storage = PreciseCHR0EntryNode.CaptureStorage(entry);
                if (storage == null || !Enumerable.Range(3, 3).Any(channel => storage[channel]?.Format == AnimDataFormat.L4)) continue;
                originals.TryGetValue(entry.Name, out var original);
                var before = original == null ? null : PreciseCHR0EntryNode.CaptureStorage(original);
                if (Enumerable.Range(3, 3).All(channel => storage[channel]?.ContentKey == before?[channel]?.ContentKey)) continue;
                // Do not use StorageKey alone for this decision: it calls an
                // unmatched planned group I12, hiding a stale L4 format claim.
                if (storage.Format(1) != AnimDataFormat.L4 || !storage.MatchesGroup(entry.Keyframes, 1))
                    throw CompactQualityFailure("The proposed new L4 whole-group plan is stale or has a noncanonical fixed companion; no format fallback is accepted as L4.");
                plans.Add(entry.Name, Enumerable.Range(3, 3).Select(channel => storage[channel]?.ContentKey).ToArray());
            }
            return plans.Count == 0 ? null : new FloatDenseExportPlanSet(StorageKey(proposed), plans);
        }

        internal static void RequireFloatDenseExportPlans(CHR0Node decoded, FloatDenseExportPlanSet plans)
        {
            if (plans != null && !plans.Matches(decoded))
                throw CompactQualityFailure("The fresh native output did not retain every proposed new whole-group L4 payload and its complete explicit storage identity; an I12 fallback is not an L4 result.");
        }

        private sealed class FloatDenseExportSearch : IDisposable
        {
            internal NativeAnimationOptimizationResult Result;
            internal int Attempts, Groups;
            internal int EligibleGroups;
            internal bool EligibilityComplete;
            internal bool BudgetExhausted;
            internal string Report;
            public void Dispose() { Result?.Dispose(); Result = null; }
        }

        // ExportSettings is supplied only at the explicit independent export
        // preparation boundary. Existing retarget/fitting master packing and
        // ordinary native Save/Export never opt into this search.
        private static bool FloatDenseExportEnabled(NativeAnimationOptimizationOptions options)
            => options != null && !options.ExactOnly && options.ExportSettings != null &&
                options.ExportSettings.Policy == NativeExportOptimizationPolicy.ErrorBounded;

        private static NativeGeneratedPairSite[] FloatDenseExportPairEvidence(NativeAnimationOptimizationOptions options)
            => SmallProfile(options) || options.AllowGeneratedPairReduction || options.AllowSpanRefitting
                ? options.GeneratedPairSites ?? new NativeGeneratedPairSite[0] : new NativeGeneratedPairSite[0];

        private static async Task<FloatDenseExportSearch> SearchFloatDenseExportStorage(CHR0Node baseline,
            NativeAnimationOptimizationResult held, NativeAnimationOptimizationResult precise,
            MDL0BoneNode[] bones, MDL0Node model, NativeAnimationOptimizationOptions options,
            string stage, Func<CHR0Node, CancellationToken, Task<NativeCompactCurveCertificate>> certify,
            string currentKey, string currentStorage, long currentBytes, long currentBlock,
            int remainingAttempts, NativeDenseProofBudget proof, CancellationToken token)
        {
            var search = new FloatDenseExportSearch();
            if (!FloatDenseExportEnabled(options) || remainingAttempts <= 0 || held.Candidate == null || baseline.Loop) return search;
            options.ExportSettings.Validate();
            var notes = new StringBuilder();
            try
            {
                var originals = baseline.Children.OfType<CHR0EntryNode>().ToDictionary(e => e.Name, StringComparer.Ordinal);
                var exact = Set(options.ExactBones); exact.UnionWith(options.ProtectedBones ?? new string[0]);
                var mandatory = (options.MandatoryFrames ?? new int[0]).Concat(options.FitJoinFrames ?? new int[0]).Distinct().ToArray();
                var choices = new List<NativeCompactStorageGroup[]>();
                foreach (var entry in held.Candidate.Children.OfType<CHR0EntryNode>())
                {
                    token.ThrowIfCancellationRequested();
                    if (!originals.TryGetValue(entry.Name, out var original)) continue;
                    var candidates = NativeCompactAnimationStorage.CreateFloatDenseAlternatives(entry, original,
                        exact.Contains(entry.Name), mandatory, MaximumKeys, proof, RotationBound(options),
                        RotationSpeedBound(options), options.FloatDenseEvidence?.ForBone(entry.Name) ?? FloatDenseExportPairEvidence(options), token);
                    if (proof.Exhausted) break;
                    if (candidates.Length != 0) choices.Add(candidates);
                }
                search.EligibleGroups = choices.Count;
                if (proof.Exhausted)
                {
                    search.BudgetExhausted = true;
                    search.Report = "L4 export-copy proposals exhausted the shared scalar budget; the verified incumbent remains.";
                    return search;
                }
                search.EligibilityComplete = true;
                choices = choices.OrderByDescending(c => c[0].BeforeBytes - c[0].AfterBytes)
                    .ThenBy(c => c[0].Bone, StringComparer.Ordinal).ToList();
                if (choices.Count == 0)
                {
                    search.Report = "L4 export-copy search found no smaller eligible nonloop rotation group under the frozen protections and quality limits.";
                    return search;
                }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                async Task<InvalidDataException> Attempt(NativeCompactStorageGroup[] plan, string label)
                {
                    token.ThrowIfCancellationRequested();
                    if (plan.Length == 0 || search.Attempts >= remainingAttempts) return null;
                    string identity = string.Join("|", plan.Select(p => p.Bone + ":" + string.Join(",", p.Curves.Select(c => c?.ContentKey ?? "fixed"))));
                    if (!seen.Add(identity)) return null;
                    long keys = CountKeys(held.Candidate);
                    foreach (var proposal in plan)
                    {
                        var entry = held.Candidate.Children.OfType<CHR0EntryNode>().Single(e => e.Name == proposal.Bone);
                        for (int axis = 0; axis < 3; axis++) if (proposal.Curves[axis] != null)
                            keys += proposal.Curves[axis].KeyCount - entry.KeyArrays[3 + axis].KeyCount;
                    }
                    search.Attempts++;
                    if (keys > MaximumKeys)
                    { notes.AppendLine(label + ": complete projected key limit exceeded; incumbent retained."); return null; }
                    InvalidDataException identified = null;
                    // Every composition starts with the held immutable owner;
                    // newly accepted results never become the sampling reference.
                    using (var candidate = Copy(held.Candidate))
                    {
                        foreach (var proposal in plan) NativeCompactAnimationStorage.ApplyGroup(candidate, proposal);
                        try
                        {
                            var verified = await TryVerifyCompact(candidate, baseline, bones, model, options, stage,
                                certify, precise, currentKey, currentStorage, currentBytes, currentBlock, notes, token,
                                false, proof, rejectedCandidate => identified = rejectedCandidate).ConfigureAwait(false);
                            if (verified != null && CompactSmaller(verified, search.Result ?? held))
                            {
                                search.Result?.Dispose(); search.Result = verified; search.Groups = plan.Length;
                                notes.AppendLine(label + ": retained " + verified.AfterBytes + " complete native bytes from " + plan.Length + " L4 groups.");
                            }
                            else verified?.Dispose();
                        }
                        catch (InvalidDataException error) when (error.Data.Contains("OptimizationBudget"))
                        { search.BudgetExhausted = true; notes.AppendLine(error.Message); }
                    }
                    return identified;
                }

                var working = choices.Select(c => c[0]).ToList();
                var failure = await Attempt(working.ToArray(), "Complete L4 export-copy plan").ConfigureAwait(false);
                // Only a typed scalar/bone dependency identifies a safe subset
                // to try. No quality budget is renewed and no limits are tuned.
                while (search.Result == null && !search.BudgetExhausted && failure != null && search.Attempts < remainingAttempts)
                {
                    var excluded = CompactFailedGroups(failure, bones, choices);
                    int removed = working.RemoveAll(p => excluded.Contains(p.Bone + "\0" + p.Group));
                    if (removed == 0 || working.Count == 0) break;
                    failure = await Attempt(working.ToArray(), "Typed L4 dependency repair").ConfigureAwait(false);
                }
                search.Report = "L4 export-copy search retained the verified " + held.AfterBytes + "-byte winner first; " +
                    choices.Count + " eligible groups; " + search.Attempts + "/" + remainingAttempts +
                    " remaining complete attempts used. Samples came only from the frozen finished target master. " +
                    "This bounded search does not establish global optimality.\r\n" + notes;
                return search;
            }
            catch { search.Dispose(); throw; }
        }
    }
}
