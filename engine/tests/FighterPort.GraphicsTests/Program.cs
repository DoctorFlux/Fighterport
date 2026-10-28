using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;
using FighterPort.Rendering;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        string output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);var checks=new List<string>();
        void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);checks.Add(message);Console.WriteLine("PASS "+message);}
        using var mutex=new Mutex(false,"Local\\FighterPortBackgroundCapture");
        if(!mutex.WaitOne(0))throw new InvalidOperationException("Serialized renderer is busy.");
        try
        {
            var fixtures=new List<(string Name,FighterProject Project)>();
            foreach(int count in new[]{4,5,9,17})
            {
                var bones=Enumerable.Range(0,count).Select(i=>new Bone("joint-"+i,-1,Transform.Identity)).ToArray();
                var influences=Enumerable.Range(0,count).Select(i=>new SkinInfluence(i,i==count-1?.19999998807907105f:.8f/(count-1))).ToArray();
                Vertex Vertex(Vector3 p)=>new(p,Vector3.UnitZ,Vector2.Zero,new(0,1,2,3),new(influences[0].Weight,influences[1].Weight,influences[2].Weight,influences[3].Weight),Vector4.One){Influences=count>4?influences:[]};
                var scene=new Scene("complete weighted triangle",bones,[new("triangle",[Vertex(new(-1,0,0)),Vertex(new(1,0,0)),Vertex(new(0,2,0))],[0,1,2])]);scene.Validate();
                var tracks=bones.Select((b,i)=>new BoneTrack(b.Name,Enumerable.Range(0,9).Select(c=>c==5?new Curve([new Key(0,i*2),new Key(2,i*3)],Interpolation.Hermite):null).ToArray())).ToArray();
                var clip=new AnimationClip(Guid.NewGuid(),"weighted motion",3,new(60),false,tracks);
                fixtures.Add(("skin-"+count,ProceduralFixture.Create() with{Scene=scene,Clips=[clip,clip with{Id=Guid.NewGuid()}],ControlRigs=[],Contacts=[]}));
            }
            if(args.Length>2)
            {
                var adapter=new BrawlAdapter(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER")??throw new InvalidOperationException("Worker required."));
                var imported=adapter.RunAsync(new("import",args[1],args[2],null,null,[],[0,.137,4.733])).GetAwaiter().GetResult();
                var project=imported.Project!;
                Check(project.Scene.Meshes.SelectMany(m=>m.Vertices).Any(v=>v.InfluenceCount>4),"Authentic renderer input retains native influences beyond four");
                fixtures.Add(("native-complete",project));
            }
            AppBuilder.Configure<Application>().UseWin32().UseSkia().SetupWithoutStarting();
            foreach(var fixture in fixtures)
            {
                var document=new EditorDocument(fixture.Project);using var renderer=new OffscreenViewport(document,480,480);
                var view=renderer.Viewport;view.FloorVisible=false;view.Camera.Target=new(0,1,0);view.Camera.Distance=5;
                if(fixture.Name.StartsWith("native")){view.Camera.Target=new(0,6,0);view.Camera.Distance=28;}
                foreach(double frame in new[]{0,.137,1.733})
                {
                    document.Seek(frame);view.RequestValidationProbe=true;var pixels=renderer.Capture();
                    Check(view.GpuProbeSamples>0&&view.GpuProbeMaxError<=.003&&view.GpuNormalProbeMaxError<=.003,fixture.Name+": complete skin positions/normals reach displayed GPU attributes at "+frame);
                    Check(!renderer.HostWasVisible&&!renderer.HostWasForeground,fixture.Name+": offscreen rendering creates no visible/foreground window");
                    if(frame==.137)
                    {
                        using var bitmap=new WriteableBitmap(new(480,480),new(96,96),PixelFormat.Rgba8888,AlphaFormat.Unpremul);
                        using(var locked=bitmap.Lock())for(int row=0;row<480;row++)Marshal.Copy(pixels,row*480*4,locked.Address+row*locked.RowBytes,480*4);
                        bitmap.Save(Path.Combine(output,fixture.Name+".png"));
                    }
                }
                if(document.Project.Clips.Length>1){view.Compare=true;view.RequestValidationProbe=true;renderer.Capture();view.Compare=false;view.RequestValidationProbe=true;renderer.Capture();Check(view.GpuProbeMaxError<=.003,fixture.Name+": comparison restores the complete-skin stream on the next target frame");}
            }
            File.WriteAllText(Path.Combine(output,"receipt.json"),JsonSerializer.Serialize(new{Passed=true,Checks=checks,OSInputInjected=false,ScreenCopied=false,VisibleWindow=false}));return 0;
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"failure.txt"),error.ToString());Console.Error.WriteLine(error);return 1;}
        finally{mutex.ReleaseMutex();}
    }
}
