using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

internal static partial class Program
{
    static void NativeOptimizationTimeBitsChecks(string stage)
    {
        Directory.CreateDirectory(stage);
        var json = new JavaScriptSerializer();
        json.RegisterConverters(new[] { new ControlKeyTimeConverter() });
        foreach (string text in new[] {
            "{\"Frame\":0,\"FrameBits\":-2147483648,\"Value\":0,\"Tangent\":0}",
            "{\"FrameBits\":-2147483648,\"Frame\":0,\"Value\":0,\"Tangent\":0}" })
        {
            var key = json.Deserialize<ControlKey>(text);
            Check(ReplayFloatBits(key.Frame) == int.MinValue, "Native optimization time bits: mutable control keys recover negative zero independently of field order");
            string encoded = json.Serialize(key);
            Check(encoded.Contains("\"FrameBits\":-2147483648") && ReplayFloatBits(json.Deserialize<ControlKey>(encoded).Frame) == int.MinValue,
                "Native optimization time bits: control transport explicitly roundtrips time bits through legacy JSON");
            key.Frame = 1.03125f;
            Check(key.FrameBits == ReplayFloatBits(1.03125f) && json.Deserialize<ControlKey>(json.Serialize(key)).Frame == 1.03125f,
                "Native optimization time bits: later deliberate time editing replaces the old metadata without a sticky bit override");
        }
        var scalars = json.Deserialize<ControlKey>("{\"Frame\":1.03125,\"Value\":0,\"ValueBits\":-2147483648,\"Tangent\":0,\"TangentBits\":-2147483648}");
        Check(scalars.Frame == 1.03125f && ReplayFloatBits(scalars.Value) == int.MinValue && ReplayFloatBits(scalars.Tangent) == int.MinValue,
            "Native optimization time bits: the same transport retains signed native values and tangents");
        bool mismatch = false;
        try { json.Deserialize<ControlKey>("{\"Frame\":1,\"FrameBits\":1069547520}"); }
        catch (Exception error) { mismatch = error is InvalidDataException || error.InnerException is InvalidDataException; }
        Check(mismatch, "Native optimization time bits: contradictory frame text and raw bits are rejected");

        var bitsType = typeof(ModelForm).GetNestedType("OptimizationKeyBits", BindingFlags.NonPublic);
        var parse = typeof(ModelForm).GetMethod("OptimizationTime", BindingFlags.NonPublic | BindingFlags.Static);
        var legacy = new JavaScriptSerializer();
        float Time(string text) => (float)parse.Invoke(null, new[] { legacy.Deserialize(text, bitsType) });
        Check(ReplayFloatBits(Time("{\"Frame\":0,\"FrameBits\":-2147483648}")) == int.MinValue &&
            Time("{\"Frame\":0.03125}") == .03125f,
            "Native optimization time bits: output parsing restores authoritative signed time and remains compatible with older frame-only payloads");
        bool invalid = false;
        try { Time("{\"Frame\":1,\"FrameBits\":1069547520}"); }
        catch (TargetInvocationException error) { invalid = error.InnerException is InvalidDataException; }
        Check(invalid, "Native optimization time bits: prepared output rejects inconsistent time metadata before native materialization");

        using (var clip = new CHR0Node { Name = "signed time fixture", FrameCount = 3 })
        {
            var entry = clip.CreateEntry("existing body");
            float negativeZero = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
            entry.Keyframes.SetFrameValue(3, negativeZero, 2, true)._tangent = 0;
            entry.Keyframes.SetFrameValue(3, 2, 3, true)._tangent = .25f;
            var wire = typeof(ModelForm).GetMethod("OptimizationWireTracks", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { clip });
            string input = legacy.Serialize(wire);
            Check(input.Contains("\"FrameBits\":-2147483648"), "Native optimization time bits: input capture supplies every original raw time bit before legacy serialization");
            File.WriteAllText(Path.Combine(stage, "optimization-input-wire.json"), input);
            var channels = entry.KeyArrays.Select(c => c.Entries.Select(k => new ControlKey { Frame = k._index, Value = k._value, Tangent = k._tangent }).ToArray()).ToArray();
            var same = typeof(NativeAnimationOptimization).GetMethod("SameChannels", BindingFlags.NonPublic | BindingFlags.Static);
            Check((bool)same.Invoke(null, new object[] { entry, channels }), "Native optimization time bits: untouched exact channels match their original signed time metadata");
            channels[3][0].Frame = 0;
            Check(!(bool)same.Invoke(null, new object[] { entry, channels }), "Native optimization time bits: protected-channel comparison distinguishes positive and negative zero times");
            var snapshotType = typeof(ModelForm).GetNestedType("NativeControlSnapshot", BindingFlags.NonPublic);
            var capture = snapshotType.GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static);
            var content = snapshotType.GetProperty("ContentKey", BindingFlags.NonPublic | BindingFlags.Instance);
            string Snapshot() => (string)content.GetValue(capture.Invoke(null, new object[] { clip, new[] { entry.Name } }));
            string before = Snapshot(); entry.KeyArrays[3].Entries.First()._index = 0;
            Check(Snapshot() != before, "Native optimization time bits: native Undo/export dependency snapshots detect a time-sign-only change");
        }
        var fixedProof = typeof(NativeAnimationOptimization).GetMethod("ExactFixedChannel", BindingFlags.NonPublic | BindingFlags.Static);
        var minus = new KeyframeEntry(BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0), 2);
        Check(!(bool)fixedProof.Invoke(null, new object[] { new[] { minus }, new[] { new ControlKey { Frame = 0, Value = 2 } } }),
            "Native optimization time bits: the native constant shortcut cannot discard a signed-zero time knot");
        var pairedConstant = new[] { new KeyframeEntry(0, 2), new KeyframeEntry(4, 2), new KeyframeEntry(4, 2), new KeyframeEntry(8, 2) };
        var fixedConstant = new[] { new KeyframeEntry(0, 2) };
        Check(ReplayConstantCollapseExact(pairedConstant, fixedConstant),
            "Boundary oracle independently accepts identical paired constant sides with exact positive-zero slopes");
        pairedConstant[2]._value = 3;
        Check(!ReplayConstantCollapseExact(pairedConstant, fixedConstant), "Boundary oracle rejects distinct values across paired sides");
        pairedConstant[2]._value = 2; pairedConstant[2]._tangent = .25f;
        Check(!ReplayConstantCollapseExact(pairedConstant, fixedConstant), "Boundary oracle rejects equal key values with nonzero tangent motion");
        pairedConstant[2]._tangent = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
        Check(!ReplayConstantCollapseExact(pairedConstant, fixedConstant), "Boundary oracle retains signed-zero tangent distinctions");
        pairedConstant[2]._tangent = 0; pairedConstant[0]._index = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
        Check(!ReplayConstantCollapseExact(pairedConstant, fixedConstant), "Boundary oracle retains signed-zero time distinctions");
        NativeFractionalI6StorageChecks(stage);
        NativeIndexedSingletonEditChecks(stage);
        NativeGeneratedPairProvenanceChecks(Path.Combine(stage, "generated-pair-provenance"));
        NativeRootPairOracleChecks();
    }

    static unsafe void NativeFractionalI6StorageChecks(string stage)
    {
        using (var original = new CHR0Node { Name = "fractional I6 winner", FrameCount = 5, Loop = true, Version = 4, OriginalPath = "original native metadata" })
        {
            var entry = original.CreateEntry("existing native bone");
            float[] times = { 0, .5f, 1.125f, 4.5f };
            for (int i = 0; i < times.Length; i++) entry.Keyframes.SetFrameValue(3, times[i], times[i], true)._tangent = 1;
            entry.Keyframes.SetFrameValue(4, 0, 0, true); entry.Keyframes.SetFrameValue(5, 0, 0, true);
            entry.Keyframes.PreserveInterpolation = true; PreciseCHR0EntryNode.Promote(original);
            entry = (CHR0EntryNode)original.Children[0];
            string originalContent = NativeAnimationOptimization.SourceContentKey(original);
            var proposals = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, true, false, new int[0], 1);
            Check(proposals.Length == 1 && proposals[0].Format == AnimDataFormat.I6 && proposals[0].IsExact &&
                proposals[0].Curves[0].Times.SequenceEqual(times),
                "Fractional native storage: exact dyadic knots select a genuinely smaller I6 group while I4 remains ineligible");
            string precisePath = Path.Combine(stage, "fractional-reference-i12.chr0"), packedPath = Path.Combine(stage, "fractional-winner-i6.chr0"), savedPath = Path.Combine(stage, "fractional-winner-i6-resaved.chr0");
            original.ExportChecked(precisePath);
            using (var packed = NativeAnimationOptimization.CopySource(original))
            {
                NativeCompactAnimationStorage.ApplyGroup(packed, proposals[0]); packed.ExportChecked(packedPath);
                using (var decoded = (CHR0Node)NodeFactory.FromFile(null, packedPath))
                {
                    var decodedEntry = (CHR0EntryNode)decoded.Children[0];
                    var stored = PreciseCHR0EntryNode.CaptureStorage(decodedEntry);
                    Check(stored != null && stored.Format(1) == AnimDataFormat.I6 && stored[3].Matches(decodedEntry.KeyArrays[3]) &&
                        NativeAnimationOptimization.SourceContentKey(decoded) == originalContent && new FileInfo(packedPath).Length < new FileInfo(precisePath).Length,
                        "Fractional native storage: winning packed file independently reopens with all exact times, values, tangents, metadata and measured smaller bytes");
                    // Frozen literal complete I6 curve: four keys; native
                    // reciprocal 0x3e638e38; base0; step2^-13; signed S10.5
                    // times0,16,36,144; values0,4096,9216,36864; slopes1.
                    const string expectedHex = "000400003E638E383900000000000000000000000100001010000100002424000100009090000100";
                    byte[] expected = Enumerable.Range(0, expectedHex.Length / 2).Select(i => Convert.ToByte(expectedHex.Substring(i * 2, 2), 16)).ToArray();
                    byte* header = (byte*)decodedEntry.WorkingUncompressed.Address;
                    uint relative = (uint)header[8] << 24 | (uint)header[9] << 16 | (uint)header[10] << 8 | header[11];
                    byte* payload = header + relative; byte* owner = (byte*)decoded.WorkingUncompressed.Address;
                    Check(payload >= owner && payload + expected.Length <= owner + decoded.WorkingUncompressed.Length,
                        "Fractional native storage: literal payload comparison stays inside the independently decoded resource");
                    var actual = new byte[expected.Length]; Marshal.Copy((IntPtr)payload, actual, 0, actual.Length);
                    Check(actual.SequenceEqual(expected), "Fractional native storage: every byte of the complete winning I6 header and all four knot records matches the frozen literal expectation");
                    Check(Enumerable.Range(0, 1025).All(i => ReplayFloatBits(decodedEntry.GetFrameValue(3, 5f * i / 1024)) ==
                        ReplayFloatBits(entry.GetFrameValue(3, 5f * i / 1024))),
                        "Fractional native storage: independent reopened I6 and precise original evaluate identically at 1025 native fractional witnesses");
                    using (var resaved = NativeAnimationOptimization.CopySource(decoded)) resaved.ExportChecked(savedPath);
                    Check(File.ReadAllBytes(savedPath).SequenceEqual(File.ReadAllBytes(packedPath)),
                        "Fractional native storage: complete packed CHR0 bytes remain identical after reopen, immutable-plan capture and resave");
                    File.WriteAllText(Path.Combine(stage, "fractional-i6-size.json"), new JavaScriptSerializer().Serialize(new {
                        I12Bytes = new FileInfo(precisePath).Length, I6Bytes = new FileInfo(packedPath).Length,
                        LiteralCurveBytes = actual.Length, LiteralCurveHex = expectedHex, NativeTimes = times, GameExecutionVerified = false }));
                }
            }
            Check(NativeAnimationOptimization.SourceContentKey(original) == originalContent, "Fractional native storage: export preparation leaves the full-quality original unchanged");
            entry.KeyArrays[3].Entries.Skip(1).First()._index = .1f;
            entry.SignalPropertyChange();
            Check(NativeCompactAnimationStorage.CreateAlternatives(entry, 1, true, false, new int[0], 1).Length == 0,
                "Fractional native storage: non-1/32 timing rejects compact I6 and I4 without rounding");
            string offGridPath = Path.Combine(stage, "fractional-non-grid-i12.chr0"); original.ExportChecked(offGridPath);
            using (var offGrid = (CHR0Node)NodeFactory.FromFile(null, offGridPath))
            {
                var offGridEntry = (CHR0EntryNode)offGrid.Children[0];
                byte* nativeEntry = (byte*)offGridEntry.WorkingUncompressed.Address;
                uint RawWord(byte* value) => (uint)value[0] << 24 | (uint)value[1] << 16 | (uint)value[2] << 8 | value[3];
                var nativeCode = (AnimationCode)RawWord(nativeEntry + 4);
                byte* encoded = nativeEntry + unchecked((int)RawWord(nativeEntry + 8));
                var storage = PreciseCHR0EntryNode.CaptureStorage(offGridEntry);
                Check(nativeCode.GetFormat(1) == AnimDataFormat.I12 &&
                    ReplayFloatBits(offGridEntry.KeyArrays[3].Entries.Skip(1).First()._index) == unchecked((int)0x3DCCCCCD) &&
                    RawWord(encoded + 20) == 0x3DCCCCCD && (storage == null || storage.Format(1) == AnimDataFormat.I12),
                    "Fractional native storage: raw I12 time word 0x3DCCCCCD and independent native reopen retain exact non-grid .1f without I4/I6 rounding");
            }
        }
    }

    static void NativeIndexedSingletonEditChecks(string stage)
    {
        string Coefficients(CHR0EntryNode entry) => string.Join("|", entry.KeyArrays.Select(array =>
            string.Join(",", array.Entries.Select(key => ReplayFloatBits(key._index) + ":" +
                ReplayFloatBits(key._value) + ":" + ReplayFloatBits(key._tangent)))));
        void RetainPolicy(CHR0Node clip)
        {
            foreach (var entry in clip.Children.OfType<CHR0EntryNode>().ToArray())
                PreciseCHR0EntryNode.RestoreStorage(entry, PreciseCHR0EntryNode.CaptureStorage(entry));
        }
        foreach (int version in new[] { 4, 5 })
        foreach (bool wasAnimated in new[] { false, true })
        using (var original = new CHR0Node { Name = "native singleton edit", Version = version, FrameCount = 5,
            Loop = true, OriginalPath = "retained original metadata" })
        {
            var entry = original.CreateEntry("edited native member");
            float[] times = { 0, .5f, 1.125f, 4.5f };
            foreach (float time in times)
            {
                entry.Keyframes.SetFrameValue(3, time, time, true)._tangent = 1;
                if (wasAnimated) entry.Keyframes.SetFrameValue(4, time, 2 * time, true)._tangent = 2;
            }
            if (!wasAnimated) entry.Keyframes.SetFrameValue(4, 0, 0, true);
            entry.Keyframes.SetFrameValue(5, 0, 0, true);
            var sibling = original.CreateEntry("untouched native sibling");
            sibling.Keyframes.SetFrameValue(6, 0, .25f, true)._tangent = -.125f;
            sibling.Keyframes.SetFrameValue(6, 4.5f, .75f, true)._tangent = .0625f;
            PreciseCHR0EntryNode.Promote(original);
            entry = (CHR0EntryNode)original.Children[0];
            var proposal = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, true, false, new int[0], 1).Single();
            Check(proposal.Format == AnimDataFormat.I6 && proposal.IsExact,
                "Native singleton edits: both animated and fixed siblings begin with an actual exact fractional I6 group");
            NativeCompactAnimationStorage.ApplyGroup(original, proposal);
            string prefix = Path.Combine(stage, "singleton-v" + version + (wasAnimated ? "-animated" : "-fixed"));
            original.ExportChecked(prefix + "-before.chr0");
            using (var edited = (CHR0Node)NodeFactory.FromFile(null, prefix + "-before.chr0"))
            {
                RetainPolicy(edited);
                var changed = (CHR0EntryNode)edited.Children[0];
                var retained = PreciseCHR0EntryNode.CaptureStorage(changed);
                string oldX = string.Join(",", changed.KeyArrays[3].Entries.Select(key =>
                    ReplayFloatBits(key._index) + ":" + ReplayFloatBits(key._value) + ":" + ReplayFloatBits(key._tangent)));
                string oldSibling = Coefficients((CHR0EntryNode)edited.Children[1]);
                Check(retained != null && retained.MatchesGroup(changed.Keyframes, 1) &&
                    (retained[4] != null) == wasAnimated,
                    "Native singleton edits: native reopen retains precisely the original animated-payload membership");
                foreach (var key in changed.KeyArrays[4].Entries.ToArray()) changed.KeyArrays[4].Remove(key._index);
                changed.SetKeyframe(4, 1.125f, 7.25f, true, true)._tangent = .375f;
                changed.SignalPropertyChange();
                Check(!retained.MatchesGroup(changed.Keyframes, 1),
                    "Native singleton edits: timed nonzero-slope singleton invalidates the entire stale packed group before ordinary Save");
                string expected = NativeAnimationOptimization.SourceContentKey(edited);
                edited.ExportChecked(prefix + "-edited.chr0");
                Check(NativeAnimationOptimization.SourceContentKey(edited) == expected,
                    "Native singleton edits: ordinary native serialization preserves the live edited content exactly");
                using (var reopened = (CHR0Node)NodeFactory.FromFile(null, prefix + "-edited.chr0"))
                {
                    var actual = (CHR0EntryNode)reopened.Children[0];
                    var only = actual.KeyArrays[4].Entries.Single();
                    Check(NativeAnimationOptimization.SourceContentKey(reopened) == expected &&
                        ReplayFloatBits(only._index) == ReplayFloatBits(1.125f) &&
                        ReplayFloatBits(only._value) == ReplayFloatBits(7.25f) &&
                        ReplayFloatBits(only._tangent) == ReplayFloatBits(.375f),
                        "Native singleton edits: ordinary Save/reopen retains exact time, value and tangent rather than replaying old I6 keys");
                    Check(oldX == string.Join(",", actual.KeyArrays[3].Entries.Select(key =>
                        ReplayFloatBits(key._index) + ":" + ReplayFloatBits(key._value) + ":" + ReplayFloatBits(key._tangent))) &&
                        oldSibling == Coefficients((CHR0EntryNode)reopened.Children[1]) &&
                        PreciseCHR0EntryNode.CaptureStorage(actual) == null,
                        "Native singleton edits: group falls back to precise I12 with other axes and sibling animation tracks unchanged");
                    RetainPolicy(reopened); reopened.ExportChecked(prefix + "-resaved.chr0");
                    Check(File.ReadAllBytes(prefix + "-edited.chr0").SequenceEqual(File.ReadAllBytes(prefix + "-resaved.chr0")),
                        "Native singleton edits: complete edited native bytes survive export/reopen/resave without a stale packed payload");
                    using (var resaved = (CHR0Node)NodeFactory.FromFile(null, prefix + "-resaved.chr0"))
                        Check(NativeAnimationOptimization.SourceContentKey(resaved) == expected &&
                            Coefficients((CHR0EntryNode)resaved.Children[1]) == oldSibling,
                            "Native singleton edits: independently reopened second save retains metadata, exact edit and untouched sibling");
                }
                // A canonical fixed axis needs no payload, but a previously
                // animated axis must not replay the retained multi-key data.
                changed.KeyArrays[4].Remove(1.125f); changed.SetKeyframe(4, 0, 9, true, true)._tangent = 0;
                Check(retained.MatchesGroup(changed.Keyframes, 1) == !wasAnimated,
                    "Native singleton edits: canonical fixed axes are eligible only when no stale animated payload can be replayed");
            }
        }
        using (var oneFrame = new CHR0Node { Name = "dense one-frame terminal", FrameCount = 1, Loop = false })
        {
            var entry = oneFrame.CreateEntry("existing native bone");
            entry.Keyframes.SetFrameValue(3, 0, 2, true)._tangent = 0;
            entry.Keyframes.SetFrameValue(4, 0, 0, true); entry.Keyframes.SetFrameValue(5, 0, 0, true);
            var payload = IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1, new[] { 2, 9 }, 1, 0, 1);
            var channels = new IndexedCHR0CurveStorage[9]; channels[3] = payload;
            var retained = new IndexedCHR0EntryStorage(channels);
            entry = PreciseCHR0EntryNode.RestoreStorage(entry, retained);
            Check(retained.MatchesGroup(entry.Keyframes, 1),
                "Native singleton edits: a matching one-frame dense projection retains its independent terminal payload");
            string path = Path.Combine(stage, "singleton-dense-terminal.chr0"); oneFrame.ExportChecked(path);
            using (var reopened = (CHR0Node)NodeFactory.FromFile(null, path))
            {
                var stored = PreciseCHR0EntryNode.CaptureStorage((CHR0EntryNode)reopened.Children[0]);
                Check(stored != null && stored[3].IsDense && stored[3].KeyCount == 1 &&
                    stored[3].EncodedValues.SequenceEqual(new[] { 2f, 9f }),
                    "Native singleton edits: ordinary native Save/reopen preserves both one-frame value and distinct dense terminal sample");
            }
        }
    }
}
