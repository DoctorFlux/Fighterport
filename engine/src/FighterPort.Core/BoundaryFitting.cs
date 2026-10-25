using System.Numerics;

namespace FighterPort.Core;

public sealed record BoundaryAnchor(AnimationClip Clip,string SkeletonFingerprint,double Frame,bool Frozen=true,double Rate=1);
public sealed record BoundaryFitSettings(int EntrySamples,int ExitSamples,TransformComponents Components,
    string[] Bones,ProtectedInterval[] Protected,bool PreserveRootTranslation=true,bool NearestEquivalentAngles=true)
{
    public bool PreserveContacts {get;init;}
    public string[] ContactBones {get;init;}=[];
    public bool ExitContactRecovery {get;init;}
}
public sealed record BoundaryFitReceipt(string Version,string Source,string? Entry,string? Exit,int SampleCount,bool Loop,
    BoundaryFitSettings Settings,int ChangedChannels,string[] Limits)
{
    public string[] ProtectedContactBones {get;init;}=[];
    public string[] RecoveredContactBones {get;init;}=[];
    public int? RecoveryStart {get;init;}
    public int? RecoveryEnd {get;init;}
}
public sealed record BoundaryFitResult(AnimationClip Clip,BoneTrack[] Changed,BoundaryFitReceipt Receipt);

