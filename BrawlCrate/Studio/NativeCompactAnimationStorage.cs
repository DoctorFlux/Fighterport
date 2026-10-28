using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace BrawlCrate.Studio
{
    // One operation-owned account is shared by every baked proposal and final
    // verification. The UI callback reserves from the existing Core proof work
    // counter; non-UI callers retain the same two-million-unit hard ceiling.
    internal sealed class NativeDenseProofBudget
    {
        private const long MaximumWork = 2000000;
        private readonly Func<long, bool> reserveSharedWork;
        private readonly Dictionary<CHR0EntryNode, string> baselines = new Dictionary<CHR0EntryNode, string>();
        private readonly HashSet<string> proofs = new HashSet<string>(StringComparer.Ordinal);
        public long Work { get; private set; }
        public bool Exhausted { get; private set; }
        public NativeDenseProofBudget(Func<long, bool> reserveSharedWork = null) { this.reserveSharedWork = reserveSharedWork; }
        internal bool Spend(long units, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (units < 0 || units > MaximumWork - Work || reserveSharedWork != null && !reserveSharedWork(units))
            { Exhausted = true; return false; }
            Work += units; return true;
        }
        internal string ProofKey(CHR0EntryNode baseline, int channel, IndexedCHR0CurveStorage curve, double valueLimit, double derivativeLimit, CancellationToken token)
        {
            if (!baselines.TryGetValue(baseline, out string original))
            {
                if (baselines.Count >= 4096 || !Spend(baseline.KeyArrays.Skip(3).Take(3).Sum(a => (long)a.KeyCount) + 1, token)) return null;
                original = NativeCompactAnimationStorage.GroupKey(baseline, 1); baselines.Add(baseline, original);
            }
            return original + ":" + channel + ":" + curve.ContentKey + ":" +
                valueLimit.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + derivativeLimit.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }
        internal bool HasProof(string key) => key != null && proofs.Contains(key);
        internal void RetainProof(string key) { if (key != null && proofs.Count < 100000) proofs.Add(key); }
    }

    // A proposal is not an accepted optimization. Its decoded coefficients must
    // pass the Core continuous certificate and the complete native pose/skin gate
    // against the operation's retained baseline before it can reach a document.
    internal sealed class NativeCompactStorageGroup
    {
        public string Bone { get; internal set; }
        public int Group { get; internal set; }
        public AnimDataFormat Format { get; internal set; }
        public IndexedCHR0CurveStorage[] Curves { get; internal set; }
        public string SourceKey { get; internal set; }
        public int BeforeBytes { get; internal set; }
        public int AfterBytes { get; internal set; }
        public int ChangedCoefficients { get; internal set; }
        public bool IsExact => ChangedCoefficients == 0;
        public bool IsDense => Format == AnimDataFormat.L1 || Format == AnimDataFormat.L2;
    }

    internal static class NativeCompactAnimationStorage
    {
        private const int MaximumKeys = 2000000;

        // The native format is shared by the three varying axes of an SRT group.
        // Fixed/absent axes remain fixed/absent. This helper never removes tracks.
        public static NativeCompactStorageGroup[] CreateAlternatives(CHR0EntryNode entry, int group,
            bool exactOnly, bool exactGroup, int[] mandatoryFrames, double lengthScale,
            CancellationToken token = default(CancellationToken))
            => CreateAlternatives(entry, group, exactOnly, exactGroup, mandatoryFrames, lengthScale, false, new int[0], token);

        // Additive profile overload: existing callers retain their exact/HQ
        // bounds. Fit joins are distinct from explicit exact mandatory knots.
        public static NativeCompactStorageGroup[] CreateAlternatives(CHR0EntryNode entry, int group,
            bool exactOnly, bool exactGroup, int[] mandatoryFrames, double lengthScale, bool smallFile, int[] fitJoinFrames,
            CancellationToken token = default(CancellationToken))
        {
            if (entry == null || group < 0 || group > 2) throw new ArgumentException("A native storage proposal requires one exact SRT group.");
            token.ThrowIfCancellationRequested();
            var arrays = Enumerable.Range(group * 3, 3).Select(c => entry.KeyArrays[c].Entries.ToArray()).ToArray();
            if (arrays.Sum(a => (long)a.Length) > MaximumKeys) throw new InvalidDataException("Native storage proposal exceeds the key budget.");
            // Indexed/dense times cannot retain an authored IEEE negative zero.
            // Keep this exact native time metadata in the precise I12 group.
            if (arrays.SelectMany(a => a).Any(k => Bits(k._index) == int.MinValue)) return new NativeCompactStorageGroup[0];
            if (!arrays.Any(a => a.Length > 1)) return new NativeCompactStorageGroup[0];
            if (!exactOnly && (!Finite(lengthScale) || lengthScale <= 0))
                throw new InvalidDataException("High-quality storage requires the verified finite positive target bind-geometry scale.");
            var mandatory = new HashSet<int>(mandatoryFrames ?? new int[0]);
            bool small = smallFile && !exactOnly;
            if (!small) mandatory.UnionWith(fitJoinFrames ?? new int[0]);
            bool exactScale = group == 0 && arrays.Any(RequiresExactScale);
            bool exact = exactOnly || exactGroup || exactScale;
            double valueLimit = small ? group == 0 ? .0001 : group == 1 ? .1 : .002 :
                group == 0 ? .00001 : group == 1 ? .01 : Math.Min(.0002, .0001 * lengthScale);
            double derivativeLimit = group == 1 ? small ? .1 : .05 : valueLimit;
            var result = new List<NativeCompactStorageGroup>();
            foreach (AnimDataFormat format in new[] { AnimDataFormat.I4, AnimDataFormat.I6 })
            {
                token.ThrowIfCancellationRequested();
                var curves = new IndexedCHR0CurveStorage[3]; bool accepted = true; int before = 0, after = 0, changes = 0;
                for (int axis = 0; axis < 3; axis++)
                {
                    var keys = arrays[axis]; if (keys.Length <= 1) continue;
                    var curve = TryCurve(keys, format, exact, mandatory, valueLimit, derivativeLimit, small, small && group != 0, token);
                    if (curve == null) { accepted = false; break; }
                    curves[axis] = curve; before += 8 + keys.Length * 12; after += curve.ByteCount;
                    var values = curve.Values; var tangents = curve.Tangents;
                    for (int i = 0; i < keys.Length; i++)
                    { if (Bits(values[i]) != Bits(keys[i]._value)) changes++; if (Bits(tangents[i]) != Bits(keys[i]._tangent)) changes++; }
                }
                // Native isotropic scale may share storage, so the byte counts
                // here are proposal estimates. The independent whole-file encode
                // supplies the only reported final size and rejects any growth.
                if (accepted && after < before)
                    result.Add(new NativeCompactStorageGroup { Bone = entry.Name, Group = group, Format = format,
                        Curves = curves, SourceKey = GroupKey(entry, group), BeforeBytes = before, AfterBytes = after, ChangedCoefficients = changes });
            }
            return result.OrderBy(p => p.AfterBytes).ThenBy(p => p.ChangedCoefficients).ToArray();
        }

        public static void ApplyGroup(CHR0Node independentCandidate, NativeCompactStorageGroup proposal)
        {
            if (independentCandidate == null || proposal == null) throw new ArgumentNullException("Compact storage applies only to an independent identified candidate.");
            var matches = independentCandidate.Children.OfType<CHR0EntryNode>().Where(e => e.Name == proposal.Bone).ToArray();
            if (matches.Length != 1 || GroupKey(matches[0], proposal.Group) != proposal.SourceKey)
                throw new InvalidOperationException("The native storage proposal is stale or its exact track identity is ambiguous.");
            var entry = matches[0]; var previous = PreciseCHR0EntryNode.CaptureStorage(entry);
            var channels = Enumerable.Range(0, 9).Select(c => previous == null ? null : previous[c]).ToArray();
            for (int axis = 0; axis < 3; axis++)
            {
                int channel = proposal.Group * 3 + axis; var curve = proposal.Curves[axis]; channels[channel] = curve;
                if (curve == null) continue;
                var keys = entry.KeyArrays[channel].Entries.ToArray(); var frames = curve.Times; var values = curve.Values; var tangents = curve.Tangents;
                if (curve.IsDense)
                {
                    // This is an operation-owned candidate, never the live
                    // authoring collection. Its generated native tangents are
                    // an explicit proposal and must pass both interpretations.
                    var replacement = new KeyframeArray(entry.Keyframes.FrameLimit) { Loop = entry.Keyframes.Loop };
                    for (int i = 0; i < frames.Length; i++) replacement.SetFrameValue(frames[i], values[i], true)._tangent = tangents[i];
                    entry.Keyframes._keyArrays[channel] = replacement;
                    continue;
                }
                if (keys.Length != frames.Length) throw new InvalidOperationException("The native storage key membership changed.");
                for (int i = 0; i < keys.Length; i++)
                {
                    if (Bits(keys[i]._index) != Bits(frames[i])) throw new InvalidOperationException("Native indexed storage cannot move authored knot times.");
                    keys[i]._value = values[i]; keys[i]._tangent = tangents[i];
                }
            }
            entry.Keyframes.PreserveInterpolation = true;
            PreciseCHR0EntryNode.RestoreStorage(entry, new IndexedCHR0EntryStorage(channels));
        }

        // Only the explicit Small file profile proposes baked rotation storage.
        // The complete group remains indexed when a paired side, exact control,
        // signed zero, mandatory boundary or either continuous interpretation
        // cannot be retained. Values are sampled from the retained baseline,
        // rather than accumulating error from a previously optimized candidate.
        public static NativeCompactStorageGroup[] CreateDenseAlternatives(CHR0EntryNode entry, CHR0EntryNode retainedBaseline,
            bool exactGroup, int[] mandatoryFrames, int remainingKeys, CancellationToken token = default(CancellationToken), NativeDenseProofBudget proofBudget = null)
        {
            proofBudget = proofBudget ?? new NativeDenseProofBudget();
            token.ThrowIfCancellationRequested(); var clip = entry?.Parent as CHR0Node;
            if (clip == null || retainedBaseline == null || exactGroup || clip.FrameCount < 1 || clip.FrameCount > ushort.MaxValue ||
                entry.Name != retainedBaseline.Name) return new NativeCompactStorageGroup[0];
            var imported = PreciseCHR0EntryNode.CaptureStorage(retainedBaseline);
            if (imported != null && Enumerable.Range(3, 3).Any(c => imported[c] != null && imported[c].IsDense))
                return new NativeCompactStorageGroup[0];
            int nativeCount = clip.FrameCount + (clip.Loop ? 1 : 0), encodedCount = clip.FrameCount + 1;
            if (!proofBudget.Spend(entry.KeyArrays.Skip(3).Take(3).Sum(a => (long)a.KeyCount) +
                retainedBaseline.KeyArrays.Skip(3).Take(3).Sum(a => (long)a.KeyCount) + 1, token)) return new NativeCompactStorageGroup[0];
            var arrays = Enumerable.Range(3, 3).Select(c => entry.KeyArrays[c].Entries.ToArray()).ToArray();
            var originals = Enumerable.Range(3, 3).Select(c => retainedBaseline.KeyArrays[c].Entries.ToArray()).ToArray();
            int varying = arrays.Count(a => a.Length > 1);
            if (varying == 0 || nativeCount < 2 || (long)encodedCount * varying > Math.Min(MaximumKeys, remainingKeys) ||
                originals.Any(a => a.GroupBy(k => k._index).Any(g => g.Count() > 2 || g.Select(k => Bits(k._value)).Distinct().Count() != 1)) ||
                originals.SelectMany(a => a).Any(k => !Finite(k._value) || !Finite(k._tangent))) return new NativeCompactStorageGroup[0];
            var mandatory = new HashSet<int>(mandatoryFrames ?? new int[0]); var results = new List<NativeCompactStorageGroup>();
            foreach (var format in new[] { AnimDataFormat.L1, AnimDataFormat.L2 })
            {
                var curves = new IndexedCHR0CurveStorage[3]; bool valid = true; int before = 0, after = 0;
                for (int axis = 0; axis < 3; axis++)
                {
                    token.ThrowIfCancellationRequested(); if (arrays[axis].Length <= 1) continue;
                    if (!proofBudget.Spend(encodedCount, token)) return results.ToArray();
                    float[] samples = new float[encodedCount];
                    for (int i = 0; i < encodedCount; i++) samples[i] = Sample(originals[axis], retainedBaseline.KeyArrays[3 + axis], i);
                    curves[axis] = TryDenseCurve(originals[axis], retainedBaseline, 3 + axis, samples, nativeCount, format, mandatory, proofBudget, token);
                    if (curves[axis] == null) { valid = false; break; }
                    before += 8 + arrays[axis].Length * 12; after += curves[axis].ByteCount;
                }
                if (valid && after < before)
                {
                    if (!proofBudget.Spend(arrays.Sum(a => (long)a.Length) + 1, token)) return results.ToArray();
                    results.Add(new NativeCompactStorageGroup { Bone = entry.Name, Group = 1, Format = format, Curves = curves,
                        SourceKey = GroupKey(entry, 1), BeforeBytes = before, AfterBytes = after, ChangedCoefficients = 1 });
                }
            }
            return results.OrderBy(p => p.AfterBytes).ToArray();
        }

        private static IndexedCHR0CurveStorage TryDenseCurve(KeyframeEntry[] original, CHR0EntryNode baselineEntry, int channel, float[] samples,
            int nativeCount, AnimDataFormat format, HashSet<int> mandatory, NativeDenseProofBudget proofBudget, CancellationToken token)
        {
            var baseline = baselineEntry.KeyArrays[channel];
            if (original.Length < 2 || original.Any(k => k._index != Math.Floor(k._index))) return null;
            if (original.Any(k => Bits(k._value) == int.MinValue || Bits(k._tangent) == int.MinValue)) return null;
            int maximum = format == AnimDataFormat.L1 ? 255 : 65535;
            float minimum = samples.Min(), maximumValue = samples.Max(); double range = (double)maximumValue - minimum;
            if (!Finite(range) || range < 0 || samples.Any(v => !Finite(v))) return null;
            var steps = new List<float>();
            if (range == 0) steps.Add(0);
            else
            {
                float fine = (float)(range / maximum); if (fine > 0 && Finite(fine)) steps.Add(fine);
                double power = Math.Pow(2, Math.Ceiling(Math.Log(range / maximum, 2)));
                if (power > 0 && Finite(power) && power <= float.MaxValue) steps.Add((float)power);
            }
            foreach (float step in steps.Distinct())
            {
                var bases = new List<float> { minimum };
                if (step > 0)
                {
                    double aligned = Math.Floor((double)minimum / step) * step;
                    if (Finite(aligned) && aligned >= -float.MaxValue && aligned <= float.MaxValue) bases.Add((float)aligned);
                }
                foreach (float valueBase in bases.Distinct())
                {
                    token.ThrowIfCancellationRequested(); if (!proofBudget.Spend(samples.Length + original.Length, token)) return null;
                    var quantized = new int[samples.Length]; bool valid = true;
                    for (int i = 0; i < samples.Length; i++)
                    {
                        double q = step == 0 ? 0 : Math.Round(((double)samples[i] - valueBase) / step, MidpointRounding.ToEven);
                        if (q < 0 || q > maximum) { valid = false; break; } quantized[i] = (int)q;
                    }
                    if (!valid) continue;
                    IndexedCHR0CurveStorage curve;
                    try { curve = IndexedCHR0CurveStorage.CreateDense(format, quantized, nativeCount, valueBase, step); }
                    catch (ArgumentException) { continue; }
                    var values = curve.EncodedValues;
                    var projectedTangents = curve.Tangents;
                    var sides = curve.Frames.Select((frame, i) => new { frame, tangent = projectedTangents[i] }).GroupBy(k => k.frame).ToDictionary(g => g.Key, g => g.Select(k => k.tangent).ToArray());
                    foreach (var key in original)
                    {
                        if (key._index >= nativeCount || !Allowed(key._value, values[checked((int)key._index)], mandatory.Contains(checked((int)key._index)), .1, true) ||
                            mandatory.Contains(checked((int)key._index)) && (!sides.TryGetValue(checked((int)key._index), out var tangents) ||
                            !tangents.Any(t => Bits(t) == Bits(key._tangent)))) { valid = false; break; }
                    }
                    if (valid)
                    {
                        string proofKey = proofBudget.ProofKey(baselineEntry, channel, curve, .1, .1, token);
                        if (proofKey != null && DenseLinearWithinBounds(original, baseline, curve.EncodedValues, .1, .1, token, proofBudget, proofKey)) return curve;
                    }
                }
            }
            return null;
        }

        // A second continuous interpretation is mandatory: independent readers
        // evaluate baked rotations linearly. Cubic Bernstein hulls compare that
        // line against the original native Hermite polynomial throughout every
        // integer interval (including the stored non-loop terminal sample).
        internal static bool DenseLinearWithinBounds(KeyframeEntry[] original, KeyframeArray baseline, float[] values,
            double valueLimit, double derivativeLimit, CancellationToken token, NativeDenseProofBudget proofBudget = null, string proofKey = null)
        {
            token.ThrowIfCancellationRequested();
            proofBudget = proofBudget ?? new NativeDenseProofBudget();
            if (proofBudget.HasProof(proofKey)) return true;
            if (original == null || original.Length == 0 || baseline == null || values == null || values.Length < 2 ||
                original.GroupBy(k => k._index).Any(g => g.Count() > 2 || g.Select(k => Bits(k._value)).Distinct().Count() != 1)) return false;
            if (!proofBudget.Spend((long)original.Length + values.Length, token)) return false;
            for (int i = 0; i < values.Length - 1; i++)
            {
                token.ThrowIfCancellationRequested(); double delta = (double)values[i + 1] - values[i];
                // CV evaluation is an unwrapped scalar line. Preserve full
                // turns instead of introducing shortest-path angular wrapping.
                if (!Finite(delta)) return false;
                Segment(original, baseline, i + .5, out KeyframeEntry left, out KeyframeEntry right, out double start, out double span);
                double p0, p1, d0, d1;
                if (right == null) { p0 = p1 = left._value; d0 = d1 = 0; }
                else { Polynomial(left, right, start, span, i, out p0, out d0); Polynomial(left, right, start, span, i + 1d, out p1, out d1); }
                double[] error = { values[i] - p0, values[i] + delta / 3 - (p0 + d0 / 3),
                    values[i] + delta * 2 / 3 - (p1 - d1 / 3), values[i + 1] - p1 };
                double magnitude = Math.Max(1, Math.Max(Math.Abs(p0), Math.Abs(p1)) + Math.Abs(d0) + Math.Abs(d1) + Math.Abs(delta));
                // Endpoint evaluations can cancel much larger coefficients.
                // Include the original normalized cubic's coefficient norm,
                // so cancellation never makes the roundoff guard smaller.
                magnitude += Math.Abs(values[i]) + Math.Abs(values[i + 1]) + Math.Abs(left._value);
                if (right != null) magnitude += Math.Abs(right._value) + span * (Math.Abs((double)left._tangent) + Math.Abs((double)right._tangent));
                // Uncertain floating-point hulls keep the indexed fallback.
                double roundoff = magnitude * 1e-11;
                if (!HullWithin(error, valueLimit, derivativeLimit, roundoff, 0, proofBudget, token)) return false;
            }
            proofBudget.RetainProof(proofKey);
            return true;
        }

        private static bool HullWithin(double[] b, double valueLimit, double derivativeLimit, double roundoff, int depth, NativeDenseProofBudget proofBudget, CancellationToken token)
        {
            if (!proofBudget.Spend(1, token)) return false;
            if (b.Any(v => !Finite(v))) return false;
            double value = b.Max(v => Math.Abs(v)), derivative = 0;
            for (int i = 0; i < 3; i++) derivative = Math.Max(derivative, Math.Abs(3 * (b[i + 1] - b[i])));
            if (value + roundoff <= valueLimit && derivative + roundoff * 6 <= derivativeLimit) return true;
            if (depth >= 8 || valueLimit <= roundoff || derivativeLimit <= roundoff * 6) return false;
            double b01 = (b[0] + b[1]) * .5, b12 = (b[1] + b[2]) * .5, b23 = (b[2] + b[3]) * .5;
            double a = (b01 + b12) * .5, c = (b12 + b23) * .5, m = (a + c) * .5;
            return HullWithin(new[] { b[0], b01, a, m }, valueLimit, derivativeLimit * .5, roundoff * 2, depth + 1, proofBudget, token) &&
                HullWithin(new[] { m, c, b23, b[3] }, valueLimit, derivativeLimit * .5, roundoff * 2, depth + 1, proofBudget, token);
        }

        private static void Polynomial(KeyframeEntry left, KeyframeEntry right, double start, double span, double time, out double value, out double derivative)
        {
            double t = (time - start) / span, p = left._value, q = right._value, a = left._tangent * span, b = right._tangent * span;
            value = (2 * t * t * t - 3 * t * t + 1) * p + (t * t * t - 2 * t * t + t) * a +
                (-2 * t * t * t + 3 * t * t) * q + (t * t * t - t * t) * b;
            derivative = ((6 * t * t - 6 * t) * p + (3 * t * t - 4 * t + 1) * a +
                (-6 * t * t + 6 * t) * q + (3 * t * t - 2 * t) * b) / span;
        }

        private static float Sample(KeyframeEntry[] keys, KeyframeArray array, int time)
        {
            if (keys.Length == 0) return array.GetFrameValue(time);
            int low = 0, high = keys.Length;
            while (low < high) { int middle = (low + high) / 2; if (keys[middle]._index < time) low = middle + 1; else high = middle; }
            if (low < keys.Length && keys[low]._index == time) return keys[low]._value;
            Segment(keys, array, time, out KeyframeEntry left, out KeyframeEntry right, out double start, out double span);
            return right == null ? left._value : left.Interpolate((float)(time - start), (float)span, right);
        }

        private static void Segment(KeyframeEntry[] keys, KeyframeArray array, double time, out KeyframeEntry left,
            out KeyframeEntry right, out double start, out double span)
        {
            left = keys[0]; right = null; start = 0; span = 1;
            if (keys.Length == 1) return;
            if (time < keys[0]._index || time > keys[keys.Length - 1]._index)
            {
                if (!array.Loop) { left = time < keys[0]._index ? keys[0] : keys[keys.Length - 1]; return; }
                left = keys[keys.Length - 1]; right = keys[0]; span = array.FrameLimit - left._index + right._index;
                start = time > left._index && time < array.FrameLimit ? left._index : left._index - array.FrameLimit;
                return;
            }
            int low = 0, high = keys.Length;
            while (low < high) { int middle = (low + high) / 2; if (keys[middle]._index <= time) low = middle + 1; else high = middle; }
            int index = Math.Max(0, low - 1); left = keys[index];
            if (index == keys.Length - 1) return;
            right = keys[index + 1]; start = left._index; span = right._index - left._index;
        }

        internal static CHR0Node CreateLinearProjection(CHR0Node independentlyDecoded, HashSet<string> changedChannels)
        {
            // Caller supplies and disposes a separate snapshot; this projection
            // is never serialized, applied or retained by document ownership.
            foreach (var entry in independentlyDecoded.Children.OfType<CHR0EntryNode>().ToArray())
            {
                var storage = PreciseCHR0EntryNode.CaptureStorage(entry); if (storage == null) continue;
                for (int channel = 3; channel < 6; channel++)
                {
                    var curve = storage[channel]; if (curve == null || !curve.IsDense) continue;
                    if (changedChannels != null && !changedChannels.Contains(entry.Name + "\0" + channel)) continue;
                    var values = curve.EncodedValues;
                    var array = new KeyframeArray(values.Length) { Loop = false };
                    for (int i = 0; i < values.Length; i++)
                    {
                        float incoming = i == 0 ? values[1] - values[0] : values[i] - values[i - 1];
                        float outgoing = i == values.Length - 1 ? incoming : values[i + 1] - values[i];
                        array.SetFrameValue(i, values[i], true)._tangent = incoming;
                        if (i > 0 && i < values.Length - 1) array.SetFrameValue(i, values[i], true)._tangent = outgoing;
                    }
                    entry.Keyframes._keyArrays[channel] = array;
                }
            }
            return independentlyDecoded;
        }

        private static IndexedCHR0CurveStorage TryCurve(KeyframeEntry[] keys, AnimDataFormat format, bool exact,
            HashSet<int> mandatory, double valueLimit, double derivativeLimit, bool zeroAlignedGrid, bool allowPositiveZero, CancellationToken token)
        {
            double frameLimit = format == AnimDataFormat.I4 ? 255 : 1023.96875; int valueMaximum = format == AnimDataFormat.I4 ? 4095 : 65535;
            int tangentMinimum = format == AnimDataFormat.I4 ? -2048 : -32768, tangentMaximum = format == AnimDataFormat.I4 ? 2047 : 32767;
            double tangentScale = format == AnimDataFormat.I4 ? 32 : 256;
            if (keys.Length > ushort.MaxValue || keys.Any(k => k._index < 0 || k._index > frameLimit || !Finite(k._index) || k._index * (format == AnimDataFormat.I4 ? 1 : 32) != Math.Floor(k._index * (format == AnimDataFormat.I4 ? 1 : 32)) || !Finite(k._value) || !Finite(k._tangent))) return null;
            var discontinuities = new HashSet<float>(keys.GroupBy(k => k._index).Where(g => g.Select(k => Bits(k._value)).Distinct().Count() > 1).Select(g => g.Key));
            var pairedFrames = new HashSet<float>(keys.GroupBy(k => k._index).Where(g => g.Count() > 1).Select(g => g.Key));
            if (keys.GroupBy(k => k._index).Any(g => g.Count() > 2)) return null;
            float minimum = keys.Min(k => k._value), maximum = keys.Max(k => k._value);
            double range = (double)maximum - minimum;
            var steps = new List<float>();
            if (range == 0) steps.Add(0);
            else
            {
                float fine = (float)(range / valueMaximum); if (Finite(fine) && fine > 0) steps.Add(fine);
                // A power-of-two grid allows mathematically exact binary32 data
                // to retain its coefficients when it fits the native integer span.
                double power = Math.Pow(2, Math.Ceiling(Math.Log(range / valueMaximum, 2)));
                if (Finite(power) && power > 0 && power <= float.MaxValue) steps.Add((float)power);
            }
            IndexedCHR0CurveStorage best = null; double bestError = double.PositiveInfinity;
            foreach (float step in steps.Distinct())
            {
                token.ThrowIfCancellationRequested();
                var bases = new List<float> { minimum };
                if (zeroAlignedGrid && step > 0)
                {
                    // A minimum-based grid need not represent an authored +zero.
                    // Keep it as the first choice, then try one bounded grid
                    // whose origin is an integer multiple of this same step.
                    // Binary power-of-two steps can represent zero exactly.
                    double aligned = Math.Floor((double)minimum / step) * step;
                    if (Finite(aligned) && aligned >= -float.MaxValue && aligned <= float.MaxValue) bases.Add((float)aligned);
                }
                foreach (float valueBase in bases.Distinct())
                {
                    token.ThrowIfCancellationRequested();
                    var values = new int[keys.Length]; var tangents = new int[keys.Length]; bool valid = true;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        double q = step == 0 ? 0 : Math.Round(((double)keys[i]._value - valueBase) / step, MidpointRounding.ToEven);
                        double tangent = Math.Round((double)keys[i]._tangent * tangentScale, MidpointRounding.ToEven);
                        if (q < 0 || q > valueMaximum || tangent < tangentMinimum || tangent > tangentMaximum) { valid = false; break; }
                        values[i] = (int)q; tangents[i] = (int)tangent;
                    }
                    if (!valid) continue;
                    IndexedCHR0CurveStorage curve;
                    try { curve = IndexedCHR0CurveStorage.Create(format, keys.Select(k => k._index).ToArray(), values, tangents, valueBase, step); }
                    catch (ArgumentException) { continue; }
                    var decodedValues = curve.Values; var decodedTangents = curve.Tangents; double error = 0;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        // Both sides and their order remain. Continuous body
                        // slopes may use bounded compact coefficients; jumps,
                        // protected controls and mandatory knots stay exact.
                        bool strict = exact || mandatory.Any(frame => frame == keys[i]._index) || discontinuities.Contains(keys[i]._index)
                            || !allowPositiveZero && pairedFrames.Contains(keys[i]._index);
                        if (!Allowed(keys[i]._value, decodedValues[i], strict, valueLimit, allowPositiveZero) || !Allowed(keys[i]._tangent, decodedTangents[i], strict, derivativeLimit, allowPositiveZero)) { valid = false; break; }
                        error = Math.Max(error, Math.Abs((double)keys[i]._value - decodedValues[i]) / Math.Max(valueLimit, double.Epsilon));
                        error = Math.Max(error, Math.Abs((double)keys[i]._tangent - decodedTangents[i]) / Math.Max(derivativeLimit, double.Epsilon));
                    }
                    if (valid && error < bestError) { best = curve; bestError = error; }
                }
            }
            return best;
        }

        private static bool Allowed(float original, float candidate, bool exact, double limit, bool allowPositiveZero)
        {
            if (Bits(original) == Bits(candidate)) return true;
            // Authored +/-zero always retains its exact bits. Only explicit
            // Small file rotation/translation may round a tiny nonzero value
            // or tangent to +zero, inside the same approved scalar bounds.
            // Exact/HQ and scale groups retain the previous zero policy.
            if (exact || original == 0 || candidate == 0 && (!allowPositiveZero || Bits(candidate) != 0)) return false;
            return Math.Abs((double)original - candidate) <= limit;
        }
        private static bool RequiresExactScale(KeyframeEntry[] keys)
        {
            if (keys.Any(k => k._value == 0)) return true;
            // Tangent overshoot may pass through zero despite like-signed knots.
            // Such a span is left exact unless a later dedicated proof qualifies it.
            for (int i = 1; i < keys.Length; i++)
                if (Math.Sign(keys[i]._value) != Math.Sign(keys[i - 1]._value) || keys[i]._tangent != 0 || keys[i - 1]._tangent != 0) return true;
            return false;
        }
        internal static string GroupKey(CHR0EntryNode entry, int group)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                {
                    bool fractional = entry.KeyArrays.Skip(group * 3).Take(3).SelectMany(a => a.Entries).Any(k => k._index != Math.Floor(k._index) || Bits(k._index)==int.MinValue);
                    if (fractional) writer.Write("CHR0-fractional-group-v1");
                    writer.Write(entry.Name); writer.Write(group); writer.Write(entry.Keyframes.FrameLimit); writer.Write(entry.Keyframes.Loop);
                    writer.Write(entry.UseModelScale); writer.Write(entry.UseModelRotate); writer.Write(entry.UseModelTranslate);
                    writer.Write(entry.ScaleCompensateApply); writer.Write(entry.ScaleCompensateParent); writer.Write(entry.ClassicScaleOff);
                    for (int channel = group * 3; channel < group * 3 + 3; channel++)
                    {
                        writer.Write(entry.KeyArrays[channel].KeyCount);
                        foreach (var key in entry.KeyArrays[channel].Entries)
                        { if (fractional) writer.Write(key._index); else writer.Write(checked((int)key._index)); writer.Write(Bits(key._value)); writer.Write(Bits(key._tangent)); }
                    }
                }
                using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "");
            }
        }
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
