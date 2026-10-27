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
                var keys = entry.KeyArrays[channel].Entries.ToArray(); var frames = curve.Frames; var values = curve.Values; var tangents = curve.Tangents;
                if (keys.Length != frames.Length) throw new InvalidOperationException("The native storage key membership changed.");
                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i]._index != frames[i]) throw new InvalidOperationException("Native indexed storage cannot move authored knot times.");
                    keys[i]._value = values[i]; keys[i]._tangent = tangents[i];
                }
            }
            entry.Keyframes.PreserveInterpolation = true;
            PreciseCHR0EntryNode.RestoreStorage(entry, new IndexedCHR0EntryStorage(channels));
        }

        private static IndexedCHR0CurveStorage TryCurve(KeyframeEntry[] keys, AnimDataFormat format, bool exact,
            HashSet<int> mandatory, double valueLimit, double derivativeLimit, bool zeroAlignedGrid, bool allowPositiveZero, CancellationToken token)
        {
            int frameLimit = format == AnimDataFormat.I4 ? 255 : 2047, valueMaximum = format == AnimDataFormat.I4 ? 4095 : 65535;
            int tangentMinimum = format == AnimDataFormat.I4 ? -2048 : -32768, tangentMaximum = format == AnimDataFormat.I4 ? 2047 : 32767;
            double tangentScale = format == AnimDataFormat.I4 ? 32 : 256;
            if (keys.Length > ushort.MaxValue || keys.Any(k => k._index < 0 || k._index > frameLimit || !Finite(k._value) || !Finite(k._tangent))) return null;
            var duplicateFrames = new HashSet<int>(keys.GroupBy(k => k._index).Where(g => g.Count() > 1).Select(g => g.Key));
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
                        bool strict = exact || mandatory.Contains(keys[i]._index) || duplicateFrames.Contains(keys[i]._index);
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
        private static string GroupKey(CHR0EntryNode entry, int group)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(entry.Name); writer.Write(group); writer.Write(entry.Keyframes.FrameLimit); writer.Write(entry.Keyframes.Loop);
                    writer.Write(entry.UseModelScale); writer.Write(entry.UseModelRotate); writer.Write(entry.UseModelTranslate);
                    writer.Write(entry.ScaleCompensateApply); writer.Write(entry.ScaleCompensateParent); writer.Write(entry.ClassicScaleOff);
                    for (int channel = group * 3; channel < group * 3 + 3; channel++)
                    {
                        writer.Write(entry.KeyArrays[channel].KeyCount);
                        foreach (var key in entry.KeyArrays[channel].Entries)
                        { writer.Write(key._index); writer.Write(Bits(key._value)); writer.Write(Bits(key._tangent)); }
                    }
                }
                using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "");
            }
        }
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
