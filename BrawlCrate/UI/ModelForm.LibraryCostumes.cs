using BrawlCrate.Studio;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private ComboBox _libraryCostume;
        private TableLayoutPanel _libraryCostumeRow;
        private Button _libraryOpenTarget;
        private bool _libraryUpdatingCostume;
        private AnimationLibrary _libraryCostumeCatalogue;
        private string _libraryCostumeClip;
        private readonly Dictionary<string, string> _libraryCostumeChoices = new Dictionary<string, string>();

        private LibraryCostumeOption SelectedLibraryCostume => _libraryCostume?.SelectedItem as LibraryCostumeOption;
        private bool LibraryCostumeReady => _libraryCostume?.Items.Count == 0 || _libraryCostume == null || SelectedLibraryCostume?.Available == true;
        private string LibraryCostumeKey(LibrarySearchItem item) => _library.DirectoryPath + "|" +
            (!string.IsNullOrEmpty(item.Motion.GameSourceId)&&item.Motion.Provider==BrawlLibrarySource.Provider?
                item.Motion.GameSourceId+"|"+string.Join("/",(item.Motion.VirtualPath??"").Split('/').Take(2)):item.Clip.Id);

        private Control BuildLibraryCostumeRow()
        {
            _libraryCostumeRow = new TableLayoutPanel { Name = "libraryCostumeRow", Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Visible = false };
            _libraryCostumeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _libraryCostumeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _libraryCostumeRow.Controls.Add(new Label { Text = "Costume", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _libraryCostume = new ComboBox { Name = "libraryCostume", Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Original Brawl costume",
                AccessibleDescription = "Choose an exact compatible original Brawl model. Preview, open as target and retarget use this costume. Different binds require review; no skeleton is converted." };
            _libraryCostumeRow.Controls.Add(_libraryCostume, 1, 0);
            _libraryCostume.SelectedIndexChanged += (sender, args) =>
            {
                if (_libraryUpdatingCostume || LibrarySelection == null) return;
                if (SimpleWorkspaceBusy) { UpdateLibrarySelection(); return; }
                _libraryTargetPreview = null;
                _simpleWorkspaceSourceIssue = _simpleWorkspaceIssueClip = null;
                if (SelectedLibraryCostume != null)
                {
                    if (_libraryCostumeChoices.Count >= 256) _libraryCostumeChoices.Clear();
                    _libraryCostumeChoices[LibraryCostumeKey(LibrarySelection)] = SelectedLibraryCostume.ModelId;
                }
                // Browser choices never redirect a recorded comparison or
                // decode source data. An explicit Preview/Use loads the model.
                ReleaseLibraryPreview();
                UpdateLibrarySelection();
                if (SelectedLibraryCostume?.Available == false) _fighterStatus.Text = SelectedLibraryCostume.UnavailableReason;
            };
            return _libraryCostumeRow;
        }

        private void UpdateLibraryCostumes(LibrarySearchItem item)
        {
            if (_libraryCostume == null) return;
            bool stable = item != null && !_libraryReplacingTree && _librarySearchTimer?.Enabled != true;
            if (stable && _libraryTargetPreview != null && _libraryTargetPreview.ClipId != item.Clip.Id) _libraryTargetPreview = null;
            if (stable && ReferenceEquals(_libraryCostumeCatalogue, _library) && _libraryCostumeClip == item.Clip.Id) return;
            _libraryUpdatingCostume = true;
            try
            {
                _libraryCostume.Items.Clear();
                _libraryCostumeCatalogue = stable ? _library : null; _libraryCostumeClip = stable ? item.Clip.Id : null;
                if (stable)
                {
                    var options = _library.GetCostumes(item.Clip.Id);
                    _libraryCostume.Items.AddRange(options);
                    _libraryCostumeChoices.TryGetValue(LibraryCostumeKey(item), out string remembered);
                    var selected = options.FirstOrDefault(option => option.ModelId == remembered) ?? options.FirstOrDefault(option => option.IsCanonical);
                    if (selected != null) _libraryCostume.SelectedItem = selected;
                }
                _libraryCostumeRow.Visible = _libraryCostume.Items.Count > 0;
            }
            finally { _libraryUpdatingCostume = false; }
        }

        private void SelectLibraryCostume(string modelId)
        {
            UpdateLibraryCostumes(LibrarySelection);
            var selected = _libraryCostume.Items.Cast<LibraryCostumeOption>().SingleOrDefault(option => option.ModelId == modelId);
            if (selected == null || !selected.Available)
                throw new InvalidDataException(selected?.UnavailableReason ?? "The exact recorded Brawl costume is unavailable. No replacement model was selected.");
            _libraryCostume.SelectedItem = selected;
        }

        private LibrarySourceLease LoadSelectedLibrarySource()
        {
            var item = LibrarySelection ?? throw new InvalidOperationException("Choose a source animation first.");
            var costume = SelectedLibraryCostume;
            if (costume?.Available == false) throw new InvalidDataException(costume.UnavailableReason);
            try
            {
                return LoadLibrarySource(new LibraryClipReference { ClipId = item.Clip.Id, ModelId = costume?.ModelId ?? item.Model?.Id });
            }
            catch (Exception error) when (error is IOException || error is InvalidOperationException || error is UnauthorizedAccessException)
            {
                _simpleWorkspaceSourceIssue = error.Message; _simpleWorkspaceIssueClip = item.Clip.Id;
                UpdateLibrarySelection();
                throw;
            }
        }

        private LibrarySourceLease LoadLibrarySource(LibraryClipReference reference)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            string clipId = reference.ClipId, modelId = reference.ModelId;
            return LibraryWork.Run(this, "Loading Verified Original Costume", (cancel, progress) =>
            {
                cancel.ThrowIfCancellationRequested();
                var source = ResolveSourceCatalogue(clipId).Open(clipId, modelId);
                if (cancel.IsCancellationRequested) { source.Dispose(); cancel.ThrowIfCancellationRequested(); }
                return source;
            });
        }

        private void ShowLibrarySourceSettings()
        {
            UseSharedSourceLibrary(); PauseSourceOperation(); ReleaseLibraryPreview();
            using (var dialog = new LibrarySourcesDialog(_library, IndexLibraryGameSource)) dialog.ShowDialog(this);
            RefreshLibrary();
        }
    }
}
