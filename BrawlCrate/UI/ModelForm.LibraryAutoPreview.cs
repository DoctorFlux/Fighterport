using BrawlCrate.Studio;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private LibrarySearchItem _libraryCharacterSelection;
        private System.Windows.Forms.Timer _libraryAutoTimer;
        private CancellationTokenSource _libraryAutoCancel;
        private readonly SemaphoreSlim _libraryAutoLoadGate=new SemaphoreSlim(1,1);
        private int _libraryAutoEpoch;
        private string _libraryPreviewStateKey;
        private LibrarySourceState _libraryPreviewState;
        private bool LibraryPreviewReady=>LibrarySelection!=null&&_libraryPreviewState==LibrarySourceState.Available&&LibraryCostumeReady&&
            !_libraryReplacingTree&&_librarySearchTimer?.Enabled!=true&&!SimpleWorkspaceSourceNeedsRepair(LibrarySelection);
        private bool LibraryCanOpenTarget=>
            LibraryPreviewReady&&(NativeGameAdapters.ForNative(LibrarySelection?.Motion.Provider)?.Operations.HasFlag(NativeGameOperation.TargetEdit)??false)||
            _libraryList?.SelectedNode?.Tag is Bt3BrowseChoice choice&&ResolveBt3Choice(choice)!=null&&!_libraryReplacingTree;
        private void LibraryBrowseSelectionChanged()
        {
            _libraryCharacterSelection=null;
            bool bt3=_libraryList.SelectedNode?.Tag is Bt3DiscInventory||_libraryList.SelectedNode?.Tag is Bt3BrowseChoice;
            if(bt3)
            {
                if(_libraryLease!=null)ReleaseLibraryPreview();
                if(_libraryList.SelectedNode?.Tag is Bt3DiscInventory)ReleaseBt3Preview();
            }
            else{ReleaseBt3Preview();if(_bt3Selectors!=null)_bt3Selectors.Visible=false;}
            if(_libraryList.SelectedNode?.Tag is LibraryGroup group)
                _libraryCharacterSelection=group.Items.Where(i=>i.Model!=null&&(i.Clip.Channel=="CHR0 skeletal"||i.Clip.Channel==MeleeGameSource.Channel)&&i.State==LibrarySourceState.Available)
                    .GroupBy(i=>i.Model.Id).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key,StringComparer.Ordinal).FirstOrDefault()?
                    .OrderByDescending(i=>i.Clip.Name=="Wait1"||i.Clip.Name=="Idle").ThenBy(i=>i.Clip.Name,StringComparer.Ordinal).FirstOrDefault()
                    ??group.Items.FirstOrDefault(i=>LibraryNativePreview.Supported(i.Clip));
            UpdateLibrarySelection();QueueLibraryAutoPreview();if(bt3)QueueBt3Preview();
        }
        private void CancelLibraryAutoPreview()
        {
            _libraryAutoEpoch++;_libraryAutoTimer?.Stop();_libraryAutoCancel?.Cancel();
        }
        private void QueueLibraryAutoPreview()
        {
            if(_libraryPane?.Visible!=true||_libraryReplacingTree||_librarySearchTimer?.Enabled==true)return;
            CancelLibraryAutoPreview();
            if(!LibraryPreviewReady)return;
            string model=SelectedLibraryCostume?.ModelId??LibrarySelection.Model?.Id;
            if(_libraryLease?.Reference.ClipId==LibrarySelection.Clip.Id&&_libraryLease.Reference.ModelId==model)return;
            PauseSourceOperation();
            try
            {
                if(_libraryLease!=null&&_library.TrySelectPreview(_libraryLease,LibrarySelection.Clip.Id,model))
                {PrepareLibraryPreviewView(false);return;}
            }
            catch(Exception error)
            {
                _simpleWorkspaceSourceIssue=error.Message;_simpleWorkspaceIssueClip=LibrarySelection.Clip.Id;
                _libraryOriginalLabel.Text="Source unavailable: "+error.Message;UpdateLibrarySelection();return;
            }
            _libraryOriginalLabel.Text="Loading selected original source...";
            _libraryViewport.ClearAll();
            if(_libraryAutoTimer==null)
            {_libraryAutoTimer=new System.Windows.Forms.Timer{Interval=120};_libraryAutoTimer.Tick+=(s,e)=>StartLibraryAutoPreview();}
            _libraryAutoTimer.Start();
        }
        private async void StartLibraryAutoPreview()
        {
            _libraryAutoTimer.Stop();
            if(IsDisposed||Disposing||!LibraryPreviewReady||_libraryPane?.Visible!=true)return;
            if(SimpleWorkspaceBusy||modelEditControl1.Playing||_libraryTimer?.Enabled==true){_libraryAutoTimer.Start();return;}
            var library=_library;string clip=LibrarySelection.Clip.Id,model=SelectedLibraryCostume?.ModelId??LibrarySelection.Model?.Id;
            int epoch=_libraryAutoEpoch;var cancel=new CancellationTokenSource();_libraryAutoCancel=cancel;
            LibrarySourceLease source=null;
            try
            {
                source=await Task.Run(async()=>
                {
                    await _libraryAutoLoadGate.WaitAsync(cancel.Token);
                    try{cancel.Token.ThrowIfCancellationRequested();return library.OpenPreview(clip,model,cancel.Token);}
                    finally{_libraryAutoLoadGate.Release();}
                });
                if(cancel.IsCancellationRequested||IsDisposed||Disposing||epoch!=_libraryAutoEpoch||!ReferenceEquals(library,_library)||
                    LibrarySelection?.Clip.Id!=clip||(SelectedLibraryCostume?.ModelId??LibrarySelection?.Model?.Id)!=model||_libraryPane.Visible!=true)return;
                ReleaseLibraryPreview();_libraryLease=source;source=null;PrepareLibraryPreviewView(true);
            }
            catch(OperationCanceledException){}
            catch(Exception error)
            {
                if(!IsDisposed&&epoch==_libraryAutoEpoch)
                {_simpleWorkspaceSourceIssue=error.Message;_simpleWorkspaceIssueClip=clip;_libraryOriginalLabel.Text="Source unavailable: "+error.Message;UpdateLibrarySelection();}
            }
            finally{source?.Dispose();if(ReferenceEquals(_libraryAutoCancel,cancel))_libraryAutoCancel=null;cancel.Dispose();}
        }
        private void PrepareLibraryPreviewView(bool reframe)
        {
            LayoutSimpleWorkspaceLibraryPreview();_libraryPane.PerformLayout();_sourcePreviewHost?.PerformLayout();
            _libraryViewport.CreateControl();_libraryViewport.Capture();_libraryLease.MeleeRenderer?.Prepare();
            _libraryTime=0;_libraryScrubbing=true;
            try{_libraryFrame.Maximum=_libraryLease.DisplayFrameCount;_libraryFrame.Value=1;}finally{_libraryScrubbing=false;}
            _libraryLease.Model.ResetToBindState();_libraryLease.Model.ApplySHP(null,0);
            _libraryViewport.ClearAll();_libraryViewport.AddReference(_libraryLease.Model.RootNode);_libraryViewport.AddTarget(_libraryLease.NativePreview?.RenderTarget??_libraryLease.Model);
            if(_libraryLease.NativePreview!=null)_libraryLease.NativePreview.ApplyDisplayFrame(1);
            if(reframe){if(_libraryLease.MeleeRenderer==null)_libraryViewport.PrepareModelTextures(_libraryLease.Model);FrameLibraryOriginal();}
            RenderLibraryFrame();_libraryViewport.Update();
            string bindings=LibraryNativePreview.Bindings(_libraryLease.Model,_libraryLease.PreviewBundle);
            _libraryDetails.Text+=Environment.NewLine+_libraryLease.PreviewNotice+" "+bindings;
            _libraryDetails.SelectionStart=0;_libraryDetails.SelectionLength=0;_libraryDetails.ScrollToCaret();
        }
    }
}
