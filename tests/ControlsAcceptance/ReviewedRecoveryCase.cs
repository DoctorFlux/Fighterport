using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
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
    static async Task ReviewedRecoveryCase(ModelForm form,CHR0Node current,string targetMotion,string output,bool ground=false,string referenceName="Wait1",string referenceFixture=null)
    {
        var editor=form.FighterEditor;var model=(MDL0Node)editor.TargetModel;
        var json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024,RecursionLimit=100};
        string motionHash=FighterProject.HashFile(targetMotion),modelHash=(string)Call(form,"NativeControlHash",model,".mdl0");
        string expectedReference=null;double? reviewedFloor=null;
        if(referenceFixture!=null)
        {
            Check(new FileInfo(referenceFixture).Length<=16384,"Reference fixture is bounded before reading");
            var pin=json.Deserialize<AuthenticFloatDenseFixture>(File.ReadAllText(referenceFixture));
            reviewedFloor=pin.FittingFloorHeight;
            Check(pin.Schema=="fighterport-authentic-export-fixture-1"&&string.Equals(pin.SourceClipSha256,motionHash,StringComparison.OrdinalIgnoreCase)&&pin.SourceClipName==referenceName&&
                string.Equals(pin.SourceRigFingerprint,BrawlRetargeter.SkeletonFingerprint(model),StringComparison.OrdinalIgnoreCase),"Target reference is pinned to this exact target rig, native motion file and selected reference identity");
            string Member(ResourceNode n)=>n.Parent==null?"root":Member(n.Parent)+"/"+n.Index;
            using(var owner=NodeFactory.FromFile(null,targetMotion))
            {
                var exact=Walk(owner).OfType<CHR0Node>().Single(c=>c.Name==referenceName&&Member(c)==pin.SourceNativeMember);
                Check(exact.FrameCount==pin.SourceFrames&&exact.Loop==pin.SourceLoop&&exact.Version==pin.SourceVersion,"Exact target reference member retains its frozen native duration/version/loop state");
                expectedReference=NativeAnimationOptimization.SourceContentKey(exact);
            }
            if(pin.FittingRoles!=null)await ReviewedFittingRoleFixture(form,model,pin,output);
        }
        var setup=await Await<ControlReply>(Call(form,"LoadControls"));
        File.WriteAllText(Path.Combine(output,"fitting-control-setup.json"),json.Serialize(setup));
        string[] travel=setup.Roles.Where(r=>r.Reviewed&&r.Role=="RootMotion"&&r.Bone.HasValue).Select(r=>setup.BoneNames[r.Bone.Value]).ToArray();
        Check(travel.Length>0,"Recovery case has reviewed source-owned translation controls");
        using(var baseline=NativeAnimationOptimization.CopySource(current))
        {
            baseline.ExportChecked(Path.Combine(output,"01-retargeted.chr0"));
            Call(form,"ImportFighterAnimations",new[]{new[]{targetMotion}});await SimpleControlsReady(form);
            var reference=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c=>c.Name==referenceName);
            Check(expectedReference==null||NativeAnimationOptimization.SourceContentKey(reference)==expectedReference,"Imported target reference matches the independently pinned native member");
            using(var copy=NativeAnimationOptimization.CopySource(reference))copy.ExportChecked(Path.Combine(output,"frozen-reference.chr0"));
            string referenceContent=NativeAnimationOptimization.SourceContentKey(reference),before=SetNativeContent(current);
            editor.leftPanel.listAnims.SelectedItems.Clear();foreach(ListViewItem row in editor.leftPanel.listAnims.Items)row.Selected=ReferenceEquals(row.Tag,current);
            await SimpleControlsReady(form);Check(ReferenceEquals(editor.SelectedCHR0,current),"Fitting resolves the exact retargeted target member after opening its own reference");
            Exception failure=null;bool done=false;
            form.TransitionFitReady=async window=>
            {
                try
                {
                    File.WriteAllText(Path.Combine(output,"fit-dialog-defaults.json"),json.Serialize(window.ReadOptions()));
                    SimpleControl<CheckBox>(window,"transition_entryEnabled").Checked=false;
                    SimpleControl<CheckBox>(window,"transition_exitEnabled").Checked=true;
                    var choice=SimpleControl<ComboBox>(window,"transition_exit");choice.SelectedItem=choice.Items.OfType<TransitionClipChoice>().Single(c=>ReferenceEquals(c.Clip,reference));
                    SimpleControl<CheckBox>(window,"transition_exitFrozen").Checked=true;SimpleControl<NumericUpDown>(window,"transition_exitFrame").Value=1;
                    SimpleControl<NumericUpDown>(window,"transition_exitSamples").Value=10;
                    if(referenceFixture==null)SimpleControl<CheckBox>(window,"transition_boundaryContactRecovery").Checked=false;
                    SimpleControl<CheckBox>(window,"transition_matchReferenceControls").Checked=true;
                    SimpleControl<CheckBox>(window,"transition_binding").Checked=true;
                    SimpleControl<CheckBox>(window,"transition_existingTracksOnly").Checked=false;
                    if(reviewedFloor.HasValue)
                    {
                        SimpleControl<CheckBox>(window,"transition_preventFootDipping").Checked=true;
                        SimpleControl<NumericUpDown>(window,"transition_floorHeight").Value=(decimal)reviewedFloor.Value;
                        SimpleControl<CheckBox>(window,"transition_floorReviewed").Checked=true;
                    }
                    await (Task)Call(window,"PrepareFit");
                    string report=SimpleControl<TextBox>(window,"transitionReport").Text;
                    File.WriteAllText(Path.Combine(output,"fit-report.txt"),report);File.WriteAllText(Path.Combine(output,"fit-options.json"),json.Serialize(window.ReadOptions()));
                    Check(window.Candidate!=null,"End-only fitting prepares the target's frozen "+referenceName+" first frame: "+report);
                    var fitted=window.Candidate;int join=current.FrameCount-10;
                    Check(fitted.FrameCount==current.FrameCount&&RecoveryKeys(fitted,travel)==RecoveryKeys(current,travel),"End fitting preserves original duration and every attack-owned translation key/tangent/flag");
                    Check(RecoveryRetainedKeysExact(current,fitted,0,join),"End fitting preserves every authored beginning/middle key and paired side through the join");
                    Check(model.AllBones.All(b=>RecoverySamePose(RecoverySample(fitted,b,0),RecoverySample(current,b,0))),"Lying/hanging/action beginning stays the donor move's retargeted beginning");
                    Check(model.AllBones.All(b=>RecoverySamePose(RecoverySample(fitted,b,fitted.FrameCount-1),RecoverySample(travel.Contains(b.Name)?current:reference,b,travel.Contains(b.Name)?current.FrameCount-1:0))),"Final body/orientation reaches only the target's reference first frame, with attack-owned translation");
                    using(var copy=NativeAnimationOptimization.CopySource(fitted))copy.ExportChecked(Path.Combine(output,"02-fitted.chr0"));
                    ReviewedFitCorpusOracle(model,current,reference,fitted,travel,join,Path.Combine(output,"02-fitted.chr0"),output);
                    if(reviewedFloor.HasValue)
                    {
                        using(var decoded=NodeFactory.FromFile(null,Path.Combine(output,"02-fitted.chr0")) as CHR0Node)
                        using(var aligned=RecoveryReferenceUnderSourceRoots(model,current,reference,travel))
                            RecoveryVerifyNativeFloor(model,current,decoded,aligned,setup,join,output,floorHeight:reviewedFloor.Value);
                    }
                    Check(SetNativeContent(current)==before&&NativeAnimationOptimization.SourceContentKey(reference)==referenceContent,"Independent fitting preview preserves editable retargeted master and immutable own reference");
                    SimpleControl<Button>(window,"applyTransition").PerformClick();
                }
                catch(Exception error){failure=error;window.Close();}finally{done=true;}
            };
            Call(form,"ShowTransitionFit");await Wait(()=>done);form.TransitionFitReady=null;if(failure!=null)throw failure;
            Check(ReferenceEquals(editor.SelectedCHR0,current),"End fit Apply updates the current target animation without replacing native ownership");
            string fittedContent=SetNativeContent(current);
            editor.Undo();await SimpleControlsReady(form);
            Check(ReferenceEquals(editor.SelectedCHR0,current)&&SetNativeContent(current)==before,"Own-reference fitting Undo restores the exact retargeted animation in its original native owner");
            editor.Redo();await SimpleControlsReady(form);
            Check(ReferenceEquals(editor.SelectedCHR0,current)&&SetNativeContent(current)==fittedContent,"Own-reference fitting Redo restores the exact fitted result without solving again");
            editor.SetFrame(current.FrameCount);editor.FrameFighterModel();
            await CaptureWindow(form,Path.Combine(output,"fitted-own-reference-endpoint.png"));
            Check(FighterProject.HashFile(targetMotion)==motionHash&&(string)Call(form,"NativeControlHash",model,".mdl0")==modelHash,"Recovery baseline preserves all original reference bytes, native binds and skin weights");
            form.FighterProject.Save(Path.Combine(output,"fitted-recovery.fpstudio"));await SimpleControlsReady(form);
            File.WriteAllText(Path.Combine(output,"recovery-baseline.json"),json.Serialize(new{Clip=current.Name,Frames=current.FrameCount,Reference=referenceName,ReferenceFrame=1,ReferenceContent=referenceContent,ReferenceFixture=referenceFixture,EntryFitting=false,ExitSamples=10,
                TravelControls=travel,ReviewedTravelBoneIndices=setup.Roles.Where(r=>r.Reviewed&&r.Role=="RootMotion"&&r.Bone.HasValue).Select(r=>r.Bone.Value).ToArray(),
                RetargetedBytes=new FileInfo(Path.Combine(output,"01-retargeted.chr0")).Length,FittedBytes=new FileInfo(Path.Combine(output,"02-fitted.chr0")).Length,
                GroundMode=form.FighterProject.GroundMode.ToString(),ReviewedFloor=false,ReviewedLedge=false,ContactAcceptance=false,Note="Frozen own-reference frame-1 endpoint, original move beginning and attack-owned translation; absolute contact qualification remains separate."}));
            if(ground)await ReviewedGroundAfterFit(form,current,reference,output);
        }
    }
}
