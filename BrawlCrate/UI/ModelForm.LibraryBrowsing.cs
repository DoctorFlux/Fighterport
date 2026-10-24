using BrawlCrate.Studio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        // Prepare only metadata off-thread. Tree nodes and native handles belong
        // exclusively to the UI thread; no model/animation decoding happens here.
        private static LibraryGameBranch[] PlanLibraryOutline(List<LibrarySearchItem> items,string[] configured,string selected)
        {
            var games=items.GroupBy(i=>i.Motion.Game).ToDictionary(g=>g.Key,g=>g.ToList());
            return games.Keys.Concat(configured).Distinct().OrderBy(g=>g).Select(game=>{
                var rows=games.TryGetValue(game,out var found)?found:new List<LibrarySearchItem>();
                return new LibraryGameBranch{Name=game,Characters=rows
                    .GroupBy(i=>i.Model?.Character??i.Clip.SourceCharacter??"Unverified",StringComparer.Ordinal)
                    .OrderBy(g=>g.Key,StringComparer.OrdinalIgnoreCase).Select(character=>{
                        var clips=character.ToList();
                        var models=clips.GroupBy(i=>i.Model?.Id??"unassociated-"+i.Motion.Id)
                            .OrderBy(g=>g.First().Model?.Name??Path.GetFileNameWithoutExtension(g.First().Motion.LocalPath),StringComparer.OrdinalIgnoreCase)
                            .Select(model=>{
                                var first=model.First();var motions=model.ToList();
                                string label=first.Model?.Name??"Unassociated · "+Path.GetFileNameWithoutExtension(first.Motion.LocalPath);
                                string variant=first.Model?.Variant;
                                if(first.Model!=null&&!string.IsNullOrWhiteSpace(variant)&&variant!=first.Model.Name)label+=" · "+variant;
                                return new LibraryBranch{Name=model.Key,Text=label,Group=new LibraryGroup{Items=motions},
                                    HasSelection=motions.Any(i=>i.Clip.Id==selected),
                                    ToolTip=motions.Count+" indexed animations · "+first.Motion.GameVersion+" · "+(first.Model?.ResourcePath??"Model association required")};
                            }).ToArray();
                        return new LibraryBranch{Name="character:"+game+"|"+character.Key,Text=character.Key,
                            ToolTip=clips.Count+" indexed animations · "+game,HasSelection=models.Any(m=>m.HasSelection),
                            Group=new LibraryGroup{Items=clips,Character=true,Models=models}};
                    }).ToArray()};
            }).ToArray();
        }

        private void CancelLibraryTreeWork()
        {
            _libraryPopulateTimer?.Stop();_libraryPendingGroups.Clear();_libraryTreeWork.Clear();
            _libraryTreeCompleted=null;_libraryClearingTree=false;
        }

        private async void RefreshLibrary(string selectId=null)
        {
            int generation=++_librarySearchGeneration;
            CancelLibraryTreeWork();_libraryReplacingTree=true;
            try
            {
                string selected=selectId??LibrarySelection?.Clip.Id;
                if(modelEditControl1.Playing&&selectId==null)selected=null;
                string query=_librarySearch.Text;
                _libraryUse.Enabled=_libraryPreview.Enabled=false;_libraryPage.Text="Searching...";
                var result=await Task.Run(()=>{
                    if(generation!=_librarySearchGeneration)return null;
                    var rows=_library.Browse(query);
                    return Tuple.Create(PlanLibraryOutline(rows,_library.ConfiguredGames,selected),rows.Count,_library.ClipCount,_library.CanUndo);
                });
                if(result==null||IsDisposed||_libraryPane.IsDisposed||generation!=_librarySearchGeneration)return;
                _librarySelectAfterRefresh=selected;_libraryExpandSearch=!string.IsNullOrWhiteSpace(query)&&result.Item2<=200;
                _libraryRestoreExpansions=!modelEditControl1.Playing;
                _libraryTreeGeneration=generation;_libraryClearingTree=true;
                _libraryUpdatingTree=true;
                try{_libraryList.SelectedNode=null;}finally{_libraryUpdatingTree=false;}
                foreach(var game in result.Item1)
                    _libraryTreeWork.Enqueue(()=>{
                        var node=new TreeNode(game.Name){Name=game.Name};_libraryList.Nodes.Add(node);
                        node.Nodes.Add(game.Characters.Length==0?"Game/build path saved — not indexed":"Loading characters...");
                        if(!_libraryCollapsedGames.Contains(game.Name))ExpandLibraryBranch(node);
                        foreach(var character in game.Characters)
                            _libraryTreeWork.Enqueue(()=>AddLibraryBranch(node,character));
                        if(game.Characters.Length>0)_libraryTreeWork.Enqueue(()=>node.Nodes.RemoveAt(0));
                    });
                _libraryTreeCompleted=()=>{
                    _libraryPage.Text=result.Item3==0?"No clips":result.Item2+" matches · "+result.Item3+" total";
                    _libraryUndo.Enabled=result.Item4;UpdateLibrarySelection();
                };
                _libraryPopulateTimer.Start();
            }
            catch(Exception exception)
            {
                if(!IsDisposed&&generation==_librarySearchGeneration)
                {CancelLibraryTreeWork();MessageBox.Show(this,exception.Message,"Animation Library",MessageBoxButtons.OK,MessageBoxIcon.Error);}
            }
        }

        private void ExpandLibraryBranch(TreeNode node)
        {
            bool previous=_libraryApplyingExpansion;_libraryApplyingExpansion=true;
            try{node.Expand();}finally{_libraryApplyingExpansion=previous;}
        }

        private void AddLibraryBranch(TreeNode parent,LibraryBranch branch)
        {
            if(parent.TreeView!=_libraryList)return;
            var node=new TreeNode(branch.Text){Name=branch.Name,ToolTipText=branch.ToolTip,Tag=branch.Group};
            parent.Nodes.Add(node);node.Nodes.Add("Expand to browse all "+branch.Group.Items.Count+" animations");
            var collapsed=branch.Group.Character?_libraryCollapsedCharacters:_libraryCollapsedModels;
            var expanded=branch.Group.Character?_libraryExpandedCharacters:_libraryExpandedModels;
            if((!collapsed.Contains(node.Name)&&(_libraryExpandSearch||_libraryRestoreExpansions&&expanded.Contains(node.Name)))||branch.HasSelection)
                ExpandLibraryBranch(node);
        }

        private void ProcessLibraryTreeWork()
        {
            if(_libraryTreeGeneration!=_librarySearchGeneration){CancelLibraryTreeWork();return;}
            long start=Stopwatch.GetTimestamp();bool previous=_libraryUpdatingTree;_libraryUpdatingTree=true;
            try
            {
                // Never delete an expanded subtree in one TVM_DELETEITEM call.
                // Remove its leaves individually, yielding within one millisecond.
                do
                {
                    if(_libraryClearingTree)
                    {
                        if(_libraryList.Nodes.Count==0){_libraryClearingTree=false;continue;}
                        long clearStart=Stopwatch.GetTimestamp();
                        var leaf=_libraryList.Nodes[_libraryList.Nodes.Count-1];
                        while(leaf.Nodes.Count>0)leaf=leaf.Nodes[leaf.Nodes.Count-1];
                        leaf.Remove();MeasureLibraryUi(1,clearStart);
                    }
                    else if(_libraryTreeWork.Count>0)
                    {
                        long insertStart=Stopwatch.GetTimestamp();_libraryTreeWork.Dequeue()();MeasureLibraryUi(2,insertStart);
                    }
                    else break;
                }while((Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency<1);

                if(!_libraryClearingTree&&_libraryTreeWork.Count==0)
                {
                    if(_libraryTreeCompleted!=null)
                    {
                        var complete=_libraryTreeCompleted;_libraryTreeCompleted=null;_libraryReplacingTree=false;complete();
                    }
                    // Native redraw suppression flushes all deferred tree layout
                    // synchronously on EndUpdate. Let WM_PAINT coalesce small edits.
                    if((Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency<1)
                    {
                        var node=_libraryPendingGroups.FirstOrDefault();
                        if(node!=null)
                        {
                            if(node.TreeView==_libraryList&&node.Tag is LibraryGroup group)AppendLibraryLeaves(node,group);
                            else _libraryPendingGroups.Remove(node);
                        }
                    }
                }
                if(!_libraryClearingTree&&_libraryTreeWork.Count==0&&_libraryPendingGroups.Count==0)_libraryPopulateTimer.Stop();
            }
            finally{_libraryUpdatingTree=previous;MeasureLibraryUi(0,start);}
        }

        private void PopulateLibraryGroup(TreeNode node,bool more=false)
        {
            if(!(node?.Tag is LibraryGroup group)||group.Loaded>0&&!more)return;
            long start=Stopwatch.GetTimestamp();
            if(group.Character)
            {
                group.Loaded=group.Items.Count;
                // Keep one child while BeforeExpand returns. Native TreeView
                // cannot expand an empty node whose real children arrive later.
                if(node.Nodes.Count>0)node.Nodes[0].Text="Loading source models...";
                foreach(var model in group.Models)_libraryTreeWork.Enqueue(()=>AddLibraryBranch(node,model));
                _libraryTreeWork.Enqueue(()=>{if(node.TreeView==_libraryList&&node.Nodes.Count>1)node.Nodes.RemoveAt(0);});
                MeasureLibraryUi(3,start);
            }
            else
            {
                if(group.Loaded==0)node.Nodes.Clear(); // Only the single expansion placeholder.
                else if(node.Nodes.Count>0&&node.Nodes[node.Nodes.Count-1].Name=="libraryMore")node.Nodes.RemoveAt(node.Nodes.Count-1);
                group.BatchEnd=Math.Min(group.Items.Count,group.Loaded+1000);
                node.Nodes.Insert(0,new TreeNode("Loading animations..."){Name="libraryLoading"});
                _libraryPendingGroups.Add(node);
            }
            _libraryPopulateTimer.Start();
        }

        private void AppendLibraryLeaves(TreeNode node,LibraryGroup group)
        {
            long start=Stopwatch.GetTimestamp();bool selected=false;
            do
            {
                var item=group.Items[group.Loaded];
                var row=new TreeNode(item.Clip.Name+" · "+(item.Model?.Name??"Unassociated")+" · "+LibraryStateText(item.State))
                    {Name=item.Clip.Id,Tag=item,ToolTipText=item.Motion.GameVersion+" · "+item.Clip.ResourcePath+" ["+item.Clip.ResourceKey+"] · container SHA256 "+item.Motion.ContainerSha256+" · clip SHA256 "+item.Clip.ResourceSha256};
                node.Nodes.Add(row);group.Loaded++;
                if(item.Clip.Id==_librarySelectAfterRefresh){_libraryList.SelectedNode=row;selected=true;}
            }while(group.Loaded<group.BatchEnd&&(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency<1);
            if(group.Loaded>=group.BatchEnd)
            {
                _libraryPendingGroups.Remove(node);
                if(node.Nodes.Count>0&&node.Nodes[0].Name=="libraryLoading")node.Nodes.RemoveAt(0);
                if(group.Loaded<group.Items.Count)node.Nodes.Add(new TreeNode("Show more animations ("+group.Loaded+" / "+group.Items.Count+")"){Name="libraryMore"});
            }
            if(selected)UpdateLibrarySelection();
            MeasureLibraryUi(4,start);
        }
    }
}
