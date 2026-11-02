using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace BrawlCrate.Studio
{
    // Read-only PS2 asset interpretation. Neither these values nor a Brawl
    // projection replace the native pmdl, bind data, weights or source bytes.
    internal static class Bt3NativeAssetReader
    {
        internal sealed class Bone
        {
            internal int Id,Parent,RecordOffset;
            internal Vector3 Rest;
            internal bool HasMesh;
        }
        internal sealed class Skeleton
        {
            internal Bone[] Bones;
            internal string SourceSha256;
        }
        internal sealed class Key
        {
            internal int Frame;
            internal Vector3 Translation;
            internal Quaternion Rotation;
        }
        internal sealed class Track
        {
            internal int Bone;
            internal bool RotationOnly;
            internal Key[] Keys;
        }
        internal sealed class Motion
        {
            internal int Action,Length,EventCount;
            internal Track[] Tracks;
            internal string SourceSha256;
        }
        internal sealed class Pose
        {
            internal Vector3[] Translation;
            internal Quaternion[] Rotation;
        }
        private static ushort U16(byte[] data,int at)=>BitConverter.ToUInt16(data,at);
        private static int I32(byte[] data,int at)=>BitConverter.ToInt32(data,at);
        private static uint U32(byte[] data,int at)=>BitConverter.ToUInt32(data,at);
        private static string Hash(byte[] data)
        {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();
        }
        private static void Bounds(byte[] data,long at,long bytes,string what)
        {
            if(at<0||bytes<0||at+bytes>data.Length)throw new InvalidDataException("BT3 "+what+" exceeds the native member.");
        }
        private static Vector3 Vec3(byte[] data,int at,string what)
        {
            Bounds(data,at,12,what);
            var x=BitConverter.ToSingle(data,at);var y=BitConverter.ToSingle(data,at+4);var z=BitConverter.ToSingle(data,at+8);
            if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||float.IsNaN(z)||float.IsInfinity(z))
                throw new InvalidDataException("BT3 "+what+" contains a nonfinite coordinate.");
            return new Vector3(x,y,z);
        }
        internal static Skeleton ReadSkeleton(byte[] pmdl)
        {
            if(pmdl==null||pmdl.Length<0x70||pmdl[0]!='p'||pmdl[1]!='m'||pmdl[2]!='d'||pmdl[3]!='l')
                throw new InvalidDataException("The selected BT3 model member is not pmdl.");
            int at=I32(pmdl,0x6C);var list=new List<Bone>();var stack=new Stack<int>();var ids=new HashSet<int>();
            for(int i=0;i<4096;i++)
            {
                Bounds(pmdl,at,0x40,"model node");
                int next=I32(pmdl,at),pop=U16(pmdl,at+4),last=U16(pmdl,at+6),id=U16(pmdl,at+0xA);
                if((last==0&&next<0x40)||(last!=0&&next!=0&&next<0x40)||
                    next>pmdl.Length-at||pop>stack.Count+1||!ids.Add(id))
                    throw new InvalidDataException("BT3 pmdl node chain is invalid.");
                var rest=Vec3(pmdl,at+0x30,"model rest transform");
                list.Add(new Bone{Id=id,Parent=stack.Count==0?-1:stack.Peek(),RecordOffset=at,Rest=rest,HasMesh=next>0x40});
                stack.Push(id);
                for(int j=0;j<pop;j++)stack.Pop();
                if(last!=0)
                {
                    if(list.Count<2)throw new InvalidDataException("BT3 pmdl node chain is incomplete.");
                    return new Skeleton{Bones=list.ToArray(),SourceSha256=Hash(pmdl)};
                }
                at=checked(at+next);
            }
            throw new InvalidDataException("BT3 pmdl node chain did not terminate within the work bound.");
        }
        internal static byte[] DecodeBpe(byte[] source)
        {
            if(source==null||source.Length<10)throw new InvalidDataException("BT3 motion has no BPE header.");
            int size=I32(source,0),packed=I32(source,4);
            if(size<0x94||size>0xC000||packed<3||packed>source.Length-8)
                throw new InvalidDataException("BT3 BPE size exceeds the game's bounded motion buffer.");
            int end=8+packed,at=8,written=0,steps=0;var result=new byte[size];
            var left=new byte[256];var right=new byte[256];var pending=new Stack<byte>();
            byte Next()
            {
                if(at>=end)throw new InvalidDataException("Truncated BT3 BPE motion.");
                return source[at++];
            }
            while(at<end)
            {
                for(int i=0;i<256;i++)left[i]=(byte)i;
                int symbol=0;
                while(symbol<256)
                {
                    int run=Next();
                    int count;
                    if(run>127){symbol+=run-127;if(symbol==256)break;count=1;}
                    else count=run+1;
                    if(symbol>256)throw new InvalidDataException("BT3 BPE table skip exceeds 256 symbols.");
                    if(symbol+count>256)throw new InvalidDataException("BT3 BPE symbol table exceeds 256 entries.");
                    for(int i=0;i<count;i++,symbol++)
                    {
                        left[symbol]=Next();
                        if(left[symbol]!=symbol)right[symbol]=Next();
                    }
                }
                int encoded=(Next()<<8)|Next();pending.Clear();
                for(int i=0;i<encoded||pending.Count>0;)
                {
                    byte code;
                    if(pending.Count>0)code=pending.Pop();else{code=Next();i++;}
                    if(++steps>size*64||pending.Count>8192)throw new InvalidDataException("BT3 BPE expansion exceeded its work bound.");
                    if(left[code]==code)
                    {
                        if(written>=size)throw new InvalidDataException("BT3 BPE motion exceeded its declared length.");
                        result[written++]=code;
                    }
                    else{pending.Push(right[code]);pending.Push(left[code]);}
                }
            }
            if(written!=size||at!=end)throw new InvalidDataException("BT3 BPE motion length does not match its header.");
            return result;
        }
        internal static Quaternion UnpackQuaternion(ulong bits)
        {
            int largest=(int)(bits>>60);if(largest>3)throw new InvalidDataException("BT3 packed rotation has an invalid component index.");
            var values=new float[4];float sum=0;
            for(int i=0,j=0;i<4;i++)
            {
                if(i==largest)continue;
                int sample=(int)((bits>>(j*20))&0xFFFFF);j++;
                float value=(sample/1048575f-0.5f)*1.41421356f;
                values[i]=value;sum+=value*value;
            }
            if(sum>1.00001f)throw new InvalidDataException("BT3 packed rotation is outside the unit sphere.");
            values[largest]=(float)Math.Sqrt(Math.Max(0,1-sum));
            return new Quaternion(values[0],values[1],values[2],values[3]);
        }
        internal static Motion ReadMotion(byte[] packed,int action)
        {
            var data=DecodeBpe(packed);int events=data[1],length=U16(data,2);
            Bounds(data,0x94,events*16,"motion event table");
            var tracks=new List<Track>();
            for(int bone=0;bone<71;bone++)
            {
                int offset=U16(data,6+bone*2)*4;
                if(offset==0)continue;
                Bounds(data,offset,4,"motion track");
                int flags=U16(data,offset),keys=U16(data,offset+2);
                if((flags&~1)!=0||keys<1||keys>4096)throw new InvalidDataException("BT3 motion track has unsupported flags or key count.");
                bool rotationOnly=(flags&1)!=0;long bytes=4L+keys*(rotationOnly?8L:24L)+(rotationOnly?keys*2L:0);
                Bounds(data,offset,bytes,"motion track keys");
                var values=new Key[keys];int prior=-1;
                for(int i=0;i<keys;i++)
                {
                    int frame=rotationOnly?U16(data,offset+4+keys*8+i*2):checked((int)U32(data,offset+4+i*24+12));
                    if(frame<=prior||frame>length+1)throw new InvalidDataException("BT3 motion key times are not strictly ordered within the clip.");
                    int quatAt=rotationOnly?offset+4+i*8:offset+4+i*24+16;
                    var rotation=UnpackQuaternion(BitConverter.ToUInt64(data,quatAt));
                    var translation=rotationOnly?Vector3.Zero:Vec3(data,offset+4+i*24,"motion translation");
                    values[i]=new Key{Frame=frame,Translation=translation,Rotation=rotation};prior=frame;
                }
                tracks.Add(new Track{Bone=bone,RotationOnly=rotationOnly,Keys=values});
            }
            if(tracks.Count==0)throw new InvalidDataException("BT3 motion has no decoded body tracks.");
            return new Motion{Action=action,Length=length,EventCount=events,Tracks=tracks.ToArray(),SourceSha256=Hash(packed)};
        }
        // Native Bobj playback uses local rest translation for an absent or
        // rotation-only track, linear position and quaternion slerp between
        // authored keys. This samples already-decoded owned bytes only.
        internal static Pose Sample(Skeleton skeleton,Motion motion,float frame)
        {
            if(skeleton==null||motion==null||float.IsNaN(frame)||float.IsInfinity(frame)||frame<0||frame>motion.Length)
                throw new ArgumentOutOfRangeException(nameof(frame));
            var translation=new Vector3[skeleton.Bones.Length];var rotation=new Quaternion[skeleton.Bones.Length];
            var tracks=motion.Tracks.ToDictionary(track=>track.Bone);
            for(int i=0;i<skeleton.Bones.Length;i++)
            {
                var bone=skeleton.Bones[i];translation[i]=bone.Rest;rotation[i]=Quaternion.Identity;
                if(!tracks.TryGetValue(bone.Id,out Track track))continue;
                Key[] keys=track.Keys;
                if(keys.Length==1)
                {
                    if(!track.RotationOnly)translation[i]=keys[0].Translation;
                    rotation[i]=keys[0].Rotation;continue;
                }
                // The game returns its zero/identity fallback outside the
                // authored range. Rotation-only tracks keep model rest pos.
                if(frame<keys[0].Frame||frame>keys[keys.Length-1].Frame)
                {
                    if(!track.RotationOnly)translation[i]=Vector3.Zero;
                    continue;
                }
                int lo=0,hi=keys.Length-1;
                while(lo<hi)
                {
                    int mid=(lo+hi)/2;
                    if(keys[mid].Frame<frame)lo=mid+1;else hi=mid;
                }
                if(keys[lo].Frame==frame)
                {
                    if(!track.RotationOnly)translation[i]=keys[lo].Translation;
                    rotation[i]=keys[lo].Rotation;continue;
                }
                int before=lo-1;
                float fraction=(frame-keys[before].Frame)/(keys[lo].Frame-keys[before].Frame);
                if(!track.RotationOnly)translation[i]=Vector3.Lerp(keys[before].Translation,keys[lo].Translation,fraction);
                rotation[i]=Quaternion.Slerp(keys[before].Rotation,keys[lo].Rotation,fraction);
            }
            return new Pose{Translation=translation,Rotation=rotation};
        }
    }
}
