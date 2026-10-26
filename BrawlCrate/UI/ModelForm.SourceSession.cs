using BrawlCrate.Studio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private AnimationLibrary _sharedSourceLibrary, _sessionSourceLibrary;
        private LibraryRegistration _sessionSourceRegistration;
        private readonly List<AnimationLibrary> _sessionSourceCatalogues=new List<AnimationLibrary>();
        private readonly Dictionary<AnimationLibrary,SourceBrowserState> _sourceBrowserStates=new Dictionary<AnimationLibrary,SourceBrowserState>();
        private Label _sourceModeLabel;
        private Button _sourceAddButton;

        private sealed class SourceBrowserState
        {
            internal string Search,Game,Character,Channel,Selected;
            internal bool Favorites,Expanded;
            internal int Availability;
            internal string[] CollapsedGames,CollapsedCharacters,CollapsedModels,ExpandedCharacters,ExpandedModels;
        }

        private static LibraryRegistration CopySourceRegistration(LibraryRegistration source)=>new LibraryRegistration{
            ModelPath=source.ModelPath,ModelKey=source.ModelKey,MotionPath=source.MotionPath,Character=source.Character,
            Game=source.Game,GameVersion=source.GameVersion,BuildEvidence=source.BuildEvidence,ApproveAssociation=source.ApproveAssociation
        };

        private void LoadSessionSource()
        {
            if(SimpleWorkspaceBusy)throw new InvalidOperationException("Finish or cancel the current operation before loading source files.");
            PauseSourceOperation();
            using(var dialog=new LibrarySourceDialog())
            {
                dialog.Text="Load Source Files — This Session Only";
                var introduction=dialog.Controls.OfType<Label>().FirstOrDefault();
                if(introduction!=null)introduction.Text="Temporary source. Original files stay read-only; nothing is added to the shared library.";
                var accept=dialog.Controls.Find("libraryRegisterSource",true).OfType<Button>().Single();
                Action label=()=>{
                    string replacement=accept.Text=="Add reviewed motion set"?"Load reviewed motion set":accept.Text=="Index for review"?"Load for review":null;
                    if(replacement!=null)accept.Text=replacement;
                };
                accept.TextChanged+=(sender,args)=>label();label();
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                LoadSessionSource(dialog.Registration);
            }
        }

        // The same approved inventory path serves UI and background harnesses.
        // Only a fresh temporary catalogue is committed; persistent registration
        // remains a separate explicit command, even after a successful transfer.
        private string LoadSessionSource(LibraryRegistration request)
        {
            if(SimpleWorkspaceBusy)throw new InvalidOperationException("Finish or cancel the current operation before loading source files.");
            if(request==null)throw new ArgumentNullException(nameof(request));
            if(_libraryPane==null)BuildLibraryPane();
            PauseSourceOperation();
            var registration=CopySourceRegistration(request);
            var plan=InventoryLibrarySource(registration);
            if(plan==null)return null;
            var temporary=new AnimationLibrary(Path.Combine(_controlStage,"session-source-"+Guid.NewGuid().ToString("N")));
            temporary.Register(plan);
            string selected=RegisteredSourceClip(temporary,plan);
            _sessionSourceCatalogues.Add(temporary);
            _sessionSourceLibrary=temporary;_sessionSourceRegistration=registration;
            ActivateSourceCatalogue(temporary,selected);
            _fighterStatus.Text="Temporary source files loaded for this session. Choose a clip, Preview Original, then Use on This Model. Add to library is optional.";
            return selected;
        }

        private void UseSharedSourceLibrary()
        {
            if(SimpleWorkspaceBusy)throw new InvalidOperationException("Finish or cancel the current operation before changing source mode.");
            if(_libraryPane==null)BuildLibraryPane();
            _sharedSourceLibrary=_sharedSourceLibrary??new AnimationLibrary();
            ActivateSourceCatalogue(_sharedSourceLibrary);
        }

        private void AddSessionSourceToLibrary()
        {
            if(SimpleWorkspaceBusy)throw new InvalidOperationException("Finish or cancel the current operation before registering source files.");
            if(_sessionSourceRegistration==null||!ReferenceEquals(_library,_sessionSourceLibrary))
                throw new InvalidOperationException("Load temporary source files before choosing Add to library. Library sources are already registered.");
            PauseSourceOperation();
            // Freeze the exact temporary identities/hashes first. Repair may
            // have relocated either mount without changing its source identity;
            // use those verified current paths rather than the original request.
            var frozen=_sessionSourceLibrary.Read();
            var original=frozen.Models.Single(m=>m.ResourceKey==_sessionSourceRegistration.ModelKey);
            var modelMount=frozen.Mounts.Single(m=>m.Id==original.MountId);
            var motionMount=frozen.Mounts.Single(m=>original.MotionMountIds.Contains(m.Id));
            var motionResources=frozen.Clips.Where(c=>c.MountId==motionMount.Id)
                .OrderBy(c=>c.ResourceKey,StringComparer.Ordinal).ThenBy(c=>c.ResourceSha256,StringComparer.Ordinal)
                .Select(c=>Tuple.Create(c.ResourceKey,c.ResourceSha256)).ToArray();
            var registration=CopySourceRegistration(_sessionSourceRegistration);
            registration.ModelPath=modelMount.LocalPath;registration.MotionPath=motionMount.LocalPath;
            // Re-inventory and the existing Register locks independently verify
            // current source bytes. No selection/provenance/approval is rewritten.
            var plan=InventoryLibrarySource(registration);
            if(plan==null)return;
            if(plan.ModelMount.ContainerSha256!=modelMount.ContainerSha256||plan.MotionMount.ContainerSha256!=motionMount.ContainerSha256||
                plan.Model.ResourceSha256!=original.ResourceSha256||plan.Model.SkeletonSha256!=original.SkeletonSha256||
                !motionResources.SequenceEqual(plan.Clips.OrderBy(c=>c.ResourceKey,StringComparer.Ordinal).ThenBy(c=>c.ResourceSha256,StringComparer.Ordinal)
                    .Select(c=>Tuple.Create(c.ResourceKey,c.ResourceSha256))))
                throw new InvalidDataException("The temporary source files changed. Load and review the current source model and motion set before adding them to the shared library.");
            _sharedSourceLibrary=_sharedSourceLibrary??new AnimationLibrary();
            int added=_sharedSourceLibrary.Register(plan);
            _fighterStatus.Text=added==0?"This exact source model and motion set are already in the shared library. Temporary source selection is unchanged.":
                "Added "+added+" source entries to the shared library. Temporary source selection and the editable target are unchanged.";
        }

        private static string RegisteredSourceClip(AnimationLibrary catalogue,LibraryImportPlan plan)
        {
            var data=catalogue.Read();
            var motion=data.Mounts.First(m=>string.Equals(m.LocalPath,plan.MotionMount.LocalPath,StringComparison.OrdinalIgnoreCase)&&
                m.ContainerSha256==plan.MotionMount.ContainerSha256&&m.Game==plan.MotionMount.Game&&m.GameVersion==plan.MotionMount.GameVersion);
            var original=data.Models.First(m=>m.ResourceKey==plan.Model.ResourceKey&&m.ResourceSha256==plan.Model.ResourceSha256&&
                data.Mounts.Any(a=>a.Id==m.MountId&&string.Equals(a.LocalPath,plan.ModelMount.LocalPath,StringComparison.OrdinalIgnoreCase)&&
                    a.ContainerSha256==plan.ModelMount.ContainerSha256&&a.Game==plan.ModelMount.Game&&a.GameVersion==plan.ModelMount.GameVersion));
            return data.Clips.Where(c=>c.MountId==motion.Id&&plan.Clips.Any(p=>p.ResourceKey==c.ResourceKey&&p.ResourceSha256==c.ResourceSha256&&p.ModelId==c.ModelId))
                .OrderByDescending(c=>c.ModelId==original.Id).ThenByDescending(c=>c.Channel=="CHR0 skeletal")
                .ThenBy(c=>c.Name,StringComparer.OrdinalIgnoreCase).Select(c=>c.Id).FirstOrDefault();
        }

        private AnimationLibrary ResolveSourceCatalogue(string clipId)
        {
            if(string.IsNullOrWhiteSpace(clipId))throw new InvalidDataException("The exact recorded source clip identity is unavailable.");
            var candidates=new[]{_library,_sharedSourceLibrary}.Concat(_sessionSourceCatalogues).Where(c=>c!=null).Distinct();
            foreach(var candidate in candidates)
                if(candidate.Read().Clips.Any(c=>c.Id==clipId))return candidate;
            throw new InvalidDataException("The exact recorded source clip is unavailable. Temporary source catalogues last only for their owning session; native target editing remains available.");
        }

        private SourceBrowserState CaptureSourceBrowser()=>new SourceBrowserState{
            Search=_librarySearch.Text,Game=_libraryGameFilter.Text,Character=_libraryCharacterFilter.Text,
            Channel=_libraryChannelFilter.SelectedItem as string,Favorites=_libraryFavoritesOnly.Checked,
            Availability=_libraryAvailabilityFilter.SelectedIndex,Expanded=_libraryFiltersExpanded,Selected=LibrarySelection?.Clip.Id,
            CollapsedGames=_libraryCollapsedGames.ToArray(),CollapsedCharacters=_libraryCollapsedCharacters.ToArray(),
            CollapsedModels=_libraryCollapsedModels.ToArray(),ExpandedCharacters=_libraryExpandedCharacters.ToArray(),ExpandedModels=_libraryExpandedModels.ToArray()
        };

        private void RestoreSourceBrowser(SourceBrowserState state)
        {
            state=state??new SourceBrowserState();
            _libraryUpdatingFilters=true;
            try
            {
                _librarySearch.Text=state.Search??"";_libraryGameFilter.Text=state.Game??"";_libraryCharacterFilter.Text=state.Character??"";
                _libraryFavoritesOnly.Checked=state.Favorites;
                _libraryAvailabilityFilter.SelectedIndex=Math.Max(0,Math.Min(_libraryAvailabilityFilter.Items.Count-1,state.Availability));
                string channel=state.Channel??"All channels";
                if(!_libraryChannelFilter.Items.Contains(channel))_libraryChannelFilter.Items.Add(channel);
                _libraryChannelFilter.SelectedItem=channel;
                _libraryFiltersExpanded=state.Expanded;_libraryFilters.Visible=state.Expanded;
                void Restore(HashSet<string> target,string[] values){target.Clear();foreach(string value in values??new string[0])target.Add(value);}
                Restore(_libraryCollapsedGames,state.CollapsedGames);Restore(_libraryCollapsedCharacters,state.CollapsedCharacters);
                Restore(_libraryCollapsedModels,state.CollapsedModels);Restore(_libraryExpandedCharacters,state.ExpandedCharacters);Restore(_libraryExpandedModels,state.ExpandedModels);
            }
            finally{_libraryUpdatingFilters=false;}
            UpdateLibraryFilterDisclosure();
        }

        private void ActivateSourceCatalogue(AnimationLibrary catalogue,string selected=null)
        {
            bool switching=!ReferenceEquals(_library,catalogue);
            if(switching)
            {
                if(_library!=null)_sourceBrowserStates[_library]=CaptureSourceBrowser();
                modelEditControl1.StopAnim();ReleaseLibraryPreview();
                ++_librarySearchGeneration;_librarySearchTimer.Stop();CancelLibraryTreeWork();_libraryReplacingTree=true;
                _libraryUpdatingTree=true;try{_libraryList.SelectedNode=null;}finally{_libraryUpdatingTree=false;}
                _library=catalogue;
                _sourceBrowserStates.TryGetValue(catalogue,out var prior);RestoreSourceBrowser(prior);
                selected=selected??prior?.Selected;
            }
            if(!_libraryPane.Visible)_workspaceLibraryOpener=_workspaceInvoker??CurrentWorkspaceFocus()??_simpleWorkspaceLibrary;
            HideBatchReview();HideLoopInspection();if(_comparisonPane!=null)_comparisonPane.Visible=false;
            _libraryPane.Visible=true;
            UpdateSourceSessionCommands();
            RefreshLibrary(selected);UpdateSimpleWorkspace();FocusWorkspaceTarget(_librarySearch);
        }

        private void UpdateSourceSessionCommands()
        {
            bool temporary=ReferenceEquals(_library,_sessionSourceLibrary)&&_sessionSourceLibrary!=null;
            if(_sourceModeLabel!=null)_sourceModeLabel.Text=temporary?"Source files · temporary session":"Shared animation library";
            if(_sourceAddButton!=null)
            {
                string caption=temporary?"Add to library":"Add source...";
                if(_sourceAddButton.Text!=caption){_sourceAddButton.Text=caption;_workspaceLibraryWidth=-1;}
            }
            if(_libraryPane!=null)_libraryPane.AccessibleName=temporary?"Temporary source files":"Shared animation library";
        }

        private void PauseSourceOperation()
        {
            modelEditControl1.StopAnim();_libraryTimer?.Stop();if(_libraryPlay!=null)_libraryPlay.Text="Play";
        }
    }
}
