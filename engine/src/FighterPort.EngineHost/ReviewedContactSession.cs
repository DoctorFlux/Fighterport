using FighterPort.Core;
using FighterPort.Adapters.Brawl;
using System.Numerics;
using System.Text.Json;

sealed partial class ControlSession
{
    async Task<object> ReviewedContactSet(Request request)
    {
        if(request.Model is null||request.Motion is null)throw new InvalidDataException("Choose the current target model and native animation.");
        var project=await adapter.ImportAsync(request.Model,request.Motion);var clip=project.Clips.Single();
        var state=string.IsNullOrEmpty(request.State)?new ControlState(AnimationControls.Fingerprint(project.Scene),clip.Id,BrawlControlProfiles.SetUp(project),[],[],null,[]):
            JsonSerializer.Deserialize<ControlState>(request.State,ProjectStore.Json)??throw new InvalidDataException("Missing reviewed control setup.");
        if(state.Fingerprint!=AnimationControls.Fingerprint(project.Scene))throw new InvalidDataException("Reviewed contact setup belongs to another exact native rig.");
        if(state.AuthoringContacts.Length>0||state.AuthoringPose.Count>0)throw new NotSupportedException("Uncommitted authoring pose/pins need their complete native adapter before contact correction.");
        clip=clip with{Id=state.Clip};project=project with{Clips=[clip],Contacts=state.Contacts,ControlRigs=[state.Setup],BonePins=state.BonePins};
        object Reply(ReviewedContactContext context)=>new{Ok=true,Algorithm=ReviewedContactSets.Version,
            ContactContextJson=JsonSerializer.Serialize(context,ProjectStore.Json),
            ContactRanges=context.Ranges.Select(r=>new{r.Id,r.Role,r.Bone,r.Start,r.End,r.Blend,r.AnchorFrame,Anchor=new[]{r.Anchor.X,r.Anchor.Y,r.Anchor.Z},r.Reviewed}).ToArray(),
            Diagnostics=context.Diagnostics};
        ReviewedContactContext Evidence(ReviewedContactContext value,bool attach)
        {
            if(request.NativeSupportJson is null)
            {
                if(value.OriginalSupportKey!=null||value.InferredSupports.Length>0)throw new InvalidDataException("The original native support context is missing.");
                return value;
            }
            var source=OriginalSupportEvidence.Read(project.Scene,clip.SampleCount,request.NativeSupportJson);
            if(!attach&&(value.OriginalSupportKey!=source.Key||ReviewedContactSets.ContentKey(value.InferredSupports)!=ReviewedContactSets.ContentKey(source.Ranges)))
                throw new InvalidDataException("The original native support timeline changed after review.");
            return value with{OriginalSupportKey=source.Key,InferredSupports=source.Ranges};
        }
        if(request.Operation=="contact-set-suggest")
        {
            var settings=request.ContactSurface??throw new InvalidDataException("Review the static ground plane first.");
            var suggested=await Task.Run(()=>ReviewedContactSets.Suggest(project,clip,state.Setup,settings,request.ContactProtection??[]));
            return Reply(Evidence(suggested with{ProtectedIntervals=request.ReviewedProtectedRanges??[],FittingMiddle=request.ReviewedFittingMiddle},true));
        }
        if(request.ReviewedContactsJson is null||request.ReviewedContactsJson.Length>8*1024*1024)throw new InvalidDataException("Missing or oversized complete contact context.");
        var context=JsonSerializer.Deserialize<ReviewedContactContext>(request.ReviewedContactsJson,ProjectStore.Json)??throw new InvalidDataException("Invalid complete contact context.");
        context=Evidence(context,false);
        if(request.Operation=="contact-set-verify"||request.Operation=="contact-set-verify-export")
        {
            if(request.Path is null)throw new InvalidDataException("Fresh contact validation requires the frozen full-quality master.");
            var master=(await adapter.ImportAsync(request.Model,request.Path)).Clips.Single() with{Id=state.Clip};
            var proof=request.ReviewedContactProof??throw new InvalidDataException("Fresh contact validation requires its complete receipt.");
            if(proof.Version!=ReviewedContactSets.Version||proof.ContextKey!=ReviewedContactSets.ContentKey(context)||proof.MasterKey!=context.MasterKey)
                throw new InvalidDataException("Contact evidence/receipt changed before native verification.");
            // Export separately proves equivalence/deviation against its own
            // finished corrected master. Replay absolute contact acceptance
            // here, without rejecting proven Exact key/constant cleanup merely
            // because it has a different authored representation.
            var result=ReviewedContactSets.Validate(project.Scene,clip,master,state.Setup,context,proof.PositionBound,proof.OrientationBound,proof.Witnesses,
                preserveEditScope:request.Operation!="contact-set-verify-export");
            return new{Ok=true,ContactMetrics=new{result.Drift,result.Penetration,result.Angle}};
        }
        foreach(var edit in request.ReviewedRangeEdits??[])
        {
            if(edit.Remove)
            {
                if(edit.Id is null)throw new InvalidDataException("Choose an existing contact row to remove.");
                context=context with{Ranges=context.Ranges.Where(r=>r.Id!=edit.Id).ToArray()};continue;
            }
            if(edit.Offset.Length!=3)throw new InvalidDataException("A reviewed anchor offset requires three native components.");
            context=ReviewedContactSets.SetRange(project.Scene,clip,state.Setup,context,edit.Role,edit.Start,edit.End,edit.Blend,edit.AnchorFrame,
                new Vector3(edit.Offset[0],edit.Offset[1],edit.Offset[2]),edit.Reviewed,edit.Id);
        }
        context=context with{EarlierBodyCorrectionStart=request.ReviewedCorrectionStart};
        if(request.Operation=="contact-set-review")return Reply(context);
        if(request.Operation!="contact-set-prepare")throw new NotSupportedException("Unknown reviewed contact operation.");
        var prepared=await Task.Run(()=>ReviewedContactSets.Prepare(project,clip,state.Setup,context));
        var copied=state with{Contacts=prepared.Contacts};
        return new{Ok=true,Algorithm=ReviewedContactSets.Version,State=JsonSerializer.Serialize(copied,ProjectStore.Json),
            ContactContextJson=JsonSerializer.Serialize(prepared.Context,ProjectStore.Json),Receipt=JsonSerializer.Serialize(prepared.Receipt,ProjectStore.Json),
            Tracks=prepared.Changed.Select(t=>NativeTrack(project.Scene,t)).ToArray(),Diagnostics=prepared.Receipt.Limits};
    }
}
