using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrawlLib.Internal;
using BrawlLib.Imaging;
using BrawlLib.Modeling.Collada;
using BrawlLib.SSBB.ResourceNodes;

internal sealed class MorphSampleData
{
    public string[] Diagnostics {get;set;} public float[][][] ExpectedVertices {get;set;} public float[][][] ExpectedNormals {get;set;} public float[][][] ExpectedColors {get;set;} public float[][][] ExpectedColors1 {get;set;}
}
internal static unsafe partial class Program
{
    static void WrapMotionFixture(Request r)
    {
        if(File.Exists(r.Output))throw new IOException("Fixture output already exists.");
        string motionHash=Hash(r.Motion),sourceHash=string.IsNullOrEmpty(r.Model)?null:Hash(r.Model);
        using(var original=string.IsNullOrEmpty(r.Model)?new ARCNode{Name="OriginalMorphPAC"}:NodeFactory.FromFile(null,r.Model))
        using(var group=NodeFactory.FromFile(null,r.Motion))
        {
            var arc=original as ARCNode??throw new InvalidDataException("PAC fixture basis must be an ARC.");var resource=group as BRRESNode??throw new InvalidDataException("Motion fixture must be BRRES.");Walk(arc).ToArray();resource.FileIndex=(short)arc.Children.Count;arc.AddChild(resource);arc.Export(r.Output);resource.Remove();
        }
        if(Hash(r.Motion)!=motionHash||sourceHash!=null&&Hash(r.Model)!=sourceHash)throw new IOException("Fixture source changed.");
    }
    static void MorphFixture(Request r)
    {
        if(File.Exists(r.Output)||File.Exists(r.Motion))throw new IOException("Morph fixture outputs already exist.");
        string original=Hash(r.Model);
        if(r.Operation=="morph-copy")
        {
            using(var root=NodeFactory.FromFile(null,r.Model)){var nodes=Walk(root).ToArray();var model=nodes.OfType<MDL0Node>().OrderByDescending(m=>m.NumVertices).First();WriteMorphFixture(model,r,nodes.Where(n=>(n is TEX0Node||n is PLT0Node)&&n.WorkingSource.Length>0).ToArray());}
        }
        else
        {
            Collada._importOptions=new Collada.ImportOptions{ModelVersion=9,BlenderFBXBoneFix=false,SetOriginalPath=false,UseTristrips=false};
            string prepared=r.Output+".base.mdl0";
            try
            {
                using(var importer=new Collada())using(var model=importer.ImportModel(r.Model,Collada.ImportType.MDL0) as MDL0Node){if(model==null)throw new InvalidDataException("Original morph fixture model failed to import.");model.Export(prepared);}
                using(var root=NodeFactory.FromFile(null,prepared))WriteMorphFixture((MDL0Node)root,r);
            }
            finally{if(File.Exists(prepared))File.Delete(prepared);}
        }
        if(Hash(r.Model)!=original)throw new IOException("Morph fixture modified original source.");
    }
    static void WriteMorphFixture(MDL0Node model,Request r,ResourceNode[] images=null)
    {
        Walk(model).ToArray();
        var objects=model.PolygonList.Cast<MDL0ObjectNode>().ToArray();var pairs=new Dictionary<ResourceNode,string[]>();float size=Math.Max(.01f,model.VertexList.Cast<MDL0VertexNode>().SelectMany(v=>v.Vertices).Max(p=>Math.Abs(p._y))*.16f);
        if(r.Operation=="morph-fixture")foreach(var obj in objects.Where(o=>o._colorSet[0]!=null&&o._colorSet[1]==null))
        {
            obj.SetColors(1,obj.ColorNode0,true);var manager=obj._manager;manager._faceData[3]=new UnsafeBuffer(manager._pointCount*4);
            for(int point=0;point<manager._pointCount;point++)((RGBAPixel*)manager._faceData[3].Address)[point]=((RGBAPixel*)manager._faceData[2].Address)[point];
            foreach(var vertex in obj.Vertices)foreach(var face in vertex.Facepoints)face._colorIndices[1]=face._colorIndices[0];manager._dirty[3]=true;obj._forceRebuild=true;
        }
        string[] Shapes(ResourceNode basis)
        {
            if(pairs.TryGetValue(basis,out var existing))return existing;var names=new[]{basis.Name+"_FP_Expand",basis.Name+"_FP_Compress"};
            for(int target=0;target<2;target++)
            {
                ResourceNode added;
                if(basis is MDL0VertexNode positions)added=new MDL0VertexNode{Name=names[target],ForceFloat=true,Vertices=positions.Vertices.Select((p,i)=>p+new Vector3((target==0?1:-.7f)*size,(i%3-1)*size*.2f,(target==0?.3f:-.2f)*size)).ToArray()};
                else if(basis is MDL0NormalNode normals)added=new MDL0NormalNode{Name=names[target],ForceFloat=true,Normals=normals.Normals.Select(p=>target==0?new Vector3(p._z,p._x,p._y):new Vector3(-p._y,p._z,p._x)).ToArray()};
                else{var colors=(MDL0ColorNode)basis;added=new PreciseMorphColors{Name=names[target],Colors=colors.Colors.Select(p=>target==0?new RGBAPixel(250,60,90,255):new RGBAPixel(30,180,240,180)).ToArray()};}
                basis.Parent.AddChild(added);
            }
            pairs.Add(basis,names);return names;
        }
        SHP0EntryNode Entry(SHP0Node node,string name)
        {
            var existing=node.Children.OfType<SHP0EntryNode>().FirstOrDefault(e=>e.Name==name);if(existing!=null)return existing;
            var created=node.FindOrCreateEntry(name);foreach(var placeholder in created.Children.ToArray())placeholder.Remove();return created;
        }
        using(var motion=new BRRESNode{Name="OriginalMorphMotion"})
        {
            var chr=new CHR0Node{Name="OriginalMorph",Version=4,FrameCount=120,Loop=true};motion.GetOrCreateFolder<CHR0Node>().AddChild(chr);var bones=OrderedBones(model);var root=chr.CreateEntry(bones[0].Name);root.Keyframes.SetFrameValue(6,0,0,true)._tangent=0;root.Keyframes.SetFrameValue(6,60,size,true)._tangent=0;root.Keyframes.SetFrameValue(6,120,0,true)._tangent=0;
            var moving=bones.FirstOrDefault(b=>b.Name=="Arm.R"||b.Name=="LShoulderJ")??bones.Last();var limb=chr.CreateEntry(moving.Name);limb.Keyframes.SetFrameValue(5,0,moving.Rotation._z,true)._tangent=0;limb.Keyframes.SetFrameValue(5,60,moving.Rotation._z+45,true)._tangent=0;limb.Keyframes.SetFrameValue(5,120,moving.Rotation._z,true)._tangent=0;
            var shp=new SHP0Node{Name="OriginalMorph",Version=r.OutputSampleCount==4?4:3,FrameCount=60,Loop=true};motion.GetOrCreateFolder<SHP0Node>().AddChild(shp);
            foreach(var obj in objects)foreach(var basis in new ResourceNode[]{obj._vertexNode,obj._normalNode,obj._colorSet[0],obj._colorSet[1]}.Where(n=>n!=null))
            {
                var entry=Entry(shp,basis.Name);entry.Enabled=true;entry.UpdateVertices|=basis is MDL0VertexNode;entry.UpdateNormals|=basis is MDL0NormalNode;entry.UpdateColors|=basis is MDL0ColorNode;
                var names=Shapes(basis);for(int t=0;t<2;t++){var set=entry.FindOrCreateEntry(names[t]);foreach(var key in new[]{new[]{0f,0f},new[]{15f,t==0?.8f:.25f},new[]{30f,t==0?-.15f:.5f},new[]{60f,0f}})set.Keyframes.SetFrameValue((int)key[0],key[1])._tangent=0;}
            }
            // A disabled real entry must retain its flags/curves without affecting the mesh.
            if(shp.Children.Count>0)shp.Children.OfType<SHP0EntryNode>().Single(e=>e.Name==objects.Last().VertexNode).Enabled=false;
            shp.Children.OfType<SHP0EntryNode>().First(e=>e.Enabled).FindOrCreateEntry("Unused_Zero_Target").Keyframes.SetFrameValue(0,0);
            var missing=new SHP0Node{Name="MissingTarget",Version=3,FrameCount=60};var bad=Entry(missing,objects[0].VertexNode);bad.FindOrCreateEntry("Unresolved_Original_Array").Keyframes.SetFrameValue(0,.5f);motion.GetOrCreateFolder<SHP0Node>().AddChild(missing);
            var absent=new SHP0Node{Name="MissingBase",Version=3,FrameCount=60};Entry(absent,"Absent_Original_Base").FindOrCreateEntry("Absent_Original_Target").Keyframes.SetFrameValue(0,.5f);motion.GetOrCreateFolder<SHP0Node>().AddChild(absent);
            var srt=new SRT0Node{Name="OriginalMorph",FrameCount=60,Loop=true};motion.GetOrCreateFolder<SRT0Node>().AddChild(srt);if(model.MaterialList.Count>0){var texture=srt.FindOrCreateEntry(model.MaterialList[0].Name,0,false);texture.SetKeyframe(3,0,0);texture.SetKeyframe(3,30,.4f);texture.SetKeyframe(3,60,0);}
            var vis=new VIS0Node{Name="OriginalMorph",FrameCount=120,Loop=true};motion.GetOrCreateFolder<VIS0Node>().AddChild(vis);var draw=objects.SelectMany(o=>o.DrawCalls.Cast<DrawCall>()).FirstOrDefault(d=>d.VisibilityBoneNode!=null);if(draw!=null){var visible=vis.CreateEntry();visible.Name=draw.VisibilityBoneNode.Name;visible.Constant=false;for(int i=0;i<120;i++)visible.SetEntry(i,i<60||i>=90);}
            var scn=new SCN0Node{Name="OriginalMorph",FrameCount=120,Loop=true};motion.GetOrCreateFolder<SCN0Node>().AddChild(scn);
            foreach(var array in model.VertexList.OfType<MDL0VertexNode>())array.ForceFloat=true;
            if(r.Operation=="morph-copy")
            {
                // Keep authentic texture/palette resources with the authored model in this NEW private fixture.
                using(var package=new BRRESNode{Name="AuthoredMorphModel"})
                {
                    model.Remove();package.GetOrCreateFolder<MDL0Node>().AddChild(model);
                    foreach(var image in images){image.Remove();if(image is TEX0Node)package.GetOrCreateFolder<TEX0Node>().AddChild(image);else package.GetOrCreateFolder<PLT0Node>().AddChild(image);}
                    package.Export(r.Output);
                }
            }
            else model.Export(r.Output);
            motion.Export(r.Motion);
        }
    }
    static MorphSampleData MorphSamples(MDL0Node model,CHR0Node clip,SHP0Node shape,Request r)
    {
        var positions=new List<float[][]>();var normals=new List<float[][]>();var colors=new List<float[][]>();var colors1=new List<float[][]>();
        foreach(double time in r.Samples)
        {
            model.ApplySHP(null,0);model.ApplyCHR(clip,(float)time+1);
            double frame=time/60*60;frame=shape.Loop?frame%shape.FrameCount:Math.Min(frame,shape.FrameCount-1);model.ApplySHP(shape,(float)frame+1);
            var ps=new List<float[]>();var ns=new List<float[]>();var cs=new List<float[]>();var ds=new List<float[]>();
            foreach(var obj in model.PolygonList.Cast<MDL0ObjectNode>())
            {
                var manager=obj._manager;var map=(ushort*)manager._indices.Address;
                for(int i=0;i<manager._pointCount;i++)
                {
                    var vertex=obj.Vertices[map[i]];var p=vertex.WeightedPosition;ps.Add(new[]{p._x,p._y,p._z});
                    var n=manager._faceData[1]==null?new Vector3(0,1,0):((Vector3*)manager._faceData[1].Address)[i];n=(vertex.GetMatrix().GetRotationMatrix()*n).Normalize();ns.Add(new[]{n._x,n._y,n._z});
                    var c=manager._faceData[2]==null?new RGBAPixel(255,255,255,255):((RGBAPixel*)manager._faceData[2].Address)[i];cs.Add(new[]{c.R/255f,c.G/255f,c.B/255f,c.A/255f});
                    c=manager._faceData[3]==null?new RGBAPixel(255,255,255,255):((RGBAPixel*)manager._faceData[3].Address)[i];ds.Add(new[]{c.R/255f,c.G/255f,c.B/255f,c.A/255f});
                }
            }
            positions.Add(ps.ToArray());normals.Add(ns.ToArray());colors.Add(cs.ToArray());colors1.Add(ds.ToArray());
        }
        return new MorphSampleData{Diagnostics=new[]{"Independent native ApplyCHR then ApplySHP sampling; no scene animation"},ExpectedVertices=positions.ToArray(),ExpectedNormals=normals.ToArray(),ExpectedColors=colors.ToArray(),ExpectedColors1=colors1.ToArray()};
    }
}
