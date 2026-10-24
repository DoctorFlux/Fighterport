using BrawlCrate.Studio.Retargeting;
using System;
using System.Linq;
using System.Numerics;

internal static partial class Program
{
    // An independent continuous takeoff/flight/landing control on real rigs.
    // Phase labels come from this authored numerical trajectory, never from the
    // detector under test. Native example assets remain the user's originals.
    private static void FlightCases(Rig source,Rig target)
    {
        var plan=new HumanoidRetargeter(source,target);
        var travel=plan.Mapping.Last(plan.IsTrajectory);
        float leg=Vector3.Distance(source.Rest.Point("left.upperLeg"),source.Rest.Point("left.lowerLeg"))+
            Vector3.Distance(source.Rest.Point("left.lowerLeg"),source.Rest.Point("left.foot"));
        Func<float,RigPose> sample=time=>{
            var pose=BouncingStance(source,0);
            float phase=RigMath.Clamp((time-6)/12,0,1);
            float lift=leg*1.2f*(float)Math.Pow(Math.Sin(phase*Math.PI),2);
            pose.SetWorldPoint(source.Roles["pelvis"],pose.Point("pelvis")+source.GroundNormal*lift);
            pose.SetWorldPoint(travel.SourceIndex,pose.Point(travel.SourceIndex)+new Vector3(0,0,time*0.4f));
            return pose;
        };
        var motion=new ContactMotion(plan,sample,24,true,requireFloorEvidence:true);
        double flightError=0,groundError=0,routeError=0,maxAirLock=0;int air=0,ground=0;
        for(int i=0;i<=96;i++)
        {
            float time=i/4f;var donor=sample(time);var expected=plan.Evaluate(donor).Pose;var actual=motion.Evaluate(time).Pose;
            routeError=Math.Max(routeError,Vector3.Distance(expected.Local[travel.TargetIndex].Translation,actual.Local[travel.TargetIndex].Translation));
            if(time>=9 && time<=15)
            {
                air++;
                foreach(string role in new[]{"left.foot","right.foot"})
                {
                    maxAirLock=Math.Max(maxAirLock,Math.Max(motion.At(role,time).Support,motion.At(role,time).Lock));
                    Check(source.Surfaces[role].Clearance(donor)>leg*0.5f,"Independent airborne fixture has real source sole clearance");
                }
                flightError=Math.Max(flightError,Enumerable.Range(0,target.Joints.Length).Max(j=>Vector3.Distance(expected.Point(j),actual.Point(j))));
            }
            if(time<=4 || time>=20)
            {
                ground++;
                foreach(string role in new[]{"left.foot","right.foot"})
                    groundError=Math.Max(groundError,Math.Abs(target.Surfaces[role].Clearance(actual)));
            }
        }
        Check(air>0 && ground>0 && maxAirLock==0 && flightError<0.001 && groundError<0.02 && routeError<0.001,
            "Automatic continuous takeoff/flight/landing releases both feet in flight, restores floor support and preserves authored actor travel: "+source.Name+" to "+target.Name,
            new{AirSamples=air,GroundSamples=ground,MaximumAirContact=maxAirLock,FlightFkError=flightError,GroundSoleError=groundError,TrajectoryError=routeError});
    }
}
