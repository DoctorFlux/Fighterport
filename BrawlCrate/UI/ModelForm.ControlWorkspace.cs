using BrawlCrate.Studio;
using BrawlLib.Internal;
using BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CoordinateType=BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase.ModelEditorBase.CoordinateType;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private TreeView _controlTree;
        private bool _controlTreeSync;
        private string _controlTreeSignature;
        private Label _controlSelectionLabel;
        private void ShowAnimationControls()
        {
            _controlPickFK.Checked=true;
            if(_controlsWindow!=null&&!_controlsWindow.IsDisposed){_controlsWindow.Show();return;}
            var window=new Form{Text="Animation Controls",ClientSize=new Size(440,610),MinimumSize=new Size(360,380),ShowInTaskbar=false};
            var tools=new ToolStrip{Dock=DockStyle.Top,GripStyle=ToolStripGripStyle.Hidden};
            foreach(var mode in new[]{TransformType.Translation,TransformType.Rotation,TransformType.Scale})
            {
                var chosen=mode;tools.Items.Add(mode==TransformType.Translation?"Move":mode==TransformType.Rotation?"Rotate":"Scale",null,(s,e)=>modelEditControl1.ControlType=chosen);
            }
            tools.Items.Add("Curves",null,(s,e)=>modelEditControl1.ShowControlInterpolation());
            var coordinate=new ToolStripDropDownButton("Coordinates");
            foreach(var type in new[]{CoordinateType.Local,CoordinateType.World,CoordinateType.Screen}){var chosen=type;coordinate.DropDownItems.Add(type.ToString(),null,(s,e)=>{for(int i=0;i<3;i++)modelEditControl1._coordinateTypes[i]=chosen;modelEditControl1.ModelPanel.Invalidate();});}tools.Items.Add(coordinate);
            _controlTree=new TreeView{Dock=DockStyle.Fill,HideSelection=false,Name="animationControlTree"};
            _controlTree.AfterSelect+=(s,e)=>{if(!_controlTreeSync&&e.Node.Tag is ControlPin handle&&!_controlBusy&&!_retargetBusy)modelEditControl1.SelectControlHandle(handle);};
            var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new Padding(6),WrapContents=true};
            var add=new Button{Text="Pin Control / Bone",AutoSize=true};add.Click+=(s,e)=>ControlAction(()=>AttachControlPin(false));actions.Controls.Add(add);
            var soft=new Button{Text="Soft Pin Hands and Feet",AutoSize=true};soft.Click+=(s,e)=>ControlAction(()=>AttachControlPin(true));actions.Controls.Add(soft);
            var remove=new Button{Text="Remove Pin",AutoSize=true};remove.Click+=(s,e)=>ControlAction(RemoveControlPin);actions.Controls.Add(remove);
            var follow=new CheckBox{Text="New pins follow existing animation",Checked=_controlFollow.Checked,AutoSize=true};follow.CheckedChanged+=(s,e)=>_controlFollow.Checked=follow.Checked;actions.Controls.Add(follow);
            _controlSelectionLabel=new Label{Dock=DockStyle.Bottom,Height=55,Padding=new Padding(8),Text="Select an FK control, IK control or pin.\nUse the viewer's move, rotate and scale tools. Escape cancels."};
            window.Controls.Add(_controlTree);window.Controls.Add(_controlSelectionLabel);window.Controls.Add(actions);window.Controls.Add(tools);_controlTree.BringToFront();
            _controlsWindow=window;_controlTreeSignature=null;RefreshControlTree();DarkTheme.Apply(window);window.Show(this);
        }
        private void RefreshControlTree()
        {
            if(_controlTree==null||_controlTree.IsDisposed)return;
            var all=modelEditControl1.AnimationHandles.Concat(modelEditControl1.AnimationPins).ToArray();
            string signature=string.Join("|",all.Select(p=>p.Id+":"+p.BoneName));
            if(signature==_controlTreeSignature){foreach(var node in ControlNodes(_controlTree.Nodes)){if(node.Tag is ControlPin p)node.Tag=all.FirstOrDefault(a=>a.Id==p.Id);}SyncControlSelection();return;}
            _controlTreeSignature=signature;_controlTreeSync=true;_controlTree.BeginUpdate();
            try
            {
                _controlTree.Nodes.Clear();var fk=_controlTree.Nodes.Add("FK controls");var ik=_controlTree.Nodes.Add("IK controls");var pins=_controlTree.Nodes.Add("Pins");var poles=_controlTree.Nodes.Add("Pole controls");
                var handles=all.Where(p=>p.Id.StartsWith("fk:")).ToDictionary(p=>p.BoneName,p=>new TreeNode(p.Name){Tag=p});
                var model=modelEditControl1.TargetModel as MDL0Node;
                foreach(var handle in handles)
                {
                    string parent=model?._linker.BoneCache.Single(b=>b.Name==handle.Key).Parent?.Name;
                    if(parent!=null&&handles.TryGetValue(parent,out var parentNode))parentNode.Nodes.Add(handle.Value);else fk.Nodes.Add(handle.Value);
                }
                foreach(var p in all.Where(p=>p.Id.StartsWith("ik:")))ik.Nodes.Add(new TreeNode(p.Name+" · "+p.BoneName){Tag=p});
                foreach(var p in modelEditControl1.AnimationPins)pins.Nodes.Add(new TreeNode(p.ToString()){Tag=p});
                foreach(var p in all.Where(p=>p.Id.StartsWith("pole:")))poles.Nodes.Add(new TreeNode(p.Name+" · bend direction"){Tag=p});fk.Expand();ik.Expand();pins.Expand();poles.Expand();
            }
            finally{_controlTree.EndUpdate();_controlTreeSync=false;}
            SyncControlSelection();
        }
        private static System.Collections.Generic.IEnumerable<TreeNode> ControlNodes(TreeNodeCollection nodes)
        {
            foreach(TreeNode node in nodes){yield return node;foreach(var child in ControlNodes(node.Nodes))yield return child;}
        }
        private void SyncControlSelection()
        {
            if(_controlTree==null||_controlTree.IsDisposed)return;_controlTreeSync=true;
            try
            {
                var selected=ControlNodes(_controlTree.Nodes).FirstOrDefault(n=>(n.Tag as ControlPin)?.Id==modelEditControl1.SelectedAnimationPin&&n.Tag!=null);
                _controlTree.SelectedNode=selected;if(selected!=null){selected.EnsureVisible();_controlSelectionLabel.Text=selected.Text+"\n"+((selected.Tag as ControlPin).Id.StartsWith("fk:")?"Local transform fields · native bone channels":(selected.Tag as ControlPin).Id.StartsWith("pole:")?"Pole position · Move · bend saved to native bone channels":"World position/rotation · local scale · native bone channels");}
            }
            finally{_controlTreeSync=false;}
        }
    }
}
