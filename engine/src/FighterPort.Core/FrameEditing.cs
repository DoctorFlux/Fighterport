namespace FighterPort.Core;

[Flags]
public enum TransformComponents { None=0, Scale=1, Rotation=2, Translation=4, All=7 }
public sealed record BoneFrameValue(string Bone,float[] Values,bool[] Keyed,QuaternionKey? Rotation);
public sealed record AnimationFrameBuffer(Guid SkeletonRevision,string Fingerprint,BoneFrameValue[] Bones);
public sealed record AnimationCleanup(AnimationClip Before,AnimationClip After,string[] Changes);

// These commands own only the selected animation revision. Native originals,
// deform data, companion resources and other target documents are never edited.
public sealed partial class EditorDocument
{
    static bool Included(TransformComponents components,int channel)=>(components&(channel<3?TransformComponents.Scale:channel<6?TransformComponents.Rotation:TransformComponents.Translation))!=0;
    void RequireMotionEdit()
    {
        RequireEditing();
        if(Project.Clips.Length==0)throw new InvalidOperationException("Create or select an animation first.");
    }
    public AnimationFrameBuffer CopyAnimationFrame(bool allBones=false)
    {
        if(Project.Scene.Bones.Length==0)throw new InvalidOperationException("Load a model first.");
        var selected=Project.Scene.Bones[SelectedBone];
        var names=allBones?(Project.Clips.Length==0?Project.Scene.Bones.Select(b=>b.Name):Clip.Tracks.Select(t=>t.Bone)):[selected.Name];
        var values=names.Select(name=>
        {
            var bone=Project.Scene.Bones.FirstOrDefault(b=>b.Name==name);
            if(bone is null)throw new NotSupportedException("The animation contains an unbound bone entry: "+name+". Review its native binding before copying the whole frame.");
            var track=Clip.Tracks.FirstOrDefault(t=>t.Bone==name);
            var rotation=track?.Rotation?.Keys.FirstOrDefault(k=>k.Frame==Clock.Frame);
            var keyed=Enumerable.Range(0,9).Select(c=>c is >=3 and <6&&track?.Rotation is not null?rotation is not null:track?.Channels[c]?.Keys.Any(k=>k.Frame==Clock.Frame)==true).ToArray();
            return new BoneFrameValue(name,Evaluate().Local[Array.IndexOf(Project.Scene.Bones,bone)].Channels(),keyed,
                track?.Rotation is {} q?new(Clock.Frame,q.Sample(Clock.Frame)):null);
        }).ToArray();
        return new(Project.Scene.SkeletonRevision,SceneFingerprint,values);
    }
    public bool PasteAnimationFrame(AnimationFrameBuffer buffer,TransformComponents components,bool allBones=false,bool keyedOnly=false)
    {
        RequireEditing();
        if(buffer.SkeletonRevision!=Project.Scene.SkeletonRevision||buffer.Fingerprint!=SceneFingerprint)
            throw new InvalidOperationException("This clipboard belongs to another rig revision. Use the explicit animation mapping/retarget route.");
        if(!allBones&&buffer.Bones.Length!=1)throw new InvalidOperationException("Use the selected-bone clipboard for Transform Frame.");
        if(Project.Clips.Length==0)
        {
            var pose=Evaluate();var edits=new Dictionary<int,Transform>();
            foreach(var value in buffer.Bones){int bone=allBones?Array.FindIndex(Project.Scene.Bones,b=>b.Name==value.Bone):SelectedBone;if(bone<0)throw new InvalidOperationException("The clipboard bone is absent from this rig.");var channels=pose.Local[bone].Channels();for(int c=0;c<9;c++)if(Included(components,c)&&(!keyedOnly||value.Keyed[c]))channels[c]=value.Values[c];edits[bone]=Transform.From(channels);}
            using var gesture=BeginTransformEdit();return gesture.Commit(edits);
        }
        var motion=Clip;var tracks=motion.Tracks.ToList();bool changed=false;
        foreach(var value in buffer.Bones)
        {
            string name=allBones?value.Bone:Project.Scene.Bones[SelectedBone].Name;
            var bone=Project.Scene.Bones.Single(b=>b.Name==name);int index=tracks.FindIndex(t=>t.Bone==name);
            var track=index<0?new BoneTrack(name,new Curve?[9]):tracks[index];var channels=track.Channels.ToArray();bool entryChanged=false;
            for(int c=0;c<9;c++)
            {
                if(!Included(components,c)||keyedOnly&&!value.Keyed[c])continue;
                if(c is >=3 and <6&&(track.Rotation is not null||value.Rotation is not null))continue;
                var curve=channels[c];var key=curve?.Keys.FirstOrDefault(k=>k.Frame==Clock.Frame);
                if(key is not null&&key.Value==value.Values[c])continue;
                if(curve is null)curve=new(Clock.Frame==0?[]:[new(0,motion.Sample(bone,0).Channels()[c])]);
                if(key is not null)
                {
                    var keys=curve.Keys.ToArray();keys[Array.FindIndex(keys,k=>k.Frame==Clock.Frame)]=key with{Value=value.Values[c]};channels[c]=curve with{Keys=keys};
                }
                else
                {
                    float tangent=curve.Keys.Length>1&&curve.Interpolation==Interpolation.Hermite?(curve.Sample(Clock.Frame+.001)-curve.Sample(Clock.Frame-.001))/.002f:0;
                    channels[c]=curve.Put(new(Clock.Frame,value.Values[c],tangent,tangent));
                }
                entryChanged=true;
            }
            var rotation=track.Rotation;
            if((components&TransformComponents.Rotation)!=0&&(!keyedOnly||value.Keyed.Skip(3).Take(3).Any(k=>k))&&(rotation is not null||value.Rotation is not null))
            {
                var q=value.Rotation?.Value??System.Numerics.Quaternion.CreateFromRotationMatrix((Transform.Identity with{Rotation=Transform.From(value.Values).Rotation}).Matrix);
                var key=rotation?.Keys.FirstOrDefault(k=>k.Frame==Clock.Frame);
                if(key?.Value!=q){rotation=(rotation??new QuaternionCurve([])).Put(new(Clock.Frame,q));entryChanged=true;}
            }
            if(!entryChanged)continue;changed=true;var updated=track with{Channels=channels,Rotation=rotation};if(index<0)tracks.Add(updated);else tracks[index]=updated;
        }
        return changed&&CommitAnimationEdit("Paste "+(allBones?"whole frame":"bone frame"),motion with{Tracks=tracks.ToArray()});
    }
    public bool ClearAnimationFrame(TransformComponents components,bool allBones=false)
    {
        RequireMotionEdit();var motion=Clip;bool changed=false;string selected=Project.Scene.Bones[SelectedBone].Name;
        var tracks=motion.Tracks.Select(t=>
        {
            if(!allBones&&t.Bone!=selected)return t;
            var channels=t.Channels.Select((curve,c)=>
            {
                if(curve is null||!Included(components,c)||!curve.Keys.Any(k=>k.Frame==Clock.Frame))return curve;
                changed=true;var keys=curve.Keys.Where(k=>k.Frame!=Clock.Frame).ToArray();return keys.Length==0?null:curve with{Keys=keys};
            }).ToArray();
            var rotation=t.Rotation;
            if((components&TransformComponents.Rotation)!=0&&rotation?.Keys.Any(k=>k.Frame==Clock.Frame)==true)
            {changed=true;var keys=rotation.Keys.Where(k=>k.Frame!=Clock.Frame).ToArray();rotation=keys.Length==0?null:rotation with{Keys=keys};}
            return t with{Channels=channels,Rotation=rotation};
        }).ToArray();
        return changed&&CommitAnimationEdit("Clear "+(allBones?"whole frame":"bone frame"),motion with{Tracks=tracks});
    }
    public bool EditAnimationChannel(int channel,Func<float,float>? edit)
    {
        RequireMotionEdit();if(channel is <0 or >8)throw new ArgumentOutOfRangeException(nameof(channel));
        string name=Project.Scene.Bones[SelectedBone].Name;var track=Clip.Tracks.FirstOrDefault(t=>t.Bone==name);
        if(track is null)return false;
        if(channel is >=3 and <6&&track.Rotation is not null)throw new NotSupportedException("Independent Euler-channel edits are unavailable for quaternion rotation tracks.");
        var curve=track.Channels[channel];if(curve is null)return false;
        var changed=edit is null?null:curve with{Keys=curve.Keys.Select(k=>k with{Value=edit(k.Value)}).ToArray()};
        if(changed is not null&&changed.Keys.SequenceEqual(curve.Keys))return false;
        var channels=track.Channels.ToArray();channels[channel]=changed;
        return CommitAnimationEdit(edit is null?"Remove all channel keys":"Edit all channel values",Clip with{Tracks=Clip.Tracks.Select(t=>ReferenceEquals(t,track)?t with{Channels=channels}:t).ToArray()});
    }
    public bool EditAnimationKey(int bone,int channel,double time,Key? replacement)
    {
        RequireMotionEdit();if(bone<0||bone>=Project.Scene.Bones.Length||channel is <0 or >8)throw new ArgumentOutOfRangeException();
        var track=Clip.Tracks.FirstOrDefault(t=>t.Bone==Project.Scene.Bones[bone].Name);var curve=track?.Channels[channel];if(track is null||curve is null)return false;
        if(channel is >=3 and <6&&track.Rotation is not null)throw new NotSupportedException("Edit the quaternion rotation as a complete rotation.");
        var existing=curve.Keys.FirstOrDefault(k=>k.Frame==time);if(existing is null||replacement==existing)return false;
        var keys=curve.Keys.ToList();int index=keys.FindIndex(k=>k.Frame==time);if(replacement is null)keys.RemoveAt(index);else keys[index]=replacement;
        var channels=track.Channels.ToArray();channels[channel]=keys.Count==0?null:curve with{Keys=keys.OrderBy(k=>k.Frame).ToArray()};
        return CommitAnimationEdit(replacement is null?"Delete animation key":"Edit animation key",Clip with{Tracks=Clip.Tracks.Select(t=>ReferenceEquals(t,track)?t with{Channels=channels}:t).ToArray()});
    }
    public bool ReplaceAnimationCurve(int bone,int channel,Curve expected,Curve? replacement)
    {
        RequireMotionEdit();if(bone<0||bone>=Project.Scene.Bones.Length||channel is <0 or >8)throw new ArgumentOutOfRangeException();
        var track=Clip.Tracks.Single(t=>t.Bone==Project.Scene.Bones[bone].Name);if(!ReferenceEquals(track.Channels[channel],expected))throw new InvalidOperationException("The curve edit is stale.");
        if(channel is >=3 and <6&&track.Rotation is not null)throw new NotSupportedException("Quaternion rotation requires complete rotation editing.");
        ValidateViewerCurve(replacement,Clip.SampleCount);if(SameViewerCurve(expected,replacement))return false;
        var channels=track.Channels.ToArray();channels[channel]=replacement;
        return CommitAnimationEdit("Edit interpolation curve",Clip with{Tracks=Clip.Tracks.Select(t=>ReferenceEquals(t,track)?t with{Channels=channels}:t).ToArray()});
    }
    static bool SameViewerCurve(Curve expected,Curve? replacement)=>replacement is not null&&expected.Interpolation==replacement.Interpolation&&expected.LoopFrameLimit==replacement.LoopFrameLimit&&expected.Keys.SequenceEqual(replacement.Keys);
    static void ValidateViewerCurve(Curve? curve,int count)
    {
        if(curve is null)return;if(curve.Keys.Length==0||curve.Keys.Any(k=>!double.IsFinite(k.Frame)||k.Frame<0||k.Frame>count||!float.IsFinite(k.Value)||!float.IsFinite(k.InTangent)||!float.IsFinite(k.OutTangent))||curve.Keys.Zip(curve.Keys.Skip(1)).Any(p=>p.First.Frame>p.Second.Frame)||curve.Keys.GroupBy(k=>k.Frame).Any(g=>g.Count()>2))throw new InvalidDataException("Curve edits require ordered finite keys, at most two sides per native time, within the resource duration.");
    }
    void RequireIndependentTimeline()
    {
        RequireMotionEdit();
        if(Project.NativeLayers.Any(l=>l.Kind!="SCN0")||Project.Contacts.Any(c=>c.Clip==Clip.Id)||Clip.Transition is not null||Project.Moves.Length>0)
            throw new NotSupportedException("Time editing is unavailable for a composition with companion layers, contacts, transitions or gameplay events. Their synchronized time-edit adapter must be supported first.");
    }
    public bool InsertAnimationFrame()=>ChangeAnimationTime(true);
    public bool DeleteAnimationFrame()=>ChangeAnimationTime(false);
    bool ChangeAnimationTime(bool insert)
    {
        RequireIndependentTimeline();var motion=Clip;double at=Clock.Frame;
        if(at!=Math.Truncate(at)||at>motion.LastSampleFrame)throw new InvalidOperationException("Select a whole stored animation frame for time insertion/deletion.");
        if(!insert&&motion.SampleCount==1)throw new InvalidOperationException("An animation must retain at least one sample.");
        int count=checked(motion.SampleCount+(insert?1:-1));
        Key[] Shift(Key[] keys)=>keys.Where(k=>insert||k.Frame<at||k.Frame>=at+1).Select(k=>k.Frame>=at+(insert?0:1)?k with{Frame=k.Frame+(insert?1:-1)}:k).ToArray();
        var tracks=motion.Tracks.Select(t=>t with
        {
            Channels=t.Channels.Select(c=>
            {
                if(c is null)return null;var keys=Shift(c.Keys);return keys.Length==0?null:c with{Keys=keys,LoopFrameLimit=c.LoopFrameLimit is {} limit?checked(count+limit-motion.SampleCount):null};
            }).ToArray(),
            Rotation=t.Rotation is not {} q?null:q with{Keys=q.Keys.Where(k=>insert||k.Frame<at||k.Frame>=at+1).Select(k=>k.Frame>=at+(insert?0:1)?k with{Frame=k.Frame+(insert?1:-1)}:k).ToArray()}
        }).Select(t=>t.Rotation is {Keys.Length:0}?t with{Rotation=null}:t).ToArray();
        return CommitAnimationEdit(insert?"Insert animation time":"Delete animation time",motion with{SampleCount=count,Tracks=tracks});
    }
    public bool SetAnimationSampleCount(int count)
    {
        RequireIndependentTimeline();if(count<1||count>1000000)throw new ArgumentOutOfRangeException(nameof(count),"Use 1–1000000 samples.");
        var motion=Clip;if(count==motion.SampleCount)return false;
        double limit=count;
        var tracks=motion.Tracks.Select(t=>t with
        {
            Channels=t.Channels.Select(c=>{if(c is null)return null;var keys=c.Keys.Where(k=>k.Frame<=limit).ToArray();return keys.Length==0?null:c with{Keys=keys,LoopFrameLimit=c.LoopFrameLimit is {} oldLimit?checked(count+oldLimit-motion.SampleCount):null};}).ToArray(),
            Rotation=t.Rotation is not {} q?null:q with{Keys=q.Keys.Where(k=>k.Frame<=limit).ToArray()}
        }).Select(t=>t.Rotation is {Keys.Length:0}?t with{Rotation=null}:t).ToArray();
        return CommitAnimationEdit("Change animation sample count",motion with{SampleCount=count,Tracks=tracks});
    }
    public bool SetAnimationLoop(bool loop)
    {
        RequireMotionEdit();return loop!=Clip.Loop&&CommitAnimationEdit("Change native animation loop",Clip with{Loop=loop});
    }
    public bool ResizeAnimation(int count)
    {
        RequireIndependentTimeline();if(count<1||count>1000000)throw new ArgumentOutOfRangeException(nameof(count),"Use 1–1000000 samples.");
        var motion=Clip;if(count==motion.SampleCount)return false;
        double ratio=(double)count/motion.SampleCount;
        // Preserve continuous time and both duplicate sides. Native writers may
        // reject nonintegral times; never silently round or merge authored keys.
        var tracks=motion.Tracks.Select(t=>t with
        {
            Channels=t.Channels.Select(c=>c is null?null:c with{Keys=c.Keys.Select(k=>k with{Frame=k.Frame*ratio,InTangent=(float)(k.InTangent/ratio),OutTangent=(float)(k.OutTangent/ratio)}).ToArray(),LoopFrameLimit=c.LoopFrameLimit is {} oldLimit?checked(count+oldLimit-motion.SampleCount):null}).ToArray(),
            Rotation=t.Rotation is not {} q?null:q with{Keys=q.Keys.Select(k=>k with{Frame=k.Frame*ratio}).ToArray()}
        }).ToArray();
        return CommitAnimationEdit("Resize animation timing",motion with{SampleCount=count,Tracks=tracks});
    }
    public AnimationCleanup PlanAnimationCleanup()
    {
        RequireMotionEdit();var changes=new List<string>();
        var tracks=Clip.Tracks.Select(t=>t with{Channels=t.Channels.Select((c,i)=>
        {
            if(c is null||c.Keys.Length<2)return c;
            // Exact constant proof only. Do not apply a tolerance or regenerate
            // tangents; nonzero Hermite constants can differ in float evaluation.
            bool constant=c.Keys.All(k=>k.Value==c.Keys[0].Value)&&
                (c.Interpolation!=Interpolation.Hermite||c.Keys.All(k=>k.Value==0&&k.InTangent==0&&k.OutTangent==0));
            if(!constant)return c;changes.Add($"{t.Bone} channel {i}: remove {c.Keys.Length-1} exactly constant keys; retain first key/tangents.");return c with{Keys=[c.Keys[0]]};
        }).ToArray()}).ToArray();
        return new(Clip,Clip with{Tracks=tracks},changes.ToArray());
    }
    public bool ApplyAnimationCleanup(AnimationCleanup plan)
    {
        RequireMotionEdit();if(!ReferenceEquals(plan.Before,Clip))throw new InvalidOperationException("The cleanup review is stale; review the current animation again.");
        return plan.Changes.Length>0&&CommitAnimationEdit("Clean exact constant animation keys",plan.After);
    }
    bool CommitAnimationEdit(string name,AnimationClip next)
    {
        RequireMotionEdit();CancelTransformEdit();Clock.Playing=false;var id=Clip.Id;
        next=next with{Revision=Guid.NewGuid(),RewriteNativeChannels=true,Retarget=next.Retarget is {} recipe?recipe with{Validation=null,IsDraft=true}:null};
        Transaction(name,p=>p with{Clips=p.Clips.Select(c=>c.Id==id?next:c).ToArray()});
        if(Clock.Frame>Clip.PreviewEndFrame)Seek(Clip.PreviewEndFrame);
        return true;
    }
}
