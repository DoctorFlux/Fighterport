using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Read-only fresh native decode. Reference identity/ranges come from the
    // fixture and normal fitting dialog, never character/action classification.
    static void ReviewedFitCorpusOracle(MDL0Node model,CHR0Node original,CHR0Node reference,CHR0Node candidate,string[] travel,int join,string file,string output)
    {
        using(var saved=NodeFactory.FromFile(null,file) as CHR0Node)
        {
            Check(saved!=null&&NativeAnimationOptimization.SourceContentKey(saved)==NativeAnimationOptimization.SourceContentKey(candidate),
                "Fresh native fitted export preserves all decoded coefficients, flags and metadata");
            Check(saved.FrameCount==original.FrameCount&&saved.Loop==original.Loop&&saved.Version==original.Version&&saved.NativeScalingRule==original.NativeScalingRule,
                "Fitting preserves duration, loop, CHR0 version and native scaling semantics without appending a reference");
            Check(RecoveryKeys(saved,travel)==RecoveryKeys(original,travel)&&RecoveryRetainedKeysExact(original,saved,0,join),
                "Fresh fitted decode preserves every attack travel coefficient and original beginning/middle key and paired side");
            var times=new SortedSet<float>(Enumerable.Range(0,join*4+1).Select(i=>i/4f));
            for(int frame=0;frame<join;frame++)foreach(float fraction in new[]{.137f,.371f,.623f})times.Add(frame+fraction);
            foreach(var entry in original.Children.OfType<CHR0EntryNode>())foreach(var array in entry.KeyArrays)foreach(var key in array.Entries)
                if(key._index>=0&&key._index<=join)times.Add(key._index);
            Check(times.Count<=2000000,"Independent unchanged-middle native witness schedule stays bounded");
            double position=0,angle=0,scale=0,endPosition=0,endAngle=0,endScale=0;
            void Compare(BrawlLib.Modeling.FrameState a,BrawlLib.Modeling.FrameState b,ref double p,ref double r,ref double s)
            {
                p=Math.Max(p,Math.Sqrt(Enumerable.Range(6,3).Sum(c=>Math.Pow((double)a[c]-b[c],2))));
                r=Math.Max(r,LoopIndependentAngle(Enumerable.Range(3,3).Select(c=>(double)a[c]).ToArray(),Enumerable.Range(3,3).Select(c=>(double)b[c]).ToArray()));
                s=Math.Max(s,Enumerable.Range(0,3).Max(c=>Math.Abs((double)a[c]-b[c])));
            }
            foreach(float time in times)foreach(var bone in model.AllBones)
                Compare(RecoverySample(saved,bone,time),RecoverySample(original,bone,time),ref position,ref angle,ref scale);
            foreach(var bone in model.AllBones)
                Compare(RecoverySample(saved,bone,saved.FrameCount-1),RecoverySample(travel.Contains(bone.Name)?original:reference,bone,travel.Contains(bone.Name)?original.FrameCount-1:0),ref endPosition,ref endAngle,ref endScale);
            bool passed=position<=LoopPositionTolerance&&angle<=LoopOrientationTolerance&&scale<=LoopScaleTolerance&&
                endPosition<=LoopPositionTolerance&&endAngle<=LoopOrientationTolerance&&endScale<=LoopScaleTolerance;
            var rig=BrawlRetargeter.Capture(model);var feet=rig.Rig.Surfaces.Where(p=>p.Key.EndsWith(".foot",StringComparison.Ordinal)).ToArray();
            var recovery=new List<object>();
            foreach(float time in Enumerable.Range(join*4,(saved.FrameCount-1-join)*4+1).Select(i=>i/4f))
            {
                var before=BrawlRetargeter.Sample(rig,original,time);var after=BrawlRetargeter.Sample(rig,saved,time);
                recovery.Add(new{Time=time,Feet=feet.Select(p=>new{Role=p.Key,Before=p.Value.Points.Min(point=>System.Numerics.Vector3.Dot(point.Evaluate(before),rig.Rig.GroundNormal))-rig.Rig.GroundHeight,
                    After=p.Value.Points.Min(point=>System.Numerics.Vector3.Dot(point.Evaluate(after),rig.Rig.GroundNormal))-rig.Rig.GroundHeight}).ToArray()});
            }
            File.WriteAllText(Path.Combine(output,"fit-native-oracle.json"),new JavaScriptSerializer{MaxJsonLength=16*1024*1024}.Serialize(new{Passed=passed,Reference=reference.Name,ReferenceFrame=1,
                Frames=saved.FrameCount,JoinNativeTime=join,MiddleSamples=times.Count,MaximumMiddleTranslation=position,MaximumMiddleOrientation=angle,MaximumMiddleScale=scale,
                MaximumEndpointTranslation=endPosition,MaximumEndpointOrientation=endAngle,MaximumEndpointScale=endScale,LoopPositionTolerance,LoopOrientationTolerance,LoopScaleTolerance,
                FeetDiagnosticOnly=true,ReviewedFloor=false,ContactAcceptance=false,NativeBindPlane=rig.Rig.GroundHeight,Recovery=recovery,AllSkinInfluences=true}));
            Check(passed,"Independent fresh native samples preserve the pre-recovery motion and reach the exact target first body pose under attack-owned travel within unchanged native comparison limits");
        }
    }
}
