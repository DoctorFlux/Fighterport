using System.Numerics;
using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

if(args.FirstOrDefault()=="cli")
{
    var cli=System.Reflection.Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,"FighterPort.Cli.dll"));
    var result=cli.EntryPoint!.Invoke(null,[args.Skip(1).ToArray()]);
    Environment.ExitCode=result is Task<int> task?await task:result is int code?code:0;return;
}

// One sequential local session. No transport clock, renderer or native UI types.
// The frontend owns native Undo and publication. Preview never writes a file.
var session=new ControlSession();
var outputJson=new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false};
while(await Console.In.ReadLineAsync() is {} line)
{
    try { var request=JsonSerializer.Deserialize<Request>(line,ProjectStore.Json)??throw new InvalidDataException("Empty request.");Console.WriteLine(JsonSerializer.Serialize(await session.Run(request),outputJson)); }
    catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new { Ok=false, Error=e.Message },outputJson));}
}

sealed record BindingReview(string Fingerprint,Dictionary<string,string> Roles,float[] Right,float[] Up,float[] Forward,string Evidence);
sealed record SetupReview(string? Role,int[]? Bones,float[]? Pole,Dictionary<string,int?> Roles,string Evidence);
sealed record Request(string Operation,string? Model=null,string? Motion=null,string? State=null,double Frame=0,int Bone=0,string? Pin=null,float[]? Values=null,bool Follow=false,string? SourceModel=null,bool Fit=true,BindingReview? SourceBinding=null,BindingReview? TargetBinding=null,string? Name=null,int SampleCount=60,string? Path=null,string? TargetId=null,string? SceneId=null,string? Game=null,string? Platform=null,string? Build=null,string? Mode=null,string? Package=null,string? Output=null,WorkspaceMotion[]? Motions=null,string? ClipKey=null,SetupReview? Review=null,FittingOptions? Fitting=null,string? SourceAssociation=null,string? SourceRegistration=null,BoundaryRequest? Boundary=null,EditorSlotIntent[]? Slots=null,ContactBakeOptions? ContactEdit=null,string[]? ContactProtection=null,ContactBakeReceipt? ContactProof=null);
sealed record ControlState(string Fingerprint,Guid Clip,ControlRigSetup Setup,ContactFrame[] Contacts,ContactPin[] AuthoringContacts,EditorBonePin[]? BonePins,Dictionary<int,Transform> AuthoringPose,string? BindingKey=null);
sealed record PinView(string Id,int Bone,string BoneName,string Name,bool Soft,float[] Position,float[] Rotation);
sealed record KeyView(int Frame,float Value,float Tangent);
sealed record TrackView(string Bone,KeyView[][] Channels);
sealed partial class ControlSession
{
    EditorDocument? document;ControlRigSetup? setup;TransformEdit? edit;EvaluatedPose? input;
    string? bindingKey;
    Dictionary<int,Transform> changes=[];Dictionary<Guid,EditorPinTarget> goals=[];Guid? selectedPin;bool selectedIk,selectedPole;
    readonly BrawlAdapter adapter=new(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER")??BrawlAdapter.DefaultWorker);
    public async Task<object> Run(Request r)
    {
        if(r.Operation.StartsWith("workspace-",StringComparison.Ordinal))return await Workspace(r);
        if(r.Operation.StartsWith("fitting-",StringComparison.Ordinal))return await Fitting(r);
        if(r.Operation=="transition-prepare")return await Transition(r);
        if(r.Operation is "contact-prepare" or "contact-verify")return await AuthorContact(r);
        if(r.Operation=="hello")return new {Ok=true,Protocol=1,Engine="FighterPort.Core",NativeRate=60};
        if(r.Operation=="retarget")
        {
            var donor=await adapter.ImportAsync(r.SourceModel!,r.Motion!);var target=await adapter.ImportModelAsync(r.Model!);
            var sourceSetup=ReviewedBinding(donor,BrawlControlProfiles.SetUp(donor),r.SourceBinding);var targetSetup=ReviewedBinding(target,BrawlControlProfiles.SetUp(target),r.TargetBinding);
            if(sourceSetup.Axes is null||targetSetup.Axes is null)return new{Ok=true,NeedsRigReview=true,ReviewSource=sourceSetup.Axes is null,ReviewTarget=targetSetup.Axes is null};
            var output=await Task.Run(()=>RetargetService.Bake(donor,donor.Clips.Single(),sourceSetup,target,targetSetup,useContacts:false,fitting:new(){AutomaticFitting=r.Fit,AutomaticContacts=false}));
            var bake=output.Value??throw new InvalidOperationException(string.Join("; ",output.Diagnostics.Select(d=>d.Message)));
            var validation=RetargetAcceptance.Measure(target,targetSetup,bake);
            bool ready=RetargetAcceptance.Ready(validation,output.Diagnostics,output.LostChannels);
            var json=new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false};
            return new{Ok=true,Algorithm=RetargetService.Version,Name=bake.Clip.Name,SampleCount=bake.Clip.SampleCount,Loop=bake.Clip.Loop,Draft=!ready,
                Tracks=bake.Clip.Tracks.Select(t=>NativeTrack(target.Scene,t)).ToArray(),
                Receipt=JsonSerializer.Serialize(new{Version=1,Algorithm=RetargetService.Version,Draft=!ready,Validation=validation,Diagnostics=output.Diagnostics,Losses=output.LostChannels,Recipe=bake.Clip.Retarget},json),
                Mapping=RetargetBoneMapping.Build(donor.Scene,sourceSetup,target.Scene,targetSetup).Bones.Select(b=>new{b.Role,Source=donor.Scene.Bones[b.Source].Name,Target=target.Scene.Bones[b.Target].Name}).ToArray(),
                Diagnostics=output.Diagnostics.Select(d=>d.Message).Concat(output.LostChannels).ToArray()};
        }
        if(r.Operation=="load")
        {
            edit?.Cancel();edit=null;var p=string.IsNullOrEmpty(r.Motion)?await adapter.ImportModelAsync(r.Model!):await adapter.ImportAsync(r.Model!,r.Motion!);
            var state=string.IsNullOrEmpty(r.State)?null:JsonSerializer.Deserialize<ControlState>(r.State,ProjectStore.Json);
            if(state is not null)
            {
                if(state.Fingerprint!=AnimationControls.Fingerprint(p.Scene))throw new InvalidDataException("Controls belong to a different native rig revision. Set up this model separately.");
                if(state.Setup.SkeletonFingerprint!=state.Fingerprint||state.Setup.BoneRoles.Any(b=>b.Bone<0||b.Bone>=p.Scene.Bones.Length)||state.Setup.Chains.Any(c=>new[]{c.Root,c.Middle,c.Effector}.Any(b=>b<0||b>=p.Scene.Bones.Length)))throw new InvalidDataException("Saved controls contain an invalid native rig binding.");
                p=p with{Clips=p.Clips.Select(c=>c with{Id=state.Clip}).ToArray(),ControlRigs=[state.Setup],Contacts=state.Contacts,AuthoringContacts=state.AuthoringContacts,BonePins=state.BonePins,AuthoringPose=state.AuthoringPose};
            }
            bindingKey=r.TargetBinding is null?state?.BindingKey:Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{r.TargetBinding.Fingerprint,Roles=r.TargetBinding.Roles.OrderBy(p=>p.Key,StringComparer.Ordinal).ToArray(),r.TargetBinding.Right,r.TargetBinding.Up,r.TargetBinding.Forward,r.TargetBinding.Evidence},ProjectStore.Json)));
            setup=state is not null&&(r.TargetBinding is null||state.BindingKey==bindingKey)?state.Setup:ReviewedBinding(p,BrawlControlProfiles.SetUp(p),r.TargetBinding);p=p with{ControlRigs=[setup]};document=new(p);document.Seek(r.Frame);return Reply();
        }
        var d=document??throw new InvalidOperationException("Load an existing model first.");
        switch(r.Operation)
        {
            case "review-controls":
                if(edit is not null)throw new InvalidOperationException("Finish the current gesture before reviewing controls.");
                var review=r.Review??throw new InvalidDataException("Missing control review.");var reviewed=setup!;
                if(review.Role is not null)
                {
                    if(review.Bones?.Length!=3||review.Pole?.Length!=3)throw new InvalidDataException("Choose three existing joints and a pole direction.");
                    var result=AnimationControls.ReviewChain(d.Project.Scene,reviewed,review.Role,review.Bones[0],review.Bones[1],review.Bones[2],new(review.Pole[0],review.Pole[1],review.Pole[2]),review.Evidence);
                    reviewed=result.Value??throw new InvalidOperationException(string.Join("; ",result.Diagnostics.Select(x=>x.Message)));
                }
                foreach(var role in review.Roles)
                {
                    var result=AnimationControls.ReviewBoneRole(d.Project.Scene,reviewed,role.Key,role.Value,review.Evidence);
                    reviewed=result.Value??throw new InvalidOperationException(string.Join("; ",result.Diagnostics.Select(x=>x.Message)));
                }
                setup=reviewed;d.Transaction("Review animation controls",p=>p with{ControlRigs=p.ControlRigs.Where(c=>c.SkeletonFingerprint!=reviewed.SkeletonFingerprint).Append(reviewed).ToArray()});return Reply();
            case "create-animation":
                if(edit is not null)throw new InvalidOperationException("Finish the current gesture before creating an animation.");
                if(r.SampleCount<1||r.SampleCount>65535)throw new InvalidDataException("Choose 1–65535 native animation frames.");
                d.CreateAnimation(r.Name??"NewAnimation",r.SampleCount,new(60,1),true);
                return new{Ok=true,Name=d.Clip.Name,SampleCount=d.Clip.SampleCount,Loop=d.Clip.Loop,Tracks=d.Clip.Tracks.Select(t=>NativeTrack(d.Project.Scene,t)).ToArray(),State=State(),Pose=d.Evaluate().Local.Select(t=>t.Channels()).ToArray(),BoneNames=d.Project.Scene.Bones.Select(b=>b.Name).ToArray(),Pins=Pins()};
            case "pin":
                d.Seek(r.Frame);var chain=setup!.Chains.FirstOrDefault(c=>c.Effector==r.Bone);
                if(chain is null)d.AddBonePin(r.Bone);else d.PinControls(setup,[chain.Role],EditorPinBehavior.SoftReach,r.Follow);
                return Reply();
            case "pin-limbs":d.Seek(r.Frame);if(setup!.Chains.Length==0)throw new InvalidOperationException("No unambiguous limb bindings. Attach individual FK pins or review the mapping first.");d.PinControls(setup,setup.Chains.Select(c=>c.Role).ToArray(),EditorPinBehavior.SoftReach,r.Follow);return Reply();
            case "remove":d.Seek(r.Frame);d.RemoveEditorPin(Guid.Parse(r.Pin!));return Reply();
            case "begin":
                edit?.Cancel();d.Seek(r.Frame);d.SelectedBone=r.Bone;selectedPin=Guid.TryParse(r.Pin,out var id)?id:null;selectedIk=r.Pin?.StartsWith("ik:",StringComparison.Ordinal)==true;selectedPole=r.Pin?.StartsWith("pole:",StringComparison.Ordinal)==true;input=d.Evaluate();edit=d.BeginTransformEdit();changes=[];goals=[];return Reply();
            case "preview":
                if(edit is null||input is null)throw new InvalidOperationException("Begin a gesture first.");
                var values=r.Values??throw new InvalidDataException("Missing transform.");var scene=d.Project.Scene;
                var pins=d.CurrentContacts;
                if(selectedPin is not null || selectedPole || values.Length==10)
                {
                    if(values.Length!=10||values.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Expected finite world position, quaternion and scale.");
                    var position=new Vector3(values[0],values[1],values[2]);var rotation=new Quaternion(values[3],values[4],values[5],values[6]);var scale=new Vector3(values[7],values[8],values[9]);
                    if(Math.Abs(rotation.LengthSquared()-1)>.001f||scale.X<=0||scale.Y<=0||scale.Z<=0)throw new InvalidDataException("Invalid pin orientation or scale.");
                    var pin=pins.SingleOrDefault(p=>p.Id==selectedPin);
                    if(selectedPole)
                    {
                        if(rotation!=Quaternion.Identity||scale!=Vector3.One)throw new NotSupportedException("Pole controls edit a world position. Use Move; rotate and scale the IK effector or FK control.");
                        var limb=setup!.Chains.Single(c=>c.Effector==d.SelectedBone);
                        var solved=AnimationControls.Solve(scene,input,setup,limb,input.World[limb.Effector].Translation,position);
                        if(solved.Value is null)throw new InvalidOperationException(string.Join("; ",solved.Diagnostics.Select(x=>x.Message)));
                        var constrained=EditorControlPosing.Solve(scene,input,setup,solved.Value.Transforms,pins.Where(p=>p.Bone!=limb.Effector||p.EditorBehavior!=EditorPinBehavior.SoftReach).ToArray());
                        if(constrained.Value is null)throw new InvalidOperationException(string.Join("; ",constrained.Diagnostics.Select(x=>x.Message)));
                        changes=constrained.Value.Transforms;
                    }
                    else if(selectedIk)
                    {
                        var limb=setup!.Chains.Single(c=>c.Effector==d.SelectedBone);
                        if(Math.Abs(scale.X-scale.Y)>.0001f||Math.Abs(scale.Y-scale.Z)>.0001f)throw new NotSupportedException("IK handles require uniform positive scale.");
                        var target=new ContactPin(Guid.Empty,limb.Role,limb.Effector,Vector3.Zero,position,rotation,true,"Editor IK control"){EditorBehavior=EditorPinBehavior.SoftReach};
                        var solved=EditorControlPosing.Solve(scene,input,setup,new Dictionary<int,Transform>(),pins.Where(p=>p.Bone!=limb.Effector).Append(target).ToArray());
                        if(solved.Value is null)throw new InvalidOperationException(string.Join("; ",solved.Diagnostics.Select(x=>x.Message)));
                        changes=solved.Value.Transforms;
                        if(scale!=input.Local[limb.Effector].Scale)changes[limb.Effector]=(changes.GetValueOrDefault(limb.Effector)??input.Local[limb.Effector]) with{Scale=scale};
                    }
                    else if(pin is not null)
                    {
                        if(Math.Abs(scale.X-scale.Y)>.0001f||Math.Abs(scale.Y-scale.Z)>.0001f)throw new NotSupportedException("Soft limb handles require uniform positive scale.");
                        var result=EditorControlPosing.Solve(scene,input,setup!,new Dictionary<int,Transform>(),pins.Select(p=>p.Id==pin.Id?p with{Position=position,Rotation=rotation}:p).ToArray());
                        if(result.Value is null)throw new InvalidOperationException(string.Join("; ",result.Diagnostics.Select(x=>x.Message)));
                        changes=result.Value.Transforms;goals=new(){[pin.Id]=new(position,rotation)};
                        if(scale!=input.Local[pin.Bone].Scale)changes[pin.Bone]=(changes.GetValueOrDefault(pin.Bone)??input.Local[pin.Bone]) with{Scale=scale};
                    }
                    else
                    {
                        int bone=selectedPin is {} pinId?d.CurrentBonePins.Single(p=>p.Id==pinId).Bone:d.SelectedBone;
                        var parent=scene.Bones[bone].Parent;
                        if(!Matrix4x4.Invert(parent<0?Matrix4x4.Identity:input.World[parent],out var inverse))throw new InvalidDataException("Singular native parent.");
                        SolveFk(bone,PoseGeometry.WorldRotation(scene,input,bone,rotation) with{Translation=Vector3.Transform(position,inverse),Scale=scale});
                    }
                }
                else {if(values.Length!=9||values.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Expected nine finite native channels.");SolveFk(d.SelectedBone,Transform.From(values));}
                d.PreviewTransforms(changes);return Reply(false);
            case "commit":
                if(edit is null)throw new InvalidOperationException("No active gesture.");
                bool committed=edit.Commit(changes,goals);edit=null;
                var tracks=d.Project.Clips.Length==0?[]:d.Clip.Tracks.Where(t=>changes.Keys.Any(i=>d.Project.Scene.Bones[i].Name==t.Bone)).Select(t=>NativeTrack(d.Project.Scene,t)).ToArray();
                return new {Ok=true,Committed=committed,Tracks=tracks,State=State(),Pose=d.Evaluate().Local.Select(t=>t.Channels()).ToArray(),Pins=Pins()};
            case "cancel":edit?.Cancel();edit=null;return Reply();
            default:throw new NotSupportedException("Unknown engine operation.");
        }
        void SolveFk(int bone,Transform value)
        {
            var solved=EditorControlPosing.Solve(d.Project.Scene,input!,setup!,new Dictionary<int,Transform>{{bone,value}},d.CurrentContacts);
            if(solved.Value is null)throw new InvalidOperationException(string.Join("; ",solved.Diagnostics.Select(x=>x.Message)));changes=solved.Value.Transforms;
        }
    }
    static TrackView NativeTrack(Scene scene,BoneTrack track)
    {
        // Explicit native-boundary policy: fill absent axes of an authored group
        // with authentic rest constants. Never modify the source/Core clip.
        var rest=scene.Bones.Single(b=>b.Name==track.Bone).Rest.Channels();
        return new(track.Bone,track.Channels.Select((c,i)=>
        {
            bool group=track.Channels.Skip(i/3*3).Take(3).Any(c=>c is not null);
            var keys=c?.Keys??(group?[new Key(0,rest[i])]:[]);
            if(keys.Any(k=>k.Frame!=Math.Truncate(k.Frame)))throw new NotSupportedException("Native commit requires integral key times; exact fractional project data remains available.");
            // CHR0 represents incoming/outgoing sides with up to two entries at
            // one time. Preserve existing discontinuities and encode asymmetric
            // smooth tangents as equal-value native pairs.
            var native=keys.SelectMany(k=>k.InTangent==k.OutTangent?new[]{new KeyView((int)k.Frame,k.Value,k.OutTangent)}:new[]{new KeyView((int)k.Frame,k.Value,k.InTangent),new KeyView((int)k.Frame,k.Value,k.OutTangent)}).ToArray();
            if(native.GroupBy(k=>k.Frame).Any(g=>g.Count()>2))throw new NotSupportedException("A native curve supports at most two entries at one key time; the original project curve remains intact.");
            return native;
        }).ToArray());
    }
    static ControlRigSetup ReviewedBinding(FighterProject project,ControlRigSetup original,BindingReview? review)
    {
        if(review is null)return original;
        var scene=project.Scene;if(review.Fingerprint!=AnimationControls.Fingerprint(scene)||string.IsNullOrWhiteSpace(review.Evidence))throw new InvalidDataException("Binding review belongs to another exact native rest/hierarchy revision or has no evidence.");
        Vector3 Axis(float[] values){if(values.Length!=3||values.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Invalid reviewed axes.");return new(values[0],values[1],values[2]);}
        int Bone(string role)=>Array.FindIndex(scene.Bones,b=>b.Name==review.Roles[role]);
        if(review.Roles.Any(p=>Array.FindIndex(scene.Bones,b=>b.Name==p.Value)<0))throw new InvalidDataException("A reviewed role references a missing native bone.");
        var setup=original with{Id=Guid.NewGuid(),Chains=[],Axes=new(Axis(review.Right),Axis(review.Up),Axis(review.Forward))};
        foreach(string side in new[]{"L","R"})foreach(string family in new[]{"Hand","Foot"})
        {
            string[] roles=family=="Hand"?["UpperArm."+side,"Elbow."+side,"Wrist."+side]:["UpperLeg."+side,"Knee."+side,"Ankle."+side];if(roles.Any(r=>!review.Roles.ContainsKey(r)))continue;
            int root=Bone(roles[0]),middle=Bone(roles[1]),end=Bone(roles[2]);
            var result=AnimationControls.ReviewChain(scene,setup,family+"."+side,root,middle,end,Axis(review.Forward),review.Evidence);
            if(result.Value is null)throw new NotSupportedException(string.Join("; ",result.Diagnostics.Select(d=>d.Message)));setup=result.Value;
        }
        foreach(var pair in review.Roles){var result=AnimationControls.ReviewBoneRole(scene,setup,pair.Key,Bone(pair.Key),review.Evidence);if(result.Value is null)throw new InvalidDataException(string.Join("; ",result.Diagnostics.Select(d=>d.Message)));setup=result.Value;}
        return setup;
    }
    string State()
    {
        var d=document!;bool animated=d.Project.Clips.Length>0;
        return JsonSerializer.Serialize(new ControlState(d.SceneFingerprint,d.Clip.Id,setup!,animated?d.Project.Contacts.Where(c=>c.Clip==d.Clip.Id).ToArray():[],animated?[]:d.Project.AuthoringContacts,d.CurrentBonePins,d.Project.AuthoringPose,bindingKey),ProjectStore.Json);
    }
    PinView[] Pins()
    {
        var d=document!;var p=d.Evaluate();
        PinView View(Guid id,int b,string name,bool soft){var q=PoseGeometry.Rotation(p.World[b]);var v=p.World[b].Translation;return new(id.ToString(),b,d.Project.Scene.Bones[b].Name,name,soft,[v.X,v.Y,v.Z],[q.X,q.Y,q.Z,q.W]);}
        return d.CurrentContacts.Where(p=>p.EditorBehavior==EditorPinBehavior.SoftReach).Select(p=>View(p.Id,p.Bone,p.Role,true)).Concat(d.CurrentBonePins.Select(p=>View(p.Id,p.Bone,d.Project.Scene.Bones[p.Bone].Name,false))).ToArray();
    }
    object Reply(bool state=true)=>new{Ok=true,State=state?State():null,BoneNames=document!.Project.Scene.Bones.Select(b=>b.Name).ToArray(),Pose=document!.Evaluate().Local.Select(t=>t.Channels()).ToArray(),Pins=Pins(),Chains=setup!.Chains.Select(c=>new{c.Role,c.Root,c.Middle,c.Effector,c.Evidence,c.Reviewed,Pole=new[]{c.PoleDirection.X,c.PoleDirection.Y,c.PoleDirection.Z},PoleParent=c.Calibration is {} calibration?new[]{calibration.PoleInParentSpace.X,calibration.PoleInParentSpace.Y,calibration.PoleInParentSpace.Z}:null}),Roles=setup.BoneRoles.Select(r=>new{Role=r.SemanticRole,Bone=(int?)r.Bone,r.Reviewed,r.Evidence}).Concat((setup.UnassignedBoneRoles??[]).Select(r=>new{Role=r,Bone=(int?)null,Reviewed=true,Evidence="Explicitly unassigned"})),Diagnostics=setup.Diagnostics.Select(d=>d.Message).ToArray()};
}
