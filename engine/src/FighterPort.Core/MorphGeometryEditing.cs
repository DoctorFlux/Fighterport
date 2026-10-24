using System.Numerics;

namespace FighterPort.Core;

public static class MorphGeometryEditing
{
    public static Vector4[] Prepare(MorphTopology topology,Vector4[] local,MorphAttribute attribute=MorphAttribute.Position)=>topology.Indices.Select((index,p)=>
        attribute is MorphAttribute.Color0 or MorphAttribute.Color1?local[index]:new Vector4(attribute==MorphAttribute.Normal?Vector3.TransformNormal(new Vector3(local[index].X,local[index].Y,local[index].Z),topology.BindMatrices[topology.TransformIndices[p]]):Vector3.Transform(new Vector3(local[index].X,local[index].Y,local[index].Z),topology.BindMatrices[topology.TransformIndices[p]]),0)).ToArray();

    public static void Validate(MorphBinding binding)
    {
        if(binding.Topology is not {} map)return;
        if(map.BaseValues.Length is <1 or >2_000_000||map.Indices.Length!=map.TransformIndices.Length||map.Indices.Any(i=>i<0||i>=map.BaseValues.Length)||map.TransformIndices.Any(i=>i<0||i>=map.BindMatrices.Length))throw new InvalidDataException("Invalid morph native-index correspondence.");
        if(map.BaseValues.Any(v=>!Finite(v))||map.BindMatrices.Any(m=>!Matrix4x4.Invert(m,out var inverse)||!float.IsFinite(inverse.GetDeterminant())))throw new InvalidDataException("Invalid morph bind geometry.");
        foreach(var target in binding.Targets)if(target.LocalValues is {} values&&(values.Length!=map.BaseValues.Length||values.Any(v=>!Finite(v))||target.Values.Length!=map.Indices.Length))throw new InvalidDataException("Morph geometry must preserve native topology and finite values.");
    }
    static bool Finite(Vector4 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z)&&float.IsFinite(v.W);

    public static FighterProject Create(FighterProject project,Guid layerId,int bindingIndex,string name)
    {
        var layer=project.NativeLayers.Single(l=>l.Id==layerId&&l.Kind=="SHP0");
        if(bindingIndex<0||bindingIndex>=layer.Morphs.Length)throw new ArgumentOutOfRangeException(nameof(bindingIndex));
        var basis=layer.Morphs[bindingIndex];var topology=basis.Topology??throw new InvalidOperationException("Reimport the model/motion pair to obtain native position-array correspondence.");
        name=name.Trim();if(name.Length is <1 or >100||name.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c=='_')))throw new ArgumentException("Use 1–100 letters, digits or underscores for a new target name.");
        if(project.NativeResources.Any(r=>r.Name==name&&r.NodeType is "MDL0VertexNode" or "MDL0NormalNode" or "MDL0ColorNode")||project.NativeLayers.SelectMany(l=>l.Morphs).SelectMany(b=>b.Targets).Any(t=>t.Name==name))throw new InvalidOperationException("A model morph array already uses that name. Choose a unique name.");
        if(basis.Native is null)throw new InvalidOperationException("An exact native morph entry is required.");
        var tracks=layer.Tracks.Where(t=>MorphEditing.Matches(t,basis)).ToArray();
        if(basis.Attribute!=MorphAttribute.Position||tracks.Length>=32)throw new NotSupportedException("Select a position binding with fewer than 32 native targets.");
        var curve=new Curve([new(0,0)],LoopFrameLimit:layer.Loop?layer.SampleCount+1:null);
        var track=new LayerTrack(tracks.FirstOrDefault()?.Target??basis.Entry,tracks.Length,[curve]){MorphTarget=name,NewMorphTarget=true,NativeFlags=tracks.FirstOrDefault()?.NativeFlags??3,Native=basis.Native with{Entry=basis.Native.Entry+"/new-"+name}};
        var changed=layer with{Edited=true,Tracks=[..layer.Tracks,track],Morphs=layer.Morphs.Select(b=>b.Native==basis.Native&&b.Topology is {} map?b with{Targets=[..b.Targets,new MorphTarget(name,curve,Prepare(map,map.BaseValues,b.Attribute)){LocalValues=map.BaseValues.ToArray(),GeometryEdited=true}]}:b).ToArray()};
        return project with{NativeLayers=project.NativeLayers.Select(l=>l.Id==layerId?changed:l).ToArray()};
    }

    public static FighterProject EditPoint(FighterProject project,Guid layerId,int trackIndex,int index,Vector3 point)
    {
        if(!float.IsFinite(point.X)||!float.IsFinite(point.Y)||!float.IsFinite(point.Z))throw new ArgumentException("Geometry coordinates must be finite.");
        var layer=project.NativeLayers.Single(l=>l.Id==layerId);var track=layer.Tracks[trackIndex];
        var binding=layer.Morphs.First(b=>b.Attribute==MorphAttribute.Position&&MorphEditing.Matches(track,b)&&b.Topology is not null&&b.Targets.Any(t=>t.Name==track.MorphTarget));
        var target=binding.Targets.Single(t=>t.Name==track.MorphTarget);var local=target.LocalValues??throw new InvalidOperationException("Reimport this target to edit native geometry.");
        if(index<0||index>=local.Length)throw new ArgumentOutOfRangeException(nameof(index));
        if(target.Native is {} native&&project.NativeLayers.SelectMany(l=>l.Morphs).Any(b=>b.Topology?.Basis==native))throw new InvalidOperationException("This target is also used as base geometry. Create an independent target before editing it.");
        var values=local.ToArray();values[index]=new(point,0);
        bool Same(MorphTarget t,MorphBinding b)=>b.Attribute==MorphAttribute.Position&&(target.Native is not null?t.Native==target.Native:t.Native is null&&t.Name==target.Name&&b.Topology?.Basis.Path==binding.Topology!.Basis.Path&&b.Topology?.Basis.Sha256==binding.Topology.Basis.Sha256);
        return project with{NativeLayers=project.NativeLayers.Select(l=>l with{Morphs=l.Morphs.Select(b=>b.Topology is {} map?b with{Targets=b.Targets.Select(t=>Same(t,b)?t with{LocalValues=values,Values=Prepare(map,values),GeometryEdited=true}:t).ToArray()}:b).ToArray()}).ToArray()};
    }
}
