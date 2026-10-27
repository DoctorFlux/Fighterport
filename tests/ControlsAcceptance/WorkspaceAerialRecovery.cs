using BrawlCrate;
using BrawlCrate.Studio;
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
    static async Task WorkspaceAerialRecovery(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Aerial fit verification requires the private desktop.");
        string Arg(string key)=>args[Array.IndexOf(args,key)+1];
        string folder=Path.Combine(output,"library");Directory.CreateDirectory(folder);File.Copy(Arg("--library-index"),Path.Combine(folder,"catalogue.xml"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",folder);Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_TEMP_ROOT",Path.Combine(output,"transient"));
        var library=new AnimationLibrary(folder);string catalogue=FighterProject.HashFile(library.IndexPath);
        var item=library.Browse(new LibraryQuery{Character="sonic",Text="AttackAirLw",Channel="CHR0 skeletal"}).Single(i=>i.Clip.Name=="AttackAirLw"&&i.Model.Name=="FitSonic00");
        using(var startup=MainForm.StartViewerApplication())
        {
            var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
            try
            {
                await SimpleControlsReady(form);
                var targetItem=library.Browse(new LibraryQuery{Character="mario",Text="Fall",Channel="CHR0 skeletal"}).Single(i=>i.Clip.Name=="Fall"&&i.Model.Name=="FitMario00");
                using(var original=library.Open(targetItem.Clip.Id))
                {
                    var project=ModelForm.PrepareLibraryTarget(original);
                    if(!MainForm.Instance.OpenLibraryFighterProject(project,out form)){project.Dispose();throw new Exception("Verified Mario target failed to open.");}
                }
                form.ClientSize=new Size(1700,950);await SimpleControlsReady(form);
                Call(form,"UseSharedSourceLibrary");await LibraryQuerySettled(form);
                var asset=await ComparisonRevision(form,library,item.Clip.Id,null);var editor=form.FighterEditor;var source=editor.SelectedCHR0;var model=(MDL0Node)editor.TargetModel;
                Check(source.FrameCount==44&&!source.Loop,"Authentic original Next Sonic AttackAirLw on Mario has the reported 44 non-looping frames");
                var fall=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c=>c.Name=="Fall"&&c.RootNode!=source.RootNode);
                var setup=await Await<ControlReply>(Call(form,"LoadControls"));var roots=setup.Roles.Where(r=>r.Reviewed&&r.Role=="RootMotion").Select(r=>setup.BoneNames[r.Bone.Value]).ToArray();
                Check(roots.Length>0,"Aerial fixture has reviewed authentic motion-position controls");
                string before=SetNativeContent(source),rootKeys=RecoveryKeys(source,roots),reference=SetNativeContent(fall),result=null;int count=form.FighterProject.Animations.Count;
                var witness=new GameLibraryDocument(form);bool finished=false;Exception failure=null;
                form.TransitionFitReady=async window=>
                {
                    try
                    {

                        foreach(string side in new[]{"entry","exit"}){var combo=SimpleControl<ComboBox>(window,"transition_"+side);combo.SelectedItem=combo.Items.OfType<TransitionClipChoice>().Single(c=>ReferenceEquals(c.Clip,fall));}
                        SimpleControl<NumericUpDown>(window,"transition_entrySamples").Value=5;SimpleControl<NumericUpDown>(window,"transition_exitStart").Value=35;
                        var adapt=SimpleControl<CheckBox>(window,"transition_boundaryContactRecovery");if(adapt.Enabled)adapt.Checked=true;
                        Check(!adapt.Enabled&&!adapt.Checked&&!window.ReadOptions().PreserveContacts,"An aerial without baked supports uses ordinary body fitting and cannot request nonexistent contact recovery");
                        Check(SimpleControl<CheckBox>(window,"transition_binding").Checked,"Exact library/rig evidence accepts the native Fall reference without advanced setup");
                        File.WriteAllText(Path.Combine(output,"reported-options.json"),new JavaScriptSerializer().Serialize(new{Options=window.ReadOptions(),HasSupport=NativeTransitionContacts.HasBakedSupport(asset),Contacts=NativeTransitionContacts.Resolve(model,asset,true),RecoveryEnabled=adapt.Enabled}));
                        await (Task)Call(window,"PrepareFit");await CaptureWindow(window,Path.Combine(output,"reported-fit.png"));
                        Check(window.Candidate!=null,"Sonic AttackAirLw to Mario Fall prepares at visible ranges 1-5 / 35-44 without hidden setup: "+SimpleControl<TextBox>(window,"transitionReport").Text);
                        var entryEnabled=SimpleControl<CheckBox>(window,"transition_entryEnabled");entryEnabled.Checked=false;
                        await (Task)Call(window,"PrepareFit");Check(window.Candidate!=null,"The same aerial also fits Fall with only Fit end enabled");
                        Check(model.AllBones.All(b=>RecoverySamePose(RecoverySample(window.Candidate,b,0),RecoverySample(source,b,0))),"End-only aerial fitting preserves the original first pose");
                        entryEnabled.Checked=true;SimpleControl<NumericUpDown>(window,"transition_entrySamples").Value=35;
                        await (Task)Call(window,"PrepareFit");Check(window.Candidate==null&&SimpleControl<TextBox>(window,"transitionReport").Text.Contains("overlap"),"Actually overlapping visible aerial ranges still reject without changing the live animation");
                        SimpleControl<NumericUpDown>(window,"transition_entrySamples").Value=5;await (Task)Call(window,"PrepareFit");Check(window.Candidate!=null,"Restoring the reported non-overlapping ranges prepares a fresh aerial fit");
                        var fitted=window.Candidate;result=SetNativeContent(fitted);
                        Check(window.ReadOptions().OptimizeRotationPaths,"Simple fitting enables the shared orientation-aware native path policy");
                        Check(RecoveryKeys(fitted,roots)==rootKeys,"Fall fitting preserves every attack motion-position key, tangent and flag");
                        foreach(int at in new[]{0,43})Check(model.AllBones.All(b=>RecoverySamePose(RecoverySample(fitted,b,at),RecoverySample(roots.Contains(b.Name)?source:fall,b,roots.Contains(b.Name)?at:0))),"Aerial boundary "+(at+1)+" reaches only Mario Fall first pose under the attack travel policy");
                        Check(RecoveryRetainedKeysExact(source,fitted,4,34),"Aerial fitting retains exact native keys and tangents at and between both joins");
                        for(double frame=4;frame<=34;frame+=.37)Check(model.AllBones.All(b=>RecoverySamePose(RecoverySample(fitted,b,(float)frame),RecoverySample(source,b,(float)frame))),"Aerial native middle remains unchanged at "+frame.ToString("F2"));
                        witness.CheckPreserved("aerial fit preparation");
                        foreach(int percent in new[]{100,175})foreach(var size in new[]{new Size(980,720),new Size(1600,960)})
                        {
                            Call(form,"SetWorkspaceTextSize",percent);window.ApplyTextSize();window.ClientSize=size;window.PerformLayout();
                            var panel=(FlowLayoutPanel)Field(window,"fields");panel.AutoScrollPosition=Point.Empty;
                            var visible=panel.Controls.Cast<Control>().Where(c=>c.Visible).OrderBy(c=>c.Top).ToArray();
                            Check(visible.Zip(visible.Skip(1),(a,b)=>a.Bottom<=b.Top).All(v=>v)&&visible.All(c=>c.Width+c.Margin.Horizontal<=panel.ClientSize.Width-panel.Padding.Horizontal),"Aerial fit controls fit without overlap at "+size.Width+" / "+percent+"%");
                            SimpleControl<TrackBar>(window,"transitionFrame").Value=44;await CaptureWindow(window,Path.Combine(output,"aerial-fall-"+size.Width+"-"+percent+".png"));
                            if(size.Width==1600&&percent==100)foreach(int sample in new[]{36,39,42}){SimpleControl<TrackBar>(window,"transitionFrame").Value=sample;await CaptureWindow(window,Path.Combine(output,"aerial-recovery-frame-"+sample+".png"));}
                        }
                        SimpleControl<Button>(window,"applyTransition").PerformClick();
                    }
                    catch(Exception error){failure=error;window.Close();}
                    finally{finished=true;}
                };
                Call(form,"ShowTransitionFit");await GameLibraryWait(()=>finished,"Aerial fitting did not complete",150);if(failure!=null)throw failure;await SimpleControlsReady(form);
                var generated=form.FighterProject.Animations.Single(a=>a.TransitionFit!=null);generated.TransitionFit.Validate();
                Check(generated.TransitionFit.Receipt.Contains("native-orientation-path-1"),"Applied animation records the exact versioned rotation-path policy");
                string savedOptions=generated.TransitionFit.Options;var altered=new JavaScriptSerializer().Deserialize<TransitionFitOptions>(savedOptions);altered.OptimizeRotationPaths=false;
                bool refused=false;try{generated.TransitionFit.Options=new JavaScriptSerializer().Serialize(altered);generated.TransitionFit.Validate();}catch(InvalidDataException){refused=true;}finally{generated.TransitionFit.Options=savedOptions;}
                Check(refused,"Saved rotation options cannot contradict their verified receipt");
                Check(form.FighterProject.Animations.Count==count&&ReferenceEquals(editor.SelectedCHR0,source)&&SetNativeContent(source)==result,"Aerial fit Apply updates the current animation with exactly the prepared native curves and valid recipe");
                editor.Undo();await SimpleControlsReady(form);Check(SetNativeContent(source)==before&&form.FighterProject.Animations.Count==count,"Native Undo restores the original aerial curves and recipe");
                editor.Redo();await SimpleControlsReady(form);Check(SetNativeContent(editor.SelectedCHR0)==result,"Native Redo restores the exact aerial fit");
                string export=Path.Combine(output,"aerial-fitted.chr0"),saved=Path.Combine(output,"aerial-fitted.fpstudio");form.ExportSimpleWorkspaceResult(export);form.SaveFighterProject(saved);
                using(var decoded=NodeFactory.FromFile(null,export) as CHR0Node)Check(SetNativeContent(decoded)==result,"Verified export before Save retains all aerial fitted native curves");
                using(var project=FighterProject.Open(saved))using(var root=NodeFactory.FromFile(null,project.Animations.Single(a=>a.Id==generated.Id).WorkingPath)){project.Animations.Single(a=>a.Id==generated.Id).TransitionFit.Validate();Check(SetNativeContent((CHR0Node)root)==result,"Aerial project reopen retains fitted curves and recipe");}
                Check(SetNativeContent(source)==result&&SetNativeContent(fall)==reference&&FighterProject.HashFile(library.IndexPath)==catalogue,"Aerial fitting keeps the applied attack, untouched reference and unchanged source catalogue");
            }
            finally{if(!form.IsDisposed)SimpleDisposeForm(form);}
        }
    }
}
