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
                Motion=motion,Action=action};
        }

        private void CancelBt3PreviewLoad()
        {
            _bt3PreviewEpoch++;_bt3PreviewTimer?.Stop();_bt3PreviewCancel?.Cancel();
        }
        private void ReleaseBt3Preview()
        {
            CancelBt3PreviewLoad();
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
            var priorScene=ReferenceEquals(_bt3PreviewChoice?.Costume,costume)?_bt3Renderer?.Scene:null;
            int epoch=_bt3PreviewEpoch;var cancel=new CancellationTokenSource();_bt3PreviewCancel=cancel;
            _libraryOriginalLabel.Text="Loading original BT3 model, textures and action...";
            try
            {
                var prepared=await Task.Run(()=>{
                    cancel.Token.ThrowIfCancellationRequested();
                    Bt3NativeMesh.Scene scene=null;
                    if(priorScene==null)
                        scene=Bt3NativeMesh.Read(choice.Inventory.ReadOwned(costume,4*1024*1024));
                    cancel.Token.ThrowIfCancellationRequested();
                    var motion=Bt3NativeAssetReader.ReadMotion(choice.Inventory.ReadOwned(action,4*1024*1024),
                        action.PackIndex);
                    var rig=scene?.Skeleton??priorScene?.Skeleton;
                    if(rig==null||!motion.Tracks.All(t=>rig.Bones.Any(b=>b.Id==t.Bone)))
                        throw new InvalidDataException("The selected BT3 action does not match its exact costume skeleton.");
                    return Tuple.Create(scene,motion);
                },cancel.Token);
                if(cancel.IsCancellationRequested||epoch!=_bt3PreviewEpoch||IsDisposed||Disposing||
                    !ReferenceEquals(_libraryList?.SelectedNode?.Tag,choice)||_libraryPane?.Visible!=true||
                    !ReferenceEquals(_bt3Transform.SelectedItem,exact.Form)||
                    !ReferenceEquals((_bt3Costume.SelectedItem as Bt3CostumeOption)?.Resource,costume))return;
                if(prepared.Item1!=null)
                {
                    ReleaseLibraryPreview();_bt3Renderer=new Bt3NativeRender(prepared.Item1);
                    _libraryViewport.CreateControl();_libraryViewport.Capture();
                    _libraryViewport.ClearAll();_libraryViewport.AddTarget(_bt3Renderer);
                }
                _bt3Motion=prepared.Item2;_bt3PreviewChoice=exact;
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

        private void QueueBt3Forms(TreeNode parent,Bt3DiscInventory inventory,Bt3DiscInventory.Bt3FighterSlot[] forms)
        {
            foreach(var group in forms.GroupBy(f=>Bt3CharacterNames.CharacterSlot(f.Slot)).OrderBy(g=>g.Key))
            {
                var family=group.ToArray();var form=family[0];
                _libraryTreeWork.Enqueue(()=>{
                    if(parent.TreeView!=_libraryList)return;
                    var row=new TreeNode(Bt3CharacterNames.Get(group.Key)??form.Display)
                    {Name="bt3-character:"+parent.Name+":"+group.Key.ToString("D3"),
                        Tag=new Bt3BrowseChoice{Inventory=inventory,Form=form,Forms=family},
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
            var actions=choice.Forms.SelectMany(form=>form.MotionMembers[0])
                .Where(m=>m.PackIndex>=0&&m.PackIndex<0x19E&&m.Bytes>=10)
                .Select(m=>m.PackIndex).Distinct().OrderBy(a=>a).ToArray();
            foreach(int action in actions)
            {
                _libraryTreeWork.Enqueue(()=>{
                    if(node.TreeView!=_libraryList)return;
                    var row=new TreeNode(Bt3ActionLabels.Describe(action))
                    {Name="bt3-action:"+choice.Form.Slot.ToString("D3")+":"+action.ToString("D3"),
                        Tag=new Bt3BrowseChoice{Inventory=choice.Inventory,Form=choice.Form,Forms=choice.Forms,Action=action},
                        ToolTipText="Exact action "+action.ToString("D3")+" of the transformation selected below"};
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
            UpdateLibraryCostumes(null);UpdateBt3Selectors(choice);
            _libraryPreview.Text=choice?.Form==null?"Inspect BT3":"Preview Original";
            _libraryPreview.Enabled=!SimpleWorkspaceBusy&&!_libraryReplacingTree;
            _libraryUse.Enabled=_libraryRepair.Enabled=false;
            var exact=ResolveBt3Choice(choice);
            _libraryOpenTarget.Enabled=exact!=null&&!SimpleWorkspaceBusy&&!_libraryReplacingTree;
            _libraryOpenTarget.AccessibleDescription="Open this exact BT3 form, costume and action in the shared Model Previewer. Native bone keys can be edited and saved as an independent BPE motion member.";
            if(_bt3Renderer==null)_libraryOriginalLabel.Text="BT3 source preview · choose a form, costume or action";
            _libraryDetails.Text=choice==null?"Browse the saved BT3 disc by character. Choose a transformation and costume below, then choose an action.":
                (exact==null?"This action is unavailable for the selected transformation.":
                    exact.Form.Display+Environment.NewLine+
                    (_bt3Costume.SelectedItem?.ToString()??"Costume")+" · "+exact.Costume.Identity+Environment.NewLine+
                    "Action "+exact.Action.ToString("D3")+" · "+exact.Motion.Identity+" · "+exact.Motion.Bytes+" original bytes.")+
                Environment.NewLine+"Native source preview and target bone-key editing. Save native motion writes an independent BPE member; ISO insertion and project Save are not yet supported for BT3.";
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
