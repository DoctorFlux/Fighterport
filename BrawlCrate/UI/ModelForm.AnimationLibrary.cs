using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private Panel _libraryPane;
        private TextBox _librarySearch, _libraryDetails;
        private TreeView _libraryList;
        private readonly HashSet<string> _libraryCollapsedGames=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _libraryCollapsedCharacters=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _libraryCollapsedModels=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _libraryExpandedCharacters=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _libraryExpandedModels=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<TreeNode> _libraryPendingGroups=new HashSet<TreeNode>();
        private Label _libraryPage, _libraryOriginalLabel;
        private Button _libraryUse, _libraryPreview, _libraryRepair, _libraryUndo, _libraryPlay;
        private TrackBar _libraryFrame;
        private ModelPanel _libraryViewport;
        private System.Windows.Forms.Timer _libraryTimer, _librarySearchTimer, _libraryPopulateTimer;
        private AnimationLibrary _library;
        private LibrarySourceLease _libraryLease;
        private readonly Stopwatch _libraryClock=new Stopwatch();
        private double _libraryTime;
        private string _librarySelectAfterRefresh;
        private bool _libraryExpandSearch;
        private bool _libraryRestoreExpansions;
        private sealed class LibraryGroup {internal List<LibrarySearchItem> Items;internal bool Character,DirectAnimations;internal HashSet<string> DuplicateNames;internal int Loaded,BatchEnd;internal LibraryBranch[] Models;}
        private sealed class LibraryBranch {internal string Name,Text,ToolTip;internal LibraryGroup Group;internal bool HasSelection;}
        private sealed class LibraryGameBranch {internal string Name,Text,ToolTip;internal bool Indexed;internal LibraryBranch[] Characters;}
        private readonly Queue<Action> _libraryTreeWork=new Queue<Action>();
        private bool _libraryClearingTree,_libraryReplacingTree,_libraryApplyingExpansion;
        private int _libraryTreeGeneration;
        private Action _libraryTreeCompleted;
        private int _librarySearchGeneration;
        private bool _libraryScrubbing;
        private bool _libraryUpdatingTree;
        private object _libraryLastTarget;
        private LibrarySearchItem LibrarySelection=>(_libraryList?.SelectedNode?.Tag as LibrarySearchItem)??(_libraryList?.SelectedNode?.Tag is LibraryGroup?_libraryCharacterSelection:null);
        private readonly long[] _libraryUiMaximumTicks=new long[8];
        private readonly int[] _libraryUiCalls=new int[8];
        private void MeasureLibraryUi(int operation,long start)
        {
            _libraryUiCalls[operation]++;
            _libraryUiMaximumTicks[operation]=Math.Max(_libraryUiMaximumTicks[operation],Stopwatch.GetTimestamp()-start);
        }
        protected override void WndProc(ref Message message)
        {
            if(message.Msg==0x84E8){string stop=modelEditControl1?._timer?.DiagnosticLastStop??"";int index=message.WParam.ToInt32();
                message.Result=(IntPtr)(index<0?stop.Length:index<stop.Length?stop[index]:0);return;}
            if(message.Msg==0x84E6){int metric=message.WParam.ToInt32();message.Result=(IntPtr)(metric==0?BrawlRetargeter.DiagnosticCaptures:
                metric==1?BrawlRetargeter.DiagnosticBakes:metric==2?(modelEditControl1.Playing?1:0):metric==3?(modelEditControl1._timer.IsRunning?1:0):
                metric==4?(modelEditControl1._loop?1:0):metric==5?modelEditControl1.MaxFrame:metric==6?modelEditControl1.CurrentFrame:0);return;}
            if(message.Msg==0x84E5){modelEditControl1?._timer?.ResetDiagnostics();return;}
            if(message.Msg==0x84E4){var timer=modelEditControl1?._timer;int metric=message.WParam.ToInt32();
                message.Result=timer==null?IntPtr.Zero:(IntPtr)(metric==5?modelEditControl1.PlaybackPanel.DiagnosticPlaybackTextUpdates:metric==0?timer.DiagnosticFrames:metric==1?
                (int)(timer.DiagnosticMaximumWorkTicks*1000000.0/Stopwatch.Frequency):metric==3?timer.DiagnosticStage:metric==4?
                (int)(timer.DiagnosticActiveStartTicks==0?0:(Stopwatch.GetTimestamp()-timer.DiagnosticActiveStartTicks)*1000000.0/Stopwatch.Frequency):
                (int)(timer.DiagnosticMaximumQueueTicks*1000000.0/Stopwatch.Frequency));return;}
            if(message.Msg==0x84E1){Array.Clear(_libraryUiMaximumTicks,0,8);Array.Clear(_libraryUiCalls,0,8);return;}
            if(message.Msg==0x84E0||message.Msg==0x84E2){int index=message.WParam.ToInt32();message.Result=index>=0&&index<8?
                (IntPtr)(message.Msg==0x84E2?_libraryUiCalls[index]:(int)(_libraryUiMaximumTicks[index]*1000000.0/Stopwatch.Frequency)):IntPtr.Zero;return;}
            base.WndProc(ref message);
        }

        private void InitializeAnimationLibrary(ToolStrip toolbar,ToolStripDropDownButton file)
        {
            AddFighterButton(toolbar,"Library","fighterLibrary",ToggleAnimationLibrary);
            file.DropDownItems.Add("Animation Library",null,(s,e)=>ToggleAnimationLibrary());
            // The drawer is created lazily; ordinary native editing never depends on its catalogue.
        }
        private Button LibraryButton(string name,string text,Action action)
        {
            var button=new WorkspaceButton{Name=name,Text=text,AutoSize=true,Height=27,Margin=new Padding(2)};
            button.Click+=(s,e)=>RunFighterAction(action);return button;
        }
        private void ToggleAnimationLibrary()
        {
            if (SimpleWorkspaceBusy) return;
            if(_libraryPane==null)BuildLibraryPane();
            bool opening = !_libraryPane.Visible;
            if (opening) { HideBatchReview(); HideLoopInspection(); }
            if (opening && _comparisonPane != null) _comparisonPane.Visible = false;
            if (opening) _workspaceLibraryOpener = _workspaceInvoker ?? CurrentWorkspaceFocus() ?? _simpleWorkspaceLibrary;
            _libraryPane.Visible=!_libraryPane.Visible;
            if(_libraryPane.Visible)RunFighterAction(()=>RefreshLibrary(LibrarySelection?.Clip.Id??SelectedFighterAsset()?.Retarget?.Library?.ClipId));else{CancelLibraryAutoPreview();++_librarySearchGeneration;CancelLibraryTreeWork();ReleaseLibraryPreview();}
            modelEditControl1.BringToFront();PerformLayout();
            UpdateSimpleWorkspace();
            if (opening) FocusWorkspaceTarget(_librarySearch);
            else RestoreWorkspaceFocus(_workspaceLibraryOpener);
        }
        private void BuildLibraryPane()
        {
            _sharedSourceLibrary=_sharedSourceLibrary??_library??new AnimationLibrary();
            _library=_library??_sharedSourceLibrary;
            _libraryPane=new Panel{Name="animationLibraryPane",Dock=DockStyle.Right,Width=370,Visible=false,Padding=new Padding(6),BorderStyle=BorderStyle.FixedSingle};
            var layout=new TableLayoutPanel{Name="libraryCatalogueLayout",Dock=DockStyle.Fill,ColumnCount=1,RowCount=9};
            _sourceCatalogueLayout=layout;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(int h in new[]{60,30,0,24,70,66,22,160,38})layout.RowStyles.Add(h==0?new RowStyle(SizeType.Percent,100):new RowStyle(SizeType.Absolute,h));
            var header=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};
            _sourceModeLabel=new Label{Name="sourceModeTitle",Text="Shared animation library",AutoSize=false,Width=330,Height=24};
            header.Controls.Add(_sourceModeLabel);
            header.Controls.Add(LibraryButton("libraryLoadFiles","Load files...",LoadSessionSource));
            header.Controls.Add(LibraryButton("librarySharedSources","Library",UseSharedSourceLibrary));
            _sourceAddButton=LibraryButton("libraryAddSource","Add source...",()=>{
                if(ReferenceEquals(_library,_sessionSourceLibrary))AddSessionSourceToLibrary();else AddLibrarySource();
            });
            header.Controls.Add(LibraryButton("libraryAddBrawlDisc","Add game...",ShowBrawlDiscLibrary));
            _libraryUndo=LibraryButton("libraryUndo","Undo Source",()=>{ReleaseLibraryPreview();_library.UndoLastUpdate();RefreshLibrary();});
            header.Controls.Add(LibraryButton("libraryClose","Close",ToggleAnimationLibrary));layout.Controls.Add(header,0,0);
            _librarySearch=new TextBox{Name="librarySearch",AccessibleName="Search Animation Library",Dock=DockStyle.Fill,MaxLength=512};
            _librarySearchTimer=new System.Windows.Forms.Timer{Interval=300};
            _librarySearch.TextChanged+=(s,e)=>QueueLibrarySearch();
            _librarySearchTimer.Tick+=(s,e)=>{_librarySearchTimer.Stop();RunFighterAction(()=>RefreshLibrary());};
            var searchRow=new Panel{Dock=DockStyle.Fill};searchRow.Controls.Add(_librarySearch);
            var clear=LibraryButton("libraryClearSearch","Clear",()=>{if(_librarySearch.Text.Length>0)_librarySearch.Clear();else RefreshLibrary();});clear.Dock=DockStyle.Right;clear.AutoSize=false;clear.Width=49;searchRow.Controls.Add(clear);
            searchRow.Controls.Add(new Label{Text="Search",Dock=DockStyle.Left,Width=49,Padding=new Padding(0,4,0,0)});_librarySearch.BringToFront();layout.Controls.Add(searchRow,0,1);
            _libraryList=new TreeView{Name="libraryClips",AccessibleName="Animation Library by game",Dock=DockStyle.Fill,HideSelection=false,ShowLines=true,ShowNodeToolTips=true};
            _libraryPopulateTimer=new System.Windows.Forms.Timer{Interval=8};
            _libraryPopulateTimer.Tick+=(s,e)=>RunFighterAction(ProcessLibraryTreeWork);
            _libraryList.BeforeExpand+=(s,e)=>{if(_libraryReplacingTree&&!_libraryApplyingExpansion){e.Cancel=true;return;}PopulateLibraryGroup(e.Node);};
            _libraryList.AfterSelect+=(s,e)=>{if(_libraryUpdatingTree)return;if(e.Node?.Name=="libraryMore"&&e.Node.Parent!=null){var parent=e.Node.Parent;PopulateLibraryGroup(parent,true);_libraryList.SelectedNode=parent;}LibraryBrowseSelectionChanged();};
            _libraryList.AfterCollapse+=(s,e)=>{if(_libraryUpdatingTree||e.Node==null)return;if(e.Node.Level==0)_libraryCollapsedGames.Add(e.Node.Name);else if(e.Node.Level==1){_libraryCollapsedCharacters.Add(e.Node.Name);_libraryExpandedCharacters.Remove(e.Node.Name);}else if(e.Node.Level==2){_libraryCollapsedModels.Add(e.Node.Name);_libraryExpandedModels.Remove(e.Node.Name);}};
            _libraryList.AfterExpand+=(s,e)=>{if(_libraryUpdatingTree||e.Node==null)return;if(e.Node.Level==0)_libraryCollapsedGames.Remove(e.Node.Name);else if(e.Node.Level==1){_libraryCollapsedCharacters.Remove(e.Node.Name);_libraryExpandedCharacters.Add(e.Node.Name);}else if(e.Node.Level==2){_libraryCollapsedModels.Remove(e.Node.Name);_libraryExpandedModels.Add(e.Node.Name);}};layout.Controls.Add(_libraryList,0,2);
            var pages=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
            _libraryPage=new Label{Name="libraryPage",AutoSize=true,Padding=new Padding(5,7,0,0)};
            pages.Controls.Add(_libraryPage);layout.Controls.Add(pages,0,3);
            _libraryDetails=new TextBox{Name="libraryDetails",Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill};layout.Controls.Add(_libraryDetails,0,4);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};
            _libraryPreview=LibraryButton("libraryPreviewOriginal","Load as source",PreviewLibraryOriginal);
            _libraryPreview.AccessibleDescription="Refresh and frame the selected original source in the small read-only preview. Selection previews automatically. Use on This Model retargets this source to the editable main model.";
            _libraryOpenTarget=LibraryButton("libraryOpenTarget","Open as target",OpenLibraryAsTarget);
            _libraryOpenTarget.AccessibleDescription="Open the selected model, costume and animation in the large editable main preview, with the existing unsaved-change handling.";
            _libraryUse=LibraryButton("libraryUseOnTarget","Use on This Model",UseLibraryOnTarget);
            _libraryRepair=LibraryButton("libraryRepairSource","Repair...",RepairLibrarySource);
            actions.Controls.AddRange(new Control[]{_libraryPreview,_libraryOpenTarget,_libraryUse,_libraryRepair,LibraryButton("libraryBatch","Batch / Fit...",ShowLibraryBatch),LibraryButton("libraryAdvancedFitting","Advanced Fitting...",OpenAdvancedSceneFitting),_sourceAddButton,_libraryUndo});layout.Controls.Add(actions,0,5);
            _libraryOriginalLabel=new Label{Name="libraryOriginalLabel",Text="Original source preview (read-only)",Dock=DockStyle.Fill,AutoEllipsis=true};layout.Controls.Add(_libraryOriginalLabel,0,6);
            _libraryViewport=new ModelPanel{Name="libraryOriginalViewport",Dock=DockStyle.Fill,Margin=Padding.Empty,
                AccessibleName="Original source preview",AccessibleDescription="Read-only original source model. Selecting a character, animation or costume automatically previews its original source here. Open as target explicitly replaces the editable main model."};
            _libraryViewport.CurrentViewport.AllowSelection=false;
            _libraryViewport.CurrentViewport.RenderFloor=true;
            _libraryViewport.CurrentViewport.BackgroundColor=Color.FromArgb(35,39,46);
            layout.Controls.Add(_libraryViewport,0,7);
            var transport=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};transport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,65));transport.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            _libraryPlay=LibraryButton("libraryOriginalPlay","Play",()=>{
                if(_libraryLease==null)return;
                if(_libraryTimer.Enabled){_libraryTimer.Stop();_libraryPlay.Text="Play";}else{modelEditControl1.StopAnim();_libraryClock.Restart();_libraryTimer.Start();_libraryPlay.Text="Pause";}
            });
            _libraryFrame=new TrackBar{Name="libraryOriginalFrame",AccessibleName="Original source frame",Dock=DockStyle.Fill,Minimum=1,Maximum=1,TickStyle=TickStyle.None};
            _libraryFrame.ValueChanged+=(s,e)=>{if(!_libraryScrubbing&&_libraryLease!=null){_libraryTime=_libraryFrame.Value-1;_libraryClock.Restart();RenderLibraryFrame();}};
            transport.Controls.Add(_libraryPlay,0,0);transport.Controls.Add(_libraryFrame,1,0);layout.Controls.Add(transport,0,8);
            _libraryTimer=new System.Windows.Forms.Timer{Interval=16};
            _libraryTimer.Tick+=(s,e)=>{
                if(_libraryLease==null)return;
                if(modelEditControl1.Playing){_libraryTimer.Stop();_libraryPlay.Text="Play";return;}
                _libraryTime+=_libraryClock.Elapsed.TotalSeconds*60;_libraryClock.Restart();
                int frames=_libraryLease.PreviewAnimation.FrameCount;
                if(_libraryTime>frames-1){if(_libraryLease.PreviewAnimation.Loop)_libraryTime%=frames;else{_libraryTime=frames-1;_libraryTimer.Stop();_libraryPlay.Text="Play";}}
                RenderLibraryFrame();
            };
            BuildLibraryFilters(layout,searchRow,actions);
            BuildNextLibraryPreviewHost(layout,transport);
            PrepareWorkspaceTabOrder(_libraryPane);
            layout.GetControlFromPosition(0,1).TabIndex = 0;
            _libraryList.TabIndex = 1;
            header.TabIndex = 2;
            _libraryPane.AccessibleDescription = "Search and select original source clips. Tab visits library actions. Escape closes this pane; F6 moves between regions.";(_simpleWorkspaceBody?.Controls??Controls).Add(_libraryPane);modelEditControl1.BringToFront();
            UpdateSourceSessionCommands();
            LayoutSimpleWorkspaceLibraryPreview();
        }
        private static string LibraryStateText(LibrarySourceState state)
        {
            switch(state){case LibrarySourceState.Available:return "Available";case LibrarySourceState.SourceChanged:return "Source changed";
                case LibrarySourceState.NeedsModelAssociation:return "Needs model association";case LibrarySourceState.UnsupportedChannelVersion:return "Unsupported channel/version";default:return "Missing";}
        }
        private void UpdateLibrarySelection()
        {
            long selectionStart=Stopwatch.GetTimestamp();
            _libraryLastTarget=modelEditControl1.TargetModel;
            var item=LibrarySelection;bool available=!_libraryReplacingTree&&!_librarySearchTimer.Enabled&&item?.State==LibrarySourceState.Available;
            UpdateLibraryCostumes(item);
            string previewKey=item==null?null:_library.DirectoryPath+"|"+item.Clip.Id+"|"+_librarySearchGeneration;
            if(previewKey!=_libraryPreviewStateKey)
            {_libraryPreviewStateKey=previewKey;_libraryPreviewState=item==null?LibrarySourceState.Missing:_library.GetPreviewState(item.Clip.Id);}
            bool preview=LibraryPreviewReady;
            _libraryPreview.Enabled=preview;_libraryUse.Enabled=available&&LibraryCostumeReady&&!SimpleWorkspaceSourceNeedsRepair(item)&&item?.Clip.Channel=="CHR0 skeletal"&&modelEditControl1.TargetModel is MDL0Node;
            _libraryOpenTarget.Enabled=preview&&!SimpleWorkspaceBusy;
            _libraryRepair.Enabled=SimpleWorkspaceSourceNeedsRepair(item)&&!_libraryReplacingTree&&!_librarySearchTimer.Enabled;
            _libraryDetails.Text=item==null?"One shared library across all fighters. Add an original model and its complete motion set.":
                item.Motion.Game+" · "+item.Motion.GameVersion+" · "+item.Clip.Channel+" v"+item.Clip.NativeVersion+Environment.NewLine+
                item.Clip.FrameCount+" ticks @ "+item.Clip.TicksPerSecond+" Hz · "+(item.Clip.Loop?"Loop":"No loop")+" · "+(preview&&item.Clip.Channel!="CHR0 skeletal"?"Original native preview; skeletal transfer unavailable":SimpleWorkspaceSourceStateText(item))+Environment.NewLine+
                "Original: "+(SelectedLibraryCostume?.Name??item.Model?.Name??"Not verified")+" · "+item.Clip.ResourceKey+Environment.NewLine+
                (item.Clip.AssociationEvidence??item.Clip.AssociationIssue)+Environment.NewLine+
                (item.Clip.UnboundSourceTracks==null?"":"Native tracks absent on original model (not evaluated): "+item.Clip.UnboundSourceTracks+Environment.NewLine)+"SHA256: "+item.Clip.ResourceSha256;
            UpdateLibraryFavorite();
            MeasureLibraryUi(7,selectionStart);
            UpdateSimpleWorkspace();
        }
        private void AddLibrarySource()
        {
            if(!ReferenceEquals(_library,_sharedSourceLibrary))UseSharedSourceLibrary();
            modelEditControl1.StopAnim();ReleaseLibraryPreview();
            using(var choose=new LibrarySourceDialog())
            {
                if(choose.ShowDialog(this)!=DialogResult.OK)return;
                LibraryImportPlan plan=InventoryLibrarySource(choose.Registration);
                if(plan==null)return;
                int added=_library.Register(plan);
                var data=_library.Read();
                var motion=data.Mounts.First(m=>string.Equals(m.LocalPath,plan.MotionMount.LocalPath,StringComparison.OrdinalIgnoreCase)&&
                    m.ContainerSha256==plan.MotionMount.ContainerSha256&&m.Game==plan.MotionMount.Game&&m.GameVersion==plan.MotionMount.GameVersion);
                var original=data.Models.First(m=>m.ResourceKey==plan.Model.ResourceKey&&m.ResourceSha256==plan.Model.ResourceSha256&&
                    data.Mounts.Any(a=>a.Id==m.MountId&&string.Equals(a.LocalPath,plan.ModelMount.LocalPath,StringComparison.OrdinalIgnoreCase)&&
                        a.ContainerSha256==plan.ModelMount.ContainerSha256&&a.Game==plan.ModelMount.Game&&a.GameVersion==plan.ModelMount.GameVersion));
                string selected=data.Clips.Where(c=>c.MountId==motion.Id&&plan.Clips.Any(p=>
                        p.ResourceKey==c.ResourceKey&&p.ResourceSha256==c.ResourceSha256&&p.ModelId==c.ModelId))
                    .OrderByDescending(c=>c.ModelId==original.Id).ThenByDescending(c=>c.Channel=="CHR0 skeletal")
                    .ThenBy(c=>c.Name,StringComparer.OrdinalIgnoreCase).Select(c=>c.Id).FirstOrDefault();
                _simpleWorkspaceSourceIssue=_simpleWorkspaceIssueClip=null;
                _librarySearchTimer.Stop();_librarySearch.Text="";_librarySearchTimer.Stop();RefreshLibrary(selected);
                _fighterStatus.Text="Shared library: "+added+" entries added. Original sources remain read-only.";
            }
        }
        private void IndexLibraryGameSource(IWin32Window owner,LibraryGameRequest request)
        {
            if(_libraryPane==null||!ReferenceEquals(_library,_sharedSourceLibrary))UseSharedSourceLibrary();
            modelEditControl1.StopAnim();ReleaseLibraryPreview();_librarySearchTimer.Stop();
            // Adding the source authorizes its complete native inventory. No
            // character picker or second document state is introduced.
            request.ApproveBuild=true;request.Characters=null;
            int added=0;
            var indexed=LibraryWork.Run(owner,"Adding Brawl Library",(cancel,progress)=>{
                var plan=BrawlGameSource.Prepare(request,Path.Combine(_library.DirectoryPath,"NativeCache"),cancel,(percent,message)=>progress.Report(message));
                cancel.ThrowIfCancellationRequested();progress.Report("Publishing the complete source catalogue...");added=_library.RegisterGame(plan,cancel);return plan;
            });
            if(indexed==null)return;
            var savedSource=_library.Read().GameLocations.Single(l=>l.Indexed&&l.Game==indexed.Source.Game&&l.GameVersion==indexed.Source.GameVersion&&
                string.Equals(l.Path,indexed.Source.Path,StringComparison.OrdinalIgnoreCase)&&l.InventorySha256==indexed.Source.InventorySha256&&l.ContainerSha256==indexed.Source.ContainerSha256&&l.Provider==indexed.Source.Provider);
            OpenSavedGameLibrary(_library,savedSource);
            _fighterStatus.Text="Added "+indexed.Mounts.Select(m=>m.VirtualPath.Split('/')).Where(p=>p.Length>=3).Select(p=>p[1]).Distinct().Count()+" characters · "+added+" native animation resources · "+indexed.Issues.Count+" source issues. Original game remains read-only.";
            if(indexed.Issues.Count>0)MessageBox.Show(owner,string.Join(Environment.NewLine,indexed.Issues.Take(12)),"Source inventory issues",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        private LibraryImportPlan InventoryLibrarySource(LibraryRegistration request)
        {
            LibraryImportPlan result=null;Exception error=null;bool complete=false;
            using(var cancel=new CancellationTokenSource())
            using(var dialog=new Form{Text="Indexing Source",ClientSize=new Size(440,110),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false})
            {
                var label=new Label{Text="Reading native source metadata...",Bounds=new Rectangle(14,12,410,25)};
                var progress=new ProgressBar{Style=ProgressBarStyle.Marquee,Bounds=new Rectangle(14,42,410,18)};
                var button=new Button{Text="Cancel",Bounds=new Rectangle(334,74,90,27)};button.Click+=(s,e)=>{cancel.Cancel();button.Enabled=false;};
                dialog.Controls.AddRange(new Control[]{label,progress,button});
                dialog.FormClosing+=(s,e)=>{if(!complete){e.Cancel=true;cancel.Cancel();}};
                var report=new Progress<string>(message=>{if(!dialog.IsDisposed)label.Text=message;});
                dialog.Shown+=(s,e)=>Task.Run(()=>BrawlLibrarySource.Prepare(request,cancel.Token,(percent,message)=>((IProgress<string>)report).Report(message)),cancel.Token)
                    .ContinueWith(task=>{complete=true;if(task.IsFaulted)error=task.Exception.GetBaseException();else if(!task.IsCanceled)result=task.Result;dialog.Close();},TaskScheduler.FromCurrentSynchronizationContext());
                dialog.ShowDialog(this);
            }
            if(error!=null&&!(error is OperationCanceledException))throw error;return result;
        }
        private void PreviewLibraryOriginal()
        {
            var item=LibrarySelection;if(item==null)return;
            CancelLibraryAutoPreview();PauseSourceOperation();
            string model=SelectedLibraryCostume?.ModelId??item.Model?.Id;
            if(_libraryLease!=null&&_library.TrySelectPreview(_libraryLease,item.Clip.Id,model)){PrepareLibraryPreviewView(true);return;}
            var source=LibraryWork.Run(this,"Loading original source",(cancel,progress)=>_library.OpenPreview(item.Clip.Id,model,cancel));
            if(source==null)return;ReleaseLibraryPreview();_libraryLease=source;PrepareLibraryPreviewView(true);
        }
        private void FrameLibraryOriginal()
        {
            var box=_libraryLease.Model.GetBox();var center=(box.Min+box.Max)*0.5f;var half=(box.Max-box.Min)*0.65f;
            // Frame this viewport in its own projection and final laid-out size.
            // A remembered orthographic view/zoom or an earlier zero-height
            // layout must not leave a successfully loaded source out of view.
            _libraryViewport.CurrentViewport.SetProjectionType(BrawlLib.OpenGL.ViewportProjection.Perspective);
            var camera=_libraryViewport.Camera;
            camera.SetProjectionParams(Math.Max(.1f,(float)_libraryViewport.ClientSize.Width/Math.Max(1,_libraryViewport.ClientSize.Height)),45,200000,1);
            float tangent=(float)Math.Tan(camera.VerticalFieldOfView*Math.PI/360);
            float aspect=Math.Max(0.1f,(float)_libraryViewport.Width/Math.Max(1,_libraryViewport.Height));
            float distance=Math.Max(half._y/tangent,half._x/(tangent*aspect))+half._z+Math.Max(1,camera.NearDepth*2);
            camera.Set(center+new BrawlLib.Internal.Vector3(0,0,distance),new BrawlLib.Internal.Vector3(),new BrawlLib.Internal.Vector3(1));
            _libraryViewport.CurrentViewport._viewDistance=distance;
        }
        private void RenderLibraryFrame()
        {
            if(_libraryLease==null)return;
            LibraryNativePreview.Apply(_libraryLease.Model,_libraryLease.PreviewBundle,(float)_libraryTime+1);
            _libraryScrubbing=true;try{_libraryFrame.Value=Math.Max(1,Math.Min(_libraryFrame.Maximum,(int)_libraryTime+1));}finally{_libraryScrubbing=false;}
            _libraryOriginalLabel.Text="Original: "+_libraryLease.Model.Name+" · "+_libraryLease.PreviewAnimation.Name+" · "+_libraryLease.PreviewAnimation.GetType().Name.Replace("Node","")+" · frame "+(_libraryTime+1).ToString("0.0")+" (read-only)";
            _libraryViewport.Invalidate();
        }
        private void ReleaseLibraryPreview()
        {
            _libraryTimer?.Stop();if(_libraryPlay!=null)_libraryPlay.Text="Play";
            if(_libraryLease==null)return;
            _libraryViewport.ClearAll();_libraryLease.Dispose();_libraryLease=null;
            _libraryOriginalLabel.Text="Original source preview (read-only)";
            LayoutSimpleWorkspaceLibraryPreview();
        }
        private bool _animationLibraryDisposed;
        protected override void Dispose(bool disposing)
        {
            if(disposing)DisposeAnimationLibrary();
            base.Dispose(disposing);
        }
        private void DisposeAnimationLibrary()
        {
            if(_animationLibraryDisposed)return;
            _animationLibraryDisposed=true;
            ResetBatchReview(); ResetLoopInspection();
            CloseLinkedComparison();
            CancelLibraryAutoPreview();_libraryAutoTimer?.Dispose();
            _librarySearchGeneration++;CancelLibraryTreeWork();
            ReleaseLibraryPreview();_libraryTimer?.Dispose();_librarySearchTimer?.Dispose();_libraryPopulateTimer?.Dispose();_libraryPendingGroups.Clear();
        }
        private void UseLibraryOnTarget()
        {
            var item=LibrarySelection;
            if(item==null||!(modelEditControl1.TargetModel is MDL0Node target))return;
            // A second 60 Hz OpenGL preview must not compete with the target.
            _libraryTimer.Stop();_libraryPlay.Text="Play";
            modelEditControl1.StopAnim();
            var loaded=LoadSelectedLibrarySource();if(loaded==null)return;
            using(var source=loaded)
            using(var result=BakeWithProgress(source.Model,target,source.Animation))
            {
                if(result==null)return;
                // Capture the current editable target bytes, including any unsaved native edits.
                var targetRoot=FighterRoot(_fighterProject.Model);
                if(targetRoot.IsDirty){targetRoot.Merge();targetRoot.ExportChecked(_fighterProject.Model.WorkingPath);}
                var asset=_fighterProject.CreateLibraryRevision(source,result);
                var root=FighterRoot(asset); // Validate native decoding before the atomic visible import.
                RememberFighterImport();_fighterProject.Animations.Add(asset);
                _fighterProject.SelectedAnimation=FighterAnimationKey(asset,root);_fighterProject.Frame=1;
                RebuildFighterScene();
                if(result.Contacts.FloorEnabled)modelEditControl1.QueueGroundContactView();else modelEditControl1.QueueFrameFighterModel();
                MarkFighterChanged();UpdateFighterStatus();
                _fighterStatus.Text="Added independent target revision: "+source.Animation.Name+" · Original source resolved and verified automatically.";
            }
        }
        private LibrarySourceLease LoadLibrarySource(string clipId)
        {
            return LibraryWork.Run(this,"Loading Verified Original Source",(cancel,progress)=>{
                cancel.ThrowIfCancellationRequested();var source=ResolveSourceCatalogue(clipId).Open(clipId,null,cancel);
                if(cancel.IsCancellationRequested){source.Dispose();cancel.ThrowIfCancellationRequested();}
                return source;
            });
        }
        private void RepairLibrarySource()
        {
            var item=LibrarySelection;if(item==null)return;
            if(!SimpleWorkspaceSourceNeedsRepair(item))return;
            modelEditControl1.StopAnim();ReleaseLibraryPreview();
            var data=_library.Read();
            var selectedModel=data.Models.SingleOrDefault(model=>model.Id==SelectedLibraryCostume?.ModelId);
            var ids=new[]{item.Clip.MountId,item.Model?.MountId,selectedModel?.MountId}.Where(id=>id!=null).Distinct().ToArray();
            var choices=ids.Where(id=>data.Mounts.Single(m=>m.Id==id).OriginPath==null).Select(id=>Tuple.Create(false,id,Path.GetFileName(data.Mounts.Single(m=>m.Id==id).LocalPath))).ToList();
            foreach(var sourceId in ids.Select(id=>data.Mounts.Single(m=>m.Id==id)).Where(m=>m.OriginPath!=null).Select(m=>m.GameSourceId).Distinct())
                choices.Add(Tuple.Create(true,sourceId,"Disc image · "+data.GameLocations.Single(l=>l.Id==sourceId).GameVersion));
            using(var chooser=new Form{Name="LibraryRepairDialog",Text="Repair Original Source",ClientSize=new Size(650,252),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false})
            {
                var list=new ComboBox{Name="libraryRepairMount",DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new Rectangle(14,44,622,26)};
                foreach(var choice in choices)list.Items.Add(choice.Item3);list.SelectedIndex=0;
                if(choices.Any(c=>c.Item1)&&ids.Any(id=>{var mount=data.Mounts.Single(m=>m.Id==id);return mount.OriginPath!=null&&!File.Exists(mount.OriginPath);}))list.SelectedIndex=choices.FindIndex(c=>c.Item1);
                var path=new TextBox{Name="libraryRepairPath",AccessibleName="Exact original file path",Bounds=new Rectangle(14,103,510,25)};
                var validation=new Label{Name="libraryRepairValidation",Text="Choose the same indexed bytes at their current location. A different model or build must be added as a new source.",Bounds=new Rectangle(14,139,622,54)};
                var apply=new Button{Name="libraryRepairApply",Text="Verify and repair",Bounds=new Rectangle(350,210,172,29)};
                var cancel=new Button{Name="libraryRepairCancel",Text="Cancel",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(536,210,100,29)};
                var browse=new Button{Name="libraryRepairBrowse",Text="Browse...",Bounds=new Rectangle(536,101,100,29)};
                browse.Click+=(s,e)=>{
                    var choice=choices[list.SelectedIndex];
                    using(var file=new OpenFileDialog{Title="Locate Exact Indexed Source",Filter=choice.Item1?"Disc images|*.iso;*.wbfs;*.rvz":"Native Brawl files|*.pac;*.pcs;*.brres;*.mdl0;*.chr0"})
                    {
                        if(file.ShowDialog(chooser)!=DialogResult.OK)return;
                        path.Text=file.FileName;
                    }
                };
                apply.Click+=(s,e)=>{
                    try
                    {
                        var choice=choices[list.SelectedIndex];
                        string replacement=Path.GetFullPath(path.Text.Trim().Trim('"'));
                        if(!File.Exists(replacement)){validation.Text="File not found. Paste the exact original file path or choose Browse.";return;}
                        apply.Enabled=browse.Enabled=list.Enabled=path.Enabled=false;
                        var repaired=LibraryWork.Run(chooser,"Verifying Original Source",(token,progress)=>{
                            token.ThrowIfCancellationRequested();
                            if(choice.Item1)_library.RepairGameSource(choice.Item2,replacement,token);
                            else _library.Repair(choice.Item2,replacement,token);
                            return replacement;
                        });
                        if(repaired==null)return;
                        _simpleWorkspaceSourceIssue=_simpleWorkspaceIssueClip=null;
                        RefreshLibrary(item.Clip.Id);chooser.DialogResult=DialogResult.OK;chooser.Close();
                    }
                    catch(Exception error){validation.Text=error.Message;}
                    finally{if(!chooser.IsDisposed)apply.Enabled=browse.Enabled=list.Enabled=path.Enabled=true;}
                };
                chooser.Controls.AddRange(new Control[]{new Label{Text="Repair the original source without changing its identity or model association.",Bounds=new Rectangle(14,12,622,28)},list,
                    new Label{Text="Exact original file path",Bounds=new Rectangle(14,80,622,20)},path,browse,validation,apply,cancel});
                chooser.AcceptButton=apply;chooser.CancelButton=cancel;DarkTheme.Apply(chooser);chooser.ShowDialog(this);
            }
        }
        private void RecordLibraryExport(ResourceNode node,string destination)
        {
            var asset=_fighterProject.Animations.FirstOrDefault(a=>a.Retarget?.Library!=null&&_fighterRoots.TryGetValue(a.Id,out var root)&&ReferenceEquals(root,node?.RootNode));
            if(asset==null)return;
            asset.Retarget.Library.LastExportSha256=FighterProject.HashFile(destination);
            asset.Retarget.Library.LastExportFormat=Path.GetExtension(destination).TrimStart('.').ToUpperInvariant();MarkFighterChanged();
        }
    }
}
