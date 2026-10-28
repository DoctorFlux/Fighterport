using BrawlCrate.Studio;
using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

internal static partial class Program
{
    // Independent physical-byte receipts use synthetic native writes. Expected
    // payloads follow the format widths, not the writer's size calculation.
    static void NativeChr0SizeAccountingChecks(string stage)
    {
        Directory.CreateDirectory(stage);
        byte[] canonical = null;
        foreach (int version in new[] { 4, 5 })
            foreach (var format in new[] { AnimDataFormat.I12, AnimDataFormat.I4, AnimDataFormat.I6 })
                using (var clip = CompactFixture(12, 3, new[] { 0, 4, 8 }, i => i * .125f, i => .125f))
                {
                    clip.Version = version;
                    if (format != AnimDataFormat.I12)
                    {
                        var proposal = NativeCompactAnimationStorage.CreateAlternatives((CHR0EntryNode)clip.Children[0], 1,
                            true, false, new int[0], 1).Single(p => p.Format == format);
                        NativeCompactAnimationStorage.ApplyGroup(clip, proposal);
                    }
                    string path = Path.Combine(stage, "size-v" + version + "-" + format + ".chr0");
                    clip.ExportChecked(path);
                    byte[] data = File.ReadAllBytes(path); var report = NativeChr0SizeReport.Read(path);
                    int width = format == AnimDataFormat.I4 ? 4 : format == AnimDataFormat.I6 ? 6 : 12;
                    int header = format == AnimDataFormat.I12 ? 8 : 16;
                    Check(report.Version == version && report.Frames == 12 && report.FileBytes == data.Length &&
                        report.DeclaredResourceBytes == SizeReceiptRead32(data, 4) && report.AccountedBytes == data.Length &&
                        report.HeaderBytes == (version == 4 ? 40 : 44),
                        "Byte receipt: v" + version + " " + format + " physical total, declared block and version header independently match native bytes");
                    Check(report.CurvePayloadBytes == width * 3 && report.CurveHeaderBytes == header &&
                        report.Streams.TryGetValue(format.ToString(), out int streams) && streams == 1 &&
                        report.Streams.TryGetValue("fixed", out int fixedSlots) && fixedSlots == 2 &&
                        report.BoneEntryBytes == 20 && report.DictionaryBytes == 40,
                        "Byte receipt: v" + version + " " + format + " counts each three-knot stream and its two fixed rotation slots exactly once");
                    Check(report.NameBytes > 0 && report.FileBytes > report.DeclaredResourceBytes && report.UserMetadataBytes == 0 &&
                        report.Describe().Contains("Container and outer archive savings were not measured"),
                        "Byte receipt: standalone names remain distinct from the CHR0 resource block and no container savings are invented");
                    if (version == 4 && format == AnimDataFormat.I12) canonical = data;
                }

        using (var fixedClip = CompactFixture(12, 3, new[] { 0 }, i => 23.5f, i => 0))
        {
            string path = Path.Combine(stage, "size-fixed.chr0"); fixedClip.ExportChecked(path);
            var report = NativeChr0SizeReport.Read(path);
            Check(report.CurveHeaderBytes == 0 && report.CurvePayloadBytes == 0 && report.Streams.Count == 1 &&
                report.Streams["fixed"] == 3 && report.BoneEntryBytes == 20,
                "Byte receipt: constant rotation stores three inline fixed slots and no indexed stream");
        }

        using (var iso = new CHR0Node { Name = "Independent isotropic scale", Version = 4, FrameCount = 12, Loop = true })
        {
            var entry = iso.CreateEntry("Independent scale target");
            for (int ch = 0; ch < 3; ch++)
                for (int k = 0; k < 3; k++)
                    entry.Keyframes.SetFrameValue(ch, k * 4, 1 + k * .125f, true)._tangent = .125f;
            entry.Keyframes.PreserveInterpolation = true; PreciseCHR0EntryNode.Promote(iso);
            string path = Path.Combine(stage, "size-isotropic.chr0"); iso.ExportChecked(path);
            var report = NativeChr0SizeReport.Read(path); byte[] data = File.ReadAllBytes(path);
            int group = SizeReceiptRead32(data, 16), bone = group + SizeReceiptRead32(data, group + 36);
            uint code = unchecked((uint)SizeReceiptRead32(data, bone + 4));
            Check((code & (1u << 4)) != 0 && report.CurvePayloadBytes == 36 && report.CurveHeaderBytes == 8 &&
                report.Streams["I12"] == 1 && report.BoneEntryBytes == 12,
                "Byte receipt: isotropic scale has one physical stream and slot rather than charging the same XYZ curve three times");
        }

        using (var metadata = CompactFixture(12, 3, new[] { 0, 4, 8 }, i => i * .125f, i => .125f))
        {
            metadata.Version = 5;
            metadata.UserEntries.Add(new UserDataClass { Name = "integer values", DataType = UserValueType.Int, Entries = new[] { "-7", "0", "42" } });
            metadata.UserEntries.Add(new UserDataClass { Name = "floating values", DataType = UserValueType.Float,
                Entries = new[] { .125f, -2.5f }.Select(v => v.ToString("R", CultureInfo.CurrentCulture)).ToArray() });
            metadata.UserEntries.Add(new UserDataClass { Name = "text value", DataType = UserValueType.String, Entries = new[] { "Independent Unicode metadata é" } });
            string path = Path.Combine(stage, "size-userdata-v5.chr0"); metadata.ExportChecked(path);
            var report = NativeChr0SizeReport.Read(path); byte[] data = File.ReadAllBytes(path);
            // User header 4 + dictionary (24 + 3*16) + three 24-byte
            // entry headers + 3 int/2 float values. Names count separately.
            Check(report.UserMetadataBytes == 4 + 72 + 72 + 20 && report.NameBytes > 0 &&
                report.CurvePayloadBytes == 36 && report.AccountedBytes == data.Length,
                "Byte receipt: v5 user dictionaries, entry headers and numeric values are separated from UTF-8 names and curve payloads");
            int userGroup = SizeReceiptRead32(data, 20) + 4;
            int userEntry = userGroup + SizeReceiptRead32(data, userGroup + 36);
            SizeReceiptReject(stage, "userdata-count", data, b => SizeReceiptWrite32(b, userEntry + 8, -1));
            SizeReceiptReject(stage, "userdata-size", data, b => SizeReceiptWrite32(b, userEntry, 23));
            SizeReceiptReject(stage, "userdata-pointer", data, b => SizeReceiptWrite32(b, userEntry + 4, int.MaxValue));
        }

        foreach (bool signedZero in new[] { false, true })
            using (var clip = new CHR0Node { Name = "Independent distinct scale coefficients", Version = 4, FrameCount = 12 })
            {
                var entry = clip.CreateEntry("Independent scale directions");
                for (int ch = 0; ch < 3; ch++) for (int key = 0; key < 3; key++)
                    entry.Keyframes.SetFrameValue(ch, key * 4, 1 + key * .125f, true)._tangent = signedZero ? 0 : .125f;
                entry.KeyArrays[1].Entries.First()._tangent = signedZero ? NegativeZero() : .25f;
                entry.Keyframes.PreserveInterpolation = true; PreciseCHR0EntryNode.Promote(clip);
                string path = Path.Combine(stage, signedZero ? "scale-signed-zero.chr0" : "scale-distinct-tangents.chr0"); clip.ExportChecked(path);
                var report = NativeChr0SizeReport.Read(path);
                using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
                {
                    var actual = (CHR0EntryNode)decoded.Children.Single();
                    Check(report.Streams["I12"] == 3 && report.CurvePayloadBytes == 108 && report.CurveHeaderBytes == 24 && report.BoneEntryBytes == 20 &&
                        Enumerable.Range(0, 3).All(ch => actual.KeyArrays[ch].Entries.Select(k => ReplayFloatBits(k._tangent)).SequenceEqual(
                            ((CHR0EntryNode)clip.Children.Single()).KeyArrays[ch].Entries.Select(k => ReplayFloatBits(k._tangent)))),
                        signedZero ? "Byte receipt: signed-zero scale tangent distinctions prevent isotropic sharing and survive exact decode" :
                        "Byte receipt: identical scale times and values with distinct tangents own three streams and retain every slope");
                }
            }

        using (var clip = CompactFixture(6, 3, new[] { 0 }, i => 4.125f, i => 0))
        {
            var entry = (CHR0EntryNode)clip.Children.Single(); entry.KeyArrays[3].Remove(0);
            entry.Keyframes.SetFrameValue(3, 1.25f, 4.125f, true)._tangent = -.75f;
            string path = Path.Combine(stage, "single-noncanonical-fractional-key.chr0"); clip.ExportChecked(path);
            var report = NativeChr0SizeReport.Read(path);
            using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
            {
                var keys = ((CHR0EntryNode)decoded.Children.Single()).KeyArrays[3].Entries.ToArray();
                Check(report.Streams.TryGetValue("I12", out int n) && n == 1 && report.CurvePayloadBytes == 12 && report.CurveHeaderBytes == 8 &&
                    keys.Length == 1 && ReplayFloatBits(keys[0]._index) == ReplayFloatBits(1.25f) &&
                    ReplayFloatBits(keys[0]._value) == ReplayFloatBits(4.125f) && ReplayFloatBits(keys[0]._tangent) == ReplayFloatBits(-.75f),
                    "Byte receipt: a single noncanonical fractional knot retains its indexed time and tangent instead of silently becoming a fixed slot");
            }
        }

        using (var clip = new CHR0Node { Name = "Independent isotropic timed singleton", Version = 4, FrameCount = 6 })
        {
            var entry = clip.CreateEntry("Independent shared scale");
            for (int c = 0; c < 3; c++) entry.Keyframes.SetFrameValue(c, 1.125f, 1.25f, true)._tangent = .375f;
            entry.Keyframes.PreserveInterpolation = true; PreciseCHR0EntryNode.Promote(clip);
            string path = Path.Combine(stage, "isotropic-timed-singleton.chr0"); clip.ExportChecked(path);
            var report = NativeChr0SizeReport.Read(path);
            using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
            {
                var actual = (CHR0EntryNode)decoded.Children.Single();
                Check(report.Streams.TryGetValue("I12", out int streams) && streams == 1 && report.CurveHeaderBytes == 8 && report.CurvePayloadBytes == 12 &&
                    report.BoneEntryBytes == 12 && Enumerable.Range(0, 3).All(c => actual.KeyArrays[c].KeyCount == 1 &&
                        actual.KeyArrays[c].Entries.All(k => ReplayFloatBits(k._index) == ReplayFloatBits(1.125f) &&
                            ReplayFloatBits(k._value) == ReplayFloatBits(1.25f) && ReplayFloatBits(k._tangent) == ReplayFloatBits(.375f))),
                    "Byte receipt: isotropic timed/slope singleton emits one 20-byte I12 stream and freshly retains all three axis tuples");
            }
        }

        foreach (bool changeFlags in new[] { false, true })
            using (var clip = CompactFixture(12, 3, new[] { 0, 4, 8 }, i => i * .125f, i => .125f))
            {
                var entry = (PreciseCHR0EntryNode)clip.Children.Single();
                int size = entry.OnCalculateSize(true);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                int entryBytes = (int)typeof(CHR0EntryNode).GetField("_entryLen", flags).GetValue(entry);
                var dataAddress = typeof(CHR0EntryNode).GetField("_dataAddr", flags);
                IntPtr memory = Marshal.AllocHGlobal(size + 64);
                byte[] sentinel = Enumerable.Repeat((byte)0xA5, size + 64).ToArray();
                try
                {
                    Marshal.Copy(sentinel, 0, memory, sentinel.Length);
                    // Supply a bounded valid layout so even an incorrect guard
                    // writes a detectable buffer rather than dereferencing null.
                    dataAddress.SetValue(entry, (BrawlLib.Internal.VoidPtr)IntPtr.Add(memory, entryBytes));
                    if (changeFlags) entry.UseModelRotate = !entry.UseModelRotate;
                    else entry.KeyArrays[3].Entries.Skip(1).First()._tangent += .125f;
                    bool stale = false;
                    try { entry.OnRebuild(memory, size, true); } catch (InvalidOperationException) { stale = true; }
                    var actual = new byte[sentinel.Length]; Marshal.Copy(memory, actual, 0, actual.Length);
                    Check(stale && actual.SequenceEqual(sentinel), changeFlags ?
                        "Precise write plan: changed semantic flags reject before touching any planned output byte" :
                        "Precise write plan: changed curve coefficients reject before touching any planned output byte");
                }
                finally { dataAddress.SetValue(entry, (BrawlLib.Internal.VoidPtr)IntPtr.Zero); Marshal.FreeHGlobal(memory); }
            }

        int mainGroup = SizeReceiptRead32(canonical, 16), mainEntry = mainGroup + SizeReceiptRead32(canonical, mainGroup + 36);
        int stream = mainEntry + SizeReceiptRead32(canonical, mainEntry + 8);
        SizeReceiptReject(stage, "magic", canonical, b => b[0] = (byte)'X');
        SizeReceiptReject(stage, "version", canonical, b => SizeReceiptWrite32(b, 8, 6));
        SizeReceiptReject(stage, "resource-bound", canonical, b => SizeReceiptWrite32(b, 4, b.Length + 1));
        SizeReceiptReject(stage, "dictionary-size", canonical, b => SizeReceiptWrite32(b, mainGroup, 39));
        SizeReceiptReject(stage, "dictionary-link", canonical, b => { b[mainGroup + 12] = 0x7F; b[mainGroup + 13] = 0xFF; });
        SizeReceiptReject(stage, "entry-count", canonical, b => { b[30] = 0; b[31] = 2; });
        SizeReceiptReject(stage, "stream-pointer", canonical, b => SizeReceiptWrite32(b, mainEntry + 8, int.MaxValue));
        SizeReceiptReject(stage, "stream-count-zero", canonical, b => { b[stream] = 0; b[stream + 1] = 0; });
        SizeReceiptReject(stage, "stream-count-bound", canonical, b => { b[stream] = 0xFF; b[stream + 1] = 0xFF; });
        string truncated = Path.Combine(stage, "reject-truncated.chr0"); File.WriteAllBytes(truncated, canonical.Take(39).ToArray());
        bool refused = false; try { NativeChr0SizeReport.Read(truncated); } catch (InvalidDataException) { refused = true; }
        Check(refused, "Byte receipt: bounded standalone input rejects truncated headers");
        NativeChr0UnalignedDenseAccountingChecks(stage, canonical);
    }

