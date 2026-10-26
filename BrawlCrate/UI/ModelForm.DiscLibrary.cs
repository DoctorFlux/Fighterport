using BrawlCrate.Studio;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private void ShowBrawlDiscLibrary()
        {
            if (SimpleWorkspaceBusy) throw new InvalidOperationException("Finish or cancel the current operation before adding a game source.");
            var restore = CaptureWorkspaceFocus();
            PauseSourceOperation();
            try
            {
                // Reading saved source choices does not register a path or
                // change the active temporary source catalogue on cancellation.
                var shared = _sharedSourceLibrary ?? new AnimationLibrary();
                using (var dialog = new GameSourceDialog(shared.Read().GameLocations.ToArray(),shared,ReleaseLibraryPreview))
                {
                    var result=dialog.ShowDialog(this);
                    if(dialog.SourcesChanged)RefreshSavedGameSourceUi(shared);
                    if (result != DialogResult.OK) return;
                    if(dialog.ExistingSourceId!=null)
                    {
                        OpenSavedGameLibrary(shared,shared.Read().GameLocations.Single(l=>l.Id==dialog.ExistingSourceId));
                        _fighterStatus.Text="Opened saved "+dialog.Request.Game+" library. Choose a character, animation and costume; no game extraction was repeated.";
                    }
                    else if(dialog.PathOnly)
                    {
                        shared.RememberGameSource(dialog.Request.Game,dialog.Request.GameVersion,dialog.Request.Path);
                        _sharedSourceLibrary=shared;
                        _fighterStatus.Text="Saved "+dialog.Request.Game+" source location for later use. This source has not been indexed.";
                        if(ReferenceEquals(_library,shared))RefreshLibrary();
                    }
                    else IndexLibraryGameSource(this, dialog.Request);
                }
            }
            finally { restore(); }
        }
        private void ShowSavedGameSourceSettings()
        {
            if(SimpleWorkspaceBusy)throw new InvalidOperationException("Finish the current operation before editing saved game sources.");
            var restore=CaptureWorkspaceFocus();PauseSourceOperation();ReleaseLibraryPreview();
            try
            {
                var shared=_sharedSourceLibrary??new AnimationLibrary();
                using(var window=new SavedGameSourcesDialog(shared))window.ShowDialog(this);
                RefreshSavedGameSourceUi(shared);
            }
            finally{restore();}
        }
        private void OpenSavedGameLibrary(AnimationLibrary shared,LibraryGameLocation source)
        {
            _sharedSourceLibrary=shared;UseSharedSourceLibrary();ClearLibraryFilters();
            _libraryGameSourceFilter=source.Id;_libraryGameSourceName="Library · "+source.Game+" · "+source.Label;
            UpdateSourceSessionCommands();_libraryGameFilter.Text=source.Game+" "+source.GameVersion;
            _librarySearchTimer.Stop();RefreshLibrary();
        }
        private void RefreshSavedGameSourceUi(AnimationLibrary shared)
        {
            _sharedSourceLibrary=shared;
            if(_libraryGameSourceFilter!=null)
            {
                var source=shared.Read().GameLocations.SingleOrDefault(l=>l.Id==_libraryGameSourceFilter);
                if(source==null)_libraryGameSourceFilter=_libraryGameSourceName=null;
                else _libraryGameSourceName="Library · "+source.Game+" · "+source.Label;
            }
            UpdateSourceSessionCommands();
            if(ReferenceEquals(_library,shared)){_librarySearchTimer?.Stop();RefreshLibrary();}
        }
    }
}
