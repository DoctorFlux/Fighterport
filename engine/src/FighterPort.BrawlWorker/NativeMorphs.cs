using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrawlLib.Internal;
using BrawlLib.Imaging;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;

internal sealed class MorphIssue
{
    public string Code {get;set;} public string Message {get;set;} public bool Error {get;set;}=true;
}
internal sealed class MorphData
{
    public List<object> Bindings=new List<object>();public List<MorphIssue> Issues=new List<MorphIssue>();
}
internal static unsafe partial class Program
{
    static object Four(Vector3 value)=>V4(value._x,value._y,value._z,0);
    static object Pixel(RGBAPixel color)=>V4(color.R/255f,color.G/255f,color.B/255f,color.A/255f);
    static object MatrixData(Matrix matrix)
    {
        var f=RawMatrix(matrix);return new{M11=f[0],M12=f[1],M13=f[2],M14=f[3],M21=f[4],M22=f[5],M23=f[6],M24=f[7],M31=f[8],M32=f[9],M33=f[10],M34=f[11],M41=f[12],M42=f[13],M43=f[14],M44=f[15]};
    }
    static object PositionTopology(MDL0ObjectNode obj,ResourceNode basis,int attribute,string path,string hash)
    {
        var indices=new int[obj._manager._pointCount];var transforms=new int[indices.Length];var matrices=new List<object>();var lookup=new Dictionary<string,int>();var points=(ushort*)obj._manager._indices.Address;
        for(int p=0;p<indices.Length;p++)
        {
            var vertex=obj.Vertices[points[p]];var face=vertex.Facepoints[vertex.FaceDataIndices.IndexOf(p)];indices[p]=attribute==0?vertex.Facepoints[0]._vertexIndex:attribute==1?face._normalIndex:face._colorIndices[attribute-2];var matrix=attribute==0?vertex.GetMatrix():attribute==1?vertex.GetMatrix().GetRotationMatrix():Matrix.Identity;string key=string.Join(",",RawMatrix(matrix).Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            if(!lookup.TryGetValue(key,out int transform)){transform=matrices.Count;lookup.Add(key,transform);matrices.Add(MatrixData(matrix));}transforms[p]=transform;
        }
        return new{Basis=Resource(basis,path,hash),BaseValues=ArrayValues(basis),Indices=indices,TransformIndices=transforms,BindMatrices=matrices.ToArray()};
    }
    static object[] ArrayValues(ResourceNode node)=>node is MDL0VertexNode v?v.Vertices.Select(Four).ToArray():node is MDL0NormalNode n?n.Normals.Select(Four).ToArray():((MDL0ColorNode)node).Colors.Select(Pixel).ToArray();
    static MorphData BuildMorphs(MDL0Node model,SHP0Node shape,string modelPath,string modelHash,string shapePath,string shapeHash)
    {
        var result=new MorphData();var matched=new HashSet<SHP0EntryNode>();long budget=0;
        var meshes=model.PolygonList.Cast<MDL0ObjectNode>().ToArray();
        for(int mesh=0;mesh<meshes.Length;mesh++)
        {
            var obj=meshes[mesh];var manager=obj._manager;if(manager==null)continue;
            for(int attribute=0;attribute<4;attribute++)
            {
                ResourceNode basis=attribute==0?(ResourceNode)obj._vertexNode:attribute==1?obj._normalNode:obj._colorSet[attribute-2];if(basis==null)continue;
                var entries=shape.Children.OfType<SHP0EntryNode>().Where(e=>string.Equals(e.Name,basis.Name,StringComparison.OrdinalIgnoreCase)).ToArray();
                if(entries.Length==0)continue;foreach(var e in entries)matched.Add(e);
                if(entries.Length>1){result.Issues.Add(new MorphIssue{Code="MORPH_AMBIGUOUS_ENTRY",Message="Ambiguous SHP0 base array '"+basis.Name+"' on mesh '"+obj.Name+"': "+string.Join(", ",entries.Select(Identity))});continue;}
                var entry=entries[0];if(!entry.Enabled||!(attribute==0?entry.UpdateVertices:attribute==1?entry.UpdateNormals:entry.UpdateColors))continue;
                try
                {
                    if(entry.Children.Count>32)throw new NotSupportedException("More than 32 targets in one entry.");
                    var targets=new List<object>();var map=(ushort*)manager._indices.Address;
                    foreach(var set in entry.Children.OfType<SHP0VertexSetNode>())
                    {
                        var arrays=(attribute==0?model.VertexList:attribute==1?model.NormalList:model.ColorList).Where(n=>n.Name==set.Name).ToArray();
                        if(arrays.Length==0&&!MorphHasWeight(set))
                        {
                            string message="Unused zero-weight target '"+set.Name+"' ["+Identity(set)+"] has no model array; its original channel is retained.";
                            if(!result.Issues.Any(d=>d.Code=="MORPH_INACTIVE_TARGET"&&d.Message==message))result.Issues.Add(new MorphIssue{Code="MORPH_INACTIVE_TARGET",Message=message,Error=false});continue;
                        }
                        if(arrays.Length!=1)throw new InvalidDataException("Target '"+set.Name+"' resolves to "+arrays.Length+" model arrays.");
                        var target=arrays[0];var values=new object[manager._pointCount];
                        if(budget+values.Length>2_000_000)throw new NotSupportedException("Prepared SHP0 budget exceeds 2,000,000 facepoint values.");
                        for(int point=0;point<values.Length;point++)
                        {
                            var vertex=obj.Vertices[map[point]];int face=vertex.FaceDataIndices.IndexOf(point);
                            if(face<0||face>=vertex.Facepoints.Count)throw new InvalidDataException("No native facepoint mapping at point "+point+".");
                            var fp=vertex.Facepoints[face];int index=attribute==0?vertex.Facepoints[0]._vertexIndex:attribute==1?fp._normalIndex:fp._colorIndices[attribute-2];
                            if(attribute==0){var data=((MDL0VertexNode)target).Vertices;if(index<0||index>=data.Length)throw new InvalidDataException("Target vertex array index out of range: "+index);values[point]=Four(vertex.GetMatrix()*data[index]);}
                            else if(attribute==1){var data=((MDL0NormalNode)target).Normals;if(index<0||index>=data.Length)throw new InvalidDataException("Target normal array index out of range: "+index);values[point]=Four(vertex.GetMatrix().GetRotationMatrix()*data[index]);}
                            else{var data=((MDL0ColorNode)target).Colors;if(index<0||index>=data.Length)throw new InvalidDataException("Target color array index out of range: "+index);values[point]=Pixel(data[index]);}
                        }
                        var curve=Curve(set.Keyframes)??new{Keys=new[]{new{Frame=0,Value=0f,InTangent=0f,OutTangent=0f}},Interpolation="Hermite"};
                        targets.Add(new{Name=set.Name,Weight=curve,Values=values,Native=Resource(target,modelPath,modelHash),LocalValues=ArrayValues(target).Length==ArrayValues(basis).Length?ArrayValues(target):null});budget+=values.Length;
                    }
                    result.Bindings.Add(new{Entry=entry.Name+" ["+Identity(entry)+"]",Native=Resource(entry,shapePath,shapeHash),Mesh=mesh,Attribute=new[]{"Position","Normal","Color0","Color1"}[attribute],ByteColors=true,NormalPolicy=attribute==1?"NativeWeightedRoundTrip":"NormalizeBeforeSkin",Targets=targets.ToArray(),Topology=PositionTopology(obj,basis,attribute,modelPath,modelHash)});
                }
                catch(Exception e){result.Issues.Add(new MorphIssue{Code="MORPH_BINDING",Message=shape.Name+" / "+entry.Name+" ["+Identity(entry)+"] / "+obj.Name+": "+e.Message});}
            }
        }
        foreach(var entry in shape.Children.OfType<SHP0EntryNode>().Where(e=>e.Enabled&&!matched.Contains(e)))result.Issues.Add(new MorphIssue{Code="MORPH_BASE_MISSING",Message="SHP0 base array '"+entry.Name+"' ["+Identity(entry)+"] is absent from selected model '"+model.Name+"'."});
        return result;
    }
    static bool MorphHasWeight(SHP0VertexSetNode set)
    {
        var array=set.Keyframes;var type=typeof(BrawlLib.Wii.Animations.KeyframeArray);
        int count=(int)type.GetField("_keyCount",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(array);
        var root=(BrawlLib.Wii.Animations.KeyframeEntry)type.GetField("_keyRoot",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(array);var key=root._next;
        for(int i=0;i<count;i++,key=key._next)if(key._value!=0||key._tangent!=0)return true;return false;
    }
}
