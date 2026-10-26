using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace BrawlCrate.Studio
{
    // Native ResourceNode owns file-backed maps. Only explicitly selected
    // packages are materialized; this directory belongs to one operation/lease.
    internal sealed class NativeSourceScratch : IDisposable
    {
        private readonly string parent;
        internal string DirectoryPath {get;}
        internal NativeSourceScratch()
        {
            parent=AnimationLibrary.OrdinaryPath(Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_TEMP_ROOT")??Path.Combine(Path.GetTempPath(),"Fighterport-native-sources"));
            AnimationLibrary.NoLinks(parent);Directory.CreateDirectory(parent);
            DirectoryPath=Path.Combine(parent,Guid.NewGuid().ToString("N"));
            if(Directory.Exists(DirectoryPath))throw new IOException("Native source staging collision.");
            Directory.CreateDirectory(DirectoryPath);AnimationLibrary.NoLinks(DirectoryPath);
        }
        internal string Read(BrawlDiscImage image,string virtualPath,string key)
        {
            if(string.IsNullOrEmpty(key)||key.Length>64||key.Any(c=>!char.IsLetterOrDigit(c)&&c!='_'&&c!='-'))throw new InvalidDataException("Invalid native staging identity.");
            string folder=Path.Combine(DirectoryPath,key);Directory.CreateDirectory(folder);AnimationLibrary.NoLinks(folder);
            string path=Path.Combine(folder,Path.GetFileName(virtualPath));AnimationLibrary.NoLinks(path);
            using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))image.CopyNativeFile(virtualPath,stream);
            return path;
        }
        internal static void Verify(BrawlDiscImage image,LibraryMount mount,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            using(var sha=SHA256.Create())using(var hash=new CryptoStream(Stream.Null,sha,CryptoStreamMode.Write))
            {
                long bytes=image.CopyNativeFile(mount.VirtualPath,hash);hash.FlushFinalBlock();
                if(bytes!=mount.Bytes||BrawlLibrarySource.Hex(sha.Hash)!=mount.ContainerSha256)throw new InvalidDataException("The native package in the original disc differs from its indexed identity.");
            }
        }
        public void Dispose()
        {
            string absolute=Path.GetFullPath(DirectoryPath),prefix=Path.GetFullPath(parent).TrimEnd('\\','/')+Path.DirectorySeparatorChar;
            if(!absolute.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!Guid.TryParseExact(Path.GetFileName(absolute),"N",out _))throw new InvalidOperationException("Invalid native scratch ownership.");
            if(Directory.Exists(absolute))
            {
                AnimationLibrary.NoLinks(absolute);
                Directory.Delete(absolute,true);
            }
        }
    }
}
