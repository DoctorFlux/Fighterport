using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    internal sealed class UnsavedProjectDialog : Form
    {
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
                Bounds = new Rectangle(16, 14, 538, 38), UseMnemonic = false };
            var status = new Label { Name = "unsavedProjectStatus", Text =
                "Save keeps your current work. Discard leaves the last saved project on disk.",
                Bounds = new Rectangle(16, 58, 538, 56), UseMnemonic = false };
            var saveButton = new Button { Name = "unsavedProjectSave", Text = "Save", Bounds = new Rectangle(208, 138, 100, 30) };
            var discard = new Button { Name = "unsavedProjectDiscard", Text = "Discard", DialogResult = DialogResult.No, Bounds = new Rectangle(322, 138, 100, 30) };
            var cancel = new Button { Name = "unsavedProjectCancel", Text = "Keep editing", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(436, 138, 118, 30) };
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
                    if (!IsDisposed) saveButton.Enabled = discard.Enabled = cancel.Enabled = true;
                }
            };
            Controls.AddRange(new Control[] { message, status, saveButton, discard, cancel });
            AcceptButton = saveButton;
            CancelButton = cancel;
            FormClosing += (sender, args) => { if (_saving) args.Cancel = true; };
            DarkTheme.Apply(this);
        }
    }
}
