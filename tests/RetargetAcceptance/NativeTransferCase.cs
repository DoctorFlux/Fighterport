using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Read-only case runner: fixture identity selects data, never a solver policy.
    sealed class TransferFixture
    {
        public string Schema,SourceClipName,SourceNativeMember,SourceClipSha256,SourceModelSha256,TargetModelSha256,SourceRigFingerprint,TargetRigFingerprint;
        public int SourceFrames,SourceVersion;public bool SourceLoop;
    }
    static int NativeTransferCase(string sourcePath,string targetPath,string motionPath,string manifest,string output)
    {
        Process.GetCurrentProcess().PriorityClass=ProcessPriorityClass.BelowNormal;
        if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new InvalidDataException("Use a fresh transfer evidence directory.");
        Directory.CreateDirectory(output);
        var json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=100};
        if(new FileInfo(manifest).Length>16384)throw new InvalidDataException("Unbounded fixture manifest.");
        string text=File.ReadAllText(manifest);
        var f=json.Deserialize<TransferFixture>(text);
        var hashes=new[]{sourcePath,targetPath,motionPath,manifest}.Distinct().ToDictionary(p=>p,FighterProject.HashFile);
        if(f.Schema!="fighterport-authentic-export-fixture-1"||!Same(hashes[sourcePath],f.SourceModelSha256)||!Same(hashes[targetPath],f.TargetModelSha256)||!Same(hashes[motionPath],f.SourceClipSha256))throw new InvalidDataException("Exact fixture input hashes differ.");
        using(var s=NodeFactory.FromFile(null,sourcePath))using(var t=NodeFactory.FromFile(null,targetPath))using(var m=NodeFactory.FromFile(null,motionPath))
        {
            MDL0Node Exact(ResourceNode root,string identity)=>Walk(root).OfType<MDL0Node>().Single(model=>Same(BrawlRetargeter.SkeletonFingerprint(model),identity));
            var sm=Exact(s,f.SourceRigFingerprint);var tm=Exact(t,f.TargetRigFingerprint);
            var source=BrawlRetargeter.Capture(sm);var target=BrawlRetargeter.Capture(tm);
            string Member(ResourceNode n)=>n.Parent==null?"root":Member(n.Parent)+"/"+n.Index;
            var clip=Walk(m).OfType<CHR0Node>().Single(c=>c.Name==f.SourceClipName&&Member(c)==f.SourceNativeMember);
            if(clip.FrameCount!=f.SourceFrames||clip.Version!=f.SourceVersion||clip.Loop!=f.SourceLoop)throw new InvalidDataException("Exact native member timing differs.");
            string sourceState=NativeOwnership(sm),targetState=NativeOwnership(tm),sourceContent=NativeAnimationOptimization.SourceContentKey(clip);
            string stage="master",failure=null;bool master=false,channel=false,roots=false;RetargetKeyframeReport quality=null;string diagnostic=null;
            var watch=Stopwatch.StartNew();long masterMs=0,channelMs=0;
            try
            {
                using(var result=BrawlRetargeter.Bake(source,target,clip,groundMode:GroundContactMode.Auto,progress:(p,message)=>Console.WriteLine(watch.ElapsedMilliseconds+" ms "+p+"% "+message)))
                {
                    result.Animation.ExportChecked(Path.Combine(output,"dense-target.chr0"));masterMs=watch.ElapsedMilliseconds;
                    using(var fresh=NodeFactory.FromFile(null,Path.Combine(output,"dense-target.chr0")) as CHR0Node)
                        if(fresh==null||NativeAnimationOptimization.ContentKey(fresh)!=NativeAnimationOptimization.ContentKey(result.Animation))throw new InvalidDataException("Full-quality target changed on fresh native reopen.");
                    master=true;
                    var controls=result.Plan.TargetMotionControls.Select(i=>target.Bones[i].Name).ToArray();
                    string protection=ProtectedTransferKeys(result.Animation,controls);
                    File.WriteAllText(Path.Combine(output,"master-report.txt"),result.OptimizationReport??"");
                    stage="source-channel";
                    RetargetKeyframes.Prepare(result,source,target,clip,RetargetKeyframeMode.SourceChannelTimes,CancellationToken.None,null);
                    channelMs=watch.ElapsedMilliseconds-masterMs;quality=result.KeyframeReport;
                    if(quality==null||!quality.ScalarCertified||!quality.QualityVerified)throw new InvalidDataException("Default source-channel qualification is incomplete.");
                    roots=protection==ProtectedTransferKeys(result.Animation,controls);
                    if(!roots)throw new InvalidDataException("Protected motion controls changed in default representation.");
                    string file=Path.Combine(output,"current-target.chr0");result.Animation.ExportChecked(file);
                    using(var fresh=NodeFactory.FromFile(null,file) as CHR0Node)
                        if(fresh==null||NativeAnimationOptimization.ContentKey(fresh)!=NativeAnimationOptimization.ContentKey(result.Animation))throw new InvalidDataException("Default native export changed on fresh reopen.");
                    channel=true;
                }
            }
            catch(Exception error){failure=error.ToString();if(error.Data.Contains("NativeCurveWitness"))diagnostic=Convert.ToString(error.Data["NativeCurveWitness"]);}
            bool state=sourceState==NativeOwnership(sm)&&targetState==NativeOwnership(tm)&&sourceContent==NativeAnimationOptimization.SourceContentKey(clip);
            bool originals=hashes.All(p=>FighterProject.HashFile(p.Key)==p.Value);
            File.WriteAllText(Path.Combine(output,"case.json"),json.Serialize(new{MasterReady=master,DefaultReady=channel,ProtectedControlsExact=roots,NativeOwnershipUnchanged=state,OriginalFilesUnchanged=originals,
                FailureStage=failure==null?null:stage,Failure=failure,Diagnostic=diagnostic,MasterMilliseconds=masterMs,DefaultPreparationMilliseconds=channelMs,Milliseconds=watch.ElapsedMilliseconds,
                SourceModelName=sm.Name,TargetModelName=tm.Name,SourceModelSha256=hashes[sourcePath],TargetModelSha256=hashes[targetPath],SourceMotionSha256=hashes[motionPath],
                SourceRig=source.Fingerprint,TargetRig=target.Fingerprint,SourceClip=clip.Name,SourceMember=Member(clip),clip.FrameCount,clip.Loop,clip.Version,
                SourceGroundHeight=source.Rig.GroundHeight,TargetGroundHeight=target.Rig.GroundHeight,GroundMode="Auto",KeyMode="SourceChannelTimes",Quality=quality,Checks}));
            if(!state||!originals)throw new InvalidDataException("Read-only transfer changed source, native binds, inverse binds, vertices or skin weights.");
            return master&&channel?0:1;
        }
    }
    static bool Same(string a,string b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
    static string ProtectedTransferKeys(CHR0Node clip,string[] controls)
    {
        var data=clip.Children.OfType<CHR0EntryNode>().Where(e=>controls.Contains(e.Name)).Select(e=>new{e.Name,e.UseModelScale,e.UseModelRotate,e.UseModelTranslate,e.ScaleCompensateApply,e.ScaleCompensateParent,e.ClassicScaleOff,
            Keys=e.KeyArrays.Select(a=>a.Entries.Select(k=>new[]{BitsTransfer(k._index),BitsTransfer(k._value),BitsTransfer(k._tangent)}).ToArray()).ToArray()}).ToArray();
        return new JavaScriptSerializer().Serialize(data);
    }
    static int BitsTransfer(float f)=>BitConverter.ToInt32(BitConverter.GetBytes(f),0);
    static string NativeOwnership(MDL0Node model)
    {
        using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes))using(var sha=SHA256.Create())
        {
            foreach(var bone in model.AllBones)
            {
                writer.Write(bone.Name);writer.Write((bone.Parent as MDL0BoneNode)?.Name??"");
                for(int i=0;i<9;i++)writer.Write(BitsTransfer(bone.BindState[i]));
                for(int i=0;i<16;i++){writer.Write(BitsTransfer(bone.BindMatrix[i]));writer.Write(BitsTransfer(bone.InverseBindMatrix[i]));}
            }
            foreach(var obj in model.PolygonList.OfType<MDL0ObjectNode>())
            {
                writer.Write(obj.Name);writer.Write(obj.Vertices.Count);
                foreach(var vertex in obj.Vertices)
                {
                    writer.Write(BitsTransfer(vertex.Position._x));writer.Write(BitsTransfer(vertex.Position._y));writer.Write(BitsTransfer(vertex.Position._z));
                    var weights=vertex.GetMatrixNode()?.Weights;writer.Write(weights?.Count??-1);
                    if(weights!=null)foreach(var weight in weights){writer.Write((weight.Bone as MDL0BoneNode)?.Name??"");writer.Write(BitsTransfer(weight.Weight));}
                }
            }
            writer.Flush();return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-","");
        }
    }
}
