using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Threading.Tasks;

internal static partial class Program
{
    static Task ReviewedContactInventory(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Contact fixture inventory requires the private desktop.");
        string Argument(string flag){int i=Array.IndexOf(args,flag);if(i<0||i+1>=args.Length)throw new ArgumentException("Missing inventory input "+flag);return args[i+1];}
        var wanted=new[]{"Wait1","AttackS3S","AttackS3S1","CliffClimbQuick","CliffClimbSlow","CliffAttackQuick","CliffAttackSlow","DownStandU","DownStandD","DownAttackU","DownAttackD"};
        var files=new[]{args[0],args[1],Argument("--retarget-source"),Argument("--target-motion")};
        var originals=files.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(p=>p,FighterProject.HashFile,StringComparer.OrdinalIgnoreCase);
        var catalogues=new List<object>();
        string Hash(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
        string Identity(ResourceNode node)=>node.Parent==null?"root":Identity(node.Parent)+"/"+node.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        void Read(string modelPath,string motionPath,string direction)
        {
            using(var root=NodeFactory.FromFile(null,modelPath))using(var motion=NodeFactory.FromFile(null,motionPath))
            {
                var models=Walk(root).OfType<MDL0Node>().Select(m=>new{Entry=Identity(m),m.Name,Rig=BrawlRetargeter.SkeletonFingerprint(m),Bones=m._linker.BoneCache.Length}).ToArray();
                var clips=Walk(motion).OfType<CHR0Node>().Where(c=>wanted.Contains(c.Name,StringComparer.Ordinal)).ToArray();
                var rows=new List<object>();
                foreach(var clip in clips)
                {
                    string member=Identity(clip);string key=Hash(originals[motionPath]+"|"+member);
                    string path=Path.Combine(output,direction+"-"+clip.Name+"-"+key.Substring(0,12)+".chr0");
                    string snapshotFailure=null;
                    try{using(var copy=NativeAnimationOptimization.CopySource(clip))copy.ExportChecked(path);}
                    catch(Exception error)when(error is InvalidDataException||error is NotSupportedException){snapshotFailure=error.Message;}
                    string content=null;
                    if(snapshotFailure==null)content=NativeAnimationOptimization.SourceContentKey(clip);
                    rows.Add(new{clip.Name,Entry=member,OriginalContainerSha256=originals[motionPath],MemberIdentitySha256=key,
                        clip.FrameCount,clip.Loop,clip.Version,clip.NativeScalingRule,SnapshotFile=snapshotFailure==null?Path.GetFileName(path):null,
                        SnapshotSha256=snapshotFailure==null?FighterProject.HashFile(path):null,SnapshotBytes=snapshotFailure==null?(long?)new FileInfo(path).Length:null,SnapshotFailure=snapshotFailure,
                        NativeContentKey=content,Tracks=clip.Children.Select(c=>c.Name).ToArray()});
                }
                catalogues.Add(new{Direction=direction,ModelContainerSha256=originals[modelPath],MotionContainerSha256=originals[motionPath],Models=models,Clips=rows,
                    Missing=wanted.Where(name=>!clips.Any(c=>c.Name==name)).ToArray(),Ambiguous=wanted.Where(name=>clips.Count(c=>c.Name==name)>1).ToArray()});
                Check(models.Length>0,"Contact inventory records authentic model identities without picking an ambiguous rig");
                Check(clips.Any(c=>c.Name=="Wait1"),"Contact inventory records the character's actual Wait1 reference");
            }
        }
        Read(files[0],files[1],"target");Read(files[2],files[3],"donor");
        foreach(var input in originals)Check(FighterProject.HashFile(input.Key)==input.Value,"Contact inventory preserves original input bytes");
        File.WriteAllText(Path.Combine(output,"inventory.json"),new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=100}.Serialize(new{
            Schema="fighterport-contact-fixture-inventory-1",Cases=catalogues,Originals=originals,
            Note="Member identity hashes bind exact native index paths to original container bytes. Snapshot hashes describe independent precise exports; neither is presented as a raw standalone original-member hash."}));
        return Task.CompletedTask;
    }
}
