using BrawlLib.OpenGL;
using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace BrawlLib.Internal
{
    // The worker requests a coalesced WM_PAINT, never an animation callback.
    // A subsequent deadline is armed only after the UI finishes the prior paint.
    internal sealed class PaintFrameClock : IDisposable
    {
        private sealed class TimerHandle : WaitHandle
        {
            internal TimerHandle(SafeWaitHandle handle) { SafeWaitHandle = handle; }
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeWaitHandle CreateWaitableTimerExW(IntPtr attributes, string name, uint flags, uint access);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetWaitableTimer(SafeWaitHandle timer, ref long due, int period, IntPtr callback, IntPtr argument, bool resume);
        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr window, IntPtr rectangle, bool erase);

        private readonly GLPanel _panel;
        private readonly IntPtr _window;
        private readonly Action _tick;
        private readonly Func<double> _period;
        private readonly Action _fallback;
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private readonly TimerHandle _timer;
        private readonly object _gate = new object();
        private bool _disposed, _paintTick;
        private int _requested;
        private long _deadline;

        internal PaintFrameClock(GLPanel panel, Action tick, Func<double> period, Action fallback)
        {
            _panel = panel; _window = panel.Handle; _tick = tick; _period = period; _fallback = fallback;
            var handle = CreateWaitableTimerExW(IntPtr.Zero, null, 2, 0x00100002);
            if (handle.IsInvalid && Marshal.GetLastWin32Error() == 87)
            {
                handle.Dispose(); handle = CreateWaitableTimerExW(IntPtr.Zero, null, 0, 0x00100002);
            }
            if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); _stop.Dispose(); throw new Win32Exception(error); }
            _timer = new TimerHandle(handle);
            _panel.FrameStarting += Starting;
            _panel.FrameCompleted += Completed;
            _panel.HandleDestroyed += Destroyed;
            var thread = new Thread(() =>
            {
                try
                {
                    var waits = new WaitHandle[] { _stop, _timer };
                    while (WaitHandle.WaitAny(waits) == 1)
                    {
                        lock (_gate)
                        {
                            if (_disposed) break;
                            Interlocked.Exchange(ref _requested, 1);
                            InvalidateRect(_window, IntPtr.Zero, false);
                        }
                    }
                }
                finally { _timer.Dispose(); _stop.Dispose(); }
            }) { IsBackground = true, Name = "Viewport paint deadline" };
            bool started = false;
            try { thread.Start(); started = true; Arm(0.001); }
            catch
            {
                Dispose();
                if (!started) { _timer.Dispose(); _stop.Dispose(); }
                throw;
            }
        }

        private void Starting(object sender, EventArgs e)
        {
            if (_disposed || Interlocked.Exchange(ref _requested, 0) == 0) return;
            _paintTick = true;
            long now = Stopwatch.GetTimestamp(), period = (long)(_period() * Stopwatch.Frequency);
            // Carry the phase forward rather than adding wakeup latency every
            // frame. Drop missed deadlines instead of replaying old poses.
            _deadline = _deadline == 0 ? now + period : _deadline + period;
            if (_deadline <= now) _deadline = now + period;
            _tick();
        }
        private void Completed(object sender, EventArgs e)
        {
            if (!_paintTick || _disposed) return;
            _paintTick = false;
            try { Arm((_deadline - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency); }
            catch (Win32Exception) { _fallback(); }
        }
        private void Arm(double seconds)
        {
            lock (_gate)
            {
                if (_disposed) return;
                long due = -(long)(Math.Max(0.001, Math.Min(1, seconds)) * 10000000);
                if (!SetWaitableTimer(_timer.SafeWaitHandle, ref due, 0, IntPtr.Zero, IntPtr.Zero, false)) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        private void Destroyed(object sender, EventArgs e) { Dispose(); _fallback(); }
        public void Dispose()
        {
            _panel.FrameStarting -= Starting; _panel.FrameCompleted -= Completed; _panel.HandleDestroyed -= Destroyed;
            lock (_gate) { if (_disposed) return; _disposed = true; _stop.Set(); }
        }
    }
}
