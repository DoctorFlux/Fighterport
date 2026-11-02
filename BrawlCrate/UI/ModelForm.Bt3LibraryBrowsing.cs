using BrawlCrate.Studio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
            internal Bt3DiscInventory.Bt3Resource Costume,Motion;
            internal int Action=-1;
            internal bool Loaded;
        }
        private readonly object _bt3BrowseCacheGate=new object();
        private readonly Dictionary<string,Lazy<Bt3DiscInventory>> _bt3BrowseCache=new Dictionary<string,Lazy<Bt3DiscInventory>>(StringComparer.OrdinalIgnoreCase);

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
            foreach(var form in forms)
                _libraryTreeWork.Enqueue(()=>{
                    if(parent.TreeView!=_libraryList)return;
                    var row=new TreeNode(form.Display){Name="bt3-form:"+parent.Name+":"+form.Slot.ToString("D3"),
                        Tag=new Bt3BrowseChoice{Inventory=inventory,Form=form},
                        ToolTipText="Exact native form slot "+form.Slot.ToString("D3")+" · choose a costume, then an action"};
                    row.Nodes.Add("Expand to choose costume");parent.Nodes.Add(row);
                });
            _libraryTreeWork.Enqueue(()=>{
                if(parent.TreeView!=_libraryList)return;
                if(parent.Nodes.Count>0)parent.Nodes.RemoveAt(0);
                if(forms.Length==0)parent.Nodes.Add("No matching BT3 forms");
            });
        }

        private bool PopulateBt3Branch(TreeNode node)
        {
            if(node?.Tag is Bt3DiscInventory)return true; // Forms already queued with the game branch.
            if(!(node?.Tag is Bt3BrowseChoice choice))return false;
            if(choice.Loaded)return true;
            choice.Loaded=true;
            if(choice.Costume==null)
            {
                foreach(var costume in choice.Form.Costumes)
                    _libraryTreeWork.Enqueue(()=>{
                        if(node.TreeView!=_libraryList)return;
                        int index=costume.AfsIndex-(Bt3DiscInventory.FighterAfsBase+choice.Form.Slot*10);
                        var row=new TreeNode("Costume "+index+" · native model + textures")
                        {Name="bt3-costume:"+choice.Form.Slot.ToString("D3")+":"+index,
                            Tag=new Bt3BrowseChoice{Inventory=choice.Inventory,Form=choice.Form,Costume=costume},
                            ToolTipText=costume.Identity+" · "+costume.Bytes+" original bytes"};
                        row.Nodes.Add("Expand to choose native action");node.Nodes.Add(row);
                    });
            }
            else
            {
                foreach(var motion in choice.Form.MotionMembers[0].Where(m=>m.PackIndex>0&&m.PackIndex<=0x19E&&m.Bytes>=10))
                    _libraryTreeWork.Enqueue(()=>{
                        if(node.TreeView!=_libraryList)return;
                        int action=motion.PackIndex-1;
                        var row=new TreeNode((action==0?"Idle · ":"")+"Action "+action.ToString("D3")+" · native pose (inspect)")
                        {Name="bt3-action:"+choice.Form.Slot.ToString("D3")+":"+choice.Costume.AfsIndex+":"+action.ToString("D3"),
                            Tag=new Bt3BrowseChoice{Inventory=choice.Inventory,Form=choice.Form,Costume=choice.Costume,Motion=motion,Action=action},
                            ToolTipText=motion.Identity+" · "+motion.Bytes+" original bytes · native preview not yet qualified"};
                        node.Nodes.Add(row);
                    });
            }
            _libraryTreeWork.Enqueue(()=>{
                if(node.TreeView!=_libraryList)return;
                if(node.Nodes.Count>0)node.Nodes.RemoveAt(0);
                if(node.Nodes.Count==0)node.Nodes.Add(choice.Costume==null?"No costumes in this form":"No verified action members");
            });
            _libraryPopulateTimer.Start();return true;
        }

        private bool UpdateBt3LibrarySelection()
        {
            object selected=_libraryList?.SelectedNode?.Tag;
            var choice=selected as Bt3BrowseChoice;
            var inventory=selected as Bt3DiscInventory??choice?.Inventory;
            if(inventory==null)return false;
            UpdateLibraryCostumes(null);_libraryPreview.Text="Inspect BT3";
            _libraryPreview.Enabled=!SimpleWorkspaceBusy&&!_libraryReplacingTree;
            _libraryUse.Enabled=_libraryOpenTarget.Enabled=_libraryRepair.Enabled=false;
            _libraryOriginalLabel.Text="BT3 native model preview is not qualified yet";
            _libraryDetails.Text=choice==null?"Browse the saved BT3 disc directly. Expand a form, choose its costume, then choose a native action. Inspect reads its original pose data.":
                choice.Form.Display+Environment.NewLine+
                (choice.Costume==null?"Choose a costume for this form.":
                    "Costume "+(choice.Costume.AfsIndex-(Bt3DiscInventory.FighterAfsBase+choice.Form.Slot*10))+" · "+choice.Costume.Identity+Environment.NewLine+
                    (choice.Motion==null?"Choose an action. Action 000 is idle.":
                        "Action "+choice.Action.ToString("D3")+" · "+choice.Motion.Identity+" · "+choice.Motion.Bytes+" original bytes."))+Environment.NewLine+
                "Read-only native inspection. Mesh/texture preview, retargeting and target editing remain unavailable.";
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
                if(choice!=null)dialog.SelectNative(choice.Form,choice.Costume,choice.Action);
                dialog.ShowDialog(this);
            }
            return true;
        }
    }
}
