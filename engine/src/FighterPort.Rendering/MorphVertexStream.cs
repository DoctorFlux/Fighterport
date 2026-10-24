using FighterPort.Core;
using Silk.NET.OpenGL;
using Gl=Silk.NET.OpenGL.GL;

namespace FighterPort.Rendering;

// Only prepared deformation attributes are streamed. UVs, topology, skin bindings and sources remain static.
internal sealed unsafe class MorphVertexStream(Gl gl) : IDisposable
{
    uint buffer=gl.GenBuffer();MorphEvaluation? active;bool overridden;long revision=-1;
    public long Uploads {get;private set;}
    public long Bytes {get;private set;}
    public int Bindings=>active?.BindingCount??0;
    void Attributes(int stride,int normal,int color0,int color1)
    {
        gl.VertexAttribPointer(0,3,VertexAttribPointerType.Float,false,(uint)stride,null);gl.VertexAttribPointer(1,3,VertexAttribPointerType.Float,false,(uint)stride,(void*)normal);
        gl.VertexAttribPointer(5,4,VertexAttribPointerType.Float,false,(uint)stride,(void*)color0);gl.VertexAttribPointer(6,4,VertexAttribPointerType.Float,false,(uint)stride,(void*)color1);
    }
    public void Reset(uint original)
    {
        if(overridden){gl.BindBuffer(BufferTargetARB.ArrayBuffer,original);Attributes(152,12,64,80);}
        active=null;overridden=false;
    }
    public void Apply(MorphEvaluation? evaluation,uint original)
    {
        if(evaluation is null){Reset(original);return;}
        gl.BindBuffer(BufferTargetARB.ArrayBuffer,buffer);
        fixed(float* data=evaluation.VertexData)
        {
            if(!ReferenceEquals(active,evaluation))
            {
                nuint bytes=(nuint)(evaluation.VertexData.Length*4);gl.BufferData(BufferTargetARB.ArrayBuffer,bytes,data,BufferUsageARB.DynamicDraw);Uploads++;Bytes+=(long)bytes;active=evaluation;
            }
            else if(revision!=evaluation.Revision)foreach(var range in evaluation.Ranges)
            {
                int offset=range.FirstVertex*MorphEvaluation.Stride;int count=range.VertexCount*MorphEvaluation.Stride;
                gl.BufferSubData(BufferTargetARB.ArrayBuffer,(nint)(offset*4),(nuint)(count*4),data+offset);Uploads++;Bytes+=count*4;
            }
        }
        revision=evaluation.Revision;
        if(!overridden){Attributes(MorphEvaluation.Stride*4,12,24,40);overridden=true;}
    }
    public void Dispose()=>gl.DeleteBuffer(buffer);
}
