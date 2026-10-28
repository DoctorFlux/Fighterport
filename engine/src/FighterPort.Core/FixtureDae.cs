using System.Globalization;
using System.Numerics;
using System.Xml.Linq;

namespace FighterPort.Core;

// Minimal original-fixture interchange only. Not a general production DAE exporter.
public static class FixtureDae
{
    public static void Write(Scene scene,string path,bool attributes=false)
    {
        if(scene.Meshes.SelectMany(m=>m.Vertices).Any(v=>Enumerable.Range(0,v.InfluenceCount).Count(i=>v.WeightAt(i)!=0)!=1 ||
            Enumerable.Range(0,v.InfluenceCount).Where(i=>v.WeightAt(i)!=0).Any(i=>v.WeightAt(i)!=1)))
            throw new NotSupportedException("This procedural rigid-fixture writer cannot export weighted geometry. Native model writing preserves all weights.");
        XNamespace ns="http://www.collada.org/2005/11/COLLADASchema";
        XElement E(string name,params object[] content)=>new(ns+name,content);
        XAttribute A(string name,object value)=>new(name,value);
        string Numbers(IEnumerable<float> values)=>string.Join(" ",values.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)));
        var pose=AnimationService.Evaluate(scene,null,0);var geometries=new List<XElement>();
        var nodes=scene.Bones.Select((b,i)=>E("node",A("id",b.Name),A("sid",b.Name),A("name",b.Name),A("type","JOINT"),E("translate",Numbers([b.Rest.Translation.X,b.Rest.Translation.Y,b.Rest.Translation.Z])))).ToArray();
        for(int i=0;i<scene.Bones.Length;i++)
        {
            var all=scene.Meshes.SelectMany(m=>m.Indices.Chunk(3).Where(tri=>tri.All(idx=>Enumerable.Range(0,m.Vertices[idx].InfluenceCount).Any(slot=>m.Vertices[idx].WeightAt(slot)!=0&&m.Vertices[idx].JointAt(slot)==i))).SelectMany(tri=>tri.Select(idx=>m.Vertices[idx]))).ToArray();
            if(all.Length==0)continue;
            Matrix4x4.Invert(pose.World[i],out var inv);var positions=all.Select(v=>Vector3.Transform(v.Position,inv)).ToArray();string id="geo"+i;
            var source=E("source",A("id",id+"pos"),E("float_array",A("id",id+"array"),A("count",all.Length*3),Numbers(positions.SelectMany(p=>new[]{p.X,p.Y,p.Z}))),E("technique_common",E("accessor",A("source","#"+id+"array"),A("count",all.Length),A("stride",3),E("param",A("name","X"),A("type","float")),E("param",A("name","Y"),A("type","float")),E("param",A("name","Z"),A("type","float")))));
            var mesh=E("mesh",source,E("vertices",A("id",id+"v"),E("input",A("semantic","POSITION"),A("source","#"+id+"pos"))));
            var triangles=E("triangles",A("count",all.Length/3),E("input",A("semantic","VERTEX"),A("source","#"+id+"v"),A("offset",0)));
            if(attributes)for(int stream=0;stream<3;stream++)
            {
                string sid=id+"attribute"+stream;int components=stream==0?3:4;var data=all.SelectMany(v=>{if(stream==0){var n=Vector3.TransformNormal(v.Normal,inv);return new[]{n.X,n.Y,n.Z};}var c=stream==1?v.Color:v.Color1;return new[]{c.X,c.Y,c.Z,c.W};}).ToArray();
                mesh.Add(E("source",A("id",sid),E("float_array",A("id",sid+"array"),A("count",data.Length),Numbers(data)),E("technique_common",E("accessor",A("source","#"+sid+"array"),A("count",all.Length),A("stride",components),Enumerable.Range(0,components).Select(ch=>E("param",A("name",(stream==0?new[]{"X","Y","Z"}:new[]{"R","G","B","A"})[ch]),A("type","float")))))));
                var input=E("input",A("semantic",stream==0?"NORMAL":"COLOR"),A("source","#"+sid),A("offset",stream+1));if(stream>0)input.Add(A("set",stream-1));triangles.Add(input);
            }
            triangles.Add(E("p",string.Join(" ",Enumerable.Range(0,all.Length).SelectMany(v=>Enumerable.Repeat(v,attributes?4:1)))));mesh.Add(triangles);geometries.Add(E("geometry",A("id",id),A("name",id),mesh));
            nodes[i].Add(E("node",A("id",id+"instance"),A("name",id+"instance"),E("instance_geometry",A("url","#"+id))));
        }
        var visual=E("visual_scene",A("id","Scene"));for(int i=0;i<nodes.Length;i++){int p=scene.Bones[i].Parent;if(p<0)visual.Add(nodes[i]);else nodes[p].Add(nodes[i]);}
        new XDocument(E("COLLADA",A("version","1.4.1"),E("asset",E("up_axis","Y_UP"),E("unit",A("meter",1),A("name","meter"))),E("library_geometries",geometries.ToArray()),E("library_visual_scenes",visual),E("scene",E("instance_visual_scene",A("url","#Scene"))))).Save(path);
    }
}
