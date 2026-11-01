using HSDRaw.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
namespace FighterPort.Adapters.Melee
{
    // Independent linear-algebra implementation of the native HSD transform
    // contract, inspected against doldecomp/melee 598987cf347c125c7416517af18bf5c1dc3f15a1.
    // No Brawl rest reconstruction, bind normalization or truncated skin arrays.
    public sealed class MeleePose
    {
        private readonly HSD_JOBJ[] joints;
        private readonly int[] parents;
        private readonly Matrix4x4[] envelope;
        private readonly bool[] hasEnvelope,hasScale;
        private readonly Vector3[] cumulativeScale;
        private readonly float[][] values;
        public Matrix4x4[] World {get;}
        public int JointCount=>joints.Length;
        public MeleePose(MeleeNativeModel model)
        {
            joints=MeleeNative.Joints(model.Root);parents=Enumerable.Repeat(-1,joints.Length).ToArray();var ids=joints.Select((j,i)=>new{j._s,Index=i}).ToDictionary(j=>j._s,j=>j.Index);
            World=new Matrix4x4[joints.Length];envelope=new Matrix4x4[joints.Length];hasEnvelope=new bool[joints.Length];hasScale=new bool[joints.Length];cumulativeScale=new Vector3[joints.Length];values=joints.Select(j=>new float[9]).ToArray();
            for(int i=0;i<joints.Length;i++)
            {
                for(var child=joints[i].Child;child!=null;child=child.Next)parents[ids[child._s]]=i;
                if(joints[i].InverseWorldTransform is HSD_Matrix4x3 m){hasEnvelope[i]=true;envelope[i]=new Matrix4x4(m.M11,m.M21,m.M31,0,m.M12,m.M22,m.M32,0,m.M13,m.M23,m.M33,0,m.M14,m.M24,m.M34,1);}
            }
            Sample(null,-1);
        }
        public void Sample(MeleePlayback playback,float frame)
        {
            for(int i=0;i<joints.Length;i++)
            {
                var v=values[i];var j=joints[i];
                if(playback!=null)playback.SampleInto(i,frame,v);else{v[0]=j.SX;v[1]=j.SY;v[2]=j.SZ;v[3]=j.RX;v[4]=j.RY;v[5]=j.RZ;v[6]=j.TX;v[7]=j.TY;v[8]=j.TZ;}
                int p=parents[i];bool parentScale=p>=0&&hasScale[p];Vector3 scale=new Vector3(v[0],v[1],v[2]);
                bool classical=playback!=null&&frame>=0?playback.UsesClassicalScale(i,(j.Flags&JOBJ_FLAG.CLASSICAL_SCALING)!=0):(j.Flags&JOBJ_FLAG.CLASSICAL_SCALING)!=0;
                hasScale[i]=!classical||parentScale;
                cumulativeScale[i]=classical?(parentScale?cumulativeScale[p]:Vector3.One):scale*(parentScale?cumulativeScale[p]:Vector3.One);
                var unsupported=JOBJ_FLAG.USE_QUATERNION|JOBJ_FLAG.USER_DEFINED_MTX|JOBJ_FLAG.INSTANCE;
                if((j.Flags&unsupported)!=0)throw new NotSupportedException("Native HSD joint requires an unqualified matrix provider: "+(j.Flags&unsupported));
                Matrix4x4 local=Matrix4x4.CreateScale(scale);
                if(parentScale)local*=Matrix4x4.CreateScale(cumulativeScale[p]);
                local*=Matrix4x4.CreateRotationX(v[3])*Matrix4x4.CreateRotationY(v[4])*Matrix4x4.CreateRotationZ(v[5]);
                if(parentScale)
                {
                    var s=cumulativeScale[p];if(s.X==0||s.Y==0||s.Z==0)throw new NotSupportedException("Native inherited zero-scale compensation requires singular-matrix qualification.");
                    local*=Matrix4x4.CreateScale(1/s.X,1/s.Y,1/s.Z);
                }
                local*=Matrix4x4.CreateTranslation(v[6],v[7],v[8]);World[i]=local*(p<0?Matrix4x4.Identity:World[p]);
            }
        }
        public Matrix4x4 VertexMatrix(MeleeMesh mesh,MeleeVertex vertex)
        {
            int owner=mesh.OwnerJoint;
            if(mesh.NativeEnvelope)
            {
                if(vertex.Weights.Count==0)throw new InvalidDataException("Native envelope has no influences.");
                bool right=(joints[owner].Flags&JOBJ_FLAG.SKELETON_ROOT)==0;
                Matrix4x4 matrix;
                if(vertex.Weights[0].Item2>=1f-1.1920929e-7f)
                {
                    int bone=vertex.Weights[0].Item1;matrix=World[bone];if(right)matrix=Envelope(bone)*matrix;
                }
                else
                {
                    matrix=new Matrix4x4();
                    foreach(var weight in vertex.Weights)matrix=Add(matrix,Scale(Envelope(weight.Item1)*World[weight.Item1],weight.Item2));
                    matrix.M14=matrix.M24=matrix.M34=0;matrix.M44=1;
                }
                if(right)matrix=OwnerBasis(owner)*matrix;
                return matrix;
            }
            if(mesh.SharedJoint>=0&&vertex.MatrixIndex==1)return World[mesh.SharedJoint];
            if(mesh.SharedJoint>=0&&vertex.MatrixIndex>1)throw new InvalidDataException("Shared native display list has an out-of-range matrix index.");
            return World[owner];
        }
        private Matrix4x4 Envelope(int joint)
        {if(!hasEnvelope[joint])throw new InvalidDataException("Native weighted deformation requires the authored envelope matrix.");return envelope[joint];}
        private Matrix4x4 OwnerBasis(int owner)
        {
            int skeleton=owner;while(skeleton>=0&&(joints[skeleton].Flags&(JOBJ_FLAG.SKELETON|JOBJ_FLAG.SKELETON_ROOT))==0)skeleton=parents[skeleton];
            if(skeleton<0)throw new InvalidDataException("Native envelope model has no declared skeleton ancestor.");
            Matrix4x4 basis;
            if(skeleton==owner)basis=Envelope(owner);
            else basis=(joints[skeleton].Flags&JOBJ_FLAG.SKELETON_ROOT)!=0?World[skeleton]:Envelope(skeleton)*World[skeleton];
            if(!Matrix4x4.Invert(basis,out Matrix4x4 inverse))throw new NotSupportedException("Native envelope-node basis is singular.");
            return skeleton==owner?inverse:World[owner]*inverse;
        }
        private static Matrix4x4 Scale(Matrix4x4 m,float w)=>new Matrix4x4(m.M11*w,m.M12*w,m.M13*w,m.M14*w,m.M21*w,m.M22*w,m.M23*w,m.M24*w,m.M31*w,m.M32*w,m.M33*w,m.M34*w,m.M41*w,m.M42*w,m.M43*w,m.M44*w);
        private static Matrix4x4 Add(Matrix4x4 a,Matrix4x4 b)=>new Matrix4x4(a.M11+b.M11,a.M12+b.M12,a.M13+b.M13,a.M14+b.M14,a.M21+b.M21,a.M22+b.M22,a.M23+b.M23,a.M24+b.M24,a.M31+b.M31,a.M32+b.M32,a.M33+b.M33,a.M34+b.M34,a.M41+b.M41,a.M42+b.M42,a.M43+b.M43,a.M44+b.M44);
    }
}
