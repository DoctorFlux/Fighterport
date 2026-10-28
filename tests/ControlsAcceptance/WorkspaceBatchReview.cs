using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    static Task WorkspaceBatchReview(string[] args,string output) => LibraryBatch(args,output);
    static object[] ReviewRows(ModelForm form) => ((System.Collections.IEnumerable)Field(form,"_batchReviewRows")).Cast<object>().ToArray();
    static bool ReviewApproved(object row) => (bool)Field(row,"Approved");
    static void SetField(object owner,string name,object value)=>owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);
    static string LegacyReviewRecipe(FighterAsset asset)
    {
        // Frozen preview.32 observer, independent of the compact production
        // snapshot. Its complete result defines the existing equality contract.
        var json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024};
        var data=json.DeserializeObject(json.Serialize(asset.Retarget)) as Dictionary<string,object>;
        if(data!=null&&data.TryGetValue("Library",out var value)&&value is Dictionary<string,object> library)
        {library.Remove("LastExportSha256");library.Remove("LastExportFormat");}
        return json.Serialize(new{asset.RigIdentity,Recipe=data,asset.TransitionFit});
    }
    static void ReviewRecipeProjectionCases(ModelForm form)
    {
        var probe=new FighterAsset{RigIdentity="Procedural exact rig",Retarget=new RetargetProvenance{
            Timing=new NativeTimingFit{Start=1,End=8,Frames=9},Library=new LibraryClipReference(),DenseReferenceStatistics=new RetargetDenseReferenceStatistics(),
            IgnoredSourceBones=new[]{"first omitted","second omitted"},Mapping=new List<RetargetBoneBinding>{new RetargetBoneBinding{Role="first",Source="source a",Target="target a"},new RetargetBoneBinding{Role="second",Source="source b",Target="target b"}}},
            TransitionFit=new NativeTransitionFit{Inputs=new[]{new TransitionNativeInput{Role="source",Snapshot="AQID\r\n\"\\",Name="source",AssetId="asset a",Entry="entry a",Sha256="hash a",Samples=9},new TransitionNativeInput{Role="exit",Snapshot="BAUG",Name="exit",AssetId="asset b",Entry="entry b",Sha256="hash b",Samples=8}}}};
        foreach(var owner in new object[]{probe.Retarget,probe.Retarget.Library,probe.TransitionFit})
        foreach(var property in owner.GetType().GetProperties().Where(p=>p.PropertyType==typeof(string)))property.SetValue(owner,"Procedural "+property.Name+" \"\\\r\n");
        string[] expectedRetarget="KeyframeMode KeyframeReceipt DenseReferenceStatistics Timing ReviewDraft EngineReceipt Library Algorithm MotionControlPolicy SourceModelPath SourceModelSha256 SourceRig SourceAnimationId SourceClip FromProfile ToProfile LegScale ArmScale MaximumLimbResidual ClampedLimbSamples FloorContactsEnabled SuspendedSupportEnabled GroundMode PlantedFootSamples PlantedHandSamples PlantedBodySamples LockedContactSamples MaximumContactResidual MaximumPelvisCorrection IgnoredSourceBones Mapping".Split(' ');
        Check(typeof(RetargetProvenance).GetProperties().Select(p=>p.Name).OrderBy(n=>n).SequenceEqual(expectedRetarget.OrderBy(n=>n)),"Compact review projection covers the complete current retarget metadata schema");
        Check(typeof(RetargetDenseReferenceStatistics).GetProperties().Select(p=>p.Name).OrderBy(n=>n).SequenceEqual("FloorContactsEnabled SuspendedSupportEnabled PlantedFootSamples PlantedHandSamples PlantedBodySamples LockedContactSamples MaximumContactResidual MaximumPelvisCorrection MaximumLimbResidual ClampedLimbSamples".Split(' ').OrderBy(n=>n)),
            "Review projection covers every historical dense reference statistic");
        var keyframes=new RetargetKeyframeReport{Mode=RetargetKeyframeMode.SourceBoneTimes};
        foreach(var property in keyframes.GetType().GetProperties().Where(p=>p.PropertyType==typeof(string[])))property.SetValue(keyframes,new[]{"first "+property.Name,"second "+property.Name});
        var keyframeJson=new JavaScriptSerializer{MaxJsonLength=1024*1024};
        Action refreshKeys=()=>probe.Retarget.KeyframeReceipt=keyframeJson.Serialize(keyframes);refreshKeys();
        Check(typeof(NativeTransitionFit).GetProperties().Select(p=>p.Name).OrderBy(n=>n).SequenceEqual(new[]{"Algorithm","ModelSha256","Options","Receipt","InitialResultSha256","Inputs"}.OrderBy(n=>n))&&typeof(TransitionNativeInput).GetProperties().Select(p=>p.Name).OrderBy(n=>n).SequenceEqual(new[]{"Role","AssetId","Entry","Sha256","Snapshot","Name","Samples"}.OrderBy(n=>n)),"Compact review projection covers every transition and frozen-input field");
        string original=LegacyReviewRecipe(probe);object snapshot=Call(form,"ReviewRecipe",probe);
        void Mutation(string name,Action change,Action restore,bool same=false)
        {
            try{change();Check((LegacyReviewRecipe(probe)==original)==same&&snapshot.Equals(Call(form,"ReviewRecipe",probe))==same,"Compact review equality matches the complete legacy recipe: "+name);}
            finally{restore();}
        }
        void Scalars(object owner,string prefix,Action refresh=null)
        {
            foreach(var property in owner.GetType().GetProperties())
            {
                object before=property.GetValue(owner),changed=null;Type type=property.PropertyType;
                if(type==typeof(string))changed=(string)before+" changed";
                else if(type==typeof(bool))changed=!(bool)before;
                else if(type==typeof(int))changed=(int)before+1;
                else if(type==typeof(float))changed=(float)before+.25f;
                else if(type==typeof(long))changed=(long)before+1;
                else if(type==typeof(double))changed=(double)before+.25;
                else if(type.IsEnum)changed=Enum.ToObject(type,Convert.ToInt32(before)+1);
                else continue;
                Mutation(prefix+property.Name,()=>{property.SetValue(owner,changed);refresh?.Invoke();},()=>{property.SetValue(owner,before);refresh?.Invoke();},prefix=="Library."&&(property.Name=="LastExportSha256"||property.Name=="LastExportFormat"));
            }
        }
        Scalars(probe.Retarget,"Retarget.");Scalars(probe.Retarget.Timing,"Timing.");Scalars(probe.Retarget.Library,"Library.");Scalars(probe.TransitionFit,"Transition.");
        Scalars(probe.Retarget.DenseReferenceStatistics,"Dense reference.");Scalars(keyframes,"Source-key report.",refreshKeys);
        foreach(var property in keyframes.GetType().GetProperties().Where(p=>p.PropertyType==typeof(string[])))
        {
            var values=(string[])property.GetValue(keyframes);string firstValue=values[0];
            Mutation("source-key "+property.Name+" content",()=>{values[0]+=" changed";refreshKeys();},()=>{values[0]=firstValue;refreshKeys();});
            Mutation("source-key "+property.Name+" order",()=>{property.SetValue(keyframes,values.Reverse().ToArray());refreshKeys();},()=>{property.SetValue(keyframes,values);refreshKeys();});
        }
        foreach(var input in probe.TransitionFit.Inputs)Scalars(input,"Input "+input.Role+".");
        foreach(var binding in probe.Retarget.Mapping)Scalars(binding,"Mapping "+binding.Role+".");
        string rig=probe.RigIdentity;Mutation("exact target rig",()=>probe.RigIdentity+=" changed",()=>probe.RigIdentity=rig);
        var recipe=probe.Retarget;Mutation("null retarget",()=>probe.Retarget=null,()=>probe.Retarget=recipe);
        var timing=recipe.Timing;Mutation("null timing",()=>recipe.Timing=null,()=>recipe.Timing=timing);
        var denseReference=recipe.DenseReferenceStatistics;Mutation("null dense reference statistics",()=>recipe.DenseReferenceStatistics=null,()=>recipe.DenseReferenceStatistics=denseReference);
        var reference=recipe.Library;Mutation("null library",()=>recipe.Library=null,()=>recipe.Library=reference);
        var fit=probe.TransitionFit;Mutation("null transition",()=>probe.TransitionFit=null,()=>probe.TransitionFit=fit);
        var inputs=fit.Inputs;Mutation("null frozen inputs",()=>fit.Inputs=null,()=>fit.Inputs=inputs);
        Mutation("frozen input order",()=>fit.Inputs=inputs.Reverse().ToArray(),()=>fit.Inputs=inputs);
        var first=inputs[0];Mutation("null frozen input",()=>inputs[0]=null,()=>inputs[0]=first);
        var mapping=recipe.Mapping;Mutation("null mapping",()=>recipe.Mapping=null,()=>recipe.Mapping=mapping);
        Mutation("mapping order",()=>recipe.Mapping=mapping.AsEnumerable().Reverse().ToList(),()=>recipe.Mapping=mapping);
        var ignored=recipe.IgnoredSourceBones;Mutation("null omitted tracks",()=>recipe.IgnoredSourceBones=null,()=>recipe.IgnoredSourceBones=ignored);
        string omitted=ignored[0];Mutation("in-place omitted track",()=>ignored[0]+=" changed",()=>ignored[0]=omitted);
        string engine=recipe.EngineReceipt;Mutation("null receipt",()=>recipe.EngineReceipt=null,()=>recipe.EngineReceipt=engine);
        Mutation("equivalent immutable receipt value",()=>recipe.EngineReceipt=new string(engine.ToCharArray()),()=>recipe.EngineReceipt=engine,true);
        var copy=new JavaScriptSerializer().Deserialize<FighterAsset>(new JavaScriptSerializer().Serialize(probe));
        Check(snapshot.Equals(Call(form,"ReviewRecipe",copy)),"Deeply replaced but exactly equal recipe objects retain approval semantics");
        var text=(string[])Field(snapshot,"Text");
        Check(ReferenceEquals(text[0],recipe.EngineReceipt)&&text.Any(t=>ReferenceEquals(t,fit.Inputs[0].Snapshot)),"Large review fields retain exact immutable string identities without JSON copies");
    }
    static void ReviewLargeRecipeCases(ModelForm form,FighterAsset asset,object row,string output)
    {
        ReviewRecipeProjectionCases(form);
        var editor=form.FighterEditor;var clip=editor.SelectedCHR0;var bone=editor.SelectedBone;int frame=editor.CurrentFrame,undo=editor._undoSaves.Count,redo=editor._redoSaves.Count;
        string content=SetNativeContent(clip),oldEngine=asset.Retarget.EngineReceipt;var oldFit=asset.TransitionFit;
        // Large original procedural metadata stresses observer work only. These
        // opaque test snapshots are never baked, saved, exported or published.
        var frozen=new string('A',2*1024*1024);
        asset.Retarget.EngineReceipt=new string('r',128*1024);
        asset.TransitionFit=new NativeTransitionFit{Algorithm="Procedural observer probe",ModelSha256="procedural model",InitialResultSha256="procedural result",Options="{}",Receipt=new string('d',128*1024),Inputs=new[]{new TransitionNativeInput{Role="source",Snapshot=frozen,Name="source",Samples=9},new TransitionNativeInput{Role="entry",Snapshot=new string('B',2*1024*1024),Name="entry",Samples=9},new TransitionNativeInput{Role="exit",Snapshot=new string('C',2*1024*1024),Name="exit",Samples=9}}};
        try
        {
            Call(form,"MarkBatchReview");Check(ReviewApproved(row),"Large procedural recipe receives only explicit current session approval");
            string baseline=LegacyReviewRecipe(asset);int hashes=FighterProject.DiagnosticFileHashes,opens=AnimationLibrary.DiagnosticOpens,bakes=BrawlRetargeter.DiagnosticBakes;
            const int iterations=4;long oldCharacters=0;int oldGen0=GC.CollectionCount(0),oldGen1=GC.CollectionCount(1),oldGen2=GC.CollectionCount(2);
            var watch=Stopwatch.StartNew();for(int i=0;i<iterations;i++){string value=LegacyReviewRecipe(asset);if(value!=baseline)throw new Exception("Legacy procedural baseline changed.");oldCharacters+=value.Length;}watch.Stop();double oldMs=watch.Elapsed.TotalMilliseconds;
            oldGen0=GC.CollectionCount(0)-oldGen0;oldGen1=GC.CollectionCount(1)-oldGen1;oldGen2=GC.CollectionCount(2)-oldGen2;
            long before=form.BatchReviewRecipeScalarCharacters,retained=form.BatchReviewRecipeRetainedCharacters;int captures=form.BatchReviewRecipeCaptures,newGen0=GC.CollectionCount(0),newGen1=GC.CollectionCount(1),newGen2=GC.CollectionCount(2);
            watch.Restart();for(int i=0;i<iterations;i++){SetField(form,"_batchReviewDirty",true);Call(form,"SyncBatchReview");if(!ReviewApproved(row))throw new Exception("Unchanged procedural metadata lost approval.");}watch.Stop();
            long scalar=form.BatchReviewRecipeScalarCharacters-before;
            File.WriteAllText(Path.Combine(output,"review-recipe-performance.json"),new JavaScriptSerializer().Serialize(new{Workload="one approved large procedural recipe; four repeated unchanged observer checks",Iterations=iterations,LegacyRecipeMilliseconds=oldMs,CompactDirtySyncMilliseconds=watch.Elapsed.TotalMilliseconds,LegacyReturnedJsonCharacters=oldCharacters,CompactSerializedCharacters=scalar,RetainedImmutableCharacters=form.BatchReviewRecipeRetainedCharacters-retained,Captures=form.BatchReviewRecipeCaptures-captures,LegacyCollections=new[]{oldGen0,oldGen1,oldGen2},CompactCollections=new[]{GC.CollectionCount(0)-newGen0,GC.CollectionCount(1)-newGen1,GC.CollectionCount(2)-newGen2},TimingClaim="Descriptive background owner-thread cost; separate from viewport FPS or human input latency"}));
            Check(form.BatchReviewRecipeCaptures-captures==iterations&&scalar<oldCharacters/100,"Repeated large-recipe dependency checks serialize under one percent of previous full recipe JSON while checking every field");
            var input=asset.TransitionFit.Inputs[0];input.Snapshot=new string(frozen.ToCharArray());SetField(form,"_batchReviewDirty",true);Call(form,"SyncBatchReview");
            Check(ReviewApproved(row),"An equal frozen snapshot in a different string object retains current approval");
            input.Snapshot=frozen.Substring(0,frozen.Length-1)+"D";SetField(form,"_batchReviewDirty",true);Call(form,"SyncBatchReview");
            Check(!ReviewApproved(row),"One changed character at the end of a large frozen snapshot invalidates approval");
            Check(hashes==FighterProject.DiagnosticFileHashes&&opens==AnimationLibrary.DiagnosticOpens&&bakes==BrawlRetargeter.DiagnosticBakes,"Large metadata observation performs no file hashing, source opening or retarget bake");
        }
        finally{asset.Retarget.EngineReceipt=oldEngine;asset.TransitionFit=oldFit;SetField(row,"Approved",false);SetField(form,"_batchReviewDirty",true);Call(form,"SyncBatchReview");}
        Check(ReferenceEquals(editor.SelectedCHR0,clip)&&ReferenceEquals(editor.SelectedBone,bone)&&editor.CurrentFrame==frame&&editor._undoSaves.Count==undo&&editor._redoSaves.Count==redo&&SetNativeContent(clip)==content,"Large procedural observer tests leave native content, selection, frame and Undo unchanged");
    }
    static async Task ReviewBodyEdit(ModelForm form,float amount)
    {
        var editor=form.FighterEditor;editor.SetFrame(1);await SimpleControlsReady(form);
        var setup=await Await<ControlReply>(Call(form,"LoadControls"));editor.SetFrame(3);
        editor.SelectedBone=((MDL0Node)editor.TargetModel)._linker.BoneCache.Single(b=>b.Name==setup.BoneNames[setup.Roles.Single(r=>r.Role=="Hips").Bone.Value]);
        var field=editor.CHR0Editor._transBoxes[3];field.Text=(editor.SelectedBone.FrameState._rotate._x+amount).ToString();Call(field,"Apply");await SimpleControlsReady(form);
    }
    static async Task ReviewBatchCases(ModelForm form,FighterAsset[] assets,string output)
    {
        var editor=form.FighterEditor;
        await Wait(()=>((Panel)Field(form,"_batchReviewPane"))?.Visible==true);await SimpleControlsReady(form);
        var target=editor.TargetModel;var viewport=editor.ModelPanel;
        Check(ReviewRows(form).Length==2,"Actual reviewed batch import opens a session queue containing both exact target revisions");
        var rows=ReviewRows(form);var clips=rows.Select(r=>(CHR0Node)Field(r,"Clip")).ToArray();
        Check(rows.Select(r=>(string)Field(r,"Key")).Distinct().Count()==2&&!rows.Any(ReviewApproved),"Review identities are distinct and batch loss review never grants visual export approval");
        var library=(AnimationLibrary)Field(form,"_library");
        foreach(var asset in assets)
        {
            using(var source=library.Open(asset.Retarget.Library.ClipId))
            using(var fit=asset.Retarget.Timing.Resample(source.Animation))
            {
                string fitPath=Path.Combine(output,"reference-fit-"+asset.Id+".chr0"),resultPath=Path.Combine(output,"reference-result-"+asset.Id+".chr0");
                // Match the actual worker's immutable precise source snapshot.
                // A bare legacy Export can alter the resampled source curves
                // before this independent solver replay even begins.
                using(var frozenFit=NativeAnimationOptimization.CopySource(fit))frozenFit.ExportChecked(fitPath);
                using(var decodedFit=NodeFactory.FromFile(null,fitPath) as CHR0Node)
                {
                    Check(decodedFit!=null&&NativeAnimationOptimization.SourceContentKey(decodedFit)==NativeAnimationOptimization.SourceContentKey(fit),
                        "Independent batch replay freezes every original resampled source coefficient exactly before the unchanged Next solver");
                    using(var expected=BrawlRetargeter.Bake(source.Model,(MDL0Node)target,decodedFit,groundMode:form.FighterProject.GroundMode))expected.Animation.ExportChecked(resultPath);
                }
                using(var expected=NodeFactory.FromFile(null,resultPath) as CHR0Node)
                {
                    string difference=NativeDifference(expected,(CHR0Node)Call(form,"FighterRoot",asset));File.WriteAllText(resultPath+".comparison.txt",difference??"Exact original Next native match");
                    Check(difference==null,"Batch review retains every original Next channel and frozen timing result: "+difference);
                }
            }
        }
        string baselineA=LifecycleContent(clips[0]),baselineB=LifecycleContent(clips[1]);
        ReviewLargeRecipeCases(form,assets[0],rows[0],output);
        Call(form,"MarkBatchReview");Check(ReviewApproved(rows[0])&&!ReviewApproved(rows[1]),"Mark reviewed approves only the exact selected result");
        int hashes=FighterProject.DiagnosticFileHashes,opens=AnimationLibrary.DiagnosticOpens,bakes=BrawlRetargeter.DiagnosticBakes;
        editor.SetFrame(0);editor.SetFrame(2);editor.SetFrame(3);Call(form,"UpdateFighterStatus");editor.PlayAnim();await Task.Delay(300);editor.StopAnim();editor.SetFrame(3);
        Check(ReviewApproved(rows[0])&&FighterProject.DiagnosticFileHashes==hashes&&AnimationLibrary.DiagnosticOpens==opens&&BrawlRetargeter.DiagnosticBakes==bakes,"Scrubbing/navigation retain review approval without hashing, opening or baking");
        await ReviewBodyEdit(form,2);string editedA=LifecycleContent(clips[0]);
        Check(!ReviewApproved(rows[0])&&editedA!=baselineA,"A real native body edit invalidates its previously approved clip");
        Call(form,"MarkBatchReview");Call(form,"MoveBatchReview",1);await SimpleControlsReady(form);
        Check(ReferenceEquals(editor.SelectedCHR0,clips[1])&&ReferenceEquals(viewport,editor.ModelPanel)&&ReferenceEquals(target,editor.TargetModel),"Next selects the exact native result while retaining the complete editor and target");
        await ReviewBodyEdit(form,4);string editedB=LifecycleContent(clips[1]);Call(form,"MarkBatchReview");
        Check(rows.All(ReviewApproved),"Independent native edits can each be reviewed against the same authentic target");
        editor.Undo();await SimpleControlsReady(form);
        Check(LifecycleContent(clips[1])==baselineB&&!ReviewApproved(rows[1])&&ReviewApproved(rows[0]),"Native Undo restores clip B exactly and invalidates only B approval");
        editor.Undo();await SimpleControlsReady(form);
        Check(ReferenceEquals(editor.SelectedCHR0,clips[0])&&LifecycleContent(clips[0])==baselineA&&!rows.Any(ReviewApproved),"Cross-clip native Undo selects clip A and its review row without another Undo stack");
        Check(ReferenceEquals(SimpleControl<ListBox>(form,"reviewResults").SelectedItem,rows[0]),"Review current row follows the native Undo-selected animation");
        editor.Redo();await SimpleControlsReady(form);editor.Redo();await SimpleControlsReady(form);
        Check(LifecycleContent(clips[0])==editedA&&LifecycleContent(clips[1])==editedB&&ReferenceEquals(editor.SelectedCHR0,clips[1]),"Native cross-clip Redo restores both exact authored curves and result selection");
        clips[1].Name=clips[0].Name;Call(form,"SyncBatchReview");
        Check(rows.Select(r=>(string)Field(r,"Key")).Distinct().Count()==2,"Duplicate native clip names retain separate exact review identities");
        Call(form,"OpenLinkedComparison");Check(Field(form,"_comparisonLease")!=null&&!((Panel)Field(form,"_batchReviewPane")).Visible,"Compare source hides the review drawer and resolves the selected result's recorded source");
        Call(form,"OpenBatchReview");Check(ReviewRows(form).Length==2&&Field(form,"_comparisonLease")!=null,"Recovering Review results retains rows and the prepared comparison lease");
        string contentA=LifecycleContent(clips[0]),contentB=LifecycleContent(clips[1]);int undo=editor._undoSaves.Count;
        foreach(int text in new[]{100,175})foreach(int width in new[]{1050,1700})
        {
            form.ClientSize=new Size(width,950);Call(form,"SetWorkspaceTextSize",text);Call(form,"OpenBatchReview");
            var list=SimpleControl<ListBox>(form,"reviewResults");
            Check(list.Visible&&list.Width>160&&!((Control)Field(form,"_simpleWorkspaceTask")).Visible,"Review drawer owns visible task space at "+width+"/"+text);
            var pane=(Panel)Field(form,"_batchReviewPane");var body=(Panel)Field(form,"_simpleWorkspaceBody");
            Check(ReferenceEquals(body.GetChildAtPoint(new Point(pane.Left+10,pane.Top+45)),pane),"Review drawer is the actual frontmost native control at "+width+"/"+text);
            Check(WorkspaceKey(form,Keys.F6)&&ReferenceEquals(viewport,editor.ModelPanel),"F6 handles the review region without replacing the native viewport");
            await CaptureSimpleWorkspace(form,Path.Combine(output,"batch-review-"+width+"-"+text+".png"));
        }
        Call(form,"RestoreWorkspaceFocus",SimpleControl<Button>(form,"reviewMark"));
        Check(SimpleControl<Button>(form,"reviewMark").Focused&&InClient(SimpleControl<Button>(form,"reviewMark"),(Panel)Field(form,"_batchReviewPane")),"Mark reviewed remains focusable and scrolls fully into view at 175% text");
        await CaptureSimpleWorkspace(form,Path.Combine(output,"batch-review-focused-175.png"));
        Check(LifecycleContent(clips[0])==contentA&&LifecycleContent(clips[1])==contentB&&editor._undoSaves.Count==undo,"All review layout/focus cycles preserve exact authored clips and native Undo");
        Call(form,"OpenBatchReview");Check(WorkspaceKey(form,Keys.Escape)&&!((Panel)Field(form,"_batchReviewPane")).Visible,"Escape inside review closes only the drawer");
        Call(form,"OpenBatchReview");Call(form,"ToggleSimpleWorkspaceFocus");Check(!((Panel)Field(form,"_batchReviewPane")).Visible&&editor.ModelPanel.Focused,"Focus editor closes comparison/review and restores the original viewport");
        Call(form,"OpenBatchReview");Call(form,"RemoveBatchReview");
        Check(ReviewRows(form).Length==1&&LifecycleContent(clips[1])==contentB&&form.FighterProject.Animations.Contains(assets[1]),"Remove from list never removes the native project animation");
        Call(form,"AddSelectedBatchReviewClip");Call(form,"MarkBatchReview");
        var current=ReviewRows(form).Single(r=>ReferenceEquals(Field(r,"Clip"),editor.SelectedCHR0));
        assets[1].Retarget.Timing.Frames++;
        bool stale=false;try{Call(form,"ValidateBatchReviewRows");}catch(System.Reflection.TargetInvocationException e){stale=e.InnerException is InvalidOperationException;}
        Check(stale&&!ReviewApproved(current),"Changed recorded recipe invalidates exact review approval before export");assets[1].Retarget.Timing.Frames--;
        Call(form,"MarkBatchReview");
        string saved=Path.Combine(output,"review-session.fpstudio");form.SaveFighterProject(saved);
        Check(ReviewApproved(current),"Saving unchanged exact native content retains its session approval while rebuilding watched tracks");
        using(var fresh=FighterProject.Open(saved))using(var reopened=new ModelForm())
        {
            reopened.InitializeFighterProject(fresh);reopened.Show();await Task.Delay(150);await SimpleControlsReady(reopened);Call(reopened,"OpenBatchReview");
            Check(ReviewRows(reopened).Length==0,"Fresh project reopen retains native animations but no session approval or review list");
            Call(reopened,"AddSelectedBatchReviewClip");Check(ReviewRows(reopened).Length==1&&!ReviewRows(reopened).Any(ReviewApproved),"Reopened project-owned CHR0 joins review without a donor or inherited approval");
            Check(LifecycleContent(reopened.FighterEditor.SelectedCHR0)==contentB,"Session-only review adds no project schema or curve changes at reopen");
            await CaptureSimpleWorkspace(reopened,Path.Combine(output,"batch-review-reopened.png"));
            SimpleDisposeForm(reopened);
        }
    }

    static async Task ReviewBatchFailureCases(ModelForm form,FighterAsset[] assets,string output,string ownedMotion)
    {
        int before=form.FighterProject.Animations.Count;
        foreach(string mode in new[]{"mixed","cancel","stale-source"})
        {
            Exception failure=null;bool finished=false;
            form.LibraryBatchReady=async window=>
            {
                byte[] original=null;
                try
                {
                    var grid=SimpleControl<DataGridView>(window,"batchClips");
                    foreach(DataGridViewRow row in grid.Rows)
                    {row.Cells["Use"].Value=true;row.Cells["Start"].Value=2;row.Cells["End"].Value=5;row.Cells["Frames"].Value=7;}
                    if(mode=="mixed")grid.Rows[0].Cells["Start"].Value=-1;
                    var prepare=SimpleControl<Button>(window,"prepareBatch");
                    if(mode=="cancel")
                    {
                        using(OnRetargetReady(form,dialog=>dialog.Controls.OfType<Button>().Single().PerformClick()))prepare.PerformClick();
                        Check(!SimpleControl<Button>(window,"applyBatch").Enabled&&form.FighterProject.Animations.Count==before,"Actual batch cancellation adds no native revision or approval");
                    }
                    else
                    {
                        prepare.PerformClick();
                        if(mode=="mixed")Check(Convert.ToString(grid.Rows[0].Cells["Result"].Value).StartsWith("Failed:")&&Convert.ToString(grid.Rows[1].Cells["Result"].Value).StartsWith("Review:"),"A failed row remains excluded while another source prepares successfully");
                        else {original=File.ReadAllBytes(ownedMotion);var changed=(byte[])original.Clone();changed[changed.Length-1]^=1;File.WriteAllBytes(ownedMotion,changed);}
                        SimpleControl<CheckBox>(window,"reviewBatch").Checked=true;SimpleControl<Button>(window,"applyBatch").PerformClick();
                        Check(form.FighterProject.Animations.Count==before+(mode=="mixed"?1:0),mode=="mixed"?"Explicit loss review imports only the successful batch row":"Changed original source bytes reject batch Apply without partial project changes");
                        if(mode=="mixed")await Wait(()=>ReviewRows(form).Length==3);
                        if(mode=="stale-source")await CaptureWindow(window,Path.Combine(output,"batch-failure-review-175.png"));
                    }
                }
                catch(Exception error){failure=error;}
                finally{if(original!=null)File.WriteAllBytes(ownedMotion,original);window.Close();finished=true;}
            };
            Call(form,"ShowLibraryBatch");await Wait(()=>finished);if(failure!=null)throw failure;
            if(mode=="mixed")
            {
                Call(form,"UndoFighterImport");await SimpleControlsReady(form);
                Check(form.FighterProject.Animations.Count==before&&ReviewRows(form).Length==2,"One import Undo removes only the successful added batch and its session row");
            }
        }
        string native=Path.Combine(output,"native-without-donor.chr0");File.Copy(assets[0].WorkingPath,native);
        Call(form,"ImportFighterAnimations",new[]{new[]{native}});await SimpleControlsReady(form);Call(form,"OpenBatchReview");Call(form,"AddSelectedBatchReviewClip");Call(form,"MarkBatchReview");
        Check(ReviewRows(form).Any(r=>ReviewApproved(r)&&ReferenceEquals(Field(r,"Clip"),form.FighterEditor.SelectedCHR0)),"Imported native animation is reviewable without donor association or retargeting");
        Call(form,"OpenLinkedComparison");Check(Field(form,"_comparisonLease")==null&&SimpleControl<Label>(form,"workspaceComparisonStatus").Text.Contains("unavailable"),"Donor-free native review reports unavailable comparison while keeping target editing");
        Call(form,"CloseLinkedComparison");Call(form,"UndoFighterImport");await SimpleControlsReady(form);
        Check(form.FighterProject.Animations.Count==before&&ReviewRows(form).Length==2,"Undoing a donor-free import prunes only its review row");
        form.LibraryBatchReady=null;
    }
}
