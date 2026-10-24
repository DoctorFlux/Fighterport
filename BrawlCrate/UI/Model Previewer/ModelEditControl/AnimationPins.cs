using BrawlCrate.Studio;
using BrawlLib.Internal;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using OpenTK.Graphics.OpenGL;
using System;
using System.Linq;
using System.Windows.Forms;
using N=System.Numerics;

namespace BrawlCrate.UI.Model_Previewer.ModelEditControl
{
    public partial class ModelEditControl
    {
        internal ControlPin[] AnimationPins=new ControlPin[0];
        internal ControlPin[] AnimationHandles=new ControlPin[0];
        private string _selectedAnimationPin;
        internal string SelectedAnimationPin
        {
            get=>_selectedAnimationPin;
            set{_selectedAnimationPin=value;_pinSelection.ResetAll();RefreshControlCurveTarget();AnimationHandleSelectionChanged?.Invoke();ModelPanel?.Invalidate();}
        }
        internal bool PickAnimationPinsOnly;
        internal bool PickAnimationFKControls;
        internal int AnimationPinRotationAxis=1; // Legacy project/test compatibility; native gizmos choose axes.
        internal Action<ControlPin> AnimationPinDown;
        internal Action<float[]> AnimationPinMove;
        internal Action AnimationPinUp,AnimationPinCancel,AnimationHandleSelectionChanged;
        internal Func<bool> AnimationControlsBusy;
        internal float[][] AnimationAuthoringPose;
        internal string[] AnimationAuthoringBones;
        private readonly SelectionParams _pinSelection=new SelectionParams();
        private ControlPin _dragPin;
        private FrameState _pinFrame;
        private TransformType _pinMode;
        private ModelPanelViewport _pinViewport;
        internal ControlPin SelectedControlHandle=>AnimationPins.Concat(AnimationHandles).FirstOrDefault(p=>p.Id==SelectedAnimationPin);
        private string CurveBoneName(ControlPin handle)=>handle.Id.StartsWith("pole:")?handle.MiddleName:handle.BoneName;
        public override CHR0EntryNode AnimationChannelTarget=>SelectedControlHandle is ControlPin handle?SelectedCHR0?.FindChild(CurveBoneName(handle),false) as CHR0EntryNode:base.AnimationChannelTarget;
        public override FrameState? SelectedControlTransform=>SelectedControlHandle is ControlPin handle&&PinBone(handle)!=null?(handle.Id.StartsWith("fk:")?PinBone(handle).FrameState:HandleFrame(handle)):(FrameState?)null;
        internal void SelectControlHandle(ControlPin pin)
        {
            if(pin?.Id.StartsWith("pole:")==true)ControlType=TransformType.Translation;
            SelectedBone=null;ClearSelectedVertices();SelectedAnimationPin=pin?.Id;if(pin!=null)BonesPanel?.SetSelectedBone(PinBone(pin));UpdatePropDisplay();
        }
        internal void RefreshControlCurveTarget()
        {
            var pin=SelectedControlHandle;
            if(pin==null)return;
            var entry=SelectedCHR0?.FindChild(CurveBoneName(pin),false) as CHR0EntryNode;
            if(KeyframePanel!=null)KeyframePanel.TargetSequence=entry;
            if(_interpolationEditor!=null&&_interpolationEditor._targetNode!=entry)_interpolationEditor.SetTarget(entry);
            if(_interpolationForm!=null&&_interpolationForm._interpolationEditor._targetNode!=entry)_interpolationForm._interpolationEditor.SetTarget(entry);
        }
        internal void RebindSavedCurveTargets(CHR0Node clip, CHR0EntryNode[] previous)
        {
            foreach (var editor in new[] { _interpolationEditor, _interpolationForm?._interpolationEditor })
                if (editor?._targetNode is CHR0EntryNode entry && previous.Contains(entry))
                    editor.SetTarget(clip.Children.OfType<CHR0EntryNode>().Single(n => n.Name == entry.Name));
            if (KeyframePanel?.TargetSequence is CHR0EntryNode target && previous.Contains(target))
                KeyframePanel.TargetSequence = clip.Children.OfType<CHR0EntryNode>().Single(n => n.Name == target.Name);
        }
        internal void ShowControlInterpolation()
        {
            if(_interpolationForm==null)interpolationEditorToolStripMenuItem_Click(this,EventArgs.Empty);
            RefreshControlCurveTarget();if(_interpolationForm!=null){_interpolationForm.Owner=FindForm();_interpolationForm.TopMost=false;DarkTheme.Apply(_interpolationForm);}
        }
        public override void PlayAnim()
        {
            if(AnimationControlsBusy?.Invoke()==true){CancelAnimationPin();return;}
            base.PlayAnim();
        }
        public override void UpdateModel(float frame)
        {
            base.UpdateModel(frame);
            if(SelectedCHR0==null&&TargetModel is MDL0Node model&&AnimationAuthoringPose!=null&&model._linker.BoneCache.Length==AnimationAuthoringPose.Length)
            {
                for(int i=0;i<AnimationAuthoringPose.Length;i++){var p=AnimationAuthoringPose[i];model._linker.BoneCache.Single(b=>b.Name==AnimationAuthoringBones[i]).FrameState=new FrameState(new Vector3(p[0],p[1],p[2]),new Vector3(p[3],p[4],p[5]),new Vector3(p[6],p[7],p[8]));}
                foreach(var root in model.BoneList.OfType<MDL0BoneNode>())root.RecalcFrameState();model.WeightMeshes();
            }
        }
        private MDL0BoneNode PinBone(ControlPin pin)=>(TargetModel as MDL0Node)?._linker.BoneCache.SingleOrDefault(b=>b.Name==pin.BoneName);
        private Vector3 PinPosition(ControlPin pin)
        {
            if(!pin.Id.StartsWith("pole:"))return PinBone(pin)?.Matrix.GetPoint()??Vector3.Zero;
            var bones=((MDL0Node)TargetModel)._linker.BoneCache;
            var root=bones.Single(joint=>joint.Name==pin.RootName);var middle=bones.Single(joint=>joint.Name==pin.MiddleName);
            N.Vector3 V(Vector3 v)=>new N.Vector3(v._x,v._y,v._z);
            var a=V(root.Matrix.GetPoint());var b=V(middle.Matrix.GetPoint());var c=V(PinBone(pin).Matrix.GetPoint());
            var axis=c-a;float length=N.Vector3.Distance(a,b)+N.Vector3.Distance(b,c);
            var fallback=new Vector3(pin.Pole[0],pin.Pole[1],pin.Pole[2]);
            if(pin.PoleParent!=null){fallback=new Vector3(pin.PoleParent[0],pin.PoleParent[1],pin.PoleParent[2]);if(root.Parent is MDL0BoneNode parent)fallback=parent.Matrix*fallback-parent.Matrix.GetPoint();}
            var bend=V(fallback);
            if(axis.LengthSquared()>1e-10f){axis=N.Vector3.Normalize(axis);var current=b-a-axis*N.Vector3.Dot(b-a,axis);if(current.Length()>length*.001f)bend=current;bend-=axis*N.Vector3.Dot(bend,axis);if(bend.LengthSquared()<1e-10f)bend=N.Vector3.Cross(axis,Math.Abs(axis.Y)<.9f?N.Vector3.UnitY:N.Vector3.UnitX);}
            var p=a+N.Vector3.Normalize(bend)*length;return new Vector3(p.X,p.Y,p.Z);
        }
        private System.Collections.Generic.IEnumerable<ControlPin> VisibleHandles()=>AnimationPins.Concat(AnimationHandles.Where(p=>p.Id==SelectedAnimationPin||PickAnimationFKControls&&p.Id.StartsWith("pole:")));
        private Vector3 PinScreen(ControlPin pin,ModelPanelViewport viewport)
        {
            var p=viewport.Camera.Project(PinPosition(pin));p._x+=viewport.Region.X;p._y=ModelPanel.Height-viewport.Region.Bottom+p._y;return p;
        }
        private FrameState HandleFrame(ControlPin pin)
        {
            if(pin.Id.StartsWith("pole:"))return new FrameState(Vector3.One,Vector3.Zero,PinPosition(pin));
            var bone=PinBone(pin);var m=bone.Matrix;
            // Same orthonormal world orientation as the engine; never alter native binds.
            var x=N.Vector3.Normalize(new N.Vector3(m[0],m[1],m[2]));var y=new N.Vector3(m[4],m[5],m[6]);y=N.Vector3.Normalize(y-x*N.Vector3.Dot(x,y));var z=N.Vector3.Normalize(N.Vector3.Cross(x,y));
            var rotation=Matrix.Identity;rotation[0]=x.X;rotation[1]=x.Y;rotation[2]=x.Z;rotation[4]=y.X;rotation[5]=y.Y;rotation[6]=y.Z;rotation[8]=z.X;rotation[9]=z.Y;rotation[10]=z.Z;
            return new FrameState(bone.FrameState._scale,rotation.GetAngles(),m.GetPoint());
        }
        private Matrix HandleMatrix(FrameState frame)=>Matrix.TransformMatrix(Vector3.One,frame._rotate,frame._translate);
        private void HighlightControlHandle(MouseEventArgs e,ModelPanelViewport viewport)
        {
            _pinSelection.ResetHighlights();var selected=SelectedControlHandle;if(selected==null||PinBone(selected)==null||ControlType==TransformType.None)return;
            var m=HandleMatrix(HandleFrame(selected));MouseMoveTarget(ModelPanel,e,0,viewport,m,m.Invert(),_pinSelection);
        }
        protected override void modelPanel1_MouseDown(object sender,MouseEventArgs e)
        {
            if(e.Button==MouseButtons.Left&&!Playing&&TargetModel is MDL0Node&&ModelPanel.CurrentViewport!=null)
            {
                if(AnimationControlsBusy?.Invoke()==true)return;
                var viewport=ModelPanel.CurrentViewport;HighlightControlHandle(e,viewport);
                var selected=SelectedControlHandle;
                bool gizmo=selected!=null&&_pinSelection.HighlightingAny();
                var hit=VisibleHandles().Select(p=>new{Pin=p,Screen=PinScreen(p,viewport)}).Where(p=>p.Screen._z>=0&&p.Screen._z<=1).OrderBy(p=>Math.Pow(p.Screen._x-e.X,2)+Math.Pow(p.Screen._y-e.Y,2)).FirstOrDefault();
                if(!gizmo&&hit!=null&&Math.Pow(hit.Screen._x-e.X,2)+Math.Pow(hit.Screen._y-e.Y,2)<=144)
                {
                    SelectControlHandle(hit.Pin);selected=hit.Pin;
                    // Selecting the diamond is independent of selecting a bone. A
                    // move may start in its view plane; rotation/scale use the rings/axes.
                    if(ControlType==TransformType.Translation){_pinSelection._hiCirc=true;gizmo=true;}
                    else return;
                }
                if(gizmo)
                {
                    if(SelectedCHR0!=null&&CurrentFrame==0)SetFrame(1);
                    _pinFrame=HandleFrame(selected);var m=HandleMatrix(_pinFrame);_pinSelection.ApplySnaps();
                    if(GetTransformPoint(new Vector2(e.X,e.Y),out var point,viewport,m,_pinSelection))
                    {
                        _dragPin=selected;_pinViewport=viewport;_pinMode=ControlType;_pinSelection.Update(ControlType,point,m.Invert()*point,_pinFrame);
                        viewport.AllowSelection=false;((Control)ModelPanel).Capture=true;AnimationPinDown?.Invoke(selected);ModelPanel.Invalidate();
                    }
                    return;
                }
                if(PickAnimationPinsOnly)return;SelectedAnimationPin=null;
            }
            base.modelPanel1_MouseDown(sender,e);
        }
        internal bool MoveAnimationPin(MouseEventArgs e)
        {
            if(_dragPin==null)
            {
                if(SelectedControlHandle!=null&&!Playing&&ModelPanel.CurrentViewport!=null){HighlightControlHandle(e,ModelPanel.CurrentViewport);if(_pinSelection.HighlightingAny())return true;}
                return false;
            }
            if(Playing||e.Button!=MouseButtons.Left||ControlType!=_pinMode){CancelAnimationPin();return true;}
            var matrix=HandleMatrix(_pinFrame);
            var delta=GetLocalTransform(e,_pinViewport,matrix,matrix.Invert(),matrix,_pinSelection,out var point);
            if(point==null)return true;
            if(_pinMode==TransformType.Rotation)_pinFrame._rotate+=delta;
            else if(_pinMode==TransformType.Scale)
            {
                for(int i=0;i<3;i++)if(float.IsNaN(delta[i])||float.IsInfinity(delta[i])||delta[i]<=0)return true;
                _pinFrame._scale*=delta;
            }
            else _pinFrame._translate+=delta;
            _pinFrame.CalcTransforms();_pinSelection._lastPointLocal=HandleMatrix(_pinFrame).Invert()*point.Value;
            AnimationPinMove?.Invoke(ControlHandleGoal(_pinFrame));return true;
        }
        internal static float[] ControlHandleGoal(FrameState frame)
        {
            var rot=Matrix.RotationMatrix(frame._rotate);
            var q=N.Quaternion.Normalize(N.Quaternion.CreateFromRotationMatrix(new N.Matrix4x4(rot[0],rot[1],rot[2],0,rot[4],rot[5],rot[6],0,rot[8],rot[9],rot[10],0,0,0,0,1)));
            var p=frame._translate;var s=frame._scale;
            return new[]{p._x,p._y,p._z,q.X,q.Y,q.Z,q.W,s._x,s._y,s._z};
        }
        internal bool EndAnimationPin(MouseEventArgs e)
        {
            if(_dragPin==null||e.Button!=MouseButtons.Left)return false;_dragPin=null;_pinSelection.ResetAll();_pinViewport.AllowSelection=true;((Control)ModelPanel).Capture=false;AnimationPinUp?.Invoke();return true;
        }
        internal void CancelAnimationPin(){_dragPin=null;_pinSelection.ResetAll();if(_pinViewport!=null)_pinViewport.AllowSelection=true;((Control)ModelPanel).Capture=false;AnimationPinCancel?.Invoke();}
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
        {
            if(keyData==Keys.Escape&&_dragPin!=null){CancelAnimationPin();return true;}
            return base.ProcessCmdKey(ref msg,keyData);
        }
        internal void RenderAnimationPins(ModelPanelViewport viewport)
        {
            if(TargetModel==null)return;
            GL.PushAttrib(AttribMask.AllAttribBits);
            try
            {
                GL.Disable(EnableCap.Lighting);GL.Disable(EnableCap.DepthTest);GL.LineWidth(2);GL.Begin(BeginMode.Lines);
                foreach(var pin in VisibleHandles().Where(p=>PinBone(p)!=null))
                {
                    var p=PinPosition(pin);var projected=viewport.Camera.Project(p);float size=(viewport.Camera.UnProject(projected._x+7,projected._y,projected._z)-p).TrueDistance();
                    GL.Color3(pin.Id==SelectedAnimationPin?System.Drawing.Color.White:pin.Id.StartsWith("pole:")?System.Drawing.Color.Orchid:pin.Soft?System.Drawing.Color.Goldenrod:System.Drawing.Color.DeepSkyBlue);
                    if(pin.Id.StartsWith("pole:"))Line(((MDL0BoneNode)PinBone(pin).Parent).Matrix.GetPoint(),p);
                    var a=new Vector3(size,0,0);var b=new Vector3(0,size,0);var c=new Vector3(0,0,size);
                    Line(p+a,p+b);Line(p+b,p-a);Line(p-a,p-b);Line(p-b,p+a);Line(p+a,p+c);Line(p+c,p-a);Line(p-a,p-c);Line(p-c,p+a);
                }
                GL.End();
                var selected=SelectedControlHandle;
                if(selected!=null&&PinBone(selected)!=null&&!Playing&&ControlType!=TransformType.None)
                {
                    var frame=_dragPin==null?HandleFrame(selected):_pinFrame;var rotation=Matrix.Identity;
                    switch(_coordinateTypes[(int)ControlType]){case CoordinateType.Local:rotation=Matrix.RotationMatrix(frame._rotate);break;case CoordinateType.Screen:rotation=Matrix.RotationMatrix(viewport.Camera._rotation);break;}
                    RenderTransformControl(frame._translate,rotation,OrbRadius(frame._translate,viewport.Camera),viewport,_pinSelection);
                }
            }
            finally{GL.PopAttrib();}
        }
        private static void Line(Vector3 a,Vector3 b){GL.Vertex3(a._x,a._y,a._z);GL.Vertex3(b._x,b._y,b._z);}
    }
}
