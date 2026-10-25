using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    static void BoundaryRangeTextFits(TransitionFitDialog window,string context)
    {
        var summary=SimpleControl<Label>(window,"transitionRanges");
        // Ask the native label to measure its complete text independently of
        // the dialog's TextRenderer-based fixed-height layout implementation.
        int required=summary.GetPreferredSize(new Size(summary.ClientSize.Width,int.MaxValue)).Height;
        Check(summary.ClientSize.Height>=required,"Complete inclusive range summary fits its native label without clipping: "+context+" (required "+required+", actual "+summary.ClientSize.Height+")");
    }
    static void BoundaryRangeSelectionLayout(ModelForm form,TransitionFitDialog window)
    {
        var entry=SimpleControl<ComboBox>(window,"transition_entry");var exit=SimpleControl<ComboBox>(window,"transition_exit");
        int entryIndex=entry.SelectedIndex,exitIndex=exit.SelectedIndex,text=WorkspaceAppearance.TextPercent;var originalSize=window.ClientSize;
        string originalOptions=new JavaScriptSerializer().Serialize(window.ReadOptions());
        try
        {
            foreach(int percent in new[]{100,200})
            {
                Call(form,"SetWorkspaceTextSize",percent);window.ApplyTextSize();window.ClientSize=new Size(980,720);window.PerformLayout();
                var fixedSize=window.ClientSize;
                void Selection(int start,int end,string name)
                {
                    entry.SelectedIndex=start;exit.SelectedIndex=end;
                    BoundaryRangeTextFits(window,percent+"% "+name+" without dialog resize");
                    Check(window.ClientSize==fixedSize,"Range selection relayout preserves dialog size: "+percent+"% "+name);
                }
                Selection(0,0,"original boundaries");Selection(entryIndex,0,"start only");Selection(entryIndex,exitIndex,"both boundaries");
                Selection(0,exitIndex,"end only");Selection(0,0,"cleared boundaries");Selection(entryIndex,exitIndex,"restored boundaries");
            }
        }
        finally
        {
            entry.SelectedIndex=entryIndex;exit.SelectedIndex=exitIndex;
            Call(form,"SetWorkspaceTextSize",text);window.ApplyTextSize();window.ClientSize=originalSize;window.PerformLayout();
        }
        Check(new JavaScriptSerializer().Serialize(window.ReadOptions())==originalOptions,"Range layout probes restore every fitting setting before preparation");
    }
    static async Task WorkspaceBoundaryFit(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Start/end fitting uses a private desktop only.");
        string Arg(string name)=>args[Array.IndexOf(args,name)+1];
        var inputs=new[]{args[0],args[1],Arg("--retarget-source"),Arg("--target-motion")}.Distinct().ToDictionary(p=>p,FighterProject.HashFile);
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",Path.Combine(output,"library"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_PREFERENCES",Path.Combine(output,"preferences"));
        using(var startup=MainForm.StartViewerApplication())
        {
            var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
            try
            {
                form.ClientSize=new Size(1600,950);await SimpleControlsReady(form);
                Call(form,"ImportFighterModel",args[0]);Call(form,"ImportFighterAnimations",new[]{new[]{Arg("--target-motion")}});await SimpleControlsReady(form);
                var editor=form.FighterEditor;var model=(MDL0Node)editor.TargetModel;
                var entryReference=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c=>c.Name=="Wait1");
                var exitReference=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c=>c.Name==Arg("--reference-clip"));
                Call(form,"BuildLibraryPane");var library=(AnimationLibrary)Field(form,"_library");
                var donorModel=BrawlLibrarySource.InspectModels(Arg("--retarget-source")).First(m=>!m.Shadow);
                library.Register(BrawlLibrarySource.Prepare(new LibraryRegistration{ModelPath=Arg("--retarget-source"),ModelKey=donorModel.ResourceKey,MotionPath=args[1],Character=donorModel.Name,GameVersion="Local fixture; game build unverified",ApproveAssociation=true}));
                var donor=library.Browse(Arg("--clip")).Single(i=>i.Clip.Name==Arg("--clip")&&i.Model!=null);
                var sourceAsset=await ComparisonRevision(form,library,donor.Clip.Id,null);var source=editor.SelectedCHR0;
                var setup=await Await<ControlReply>(Call(form,"LoadControls"));
                var roots=setup.Roles.Where(r=>r.Reviewed&&r.Role=="RootMotion").Select(r=>setup.BoneNames[r.Bone.Value]).ToArray();
                var contacts=NativeTransitionContacts.Resolve(model,sourceAsset,true);
                if(args.Contains("--moving-root-fixture"))
                {
                    var track=source.Children.OfType<CHR0EntryNode>().Single(t=>roots.Contains(t.Name));var channel=track.KeyArrays[8];while(channel.KeyCount>0)channel.Remove(channel.Entries.First()._index);
                    track.SetKeyframe(8,0,0,true,true)._tangent=1.25f/(source.FrameCount-1);track.SetKeyframe(8,source.FrameCount-1,1.25f,true,true)._tangent=1.25f/(source.FrameCount-1);source.SignalPropertyChange();editor.UpdateModel();
                }
                var before=SetNativeContent(source);var entryBefore=SetNativeContent(entryReference);var exitBefore=SetNativeContent(exitReference);
                string rootKeys=RecoveryKeys(source,roots);int entryJoin=4,exitSamples=int.Parse(Arg("--exit-samples")),exitJoin=source.FrameCount-exitSamples;
                Check(entryJoin<exitJoin,"Authentic original Next result leaves a nonempty middle between display frame 5 and the chosen recovery start");
                var count=form.FighterProject.Animations.Count;int mainFrame=editor.CurrentFrame,undo=editor._undoSaves.Count,redo=editor._redoSaves.Count;var selectedBone=editor.SelectedBone;
                CHR0Node fitted=null;Exception failure=null;bool finished=false;
                form.TransitionFitReady=async window=>
                {
                    try
                    {
                        Check(!SimpleControl<FlowLayoutPanel>(window,"transitionAdvancedFields").Visible,"Fitting opens with technical native options collapsed");
                        Check(window.ReadOptions().Components==7&&window.ReadOptions().MatchReferenceControls,"Simple defaults fit complete body transforms and reviewed controls while retaining attack travel");
                        var entry=SimpleControl<ComboBox>(window,"transition_entry");
                        entry.SelectedIndex=entry.Items.Cast<object>().Select((item,i)=>new{Item=item as TransitionClipChoice,Index=i}).Single(i=>ReferenceEquals(i.Item?.Clip,entryReference)).Index;
                        SimpleControl<NumericUpDown>(window,"transition_entrySamples").Value=5;
                        SimpleControl<Button>(window,"transitionBoth").PerformClick();
                        Check(window.ReadOptions().Entry==window.ReadOptions().Exit,"One visible action reuses the exact start pose at both ends");
                        var exit=SimpleControl<ComboBox>(window,"transition_exit");
                        exit.SelectedIndex=exit.Items.Cast<object>().Select((item,i)=>new{Item=item as TransitionClipChoice,Index=i}).Single(i=>ReferenceEquals(i.Item?.Clip,exitReference)).Index;
                        SimpleControl<NumericUpDown>(window,"transition_exitStart").Value=exitJoin+1;
                        Check(window.ReadOptions().EntrySamples==5&&window.ReadOptions().ExitSamples==exitSamples,"Simple displayed join frames map exactly to existing inclusive native sample settings");
                        var summary=SimpleControl<Label>(window,"transitionRanges");
                        Check(summary.Text.Contains("1–5")&&summary.Text.Contains((exitJoin+1)+"–"+source.FrameCount)&&summary.Text.Contains("join frames shared"),"Live summary exposes both transition ranges, unchanged middle and shared joins");
                        BoundaryRangeSelectionLayout(form,window);
                        SimpleControl<CheckBox>(window,"transition_boundaryContactRecovery").Checked=contacts.Length>0;
                        SimpleControl<CheckBox>(window,"transition_binding").Checked=true;
                        if(args.Contains("--workspace-foot-recovery")){SimpleControl<CheckBox>(window,"transition_preventFootDipping").Checked=true;SimpleControl<CheckBox>(window,"transition_floorReviewed").Checked=true;}
                        var options=new JavaScriptSerializer().Serialize(window.ReadOptions());
                        var advanced=SimpleControl<Button>(window,"transitionAdvanced");advanced.PerformClick();advanced.PerformClick();
                        Check(options==new JavaScriptSerializer().Serialize(window.ReadOptions()),"Opening or hiding Advanced does not change document policy or any fitting setting");
                        // No input injection: route F6 through the application itself.
                        entry.Focus();Call(window,"ProcessCmdKey",new Message(),Keys.F6);
                        Check(window.SourceView.Viewport.ContainsFocus,"Dialog F6 moves from setup to the original native source viewport");
                        Call(window,"ProcessCmdKey",new Message(),Keys.F6);Check(window.TargetView.Viewport.ContainsFocus,"Dialog F6 advances to the native target viewport");
                        Call(window,"ProcessCmdKey",new Message(),Keys.Shift|Keys.F6);Check(window.SourceView.Viewport.ContainsFocus,"Shift+F6 reverses dialog regions without changing preview data");
                        var prepare=SimpleControl<Button>(window,"prepareTransition");var report=SimpleControl<TextBox>(window,"transitionReport");
                        prepare.PerformClick();
                        Check(window.Busy&&!SimpleControl<Button>(window,"applyTransition").Enabled,"Preparing both windows keeps Apply guarded while work is busy");
                        var busyOptions=new JavaScriptSerializer().Serialize(window.ReadOptions());SimpleControl<Button>(window,"transitionBoth").PerformClick();
                        Check(new JavaScriptSerializer().Serialize(window.ReadOptions())==busyOptions,"The simple pose-copy action keeps its busy guard");
                        SimpleControl<Button>(window,"cancelTransition").PerformClick();await Wait(()=>!window.Busy);
                        Check(window.Candidate==null&&form.FighterProject.Animations.Count==count&&SetNativeContent(source)==before,"Cancelling both-window preparation publishes nothing and preserves the original animation");
                        prepare.PerformClick();await Wait(()=>!window.Busy);
                        if(args.Contains("--boundary-floor-rejection"))
                        {
                            Check(window.Candidate==null&&report.Text.Contains("20%"),"An authentic longer recovery exceeding the frozen lift budget rejects instead of changing source motion or loosening the gate");
                            Check(form.FighterProject.Animations.Count==count&&SetNativeContent(source)==before&&editor._undoSaves.Count==undo&&rootKeys==RecoveryKeys(source,roots),"Rejected two-ended floor preparation preserves document count, exact attack travel and native Undo");
                            await CaptureWindow(window,Path.Combine(output,"floor-bound-rejection.png"));window.Close();return;
                        }
                        Check(window.Candidate!=null,"Both ends of an authentic original Next result prepare through the native worker: "+report.Text);
                        fitted=window.Candidate;
                        Check(form.FighterProject.Animations.Count==count&&SetNativeContent(source)==before&&editor.CurrentFrame==mainFrame&&editor._undoSaves.Count==undo&&editor._redoSaves.Count==redo&&ReferenceEquals(editor.SelectedBone,selectedBone),"Preview preparation preserves the live document, playhead, bone selection and native Undo");
                        Check(rootKeys==RecoveryKeys(fitted,roots)&&RecoveryRetainedKeysExact(source,fitted,entryJoin,exitJoin),"Attack motion-position tracks and every original key/tangent/flag at and between both joins remain exact");
                        foreach(int at in new[]{0,source.FrameCount-1})
                        {
                            var reference=at==0?entryReference:exitReference;
                            Check(model._linker.BoneCache.All(b=>RecoverySamePose(RecoverySample(fitted,b,at),RecoverySample(roots.Contains(b.Name)?source:reference,b,roots.Contains(b.Name)?at:0))),"Fitted display frame "+(at+1)+" reaches only the authentic first target pose under the attack's own travel");
                        }
                        for(double time=entryJoin;time<=exitJoin;time+=.37)
                            Check(model._linker.BoneCache.All(b=>RecoverySamePose(RecoverySample(fitted,b,(float)time),RecoverySample(source,b,(float)time))),"Independent native middle sample remains unchanged at "+time.ToString("F2"));
                        // Changing either simple range discards stale work.
                        SimpleControl<NumericUpDown>(window,"transition_entrySamples").Value=6;
                        Check(window.Candidate==null&&!SimpleControl<Button>(window,"applyTransition").Enabled,"A simple join edit invalidates the prepared result and Apply");
                        SimpleControl<NumericUpDown>(window,"transition_entrySamples").Value=5;
                        if(args.Contains("--workspace-foot-recovery")){Check(!window.ReadOptions().FloorReviewed,"Editing either simple transition range clears floor-baseline review");SimpleControl<CheckBox>(window,"transition_floorReviewed").Checked=true;}
                        prepare.PerformClick();await Wait(()=>!window.Busy);Check(window.Candidate!=null,"Returning to the reviewed ranges prepares a fresh result");fitted=window.Candidate;
                        foreach(int percent in new[]{100,200})foreach(var size in new[]{new Size(980,720),new Size(1600,960)})
                        {
                            Call(form,"SetWorkspaceTextSize",percent);window.ApplyTextSize();window.ClientSize=size;window.PerformLayout();
                            BoundaryRangeTextFits(window,size.Width+" / "+percent+"% final prepared settings");
                            await CaptureWindow(window,Path.Combine(output,"simple-fit-"+size.Width+"-"+percent+".png"));
                            var panel=(FlowLayoutPanel)Field(window,"fields");var visible=panel.Controls.Cast<Control>().Where(c=>c.Visible).OrderBy(c=>c.Top).ToArray();
                            File.WriteAllText(Path.Combine(output,"layout-"+size.Width+"-"+percent+".json"),new JavaScriptSerializer().Serialize(visible.Select(c=>new{c.Name,c.Text,c.Bounds,c.MinimumSize,c.MaximumSize,c.AutoSize}).ToArray()));
                            Check(visible.Zip(visible.Skip(1),(a,b)=>a.Bottom<=b.Top).All(v=>v),"Simple controls do not overlap after text relayout at "+size.Width+" / "+percent+"%");
                            Check(visible.All(c=>c.Width+c.Margin.Horizontal<=panel.ClientSize.Width-panel.Padding.Horizontal),"Simple controls fit the scroll panel width at "+size.Width+" / "+percent+"%");
                            if(args.Contains("--workspace-foot-recovery"))
                            {
                                advanced.PerformClick();var floor=SimpleControl<CheckBox>(window,"transition_floorReviewed");floor.Focus();panel.ScrollControlIntoView(floor);panel.PerformLayout();
                                await CaptureWindow(window,Path.Combine(output,"advanced-fit-"+size.Width+"-"+percent+".png"));
                                Check(panel.ClientRectangle.Contains(panel.RectangleToClient(floor.RectangleToScreen(floor.ClientRectangle))),"Advanced floor review scrolls fully into view at "+size.Width+" / "+percent+"%");
                                advanced.PerformClick();panel.AutoScrollPosition=Point.Empty;
                            }
                            Check(SimpleControl<Button>(window,"prepareTransition").Visible&&SimpleControl<NumericUpDown>(window,"transition_entrySamples").Parent.Visible&&!SimpleControl<FlowLayoutPanel>(window,"transitionAdvancedFields").Visible,"Simple flow remains available at "+size.Width+" / "+percent+"% text");
                        }
                        Call(form,"SetWorkspaceTextSize",100);window.ApplyTextSize();window.ClientSize=new Size(1240,820);
                        Check(!SimpleControl<Button>(window,"applyTransition").Enabled,"Adding still requires explicit preview and limit review");
                        SimpleControl<CheckBox>(window,"reviewTransition").Checked=true;SimpleControl<Button>(window,"applyTransition").PerformClick();
                    }
                    catch(Exception error){failure=error;window.Close();}
                    finally{finished=true;}
                };
                Call(form,"ShowTransitionFit");await Wait(()=>finished);if(failure!=null)throw failure;
                if(args.Contains("--boundary-floor-rejection")){Check(inputs.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Rejected recovery leaves every original source container immutable");return;}
                await SimpleControlsReady(form);var generated=form.FighterProject.Animations.Single(a=>a.TransitionFit!=null);
                generated.TransitionFit.Validate();Check(form.FighterProject.Animations.Count==count+1&&ReferenceEquals(editor.SelectedCHR0,Call(form,"FighterRoot",generated)),"One native import adds and selects the independent two-ended fitted result");
                if(contacts.Length>0)
                {
                    Check(generated.TransitionFit.Algorithm==(args.Contains("--workspace-foot-recovery")?"contact-boundary-floor-1":"contact-boundary-recovery-1")&&generated.Retarget!=null&&NativeTransitionContacts.HasBakedSupport(generated),"Both-window receipt preserves original Next baked-support provenance instead of stripping contact ownership");
                    var serializer=new JavaScriptSerializer{MaxJsonLength=32000000};var altered=serializer.Deserialize<NativeTransitionFit>(serializer.Serialize(generated.TransitionFit));
                    var settings=serializer.Deserialize<TransitionFitOptions>(altered.Options);settings.EntrySamples++;altered.Options=serializer.Serialize(settings);bool rejected=false;try{altered.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Saved boundary receipts reject a changed join selection");
                    var broken=serializer.Deserialize<NativeTransitionFit>(serializer.Serialize(generated.TransitionFit));
                    var root=(System.Collections.Generic.IDictionary<string,object>)serializer.DeserializeObject(broken.Receipt);var receipt=(System.Collections.Generic.IDictionary<string,object>)root.Single(p=>string.Equals(p.Key,"Receipt",StringComparison.OrdinalIgnoreCase)).Value;
                    var windows=(object[])receipt.Single(p=>string.Equals(p.Key,"RecoveryWindows",StringComparison.OrdinalIgnoreCase)).Value;var first=(System.Collections.Generic.IDictionary<string,object>)windows[0];first[first.Keys.Single(k=>string.Equals(k,"Start",StringComparison.OrdinalIgnoreCase))]=.1;broken.Receipt=serializer.Serialize(root);
                    rejected=false;try{broken.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"Saved boundary receipts reject fractional frame ranges instead of rounding them to accepted joins");
                }
                if(args.Contains("--workspace-foot-recovery"))
                {
                    WorkspaceFootReceiptValidation(generated.TransitionFit,"entry");WorkspaceFootReceiptValidation(generated.TransitionFit,"exit");
                    using(var aligned=RecoveryReferenceUnderSourceRoots(model,source,entryReference,roots))RecoveryVerifyNativeFloor(model,source,editor.SelectedCHR0,aligned,setup,0,output,entryJoin,"entry");
                    using(var aligned=RecoveryReferenceUnderSourceRoots(model,source,exitReference,roots))RecoveryVerifyNativeFloor(model,source,editor.SelectedCHR0,aligned,setup,exitJoin,output,null,"exit");
                    editor.UpdateModel();
                }
                string fittedContent=SetNativeContent(editor.SelectedCHR0);
                editor.Undo();Check(form.FighterProject.Animations.Count==count&&ReferenceEquals(editor.SelectedCHR0,source),"One native Undo removes the independent fit and restores its exact source selection");await SimpleControlsReady(form);
                editor.Redo();await SimpleControlsReady(form);Check(SetNativeContent(editor.SelectedCHR0)==fittedContent,"Native Redo restores the exact fitted result without preparing or solving again");
                string export=Path.Combine(output,"two-ended.chr0");
                form.ExportSimpleWorkspaceResult(export);
                using(var reopened=NodeFactory.FromFile(null,export) as CHR0Node)Check(SetNativeContent(reopened)==fittedContent,"Verified precise native export before project Save reopens with every fitted key and tangent intact");
                form.SaveFighterProject(Path.Combine(output,"two-ended.fpstudio"));
                using(var reopened=FighterProject.Open(Path.Combine(output,"two-ended.fpstudio"))){var saved=reopened.Animations.Single(a=>a.Id==generated.Id);saved.TransitionFit.Validate();Check(saved.TransitionFit.Inputs.Count(i=>i.Role=="entry"||i.Role=="exit")==2,"Project reopen retains both exact frozen target references and current recipe validation");}
                Check(SetNativeContent(source)==before&&SetNativeContent(entryReference)==entryBefore&&SetNativeContent(exitReference)==exitBefore&&inputs.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Both-end fitting, Undo, export and reopen preserve authentic inputs, original Next result and target reference animations");
            }
            finally{SimpleDisposeForm(form);}
        }
    }
}
