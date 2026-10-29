using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal static partial class Program
{
    static async Task NativeInterpolatedKeys(MDL0Node model,string output)
    {
        var rig=BrawlRetargeter.Capture(model);var plan=new HumanoidRetargeter(rig.Rig,rig.Rig);
        string body=rig.Bones[rig.Rig.Roles["pelvis"]].Name;
        string root=rig.Bones[plan.TargetMotionControls.First()].Name;
        var json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=200};
        string stage=Path.Combine(output,"native-interpolated-keys");Directory.CreateDirectory(stage);
        model.Export(Path.Combine(stage,"optimizer-target.mdl0"));
        string skeleton=BrawlRetargeter.SkeletonFingerprint(model),geometry=DirtyModelMemory(model);
        using(var dense=new CHR0Node{Name="Independent dense native interpolation",FrameCount=17,Version=5,OriginalPath="procedural/interpolation"})
        {
            var target=dense.CreateEntry(body);target.UseModelRotate=false;target.Keyframes.PreserveInterpolation=true;
            var motion=dense.CreateEntry(root);motion.UseModelTranslate=false;motion.Keyframes.PreserveInterpolation=true;
            for(int at=0;at<=16;at++)
            {
                // One independently specified cubic and one complete 720-degree
                // turn. Values and derivatives are exactly dyadic/integer.
                target.Keyframes.SetFrameValue(3,at,45*at,true)._tangent=45;
                target.Keyframes.SetFrameValue(4,at,at*at*at/4096f,true)._tangent=3*at*at/4096f;
                target.Keyframes.SetFrameValue(5,at,13.25f,true)._tangent=0;
                motion.Keyframes.SetFrameValue(6,at,at/8f,true)._tangent=.125f;
            }
            // Native translation storage has three group slots: an authored X
            // group requires canonical fixed Y/Z values, not absent axes.
            // Body scale remains wholly absent and is checked independently.
            motion.Keyframes.SetFrameValue(7,0,0,true)._tangent=0;
            motion.Keyframes.SetFrameValue(8,0,0,true)._tangent=0;
            PreciseCHR0EntryNode.Promote(dense);
            using(var source=NativeAnimationOptimization.Copy(dense))
            {
                string before=NativeAnimationOptimization.ContentKey(dense),sourceBefore=NativeAnimationOptimization.ContentKey(source);
                Check(source.Children.OfType<CHR0EntryNode>().Single(e=>e.Name==body).KeyArrays[4].KeyCount==17,
                    "Interpolation oracle: the donor itself has a cubic body key on every one of its 17 frames");
                foreach(var mode in new[]{RetargetKeyframeMode.SourceChannelTimes,RetargetKeyframeMode.Interpolated})
                using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan})
                {
                    RetargetKeyframes.Prepare(result,rig,rig,source,mode,CancellationToken.None,null);
                    var bodyResult=(CHR0EntryNode)result.Animation.FindChild(body,false);
                    Check(bodyResult.KeyArrays[4].KeyCount==(mode==RetargetKeyframeMode.Interpolated?2:17),
                        mode+": only generic interpolation may discard redundant per-frame donor schedule keys");
                    if(mode==RetargetKeyframeMode.Interpolated)
                    {
                        Check(bodyResult.KeyArrays[3].KeyCount==2&&bodyResult.KeyArrays[5].KeyCount==1&&bodyResult.KeyArrays[0].KeyCount==0,
                            "Interpolated keys: a 720-degree line uses endpoints, constant rotation uses one key, and absent scale stays absent");
                        double maximum=0;
                        for(int n=0;n<=4096;n++)
                        {
                            float time=n/256f;
                            maximum=Math.Max(maximum,Math.Abs(bodyResult.GetFrameValue(4,time)-(double)time*time*time/4096));
                            maximum=Math.Max(maximum,Math.Abs(bodyResult.GetFrameValue(3,time)-45d*time));
                        }
                        Check(maximum<=.00001&&bodyResult.GetFrameValue(3,16)-bodyResult.GetFrameValue(3,0)==720,
                            "Interpolated keys: 4097 withheld native fractions match the independent cubic/linear oracle without Euler wrapping");
                        Check(result.KeyframeReport.Version==RetargetKeyframes.InterpolatedVersion&&result.KeyframeReport.ScalarCertified&&
                            result.KeyframeReport.QualityVerified&&result.KeyframeReport.ResultBytes<result.KeyframeReport.DenseBytes,
                            "Interpolated keys: continuous/native checks, distinct receipt and real serialized savings qualify the independent result");
                        Check(result.Animation.Version==5&&result.Animation.FrameCount==17&&!result.Animation.Loop&&result.Animation.OriginalPath==dense.OriginalPath,
                            "Interpolated keys: native version, metadata, duration and nonlooping terminal behavior remain");
                        var provenance=new RetargetProvenance{FloorContactsEnabled=true,PlantedFootSamples=7};
                        provenance.RecordKeyframeEvidence(result);
                        var restored=json.Deserialize<RetargetProvenance>(json.Serialize(provenance));restored.ValidateKeyframeEvidence();
                        Check(restored.KeyframeMode==mode&&restored.ReadKeyframeReport().Version==RetargetKeyframes.InterpolatedVersion&&
                            !restored.FloorContactsEnabled&&restored.DenseReferenceStatistics.PlantedFootSamples==7,
                            "Interpolated keys: additive provenance roundtrip preserves mode/receipt while dense contacts remain historical");
                        var receipt=result.KeyframeReport;string version=receipt.Version;
                        try{receipt.Version="source-key-quality-v2";bool rejected=false;try{receipt.Validate();}catch(InvalidDataException){rejected=true;}
                            Check(rejected,"Interpolated keys: a legacy source-schedule receipt cannot masquerade as interpolation evidence");}
                        finally{receipt.Version=version;}
                        SourceKeyArtifactExport(result.Animation,Path.Combine(stage,"interpolated-oracle.chr0"));
                        File.WriteAllText(Path.Combine(stage,"interpolated-oracle.json"),json.Serialize(result.KeyframeReport));
                    }
                    SourceKeysSameTrack((CHR0EntryNode)dense.FindChild(root,false),(CHR0EntryNode)result.Animation.FindChild(root,false),
                        mode+": protected own-source root retains every original frame, side, value and tangent");
                }
                await NativeInterpolatedOptimizerOracle(model,dense,body,root,stage);
                Check(NativeAnimationOptimization.ContentKey(dense)==before&&NativeAnimationOptimization.ContentKey(source)==sourceBefore,
                    "Interpolation preparation preserves both fully keyed original master and donor");
                using(var cancel=new CancellationTokenSource())
                using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan})
                {
                    bool cancelled=false;
                    try{RetargetKeyframes.Prepare(result,rig,rig,source,RetargetKeyframeMode.Interpolated,cancel.Token,(p,m)=>{if(p>=92)cancel.Cancel();});}
                    catch(OperationCanceledException){cancelled=true;}
                    Check(cancelled&&result.KeyframeReport==null&&result.DenseAnimation==null&&NativeAnimationOptimization.ContentKey(result.Animation)==before,
                        "Interpolated keys: cancellation publishes no partial replacement of the dense result");
                }
            }
        }
        using(var dense=SourceKeyQualityDense(rig,3))
        using(var source=NativeAnimationOptimization.Copy(dense))
        {
            var track=(CHR0EntryNode)dense.FindChild(body,false);
            track.KeyArrays[3].GetKeyframe(1)._tangent=8;
            float bind=rig.Bones[rig.Rig.Roles["pelvis"]].BindState[3];
            Check(Math.Abs(track.GetFrameValue(3,.5f)-bind+1)<.000001,
                "Interpolation pulse oracle: equal values with nonzero tangents give minus one degree at half a frame");
            using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan})
            {
                RetargetKeyframes.Prepare(result,rig,rig,source,RetargetKeyframeMode.Interpolated,CancellationToken.None,null);
                var outputTrack=(CHR0EntryNode)result.Animation.FindChild(body,false);
                Check(outputTrack.KeyArrays[3].KeyCount>=3&&Math.Abs(outputTrack.GetFrameValue(3,.5f)-bind+1)<.00001,
                    "Interpolated keys: interior Hermite motion cannot be mistaken for a constant or an endpoint-only hold");
            }
            track.Keyframes.SetFrameValue(3,1,bind+2,true)._tangent=3;
            using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan})
            {
                RetargetKeyframes.Prepare(result,rig,rig,source,RetargetKeyframeMode.Interpolated,CancellationToken.None,null);
                var outputTrack=(CHR0EntryNode)result.Animation.FindChild(body,false);
                Check(outputTrack.KeyArrays[3].Entries.Count(k=>k._index==1)==2&&outputTrack.KeyArrays[3].Entries.Select(k=>new[]{ReplayFloatBits(k._index),ReplayFloatBits(k._value),ReplayFloatBits(k._tangent)})
                    .SelectMany(k=>k).SequenceEqual(track.KeyArrays[3].Entries.SelectMany(k=>new[]{ReplayFloatBits(k._index),ReplayFloatBits(k._value),ReplayFloatBits(k._tangent)})),
                    "Interpolated keys: unknown paired sides and actual discontinuities keep exact native tuples");
            }
            track.KeyArrays[4].GetKeyframe(1)._tangent=BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue),0);
            using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan})
            {
                RetargetKeyframes.Prepare(result,rig,rig,source,RetargetKeyframeMode.Interpolated,CancellationToken.None,null);
                var preserved=((CHR0EntryNode)result.Animation.FindChild(body,false)).KeyArrays[4];
                Check(preserved.KeyCount==3&&ReplayFloatBits(preserved.GetKeyframe(1)._tangent)==int.MinValue,
                    "Interpolated keys: signed-zero authored evidence retains its exact native knot and tangent bits");
            }
            foreach(float invalid in new[]{float.NaN,float.PositiveInfinity})
            using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan})
            {
                ((CHR0EntryNode)result.Animation.FindChild(body,false)).KeyArrays[3].Entries.First()._tangent=invalid;
                bool rejected=false;try{RetargetKeyframes.Prepare(result,rig,rig,source,RetargetKeyframeMode.Interpolated,CancellationToken.None,null);}catch(InvalidDataException){rejected=true;}
                Check(rejected&&result.DenseAnimation==null&&result.KeyframeReport==null,
                    "Interpolated keys: nonfinite authored tangents reject before any identity fallback or publication");
            }
        }
        Check(BrawlRetargeter.SkeletonFingerprint(model)==skeleton&&DirtyModelMemory(model)==geometry,
            "Interpolation fixtures preserve authentic hierarchy/binds and every original skin coefficient");
    }

    static async Task NativeInterpolatedOptimizerOracle(MDL0Node model,CHR0Node dense,string body,string root,string stage)
    {
        var json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=200};
        string modelPath=Path.Combine(stage,"optimizer-target.mdl0"),input=Path.Combine(stage,"optimizer-input.json"),output=Path.Combine(stage,"optimizer-output.json");
        File.WriteAllText(input,json.Serialize(new {dense.Name,SampleCount=dense.FrameCount,dense.Loop,HighQuality=false,
            LengthScale=NativeAnimationOptimization.GetBindBoundsDiagonal(model),Tracks=RefitNativeWire(dense),
            ExactBones=new[]{root},ProtectedBones=new[]{root},MandatoryFrames=new int[0],ProtectedRanges=new object[0],ProtectionKnown=true,
            MaximumWork=2000000,MaximumCandidateSpans=100000,EnableSpanRefitting=true}));
        using(var client=new AnimationControlClient())
        {
            var reply=await client.Send(new{Operation="animation-optimize-prepare",Model=modelPath,Optimization=new{InputPath=input,OutputPath=output}});
            Check(reply.OptimizationPath==output,"Interpolation optimizer oracle: actual Core worker returns the independent Exact proposal");
            var result=json.Deserialize<RefitNativeOutput>(File.ReadAllText(output));var tracks=RefitNativeTracks(result.Tracks);
            Check(tracks.Single(t=>t.Bone==body).Channels[4].Length==2&&result.Report.AcceptedRefitSpans==0,
                "Interpolation optimizer oracle: Exact cleanup removes 15 redundant cubic frame keys through native Hermite equivalence, with no approximate tangent fitting");
            using(var verified=NativeAnimationOptimization.Prepare(model,dense,tracks,new NativeAnimationOptimizationOptions{ExactOnly=true,
                TargetBones=model.AllBones.Select(b=>b.Name).ToArray(),ProtectedBones=new[]{root},ExactBones=new[]{root},ProbeFrames=result.Report.ProbeFrames},stage))
            {
                Check(verified.AfterBytes<verified.BeforeBytes&&verified.Candidate.Children.OfType<CHR0EntryNode>().Single(e=>e.Name==body).KeyArrays[4].KeyCount==2,
                    "Interpolation optimizer oracle: real native encoding and fresh decode retain two cubic endpoint keys and a smaller file");
                SourceKeysSameTrack((CHR0EntryNode)dense.FindChild(root,false),(CHR0EntryNode)verified.Candidate.FindChild(root,false),
                    "Interpolation optimizer oracle: exact protected root tuples remain while ordinary body channels interpolate");
                SourceKeyArtifactExport(verified.Candidate,Path.Combine(stage,"optimized-oracle.chr0"));
                File.WriteAllText(Path.Combine(stage,"optimized-oracle.txt"),verified.Report);
            }
        }
    }
}
