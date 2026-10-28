using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    // Native owners and the frozen baseline belong to the controller. Only the
    // independently prepared preview candidate belongs to this dialog.
    internal sealed class AnimationOptimizationDialog : Form
    {
        internal readonly FittingModelView SourceView, TargetView;
        internal Func<bool, Task<CHR0Node>> Prepare;
        internal Action<bool, CHR0Node> Apply;
        internal Action CancelWork;
        internal string PreparedReport, PreparedSummary;
        internal bool PreparedCanApply;
        internal bool Busy { get; private set; }
        internal CHR0Node Candidate { get; private set; }

        readonly CHR0Node source;
        readonly WorkspaceExportOptimizationOptions exportOptions;
        readonly bool highQualityQualified;
        readonly TableLayoutPanel layout, views, transport;
        readonly FlowLayoutPanel footer;
        readonly Label identity, modeHint, summary;
        readonly Button analyze = new Button { Name = "optimizeAnalyze", Text = "Optimize", AutoSize = true };
        readonly Button apply = new Button { Name = "optimizeApply", Text = "Export optimized copy…", AutoSize = true, Enabled = false };
        readonly Button cancel = new Button { Name = "optimizationCancel", Text = "Cancel optimization", AutoSize = true, Enabled = false, Visible = false };
        readonly Button close = new Button { Name = "optimizationClose", Text = "Close", AutoSize = true };
        readonly Button details = new Button { Name = "optimizationDetails", Text = "Show details", AutoSize = true };
        readonly TextBox report = new TextBox { Name = "optimizationReport", ReadOnly = true, Multiline = true, WordWrap = false, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Visible = false };
        readonly Button play = new Button { Name = "optimizationPlay", Text = "Play preview", AutoSize = true };
        readonly Label frameText = new Label { Name = "optimizationFrameText", AutoSize = true, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 6, 0, 0) };
        readonly TrackBar frame;
        readonly Timer clock = new Timer { Interval = 33 };
        readonly Stopwatch previewTime = new Stopwatch();
        readonly Dictionary<Control, Control> regionFocus = new Dictionary<Control, Control>();
        int generation, previewStart;
        bool  preparedHighQuality, preparedSmallFile, preparedCompact, analyzingCompact, layingOut, analyzingHighQuality, analyzingSmallFile, cancellationRequested, closeQueued;

        internal AnimationOptimizationDialog(ResourceNode originalRoot, MDL0Node originalModel,
            ResourceNode resultRoot, MDL0Node resultModel, CHR0Node originalSnapshot,
            ModelPanelViewport viewer, IEnumerable<ResourceNode> references, string identityLabel,
            bool highQualityQualified = false)
        {
            if (originalSnapshot == null || originalSnapshot.FrameCount < 1) throw new ArgumentException("A frozen current animation is required.", nameof(originalSnapshot));
            source = originalSnapshot;
            this.highQualityQualified = highQualityQualified;
            Name = "animationOptimizationDialog";
            Text = "Optimize CHR0 size";
            AutoScaleMode = AutoScaleMode.None;
            Font = WorkspaceAppearance.Font;
            ClientSize = new Size(1060, 730);
            MinimumSize = new Size(760, 610);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            identity = new Label { Name = "optimizationIdentity", Text = identityLabel ?? originalModel.Name + " · " + source.Name,
                AutoSize = true, Margin = new Padding(0, 0, 0, 6), Font = WorkspaceAppearance.BoldFont };
            modeHint = new Label { Name = "optimizationModeHint", AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            summary = new Label { Name = "optimizationSummary", AutoSize = true, Margin = new Padding(0, 6, 0, 2),
                Text = "Optimize to compare native file sizes." };

            exportOptions = new WorkspaceExportOptimizationOptions(new NativeExportOptimizationPreferences().Read())
            { Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 0 };
            exportOptions.SettingsChanged += (s,e) => { if(!Busy) { Discard(); UpdateModeHint(); } };
            SourceView = new FittingModelView(originalModel, originalRoot, "Original current animation · read-only", true, viewer, references) { Dock = DockStyle.Fill };
            TargetView = new FittingModelView(resultModel, resultRoot, "Original animation · no result prepared", false, viewer, references) { Dock = DockStyle.Fill };
            views = new TableLayoutPanel { Name = "optimizationPreviews", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            views.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            views.Controls.Add(SourceView, 0, 0);
            views.Controls.Add(TargetView, 1, 0);

            frame = new TrackBar { Name = "optimizationFrame", Dock = DockStyle.Fill, Minimum = 1,
                Maximum = source.FrameCount, Value = 1, TickStyle = TickStyle.None, AccessibleName = "Preview frame, one-based" };
            transport = new TableLayoutPanel { Name = "optimizationTransport", Dock = DockStyle.Fill,
                AutoSize = true, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 4, 0, 0) };
            transport.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            transport.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            transport.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            transport.Controls.Add(play, 0, 0);
            transport.Controls.Add(frame, 1, 0);
            transport.Controls.Add(frameText, 2, 0);

            footer = new FlowLayoutPanel { Name = "optimizationActions", Dock = DockStyle.Fill, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = Padding.Empty };
            footer.Controls.AddRange(new Control[] { analyze, cancel, apply, close });
            analyze.AccessibleDescription = "Prepare an independent result for this exact current target animation. The authoring animation remains unchanged; only a separately verified export copy is published.";
            cancel.AccessibleDescription = "Cancel the pending optimization and keep the editable animation unchanged.";
            apply.AccessibleDescription = "Export the prepared independent CHR0 copy. The current animation and native Undo remain unchanged.";
            details.AccessibleDescription = "Show measured key counts, native byte sizes, errors, protections and tracks absent from the target that will be omitted from the export copy.";

            layout = new TableLayoutPanel { Name = "optimizationLayout", Dock = DockStyle.Fill, ColumnCount = 1,
                RowCount = 9, Padding = new Padding(8), Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int row = 0; row < 9; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles[3] = new RowStyle(SizeType.Percent, 100);
            layout.RowStyles[7] = new RowStyle(SizeType.Absolute, 0);
            layout.Controls.Add(identity, 0, 0);
            layout.Controls.Add(exportOptions, 0, 1);
            layout.Controls.Add(modeHint, 0, 2);
            layout.Controls.Add(views, 0, 3);
            layout.Controls.Add(transport, 0, 4);
            layout.Controls.Add(summary, 0, 5);
            layout.Controls.Add(details, 0, 6);
            layout.Controls.Add(report, 0, 7);
            layout.Controls.Add(footer, 0, 8);
            Controls.Add(layout);

            analyze.Click += async (s, e) => await Analyze();
            cancel.Click += (s, e) => CancelAnalysis();
            apply.Click += (s, e) => ApplyPrepared();
            close.Click += (s, e) => Close();
            details.Click += (s, e) => ToggleDetails();
            frame.ValueChanged += (s, e) => ShowFrame();
            play.Click += (s, e) => TogglePlayback();
            clock.Tick += (s, e) => frame.Value = 1 + (previewStart - 1 + (int)(previewTime.Elapsed.TotalSeconds * 60)) % frame.Maximum;
            CancelButton = close;
            FormClosing += (s, e) =>
            {
                if (Busy)
                {
                    // Parent-owned native preview roots cannot be released at
                    // modal return while their preparation task is still using
                    // them. Finish the pending await before allowing Close.
                    e.Cancel = true;
                    closeQueued = true;
                    close.Enabled = false;
                    CancelAnalysis();
                    return;
                }
                generation++; Stop(); CancelWork?.Invoke();
            };
            FormClosed += (s, e) => { Discard(false); clock.Dispose(); };
            ClientSizeChanged += (s, e) => LayoutText();
            Shown += (s, e) => { ShowFrame(); SourceView.ShownFrame(); TargetView.ShownFrame(); analyze.Focus(); };
            UpdateModeHint();
            ShowFrame();
            report.Text = "Optimize to see key and native byte counts, independently measured errors, retained protections and any tracks absent from this exact target skeleton.";
            DarkTheme.Apply(this);
            ApplyTextSize();
        }

        // Background harness policy probes are not product mode selectors.
        internal bool? TestHighQuality, TestSmallFile, TestCompactStorage;
        internal NativeExportOptimizationSettings ReadExportSettings() => exportOptions.Read();
        internal bool ReadHighQuality() => TestHighQuality ?? (ReadExportSettings().Policy == NativeExportOptimizationPolicy.ErrorBounded && highQualityQualified);
        internal bool ReadSmallFile() => TestSmallFile ?? ReadHighQuality();
        internal bool ReadCompactStorage() => TestCompactStorage ?? true;

        internal async Task Analyze()
        {
            if (Busy || IsDisposed) return;
            Discard();
            int token = ++generation;
            bool quality = ReadHighQuality();
            analyzingHighQuality = quality;
            analyzingSmallFile = ReadSmallFile();
            analyzingCompact = ReadCompactStorage();
            cancellationRequested = false;
            PreparedReport = PreparedSummary = null;
            PreparedCanApply = false;
            SetBusy(true);
            summary.Text = "Analyzing the frozen current animation...";
            report.Text = "Preparing native curves and measurements offline.";
            try
            {
                if (Prepare == null) throw new InvalidOperationException("Animation analysis is unavailable.");
                var result = await Prepare(quality);
                if (IsDisposed || Disposing || token != generation)
                { if (!ReferenceEquals(result, source)) result?.Dispose(); return; }
                if (result == null) throw new InvalidOperationException("No optimization result was prepared.");
                if (ReferenceEquals(result, source)) throw new InvalidOperationException("The preview must own an independent animation result.");
                if (result.FrameCount != source.FrameCount || result.Loop != source.Loop)
                { result.Dispose(); throw new InvalidOperationException("Optimization changed the native duration or loop flag. The result was discarded."); }
                Candidate = result;
                preparedHighQuality = quality;
                preparedSmallFile = analyzingSmallFile;
                preparedCompact = analyzingCompact;
                TargetView.Heading.Text = "Decoded optimized export copy · read-only";
                summary.Text = PreparedSummary ?? "Optimization ready. Compare the original and result, then export a separate copy. The master and native Undo stay unchanged.";
                report.Text = PreparedReport ?? "Analysis prepared. No detailed measurements were supplied.";
                ShowFrame();
            }
            catch (Exception error)
            {
                if (!IsDisposed && !Disposing && token == generation)
                { Discard(); summary.Text = error is OperationCanceledException ? "Analysis cancelled; the current animation is unchanged." : error.Message; report.Text = error.Data["OptimizationDetails"] as string ?? summary.Text; }
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                {
                    bool cancelled = cancellationRequested;
                    cancellationRequested = false;
                    if (cancelled) PreparedCanApply = false;
                    // Cancellation invalidates publication, but Busy belongs
                    // to the actual outstanding task rather than its generation.
                    SetBusy(false);
                    if (cancelled)
                    {
                        Discard();
                        summary.Text = "Analysis cancelled; the current animation is unchanged.";
                        report.Text = summary.Text;
                        if (!closeQueued) analyze.Focus();
                    }
                    if (closeQueued)
                    {
                        closeQueued = false;
                        close.Enabled = true;
                        Close();
                    }
                }
            }
        }

        void ApplyPrepared()
        {
            if (Busy || Candidate == null || !PreparedCanApply) return;
            Stop();
            try
            {
                if (preparedHighQuality != ReadHighQuality() || preparedSmallFile != ReadSmallFile() || preparedCompact != ReadCompactStorage()) throw new InvalidOperationException("The optimization settings changed. Analyze again.");
                if (Apply == null) throw new InvalidOperationException("Exporting the optimized copy is unavailable.");
                Apply(preparedHighQuality, Candidate);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception error)
            { Discard(); summary.Text = error.Message; report.Text = error.Message; analyze.Focus(); }
        }

        void CancelAnalysis()
        {
            if (!Busy || cancellationRequested) return;
            generation++;
            cancellationRequested = true;
            PreparedCanApply = false;
            Stop();
            cancel.Enabled = false;
            summary.Text = "Cancelling analysis; the current animation is unchanged.";
            report.Text = summary.Text;
            CancelWork?.Invoke();
        }

        void SetBusy(bool value)
        {
            bool restoreFocus = cancel.ContainsFocus;
            bool startFocus = analyze.ContainsFocus;
            Busy = value;
            cancel.Visible = value;
            cancel.Enabled = value;
            analyze.Visible = !value;
            analyze.Enabled = play.Enabled = frame.Enabled = exportOptions.Enabled = !value;
            apply.Enabled = !value && Candidate != null && PreparedCanApply;
            if (value && startFocus) cancel.Focus();
            if (!value && restoreFocus) analyze.Focus();
        }

        void Discard(bool showOriginal = true)
        {
            Stop();
            if (showOriginal) TargetView.Pose(source, frame.Value);
            var old = Candidate;
            Candidate = null;
            old?.Dispose();
            PreparedCanApply = false;
            apply.Enabled = false;
            TargetView.Heading.Text = "Original animation · no result prepared";
        }

        void UpdateModeHint()
        {
            modeHint.Visible = false;
            modeHint.Text = string.Empty;
            modeHint.AccessibleDescription = "Optimize a separate native export copy. Exact is the default; Error-bounded requires verified protections. " +
                "Up to 0.1° rotation, 0.002 translation and 0.0001 scale; root motion and pin/contact protections stay exact. Details report the actual result.";
            AccessibleDescription = modeHint.AccessibleDescription + " Advanced shows the saved limits and search effort. F6 and Shift+F6 move between settings, native previews, playback, report and export actions.";
        }

        void ShowFrame()
        {
            SourceView.Pose(source, frame.Value);
            TargetView.Pose(Candidate ?? source, frame.Value);
            frameText.Text = frame.Value + " / " + source.FrameCount;
        }

        void TogglePlayback()
        {
            if (Busy) return;
            if (clock.Enabled) { Stop(); return; }
            previewStart = frame.Value;
            previewTime.Restart();
            clock.Start();
            play.Text = "Pause preview";
        }

        void Stop()
        {
            clock.Stop();
            previewTime.Stop();
            play.Text = "Play preview";
        }

        void ToggleDetails()
        {
            if (report.Visible && report.ContainsFocus) details.Focus();
            bool visible = !report.Visible;
            report.Visible = visible;
            details.Text = visible ? "Hide details" : "Show details";
            layout.RowStyles[3].Height = visible ? 68 : 100;
            layout.RowStyles[7].SizeType = visible ? SizeType.Percent : SizeType.Absolute;
            layout.RowStyles[7].Height = visible ? 32 : 0;
            LayoutText();
        }

        internal void ApplyTextSize()
        {
            Font = WorkspaceAppearance.Font;
            foreach (var control in Descendants(this)) control.Font = Font;
            identity.Font = WorkspaceAppearance.BoldFont;
            SourceView.Heading.Height = TargetView.Heading.Height = Math.Max(38, Font.Height * 2 + 12);
            frame.Height = Math.Max(38, Font.Height + 18);
            LayoutText();
        }

        void LayoutText()
        {
            if (layout == null || layingOut) return;
            layingOut = true;
            try
            {
                int width = Math.Max(120, layout.ClientSize.Width - layout.Padding.Horizontal - 6);
                foreach (var label in new[] { identity, modeHint, summary }) label.MaximumSize = new Size(width, 0);
                layout.PerformLayout();
            }
            finally { layingOut = false; }
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == Keys.F6 || keyData == (Keys.Shift | Keys.F6))
            {
                Control[] regions = { exportOptions, SourceView.Viewport, TargetView.Viewport, transport, report, footer };
                int at = Array.FindIndex(regions, c => c.ContainsFocus);
                if (at >= 0) regionFocus[regions[at]] = Descendants(regions[at]).FirstOrDefault(c => c.Focused) ?? regions[at];
                int direction = keyData == Keys.F6 ? 1 : -1;
                for (int step = 1; step <= regions.Length; step++)
                {
                    var next = regions[(Math.Max(0, at) + direction * step + regions.Length * 2) % regions.Length];
                    if (!next.Visible || !next.Enabled) continue;
                    if (regionFocus.TryGetValue(next, out var previous) && previous.CanFocus && previous.Visible && previous.Enabled) previous.Focus();
                    else if (!next.Focus()) next.SelectNextControl(null, true, true, true, false);
                    return true;
                }
                return true;
            }
            return base.ProcessCmdKey(ref message, keyData);
        }

        static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
        }
    }
}
