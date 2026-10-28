using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    // This oracle deliberately reads all native channels and metadata separately
    // from the application's snapshot/verification helpers.
    static string SetNativeContent(CHR0Node clip) => new JavaScriptSerializer { MaxJsonLength=16000000 }.Serialize(new {
        clip.Version, clip.OriginalPath, Users=clip.UserEntries.Cast<UserDataClass>().Select(u=>new{u.Name,u.DataType,u.Entries}).ToArray(),
        Order=clip.Children.Select(c=>c.Name).ToArray(), Curves=LifecycleContent(clip)
    });
    static object[] SetItems(object frozen) => ((System.Collections.IEnumerable)Field(frozen,"Items")).Cast<object>().ToArray();
    static async Task ApproveSet(ModelForm form)
    {
        foreach(var row in ReviewRows(form).Where(r=>!ReviewApproved(r))) { Call(form,"SelectBatchReview",row);await SimpleControlsReady(form);Call(form,"MarkBatchReview"); }
    }
    static async Task<Exception> TrySetExport(ModelForm form,object frozen,string path,CancellationToken token=default(CancellationToken))
    {
        try { await (Task)Call(form,"ExportWorkspaceSet",frozen,path,token,null); return null; }
        catch(System.Reflection.TargetInvocationException e) { return e.InnerException; }
        catch(Exception e) { return e; }
    }
    static string SetTextHash(string text)
    {using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
    static void VerifyOptimizedSetMember(ModelForm form,CHR0Node live,CHR0Node decoded,System.Collections.Generic.Dictionary<string,object> receipt,string file,string reportPath,string targetHash,string[] operationStages)
    {
        var contentMethod=form.GetType().GetMethod("ExportContent",Private|System.Reflection.BindingFlags.Static);
        string master=(string)contentMethod.Invoke(null,new object[]{live}),candidate=(string)contentMethod.Invoke(null,new object[]{decoded});
        string Metadata(CHR0Node clip)=>new JavaScriptSerializer().Serialize(new{clip.Name,clip.Version,clip.FrameCount,clip.Loop,clip.OriginalPath,
            ScalingRule=NativeAnimationOptimization.SourceScalingRule(clip),Users=clip.UserEntries.Cast<UserDataClass>().Select(user=>new{user.Name,user.DataType,user.Entries}).ToArray()});
        var retained=decoded.Children.OfType<CHR0EntryNode>().ToArray();var retainedNames=retained.Select(entry=>entry.Name).ToArray();
        Check(Metadata(live)==Metadata(decoded)&&live.Children.Select(entry=>entry.Name).Where(retainedNames.Contains).SequenceEqual(retainedNames)&&retained.All(entry=>
        {
            var source=live.Children.OfType<CHR0EntryNode>().SingleOrDefault(item=>item.Name==entry.Name);
            return source!=null&&source.UseModelScale==entry.UseModelScale&&source.UseModelRotate==entry.UseModelRotate&&source.UseModelTranslate==entry.UseModelTranslate&&
                source.ScaleCompensateApply==entry.ScaleCompensateApply&&source.ScaleCompensateParent==entry.ScaleCompensateParent&&source.ClassicScaleOff==entry.ClassicScaleOff&&
                source.Keyframes.FrameLimit==entry.Keyframes.FrameLimit&&source.Keyframes.Loop==entry.Keyframes.Loop;
        }),"Optimized set retains complete native metadata, timing, semantic flags and surviving track order independently of key reduction");
        Check((string)receipt["MasterContentSha256"]==SetTextHash(master)&&(string)receipt["ExportContentSha256"]==SetTextHash(candidate),
            "Optimized set receipt independently identifies the unchanged full master and complete decoded candidate content/storage: "+live.Name);
        string policy=(string)receipt["EffectiveExportPolicy"];
        Check(policy.StartsWith("Exact",StringComparison.Ordinal)&&((string)receipt["RequestedExportPolicy"]).Contains("\"Policy\":0")&&
            Convert.ToInt64(receipt["PreparationMilliseconds"])>=0&&!string.IsNullOrWhiteSpace((string)receipt["ByteAccounting"]),
            "Actual set dialog reports its explicit Exact policy, native byte accounting and completed preparation: "+live.Name);
        string reference=reportPath+".master-reference.chr0";
        try
        {
            using(var copy=NativeAnimationOptimization.CopySource(live))copy.ExportChecked(reference);
            Check(new FileInfo(file).Length<=new FileInfo(reference).Length&&NativeChr0SizeReport.Read(file).AccountedBytes==new FileInfo(file).Length,
                "Actual optimized set member fits its independently encoded same-finished-master byte cap: "+live.Name);
        }
        finally{if(File.Exists(reference))File.Delete(reference);}
        // The original PAC need not retain the rebuilt storage of the finished
        // editable model (the stale-target matrix also changes/restores its name).
        // Resolve this member's actual preparation stage, using both published
        // identities rather than choosing a newest/arbitrary model snapshot.
        string candidateHash=(string)receipt["Sha256"];
        var models=operationStages.Select(stage=>new{Stage=stage,Model=Path.Combine(stage,"target.mdl0")}).Where(item=>
            File.Exists(item.Model)&&(File.GetAttributes(item.Stage)&FileAttributes.ReparsePoint)==0&&
            (File.GetAttributes(item.Model)&FileAttributes.ReparsePoint)==0&&FighterProject.HashFile(item.Model)==targetHash&&
            new[]{"verified-export-copy.chr0","finished-target-master.chr0"}.Select(name=>Path.Combine(item.Stage,name)).Any(path=>File.Exists(path)&&FighterProject.HashFile(path)==candidateHash)).ToArray();
        Check(models.Length==1,"A unique operation-owned target snapshot matches both the exact target receipt and this member's qualified bytes: "+live.Name);
        string modelPath=models.Single().Model;
        using(var beforeRoot=NodeFactory.FromFile(null,modelPath))using(var afterRoot=NodeFactory.FromFile(null,modelPath))
        {
            var target=(MDL0Node)form.FighterEditor.TargetModel;
            var before=Walk(beforeRoot).OfType<MDL0Node>().Single(model=>model.Name==target.Name);
            var after=Walk(afterRoot).OfType<MDL0Node>().Single(model=>model.Name==target.Name);before.Populate();after.Populate();
            Check((string)Call(form,"NativeControlHash",before,".mdl0")==targetHash&&
                (string)Call(form,"NativeControlHash",after,".mdl0")==targetHash,
                "Independent set geometry witnesses own exact frozen target model copies rather than posing the editable viewport");
            Check(live.Children.OfType<CHR0EntryNode>().Where(entry=>!retainedNames.Contains(entry.Name)).All(entry=>
                !before.AllBones.Any(bone=>bone.Name==entry.Name)||ReplayBindTrackExact(entry,before.AllBones.Single(bone=>bone.Name==entry.Name))),
                "Optimized set omits only absent-target tracks or independently proved native bind-pose tracks");
            OptimizationWithheldGeometry(before,after,live,decoded,reportPath);
        }
        Check((string)contentMethod.Invoke(null,new object[]{live})==master,"Independent set scalar/FK/full-skin verification leaves every live master coefficient and flag unchanged");
    }
    static void VerifySetFiles(ModelForm form,string folder,int count,bool optimized=false,string[] operationStages=null)
    {
        var json=new JavaScriptSerializer();var receipt=json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(Path.Combine(folder,"fighterport-set.json")));
        var clips=((System.Collections.IEnumerable)receipt["Clips"]).Cast<System.Collections.Generic.Dictionary<string,object>>().ToArray();
        Check(((string)receipt["TargetIdentity"]).StartsWith(form.FighterProject.Model.Id+"|@")&&((string)receipt["TargetSha256"]).Length==64,"Set receipt binds the exact target resource identity and verified content hash");
        Check(clips.Length==count&&Directory.GetFiles(folder).Length==count+1,"Committed directory contains the exact complete set and one receipt");
        Check(clips.Select(c=>(string)c["Filename"]).Distinct(StringComparer.OrdinalIgnoreCase).Count()==count,"Duplicate readable names have distinct stable identity filenames");
        foreach(var entry in clips)
        {
            var row=ReviewRows(form).Single(r=>(string)Field(r,"Key")== (string)entry["Identity"]);var live=(CHR0Node)Field(row,"Clip");string path=Path.Combine(folder,(string)entry["Filename"]);
            using(var decoded=NodeFactory.FromFile(null,path) as CHR0Node)
            {
                Check(decoded!=null&&Convert.ToInt32(entry["Frames"])==live.FrameCount,"Independent set oracle verifies exact resource/timing identity: "+live.Name);
                if(optimized)VerifyOptimizedSetMember(form,live,decoded,entry,path,Path.Combine(Path.GetDirectoryName(folder),Path.GetFileName(folder)+"-"+Path.GetFileNameWithoutExtension(path)+"-withheld-geometry.json"),(string)receipt["TargetSha256"],operationStages??new string[0]);
                else Check(SetNativeContent(live)==SetNativeContent(decoded),"Reference set oracle retains literal metadata/order/flags/indices/values/tangents/timing/loop: "+live.Name);
            }
            Check((string)entry["Sha256"]==FighterProject.HashFile(path),"Set receipt records the verified published hash and exact native identity");
        }
        string text=File.ReadAllText(Path.Combine(folder,"fighterport-set.json"));
        Check(!text.Contains(((string)Field(form.FighterProject,"_stageDirectory")).Replace("\\","\\\\"))&&!text.Contains("owned-source-")&&!text.Contains("SourcePath")&&!text.Contains("WorkingPath"),"Set receipt omits private source and working paths");
    }
    static async Task SetExportCases(ModelForm form,FighterAsset[] assets,string output)
    {
        var editor=form.FighterEditor;await SimpleControlsReady(form);await Wait(()=>ReviewRows(form).Length==2);
        var rows=ReviewRows(form);var clips=rows.Select(r=>(CHR0Node)Field(r,"Clip")).ToArray();
        bool unapproved=false;try{Call(form,"FreezeWorkspaceSet");}catch(System.Reflection.TargetInvocationException e){unapproved=e.InnerException is InvalidOperationException;}
        Check(unapproved,"A prepared batch cannot be exported without exact session review approval");
        clips[1].Name=clips[0].Name;
        Call(form,"SelectBatchReview",rows[0]);await SimpleControlsReady(form);string before=SetNativeContent(clips[0]);
        await ReviewBodyEdit(form,2);string edited=SetNativeContent(clips[0]);
        Check(before!=edited&&form.FighterProject.ManifestPath==null,"Real batch body edit remains unsaved before verified set export");
        await ApproveSet(form);editor.SetFrame(3);var target=editor.TargetModel;var selected=editor.SelectedCHR0;var bone=editor.SelectedBone;int undo=editor._undoSaves.Count,redo=editor._redoSaves.Count;
        var expected=clips.Select(SetNativeContent).ToArray();var children=clips.Select(c=>c.Children.ToArray()).ToArray();
        object first=Call(form,"FreezeWorkspaceSet");string[] filenames=SetItems(first).Select(i=>(string)Field(i,"Filename")).ToArray();
        string destination=Path.Combine(output,"verified-unsaved-set");
        using((IDisposable)first)
        {
            Check(await TrySetExport(form,first,destination)==null,"All approved unsaved batch clips publish in one directory commit");
            await SimpleControlsReady(form);
            var member=SetItems(first)[0];string repeat=Path.Combine(output,"repeated-reviewed-retarget-copy.chr0");
            Call(form,"ExportWorkspaceSnapshot",Field(member,"Snapshot"),repeat);
            Check(FighterProject.HashFile(repeat)==FighterProject.HashFile(Path.Combine(destination,(string)Field(member,"Filename"))),
                "A frozen reviewed retarget copy remains valid after its successful export receipt changes and republishes identical bytes");
        }
        VerifySetFiles(form,destination,2);
        Check(clips.Select(SetNativeContent).SequenceEqual(expected)&&clips.Select((c,i)=>c.Children.SequenceEqual(children[i])).All(x=>x)&&ReferenceEquals(editor.SelectedCHR0,selected)&&ReferenceEquals(editor.SelectedBone,bone)&&editor.CurrentFrame==3&&editor._undoSaves.Count==undo,"Set export never selects/rebuilds clips and preserves exact live content, bone/frame and native Undo");
        Check(assets.All(a=>a.Retarget.Library.LastExportFormat=="CHR0"&&!string.IsNullOrEmpty(a.Retarget.Library.LastExportSha256)),"Application receipts appear only after the directory commit");
        Check(rows.All(ReviewApproved),"Export receipt metadata does not invalidate unchanged motion review");
        using(var optimized=(IDisposable)Call(form,"FreezeWorkspaceSet"))
        {
            var settings=new NativeExportOptimizationSettings();string optimizedDestination=Path.Combine(output,"verified-explicit-exact-set");
            await (Task)Call(form,"ExportOptimizedWorkspaceSet",optimized,optimizedDestination,CancellationToken.None,null,settings);
            await SimpleControlsReady(form);
            var json=new JavaScriptSerializer();var receipt=json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(Path.Combine(optimizedDestination,"fighterport-set.json")));
            var entries=((System.Collections.IEnumerable)receipt["Clips"]).Cast<System.Collections.Generic.Dictionary<string,object>>().ToArray();
            Check(entries.Length==clips.Length&&Directory.GetFiles(optimizedDestination).Length==clips.Length+1,"Explicit optimized-set route commits the complete candidate set and one receipt");
            var contentMethod=form.GetType().GetMethod("ExportContent",Private|System.Reflection.BindingFlags.Static);
            string HashText(string text)
            {
                using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();
            }
            foreach(var member in SetItems(optimized))
            {
                var snapshot=Field(member,"Snapshot");var candidate=(CHR0Node)Field(snapshot,"Candidate");var master=(CHR0Node)Field(snapshot,"Master");
                string key=(string)Field(member,"Key"),path=Path.Combine(optimizedDestination,(string)Field(member,"Filename"));var entry=entries.Single(item=>(string)item["Identity"]==key);
                using(var decoded=NodeFactory.FromFile(null,path) as CHR0Node)
                    Check(decoded!=null&&NativeAnimationOptimization.SourceContentKey(decoded)==NativeAnimationOptimization.SourceContentKey(candidate)&&NativeAnimationOptimization.StorageKey(decoded)==NativeAnimationOptimization.StorageKey(candidate),
                        "Optimized set independently reopens every prepared native key/metadata/storage identity: "+key);
                var bytes=NativeChr0SizeReport.Read(path);
                Check(bytes.FileBytes<=(long)Field(snapshot,"MasterBytes")&&bytes.AccountedBytes==bytes.FileBytes&&FighterProject.HashFile(path)==(string)Field(snapshot,"PreparedEncodedSha256")&&FighterProject.HashFile(path)==(string)entry["Sha256"],
                    "Optimized set publishes exactly qualified bytes within each same-master size cap: "+key);
                Check((string)entry["RequestedExportPolicy"]==settings.StableKey&&(string)entry["EffectiveExportPolicy"]==(string)Field(snapshot,"EffectiveExportPolicy")&&((string)entry["EffectiveExportPolicy"]).StartsWith("Exact",StringComparison.Ordinal)&&
                    !string.IsNullOrWhiteSpace((string)entry["ByteAccounting"])&&Convert.ToInt64(entry["PreparationMilliseconds"])==(long)Field(snapshot,"PreparationMilliseconds"),
                    "Optimized set receipt truthfully records explicit/effective policy, native byte accounting and preparation wall time: "+key);
                Check((string)entry["MasterContentSha256"]==HashText((string)contentMethod.Invoke(null,new object[]{master}))&&(string)entry["ExportContentSha256"]==HashText((string)Field(snapshot,"ExpectedContent")),
                    "Optimized set receipt keeps its finished-master and prepared-copy identities separate: "+key);
            }
            Check(clips.Select(SetNativeContent).SequenceEqual(expected)&&clips.Select((clip,index)=>clip.Children.SequenceEqual(children[index])).All(same=>same)&&rows.All(ReviewApproved)&&
                ReferenceEquals(editor.TargetModel,target)&&ReferenceEquals(editor.SelectedCHR0,selected)&&ReferenceEquals(editor.SelectedBone,bone)&&editor.CurrentFrame==3&&editor._undoSaves.Count==undo&&editor._redoSaves.Count==redo,
                "Explicit optimized-set publication preserves both full-quality masters, native membership, approvals, target selection, bone, playhead and Undo");
        }
        editor.Undo();await SimpleControlsReady(form);Check(SetNativeContent(clips[0])==before&&!ReviewApproved(rows[0]),"Native body Undo survives set export and invalidates the affected approval");
        editor.Redo();await SimpleControlsReady(form);Check(SetNativeContent(clips[0])==edited,"Native body Redo survives set export exactly");
        await ApproveSet(form);
        using(var frozen=(IDisposable)Call(form,"FreezeWorkspaceSet"))
            Check(SetItems(frozen).Select(i=>(string)Field(i,"Filename")).SequenceEqual(filenames),"Filename identity suffixes remain stable across repeated operations");
        using(var frozen=(IDisposable)Call(form,"FreezeWorkspaceSet"))
        {
            Call(form,"SelectBatchReview",rows[0]);await SimpleControlsReady(form);editor.SetFrame(4);
            Check(await TrySetExport(form,frozen,Path.Combine(output,"navigated-frozen-set"))==null&&ReferenceEquals(editor.SelectedCHR0,clips[0])&&editor.CurrentFrame==4,"Frozen explicit-clip export tolerates ordinary clip navigation/scrubbing without selecting clips itself");
        }

        string receiptState=new JavaScriptSerializer().Serialize(assets.Select(a=>a.Retarget.Library).ToArray());
        foreach(string mode in new[]{"existing-set","existing-empty","existing-file","cancel-first","cancel-write","cancel-publish","damaged","appended-growth","write-failure","staged-change","receipt-change","rename-failure","stale-clip","stale-target","stale-recipe","stale-list","filename-collision"})
        {
            await ApproveSet(form);editor.SetFrame(3);var live=editor.SelectedCHR0;int history=editor._undoSaves.Count;
            string path=mode=="existing-set"?destination:Path.Combine(output,"failure-"+mode);string staging=null;FileStream locked=null;var cts=new CancellationTokenSource();long growthBytes=0;
            if(mode=="existing-empty")Directory.CreateDirectory(path);
            if(mode=="existing-file")File.WriteAllText(path,"keep original");
            string targetName=((MDL0Node)editor.TargetModel).Name;bool loop=clips[0].Loop;int frames=assets[0].Retarget.Timing.Frames;
            string targetBeforeInjectedChange=mode=="stale-target"?(string)Call(form,"NativeControlHash",editor.TargetModel,".mdl0"):null,targetAfterCommittedSave=null;
            string rigBeforeInjectedChange=mode=="stale-target"?NativeGeneratedPairRegistry.RigKey((MDL0Node)editor.TargetModel):null;
            if(mode=="stale-target")((MDL0Node)editor.TargetModel).Export(Path.Combine(output,"pre-injected-target.mdl0"));
            form.SetExportCandidateWritten=p=>
            {
                staging=Path.GetDirectoryName(p);
                if(mode=="cancel-write")cts.Cancel();
                if(mode=="write-failure")throw new IOException("Injected candidate write failure");
                if(mode=="damaged") { using(var candidate=NodeFactory.FromFile(null,p) as CHR0Node){candidate.Loop=!candidate.Loop;candidate.ExportChecked(p);} }
                if(mode=="appended-growth")using(var file=new FileStream(p,FileMode.Append,FileAccess.Write,FileShare.None))file.SetLength(file.Length+growthBytes);
            };
            form.SetExportBeforePublish=()=>
            {
                if(mode=="cancel-publish")cts.Cancel();
                if(mode=="staged-change")File.AppendAllText(Directory.GetFiles(staging,"*.chr0")[0],"changed after decode");
                if(mode=="receipt-change")File.AppendAllText(Path.Combine(staging,"fighterport-set.json"),"changed receipt");
                if(mode=="rename-failure")locked=new FileStream(Directory.GetFiles(staging,"*.chr0")[0],FileMode.Open,FileAccess.Read,FileShare.Read);
                if(mode=="stale-clip")clips[0].Loop=!loop;
                if(mode=="stale-target")((MDL0Node)editor.TargetModel).Name=targetName+" changed";
                if(mode=="stale-recipe")assets[0].Retarget.Timing.Frames++;
                if(mode=="stale-list")Call(form,"RemoveBatchReview");
            };
            Exception failure;
            using(var frozen=(IDisposable)Call(form,"FreezeWorkspaceSet"))
            {
                if(mode=="appended-growth")foreach(var item in SetItems(frozen))
                {
                    var snapshot=Field(item,"Snapshot");var master=(CHR0Node)Field(snapshot,"Master");string baseline=Path.Combine(output,"growth-baseline-"+Guid.NewGuid().ToString("N")+".chr0");
                    try{master.ExportChecked(baseline);long bytes=new FileInfo(baseline).Length;snapshot.GetType().GetField("MasterBytes",Private).SetValue(snapshot,bytes);growthBytes=Math.Max(growthBytes,bytes+1);}
                    finally{if(File.Exists(baseline))File.Delete(baseline);}
                }
                if(mode=="cancel-first")cts.Cancel();
                if(mode=="filename-collision") { var items=SetItems(frozen);items[1].GetType().GetField("Filename",Private).SetValue(items[1],((string)Field(items[0],"Filename")).ToUpperInvariant()); }
                failure=await TrySetExport(form,frozen,path,cts.Token);
            }
            form.SetExportCandidateWritten=null;form.SetExportBeforePublish=null;locked?.Dispose();cts.Dispose();
            Check(mode.StartsWith("cancel-")?failure is OperationCanceledException:mode=="damaged"||mode=="appended-growth"?failure is InvalidDataException:mode=="rename-failure"||mode=="write-failure"?failure is IOException:failure!=null,"Set publication rejects "+mode+" through the expected validation/cancellation/write boundary");
            if(mode=="existing-set")VerifySetFiles(form,destination,2);
            else Check(mode=="existing-empty"?Directory.Exists(path)&&Directory.GetFileSystemEntries(path).Length==0:mode=="existing-file"?File.ReadAllText(path)=="keep original":!Directory.Exists(path)&&!File.Exists(path),"Failed/cancelled set leaves destinations untouched: "+mode);
            // A locked Windows rename can delay cleanup; after releasing the
            // simulated external handle remove only this test's owned staging.
            if(mode=="rename-failure"&&staging!=null&&Directory.Exists(staging)) {foreach(var file in Directory.GetFiles(staging))File.Delete(file);Directory.Delete(staging);}
            Check(!Directory.GetDirectories(output,".fighterport-set-*").Any(),"No partial set remains after "+mode);
            if(mode=="stale-clip")clips[0].Loop=loop;
            if(mode=="stale-target")
            {
                var model=(MDL0Node)editor.TargetModel;model.Name=targetName;
                Check(model.IsDirty,"Restoring the injected target name remains a pending native model edit until an explicit ordinary Save");
                form.SaveFighterProject(Path.Combine(output,"restored-target-after-stale-check.fpstudio"));
                string afterHash=model.IsDirty?null:(string)Call(form,"NativeControlHash",model,".mdl0");
                if(!model.IsDirty)model.Export(Path.Combine(output,"post-save-restored-target.mdl0"));
                File.WriteAllText(Path.Combine(output,"stale-target-save-diagnostic.json"),new JavaScriptSerializer().Serialize(new{
                    DirtyAfterSave=model.IsDirty,SameLiveTarget=ReferenceEquals(model,editor.TargetModel),model.Name,BeforeHash=targetBeforeInjectedChange,AfterHash=afterHash,
                    RigBeforeSave=rigBeforeInjectedChange,RigAfterSave=NativeGeneratedPairRegistry.RigKey(model),BeforeFile="pre-injected-target.mdl0",AfterFile="post-save-restored-target.mdl0"}));
                Check(!model.IsDirty&&ReferenceEquals(model,editor.TargetModel)&&model.Name==targetName&&NativeGeneratedPairRegistry.RigKey(model)==rigBeforeInjectedChange,
                    "Explicit ordinary Save commits the same restored live target identity and exact rig/bind/inverse data before later independent export cases");
                // A deliberate native model Save may rebuild draw ordering and
                // derived HasGeometry flags. Its complete byte/field diagnosis
                // is retained separately; subsequent exports bind this actual
                // freshly committed model, never the earlier raw layout.
                targetAfterCommittedSave=afterHash;
                Check(afterHash!=null&&afterHash.Length==64&&FighterProject.HashFile(Path.Combine(output,"post-save-restored-target.mdl0"))==afterHash&&!rows.Any(ReviewApproved),
                    "The freshly committed exact target snapshot invalidates earlier approvals and becomes the new independent export dependency");
            }
            if(mode=="stale-recipe")assets[0].Retarget.Timing.Frames=frames;
            if(mode=="stale-list")Call(form,"AddSelectedBatchReviewClip");
            Check(clips.Select(SetNativeContent).SequenceEqual(expected)&&ReferenceEquals(editor.SelectedCHR0,live)&&editor.CurrentFrame==3&&editor._undoSaves.Count==history&&new JavaScriptSerializer().Serialize(assets.Select(a=>a.Retarget.Library).ToArray())==receiptState,"Rejected export preserves live content/selection/frame/Undo and prior receipts: "+mode);
            if(mode=="stale-target")
            {
                await ApproveSet(form);
                Check(rows.All(row=>ReviewApproved(row)&&(string)Field(row,"TargetContent")==targetAfterCommittedSave),
                    "Explicit review binds every retained animation to the exact newly committed target model hash");
            }
        }
        await ApproveSet(form);
        using(var frozen=(IDisposable)Call(form,"FreezeWorkspaceSet"))
        {
            bool protectedPath=false;try{Call(form,"ValidateSetDestination",frozen,Path.Combine(((string)Field(form.FighterProject,"_stageDirectory")),"set"));}catch(System.Reflection.TargetInvocationException e){protectedPath=e.InnerException is InvalidOperationException;}
            Check(protectedPath,"Reviewed set publication protects the project working tree");
        }
        Call(form,"SetSimpleWorkspaceStage",3);
        Check(SimpleControl<Button>(form,"workspaceExportSetAction").Visible&&SimpleControl<Button>(form,"workspaceExportSetAction").Enabled,"Export stage exposes Export reviewed set through the existing workspace action routing");
        await CaptureSimpleWorkspace(form,Path.Combine(output,"set-export-stage.png"));
        foreach(int text in new[]{100,175})foreach(int width in new[]{1050,1700})
        {
            form.ClientSize=new Size(width,950);Call(form,"SetWorkspaceTextSize",text);await ApproveSet(form);
            using(FlowModal("setExportPath",dialog=>
            {
                Check(SimpleControl<Label>(dialog,"setExportSummary").Text.Contains("2 reviewed animations")&&SimpleControl<TextBox>(dialog,"setExportClips").Text.Contains(filenames[0])&&SimpleControl<TextBox>(dialog,"setExportClips").Text.Contains(form.FighterProject.Model.Id+"|@"),"Set review identifies project, exact target resource/hash, count, identities and filenames at "+width+"/"+text);
                SimpleControl<TextBox>(dialog,"setExportPath").Text=destination;
                Check(!SimpleControl<Button>(dialog,"setExportAccept").Enabled,"Existing set folder is rejected inline without overwrite at "+width+"/"+text);
                SimpleControl<TextBox>(dialog,"setExportPath").Text=Path.Combine(output,"dialog-unused-"+width+"-"+text);
                Check(SimpleControl<Button>(dialog,"setExportAccept").Enabled,"New output folder enables verified set publication at "+width+"/"+text);
                CaptureDialog(dialog,output,"set-export-review-"+width+"-"+text+".png");
                var button=SimpleControl<Button>(dialog,"setExportAccept");button.Focus();Check(button.Focused&&InClient(button,dialog),"Set action has visible keyboard focus within its dialog");
                SimpleControl<Button>(dialog,"setExportCancel").PerformClick();
            }))SimpleControl<Button>(form,"workspaceExportSetAction").PerformClick();
        }
        await SetExportDialogCases(form,output);
        Check(clips.Select(SetNativeContent).SequenceEqual(expected),"All set review dialogs retain exact target channels");
        await SetExportProcedural(form,output);
        // The enclosing batch test then saves/reopens the actual Next results,
        // checks source immutability and undoes their single import transaction.
    }
    static async Task SetExportDialogCases(ModelForm form,string output)
    {
        await ApproveSet(form);Exception failure=null;bool finished=false;string path=Path.Combine(output,"dialog-published-set");
        string controlStage=(string)Field(form,"_controlStage");
        var previousStages=Directory.GetDirectories(controlStage,"export-optimization-*",SearchOption.TopDirectoryOnly);
        using(FlowModal("setExportPath",async dialog=>
        {
            try
            {
                SimpleControl<TextBox>(dialog,"setExportPath").Text=path;
                SimpleControl<Button>(dialog,"setExportAccept").PerformClick();
                await Wait(()=>dialog.DialogResult==DialogResult.OK||!dialog.Visible);
                Check(dialog.DialogResult==DialogResult.OK&&Directory.Exists(path),"Actual application-owned dialog publishes a verified set asynchronously");
            }
            catch(Exception e){failure=e;dialog.Close();}finally{finished=true;}
        }))Call(form,"ChooseWorkspaceSetExport");
        await Wait(()=>finished);if(failure!=null)throw failure;
        var operationStages=Directory.GetDirectories(controlStage,"export-optimization-*",SearchOption.TopDirectoryOnly).Except(previousStages,StringComparer.OrdinalIgnoreCase).ToArray();
        VerifySetFiles(form,path,2,true,operationStages);
        await ApproveSet(form);finished=false;path=Path.Combine(output,"dialog-cancelled-set");
        using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())
        {
            form.SetExportCandidateWritten=p=>{entered.Set();if(!release.Wait(10000))throw new IOException("Test cancellation release timed out");};
            try
            {
                using(FlowModal("setExportPath",async dialog=>
                {
                    try
                    {
                        SimpleControl<TextBox>(dialog,"setExportPath").Text=path;SimpleControl<Button>(dialog,"setExportAccept").PerformClick();await Wait(()=>entered.IsSet);
                        SimpleControl<Button>(dialog,"setExportCancel").PerformClick();
                        Check(dialog.Visible&&!(bool)SimpleControl<TextBox>(dialog,"setExportPath").Enabled,"Cancellation keeps frozen candidates owned until the worker actually stops");
                        release.Set();await Wait(()=>SimpleControl<TextBox>(dialog,"setExportPath").Enabled);
                        Check(!Directory.Exists(path)&&SimpleControl<Label>(dialog,"setExportStatus").Text.Contains("cancelled"),"Dialog cancellation publishes no folder and reports completion");
                        CaptureDialog(dialog,output,"set-export-cancelled-175.png");SimpleControl<Button>(dialog,"setExportCancel").PerformClick();
                    }
                    catch(Exception e){failure=e;release.Set();dialog.Close();}finally{finished=true;}
                }))Call(form,"ChooseWorkspaceSetExport");
                await Wait(()=>finished);if(failure!=null)throw failure;
            }
            finally{release.Set();form.SetExportCandidateWritten=null;}
        }
    }
    static async Task SetExportProcedural(ModelForm form,string output)
    {
        var fixture=new CHR0Node{Name="Duplicate:/ Animation",Version=5,FrameCount=19,Loop=true,OriginalPath="authored/original"};
        fixture.UserEntries.Add(new UserDataClass{Name="author",DataType=UserValueType.String,Entries=new[]{"procedural"}});
        fixture.UserEntries.Add(new UserDataClass{Name="number",DataType=UserValueType.Int,Entries=new[]{"42","-7"}});
        fixture.UserEntries.Add(new UserDataClass{Name="precise-float",DataType=UserValueType.Float,Entries=new[]{1.2345679f.ToString("R")}});
        var track=fixture.CreateEntry("Body");track.SetKeyframe(3,0,1.234567f,true,true)._tangent=.1234567f;track.SetKeyframe(3,13,-5.678912f,true,true)._tangent=-.7654321f;
        track.UseModelTranslate=true;track.ScaleCompensateApply=true;fixture.CreateEntry("AbsentChannels").UseModelRotate=true;
        var root=fixture.CreateEntry("TransN");root.SetKeyframe(6,0,2.345678f,true,true)._tangent=.2345678f;root.SetKeyframe(6,18,4.567891f,true,true)._tangent=-.3456789f;
        PreciseCHR0EntryNode.Promote(fixture);string input=Path.Combine(output,"procedural-native.chr0");fixture.ExportChecked(input);fixture.Dispose();
        Call(form,"ImportFighterAnimations",new[]{new[]{input}});await SimpleControlsReady(form);var procedural=form.FighterEditor.SelectedCHR0;Call(form,"AddSelectedBatchReviewClip");await ApproveSet(form);
        string destination=Path.Combine(output,"metadata-sparse-root-set");
        using(var frozen=(IDisposable)Call(form,"FreezeWorkspaceSet"))Check(await TrySetExport(form,frozen,destination)==null,"Donor-free native clip joins reviewed Next results in a verified set before Save");
        VerifySetFiles(form,destination,3);
        string file=Directory.GetFiles(destination,"*.chr0").Single(p=>Path.GetFileName(p).StartsWith("Duplicate__ Animation-"));
        using(var decoded=NodeFactory.FromFile(null,file) as CHR0Node)
            Check(decoded.Version==5&&decoded.OriginalPath=="authored/original"&&decoded.UserEntries.Count==3&&decoded.UserEntries[0].Entries.Single()=="procedural"&&decoded.UserEntries[1].Entries.SequenceEqual(new[]{"42","-7"})&&float.Parse(decoded.UserEntries[2].Entries.Single())==1.2345679f&&decoded.Children.Count==3,"Version/path/string/int/float metadata, sparse/absent tracks and authored protected controls survive exact set export");
        Call(form,"SelectBatchReview",ReviewRows(form).Single(r=>ReferenceEquals(Field(r,"Clip"),procedural)));await SimpleControlsReady(form);Call(form,"RemoveBatchReview");Call(form,"UndoFighterImport");await SimpleControlsReady(form);
        Check(ReviewRows(form).Length==2,"Removing and undoing the procedural import leaves the original reviewed batch intact");
    }
}
