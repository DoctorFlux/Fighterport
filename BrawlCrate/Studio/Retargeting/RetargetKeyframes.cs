using BrawlLib.Internal;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    // Dense is zero so projects and callers written before these optional
    // representations retain the original Next behavior.
    public enum RetargetKeyframeMode { Dense, SourceBoneTimes, SourceChannelTimes }

    public sealed class RetargetKeyframeReport
    {
        public string Version { get; set; } = "source-key-layout-v1";
        public RetargetKeyframeMode Mode { get; set; }
        public int DenseKeys { get; set; }
        public int ResultKeys { get; set; }
        public long DenseBytes { get; set; }
        public long ResultBytes { get; set; }
        public string DenseContent { get; set; }
        public string ResultContent { get; set; }
        public string VerifiedSha256 { get; set; }
        public int Samples { get; set; }
        public long BoneVisits { get; set; }
        public long VertexVisits { get; set; }
        public double MaximumEulerError { get; set; }
        public double MaximumOrientationError { get; set; }
        public double MaximumScaleError { get; set; }
        public double MaximumLocalTranslationError { get; set; }
        public double MaximumWorldBoneError { get; set; }
        public double MaximumWorldBasisError { get; set; }
        public double MaximumSkinError { get; set; }
        public double WorstFrame { get; set; }
        public string WorstBone { get; set; }
        public string[] ProtectedControls { get; set; } = new string[0];
        public string[] OmittedSourceTracks { get; set; } = new string[0];
        public string[] OmittedTargetChannels { get; set; } = new string[0];
        public string[] OmittedTargetTracks { get; set; } = new string[0];
        public int PairedKnots { get; set; }

        internal void Validate()
        {
            bool Hash(string s) => s!=null&&s.Length==64&&s.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
            if(Version!="source-key-layout-v1"||Mode==RetargetKeyframeMode.Dense||!Enum.IsDefined(typeof(RetargetKeyframeMode),Mode)||
                DenseKeys<0||DenseKeys>2000000||ResultKeys<0||ResultKeys>2000000||DenseBytes<1||ResultBytes<1||
                !Hash(DenseContent)||!Hash(ResultContent)||!Hash(VerifiedSha256)||Samples<1||Samples>100000||
                BoneVisits<0||BoneVisits>10000000||VertexVisits<0||VertexVisits>1000000000||PairedKnots<0||PairedKnots>2000000||
                new[]{MaximumEulerError,MaximumOrientationError,MaximumScaleError,MaximumLocalTranslationError,MaximumWorldBoneError,
                    MaximumWorldBasisError,MaximumSkinError,WorstFrame}.Any(v=>double.IsNaN(v)||double.IsInfinity(v)||v<0)||
                (WorstBone?.Length??0)>256||new[]{ProtectedControls,OmittedSourceTracks,OmittedTargetChannels,OmittedTargetTracks}.Any(a=>a==null||a.Length>65536||a.Any(s=>string.IsNullOrEmpty(s)||s.Length>512)))
                throw new InvalidDataException("Invalid source-key retarget comparison receipt.");
        }

        public string Describe()
        {
            string N(double value)=>value.ToString("G7",CultureInfo.InvariantCulture);
            return (Mode==RetargetKeyframeMode.SourceBoneTimes?"Source bone keys":"Source channel keys")+
                " — retargeted values, source key layout; poses between keys can differ.\r\n"+
                "Keys: "+DenseKeys+" → "+ResultKeys+". Fresh precise CHR0 bytes: "+DenseBytes+" → "+ResultBytes+".\r\n"+
                "Compared against the frozen original dense Next result, using "+Samples+" complete native pose/skin samples.\r\n"+
                "Maximum sampled differences: raw Euler "+N(MaximumEulerError)+"°, local orientation "+N(MaximumOrientationError)+
                "°, local translation "+N(MaximumLocalTranslationError)+", scale "+N(MaximumScaleError)+".\r\n"+
                "Native world bone position "+N(MaximumWorldBoneError)+", forward basis "+N(MaximumWorldBasisError)+", all skinned vertices "+N(MaximumSkinError)+".\r\n"+
                "Largest skin/bone-position witness: "+(WorstBone??"none")+", native display frame "+N(WorstFrame+1)+".\r\n"+
                "Protected controls retain the original exact own-source curve policy, including any necessary axis-conversion keys: "+
                (ProtectedControls.Length==0?"none":string.Join(", ",ProtectedControls))+".\r\n"+
                "Source paired knot layouts retained: "+PairedKnots+". Retargeted sides use the dense target result; this does not recover discontinuities already absent from that dense bake.\r\n"+
                (OmittedSourceTracks.Length==0?"":"Unmapped source tracks omitted: "+string.Join(", ",OmittedSourceTracks)+".\r\n")+
                (OmittedTargetTracks.Length==0?"":"Dense target tracks without an authored mapped source bone: "+string.Join(", ",OmittedTargetTracks)+". They use the target bind pose under this layout.\r\n")+
                (OmittedTargetChannels.Length==0?"":"Channels absent under this layout: "+string.Join(", ",OmittedTargetChannels)+".\r\n")+
                "These are sampled differences, not continuous error bounds or a contact/game acceptance result. Dense solver contact statistics remain historical and do not validate these changed curves. No PAC/container savings are claimed.";
        }
    }

    internal static class RetargetKeyframes
    {
        static readonly string[] Channels={"SX","SY","SZ","RX","RY","RZ","TX","TY","TZ"};
        internal static void Prepare(RetargetResult result,BrawlRig source,BrawlRig target,CHR0Node sourceClip,
            RetargetKeyframeMode mode,CancellationToken token,Action<int,string> progress)
        {
            token.ThrowIfCancellationRequested();
            var dense=result.Animation;
            if(Count(dense)>2000000||Count(sourceClip)>2000000)throw new InvalidDataException("Source-key preparation exceeds the two-million-key operation limit.");
            var report=new RetargetKeyframeReport {Mode=mode,DenseKeys=Count(dense),DenseContent=NativeAnimationOptimization.ContentKey(dense),
                ProtectedControls=result.Plan.TargetMotionControls.Select(i=>target.Bones[i].Name).ToArray(),OmittedSourceTracks=result.IgnoredSourceBones.ToArray()};
            var controls=new HashSet<string>(report.ProtectedControls,StringComparer.Ordinal);
            CHR0Node candidate=NativeAnimationOptimization.Copy(dense),decoded=null;
            string folder=Path.Combine(Path.GetTempPath(),"Fighterport-sourcekeys-"+Guid.NewGuid().ToString("N"));
            string beforePath=Path.Combine(folder,"dense.chr0"),afterPath=Path.Combine(folder,"source-keys.chr0");
            try
            {
                Directory.CreateDirectory(folder);
                var sourceTracks=sourceClip.Children.OfType<CHR0EntryNode>().ToDictionary(t=>t.Name,StringComparer.Ordinal);
                var omitted=new List<string>();var omittedTracks=new List<string>();
                long plannedKeys=candidate.Children.OfType<CHR0EntryNode>().Where(e=>controls.Contains(e.Name)).Sum(e=>e.KeyArrays.Sum(a=>(long)a.KeyCount));
                foreach(var entry in candidate.Children.OfType<CHR0EntryNode>().ToArray())
                {
                    token.ThrowIfCancellationRequested();
                    if(controls.Contains(entry.Name))continue;
                    var map=result.Plan.Mapping.SingleOrDefault(m=>m.Target==entry.Name);
                    CHR0EntryNode input=null;
                    if(map==null||!sourceTracks.TryGetValue(map.Source,out input)||!input.KeyArrays.Any(a=>a.KeyCount>0))
                    {omittedTracks.Add(entry.Name);entry.Remove();entry.Dispose();continue;}
                    int[] union=input.KeyArrays.SelectMany(a=>a.Entries.GroupBy(k=>k._index).Select(g=>new{Time=g.Key,Count=g.Count()}))
                        .GroupBy(p=>p.Time).OrderBy(g=>g.Key).SelectMany(g=>Enumerable.Repeat(g.Key,g.Max(p=>p.Count))).ToArray();
                    if(mode==RetargetKeyframeMode.SourceChannelTimes)
                        for(int group=0;group<3;group++)
                        {
                            int present=Enumerable.Range(group*3,3).Count(c=>input.KeyArrays[c].KeyCount>0);
                            if(present!=0&&present!=3)throw new NotSupportedException("Source channel keys cannot preserve a partial-axis "+(group==0?"scale":group==1?"rotation":"translation")+
                                " group on "+input.Name+" with the qualified Brawl CHR0 writer. Use Source bone keys or Dense Next; no replacement fixed keys were added.");
                        }
                    var neededGroups=Enumerable.Range(0,3).Select(group=>Enumerable.Range(group*3,3).Any(c=>
                        entry.KeyArrays[c].Entries.Any(k=>!SameBits(k._value,target.Bones[map.TargetIndex].BindState[c])||!SameBits(k._tangent,0)))).ToArray();
                    for(int channel=0;channel<9;channel++)
                    {
                        var original=entry.KeyArrays[channel];
                        int[] times=mode==RetargetKeyframeMode.SourceBoneTimes?union:input.KeyArrays[channel].Entries.Select(k=>k._index).ToArray();
                        // Native CHR0 stores complete S/R/T groups. Retain the
                        // union on every axis of each required target group;
                        // an entirely bind-equivalent group stays absent.
                        if(mode==RetargetKeyframeMode.SourceBoneTimes&&!neededGroups[channel/3])times=new int[0];
                        if(times.Length==1&&times[0]!=0)throw new NotSupportedException("The source has one timed key away from frame zero on "+input.Name+" "+Channels[channel]+". Native fixed-channel storage cannot preserve that exact key layout.");
                        if(times.Any(t=>t<0||t>(sourceClip.Loop?sourceClip.FrameCount:sourceClip.FrameCount-1))||times.GroupBy(t=>t).Any(g=>g.Count()>2))
                            throw new InvalidDataException("The source key layout has unsupported timing or more than two sides on "+input.Name+" "+Channels[channel]+".");
                        plannedKeys=checked(plannedKeys+times.Length);
                        if(plannedKeys>2000000)throw new InvalidDataException("The projected source-key result exceeds two million keys; no partial candidate can be applied.");
                        var curve=new KeyframeArray(entry.Keyframes.FrameLimit,target.Bones[map.TargetIndex].BindState[channel]){Loop=candidate.Loop};
                        var seen=new HashSet<int>();
                        foreach(int time in times)
                        {
                            bool after=!seen.Add(time);
                            var key=original.GetKeyframe(time);
                            if(key==null)throw new InvalidDataException("The frozen dense reference lacks a source knot required by "+entry.Name+".");
                            if(after&&key.Second!=null)key=key.Second;
                            curve.SetFrameValue(time,original.GetFrameValue(time,after),true)._tangent=times.Length==1?0:key._tangent;
                        }
                        report.PairedKnots+=times.GroupBy(t=>t).Count(g=>g.Count()==2);
                        entry.Keyframes._keyArrays[channel]=curve;
                        if(times.Length==0&&original.KeyCount>0)omitted.Add(entry.Name+" "+Channels[channel]);
                    }
                    entry.UseModelScale=!Enumerable.Range(0,3).Any(c=>entry.KeyArrays[c].KeyCount>0);
                    entry.UseModelRotate=!Enumerable.Range(3,3).Any(c=>entry.KeyArrays[c].KeyCount>0);
                    entry.UseModelTranslate=!Enumerable.Range(6,3).Any(c=>entry.KeyArrays[c].KeyCount>0);
                    entry.Keyframes.PreserveInterpolation=true;entry.SignalPropertyChange();
                }
                if(candidate.Children.Any(e=>!target.Bones.Any(b=>b.Name==e.Name)))throw new InvalidDataException("A source-key candidate contains a track absent from its exact target.");
                report.OmittedTargetChannels=omitted.ToArray();report.OmittedTargetTracks=omittedTracks.ToArray();report.ResultKeys=Count(candidate);
                if(report.ResultKeys>2000000)throw new InvalidDataException("The projected source-key result exceeds two million keys.");
                progress?.Invoke(92,"Independently verifying source-key storage and motion...");
                // Encode both representations precisely. Reopen and compare
                // complete native metadata/coefficients, not merely durations.
                using(var precise=NativeAnimationOptimization.Copy(dense))precise.ExportChecked(beforePath);
                using(var reopened=NodeFactory.FromFile(null,beforePath) as CHR0Node)
                    if(reopened==null||NativeAnimationOptimization.ContentKey(reopened)!=report.DenseContent)throw new InvalidDataException("The dense reference changed during independent native encoding.");
                report.ResultContent=NativeAnimationOptimization.ContentKey(candidate);candidate.ExportChecked(afterPath);
                decoded=NodeFactory.FromFile(null,afterPath) as CHR0Node;
                if(decoded==null||NativeAnimationOptimization.ContentKey(decoded)!=report.ResultContent)throw new InvalidDataException("Native encoding did not preserve the proposed source key times, sides, values, tangents or flags.");
                report.DenseBytes=new FileInfo(beforePath).Length;report.ResultBytes=new FileInfo(afterPath).Length;report.VerifiedSha256=FighterProject.HashFile(afterPath);
                var model=target.Bones.First().Model;
                if(model==null)throw new InvalidDataException("Source-key verification requires the independently owned native target model.");
                Measure(model,target.Bones,dense,decoded,sourceClip,report,token,progress);
                if(NativeAnimationOptimization.ContentKey(dense)!=report.DenseContent)throw new InvalidOperationException("The frozen dense baseline changed during source-key preparation.");
                token.ThrowIfCancellationRequested();report.Validate();
                result.KeyframeMode=mode;result.KeyframeReport=report;result.DenseAnimation=dense;result.Animation=decoded;decoded=null;
            }
            finally
            {
                decoded?.Dispose();candidate.Dispose();
                foreach(string path in new[]{beforePath,afterPath})if(File.Exists(path))File.Delete(path);
                if(Directory.Exists(folder))Directory.Delete(folder);
            }
        }

        static void Measure(MDL0Node model,MDL0BoneNode[] bones,CHR0Node before,CHR0Node after,CHR0Node source,
            RetargetKeyframeReport result,CancellationToken token,Action<int,string> progress)
        {
            int last=before.Loop?before.FrameCount:before.FrameCount-1;
            var times=new SortedSet<float>{0,last};
            for(int f=0;f<last;f++)foreach(float offset in new[]{0f,.25f,.37f,.5f,.73f,.75f})times.Add(f+offset);
            // Both sides of native paired knots, including a loop closing knot,
            // are separate witnesses. Frame zero here is native animation time,
            // while the actual bind pose is checked separately below.
            foreach(var key in source.Children.OfType<CHR0EntryNode>().SelectMany(e=>e.KeyArrays).SelectMany(a=>a.Entries))
            {times.Add(key._index);if(key.Second!=null){if(key._index>0)times.Add(PreviousFloat(key._index+1)-1);if(key._index<last)times.Add(NextFloat(key._index+1)-1);}}
            var vertices=model.Objects.OfType<MDL0ObjectNode>().SelectMany(o=>o.Vertices??new List<Vertex3>()).ToArray();
            var budget=new NativeAnimationOptimizationBudget();budget.Require(times.Count+1,bones.Length,vertices.Length,"complete source-key pose and mesh comparison");
            var local=new float[bones.Length*9];var rotation=new Matrix[bones.Length];var world=new Matrix[bones.Length];var skin=new Vector3[vertices.Length];
            double worst=0;int count=0;
            foreach(float time in new[]{-1f}.Concat(times))
            {
                token.ThrowIfCancellationRequested();budget.Consume(bones.Length,vertices.Length);result.Samples++;
                model.ApplyCHR(time<0?null:before,time<0?0:time+1);
                for(int i=0;i<bones.Length;i++)
                {for(int c=0;c<9;c++)local[i*9+c]=bones[i].FrameState[c];rotation[i]=Matrix.RotationMatrix(bones[i].FrameState.Rotate);world[i]=bones[i]._frameMatrix;}
                for(int v=0;v<vertices.Length;v++)skin[v]=vertices[v].WeightedPosition;
                model.ApplyCHR(time<0?null:after,time<0?0:time+1);
                for(int i=0;i<bones.Length;i++)
                {
                    double translation=0,orientation=0;
                    for(int c=0;c<9;c++)
                    {
                        double error=Math.Abs(bones[i].FrameState[c]-(double)local[i*9+c]);Finite(error);
                        if(c<3)result.MaximumScaleError=Math.Max(result.MaximumScaleError,error);
                        else if(c<6)result.MaximumEulerError=Math.Max(result.MaximumEulerError,error);
                        else translation+=error*error;
                    }
                    result.MaximumLocalTranslationError=Math.Max(result.MaximumLocalTranslationError,Math.Sqrt(translation));
                    Matrix current=Matrix.RotationMatrix(bones[i].FrameState.Rotate);
                    for(int c=0;c<3;c++)for(int r=0;r<3;r++){double difference=current[c*4+r]-(double)rotation[i][c*4+r];orientation+=difference*difference;}
                    result.MaximumOrientationError=Math.Max(result.MaximumOrientationError,2*Math.Asin(Math.Min(1,Math.Sqrt(orientation)/(2*Math.Sqrt(2))))*180/Math.PI);
                    for(int c=0;c<4;c++)
                    {
                        double distance=0;
                        for(int r=0;r<3;r++){double difference=bones[i]._frameMatrix[c*4+r]-(double)world[i][c*4+r];distance+=difference*difference;}
                        double error=Math.Sqrt(distance);Finite(error);
                        if(c<3)result.MaximumWorldBasisError=Math.Max(result.MaximumWorldBasisError,error);
                        else {result.MaximumWorldBoneError=Math.Max(result.MaximumWorldBoneError,error);if(error>worst){worst=error;result.WorstFrame=Math.Max(0,time);result.WorstBone=bones[i].Name;}}
                    }
                }
                for(int v=0;v<vertices.Length;v++)
                {
                    Vector3 p=vertices[v].WeightedPosition;double dx=p._x-(double)skin[v]._x,dy=p._y-(double)skin[v]._y,dz=p._z-(double)skin[v]._z;
                    double error=Math.Sqrt(dx*dx+dy*dy+dz*dz);Finite(error);result.MaximumSkinError=Math.Max(result.MaximumSkinError,error);
                    if(error>worst){worst=error;result.WorstFrame=Math.Max(0,time);result.WorstBone="vertex "+v;}
                }
                if(++count%64==0)progress?.Invoke(92+count*7/Math.Max(1,times.Count+1),"Measuring source-key motion differences...");
            }
            result.BoneVisits=budget.BoneVisits;result.VertexVisits=budget.VertexVisits;
        }

        static int Count(CHR0Node clip)=>checked(clip.Children.OfType<CHR0EntryNode>().Sum(e=>e.KeyArrays.Sum(a=>a.KeyCount)));
        static bool SameBits(float a,float b)=>BitConverter.ToInt32(BitConverter.GetBytes(a),0)==BitConverter.ToInt32(BitConverter.GetBytes(b),0);
        static float PreviousFloat(int value){int bits=BitConverter.ToInt32(BitConverter.GetBytes((float)value),0);return BitConverter.ToSingle(BitConverter.GetBytes(bits-1),0);}
        static float NextFloat(int value){int bits=BitConverter.ToInt32(BitConverter.GetBytes((float)value),0);return BitConverter.ToSingle(BitConverter.GetBytes(bits+1),0);}
        static void Finite(double value){if(double.IsNaN(value)||double.IsInfinity(value))throw new InvalidDataException("Source-key native evaluation produced a non-finite value. The candidate was discarded.");}
    }
}
