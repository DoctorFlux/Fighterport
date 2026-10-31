using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    internal sealed class SupportProposalUnavailable : InvalidOperationException
    {
        internal SupportProposalUnavailable(string message):base(message){}
    }
    // Offline authoring only. Transport generated corrections over continuous
    // source FK, retaining every original solved anchor and source-owned local.
    internal sealed class RetargetCorrectionTrajectory
    {
        internal const string Policy="original-next-eighth-correction-trajectory-2";
        sealed class Anchor
        {
            internal float Time;
            internal AdaptiveRotationPose Pose;
            internal Quaternion[] Rotations;
            internal Vector3[] Translations;
        }
        readonly Rig rig;
        readonly int[] editable;
        readonly Func<float,RigPose> fk;
        readonly Func<int,float,Vector3> branch;
        Anchor[] nodes;
        bool refined;
        internal int AnchorCount=>nodes.Length;
        internal float[] AnchorTimes=>nodes.Select(node=>node.Time).ToArray();
        internal RetargetCorrectionTrajectory(Rig rig,AdaptiveRotationPose[] anchors,int subdivisions,bool[] editable,
            Func<float,RigPose> fk,Func<int,float,Vector3> branch,CancellationToken token)
        {
            if(subdivisions!=8||anchors.Length<1||editable.Length!=rig.Joints.Length)throw new ArgumentException("Invalid bounded native correction trajectory.");
            this.rig=rig;this.editable=Enumerable.Range(0,editable.Length).Where(b=>editable[b]).ToArray();this.fk=fk;this.branch=branch;
            nodes=anchors.Select((pose,index)=>{token.ThrowIfCancellationRequested();return MakeAnchor(index/(float)subdivisions,pose);}).ToArray();
        }
        Anchor MakeAnchor(float time,AdaptiveRotationPose pose)
        {
            var baseline=fk(time);var node=new Anchor{Time=time,Pose=pose,Rotations=new Quaternion[rig.Joints.Length],Translations=new Vector3[rig.Joints.Length]};
            foreach(int bone in editable)
            {
                if(pose.Scales[bone]!=baseline.Local[bone].Scale)throw new InvalidOperationException("Generated corrections cannot replace native scale semantics.");
                var solved=pose.LocalRotations[bone];var original=baseline.Local[bone].Rotation;
                node.Rotations[bone]=solved==original?Quaternion.Identity:Quaternion.Normalize(solved*Quaternion.Inverse(original));
                node.Translations[bone]=pose.Translations[bone]-baseline.Local[bone].Translation;
            }
            return node;
        }
        RigPose Between(Anchor first,Anchor last,float time)
        {
            float weight=(time-first.Time)/(last.Time-first.Time);var baseline=fk(time);var local=(JointTransform[])baseline.Local.Clone();
            foreach(int bone in editable)
            {
                var a=first.Rotations[bone];var b=last.Rotations[bone];
                if(a!=Quaternion.Identity||b!=Quaternion.Identity)local[bone].Rotation=Quaternion.Normalize(Quaternion.Slerp(a,b,weight)*local[bone].Rotation);
                var shift=Vector3.Lerp(first.Translations[bone],last.Translations[bone],weight);
                if(shift!=Vector3.Zero)local[bone].Translation+=shift;
            }
            return new RigPose(rig,local);
        }
        // A preparatory, geometry-driven refinement; not a new contact solver.
        // Add original Next samples only where transported correction violates
        // reserved support headroom. Original anchors are never replaced.
        internal void RefineSupports(Func<float,bool> needsCheck,Func<float,RigPose,bool> acceptable,
            Func<float,AdaptiveRotationPose> solve,Func<long,bool> reserve,long witnessWork,CancellationToken token)
        {
            if(refined||needsCheck==null||acceptable==null||solve==null||reserve==null||witnessWork<1)throw new ArgumentException("Invalid support-refinement preparation.");
            refined=true;var prepared=new List<Anchor>{nodes[0]};
            void Charge(){token.ThrowIfCancellationRequested();if(!reserve(witnessWork))throw new SupportProposalUnavailable("Support proposal reached its bounded preparation budget; original anchors remain unchanged.");}
            foreach(var node in nodes)
            {
                if(!needsCheck(node.Time))continue;
                Charge();var p=node.Pose;
                var pose=new RigPose(rig,Enumerable.Range(0,rig.Joints.Length).Select(b=>new JointTransform(p.Translations[b],p.LocalRotations[b],p.Scales[b])).ToArray());
                if(!acceptable(node.Time,pose))throw new SupportProposalUnavailable("An original solved support anchor exceeds the refinement reserve at "+node.Time+"; review the support context. Original anchors remain unchanged.");
            }
            void Refine(Anchor first,Anchor last,int depth)
            {
                bool failed=false;
                foreach(float fraction in new[]{.25f,.5f,.75f})
                {
                    float time=first.Time+(last.Time-first.Time)*fraction;
                    if(time<=first.Time||time>=last.Time||!needsCheck(time))continue;
                    Charge();if(!acceptable(time,Between(first,last,time)))failed=true;
                }
                if(!failed){prepared.Add(last);return;}
                float middle=first.Time+(last.Time-first.Time)*.5f;
                if(depth>=20||middle<=first.Time||middle>=last.Time)throw new SupportProposalUnavailable("Original Next support cannot satisfy the contact reserve at "+first.Time+".."+last.Time+"; review the support context. Original anchors remain unchanged.");
                Charge();var added=MakeAnchor(middle,solve(middle));
                Refine(first,added,depth+1);Refine(added,last,depth+1);
            }
            for(int i=1;i<nodes.Length;i++)Refine(nodes[i-1],nodes[i],0);
            nodes=prepared.ToArray();
        }
        internal AdaptiveRotationPose Evaluate(float time)
        {
            time=Math.Max(nodes[0].Time,Math.Min(nodes[nodes.Length-1].Time,time));int first=0,last=nodes.Length-1;
            while(first<last){int middle=(first+last+1)/2;if(nodes[middle].Time<=time)first=middle;else last=middle-1;}
            if(time==nodes[first].Time)return nodes[first].Pose;
            var pose=Between(nodes[first],nodes[first+1],time);var local=pose.Local;
            return new AdaptiveRotationPose{EulerDegrees=local.Select((value,bone)=>BrawlRetargeter.EulerNear(value.Rotation,branch(bone,time))).ToArray(),
                WorldRotations=pose.Orientation,LocalRotations=local.Select(v=>v.Rotation).ToArray(),Translations=local.Select(v=>v.Translation).ToArray(),Scales=local.Select(v=>v.Scale).ToArray()};
        }
    }
}
