using BrawlCrate.Studio.Retargeting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

internal static partial class Program
{
    static void CorrectionSupportChecks()
    {
        foreach(bool renamed in new[]{false,true})foreach(bool permuted in new[]{false,true})foreach(float size in new[]{.3f,1.7f})
        {
            int[] order=permuted?new[]{0,1,6,3,5,2,4}:Enumerable.Range(0,7).ToArray();
            var inverse=Enumerable.Range(0,7).ToDictionary(i=>order[i],i=>i);
            var joints=order.Select(logical=>new RigJoint((renamed?"Opaque_":"Fixture_")+logical,logical==0?-1:inverse[logical==1?0:1],
                new JointTransform(new Vector3(0,logical==1?2*size:logical>1?-2*size:0,0),Quaternion.Identity,Vector3.One))).ToArray();
            var rig=new Rig("Original procedural support fixture","No game assets",joints,new Dictionary<string,int>(),size);
            RigPose Fk(float time)
            {
                var local=joints.Select(j=>j.Bind).ToArray();local[0].Translation.X=time*3*size;
                local[0].Rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,time*(float)Math.PI*4);
                if(time>.01f&&time<.02f)local[1].Translation.X+=(float)Math.Sin((time-.01f)*Math.PI/.01f)*.4f*size;
                local[inverse[3]].Scale=new Vector3(-1,2,.5f);
                return new RigPose(rig,local);
            }
            AdaptiveRotationPose Solved(float time)
            {
                var p=Fk(time);float angle=.9f*(float)Math.Sin(time*Math.PI*2);
                p.Local[1].Rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,angle);
                p.Local[1].Translation.Y=2*size*(float)Math.Cos(angle);p.Update();
                return new AdaptiveRotationPose{EulerDegrees=p.Local.Select(v=>BrawlRetargeter.Euler(v.Rotation)).ToArray(),
                    WorldRotations=p.Orientation,LocalRotations=p.Local.Select(v=>v.Rotation).ToArray(),Translations=p.Local.Select(v=>v.Translation).ToArray(),Scales=p.Local.Select(v=>v.Scale).ToArray()};
            }
            var patch=new SkinPoint{Weights=Enumerable.Range(2,5).Select(logical=>new SkinWeight{Joint=inverse[logical],Local=Vector3.Zero,Weight=.2f}).ToArray()};
            var anchors=Enumerable.Range(0,9).Select(i=>Solved(i/8f)).ToArray();
            RetargetCorrectionTrajectory Create()=>new RetargetCorrectionTrajectory(rig,anchors,8,new[]{false,true,false,false,false,false,false},Fk,(b,t)=>b==0?new Vector3(0,t*720,0):Vector3.Zero,CancellationToken.None);
            var result=Create();long work=0;
            result.RefineSupports(t=>true,(t,p)=>Math.Abs(patch.Evaluate(p).Y)<=.001f*size,Solved,count=>{work+=count;return work<=2000000;},33,CancellationToken.None);
            Check(result.AnchorCount>anchors.Length&&work<=2000000,"Measured full-influence support curvature adds bounded original-solver anchors on unequal, renamed and permuted rigs",new{renamed,permuted,size,result.AnchorCount,work});
            Check(Enumerable.Range(0,9).All(i=>ReferenceEquals(result.Evaluate(i/8f),anchors[i])),"Support refinement retains every exact original eighth-frame anchor");
            double maximum=0,maxFkError=0;bool exactMotion=true;int samples=0;
            foreach(float time in Enumerable.Range(0,1025).Select(i=>i/1024f).Concat(new[]{.015f,.137f,.371f,.623f,.913f}))
            {
                var sampled=result.Evaluate(time);var posed=new RigPose(rig,Enumerable.Range(0,joints.Length).Select(b=>new JointTransform(sampled.Translations[b],sampled.LocalRotations[b],sampled.Scales[b])).ToArray());
                maximum=Math.Max(maximum,Math.Abs(patch.Evaluate(posed).Y)/size);
                var fk=Fk(time);
                exactMotion&=sampled.LocalRotations[0]==fk.Local[0].Rotation&&sampled.Translations[0]==fk.Local[0].Translation&&sampled.Scales.SequenceEqual(fk.Local.Select(v=>v.Scale));
                maxFkError=Math.Max(maxFkError,Math.Abs(sampled.Translations[1].X-fk.Local[1].Translation.X));samples++;
            }
            Check(exactMotion,"Refined support retains authored root turns/travel and every negative/nonuniform scale",new{samples});
            Check(maxFkError<.0000001,"Refined corrections retain source-FK motion between original anchors",new{samples,maxFkError});
            Check(maximum<=.002,"Independent analytic floor expectations pass withheld samples with all five skin influences",new{renamed,permuted,size,maximum});
            var refused=Create();bool exhausted=false;
            try{refused.RefineSupports(t=>true,(t,p)=>false,Solved,count=>false,33,CancellationToken.None);}catch(InvalidOperationException){exhausted=true;}
            Check(exhausted&&refused.AnchorCount==9&&ReferenceEquals(refused.Evaluate(.5f),anchors[4]),"Exhausted support preparation publishes no partial reference or changed anchor");
            AdaptiveRotationPose Turning(float time)
            {
                var p=Fk(time);p.Local[1].Rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.4f*(float)Math.Sin(time*Math.PI*8));p.Update();
                return new AdaptiveRotationPose{EulerDegrees=p.Local.Select(v=>BrawlRetargeter.Euler(v.Rotation)).ToArray(),WorldRotations=p.Orientation,
                    LocalRotations=p.Local.Select(v=>v.Rotation).ToArray(),Translations=p.Local.Select(v=>v.Translation).ToArray(),Scales=p.Local.Select(v=>v.Scale).ToArray()};
            }
            var turningAnchors=Enumerable.Range(0,9).Select(i=>Turning(i/8f)).ToArray();
            var turning=new RetargetCorrectionTrajectory(rig,turningAnchors,8,new[]{false,true,false,false,false,false,false},Fk,(b,t)=>Vector3.Zero,CancellationToken.None);
            long turningWork=0;
            turning.RefineSupports(t=>true,(t,p)=>ContactMotion.EndOrientationDegrees(p.Orientation[1],Turning(t).WorldRotations[1])<=.125,
                Turning,count=>{turningWork+=count;return turningWork<=2000000;},33,CancellationToken.None);
            double orientationMaximum=Enumerable.Range(0,1025).Select(i=>i/1024f).Max(t=>SpanAngle(turning.Evaluate(t).WorldRotations[1],Turning(t).WorldRotations[1]));
            Check(turning.AnchorCount>9&&orientationMaximum<=.125,"Orientation-only curvature refines contact preparation even when floor position alone cannot reveal it",new{renamed,permuted,size,turning.AnchorCount,orientationMaximum,turningWork});
            Check(Enumerable.Range(0,9).All(i=>ReferenceEquals(turning.Evaluate(i/8f),turningAnchors[i])),"Orientation refinement retains every original solved reference anchor");
        }
    }
}
