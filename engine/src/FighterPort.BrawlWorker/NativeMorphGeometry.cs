using System;
using System.IO;
using System.Linq;
using BrawlLib.Internal;
using BrawlLib.Imaging;
using BrawlLib.SSBB.ResourceNodes;

internal sealed class GeometryChange {public string Entry {get;set;} public string Basis {get;set;} public string Name {get;set;} public float[][] Values {get;set;} public string Attribute {get;set;}}
internal sealed class GeometryReceipt {public string Invariant;public PreservedNative[] Preserved;}
internal static unsafe partial class Program
{
    static string ModelInvariant(MDL0Node model,GeometryChange[] changes)
    {
        model.ApplySHP(null,0);model.ApplyCHR(null,0);var bones=OrderedBones(model);
        return Json.Serialize(new{model.Name,model.Version,Bones=bones.Select(b=>new{b.Name,Parent=b.Parent is MDL0BoneNode p?Array.IndexOf(bones,p):-1,Rest=Rest(b),Bind=RawMatrix(b.BindMatrix),Inverse=RawMatrix(b.InverseBindMatrix)}).ToArray(),
            Meshes=model.PolygonList.Cast<MDL0ObjectNode>().Select(o=>Mesh(o,bones)).ToArray(),
            Positions=model.VertexList.Cast<MDL0VertexNode>().Where(v=>!changes.Any(c=>c.Attribute=="Position"&&c.Name==v.Name)).Select(v=>new{v.Name,Values=v.Vertices.Select(V).ToArray()}).ToArray(),
            Normals=model.NormalList.Cast<MDL0NormalNode>().Where(v=>!changes.Any(c=>c.Attribute=="Normal"&&c.Name==v.Name)).Select(n=>new{n.Name,Values=n.Normals.Select(V).ToArray()}).ToArray(),
            Colors=model.ColorList.Cast<MDL0ColorNode>().Where(v=>!changes.Any(c=>c.Attribute=="Color0"&&c.Name==v.Name)).Select(c=>new{c.Name,Values=c.Colors.Select(Pixel).ToArray()}).ToArray(),
            Materials=model.MaterialList.Cast<MDL0MaterialNode>().Select(m=>Material(m,"preservation","unchanged")).ToArray()});
    }
    static GeometryReceipt WriteGeometry(MDL0Node model,Request r)
    {
        var changes=r.GeometryChanges??new GeometryChange[0];if(changes.Length==0)return null;
        if(File.Exists(r.ModelOutput)||Same(r.ModelOutput,r.Model)||Same(r.ModelOutput,r.Motion)||Same(r.ModelOutput,r.Output))throw new IOException("Choose a new model output distinct from all inputs and motion output.");
        ResourceNode root=model;while(root.Parent!=null)root=root.Parent;
        string extension=Path.GetExtension(r.ModelOutput).ToLowerInvariant();if(root is ARCNode?extension!=".pac":root is BRRESNode?extension!=".brres":root is MDL0Node?extension!=".mdl0":true)throw new NotSupportedException("Model output must retain its PAC, BRRES or MDL0 container type.");
        if(changes.Select(c=>c.Name+"|"+c.Attribute).Distinct().Count()!=changes.Length)throw new InvalidDataException("Duplicate geometry array changes.");
        var receipt=new GeometryReceipt{Invariant=ModelInvariant(model,changes),Preserved=ResourceHashes(root,edited:new ResourceNode[]{model})};
        foreach(var change in changes)
        {
            var list=change.Attribute=="Position"?model.VertexList:change.Attribute=="Normal"?model.NormalList:change.Attribute=="Color0"?model.ColorList:throw new NotSupportedException("Unsupported native array attribute.");var basis=list.Single(v=>Identity(v)==change.Basis);
            if(change.Values.Length!=ArrayValues(basis).Length||change.Values.Any(v=>v.Length!=4||v.Any(f=>float.IsNaN(f)||float.IsInfinity(f))))throw new InvalidDataException("Native target must preserve base array count and finite geometry.");
            if(string.IsNullOrWhiteSpace(change.Name)||change.Name.Length>100)throw new InvalidDataException("Invalid native array name.");
            var vertices=change.Values.Select(v=>new Vector3(v[0],v[1],v[2])).ToArray();ResourceNode target;
            if(change.Entry==null)
            {
                if(list.Any(v=>v.Name==change.Name))throw new InvalidDataException("Native model already contains that target name.");
                target=change.Attribute=="Position"?(ResourceNode)new PreciseMorphPositions{Name=change.Name}:change.Attribute=="Normal"?new PreciseMorphNormals{Name=change.Name}:new PreciseMorphColors{Name=change.Name};basis.Parent.AddChild(target);
            }
            else
            {
                target=list.Single(v=>Identity(v)==change.Entry&&v.Name==change.Name);
                if(model.PolygonList.Cast<MDL0ObjectNode>().Any(o=>ReferenceEquals(o._vertexNode,target)||ReferenceEquals(o._normalNode,target)||o._colorSet.Any(c=>ReferenceEquals(c,target))))throw new InvalidDataException("Editing a morph must not change base mesh geometry.");
                if(target is MDL0VertexNode||target is MDL0NormalNode){var parent=target.Parent;int index=target.Index;target.Remove();target=change.Attribute=="Position"?(ResourceNode)new PreciseMorphPositions{Name=change.Name}:new PreciseMorphNormals{Name=change.Name};parent.InsertChild(target,index);}
            }
            if(target is MDL0VertexNode positions){positions.ForceFloat=true;positions.Vertices=vertices;}else if(target is MDL0NormalNode normals){normals.ForceFloat=true;normals.Normals=vertices;}else ((MDL0ColorNode)target).Colors=change.Values.Select(v=>new RGBAPixel((byte)Math.Round(v[0]*255),(byte)Math.Round(v[1]*255),(byte)Math.Round(v[2]*255),(byte)Math.Round(v[3]*255))).ToArray();target.SignalPropertyChange();
        }
        root.Export(r.ModelOutput);return receipt;
    }
    static void VerifyGeometry(MDL0Node model,ResourceNode root,Request r,GeometryReceipt receipt)
    {
        VerifyPreserved(root,receipt.Preserved);
        string invariant=ModelInvariant(model,r.GeometryChanges);if(invariant!=receipt.Invariant){int diff=0;while(diff<invariant.Length&&diff<receipt.Invariant.Length&&invariant[diff]==receipt.Invariant[diff])diff++;int start=Math.Max(0,diff-80);throw new InvalidDataException("Fresh model invariant changed at "+diff+"; expected "+receipt.Invariant.Substring(start,Math.Min(230,receipt.Invariant.Length-start))+"; actual "+invariant.Substring(start,Math.Min(230,invariant.Length-start)));}
        foreach(var change in r.GeometryChanges)
        {
            var list=change.Attribute=="Position"?model.VertexList:change.Attribute=="Normal"?model.NormalList:model.ColorList;var node=list.Single(v=>v.Name==change.Name);
            float[][] array=node is MDL0VertexNode v?v.Vertices.Select(p=>new[]{p._x,p._y,p._z,0f}).ToArray():node is MDL0NormalNode n?n.Normals.Select(p=>new[]{p._x,p._y,p._z,0f}).ToArray():((MDL0ColorNode)node).Colors.Select(p=>new[]{p.R/255f,p.G/255f,p.B/255f,p.A/255f}).ToArray();
            if(array.Length!=change.Values.Length)throw new InvalidDataException("Fresh native target array count differs: "+change.Name+" / "+change.Attribute);
            int mismatch=Array.FindIndex(Enumerable.Range(0,array.Length).ToArray(),i=>!array[i].SequenceEqual(change.Values[i]));if(mismatch>=0)throw new InvalidDataException("Fresh native target array differs: "+change.Name+" / "+change.Attribute+" / point "+mismatch+" expected "+string.Join(",",change.Values[mismatch])+" actual "+string.Join(",",array[mismatch])+" format "+(node is MDL0VertexNode vertex?vertex.Format.ToString():"other")+" mismatch count "+array.Where((p,i)=>!p.SequenceEqual(change.Values[i])).Count()+" nearest source point "+Array.FindIndex(change.Values,p=>p.SequenceEqual(array[mismatch])));
        }
    }
}
