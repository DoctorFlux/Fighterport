using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace FighterPort.NativeEvaluation
{
    // Portable mathematical contracts. Readers translate native flags into these
    // policies; neither game types nor a display-model approximation enter them.
    public sealed class JointPolicy
    {
        public bool CompensatedScale {get;set;}
        public bool? AnimationCompensatedScale {get;set;}
        public bool Skeleton {get;set;}
        public bool SkeletonRoot {get;set;}
        public bool HasInverseBind {get;set;}
        public Matrix4x4 InverseBind {get;set;}
    }
    public sealed class SkinPolicy
    {
        public int Owner {get;set;}
        public int SharedJoint {get;set;}=-1;
        public bool Envelope {get;set;}
        public int MatrixIndex {get;set;}
    }
    public sealed class NativeVertex
    {
        public Vector3 Position {get;set;}
        public Vector3 Normal {get;set;}
        public int[] Joints {get;set;}
        public float[] Weights {get;set;}
        public SkinPolicy Skin {get;set;}
    }
    public sealed class NativeRigData
    {
        public string Identity {get;set;}
        public JointPolicy[] Joints {get;set;}
        public NativeVertex[] Vertices {get;set;}
        public Dictionary<string,int> Roles {get;set;}
    }
    public sealed class StaticInfluence
    {
        public int Joint {get;set;}
        public Vector3 Local {get;set;}
        public float Weight {get;set;}
    }
    public static class EvaluationMath
    {
        public static StaticInfluence[] StaticWeights(JointPolicy[] joints,SkinPolicy policy,Vector3 point,int[] bones,float[] weights)
        {
            int owner=policy.Owner;
            if(!policy.Envelope)return new[]{new StaticInfluence{Joint=policy.SharedJoint>=0&&policy.MatrixIndex==1?policy.SharedJoint:owner,Local=point,Weight=1}};
            bool right=!joints[owner].SkeletonRoot;
            if(right)
            {
                if(!joints[owner].Skeleton)return null; // a moving owner/ancestor basis is not a fixed influence equation
                if(!Matrix4x4.Invert(InverseBind(joints,owner),out var basis))throw new NotSupportedException("The native envelope owner basis is singular.");
                point=Vector3.Transform(point,basis);
            }
            if(weights[0]>=1f-1.1920929e-7f)return new[]{new StaticInfluence{Joint=bones[0],Weight=1,Local=right?Vector3.Transform(point,InverseBind(joints,bones[0])):point}};
            var result=new StaticInfluence[bones.Length];for(int i=0;i<result.Length;i++)result[i]=new StaticInfluence{Joint=bones[i],Weight=weights[i],Local=Vector3.Transform(point,InverseBind(joints,bones[i]))};return result;
        }
        public static Matrix4x4 Local(Vector3 scale,Quaternion rotation,Vector3 translation,int parent,bool compensate,Vector3[] cumulative,bool[] hasScale,int joint)
        {
            bool inherited=parent>=0&&hasScale[parent];
            hasScale[joint]=compensate||inherited;
            cumulative[joint]=compensate?scale*(inherited?cumulative[parent]:Vector3.One):(inherited?cumulative[parent]:Vector3.One);
            var local=Matrix4x4.CreateScale(scale);
            if(inherited)local*=Matrix4x4.CreateScale(cumulative[parent]);
            local*=Matrix4x4.CreateFromQuaternion(rotation);
            if(inherited)
            {
                var s=cumulative[parent];if(s.X==0||s.Y==0||s.Z==0)throw new NotSupportedException("Singular inherited scale compensation is unavailable; native transforms were not normalized.");
                local*=Matrix4x4.CreateScale(1/s.X,1/s.Y,1/s.Z);
            }
            return local*Matrix4x4.CreateTranslation(translation);
        }
        static Matrix4x4 InverseBind(JointPolicy[] joints,int joint)
        {if(!joints[joint].HasInverseBind)throw new InvalidDataException("An authored skin inverse bind is missing.");return joints[joint].InverseBind;}
        public static Matrix4x4 SkinMatrix(JointPolicy[] joints,int[] parents,Matrix4x4[] world,SkinPolicy policy,int[] bones,float[] weights)
        {
            int owner=policy.Owner;
            if(owner<0||owner>=joints.Length||policy.SharedJoint>=joints.Length||policy.MatrixIndex<0||policy.SharedJoint>=0&&policy.MatrixIndex>1)throw new InvalidDataException("Native skin policy lies outside the original rig or matrix slots.");
            if(!policy.Envelope)return world[policy.SharedJoint>=0&&policy.MatrixIndex==1?policy.SharedJoint:owner];
            if(bones==null||weights==null||bones.Length==0||bones.Length!=weights.Length)throw new InvalidDataException("Complete native skin influences are required.");
            bool right=!joints[owner].SkeletonRoot;Matrix4x4 matrix;
            if(weights[0]>=1f-1.1920929e-7f)
                matrix=(right?InverseBind(joints,bones[0]):Matrix4x4.Identity)*world[bones[0]];
            else
            {
                matrix=new Matrix4x4();
                for(int i=0;i<bones.Length;i++)matrix+=InverseBind(joints,bones[i])*world[bones[i]]*weights[i];
                matrix.M14=matrix.M24=matrix.M34=0;matrix.M44=1;
            }
            if(right)
            {
                int skeleton=owner;while(skeleton>=0&&!joints[skeleton].Skeleton&&!joints[skeleton].SkeletonRoot)skeleton=parents[skeleton];
                if(skeleton<0)throw new InvalidDataException("The native envelope owner has no declared skeleton basis.");
                var basis=skeleton==owner?InverseBind(joints,owner):joints[skeleton].SkeletonRoot?world[skeleton]:InverseBind(joints,skeleton)*world[skeleton];
                if(!Matrix4x4.Invert(basis,out var inverse))throw new NotSupportedException("The native owner basis is singular.");
                matrix=(skeleton==owner?inverse:world[owner]*inverse)*matrix;
            }
            return matrix;
        }
    }
}
