using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    internal static partial class RetargetKeyframes
    {
        sealed class SelectiveQualityResult : IDisposable
        {
            internal NativeAnimationOptimizationResult Result;
            internal QualityWire Wire;
            internal bool StorageApproximation;
            public void Dispose() => Result?.Dispose();
        }

        static readonly HashSet<string> SelectiveBoneMetrics = new HashSet<string>(new[] {
            "local scale", "Euler rotation", "local translation", "native scale derivative", "native Euler derivative",
            "native translation derivative", "local translation vector", "native translation vector derivative",
            "local orientation", "world bone position", "world forward basis", "world forward derivative", "local orientation derivative"
        }, StringComparer.Ordinal);

        // First retain the complete legacy precision/packing result. Extra
        // proposals only exchange complete, already Dense-certified bone curves
        // between its precise wire and the first coarse wire. Neither the last
        // accepted approximation nor a different character becomes a reference.
        static SelectiveQualityResult PrepareSelectiveQuality(MDL0Node model, CHR0Node dense,
            QualityWire coarse, QualityWire baseline, InvalidDataException failure,
            NativeAnimationOptimizationResult heldBaseline, HashSet<string> controls,
            NativeGeneratedPairSite[] generatedPairs, double length, QualityProofBudget proof,
            NativeAnimationOptimizationBudget nativeBudget, string folder, CancellationToken token,
            Action<int, string> progress, out string note)
        {
            note = "";
            if (coarse == null || baseline == null || ReferenceEquals(coarse, baseline) || failure == null || proof.Limited ||
                !coarse.Tracks.Select(t => t.Bone).SequenceEqual(baseline.Tracks.Select(t => t.Bone), StringComparer.Ordinal) ||
                !coarse.BindOmissions.SequenceEqual(baseline.BindOmissions, StringComparer.Ordinal)) return null;
            var bones = model.AllBones.ToDictionary(b => b.Name, StringComparer.Ordinal);
            var restored = new HashSet<string>(StringComparer.Ordinal);
            var diagnostics = new StringBuilder();
            string frozenDense = NativeAnimationOptimization.ContentKey(dense);
            int attempts = 0;
            SelectiveQualityResult selected = null;
            try
            {
                while (attempts < 4)
                {
                    token.ThrowIfCancellationRequested();
                    if (!RestoreSelectiveAncestors(failure, bones, restored, out string restoration)) break;
                    diagnostics.AppendLine(restoration);
                    var mixed = ComposeQualityWires(coarse, baseline, restored);
                    if (!mixed.Tracks.Where((track, index) => !ReferenceEquals(track, baseline.Tracks[index])).Any()) break;
                    if (proof.Limited || frozenDense != NativeAnimationOptimization.ContentKey(dense)) break;
                    attempts++;
                    progress?.Invoke(96, "Checking selective target-curve preparation against the finished motion...");
                    var options = new NativeAnimationOptimizationOptions { ExactOnly = false, LengthScale = length,
                        TargetBones = bones.Keys.ToArray(), ExactBones = controls.ToArray(),
                        AllowGeneratedPairReduction = true, GeneratedPairSites = generatedPairs,
                        BindPoseCleanupBones = mixed.BindOmissions, Budget = nativeBudget,
                        ProbeFrames = proof.Probes.ToArray(), CompactCandidateAttemptLimit = 6 };
                    NativeAnimationOptimizationResult precise = null, packed = null;
                    try
                    {
                        precise = NativeAnimationOptimization.Prepare(model, dense, mixed.Tracks, options, folder, token);
                        var decoded = precise.Candidate ?? dense;
                        if (!CertifyQualityComposition(dense, decoded, coarse, baseline, mixed, restored, proof, out string reason))
                        {
                            diagnostics.AppendLine("Selective composition retained the verified baseline: " + reason);
                            break;
                        }
                        // A complete precise candidate is useful on its own.
                        // Optional packing shares the remaining original proof
                        // and native budgets; its failure cannot discard it.
                        if (!proof.Limited && precise.AfterBytes >= heldBaseline.AfterBytes)
                        {
                            try
                            {
                                packed = NativeAnimationOptimization.PrepareCompact(model, dense, decoded, precise,
                                    options, folder, (candidate, cancellation) => System.Threading.Tasks.Task.FromResult(
                                        QualityStorageCertificate(dense, decoded, candidate, proof, length, controls,
                                            generatedPairs, mixed.BindOmissions)), token).GetAwaiter().GetResult();
                            }
                            catch (InvalidDataException error) when (error.Data.Contains("OptimizationQuality") ||
                                error.Data.Contains("OptimizationBudget") || error.Data.Contains("OptimizationSize"))
                            { diagnostics.AppendLine("Selective packing retained its verified precise copy: " + error.Message); }
                        }
                        var candidateResult = packed ?? precise;
                        diagnostics.AppendLine("Selective composition " + attempts + ": " + candidateResult.AfterBytes +
                            " complete bytes / " + candidateResult.AfterKeys + " native keys; " + restored.Count + " baseline branch/ancestor bindings.");
                        if (candidateResult.AfterBytes < heldBaseline.AfterBytes &&
                            candidateResult.CandidateResourceBytes <= heldBaseline.CandidateResourceBytes)
                        {
                            selected = new SelectiveQualityResult { Result = candidateResult, Wire = mixed,
                                StorageApproximation = candidateResult.ContentKey != precise.ContentKey };
                            if (ReferenceEquals(candidateResult, packed)) packed = null; else precise = null;
                        }
                        // A complete passing composition ends this bounded
                        // exploration; further attempts only repair typed native
                        // failures, never tune against an accepted candidate.
                        break;
                    }
                    catch (InvalidDataException error) when (error.Data.Contains("OptimizationQuality") ||
                        error.Data.Contains("OptimizationBudget") || error.Data.Contains("OptimizationSize"))
                    {
                        diagnostics.AppendLine("Selective composition " + attempts + " retained the verified baseline: " + error.Message);
                        if (!error.Data.Contains("OptimizationQuality")) break;
                        failure = error;
                    }
                    finally { packed?.Dispose(); precise?.Dispose(); }
                }
                token.ThrowIfCancellationRequested();
                note = "Selective target preparation: held baseline " + heldBaseline.AfterBytes + " complete bytes / " +
                    heldBaseline.CandidateResourceBytes + " resource bytes; " + attempts + "/4 complete inner compositions under the unchanged shared limits; " +
                    (selected == null ? "the verified original precision/packing baseline was retained. " :
                        "a strictly smaller complete candidate was selected against the same full Dense target. ") + diagnostics;
                return selected;
            }
            catch { selected?.Dispose(); throw; }
        }

        static bool RestoreSelectiveAncestors(InvalidDataException failure, Dictionary<string, MDL0BoneNode> bones,
            HashSet<string> restored, out string report)
        {
            report = "";
            if (failure == null || !failure.Data.Contains("OptimizationQuality") ||
                !(failure.Data["Metric"] is string metric) || !SelectiveBoneMetrics.Contains(metric) ||
                !(failure.Data["Bone"] is string name) || !bones.TryGetValue(name, out var failed)) return false;
            var chain = new List<MDL0BoneNode>();
            var visited = new HashSet<MDL0BoneNode>();
            for (var bone = failed; bone != null; bone = bone.Parent as MDL0BoneNode)
            {
                if (!visited.Add(bone)) return false;
                chain.Add(bone);
            }
            // The first failure retains the narrow ancestor repair. Later
            // failures can identify another descendant of an already repaired
            // parent. Restore that newly implicated native child branch as a
            // unit instead of spending the same bounded attempts on siblings.
            // The branch comes solely from the actual hierarchy, not names.
            int shared = restored.Count == 0 ? -1 : chain.FindIndex(bone => restored.Contains(bone.Name));
            var recovery = new List<MDL0BoneNode>(chain);
            if (shared > 0)
            {
                var branch = chain[shared - 1];
                var pending = new Stack<MDL0BoneNode>(); pending.Push(branch);
                var descendants = new HashSet<MDL0BoneNode>();
                while (pending.Count > 0)
                {
                    var bone = pending.Pop();
                    if (!descendants.Add(bone) || !bones.TryGetValue(bone.Name, out var owner) || !ReferenceEquals(owner, bone)) return false;
                    recovery.Add(bone);
                    foreach (var child in bone.Children.OfType<MDL0BoneNode>()) pending.Push(child);
                }
                report = "Restored the baseline wire throughout actual native branch " + branch.Name +
                    " below its previously restored ancestor " + chain[shared].Name + ".";
            }
            else report = "Restored the baseline wire on failed native bone " + failed.Name + " and its actual ancestors.";
            bool changed = false;
            foreach (var bone in recovery) changed |= restored.Add(bone.Name);
            return changed;
        }

        static QualityWire ComposeQualityWires(QualityWire coarse, QualityWire baseline, HashSet<string> restored)
        {
            var result = new QualityWire { Tracks = coarse.Tracks.Select((track, index) =>
                    restored.Contains(track.Bone) ? baseline.Tracks[index] : track).ToArray(),
                BindOmissions = baseline.BindOmissions.ToArray(),
                Value = coarse.Value.Zip(baseline.Value, Math.Max).ToArray(),
                Derivative = coarse.Derivative.Zip(baseline.Derivative, Math.Max).ToArray() };
            foreach (var track in result.Tracks)
            {
                var source = restored.Contains(track.Bone) ? baseline : coarse;
                var seeds = source.SeedFrames[track.Bone];
                result.SeedFrames.Add(track.Bone, seeds);
                result.SeedKeys = checked(result.SeedKeys + seeds.Sum(channel => channel.Length));
                if (source.RestoredTracks.Contains(track.Bone)) result.RestoredTracks.Add(track.Bone);
                for (int channel = 0; channel < 9; channel++)
                {
                    string name = track.Bone + " " + Channels[channel];
                    if (source.RestoredChannels.Contains(name)) result.RestoredChannels.Add(name);
                }
            }
            return result;
        }

        // Scalar proof composes by independent complete channels. Exact selected
        // wire membership and every freshly decoded coefficient must agree;
        // matching sample poses, names or a cheaper encoded size are insufficient.
        static bool CertifyQualityComposition(CHR0Node dense, CHR0Node decoded, QualityWire coarse, QualityWire baseline,
            QualityWire mixed, HashSet<string> restored, QualityProofBudget proof, out string reason)
        {
            reason = "The complete decoded composition does not match its immutable Dense-certified wire selection.";
            if (decoded.Name != dense.Name || decoded.Version != dense.Version || decoded.FrameCount != dense.FrameCount || decoded.Loop != dense.Loop ||
                !decoded.Children.Select(entry => entry.Name).SequenceEqual(mixed.Tracks.Select(track => track.Bone), StringComparer.Ordinal)) return false;
            var original = dense.Children.OfType<CHR0EntryNode>().ToDictionary(entry => entry.Name, StringComparer.Ordinal);
            for (int index = 0; index < mixed.Tracks.Length; index++)
            {
                proof.Token.ThrowIfCancellationRequested();
                var track = mixed.Tracks[index]; var entry = decoded.Children[index] as CHR0EntryNode;
                var selected = restored.Contains(track.Bone) ? baseline.Tracks[index] : coarse.Tracks[index];
                if (!ReferenceEquals(track, selected) || entry == null || !original.TryGetValue(entry.Name, out var before) ||
                    QualityStorageFlags(entry) != QualityStorageFlags(before)) return false;
                for (int channel = 0; channel < 9; channel++)
                {
                    var expected = track.Channels[channel]; var actual = entry.KeyArrays[channel].Entries.ToArray();
                    if (!proof.Charge(checked(actual.Length + expected.Length + 1)))
                    { reason = "The shared continuous proof budget was exhausted during complete coefficient composition."; return false; }
                    if (before.KeyArrays[channel].Loop != entry.KeyArrays[channel].Loop ||
                        before.KeyArrays[channel].FrameLimit != entry.KeyArrays[channel].FrameLimit || actual.Length != expected.Length ||
                        actual.Where((key, keyIndex) => !SameBits(key._index, expected[keyIndex].Frame) ||
                            !SameBits(key._value, expected[keyIndex].Value) || !SameBits(key._tangent, expected[keyIndex].Tangent)).Any()) return false;
                }
            }
            reason = "Complete decoded coefficient composition retains the original Dense scalar certificates, exact controls, unknown pairs, signed zeros and source seeds.";
            return true;
        }
    }
}
