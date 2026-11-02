using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio
{
    // Edits an independent native BPE motion member. The original compressed
    // bytes remain the immutable reference and every publication reopens.
    internal sealed class Bt3NativeMotionEditor
    {
        private readonly byte[] original;
        private readonly byte[] decoded;
        private byte[] saved;
        private readonly Bt3NativeAssetReader.Skeleton skeleton;
        private readonly Bt3NativeAssetReader.Motion baseline;
        private readonly int action;
        private readonly Stack<Bt3NativeAssetReader.Motion> undo=new Stack<Bt3NativeAssetReader.Motion>();
        private readonly Stack<Bt3NativeAssetReader.Motion> redo=new Stack<Bt3NativeAssetReader.Motion>();
        internal Bt3NativeAssetReader.Motion Motion {get;private set;}
        internal bool Dirty=>undo.Count>0;
        internal bool CanUndo=>undo.Count>0;
        internal bool CanRedo=>redo.Count>0;
        internal bool HasUnsavedChanges=>!Export().SequenceEqual(saved);

        internal Bt3NativeMotionEditor(byte[] packed,Bt3NativeAssetReader.Skeleton target,int nativeAction)
        {
            if(packed==null||target==null)throw new ArgumentNullException();
            original=(byte[])packed.Clone();decoded=Bt3NativeAssetReader.DecodeBpe(original);
            saved=(byte[])original.Clone();
            skeleton=target;action=nativeAction;
            Motion=Bt3NativeAssetReader.ReadMotion(original,action);
            baseline=Copy(Motion);
            if(Motion.Tracks.Any(t=>!target.Bones.Any(b=>b.Id==t.Bone)))
                throw new InvalidDataException("The native motion has tracks outside the selected costume skeleton.");
        }
        private static Bt3NativeAssetReader.Motion Copy(Bt3NativeAssetReader.Motion source)
            =>new Bt3NativeAssetReader.Motion{Action=source.Action,Length=source.Length,EventCount=source.EventCount,
                SourceSha256=source.SourceSha256,Tracks=source.Tracks.Select(t=>new Bt3NativeAssetReader.Track{Bone=t.Bone,
                    RotationOnly=t.RotationOnly,Keys=t.Keys.Select(k=>new Bt3NativeAssetReader.Key{Frame=k.Frame,
                        Translation=k.Translation,Rotation=k.Rotation}).ToArray()}).ToArray()};
        private static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        internal void SetKey(int bone,int frame,Vector3 translation,Quaternion rotation)
        {
            if(bone<0||bone>=71||!skeleton.Bones.Any(b=>b.Id==bone)||frame<0||frame>Motion.Length||
                !Finite(translation.X)||!Finite(translation.Y)||!Finite(translation.Z)||
                !Finite(rotation.X)||!Finite(rotation.Y)||!Finite(rotation.Z)||!Finite(rotation.W)||
                rotation.LengthSquared()<0.0001f)
                throw new ArgumentOutOfRangeException(nameof(bone),"Choose a finite native bone pose and an in-range integer frame.");
            rotation=Quaternion.Normalize(rotation);
            var next=Copy(Motion);var track=next.Tracks.SingleOrDefault(t=>t.Bone==bone);
            if(track==null)
            {
                var rest=skeleton.Bones.Single(b=>b.Id==bone).Rest;
                var keys=new List<Bt3NativeAssetReader.Key>();
                if(frame!=0)keys.Add(new Bt3NativeAssetReader.Key{Frame=0,Translation=rest,Rotation=Quaternion.Identity});
                keys.Add(new Bt3NativeAssetReader.Key{Frame=frame,Translation=translation,Rotation=rotation});
                if(frame!=next.Length)keys.Add(new Bt3NativeAssetReader.Key{Frame=next.Length,Translation=rest,Rotation=Quaternion.Identity});
                track=new Bt3NativeAssetReader.Track{Bone=bone,RotationOnly=false,Keys=keys.ToArray()};
                next.Tracks=next.Tracks.Concat(new[]{track}).OrderBy(t=>t.Bone).ToArray();
            }
            else
            {
                var keys=track.Keys.ToList();int at=keys.FindIndex(k=>k.Frame>=frame);
                var key=new Bt3NativeAssetReader.Key{Frame=frame,Translation=translation,Rotation=rotation};
                if(at>=0&&keys[at].Frame==frame)keys[at]=key;
                else keys.Insert(at<0?keys.Count:at,key);
                // The authored rest translation for a rotation-only track is
                // retained until the user explicitly changes position.
                var rest=skeleton.Bones.Single(b=>b.Id==bone).Rest;
                if(track.RotationOnly&&translation!=rest)
                {track.RotationOnly=false;foreach(var old in keys)if(old.Frame!=frame)old.Translation=rest;}
                track.Keys=keys.ToArray();
            }
            SerializeAndVerify(next);
            undo.Push(Motion);redo.Clear();Motion=next;
        }
        internal void Undo(){if(!CanUndo)return;redo.Push(Motion);Motion=undo.Pop();}
        internal void Redo(){if(!CanRedo)return;undo.Push(Motion);Motion=redo.Pop();}

        private static ulong Pack(Quaternion q)
        {
            q=Quaternion.Normalize(q);float[] components={q.X,q.Y,q.Z,q.W};
            int largest=0;for(int i=1;i<4;i++)if(Math.Abs(components[i])>Math.Abs(components[largest]))largest=i;
            if(components[largest]<0)for(int i=0;i<4;i++)components[i]=-components[i];
            ulong bits=(ulong)largest<<60;int shift=0;
            for(int i=0;i<4;i++)if(i!=largest)
            {
                double scaled=(components[i]/1.4142135623730951+0.5)*1048575.0;
                long sample=(long)Math.Round(scaled,MidpointRounding.AwayFromZero);
                if(sample<0||sample>0xFFFFF)throw new InvalidDataException("A native quaternion component exceeds its packed range.");
                bits|=(ulong)sample<<(20*shift++);
            }
            return bits;
        }
        private static void U16(byte[] data,int at,ushort value)=>Array.Copy(BitConverter.GetBytes(value),0,data,at,2);
        private static void I32(byte[] data,int at,int value)=>Array.Copy(BitConverter.GetBytes(value),0,data,at,4);
        private static void Write(byte[] data,int at,byte[] value)=>Array.Copy(value,0,data,at,value.Length);
        private static bool Same(Bt3NativeAssetReader.Track a,Bt3NativeAssetReader.Track b)
        {
            if(a==null||b==null||a.Bone!=b.Bone||a.RotationOnly!=b.RotationOnly||a.Keys.Length!=b.Keys.Length)return false;
            for(int i=0;i<a.Keys.Length;i++)if(a.Keys[i].Frame!=b.Keys[i].Frame||
                a.Keys[i].Translation!=b.Keys[i].Translation||a.Keys[i].Rotation!=b.Keys[i].Rotation)return false;
            return true;
        }
        private static byte[] LiteralBpe(byte[] raw)
        {
            // One identity-table block. This is a legal BPE stream, but not a
            // size optimizer; the native game/archive size gate remains open.
            var result=new byte[raw.Length+13];I32(result,0,raw.Length);I32(result,4,raw.Length+5);
            result[8]=255;result[9]=128;result[10]=254;
            result[11]=(byte)(raw.Length>>8);result[12]=(byte)raw.Length;
            Array.Copy(raw,0,result,13,raw.Length);return result;
        }
        internal byte[] SerializeAndVerify(Bt3NativeAssetReader.Motion desired)
        {
            if(desired.Length!=Motion.Length||desired.EventCount!=Motion.EventCount||desired.Tracks.Length>71)
                throw new InvalidDataException("Native timing, events or track capacity changed.");
            var data=new List<byte>(decoded);
            foreach(var track in desired.Tracks)
            {
                var originalTrack=baseline.Tracks.SingleOrDefault(t=>t.Bone==track.Bone);
                if(Same(originalTrack,track))continue;
                // Copy-on-write retains all opaque source/event bytes. Append
                // changed track records and redirect only their native offsets.
                while((data.Count&3)!=0)data.Add(0);
                int offset=data.Count;
                if(offset/4>ushort.MaxValue||track.Keys.Length>4096)throw new InvalidDataException("Native motion track exceeds its offset or key bound.");
                int bytes=4+track.Keys.Length*(track.RotationOnly?10:24);
                if(data.Count+bytes>0xC000)throw new InvalidDataException("The edited motion exceeds the native decoded buffer bound.");
                data.AddRange(new byte[bytes]);var raw=data.ToArray();
                U16(raw,6+track.Bone*2,(ushort)(offset/4));
                U16(raw,offset,(ushort)(track.RotationOnly?1:0));U16(raw,offset+2,(ushort)track.Keys.Length);
                for(int i=0;i<track.Keys.Length;i++)
                {
                    var key=track.Keys[i];if(key.Frame<0||key.Frame>desired.Length+1)
                        throw new InvalidDataException("A native key lies outside its action duration.");
                    if(track.RotationOnly)
                    {Write(raw,offset+4+i*8,BitConverter.GetBytes(Pack(key.Rotation)));
                     U16(raw,offset+4+track.Keys.Length*8+i*2,(ushort)key.Frame);}
                    else
                    {Write(raw,offset+4+i*24,BitConverter.GetBytes(key.Translation.X));
                     Write(raw,offset+8+i*24,BitConverter.GetBytes(key.Translation.Y));
                     Write(raw,offset+12+i*24,BitConverter.GetBytes(key.Translation.Z));
                     I32(raw,offset+16+i*24,key.Frame);
                     Write(raw,offset+20+i*24,BitConverter.GetBytes(Pack(key.Rotation)));}
                }
                data=new List<byte>(raw);
            }
            var packed=LiteralBpe(data.ToArray());
            var reopened=Bt3NativeAssetReader.ReadMotion(packed,action);
            if(reopened.Length!=desired.Length||reopened.EventCount!=desired.EventCount||reopened.Tracks.Length!=desired.Tracks.Length)
                throw new InvalidDataException("The serialized motion changed timing, events or track membership.");
            foreach(var track in desired.Tracks)
            {
                var check=reopened.Tracks.Single(t=>t.Bone==track.Bone);
                if(check.RotationOnly!=track.RotationOnly||check.Keys.Length!=track.Keys.Length)
                    throw new InvalidDataException("The serialized native track layout changed.");
                for(int i=0;i<track.Keys.Length;i++)
                {
                    var a=track.Keys[i];var b=check.Keys[i];
                    if(a.Frame!=b.Frame||!track.RotationOnly&&a.Translation!=b.Translation||
                        Math.Abs(Quaternion.Dot(a.Rotation,b.Rotation))<0.999999f)
                        throw new InvalidDataException("A native key changed outside the verified packed-quaternion bound.");
                }
            }
            return packed;
        }
        internal byte[] Export()=>Dirty?SerializeAndVerify(Motion):(byte[])original.Clone();
        internal void MarkSaved(byte[] bytes){saved=(byte[])bytes.Clone();}
    }
}
