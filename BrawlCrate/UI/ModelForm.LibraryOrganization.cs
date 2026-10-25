using BrawlCrate.Studio;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private TextBox _libraryGameFilter, _libraryCharacterFilter;
        private ComboBox _libraryAvailabilityFilter, _libraryChannelFilter;
        private CheckBox _libraryFavoritesOnly, _libraryFavorite;
        private TableLayoutPanel _libraryQueryStack, _libraryFilters;
        private FlowLayoutPanel _libraryFilterActions;
        private Button _libraryFilterToggle;
        private bool _libraryFiltersExpanded;
        private bool _libraryUpdatingFilters, _libraryUpdatingFavorite;

        private LibraryQuery CurrentLibraryQuery() => new LibraryQuery {
            Text = _librarySearch.Text, GameBuildText = _libraryGameFilter.Text,
            CharacterModelText = _libraryCharacterFilter.Text, FavoritesOnly = _libraryFavoritesOnly.Checked,
            Availability = _libraryAvailabilityFilter.SelectedIndex <= 0 ? null : (LibrarySourceState?)(_libraryAvailabilityFilter.SelectedIndex - 1),
            Channel = _libraryChannelFilter.SelectedIndex <= 0 ? null : _libraryChannelFilter.SelectedItem as string
        };

        private void QueueLibrarySearch()
        {
            if (_libraryUpdatingFilters) return;
            UpdateLibraryFilterDisclosure();
            ++_librarySearchGeneration; CancelLibraryTreeWork(); _libraryReplacingTree = true;
            _librarySearchTimer.Stop(); _librarySearchTimer.Start();
            if (_libraryUse != null) _libraryUse.Enabled = false;
            if (_libraryPreview != null) _libraryPreview.Enabled = false;
            if (_libraryRepair != null) _libraryRepair.Enabled = false;
            if (_libraryFavorite != null) _libraryFavorite.Enabled = false;
            UpdateSimpleWorkspace();
        }

        private void BuildLibraryFilters(TableLayoutPanel layout, Panel searchRow, FlowLayoutPanel actions)
        {
            searchRow.Name = "librarySearchRow";
            _libraryQueryStack = new TableLayoutPanel { Name = "libraryQueryStack", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            _libraryQueryStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _libraryQueryStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            _libraryQueryStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            _libraryQueryStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            layout.Controls.Remove(searchRow); _libraryQueryStack.Controls.Add(searchRow, 0, 0);
            _libraryFilters = new TableLayoutPanel { Name = "libraryFilters", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Visible = false };
            _libraryFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            _libraryFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int row = 0; row < 4; row++) _libraryFilters.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            _libraryGameFilter = new TextBox { Name = "libraryGameFilter", Dock = DockStyle.Fill, MaxLength = 512, AccessibleName = "Filter game and build",
                AccessibleDescription = "Combine words from the game and build. This combines with all other filters and search." };
            _libraryCharacterFilter = new TextBox { Name = "libraryCharacterFilter", Dock = DockStyle.Fill, MaxLength = 512, AccessibleName = "Filter character and model",
                AccessibleDescription = "Combine words from the original character and model names." };
            _libraryAvailabilityFilter = new ComboBox { Name = "libraryAvailabilityFilter", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Filter source availability" };
            _libraryAvailabilityFilter.Items.Add("All availability");
            foreach (LibrarySourceState state in Enum.GetValues(typeof(LibrarySourceState))) _libraryAvailabilityFilter.Items.Add(LibraryStateText(state));
            _libraryAvailabilityFilter.SelectedIndex = 0;
            _libraryChannelFilter = new ComboBox { Name = "libraryChannelFilter", Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Filter animation channel" };
            _libraryChannelFilter.Items.Add("All channels"); _libraryChannelFilter.SelectedIndex = 0;
            Control[] fields = { _libraryGameFilter, _libraryCharacterFilter, _libraryAvailabilityFilter, _libraryChannelFilter };
            string[] labels = { "Game / build", "Character / model", "Availability", "Channel" };
            for (int row = 0; row < fields.Length; row++)
            {
                _libraryFilters.Controls.Add(new Label { Text = labels[row], Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false }, 0, row);
                _libraryFilters.Controls.Add(fields[row], 1, row);
            }
            _libraryFilterActions = new FlowLayoutPanel { Name = "libraryFilterActions", Dock = DockStyle.Fill, WrapContents = true, Margin = Padding.Empty };
            _libraryFilterToggle = LibraryButton("libraryToggleFilters", "Filters", ToggleLibraryFilters);
            _libraryFilterToggle.AccessibleName = "Show library filters";
            _libraryFilterActions.Controls.Add(_libraryFilterToggle);
            _libraryFavoritesOnly = new CheckBox { Name = "libraryFavoritesOnly", Text = "Favorites only", AutoSize = true,
                AccessibleDescription = "Show only exact clip identities marked as favorites in this library." };
            _libraryFilterActions.Controls.Add(_libraryFavoritesOnly);
            _libraryFilterActions.Controls.Add(LibraryButton("libraryClearFilters", "Clear filters", ClearLibraryFilters));
            _libraryQueryStack.Controls.Add(_libraryFilterActions, 0, 1);
            _libraryQueryStack.Controls.Add(_libraryFilters, 0, 2); layout.Controls.Add(_libraryQueryStack, 0, 1);
            _libraryGameFilter.TextChanged += (s, e) => QueueLibrarySearch();
            _libraryCharacterFilter.TextChanged += (s, e) => QueueLibrarySearch();
            _libraryAvailabilityFilter.SelectedIndexChanged += (s, e) => QueueLibrarySearch();
            _libraryChannelFilter.SelectedIndexChanged += (s, e) => QueueLibrarySearch();
            _libraryFavoritesOnly.CheckedChanged += (s, e) => QueueLibrarySearch();
            _libraryFavorite = new CheckBox { Name = "libraryFavorite", Text = "Favorite", Appearance = Appearance.Button, AutoSize = true, Enabled = false,
                AccessibleName = "Favorite selected source", AccessibleDescription = "Toggle this exact library clip's favorite preference; source association and target stay unchanged." };
            _libraryFavorite.CheckedChanged += (s, e) => {
                if (_libraryUpdatingFavorite) return;
                var item = LibrarySelection;
                if (item == null || _libraryReplacingTree || _librarySearchTimer.Enabled || SimpleWorkspaceBusy) { UpdateLibrarySelection(); return; }
                RunFighterAction(()=>{ _library.SetFavorite(item.Clip.Id, _libraryFavorite.Checked); RefreshLibrary(item.Clip.Id); });
            };
            actions.Controls.Add(_libraryFavorite);
            var tips = new ToolTip(); _libraryPane.Disposed += (s, e) => tips.Dispose();
            foreach (var field in fields) tips.SetToolTip(field, field.AccessibleDescription ?? field.AccessibleName);
            tips.SetToolTip(_libraryFavoritesOnly, _libraryFavoritesOnly.AccessibleDescription);
            tips.SetToolTip(_libraryFavorite, _libraryFavorite.AccessibleDescription);
            UpdateLibraryFilterDisclosure();
        }

        private void ToggleLibraryFilters()
        {
            if (SimpleWorkspaceBusy) return;
            _libraryFiltersExpanded = !_libraryFiltersExpanded;
            _libraryFilters.Visible = _libraryFiltersExpanded;
            UpdateLibraryFilterDisclosure();
            LayoutSimpleWorkspace();
            FocusWorkspaceTarget(_libraryFilterToggle);
        }

        private void UpdateLibraryFilterDisclosure()
        {
            if (_libraryFilterToggle == null) return;
            int active = (string.IsNullOrWhiteSpace(_libraryGameFilter.Text) ? 0 : 1) +
                (string.IsNullOrWhiteSpace(_libraryCharacterFilter.Text) ? 0 : 1) +
                (_libraryAvailabilityFilter.SelectedIndex <= 0 ? 0 : 1) + (_libraryChannelFilter.SelectedIndex <= 0 ? 0 : 1);
            string caption = (_libraryFiltersExpanded ? "Hide filters" : "Filters") + (active == 0 ? "" : " · " + active + " active");
            bool resized = _libraryFilterToggle.Text != caption;
            _libraryFilterToggle.Text = caption;
            _libraryFilterToggle.AccessibleName = (_libraryFiltersExpanded ? "Hide" : "Show") + " library filters";
            _libraryFilterToggle.AccessibleDescription = active + " advanced filters active. Hiding filters retains their values and the current query. Favorites and Clear filters remain available.";
            if (resized) SizeLibraryFilters(WorkspaceAppearance.Font.Height);
        }

        private void ClearLibraryFilters()
        {
            _libraryUpdatingFilters = true;
            try {
                _librarySearch.Clear(); _libraryGameFilter.Clear(); _libraryCharacterFilter.Clear();
                _libraryFavoritesOnly.Checked = false; _libraryAvailabilityFilter.SelectedIndex = 0; _libraryChannelFilter.SelectedIndex = 0;
            }
            finally { _libraryUpdatingFilters = false; }
            QueueLibrarySearch(); FocusWorkspaceTarget(_librarySearch);
        }

        private void UpdateLibraryChannels(string[] channels)
        {
            var expected = new[] { "All channels" }.Concat(channels).ToArray();
            if (_libraryChannelFilter.Items.Cast<string>().SequenceEqual(expected)) return;
            string selected = _libraryChannelFilter.SelectedItem as string;
            _libraryUpdatingFilters = true;
            try {
                _libraryChannelFilter.Items.Clear(); _libraryChannelFilter.Items.AddRange(expected);
                if (selected != null && !expected.Contains(selected)) _libraryChannelFilter.Items.Add(selected);
                _libraryChannelFilter.SelectedItem = selected ?? "All channels";
            }
            finally { _libraryUpdatingFilters = false; }
        }

        private void UpdateLibraryFavorite()
        {
            if (_libraryFavorite == null) return;
            _libraryUpdatingFavorite = true;
            try {
                var selected = LibrarySelection;
                _libraryFavorite.Checked = selected != null && _library.IsFavorite(selected.Clip.Id);
                _libraryFavorite.Text = _libraryFavorite.Checked ? "Favorited" : "Favorite";
                _libraryFavorite.Enabled = selected != null && !_libraryReplacingTree && !_librarySearchTimer.Enabled && !SimpleWorkspaceBusy;
                if (_library.FavoritePreferenceError != null) _libraryDetails.AppendText(Environment.NewLine + _library.FavoritePreferenceError);
            }
            finally { _libraryUpdatingFavorite = false; }
        }

        private void SizeLibraryFilters(int line)
        {
            int rowHeight = Math.Max(line + 12, WorkspaceAppearance.Pixels(32));
            _libraryQueryStack.RowStyles[0].Height = rowHeight;
            for (int row = 0; row < 4; row++) _libraryFilters.RowStyles[row].Height = rowHeight;
            foreach (Button button in _libraryFilterActions.Controls.OfType<Button>())
            {
                button.AutoSize=false;
                button.Size=new Size(TextRenderer.MeasureText(button.Text,button.Font).Width+WorkspaceAppearance.Pixels(20),Math.Max(line+12,WorkspaceAppearance.Pixels(30)));
            }
            int available = Math.Max(1, _libraryPane.ClientSize.Width - _libraryPane.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 8);
            int rows = 1, used = 0, actionHeight = 0;
            foreach (Control control in _libraryFilterActions.Controls)
            {
                int itemWidth = control.Width + control.Margin.Horizontal;
                if (used > 0 && used + itemWidth > available) { rows++; used = 0; }
                used += itemWidth; actionHeight = Math.Max(actionHeight, control.Height + control.Margin.Vertical);
            }
            actionHeight = rows * actionHeight + 4;
            _libraryQueryStack.RowStyles[1].Height = actionHeight;
            _libraryQueryStack.RowStyles[2].Height = _libraryFiltersExpanded ? rowHeight * 4 + 8 : 0;
            _libraryFilters.ColumnStyles[0].Width = TextRenderer.MeasureText("Character / model", _libraryPane.Font).Width + WorkspaceAppearance.Pixels(8);
            var layout = (TableLayoutPanel)_libraryQueryStack.Parent;
            layout.RowStyles[1].Height = rowHeight + actionHeight + (_libraryFiltersExpanded ? rowHeight * 4 + 8 : 0);
        }
    }
}
