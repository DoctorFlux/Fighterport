using BrawlLib.Internal;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using N=System.Numerics;

namespace BrawlCrate.Studio
{
    internal sealed class FittingModelView:UserControl
    {
        internal readonly ModelPanel Viewport=new ModelPanel{Dock=DockStyle.Fill};
        internal readonly MDL0Node Model;internal readonly Label Heading=new Label{Dock=DockStyle.Top,Height=38,Padding=new Padding(5)};
        internal SceneFittingOptions Options;internal float[] Up,Forward;internal bool Source;
        internal int ReferencePreviewSamples { get; private set; }
        internal int ReferencePreviewSkinPasses { get; private set; }
        internal FittingModelView(MDL0Node model,ResourceNode root,string title,bool source,ModelPanelViewport viewer,IEnumerable<ResourceNode> references=null)
        {
            Model=model;Source=source;Heading.Text=title;
            // This is the model previewer's actual native ModelPanel, MDL0
            // materials and shader generator. Copy its viewport settings, with
            // independent attributes so a review cannot mutate the authoring view.
            var settings=viewer.GetInfo();settings._renderAttrib=new ModelRenderAttributes();
            foreach(var field in typeof(ModelRenderAttributes).GetFields())field.SetValue(settings._renderAttrib,field.GetValue(viewer._renderAttrib));
            settings._percentages=new Vector4(0,0,1,1);settings._allowSelection=false;settings._renderFloor=false;
            Viewport.ClearViewports();Viewport.AddViewport(settings.AsViewport());
            Model.ResetToBindState();Model.ApplySHP(null,0);
            Viewport.AddReference(root);if(references!=null)foreach(var reference in references)Viewport.AddReference(reference);Viewport.AddTarget(model);
            Viewport.PreRender+=v=>{DrawSupport();GL.Enable(EnableCap.DepthTest);GL.DepthFunc(DepthFunction.Lequal);};
            Viewport.PostRender+=v=>{GL.PushAttrib(AttribMask.AllAttribBits);try{GL.Enable(EnableCap.Blend);GL.BlendFunc(BlendingFactorSrc.SrcAlpha,BlendingFactorDest.OneMinusSrcAlpha);GL.Disable(EnableCap.Lighting);ModelEditorBase.RenderModelOverlays(Model,v);}finally{GL.PopAttrib();}};
            Controls.Add(Viewport);Controls.Add(Heading);ShownFrame();
        }
        internal void ShownFrame(){if(!IsHandleCreated)return;var box=Model.GetBox();var center=(box.Min+box.Max)*.5f;var half=(box.Max-box.Min)*.65f;var camera=Viewport.Camera;if(camera.Orthographic){Viewport.SetCamWithBox(center-half,center+half);return;}float tangent=(float)Math.Tan(camera.VerticalFieldOfView*Math.PI/360),aspect=Math.Max(.1f,Viewport.Width/(float)Math.Max(1,Viewport.Height));float distance=Math.Max(half._y/tangent,half._x/(tangent*aspect))+half._z+Math.Max(1,camera.NearDepth*2);camera.Set(center+new Vector3(0,0,distance),Vector3.Zero,Vector3.One);Viewport.CurrentViewport._viewDistance=distance;Viewport.Invalidate();}
        internal void Pose(CHR0Node animation,float frame){Model.ApplyCHR(animation,frame);Viewport.Invalidate();}
        internal void PoseBodyReference(CHR0Node reference,float referenceFrame,CHR0Node source,float sourceFrame,IEnumerable<string> protectedControls)
        {
            if(reference==null||source==null||protectedControls==null||float.IsNaN(referenceFrame)||float.IsInfinity(referenceFrame)||float.IsNaN(sourceFrame)||float.IsInfinity(sourceFrame)||referenceFrame<1||referenceFrame>reference.FrameCount||sourceFrame<1||sourceFrame>source.FrameCount)
                throw new InvalidDataException("Reference-body preview requires valid native frames and reviewed source motion controls.");
            // This model belongs to the independent preview. The reference
            // pose is intermediate: its geometry is never shown before the
            // reviewed controls below have been replaced. Keep native curve/FK
            // evaluation, and skin the final composed pose only once.
            bool suppressMesh=Model._dontUpdateMesh;
            try { Model._dontUpdateMesh=true;Model.ApplyCHR(reference,referenceFrame); }
            finally { Model._dontUpdateMesh=suppressMesh; }
            foreach(string name in protectedControls.Distinct(StringComparer.Ordinal))
            {
                var bone=Model._linker.BoneCache.SingleOrDefault(b=>b.Name==name)??throw new InvalidDataException("A reviewed motion control is missing from the reference preview model: "+name);
                var pose=bone.BindState;var track=source.Children.OfType<CHR0EntryNode>().SingleOrDefault(t=>string.Equals(t.Name,name,StringComparison.OrdinalIgnoreCase));
                if(track!=null)for(int channel=0;channel<9;channel++)if(track.KeyArrays[channel].KeyCount>0)pose[channel]=track.GetFrameValue(channel,sourceFrame-1);
                pose.CalcTransforms();bone.FrameState=pose;
            }
            // Recalculate complete native FK and skinning after replacing only
            // reviewed control locals. The source and reference clips stay owned
            // and unchanged; this path performs no loading, hashing or solving.
            Model.WeightMeshes();ReferencePreviewSamples++;
            if(!suppressMesh)ReferencePreviewSkinPasses++;
            Viewport.Invalidate();
        }
        private void DrawSupport()
        {
            if(Options==null||Options.Scene==0||Up==null||Forward==null)return;
            var up=new N.Vector3(Up[0],Up[1],Up[2]);var forward=new N.Vector3(Forward[0],Forward[1],Forward[2])*(Options.ReverseEdge?-1:1);var right=N.Vector3.Normalize(N.Vector3.Cross(up,forward));
            var box=Model.GetBox();float size=Math.Max(2,(box.Max-box.Min).TrueDistance());float floor=(Source?Options.SourceFloor:Options.TargetFloor)??0,edge=Source?Options.SourceEdge:Options.TargetEdge;
            N.Vector3 center=up*floor+forward*(Options.Scene==2?edge:0);var a=center-right*size;var b=center+right*size;var c=b+forward*size;var d=a+forward*size;if(Options.Scene!=2){a-=forward*size;b-=forward*size;}
            void Vertex(N.Vector3 p)=>GL.Vertex3(p.X,p.Y,p.Z);
            GL.PushAttrib(AttribMask.AllAttribBits);
            try
            {
                GL.Disable(EnableCap.Lighting);GL.Disable(EnableCap.CullFace);GL.Enable(EnableCap.DepthTest);GL.Color3(Color.FromArgb(63,67,86));GL.Begin(BeginMode.Quads);Vertex(a);Vertex(b);Vertex(c);Vertex(d);if(Options.Scene==2){Vertex(a);Vertex(a-up*size);Vertex(b-up*size);Vertex(b);}GL.End();
                GL.Color3(Color.SlateGray);GL.LineWidth(1);GL.Begin(BeginMode.Lines);for(int i=0;i<=12;i++){float t=i/12f;Vertex(a+(b-a)*t);Vertex(d+(c-d)*t);Vertex(a+(d-a)*t);Vertex(b+(c-b)*t);}GL.End();
                if(Options.Scene==2){GL.Color3(Color.Goldenrod);GL.LineWidth(3);GL.Begin(BeginMode.Lines);Vertex(a);Vertex(b);GL.End();}
            }
            finally{GL.PopAttrib();}
        }
    }
}
