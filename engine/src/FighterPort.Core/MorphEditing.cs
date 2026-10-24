namespace FighterPort.Core;

public static class MorphEditing
{
    public static bool Matches(LayerTrack track,MorphBinding binding)=>track.Native is {} native&&binding.Native is {} entry
        ? native.Path==entry.Path&&native.Sha256==entry.Sha256&&native.Entry.Contains('/')&&native.Entry[..native.Entry.LastIndexOf('/')] ==entry.Entry
        : track.Native is null&&binding.Native is null&&track.Target==binding.Entry;
    public static int[] EditableTracks(NativeAnimationLayer layer)=>Enumerable.Range(0,layer.Tracks.Length)
        .Where(i=>layer.Tracks[i].MorphTarget is {} name&&layer.Morphs.Any(b=>Matches(layer.Tracks[i],b)&&b.Targets.Any(t=>t.Name==name))).ToArray();
    public static NativeAnimationLayer Edit(NativeAnimationLayer layer,int index,double frame,Key? key)
    {
        if(index<0||index>=layer.Tracks.Length||!EditableTracks(layer).Contains(index))throw new InvalidOperationException("This morph target has no resolved editable model binding. Reimport its complete model/motion pair or resolve the reported array conflict.");
        if(!double.IsFinite(frame)||frame<0||frame>(layer.Loop?layer.SampleCount:layer.SampleCount-1))throw new ArgumentOutOfRangeException(nameof(frame),"Morph key time is outside its native sample/closing-key range.");
        if(key is not null&&(key.Frame!=frame||!float.IsFinite(key.Value)||!float.IsFinite(key.InTangent)||!float.IsFinite(key.OutTangent)))throw new ArgumentException("Morph keys require finite values and tangents at the selected time.");
        var track=layer.Tracks[index];var curve=track.Curves.SingleOrDefault()??new Curve([new(0,0)],LoopFrameLimit:layer.Loop?layer.SampleCount+1:null);
        if(curve.Keys.Select(k=>k.Frame).Distinct().Count()!=curve.Keys.Length)throw new InvalidOperationException("Review duplicate native morph keys before editing this channel.");
        curve=key is not null?curve.Put(key):curve with{Keys=curve.Keys.Where(k=>k.Frame!=frame).ToArray()};
        if(curve.Keys.Length==0)curve=curve with{Keys=[new(0,0)]};
        return layer with{Edited=true,Tracks=layer.Tracks.Select((t,i)=>i==index?t with{Curves=[curve]}:t).ToArray(),
            Morphs=layer.Morphs.Select(b=>Matches(track,b)?b with{Targets=b.Targets.Select(t=>t.Name==track.MorphTarget?t with{Weight=curve}:t).ToArray()}:b).ToArray()};
    }
}
