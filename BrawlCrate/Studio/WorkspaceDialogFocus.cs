using BrawlCrate.UI;
using System;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    internal static class WorkspaceDialogFocus
    {
        internal static Action Capture(IWin32Window owner)
        {
            // Nested Save belongs to the unsaved decision, even when the native
            // save handler supplies the model form as its owner.
            Form form = Form.ActiveForm?.Modal == true ? Form.ActiveForm : owner as Form;
            if (form is ModelForm model) return model.CaptureWorkspaceFocus();
            Control focused = form;
            while (focused is ContainerControl container && container.ActiveControl != null) focused = container.ActiveControl;
            return () => { if (focused != null && !focused.IsDisposed && focused.Visible && focused.Enabled) focused.Focus(); };
        }
    }
}
