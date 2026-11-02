using BrawlCrate.Studio;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        // A native, read-only target view. It deliberately owns no FighterProject
        // asset or CHR0 bridge; the existing editable document remains intact.
        private TableLayoutPanel _bt3TargetHost;
        private ModelPanel _bt3TargetViewport;
        private ComboBox _bt3TargetForm, _bt3TargetCostume, _bt3TargetAction;
        private TrackBar _bt3TargetFrame;
        private Label _bt3TargetCaption;
        private Button _bt3TargetPlay;
        private System.Windows.Forms.Timer _bt3TargetTimer;
        private readonly Stopwatch _bt3TargetClock=new Stopwatch();
        private Bt3BrowseChoice _bt3TargetChoice;
        private Bt3NativeRender _bt3TargetRenderer;
        private Bt3NativeAssetReader.Motion _bt3TargetMotion;
        private CancellationTokenSource _bt3TargetCancel;
        private double _bt3TargetTime;
        private int _bt3TargetEpoch;
        private bool _bt3TargetUpdating;
        private sealed class Bt3ActionOption
        {
            internal int Action;
            public override string ToString()=>Action==0?"Idle · Action 000":"Action "+Action.ToString("D3");
        }

        private static ComboBox Bt3TargetCombo(string name,int width)
            =>new ComboBox{Name=name,Width=width,DropDownStyle=ComboBoxStyle.DropDownList,Margin=new Padding(3)};

        private void BuildBt3TargetHost()
        {
            if(_bt3TargetHost!=null)return;
            _bt3TargetHost=new TableLayoutPanel{Name="bt3NativeTargetView",Dock=DockStyle.Fill,
                ColumnCount=1,RowCount=3,Visible=false,BackColor=Color.FromArgb(29,32,38)};
            _bt3TargetHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            _bt3TargetHost.RowStyles.Add(new RowStyle(SizeType.Absolute,80));
            _bt3TargetHost.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            _bt3TargetHost.RowStyles.Add(new RowStyle(SizeType.Absolute,43));
            var top=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true,Padding=new Padding(6,3,3,0)};
            _bt3TargetCaption=new Label{Name="bt3TargetCaption",Text="Native BT3 target view · read-only",
                AutoSize=false,Width=650,Height=22,ForeColor=Color.White};
            top.Controls.Add(_bt3TargetCaption);
            top.SetFlowBreak(_bt3TargetCaption,true);
            top.Controls.Add(new Label{Text="Transform",AutoSize=true,ForeColor=Color.White,Padding=new Padding(0,5,0,0)});
            _bt3TargetForm=Bt3TargetCombo("bt3TargetTransform",235);top.Controls.Add(_bt3TargetForm);
            top.Controls.Add(new Label{Text="Costume",AutoSize=true,ForeColor=Color.White,Padding=new Padding(0,5,0,0)});
            _bt3TargetCostume=Bt3TargetCombo("bt3TargetCostume",178);top.Controls.Add(_bt3TargetCostume);
            top.Controls.Add(new Label{Text="Animation",AutoSize=true,ForeColor=Color.White,Padding=new Padding(0,5,0,0)});
            _bt3TargetAction=Bt3TargetCombo("bt3TargetAction",150);top.Controls.Add(_bt3TargetAction);
            var back=new Button{Name="bt3TargetBack",Text="Return to project",AutoSize=true};
            back.Click+=(s,e)=>CloseBt3TargetView();top.Controls.Add(back);
            _bt3TargetHost.Controls.Add(top,0,0);
            _bt3TargetViewport=new ModelPanel{Name="bt3TargetViewport",Dock=DockStyle.Fill,
                AccessibleName="Native BT3 target model view"};
            _bt3TargetViewport.CurrentViewport.AllowSelection=false;
            _bt3TargetViewport.CurrentViewport.RenderFloor=true;
            _bt3TargetViewport.CurrentViewport.BackgroundColor=Color.FromArgb(35,39,46);
            _bt3TargetHost.Controls.Add(_bt3TargetViewport,0,1);
            var transport=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};
            transport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,80));
            transport.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            _bt3TargetPlay=new Button{Name="bt3TargetPlay",Text="Play",Dock=DockStyle.Fill};
            _bt3TargetPlay.Click+=(s,e)=>{
                if(_bt3TargetMotion==null)return;
                if(_bt3TargetTimer.Enabled)StopBt3TargetPlayback();
                else{modelEditControl1.StopAnim();_bt3TargetClock.Restart();_bt3TargetTimer.Start();_bt3TargetPlay.Text="Pause";}
            };
            _bt3TargetFrame=new TrackBar{Name="bt3TargetFrame",Dock=DockStyle.Fill,
                AutoSize=false,Minimum=1,Maximum=1,TickStyle=TickStyle.None};
            _bt3TargetFrame.ValueChanged+=(s,e)=>{
                if(_bt3TargetUpdating||_bt3TargetMotion==null)return;
                _bt3TargetTime=_bt3TargetFrame.Value-1;_bt3TargetClock.Restart();RenderBt3TargetFrame();
            };
            transport.Controls.Add(_bt3TargetPlay,0,0);transport.Controls.Add(_bt3TargetFrame,1,0);
            _bt3TargetHost.Controls.Add(transport,0,2);
            _bt3TargetTimer=new System.Windows.Forms.Timer{Interval=16};
            _bt3TargetTimer.Tick+=(s,e)=>{
                if(_bt3TargetMotion==null||_bt3TargetHost?.Visible!=true){StopBt3TargetPlayback();return;}
                _bt3TargetTime+=_bt3TargetClock.Elapsed.TotalSeconds*60;_bt3TargetClock.Restart();
                if(_bt3TargetTime>=_bt3TargetMotion.Length)
                {_bt3TargetTime=_bt3TargetMotion.Length;StopBt3TargetPlayback();}
                RenderBt3TargetFrame();
            };
            _bt3TargetForm.SelectedIndexChanged+=(s,e)=>{
                if(_bt3TargetUpdating)return;
                PopulateBt3TargetCostumes();PopulateBt3TargetActions(0);QueueBt3TargetLoad();
            };
            _bt3TargetCostume.SelectedIndexChanged+=(s,e)=>{if(!_bt3TargetUpdating)QueueBt3TargetLoad();};
            _bt3TargetAction.SelectedIndexChanged+=(s,e)=>{if(!_bt3TargetUpdating)QueueBt3TargetLoad();};
            _simpleWorkspaceBody.Controls.Add(_bt3TargetHost);
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
        }

        private void OpenBt3TargetView()
        {
            if(SimpleWorkspaceBusy)throw new InvalidOperationException("Finish the current operation before opening a BT3 target.");
            var selected=ResolveBt3Choice(_libraryList?.SelectedNode?.Tag as Bt3BrowseChoice)
                ??throw new InvalidOperationException("Choose an available BT3 transformation, costume and action.");
            BuildBt3TargetHost();CloseBt3TargetView(false);
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
            modelEditControl1.StopAnim();
            if(_libraryPane?.Visible==true)SetSimpleWorkspaceLibraryVisible(false);
            modelEditControl1.Visible=false;
            _simpleWorkspaceToolbar.Enabled=false;
            _simpleWorkspaceFocused=true;_simpleWorkspaceDrawer=false;
            _bt3TargetHost.Visible=true;_bt3TargetHost.BringToFront();
            UpdateSimpleWorkspace();QueueBt3TargetLoad();
        }

        private void StopBt3TargetPlayback()
        {
            _bt3TargetTimer?.Stop();if(_bt3TargetPlay!=null)_bt3TargetPlay.Text="Play";
        }
        private void CancelBt3TargetLoad()
        {
            _bt3TargetEpoch++;_bt3TargetCancel?.Cancel();StopBt3TargetPlayback();
        }
        private void CloseBt3TargetView(bool restore=true)
        {
            CancelBt3TargetLoad();
            if(_bt3TargetHost==null)return;
            _bt3TargetViewport.ClearAll();_bt3TargetRenderer?.Dispose();
            _bt3TargetRenderer=null;_bt3TargetMotion=null;_bt3TargetChoice=null;
            _bt3TargetHost.Visible=false;
            if(restore)
            {
                modelEditControl1.Visible=true;_simpleWorkspaceToolbar.Enabled=true;
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
            var priorScene=ReferenceEquals(current?.Costume,costume)?_bt3TargetRenderer?.Scene:null;
            int epoch=_bt3TargetEpoch;var cancel=new CancellationTokenSource();_bt3TargetCancel=cancel;
            _bt3TargetCaption.Text="Loading exact BT3 model and action...";
            try
            {
                var result=await Task.Run(()=>{
                    cancel.Token.ThrowIfCancellationRequested();
                    var scene=priorScene??Bt3NativeMesh.Read(current.Inventory.ReadOwned(costume,4*1024*1024));
                    cancel.Token.ThrowIfCancellationRequested();
                    var clip=Bt3NativeAssetReader.ReadMotion(current.Inventory.ReadOwned(motion,4*1024*1024),action);
                    if(!clip.Tracks.All(t=>scene.Skeleton.Bones.Any(b=>b.Id==t.Bone)))
                        throw new InvalidDataException("The action does not match this native costume skeleton.");
                    return Tuple.Create(scene,clip);
                },cancel.Token);
                if(cancel.IsCancellationRequested||epoch!=_bt3TargetEpoch||_bt3TargetHost?.Visible!=true||
                    !ReferenceEquals(_bt3TargetForm.SelectedItem,form)||
                    !ReferenceEquals((_bt3TargetCostume.SelectedItem as Bt3CostumeOption)?.Resource,costume)||
                    !ReferenceEquals(_bt3TargetAction.SelectedItem,selected))return;
                if(!ReferenceEquals(priorScene,result.Item1))
                {
                    _bt3TargetViewport.CreateControl();_bt3TargetViewport.Capture();
                    _bt3TargetViewport.ClearAll();_bt3TargetRenderer?.Dispose();
                    _bt3TargetRenderer=new Bt3NativeRender(result.Item1);
                    _bt3TargetViewport.AddTarget(_bt3TargetRenderer);
                    _bt3TargetRenderer.Apply(result.Item2,0);
                    var box=_bt3TargetRenderer.GetBox();var center=(box.Min+box.Max)*0.5f;
                    var half=(box.Max-box.Min)*0.75f;
                    _bt3TargetViewport.SetCamWithBox(center-half,center+half);
                }
                _bt3TargetChoice=new Bt3BrowseChoice{Inventory=current.Inventory,Forms=current.Forms,Form=form,
                    Costume=costume,Motion=motion,Action=action};
                _bt3TargetMotion=result.Item2;_bt3TargetTime=0;
                _bt3TargetUpdating=true;
                try{_bt3TargetFrame.Maximum=Math.Max(1,result.Item2.Length+1);_bt3TargetFrame.Value=1;}
                finally{_bt3TargetUpdating=false;}
                RenderBt3TargetFrame();
            }
            catch(OperationCanceledException){}
            catch(Exception error)
            {if(epoch==_bt3TargetEpoch&&!IsDisposed){_bt3TargetViewport.ClearAll();_bt3TargetRenderer?.Dispose();
                _bt3TargetRenderer=null;_bt3TargetMotion=null;_bt3TargetCaption.Text="BT3 target view unavailable: "+error.Message;}}
            finally{if(ReferenceEquals(_bt3TargetCancel,cancel))_bt3TargetCancel=null;cancel.Dispose();}
        }

        private void RenderBt3TargetFrame()
        {
            if(_bt3TargetRenderer==null||_bt3TargetMotion==null)return;
            _bt3TargetRenderer.Apply(_bt3TargetMotion,(float)_bt3TargetTime);
            _bt3TargetUpdating=true;
            try{_bt3TargetFrame.Value=Math.Max(1,Math.Min(_bt3TargetFrame.Maximum,(int)_bt3TargetTime+1));}
            finally{_bt3TargetUpdating=false;}
            _bt3TargetCaption.Text="Native BT3 target view · "+_bt3TargetChoice.Form.Display+" · "+
                (_bt3TargetCostume.SelectedItem?.ToString()??"Costume")+" · action "+
                _bt3TargetChoice.Action.ToString("D3")+" · frame "+((int)_bt3TargetTime+1)+" / "+
                (_bt3TargetMotion.Length+1)+" · read-only";
            _bt3TargetViewport.Invalidate();
        }
    }
}