// Offline FK curve editing on the existing rig. This is not cross-rig
// retargeting, a contact solver, a time edit or a claim of game/state acceptance.
public static class BoundaryFitting
{
    public const string Version="fixed-duration-fk-hermite-1";
    public const string ContactVersion="contact-trajectory-preserve-1";
    public const string ExitRecoveryVersion="contact-exit-recovery-1";
    public static BoundaryFitResult Prepare(FighterProject project,AnimationClip source,BoundaryAnchor? entry,BoundaryAnchor? exit,BoundaryFitSettings settings)
    {
        string fingerprint=AnimationControls.Fingerprint(project.Scene);int last=source.SampleCount-1;
        var controls=project.ControlRigs.FirstOrDefault(r=>r.SkeletonFingerprint==fingerprint);
        var protectedMotion=controls is null?new HashSet<string>(StringComparer.Ordinal):
            AnimationControls.ProtectedMotionBones(project.Scene,controls).Select(i=>project.Scene.Bones[i].Name).ToHashSet(StringComparer.Ordinal);
        if(source.SampleCount<3||source.SampleCount>100000)throw new InvalidDataException("Boundary fitting requires 3–100000 existing samples.");
        if(entry is null&&exit is null)throw new InvalidDataException("Choose an entry or exit reference.");
        if(settings.Components==TransformComponents.None||(settings.Components&~TransformComponents.All)!=0)throw new InvalidDataException("Choose supported transform components.");
        if(settings.ExitContactRecovery&&(entry is not null||exit is not {Frozen:true,Frame:0}||!settings.PreserveContacts||settings.Components!=TransformComponents.All))
            throw new InvalidDataException("Exit contact recovery requires an exit-only frozen first-frame reference, all S/R/T components and existing contact preservation.");
        if(settings.ExitContactRecovery&&source.Loop)
            throw new NotSupportedException("Exit contact recovery currently supports non-looping source animations only. Stored-loop wrap dependencies require a separate verified recovery adapter; the original animation and loop flag remain unchanged.");
        if(settings.Bones.Length==0||settings.Bones.Distinct(StringComparer.Ordinal).Count()!=settings.Bones.Length||settings.Bones.Any(n=>!project.Scene.Bones.Any(b=>b.Name==n)))throw new InvalidDataException("Choose existing bones from this exact rig revision.");
        int start=entry is null?0:settings.EntrySamples-1,end=exit is null?last:source.SampleCount-settings.ExitSamples;
        if(entry is not null&&(settings.EntrySamples<2||settings.EntrySamples>=source.SampleCount)||exit is not null&&(settings.ExitSamples<2||settings.ExitSamples>=source.SampleCount)||start>=end)throw new InvalidDataException("Fit ranges must have at least two samples, leave a protected middle, and must not overlap.");
        var protectedRanges=settings.Protected.Concat(project.Moves.SelectMany(m=>m.Events)).ToArray();
        if(protectedRanges.Any(p=>!double.IsFinite(p.Start)||!double.IsFinite(p.End)||p.Start<0||p.End<p.Start||p.End>last||entry is not null&&p.Start<start||exit is not null&&p.End>end))throw new InvalidDataException("A fit range intersects a protected frame/event interval. Shorten the fit or review that protection explicitly.");
        var contactBones=ContactBoundaryProtection.Resolve(project,source,settings);
        if(project.NativeLayers.Any(l=>l.Kind!="SCN0"))throw new NotSupportedException("Composed companion layers require a synchronized fitting adapter; the body-only operation cannot rewrite that composition.");
        void Anchor(BoundaryAnchor? a)
        {
            if(a is null)return;
            if(a.SkeletonFingerprint!=fingerprint)throw new InvalidDataException("A transition reference belongs to another native rig revision. Retarget it explicitly first.");
            if(!double.IsFinite(a.Frame)||a.Frame<0||a.Frame>a.Clip.LastSampleFrame||!double.IsFinite(a.Rate)||a.Rate<=0||a.Rate>4)throw new InvalidDataException("Choose a valid reference frame and a positive reference speed of at most 4.");
            a.Clip.Rate.Validate();
        }
        Anchor(entry);Anchor(exit);source.Rate.Validate();
        var recovered=ContactBoundaryProtection.ResolveExitRecovery(project,source,settings,controls,protectedMotion,contactBones,end);
        if(settings.ExitContactRecovery)
            foreach(var bone in project.Scene.Bones.Where(b=>protectedMotion.Contains(b.Name)))
                if(!ProtectedEndpointMatches(source.Sample(bone,last),exit!.Clip.Sample(bone,0)))
                    throw new InvalidDataException("A source-authored protected motion-control endpoint differs from the frozen reference first pose: "+bone.Name+". Recovery cannot match this world pose without changing protected roots; no root compensation is allowed.");
        var tracks=source.Tracks.ToList();var changed=new List<BoneTrack>();int changedChannels=0;
        foreach(string name in settings.Bones)
        {
            if(protectedMotion.Contains(name))continue;
            if(contactBones.Contains(name)&&!recovered.Contains(name))continue;
            var bone=project.Scene.Bones.Single(b=>b.Name==name);var rest=bone.Rest.Channels();
            var original=source.Tracks.SingleOrDefault(t=>t.Bone==name)??new BoneTrack(name,new Curve?[9]);
            if(original.Rotation is not null&&(settings.Components&TransformComponents.Rotation)!=0)throw new NotSupportedException("Quaternion boundary fitting requires a rotation-space adapter. Euler native curves remain unchanged.");
            var channels=original.Channels.ToArray();bool any=false;
            for(int channel=0;channel<9;channel++)
            {
                var component=channel<3?TransformComponents.Scale:channel<6?TransformComponents.Rotation:TransformComponents.Translation;
                if((settings.Components&component)==0||settings.PreserveRootTranslation&&bone.Parent<0&&component==TransformComponents.Translation)continue;
                var curve=channels[channel];
                if(curve is not null&&curve.Interpolation!=Interpolation.Hermite)throw new NotSupportedException("The fitted native channel must use Hermite interpolation; other channels remain intact.");
                if(curve?.Keys.Any(k=>!double.IsFinite(k.Frame)||!float.IsFinite(k.Value)||!float.IsFinite(k.InTangent)||!float.IsFinite(k.OutTangent))==true)throw new InvalidDataException("A source channel contains nonfinite keys.");
                float Value(double at)=>curve?.Sample(at)??rest[channel];
                var keys=(curve?.Keys??[]).Where(k=>!(entry is not null&&k.Frame>=0&&k.Frame<start)&&!(exit is not null&&k.Frame>end&&k.Frame<=last)).ToList();
                void Join(int at){if(!keys.Any(k=>k.Frame==at)){float slope=Derivative(curve,at);keys.Add(new(at,Value(at),slope,slope));}}
                Key Endpoint(BoundaryAnchor a,int at,int join)
                {
                    var reference=a.Clip.Tracks.SingleOrDefault(t=>t.Bone==name);
                    if(reference?.Rotation is not null&&channel is >=3 and <6)throw new NotSupportedException("The reference has quaternion rotation; use an explicit rotation adapter.");
                    var referenceCurve=reference?.Channels[channel];float value=referenceCurve?.Sample(a.Frame)??rest[channel];
                    if(!a.Frozen&&referenceCurve is not null&&referenceCurve.Keys.Where(k=>k.Frame==a.Frame).Select(k=>k.Value).Distinct().Count()>1)throw new NotSupportedException("The moving reference phase has a native value discontinuity. Choose a frozen pose or another reference frame.");
                    if(settings.NearestEquivalentAngles&&channel is >=3 and <6)value+=360*(float)Math.Round((Value(join)-value)/360,MidpointRounding.ToEven);
                    float slope=a.Frozen?0:Derivative(referenceCurve,a.Frame)*(float)(a.Rate*a.Clip.Rate.FramesPerSecond/source.Rate.FramesPerSecond);
                    if(!float.IsFinite(value)||!float.IsFinite(slope))throw new InvalidDataException("The reference produced a nonfinite endpoint.");
                    return new(at,value,slope,slope);
                }
                if(entry is not null){Join(start);keys.Add(Endpoint(entry,0,start));}
                if(exit is not null){Join(end);keys.Add(Endpoint(exit,last,end));}
                channels[channel]=new(keys.OrderBy(k=>k.Frame).ToArray(),Interpolation.Hermite,curve?.LoopFrameLimit??(source.Loop?source.SampleCount:null));changedChannels++;any=true;
            }
            if(!any)continue;
            var updated=original with{Channels=channels};int index=tracks.FindIndex(t=>t.Bone==name);if(index<0)tracks.Add(updated);else tracks[index]=updated;changed.Add(updated);
        }
        if(changed.Count==0)throw new InvalidDataException("The mask, root policy and protected contact paths leave no eligible channels to fit. Choose unprotected bones; repositioning these contacts needs a coupled solver. Protected: "+string.Join(", ",contactBones));
        string[] limits=["FK component curves only; no contact, clearance, joint-limit, world-trajectory or game-state acceptance.","Existing duration, rate, loop flag, middle curves and unselected channels are retained; inspect the loop seam.",settings.NearestEquivalentAngles?"Reference Euler angles use the nearest equivalent turn at each original join.":"Reference Euler channel values retain their authored turn counts, including full spins.","Native companion resources are not copied into this independent body animation."];
        string version=settings.ExitContactRecovery?ExitRecoveryVersion:settings.PreserveContacts?ContactVersion:Version;
        if(protectedMotion.Count>0)limits=[..limits,"Source-authored motion controls retain their original curves: "+string.Join(", ",protectedMotion.Order(StringComparer.Ordinal))];
        if(settings.ExitContactRecovery)limits=[..limits,
            "Explicit reviewed exit contact recovery: all original source motion is retained through key time "+end+"; only the final window to "+last+" adapts the selected body curves to the frozen reference first frame with zero endpoint slope.",
            "Recovered native support dependency paths: "+string.Join(", ",recovered)+". Existing baked support preservation is explicitly released only after the join in this window; no contact, clearance, release-trajectory or coupled-solver acceptance is claimed.",
            "Protected roots, fixed/authored pins and protected intervals are not released. Existing contact metadata remains recorded and is not silently removed."];
        else if(settings.PreserveContacts)limits=[..limits,"Existing contact trajectories are preserved by retaining ALL original curves on their complete bone dependency paths for the whole clip. These protected bones are excluded from endpoint matching. No new contact, release, anchor correction or coupled IK solve is inferred.","Protected contact bones: "+string.Join(", ",contactBones)];
        var retainedContacts=contactBones.Except(recovered,StringComparer.Ordinal).ToArray();
        var receipt=new BoundaryFitReceipt(version,RetargetDependencies.Hash(source),entry is null?null:RetargetDependencies.Hash(entry),exit is null?null:RetargetDependencies.Hash(exit),source.SampleCount,source.Loop,settings,changedChannels,limits){ProtectedContactBones=retainedContacts,RecoveredContactBones=recovered,RecoveryStart=settings.ExitContactRecovery?end:null,RecoveryEnd=settings.ExitContactRecovery?last:null};
        TransitionReference? Ref(BoundaryAnchor? a)=>a is null?null:new(a.Clip.Revision,project.Scene.SkeletonRevision,a.Frame,a.Rate,a.Frozen);
        var recipe=new TransitionRecipe(Ref(entry),Ref(exit),entry is null?0:settings.EntrySamples,exit is null?0:settings.ExitSamples,protectedRanges,RootMotionPolicy.Preserve,settings.Bones,retainedContacts,version){CurveFit=receipt};
        var result=source with{Id=Guid.NewGuid(),Revision=Guid.NewGuid(),Name=source.Name+" (boundary fit)",Tracks=tracks.ToArray(),Transition=recipe,NativeWrite=NativeWriteMode.NewResource,NativeWriteName=null};
        return new(result,changed.ToArray(),receipt);
    }

