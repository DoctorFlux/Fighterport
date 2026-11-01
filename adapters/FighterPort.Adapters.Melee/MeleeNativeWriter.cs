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
        public static void Publish(MeleeWriteCandidate candidate,string destination,IEnumerable<string> protectedSources,CancellationToken cancellation=default)
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
                cancellation.ThrowIfCancellationRequested();File.Move(temporary,path);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
