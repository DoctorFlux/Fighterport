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
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    sealed class WalkthroughJournal
    {
        readonly string _path;
        internal readonly List<object> Steps=new List<object>();
        internal WalkthroughJournal(string output) { _path=Path.Combine(output,"walkthrough-steps.json"); }
        internal void Step(string scenario,string action,Action handler,string kind="ApplicationHandler")
        {
            var watch=Stopwatch.StartNew();
            string outcome="completed";
            try { handler(); }
            catch { outcome="failed";throw; }
            finally
            {
                Steps.Add(new {Ordinal=Steps.Count+1,Scenario=scenario,Action=action,Kind=kind,Outcome=outcome,HandlerElapsedMilliseconds=watch.ElapsedMilliseconds});
                File.WriteAllText(_path,new JavaScriptSerializer().Serialize(new {EvidenceClass="Scripted private-desktop task replay",HumanWalkthroughPerformed=false,PhysicalInputExercised=false,HumanStepCountsMeasured=false,Steps}));
            }
        }
    }

    static string WalkthroughMotionControls(CHR0Node clip,IEnumerable<string> controls)
    {
        var names=new HashSet<string>(controls,StringComparer.Ordinal);
        return new JavaScriptSerializer().Serialize(clip.Children.OfType<CHR0EntryNode>().Where(e=>names.Contains(e.Name)).OrderBy(e=>e.Name).Select(e=>new
        {
            e.Name,e.UseModelScale,e.UseModelRotate,e.UseModelTranslate,e.ScaleCompensateApply,e.ScaleCompensateParent,e.ClassicScaleOff,
            Keys=e.KeyArrays.Select(a=>a.Entries.Select(k=>new{k._index,k._value,k._tangent}).ToArray()).ToArray()
        }).ToArray());
    }

    static async Task WorkspaceWalkthrough(string[] args,string output)
    {
        if(!PrivateDesktop||!args.Contains("--retarget-source")||!args.Contains("--target-motion"))throw new InvalidOperationException("Walkthrough replay requires a private desktop and authentic source model.");
        string donor=args[Array.IndexOf(args,"--retarget-source")+1],clipName=args[Array.IndexOf(args,"--clip")+1];
        string targetMotion=args[Array.IndexOf(args,"--target-motion")+1];
        var originals=new[]{args[0],args[1],donor,targetMotion}.Distinct().ToDictionary(p=>p,FighterProject.HashFile);
        string prior=Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",Path.Combine(output,"isolated-library"));
        var journal=new WalkthroughJournal(output);
        ModelForm form=null;
        string firstManifest=Path.Combine(output,"first-project.fpstudio"),secondManifest=Path.Combine(output,"repeat-project.fpstudio");
        string firstExport=Path.Combine(output,"first-result.chr0"),secondExport=Path.Combine(output,"repeat-result.chr0");
        string firstInitial=null,firstEdited=null,repeatEdited=null,sourceId=null;
        Dictionary<string,string> firstSavedFiles=null;
        long libraryGeneration=0;
        int associationDialogs=0,retargetDialogs=0;
        try
        {
            using(var startup=MainForm.StartViewerApplication())
            {
                form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
                form.WindowState=FormWindowState.Normal;form.ClientSize=new Size(1600,950);
                await Task.Delay(150);await SimpleControlsReady(form);
                Check(form.FighterEditor.TargetModel==null&&FlowStage(form,"Source")&&!(bool)Field(form,"_fighterDirty"),"Walkthrough starts at a clean Source stage without a fabricated donor or target");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"walkthrough-start.png"));

                for(int pass=0;pass<2;pass++)
                {
                    string scenario=pass==0?"first-use":"repeat-use";
                    string manifest=pass==0?firstManifest:secondManifest,export=pass==0?firstExport:secondExport;
                    var editor=form.FighterEditor;
                    var originalViewport=editor.ModelPanel;var originalBones=editor.rightPanel;var originalAnimations=editor.leftPanel;
                    if(pass==0)
                    {
                        journal.Step(scenario,"Register the original model/motion pair once",()=>
                        {
                            using(FlowModal("libraryModelFile",dialog=>
                            {
                                associationDialogs++;
                                FlowPathChoice(SimpleControl<ComboBox>(dialog,"libraryModelFile"),donor);
                                FlowPathChoice(SimpleControl<ComboBox>(dialog,"libraryMotionFile"),args[1]);
                                SimpleControl<TextBox>(dialog,"libraryGameVersion").Text="Local supplied fixture; actual game build unverified";
                                SimpleControl<CheckBox>(dialog,"libraryApproveAssociation").Checked=true;
                                CaptureDialog(dialog,output,"walkthrough-register-source.png");
                                SimpleControl<Button>(dialog,"libraryRegisterSource").PerformClick();
                            }))SimpleControl<Button>(form,"workspaceAddSourceAction").PerformClick();
                        });
                    }
                    else journal.Step(scenario,"Open the persisted source library",()=>SimpleToolbarItem(form,"workspaceSourceLibrary").PerformClick());
                    await Wait(()=>!(bool)Field(form,"_libraryReplacingTree")&&!((Timer)Field(form,"_librarySearchTimer")).Enabled);
                    var library=(AnimationLibrary)Field(form,"_library");
                    var selected=await FlowSelectSource(form,library,clipName);
                    if(pass==0){sourceId=selected.Clip.Id;libraryGeneration=library.Read().Generation;}
                    Check(selected.Clip.Id==sourceId&&library.Read().Generation==libraryGeneration&&associationDialogs==1,"Known source association survives the independent project boundary without another registration: "+scenario);
                    journal.Step(scenario,"Continue from the verified source",()=>
                    {
                        SimpleToolbarItem(form,"workspaceStageSource").PerformClick();
                        SimpleControl<Button>(form,"workspacePrimaryAction").PerformClick();
                    });
                    Check(FlowStage(form,"Target")&&editor.TargetModel==null,"Source Continue reaches Target without inventing a target: "+scenario);
                    journal.Step(scenario,"Cancel target selection and keep the source",()=>
                    {
                        using(FlowModal("workspaceFileCancel",dialog=>SimpleControl<Button>(dialog,"workspaceFileCancel").PerformClick()))
                            SimpleControl<Button>(form,"workspacePrimaryAction").PerformClick();
                    });
                    Check(editor.TargetModel==null&&((LibrarySearchItem)((TreeView)Field(form,"_libraryList")).SelectedNode.Tag).Clip.Id==sourceId,"Cancelled target selection preserves the exact source and empty target: "+scenario);
                    journal.Step(scenario,"Open an existing target through the workspace path dialog",()=>
                    {
                        using(FlowModal("workspaceFilePath",dialog=>FlowAcceptFile(dialog,args[0])))SimpleControl<Button>(form,"workspacePrimaryAction").PerformClick();
                    });
                    await SimpleControlsReady(form);
                    var target=(MDL0Node)editor.TargetModel;
                    string modelHash=(string)Call(form,"NativeControlHash",target,".mdl0");
                    Check(FlowStage(form,"Target")&&ReferenceEquals(editor.ModelPanel,originalViewport)&&ReferenceEquals(editor.rightPanel,originalBones)&&ReferenceEquals(editor.leftPanel,originalAnimations),"Target loading retains the full original native editor controls: "+scenario);
                    journal.Step(scenario,"Continue to Preview and fix",()=>SimpleControl<Button>(form,"workspacePrimaryAction").PerformClick());
                    Check(FlowStage(form,"Preview"),"Target Continue reaches Preview through the ordinary primary action: "+scenario);
                    UseModelSelectMode(form, RetargetKeyframeMode.Dense);
                    journal.Step(scenario,"Preview transfer with original Next",()=>
                    {
                        using(OnRetargetReady(form,dialog=>retargetDialogs++))SimpleControl<Button>(form,"workspacePrimaryAction").PerformClick();
                    });
                    await SimpleControlsReady(form);
                    var clip=editor.SelectedCHR0;
                    var asset=form.FighterProject.Animations.Single();
                    Check(clip!=null&&asset.Retarget.Algorithm==HumanoidRetargeter.Algorithm&&asset.Retarget.MotionControlPolicy==HumanoidRetargeter.MotionControlPolicy&&asset.Retarget.Library.ClipId==sourceId&&ReferenceEquals(Call(form,"SelectedFighterAsset"),asset),"Walkthrough transfer selects the exact independent result with original Next and source-local policy provenance: "+scenario);
                    string initial=LifecycleContent(clip);
                    clip.Export(Path.Combine(output,"walkthrough-"+scenario+"-initial.chr0"));
                    File.WriteAllText(Path.Combine(output,"walkthrough-"+scenario+"-initial.json"),initial);
                    if(pass==0)firstInitial=initial;
                    else Check(initial==firstInitial&&initial!=firstEdited,"The returning project starts with a fresh transfer and inherits none of the first project's body edits");
                    var setup=await Await<ControlReply>(Call(form,"LoadControls"));
                    string[] controls=setup.ProtectedMotionBones.Select(i=>setup.BoneNames[i]).ToArray();
                    string protectedBefore=WalkthroughMotionControls(clip,controls);
                    Check(controls.Length>0&&controls.All(name=>!editor.CanEditAnimationBone(name)),"Reviewed global/motion controls remain source-only during the walkthrough: "+scenario);
                    int bodyIndex=setup.Roles.Single(r=>r.Role=="Hips").Bone.Value;
                    editor.SetFrame(3);editor.SelectedBone=target._linker.BoneCache.Single(b=>b.Name==setup.BoneNames[bodyIndex]);
                    File.WriteAllText(Path.Combine(output,"walkthrough-"+scenario+"-selected.json"),LifecycleContent(clip));
                    var body=editor.SelectedBone;float before=body.FrameState._rotate._x,delta=pass==0?2f:4f;
                    journal.Step(scenario,"Author a body correction with the original native transform field",()=>
                    {
                        var field=editor.CHR0Editor._transBoxes[3];field.Text=(before+delta).ToString(System.Globalization.CultureInfo.CurrentCulture);Call(field,"Apply");
                    },"NativeTransformFieldHandler");
                    await Wait(()=>Field(form,"_pinGesture")==null);await SimpleControlsReady(form);
                    string edited=LifecycleContent(clip);
                    Check(Math.Abs(body.FrameState._rotate._x-before-delta)<.0002f&&edited!=initial,"Native body rotation reaches the independently specified correction: "+scenario);
                    Check(WalkthroughMotionControls(clip,controls)==protectedBefore,"Body correction does not change any protected control key, tangent or flag: "+scenario);
                    journal.Step(scenario,"Undo the body correction",editor.Undo,"NativeUndoHandler");await SimpleControlsReady(form);
                    File.WriteAllText(Path.Combine(output,"walkthrough-"+scenario+"-undo.json"),LifecycleContent(editor.SelectedCHR0));
                    Console.WriteLine("TRACE walkthrough undo sameClip="+ReferenceEquals(clip,editor.SelectedCHR0)+" canUndo="+editor.CanUndo+" body="+body.FrameState._rotate._x+" expected="+before);
                    Check(LifecycleContent(editor.SelectedCHR0)==initial,"Native Undo restores exact pre-edit keys and flags: "+scenario);
                    journal.Step(scenario,"Redo the body correction",editor.Redo,"NativeRedoHandler");await SimpleControlsReady(form);
                    Check(LifecycleContent(clip)==edited,"Native Redo restores the exact independent correction: "+scenario);
                    if(pass==0)firstEdited=edited;else repeatEdited=edited;
                    editor.FrameFighterModel();
                    await CaptureSimpleWorkspace(form,Path.Combine(output,"walkthrough-"+scenario+"-edited.png"));
                    journal.Step(scenario,"Export the selected independent target animation",()=>
                    {
                        SimpleToolbarItem(form,"workspaceStageExport").PerformClick();Call(form,"ExportSimpleWorkspaceResult",export);
                    },"NativeExportHandlerWithoutOSFileDialog");
                    using(var decoded=NodeFactory.FromFile(null,export) as CHR0Node)Check(NativeDifference(clip,decoded)==null,"Fresh native decode matches every edited key, tangent and flag: "+scenario);
                    journal.Step(scenario,"Save the project through its path dialog",()=>
                    {
                        using(FlowModal("workspaceFilePath",dialog=>FlowAcceptFile(dialog,manifest)))SimpleToolbarItem(form,"fighterSave").PerformClick();
                    });
                    Check(File.Exists(manifest)&&form.FighterProject.ManifestPath==manifest&&!(bool)Field(form,"_fighterDirty"),"Application Save commits the intended independent project: "+scenario);
                    Check((string)Call(form,"NativeControlHash",target,".mdl0")==modelHash,"The complete workflow preserves authentic target model bytes, hierarchy, binds and weights: "+scenario);
                    if(pass==0)
                    {
                        Call(form,"MarkFighterChanged");
                        journal.Step(scenario,"Keep editing after New project",()=>
                        {
                            using(FlowModal("unsavedProjectCancel",dialog=>
                            {
                                CaptureDialog(dialog,output,"walkthrough-keep-editing.png");SimpleControl<Button>(dialog,"unsavedProjectCancel").PerformClick();
                            }))SimpleToolbarItem(form,"workspaceNewProject").PerformClick();
                        });
                        Check(!form.IsDisposed&&LifecycleContent(clip)==edited&&editor.CurrentFrame==3&&editor.CanUndo,"Keep editing preserves exact native work, frame and a usable Undo stack");
                        using(var locked=new FileStream(manifest,FileMode.Open,FileAccess.Read,FileShare.None))
                            journal.Step(scenario,"Attempt Save before New while the project is locked",()=>
                            {
                                using(FlowModal("unsavedProjectSave",dialog=>
                                {
                                    SimpleControl<Button>(dialog,"unsavedProjectSave").PerformClick();
                                    Check(dialog.Visible&&SimpleControl<Label>(dialog,"unsavedProjectStatus").Text.StartsWith("Could not save"),"A real sharing violation keeps the current decision open with its save error");
                                    CaptureDialog(dialog,output,"walkthrough-save-failed.png");SimpleControl<Button>(dialog,"unsavedProjectCancel").PerformClick();
                                }))SimpleToolbarItem(form,"workspaceNewProject").PerformClick();
                            });
                        Check(!form.IsDisposed&&(bool)Field(form,"_fighterDirty")&&LifecycleContent(clip)==edited,"Failed replacement leaves the dirty edited project available");
                        journal.Step(scenario,"Undo after the failed save",editor.Undo,"NativeUndoHandler");await SimpleControlsReady(form);
                        Check(LifecycleContent(clip)==initial,"Exact bone-channel Undo remains valid after native resource merge and failed save");
                        journal.Step(scenario,"Redo after the failed save",editor.Redo,"NativeRedoHandler");await SimpleControlsReady(form);
                        Check(LifecycleContent(clip)==edited,"Exact bone-channel Redo remains valid after the failed save");
                        journal.Step(scenario,"Save successfully after releasing the file lock",()=>SimpleToolbarItem(form,"fighterSave").PerformClick());
                        Check(!(bool)Field(form,"_fighterDirty"),"Save recovers after the real file-lock failure");
                        firstSavedFiles=Directory.EnumerateFiles(manifest+".assets","*",SearchOption.AllDirectories).Concat(new[]{manifest,export}).ToDictionary(p=>p,FighterProject.HashFile);
                        journal.Step(scenario,"Start a second independent project",()=>SimpleToolbarItem(form,"workspaceNewProject").PerformClick());
                        Check(form.IsDisposed,"New replaces only the successfully saved first project");
                        form=(ModelForm)Field(MainForm.Instance,"_fighterForm");form.WindowState=FormWindowState.Normal;form.ClientSize=new Size(1600,950);
                        await Task.Delay(150);await SimpleControlsReady(form);
                        Check(form.FighterEditor.TargetModel==null&&!(bool)Field(form,"_fighterDirty"),"Returning workflow starts a clean independent target document");
                    }
                }
                Check(associationDialogs==1&&retargetDialogs==2,"Two independent transfer projects require one source registration and two actual Next operations");
                Check(firstSavedFiles.All(p=>File.Exists(p.Key)&&FighterProject.HashFile(p.Key)==p.Value),"The second project's transfer, correction and save leave all first-project files byte-identical");
                foreach(var saved in new[]{new{Path=firstManifest,Content=firstEdited,Label="first"},new{Path=secondManifest,Content=repeatEdited,Label="repeat"}})
                {
                    journal.Step("resume","Open the "+saved.Label+" saved project",()=>
                    {
                        using(FlowModal("workspaceFilePath",dialog=>FlowAcceptFile(dialog,saved.Path)))SimpleToolbarItem(form,"workspaceOpenProject").PerformClick();
                    });
                    form=(ModelForm)Field(MainForm.Instance,"_fighterForm");await Task.Delay(150);await SimpleControlsReady(form);
                    var editor=form.FighterEditor;
                    Check(editor.SelectedCHR0!=null&&LifecycleContent(editor.SelectedCHR0)==saved.Content&&editor.CurrentFrame==3,"Actual workspace reopen restores the exact "+saved.Label+" authored native clip and frame");
                    Check(form.FighterProject.Warnings.Count==0&&form.FighterProject.Animations.Single().Retarget.Library.ClipId==sourceId,"Reopened "+saved.Label+" project retains the original shared source identity without another association");
                    form.WindowState=FormWindowState.Normal;form.ClientSize=new Size(1600,950);editor.FrameFighterModel();
                    await CaptureSimpleWorkspace(form,Path.Combine(output,"walkthrough-reopened-"+saved.Label+".png"));
                }
                journal.Step("donor-free","Start a clean native-edit project",()=>SimpleToolbarItem(form,"workspaceNewProject").PerformClick());
                Check(form.IsDisposed,"Saved repeat project can be left without an unnecessary save prompt");
                form=(ModelForm)Field(MainForm.Instance,"_fighterForm");form.WindowState=FormWindowState.Normal;form.ClientSize=new Size(1600,950);
                await Task.Delay(150);await SimpleControlsReady(form);
                journal.Step("donor-free","Open a native target without browsing the source library",()=>
                {
                    SimpleToolbarItem(form,"workspaceStageTarget").PerformClick();
                    using(FlowModal("workspaceFilePath",dialog=>FlowAcceptFile(dialog,args[0])))SimpleControl<Button>(form,"workspacePrimaryAction").PerformClick();
                });
                journal.Step("donor-free","Import that target's own native motion package",()=>Call(form,"ImportFighterAnimations",new[]{new[]{targetMotion}}),"NativeImportHandlerWithoutOSFileDialog");
                var nativeEditor=form.FighterEditor;
                nativeEditor.SelectedCHR0=nativeEditor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First(c=>c.Name==clipName);
                nativeEditor.SetFrame(3);await SimpleControlsReady(form);
                var nativeSetup=await Await<ControlReply>(Call(form,"LoadControls"));
                Check(Field(form,"_library")==null&&nativeEditor.SelectedCHR0!=null&&nativeSetup.BoneNames.Length>0,"Donor-free native animation and existing-rig controls work without a registered library source in this project");
                string nativeContent=LifecycleContent(nativeEditor.SelectedCHR0),nativeExport=Path.Combine(output,"native-only.chr0"),nativeManifest=Path.Combine(output,"native-only.fpstudio");
                journal.Step("donor-free","Export the selected native animation",()=>Call(form,"ExportSimpleWorkspaceResult",nativeExport),"NativeExportHandlerWithoutOSFileDialog");
                using(var decoded=NodeFactory.FromFile(null,nativeExport) as CHR0Node)Check(NativeDifference(nativeEditor.SelectedCHR0,decoded)==null,"Donor-free output freshly decodes with its native channels and flags preserved");
                journal.Step("donor-free","Save the native-edit project",()=>
                {
                    using(FlowModal("workspaceFilePath",dialog=>FlowAcceptFile(dialog,nativeManifest)))SimpleToolbarItem(form,"fighterSave").PerformClick();
                });
                Check(LifecycleContent(nativeEditor.SelectedCHR0)==nativeContent&&File.Exists(nativeManifest),"Donor-free saving does not retarget or change the imported motion");
                nativeEditor.FrameFighterModel();await CaptureSimpleWorkspace(form,Path.Combine(output,"walkthrough-native-only.png"));
                // Original procedural clips cover both absent bone tracks and a
                // new key between sparse neighbors with authored tangents/flags.
                string bodyName=nativeSetup.BoneNames[nativeSetup.Roles.Single(r=>r.Role=="Hips").Bone.Value];
                foreach(bool sparse in new[]{false,true})
                {
                    string fixtureName=sparse?"SparseBodyTrack":"AbsentBodyTrack";
                    string fixturePath=Path.Combine(output,fixtureName+".chr0");
                    using(var fixture=new CHR0Node{Name=fixtureName,FrameCount=5,Loop=false})
                    {
                        if(sparse)
                        {
                            var entry=fixture.CreateEntry(bodyName);entry.UseModelScale=true;entry.UseModelRotate=false;entry.UseModelTranslate=true;
                            entry.ScaleCompensateParent=true;entry.ClassicScaleOff=true;
                            entry.SetKeyframe(3,0,10,true,true)._tangent=.1234567f;
                            entry.SetKeyframe(3,4,18,true,true)._tangent=-.9876543f;
                        }
                        PreciseCHR0EntryNode.Promote(fixture);
                        fixture.Export(fixturePath);
                    }
                    journal.Step("native-undo", "Import original procedural "+fixtureName,()=>Call(form,"ImportFighterAnimations",new[]{new[]{fixturePath}}),"ProceduralNativeImportHandler");
                    var fixtureClip=nativeEditor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c=>c.Name==fixtureName);
                    nativeEditor.SelectedCHR0=fixtureClip;nativeEditor.SetFrame(3);await SimpleControlsReady(form);
                    nativeEditor.SelectedBone=((MDL0Node)nativeEditor.TargetModel)._linker.BoneCache.Single(b=>b.Name==bodyName);
                    var originalEntry=fixtureClip.Children.OfType<CHR0EntryNode>().SingleOrDefault(e=>e.Name==bodyName);
                    Check(sparse?originalEntry!=null&&originalEntry.GetKeyframe(3,2)==null:originalEntry==null,"Procedural regression starts with the intended missing key/track: "+fixtureName);
                    string fixtureBefore=LifecycleContent(fixtureClip);
                    float value=nativeEditor.SelectedBone.FrameState._rotate._x;
                    journal.Step("native-undo", "Edit body field in "+fixtureName,()=>
                    {
                        var field=nativeEditor.CHR0Editor._transBoxes[3];field.Text=(value+2).ToString(System.Globalization.CultureInfo.CurrentCulture);Call(field,"Apply");
                    },"NativeTransformFieldHandler");
                    await SimpleControlsReady(form);string fixtureAfter=LifecycleContent(fixtureClip);
                    Check(fixtureAfter!=fixtureBefore&&fixtureClip.Children.OfType<CHR0EntryNode>().Single(e=>e.Name==bodyName).GetKeyframe(3,2)!=null,"Native field inserts the expected body key: "+fixtureName);
                    Check(nativeEditor._undoSaves.Last() is BrawlLib.Modeling.BoneState,"Procedural field regression exercises the retained native BoneState route: "+fixtureName);
                    journal.Step("native-undo", "Undo "+fixtureName,nativeEditor.Undo,"NativeUndoHandler");await SimpleControlsReady(form);
                    Check(LifecycleContent(fixtureClip)==fixtureBefore,"Exact native Undo preserves missing groups/keys and original tangents/flags: "+fixtureName);
                    journal.Step("native-undo", "Redo "+fixtureName,nativeEditor.Redo,"NativeRedoHandler");await SimpleControlsReady(form);
                    Check(LifecycleContent(fixtureClip)==fixtureAfter,"Exact native Redo restores the inserted key and edited neighbor tangents/flags: "+fixtureName);
                }
                Check(firstSavedFiles.All(p=>File.Exists(p.Key)&&FighterProject.HashFile(p.Key)==p.Value),"Reopen and donor-free work also leave every saved first-project asset unchanged");
                Check(originals.All(p=>FighterProject.HashFile(p.Key)==p.Value),"First/repeat transfer, native correction, save failure and reopen preserve all supplied originals");
                File.WriteAllText(Path.Combine(output,"walkthrough-summary.json"),new JavaScriptSerializer().Serialize(new
                {
                    Passed=true,HumanWalkthroughPerformed=false,PhysicalInputExercised=false,HumanTaskTimeMeasured=false,
                    SourceRegistrations=associationDialogs,NextOperations=retargetDialogs,RecordedHandlerSteps=journal.Steps.Count,
                    IndependentProjects=3,SourceClipId=sourceId,OriginalHashes=originals,
                    Limits=new[]{"OS Browse/Save dialog interaction not exercised","Handler steps and durations are not human usability metrics","Human first/repeat task outcomes and physical input/DPI remain unverified","Other-game/model-port/batch and general motion quality are outside this replay"}
                }));
            }
        }
        finally
        {
            if(form!=null&&!form.IsDisposed){Call(form,"DisposeAnimationLibrary");SimpleDisposeForm(form);}
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",prior);
        }
    }
}
