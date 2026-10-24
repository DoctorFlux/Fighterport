using System.Numerics;
using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

sealed record FittingOptions(bool AutomaticFitting=true,bool AllowOverlap=true,int RootPolicy=0,int Scene=1,int SurfaceMode=0,float? SourceFloor=null,float? TargetFloor=null,float SourceEdge=0,float TargetEdge=0,bool ReverseEdge=false,float? LimbTolerancePercent=null,double TrimStartSeconds=0,double? TrimEndSeconds=null,double ExportSpeed=1,bool LossesReviewed=false);
sealed partial class ControlSession
{
    FighterProject? fittingSource,fittingTarget;ControlRigSetup? fittingSourceRig,fittingTargetRig;
    string? fittingDependency;string[] fittingMissing=[];string fittingContext="Authentic native source; no companion contact context supplied";
    string? fittingContextPath,fittingContextFingerprint;
    async Task CheckFittingContext()
    {
        if(fittingContextPath is not null)
        {
            var companion=await ProjectStore.LoadAsync(fittingContextPath);
            if(RetargetDependencies.Hash(companion)!=fittingContextFingerprint||ProjectStore.ValidateSources(companion).Length>0)
                throw new InvalidDataException("The companion source context or its preserved native inputs changed. Reload and prepare a new fitting review; stale results were discarded.");
        }
    }
    AnimationClip? fittingClip;RetargetBake? fittingBake;ContactValidation? fittingValidation;Diagnostic[] fittingDiagnostics=[];string[] fittingLosses=[];FittingOptions? fittingOptions;
    async Task<object> Fitting(Request r)
    {
        if(r.Operation=="fitting-context-list")
        {
            var context=await ProjectStore.LoadAsync(r.Path!);
            return new{Ok=true,Fitting=new{ContextClips=context.Clips.Select(c=>new{Id=c.Id.ToString(),c.Name,c.SampleCount}).ToArray(),Context=$"{context.Contacts.Length} contact intervals; {context.NativeLayers.Length} native layers; {context.LibrarySources.Length} source contexts retained"}};
        }
        if(r.Operation=="fitting-load")
        {
            fittingSource=null;fittingTarget=null;fittingBake=null;fittingValidation=null;fittingOptions=null;fittingDiagnostics=[];fittingLosses=[];fittingContextPath=null;fittingContextFingerprint=null;
            var native=await adapter.ImportAsync(r.SourceModel!,r.Motion!);var target=await adapter.ImportModelAsync(r.Model!);fittingClip=native.Clips.Single();
            fittingContext="Authentic native source; no companion contact context supplied";
            if(!string.IsNullOrEmpty(r.Path))
            {
                // A companion is retained whole. Never repair mismatch by replacing
                // its scene, removing tracks, dropping contacts or flattening layers.
                var companion=await ProjectStore.LoadAsync(r.Path);
                if(ProjectStore.ValidateSources(companion).Length>0)throw new InvalidDataException("The companion has missing or changed preserved native inputs. Restore its authentic inputs before fitting.");
                fittingContextPath=r.Path;fittingContextFingerprint=RetargetDependencies.Hash(companion);
                if(AnimationControls.DeformChecksum(companion.Scene)!=AnimationControls.DeformChecksum(native.Scene))throw new InvalidDataException("The companion context does not match the exact native deform revision. Original context was retained; no geometry was normalized.");
                var selected=companion.Clips.Single(c=>c.Id==Guid.Parse(r.ClipKey!));
                string MotionKey(AnimationClip c)=>RetargetDependencies.Hash(new{c.SampleCount,c.Rate,c.Loop,c.Tracks});
                if(MotionKey(selected)!=MotionKey(native.Clips.Single()))throw new InvalidDataException("The companion animation differs from this authentic native clip. Select its matching native source; context was not merged.");
                native=companion;fittingClip=selected;fittingContext=$"Exact companion context retained: {companion.Clips.Length} clips, {companion.Contacts.Count(c=>c.Clip==selected.Id)} selected-clip contact intervals, {companion.NativeLayers.Length} native layers, {companion.LibrarySources.Length} source contexts. Saved review references this external companion; it does not embed a portable copy.";
            }
            if(!string.IsNullOrWhiteSpace(r.SourceAssociation)&&Guid.TryParse(r.SourceRegistration,out var registration))native=native with{LibraryRegistrationId=native.LibraryRegistrationId??registration,SourceAssociationEvidence=native.SourceAssociationEvidence??r.SourceAssociation};
            fittingSource=native;fittingTarget=target;
            fittingSourceRig=ReviewedBinding(native,native.ControlRigs.FirstOrDefault(s=>s.SkeletonFingerprint==AnimationControls.Fingerprint(native.Scene))??BrawlControlProfiles.SetUp(native),r.SourceBinding);
            fittingTargetRig=ReviewedBinding(target,BrawlControlProfiles.SetUp(target),r.TargetBinding);
            fittingMissing=fittingClip!.Tracks.Where(t=>t.HasKeys&&!native.Scene.Bones.Any(b=>b.Name==t.Bone)).Select(t=>t.Bone).ToArray();
            fittingDependency=RetargetDependencies.Hash(new{Source=native,Clip=fittingClip,SourceRig=fittingSourceRig,Target=target,TargetRig=fittingTargetRig});
            return FittingReply("Loaded");
        }
        var source=fittingSource??throw new InvalidOperationException("Load a source-bound fitting review first.");
        var destination=fittingTarget!;var a=fittingSourceRig!;var b=fittingTargetRig!;
        await CheckFittingContext();
        if(r.Operation=="fitting-check")
        {
            if(r.State!=fittingDependency||fittingOptions is not null&&r.Fitting!=fittingOptions)throw new InvalidOperationException("Fitting settings or source dependencies changed; prepare a new review before saving its receipt.");
            return new{Ok=true};
        }
        if(r.Operation=="fitting-prepare")
        {
            fittingBake=null;fittingValidation=null;fittingDiagnostics=[];fittingLosses=[];fittingOptions=r.Fitting??new();
            var o=fittingOptions;
            if(o.Scene<0||o.Scene>2||!Enum.IsDefined((SupportSurfaceMode)o.SurfaceMode)||!Enum.IsDefined((RootMotionPolicy)o.RootPolicy)||new[]{o.SourceFloor??0,o.TargetFloor??0,o.SourceEdge,o.TargetEdge,o.LimbTolerancePercent??1}.Any(v=>!float.IsFinite(v))||o.LimbTolerancePercent is <=0 or >100)throw new InvalidDataException("Choose finite scene coordinates, a supported surface/root mode and a positive limb tolerance of at most 100 percent.");
            var timing=new RetargetTiming{TrimStartSeconds=o.TrimStartSeconds,TrimEndSeconds=o.TrimEndSeconds,ExportSpeed=o.ExportSpeed};timing.Validate(fittingClip!);
            if(a.Axes is null||b.Axes is null){fittingDiagnostics=[new("RIG_BINDING_REVIEW","Review source and target coordinates before advanced fitting.")];return FittingReply("Review");}
            var ground=new GroundContactSettings{ExplicitSurface=o.Scene>0,SurfaceMode=(SupportSurfaceMode)o.SurfaceMode,SourceFloor=o.SourceFloor??(o.Scene>0?0:null),TargetFloor=o.TargetFloor??(o.Scene>0?0:null),LimbHeightFraction=o.LimbTolerancePercent/100,
                SourceEdge=o.Scene==2?new(a.Axes.Forward*(o.ReverseEdge?-1:1),o.SourceEdge):null,TargetEdge=o.Scene==2?new(b.Axes.Forward*(o.ReverseEdge?-1:1),o.TargetEdge):null};
            ground.SourceEdge?.Validate(a.Axes.Up);ground.TargetEdge?.Validate(b.Axes.Up);
            var output=await Task.Run(()=>RetargetService.Bake(source,fittingClip!,a,destination,b,(RootMotionPolicy)o.RootPolicy,useContacts:true,ground:ground,fitting:new(){AutomaticFitting=o.AutomaticFitting,AutomaticContacts=true,VolumePolicy=o.AllowOverlap?BodyVolumePolicy.ReportOverlap:BodyVolumePolicy.RequireVertexClearance},timing:timing));
            fittingBake=output.Value;fittingDiagnostics=output.Diagnostics;fittingLosses=output.LostChannels;
            if(fittingBake is {} bake)fittingValidation=await Task.Run(()=>RetargetAcceptance.Measure(destination,b,bake));
            await CheckFittingContext();
            return FittingReply(fittingBake is null?"Unresolved":"Review");
        }
        if(r.Operation=="fitting-apply")
        {
            // Native publication is a separate authoring operation. This review
            // endpoint never converts an unresolved draft into an animation.
            throw new InvalidOperationException("Advanced fitting is a review workflow. Apply requires resolved motion/contact, native and displayed acceptance; use original Next retargeting for the ordinary authoring path.");
        }
        throw new NotSupportedException("Unknown fitting operation.");
    }
    object FittingReply(string state)
    {
        var source=fittingSource!;var target=fittingTarget!;var clip=fittingClip!;
        var json=new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false};
        var receipt=JsonSerializer.Serialize(new{Version=1,Algorithm=RetargetService.Version,Dependency=fittingDependency,State=state,Draft=true,Options=fittingOptions,Context=fittingContext,ContextFingerprint=fittingContextFingerprint,MissingSourceBones=fittingMissing,Validation=fittingValidation,Diagnostics=fittingDiagnostics,Losses=fittingLosses,Recipe=fittingBake?.Clip.Retarget},json);
        var summary=new List<string>();
        if(fittingValidation is {} validation)summary.Add($"Contact / floor measurements: {validation.Separation:G5} / {validation.Penetration:G5} native units. Bounds {validation.PositionBound:G5} / {validation.PenetrationBound:G5}. Measurements do not resolve suspended contact acceptance.");
        if(fittingBake?.Clip.Retarget is {} recipe)
        {
            if(recipe.ActionFidelity is {} action)summary.Add($"Action correction: {action.MaximumDegrees:G5} degrees; worst frame {action.WorstFrame:G5}, joint {action.Bone}. {(action.Passed?"Sampled action bounds pass; other gates remain":"Action bounds unresolved")}.");
            if(recipe.Pipeline?.BodyReach is {} reach)summary.Add($"Body-relative reach: {reach.Chains.Length} reviewed native paths; {reach.SurfaceRelationships} source-surface relationships. These are soft goals, not contacts.");
            if(recipe.Pipeline?.SceneBody is {} scene)summary.Add($"Body / scene placement: maximum correction {scene.MaximumCorrection:G5} native units. Measured vertex overlap {scene.MaximumInitialPenetration:G5} to {scene.MaximumFinalPenetration:G5}. Policy: {scene.VolumePolicy}. Support accuracy remains unverified.");
            if(recipe.Pipeline?.BodyTrajectory is {} trajectory)summary.Add($"Coupled trajectory: {trajectory.Knots} knots, {trajectory.Collocations} observations. Soft cost {trajectory.InitialCost:G5} to {trajectory.FinalCost:G5}. {trajectory.Status}; this does not establish convergence or contact acceptance.");
            if(recipe.Pipeline?.Reconstruction is {} representation)summary.Add($"Curve representation: {(representation.Passed?"sampled bounds pass":"bounds unresolved")}; {representation.AddedFractionalKeys?.ToString()??"unrecorded"} added fractional keys. The native review preview is sampled and cannot be exported as this exact result.");
        }
        // At most 121 prepared native-frame samples. These curves are a readonly
        // review proxy, never a writer or evidence of exact native representation.
        TrackView[] preview=[];
        if(fittingBake is {} bake)
        {
            int last=bake.Clip.SampleCount-1;var times=Enumerable.Range(0,Math.Min(120,last)+1).Select(i=>last==0?0:(int)Math.Round(i*last/(double)Math.Min(120,last))).Distinct().ToArray();
            preview=bake.Clip.Tracks.Select(track=>new TrackView(track.Bone,track.Channels.Select(c=>c is null?[]:times.Select(t=>new KeyView(t,c.Sample(t),0)).ToArray()).ToArray())).ToArray();
        }
        float[]? Axis(ControlRigSetup? setup,bool up)=>setup?.Axes is {} axes?(up?new[]{axes.Up.X,axes.Up.Y,axes.Up.Z}:new[]{axes.Forward.X,axes.Forward.Y,axes.Forward.Z}):null;
        return new{Ok=true,Fitting=new{State=state,Dependency=fittingDependency,Algorithm=RetargetService.Version,ApplyAllowed=false,MeasuredReady=fittingBake is not null&&fittingValidation is not null&&RetargetAcceptance.Ready(fittingValidation,fittingDiagnostics,fittingLosses,fittingOptions?.LossesReviewed??false),Receipt=receipt,Summary=string.Join("\n",summary),Diagnostics=fittingDiagnostics,Losses=fittingLosses,MissingSourceBones=fittingMissing,SourceName=source.Scene.Name,SourceClip=clip.Name,SourceFrames=clip.SampleCount,TargetFrames=fittingBake?.Clip.SampleCount??0,Context=fittingContext,SourceUp=Axis(fittingSourceRig,true),TargetUp=Axis(fittingTargetRig,true),SourceForward=Axis(fittingSourceRig,false),TargetForward=Axis(fittingTargetRig,false),SourceNeedsReview=fittingSourceRig?.Axes is null,TargetNeedsReview=fittingTargetRig?.Axes is null,PreviewTracks=preview,PreviewLoop=fittingBake?.Clip.Loop??false,PreviewOnly=true}};
    }
}
