using System.Numerics;
using System.Text.RegularExpressions;
using FighterPort.Core;
using Silk.NET.OpenGL;
using Gl=Silk.NET.OpenGL.GL;

namespace FighterPort.Rendering;

// Owns GPU programs/state. BrawlLib supplies only the audited generated TEV source/data.
internal sealed unsafe class NativeMaterialPipeline(Gl gl,bool es) : IDisposable
{
    readonly Dictionary<MaterialProgram,uint> programs=[];
    public List<string> Diagnostics {get;}=[];
    public int ProgramCount=>programs.Values.Count(p=>p!=0);
    public bool Bind(Material material,OrbitCamera camera,Matrix4x4 vp,EvaluatedPose pose,Vector3 offset,IReadOnlyDictionary<string,uint> textures,EditorDocument document,bool referencePose=false)
    {
        if(material.Program is not {} data)
        {
            if(material.Native is not null)DiagnosticsOnce("Native material program unavailable: "+material.Name+". Reimport an older project or inspect native shader support.");
            return false;
        }
        foreach(var limitation in data.Limitations)DiagnosticsOnce(material.Name+": "+limitation);
        if(!programs.TryGetValue(data,out uint program))
        {
            try{program=Compile(data);}catch(Exception e){Diagnostics.Add(material.Name+": "+e.Message);program=0;}programs[data]=program;
        }
        if(program==0)return false;
        gl.UseProgram(program);Matrix(program,"viewProjection",vp);Matrix(program,"view",camera.View);
        fixed(Matrix4x4* p=pose.Skin)gl.UniformMatrix4(gl.GetUniformLocation(program,"bones[0]"),(uint)pose.Skin.Length,false,(float*)p);
        gl.Uniform3(gl.GetUniformLocation(program,"offset"),offset.X,offset.Y,offset.Z);
        foreach(var pair in data.Colors)Vector(program,pair.Key,pair.Value);
        var clr=referencePose?null:document.Layer("CLR0");if(clr is not null)foreach(var track in clr.Tracks.Where(t=>t.Target==material.Name)){var uniform=NativeMaterialSampling.ColorUniform(track.Slot);if(uniform is not null&&data.Colors.TryGetValue(uniform,out var original))Vector(program,uniform,NativeMaterialSampling.Color(original,track,document.LayerFrame(clr)));}
        var srt=referencePose?null:document.Layer("SRT0");double srtFrame=srt is null?0:document.LayerFrame(srt);var pat=referencePose?null:document.Layer("PAT0");
        // BrawlCrate's default shader path: one directional viewport light and no SCN0 override.
        Vector(program,"scnSetAmbLight",new(90/255f,90/255f,90/255f,1));
        float az=45*MathF.PI/180,elevation=360f-45*MathF.PI/180;
        var position=new Vector3(100*(float)Math.Cos(az)*(float)Math.Sin(elevation),100*(float)Math.Cos(elevation),100*(float)Math.Sin(az)*(float)Math.Sin(elevation))*1e10f;
        for(int i=0;i<8;i++)gl.Uniform1(gl.GetUniformLocation(program,$"scnSetLights[{i}].enabled"),i==0?1:0);
        Vector3(program,"scnSetLights[0].pos",position);Vector3(program,"scnSetLights[0].dir",System.Numerics.Vector3.Zero);
        Vector(program,"scnSetLights[0].color",new(70/255f,70/255f,70/255f,1));Vector3(program,"scnSetLights[0].distCoefs",System.Numerics.Vector3.UnitX);Vector3(program,"scnSetLights[0].spotCoefs",System.Numerics.Vector3.UnitX);
        gl.Uniform1(gl.GetUniformLocation(program,"scnSetLights[0].hasSpecular"),1);Vector(program,"scnSetLights[0].specColor",new(0,0,0,1));Vector3(program,"scnSetLights[0].distCoefsSpec",new(32,0,-31));
        gl.Uniform1(gl.GetUniformLocation(program,"scnFogType"),0);
        for(int i=0;i<data.Bindings.Length;i++)
        {
            var binding=data.Bindings[i];var swap=pat is null?null:NativeMaterialSampling.Texture(pat.Tracks.FirstOrDefault(t=>t.Target==material.Name&&t.Slot==i),document.LayerFrame(pat));
            string name=string.IsNullOrEmpty(swap?.Texture)?binding.Texture:swap.Texture;string? palette=string.IsNullOrEmpty(swap?.Palette)?binding.Palette:swap.Palette;string key=string.IsNullOrEmpty(palette)?name:name+"::"+palette;
            if(!textures.TryGetValue(key,out uint image)){DiagnosticsOnce($"Unresolved native texture/palette: {material.Name}, slot {i}: {key}; fallback preview used.");if(!textures.TryGetValue(name,out image))return false;}
            gl.ActiveTexture(TextureUnit.Texture0+i);gl.BindTexture(TextureTarget.Texture2D,image);
            gl.Uniform1(gl.GetUniformLocation(program,$"texture{i}"),i);
            var animated=srt?.Tracks.FirstOrDefault(t=>t.Target==material.Name&&t.Slot==i);
            if(animated is not null){var values=binding.BaseSrt.Select((value,ch)=>animated.Curves[ch]?.Sample(srtFrame)??value).ToArray();var matrix=BrawlTextureTransform.Evaluate(values[0],values[1],values[2],values[3],values[4],srt!.MatrixMode);Matrix(program,$"textureMatrices[{i}]",matrix);}
            else fixed(float* p=binding.Matrix)gl.UniformMatrix4(gl.GetUniformLocation(program,$"textureMatrices[{i}]"),1,false,p);
            gl.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapS,binding.WrapS switch{0=>(int)TextureWrapMode.ClampToEdge,2=>(int)TextureWrapMode.MirroredRepeat,_=>(int)TextureWrapMode.Repeat});
            gl.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapT,binding.WrapT switch{0=>(int)TextureWrapMode.ClampToEdge,2=>(int)TextureWrapMode.MirroredRepeat,_=>(int)TextureWrapMode.Repeat});
            gl.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter,binding.MinFilter switch{0=>(int)TextureMinFilter.Nearest,1=>(int)TextureMinFilter.Linear,2=>(int)TextureMinFilter.NearestMipmapNearest,3=>(int)TextureMinFilter.LinearMipmapNearest,4=>(int)TextureMinFilter.NearestMipmapLinear,_=>(int)TextureMinFilter.LinearMipmapLinear});
            gl.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter,binding.MagFilter==0?(int)TextureMagFilter.Nearest:(int)TextureMagFilter.Linear);
        }
        if(data.Cull==0)gl.Disable(EnableCap.CullFace);else{gl.Enable(EnableCap.CullFace);gl.CullFace(data.Cull switch{1=>TriangleFace.Front,2=>TriangleFace.Back,_=>TriangleFace.FrontAndBack});}
        if(data.DepthTest)gl.Enable(EnableCap.DepthTest);else gl.Disable(EnableCap.DepthTest);
        gl.DepthMask(data.DepthWrite);gl.DepthFunc((DepthFunction)(512+data.DepthCompare));
        if(data.Blend){gl.Enable(EnableCap.Blend);gl.BlendEquation(data.Subtract?BlendEquationModeEXT.FuncSubtract:BlendEquationModeEXT.FuncAdd);gl.BlendFunc(Source(data.BlendSource),Destination(data.BlendDestination));}else gl.Disable(EnableCap.Blend);
        if(data.LogicOperation)DiagnosticsOnce("GX logic operations require a verified backend implementation: "+material.Name);
        return true;
    }
    void DiagnosticsOnce(string text){if(!Diagnostics.Contains(text))Diagnostics.Add(text);}
    static BlendingFactor Source(int value)=>(BlendingFactor)(value switch{0=>0,1=>1,2=>774,3=>775,4=>770,5=>771,6=>772,_=>773});
    static BlendingFactor Destination(int value)=>(BlendingFactor)(value switch{0=>0,1=>1,2=>768,3=>769,4=>770,5=>771,6=>772,_=>773});
    void Matrix(uint p,string name,Matrix4x4 m)=>gl.UniformMatrix4(gl.GetUniformLocation(p,name),1,false,(float*)&m);
    void Vector(uint p,string name,Vector4 v)=>gl.Uniform4(gl.GetUniformLocation(p,name),v.X,v.Y,v.Z,v.W);
    void Vector3(uint p,string name,System.Numerics.Vector3 v)=>gl.Uniform3(gl.GetUniformLocation(p,name),v.X,v.Y,v.Z);
    uint Compile(MaterialProgram data)
    {
        string vs=Adapt(data.VertexSource,true,data),fs=Adapt(data.FragmentSource,false,data);uint p=gl.CreateProgram(),v=0,f=0;
        try{v=Shader(ShaderType.VertexShader,vs);f=Shader(ShaderType.FragmentShader,fs);gl.AttachShader(p,v);gl.AttachShader(p,f);gl.LinkProgram(p);gl.GetProgram(p,ProgramPropertyARB.LinkStatus,out int ok);if(ok==0)throw new InvalidOperationException(gl.GetProgramInfoLog(p));return p;}
        catch{gl.DeleteProgram(p);throw;}finally{if(v!=0)gl.DeleteShader(v);if(f!=0)gl.DeleteShader(f);}
    }
    uint Shader(ShaderType type,string source){uint shader=gl.CreateShader(type);gl.ShaderSource(shader,source);gl.CompileShader(shader);gl.GetShader(shader,ShaderParameterName.CompileStatus,out int ok);if(ok==0){string error=gl.GetShaderInfoLog(shader);gl.DeleteShader(shader);throw new InvalidOperationException(error);}return shader;}
    string Adapt(string source,bool vertex,MaterialProgram data)
    {
        source=Regex.Replace(source,@"#version\s+120","");source=Regex.Replace(source,@"(\d+\.\d+)f\b","$1");
        source=source.Replace("varying ",vertex?"out ":"in ").Replace("texture2D(","texture(").Replace("gl_FragColor","outputColor");
        for(int i=0;i<8;i++)source=source.Replace($"gl_TexCoord[{i}]",$"fpTexCoord{i}").Replace($"gl_TextureMatrix[{i}]",$"textureMatrices[{i}]");
        string header=es?"#version 300 es\nprecision highp float;\nprecision highp int;\n":"#version 330 core\n";
        header+=string.Join("\n",Enumerable.Range(0,data.Bindings.Length).Select(i=>$"{(vertex?"out":"in")} vec4 fpTexCoord{i};"))+"\n";
        if(!vertex)return header+"out vec4 outputColor;\n"+source;
        header+="layout(location=0) in vec3 position;layout(location=1) in vec3 normal;layout(location=2) in vec2 uv0;layout(location=3) in vec4 joints;layout(location=4) in vec4 weights;layout(location=5) in vec4 color0;layout(location=6) in vec4 color1;\n";
        header+=string.Join("\n",Enumerable.Range(1,7).Select(i=>$"layout(location={i+6}) in vec2 uv{i};"));
        header+="\nuniform mat4 bones[128];uniform mat4 viewProjection;uniform mat4 view;uniform mat4 textureMatrices[8];uniform vec3 offset;\n";
        for(int i=0;i<8;i++){int coord=i<data.Bindings.Length?data.Bindings[i].Coordinate:i;source=source.Replace("gl_MultiTexCoord"+i,$"vec4(uv{Math.Clamp(coord,0,7)},0.0,1.0)");}
        source=source.Replace("gl_ModelViewProjectionMatrix","viewProjection").Replace("gl_ModelViewMatrix","view").Replace("gl_NormalMatrix","fpNormalMatrix").Replace("gl_Normal","fpWorldNormal").Replace("gl_Vertex","fpVertex").Replace("gl_SecondaryColor","color1").Replace("gl_Color","color0");
        int main=source.IndexOf("void main",StringComparison.Ordinal),brace=source.IndexOf('{',main);
        string skin="\nmat4 fpSkin=bones[int(joints.x)]*weights.x+bones[int(joints.y)]*weights.y+bones[int(joints.z)]*weights.z+bones[int(joints.w)]*weights.w;vec4 fpVertex=fpSkin*vec4(position,1.0);fpVertex.xyz+=offset;vec3 fpWorldNormal=mat3(fpSkin)*normal;mat3 fpNormalMatrix=mat3(view);\n";
        return header+source.Insert(brace+1,skin);
    }
    public void Dispose(){foreach(uint p in programs.Values)if(p!=0)gl.DeleteProgram(p);programs.Clear();}
}
