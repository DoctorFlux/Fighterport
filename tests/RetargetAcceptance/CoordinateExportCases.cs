using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;

internal static partial class Program
{
    private static string CoordinateRootTuple(CHR0EntryNode entry)
    {
        if(entry==null)return "absent";
        int Bits(float value)=>BitConverter.ToInt32(BitConverter.GetBytes(value),0);
        return entry.Name+"|"+entry.UseModelScale+"|"+entry.UseModelRotate+"|"+entry.UseModelTranslate+"|"+entry.ScaleCompensateApply+"|"+entry.ScaleCompensateParent+"|"+entry.ClassicScaleOff+"|"+
            string.Join(";",entry.KeyArrays.Select(a=>string.Join(",",a.Entries.Select(k=>k._index+":"+Bits(k._value)+":"+Bits(k._tangent)))));
    }
    private static int CoordinateNativeExports(string sourcePath,string targetPath,string clipPath,string oldDensePath,string output)
    {
        Directory.CreateDirectory(output);
        var hashes=new[]{sourcePath,targetPath,clipPath,oldDensePath}.Distinct().ToDictionary(p=>p,FighterProject.HashFile);
        var records=new System.Collections.Generic.List<object>();
        using(var sourceRoot=NodeFactory.FromFile(null,sourcePath))using(var targetRoot=NodeFactory.FromFile(null,targetPath))
        using(var clip=NodeFactory.FromFile(null,clipPath) as CHR0Node)using(var old=NodeFactory.FromFile(null,oldDensePath) as CHR0Node)
        {
            var sourceModel=Walk(sourceRoot).OfType<MDL0Node>().OrderByDescending(m=>m.NumVertices).First();
            var targetModel=Walk(targetRoot).OfType<MDL0Node>().OrderByDescending(m=>m.NumVertices).First();
            var source=BrawlRetargeter.Capture(sourceModel);var target=BrawlRetargeter.Capture(targetModel);
            string sourceContent=NativeAnimationOptimization.SourceContentKey(clip),sourceRig=source.Fingerprint,targetRig=target.Fingerprint;
            foreach(bool correction in new[]{true,false})
            using(var result=BrawlRetargeter.Bake(source,target,clip,correction,CancellationToken.None,null,correction?GroundContactMode.Auto:GroundContactMode.Airborne,RetargetKeyframeMode.Dense))
            using(var precise=NativeAnimationOptimization.CopySource(result.Animation))
            {
                string file=Path.Combine(output,correction?"corrected-dense.chr0":"corrected-fk-only.chr0");precise.ExportChecked(file);
                using(var saved=NodeFactory.FromFile(null,file) as CHR0Node)
                {
                    Check(saved!=null&&NativeAnimationOptimization.SourceContentKey(saved)==NativeAnimationOptimization.SourceContentKey(result.Animation),
                        "Corrected coordinate export independently reopens every native key, tangent, semantic flag and metadata field",new{correction});
                    Check(result.Plan.TargetMotionControls.All(i=>CoordinateRootTuple(old.FindChild(target.Bones[i].Name,false) as CHR0EntryNode)==
                        CoordinateRootTuple(saved.FindChild(target.Bones[i].Name,false) as CHR0EntryNode)),
                        "Corrected coordinate calibration preserves every original52 source-owned motion-control key/time/value/tangent/semantic flag exactly",new{correction});
                    Check(saved.FrameCount==clip.FrameCount&&saved.Loop==clip.Loop&&saved.Children.All(n=>target.Bones.Any(b=>b.Name==n.Name)),
                        "Corrected native bake retains source timing/loop state and emits only actual target bones",new{correction});
                    records.Add(new{File=file,Bytes=new FileInfo(file).Length,SHA256=FighterProject.HashFile(file),Correction=correction,
                        Algorithm=HumanoidRetargeter.Algorithm,CalibrationEvidence=result.Plan.CalibrationEvidence,
                        CalibrationDegrees=CoordinateAngle(result.Plan.Calibration,System.Numerics.Quaternion.Identity),
                        result.MaximumLimbResidual,result.ClampedLimbSamples,result.MaximumContactResidual,result.MaximumPelvisCorrection,
                        RootsExactAgainst52=true,FrameCount=saved.FrameCount,Loop=saved.Loop});
                }
            }
            Check(NativeAnimationOptimization.SourceContentKey(clip)==sourceContent&&BrawlRetargeter.SkeletonFingerprint(sourceModel)==sourceRig&&BrawlRetargeter.SkeletonFingerprint(targetModel)==targetRig,
                "Read-only coordinate bake/export preserves source animation content and both authentic native bind rigs");
        }
        Check(hashes.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Coordinate export leaves all original model/animation/reference file bytes immutable");
        File.WriteAllText(Path.Combine(output,"coordinate-export.json"),new JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(new{Passed=true,Checks,Inputs=hashes,Outputs=records,NoRendering=true,NoInputInjection=true}));
        Console.WriteLine("PASS "+Checks.Count+" coordinate export checks");return 0;
    }
}
