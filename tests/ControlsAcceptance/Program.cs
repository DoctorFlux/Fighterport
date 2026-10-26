using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.Internal;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    static readonly List<string> Checks=new List<string>();
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,uint flags,uint access,IntPtr security);
    [DllImport("user32.dll",SetLastError=true)]static extern bool SetThreadDesktop(IntPtr desktop);
    [DllImport("user32.dll")]static extern bool CloseDesktop(IntPtr desktop);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct StartupInfo {public int Size;public string Reserved,Desktop,Title;public int X,Y,Width,Height,XChars,YChars,Fill,Flags;public short Show,Reserved2;public IntPtr ReservedBytes,Input,Output,Error;}
    [StructLayout(LayoutKind.Sequential)]struct ProcessInfo {public IntPtr Process,Thread;public uint Id,ThreadId;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcess(string app,System.Text.StringBuilder command,IntPtr processAttributes,IntPtr threadAttributes,bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo process);
    [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
    [DllImport("kernel32.dll")]static extern bool GetExitCodeProcess(IntPtr handle,out uint code);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")]static extern bool TerminateProcess(IntPtr handle,uint code);
    static bool PrivateDesktop;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);Checks.Add(name);Console.WriteLine("PASS "+name);}
    static object Call(object obj,string name,params object[] values)=>obj.GetType().GetMethod(name,Private).Invoke(obj,values);
    static async Task<T> Await<T>(object value)=>await (Task<T>)value;
    [STAThread] static int Main(string[] args)
    {
        try
        {
            if(args.Contains("--private-desktop"))
            {
                return LaunchBackground(args);
            }
            PrivateDesktop=args.Contains("--isolated-child");
            if(PrivateDesktop){Directory.CreateDirectory(args[2]);Console.SetOut(new StreamWriter(Path.Combine(args[2],"background.log")){AutoFlush=true});Console.SetError(Console.Out);AppDomain.CurrentDomain.UnhandledException+=(s,e)=>Console.WriteLine("UNHANDLED "+e.ExceptionObject);}
            Console.WriteLine("TRACE native static initialization");
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(BrawlCrate.Program).TypeHandle);
            Console.WriteLine("TRACE native static initialization finished");
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            string output=Path.GetFullPath(args[2]);Directory.CreateDirectory(output);
            Console.WriteLine("TRACE acceptance dispatch");
            int watchdogMinutes=args.Contains("--workspace-game-library")?15:3;
            var run=args.Contains("--workspace-game-library")?WorkspaceGameLibrary(args,output):args.Contains("--workspace-library-target")?WorkspaceLibraryTarget(args,output):args.Contains("--workspace-next-layout")?WorkspaceNextLayout(args,output):args.Contains("--workspace-performance")?WorkspacePerformance(args,output):args.Contains("--workspace-boundary-fit")?WorkspaceBoundaryFit(args,output):args.Contains("--workspace-exit-recovery")?WorkspaceExitRecovery(args,output):args.Contains("--workspace-loop-inspection")?WorkspaceLoopInspection(args,output):args.Contains("--workspace-set-export")?LibraryBatch(args,output):args.Contains("--workspace-batch-review")?WorkspaceBatchReview(args,output):args.Contains("--workspace-library")?WorkspaceLibrary(args,output):args.Contains("--workspace-comparison")?WorkspaceComparison(args,output):args.Contains("--workspace-export")?WorkspaceExport(args,output):args.Contains("--workspace-recent")?WorkspaceRecent(args,output):args.Contains("--workspace-dialogs")?WorkspaceDialogScaling(args,output):args.Contains("--workspace-walkthrough")?WorkspaceWalkthrough(args,output):args.Contains("--workspace-keyboard")?WorkspaceKeyboard(args,output):args.Contains("--workspace-scaling")?WorkspaceScaling(args,output):args.Contains("--project-lifecycle")?ProjectLifecycle(args,output):args.Contains("--workspace-flow")?WorkspaceFlow(args,output):args.Contains("--simple-workspace")?SimpleWorkspace(args,output):args.Contains("--contact-authoring")?ContactAuthoringWorkflow(args[0],args[1],output):args.Contains("--contact-transitions-project")?ContactTransitionProject(args[0],args[Array.IndexOf(args,"--contact-transitions-project")+1],output):args.Contains("--transition-fit")?TransitionFitting(args[0],args[1],output):args.Contains("--scene-fitting")?SceneFitting(args,output):args.Contains("--library-batch")?LibraryBatch(args,output):args.Contains("--saved-project")?ProjectCompatibility(args,output):args.Contains("--character-workspace")?CharacterWorkspace(args[0],args[1],output):args.Contains("--control-review")?ControlReview(args[0],args[1],output):args.Contains("--control-gizmos")?ControlGizmos(args[0],args[1],output):args.Contains("--model-pose")?ModelPoseAnimation(args[0],output):args.Contains("--retarget-source")?RetargetNext(args,output):args.Contains("--capture-model")?CaptureModel(args[0],args[1],output):Run(args[0],args[1],output);var timeout=DateTime.UtcNow.AddMinutes(watchdogMinutes);
            while(!run.IsCompleted){string control=Environment.GetEnvironmentVariable("FIGHTERPORT_CAPTURE_CONTROL");if(control!=null&&File.Exists(Path.Combine(control,"pause")))timeout=DateTime.UtcNow.AddMinutes(watchdogMinutes);if(DateTime.UtcNow>timeout)throw new TimeoutException("Controls acceptance timed out.");Application.DoEvents();Thread.Sleep(20);}
            run.GetAwaiter().GetResult();File.WriteAllText(Path.Combine(output,"receipt.json"),new JavaScriptSerializer().Serialize(new{Passed=true,Checks,ActiveDesktopWindows=false,PrivateDesktop,OSInputInjected=false,ScreenCopied=false}));Console.WriteLine("PASS "+Checks.Count+" native controls integration checks");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);Console.WriteLine(e);return 1;}
    }
    static int LaunchBackground(string[] args)
    {
        string name="FighterPortBackground"+Guid.NewGuid().ToString("N");
        var desktop=CreateDesktop(name,IntPtr.Zero,IntPtr.Zero,0,0x01ff,IntPtr.Zero);
        if(desktop==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        var info=new StartupInfo{Size=Marshal.SizeOf(typeof(StartupInfo)),Desktop="WinSta0\\"+name};ProcessInfo child;
        string exe=Assembly.GetExecutingAssembly().Location;
        var command=new System.Text.StringBuilder("\""+exe+"\" "+string.Join(" ",args.Where(a=>a!="--private-desktop").Select(a=>"\""+a+"\""))+" --isolated-child");
        try
        {
            // The child starts on its private desktop before CLR/OLE creates
            // any HWND. No SwitchDesktop or SendInput call exists here.
            if(!CreateProcess(exe,command,IntPtr.Zero,IntPtr.Zero,false,0x08004000,IntPtr.Zero,Environment.CurrentDirectory,ref info,out child))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                int watchdogMinutes=args.Contains("--workspace-game-library")?15:3;
                var deadline=DateTime.UtcNow.AddMinutes(watchdogMinutes);
                while(WaitForSingleObject(child.Process,1000)!=0)
                {
                    string control=Environment.GetEnvironmentVariable("FIGHTERPORT_CAPTURE_CONTROL");
                    if(control!=null&&File.Exists(Path.Combine(control,"cancel"))){TerminateProcess(child.Process,125);throw new OperationCanceledException("Background capture cancelled.");}
                    if(control!=null&&File.Exists(Path.Combine(control,"pause")))deadline=DateTime.UtcNow.AddMinutes(watchdogMinutes);
                    if(DateTime.UtcNow>deadline){TerminateProcess(child.Process,124);throw new TimeoutException("Background capture timed out.");}
                }
                GetExitCodeProcess(child.Process,out var code);string log=Path.Combine(args[2],"background.log");if(File.Exists(log))Console.Write(File.ReadAllText(log));return (int)code;
            }
            finally{CloseHandle(child.Thread);CloseHandle(child.Process);}
        }
        finally{CloseDesktop(desktop);}
    }
    static async Task CaptureModel(string modelPath,string motionPath,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Model capture requires the isolated desktop launcher.");
        var project=FighterProject.Create("Local native model verification");var form=new ModelForm();form.InitializeFighterProject(project);form.Show();
        Call(form,"ImportFighterModel",modelPath);Call(form,"ImportFighterAnimations",new[]{new[]{motionPath}});
        var editor=form.FighterEditor;editor.ConfigureFighterView();editor.SelectedCHR0=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First();editor.SetFrame(1);editor.FrameFighterModel();
        await Task.Delay(150);await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        var setup=await Await<ControlReply>(Call(form,"LoadControls"));Check(setup.Pose.Length==((MDL0Node)editor.TargetModel)._linker.BoneCache.Length,"Supplied native model retains all original bones across the process boundary");
        editor.SelectedBone=((MDL0Node)editor.TargetModel)._linker.BoneCache.Single(b=>b.Name==setup.BoneNames[setup.Roles.Single(r=>r.Role=="Hips").Bone.Value]);await(Task)Call(form,"AttachControlPin",false);
        var loaded=await Await<ControlReply>(Call(form,"LoadControls"));var original=loaded.Pose.Select(a=>a.ToArray()).ToArray();
        var pin=editor.AnimationPins.Single();Call(form,"BeginControlGesture",pin);await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));
        var scale=original[pin.Bone];Call(form,"MoveControlGesture",new[]{pin.Position[0]+.25f,pin.Position[1],pin.Position[2],pin.Rotation[0],pin.Rotation[1],pin.Rotation[2],pin.Rotation[3],scale[0],scale[1],scale[2]});Call(form,"EndControlGesture");await Wait(()=>Field(form,"_pinGesture")==null);
        var current=await Await<ControlReply>(Call(form,"LoadControls"));Check(Math.Abs(current.Pose[pin.Bone][6]-original[pin.Bone][6]-.25f)<.0001f,"Supplied native model pin edit persists to the existing animation");
        string nativePath=Path.Combine(output,"supplied-pin-edit.chr0");editor.SelectedCHR0.Export(nativePath);
        using(var reopened=NodeFactory.FromFile(null,nativePath) as CHR0Node)Check(Math.Abs(reopened.Children.OfType<CHR0EntryNode>().Single(t=>t.Name==pin.BoneName).GetFrameValue(6,0)-current.Pose[pin.Bone][6])<.0001f,"Supplied native edited CHR0 freshly reopens with its authored pin channel");
        foreach(int frame in new[]{1,Math.Min(8,editor.SelectedCHR0.FrameCount)})
        {
            editor.SetFrame(frame);await CaptureCheckpoint();editor.ModelPanel.Capture();editor.ModelPanel.Invalidate();editor.ModelPanel.Update();
            using(var modelImage=editor.ModelPanel.GetScreenshot(editor.ModelPanel.ClientRectangle,false))
            using(var bitmap=new Bitmap(form.Width,form.Height))
            {
                modelImage.Save(Path.Combine(output,"supplied-model-frame-"+frame+".png"));form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));var point=form.PointToClient(editor.ModelPanel.PointToScreen(Point.Empty));int border=(form.Width-form.ClientSize.Width)/2;using(var graphics=Graphics.FromImage(bitmap))graphics.DrawImageUnscaled(modelImage,point.X+border,point.Y+form.Height-form.ClientSize.Height-border);bitmap.Save(Path.Combine(output,"supplied-viewer-frame-"+frame+".png"));
            }
        }
        ((AnimationControlClient)Field(form,"_controlEngine")).Dispose();project.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(editor);form.Dispose();
    }
    static async Task Run(string modelPath,string motionPath,string output)
    {
        using(var client=new AnimationControlClient())
        {
            var hello=await client.Send(new{Operation="hello"});Check(hello.Ok,"Native frontend starts the x64 engine without a visible window");
            var loaded=await client.Send(new{Operation="load",Model=modelPath,Motion=motionPath});Check(loaded.Pose.Length>1,"Native worker imports original fixture hierarchy");
            var pin=await client.Send(new{Operation="pin",Bone=0,Frame=0});Check(pin.Pins.Length==1&&!pin.Pins[0].Soft,"Arbitrary existing bone receives an independently identified FK pin");
            var p=pin.Pins[0];var before=loaded.Pose[0];
            await client.Send(new{Operation="begin",Bone=0,Pin=p.Id,Frame=0});
            var goal=new[]{p.Position[0]+2,p.Position[1]+3,p.Position[2]+1,p.Rotation[0],p.Rotation[1],p.Rotation[2],p.Rotation[3],before[0],before[1],before[2]};
            var preview=await client.Send(new{Operation="preview",Values=goal});Check(Math.Abs(preview.Pose[0][6]-before[6]-2)<.0001f&&Math.Abs(preview.Pose[0][7]-before[7]-3)<.0001f,"World pin drag changes native existing-bone translation by independent expected values");
            var commit=await client.Send(new{Operation="commit"});Check(commit.Committed&&commit.Tracks.Length==1,"A pin gesture commits one existing-bone track");
            Check(commit.Tracks[0].Channels.Skip(6).Take(3).All(c=>c.Length>0),"Native group materialization supplies all authored translation axes");
            var restored=await client.Send(new{Operation="load",Model=modelPath,Motion=motionPath,State=commit.State});Check(restored.Pins.Single().Id==p.Id,"Pin metadata reload preserves independent pin identity");
            await client.Send(new{Operation="begin",Bone=0,Pin=p.Id,Frame=0});
            float angle=(float)Math.PI/6;var rotated=new[]{p.Position[0],p.Position[1],p.Position[2],0f,(float)Math.Sin(angle/2),0f,(float)Math.Cos(angle/2),before[0],before[1],before[2]};
            await client.Send(new{Operation="preview",Values=rotated});var rotation=await client.Send(new{Operation="commit"});
            Check(Math.Abs(rotation.Pose[0][4]-30)<.0002f,"Pin world rotation bakes the independent 30 degree native Y rotation");
            await client.Send(new{Operation="begin",Bone=0,Pin=p.Id,Frame=0});await client.Send(new{Operation="preview",Values=goal});var cancelled=await client.Send(new{Operation="cancel"});
            Check(Math.Abs(cancelled.Pose[0][4]-30)<.0002f,"Cancelling a later gesture restores the previously committed rotation");
            await client.Send(new{Operation="load",Model=modelPath,Motion=motionPath});
            var soft=await client.Send(new{Operation="pin-limbs",Frame=0,Follow=true});
            Check(soft.Pins.Length==4&&soft.Pins.All(pin=>pin.Soft)&&soft.State.Contains("EditorMotion"),"Automatic structural limb controls create four soft pins with frozen animation-follow trajectories");
            await client.Send(new{Operation="pin",Bone=0,Frame=0});var body=await client.Send(new{Operation="begin",Bone=0,Frame=0});
            var bodyChannels=body.Pose[0].ToArray();bodyChannels[7]+=100;
            var yielded=await client.Send(new{Operation="preview",Values=bodyChannels});
            Check(Math.Abs(yielded.Pose[0][7]-bodyChannels[7])<.0001f&&yielded.Pins.Where(pin=>pin.Soft).All(pin=>pin.Position[1]>50),"Large body movement remains authored while soft pins yield at native chain reach limits");
            await client.Send(new{Operation="cancel"});
            var restLoaded=await client.Send(new{Operation="load",Model=modelPath,Motion=motionPath});
            var restPin=await client.Send(new{Operation="pin",Bone=1,Frame=0});var joint=restPin.Pins.Single();
            await client.Send(new{Operation="begin",Bone=1,Pin=joint.Id,Frame=0});var poseChannels=restLoaded.Pose[1];
            await client.Send(new{Operation="preview",Values=new[]{joint.Position[0]+.5f,joint.Position[1],joint.Position[2],joint.Rotation[0],joint.Rotation[1],joint.Rotation[2],joint.Rotation[3],poseChannels[0],poseChannels[1],poseChannels[2]}});
            var partial=await client.Send(new{Operation="commit"});
            Check(partial.Tracks.Single().Channels[7].Single().Value==poseChannels[7]&&poseChannels[7]!=0,"Editing only X materializes the untouched native Y axis from its nonzero authentic rest value");
            using(var nativePartial=new CHR0Node{Name="OriginalRestAxis",FrameCount=24})
            {
                var writer=typeof(ModelForm).GetNestedType("NativeControlSnapshot",BindingFlags.NonPublic).GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic);writer.Invoke(null,new object[]{nativePartial,partial.Tracks});
                string path=Path.Combine(output,"partial-rest-axis.chr0");nativePartial.Export(path);
                using(var reopened=NodeFactory.FromFile(null,path) as CHR0Node)Check(reopened.Children.OfType<CHR0EntryNode>().Single().GetFrameValue(7,0)==poseChannels[7],"Fresh native decode preserves the nonzero rest axis when another pin axis is edited");
            }
            var needsAxes=await client.Send(new{Operation="retarget",SourceModel=modelPath,Model=modelPath,Motion=motionPath,Fit=false});
            Check(needsAxes.NeedsRigReview&&needsAxes.ReviewSource&&needsAxes.ReviewTarget,"Unreviewed native fixture axes request mapping/coordinate review instead of being inferred from names");
            object binding;using(var fixture=NodeFactory.FromFile(null,modelPath) as MDL0Node)
            {
                var roles=new Dictionary<string,string>{{"RootMotion","Root"},{"Hips","Pelvis"},{"Spine","Spine"},{"Head","Head"}};
                foreach(string side in new[]{"L","R"})foreach(var pair in new[]{new[]{"UpperArm","Arm"},new[]{"Elbow","Forearm"},new[]{"Wrist","Hand"},new[]{"UpperLeg","Thigh"},new[]{"Knee","Shin"},new[]{"Ankle","Foot"}})roles[pair[0]+"."+side]=pair[1]+"."+side;
                binding=new{Fingerprint=EngineRigReview.FingerprintOf(fixture),Roles=roles,Right=new[]{1f,0,0},Up=new[]{0f,1,0},Forward=new[]{0f,0,1},Evidence="Original procedural fixture declaration: authored axes and hierarchy; independently reviewed binding."};
                var expected=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(loaded.State)["Fingerprint"].ToString();
                Check(EngineRigReview.FingerprintOf(fixture)==expected,"Native and engine exact rig-review fingerprints agree across the transport boundary ("+EngineRigReview.FingerprintOf(fixture)+" / "+expected+")");
            }
            var retarget=await client.Send(new{Operation="retarget",SourceModel=modelPath,Model=modelPath,Motion=motionPath,Fit=false,SourceBinding=binding,TargetBinding=binding});
            Check(retarget.Algorithm=="native-body-surface-96.2"&&retarget.Draft&&retarget.Tracks.Length>0&&retarget.Receipt.Contains("RETARGET_POSE_REWRITE_REVIEW"),"Imported engine protocol remains available with unresolved draft acceptance independently of the native Next retarget action");
        }
        var project=FighterProject.Create("Original controls fixture");
        // Never Show/ShowDialog: exercise actual controls and routed methods on
        // the owner STA, with a message pump and no desktop input injection.
        var form=new ModelForm();form.InitializeFighterProject(project);form.ClientSize=new Size(1320,850);
        if(PrivateDesktop){Console.WriteLine("Rendering native form on isolated desktop");form.Show();await Task.Delay(500);}
        Call(form,"ImportFighterModel",modelPath);Call(form,"ImportFighterAnimations",new[]{new[]{motionPath}});
        var editor=form.FighterEditor;editor.ConfigureFighterView();editor.SelectedCHR0=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First();editor.SetFrame(1);
        if(PrivateDesktop){await Task.Delay(100);await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));}
        var setup=await Await<ControlReply>(Call(form,"LoadControls"));Check(setup.Ok&&editor.RenderBones,"Standard native viewer opens with bones visible and engine setup attached");
        Check(editor.leftPanel.listAnims.Items.Count>0,"Dark theme preserves native search-placeholder semantics and the animation list");
        var model=(MDL0Node)editor.TargetModel;editor.SelectedBone=model._linker.BoneCache[0];
        await (Task)Call(form,"AttachControlPin",false);Check(editor.AnimationPins.Length==1,"Native Animation Controls menu attaches a pin to selected bone");
        var marker=editor.AnimationPins[0];var start=marker.Position;var initialKeys=editor.SelectedCHR0.Children.OfType<CHR0EntryNode>().Select(n=>n.Name+":"+string.Join(",",n.KeyArrays.Select(a=>a.KeyCount))).ToArray();
        Call(form,"BeginControlGesture",marker);
        await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));
        Call(form,"MoveControlGesture",new[]{start[0]+1,start[1]+2,start[2]+3,marker.Rotation[0],marker.Rotation[1],marker.Rotation[2],marker.Rotation[3],setup.Pose[0][0],setup.Pose[0][1],setup.Pose[0][2]});
        Call(form,"EndControlGesture");await Wait(()=>Field(form,"_pinGesture")==null);
        var root=model._linker.BoneCache[0];Check(Math.Abs(root.FrameState._translate._x-setup.Pose[0][6]-1)<.0001f,"Native preview retains committed pin movement after UpdateModel");
        var native=editor.SelectedCHR0;string nativePath=Path.Combine(output,"pin-edited.chr0");native.Export(nativePath);
        using(var reopened=NodeFactory.FromFile(null,nativePath) as CHR0Node){var entry=reopened.Children.OfType<CHR0EntryNode>().Single(n=>n.Name==root.Name);Check(Math.Abs(entry.GetFrameValue(6,0)-setup.Pose[0][6]-1)<.0001f&&Math.Abs(entry.GetFrameValue(7,0)-setup.Pose[0][7]-2)<.0001f,"Fresh native CHR0 decode preserves moved X and Y pin channels");}
        editor.Undo();
        var undoneKeys=native.Children.OfType<CHR0EntryNode>().Select(n=>n.Name+":"+string.Join(",",n.KeyArrays.Select(a=>a.KeyCount))).ToArray();
        Check(initialKeys.SequenceEqual(undoneKeys),"Native Undo restores exact prior key presence/counts");
        await Wait(()=>!(bool)Field(form,"_controlBusy"));editor.Redo();await Wait(()=>!(bool)Field(form,"_controlBusy"));
        Check(Math.Abs(root.FrameState._translate._x-setup.Pose[0][6]-1)<.0001f,"Native Redo restores committed pin pose");
        if(PrivateDesktop)
        {
            editor.ControlType=BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase.TransformType.Translation;
            var routed=editor.AnimationPins.Single();var viewport=editor.ModelPanel.CurrentViewport;
            var screen=(Vector3)Call(editor,"PinScreen",routed,viewport);int x=(int)Math.Round(screen._x),y=(int)Math.Round(screen._y);
            editor.PickAnimationPinsOnly=true;
            Call(editor,"modelPanel1_MouseDown",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,1,x,y,0));
            Check(editor.SelectedAnimationPin==routed.Id&&editor.SelectedBone==null,"Actual native pointer handler picks the pin identity before any bone selection");
            await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));
            Call(editor,"modelPanel1_MouseMove",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,0,x+20,y-12,0));
            Call(editor,"modelPanel1_MouseUp",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,1,x+20,y-12,0));
            await Wait(()=>Field(form,"_pinGesture")==null);
            Check(Math.Abs(root.FrameState._translate._x-setup.Pose[0][6]-1)>.0001f,"Actual native move/release handlers commit screen-plane pin dragging to keys");
            var moved=root.FrameState;await RotateNativeRing(form);
            Check(Math.Abs(root.FrameState._rotate._z-moved._rotate._z-30)<1,"Actual native rotation ring/release handlers retain the projected 30 degree pin rotation in animation");            string routedPath=Path.Combine(output,"routed-pin.chr0");native.Export(routedPath);
            using(var reopened=NodeFactory.FromFile(null,routedPath) as CHR0Node)Check(Math.Abs(reopened.Children.OfType<CHR0EntryNode>().Single(n=>n.Name==root.Name).GetFrameValue(5,0)-root.FrameState._rotate._z)<.00001f,"Fresh CHR0 decode preserves rotation authored through actual native pointer handlers");
            editor.ControlType=BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase.TransformType.Translation;var pinsBefore=editor.AnimationPins.Select(pin=>pin.Id).ToArray();
            routed=editor.AnimationPins.Single();screen=(Vector3)Call(editor,"PinScreen",routed,viewport);x=(int)Math.Round(screen._x);y=(int)Math.Round(screen._y);
            Call(editor,"modelPanel1_MouseDown",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,1,x,y,0));
            await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));
            var beforeCancel=root.FrameState;
            Call(editor,"modelPanel1_MouseMove",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,0,x+15,y,0));
            editor.SetFrame(2);Call(editor,"modelPanel1_MouseUp",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,1,x+15,y,0));
            await Wait(()=>Field(form,"_pinGesture")==null);editor.SetFrame(1);
            Check(Math.Abs(root.FrameState._rotate._z-beforeCancel._rotate._z)<.0001f,"Changing the frame during a native pin gesture rejects stale publication and commit");
            float angleBeforeUndo=root.FrameState._rotate._z;((ToolStripButton)Field(form,"_fighterUndo")).PerformClick();await Wait(()=>!(bool)Field(form,"_controlBusy"));
            Check(Math.Abs(root.FrameState._rotate._z-angleBeforeUndo)>.001f,"The top Fighter Undo button undoes pin keys before import history");
            editor.Redo();await Wait(()=>!(bool)Field(form,"_controlBusy"));Check(Math.Abs(root.FrameState._rotate._z-angleBeforeUndo)<.001f,"Native Redo restores the top-toolbar pin Undo");
        }
        string projectPath=Path.Combine(output,"controls.fpstudio");
        var roots=(Dictionary<string,ResourceNode>)Field(form,"_fighterRoots");
        foreach(var asset in project.RegisteredAssets())if(roots.TryGetValue(asset.Id,out var owned))owned.Export(asset.WorkingPath);
        form.SaveFighterProject(projectPath);using(var reopened=FighterProject.Open(projectPath))Check(reopened.AnimationControls.Any(s=>s.Json.Contains(marker.Id)),"Next project Save Fighter/reopen retains editor pin state");
        if(PrivateDesktop)
        {
            await CaptureCheckpoint();
            editor.ModelPanel.Capture();editor.ModelPanel.Invalidate();editor.ModelPanel.Update();
            using(var modelImage=editor.ModelPanel.GetScreenshot(editor.ModelPanel.ClientRectangle,false))
            using(var bitmap=new Bitmap(form.Width,form.Height))
            {
                modelImage.Save(Path.Combine(output,"native-model-pins.png"));
                form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
                var clientPoint=form.PointToClient(editor.ModelPanel.PointToScreen(Point.Empty));
                var offset=new Point((form.Width-form.ClientSize.Width)/2,form.Height-form.ClientSize.Height-(form.Width-form.ClientSize.Width)/2);
                using(var graphics=Graphics.FromImage(bitmap))graphics.DrawImageUnscaled(modelImage,clientPoint.X+offset.X,clientPoint.Y+offset.Y);
                bitmap.Save(Path.Combine(output,"native-dark-controls.png"));
            }
        }
        Check(PrivateDesktop||!form.Visible,"Native checks use an invisible form or isolated desktop without switching the user's desktop");
        editor.SelectedBone=root;var nativeBefore=root.FrameState;editor.BoneChange(root);var nativeChanged=nativeBefore;nativeChanged.Translate=nativeChanged._translate+new Vector3(.5f,0,0);editor.CHR0Editor.ApplyState(nativeChanged);editor.BoneChange(root);editor.Undo();
        Check(editor.TargetModel==model&&Math.Abs(root.FrameState._translate._x-nativeBefore._translate._x)<.0001f,"Ordinary native bone Undo retains its target model and restores its pose in the shared history");
        editor.Redo();Check(Math.Abs(root.FrameState._translate._x-nativeChanged._translate._x)<.0001f,"Ordinary native bone Redo remains compatible with pin command history");editor.Undo();
        int counter=0;editor.RecordEditorCommand(()=>counter=0,()=>counter=1);counter=1;editor.RecordEditorCommand(()=>counter=1,()=>counter=2);counter=2;editor.Undo();editor.Undo();Check(counter==0,"Two consecutive native Undos restore both command boundaries");editor.Redo();Check(counter==1,"Native Redo advances one command after multiple Undos");editor.RecordEditorCommand(()=>counter=1,()=>counter=3);counter=3;Check(!editor.CanRedo,"A new edit after Undo discards the obsolete redo branch");editor.Undo();editor.Redo();Check(counter==3,"Undo/Redo remains coherent after replacing an undone branch");
        ((AnimationControlClient)Field(form,"_controlEngine")).Dispose();project.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(editor);form.Dispose();
        if(PrivateDesktop)
        {
            using(var startup=MainForm.StartViewerApplication())
            {
                await Task.Delay(150);var viewer=Application.OpenForms.OfType<ModelForm>().Single();
                Check(viewer.Visible&&!MainForm.Instance.Visible&&viewer.FighterEditor.TargetModel==null,"The actual no-argument startup factory opens the empty model viewer while the resource window stays hidden");
                Check(viewer.FighterEditor.RenderBones&&viewer.Controls.OfType<ToolStrip>().Any(s=>s.Name=="fighterToolbar"),"Direct startup retains standard native bones and the extra Fighter toolbar");
                await CaptureCheckpoint();using(var bitmap=new Bitmap(viewer.Width,viewer.Height)){viewer.DrawToBitmap(bitmap,new Rectangle(Point.Empty,viewer.Size));bitmap.Save(Path.Combine(output,"native-startup.png"));}
                ((AnimationControlClient)Field(viewer,"_controlEngine")).Dispose();viewer.FighterProject.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(viewer.FighterEditor);viewer.Dispose();
            }
        }
    }
    static object Field(object obj,string name)=>obj.GetType().GetField(name,Private).GetValue(obj);
    static async Task CaptureCheckpoint()
    {
        string path=Environment.GetEnvironmentVariable("FIGHTERPORT_CAPTURE_CONTROL");
        if(path!=null){while(File.Exists(Path.Combine(path,"pause"))){if(File.Exists(Path.Combine(path,"cancel")))throw new OperationCanceledException("Background capture cancelled.");await Task.Delay(250);}if(File.Exists(Path.Combine(path,"cancel")))throw new OperationCanceledException("Background capture cancelled.");}
        await Task.Delay(500);
    }
    static async Task Wait(Func<bool> done){var timeout=DateTime.UtcNow.AddSeconds(30);while(!done()){if(DateTime.UtcNow>timeout)throw new TimeoutException("Native gesture did not finish.");await Task.Delay(30);}}
    static IEnumerable<ResourceNode> Walk(ResourceNode node){yield return node;foreach(var child in node.Children)foreach(var n in Walk(child))yield return n;}
}
