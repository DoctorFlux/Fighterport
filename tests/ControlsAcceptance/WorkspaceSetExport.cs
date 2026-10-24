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
    static void VerifySetFiles(ModelForm form,string folder,int count)
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
                Check(decoded!=null&&SetNativeContent(live)==SetNativeContent(decoded)&&Convert.ToInt32(entry["Frames"])==live.FrameCount,"Independent set oracle verifies exact metadata/order/flags/indices/values/tangents/timing/loop: "+live.Name);
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
        await ApproveSet(form);editor.SetFrame(3);var selected=editor.SelectedCHR0;var bone=editor.SelectedBone;int undo=editor._undoSaves.Count;
        var expected=clips.Select(SetNativeContent).ToArray();var children=clips.Select(c=>c.Children.ToArray()).ToArray();
        object first=Call(form,"FreezeWorkspaceSet");string[] filenames=SetItems(first).Select(i=>(string)Field(i,"Filename")).ToArray();
        string destination=Path.Combine(output,"verified-unsaved-set");
        using((IDisposable)first)Check(await TrySetExport(form,first,destination)==null,"All approved unsaved batch clips publish in one directory commit");
        VerifySetFiles(form,destination,2);
        Check(clips.Select(SetNativeContent).SequenceEqual(expected)&&clips.Select((c,i)=>c.Children.SequenceEqual(children[i])).All(x=>x)&&ReferenceEquals(editor.SelectedCHR0,selected)&&ReferenceEquals(editor.SelectedBone,bone)&&editor.CurrentFrame==3&&editor._undoSaves.Count==undo,"Set export never selects/rebuilds clips and preserves exact live content, bone/frame and native Undo");
        Check(assets.All(a=>a.Retarget.Library.LastExportFormat=="CHR0"&&!string.IsNullOrEmpty(a.Retarget.Library.LastExportSha256)),"Application receipts appear only after the directory commit");
        Check(rows.All(ReviewApproved),"Export receipt metadata does not invalidate unchanged motion review");
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
        foreach(string mode in new[]{"existing-set","existing-empty","existing-file","cancel-first","cancel-write","cancel-publish","damaged","write-failure","staged-change","receipt-change","rename-failure","stale-clip","stale-target","stale-recipe","stale-list","filename-collision"})
        {
            await ApproveSet(form);editor.SetFrame(3);var live=editor.SelectedCHR0;int history=editor._undoSaves.Count;
            string path=mode=="existing-set"?destination:Path.Combine(output,"failure-"+mode);string staging=null;FileStream locked=null;var cts=new CancellationTokenSource();
            if(mode=="existing-empty")Directory.CreateDirectory(path);
            if(mode=="existing-file")File.WriteAllText(path,"keep original");
            string targetName=((MDL0Node)editor.TargetModel).Name;bool loop=clips[0].Loop;int frames=assets[0].Retarget.Timing.Frames;
            form.SetExportCandidateWritten=p=>
            {
                staging=Path.GetDirectoryName(p);
                if(mode=="cancel-write")cts.Cancel();
                if(mode=="write-failure")throw new IOException("Injected candidate write failure");
                if(mode=="damaged") { using(var candidate=NodeFactory.FromFile(null,p) as CHR0Node){candidate.Loop=!candidate.Loop;candidate.ExportChecked(p);} }
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
                if(mode=="cancel-first")cts.Cancel();
                if(mode=="filename-collision") { var items=SetItems(frozen);items[1].GetType().GetField("Filename",Private).SetValue(items[1],((string)Field(items[0],"Filename")).ToUpperInvariant()); }
                failure=await TrySetExport(form,frozen,path,cts.Token);
            }
            form.SetExportCandidateWritten=null;form.SetExportBeforePublish=null;locked?.Dispose();cts.Dispose();
            Check(mode.StartsWith("cancel-")?failure is OperationCanceledException:mode=="damaged"?failure is InvalidDataException:mode=="rename-failure"||mode=="write-failure"?failure is IOException:failure!=null,"Set publication rejects "+mode+" through the expected validation/cancellation/write boundary");
            if(mode=="existing-set")VerifySetFiles(form,destination,2);
            else Check(mode=="existing-empty"?Directory.Exists(path)&&Directory.GetFileSystemEntries(path).Length==0:mode=="existing-file"?File.ReadAllText(path)=="keep original":!Directory.Exists(path)&&!File.Exists(path),"Failed/cancelled set leaves destinations untouched: "+mode);
            // A locked Windows rename can delay cleanup; after releasing the
            // simulated external handle remove only this test's owned staging.
            if(mode=="rename-failure"&&staging!=null&&Directory.Exists(staging)) {foreach(var file in Directory.GetFiles(staging))File.Delete(file);Directory.Delete(staging);}
            Check(!Directory.GetDirectories(output,".fighterport-set-*").Any(),"No partial set remains after "+mode);
            if(mode=="stale-clip")clips[0].Loop=loop;
            if(mode=="stale-target")((MDL0Node)editor.TargetModel).Name=targetName;
            if(mode=="stale-recipe")assets[0].Retarget.Timing.Frames=frames;
            if(mode=="stale-list")Call(form,"AddSelectedBatchReviewClip");
            Check(clips.Select(SetNativeContent).SequenceEqual(expected)&&ReferenceEquals(editor.SelectedCHR0,live)&&editor.CurrentFrame==3&&editor._undoSaves.Count==history&&new JavaScriptSerializer().Serialize(assets.Select(a=>a.Retarget.Library).ToArray())==receiptState,"Rejected export preserves live content/selection/frame/Undo and prior receipts: "+mode);
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
        foreach(int text in new[]{100,200})foreach(int width in new[]{1050,1700})
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
        await Wait(()=>finished);if(failure!=null)throw failure;VerifySetFiles(form,path,2);
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
                        CaptureDialog(dialog,output,"set-export-cancelled-200.png");SimpleControl<Button>(dialog,"setExportCancel").PerformClick();
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
