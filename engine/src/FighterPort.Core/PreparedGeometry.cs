using System.Numerics;

namespace FighterPort.Core;

// Offline analysis uses the same automatic companion selection/morph evaluator as
// the owned previews. Native resources were prepared before this object is made.
public sealed class PreparedGeometry
{
    readonly FighterProject project;readonly AnimationClip clip;readonly EditorDocument layers;
    public PreparedGeometry(FighterProject project,AnimationClip clip)
    {
        this.project=project;this.clip=clip;layers=new(project);int index=Array.FindIndex(project.Clips,c=>c.Id==clip.Id&&c.Revision==clip.Revision);if(index<0)throw new InvalidOperationException("Geometry sampling requires an owned authentic clip revision.");layers.SelectClip(index);
    }
    public EvaluatedPose Evaluate(double frame)
    {
        var pose=AnimationService.Evaluate(project.Scene,clip,frame);var morph=layers.EvaluateMorph(pose,frame);if(morph is null)return pose;
        var positions=new Vector3[project.Scene.Meshes.Length][];int offset=0;
        for(int mesh=0;mesh<positions.Length;mesh++)
        {
            positions[mesh]=new Vector3[project.Scene.Meshes[mesh].Vertices.Length];for(int vertex=0;vertex<positions[mesh].Length;vertex++){int p=(offset+vertex)*MorphEvaluation.Stride;positions[mesh][vertex]=new(morph.VertexData[p],morph.VertexData[p+1],morph.VertexData[p+2]);}offset+=positions[mesh].Length;
        }
        return pose with{GeometryPositions=positions};
    }
    // Same automatic VIS0/default draw-call choice as the viewport. User-only
    // object toggles are not authentic source evidence and are not sampled.
    public bool MeshVisible(int mesh,double frame)
    {
        var value=project.Scene.Meshes[mesh];var visibility=layers.Layer("VIS0");
        var calls=value.DrawCalls.Length>0?value.DrawCalls:[new NativeDrawCall(value.Material,value.VisibilityBone,true,0,0,0)];
        foreach(var call in calls)
        {
            var track=visibility?.Tracks.FirstOrDefault(t=>t.Target==call.VisibilityBone)?.Visibility;
            if(track is {Length:>0}){if(track[Math.Clamp((int)layers.LayerFrame(visibility!,frame),0,track.Length-1)])return true;}
            else if(track is not null||call.DefaultVisible)return true;
        }
        return false;
    }
}
