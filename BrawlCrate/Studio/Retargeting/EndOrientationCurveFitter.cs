using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    public static partial class BrawlRetargeter
    {
        // The pose solver is unchanged. Refine only the representation of a
        // solved limb in native, integer-time Hermite curves. Coupled parent
        // derivatives give the end joint a smoother compensating local curve.
        private static void RefineEndOrientations(BrawlRig target, CHR0Node output, HumanoidRetargeter plan,
            List<Quaternion[]> desired, List<float[][]> desiredValues, int last, int subdivisions, CancellationToken cancellation)
        {
            double length=NativeAnimationOptimization.GetBindBoundsDiagonal(target.Bones[0].Model);
            if(!(length>0)||last==0)return;
            double positionTolerance=.001*length;
            var entries=output.Children.OfType<CHR0EntryNode>().ToDictionary(e=>e.Name,StringComparer.Ordinal);
            var model=target.Bones[0].Model;
            var vertices=model.Objects.OfType<MDL0ObjectNode>().SelectMany(o=>o.Vertices).ToArray();
            var mapped=new HashSet<int>(plan.Mapping.Select(m=>m.TargetIndex));
            foreach(string role in new[]{"left.foot","right.foot","left.hand","right.hand"})
            {
                if(!target.Rig.Roles.TryGetValue(role,out int end)||ConstantNativeRotation(entries[target.Bones[end].Name]))continue;
                int middle=target.Rig.Joints[end].Parent,upper=middle<0?-1:target.Rig.Joints[middle].Parent;
                int[] chain={upper,middle,end};
                if(chain.Any(i=>i<0||!mapped.Contains(i)||plan.TargetMotionControls.Contains(i)))continue;
                var available=target.Rig.Surfaces.Values.SelectMany(surface=>surface.Points).Distinct()
                    .Where(point=>point.Weights.Any(weight=>weight.Weight>0&&chain.Any(bone=>target.Rig.Ancestors[weight.Joint][bone])))
                    .Select(point=>new{Point=point,Bind=point.Evaluate(target.Rig.Rest)})
                    .OrderBy(point=>point.Bind.X).ThenBy(point=>point.Bind.Y).ThenBy(point=>point.Bind.Z).ToArray();
                var witnesses=new List<SkinPoint>();var distances=available.Select(_=>float.MaxValue).ToArray();int chosen=0;
                for(int selected=0;selected<Math.Min(64,available.Length);selected++)
                {
                    witnesses.Add(available[chosen].Point);var bind=available[chosen].Bind;
                    for(int vertex=0;vertex<available.Length;vertex++)distances[vertex]=Math.Min(distances[vertex],Vector3.DistanceSquared(bind,available[vertex].Bind));
                    chosen=0;for(int vertex=1;vertex<available.Length;vertex++)if(distances[vertex]>distances[chosen])chosen=vertex;
                    if(distances[chosen]==0)break;
                }
                for(int interval=0;interval<last;interval++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var samples=Enumerable.Range(1,subdivisions-1).Select(i=>interval*subdivisions+i).ToArray();
                    var reference=samples.Select(i=>Sample(target,output,i/(float)subdivisions)).ToArray();
                    double Error(Quaternion actual,Quaternion wanted)
                    {var d=Quaternion.Normalize(Quaternion.Inverse(wanted)*actual);return 2*Math.Atan2(Math.Sqrt((double)d.X*d.X+(double)d.Y*d.Y+(double)d.Z*d.Z),Math.Abs(d.W))*180/Math.PI;}
                    double originalMaximum=samples.Select((sample,i)=>Error(reference[i].Orientation[end],desired[sample][end])).Max();
                    if(originalMaximum<=.025)continue;
                    if(output.Children.OfType<CHR0EntryNode>().Sum(e=>(long)e.KeyArrays.Sum(a=>a.KeyCount))+18>2000000)return;
                    // Correct representation error against Next's solved pose,
                    // not against the erroneous interpolation being repaired.
                    // Integer knots and source-owned controls stay unchanged.
                    var goals=samples.Select(sample=>new RigPose(target.Rig,desiredValues[sample].Select(v=>new JointTransform(
                        new Vector3(v[6],v[7],v[8]),Rotation(new BrawlLib.Internal.Vector3(v[3],v[4],v[5])),new Vector3(v[0],v[1],v[2]))).ToArray())).ToArray();
                    var witnessBaseline=goals.Select(pose=>witnesses.Select(point=>point.Evaluate(pose)).ToArray()).ToArray();
                    var idealSkin=samples.Select(sample=>
                    {
                        using(var pose=new CHR0Node{FrameCount=1})
                        {
                            for(int bone=0;bone<target.Bones.Length;bone++)
                            {
                                var entry=pose.CreateEntry(target.Bones[bone].Name);
                                entry.ScaleCompensateApply=target.Bones[bone].SegScaleCompApply;
                                entry.ScaleCompensateParent=target.Bones[bone].SegScaleCompParent;
                                entry.ClassicScaleOff=!target.Bones[bone].ClassicScale;
                                for(int channel=0;channel<9;channel++)entry.SetKeyframe(channel,0,desiredValues[sample][bone][channel],true)._tangent=0;
                            }
                            model.ApplyCHR(pose,1);
                            return vertices.Select(v=>Vector(v.WeightedPosition)).ToArray();
                        }
                    }).ToArray();
                    double originalSkinError=0;
                    for(int sample=0;sample<samples.Length;sample++)
                    {
                        model.ApplyCHR(output,samples[sample]/(float)subdivisions+1);
                        for(int vertex=0;vertex<vertices.Length;vertex++)originalSkinError=Math.Max(originalSkinError,Vector3.Distance(idealSkin[sample][vertex],Vector(vertices[vertex].WeightedPosition)));
                    }
                    double allowedSkinError=Math.Max(positionTolerance,originalSkinError);
                    double allowedPositionError=Math.Max(positionTolerance,reference.Select((pose,i)=>Vector3.Distance(pose.Point(end),goals[i].Point(end))).Max());
                    var owned=chain.Select(b=>entries[target.Bones[b].Name]).ToArray();
                    var saved=owned.Select(e=>Enumerable.Range(3,3).Select(c=>e.Keyframes[c].Entries.Select(k=>new[]{(float)k._index,k._value,k._tangent}).ToArray()).ToArray()).ToArray();
                    var keys=new List<KeyframeEntry>();
                    foreach(var entry in owned)for(int c=3;c<6;c++)
                    {
                        KeyframeEntry Side(int frame,bool outgoing)
                        {
                            var first=entry.Keyframes[c].GetKeyframe(frame);
                            if(frame>0&&frame<last&&first.Second==null)entry.SetKeyframe(c,frame,first._value,true,true)._tangent=first._tangent;
                            return outgoing?first.Second??first:first;
                        }
                        keys.Add(Side(interval,true));keys.Add(Side(interval+1,false));
                    }
                    var original=keys.Select(k=>k._tangent).ToArray();
                    double[] Residual(out double maximum,out double position)
                    {
                        maximum=position=0;var rows=new List<double>();
                        for(int i=0;i<samples.Length;i++)
                        {
                            var local=(JointTransform[])reference[i].Local.Clone();
                            // Existing dense knot references give exactly the
                            // native Hermite interval without repeated linked-list scans.
                            float offset=(samples[i]-interval*subdivisions)/(float)subdivisions;
                            for(int j=0;j<chain.Length;j++)local[chain[j]].Rotation=Rotation(new BrawlLib.Internal.Vector3(
                                keys[j*6].Interpolate(offset,1,keys[j*6+1]),keys[j*6+2].Interpolate(offset,1,keys[j*6+3]),keys[j*6+4].Interpolate(offset,1,keys[j*6+5])));
                            var pose=new RigPose(reference[i],local,chain);
                            for(int j=0;j<chain.Length;j++)
                            {
                                var d=Quaternion.Normalize(Quaternion.Inverse(desired[samples[i]][chain[j]])*pose.Orientation[chain[j]]);
                                double sign=d.W<0?-1:1,weight=j==2?1:.002;
                                rows.Add(d.X*sign*114.591559*weight);rows.Add(d.Y*sign*114.591559*weight);rows.Add(d.Z*sign*114.591559*weight);
                            }
                            maximum=Math.Max(maximum,Error(pose.Orientation[end],desired[samples[i]][end]));
                            Vector3 delta=pose.Point(end)-goals[i].Point(end);position=Math.Max(position,delta.Length());
                            double scale=10/length;rows.Add(delta.X*scale);rows.Add(delta.Y*scale);rows.Add(delta.Z*scale);
                            foreach(int witness in Enumerable.Range(0,witnesses.Count))
                            {
                                var skinDelta=witnesses[witness].Evaluate(pose)-witnessBaseline[i][witness];double skinScale=10/length;
                                rows.Add(skinDelta.X*skinScale);rows.Add(skinDelta.Y*skinScale);rows.Add(skinDelta.Z*skinScale);
                            }

                        }
                        for(int k=0;k<keys.Count;k++)rows.Add((keys[k]._tangent-original[k])*.00001);
                        return rows.ToArray();
                    }
                    double loss=Residual(out _,out _).Sum(v=>v*v);
                    for(int iteration=0;iteration<40;iteration++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var residual=Residual(out double maximum,out _);if(maximum<=.005)break;
                        var start=keys.Select(k=>k._tangent).ToArray();var columns=new double[keys.Count][];
                        for(int k=0;k<keys.Count;k++)
                        {
                            keys[k]._tangent=start[k]+.02f;var changed=Residual(out _,out _);keys[k]._tangent=start[k];
                            columns[k]=changed.Select((v,r)=>(v-residual[r])/.02).ToArray();
                        }
                        var normal=new double[keys.Count,keys.Count];var rhs=new double[keys.Count];
                        for(int a=0;a<keys.Count;a++)
                        {
                            for(int r=0;r<residual.Length;r++)rhs[a]-=columns[a][r]*residual[r];
                            for(int b=0;b<=a;b++){double v=0;for(int r=0;r<residual.Length;r++)v+=columns[a][r]*columns[b][r];normal[a,b]=normal[b,a]=v;}
                            normal[a,a]+=1e-7;
                        }
                        var descent=Enumerable.Range(0,keys.Count).Select(k=>rhs[k]/normal[k,k]).ToArray();
                        var newton=ContactMotion.SolveContactSystem(normal,rhs);bool improved=false;
                        foreach(var direction in new[]{newton,descent})
                        {
                            double largest=direction.Select(Math.Abs).Max();double stepScale=largest>10?10/largest:1;
                            for(double factor=stepScale;factor>=stepScale/128;factor*=.5)
                            {
                                for(int k=0;k<keys.Count;k++)keys[k]._tangent=start[k]+(float)(factor*direction[k]);
                                double candidate=Residual(out _,out double displacement).Sum(v=>v*v);
                                if(candidate<loss&&displacement<=allowedPositionError){loss=candidate;improved=true;break;}
                            }
                            if(improved)break;
                        }
                        if(!improved){for(int k=0;k<keys.Count;k++)keys[k]._tangent=start[k];break;}
                    }
                    Residual(out double finalMaximum,out double finalPosition);
                    if(Environment.GetEnvironmentVariable("FIGHTERPORT_CURVE_DIAGNOSTIC")=="1")Console.WriteLine("CURVE "+role+" "+interval+": "+originalMaximum+" -> "+finalMaximum+"; displacement "+finalPosition+" / "+(positionTolerance));
                    double skinError=0;
                    for(int sample=0;sample<samples.Length&&skinError<=allowedSkinError;sample++)
                    {
                        model.ApplyCHR(output,samples[sample]/(float)subdivisions+1);
                        for(int vertex=0;vertex<vertices.Length;vertex++)skinError=Math.Max(skinError,Vector3.Distance(idealSkin[sample][vertex],Vector(vertices[vertex].WeightedPosition)));
                    }
                    if(skinError>allowedSkinError||finalMaximum>originalMaximum||finalPosition>allowedPositionError||!RigMath.Finite((float)loss))
                        for(int j=0;j<owned.Length;j++)for(int c=0;c<3;c++)
                        {
                            var array=owned[j].Keyframes[c+3];while(array.KeyCount>0)array.Remove(array.Entries.First()._index);
                            foreach(var key in saved[j][c])owned[j].SetKeyframe(c+3,(int)key[0],key[1],true,true)._tangent=key[2];
                        }
                }
            }
        }
    }
}
