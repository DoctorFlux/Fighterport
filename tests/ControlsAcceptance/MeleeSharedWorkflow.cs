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
    static async Task MeleeSharedWorkflow(string output,AnimationLibrary melee,AnimationLibrary brawl)
    {
        using(var startup=MainForm.StartViewerApplication())
        {
            var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
            try
            {
                foreach(string actor in new[]{"Mario","Fox"}.Where(a=>Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_BRIDGE_ACTOR") is string selected&&selected.Length>0?a==selected:true))foreach(bool toMelee in new[]{true,false}.Where(direction=>Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_BRIDGE_DIRECTION") is string wanted&&wanted.Length>0?direction==(wanted=="Melee"):true))
                {
                    string label=actor+(toMelee?"-brawl-to-melee":"-melee-to-brawl");Console.WriteLine("TRACE UI "+label);
                    string dir=Path.Combine(output,label);Directory.CreateDirectory(dir);
                    var targetLibrary=toMelee?melee:brawl;var sourceLibrary=toMelee?brawl:melee;
                    LibrarySearchItem Idle(AnimationLibrary library,bool isMelee)=>library.Browse("Wait1").First(i=>i.Clip.Name=="Wait1"&&(isMelee?i.Model?.Character==actor:i.Model?.Name=="Fit"+actor+"00"));
                    var targetItem=Idle(targetLibrary,toMelee);var donorItem=Idle(sourceLibrary,!toMelee);
                    Call(form,"OpenSavedGameLibrary",targetLibrary,targetLibrary.Read().GameLocations.Single(l=>l.Game==(toMelee?"Melee":"Brawl")));
                    Call(form,"SetSimpleWorkspaceLibraryVisible",true);await Flow39Select(form,targetItem);Call(form,"PauseSourceOperation");
                    using(FlowModal("unsavedProjectDiscard",w=>SimpleControl<Button>(w,"unsavedProjectDiscard").PerformClick()))Call(form,"OpenLibraryAsTarget");
                    await SimpleControlsReady(form);
                    var editor=form.FighterEditor;var target=(MDL0Node)editor.TargetModel;var ownIdle=editor.SelectedCHR0;
                    Check(ReferenceEquals(form,Field(MainForm.Instance,"_fighterForm"))&&ownIdle?.Name=="Wait1","Actual Open as target keeps the full viewer and exact selected native member: "+label);
                    if(toMelee)Check(editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Count()==form.FighterProject.NativeTarget.Members.Length,"Actual main editor lists the complete native character animation set: "+label);
                    string modelHash=(string)Call(form,"NativeControlHash",target,".mdl0");
                    Call(form,"OpenSavedGameLibrary",sourceLibrary,sourceLibrary.Read().GameLocations.Single(l=>l.Game==(!toMelee?"Melee":"Brawl")));
                    await Flow39Select(form,donorItem);Call(form,"PauseSourceOperation");
                    var donor=(LibrarySourceLease)Field(form,"_libraryLease");string originalSource=NativeAnimationOptimization.SourceContentKey(donor.Animation);
                    if(actor=="Fox")
                    {
                        // The fixture's structural roles are explicit test review,
                        // not a hidden mapping exception in the application.
                        SonicReviewNativeMapping(form,target,BrawlRetargeter.SkeletonFingerprint(target),dir,"target");
                        SonicReviewNativeMapping(form,donor.Model,BrawlRetargeter.SkeletonFingerprint(donor.Model),dir,"source");
                        if(!toMelee)
                        {
                            var targetRig=BrawlRetargeter.Capture(target);var reviewedRoles=targetRig.Rig.Roles.ToDictionary(p=>p.Key,p=>targetRig.Bones[p.Value].Name);
                            using(var axes=Modal("Rig Coordinates",window=>window.Controls.OfType<Button>().Single(b=>b.Text=="Use Coordinates").PerformClick()))
                                Check((bool)Call(form,"ReviewEngineCoordinates",target,reviewedRoles),"Existing Core coordinate review confirms the exact native Brawl Fox chain geometry");
                        }
                    }
                    Check(SimpleControl<Button>(form,"libraryUseOnTarget").Enabled,"Cross-game source enables the actual Use on this model button: "+label);
                    bool reviewed=false;Exception reviewFailure=null;
                    form.RetargetDialogReady+=dialog=>File.AppendAllText(Path.Combine(dir,"ui-trace.txt"),"Retarget progress opened: "+dialog.Text+Environment.NewLine);
                    form.RetargetKeyframeReviewReady=window=>
                    {
                        try
                        {
                        var dense=(CHR0Node)Field(window,"dense");var candidate=(CHR0Node)Field(window,"candidate");
                        var solver=new HumanoidRetargeter(BrawlRetargeter.Capture(donor.Model).Rig,BrawlRetargeter.Capture(target).Rig);
                        var names=solver.TargetMotionControls.Select(i=>target.AllBones[i].Name).ToArray();
                        Check(RecoveryKeys(dense,names)==RecoveryKeys(candidate,names),"Actual Use sparse review retains every source-owned control coefficient: "+label);
                        reviewed=true;SimpleControl<CheckBox>(window,"sourceKeysReviewed").Checked=true;SimpleControl<Button>(window,"sourceKeysUse").PerformClick();
                        }
                        catch(Exception error){reviewFailure=error;window.Close();}
                    };
                    int beforeCount=form.FighterProject.Animations.Count;
                    using(var errors=new Timer{Interval=100})
                    {
                        int ticks=0;
                        errors.Tick+=(sender,eventArgs)=>
                        {
                            if(++ticks%30==0)File.AppendAllText(Path.Combine(dir,"ui-trace.txt"),string.Join(" | ",Application.OpenForms.Cast<Form>().Select(w=>w.GetType().Name+":"+w.Text))+Environment.NewLine);
                            var message=Application.OpenForms.Cast<Form>().FirstOrDefault(w=>w!=form&&w.Text=="FighterPort Studio V2");
                            if(message==null)return;
                            reviewFailure=new InvalidOperationException("Actual Use reported: "+string.Join(" ",message.Controls.Cast<Control>().Select(c=>c.Text)));
                            message.Close();
                        };
                        errors.Start();
                        SimpleControl<Button>(form,"libraryUseOnTarget").PerformClick();
                        errors.Stop();
                    }
                    if(reviewFailure!=null)throw reviewFailure;await SimpleControlsReady(form);form.RetargetKeyframeReviewReady=null;
                    var clip=editor.SelectedCHR0;
                    Check(reviewed&&form.FighterProject.Animations.Count==beforeCount+1&&clip!=ownIdle,"Actual Use publishes exactly one independent target animation: "+label);
                    Check(NativeAnimationOptimization.SourceContentKey(donor.Animation)==originalSource,"Use preserves the exact source master: "+label);
                    editor.SetFrame(3);var controls=await Await<ControlReply>(Call(form,"LoadControls"));
                    Console.WriteLine("TRACE controls "+label+" chains="+(controls.Chains?.Length??-1)+" roots="+(controls.ProtectedMotionBones?.Length??-1)+" body="+controls.BodyBone+" diagnostics="+string.Join(" | ",controls.Diagnostics??new string[0]));
                    Check(controls.Chains.Any(c=>c.Reviewed)&&controls.ProtectedMotionBones.Length>0,"Shared native setup exposes reviewed IK chains and protected roots: "+label);
                    int body=controls.BodyBone.Value;string roots=RecoveryKeys(clip,controls.ProtectedMotionBones.Select(i=>controls.BoneNames[i]).ToArray());
                    var ik=editor.AnimationHandles.First(h=>h.Id.StartsWith("ik:",StringComparison.Ordinal));
                    string beforeIk=SetNativeContent(clip);editor.SelectControlHandle(ik);
                    Call(form,"BeginControlGesture",ik);await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));
                    var ikFrame=editor.SelectedControlTransform.Value;ikFrame._translate._x+=.025f;
                    Call(form,"MoveControlGesture",BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.ControlHandleGoal(ikFrame));
                    Call(form,"EndControlGesture");await Wait(()=>Field(form,"_pinGesture")==null);
                    string afterIk=SetNativeContent(clip);
                    Check(afterIk!=beforeIk&&RecoveryKeys(clip,controls.ProtectedMotionBones.Select(i=>controls.BoneNames[i]).ToArray())==roots,"Native IK edits an existing body chain and preserves authored roots: "+label);
                    editor.Undo();await SimpleControlsReady(form);Check(SetNativeContent(clip)==beforeIk,"Native IK Undo restores all exact channels: "+label);
                    editor.Redo();await SimpleControlsReady(form);Check(SetNativeContent(clip)==afterIk,"Native IK Redo restores the solved chain: "+label);
                    editor.Undo();await SimpleControlsReady(form);
                    string beforeEdit=SetNativeContent(clip);
                    editor.SelectedBone=target.AllBones.Single(b=>b.Name==controls.BoneNames[body]);
                    var box=editor.chr0Editor._transBoxes[3];box.Value+=1;editor.chr0Editor.BoxChangedCreateUndo(box,EventArgs.Empty);await SimpleControlsReady(form);
                    string afterEdit=SetNativeContent(clip);Check(afterEdit!=beforeEdit,"Current native body edits remain available after cross-game Use: "+label);
                    editor.Undo();await SimpleControlsReady(form);Check(SetNativeContent(clip)==beforeEdit,"Body Undo restores the exact cross-game result: "+label);
                    editor.Redo();await SimpleControlsReady(form);Check(SetNativeContent(clip)==afterEdit,"Body Redo restores the exact edited result: "+label);
                    editor.Undo();await SimpleControlsReady(form);
                    // Use the actual independent fitting window and native Apply command.
                    Exception failure=null;bool finished=false;string unfitted=SetNativeContent(clip);
                    form.TransitionFitReady=async window=>
                    {
                        try
                        {
                            SimpleControl<CheckBox>(window,"transition_entryEnabled").Checked=false;
                            SimpleControl<CheckBox>(window,"transition_exitEnabled").Checked=true;
                            var choice=SimpleControl<ComboBox>(window,"transition_exit");choice.SelectedItem=choice.Items.OfType<TransitionClipChoice>().Single(c=>ReferenceEquals(c.Clip,ownIdle));
                            SimpleControl<CheckBox>(window,"transition_exitFrozen").Checked=true;SimpleControl<NumericUpDown>(window,"transition_exitFrame").Value=1;SimpleControl<NumericUpDown>(window,"transition_exitSamples").Value=10;
                            SimpleControl<CheckBox>(window,"transition_boundaryContactRecovery").Checked=false;SimpleControl<CheckBox>(window,"transition_matchReferenceControls").Checked=false;SimpleControl<CheckBox>(window,"transition_binding").Checked=true;
                            await (Task)Call(window,"PrepareFit");
                            string report=SimpleControl<TextBox>(window,"transitionReport").Text;File.WriteAllText(Path.Combine(dir,"fit-report.txt"),report);
                            Check(window.Candidate!=null,"Actual fit prepares own Wait1 first frame: "+label+" "+report);
                            Check(SetNativeContent(clip)==unfitted,"Independent fitting preview does not mutate the editable master: "+label);
                            var fitted=window.Candidate;var bones=window.ReadOptions().Bones;
                            var reference=BrawlRetargeter.Sample(BrawlRetargeter.Capture(target),ownIdle,0);var result=BrawlRetargeter.Sample(BrawlRetargeter.Capture(target),fitted,fitted.FrameCount-1);
                            Check(bones.Where(n=>!controls.ProtectedMotionBones.Any(i=>controls.BoneNames[i]==n)).All(n=>RecoverySamePose(RecoverySample(fitted,target.AllBones.Single(b=>b.Name==n),fitted.FrameCount-1),RecoverySample(ownIdle,target.AllBones.Single(b=>b.Name==n),0))),"Fitted body endpoint is the target's own frozen idle pose: "+label);
                            SimpleControl<Button>(window,"applyTransition").PerformClick();
                        }
                        catch(Exception e){failure=e;window.Close();}finally{finished=true;}
                    };
                    Call(form,"ShowTransitionFit");await Wait(()=>finished);form.TransitionFitReady=null;if(failure!=null)throw failure;await SimpleControlsReady(form);
                    string fittedContent=SetNativeContent(clip);Check(ReferenceEquals(editor.SelectedCHR0,clip)&&RecoveryKeys(clip,controls.ProtectedMotionBones.Select(i=>controls.BoneNames[i]).ToArray())==roots,"Fit Apply keeps the current clip and source-local root data: "+label);
                    editor.Undo();await SimpleControlsReady(form);Check(SetNativeContent(clip)==unfitted,"Fit Undo restores the exact finished retarget master: "+label);editor.Redo();await SimpleControlsReady(form);Check(SetNativeContent(clip)==fittedContent,"Fit Redo restores without solving again: "+label);
                    string savedContent=NativeAnimationOptimization.SourceContentKey(clip);
                    string savedProject=Path.Combine(dir,"bridge.fpstudio");form.SaveFighterProject(savedProject);
                    using(var opened=FighterProject.Open(savedProject))using(var savedRoot=NodeFactory.FromFile(null,opened.Animations.Single(a=>a.Id==form.FighterProject.Animations.Last().Id).WorkingPath))
                        Check(Walk(savedRoot).OfType<CHR0Node>().Any(c=>NativeAnimationOptimization.SourceContentKey(c)==savedContent)&&(!toMelee||opened.NativeTarget.Members.Length==(actor=="Mario"?191:217)),"Project reopen retains the fitted result and complete native target animation set: "+label);
                    using(var copied=NativeAnimationOptimization.CopySource(clip)){copied.ExportChecked(Path.Combine(dir,"fitted-master.chr0"));using(var reopened=NodeFactory.FromFile(null,Path.Combine(dir,"fitted-master.chr0")) as CHR0Node)Check(NativeAnimationOptimization.SourceContentKey(copied)==NativeAnimationOptimization.SourceContentKey(reopened),"Finished bridge output freshly reopens with exact native curves: "+label);}
                    editor.SetFrame(clip.FrameCount);editor.FrameFighterModel();await CaptureSimpleWorkspace(form,Path.Combine(dir,"shared-bridge.png"));
                    Check(modelHash==(string)Call(form,"NativeControlHash",target,".mdl0"),"Use, body editing and fitting preserve all binds and weights: "+label);
                }
            }
            finally{if(!form.IsDisposed)SimpleDisposeForm(form);}
        }
    }
}