    static void NativeChr0RetainedDenseAccountingChecks(string sourceFile)
    {
        byte[] bytes = File.ReadAllBytes(sourceFile);
        var dense = DenseReadCompletePayloads(bytes).GroupBy(item => item.Offset).Select(group => group.First()).ToArray();
        var report = NativeChr0SizeReport.Read(sourceFile);
        Check(dense.Length > 0 && report.Streams.Where(item => item.Key == "L1" || item.Key == "L2" || item.Key == "L4").Sum(item => item.Value) == dense.Length &&
            report.CurvePayloadBytes >= dense.Sum(item => item.EncodedValues.Length * (item.Format == 4 ? 1 : item.Format == 5 ? 2 : 4)) &&
            report.FileBytes == bytes.Length && report.AccountedBytes == bytes.Length && File.ReadAllBytes(sourceFile).SequenceEqual(bytes),
            "Byte receipt: authentic retained dense streams use every independently read N+1 sample without requiring writer-specific padding or rewriting source bytes");
    }

    static void NativeChr0UnalignedDenseAccountingChecks(string stage, byte[] template)
    {
        foreach (bool zeroNextHeader in new[] { false, true })
        {
            byte[] bytes = SizeReceiptUnalignedDense(template, zeroNextHeader);
            string path = Path.Combine(stage, zeroNextHeader ? "dense-tight-zero-next-header.chr0" : "dense-tight-next-header.chr0");
            File.WriteAllBytes(path, bytes); var report = NativeChr0SizeReport.Read(path);
            int dictionary = SizeReceiptRead32(bytes, 16), entry = dictionary + SizeReceiptRead32(bytes, dictionary + 36);
            int first = entry + SizeReceiptRead32(bytes, entry + 8), second = entry + SizeReceiptRead32(bytes, entry + 12);
            Check(second == first + 13 && SizeReceiptRead32(bytes, 4) == second + 13 &&
                report.CurvePayloadBytes == 10 && report.CurveHeaderBytes == 16 && report.Streams["L1"] == 2 &&
                report.BoneEntryBytes == 20 && report.AccountedBytes == bytes.Length,
                "Byte receipt: adjacent unpadded L1 streams retain their exact 13-byte extents, including an unaligned final resource boundary and " + (zeroNextHeader ? "zero" : "nonzero") + " next header");
            var physical = DenseReadCompletePayloads(bytes);
            using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
            {
                var bone = (CHR0EntryNode)decoded.Children.Single();
                Check(decoded.FrameCount == 4 && physical.Length == 2 && physical.All(item => item.EncodedValues.Length == 5) &&
                    physical.All(item => Enumerable.Range(0, 4).All(frame => ReplayFloatBits(bone.GetFrameValue(item.Channel, frame)) == ReplayFloatBits(item.EncodedValues[frame]))),
                    "Byte receipt: the independent complete-payload reader and fresh native decoder agree on all visible tight dense samples; both terminal values remain present");
            }
            SizeReceiptReject(stage, zeroNextHeader ? "dense-tight-zero-truncated-terminal" : "dense-tight-truncated-terminal", bytes,
                bad => SizeReceiptWrite32(bad, 4, second + 12));
            SizeReceiptReject(stage, zeroNextHeader ? "dense-tight-zero-truncated-header" : "dense-tight-truncated-header", bytes,
                bad => SizeReceiptWrite32(bad, entry + 12, SizeReceiptRead32(bad, 4) - entry - 7));
            // Point an entry slot into another curve's required payload. The
            // actual header/payload overlap must still reject; deferred padding
            // never grants permission for incompatible structures to overlap.
            SizeReceiptReject(stage, zeroNextHeader ? "dense-tight-zero-overlap" : "dense-tight-overlap", bytes,
                bad => SizeReceiptWrite32(bad, entry + 12, first + 8 - entry));
        }

        // Keep nonzero unreferenced bytes explicit. They are not proved padding
        // and must not be swallowed merely because the next four-byte boundary
        // would contain them.
        byte[] unknown = SizeReceiptUnalignedDense(template, false, 2);
        int group = SizeReceiptRead32(unknown, 16), boneEntry = group + SizeReceiptRead32(unknown, group + 36);
        int end = boneEntry + SizeReceiptRead32(unknown, boneEntry + 12) + 13;
        unknown[end] = 0xA5; unknown[end + 1] = 0x5A;
        string unknownPath = Path.Combine(stage, "dense-tight-unknown-not-padding.chr0"); File.WriteAllBytes(unknownPath, unknown);
        var unknownReport = NativeChr0SizeReport.Read(unknownPath);
        Check(unknownReport.UnclassifiedBytes >= 2 && unknownReport.CurvePayloadBytes == 10 &&
            unknownReport.AccountedBytes == unknown.Length && File.ReadAllBytes(unknownPath).SequenceEqual(unknown),
            "Byte receipt: nonzero bytes following a tight dense payload remain explicit unknown data, never alignment or silently removable waste");
    }

