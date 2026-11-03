using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;

namespace BrawlCrate.Studio
{
    // A glTF skin is qualified only when BT3's two native pivot evaluations
    // converge to one bind-space point for each blended vertex. Otherwise its
    // native deformation cannot be represented by ordinary linear skinning.
    internal static class PortableBt3Rigged
    {
        private static readonly XNamespace Ns="http://www.collada.org/2005/11/COLLADASchema";
        private static XElement E(string name,params object[] content)=>new XElement(Ns+name,content);
        private static XAttribute A(string name,object value)=>new XAttribute(name,value);
        private static string F(float value)
        {
            if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Nonfinite BT3 portable value.");
            return value.ToString("R",CultureInfo.InvariantCulture);
        }
        private static string Join(IEnumerable<float> values)=>string.Join(" ",values.Select(F));
        private static string Name(int id)=>"BT3_J"+id.ToString("D4",CultureInfo.InvariantCulture);
        private static float[] Matrix(Matrix4x4 m)=>new[]{m.M11,m.M21,m.M31,m.M41,m.M12,m.M22,m.M32,m.M42,
            m.M13,m.M23,m.M33,m.M43,m.M14,m.M24,m.M34,m.M44};
        private static XElement FloatSource(string id,float[] values,int stride,params string[] names)
            =>E("source",A("id",id),
                E("float_array",A("id",id+"-array"),A("count",values.Length),Join(values)),
                E("technique_common",E("accessor",A("source","#"+id+"-array"),
                    A("count",values.Length/stride),A("stride",stride),
                    names.Select(name=>E("param",A("name",name),A("type",stride==16?"float4x4":"float"))))));
        private static Vector3 Invert(Vector3 value)=>new Vector3(-value.X,-value.Y,-value.Z);
        internal static bool CanEncodeAction(Bt3NativeAssetReader.Motion motion,out string reason)
        {
            reason=null;
            if(motion?.Tracks==null||motion.Tracks.Length==0||motion.Length<0)
            {reason="The selected BT3 action is unavailable.";return false;}
            if(motion.Tracks.Select(track=>track.Bone).Distinct().Count()!=motion.Tracks.Length||
                motion.Tracks.Any(track=>track.Keys==null||track.Keys.Length==0||
                    track.Keys.Any(key=>key.Frame<0||key.Frame>motion.Length||
                        float.IsNaN(key.Translation.X)||float.IsNaN(key.Translation.Y)||float.IsNaN(key.Translation.Z)||
                        float.IsNaN(key.Rotation.X)||float.IsNaN(key.Rotation.Y)||float.IsNaN(key.Rotation.Z)||
                        float.IsNaN(key.Rotation.W)||float.IsInfinity(key.Translation.X)||
                        float.IsInfinity(key.Translation.Y)||float.IsInfinity(key.Translation.Z)||
                        float.IsInfinity(key.Rotation.X)||float.IsInfinity(key.Rotation.Y)||
                        float.IsInfinity(key.Rotation.Z)||float.IsInfinity(key.Rotation.W))||
                    track.Keys.Zip(track.Keys.Skip(1),(left,right)=>right.Frame>left.Frame).Any(ok=>!ok)))
            {reason="The BT3 action has duplicate joints, invalid values or non-increasing native key times.";return false;}
            if(motion.Tracks.Any(track=>track.Keys==null||track.Keys.Length==0||
                track.Keys.Length>1&&(track.Keys[0].Frame!=0||
                track.Keys[track.Keys.Length-1].Frame!=motion.Length)))
            {
                reason="One or more BT3 tracks use a native out-of-range fallback that glTF LINEAR channels cannot reproduce exactly.";
                return false;
            }
            return true;
        }
        internal static PortableRiggedGltf.Clip ToClip(Bt3NativeAssetReader.Motion motion)
        {
            if(!CanEncodeAction(motion,out string reason))throw new InvalidDataException(reason);
            return new PortableRiggedGltf.Clip{Name="Action-"+motion.Action.ToString("D3",CultureInfo.InvariantCulture),
                Identity="BT3-action-"+motion.Action.ToString("D3",CultureInfo.InvariantCulture)+"-"+motion.SourceSha256,
                FramesPerSecond=60,
                Tracks=motion.Tracks.Select(track=>new PortableRiggedGltf.Track{
                    Bone=Name(track.Bone),Frames=track.Keys.Select(key=>(float)key.Frame).ToArray(),
                    Translation=track.RotationOnly?null:track.Keys.Select(key=>Invert(key.Translation)).ToArray(),
                    Rotation=track.Keys.Select(key=>key.Rotation).ToArray()}).ToArray()};
        }
        internal static bool TryWrite(Bt3NativeMesh.Scene model,Bt3NativeAssetReader.Motion motion,string stage,out string reason)
        {
            reason=null;
            if(model?.Skeleton?.Bones==null||model.Skeleton.Bones.Length==0)
            {reason="No verified BT3 skeleton was available.";return false;}
            var bones=model.Skeleton.Bones;
            var byId=bones.ToDictionary(b=>b.Id);
            var world=new Dictionary<int,Matrix4x4>();
            var displayWorld=new Dictionary<int,Matrix4x4>();
            foreach(var bone in bones)
            {
                if(bone.Parent>=0&&!world.ContainsKey(bone.Parent))
                    throw new InvalidDataException("BT3 skeleton is not ordered parent before child.");
                var local=Matrix4x4.CreateTranslation(bone.Rest);
                world.Add(bone.Id,bone.Parent<0?local:local*world[bone.Parent]);
                var displayed=Matrix4x4.CreateTranslation(Invert(bone.Rest));
                displayWorld.Add(bone.Id,bone.Parent<0?displayed:displayed*displayWorld[bone.Parent]);
            }
            if(motion!=null&&!CanEncodeAction(motion,out reason))
            {
                return false;
            }
            var bindPositions=new Dictionary<Bt3NativeMesh.Vertex,Vector3>();
            var bindNormals=new Dictionary<Bt3NativeMesh.Vertex,Vector3>();
            double maximumGap=0;
            foreach(var batch in model.Batches)foreach(var vertex in batch.Triangles)
            {
                int id=vertex.Bone<0?batch.Bone:vertex.Bone;
                if(!byId.TryGetValue(id,out var bone))throw new InvalidDataException("BT3 mesh references an absent joint.");
                var parent=bone.Parent;
                Matrix4x4 a=world[id],b=parent<0?Matrix4x4.Identity:world[parent];
                var pivotA=vertex.Bone<0?batch.PivotA:model.Pivots[id].A;
                var pivotB=vertex.Bone<0?batch.PivotB:model.Pivots[id].B;
                var pa=Vector3.Transform(vertex.Position-pivotA,a);
                var pb=Vector3.Transform(vertex.Position-pivotB,b);
                if(vertex.Weight>0&&vertex.Weight<1)
                    maximumGap=Math.Max(maximumGap,Vector3.Distance(pa,pb));
                var position=Invert(Vector3.Lerp(pb,pa,vertex.Weight));
                var normal=Invert(Vector3.Lerp(Vector3.TransformNormal(vertex.Normal,b),
                    Vector3.TransformNormal(vertex.Normal,a),vertex.Weight));
                if(normal.LengthSquared()<1e-12f)throw new InvalidDataException("BT3 skinned vertex has no finite normal.");
                bindPositions[vertex]=position;bindNormals[vertex]=Vector3.Normalize(normal);
            }
            if(maximumGap>0.0001)
            {
                reason="Native BT3 pivot influences disagree at bind pose by "+maximumGap.ToString("R",CultureInfo.InvariantCulture)+
                    " units; the exported pose remains static until this skin is reviewed.";
                return false;
            }
            // Each native bone is retained, including helpers and fingers. The
            // identity model root represents a root's parent influence.
            var names=new[]{"BT3_ModelRoot"}.Concat(bones.Select(bone=>Name(bone.Id))).ToArray();
            var jointIndex=bones.Select((bone,index)=>new{bone.Id,Index=index+1}).ToDictionary(x=>x.Id,x=>x.Index);
            var inverse=new List<float>(names.Length*16);inverse.AddRange(Matrix(Matrix4x4.Identity));
            foreach(var bone in bones)
            {
                if(!Matrix4x4.Invert(displayWorld[bone.Id],out var inv))
                    throw new InvalidDataException("BT3 portable bind matrix is singular.");
                inverse.AddRange(Matrix(inv));
            }
            var root=E("COLLADA",A("version","1.4.1"));var document=new XDocument(root);
            root.Add(E("asset",E("contributor",E("authoring_tool","Fighterport BT3 rigged projection")),
                E("unit",A("name","viewport unit"),A("meter",1)),E("up_axis","Y_UP")));
            var images=E("library_images");var effects=E("library_effects");
            var materials=E("library_materials");var geometries=E("library_geometries");
            var controllers=E("library_controllers");var visual=E("visual_scene",A("id","Scene"));
            var modelRoot=E("node",A("id","BT3_ModelRoot"),A("sid","BT3_ModelRoot"),
                A("type","JOINT"));visual.Add(modelRoot);
            var nodes=new Dictionary<int,XElement>();
            foreach(var bone in bones)
            {
                var node=E("node",A("id",Name(bone.Id)),A("sid",Name(bone.Id)),
                    A("name",Name(bone.Id)),A("type","JOINT"),
                    E("matrix",Join(Matrix(Matrix4x4.CreateTranslation(Invert(bone.Rest))))));
                if(bone.Parent<0)modelRoot.Add(node);else nodes[bone.Parent].Add(node);
                nodes.Add(bone.Id,node);
            }
            for(int i=0;i<model.Textures.Length;i++)
            {
                string id="tex"+i,file="Texture-"+i.ToString("D3",CultureInfo.InvariantCulture)+".png";
                if(!File.Exists(Path.Combine(stage,file)))throw new InvalidDataException("BT3 portable image is absent.");
                images.Add(E("image",A("id",id),E("init_from",file)));
                effects.Add(E("effect",A("id",id+"-fx"),E("profile_COMMON",
                    E("newparam",A("sid",id+"-surface"),E("surface",A("type","2D"),E("init_from",id))),
                    E("newparam",A("sid",id+"-sampler"),E("sampler2D",E("source",id+"-surface"))),
                    E("technique",A("sid","common"),E("lambert",E("diffuse",
                        E("texture",A("texture",id+"-sampler"),A("texcoord","UVSET0"))))))));
                materials.Add(E("material",A("id",id+"-material"),
                    E("instance_effect",A("url","#"+id+"-fx"))));
            }
            for(int batchIndex=0;batchIndex<model.Batches.Length;batchIndex++)
            {
                var batch=model.Batches[batchIndex];var vertices=batch.Triangles;
                if(vertices.Length<3||vertices.Length%3!=0||vertices.Length>2000000)
                    throw new InvalidDataException("BT3 portable triangle count exceeds its work bound.");
                string id="mesh"+batchIndex,material="tex"+batch.Texture+"-material";
                geometries.Add(E("geometry",A("id",id),E("mesh",
                    FloatSource(id+"-positions",vertices.SelectMany(v=>new[]{
                        bindPositions[v].X,bindPositions[v].Y,bindPositions[v].Z}).ToArray(),3,"X","Y","Z"),
                    FloatSource(id+"-normals",vertices.SelectMany(v=>new[]{
                        bindNormals[v].X,bindNormals[v].Y,bindNormals[v].Z}).ToArray(),3,"X","Y","Z"),
                    FloatSource(id+"-uv",vertices.SelectMany(v=>new[]{v.U,v.V}).ToArray(),2,"S","T"),
                    E("vertices",A("id",id+"-vertices"),
                        E("input",A("semantic","POSITION"),A("source","#"+id+"-positions"))),
                    E("triangles",A("count",vertices.Length/3),A("material",material),
                        E("input",A("semantic","VERTEX"),A("source","#"+id+"-vertices"),A("offset",0)),
                        E("input",A("semantic","NORMAL"),A("source","#"+id+"-normals"),A("offset",1)),
                        E("input",A("semantic","TEXCOORD"),A("source","#"+id+"-uv"),A("offset",2),A("set",0)),
                        E("p",string.Join(" ",Enumerable.Range(0,vertices.Length).SelectMany(v=>new[]{v,v,v})))))));
                var weights=new List<float>();var counts=new List<int>();var pairs=new List<int>();
                foreach(var vertex in vertices)
                {
                    int child=vertex.Bone<0?batch.Bone:vertex.Bone;
                    int parent=byId[child].Parent;
                    int count=0;
                    if(vertex.Weight>0){pairs.Add(jointIndex[child]);pairs.Add(weights.Count);weights.Add(vertex.Weight);count++;}
                    if(vertex.Weight<1){pairs.Add(parent<0?0:jointIndex[parent]);pairs.Add(weights.Count);weights.Add(1-vertex.Weight);count++;}
                    counts.Add(count);
                }
                var skin=E("skin",A("source","#"+id),
                    E("bind_shape_matrix",Join(Matrix(Matrix4x4.Identity))),
                    E("source",A("id",id+"-joints"),
                        E("Name_array",A("id",id+"-names"),A("count",names.Length),string.Join(" ",names)),
                        E("technique_common",E("accessor",A("source","#"+id+"-names"),
                            A("count",names.Length),A("stride",1),
                            E("param",A("name","JOINT"),A("type","Name"))))),
                    FloatSource(id+"-binds",inverse.ToArray(),16,"TRANSFORM"),
                    FloatSource(id+"-weights",weights.ToArray(),1,"WEIGHT"),
                    E("joints",
                        E("input",A("semantic","JOINT"),A("source","#"+id+"-joints")),
                        E("input",A("semantic","INV_BIND_MATRIX"),A("source","#"+id+"-binds"))),
                    E("vertex_weights",A("count",vertices.Length),
                        E("input",A("semantic","JOINT"),A("source","#"+id+"-joints"),A("offset",0)),
                        E("input",A("semantic","WEIGHT"),A("source","#"+id+"-weights"),A("offset",1)),
                        E("vcount",string.Join(" ",counts)),
                        E("v",string.Join(" ",pairs))));
                controllers.Add(E("controller",A("id",id+"-skin"),skin));
                visual.Add(E("node",A("id",id+"-node"),
                    E("instance_controller",A("url","#"+id+"-skin"),
                        E("skeleton","#BT3_ModelRoot"),
                        E("bind_material",E("technique_common",
                            E("instance_material",A("symbol",material),A("target","#"+material)))))));
            }
            root.Add(images,effects,materials,geometries,controllers,
                E("library_visual_scenes",visual),E("scene",E("instance_visual_scene",A("url","#Scene"))));
            document.Save(Path.Combine(stage,"Model.dae"));
            PortableRiggedGltf.Clip clip=motion==null?null:ToClip(motion);
            PortableRiggedGltf.Write(stage,clip);
            reason=clip==null?"Rigged model and all native skin influences exported without animation clips.":
                "Rigged model, all native skin influences and "+clip.Tracks.Length+
                " full-range action tracks exported. Native gameplay events and loop policy remain outside glTF.";
            return true;
        }
    }
}
