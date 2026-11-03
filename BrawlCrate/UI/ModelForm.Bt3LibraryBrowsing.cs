using BrawlCrate.Studio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        // Direct disc browsing is deliberately separate from the catalogue of
        // previewable/editable clips. It adds no fake model or animation lease.
        private sealed class Bt3BrowseChoice
        {
            internal Bt3DiscInventory Inventory;
            internal string SourceId;
            internal Bt3DiscInventory.Bt3FighterSlot Form;
            internal Bt3DiscInventory.Bt3FighterSlot[] Forms;
            internal Bt3DiscInventory.Bt3Resource Costume,Motion;
            internal int Action=-1;
            internal bool Loaded;
        }
        private sealed class Bt3CostumeOption
        {
            internal Bt3DiscInventory.Bt3Resource Resource;
            internal int Number;
            internal bool Damaged;
            public override string ToString()=>"Costume "+(Number+1)+(Damaged?" · Battle damaged":" · Normal");
        }
        private TableLayoutPanel _bt3Selectors;
        private ComboBox _bt3Transform, _bt3Costume;
        private bool _bt3UpdatingSelectors;
        private readonly object _bt3BrowseCacheGate=new object();
        private readonly Dictionary<string,Lazy<Bt3DiscInventory>> _bt3BrowseCache=new Dictionary<string,Lazy<Bt3DiscInventory>>(StringComparer.OrdinalIgnoreCase);
        private Bt3NativeRender _bt3Renderer;
        private Bt3NativeAssetReader.Motion _bt3Motion;
        private Bt3SourceLease _bt3SourceLease;
        private Bt3BrowseChoice _bt3PreviewChoice;
        private CancellationTokenSource _bt3PreviewCancel;
        private System.Windows.Forms.Timer _bt3PreviewTimer;
        private int _bt3PreviewEpoch;

        private Control BuildBt3Selectors()
        {
            _bt3Selectors=new TableLayoutPanel{Name="bt3NativeSelectors",Dock=DockStyle.Fill,Visible=false,
                ColumnCount=2,RowCount=2,Margin=Padding.Empty};
            _bt3Selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,86));
            _bt3Selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            _bt3Selectors.Controls.Add(new Label{Text="Transform",AutoSize=false,Dock=DockStyle.Fill,AutoEllipsis=true},0,0);
            _bt3Selectors.Controls.Add(new Label{Text="Costume",AutoSize=false,Dock=DockStyle.Fill,AutoEllipsis=true},0,1);
            _bt3Transform=new ComboBox{Name="bt3Transform",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,
                AccessibleName="BT3 form or transformation"};
            _bt3Costume=new ComboBox{Name="bt3Costume",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,
                AccessibleName="BT3 costume and battle damage"};
            _bt3Selectors.Controls.Add(_bt3Transform,1,0);_bt3Selectors.Controls.Add(_bt3Costume,1,1);
            _bt3Transform.SelectedIndexChanged+=(s,e)=>{
                if(_bt3UpdatingSelectors)return;
                UpdateBt3Costumes();UpdateBt3LibrarySelection();QueueBt3Preview();
            };
            _bt3Costume.SelectedIndexChanged+=(s,e)=>{
                if(_bt3UpdatingSelectors)return;
                UpdateBt3LibrarySelection();QueueBt3Preview();
            };
            return _bt3Selectors;
        }
        private void UpdateBt3Costumes()
        {
            var form=_bt3Transform?.SelectedItem as Bt3DiscInventory.Bt3FighterSlot;
            _bt3UpdatingSelectors=true;
            try
            {
                _bt3Costume.Items.Clear();
                if(form!=null)foreach(var resource in form.Costumes)
                {
                    int native=resource.AfsIndex-(Bt3DiscInventory.FighterAfsBase+form.Slot*10);
                    _bt3Costume.Items.Add(new Bt3CostumeOption{Resource=resource,Number=native%4,Damaged=native>=4});
                }
                _bt3Costume.SelectedItem=_bt3Costume.Items.Cast<Bt3CostumeOption>().FirstOrDefault(c=>!c.Damaged)
                    ??_bt3Costume.Items.Cast<Bt3CostumeOption>().FirstOrDefault();
            }
            finally{_bt3UpdatingSelectors=false;}
        }
        private void UpdateBt3Selectors(Bt3BrowseChoice choice)
        {
            if(_bt3Selectors==null)return;
            bool visible=choice?.Forms?.Length>0;
            _bt3Selectors.Visible=visible;
            if(!visible)return;
            var selected=_bt3Transform.SelectedItem as Bt3DiscInventory.Bt3FighterSlot;
            if(selected!=null&&choice.Forms.Contains(selected))return;
            _bt3UpdatingSelectors=true;
            try
            {
                _bt3Transform.Items.Clear();_bt3Transform.Items.AddRange(choice.Forms.Cast<object>().ToArray());
                _bt3Transform.SelectedItem=choice.Form??choice.Forms[0];
            }
            finally{_bt3UpdatingSelectors=false;}
            UpdateBt3Costumes();
        }
        private Bt3BrowseChoice ResolveBt3Choice(Bt3BrowseChoice choice)
        {
            if(choice==null)return null;
            var form=_bt3Transform?.SelectedItem as Bt3DiscInventory.Bt3FighterSlot??choice.Form;
            if(form==null||choice.Forms!=null&&!choice.Forms.Contains(form))return null;
            var costume=(_bt3Costume?.SelectedItem as Bt3CostumeOption)?.Resource??form.Costumes.FirstOrDefault();
            if(costume==null||!form.Costumes.Contains(costume))return null;
            int action=Math.Max(0,choice.Action);
            var motion=form.MotionMembers[0].FirstOrDefault(m=>m.PackIndex==action&&m.Bytes>=10);
            if(motion==null)return null;
            return new Bt3BrowseChoice{Inventory=choice.Inventory,Forms=choice.Forms,Form=form,Costume=costume,
                Motion=motion,Action=action,SourceId=choice.SourceId};
        }

        private void CancelBt3PreviewLoad()
        {
            _bt3PreviewEpoch++;_bt3PreviewTimer?.Stop();_bt3PreviewCancel?.Cancel();
        }
        private void ReleaseBt3Preview()
        {
            CancelBt3PreviewLoad();
            _bt3SourceLease=null;
            if(_bt3Renderer==null)return;
            _libraryTimer?.Stop();if(_libraryPlay!=null)_libraryPlay.Text="Play";
            _libraryViewport?.ClearAll();_bt3Renderer.Dispose();_bt3Renderer=null;_bt3Motion=null;_bt3PreviewChoice=null;
        }
        private bool PreviewSelectedBt3()
        {
            object selected=_libraryList?.SelectedNode?.Tag;
            if(selected is Bt3BrowseChoice choice)
            {if(choice.Form!=null&&choice.Form.Costumes.Length>0){StartBt3Preview(choice);return true;}}
            if(selected is Bt3DiscInventory){OpenSelectedBt3Inspector();return true;}
            return false;
        }
        private void QueueBt3Preview()
        {
            CancelBt3PreviewLoad();
            if(!(_libraryList?.SelectedNode?.Tag is Bt3BrowseChoice choice)||
                choice.Form?.Costumes.Length<1||_libraryPane?.Visible!=true)return;
            if(_bt3PreviewTimer==null)
            {_bt3PreviewTimer=new System.Windows.Forms.Timer{Interval=180};_bt3PreviewTimer.Tick+=(s,e)=>{
                _bt3PreviewTimer.Stop();if(_libraryList?.SelectedNode?.Tag is Bt3BrowseChoice current)StartBt3Preview(current);};}
            _bt3PreviewTimer.Start();
        }
        private async void StartBt3Preview(Bt3BrowseChoice choice)
        {
            CancelBt3PreviewLoad();
            if(choice==null||SimpleWorkspaceBusy||IsDisposed||Disposing)return;
            var exact=ResolveBt3Choice(choice);
            if(exact==null){ReleaseBt3Preview();_libraryOriginalLabel.Text="This action is unavailable for the selected transformation.";return;}
            var costume=exact.Costume;var action=exact.Motion;
            var priorLease=ReferenceEquals(_bt3PreviewChoice?.Costume,costume)?_bt3SourceLease:null;
            int epoch=_bt3PreviewEpoch;var cancel=new CancellationTokenSource();_bt3PreviewCancel=cancel;
            _libraryOriginalLabel.Text="Loading original BT3 model, textures and action...";
            try
            {
                var prepared=await Task.Run(()=>Bt3SourceLease.Open(choice.Inventory,exact.Form,costume,action,
                    priorLease,cancel.Token),cancel.Token);
                if(cancel.IsCancellationRequested||epoch!=_bt3PreviewEpoch||IsDisposed||Disposing||
                    !ReferenceEquals(_libraryList?.SelectedNode?.Tag,choice)||_libraryPane?.Visible!=true||
                    !ReferenceEquals(_bt3Transform.SelectedItem,exact.Form)||
                    !ReferenceEquals((_bt3Costume.SelectedItem as Bt3CostumeOption)?.Resource,costume))return;
                if(priorLease==null)
                {
                    ReleaseLibraryPreview();_bt3Renderer=new Bt3NativeRender(prepared.Source.Scene);
                    _libraryViewport.CreateControl();_libraryViewport.Capture();
                    _libraryViewport.ClearAll();_libraryViewport.AddTarget(_bt3Renderer);
                }
                _bt3SourceLease=prepared;_bt3Motion=prepared.Source.Motion;_bt3PreviewChoice=exact;
                _libraryTime=0;_libraryScrubbing=true;
                try{_libraryFrame.Maximum=Math.Max(1,_bt3Motion.Length+1);_libraryFrame.Value=1;}
                finally{_libraryScrubbing=false;}
                _bt3Renderer.Apply(_bt3Motion,0);FrameLibraryOriginal();RenderLibraryFrame();_libraryViewport.Update();
                _libraryDetails.Text+="\r\nNative BT3 source preview: "+_bt3Renderer.Scene.TriangleCount+" textured triangles, "+
                    _bt3Renderer.Scene.Textures.Length+" embedded textures; read-only.";
            }
            catch(OperationCanceledException){}
            catch(Exception error)
            {if(!IsDisposed&&epoch==_bt3PreviewEpoch){ReleaseBt3Preview();_libraryOriginalLabel.Text="BT3 preview unavailable: "+error.Message;}}
            finally{if(ReferenceEquals(_bt3PreviewCancel,cancel))_bt3PreviewCancel=null;cancel.Dispose();}
        }

        private static bool Bt3QueryMatch(string haystack,string words)
            =>string.IsNullOrWhiteSpace(words)||words.Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries)
                .All(word=>haystack.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0);

        private static bool Bt3FormHasActionMatch(AnimationLibrary library,string sourceId,
            Bt3DiscInventory.Bt3FighterSlot form,string query)
        {
            if(string.IsNullOrWhiteSpace(query))return true;
            return form.MotionMembers[0].Any(m=>m.PackIndex>=0&&m.PackIndex<0x19E&&m.Bytes>=10&&
                Bt3QueryMatch(form.Display+" "+Bt3ActionLabels.Describe(m.PackIndex,
                    library.GetBt3ActionAlias(sourceId,form.Slot,m.PackIndex)),query));
        }
        private string Bt3ActionName(string sourceId,int form,int action)
            =>Bt3ActionLabels.Describe(action,_library?.GetBt3ActionAlias(sourceId,form,action));
        private string Bt3ActionDetail(string sourceId,int form,int action)
            =>Bt3ActionLabels.Detail(action,_library?.GetBt3ActionAlias(sourceId,form,action));
        private void RefreshBt3ActionRows()
        {
            var selected=_libraryList?.SelectedNode;
            var branch=selected?.Tag is Bt3BrowseChoice row&&row.Action>=0?selected.Parent:selected;
            if(!(branch?.Tag is Bt3BrowseChoice choice)||choice.Action>=0)return;
            int form=(_bt3Transform?.SelectedItem as Bt3DiscInventory.Bt3FighterSlot)?.Slot??choice.Form.Slot;
            foreach(TreeNode child in branch.Nodes)
                if(child.Tag is Bt3BrowseChoice action&&action.Action>=0)
                {
                    child.Text=Bt3ActionName(choice.SourceId,form,action.Action);
                    child.ToolTipText=Bt3ActionDetail(choice.SourceId,form,action.Action);
                }
        }
        private void LabelSelectedBt3Action()
        {
            if(SimpleWorkspaceBusy||!(_libraryList?.SelectedNode?.Tag is Bt3BrowseChoice row)||row.Action<0||
                row.SourceId==null)return;
            var exact=ResolveBt3Choice(row);
            if(exact==null)return;
            string current=_library.GetBt3ActionAlias(exact.SourceId,exact.Form.Slot,exact.Action);
            using(var dialog=new Form{Name="bt3ActionLabelDialog",Text="Label BT3 action",StartPosition=FormStartPosition.CenterParent,
                FormBorderStyle=FormBorderStyle.FixedDialog,MinimizeBox=false,MaximizeBox=false,
                ShowInTaskbar=false,ClientSize=new System.Drawing.Size(440,135),Font=WorkspaceAppearance.Font})
            {
                var description=new Label{Text=exact.Form.Display+" · native action "+exact.Action.ToString("D3"),
                    Left=12,Top=10,Width=416,Height=38,AutoEllipsis=true};
                var input=new TextBox{Name="bt3ActionLabelInput",Left=12,Top=50,Width=416,MaxLength=80,
                    Text=current??"",AccessibleName="Display label for this exact BT3 form and action"};
                var save=new WorkspaceButton{Name="bt3ActionLabelSave",Text="Save label",Left=220,Top=89,Width=98,
                    DialogResult=DialogResult.OK};
                var clear=new WorkspaceButton{Name="bt3ActionLabelClear",Text="Clear label",Left=118,Top=89,Width=96,
                    DialogResult=DialogResult.No,Enabled=current!=null};
                var cancel=new WorkspaceButton{Name="bt3ActionLabelCancel",Text="Cancel",Left=324,Top=89,Width=102,
                    DialogResult=DialogResult.Cancel};
                dialog.Controls.AddRange(new Control[]{description,input,save,clear,cancel});
                dialog.AcceptButton=save;dialog.CancelButton=cancel;DarkTheme.Apply(dialog);
                var result=dialog.ShowDialog(this);
                if(result!=DialogResult.OK&&result!=DialogResult.No)return;
                string label=result==DialogResult.No?null:input.Text.Trim();
                if(result==DialogResult.OK&&label.Length==0)
                    throw new InvalidOperationException("Enter a label or choose Clear label.");
                _library.SetBt3ActionAlias(exact.SourceId,exact.Form.Slot,exact.Action,label);
                RefreshBt3ActionRows();
                if(_bt3TargetChoice?.SourceId==exact.SourceId&&_bt3TargetForm?.SelectedItem is Bt3DiscInventory.Bt3FighterSlot target&&
                    target.Slot==exact.Form.Slot)
                {
                    _bt3TargetAction.Refresh();
                    if(_bt3TargetRenderer!=null)RefreshBt3NativeLists(_bt3TargetRenderer.Scene);
                }
                UpdateBt3LibrarySelection();
                if(_library.Bt3ActionLabelError!=null)
                    MessageBox.Show(this,_library.Bt3ActionLabelError,"BT3 action labels",MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
        }

        private Bt3DiscInventory GetBt3BrowseInventory(LibraryGameLocation source)
        {
            var file=new FileInfo(source.Path);
            if(!file.Exists)throw new FileNotFoundException("The saved BT3 disc is unavailable. Repair its path in Saved games and paths.");
            string key=file.FullName+"|"+file.Length+"|"+file.LastWriteTimeUtc.Ticks;
            Lazy<Bt3DiscInventory> read;
            lock(_bt3BrowseCacheGate)
            {
                if(!_bt3BrowseCache.TryGetValue(key,out read))
                {
                    if(_bt3BrowseCache.Count>=4)_bt3BrowseCache.Clear();
                    read=new Lazy<Bt3DiscInventory>(()=>Bt3DiscInventory.Inspect(file.FullName),LazyThreadSafetyMode.ExecutionAndPublication);
                    _bt3BrowseCache[key]=read;
                }
            }
            try{return read.Value;}
            catch
            {
                lock(_bt3BrowseCacheGate)if(_bt3BrowseCache.TryGetValue(key,out var current)&&ReferenceEquals(current,read))_bt3BrowseCache.Remove(key);
                throw;
            }
        }

        private void QueueBt3Forms(TreeNode parent,Bt3DiscInventory inventory,Bt3DiscInventory.Bt3FighterSlot[] forms,
            string sourceId)
        {
            foreach(var group in forms.GroupBy(f=>Bt3CharacterNames.CharacterSlot(f.Slot)).OrderBy(g=>g.Key))
            {
                var family=group.ToArray();var form=family[0];
                _libraryTreeWork.Enqueue(()=>{
                    if(parent.TreeView!=_libraryList)return;
                    var row=new TreeNode(Bt3CharacterNames.Get(group.Key)??form.Display)
                    {Name="bt3-character:"+parent.Name+":"+group.Key.ToString("D3"),
                        Tag=new Bt3BrowseChoice{Inventory=inventory,Form=form,Forms=family,SourceId=sourceId},
                        ToolTipText=family.Length+" native form(s) · choose a transformation and costume below"};
                    row.Nodes.Add("Expand to browse native actions");parent.Nodes.Add(row);
                });
            }
            _libraryTreeWork.Enqueue(()=>{
                if(parent.TreeView!=_libraryList)return;
                if(parent.Nodes.Count>0)parent.Nodes.RemoveAt(0);
                if(forms.Length==0)parent.Nodes.Add("No matching BT3 characters");
            });
        }

        private bool PopulateBt3Branch(TreeNode node)
        {
            if(node?.Tag is Bt3DiscInventory)return true; // Forms already queued with the game branch.
            if(!(node?.Tag is Bt3BrowseChoice choice))return false;
            if(choice.Loaded)return true;
            choice.Loaded=true;
            bool all=choice.Forms.Any(f=>Bt3QueryMatch(f.Display,_librarySearch.Text));
            var actions=choice.Forms.SelectMany(form=>form.MotionMembers[0])
                .Where(m=>m.PackIndex>=0&&m.PackIndex<0x19E&&m.Bytes>=10)
                .Select(m=>m.PackIndex).Distinct().OrderBy(a=>a)
                .Where(a=>all||choice.Forms.Any(f=>Bt3QueryMatch(f.Display+" "+
                    Bt3ActionName(choice.SourceId,f.Slot,a),_librarySearch.Text))).ToArray();
            foreach(int action in actions)
            {
                _libraryTreeWork.Enqueue(()=>{
                    if(node.TreeView!=_libraryList)return;
                    var row=new TreeNode(Bt3ActionName(choice.SourceId,choice.Form.Slot,action))
                    {Name="bt3-action:"+choice.Form.Slot.ToString("D3")+":"+action.ToString("D3"),
                        Tag=new Bt3BrowseChoice{Inventory=choice.Inventory,Form=choice.Form,Forms=choice.Forms,Action=action,SourceId=choice.SourceId},
                        ToolTipText=Bt3ActionDetail(choice.SourceId,choice.Form.Slot,action)};
                    node.Nodes.Add(row);
                });
            }
            _libraryTreeWork.Enqueue(()=>{
                if(node.TreeView!=_libraryList)return;
                if(node.Nodes.Count>0)node.Nodes.RemoveAt(0);
                if(node.Nodes.Count==0)node.Nodes.Add("No indexed native actions");
            });
            _libraryPopulateTimer.Start();return true;
        }

        private bool UpdateBt3LibrarySelection()
        {
            object selected=_libraryList?.SelectedNode?.Tag;
            var choice=selected as Bt3BrowseChoice;
            var inventory=selected as Bt3DiscInventory??choice?.Inventory;
            if(inventory==null)return false;
            UpdateLibraryCostumes(null);UpdateBt3Selectors(choice);RefreshBt3ActionRows();
            _libraryPreview.Text=choice?.Form==null?"Inspect BT3":"Preview Original";
            _libraryPreview.Enabled=!SimpleWorkspaceBusy&&!_libraryReplacingTree;
            _libraryRepair.Enabled=false;
            var exact=ResolveBt3Choice(choice);
            _libraryUse.Enabled=exact!=null&&!SimpleWorkspaceBusy&&!_libraryReplacingTree&&
                _fighterProject?.TargetGame=="Brawl"&&_fighterProject.NativeTarget==null&&
                modelEditControl1.TargetModel is BrawlLib.SSBB.ResourceNodes.MDL0Node;
            _libraryUse.AccessibleDescription="Use this exact BT3 form, costume and action on the current Brawl target. The resulting native CHR0 is a review draft; contact and game acceptance remain open.";
            _libraryOpenTarget.Enabled=exact!=null&&!SimpleWorkspaceBusy&&!_libraryReplacingTree;
            _libraryOpenTarget.AccessibleDescription="Open this exact BT3 form, costume and action in the shared Model Previewer. Native bone keys can be edited and saved as an independent BPE motion member.";
            if(_bt3Renderer==null)_libraryOriginalLabel.Text="BT3 source preview · choose a form, costume or action";
            _libraryDetails.Text=choice==null?"Browse the saved BT3 disc by character. Choose a transformation and costume below, then choose an action.":
                (exact==null?"This action is unavailable for the selected transformation.":
                    exact.Form.Display+Environment.NewLine+
                    (_bt3Costume.SelectedItem?.ToString()??"Costume")+" · "+exact.Costume.Identity+Environment.NewLine+
                    Bt3ActionName(exact.SourceId,exact.Form.Slot,exact.Action)+" · "+exact.Motion.Identity+" · "+exact.Motion.Bytes+" original bytes.")+
                Environment.NewLine+"Right-click an action to set its form-specific library label. Native source preview and target bone-key editing. Save native motion writes an independent BPE member; ISO insertion and project Save are not yet supported for BT3.";
            UpdateLibraryFavorite();UpdateSimpleWorkspace();return true;
        }

        private bool OpenSelectedBt3Inspector()
        {
            object selected=_libraryList?.SelectedNode?.Tag;
            var choice=selected as Bt3BrowseChoice;
            var inventory=selected as Bt3DiscInventory??choice?.Inventory;
            if(inventory==null)return false;
            if(SimpleWorkspaceBusy)return true;
            CancelLibraryAutoPreview();PauseSourceOperation();
            using(var dialog=new Bt3DiscInventoryDialog(inventory))
            {
                var exact=ResolveBt3Choice(choice);
                if(exact!=null)dialog.SelectNative(exact.Form,exact.Costume,exact.Action);
                dialog.ShowDialog(this);
            }
            return true;
        }
    }
}
