using System.Numerics;
using System.Text.Json;
using FighterPort.Core;

internal static class FrameEditingChecks
{
    static FighterProject Fixture()
    {
        var scene=new Scene("Original viewer command fixture",[new("root",-1,Transform.Identity),new("child",0,new(Vector3.One,new(11,12,13),new(2,3,4)))],[]);
        var root=new Curve?[9];root[0]=new([new(0,1),new(4,1)],Interpolation.Linear);root[3]=new([new(0,10,7,9),new(2,30,3,4),new(2,35,5,6),new(4,50,8,2)]);root[6]=new([new(0,1),new(1.25,4),new(2,5),new(4,9)],Interpolation.Linear);
        var child=new Curve?[9];child[7]=new([new(0,3),new(2,8),new(4,13)],Interpolation.Linear);
        return new(1,Guid.NewGuid(),"Original command fixture",scene,[new(Guid.NewGuid(),"Motion",5,new(60),false,[new("root",root),new("child",child)])]);
    }
    public static async Task RunAsync(string dir,Action<bool,string> check)
    {
        Directory.CreateDirectory(dir);var original=Fixture();var originalText=JsonSerializer.Serialize(original,ProjectStore.Json);var d=new EditorDocument(original);d.Seek(2);
        var single=d.CopyAnimationFrame();var all=d.CopyAnimationFrame(true);
        var resized=new EditorDocument(original);resized.ResizeAnimation(10);var stretched=resized.Clip.Tracks[0].Channels[3]!;
        check(resized.Clip.SampleCount==10&&stretched.Keys[1]==new Key(4,30,1.5f,2)&&stretched.Keys[2]==new Key(4,35,2.5f,3),"Resize scales time and both duplicate sides with reciprocal tangent scaling");
        check(Math.Abs(stretched.Sample(2.5)-original.Clips[0].Tracks[0].Channels[3]!.Sample(1.25))<.00001f,"Resized continuous Hermite motion agrees at independently mapped fractional time");
        resized.Undo();check(ReferenceEquals(resized.Project,original),"Resize is one exact Undo and retains native source ownership");
        check(!d.Dirty&&!d.CanUndo&&single.Bones.Single().Values[6]==5&&single.Bones.Single().Keyed[6],"Frame copy is nonmutating and samples the exact selected key");
        check(!single.Bones.Single().Keyed[0]&&single.Bones.Single().Values[0]==1,"Frame buffer distinguishes an evaluated value from an authored key");
        check(all.Bones.Length==2&&all.Bones[1].Values[7]==8,"Whole-frame buffer covers animation entries at the current time");
        d.Seek(3);check(d.PasteAnimationFrame(single,TransformComponents.Translation),"Filtered frame paste creates eligible translation keys");
        check(d.Clip.Tracks[0].Channels[6]!.Keys.Single(k=>k.Frame==3).Value==5,"Frame paste writes at the destination time rather than every sample");
        check(ReferenceEquals(d.Clip.Tracks[0].Channels[3],original.Clips[0].Tracks[0].Channels[3]),"Filtered paste preserves unchecked rotations and native duplicate/tangent state");
        var after=d.Project;d.Undo();check(ReferenceEquals(d.Project,original),"One Undo restores the exact complete pre-paste revision");d.Redo();check(ReferenceEquals(d.Project,after),"Redo restores stored post-paste curves without recomputation");
        d=new(original);d.Seek(2);single=d.CopyAnimationFrame();d.Seek(3);check(!d.PasteAnimationFrame(single,TransformComponents.Scale,keyedOnly:true)&&!d.Dirty&&!d.CanUndo,"Keyed-only paste ignores sampled but unkeyed channels without creating history");
        d.Seek(2);check(d.ClearAnimationFrame(TransformComponents.Translation),"Filtered clear removes selected current-frame translation keys");
        check(!d.Clip.Tracks[0].Channels[6]!.Keys.Any(k=>k.Frame==2)&&d.Clip.SampleCount==5,"Clear removes keys without deleting animation time");
        check(d.Clip.Tracks[0].Channels[3]!.Keys.Count(k=>k.Frame==2)==2&&d.Clip.Tracks[1].Channels[7]!.Keys.Single(k=>k.Frame==2).Value==8,"Bone clear preserves unchecked duplicate rotations and other bone entries");
        d.Undo();check(ReferenceEquals(d.Project,original),"Clear Undo restores all key-presence/tangent state exactly");
        check(d.ClearAnimationFrame(TransformComponents.Translation,true)&&!d.Clip.Tracks.Any(t=>t.Channels.Skip(6).OfType<Curve>().Any(c=>c.Keys.Any(k=>k.Frame==2))),"Whole-frame Clear spans all bone entries, only selected component groups");
        d.Undo();d.Seek(1.25);var fractional=d.CopyAnimationFrame();d.Seek(3.125);check(d.PasteAnimationFrame(fractional,TransformComponents.Translation)&&d.Clip.Tracks[0].Channels[6]!.Keys.Any(k=>k.Frame==3.125&&k.Value==4),"Fractional clipboard keys remain exact; no whole-frame rounding");
        d=new(original);d.Seek(2);check(d.InsertAnimationFrame()&&d.Clip.SampleCount==6,"Insert adds one native sample of time");
        check(d.Clip.Tracks[0].Channels[6]!.Keys.Select(k=>k.Frame).SequenceEqual(new double[]{0,1.25,3,5}),"Insert shifts later keys one unit and preserves earlier fractional keys");
        check(d.Clip.Tracks[0].Channels[3]!.Keys.Where(k=>k.Frame==3).Select(k=>(k.Value,k.InTangent,k.OutTangent)).SequenceEqual(new[]{(30f,3f,4f),(35f,5f,6f)}),"Insert preserves both sides of native duplicate keys and tangents");
        check(d.Clip.Tracks[1].Channels[7]!.Keys.Select(k=>k.Frame).SequenceEqual(new double[]{0,3,5}),"Time insertion spans every animation entry");
        var inserted=d.Project;d.Undo();check(ReferenceEquals(d.Project,original)&&d.Clock.Frame==2,"Insert Undo restores duration, exact keys and playhead");d.Redo();check(ReferenceEquals(d.Project,inserted),"Insert Redo restores the stored whole-clip result");
        d=new(original);d.Seek(2);check(d.DeleteAnimationFrame()&&d.Clip.SampleCount==4,"Delete removes one native unit of animation time");
        check(d.Clip.Tracks[0].Channels[6]!.Keys.Select(k=>k.Frame).SequenceEqual(new double[]{0,1.25,3})&&d.Clip.Tracks[0].Channels[3]!.Keys.Length==2,"Time Delete removes the interval and shifts later curves, including duplicate keys");
        d.Undo();check(ReferenceEquals(d.Project,original),"Time Delete is one exact Undo transaction");
        check(d.SetAnimationSampleCount(3)&&d.Clip.SampleCount==3&&d.Clip.Tracks[0].Channels[6]!.Keys.Max(k=>k.Frame)==2,"Total-count shortening truncates out-of-range keys without rescaling earlier times");
        d.Undo();check(ReferenceEquals(d.Project,original),"Total-count Undo restores removed keys and duration");
        check(d.SetAnimationSampleCount(8)&&d.Clip.Tracks[0].Channels[6]!.Keys.Select(k=>k.Frame).SequenceEqual(new double[]{0,1.25,2,4}),"Total-count extension preserves existing key indices");
        d=new(original);d.Seek(2);check(d.EditAnimationChannel(3,v=>v-180),"Subtract From All implements the reference minus-180 command");
        check(d.Clip.Tracks[0].Channels[3]!.Keys.Select(k=>k.Value).SequenceEqual(new float[]{-170,-150,-145,-130})&&d.Clip.Tracks[0].Channels[3]!.Keys[1].OutTangent==4,"Channel arithmetic edits existing values while preserving duplicate sides and tangents");
        d.Undo();check(ReferenceEquals(d.Project,original),"Channel edit Undo restores exact curves");
        d=new(original);var cleanup=d.PlanAnimationCleanup();check(cleanup.Changes.Length==1&&cleanup.After.Tracks[0].Channels[0]!.Keys.Length==1&&!d.Dirty,"Clean review identifies exact linear constants without mutating the document");
        check(cleanup.After.Tracks[0].Channels[3]!.Keys.SequenceEqual(original.Clips[0].Tracks[0].Channels[3]!.Keys),"Cleanup retains native nonconstant Hermite curves and tangents");
        check(d.ApplyAnimationCleanup(cleanup),"Reviewed exact cleanup commits once");d.Undo();check(ReferenceEquals(d.Project,original),"Clean Undo restores removed keys exactly");
        d.Seek(2);d.EditAnimationKey(0,3,2,new(2,32,3,4));var stale=d.Project;
        try{d.ApplyAnimationCleanup(cleanup);check(false,"Stale cleanup must reject");}catch(InvalidOperationException){check(ReferenceEquals(d.Project,stale),"Changed clip invalidates a pending cleanup review without partial mutation");}
        d.Undo();check(ReferenceEquals(d.Project,original),"Key editing uses the existing exact Undo history");
        var different=new EditorDocument(original with{Scene=original.Scene with{SkeletonRevision=Guid.NewGuid()}});try{different.PasteAnimationFrame(single,TransformComponents.All);check(false,"Foreign rig paste must reject");}catch(InvalidOperationException){check(!different.Dirty&&!different.CanUndo,"Clipboard identity rejects an unreviewed foreign rig revision");}
        d=new(original);d.BeginSourcePreview(original,original.Clips[0].Id);try{d.ClearAnimationFrame(TransformComponents.All,true);check(false,"Source clear must reject");}catch(InvalidOperationException){check(!d.Dirty,"Source-preview clipboard cannot authorize edits to the original");}d.EndSourcePreview();
        var owned=new EditorDocument(original);var other=new EditorDocument(original);owned.Seek(2);owned.ClearAnimationFrame(TransformComponents.All,true);check(ReferenceEquals(other.Project,original)&&!other.Dirty&&!other.CanUndo,"Another target document and its history remain independent");
        var synchronized=new EditorDocument(original with{NativeLayers=[new(Guid.NewGuid(),"Paired","VIS0",5,false,new(60),new("not-read","entry","hash","VIS0",0),[],"fixture")]});var synchronizedBefore=synchronized.Project;
        try{synchronized.InsertAnimationFrame();check(false,"Unqualified synchronized time editing must reject");}catch(NotSupportedException){check(ReferenceEquals(synchronized.Project,synchronizedBefore)&&!synchronized.Dirty,"Unsupported composition time edits reject atomically rather than silently desynchronizing layers");}
        var fraction=new EditorDocument(original);fraction.Seek(.5);try{fraction.DeleteAnimationFrame();check(false,"Fractional time Delete must reject");}catch(InvalidOperationException){check(!fraction.Dirty&&!fraction.CanUndo,"Structural time deletion never rounds a fractional playhead");}
        var looped=new EditorDocument(original with{Clips=[original.Clips[0] with{Loop=true}]});looped.Seek(5);check(looped.Clock.Frame==5&&looped.Clip.SampleCount==5,"Exact loop endpoint is a preview address without changing duration or native indices");looped.TogglePlayback();check(looped.Clock.Frame==0&&looped.Clock.Playing,"Play at the loop endpoint restarts from the first native sample");
        var empty=new EditorDocument(new(1,Guid.NewGuid(),"Empty",new("",[],[]),[]));check(empty.Project.Scene.Bones.Length==0&&empty.Evaluate().Skin.Length==0&&!empty.Dirty,"An empty viewer document has no substitute mesh, deform skeleton or authored keys");
        var modelOriginal=original with{Clips=[]};var modelOnly=new EditorDocument(modelOriginal);var restClipboard=modelOnly.CopyAnimationFrame();check(modelOnly.CopyAnimationFrame(true).Bones.Length==2&&!modelOnly.Dirty,"Model-only frame copy samples existing native bones without creating a clip");modelOnly.SelectedBone=1;
        check(modelOnly.PasteAnimationFrame(restClipboard,TransformComponents.Translation)&&modelOnly.Project.Clips.Length==0&&modelOnly.Project.AuthoringPose[1].Translation==Vector3.Zero&&ReferenceEquals(modelOnly.Project.Scene,modelOriginal.Scene),"Model-only paste edits the owned authoring pose and preserves the native deform scene without inventing an animation");modelOnly.Undo();check(ReferenceEquals(modelOnly.Project,modelOriginal),"One model-only clipboard Undo restores exact pose presence and native bind data");
        var saved=Path.Combine(dir,"edited.fighterport");await ProjectStore.SaveAsync(owned.Project,saved);var reopened=await ProjectStore.LoadAsync(saved);check(JsonSerializer.Serialize(reopened.Clips,ProjectStore.Json)==JsonSerializer.Serialize(owned.Project.Clips,ProjectStore.Json),"Edited native-channel revisions survive actual project save/reopen");
        var curves=new EditorDocument(original);var expected=curves.Clip.Tracks[0].Channels[3]!;var replacement=expected with{Keys=expected.Keys.Select((k,i)=>i==2?k with{Value=39,OutTangent=17}:k).ToArray()};
        check(curves.ReplaceAnimationCurve(0,3,expected,replacement)&&curves.Clip.Tracks[0].Channels[3]!.Keys[1]==expected.Keys[1]&&curves.Clip.Tracks[0].Channels[3]!.Keys[2].OutTangent==17,"Interpolation editing retains the untouched incoming side and changes the selected outgoing key/tangent");
        var newer=curves.Project;try{curves.ReplaceAnimationCurve(0,3,expected,expected);check(false,"Stale graph must reject");}catch(InvalidOperationException){check(ReferenceEquals(curves.Project,newer),"A stale interpolation graph cannot overwrite newer authored keys");}
        curves.Undo();check(ReferenceEquals(curves.Project,original),"One interpolation Undo restores exact curve identity and all duplicate/tangent data");
        try{curves.ReplaceAnimationCurve(0,3,expected,expected with{Keys=[new(0,float.NaN)]});check(false,"Nonfinite graph edit must reject");}catch(InvalidDataException){check(ReferenceEquals(curves.Project,original),"Nonfinite interpolation edits reject before document publication");}
        check(JsonSerializer.Serialize(original,ProjectStore.Json)==originalText&&ReferenceEquals(owned.Project.Scene,original.Scene),"All commands preserve the original source, deform hierarchy, binds, geometry and skin data");
        var projectWriter=new ProjectAnimationFileWriter();string savedResult=Path.Combine(dir,"retarget-result.fighterport");var savedReceipt=await projectWriter.WriteAnimationAsync(owned.Project,owned.Clip,savedResult);var savedResultProject=await ProjectStore.LoadAsync(savedResult);
        check(savedReceipt.Reopened&&savedReceipt.Sha256==ProjectStore.Hash(savedResult)&&savedResultProject.Scene.SkeletonRevision==owned.Project.Scene.SkeletonRevision&&savedResultProject.Clips.Any(c=>c.Id==owned.Clip.Id&&c.Revision==owned.Clip.Revision),"The shared writer contract saves and reopens the selected animation project with exact rig/revision identity");
        string savedHash=ProjectStore.Hash(savedResult);try{await projectWriter.WriteAnimationAsync(owned.Project,owned.Clip,savedResult);check(false,"Existing export must reject");}catch(IOException){check(ProjectStore.Hash(savedResult)==savedHash,"Saving a result through the shared writer cannot overwrite an existing file");}
        try{await projectWriter.WriteAnimationAsync(owned.Project,owned.Clip,Path.Combine(dir,"fake.chr0"));check(false,"Unsupported extension must reject");}catch(NotSupportedException){check(!File.Exists(Path.Combine(dir,"fake.chr0")),"A future-format extension cannot masquerade as a supported writer");}
        var vis=new NativeAnimationLayer(Guid.NewGuid(),"Original visibility","VIS0",4,false,new(30),new("unread-source","entry","hash","VIS0",0),[new("root",0,[],[true,false,false,true])],"prepared fixture");
        var color=new NativeAnimationLayer(Guid.NewGuid(),"Original color","CLR0",4,false,new(30),new("unread-source","entry","hash","CLR0",0),[new("material",0,[new([new(0,.1f),new(1,.3f,2,3),new(1,.8f,4,5),new(3,.9f)])])],"prepared fixture");
        var typedOriginal=original with{NativeLayers=[vis,color]};var typed=new EditorDocument(typedOriginal);
        check(typed.EditLayerVisibility(vis.Id,0,2,enabled:true)&&typed.Project.NativeLayers[0].Tracks[0].Visibility!.SequenceEqual(new[]{true,false,true,true}),"Visibility editing changes exactly one discrete sample");
        typed.Undo();check(ReferenceEquals(typed.Project,typedOriginal),"Visibility Undo restores the exact original layer and all companion data");
        check(typed.EditLayerVisibilityFrame(vis.Id,2,new Dictionary<int,bool>{{0,true}})&&typed.Project.NativeLayers[0].Tracks[0].Visibility![2],"Object visibility synchronization writes exactly the selected native visibility sample");typed.Undo();check(ReferenceEquals(typed.Project,typedOriginal),"Visibility synchronization is one exact Undo without changing sources or other layers");
        check(typed.EditLayerVisibility(vis.Id,0,1,constant:true)&&typed.Project.NativeLayers[0].Tracks[0].Visibility!.SequenceEqual(new[]{false}),"Constant visibility captures the selected sample without changing its timebase");
        check(typed.EditLayerVisibility(vis.Id,0,1,constant:false)&&typed.Project.NativeLayers[0].Tracks[0].Visibility!.SequenceEqual(new[]{false,false,false,false}),"Disabling Constant restores an editable full-duration visibility sequence");
        typed=new(typedOriginal);check(typed.EditLayerCurve(color.Id,0,0,1,new(1,.5f,2,3)),"Color keys use the same owned transaction service");
        var colorKeys=typed.Project.NativeLayers[1].Tracks[0].Curves[0]!.Keys;check(colorKeys.Length==4&&colorKeys[1].Value==.5f&&colorKeys[2]==new Key(1,.8f,4,5),"Color editing preserves the outgoing duplicate and neighboring tangents");
        typed.Undo();check(ReferenceEquals(typed.Project,typedOriginal),"Color Undo restores exact per-family curve/key-presence state");
        var scalarEdits=new Dictionary<int,IReadOnlyDictionary<int,float?>>{{0,new Dictionary<int,float?>{{0,.65f}}}};
        check(typed.EditLayerFrame(color.Id,1,scalarEdits)&&typed.Project.NativeLayers[1].Tracks[0].Curves[0]!.Keys[1].Value==.65f&&typed.Project.NativeLayers[1].Tracks[0].Curves[0]!.Keys[2].Value==.8f,"Typed frame paste changes the incoming key while retaining the exact outgoing duplicate");
        typed.Undo();check(ReferenceEquals(typed.Project,typedOriginal),"Typed frame paste is one exact Undo");
        try{typed.EditLayerFrame(color.Id,1,new Dictionary<int,IReadOnlyDictionary<int,float?>>{{0,new Dictionary<int,float?>{{0,.5f},{9,1}}}});check(false,"Invalid compound scalar edit must reject");}catch(InvalidDataException){check(ReferenceEquals(typed.Project,typedOriginal),"A bad channel rejects the whole scalar frame before publication");}
        var pat=new NativeAnimationLayer(Guid.NewGuid(),"Pattern","PAT0",4,false,new(30),new("unread-source","entry","hash","PAT0",0),[new("material",1,[]){TextureSwaps=[new(0,"base","base-palette"),new(3,"end",null)]}],"prepared fixture");
        var patOriginal=original with{NativeLayers=[pat]};var pattern=new EditorDocument(patOriginal);
        check(pattern.EditLayerTexture(pat.Id,0,1.5,"middle","alternate")&&pattern.Project.NativeLayers[0].Tracks[0].TextureSwaps.SequenceEqual(new[]{new TextureSwap(0,"base","base-palette"),new TextureSwap(1.5,"middle","alternate"),new TextureSwap(3,"end",null)}),"Texture-pattern editing preserves exact fractional time, palette and neighboring swaps");
        pattern.Undo();check(ReferenceEquals(pattern.Project,patOriginal),"Texture-pattern Undo restores exact original identity");pattern.Redo();check(pattern.EditLayerTexture(pat.Id,0,1.5,null,null)&&pattern.Project.NativeLayers[0].Tracks[0].TextureSwaps.SequenceEqual(pat.Tracks[0].TextureSwaps),"Texture-pattern cut removes only its selected authored event");
        try{typed.EditLayerVisibility(vis.Id,0,4,enabled:true);check(false,"Out-of-range visibility must reject");}catch(ArgumentOutOfRangeException){check(ReferenceEquals(typed.Project,typedOriginal),"Visibility out-of-range edits reject before mutation");}
        typed.BeginSourcePreview(typedOriginal,typedOriginal.Clips[0].Id);try{typed.EditLayerCurve(color.Id,0,0,1,new(1,.4f));check(false,"Read-only layer editing must reject");}catch(InvalidOperationException){check(typed.Project.NativeLayers[1].Tracks[0].Curves[0]!.Keys[1].Value==.3f,"Source-preview layers retain read-only ownership");}
    }
}
