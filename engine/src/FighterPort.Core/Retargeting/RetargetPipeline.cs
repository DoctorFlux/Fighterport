using System.Diagnostics;
using System.Numerics;

namespace FighterPort.Core;

public static class RetargetPipeline
{
    public const string Version="native-body-surface-96.2";
    public static OperationResult<RetargetBake> Bake(RetargetRequest request)
    {
        var stages=new List<RetargetStageReceipt>();
        var diagnostics=new List<Diagnostic>{new("RETARGET_BACKEND",RetargetBackendIdentity.CurrentId+" / "+Version+"; single pose-transfer pipeline",false),
            new("RETARGET_POSE_REWRITE_REVIEW","Replacement pose transfer remains a review draft. General body/reach/action, limits, native and displayed release gates remain open; Apply is unavailable.",false)};
        var losses=new List<string>();
        T Stage<T>(string name,string version,Func<T> run){request.Cancellation.ThrowIfCancellationRequested();var timer=Stopwatch.StartNew();var value=run();stages.Add(new(name,version,timer.Elapsed.TotalMilliseconds));return value;}
        try
        {
            var source=request.Source;var clip=request.Clip;var target=request.Target;var settings=request.Fitting??new();settings.Validate();
            if(!source.Clips.Any(c=>c.Id==clip.Id&&c.Revision==clip.Revision))throw new InvalidDataException("Selected motion is not owned by this authentic source revision.");
            if(clip.Tracks.Any(t=>t.HasKeys&&!source.Scene.Bones.Any(b=>b.Name==t.Bone)))throw new InvalidDataException("Source motion has keyed native bones absent from its authentic source model.");
            if(settings.ArmSpacingDegrees!=0||settings.StanceWidth!=1||settings.PelvisHeight!=0||settings.BodyLeanDegrees!=0||settings.FreeHandReach!=1)
                throw new NotSupportedException("Pose preferences are unavailable until calibrated against the replacement body/reach fit. Saved settings are retained; use neutral values for this draft.");
            if(!Enum.IsDefined(request.RootPolicy))throw new NotSupportedException("Unknown root-motion policy.");
            var a=Stage("source rig",RigDescriptor.Version,()=>RigDescriptor.Prepare(source.Scene,request.SourceRig));
            var b=Stage("target rig",RigDescriptor.Version,()=>RigDescriptor.Prepare(target.Scene,request.TargetRig));diagnostics.AddRange(a.Diagnostics);diagnostics.AddRange(b.Diagnostics);
            var transfer=Stage("actor-relative transfer",BaselineMotionTransfer.Version,()=>new BaselineMotionTransfer(request,a,b));
            if(transfer.Mapping.Bones.Any(p=>p.Binding?.RequiresReview==true))diagnostics.Add(new("RIG_BINDING_REVIEW","Inferred detailed-branch correspondences require review; topology/geometry candidates are not verified semantic bindings.",false));
            if(clip.Source?.AssociationEvidence.Contains("pending",StringComparison.OrdinalIgnoreCase)==true&&
                !(source.LibraryRegistrationId is not null&&source.SourceAssociationEvidence is {Length:>0} review&&!review.Contains("pending",StringComparison.OrdinalIgnoreCase)))
                diagnostics.Add(new("SOURCE_ASSOCIATION_REVIEW","The source model/motion association still needs review; a pose or native round trip cannot verify that association.",false));
            diagnostics.Add(new("JOINT_LIMITS_REVIEW","Whole-body joint-limit acceptance has not been rated. Independent reach proposals outside supplied reviewed limits are rejected; this is not a complete constrained solve or joint-limit certification.",false));
            diagnostics.Add(new("RETARGET_COLLISION_REVIEW","Full body/self/prop collision acceptance has not been rated by the replacement pipeline; floor measurement alone does not establish clearance.",false));
            var authored=source.Contacts.Where(c=>c.Clip==clip.Id).ToArray();
            bool unsolved=authored.Any(c=>c.Pins.Length>0)||(request.ContactEdits?.Length??0)>0||(request.Supports?.Length??0)>0;
            if(request.UseContacts||request.Ground is not null||settings.AutomaticContacts||unsolved)
                diagnostics.Add(new("RETARGET_CONTACTS_DISABLED_REVIEW","Contact reconstruction is disabled. Supplied floor/ledge geometry can guide soft body placement; saved contacts, fixed pins and source annotations are not enforced.",false));
            if(unsolved)diagnostics.Add(new("AUTHORED_CONSTRAINTS_NOT_SOLVED","Authored intervals, exact pins or support bindings are preserved but unsolved. No target constraint satisfaction or permitted release is claimed.",false));
            var analysis=SourceEvidence(request,authored);diagnostics.AddRange(analysis.Interactions.Diagnostics);
            var timing=request.Timing??new();timing.Validate(clip);
            var rate=request.TargetRate??target.Clips.FirstOrDefault()?.NativeTarget?.Timebase??target.Clips.FirstOrDefault()?.Source?.Timebase??clip.Rate;rate.Validate();
            bool retimed=timing.TrimStartSeconds!=0||timing.TrimEndSeconds is not null||timing.ExportSpeed!=1;
            if(source.Moves.Any(m=>m.Events.Length>0)&&retimed)throw new NotSupportedException("Protected gameplay event ownership must be resolved before trim/retiming.");
            double count=((timing.TrimEndSeconds??clip.DurationSeconds)-timing.TrimStartSeconds)/timing.ExportSpeed*rate.FramesPerSecond;
            int samples=checked((int)Math.Round(count));if(samples<1||Math.Abs(samples-count)>1e-7)throw new NotSupportedException($"Native output needs {count:G12} samples; nonintegral duration was not rounded.");
            bool loop=clip.Loop&&timing.TrimStartSeconds==0&&timing.TrimEndSeconds is null;
            bool closing=loop||clip.LastKeyFrame>=clip.SampleCount||retimed;
            PoseMotionReconstruction.ValidateResourceBudget(source.Scene,target.Scene,samples,closing,request.Cancellation);
            var reach=Stage("body/reach and scene preparation",BodyReachFitting.Version,()=>new BodyReachFitting(request,a,b,transfer));
            diagnostics.AddRange(reach.Diagnostics);
            bool exact=transfer.ExactRig&&!reach.SceneFitting.ChangesGeometryIdentity&&request.RootPolicy==RootMotionPolicy.Preserve&&!retimed&&rate==clip.Rate;
            var curves=clip.Tracks.SelectMany(t=>t.Channels.OfType<Curve>()).ToArray();
            bool anyHeld=curves.Any(c=>c.Interpolation==Interpolation.Step)||clip.Tracks.Any(t=>t.Rotation?.Interpolation==QuaternionInterpolation.Step);
            bool held=anyHeld&&curves.All(c=>c.Interpolation==Interpolation.Step)&&clip.Tracks.Where(t=>t.Rotation is not null).All(t=>t.Rotation!.Interpolation==QuaternionInterpolation.Step);
            if(anyHeld&&!held&&!exact)throw new NotSupportedException("Mixed held/continuous reconstruction needs a reviewed channel policy; holds were not smoothed away.");
            foreach(double boundary in curves.Where(c=>c.Interpolation==Interpolation.Step).SelectMany(c=>c.Keys).Select(k=>k.Frame).Concat(clip.Tracks.Where(t=>t.Rotation?.Interpolation==QuaternionInterpolation.Step).SelectMany(t=>t.Rotation!.Keys).Select(k=>k.Frame)))
            {
                double mapped=timing.TargetFrame(boundary,clip.Rate.FramesPerSecond,rate.FramesPerSecond);
                if(!exact&&mapped>=0&&mapped<samples&&Math.Abs(mapped-Math.Round(mapped))>1e-7)throw new NotSupportedException($"Held boundary requires nonintegral native key {mapped:G12}.");
            }
            if(!exact&&!held)foreach(var track in clip.Tracks.Where(t=>t.Rotation is null))for(int channel=3;channel<6;channel++)if(track.Channels[channel] is {} curve)
                for(double f=0;f<samples;f+=1d/PoseMotionReconstruction.Substeps)
                {
                    request.Cancellation.ThrowIfCancellationRequested();
                    double first=timing.SourceFrame(f,clip.Rate.FramesPerSecond,rate.FramesPerSecond),next=timing.SourceFrame(Math.Min(f+1d/PoseMotionReconstruction.Substeps,samples),clip.Rate.FramesPerSecond,rate.FramesPerSecond);
                    if(Math.Abs(curve.Sample(next)-curve.Sample(first))>=179.9f)throw new NotSupportedException($"Source winding exceeds declared tracking resolution near {first:G12}; no shortest-path turn loss was accepted.");
                }
            var reconstruction=Stage("fixed-pose adaptive curve reconstruction",PoseMotionReconstruction.Version,()=>PoseMotionReconstruction.Build(request,b,transfer,timing,rate,samples,closing,held,exact,reach));
            if(reconstruction.Receipt.MinimaxLimitedChannels is >0)
                diagnostics.Add(new("RETARGET_CURVE_REFINEMENT_LIMIT_REVIEW",$"Optional derivative optimization reached its work limit for {reconstruction.Receipt.MinimaxLimitedChannels} channels. Best candidates remain independently rated; this is not convergence, native support or permission to ignore failed curve bounds.",false));
            foreach(var diagnostic in reach.Diagnostics)if(!diagnostics.Contains(diagnostic))diagnostics.Add(diagnostic);
            var reachReceipt=reach.Receipt();
            var sceneReceipt=reach.SceneFitting.Receipt();
            if(reachReceipt.Enabled&&!reachReceipt.ExactRig&&reachReceipt.Chains.Length>0&&!reachReceipt.PositionalGoalsSatisfied)
                diagnostics.Add(new("BODY_REACH_REVIEW","Source-relative reach remains unrated after coordinated scene fitting, or its seed objectives are unreachable, unsupported, limited, unresolved or lack bend evidence. The per-chain seed receipt does not rate the final fitted pose; no contact, action or anatomy pass is implied.",false));
            if(!reconstruction.Receipt.Passed)diagnostics.Add(new("RETARGET_RECONSTRUCTION_REVIEW",$"Fixed-key representation needs refinement: skin {reconstruction.Receipt.MaximumSkinError:G9}/{reconstruction.Receipt.SkinBound:G9} at {reconstruction.Receipt.WorstSkinFrame:G12}, rotation {reconstruction.Receipt.MaximumRotationRadians:G9}/{reconstruction.Receipt.RotationBound:G9}. Transferred integer poses were not changed to hide interpolation error.",false));
            foreach(var unmapped in transfer.Mapping.SourceOnly.Where(p=>clip.Tracks.Any(t=>t.Bone==p.Name&&t.HasKeys)))losses.Add("Unmapped source channel: "+unmapped.Name+"; "+unmapped.Reason);
            if(source.NativeLayers.Length>0)losses.Add("Source companion material/visibility/morph/attachment channels are retained, not cross-rig converted.");
            var ledger=RetargetChannelLedger.Build(source.Scene,clip,target.Scene,transfer.Mapping,request.RootPolicy,exact,reachReceipt,sceneReceipt);
            var snapshot=new SourceAssetSnapshot(Guid.NewGuid(),source.Scene,clip,request.SourceRig,source.SourceModels){Layers=source.NativeLayers,Resources=source.NativeResources,Build=source.SourceBuild,BuildVerified=source.SourceBuildVerified,Provider=source.SourceProvider,AssociationEvidence=source.SourceAssociationEvidence,LibraryRegistrationId=source.LibraryRegistrationId,Contacts=authored,SourceFiles=source.SourceFiles,MotionAnalysis=analysis};
            var pipeline=new RetargetPipelineReceipt(RetargetBackendIdentity.CurrentId,2,stages.ToArray(),["native rig preservation","actor-frame rest-relative transfer","parent-frame native-path reach objectives","coupled native correction trajectory and relative action frames","fixed prepared integer poses","offline adaptive shared-native-derivative reconstruction"],["body-relative reach/action quality","temporal continuity","contact reconstruction disabled","joint/collision limits","native and displayed release","held-out anatomy/topology"]){Reconstruction=reconstruction.Receipt,BodyReach=reachReceipt,SceneBody=sceneReceipt,BodyTrajectory=reach.TemporalReceipt,ContactCoverage=analysis.Interactions.Coverage};
            var result=new AnimationClip(Guid.NewGuid(),clip.Name+" · retargeted",samples,rate,loop,reconstruction.Tracks){Source=clip.Source,NativeTarget=target.Clips.FirstOrDefault()?.NativeTarget??target.Clips.FirstOrDefault()?.Source,NativeWrite=NativeWriteMode.NewResource,NativeWriteName=(clip.Source?.NativeName??"Motion")+"_Retargeted",SourceSnapshot=snapshot.Id,
                Retarget=new(clip.Revision,target.Scene.SkeletonRevision,request.SourceRig.Id,request.TargetRig.Id,request.RootPolicy,Version){Backend=RetargetBackendIdentity.CurrentId,BackendSchema=2,Pipeline=pipeline,GroundDetection=request.Ground,BoneMapping=transfer.Mapping,ChannelLedger=ledger,Fitting=settings,Timing=timing,SourceAnalysisKey=analysis.Revision,ContactEdits=request.ContactEdits??[],SupportBindings=request.Supports??[],UntransferredChannels=losses.ToArray(),IsDraft=true}};
            if(request.RootPolicy!=RootMotionPolicy.Preserve)
            {
                diagnostics.Add(new("RETARGET_ACTOR_CONTEXT_REVIEW","Extracted actor travel remains separate recipe data; native actor/gameplay packaging is unavailable.",false));losses.Add("Extracted actor travel needs destination actor/gameplay encoding.");
                var up=new Vector3(a.Basis.M21,a.Basis.M22,a.Basis.M23);int actor=a.Binding.Roles["RootMotion"];
                var travel=Enumerable.Range(0,samples+(closing?1:0)).Select(f=>{var pose=AnimationService.Evaluate(source.Scene,clip,timing.SourceFrame(f,clip.Rate.FramesPerSecond,rate.FramesPerSecond));var move=pose.World[actor].Translation-a.Rest.World[actor].Translation;if(request.RootPolicy==RootMotionPolicy.InPlace)move-=up*Vector3.Dot(move,up);return new ActorMotionKey(f/rate.FramesPerSecond,transfer.ConvertDirection(move)*transfer.RootNativeRatio);}).ToArray();
                result=result with{Retarget=result.Retarget! with{ExtractedActorMotion=travel,UntransferredChannels=losses.ToArray()}};
            }
            var bake=new RetargetBake(result,[],snapshot,0,transfer.RootNativeRatio){Grounding=ExplicitGeometry(request,a,b),SourceDependencies=source.LibrarySources};
            var validation=Stage("independent geometry measurement",RetargetValidation.Version,()=>RetargetValidation.Measure(target,request.TargetRig,bake,request.Cancellation));
            if(!validation.Passed)diagnostics.Add(new("RETARGET_CONTACT_REVIEW",$"Contact acceptance is unverified ({validation.Applicability}); measured penetration {validation.Penetration:G9}/{validation.PenetrationBound:G9} at {validation.WorstFloorFrame:G12}. No contacts were solved.",false));
            result=result with{Retarget=result.Retarget! with{Pipeline=pipeline with{Stages=stages.ToArray()},Validation=validation,IsDraft=true}};
            return new(bake with{Clip=result,MaximumContactError=validation.Penetration},diagnostics.ToArray(),losses.ToArray());
        }
        catch(OperationCanceledException){throw;}
        catch(Exception e){return new(null,diagnostics.Append(new Diagnostic(e is NotSupportedException?"RETARGET_UNSUPPORTED":"RETARGET_INPUT",e.Message)).ToArray(),losses.ToArray());}
    }
    static SourceMotionAnalysis SourceEvidence(RetargetRequest request,ContactFrame[] authored)
    {
        var clip=request.Clip;var labels=request.Source.MotionCoverage.Where(l=>l.ClipRevision==clip.Revision&&l.Reviewed&&!string.IsNullOrWhiteSpace(l.Evidence)).OrderBy(l=>l.StartSeconds).ToArray();
        double end=0;bool complete=labels.Length>0;
        foreach(var label in labels)
        {
            if(!double.IsFinite(label.StartSeconds)||!double.IsFinite(label.EndSeconds)||label.StartSeconds<0||label.StartSeconds>end+1e-9||label.EndSeconds<=label.StartSeconds||label.EndSeconds>clip.DurationSeconds+1e-9){complete=false;break;}
            end=Math.Max(end,label.EndSeconds);
        }
        complete&=end>=clip.DurationSeconds-1e-9;bool free=complete&&labels.All(l=>!l.RequiredContacts);
        bool hasPins=authored.Any(c=>c.Pins.Length>0);
        if(free&&hasPins)throw new InvalidDataException("Reviewed free-motion labels conflict with source contacts.");
        var coverage=hasPins||labels.Any(l=>l.RequiredContacts)?MotionContactCoverage.Required:free?MotionContactCoverage.ConfirmedFree:MotionContactCoverage.Unknown;
        Diagnostic[] diagnostics=complete?[]:[new("SOURCE_MODE_REVIEW","Complete independent source interaction coverage is absent; empty detection is not a free-motion pass.",false)];
        var context=request.Ground is {} ground?ground with{TargetFloor=null,TargetEdge=null}:null;
        var source=request.Source;
        var revision=RetargetDependencies.Hash(new{Version,source.Scene,Clip=clip,Rig=request.SourceRig,source.SourceProvider,source.SourceBuild,source.SourceBuildVerified,source.SourceModels,source.SourceFiles,source.NativeLayers,source.NativeResources,Contacts=authored,Labels=labels,SourceSupportContext=context});
        return new(null,new(authored,coverage,diagnostics),revision){Labels=labels,CoverageReviewed=complete,SourceSupportContext=context};
    }
    static GroundContactReport? ExplicitGeometry(RetargetRequest request,RigDescriptor source,RigDescriptor target)
    {
        if(request.Ground is not {ExplicitSurface:true} ground)return null;
        ground.SourceEdge?.Validate(new(source.Basis.M21,source.Basis.M22,source.Basis.M23));ground.TargetEdge?.Validate(new(target.Basis.M21,target.Basis.M22,target.Basis.M23));
        if(ground.SourceFloor is not {} a||ground.TargetFloor is not {} b||!float.IsFinite(a)||!float.IsFinite(b))throw new InvalidDataException("Explicit geometry needs finite source and target floor heights; no floor was inferred.");
        return new("explicit-context-only-1",a,b,0,[],0){ExplicitSurface=true,SourceEdge=ground.SourceEdge,TargetEdge=ground.TargetEdge};
    }
}
