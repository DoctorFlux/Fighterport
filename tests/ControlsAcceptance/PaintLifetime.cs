using BrawlCrate.UI;
using BrawlLib.OpenGL;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
internal static partial class Program
{
    [DllImport("user32.dll")]static extern uint GetGuiResources(IntPtr process,uint flags);
    static async Task NativePaintLifetime(ModelForm owner,string output)
    {
        using(var process=Process.GetCurrentProcess())
        {
            GC.Collect();GC.WaitForPendingFinalizers();uint before=GetGuiResources(process.Handle,0);int contexts=TKContext.BoundContexts.Count;
            for(int iteration=0;iteration<8;iteration++)
            {
                Exception failure=null;bool done=false;TKContext a=null,b=null;
                owner.TransitionFitReady=async window=>{try{a=window.SourceView.Viewport.Context;b=window.TargetView.Viewport.Context;Check(a?.WindowInfo!=null&&b?.WindowInfo!=null,"Native review cycle "+iteration+" owns two live window/DC wrappers");if(iteration==0||iteration==7)await CaptureWindow(window,Path.Combine(output,"painting-cycle-"+iteration+".png"));}catch(Exception error){failure=error;}finally{window.Close();done=true;}};
                Call(owner,"ShowTransitionFit");await Wait(()=>done);if(failure!=null)throw failure;
                Check(a.WindowInfo==null&&b.WindowInfo==null,"Native review cycle "+iteration+" releases both Windows/DC wrappers on close");
                Check(TKContext.BoundContexts.Count==contexts,"Native review cycle "+iteration+" restores the exact live context count");
            }
            GC.Collect();GC.WaitForPendingFinalizers();await Task.Delay(100);uint after=GetGuiResources(process.Handle,0);
            File.WriteAllText(Path.Combine(output,"painting-resources.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new{Cycles=8,ContextsBefore=contexts,ContextsAfter=TKContext.BoundContexts.Count,GdiBefore=before,GdiAfter=after,AllowedGdiDrift=8}));
            Check(after<=before+8,"Repeated modal/native model painting returns GDI resources to the bounded warmed baseline");
        }
    }
}
