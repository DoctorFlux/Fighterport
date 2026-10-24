using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FighterPort.Core;

public record FighterProject(int SchemaVersion, Guid Id, string Name, Scene Scene, AnimationClip[] Clips)
{
    public Dictionary<int,Transform> AuthoringPose {get;init;}=[];
    public ContactPin[] AuthoringContacts {get;init;}=[];
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public EditorBonePin[]? BonePins {get;init;}
    public MotionCoverageLabel[] MotionCoverage {get;init;}=[];
    public TargetProfile[] Targets { get; init; }=[];
    public OutfitVariant[] Outfits { get; init; }=[];
    public SlotAssignment[] Slots { get; init; }=[];
    public MoveDefinition[] Moves { get; init; }=[];
    public ScriptDocument[] Scripts { get; init; }=[];
    public AudioCue[] Audio { get; init; }=[];
    public VfxBinding[] Effects { get; init; }=[];
    public PortraitSet Portraits { get; init; }=new(null,null,null);
    public NativeResourceEntry[] NativeResources { get; init; }=[];
    public NativeAnimationLayer[] NativeLayers { get; init; }=[];
    public SourceModelRevision[] SourceModels { get; init; }=[];
    public ControlRigSetup[] ControlRigs { get; init; }=[];
    public ContactFrame[] Contacts {get;init;}=[];
    public SourceAssetSnapshot[] LibrarySources {get;init;}=[];
    public SourceBuildIdentity? SourceBuild {get;init;}
    public bool SourceBuildVerified {get;init;}
    public string? SourceProvider {get;init;}
    public string? SourceAssociationEvidence {get;init;}
    public Guid? LibraryRegistrationId {get;init;}
    public SourceFileProof[] SourceFiles {get;init;}=[];
    public FittingProfile[] FittingProfiles {get;init;}=[];
    public RetargetBatchReceipt[] RetargetBatches {get;init;}=[];
}
public sealed partial class EditorDocument
{
    public FighterProject Project { get; private set; }
    public FighterProject AuthoringProject=>sourcePreview?.Project??Project;
    public bool IsSourcePreview=>sourcePreview is not null;
    public AnimationClock Clock { get; }=new();
    public int SelectedBone { get; set; }=0;
    public int SelectedMorphTrack {get;set;}
    public int SelectedMorphPoint {get;set;}
    public int SelectedClip { get; private set; }
    readonly AnimationClip authoringPose=new(Guid.NewGuid(),"Authored Animation",60,new(60),false,[]);
    public AnimationClip Clip=>Project.Clips.Length==0?authoringPose:Project.Clips[SelectedClip];
    public Dictionary<int,Transform> PoseOverrides { get; }=[];
    public Dictionary<string,Guid> ActiveLayers { get; }=[];
    public bool AutoMatchNativeLayers {get;set;}=true;
    public Diagnostic[] LayerDiagnostics {get;private set;}=[];
    MorphEvaluation? morph;NativeAnimationLayer? morphLayer;Scene? morphScene;
    public event Action? Changed;
    readonly Stack<(FighterProject Before,FighterProject After,string Name,Guid BeforeClip,Guid AfterClip,double BeforeFrame,double AfterFrame)> undo=[];
    readonly Stack<(FighterProject Before,FighterProject After,string Name,Guid BeforeClip,Guid AfterClip,double BeforeFrame,double AfterFrame)> redo=[];
    public bool CanUndo=>!IsSourcePreview&&undo.Count>0;
    public bool CanRedo=>!IsSourcePreview&&redo.Count>0;
    public bool Dirty { get; private set; }
    // Monotonic even across Undo/Redo. Transport/selection/save do not change content.
    public long ContentVersion {get;private set;}
    Scene? fingerprintScene;string sceneFingerprint="";
    public string SceneFingerprint=>sceneFingerprint;
    public int SceneFingerprintPreparations {get;private set;}
    void PrepareSceneFingerprint(){if(ReferenceEquals(fingerprintScene,Project.Scene))return;sceneFingerprint=AnimationControls.Fingerprint(Project.Scene);fingerprintScene=Project.Scene;SceneFingerprintPreparations++;}
    public EditorDocument(FighterProject project) { Project=project; ProjectStore.Validate(project);PrepareSceneFingerprint(); MatchNativeLayers(); }
    public EvaluatedPose Evaluate()=>AnimationService.Evaluate(Project.Scene,Clip,Clock.Frame,Project.Clips.Length==0?Project.AuthoringPose.Concat(PoseOverrides).GroupBy(p=>p.Key).ToDictionary(g=>g.Key,g=>g.Last().Value):PoseOverrides);
    public void SelectClip(int index) { SelectedClip=Project.Clips.Length==0?0:Math.Clamp(index,0,Project.Clips.Length-1); PoseOverrides.Clear(); Clock.Seek(0,Clip); if(AutoMatchNativeLayers)MatchNativeLayers();Changed?.Invoke(); }
    public void Seek(double frame) { Clock.Playing=false; PoseOverrides.Clear(); Clock.Seek(frame,Clip); Changed?.Invoke(); }
    public void Tick(double seconds) { Clock.Advance(seconds,Clip); }
    public void TogglePlayback(){if(Project.Clips.Length==0)return;if(Clock.Playing)Clock.Playing=false;else Clock.Play(Clip);Changed?.Invoke();}
    public void CreateAnimation(string name,int samples,RationalRate rate,bool fromCurrentPose,bool duplicate=false)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Contains('\0')||samples<1)throw new ArgumentException("Choose a name and a positive native sample count.");rate.Validate();
        bool firstPose=Project.Clips.Length==0&&fromCurrentPose;
        var previous=Clip;var pose=Evaluate();var tracks=duplicate?previous.Tracks:fromCurrentPose?Project.Scene.Bones.Select((b,i)=>new BoneTrack(b.Name,pose.Local[i].Channels().Select(v=>(Curve?)new Curve([new(0,v)])).ToArray())).ToArray():[];
        var clip=new AnimationClip(Guid.NewGuid(),name,samples,rate,duplicate&&previous.Loop,tracks){Source=duplicate?previous.Source:null,NativeTarget=previous.NativeTarget??previous.Source,NativeWrite=NativeWriteMode.NewResource,NativeWriteName=name,SourceSnapshot=duplicate?previous.SourceSnapshot:null,Retarget=duplicate?previous.Retarget:null};
        Transaction(duplicate?"Duplicate animation":"Create animation",p=>p with
        {
            Clips=p.Clips.Append(clip).ToArray(),
            Contacts=firstPose&&p.AuthoringContacts.Length>0?p.Contacts.Append(new ContactFrame(Guid.NewGuid(),clip.Id,0,sceneFingerprint,p.AuthoringContacts){EndFrame=clip.LastSampleFrame}).ToArray():duplicate?p.Contacts.Concat(p.Contacts.Where(c=>c.Clip==previous.Id).Select(c=>c with{Id=Guid.NewGuid(),Clip=clip.Id,Pins=c.Pins.Select(pin=>pin with{Id=Guid.NewGuid()}).ToArray()})).ToArray():p.Contacts,
            AuthoringContacts=firstPose?[]:p.AuthoringContacts,
            BonePins=firstPose?p.BonePins?.Select(pin=>pin.Clip is null?pin with{Clip=clip.Id}:pin).ToArray():duplicate&&p.BonePins is {} pins?pins.Concat(pins.Where(pin=>pin.Clip==previous.Id).Select(pin=>pin with{Id=Guid.NewGuid(),Clip=clip.Id})).ToArray():p.BonePins
        });SelectClip(Project.Clips.Length-1);
    }
    public void RenameAnimation(string name)
    {
        if(Project.Clips.Length==0)throw new InvalidOperationException("Create an animation before renaming it.");
        if(string.IsNullOrWhiteSpace(name)||name.Contains('\0'))throw new ArgumentException("Choose a valid animation name.");
        Transaction("Rename animation",p=>p with{Clips=p.Clips.Select((c,i)=>i==SelectedClip?c with{Name=name,NativeWriteName=name}:c).ToArray()});
    }
    public ContactPin[] CurrentContacts=>Project.Clips.Length==0?Project.AuthoringContacts:Project.Contacts.Where(c=>c.Clip==Clip.Id&&c.Contains(Clock.Frame)&&c.SkeletonFingerprint==sceneFingerprint).SelectMany(c=>c.Pins).Select(p=>p.EditorMotion?.Resolve(Project.Scene,p,Clock.Frame,Clip.Rate)??p).ToArray();
    public void PinPoint(ContactPin pin,double end)
    {
        if(Project.Clips.Length==0){Transaction("Pin model-pose support",p=>p with{AuthoringContacts=p.AuthoringContacts.Append(pin).ToArray()});return;}
        if(PoseOverrides.Count>0)throw new InvalidOperationException("Bake the current pose before pinning contacts.");
        if(end<Clock.Frame||end>Clip.LastSampleFrame)throw new InvalidOperationException("The contact end must be between this frame and the final sample.");
        var clip=Clip;Transaction("Pin support point",p=>p with{Clips=p.Clips.Length==0?[clip]:p.Clips,Contacts=p.Contacts.Append(new ContactFrame(Guid.NewGuid(),clip.Id,Clock.Frame,AnimationControls.Fingerprint(p.Scene),[pin]){EndFrame=end}).ToArray()});
    }
    public void PinControls(ControlRigSetup setup,string[] roles,EditorPinBehavior behavior=EditorPinBehavior.Fixed,bool followExistingAnimation=false)
    {
        if(PoseOverrides.Count>0)throw new InvalidOperationException("Bake the current pose before pinning contacts.");
        if(!Enum.IsDefined(behavior))throw new ArgumentOutOfRangeException(nameof(behavior));
        if(followExistingAnimation&&(Project.Clips.Length==0||behavior!=EditorPinBehavior.SoftReach))throw new InvalidOperationException("Following animation requires an existing clip and editor soft pins; fixed support locks retain their authored policy.");
        var clip=Clip;var pose=Evaluate();var pins=roles.Select(role=>{var pin=ContactSolver.Capture(Project.Scene,pose,setup,role);return pin with{EditorBehavior=behavior,EditorMotion=followExistingAnimation?EditorPinMotion.Capture(Project.Scene,clip,pin.Bone,Clock.Frame):null,Provenance=behavior==EditorPinBehavior.SoftReach?"Editor soft pin: "+(followExistingAnimation?"follow frozen existing animation; ":"hold world anchor; ")+"yield only to existing-chain reach or explicitly edited joints":"Authored exact pin"};}).ToArray();
        if(Project.Clips.Length==0){Transaction("Pin model-pose controls",p=>p with{AuthoringContacts=p.AuthoringContacts.Where(old=>!roles.Contains(old.Role)).Concat(pins).ToArray()});return;}
        if(behavior==EditorPinBehavior.SoftReach)
        {
            double start=Clock.Frame;var contacts=new List<ContactFrame>();
            foreach(var contact in Project.Contacts)
            {
                if(contact.Clip!=clip.Id||(contact.EndFrame??contact.Frame)<start||contact.EndExclusive&&contact.EndFrame==start||!contact.Pins.Any(p=>roles.Contains(p.Role))){contacts.Add(contact);continue;}
                var others=contact.Pins.Where(p=>!roles.Contains(p.Role)).ToArray();if(others.Length>0)contacts.Add(contact with{Pins=others});
                if(contact.Frame<start)contacts.Add(contact with{Id=Guid.NewGuid(),Pins=contact.Pins.Where(p=>roles.Contains(p.Role)).ToArray(),EndFrame=start,EndExclusive=true});
            }
            contacts.Add(new(Guid.NewGuid(),clip.Id,start,sceneFingerprint,pins){EndFrame=Math.Max(start,clip.LastSampleFrame)});
            Transaction(followExistingAnimation?"Soft pin following existing animation":"Soft pin in world space",p=>p with{Contacts=contacts.ToArray()});return;
        }
        Transaction("Pin existing-bone contacts",p=>p with{Clips=p.Clips.Length==0?[clip]:p.Clips,Contacts=p.Contacts.Where(c=>c.Clip!=clip.Id||Math.Abs(c.Frame-Clock.Frame)>.000001).Append(new(Guid.NewGuid(),clip.Id,Clock.Frame,AnimationControls.Fingerprint(p.Scene),p.Contacts.Where(c=>c.Clip==clip.Id&&c.Contains(Clock.Frame)&&c.SkeletonFingerprint==sceneFingerprint).SelectMany(c=>c.Pins).Where(old=>!roles.Contains(old.Role)).Concat(pins).ToArray())).ToArray()});
    }
    public void ReleaseContacts()=>Transaction("Release contacts",p=>p.Clips.Length==0?p with{AuthoringContacts=[]}:p with{Contacts=p.Contacts.Where(c=>c.Clip!=Clip.Id||!c.Contains(Clock.Frame)).ToArray()});
    public void Notify()=>Changed?.Invoke();
    public NativeAnimationLayer? Layer(string kind)=>ActiveLayers.TryGetValue(kind,out var id)?Project.NativeLayers.FirstOrDefault(x=>x.Id==id):null;
    public double LayerFrame(NativeAnimationLayer layer,double? clipFrame=null){if(layer.SampleCount<=0)return 0;double frame=(clipFrame??Clock.Frame)/Clip.Rate.FramesPerSecond*layer.Rate.FramesPerSecond;return layer.Loop?((frame%layer.SampleCount)+layer.SampleCount)%layer.SampleCount:Math.Clamp(frame,0,layer.SampleCount-1);}
    public MorphEvaluation? EvaluateMorph(EvaluatedPose? pose=null,double? clipFrame=null){if(morph is not null&&morphLayer is not null)morph.Sample(LayerFrame(morphLayer,clipFrame),pose??Evaluate());return morph;}
    void PrepareMorph()
    {
        var layer=Layer("SHP0");if(!ReferenceEquals(morphLayer,layer)||!ReferenceEquals(morphScene,Project.Scene)){morph=layer is {Morphs.Length:>0}?new(Project.Scene,layer):null;morphLayer=layer;morphScene=Project.Scene;}
        LayerDiagnostics=LayerDiagnostics.Where(d=>!d.Code.StartsWith("MORPH_",StringComparison.Ordinal)).Concat(layer?.Diagnostics??[]).ToArray();
    }
    public void SetLayer(string kind,Guid? id){if(kind=="SCN0"&&id is not null)throw new NotSupportedException("Scene animation is deferred; its original resource is preserved.");if(id is {} value){if(!Project.NativeLayers.Any(l=>l.Id==value&&l.Kind==kind))throw new ArgumentException("Layer does not belong to this document/family.");ActiveLayers[kind]=value;}else ActiveLayers.Remove(kind);PrepareMorph();Changed?.Invoke();}
    public void MatchNativeLayers()
    {
        ActiveLayers.Clear();var diagnostics=new List<Diagnostic>();
        if((Clip.NativeTarget??Clip.Source) is not {} source||source.Resource.Format!="CHR0"){LayerDiagnostics=[];PrepareMorph();return;}
        string suffix=" ["+source.Resource.Entry+"]";string name=source.NativeName??(Clip.Name.EndsWith(suffix,StringComparison.Ordinal)?Clip.Name[..^suffix.Length]:Clip.Name);
        if(Clip.NativeWrite==NativeWriteMode.NewResource)name=Clip.NativeWriteName??Clip.Name;
        int CommonPrefix(string entry){var a=source.Resource.Entry.Split('/');var b=entry.Split('/');int i=0;while(i<a.Length&&i<b.Length&&a[i]==b[i])i++;return i;}
        static string Container(NativeResourceRef resource){if(resource.ContainerEntry is {} owner)return owner;var parts=resource.Entry.Split('/');return string.Join('/',parts.Take(Math.Max(1,parts.Length-2)));}
        foreach(var family in Project.NativeLayers.Where(l=>l.Kind!="SCN0"&&l.Name==name&&l.Resource.Sha256==source.Resource.Sha256&&string.Equals(Path.GetFullPath(l.Resource.Path),Path.GetFullPath(source.Resource.Path),StringComparison.OrdinalIgnoreCase)).GroupBy(l=>l.Kind))
        {
            var eligible=family.Where(l=>Container(l.Resource)==Container(source.Resource)).ToArray();if(eligible.Length==0){diagnostics.Add(new("COMPANION_CONTAINER",$"{family.Key} '{name}' exists only in another native container: {string.Join(", ",family.Select(l=>l.Resource.Entry))}. Select explicitly to combine them.",false));continue;}
            int best=eligible.Max(l=>CommonPrefix(l.Resource.Entry));var candidates=eligible.Where(l=>CommonPrefix(l.Resource.Entry)==best).ToArray();
            if(candidates.Length==1)ActiveLayers[family.Key]=candidates[0].Id;
            else diagnostics.Add(new("AMBIGUOUS_COMPANION",$"{family.Key} '{name}' has equally close native entries: {string.Join(", ",candidates.Select(l=>l.Resource.Entry))}. Choose the intended layer.",false));
        }
        LayerDiagnostics=diagnostics.ToArray();PrepareMorph();
    }
    public void Transaction(string name, Func<FighterProject,FighterProject> edit)
    {
        RequireEditing();var selected=MorphSelection();var before=Project; var after=edit(before); ProjectStore.Validate(after);
        undo.Push((before,after,name,Clip.Id,Clip.Id,Clock.Frame,Clock.Frame)); redo.Clear(); Project=after;PrepareSceneFingerprint();ContentVersion++;RestoreMorphSelection(selected);PrepareMorph(); PoseOverrides.Clear(); Dirty=true; Changed?.Invoke();
    }
    public void SetPose(int bone, Transform value) { RequireEditing();PoseOverrides[bone]=value;ContentVersion++; Changed?.Invoke(); }
    public void EditMorphKey(Guid layer,int track,double frame,Key? key)
    {
        if(!Project.NativeLayers.Any(l=>l.Id==layer))throw new ArgumentException("Morph layer does not belong to this document.");
        var edited=MorphEditing.Edit(Project.NativeLayers.Single(l=>l.Id==layer),track,frame,key);Clock.Playing=false;Transaction(key is null?"Delete morph key":"Key morph weight",p=>p with{NativeLayers=p.NativeLayers.Select(l=>l.Id==layer?edited:l).ToArray()});
    }
    public void PreviewTransforms(IReadOnlyDictionary<int,Transform> values){RequireEditing();foreach(var p in values)PoseOverrides[p.Key]=p.Value;ContentVersion++;Changed?.Invoke();}
    public void CreateMorphTarget(Guid layer,int binding,string name)
    {
        Clock.Playing=false;Transaction("Create morph target",p=>MorphGeometryEditing.Create(p,layer,binding,name));SelectedMorphTrack=Project.NativeLayers.Single(l=>l.Id==layer).Tracks.Length-1;
    }
    public void EditMorphPoint(Guid layer,int track,int index,Vector3 point)
    {Clock.Playing=false;Transaction("Edit morph geometry",p=>MorphGeometryEditing.EditPoint(p,layer,track,index,point));}
    public void KeyTransform(int bone, Transform value)
        =>KeyTransforms(new Dictionary<int,Transform>{{bone,value}},"Key bone transform");
    public void KeyTransforms(IReadOnlyDictionary<int,Transform> values,string name="Bake animation controls")
    {
        var frame=Clock.Frame;
        Transaction(name,p=> {
            var next=Clip;
            foreach(var pair in values){var boneName=p.Scene.Bones[pair.Key].Name;var track=next.Tracks.FirstOrDefault(t=>t.Bone==boneName)??new BoneTrack(boneName,new Curve?[9]);var channels=(Curve?[])track.Channels.Clone();var v=pair.Value.Channels();for(int c=0;c<9;c++)channels[c]=(channels[c]??new Curve([])).Put(new(frame,v[c]));var rotation=track.Rotation?.Put(new(frame,Quaternion.CreateFromRotationMatrix((Transform.Identity with{Rotation=pair.Value.Rotation}).Matrix)));next=next with{Tracks=next.Tracks.Where(t=>t.Bone!=boneName).Append(new BoneTrack(boneName,channels){Rotation=rotation}).ToArray(),Revision=Guid.NewGuid(),Retarget=next.Retarget is {} recipe?recipe with{Validation=null,IsDraft=true}:null};}
            var clips=p.Clips.Length==0?new AnimationClip[1]:(AnimationClip[])p.Clips.Clone(); clips[SelectedClip]=next; return p with { Clips=clips };
        });
    }
    void RestoreSelection(Guid id,double frame){SelectedClip=Math.Max(0,Array.FindIndex(Project.Clips,c=>c.Id==id));Clock.Playing=false;Clock.Seek(frame,Clip);if(AutoMatchNativeLayers)MatchNativeLayers();else PrepareMorph();}
    (Guid Layer,string Base,string? Target)? MorphSelection(){var layer=Layer("SHP0");return layer is not null&&SelectedMorphTrack>=0&&SelectedMorphTrack<layer.Tracks.Length?(layer.Id,layer.Tracks[SelectedMorphTrack].Target,layer.Tracks[SelectedMorphTrack].MorphTarget):null;}
    void RestoreMorphSelection((Guid Layer,string Base,string? Target)? selected){if(selected is not {} s)return;var layer=Project.NativeLayers.FirstOrDefault(l=>l.Id==s.Layer);if(layer is null)return;int index=Array.FindIndex(layer.Tracks,t=>t.Target==s.Base&&t.MorphTarget==s.Target);if(index>=0)SelectedMorphTrack=index;}
    public void Undo() { if(IsSourcePreview)return;if(undo.TryPop(out var t)) {var selected=MorphSelection();redo.Push((t.Before,t.After,t.Name,t.BeforeClip,Clip.Id,t.BeforeFrame,Clock.Frame));Project=t.Before;PrepareSceneFingerprint();ContentVersion++;RestoreSelection(t.BeforeClip,t.BeforeFrame);RestoreMorphSelection(selected);PoseOverrides.Clear(); Dirty=true; Changed?.Invoke(); } }
    public void Redo() { if(IsSourcePreview)return;if(redo.TryPop(out var t)) {var selected=MorphSelection();Project=t.After;PrepareSceneFingerprint();ContentVersion++;RestoreSelection(t.AfterClip,t.AfterFrame);RestoreMorphSelection(selected);undo.Push(t);PoseOverrides.Clear(); Dirty=true; Changed?.Invoke(); } }
    public void Replace(FighterProject project) { RequireEditing();ProjectStore.Validate(project); Project=project;PrepareSceneFingerprint();ContentVersion++; SelectedClip=0; SelectedBone=0; undo.Clear(); redo.Clear(); PoseOverrides.Clear(); ActiveLayers.Clear();if(AutoMatchNativeLayers)MatchNativeLayers();PrepareMorph(); Clock.Playing=false; Clock.Seek(0,Clip); Dirty=false; Changed?.Invoke(); }
    public void MarkSaved() { Dirty=false;if(sourcePreview is {} preview)sourcePreview=preview with{Dirty=false};Changed?.Invoke(); }
}

