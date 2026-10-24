using BrawlLib.Internal;
using BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase;
using BrawlLib.Internal.Windows.Controls.ModelViewer.Panels;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

internal static partial class Program
{
    private static void DispatcherTimingRegression()
    {
        using (var owner = new Control())
        {
            owner.CreateControl();
            var timer = new CoolTimer { UseUiDispatcher = true };
            int uiThread = Thread.CurrentThread.ManagedThreadId, calls = 0;
            bool onUiThread = true;
            double largestInterval = 0;
            timer.RenderFrame += (s, e) => {
                calls++; onUiThread &= Thread.CurrentThread.ManagedThreadId == uiThread;
                largestInterval = Math.Max(largestInterval, e.Time);
            };
            try
            {
                var clock = Stopwatch.StartNew();
                timer.Run(0, 60);
                Check(clock.ElapsedMilliseconds < 100 && timer.IsRunning, "Native preview timer starts without blocking the UI caller");
                while (clock.ElapsedMilliseconds < 180) { Application.DoEvents(); Thread.Sleep(1); }
                Check(calls >= 3 && onUiThread, "Coalesced message ticks deliver native animation work on its owning UI thread");
                Thread.Sleep(120);
                int before = calls;
                Application.DoEvents();
                Check(calls > before && calls <= before + 2 && largestInterval >= 0.1, "A stalled UI receives elapsed time without an accumulated frame callback queue");
                timer.Stop(); before = calls;
                Thread.Sleep(30); Application.DoEvents();
                Check(calls == before && !timer.IsRunning, "Stopping preview rejects stale timer ticks");
                timer.Run(0, 60); clock.Restart();
                while (clock.ElapsedMilliseconds < 80) { Application.DoEvents(); Thread.Sleep(1); }
                Check(calls > before && onUiThread, "Native preview timer restarts after Stop with fresh UI callbacks");
                timer.Stop(); Thread.Sleep(50); Application.DoEvents();
                GC.Collect(); GC.WaitForPendingFinalizers();
                int handles;
                using (var process = Process.GetCurrentProcess()) handles = process.HandleCount;
                for (int i = 0; i < 25; i++) { timer.Run(0, 60); timer.Stop(); }
                Thread.Sleep(150); Application.DoEvents();
                // Compare both sides after collecting finalizable framework
                // wrappers, rather than counting unrelated pending finalizers.
                GC.Collect(); GC.WaitForPendingFinalizers();
                using (var process = Process.GetCurrentProcess())
                    Check(process.HandleCount <= handles + 8,
                        "Repeated preview start/stop releases message timers without accumulating handles",
                        new { Before = handles, After = process.HandleCount });
            }
            finally { timer.Stop(); }
        }
    }

