using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace BrawlCrate.Studio
{
    // Independent managed, read-only Wii ISO/WBFS reader. Format references and
    // inherited BrawlLib audit are recorded in docs/INTEGRATED_DISC_READER.md.
    // No ResourceNode, UI, process, emulator, native DLL or whole-disc mapping.
    internal sealed class BrawlDiscImage : IDisposable
    {
        internal static int DiagnosticOpens;
        internal static int DiagnosticNativeReads;
        internal const string Provider = "fighterport-brawl-disc-v1";
        private const long MaximumDisc = 143432L * 2 * 0x8000;
        private const int Cluster = 0x8000, Payload = 0x7c00;
        private readonly FileStream input;
        private readonly CancellationToken cancellation;
        private readonly long blockSize, logicalLength;
        private readonly ushort[] allocation;
        private readonly byte[] titleKey, h3;
        private readonly long dataStart, dataLength;
        private readonly bool hashes, encrypted;
        private readonly byte[] raw = new byte[Cluster];
        private long cachedBlock = -1;
        private byte[] cachedData;
        private readonly List<Entry> entries = new List<Entry>();
        internal string GameId { get; }
        internal int Revision { get; }
        internal string Region { get; }
        internal long VerifiedClusters { get; private set; }
        internal sealed class Entry
        {
            internal string Path;
            internal long Offset, Length;
        }
        // Retail Wii format constants; no console-specific/user key material.
        private static readonly byte[] RetailKey = { 0xeb,0xe4,0x2a,0x22,0x5e,0x85,0x93,0xe4,0x48,0xd9,0xc5,0x45,0x73,0x81,0xaa,0xf7 };

        internal BrawlDiscImage(string path, CancellationToken token = default)
        {
            cancellation = token; cancellation.ThrowIfCancellationRequested();
            Interlocked.Increment(ref DiagnosticOpens);
            path = AnimationLibrary.OrdinaryPath(path); AnimationLibrary.NoLinks(path);
            BrawlGameSource.EnsureSingleDiscSource(path);
            if (!new[] { ".iso", ".wbfs" }.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant()))
                throw new NotSupportedException("The built-in Brawl reader accepts ISO and single-file WBFS. Choose one of those or an extracted Brawl folder.");
            input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.RandomAccess);
            try
            {
                if (input.Length < 0x50000 || input.Length > 64L * 1024 * 1024 * 1024)
                    throw new InvalidDataException("The Brawl image is truncated or exceeds the reader's 64 GB bound.");
                byte[] first = Physical(0, 12);
                if (U32(first, 0) == 0x57424653)
                {
                    int hdShift = first[8], wbShift = first[9];
                    if (hdShift < 9 || hdShift > 16 || wbShift < 15 || wbShift > 30 || wbShift < hdShift)
                        throw new InvalidDataException("Invalid WBFS sector dimensions.");
                    int hdSize = 1 << hdShift; blockSize = 1L << wbShift;
                    if ((long)U32(first, 4) * hdSize != input.Length)
                        throw new InvalidDataException("WBFS physical size differs from its complete single-file header.");
                    byte[] slots = Physical(12, hdSize - 12);
                    byte[] occupied = slots.Where(value => value != 0).ToArray();
                    if (occupied.Length != 1) throw new InvalidDataException("Choose a WBFS image containing exactly one disc.");
                    int count = checked((int)((MaximumDisc + blockSize - 1) / blockSize));
                    // Slot bytes are occupancy flags, not byte/sector pointers.
                    long infoSize = ((0x100L + count * 2 + hdSize - 1) / hdSize) * hdSize;
                    long info = checked(hdSize + Array.FindIndex(slots, value => value != 0) * infoSize);
                    byte[] table = Physical(info + 0x100, checked(count * 2));
                    long metadataEnd = info + 0x100 + table.Length;
                    allocation = new ushort[count]; var used = new HashSet<ushort>();
                    for (int i = 0; i < count; i++)
                    {
                        ushort value = (ushort)((table[i * 2] << 8) | table[i * 2 + 1]); allocation[i] = value;
                        if (value == 0) continue;
                        long address = value * blockSize;
                        if (address < metadataEnd || address > input.Length - blockSize || !used.Add(value))
                            throw new InvalidDataException("WBFS has an overlapping, duplicated or out-of-range allocation.");
                    }
                    logicalLength = MaximumDisc;
                    if (!Physical(info, 0x100).SequenceEqual(ReadDisc(0, 0x100)))
                        throw new InvalidDataException("WBFS disc-info identity differs from its mapped disc header.");
                }
                else logicalLength = input.Length;
                byte[] header = ReadDisc(0, 0x440);
                GameId = Encoding.ASCII.GetString(header, 0, 6);
                if (U32(header, 0x18) != 0x5d1c9ea3 || !Regex.IsMatch(GameId, @"^RSB[A-Z0-9]{3}$"))
                    throw new InvalidDataException("This image is not identified as a Brawl Wii disc. Other games require their own source readers.");
                Revision = header[7]; Region = GameId[3] == 'E' ? "NTSC-U" : GameId[3] == 'J' ? "NTSC-J" : GameId[3] == 'P' ? "PAL" : "Unknown";
                if (header[0x60] > 1 || header[0x61] > 1) throw new InvalidDataException("Invalid Wii partition flags.");
                hashes = header[0x60] == 0; encrypted = header[0x61] == 0;
                if (encrypted && !hashes) throw new InvalidDataException("Encrypted Wii data without hash clusters is unsupported.");
                byte[] groups = ReadDisc(0x40000, 32); var partitions = new List<long>(); var seen = new HashSet<long>();
                for (int group = 0; group < 4; group++)
                {
                    uint count = U32(groups, group * 8); if (count > 256) throw new InvalidDataException("Too many Wii partitions.");
                    if (count == 0) continue;
                    byte[] table = ReadDisc((long)U32(groups, group * 8 + 4) * 4, checked((int)count * 8));
                    for (int i = 0; i < count; i++)
                    {
                        long offset = (long)U32(table, i * 8) * 4;
                        if (!seen.Add(offset)) throw new InvalidDataException("Duplicate Wii partition address.");
                        if (U32(table, i * 8 + 4) == 0) partitions.Add(offset);
                    }
                }
                if (partitions.Count != 1) throw new InvalidDataException("The Brawl image must contain one unambiguous DATA partition.");
                long partition = partitions[0]; byte[] ticket = ReadDisc(partition, 0x2c0);
                if (encrypted)
                {
                    if (U32(ticket, 0) != 0x10001 || ticket[0x1f1] != 0)
                        throw new NotSupportedException("This ticket type/common-key index is outside the built-in retail Brawl reader.");
                    byte[] iv = new byte[16]; Buffer.BlockCopy(ticket, 0x1dc, iv, 0, 8);
                    titleKey = Decrypt(RetailKey, iv, ticket, 0x1bf, 16);
                }
                long relativeData = (long)U32(ticket, 0x2b8) * 4;
                long physicalDataLength = (long)U32(ticket, 0x2bc) * 4;
                if (relativeData < 0x2c0 || physicalDataLength == 0) throw new InvalidDataException("Invalid Wii DATA extent.");
                dataStart = checked(partition + relativeData); CheckRange(dataStart, physicalDataLength, logicalLength);
                if (hashes && physicalDataLength % Cluster != 0) throw new InvalidDataException("Truncated Wii cluster extent.");
                dataLength = hashes ? physicalDataLength / Cluster * Payload : physicalDataLength;
                if (hashes)
                {
                    long h3Offset = (long)U32(ticket, 0x2b4) * 4;
                    if (h3Offset < 0x2c0 || h3Offset > relativeData - 0x18000)
                        throw new InvalidDataException("Invalid Wii H3 table extent.");
                    h3 = ReadDisc(partition + h3Offset, 0x18000);
                }
                byte[] boot = ReadPartition(0, 0x440);
                if (U32(boot, 0x18) != 0x5d1c9ea3 || Encoding.ASCII.GetString(boot, 0, 6) != GameId || boot[7] != Revision)
                    throw new InvalidDataException("The decrypted DATA partition does not match its Brawl disc header.");
                long fstOffset = (long)U32(boot, 0x424) * 4, fstLength = (long)U32(boot, 0x428) * 4;
                if (fstLength < 12 || fstLength > 32 * 1024 * 1024) throw new InvalidDataException("The Wii filesystem table exceeds its 32 MB bound.");
                ReadFileSystem(ReadPartition(fstOffset, (int)fstLength));
            }
            catch { input.Dispose(); throw; }
        }

        private static uint U32(byte[] bytes, int at) => (uint)bytes[at] << 24 | (uint)bytes[at + 1] << 16 | (uint)bytes[at + 2] << 8 | bytes[at + 3];
        private static void CheckRange(long offset, long count, long length)
        { if (offset < 0 || count < 0 || offset > length || count > length - offset) throw new InvalidDataException("Disc data points outside its bounded extent."); }
        private byte[] Physical(long offset, int count)
        {
            CheckRange(offset, count, input.Length); var bytes = new byte[count]; input.Position = offset; int done = 0;
            while (done < count) { cancellation.ThrowIfCancellationRequested(); int read = input.Read(bytes, done, count - done); if (read == 0) throw new EndOfStreamException("Disc source was truncated."); done += read; }
            return bytes;
        }
        private byte[] ReadDisc(long offset, int count)
        {
            CheckRange(offset, count, logicalLength); if (allocation == null) return Physical(offset, count);
            byte[] bytes = new byte[count]; int done = 0;
            while (done < count)
            {
                cancellation.ThrowIfCancellationRequested(); int index = checked((int)(offset / blockSize));
                if (index >= allocation.Length || allocation[index] == 0) throw new InvalidDataException("Required Wii data is missing from the WBFS allocation map.");
                long within = offset % blockSize; int take = (int)Math.Min(count - done, blockSize - within);
                byte[] part = Physical(allocation[index] * blockSize + within, take); Buffer.BlockCopy(part, 0, bytes, done, take); offset += take; done += take;
            }
            return bytes;
        }
        private static byte[] Decrypt(byte[] key, byte[] iv, byte[] bytes, int offset, int count)
        {
            using (var aes = Aes.Create()) { aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.None;
                using (var transform = aes.CreateDecryptor()) return transform.TransformFinalBlock(bytes, offset, count); }
        }
        internal byte[] ReadPartition(long offset, int count)
        {
            if (count > 32 * 1024 * 1024) throw new InvalidDataException("A native disc read exceeds its 32 MB buffer bound.");
            CheckRange(offset, count, dataLength); if (!hashes) return ReadDisc(dataStart + offset, count);
            byte[] result = new byte[count]; int done = 0;
            while (done < count)
            {
                cancellation.ThrowIfCancellationRequested(); long block = offset / Payload;
                if (cachedBlock != block)
                {
                    byte[] bytes = ReadDisc(dataStart + block * Cluster, Cluster);
                    Buffer.BlockCopy(bytes, 0, raw, 0, Cluster);
                    byte[] iv = new byte[16]; Buffer.BlockCopy(raw, 0x3d0, iv, 0, 16);
                    byte[] data = encrypted ? Decrypt(titleKey, iv, raw, 0x400, Payload) : raw.Skip(0x400).ToArray();
                    byte[] checks = encrypted ? Decrypt(titleKey, new byte[16], raw, 0, 0x400) : raw.Take(0x400).ToArray();
                    using (var sha = SHA1.Create())
                    {
                        for (int i = 0; i < 31; i++) MatchHash(sha.ComputeHash(data, i * 0x400, 0x400), checks, i * 20);
                        MatchHash(sha.ComputeHash(checks, 0, 31 * 20), checks, 0x280 + (int)(block % 8) * 20);
                        MatchHash(sha.ComputeHash(checks, 0x280, 8 * 20), checks, 0x340 + (int)(block / 8 % 8) * 20);
                        MatchHash(sha.ComputeHash(checks, 0x340, 8 * 20), h3, checked((int)(block / 64 * 20)));
                    }
                    cachedBlock = block; cachedData = data; VerifiedClusters++;
                }
                int within = (int)(offset % Payload), take = Math.Min(count - done, Payload - within);
                Buffer.BlockCopy(cachedData, within, result, done, take); offset += take; done += take;
            }
            return result;
        }
        private static void MatchHash(byte[] actual, byte[] expected, int at)
        {
            if (at < 0 || at > expected.Length - actual.Length) throw new InvalidDataException("Wii cluster integrity table is out of range.");
            for (int i = 0; i < actual.Length; i++) if (actual[i] != expected[at + i]) throw new InvalidDataException("Wii cluster integrity verification failed. The image may be damaged or unsupported.");
        }
        private sealed class DirectoryEntry { internal string Path; internal int Index, End; }
        private void ReadFileSystem(byte[] fst)
        {
            uint count = U32(fst, 8);
            if (fst[0] != 1 || U32(fst, 4) != 0 || count < 1 || count > 200000 || (long)count * 12 > fst.Length)
                throw new InvalidDataException("Invalid or oversized Wii filesystem root.");
            int names = checked((int)count * 12); var stack = new Stack<DirectoryEntry>();
            stack.Push(new DirectoryEntry { Path = "", Index = 0, End = (int)count }); var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sjis = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            for (int i = 1; i < count; i++)
            {
                cancellation.ThrowIfCancellationRequested(); while (stack.Count > 0 && i >= stack.Peek().End) stack.Pop();
                if (stack.Count == 0 || stack.Count > 64) throw new InvalidDataException("Invalid Wii directory nesting.");
                var parent = stack.Peek(); int at = i * 12; uint tagged = U32(fst, at); uint type = tagged >> 24;
                if (type > 1) throw new InvalidDataException("Invalid Wii filesystem entry type.");
                long start = names + (tagged & 0xffffff); if (start >= fst.Length) throw new InvalidDataException("Wii filename is out of bounds.");
                int end = (int)start; while (end < fst.Length && end - start <= 255 && fst[end] != 0) end++;
                if (end >= fst.Length || end - start > 255) throw new InvalidDataException("Unterminated or oversized Wii filename.");
                string name = sjis.GetString(fst, (int)start, end - (int)start);
                if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(".") || name.EndsWith(" ") || Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase))
                    throw new InvalidDataException("Unsafe or ambiguous Wii filename.");
                string path = parent.Path + name;
                if (path.Length > 1024 || !unique.Add(path)) throw new InvalidDataException("Duplicate or oversized Wii filesystem path.");
                if (type == 1)
                {
                    uint next = U32(fst, at + 8);
                    if (U32(fst, at + 4) != parent.Index || next <= i || next > parent.End) throw new InvalidDataException("Invalid Wii directory parent or subtree range.");
                    stack.Push(new DirectoryEntry { Path = path + "/", Index = i, End = (int)next });
                }
                else
                {
                    long offset = (long)U32(fst, at + 4) * 4, length = U32(fst, at + 8); CheckRange(offset, length, dataLength);
                    if (path.StartsWith("fighter/", StringComparison.Ordinal))
                    {
                        if (entries.Count >= 10000) throw new InvalidDataException("The fighter inventory exceeds its 10,000-file bound.");
                        entries.Add(new Entry { Path = path, Offset = offset, Length = length });
                    }
                }
            }
            if (entries.Count == 0) throw new InvalidDataException("This DATA partition contains no native fighter filesystem.");
        }
        internal string[] Files => entries.Select(entry => entry.Path).ToArray();
        internal Entry[] Inventory => entries.ToArray();
        internal long CopyNativeFile(string virtualPath,Stream output)
        {
            var entry=entries.SingleOrDefault(e=>e.Path==virtualPath)??throw new InvalidDataException("The exact indexed fighter package is missing from this disc.");
            if(entry.Length<1||entry.Length>FighterProject.MaximumAssetBytes)throw new InvalidDataException("The selected native package exceeds its bounded load size.");
            Interlocked.Increment(ref DiagnosticNativeReads);
            long done=0;
            while(done<entry.Length)
            {
                cancellation.ThrowIfCancellationRequested();int take=(int)Math.Min(256*1024,entry.Length-done);
                byte[] bytes=ReadPartition(entry.Offset+done,take);output.Write(bytes,0,bytes.Length);done+=take;
            }
            return done;
        }
        internal void Extract(string character, string root)
        {
            if (!Regex.IsMatch(character ?? "", @"^[A-Za-z0-9_-]+$")) throw new InvalidDataException("Choose an exact indexed character folder.");
            Entry[] selected = entries.Where(entry => entry.Path.StartsWith("fighter/" + character + "/", StringComparison.Ordinal)).ToArray();
            if (selected.Length == 0) throw new InvalidDataException("The selected character is absent from this disc.");
            ExtractEntries(selected, root);
        }
        internal void ExtractAll(string root) => ExtractEntries(entries.ToArray(), root);
        private void ExtractEntries(Entry[] selected, string root)
        {
            if (selected.Sum(entry => entry.Length) > 4L * 1024 * 1024 * 1024 || selected.Any(entry => entry.Length > FighterProject.MaximumAssetBytes))
                throw new InvalidDataException("The selected native subtree exceeds its extraction bounds.");
            root = AnimationLibrary.OrdinaryPath(root); AnimationLibrary.NoLinks(root);
            string prefix = root.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            foreach (var entry in selected)
            {
                cancellation.ThrowIfCancellationRequested(); string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, entry.Path.Replace('/', System.IO.Path.DirectorySeparatorChar)));
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || path.Length >= 250) throw new InvalidDataException("Choose a shorter local library directory for the native cache.");
                AnimationLibrary.NoLinks(path); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)); AnimationLibrary.NoLinks(path);
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    long done = 0;
                    while (done < entry.Length) { cancellation.ThrowIfCancellationRequested(); int take = (int)Math.Min(256 * 1024, entry.Length - done); byte[] bytes = ReadPartition(entry.Offset + done, take); output.Write(bytes, 0, bytes.Length); done += take; }
                    output.Flush(true);
                }
            }
        }
        public void Dispose() { input.Dispose(); if (titleKey != null) Array.Clear(titleKey, 0, titleKey.Length); cachedData = null; }
    }
}
