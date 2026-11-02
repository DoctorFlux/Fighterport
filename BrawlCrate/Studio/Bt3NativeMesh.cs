using BrawlLib.Internal;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.OpenGL;
using BrawlLib.SSBB.ResourceNodes;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Vector3=System.Numerics.Vector3;

namespace BrawlCrate.Studio
{
    // Read-only interpretation of the PS2 fighter VIF strips and GS upload
    // packets. Source pack bytes remain owned by the disc inventory.
    internal static class Bt3NativeMesh
    {
        internal sealed class Vertex
        {
            internal Vector3 Position, Normal;
            internal float U,V,Weight;
            internal int Bone=-1;
        }
        internal sealed class Batch
        {
            internal int Bone,Parent,Texture;
            internal Vector3 PivotA,PivotB;
            internal Vertex[] Triangles;
        }
        internal sealed class Texture
        {
            internal int Width,Height;
            internal byte[] Rgba;
        }
        internal sealed class Pivot
        {
            internal Vector3 A,B;
        }
        internal sealed class Scene
        {
            internal Bt3NativeAssetReader.Skeleton Skeleton;
            internal Batch[] Batches;
            internal Texture[] Textures;
            internal Dictionary<int,Pivot> Pivots;
            internal int SourceVertexCount,TriangleCount,SeamTriangleCount;
        }
        private static int I(byte[] b,int p)=>BitConverter.ToInt32(b,p);
        private static uint U(byte[] b,int p)=>BitConverter.ToUInt32(b,p);
        private static ushort H(byte[] b,int p)=>BitConverter.ToUInt16(b,p);
        private static float F(byte[] b,int p)=>BitConverter.ToSingle(b,p);
        private static void Need(byte[] b,long p,long n)
        {if(p<0||n<0||p+n>b.Length)throw new InvalidDataException("BT3 native drawing data exceeds its member.");}
        private static Vector3 Vec(byte[] b,int p)
        {
            Need(b,p,12);var v=new Vector3(F(b,p),F(b,p+4),F(b,p+8));
            if(!Finite(v.X)||!Finite(v.Y)||!Finite(v.Z))throw new InvalidDataException("BT3 drawing data has a nonfinite vector.");
            return v;
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private static byte[][] Pack(byte[] source)
        {
            Need(source,0,8);int count=I(source,0);
            if(count<14||count>4096)throw new InvalidDataException("BT3 costume pack has an unsupported member table.");
            Need(source,4,4L*(count+1));var result=new byte[count][];int previous=4+4*(count+1);
            for(int i=0;i<count;i++)
            {
                int a=I(source,4+i*4),z=I(source,8+i*4);
                if(a<previous||z<a||z>source.Length)throw new InvalidDataException("BT3 costume pack member offsets are invalid.");
                result[i]=new byte[z-a];Buffer.BlockCopy(source,a,result[i],0,z-a);previous=z;
            }
            return result;
        }
        private sealed class TextureRecord
        {
            internal int Block,Format;
            internal Texture Image;
        }
        // PS2 texture packets upload a rectangle of 32-bit words into GS
        // local memory, then TEX0 interprets the same blocks as 4-bit texels.
        // These address functions are stated in terms of the GS page/block/
        // column bit positions, rather than assuming a linear image stream.
        private static int Block32(int x,int y)
            =>(x&1)|((y&1)<<1)|((x&2)<<1)|((y&2)<<2)|((x&4)<<2);
        private static int Column32(int x,int y)
            =>(x&1)|((y&1)<<1)|((x&2)<<1)|((x&4)<<1)|((y&2)<<3)|((y&4)<<3);
        private static int Block4(int x,int y)
            =>((x&1)<<1)|((y&1))|((x&2)<<2)|((y&2)<<1)|((y&4)<<2);
        private static int Column4(int x,int y)
        {
            int group=y>>2;if((group&1)!=0)x^=4;
            int value=((x&1)<<3)|((x&2)<<4)|((x&4)<<4)|((x&8)>>2)|((x&16)>>2);
            if((y&2)!=0)value=(value^64)|1;
            if((y&1)!=0)value+=16;
            return value+group*128;
        }
        private static int Address32(int x,int y,int width)
        {
            int pages=Math.Max(1,(width+63)/64);
            int page=(y/32)*pages+x/64;
            return page*8192+Block32((x/8)&7,(y/8)&3)*256+Column32(x&7,y&7)*4;
        }
        private static int Address4(int x,int y,int width)
        {
            int pages=Math.Max(1,(width+127)/128);
            int page=(y/128)*pages+x/128;
            return page*16384+Block4((x/32)&3,(y/16)&7)*512+Column4(x&31,y&15);
        }
        private static IEnumerable<TextureRecord> ReadTextureFile(byte[] data)
        {
            if(data.Length<32)yield break;
            int count=I(data,0),table=checked(I(data,4)*4);
            if(count<1||count>256||table<16||table+64L*count>data.Length)yield break;
            for(int i=0;i<count;i++)
            {
                int p=table+i*64,pix=checked(I(data,p)*4),clut=checked(I(data,p+4)*4),pixSize=I(data,p+8),clutSize=I(data,p+12);
                ulong tex0=BitConverter.ToUInt64(data,p+0x30);int format=(int)((tex0>>20)&63);
                int width=1<<(int)((tex0>>26)&15),height=1<<(int)((tex0>>30)&15);
                if(width>1024||height>1024||width<1||height<1||!new[]{0,20}.Contains(format))continue;
                int pixels=checked(width*height),needed=format==20?(pixels+1)/2:pixels*4;
                if(pix<=0||pixSize<needed+128||pix+pixSize>data.Length)continue;
                int paletteCount=format==20?16:0;
                if(paletteCount>0&&(clut<=0||clutSize<paletteCount*4+128||clut+clutSize>data.Length))continue;
                // GIF upload: 0x60-byte register packet, raw image, then
                // the final transfer tag. Refuse unrecognised packet layouts.
                if((U(data,pix+0x0C)&0xFF000000)!=0x50000000||
                    (paletteCount>0&&(U(data,clut+0x0C)&0xFF000000)!=0x50000000))continue;
                int uploadWidth=I(data,pix+0x30),uploadHeight=I(data,pix+0x34);
                if(uploadWidth<1||uploadHeight<1||uploadWidth>1024||uploadHeight>1024||
                    (long)uploadWidth*uploadHeight*4!=needed)continue;
                var memory=new byte[needed];
                for(int y=0;y<uploadHeight;y++)for(int x=0;x<uploadWidth;x++)
                {
                    int address=Address32(x,y,uploadWidth),source=pix+0x60+(y*uploadWidth+x)*4;
                    if(address<0||address+4>memory.Length)throw new InvalidDataException("BT3 GS pixel transfer exceeds its texture blocks.");
                    Buffer.BlockCopy(data,source,memory,address,4);
                }
                var rgba=new byte[pixels*4];
                for(int j=0;j<pixels;j++)
                {
                    int color;
                    int x=j%width,y=j/width;
                    if(format==0)
                    {
                        int address=Address32(x,y,width);
                        if(address+4>memory.Length)throw new InvalidDataException("BT3 GS direct texture exceeds its blocks.");
                        int output=j*4;rgba[output]=memory[address];rgba[output+1]=memory[address+1];rgba[output+2]=memory[address+2];
                        rgba[output+3]=(byte)Math.Min(255,memory[address+3]*2);continue;
                    }
                    else
                    {
                        int index;
                        if(format==20)
                        {
                            int nibble=Address4(x,y,width);
                            if(nibble/2>=memory.Length)throw new InvalidDataException("BT3 GS indexed texture exceeds its blocks.");
                            index=(memory[nibble/2]>>(4*(nibble&1)))&15;
                        }
                        else throw new InvalidDataException("BT3 indexed texture format is unsupported.");
                        color=clut+0x60+index*4;
                    }
                    int q=j*4;rgba[q]=data[color];rgba[q+1]=data[color+1];rgba[q+2]=data[color+2];
                    rgba[q+3]=(byte)Math.Min(255,data[color+3]*2);
                }
                yield return new TextureRecord{Block=I(data,p+0x20),Format=format,
                    Image=new Texture{Width=width,Height=height,Rgba=rgba}};
            }
        }
        private static Vertex[] Strip(byte[] data,int p,int vectors,int count)
        {
            if(vectors!=count*3||count<3||count>38)throw new InvalidDataException("BT3 VIF strip vector count is invalid.");
            var vertices=new Vertex[count];
            for(int i=0;i<count;i++)
            {
                int at=p+i*48;var v=new Vertex{Position=Vec(data,at),Normal=Vec(data,at+16),Weight=F(data,at+12),U=F(data,at+32),V=F(data,at+36)};
                if(!Finite(v.Weight)||v.Weight<0||v.Weight>1||!Finite(v.U)||!Finite(v.V))
                    throw new InvalidDataException("BT3 VIF strip contains an invalid weight or UV.");
                vertices[i]=v;
            }
            var triangles=new List<Vertex>((count-2)*3);
            for(int i=2;i<count;i++)
            {
                var a=vertices[i-2];var b=vertices[i-1];var c=vertices[i];
                if(i%2!=0){var swap=a;a=b;b=swap;}
                if(Vector3.Cross(b.Position-a.Position,c.Position-a.Position).LengthSquared()<1e-12f)continue;
                triangles.Add(a);triangles.Add(b);triangles.Add(c);
            }
            return triangles.ToArray();
        }
        internal static Scene Read(byte[] pack)
        {
            var members=Pack(pack);byte[] model=members[2];var rig=Bt3NativeAssetReader.ReadSkeleton(model);
            var textures=ReadTextureFile(members[11]).ToArray();
            if(textures.Length==0)throw new InvalidDataException("BT3 costume texture uploads are unavailable.");
            var lookup=textures.GroupBy(t=>Tuple.Create(t.Block,t.Format)).ToDictionary(g=>g.Key,g=>g.First().Image);
            var imageList=lookup.Values.ToArray();var imageIndex=lookup.ToDictionary(k=>k.Key,k=>Array.IndexOf(imageList,k.Value));
            var batches=new List<Batch>();var pivots=rig.Bones.ToDictionary(b=>b.Id,
                b=>new Pivot{A=Vec(model,b.RecordOffset+0x10),B=Vec(model,b.RecordOffset+0x20)});
            int originalVertices=0;
            foreach(var bone in rig.Bones.Where(b=>b.HasMesh))
            {
                int at=bone.RecordOffset,span=I(model,at),p=at+0x68;
                if(span<0x70||at+span>model.Length||((U(model,at+0x60)>>28)&7)!=6)
                    throw new InvalidDataException("BT3 part DMA chain is unsupported.");
                // The two VIF code words live inside the 16-byte DMA tag;
                // QWC counts the bytes after that tag.
                int end=checked(at+0x70+(int)(U(model,at+0x60)&0xFFFF)*16);
                if(end>at+span)throw new InvalidDataException("BT3 part DMA length exceeds the node.");
                int texture=-1,count=0;bool header=false;
                while(p<end)
                {
                    uint code=U(model,p);p+=4;int op=(int)(code>>24);
                    if(op==0)continue;
                    if(op==0x17){if(!header)throw new InvalidDataException("BT3 VIF batch has no header.");header=false;continue;}
                    if(op!=0x6C)throw new InvalidDataException("BT3 VIF opcode is unsupported: "+op.ToString("X2"));
                    int vectors=(int)((code>>16)&255);int destination=(int)(code&0x3FFF);int bytes=checked(vectors*16);
                    if(vectors<1||p+bytes>end)throw new InvalidDataException("BT3 VIF unpack exceeds the part DMA chain.");
                    if(destination==0&&vectors==5)
                    {
                        ulong tex0=BitConverter.ToUInt64(model,p+16);
                        var key=Tuple.Create((int)(tex0&0x3FFF),(int)((tex0>>20)&63));
                        if(!imageIndex.TryGetValue(key,out texture))
                            throw new InvalidDataException("BT3 model references a missing costume texture block "+key+".");
                        count=(int)(U(model,p+3*16)&0x7FFF);header=true;
                    }
                    else if(destination==5&&header)
                    {
                        var tris=Strip(model,p,vectors,count);originalVertices+=count;
                        if(tris.Length>0)batches.Add(new Batch{Bone=bone.Id,Parent=bone.Parent,Texture=texture,
                            PivotA=Vec(model,at+0x10),PivotB=Vec(model,at+0x20),Triangles=tris});
                    }
                    else throw new InvalidDataException("BT3 VIF batch layout is unsupported.");
                    p+=bytes;
                }
                if(header)throw new InvalidDataException("BT3 VIF batch did not finish.");
            }
            int seam=I(model,0x70),seamTriangles=0;
            if(seam!=0)
            {
                Need(model,seam,16);int vertices=I(model,seam),triangles=I(model,seam+4);
                int vertexAt=I(model,seam+8),triangleAt=I(model,seam+12);
                if(vertices<1||vertices>100000||triangles<1||triangles>100000)
                    throw new InvalidDataException("BT3 seam geometry exceeds its work bound.");
                Need(model,vertexAt,vertices*0x70L);Need(model,triangleAt,triangles*0x20L);
                ulong seamTex0=BitConverter.ToUInt64(model,triangleAt+0x10);
                var seamKey=Tuple.Create((int)(seamTex0&0x3FFF),(int)((seamTex0>>20)&63));
                if(!imageIndex.TryGetValue(seamKey,out int seamTexture))
                    throw new InvalidDataException("BT3 seam references a missing costume texture block.");
                var seamVertices=new Vertex[vertices];var boneIds=new HashSet<int>(rig.Bones.Select(b=>b.Id));
                for(int i=0;i<vertices;i++)
                {
                    int p=vertexAt+i*0x70,id=I(model,p+0x1C);float weight=F(model,p+12);
                    if(!boneIds.Contains(id)||!pivots.ContainsKey(id)||!Finite(weight)||weight<0||weight>1)
                        throw new InvalidDataException("BT3 seam has an invalid native bone or weight.");
                    float u=F(model,p+0x20),v=F(model,p+0x24);
                    if(!Finite(u)||!Finite(v))throw new InvalidDataException("BT3 seam has an invalid UV.");
                    seamVertices[i]=new Vertex{Position=Vec(model,p),Normal=Vec(model,p+0x10),Weight=weight,U=u,V=v,Bone=id};
                }
                var seamTris=new List<Vertex>(triangles*3);
                for(int i=0;i<triangles;i++)
                {
                    int p=triangleAt+i*0x20,a=I(model,p),b=I(model,p+4),c=I(model,p+8);
                    if(a<0||b<0||c<0||a>=vertices||b>=vertices||c>=vertices)
                        throw new InvalidDataException("BT3 seam triangle references an invalid vertex.");
                    seamTris.Add(seamVertices[a]);seamTris.Add(seamVertices[b]);seamTris.Add(seamVertices[c]);
                }
                batches.Add(new Batch{Bone=-1,Texture=seamTexture,Triangles=seamTris.ToArray()});originalVertices+=vertices;
                seamTriangles=triangles;
            }
            if(batches.Count==0)throw new InvalidDataException("BT3 native model has no decoded triangles.");
            return new Scene{Skeleton=rig,Batches=batches.ToArray(),Textures=imageList,Pivots=pivots,SourceVertexCount=originalVertices,
                TriangleCount=batches.Sum(b=>b.Triangles.Length/3),SeamTriangleCount=seamTriangles};
        }
    }

