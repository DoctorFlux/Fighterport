using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.IO;
using System.Linq;

namespace BrawlCrate.Studio.Retargeting
{
    // Baked output has a key at every integer. Index the existing native keys
    // once, retaining BrawlLib's exact Hermite evaluator and live tangent edits.
    // This avoids walking hundreds of linked keys for every fitting sample.
    internal sealed class DenseCurveSampler
    {
        private readonly BrawlRig target;
        private readonly int last;
        private readonly KeyframeArray[][] fixedCurves;
        internal KeyframeEntry[][][] Keys {get;}
        internal DenseCurveSampler(BrawlRig rig,CHR0Node animation,int lastFrame)
        {
            target=rig;last=lastFrame;
            var entries=animation.Children.OfType<CHR0EntryNode>().ToDictionary(e=>e.Name);
            Keys=new KeyframeEntry[rig.Bones.Length][][];
            fixedCurves=new KeyframeArray[rig.Bones.Length][];
            for(int bone=0;bone<rig.Bones.Length;bone++)
            {
                Keys[bone]=new KeyframeEntry[9][];
                fixedCurves[bone]=new KeyframeArray[9];
                for(int channel=0;channel<9;channel++)
                {
                    Keys[bone][channel]=new KeyframeEntry[last+1];
                    var key=entries[rig.Bones[bone].Name].Keyframes[channel].GetKeyframe(0);
                    for(int frame=0;frame<=last;frame++)
                    {
                        if(key==null || key._index!=frame || key.Second!=null)
                        {
                            int pelvis=rig.Rig.Roles["pelvis"];
                            if(channel<6 || bone==pelvis || !rig.Rig.Ancestors[pelvis][bone])throw new InvalidDataException("Native curve fitting requires dense anatomical keys.");
                            fixedCurves[bone][channel]=entries[rig.Bones[bone].Name].Keyframes[channel];break;
                        }
                        Keys[bone][channel][frame]=key;key=key._next;
                    }
                }
            }
        }
        internal RigPose Sample(float time,RigPose reference=null,int[] changedBones=null)
        {
            time=RigMath.Clamp(time,0,last);int a=(int)time,b=Math.Min(last,a+1);float offset=time-a;
            var local=reference==null?new JointTransform[target.Bones.Length]:(JointTransform[])reference.Local.Clone();
            var bones=reference==null?Enumerable.Range(0,local.Length):changedBones.AsEnumerable();
            foreach(int bone in bones)
            {
                FrameState state=target.Bones[bone].BindState;
                for(int channel=0;channel<9;channel++)state[channel]=fixedCurves[bone][channel]!=null?fixedCurves[bone][channel].GetFrameValue(time):
                    offset==0?Keys[bone][channel][a]._value:Keys[bone][channel][a].Interpolate(offset,1,Keys[bone][channel][b]);
                local[bone]=BrawlRetargeter.Transform(state);
            }
            return reference==null?new RigPose(target.Rig,local):new RigPose(reference,local,changedBones);
        }
    }
}
