using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.IO;
using System.Linq;

internal static partial class Program
{
    static void NativeSmallFileZeroGridChecks(string stage, double length)
    {
        var frames = Enumerable.Range(0, 16).ToArray();
        using (var source = CompactFixture(20, 3, frames, i => 3 + i * .125f, i => 1f / 4096))
        {
            var entry = (CHR0EntryNode)source.Children[0]; string before = NativeAnimationOptimization.ContentKey(source);
            var strict = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], length);
            var small = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], length, true, new int[0]);
            Check(strict.Length == 0 && small.Any(p => p.Format == AnimDataFormat.I4 && !p.IsExact && p.Curves[0].Tangents.All(v => ReplayFloatBits(v) == 0)),
                "Small zero packing: only the explicit Small profile may round an independently authored 1/4096-degree tangent to +zero inside its approved derivative bound");
            Check(small.All(p => p.Curves[0].Values.Select((v, i) => Math.Abs(v - (double)entry.KeyArrays[3].GetKeyframe(i)._value)).Max() <= .1 &&
                p.Curves[0].Tangents.All(v => Math.Abs(v - 1d / 4096) < .1)),
                "Small zero packing: tangent cleanup retains the unwrapped original value path within its frozen value/derivative coefficient bounds");
            Check(NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, true, new int[0], length, true, new int[0]).Length == 0 &&
                NativeCompactAnimationStorage.CreateAlternatives(entry, 1, true, false, new int[0], length, true, new int[0]).Length == 0 &&
                NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, frames, length, true, new int[0]).Length == 0,
                "Small zero packing cannot loosen exact source controls, pin/contact groups, ExactOnly or explicit mandatory tangents");
            Check(NativeAnimationOptimization.ContentKey(source) == before,
                "Small zero packing proposal generation preserves every original coefficient and native semantic flag");
        }

        using (var source = CompactFixture(20, 3, frames, i => i == 0 ? -.01321f : i == 1 ? 0 : (i - 1) * .125f, i => 0))
        {
            var entry = (CHR0EntryNode)source.Children[0];
            var strict = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], length);
            var small = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], length, true, new int[0]);
            var packed = small.FirstOrDefault(p => p.Format == AnimDataFormat.I4);
            Check(strict.Length == 0 && packed != null && ReplayFloatBits(packed.Curves[0].Values[1]) == 0,
                "Small zero packing: a supplementary zero-aligned native grid keeps an authored +zero exactly when the unchanged minimum-only HQ grid cannot encode it");
            Check(small.All(p => p.Curves[0].Frames.SequenceEqual(frames) && ReplayFloatBits(p.Curves[0].Values[1]) == 0 &&
                p.Curves[0].Tangents.All(v => ReplayFloatBits(v) == 0) && p.Curves[0].Values.Select((v, i) => Math.Abs(v - (double)entry.KeyArrays[3].GetKeyframe(i)._value)).Max() <= .1),
                "Small zero packing preserves every authored +zero value/tangent and exact knot time while all proposed coefficients remain within the frozen bound");
            using (var candidate = NativeAnimationOptimization.Copy(source))
            {
                NativeCompactAnimationStorage.ApplyGroup(candidate, packed);
                string expected = NativeAnimationOptimization.ContentKey(candidate), path = Path.Combine(stage, "small-zero-aligned-i4.chr0"); candidate.ExportChecked(path);
                using (var reopened = NodeFactory.FromFile(null, path) as CHR0Node)
                    Check(reopened != null && NativeAnimationOptimization.ContentKey(reopened) == expected &&
                        ReplayFloatBits(((CHR0EntryNode)reopened.Children[0]).KeyArrays[3].GetKeyframe(1)._value) == 0 &&
                        PreciseCHR0EntryNode.CaptureStorage((CHR0EntryNode)reopened.Children[0]).Format(1) == AnimDataFormat.I4,
                        "Small zero packing independently reopens the exact reviewed I4 proposal, including its authored zero and complete metadata");
            }
        }

        foreach (int channel in new[] { 3, 6 })
        using (var source = CompactFixture(20, channel, frames, i => i == 0 ? 0 : i == 1 ? 1f / 8192 : i == 2 ? 1 : i == 15 ? 7.9f : i * .125f, i => 0))
        {
            int group = channel / 3; var entry = (CHR0EntryNode)source.Children[0];
            var small = NativeCompactAnimationStorage.CreateAlternatives(entry, group, false, false, new[] { 2 }, length, true, new int[0]);
            var packed = small.FirstOrDefault(p => p.Format == AnimDataFormat.I4);
            Check(packed != null && ReplayFloatBits(packed.Curves[0].Values[0]) == 0 && ReplayFloatBits(packed.Curves[0].Values[1]) == 0 &&
                ReplayFloatBits(packed.Curves[0].Values[2]) == ReplayFloatBits(1f),
                "Small zero packing: a tiny nonzero body coefficient may become +zero while authored +zero and a different explicit mandatory value remain exact / channel " + channel);
            Check(NativeCompactAnimationStorage.CreateAlternatives(entry, group, false, false, new[] { 1, 2 }, length, true, new int[0]).All(p => ReplayFloatBits(p.Curves[0].Values[1]) == ReplayFloatBits(1f / 8192)),
                "Small zero packing cannot erase a tiny nonzero coefficient when its own frame is explicitly mandatory / channel " + channel);
        }

        using (var source = CompactFixture(20, 3, frames, i => i == 1 ? NegativeZero() : i * .125f, i => 0))
        {
            var entry = (CHR0EntryNode)source.Children[0];
            Check(NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], length, true, new int[0]).All(p => ReplayFloatBits(p.Curves[0].Values[1]) == int.MinValue),
                "Small zero packing never normalizes an authored negative-zero value when exploring additional grids");
        }
        using (var source = CompactFixture(20, 3, new[] { 0, 4, 4, 8 }, i => i * .125f, i => 1f / 4096))
        {
            var entry = (CHR0EntryNode)source.Children[0];
            Check(NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], length, true, new int[0]).All(p =>
                p.Curves[0].Tangents[1] == 1f / 4096 && p.Curves[0].Tangents[2] == 1f / 4096),
                "Small zero packing cannot round the tiny authored tangent on either side of a paired native knot");
        }
        using (var source = CompactFixture(20, 0, frames, i => i == 0 ? 1f / 8192 : i == 15 ? 7.9f : i * .125f, i => 0))
        {
            var entry = (CHR0EntryNode)source.Children[0];
            Check(NativeCompactAnimationStorage.CreateAlternatives(entry, 0, false, false, new int[0], length, true, new int[0]).All(p => p.Curves.Where(c => c != null).All(c => c.Values.All(v => v != 0))),
                "Small zero packing never introduces a singular zero scale from an originally positive scale coefficient");
        }
        using (var source = CompactFixture(20, 0, frames, i => i == 0 ? -1 : 1 + i * .125f, i => 1f / 4096))
        {
            var entry = (CHR0EntryNode)source.Children[0];
            Check(NativeCompactAnimationStorage.CreateAlternatives(entry, 0, false, false, new int[0], length, true, new int[0]).All(p => p.IsExact),
                "Small zero packing retains exact-only sign-changing scale and its tiny nonzero authored tangents");
        }
    }
}
