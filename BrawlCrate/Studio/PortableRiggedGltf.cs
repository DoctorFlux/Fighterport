using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace BrawlCrate.Studio
{
    // Consumes only the separately staged, verified native projection. This
    // avoids a second skin/geometry interpretation of Brawl and Melee data.
    internal static class PortableRiggedGltf
    {
        internal sealed class Clip
        {
            internal string Name;
            internal float FramesPerSecond;
            internal Track[] Tracks;
        }
        internal sealed class Track
        {
            internal string Bone;
            internal float[] Frames;
            internal Vector3[] Translation;
            internal Quaternion[] Rotation;
        }
        private static string Id(XElement e)=>((string)e.Attribute("id"))??"";
        private static string Ref(string s)=>s!=null&&s.StartsWith("#",StringComparison.Ordinal)?s.Substring(1):s;
        private static string Attr(XElement e,string name)=>((string)e?.Attribute(name))??"";
        private static int Integer(string s)=>int.Parse(s,CultureInfo.InvariantCulture);
        private static float Real(string s)
        {
            float value=float.Parse(s,CultureInfo.InvariantCulture);
            if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Nonfinite portable model value.");
            return value;
        }
        private static string[] Words(string value)=>value.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
        private static int[] Ints(string value)=>Words(value).Select(Integer).ToArray();
        private static float[] Floats(string value)=>Words(value).Select(Real).ToArray();
        private static Dictionary<string,object> D(params object[] pairs)
        {
            var result=new Dictionary<string,object>(StringComparer.Ordinal);
            for(int i=0;i<pairs.Length;i+=2)result.Add((string)pairs[i],pairs[i+1]);
            return result;
        }
        private static float[] Matrix(Matrix4x4 m)=>new[]{m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,
            m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44};
        private static float[] Transpose(float[] source)
        {
            if(source.Length%16!=0)throw new InvalidDataException("A portable matrix stream is incomplete.");
            var result=new float[source.Length];
            for(int at=0;at<source.Length;at+=16)
                for(int row=0;row<4;row++)for(int column=0;column<4;column++)
                    result[at+row*4+column]=source[at+column*4+row];
            return result;
        }
        private static Matrix4x4 Local(XElement node)
        {
            Matrix4x4 result=Matrix4x4.Identity;
            foreach(var child in node.Elements())
            {
                Matrix4x4 part;
                switch(child.Name.LocalName)
                {
                    case "matrix":
                        var m=Floats(child.Value);if(m.Length!=16)throw new InvalidDataException("Invalid COLLADA joint matrix.");
                        m=Transpose(m);
                        part=new Matrix4x4(m[0],m[1],m[2],m[3],m[4],m[5],m[6],m[7],
                            m[8],m[9],m[10],m[11],m[12],m[13],m[14],m[15]);break;
                    case "translate":
                        var t=Floats(child.Value);if(t.Length!=3)throw new InvalidDataException("Invalid COLLADA translation.");
                        part=Matrix4x4.CreateTranslation(t[0],t[1],t[2]);break;
                    case "scale":
                        var s=Floats(child.Value);if(s.Length!=3)throw new InvalidDataException("Invalid COLLADA scale.");
                        part=Matrix4x4.CreateScale(s[0],s[1],s[2]);break;
                    case "rotate":
                        var r=Floats(child.Value);if(r.Length!=4)throw new InvalidDataException("Invalid COLLADA rotation.");
                        var axis=new Vector3(r[0],r[1],r[2]);
                        if(axis.LengthSquared()<1e-12f)
                        {
                            if(Math.Abs(r[3])>1e-6f)throw new InvalidDataException("A portable joint has a nonzero rotation around a zero axis.");
                            part=Matrix4x4.Identity;
                        }
                        else part=Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(axis),r[3]*(float)Math.PI/180f);
                        break;
                    default:continue;
                }
                // COLLADA column-vector transforms compose in document order.
                // System.Numerics uses row vectors, so prepend each element.
                result=part*result;
            }
            return result;
        }
        private sealed class BinaryPlan
        {
            internal readonly MemoryStream Stream=new MemoryStream();
            internal readonly BinaryWriter Writer;
            internal readonly List<object> Views=new List<object>(),Accessors=new List<object>();
            internal BinaryPlan(){Writer=new BinaryWriter(Stream);}
            internal void Align(){while(Stream.Position%4!=0)Writer.Write((byte)0);}
            internal int View(Action write,int target=0)
            {
                Align();int start=checked((int)Stream.Position);write();int length=checked((int)Stream.Position-start);
                if(length<1)throw new InvalidDataException("Empty portable buffer view.");
                var view=D("buffer",0,"byteOffset",start,"byteLength",length);
                if(target!=0)view.Add("target",target);
                Views.Add(view);return Views.Count-1;
            }
            internal int Floats(float[] values,string type,int count,int target=0,bool bounds=false)
            {
                if(values.Length==0||values.Length!=count*(type=="MAT4"?16:type=="VEC4"?4:type=="VEC3"?3:type=="VEC2"?2:1))
                    throw new InvalidDataException("Invalid portable accessor dimensions.");
                int view=View(()=>{foreach(float f in values){if(float.IsNaN(f)||float.IsInfinity(f))throw new InvalidDataException("Nonfinite portable accessor.");Writer.Write(f);}},target);
                var accessor=D("bufferView",view,"componentType",5126,"count",count,"type",type);
                if(bounds&&type=="VEC3")
                {
                    var min=new[]{float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity};
                    var max=new[]{float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity};
                    for(int i=0;i<count;i++)for(int j=0;j<3;j++){min[j]=Math.Min(min[j],values[i*3+j]);max[j]=Math.Max(max[j],values[i*3+j]);}
                    accessor.Add("min",min);accessor.Add("max",max);
                }
                if(bounds&&type=="SCALAR")
                {
                    accessor.Add("min",new[]{values.Min()});
                    accessor.Add("max",new[]{values.Max()});
                }
                Accessors.Add(accessor);return Accessors.Count-1;
            }
            internal int Joints(ushort[] values,int count)
            {
                if(values.Length!=count*4)throw new InvalidDataException("Invalid joint accessor.");
                int view=View(()=>{foreach(ushort v in values)Writer.Write(v);},34962);
                Accessors.Add(D("bufferView",view,"componentType",5123,"count",count,"type","VEC4"));
                return Accessors.Count-1;
            }
        }
        private static XElement Source(XElement owner,string id)
            =>owner.Elements().SingleOrDefault(e=>e.Name.LocalName=="source"&&Id(e)==id)
              ??throw new InvalidDataException("Missing COLLADA source "+id+".");
        private static float[] Values(XElement source)
            =>Floats(source.Elements().Single(e=>e.Name.LocalName=="float_array").Value);
        private static string[] Names(XElement source)
            =>Words(source.Elements().Single(e=>e.Name.LocalName=="Name_array"||e.Name.LocalName=="IDREF_array").Value);
        private static string Input(XElement parent,string semantic)
            =>Ref(Attr(parent.Elements().Single(e=>e.Name.LocalName=="input"&&Attr(e,"semantic")==semantic),"source"));
        private static float[] Item(float[] values,int index,int stride)
        {
            if(index<0||index*stride+stride>values.Length)throw new InvalidDataException("COLLADA index exceeds source.");
            var result=new float[stride];System.Array.Copy(values,index*stride,result,0,stride);return result;
        }
        private static string ImageFile(XDocument document,string material)
        {
            var materials=document.Descendants().Where(e=>e.Name.LocalName=="material");
            var mat=materials.SingleOrDefault(e=>Id(e)==material);
            if(mat==null)return null;
            string effect=Ref(Attr(mat.Elements().Single(e=>e.Name.LocalName=="instance_effect"),"url"));
            var fx=document.Descendants().SingleOrDefault(e=>e.Name.LocalName=="effect"&&Id(e)==effect);
            var texture=fx?.Descendants().FirstOrDefault(e=>e.Name.LocalName=="diffuse")?
                .Descendants().FirstOrDefault(e=>e.Name.LocalName=="texture");
            if(texture==null)return null;
            string sampler=Attr(texture,"texture");
            var param=fx.Descendants().FirstOrDefault(e=>e.Name.LocalName=="newparam"&&Attr(e,"sid")==sampler);
            string surface=param?.Descendants().FirstOrDefault(e=>e.Name.LocalName=="source")?.Value;
            var surf=fx.Descendants().FirstOrDefault(e=>e.Name.LocalName=="newparam"&&Attr(e,"sid")==surface);
            string image=surf?.Descendants().FirstOrDefault(e=>e.Name.LocalName=="init_from")?
                .DescendantsAndSelf().LastOrDefault(e=>e.Name.LocalName=="ref"||e.Name.LocalName=="init_from")?.Value;
            var img=document.Descendants().FirstOrDefault(e=>e.Name.LocalName=="image"&&Id(e)==image);
            string path=img?.Descendants().FirstOrDefault(e=>e.Name.LocalName=="init_from")?
                .DescendantsAndSelf().LastOrDefault(e=>e.Name.LocalName=="ref"||e.Name.LocalName=="init_from")?.Value;
            return path==null?null:Path.GetFileName(path);
        }
        internal static void Write(string stage,Clip clip=null)
        {
            string dae=Path.Combine(stage,"Model.dae");
            var document=XDocument.Load(dae);
            var scene=document.Descendants().Single(e=>e.Name.LocalName=="visual_scene");
            var nodes=new List<Dictionary<string,object>>();
            var bones=new Dictionary<string,int>(StringComparer.Ordinal);
            var animated=clip==null?new HashSet<string>(StringComparer.Ordinal):
                new HashSet<string>(clip.Tracks.Select(track=>track.Bone),StringComparer.Ordinal);
            int AddBone(XElement element)
            {
                int index=nodes.Count;string name=Id(element);
                if(name.Length==0||bones.ContainsKey(name))throw new InvalidDataException("Duplicate or empty portable bone identity.");
                bones.Add(name,index);
                var matrix=Local(element);
                var node=D("name",name);
                if(animated.Contains(name))
                {
                    if(!Matrix4x4.Decompose(matrix,out var scale,out var rotation,out var translation))
                        throw new InvalidDataException("An animated portable joint cannot be decomposed to TRS.");
                    var rebuilt=Matrix4x4.CreateScale(scale)*Matrix4x4.CreateFromQuaternion(rotation)*
                        Matrix4x4.CreateTranslation(translation);
                    var original=Matrix(matrix);var check=Matrix(rebuilt);
                    if(original.Where((value,i)=>Math.Abs(value-check[i])>0.0001f).Any())
                        throw new InvalidDataException("An animated portable joint has non-TRS bind semantics.");
                    if(translation!=Vector3.Zero)node.Add("translation",new[]{translation.X,translation.Y,translation.Z});
                    if(rotation!=Quaternion.Identity)node.Add("rotation",new[]{rotation.X,rotation.Y,rotation.Z,rotation.W});
                    if(scale!=Vector3.One)node.Add("scale",new[]{scale.X,scale.Y,scale.Z});
                }
                else if(matrix!=Matrix4x4.Identity)node.Add("matrix",Matrix(matrix));
                nodes.Add(node);
                var children=element.Elements().Where(e=>e.Name.LocalName=="node"&&Attr(e,"type")=="JOINT").Select(AddBone).ToArray();
                if(children.Length>0)node.Add("children",children);
                return index;
            }
            var roots=scene.Elements().Where(e=>e.Name.LocalName=="node"&&Attr(e,"type")=="JOINT").Select(AddBone).ToList();
            if(roots.Count==0)throw new InvalidDataException("The rigged portable source has no joint hierarchy.");
            var plan=new BinaryPlan();
            var meshes=new List<object>();var skins=new List<object>();
            var materials=new List<object>();var images=new List<object>();var textures=new List<object>();
            var imageIds=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            foreach(var instance in scene.Descendants().Where(e=>e.Name.LocalName=="instance_controller"))
            {
                string controllerId=Ref(Attr(instance,"url"));
                var controller=document.Descendants().Single(e=>e.Name.LocalName=="controller"&&Id(e)==controllerId);
                var skin=controller.Elements().Single(e=>e.Name.LocalName=="skin");
                string geometryId=Ref(Attr(skin,"source"));
                var geometry=document.Descendants().Single(e=>e.Name.LocalName=="geometry"&&Id(e)==geometryId);
                var mesh=geometry.Elements().Single(e=>e.Name.LocalName=="mesh");
                var jointInput=skin.Elements().Single(e=>e.Name.LocalName=="joints");
                var jointNames=Names(Source(skin,Input(jointInput,"JOINT")));
                if(jointNames.Length==0||jointNames.Length>ushort.MaxValue||jointNames.Any(j=>!bones.ContainsKey(j)))
                    throw new InvalidDataException("Portable skin refers to a missing joint.");
                string bindId=Input(jointInput,"INV_BIND_MATRIX");
                float[] binds=Transpose(Values(Source(skin,bindId)));
                if(binds.Length!=jointNames.Length*16)throw new InvalidDataException("Inverse bind count differs from joints.");
                int inverse=plan.Floats(binds,"MAT4",jointNames.Length);
                var gltfSkin=D("joints",jointNames.Select(j=>bones[j]).ToArray(),"inverseBindMatrices",inverse,
                    "skeleton",roots[0]);
                int skinIndex=skins.Count;skins.Add(gltfSkin);
                var weightsElement=skin.Elements().Single(e=>e.Name.LocalName=="vertex_weights");
                var weightInputs=weightsElement.Elements().Where(e=>e.Name.LocalName=="input").ToArray();
                int weightStride=weightInputs.Max(e=>Integer(Attr(e,"offset")))+1;
                int jointOffset=Integer(Attr(weightInputs.Single(e=>Attr(e,"semantic")=="JOINT"),"offset"));
                var weightInput=weightInputs.Single(e=>Attr(e,"semantic")=="WEIGHT");
                int weightOffset=Integer(Attr(weightInput,"offset"));
                float[] weightValues=Values(Source(skin,Ref(Attr(weightInput,"source"))));
                int[] vcount=Ints(weightsElement.Elements().Single(e=>e.Name.LocalName=="vcount").Value);
                int[] v=Ints(weightsElement.Elements().Single(e=>e.Name.LocalName=="v").Value);
                if(vcount.Length>2000000||vcount.Sum(x=>(long)x)*weightStride!=v.Length)
                    throw new InvalidDataException("Portable skin weights exceed the work bound.");
                var influence=new List<Tuple<ushort,float>>[vcount.Length];int cursor=0,maxSets=1;
                for(int i=0;i<vcount.Length;i++)
                {
                    if(vcount[i]<1||vcount[i]>128)throw new InvalidDataException("Invalid portable vertex influence count.");
                    var list=new List<Tuple<ushort,float>>();
                    for(int j=0;j<vcount[i];j++)
                    {
                        int bone=v[cursor+jointOffset],wi=v[cursor+weightOffset];cursor+=weightStride;
                        if(bone<0||bone>=jointNames.Length||wi<0||wi>=weightValues.Length||weightValues[wi]<0)
                            throw new InvalidDataException("Invalid portable skin influence.");
                        list.Add(Tuple.Create((ushort)bone,weightValues[wi]));
                    }
                    influence[i]=list;maxSets=Math.Max(maxSets,(list.Count+3)/4);
                }
                var vertex=mesh.Elements().Single(e=>e.Name.LocalName=="vertices");
                string positionId=Input(vertex,"POSITION");
                float[] positions=Values(Source(mesh,positionId));
                if(positions.Length/3!=vcount.Length)throw new InvalidDataException("Geometry and skin vertex counts differ.");
                foreach(var triangles in mesh.Elements().Where(e=>e.Name.LocalName=="triangles"))
                {
                    var inputs=triangles.Elements().Where(e=>e.Name.LocalName=="input").ToArray();
                    int stride=inputs.Max(e=>Integer(Attr(e,"offset")))+1;
                    int vertexOffset=Integer(Attr(inputs.Single(e=>Attr(e,"semantic")=="VERTEX"),"offset"));
                    var normalInput=inputs.Single(e=>Attr(e,"semantic")=="NORMAL");
                    int normalOffset=Integer(Attr(normalInput,"offset"));
                    float[] normals=Values(Source(mesh,Ref(Attr(normalInput,"source"))));
                    var uvInput=inputs.FirstOrDefault(e=>Attr(e,"semantic")=="TEXCOORD"&&(Attr(e,"set")==""||Attr(e,"set")=="0"));
                    float[] uv=uvInput==null?null:Values(Source(mesh,Ref(Attr(uvInput,"source"))));
                    int uvOffset=uvInput==null?-1:Integer(Attr(uvInput,"offset"));
                    int[] p=Ints(triangles.Elements().Single(e=>e.Name.LocalName=="p").Value);
                    int count=Integer(Attr(triangles,"count"))*3;
                    if(count<3||count>2000000||p.Length!=count*stride)
                        throw new InvalidDataException("Portable triangle stream length differs from its header.");
                    var xyz=new float[count*3];var nrm=new float[count*3];var tex=new float[count*2];
                    var joints=Enumerable.Range(0,maxSets).Select(_=>new ushort[count*4]).ToArray();
                    var weights=Enumerable.Range(0,maxSets).Select(_=>new float[count*4]).ToArray();
                    for(int i=0;i<count;i++)
                    {
                        int at=i*stride,vi=p[at+vertexOffset],ni=p[at+normalOffset];
                        if(vi<0||vi>=influence.Length)throw new InvalidDataException("Portable mesh vertex index exceeds skin.");
                        System.Array.Copy(Item(positions,vi,3),0,xyz,i*3,3);
                        System.Array.Copy(Item(normals,ni,3),0,nrm,i*3,3);
                        if(uv!=null)System.Array.Copy(Item(uv,p[at+uvOffset],2),0,tex,i*2,2);
                        for(int k=0;k<influence[vi].Count;k++)
                        {
                            joints[k/4][i*4+k%4]=influence[vi][k].Item1;
                            weights[k/4][i*4+k%4]=influence[vi][k].Item2;
                        }
                    }
                    // Some native GX normals are quantized and their decoded
                    // length is not one. glTF requires unit normals; normalize
                    // only this independent interchange copy.
                    for(int i=0;i<count;i+=3)
                    {
                        var a=new Vector3(xyz[i*3],xyz[i*3+1],xyz[i*3+2]);
                        var b=new Vector3(xyz[(i+1)*3],xyz[(i+1)*3+1],xyz[(i+1)*3+2]);
                        var c=new Vector3(xyz[(i+2)*3],xyz[(i+2)*3+1],xyz[(i+2)*3+2]);
                        var face=Vector3.Cross(b-a,c-a);
                        if(face.LengthSquared()>1e-12f)face=Vector3.Normalize(face);
                        for(int corner=0;corner<3;corner++)
                        {
                            int at=(i+corner)*3;
                            var normal=new Vector3(nrm[at],nrm[at+1],nrm[at+2]);
                            if(normal.LengthSquared()<1e-12f)normal=face;
                            if(normal.LengthSquared()<1e-12f)throw new InvalidDataException("A portable triangle has no usable normal.");
                            normal=Vector3.Normalize(normal);
                            nrm[at]=normal.X;nrm[at+1]=normal.Y;nrm[at+2]=normal.Z;
                        }
                    }
                    var attributes=D("POSITION",plan.Floats(xyz,"VEC3",count,34962,true),
                        "NORMAL",plan.Floats(nrm,"VEC3",count,34962),"TEXCOORD_0",plan.Floats(tex,"VEC2",count,34962));
                    for(int set=0;set<maxSets;set++)
                    {
                        attributes.Add("JOINTS_"+set,plan.Joints(joints[set],count));
                        attributes.Add("WEIGHTS_"+set,plan.Floats(weights[set],"VEC4",count,34962));
                    }
                    var primitive=D("attributes",attributes,"mode",4);
                    string symbol=Attr(triangles,"material");
                    var instanceMaterial=instance.Descendants().FirstOrDefault(e=>e.Name.LocalName=="instance_material"&&Attr(e,"symbol")==symbol);
                    string materialId=Ref(Attr(instanceMaterial,"target"));
                    string file=ImageFile(document,materialId);
                    var material=D("name",materialId.Length==0?"material":materialId,"doubleSided",true);
                    var pbr=D("metallicFactor",0,"roughnessFactor",1);
                    if(file!=null&&File.Exists(Path.Combine(stage,file)))
                    {
                        if(!imageIds.TryGetValue(file,out int image))
                        {
                            byte[] png=File.ReadAllBytes(Path.Combine(stage,file));
                            if(png.Length<8||!png.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))
                                throw new InvalidDataException("Portable image has invalid PNG bytes.");
                            int view=plan.View(()=>plan.Writer.Write(png));
                            image=images.Count;images.Add(D("bufferView",view,"mimeType","image/png","name",file));
                            textures.Add(D("source",image,"sampler",0));imageIds.Add(file,image);
                        }
                        pbr.Add("baseColorTexture",D("index",image));
                    }
                    material.Add("pbrMetallicRoughness",pbr);
                    int materialIndex=materials.Count;materials.Add(material);primitive.Add("material",materialIndex);
                    int meshIndex=meshes.Count;meshes.Add(D("name",geometryId,"primitives",new[]{primitive}));
                    int nodeIndex=nodes.Count;nodes.Add(D("name",geometryId,"mesh",meshIndex,"skin",skinIndex));
                    roots.Add(nodeIndex);
                }
            }
            if(meshes.Count==0)throw new InvalidDataException("No rigged portable mesh was found.");
            object[] animations=null;
            if(clip!=null)
            {
                if(clip.FramesPerSecond<=0||float.IsNaN(clip.FramesPerSecond)||float.IsInfinity(clip.FramesPerSecond))
                    throw new InvalidDataException("Portable animation has no valid timebase.");
                var samplers=new List<object>();var channels=new List<object>();
                foreach(var track in clip.Tracks)
                {
                    if(!bones.TryGetValue(track.Bone,out int node)||track.Frames==null||track.Frames.Length==0||
                        track.Rotation?.Length!=track.Frames.Length||
                        track.Translation!=null&&track.Translation.Length!=track.Frames.Length)
                        throw new InvalidDataException("A portable animation track does not match the owned rig.");
                    for(int i=0;i<track.Frames.Length;i++)
                        if(track.Frames[i]<0||float.IsNaN(track.Frames[i])||float.IsInfinity(track.Frames[i])||
                            i>0&&track.Frames[i]<=track.Frames[i-1])
                            throw new InvalidDataException("Portable animation key times are not strictly increasing.");
                    int time=plan.Floats(track.Frames.Select(f=>f/clip.FramesPerSecond).ToArray(),
                        "SCALAR",track.Frames.Length,0,true);
                    void Channel(string path,float[] values,string type)
                    {
                        int output=plan.Floats(values,type,track.Frames.Length);
                        int sampler=samplers.Count;
                        samplers.Add(D("input",time,"output",output,"interpolation","LINEAR"));
                        channels.Add(D("sampler",sampler,"target",D("node",node,"path",path)));
                    }
                    if(track.Translation!=null)
                        Channel("translation",track.Translation.SelectMany(v=>new[]{v.X,v.Y,v.Z}).ToArray(),"VEC3");
                    Channel("rotation",track.Rotation.SelectMany(q=>
                    {
                        if(q.LengthSquared()<1e-12f)throw new InvalidDataException("A portable animation has a zero quaternion.");
                        q=Quaternion.Normalize(q);return new[]{q.X,q.Y,q.Z,q.W};
                    }).ToArray(),"VEC4");
                }
                animations=new[]{D("name",clip.Name??"Animation","samplers",samplers,"channels",channels)};
            }
            int binaryLength=checked((int)plan.Stream.Position);plan.Align();byte[] bytes=plan.Stream.ToArray();
            var gltf=D("asset",D("version","2.0","generator","Fighterport portable rigged scene"),
                "scene",0,"scenes",new[]{D("nodes",roots.ToArray())},"nodes",nodes,
                "meshes",meshes,"skins",skins,"buffers",new[]{D("byteLength",binaryLength)},
                "bufferViews",plan.Views,"accessors",plan.Accessors,"materials",materials,
                "images",images,"textures",textures,
                "samplers",new[]{D("magFilter",9729,"minFilter",9729,"wrapS",33071,"wrapT",10497)});
            if(animations!=null)gltf.Add("animations",animations);
            var json=new JavaScriptSerializer{MaxJsonLength=64*1024*1024};
            string content=json.Serialize(gltf);
            ((Dictionary<string,object>)((object[])gltf["buffers"])[0]).Add("uri","Model.bin");
            File.WriteAllText(Path.Combine(stage,"Model.gltf"),json.Serialize(gltf),new UTF8Encoding(false));
            File.WriteAllBytes(Path.Combine(stage,"Model.bin"),bytes);
            byte[] jsonBytes=Encoding.UTF8.GetBytes(content);
            int jsonAligned=checked((jsonBytes.Length+3)&~3);
            using(var output=new BinaryWriter(File.Create(Path.Combine(stage,"Model.glb"))))
            {
                output.Write(0x46546C67u);output.Write(2u);output.Write(checked(12+8+jsonAligned+8+bytes.Length));
                output.Write(jsonAligned);output.Write(0x4E4F534Au);output.Write(jsonBytes);
                for(int i=jsonBytes.Length;i<jsonAligned;i++)output.Write((byte)' ');
                output.Write(bytes.Length);output.Write(0x004E4942u);output.Write(bytes);
            }
        }
    }
}
