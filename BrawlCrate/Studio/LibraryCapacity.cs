using System;
using System.IO;
using System.Runtime.InteropServices;

namespace BrawlCrate.Studio
{
    // Capacity follows this process and machine, rather than an animation count.
    // Reserve native-editor headroom and budget six bytes of working memory per
    // serialized byte for strings, indexed rows and concurrent metadata copies.
    internal static class LibraryCapacity
    {
        internal const long EditorReserve=256L*1024*1024;
        internal static long XmlAllowance(long virtualFree,long physicalFree,long commitFree)
            =>Math.Max(0,Math.Min(Math.Min(virtualFree-EditorReserve,commitFree-EditorReserve),physicalFree/2)/6);
        [StructLayout(LayoutKind.Sequential)]private struct MemoryStatus
        {internal uint Length,Load;internal ulong Physical,PhysicalFree,PageFile,PageFree,Virtual,VirtualFree,ExtendedFree;}
        [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool GetDiskFreeSpaceEx(string directory,out ulong available,out ulong total,out ulong free);
        internal static long CurrentXmlAllowance()
        {
            var status=new MemoryStatus{Length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};
            if(!GlobalMemoryStatusEx(ref status))throw new IOException("Available library memory could not be checked. The existing index was kept.");
            return XmlAllowance((long)Math.Min(status.VirtualFree,(ulong)long.MaxValue),(long)Math.Min(status.PhysicalFree,(ulong)long.MaxValue),(long)Math.Min(status.PageFree,(ulong)long.MaxValue));
        }
        internal static long CheckRead(long expandedBytes)
        {
            long allowed=CurrentXmlAllowance();
            if(expandedBytes>allowed)
            {
                // Only a paused explicit metadata operation reaches this path;
                // retire unreferenced snapshots before rejecting a large load.
                GC.Collect();GC.WaitForPendingFinalizers();allowed=CurrentXmlAllowance();
            }
            if(expandedBytes>allowed||allowed<1)throw new InvalidDataException("The library needs more available memory to load safely. Close other documents or applications and try again. Original game sources and the index are unchanged.");
            return allowed;
        }
        internal static long DiskAllowance(string directory)
        {
            if(!GetDiskFreeSpaceEx(directory,out ulong available,out _,out _))throw new IOException("Available library disk space could not be checked.");
            long allowed=(long)Math.Min(available,(ulong)long.MaxValue)-64L*1024*1024;
            if(allowed<1)throw new IOException("There is not enough free space for a safe metadata update. The existing index was kept.");
            return allowed;
        }
    }
    internal sealed class LibraryQuotaWriter:Stream
    {
        private readonly Stream output;private readonly long limit;private long written;
        internal LibraryQuotaWriter(Stream output,long limit){this.output=output;this.limit=limit;}
        public override void Write(byte[] buffer,int offset,int count)
        {if(count>limit-written)throw new IOException("The library update exceeded available resource headroom. The existing index was kept; free memory/disk space and try again.");output.Write(buffer,offset,count);written+=count;}
        public override void Flush()=>output.Flush();
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>written;public override long Position{get=>written;set=>throw new NotSupportedException();}
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
    }
}
