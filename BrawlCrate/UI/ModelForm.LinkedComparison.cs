using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.Internal;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private Panel _comparisonPane;
        private ModelPanel _comparisonViewport;
        private Label _comparisonStatus, _comparisonTime;
        private LibrarySourceLease _comparisonLease;
        private CHR0Node _comparisonClip;
        private MDL0Node _comparisonTarget;
        private Func<float, float> _comparisonMap;
        private int _comparisonFrames;
        private bool _comparisonLoop;
        private object _comparisonOpener;
        private string _comparisonProvenance;
        internal float ComparisonSourceFrame { get; private set; }
        internal int ComparisonSampleCount { get; private set; }

        private static string ComparisonSourceKey(LibraryClipReference source)
        {
            var copy = source.Copy();
            copy.TargetRig = copy.TargetModelContentSha256 = copy.MappingSha256 = copy.InitialBakeSha256 = null;
            copy.LastExportSha256 = copy.LastExportFormat = null;
            return new JavaScriptSerializer().Serialize(copy);
        }

        private void BuildComparisonPane()
        {
            if (_comparisonPane != null) return;
            _comparisonPane = new Panel { Name = "workspaceComparison", Dock = DockStyle.Left, Visible = false, Padding = new Padding(8) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var close = new WorkspaceButton { Name = "workspaceCloseComparison", Text = "Close comparison", Dock = DockStyle.Top, AutoSize = true, TabIndex = 0,
                AccessibleDescription = "Return to the editable target. Escape closes comparison when focus is in this pane. F6 moves between regions." };
            close.Click += (s, e) => CloseLinkedComparison();
            _comparisonStatus = new Label { Name = "workspaceComparisonStatus", AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false, Padding = new Padding(0, 8, 0, 8) };
            _comparisonTime = new Label { Name = "workspaceComparisonTime", AutoSize = true, Dock = DockStyle.Fill, UseMnemonic = false };
            _comparisonViewport = new ModelPanel { Name = "workspaceComparisonViewport", AccessibleName = "Read-only original source", Dock = DockStyle.Fill, TabIndex = 1 };
            _comparisonViewport.CurrentViewport.AllowSelection = false;
            _comparisonViewport.CurrentViewport.RenderFloor = true;
            _comparisonViewport.CurrentViewport.BackgroundColor = Color.FromArgb(35, 39, 46);
            layout.Controls.Add(close, 0, 0); layout.Controls.Add(_comparisonStatus, 0, 1);
            layout.Controls.Add(_comparisonViewport, 0, 2); layout.Controls.Add(_comparisonTime, 0, 3);
            _comparisonPane.Controls.Add(layout); _simpleWorkspaceBody.Controls.Add(_comparisonPane);
            PrepareWorkspaceTabOrder(_comparisonPane); DarkTheme.Apply(_comparisonPane);
            _comparisonPane.AccessibleDescription = "Read-only original source linked to the editable target timeline. F6 moves between regions; Escape closes this pane.";
        }

        private void OpenLinkedComparison()
        {
            if (SimpleWorkspaceBusy) throw new InvalidOperationException("Finish the current operation before comparing.");
            HideBatchReview(); HideLoopInspection();
            BuildComparisonPane();
            _comparisonOpener = _workspaceInvoker ?? CurrentWorkspaceFocus();
            if (_libraryPane?.Visible == true) SetSimpleWorkspaceLibraryVisible(false);
            _comparisonPane.Visible = true; UpdateSimpleWorkspace();
            if (_comparisonLease != null && ReferenceEquals(_comparisonClip, modelEditControl1.TargetAnimation) &&
                ReferenceEquals(_comparisonTarget, modelEditControl1.TargetModel) &&
                _comparisonProvenance == new JavaScriptSerializer().Serialize(SelectedFighterAsset()?.Retarget))
            { SampleLinkedComparison(modelEditControl1.CurrentFrame); FocusWorkspaceRegion(_comparisonPane); return; }
            ReleaseComparisonSource();
            var clip = modelEditControl1.TargetAnimation as CHR0Node;
            var target = modelEditControl1.TargetModel as MDL0Node;
            var asset = SelectedFighterAsset(); var reference = asset?.Retarget?.Library;
            LibrarySourceLease loaded = null;
            try
            {
                if (clip == null || target == null || reference == null || asset.Retarget.ReviewDraft)
                    throw new InvalidDataException("This animation has no accepted, recorded library source. Native target editing remains available.");
                if (reference.Channel != "CHR0 skeletal" || reference.TicksPerSecond != 60 || reference.FrameCount < 1 ||
                    asset.Retarget.SourceAnimationId != reference.ClipId || asset.Retarget.SourceRig != reference.SourceRig)
                    throw new InvalidDataException("The recorded source identity or native timebase cannot be verified.");
                Func<float, float> map;
                var timing = asset.Retarget.Timing;
                if (timing == null)
                {
                    if (clip.FrameCount != reference.FrameCount || clip.Loop && !reference.Loop)
                        throw new InvalidDataException("The target duration or loop changed without a verified source timing map.");
                    map = frame => frame;
                }
                else
                {
                    timing.Validate(reference.FrameCount);
                    if (clip.FrameCount != timing.Frames || clip.Loop)
                        throw new InvalidDataException("The target no longer matches its recorded trim/timing fit.");
                    int start = timing.Start, end = timing.End, frames = timing.Frames;
                    map = frame => frame <= 0 ? 0 : 1 + start + (frames == 1 ? 0 : (end - start) * ((frame - 1) / (frames - 1)));
                }
                modelEditControl1.StopAnim();
                var project = _fighterProject;
                string content = ExportContent(clip), document = project.DocumentIdentity();
                string rig = BrawlRetargeter.SkeletonFingerprint(target), sourceKey = ComparisonSourceKey(reference);
                _library = _library ?? new AnimationLibrary();
                _workspaceBusy = true;
                try { UpdateSimpleWorkspace(); loaded = LoadLibrarySource(reference.ClipId); }
                finally { _workspaceBusy = false; }
                if (loaded == null) { _comparisonStatus.Text = "Comparison loading cancelled. Target editing remains available."; return; }
                if (!ReferenceEquals(project, _fighterProject) || !ReferenceEquals(target, modelEditControl1.TargetModel) ||
                    !ReferenceEquals(clip, modelEditControl1.TargetAnimation) || content != ExportContent(clip) ||
                    document != project.DocumentIdentity() || rig != BrawlRetargeter.SkeletonFingerprint(target))
                    throw new InvalidDataException("The target or project changed while loading. Compare again to review the current selection.");
                if (sourceKey != ComparisonSourceKey(loaded.Reference))
                    throw new InvalidDataException("The resolved source differs from this result's recorded identity, hashes, revision or timing.");
                _comparisonLease = loaded; loaded = null;
                _comparisonClip = clip; _comparisonTarget = target; _comparisonMap = map;
                _comparisonFrames = clip.FrameCount; _comparisonLoop = clip.Loop;
                _comparisonProvenance = new JavaScriptSerializer().Serialize(asset.Retarget);
                _comparisonLease.Model.ResetToBindState();
                _comparisonViewport.AddReference(_comparisonLease.Model.RootNode);
                _comparisonViewport.AddTarget(_comparisonLease.Model);
                _comparisonViewport.PrepareModelTextures(_comparisonLease.Model);
                _comparisonStatus.Text = "Original source (read-only)\n" + _comparisonLease.Model.Name + " / " + _comparisonLease.Animation.Name +
                    "\nLinked to the target timeline";
                SampleLinkedComparison(modelEditControl1.CurrentFrame); FrameComparisonSource();
            }
            catch (Exception error) { ReleaseComparisonSource(); _comparisonStatus.Text = "Linked comparison unavailable\n" + error.Message; }
            finally
            {
                loaded?.Dispose(); _workspaceBusy = false; UpdateSimpleWorkspace();
                _comparisonStatus.AccessibleDescription = _comparisonStatus.Text;
                FocusWorkspaceRegion(_comparisonPane, remember: false);
            }
        }

        // Called by the native evaluator with its actual fractional playback sample.
        // This path uses only prepared in-memory native curves: no I/O, hashing or bake.
        private void SampleLinkedComparison(float frame)
        {
            if (_comparisonLease == null) return;
            if (!ReferenceEquals(_comparisonClip, modelEditControl1.TargetAnimation) ||
                !ReferenceEquals(_comparisonTarget, modelEditControl1.TargetModel) ||
                _comparisonClip.FrameCount != _comparisonFrames || _comparisonClip.Loop != _comparisonLoop)
            { ReleaseComparisonSource(); _comparisonStatus.Text = "Linked comparison unavailable\nTarget selection or timing changed. Choose Compare source again."; return; }
            // A retained lease lets the drawer reopen without loading again.
            // Hidden comparison is dormant: native skinning can be expensive
            // even when the source viewport itself is not being painted.
            if (_comparisonPane?.Visible != true) return;
            ComparisonSourceFrame = _comparisonMap(frame);
            _comparisonLease.Model.ApplyCHR(frame <= 0 ? null : _comparisonLease.Animation, ComparisonSourceFrame);
            ComparisonSampleCount++;
            _comparisonTime.Text = frame <= 0 ? "Bind poses · use the target timeline" :
                "Source " + ComparisonSourceFrame.ToString("0.###") + " / target " + frame.ToString("0.###");
            _comparisonViewport.Invalidate();
        }

        private void FrameComparisonSource()
        {
            var box = _comparisonLease.Model.GetBox(); var center = (box.Min + box.Max) * .5f; var half = (box.Max - box.Min) * .65f;
            var camera = _comparisonViewport.Camera;
            float tangent = (float)Math.Tan(camera.VerticalFieldOfView * Math.PI / 360);
            float aspect = Math.Max(.1f, (float)_comparisonViewport.Width / Math.Max(1, _comparisonViewport.Height));
            float distance = Math.Max(half._y / tangent, half._x / (tangent * aspect)) + half._z + Math.Max(1, camera.NearDepth * 2);
            camera.Set(center + new Vector3(0, 0, distance), new Vector3(), new Vector3(1));
            _comparisonViewport.CurrentViewport._viewDistance = distance;
        }

        private void ReleaseComparisonSource()
        {
            if (_comparisonLease == null) return;
            _comparisonViewport.ClearAll(); _comparisonLease.Dispose(); _comparisonLease = null;
            _comparisonMap = null; _comparisonClip = null; _comparisonTarget = null;
            _comparisonTime.Text = "";
        }

        private void CloseLinkedComparison()
        {
            ReleaseComparisonSource();
            if (_comparisonPane == null) return;
            _comparisonPane.Visible = false; UpdateSimpleWorkspace(); RestoreWorkspaceFocus(_comparisonOpener);
        }
    }
}
