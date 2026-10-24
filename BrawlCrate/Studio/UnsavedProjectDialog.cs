using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    internal sealed class UnsavedProjectDialog : Form
    {
        public new DialogResult ShowDialog(IWin32Window owner)
        {
            Action restore = WorkspaceDialogFocus.Capture(owner);
            try { return base.ShowDialog(owner); }
            finally { restore(); }
        }

        private bool _saving;
        internal UnsavedProjectDialog(string projectName, Func<Task<string>> save)
        {
            Name = "unsavedProjectDialog";
            Text = "Unsaved Project Changes";
            ClientSize = new Size(570, 186);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = ShowInTaskbar = false;
            var message = new Label { Name = "unsavedProjectMessage", Text =
                "Save changes to “" + projectName + "” before leaving this project?",
                Dock=DockStyle.Fill, AutoSize=true, Margin=new Padding(0,0,0,16), UseMnemonic = false };
            var status = new Label { Name = "unsavedProjectStatus", Text =
                "Save keeps your current work. Discard leaves the last saved project on disk.",
                Dock=DockStyle.Fill, AutoSize=true, Margin=Padding.Empty, UseMnemonic = false };
            var saveButton = new WorkspaceButton { Name = "unsavedProjectSave", TabIndex = 0, Text = "Save", Size=new Size(100,30) };
            var discard = new WorkspaceButton { Name = "unsavedProjectDiscard", TabIndex = 1, Text = "Discard", DialogResult = DialogResult.No, Size=new Size(100,30) };
            var cancel = new WorkspaceButton { Name = "unsavedProjectCancel", TabIndex = 2, Text = "Keep editing", DialogResult = DialogResult.Cancel, Size=new Size(118,30) };
            saveButton.Click += async (sender, args) =>
            {
                if (_saving) return;
                _saving = true;
                saveButton.Enabled = discard.Enabled = cancel.Enabled = false;
                status.Text = "Saving the current project...";
                try
                {
                    string error = await save();
                    if (error == null) { _saving = false; DialogResult = DialogResult.Yes; Close(); }
                    else status.Text = error;
                }
                catch (Exception error) { status.Text = "Could not save: " + error.Message; }
                finally
                {
                    _saving = false;
                    if (!IsDisposed)
                    {
                        saveButton.Enabled = discard.Enabled = cancel.Enabled = true;
                        if (ContainsFocus || Form.ActiveForm == this) saveButton.Focus();
                    }
                }
            };
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(16)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,AutoSize=true,WrapContents=true,Margin=Padding.Empty};
            actions.Controls.AddRange(new Control[]{cancel,discard,saveButton});
            layout.Controls.Add(message,0,0);layout.Controls.Add(status,0,1);layout.Controls.Add(actions,0,2);Controls.Add(layout);
            Shown += (sender, args) => cancel.Focus();
            AcceptButton = saveButton;
            CancelButton = cancel;
            FormClosing += (sender, args) => { if (_saving) args.Cancel = true; };
            DarkTheme.Apply(this);
            WorkspaceAppearance.SizeDialog(this);
        }
    }
}
