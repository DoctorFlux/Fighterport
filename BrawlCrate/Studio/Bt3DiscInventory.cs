using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace BrawlCrate.Studio
{
    // Read-only SLUS-21678 ISO9660 / AFS directory inspector. No extracted
    // game files or derivative models are cached by this boundary.
    internal sealed class Bt3DiscInventory
    {
        internal const string Game = "Dragon Ball Z: Budokai Tenkaichi 3";
        internal const string Provider = "bt3-ps2-iso-inventory-v1";
        internal const int FighterAfsBase = 0x590 - 1; // game file IDs are 1-based in partition 1
        internal const int FighterSlots = 161;
        internal const int LabelAfsBase = FighterAfsBase + FighterSlots * 10;
        internal string Path, Volume;
        internal long Length, WriteTicks;
        internal int Afs0Files, Afs1Files, Afs2Files;
        internal Bt3FighterSlot[] Fighters;

        internal sealed class Bt3Resource
        {
            internal int AfsIndex, PackIndex = -1;
            internal long Offset;
            internal int Bytes;
            internal string Kind;
            internal string Identity => "PZS3US1.AFS/" + AfsIndex.ToString("D5") +
                (PackIndex < 0 ? "" : "/" + PackIndex.ToString("D3"));
        }
        internal sealed class Bt3FighterSlot
        {
            internal int Slot;
            internal string DiscLabel;
            internal Bt3Resource[] Costumes;
            internal Bt3Resource[][] MotionMembers;
            internal string Display => "Character slot " + Slot.ToString("D3") +
                (DiscLabel == null ? "" : " · " + DiscLabel + " (disc label)");
            public override string ToString()=>Display;
        }
        private sealed class IsoEntry { internal string Name; internal long Offset; internal int Bytes; internal bool Directory; }
        private sealed class Afs { internal long Base; internal int Bytes,Count; internal uint[] Offsets,Lengths; }

        internal static Bt3DiscInventory Inspect(string path,CancellationToken cancellation=default)
        {
            if(string.IsNullOrWhiteSpace(path)||!File.Exists(path)||!string.Equals(System.IO.Path.GetExtension(path),".iso",StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Choose an existing PS2 ISO image for BT3 disc inspection.");
            var info=new FileInfo(path);
            if((info.Attributes&FileAttributes.ReparsePoint)!=0||info.Length<32768+2048||info.Length>64L*1024*1024*1024)
                throw new InvalidDataException("The BT3 disc must be an ordinary ISO file within the supported size bound.");
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,65536,FileOptions.RandomAccess))
            {
                var pvd=Read(stream,16L*2048,2048);
                if(pvd[0]!=1||Encoding.ASCII.GetString(pvd,1,5)!="CD001"||pvd[6]!=1||
                    Encoding.ASCII.GetString(pvd,8,32).Trim()!="PLAYSTATION"||
                    Encoding.ASCII.GetString(pvd,40,32).Trim()!="SLUS_21678")
                    throw new InvalidDataException("This image is not the inspected PS2 USA SLUS-21678 disc. Other builds need their own verified index mapping.");
                var root=ReadDirectory(stream,DirectoryRecord(pvd,156),cancellation);
                if(!root.Any(e=>e.Name=="SLUS_216.78"&&!e.Directory))throw new InvalidDataException("The expected BT3 executable is absent.");
                var data=root.SingleOrDefault(e=>e.Name=="DATA"&&e.Directory)??throw new InvalidDataException("The BT3 DATA directory is absent.");
                var files=ReadDirectory(stream,data,cancellation);
                Afs Open(string name)
                {
                    var file=files.SingleOrDefault(e=>e.Name==name&&!e.Directory)??throw new InvalidDataException("Missing "+name+" in the native disc.");
                    return ReadAfs(stream,file);
                }
                var zero=Open("PZS3US0.AFS");var one=Open("PZS3US1.AFS");var two=Open("PZS3US2.AFS");
                if(zero.Count!=1||one.Count!=3399||two.Count!=65201||one.Count<LabelAfsBase+FighterSlots)
                    throw new InvalidDataException("The AFS partitions differ from the inspected SLUS-21678 layout.");
                var report=new Bt3DiscInventory{Path=System.IO.Path.GetFullPath(path),Volume="SLUS_21678",Length=info.Length,
                    WriteTicks=info.LastWriteTimeUtc.Ticks,Afs0Files=zero.Count,Afs1Files=one.Count,Afs2Files=two.Count};
                // The metadata label is advisory. Structural identity is always
                // the exact slot and AFS index, including duplicate names.
                var metadata=Read(stream,one.Base+one.Offsets[one.Count],checked((int)one.Lengths[one.Count]));
                if(metadata.Length<one.Count*48)throw new InvalidDataException("AFS member-name table is truncated.");
                var fighters=new List<Bt3FighterSlot>(FighterSlots);
                for(int slot=0;slot<FighterSlots;slot++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int first=FighterAfsBase+slot*10;
                    var costumes=new List<Bt3Resource>();
                    for(int costume=0;costume<8;costume++)
                    {
                        int index=first+costume;
                        if(one.Lengths[index]<=64)continue;
                        var member=Member(one,index,"model + texture pack");
                        var pack=ReadPack(stream,member,4096);
                        if(pack.Length<=2)throw new InvalidDataException("BT3 model pack is missing its native model entry at "+member.Identity);
                        var model=pack[2];var magic=Read(stream,model.Offset,4);
                        if(Encoding.ASCII.GetString(magic)!="pmdl")throw new InvalidDataException("Expected a native pmdl in "+member.Identity);
                        costumes.Add(member);
                    }
                    var banks=new Bt3Resource[2][];
                    for(int bank=0;bank<2;bank++)
                    {
                        int index=first+8+bank;
                        banks[bank]=one.Lengths[index]<=64?new Bt3Resource[0]:ReadPack(stream,Member(one,index,"motion bank"),2048);
                    }
                    var label=ReadLabel(metadata,(LabelAfsBase+slot)*48);
                    fighters.Add(new Bt3FighterSlot{Slot=slot,DiscLabel=label,Costumes=costumes.ToArray(),MotionMembers=banks});
                }
                report.Fighters=fighters.ToArray();return report;
            }
        }
        private static string ReadLabel(byte[] bytes,int at)
        {
            int n=0;while(n<32&&bytes[at+n]!=0)n++;
            var label=Encoding.ASCII.GetString(bytes,at,n);
            return label.Length>1&&label.All(c=>char.IsLetterOrDigit(c)||c=='_'||c=='-'||c==' ')?label:null;
        }
        private static Bt3Resource Member(Afs afs,int index,string kind)
        {
            if(index<0||index>=afs.Count||afs.Lengths[index]>int.MaxValue)throw new InvalidDataException("Invalid BT3 AFS member.");
            return new Bt3Resource{AfsIndex=index,Offset=afs.Base+afs.Offsets[index],Bytes=(int)afs.Lengths[index],Kind=kind};
        }
        private static Bt3Resource[] ReadPack(Stream stream,Bt3Resource parent,int maximum)
        {
            var header=Read(stream,parent.Offset,4);int count=BitConverter.ToInt32(header,0);
            if(count<1||count>maximum||4L+(count+1L)*4>parent.Bytes)throw new InvalidDataException("Invalid BT3 pack table at "+parent.Identity);
            var table=Read(stream,parent.Offset+4,checked((count+1)*4));var result=new Bt3Resource[count];long previous=4L+(count+1L)*4;
            for(int i=0;i<=count;i++)
            {
                uint offset=BitConverter.ToUInt32(table,i*4);
                if(offset<previous||offset>parent.Bytes)throw new InvalidDataException("Invalid BT3 pack offset at "+parent.Identity);
                if(i>0)result[i-1].Bytes=checked((int)(offset-(result[i-1].Offset-parent.Offset)));
                if(i<count)result[i]=new Bt3Resource{AfsIndex=parent.AfsIndex,PackIndex=i,Offset=parent.Offset+offset,Kind="raw motion member"};
                previous=offset;
            }
            return result;
        }
        private static Afs ReadAfs(Stream stream,IsoEntry file)
        {
            var header=Read(stream,file.Offset,8);
            if(header[0]!='A'||header[1]!='F'||header[2]!='S'||header[3]!=0)throw new InvalidDataException("Disc file is not an AFS archive: "+file.Name);
            int count=BitConverter.ToInt32(header,4);
            if(count<1||count>100000||8L+(count+1L)*8>file.Bytes)throw new InvalidDataException("AFS member table exceeds its archive.");
            var table=Read(stream,file.Offset+8,checked((count+1)*8));var offsets=new uint[count+1];var lengths=new uint[count+1];
            for(int i=0;i<=count;i++)
            {
                offsets[i]=BitConverter.ToUInt32(table,i*8);lengths[i]=BitConverter.ToUInt32(table,i*8+4);
                if(offsets[i]<8+(count+1)*8||offsets[i]>(ulong)file.Bytes||lengths[i]>(ulong)file.Bytes-offsets[i])
                    throw new InvalidDataException("AFS member exceeds its archive: "+file.Name+" #"+i);
            }
            return new Afs{Base=file.Offset,Bytes=file.Bytes,Count=count,Offsets=offsets,Lengths=lengths};
        }
        private static IsoEntry DirectoryRecord(byte[] bytes,int at)
        {
            if(at<0||at+34>bytes.Length||bytes[at]<34||at+bytes[at]>bytes.Length)throw new InvalidDataException("Malformed ISO9660 directory record.");
            uint lba=BitConverter.ToUInt32(bytes,at+2),size=BitConverter.ToUInt32(bytes,at+10);
            if(size>int.MaxValue)throw new InvalidDataException("ISO9660 file exceeds the supported read bound.");
            int nameLength=bytes[at+32];if(at+33+nameLength>at+bytes[at])throw new InvalidDataException("Malformed ISO9660 filename.");
            string name=Encoding.ASCII.GetString(bytes,at+33,nameLength).Split(';')[0];
            return new IsoEntry{Name=name,Offset=(long)lba*2048,Bytes=(int)size,Directory=(bytes[at+25]&2)!=0};
        }
        private static List<IsoEntry> ReadDirectory(Stream stream,IsoEntry directory,CancellationToken cancellation)
        {
            if(!directory.Directory||directory.Bytes>1024*1024||directory.Offset+directory.Bytes>stream.Length)throw new InvalidDataException("Invalid ISO9660 directory.");
            var bytes=Read(stream,directory.Offset,directory.Bytes);var result=new List<IsoEntry>();int at=0;
            while(at<bytes.Length)
            {
                cancellation.ThrowIfCancellationRequested();int n=bytes[at];if(n==0){at=(at/2048+1)*2048;continue;}
                var item=DirectoryRecord(bytes,at);if(item.Offset+item.Bytes>stream.Length)throw new InvalidDataException("ISO9660 member exceeds the disc.");
                if(!(item.Name.Length==1&&(item.Name[0]==0||item.Name[0]==1)))result.Add(item);at+=n;
            }
            return result;
        }
        private static byte[] Read(Stream stream,long offset,int bytes)
        {
            if(offset<0||bytes<0||bytes>4*1024*1024||offset+bytes>stream.Length)throw new InvalidDataException("Native disc read exceeds its verified bounds.");
            var result=new byte[bytes];stream.Position=offset;int read=0;while(read<bytes){int n=stream.Read(result,read,bytes-read);if(n==0)throw new EndOfStreamException();read+=n;}return result;
        }
        internal string ExportOriginal(Bt3Resource resource,string destination,CancellationToken cancellation=default)
        {
            if(resource==null||resource.Bytes<0||resource.Offset<0||resource.Offset+(long)resource.Bytes>Length)
                throw new InvalidDataException("Select one bounded native pack or motion member.");
            if(Fighters==null||!Fighters.Any(f=>f.Costumes.Any(c=>ReferenceEquals(c,resource))||
                f.MotionMembers.Any(bank=>bank.Any(c=>ReferenceEquals(c,resource)))))
                throw new InvalidDataException("Select a member from this exact inspected disc inventory.");
            if(!System.IO.Path.IsPathRooted(destination)||File.Exists(destination)||Directory.Exists(destination)||
                string.Equals(System.IO.Path.GetFullPath(destination),Path,StringComparison.OrdinalIgnoreCase))throw new IOException("Choose a new output filename outside the disc image.");
            var info=new FileInfo(Path);if(info.Length!=Length||info.LastWriteTimeUtc.Ticks!=WriteTicks)throw new InvalidDataException("The disc changed since inspection. Inspect it again.");
            string temp=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(destination),".fp-bt3-"+Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                string digest;
                using(var input=new FileStream(Path,FileMode.Open,FileAccess.Read,FileShare.Read))
                using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using(var hash=SHA256.Create())
                {
                    input.Position=resource.Offset;var buffer=new byte[65536];int left=resource.Bytes;
                    while(left>0){cancellation.ThrowIfCancellationRequested();int n=input.Read(buffer,0,Math.Min(left,buffer.Length));if(n==0)throw new EndOfStreamException();output.Write(buffer,0,n);hash.TransformBlock(buffer,0,n,null,0);left-=n;}
                    hash.TransformFinalBlock(new byte[0],0,0);output.Flush(true);
                    digest=BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();
                }
                if(new FileInfo(temp).Length!=resource.Bytes||!string.Equals(FighterProject.HashFile(temp),digest,StringComparison.Ordinal))
                    throw new IOException("Incomplete BT3 member copy.");
                cancellation.ThrowIfCancellationRequested();
                info.Refresh();if(info.Length!=Length||info.LastWriteTimeUtc.Ticks!=WriteTicks)
                    throw new InvalidDataException("The disc changed during original member export. Inspect it again.");
                if(File.Exists(destination))throw new IOException("Output appeared during export; it was preserved.");
                File.Move(temp,destination);return digest;
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
