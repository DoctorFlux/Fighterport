using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace FighterPort.Adapters.Melee
{
    public sealed class MeleeFile
    {
        public string Path { get; internal set; }
        public string PhysicalPath { get; internal set; }
        public long Offset { get; internal set; }
        public int Length { get; internal set; }
        public bool FromBase { get; internal set; }
    }

    // Read-only bounded provider. ISO offsets are bytes (not Wii word offsets).
    // Sources stay open with FileShare.Read; no extraction or persistent asset cache.
    public sealed class MeleeSource : IDisposable
    {
        public const string DiscProvider = "melee-gamecube-disc-v1";
        public const string FolderProvider = "melee-hsd-folder-v1";
        public const int MaximumAssetBytes = 256 * 1024 * 1024;
        private readonly Dictionary<string, MeleeFile> files = new Dictionary<string, MeleeFile>(StringComparer.OrdinalIgnoreCase);
        private FileStream disc;
        private MeleeSource baseSource;
        private readonly CancellationToken cancellation;
        public string SourcePath { get; }
        public string GameId { get; private set; }
        public int Revision { get; private set; }
        public bool IsDirectory { get; }
        public IReadOnlyList<MeleeFile> Files => new ReadOnlyCollection<MeleeFile>(files.Values.OrderBy(f => f.Path, StringComparer.Ordinal).ToList());
        public string Provider => IsDirectory ? FolderProvider : DiscProvider;

        public MeleeSource(string path, string basePath = null, CancellationToken cancellation = default)
        {
            this.cancellation = cancellation;
            SourcePath = System.IO.Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
            NoLinks(SourcePath);
            IsDirectory = Directory.Exists(SourcePath);
            try
            {
                if (basePath != null)
                {
                    if (!IsDirectory || string.Equals(SourcePath, System.IO.Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Only a distinct mod folder may use a base source.");
                    baseSource = new MeleeSource(basePath, null, cancellation);
                    GameId = baseSource.GameId; Revision = baseSource.Revision;
                    foreach (var entry in baseSource.Files)
                        files.Add(entry.Path, new MeleeFile { Path = entry.Path, PhysicalPath = entry.PhysicalPath, Offset = entry.Offset, Length = entry.Length, FromBase = true });
                }
                if (IsDirectory) IndexFolder(); else IndexDisc();
                if (!files.Values.Any(f => NativeFile(f.Path))) throw new InvalidDataException("No Melee native DAT/USD resources were found.");
            }
            catch { Dispose(); throw; }
        }
        public static bool NativeFile(string name) => new[] { ".dat", ".usd" }.Contains(System.IO.Path.GetExtension(name).ToLowerInvariant());
        public static void NoLinks(string path)
        {
            for (string at = System.IO.Path.GetFullPath(path); at != null; at = System.IO.Path.GetDirectoryName(at))
                if ((File.Exists(at) || Directory.Exists(at)) && (File.GetAttributes(at) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked source paths require an explicit ordinary source location.");
        }
        private void IndexFolder()
        {
            string root = SourcePath;
            if (Directory.Exists(System.IO.Path.Combine(root, "files"))) root = System.IO.Path.Combine(root, "files");
            var pending = new Stack<string>(); pending.Push(root); int count = 0;
            while (pending.Count > 0)
            {
                cancellation.ThrowIfCancellationRequested(); var dir = pending.Pop(); NoLinks(dir);
                if (++count > 100000) throw new InvalidDataException("Melee folder enumeration exceeded its directory budget.");
                foreach (var child in Directory.EnumerateDirectories(dir)) { NoLinks(child); pending.Push(child); }
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    cancellation.ThrowIfCancellationRequested(); NoLinks(file);
                    if (!NativeFile(file)) continue;
                    string virtualPath = file.Substring(root.TrimEnd('\\', '/').Length + 1).Replace('\\', '/');
                    ValidateVirtualPath(virtualPath); var info = new FileInfo(file);
                    if (info.Length < 32 || info.Length > MaximumAssetBytes) throw new InvalidDataException("Native Melee resource exceeds the reader's size bound: " + virtualPath);
                    if (files.TryGetValue(virtualPath, out var old) && !old.FromBase) throw new InvalidDataException("Ambiguous case-insensitive native paths: " + virtualPath);
                    files[virtualPath] = new MeleeFile { Path = virtualPath, PhysicalPath = file, Length = (int)info.Length };
                    if (files.Count > 1000000) throw new InvalidDataException("Melee source exceeded its file budget.");
                }
            }
        }
        private void IndexDisc()
        {
            string extension = System.IO.Path.GetExtension(SourcePath).ToLowerInvariant();
            if (extension != ".iso" && extension != ".gcm") throw new NotSupportedException("Choose a GameCube ISO/GCM or an extracted Melee/mod folder. ZIP/RVZ images need a separately qualified reader.");
            disc = new FileStream(SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.RandomAccess);
            if (disc.Length < 0x440 || disc.Length > 4L * 1024 * 1024 * 1024) throw new InvalidDataException("Invalid GameCube image length.");
            var header = ReadRange(disc, 0, 0x440, cancellation);
            if (U32(header, 0x1c) != 0xc2339f3d) throw new InvalidDataException("The image does not have a GameCube disc header.");
            GameId = Encoding.ASCII.GetString(header, 0, 6); Revision = header[7];
            if (!GameId.StartsWith("GAL", StringComparison.Ordinal)) throw new NotSupportedException("This GameCube disc does not identify as Melee. Unknown mod-disc IDs need explicit build qualification.");
            long fstOffset = U32(header, 0x424); int fstLength = checked((int)U32(header, 0x428));
            if (fstLength < 12 || fstLength > 64 * 1024 * 1024) throw new InvalidDataException("Invalid GameCube filesystem length.");
            var fst = ReadRange(disc, fstOffset, fstLength, cancellation);
            int entries = checked((int)U32(fst, 8));
            if (fst[0] != 1 || U32(fst, 4) != 0 || entries < 1 || (long)entries * 12 >= fst.Length) throw new InvalidDataException("Invalid GameCube filesystem root.");
            int stringStart = checked(entries * 12);
            var stack = new Stack<Tuple<string,int,int>>(); stack.Push(Tuple.Create("", entries, 0));
            for (int i = 1; i < entries; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                while (stack.Count > 0 && i >= stack.Peek().Item2) stack.Pop();
                if (stack.Count == 0) throw new InvalidDataException("Filesystem entry escaped its parent.");
                int at = i * 12; uint flags = U32(fst, at); int nameOffset = checked(stringStart + (int)(flags & 0xffffff));
                if (nameOffset < stringStart || nameOffset >= fst.Length) throw new InvalidDataException("Invalid filesystem name offset.");
                int end = Array.IndexOf(fst, (byte)0, nameOffset);
                if (end < 0 || end - nameOffset > 1024) throw new InvalidDataException("Unterminated filesystem name.");
                string name = Encoding.ASCII.GetString(fst, nameOffset, end - nameOffset);
                if (name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0) throw new InvalidDataException("Invalid filesystem component.");
                string full = stack.Peek().Item1 + name; ValidateVirtualPath(full);
                long offset = U32(fst, at + 4), length = U32(fst, at + 8);
                if ((flags >> 24) == 1)
                {
                    if (offset != stack.Peek().Item3 || length <= i || length > stack.Peek().Item2 || stack.Count > 128) throw new InvalidDataException("Invalid filesystem directory boundary.");
                    stack.Push(Tuple.Create(full + "/", (int)length, i));
                }
                else
                {
                    if ((flags >> 24) != 0 || offset < 0x440 || length < 0 || offset > disc.Length - length) throw new InvalidDataException("Native file lies outside the image.");
                    if (NativeFile(full))
                    {
                        if (length < 32 || length > MaximumAssetBytes || files.ContainsKey(full)) throw new InvalidDataException("Invalid or ambiguous Melee native member: " + full);
                        files.Add(full, new MeleeFile { Path = full, PhysicalPath = SourcePath, Offset = offset, Length = (int)length });
                    }
                }
            }
        }
        public byte[] ReadExecutable()
        {
            cancellation.ThrowIfCancellationRequested();
            if(IsDirectory)
            {
                foreach(string relative in new[]{"sys/main.dol","main.dol"})
                {
                    string candidate=System.IO.Path.Combine(SourcePath,relative.Replace('/',System.IO.Path.DirectorySeparatorChar));
                    if(!File.Exists(candidate))continue;NoLinks(candidate);
                    using(var input=new FileStream(candidate,FileMode.Open,FileAccess.Read,FileShare.Read))
                    {if(input.Length<256||input.Length>32*1024*1024)throw new InvalidDataException("Native DOL length exceeds its bound.");return ReadRange(input,0,(int)input.Length,cancellation);}
                }
                if(baseSource!=null)return baseSource.ReadExecutable();
                throw new FileNotFoundException("Exact native main.dol is unavailable for this folder.");
            }
            long offset=U32(ReadRange(disc,0x420,4,cancellation),0);
            var header=ReadRange(disc,offset,256,cancellation);long length=256;
            for(int i=0;i<18;i++)if(U32(header,0x90+i*4)>0)length=Math.Max(length,(long)U32(header,i*4)+U32(header,0x90+i*4));
            if(offset<0x440||length>32*1024*1024||offset>disc.Length-length)throw new InvalidDataException("Native DOL sections lie outside the source.");
            return ReadRange(disc,offset,(int)length,cancellation);
        }
        public byte[] Read(string virtualPath)
        {
            cancellation.ThrowIfCancellationRequested(); ValidateVirtualPath(virtualPath);
            if (!files.TryGetValue(virtualPath, out var file)) throw new FileNotFoundException("The native member is unavailable.", virtualPath);
            if (file.FromBase) return baseSource.Read(virtualPath);
            if (!IsDirectory) return ReadRange(disc, file.Offset, file.Length, cancellation);
            NoLinks(file.PhysicalPath);
            using (var input = new FileStream(file.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length != file.Length) throw new InvalidDataException("The Melee folder member changed after enumeration.");
                return ReadRange(input, 0, file.Length, cancellation);
            }
        }
        public string FileHash(string virtualPath) => Sha256(Read(virtualPath));
        public string SourceHash()
        {
            if (IsDirectory)
                return Sha256(Encoding.UTF8.GetBytes(string.Join("\n", Files.Select(f => f.Path + "\0" + FileHash(f.Path)))));
            disc.Position = 0;
            using (var sha = SHA256.Create())
            {
                var buffer = new byte[1024 * 1024]; int count;
                while ((count = disc.Read(buffer, 0, buffer.Length)) > 0) { cancellation.ThrowIfCancellationRequested(); sha.TransformBlock(buffer, 0, count, buffer, 0); }
                sha.TransformFinalBlock(buffer, 0, 0); return Hex(sha.Hash);
            }
        }
        public static string Sha256(byte[] bytes) { using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes)); }
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        public static uint U32(byte[] data, int offset)
        {
            if (offset < 0 || offset > data.Length - 4) throw new InvalidDataException("Truncated native integer.");
            return (uint)data[offset] << 24 | (uint)data[offset + 1] << 16 | (uint)data[offset + 2] << 8 | data[offset + 3];
        }
        public static void ValidateVirtualPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 4096 || path.IndexOf('\\') >= 0 || path.IndexOf(':') >= 0 || path.StartsWith("/", StringComparison.Ordinal) || path.Split('/').Any(p => p.Length == 0 || p == "." || p == ".." || p.Any(c => c < 32)))
                throw new InvalidDataException("Invalid Melee virtual member path.");
        }
        private static byte[] ReadRange(Stream input, long offset, int count, CancellationToken cancellation)
        {
            if (offset < 0 || count < 0 || offset > input.Length - count) throw new InvalidDataException("Native range lies outside its source.");
            input.Position = offset; var bytes = new byte[count]; int done = 0;
            while (done < count) { cancellation.ThrowIfCancellationRequested(); int n = input.Read(bytes, done, Math.Min(65536, count - done)); if (n == 0) throw new EndOfStreamException(); done += n; }
            return bytes;
        }
        public void Dispose() { disc?.Dispose(); disc = null; baseSource?.Dispose(); baseSource = null; }
    }
}
