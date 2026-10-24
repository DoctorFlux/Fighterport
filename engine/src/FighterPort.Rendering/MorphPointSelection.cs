using System.Numerics;
using FighterPort.Core;

namespace FighterPort.Rendering;

public sealed unsafe partial class SkinnedViewport
{
    public bool MorphGeometryMode {get;set;}
    MorphBinding? PointBinding()
    {
        var layer=Document.Layer("SHP0");int index=Document.SelectedMorphTrack;
        if(layer is null||index<0||index>=layer.Tracks.Length)return null;
        return layer.Morphs.FirstOrDefault(b=>b.Attribute==MorphAttribute.Position&&b.Topology is not null&&MorphEditing.Matches(layer.Tracks[index],b));
    }
    void PickMorphPoint(Vector2 point)
    {
        var binding=PointBinding();if(binding?.Topology is not {} map)return;
        Document.Clock.Playing=false;var pose=Document.Evaluate();var morph=Document.EvaluateMorph(pose);var vertices=Document.Project.Scene.Meshes[binding.Mesh].Vertices;
        int offset=Document.Project.Scene.Meshes.Take(binding.Mesh).Sum(m=>m.Vertices.Length),picked=-1;float best=14;
        for(int p=0;p<vertices.Length;p++)
        {
            var v=morph?.Vertex(offset+p,vertices[p])??vertices[p];float distance=Vector2.Distance(point,Screen(AnimationService.Skin(v,pose)));
            if(distance<best){best=distance;picked=map.Indices[p];}
        }
        if(picked>=0){Document.SelectedMorphPoint=picked;PoseChanged?.Invoke();}
    }
    void DrawMorphPoint(EvaluatedPose pose)
    {
        if(!MorphGeometryMode||PointBinding() is not {} binding)return;
        int point=Array.IndexOf(binding.Topology!.Indices,Document.SelectedMorphPoint);if(point<0)return;
        int offset=Document.Project.Scene.Meshes.Take(binding.Mesh).Sum(m=>m.Vertices.Length);var vertex=Document.Project.Scene.Meshes[binding.Mesh].Vertices[point];
        var morph=Document.EvaluateMorph(pose);var position=AnimationService.Skin(morph?.Vertex(offset+point,vertex)??vertex,pose);float size=SceneScale*.025f;var lines=new List<Vertex>();
        foreach(var axis in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ})Line(lines,position-axis*size,position+axis*size,new(0,1,1,1));Lines(lines,false);
    }
}
