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
        private readonly HSD_JOBJ[] nativeBones;
        private readonly MeleePlayback nativePlayback;
        private readonly float[][] values;
        private readonly bool[] classical;
        private readonly bool? animationClassical;
        internal readonly MeleeNativeRender Renderer;
        internal readonly MeleeBoneEditPolicy EditPolicy;
        private CHR0Node animation;
        private float frame;
        private readonly Dictionary<string,CHR0EntryNode> entries=new Dictionary<string,CHR0EntryNode>(StringComparer.Ordinal);
        public List<DrawCallBase> DrawCalls=>Renderer.DrawCalls;
        public Box Bounds=>Renderer.GetBox();
        internal MeleeModelEditingBackend(FighterProject project,MDL0Node model,MeleeNativeModel native,MeleeNativeMotion motion)
        {
            model.Children.ToArray();model.BoneGroup?.Children.ToArray();
            nativeBones=MeleeNative.Joints(native.Root);bones=NativeJointIdentity.Bind(model,MeleeGameSource.NativeProvider,MeleeNative.RigHash(native),nativeBones.Length);
            if(bones.Length!=nativeBones.Length)throw new InvalidDataException("Editable model lost an authentic native joint.");
            values=bones.Select(b=>new float[9]).ToArray();classical=new bool[bones.Length];nativePlayback=new MeleePlayback(native,motion);animationClassical=motion.Figatree==null?(bool?)null:(motion.Figatree.Type&1)!=0;
            var common=project.NativeSources.SingleOrDefault(a=>a.Id==project.NativeTarget.CommonSource);
            EditPolicy=common==null?MeleeBoneEditPolicy.Create(native,null):MeleeBoneEditPolicy.Read(native,File.ReadAllBytes(common.WorkingPath));
            var parts=MeleeTargetDocument.Parts(project,native);var report=MeleeProjection.Write(native,null,false,parts);
            Renderer=new MeleeNativeRender(model,report,native){Parts=parts};Renderer.RefreshParts();
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
                classical[i]=frame<=0||animation==null?native:nativePlayback.UsesClassicalScale(i,native);
                if(frame>0&&animationClassical.HasValue&&entries.TryGetValue(b.Name,out var entry)&&entry.KeyArrays.Any(a=>a.KeyCount>0))classical[i]=animationClassical.Value;
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
            // CHR0 enables complete S/R/T groups. Activating one sparse native
            // axis must not turn its unanimated siblings into generic zero/one.
            for(int axis=channel/3*3;axis<channel/3*3+3;axis++)
                if(axis!=channel&&entry.KeyArrays[axis].KeyCount==0)
                    entry.SetKeyframe(axis,0,bind[axis]*(axis>=3&&axis<6?(float)(180/Math.PI):1));
        }
        public void ValidateSharedRigEvaluation()=>throw new NotSupportedException("Melee native animation editing, Undo, project saving and representable action-member export are available. The shared retarget/fitting/contact rig bridge still needs native scale and skin qualification; no approximate MDL0 rig is substituted.");
        public void Dispose()=>Renderer.Dispose();
    }
}
