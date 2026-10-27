using System.Numerics;

namespace FighterPort.Core;

public sealed record BoundaryAnchor(AnimationClip Clip,string SkeletonFingerprint,double Frame,bool Frozen=true,double Rate=1);
public sealed record BoundaryFitSettings(int EntrySamples,int ExitSamples,TransformComponents Components,
    string[] Bones,ProtectedInterval[] Protected,bool PreserveRootTranslation=true,bool NearestEquivalentAngles=true)
{
    public bool MatchReferenceControls {get;init;}
    public bool OptimizeRotationPaths {get;init;}
    public bool PreserveContacts {get;init;}
    public string[] ContactBones {get;init;}=[];
    public bool ExitContactRecovery {get;init;}
    public bool BoundaryContactRecovery {get;init;}
    public BoundaryFloorSettings? FloorSettings {get;init;}
}
public sealed record BoundaryRecoveryWindow(string Side,int Start,int End,BoundaryFloorReceipt? Floor);
public sealed record BoundaryFitReceipt(string Version,string Source,string? Entry,string? Exit,int SampleCount,bool Loop,
    BoundaryFitSettings Settings,int ChangedChannels,string[] Limits)
{
    public string[] MatchedReferenceControls {get;init;}=[];
    public string[] ProtectedContactBones {get;init;}=[];
    public string[] RecoveredContactBones {get;init;}=[];
    public int? RecoveryStart {get;init;}
    public int? RecoveryEnd {get;init;}
    public string ReferenceRootPolicy {get;init;}="";
    public BoundaryFloorReceipt? FloorRecovery {get;init;}
    public BoundaryRecoveryWindow[] RecoveryWindows {get;init;}=[];
    public string RotationInterpolation {get;init;}="native-euler-hermite-1";
    public double RotationPathDegrees {get;init;}
    public double LegacyRotationPathDegrees {get;init;}
}
public sealed record BoundaryFitResult(AnimationClip Clip,BoneTrack[] Changed,BoundaryFitReceipt Receipt);

