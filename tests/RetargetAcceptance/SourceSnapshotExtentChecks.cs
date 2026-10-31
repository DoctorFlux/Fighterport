using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.IO;
using System.Linq;

internal static partial class Program
{
    static void SourceSnapshotExtentChecks(string output)
    {
        string directory=Path.Combine(output,"bounded-source-N");Directory.CreateDirectory(directory);
        int Read(byte[] data,int at)=>data[at]<<24|data[at+1]<<16|data[at+2]<<8|data[at+3];
        void Write(byte[] data,int at,int value){for(int i=0;i<4;i++)data[at+i]=(byte)((uint)value>>(24-8*i));}
        float Value(int axis,int frame)=>axis==0&&frame==0?BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue),0):axis*137+frame*frame-7*frame+17;
        byte[] Fixture(int version,int frames,int gap=0)
        {
            string template=Path.Combine(directory,"template-"+version+"-"+frames+".chr0");int capacity=frames+2;
            using(var clip=new CHR0Node{Name="Original procedural N extent",Version=version,FrameCount=capacity})
            {
                var entry=clip.CreateEntry("Unfamiliar source bone");var curves=new IndexedCHR0CurveStorage[9];
                for(int axis=0;axis<3;axis++)
                {
                    var values=Enumerable.Range(0,capacity+1).Select(frame=>Value(axis,Math.Min(frame,capacity-1))).ToArray();
                    entry.Keyframes._keyArrays[3+axis]=DenseCHR0Projection.Create(values.Take(capacity).ToArray(),capacity);
                    curves[3+axis]=IndexedCHR0CurveStorage.CreateFloatDense(values,capacity);
                }
                PreciseCHR0EntryNode.RestoreStorage(entry,new IndexedCHR0EntryStorage(curves));clip.ExportChecked(template);
            }
            var bytes=File.ReadAllBytes(template);int group=Read(bytes,0x10),entryOffset=group+Read(bytes,group+0x24),code=Read(bytes,entryOffset+4);
            int scaleCells=(code>>22&1)==0?0:(code&16)!=0?1:3;int rotation=entryOffset+8+scaleCells*4,begin=entryOffset+Read(bytes,rotation);
            Check((code>>27&7)==6,"Synthetic source fixture independently locates real native L4 payload cells");
            for(int axis=0;axis<3;axis++)
            {
                int address=begin+axis*(frames*4+gap);Write(bytes,rotation+axis*4,address-entryOffset);
                for(int frame=0;frame<frames;frame++)Write(bytes,address+frame*4,BitConverter.ToInt32(BitConverter.GetBytes(Value(axis,frame)),0));
            }
            Write(bytes,4,begin+3*(frames*4+gap));int frameOffset=version==4?0x1c:0x20;bytes[frameOffset]=(byte)(frames>>8);bytes[frameOffset+1]=(byte)frames;
            return bytes;
        }
        foreach(int version in new[]{4,5})foreach(int frames in new[]{1,3,33})
        {
            string path=Path.Combine(directory,"source-N-"+version+"-"+frames+".chr0");var bytes=Fixture(version,frames);File.WriteAllBytes(path,bytes);
            string hash=FighterProject.HashFile(path);
            using(var source=NodeFactory.FromFile(null,path) as CHR0Node)
            {
                var original=source.Children.OfType<CHR0EntryNode>().Single();string content=NativeAnimationOptimization.SourceContentKey(source);
                Check(!IndexedCHR0EntryStorage.CaptureForWrite(original).Accepted&&IndexedCHR0EntryStorage.CaptureForSourceSnapshot(original).Accepted,
                    "Bounded source-N proof enables only read-only snapshots; ordinary native writing stays refused",new{version,frames});
                using(var snapshot=NativeAnimationOptimization.CopySource(source))
                {
                    string frozen=Path.Combine(directory,"snapshot-"+version+"-"+frames+".chr0");snapshot.ExportChecked(frozen);
                    using(var fresh=NodeFactory.FromFile(null,frozen) as CHR0Node)
                    {
                        Check(NativeAnimationOptimization.SourceContentKey(fresh)==content,"N-sample source snapshot freshly reopens with every exact knot/tangent/flag and native timing");
                        double maximum=0;bool exact=true;
                        foreach(float time in Enumerable.Range(0,frames*8+1).Select(i=>i/8f))for(int axis=0;axis<3;axis++)
                        {
                            float clamped=Math.Min(time,frames-1);int first=(int)clamped,last=Math.Min(first+1,frames-1);
                            float expected=Value(axis,first)+(clamped-first)*(Value(axis,last)-Value(axis,first));
                            float actual=original.GetFrameValue(3+axis,time),saved=((CHR0EntryNode)fresh.Children[0]).GetFrameValue(3+axis,time);
                            maximum=Math.Max(maximum,Math.Abs(actual-expected));exact&=BitConverter.ToInt32(BitConverter.GetBytes(actual),0)==BitConverter.ToInt32(BitConverter.GetBytes(saved),0);
                        }
                        Check(exact&&maximum<.00001,"Independent raw-sample interpolation and fresh I12 evaluation preserve final holds without borrowing the next axis as a terminal",new{version,frames,maximum});
                    }
                }
                Check(NativeAnimationOptimization.SourceContentKey(source)==content&&FighterProject.HashFile(path)==hash,"Read-only N-sample preparation preserves exact original native content and bytes");
            }
            foreach(string mutation in new[]{"coefficient","timing","loop","gap","nonfinite"})
            {
                string invalid=Path.Combine(directory,"refuse-"+version+"-"+frames+"-"+mutation+".chr0");var altered=mutation=="gap"?Fixture(version,frames,8):(byte[])bytes.Clone();
                if(mutation=="nonfinite")
                {int group=Read(altered,0x10),entry=group+Read(altered,group+0x24),code=Read(altered,entry+4),cells=(code>>22&1)==0?0:(code&16)!=0?1:3;Write(altered,entry+Read(altered,entry+8+cells*4),unchecked((int)0x7fc00000));}
                File.WriteAllBytes(invalid,altered);
                using(var source=NodeFactory.FromFile(null,invalid) as CHR0Node)
                {
                    var entry=(CHR0EntryNode)source.Children[0];
                    if(mutation=="coefficient")entry.KeyArrays[3].Entries.First()._value+=.25f;
                    if(mutation=="timing")source.FrameCount++;
                    if(mutation=="loop")source.Loop=true;
                    Check(!IndexedCHR0EntryStorage.CaptureForSourceSnapshot(entry).Accepted,"Source snapshot refuses edited/changed/gapped/nonfinite dense contexts without inventing terminal data",new{version,frames,mutation});
                }
            }
        }
    }
}
