using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

sealed record BoundaryRequest(string? EntryMotion,string? ExitMotion,double EntryFrame,double ExitFrame,bool EntryFrozen,bool ExitFrozen,double EntryRate,double ExitRate,BoundaryFitSettings Settings);
sealed partial class ControlSession
{
    async Task<object> Transition(Request request)
    {
        var options=request.Boundary??throw new InvalidDataException("Choose transition reference settings.");
        var source=await adapter.ImportAsync(request.Model!,request.Motion!);var clip=source.Clips.Single();
        var state=string.IsNullOrEmpty(request.State)?null:JsonSerializer.Deserialize<ControlState>(request.State,ProjectStore.Json);
        string fingerprint=AnimationControls.Fingerprint(source.Scene);
        if(state is not null)
        {
            if(state.Fingerprint!=fingerprint||state.Setup.SkeletonFingerprint!=fingerprint)throw new InvalidDataException("Saved controls belong to another native rig revision.");
            clip=clip with{Id=state.Clip};source=source with{Clips=[clip],Contacts=state.Contacts,AuthoringContacts=state.AuthoringContacts,BonePins=state.BonePins,ControlRigs=[state.Setup]};
        }
        async Task<BoundaryAnchor?> Reference(string? path,double frame,bool frozen,double rate)
        {
            if(string.IsNullOrEmpty(path))return null;
            var native=await adapter.ImportAsync(request.Model!,path);
            return new(native.Clips.Single(),AnimationControls.Fingerprint(native.Scene),frame,frozen,rate);
        }
        var entry=await Reference(options.EntryMotion,options.EntryFrame,options.EntryFrozen,options.EntryRate);
        var exit=await Reference(options.ExitMotion,options.ExitFrame,options.ExitFrozen,options.ExitRate);
        state??=new(fingerprint,clip.Id,BrawlControlProfiles.SetUp(source),[],[],null,[]);
        source=source with{ControlRigs=[state.Setup]};
        var fit=await Task.Run(()=>BoundaryFitting.Prepare(source,clip,entry,exit,options.Settings));
        state??=new(fingerprint,clip.Id,BrawlControlProfiles.SetUp(source),[],[],null,[]);
        string copiedState=JsonSerializer.Serialize(state with{Clip=fit.Clip.Id,Contacts=state.Contacts.Select(c=>c.Clip==clip.Id?c with{Clip=fit.Clip.Id}:c).ToArray(),BonePins=state.BonePins?.Select(p=>p.Clip==clip.Id?p with{Clip=fit.Clip.Id}:p).ToArray()},ProjectStore.Json);
        string receipt=JsonSerializer.Serialize(new{fit.Receipt,fit.Clip.Transition},ProjectStore.Json);
        return new{Ok=true,Name=fit.Clip.Name,SampleCount=fit.Clip.SampleCount,fit.Clip.Loop,State=copiedState,Algorithm=fit.Receipt.Version,Receipt=receipt,
            Tracks=fit.Changed.Select(t=>NativeTrack(source.Scene,t)).ToArray(),Diagnostics=fit.Receipt.Limits};
    }
}
