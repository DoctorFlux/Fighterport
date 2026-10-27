using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

internal static partial class Program
{
    static void NativeSourceKeyFallbackChecks(MDL0Node model)
    {
        var rig=BrawlRetargeter.Capture(model);var plan=new HumanoidRetargeter(rig.Rig,rig.Rig);
        string body=rig.Bones[rig.Rig.Roles["pelvis"]].Name,control=rig.Bones[plan.TargetMotionControls.First()].Name;
        var meshes=model.Objects.OfType<MDL0ObjectNode>().Where(o=>o.Vertices!=null).Select(o=>o.Vertices).ToArray();
        var original=meshes.Select(v=>v.ToArray()).ToArray();
        try
        {
            foreach(var vertices in meshes)vertices.Clear();
            Check(NativeAnimationOptimization.GetBindBoundsDiagonal(model)==0,"Fallback fixture has no bind-geometry scale and must use exact coefficient identity rather than approximate quality claims");
            using(var dense=SourceKeyQualityDense(rig,3))
            using(var source=SourceKeyQualitySource(rig,3,new[]{body}))
            {
                string content=NativeAnimationOptimization.ContentKey(dense);
                foreach(var mode in new[]{RetargetKeyframeMode.SourceBoneTimes,RetargetKeyframeMode.SourceChannelTimes})
                using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan})
                {
                    RetargetKeyframes.Prepare(result,rig,rig,source,mode,CancellationToken.None,null);
                    Check(result.KeyframeReport.DenseFallback&&result.KeyframeReport.ScalarCertified&&result.KeyframeReport.QualityVerified&&
                        result.KeyframeReport.Samples==0&&result.KeyframeReport.LengthScale==0&&NativeAnimationOptimization.ContentKey(result.Animation)==content,
                        mode+": missing geometry returns an independently encoded exact dense identity with zero invented native samples");
                }
                foreach(string bone in new[]{body,control})foreach(bool paired in new[]{false,true})
                foreach(bool tangent in new[]{false,true})foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
                using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan})
                {
                    var track=(CHR0EntryNode)result.Animation.FindChild(bone,false);
                    if(paired)track.Keyframes.SetFrameValue(3,1,track.KeyArrays[3].GetKeyframe(1)._value,true)._tangent=0;
                    var key=track.KeyArrays[3].GetKeyframe(1);if(tangent)key._tangent=invalid;else key._value=invalid;
                    string before=NativeAnimationOptimization.ContentKey(result.Animation);bool rejected=false;
                    try{RetargetKeyframes.Prepare(result,rig,rig,source,RetargetKeyframeMode.SourceChannelTimes,CancellationToken.None,null);}
                    catch(System.IO.InvalidDataException error){rejected=error.Message.Contains("non-finite");}
                    Check(rejected&&result.KeyframeReport==null&&result.DenseAnimation==null&&NativeAnimationOptimization.ContentKey(result.Animation)==before,
                        "Exact identity fallback rejects non-finite "+(tangent?"tangent":"value")+" before claiming valid motion / protected="+(bone==control)+" / paired="+paired+" / "+invalid);
                }
                foreach(int invalid in new[]{-2,3,int.MaxValue})
                using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan})
                {
                    var track=(CHR0EntryNode)result.Animation.FindChild(body,false);track.KeyArrays[3].Entries.Last()._index=invalid;
                    bool rejected=false;
                    try{RetargetKeyframes.Prepare(result,rig,rig,source,RetargetKeyframeMode.SourceBoneTimes,CancellationToken.None,null);}
                    catch(System.IO.InvalidDataException error){rejected=error.Message.Contains("timing");}
                    Check(rejected&&result.KeyframeReport==null,"Exact identity fallback rejects an out-of-range or unordered native knot before encoding / "+invalid);
                }
                using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan})
                {
                    var track=(CHR0EntryNode)result.Animation.FindChild(body,false);
                    track.Keyframes.SetFrameValue(3,1,0,true)._tangent=0;track.Keyframes.SetFrameValue(3,1,0,true)._tangent=0;
                    bool rejected=false;
                    try{RetargetKeyframes.Prepare(result,rig,rig,source,RetargetKeyframeMode.SourceBoneTimes,CancellationToken.None,null);}
                    catch(System.IO.InvalidDataException error){rejected=error.Message.Contains("paired-key sides");}
                    Check(rejected&&result.KeyframeReport==null,"Exact identity fallback rejects more than two authored knot sides instead of certifying malformed native data");
                }
            }
        }
        finally
        {
            for(int i=0;i<meshes.Length;i++){meshes[i].Clear();meshes[i].AddRange(original[i]);}
            model.ApplyCHR(null,0);
        }
        Check(meshes.Select((v,i)=>v.SequenceEqual(original[i])).All(same=>same),"Fallback numeric tests restore every original native vertex reference and model bind pose");
    }
}
