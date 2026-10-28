using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading;

internal static partial class Program
{
    // Pure native writer/proposal checks; the caller's private-desktop suite adds
    // baseline scalar certificates, model/skin quality and document integration.
    static void NativeCompactStorageChecks(string stage)
    {
        Directory.CreateDirectory(stage);
        using (var original = CompactFixture(32, 3, Enumerable.Range(0, 16).Select(i => i).ToArray(), i => i * .125f, i => .125f))
        {
            string baseline = NativeAnimationOptimization.ContentKey(original);
            var entry = (CHR0EntryNode)original.Children[0];
            var alternatives = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, true, false, new int[0], 1);
            Check(alternatives.Length == 2 && alternatives[0].Format == AnimDataFormat.I4 && alternatives.All(p => p.IsExact),
                "Compact storage: exact dyadic curves offer both I4 and I6, ordered by measured payload bytes");
            using (var candidate = NativeAnimationOptimization.Copy(original))
            {
                NativeCompactAnimationStorage.ApplyGroup(candidate, alternatives[0]);
                string packed = Path.Combine(stage, "exact-i4.chr0"), precise = Path.Combine(stage, "precise-i12.chr0");
                candidate.ExportChecked(packed); original.ExportChecked(precise);
                using (var reopened = NodeFactory.FromFile(null, packed) as CHR0Node)
                {
                    var storage = PreciseCHR0EntryNode.CaptureStorage((CHR0EntryNode)reopened.Children[0]);
                    Check(reopened != null && NativeAnimationOptimization.ContentKey(reopened) == baseline && storage != null && storage.Format(1) == AnimDataFormat.I4 && new FileInfo(packed).Length < new FileInfo(precise).Length,
                        "Compact storage: independent I4 decode preserves all metadata/coefficients and actually shrinks the CHR0");
                    using (var copy = NativeAnimationOptimization.Copy(reopened))
                    {
                        PreciseCHR0EntryNode.RestoreStorage((CHR0EntryNode)copy.Children[0], storage);
                        string saved = Path.Combine(stage, "retained-i4.chr0"); copy.ExportChecked(saved);
                        Check(File.ReadAllBytes(saved).SequenceEqual(File.ReadAllBytes(packed)),
                            "Compact storage: restoring immutable storage preserves the complete encoded resource on repeated Save/export");
                        using (var cleared = NativeAnimationOptimization.Copy(copy))
                        {
                            string rebuilt = Path.Combine(stage, "rebuilt-packed-before-policy-undo.chr0"); cleared.ExportChecked(rebuilt);
                            PreciseCHR0EntryNode.RestoreStorage((CHR0EntryNode)cleared.Children[0], null);
                            Check(PreciseCHR0EntryNode.CaptureStorage((CHR0EntryNode)cleared.Children[0]) == null,
                                "Compact storage: explicitly cleared precise policy cannot resurrect matching packed bytes from an earlier rebuild");
                            using (var frozen = NativeAnimationOptimization.Copy(cleared))
                            {
                                string reset = Path.Combine(stage, "precise-after-policy-undo.chr0"); frozen.ExportChecked(reset);
                                using (var decoded = NodeFactory.FromFile(null, reset) as CHR0Node)
                                    Check(NativeAnimationOptimization.ContentKey(decoded) == baseline && PreciseCHR0EntryNode.CaptureStorage((CHR0EntryNode)decoded.Children[0]) == null &&
                                        new FileInfo(reset).Length == new FileInfo(precise).Length,
                                        "Compact storage: snapshots and export retain explicit I12 after storage Undo following a packed Save/rebuild");
                            }
                        }
                        var changed = (CHR0EntryNode)copy.Children[0]; changed.KeyArrays[3].Entries.Skip(4).First()._value += .000031f; changed.SignalPropertyChange();
                        string expected = NativeAnimationOptimization.ContentKey(copy), edited = Path.Combine(stage, "edited-fallback.chr0"); copy.ExportChecked(edited);
                        using (var decoded = NodeFactory.FromFile(null, edited) as CHR0Node)
                            Check(NativeAnimationOptimization.ContentKey(decoded) == expected && PreciseCHR0EntryNode.CaptureStorage((CHR0EntryNode)decoded.Children[0]) == null,
                                "Compact storage: editing a packed coefficient falls back to precise I12 without silently quantizing the edit");
                    }
                }
            }
            Check(NativeAnimationOptimization.ContentKey(original) == baseline,
                "Compact storage: preparation, encode and candidate changes preserve the original native snapshot");
        }

