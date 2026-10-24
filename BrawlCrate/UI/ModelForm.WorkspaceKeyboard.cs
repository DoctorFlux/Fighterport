using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private readonly Dictionary<Control, object> _workspaceFocus = new Dictionary<Control, object>();
        private object _workspaceInvoker, _workspaceLibraryOpener, _workspaceTaskOpener;

        private Control[] WorkspaceRegions => new Control[] { _simpleWorkspaceToolbar, _simpleWorkspaceActions,
            _simpleWorkspaceNavigation, _libraryPane, _simpleWorkspaceTask, _comparisonPane, modelEditControl1 }
            .Where(c => c != null && !c.IsDisposed && c.Visible && c.Enabled).ToArray();

        private static Control WorkspaceFocusedControl(Control root)
        {
            if (root == null || root.IsDisposed) return null;
            foreach (Control child in root.Controls)
                if (child.ContainsFocus) return WorkspaceFocusedControl(child);
            return root.ContainsFocus ? root : null;
        }

        private object CurrentWorkspaceFocus()
        {
            Control control = WorkspaceFocusedControl(this);
            // UpDownBase focuses a private edit child that is not independently
            // selectable. Remember the public composite, not that child HWND.
            if (control?.Parent is UpDownBase spinner) control = spinner;
            while (control != null && !control.CanSelect && control.Parent != this) control = control.Parent;
            if (control is ToolStrip strip)
                return (object)strip.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Selected) ?? control;
            return control;
        }

        private static bool WorkspaceTargetReady(object target)
        {
            if (target is ToolStripItem item)
                return !item.IsDisposed && item.Available && item.Enabled && item.Owner != null &&
                    item.Owner.Visible && item.Owner.Enabled && item.Placement == ToolStripItemPlacement.Main;
            return target is Control control && !control.IsDisposed && control.Visible && control.Enabled && control.CanSelect;
        }

        private static bool FocusWorkspaceTarget(object target)
        {
            if (!WorkspaceTargetReady(target)) return false;
            if (target is ToolStripControlHost host) return host.Control.Focus();
            if (target is ToolStripItem item)
            {
                if (!item.Owner.Focus()) return false;
                item.Select();
                return item.Selected;
            }
            var control = (Control)target;
            for (Control parent = control.Parent; parent != null; parent = parent.Parent)
                if (parent is ScrollableControl scroll && scroll.AutoScroll) scroll.ScrollControlIntoView(control);
            return control.Focus();
        }

        private void RestoreWorkspaceFocus(object target)
        {
            if (!IsDisposed && !Disposing && !FocusWorkspaceTarget(target))
                FocusWorkspaceTarget(modelEditControl1.ModelPanel);
        }

        internal Action CaptureWorkspaceFocus()
        {
            object target = _workspaceInvoker ?? CurrentWorkspaceFocus();
            return () => RestoreWorkspaceFocus(target);
        }

        private void WithWorkspaceInvoker(object invoker, Action action)
        {
            object previous = _workspaceInvoker;
            _workspaceInvoker = invoker;
            try { action(); }
            finally { _workspaceInvoker = previous; }
        }

        private static IEnumerable<object> WorkspaceTabTargets(Control root)
        {
            if (root is ToolStrip strip)
            {
                foreach (ToolStripItem item in strip.Items)
                    if (!(item is ToolStripLabel) && !(item is ToolStripSeparator) && WorkspaceTargetReady(item)) yield return item;
                if (strip.OverflowButton.Visible && strip.OverflowButton.Enabled) yield return strip.OverflowButton;
                yield break;
            }
            foreach (Control child in root.Controls.Cast<Control>().OrderBy(c => c.TabIndex))
            {
                if (!child.Visible || !child.Enabled) continue;
                if (child.TabStop && child.CanSelect) yield return child;
                foreach (object nested in WorkspaceTabTargets(child)) yield return nested;
            }
        }

        private bool FocusWorkspaceRegion(Control region, bool last = false, bool remember = true)
        {
            if (remember && _workspaceFocus.TryGetValue(region, out object target) && WorkspaceTargetReady(target) && FocusWorkspaceTarget(target)) return true;
            if (region == modelEditControl1) return FocusWorkspaceTarget(modelEditControl1.ModelPanel);
            var candidates = WorkspaceTabTargets(region).ToArray();
            return (last ? candidates.Reverse() : candidates).Any(FocusWorkspaceTarget);
        }

        private bool MoveWorkspaceRegion(bool reverse, bool remember = true)
        {
            var regions = WorkspaceRegions;
            object focused = CurrentWorkspaceFocus();
            Control leaf = focused is ToolStripItem item ? item.Owner : focused as Control;
            int index = Array.FindIndex(regions, r => r == leaf || leaf != null && r.Contains(leaf));
            if (index >= 0 && WorkspaceTargetReady(focused)) _workspaceFocus[regions[index]] = focused;
            int start = index < 0 ? (reverse ? 0 : -1) : index;
            for (int offset = 1; offset <= regions.Length; offset++)
            {
                var region = regions[(start + (reverse ? -offset : offset) + regions.Length * 2) % regions.Length];
                if (FocusWorkspaceRegion(region, reverse, remember)) return true;
            }
            return false;
        }

        private bool WorkspaceMenuOpen => new[] { _simpleWorkspaceToolbar, _simpleWorkspaceActions, _simpleWorkspaceNavigation }
            .Where(s => s != null).Any(s => s.Items.Cast<ToolStripItem>().OfType<ToolStripDropDownItem>().Any(i => i.DropDown.Visible)
                || s.OverflowButton.DropDown.Visible);

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (_simpleWorkspaceActions != null && !Application.OpenForms.Cast<Form>().Any(f => f != this && f.Visible && f.Modal) && !WorkspaceMenuOpen)
            {
                if ((keyData == Keys.F6 || keyData == (Keys.Shift | Keys.F6)) && _pinGesture == null)
                    return MoveWorkspaceRegion((keyData & Keys.Shift) != 0);
                Control focused = WorkspaceFocusedControl(this);
                if (keyData == Keys.Escape && !SimpleWorkspaceBusy)
                {
                    if (_comparisonPane?.ContainsFocus == true) { CloseLinkedComparison(); return true; }
                    if (_libraryPane?.ContainsFocus == true)
                    {
                        SetSimpleWorkspaceLibraryVisible(false);
                        return true;
                    }
                    if (_simpleWorkspaceTask.ContainsFocus)
                    {
                        _simpleWorkspaceFocused = true; _simpleWorkspaceDrawer = false;
                        UpdateSimpleWorkspace(); RestoreWorkspaceFocus(_workspaceTaskOpener);
                        return true;
                    }
                }
                if ((keyData == Keys.Tab || keyData == (Keys.Shift | Keys.Tab)) && focused != null && !modelEditControl1.ContainsFocus)
                {
                    bool reverse = (keyData & Keys.Shift) != 0;
                    var region = WorkspaceRegions.FirstOrDefault(r => r == focused || r.Contains(focused));
                    if (region != null)
                    {
                        object current = CurrentWorkspaceFocus(); var targets = WorkspaceTabTargets(region).ToArray();
                        int index = Array.FindIndex(targets, t => ReferenceEquals(t, current) || t is ToolStripControlHost host && (host.Control == focused || host.Control.Contains(focused)));
                        int next = index + (reverse ? -1 : 1);
                        if (index >= 0 && next >= 0 && next < targets.Length && FocusWorkspaceTarget(targets[next])) return true;
                        return MoveWorkspaceRegion(reverse, false);
                    }
                }
            }
            return base.ProcessCmdKey(ref message, keyData);
        }

        private static void PrepareWorkspaceTabOrder(Control root)
        {
            int index = 0;
            foreach (Control child in root.Controls)
            {
                child.TabIndex = index++;
                if (child is Panel || child is UserControl) child.TabStop = false;
                if (child is Button || child is LinkLabel || child is TextBoxBase || child is TreeView || child is TrackBar || child is CheckBox || child is ComboBox)
                    child.Enter += (sender, args) =>
                    {
                        for (Control parent = child.Parent; parent != null && parent != root.Parent; parent = parent.Parent)
                            if (parent is ScrollableControl scroll && scroll.AutoScroll) scroll.ScrollControlIntoView(child);
                    };
                PrepareWorkspaceTabOrder(child);
            }
        }

        private void InitializeWorkspaceKeyboard()
        {
            foreach (ToolStrip strip in new[] { _simpleWorkspaceToolbar, _simpleWorkspaceActions, _simpleWorkspaceNavigation })
            {
                strip.TabStop = true;
                strip.AccessibleDescription = "F6 or Shift+F6 moves between workspace regions. Tab moves between commands; arrow keys navigate this toolbar.";
            }
            _simpleWorkspaceWorkflow.ToolTipText = "Show or hide workflow tasks. F6 moves between workspace regions.";
            _simpleWorkspaceFocus.ToolTipText = "Focus the native editor; its original editing shortcuts remain available.";
            PrepareWorkspaceTabOrder(_simpleWorkspaceTask);
            _simpleWorkspaceTask.Controls["workspaceTaskFooter"].TabIndex = 0;
            _simpleWorkspaceTask.Controls["workspaceTaskScroll"].TabIndex = 1;
            _simpleWorkspaceTask.AccessibleDescription = "Tab visits the primary action and task details. Escape closes this pane. F6 moves to another region.";
        }
    }
}
