using System.Drawing;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    internal sealed class WorkspaceOverwriteDialog : Form
    {
        internal WorkspaceOverwriteDialog(string path)
        {
            Text = "Replace exported animation?";
            Name = "workspaceOverwriteDialog";
            ClientSize = new Size(620, 190);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = ShowInTaskbar = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), RowCount = 2, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = "Replace this file only after the animation passes verification?\n" + path,
                UseMnemonic = false, AutoSize = true, Dock = DockStyle.Fill }, 0, 0);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new WorkspaceButton { Name = "workspaceOverwriteCancel", Text = "Keep existing", AutoSize = true, DialogResult = DialogResult.Cancel, TabIndex = 1 };
            var replace = new WorkspaceButton { Name = "workspaceOverwriteAccept", Text = "Replace", AutoSize = true, DialogResult = DialogResult.Yes, TabIndex = 0 };
            buttons.Controls.AddRange(new Control[] { cancel, replace });
            layout.Controls.Add(buttons, 0, 1); Controls.Add(layout);
            CancelButton = cancel; AcceptButton = replace;
            Shown += (s, e) => cancel.Focus();
            DarkTheme.Apply(this); WorkspaceAppearance.SizeDialog(this);
        }
    }
}
