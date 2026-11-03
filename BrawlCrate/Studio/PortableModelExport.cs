using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using FighterPort.Adapters.Melee;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Xml.Linq;

namespace BrawlCrate.Studio
{
    // Interchange output is independent of native model writing. The model
    // currently displayed by Brawl/BT3 is never repacked or replaced here.
    internal static class PortableModelExport
    {
        private static readonly XNamespace Ns="http://www.collada.org/2005/11/COLLADASchema";
        private static XElement E(string name,params object[] content)=>new XElement(Ns+name,content);
        private static XAttribute A(string name,object value)=>new XAttribute(name,value);
        private static string Number(float value)
        {
            if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("The model contains a nonfinite export coordinate.");
            return value.ToString("R",CultureInfo.InvariantCulture);
        }
        private static string Numbers(System.Collections.Generic.IEnumerable<float> values)
            =>string.Join(" ",values.Select(Number));
        private static string Hash(string file)
        {using(var stream=File.OpenRead(file))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        private static void RequireSafeTextureName(string name)
        {
            if(string.IsNullOrWhiteSpace(name)||name=="."||name==".."||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||
                name.Contains("/")||name.Contains("\\")||name.EndsWith(".",StringComparison.Ordinal))
                throw new InvalidDataException("A model texture has an unsafe interchange filename.");
        }
        internal static void Publish(string destination,Action<string> prepare,Action validate,CancellationToken cancellation)
        {
            if(prepare==null)throw new ArgumentNullException(nameof(prepare));
            destination=Path.GetFullPath(destination);
            if(File.Exists(destination)||Directory.Exists(destination))throw new IOException("The interchange destination already exists. Choose a new folder.");
            string parent=Path.GetDirectoryName(destination);
            if(!Directory.Exists(parent))throw new DirectoryNotFoundException("The selected export parent no longer exists.");
            string stage=Path.Combine(parent,".fighterport-portable-"+Guid.NewGuid().ToString("N"));
            string parentPrefix=Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string stageFull=Path.GetFullPath(stage);
            if(!stageFull.StartsWith(parentPrefix,StringComparison.OrdinalIgnoreCase)||
                !Path.GetFileName(stageFull).StartsWith(".fighterport-portable-",StringComparison.Ordinal))
                throw new IOException("Invalid portable staging directory.");
            Directory.CreateDirectory(stage);
            try
            {
                cancellation.ThrowIfCancellationRequested();validate?.Invoke();prepare(stage);
                var dae=Path.Combine(stage,"Model.dae");
                if(!File.Exists(dae)||new FileInfo(dae).Length==0)throw new InvalidDataException("The interchange model was not written.");
                XDocument.Load(dae);
                if(File.Exists(Path.Combine(stage,"Model.glb")))PortableGlbExport.Verify(stage);
                foreach(string file in Directory.GetFiles(stage,"*",SearchOption.TopDirectoryOnly))
                    if(new FileInfo(file).Length==0)throw new InvalidDataException("The interchange bundle contains an empty file.");
                cancellation.ThrowIfCancellationRequested();validate?.Invoke();
                if(File.Exists(destination)||Directory.Exists(destination))throw new IOException("The interchange destination appeared during export.");
                Directory.Move(stage,destination);
            }
            finally
            {
                if(Directory.Exists(stageFull))
                {
                    if(!stageFull.StartsWith(parentPrefix,StringComparison.OrdinalIgnoreCase)||
                        (File.GetAttributes(stageFull)&FileAttributes.ReparsePoint)!=0)
                        throw new IOException("Portable staging directory changed ownership.");
                    Directory.Delete(stageFull,true);
                }
            }
        }
        internal static void WriteBrawl(FighterProject project,string exactModelTreePath,string stage)
        {
            if(project?.Model==null)throw new InvalidOperationException("Open a project-owned Brawl model first.");
            using(var root=NodeFactory.FromFile(null,project.Model.WorkingPath))
            {
                var model=ModelPanel.CollectModels(root).OfType<MDL0Node>()
                    .SingleOrDefault(m=>m.TreePath==exactModelTreePath)
                    ??throw new InvalidDataException("The exact saved model member is unavailable.");
                model.Export(Path.Combine(stage,"Model.dae"));
                var names=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var missing=new System.Collections.Generic.List<string>();int decoded=0;
                var embedded=root.GetChildrenRecursive().OfType<TEX0Node>().ToArray();
                foreach(var texture in model.TextureList.OfType<MDL0TextureNode>())
                {
                    RequireSafeTextureName(texture.Name);
                    if(!names.Add(texture.Name))throw new InvalidDataException("Two textures have the same interchange filename.");
                    // MDL0TextureNode.Load is a GL-context path. Resolve the
                    // embedded TEX0 directly so background export can decode
                    // without creating a viewport or touching playback.
                    texture.GetSource();
                    if(texture.Source==null)
                    {
                        var candidates=embedded.Where(image=>string.Equals(image.Name,texture.Name,StringComparison.Ordinal)).ToArray();
                        if(candidates.Length==1)texture.Source=candidates[0];
                        else if(candidates.Length>1)throw new InvalidDataException("The costume has ambiguous embedded images for '"+texture.Name+"'.");
                    }
                    using(var image=texture.GetImage(0))
                    {
                        if(image==null){missing.Add(texture.Name);continue;}
                        image.Save(Path.Combine(stage,texture.Name+".png"),ImageFormat.Png);
                        decoded++;
                    }
                }
                if(missing.Count>0)File.WriteAllLines(Path.Combine(stage,"Missing-textures.txt"),missing.Select(name=>name+".png (referenced by MDL0; no image decoded from the current owned resource)"));
                WriteReceipt(stage,"Brawl",model.Name,"Rigged COLLADA model with "+decoded+"/"+names.Count+
                    " referenced PNG textures decoded. See Missing-textures.txt when present. Native materials and shaders may differ in other tools.");
            }
        }
        internal static void WriteMelee(FighterProject project,string stage)
        {
            if(project?.NativeTarget?.Provider!=MeleeGameSource.NativeProvider)
                throw new InvalidOperationException("Open a native Melee target first.");
            MeleeTargetDocument.Validate(project);
            var model=MeleeTargetDocument.OriginalModel(project);
            var parts=MeleeTargetDocument.Parts(project,model);
            var result=MeleeProjection.Write(model,stage,true,parts);
            if(result.ColladaPath==null||!File.Exists(result.ColladaPath))throw new InvalidDataException("The native Melee projection did not produce a model.");
            // MeleeProjection writes absolute image URIs for its scratch
            // importer. Convert only this independent bundle to relative
            // references before the sibling staging directory is renamed.
            var document=XDocument.Load(result.ColladaPath);
            foreach(var image in document.Descendants().Where(element=>element.Name.LocalName=="image"))
            {
                var source=image.Descendants().SingleOrDefault(element=>element.Name.LocalName=="init_from")
                    ??throw new InvalidDataException("A projected Melee texture has no image source.");
                if(!Uri.TryCreate(source.Value,UriKind.Absolute,out var uri)||!uri.IsFile)
                    throw new InvalidDataException("A projected Melee texture has an invalid staging URI.");
                string file=Path.GetFileName(uri.LocalPath);
                RequireSafeTextureName(file);
                if(!string.Equals(Path.GetFullPath(uri.LocalPath),Path.GetFullPath(Path.Combine(stage,file)),StringComparison.OrdinalIgnoreCase)||
                    !File.Exists(Path.Combine(stage,file)))
                    throw new InvalidDataException("A projected Melee texture is outside its staged bundle.");
                source.Value=file;
            }
            document.Save(Path.Combine(stage,"Model.dae"));
            File.Delete(result.ColladaPath);
            WriteReceipt(stage,"Melee",model.Symbol,"Rigged normal-detail HSD projection with diffuse PNG textures. Native material, visibility and shape animation remain in the source DAT.");
        }
        private static XElement FloatSource(string id,float[] values,int stride,params string[] names)
            =>E("source",A("id",id),E("float_array",A("id",id+"-array"),A("count",values.Length),Numbers(values)),
                E("technique_common",E("accessor",A("source","#"+id+"-array"),A("count",values.Length/stride),A("stride",stride),
                    names.Select(name=>E("param",A("name",name),A("type","float"))))));
        internal static void WriteBt3Snapshot(Bt3NativeMesh.Scene scene,Bt3NativeRender.PosedMesh[] meshes,string stage)
        {
            if(scene==null||meshes==null||meshes.Length==0)throw new InvalidDataException("The BT3 viewport has no evaluated model.");
            var root=E("COLLADA",A("version","1.4.1"));var document=new XDocument(root);
            root.Add(E("asset",E("contributor",E("authoring_tool","Fighterport BT3 viewport export")),
                E("unit",A("name","native viewport unit"),A("meter",1)),E("up_axis","Y_UP")));
            var images=E("library_images");var effects=E("library_effects");
            var materials=E("library_materials");var geometry=E("library_geometries");
            var visual=E("visual_scene",A("id","Scene"));
            for(int i=0;i<scene.Textures.Length;i++)
            {
                var texture=scene.Textures[i];
                if(texture.Width<1||texture.Height<1||(long)texture.Width*texture.Height>16777216||
                    texture.Rgba?.Length!=(long)texture.Width*texture.Height*4)
                    throw new InvalidDataException("BT3 texture dimensions or decoded bytes are invalid.");
                string file="Texture-"+i.ToString("D3",CultureInfo.InvariantCulture)+".png";
                using(var bitmap=new Bitmap(texture.Width,texture.Height,PixelFormat.Format32bppArgb))
                {
                    var bits=bitmap.LockBits(new Rectangle(0,0,texture.Width,texture.Height),ImageLockMode.WriteOnly,bitmap.PixelFormat);
                    try
                    {
                        var row=new byte[texture.Width*4];
                        for(int y=0;y<texture.Height;y++)
                        {
                            for(int x=0;x<texture.Width;x++)
                            {
                                int p=x*4,s=(y*texture.Width+x)*4;
                                row[p]=texture.Rgba[s+2];row[p+1]=texture.Rgba[s+1];row[p+2]=texture.Rgba[s];row[p+3]=texture.Rgba[s+3];
                            }
                            Marshal.Copy(row,0,IntPtr.Add(bits.Scan0,y*bits.Stride),row.Length);
                        }
                    }
                    finally{bitmap.UnlockBits(bits);}
                    bitmap.Save(Path.Combine(stage,file),ImageFormat.Png);
                }
                string id="tex"+i;
                images.Add(E("image",A("id",id),E("init_from",file)));
                effects.Add(E("effect",A("id",id+"-fx"),E("profile_COMMON",
                    E("newparam",A("sid",id+"-surface"),E("surface",A("type","2D"),E("init_from",id))),
                    E("newparam",A("sid",id+"-sampler"),E("sampler2D",E("source",id+"-surface"))),
                    E("technique",A("sid","common"),E("lambert",E("diffuse",E("texture",A("texture",id+"-sampler"),A("texcoord","UVSET0"))))))));
                materials.Add(E("material",A("id",id+"-material"),E("instance_effect",A("url","#"+id+"-fx"))));
            }
            for(int i=0;i<meshes.Length;i++)
            {
                var mesh=meshes[i];int count=mesh.Positions?.Length??0;
                if(count==0||count%3!=0||mesh.Normals?.Length!=count||mesh.Uvs?.Length!=count||mesh.Texture<0||mesh.Texture>=scene.Textures.Length)
                    throw new InvalidDataException("BT3 posed geometry has an incomplete triangle or material.");
                string id="mesh"+i,mat="tex"+mesh.Texture+"-material";
                geometry.Add(E("geometry",A("id",id),E("mesh",
                    FloatSource(id+"-positions",mesh.Positions.SelectMany(v=>new[]{v.X,v.Y,v.Z}).ToArray(),3,"X","Y","Z"),
                    FloatSource(id+"-normals",mesh.Normals.SelectMany(v=>new[]{v.X,v.Y,v.Z}).ToArray(),3,"X","Y","Z"),
                    FloatSource(id+"-uv",mesh.Uvs.SelectMany(v=>new[]{v.X,v.Y}).ToArray(),2,"S","T"),
                    E("vertices",A("id",id+"-vertices"),E("input",A("semantic","POSITION"),A("source","#"+id+"-positions"))),
                    E("triangles",A("count",count/3),A("material",mat),
                        E("input",A("semantic","VERTEX"),A("source","#"+id+"-vertices"),A("offset",0)),
                        E("input",A("semantic","NORMAL"),A("source","#"+id+"-normals"),A("offset",1)),
                        E("input",A("semantic","TEXCOORD"),A("source","#"+id+"-uv"),A("offset",2),A("set",0)),
                        E("p",string.Join(" ",Enumerable.Range(0,count).SelectMany(v=>new[]{v,v,v})))))));
                visual.Add(E("node",A("id",id+"-node"),E("instance_geometry",A("url","#"+id),
                    E("bind_material",E("technique_common",E("instance_material",A("symbol",mat),A("target","#"+mat),
                        E("bind_vertex_input",A("semantic","UVSET0"),A("input_semantic","TEXCOORD"),A("input_set",0))))))));
            }
            root.Add(images,effects,materials,geometry,E("library_visual_scenes",visual),E("scene",E("instance_visual_scene",A("url","#Scene"))));
            document.Save(Path.Combine(stage,"Model.dae"));
            PortableGlbExport.Write(scene,meshes,stage);
            WriteReceipt(stage,"BT3","costume mesh","Current posed viewport mesh and PNG textures in COLLADA, glTF 2.0 and GLB. These are static snapshots: native joints, weights and action curves are not included.");
        }
        private static void WriteReceipt(string stage,string game,string model,string scope)
        {
            var files=Directory.GetFiles(stage,"*",SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName,StringComparer.Ordinal).ToArray();
            File.WriteAllLines(Path.Combine(stage,"Export-info.txt"),new[]{"Fighterport portable model export", "Game: "+game,"Model: "+model,
                "Scope: "+scope,"This interchange bundle is not a game-native model or proof of runtime compatibility."}
                .Concat(files.Select(path=>Path.GetFileName(path)+" SHA-256 "+Hash(path))));
        }
    }
}
