using BrawlCrate.Studio;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.Internal.Windows.Controls.ModelViewer.Panels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        // Independent native BT3 motion editor. It owns no CHR0/BRRES bridge;
        // the existing FighterProject remains intact underneath this view.
        private TableLayoutPanel _bt3TargetHost;
        private TableLayoutPanel _bt3TargetBody;
        private ModelPanel _bt3TargetViewport;
        private Control _bt3OriginalViewportParent;
        private BrawlLib.OpenGL.IRenderedObject[] _bt3OriginalRenderTargets;
        private Comparison<BrawlLib.OpenGL.DrawCallBase> _bt3OriginalDrawCallSort;
        private bool _bt3OriginalLeftVisible,_bt3OriginalRightVisible;
        private Control _bt3OriginalAnimationEditors;
        private bool _bt3OriginalAnimationEditorsVisible,_bt3OriginalSelection,_bt3OriginalFloor;
        private Color _bt3OriginalBackground;
        private bool _bt3OriginalModelPickerVisible,_bt3OriginalModelMenuVisible;
        private ToolStripItem _bt3ModelMenuItem;
        private ToolStripItem[] _bt3NativeTransformTools;
        private ToolStripButton _bt3BonesToggle,_bt3PolygonsToggle,_bt3VerticesToggle;
        private bool[] _bt3NativeTransformEnabled;
        private BrawlLib.OpenGL.GLCamera _bt3SavedCamera;
        private BrawlLib.Internal.Matrix _bt3SavedCameraMatrix,_bt3SavedCameraInverse;
        private BrawlLib.Internal.Vector3 _bt3SavedCameraRotation,_bt3SavedCameraScale;
        private ComboBox _bt3TargetForm, _bt3TargetCostume;
        private ListBox _bt3TargetAction;
        private Control _bt3NativeLeftPane,_bt3NativeRightPane;
        private Control _bt3NativeTransformPanel;
        private ComboBox _bt3RightTab;
        private int _bt3OriginalAnimEditorsHeight,_bt3OriginalAnimControlWidth;
        private Dictionary<Control,bool> _bt3OriginalAnimControlsVisible;
        private bool _bt3OriginalKeyframePanelEnabled;
        private int _bt3OriginalRightTab;
        private int _bt3SelectedBoneId=-1;
        private Label _bt3SelectedBoneLabel;
        private int _bt3OriginalLeftWidth,_bt3OriginalRightWidth;
        private BrawlLib.Modeling.IBoneNode _bt3OriginalSelectedBone;
        private BrawlLib.SSBB.ResourceNodes.NW4RAnimationNode _bt3OriginalAnimation;
        private int _bt3OriginalFrame;
        private ModelPlaybackPanel _bt3TargetPlayback;
        private Control _bt3OriginalPlaybackParent;
        private DockStyle _bt3OriginalPlaybackDock;
        private bool _bt3OriginalPlaybackEnabled,_bt3OriginalPreviewLoop;
        private bool[] _bt3OriginalPlaybackButtonsEnabled;
        private decimal _bt3OriginalPlaybackFrame,_bt3OriginalPlaybackTotal,_bt3OriginalPlaybackFps;
        private Label _bt3TargetCaption;
        private bool _bt3PreviewLoop;
        private System.Windows.Forms.Timer _bt3TargetTimer;
        private readonly Stopwatch _bt3TargetClock=new Stopwatch();
        private Bt3BrowseChoice _bt3TargetChoice;
        private Bt3NativeRender _bt3TargetRenderer;
        private Bt3NativeAssetReader.Motion _bt3TargetMotion;
        private Bt3NativeMotionEditor _bt3TargetEditor;
        private readonly Dictionary<string,Bt3NativeMotionEditor> _bt3TargetEdits=new Dictionary<string,Bt3NativeMotionEditor>(StringComparer.Ordinal);
        private NumericUpDown[] _bt3EditTranslation,_bt3EditRotation;
        private Button _bt3EditApply,_bt3EditUndo,_bt3EditRedo,_bt3EditExport;
        private Button _bt3EditImport;
        private ToolStrip _bt3HiddenProjectToolbar;
        private CancellationTokenSource _bt3TargetCancel;
        private double _bt3TargetTime;
        private int _bt3TargetEpoch;
        private bool _bt3TargetUpdating;
        private sealed class Bt3ActionOption
        {
            internal int Action;
            public override string ToString()=>Bt3ActionLabels.Describe(Action);
        }

        private sealed class Bt3TargetTransport : INativeModelTransport
        {
            private readonly ModelForm _owner;
            internal Bt3TargetTransport(ModelForm owner){_owner=owner;}
            public void TogglePlay(){_owner.ToggleBt3TargetPlayback();}
            public void SeekDisplayFrame(int frame){_owner.SeekBt3TargetDisplayFrame(frame);}
            public void SetPreviewLoop(bool loop){_owner._bt3PreviewLoop=loop;}
        }

        private static ComboBox Bt3TargetCombo(string name,int width)
            =>new ComboBox{Name=name,Width=width,DropDownStyle=ComboBoxStyle.DropDownList,Margin=new Padding(3)};

        private void BuildBt3TargetHost()
        {
            if(_bt3TargetHost!=null)return;
            _bt3TargetHost=new TableLayoutPanel{Name="bt3NativeTargetView",Dock=DockStyle.Fill,
                ColumnCount=1,RowCount=2,Visible=false,BackColor=Color.FromArgb(29,32,38)};
            _bt3TargetHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            _bt3TargetHost.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
            _bt3TargetHost.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var top=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(6,3,3,0)};
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));
            _bt3TargetCaption=new Label{Name="bt3TargetCaption",Text="Native BT3 target animation",
                AutoSize=false,Dock=DockStyle.Fill,AutoEllipsis=true,ForeColor=Color.White,
                TextAlign=ContentAlignment.MiddleLeft};
            top.Controls.Add(_bt3TargetCaption,0,0);
            var back=new Button{Name="bt3TargetBack",Text="Return to project",Dock=DockStyle.Fill};
            back.Click+=(s,e)=>CloseBt3TargetView();top.Controls.Add(back,1,0);
            _bt3TargetHost.Controls.Add(top,0,0);
            _bt3TargetViewport=modelEditControl1.ModelPanel;
            _bt3TargetViewport.MouseDown+=(s,e)=>{
                if(e.Button!=MouseButtons.Left||_bt3TargetHost?.Visible!=true||_bt3TargetRenderer==null)return;
                int id=_bt3TargetRenderer.PickBone(_bt3TargetViewport.CurrentViewport,e.Location);
                if(id>=0)
                {
                    modelEditControl1.rightPanel.pnlBones.SelectNativeBone(id);
                    if(_bt3RightTab!=null)_bt3RightTab.SelectedIndex=0;
                }
            };
            _bt3TargetBody=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=1,Margin=Padding.Empty};
            _bt3TargetBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            _bt3TargetHost.Controls.Add(_bt3TargetBody,0,1);
            _bt3NativeLeftPane=BuildBt3NativeActionPanel();
            _bt3NativeTransformPanel=BuildBt3NativeTransformPanel();
            _bt3NativeRightPane=BuildBt3NativeEditPanel();
            var viewTools=modelEditControl1.Controls.Find("toolStrip1",true).OfType<ToolStrip>().FirstOrDefault();
            _bt3BonesToggle=viewTools?.Items.Find("chkBones",true).OfType<ToolStripButton>().FirstOrDefault();
            _bt3PolygonsToggle=viewTools?.Items.Find("chkPolygons",true).OfType<ToolStripButton>().FirstOrDefault();
            _bt3VerticesToggle=viewTools?.Items.Find("chkVertices",true).OfType<ToolStripButton>().FirstOrDefault();
            foreach(var toggle in new[]{_bt3BonesToggle,_bt3PolygonsToggle,_bt3VerticesToggle})
                if(toggle!=null)toggle.Click+=(s,e)=>SyncBt3NativeRenderToggles();
            _bt3TargetPlayback=modelEditControl1.PlaybackPanel;
            _bt3TargetTimer=new System.Windows.Forms.Timer{Interval=16};
            _bt3TargetTimer.Tick+=(s,e)=>{
                if(_bt3TargetMotion==null||_bt3TargetHost?.Visible!=true){StopBt3TargetPlayback();return;}
                _bt3TargetTime+=_bt3TargetClock.Elapsed.TotalSeconds*60;_bt3TargetClock.Restart();
                if(_bt3TargetTime>=_bt3TargetMotion.Length)
                {
                    if(_bt3PreviewLoop&&_bt3TargetMotion.Length>0)
                        _bt3TargetTime %= _bt3TargetMotion.Length;
                    else{_bt3TargetTime=_bt3TargetMotion.Length;StopBt3TargetPlayback();}
                }
                RenderBt3TargetFrame();
            };
            _bt3TargetForm.SelectedIndexChanged+=(s,e)=>{
                if(_bt3TargetUpdating)return;
                PopulateBt3TargetCostumes();PopulateBt3TargetActions(0);QueueBt3TargetLoad();
            };
            _bt3TargetCostume.SelectedIndexChanged+=(s,e)=>{if(!_bt3TargetUpdating)QueueBt3TargetLoad();};
            _bt3TargetAction.SelectedIndexChanged+=(s,e)=>{if(!_bt3TargetUpdating)QueueBt3TargetLoad();};
            FormClosing+=(s,e)=>{
                if(e.CloseReason==CloseReason.UserClosing&&_bt3TargetEdits.Values.Any(edit=>edit.HasUnsavedChanges)&&
                    MessageBox.Show(this,"BT3 motion edits have not been saved to independent native files. Close Fighterport?",
                        "BT3 motion edits",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)e.Cancel=true;
            };
            modelEditControl1.ModelPanel.Parent.Controls.Add(_bt3TargetHost);
        }

        private void SyncBt3NativeRenderToggles()
        {
            if(_bt3TargetRenderer==null)return;
            _bt3TargetRenderer.ShowBones=_bt3BonesToggle?.Checked??true;
            _bt3TargetRenderer.ShowPolygons=_bt3PolygonsToggle?.Checked??true;
            _bt3TargetRenderer.ShowVertices=_bt3VerticesToggle?.Checked??false;
            _bt3TargetViewport.Invalidate();
        }
        private Control BuildBt3NativeActionPanel()
        {
            var panel=new TableLayoutPanel{Name="bt3NativeSelectors",Dock=DockStyle.Fill,
                BackColor=Color.FromArgb(35,39,46),ColumnCount=1,RowCount=4,Padding=new Padding(2)};
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(int height in new[]{16,24,16,24})panel.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            Label Title(string value)=>new Label{Text=value,Dock=DockStyle.Fill,ForeColor=Color.White};
            panel.Controls.Add(Title("Transform"),0,0);
            _bt3TargetForm=Bt3TargetCombo("bt3TargetTransform",180);_bt3TargetForm.Dock=DockStyle.Fill;
            panel.Controls.Add(_bt3TargetForm,0,1);
            panel.Controls.Add(Title("Costume"),0,2);
            _bt3TargetCostume=Bt3TargetCombo("bt3TargetCostume",180);_bt3TargetCostume.Dock=DockStyle.Fill;
            panel.Controls.Add(_bt3TargetCostume,0,3);
            _bt3TargetAction=new ListBox{Name="bt3TargetActionState"};
            return panel;
        }
        private Control BuildBt3NativeTransformPanel()
        {
            var panel=new TableLayoutPanel{Name="bt3NativeTransformPanel",Dock=DockStyle.Fill,ColumnCount=4,RowCount=4,
                BackColor=Color.FromArgb(35,39,46),Padding=new Padding(2)};
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,76));
            for(int i=0;i<3;i++)panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3f));
            foreach(int height in new[]{18,26,26,32})panel.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            for(int i=0;i<3;i++)panel.Controls.Add(new Label{Text=new[]{"X","Y","Z"}[i],Dock=DockStyle.Fill,
                ForeColor=Color.White,TextAlign=ContentAlignment.MiddleCenter},i+1,0);
            panel.Controls.Add(new Label{Text="Position",Dock=DockStyle.Fill,ForeColor=Color.White},0,1);
            _bt3EditTranslation=Enumerable.Range(0,3).Select(i=>Bt3EditNumber("bt3EditTranslation"+i,100000,4)).ToArray();
            for(int i=0;i<3;i++){_bt3EditTranslation[i].Dock=DockStyle.Fill;panel.Controls.Add(_bt3EditTranslation[i],i+1,1);}
            panel.Controls.Add(new Label{Text="Rotate +°",Dock=DockStyle.Fill,ForeColor=Color.White},0,2);
            _bt3EditRotation=Enumerable.Range(0,3).Select(i=>Bt3EditNumber("bt3EditRotation"+i,3600,3)).ToArray();
            for(int i=0;i<3;i++){_bt3EditRotation[i].Dock=DockStyle.Fill;panel.Controls.Add(_bt3EditRotation[i],i+1,2);}
            _bt3SelectedBoneLabel=new Label{Name="bt3SelectedBoneLabel",Text="Bone —",Dock=DockStyle.Fill,
                ForeColor=Color.White,TextAlign=ContentAlignment.MiddleLeft};
            panel.Controls.Add(_bt3SelectedBoneLabel,0,3);
            _bt3EditApply=new Button{Name="bt3ApplyNativeKey",Text="Apply key",Dock=DockStyle.Fill};
            _bt3EditApply.Click+=(s,e)=>ApplyBt3NativeKey();panel.Controls.Add(_bt3EditApply,1,3);
            _bt3EditUndo=new Button{Name="bt3NativeUndo",Text="Undo",Dock=DockStyle.Fill};
            _bt3EditRedo=new Button{Name="bt3NativeRedo",Text="Redo",Dock=DockStyle.Fill};
            _bt3EditUndo.Click+=(s,e)=>{_bt3TargetEditor?.Undo();SyncBt3NativeEditor();};
            _bt3EditRedo.Click+=(s,e)=>{_bt3TargetEditor?.Redo();SyncBt3NativeEditor();};
            panel.Controls.Add(_bt3EditUndo,2,3);panel.Controls.Add(_bt3EditRedo,3,3);
            return panel;
        }
        private Control BuildBt3NativeEditPanel()
        {
            var panel=new FlowLayoutPanel{Name="bt3NativeEditPanel",Dock=DockStyle.Fill,Visible=false,
                FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(6),
                BackColor=Color.FromArgb(35,39,46)};
            panel.Controls.Add(new Label{Text="Native BT3 action member",Width=204,Height=30,ForeColor=Color.White});
            _bt3EditExport=new Button{Name="bt3ExportNativeMotion",Text="Save native motion...",Width=204,Height=32};
            _bt3EditExport.Click+=(s,e)=>SaveBt3NativeMotion();panel.Controls.Add(_bt3EditExport);
            _bt3EditImport=new Button{Name="bt3ImportNativeMotion",Text="Load native motion...",Width=204,Height=32};
            _bt3EditImport.Click+=(s,e)=>ImportBt3NativeMotion();panel.Controls.Add(_bt3EditImport);
            panel.Controls.Add(new Label{Text="Original disc remains unchanged.",Width=204,Height=30,ForeColor=Color.White});
            return panel;
        }
        private static NumericUpDown Bt3EditNumber(string name,int range,int decimals)
            =>new NumericUpDown{Name=name,Width=72,Minimum=-range,Maximum=range,DecimalPlaces=decimals,
                Increment=decimals==4?0.01m:1m};
        private void RefreshBt3NativePoseControls()
        {
            if(_bt3TargetEditor==null||_bt3SelectedBoneId<0)return;
            var rig=_bt3TargetRenderer.Scene.Skeleton;
            var pose=Bt3NativeAssetReader.Sample(rig,_bt3TargetEditor.Motion,(float)_bt3TargetTime);
            int index=Array.FindIndex(rig.Bones,b=>b.Id==_bt3SelectedBoneId);
            if(index<0)return;
            var value=pose.Translation[index];float[] parts={value.X,value.Y,value.Z};
            for(int i=0;i<3;i++)_bt3EditTranslation[i].Value=Math.Max(_bt3EditTranslation[i].Minimum,
                Math.Min(_bt3EditTranslation[i].Maximum,(decimal)parts[i]));
            foreach(var number in _bt3EditRotation)number.Value=0;
        }
        private void SyncBt3NativeEditor()
        {
            if(_bt3TargetEditor==null)return;
            _bt3TargetMotion=_bt3TargetEditor.Motion;RenderBt3TargetFrame();RefreshBt3NativePoseControls();
            _bt3EditUndo.Enabled=_bt3TargetEditor.CanUndo;_bt3EditRedo.Enabled=_bt3TargetEditor.CanRedo;
        }
        private void ApplyBt3NativeKey()
        {
            if(_bt3TargetEditor==null||_bt3SelectedBoneId<0)return;
            StopBt3TargetPlayback();
            try
            {
                var rig=_bt3TargetRenderer.Scene.Skeleton;
                var pose=Bt3NativeAssetReader.Sample(rig,_bt3TargetEditor.Motion,(float)_bt3TargetTime);
                int index=Array.FindIndex(rig.Bones,b=>b.Id==_bt3SelectedBoneId);
                if(index<0)throw new InvalidDataException("The selected native bone is unavailable.");
                var delta=Quaternion.CreateFromYawPitchRoll((float)_bt3EditRotation[1].Value*(float)Math.PI/180f,
                    (float)_bt3EditRotation[0].Value*(float)Math.PI/180f,
                    (float)_bt3EditRotation[2].Value*(float)Math.PI/180f);
                var translation=new Vector3((float)_bt3EditTranslation[0].Value,
                    (float)_bt3EditTranslation[1].Value,(float)_bt3EditTranslation[2].Value);
                _bt3TargetEditor.SetKey(_bt3SelectedBoneId,(int)_bt3TargetPlayback.numFrameIndex.Value-1,translation,
                    Quaternion.Normalize(pose.Rotation[index]*delta));
                SyncBt3NativeEditor();
            }
            catch(Exception error)when(error is InvalidDataException||error is ArgumentException||error is OverflowException)
            {MessageBox.Show(this,error.Message,"BT3 native edit",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        private void SaveBt3NativeMotion()
        {
            if(_bt3TargetEditor==null)return;
            using(var dialog=new SaveFileDialog{Title="Save independent native BT3 motion member",Filter="BT3 BPE motion (*.bpe)|*.bpe",FileName="Action-"+_bt3TargetChoice.Action.ToString("D3")+".bpe",OverwritePrompt=true})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try{WriteBt3NativeMotion(dialog.FileName);}
                catch(Exception error)when(error is IOException||error is InvalidDataException||error is UnauthorizedAccessException)
                {MessageBox.Show(this,error.Message,"BT3 native motion save",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
            }
        }
        private void WriteBt3NativeMotion(string destination)
        {
            if(_bt3TargetEditor==null)throw new InvalidOperationException("Choose an editable BT3 animation first.");
            string path=Path.GetFullPath(destination),source=Path.GetFullPath(_bt3TargetChoice.Inventory.Path);
            if(string.Equals(path,source,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The original disc cannot be overwritten.");
            byte[] candidate=_bt3TargetEditor.Export();
            var check=Bt3NativeAssetReader.ReadMotion(candidate,_bt3TargetChoice.Action);
            if(check.Length!=_bt3TargetEditor.Motion.Length||check.Tracks.Length!=_bt3TargetEditor.Motion.Tracks.Length)
                throw new InvalidDataException("The native candidate did not reopen with the same timing and tracks.");
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllBytes(temporary,candidate);
                Bt3NativeAssetReader.ReadMotion(File.ReadAllBytes(temporary),_bt3TargetChoice.Action);
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
                _bt3TargetEditor.MarkSaved(candidate);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        private void ImportBt3NativeMotion()
        {
            if(_bt3TargetEditor==null)return;
            if(_bt3TargetEditor.Dirty&&MessageBox.Show(this,"Replace the current BT3 motion edits?",
                "Load native motion",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
            using(var dialog=new OpenFileDialog{Title="Load an independent native BT3 motion member",
                Filter="BT3 BPE motion (*.bpe)|*.bpe|All files (*.*)|*.*"})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try{LoadBt3NativeMotion(dialog.FileName);}
                catch(Exception error)when(error is IOException||error is InvalidDataException||error is UnauthorizedAccessException)
                {MessageBox.Show(this,error.Message,"BT3 native motion load",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
            }
        }
        private void LoadBt3NativeMotion(string path)
        {
            if(_bt3TargetEditor==null||_bt3TargetRenderer==null)throw new InvalidOperationException("Open a native BT3 target first.");
            var info=new FileInfo(path);
            if(!info.Exists||info.Length<13||info.Length>4*1024*1024)
                throw new InvalidDataException("Choose a bounded native BPE motion member.");
            var next=new Bt3NativeMotionEditor(File.ReadAllBytes(path),_bt3TargetRenderer.Scene.Skeleton,_bt3TargetChoice.Action);
            if(next.Motion.Length!=_bt3TargetMotion.Length||next.Motion.EventCount!=_bt3TargetMotion.EventCount)
                throw new InvalidDataException("The native motion's timing or events do not match the selected action.");
            StopBt3TargetPlayback();
            string key=_bt3TargetChoice.Costume.Identity+"|"+_bt3TargetChoice.Motion.Identity;
            _bt3TargetEdits[key]=next;_bt3TargetEditor=next;SyncBt3NativeEditor();
        }

        private void PopulateBt3TargetCostumes()
        {
            var form=_bt3TargetForm.SelectedItem as Bt3DiscInventory.Bt3FighterSlot;
            _bt3TargetUpdating=true;
            try
            {
                _bt3TargetCostume.Items.Clear();
                if(form!=null)foreach(var resource in form.Costumes)
                {
                    int native=resource.AfsIndex-(Bt3DiscInventory.FighterAfsBase+form.Slot*10);
                    _bt3TargetCostume.Items.Add(new Bt3CostumeOption{Resource=resource,Number=native%4,Damaged=native>=4});
                }
                _bt3TargetCostume.SelectedItem=_bt3TargetCostume.Items.Cast<Bt3CostumeOption>().FirstOrDefault(c=>!c.Damaged)
                    ??_bt3TargetCostume.Items.Cast<Bt3CostumeOption>().FirstOrDefault();
            }
            finally{_bt3TargetUpdating=false;}
        }
        private void PopulateBt3TargetActions(int wanted)
        {
            var form=_bt3TargetForm.SelectedItem as Bt3DiscInventory.Bt3FighterSlot;
            _bt3TargetUpdating=true;
            try
            {
                _bt3TargetAction.Items.Clear();
                if(form!=null)foreach(int action in form.MotionMembers[0]
                    .Where(m=>m.PackIndex>0&&m.PackIndex<=0x19E&&m.Bytes>=10)
                    .Select(m=>m.PackIndex-1).Distinct().OrderBy(a=>a))
                    _bt3TargetAction.Items.Add(new Bt3ActionOption{Action=action});
                _bt3TargetAction.SelectedItem=_bt3TargetAction.Items.Cast<Bt3ActionOption>()
                    .FirstOrDefault(a=>a.Action==wanted)??_bt3TargetAction.Items.Cast<Bt3ActionOption>().FirstOrDefault();
            }
            finally{_bt3TargetUpdating=false;}
            if(_bt3OriginalViewportParent!=null)RefreshBt3NativeLists(null);
        }

        private void RefreshBt3NativeLists(Bt3NativeMesh.Scene scene)
        {
            var actions=_bt3TargetAction.Items.Cast<Bt3ActionOption>()
                .Select(a=>new BrawlCrate.UI.Model_Previewer.NativeViewerItem(a.Action,a.ToString())).ToArray();
            var objects=scene?.Batches.Select((batch,index)=>
                new BrawlCrate.UI.Model_Previewer.NativeViewerItem(index,
                    "Mesh "+index.ToString("D3")+" · bone "+batch.Bone.ToString("D2"))).ToArray()
                ??new BrawlCrate.UI.Model_Previewer.NativeViewerItem[0];
            var textures=scene?.Textures.Select((texture,index)=>
                new BrawlCrate.UI.Model_Previewer.NativeViewerItem(index,
                    "Texture "+index.ToString("D2")+" · "+texture.Width+"×"+texture.Height)).ToArray()
                ??new BrawlCrate.UI.Model_Previewer.NativeViewerItem[0];
            modelEditControl1.leftPanel.ShowNativeViewer(_bt3NativeLeftPane,actions,objects,textures,
                id=>{
                    var option=_bt3TargetAction.Items.Cast<Bt3ActionOption>().FirstOrDefault(a=>a.Action==id);
                    if(option!=null)_bt3TargetAction.SelectedItem=option;
                },
                (id,visible)=>{_bt3TargetRenderer?.SetBatchVisible(id,visible);_bt3TargetViewport.Invalidate();},
                (id,visible)=>{_bt3TargetRenderer?.SetTextureVisible(id,visible);_bt3TargetViewport.Invalidate();});
            if(_bt3TargetAction.SelectedItem is Bt3ActionOption current)
                modelEditControl1.leftPanel.SelectNativeAnimation(current.Action);
        }

        private void OpenBt3TargetView()
        {
            if(SimpleWorkspaceBusy)throw new InvalidOperationException("Finish the current operation before opening a BT3 target.");
            var selected=ResolveBt3Choice(_libraryList?.SelectedNode?.Tag as Bt3BrowseChoice)
                ??throw new InvalidOperationException("Choose an available BT3 transformation, costume and action.");
            BuildBt3TargetHost();CloseBt3TargetView(false);
            modelEditControl1.StopAnim();
            _bt3OriginalSelectedBone=modelEditControl1.SelectedBone;
            _bt3OriginalAnimation=modelEditControl1.TargetAnimation;
            _bt3OriginalFrame=modelEditControl1.CurrentFrame;
            _bt3OriginalViewportParent=_bt3TargetViewport.Parent;
            _bt3OriginalRenderTargets=_bt3TargetViewport._renderList.ToArray();
            _bt3OriginalDrawCallSort=_bt3TargetViewport.DrawCallSort;
            foreach(var target in _bt3OriginalRenderTargets)_bt3TargetViewport.RemoveTarget(target,false);
            _bt3TargetViewport.DrawCallSort=BrawlLib.OpenGL.DrawCallBase.Sort;
            _bt3TargetBody.Controls.Add(_bt3TargetViewport,0,0);
            _bt3OriginalSelection=_bt3TargetViewport.CurrentViewport.AllowSelection;
            _bt3OriginalFloor=_bt3TargetViewport.CurrentViewport.RenderFloor;
            _bt3OriginalBackground=_bt3TargetViewport.CurrentViewport.BackgroundColor;
            _bt3SavedCamera=_bt3TargetViewport.CurrentViewport.Camera;
            _bt3SavedCameraMatrix=_bt3SavedCamera._matrix;
            _bt3SavedCameraInverse=_bt3SavedCamera._matrixInverse;
            _bt3SavedCameraRotation=_bt3SavedCamera._rotation;
            _bt3SavedCameraScale=_bt3SavedCamera._scale;
            _bt3TargetViewport.CurrentViewport.AllowSelection=false;
            _bt3TargetViewport.CurrentViewport.RenderFloor=true;
            _bt3TargetViewport.CurrentViewport.BackgroundColor=Color.FromArgb(35,39,46);
            _bt3OriginalLeftVisible=modelEditControl1.leftPanel.Visible;
            _bt3OriginalRightVisible=modelEditControl1.rightPanel.Visible;
            _bt3OriginalLeftWidth=modelEditControl1.leftPanel.Width;
            _bt3OriginalRightWidth=modelEditControl1.rightPanel.Width;
            modelEditControl1.leftPanel.Width=Math.Max(215,_bt3OriginalLeftWidth);
            modelEditControl1.rightPanel.Width=Math.Max(230,_bt3OriginalRightWidth);
            modelEditControl1.leftPanel.Visible=true;
            modelEditControl1.rightPanel.Visible=true;
            _bt3RightTab=modelEditControl1.rightPanel.Controls.Find("editor",true).OfType<ComboBox>().FirstOrDefault();
            _bt3OriginalRightTab=_bt3RightTab?.SelectedIndex??0;
            if(_bt3RightTab!=null){_bt3RightTab.Items[1]="Native keys";_bt3RightTab.SelectedIndex=0;}
            _bt3OriginalKeyframePanelEnabled=modelEditControl1.rightPanel.pnlKeyframes.Enabled;
            modelEditControl1.rightPanel.pnlKeyframes.Enabled=true;
            modelEditControl1.rightPanel.pnlKeyframes.Controls.Add(_bt3NativeRightPane);
            _bt3NativeRightPane.Dock=DockStyle.Fill;_bt3NativeRightPane.Visible=true;
            _bt3NativeRightPane.BringToFront();
            modelEditControl1.rightPanel.pnlBones.NativeBoneSelected=id=>{
                _bt3SelectedBoneId=id;
                if(_bt3SelectedBoneLabel!=null)_bt3SelectedBoneLabel.Text="Bone "+id.ToString("D2");
                if(_bt3TargetRenderer!=null){_bt3TargetRenderer.SelectedBoneId=id;_bt3TargetViewport.Invalidate();}
                RefreshBt3NativePoseControls();
            };
            modelEditControl1.rightPanel.pnlBones.NativeBoneVisibility=(id,visible)=>{
                _bt3TargetRenderer?.SetBoneVisible(id,visible);_bt3TargetViewport.Invalidate();
            };
            _bt3OriginalAnimationEditors=modelEditControl1.pnlPlayback.Parent;
            _bt3OriginalAnimationEditorsVisible=_bt3OriginalAnimationEditors?.Visible==true;
            _bt3OriginalAnimEditorsHeight=modelEditControl1.animEditors.Height;
            _bt3OriginalAnimControlWidth=modelEditControl1.animCtrlPnl.Width;
            _bt3OriginalAnimControlsVisible=modelEditControl1.animCtrlPnl.Controls.Cast<Control>()
                .ToDictionary(control=>control,control=>control.Visible);
            foreach(var control in _bt3OriginalAnimControlsVisible.Keys)control.Visible=false;
            modelEditControl1.animCtrlPnl.Controls.Add(_bt3NativeTransformPanel);
            _bt3NativeTransformPanel.Dock=DockStyle.Fill;_bt3NativeTransformPanel.Visible=true;
            _bt3NativeTransformPanel.BringToFront();
            modelEditControl1.NativeEditorSize=new Size(320,106);
            modelEditControl1.CheckDimensions();
            modelEditControl1.animEditors.Visible=true;
            _bt3OriginalPlaybackParent=_bt3TargetPlayback.Parent;
            _bt3OriginalPlaybackDock=_bt3TargetPlayback.Dock;
            _bt3OriginalPlaybackEnabled=_bt3TargetPlayback.Enabled;
            _bt3OriginalPlaybackFrame=_bt3TargetPlayback.numFrameIndex.Value;
            _bt3OriginalPlaybackTotal=_bt3TargetPlayback.numTotalFrames.Value;
            _bt3OriginalPlaybackFps=_bt3TargetPlayback.numFPS.Value;
            _bt3OriginalPreviewLoop=_bt3TargetPlayback.chkLoop.Checked;
            var playbackButtons=new Control[]{_bt3TargetPlayback.btnPlay,_bt3TargetPlayback.numFrameIndex,
                _bt3TargetPlayback.btnFirst,_bt3TargetPlayback.btnLast,
                _bt3TargetPlayback.btnPrevFrame,_bt3TargetPlayback.btnNextFrame};
            _bt3OriginalPlaybackButtonsEnabled=playbackButtons.Select(button=>button.Enabled).ToArray();
            _bt3TargetPlayback.NativeTransport=new Bt3TargetTransport(this);
            _bt3TargetPlayback.Enabled=true;
            foreach(var button in playbackButtons)button.Enabled=true;
            _bt3PreviewLoop=false;
            _bt3TargetPlayback.chkLoop.Checked=false;
            _bt3OriginalModelPickerVisible=modelEditControl1.models.Visible;
            modelEditControl1.models.Visible=false;
            var nativeMenu=modelEditControl1.Controls.Find("menuStrip1",true).OfType<MenuStrip>().FirstOrDefault();
            _bt3ModelMenuItem=nativeMenu?.Items.Find("targetModelToolStripMenuItem",true).FirstOrDefault();
            _bt3OriginalModelMenuVisible=_bt3ModelMenuItem?.Visible==true;
            if(_bt3ModelMenuItem!=null)_bt3ModelMenuItem.Visible=false;
            var nativeTools=modelEditControl1.Controls.Find("toolStrip1",true).OfType<ToolStrip>().FirstOrDefault();
            _bt3NativeTransformTools=new[]{"chkToolTranslate","chkToolRotate","chkToolScale"}
                .Select(name=>nativeTools?.Items.Find(name,true).FirstOrDefault()).Where(item=>item!=null).ToArray();
            _bt3NativeTransformEnabled=_bt3NativeTransformTools.Select(item=>item.Enabled).ToArray();
            foreach(var item in _bt3NativeTransformTools)item.Enabled=false;
            _bt3TargetChoice=selected;
            _bt3TargetUpdating=true;
            try
            {
                _bt3TargetForm.Items.Clear();_bt3TargetForm.Items.AddRange(selected.Forms.Cast<object>().ToArray());
                _bt3TargetForm.SelectedItem=selected.Form;
            }
            finally{_bt3TargetUpdating=false;}
            PopulateBt3TargetCostumes();
            _bt3TargetUpdating=true;
            try{_bt3TargetCostume.SelectedItem=_bt3TargetCostume.Items.Cast<Bt3CostumeOption>()
                .FirstOrDefault(c=>ReferenceEquals(c.Resource,selected.Costume));}
            finally{_bt3TargetUpdating=false;}
            PopulateBt3TargetActions(selected.Action);
            RefreshBt3NativeLists(null);
            if(_libraryPane?.Visible==true)SetSimpleWorkspaceLibraryVisible(false);
            _simpleWorkspaceToolbar.Enabled=false;
            _bt3HiddenProjectToolbar=Controls.Find("fighterToolbar",true).OfType<ToolStrip>().FirstOrDefault();
            if(_bt3HiddenProjectToolbar!=null)_bt3HiddenProjectToolbar.Enabled=false;
            _simpleWorkspaceFocused=true;_simpleWorkspaceDrawer=false;
            _bt3TargetHost.Visible=true;_bt3TargetHost.BringToFront();
            UpdateSimpleWorkspace();QueueBt3TargetLoad();
        }

        private void StopBt3TargetPlayback()
        {
            _bt3TargetTimer?.Stop();if(_bt3TargetPlayback!=null)_bt3TargetPlayback.btnPlay.Text="Play";
        }
        private void ToggleBt3TargetPlayback()
        {
            if(_bt3TargetMotion==null)return;
            if(_bt3TargetTimer.Enabled){StopBt3TargetPlayback();return;}
            modelEditControl1.StopAnim();_bt3TargetClock.Restart();_bt3TargetTimer.Start();
            _bt3TargetPlayback.btnPlay.Text="Pause";
        }
        private void SeekBt3TargetDisplayFrame(int frame)
        {
            if(_bt3TargetUpdating||_bt3TargetMotion==null)return;
            _bt3TargetTime=frame-1;_bt3TargetClock.Restart();RenderBt3TargetFrame();
        }
        private void CancelBt3TargetLoad()
        {
            _bt3TargetEpoch++;_bt3TargetCancel?.Cancel();StopBt3TargetPlayback();
        }
        private void CloseBt3TargetView(bool restore=true)
        {
            CancelBt3TargetLoad();
            if(_bt3TargetHost==null)return;
            if(_bt3TargetRenderer!=null)_bt3TargetViewport.RemoveTarget(_bt3TargetRenderer);
            _bt3TargetRenderer?.Dispose();
            _bt3TargetRenderer=null;_bt3TargetMotion=null;_bt3TargetEditor=null;_bt3TargetChoice=null;
            _bt3TargetHost.Visible=false;
            if(_bt3OriginalViewportParent!=null)
            {
                _bt3OriginalViewportParent.Controls.Add(_bt3TargetViewport);
                _bt3TargetViewport.Dock=DockStyle.Fill;
                _bt3TargetViewport.DrawCallSort=_bt3OriginalDrawCallSort;
                foreach(var target in _bt3OriginalRenderTargets??new BrawlLib.OpenGL.IRenderedObject[0])
                    _bt3TargetViewport.AddTarget(target,false);
                _bt3TargetViewport.CurrentViewport.AllowSelection=_bt3OriginalSelection;
                _bt3TargetViewport.CurrentViewport.RenderFloor=_bt3OriginalFloor;
                _bt3TargetViewport.CurrentViewport.BackgroundColor=_bt3OriginalBackground;
                if(_bt3SavedCamera!=null)
                {
                    _bt3SavedCamera._matrix=_bt3SavedCameraMatrix;
                    _bt3SavedCamera._matrixInverse=_bt3SavedCameraInverse;
                    _bt3SavedCamera._rotation=_bt3SavedCameraRotation;
                    _bt3SavedCamera._scale=_bt3SavedCameraScale;
                }
                modelEditControl1.leftPanel.HideNativeViewer();
                modelEditControl1.rightPanel.pnlBones.HideNativeBones();
                if(_bt3OriginalAnimation!=null)
                {
                    var animation=modelEditControl1.leftPanel.listAnims.Items.Cast<ListViewItem>()
                        .FirstOrDefault(item=>ReferenceEquals(item.Tag,_bt3OriginalAnimation));
                    if(animation!=null)animation.Selected=true;
                }
                modelEditControl1.SelectedBone=_bt3OriginalSelectedBone;
                modelEditControl1.SetFrame(_bt3OriginalFrame);
                modelEditControl1.rightPanel.pnlKeyframes.Controls.Remove(_bt3NativeRightPane);
                modelEditControl1.rightPanel.pnlKeyframes.Enabled=_bt3OriginalKeyframePanelEnabled;
                if(_bt3RightTab!=null)
                {
                    _bt3RightTab.Items[1]="Keyframes";
                    _bt3RightTab.SelectedIndex=_bt3OriginalRightTab;
                }
                modelEditControl1.leftPanel.Width=_bt3OriginalLeftWidth;
                modelEditControl1.rightPanel.Width=_bt3OriginalRightWidth;
                modelEditControl1.leftPanel.Visible=_bt3OriginalLeftVisible;
                modelEditControl1.rightPanel.Visible=_bt3OriginalRightVisible;
                if(_bt3OriginalPlaybackParent!=null)
                {
                    _bt3OriginalPlaybackParent.Controls.Add(_bt3TargetPlayback);
                    _bt3TargetPlayback.Dock=_bt3OriginalPlaybackDock;
                    _bt3TargetPlayback.numTotalFrames.Value=Math.Max(_bt3OriginalPlaybackTotal,_bt3TargetPlayback.numTotalFrames.Minimum);
                    _bt3TargetPlayback.numFrameIndex.Maximum=Math.Max(_bt3TargetPlayback.numFrameIndex.Minimum,
                        _bt3TargetPlayback.numTotalFrames.Value);
                    _bt3TargetPlayback.numFrameIndex.Value=Math.Min(_bt3OriginalPlaybackFrame,
                        _bt3TargetPlayback.numFrameIndex.Maximum);
                    _bt3TargetPlayback.numFPS.Value=_bt3OriginalPlaybackFps;
                    _bt3TargetPlayback.chkLoop.Checked=_bt3OriginalPreviewLoop;
                    _bt3TargetPlayback.Enabled=_bt3OriginalPlaybackEnabled;
                    var playbackButtons=new Control[]{_bt3TargetPlayback.btnPlay,_bt3TargetPlayback.numFrameIndex,
                        _bt3TargetPlayback.btnFirst,_bt3TargetPlayback.btnLast,
                        _bt3TargetPlayback.btnPrevFrame,_bt3TargetPlayback.btnNextFrame};
                    for(int i=0;i<playbackButtons.Length;i++)playbackButtons[i].Enabled=_bt3OriginalPlaybackButtonsEnabled[i];
                    _bt3TargetPlayback.NativeTransport=null;
                    _bt3OriginalPlaybackParent=null;
                }
                modelEditControl1.animCtrlPnl.Controls.Remove(_bt3NativeTransformPanel);
                foreach(var old in _bt3OriginalAnimControlsVisible)old.Key.Visible=old.Value;
                _bt3OriginalAnimControlsVisible=null;
                modelEditControl1.NativeEditorSize=null;
                modelEditControl1.CheckDimensions();
                modelEditControl1.animCtrlPnl.Width=_bt3OriginalAnimControlWidth;
                modelEditControl1.animEditors.Height=_bt3OriginalAnimEditorsHeight;
                if(_bt3OriginalAnimationEditors!=null)_bt3OriginalAnimationEditors.Visible=_bt3OriginalAnimationEditorsVisible;
                modelEditControl1.models.Visible=_bt3OriginalModelPickerVisible;
                if(_bt3ModelMenuItem!=null)_bt3ModelMenuItem.Visible=_bt3OriginalModelMenuVisible;
                if(_bt3NativeTransformTools!=null)
                    for(int i=0;i<_bt3NativeTransformTools.Length;i++)_bt3NativeTransformTools[i].Enabled=_bt3NativeTransformEnabled[i];
                _bt3OriginalViewportParent=null;_bt3OriginalRenderTargets=null;
                _bt3TargetViewport.Invalidate();
            }
            if(restore)
            {
                _simpleWorkspaceToolbar.Enabled=true;
                if(_bt3HiddenProjectToolbar!=null)_bt3HiddenProjectToolbar.Enabled=true;
                _simpleWorkspaceFocused=true;_simpleWorkspaceDrawer=false;
                UpdateSimpleWorkspace();modelEditControl1.ModelPanel.Focus();
            }
        }

        private async void QueueBt3TargetLoad()
        {
            CancelBt3TargetLoad();
            if(_bt3TargetHost?.Visible!=true)return;
            var form=_bt3TargetForm.SelectedItem as Bt3DiscInventory.Bt3FighterSlot;
            var costume=(_bt3TargetCostume.SelectedItem as Bt3CostumeOption)?.Resource;
            if(form==null||costume==null||!(_bt3TargetAction.SelectedItem is Bt3ActionOption selected))return;
            int action=selected.Action;
            var current=_bt3TargetChoice;
            var motion=form.MotionMembers[0].FirstOrDefault(m=>m.PackIndex==action+1&&m.Bytes>=10);
            if(motion==null){_bt3TargetCaption.Text="This action is unavailable in the selected form.";return;}
            string editKey=costume.Identity+"|"+motion.Identity;
            _bt3TargetEdits.TryGetValue(editKey,out var retained);
            var priorScene=ReferenceEquals(current?.Costume,costume)?_bt3TargetRenderer?.Scene:null;
            int epoch=_bt3TargetEpoch;var cancel=new CancellationTokenSource();_bt3TargetCancel=cancel;
            _bt3TargetCaption.Text="Loading exact BT3 model and action...";
            try
            {
                var result=await Task.Run(()=>{
                    cancel.Token.ThrowIfCancellationRequested();
                    var scene=priorScene??Bt3NativeMesh.Read(current.Inventory.ReadOwned(costume,4*1024*1024));
                    cancel.Token.ThrowIfCancellationRequested();
                    var editor=retained??new Bt3NativeMotionEditor(current.Inventory.ReadOwned(motion,4*1024*1024),scene.Skeleton,action);
                    var clip=editor.Motion;
                    if(!clip.Tracks.All(t=>scene.Skeleton.Bones.Any(b=>b.Id==t.Bone)))
                        throw new InvalidDataException("The action does not match this native costume skeleton.");
                    return Tuple.Create(scene,editor);
                },cancel.Token);
                if(cancel.IsCancellationRequested||epoch!=_bt3TargetEpoch||_bt3TargetHost?.Visible!=true||
                    !ReferenceEquals(_bt3TargetForm.SelectedItem,form)||
                    !ReferenceEquals((_bt3TargetCostume.SelectedItem as Bt3CostumeOption)?.Resource,costume)||
                    !ReferenceEquals(_bt3TargetAction.SelectedItem,selected))return;
                if(!ReferenceEquals(priorScene,result.Item1))
                {
                    _bt3TargetViewport.CreateControl();_bt3TargetViewport.Capture();
                    if(_bt3TargetRenderer!=null)_bt3TargetViewport.RemoveTarget(_bt3TargetRenderer);
                    _bt3TargetRenderer?.Dispose();
                    _bt3TargetRenderer=new Bt3NativeRender(result.Item1);
                    SyncBt3NativeRenderToggles();
                    _bt3TargetViewport.AddTarget(_bt3TargetRenderer);
                    _bt3TargetRenderer.Apply(result.Item2.Motion,0);
                    var box=_bt3TargetRenderer.GetBox();var center=(box.Min+box.Max)*0.5f;
                    var half=(box.Max-box.Min)*0.75f;
                    _bt3TargetViewport.SetCamWithBox(center-half,center+half);
                    var nativeBones=result.Item1.Skeleton.Bones.Where(b=>b.Id>=0&&b.Id<71)
                        .Select(b=>new NativeViewerBone(b.Id,b.Parent,"Bone "+b.Id.ToString("D2"))).ToArray();
                    modelEditControl1.rightPanel.pnlBones.ShowNativeBones(nativeBones);
                    _bt3SelectedBoneId=nativeBones.Length>0?nativeBones[0].Id:-1;
                    modelEditControl1.rightPanel.pnlBones.SelectNativeBone(_bt3SelectedBoneId);
                    _bt3TargetRenderer.SelectedBoneId=_bt3SelectedBoneId;
                }
                _bt3TargetChoice=new Bt3BrowseChoice{Inventory=current.Inventory,Forms=current.Forms,Form=form,
                    Costume=costume,Motion=motion,Action=action};
                _bt3TargetEditor=result.Item2;_bt3TargetEdits[editKey]=result.Item2;
                _bt3TargetMotion=result.Item2.Motion;_bt3TargetTime=0;
                RefreshBt3NativeLists(result.Item1);
                _bt3TargetUpdating=true;
                try
                {
                    _bt3TargetPlayback.numTotalFrames.Value=Math.Max(1,result.Item2.Motion.Length+1);
                    _bt3TargetPlayback.numFrameIndex.Maximum=_bt3TargetPlayback.numTotalFrames.Value;
                    _bt3TargetPlayback.numFrameIndex.Value=1;
                }
                finally{_bt3TargetUpdating=false;}
                SyncBt3NativeEditor();
            }
            catch(OperationCanceledException){}
            catch(Exception error)
            {if(epoch==_bt3TargetEpoch&&!IsDisposed){if(_bt3TargetRenderer!=null)_bt3TargetViewport.RemoveTarget(_bt3TargetRenderer);
                _bt3TargetRenderer?.Dispose();
                _bt3TargetRenderer=null;_bt3TargetMotion=null;_bt3TargetEditor=null;
                _bt3TargetCaption.Text="BT3 target view unavailable: "+error.Message;}}
            finally{if(ReferenceEquals(_bt3TargetCancel,cancel))_bt3TargetCancel=null;cancel.Dispose();}
        }

        private void RenderBt3TargetFrame()
        {
            if(_bt3TargetRenderer==null||_bt3TargetMotion==null)return;
            _bt3TargetRenderer.Apply(_bt3TargetMotion,(float)_bt3TargetTime);
            _bt3TargetUpdating=true;
            try{_bt3TargetPlayback.UpdateInterface(Math.Max(1,Math.Min((int)_bt3TargetPlayback.numTotalFrames.Value,
                (int)_bt3TargetTime+1)),(int)_bt3TargetPlayback.numTotalFrames.Value);}
            finally{_bt3TargetUpdating=false;}
            _bt3TargetCaption.Text="Native BT3 target view · "+_bt3TargetChoice.Form.Display+" · "+
                (_bt3TargetCostume.SelectedItem?.ToString()??"Costume")+" · action "+
                _bt3TargetChoice.Action.ToString("D3")+" · frame "+((int)_bt3TargetTime+1)+" / "+
                (_bt3TargetMotion.Length+1)+(_bt3TargetEditor?.Dirty==true?" · edited":"");
            if(_fighterStatus!=null)_fighterStatus.Text="BT3 native animation editor · Save native motion writes an independent BPE member; project Save does not store this view.";
            if(_bt3TargetTimer?.Enabled!=true)RefreshBt3NativePoseControls();
            _bt3TargetViewport.Invalidate();
        }
    }
}