        using (var source = CompactFixture(320, 3, Enumerable.Range(0, 16).Select(i => i * 20).ToArray(), i => i * .125f, i => .125f))
        {
            var alternatives = NativeCompactAnimationStorage.CreateAlternatives((CHR0EntryNode)source.Children[0], 1, true, false, new int[0], 1);
            Check(alternatives.Length == 1 && alternatives[0].Format == AnimDataFormat.I6,
                "Compact storage: authored times above 255 use I6, preserving their exact original knot times");
            NativeCompactAnimationStorage.ApplyGroup(source, alternatives[0]); string path = Path.Combine(stage, "exact-i6.chr0"); source.ExportChecked(path);
            using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
                Check(NativeAnimationOptimization.ContentKey(decoded) == NativeAnimationOptimization.ContentKey(source) && PreciseCHR0EntryNode.CaptureStorage((CHR0EntryNode)decoded.Children[0]).Format(1) == AnimDataFormat.I6,
                    "Compact storage: independent I6 decode retains values, tangents, flags, timing and metadata");
        }

        using (var source = CompactFixture(32, 3, Enumerable.Range(0, 16).ToArray(), i => .0012345f + i * .1371397f, i => .1234567f))
        {
            var entry = (CHR0EntryNode)source.Children[0];
            Check(NativeCompactAnimationStorage.CreateAlternatives(entry, 1, true, false, new int[0], 1).Length == 0,
                "Compact storage: Exact never accepts an approximate value or tangent");
            var proposals = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], 1);
            Check(proposals.Length == 2 && proposals.All(p => !p.IsExact),
                "Compact storage: High-quality proposes bounded I4/I6 quantization for independent continuous and cumulative verification");
            Check(NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, true, new int[0], 1).Length == 0 &&
                NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new[] { 0, 15 }, 1).Length == 0,
                "Compact storage: protected controls and mandatory fitting joins cannot receive approximate tangents");
            using (var copy = NativeAnimationOptimization.Copy(source))
            {
                NativeCompactAnimationStorage.ApplyGroup(copy, proposals[0]); string expected = NativeAnimationOptimization.ContentKey(copy);
                string path = Path.Combine(stage, "hq-proposal-i4.chr0"); copy.ExportChecked(path);
                using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
                    Check(NativeAnimationOptimization.ContentKey(decoded) == expected,
                        "Compact storage: quantized payload independently decodes exactly to the reviewed proposed coefficient tuples");
            }
            entry.KeyArrays[3].Entries.First()._value += .1f; bool stale = false;
            try { NativeCompactAnimationStorage.ApplyGroup(source, proposals[0]); } catch (InvalidOperationException) { stale = true; }
            Check(stale, "Compact storage: changed authored coefficients invalidate the frozen storage proposal");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel(); bool stopped = false;
                try { NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], 1, cancelled.Token); } catch (OperationCanceledException) { stopped = true; }
                Check(stopped, "Compact storage: cancellation stops preparation before writing any candidate");
            }
        }

        using (var source = CompactFixture(2100, 3, new[] { 0, 2050, 2055 }, i => i * .125f, i => .125f))
            Check(NativeCompactAnimationStorage.CreateAlternatives((CHR0EntryNode)source.Children[0], 1, false, false, new int[0], 1).Length == 0,
                "Compact storage: times beyond I6 capacity retain precise storage rather than changing times or linearizing");
        using (var source = CompactFixture(32, 3, new[] { 0, 4, 8 }, i => i * .125f, i => 128f))
            Check(NativeCompactAnimationStorage.CreateAlternatives((CHR0EntryNode)source.Children[0], 1, false, false, new int[0], 1).Length == 0,
                "Compact storage: tangent overflow retains I12; it is never clamped");
        using (var source = CompactFixture(32, 3, new[] { 0, 4, 8 }, i => NegativeZero(), i => 0))
            Check(NativeCompactAnimationStorage.CreateAlternatives((CHR0EntryNode)source.Children[0], 1, false, false, new int[0], 1).Length == 0,
                "Compact storage: authored negative zero is preserved through exact-only fallback");
        using (var source = CompactFixture(32, 0, new[] { 0, 4, 8 }, i => i == 0 ? -1 : 1, i => .1234567f))
            Check(NativeCompactAnimationStorage.CreateAlternatives((CHR0EntryNode)source.Children[0], 0, false, false, new int[0], 1).Length == 0,
                "Compact storage: sign-changing scale retains exact coefficients without normalization");
        using (var source = CompactFixture(12, 3, new[] { 0, 4, 4, 8 }, i => i * .125f, i => .125f))
        {
            var entry = (CHR0EntryNode)source.Children[0]; string expected = NativeAnimationOptimization.ContentKey(source);
            var proposal = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, true, true, new int[0], 0).First();
            NativeCompactAnimationStorage.ApplyGroup(source, proposal); string path = Path.Combine(stage, "paired-i4.chr0"); source.ExportChecked(path);
            using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
                Check(NativeAnimationOptimization.ContentKey(decoded) == expected && ((CHR0EntryNode)decoded.Children[0]).KeyArrays[3].Entries.Count(k => k._index == 4) == 2,
                    "Compact storage: native I4 independently retains both authored paired-key sides, their values and tangents");
        }
        using (var source = CompactFixture(12, 3, new[] { 0, 4, 4, 8 }, i => i * .125f, i => .1234567f))
            Check(NativeCompactAnimationStorage.CreateAlternatives((CHR0EntryNode)source.Children[0], 1, false, false, new int[0], 1).Length == 0,
                "Compact storage: high quality cannot quantize either side of an authored paired key");
        using (var source = CompactFixture(20, 3, new[] { 0, 4, 4, 8 }, i => new[] { 1.123f, 2.234f, 2.234f, 3.456f }[i], i => i < 2 ? .1234567f : .2234567f))
        {
            var entry = (CHR0EntryNode)source.Children[0];
            var choices = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], 1, true, new int[0]);
            Check(choices.Length > 0 && choices.All(p => p.Curves[0].Frames.SequenceEqual(new[] { 0, 4, 4, 8 })),
                "Bounded compact storage retains both continuous body sides while offering smaller decoded tangent coefficients");
            Check(NativeCompactAnimationStorage.CreateAlternatives(entry, 1, true, true, new int[0], 1, true, new int[0]).Length == 0 &&
                NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], 1).Length == 0,
                "Exact roots and the strict quality profile retain the original continuous paired tangent bits");
            NativeCompactAnimationStorage.ApplyGroup(source, choices[0]);
            string path = Path.Combine(stage, "bounded-continuous-pair.chr0"); source.ExportChecked(path);
            using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
                Check(((CHR0EntryNode)decoded.Children[0]).KeyArrays[3].Entries.Count(k => k._index == 4) == 2 &&
                    ((CHR0EntryNode)decoded.Children[0]).KeyArrays[3].Entries.Where(k => k._index == 4).Select(k => ReplayFloatBits(k._value)).Distinct().Count() == 1,
                    "Independent compact decode retains continuous paired values and both ordered tangent sides");
        }
        using (var source = CompactFixture(20, 3, Enumerable.Range(0, 16).ToArray(), i => i * 48f, i => 48f))
        {
            string expected = NativeAnimationOptimization.ContentKey(source);
            var proposal = NativeCompactAnimationStorage.CreateAlternatives((CHR0EntryNode)source.Children[0], 1, true, true, new int[0], 0).First();
            NativeCompactAnimationStorage.ApplyGroup(source, proposal); string path = Path.Combine(stage, "full-turns-i4.chr0"); source.ExportChecked(path);
            using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
                Check(NativeAnimationOptimization.ContentKey(decoded) == expected && ((CHR0EntryNode)decoded.Children[0]).KeyArrays[3].Entries.Last()._value == 720,
                    "Compact storage: exact indexed storage retains raw authored 720-degree turns without wrapping or quaternion conversion");
        }
        using (var empty = new CHR0Node { Name = "Empty native animation", Version = 4, FrameCount = 1 })
        {
            var entry = empty.CreateEntry("Independent unchanged target bone");
            Check(NativeCompactAnimationStorage.CreateAlternatives(entry, 0, true, false, new int[0], 0).Length == 0,
                "Compact storage: empty and absent groups remain absent without manufacturing animation keys");
        }
    }

    static CHR0Node CompactFixture(int count, int channel, int[] frames, Func<int, float> values, Func<int, float> tangents)
    {
        var clip = new CHR0Node { Name = "Independent compact storage fixture", Version = 4, FrameCount = count, Loop = true, OriginalPath = "original native metadata" };
        var entry = clip.CreateEntry("Independent target bone");
        for (int axis = channel / 3 * 3; axis < channel / 3 * 3 + 3; axis++)
            if (axis != channel) entry.Keyframes.SetFrameValue(axis, 0, channel < 3 ? 1 : 0, true)._tangent = 0;
        for (int i = 0; i < frames.Length; i++) entry.Keyframes.SetFrameValue(channel, frames[i], values(i), true)._tangent = tangents(i);
        entry.Keyframes.PreserveInterpolation = true; PreciseCHR0EntryNode.Promote(clip); return clip;
    }
    static float NegativeZero() => BitConverter.ToSingle(BitConverter.GetBytes(unchecked((int)0x80000000)), 0);
}
