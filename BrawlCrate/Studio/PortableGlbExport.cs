using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Web.Script.Serialization;

namespace BrawlCrate.Studio
{
    // Minimal glTF 2.0 binary scene for a decoded, posed native mesh. Native
    // rig/animation conversion is a separate adapter gate; no skin is invented.
    internal static class PortableGlbExport
    {
        private static int Int(Dictionary<string,object> value,string key)
            =>Convert.ToInt32(value[key],System.Globalization.CultureInfo.InvariantCulture);
        internal static void Verify(string stage)
        {
            byte[] glb=File.ReadAllBytes(Path.Combine(stage,"Model.glb"));
            if(glb.Length<28||glb.Length>512*1024*1024||BitConverter.ToUInt32(glb,0)!=0x46546C67||
                BitConverter.ToInt32(glb,4)!=2||BitConverter.ToInt32(glb,8)!=glb.Length)
                throw new InvalidDataException("The GLB header or physical length is invalid.");
            int jsonLength=BitConverter.ToInt32(glb,12);
            if(jsonLength<2||jsonLength>32*1024*1024||jsonLength%4!=0||
                28L+jsonLength>glb.Length||BitConverter.ToUInt32(glb,16)!=0x4E4F534A)
                throw new InvalidDataException("The GLB JSON chunk is invalid.");
            int binLength=BitConverter.ToInt32(glb,20+jsonLength);
            if(binLength<1||28L+jsonLength+binLength!=glb.Length||BitConverter.ToUInt32(glb,24+jsonLength)!=0x004E4942)
                throw new InvalidDataException("The GLB BIN chunk is invalid.");
            var serializer=new JavaScriptSerializer{MaxJsonLength=32*1024*1024};
            var gltf=(Dictionary<string,object>)serializer.DeserializeObject(Encoding.UTF8.GetString(glb,20,jsonLength).TrimEnd());
            var buffers=(object[])gltf["buffers"];
            if(buffers.Length!=1)throw new InvalidDataException("The GLB must have exactly one binary buffer.");
            int declared=Int((Dictionary<string,object>)buffers[0],"byteLength");
            if(declared<1||declared>binLength||binLength-declared>3)
                throw new InvalidDataException("The GLB binary buffer length differs from its chunk.");
            var views=((object[])gltf["bufferViews"]).Cast<Dictionary<string,object>>().ToArray();
            foreach(var view in views)
            {
                int offset=Int(view,"byteOffset"),length=Int(view,"byteLength");
                if(Int(view,"buffer")!=0||offset<0||length<1||(long)offset+length>declared)
                    throw new InvalidDataException("A GLB buffer view exceeds the encoded binary data.");
            }
            foreach(var image in (gltf.ContainsKey("images")?(object[])gltf["images"]:Array.Empty<object>())
                .Cast<Dictionary<string,object>>())
            {
                int viewIndex=Int(image,"bufferView");
                if(viewIndex<0||viewIndex>=views.Length||!string.Equals((string)image["mimeType"],"image/png",StringComparison.Ordinal))
                    throw new InvalidDataException("A GLB image has no verified PNG view.");
                int offset=28+jsonLength+Int(views[viewIndex],"byteOffset"),length=Int(views[viewIndex],"byteLength");
                if(length<8||!glb.Skip(offset).Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))
                    throw new InvalidDataException("A GLB image does not contain a PNG signature.");
            }
            bool hasMesh=gltf.ContainsKey("meshes")&&((object[])gltf["meshes"]).Length>0;
            bool hasMotion=gltf.ContainsKey("animations")&&((object[])gltf["animations"]).Length>0;
            if((!hasMesh&&!hasMotion)||((object[])gltf["accessors"]).Length==0)
                throw new InvalidDataException("The GLB has no model geometry or animation.");
            var separated=(Dictionary<string,object>)serializer.DeserializeObject(File.ReadAllText(Path.Combine(stage,"Model.gltf")));
            var sideBuffer=(Dictionary<string,object>)((object[])separated["buffers"])[0];
            if((string)sideBuffer["uri"]!="Model.bin"||Int(sideBuffer,"byteLength")!=declared||
                !File.ReadAllBytes(Path.Combine(stage,"Model.bin")).SequenceEqual(glb.Skip(28+jsonLength)))
                throw new InvalidDataException("The separated glTF binary differs from its GLB counterpart.");
        }
        private static void Align(BinaryWriter writer)
        {while(writer.BaseStream.Position%4!=0)writer.Write((byte)0);}
        private static void Check(Vector3 value)
        {if(float.IsNaN(value.X)||float.IsInfinity(value.X)||float.IsNaN(value.Y)||float.IsInfinity(value.Y)||float.IsNaN(value.Z)||float.IsInfinity(value.Z))throw new InvalidDataException("The posed mesh contains a nonfinite glTF coordinate.");}
        internal static void Write(Bt3NativeMesh.Scene scene,Bt3NativeRender.PosedMesh[] meshes,string stage)
        {
            if(scene==null||meshes==null||meshes.Length==0)throw new InvalidDataException("There is no decoded BT3 model for glTF export.");
            var views=new List<object>();var accessors=new List<object>();var primitives=new List<object>();
            using(var binary=new MemoryStream())using(var writer=new BinaryWriter(binary))
            {
                int AddView(Action write,int target=0)
                {
                    Align(writer);int start=checked((int)binary.Position);write();int size=checked((int)binary.Position-start);
                    if(target==0)views.Add(new{buffer=0,byteOffset=start,byteLength=size});
                    else views.Add(new{buffer=0,byteOffset=start,byteLength=size,target});
                    return views.Count-1;
                }
                int AddVectors3(Vector3[] values,bool bounds)
                {
                    if(values==null||values.Length==0)throw new InvalidDataException("An exported mesh has no positions or normals.");
                    var min=new[]{float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity};
                    var max=new[]{float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity};
                    int view=AddView(()=>{foreach(var value in values){Check(value);float[] v={value.X,value.Y,value.Z};
                        for(int i=0;i<3;i++){writer.Write(v[i]);min[i]=Math.Min(min[i],v[i]);max[i]=Math.Max(max[i],v[i]);}}},34962);
                    if(bounds)accessors.Add(new{bufferView=view,componentType=5126,count=values.Length,type="VEC3",min,max});
                    else accessors.Add(new{bufferView=view,componentType=5126,count=values.Length,type="VEC3"});
                    return accessors.Count-1;
                }
                int AddVectors2(Vector2[] values)
                {
                    if(values==null||values.Length==0)throw new InvalidDataException("An exported mesh has no UV coordinates.");
                    int view=AddView(()=>{foreach(var value in values)
                    {
                        if(float.IsNaN(value.X)||float.IsInfinity(value.X)||float.IsNaN(value.Y)||float.IsInfinity(value.Y))
                            throw new InvalidDataException("The posed mesh contains a nonfinite glTF UV.");
                        writer.Write(value.X);writer.Write(value.Y);
                    }},34962);
                    accessors.Add(new{bufferView=view,componentType=5126,count=values.Length,type="VEC2"});return accessors.Count-1;
                }
                for(int i=0;i<meshes.Length;i++)
                {
                    var mesh=meshes[i];int count=mesh.Positions?.Length??0;
                    if(count<3||count%3!=0||mesh.Normals?.Length!=count||mesh.Uvs?.Length!=count||
                        mesh.Texture<0||mesh.Texture>=scene.Textures.Length)
                        throw new InvalidDataException("A posed mesh has invalid triangle or material data.");
                    int position=AddVectors3(mesh.Positions,true),normal=AddVectors3(mesh.Normals,false),uv=AddVectors2(mesh.Uvs);
                    primitives.Add(new{attributes=new{POSITION=position,NORMAL=normal,TEXCOORD_0=uv},material=mesh.Texture,mode=4});
                }
                var images=new List<object>();
                for(int i=0;i<scene.Textures.Length;i++)
                {
                    string file="Texture-"+i.ToString("D3",System.Globalization.CultureInfo.InvariantCulture)+".png";
                    byte[] png=File.ReadAllBytes(Path.Combine(stage,file));
                    if(png.Length<24||png.Length>64*1024*1024)throw new InvalidDataException("An encoded PNG is outside the glTF image bound.");
                    int view=AddView(()=>writer.Write(png));images.Add(new{bufferView=view,mimeType="image/png"});
                }
                int binaryLength=checked((int)binary.Position);Align(writer);byte[] bytes=binary.ToArray();
                var serializer=new JavaScriptSerializer{MaxJsonLength=32*1024*1024};
                var json=serializer.Serialize(new
                {
                    asset=new{version="2.0",generator="Fighterport portable BT3 pose"},scene=0,
                    scenes=new[]{new{nodes=Enumerable.Range(0,meshes.Length).ToArray()}},
                    nodes=Enumerable.Range(0,meshes.Length).Select(i=>new{name="mesh"+i,mesh=i}).ToArray(),
                    meshes=primitives.Select((primitive,i)=>new{name="mesh"+i,primitives=new[]{primitive}}).ToArray(),
                    buffers=new[]{new{byteLength=binaryLength}},bufferViews=views,accessors,
                    images,textures=Enumerable.Range(0,scene.Textures.Length).Select(i=>new{sampler=0,source=i}).ToArray(),
                    samplers=new[]{new{magFilter=9729,minFilter=9729,wrapS=33071,wrapT=10497}},
                    materials=Enumerable.Range(0,scene.Textures.Length).Select(i=>new
                    {name="Texture-"+i,pbrMetallicRoughness=new{baseColorTexture=new{index=i},metallicFactor=0,roughnessFactor=1},
                        alphaMode="BLEND",doubleSided=true}).ToArray()
                });
                var separated=(Dictionary<string,object>)serializer.DeserializeObject(json);
                separated["buffers"]=new[]{new{byteLength=binaryLength,uri="Model.bin"}};
                File.WriteAllText(Path.Combine(stage,"Model.gltf"),serializer.Serialize(separated),new UTF8Encoding(false));
                File.WriteAllBytes(Path.Combine(stage,"Model.bin"),bytes);
                byte[] jsonBytes=Encoding.UTF8.GetBytes(json);
                int jsonAligned=checked((jsonBytes.Length+3)&~3);
                int total=checked(12+8+jsonAligned+8+bytes.Length);
                using(var file=new BinaryWriter(File.Create(Path.Combine(stage,"Model.glb"))))
                {
                    file.Write(0x46546C67u);file.Write(2u);file.Write(total);
                    file.Write(jsonAligned);file.Write(0x4E4F534Au);file.Write(jsonBytes);
                    for(int i=jsonBytes.Length;i<jsonAligned;i++)file.Write((byte)' ');
                    file.Write(bytes.Length);file.Write(0x004E4942u);file.Write(bytes);
                }
            }
        }
    }
}
