using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using BrawlLib.Internal;
using BrawlLib.Modeling;
using BrawlLib.Modeling.Collada;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Models;
using BrawlLib.SSBB.Types;

internal sealed class Request
{
    public string Operation {get;set;} public string Model {get;set;} public string Motion {get;set;} public string Entry {get;set;} public string Output {get;set;}
    public Edit[] Edits {get;set;} public double[] Samples {get;set;}
    public string ModelEntry {get;set;}
    public TextureMatrixInput[] TextureMatrices {get;set;}
    public int? OutputSampleCount {get;set;} public bool? OutputLoop {get;set;} public bool ReplaceTracks {get;set;} public bool RewriteChannels {get;set;}
    public string OutputName {get;set;}
    public string MorphEntry {get;set;}
    public MorphChange[] MorphChanges {get;set;} public string LogicalOutput {get;set;}
    public GeometryChange[] GeometryChanges {get;set;} public string ModelOutput {get;set;} public string LogicalModelOutput {get;set;}
}
internal sealed class TextureMatrixInput {public int Mode {get;set;} public float[] Values {get;set;} }
internal sealed class Edit {public string Bone {get;set;} public int Channel {get;set;} public int Frame {get;set;} public float Value {get;set;} public float Tangent {get;set;} public int? ValueBits {get;set;} public int? TangentBits {get;set;}}
internal static unsafe partial class Program
{
    static readonly JavaScriptSerializer Json=new JavaScriptSerializer {MaxJsonLength=256*1024*1024,RecursionLimit=200};
    static readonly List<string> Diagnostics=new List<string>();
    [STAThread] static int Main(string[] args)
    {
        string pending=null;
        try
        {
            if(args.Length!=2)throw new ArgumentException("Expected request.json response.json");
            var r=Json.Deserialize<Request>(File.ReadAllText(args[0]));
            if(r.Operation=="texture-matrices"){var matrices=r.TextureMatrices.Select(c=>{var f=c.Values;return RawMatrix(new TextureFrameState(new Vector2(f[0],f[1]),f[2],new Vector2(f[3],f[4]),(TexMatrixMode)c.Mode,false)._transform);}).ToArray();File.WriteAllText(args[1],Json.Serialize(new{Diagnostics=new[]{"Native Matrix34/TextureFrameState evaluation"},TextureMatrixSamples=matrices}));return 0;}
            if(r.Operation=="fixture") {Fixture(r);File.WriteAllText(args[1],Json.Serialize(new {Diagnostics=new[]{"Original native fixture generated"}}));return 0;}
            if(r.Operation=="morph-fixture"||r.Operation=="morph-copy"){MorphFixture(r);File.WriteAllText(args[1],Json.Serialize(new{Diagnostics=new[]{"Authored native morph fixture generated; source unchanged"}}));return 0;}
            if(r.Operation=="wrap-motion"){WrapMotionFixture(r);File.WriteAllText(args[1],Json.Serialize(new{Diagnostics=new[]{"New PAC fixture composed; inputs unchanged"}}));return 0;}
            bool modelOnly=r.Operation=="model"||r.Operation=="create";
            if(new FileInfo(r.Model).Length>128*1024*1024 || (!modelOnly&&new FileInfo(r.Motion).Length>128*1024*1024))throw new NotSupportedException("Native worker input budget is 128 MiB per file. Disc images require a separately verified source provider.");
            var mh=Hash(r.Model);var ch=modelOnly?"":Hash(r.Motion);
            using(var modelRoot=NodeFactory.FromFile(null,r.Model))
            using(var motionRoot=modelOnly?null:NodeFactory.FromFile(null,r.Motion))
            {
                if(modelRoot==null || (motionRoot==null&&!modelOnly))throw new InvalidDataException("Unrecognized native input.");
                var models=Walk(modelRoot).OfType<MDL0Node>().ToArray();if(models.Length==0)throw new InvalidDataException("No native MDL0 model found.");
                var model=r.ModelEntry==null?models.OrderByDescending(m=>m.NumVertices).First():models.Single(m=>Identity(m)==r.ModelEntry);
                MorphFloatBytes.Cache(model);
                if(models.Length>1)Diagnostics.Add("Preview selects largest geometry model "+model.Name+" ["+Identity(model)+"]; other native model resources are preserved in the original.");
                var clips=motionRoot==null?new CHR0Node[0]:Walk(motionRoot).OfType<CHR0Node>().ToArray();if(clips.Length==0&&!modelOnly)throw new InvalidDataException("No CHR0 animation in separate motion resource.");
                foreach(var nativeClip in clips)NativeChrFloats.Cache(nativeClip);
                object response;
                if(r.Operation=="export"||r.Operation=="insert"||r.Operation=="create")
                {
                    if(File.Exists(r.Output))throw new IOException("Output already exists.");
                    if(Same(r.Output,r.Model)||!modelOnly&&Same(r.Output,r.Motion))throw new IOException("Output must not overwrite a source.");
                    var basis=r.Operation=="create"?null:clips.Single(c=>Identity(c)==r.Entry);
                    var clip=r.ReplaceTracks?new CHR0Node{Name=r.OutputName??basis?.Name??"NewAnimation",Version=basis?.Version??4,FrameCount=r.OutputSampleCount??basis?.FrameCount??24,Loop=r.OutputLoop??basis?.Loop??false}:basis;
                    if(!r.ReplaceTracks&&r.OutputName!=null)clip.Name=r.OutputName;
                    if(!r.ReplaceTracks&&!r.RewriteChannels&&(clip.FrameCount!=(r.OutputSampleCount??clip.FrameCount)||clip.Loop!=(r.OutputLoop??clip.Loop)))throw new InvalidDataException("Changing native timing requires a new retargeted resource or an explicit timing edit.");
                    if(!r.ReplaceTracks)PreciseChrEntry.Promote(clip);
                    if(r.RewriteChannels&&!r.ReplaceTracks)
                    {
                        // Explicit owned-channel editing is authoritative even
                        // when the result has fewer keys. Preserve native entry
                        // metadata; never leave deleted source keys underneath.
                        foreach(var entry in clip.Children.OfType<CHR0EntryNode>())
                        {foreach(var array in entry.KeyArrays)foreach(var old in NativeKeys(array))array.Remove(old._index);entry.SignalPropertyChange();}
                        clip.FrameCount=r.OutputSampleCount??clip.FrameCount;clip.Loop=r.OutputLoop??clip.Loop;
                    }
                    var semanticFlags=clip.Children.OfType<CHR0EntryNode>().ToDictionary(t=>t.Name,t=>new[]{t.UseModelScale,t.UseModelRotate,t.UseModelTranslate,t.ScaleCompensateApply,t.ScaleCompensateParent,t.ClassicScaleOff});
                    foreach(var e in r.Edits)
                    {
                        if(e.Channel<0 || e.Channel>8 || e.Frame<0 || e.Frame>clip.FrameCount)throw new InvalidDataException("Native edit index outside resource.");
                        var track=clip.Children.OfType<CHR0EntryNode>().SingleOrDefault(t=>t.Name==e.Bone);
                        if(track==null){track=new PreciseChrEntry{Name=e.Bone};clip.AddChild(track);}
                        ApplyExactEdit(track.Keyframes.SetFrameValue(e.Channel,e.Frame,e.Value,false),e);
                        track.SignalPropertyChange();
                    }
                    VerifyDerivedKeys(clip,r.Edits,"Before encode");
                    pending=r.Output+"."+Guid.NewGuid().ToString("N")+".pending"+Path.GetExtension(r.Output);
                    var preserved=r.Operation=="insert"?InsertResource(motionRoot,basis,clip,pending):null;
                    if(r.Operation!="insert"){clip.Export(pending??r.Output);if(r.ReplaceTracks)clip.Dispose();}
                    using(var fresh=NodeFactory.FromFile(null,pending??r.Output))
                    {
                        if(preserved!=null)VerifyPreserved(fresh,preserved);
                        var native=Walk(fresh).OfType<CHR0Node>().Single(n=>r.Operation!="insert"||Identity(n)==Identity(clip));
                        NativeChrFloats.Cache(native);
                        VerifyDerivedKeys(native,r.Edits,"Fresh native reopen");
                        foreach(var track in native.Children.OfType<CHR0EntryNode>())if(semanticFlags.TryGetValue(track.Name,out var flags)&&!flags.SequenceEqual(new[]{track.UseModelScale,track.UseModelRotate,track.UseModelTranslate,track.ScaleCompensateApply,track.ScaleCompensateParent,track.ClassicScaleOff}))throw new InvalidDataException("Native entry semantic flags changed: "+track.Name);
                        var expected=Sample(model,native,r.Samples);
                        var expectedBones=SampleBones(model,native,r.Samples);
                        response=new {Project=Project(model,new[]{native},r.Model,r.Output,mh,Hash(pending??r.Output),r.Operation=="insert"?fresh:null),Diagnostics=new[]{"Fresh native CHR0 reopened", "Sample zero = ApplyCHR(1)",r.Operation=="insert"?"New container reopened; unrelated native resource hashes verified; original unchanged":"Standalone resource only; source archives unchanged"},ExpectedVertices=expected,ExpectedBonePositions=expectedBones,PreservedResources=preserved};
                    }
                }
                else if(r.Operation=="save-motion")response=SaveMotion(model,motionRoot,r,mh);
                else if(r.Operation=="sample-morph")
                {
                    var clip=clips.Single(c=>Identity(c)==r.Entry);var shape=Walk(motionRoot).OfType<SHP0Node>().Single(s=>Identity(s)==r.MorphEntry);
                    response=MorphSamples(model,clip,shape,r);
                }
                else
                {
                    var selected=r.Entry==null?clips:clips.Where(c=>Identity(c)==r.Entry).ToArray();
                    response=new {Project=Project(model,selected,r.Model,r.Motion,mh,ch,motionRoot),Diagnostics=Diagnostics.ToArray(),ExpectedVertices=r.Samples.Length==0?null:Sample(model,selected[0],r.Samples),ExpectedBonePositions=r.Samples.Length==0?null:SampleBones(model,selected[0],r.Samples)};
                }
                if(Hash(r.Model)!=mh || (!modelOnly&&Hash(r.Motion)!=ch))throw new IOException("Source integrity check failed.");
                if(pending!=null){File.Move(pending,r.Output);pending=null;}
                File.WriteAllText(args[1],Json.Serialize(response),new UTF8Encoding(false));
            }
            return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
        finally{if(pending!=null&&File.Exists(pending))File.Delete(pending);}
    }
    static bool Same(string a,string b)=>string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);
    static int FloatBits(float value)=>*(int*)&value;
    static float FloatValue(int bits)=>*(float*)&bits;
    static void ApplyExactEdit(BrawlLib.Wii.Animations.KeyframeEntry key,Edit edit)
    {
        key._value=edit.ValueBits.HasValue?FloatValue(edit.ValueBits.Value):edit.Value;
        key._tangent=edit.TangentBits.HasValue?FloatValue(edit.TangentBits.Value):edit.Tangent;
    }
    static void VerifyDerivedKeys(CHR0Node clip,Edit[] edits,string stage)
    {
        var tracks=clip.Children.OfType<CHR0EntryNode>().ToDictionary(t=>t.Name);
        foreach(var e in edits){var key=tracks[e.Bone].Keyframes.GetKeyframe(e.Channel,e.Frame);if(key==null||FloatBits(key._value)!=(e.ValueBits??FloatBits(e.Value))||FloatBits(key._tangent)!=(e.TangentBits??FloatBits(e.Tangent)))throw new InvalidDataException(stage+": exact derived key bits changed at "+e.Bone+" channel "+e.Channel+" sample "+e.Frame+". Output not committed.");}
    }
    static string Hash(string path){using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","");}
    static string StableId(string identity){using(var h=SHA256.Create())return new Guid(h.ComputeHash(Encoding.UTF8.GetBytes(identity)).Take(16).ToArray()).ToString();}
    static IEnumerable<ResourceNode> Walk(ResourceNode n){yield return n;foreach(var c in n.Children)foreach(var d in Walk(c))yield return d;}
    static string Identity(ResourceNode n){var parts=new List<string>();while(n.Parent!=null){parts.Add(n.Index.ToString());n=n.Parent;}parts.Reverse();return "root"+(parts.Count==0?"":"/"+string.Join("/",parts));}
    static object V(Vector3 v)=>new {X=v._x,Y=v._y,Z=v._z};
    static object V4(float a,float b,float c,float d)=>new{X=a,Y=b,Z=c,W=d};
    static object Rest(MDL0BoneNode b)=>new{Scale=V(b.Scale),Rotation=V(b.Rotation),Translation=V(b.Translation)};
    static MDL0BoneNode[] OrderedBones(MDL0Node m)
    {
        var result=new List<MDL0BoneNode>();
        Action<MDL0BoneNode> add=null;add=b=>{if(result.Contains(b))return;if(b.Parent is MDL0BoneNode p)add(p);result.Add(b);};
        foreach(var bone in m.AllBones)add(bone);return result.ToArray();
    }
    static object Project(MDL0Node model,CHR0Node[] clips,string modelPath,string motionPath,string modelHash,string motionHash,ResourceNode motionRoot=null)
    {
        var bones=OrderedBones(model);if(bones.Length>128)throw new NotSupportedException("More than 128 bones requires a larger GPU palette.");
        var rig=StableId(modelHash+"|"+Identity(model));model.ApplySHP(null,0);model.ApplyCHR(null,0);
        var meshes=model.PolygonList.Cast<MDL0ObjectNode>().Select(o=>Mesh(o,bones)).ToArray();
        var modelRef=new{Path=Path.GetFullPath(modelPath),Entry=Identity(model),Sha256=modelHash,Format="MDL0",Version=model.Version};
        ResourceNode modelRoot=model;while(modelRoot.Parent!=null)modelRoot=modelRoot.Parent;
        var resources=Walk(modelRoot).Select(n=>new{Name=n.Name,Resource=Resource(n,modelPath,modelHash),NodeType=n.GetType().Name}).Concat(motionRoot==null?Enumerable.Empty<object>():Walk(motionRoot).Select(n=>(object)new{Name=n.Name,Resource=Resource(n,motionPath,motionHash),NodeType=n.GetType().Name})).ToArray();
        var layers=motionRoot==null?new object[0]:Walk(motionRoot).Where(n=>n is NW4RAnimationNode && !(n is CHR0Node)).Select(n=>Layer((NW4RAnimationNode)n,motionPath,motionHash,model,modelPath,modelHash)).ToArray();
        var textures=TextureImages(modelRoot,motionRoot,model);
        var materials=model.MaterialList==null?new object[0]:model.MaterialList.Cast<MDL0MaterialNode>().Select(m=>Material(m,modelPath,modelHash)).ToArray();
        var originalSkeleton=bones.Select(b=>new{Name=b.Name,Parent=b.Parent is MDL0BoneNode p?Array.IndexOf(bones,p):-1,Rest=Rest(b)}).ToArray();
        return new{SchemaVersion=1,Id=Guid.NewGuid().ToString(),Name=model.Name,NativeResources=resources,NativeLayers=layers,SourceModels=new[]{new{Id=rig,Name=model.Name,Resource=modelRef,OriginalSkeleton=originalSkeleton,CoordinateConvention="Native Brawl model coordinates; preserved without conversion"}},Scene=new{Name=model.Name,SkeletonRevision=rig,Materials=materials,Textures=textures,
            Bones=bones.Select(b=>new{Name=b.Name,Parent=b.Parent is MDL0BoneNode p?Array.IndexOf(bones,p):-1,Rest=Rest(b)}).ToArray(),Meshes=meshes},
            Clips=clips.Select(c=>new{Id=Guid.NewGuid().ToString(),Revision=Guid.NewGuid().ToString(),Name=c.Name+" ["+Identity(c)+"]",SampleCount=c.FrameCount,Rate=new{Numerator=60,Denominator=1},Loop=c.Loop,
                Source=new{Id=StableId(motionHash+"|"+Identity(c)+"|"+rig),NativeName=c.Name,Resource=new{Path=Path.GetFullPath(motionPath),Entry=Identity(c),ContainerEntry=Container(c),Sha256=motionHash,Format="CHR0",Version=c.Version},Model=modelRef,SkeletonRevision=rig,SourceModelRevisionId=rig,AssociationEvidence="Explicitly paired model/motion inputs; immutable model identity retained; ownership review pending",Timebase=new{Numerator=60,Denominator=1}},
                Tracks=c.Children.OfType<CHR0EntryNode>().Select(t=>new{Bone=t.Name,Channels=Enumerable.Range(0,9).Select(i=>Curve(t,i)).ToArray()}).ToArray()}).ToArray()};
    }
    static object Curve(CHR0EntryNode t,int channel)
    {
        return Curve(t.KeyArrays[channel]);
    }
    static object Curve(BrawlLib.Wii.Animations.KeyframeArray a)
    {
        int count=(int)typeof(BrawlLib.Wii.Animations.KeyframeArray).GetField("_keyCount",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(a);if(count==0)return null;
        var root=(BrawlLib.Wii.Animations.KeyframeEntry)typeof(BrawlLib.Wii.Animations.KeyframeArray).GetField("_keyRoot",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(a);
        var keys=new List<object>();var k=root._next;
        for(int j=0;j<count;j++,k=k._next)keys.Add(new{Frame=k._index,Value=k._value,InTangent=k._tangent,OutTangent=k._tangent,ValueBits=FloatBits(k._value),InTangentBits=FloatBits(k._tangent),OutTangentBits=FloatBits(k._tangent)});
        return new{Keys=keys.ToArray(),Interpolation="Hermite",LoopFrameLimit=a.Loop?(int?)a.FrameLimit:null};
    }
    static string Container(ResourceNode node){var owner=node.Parent??node;while(!(owner is BRRESNode)&&owner.Parent!=null)owner=owner.Parent;return Identity(owner);}
    static object Resource(ResourceNode node,string path,string hash)=>new{Path=Path.GetFullPath(path),Entry=Identity(node),ContainerEntry=Container(node),Sha256=hash,Format=node.ResourceFileType.ToString(),Version=node.GetType().GetProperty("Version")?.GetValue(node,null) is int version?version:0};
    static object Layer(NW4RAnimationNode node,string path,string hash,MDL0Node model,string modelPath,string modelHash)
    {
        string kind=node.GetType().Name.Replace("Node","");var tracks=new List<object>();string status="Loaded; preview not yet implemented";
        if(node is SRT0Node srt)
        {
            foreach(var mat in srt.Children)foreach(var t in mat.Children.OfType<SRT0TextureNode>())
                if(!t._indirect)tracks.Add(new{Target=mat.Name,Slot=t._textureIndex,Curves=Enumerable.Range(0,5).Select(i=>Curve(t.KeyArrays[i])).ToArray()});
            status="Native Maya/XSI/Max direct texture transforms; GX indirect stages pending";
        }
        if(node is VIS0Node vis)
        {
            foreach(var t in vis.Children.OfType<VIS0EntryNode>())tracks.Add(new{Target=t.Name,Slot=0,Curves=new object[0],Visibility=Enumerable.Range(0,vis.FrameCount).Select(i=>t.Constant?t.Enabled:t.GetEntry(i)).ToArray()});
            status="Experimental visibility sampling";
        }
        if(node is CLR0Node clr)
        {
            foreach(var mat in clr.Children)foreach(var entry in mat.Children.OfType<CLR0MaterialEntryNode>())
            {
                var colors=entry.Constant?new[]{entry.SolidColor}:entry.Colors.ToArray();
                var mask=entry.ColorMask;uint packed=(uint)(mask.A<<24|mask.R<<16|mask.G<<8|mask.B);
                tracks.Add(new{Target=mat.Name,Slot=(int)entry.Target,ColorMask=packed,Curves=Enumerable.Range(0,4).Select(ch=>(object)new{Keys=colors.Select((c,i)=>new{Frame=i,Value=ch==0?c.R:ch==1?c.G:ch==2?c.B:c.A,InTangent=0,OutTangent=0}).ToArray(),Interpolation="Step"}).ToArray()});
            }
            status="Native stepped masked material/register colors";
        }
        if(node is PAT0Node pat)
        {
            foreach(var mat in pat.Children)foreach(var texture in mat.Children.OfType<PAT0TextureNode>())tracks.Add(new{Target=mat.Name,Slot=texture.TextureIndex,Curves=new object[0],TextureSwaps=texture.Children.OfType<PAT0TextureEntryNode>().Select(key=>new{Frame=key.FrameIndex,Texture=key.Texture,Palette=key.Palette}).ToArray()});
            status="Native held texture/palette bindings; referenced images must resolve";
        }
        if(node is SHP0Node decoded)foreach(var entry in decoded.Children.OfType<SHP0EntryNode>())foreach(var set in entry.Children.OfType<SHP0VertexSetNode>())tracks.Add(new{Target=entry.Name,Slot=set.Index,MorphTarget=set.Name,NativeFlags=(uint)entry._flags,Native=Resource(set,path,hash),Curves=new[]{Curve(set.Keyframes)}});
        var morph=node is SHP0Node shape?BuildMorphs(model,shape,modelPath,modelHash,path,hash):new MorphData();
        if(node is SHP0Node)status="Prepared vertex/normal/color morphs: "+morph.Bindings.Count+" bindings"+(morph.Issues.Count==0?"":"; "+string.Join("; ",morph.Issues.Select(d=>d.Message)));
        return new{Id=Guid.NewGuid().ToString(),Name=node.Name,Kind=kind,SampleCount=node.FrameCount,Loop=node.Loop,Rate=new{Numerator=60,Denominator=1},Resource=Resource(node,path,hash),Tracks=tracks.ToArray(),Morphs=morph.Bindings.ToArray(),Diagnostics=morph.Issues.ToArray(),PreviewStatus=status,MatrixMode=node is SRT0Node transform?(int)transform.MatrixMode:0};
    }
    static object Texture(TEX0Node node,PLT0Node palette=null,string alias=null)
    {
        try
        {
            using(var bitmap=palette==null?node.GetImage(0):node.GetImage(0,palette))
            {
                if(bitmap==null)return null;
                if(bitmap.Width>4096 || bitmap.Height>4096)throw new NotSupportedException("Texture budget exceeded.");
                var rgba=new byte[bitmap.Width*bitmap.Height*4];var data=bitmap.LockBits(new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                try{var row=new byte[bitmap.Width*4];for(int y=0;y<bitmap.Height;y++){Marshal.Copy(data.Scan0+y*data.Stride,row,0,row.Length);for(int x=0;x<bitmap.Width;x++){int from=x*4,to=(y*bitmap.Width+x)*4;rgba[to]=row[from+2];rgba[to+1]=row[from+1];rgba[to+2]=row[from];rgba[to+3]=row[from+3];}}}finally{bitmap.UnlockBits(data);}
                return new{Name=alias??node.Name,Width=bitmap.Width,Height=bitmap.Height,Rgba=Convert.ToBase64String(rgba)};
            }
        }
        catch(Exception e){Diagnostics.Add("Texture decode unavailable: "+node.Name+" / "+e.Message);return null;}
    }
    static object Mesh(MDL0ObjectNode obj,MDL0BoneNode[] bones)
    {
        var manager=obj._manager;var primitive=(GLPrimitive)typeof(PrimitiveManager).GetField("_triangles",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(manager);
        var faceToVertex=(ushort*)manager._indices.Address;
        var vertices=new List<object>();
        for(int point=0;point<manager._pointCount;point++)
        {
            var v=obj.Vertices[faceToVertex[point]];var weights=v.GetBoneWeights();
            if(weights==null || weights.Count==0)throw new InvalidDataException("Unbound geometry needs an explicit rigid transform.");
            if(weights.Count>4)throw new NotSupportedException("More than four influences per vertex requires an explicit loss policy.");
            float[] joints=new float[4],values=new float[4];
            for(int j=0;j<weights.Count;j++){joints[j]=Array.IndexOf(bones,weights[j].Bone);values[j]=weights[j].Weight;}
            var normal=manager._faceData[1]==null?new Vector3(0,1,0):((Vector3*)manager._faceData[1].Address)[point];
            normal=normal*v.GetMatrix().GetRotationMatrix();
            var uv=manager._faceData[4]==null?new Vector2():((Vector2*)manager._faceData[4].Address)[point];
            var color=manager._faceData[2]==null?new BrawlLib.Imaging.RGBAPixel(255,255,255,255):((BrawlLib.Imaging.RGBAPixel*)manager._faceData[2].Address)[point];
            var color1=manager._faceData[3]==null?new BrawlLib.Imaging.RGBAPixel(255,255,255,255):((BrawlLib.Imaging.RGBAPixel*)manager._faceData[3].Address)[point];
            var uvs=Enumerable.Range(0,8).Select(i=>manager._faceData[i+4]==null?new Vector2():((Vector2*)manager._faceData[i+4].Address)[point]).Select(t=>new{X=t._x,Y=t._y}).ToArray();
            vertices.Add(new {Position=V(v.WeightedPosition),Normal=V(normal),UV=new{X=uv._x,Y=uv._y},UVSets=uvs,Joints=V4(joints[0],joints[1],joints[2],joints[3]),Weights=V4(values[0],values[1],values[2],values[3]),Color=V4(color.R/255f,color.G/255f,color.B/255f,color.A/255f),Color1=V4(color1.R/255f,color1.G/255f,color1.B/255f,color1.A/255f)});
        }
        return new{Name=obj.Name,Vertices=vertices.ToArray(),Indices=primitive?._indices??new uint[0],Material=obj.DrawCalls.OfType<DrawCall>().FirstOrDefault()?.MaterialNode?.Name,VisibilityBone=obj.DrawCalls.OfType<DrawCall>().FirstOrDefault()?.VisibilityBoneNode?.Name,
            DrawCalls=obj.DrawCalls.OfType<DrawCall>().Select(d=>{d.Bind();return new{Material=d.MaterialNode?.Name,VisibilityBone=d.VisibilityBoneNode?.Name,DefaultVisible=d._render,Pass=(int)d.DrawPass,Priority=(int)d.DrawPriority,MaterialIndex=d.MaterialNode?.Index??-1};}).ToArray()};
    }
    static float[][][] Sample(MDL0Node model,CHR0Node clip,double[] times)
    {
        return times.Select(time=>{
            model.ApplyCHR(clip,(float)time+1);
            return model.PolygonList.Cast<MDL0ObjectNode>().SelectMany(o=>{
                var map=(ushort*)o._manager._indices.Address;
                var result=new List<float[]>();for(int i=0;i<o._manager._pointCount;i++){var v=o.Vertices[map[i]].WeightedPosition;result.Add(new[]{v._x,v._y,v._z});}return result;
            }).ToArray();
        }).ToArray();
    }
    static float[][][] SampleBones(MDL0Node model,CHR0Node clip,double[] times)
    {
        var bones=OrderedBones(model);
        return times.Select(time=>{model.ApplyCHR(clip,(float)time+1);return bones.Select(b=>{var p=b.Matrix.GetPoint();return new[]{p._x,p._y,p._z};}).ToArray();}).ToArray();
    }
    static void Fixture(Request r)
    {
        if(File.Exists(r.Output)||File.Exists(r.Motion))throw new IOException("Fixture outputs already exist.");
        Collada._importOptions=new Collada.ImportOptions{ModelVersion=9,BlenderFBXBoneFix=false,SetOriginalPath=false,UseTristrips=false};
        using(var importer=new Collada())using(var model=importer.ImportModel(r.Model,Collada.ImportType.MDL0) as MDL0Node){if(model==null)throw new InvalidDataException("Fixture DAE import failed.");model.Export(r.Output);}
        using(var clip=new CHR0Node{Name="Original24",FrameCount=24,Loop=false})
        {var root=clip.CreateEntry("Root");root.Keyframes.SetFrameValue(6,0,0,true)._tangent=.05f;root.Keyframes.SetFrameValue(6,23,1.15f,true)._tangent=.05f;
         var arm=clip.CreateEntry("Arm.R");arm.Keyframes.SetFrameValue(5,0,0,true)._tangent=4;arm.Keyframes.SetFrameValue(5,23,92,true)._tangent=4;clip.Export(r.Motion);}
        using(var container=new BRRESNode{Name="OriginalMotion"}){container.GetOrCreateFolder<CHR0Node>().AddChild(NodeFactory.FromFile(null,r.Motion));container.Export(r.Output+".motion.brres");}
    }
}
