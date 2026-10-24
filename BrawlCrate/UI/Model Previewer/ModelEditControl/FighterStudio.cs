using BrawlLib.Internal;
using BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI.Model_Previewer.ModelEditControl
{
    public partial class ModelEditControl
    {
        internal Action<string, bool, bool, bool> FighterOpenRequested;
        internal Func<bool> FighterSaveRequested;
        internal Action<ResourceNode> FighterResourcesRequested;
        internal Func<ResourceNode, string> FighterResourceLabel;
        internal Func<ResourceNode, bool, bool> FighterFileSaveRequested;
        internal Predicate<ResourceNode> FighterOwnsFile;
        internal Action<ResourceNode, string> FighterNativeExportRequested;
        internal Predicate<NW4RAnimationNode> FighterAllowsCompanionChannels;
        internal Func<ResourceNode, string> FighterAnimationLabel;
        internal Func<MDL0Node,Studio.Retargeting.BrawlRig> FighterCaptureRig;
        internal Action FighterUndoStateChanged;
        internal bool FighterGroundLevelView => _fighterGroundView && _fighterGroundAngle==1;
        private bool _fighterFrameQueued;
        private bool _fighterGroundView;
        private int _fighterGroundAngle;

        protected override NW4RAnimationNode FindCorrespondingAnimation(NW4RAnimationNode focusFile, NW4RAnimType targetType)
        {
            if (FighterAllowsCompanionChannels != null && !FighterAllowsCompanionChannels(focusFile)) return null;
            return base.FindCorrespondingAnimation(focusFile, targetType);
        }

        public override bool CanCloseExternalFile(ResourceNode node)
        {
            return FighterOwnsFile == null || !FighterOwnsFile(node);
        }

        public override bool TrySaveExternalFile(ResourceNode node, bool saveAs, out bool result)
        {
            if (FighterFileSaveRequested != null && FighterOwnsFile != null && FighterOwnsFile(node))
            {
                result = FighterFileSaveRequested(node, saveAs);
                return true;
            }

            return base.TrySaveExternalFile(node, saveAs, out result);
        }

        public override void OpenFile(string file, bool models, bool animations = true, bool etc = true)
        {
            if (FighterOpenRequested != null)
            {
                FighterOpenRequested(file, models, animations, etc);
                return;
            }

            base.OpenFile(file, models, animations, etc);
        }

        internal void ConfigureFighterView()
        {
            _fighterGroundView=false;
            FloorHeight=0;
            FilledFloor=false;
            foreach(var viewport in ModelPanel)viewport.BackgroundColor=System.Drawing.Color.FromArgb(36,39,46);
            showLeft.Checked = true;
            showRight.Checked = true;
            showTop.Checked = true;
            showBottom.Checked = true;
            animEditors.Visible = true;
            leftPanel.Visible = true;
            controlPanel.Visible = true;
            chkExternalAnims.Checked = true;
            RenderBones = true;
            leftPanel.listAnims.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            leftPanel.listAnims.Columns[0].Text = "Animation";
            leftPanel.listAnims.Columns[0].Width = 170;
            if (leftPanel.listAnims.Columns.Count == 1)
            {
                leftPanel.listAnims.Columns.Add("Frames", 50);
                leftPanel.listAnims.Columns.Add("Loop", 42);
            }

            CheckDimensions();
        }

        internal void FrameFighterModel()
        {
            if (TargetModel == null)
            {
                return;
            }

            UpdateModel();
            Box box = TargetModel.GetBox();
            Vector3 margin = (box.Max - box.Min) * 0.2f;
            if (float.IsNaN(margin._x) || float.IsInfinity(margin._x) ||
                float.IsNaN(margin._y) || float.IsInfinity(margin._y))
            {
                return;
            }

            var viewport = ModelPanel.CurrentViewport;
            var camera = viewport.Camera;
            Vector3 min = box.Min - margin;
            Vector3 max = box.Max + margin;
            if (camera.Orthographic)
            {
                ModelPanel.SetCamWithBox(min, max);
            }
            else
            {
                // Native camera defaults may contain an angled view. Fit in world coordinates,
                // without composing local translations with those saved defaults.
                Vector3 center = (min + max) * 0.5f;
                Vector3 half = (max - min) * 0.5f;
                float aspect = camera.Width / Math.Max(camera.Height, 1);
                float tangent = (float) Math.Tan(camera.VerticalFieldOfView * Math.PI / 360.0);
                if (aspect <= 0 || tangent <= 0)
                {
                    return;
                }

                float distance = Math.Max(half._y / tangent, half._x / (tangent * aspect));
                Vector3 eye = new Vector3(center._x, center._y, max._z + distance + Math.Max(1, camera.NearDepth * 2));
                if(_fighterGroundView)
                {
                    Vector3 span=box.Max-box.Min;
                    float yaw=span._y<Math.Max(span._x,span._z)*0.65f?(span._z>span._x?65:25):30;
                    float sign=-1;
                    if(TargetModel is MDL0Node body)
                    {
                        var contacts=(body.PolygonList??new System.Collections.Generic.List<ResourceNode>()).OfType<MDL0ObjectNode>().Where(o=>o.Vertices!=null).SelectMany(o=>o.Vertices).Where(v=>v!=null).Select(v=>v.WeightedPosition)
                            .Where(p=>Math.Abs(p._y)<Math.Max(0.05f,span._y*0.045f)).ToArray();
                        if(contacts.Length>0){float side=contacts.Average(p=>p._x)-center._x;if(Math.Abs(side)>span._x*0.05f)sign=Math.Sign(side);}
                    }
                    Vector3 rotation=new Vector3(-14,yaw*sign,0);
                    if(_fighterGroundAngle==1)
                    {
                        // Ground-level inspection fits the feet and knees, not
                        // the whole character. Keep the eye close to the sole
                        // plane so small gaps and toe/heel roll stay visible.
                        rotation=new Vector3(0,90*sign,0);
                        if(TargetModel is MDL0Node contactModel)
                        try
                        {
                            var rig=FighterCaptureRig?.Invoke(contactModel)??Studio.Retargeting.BrawlRetargeter.Capture(contactModel);
                            float leg=new[]{"left","right"}.Average(side=>
                                System.Numerics.Vector3.Distance(rig.Rig.Rest.Point(side+".upperLeg"),rig.Rig.Rest.Point(side+".lowerLeg"))+
                                System.Numerics.Vector3.Distance(rig.Rig.Rest.Point(side+".lowerLeg"),rig.Rig.Rest.Point(side+".foot")));
                            var points=new[]{"left","right"}.SelectMany(side=>new[]{side+".foot",side+".toe",side+".lowerLeg"})
                                .Where(rig.Rig.Roles.ContainsKey).Select(role=>rig.Bones[rig.Rig.Roles[role]].Matrix.GetPoint()).ToArray();
                            min=new Vector3(points.Min(p=>p._x)-leg*0.15f,FloorHeight-leg*0.08f,points.Min(p=>p._z)-leg*0.15f);
                            max=new Vector3(points.Max(p=>p._x)+leg*0.15f,Math.Max(FloorHeight+leg*0.4f,points.Max(p=>p._y))+leg*0.06f,points.Max(p=>p._z)+leg*0.15f);
                            center=(min+max)*0.5f;
                            center._y=FloorHeight+leg*0.08f;
                        }
                        catch(NotSupportedException){}
                        catch(System.IO.InvalidDataException){}
                    }
                    else if(_fighterGroundAngle==2)rotation=new Vector3(-10,-yaw*sign,0);
                    Matrix inverse=Matrix.ReverseTransformMatrix(new Vector3(1),rotation,new Vector3());
                    distance=1;
                    foreach(float x in new[]{min._x,max._x})foreach(float y in new[]{min._y,max._y})foreach(float z in new[]{min._z,max._z})
                    {
                        Vector3 corner=inverse*(new Vector3(x,y,z)-center);
                        distance=Math.Max(distance,corner._z+Math.Max(Math.Abs(corner._y)/tangent,Math.Abs(corner._x)/(tangent*aspect)));
                    }
                    distance+=Math.Max(1,camera.NearDepth*2);
                    eye=center+Matrix.RotationMatrix(rotation)*new Vector3(0,0,distance);
                    camera.Set(eye,rotation,new Vector3(1));
                }
                else camera.Set(eye, new Vector3(), new Vector3(1));
                viewport._viewDistance = _fighterGroundView?distance:eye._z-center._z;
                ModelPanel.Invalidate();
            }
        }

        internal void QueueFrameFighterModel()
        {
            FrameFighterModel();
            if (!_fighterFrameQueued)
            {
                _fighterFrameQueued = true;
                ModelPanel.PostRender += FrameFighterAfterRender;
            }

            ModelPanel.Invalidate();
        }

        internal void QueueGroundContactView(int angle=0)
        {
            _fighterGroundView=true;
            _fighterGroundAngle=angle;
            FloorHeight=0;
            if(TargetModel is MDL0Node model)
            {
                try{FloorHeight=(FighterCaptureRig?.Invoke(model)??Studio.Retargeting.BrawlRetargeter.Capture(model)).Rig.GroundHeight;}
                catch(NotSupportedException){}
                catch(System.IO.InvalidDataException){_fighterGroundView=false;}
            }
            RenderFloor=true;
            FilledFloor=true;
            ModelPanel.Camera.Orthographic=false;
            QueueFrameFighterModel();
        }

        private void FrameFighterAfterRender(BrawlLib.Internal.Windows.Controls.Model_Panel.ModelPanelViewport viewport)
        {
            ModelPanel.PostRender -= FrameFighterAfterRender;
            _fighterFrameQueued = false;
            if (!IsDisposed && IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed)
                    {
                        FrameFighterModel();
                    }
                }));
            }
        }
    }
}
