using OpenTK.Graphics.OpenGL;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BrawlLib.OpenGL
{
    // Windows-only presentation adapter. The existing GL scene renders unchanged;
    // its completed back buffer is copied on the GPU into a DXGI flip surface.
    internal sealed class DxgiFramePresenter : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SwapDescription
        {
            internal uint Width, Height, RateNumerator, RateDenominator, Format, Scanline, Scaling;
            internal uint SampleCount, SampleQuality, Usage, BufferCount;
            internal IntPtr Window;
            internal int Windowed;
            internal uint SwapEffect, Flags;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct TextureDescription
        {
            internal uint Width, Height, Mips, ArraySize, Format, SampleCount, SampleQuality, Usage, BindFlags, CpuAccess, MiscFlags;
        }
        [DllImport("d3d11.dll")]
        private static extern int D3D11CreateDeviceAndSwapChain(IntPtr adapter, uint driver, IntPtr software,
            uint flags, IntPtr levels, uint levelCount, uint sdk, ref SwapDescription description,
            out IntPtr chain, out IntPtr device, out uint level, out IntPtr context);
        [DllImport("opengl32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr wglGetProcAddress(string name);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)] private delegate IntPtr OpenDevice(IntPtr device);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)] private delegate bool CloseDevice(IntPtr device);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)] private delegate IntPtr RegisterObject(IntPtr device, IntPtr resource, uint name, uint type, uint access);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)] private delegate bool UnregisterObject(IntPtr device, IntPtr resource);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)] private delegate bool LockObjects(IntPtr device, int count, IntPtr[] objects);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetBuffer(IntPtr self, uint index, ref Guid iid, out IntPtr resource);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateTexture(IntPtr self, ref TextureDescription description, IntPtr data, out IntPtr texture);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void CopyResource(IntPtr self, IntPtr destination, IntPtr source);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int PresentBuffer(IntPtr self, uint interval, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ResizeBuffers(IntPtr self, uint count, uint width, uint height, uint format, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int MaximumLatency(IntPtr self, uint frames);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void ContextCommand(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetParent(IntPtr self, ref Guid iid, out IntPtr parent);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int WindowAssociation(IntPtr self, IntPtr window, uint flags);

        private static T Method<T>(IntPtr instance, int index) where T : class =>
            (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), index * IntPtr.Size), typeof(T));
        private static T Extension<T>(string name) where T : class
        {
            IntPtr address = wglGetProcAddress(name);
            if (address == IntPtr.Zero || address.ToInt64() <= 3 || address == new IntPtr(-1)) throw new NotSupportedException(name + " is unavailable.");
            return (T)(object)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
        }
        private static void Check(int result) { Marshal.ThrowExceptionForHR(result); }
        private static void Release(ref IntPtr value) { if (value != IntPtr.Zero) { Marshal.Release(value); value = IntPtr.Zero; } }
        private IntPtr _chain, _device, _context, _backBuffer, _texture, _interop;
        private IntPtr[] _objects;
        private uint _framebuffer, _renderbuffer;
        private readonly LockObjects _lock, _unlock;
        private readonly UnregisterObject _unregister;
        private readonly CloseDevice _close;
        private readonly CopyResource _copy;
        private readonly PresentBuffer _present;
        internal int Width { get; private set; }
        internal int Height { get; private set; }
        internal bool IsOccluded { get; private set; }

        internal DxgiFramePresenter(IntPtr window, int width, int height)
        {
            Width = width; Height = height;
            if (GL.GetString(StringName.Extensions)?.Contains("GL_EXT_framebuffer_blit") != true)
                throw new NotSupportedException("GPU framebuffer copying is unavailable.");
            var open = Extension<OpenDevice>("wglDXOpenDeviceNV");
            var register = Extension<RegisterObject>("wglDXRegisterObjectNV");
            _close = Extension<CloseDevice>("wglDXCloseDeviceNV");
            _unregister = Extension<UnregisterObject>("wglDXUnregisterObjectNV");
            _lock = Extension<LockObjects>("wglDXLockObjectsNV");
            _unlock = Extension<LockObjects>("wglDXUnlockObjectsNV");
            try
            {
                var description = new SwapDescription
                {
                    Width = (uint)width, Height = (uint)height, Format = 87, RateDenominator = 1,
                    SampleCount = 1, Usage = 0x20, BufferCount = 2, Window = window, Windowed = 1, SwapEffect = 3
                };
                Check(D3D11CreateDeviceAndSwapChain(IntPtr.Zero, 1, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7,
                    ref description, out _chain, out _device, out uint level, out _context));
                var factoryIid = new Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369");
                Check(Method<GetParent>(_chain, 6)(_chain, ref factoryIid, out IntPtr factory));
                try { Check(Method<WindowAssociation>(factory, 8)(factory, GetAncestor(window, 2), 2)); } // Keep native Alt+Enter handling.
                finally { Marshal.Release(factory); }
                var latencyIid = new Guid("77db970f-6276-48ba-ba28-070143b4392c");
                if (Marshal.QueryInterface(_device, ref latencyIid, out IntPtr latencyDevice) == 0)
                {
                    try { Check(Method<MaximumLatency>(latencyDevice, 12)(latencyDevice, 1)); }
                    finally { Marshal.Release(latencyDevice); }
                }
                _interop = open(_device);
                if (_interop == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot share this graphics adapter with OpenGL.");
                CreateSurface(register);
                _copy = Method<CopyResource>(_context, 47);
                _present = Method<PresentBuffer>(_chain, 8);
            }
            catch { Dispose(); throw; }
        }

        private void CreateSurface(RegisterObject register)
        {
            var iid = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
            Check(Method<GetBuffer>(_chain, 9)(_chain, 0, ref iid, out _backBuffer));
            var texture = new TextureDescription { Width = (uint)Width, Height = (uint)Height, Mips = 1, ArraySize = 1, Format = 87, SampleCount = 1, BindFlags = 0x28 };
            Check(Method<CreateTexture>(_device, 5)(_device, ref texture, IntPtr.Zero, out _texture));
            GL.Ext.GenRenderbuffers(1, out _renderbuffer);
            IntPtr resource = register(_interop, _texture, _renderbuffer, 0x8D41, 2);
            if (resource == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot register the shared presentation surface.");
            _objects = new[] { resource };
            GL.Ext.GenFramebuffers(1, out _framebuffer);
        }

        internal void Resize(int width, int height)
        {
            ReleaseSurface();
            Check(Method<ResizeBuffers>(_chain, 13)(_chain, 2, (uint)width, (uint)height, 87, 0));
            Width = width; Height = height;
            CreateSurface(Extension<RegisterObject>("wglDXRegisterObjectNV"));
        }

        internal void Present()
        {
            if (!_lock(_interop, 1, _objects)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot lock presentation surface.");
            try
            {
                GL.Ext.BindFramebuffer((FramebufferTarget)0x8CA8, 0);
                GL.ReadBuffer(ReadBufferMode.Back);
                GL.Ext.BindFramebuffer((FramebufferTarget)0x8CA9, _framebuffer);
                GL.Ext.FramebufferRenderbuffer((FramebufferTarget)0x8CA9, FramebufferAttachment.ColorAttachment0Ext, RenderbufferTarget.RenderbufferExt, _renderbuffer);
                GL.DrawBuffer((DrawBufferMode)0x8CE0);
                if ((int)GL.Ext.CheckFramebufferStatus((FramebufferTarget)0x8CA9) != 0x8CD5) throw new InvalidOperationException("Incomplete shared framebuffer.");
                GL.Ext.BlitFramebuffer(0, 0, Width, Height, 0, Height, Width, 0, ClearBufferMask.ColorBufferBit, (ExtFramebufferBlit)0x2600);
                var error = GL.GetError();
                if (error != ErrorCode.NoError) throw new InvalidOperationException("GPU presentation copy failed: " + error);
            }
            finally
            {
                GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
                GL.DrawBuffer(DrawBufferMode.Back); GL.ReadBuffer(ReadBufferMode.Back);
                if (!_unlock(_interop, 1, _objects)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot unlock presentation surface.");
            }
            _copy(_context, _backBuffer, _texture);
            int result = _present(_chain, 1, 0);
            Check(result);
            IsOccluded = result == 0x087A0001; // DXGI_STATUS_OCCLUDED
        }

        private void ReleaseSurface()
        {
            if (_objects != null && _interop != IntPtr.Zero) { _unregister(_interop, _objects[0]); _objects = null; }
            if (_framebuffer != 0) { GL.Ext.DeleteFramebuffers(1, ref _framebuffer); _framebuffer = 0; }
            if (_renderbuffer != 0) { GL.Ext.DeleteRenderbuffers(1, ref _renderbuffer); _renderbuffer = 0; }
            Release(ref _backBuffer); Release(ref _texture);
        }

        public void Dispose()
        {
            ReleaseSurface();
            if (_interop != IntPtr.Zero) { _close(_interop); _interop = IntPtr.Zero; }
            if (_context != IntPtr.Zero)
            {
                Method<ContextCommand>(_context, 110)(_context); // ClearState
                Method<ContextCommand>(_context, 111)(_context); // Flush deferred COM resource releases
            }
            Release(ref _chain); Release(ref _context); Release(ref _device);
        }
    }
}
