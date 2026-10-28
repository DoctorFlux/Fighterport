using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace BrawlCrate.Studio
{
    // Direct serialized-resource evidence, without calling Export/Rebuild. A
    // BRRES relocation changes its parent pointer and external string offsets;
    // those cells alone are normalized. Every other declared byte participates,
    // including I12 frameScale/reserved headers, fixed slots and alignment.
    // String contents are independently checked by SourceContentKey.
    internal static class NativeChr0RawStorage
    {
        internal static unsafe string Signature(CHR0Node clip)
        {
            if (clip == null || clip.IsDirty) throw new InvalidDataException("Raw CHR0 storage verification requires a freshly decoded unchanged member.");
            var source = clip.WorkingUncompressed;
            if (source.Length < 40 || source.Length > 256 * 1024 * 1024)
                throw new InvalidDataException("Raw CHR0 storage exceeds the bounded native resource size.");
            byte* address = (byte*)source.Address;
            int declared = unchecked((int)((uint)address[4] << 24 | (uint)address[5] << 16 | (uint)address[6] << 8 | address[7]));
            if (declared < 40 || declared > source.Length) throw new InvalidDataException("The raw CHR0 declared resource is truncated.");
            var bytes = new byte[declared]; Marshal.Copy((IntPtr)address, bytes, 0, declared);
            return new Normalizer(bytes).Signature();
        }

        private sealed class Normalizer
        {
            readonly byte[] original, normalized;
            internal Normalizer(byte[] bytes) { original = bytes; normalized = (byte[])bytes.Clone(); }
            void Need(long at, long count)
            {
                if (at < 0 || count < 0 || at > original.Length || count > original.Length - at)
                    throw new InvalidDataException("Raw CHR0 relocation field is outside the declared resource.");
            }
            int U16(int at) { Need(at, 2); return original[at] << 8 | original[at + 1]; }
            int I32(int at)
            {
                Need(at, 4);
                return unchecked((int)((uint)original[at] << 24 | (uint)original[at + 1] << 16 | (uint)original[at + 2] << 8 | original[at + 3]));
            }
            int Pointer(int origin, int cell, int length)
            {
                long at = (long)origin + I32(cell); Need(at, length); return checked((int)at);
            }
            void StringPointer(int cell)
            {
                // Null remains distinct. Offsets can be negative in containers.
                bool present = I32(cell) != 0;
                for (int i = 0; i < 4; i++) normalized[cell + i] = 0;
                if (present) normalized[cell + 3] = 1;
            }
            int Dictionary(int group)
            {
                Need(group, 24); int count = I32(group + 4), size = I32(group);
                if (count < 0 || count > 65535 || size != 24L + 16L * count) throw new InvalidDataException("Raw CHR0 dictionary size is invalid.");
                Need(group, size);
                for (int row = 0; row <= count; row++)
                {
                    int at = group + 8 + 16 * row;
                    if (U16(at + 4) > count || U16(at + 6) > count) throw new InvalidDataException("Raw CHR0 dictionary links are invalid.");
                    if (row > 0) StringPointer(at + 8);
                }
                return count;
            }
            internal string Signature()
            {
                if (Encoding.ASCII.GetString(original, 0, 4) != "CHR0") throw new InvalidDataException("Expected a raw native CHR0 member.");
                int version = I32(8), header = version == 4 ? 40 : version == 5 ? 44 : 0;
                if (header == 0) throw new InvalidDataException("Raw CHR0 verification supports native v4/v5.");
                Need(0, header);
                // _bresOffset alone necessarily differs standalone vs BRRES.
                for (int at = 12; at < 16; at++) normalized[at] = 0;
                StringPointer(version == 4 ? 20 : 24); StringPointer(version == 4 ? 24 : 28);
                int group = Pointer(0, 16, 24), count = Dictionary(group);
                int frameCell = version == 4 ? 28 : 32;
                if (count != U16(frameCell + 2)) throw new InvalidDataException("Raw CHR0 dictionary and header entry counts disagree.");
                for (int row = 0; row < count; row++)
                {
                    int entry = Pointer(group, group + 24 + 16 * row + 12, 8);
                    StringPointer(entry);
                }
                if (version == 5 && I32(20) != 0)
                {
                    int users = Pointer(0, 20, 4), userSize = I32(users); Need(users, userSize);
                    int userGroup = users + 4, userCount = Dictionary(userGroup);
                    for (int row = 0; row < userCount; row++)
                    {
                        int entry = Pointer(userGroup, userGroup + 24 + 16 * row + 12, 24);
                        int size = I32(entry), values = I32(entry + 8), type = I32(entry + 12);
                        if (size < 24 || values < 0 || values > 2000000 || type < 0 || type > 2)
                            throw new InvalidDataException("Raw CHR0 user metadata is invalid.");
                        Need(entry, size); StringPointer(entry + 16);
                        if (type == 2 && values > 0) StringPointer(entry + 4);
                    }
                }
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(normalized)).Replace("-", "");
            }
        }
    }
}