public static partial class ProjectStore
{
    public static JsonSerializerOptions Json { get; }=new() { IncludeFields=true, PropertyNameCaseInsensitive=true, WriteIndented=false,
        IgnoreReadOnlyProperties=true, Converters={new JsonStringEnumConverter()} };
    public static void Validate(FighterProject p)
    {
        if(p.SchemaVersion!=1) throw new InvalidDataException($"Unsupported project schema {p.SchemaVersion}.");
        p.Scene.Validate();
        if(p.BonePins is {} editorPins&&(editorPins.Any(pin=>pin.Id==Guid.Empty||pin.Bone<0||pin.SkeletonFingerprint==AnimationControls.Fingerprint(p.Scene)&&pin.Bone>=p.Scene.Bones.Length)||editorPins.Select(pin=>pin.Id).Distinct().Count()!=editorPins.Length))throw new InvalidDataException("Invalid editor bone pin identity.");
        if(p.AuthoringPose.Any(pose=>pose.Key<0||pose.Key>=p.Scene.Bones.Length||!pose.Value.Channels().All(float.IsFinite)))throw new InvalidDataException("Invalid model-only authoring pose.");
        if(p.AuthoringContacts.Any(pin=>pin.Bone<0||pin.Bone>=p.Scene.Bones.Length||!ContactGeometry.Valid(pin,p.Scene.Bones.Length)))throw new InvalidDataException("Invalid model-only support anchor.");
        foreach(var profile in p.FittingProfiles){if(string.IsNullOrWhiteSpace(profile.Key)||string.IsNullOrWhiteSpace(profile.Name))throw new InvalidDataException("Invalid fitting-profile identity.");profile.Settings.Validate();}
        foreach(var layer in p.NativeLayers){layer.Rate.Validate();if(layer.SampleCount<1&&layer.Morphs.Length>0)throw new InvalidDataException("Invalid morph timebase/sample count.");MorphEvaluation.Validate(p.Scene,layer);}
        foreach(var clip in p.Clips)
        {
            clip.Rate.Validate();
            if(clip.SampleCount<1) throw new InvalidDataException("Invalid clip sample count.");
            if(clip.Retarget is {} recipe){recipe.Fitting.Validate();if(recipe.ContactEdits.Any(e=>!double.IsFinite(e.Start)||!double.IsFinite(e.End)||e.Start<0||e.End<e.Start||!Enum.IsDefined(e.Action)||!Enum.IsDefined(e.TimeDomain)))throw new InvalidDataException("Invalid saved contact policy.");}
            foreach(var track in clip.Tracks)
            {
                if(track.Channels.Length!=9) throw new InvalidDataException("Expected nine SRT channel slots.");
                if(track.Rotation is {} rotation)
                {
                    if(rotation.Keys.Length==0||rotation.Keys.Any(k=>!double.IsFinite(k.Frame)||!float.IsFinite(k.Value.LengthSquared())||k.Value.LengthSquared()<.000001f))throw new InvalidDataException("Invalid quaternion rotation channel.");
                    if(!rotation.Keys.Select(k=>k.Frame).SequenceEqual(rotation.Keys.Select(k=>k.Frame).Distinct().Order()))throw new InvalidDataException("Unsorted or duplicate quaternion keys.");
                }
                foreach(var curve in track.Channels.OfType<Curve>())
                {
                    if(curve.Keys.Length==0 || curve.Keys.Any(k=>!double.IsFinite(k.Frame)||!float.IsFinite(k.Value)||!float.IsFinite(k.InTangent)||!float.IsFinite(k.OutTangent))) throw new InvalidDataException("Invalid curve.");
                    if(!curve.Keys.Select(k=>k.Frame).SequenceEqual(curve.Keys.Select(k=>k.Frame).Order())) throw new InvalidDataException("Unsorted keys.");
                }
            }
        }
        foreach(var contact in p.Contacts)
        {
            if(!p.Clips.Any(c=>c.Id==contact.Clip)||!double.IsFinite(contact.Frame)||contact.Frame<0||contact.EndFrame is {} end&&(!double.IsFinite(end)||end<contact.Frame))throw new InvalidDataException("Invalid contact interval.");
            if(contact.Pins.Any(pin=>pin.Bone<0||pin.Bone>=p.Scene.Bones.Length||!PoseGeometry.Finite(pin.LocalPoint)||!PoseGeometry.Finite(pin.Position)||!float.IsFinite(pin.Rotation.LengthSquared())||pin.Rotation.LengthSquared()<.000001f||pin.Pole is {} pole&&!PoseGeometry.Finite(pole)||!ContactGeometry.Valid(pin,p.Scene.Bones.Length)))throw new InvalidDataException("Invalid contact anchor.");
            if(contact.Pins.Any(pin=>pin.EditorMotion is not null)){string fingerprint=AnimationControls.Fingerprint(p.Scene);foreach(var pin in contact.Pins)pin.EditorMotion?.Validate(p.Scene,pin,fingerprint);}
            foreach(var pin in contact.Pins){pin.Reference?.Validate(p.Scene.Bones.Length);if(!Enum.IsDefined(pin.Mode))throw new InvalidDataException("Invalid contact mode.");foreach(var point in pin.SurfacePoints.Concat(pin.SurfaceRegion).Concat(pin.Clearance?.Points??[]).Concat(pin.Reference?.SurfaceTripod??[]))if(point.Mesh>=0&&(point.Mesh>=p.Scene.Meshes.Length||point.Vertex<0||point.Vertex>=p.Scene.Meshes[point.Mesh].Vertices.Length))throw new InvalidDataException("Contact surface identity is outside its calibrated mesh.");}
        }
        if(p.Clips.Any(c=>c.SourceSnapshot is {} id&&!p.LibrarySources.Any(s=>s.Id==id)))throw new InvalidDataException("Missing authentic source snapshot.");
        foreach(var source in p.LibrarySources)
        {
            if(source.Analysis is {} analysis&&(analysis.Clip!=source.Clip.Id||analysis.ClipRevision!=source.Clip.Revision||analysis.Key.Length!=64))throw new InvalidDataException("Source analysis belongs to a different authentic clip revision.");
            if(source.Clip.SourceSnapshot is {} id&&!p.LibrarySources.Any(s=>s.Id==id))throw new InvalidDataException("Missing ancestor source snapshot.");
            Validate(p with{Scene=source.Scene,Clips=[source.Clip with{SourceSnapshot=null}],NativeLayers=source.Layers,Contacts=source.Contacts,LibrarySources=[],BonePins=null});
        }
    }
    public static async Task SaveAsync(FighterProject project,string path,CancellationToken ct=default)
    {
        path=Path.GetFullPath(path); var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        if(PreservedSourcePaths(project).Any(p=>string.Equals(Path.GetFullPath(p),path,StringComparison.OrdinalIgnoreCase)))throw new IOException("Save the editable project to a new path distinct from preserved original/cache inputs.");
        try { await Task.Run(()=>{Validate(project);using var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None);WriteProject(stream,project,ct);stream.Flush(true);},ct).ConfigureAwait(false);ct.ThrowIfCancellationRequested();File.Move(temp,path,true); }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    public static async Task<FighterProject> LoadAsync(string path,CancellationToken ct=default)
    {
        return await Task.Run(()=>{using var stream=File.OpenRead(path);using var json=JsonDocument.Parse(stream);ct.ThrowIfCancellationRequested();return ReadProject(json.RootElement);},ct).ConfigureAwait(false);
    }
    public static string Hash(string path) { using var s=File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)); }
    public static IEnumerable<NativeResourceRef> SourceReferences(FighterProject project)=>project.Clips.SelectMany(c=>new[]{c.Source?.Resource,c.Source?.Model,c.NativeTarget?.Resource,c.NativeTarget?.Model})
        .OfType<NativeResourceRef>().Concat(project.NativeResources.Select(r=>r.Resource)).Concat(project.SourceModels.Select(m=>m.Resource)).Concat(project.NativeLayers.Select(l=>l.Resource)).Concat(project.LibrarySources.SelectMany(s=>s.Models.Select(m=>m.Resource).Concat(s.Resources.Select(r=>r.Resource)).Concat(s.Layers.Select(l=>l.Resource)).Concat(new[]{s.Clip.Source?.Resource,s.Clip.Source?.Model,s.Clip.NativeTarget?.Resource,s.Clip.NativeTarget?.Model}.OfType<NativeResourceRef>()))).Concat(project.SourceFiles.Concat(project.LibrarySources.SelectMany(s=>s.SourceFiles)).Select(f=>new NativeResourceRef(f.CachePath,"original",f.Sha256,"SOURCE_FILE",1){OriginalPath=f.OriginalPath}));
    public static IEnumerable<string> PreservedSourcePaths(FighterProject project)=>SourceReferences(project).SelectMany(r=>new[]{r.Path,r.OriginalPath}).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase);
    public static Diagnostic[] ValidateSources(FighterProject project)=>SourceReferences(project).DistinctBy(r=>(r.Path,r.Sha256)).Where(r=>!File.Exists(r.Path)||Hash(r.Path)!=r.Sha256)
        .Select(r=>new Diagnostic("SOURCE_CHANGED",$"Missing or changed source: {r.Path} [{r.Entry}]" )).ToArray();
}
