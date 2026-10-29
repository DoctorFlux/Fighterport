using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

internal static partial class Program
{
    sealed class ReadOnlyChr0AuditBudget
    {
        internal long RawBytes,Keys,TextBytes;
        internal readonly Dictionary<ulong,string> Strings=new Dictionary<ulong,string>();
        internal void Raw(long count){if(count<0||count>256L*1024*1024-RawBytes)throw new InvalidDataException("Read-only sibling audit exceeds its 256 MiB raw-resource budget.");RawBytes+=count;}
        internal void Key(){if(++Keys>2000000)throw new InvalidDataException("Read-only sibling audit exceeds its two-million-key budget.");}
        internal void Text(long count){if(count<0||count>16L*1024*1024-TextBytes)throw new InvalidDataException("Read-only sibling audit exceeds its 16 MiB metadata budget.");TextBytes+=count;}
    }

    // Diagnostic only: never Export, Rebuild, CopySource or adopt a storage
    // plan for an unselected clip. Unknown/gapped terminal layouts remain raw
    // evidence, not newly qualified export inputs. Both native buffers include
    // every byte inside the declared resource block, including opaque gaps.
    static string ReadOnlyChr0SiblingHash(CHR0Node clip,ReadOnlyChr0AuditBudget budget)
    {
        using(var stream=new MemoryStream())
        using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            void Text(string value){writer.Write(value!=null);if(value!=null){budget.Text(Encoding.UTF8.GetByteCount(value));writer.Write(value);}}
            Text(clip.Name);writer.Write(clip.Version);writer.Write(clip.FrameCount);writer.Write(clip.Loop);Text(clip.OriginalPath);writer.Write(clip.NativeScalingRule);
            writer.Write(clip.UserEntries.Count);
            foreach(BrawlLib.SSBB.UserDataClass user in clip.UserEntries)
            {Text(user.Name);writer.Write((int)user.DataType);writer.Write(user.Entries?.Length??-1);foreach(string value in user.Entries??new string[0])Text(value);}
            var entries=clip.Children.OfType<CHR0EntryNode>().ToArray();writer.Write(entries.Length);
            foreach(var entry in entries)
            {
                Text(entry.Name);writer.Write(entry.UseModelScale);writer.Write(entry.UseModelRotate);writer.Write(entry.UseModelTranslate);
                writer.Write(entry.ScaleCompensateApply);writer.Write(entry.ScaleCompensateParent);writer.Write(entry.ClassicScaleOff);
                // Compare effective immutable group policies across a normal
                // fresh reopen too, without treating a CLR wrapper type as
                // document content. Unknown raw layouts stay covered below.
                var storage=PreciseCHR0EntryNode.CaptureStorage(entry);
                for(int group=0;group<3;group++)for(int axis=0;axis<3;axis++)
                    Text(storage!=null&&storage.MatchesGroup(entry.Keyframes,group)?storage[group*3+axis]?.ContentKey:null);
                foreach(var array in entry.KeyArrays)
                {
                    writer.Write(array.FrameLimit);writer.Write(array.Loop);writer.Write(array.KeyCount);
                    foreach(var key in array.Entries){budget.Key();writer.Write(Chr0ForensicBits(key._index));writer.Write(Chr0ForensicBits(key._value));writer.Write(Chr0ForensicBits(key._tangent));}
                }
            }
            Text(ReadOnlyChr0RawHash(clip,clip.UncompressedSource,budget));
            Text(ReadOnlyChr0RawHash(clip,clip.WorkingUncompressed,budget));
            writer.Flush();return ReadOnlySiblingDigest(stream.ToArray());
        }
    }

    static string ReadOnlyChr0RawHash(CHR0Node clip,DataSource source,ReadOnlyChr0AuditBudget budget)
    {
        IntPtr origin=(IntPtr)source.Address;if(origin==IntPtr.Zero)return "no-native-buffer";
        if(!WritePathHasOwnerBytes(clip,origin,16))throw new InvalidDataException("Sibling CHR0 header has no independently bounded native owner.");
        var header=new byte[16];Marshal.Copy(origin,header,0,header.Length);
        int size=DenseRead32(header,4),version=DenseRead32(header,8),headerSize=version==4?40:version==5?44:0;
        if(header[0]!='C'||header[1]!='H'||header[2]!='R'||header[3]!='0'||headerSize==0||size<headerSize||
            !WritePathHasOwnerBytes(clip,origin,size))throw new InvalidDataException("Sibling CHR0 declared resource block is unsupported or outside its native owner.");
        budget.Raw(size);var bytes=new byte[size];Marshal.Copy(origin,bytes,0,size);
        var strings=new List<string>();
        void Need(long at,long count){if(at<0||count<0||at>bytes.Length||count>bytes.Length-at)throw new InvalidDataException("Sibling CHR0 relocation cell lies outside its declared resource block.");}
        int I(int at){Need(at,4);return DenseRead32(bytes,at);}
        int Pointer(int baseOffset,int cell,int count)
        {long at=(long)baseOffset+I(cell);Need(at,count);return checked((int)at);}
        void Clear(int at){Need(at,4);Array.Clear(bytes,at,4);}
        void Name(int baseOffset,int cell)
        {
            int relative=I(cell);strings.Add(relative==0?"null":ReadOnlySiblingString(clip,ReadOnlySiblingAdd(origin,(long)baseOffset+relative),budget));Clear(cell);
        }
        int Dictionary(int group)
        {
            Need(group,24);int count=I(group+4);if(count<0||count>4096||I(group)!=24L+16L*count)throw new InvalidDataException("Sibling CHR0 dictionary exceeds its bounded native layout.");
            Need(group,24L+16L*count);for(int i=0;i<count;i++)Name(group,group+24+i*16+8);return count;
        }
        // These cells relocate to the parent/string pool. All internal table,
        // curve and payload offsets, flags, padding and FrameScale stay exact.
        int bres=I(12);if(bres!=0&&!WritePathHasOwnerBytes(clip,ReadOnlySiblingAdd(origin,bres),16))throw new InvalidDataException("Sibling CHR0 parent relocation is not bounded by a real native owner.");Clear(12);
        Name(0,version==4?20:24);Name(0,version==4?24:28);
        int group=Pointer(0,16,24),entries=Dictionary(group);
        for(int i=0;i<entries;i++){int entry=Pointer(group,group+24+i*16+12,8);Name(entry,entry);}
        if(version==5&&I(20)!=0)
        {
            int user=Pointer(0,20,4),userSize=I(user);Need(user,userSize);int userGroup=checked(user+4),count=Dictionary(userGroup);
            for(int i=0;i<count;i++)
            {
                int entry=Pointer(userGroup,userGroup+24+i*16+12,24),entrySize=I(entry),values=I(entry+8),type=I(entry+12);
                if(entrySize<24||values<0||values>2000000)throw new InvalidDataException("Sibling CHR0 user metadata has an invalid native extent.");
                Need(entry,entrySize);Name(entry,entry+16);if(type==2&&values>0)Name(entry,entry+4);
            }
        }
        using(var sha=SHA256.Create())using(var stream=new CryptoStream(Stream.Null,sha,CryptoStreamMode.Write))
        using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {writer.Write(bytes.Length);writer.Write(bytes);writer.Write(strings.Count);foreach(string text in strings)writer.Write(text);writer.Flush();stream.FlushFinalBlock();return BitConverter.ToString(sha.Hash).Replace("-","");}
    }

    static ulong ReadOnlySiblingAddress(IntPtr pointer)=>IntPtr.Size==4?unchecked((uint)pointer.ToInt32()):unchecked((ulong)pointer.ToInt64());
    static IntPtr ReadOnlySiblingAdd(IntPtr pointer,long offset)
    {
        ulong address=ReadOnlySiblingAddress(pointer),result;
        if(offset<0){ulong delta=unchecked((ulong)(-offset));if(delta>address)throw new InvalidDataException("Native sibling pointer underflow.");result=address-delta;}
        else{ulong delta=(ulong)offset;if(delta>ulong.MaxValue-address)throw new InvalidDataException("Native sibling pointer overflow.");result=address+delta;}
        if(result==0||IntPtr.Size==4&&result>uint.MaxValue)throw new InvalidDataException("Native sibling pointer is not representable.");
        return IntPtr.Size==4?new IntPtr(unchecked((int)(uint)result)):new IntPtr(unchecked((long)result));
    }
    static string ReadOnlySiblingString(ResourceNode node,IntPtr pointer,ReadOnlyChr0AuditBudget budget)
    {
        ulong address=ReadOnlySiblingAddress(pointer);if(budget.Strings.TryGetValue(address,out string known))return known;
        long available=0;var seen=new HashSet<ResourceNode>();
        void Span(DataSource data)
        {
            IntPtr start;long length;if(data.Map!=null&&data.Map.Address!=null&&data.Map.Length>0){start=(IntPtr)data.Map.Address;length=data.Map.Length;}else{start=(IntPtr)data.Address;length=data.Length;}
            if(start==IntPtr.Zero||length<1)return;ulong begin=ReadOnlySiblingAddress(start);
            if(address>=begin&&address-begin<(ulong)length)available=Math.Max(available,length-(long)(address-begin));
        }
        for(var owner=node;owner!=null&&seen.Add(owner);owner=owner.Parent){Span(owner.UncompressedSource);Span(owner.WorkingUncompressed);}
        int count=checked((int)Math.Min(available,65536));if(count<1)throw new InvalidDataException("Sibling CHR0 string has no bounded native owner.");
        var bytes=new byte[count];Marshal.Copy(pointer,bytes,0,count);int end=Array.IndexOf(bytes,(byte)0);
        if(end<0)throw new InvalidDataException("Sibling CHR0 string has no bounded terminator within 64 KiB.");
        budget.Text(end);var text=new byte[end];Array.Copy(bytes,text,end);string hash=ReadOnlySiblingDigest(text);budget.Strings.Add(address,hash);return hash;
    }
    static string ReadOnlySiblingDigest(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}

    static void NativeReadOnlySiblingAuditChecks(string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Read-only sibling audit qualification requires the private desktop.");
        Directory.CreateDirectory(output);string path=EditedDenseLiteral(output,"readonly-unbounded-tail",4,true,false),fileHash=FighterProject.HashFile(path);
        string original;byte[] source=File.ReadAllBytes(path);
        using(var clip=NodeFactory.FromFile(null,path) as CHR0Node)
        {
            bool dirty=clip.IsDirty;string backing=Chr0ForensicBackingHash(clip);original=ReadOnlyChr0SiblingHash(clip,new ReadOnlyChr0AuditBudget());
            bool unsupported=false;try{using(var copy=NativeAnimationOptimization.CopySource(clip)) { }}catch(InvalidDataException){unsupported=true;}
            Check(unsupported&&original==ReadOnlyChr0SiblingHash(clip,new ReadOnlyChr0AuditBudget())&&clip.IsDirty==dirty&&Chr0ForensicBackingHash(clip)==backing,
                "Read-only sibling audit observes an unbounded/gapped dense terminal without serializing, qualifying, dirtying or rebuilding it");
        }
        int group=DenseRead32(source,16),entry=group+DenseRead32(source,group+24+12),curve=entry+DenseRead32(source,entry+8);
        var changed=(byte[])source.Clone();changed[curve+24]^=1;string changedPath=Path.Combine(output,"readonly-opaque-gap-changed.chr0");File.WriteAllBytes(changedPath,changed);
        using(var clip=NodeFactory.FromFile(null,changedPath) as CHR0Node)
            Check(ReadOnlyChr0SiblingHash(clip,new ReadOnlyChr0AuditBudget())!=original,"Read-only sibling audit detects a changed opaque byte beyond the projected samples inside the declared resource block");
        int name=DenseRead32(source,20),end=name;while(end<source.Length&&source[end]!=0)end++;
        if(name<=0||end==source.Length)throw new InvalidDataException("Relocation fixture has no original bounded resource name.");
        var relocated=new byte[source.Length+end-name+1];Array.Copy(source,relocated,source.Length);Array.Copy(source,name,relocated,source.Length,end-name+1);DenseWrite32(relocated,20,source.Length);
        string relocatedPath=Path.Combine(output,"readonly-name-relocated.chr0");File.WriteAllBytes(relocatedPath,relocated);
        using(var clip=NodeFactory.FromFile(null,relocatedPath) as CHR0Node)
            Check(ReadOnlyChr0SiblingHash(clip,new ReadOnlyChr0AuditBudget())==original,"Read-only sibling audit permits a verified external-name relocation while hashing its unchanged bytes and all native data");
        Check(FighterProject.HashFile(path)==fileHash,"Read-only sibling audit regression preserves immutable original fixture bytes");
    }
}
