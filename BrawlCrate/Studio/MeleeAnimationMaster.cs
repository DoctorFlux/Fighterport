using FighterPort.Adapters.Melee;
using HSDRaw.Tools;
using HSDRaw.Common.Animation;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace BrawlCrate.Studio
{
    // An editable, exact-storage authoring master. This is never represented as
    // an exported Melee FOBJ stream; the native writer validates its own output.
    internal static class MeleeAnimationMaster
    {
        private sealed class Segment
        {
            internal FOBJAnimState State;
            internal Tuple<double,double> At(double frame)
            {
                var s=State;double length=s.t1-s.t0;
                if(length<=0||s.op_intrp==GXInterpolationType.HSD_A_OP_CON||s.op_intrp==GXInterpolationType.HSD_A_OP_KEY)return Tuple.Create((double)s.p0,0d);
                double u=(frame-s.t0)/length;
                if(s.op_intrp==GXInterpolationType.HSD_A_OP_LIN)return Tuple.Create(s.p0+(s.p1-s.p0)*u,(s.p1-s.p0)/length);
                double a=2*s.p0-2*s.p1+length*(s.d0+s.d1),b=-3*s.p0+3*s.p1-length*(2*s.d0+s.d1),c=length*s.d0;
                return Tuple.Create(((a*u+b)*u+c)*u+s.p0,((3*a*u+2*b)*u+c)/length);
            }
        }
        internal static CHR0Node Prepare(MeleeNativeModel model,MeleeNativeMotion motion,string name)
        {
            if(motion.Figatree?.Nodes.Any(n=>n.Tracks.Any(t=>t.StartFrame!=0))==true)throw new NotSupportedException("Editable conversion of nonzero native FOBJ start offsets is not qualified. No timing is silently rebaked or truncated.");
            var playback=new MeleePlayback(model,motion);var bind=MeleeNative.Joints(model.Root);var output=new CHR0Node{Name=name,FrameCount=checked((int)Math.Ceiling(motion.LastFrame)+1),Loop=false};
            try
            {
                if(((long)Math.Ceiling(motion.LastFrame*8)+1)*playback.JointCount>2000000)
                    throw new InvalidDataException("Native authoring verification exceeds its sample budget.");
                int count=0;
                for(int bone=0;bone<playback.JointCount;bone++)
                {
                    var tracks=playback.Tracks(bone).Where(t=>MeleePlayback.Axis(t.TrackType)>=0&&t.Keys.Count>0).ToArray();if(tracks.Length==0)continue;
                    var entry=new PreciseCHR0EntryNode{Name=MeleeProjection.JointName(bone)};output.AddChild(entry);entry.Keyframes.PreserveInterpolation=true;
                    foreach(var track in tracks)
                    {
                        int axis=MeleePlayback.Axis(track.TrackType);double factor=axis>=3&&axis<6?180/Math.PI:1;
                        var times=track.Keys.Select(k=>k.Frame).Where(t=>t>0&&t<motion.LastFrame).Concat(new[]{0f,motion.LastFrame}).Distinct().OrderBy(t=>t).ToArray();
                        var segments=new Segment[Math.Max(0,times.Length-1)];for(int i=0;i<segments.Length;i++)segments[i]=new Segment{State=track.GetState((times[i]+times[i+1])*.5f)};
                        var array=entry.KeyArrays[axis];
                        Action<float,double,double> add=(time,value,tangent)=>{if(++count>2000000)throw new InvalidDataException("Native authoring master exceeds its two-million-key budget.");var key=array.SetFrameValue(time,(float)(value*factor),true);key._tangent=(float)(tangent*factor);};
                        for(int i=0;i<times.Length;i++)
                        {
                            float time=times[i];var incoming=i>0?segments[i-1].At(time):Tuple.Create((double)track.GetValue(time),0d);
                            var outgoing=i<segments.Length?segments[i].At(time):Tuple.Create((double)track.GetValue(time),0d);
                            double actual=track.GetValue(time);
                            if(Math.Abs(incoming.Item1-actual)>Math.Max(1e-7,Math.Abs(actual)*1e-7))
                            {
                                // Native float-time step: keep the incoming hold through
                                // the immediately preceding float, then its authored value.
                                int bits=BitConverter.ToInt32(BitConverter.GetBytes(time),0);float previous=BitConverter.ToSingle(BitConverter.GetBytes(bits-1),0);
                                if(i>0&&previous>times[i-1])add(previous,incoming.Item1,incoming.Item2);
                                incoming=Tuple.Create(actual,outgoing.Item2);
                            }
                            add(time,actual,incoming.Item2);
                            if(i<segments.Length&&(outgoing.Item2!=incoming.Item2||outgoing.Item1!=actual))add(time,outgoing.Item1,outgoing.Item2);
                        }
                        // Native scalar witnesses include original knots, quarter frames
                        // and withheld subframes. Raw Euler winding is never wrapped.
                        var witnesses=times.Concat(Enumerable.Range(0,checked((int)Math.Ceiling(motion.LastFrame*8)+1)).Select(i=>Math.Min(motion.LastFrame,i/8f))).Distinct();
                        foreach(float time in witnesses)
                        {
                            double expected=track.GetValue(time)*factor,actual=array.GetFrameValue(time);
                            double limit=axis>=3&&axis<6?.0003:Math.Max(.000002,Math.Abs(expected)*.000001);
                            if(double.IsNaN(expected)||double.IsInfinity(expected)||double.IsNaN(actual)||double.IsInfinity(actual)||Math.Abs(expected-actual)>limit)throw new InvalidDataException("Native authoring interpolation differs at joint "+bone+", channel "+axis+", frame "+time+": "+Math.Abs(expected-actual));
                        }
                    }
                    // CHR0 stores axes as S/R/T groups. An enabled group has
                    // fixed slots for its unanimated axes; write authentic bind
                    // values there instead of the generic zero/one defaults.
                    float[] rest={bind[bone].SX,bind[bone].SY,bind[bone].SZ,bind[bone].RX,bind[bone].RY,bind[bone].RZ,bind[bone].TX,bind[bone].TY,bind[bone].TZ};
                    for(int group=0;group<3;group++)if(Enumerable.Range(group*3,3).Any(axis=>entry.KeyArrays[axis].KeyCount>0))
                        for(int axis=group*3;axis<group*3+3;axis++)if(entry.KeyArrays[axis].KeyCount==0)
                            entry.KeyArrays[axis].SetFrameValue(0,(float)(rest[axis]*(axis>=3&&axis<6?180/Math.PI:1)))._tangent=0;
                }
                return output;
            }
            catch{output.Dispose();throw;}
        }
        internal static void VerifySerialized(MeleeNativeModel model,MeleeNativeMotion motion,CHR0Node master)
        {
            if(master==null)throw new InvalidDataException("Editable animation did not freshly reopen.");
            var playback=new MeleePlayback(model,motion);var entries=master.Children.Cast<CHR0EntryNode>().ToDictionary(e=>e.Name);
            long count=(long)Math.Ceiling(motion.LastFrame*8)+1;
            if(count*playback.JointCount>2000000)throw new InvalidDataException("Native authoring verification exceeds its sample budget.");
            var joints=MeleeNative.Joints(model.Root);
            for(int sample=0;sample<count;sample++)for(int joint=0;joint<joints.Length;joint++)
            {
                float time=Math.Min(motion.LastFrame,sample/8f);var expected=playback.Sample(joint,time);var j=joints[joint];
                float[] actual={j.SX,j.SY,j.SZ,j.RX,j.RY,j.RZ,j.TX,j.TY,j.TZ};
                if(entries.TryGetValue(MeleeProjection.JointName(joint),out var entry))for(int axis=0;axis<9;axis++)if(entry.KeyArrays[axis].KeyCount>0)actual[axis]=entry.KeyArrays[axis].GetFrameValue(time)*(axis>=3&&axis<6?(float)(Math.PI/180):1);
                for(int axis=0;axis<9;axis++)
                {
                    MeleeNative.Finite(actual[axis]);MeleeNative.Finite(expected[axis]);
                    double limit=axis>=3&&axis<6?.0003*Math.PI/180:Math.Max(.000002,Math.Abs(expected[axis])*.000001);
                    if(Math.Abs(actual[axis]-expected[axis])>limit)throw new InvalidDataException("Serialized editable native pose differs at joint "+joint+", channel "+axis+", frame "+time+".");
                }
            }
        }
    }
}
