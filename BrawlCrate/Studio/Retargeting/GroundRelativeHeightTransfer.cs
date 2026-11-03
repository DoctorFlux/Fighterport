using System;
using System.IO;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio.Retargeting
{
    // A format-neutral body-proportion bridge. Relative sole height is taken
    // from weighted source/target geometry, so the target keeps a source
    // foot's lift above its own model floor without moving actor controls or
    // changing either rig's binds and weights. This does not establish contact.
    internal sealed class GroundRelativeHeightTransfer
    {
        private static readonly string[] Feet={"left.foot","right.foot"};
        private readonly Rig source,target;
        private readonly float legScale,tolerance;
        internal float MaximumDecodedError {get;private set;}

        internal static bool Available(Rig source,Rig target)=>
            source!=null&&target!=null&&Feet.All(role=>
                source.Surfaces.ContainsKey(role)&&target.Surfaces.ContainsKey(role));

        internal GroundRelativeHeightTransfer(Rig source,Rig target,float legScale)
        {
            if(!Available(source,target)||!RigMath.Finite(legScale)||legScale<=0)
                throw new InvalidDataException("Weighted feet and a finite positive leg scale are needed for relative height transfer.");
            this.source=source;this.target=target;this.legScale=legScale;
            float length=Feet.Average(role=>{
                string side=role.Split('.')[0];
                return Vector3.Distance(target.Rest.Point(side+".upperLeg"),target.Rest.Point(side+".lowerLeg"))+
                    Vector3.Distance(target.Rest.Point(side+".lowerLeg"),target.Rest.Point(role));});
            tolerance=length*.001f;
        }

        private float Expected(RigPose sourcePose)=>Feet.Min(role=>
            source.Surfaces[role].Sample(sourcePose).Clearance)*legScale;
        private float Actual(RigPose targetPose)=>Feet.Min(role=>
            target.Surfaces[role].Sample(targetPose).Clearance);

        internal void Apply(RigPose sourcePose,RigPose targetPose)
        {
            if(!ReferenceEquals(sourcePose.Rig,source)||!ReferenceEquals(targetPose.Rig,target))
                throw new ArgumentException("The relative height poses do not belong to their reviewed rigs.");
            float shift=Expected(sourcePose)-Actual(targetPose);
            if(!RigMath.Finite(shift))throw new InvalidDataException("The source-proportional support height is nonfinite.");
            int pelvis=target.Roles["pelvis"];
            targetPose.SetWorldPoint(pelvis,targetPose.Point(pelvis)+target.GroundNormal*shift);
            if(Math.Abs(Actual(targetPose)-Expected(sourcePose))>tolerance)
                throw new InvalidDataException("The target body could not represent the source-proportional foot height.");
        }

        internal void VerifyDecoded(RigPose sourcePose,RigPose targetPose,float time)
        {
            float error=Math.Abs(Actual(targetPose)-Expected(sourcePose));
            MaximumDecodedError=Math.Max(MaximumDecodedError,error);
            if(!RigMath.Finite(error)||error>tolerance)
                throw new InvalidDataException("The reopened target lost source-proportional foot height at frame "+time+"; error "+error+".");
        }
    }
}
