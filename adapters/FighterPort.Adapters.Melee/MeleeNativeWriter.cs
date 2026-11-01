using HSDRaw;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
namespace FighterPort.Adapters.Melee
{
    public sealed class MeleeWriteCandidate
    {
        private readonly byte[] bytes;
        public string ReferenceSha256 {get;}
        public string Sha256 {get;}
        public string GraphSha256 {get;}
        public string FallbackReason {get;}
        public bool Rebuilt {get;}
        public int Length=>bytes.Length;
        internal MeleeWriteCandidate(byte[] bytes,string reference,string graph,bool rebuilt,string fallback)
        {this.bytes=(byte[])bytes.Clone();ReferenceSha256=reference;Sha256=MeleeSource.Sha256(bytes);GraphSha256=graph;Rebuilt=rebuilt;FallbackReason=fallback;}
        public byte[] CopyBytes()=>(byte[])bytes.Clone();
    }
    public static class MeleeNativeWriter
    {
        // A complete rooted/reference graph witness: offsets are normalized,
        // every other byte and every ordered pointer edge remains exact.
        public static string GraphHash(HSDRawFile native,CancellationToken cancellation=default)
        {
            var ids=new Dictionary<HSDStruct,int>();var nodes=new List<HSDStruct>();
            Action<HSDStruct> add=node=>{if(node!=null&&!ids.ContainsKey(node)){if(nodes.Count>=2000000)throw new InvalidDataException("HSD graph verification budget exceeded.");ids.Add(node,nodes.Count);nodes.Add(node);}};
            foreach(var root in native.Roots.Concat(native.References))add(root.Data?._s);
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                writer.Write(native.Roots.Count);writer.Write(native.References.Count);
                foreach(var root in native.Roots.Concat(native.References)){writer.Write(root.Name??"");writer.Write(root.Data==null?-1:ids[root.Data._s]);}
                long total=0;
                for(int i=0;i<nodes.Count;i++)
                {
                    cancellation.ThrowIfCancellationRequested();var node=nodes[i];var data=(byte[])node.GetData().Clone();total+=data.Length;
                    if(total>MeleeSource.MaximumAssetBytes)throw new InvalidDataException("HSD graph verification exceeded its byte budget.");
                    foreach(var reference in node.References.OrderBy(p=>p.Key))
                    {if(reference.Key<0||reference.Key>data.Length-4)throw new InvalidDataException("Native graph pointer lies outside its owning structure.");Array.Clear(data,reference.Key,4);add(reference.Value);}
                    writer.Write(data.Length);writer.Write(data);writer.Write(node.References.Count);
                    foreach(var reference in node.References.OrderBy(p=>p.Key)){writer.Write(reference.Key);writer.Write(reference.Value==null?-1:ids[reference.Value]);}
                }
                writer.Flush();return MeleeSource.Sha256(stream.ToArray());
            }
        }
        public static MeleeWriteCandidate PrepareOriginalSnapshot(MeleeMember master,CancellationToken cancellation=default)
        {
            if(master==null)throw new ArgumentNullException(nameof(master));cancellation.ThrowIfCancellationRequested();
            // Always start with an independent source snapshot, never a previous export.
            byte[] original=master.CopyOriginalBytes();MeleeNative.ValidateDat(original);var baseline=new HSDRawFile(original);string graph=GraphHash(baseline,cancellation);
            try
            {
                byte[] encoded;
                using(var output=new MemoryStream()){baseline.Save(output,bufferAlign:true,optimize:false,trim:false);encoded=output.ToArray();}
                if(encoded.Length<32)throw new InvalidDataException("Native writer produced a truncated header.");
                Buffer.BlockCopy(original,20,encoded,20,12);MeleeNative.ValidateDat(encoded);var reopened=new HSDRawFile(encoded);
                if(GraphHash(reopened,cancellation)!=graph)throw new InvalidDataException("Native rebuild changed an exact graph byte or reference edge.");
                cancellation.ThrowIfCancellationRequested();return new MeleeWriteCandidate(encoded,master.Sha256,graph,true,null);
            }
            catch(Exception error)when(error is InvalidDataException||error is ArgumentException||error is OverflowException)
            {
                // A no-edit export can safely preserve the exact native bytes.
                // This fallback is not evidence that edited native writing qualifies.
                MeleeNative.ValidateDat(original);if(GraphHash(new HSDRawFile(original),cancellation)!=graph)throw;
                return new MeleeWriteCandidate(original,master.Sha256,graph,false,error.Message);
            }
        }
        public static MeleeWriteCandidate PrepareEditedSnapshot(MeleeMember master,Action<HSDRawFile> edit,CancellationToken cancellation=default)
        {
            if(master==null||edit==null)throw new ArgumentNullException();byte[] original=master.CopyOriginalBytes();MeleeNative.ValidateDat(original);
            var native=new HSDRawFile(original);var layout=OriginalLayout(native,original);string originalGraph=GraphHash(native,cancellation);
            edit(native);cancellation.ThrowIfCancellationRequested();string expected=GraphHash(native,cancellation);
            if(expected==originalGraph)return new MeleeWriteCandidate(original,master.Sha256,expected,false,null);
            byte[] encoded=null;string reason=null;
            try
            {
                var reachable=Reachable(native);encoded=(byte[])original.Clone();
                foreach(var node in reachable)
                {
                    if(!layout.TryGetValue(node,out var location)||node.Length!=location.Item2||!node.References.Keys.OrderBy(k=>k).SequenceEqual(location.Item3))throw new InvalidDataException("Native edited graph needs a verified rebuild.");
                    byte[] data=(byte[])node.GetData().Clone();
                    foreach(var reference in node.References)
                    {
                        if(reference.Value==null||!layout.TryGetValue(reference.Value,out var target))throw new InvalidDataException("Native edited reference has no original storage slot.");
                        WriteU32(data,reference.Key,checked((uint)(target.Item1-32)));
                    }
                    Buffer.BlockCopy(data,0,encoded,location.Item1,data.Length);
                }
                MeleeNative.ValidateDat(encoded);
                if(GraphHash(new HSDRawFile(encoded),cancellation)!=expected)throw new InvalidDataException("Edited original-layout bytes did not reopen with the exact expected graph.");
            }
            catch(Exception error)when(error is InvalidDataException||error is ArgumentException||error is OverflowException)
            {
                reason=error.Message;
                encoded=AppendEditedGraph(native,original,layout,cancellation);
                MeleeNative.ValidateDat(encoded);
                if(GraphHash(new HSDRawFile(encoded),cancellation)!=expected)throw new InvalidDataException("Native edited storage did not preserve the complete expected graph. Nothing was exported. Original-layout reason: "+reason);
            }
            cancellation.ThrowIfCancellationRequested();return new MeleeWriteCandidate(encoded,master.Sha256,expected,true,reason);
        }
        // Retain every original data byte/offset. New or resized structures are
        // appended before rebuilt relocation/symbol tables; old storage is opaque,
        // unreachable history, never a substitute for the edited rooted graph.
        // Orphan boundary pointers preserve the native reader's exact structure
        // extents when a former target is no longer referenced by the live graph.
        private static byte[] AppendEditedGraph(HSDRawFile file,byte[] original,Dictionary<HSDStruct,Tuple<int,int,int[]>> layout,CancellationToken cancellation)
        {
            var nodes=Reachable(file);var offsets=new Dictionary<HSDStruct,int>();
            int dataEnd=checked(32+(int)MeleeSource.U32(original,4)),next=dataEnd;
            foreach(var node in nodes)
            {
                cancellation.ThrowIfCancellationRequested();
                if(layout.TryGetValue(node,out var old)&&node.Length==old.Item2)offsets.Add(node,old.Item1);
                else
                {
                    if((node.Length&3)!=0)throw new InvalidDataException("New native structure is not explicitly four-byte padded.");
                    if(node.IsBufferAligned&&(next&31)!=0)throw new InvalidDataException("New native buffer requires a separately verified 32-byte allocation.");
                    offsets.Add(node,next);next=checked(next+node.Length);
                }
            }
            int oldCount=checked((int)MeleeSource.U32(original,8)),oldSymbols=checked(dataEnd+4*oldCount);
            int rootCount=checked((int)(MeleeSource.U32(original,12)+MeleeSource.U32(original,16)));
            if(file.Roots.Count+file.References.Count!=rootCount)throw new InvalidDataException("Native root-table edits require a separately verified writer.");
            var relocations=new HashSet<int>(Enumerable.Range(0,oldCount).Select(i=>checked(32+(int)MeleeSource.U32(original,dataEnd+4*i))));
            foreach(var node in nodes)if(layout.TryGetValue(node,out var old)&&offsets[node]==old.Item1)foreach(int slot in old.Item3)relocations.Remove(old.Item1+slot);
            foreach(var node in nodes)foreach(var edge in node.References)relocations.Add(offsets[node]+edge.Key);
            // Keep boundaries for all original structures, including orphan buffers.
            // The extra self pointer also terminates the last new structure exactly.
            var targets=new HashSet<int>(nodes.SelectMany(n=>n.References.Values).Where(n=>n!=null).Select(n=>offsets[n]));
            foreach(var root in file.Roots.Concat(file.References))if(root.Data!=null)targets.Add(offsets[root.Data._s]);
            var missing=layout.Values.Select(v=>v.Item1).Distinct().Where(at=>!targets.Contains(at)).OrderBy(at=>at).ToList();
            int markers=next;missing.Add(markers);next=checked(next+4*missing.Count);
            if(next>MeleeSource.MaximumAssetBytes)throw new InvalidDataException("Native edited data exceeds its byte budget.");
            byte[] data=new byte[next];Buffer.BlockCopy(original,0,data,0,dataEnd);
            foreach(var node in nodes)
            {
                var bytes=(byte[])node.GetData().Clone();foreach(var edge in node.References)
                {if(edge.Value==null)throw new InvalidDataException("Unexpected null native relocation.");WriteU32(bytes,edge.Key,checked((uint)(offsets[edge.Value]-32)));}
                Buffer.BlockCopy(bytes,0,data,offsets[node],bytes.Length);
            }
            for(int i=0;i<missing.Count;i++){WriteU32(data,markers+4*i,checked((uint)(missing[i]-32)));relocations.Add(markers+4*i);}
            byte[] metadata=original.Skip(oldSymbols).ToArray();int index=0;
            foreach(var root in file.Roots.Concat(file.References))
            {
                int strings=rootCount*8;int nameAt=checked(strings+(int)MeleeSource.U32(metadata,8*index+4));int end=Array.IndexOf(metadata,(byte)0,nameAt);
                if(end<0||Encoding.UTF8.GetString(metadata,nameAt,end-nameAt)!=root.Name)throw new InvalidDataException("Native symbol changes require a separately verified writer.");
                WriteU32(metadata,index++*8,checked((uint)(offsets[root.Data._s]-32)));
            }
            int length=checked(next+relocations.Count*4+metadata.Length);if(length>MeleeSource.MaximumAssetBytes)throw new InvalidDataException("Native edited file exceeds its byte budget.");
            var output=new byte[length];Buffer.BlockCopy(data,0,output,0,data.Length);index=0;
            foreach(int relocation in relocations.OrderBy(at=>at))WriteU32(output,next+4*index++,checked((uint)(relocation-32)));
            Buffer.BlockCopy(metadata,0,output,next+4*relocations.Count,metadata.Length);
            WriteU32(output,0,(uint)length);WriteU32(output,4,(uint)(next-32));WriteU32(output,8,(uint)relocations.Count);return output;
        }
        private static List<HSDStruct> Reachable(HSDRawFile file)
        {
            var nodes=new List<HSDStruct>();var seen=new HashSet<HSDStruct>();
            Action<HSDStruct> add=node=>{if(node!=null&&seen.Add(node)){if(nodes.Count>=2000000)throw new InvalidDataException("Native graph node budget exceeded.");nodes.Add(node);}};
            foreach(var root in file.Roots.Concat(file.References))add(root.Data?._s);
            for(int i=0;i<nodes.Count;i++)foreach(var reference in nodes[i].References.OrderBy(p=>p.Key))add(reference.Value);
            return nodes;
        }
        private static Dictionary<HSDStruct,Tuple<int,int,int[]>> OriginalLayout(HSDRawFile file,byte[] original)
        {
            int symbols=checked(32+(int)MeleeSource.U32(original,4)+4*(int)MeleeSource.U32(original,8));
            var result=new Dictionary<HSDStruct,Tuple<int,int,int[]>>();var queue=new Queue<HSDStruct>();
            Action<HSDStruct,int> add=(node,offset)=>
            {
                if(node==null)return;
                if(result.TryGetValue(node,out var found)){if(found.Item1!=offset)throw new InvalidDataException("Ambiguous native source structure offset.");return;}
                if(offset<32||offset>32L+MeleeSource.U32(original,4)-node.Length)throw new InvalidDataException("Native source structure lies outside its data section.");
                result.Add(node,Tuple.Create(offset,node.Length,node.References.Keys.OrderBy(k=>k).ToArray()));queue.Enqueue(node);
            };
            int index=0;foreach(var root in file.Roots.Concat(file.References))add(root.Data?._s,checked(32+(int)MeleeSource.U32(original,symbols+8*index++)));
            while(queue.Count>0)
            {
                var node=queue.Dequeue();int offset=result[node].Item1;
                foreach(var reference in node.References)add(reference.Value,checked(32+(int)MeleeSource.U32(original,offset+reference.Key)));
            }
            return result;
        }
        private static void WriteU32(byte[] bytes,int offset,uint value)
        {bytes[offset]=(byte)(value>>24);bytes[offset+1]=(byte)(value>>16);bytes[offset+2]=(byte)(value>>8);bytes[offset+3]=(byte)value;}
        public static void Publish(MeleeWriteCandidate candidate,string destination,IEnumerable<string> protectedSources,CancellationToken cancellation=default,Action validateCurrent=null)
        {
            if(candidate==null)throw new ArgumentNullException(nameof(candidate));string path=Path.GetFullPath(destination);MeleeSource.NoLinks(path);
            foreach(string source in protectedSources??new string[0])
            {string full=Path.GetFullPath(source);if(string.Equals(path,full,StringComparison.OrdinalIgnoreCase)||Directory.Exists(full)&&path.StartsWith(full.TrimEnd('\\','/')+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Native game sources are read-only. Choose a separate export destination.");}
            if(File.Exists(path)||Directory.Exists(path))throw new IOException("Choose a new native export destination; existing files remain untouched.");
            string parent=Path.GetDirectoryName(path);if(!Directory.Exists(parent))throw new DirectoryNotFoundException("Choose an existing export directory.");
            string temporary=Path.Combine(parent,".fighterport-"+Guid.NewGuid().ToString("N")+".candidate");
            try
            {
                cancellation.ThrowIfCancellationRequested();using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] bytes=candidate.CopyBytes();output.Write(bytes,0,bytes.Length);output.Flush(true);}
                byte[] read=File.ReadAllBytes(temporary);MeleeNative.ValidateDat(read);
                if(MeleeSource.Sha256(read)!=candidate.Sha256||GraphHash(new HSDRawFile(read),cancellation)!=candidate.GraphSha256)throw new InvalidDataException("The native export candidate failed fresh-disk verification.");
                cancellation.ThrowIfCancellationRequested();validateCurrent?.Invoke();cancellation.ThrowIfCancellationRequested();File.Move(temporary,path);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
