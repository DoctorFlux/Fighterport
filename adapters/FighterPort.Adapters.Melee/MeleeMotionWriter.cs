using HSDRaw;
using HSDRaw.Common.Animation;
using HSDRaw.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace FighterPort.Adapters.Melee
{
    public sealed class MeleeCurveKey
    {
        public float Frame,Value,Incoming,Outgoing;
    }
    public sealed class MeleeCurveEdit
    {
        public int Joint,Axis;
        public MeleeCurveKey[] Keys;
    }
    public static class MeleeMotionWriter
    {
        // Float FOBJ payloads, no quantization and no implicit frame truncation.
        // Curve values/tangents are native units (radians for rotation).
        public static HSD_Track Encode(int axis,IReadOnlyList<MeleeCurveKey> keys)
        {
            if(axis<0||axis>8||keys==null||keys.Count==0||keys.Count>2000000)throw new InvalidDataException("Invalid native curve.");
            for(int i=0;i<keys.Count;i++)
            {
                var k=keys[i];foreach(float value in new[]{k.Frame,k.Value,k.Incoming,k.Outgoing})MeleeNative.Finite(value);
                if(k.Frame<0||k.Frame>10000000||k.Frame!=(float)Math.Floor(k.Frame)||i==0&&k.Frame!=0||i>0&&k.Frame<=keys[i-1].Frame)
                    throw new NotSupportedException("Melee FOBJ requires strictly increasing integer frame waits starting at zero. Fractional times are never truncated.");
            }
            byte[] payload;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                if(keys.Count==1){Packed(writer,6);writer.Write(keys[0].Value);}
                else for(int i=0;i<keys.Count;i++)
                {
                    var key=keys[i];Packed(writer,4);writer.Write(key.Value);writer.Write(key.Incoming);
                    Packed(writer,i+1<keys.Count?checked((int)(keys[i+1].Frame-key.Frame)):0);
                    if(i+1<keys.Count&&key.Incoming!=key.Outgoing){Packed(writer,5);writer.Write(key.Outgoing);}
                }
                writer.Flush();payload=stream.ToArray();
            }
            if(payload.Length>ushort.MaxValue)throw new InvalidDataException("Native FOBJ stream exceeds its 16-bit byte-length field.");
            var buffer=new byte[(payload.Length+3)&~3];Buffer.BlockCopy(payload,0,buffer,0,payload.Length);
            var result=new HSD_Track{TrackType=MeleePlayback.Track(axis),StartFrame=0,ValueFormat=GXAnimDataFormat.HSD_A_FRAC_FLOAT,TanFormat=GXAnimDataFormat.HSD_A_FRAC_FLOAT,ValueScale=1,TanScale=1,Buffer=buffer,DataLength=(ushort)payload.Length};
            var decoded=new FOBJ_Player{TrackType=result.TrackType,Keys=result.GetKeys()};
            foreach(var key in keys)
                if(decoded.GetValue(key.Frame)!=key.Value)throw new InvalidDataException("Native FOBJ boundary did not reopen exactly.");
            return result;
        }
        private static void Packed(BinaryWriter writer,int value)
        {if(value<0)throw new InvalidDataException("Negative native frame wait.");uint v=(uint)value;do{byte b=(byte)(v&127);v>>=7;writer.Write((byte)(b|(v==0?0:128)));}while(v!=0);}
        public static MeleeWriteCandidate Prepare(MeleeNativeMotion source,IReadOnlyList<MeleeCurveEdit> edits,float lastFrame,CancellationToken cancellation=default,int? nativeJointCount=null)
        {
            if(source?.Figatree==null)throw new NotSupportedException("Edited native writing currently requires a Figatree action member.");
            MeleeNative.Finite(lastFrame);if(lastFrame<0||lastFrame>10000000)throw new InvalidDataException("Invalid native ending frame.");
            if(edits==null||edits.Count>900000||edits.Select(e=>Tuple.Create(e.Joint,e.Axis)).Distinct().Count()!=edits.Count)throw new InvalidDataException("Ambiguous native curve edits.");
            var encoded=edits.Select(edit=>new{edit.Joint,edit.Axis,Track=edit.Keys.Length==0?null:Encode(edit.Axis,edit.Keys)}).ToArray();
            return MeleeNativeWriter.PrepareEditedSnapshot(source.Member,file=>
            {
                var fig=file.Roots.Single(r=>r.Name==source.Symbol).Data as HSD_FigaTree??throw new InvalidDataException("Exact native motion root is unavailable.");
                var rows=fig.Nodes;
                if(nativeJointCount.HasValue)
                {
                    if(nativeJointCount<rows.Count||nativeJointCount>100000||nativeJointCount<1)throw new InvalidDataException("Explicit native joint membership is inconsistent with the original action.");
                    int needed=encoded.Length==0?rows.Count:encoded.Max(e=>e.Joint)+1;
                    if(needed>nativeJointCount.Value)throw new InvalidDataException("Edited curve lies outside the explicitly identified native model.");
                    while(rows.Count<needed)rows.Add(new FigaTreeNode());
                }
                foreach(var edit in encoded)
                {
                    cancellation.ThrowIfCancellationRequested();if(edit.Joint<0||edit.Joint>=rows.Count)throw new InvalidDataException("Edited curve refers to a joint outside the native action.");
                    var tracks=rows[edit.Joint].Tracks;byte type=MeleePlayback.Track(edit.Axis);int index=tracks.FindIndex(t=>t.TrackType==type);
                    if(index>=0&&tracks[index].StartFrame!=0)throw new NotSupportedException("Edited writing of nonzero native FOBJ start offsets is not qualified.");
                    if(index>=0&&tracks[index].GetKeys().Any(k=>k.Frame>source.LastFrame))throw new NotSupportedException("Edited native terminal data beyond the stored action ending needs a qualified preservation rule. No terminal key was discarded.");
                    if(index>=0){if(edit.Track==null)tracks.RemoveAt(index);else tracks[index]=edit.Track;}else if(edit.Track!=null)tracks.Add(edit.Track);
                    if(tracks.Count>254)throw new InvalidDataException("Native Figatree track-count limit exceeded.");
                }
                if(encoded.Length>0)fig.Nodes=rows;fig.FrameCount=lastFrame;
            },cancellation);
        }
    }
}