// Offline FK curve editing on the existing rig. This is not cross-rig
// retargeting, a contact solver, a time edit or a claim of game/state acceptance.
public static class BoundaryFitting
{
    public const string Version="fixed-duration-fk-hermite-2";
    public const string ContactVersion="contact-trajectory-preserve-2";
    public const string ExitRecoveryVersion="contact-exit-recovery-3";
    public const string FloorVersion="contact-floor-recovery-2";
    public const string BoundaryRecoveryVersion="contact-boundary-recovery-1";
    public const string BoundaryFloorVersion="contact-boundary-floor-1";
    public const string SourceRootReferencePolicy="authentic reference body under source-authored protected locals at each time";
    public static BoundaryFitResult Prepare(FighterProject project,AnimationClip source,BoundaryAnchor? entry,BoundaryAnchor? exit,BoundaryFitSettings settings)
    {
        string fingerprint=AnimationControls.Fingerprint(project.Scene);int last=source.SampleCount-1;
        var controls=project.ControlRigs.FirstOrDefault(r=>r.SkeletonFingerprint==fingerprint);
        var protectedMotion=controls is null?new HashSet<string>(StringComparer.Ordinal):
            AnimationControls.ProtectedMotionBones(project.Scene,controls).Select(i=>project.Scene.Bones[i].Name).ToHashSet(StringComparer.Ordinal);
        var matchedControls=settings.MatchReferenceControls?BoundaryReferenceControls.Resolve(project.Scene,controls):[];
        protectedMotion.ExceptWith(matchedControls);
        string referencePolicy=settings.MatchReferenceControls?BoundaryReferenceControls.Policy:SourceRootReferencePolicy;
        if(matchedControls.Any(name=>!settings.Bones.Contains(name,StringComparer.Ordinal)))
            throw new InvalidDataException("Select every reviewed reference control when control fitting is enabled.");
        if(source.SampleCount<3||source.SampleCount>100000)throw new InvalidDataException("Boundary fitting requires 3–100000 existing samples.");
        if(entry is null&&exit is null)throw new InvalidDataException("Choose an entry or exit reference.");
        if(settings.Components==TransformComponents.None||(settings.Components&~TransformComponents.All)!=0)throw new InvalidDataException("Choose supported transform components.");
        bool recovery=settings.ExitContactRecovery||settings.BoundaryContactRecovery;
        if(settings.ExitContactRecovery&&settings.BoundaryContactRecovery)throw new InvalidDataException("Choose either legacy exit recovery or start/end recovery, not both.");
        if(settings.BoundaryContactRecovery&&(!settings.PreserveContacts||settings.Components!=TransformComponents.All||entry is not null&&entry is not {Frozen:true,Frame:0}||exit is not null&&exit is not {Frozen:true,Frame:0}))
            throw new InvalidDataException("Start/end contact recovery requires all S/R/T components, preserved contacts and frozen first-frame references.");
        if(settings.ExitContactRecovery&&(entry is not null||exit is not {Frozen:true,Frame:0}||!settings.PreserveContacts||settings.Components!=TransformComponents.All))
            throw new InvalidDataException("Exit contact recovery requires an exit-only frozen first-frame reference, all S/R/T components and existing contact preservation.");
        if(recovery&&source.Loop)
            throw new NotSupportedException("Exit contact recovery currently supports non-looping source animations only. Stored-loop wrap dependencies require a separate verified recovery adapter; the original animation and loop flag remain unchanged.");
        if(settings.FloorSettings is not null&&!recovery)
            throw new InvalidDataException("Reviewed floor recovery requires explicit exit contact recovery.");
        if(settings.Bones.Length==0||settings.Bones.Distinct(StringComparer.Ordinal).Count()!=settings.Bones.Length||settings.Bones.Any(n=>!project.Scene.Bones.Any(b=>b.Name==n)))throw new InvalidDataException("Choose existing bones from this exact rig revision.");
        int start=entry is null?0:settings.EntrySamples-1,end=exit is null?last:source.SampleCount-settings.ExitSamples;
        if(entry is not null&&(settings.EntrySamples<2||settings.EntrySamples>=source.SampleCount)||exit is not null&&(settings.ExitSamples<2||settings.ExitSamples>=source.SampleCount)||start>=end)throw new InvalidDataException("Fit ranges must have at least two samples, leave a protected middle, and must not overlap.");
        var protectedRanges=settings.Protected.Concat(project.Moves.SelectMany(m=>m.Events)).ToArray();
        if(protectedRanges.Any(p=>!double.IsFinite(p.Start)||!double.IsFinite(p.End)||p.Start<0||p.End<p.Start||p.End>last||entry is not null&&p.Start<start||exit is not null&&p.End>end))throw new InvalidDataException("A fit range intersects a protected frame/event interval. Shorten the fit or review that protection explicitly.");
        var contactBones=ContactBoundaryProtection.Resolve(project,source,settings);
        if(settings.MatchReferenceControls&&!recovery&&matchedControls.Any(name=>contactBones.Contains(name,StringComparer.Ordinal)))
            throw new NotSupportedException("Reference controls affect preserved support trajectories. Use reviewed exit recovery or retain the original control curves.");
        if(settings.MatchReferenceControls&&!recovery&&(project.BonePins??[]).Any(pin=>(pin.Clip is null||pin.Clip==source.Id)&&matchedControls.Any(name=>PoseGeometry.Ancestor(project.Scene,Array.FindIndex(project.Scene.Bones,b=>b.Name==name),pin.Bone))))
            throw new NotSupportedException("An existing-bone pin intersects reference control fitting; no anchor is released.");
        if(project.NativeLayers.Any(l=>l.Kind!="SCN0"))throw new NotSupportedException("Composed companion layers require a synchronized fitting adapter; the body-only operation cannot rewrite that composition.");
        void Anchor(BoundaryAnchor? a)
        {
            if(a is null)return;
            if(a.SkeletonFingerprint!=fingerprint)throw new InvalidDataException("A transition reference belongs to another native rig revision. Retarget it explicitly first.");
            if(!double.IsFinite(a.Frame)||a.Frame<0||a.Frame>a.Clip.LastSampleFrame||!double.IsFinite(a.Rate)||a.Rate<=0||a.Rate>4)throw new InvalidDataException("Choose a valid reference frame and a positive reference speed of at most 4.");
            a.Clip.Rate.Validate();
        }
        Anchor(entry);Anchor(exit);source.Rate.Validate();
        if(settings.OptimizeRotationPaths&&!settings.NearestEquivalentAngles)throw new InvalidDataException("Smooth rotation fitting requires equivalent-angle handling.");
        var rotationBudget=new BoundaryRotationPath.Budget();
        var recovered=ContactBoundaryProtection.ResolveExitRecovery(project,source,settings,controls,protectedMotion,contactBones,end,entry is null?null:start);
        if(recovery)
            foreach(var bone in project.Scene.Bones.Where(b=>protectedMotion.Contains(b.Name)))
                if(bone.Rest.Channels().Any(v=>!float.IsFinite(v))||source.Tracks.Where(t=>t.Bone==bone.Name).SelectMany(t=>t.Channels.OfType<Curve>()).SelectMany(c=>c.Keys)
                    .Any(k=>!double.IsFinite(k.Frame)||!float.IsFinite(k.Value)||!float.IsFinite(k.InTangent)||!float.IsFinite(k.OutTangent)))
                    throw new InvalidDataException("A source-authored protected motion control has nonfinite data: "+bone.Name);
        var tracks=source.Tracks.ToList();var changed=new List<BoneTrack>();int changedChannels=0;
        foreach(string name in settings.Bones)
        {
            if(protectedMotion.Contains(name))continue;
            if(contactBones.Contains(name)&&!recovered.Contains(name))continue;
            var bone=project.Scene.Bones.Single(b=>b.Name==name);var rest=bone.Rest.Channels();
            var original=source.Tracks.SingleOrDefault(t=>t.Bone==name)??new BoneTrack(name,new Curve?[9]);
            if(original.Rotation is not null&&(settings.Components&TransformComponents.Rotation)!=0)throw new NotSupportedException("Quaternion boundary fitting requires a rotation-space adapter. Euler native curves remain unchanged.");
            // Choose a complete equivalent XYZ triplet before editing any axis.
            // The coupled (x+180, 180-y, z+180) family avoids artificial flips
            // that cannot be fixed by independently wrapping each axis.
            (Vector3 Values,bool Alternate)? RotationEndpoint(BoundaryAnchor? anchor,int join,bool isEntry)
            {
                if(anchor is null||!settings.NearestEquivalentAngles||(settings.Components&TransformComponents.Rotation)==0)return null;
                var reference=anchor.Clip.Tracks.SingleOrDefault(t=>t.Bone==name);
                if(reference?.Rotation is not null)throw new NotSupportedException("The reference has quaternion rotation; use an explicit rotation adapter.");
                float R(int c)=>reference?.Channels[c]?.Sample(anchor.Frame)??rest[c];
                float S(int c)=>settings.OptimizeRotationPaths&&!isEntry?original.Channels[c]?.Keys.LastOrDefault(k=>k.Frame==join)?.Value??original.Channels[c]?.Sample(join)??rest[c]:original.Channels[c]?.Sample(join)??rest[c];
                if(!settings.OptimizeRotationPaths)return NearestRotation(new(R(3),R(4),R(5)),new(S(3),S(4),S(5)));
                float RS(int c)=>anchor.Frozen?0:Derivative(reference?.Channels[c],anchor.Frame)*(float)(anchor.Rate*anchor.Clip.Rate.FramesPerSecond/source.Rate.FramesPerSecond);
                float JS(int c)=>Derivative(original.Channels[c],join,!isEntry);
                return BoundaryRotationPath.Choose(new(R(3),R(4),R(5)),new(S(3),S(4),S(5)),new(RS(3),RS(4),RS(5)),new(JS(3),JS(4),JS(5)),isEntry?join:last-join,isEntry,rotationBudget);
            }
            var entryRotation=RotationEndpoint(entry,start,true);var exitRotation=RotationEndpoint(exit,end,false);
            var channels=original.Channels.ToArray();bool any=false;
            for(int channel=0;channel<9;channel++)
            {
                var component=channel<3?TransformComponents.Scale:channel<6?TransformComponents.Rotation:TransformComponents.Translation;
                if((settings.Components&component)==0||settings.PreserveRootTranslation&&bone.Parent<0&&!matchedControls.Contains(name,StringComparer.Ordinal)&&component==TransformComponents.Translation)continue;
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
                    var rotation=at==0?entryRotation:exitRotation;
                    if(rotation.HasValue&&channel is >=3 and <6)value=rotation.Value.Values[channel-3];
                    float slope=a.Frozen?0:Derivative(referenceCurve,a.Frame)*(float)(a.Rate*a.Clip.Rate.FramesPerSecond/source.Rate.FramesPerSecond);
                    if(rotation.HasValue&&rotation.Value.Alternate&&channel==4)slope=-slope;
                    if(!float.IsFinite(value)||!float.IsFinite(slope))throw new InvalidDataException("The reference produced a nonfinite endpoint.");
                    return new(at,value,slope,slope);
                }
                if(entry is not null){Join(start);keys.Add(Endpoint(entry,0,start));}
                if(exit is not null){Join(end);keys.Add(Endpoint(exit,last,end));}
                channels[channel]=new(keys.OrderBy(k=>k.Frame).ToArray(),Interpolation.Hermite,curve?.LoopFrameLimit??(source.Loop?source.SampleCount:null));changedChannels++;any=true;
            }
            if(!any)continue;
            if(settings.OptimizeRotationPaths&&(settings.Components&TransformComponents.Rotation)!=0)
            {
                if(entry is not null)BoundaryRotationPath.Refine(channels,0,start,rotationBudget);
                if(exit is not null)BoundaryRotationPath.Refine(channels,end,last,rotationBudget);
            }
            var updated=original with{Channels=channels};int index=tracks.FindIndex(t=>t.Bone==name);if(index<0)tracks.Add(updated);else tracks[index]=updated;changed.Add(updated);
        }
        if(changed.Count==0)throw new InvalidDataException("The mask, root policy and protected contact paths leave no eligible channels to fit. Choose unprotected bones; repositioning these contacts needs a coupled solver. Protected: "+string.Join(", ",contactBones));
        BoundaryFloorReceipt? floorReceipt=null;
        var windows=new List<BoundaryRecoveryWindow>();
        if(settings.BoundaryContactRecovery)
        {
            if(entry is not null)windows.Add(new("entry",0,start,null));
            if(exit is not null)windows.Add(new("exit",end,last,null));
        }
        if(settings.FloorSettings is not null)
        {
            var floorWindows=settings.BoundaryContactRecovery?windows.ToArray():[new BoundaryRecoveryWindow("exit",end,last,null)];
            if(floorWindows.Sum(w=>3L*(w.End-w.Start+1))>8192)throw new NotSupportedException("Combined boundary floor recovery exceeds the 8192 integral body-key budget.");
            if(floorWindows.Sum(w=>BoundaryFloorRecovery.PlannedEvaluations(w.End-w.Start))>200000)throw new NotSupportedException("Combined boundary floor recovery exceeds the 200000 pose-evaluation budget.");
            int evaluations=0;
            foreach(var w in floorWindows)
            {
                var corrected=BoundaryFloorRecovery.Prepare(project,source,source with{Tracks=tracks.ToArray()},w.Side=="entry"?entry!:exit!,settings,controls!,protectedMotion,w.Start,w.End);
                tracks=corrected.Clip.Tracks.ToList();evaluations+=corrected.Receipt.Evaluations;
                if(evaluations>200000)throw new NotSupportedException("Combined floor recovery exceeded its evaluation budget; no result was published.");
                if(settings.BoundaryContactRecovery)windows[windows.FindIndex(v=>v.Side==w.Side)]=w with{Floor=corrected.Receipt};
                else floorReceipt=corrected.Receipt;
                foreach(var track in corrected.Changed){int at=changed.FindIndex(t=>t.Bone==track.Bone);if(at<0)changed.Add(track);else changed[at]=track;}
            }
        }
        string[] limits=[settings.FloorSettings is null?"FK component curves only; no contact, clearance, joint-limit, world-trajectory or game-state acceptance.":"FK component curves with bounded reviewed-plane clearance measurements only; no fixed-contact, joint-limit, whole-trajectory or game-state acceptance.","Existing duration, rate, loop flag, middle curves and unselected channels are retained; inspect the loop seam.",settings.NearestEquivalentAngles?"Reference XYZ Euler endpoints use complete equivalent triplets from both families, including coupled 180-degree and whole 360-degree turns; alternate-family middle-axis tangents reverse. Nearest selection or the recorded orientation-path policy determines the branch. Authored action and control curves stay unchanged.":"Reference Euler channel values retain their authored turn counts, including full spins.","Native companion resources are not copied into this independent body animation."];
        string version=settings.BoundaryContactRecovery?(settings.FloorSettings is null?BoundaryRecoveryVersion:BoundaryFloorVersion):floorReceipt is not null?FloorVersion:settings.ExitContactRecovery?ExitRecoveryVersion:settings.PreserveContacts?ContactVersion:Version;
        if(protectedMotion.Count>0)limits=[..limits,"Source-authored motion controls retain their original curves: "+string.Join(", ",protectedMotion.Order(StringComparer.Ordinal))];
        if(settings.ExitContactRecovery)limits=[..limits,
            "Explicit reviewed exit contact recovery: all original source motion is retained through key time "+end+"; only the final window to "+last+" adapts the selected body curves to the frozen reference first frame with zero endpoint slope.",
            "Recovered native support dependency paths: "+string.Join(", ",recovered)+". Existing baked support preservation is explicitly released only after the join in this window; no contact, clearance, release-trajectory or coupled-solver acceptance is claimed.",
            "Retained motion controls, fixed/authored pins and protected intervals are not released. Existing contact metadata remains recorded and is not silently removed."];
        else if(settings.BoundaryContactRecovery)limits=[..limits,"Explicit reviewed start/end hip/leg recovery inside the recorded windows only. Shared joins and the complete original middle remain unchanged; frozen first reference poses have zero endpoint slopes. Baked support metadata is retained; adaptation does not promise planted contacts.","Retained motion-position controls, authored/editor pins and protected intervals are not released."];
        else if(settings.PreserveContacts)limits=[..limits,"Existing contact trajectories are preserved by retaining ALL original curves on their complete bone dependency paths for the whole clip. These protected bones are excluded from endpoint matching. No new contact, release, anchor correction or coupled IK solve is inferred.","Protected contact bones: "+string.Join(", ",contactBones)];
        var retainedContacts=contactBones.Except(recovered,StringComparer.Ordinal).ToArray();
        if(recovery)limits=[..limits,"Reference placement policy: "+referencePolicy+"."];
        if(matchedControls.Length>0)limits=[..limits,"Explicit boundary control fitting: "+string.Join(", ",matchedControls)+". Only reviewed motion-position curves stay source-authored; original control/action keys outside the fit windows remain exact."];
        if(settings.FloorSettings is not null)limits=[..limits,"Reviewed floor recovery uses every vertex with any positive reviewed foot-path influence, all original weights and authentic inverse binds. It prevents added penetration relative to the recorded per-foot original/reference baseline, not absolute zero penetration.",
            "Body translation correction only, within explicitly selected windows; integer Hermite keys, zero correction and slope at both ends. Maximum lift is 20% of the shorter reviewed leg chain. Fixed pins remain hard failures.",
            "Clearance is checked at the receipt's bounded native-frame witnesses; this is not a continuous-time certificate, coupled contact solver, broad character or game acceptance."];
        var receipt=new BoundaryFitReceipt(version,RetargetDependencies.Hash(source),entry is null?null:RetargetDependencies.Hash(entry),exit is null?null:RetargetDependencies.Hash(exit),source.SampleCount,source.Loop,settings,changedChannels,limits){MatchedReferenceControls=matchedControls,ProtectedContactBones=retainedContacts,RecoveredContactBones=recovered,RecoveryStart=settings.ExitContactRecovery?end:null,RecoveryEnd=settings.ExitContactRecovery?last:null,RecoveryWindows=windows.ToArray(),ReferenceRootPolicy=recovery||settings.MatchReferenceControls?referencePolicy:"",FloorRecovery=floorReceipt};
        if(settings.OptimizeRotationPaths)receipt=receipt with{RotationInterpolation=BoundaryRotationPath.Version,RotationPathDegrees=rotationBudget.Travel,LegacyRotationPathDegrees=rotationBudget.LegacyTravel,Limits=[..limits,"Orientation-aware native XYZ Hermite paths compare equivalent endpoint families and one integral FK midpoint by measured local angular travel and peak speed. Authored joins, tangents and middle remain exact. Equivalent Euler labels may differ by 180/360 degrees. This is bounded branch selection, not IK, a global path optimum or contact acceptance."]};
        TransitionReference? Ref(BoundaryAnchor? a)=>a is null?null:new(a.Clip.Revision,project.Scene.SkeletonRevision,a.Frame,a.Rate,a.Frozen);
        var recipe=new TransitionRecipe(Ref(entry),Ref(exit),entry is null?0:settings.EntrySamples,exit is null?0:settings.ExitSamples,protectedRanges,RootMotionPolicy.Preserve,settings.Bones,retainedContacts,version){CurveFit=receipt};
        var result=source with{Id=Guid.NewGuid(),Revision=Guid.NewGuid(),Name=source.Name+" (boundary fit)",Tracks=tracks.ToArray(),Transition=recipe,NativeWrite=NativeWriteMode.NewResource,NativeWriteName=null};
        return new(result,changed.ToArray(),receipt);
    }

    static (Vector3 Values,bool Alternate) NearestRotation(Vector3 reference,Vector3 join)
    {
        if(new[]{reference.X,reference.Y,reference.Z,join.X,join.Y,join.Z}.Any(v=>!float.IsFinite(v)))
            throw new InvalidDataException("Native rotation branch selection requires finite reference and join channels.");
        float Near(double value,float toward)=>(float)(value+360*Math.Round(((double)toward-value)/360,MidpointRounding.ToEven));
        var primary=new Vector3(Near(reference.X,join.X),Near(reference.Y,join.Y),Near(reference.Z,join.Z));
        var alternate=new Vector3(Near((double)reference.X+180,join.X),Near(180d-reference.Y,join.Y),Near((double)reference.Z+180,join.Z));
        double Distance(Vector3 value)=>Math.Pow((double)value.X-join.X,2)+Math.Pow((double)value.Y-join.Y,2)+Math.Pow((double)value.Z-join.Z,2);
        // Ties retain the original reference family. This chooses native Euler
        // curves; it does not rewrite the authored join or solve a quaternion path.
        bool other=Distance(alternate)<Distance(primary);var selected=other?alternate:primary;
        if(new[]{selected.X,selected.Y,selected.Z}.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Equivalent native rotation is outside the finite channel range.");
        return(selected,other);
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
