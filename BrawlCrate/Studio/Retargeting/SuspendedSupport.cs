using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio.Retargeting
{
    // An initial raised-hand support frame inferred from source anatomy/motion.
    // It supplies no terrain shape and makes no decisions from clip names.
    public sealed class SuspendedSupport
    {
        private readonly HumanoidRetargeter plan;
        private readonly RigPose initial;
        private readonly float sourceLeg;
        private readonly HashSet<string> supportedHands;
        public Vector3 SourceReference {get;}
        public Vector3 TargetReference {get;}
        internal object CaptureOriginalEvidence()=>new{Version="original-next-suspended-context-1",SourceReference,TargetReference,SourceLeg=sourceLeg,SupportedHands=supportedHands.OrderBy(s=>s,StringComparer.Ordinal).ToArray(),
            InitialLocal=initial.Local.Select(t=>new{t.Translation,t.Rotation,t.Scale}).ToArray(),InitialWorld=initial.World.ToArray()};
        private SuspendedSupport(HumanoidRetargeter retargeter,RigPose[] poses,string[] hands)
        {
            plan=retargeter;initial=poses[0];
            supportedHands=new HashSet<string>(hands);
            sourceLeg=Length(initial.Rig.Rest,"left",false);
            Vector3 up=plan.Source.GroundNormal;
            Vector3 grip=hands.Aggregate(Vector3.Zero,(point,side)=>point+initial.Point(side+".hand"))/hands.Length;
            SourceReference=grip+up*(plan.Source.GroundHeight-Vector3.Dot(grip,up));
            Vector3 sourceOrigin=up*plan.Source.GroundHeight;
            TargetReference=Vector3.Transform(SourceReference-sourceOrigin,plan.Calibration)*plan.LegScale+plan.Target.GroundNormal*plan.Target.GroundHeight;
            Vector3 offset=plan.TrajectoryOffset(initial,plan.Evaluate(initial,false).Pose);
            TargetReference+=offset-plan.Target.GroundNormal*Vector3.Dot(offset,plan.Target.GroundNormal);
        }
        internal static SuspendedSupport Detect(HumanoidRetargeter plan,RigPose[] poses)
        {
            RigPose first=poses[0];Vector3 up=plan.Source.GroundNormal;
            float leg=Length(plan.Source.Rest,"left",false);
            float hip=Vector3.Dot(first.Point("pelvis"),up)-plan.Source.GroundHeight;
            string[] hands=new[]{"left","right"}.Where(side=>{
                float arm=Length(plan.Source.Rest,side,true),hand=Vector3.Dot(first.Point(side+".hand"),up)-plan.Source.GroundHeight;
                return hand>hip+arm*0.2f && Math.Abs(hand)<arm*0.45f;
            }).ToArray();
            if(hip>=-leg*0.25f || hands.Length==0)return null;
            return new SuspendedSupport(plan,poses,hands);
        }
        private static float Length(RigPose pose,string side,bool arm)
        {return Vector3.Distance(pose.Point(side+(arm?".upperArm":".upperLeg")),pose.Point(side+(arm?".lowerArm":".lowerLeg")))+
                Vector3.Distance(pose.Point(side+(arm?".lowerArm":".lowerLeg")),pose.Point(side+(arm?".hand":".foot")));}
        private static float Smooth(float a,float b,float x)
        {float t=RigMath.Clamp((x-a)/(b-a),0,1);return t*t*(3-2*t);}
        public float GripWeight(RigPose source,string side)
        {
            if(!supportedHands.Contains(side))return 0;
            float arm=Length(plan.Source.Rest,side,true);
            float departure=Vector3.Distance(source.Point(side+".hand"),initial.Point(side+".hand"));
            float hip=Vector3.Dot(source.Point("pelvis"),plan.Source.GroundNormal)-plan.Source.GroundHeight;
            float weight=(1-Smooth(arm*0.08f,arm*0.3f,departure))*(1-Smooth(-sourceLeg*0.1f,sourceLeg*0.4f,hip));
            return weight+(1-weight)*Smooth(0.9f,0.99f,weight);
        }
        public Vector3 GripGoal(RigPose source,string side)
            =>TargetReference+Vector3.Transform(initial.Point(side+".hand")-SourceReference,plan.Calibration)*
                (Length(plan.Target.Rest,side,true)/Length(plan.Source.Rest,side,true));
        internal float FloorWeight(RigPose source,string role)
        {
            if(role.EndsWith(".hand",StringComparison.Ordinal) && GripWeight(source,role.Split('.')[0])>0.001f)return 0;
            // A body passing upward through the reference plane is not landing
            // on it. Once its pelvis rises, ordinary support analysis resumes.
            if(!ContactMotion.IsEndContact(role))
            {
                float hip=Vector3.Dot(source.Point("pelvis"),plan.Source.GroundNormal)-plan.Source.GroundHeight;
                return Smooth(-sourceLeg*0.1f,sourceLeg*0.1f,hip);
            }
            return 1;
        }
        internal void Apply(RigPose source,RetargetPose result)
        {
            RigPose target=result.Pose;Vector3 original=target.Point("pelvis"),shift=Vector3.Zero;float total=0,maximum=0;
            foreach(string side in new[]{"left","right"})
            {
                float weight=GripWeight(source,side);
                shift+=(GripGoal(source,side)-target.Point(side+".hand"))*weight;
                total+=weight;maximum=Math.Max(maximum,weight);
            }
            if(total<=0)return;
            target.SetWorldPoint(plan.Target.Roles["pelvis"],original+shift/total*maximum);
            // Different shoulder spacing can put one hand just beyond reach
            // after centering both grips. Project the pelvis into the common
            // reach region instead of stretching either arm.
            for(int iteration=0;iteration<12;iteration++)foreach(string side in new[]{"left","right"})
            {
                float weight=GripWeight(source,side);if(weight<=0)continue;
                Vector3 delta=target.Point(side+".upperArm")-GripGoal(source,side);
                float distance=delta.Length(),reach=Length(target,side,true)*0.9998f;
                if(distance>reach)target.SetWorldPoint(plan.Target.Roles["pelvis"],target.Point("pelvis")-delta*((distance-reach)/distance)*weight);
            }
            foreach(string side in new[]{"left","right"})
            {
                float weight=GripWeight(source,side);
                if(weight<=0)continue;
                Vector3 goal=Vector3.Lerp(target.Point(side+".hand"),GripGoal(source,side),weight);
                plan.SolveLimb(target,result,side,true,goal);
                result.Goals[side+".hand"]=goal;
            }
            result.PelvisCorrection=target.Point("pelvis")-original;
        }
    }
}
