using BrawlCrate.Studio.Retargeting;
using System;
using System.Linq;
using System.Numerics;

internal static partial class Program
{
    // Numerical motion only: independently construct bent legs with fixed world
    // ankles and a bouncing hip on the supplied real rest skeleton/skin. No new
    // example character or replacement animation asset is generated.
    private static RigPose BouncingStance(Rig rig,float time)
    {
        var pose=new RigPose(rig);
        float length=Vector3.Distance(rig.Rest.Point("left.upperLeg"),rig.Rest.Point("left.lowerLeg"))+
            Vector3.Distance(rig.Rest.Point("left.lowerLeg"),rig.Rest.Point("left.foot"));
        pose.SetWorldPoint(rig.Roles["pelvis"],pose.Point("pelvis")-rig.GroundNormal*length*(0.2f+0.065f*(float)Math.Sin(time*Math.PI/8)));
        foreach(string side in new[]{"left","right"})
        {
            int hip=rig.Roles[side+".upperLeg"],knee=rig.Roles[side+".lowerLeg"],foot=rig.Roles[side+".foot"];
            Vector3 start=pose.Point(hip),end=rig.Rest.Point(foot),axis=Vector3.Normalize(end-start);
            float upper=Vector3.Distance(rig.Rest.Point(hip),rig.Rest.Point(knee)),lower=Vector3.Distance(rig.Rest.Point(knee),end),distance=Vector3.Distance(start,end);
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance),bend=(float)Math.Sqrt(Math.Max(0,upper*upper-along*along));
            Vector3 pole=Vector3.Transform(Vector3.UnitZ,RigMath.BodyFrame(rig));pole=Vector3.Normalize(pole-axis*Vector3.Dot(pole,axis));
            Vector3 middle=start+axis*along+pole*bend;
            pose.SetWorldRotation(hip,RigMath.FromTo(pose.Point(knee)-start,middle-start)*pose.Orientation[hip]);
            pose.SetWorldRotation(knee,RigMath.FromTo(pose.Point(foot)-pose.Point(knee),end-pose.Point(knee))*pose.Orientation[knee]);
            pose.SetWorldRotation(foot,rig.Rest.Orientation[foot]);
        }
        return pose;
    }
    private static void StanceCases(Rig source,Rig target)
    {
        var plan=new HumanoidRetargeter(source,target);
        var contacts=new ContactMotion(plan,t=>BouncingStance(source,t),32,true);
        double sourceFloor=0,targetFloor=0,stanceError=0,sourceMin=double.MaxValue,sourceMax=double.MinValue,targetMin=double.MaxValue,targetMax=double.MinValue;
        for(int i=0;i<=128;i++)
        {
            float time=i/4f;RigPose donor=BouncingStance(source,time),result=contacts.Evaluate(time).Pose;
            double a=Vector3.Dot(donor.Point("pelvis"),source.GroundNormal),b=Vector3.Dot(result.Point("pelvis"),target.GroundNormal);
            sourceMin=Math.Min(sourceMin,a);sourceMax=Math.Max(sourceMax,a);targetMin=Math.Min(targetMin,b);targetMax=Math.Max(targetMax,b);
            foreach(string side in new[]{"left","right"})
            {
                sourceFloor=Math.Max(sourceFloor,Math.Abs(source.Surfaces[side+".foot"].Clearance(donor)));
                targetFloor=Math.Max(targetFloor,Math.Abs(target.Surfaces[side+".foot"].Clearance(result)));
                float sourceLength=Vector3.Distance(source.Rest.Point(side+".upperLeg"),source.Rest.Point(side+".lowerLeg"))+Vector3.Distance(source.Rest.Point(side+".lowerLeg"),source.Rest.Point(side+".foot"));
                float targetLength=Vector3.Distance(target.Rest.Point(side+".upperLeg"),target.Rest.Point(side+".lowerLeg"))+Vector3.Distance(target.Rest.Point(side+".lowerLeg"),target.Rest.Point(side+".foot"));
                float expected=Vector3.Dot(donor.Point(side+".upperLeg")-donor.Point(side+".foot"),source.GroundNormal)*targetLength/sourceLength;
                stanceError=Math.Max(stanceError,Math.Abs(Vector3.Dot(result.Point(side+".upperLeg")-result.Point(side+".foot"),target.GroundNormal)-expected));
            }
        }
        Check(sourceFloor<0.001 && targetFloor<0.02 && stanceError<0.05 && targetMax-targetMin>(sourceMax-sourceMin)*plan.LegScale*0.5,
            "Grounded hip bounce retains both planted soles and source-scaled stance height: "+source.Name+" to "+target.Name,
            new{SourceFloor=sourceFloor,TargetFloor=targetFloor,StanceError=stanceError,SourceBounce=sourceMax-sourceMin,TargetBounce=targetMax-targetMin});
        float sourceLeg=Vector3.Distance(source.Rest.Point("left.upperLeg"),source.Rest.Point("left.lowerLeg"))+
            Vector3.Distance(source.Rest.Point("left.lowerLeg"),source.Rest.Point("left.foot"));
        Func<float,RigPose> drift=t=>{
            var pose=BouncingStance(source,t);
            int thigh=source.Roles["right.upperLeg"];
            pose.SetWorldPoint(thigh,pose.Point(thigh)+source.GroundNormal*sourceLeg*0.06f);
            return pose;
        };
        var stable=new ContactMotion(plan,drift,32,true);
        var strict=new ContactMotion(plan,drift,32,true,stabilizeStanding:false);
        double grounded=Enumerable.Range(0,129).Max(i=>Math.Abs(target.Surfaces["right.foot"].Clearance(stable.Evaluate(i/4f).Pose)));
        double preserved=target.Surfaces["right.foot"].Clearance(strict.Evaluate(16).Pose);
        Check(grounded<0.02 && preserved>sourceLeg*plan.LegScale*0.04f,
            "Quiet low sole drift uses opposite-foot support without an idle identity; strict source contacts retain the lift: "+source.Name+" to "+target.Name,
            new{Grounded=grounded,StrictClearance=preserved});
        Func<float,RigPose> lifted=t=>{
            var pose=BouncingStance(source,t);int thigh=source.Roles["right.upperLeg"];
            pose.SetWorldPoint(thigh,pose.Point(thigh)+source.GroundNormal*sourceLeg*0.2f);
            return pose;
        };
        var held=new ContactMotion(plan,lifted,32,true);
        Check(held.At("right.foot",16).Support<0.01f &&
            target.Surfaces["right.foot"].Clearance(held.Evaluate(16).Pose)>sourceLeg*plan.LegScale*0.1f,
            "A higher held foot remains lifted despite support under the opposite sole: "+source.Name+" to "+target.Name);
        Func<float,RigPose> emerging=t=>{
            var pose=BouncingStance(source,8);
            float height=t<8?sourceLeg*(-1.4f+1.4f*t/8):0;
            int hip=source.Roles["pelvis"];pose.SetWorldPoint(hip,pose.Point(hip)+source.GroundNormal*height);
            return pose;
        };
        var crossing=new ContactMotion(plan,emerging,16,true,detectSuspended:false);
        Check(Enumerable.Range(0,32).All(i=>new[]{"left.foot","right.foot"}.All(role=>crossing.At(role,i/4f).Support<0.001f)) &&
            new[]{"left.foot","right.foot"}.All(role=>crossing.At(role,12).Support>0.99f && Math.Abs(target.Surfaces[role].Clearance(crossing.Evaluate(12).Pose))<0.02f),
            "Upward plane crossing stays unsupported until the body settles; floor support resumes without motion identity: "+source.Name+" to "+target.Name);
        Func<float,RigPose> traveling=t=>{
            var pose=BouncingStance(source,0);
            int hip=source.Roles["pelvis"],thigh=source.Roles["right.upperLeg"];
            pose.SetWorldPoint(thigh,pose.Point(thigh)+source.GroundNormal*sourceLeg*0.04f);
            Vector3 forward=Vector3.Transform(Vector3.UnitZ,RigMath.BodyFrame(source));
            forward=Vector3.Normalize(forward-source.GroundNormal*Vector3.Dot(forward,source.GroundNormal));
            float height=sourceLeg*(0.02f+0.003f*(float)Math.Sin(t));
            if(t<6 || t>10)height-=sourceLeg*1.4f;
            pose.SetWorldPoint(hip,pose.Point(hip)+source.GroundNormal*height+forward*sourceLeg*t*0.2f);
            return pose;
        };
        var movingSupport=new ContactMotion(plan,traveling,16,true,detectSuspended:false);
        var movingStrict=new ContactMotion(plan,traveling,16,true,stabilizeStanding:false,detectSuspended:false);
        var movingEvidence=new[]{"left.foot","right.foot"}.Select(role=>new{Role=role,SourceHeight=source.Surfaces[role].Clearance(traveling(8)),
            Extension=Vector3.Dot(traveling(8).Point(role.Split('.')[0]+".upperLeg")-traveling(8).Point(role),source.GroundNormal)/sourceLeg,
            movingSupport.At(role,8).Support,movingSupport.At(role,8).Lock,
            Height=target.Surfaces[role].Clearance(movingSupport.Evaluate(8).Pose),StrictHeight=target.Surfaces[role].Clearance(movingStrict.Evaluate(8).Pose)}).ToArray();
        Check(new[]{"left.foot","right.foot"}.All(role=>movingSupport.At(role,8).Support>0.99f &&
            movingSupport.At(role,8).Lock<0.01f && Math.Abs(target.Surfaces[role].Clearance(movingSupport.Evaluate(8).Pose))<0.02f) &&
            target.Surfaces["right.foot"].Clearance(movingStrict.Evaluate(8).Pose)>sourceLeg*plan.LegScale*0.03f,
            "A short traveling upright segment in the middle of unrelated motion corrects bilateral FK sole drift without stationary locks; strict source mode retains it: "+source.Name+" to "+target.Name,movingEvidence);
    }
}
