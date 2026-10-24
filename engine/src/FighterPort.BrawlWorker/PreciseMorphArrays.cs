using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BrawlLib.Internal;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.SSBB.Types;

// Byte reversal through a float can quiet a signaling NaN in the *reversed* representation
// on x86, changing otherwise ordinary finite coordinates. Move endian bits as integers.
internal static unsafe class MorphFloatBytes
{
    public static float Read(byte* p){uint bits=(uint)p[0]<<24|(uint)p[1]<<16|(uint)p[2]<<8|p[3];return *(float*)&bits;}
    public static void Write(byte* p,float value){uint bits=*(uint*)&value;p[0]=(byte)(bits>>24);p[1]=(byte)(bits>>16);p[2]=(byte)(bits>>8);p[3]=(byte)bits;}
    public static void WriteVectors(byte* output,Vector3[] values)
    {for(int i=0;i<values.Length;i++){Write(output+i*12,values[i]._x);Write(output+i*12+4,values[i]._y);Write(output+i*12+8,values[i]._z);}}
    static Vector3[] ReadVectors(byte* input,int count,int stride,bool xyz)
    {var values=new Vector3[count];for(int i=0;i<count;i++){byte* p=input+i*stride;values[i]=new Vector3(Read(p),Read(p+4),xyz?Read(p+8):0);}return values;}
    public static void Cache(MDL0Node model)
    {
        foreach(var node in model.VertexList?.OfType<MDL0VertexNode>()??Enumerable.Empty<MDL0VertexNode>())
        {
            var h=(MDL0VertexData*)node.WorkingUncompressed.Address;if((int)h->_type!=4)continue;
            Check(node,h->_dataOffset,h->_numVertices,h->_entryStride,h->_isXYZ!=0?12:8);
            node._vertices=ReadVectors((byte*)h+h->_dataOffset,h->_numVertices,h->_entryStride,h->_isXYZ!=0);
        }
        foreach(var node in model.NormalList?.OfType<MDL0NormalNode>()??Enumerable.Empty<MDL0NormalNode>())
        {
            var h=(MDL0NormalData*)node.WorkingUncompressed.Address;if((int)h->_type!=4)continue;
            if(h->_isNBT!=0)continue;
            Check(node,h->_dataOffset,h->_numVertices,h->_entryStride,12);
            var field=typeof(MDL0NormalNode).GetField("_normals",BindingFlags.NonPublic|BindingFlags.Instance)??throw new NotSupportedException("Pinned native normal cache is unavailable.");
            field.SetValue(node,ReadVectors((byte*)h+h->_dataOffset,h->_numVertices,h->_entryStride,true));
        }
        foreach(var node in model.ColorList?.OfType<MDL0ColorNode>()??Enumerable.Empty<MDL0ColorNode>())
        {var h=(MDL0ColorData*)node.WorkingUncompressed.Address;int format=h->_format;if(format<0||format>5)throw new NotSupportedException("Unsupported native color array: "+node.Name);int stride=new[]{2,3,4,2,3,4}[format];Check(node,h->_dataOffset,h->_numEntries,stride,stride);}
    }
    static void Check(ResourceNode node,int offset,int count,int stride,int components)
    {if(offset<0||stride<components||(long)offset+(long)count*stride>node.WorkingUncompressed.Length)throw new InvalidDataException("Native float array exceeds its resource bounds: "+node.Name);}
}
internal sealed unsafe class PreciseMorphPositions : MDL0VertexNode
{
    public override void OnRebuild(VoidPtr address,int length,bool force)
    {base.OnRebuild(address,length,force);var h=(MDL0VertexData*)address;if((int)h->_type!=4||h->_entryStride!=12)throw new InvalidOperationException("Expected float XYZ native morph storage.");MorphFloatBytes.WriteVectors((byte*)h+h->_dataOffset,Vertices);}
}
internal sealed unsafe class PreciseMorphNormals : MDL0NormalNode
{
    public override void OnRebuild(VoidPtr address,int length,bool force)
    {base.OnRebuild(address,length,force);var h=(MDL0NormalData*)address;if((int)h->_type!=4||h->_entryStride!=12)throw new InvalidOperationException("Expected float XYZ native normal storage.");MorphFloatBytes.WriteVectors((byte*)h+h->_dataOffset,Normals);}
}
// Morph indices address the original array, so duplicate colors must retain their slots.
// The general color codec deduplicates data but can retain the original header count.
internal sealed unsafe class PreciseMorphColors : MDL0ColorNode
{
    public override int OnCalculateSize(bool force)=>0x20+((Colors.Length*4+31)&~31);
    public override void OnRebuild(VoidPtr address,int length,bool force)
    {
        var h=(MDL0ColorData*)address;*h=new MDL0ColorData();h->_dataLen=length;h->_dataOffset=0x20;h->_index=Index;h->_isRGBA=1;h->_format=5;h->_entryStride=4;h->_numEntries=(ushort)Colors.Length;
        byte* data=(byte*)address+0x20;for(int i=0;i<Colors.Length;i++){var c=Colors[i];data[i*4]=c.R;data[i*4+1]=c.G;data[i*4+2]=c.B;data[i*4+3]=c.A;}
        for(int i=Colors.Length*4;i<length-0x20;i++)data[i]=0;
    }
}
