using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

internal static partial class Program
{
    static int NativeMemberAudit(string motion,string name,string output)
    {
        Directory.CreateDirectory(output);string original=FighterProject.HashFile(motion);
        using(var root=NodeFactory.FromFile(null,motion))
        {
            var clip=Walk(root).OfType<CHR0Node>().Single(c=>c.Name==name);
            var source=clip.WorkingUncompressed;
            if(source.Length<0x28||source.Length>16*1024*1024)throw new InvalidDataException("Unbounded diagnostic resource.");
            var bytes=new byte[source.Length];Marshal.Copy((IntPtr)source.Address,bytes,0,bytes.Length);
            File.WriteAllBytes(Path.Combine(output,"original-resource.bin"),bytes);
            var rows=clip.Children.OfType<CHR0EntryNode>().Select(entry=>{
                var capture=IndexedCHR0EntryStorage.CaptureForWrite(entry);
                return new{entry.Name,Offset=(long)(IntPtr)entry.WorkingUncompressed.Address-(long)(IntPtr)source.Address,
                    capture.Accepted,capture.Diagnostic,Keys=entry.KeyArrays.Select(a=>a.KeyCount).ToArray()};
            }).ToArray();
            File.WriteAllText(Path.Combine(output,"native-member-audit.json"),new JavaScriptSerializer{MaxJsonLength=16*1024*1024}.Serialize(new{
                clip.Name,clip.FrameCount,clip.Version,clip.Loop,Bytes=bytes.Length,OriginalContainerSha256=original,Rows=rows}));
            string content=NativeAnimationOptimization.SourceContentKey(clip);
            using(var snapshot=NativeAnimationOptimization.CopySource(clip))
            {
                string path=Path.Combine(output,"source-snapshot.chr0");snapshot.ExportChecked(path);
                using(var reopened=NodeFactory.FromFile(null,path) as CHR0Node)
                    Check(reopened!=null&&NativeAnimationOptimization.SourceContentKey(reopened)==content,"Read-only original source snapshot freshly reopens with exact native coefficients, timing and flags");
            }
        }
        Check(FighterProject.HashFile(motion)==original,"Read-only native member audit leaves original bytes unchanged");return 0;
    }
}
