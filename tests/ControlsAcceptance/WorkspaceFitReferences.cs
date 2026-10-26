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
                var editor=form.FighterEditor;var source=editor.SelectedCHR0;var model=(MDL0Node)editor.TargetModel;editor.SetFrame(3);await SimpleControlsReady(form);
                var viewerReference=NodeFactory.FromFile(null,args[1]);editor._openedFiles.Add(viewerReference);
                var witness=new GameLibraryDocument(form);string content=SetNativeContent(source),catalogue=FighterProject.HashFile(library.IndexPath);
                string[] protectedNames=((ControlReply)await Await<ControlReply>(Call(form,"LoadControls"))).Roles.Where(r=>r.Reviewed&&r.Role=="RootMotion").Select(r=>model.AllBones[r.Bone.Value].Name).ToArray();
                Check(protectedNames.Length>0,"Fixture has reviewed source motion-position controls");
                string controls=RecoveryKeys(source,protectedNames);int count=form.FighterProject.Animations.Count;
                bool finished=false;Exception failure=null;string resultContent=null;
                form.TransitionFitReady=async window=>
                {
                    try
                    {
                        Check(window.Candidate==null&&FitHasPose(window.TargetView.Model)&&Flow39Pose(window.TargetView.Model).SequenceEqual(Flow39Pose(window.SourceView.Model)),"Opening fitting displays the current same-character animation on both native views, not a false T-pose result");
                        Check(SimpleControl<CheckBox>(window,"transition_binding").Checked,"Exact recorded native source/rig evidence verifies same-character fitting without the generic review blocker");
                        var protectedText=SimpleControl<TextBox>(window,"transitionProtected");protectedText.Text="10-";
                        Check(SimpleControl<CheckBox>(window,"transition_binding").Checked&&window.Candidate==null,"Typing an incomplete advanced range does not invoke options validation or lose verified bindings");protectedText.Text="";
                        window.RestoreOptions(window.ReadOptions());
                        Check(SimpleControl<CheckBox>(window,"transition_binding").Checked,"Restoring options recomputes verified native binding evidence");

                        await (Task)Call(window,"PrepareFit");
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
                        SimpleControl<CheckBox>(window,"reviewTransition").Checked=true;SimpleControl<Button>(window,"applyTransition").PerformClick();
                    }
                    catch(Exception error){failure=error;window.Close();}
                    finally{finished=true;}
                };
                Call(form,"ShowTransitionFit");await GameLibraryWait(()=>finished,"Fitting reference fixture did not finish.",120);if(failure!=null)throw failure;await SimpleControlsReady(form);
                Check(form.FighterProject.Animations.Count==count+1&&SetNativeContent(editor.SelectedCHR0)==resultContent&&SetNativeContent(source)==content,"Apply adds the exact reviewed native fit while preserving the original multi-animation PAC");
                Check(form.FighterProject.Animations.Last().TransitionFit.Inputs.Single(i=>i.Role=="exit").Entry.StartsWith("library:"),"Fit receipt freezes the exact external library identity and native snapshot");
                string saved=Path.Combine(output,"fitted.fpstudio"),export=Path.Combine(output,"fitted.chr0");form.ExportSimpleWorkspaceResult(export);form.SaveFighterProject(saved);
                using(var project=FighterProject.Open(saved))using(var root=NodeFactory.FromFile(null,project.Animations.Last().WorkingPath))Check(SetNativeContent((CHR0Node)root)==resultContent,"Library-backed fitted curves survive project Save and fresh native reopen");
                using(var decoded=NodeFactory.FromFile(null,export) as CHR0Node)Check(SetNativeContent(decoded)==resultContent,"Verified export before project Save preserves the actual fitted animation");
                Check(FighterProject.HashFile(library.IndexPath)==catalogue,"Fitting reference loading never registers or modifies the shared catalogue");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"fit-applied-main.png"));
            }
            finally{if(!form.IsDisposed)SimpleDisposeForm(form);}
        }
    }
}
