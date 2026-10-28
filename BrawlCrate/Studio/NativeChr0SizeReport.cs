using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BrawlCrate.Studio
{
    // Reads the serialized file, independently of CalculateCHR0Size. Bytes are
    // counted once even when isotropic axes share a stream. This is a standalone
    // resource receipt, never an estimate of PAC/BRRES compression or alignment.
    internal sealed class NativeChr0SizeReport
    {
        public long FileBytes { get; private set; }
        public long DeclaredResourceBytes { get; private set; }
        public long HeaderBytes { get; private set; }
        public long DictionaryBytes { get; private set; }
        public long BoneEntryBytes { get; private set; }
        public long CurveHeaderBytes { get; private set; }
        public long CurvePayloadBytes { get; private set; }
        public long NameBytes { get; private set; }
        public long UserMetadataBytes { get; private set; }
        public long AlignmentBytes { get; private set; }
        public long UnclassifiedBytes { get; private set; }
        public int Version { get; private set; }
        public int Frames { get; private set; }
        public Dictionary<string, int> Streams { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
        public long AccountedBytes => HeaderBytes + DictionaryBytes + BoneEntryBytes + CurveHeaderBytes + CurvePayloadBytes + NameBytes + UserMetadataBytes + AlignmentBytes + UnclassifiedBytes;

        public static NativeChr0SizeReport Read(string path)
        {
            var info = new FileInfo(path);
            if (info.Length < 40 || info.Length > 256L * 1024 * 1024) throw new InvalidDataException("CHR0 accounting requires a bounded standalone file.");
            return new Reader(File.ReadAllBytes(path)).Read();
        }

        public string Describe() => "Physical CHR0 " + FileBytes + " bytes; declared resource " + DeclaredResourceBytes +
            ". Curve payload " + CurvePayloadBytes + ", curve headers " + CurveHeaderBytes + ", bone headers/slots " + BoneEntryBytes +
            ", dictionary " + DictionaryBytes + ", names " + NameBytes + ", user metadata " + UserMetadataBytes + ", header " + HeaderBytes +
            ", proved alignment " + AlignmentBytes + ", unclassified " + UnclassifiedBytes + ". Formats: " + string.Join(", ", Streams.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)) +
            ". Container and outer archive savings were not measured.";

        private sealed class Reader
        {
            readonly byte[] bytes, categories;
            readonly NativeChr0SizeReport report = new NativeChr0SizeReport();
            readonly HashSet<int> strings = new HashSet<int>();
            readonly Dictionary<int, Tuple<int, int>> streams = new Dictionary<int, Tuple<int, int>>();
            long keys;
            int resource;
            internal Reader(byte[] data) { bytes = data; categories = new byte[data.Length]; }
            void Need(long offset, long count, bool insideResource = false)
            {
                long end = insideResource ? resource : bytes.Length;
                if (offset < 0 || count < 0 || offset > end || count > end - offset) throw new InvalidDataException("CHR0 accounting found an out-of-bounds field.");
            }
            int U16(int at) { Need(at, 2); return bytes[at] << 8 | bytes[at + 1]; }
            int I32(int at) { Need(at, 4); return unchecked((int)((uint)bytes[at] << 24 | (uint)bytes[at + 1] << 16 | (uint)bytes[at + 2] << 8 | bytes[at + 3])); }
            int Pointer(int origin, int cell, int count = 1) { long at = (long)origin + I32(cell); Need(at, count); return checked((int)at); }
            void Mark(int at, int count, byte kind)
            {
                Need(at, count);
                for (int i = at; i < at + count; i++)
                {
                    if (categories[i] != 0 && categories[i] != kind) throw new InvalidDataException("CHR0 serialized accounting found overlapping incompatible structures.");
                    categories[i] = kind;
                }
            }
            void Padding(int start, int end)
            {
                Need(start, end - start);
                for (int i = start; i < end; i++)
                    if (bytes[i] != 0 || categories[i] != 0) throw new InvalidDataException("CHR0 alignment contains data or overlaps a structure.");
                Mark(start, end - start, 8);
            }
            void Name(int origin, int cell)
            {
                if (I32(cell) == 0) return;
                int at = Pointer(origin, cell);
                if (!strings.Add(at)) return;
                int end = at;
                while (end < bytes.Length && bytes[end] != 0 && end - at < 1024 * 1024) end++;
                if (end == bytes.Length || end - at >= 1024 * 1024) throw new InvalidDataException("CHR0 name is unterminated or excessive.");
                if (at >= 4 && I32(at - 4) == end - at)
                {
                    Mark(at - 4, 4, 6); Mark(at, end - at + 1, 6);
                    int aligned = checked((end + 4) & ~3);
                    if (aligned <= bytes.Length) Padding(end + 1, aligned);
                }
                else Mark(at, end - at + 1, 6);
            }
            int Dictionary(int at, byte kind)
            {
                Need(at, 24, true); int size = I32(at), count = I32(at + 4);
                if (count < 0 || count > 65535 || size != 24L + 16L * count) throw new InvalidDataException("Invalid CHR0 dictionary size.");
                Need(at, size, true); Mark(at, size, kind);
                for (int i = 0; i <= count; i++)
                {
                    int row = at + 8 + i * 16;
                    if (U16(row + 4) > count || U16(row + 6) > count) throw new InvalidDataException("Invalid CHR0 dictionary tree link.");
                    if (i > 0) Name(at, row + 8);
                }
                return count;
            }
            void Curve(int at, int format)
            {
                int header, payload, count;
                if (format >= 1 && format <= 3)
                {
                    header = format == 3 ? 8 : 16; Need(at, header, true); count = U16(at);
                    if (count == 0) throw new InvalidDataException("CHR0 indexed stream is empty.");
                    payload = checked(count * (format == 1 ? 4 : format == 2 ? 6 : 12));
                }
                else if (format >= 4 && format <= 6)
                {
                    // Existing writer/runtime CV semantics store N+1 samples,
                    // also for non-looping clips. This is not the UI frame count.
                    count = checked(report.Frames + 1); header = format == 6 ? 0 : 8;
                    payload = checked(count * (format == 4 ? 1 : format == 5 ? 2 : 4));
                }
                else throw new InvalidDataException("Unsupported CHR0 stream format in byte accounting.");
                int size = checked(header + payload), aligned = checked((size + 3) & ~3);
                Need(at, aligned, true);
                if (streams.TryGetValue(at, out var previous))
                {
                    if (previous.Item1 != format || previous.Item2 != aligned) throw new InvalidDataException("Shared CHR0 stream has inconsistent format/size.");
                    return;
                }
                streams.Add(at, Tuple.Create(format, aligned)); keys += count;
                if (keys > 2000000) throw new InvalidDataException("CHR0 byte accounting exceeds the stored-key budget.");
                Mark(at, header, 4); Mark(at + header, payload, 5); Padding(at + size, at + aligned);
                string label = format <= 3 ? new[] { "", "I4", "I6", "I12" }[format] : "L" + (format == 4 ? "1" : format == 5 ? "2" : "4");
                report.Streams[label] = report.Streams.TryGetValue(label, out int n) ? n + 1 : 1;
            }
            internal NativeChr0SizeReport Read()
            {
                if (Encoding.ASCII.GetString(bytes, 0, 4) != "CHR0") throw new InvalidDataException("Expected standalone CHR0.");
                report.Version = I32(8); int header = report.Version == 4 ? 40 : report.Version == 5 ? 44 : 0;
                if (header == 0) throw new InvalidDataException("CHR0 byte accounting supports v4/v5 only.");
                resource = I32(4); Need(0, resource); Need(0, header, true); Mark(0, header, 1);
                report.FileBytes = bytes.Length; report.DeclaredResourceBytes = resource;
                int frameCell = report.Version == 4 ? 28 : 32; report.Frames = U16(frameCell);
                if (report.Frames < 1) throw new InvalidDataException("Invalid CHR0 duration.");
                Name(0, report.Version == 4 ? 20 : 24); Name(0, report.Version == 4 ? 24 : 28);
                int group = Pointer(0, 16, 24), count = Dictionary(group, 2);
                if (count != U16(frameCell + 2)) throw new InvalidDataException("CHR0 header/dictionary entry counts disagree.");
                for (int i = 0; i < count; i++)
                {
                    int entry = Pointer(group, group + 24 + i * 16 + 12, 8), slot = entry + 8;
                    Need(entry, 8, true); Name(entry, entry); Mark(entry, 8, 3); uint code = unchecked((uint)I32(entry + 4));
                    for (int g = 0; g < 3; g++)
                    {
                        if ((code & (1u << (22 + g))) == 0) continue;
                        bool isotropic = (code & (1u << (4 + g))) != 0;
                        int format = g == 0 ? (int)(code >> 25) & 3 : g == 1 ? (int)(code >> 27) & 7 : (int)(code >> 30) & 3;
                        for (int axis = 0; axis < (isotropic ? 1 : 3); axis++, slot += 4)
                        {
                            Need(slot, 4, true); Mark(slot, 4, 3);
                            int channel = g * 3 + (isotropic ? 2 : axis);
                            if ((code & (1u << (13 + channel))) == 0) Curve(Pointer(entry, slot), format);
                            else report.Streams["fixed"] = report.Streams.TryGetValue("fixed", out int n) ? n + 1 : 1;
                        }
                    }
                }
                if (report.Version == 5 && I32(20) != 0)
                {
                    int at = Pointer(0, 20, 4), size = I32(at); Need(at, size, true); Mark(at, 4, 7);
                    int userGroup = at + 4, entries = Dictionary(userGroup, 7);
                    for (int i = 0; i < entries; i++)
                    {
                        int entry = Pointer(userGroup, userGroup + 24 + i * 16 + 12, 24), entrySize = I32(entry), values = I32(entry + 8), type = I32(entry + 12);
                        if (entrySize < 24 || values < 0 || values > 2000000) throw new InvalidDataException("Invalid CHR0 user data.");
                        Need(entry, entrySize, true); Mark(entry, 24, 7); Name(entry, entry + 16);
                        if(type == 0 || type == 1) { int data = Pointer(entry, entry + 4, checked(values * 4)); Need(data, checked(values * 4), true); Mark(data, values * 4, 7); }
                        if (type == 2 && values > 0) Name(entry, entry + 4);
                    }
                }
                report.HeaderBytes = categories.LongCount(c => c == 1); report.DictionaryBytes = categories.LongCount(c => c == 2);
                report.BoneEntryBytes = categories.LongCount(c => c == 3); report.CurveHeaderBytes = categories.LongCount(c => c == 4);
                report.CurvePayloadBytes = categories.LongCount(c => c == 5); report.NameBytes = categories.LongCount(c => c == 6);
                report.UserMetadataBytes = categories.LongCount(c => c == 7); report.AlignmentBytes = categories.LongCount(c => c == 8);
                report.UnclassifiedBytes = categories.LongCount(c => c == 0);
                if (report.AccountedBytes != bytes.Length) throw new InvalidDataException("CHR0 byte accounting is incomplete.");
                return report;
            }
        }
    }
}
