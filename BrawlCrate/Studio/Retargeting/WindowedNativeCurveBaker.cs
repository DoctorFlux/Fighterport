using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    internal static class WindowedNativeCurveBaker
    {
        internal const int WindowFrames=8;
        internal const long WindowWork=2000000,MaximumKeys=2000000;
        internal static AdaptiveRotationBake Bake(int[] parents,bool[] editable,int last,Func<float,AdaptiveRotationPose> sample,
            Func<int,float,Quaternion> frozen,CancellationToken token,float[] mandatory,NativeRetargetConstants constants,Action<int,int> progress=null)
        {
            if(parents==null||editable==null||parents.Length<1||parents.Length>512||parents.Length!=editable.Length||last<0||last>4096||
                9L*(last+1)*parents.Length>MaximumKeys||sample==null||frozen==null||mandatory!=null&&(mandatory.Length>MaximumKeys||mandatory.Any(t=>float.IsNaN(t)||float.IsInfinity(t)||t<0||t>last)))
                throw new ArgumentException("Windowed native baking requires the existing bounded rig, frame, key and finite witness domain.");
            var points=(mandatory??new float[0]).Distinct().OrderBy(t=>t).ToArray();
            int windows=Math.Max(1,(last+WindowFrames-1)/WindowFrames);
            var result=new AdaptiveRotationBake{Converged=true,AllTimesOnI6Grid=true};
            var curves=new Dictionary<int,List<AdaptiveVectorKey>[]>();var witnesses=new SortedSet<float>();Vector3[] continuation=null;
            long keys=0;
            for(int window=0;window<windows;window++)
            {
                token.ThrowIfCancellationRequested();int start=window*WindowFrames,length=Math.Min(WindowFrames,last-start);
                progress?.Invoke(window,windows);
                int at=Array.BinarySearch(points,(float)start);if(at<0)at=~at;
                var sectionPoints=new List<float>();for(int i=at;i<points.Length&&points[i]<=start+length;i++)sectionPoints.Add(points[i]);
                var part=AdaptiveRotationCurveBaker.Bake(parents,editable,start+length,sample,frozen,token,
                    maximumWork:WindowWork,generatedEulerContinuation:true,
                    mandatoryTimes:sectionPoints.ToArray(),invariants:constants,initialEuler:continuation,firstFrame:start);
                result.Work+=part.Work;result.SampleCount+=part.SampleCount;result.WindowCount++;
                result.MaximumWindowWork=Math.Max(result.MaximumWindowWork,part.Work);result.ProvenConstantGroups+=part.ProvenConstantGroups;
                result.IntegerEulerRebranches+=part.IntegerEulerRebranches;result.AllTimesOnI6Grid&=part.AllTimesOnI6Grid;
                result.MaximumOrientationDegrees=Math.Max(result.MaximumOrientationDegrees,part.MaximumOrientationDegrees);
                result.MaximumTranslationError=Math.Max(result.MaximumTranslationError,part.MaximumTranslationError);
                result.MaximumScaleError=Math.Max(result.MaximumScaleError,part.MaximumScaleError);
                result.MaximumFixedEndpointOrientationDegrees=Math.Max(result.MaximumFixedEndpointOrientationDegrees,part.MaximumFixedEndpointOrientationDegrees);
                result.LastBone=part.LastBone;result.LastChannel=part.LastChannel;result.LastFirstFrame=part.LastFirstFrame;
                result.LastLastFrame=part.LastLastFrame;result.LastDepth=part.LastDepth;result.LastMeasuredError=part.LastMeasuredError;
                if(!part.Converged)
                {
                    result.Converged=false;result.WorkLimitReached=part.WorkLimitReached;result.UnresolvedIntervals=part.UnresolvedIntervals;
                    result.FirstUnresolvedBone=part.FirstUnresolvedBone;result.FirstUnresolvedChannel=part.FirstUnresolvedChannel;
                    result.FirstUnresolvedFirstFrame=part.FirstUnresolvedFirstFrame;result.FirstUnresolvedLastFrame=part.FirstUnresolvedLastFrame;
                    result.FirstUnresolvedDepth=part.FirstUnresolvedDepth;result.FirstUnresolvedError=part.FirstUnresolvedError;result.FailureWitness=part.FailureWitness;
                    result.Tracks=new AdaptiveTransformTrack[0];return result;
                }
                continuation=sample(start+length).EulerDegrees.ToArray();
                foreach(var track in part.Tracks)
                {
                    if(!curves.TryGetValue(track.Bone,out var groups))curves.Add(track.Bone,groups=new[]{new List<AdaptiveVectorKey>(),new List<AdaptiveVectorKey>(),new List<AdaptiveVectorKey>()});
                    var incoming=new[]{track.Rotation,track.Translation,track.Scale};
                    for(int group=0;group<3;group++)
                    {
                        if(incoming[group]==null)continue;
                        if(group>0&&incoming[group].Length==1&&groups[group].Count==1&&
                            (group==1?constants?.Translation(track.Bone):constants?.Scale(track.Bone))==true)
                        {
                            if(!Same(incoming[group][0].Value,groups[group][0].Value))throw new InvalidDataException("A proven constant changed across native windows.");
                            continue;
                        }
                        foreach(var key in incoming[group])
                        {
                            var added=new AdaptiveVectorKey{Time=key.Time,Value=key.Value,IncomingSlope=key.IncomingSlope,OutgoingSlope=key.OutgoingSlope};
                            var list=groups[group];
                            if(list.Count>0&&added.Time<=list[list.Count-1].Time)
                            {
                                var prior=list[list.Count-1];
                                if(key.Time!=start||added.Time!=prior.Time||!Same(prior.Value,added.Value))throw new InvalidDataException("Native window boundary or float knot collapsed; no candidate is available.");
                                prior.OutgoingSlope=added.OutgoingSlope;
                                continue;
                            }
                            list.Add(added);keys+=6;
                            if(keys>MaximumKeys)throw new InvalidDataException("The complete windowed native master exceeds the two-million-key operation bound.");
                        }
                    }
                    continuation[track.Bone]=track.Rotation.Last().Value;
                }
                foreach(float time in part.ValidationTimes)witnesses.Add(time);
                if(witnesses.Count>MaximumKeys)throw new InvalidDataException("The complete native validation witness set exceeds its two-million-item bound.");
                // A constant group can have a single knot at the start of each
                // window. Keep it constant through the whole boundary; do not
                // invent an absent endpoint with a different value or slope.
            }
            result.NativeKeyUpperBound=keys;result.ValidationTimes=witnesses.ToArray();
            result.Tracks=curves.OrderBy(p=>p.Key).Select(p=>new AdaptiveTransformTrack{Bone=p.Key,
                Rotation=p.Value[0].ToArray(),Translation=p.Value[1].Count==0?null:p.Value[1].ToArray(),Scale=p.Value[2].Count==0?null:p.Value[2].ToArray()}).ToArray();
            return result;
        }
        // Same full-quality master, fresh native CHR0 decoder. All original
        // acceptance witnesses survive joining, including either side of each
        // integer window boundary. No partial result escapes a failed check.
        internal static void VerifyDecoded(BrawlRig target, BrawlLib.SSBB.ResourceNodes.CHR0Node candidate,
            AdaptiveRotationBake bake,Func<float,AdaptiveRotationPose> master,CancellationToken token)
        {
            string folder=Path.Combine(Path.GetTempPath(),"Fighterport-native-master-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);string file=Path.Combine(folder,"candidate.chr0");
            try
            {
                token.ThrowIfCancellationRequested();
                if(candidate.Children.OfType<BrawlLib.SSBB.ResourceNodes.CHR0EntryNode>().Sum(e=>e.KeyArrays.Sum(a=>(long)a.KeyCount))>MaximumKeys)
                    throw new InvalidDataException("The complete native master exceeds the two-million-key operation bound.");
                string[] entries=candidate.Children.OfType<BrawlLib.SSBB.ResourceNodes.CHR0EntryNode>().Select(EntrySemantics).ToArray();
                candidate.ExportChecked(file);
                using(var decoded=BrawlLib.SSBB.ResourceNodes.NodeFactory.FromFile(null,file) as BrawlLib.SSBB.ResourceNodes.CHR0Node)
                {
                    if(decoded==null||decoded.FrameCount!=candidate.FrameCount||decoded.Loop!=candidate.Loop||decoded.Version!=candidate.Version||decoded.NativeScalingRule!=candidate.NativeScalingRule||
                        decoded.Name!=candidate.Name||!entries.SequenceEqual(decoded.Children.OfType<BrawlLib.SSBB.ResourceNodes.CHR0EntryNode>().Select(EntrySemantics)))
                        throw new InvalidDataException("Windowed native master metadata changed after serialization.");
                    foreach(float time in bake.ValidationTimes)
                    {
                        token.ThrowIfCancellationRequested();
                        var expected=master(time);var actual=BrawlRetargeter.Sample(target,decoded,time);
                        for(int bone=0;bone<target.Bones.Length;bone++)
                        {
                            double error=ContactMotion.EndOrientationDegrees(actual.Orientation[bone],expected.WorldRotations[bone]);
                            if(error>.025||expected.Translations!=null&&Vector3.Distance(actual.Local[bone].Translation,expected.Translations[bone])>.0002||
                                expected.Scales!=null&&Maximum(actual.Local[bone].Scale-expected.Scales[bone])>.00001)
                                throw new InvalidDataException("Decoded native master exceeds its unchanged full-quality bound at frame "+time+", bone "+bone+"; orientation "+error+" degrees. No result was imported.");
                        }
                    }
                }
            }
            finally{try{File.Delete(file);Directory.Delete(folder);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
        static string EntrySemantics(BrawlLib.SSBB.ResourceNodes.CHR0EntryNode e)=>e.Name+"|"+e.UseModelScale+"|"+e.UseModelRotate+"|"+e.UseModelTranslate+"|"+e.ScaleCompensateApply+"|"+e.ScaleCompensateParent+"|"+e.ClassicScaleOff;
        static float Maximum(Vector3 v)=>Math.Max(Math.Abs(v.X),Math.Max(Math.Abs(v.Y),Math.Abs(v.Z)));
        static bool Same(Vector3 a,Vector3 b)=>Bits(a.X)==Bits(b.X)&&Bits(a.Y)==Bits(b.Y)&&Bits(a.Z)==Bits(b.Z);
        static int Bits(float v)=>BitConverter.ToInt32(BitConverter.GetBytes(v),0);
    }
}
