using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Numerics;

internal static partial class Program
{
    private static void ReverseCases(BrawlRig source,BrawlRig target,ResourceNode motions,MDL0Node nativeTarget,string template,string output)
    {
        Directory.CreateDirectory(output);
        var clips=Walk(motions).ToList().OfType<CHR0Node>().ToDictionary(c=>c.Name);
        var plan=new HumanoidRetargeter(source.Rig,target.Rig);
        Check(plan.Mapping.Any(m=>m.Source=="BodyN"&&m.Target=="WaistN") && plan.LegScale<plan.ArmScale && plan.ArmScale<1,
            "Reverse Snake-to-Mario mapping measures shorter target limbs independently",new{plan.LegScale,plan.ArmScale});
        foreach(string name in ContactClip==null?(FocusedSupportCases?new[]{"Wait1"}:new[]{"Wait1","Attack11","Run","JumpF","AppealHi"}):new string[0])
        using(var result=BrawlRetargeter.Bake(source,target,clips[name]))
        {
            string path=Path.Combine(output,name+"-Mario.chr0");result.Animation.ExportChecked(path);
            using(var saved=(CHR0Node)NodeFactory.FromFile(null,path))
            {
                double integer=0,fractional=0,skin=0;string worstBone=null;float worstTime=0;
                foreach(float time in Enumerable.Range(0,saved.FrameCount).Select(i=>(float)i).Concat(Enumerable.Range(0,saved.FrameCount-1).Select(i=>i+0.37f)))
                {
                    var expected=result.Contacts.Evaluate(time).Pose;
                    nativeTarget.ApplyCHR(saved,time+1);
                    double error=Enumerable.Range(0,target.Bones.Length).Max(i=>Vector3.Distance(expected.Point(i),BrawlRetargeter.Vector(target.Bones[i].Matrix.GetPoint())));
                    if(time==(int)time)integer=Math.Max(integer,error);else if(error>fractional){fractional=error;worstTime=time;worstBone=target.Bones.OrderByDescending(b=>Vector3.Distance(expected.Point(Array.IndexOf(target.Bones,b)),BrawlRetargeter.Vector(b.Matrix.GetPoint()))).First().Name;}
                }
                foreach(float time in new[]{0f,(saved.FrameCount-1)*0.37f,saved.FrameCount-1f})
                {
                    nativeTarget.ApplyCHR(saved,time+1);var actual=Vertices(nativeTarget);
                    nativeTarget.ApplyCHR(result.Animation,time+1);var expected=Vertices(nativeTarget);
                    skin=Math.Max(skin,actual.Zip(expected,(a,b)=>Vector3.Distance(a,b)).Max());
                }
                Check(integer<0.06 && skin<0.06 && fractional<1.0,"Snake-to-Mario "+name+": fresh native integer/fractional poses and skin round trip",new{IntegerError=integer,FractionalError=fractional,SkinRoundTripError=skin,WorstBone=worstBone,WorstTime=worstTime});
                // Contact evaluation must be independent of playback direction/state.
                var first=result.Contacts.Evaluate(3.37f).Pose.World;
                result.Contacts.Evaluate(saved.FrameCount-1);result.Contacts.Evaluate(0);
                Check(first.SequenceEqual(result.Contacts.Evaluate(3.37f).Pose.World),"Snake-to-Mario "+name+": contact scrubbing is deterministic");
                if(name=="Attack11")
                {
                    string pac=Path.Combine(output,"FitMarioMotionEtc-retargeted.pac");
                    BrawlRetargeter.ExportMotionPac(template,pac,nativeTarget,saved);
                    using(var root=NodeFactory.FromFile(null,pac))
                    using(var original=NodeFactory.FromFile(null,template))
                    {
                        var decoded=Walk(root).ToList().OfType<CHR0Node>().ToDictionary(c=>c.TreePath);
                        var old=Walk(original).ToList().OfType<CHR0Node>().ToDictionary(c=>c.TreePath);
                        Check(decoded.Count==297 && old.Keys.All(decoded.ContainsKey),"Snake-to-Mario native target PAC reopens with all 297 slots");
                        Check(old.Values.Where(c=>c.Name!="Attack11").All(c=>Signature(c)==Signature(decoded[c.TreePath]) && c.FrameCount==decoded[c.TreePath].FrameCount && c.Loop==decoded[c.TreePath].Loop),
                            "Every untouched Mario CHR0 slot retains its native interpolation samples",new{Slots=296});
                    }
                }
            }
        }
        ContactCases(source,target,clips,output,nativeTarget);
    }
}
