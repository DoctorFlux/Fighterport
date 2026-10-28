using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    public static partial class BrawlRetargeter
    {
        private static bool ConstantNativeRotation(CHR0EntryNode entry)
        {
            for(int channel=3;channel<6;channel++)
            {
                var keys=entry.Keyframes[channel].Entries.ToArray();if(keys.Length==0)continue;
                int first=BitConverter.ToInt32(BitConverter.GetBytes(keys[0]._value),0);
                if(keys.Any(key=>BitConverter.ToInt32(BitConverter.GetBytes(key._value),0)!=first||key._tangent!=0))return false;
            }
            return true;
        }
        // Fit against the parent that CHR0 actually evaluates, rather than the
        // ideal solver parent. This prevents ancestor interpolation error from
        // accumulating in the kick/hand/head orientation. Integer values and
        // source-authored motion-control coefficients are never changed.
        private static void FitHierarchyRotations(BrawlRig target, CHR0Node output, HumanoidRetargeter plan,
            List<Quaternion[]> desired, List<float[][]> desiredValues, int last, int subdivisions, CancellationToken cancellation)
        {
            if (last == 0) return;
            var entries = output.Children.OfType<CHR0EntryNode>().ToDictionary(e => e.Name, StringComparer.Ordinal);
            var world = Enumerable.Range(0, desired.Count).Select(_ => new Quaternion[target.Bones.Length]).ToArray();
            var mapped = new HashSet<int>(plan.Mapping.Select(m => m.TargetIndex));
            for (int bone = 0; bone < target.Bones.Length; bone++)
            {
                cancellation.ThrowIfCancellationRequested();
                int parent = target.Rig.Joints[bone].Parent;
                if(!entries.TryGetValue(target.Bones[bone].Name,out var entry))
                {
                    // An unauthored source control intentionally has no target
                    // track. Evaluate the authentic target bind without adding
                    // a synthetic channel or bypassing the own-source policy.
                    for(int sample=0;sample<desired.Count;sample++)
                        world[sample][bone]=Quaternion.Normalize(parent<0?target.Rig.Joints[bone].Bind.Rotation:
                            world[sample][parent]*target.Rig.Joints[bone].Bind.Rotation);
                    continue;
                }
                Quaternion Local(int sample) => Rotation(new BrawlLib.Internal.Vector3(
                    entry.Keyframes[3].KeyCount>0?entry.GetFrameValue(3, sample / (float)subdivisions):target.Bones[bone].BindState[3],
                    entry.Keyframes[4].KeyCount>0?entry.GetFrameValue(4, sample / (float)subdivisions):target.Bones[bone].BindState[4],
                    entry.Keyframes[5].KeyCount>0?entry.GetFrameValue(5, sample / (float)subdivisions):target.Bones[bone].BindState[5]));
                Quaternion World(int sample) => Quaternion.Normalize(parent < 0 ? Local(sample) : world[sample][parent] * Local(sample));
                double Loss(out double maximum)
                {
                    maximum = 0; double loss = 0;
                    for (int sample = 1; sample < desired.Count; sample++)
                    {
                        if (sample % subdivisions == 0) continue;
                        var delta = Quaternion.Normalize(Quaternion.Inverse(desired[sample][bone]) * World(sample));
                        double error = 2 * Math.Atan2(Math.Sqrt((double)delta.X * delta.X + (double)delta.Y * delta.Y + (double)delta.Z * delta.Z), Math.Abs(delta.W));
                        maximum = Math.Max(maximum, error); loss += error * error;
                    }
                    return loss;
                }
                if (mapped.Contains(bone) && !plan.TargetMotionControls.Contains(bone) && !ConstantNativeRotation(entry))
                {
                    var old = Enumerable.Range(3, 3).Select(c => Enumerable.Range(0, last + 1)
                        .Select(k => entry.Keyframes[c].GetKeyframe(k)._tangent).ToArray()).ToArray();
                    double before = Loss(out double beforeMax);
                    var values = new List<float[][]>();
                    for (int sample = 0; sample < desired.Count; sample++)
                    {
                        var evaluated = new Vector3(entry.GetFrameValue(3, sample / (float)subdivisions), entry.GetFrameValue(4, sample / (float)subdivisions), entry.GetFrameValue(5, sample / (float)subdivisions));
                        var local = parent < 0 ? desired[sample][bone] : Quaternion.Inverse(world[sample][parent]) * desired[sample][bone];
                        var angles = sample % subdivisions == 0 ? evaluated : EulerNear(local, evaluated);
                        values.Add(new[] { new[] { angles.X, angles.Y, angles.Z } });
                    }
                    for (int channel = 0; channel < 3; channel++)
                    {
                        var tangents = FitTangents(values, 0, channel, last, subdivisions);
                        for (int key = 0; key <= last; key++) entry.Keyframes[channel + 3].GetKeyframe(key)._tangent = tangents[key];
                    }
                    double after = Loss(out double afterMax);
                    if (!RigMath.Finite((float)after) || after > before || afterMax > beforeMax + 1e-7)
                        for (int channel = 0; channel < 3; channel++)
                            for (int key = 0; key <= last; key++) entry.Keyframes[channel + 3].GetKeyframe(key)._tangent = old[channel][key];
                    // Split only intervals that need independent native sides.
                    // A rare difficult span must not double keys across the clip.
                    for(int interval=0;interval<last;interval++)
                    {
                        double SpanMaximum()
                        {
                            double maximum=0;
                            for(int part=1;part<subdivisions;part++)
                            {
                                int sample=interval*subdivisions+part;
                                var delta=Quaternion.Normalize(Quaternion.Inverse(desired[sample][bone])*World(sample));
                                maximum=Math.Max(maximum,2*Math.Atan2(Math.Sqrt((double)delta.X*delta.X+(double)delta.Y*delta.Y+(double)delta.Z*delta.Z),Math.Abs(delta.W)));
                            }
                            return maximum;
                        }
                        double originalMaximum=SpanMaximum();
                        if(originalMaximum<=.025*Math.PI/180||output.Children.OfType<CHR0EntryNode>().Sum(e=>(long)e.KeyArrays.Sum(array=>array.KeyCount))+6>2000000)continue;
                        var retained=Enumerable.Range(3,3).Select(c=>entry.Keyframes[c].Entries.Select(k=>new[]{(float)k._index,k._value,k._tangent}).ToArray()).ToArray();
                        for(int channel=0;channel<3;channel++)
                        {
                            double aa=0,ab=0,bb=0,ar=0,br=0;
                            for(int part=1;part<subdivisions;part++)
                            {
                                double t=part/(double)subdivisions,t2=t*t,t3=t2*t,a=t3-2*t2+t,b=t3-t2;
                                double residual=values[interval*subdivisions+part][0][channel]-(2*t3-3*t2+1)*values[interval*subdivisions][0][channel]-(-2*t3+3*t2)*values[(interval+1)*subdivisions][0][channel];
                                aa+=a*a;ab+=a*b;bb+=b*b;ar+=a*residual;br+=b*residual;
                            }
                            var first=entry.Keyframes[channel+3].GetKeyframe(interval);var end=entry.Keyframes[channel+3].GetKeyframe(interval+1);
                            if(interval>0&&first.Second==null)entry.SetKeyframe(channel+3,interval,first._value,true,true)._tangent=first._tangent;
                            if(interval+1<last&&end.Second==null)entry.SetKeyframe(channel+3,interval+1,end._value,true,true)._tangent=end._tangent;
                            double determinant=aa*bb-ab*ab;
                            (first.Second??first)._tangent=(float)((ar*bb-br*ab)/determinant);end._tangent=(float)((br*aa-ar*ab)/determinant);
                        }
                        if(SpanMaximum()>originalMaximum)
                            for(int channel=0;channel<3;channel++)
                            {
                                var array=entry.Keyframes[channel+3];while(array.KeyCount>0)array.Remove(array.Entries.First()._index);
                                foreach(var key in retained[channel])entry.SetKeyframe(channel+3,(int)key[0],key[1],true,true)._tangent=key[2];
                            }
                    }
                }
                for (int sample = 0; sample < desired.Count; sample++) world[sample][bone] = World(sample);
            }
            RefineEndOrientations(target,output,plan,desired,desiredValues,last,subdivisions,cancellation);
        }
    }
}
