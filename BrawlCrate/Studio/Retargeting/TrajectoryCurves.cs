using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio.Retargeting
{
    public static partial class BrawlRetargeter
    {
        // Trajectory is authoritative. Contact fitting may change anatomy below
        // it, but must not rescale/re-fit the actor's authored movement curves.
        private static void PreserveTrajectoryCurves(HumanoidRetargeter plan,CHR0Node source,CHR0Node output,int last)
        {
            foreach(var map in plan.Mapping.Where(plan.IsTrajectory))
            {
                var input=source.Children.OfType<CHR0EntryNode>().FirstOrDefault(e=>e.Name==map.Source);
                if(input==null)continue;
                var target=(CHR0EntryNode)output.FindChild(map.Target,false);
                var axes=new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ}.Select(v=>plan.TransferTranslationDelta(map,v)).ToArray();
                var sourceBind=plan.Source.Joints[map.SourceIndex].Bind.Translation;
                var targetBind=plan.Target.Joints[map.TargetIndex].Bind.Translation;
                for(int component=0;component<3;component++)
                {
                    var curve=new KeyframeArray(target.Keyframes.FrameLimit,Component(targetBind,component)){Loop=output.Loop};
                    float[] coefficients=axes.Select(v=>Component(v,component)).ToArray();
                    int axis=Array.FindIndex(coefficients,c=>Math.Abs(Math.Abs(c)-1)<0.00001f);
                    if(axis>=0 && coefficients.Where((v,i)=>i!=axis).All(v=>Math.Abs(v)<0.00001f))
                    {
                        // Identical/permuted coordinate axes retain original
                        // values, key times, duplicate keys and cubic tangents.
                        for(int frame=0;frame<=last;frame++)
                        {
                            var key=input.Keyframes[axis+6].GetKeyframe(frame);
                            if(key==null)continue;
                            do{
                                var copied=curve.SetFrameValue(frame,Component(targetBind,component)+coefficients[axis]*(key._value-Component(sourceBind,axis)),true);
                                copied._tangent=coefficients[axis]*key._tangent;
                                key=key._next;
                            }while(key._index==frame);
                        }
                        if(curve.KeyCount==0)curve.SetFrameValue(0,Component(targetBind,component));
                    }
                    else
                    {
                        // A fixed axis calibration is a linear combination of
                        // source cubics. Split at their union of knots and retain
                        // separate incoming/outgoing values and derivatives.
                        var knots=new SortedSet<int>{0,last};
                        for(int frame=0;frame<=last;frame++)for(int c=6;c<9;c++)
                            if(input.Keyframes[c].GetKeyframe(frame)!=null)knots.Add(frame);
                        int[] times=knots.ToArray();var incoming=new float[times.Length];var outgoing=new float[times.Length];
                        Func<float,bool,float> value=(time,after)=>{
                            Vector3 local=sourceBind;
                            if(input.Keyframes[6].KeyCount>0)local.X=input.Keyframes[6].GetFrameValue(time,after);
                            if(input.Keyframes[7].KeyCount>0)local.Y=input.Keyframes[7].GetFrameValue(time,after);
                            if(input.Keyframes[8].KeyCount>0)local.Z=input.Keyframes[8].GetFrameValue(time,after);
                            return Component(targetBind+plan.TransferTranslationDelta(map,local-sourceBind),component);
                        };
                        for(int i=0;i<times.Length-1;i++)
                        {
                            float span=times[i+1]-times[i],a=value(times[i],true),b=value(times[i]+span/3,false),
                                c=value(times[i]+span*2/3,false),d=value(times[i+1],false);
                            outgoing[i]=(-11*a+18*b-9*c+2*d)/(2*span);
                            incoming[i+1]=(11*d-18*c+9*b-2*a)/(2*span);
                        }
                        if(times.Length>1){incoming[0]=outgoing[0];outgoing[times.Length-1]=incoming[times.Length-1];}
                        for(int i=0;i<times.Length;i++)
                        {
                            float a=value(times[i],false),b=value(times[i],true);
                            curve.SetFrameValue(times[i],a,true)._tangent=incoming[i];
                            if(Math.Abs(a-b)>0.000001f || Math.Abs(incoming[i]-outgoing[i])>0.000001f)
                                curve.SetFrameValue(times[i],b,true)._tangent=outgoing[i];
                        }
                    }
                    target.Keyframes._keyArrays[component+6]=curve;
                }
                target.SignalPropertyChange();
            }
        }
        private static float Component(Vector3 value,int axis)=>axis==0?value.X:axis==1?value.Y:value.Z;
    }
}
