using FighterPort.Core;
using FighterPort.Adapters.Brawl;
using System.Text.Json;

sealed partial class ControlSession
{
    async Task<object> AuthorContact(Request request)
    {
        var project=await adapter.ImportAsync(request.Model!,request.Motion!);var clip=project.Clips.Single();
        var state=string.IsNullOrEmpty(request.State)?new ControlState(AnimationControls.Fingerprint(project.Scene),clip.Id,BrawlControlProfiles.SetUp(project),[],[],null,[]):JsonSerializer.Deserialize<ControlState>(request.State,ProjectStore.Json)!;
        if(state.Fingerprint!=AnimationControls.Fingerprint(project.Scene))throw new InvalidDataException("Native contact rig changed.");
        clip=clip with{Id=state.Clip};project=project with{Clips=[clip],Contacts=state.Contacts,ControlRigs=[state.Setup],BonePins=state.BonePins};
        if(request.Operation=="contact-verify")
        {
            var proof=request.ContactProof??throw new InvalidDataException("Missing contact receipt.");
            try{ContactAuthoring.Validate(project.Scene,clip,state.Setup,state.Contacts.Where(c=>c.Clip==clip.Id).ToArray(),proof.PositionBound,proof.RotationBound,proof.FirstChanged,proof.LastChanged);}catch(Exception error){throw new InvalidDataException("Fresh native contact verification: "+error.Message,error);}
            return new{Ok=true};
        }
        var options=request.ContactEdit??throw new InvalidDataException("Choose contact placement/release settings.");
        AnimationClip? reference=null;if(options.Action=="Release")reference=(await adapter.ImportAsync(request.Model!,request.Path!)).Clips.Single();
        ContactBakeResult result;try{result=await Task.Run(()=>ContactAuthoring.Prepare(project,clip,state.Setup,options,request.ContactProtection??[],reference));}catch(Exception error){throw new InvalidDataException("Contact preparation: "+error.Message,error);}
        var copied=state with{Clip=result.Clip.Id,Contacts=result.Contacts,BonePins=state.BonePins?.Select(p=>p.Clip==clip.Id?p with{Clip=result.Clip.Id}:p).ToArray()};
        return new{Ok=true,State=JsonSerializer.Serialize(copied,ProjectStore.Json),Algorithm=ContactAuthoring.Version,Receipt=JsonSerializer.Serialize(result.Receipt,ProjectStore.Json),Tracks=result.Changed.Select(t=>NativeTrack(project.Scene,t)).ToArray(),Diagnostics=result.Receipt.Limits};
    }
}
