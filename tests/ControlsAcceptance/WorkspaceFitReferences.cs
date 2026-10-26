using BrawlCrate;
using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static bool FitHasPose(MDL0Node model)=>model.AllBones.Any(b=>Enumerable.Range(0,9).Any(c=>Math.Abs(b.FrameState[c]-b.BindState[c])>.0001f));
    static void FitReceiptStorageChecks(string saved,string id,string output)
    {
        void Reject(Action<FighterAsset> edit,string name)
        {
            using(var project=FighterProject.Open(saved))
            {
                edit(project.Animations.Single(a=>a.Id==id));string destination=Path.Combine(output,"rejected-fit-"+name+".fpstudio");bool rejected=false;
                try{project.Save(destination);}catch(InvalidDataException){rejected=true;}
                Check(rejected&&!File.Exists(destination),"Bounded per-clip fitting metadata rejects "+name+" before publishing a project");
            }
        }
        Reject(a=>a.ClipTransitionFits.Add(a.ClipTransitionFits[0]),"duplicate-native-identity");
        Reject(a=>a.ClipTransitionFits[0].Entry="root/01","noncanonical-identity");
        Reject(a=>a.ClipTransitionFits[0].Fit.Inputs.Single(i=>i.Role=="source").AssetId=Guid.NewGuid().ToString("N"),"foreign-owner");
        Reject(a=>a.ClipTransitionFits[0].Fit=null,"missing-receipt");
        Reject(a=>{var value=a.ClipTransitionFits[0];while(a.ClipTransitionFits.Count<=4096)a.ClipTransitionFits.Add(value);},"excessive-record-count");
        Reject(a=>{
            var original=a.ClipTransitionFits[0].Fit;long size=original.Inputs.Sum(i=>(long)i.Snapshot.Length),total=a.TransitionRecipes().Sum(f=>f.Inputs.Sum(i=>(long)i.Snapshot.Length));
            int index=0;while(total<=32L*1024*1024&&a.ClipTransitionFits.Count<4096)
            {
                string entry="root/900000/"+(index++);var copy=new NativeTransitionFit{Algorithm=original.Algorithm,ModelSha256=original.ModelSha256,Options=original.Options,Receipt=original.Receipt,InitialResultSha256=original.InitialResultSha256,Inputs=original.Inputs.Select(i=>new TransitionNativeInput{Role=i.Role,Name=i.Name,Samples=i.Samples,AssetId=i.AssetId,Entry=i.Role=="source"?a.Id+"|@"+entry:i.Entry,Sha256=i.Sha256,Snapshot=i.Snapshot}).ToArray()};
                a.ClipTransitionFits.Add(new NativeClipTransitionFit{Entry=entry,Fit=copy});total+=size;
            }
            if(total<=32L*1024*1024)throw new Exception("Fixture cannot reach the aggregate snapshot budget within its record bound.");
        },"aggregate-snapshot-budget");
    }
    static async Task WorkspaceFitReferences(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Fitting reference checks need the private desktop.");
        string folder=Path.Combine(output,"library");Directory.CreateDirectory(folder);File.Copy(args[Array.IndexOf(args,"--library-index")+1],Path.Combine(folder,"catalogue.xml"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",folder);Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_TEMP_ROOT",Path.Combine(output,"transient"));
        var library=new AnimationLibrary(folder);var item=library.Browse(new LibraryQuery{Character="mario",Text="AttackS3S",Channel="CHR0 skeletal"}).Single(i=>i.Clip.Name=="AttackS3S"&&i.Model?.Name=="FitMario00");
        using(var startup=MainForm.StartViewerApplication())
        {
            var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
            try
            {
                await SimpleControlsReady(form);
                using(var original=library.Open(item.Clip.Id))
                {
                    var project=ModelForm.PrepareLibraryTarget(original);
                    if(!MainForm.Instance.OpenLibraryFighterProject(project,out form)){project.Dispose();throw new Exception("Fixture target failed to open.");}
                }
                form.ClientSize=new Size(1700,950);await SimpleControlsReady(form);Call(form,"UseSharedSourceLibrary");await LibraryQuerySettled(form);
                var editor=form.FighterEditor;var source=editor.SelectedCHR0;var model=(MDL0Node)editor.TargetModel;editor.SetFrame(source.FrameCount);editor.SelectedBone=model.AllBones.Single(b=>b.Name=="HipN");await SimpleControlsReady(form);
                var viewerReference=NodeFactory.FromFile(null,args[1]);editor._openedFiles.Add(viewerReference);
                var owner=(FighterAsset)Call(form,"SelectedFighterAsset");string nativeEntry="root"+BrawlLibrarySource.Resources(source.RootNode).Single(p=>ReferenceEquals(p.Item1,source)).Item2.Substring(1);
                var siblings=Walk(source.RootNode).OfType<CHR0Node>().Where(c=>!ReferenceEquals(c,source)).ToDictionary(c=>c,SetNativeContent);
                int frameBefore=editor.CurrentFrame,undoBefore=editor._undoSaves.Count;var boneBefore=editor.SelectedBone;
                var witness=new GameLibraryDocument(form);string content=SetNativeContent(source),catalogue=FighterProject.HashFile(library.IndexPath);
                string[] protectedNames=((ControlReply)await Await<ControlReply>(Call(form,"LoadControls"))).Roles.Where(r=>r.Reviewed&&r.Role=="RootMotion").Select(r=>model.AllBones[r.Bone.Value].Name).ToArray();
                Check(protectedNames.Length>0,"Fixture has reviewed source motion-position controls");
                string controls=RecoveryKeys(source,protectedNames);int count=form.FighterProject.Animations.Count;
                bool finished=false;Exception failure=null;string resultContent=null;
                form.TransitionFitReady=async window=>
                {
                    try
                    {
                        Check(!window.Controls.Find("transition_saveAsNew",true).Any()&&!window.Controls.Find("reviewTransition",true).Any()&&SimpleControl<Button>(window,"applyTransition").Text=="Apply fitting","Fitting offers a direct Apply action without copy-mode or extra confirmation controls");
                        Check(window.Candidate==null&&FitHasPose(window.TargetView.Model)&&Flow39Pose(window.TargetView.Model).SequenceEqual(Flow39Pose(window.SourceView.Model)),"Opening fitting displays the current same-character animation on both native views, not a false T-pose result");
                        Check(SimpleControl<CheckBox>(window,"transition_binding").Checked,"Exact recorded native source/rig evidence verifies same-character fitting without the generic review blocker");
                        var protectedText=SimpleControl<TextBox>(window,"transitionProtected");protectedText.Text="10-";
                        Check(SimpleControl<CheckBox>(window,"transition_binding").Checked&&window.Candidate==null,"Typing an incomplete advanced range does not invoke options validation or lose verified bindings");protectedText.Text="";
                        window.RestoreOptions(window.ReadOptions());
                        Check(SimpleControl<CheckBox>(window,"transition_binding").Checked,"Restoring options recomputes verified native binding evidence");

                        await (Task)Call(window,"PrepareFit");
                        Check(!window.Controls.Find("transition_saveAsNew",true).Any()&&!window.Controls.Find("reviewTransition",true).Any()&&SimpleControl<Button>(window,"applyTransition").Text=="Apply fitting","Fitting offers a direct Apply action without copy-mode or extra confirmation controls");
                        Check(window.Candidate==null&&FitHasPose(window.TargetView.Model),"Fitting-off/blocked preparation keeps the original authored pose instead of displaying bind pose");
                        var reference=SimpleControl<ComboBox>(window,"transition_exit");reference.SelectedItem=reference.Items.OfType<TransitionClipChoice>().Single(c=>c.Clip.Name=="Wait1"&&c.Clip.RootNode==source.RootNode);
                        await (Task)Call(window,"PrepareFit");Check(window.Candidate!=null,"Same-character loaded native reference actually prepares a fit: "+SimpleControl<TextBox>(window,"transitionReport").Text);
                        SimpleControl<TrackBar>(window,"transitionFrame").Value=source.FrameCount;
                        Check(FitHasPose(window.TargetView.Model),"Prepared same-character fit renders an authored endpoint pose rather than bind pose");
                        var loadedPose=Flow39Pose(window.TargetView.Model);await CaptureWindow(window,Path.Combine(output,"fit-loaded-reference.png"));
                        reference.SelectedItem=reference.Items.OfType<TransitionClipChoice>().Single(c=>c.Key.StartsWith("viewer:")&&c.Clip.Name=="Wait1");
                        Check(!SimpleControl<CheckBox>(window,"transition_binding").Checked&&SimpleControl<CheckBox>(window,"transition_binding").Enabled,"Unproven viewer-loaded reference needs explicit native-rig confirmation");
                        await (Task)Call(window,"PrepareFit");Check(window.Candidate==null&&FitHasPose(window.TargetView.Model),"Unconfirmed viewer reference is blocked without a false bind-pose result");
                        SimpleControl<CheckBox>(window,"transition_binding").Checked=true;await (Task)Call(window,"PrepareFit");
                        Check(window.Candidate!=null,"Explicitly confirmed model-preview reference prepares a real fit");
                        SimpleControl<TrackBar>(window,"transitionFrame").Value=source.FrameCount;
                        Check(Flow39Pose(window.TargetView.Model).SequenceEqual(loadedPose),"Viewer-loaded reference produces the same exact endpoint pose");

                        SimpleControl<NumericUpDown>(window,"transition_exitSamples").Value+=1;
                        Check(!window.Controls.Find("transition_saveAsNew",true).Any()&&!window.Controls.Find("reviewTransition",true).Any()&&SimpleControl<Button>(window,"applyTransition").Text=="Apply fitting","Fitting offers a direct Apply action without copy-mode or extra confirmation controls");
                        Check(window.Candidate==null&&FitHasPose(window.TargetView.Model),"Changing settings invalidates the candidate while keeping the real original animation visible");
                        using(FlowModal("fittingReferenceSearch",picker=>{
                            SimpleControl<TextBox>(picker,"fittingReferenceSearch").Text="mario Wait1";
                            var list=SimpleControl<ListBox>(picker,"fittingReferenceList");
                            Check(list.Items.Cast<FittingLibraryItem>().All(i=>i.Item.Model.SkeletonSha256==BrawlRetargeter.SkeletonFingerprint(model)),"Library reference picker only offers exact reviewed same-rig native animations");
                            list.SelectedItem=list.Items.Cast<FittingLibraryItem>().Single(i=>i.Item.Clip.Name=="Wait1"&&i.Item.Clip.SourceCharacter=="mario");
                            CaptureDialog(picker,output,"fitting-library-picker.png");SimpleControl<Button>(picker,"fittingReferenceUse").PerformClick();
                        }))SimpleControl<Button>(window,"library_exit").PerformClick();
                        Check(((TransitionClipChoice)reference.SelectedItem).External&&SimpleControl<CheckBox>(window,"transition_binding").Checked,"Library reference joins fitting as a verified read-only choice without importing into the project");
                        await (Task)Call(window,"PrepareFit");Check(window.Candidate!=null,"Library-backed same-character reference actually prepares the fit: "+SimpleControl<TextBox>(window,"transitionReport").Text);
                        SimpleControl<TrackBar>(window,"transitionFrame").Value=source.FrameCount;
                        Check(Flow39Pose(window.TargetView.Model).SequenceEqual(loadedPose),"Library and loaded-native versions of Wait1 give the same exact endpoint pose");
                        var candidate=window.Candidate;var chosen=reference.SelectedItem;
                        using(FlowModal("fittingReferenceSearch",picker=>picker.Close()))SimpleControl<Button>(window,"library_exit").PerformClick();
                        Check(window.Candidate==candidate&&reference.SelectedItem==chosen&&SimpleControl<Button>(window,"library_exit").Focused,"Cancelling the library picker preserves the candidate/choice and restores invoking focus");
                        foreach(int percent in new[]{100,175})foreach(var size in new[]{new Size(980,720),new Size(1600,960)})
                        {
                            Call(form,"SetWorkspaceTextSize",percent);window.ApplyTextSize();window.ClientSize=size;window.PerformLayout();
                            var panel=(FlowLayoutPanel)Field(window,"fields");var visible=panel.Controls.Cast<Control>().Where(c=>c.Visible).OrderBy(c=>c.Top).ToArray();
                            Check(visible.Zip(visible.Skip(1),(a,b)=>a.Bottom<=b.Top).All(v=>v)&&visible.All(c=>c.Width+c.Margin.Horizontal<=panel.ClientSize.Width-panel.Padding.Horizontal),"Reference choices fit without overlap at "+size.Width+" / "+percent+"% text");
                            panel.ScrollControlIntoView(SimpleControl<Button>(window,"library_entry"));
                            await CaptureWindow(window,Path.Combine(output,"fit-references-"+size.Width+"-"+percent+".png"));
                        }
                        Call(form,"SetWorkspaceTextSize",100);window.ApplyTextSize();window.ClientSize=new Size(1240,820);
                        resultContent=SetNativeContent(window.Candidate);Check(RecoveryKeys(window.Candidate,protectedNames)==controls,"Fitting library references retains the attack's own motion-position tracks");
                        witness.CheckPreserved("same-character/library fit preparation");
                        await CaptureWindow(window,Path.Combine(output,"fit-library-reference.png"));
                        SimpleControl<Button>(window,"applyTransition").PerformClick();
                    }
                    catch(Exception error){failure=error;window.Close();}
                    finally{finished=true;}
                };
                Call(form,"ShowTransitionFit");await GameLibraryWait(()=>finished,"Fitting reference fixture did not finish.",120);if(failure!=null)throw failure;await SimpleControlsReady(form);
                Check(form.FighterProject.Animations.Count==count&&ReferenceEquals(editor.SelectedCHR0,source)&&ReferenceEquals(editor.TargetModel,model)&&ReferenceEquals(editor.SelectedBone,boneBefore)&&editor.CurrentFrame==frameBefore&&SetNativeContent(source)==resultContent,"Apply updates the exact current PAC clip without adding/replacing a target, clip, bone or playhead");
                Check(siblings.All(p=>SetNativeContent(p.Key)==p.Value),"Applying a PAC member fit leaves every sibling animation unchanged");
                var recipe=(NativeTransitionFit)Call(form,"TransitionRecipeFor",owner,source);
                Check(owner.TransitionFit==null&&owner.ClipTransitionFits.Single().Entry==nativeEntry&&recipe.Inputs.Single(i=>i.Role=="exit").Entry.StartsWith("library:"),"The PAC receipt binds to the exact native member and external reference without an asset-wide substitute");
                Check(editor._undoSaves.Count==undoBefore+1,"Current PAC fitting creates one native Undo command");
                editor.Undo();await SimpleControlsReady(form);Check(SetNativeContent(source)==content&&owner.ClipTransitionFits.Count==0&&siblings.All(p=>SetNativeContent(p.Key)==p.Value),"Native Undo restores the exact PAC member, removes its receipt and preserves siblings");
                editor.Redo();await SimpleControlsReady(form);Check(SetNativeContent(source)==resultContent&&owner.ClipTransitionFits.Single().Fit==recipe,"Native Redo restores the same fitted member and receipt");
                Call(form,"AddSelectedBatchReviewClip");Call(form,"MarkBatchReview");var approval=ReviewRows(form).Single();Check(ReviewApproved(approval),"Current PAC member review binds to its own fitting recipe");
                var frozen=typeof(ModelForm).GetMethod("FreezeOwnedWorkspaceExport",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(form,new object[]{owner,source,false,null});
                var second=siblings.Keys.Single(c=>c.Name=="AttackHi3");string secondBefore=SetNativeContent(second),secondEntry="root"+BrawlLibrarySource.Resources(second.RootNode).Single(p=>ReferenceEquals(p.Item1,second)).Item2.Substring(1),secondName=second.Name,secondResult=null;
                second.Name=source.Name;editor.SelectedCHR0=second;editor.SetFrame(5);await SimpleControlsReady(form);
                bool secondFinished=false;Exception secondFailure=null;
                form.TransitionFitReady=async window=>
                {
                    try
                    {
                        var choices=SimpleControl<ComboBox>(window,"transition_exit");choices.SelectedItem=choices.Items.OfType<TransitionClipChoice>().Single(c=>c.External&&c.Key.StartsWith("viewer:")&&c.Clip.Name=="Wait1");
                        SimpleControl<CheckBox>(window,"transition_binding").Checked=true;await(Task)Call(window,"PrepareFit");
                        Check(window.Candidate!=null&&window.ReadOptions().Name==source.Name,"An identically named sibling prepares against its own native identity: "+SimpleControl<TextBox>(window,"transitionReport").Text);
                        secondResult=SetNativeContent(window.Candidate);SimpleControl<Button>(window,"applyTransition").PerformClick();
                    }
                    catch(Exception error){secondFailure=error;window.Close();}
                    finally{secondFinished=true;}
                };
                Call(form,"ShowTransitionFit");await GameLibraryWait(()=>secondFinished,"Second PAC member fit did not complete",120);if(secondFailure!=null)throw secondFailure;await SimpleControlsReady(form);
                Check(ReferenceEquals(editor.SelectedCHR0,second)&&editor.CurrentFrame==5&&SetNativeContent(second)==secondResult&&SetNativeContent(source)==resultContent&&owner.ClipTransitionFits.Count==2,"Fitting a duplicate-name sibling changes only that exact native member and retains both recipes");
                Call(form,"SyncBatchReview");Check(ReviewApproved(approval),"A sibling's fitting recipe does not invalidate the unchanged member's review approval");
                ((Action)frozen.GetType().GetField("Validate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public).GetValue(frozen))();((IDisposable)frozen).Dispose();
                Check(true,"A frozen explicitly owned export remains current when only a sibling fit changes");
                editor.SelectedCHR0=source;editor.SetFrame(frameBefore);await SimpleControlsReady(form);editor.Undo();await SimpleControlsReady(form);
                Check(ReferenceEquals(editor.SelectedCHR0,source)&&editor.CurrentFrame==frameBefore&&SetNativeContent(source)==resultContent&&owner.ClipTransitionFits.Count==1,"Cross-clip Undo restores the sibling while preserving current selection and the first fitted member");
                editor.Redo();await SimpleControlsReady(form);Check(owner.ClipTransitionFits.Count==2&&SetNativeContent(source)==resultContent&&SetNativeContent(second)==secondResult,"Cross-clip Redo restores both exact fitted members and independent receipts");
                second.Name=secondName;secondResult=SetNativeContent(second);
                Check(siblings.Where(p=>!ReferenceEquals(p.Key,second)).All(p=>SetNativeContent(p.Key)==p.Value),"All other PAC siblings remain unchanged after two fits and cross-clip Undo/Redo");
                string saved=Path.Combine(output,"fitted.fpstudio"),export=Path.Combine(output,"fitted.chr0");form.ExportSimpleWorkspaceResult(export);form.SaveFighterProject(saved);
                using(var project=FighterProject.Open(saved))using(var root=NodeFactory.FromFile(null,project.Animations.Single(a=>a.Id==owner.Id).WorkingPath))
                {
                    var asset=project.Animations.Single(a=>a.Id==owner.Id);
                    Check(asset.ClipTransitionFits.Count==2&&asset.ClipTransitionFits.Single(f=>f.Entry==nativeEntry).Fit.Options==recipe.Options&&SetNativeContent((CHR0Node)BrawlLibrarySource.Resolve(root,"$"+nativeEntry.Substring(4)))==resultContent&&SetNativeContent((CHR0Node)BrawlLibrarySource.Resolve(root,"$"+secondEntry.Substring(4)))==secondResult,"Save/reopen retains both exact PAC member curves, identities and independent frozen recipes");
                    Check(siblings.Where(p=>!ReferenceEquals(p.Key,second)).All(p=>SetNativeContent((CHR0Node)BrawlLibrarySource.Resolve(root,BrawlLibrarySource.Resources(p.Key.RootNode).Single(r=>ReferenceEquals(r.Item1,p.Key)).Item2))==p.Value),"Fresh PAC reopen preserves every unfitted sibling curve");
                }
                using(var decoded=NodeFactory.FromFile(null,export) as CHR0Node)Check(SetNativeContent(decoded)==resultContent,"Verified export before project Save preserves the current fitted PAC member");
                FitReceiptStorageChecks(saved,owner.Id,output);
                if(args.Contains("--legacy-fit-project"))
                {
                    using(var legacy=FighterProject.Open(args[Array.IndexOf(args,"--legacy-fit-project")+1]))
                    {
                        var old=legacy.Animations.Single(a=>a.TransitionFit!=null);string options=old.TransitionFit.Options;old.TransitionFit.Validate();
                        Check(legacy.SchemaVersion==1&&old.ClipTransitionFits.Count==0,"A previously accepted schema-1 single-animation fit opens through the legacy receipt field");
                        string roundtrip=Path.Combine(output,"legacy-fit-roundtrip.fpstudio");legacy.Save(roundtrip);
                        using(var fresh=FighterProject.Open(roundtrip))Check(fresh.Animations.Single(a=>a.Id==old.Id).TransitionFit.Options==options,"Legacy fit receipts round-trip without a schema migration");
                    }
                }
                Check(FighterProject.HashFile(library.IndexPath)==catalogue,"Current-clip fitting never registers or modifies the shared catalogue");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"fit-applied-main.png"));
            }
            finally{if(!form.IsDisposed)SimpleDisposeForm(form);}
        }
    }
}