    private static void TransportTimingRegression(CHR0Node clip, string fighter)
    {
        using (var player = new PlaybackProbe(clip, true))
        {
            player.SetFrame(1);
            player.Tick(0.1 / 60);
            Thread.Sleep(40);
            player.Tick(1.0 / 60);
            player.Tick((clip.FrameCount - 1.0) / 60);
            int beforeSlider = ((TrackBar)player.PlaybackPanel.Controls.Find("trbFrame", true).Single()).Value;
            bool desynchronized = player.PlaybackPanel.numFrameIndex.Value == 1 && beforeSlider != 1 && player.CurrentFrame == 1;
            player.StopTransport();
            Check(desynchronized && ((TrackBar)player.PlaybackPanel.Controls.Find("trbFrame", true).Single()).Value == 1 && player.PlaybackPanel.numFrameIndex.Value == 1,
                fighter + ": pause synchronizes slider and frame edit when a quick loop returns to the same displayed number",
                new { BeforeSlider = beforeSlider, AfterSlider = ((TrackBar)player.PlaybackPanel.Controls.Find("trbFrame", true).Single()).Value });
        }
        using (var player = new PlaybackProbe(clip, true))
        {
            player.SetFrame(1);
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < 200; i++) player.Tick(1.0 / 600);
            Check(player.PlaybackPanel.DiagnosticPlaybackTextUpdates <= 2 + clock.Elapsed.TotalSeconds * 10,
                fighter + ": native playback avoids reformatting the frame edit on every pose");
            player.PlaybackPanel.numFrameIndex.Value = 3;
            player.Tick(0.5 / 60);
            Check(Math.Abs(player.Sample - 3.5f) < 0.001f,
                fighter + ": manual frame editing resynchronizes playback with throttled transport controls");
            player.SetFrame(4);
            Check(player.PlaybackPanel.numFrameIndex.Value == 4 && player.CurrentFrame == 4,
                fighter + ": explicit scrubbing updates the frame edit immediately");
        }
    }

    private static void PlaybackTimingRegression(CHR0Node clip, string fighter)
    {
        TransportTimingRegression(clip, fighter);
        using (var player = new PlaybackProbe(clip))
        {
            player.SetFrame(1);
            player.Tick(1.0 / 120);
            Check(Math.Abs(player.Sample - 1.5f) < 0.001f, fighter + ": native realtime playback samples subframes without modifying keys");
            player.Tick(0.1);
            Check(Math.Abs(player.Sample - 7.5f) < 0.001f, fighter + ": a delayed playback tick advances elapsed native time without a catch-up burst");
            player.SetFrame(2);
            player.Tick(1.0 / 60);
            Check(Math.Abs(player.Sample - 3) < 0.001f, fighter + ": manual scrub resynchronizes realtime playback");
            player.SetFrame(clip.FrameCount);
            player.Tick(2.0 / 60);
            Check(Math.Abs(player.Sample - 2) < 0.001f, fighter + ": realtime native looping preserves the authored duration");
            player._loop = false;
            player.SetFrame(clip.FrameCount - 1);
            player.Tick(0.1);
            Check(player.Stopped && player.CurrentFrame == clip.FrameCount, fighter + ": non-looping realtime playback stops exactly on the last native frame");
            player._timer.UseUiDispatcher = false;
            player.SetFrame(1);
            player.Tick(0.1);
            Check(player.CurrentFrame == 2, fighter + ": inherited frame-by-frame playback retains its deterministic stepping");
        }
    }

    private static void BodyPlaybackRegression(MDL0Node model, CHR0Node clip, string fighter)
    {
        using (var player = new BodyPlaybackProbe(model, clip))
        {
            double maximum = 0;
            for (int i = 0; i < clip.FrameCount * 3; i++)
            {
                player.Tick(0.5 / 60);
                var actual = model.PolygonList.OfType<MDL0ObjectNode>().SelectMany(o => o.Vertices).Select(v => v.WeightedPosition).ToArray();
                model.ApplyCHR(clip, player.Sample);
                model.ApplySRT(null, player.Sample); model.ApplySHP(null, player.Sample);
                model.ApplyPAT(null, player.Sample); model.ApplyCLR(null, player.Sample);
                model.ApplyVIS(null, 0); model.ApplySCN(null, player.Sample);
                var expected = model.PolygonList.OfType<MDL0ObjectNode>().SelectMany(o => o.Vertices).Select(v => v.WeightedPosition).ToArray();
                maximum = Math.Max(maximum, MaximumDistance(actual, expected));
            }
            Check(maximum < 0.00001, fighter + ": body-only native playback matches the complete inherited channel path across subframes and a loop", new { MaximumVertexDifference = maximum });
            player.SetFrame(2);
            Check(Math.Abs(player.Sample - 2) < 0.00001, fighter + ": explicit edits leave the body-only playback fast path immediately");
        }
    }

    private sealed class BodyPlaybackProbe : ModelEditorBase
    {
        private readonly MDL0Node _model;
        internal float Sample;
        internal BodyPlaybackProbe(MDL0Node model, CHR0Node clip)
        {
            _model = model; _chr0 = clip; _maxFrame = clip.FrameCount; _loop = true; _playing = true;
            PlayCHR0 = true;
            _timer = new CoolTimer { UseUiDispatcher = true, TargetRenderFrequency = 60 };
            SetFrame(1);
        }
        internal void Tick(double seconds) => _timer_RenderFrame(this, new FrameEventArgs(seconds));
        public override void UpdateModel(float frame) { Sample = frame; base.UpdateModel(_model, frame); }
    }

    // Exercise the actual player with a real native clip, without a GL handle.
    // Only rendering and stop UI are replaced; SetFrame and timing stay native.
    private sealed class PlaybackProbe : ModelEditorBase
    {
        internal float Sample;
        internal bool Stopped;
        private readonly ModelPlaybackPanel _panel;
        public override ModelPlaybackPanel PlaybackPanel => _panel;
        internal PlaybackProbe(CHR0Node clip, bool transport = false)
        {
            _chr0 = clip; _maxFrame = clip.FrameCount; _loop = true; _playing = true;
            _timer = new CoolTimer { UseUiDispatcher = true, TargetRenderFrequency = 60 };
            if (transport)
            {
                _panel = new ModelPlaybackPanel { _mainWindow = this };
                _panel.numFrameIndex.Maximum = clip.FrameCount + 1;
                Controls.Add(_panel);
            }
        }
        internal void Tick(double seconds) => _timer_RenderFrame(this, new FrameEventArgs(seconds));
        internal void StopTransport()
        {
            DisableBonesWhenPlaying = false;
            _interpolationEditor = new BrawlLib.Internal.Windows.Controls.InterpolationEditor { Visible = false };
            Controls.Add(_interpolationEditor);
            base.StopAnim();
        }
        public override void UpdateModel(float frame) { Sample = frame; }
        public override void StopAnim() { Stopped = true; }
    }
}
