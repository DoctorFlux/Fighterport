using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BrawlCrate.Studio
{
    internal static partial class NativeAnimationOptimization
    {
        private static readonly UTF8Encoding MetadataUtf8 = new UTF8Encoding(false, true);

        // Qualification boundary only: retaining an opaque mode in the native
        // reader/writer does not qualify its target transform semantics. Never
        // rebuild the source to discover its metadata.
        public static unsafe void ValidateMetadata(CHR0Node source)
            => ValidateMetadataPolicy(source, false);

        // A donor snapshot preserves the native scaling rule as opaque source
        // metadata. This does not broaden target optimization qualification.
        private static unsafe void ValidateMetadataPolicy(CHR0Node source, bool sourceSnapshot)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Version != 4 && source.Version != 5) throw UnsupportedMetadata("version must be 4 or 5");
            if (source is NativeSourceSnapshotNode snapshot && (sourceSnapshot
                ? snapshot.ScalingRule < 0 || snapshot.ScalingRule > 2 : snapshot.ScalingRule != 0))
                throw UnsupportedMetadata(sourceSnapshot ? "unsupported managed donor scaling rule" : "nonzero scaling rule");
            ValidateMemoryMetadata(source);
            var original = source.UncompressedSource; var working = source.WorkingUncompressed;
            if (original.Address != null) ValidateRawMetadata(source, original, false, sourceSnapshot);
            if (working.Address != null && (working.Address != original.Address || working.Length != original.Length))
                ValidateRawMetadata(source, working, source.IsBranch, sourceSnapshot);
        }

        internal static unsafe int SourceScalingRule(CHR0Node source)
        {
            ValidateMetadataPolicy(source, true);
            if (source is NativeSourceSnapshotNode snapshot) return snapshot.ScalingRule;
            var data = source.UncompressedSource.Address != null ? source.UncompressedSource : source.WorkingUncompressed;
            if (data.Address == null) return 0;
            byte* start = (byte*)data.Address;
            int rawVersion = (int)((uint)start[8] << 24 | (uint)start[9] << 16 | (uint)start[10] << 8 | start[11]);
            int offset = rawVersion == 4 ? 0x24 : 0x28;
            // ValidateMetadataPolicy already established bounded ownership and
            // supported headers for both original and working memory.
            return (int)((uint)start[offset] << 24 | (uint)start[offset + 1] << 16 | (uint)start[offset + 2] << 8 | start[offset + 3]);
        }

        private static NotSupportedException UnsupportedMetadata(string detail) => new NotSupportedException(
            "Optimization cannot preserve this native CHR0 metadata: " + detail + ". The current animation is unchanged.");

        private static void ValidateMemoryMetadata(CHR0Node source)
        {
            if (source.Version == 4 && source.UserEntries.Count != 0) throw UnsupportedMetadata("v4 user data");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (UserDataClass user in source.UserEntries)
            {
                ValidateMetadataText(user.Name, "user-data name");
                if (string.IsNullOrEmpty(user.Name) || !names.Add(user.Name)) throw UnsupportedMetadata("duplicate or empty user-data name");
                if (user.DataType != UserValueType.Int && user.DataType != UserValueType.Float && user.DataType != UserValueType.String)
                    throw UnsupportedMetadata("unknown user-data type");
                if (user.Entries == null) throw UnsupportedMetadata("missing user-data values");
                if (user.DataType == UserValueType.String)
                {
                    if (user.Entries.Length != 1) throw UnsupportedMetadata("user-data string count must be one");
                    ValidateMetadataText(user.Entries[0], "user-data string");
                }
                else foreach (string value in user.Entries)
                {
                    if (user.DataType == UserValueType.Int)
                    {
                        if (!int.TryParse(value, out int parsed)) throw UnsupportedMetadata("invalid user-data integer");
                    }
                    else
                    {
                        if (!float.TryParse(value, out float parsed) || !Finite(parsed) || !FloatMetadataRoundTrips(parsed) || parsed.ToString("R") != value)
                            throw UnsupportedMetadata("user-data float text is not an exact canonical native roundtrip in the current culture");
                    }
                }
            }
        }

        private static void ValidateMetadataText(string value, string label)
        {
            if (value == null || value.IndexOf('\0') >= 0) throw UnsupportedMetadata(label + " contains a null value or embedded terminator");
            try { MetadataUtf8.GetByteCount(value); }
            catch (EncoderFallbackException) { throw UnsupportedMetadata(label + " is not valid UTF-8"); }
        }

        private static bool FloatMetadataRoundTrips(float value) => Finite(value) && float.TryParse(value.ToString("R"), out float decoded) && Bits(value) == Bits(decoded);

        private sealed unsafe class MetadataRange
        {
            internal byte* Start; internal long Length;
            internal ulong Address => new UIntPtr(Start).ToUInt64();
            internal bool Contains(byte* pointer, long count)
            {
                if (pointer == null || count < 0) return false;
                ulong address = new UIntPtr(pointer).ToUInt64(), begin = Address;
                return address >= begin && address - begin <= (ulong)Length && (ulong)count <= (ulong)Length - (address - begin);
            }
        }

        private static unsafe List<MetadataRange> MetadataOwners(ResourceNode node, DataSource data)
        {
            var maps = new List<MetadataRange>(); var spans = new List<MetadataRange>();
            void Add(DataSource value)
            {
                if (value.Address != null && value.Length > 0) spans.Add(new MetadataRange { Start = (byte*)value.Address, Length = value.Length });
                if (value.Map != null && value.Map.Address != null && value.Map.Length > 0)
                    maps.Add(new MetadataRange { Start = (byte*)value.Map.Address, Length = value.Map.Length });
            }
            Add(data);
            var seen = new HashSet<ResourceNode>();
            for (var owner = node; owner != null; owner = owner.Parent)
            {
                if (!seen.Add(owner)) throw UnsupportedMetadata("cyclic metadata owner hierarchy");
                Add(owner.UncompressedSource); Add(owner.WorkingUncompressed);
                if (ReferenceEquals(owner.Parent, owner)) break;
            }
            // Real map extents take precedence over a child's header-declared
            // size. Standalone strings live beyond that declared CHR0 block.
            var physical = maps.Where(r => r.Contains((byte*)data.Address, 1)).ToList();
            if (physical.Count != 0) return physical;
            return spans.Where(r => r.Contains((byte*)data.Address, data.Length)).ToList();
        }

        private static unsafe void ValidateRawMetadata(CHR0Node node, DataSource data, bool generated, bool sourceSnapshot = false)
        {
            var owners = MetadataOwners(node, data); byte* start = (byte*)data.Address;
            if (data.Length < 16 || !owners.Any(r => r.Contains(start, 16))) throw UnsupportedMetadata("truncated raw header");
            int I(long offset) => (int)((uint)start[offset] << 24 | (uint)start[offset + 1] << 16 | (uint)start[offset + 2] << 8 | start[offset + 3]);
            if (start[0] != 'C' || start[1] != 'H' || start[2] != 'R' || start[3] != '0') throw UnsupportedMetadata("invalid raw CHR0 tag");
            int version = I(8), size = I(4), header = version == 4 ? 0x28 : version == 5 ? 0x2c : 0;
            if (header == 0 || size < header || size > data.Length || !owners.Any(r => r.Contains(start, size))) throw UnsupportedMetadata("unsupported or truncated raw version/header block");
            void Need(long offset, long requiredBytes, string label)
            {
                if (offset < 0 || requiredBytes < 0 || offset > size || requiredBytes > size - offset) throw UnsupportedMetadata(label + " lies outside its native metadata block");
            }
            int loop = I(version == 4 ? 0x20 : 0x24);
            if (loop != 0 && loop != 1) throw UnsupportedMetadata("noncanonical loop integer");
            int scalingRule = I(version == 4 ? 0x24 : 0x28);
            if (sourceSnapshot ? scalingRule < 0 || scalingRule > 2 : scalingRule != 0)
                throw UnsupportedMetadata(sourceSnapshot ? "unsupported donor scaling rule" : "nonzero scaling rule");
            if (I(version == 4 ? 0x18 : 0x1c) < 0) throw UnsupportedMetadata("negative original path offset");
            if (version != 5 || I(0x14) == 0) return;
            long user = I(0x14); Need(user, 4 + 0x18, "user-data collection");
            int userSize = I(user); long group = user + 4;
            if (userSize < 4 + 0x18) throw UnsupportedMetadata("invalid user-data collection size");
            Need(user, userSize, "user-data collection");
            int groupSize = I(group), count = I(group + 4);
            if (count < 0 || groupSize != 0x18L + 16L * count || groupSize > userSize - 4) throw UnsupportedMetadata("invalid user-data group size/count");
            Need(group, groupSize, "user-data group");
            var names = new HashSet<string>(StringComparer.Ordinal); var entries = new HashSet<long>();
            for (int i = 0; i < count; i++)
            {
                long row = group + 0x18 + 16L * i, entry = group + I(row + 12);
                Need(entry, 0x18, "user-data entry");
                int entrySize = I(entry), values = I(entry + 8), type = I(entry + 12);
                if (entry < group + groupSize || entrySize < 0x18 || entry > user + userSize || entrySize > user + userSize - entry || !entries.Add(entry))
                    throw UnsupportedMetadata("overlapping or invalid user-data entry block");
                if (I(entry + 20) != i) throw UnsupportedMetadata("noncanonical user-data id");
                if (type < 0 || type > 2) throw UnsupportedMetadata("unknown user-data type");
                if (values < 0 || type == 2 && values != 1) throw UnsupportedMetadata("user-data string count or numeric count");
                int groupNameOffset = I(row + 8), entryNameOffset = I(entry + 16);
                bool placeholderNames = generated && groupNameOffset == 0 && entryNameOffset == 0;
                if (!placeholderNames)
                {
                    string name = Utf8(group, groupNameOffset, "user-data name");
                    if (name.Length == 0 || !names.Add(name)) throw UnsupportedMetadata("duplicate or empty user-data name");
                    if (name != Utf8(entry, entryNameOffset, "user-data entry name")) throw UnsupportedMetadata("conflicting user-data name fields");
                }
                if (type == 2)
                {
                    if (!(generated && I(entry + 4) == 0)) Utf8(entry, I(entry + 4), "user-data string");
                }
                else
                {
                    long offset = I(entry + 4), bytes = 4L * values;
                    if (offset < 0x18 || offset > entrySize || bytes > entrySize - offset) throw UnsupportedMetadata("user-data numeric range");
                    Need(entry + offset, bytes, "user-data numeric range");
                    if (type == 1) for (int n = 0; n < values; n++)
                    {
                        int bits = I(entry + offset + 4L * n); float value = *(float*)&bits;
                        if (!FloatMetadataRoundTrips(value)) throw UnsupportedMetadata("user-data float loses native bits on roundtrip");
                    }
                }
            }

            string Utf8(long relativeBase, int offset, string label)
            {
                if (offset == 0) throw UnsupportedMetadata(label + " has a missing UTF-8 pointer");
                long relative = relativeBase + offset;
                // Relative strings may reside outside the CHR0 block, but only
                // within a known owner/map allocation containing this block.
                ulong address = new UIntPtr(start).ToUInt64();
                if (relative < 0 && (ulong)(-relative) > address || relative > 0 && (ulong)relative > ulong.MaxValue - address)
                    throw UnsupportedMetadata(label + " pointer overflow");
                ulong target = relative < 0 ? address - (ulong)(-relative) : address + (ulong)relative;
                if (IntPtr.Size == 4 && target > uint.MaxValue) throw UnsupportedMetadata(label + " pointer overflow");
                byte* pointer = (byte*)target;
                var owner = owners.Where(r => r.Contains(pointer, 1)).OrderBy(r => r.Length).FirstOrDefault();
                if (owner == null) throw UnsupportedMetadata(label + " UTF-8 pointer lies outside known owner bounds");
                long maximum = owner.Length - (long)(new UIntPtr(pointer).ToUInt64() - owner.Address), length = 0;
                while (length < maximum && pointer[length] != 0) length++;
                if (length == maximum || length > int.MaxValue) throw UnsupportedMetadata(label + " lacks a bounded UTF-8 terminator");
                try { return MetadataUtf8.GetString(pointer, (int)length); }
                catch (DecoderFallbackException) { throw UnsupportedMetadata(label + " is not valid UTF-8"); }
            }
        }
    }
}
