using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;

internal sealed class MorphKey {public double Frame {get;set;} public float Value {get;set;} public float InTangent {get;set;} public float OutTangent {get;set;}}
internal sealed class MorphChannel {public string Entry {get;set;} public string Base {get;set;} public string Target {get;set;} public int Slot {get;set;} public uint Flags {get;set;} public MorphKey[] Keys {get;set;} public bool New {get;set;}}
internal sealed class MorphChange {public string Entry {get;set;} public int Version {get;set;} public int SampleCount {get;set;} public bool Loop {get;set;} public MorphChannel[] Channels {get;set;}}
internal static unsafe partial class Program
{
    static KeyframeEntry[] NativeKeys(KeyframeArray array)
    {
        var type=typeof(KeyframeArray);int count=(int)type.GetField("_keyCount",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(array);
        var root=(KeyframeEntry)type.GetField("_keyRoot",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(array);var key=root._next;var keys=new KeyframeEntry[count];for(int i=0;i<count;i++,key=key._next)keys[i]=key;return keys;
    }
    static string ShapeSignature(SHP0Node shape)=>Json.Serialize(new{shape.Name,shape.Version,shape.FrameCount,shape.Loop,Entries=shape.Children.OfType<SHP0EntryNode>().Select(e=>new{e.Name,Flags=(uint)e._flags,Targets=e.Children.OfType<SHP0VertexSetNode>().Select(t=>new{t.Name,Curve=Curve(t.Keyframes)}).ToArray()}).ToArray()});
    static object SaveMotion(MDL0Node model,ResourceNode root,Request r,string modelHash)
    {
        if(File.Exists(r.Output)||Same(r.Output,r.Model)||Same(r.Output,r.Motion))throw new IOException("Choose a new native output distinct from originals.");
        string ext=Path.GetExtension(r.Output).ToLowerInvariant();if(root is ARCNode?ext!=".pac":root is BRRESNode?ext!=".brres":true)throw new NotSupportedException("Native output type must match the existing PAC or BRRES.");
        var nodes=Walk(root).ToArray();var chr=nodes.OfType<CHR0Node>().Single(c=>Identity(c)==r.Entry);var active=nodes.OfType<SHP0Node>().Single(s=>Identity(s)==r.MorphEntry);
        if(Container(chr)!=Container(active))throw new InvalidDataException("Bone/morph source containers differ.");
        if(chr.FrameCount!=r.OutputSampleCount||chr.Loop!=r.OutputLoop)throw new NotSupportedException("Native bone timing/loop changes are outside this save operation.");
        var changes=r.MorphChanges??new MorphChange[0];var shapes=changes.Select(c=>nodes.OfType<SHP0Node>().Single(s=>Identity(s)==c.Entry)).ToArray();
        if(shapes.Distinct().Count()!=shapes.Length)throw new InvalidDataException("Duplicate morph edit identity.");
        for(int i=0;i<shapes.Length;i++)
        {
            var shape=shapes[i];var change=changes[i];var sets=shape.Children.SelectMany(e=>e.Children).OfType<SHP0VertexSetNode>().ToArray();
            if(Container(shape)!=Container(chr)||shape.Version!=change.Version||shape.FrameCount!=change.SampleCount||shape.Loop!=change.Loop||sets.Length!=change.Channels.Count(c=>!c.New)||change.Channels.Select(c=>c.Entry).Distinct().Count()!=change.Channels.Length)throw new InvalidDataException("Morph native structure/timing does not match its immutable source.");
            foreach(var channel in change.Channels)
            {
                var set=channel.New?null:sets.Single(s=>Identity(s)==channel.Entry);var entry=channel.New?shape.Children.OfType<SHP0EntryNode>().Single(e=>Identity(e)==channel.Entry.Substring(0,channel.Entry.LastIndexOf('/'))):(SHP0EntryNode)set.Parent;
                if((set!=null&&(set.Name!=channel.Target||set.Index!=channel.Slot))||entry.Name!=channel.Base||(uint)entry._flags!=channel.Flags)throw new InvalidDataException("Morph entry/target/flag identity changed: "+channel.Entry);
                if(channel.New&&(entry.Children.Any(c=>c.Name==channel.Target)||channel.Slot!=entry.Children.Count+change.Channels.Count(c=>c.New&&c.Base==channel.Base&&c.Slot<channel.Slot)||channel.Slot>=32||!(r.GeometryChanges??new GeometryChange[0]).Any(g=>g.Entry==null&&g.Name==channel.Target)))throw new InvalidDataException("New morph target has no unique matching native geometry or append slot.");
                if(channel.Keys.Length==0||channel.Keys.Length>32767||channel.Keys.Select(k=>k.Frame).Distinct().Count()!=channel.Keys.Length||channel.Keys.Any(k=>double.IsNaN(k.Frame)||double.IsInfinity(k.Frame)||k.Frame!=Math.Truncate(k.Frame)||k.Frame<0||k.Frame>(shape.Loop?shape.FrameCount:shape.FrameCount-1)||float.IsNaN(k.Value)||float.IsInfinity(k.Value)||float.IsNaN(k.InTangent)||float.IsInfinity(k.InTangent)||k.InTangent!=k.OutTangent))throw new InvalidDataException("Invalid native morph keys: "+channel.Entry);
                if(channel.Keys.Length==1&&(channel.Keys[0].Frame!=0||channel.Keys[0].InTangent!=0))throw new InvalidDataException("Fixed SHP0 channels store only frame-zero values, without tangents.");
            }
        }
        var boneChanges=r.Edits.Where(e=>{var t=chr.Children.OfType<CHR0EntryNode>().SingleOrDefault(c=>c.Name==e.Bone);var key=t==null?null:t.KeyArrays[e.Channel].GetKeyframe(e.Frame);return key==null||FloatBits(key._value)!=(e.ValueBits??FloatBits(e.Value))||FloatBits(key._tangent)!=(e.TangentBits??FloatBits(e.Tangent));}).ToArray();
        var edited=shapes.Cast<ResourceNode>().Concat(boneChanges.Length>0?new ResourceNode[]{chr}:new ResourceNode[0]).ToArray();var preserved=ResourceHashes(root,edited:edited);
        foreach(var e in boneChanges)
        {
            if(e.Channel<0||e.Channel>8||float.IsNaN(e.Frame)||float.IsInfinity(e.Frame)||e.Frame<0||e.Frame>chr.FrameCount||!OrderedBones(model).Any(b=>b.Name==e.Bone))throw new InvalidDataException("Invalid edited native bone channel.");
            var t=chr.Children.OfType<CHR0EntryNode>().SingleOrDefault(c=>c.Name==e.Bone)??chr.CreateEntry(e.Bone);ApplyExactEdit(t.Keyframes.SetFrameValue(e.Channel,e.Frame,e.Value,false),e);t.SignalPropertyChange();
        }
        for(int i=0;i<shapes.Length;i++)foreach(var channel in changes[i].Channels)
        {
            var shape=shapes[i];var set=channel.New?shape.Children.OfType<SHP0EntryNode>().Single(e=>Identity(e)==channel.Entry.Substring(0,channel.Entry.LastIndexOf('/'))).FindOrCreateEntry(channel.Target):shape.Children.SelectMany(e=>e.Children).OfType<SHP0VertexSetNode>().Single(s=>Identity(s)==channel.Entry);
            foreach(var old in NativeKeys(set.Keyframes))set.Keyframes.Remove(old._index);
            foreach(var key in channel.Keys.OrderBy(k=>k.Frame))set.Keyframes.SetFrameValue(checked((float)key.Frame),key.Value)._tangent=key.OutTangent;
            set.SignalPropertyChange();
        }
        var signatures=shapes.ToDictionary(Identity,ShapeSignature);var geometry=WriteGeometry(model,r);root.Export(r.Output);
        using(var freshModelRoot=geometry==null?null:NodeFactory.FromFile(null,r.ModelOutput))
        using(var fresh=NodeFactory.FromFile(null,r.Output))
        {
            if(geometry!=null){model=Walk(freshModelRoot).OfType<MDL0Node>().Single(m=>Identity(m)==r.ModelEntry);MorphFloatBytes.Cache(model);VerifyGeometry(model,freshModelRoot,r,geometry);modelHash=Hash(r.ModelOutput);}
            VerifyPreserved(fresh,preserved);var reopened=Walk(fresh).ToArray();var bone=reopened.OfType<CHR0Node>().Single(c=>Identity(c)==r.Entry);var shape=reopened.OfType<SHP0Node>().Single(s=>Identity(s)==r.MorphEntry);
            foreach(var item in signatures)if(ShapeSignature(reopened.OfType<SHP0Node>().Single(s=>Identity(s)==item.Key))!=item.Value)throw new InvalidDataException("Fresh native morph keys/flags/timing changed: "+item.Key);
            if(bone.FrameCount!=chr.FrameCount||bone.Loop!=chr.Loop)throw new InvalidDataException("Fresh native bone timing changed.");
            var bonePositions=SampleBones(model,bone,r.Samples);var samples=MorphSamples(model,bone,shape,r);
            return new{Project=Project(model,reopened.OfType<CHR0Node>().ToArray(),geometry==null?r.Model:r.LogicalModelOutput,r.LogicalOutput??r.Output,modelHash,Hash(r.Output),fresh),Diagnostics=new[]{"Fresh PAC/BRRES reopened; SHP0 keys/flags/timing verified",preserved.Length+" unrelated native resource units unchanged, including skipped SCN0", geometry==null?"Native bone and morph samples verified before commit":"Fresh model arrays verified; original base geometry, bones, binds, weights, normals, colors, UVs, materials and model siblings unchanged"},ModelOutput=geometry==null?null:r.LogicalModelOutput,PreservedResources=preserved,ExpectedVertices=samples.ExpectedVertices,ExpectedNormals=samples.ExpectedNormals,ExpectedColors=samples.ExpectedColors,ExpectedColors1=samples.ExpectedColors1,ExpectedBonePositions=bonePositions};
        }
    }
}
