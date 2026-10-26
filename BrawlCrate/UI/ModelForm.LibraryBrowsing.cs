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
        private static LibraryGameBranch[] PlanLibraryOutline(List<LibrarySearchItem> items,string[] configured,string selected,LibraryGameLocation[] locations,bool includeEmptySources)
        {
            string Scope(LibrarySearchItem item)=>item.Motion.GameSourceId==null?"game:"+item.Motion.Game:"source:"+item.Motion.GameSourceId;
            var groups=items.GroupBy(Scope).ToDictionary(g=>g.Key,g=>g.ToList());
            var sources=locations.ToDictionary(l=>"source:"+l.Id);
            var keys=groups.Keys.Concat(includeEmptySources?sources.Keys:Enumerable.Empty<string>())
                .Concat(configured.Where(g=>!locations.Any(l=>l.Game==g)).Select(g=>"game:"+g)).Distinct();
            return keys.Select(scope=>{
                var rows=groups.TryGetValue(scope,out var found)?found:new List<LibrarySearchItem>();sources.TryGetValue(scope,out var location);
                string game=location?.Game??rows.FirstOrDefault()?.Motion.Game??scope.Substring(5);
                string sourceLabel=location==null?game:game+" · "+location.Label;
                if(location!=null&&locations.Count(l=>l.Game==game&&l.Label==location.Label)>1)sourceLabel+=" · "+location.GameVersion+" ["+location.Id.Substring(0,8)+"]";
                return new LibraryGameBranch{Name=scope,Text=sourceLabel,Indexed=location?.Indexed??rows.Count>0,ToolTip=location==null?game:location.GameVersion+" · "+location.Path,Characters=rows
                    .GroupBy(i=>i.Model?.Character??i.Clip.SourceCharacter??"Unverified",StringComparer.Ordinal)
                    .OrderBy(g=>g.Key,StringComparer.OrdinalIgnoreCase).Select(character=>{
                        var clips=character.ToList();
                        // Disc/install catalogues already own verified original
                        // associations. Browse a character's animations directly;
                        // compatible costume selection belongs to its selector.
                        bool direct=clips.Any(i=>!string.IsNullOrEmpty(i.Motion.GameSourceId));
                        if(direct)return new LibraryBranch{Name="character:"+scope+"|"+character.Key,Text=character.Key,
                            ToolTip=clips.Count+" indexed animations · choose a costume below",HasSelection=clips.Any(i=>i.Clip.Id==selected),
                            Group=new LibraryGroup{Character=true,DirectAnimations=true,Items=clips.OrderBy(i=>i.Clip.Name,StringComparer.OrdinalIgnoreCase).ThenBy(i=>i.Clip.Channel,StringComparer.Ordinal).ThenBy(i=>i.Clip.ResourceKey,StringComparer.Ordinal).ToList(),
                                DuplicateNames=new HashSet<string>(clips.GroupBy(i=>i.Clip.Name+"|"+i.Clip.Channel,StringComparer.Ordinal).Where(g=>g.Count()>1).Select(g=>g.Key),StringComparer.Ordinal)}};
                        var models=clips.GroupBy(i=>i.Model?.Id??"unassociated-"+i.Motion.Id)
                            .OrderBy(g=>g.First().Model?.Name??Path.GetFileNameWithoutExtension(g.First().Motion.VirtualPath??g.First().Motion.LocalPath),StringComparer.OrdinalIgnoreCase)
                            .Select(model=>{
                                var first=model.First();var motions=model.ToList();
                                string label=first.Model?.Name??"Unassociated · "+Path.GetFileNameWithoutExtension(first.Motion.VirtualPath??first.Motion.LocalPath);
                                string variant=first.Model?.Variant;
                                if(first.Model!=null&&!string.IsNullOrWhiteSpace(variant)&&variant!=first.Model.Name)label+=" · "+variant;
                                return new LibraryBranch{Name=model.Key,Text=label,Group=new LibraryGroup{Items=motions},
                                    HasSelection=motions.Any(i=>i.Clip.Id==selected),
                                    ToolTip=motions.Count+" indexed animations · "+first.Motion.GameVersion+" · "+(first.Model?.ResourcePath??"Model association required")};
                            }).ToArray();
                        return new LibraryBranch{Name="character:"+scope+"|"+character.Key,Text=character.Key,
                            ToolTip=clips.Count+" indexed animations · "+game,HasSelection=models.Any(m=>m.HasSelection),
                            Group=new LibraryGroup{Items=clips,Character=true,Models=models}};
                    }).ToArray()};
            }).OrderBy(g=>g.Text,StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private void CancelLibraryTreeWork()
        {
            _libraryPopulateTimer?.Stop();_libraryPendingGroups.Clear();_libraryTreeWork.Clear();
            _libraryTreeCompleted=null;_libraryClearingTree=false;
        }

        private async void RefreshLibrary(string selectId=null)
        {
            int generation=++_librarySearchGeneration;
            // A retained clip may have new/removed costume metadata after an
            // atomic catalogue update. Re-enumerate once when this refresh
            // settles, without adding filesystem work to ordinary UI ticks.
            _libraryCostumeCatalogue=null;_libraryCostumeClip=null;
            CancelLibraryTreeWork();_libraryReplacingTree=true;
            try
            {
                string selected=selectId??LibrarySelection?.Clip.Id;
                if(modelEditControl1.Playing&&selectId==null)selected=null;
                var query=CurrentLibraryQuery();
                var catalogue=_library;
                _libraryUse.Enabled=_libraryPreview.Enabled=false;_libraryPage.Text="Searching...";
                var result=await Task.Run(()=>{
                    if(generation!=_librarySearchGeneration)return null;
                    var rows=catalogue.Browse(query);
                    return Tuple.Create(PlanLibraryOutline(rows,query.HasFilters?new string[0]:catalogue.ConfiguredGames,selected,catalogue.Read().GameLocations.ToArray(),!query.HasFilters),rows.Count,catalogue.ClipCount,catalogue.CanUndo,catalogue.Channels);
                });
                if(result==null||IsDisposed||_libraryPane.IsDisposed||generation!=_librarySearchGeneration||!ReferenceEquals(catalogue,_library))return;
                UpdateLibraryChannels(result.Item5);
                _librarySelectAfterRefresh=selected;_libraryExpandSearch=query.HasFilters&&result.Item2<=200;
                _libraryRestoreExpansions=!modelEditControl1.Playing;
                _libraryTreeGeneration=generation;_libraryClearingTree=true;
                _libraryUpdatingTree=true;
                try{_libraryList.SelectedNode=null;}finally{_libraryUpdatingTree=false;}
                foreach(var game in result.Item1)
                    _libraryTreeWork.Enqueue(()=>{
                        var node=new TreeNode(game.Text){Name=game.Name,ToolTipText=game.ToolTip};_libraryList.Nodes.Add(node);
                        node.Nodes.Add(game.Characters.Length==0?(game.Indexed?"No matching animations":"Saved location — not indexed"):"Loading characters...");
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
            if(group.Character&&!group.DirectAnimations)
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
                int selected=group.Items.FindIndex(i=>i.Clip.Id==_librarySelectAfterRefresh);
                if(selected>=group.BatchEnd)group.BatchEnd=selected+1;
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
                string label=group.DirectAnimations?item.Clip.Name+
                    (item.Clip.Channel=="CHR0 skeletal"?"":" · "+item.Clip.Channel.Replace("Node",""))+
                    (group.DuplicateNames.Contains(item.Clip.Name+"|"+item.Clip.Channel)?" · "+(item.Model?.Name??Path.GetFileNameWithoutExtension(item.Motion.VirtualPath??item.Motion.LocalPath))+" ["+item.Clip.ResourceKey+"]":"")+
                    (item.State==LibrarySourceState.Available?"":" · "+LibraryStateText(item.State)):
                    item.Clip.Name+" · "+(item.Model?.Name??"Unassociated")+" · "+LibraryStateText(item.State);
                var row=new TreeNode(label)
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
