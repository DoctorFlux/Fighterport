using System;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    // The original contact solver can choose different redundant-joint optima
    // at arbitrarily close times. Those independent optima are not a continuous
    // trajectory. Keep every original solved eighth-frame pose, and transport
    // only its correction over the authentic, continuously evaluated source FK.
    // This is authoring the finished master, never lossy export optimization.
    internal sealed class RetargetCorrectionTrajectory
    {
        internal const string Policy="original-next-eighth-correction-trajectory-1";
        readonly Rig rig;
        readonly AdaptiveRotationPose[] anchors;
        readonly Quaternion[][] rotations;
        readonly Vector3[][] translations;
        readonly int[] changed;
        readonly int subdivisions;
        readonly Func<float,RigPose> fk;
        readonly Func<int,float,Vector3> branch;
        internal RetargetCorrectionTrajectory(Rig rig,AdaptiveRotationPose[] anchors,int subdivisions,bool[] editable,
            Func<float,RigPose> fk,Func<int,float,Vector3> branch,CancellationToken token)
        {
            if(subdivisions!=8||anchors.Length<1||editable.Length!=rig.Joints.Length)throw new ArgumentException("Invalid bounded native correction trajectory.");
            this.rig=rig;this.anchors=anchors;this.subdivisions=subdivisions;this.fk=fk;this.branch=branch;
            rotations=new Quaternion[anchors.Length][];translations=new Vector3[anchors.Length][];
            var differs=new bool[editable.Length];
            for(int i=0;i<anchors.Length;i++)
            {
                token.ThrowIfCancellationRequested();var baseline=fk(i/(float)subdivisions);
                rotations[i]=new Quaternion[editable.Length];translations[i]=new Vector3[editable.Length];
                for(int bone=0;bone<editable.Length;bone++)
                {
                    if(!editable[bone])continue;
                    if(anchors[i].Scales[bone]!=baseline.Local[bone].Scale)throw new InvalidOperationException("Contact corrections cannot replace native scale semantics.");
                    var solved=anchors[i].LocalRotations[bone];var original=baseline.Local[bone].Rotation;
                    rotations[i][bone]=solved==original?Quaternion.Identity:Quaternion.Normalize(solved*Quaternion.Inverse(original));
                    translations[i][bone]=anchors[i].Translations[bone]-baseline.Local[bone].Translation;
                    differs[bone]|=solved!=original||translations[i][bone]!=Vector3.Zero;
                }
            }
            changed=Enumerable.Range(0,editable.Length).Where(b=>differs[b]).ToArray();
        }
        internal AdaptiveRotationPose Evaluate(float time)
        {
            float at=Math.Max(0,Math.Min(anchors.Length-1,time*subdivisions));int first=(int)at,last=Math.Min(first+1,anchors.Length-1);
            if(at==first)return anchors[first];
            float weight=at-first;var baseline=fk(time);var local=(JointTransform[])baseline.Local.Clone();
            foreach(int bone in changed)
            {
                var a=rotations[first][bone];var b=rotations[last][bone];
                if(a!=Quaternion.Identity||b!=Quaternion.Identity)local[bone].Rotation=Quaternion.Normalize(Quaternion.Slerp(a,b,weight)*local[bone].Rotation);
                var shift=Vector3.Lerp(translations[first][bone],translations[last][bone],weight);
                if(shift!=Vector3.Zero)local[bone].Translation+=shift;
            }
            var pose=new RigPose(rig,local);
            return new AdaptiveRotationPose{EulerDegrees=local.Select((value,bone)=>BrawlRetargeter.EulerNear(value.Rotation,branch(bone,time))).ToArray(),
                WorldRotations=pose.Orientation,LocalRotations=local.Select(v=>v.Rotation).ToArray(),Translations=local.Select(v=>v.Translation).ToArray(),Scales=local.Select(v=>v.Scale).ToArray()};
        }
    }
}
