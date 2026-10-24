using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Numerics;

internal static partial class Program
{
    private static void TrajectoryCases(BrawlRig source,BrawlRig target,string output)
    {
        foreach(var pair in new[]{Tuple.Create(source.Rig,target.Rig),Tuple.Create(target.Rig,source.Rig)})
        {
            var plan=new HumanoidRetargeter(pair.Item1,pair.Item2);
            var map=plan.Mapping.Last(plan.IsTrajectory);
            var pose=new RigPose(pair.Item1);
            Vector3 displacement=new Vector3(3.25f,1.1f,-7.75f);
            pose.SetWorldPoint(map.SourceIndex,pose.Point(map.SourceIndex)+displacement);
            foreach(bool ik in new[]{false,true})
            {
                var rest=plan.Evaluate(pair.Item1.Rest,ik).Pose;
                var actual=plan.Evaluate(pose,ik).Pose;
                Vector3 expected=displacement;
                double error=Enumerable.Range(0,pair.Item2.Joints.Length).Max(i=>{
                    int ancestor=i;while(ancestor>=0 && ancestor!=map.TargetIndex)ancestor=pair.Item2.Joints[ancestor].Parent;
                    return Vector3.Distance(actual.Point(i)-rest.Point(i),ancestor<0?Vector3.Zero:expected);
                });
                Check(error<0.005,"Actor trajectory is independent of limb proportions with IK="+ik+": "+pair.Item1.Name+" to "+pair.Item2.Name,new{Error=error,plan.LegScale});
            }
            var a=Rebase(pair.Item1,out var aBasis);var b=Rebase(pair.Item2,out var bBasis);
            var rebased=new HumanoidRetargeter(a,b);
            var changed=new RigPose(a,pose.Local.Select((v,i)=>RebaseLocal(v,a.Joints[i].Parent,i,aBasis)).ToArray());
            var expectedPose=plan.Evaluate(pose).Pose;var actualPose=rebased.Evaluate(changed).Pose;
            double rebasedError=Enumerable.Range(0,b.Joints.Length).Max(i=>Vector3.Distance(expectedPose.Point(i),actualPose.Point(i)));
            Check(rebasedError<0.005,"Trajectory/body separation survives independent bone-axis calibration",new{MaximumError=rebasedError});
        }
        var nativePlan=new HumanoidRetargeter(source.Rig,target.Rig);
        var motion=nativePlan.Mapping.First(m=>nativePlan.IsTrajectory(m) && m.Role!="root");
        using(var fixture=new CHR0Node{Name="IndependentTravel",FrameCount=21})
        {
            var track=fixture.CreateEntry(motion.Source);
            for(int c=6;c<9;c++)foreach(int time in new[]{2,7,13,20})
                track.SetKeyframe(c,time,(c-5)*(float)Math.Sin(time*0.23),true)._tangent=(c-5)*0.23f*(float)Math.Cos(time*0.23);
            var first=track.Keyframes[8].GetKeyframe(7);
            track.Keyframes[8].SetFrameValue(7,first._value+0.125f,true)._tangent=-0.4f;
            using(var result=BrawlRetargeter.Bake(source,target,fixture,false))
            {
                string path=Path.Combine(output,"independent-travel.chr0");result.Animation.ExportChecked(path);
                using(var reopened=(CHR0Node)NodeFactory.FromFile(null,path))
                {
                    double maximum=0;
                    foreach(float time in Enumerable.Range(0,81).Select(i=>i/4f).Concat(Enumerable.Range(0,20).Select(i=>i+0.37f)))
                    {
                        var donor=BrawlRetargeter.Sample(source,fixture,time);var actual=BrawlRetargeter.Sample(target,reopened,time);
                        Vector3 expected=target.Rig.Joints[motion.TargetIndex].Bind.Translation+nativePlan.TransferTranslationDelta(motion,
                            donor.Local[motion.SourceIndex].Translation-source.Rig.Joints[motion.SourceIndex].Bind.Translation);
                        maximum=Math.Max(maximum,Vector3.Distance(expected,actual.Local[motion.TargetIndex].Translation));
                    }
                    var saved=(CHR0EntryNode)reopened.FindChild(motion.Target,false);
                    Check(maximum<0.001 && Enumerable.Range(6,3).All(c=>new[]{2,7,13,20}.All(t=>saved.Keyframes[c].GetKeyframe(t)!=null)) && saved.Keyframes[8].GetKeyframe(7).Second!=null,
                        "Native trajectory retains authored knots, discontinuity, held prefix and cubic travel at quarter/withheld times",new{MaximumError=maximum,SourceKeys=Enumerable.Range(6,3).Select(c=>track.Keyframes[c].KeyCount).ToArray(),TargetKeys=Enumerable.Range(6,3).Select(c=>saved.Keyframes[c].KeyCount).ToArray()});
                }
            }
        }
    }
}
