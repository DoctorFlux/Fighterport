using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;

namespace BrawlCrate.Studio
{
    // Source frame numbers are zero based in storage. Resampling is offline,
    // before the unchanged Next solver; the authentic library entry is immutable.
    public sealed class NativeTimingFit
    {
        public int Start {get;set;}
        public int End {get;set;}
        public int Frames {get;set;}
        public void Validate(int sourceFrames)
        {
            if(Start<0||End<Start||End>=sourceFrames||Frames<1||Frames>4096)throw new InvalidDataException("Choose a source frame range and 1–4096 output frames.");
        }
        public bool Identity(int sourceFrames)=>Start==0&&End==sourceFrames-1&&Frames==sourceFrames;
        public CHR0Node Resample(CHR0Node source)
        {
            Validate(source.FrameCount);
            var output=new CHR0Node{Name=source.Name,FrameCount=Frames,Loop=false};
            try
            {
                foreach(var entry in source.Children.OfType<CHR0EntryNode>())
                {
                    var target=new CHR0EntryNode{Name=entry.Name,UseModelScale=entry.UseModelScale,UseModelRotate=entry.UseModelRotate,UseModelTranslate=entry.UseModelTranslate,
                        ScaleCompensateApply=entry.ScaleCompensateApply,ScaleCompensateParent=entry.ScaleCompensateParent,ClassicScaleOff=entry.ClassicScaleOff};output.AddChild(target);
                    for(int channel=0;channel<9;channel++)if(entry.KeyArrays[channel].KeyCount>0)
                        for(int frame=0;frame<Frames;frame++)
                        {
                            float time=Frames==1?Start:Start+(End-Start)*(frame/(float)(Frames-1));
                            target.SetKeyframe(channel,frame,entry.GetFrameValue(channel,time),true,true);
                        }
                }
                return output;
            }
            catch{output.Dispose();throw;}
        }
    }
}
