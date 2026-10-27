using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Text;

namespace BrawlCrate.Studio
{
    internal sealed class AnimationControlClient:IDisposable
    {
        private Process _process;
        private readonly SemaphoreSlim _queue=new SemaphoreSlim(1);
        private readonly JavaScriptSerializer _json=new JavaScriptSerializer{MaxJsonLength=64*1024*1024,RecursionLimit=200};
        private Task<string> _errors;
        private StreamWriter _input;
        internal async Task<ControlReply> Send(object request)
        {
            await _queue.WaitAsync().ConfigureAwait(false);
            try
            {
                if(_process==null)
                {
                    string path=Environment.GetEnvironmentVariable("FIGHTERPORT_ENGINE_HOST")??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"engine","FighterPort.EngineHost.exe");
                    if(!File.Exists(path))throw new FileNotFoundException("Build the animation engine with scripts/Build-Engine.ps1 first.",path);
                    var start=new ProcessStartInfo(path){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
                    start.EnvironmentVariables["FIGHTERPORT_BRAWL_WORKER"]=Path.Combine(Path.GetDirectoryName(path),"worker","FighterPort.BrawlWorker.exe");
                    _process=Process.Start(start);_input=new StreamWriter(_process.StandardInput.BaseStream,new UTF8Encoding(false));_errors=_process.StandardError.ReadToEndAsync();
                }
                await _input.WriteLineAsync(_json.Serialize(request)).ConfigureAwait(false);
                await _input.FlushAsync().ConfigureAwait(false);
                var read=_process.StandardOutput.ReadLineAsync();
                if(await Task.WhenAny(read,Task.Delay(TimeSpan.FromMinutes(2))).ConfigureAwait(false)!=read)throw new IOException("The animation engine did not respond. Cancel this edit and retry.");
                string line=await read.ConfigureAwait(false);
                if(line==null)throw new IOException("Animation engine stopped. "+(_errors.IsCompleted?_errors.Result:""));
                var reply=_json.Deserialize<ControlReply>(line);
                if(reply==null||!reply.Ok)throw new InvalidOperationException(reply?.Error??"Invalid animation engine response.");
                return reply;
            }
            catch(IOException){StopProcess();throw;}
            catch(ObjectDisposedException){StopProcess();throw;}
            finally{_queue.Release();}
        }
        private void StopProcess(){var process=_process;_process=null;if(process!=null){try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}process.Dispose();}_input=null;}
        public void Dispose()=>StopProcess();
    }
    public sealed class AnimationControlState
    {
        public string Key {get;set;} public string Json {get;set;}
    }
    public sealed class WorkspaceClipLink {public string Key {get;set;}public string Id {get;set;}}
    internal sealed class ControlReply
    {
        public SceneFittingReply Fitting {get;set;}
        public string OptimizationPath {get;set;}
        public bool Ok {get;set;} public string Error {get;set;} public string State {get;set;}
        public bool Committed {get;set;} public float[][] Pose {get;set;}
        public string[] BoneNames {get;set;}
        public int[] ProtectedMotionBones {get;set;}
        public int? BodyBone {get;set;}
        public ControlPin[] Pins {get;set;} public ControlChain[] Chains {get;set;} public ControlTrack[] Tracks {get;set;} public string[] Diagnostics {get;set;}
        public ControlRole[] Roles {get;set;}
        public string Algorithm {get;set;} public string Name {get;set;} public int SampleCount {get;set;} public bool Loop {get;set;} public bool Draft {get;set;}public string Receipt {get;set;}
        public RetargetBoneBinding[] Mapping {get;set;}
        public bool NeedsRigReview {get;set;}public bool ReviewSource {get;set;}public bool ReviewTarget {get;set;}
        public NativeWorkspaceInfo Workspace {get;set;}public string Package {get;set;}public string Model {get;set;}public string ModelKey {get;set;}public string[] MotionPaths {get;set;}
        public NativeWorkspaceControls[] WorkspaceControls {get;set;}public string PoseState {get;set;}
        public string SelectedMotion {get;set;}public string SelectedEntry {get;set;}public int DisplayFrame {get;set;}
    }
    internal sealed class ControlPin
    {
        public string Id {get;set;} public int Bone {get;set;} public string BoneName {get;set;} public string Name {get;set;} public bool Soft {get;set;}
        public float[] Position {get;set;} public float[] Rotation {get;set;}
        public string RootName {get;set;} public string MiddleName {get;set;} public float[] Pole {get;set;} public float[] PoleParent {get;set;}
        public override string ToString()=>Id.StartsWith("fk:")?"FK: "+Name:Id.StartsWith("ik:")?"IK: "+Name:Id.StartsWith("pole:")?"Pole: "+Name:(Soft?"Soft pin: ":"FK pin: ")+Name;
    }
    internal sealed class ControlChain {public string Role {get;set;}public int Root {get;set;}public int Middle {get;set;}public int Effector {get;set;}public float[] Pole {get;set;}public float[] PoleParent {get;set;}public string Evidence {get;set;}public bool Reviewed {get;set;}}
    internal sealed class ControlRole {public string Role {get;set;}public int? Bone {get;set;}public bool Reviewed {get;set;}public string Evidence {get;set;}}
    internal sealed class NativeWorkspaceControls {public string Path {get;set;}public string Entry {get;set;}public string State {get;set;}public string Id {get;set;}}
    internal sealed class ControlKey {public int Frame {get;set;}public float Value {get;set;}public float Tangent {get;set;}}
    internal sealed class ControlTrack {public string Bone {get;set;}public ControlKey[][] Channels {get;set;}}
}
