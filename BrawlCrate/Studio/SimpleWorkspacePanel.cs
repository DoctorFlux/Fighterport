using System;
using System.Drawing;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    // Presentation only: the native model window remains the document owner.
    internal sealed class SimpleWorkspacePanel : UserControl
    {
        private readonly TableLayoutPanel _layout;
        private readonly Label _heading;
        private readonly Label _hint;
        private readonly LinkLabel _source;
        private readonly LinkLabel _target;
        private readonly LinkLabel _result;
        private readonly Label _readiness;
        private readonly Button _primary;
        private readonly Button _browse;
        private readonly Button _addSource;
        private readonly Button _repairSource;
        private readonly Button _changeTarget;
        private readonly Button _audition;
        private readonly Button _controls;
        private readonly Button _motions;
        private readonly Button _transitions;
        private readonly Button _timing;
        private readonly Button _save;
        private readonly Button _details;
        private readonly Font _headingFont;
        private readonly Font _sectionFont;
        private int _stage;
        private bool _hasSource;
        private bool _hasTarget;
        private bool _canTransfer;
        private bool _canExport;
        private bool _busy;
        private bool _canRepair;
        private string _readinessText;

        internal event Action<int> StageRequested;
        internal event Action<string> ActionRequested;

        internal int Stage
        {
            get { return _stage; }
            set { SetStage(value); }
        }

        internal SimpleWorkspacePanel()
        {
            Name = "simpleWorkspacePanel";
            AccessibleName = "Animation transfer workspace";
            Dock = DockStyle.Right;
            Width = 250;
            AutoScroll = true;
            Padding = new Padding(12);
            _headingFont = new Font(Font.FontFamily, Font.Size + 2, FontStyle.Bold);
            _sectionFont = new Font(Font, FontStyle.Bold);

            _layout = new TableLayoutPanel
            {
                Name = "workspaceTaskLayout",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(_layout);

            _heading = TextLabel("workspaceStageHeading", "", 8);
            _heading.Font = _headingFont;
            _hint = TextLabel("workspaceStageHint", "", 16);
            AddRow(_heading);
            AddRow(_hint);
            _source = AddSummary("Source", "workspaceSourceSummary", 0);
            _target = AddSummary("Target", "workspaceTargetSummary", 1);
            _result = AddSummary("Result", "workspaceResultSummary", 2);
            _readiness = TextLabel("workspaceReadiness", "", 8);
            _readiness.AccessibleName = "Workspace readiness";
            AddRow(_readiness);

            _primary = ActionButton("workspacePrimaryAction", "", "source");
            _primary.MinimumSize = new Size(0, 38);
            _primary.MaximumSize = new Size(0, 38);
            _primary.Height = 38;
            _primary.Font = _sectionFont;
            _primary.Margin = new Padding(0, 6, 0, 12);
            AddRow(_primary);
            _browse = AddAction("workspaceBrowseSourcesAction", "Browse sources", "source");
            _addSource = AddAction("workspaceAddSourceAction", "Add source...", "add-source");
            _repairSource = AddAction("workspaceRepairSourceAction", "Repair source...", "repair-source");
            _changeTarget = AddAction("workspaceChangeTargetAction", "Change target model", "target");
            _audition = AddAction("workspaceAuditionAction", "Preview original", "audition");
            _controls = AddAction("workspaceControlsAction", "Set up controls", "controls");
            _motions = AddAction("workspaceMotionsAction", "Add target animations", "motions");
            _transitions = AddAction("workspaceTransitionsAction", "Fit start or end", "transitions");
            _timing = AddAction("workspaceTimingAction", "Source timing / batch", "timing");
            _save = AddAction("workspaceSaveAction", "Save project", "save");
            _details = AddAction("workspaceDetailsAction", "More tools", "details");
            _details.Margin = new Padding(0, 12, 0, 6);

            SizeChanged += (sender, args) => UpdateTextWidths();
            DarkTheme.Apply(this);
            foreach (var summary in new[] { _source, _target, _result })
            {
                summary.LinkColor = Color.LightSteelBlue;
                summary.ActiveLinkColor = Color.White;
                summary.VisitedLinkColor = summary.LinkColor;
            }
            SetState(null, null, null, false, false, false, false, false);
            UpdateTextWidths();
        }

        internal void SetStage(int stage)
        {
            if (stage < 0 || stage > 3)
                throw new ArgumentOutOfRangeException(nameof(stage));
            _stage = stage;
            UpdateActions();
        }

        internal void SetState(string source, string target, string result,
            bool hasSource, bool hasTarget, bool canTransfer, bool canExport, bool busy,
            string readiness = null, bool canRepair = false)
        {
            _source.Text = Summary(source, "No source selected");
            _target.Text = Summary(target, "No target model open");
            _result.Text = Summary(result, "No transfer result");
            _hasSource = hasSource;
            _hasTarget = hasTarget;
            _canTransfer = canTransfer;
            _canExport = canExport;
            _busy = busy;
            _readinessText = readiness;
            _canRepair = canRepair;
            UpdateActions();
        }

        private void UpdateActions()
        {
            SuspendLayout();
            _layout.SuspendLayout();
            try
            {
                _heading.Text = new[] { "1  Source", "2  Target", "3  Preview and fix", "4  Export" }[_stage];
                _hint.Text = new[]
                {
                    "Choose animations from the source library. Each clip keeps its original model.",
                    "Open the model you want to animate. Native editing also works without a source clip.",
                    "Preview the transfer, then use the full native editor to review and adjust the result.",
                    "Export the selected target animation or save the project to continue later."
                }[_stage];
                string action;
                string label;
                bool enabled = true;
                switch (_stage)
                {
                    case 0:
                        action = _hasSource ? "continue" : "source";
                        label = !_hasSource ? "Browse sources" :
                            _hasTarget ? "Continue to preview" : "Continue to target";
                        break;
                    case 1:
                        action = _hasTarget ? "continue" : "target";
                        label = _hasTarget ? "Continue to preview" : "Open target model";
                        break;
                    case 2:
                        action = !_hasSource ? "source" : !_hasTarget ? "target" : "transfer";
                        label = !_hasSource ? "Choose source" :
                            !_hasTarget ? "Open target model" : "Preview transfer";
                        enabled = action != "transfer" || _canTransfer;
                        break;
                    default:
                        action = "export";
                        label = "Export result";
                        enabled = _canExport;
                        break;
                }
                _primary.Text = label;
                _primary.AccessibleName = _primary.Text;
                _primary.Tag = action;
                _primary.Enabled = !_busy && enabled;
                _readiness.Text = string.IsNullOrWhiteSpace(_readinessText) ? DefaultReadiness() : _readinessText;
                _readiness.AccessibleDescription = _readiness.Text;

                _browse.Visible = _stage == 0 && _hasSource;
                _browse.Enabled = !_busy;
                _addSource.Visible = _stage == 0;
                _addSource.Enabled = !_busy;
                _repairSource.Visible = _stage == 0 && _canRepair;
                _repairSource.Enabled = !_busy && _canRepair;
                _changeTarget.Visible = _stage == 1 && _hasTarget;
                _changeTarget.Enabled = !_busy && _hasTarget;

                _audition.Visible = _stage == 0;
                _audition.Enabled = _hasSource && !_busy;
                _controls.Visible = _stage == 1 || _stage == 2;
                _controls.Text = _stage == 2 ? "Fix hand or foot" : "Set up controls";
                _controls.AccessibleName = _controls.Text;
                _controls.Enabled = _hasTarget && !_busy;
                _motions.Visible = _stage == 1;
                _motions.Enabled = _hasTarget && !_busy;
                _transitions.Visible = _stage == 2;
                _transitions.Enabled = _hasTarget && _canExport && !_busy;
                _timing.Visible = _stage == 2;
                _timing.Enabled = _hasSource && _hasTarget && !_busy;
                _save.Visible = _stage == 3;
                _save.Enabled = _hasTarget && !_busy;
                _details.Enabled = !_busy;
            }
            finally
            {
                _layout.ResumeLayout(true);
                ResumeLayout(true);
            }
        }

        private string DefaultReadiness()
        {
            if (_busy) return "Finish or cancel the current operation before continuing.";
            switch (_stage)
            {
                case 0:
                    return _hasSource ? "Source ready. Continue, or preview the original animation." :
                        "Choose an available animation with its original source model.";
                case 1:
                    return _hasTarget ? "Target ready. Continue to preview or edit the model directly." :
                        "Open an existing model. Native editing does not require a source animation.";
                case 2:
                    if (!_hasSource) return "Choose a source for transfer. Existing target animations remain editable.";
                    if (!_hasTarget) return "Open the target model before previewing a transfer.";
                    return _canTransfer ? "Ready to preview transfer. Review the resulting motion in the native editor." :
                        "Review the current source and target before transfer.";
                default:
                    return _canExport ? "The selected target animation can be exported as native Brawl CHR0." :
                        "Select an accepted target skeletal animation before exporting.";
            }
        }

        private LinkLabel AddSummary(string caption, string name, int stage)
        {
            var title = TextLabel(name + "Heading", caption, 3);
            title.Font = _sectionFont;
            AddRow(title);
            var summary = new LinkLabel
            {
                Name = name,
                AccessibleName = caption + " summary",
                AutoSize = true,
                Dock = DockStyle.Fill,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 0, 14),
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            summary.LinkClicked += (sender, args) => StageRequested?.Invoke(stage);
            AddRow(summary);
            return summary;
        }

        private static Label TextLabel(string name, string text, int bottomMargin)
        {
            return new Label
            {
                Name = name,
                AccessibleName = name == "workspaceStageHeading" ? "Current workspace stage" : text,
                Text = text,
                AutoSize = true,
                Dock = DockStyle.Fill,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 0, bottomMargin)
            };
        }

        private Button ActionButton(string name, string text, string action)
        {
            var button = new Button
            {
                Name = name,
                AccessibleName = text,
                Text = text,
                Tag = action,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 30),
                MaximumSize = new Size(0, 30),
                Height = 30,
                Margin = new Padding(0, 0, 0, 6),
                UseMnemonic = false
            };
            button.Click += (sender, args) => ActionRequested?.Invoke((string)button.Tag);
            return button;
        }

        private Button AddAction(string name, string text, string action)
        {
            var button = ActionButton(name, text, action);
            AddRow(button);
            return button;
        }

        private void AddRow(Control control)
        {
            int row = _layout.RowCount++;
            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.Controls.Add(control, 0, row);
        }

        private void UpdateTextWidths()
        {
            int width = Math.Max(1, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
            foreach (Control control in _layout.Controls)
                if (control is Label)
                    control.MaximumSize = new Size(width, 0);
        }

        private static string Summary(string text, string fallback)
        {
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _headingFont?.Dispose();
                _sectionFont?.Dispose();
            }
        }
    }
}