    internal sealed class Bt3NativeRender:IRenderedObject,IDisposable
    {
        private sealed class SkinInput
        {
            internal Bt3NativeMesh.Batch Batch;
            internal Bt3NativeMesh.Vertex Vertex;
        }
        private sealed class Draw:DrawCallBase
        {
            internal int Texture;
            internal Bt3NativeRender Owner;
            internal readonly List<Bt3NativeMesh.Batch> Batches=new List<Bt3NativeMesh.Batch>();
            internal SkinInput[] Inputs;
            internal Vector3[] Positions,Normals;
            public override void Render(ModelPanelViewport view){if(Owner.IsRendering)Owner.Render(this,view);}
            public override int CompareTo(DrawCallBase other)=>other is BoneOverlay?-1:0;
        }
        private sealed class BoneOverlay:DrawCallBase
        {
            internal Bt3NativeRender Owner;
            public override void Render(ModelPanelViewport view)
            {if(Owner.IsRendering&&Owner.ShowBones)Owner.RenderBoneHierarchy(view);}
            public override int CompareTo(DrawCallBase other)=>other is BoneOverlay?0:1;
        }
        internal readonly Bt3NativeMesh.Scene Scene;
        private readonly Dictionary<int,Bt3NativeAssetReader.Bone> boneById;
        private readonly Dictionary<int,Bt3NativeMesh.Pivot> pivotById;
        private readonly List<Draw> draws=new List<Draw>();
        private readonly List<DrawCallBase> calls=new List<DrawCallBase>();
        private readonly HashSet<Bt3NativeMesh.Batch> hiddenBatches=new HashSet<Bt3NativeMesh.Batch>();
        private readonly HashSet<int> hiddenTextures=new HashSet<int>();
        private readonly HashSet<int> hiddenBones=new HashSet<int>();
        private readonly Dictionary<int,Vector3> bonePoints=new Dictionary<int,Vector3>();
        private readonly Dictionary<int,Matrix4x4> boneWorld=new Dictionary<int,Matrix4x4>();
        internal bool ShowBones;
        internal bool ShowPolygons=true;
        internal bool ShowVertices;
        internal int SelectedBoneId=-1;
        private int[] glTextures;
        internal int RenderCount;
        internal void SetBatchVisible(int index,bool visible)
        {
            if(index<0||index>=Scene.Batches.Length)throw new ArgumentOutOfRangeException(nameof(index));
            if(visible)hiddenBatches.Remove(Scene.Batches[index]);else hiddenBatches.Add(Scene.Batches[index]);
        }
        internal void SetTextureVisible(int index,bool visible)
        {
            if(index<0||index>=Scene.Textures.Length)throw new ArgumentOutOfRangeException(nameof(index));
            if(visible)hiddenTextures.Remove(index);else hiddenTextures.Add(index);
        }
        internal void SetBoneVisible(int id,bool visible)
        {
            if(!boneById.ContainsKey(id))throw new ArgumentOutOfRangeException(nameof(id));
            if(visible)hiddenBones.Remove(id);else hiddenBones.Add(id);
        }
        internal int PickBone(ModelPanelViewport view,System.Drawing.Point screen,float radius=12f)
        {
            if(!ShowBones||view==null)return -1;
            int nearest=-1;float limit=radius*radius;
            foreach(var bone in Scene.Skeleton.Bones)
            {
                if(hiddenBones.Contains(bone.Id)||!bonePoints.TryGetValue(bone.Id,out var world))continue;
                var point=view.Camera.Project(new BrawlLib.Internal.Vector3(world.X,world.Y,world.Z));
                float dx=point._x-screen.X,dy=point._y-screen.Y,d2=dx*dx+dy*dy;
                if(d2<limit){limit=d2;nearest=bone.Id;}
            }
            return nearest;
        }
        public bool IsRendering{get;set;}=true;
        public bool Attached{get;private set;}
        public event EventHandler DrawCallsChanged{add{} remove{}}
        public List<DrawCallBase> DrawCalls=>calls;
        internal Bt3NativeRender(Bt3NativeMesh.Scene scene)
        {
            Scene=scene;boneById=scene.Skeleton.Bones.ToDictionary(b=>b.Id);
            pivotById=scene.Pivots;
            foreach(var batch in scene.Batches)
            {
                Draw d=draws.LastOrDefault();
                if(d==null||d.Texture!=batch.Texture)
                {d=new Draw{Texture=batch.Texture,Owner=this};draws.Add(d);calls.Add(d);}
                d.Batches.Add(batch);
            }
            foreach(var d in draws)
            {
                d.Inputs=d.Batches.SelectMany(b=>b.Triangles.Select(v=>new SkinInput{Batch=b,Vertex=v})).ToArray();
                d.Positions=new Vector3[d.Inputs.Length];d.Normals=new Vector3[d.Inputs.Length];
            }
            calls.Add(new BoneOverlay{Owner=this});
            Apply(null,0);
        }
        public void Attach(){Attached=true;}
        public void Detach(){Attached=false;Dispose();}
        public void Refresh(){}
        public void PreRender(ModelPanelViewport view){}
        public Box GetBox()
        {var box=Box.ExpandableVolume;foreach(var d in draws)foreach(var p in d.Positions)box.ExpandVolume(new BrawlLib.Internal.Vector3(p.X,p.Y,p.Z));return box;}
        internal void Apply(Bt3NativeAssetReader.Motion motion,float frame)
        {
            var rig=Scene.Skeleton;var pose=motion==null?null:Bt3NativeAssetReader.Sample(rig,motion,Math.Max(0,Math.Min(frame,motion.Length)));
            var world=new Dictionary<int,Matrix4x4>();
            foreach(var bone in rig.Bones)
            {
                int index=Array.IndexOf(rig.Bones,bone);var rotation=pose?.Rotation[index]??Quaternion.Identity;
                var translation=pose?.Translation[index]??bone.Rest;
                var local=Matrix4x4.CreateFromQuaternion(rotation)*Matrix4x4.CreateTranslation(translation);
                world[bone.Id]=bone.Parent<0?local:local*world[bone.Parent];
            }
            bonePoints.Clear();
            boneWorld.Clear();
            foreach(var bone in rig.Bones)
            {
                var p=world[bone.Id].Translation;
                bonePoints[bone.Id]=new Vector3(-p.X,-p.Y,-p.Z);
                boneWorld[bone.Id]=world[bone.Id];
            }
            foreach(var d in draws)
            {
                for(int i=0;i<d.Inputs.Length;i++)
                {
                    var input=d.Inputs[i];var batch=input.Batch;var v=input.Vertex;
                    int id=v.Bone<0?batch.Bone:v.Bone;
                    var bone=boneById[id];var a=world[id];
                    var b=bone.Parent<0?Matrix4x4.Identity:world[bone.Parent];
                    var pivotA=v.Bone<0?batch.PivotA:pivotById[id].A;
                    var pivotB=v.Bone<0?batch.PivotB:pivotById[id].B;
                    var pa=Vector3.Transform(v.Position-pivotA,a);
                    var pb=Vector3.Transform(v.Position-pivotB,b);
                    var native=Vector3.Lerp(pb,pa,v.Weight);
                    // BT3's Y is down, and its front faces native -Z. Turn
                    // the read-only presentation toward this viewport.
                    d.Positions[i]=new Vector3(-native.X,-native.Y,-native.Z);
                    var na=Vector3.TransformNormal(v.Normal,a);var nb=Vector3.TransformNormal(v.Normal,b);
                    var n=Vector3.Lerp(nb,na,v.Weight);n=new Vector3(-n.X,-n.Y,-n.Z);
                    d.Normals[i]=n.LengthSquared()>1e-12f?Vector3.Normalize(n):Vector3.UnitZ;
                }
            }
        }
        private void InitTextures()
        {
            if(glTextures!=null)return;glTextures=new int[Scene.Textures.Length];
            GL.ActiveTexture(TextureUnit.Texture0);GL.PixelStore(PixelStoreParameter.UnpackAlignment,1);
            for(int i=0;i<glTextures.Length;i++)
            {
                var t=Scene.Textures[i];glTextures[i]=GL.GenTexture();GL.BindTexture(TextureTarget.Texture2D,glTextures[i]);
                GL.TexImage2D(TextureTarget.Texture2D,0,PixelInternalFormat.Rgba,t.Width,t.Height,0,OpenTK.Graphics.OpenGL.PixelFormat.Rgba,PixelType.UnsignedByte,t.Rgba);
                GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter,(int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter,(int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapS,(int)TextureWrapMode.ClampToEdge);
                // Native VIF ST has T in [-1,0]; GS wraps that interval.
                GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapT,(int)TextureWrapMode.Repeat);
            }
        }
        private void Render(Draw d,ModelPanelViewport view)
        {
            GL.GetInteger(GetPName.CurrentProgram,out int priorProgram);
            GL.PushAttrib(AttribMask.AllAttribBits);
            try
            {
                InitTextures();GL.UseProgram(0);GL.ActiveTexture(TextureUnit.Texture0);
                GL.Enable(EnableCap.Texture2D);GL.BindTexture(TextureTarget.Texture2D,glTextures[d.Texture]);
                GL.Enable(EnableCap.DepthTest);GL.Disable(EnableCap.CullFace);
                GL.Enable(EnableCap.Blend);GL.BlendFunc(BlendingFactorSrc.SrcAlpha,BlendingFactorDest.OneMinusSrcAlpha);
                if(ShowPolygons&&!hiddenTextures.Contains(d.Texture))
                {
                    GL.Begin(BeginMode.Triangles);
                    for(int i=0;i+2<d.Inputs.Length;i+=3)
                    {
                        if(hiddenBatches.Contains(d.Inputs[i].Batch))continue;
                        for(int j=i;j<i+3;j++)
                        {
                            var n=d.Normals[j];float key=Math.Max(0,Vector3.Dot(n,Vector3.Normalize(new Vector3(-.35f,.7f,.6f))));
                            float fill=Math.Max(0,Vector3.Dot(n,Vector3.Normalize(new Vector3(.5f,.3f,-.4f))));
                            float light=Math.Min(1.15f,.64f+.38f*key+.18f*fill);
                            GL.Color4(light,light,light,1f);var uv=d.Inputs[j].Vertex;GL.TexCoord2(uv.U,uv.V);
                            var p=d.Positions[j];GL.Vertex3(p.X,p.Y,p.Z);
                        }
                    }
                    GL.End();RenderCount++;
                }
                if(ShowVertices)
                {
                    GL.Disable(EnableCap.Texture2D);GL.Disable(EnableCap.Lighting);
                    GL.Disable(EnableCap.DepthTest);GL.PointSize(2f);GL.Color4(.95f,.72f,.22f,1f);
                    GL.Begin(BeginMode.Points);
                    for(int i=0;i<d.Positions.Length;i++)if(!hiddenBatches.Contains(d.Inputs[i].Batch))
                    {var p=d.Positions[i];GL.Vertex3(p.X,p.Y,p.Z);}
                    GL.End();
                }
            }
            finally{GL.UseProgram(priorProgram);GL.PopAttrib();}
        }
        private void RenderBoneHierarchy(ModelPanelViewport view)
        {
            GL.PushAttrib(AttribMask.AllAttribBits);
            try
            {
            GL.Disable(EnableCap.Texture2D);GL.Disable(EnableCap.Lighting);
            GL.Disable(EnableCap.DepthTest);GL.Disable(EnableCap.Blend);
            GL.LineWidth(1.5f);GL.Color4(.20f,.90f,.95f,1f);
            foreach(var bone in Scene.Skeleton.Bones)
            {
                if(hiddenBones.Contains(bone.Id)||!bonePoints.TryGetValue(bone.Id,out var point))continue;
                bool selected=bone.Id==SelectedBoneId;
                var line=selected?System.Drawing.Color.FromArgb(0,128,255):MDL0BoneNode.DefaultLineColor;
                GL.Color4(line.R/255f,line.G/255f,line.B/255f,1f);
                if(bone.Parent>=0&&bonePoints.TryGetValue(bone.Parent,out var parent))
                {
                    GL.Begin(BeginMode.Lines);
                    GL.Vertex3(parent.X,parent.Y,parent.Z);GL.Vertex3(point.X,point.Y,point.Z);
                    GL.End();
                }
                var orb=selected?System.Drawing.Color.FromArgb(255,128,0):MDL0BoneNode.DefaultNodeColor;
                GL.Color4(orb.R/255f,orb.G/255f,orb.B/255f,1f);
                GL.PushMatrix();GL.Translate(point.X,point.Y,point.Z);
                float scale=view?._renderAttrib._scaleBones==true?
                    BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase.ModelEditorBase.OrbRadius(
                        new BrawlLib.Internal.Vector3(point.X,point.Y,point.Z),view.Camera):1f;
                GL.Scale(scale,scale,scale);
                TKContext.FindOrCreate("BoneNodeOrb",MDL0BoneNode.CreateNodeOrb).Call();
                GL.PopMatrix();
                if(!boneWorld.TryGetValue(bone.Id,out var transform))continue;
                const float axis=MDL0BoneNode._nodeRadius*2f;
                var x=-Vector3.TransformNormal(Vector3.UnitX,transform)*axis*scale;
                var y=-Vector3.TransformNormal(Vector3.UnitY,transform)*axis*scale;
                var z=-Vector3.TransformNormal(Vector3.UnitZ,transform)*axis*scale;
                GL.Begin(BeginMode.Lines);
                GL.Color4(1f,0f,0f,1f);GL.Vertex3(point.X,point.Y,point.Z);GL.Vertex3(point.X+x.X,point.Y+x.Y,point.Z+x.Z);
                GL.Color4(0f,1f,0f,1f);GL.Vertex3(point.X,point.Y,point.Z);GL.Vertex3(point.X+y.X,point.Y+y.Y,point.Z+y.Z);
                GL.Color4(0f,0f,1f,1f);GL.Vertex3(point.X,point.Y,point.Z);GL.Vertex3(point.X+z.X,point.Y+z.Y,point.Z+z.Z);
                GL.End();
                if(selected&&view!=null)
                {
                    var projected=view.Camera.Project(new BrawlLib.Internal.Vector3(point.X,point.Y,point.Z));
                    view.SettingsScreenText["Bone "+bone.Id.ToString("D2")]=new BrawlLib.Internal.Vector3(
                        projected._x,projected._y-9f,projected._z);
                }
            }
            }
            finally{GL.PopAttrib();}
        }
        public void Dispose()
        {
            if(glTextures==null)return;
            if(TKContext.CurrentContext!=null)foreach(int id in glTextures)if(id!=0)GL.DeleteTexture(id);
            glTextures=null;
        }
    }
}
