using BrawlCrate.Studio;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal sealed class BrawlDiscDialog : Form
    {
        private readonly TextBox disc = new TextBox { Name = "discFilePath", MaxLength = 32767, Dock = DockStyle.Fill };
        private readonly TextBox build = new TextBox { Name = "discBuild", MaxLength = 240, Dock = DockStyle.Fill, Text = "Original Brawl source" };
        private readonly Label validation = new Label { Name = "discValidation", Dock = DockStyle.Fill, AutoEllipsis = false };
        internal LibraryGameRequest Request { get; private set; }

        internal BrawlDiscDialog(string initialDisc = null)
        {
            Text = "Add Brawl source to library"; Name = "BrawlDiscDialog";
            StartPosition = FormStartPosition.CenterParent; MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false; AutoScaleMode = AutoScaleMode.None; AutoScroll = true; Font = WorkspaceAppearance.Font;
            ClientSize = new Size(Math.Min(1100, WorkspaceAppearance.Pixels(760)), Math.Min(800, WorkspaceAppearance.Pixels(420)));
            MinimumSize = new Size(640, 420);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(14) };
            layout.MinimumSize = new Size(0, Math.Max(360, Font.Height * 19 + 60));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int row = 0; row < 7; row++) layout.RowStyles.Add(new RowStyle(row == 5 ? SizeType.Percent : SizeType.AutoSize, row == 5 ? 100 : 0));
            layout.Controls.Add(new Label { Text = "Choose a Brawl ISO/WBFS or extracted game folder. Fighterport reads the disc directly; no separate tool is needed. All fighters, animations and costumes are indexed; original files stay read-only.", AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) }, 0, 0);
            AddPath(layout, 1, "Disc or extracted Brawl folder", disc, "discBrowseFile", () => {
                using (var choose = new OpenFileDialog { Title = "Choose Brawl disc", Filter = "Brawl disc images (*.iso;*.wbfs)|*.iso;*.wbfs|All files (*.*)|*.*", CheckFileExists = true, FileName = disc.Text })
                    if (choose.ShowDialog(this) == DialogResult.OK) disc.Text = choose.FileName;
            }, () => {
                using (var choose = new FolderBrowserDialog { Description = "Choose an extracted or installed Brawl-family build containing its fighter folder" })
                    if (choose.ShowDialog(this) == DialogResult.OK) disc.Text = choose.SelectedPath;
            });
            layout.Controls.Add(new Label { Text = "Build label", AutoSize = true, Dock = DockStyle.Fill }, 0, 3);
            build.AccessibleName = "Brawl game/build label"; build.Margin = new Padding(0, 3, 0, 8); layout.Controls.Add(build, 0, 4);
            validation.MinimumSize = new Size(0, Font.Height * 2 + 12); validation.Text = "Add this source to include its full fighter library automatically.";
            validation.AccessibleName = "Brawl disc validation"; layout.Controls.Add(validation, 0, 5);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = true };
            var cancel = new WorkspaceButton { Name = "discCancel", Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel, Padding = new Padding(12, 6, 12, 6) };
            var next = new WorkspaceButton { Name = "discContinue", Text = "Add to library", AutoSize = true, Padding = new Padding(12, 6, 12, 6) };
            next.Click += (s, e) => Continue(); actions.Controls.Add(cancel); actions.Controls.Add(next); layout.Controls.Add(actions, 0, 6);
            Controls.Add(layout); AcceptButton = next; CancelButton = cancel;
            disc.Text = initialDisc ?? "";
            disc.AccessibleName = "Brawl disc or extracted game folder";
            disc.TextChanged += (s, e) => ResetValidation(); build.TextChanged += (s, e) => ResetValidation();
            int tab = 0; foreach (Control control in new Control[] { disc, build, next, cancel }) control.TabIndex = tab++;
            DarkTheme.Apply(this);
            Shown += (s, e) => disc.Focus();
        }

        private static void AddPath(TableLayoutPanel layout, int row, string label, TextBox field, string browseName, Action browse, Action folder = null)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Dock = DockStyle.Fill }, 0, row);
            var picker = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = folder == null ? 2 : 3, RowCount = 1, Margin = new Padding(0, 3, 0, 8) };
            picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); picker.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var button = new WorkspaceButton { Name = browseName, Text = folder == null ? "Browse..." : "Disc...", AutoSize = true, Padding = new Padding(10, 3, 10, 3) };
            button.Click += (s, e) => browse(); field.Margin = new Padding(0, 3, 8, 3);
            picker.Controls.Add(field, 0, 0); picker.Controls.Add(button, 1, 0);
            if (folder != null)
            {
                picker.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                var chooseFolder = new WorkspaceButton { Name = "discBrowseFolder", Text = "Folder...", AutoSize = true, Padding = new Padding(10, 3, 10, 3) };
                chooseFolder.Click += (s, e) => folder(); picker.Controls.Add(chooseFolder, 2, 0);
            }
            layout.Controls.Add(picker, 0, row + 1);
        }

        private void ResetValidation()
        {
            Request = null; validation.ForeColor = ForeColor;
            validation.Text = "Add this source to include its full fighter library automatically.";
        }

        private void Continue()
        {
            try
            {
                var request = new LibraryGameRequest { Game = "Brawl", Path = disc.Text.Trim().Trim('"'), GameVersion = build.Text.Trim() };
                ValidatePaths(request);
                var inspected = LibraryWork.Run(this, "Checking Brawl source", (cancel, progress) => {
                    progress.Report(Directory.Exists(request.Path) ? "Checking the extracted Brawl fighter folders; no native packages decoded yet..." : "Checking the native Brawl disc header and filesystem; no full-image hash or fighter extraction yet...");
                    ValidateRequest(request, cancel); return request;
                });
                if (inspected == null) { validation.Text = "Source check cancelled. No source was added."; return; }
                Request = inspected; DialogResult = DialogResult.OK;
            }
            catch (Exception error) when (error is InvalidDataException || error is IOException || error is UnauthorizedAccessException || error is System.Security.SecurityException || error is InvalidOperationException || error is ArgumentException || error is NotSupportedException || error is System.ComponentModel.Win32Exception || error is FormatException || error is OverflowException)
            {
                validation.ForeColor = Color.FromArgb(246, 174, 116); validation.Text = error.Message;
            }
        }

        private static void ValidatePaths(LibraryGameRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Path)) throw new InvalidDataException("Choose a Brawl disc file or extracted game folder.");
            request.Path = AnimationLibrary.OrdinaryPath(request.Path); AnimationLibrary.NoLinks(request.Path);
            if (string.IsNullOrWhiteSpace(request.GameVersion) || request.GameVersion.Length > 240) throw new InvalidDataException("Enter a short label for this original game/build.");
            if (Directory.Exists(request.Path)) return;
            if (!File.Exists(request.Path) || !new[] { ".iso", ".wbfs" }.Contains(Path.GetExtension(request.Path).ToLowerInvariant()))
                throw new InvalidDataException("Choose an existing Brawl ISO/WBFS file or an extracted Brawl folder. The built-in reader accepts single-file ISO/WBFS.");
            var image = new FileInfo(request.Path);
            if (image.Length == 0 || image.Length > 64L * 1024 * 1024 * 1024) throw new InvalidDataException("Choose a nonempty Brawl image within the reader's 64 GB limit.");
            BrawlGameSource.EnsureSingleDiscSource(request.Path);

        }

        // Explicit preflight only. It verifies the bounded native filesystem;
        // full image hashing, extraction and catalogue publication remain in the
        // existing complete-library preparation transaction.
        internal static void ValidateRequest(LibraryGameRequest request, CancellationToken cancellation)
        {
            ValidatePaths(request);
            BrawlGameSource.ValidateGameSource(request, cancellation);
        }
    }
}
