using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.Platform;
using FighterPort.Core;
using Silk.NET.OpenGL;
using Gl=Silk.NET.OpenGL.GL;
using Buffer=System.Buffer;
using PixelFormat=Silk.NET.OpenGL.PixelFormat;

namespace FighterPort.Rendering;

// Uses the application's existing platform graphics provider and renderer.
// No desktop lifetime, visible window, input or screen capture is required.
public sealed unsafe class OffscreenViewport : IDisposable
{
    readonly IGlContext context;
    readonly Gl gl;
    readonly int thread=Environment.CurrentManagedThreadId;
    readonly uint framebuffer,color,depth;
    public SkinnedViewport Viewport {get;}
    public int Width {get;}
    public int Height {get;}
    public bool HostWasVisible {get;private set;}
    public bool HostWasForeground {get;private set;}
    bool disposed;

    public OffscreenViewport(EditorDocument document,int width,int height)
    {
        if(width is <64 or >4096||height is <64 or >4096)throw new ArgumentOutOfRangeException(nameof(width));
        Width=width;Height=height;
        var platform=AvaloniaLocator.Current.GetService<IPlatformGraphics>()??throw new NotSupportedException("No platform GPU provider is available for background capture.");
        if(AvaloniaLocator.Current.GetService<IPlatformGraphicsOpenGlContextFactory>() is {} factory)
            context=factory.CreateContext([new(GlProfileType.OpenGLES,3,0),new(GlProfileType.OpenGL,3,3)])??throw new NotSupportedException("The platform could not create an offscreen OpenGL context.");
        else if(platform.UsesSharedContext)
        {
            if(platform.GetSharedContext() is not IGlContext shared||!shared.CanCreateSharedContext)throw new NotSupportedException("The platform cannot create an owned offscreen OpenGL context.");
            context=shared.CreateSharedContext(null)??throw new NotSupportedException("The platform could not create a shared offscreen context.");
        }
        else context=platform.CreateContext() as IGlContext??throw new NotSupportedException("The platform is not an OpenGL graphics provider.");
        using var current=context.MakeCurrent();gl=Gl.GetApi(name=>context.GlInterface.GetProcAddress(name));
        Viewport=new(){Document=document,SkeletonVisible=false,StrictGraphicsDiagnostics=true};
        try
        {
            Viewport.Measure(new(width,height));Viewport.Arrange(new Rect(0,0,width,height));Viewport.InitializeGraphics(gl);CheckError("renderer initialization");
            framebuffer=gl.GenFramebuffer();color=gl.GenTexture();depth=gl.GenRenderbuffer();
            gl.BindFramebuffer(FramebufferTarget.Framebuffer,framebuffer);gl.BindTexture(TextureTarget.Texture2D,color);
            gl.TexImage2D(TextureTarget.Texture2D,0,InternalFormat.Rgba8,(uint)width,(uint)height,0,PixelFormat.Rgba,PixelType.UnsignedByte,null);
            gl.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter,(int)TextureMinFilter.Nearest);
            gl.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter,(int)TextureMagFilter.Nearest);
            gl.FramebufferTexture2D(FramebufferTarget.Framebuffer,FramebufferAttachment.ColorAttachment0,TextureTarget.Texture2D,color,0);
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer,depth);gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer,InternalFormat.Depth24Stencil8,(uint)width,(uint)height);
            gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer,FramebufferAttachment.DepthStencilAttachment,RenderbufferTarget.Renderbuffer,depth);
            if(gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer)!=GLEnum.FramebufferComplete)throw new InvalidOperationException("Offscreen GPU framebuffer is incomplete.");
            CheckError("framebuffer creation");ObserveWindows();
        }
        catch{Dispose();throw;}
    }
    // Explicit offline GPU readback; ordinary playback never calls this method.
    public byte[] Capture()
    {
        VerifyThread();using var current=context.MakeCurrent();ObserveWindows();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer,framebuffer);Viewport.RenderGraphics();
        if(Viewport.RenderError is {} error)throw new InvalidOperationException(error);CheckError("viewport rendering");
        var raw=new byte[checked(Width*Height*4)];gl.PixelStore(PixelStoreParameter.PackAlignment,1);
        fixed(byte* p=raw)gl.ReadPixels(0,0,(uint)Width,(uint)Height,PixelFormat.Rgba,PixelType.UnsignedByte,p);CheckError("pixel readback");
        var row=new byte[Width*4];for(int y=0;y<Height/2;y++)
        {
            int a=y*row.Length,b=(Height-1-y)*row.Length;Buffer.BlockCopy(raw,a,row,0,row.Length);Buffer.BlockCopy(raw,b,raw,a,row.Length);Buffer.BlockCopy(row,0,raw,b,row.Length);
        }
        ObserveWindows();return raw;
    }
    void ObserveWindows()
    {
        if(!OperatingSystem.IsWindows())return;
        uint own=(uint)Environment.ProcessId;GetWindowThreadProcessId(GetForegroundWindow(),out uint active);HostWasForeground|=active==own;
        EnumWindows((window,_)=>{GetWindowThreadProcessId(window,out uint pid);if(pid==own&&IsWindowVisible(window))HostWasVisible=true;return true;},IntPtr.Zero);
        if(HostWasVisible||HostWasForeground)throw new InvalidOperationException("Background capture unexpectedly created a visible or active window.");
    }
    void CheckError(string stage){var code=gl.GetError();if(code!=GLEnum.NoError)throw new InvalidOperationException($"Offscreen GPU {stage} failed: {code}.");}
    void VerifyThread(){ObjectDisposedException.ThrowIf(disposed,this);if(thread!=Environment.CurrentManagedThreadId)throw new InvalidOperationException("Offscreen context must stay on its owning thread.");}
    public void Dispose()
    {
        if(disposed)return;VerifyThread();disposed=true;
        try{using var current=context.MakeCurrent();gl.DeleteFramebuffer(framebuffer);gl.DeleteRenderbuffer(depth);gl.DeleteTexture(color);Viewport.DisposeGraphics();}
        finally{context.Dispose();}
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate bool WindowVisitor(IntPtr window,IntPtr state);
    [DllImport("user32.dll")]static extern bool EnumWindows(WindowVisitor visitor,IntPtr state);
    [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
}