    static byte[] SizeReceiptUnalignedDense(byte[] template, bool zeroNextHeader, int extra = 0)
    {
        int group = SizeReceiptRead32(template, 16), entry = group + SizeReceiptRead32(template, group + 36);
        int data = entry + SizeReceiptRead32(template, entry + 8), oldResource = SizeReceiptRead32(template, 4);
        int newResource = data + 26 + extra, delta = newResource - oldResource;
        byte[] bytes = new byte[template.Length + delta];
        Array.Copy(template, bytes, data); Array.Copy(template, oldResource, bytes, newResource, template.Length - oldResource);
        SizeReceiptWrite32(bytes, 4, newResource); bytes[28] = 0; bytes[29] = 4;
        uint code = unchecked((uint)SizeReceiptRead32(bytes, entry + 4));
        code = (code & ~(7u << 27) & ~(1u << 17)) | (4u << 27);
        SizeReceiptWrite32(bytes, entry + 4, unchecked((int)code));
        SizeReceiptWrite32(bytes, entry + 8, data - entry); SizeReceiptWrite32(bytes, entry + 12, data + 13 - entry);
        foreach (var pointer in new[] { new[] { 0, 20 }, new[] { 0, 24 }, new[] { group, group + 32 }, new[] { entry, entry } })
            if (SizeReceiptRead32(template, pointer[1]) != 0) SizeReceiptWrite32(bytes, pointer[1], SizeReceiptRead32(template, pointer[1]) + delta);
        for (int axis = 0; axis < 2; axis++)
        {
            int at = data + axis * 13;
            float step = axis == 0 ? .5f : zeroNextHeader ? 0 : .25f, basis = axis == 0 ? -1 : 2;
            SizeReceiptWrite32(bytes, at, ReplayFloatBits(step)); SizeReceiptWrite32(bytes, at + 4, ReplayFloatBits(basis));
            for (int sample = 0; sample < 5; sample++) bytes[at + 8 + sample] = (byte)sample;
        }
        return bytes;
    }

    static int SizeReceiptRead32(byte[] data, int at) => unchecked((int)((uint)data[at] << 24 | (uint)data[at + 1] << 16 |
        (uint)data[at + 2] << 8 | data[at + 3]));
    static void SizeReceiptWrite32(byte[] data, int at, int value)
    {
        data[at] = (byte)(value >> 24); data[at + 1] = (byte)(value >> 16); data[at + 2] = (byte)(value >> 8); data[at + 3] = (byte)value;
    }
    static void SizeReceiptReject(string stage, string label, byte[] original, Action<byte[]> mutate)
    {
        byte[] data = (byte[])original.Clone(); mutate(data); string path = Path.Combine(stage, "reject-" + label + ".chr0");
        File.WriteAllBytes(path, data); bool refused = false;
        try { NativeChr0SizeReport.Read(path); } catch (InvalidDataException) { refused = true; }
        Check(refused && File.ReadAllBytes(path).SequenceEqual(data), "Byte receipt: corrupted " + label + " is rejected without rewriting source bytes");
    }
}
