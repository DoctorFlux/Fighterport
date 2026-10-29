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
    static async Task NativeSourceKeys(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Source-key qualification requires the private desktop.");
        using(var root=NodeFactory.FromFile(null,args[0]))
        {
            var model=Walk(root).OfType<MDL0Node>().OrderByDescending(m=>m.NumVertices).First();
            SharedRetargetTrackChecks(model,output);
            await NativeInterpolatedKeys(model,output);
            SourceKeysProcedural(model,output);
            SourceKeyQualityChecks(model,output);
            NativePairedSourceKeyChecks(model,output);
            NativeRetargetPayloadChecks(model,output);
            NativeSourceKeyFallbackChecks(model);
        }
        if(args.Contains("--retarget-source"))SourceKeysAuthentic(args,output);
    }

    static void SourceKeysProcedural(MDL0Node model,string output)
    {
        var rig=BrawlRetargeter.Capture(model);var plan=new HumanoidRetargeter(rig.Rig,rig.Rig);
        int body=rig.Rig.Roles["pelvis"];string name=rig.Bones[body].Name;
        using(var dense=new CHR0Node{Name="Independent linear target-space reference",FrameCount=5})
        using(var source=new CHR0Node{Name="Independent sparse source schedule",FrameCount=5})
        {
            foreach(var bone in rig.Bones)
            {
                var entry=dense.CreateEntry(bone.Name);entry.Keyframes.PreserveInterpolation=true;
                for(int c=0;c<9;c++)for(int at=0;at<5;at++)entry.Keyframes.SetFrameValue(c,at,bone.BindState[c],true)._tangent=0;
            }
            var denseBody=(CHR0EntryNode)dense.FindChild(name,false);
            for(int c=3;c<6;c++)for(int at=0;at<5;at++)
            {var key=denseBody.KeyArrays[c].GetKeyframe(at);key._value=rig.Bones[body].BindState[c]+at*2;key._tangent=2;}
            for(int at=0;at<5;at++){var key=denseBody.KeyArrays[6].GetKeyframe(at);key._value=rig.Bones[body].BindState[6]+at*2;key._tangent=2;}
            var input=source.CreateEntry(name);input.Keyframes.PreserveInterpolation=true;
            foreach(int at in new[]{0,4})input.Keyframes.SetFrameValue(3,at,at*2,true)._tangent=2;
            foreach(int at in new[]{0,2,4})input.Keyframes.SetFrameValue(4,at,at*2,true)._tangent=2;
            input.Keyframes.SetFrameValue(5,0,0,true)._tangent=0;
            source.CreateEntry("unmatched procedural extension").Keyframes.SetFrameValue(3,0,50,true)._tangent=0;
            foreach(int control in plan.TargetMotionControls)
            {
                var entry=(CHR0EntryNode)dense.FindChild(rig.Bones[control].Name,false);
                var curve=entry.KeyArrays[3];while(curve.KeyCount>0)curve.Remove(curve.Entries.First()._index);
                foreach(int at in new[]{0,2,4})curve.SetFrameValue(at,at*180,true)._tangent=180;
            }
            string baseline=NativeAnimationOptimization.ContentKey(dense),sourceBefore=NativeAnimationOptimization.ContentKey(source),bind=BrawlRetargeter.SkeletonFingerprint(model);
            foreach(var mode in new[]{RetargetKeyframeMode.SourceBoneTimes,RetargetKeyframeMode.SourceChannelTimes})
            using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan,IgnoredSourceBones=new[]{"unmatched procedural extension"}})
            {
                RetargetKeyframes.Prepare(result,rig,rig,source,mode,CancellationToken.None,null);
                var track=(CHR0EntryNode)result.Animation.FindChild(name,false);
                Check(result.KeyframeMode==mode&&result.KeyframeReport!=null&&result.KeyframeReport.ResultKeys<result.KeyframeReport.DenseKeys,
                    mode+": explicit representation reduces keys against its independently frozen dense baseline");
                Check(result.KeyframeReport.ResultBytes<result.KeyframeReport.DenseBytes&&result.KeyframeReport.Samples>=26&&result.KeyframeReport.VertexVisits>0,
                    mode+": actual native bytes shrink and complete native bone/skin witnesses are measured");
                if(mode==RetargetKeyframeMode.SourceBoneTimes)
                {
                    foreach(int c in new[]{3,4,5})Check(new[]{0,2,4}.All(at=>track.KeyArrays[c].Entries.Any(k=>k._index==at)),
                        "Source bone keys: required target axis "+c+" retains the source-bone union as quality-repair seeds");
                    Check(result.KeyframeReport.MaximumEulerError<.0001&&result.KeyframeReport.MaximumSkinError<.001,
                        "Source bone keys: independently authored linear reference retains target-space motion through native Hermite interpolation");
                }
                else
                {
                    Check(new[]{0,4}.All(at=>track.KeyArrays[3].Entries.Any(k=>k._index==at))&&new[]{0,2,4}.All(at=>track.KeyArrays[4].Entries.Any(k=>k._index==at)),
                        "Source channel keys: original varying-channel times remain seeds while quality repair may add keys");
                    Check(Math.Abs(track.GetFrameValue(5,4)-track.GetFrameValue(5,0)-8)<.0001&&Math.Abs(track.GetFrameValue(6,4)-track.GetFrameValue(6,0)-8)<.0001&&
                        result.KeyframeReport.MaximumEulerError<=.01&&result.KeyframeReport.MaximumOrientationError<=.01,
                        "Source channel keys: derived target rotation and translation are restored instead of accepting the former eight-degree omitted-axis contradiction");
                }
                Check(result.Animation.Children.All(e=>rig.Bones.Any(b=>b.Name==e.Name))&&!result.Animation.Children.Any(e=>e.Name=="unmatched procedural extension"),
                    mode+": no absent-target or unmapped donor track enters the target animation");
                foreach(int control in plan.TargetMotionControls)SourceKeysSameTrack((CHR0EntryNode)dense.FindChild(rig.Bones[control].Name,false),
                    (CHR0EntryNode)result.Animation.FindChild(rig.Bones[control].Name,false),mode+": exact protected full-turn control "+rig.Bones[control].Name);
                var provenance=new RetargetProvenance{FloorContactsEnabled=true,PlantedFootSamples=9,MaximumContactResidual=.25f,MaximumLimbResidual=.125f};
                provenance.RecordKeyframeEvidence(result);provenance.ValidateKeyframeEvidence();
                Check(!provenance.FloorContactsEnabled&&provenance.PlantedFootSamples==0&&provenance.DenseReferenceStatistics.PlantedFootSamples==9&&
                    provenance.ReadKeyframeReport().ResultContent==NativeAnimationOptimization.ContentKey(result.Animation),
                    mode+": additive receipt binds the exact result and keeps dense contact evidence historical");
                var json=new JavaScriptSerializer{MaxJsonLength=1024*1024};var roundtrip=json.Deserialize<RetargetProvenance>(json.Serialize(provenance));roundtrip.ValidateKeyframeEvidence();
                Check(roundtrip.KeyframeMode==mode&&roundtrip.ReadKeyframeReport().VerifiedSha256==result.KeyframeReport.VerifiedSha256,
                    mode+": bounded provenance roundtrip retains chosen key layout and independently verified hash");
                File.WriteAllText(Path.Combine(output,"source-keys-procedural-"+mode+".json"),json.Serialize(result.KeyframeReport));
            }
            Check(NativeAnimationOptimization.ContentKey(dense)==baseline&&NativeAnimationOptimization.ContentKey(source)==sourceBefore&&BrawlRetargeter.SkeletonFingerprint(model)==bind,
                "Source-key preparation preserves exact dense/source curves and authentic native target binds");
            input.KeyArrays[5].Remove(0);
            using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan})
            {
                RetargetKeyframes.Prepare(result,rig,rig,source,RetargetKeyframeMode.SourceChannelTimes,CancellationToken.None,null);
                var restored=(CHR0EntryNode)result.Animation.FindChild(name,false);
                Check(restored!=null&&Math.Abs(restored.GetFrameValue(5,4)-restored.GetFrameValue(5,0)-8)<.0001&&result.KeyframeReport.QualityVerified,
                    "Partial-axis source groups restore required frozen target-space axes rather than rejecting the complete animation or silently dropping motion");
            }
            input.Keyframes.SetFrameValue(5,0,0,true)._tangent=0;

            // A paired knot is an authored incoming/outgoing layout. Sparse
            // projection retains both sides without generating neighboring keys.
            input.Keyframes.SetFrameValue(3,2,4,true)._tangent=2;input.Keyframes.SetFrameValue(3,2,5,true)._tangent=3;
            foreach(var mode in new[]{RetargetKeyframeMode.SourceBoneTimes,RetargetKeyframeMode.SourceChannelTimes})
            using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan})
            {
                RetargetKeyframes.Prepare(result,rig,rig,source,mode,CancellationToken.None,null);
                var track=(CHR0EntryNode)result.Animation.FindChild(name,false);
                Check(track.KeyArrays[3].Entries.Count(k=>k._index==2)==2&&new[]{0,4}.All(at=>track.KeyArrays[3].Entries.Any(k=>k._index==at))&&result.KeyframeReport.PairedKnots>0,
                    mode+": source paired sides/count survive precise independent CHR0 encoding");
            }
            // Raw 720-degree body turns must not be replaced by a shortest-path
            // quaternion curve merely because the endpoint poses coincide.
            for(int at=0;at<5;at++){var key=denseBody.KeyArrays[3].GetKeyframe(at);key._value=rig.Bones[body].BindState[3]+at*180;key._tangent=180;}
            foreach(var mode in new[]{RetargetKeyframeMode.SourceBoneTimes,RetargetKeyframeMode.SourceChannelTimes})
            using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan})
            {
                RetargetKeyframes.Prepare(result,rig,rig,source,mode,CancellationToken.None,null);
                var track=(CHR0EntryNode)result.Animation.FindChild(name,false);
                Check(Math.Abs(track.GetFrameValue(3,4)-track.GetFrameValue(3,0)-720)<.0001,mode+": authored raw 720-degree body turn survives source layout and native reopening");
            }
            for(int at=0;at<5;at++){var key=denseBody.KeyArrays[3].GetKeyframe(at);key._value=rig.Bones[body].BindState[3]+at*2;key._tangent=2;}
            foreach(int frames in new[]{1,2})foreach(bool loop in new[]{false,true})
            using(var tinyDense=new CHR0Node{Name="Independent tiny native result",FrameCount=frames,Loop=loop})
            using(var tinySource=new CHR0Node{Name="Independent tiny native source",FrameCount=frames,Loop=loop})
            {
                int last=loop?frames:frames-1;
                var authored=tinySource.CreateEntry(name);authored.Keyframes.PreserveInterpolation=true;
                var target=tinyDense.CreateEntry(name);target.Keyframes.PreserveInterpolation=true;
                for(int c=3;c<6;c++)foreach(int at in Enumerable.Range(0,last+1))
                {
                    authored.Keyframes.SetFrameValue(c,at,at,true)._tangent=last==0?0:1;
                    target.Keyframes.SetFrameValue(c,at,rig.Bones[body].BindState[c]+at,true)._tangent=last==0?0:1;
                }
                foreach(var mode in new[]{RetargetKeyframeMode.SourceBoneTimes,RetargetKeyframeMode.SourceChannelTimes,RetargetKeyframeMode.Interpolated})
                using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(tinyDense),Plan=plan})
                {
                    RetargetKeyframes.Prepare(result,rig,rig,tinySource,mode,CancellationToken.None,null);
                    model.ApplyCHR(result.Animation,last+1);
                    Check(result.Animation.FrameCount==frames&&result.Animation.Loop==loop&&
                        Math.Abs(rig.Bones[body].FrameState[3]-rig.Bones[body].BindState[3]-last)<.0001&&result.KeyframeReport.MaximumOrientationError<.001,
                        mode+": "+frames+"-frame "+(loop?"loop closing":"non-looping")+" representation retains native timing and exact authored endpoint evaluation");
                }
            }
            using(var cancel=new CancellationTokenSource())
            using(var result=new RetargetResult{Animation=NativeAnimationOptimization.Copy(dense),Plan=plan})
            {
                bool cancelled=false;
                try{RetargetKeyframes.Prepare(result,rig,rig,source,RetargetKeyframeMode.SourceChannelTimes,cancel.Token,(p,m)=>{if(p>=92)cancel.Cancel();});}
                catch(OperationCanceledException){cancelled=true;}
                Check(cancelled&&result.DenseAnimation==null&&result.KeyframeReport==null&&NativeAnimationOptimization.ContentKey(result.Animation)==baseline,
                    "Source-key cancellation after candidate construction publishes no partial result and preserves the frozen dense baseline");
            }
            new RetargetProvenance().ValidateKeyframeEvidence();
            Check(new RetargetProvenance().KeyframeMode==RetargetKeyframeMode.Dense,"Older absent optional recipe fields retain original Dense Next defaults");
        }
    }

    static void SourceKeysAuthentic(string[] args,string output)
    {
        string donor=args[Array.IndexOf(args,"--retarget-source")+1],name=args.Contains("--clip")?args[Array.IndexOf(args,"--clip")+1]:"Wait1";
        var hashes=new[]{args[0],args[1],donor}.Distinct().ToDictionary(p=>p,FighterProject.HashFile);
        using(var targetRoot=NodeFactory.FromFile(null,args[0]))using(var sourceRoot=NodeFactory.FromFile(null,donor))using(var motionRoot=NodeFactory.FromFile(null,args[1]))
        {
            var targetModel=Walk(targetRoot).OfType<MDL0Node>().OrderByDescending(m=>m.NumVertices).First();
            var sourceModel=Walk(sourceRoot).OfType<MDL0Node>().OrderByDescending(m=>m.NumVertices).First();
            var clip=Walk(motionRoot).OfType<CHR0Node>().First(c=>c.Name==name);
            var source=BrawlRetargeter.Capture(sourceModel);var target=BrawlRetargeter.Capture(targetModel);
            string before=NativeAnimationOptimization.SourceContentKey(clip);
            string targetContainer=Path.Combine(output,"source-key-native-target"+Path.GetExtension(args[0]));
            string sourceContainer=Path.Combine(output,"source-key-native-source"+Path.GetExtension(donor));
            File.Copy(args[0],targetContainer);File.Copy(donor,sourceContainer);
            string sourceAnimation=Path.Combine(output,"source-key-native-source.chr0");
            SourceKeyArtifactExport(clip,sourceAnimation);
            File.WriteAllText(Path.Combine(output,"source-key-native-fixtures.json"),new JavaScriptSerializer().Serialize(new{
                TargetContainer=targetContainer,TargetModel=targetModel.Name,SourceContainer=sourceContainer,SourceModel=sourceModel.Name,
                SourceAnimation=sourceAnimation,Frames=clip.FrameCount,Loop=clip.Loop,SourceRig=source.Fingerprint,TargetRig=target.Fingerprint,
                SourceAnimationContent=before,BackgroundOnly=true,OSInputInjected=false,ScreenCopied=false}));
            using(var original=BrawlRetargeter.Bake(source,target,clip,false,groundMode:GroundContactMode.Airborne))
            using(var explicitDense=BrawlRetargeter.Bake(source,target,clip,false,groundMode:GroundContactMode.Airborne,keyframeMode:RetargetKeyframeMode.Dense))
            {
                Check(original.Animation.Children.All(e=>original.Plan.Mapping.Any(m=>m.Target==e.Name))&&
                    original.OmittedTargetBones.All(n=>original.Animation.FindChild(n,false)==null),
                    "Authentic dense Next output excludes target-only native bone entries before either source-key projection");
                Check(NativeAnimationOptimization.ContentKey(original.Animation)==NativeAnimationOptimization.ContentKey(explicitDense.Animation)&&original.KeyframeReport==null&&original.DenseAnimation==null,
                    "Authentic retargeting: explicit/default Dense retain the original native Next curves without a projection or review receipt");
                foreach(var mode in new[]{RetargetKeyframeMode.SourceBoneTimes,RetargetKeyframeMode.SourceChannelTimes,RetargetKeyframeMode.Interpolated})
                using(var result=BrawlRetargeter.Bake(source,target,clip,false,groundMode:GroundContactMode.Airborne,keyframeMode:mode))
                {
                    SourceKeyArtifactExport(result.DenseAnimation,Path.Combine(output,"source-keys-authentic-"+mode+"-dense.chr0"));
                    SourceKeyArtifactExport(result.Animation,Path.Combine(output,"source-keys-authentic-"+mode+".chr0"));
                    Check(result.Animation.FrameCount==clip.FrameCount&&result.Animation.Loop==clip.Loop&&result.KeyframeReport.DenseContent==NativeAnimationOptimization.ContentKey(original.Animation),
                        "Authentic "+sourceModel.Name+" → "+targetModel.Name+" "+mode+": timing/loop and exact original dense reference retained");
                    var controls=result.KeyframeReport.ProtectedControls;
                    foreach(var entry in result.Animation.Children.OfType<CHR0EntryNode>())
                    {
                        Check(target.Bones.Any(b=>b.Name==entry.Name),"Authentic "+mode+": output track belongs to the exact target: "+entry.Name);
                        Check(result.Plan.Mapping.Any(m=>m.Target==entry.Name),"Authentic "+mode+": output track has an actual corresponding source bone: "+entry.Name);
                        if(controls.Contains(entry.Name))
                        {SourceKeysSameTrack((CHR0EntryNode)original.Animation.FindChild(entry.Name,false),entry,"Authentic "+mode+": own-source protected control "+entry.Name);continue;}
                        var map=result.Plan.Mapping.SingleOrDefault(m=>m.Target==entry.Name);var input=map==null?null:(CHR0EntryNode)clip.FindChild(map.Source,false);
                        for(int c=0;c<9;c++)if(mode==RetargetKeyframeMode.SourceChannelTimes&&input!=null&&input.KeyArrays[c].KeyCount>1)
                            Check(input.KeyArrays[c].Entries.All(k=>entry.KeyArrays[c].Entries.Any(actual=>actual._index==k._index))||SourceKeyRawConstant(((CHR0EntryNode)original.Animation.FindChild(entry.Name,false)).KeyArrays[c]),
                                "Authentic source channel seeds survive unless their whole raw constant function has proved fixed storage: "+entry.Name+" / "+c);
                    }
                    bool exactIdentity=result.KeyframeReport.DenseFallback&&result.KeyframeReport.DenseContent==result.KeyframeReport.ResultContent;
                    Check((exactIdentity||result.KeyframeReport.VertexVisits>0&&result.KeyframeReport.Samples>=clip.FrameCount)&&result.KeyframeReport.Version==(mode==RetargetKeyframeMode.Interpolated?RetargetKeyframes.InterpolatedVersion:"source-key-quality-v2")&&
                        result.KeyframeReport.ScalarCertified&&result.KeyframeReport.QualityVerified&&result.KeyframeReport.MaximumEulerError<=.01&&result.KeyframeReport.MaximumOrientationError<=.01,
                        "Authentic "+mode+": full native skin/pose drift obeys the frozen quality gate and remains separate from universal contact claims");
                    File.WriteAllText(Path.Combine(output,"source-keys-authentic-"+mode+".json"),new JavaScriptSerializer{MaxJsonLength=1024*1024}.Serialize(result.KeyframeReport));
                    SourceKeyAuthenticGapEvidence(clip,original,result,output);
                }
            }
            Check(NativeAnimationOptimization.SourceContentKey(clip)==before&&hashes.All(pair=>FighterProject.HashFile(pair.Key)==pair.Value),
                "Authentic source-key retargeting leaves original source files and all authored donor curves immutable");
        }
    }
    static void SourceKeyArtifactExport(CHR0Node clip,string path)
    {
        string content=NativeAnimationOptimization.SourceContentKey(clip);
        using(var snapshot=NativeAnimationOptimization.CopySource(clip))snapshot.ExportChecked(path);
        using(var decoded=NodeFactory.FromFile(null,path) as CHR0Node)
            if(decoded==null||NativeAnimationOptimization.SourceContentKey(decoded)!=content)
                throw new InvalidDataException("The source-key rendering artifact did not independently reopen with the frozen native content.");
    }
    static void SourceKeysSameTrack(CHR0EntryNode a,CHR0EntryNode b,string message)
    {
        Check(a!=null&&b!=null&&Enumerable.Range(0,9).All(c=>a.KeyArrays[c].Entries.Select(k=>k._index+":"+BitConverter.ToInt32(BitConverter.GetBytes(k._value),0)+":"+BitConverter.ToInt32(BitConverter.GetBytes(k._tangent),0))
            .SequenceEqual(b.KeyArrays[c].Entries.Select(k=>k._index+":"+BitConverter.ToInt32(BitConverter.GetBytes(k._value),0)+":"+BitConverter.ToInt32(BitConverter.GetBytes(k._tangent),0)))),message);
    }
}
