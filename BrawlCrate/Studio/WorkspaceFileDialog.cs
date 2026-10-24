using System;
using System.Drawing;
using System.IO;
using System.Security;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    internal sealed class WorkspaceFileDialog : Form
    {
        public new DialogResult ShowDialog(IWin32Window owner)
        {
            Action restore = WorkspaceDialogFocus.Capture(owner);
            try { return base.ShowDialog(owner); }
            finally { restore(); }
        }

        private readonly TextBox _path;
        private readonly Label _validation;
        private readonly Button _accept;
        private readonly string _filter;
        private string _candidatePath;
        private readonly bool _save;
        private readonly string _extension;
        internal Action<string> CommitSelection { get; set; }

        internal string SelectedPath { get; private set; }

        internal WorkspaceFileDialog(string title, string description, string filter,
            string actionText, string initialPath = null, bool save = false, WorkspaceRecentProjects recent = null, string extension = "fpstudio")
        {
            Name = "workspaceFileDialog";
            Text = title;
            AccessibleName = title;
            ClientSize = new Size(650, recent == null ? (extension == "chr0" ? 270 : 210) : 300);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.Font;
            _filter = string.IsNullOrWhiteSpace(filter) ? "All files (*.*)|*.*" : filter;
            _save = save;
            _extension = extension.TrimStart('.');

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = recent == null ? 5 : 6
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            if (recent != null) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.Controls.Add(new Label
            {
                Name = "workspaceFileDescription",
                Text = description,
                AutoSize = true,
                MaximumSize = new Size(626, 0),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 10),
                UseMnemonic = false
            }, 0, 0);
            layout.Controls.Add(new Label
            {
                Text = "File path",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 4)
            }, 0, 1);

            var picker = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty
            };
            picker.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            picker.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
            _path = new TextBox
            {
                Name = "workspaceFilePath",
                AccessibleName = "File path",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 8, 0),
                TabIndex = 0
            };
            var browse = new WorkspaceButton
            {
                Name = "workspaceFileBrowse",
                AccessibleName = "Browse for a file",
                Text = "Browse...",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 4),
                TabIndex = 1
            };
            browse.Click += (sender, args) => Browse();
            picker.Controls.Add(_path, 0, 0);
            picker.Controls.Add(browse, 1, 0);
            layout.Controls.Add(picker, 0, 2);

            _validation = new Label
            {
                Name = "workspaceFileValidation",
                AccessibleName = "File selection status",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 6),
                UseMnemonic = false
            };
            layout.Controls.Add(_validation, 0, recent == null ? 3 : 4);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = Padding.Empty
            };
            var cancel = new WorkspaceButton
            {
                Name = "workspaceFileCancel",
                AccessibleName = "Cancel file selection",
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size(92, 30),
                Margin = new Padding(8, 0, 0, 0),
                TabIndex = 3
            };
            _accept = new WorkspaceButton
            {
                Name = "workspaceFileAccept",
                AccessibleName = string.IsNullOrWhiteSpace(actionText) ? "Open" : actionText,
                Text = string.IsNullOrWhiteSpace(actionText) ? "Open" : actionText,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(92, 30),
                Margin = Padding.Empty,
                TabIndex = 2
            };
            _accept.Click += (sender, args) => AcceptPath();
            actions.Controls.Add(cancel);
            actions.Controls.Add(_accept);
            layout.Controls.Add(actions, 0, recent == null ? 4 : 5);
            Controls.Add(layout);
            AcceptButton = _accept;
            CancelButton = cancel;

            DarkTheme.Apply(this);
            _path.TextChanged += (sender, args) => ValidatePath();
            _path.Text = initialPath ?? "";
            ValidatePath();
            Shown += (sender, args) => { _path.Focus(); _path.SelectAll(); };
            WorkspaceAppearance.SizeDialog(this);
            layout.RowStyles[2].Height = Math.Max(Font.Height+20,WorkspaceAppearance.Pixels(38));
            layout.RowStyles[recent == null ? 4 : 5].Height = Math.Max(Font.Height+24,WorkspaceAppearance.Pixels(36));
            if (recent != null) AddRecentProjects(layout, recent);
        }

        private void AddRecentProjects(TableLayoutPanel layout, WorkspaceRecentProjects recent)
        {
            var panel = new TableLayoutPanel { Name="workspaceRecentPanel", Dock=DockStyle.Fill,
                ColumnCount=3, RowCount=2, Margin=Padding.Empty, TabIndex=4 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,WorkspaceAppearance.Pixels(120)));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,WorkspaceAppearance.Pixels(105)));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            var status = new Label { Name="workspaceRecentStatus", Dock=DockStyle.Fill,
                AutoEllipsis=true, UseMnemonic=false, TabStop=false };
            panel.Controls.Add(status,0,0); panel.SetColumnSpan(status,3);
            var paths = new ComboBox { Name="workspaceRecentProjects", AccessibleName="Recent project full paths",
                AccessibleDescription="Choose a recent path, then use Open project. Selection does not open or change a project.",
                Dock=DockStyle.Fill, DropDownStyle=ComboBoxStyle.DropDownList, TabIndex=0, IntegralHeight=false, DropDownHeight=WorkspaceAppearance.Pixels(200) };
            var forget = new WorkspaceButton { Name="workspaceForgetRecent", Text="Forget selected", Dock=DockStyle.Fill, TabIndex=1,
                AccessibleDescription="Remove only this path from recent history. Keep all project files." };
            var clear = new WorkspaceButton { Name="workspaceClearRecent", Text="Clear history", Dock=DockStyle.Fill, TabIndex=2,
                AccessibleDescription="Clear only recent history. Keep all project files." };
            var tips = new ToolTip();
            Disposed+=(sender,args)=>tips.Dispose();
            Action<string> refresh = message =>
            {
                paths.Items.Clear(); paths.Items.AddRange(recent.Paths);
                int widest=0;
                foreach(string path in recent.Paths) widest=Math.Max(widest,TextRenderer.MeasureText(path,paths.Font).Width+32);
                paths.DropDownWidth=Math.Min(Screen.FromControl(this).WorkingArea.Width-48,Math.Max(WorkspaceAppearance.Pixels(350),widest));
                paths.Enabled=clear.Enabled=paths.Items.Count>0; forget.Enabled=false;
                status.Text=recent.LastError ?? message ?? (paths.Items.Count==0 ? "Recent projects — none yet" : "Recent projects — choose a full path, then Open project");
                status.AccessibleDescription=status.Text;
            };
            paths.SelectedIndexChanged+=(sender,args)=>
            {
                forget.Enabled=paths.SelectedItem!=null;
                if (paths.SelectedItem is string path)
                {
                    _path.Text=path; _path.SelectAll();
                    paths.AccessibleDescription="Recent project: "+path+". Use Open project to continue.";
                    tips.SetToolTip(paths,path); tips.SetToolTip(_path,path);
                }
            };
            forget.Click+=(sender,args)=>
            {
                if (paths.SelectedItem is string path) { recent.Forget(path); refresh("Removed from history. Project files were kept."); }
                if (paths.Enabled) paths.Focus(); else _path.Focus();
            };
            clear.Click+=(sender,args)=> { recent.Clear(); refresh("History cleared. Project files were kept."); _path.Focus(); };
            panel.Controls.Add(paths,0,1);panel.Controls.Add(forget,1,1);panel.Controls.Add(clear,2,1);
            layout.Controls.Add(panel,0,3);
            layout.RowStyles[3].Height=WorkspaceAppearance.Pixels(90);
            DarkTheme.Apply(panel);
            refresh(null);
        }

        private void Browse()
        {
            if (_save)
            {
                using (var dialog = new SaveFileDialog { Title = Text, Filter = _filter,
                    DefaultExt = _extension, AddExtension = true, OverwritePrompt = true, RestoreDirectory = true })
                {
                    if (ValidatePath()) dialog.FileName = _candidatePath;
                    if (dialog.ShowDialog(this) == DialogResult.OK) _path.Text = dialog.FileName;
                }
                return;
            }
            using (var dialog = new OpenFileDialog
            {
                Title = Text,
                Filter = _filter,
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                RestoreDirectory = true
            })
            {
                if (ValidatePath()) dialog.FileName = _candidatePath;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _path.Text = dialog.FileName;
            }
        }

        private bool ValidatePath()
        {
            _candidatePath = null;
            string entered = _path.Text.Trim();
            if (entered.Length >= 2 && entered[0] == '"' && entered[entered.Length - 1] == '"')
                entered = entered.Substring(1, entered.Length - 2).Trim();
            if (entered.Length == 0)
                return ShowValidation(_save ? "Choose a filename in an existing folder." : "Paste a file path or choose Browse to select an existing file.", false);
            try
            {
                string fullPath = Path.GetFullPath(entered);
                if (_save)
                {
                    if (!string.Equals(Path.GetExtension(fullPath), "." + _extension, StringComparison.OrdinalIgnoreCase))
                        return ShowValidation("Choose a filename ending in ." + _extension + ".", false);
                    if (Directory.Exists(fullPath) || !Directory.Exists(Path.GetDirectoryName(fullPath)))
                        return ShowValidation("Choose a filename in an existing folder.", false);
                    _candidatePath = fullPath;
                    return ShowValidation("Ready to " + _accept.Text.ToLowerInvariant() + " to this file.", true);
                }
                if (!File.Exists(fullPath))
                    return ShowValidation("File not found or unavailable. Check the path or choose Browse.", false);
                _candidatePath = fullPath;
                return ShowValidation("File found. Continue to open it; its contents will be checked when loaded.", true);
            }
            catch (Exception exception) when (exception is ArgumentException ||
                exception is NotSupportedException || exception is IOException ||
                exception is UnauthorizedAccessException || exception is SecurityException)
            {
                return ShowValidation("That path is invalid or inaccessible. Correct it or choose Browse.", false);
            }
        }

        private bool ShowValidation(string text, bool valid)
        {
            _validation.Text = text;
            _validation.AccessibleDescription = text;
            _validation.ForeColor = valid ? Color.LightGreen : Color.LightSalmon;
            _accept.Enabled = valid;
            return valid;
        }

        private void AcceptPath()
        {
            if (!ValidatePath()) return;
            try { CommitSelection?.Invoke(_candidatePath); }
            catch (Exception error) { ShowValidation((_extension == "chr0" ? "Could not export: " : "Could not save: ") + error.Message, true); _validation.ForeColor = Color.LightSalmon; return; }
            SelectedPath = _candidatePath;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
