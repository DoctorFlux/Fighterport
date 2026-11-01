using HSDRaw.Common;
using HSDRaw.Common.Animation;
using HSDRaw.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace FighterPort.Adapters.Melee
{
    // Decode once during preparation. Sampling only evaluates these prepared native players.
    public sealed class MeleePlayback
    {
        private readonly HSD_JOBJ[] joints;
        private readonly FOBJ_Player[][] tracks;
        public int JointCount => joints.Length;
        public int KeyCount { get; }
        public float LastFrame { get; }
        private readonly bool?[] classicalScale;
        public bool UsesClassicalScale(int joint,bool nativeDefault)=>classicalScale[joint]??nativeDefault;
        public MeleePlayback(MeleeNativeModel model, MeleeNativeMotion motion)
        {
            joints=MeleeNative.Joints(model.Root); LastFrame=motion.LastFrame;
            classicalScale=new bool?[joints.Length];
            var rows=new List<FOBJ_Player[]>(); int keyCount=0;
            if(motion.Figatree!=null)
                foreach(var node in motion.Figatree.Nodes)
                {
                    if(rows.Count>=joints.Length)throw new InvalidDataException("Animation has more native joint rows than its associated model.");
                    if(node.Tracks.Count>0)classicalScale[rows.Count]=(motion.Figatree.Type&1)!=0;
                    rows.Add(node.Tracks.Select(t=>new FOBJ_Player { TrackType=t.TrackType, Keys=t.GetKeys() }).ToArray());
                }
            else foreach(var node in MeleeNative.AnimJoints(motion.AnimJoint))
            {
                if(rows.Count>=joints.Length)throw new InvalidDataException("Animation has more native joint rows than its associated model.");
                classicalScale[rows.Count]=(node.Flags&1)!=0;
                var list=new List<FOBJ_Player>(); var seen=new HashSet<HSDRaw.HSDStruct>();
                for(var desc=node.AOBJ?.FObjDesc;desc!=null;desc=desc.Next)
                {if(!seen.Add(desc._s))throw new InvalidDataException("Cyclic native FOBJ list.");list.Add(new FOBJ_Player(desc));}
                rows.Add(list.ToArray());
            }
            if(rows.Count>joints.Length)throw new InvalidDataException("Animation has more native joint rows than the exact associated model.");
            tracks=new FOBJ_Player[joints.Length][];
            for(int i=0;i<joints.Length;i++)
            {
                tracks[i]=i<rows.Count?rows[i]:new FOBJ_Player[0];
                var types=new HashSet<byte>();
                foreach(var track in tracks[i])
                {
                    if(!types.Add(track.TrackType))throw new InvalidDataException("Ambiguous duplicate native joint channels.");
                    keyCount+=track.Keys?.Count??0;
                    if(keyCount>2000000)throw new InvalidDataException("Native Melee animation exceeds the two-million-key preparation budget.");
                    foreach(var key in track.Keys??new List<FOBJKey>()){MeleeNative.Finite(key.Frame);MeleeNative.Finite(key.Value);MeleeNative.Finite(key.Tan);}
                }
            }
            KeyCount=keyCount;
        }
        public float[] Sample(int joint,float frame)
        {
            if(joint<0||joint>=joints.Length)throw new ArgumentOutOfRangeException(nameof(joint));MeleeNative.Finite(frame);
            var values=new float[9];SampleInto(joint,frame,values);return values;
        }
        public void SampleInto(int joint,float frame,float[] values)
        {
            if(joint<0||joint>=joints.Length||values==null||values.Length<9)throw new ArgumentException("Invalid native pose destination.");MeleeNative.Finite(frame);
            var j=joints[joint];values[0]=j.SX;values[1]=j.SY;values[2]=j.SZ;values[3]=j.RX;values[4]=j.RY;values[5]=j.RZ;values[6]=j.TX;values[7]=j.TY;values[8]=j.TZ;
            if(frame<0)return;
            foreach(var track in tracks[joint])
            {
                int axis=Axis(track.TrackType);if(axis<0||track.Keys==null||track.Keys.Count==0)continue;
                values[axis]=track.GetValue(frame);MeleeNative.Finite(values[axis]);
            }
        }
        public static int Axis(byte track)=>track>=1&&track<=3?track+2:track>=5&&track<=7?track+1:track>=8&&track<=10?track-8:-1;
        public static byte Track(int axis)=>(byte)(axis<3?axis+8:axis<6?axis-2:axis-1);
        public IReadOnlyList<FOBJ_Player> Tracks(int joint)=>tracks[joint];
    }
}
