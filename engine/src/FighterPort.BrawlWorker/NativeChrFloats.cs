using System;
using System.IO;
using System.Linq;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;

// Repair the worker's decoded cache from the actual endian bits. A finite value can
// become a signaling NaN while byte-swapped through an x86 float, changing one bit.
// Original resource bytes remain immutable. Layouts follow the audited native codec.
internal static unsafe class NativeChrFloats
{
    static uint U32(byte* p)=>(uint)p[0]<<24|(uint)p[1]<<16|(uint)p[2]<<8|p[3];
    static int U16(byte* p)=>p[0]<<8|p[1];
    public static void Cache(CHR0Node clip)
    {
        byte* start=(byte*)clip.WorkingUncompressed.Address;long length=clip.WorkingUncompressed.Length;if(length<=0)length=U32(start+4);
        void Check(byte* p,long bytes){if(p<start||bytes<0||p-start+bytes>length)throw new InvalidDataException("CHR0 float data exceeds resource bounds.");}
        foreach(var entry in clip.Children.OfType<CHR0EntryNode>())
        {
            byte* header=(byte*)entry.WorkingUncompressed.Address;Check(header,8);uint flags=U32(header+4);byte* table=header+8;var collection=entry.Keyframes;
            for(int group=0;group<3;group++)if((flags&(1u<<(22+group)))!=0)
            {
                bool iso=(flags&(1u<<(4+group)))!=0;int columns=iso?1:3,format=(int)((flags>>(group==0?25:group==1?27:30))&(group==1?7u:3u));
                for(int axis=0;axis<columns;axis++,table+=4)
                {
                    Check(table,4);int channel=group*3+axis;bool fixedValue=(flags&(1u<<(13+group*3+(iso?2:axis))))!=0;
                    byte* data=fixedValue?table:header+U32(table);int count=fixedValue?1:format is 1 or 2 or 3?ReadCount(data):collection.FrameLimit;
                    int rowSize=format==1?4:format==2?6:format==3?12:format==4?1:format==5?2:4,offset=fixedValue||format==6?0:format==3?8:format is 1 or 2?16:8;
                    Check(data,offset+(long)rowSize*count);if(!fixedValue&&(format<1||format>6))throw new NotSupportedException("Unrecognized CHR0 curve coding.");
                    float step=!fixedValue&&format is 1 or 2?MorphFloatBytes.Read(data+8):!fixedValue&&format is 4 or 5?MorphFloatBytes.Read(data):0;
                    float basis=!fixedValue&&format is 1 or 2?MorphFloatBytes.Read(data+12):!fixedValue&&format is 4 or 5?MorphFloatBytes.Read(data+4):0;
                    for(int column=0;column<(iso?3:1);column++)
                    {
                        int ch=iso?group*3+column:channel;var old=collection._keyArrays[ch];var fresh=new KeyframeArray(old.FrameLimit,group==0?1:0){Loop=old.Loop};
                        for(int i=0;i<count;i++)
                        {
                            byte* row=data+offset+i*rowSize;int frame=i;float value,tangent=0;
                            if(fixedValue){frame=0;value=MorphFloatBytes.Read(data);}
                            else if(format==3){float time=MorphFloatBytes.Read(row);if(time!=(int)time)throw new NotSupportedException("Fractional CHR0 key indices require explicit conversion.");frame=(int)time;value=MorphFloatBytes.Read(row+4);tangent=MorphFloatBytes.Read(row+8);}
                            else if(format==1){uint packed=U32(row);frame=(int)(packed>>24);value=basis+((packed>>12)&4095)*step;tangent=((int)(packed<<20)>>20)/32f;}
                            else if(format==2){frame=U16(row)>>5;value=basis+U16(row+2)*step;tangent=(short)U16(row+4)/256f;}
                            else value=format==6?MorphFloatBytes.Read(row):basis+(format==5?U16(row):row[0])*step;
                            if(float.IsNaN(value)||float.IsInfinity(value)||float.IsNaN(tangent)||float.IsInfinity(tangent))throw new InvalidDataException("Nonfinite native animation key.");
                            var key=fresh.SetFrameValue(frame,value,true);if(!fixedValue&&format>=4)key.GenerateTangent();else key._tangent=tangent;
                        }
                        collection._keyArrays[ch]=fresh;
                    }
                }
            }
        }
        int ReadCount(byte* data){Check(data,2);return U16(data);}
    }
}
