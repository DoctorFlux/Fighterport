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
                if(input==null || !Enumerable.Range(6,3).Any(c=>input.Keyframes[c].KeyCount>0))continue;
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

        private static KeyframeArray CopyControlCurve(KeyframeArray source, int limit, bool loop, float baseline, float factor, float offset)
        {
            var result = new KeyframeArray(limit, baseline) { Loop = loop };
            // Traverse the actual linked keys: retain duplicate knots and turns.
            foreach (var key in source.Entries)
            {
                result.SetFrameValue(key._index, offset + key._value * factor, true)._tangent = key._tangent * factor;
            }
            return result;
        }

        private static void PreserveMotionControls(HumanoidRetargeter plan, CHR0Node source, CHR0Node output, BrawlRig nativeSource, BrawlRig nativeTarget, int last)
        {
            foreach (int index in plan.SourceMotionControls)
            {
                var entry = source.Children.OfType<CHR0EntryNode>().FirstOrDefault(e => e.Name == plan.Source.Joints[index].Name);
                if (entry != null && Enumerable.Range(0,9).Any(c => entry.Keyframes[c].KeyCount > 0) &&
                    !plan.Mapping.Any(m => m.SourceIndex == index && plan.IsTrajectory(m)))
                    throw new InvalidOperationException("An authored source motion control has no unambiguous target binding. Review the control hierarchy before retargeting.");
            }
            PreserveTrajectoryCurves(plan, source, output, last);
            foreach (int index in plan.TargetMotionControls)
            {
                var target = (CHR0EntryNode)output.FindChild(plan.Target.Joints[index].Name, false);
                var map = plan.Mapping.SingleOrDefault(m => m.TargetIndex == index && plan.IsTrajectory(m));
                var input = map == null ? null : source.Children.OfType<CHR0EntryNode>().FirstOrDefault(e => e.Name == map.Source);
                if (input == null || !Enumerable.Range(0,9).Any(c => input.Keyframes[c].KeyCount > 0))
                { target?.Remove(); continue; }
                var s = plan.Source.Joints[map.SourceIndex]; var t = plan.Target.Joints[index];
                Quaternion a = s.Parent < 0 ? Quaternion.Identity : plan.Source.Rest.Orientation[s.Parent];
                Quaternion b = t.Parent < 0 ? Quaternion.Identity : plan.Target.Rest.Orientation[t.Parent];
                bool sameRotation = Math.Abs(Quaternion.Dot(a,b)) > 1-1e-7f &&
                    Math.Abs(Quaternion.Dot(s.Bind.Rotation,t.Bind.Rotation)) > 1-1e-7f;
                for (int group = 0; group < 3; group++)
                {
                    bool authored = Enumerable.Range(group*3,3).Any(c => input.Keyframes[c].KeyCount > 0);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int channel = group*3+axis;
                        float baseline = group == 0 ? Component(t.Bind.Scale,axis) : group == 2 ? Component(t.Bind.Translation,axis) : Component(Euler(t.Bind.Rotation),axis);
                        if (!authored)
                            target.Keyframes._keyArrays[channel] = new KeyframeArray(target.Keyframes.FrameLimit,baseline) { Loop=output.Loop };
                        else if (group == 0 || group == 1 && sameRotation)
                        {
                            float factor = group == 0 ? Component(t.Bind.Scale,axis)/Component(s.Bind.Scale,axis) : 1;
                            target.Keyframes._keyArrays[channel] = CopyControlCurve(input.Keyframes[channel],target.Keyframes.FrameLimit,output.Loop,baseline,factor,0);
                        }
                    }
                }
                target.UseModelScale = !Enumerable.Range(0,3).Any(c=>input.Keyframes[c].KeyCount>0);
                target.UseModelRotate = !Enumerable.Range(3,3).Any(c=>input.Keyframes[c].KeyCount>0);
                target.UseModelTranslate = !Enumerable.Range(6,3).Any(c=>input.Keyframes[c].KeyCount>0);
                target.SignalPropertyChange();
            }
            // Converted Euler curves must meet a separate local-motion gate,
            // including times not used by the native curve fitter.
            foreach (float time in Enumerable.Range(0,last*4+1).Select(i=>i/4f)
                .Concat(Enumerable.Range(0,last).SelectMany(i=>new[]{i+.37f,i+.73f})))
            {
                var expected = plan.Evaluate(Sample(nativeSource,source,time),false).Pose;
                var actual = Sample(nativeTarget,output,time);
                foreach (int index in plan.TargetMotionControls)
                {
                    var e=expected.Local[index]; var v=actual.Local[index];
                    var difference = Quaternion.Normalize(e.Rotation * Quaternion.Inverse(v.Rotation));
                    double angle = 2*Math.Atan2(new Vector3(difference.X,difference.Y,difference.Z).Length(),Math.Abs(difference.W))*180/Math.PI;
                    if (Vector3.Distance(e.Translation,v.Translation)>.0002f || Vector3.Distance(e.Scale,v.Scale)>.00001f || angle>.01)
                        throw new InvalidOperationException("Source-authored motion control conversion exceeds its numeric gate. Review native axes/curves; no result was applied.");
                }
            }
        }

    }
}
