using BrawlCrate.Studio;
using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

internal static partial class Program
{
    // Fixtures patch serialized native fields, not the optimizer's interpreted
    // properties. Unsupported originals must survive even if an upstream writer
    // silently normalizes its replacement buffer before optimization is opened.
    static void NativeOptimizationMetadataChecks(string bone, string stage)
    {
        string folder = Path.Combine(stage, "metadata"); Directory.CreateDirectory(folder);
        byte[] v4 = MetadataCanonicalFixture(folder, bone, 4), v5 = MetadataCanonicalFixture(folder, bone, 5);
        foreach (int version in new[] { 4, 5 })
        {
            var canonical = version == 4 ? v4 : v5;
            string path = Path.Combine(folder, "canonical-v" + version + ".chr0");
            using (var source = NodeFactory.FromFile(null, path) as CHR0Node)
            using (var copy = NativeAnimationOptimization.Copy(source))
            {
                string content = NativeAnimationOptimization.ContentKey(source);
                string saved = Path.Combine(folder, "canonical-v" + version + "-copy.chr0"); copy.ExportChecked(saved);
                using (var decoded = NodeFactory.FromFile(null, saved) as CHR0Node)
                    Check(decoded != null && NativeAnimationOptimization.ContentKey(copy) == content && NativeAnimationOptimization.ContentKey(decoded) == content &&
                        (version == 4 || decoded.UserEntries.Count == 3 && decoded.UserEntries[0].DataType == UserValueType.Int &&
                            decoded.UserEntries[1].DataType == UserValueType.Float && decoded.UserEntries[2].DataType == UserValueType.String && decoded.UserEntries[2].Entries.Length == 1),
                        "Native metadata: canonical v" + version + " header and supported Int/Float/single-String metadata copy and freshly reopen exactly");
            }
            int scaling = version == 4 ? 0x24 : 0x28, loop = version == 4 ? 0x20 : 0x24, originalPath = version == 4 ? 0x18 : 0x1C;
            MetadataFault(folder, "v" + version + "-scaling", canonical, data => MetadataWrite32(data, scaling, 1));
            MetadataFault(folder, "v" + version + "-loop", canonical, data => MetadataWrite32(data, loop, version == 4 ? 2 : -1));
            MetadataFault(folder, "v" + version + "-negative-path", canonical, data => MetadataWrite32(data, originalPath, -4));

            string opaquePath = Path.Combine(folder, "v" + version + "-scaling.chr0");
            string originalHash = FighterProject.HashFile(opaquePath);
            using (var source = NodeFactory.FromFile(null, opaquePath) as CHR0Node)
            {
                source.SignalPropertyChange(); string rewritten = Path.Combine(folder, "v" + version + "-upstream-working.chr0");
                // This intentionally calls the inherited native writer directly.
                // Optimizer Copy/ContentKey never receive permission to do this.
                source.Export(rewritten);
                byte[] original = MetadataRaw(source.UncompressedSource), working = MetadataRaw(source.WorkingUncompressed);
                Check(MetadataRead32(original, scaling) == 1 && MetadataRead32(working, scaling) == 0 && MetadataRead32(File.ReadAllBytes(rewritten), scaling) == 0,
                    "Native metadata: upstream v" + version + " rebuilding proves Working normalization differs from retained original scaling bytes");
                Check(MetadataUnsupported(() => { using (var copy = NativeAnimationOptimization.Copy(source)) { } }) &&
                    MetadataUnsupported(() => NativeAnimationOptimization.ContentKey(source)) && original.SequenceEqual(MetadataRaw(source.UncompressedSource)) &&
                    working.SequenceEqual(MetadataRaw(source.WorkingUncompressed)) && FighterProject.HashFile(opaquePath) == originalHash,
                    "Native metadata: v" + version + " original opaque scaling remains blocked after its Working header has normalized");
            }
        }

        int group = MetadataRead32(v5, 0x14) + 4;
        int intEntry = MetadataUserEntry(v5, 0), floatEntry = MetadataUserEntry(v5, 1), stringEntry = MetadataUserEntry(v5, 2);
        MetadataFault(folder, "v5-unknown-type", v5, data => MetadataWrite32(data, intEntry + 0xC, 7));
        MetadataFault(folder, "v5-empty-string-count", v5, data => MetadataWrite32(data, stringEntry + 8, 0));
        MetadataFault(folder, "v5-multiple-string-count", v5, data => MetadataWrite32(data, stringEntry + 8, 2));
        MetadataFault(folder, "v5-nonordinal-id", v5, data => MetadataWrite32(data, floatEntry + 0x14, 7));
        MetadataFault(folder, "v5-duplicate-names", v5, data =>
        {
            int firstRow = group + 0x18, secondRow = firstRow + 0x10;
            int name = group + MetadataRead32(data, firstRow + 8);
            MetadataWrite32(data, secondRow + 8, name - group); MetadataWrite32(data, floatEntry + 0x10, name - floatEntry);
        });
        MetadataFault(folder, "v5-invalid-name-utf8", v5, data => data[group + MetadataRead32(data, group + 0x18 + 8)] = 0xFF);
        MetadataFault(folder, "v5-invalid-value-utf8", v5, data => data[stringEntry + MetadataRead32(data, stringEntry + 4)] = 0xFF);
        int floatData = floatEntry + MetadataRead32(v5, floatEntry + 4);
        foreach (var raw in new[] { new { Name = "negative-zero", Bits = unchecked((int)0x80000000) },
            new { Name = "positive-infinity", Bits = 0x7F800000 }, new { Name = "negative-infinity", Bits = unchecked((int)0xFF800000) },
            new { Name = "nan-payload", Bits = 0x7FC00001 } })
            MetadataFault(folder, "v5-float-" + raw.Name, v5, data => MetadataWrite32(data, floatData, raw.Bits));

        // Native userdata caches the legacy reader's localized numeric strings.
        // A changed writer culture can turn the same finite raw float into 125.
        var culture = Thread.CurrentThread.CurrentCulture;
        string culturePath = Path.Combine(folder, "v5-float-culture-loss.chr0"); File.WriteAllBytes(culturePath, v5);
        string cultureHash = FighterProject.HashFile(culturePath);
        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("da-DK");
            using (var source = NodeFactory.FromFile(null, culturePath) as CHR0Node)
            {
                string cached = source.UserEntries[1].Entries[0];
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                float parsed; bool loss = float.TryParse(cached, out parsed) && BitConverter.ToInt32(BitConverter.GetBytes(parsed), 0) != MetadataRead32(v5, floatData);
                Check(loss && MetadataUnsupported(() => { using (var copy = NativeAnimationOptimization.Copy(source)) { } }) &&
                    MetadataUnsupported(() => NativeAnimationOptimization.ContentKey(source)) && FighterProject.HashFile(culturePath) == cultureHash,
                    "Native metadata: finite cached float text with proven native-writer precision loss is rejected instead of rewriting original bits");
            }
        }
        finally { Thread.CurrentThread.CurrentCulture = culture; }
    }

    static byte[] MetadataCanonicalFixture(string folder, string bone, int version)
    {
        string path = Path.Combine(folder, "canonical-v" + version + ".chr0");
        using (var clip = new CHR0Node { Name = "Independent native metadata v" + version, Version = version, FrameCount = 5, OriginalPath = "procedural/native-metadata" })
        {
            OptimizationFixed(clip, bone, new float[] { 1, 1, 1, 0, 0, 0, 0, 0, 0 });
            if (version == 5)
            {
                clip.UserEntries.Add(new UserDataClass { Name = "meta-int", DataType = UserValueType.Int, Entries = new[] { "-7", "0", "42" } });
                clip.UserEntries.Add(new UserDataClass { Name = "meta-float", DataType = UserValueType.Float,
                    Entries = new[] { .125f, -2.5f, 1.5f, float.MaxValue, float.Epsilon }.Select(value => value.ToString("R", CultureInfo.CurrentCulture)).ToArray() });
                clip.UserEntries.Add(new UserDataClass { Name = "meta-text", DataType = UserValueType.String, Entries = new[] { "One original UTF-8 string" } });
            }
            clip.Export(path);
        }
        return File.ReadAllBytes(path);
    }
    static void MetadataFault(string folder, string label, byte[] canonical, Action<byte[]> patch)
    {
        byte[] raw = (byte[])canonical.Clone(); patch(raw);
        string path = Path.Combine(folder, label + ".chr0"); File.WriteAllBytes(path, raw);
        string hash = FighterProject.HashFile(path);
        using (var source = NodeFactory.FromFile(null, path) as CHR0Node)
        {
            byte[] original = MetadataRaw(source.UncompressedSource), working = MetadataRaw(source.WorkingUncompressed);
            string keys = MetadataKeys(source); bool dirty = source.IsDirty;
            bool copy = MetadataUnsupported(() => { using (var clone = NativeAnimationOptimization.Copy(source)) { } });
            bool content = MetadataUnsupported(() => NativeAnimationOptimization.ContentKey(source));
            Check(copy && content && source.IsDirty == dirty && keys == MetadataKeys(source) && original.SequenceEqual(MetadataRaw(source.UncompressedSource)) &&
                working.SequenceEqual(MetadataRaw(source.WorkingUncompressed)) && FighterProject.HashFile(path) == hash,
                "Native metadata: opaque " + label + " rejects Copy/content review before writing while retaining raw bytes and native keys");
        }
    }
    static bool MetadataUnsupported(Action action)
    {
        try { action(); return false; }
        catch (NotSupportedException error) { return error.Message.StartsWith("Optimization cannot preserve this native CHR0 metadata:", StringComparison.Ordinal) && !error.Data.Contains("OptimizationQuality") && !error.Data.Contains("OptimizationSize"); }
    }
    static byte[] MetadataRaw(DataSource source)
    { byte[] bytes = new byte[source.Length]; if (bytes.Length > 0) Marshal.Copy(source.Address, bytes, 0, bytes.Length); return bytes; }
    static string MetadataKeys(CHR0Node clip)
    {
        var values = new List<string>();
        foreach (var entry in clip.Children.OfType<CHR0EntryNode>())
        {
            values.Add(entry.Name);
            for (int channel = 0; channel < 9; channel++)
            {
                values.Add("channel " + channel);
                foreach (var key in entry.KeyArrays[channel].Entries)
                    values.Add(key._index + ":" + BitConverter.ToInt32(BitConverter.GetBytes(key._value), 0) + ":" + BitConverter.ToInt32(BitConverter.GetBytes(key._tangent), 0));
            }
        }
        return string.Join("|", values);
    }
    static int MetadataUserEntry(byte[] bytes, int index)
    { int group = MetadataRead32(bytes, 0x14) + 4; return group + MetadataRead32(bytes, group + 0x18 + index * 0x10 + 12); }
    static int MetadataRead32(byte[] bytes, int offset) => unchecked((int)(((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3]));
    static void MetadataWrite32(byte[] bytes, int offset, int value)
    { bytes[offset] = (byte)(value >> 24); bytes[offset + 1] = (byte)(value >> 16); bytes[offset + 2] = (byte)(value >> 8); bytes[offset + 3] = (byte)value; }
}
