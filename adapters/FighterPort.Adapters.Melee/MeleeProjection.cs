using HSDRaw;
using HSDRaw.Common;
using HSDRaw.GX;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
namespace FighterPort.Adapters.Melee
{
    public sealed class MeleeProjectionReport
    {
        public string ColladaPath;
        public int Bones,Vertices,Triangles,Influences,Textures,DisplayObjects;
        public readonly List<MeleeMesh> Meshes=new List<MeleeMesh>();
        public readonly List<string> Notices=new List<string>();
    }
    public sealed class MeleeVertex
    {
        public Vector3 Position,Normal,Tangent,Bitangent;
        public Vector3 NativePosition,NativeNormal;
        public int MatrixIndex;
        public Vector2 Uv;
        public Vector2[] Uvs;
        public Vector4 Color;
        public readonly List<Tuple<int,float>> Weights=new List<Tuple<int,float>>();
    }
    public sealed class MeleeMesh
    {
        public MeleeVertex[] Vertices;
        public HSD_MOBJ Material;
        public JOBJ_FLAG ParentFlags;
        public POBJ_FLAG PolygonFlags;
        public bool NativeHidden,NativeEnvelope;
        public int OwnerJoint,SharedJoint=-1,DisplayObject;
    }
    // Independent display/port projection; never replaces the original HSD graph.
    public static class MeleeProjection
    {
        private static readonly XNamespace Ns="http://www.collada.org/2005/11/COLLADASchema";
        private static XElement E(string n,params object[] c)=>new XElement(Ns+n,c);
        private static XAttribute A(string n,object v)=>new XAttribute(n,v);
        private static string F(float n){MeleeNative.Finite(n);return n.ToString("R",CultureInfo.InvariantCulture);}
        private static string Join(IEnumerable<float> n)=>string.Join(" ",n.Select(F));
        public static string JointName(int i)=>"JOBJ_"+i.ToString("D5",CultureInfo.InvariantCulture);
        private static IEnumerable<float> MatrixValues(Matrix4x4 m)=>new[]{m.M11,m.M21,m.M31,m.M41,m.M12,m.M22,m.M32,m.M42,m.M13,m.M23,m.M33,m.M43,m.M14,m.M24,m.M34,m.M44};
        public static Matrix4x4 Local(HSD_JOBJ j)=>Matrix4x4.CreateScale(j.SX,j.SY,j.SZ)*Matrix4x4.CreateRotationX(j.RX)*Matrix4x4.CreateRotationY(j.RY)*Matrix4x4.CreateRotationZ(j.RZ)*Matrix4x4.CreateTranslation(j.TX,j.TY,j.TZ);
        public static Matrix4x4 Inverse(HSD_JOBJ j,Matrix4x4 world)
        {
            if(j.InverseWorldTransform is HSD_Matrix4x3 m)return new Matrix4x4(m.M11,m.M21,m.M31,0,m.M12,m.M22,m.M32,0,m.M13,m.M23,m.M33,0,m.M14,m.M24,m.M34,1);
            if(!Matrix4x4.Invert(world,out var inverse))throw new InvalidDataException("Singular native bind matrix has no compatible MDL0 projection.");return inverse;
        }
        private static XElement FloatSource(string id,IEnumerable<float> values,int stride,string[] names)
        {
            var array=values.ToArray();return E("source",A("id",id),E("float_array",A("id",id+"-array"),A("count",array.Length),Join(array)),E("technique_common",E("accessor",A("source","#"+id+"-array"),A("count",array.Length/stride),A("stride",stride),names.Select(n=>E("param",A("name",n),A("type",stride==16?"float4x4":"float"))))));
        }
        public static MeleeProjectionReport Prepare(MeleeNativeModel native)=>Write(native,null,false);
        public static MeleeProjectionReport Write(MeleeNativeModel native,string directory,bool writeInterchange=true)
        {
            if(writeInterchange)Directory.CreateDirectory(directory);var report=new MeleeProjectionReport();
            report.Notices.Add("Native HSD source is retained. This independent MDL0 display uses diffuse textures; HSD material effects, visibility/material animation and runtime effects need separate qualification.");
            var joints=MeleeNative.Joints(native.Root);var ids=joints.Select((j,i)=>new{j._s,Index=i}).ToDictionary(j=>j._s,j=>j.Index);
            var parents=Enumerable.Repeat(-1,joints.Length).ToArray();var worlds=new Matrix4x4[joints.Length];var inverses=new Matrix4x4[joints.Length];
            for(int i=0;i<joints.Length;i++)
            {
                for(var child=joints[i].Child;child!=null;child=child.Next)parents[ids[child._s]]=i;
                var unsupported=JOBJ_FLAG.USE_QUATERNION|JOBJ_FLAG.MTX_INDEPEND_PARENT|JOBJ_FLAG.MTX_INDEPEND_SRT|JOBJ_FLAG.USER_DEFINED_MTX|JOBJ_FLAG.INSTANCE|JOBJ_FLAG.MTX_SCALE_COMPENSATE;
                if((joints[i].Flags&unsupported)!=0)throw new NotSupportedException("Native joint "+i+" requires unqualified HSD transform semantics: "+(joints[i].Flags&unsupported));
                worlds[i]=Local(joints[i])*(parents[i]<0?Matrix4x4.Identity:worlds[parents[i]]);inverses[i]=Inverse(joints[i],worlds[i]);
            }
            var root=E("COLLADA",A("version","1.4.1"));var document=new XDocument(root);
            root.Add(E("asset",E("contributor",E("authoring_tool","Fighterport HSD projection")),E("unit",A("name","native"),A("meter",1)),E("up_axis","Y_UP")));
            var images=E("library_images");var effects=E("library_effects");var materials=E("library_materials");var geometries=E("library_geometries");var controllers=E("library_controllers");var scene=E("visual_scene",A("id","Scene"));var nodes=new XElement[joints.Length];
            for(int i=0;i<joints.Length;i++)
            {
                string name=JointName(i);nodes[i]=E("node",A("id",name),A("sid",name),A("name",name),A("type","JOINT"),E("matrix",A("sid","transform"),Join(MatrixValues(Local(joints[i])))));
                if(parents[i]<0)scene.Add(nodes[i]);else nodes[parents[i]].Add(nodes[i]);
            }
            int meshIndex=0;
            for(int ji=0;ji<joints.Length;ji++)
            {
                var seenD=new HashSet<HSDStruct>();
                for(var d=joints[ji].Dobj;d!=null;d=d.Next)
                {
                    if(!seenD.Add(d._s))throw new InvalidDataException("Cyclic display object list.");
                    int displayObject=report.DisplayObjects++;
                    var vertices=new List<MeleeVertex>();var seenP=new HashSet<HSDStruct>();
                    for(var p=d.Pobj;p!=null;p=p.Next)
                    {
                        int nativeFirst=vertices.Count;
                        if(!seenP.Add(p._s))throw new InvalidDataException("Cyclic polygon object list.");
                        if(p.Flags.HasFlag(POBJ_FLAG.SHAPEANIM))report.Notices.Add("Mesh "+meshIndex+": shape animation is retained natively; display shows its base mesh.");
                        int nativeType=(int)p.Flags&0x3000;
                        if(nativeType==0x3000)throw new NotSupportedException("Invalid native polygon matrix type.");
                        bool nativeEnvelope=nativeType==0x2000;
                        HSD_JOBJ shared=nativeType==0?p._s.GetReference<HSD_JOBJ>(0x14):null;
                        int sharedId=-1;if(shared!=null&&!ids.TryGetValue(shared._s,out sharedId))throw new InvalidDataException("Shared native matrix references an external joint.");
                        var dl=p.ToDisplayList();int offset=0;bool hasEnvelope=p.HasAttribute(GXAttribName.GX_VA_PNMTXIDX);var envelopes=nativeEnvelope?p.EnvelopeWeights:null;
                        foreach(var prim in dl.Primitives)
                        {
                            var decoded=dl.Vertices.Skip(offset).Take(prim.Count).ToArray();offset+=prim.Count;
                            foreach(int vi in Triangles(prim.PrimitiveType,decoded.Length))
                            {
                                var v=decoded[vi];var vertex=new MeleeVertex{Position=new Vector3(v.POS.X,v.POS.Y,v.POS.Z),Normal=new Vector3(v.NRM.X,v.NRM.Y,v.NRM.Z),Uv=new Vector2(v.TEX0.X,v.TEX0.Y),Uvs=new[]{new Vector2(v.TEX0.X,v.TEX0.Y),new Vector2(v.TEX1.X,v.TEX1.Y),new Vector2(v.TEX2.X,v.TEX2.Y),new Vector2(v.TEX3.X,v.TEX3.Y),new Vector2(v.TEX4.X,v.TEX4.Y),new Vector2(v.TEX5.X,v.TEX5.Y),new Vector2(v.TEX6.X,v.TEX6.Y),new Vector2(v.TEX7.X,v.TEX7.Y)},Color=new Vector4(v.CLR0.R,v.CLR0.G,v.CLR0.B,v.CLR0.A),Tangent=new Vector3(v.TAN.X,v.TAN.Y,v.TAN.Z),Bitangent=new Vector3(v.BITAN.X,v.BITAN.Y,v.BITAN.Z)};
                                vertex.NativePosition=vertex.Position;vertex.NativeNormal=vertex.Normal;vertex.MatrixIndex=v.PNMTXIDX/3;
                                Matrix4x4 single=Matrix4x4.Identity;
                                if(nativeEnvelope)
                                {
                                    int ei=hasEnvelope?v.PNMTXIDX/3:0;
                                    if(envelopes==null||ei<0||ei>=envelopes.Length)throw new InvalidDataException("Envelope index is outside its native table.");var env=envelopes[ei];
                                    for(int w=0;w<env.EnvelopeCount;w++)
                                    {
                                        if(!ids.TryGetValue(env.GetJOBJAt(w)._s,out int bind))throw new InvalidDataException("Native envelope references an external joint.");
                                        float weight=env.GetWeightAt(w);MeleeNative.Finite(weight);if(weight<0)throw new NotSupportedException("Negative native skin weight.");vertex.Weights.Add(Tuple.Create(bind,weight));
                                    }
                                    single=env.EnvelopeCount>0&&env.GetWeightAt(0)==1?worlds[ids[env.GetJOBJAt(0)._s]]:worlds[ji];
                                }
                                else
                                {
                                    if(sharedId>=0&&vertex.MatrixIndex>1)throw new InvalidDataException("Shared native matrix index exceeds the two supplied joints.");
                                    int bind=sharedId>=0&&vertex.MatrixIndex==1?sharedId:ji;vertex.Weights.Add(Tuple.Create(bind,1f));single=worlds[bind];
                                }
                                var transform=Matrix4x4.Identity;
                                if(!p.Flags.HasFlag(POBJ_FLAG.SHAPESET_AVERAGE)&&!hasEnvelope)transform*=worlds[ji];
                                if((joints[ji].Flags&(JOBJ_FLAG.SKELETON|JOBJ_FLAG.SKELETON_ROOT))!=0||p.Flags.HasFlag(POBJ_FLAG.UNKNOWN2))transform*=single;
                                if(p.SingleBoundJOBJ!=null)transform*=worlds[ids[p.SingleBoundJOBJ._s]];
                                vertex.Position=Vector3.Transform(vertex.Position,transform);vertex.Normal=Vector3.TransformNormal(vertex.Normal,transform);
                                if(vertex.Weights.Count==0)throw new InvalidDataException("Native vertex has no verified skin binding.");vertices.Add(vertex);report.Influences+=vertex.Weights.Count;
                            }
                        }
                        bool hidden=false;for(int ancestor=ji;ancestor>=0;ancestor=parents[ancestor])hidden|=(joints[ancestor].Flags&JOBJ_FLAG.HIDDEN)!=0;
                        report.Meshes.Add(new MeleeMesh{Vertices=vertices.Skip(nativeFirst).ToArray(),Material=d.Mobj,ParentFlags=joints[ji].Flags,PolygonFlags=p.Flags,NativeHidden=hidden,NativeEnvelope=nativeEnvelope,OwnerJoint=ji,SharedJoint=sharedId,DisplayObject=displayObject});
                    }
                    if(vertices.Count==0)continue;
                    string id="mesh"+meshIndex++,mat=id+"-material",fx=id+"-effect",textureId=id+"-texture";HSD_TOBJ tex=null;
                    var seenTex=new HashSet<HSDStruct>();
                    for(var t=d.Mobj?.Textures;t!=null;t=t.Next){if(!seenTex.Add(t._s))throw new InvalidDataException("Cyclic texture list.");if(t.ImageData!=null&&(tex==null||t.DiffuseLightmap))tex=t;}
                    XElement diffuse;
                    var profile=E("profile_COMMON");
                    if(tex!=null)
                    {
                        int width=tex.ImageData.Width,height=tex.ImageData.Height;if(width<1||height<1||(long)width*height>16777216)throw new InvalidDataException("Native texture dimensions exceed the display budget.");var bgra=tex.GetDecodedImageData();
                        if(bgra.Length!=width*height*4)throw new InvalidDataException("Decoded native texture length differs.");
                        string png=writeInterchange?Path.Combine(directory,textureId+".png"):textureId+".png";
                        if(writeInterchange)using(var bitmap=new Bitmap(width,height,PixelFormat.Format32bppArgb))
                        {var bits=bitmap.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,bitmap.PixelFormat);try{for(int row=0;row<height;row++)Marshal.Copy(bgra,row*width*4,IntPtr.Add(bits.Scan0,row*bits.Stride),width*4);}finally{bitmap.UnlockBits(bits);}bitmap.Save(png,ImageFormat.Png);}
                        if(writeInterchange)images.Add(E("image",A("id",textureId),E("init_from",new Uri(png).AbsoluteUri)));report.Textures++;
                        profile.Add(E("newparam",A("sid",id+"-surface"),E("surface",A("type","2D"),E("init_from",textureId))),E("newparam",A("sid",id+"-sampler"),E("sampler2D",E("source",id+"-surface"))));diffuse=E("texture",A("texture",id+"-sampler"),A("texcoord","UVSET0"));
                    }
                    else{var color=d.Mobj?.Material?.DiffuseColor??Color.White;diffuse=E("color",Join(new[]{color.R/255f,color.G/255f,color.B/255f,color.A/255f}));}
                    profile.Add(E("technique",A("sid","common"),E("phong",E("diffuse",diffuse))));effects.Add(E("effect",A("id",fx),profile));materials.Add(E("material",A("id",mat),A("name",mat),E("instance_effect",A("url","#"+fx))));
                    var mesh=E("mesh",FloatSource(id+"-positions",vertices.SelectMany(v=>new[]{v.Position.X,v.Position.Y,v.Position.Z}),3,new[]{"X","Y","Z"}),FloatSource(id+"-normals",vertices.SelectMany(v=>new[]{v.Normal.X,v.Normal.Y,v.Normal.Z}),3,new[]{"X","Y","Z"}),FloatSource(id+"-uv",vertices.SelectMany(v=>new[]{v.Uv.X,v.Uv.Y}),2,new[]{"S","T"}),E("vertices",A("id",id+"-vertices"),E("input",A("semantic","POSITION"),A("source","#"+id+"-positions"))),E("triangles",A("count",vertices.Count/3),A("material",mat),E("input",A("semantic","VERTEX"),A("source","#"+id+"-vertices"),A("offset",0)),E("input",A("semantic","NORMAL"),A("source","#"+id+"-normals"),A("offset",1)),E("input",A("semantic","TEXCOORD"),A("source","#"+id+"-uv"),A("offset",2),A("set",0)),E("p",string.Join(" ",Enumerable.Range(0,vertices.Count).SelectMany(i=>new[]{i,i,i})))));
                    geometries.Add(E("geometry",A("id",id),A("name",id),mesh));
                    var weights=vertices.SelectMany(v=>v.Weights).ToArray();var skin=E("skin",A("source","#"+id),E("bind_shape_matrix",Join(MatrixValues(Matrix4x4.Identity))),E("source",A("id",id+"-joints"),E("Name_array",A("id",id+"-joints-array"),A("count",joints.Length),string.Join(" ",Enumerable.Range(0,joints.Length).Select(JointName))),E("technique_common",E("accessor",A("source","#"+id+"-joints-array"),A("count",joints.Length),A("stride",1),E("param",A("name","JOINT"),A("type","Name"))))),FloatSource(id+"-binds",inverses.SelectMany(MatrixValues),16,new[]{"TRANSFORM"}),FloatSource(id+"-weights",weights.Select(w=>w.Item2),1,new[]{"WEIGHT"}),E("joints",E("input",A("semantic","JOINT"),A("source","#"+id+"-joints")),E("input",A("semantic","INV_BIND_MATRIX"),A("source","#"+id+"-binds"))),E("vertex_weights",A("count",vertices.Count),E("input",A("semantic","JOINT"),A("source","#"+id+"-joints"),A("offset",0)),E("input",A("semantic","WEIGHT"),A("source","#"+id+"-weights"),A("offset",1)),E("vcount",string.Join(" ",vertices.Select(v=>v.Weights.Count))),E("v",string.Join(" ",weights.SelectMany((w,i)=>new[]{w.Item1,i})))));
                    controllers.Add(E("controller",A("id",id+"-skin"),skin));scene.Add(E("node",A("id",id+"-node"),E("instance_controller",A("url","#"+id+"-skin"),E("skeleton","#"+JointName(0)),E("bind_material",E("technique_common",E("instance_material",A("symbol",mat),A("target","#"+mat),E("bind_vertex_input",A("semantic","UVSET0"),A("input_semantic","TEXCOORD"),A("input_set",0))))))));
                    report.Vertices+=vertices.Count;report.Triangles+=vertices.Count/3;
                }
            }
            root.Add(images,effects,materials,geometries,controllers,E("library_visual_scenes",scene),E("scene",E("instance_visual_scene",A("url","#Scene"))));
            report.Bones=joints.Length;if(writeInterchange){report.ColladaPath=Path.Combine(directory,"MeleeModel.dae");document.Save(report.ColladaPath);}return report;
        }
        private static IEnumerable<int> Triangles(GXPrimitiveType primitive,int count)
        {
            if(primitive==GXPrimitiveType.Triangles){if(count%3!=0)throw new InvalidDataException("Incomplete native triangle.");for(int i=0;i<count;i++)yield return i;}
            else if(primitive==GXPrimitiveType.TriangleStrip){for(int i=2;i<count;i++){yield return i-2;yield return (i&1)==0?i-1:i;yield return (i&1)==0?i:i-1;}}
            else if(primitive==GXPrimitiveType.Quads){if(count%4!=0)throw new InvalidDataException("Incomplete native quad.");for(int i=0;i<count;i+=4)foreach(int n in new[]{0,1,2,0,2,3})yield return i+n;}
            else if(primitive==GXPrimitiveType.TriangleFan){for(int i=2;i<count;i++){yield return 0;yield return i-1;yield return i;}}
            else throw new NotSupportedException("Native display primitive requires qualification: "+primitive);
        }
    }
}
