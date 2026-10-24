namespace FighterPort.Core;

public sealed partial class EditorDocument
{
    sealed record SourcePreviewState(FighterProject Project,int Clip,int Bone,double Frame,double Speed,bool? Loop,bool Dirty,bool AutoMatch,
        Dictionary<int,Transform> Overrides,Dictionary<string,Guid> Layers,int MorphTrack,int MorphPoint,Diagnostic[] Diagnostics);
    SourcePreviewState? sourcePreview;
    void RequireEditing(){if(IsSourcePreview)throw new InvalidOperationException("Original source preview is read-only. Return to This Fighter before editing.");}
    public void BeginSourcePreview(FighterProject source,Guid clip)
    {
        ProjectStore.Validate(source);int index=Array.FindIndex(source.Clips,c=>c.Id==clip);if(index<0)throw new ArgumentException("The preview clip is not owned by this source model.");
        if(IsSourcePreview)EndSourcePreview();
        sourcePreview=new(Project,SelectedClip,SelectedBone,Clock.Frame,Clock.Speed,Clock.LoopOverride,Dirty,AutoMatchNativeLayers,PoseOverrides.ToDictionary(),ActiveLayers.ToDictionary(),SelectedMorphTrack,SelectedMorphPoint,LayerDiagnostics);
        Project=source;PrepareSceneFingerprint();SelectedClip=index;SelectedBone=0;SelectedMorphTrack=SelectedMorphPoint=0;PoseOverrides.Clear();ActiveLayers.Clear();AutoMatchNativeLayers=true;
        Clock.Playing=false;Clock.LoopOverride=null;Clock.Speed=1;Clock.Seek(0,Clip);MatchNativeLayers();Changed?.Invoke();
    }
    public void EndSourcePreview()
    {
        if(sourcePreview is not {} saved)return;sourcePreview=null;Project=saved.Project;PrepareSceneFingerprint();SelectedClip=saved.Clip;SelectedBone=saved.Bone;SelectedMorphTrack=saved.MorphTrack;SelectedMorphPoint=saved.MorphPoint;
        Dirty=saved.Dirty;AutoMatchNativeLayers=saved.AutoMatch;LayerDiagnostics=saved.Diagnostics;PoseOverrides.Clear();foreach(var pair in saved.Overrides)PoseOverrides[pair.Key]=pair.Value;
        ActiveLayers.Clear();foreach(var pair in saved.Layers)ActiveLayers[pair.Key]=pair.Value;
        Clock.Playing=false;Clock.Speed=saved.Speed;Clock.LoopOverride=saved.Loop;Clock.Seek(saved.Frame,Clip);PrepareMorph();Changed?.Invoke();
    }
}
