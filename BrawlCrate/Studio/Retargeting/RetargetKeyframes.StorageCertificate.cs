using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BrawlCrate.Studio.Retargeting
{
    internal static partial class RetargetKeyframes
    {
        sealed class QualityStorageKnot
        {
            internal float Time;
            internal KeyframeEntry Incoming, Outgoing;
        }

        // Compact storage may spend only the remaining error of the original
        // full-quality TARGET. The source clip and the progressively accepted
        // indexed candidate are never compression references. Native complete
        // FK/skin verification follows this scalar certificate in PrepareCompact.
        static NativeCompactCurveCertificate QualityStorageCertificate(CHR0Node dense, CHR0Node frozenPrecise,
            CHR0Node decoded, QualityProofBudget proof, double length, HashSet<string> exactControls,
            NativeGeneratedPairSite[] knownGeneratedPairs, string[] bindOmissions)
        {
            NativeCompactCurveCertificate Fail(string reason, string bone = null, int? channel = null)
                => new NativeCompactCurveCertificate { Accepted = false, BudgetExhausted = proof.Limited,
                    Report = reason, FailedBone = bone, FailedChannel = channel, ProbeFrames = proof.Probes.ToArray() };

            if (dense == null || frozenPrecise == null || decoded == null || proof == null || exactControls == null)
                throw new ArgumentNullException("Native source-key storage certification requires frozen target references and the shared scalar budget.");
            proof.Token.ThrowIfCancellationRequested();
            if (!proof.Charge(1)) return Fail("The shared continuous source-key proof budget is exhausted.");
            if (double.IsNaN(length) || double.IsInfinity(length) || length <= 0)
                return Fail("Approximate native source-key storage requires the verified positive target geometry scale.");
            if (dense.FrameCount != decoded.FrameCount || dense.Loop != decoded.Loop || dense.Version != decoded.Version ||
                frozenPrecise.FrameCount != decoded.FrameCount || frozenPrecise.Loop != decoded.Loop || frozenPrecise.Version != decoded.Version)
                return Fail("Compact storage changed frozen native timing, loop state or version.");

            var originals = dense.Children.OfType<CHR0EntryNode>().ToArray();
            var precise = frozenPrecise.Children.OfType<CHR0EntryNode>().ToArray();
            var actual = decoded.Children.OfType<CHR0EntryNode>().ToArray();
            if (originals.Length != dense.Children.Count || precise.Length != frozenPrecise.Children.Count || actual.Length != decoded.Children.Count ||
                originals.Select(e => e.Name).Distinct(StringComparer.Ordinal).Count() != originals.Length ||
                !precise.Select(e => e.Name).SequenceEqual(actual.Select(e => e.Name), StringComparer.Ordinal))
                return Fail("Compact storage changed or ambiguously identifies the qualified native track membership/order.");
            var originalByName = originals.ToDictionary(e => e.Name, StringComparer.Ordinal);
            var omitted = new HashSet<string>(bindOmissions ?? new string[0], StringComparer.Ordinal);
            var present = new HashSet<string>(actual.Select(e => e.Name), StringComparer.Ordinal);
            foreach (var original in originals)
                if (!present.Contains(original.Name) && (!omitted.Contains(original.Name) || exactControls.Contains(original.Name)))
                    return Fail("A native target track is absent without the already verified exact bind-omission proof.", original.Name);
            var generated = (knownGeneratedPairs ?? new NativeGeneratedPairSite[0]).Where(p => p != null)
                .GroupBy(p => (p.Bone, p.Channel)).ToDictionary(g => g.Key, g => new HashSet<float>(g.Select(p => p.Frame)));
            var maximumValue = new double[3]; var maximumDerivative = new double[3];
            double nativeEnd = dense.Loop ? dense.FrameCount : Math.Max(0, dense.FrameCount - .0001);

            for (int entryIndex = 0; entryIndex < actual.Length; entryIndex++)
            {
                proof.Token.ThrowIfCancellationRequested();
                var entry = actual[entryIndex]; var preparedEntry = precise[entryIndex];
                if (!originalByName.TryGetValue(entry.Name, out CHR0EntryNode originalEntry) ||
                    QualityStorageFlags(originalEntry) != QualityStorageFlags(entry) ||
                    QualityStorageFlags(preparedEntry) != QualityStorageFlags(entry) ||
                    originalEntry.KeyArrays.Length != 9 || preparedEntry.KeyArrays.Length != 9 || entry.KeyArrays.Length != 9)
                    return Fail("Compact storage changed a native track's transform semantics or introduced a track.", entry.Name);
                for (int channel = 0; channel < 9; channel++)
                {
                    var beforeArray = originalEntry.KeyArrays[channel]; var afterArray = entry.KeyArrays[channel];
                    if (beforeArray.Loop != afterArray.Loop || beforeArray.FrameLimit != afterArray.FrameLimit)
                        return Fail("Compact storage changed a native channel's terminal or loop-evaluation context.", entry.Name, channel);
                    var before = beforeArray.Entries.ToArray(); var after = afterArray.Entries.ToArray();
                    var prepared = preparedEntry.KeyArrays[channel].Entries.ToArray();
                    if (!proof.Charge(checked(before.Length + after.Length + prepared.Length + 1)))
                        return Fail("The shared continuous source-key proof budget is exhausted.", entry.Name, channel);
                    if (!QualityStorageValid(before, dense.FrameCount, dense.Loop) || !QualityStorageValid(after, dense.FrameCount, dense.Loop) ||
                        !QualityStorageSameTimes(prepared, after))
                        return Fail("Indexed storage changed a qualified knot's time/membership or contains invalid native coefficients.", entry.Name, channel);
                    if (QualityStorageSameCoefficients(before, after)) continue;
                    if (before.Length == 0 || after.Length == 0)
                        return Fail("Compact storage cannot substitute an absent native channel for an authored channel.", entry.Name, channel);
                    generated.TryGetValue((entry.Name, channel), out HashSet<float> known);
                    var groupsBefore = QualityStorageKnots(before); var groupsAfter = QualityStorageKnots(after);
                    var groupsPrepared = QualityStorageKnots(prepared);
                    bool unknownPair = groupsBefore.Concat(groupsPrepared).Any(g => !ReferenceEquals(g.Incoming, g.Outgoing) &&
                        (known == null || !known.Contains(g.Time) || !SameBits(g.Incoming._value, g.Outgoing._value)));
                    bool signedZero = before.Concat(after).Any(k => NegativeZero(k._index) || NegativeZero(k._value) || NegativeZero(k._tangent));
                    if (exactControls.Contains(entry.Name) || unknownPair || signedZero)
                        return Fail("Exact motion controls, authored/unknown paired sides and signed-zero channels retain their complete original coefficients.", entry.Name, channel);
                    if (groupsAfter.Any(g => !ReferenceEquals(g.Incoming, g.Outgoing) && !SameBits(g.Incoming._value, g.Outgoing._value)))
                        return Fail("Compact storage introduced a discontinuity at a generated continuous native pair.", entry.Name, channel);

                    // This exact constant proof also covers a fixed singleton,
                    // zero scale and a loop without explicit boundary knots.
                    // Equal values alone are insufficient: all slopes must be
                    // positive zero and IEEE signed zeros were protected above.
                    if (QualityStorageEqualConstants(before, after)) continue;
                    if (channel < 3 && (!QualitySignedScale(before, proof) || !QualitySignedScale(after, proof) ||
                        Math.Sign(before[0]._value) != Math.Sign(after[0]._value)))
                        return Fail("Singular, sign-changing or unproved native scale remains exact-only.", entry.Name, channel);
                    // FrameLimit is N+1 for a native looping CHR0. Explicit
                    // 0/N keys do not by themselves certify its N -> N+1 wrap.
                    // Until that separate periodic polynomial is qualified,
                    // every changed nonconstant looping channel stays exact.
                    if (dense.Loop)
                        return Fail("Nonconstant looping channels retain their coefficients: the complete native FrameLimit wrap has not been independently certified for approximate storage.", entry.Name, channel);

                    var boundaries = groupsBefore.Select(g => g.Time).Concat(groupsAfter.Select(g => g.Time))
                        .Concat(new[] { 0f, (float)dense.FrameCount }).Distinct().OrderBy(t => t).ToArray();
                    double valueLimit = channel < 3 ? .00001 : channel < 6 ? .01 : Math.Min(.0002, .0001 * length);
                    double derivativeLimit = channel >= 3 && channel < 6 ? .05 : valueLimit;
                    // Interval hulls cover incoming/outgoing slopes wherever
                    // an adjacent interval exists. Native derivative consumers
                    // also expose the first incoming and final outgoing side
                    // at an exact endpoint, despite the surrounding hold.
                    // Keep the qualified endpoint times and bound these two
                    // otherwise-unused authored tangent sides explicitly.
                    if (!SameBits(before[0]._index, after[0]._index) ||
                        !SameBits(before[before.Length - 1]._index, after[after.Length - 1]._index))
                        return Fail("Compact source-key storage changed a nonconstant native channel's endpoint coverage.", entry.Name, channel);
                    foreach (var endpoints in new[] { Tuple.Create(before[0], after[0]),
                        Tuple.Create(before[before.Length - 1], after[after.Length - 1]) })
                    {
                        if (!proof.Charge(2))
                            return Fail("The shared continuous source-key proof budget is exhausted.", entry.Name, channel);
                        var difference = (SourceRational.FromFloat(endpoints.Item1._tangent) - SourceRational.FromFloat(endpoints.Item2._tangent)).Abs();
                        if (difference > SourceRational.FromDouble(derivativeLimit))
                            return Fail("The complete decoded candidate exceeds the original target's native endpoint-side tangent bound.", entry.Name, channel);
                        maximumDerivative[channel / 3] = Math.Max(maximumDerivative[channel / 3], difference.UpperDouble());
                    }
                    int beforeIndex = 0, afterIndex = 0;
                    for (int interval = 1; interval < boundaries.Length; interval++)
                    {
                        float first = boundaries[interval - 1], last = boundaries[interval];
                        if (last <= first) continue;
                        if (!proof.Span() || !proof.Charge(8))
                            return Fail("The shared continuous source-key proof budget is exhausted.", entry.Name, channel);
                        var original = QualityStorageBernstein(groupsBefore, ref beforeIndex, first, last);
                        var candidate = QualityStorageBernstein(groupsAfter, ref afterIndex, first, last);
                        var difference = original.Zip(candidate, (a, b) => a - b).ToArray();
                        if (!QualityBound(difference, SourceRational.FromDouble(valueLimit), proof, out double valueError))
                            return Fail("The complete decoded candidate exceeds or cannot prove the original target's continuous raw Hermite value bound.", entry.Name, channel);
                        SourceRational dt = SourceRational.FromFloat(last) - SourceRational.FromFloat(first);
                        var derivative = Enumerable.Range(0, 3).Select(i => 3 * (difference[i + 1] - difference[i]) / dt).ToArray();
                        if (!QualityBound(derivative, SourceRational.FromDouble(derivativeLimit), proof, out double derivativeError))
                            return Fail("The complete decoded candidate exceeds or cannot prove the original target's continuous Hermite derivative bound.", entry.Name, channel);
                        maximumValue[channel / 3] = Math.Max(maximumValue[channel / 3], valueError);
                        maximumDerivative[channel / 3] = Math.Max(maximumDerivative[channel / 3], derivativeError);
                        // Native cumulative validation has its own mandatory
                        // quarter frames and held-out samples. Add original /
                        // candidate boundaries and interior irrational witnesses
                        // within its real nonloop display domain, not frame N.
                        foreach (double frame in new[] { (double)first, first + ((double)last - first) * .381966011250105,
                            first + ((double)last - first) * .618033988749895, last })
                            if (frame <= nativeEnd) proof.Probe(frame);
                    }
                }
            }
            string G(double value) => value.ToString("G9", CultureInfo.InvariantCulture);
            return new NativeCompactCurveCertificate { Accepted = true, ProbeFrames = proof.Probes.ToArray(),
                Report = "Complete freshly decoded indexed storage versus the frozen full-quality TARGET: continuous rational Bernstein raw Hermite value/derivative bounds passed. " +
                    "Certified scale " + G(maximumValue[0]) + ", Euler " + G(maximumValue[1]) + " degrees, translation " + G(maximumValue[2]) +
                    "; derivative per native frame scale " + G(maximumDerivative[0]) + ", Euler " + G(maximumDerivative[1]) + " degrees, translation " + G(maximumDerivative[2]) +
                    ". Exact controls, unknown paired sides, signed zeros and nonconstant looping/wrap semantics were preserved. Native floating-point FK/skin sampling is a separate cumulative gate." };
        }

        static int QualityStorageFlags(CHR0EntryNode entry) => (entry.UseModelScale ? 1 : 0) | (entry.UseModelRotate ? 2 : 0) |
            (entry.UseModelTranslate ? 4 : 0) | (entry.ScaleCompensateApply ? 8 : 0) | (entry.ScaleCompensateParent ? 16 : 0) | (entry.ClassicScaleOff ? 32 : 0);

        static bool QualityStorageValid(KeyframeEntry[] keys, int frames, bool loop)
        {
            float previous = -1; int sides = 0;
            foreach (var key in keys)
            {
                if (float.IsNaN(key._index) || float.IsInfinity(key._index) || key._index < 0 || key._index > (loop ? frames : frames - 1) ||
                    key._index < previous || float.IsNaN(key._value) || float.IsInfinity(key._value) || float.IsNaN(key._tangent) || float.IsInfinity(key._tangent)) return false;
                sides = key._index == previous ? sides + 1 : 1; previous = key._index;
                if (sides > 2) return false;
            }
            return true;
        }

        static bool QualityStorageSameTimes(KeyframeEntry[] before, KeyframeEntry[] after)
            => before.Length == after.Length && !before.Where((k, i) => !SameBits(k._index, after[i]._index)).Any();

        static bool QualityStorageSameCoefficients(KeyframeEntry[] before, KeyframeEntry[] after)
            => QualityStorageSameTimes(before, after) && !before.Where((k, i) => !SameBits(k._value, after[i]._value) || !SameBits(k._tangent, after[i]._tangent)).Any();

        static bool QualityStorageEqualConstants(KeyframeEntry[] before, KeyframeEntry[] after)
            => before.Length > 0 && after.Length > 0 && before.Concat(after).All(k => SameBits(k._value, before[0]._value) && PositiveZero(k._tangent));

        static QualityStorageKnot[] QualityStorageKnots(KeyframeEntry[] keys)
            => keys.GroupBy(k => k._index).Select(g => new QualityStorageKnot { Time = g.Key, Incoming = g.First(), Outgoing = g.Last() }).ToArray();

        static SourceRational[] QualityStorageBernstein(QualityStorageKnot[] groups, ref int index, float first, float last)
        {
            while (index + 1 < groups.Length && groups[index + 1].Time <= first) index++;
            // Native nonloop channels hold the incoming first value before the
            // first key and the outgoing last value after the last key.
            // Nonconstant loop changes never reach this path.
            if (last <= groups[0].Time)
            {
                var value = SourceRational.FromFloat(groups[0].Incoming._value);
                return new[] { value, value, value, value };
            }
            if (first >= groups[groups.Length - 1].Time)
            {
                var value = SourceRational.FromFloat(groups[groups.Length - 1].Outgoing._value);
                return new[] { value, value, value, value };
            }
            // A paired time owns the incoming first side and outgoing last
            // side. Partitioning at the complete union ensures no interval
            // crosses a jump or confuses those two native Hermite tangents.
            return QualityBernstein(groups[index].Outgoing, groups[index + 1].Incoming, first, last);
        }
    }
}
