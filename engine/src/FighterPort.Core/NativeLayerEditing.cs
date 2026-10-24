namespace FighterPort.Core;

public sealed partial class EditorDocument
{
    public bool EditLayerVisibilityFrame(Guid layerId,int frame,IReadOnlyDictionary<int,bool> edits)
    {
        RequireEditing();var layer=Project.NativeLayers.Single(l=>l.Id==layerId);if(layer.Kind!="VIS0"||frame<0||frame>=layer.SampleCount)throw new ArgumentOutOfRangeException();
        var tracks=layer.Tracks.ToArray();bool changed=false;
        foreach(var edit in edits){if(edit.Key<0||edit.Key>=tracks.Length)throw new ArgumentOutOfRangeException(nameof(edits));var track=tracks[edit.Key];if(track.Visibility is not {Length:>0} old)throw new InvalidOperationException("The native entry has no visibility samples.");int index=Math.Min(frame,old.Length-1);if(old[index]==edit.Value)continue;var values=old.ToArray();values[index]=edit.Value;tracks[edit.Key]=track with{Visibility=values};changed=true;}
        if(!changed)return false;Clock.Playing=false;CancelTransformEdit();Transaction("Synchronize visibility frame",p=>p with{NativeLayers=p.NativeLayers.Select(l=>l.Id==layerId?l with{Tracks=tracks,Edited=true}:l).ToArray()});return true;
    }
    public bool ReplaceLayerCurve(Guid layerId,int trackIndex,int channel,Curve expected,Curve? replacement)
    {
        RequireEditing();var layer=Project.NativeLayers.Single(l=>l.Id==layerId);if(layer.Kind=="SCN0")throw new NotSupportedException("Scene animation is preserved and inactive.");
        var track=layer.Tracks[trackIndex];if(!ReferenceEquals(track.Curves[channel],expected))throw new InvalidOperationException("The layer curve edit is stale.");ValidateViewerCurve(replacement,layer.SampleCount);if(SameViewerCurve(expected,replacement))return false;
        var curves=track.Curves.ToArray();curves[channel]=replacement;var tracks=layer.Tracks.ToArray();tracks[trackIndex]=track with{Curves=curves};Clock.Playing=false;CancelTransformEdit();Transaction("Edit layer interpolation curve",p=>p with{NativeLayers=p.NativeLayers.Select(l=>l.Id==layerId?l with{Tracks=tracks,Edited=true}:l).ToArray()});return true;
    }
    public bool EditLayerFrame(Guid layerId,double frame,IReadOnlyDictionary<int,IReadOnlyDictionary<int,float?>> edits)
    {
        RequireEditing();var layer=Project.NativeLayers.Single(l=>l.Id==layerId);
        if(layer.Kind is not ("SRT0" or "CLR0"))throw new NotSupportedException("This frame operation requires a scalar UV or color layer.");
        if(!double.IsFinite(frame)||frame<0||frame>layer.SampleCount)throw new ArgumentOutOfRangeException(nameof(frame));
        var tracks=layer.Tracks.ToArray();bool changed=false;
        foreach(var entry in edits)
        {
            if(entry.Key<0||entry.Key>=tracks.Length)throw new ArgumentOutOfRangeException(nameof(edits));
            var track=tracks[entry.Key];var curves=track.Curves.ToArray();
            foreach(var channel in entry.Value)
            {
                if(channel.Key<0||channel.Key>=curves.Length||channel.Value is {} v&&!float.IsFinite(v))throw new InvalidDataException("Invalid scalar frame channel.");
                var curve=curves[channel.Key];var keys=curve?.Keys.ToList()??[];int index=keys.FindIndex(k=>k.Frame==frame);
                if(channel.Value is not {} value){if(index<0)continue;keys.RemoveAt(index);}
                else if(index<0)keys.Add(new(frame,value));else{if(keys[index].Value==value)continue;keys[index]=keys[index] with{Value=value};}
                curves[channel.Key]=keys.Count==0?null:curve is null?new(keys.OrderBy(k=>k.Frame).ToArray()):curve with{Keys=keys.OrderBy(k=>k.Frame).ToArray()};changed=true;
            }
            tracks[entry.Key]=track with{Curves=curves};
        }
        if(!changed)return false;Clock.Playing=false;CancelTransformEdit();Transaction("Edit typed animation frame",p=>p with{NativeLayers=p.NativeLayers.Select(l=>l.Id==layerId?l with{Tracks=tracks,Edited=true}:l).ToArray()});return true;
    }
    public bool EditLayerTexture(Guid layerId,int trackIndex,double frame,string? texture,string? palette)
    {
        RequireEditing();var layer=Project.NativeLayers.Single(l=>l.Id==layerId);
        if(layer.Kind!="PAT0")throw new NotSupportedException("This resource is not a texture-pattern layer.");
        if(trackIndex<0||trackIndex>=layer.Tracks.Length||!double.IsFinite(frame)||frame<0||frame>layer.SampleCount)throw new ArgumentOutOfRangeException();
        var track=layer.Tracks[trackIndex];var swaps=track.TextureSwaps.ToList();int index=swaps.FindIndex(k=>k.Frame==frame);
        if(texture is null&&palette is null){if(index<0)return false;swaps.RemoveAt(index);}else{var value=new TextureSwap(frame,texture,palette);if(index<0)swaps.Add(value);else{if(swaps[index]==value)return false;swaps[index]=value;}}
        var tracks=layer.Tracks.ToArray();tracks[trackIndex]=track with{TextureSwaps=swaps.OrderBy(s=>s.Frame).ToArray()};Clock.Playing=false;CancelTransformEdit();Transaction("Edit texture-pattern frame",p=>p with{NativeLayers=p.NativeLayers.Select(l=>l.Id==layerId?l with{Tracks=tracks,Edited=true}:l).ToArray()});return true;
    }
    public bool EditLayerCurve(Guid layerId,int trackIndex,int channel,double frame,Key? replacement)
    {
        RequireEditing();var layer=Project.NativeLayers.Single(l=>l.Id==layerId);
        if(layer.Kind=="SCN0")throw new NotSupportedException("Scene animation is preserved and inactive.");
        if(trackIndex<0||trackIndex>=layer.Tracks.Length||channel<0||channel>=layer.Tracks[trackIndex].Curves.Length||!double.IsFinite(frame)||frame<0||frame>layer.SampleCount)throw new ArgumentOutOfRangeException();
        if(replacement is {} value&&(value.Frame!=frame||!float.IsFinite(value.Value)||!float.IsFinite(value.InTangent)||!float.IsFinite(value.OutTangent)))throw new InvalidDataException("Layer keys require a finite value/tangents at the selected native time.");
        var track=layer.Tracks[trackIndex];var curve=track.Curves[channel];var existing=curve?.Keys.FirstOrDefault(k=>k.Frame==frame);
        if(replacement==existing)return false;
        var keys=curve?.Keys.ToList()??[];int index=keys.FindIndex(k=>k.Frame==frame);
        if(replacement is null){if(index<0)return false;keys.RemoveAt(index);}else if(index<0)keys.Add(replacement);else keys[index]=replacement;
        var curves=track.Curves.ToArray();curves[channel]=keys.Count==0?null:curve is null?new(keys.OrderBy(k=>k.Frame).ToArray()):curve with{Keys=keys.OrderBy(k=>k.Frame).ToArray()};
        var tracks=layer.Tracks.ToArray();tracks[trackIndex]=track with{Curves=curves};Clock.Playing=false;CancelTransformEdit();Transaction("Edit typed animation key",p=>p with{NativeLayers=p.NativeLayers.Select(l=>l.Id==layerId?l with{Tracks=tracks,Edited=true}:l).ToArray()});return true;
    }
    public bool EditLayerVisibility(Guid layerId,int trackIndex,int frame,bool? constant=null,bool? enabled=null)
    {
        RequireEditing();var layer=Project.NativeLayers.Single(l=>l.Id==layerId);
        if(layer.Kind!="VIS0")throw new NotSupportedException("This resource is not a supported discrete visibility layer.");
        if(trackIndex<0||trackIndex>=layer.Tracks.Length||frame<0||frame>=layer.SampleCount||layer.SampleCount>1000000)throw new ArgumentOutOfRangeException();
        var track=layer.Tracks[trackIndex];var old=track.Visibility??throw new InvalidOperationException("The native entry has no visibility data.");
        if(old.Length==0)throw new InvalidOperationException("The native entry has no visibility samples.");
        var values=old.ToArray();bool current=old[Math.Min(frame,old.Length-1)];
        if(constant==true)values=[current];else if(constant==false&&values.Length==1)values=Enumerable.Repeat(current,layer.SampleCount).ToArray();
        if(enabled is {} value)values[values.Length==1?0:Math.Min(frame,values.Length-1)]=value;
        if(values.SequenceEqual(old))return false;
        var tracks=layer.Tracks.ToArray();tracks[trackIndex]=track with{Visibility=values};Clock.Playing=false;CancelTransformEdit();Transaction("Edit typed animation visibility",p=>p with{NativeLayers=p.NativeLayers.Select(l=>l.Id==layerId?l with{Tracks=tracks,Edited=true}:l).ToArray()});return true;
    }
}
