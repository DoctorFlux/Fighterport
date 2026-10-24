using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrawlLib.SSBB.ResourceNodes;

internal sealed class PreservedNative
{
    public string Entry {get;set;} public string Name {get;set;} public string Format {get;set;} public string Sha256 {get;set;} public long Length {get;set;}
}
internal static unsafe partial class Program
{
    static bool TopResource(ResourceNode n)=>n is NW4RAnimationNode||n is MDL0Node||n is TEX0Node||n is PLT0Node||n is ARCEntryNode&&n.Children.Count==0;
    static bool AncestorOf(ResourceNode parent,ResourceNode node){for(var n=node;n!=null;n=n.Parent)if(ReferenceEquals(n,parent))return true;return false;}
    static PreservedNative[] ResourceHashes(ResourceNode root,ResourceNode changed=null,ResourceNode[] edited=null)
    {
        string folder=Path.Combine(Path.GetTempPath(),"FighterPort-native-preservation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            var result=new List<PreservedNative>();foreach(var node in Walk(root).Where(n=>TopResource(n)||n.Parent is BRESGroupNode||n.Parent is ARCNode&&!AncestorOf(n,changed)))
            {
                if(edited!=null&&edited.Any(c=>AncestorOf(node,c)))continue;
                string path=Path.Combine(folder,result.Count+".bin");
                // Native ARC placeholders legitimately have no payload. BrawlLib's generic Export
                // opens a modal "Data was empty!" for them; their actual empty bytes still need hashing.
                if(node.WorkingSource.Length==0&&!node.IsDirty)File.WriteAllBytes(path,new byte[0]);
                else node.Export(path);
                result.Add(new PreservedNative{Entry=Identity(node),Name=node.Name,Format=node.ResourceFileType.ToString(),Sha256=Hash(path),Length=new FileInfo(path).Length});
            }
            return result.ToArray();
        }
        finally{Directory.Delete(folder,true);}
    }
    static PreservedNative[] InsertResource(ResourceNode root,CHR0Node basis,CHR0Node added,string output)
    {
        if(!(root is ARCNode)&&!(root is BRRESNode))throw new NotSupportedException("Adding an animation currently requires an existing PAC/PCS/BRRES motion container, not a standalone CHR0.");
        if(basis.Parent==null)throw new NotSupportedException("No native animation group was resolved.");
        if(basis.Parent.Children.Any(c=>string.Equals(c.Name,added.Name,StringComparison.Ordinal)))throw new InvalidDataException("Destination already contains animation '"+added.Name+"'. Rename/duplicate the edited animation before adding it.");
        var hashes=ResourceHashes(root,basis);basis.Parent.AddChild(added);root.Export(output);return hashes;
    }
    static void VerifyPreserved(ResourceNode fresh,PreservedNative[] expected)
    {
        var found=ResourceHashes(fresh).ToDictionary(p=>p.Entry);foreach(var original in expected)if(!found.TryGetValue(original.Entry,out var actual)||actual.Name!=original.Name||actual.Format!=original.Format||actual.Sha256!=original.Sha256||actual.Length!=original.Length)throw new InvalidDataException("Unrelated native resource changed during container rebuild: "+original.Entry+" / "+original.Name+" ("+original.Format+"). Output is not accepted.");
    }
}
