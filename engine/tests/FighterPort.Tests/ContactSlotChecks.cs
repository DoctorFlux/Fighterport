using FighterPort.Core;
using System.Numerics;
using System.Text.Json;

internal static class ContactSlotChecks
{
    public static async Task RunAsync(string dir,Action<bool,string> check)
    {
        var scene=new Scene("Original contact branching rig",[new("body",-1,Transform.Identity),new("support",0,new(Vector3.One,Vector3.Zero,new(0,-2,0))),new("free",0,new(Vector3.One,Vector3.Zero,new(2,0,0)))],[]);
        Curve?[] Rotation(float angle){var c=new Curve?[9];c[3]=new([new(0,angle),new(23,angle)]);return c;}
        var clip=new AnimationClip(Guid.NewGuid(),"Action",24,new(60),false,[new("body",Rotation(0)),new("support",Rotation(0)),new("free",Rotation(0))]);
        var reference=clip with{Id=Guid.NewGuid(),Revision=Guid.NewGuid(),Tracks=clip.Tracks.Select(t=>t with{Channels=Rotation(20)}).ToArray()};
        string fingerprint=AnimationControls.Fingerprint(scene);var pin=new ContactPin(Guid.NewGuid(),"Reviewed support",1,Vector3.Zero,new(0,-2,0),Quaternion.Identity,true,"Authored point");
        var contact=new ContactFrame(Guid.NewGuid(),clip.Id,0,fingerprint,[pin]){EndFrame=23};
        var project=new FighterProject(1,Guid.NewGuid(),"Contact editing",scene,[clip]){Contacts=[contact]};string before=JsonSerializer.Serialize(project,ProjectStore.Json);
        var options=new BoundaryFitSettings(6,6,TransformComponents.All,["body","support","free"],[new(5,18,"Protected action")]){PreserveContacts=true};
        var fit=BoundaryFitting.Prepare(project,clip,new(reference,fingerprint,0),new(reference,fingerprint,23),options);
        check(fit.Receipt.Version==BoundaryFitting.ContactVersion&&fit.Receipt.ProtectedContactBones.SequenceEqual(new[]{"body","support"}),"Contact receipt records the exact complete ancestor path without names-as-anatomy rules");
        check(ReferenceEquals(fit.Clip.Tracks[0],clip.Tracks[0])&&ReferenceEquals(fit.Clip.Tracks[1],clip.Tracks[1]),"Every original S/R/T curve and native flag on the support path remains untouched");
        check(Enumerable.Range(0,369).All(i=>AnimationService.Evaluate(scene,clip,i/16d).World[1]==AnimationService.Evaluate(scene,fit.Clip,i/16d).World[1]),"Fixed contact world position and orientation remain exact at independent fractional samples");
        check(fit.Clip.Tracks[2].Channels[3]!.Sample(0)==20&&fit.Clip.Tracks[2].Channels[3]!.Sample(23)==20,"Unprotected branch still fits both requested reference endpoints");
        check(fit.Receipt.Limits.Any(l=>l.Contains("excluded from endpoint matching")),"Review explicitly lists the conservative contact exclusion rather than claiming a new IK solution");
        check(JsonSerializer.Serialize(project,ProjectStore.Json)==before,"Contact fitting retains immutable sources, authored anchors and contact intervals");
        bool Reject(Action action,string expected){try{action();return false;}catch(Exception e)when(e is InvalidDataException or NotSupportedException){return e.Message.Contains(expected,StringComparison.OrdinalIgnoreCase);}}
        check(Reject(()=>BoundaryFitting.Prepare(project,clip,new(reference,fingerprint,0),null,options with{Bones=["body","support"]}),"no eligible"),"A fully constrained mask rejects without pretending to match its endpoints");
        check(Reject(()=>BoundaryFitting.Prepare(project with{Contacts=[contact with{SkeletonFingerprint="changed"}]},clip,new(reference,fingerprint,0),null,options),"rig revision"),"Stale native contact revisions cannot protect another rig");
        check(Reject(()=>BoundaryFitting.Prepare(project,clip,new(reference,fingerprint,0),null,options with{ContactBones=["absent"]}),"absent"),"Unknown reviewed support dependencies reject before editing");
        check(Reject(()=>BoundaryFitting.Prepare(project with{Contacts=[]},clip,new(reference,fingerprint,0),null,options),"No existing contact"),"Contact preservation without any identified support cannot imply contact acceptance");
        var native=BoundaryFitting.Prepare(project with{Contacts=[]},clip,new(reference,fingerprint,0),null,options with{ContactBones=["support"]});
        check(native.Receipt.ProtectedContactBones.SequenceEqual(fit.Receipt.ProtectedContactBones),"Native skin dependency transport uses the same general ancestor-protection algorithm");
        var targetSlot=new SlotAssignment("TargetOwned",clip.Id,true,"Historical verified target requirement");
        var baseProject=project with{Slots=[targetSlot]};var intents=new[]{new EditorSlotIntent("Entry","resource/one",true,true,"Assigned exact revision","template-sha/native-entry"),new EditorSlotIntent("Exit",null,false,false,"Unassigned")};
        var synced=AnimationSlots.Synchronize(baseProject,intents,new Dictionary<string,Guid>{{"resource/one",clip.Id}});
        check(synced.Slots.Length==3&&synced.Slots[0]==targetSlot,"Custom editor slot synchronization preserves existing target-owned assignments");
        check(synced.Slots[1].Animation==clip.Id&&synced.Slots[1].Protected&&synced.Slots[2].Animation==null,"Custom required, optional, protected and unassigned slots retain exact clip identities");
        check(synced.Slots[1].ProfileKey=="template-sha/native-entry","Source-bound slot template identity crosses the native/Core boundary without inventing a runtime rule");
        var missing=AnimationSlots.Synchronize(synced,intents,new Dictionary<string,Guid>{{"same-name/different-resource",clip.Id}});
        check(missing.Slots[1].Animation==null&&missing.Slots[1].ResourceKey=="resource/one","Missing exact native resources stay unassigned instead of substituting a same-named animation");
        check(Reject(()=>AnimationSlots.Synchronize(project,[intents[0],intents[0] with{Id="entry"}],new Dictionary<string,Guid>()),"Invalid custom"),"Case-insensitive duplicate custom slots reject");
        check(Reject(()=>AnimationSlots.Synchronize(project,intents,new Dictionary<string,Guid>{{"resource/one",Guid.NewGuid()}}),"different scene"),"Slot assignments cannot bind a clip from another scene");
        check(AnimationSlots.Synchronize(synced,[],new Dictionary<string,Guid>()).Slots.SequenceEqual(new[]{targetSlot}),"Clearing custom assignments preserves all unrelated slot contracts");
        Directory.CreateDirectory(dir);string path=Path.Combine(dir,"contact-slots.fighterport");await ProjectStore.SaveAsync(synced with{Clips=[fit.Clip with{Id=clip.Id}]},path);var reopened=await ProjectStore.LoadAsync(path);
        check(reopened.Slots.SequenceEqual(synced.Slots)&&reopened.Clips[0].Transition?.CurveFit?.ProtectedContactBones.SequenceEqual(new[]{"body","support"})==true,"Core project storage preserves slot identities and complete contact-protection receipts");
    }
}
