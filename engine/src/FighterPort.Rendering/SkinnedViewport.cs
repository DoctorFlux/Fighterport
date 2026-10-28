using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using FighterPort.Core;
using Silk.NET.OpenGL;
using Gl = Silk.NET.OpenGL.GL;

namespace FighterPort.Rendering;

public enum GizmoMode { Translate, Rotate, Scale }
public sealed unsafe partial class SkinnedViewport : OpenGlControlBase
{
    public EditorDocument Document {get;set;}=null!;
    TransformEdit? transformEdit;
    int gestureBone;
    public OrbitCamera Camera {get;}=new();
    ViewerCameraInput? cameraInput;
    public ViewerCameraInput CameraInput=>cameraInput??=new(Camera);
    public SupportSurface? SupportContext {get;set;}
    public GizmoMode Mode {get;set;}=GizmoMode.Rotate;
    public bool Compare {get;set;}
    public bool SkeletonVisible {get;set;}=true;
    public bool PolygonsVisible {get;set;}=true;
    public bool VerticesVisible {get;set;}
    public bool Wireframe {get;set;}
    public bool ShadersVisible {get;set;}=true;
    public bool ModelBoundsVisible {get;set;}
    public bool ObjectBoundsVisible {get;set;}
    public Vector4 BackgroundColor {get;set;}=new(.047f,.067f,.09f,1);
    public bool FloorVisible {get;set;}=true;
    public bool SyncVisibility {get;set;}=true;
    public bool ReferencePose {get;set;}
    public bool SkeletalAnimationVisible {get;set;}=true;
    public bool PoseEditingEnabled {get;set;}=true;
    public HashSet<string> HiddenTextures {get;}=[];
    public HashSet<int> HiddenBones {get;}=[];
    public HashSet<(string Mesh,int Call)> HiddenDrawCalls {get;}=[];
    public string GpuInfo {get;private set;}="GPU initializing";
    public string? RenderError {get;private set;}
    internal bool StrictGraphicsDiagnostics {get;set;}
    public long PresentedFrames {get;private set;}
    public List<double> RenderIntervalsMs {get;}=[];
    public HashSet<string> HiddenMeshes {get;}=[];
    public HashSet<string> ShownMeshes {get;}=[];
    public List<string> MaterialDiagnostics {get;}=[];
    public event Action<string>? MaterialDiagnosticRaised;
    public int NativeProgramCount {get;private set;}
    public long NativeDrawCount {get;private set;}
    public bool RequestValidationProbe {get;set;}
    public float GpuProbeMaxError {get;private set;}
    public int GpuProbeSamples {get;private set;}
    public float GpuNormalProbeMaxError {get;private set;}
    public float GpuColorProbeMaxError {get;private set;}
    public List<string> PointerDiagnostics {get;}=[];
    public ControlRigSetup? ControlSetup {get;set;}
    public ControlChain? IkChain {get;private set;}
    public Vector3 IkGoal {get;private set;}
    public Vector3 IkPole {get;private set;}
    public bool EditPole {get;set;}
    public bool IsDragging=>transformEdit is not null;
    public string? InteractionDiagnostic {get;private set;}
    EvaluatedPose? ikInput;
    Vector3 initialGoal,initialPole;
    Dictionary<int,Transform>? ikKeys,initialOverrides;
    public event Action? PoseChanged;
    Gl? api; uint program,probeProgram,vao,vbo,ebo,wireEbo,texture,lineVao,lineVbo; int elementCount;
    readonly Dictionary<string,uint> textures=[];
    readonly List<(Mesh Mesh,int Offset,int Count)> ranges=[];
    Scene? uploadedScene;
    Vertex[] uploadedVertices=[];bool completeSkin;
    NativeMaterialPipeline? nativeMaterials;
    JointDisplay? jointDisplay;
    MorphVertexStream? morphStream;
    public long MorphUploads {get;private set;}
    public long MorphBytes {get;private set;}
    public int DisplayedMorphBindings {get;private set;}
    int hoveredBone=-1;
    public int DisplayedJoints {get;private set;}
    readonly List<(Mesh Mesh,int Offset,int Count,NativeDrawCall Call,int Index)> drawRanges=[];
    long lastTimestamp;
    Point previous,dragOrigin;
    int dragAxis=-1;
    Transform? initial;
    bool rightCamera,middleCamera;
    EvaluatedPose? currentPose;
    float comparisonOffset;
    float SceneScale=>Math.Max(Camera.Distance/4,.01f);
    public void Refresh()=>RequestNextFrameRendering();
    // Composition-backed OpenGL surfaces do not provide a filled Avalonia hit-test area.
    // The owned transparent input host forwards presses/wheel events; capture then routes drags here.
    public void ForwardPointerPressed(PointerPressedEventArgs e){if(!e.Handled)OnPointerPressed(e);}
    public void ForwardPointerWheel(PointerWheelEventArgs e){if(!e.Handled)OnPointerWheelChanged(e);}
    public void DisableIK(){IkChain=null;ikKeys=null;MorphGeometryMode=false;InteractionDiagnostic=null;Refresh();}
    public void EnableIK(ControlRigSetup setup,ControlChain chain,bool pole=false){ClearEditorPinSelection();PickPinsOnly=false;MorphGeometryMode=false;ControlSetup=setup;IkChain=chain;EditPole=pole;Mode=GizmoMode.Translate;Document.SelectedBone=chain.Effector;ResetControlTargets();Refresh();}
    public void ResetControlTargets()
    {
        if(IsDragging||IkChain is not {} chain)return;
        var pose=Document.Evaluate();IkGoal=pose.World[chain.Effector].Translation;
        IkPole=AnimationControls.DefaultPole(Document.Project.Scene,pose,chain);
    }
    public bool PreviewIK(Vector3 goal,Vector3 pole)
    {
        if(ControlSetup is null||IkChain is null)return false;
        var result=AnimationControls.Solve(Document.Project.Scene,ikInput??Document.Evaluate(),ControlSetup,IkChain,goal,pole);
        var pins=Document.CurrentContacts;
        if(pins.Length>0||result.Diagnostics.Any(d=>d.Code=="NONUNIFORM_SCALE"))
        {
            var pose=ikInput??Document.Evaluate();var goalPin=new ContactPin(Guid.NewGuid(),IkChain.Role,IkChain.Effector,Vector3.Zero,goal,PoseGeometry.Rotation(pose.World[IkChain.Effector]),false,"IK goal",pole);
            if(pins.Any(p=>p.EditorBehavior==EditorPinBehavior.SoftReach)&&result.Value is null){InteractionDiagnostic=string.Join(" · ",result.Diagnostics.Select(d=>d.Message));return false;}
            var constrained=pins.Any(p=>p.EditorBehavior==EditorPinBehavior.SoftReach)
                ?EditorControlPosing.Solve(Document.Project.Scene,pose,ControlSetup,result.Value!.Transforms,pins.Where(p=>p.Bone!=IkChain.Effector||p.EditorBehavior!=EditorPinBehavior.SoftReach).ToArray())
                :ContactSolver.Solve(Document.Project.Scene,pose,ControlSetup,new Dictionary<int,Transform>(),[..pins,goalPin]);
            IkGoal=goal;IkPole=pole;InteractionDiagnostic=string.Join(" · ",constrained.Diagnostics.Select(d=>d.Message));
            if(constrained.Value is null)return false;ikKeys=constrained.Value.Transforms;Document.PreviewTransforms(ikKeys);return true;
        }
        IkGoal=goal;IkPole=pole;InteractionDiagnostic=string.Join(" · ",result.Diagnostics.Select(x=>x.Message));
        if(result.Value is null)return false;
        ikKeys=result.Value.Transforms;Document.PreviewTransforms(ikKeys);return true;
    }
    public bool PreviewFK(int bone,Transform value)
    {
        var edits=new Dictionary<int,Transform>{{bone,value}};var pins=Document.CurrentContacts;
        if(pins.Length==0){ikKeys=edits;Document.SetPose(bone,value);InteractionDiagnostic=null;return true;}
        if(ControlSetup is null){InteractionDiagnostic="Set up the existing-bone controls before editing a pinned pose.";return false;}
        var solved=EditorControlPosing.Solve(Document.Project.Scene,ikInput??Document.Evaluate(),ControlSetup,edits,pins);
        InteractionDiagnostic=string.Join(" · ",solved.Diagnostics.Select(d=>d.Message));
        if(solved.Value is null)return false;
        ikKeys=solved.Value.Transforms;Document.PreviewTransforms(ikKeys);return true;
    }
    public bool CommitIKGoal(Vector3 goal,Vector3 pole)
    {
        using var edit=Document.BeginTransformEdit();ikKeys=null;ikInput=Document.Evaluate();
        try{if(!PreviewIK(goal,pole)||ikKeys is null)return false;return edit.Commit(ikKeys,EditorPinTargets(IkChain!.Effector));}
        finally{ikInput=null;ResetControlTargets();Refresh();}
    }
    public void BeginPoseGesture()
    {
        transformEdit=Document.BeginTransformEdit();gestureBone=Document.SelectedBone;initial=Document.Evaluate().Local[gestureBone];ikInput=Document.Evaluate();initialGoal=IkGoal;initialPole=IkPole;initialOverrides=Document.PoseOverrides.ToDictionary();ikKeys=null;
        gesturePin=SelectedPinId;pinGestureTarget=null;if(SelectedEditorPin is {} pin){initialPinPosition=pin.Position;initialPinRotation=pin.Rotation;}
    }
    public bool CommitPoseGesture()
    {
        bool committed=false;
        try{if(transformEdit is {} edit){if(ikKeys is not null&&gesturePin==SelectedPinId)committed=edit.Commit(ikKeys,pinGestureTarget is {} target&&gesturePin is {} id?new Dictionary<Guid,EditorPinTarget>{{id,target}}:EditorPinTargets(gestureBone));else edit.Cancel();}}
        finally{transformEdit=null;dragAxis=-1;initial=null;ikInput=null;initialOverrides=null;gesturePin=null;pinGestureTarget=null;ResetControlTargets();PoseChanged?.Invoke();Refresh();}
        return committed;
    }
    public void CancelPoseGesture()
    {
        transformEdit?.Cancel();transformEdit=null;dragAxis=-1;initial=null;ikInput=null;ikKeys=null;initialOverrides=null;gesturePin=null;pinGestureTarget=null;ResetControlTargets();PoseChanged?.Invoke();Refresh();
    }
    public IReadOnlyDictionary<Guid,EditorPinTarget> EditorPinTargets(int directlyEditedBone)
    {
        var pose=Document.Evaluate();return Document.CurrentContacts.Where(p=>p.EditorBehavior==EditorPinBehavior.SoftReach&&p.Bone==directlyEditedBone).ToDictionary(p=>p.Id,p=>new EditorPinTarget(pose.World[p.Bone].Translation,PoseGeometry.Rotation(pose.World[p.Bone])));
    }
    protected override void OnOpenGlInit(GlInterface gl)=>InitializeGraphics(Gl.GetApi(name=>gl.GetProcAddress(name)));
    // The offscreen acceptance host uses these same renderer entry points with its
    // own context/FBO. It never needs a visible Avalonia window or screen capture.
    internal void InitializeGraphics(Gl graphics)
    {
        api=graphics; var g=api;
        uploadedScene=null;
        GpuInfo=$"{g.GetStringS(StringName.Renderer)} · {g.GetStringS(StringName.Version)}";
        nativeMaterials=new(g,GpuInfo.Contains("OpenGL ES",StringComparison.Ordinal));
        jointDisplay=new(g,GpuInfo.Contains("OpenGL ES",StringComparison.Ordinal));
        morphStream=new(g);
        uint vs=Shader(ShaderType.VertexShader,VertexSource),fs=Shader(ShaderType.FragmentShader,FragmentSource);
        program=LinkProgram(vs,fs,false);probeProgram=LinkProgram(vs,fs,true);
        g.DeleteShader(vs);g.DeleteShader(fs);
        vao=g.GenVertexArray();vbo=g.GenBuffer();ebo=g.GenBuffer();wireEbo=g.GenBuffer();
        g.BindVertexArray(vao);g.BindBuffer(BufferTargetARB.ArrayBuffer,vbo);g.BindBuffer(BufferTargetARB.ElementArrayBuffer,ebo);
        int[] sizes=[3,3,2,4,4,4,4,2,2,2,2,2,2,2]; int offset=0;
        for(uint i=0;i<sizes.Length;i++) { g.EnableVertexAttribArray(i);g.VertexAttribPointer(i,sizes[i],VertexAttribPointerType.Float,false,152,(void*)(offset*4));offset+=sizes[i]; }
        lineVao=g.GenVertexArray();lineVbo=g.GenBuffer();g.BindVertexArray(lineVao);g.BindBuffer(BufferTargetARB.ArrayBuffer,lineVbo);offset=0;
        for(uint i=0;i<sizes.Length;i++){g.EnableVertexAttribArray(i);g.VertexAttribPointer(i,sizes[i],VertexAttribPointerType.Float,false,152,(void*)(offset*4));offset+=sizes[i];}
        texture=g.GenTexture();g.BindTexture(TextureTarget.Texture2D,texture);
        byte[] pixels=[240,245,255,255,180,195,210,255,180,195,210,255,240,245,255,255];
        fixed(byte* p=pixels) g.TexImage2D(TextureTarget.Texture2D,0,InternalFormat.Rgba,2,2,0,PixelFormat.Rgba,PixelType.UnsignedByte,p);
        g.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter,(int)TextureMinFilter.Nearest);
        g.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter,(int)TextureMagFilter.Nearest);
        g.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapS,(int)TextureWrapMode.Repeat);
        g.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapT,(int)TextureWrapMode.Repeat);
    }
    uint LinkProgram(uint vertex,uint fragment,bool feedback)
    {
        var g=api!;uint result=g.CreateProgram();g.AttachShader(result,vertex);g.AttachShader(result,fragment);
        if(feedback)g.TransformFeedbackVaryings(result,4,new[]{"skinPosition","probeNormal","probeColor0","probeColor1"},GLEnum.InterleavedAttribs);
        g.LinkProgram(result);g.GetProgram(result,ProgramPropertyARB.LinkStatus,out int ok);
        if(ok==0){string error=g.GetProgramInfoLog(result);g.DeleteProgram(result);throw new InvalidOperationException(error);}return result;
    }
    uint Shader(ShaderType type,string source)
    {
        if(GpuInfo.Contains("OpenGL ES",StringComparison.Ordinal))source=source.Replace("#version 330 core","#version 300 es\nprecision highp float;\nprecision highp int;");
        var g=api!;uint shader=g.CreateShader(type);g.ShaderSource(shader,source);g.CompileShader(shader);g.GetShader(shader,ShaderParameterName.CompileStatus,out int ok);
        if(ok==0) throw new InvalidOperationException(g.GetShaderInfoLog(shader));return shader;
    }
    protected override void OnOpenGlRender(GlInterface gl,int fb)=>RenderGraphics();
    internal void RenderGraphics()
    {
        if(api is not {} g) return;
        try
        {
            int width=Math.Max(1,(int)(Bounds.Width*(TopLevelScale()))),height=Math.Max(1,(int)(Bounds.Height*TopLevelScale()));
            g.Disable(EnableCap.ScissorTest);g.Disable(EnableCap.StencilTest);g.ColorMask(true,true,true,true);g.DepthMask(true);g.Viewport(0,0,(uint)width,(uint)height);g.ClearColor(BackgroundColor.X,BackgroundColor.Y,BackgroundColor.Z,BackgroundColor.W);g.Clear(ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);
            if(Document is null)return;
            g.Enable(EnableCap.DepthTest);g.Disable(EnableCap.CullFace);g.UseProgram(program);g.BindVertexArray(vao);g.ActiveTexture(TextureUnit.Texture0);g.BindTexture(TextureTarget.Texture2D,texture);
            var vp=Camera.ViewProjection((float)Bounds.Width/Math.Max(1,(float)Bounds.Height));UniformMatrix("viewProjection",vp);
            var pose=ReferencePose||!SkeletalAnimationVisible?AnimationService.Evaluate(Document.Project.Scene,null,0):Document.Evaluate();currentPose=pose;
            // Dynamic line data and mesh share a buffer; reupload only prepared geometry, never decode here.
            if(FloorVisible)DrawGrid();CheckGraphics("grid");if(pose.World.Length==0){PresentedFrames++;RenderError=null;return;}UploadMesh(Document.Project.Scene);CheckGraphics("mesh upload");
            var morph=ReferencePose?null:Document.EvaluateMorph(pose);
            if(completeSkin)morphStream!.ApplyCompleteSkin(uploadedVertices,pose,morph);else morphStream!.Apply(morph,vbo);
            MorphUploads=morphStream.Uploads;MorphBytes=morphStream.Bytes;DisplayedMorphBindings=morphStream.Bindings;
            g.Uniform1(g.GetUniformLocation(program,"skinned"),completeSkin?0:1);g.Uniform1(g.GetUniformLocation(program,"lit"),1);
            g.Uniform3(g.GetUniformLocation(program,"offset"),0f,0f,0f);
            fixed(Matrix4x4* p=pose.Skin) g.UniformMatrix4(g.GetUniformLocation(program,"bones[0]"),(uint)pose.Skin.Length,false,(float*)p);
            CheckGraphics("skin uniforms");
            if(RequestValidationProbe&&!Document.Clock.Playing){RequestValidationProbe=false;ValidateGpuSkinning(pose);}
            CheckGraphics("skin probe");
            DrawMeshes(pose,Vector3.Zero);
            CheckGraphics("mesh draws");
            if(Compare&&Document.Project.Clips.Length>0)
            {
                var reference=Document.Project.Clips[(Document.SelectedClip+1)%Document.Project.Clips.Length];
                double referenceFrame=Document.Clock.Frame/Document.Clip.Rate.FramesPerSecond*reference.Rate.FramesPerSecond;referenceFrame=reference.Loop?referenceFrame%reference.SampleCount:Math.Min(referenceFrame,reference.LastSampleFrame);
                var bind=AnimationService.Evaluate(Document.Project.Scene,reference,referenceFrame);
                if(completeSkin)morphStream!.ApplyCompleteSkin(uploadedVertices,bind,null);
                g.UseProgram(program);
                fixed(Matrix4x4* p=bind.Skin)g.UniformMatrix4(g.GetUniformLocation(program,"bones[0]"),(uint)bind.Skin.Length,false,(float*)p);
                g.UseProgram(program);g.Uniform3(g.GetUniformLocation(program,"offset"),comparisonOffset,0,0);DrawMeshes(bind,new(comparisonOffset,0,0));
            }
            g.UseProgram(program);g.ActiveTexture(TextureUnit.Texture0);g.Disable(EnableCap.Blend);g.Disable(EnableCap.CullFace);g.DepthMask(true);g.DepthFunc(DepthFunction.Less);g.Uniform3(g.GetUniformLocation(program,"offset"),0f,0f,0f);
            DrawOverlays(pose);
            DrawMorphPoint(pose);
            CheckGraphics("overlays");
            var now=Stopwatch.GetTimestamp();if(lastTimestamp!=0 && RenderIntervalsMs.Count<20000)RenderIntervalsMs.Add(Stopwatch.GetElapsedTime(lastTimestamp,now).TotalMilliseconds);lastTimestamp=now;
            PresentedFrames++; RenderError=null;
        }
        catch(Exception e) { RenderError=e.ToString(); }
        // Avalonia schedules composition; no busy render loop and no IO/solver work.
    }
    void CheckGraphics(string stage){if(StrictGraphicsDiagnostics&&api!.GetError() is var error&&error!=GLEnum.NoError)throw new InvalidOperationException($"GPU {stage}: {error}.");}
    double TopLevelScale()=>Avalonia.Controls.TopLevel.GetTopLevel(this)?.RenderScaling??1;
    void UniformMatrix(string name,Matrix4x4 value)=>api!.UniformMatrix4(api.GetUniformLocation(program,name),1,false,(float*)&value);
    static float[] Pack(IEnumerable<Vertex> vertices)=>vertices.SelectMany(v=>new[]{v.Position.X,v.Position.Y,v.Position.Z,v.Normal.X,v.Normal.Y,v.Normal.Z,v.UV.X,v.UV.Y,v.Joints.X,v.Joints.Y,v.Joints.Z,v.Joints.W,v.Weights.X,v.Weights.Y,v.Weights.Z,v.Weights.W,v.Color.X,v.Color.Y,v.Color.Z,v.Color.W,v.Color1.X,v.Color1.Y,v.Color1.Z,v.Color1.W}.Concat(Enumerable.Range(1,7).SelectMany(i=>i<v.UVSets.Length?new[]{v.UVSets[i].X,v.UVSets[i].Y}:new[]{0f,0f}))).ToArray();
    void UploadMesh(Scene scene)
    {
        var g=api!;g.BindVertexArray(vao);
        if(ReferenceEquals(uploadedScene,scene))return;
        morphStream?.Reset(vbo);
        uploadedVertices=scene.Meshes.SelectMany(x=>x.Vertices).ToArray();completeSkin=uploadedVertices.Any(v=>v.InfluenceCount>4);
        var data=Pack(uploadedVertices);var indices=new List<uint>();uint offset=0;
        ranges.Clear();foreach(var m in scene.Meshes){ranges.Add((m,indices.Count,m.Indices.Length));indices.AddRange(m.Indices.Select(x=>x+offset));offset+=(uint)m.Vertices.Length;}
        nativeMaterials?.Dispose();nativeMaterials=new(g,GpuInfo.Contains("OpenGL ES",StringComparison.Ordinal));drawRanges.Clear();
        foreach(var range in ranges){var calls=range.Mesh.DrawCalls.Length>0?range.Mesh.DrawCalls:[new(range.Mesh.Material,range.Mesh.VisibilityBone,true,0,0,0)];for(int i=0;i<calls.Length;i++)drawRanges.Add((range.Mesh,range.Offset,range.Count,calls[i],i));}
        drawRanges.Sort((a,b)=>{int pass=a.Call.Pass.CompareTo(b.Call.Pass);if(pass!=0)return pass;int priority=a.Call.Priority.CompareTo(b.Call.Priority);return priority!=0?priority:a.Call.Pass==1?a.Call.MaterialIndex.CompareTo(b.Call.MaterialIndex):0;});
        var idx=indices.ToArray();g.BindBuffer(BufferTargetARB.ArrayBuffer,vbo);g.BindBuffer(BufferTargetARB.ElementArrayBuffer,ebo);
        fixed(float* p=data)g.BufferData(BufferTargetARB.ArrayBuffer,(nuint)(data.Length*4),p,BufferUsageARB.StaticDraw);
        fixed(uint* p=idx)g.BufferData(BufferTargetARB.ElementArrayBuffer,(nuint)(idx.Length*4),p,BufferUsageARB.StaticDraw);
        var edges=new uint[idx.Length*2];for(int i=0;i+2<idx.Length;i+=3){int o=i*2;edges[o]=idx[i];edges[o+1]=idx[i+1];edges[o+2]=idx[i+1];edges[o+3]=idx[i+2];edges[o+4]=idx[i+2];edges[o+5]=idx[i];}
        g.BindBuffer(BufferTargetARB.ElementArrayBuffer,wireEbo);fixed(uint* p=edges)g.BufferData(BufferTargetARB.ElementArrayBuffer,(nuint)(edges.Length*4),p,BufferUsageARB.StaticDraw);g.BindBuffer(BufferTargetARB.ElementArrayBuffer,ebo);
        foreach(var t in textures.Values)g.DeleteTexture(t);textures.Clear();
        foreach(var t in scene.Textures)
        {
            uint tex=g.GenTexture();textures[t.Name]=tex;g.BindTexture(TextureTarget.Texture2D,tex);
            fixed(byte* p=t.Rgba)g.TexImage2D(TextureTarget.Texture2D,0,InternalFormat.Rgba,(uint)t.Width,(uint)t.Height,0,PixelFormat.Rgba,PixelType.UnsignedByte,p);
            g.GenerateMipmap(TextureTarget.Texture2D);
            g.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter,(int)TextureMinFilter.Linear);g.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter,(int)TextureMagFilter.Linear);
        }
        elementCount=idx.Length;uploadedScene=scene;
    }
    void DrawMeshes(EvaluatedPose pose,Vector3 offset)
    {
        if(!PolygonsVisible&&!VerticesVisible)return;
        var g=api!;g.BindVertexArray(vao);var scene=Document.Project.Scene;var visibility=SyncVisibility&&!ReferencePose?Document.Layer("VIS0"):null;var srt=ReferencePose?null:Document.Layer("SRT0");
        foreach(var range in drawRanges)
        {
            if(HiddenMeshes.Contains(range.Mesh.Name)||HiddenDrawCalls.Contains((range.Mesh.Name,range.Index)))continue;
            var vis=visibility?.Tracks.FirstOrDefault(t=>t.Target==range.Call.VisibilityBone)?.Visibility;
            if(vis is {Length:>0} && !vis[Math.Clamp((int)Document.LayerFrame(visibility!),0,vis.Length-1)])continue;
            if(vis is null&&!range.Call.DefaultVisible&&!ShownMeshes.Contains(range.Mesh.Name))continue;
            var material=scene.Materials.FirstOrDefault(m=>m.Name==range.Call.Material);string? image=material?.Textures.FirstOrDefault();
            bool hiddenTexture=material?.Textures.Any(HiddenTextures.Contains)==true;
            bool bound=ShadersVisible&&!hiddenTexture&&material is not null&&nativeMaterials!.Bind(material,Camera,Camera.ViewProjection((float)Bounds.Width/Math.Max(1,(float)Bounds.Height)),pose,offset,textures,Document,ReferencePose,completeSkin);
            NativeProgramCount=Math.Max(NativeProgramCount,nativeMaterials!.ProgramCount);foreach(var message in nativeMaterials.Diagnostics)if(!MaterialDiagnostics.Contains(message)){MaterialDiagnostics.Add(message);MaterialDiagnosticRaised?.Invoke(message);}
            if(bound){NativeDrawCount++;DrawVisibleRange(range.Offset,range.Count);continue;}
            g.UseProgram(program);g.ActiveTexture(TextureUnit.Texture0);g.Disable(EnableCap.Blend);g.Disable(EnableCap.CullFace);g.DepthMask(true);g.Enable(EnableCap.DepthTest);g.DepthFunc(DepthFunction.Less);
            bool native=!hiddenTexture&&image is not null && textures.TryGetValue(image,out _);g.BindTexture(TextureTarget.Texture2D,native?textures[image!]:texture);g.Uniform1(g.GetUniformLocation(program,"nativeTexture"),native?1:0);
            float sx=1,sy=1,r=0,tx=0,ty=0;var track=srt?.Tracks.FirstOrDefault(t=>t.Target==range.Mesh.Material && t.Slot==0);
            if(track is not null){double f=Document.LayerFrame(srt!);sx=track.Curves[0]?.Sample(f)??1;sy=track.Curves[1]?.Sample(f)??1;r=track.Curves[2]?.Sample(f)??0;tx=track.Curves[3]?.Sample(f)??0;ty=track.Curves[4]?.Sample(f)??0;}
            g.Uniform4(g.GetUniformLocation(program,"uvTransform"),sx,sy,tx,ty);g.Uniform1(g.GetUniformLocation(program,"uvRotation"),r*MathF.PI/180);
            DrawVisibleRange(range.Offset,range.Count);
        }
    }
    void DrawVisibleRange(int offset,int count)
    {
        var g=api!;
        if(Wireframe&&PolygonsVisible){g.BindBuffer(BufferTargetARB.ElementArrayBuffer,wireEbo);g.DrawElements(PrimitiveType.Lines,(uint)(count*2),DrawElementsType.UnsignedInt,(void*)(offset*8));g.BindBuffer(BufferTargetARB.ElementArrayBuffer,ebo);}
        else g.DrawElements(PolygonsVisible?PrimitiveType.Triangles:PrimitiveType.Points,(uint)count,DrawElementsType.UnsignedInt,(void*)(offset*4));
        if(VerticesVisible&&PolygonsVisible){g.DepthFunc(DepthFunction.Lequal);g.DrawElements(PrimitiveType.Points,(uint)count,DrawElementsType.UnsignedInt,(void*)(offset*4));}
    }
    void ValidateGpuSkinning(EvaluatedPose pose)
    {
        // Explicit acceptance job only. This readback/CPU oracle never runs in normal playback.
        var g=api!;var vertices=Document.Project.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();if(vertices.Length==0)return;
        // Keep transform-feedback shader variants separate from programs used for displayed triangles.
        g.UseProgram(probeProgram);var vp=Camera.ViewProjection((float)Bounds.Width/Math.Max(1,(float)Bounds.Height));
        g.UniformMatrix4(g.GetUniformLocation(probeProgram,"viewProjection"),1,false,(float*)&vp);
        fixed(Matrix4x4* p=pose.Skin)g.UniformMatrix4(g.GetUniformLocation(probeProgram,"bones[0]"),(uint)pose.Skin.Length,false,(float*)p);
        g.Uniform1(g.GetUniformLocation(probeProgram,"skinned"),completeSkin?0:1);g.Uniform3(g.GetUniformLocation(probeProgram,"offset"),0f,0f,0f);
        uint feedback=g.GenBuffer();bool mapped=false;
        float error=0,normalError=0,colorError=0;
        try
        {
            g.BindBuffer(BufferTargetARB.TransformFeedbackBuffer,feedback);g.BufferData(BufferTargetARB.TransformFeedbackBuffer,(nuint)(vertices.Length*56),null,BufferUsageARB.StreamRead);g.BindBufferBase(BufferTargetARB.TransformFeedbackBuffer,0,feedback);
            // Discard alone leaks point fragments on the measured ANGLE backend. Probes must never
            // change the displayed color/depth attachments, even if a driver fails to discard them.
            g.ColorMask(false,false,false,false);g.DepthMask(false);
            g.Enable(EnableCap.RasterizerDiscard);g.BeginTransformFeedback(PrimitiveType.Points);g.DrawArrays(PrimitiveType.Points,0,(uint)vertices.Length);g.EndTransformFeedback();g.Disable(EnableCap.RasterizerDiscard);g.Finish();
            var data=(float*)g.MapBufferRange(BufferTargetARB.TransformFeedbackBuffer,0,(nuint)(vertices.Length*56),MapBufferAccessMask.ReadBit);
            if(data==null)throw new InvalidOperationException("GPU acceptance readback unavailable.");mapped=true;
            var morph=ReferencePose?null:Document.EvaluateMorph(pose);for(int i=0;i<vertices.Length;i++)
            {
                int o=i*14;var vertex=morph?.Vertex(i,vertices[i])??vertices[i];error=Math.Max(error,Vector3.Distance(new(data[o],data[o+1],data[o+2]),AnimationService.Skin(vertex,pose)));
                Vector3 n=Vector3.Zero;for(int j=0;j<vertex.InfluenceCount;j++)n+=Vector3.TransformNormal(vertex.Normal,pose.Skin[(int)vertex.JointAt(j)])*vertex.WeightAt(j);normalError=Math.Max(normalError,Vector3.Distance(new(data[o+3],data[o+4],data[o+5]),n));
                colorError=Math.Max(colorError,Vector4.Distance(new(data[o+6],data[o+7],data[o+8],data[o+9]),vertex.Color));colorError=Math.Max(colorError,Vector4.Distance(new(data[o+10],data[o+11],data[o+12],data[o+13]),vertex.Color1));
            }
        }
        finally{if(mapped)g.UnmapBuffer(BufferTargetARB.TransformFeedbackBuffer);g.Disable(EnableCap.RasterizerDiscard);g.ColorMask(true,true,true,true);g.DepthMask(true);g.BindBufferBase(BufferTargetARB.TransformFeedbackBuffer,0,0);g.BindBuffer(BufferTargetARB.TransformFeedbackBuffer,0);g.DeleteBuffer(feedback);g.UseProgram(program);}
        GpuProbeSamples++;GpuProbeMaxError=Math.Max(GpuProbeMaxError,error);
        GpuNormalProbeMaxError=Math.Max(GpuNormalProbeMaxError,normalError);GpuColorProbeMaxError=Math.Max(GpuColorProbeMaxError,colorError);
        if(error>.003f||normalError>.003f||colorError>.003f)throw new InvalidOperationException($"GPU vertex stream differs from CPU expectations: position={error}, normal={normalError}, color={colorError}");
    }
    void Lines(List<Vertex> vertices,bool depth)
    {
        if(vertices.Count==0)return;var g=api!;var data=Pack(vertices);
        if(depth)g.Enable(EnableCap.DepthTest);else g.Disable(EnableCap.DepthTest);
        g.Uniform1(g.GetUniformLocation(program,"skinned"),0);g.Uniform1(g.GetUniformLocation(program,"lit"),0);
        g.BindVertexArray(lineVao);g.BindBuffer(BufferTargetARB.ArrayBuffer,lineVbo);fixed(float* p=data)g.BufferData(BufferTargetARB.ArrayBuffer,(nuint)(data.Length*4),p,BufferUsageARB.DynamicDraw);
        g.DrawArrays(PrimitiveType.Lines,0,(uint)vertices.Count);
    }
    static void Line(List<Vertex> v,Vector3 a,Vector3 b,Vector4 color)
    { v.Add(new(a,Vector3.UnitY,Vector2.Zero,Vector4.Zero,Vector4.Zero,color));v.Add(new(b,Vector3.UnitY,Vector2.Zero,Vector4.Zero,Vector4.Zero,color)); }
    void DrawGrid()
    {
        var lines=new List<Vertex>();float s=SceneScale;
        if(SupportContext is {} surface)
        {
            var inward=surface.Edge?.Inward??Vector3.Normalize(Vector3.Cross(Math.Abs(Vector3.Dot(surface.Normal,Vector3.UnitX))<.9f?Vector3.UnitX:Vector3.UnitY,surface.Normal));
            var along=Vector3.Normalize(Vector3.Cross(surface.Normal,inward));var origin=surface.Normal*surface.Height+inward*(surface.Edge?.Offset??0);var color=new Vector4(.16f,.28f,.35f,1);
            for(int i=-10;i<=10;i++)
            {
                Line(lines,origin+along*i*s+inward*(surface.Edge is null?-10:0)*s,origin+along*i*s+inward*10*s,color);
                if(surface.Edge is null||i>=0)Line(lines,origin+inward*i*s-along*10*s,origin+inward*i*s+along*10*s,color);
                if(surface.Edge is not null){Line(lines,origin+along*i*s,origin+along*i*s-surface.Normal*10*s,color);if(i>=0)Line(lines,origin-surface.Normal*i*s-along*10*s,origin-surface.Normal*i*s+along*10*s,color);}
            }
            if(surface.Edge is not null)Line(lines,origin-along*10*s,origin+along*10*s,new(1,.65f,.15f,1));
            Lines(lines,true);return;
        }
        for(int i=-10;i<=10;i++) { var c=new Vector4(.12f,.18f,.23f,1);Line(lines,new(i*s,0,-10*s),new(i*s,0,10*s),c);Line(lines,new(-10*s,0,i*s),new(10*s,0,i*s),c); }
        Lines(lines,true);
    }
    Vector3 BonePoint(int i)=>currentPose!.World[i].Translation;
    Vector3 Axis(int axis)
    {
        if(IkChain is not null||SelectedPinId is not null)return axis==0?Vector3.UnitX:axis==1?Vector3.UnitY:Vector3.UnitZ;
        var b=Document.Project.Scene.Bones[Document.SelectedBone];
        var parent=b.Parent<0?Matrix4x4.Identity:currentPose!.World[b.Parent];
        return Vector3.Normalize(Vector3.TransformNormal(axis==0?Vector3.UnitX:axis==1?Vector3.UnitY:Vector3.UnitZ,parent));
    }
    void DrawOverlays(EvaluatedPose pose)
    {
        var lines=new List<Vertex>();
        if(ModelBoundsVisible||ObjectBoundsVisible)
        {
            var groups=ObjectBoundsVisible?Document.Project.Scene.Meshes.Where(m=>!HiddenMeshes.Contains(m.Name)).Select(m=>m.Vertices):[Document.Project.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray()];
            foreach(var vertices in groups){var points=vertices.Select(v=>AnimationService.Skin(v,pose)).ToArray();if(points.Length==0)continue;var min=points.Aggregate(Vector3.Min);var max=points.Aggregate(Vector3.Max);Vector3[] corners=Enumerable.Range(0,8).Select(i=>new Vector3((i&1)==0?min.X:max.X,(i&2)==0?min.Y:max.Y,(i&4)==0?min.Z:max.Z)).ToArray();for(int i=0;i<8;i++)foreach(int bit in new[]{1,2,4})if((i&bit)==0)Line(lines,corners[i],corners[i|bit],new(1,.75f,.1f,1));}
        }
        if(SkeletonVisible) for(int i=0;i<pose.World.Length;i++)
        {
            if(HiddenBones.Contains(i))continue;
            int p=Document.Project.Scene.Bones[i].Parent;
            Line(lines,p>=0?BonePoint(p):Vector3.Zero,BonePoint(i),new(0,0,128/255f,1));
        }
        Lines(lines,false);lines.Clear();
        if(SkeletonVisible){float radius=Document.Project.SourceModels.Any(m=>m.Resource.Format=="MDL0")?.2f:ControlSetup is {} controls?PoseGeometry.Scale(Document.Project.Scene,controls)*.025f:SceneScale*.025f;jointDisplay!.Draw(pose,Camera.ViewProjection((float)Bounds.Width/Math.Max(1,(float)Bounds.Height)),radius,Document.SelectedBone,hoveredBone,HiddenBones);DisplayedJoints=pose.World.Length-HiddenBones.Count;api!.UseProgram(program);}else DisplayedJoints=0;
        foreach(var pin in Document.CurrentContacts)
        {
            var p=ContactGeometry.Target(pin,pose);float size=SceneScale*.045f;var c=new Vector4(1,.65f,.15f,1);Line(lines,p-Vector3.UnitX*size,p+Vector3.UnitX*size,c);Line(lines,p-Vector3.UnitZ*size,p+Vector3.UnitZ*size,c);Line(lines,ContactGeometry.Point(pin,pose),p,c);
        }
        DrawEditorPinMarkers(lines,pose);
        if(Document.IsSourcePreview||!PoseEditingEnabled){Lines(lines,false);return;}
        if(ControlSetup is {} setup)foreach(var chain in setup.Chains)
        {
            if(chain.Effector>=pose.World.Length)continue;var point=pose.World[chain.Effector].Translation;float size=SceneScale*.035f;
            Line(lines,point-Vector3.UnitX*size,point+Vector3.UnitX*size,new(1,.8f,.3f,1));Line(lines,point-Vector3.UnitY*size,point+Vector3.UnitY*size,new(1,.8f,.3f,1));
        }
        if(IkChain is {} active)
        {
            if(Document.Clock.Playing&&!IsDragging)ResetControlTargets();
            Line(lines,pose.World[active.Middle].Translation,IkPole,new(.9f,.35f,.8f,1));float s=SceneScale*.05f;
            Line(lines,IkPole-Vector3.UnitX*s,IkPole+Vector3.UnitX*s,new(.9f,.35f,.8f,1));Line(lines,IkPole-Vector3.UnitY*s,IkPole+Vector3.UnitY*s,new(.9f,.35f,.8f,1));
        }
        if(PickPinsOnly&&SelectedEditorPin is null){Lines(lines,false);return;}
        Vector4[] colors=[new(1,.3f,.34f,1),new(.42f,.9f,.45f,1),new(.37f,.61f,1,1)];var origin=SelectedEditorPin?.Position??(IkChain is null?BonePoint(Document.SelectedBone):EditPole?IkPole:IkGoal);float length=SceneScale*.65f;
        for(int i=0;i<3;i++)
        {
            if(Mode==GizmoMode.Rotate)
            {
                var a=Axis((i+1)%3);var b=Axis((i+2)%3);
                for(int j=0;j<48;j++)Line(lines,origin+(a*MathF.Cos(j*MathF.Tau/48)+b*MathF.Sin(j*MathF.Tau/48))*length,origin+(a*MathF.Cos((j+1)*MathF.Tau/48)+b*MathF.Sin((j+1)*MathF.Tau/48))*length,colors[i]);
            }
            else Line(lines,origin,origin+Axis(i)*length,colors[i]);
        }
        Lines(lines,false);
    }
    protected override void OnOpenGlDeinit(GlInterface gl)=>DisposeGraphics();
    internal void DisposeGraphics()
    {
        if(api is not {} g)return;morphStream?.Dispose();morphStream=null;jointDisplay?.Dispose();jointDisplay=null;nativeMaterials?.Dispose();nativeMaterials=null;foreach(var t in textures.Values)g.DeleteTexture(t);textures.Clear();g.DeleteBuffer(vbo);g.DeleteBuffer(ebo);g.DeleteBuffer(wireEbo);g.DeleteBuffer(lineVbo);g.DeleteVertexArray(lineVao);g.DeleteVertexArray(vao);g.DeleteTexture(texture);g.DeleteProgram(program);g.DeleteProgram(probeProgram);g.Dispose();api=null;lastTimestamp=0;
    }
    public void FrameScene()
    {
        if(Document is null)return;
        var vertices=Document.Project.Scene.Meshes.SelectMany(m=>m.Vertices).Select(v=>v.Position).ToArray();if(vertices.Length==0)return;
        var min=vertices.Aggregate(Vector3.Min);var max=vertices.Aggregate(Vector3.Max);comparisonOffset=Math.Max((max.X-min.X)*1.15f,.1f);if(Compare)max.X+=comparisonOffset;Camera.Target=(min+max)/2;Camera.Distance=Math.Max((max-min).Length()*1.5f,.1f);Refresh();
    }
    public void FrameCurrentPose()
    {
        var pose=Document.Evaluate();var morph=Document.EvaluateMorph(pose);int index=0;
        var points=Document.Project.Scene.Meshes.SelectMany(m=>m.Vertices).Select(v=>AnimationService.Skin(morph?.Vertex(index++,v)??v,pose)).ToArray();
        if(points.Length==0)return;
        var min=points.Aggregate(Vector3.Min);var max=points.Aggregate(Vector3.Max);Camera.Target=(min+max)/2;Camera.Distance=Math.Max((max-min).Length()*1.5f,.1f);
    }
    public void InspectionAngle(string angle)
    {
        Camera.Yaw=angle switch{"Front"=>0,"Side"=>MathF.PI/2,"Rear"=>MathF.PI,"Low"=>-.8f,_=>throw new ArgumentException("Unknown inspection angle.",nameof(angle))};
        Camera.Orthographic=angle!="Low";Camera.Pitch=angle=="Low"?MathF.Asin(Math.Clamp((.02f-Camera.Target.Y)/Camera.Distance,-.95f,.95f)):0;
    }
    Vector2 Screen(Vector3 p)=>Camera.Project(p,(float)Bounds.Width,(float)Bounds.Height);
    static float DistanceSegment(Vector2 p,Vector2 a,Vector2 b) {var d=b-a;float u=Math.Clamp(Vector2.Dot(p-a,d)/Math.Max(d.LengthSquared(),.0001f),0,1);return Vector2.Distance(p,a+d*u);}
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if(Document is null)return;
        Focus();previous=dragOrigin=e.GetPosition(this);var props=e.GetCurrentPoint(this).Properties;rightCamera=props.IsRightButtonPressed;middleCamera=props.IsMiddleButtonPressed;
        if(props.IsLeftButtonPressed && currentPose is {World.Length:>0} && !Document.IsSourcePreview && PoseEditingEnabled)
        {
            var point=new Vector2((float)previous.X,(float)previous.Y);
            if(MorphGeometryMode&&PointBinding() is not null){PickMorphPoint(point);e.Handled=true;Refresh();return;}
            if(TryPickEditorPin(point)){e.Handled=true;return;}
            if(PickPinsOnly&&SelectedEditorPin is null){e.Handled=true;return;}
            if(!PickPinsOnly&&SelectedPinId is null&&Mode==GizmoMode.Translate&&ControlSetup is {} rig && IkChain is null){var picked=rig.Chains.FirstOrDefault(c=>Vector2.Distance(point,Screen(BonePoint(c.Effector)))<9);if(picked is not null){EnableIK(rig,picked);PoseChanged?.Invoke();}}
            float best=13;var o=SelectedEditorPin?.Position??(IkChain is null?BonePoint(Document.SelectedBone):EditPole?IkPole:IkGoal);float length=SceneScale*.65f;
            for(int axis=0;axis<3;axis++)
            {
                float distance;
                if(Mode==GizmoMode.Rotate)
                {
                    distance=float.MaxValue;var a=Axis((axis+1)%3);var b=Axis((axis+2)%3);
                    for(int j=0;j<48;j++)distance=Math.Min(distance,DistanceSegment(point,Screen(o+(a*MathF.Cos(j*MathF.Tau/48)+b*MathF.Sin(j*MathF.Tau/48))*length),Screen(o+(a*MathF.Cos((j+1)*MathF.Tau/48)+b*MathF.Sin((j+1)*MathF.Tau/48))*length)));
                }
                else distance=DistanceSegment(point,Screen(o),Screen(o+Axis(axis)*length));
                if(distance<best){best=distance;dragAxis=axis;}
            }
            if(dragAxis>=0)BeginPoseGesture();
            else if(!PickPinsOnly&&SkeletonVisible)for(int i=0;i<currentPose.World.Length;i++)if(!HiddenBones.Contains(i)&&Vector2.Distance(point,Screen(BonePoint(i)))<10){ClearEditorPinSelection();Document.SelectedBone=i;PoseChanged?.Invoke();break;}
        }
        if(Environment.GetEnvironmentVariable("FIGHTERPORT_GPU_PROBE")=="1")PointerDiagnostics.Add($"{Mode}: press {previous}, left={props.IsLeftButtonPressed}, axis={dragAxis}, projected={Screen(BonePoint(Document.SelectedBone))}, bounds={Bounds}");
        e.Pointer.Capture(this);e.Handled=true;Refresh();
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if(Document is null)return;
        var p=e.GetPosition(this);float dx=(float)(p.X-previous.X),dy=(float)(p.Y-previous.Y);
        if((rightCamera||middleCamera)&&CameraInput.WrapCursor&&OperatingSystem.IsWindows()&&e.Pointer.Captured==this&&Avalonia.Controls.TopLevel.GetTopLevel(this) is Avalonia.Controls.Window{IsActive:true}&&Bounds.Width>4&&Bounds.Height>4)
        {
            double x=p.X,y=p.Y;
            if(x<=1&&dx<0)x=Bounds.Width-2;else if(x>=Bounds.Width-1&&dx>0)x=2;
            if(y<=1&&dy<0)y=Bounds.Height-2;else if(y>=Bounds.Height-1&&dy>0)y=2;
            if(x!=p.X||y!=p.Y){var wrapped=new Point(x,y);var screen=this.PointToScreen(wrapped);if(SetCameraCursorPosition(screen.X,screen.Y)){previous=wrapped;e.Handled=true;return;}}
        }
        if(rightCamera||middleCamera)CameraInput.Drag(dx,-dy,middleCamera,e.KeyModifiers);
        if(dragAxis>=0 && initial is {} t)
        {
            var amount=(float)(p.X-dragOrigin.X-p.Y+dragOrigin.Y);var axis=dragAxis==0?Vector3.UnitX:dragAxis==1?Vector3.UnitY:Vector3.UnitZ;
            if(SelectedPinId is not null){var position=Mode==GizmoMode.Translate?initialPinPosition+axis*amount*SceneScale*.006f:initialPinPosition;var rotation=Mode==GizmoMode.Rotate?Quaternion.Normalize(Quaternion.Concatenate(initialPinRotation,Quaternion.CreateFromAxisAngle(axis,amount*.8f*MathF.PI/180))):initialPinRotation;PreviewPinTransform(position,rotation,Mode==GizmoMode.Scale?Vector3.Max(new(.01f),t.Scale+axis*amount*.01f):null);}
            else if(IkChain is not null){var delta=axis*amount*SceneScale*.006f;PreviewIK(EditPole?initialGoal:initialGoal+delta,EditPole?initialPole+delta:initialPole);}
            else{t=Mode switch {GizmoMode.Translate=>t with {Translation=t.Translation+axis*amount*SceneScale*.006f},GizmoMode.Rotate=>t with {Rotation=t.Rotation+axis*amount*.8f},_=>t with {Scale=Vector3.Max(new(.01f),t.Scale+axis*amount*.01f)}};PreviewFK(gestureBone,t);}
            PoseChanged?.Invoke();
        }
        if(!rightCamera&&!middleCamera&&dragAxis<0&&currentPose is {World.Length:>0}&&SkeletonVisible){var point=new Vector2((float)p.X,(float)p.Y);int next=Enumerable.Range(0,currentPose.World.Length).Where(i=>!HiddenBones.Contains(i)).OrderBy(i=>Vector2.DistanceSquared(point,Screen(BonePoint(i)))).FirstOrDefault(-1);if(next>=0)next=Vector2.Distance(point,Screen(BonePoint(next)))<9?next:-1;if(next!=hoveredBone){hoveredBone=next;Refresh();}}
        previous=p;if(rightCamera||middleCamera||dragAxis>=0)Refresh();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        CommitPoseGesture();rightCamera=middleCamera=false;e.Pointer.Capture(null);Refresh();
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        CancelPoseGesture();rightCamera=middleCamera=false;base.OnPointerCaptureLost(e);Refresh();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if(e.Key==Avalonia.Input.Key.Escape&&transformEdit is not null){CancelPoseGesture();e.Handled=true;return;}
        if(e.Key==Avalonia.Input.Key.Escape&&SelectedPinId is not null){ClearEditorPinSelection();PoseChanged?.Invoke();e.Handled=true;return;}
        if(e.Key==Avalonia.Input.Key.Delete&&SelectedPinId is {} pin){ClearEditorPinSelection();Document.RemoveEditorPin(pin);PoseChanged?.Invoke();e.Handled=true;return;}
        if(!e.Handled&&CameraInput.Key(e.Key,e.KeyModifiers)){e.Handled=true;Refresh();return;}base.OnKeyDown(e);
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e){CameraInput.Wheel((float)e.Delta.Y,e.KeyModifiers);Refresh();e.Handled=true;}
    // Only reached during a physical captured camera drag in the active editor.
    // The offscreen/background verification route never calls this API.
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SetCursorPos")]
    [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    static extern bool SetCameraCursorPosition(int x,int y);
    const string VertexSource="""
    #version 330 core
    layout(location=0) in vec3 position;
    layout(location=1) in vec3 normal;
    layout(location=2) in vec2 texcoord;
    layout(location=3) in vec4 joints;
    layout(location=4) in vec4 weights;
    layout(location=5) in vec4 color;
    layout(location=6) in vec4 color1;
    uniform mat4 viewProjection;
    uniform mat4 bones[128];
    uniform int skinned;
    uniform vec3 offset;
    out vec3 n;out vec2 uv;out vec4 tint;out vec3 skinPosition;out vec3 probeNormal;out vec4 probeColor0;out vec4 probeColor1;
    void main(){mat4 s=mat4(1.0);if(skinned==1)s=bones[int(joints.x)]*weights.x+bones[int(joints.y)]*weights.y+bones[int(joints.z)]*weights.z+bones[int(joints.w)]*weights.w;
    skinPosition=(s*vec4(position,1.0)).xyz;gl_Position=viewProjection*vec4(skinPosition+offset,1.0);n=mat3(s)*normal;uv=texcoord;tint=color;probeNormal=n;probeColor0=color;probeColor1=color1;}
    """;
    const string FragmentSource="""
    #version 330 core
    in vec3 n;in vec2 uv;in vec4 tint;out vec4 outputColor;
    uniform sampler2D checker;uniform int lit;uniform int nativeTexture;
    uniform vec4 uvTransform;uniform float uvRotation;
    void main(){float l=lit==1?0.38+0.62*max(dot(normalize(n),normalize(vec3(-0.4,0.8,0.7))),0.0):1.0;
    vec2 p=uv*uvTransform.xy-vec2(0.5);float c=cos(uvRotation),s=sin(uvRotation);p=mat2(c,s,-s,c)*p+vec2(0.5)+uvTransform.zw;
    vec4 tex=lit==1?texture(checker,p*(nativeTexture==1?1.0:3.0)):vec4(1.0);
    if(lit==1 && tex.a<0.05)discard;
    outputColor=vec4((nativeTexture==1 && lit==1?vec3(1.0):tint.rgb)*l*tex.rgb,tint.a*tex.a);}
    """;
}
