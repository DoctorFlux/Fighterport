using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace BrawlCrate.Studio
{
    internal static partial class NativeAnimationOptimization
    {
        static readonly HashSet<string> ReductionBoneMetrics = new HashSet<string>(new[] {
            "local scale", "Euler rotation", "local translation", "native scale derivative", "native Euler derivative",
            "native translation derivative", "local translation vector", "native translation vector derivative",
            "local orientation", "world bone position", "world forward basis", "world forward derivative", "local orientation derivative"
        },StringComparer.Ordinal);
        // Internal composition contract: the optional exact tracks must be the
        // already Core-certified and NativePrepare-verified Exact attempt for
        // this same immutable baseline, target and protections, before storage.
        // Keys are immutable operation data; this method never edits their arrays
        // or creates replacement tangents. Every composition is fully rechecked.
        public static NativeAnimationOptimizationResult PrepareReduction(MDL0Node modelSnapshot, CHR0Node beforeSnapshot,
            ControlTrack[] proposedTracks, ControlTrack[] verifiedExactTracks, NativeAnimationOptimizationOptions options,
            string stageDirectory, CancellationToken token = default(CancellationToken))
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.ExactOnly) return Prepare(modelSnapshot, beforeSnapshot, proposedTracks, options, stageDirectory, token);
            token.ThrowIfCancellationRequested();
            if (modelSnapshot == null || beforeSnapshot == null) throw new ArgumentNullException("Reduction needs owned native baseline/target snapshots.");
            var working = (proposedTracks ?? new ControlTrack[0]).Select(ReductionTrackWrapper).ToList();
            var exact = verifiedExactTracks == null ? null : verifiedExactTracks.Select(ReductionTrackWrapper).ToDictionary(t => t.Bone, StringComparer.Ordinal);
            var bones = modelSnapshot.AllBones.ToDictionary(b => b.Name, StringComparer.Ordinal);
            var originals = beforeSnapshot.Children.OfType<CHR0EntryNode>().ToDictionary(e => e.Name, StringComparer.Ordinal);
            var restored = new HashSet<string>(StringComparer.Ordinal);
            string baseline = ContentKey(beforeSnapshot);
            var budget = options.Budget ?? new NativeAnimationOptimizationBudget();
            var frozen = ReductionOptions(options, budget);
            for (int attempt = 1; attempt <= 8; attempt++)
            {
                token.ThrowIfCancellationRequested();
                if (ContentKey(beforeSnapshot) != baseline) throw new InvalidOperationException("The frozen reduction baseline changed; the composed candidate was discarded.");
                try
                {
                    var result = Prepare(modelSnapshot, beforeSnapshot, working.ToArray(), frozen, stageDirectory, token);
                    if (restored.Count > 0)
                    {
                        result.ReductionSelectionReport = "Selective High quality reduction: " + attempt + " complete native attempts; " + restored.Count +
                            " bones restored with their actual ancestor dependencies: " + string.Join(", ", restored.OrderBy(n => n, StringComparer.Ordinal)) + "." +
                            "\r\nRetained proposed channels keep their original Core continuous scalar certificates. Restored complete channels use " +
                            (exact == null ? "the original baseline coefficient identity" : "the same-baseline verified Exact proof") + "." +
                            "\r\nCore proposal merge counts and scalar maxima remain conservative proposal data; the native key counts, bytes and cumulative errors above describe this final composed candidate. No bound was relaxed.";
                        result.Report += "\r\n" + result.ReductionSelectionReport;
                    }
                    return result;
                }
                catch (InvalidDataException error) when (error.Data.Contains("OptimizationQuality"))
                {
                    token.ThrowIfCancellationRequested();
                    if (attempt == 8 || !(error.Data["Metric"] is string metric) || !ReductionBoneMetrics.Contains(metric) ||
                        !(error.Data["Bone"] is string name) || !bones.TryGetValue(name, out var failed)) throw;
                    bool changed = false;
                    var seen = new HashSet<MDL0BoneNode>();
                    // A world-matrix error may be inherited from any ancestor.
                    // Restore the actual chain rather than guessing from names.
                    for (var bone = failed; bone != null; bone = bone.Parent as MDL0BoneNode)
                    {
                        if(!seen.Add(bone))throw new InvalidDataException("Selective reduction cannot restore a cyclic native ancestor hierarchy.");
                        ControlTrack replacement = null;
                        if (exact != null) exact.TryGetValue(bone.Name, out replacement);
                        else if (originals.TryGetValue(bone.Name, out var original)) replacement = ReductionOriginalTrack(original);
                        int index = working.FindIndex(t => t.Bone == bone.Name);
                        if (replacement == null)
                        {
                            if (index < 0) continue;
                            working.RemoveAt(index);
                        }
                        else
                        {
                            if (index >= 0 && ReductionSameTrack(working[index], replacement)) continue;
                            var copy = ReductionTrackWrapper(replacement);
                            if (index >= 0) working[index] = copy; else working.Add(copy);
                        }
                        restored.Add(bone.Name); changed = true;
                    }
                    if (!changed) throw;
                }
            }
            throw new InvalidOperationException("The bounded reduction verification did not complete.");
        }

        static ControlTrack ReductionTrackWrapper(ControlTrack track)
        {
            if (track == null || track.Bone == null || track.Channels == null || track.Channels.Length != 9 || track.Channels.Any(c => c == null))
                throw new InvalidDataException("Selective reduction requires complete immutable native track channels.");
            return new ControlTrack { Bone = track.Bone, Channels = track.Channels.ToArray() };
        }

        static ControlTrack ReductionOriginalTrack(CHR0EntryNode entry) => new ControlTrack { Bone = entry.Name,
            Channels = entry.KeyArrays.Select(array => array.Entries.Select(key => new ControlKey {
                Frame = key._index, Value = key._value, Tangent = key._tangent }).ToArray()).ToArray() };

        static bool ReductionSameTrack(ControlTrack a, ControlTrack b) => a.Bone == b.Bone && Enumerable.Range(0, 9).All(c =>
            a.Channels[c].Length == b.Channels[c].Length && a.Channels[c].Select((key, i) => key != null && b.Channels[c][i] != null &&
                key.Frame == b.Channels[c][i].Frame && Bits(key.Value) == Bits(b.Channels[c][i].Value) && Bits(key.Tangent) == Bits(b.Channels[c][i].Tangent)).All(same => same));

        static NativeAnimationOptimizationOptions ReductionOptions(NativeAnimationOptimizationOptions source, NativeAnimationOptimizationBudget budget) =>
            new NativeAnimationOptimizationOptions { ExactOnly = source.ExactOnly, LengthScale = source.LengthScale,
                OriginalStoredBytes = source.OriginalStoredBytes, OriginalStoredResourceBytes = source.OriginalStoredResourceBytes,
                TargetBones = source.TargetBones?.ToArray(), ProtectedBones = source.ProtectedBones?.ToArray(), ExactBones = source.ExactBones?.ToArray(),
                MandatoryFrames = source.MandatoryFrames?.ToArray(), ProbeFrames = source.ProbeFrames?.ToArray(),
                BindPoseCleanupBones = source.BindPoseCleanupBones?.ToArray(), DeferSizeValidation = source.DeferSizeValidation, Budget = budget };
    }
}
