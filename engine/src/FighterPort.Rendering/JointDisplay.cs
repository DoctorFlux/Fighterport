using System.Numerics;
using FighterPort.Core;
using Silk.NET.OpenGL;
using Gl=Silk.NET.OpenGL.GL;

namespace FighterPort.Rendering;

// Owned instanced geometry; the behavioral reference and revision are in LICENSE_AUDIT.md.
internal sealed unsafe class JointDisplay : IDisposable
{
    readonly Gl gl;readonly uint program,vao,vbo;readonly int count;
    public JointDisplay(Gl gl,bool es)
    {
        this.gl=gl;string version=es?"#version 300 es\nprecision highp float;precision highp int;\n":"#version 330 core\n";
        uint Shader(ShaderType type,string text){uint s=gl.CreateShader(type);gl.ShaderSource(s,version+text);gl.CompileShader(s);gl.GetShader(s,ShaderParameterName.CompileStatus,out int ok);if(ok==0)throw new InvalidOperationException(gl.GetShaderInfoLog(s));return s;}
        uint vs=Shader(ShaderType.VertexShader,"""
        layout(location=0) in vec3 position;layout(location=1) in vec4 color;
        uniform mat4 viewProjection;uniform mat4 world[128];uniform float radius;uniform int selected;uniform int hovered;uniform int visible[128];
        out vec4 shade;
        void main(){gl_Position=visible[gl_InstanceID]==0?vec4(2.0,2.0,2.0,1.0):viewProjection*world[gl_InstanceID]*vec4(position*radius,1.0);shade=color;if(color.a==0.0){shade=vec4(0.0,128.0/255.0,0.0,1.0);if(gl_InstanceID==selected||gl_InstanceID==hovered)shade=vec4(1.0,128.0/255.0,0.0,1.0);}}
        """);
        uint fs=Shader(ShaderType.FragmentShader,"in vec4 shade;out vec4 result;void main(){result=shade;}");
        program=gl.CreateProgram();gl.AttachShader(program,vs);gl.AttachShader(program,fs);gl.LinkProgram(program);gl.GetProgram(program,ProgramPropertyARB.LinkStatus,out int linked);gl.DeleteShader(vs);gl.DeleteShader(fs);if(linked==0)throw new InvalidOperationException(gl.GetProgramInfoLog(program));
        var vertices=new List<float>();void Vertex(Vector3 p,Vector4 c)=>vertices.AddRange([p.X,p.Y,p.Z,c.X,c.Y,c.Z,c.W]);
        for(int plane=0;plane<3;plane++)for(int segment=0;segment<360;segment++)for(int end=0;end<2;end++)
        {
            float a=(segment+end)*MathF.PI/180;var p=new Vector3(MathF.Cos(a),MathF.Sin(a),0);
            if(plane==1)p=new(p.X,0,p.Y);else if(plane==2)p=new(0,p.X,p.Y);Vertex(p,Vector4.Zero);
        }
        for(int axis=0;axis<3;axis++){var p=Vector3.Zero;p[axis]=2;var c=axis==0?new Vector4(1,0,0,1):axis==1?new Vector4(0,1,0,1):new Vector4(0,0,1,1);Vertex(Vector3.Zero,c);Vertex(p,c);}
        count=vertices.Count/7;var data=vertices.ToArray();vao=gl.GenVertexArray();vbo=gl.GenBuffer();gl.BindVertexArray(vao);gl.BindBuffer(BufferTargetARB.ArrayBuffer,vbo);fixed(float* p=data)gl.BufferData(BufferTargetARB.ArrayBuffer,(nuint)(data.Length*4),p,BufferUsageARB.StaticDraw);
        gl.EnableVertexAttribArray(0);gl.VertexAttribPointer(0,3,VertexAttribPointerType.Float,false,28,null);gl.EnableVertexAttribArray(1);gl.VertexAttribPointer(1,4,VertexAttribPointerType.Float,false,28,(void*)12);
    }
    public void Draw(EvaluatedPose pose,Matrix4x4 vp,float radius,int selected,int hovered,IReadOnlySet<int> hidden)
    {
        gl.UseProgram(program);gl.BindVertexArray(vao);gl.Disable(EnableCap.DepthTest);gl.Disable(EnableCap.Blend);
        gl.UniformMatrix4(gl.GetUniformLocation(program,"viewProjection"),1,false,(float*)&vp);fixed(Matrix4x4* p=pose.World)gl.UniformMatrix4(gl.GetUniformLocation(program,"world[0]"),(uint)pose.World.Length,false,(float*)p);
        gl.Uniform1(gl.GetUniformLocation(program,"radius"),radius);gl.Uniform1(gl.GetUniformLocation(program,"selected"),selected);gl.Uniform1(gl.GetUniformLocation(program,"hovered"),hovered);
        // Preserve stable native bone indices while clipping hidden instances.
        int[] visibility=Enumerable.Range(0,pose.World.Length).Select(i=>hidden.Contains(i)?0:1).ToArray();fixed(int* p=visibility)gl.Uniform1(gl.GetUniformLocation(program,"visible[0]"),(uint)visibility.Length,p);
        gl.DrawArraysInstanced(PrimitiveType.Lines,0,(uint)count,(uint)pose.World.Length);
    }
    public void Dispose(){gl.DeleteBuffer(vbo);gl.DeleteVertexArray(vao);gl.DeleteProgram(program);}
}
