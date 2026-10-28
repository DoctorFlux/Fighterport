using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    internal sealed class WorkspaceSetExportDialog : Form
    {
        private readonly TextBox _path;
        private readonly Label _status;
        private readonly Button _accept, _cancel, _browse;
        private readonly WorkspaceExportOptimizationOptions _optimization;
        private bool _running;
        private CancellationTokenSource _cancellation;
        internal Action<string> ValidateDestination;
        internal Func<string, CancellationToken, IProgress<string>, Task> Export;

        internal WorkspaceSetExportDialog(string project, string target, string targetIdentity, string targetHash, string[] clips)
        {
            Name = "workspaceSetExportDialog"; Text = "Export reviewed animation set";
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false;
            ClientSize = new Size(780, 630); FormBorderStyle = FormBorderStyle.FixedDialog;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 7 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            var heading = new Label { Name = "setExportSummary", AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false,
                Text = "Project: " + project + Environment.NewLine + "Target: " + target + Environment.NewLine +
                    clips.Length + " reviewed animations · native Brawl CHR0", Margin = new Padding(0, 0, 0, 12) };
            layout.Controls.Add(heading, 0, 0);
            var list = new TextBox { Name = "setExportClips", Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
                WordWrap = false, Text = "Exact target: " + targetIdentity + Environment.NewLine + "Target SHA-256: " + targetHash +
                    Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine + Environment.NewLine, clips), TabIndex = 0,
                AccessibleName = "Frozen clips, exact identities, frame counts and proposed filenames" };
            layout.Controls.Add(list, 0, 1);
            _optimization = new WorkspaceExportOptimizationOptions(new NativeExportOptimizationPreferences().Read()) { TabIndex = 1 };
            layout.Controls.Add(_optimization, 0, 2);
            layout.Controls.Add(new Label { AutoSize = true, Text = "New output folder", Margin = new Padding(0, 10, 0, 6) }, 0, 3);
            var picker = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, TabIndex = 2 };
            picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); picker.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            picker.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _path = new TextBox { Name = "setExportPath", Dock = DockStyle.Fill, TabIndex = 1, AccessibleName = "New, nonexistent output folder" };
            _browse = new WorkspaceButton { Name = "setExportBrowse", Text = "Browse parent...", Dock = DockStyle.Fill, TabIndex = 2,
                AccessibleDescription = "Choose an existing parent directory, then name the new set folder." };
            _browse.Click += (s, e) => BrowseParent(); _path.TextChanged += (s, e) => ValidatePath();
            picker.Controls.Add(_path, 0, 0); picker.Controls.Add(_browse, 1, 0); layout.Controls.Add(picker, 0, 4);
            _status = new Label { Name = "setExportStatus", Dock = DockStyle.Fill, UseMnemonic = false, AccessibleName = "Set export validation and progress" };
            layout.Controls.Add(_status, 0, 5);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty, TabIndex = 3 };
            _cancel = new WorkspaceButton { Name = "setExportCancel", Text = "Cancel", AutoSize = true, TabIndex = 4 };
            _accept = new WorkspaceButton { Name = "setExportAccept", Text = "Export set", AutoSize = true, Enabled = false, TabIndex = 3 };
            _cancel.Click += (s, e) => { if (_running) RequestCancel(); else DialogResult = DialogResult.Cancel; };
            _accept.Click += async (s, e) => await ExportSet();
            actions.Controls.Add(_cancel); actions.Controls.Add(_accept); layout.Controls.Add(actions, 0, 6); Controls.Add(layout);
            AcceptButton = _accept; CancelButton = _cancel;
            FormClosing += (s, e) => { if (_running) { e.Cancel = true; RequestCancel(); } };
            DarkTheme.Apply(this); WorkspaceAppearance.SizeDialog(this);
            foreach (Button button in actions.Controls) { button.AutoSize = false; button.Size = new Size(TextRenderer.MeasureText(button.Text, button.Font).Width + 28, button.Font.Height + 12); }
            Shown += (s, e) => { ValidatePath(); _path.Focus(); };
            AccessibleDescription = "Review the frozen animation list and choose a new folder. Tab navigates controls; Escape cancels. Existing folders are preserved.";
        }
        internal NativeExportOptimizationSettings ReadOptimizationSettings() => _optimization.Read();
        public new DialogResult ShowDialog(IWin32Window owner)
        {
            Action restore = WorkspaceDialogFocus.Capture(owner);
            try { return base.ShowDialog(owner); } finally { restore(); }
        }
        private string Destination => AnimationLibrary.OrdinaryPath(_path.Text.Trim().Trim('"'));
        private bool ValidatePath()
        {
            if (_running) return false;
            bool valid = false; string message;
            try
            {
                if (string.IsNullOrWhiteSpace(_path.Text)) throw new IOException("Choose a new named folder within an existing folder.");
                string path = Destination;
                if (File.Exists(path) || Directory.Exists(path) || !Directory.Exists(Path.GetDirectoryName(path)))
                    throw new IOException("Choose a new, nonexistent folder. Existing destinations are preserved.");
                ValidateDestination?.Invoke(path); message = "Ready to verify every animation and publish the complete set."; valid = true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is InvalidOperationException || error is NotSupportedException)
            { message = error.Message; }
            _accept.Enabled = valid; _status.Text = message; _status.AccessibleDescription = message;
            return valid;
        }
        private void BrowseParent()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Choose the parent folder for a new animation set.", ShowNewFolderButton = false })
                if (dialog.ShowDialog(this) == DialogResult.OK) _path.Text = Path.Combine(dialog.SelectedPath, "Fighterport export");
            _path.Focus();
        }
        private void RequestCancel()
        {
            _cancellation?.Cancel(); _cancel.Enabled = false; _status.Text = "Stopping export; waiting for the operation to finish.";
        }
        private async Task ExportSet()
        {
            if (_running || !ValidatePath()) return;
            string destination = Destination;
            _running = true; _path.Enabled = _browse.Enabled = _accept.Enabled = _optimization.Enabled = false;
            _cancellation = new CancellationTokenSource();
            var cancellation = _cancellation;
            try
            {
                var progress = new Progress<string>(message =>
                {
                    if (!IsDisposed && _running && ReferenceEquals(_cancellation, cancellation) && !cancellation.IsCancellationRequested) _status.Text = message;
                });
                await Export(destination, _cancellation.Token, progress);
                _running = false; DialogResult = DialogResult.OK;
            }
            catch (OperationCanceledException) { _status.Text = "Export cancelled. No set was published."; }
            catch (Exception error) { _status.Text = error.Message; }
            finally
            {
                _running = false; _cancellation.Dispose(); _cancellation = null;
                _cancel.Enabled = _path.Enabled = _browse.Enabled = _optimization.Enabled = true;
                _status.AccessibleDescription = _status.Text;
                // On error, keep the frozen operation reviewable and let the
                // caller reopen if its content dependencies became stale.
                if (DialogResult != DialogResult.OK) _accept.Enabled = true;
            }
        }
    }
}
