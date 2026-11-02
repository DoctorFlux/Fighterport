using FighterPort.Adapters.Melee;
using HSDRaw.Common;
using BrawlLib.Modeling;
using BrawlLib.OpenGL;
using BrawlLib.Internal;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NMatrix=System.Numerics.Matrix4x4;
namespace BrawlCrate.Studio
{
    internal sealed class MeleeModelEditingBackend:INativeModelEditingBackend
    {
        private readonly MDL0BoneNode[] bones;
        private readonly MDL0Node authoringModel;
        private readonly HSD_JOBJ[] nativeBones;
        private readonly MeleePlayback nativePlayback;
        private readonly float[][] values;
        private readonly bool[] classical;
        private readonly bool? animationClassical;
        private readonly MeleeNativeModel nativeModel;
        private readonly MeleeNativeMotion originalMotion;
        private readonly byte[] commonContext;
        private readonly MeleeProjectionReport report;
        private FighterPort.NativeEvaluation.NativeRigData evaluation;
        internal readonly MeleeNativeRender Renderer;
        internal readonly MeleeBoneEditPolicy EditPolicy;
        private CHR0Node animation;
        private float frame;
        private readonly Dictionary<string,CHR0EntryNode> entries=new Dictionary<string,CHR0EntryNode>(StringComparer.Ordinal);
        public List<DrawCallBase> DrawCalls=>Renderer.DrawCalls;
        public Box Bounds=>Renderer.GetBox();
        internal MeleeModelEditingBackend(FighterProject project,MDL0Node model,MeleeNativeModel native,MeleeNativeMotion motion)
            :this(model,native,motion,MeleeTargetDocument.Parts(project,native),project.NativeSources.SingleOrDefault(a=>a.Id==project.NativeTarget.CommonSource) is FighterAsset common?File.ReadAllBytes(common.WorkingPath):null)
        {}
        internal MeleeModelEditingBackend(MDL0Node model,MeleeNativeModel native,MeleeNativeMotion motion,MeleePartVisibility parts,byte[] commonBytes,MeleeNativeRender renderer=null)
        {
            model.Children.ToArray();model.BoneGroup?.Children.ToArray();
            authoringModel=model;
            nativeModel=native;originalMotion=motion;commonContext=commonBytes==null?null:(byte[])commonBytes.Clone();nativeBones=MeleeNative.Joints(native.Root);bones=NativeJointIdentity.Bind(model,MeleeGameSource.NativeProvider,MeleeNative.RigHash(native),nativeBones.Length);
            if(bones.Length!=nativeBones.Length)throw new InvalidDataException("Editable model lost an authentic native joint.");
            values=bones.Select(b=>new float[9]).ToArray();classical=new bool[bones.Length];nativePlayback=new MeleePlayback(native,motion);animationClassical=motion.Figatree==null?(bool?)null:(motion.Figatree.Type&1)!=0;
            EditPolicy=commonBytes==null?MeleeBoneEditPolicy.Create(native,null):MeleeBoneEditPolicy.Read(native,commonBytes);
            report=MeleeProjection.Write(native,null,false,parts);
            Renderer=renderer??new MeleeNativeRender(model,report,native){Parts=parts};Renderer.RefreshParts();
        }
        public void Sample(MDL0Node model,CHR0Node clip,float displayFrame){animation=clip;frame=displayFrame;UpdatePose(model);}
        public void UpdatePose(MDL0Node model)
        {
            entries.Clear();if(animation!=null)foreach(var child in animation.Children)if(child is CHR0EntryNode entry)entries.Add(entry.Name,entry);
            for(int i=0;i<bones.Length;i++)
            {
                var b=bones[i];var v=values[i];for(int axis=0;axis<9;axis++)v[axis]=b.FrameState[axis];
                for(int axis=3;axis<6;axis++)v[axis]*=(float)(Math.PI/180);
                bool native=(nativeBones[i].Flags&JOBJ_FLAG.CLASSICAL_SCALING)!=0;
                classical[i]=frame<=0||animation==null?native:entries.TryGetValue(b.Name,out var entry)?!entry.ClassicScaleOff:native;
            }
            Renderer.UpdateAuthored(values,classical);
            for(int i=0;i<bones.Length;i++)
            {
                var matrix=Renderer.NativeWorld[i];bones[i]._frameMatrix=Convert(matrix);
                if(NMatrix.Invert(matrix,out var inverse))bones[i]._inverseFrameMatrix=Convert(inverse);
            }
            model.UpdateMeshesFromCurrentMatrices();
        }
        internal static Matrix Convert(NMatrix m)
        {
            var result=new Matrix();result[0]=m.M11;result[1]=m.M12;result[2]=m.M13;result[3]=m.M14;result[4]=m.M21;result[5]=m.M22;result[6]=m.M23;result[7]=m.M24;result[8]=m.M31;result[9]=m.M32;result[10]=m.M33;result[11]=m.M34;result[12]=m.M41;result[13]=m.M42;result[14]=m.M43;result[15]=m.M44;return result;
        }
        public bool CanEditAnimationBone(string name){int index=Array.FindIndex(bones,b=>b.Name==name);return EditPolicy.CanEdit(index);}
        public void PrepareAnimationChannel(CHR0EntryNode entry,int channel)
        {
            int joint=Array.FindIndex(bones,b=>b.Name==entry.Name);
            if(channel<0||channel>8||!EditPolicy.CanEdit(joint))throw new InvalidOperationException("This native animation channel is not editable.");
            var j=nativeBones[joint];float[] bind={j.SX,j.SY,j.SZ,j.RX,j.RY,j.RZ,j.TX,j.TY,j.TZ};
            if(entry.KeyArrays.All(a=>a.KeyCount==0))
            {
                var clip=entry.Parent as CHR0Node;
                var policy=clip?.Children.OfType<CHR0EntryNode>().Where(e=>!ReferenceEquals(e,entry)).Select(e=>e.ClassicScaleOff).Distinct().ToArray()??new bool[0];
                if(policy.Length>1)throw new NotSupportedException("This animation mixes native scale policies. Review a native policy for the new bone before adding its first channel.");
                entry.ClassicScaleOff=policy.Length==1?policy[0]:(j.Flags&JOBJ_FLAG.CLASSICAL_SCALING)==0;
            }
            // CHR0 enables complete S/R/T groups. Activating one sparse native
            // axis must not turn its unanimated siblings into generic zero/one.
            for(int axis=channel/3*3;axis<channel/3*3+3;axis++)
                if(axis!=channel&&entry.KeyArrays[axis].KeyCount==0)
                    entry.SetKeyframe(axis,0,bind[axis]*(axis>=3&&axis<6?(float)(180/Math.PI):1));
        }
        public FighterPort.NativeEvaluation.NativeRigData CaptureEvaluation()
        {
            if(evaluation!=null)return evaluation;
            var policies=nativeBones.Select(j=>new FighterPort.NativeEvaluation.JointPolicy{CompensatedScale=(j.Flags&JOBJ_FLAG.CLASSICAL_SCALING)==0,Skeleton=(j.Flags&JOBJ_FLAG.SKELETON)!=0,SkeletonRoot=(j.Flags&JOBJ_FLAG.SKELETON_ROOT)!=0,HasInverseBind=j.InverseWorldTransform!=null,InverseBind=j.InverseWorldTransform is HSD_Matrix4x3 m?new NMatrix(m.M11,m.M21,m.M31,0,m.M12,m.M22,m.M32,0,m.M13,m.M23,m.M33,0,m.M14,m.M24,m.M34,1):NMatrix.Identity}).ToArray();
            var vertices=report.Meshes.SelectMany(mesh=>mesh.Vertices.Select(v=>new FighterPort.NativeEvaluation.NativeVertex{Position=v.NativePosition,Normal=v.NativeNormal,Joints=v.Weights.Select(w=>w.Item1).ToArray(),Weights=v.Weights.Select(w=>w.Item2).ToArray(),Skin=new FighterPort.NativeEvaluation.SkinPolicy{Owner=mesh.OwnerJoint,SharedJoint=mesh.SharedJoint,MatrixIndex=v.MatrixIndex,Envelope=mesh.NativeEnvelope}})).ToArray();
            return evaluation=new FighterPort.NativeEvaluation.NativeRigData{Identity=MeleeNative.RigHash(nativeModel),Joints=policies,Vertices=vertices,Roles=new Dictionary<string,int>(EditPolicy.Roles)};
        }
        public BrawlLib.Modeling.INativeModelEditingBackend CreateSnapshotBackend(MDL0Node model)=>new MeleeModelEditingBackend(model,nativeModel,originalMotion,Renderer.Parts.Copy(),commonContext);
        public void ValidateSharedRigEvaluation(){if(authoringModel.IsDirty)throw new NotSupportedException("Native geometry, weights or bind edits need their own verified write mapping before shared rig tools can use them. The original native rig was not substituted.");if(!EditPolicy.Available)throw new NotSupportedException("Review unambiguous native body/root roles before using the shared rig tools.");CaptureEvaluation();}
        public void Dispose()=>Renderer.Dispose();
    }
}
