using BrawlLib.Internal;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.OpenGL;
using BrawlLib.SSBB.ResourceNodes;
using FighterPort.Adapters.Melee;
using HSDRaw;
using HSDRaw.Common;
using HSDRaw.GX;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using NMatrix=System.Numerics.Matrix4x4;
namespace BrawlCrate.Studio
{
    // Same ModelPanel/camera/offscreen renderer; native HSD material draw calls.
    // Geometry is CPU skinned with every native influence. The shader has no
    // GPU bone or influence cap. Native source graphs remain untouched.
    internal sealed class MeleeNativeRender:IRenderedObject,IDisposable
    {
                private readonly MeleePose nativePose;
        private readonly List<NativeDraw> draws=new List<NativeDraw>();
        private readonly List<DrawCallBase> drawCalls=new List<DrawCallBase>();
        private readonly Dictionary<HSDStruct,Texture> textures=new Dictionary<HSDStruct,Texture>();
        private readonly Dictionary<string,int> locations=new Dictionary<string,int>();
        private int program;
        private bool attached;
        internal int RenderCount;
        internal MeleePartVisibility Parts;
        private bool[] partVisibility;
        internal void RefreshParts(){partVisibility=Parts?.Snapshot();}
        public event EventHandler DrawCallsChanged {add{} remove{}}
        public List<DrawCallBase> DrawCalls=>drawCalls;
        public bool IsRendering{get;set;}=true;
        public bool Attached=>attached;
        internal MeleeNativeRender(MDL0Node model,MeleeProjectionReport report,MeleeNativeModel nativeModel)
        {
            nativePose=new MeleePose(nativeModel);
            foreach(var mesh in report.Meshes)
            {
                var obj=model.PolygonList.OfType<MDL0ObjectNode>().SingleOrDefault(o=>o.Name==mesh.AuthoringObject);
                var native=new NativeDraw(this,mesh,draws.Count,obj);draws.Add(native);drawCalls.Add(native);
                var seen=new HashSet<HSDStruct>();int count=0;
                for(var tex=mesh.Material?.Textures;tex!=null;tex=tex.Next)
                {
                    if(!seen.Add(tex._s)||++count>8)throw new NotSupportedException("Native HSD texture graph exceeds the GameCube eight-stage limit.");
                    if(tex.ImageData==null)continue;
                    if(!textures.ContainsKey(tex._s))textures.Add(tex._s,new Texture(tex));
                }
            }
        }
        public void Attach(){attached=true;}
        public void Detach(){if(!attached&&program==0)return;Dispose();attached=false;}
        public void Refresh(){}
        public void PreRender(ModelPanelViewport view){}
        public Box GetBox()
        {
            var box=Box.ExpandableVolume;foreach(var draw in draws.Where(d=>d.Visible))foreach(var vertex in draw.Vertices)box.ExpandVolume(vertex.Position);return box;
        }
        internal System.Numerics.Matrix4x4[] NativeWorld=>nativePose.World;
        internal void UpdateAuthored(float[][] values,bool[] classical){nativePose.SampleAuthored(values,classical);foreach(var draw in draws)draw.UpdatePose();}
        internal void UpdatePose(MeleePlayback playback,float frame){nativePose.Sample(playback,frame);foreach(var draw in draws)draw.UpdatePose();}
        private sealed class Texture
        {
            internal readonly HSD_TOBJ Native;
            internal readonly byte[] Pixels;
            internal int Id;
            internal Texture(HSD_TOBJ native)
            {
                Native=native;int w=native.ImageData.Width,h=native.ImageData.Height;
                if(w<1||h<1||(long)w*h>16777216)throw new InvalidDataException("Native texture exceeds the renderer's pixel budget.");
                Pixels=native.GetDecodedImageData();if(Pixels.Length!=w*h*4)throw new InvalidDataException("Native texture decoding failed its length check.");
            }
        }
        private sealed class SkinVertex
        {
            internal Vector3 Position,Normal;
            internal readonly MeleeVertex Native;
            internal SkinVertex(MeleeVertex v){Native=v;}
            internal void Update(NMatrix matrix,NMatrix normalMatrix)
            {
                var p=System.Numerics.Vector3.Transform(Native.NativePosition,matrix);Position=new Vector3(p.X,p.Y,p.Z);
                var n=System.Numerics.Vector3.TransformNormal(Native.NativeNormal,normalMatrix);Normal=new Vector3(n.X,n.Y,n.Z);
            }
        }
        private sealed class NativeDraw:DrawCallBase
        {
            internal readonly SkinVertex[] Vertices;
            internal readonly MeleeMesh Mesh;
            internal readonly MeleeNativeRender Owner;
            internal readonly int Index;
            private readonly MDL0ObjectNode authoringObject;
            internal bool Visible=>!Mesh.NativeHidden&&(authoringObject?.IsRendering??true)&&(Owner.partVisibility==null||Owner.partVisibility[Mesh.DisplayObject]);
            private readonly SkinVertex[][] matrixGroups;
            internal NativeDraw(MeleeNativeRender owner,MeleeMesh mesh,int index,MDL0ObjectNode obj)
            {
                Owner=owner;Mesh=mesh;Index=index;authoringObject=obj;_render=true;Vertices=mesh.Vertices.Select(v=>new SkinVertex(v)).ToArray();
                matrixGroups=Vertices.GroupBy(v=>v.Native.MatrixIndex).Select(g=>g.ToArray()).ToArray();
                foreach(var group in matrixGroups)if(group.Any(v=>!v.Native.Weights.SequenceEqual(group[0].Native.Weights)))throw new InvalidDataException("Native vertex matrix slot has inconsistent envelope data.");
                UpdatePose();
            }
            internal void UpdatePose()
            {
                // One native matrix and inverse-transpose per display-list slot,
                // retaining every influence; never invert per triangle vertex.
                foreach(var group in matrixGroups)
                {
                    var matrix=Owner.nativePose.VertexMatrix(Mesh,group[0].Native);NMatrix normal=new NMatrix();
                    if(NMatrix.Invert(matrix,out var inverse))normal=NMatrix.Transpose(inverse);
                    foreach(var v in group)v.Update(matrix,normal);
                }
            }
            public override int CompareTo(DrawCallBase other)
            {if(!(other is NativeDraw d))return 0;bool a=Mesh.Material?.RenderFlags.HasFlag(RENDER_MODE.XLU)??false,b=d.Mesh.Material?.RenderFlags.HasFlag(RENDER_MODE.XLU)??false;return a==b?Index.CompareTo(d.Index):a?1:-1;}
            public override void Render(ModelPanelViewport view){if(Owner.IsRendering&&Visible)Owner.Draw(this,view);}
        }
        private int Location(string name){if(!locations.TryGetValue(name,out int value)){value=GL.GetUniformLocation(program,name);locations.Add(name,value);}return value;}
        private void I(string name,int value){int at=Location(name);if(at>=0)GL.Uniform1(at,value);}
        private void F(string name,float value){int at=Location(name);if(at>=0)GL.Uniform1(at,value);}
        private void C(string name,Color c){int at=Location(name);if(at>=0)GL.Uniform4(at,c.R/255f,c.G/255f,c.B/255f,c.A/255f);}
        private void ColorAlpha(string name,Color c,float alpha){int at=Location(name);if(at>=0)GL.Uniform4(at,c.R/255f,c.G/255f,c.B/255f,alpha);}
        private void M(string name,NMatrix m)
        {int at=Location(name);if(at>=0)GL.UniformMatrix4(at,1,false,new[]{m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44});}
        private static string Fragment(string name)
        {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Fighterport.HSD."+name))using(var reader=new StreamReader(stream))
            {
                string s=reader.ReadToEnd().Replace("#version 330","#version 330 compatibility").Replace("#define MAX_TEX 4","#define MAX_TEX 8").Replace("texcoord[4]","texcoord[8]");
                if(name=="gx_lightmap.frag")
                {s=s.Replace("uniform sampler2D sampler3;","uniform sampler2D sampler3;\nuniform sampler2D sampler4;\nuniform sampler2D sampler5;\nuniform sampler2D sampler6;\nuniform sampler2D sampler7;");s=s.Replace("case 3: return texture(sampler3, uv);","case 3: return texture(sampler3, uv);\ncase 4: return texture(sampler4, uv);\ncase 5: return texture(sampler5, uv);\ncase 6: return texture(sampler6, uv);\ncase 7: return texture(sampler7, uv);");}
                if(name=="gx_uv.frag")
                {
                    s=s.Replace("case GX_TG_TEX4: tex0 = texcoord[3]; break;","case GX_TG_TEX4: tex0=texcoord[4];break;").Replace("case GX_TG_TEX5: tex0 = texcoord[3]; break;","case GX_TG_TEX5: tex0=texcoord[5];break;").Replace("case GX_TG_TEX6: tex0 = texcoord[3]; break;","case GX_TG_TEX6: tex0=texcoord[6];break;").Replace("case GX_TG_TEX7: tex0 = texcoord[3]; break;","case GX_TG_TEX7: tex0=texcoord[7];break;");
                }
                return s;
            }
        }
        internal void Prepare()=>Initialize();
        private void Initialize()
        {
            if(program!=0)return;var shaders=new List<int>();int candidate=GL.CreateProgram();
            try
            {
                string vertex=@"#version 330 compatibility
out vec3 posVA;out vec3 vertPosition;out vec3 normal;out vec3 ntan;out vec3 bitan;
out vec3 ambientLight;out vec3 diffuseLight;out vec3 specularLight;
out vec2 texcoord[8];out vec4 vertexColor;flat out int vbones[6];out float vweights[6];out float fogAmt;
void main(){posVA=gl_Vertex.xyz;vertPosition=gl_Vertex.xyz;normal=gl_Normal;ntan=vec3(1,0,0);bitan=vec3(0,1,0);ambientLight=vec3(0.35);diffuseLight=vec3(0.8);specularLight=vec3(0);vertexColor=gl_Color;
texcoord[0]=gl_MultiTexCoord0.xy;texcoord[1]=gl_MultiTexCoord1.xy;texcoord[2]=gl_MultiTexCoord2.xy;texcoord[3]=gl_MultiTexCoord3.xy;texcoord[4]=gl_MultiTexCoord4.xy;texcoord[5]=gl_MultiTexCoord5.xy;texcoord[6]=gl_MultiTexCoord6.xy;texcoord[7]=gl_MultiTexCoord7.xy;
for(int i=0;i<6;i++){vbones[i]=-1;vweights[i]=0;}fogAmt=0;gl_Position=gl_ModelViewProjectionMatrix*gl_Vertex;}";
                shaders.Add(Compile(ShaderType.VertexShader,vertex));
                foreach(string file in new[]{"gx.frag","gx_material.frag","gx_lightmap.frag","gx_uv.frag","gx_alpha_test.frag"})shaders.Add(Compile(ShaderType.FragmentShader,Fragment(file)));
                foreach(int shader in shaders)GL.AttachShader(candidate,shader);GL.LinkProgram(candidate);GL.GetProgram(candidate,ProgramParameter.LinkStatus,out int linked);
                if(linked==0)throw new NotSupportedException("Native HSD shader link failed: "+GL.GetProgramInfoLog(candidate));program=candidate;candidate=0;
            }
            finally{foreach(int shader in shaders)GL.DeleteShader(shader);if(candidate!=0)GL.DeleteProgram(candidate);}
        }
        private static int Compile(ShaderType type,string source)
        {int shader=GL.CreateShader(type);GL.ShaderSource(shader,source);GL.CompileShader(shader);GL.GetShader(shader,ShaderParameter.CompileStatus,out int compiled);if(compiled==0){string log=GL.GetShaderInfoLog(shader);GL.DeleteShader(shader);throw new NotSupportedException("Native HSD shader compile failed: "+log);}return shader;}
        private void Draw(NativeDraw draw,ModelPanelViewport view)
        {
            Initialize();GL.GetInteger(GetPName.CurrentProgram,out int previous);GL.PushAttrib(AttribMask.AllAttribBits);
            try
            {
                GL.UseProgram(program);I("renderOverride",0);I("selectedBone",-1);I("colorOverride",0);F("saturate",1);I("perPixelLighting",1);I("fog.type",0);I("alphaOp",-1);I("alphaComp0",7);I("alphaComp1",7);I("BumpTexture",-1);int overlay=Location("overlayColor");if(overlay>=0)GL.Uniform3(overlay,1f,1f,1f);
                var camera=view.Camera.GetPoint();int cameraAt=Location("cameraPos");if(cameraAt>=0)GL.Uniform3(cameraAt,camera._x,camera._y,camera._z);Matrix sphere=view.Camera._matrixInverse;int sphereAt=Location("sphereMatrix");if(sphereAt>=0)GL.UniformMatrix4(sphereAt,1,false,Enumerable.Range(0,16).Select(i=>sphere[i]).ToArray());
                // Neutral inspection lighting, independent of character/material names.
                // Ambient + key + diffuse fill avoid a black unlit side while retaining
                // the native material/texture values and a single restrained specular key.
                for(int i=0;i<4;i++)
                {
                    I("light["+i+"].enabled",i<3?1:0);I("light["+i+"].type",i==0?0:1);I("light["+i+"].flags",i==1?3:1);
                    float intensity=i==0?.70f:i==1?.60f:.50f;int color=Location("light["+i+"].color");if(color>=0)GL.Uniform3(color,intensity,intensity,intensity);
                    int direction=Location("light["+i+"].position");if(direction>=0)GL.Uniform3(direction,i==2?-30f:20f,i==2?12f:30f,i==2?-20f:40f);
                }
                var m=draw.Mesh.Material;var flags=m?.RenderFlags??RENDER_MODE.CONSTANT;var material=m?.Material;
                C("ambientColor",material?.AmbientColor??Color.White);C("diffuseColor",material?.DiffuseColor??Color.White);C("specularColor",material?.SpecularColor??Color.Black);F("shinniness",material?.Shininess??0);F("alpha",material?.Alpha??1);
                I("enableDiffuse",(draw.Mesh.ParentFlags&JOBJ_FLAG.LIGHTING)!=0&&(flags&RENDER_MODE.DIFFUSE)!=0?1:0);I("enableSpecular",(draw.Mesh.ParentFlags&JOBJ_FLAG.SPECULAR)!=0&&(flags&RENDER_MODE.SPECULAR)!=0?1:0);I("useConstant",(flags&RENDER_MODE.CONSTANT)!=0?1:0);I("useVertexColor",(flags&RENDER_MODE.VERTEX)!=0?1:0);I("useToonShading",(flags&RENDER_MODE.TOON)!=0?1:0);
                GL.Enable(EnableCap.DepthTest);GL.DepthFunc(DepthFunction.Lequal);GL.DepthMask((flags&RENDER_MODE.NO_ZUPDATE)==0);GL.Enable(EnableCap.Blend);GL.BlendFunc(BlendingFactorSrc.SrcAlpha,BlendingFactorDest.OneMinusSrcAlpha);if((draw.Mesh.PolygonFlags&(POBJ_FLAG.CULLBACK|POBJ_FLAG.CULLFRONT))==0)GL.Disable(EnableCap.CullFace);else{GL.Enable(EnableCap.CullFace);GL.CullFace((draw.Mesh.PolygonFlags&(POBJ_FLAG.CULLBACK|POBJ_FLAG.CULLFRONT))==(POBJ_FLAG.CULLBACK|POBJ_FLAG.CULLFRONT)?CullFaceMode.FrontAndBack:(draw.Mesh.PolygonFlags&POBJ_FLAG.CULLBACK)!=0?CullFaceMode.Back:CullFaceMode.Front);}
                GL.Disable(EnableCap.ColorLogicOp);GL.BlendEquation(BlendEquationMode.FuncAdd);
                if(m?.PEDesc is HSD_PEDesc pp)
                {GL.DepthFunc((DepthFunction)(0x200+(int)pp.DepthFunction));I("alphaOp",(int)pp.AlphaOp);I("alphaComp0",(int)pp.AlphaComp0);I("alphaComp1",(int)pp.AlphaComp1);F("alphaRef0",pp.AlphaRef0/255f);F("alphaRef1",pp.AlphaRef1/255f);GL.BlendFunc((BlendingFactorSrc)Blend(pp.SrcFactor),(BlendingFactorDest)Blend(pp.DstFactor));
                    if(pp.BlendMode==GXBlendMode.GX_NONE)GL.Disable(EnableCap.Blend);
                    else if(pp.BlendMode==GXBlendMode.GX_SUBTRACT)GL.BlendEquation(BlendEquationMode.FuncReverseSubtract);
                    else if(pp.BlendMode==GXBlendMode.GX_LOGIC){GL.Disable(EnableCap.Blend);GL.Enable(EnableCap.ColorLogicOp);GL.LogicOp((LogicOp)(0x1500+(int)pp.BlendOp));}}
                for(int i=0;i<8;i++){I("hasTEX["+i+"]",0);I("hasColorTev["+i+"]",0);I("hasAlphaTev["+i+"]",0);}
                int ti=0;for(var t=m?.Textures;t!=null;t=t.Next,ti++)
                {
                    if(t.ImageData==null)continue;var texture=textures[t._s];GL.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0+ti));
                    if(texture.Id==0){texture.Id=GL.GenTexture();GL.BindTexture(TextureTarget.Texture2D,texture.Id);GL.TexImage2D(TextureTarget.Texture2D,0,PixelInternalFormat.Rgba,t.ImageData.Width,t.ImageData.Height,0,OpenTK.Graphics.OpenGL.PixelFormat.Bgra,PixelType.UnsignedByte,texture.Pixels);GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter,(int)TextureMinFilter.Linear);GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter,(int)TextureMagFilter.Linear);}
                    GL.BindTexture(TextureTarget.Texture2D,texture.Id);GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapS,(int)Wrap(t.WrapS));GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapT,(int)Wrap(t.WrapT));I("sampler"+ti,ti);I("hasTEX["+ti+"]",(flags&RENDER_MODE.DF_ALL)!=0||((uint)flags&(1u<<(ti+4)))!=0?1:0);
                    string unit="TEX["+ti+"]";I(unit+".gensrc",(int)t.GXTexGenSrc);I(unit+".is_ambient",t.AmbientLightmap?1:0);I(unit+".is_diffuse",t.DiffuseLightmap?1:0);I(unit+".is_specular",t.SpecularLightmap?1:0);I(unit+".is_ext",t.ExtLightmap?1:0);I(unit+".is_bump",t.BumpMap?1:0);I(unit+".color_operation",t.BumpMap?4:(int)t.ColorOperation);I(unit+".alpha_operation",(int)t.AlphaOperation);I(unit+".coord_type",(int)t.CoordType);F(unit+".blend",t.Blending);M(unit+".transform",TextureMatrix(t));
                    var tev=t.TEV;bool ct=tev!=null&&tev.active.HasFlag(TOBJ_TEVREG_ACTIVE.COLOR_TEV),at=tev!=null&&tev.active.HasFlag(TOBJ_TEVREG_ACTIVE.ALPHA_TEV);I("hasColorTev["+ti+"]",ct?1:0);I("hasAlphaTev["+ti+"]",at?1:0);
                    if(tev!=null)
                    {
                        string key="Tev["+ti+"]";I(key+".color_op",(int)tev.color_op);I(key+".color_bias",(int)tev.color_bias);I(key+".color_scale",(int)tev.color_scale);I(key+".color_clamp",tev.color_clamp?1:0);I(key+".color_a",(int)tev.color_a_in);I(key+".color_b",(int)tev.color_b_in);I(key+".color_c",(int)tev.color_c_in);I(key+".color_d",(int)tev.color_d_in);I(key+".alpha_op",(int)tev.alpha_op);I(key+".alpha_bias",(int)tev.alpha_bias);I(key+".alpha_scale",(int)tev.alpha_scale);I(key+".alpha_clamp",tev.alpha_clamp?1:0);I(key+".alpha_a",(int)tev.alpha_a_in);I(key+".alpha_b",(int)tev.alpha_b_in);I(key+".alpha_c",(int)tev.alpha_c_in);I(key+".alpha_d",(int)tev.alpha_d_in);ColorAlpha(key+".konst",tev.constant,tev.constantAlpha/255f);ColorAlpha(key+".tev0",tev.tev0,tev.tev0Alpha/255f);ColorAlpha(key+".tev1",tev.tev1,tev.tev1Alpha/255f);
                    }
                }
                GL.Begin(BeginMode.Triangles);
                foreach(var v in draw.Vertices)
                {
                    var color=v.Native.Color;GL.Color4(color.X,color.Y,color.Z,color.W);GL.Normal3(v.Normal._x,v.Normal._y,v.Normal._z);
                    for(int i=0;i<8;i++){var uv=v.Native.Uvs[i];GL.MultiTexCoord2((TextureUnit)((int)TextureUnit.Texture0+i),uv.X,uv.Y);}
                    GL.Vertex3(v.Position._x,v.Position._y,v.Position._z);
                }
                GL.End();RenderCount++;
            }
            finally{GL.UseProgram(previous);GL.PopAttrib();}
        }
        private static TextureWrapMode Wrap(GXWrapMode wrap)=>wrap==GXWrapMode.CLAMP?TextureWrapMode.ClampToEdge:wrap==GXWrapMode.MIRROR?TextureWrapMode.MirroredRepeat:TextureWrapMode.Repeat;
        private static int Blend(GXBlendFactor b)
        {switch((int)b){case 0:return 0;case 1:return 1;case 2:return 0x300;case 3:return 0x301;case 4:return 0x302;case 5:return 0x303;case 6:return 0x304;case 7:return 0x305;default:throw new NotSupportedException("Unknown native GX blend factor.");}}
        private static NMatrix TextureMatrix(HSD_TOBJ t)
        {
            float mirror=t.WrapT==GXWrapMode.MIRROR&&t.RepeatT!=0?t.SY/t.RepeatT:0;
            return NMatrix.CreateTranslation(-t.TX,-(t.TY+mirror),t.TZ)*NMatrix.CreateRotationX(t.RX)*NMatrix.CreateRotationY(t.RY)*NMatrix.CreateRotationZ(-t.RZ)*NMatrix.CreateScale(t.SX==0?0:t.RepeatS/t.SX,t.SY==0?0:t.RepeatT/t.SY,t.SZ);
        }
        public void Dispose()
        {
            if(TKContext.CurrentContext!=null){foreach(var t in textures.Values)if(t.Id!=0){GL.DeleteTexture(t.Id);t.Id=0;}if(program!=0)GL.DeleteProgram(program);}
            program=0;locations.Clear();
        }
    }
}