    // Frozen existing root-policy gates: 0.0002 native translation units,
    // 0.00001 scale and 0.01 degree shortest orientation. Full turns remain
    // source-authored; equivalent Euler spellings do not authorize rewrites.
    static bool ProtectedEndpointMatches(Transform a,Transform b)
    {
        if(a.Channels().Concat(b.Channels()).Any(value=>!float.IsFinite(value)))return false;
        double dx=(double)a.Translation.X-b.Translation.X,dy=(double)a.Translation.Y-b.Translation.Y,dz=(double)a.Translation.Z-b.Translation.Z;
        if(Math.Sqrt(dx*dx+dy*dy+dz*dz)>.0002||Math.Max(Math.Abs((double)a.Scale.X-b.Scale.X),Math.Max(Math.Abs((double)a.Scale.Y-b.Scale.Y),Math.Abs((double)a.Scale.Z-b.Scale.Z)))>.00001)return false;
        Quaternion Rotation(Vector3 value)=>Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationX(value.X*MathF.PI/180)*Matrix4x4.CreateRotationY(value.Y*MathF.PI/180)*Matrix4x4.CreateRotationZ(value.Z*MathF.PI/180));
        var qa=Rotation(a.Rotation);var qb=Rotation(b.Rotation);
        double normA=Math.Sqrt((double)qa.X*qa.X+(double)qa.Y*qa.Y+(double)qa.Z*qa.Z+(double)qa.W*qa.W);
        double normB=Math.Sqrt((double)qb.X*qb.X+(double)qb.Y*qb.Y+(double)qb.Z*qb.Z+(double)qb.W*qb.W);
        double dot=Math.Abs(((double)qa.X*qb.X+(double)qa.Y*qb.Y+(double)qa.Z*qb.Z+(double)qa.W*qb.W)/(normA*normB));
        double degrees=2*Math.Acos(Math.Min(1,dot))*180/Math.PI;
        return double.IsFinite(degrees)&&degrees<=.01;
    }

    // Analytic per-frame derivative of the same Hermite/linear/step and native
    // loop bridge sampled by Curve. Exact-time duplicate sides stay distinct.
    public static float Derivative(Curve? curve,double frame,bool outgoing=true)
    {
        if(curve is null||curve.Keys.Length<2||curve.Interpolation==Interpolation.Step)return 0;
        var keys=curve.Keys;int first=Array.FindIndex(keys,k=>k.Frame==frame),last=Array.FindLastIndex(keys,k=>k.Frame==frame);
        if(first>=0)
        {
            if(outgoing&&last<keys.Length-1)return curve.Interpolation==Interpolation.Hermite?keys[last].OutTangent:(keys[last+1].Value-keys[last].Value)/(float)(keys[last+1].Frame-frame);
            if(!outgoing&&first>0)return curve.Interpolation==Interpolation.Hermite?keys[first].InTangent:(keys[first].Value-keys[first-1].Value)/(float)(frame-keys[first-1].Frame);
            if(curve.LoopFrameLimit is null)return 0;
        }
        Key a,b;double at=frame;
        if(frame<=keys[0].Frame||frame>=keys[^1].Frame)
        {
            if(curve.LoopFrameLimit is not {} limit)return 0;
            a=keys[^1];b=keys[0] with{Frame=limit+keys[0].Frame};if(frame<=keys[0].Frame)at+=limit;
        }
        else{int next=Array.FindIndex(keys,k=>k.Frame>frame);a=keys[next-1];b=keys[next];}
        double dt=b.Frame-a.Frame;if(dt<=0)return 0;double u=(at-a.Frame)/dt;
        if(curve.Interpolation==Interpolation.Linear)return (b.Value-a.Value)/(float)dt;
        return (float)(((6*u*u-6*u)*a.Value+(-6*u*u+6*u)*b.Value)/dt+(3*u*u-4*u+1)*a.OutTangent+(3*u*u-2*u)*b.InTangent);
    }
}
