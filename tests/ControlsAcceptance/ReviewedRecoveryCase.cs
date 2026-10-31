using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    // Test-only continuation of the actual source-pane Use path. Airborne is
    // the existing baseline's explicit "no reviewed support context" setting;
    // these files do not qualify floor/ledge contact correction.
    static async Task ReviewedRecoveryCase(ModelForm form,CHR0Node current,string targetMotion,string output,bool ground=false)
    {
        var editor=form.FighterEditor;var model=(MDL0Node)editor.TargetModel;
        var json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=100};
        string motionHash=FighterProject.HashFile(targetMotion),modelHash=(string)Call(form,"NativeControlHash",model,".mdl0");
        var setup=await Await<ControlReply>(Call(form,"LoadControls"));
        string[] travel=setup.Roles.Where(r=>r.Reviewed&&r.Role=="RootMotion"&&r.Bone.HasValue).Select(r=>setup.BoneNames[r.Bone.Value]).ToArray();
        Check(travel.Length>0,"Recovery case has reviewed source-owned translation controls");
        using(var baseline=NativeAnimationOptimization.CopySource(current))
        {
            baseline.ExportChecked(Path.Combine(output,"01-retargeted.chr0"));
            Call(form,"ImportFighterAnimations",new[]{new[]{targetMotion}});await SimpleControlsReady(form);
            var reference=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c=>c.Name=="Wait1");
            string referenceContent=NativeAnimationOptimization.SourceContentKey(reference),before=SetNativeContent(current);
            editor.leftPanel.listAnims.SelectedItems.Clear();foreach(ListViewItem row in editor.leftPanel.listAnims.Items)row.Selected=ReferenceEquals(row.Tag,current);
            await SimpleControlsReady(form);Check(ReferenceEquals(editor.SelectedCHR0,current),"Fitting resolves the exact retargeted target member after opening its own Wait1");
            Exception failure=null;bool done=false;
            form.TransitionFitReady=async window=>
            {
                try
                {
                    SimpleControl<CheckBox>(window,"transition_entryEnabled").Checked=false;
                    SimpleControl<CheckBox>(window,"transition_exitEnabled").Checked=true;
                    var choice=SimpleControl<ComboBox>(window,"transition_exit");choice.SelectedItem=choice.Items.OfType<TransitionClipChoice>().Single(c=>ReferenceEquals(c.Clip,reference));
                    SimpleControl<CheckBox>(window,"transition_exitFrozen").Checked=true;SimpleControl<NumericUpDown>(window,"transition_exitFrame").Value=1;
                    SimpleControl<NumericUpDown>(window,"transition_exitSamples").Value=10;
                    SimpleControl<CheckBox>(window,"transition_boundaryContactRecovery").Checked=false;
                    SimpleControl<CheckBox>(window,"transition_matchReferenceControls").Checked=true;
                    SimpleControl<CheckBox>(window,"transition_binding").Checked=true;
                    SimpleControl<CheckBox>(window,"transition_existingTracksOnly").Checked=false;
                    await (Task)Call(window,"PrepareFit");
                    string report=SimpleControl<TextBox>(window,"transitionReport").Text;
                    File.WriteAllText(Path.Combine(output,"fit-report.txt"),report);File.WriteAllText(Path.Combine(output,"fit-options.json"),json.Serialize(window.ReadOptions()));
                    Check(window.Candidate!=null,"End-only fitting prepares the target's frozen Wait1 first frame: "+report);
                    var fitted=window.Candidate;int join=current.FrameCount-10;
                    Check(fitted.FrameCount==current.FrameCount&&RecoveryKeys(fitted,travel)==RecoveryKeys(current,travel),"End fitting preserves original duration and every attack-owned translation key/tangent/flag");
                    Check(RecoveryRetainedKeysExact(current,fitted,0,join),"End fitting preserves every authored beginning/middle key and paired side through the join");
                    Check(model.AllBones.All(b=>RecoverySamePose(RecoverySample(fitted,b,0),RecoverySample(current,b,0))),"Lying/hanging/action beginning stays the donor move's retargeted beginning");
                    Check(model.AllBones.All(b=>RecoverySamePose(RecoverySample(fitted,b,fitted.FrameCount-1),RecoverySample(travel.Contains(b.Name)?current:reference,b,travel.Contains(b.Name)?current.FrameCount-1:0))),"Final body/orientation reaches only the target's Wait1 first frame, with attack-owned translation");
                    using(var copy=NativeAnimationOptimization.CopySource(fitted))copy.ExportChecked(Path.Combine(output,"02-fitted.chr0"));
                    Check(SetNativeContent(current)==before&&NativeAnimationOptimization.SourceContentKey(reference)==referenceContent,"Independent fitting preview preserves editable retargeted master and immutable own Wait1");
                    SimpleControl<Button>(window,"applyTransition").PerformClick();
                }
                catch(Exception error){failure=error;window.Close();}finally{done=true;}
            };
            Call(form,"ShowTransitionFit");await Wait(()=>done);form.TransitionFitReady=null;if(failure!=null)throw failure;
            Check(ReferenceEquals(editor.SelectedCHR0,current),"End fit Apply updates the current target animation without replacing native ownership");
            string fittedContent=SetNativeContent(current);
            editor.Undo();await SimpleControlsReady(form);
            Check(ReferenceEquals(editor.SelectedCHR0,current)&&SetNativeContent(current)==before,"Own-Wait1 fitting Undo restores the exact retargeted animation in its original native owner");
            editor.Redo();await SimpleControlsReady(form);
            Check(ReferenceEquals(editor.SelectedCHR0,current)&&SetNativeContent(current)==fittedContent,"Own-Wait1 fitting Redo restores the exact fitted result without solving again");
            editor.SetFrame(current.FrameCount);editor.FrameFighterModel();
            await CaptureWindow(form,Path.Combine(output,"fitted-own-wait-endpoint.png"));
            Check(FighterProject.HashFile(targetMotion)==motionHash&&(string)Call(form,"NativeControlHash",model,".mdl0")==modelHash,"Recovery baseline preserves all original reference bytes, native binds and skin weights");
            form.FighterProject.Save(Path.Combine(output,"fitted-recovery.fpstudio"));await SimpleControlsReady(form);
            File.WriteAllText(Path.Combine(output,"recovery-baseline.json"),json.Serialize(new{Clip=current.Name,Frames=current.FrameCount,Reference="target Wait1 first frame only",EntryFitting=false,ExitSamples=10,
                RetargetedBytes=new FileInfo(Path.Combine(output,"01-retargeted.chr0")).Length,FittedBytes=new FileInfo(Path.Combine(output,"02-fitted.chr0")).Length,
                GroundMode=form.FighterProject.GroundMode.ToString(),ReviewedFloor=false,ReviewedLedge=false,ContactAcceptance=false,Note="Frozen own-Wait1 endpoint, original move beginning and attack-owned translation; absolute contact qualification remains separate."}));
            if(ground)await ReviewedGroundAfterFit(form,current,reference,output);
        }
    }
}
