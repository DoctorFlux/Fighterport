using System;
using System.Drawing;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    internal sealed class WorkspaceExportOptimizationOptions : UserControl
    {
        internal event EventHandler SettingsChanged;
        private readonly ComboBox _policy, _effort;
        private readonly TableLayoutPanel _advanced, _limits;
        private readonly NativeExportOptimizationSettings _initial;
        private readonly NumericUpDown[] _values = new NumericUpDown[7];
        private readonly Label _description;
        private readonly ToolTip _hints = new ToolTip();
        internal WorkspaceExportOptimizationOptions(NativeExportOptimizationSettings settings)
        {
            _initial = settings.Clone(); Name = "exportOptimizationOptions"; AutoSize = true; Dock = DockStyle.Top; TabStop = false;
            AccessibleName = "Export copy optimization policy";
            var layout = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1, Padding = Padding.Empty, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var choices = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = Padding.Empty, TabIndex = 0 };
            choices.Controls.Add(new Label { Text = "Export copy", AutoSize = true, Margin = new Padding(0, 7, 6, 0) });
            _policy = new ComboBox { Name = "exportOptimizationPolicy", DropDownStyle = ComboBoxStyle.DropDownList, Width = 145, AccessibleName = "Export policy", TabIndex = 0 };
            _policy.Items.AddRange(new object[] { "Exact", "Error-bounded" }); _policy.SelectedIndex = (int)settings.Policy;
            choices.Controls.Add(_policy);
            var details = new LinkLabel { Name = "exportOptimizationLimits", Text = "Advanced…", AutoSize = true, Margin = new Padding(10, 7, 0, 0), TabIndex = 1,
                AccessibleDescription = "Show saved search effort and cumulative error ceilings. Collapsing this section changes no settings; search effort never changes quality limits." };
            choices.Controls.Add(details); layout.Controls.Add(choices);
            _description = new Label { Name = "exportOptimizationDescription", AutoSize = true, MaximumSize = new Size(720, 0), Dock = DockStyle.Fill, UseMnemonic = false, Margin = new Padding(0, 4, 0, 7) };
            layout.Controls.Add(_description);
            _advanced = new TableLayoutPanel { Name = "exportOptimizationAdvancedFields", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top, ColumnCount = 1, RowCount = 2, Visible = false, Margin = Padding.Empty, TabIndex = 2 };
            _advanced.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _advanced.RowStyles.Add(new RowStyle(SizeType.AutoSize)); _advanced.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var search = new FlowLayoutPanel { Name = "exportOptimizationSearchFields", AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = Padding.Empty, TabIndex = 0 };
            search.Controls.Add(new Label { Text = "Search", AutoSize = true, Margin = new Padding(0, 7, 6, 0) });
            _effort = new ComboBox { Name = "exportOptimizationEffort", DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, AccessibleName = "Search effort, independent of quality limits", TabIndex = 1 };
            _effort.Items.AddRange(new object[] { "Standard", "Thorough" }); _effort.SelectedIndex = (int)settings.SearchEffort; search.Controls.Add(_effort);
            _advanced.Controls.Add(search, 0, 0);
            _limits = new TableLayoutPanel { Name = "exportOptimizationLimitFields", AutoSize = true, Dock = DockStyle.Top, ColumnCount = 4, Margin = Padding.Empty, TabIndex = 1 };
            _limits.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); _limits.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _limits.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); _limits.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            string[] names = { "Rotation (°)", "Translation", "Scale", "Full model × L", "Rotation / frame", "Translation / frame", "Scale / frame" };
            double[] values = { settings.RotationDegrees, settings.Translation, settings.Scale, settings.GeometryRelative, settings.RotationDerivative, settings.TranslationDerivative, settings.ScaleDerivative };
            decimal[] maxima = { .1m, .002m, .0001m, .001m, .1m, .002m, .0001m };
            for (int i = 0; i < names.Length; i++)
            {
                int column = i % 2 * 2, row = i / 2;
                _limits.Controls.Add(new Label { AutoSize = true, Text = names[i], Margin = new Padding(0, 6, 8, 0) }, column, row);
                _values[i] = new NumericUpDown { Name = "exportOptimizationLimit" + i, DecimalPlaces = 7, Minimum = .0000001m, Maximum = maxima[i], Increment = maxima[i] / 10,
                    Value = (decimal)values[i], Dock = DockStyle.Fill, TabIndex = i + 3, AccessibleName = names[i] + " maximum cumulative error" };
                _limits.Controls.Add(_values[i], column + 1, row);
                _values[i].ValueChanged += (s, e) => SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            _advanced.Controls.Add(_limits, 0, 1); layout.Controls.Add(_advanced); Controls.Add(layout);
            details.LinkClicked += (s, e) =>
            {
                if (_advanced.Visible && _advanced.ContainsFocus && details.CanFocus) details.Focus();
                _advanced.Visible = !_advanced.Visible; details.Text = _advanced.Visible ? "Hide advanced" : "Advanced…";
                FindForm()?.PerformLayout();
            };
            _policy.AccessibleDescription = "Exact preserves proved native curve behavior. Error-bounded permits verified changes within the saved cumulative limits, with exact protection overrides.";
            _effort.AccessibleDescription = "Search harder using the same saved quality limits. Thorough does not allow worse motion.";
            _hints.SetToolTip(_policy, _policy.AccessibleDescription); _hints.SetToolTip(_effort, _effort.AccessibleDescription); _hints.SetToolTip(details, details.AccessibleDescription);
            Disposed += (s, e) => _hints.Dispose();
            _policy.SelectedIndexChanged += (s, e) => { UpdatePolicy(); SettingsChanged?.Invoke(this, EventArgs.Empty); };
            _effort.SelectedIndexChanged += (s, e) => SettingsChanged?.Invoke(this, EventArgs.Empty); UpdatePolicy();
            _policy.FontChanged += (s, e) => SizeChoices(); _effort.FontChanged += (s, e) => SizeChoices(); SizeChoices();
        }
        private void SizeChoices()
        {
            _policy.Width = Math.Max(145, TextRenderer.MeasureText("Error-bounded", _policy.Font).Width + SystemInformation.VerticalScrollBarWidth + 16);
            _effort.Width = Math.Max(110, TextRenderer.MeasureText("Thorough", _effort.Font).Width + SystemInformation.VerticalScrollBarWidth + 16);
        }
        private void UpdatePolicy()
        {
            bool bounded = _policy.SelectedIndex == (int)NativeExportOptimizationPolicy.ErrorBounded; _limits.Enabled = bounded;
            _description.Text = bounded ? "Smaller export within saved limits; protected motion stays exact. Unsupported cases use Exact."
                : "Exact export copy; editable animation stays unchanged.";
            _description.AccessibleDescription = _description.Text + " Advanced shows the saved cumulative limits and search effort. Only a separate export copy is optimized; ordinary Save preserves the editable master.";
            _hints.SetToolTip(_description, _description.AccessibleDescription);
        }
        internal NativeExportOptimizationSettings Read()
        {
            var settings = _initial.Clone(); settings.Policy = (NativeExportOptimizationPolicy)_policy.SelectedIndex; settings.SearchEffort = (NativeExportSearchEffort)_effort.SelectedIndex;
            settings.RotationDegrees = (double)_values[0].Value; settings.Translation = (double)_values[1].Value; settings.Scale = (double)_values[2].Value;
            settings.GeometryRelative = (double)_values[3].Value; settings.RotationDerivative = (double)_values[4].Value; settings.TranslationDerivative = (double)_values[5].Value; settings.ScaleDerivative = (double)_values[6].Value;
            settings.Validate(); return settings;
        }
    }
}
