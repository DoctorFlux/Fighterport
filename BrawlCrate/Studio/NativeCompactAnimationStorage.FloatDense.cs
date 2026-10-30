using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BrawlCrate.Studio
{
    internal static partial class NativeCompactAnimationStorage
    {
        // Export-copy proposals only. The immutable finished target supplies
        // samples; the held winner supplies identity and actual byte ceiling.
        // This never changes either owner and does not choose L1/L2 encodings.
        internal static NativeCompactStorageGroup[] CreateFloatDenseAlternatives(
            CHR0EntryNode entry, CHR0EntryNode original, bool exactGroup, int[] mandatoryFrames,
            int remainingKeys, NativeDenseProofBudget proof, double valueLimit, double derivativeLimit,
            NativeGeneratedPairSite[] reviewedSites, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            if (proof == null) throw new ArgumentNullException(nameof(proof));
            if (!Finite(valueLimit) || !Finite(derivativeLimit) || valueLimit <= 0 || derivativeLimit <= 0)
                throw new ArgumentException("L4 proposals need explicit finite positive saved bounds.");
            var clip = entry?.Parent as CHR0Node; var reference = original?.Parent as CHR0Node;
            if (exactGroup || clip == null || reference == null || clip.Loop || reference.Loop ||
                clip.Version != reference.Version || clip.Version != 4 && clip.Version != 5 ||
                clip.FrameCount != reference.FrameCount || clip.FrameCount < 2 || clip.FrameCount > ushort.MaxValue ||
                clip.NativeScalingRule != reference.NativeScalingRule || entry.Name != original.Name ||
                FloatDenseFlags(entry) != FloatDenseFlags(original) || entry.UseModelRotate ||
                entry.Keyframes.FrameLimit != reference.FrameCount || original.Keyframes.FrameLimit != reference.FrameCount ||
                entry.Keyframes.Loop || original.Keyframes.Loop)
                return new NativeCompactStorageGroup[0];
            var imported = PreciseCHR0EntryNode.CaptureStorage(original);
            if (imported != null && Enumerable.Range(3, 3).Any(c => imported[c]?.IsDense == true))
                return new NativeCompactStorageGroup[0];

            int frames = reference.FrameCount, physical = checked(frames + 1);
            var current = Enumerable.Range(3, 3).Select(c => entry.KeyArrays[c].Entries.ToArray()).ToArray();
            var baseline = Enumerable.Range(3, 3).Select(c => original.KeyArrays[c].Entries.ToArray()).ToArray();
            if (!proof.Spend(current.Sum(a => (long)a.Length) + baseline.Sum(a => (long)a.Length) + 1, token))
                return new NativeCompactStorageGroup[0];
            // One format covers the entire R group. A timed or sloped
            // singleton (including -zero time/tangent) requires indexed I12;
            // treating it as a fixed companion would make the writer abandon
            // the proposed L4 format for every varying axis in this group.
            if (current.Concat(baseline).Any(keys => keys.Length <= 1 && !FloatDenseCanonicalFixed(keys)))
                return new NativeCompactStorageGroup[0];
            int varying = current.Count(a => a.Length > 1);
            // Bound worst-case paired projection before allocating its arrays.
            if (varying == 0 || 2L * frames * varying > Math.Min(MaximumKeys, remainingKeys))
                return new NativeCompactStorageGroup[0];
            var old = PreciseCHR0EntryNode.CaptureStorage(entry);
            bool retained = old != null && old.MatchesGroup(entry.Keyframes, 1);
            int before = 0;
            for (int axis = 0; axis < 3; axis++) if (current[axis].Length > 1)
                before = checked(before + (retained && old[3 + axis] != null ? old[3 + axis].ByteCount : 8 + current[axis].Length * 12));
            int after = checked(4 * physical * varying);
            if (after >= before) return new NativeCompactStorageGroup[0];
            var mandatory = new HashSet<int>(mandatoryFrames ?? new int[0]);
            if (mandatory.Any(frame => frame < 0 || frame >= frames)) return new NativeCompactStorageGroup[0];
            var curves = new IndexedCHR0CurveStorage[3];
            for (int axis = 0; axis < 3; axis++)
            {
                token.ThrowIfCancellationRequested();
                if (current[axis].Length <= 1) continue;
                if (!FloatDenseInputValid(current[axis], frames) || !FloatDenseInputValid(baseline[axis], frames) ||
                    !FloatDenseReviewedPairs(entry.Name, axis + 3, current[axis], reviewedSites, proof, token))
                    return new NativeCompactStorageGroup[0];
                if (!proof.Spend(physical, token)) return new NativeCompactStorageGroup[0];
                var values = new float[physical];
                for (int frame = 0; frame < physical; frame++)
                {
                    token.ThrowIfCancellationRequested();
                    values[frame] = Sample(baseline[axis], original.KeyArrays[axis + 3], frame);
                    if (!Finite(values[frame])) return new NativeCompactStorageGroup[0];
                }
                IndexedCHR0CurveStorage curve;
                try { curve = IndexedCHR0CurveStorage.CreateFloatDense(values, frames); }
                catch (ArgumentException) { return new NativeCompactStorageGroup[0]; }
                if (!FloatDenseExportCurveWithin(original, axis + 3, curve, mandatory.ToArray(), reviewedSites,
                    valueLimit, derivativeLimit, proof, token)) return new NativeCompactStorageGroup[0];
                curves[axis] = curve;
            }
            if (curves.Where(c => c != null).Sum(c => (long)c.KeyCount) > remainingKeys)
                return new NativeCompactStorageGroup[0];
            return new[] { new NativeCompactStorageGroup { Bone = entry.Name, Group = 1, Format = AnimDataFormat.L4,
                Curves = curves, SourceKey = GroupKey(entry, 1), BeforeBytes = before, AfterBytes = after, ChangedCoefficients = 1 } };
        }

        // Admission is repeated on fresh decoded physical L4 payloads. Unknown
        // source sides never acquire permission merely because the new native
        // projection generated paired slopes. Mandatory joins stay exact.
        internal static bool FloatDenseExportCurveWithin(CHR0EntryNode original, int channel,
            IndexedCHR0CurveStorage curve, int[] mandatoryFrames, NativeGeneratedPairSite[] reviewedSites,
            double valueLimit, double derivativeLimit, NativeDenseProofBudget proof, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var clip = original?.Parent as CHR0Node;
            if (proof == null || clip == null || clip.Loop || clip.FrameCount < 2 || channel < 3 || channel > 5 ||
                curve == null || curve.Format != AnimDataFormat.L4 || curve.NativeSampleCount != clip.FrameCount ||
                curve.EncodedSampleCount != clip.FrameCount + 1 || curve.ByteCount != checked(4 * (clip.FrameCount + 1))) return false;
            var before = original.KeyArrays[channel].Entries.ToArray();
            if (!FloatDenseInputValid(before, clip.FrameCount) ||
                !FloatDenseReviewedPairs(original.Name, channel, before, reviewedSites, proof, token)) return false;
            var values = curve.EncodedValues; var times = curve.Times; var result = curve.Values; var tangents = curve.Tangents;
            if (!proof.Spend(values.Length + times.Length + before.Length + 1, token) ||
                Bits(values[values.Length - 1]) != Bits(values[values.Length - 2]) ||
                Bits(times[0]) != Bits(before[0]._index) || Bits(times[times.Length - 1]) != Bits(before[before.Length - 1]._index) ||
                Math.Abs((double)before[0]._tangent - tangents[0]) > derivativeLimit ||
                Math.Abs((double)before[before.Length - 1]._tangent - tangents[tangents.Length - 1]) > derivativeLimit) return false;
            foreach (int frame in mandatoryFrames ?? new int[0])
            {
                var required = before.Where(key => key._index == frame).ToArray();
                var actual = Enumerable.Range(0, times.Length).Where(i => times[i] == frame).ToArray();
                if (!proof.Spend(required.Length + actual.Length + 1, token) || required.Length == 0 || required.Length != actual.Length ||
                    required.Where((key, i) => Bits(key._index) != Bits(times[actual[i]]) || Bits(key._value) != Bits(result[actual[i]]) ||
                        Bits(key._tangent) != Bits(tangents[actual[i]])).Any()) return false;
            }
            string proofKey = proof.ProofKey(original, channel, curve, valueLimit, derivativeLimit, token);
            return proofKey != null && DenseLinearWithinBoundsPartitioned(before, original.KeyArrays[channel], values,
                valueLimit, derivativeLimit, token, proof, proofKey);
        }

        internal static bool FloatDenseExportGroupMatches(CHR0EntryNode original, CHR0EntryNode decoded)
        {
            var before = original?.Parent as CHR0Node; var after = decoded?.Parent as CHR0Node;
            return before != null && after != null && !before.Loop && !after.Loop &&
                before.Version == after.Version && before.FrameCount == after.FrameCount && before.NativeScalingRule == after.NativeScalingRule &&
                original.Name == decoded.Name && FloatDenseFlags(original) == FloatDenseFlags(decoded) &&
                original.Keyframes.FrameLimit == decoded.Keyframes.FrameLimit && original.Keyframes.Loop == decoded.Keyframes.Loop &&
                Enumerable.Range(0, 9).All(channel => original.KeyArrays[channel].FrameLimit == decoded.KeyArrays[channel].FrameLimit &&
                    original.KeyArrays[channel].Loop == decoded.KeyArrays[channel].Loop);
        }

        private static int FloatDenseFlags(CHR0EntryNode entry) => (entry.UseModelScale ? 1 : 0) | (entry.UseModelRotate ? 2 : 0) |
            (entry.UseModelTranslate ? 4 : 0) | (entry.ScaleCompensateApply ? 8 : 0) | (entry.ScaleCompensateParent ? 16 : 0) | (entry.ClassicScaleOff ? 32 : 0);

        private static bool FloatDenseCanonicalFixed(KeyframeEntry[] keys)
            => keys.Length == 0 || keys.Length == 1 && keys[0] != null && Finite(keys[0]._value) &&
                Bits(keys[0]._index) == 0 && Bits(keys[0]._tangent) == 0;

        private static bool FloatDenseReviewedPairs(string bone, int channel, KeyframeEntry[] keys,
            NativeGeneratedPairSite[] sites, NativeDenseProofBudget proof, CancellationToken token)
        {
            if ((sites?.Length ?? 0) > MaximumKeys || !proof.Spend((sites?.Length ?? 0) + keys.Length + 1L, token)) return false;
            var claims = new Dictionary<int, int>();
            foreach (var site in sites ?? new NativeGeneratedPairSite[0])
            {
                token.ThrowIfCancellationRequested();
                if (site == null || site.Bone != bone || site.Channel != channel || !Finite(site.Frame) || site.Frame < 0 || Bits(site.Frame) == int.MinValue) continue;
                int frame = Bits(site.Frame); claims[frame] = claims.TryGetValue(frame, out int count) ? Math.Min(2, count + 1) : 1;
            }
            foreach (var group in keys.GroupBy(key => Bits(key._index)))
            {
                var pair = group.Take(3).ToArray();
                if (pair.Length <= 1) continue;
                if (pair.Length != 2 || Bits(pair[0]._value) != Bits(pair[1]._value) || !claims.TryGetValue(group.Key, out int count) || count != 1) return false;
            }
            return true;
        }

        private static bool FloatDenseInputValid(KeyframeEntry[] keys, int frames)
        {
            if (keys == null || keys.Length < 2 || keys.Length > MaximumKeys || Bits(keys[0]._index) != 0 || keys[keys.Length - 1]._index != frames - 1) return false;
            float previous = -1; int sides = 0; float value = 0;
            foreach (var key in keys)
            {
                if (key == null || !Finite(key._index) || !Finite(key._value) || !Finite(key._tangent) ||
                    key._index < 0 || key._index >= frames || key._index < previous ||
                    Bits(key._index) == int.MinValue || Bits(key._value) == int.MinValue || Bits(key._tangent) == int.MinValue) return false;
                if (key._index == previous) { if (++sides > 2 || Bits(key._value) != Bits(value)) return false; }
                else { previous = key._index; sides = 1; value = key._value; }
            }
            return true;
        }

        // Pre-filter the independent physical LINEAR interpretation at every
        // original knot AND integer sample, including the physical final hold.
        // Final decoded Core rational proofs and collective native geometry
        // remain separate mandatory gates; this floating hull is not their proxy.
        internal static bool DenseLinearWithinBoundsPartitioned(KeyframeEntry[] original, KeyframeArray baseline,
            float[] values, double valueLimit, double derivativeLimit, CancellationToken token,
            NativeDenseProofBudget proof, string proofKey = null)
        {
            token.ThrowIfCancellationRequested();
            if (proof == null) throw new ArgumentNullException(nameof(proof));
            string cache = proofKey == null ? null : "partitioned-L4-export-v1:" + proofKey;
            if (proof.HasProof(cache)) return true;
            if (baseline == null || baseline.Loop || values == null || values.Length < 3 || values.Length > 65536 ||
                !Finite(valueLimit) || !Finite(derivativeLimit) || valueLimit <= 0 || derivativeLimit <= 0 ||
                !FloatDenseInputValid(original, values.Length - 1) || values.Any(value => !Finite(value)) ||
                Bits(values[values.Length - 1]) != Bits(values[values.Length - 2])) return false;
            if (!proof.Spend(original.Length + (long)values.Length, token)) return false;
            var breaks = new SortedSet<double>(Enumerable.Range(0, values.Length).Select(frame => (double)frame));
            foreach (var key in original) breaks.Add(key._index);
            double previous = 0; bool first = true;
            foreach (double next in breaks)
            {
                token.ThrowIfCancellationRequested();
                if (first) { previous = next; first = false; continue; }
                double start = previous, width = next - start; previous = next;
                if (!(width > 0) || !Finite(width)) return false;
                int frame = Math.Min(values.Length - 2, checked((int)Math.Floor(start)));
                double middle = start + width * .5;
                if (frame < 0 || !(middle > start && middle < next)) return false;
                Segment(original, baseline, middle, out KeyframeEntry left, out KeyframeEntry right, out double origin, out double span);
                double p0, p1, d0, d1;
                if (right == null) { p0 = p1 = left._value; d0 = d1 = 0; }
                else
                {
                    if (!(span > 0) || !Finite(span)) return false;
                    Polynomial(left, right, origin, span, start, out p0, out d0);
                    Polynomial(left, right, origin, span, next, out p1, out d1);
                }
                double slope = (double)values[frame + 1] - values[frame];
                double c0 = values[frame] + slope * (start - frame), c1 = values[frame] + slope * (next - frame);
                var error = new[] { c0 - p0, c0 - p0 + width * (slope - d0) / 3,
                    c1 - p1 - width * (slope - d1) / 3, c1 - p1 };
                double magnitude = 1 + Math.Abs(c0) + Math.Abs(c1) + Math.Abs(p0) + Math.Abs(p1) +
                    width * (Math.Abs(slope) + Math.Abs(d0) + Math.Abs(d1)) + Math.Abs(left._value);
                if (right != null) magnitude += Math.Abs(right._value) + span * (Math.Abs((double)left._tangent) + Math.Abs((double)right._tangent));
                if (!HullWithin(error, valueLimit, derivativeLimit * width, magnitude * 1e-11, 0, proof, token)) return false;
            }
            proof.RetainProof(cache); return true;
        }
    }
}
