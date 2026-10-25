using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.Internal;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task TransitionFitting(string modelPath,string motionPath,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Transition acceptance requires the private desktop.");
        string modelHash=FighterProject.HashFile(modelPath),motionHash=FighterProject.HashFile(motionPath);
        var project=FighterProject.Create("Native boundary fitting");var form=new ModelForm();form.InitializeFighterProject(project);form.Show();Call(form,"ImportFighterModel",modelPath);Call(form,"ImportFighterAnimations",new[]{new[]{motionPath}});
        var editor=form.FighterEditor;var source=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().FirstOrDefault(c=>c.Name=="Wait1")??editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First();foreach(ListViewItem item in editor.leftPanel.listAnims.Items)item.Selected=ReferenceEquals(item.Tag,source);
        editor.SetFrame(1);editor.FrameFighterModel();await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        var model=(MDL0Node)editor.TargetModel;var setup=await Await<ControlReply>(Call(form,"LoadControls"));string boneName=setup.BoneNames[setup.Roles.Single(r=>r.Role=="Hips").Bone.Value];var bone=model._linker.BoneCache.Single(b=>b.Name==boneName);editor.SelectedBone=bone;await(Task)Call(form,"AttachControlPin",false);string pinId=editor.AnimationPins.Single().Id;
        // Imported live edit before Save: a normal native export may quantize
        // this retained value/tangent. Transition staging must use precise clones.
        var editedTrack=source.Children.OfType<CHR0EntryNode>().SingleOrDefault(e=>e.Name==boneName)??source.CreateEntry(boneName);
        editedTrack.SetKeyframe(4,7,13.1234567f,true,true)._tangent=.012345679f;source.SignalPropertyChange();
        string sourceKey=(string)Call(form,"SelectedFighterAnimationKey"),nativeHash=(string)Call(form,"NativeControlHash",model,".mdl0"),sourceHash=(string)Call(form,"NativeControlHash",source,".chr0");
        string snapshot=Path.Combine(output,"source-snapshot.chr0");source.Export(snapshot);FighterAsset referenceAsset;
        using(var reference=NodeFactory.FromFile(null,snapshot) as CHR0Node)
        {
            reference.Name="BoundaryReference";var entry=reference.Children.OfType<CHR0EntryNode>().SingleOrDefault(e=>e.Name==boneName)??reference.CreateEntry(boneName);
            for(int channel=3;channel<6;channel++){while(entry.KeyArrays[channel].KeyCount>0)entry.KeyArrays[channel].Remove(entry.KeyArrays[channel].Entries.First()._index);float value=channel==3?bone.BindState[3]+200:channel==4?180-bone.BindState[4]:bone.BindState[5]+180;entry.SetKeyframe(channel,0,value,true,true)._tangent=0;entry.SetKeyframe(channel,reference.FrameCount-1,value,true,true)._tangent=0;}entry.UseModelRotate=false;
            referenceAsset=project.CreateAnimation("BoundaryReference.chr0",reference.Export);project.Animations.Add(referenceAsset);
        }
        project.SelectedAnimation=sourceKey;project.Frame=1;Call(form,"RebuildFighterScene");await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));int count=project.Animations.Count;
        Exception failure=null;bool finished=false;int samples=Math.Min(6,(source.FrameCount-1)/3);if(samples<2)throw new Exception("Fixture needs at least seven source frames.");
        float NativeValue(CHR0Node clip,int channel,float time){var node=clip.Children.OfType<CHR0EntryNode>().SingleOrDefault(e=>e.Name==boneName);return node?.KeyArrays[channel].KeyCount>0?node.GetFrameValue(channel,time):bone.BindState[channel];}
        form.TransitionFitReady=async window=>
        {
            try
            {
                ((CheckBox)window.Controls.Find("transition_matchReferenceControls",true).Single()).Checked=false;
                Check(window.SourceView.Model!=model&&window.TargetView.Model!=model,"Transition previews own native model copies rather than posing the authoring rig");
                Check(window.SourceView.Viewport.CurrentViewport._renderAttrib._renderShaders==editor.ModelPanel.CurrentViewport._renderAttrib._renderShaders&&window.TargetView.Viewport.CurrentViewport._ambient==editor.ModelPanel.CurrentViewport._ambient,"Transition previews reuse the main native shader and lighting settings");
                var prepare=(Button)window.Controls.Find("prepareTransition",true).Single();var apply=(Button)window.Controls.Find("applyTransition",true).Single();var review=(CheckBox)window.Controls.Find("reviewTransition",true).Single();var report=(TextBox)window.Controls.Find("transitionReport",true).Single();
                foreach(string side in new[]{"entry","exit"}){var choice=(ComboBox)window.Controls.Find("transition_"+side,true).Single();choice.SelectedIndex=choice.Items.Cast<object>().Select((v,i)=>new{Value=v as TransitionClipChoice,Index=i}).Single(v=>v.Value?.Asset==referenceAsset).Index;((NumericUpDown)window.Controls.Find("transition_"+side+"Samples",true).Single()).Value=samples;}
                var mask=(CheckedListBox)window.Controls.Find("transitionBones",true).Single();for(int i=0;i<mask.Items.Count;i++)mask.SetItemChecked(i,Convert.ToString(mask.Items[i])==boneName);
                prepare.PerformClick();await Wait(()=>!window.Busy);Check(window.Candidate==null&&report.Text.Contains("Review that source/reference"),"An unreviewed native clip binding cannot silently become a fitted result");
                ((CheckBox)window.Controls.Find("transition_binding",true).Single()).Checked=true;var protectedText=(TextBox)window.Controls.Find("transitionProtected",true).Single();protectedText.Text="1-2";prepare.PerformClick();await Wait(()=>!window.Busy);Check(window.Candidate==null&&report.Text.Contains("protected"),"Native transition workflow rejects a protected immediate-reaction conflict");
                protectedText.Text=samples+"-"+(source.FrameCount-samples+1);prepare.PerformClick();Check(window.Busy,"Native transition preparation runs asynchronously with cancellation available");((Button)window.Controls.Find("cancelTransition",true).Single()).PerformClick();await Task.Delay(100);Check(!window.Busy&&window.Candidate==null&&project.Animations.Count==count,"Cancelling transition preparation discards late output without changing source or authoring animations");
                prepare.PerformClick();await Wait(()=>!window.Busy);File.WriteAllText(Path.Combine(output,"transition-report.txt"),report.Text);Check(window.Candidate!=null,"The real transition action prepares fixed-duration native curves through Core");
                Check(project.Animations.Count==count&&!apply.Enabled,"Preparation remains separate from authoring until explicit result review");
                Check(window.Candidate.FrameCount==source.FrameCount&&window.Candidate.Loop==source.Loop,"Native fit preserves total samples and source loop policy");
                Check(RecoveryRetainedKeysExact(source,window.Candidate,samples-1,source.FrameCount-samples),"Imported pre-Save high-precision body keys/tangents survive transition snapshots exactly");
                Check(Math.Abs(NativeValue(window.Candidate,3,0)-bone.BindState[3]-20)<.0001&&Math.Abs(NativeValue(window.Candidate,3,source.FrameCount-1)-bone.BindState[3]-20)<.0001,"Native fitted endpoints match the explicitly chosen same-rig reference pose");
                Check(new[]{0f,(float)(source.FrameCount-1)}.All(t=>LoopIndependentAngle(Enumerable.Range(3,3).Select(c=>(double)NativeValue(window.Candidate,c,t)).ToArray(),new[]{(double)bone.BindState[3]+20,bone.BindState[4],bone.BindState[5]})<=LoopOrientationTolerance),"Entry and exit choose a complete equivalent native XYZ pose instead of independent 180-degree axis flips");
                Check(Enumerable.Range(0,81).All(i=>Math.Abs(NativeValue(window.Candidate,3,samples-1+(source.FrameCount-2*samples+1)*(i/80f))-NativeValue(source,3,samples-1+(source.FrameCount-2*samples+1)*(i/80f)))<.0001),"Native fitted middle preserves the original curve at independent fractional times");
                var originalEntry=source.Children.OfType<CHR0EntryNode>().SingleOrDefault(e=>e.Name==boneName);var fittedEntry=window.Candidate.Children.OfType<CHR0EntryNode>().Single(e=>e.Name==boneName);
                bool unchanged=Enumerable.Range(0,9).Where(c=>c<3||c>=6).All(c=>
                {
                    var before=originalEntry==null?Enumerable.Empty<Tuple<int,float,float>>():originalEntry.KeyArrays[c].Entries.Select(k=>Tuple.Create(k._index,k._value,k._tangent));
                    return before.SequenceEqual(fittedEntry.KeyArrays[c].Entries.Select(k=>Tuple.Create(k._index,k._value,k._tangent)));
                });
                Check(unchanged,"Unselected native scale/translation keys and tangents stay exact");
                await CaptureWindow(window,Path.Combine(output,"transition-review.png"));((TrackBar)window.Controls.Find("transitionFrame",true).Single()).Value=source.FrameCount;await CaptureWindow(window,Path.Combine(output,"transition-exit.png"));
                var nativeReference=(CHR0Node)Call(form,"FighterRoot",referenceAsset);var key=nativeReference.Children.OfType<CHR0EntryNode>().Single(e=>e.Name==boneName).GetKeyframe(3,0);float old=key._value;key._value+=1;nativeReference.SignalPropertyChange();review.Checked=true;apply.PerformClick();Check(project.Animations.Count==count&&window.Candidate==null&&report.Text.Contains("reference changed"),"Changed native reference content rejects and discards stale Apply");key._value=old;nativeReference.SignalPropertyChange();
                prepare.PerformClick();await Wait(()=>!window.Busy);Check(window.Candidate!=null,"Restoring authentic reference content allows a newly prepared independent fit");review.Checked=true;apply.PerformClick();Check(project.Animations.Count==count+1,"Reviewed fit adds one real independent native animation");
            }
            catch(Exception error){failure=error;window.Close();}
            finally{finished=true;}
        };
        Call(form,"ShowTransitionFit");await Wait(()=>finished);if(failure!=null)throw failure;await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        var generated=project.Animations.Last();Check(generated.TransitionFit?.Algorithm=="fixed-duration-fk-hermite-2"&&generated.TransitionFit.Inputs.Length==3,"Independent result retains exact source/entry/exit native snapshots and Core recipe");generated.TransitionFit.Validate();
        string authentic=generated.TransitionFit.Inputs[0].Snapshot;byte[] changed=Convert.FromBase64String(authentic);changed[changed.Length-1]^=1;generated.TransitionFit.Inputs[0].Snapshot=Convert.ToBase64String(changed);bool rejected=false;try{generated.TransitionFit.Validate();}catch(InvalidDataException){rejected=true;}finally{generated.TransitionFit.Inputs[0].Snapshot=authentic;}Check(rejected,"Corrupted portable reference bytes reject independently of the UI's stale-input check");
        Check(editor.AnimationPins.Any(p=>p.Id==pinId&&p.BoneName==boneName),"Existing FK pin identity is copied into the independent animation's own control state");
        editor.Undo();Check(project.Animations.Count==count&&ReferenceEquals(editor.SelectedCHR0,source),"One native Undo removes the fitted animation and restores the exact source selection");await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));string history="Before redo: "+editor.CanRedo+" cursor "+editor._saveIndex+" undo "+editor._undoSaves.Count+" redo "+editor._redoSaves.Count;editor.Redo();history+="\r\nAfter: count "+project.Animations.Count+" name "+editor.SelectedCHR0?.Name+" cursor "+editor._saveIndex+" undo "+editor._undoSaves.Count+" redo "+editor._redoSaves.Count;File.WriteAllText(Path.Combine(output,"redo-diagnostic.txt"),history);Check(project.Animations.Count==count+1&&ReferenceEquals(editor.SelectedCHR0,Call(form,"FighterRoot",generated)),"Native Redo restores the prepared animation without recomputing fitting");await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        string fittedPath=Path.Combine(output,"boundary-fitted.chr0");editor.SelectedCHR0.Export(fittedPath);string save=Path.Combine(output,"boundary-fit.fpstudio");form.SaveFighterProject(save);
        using(var fresh=FighterProject.Open(save)){var native=fresh.Animations.Single(a=>a.Id==generated.Id);Check(native.TransitionFit.Inputs.All(i=>Convert.FromBase64String(i.Snapshot).Length>0)&&native.TransitionFit.Options.Contains("Protected"),"Fresh project reopen preserves portable native references and protected fitting options");native.TransitionFit.Validate();Check(fresh.AnimationControls.Any(c=>c.Json.Contains(pinId)),"Fresh fitted project retains independently scoped FK pin metadata");}
        finished=false;form.TransitionDetailsReady=async window=>{try{var text=((TextBox)window.Controls.Find("transitionDetailsReport",true).Single()).Text;Check(text.Contains("matches the saved initial fit")&&text.Contains("BoundaryReference"),"Native details identify the current fitted revision and its frozen reference snapshots");await CaptureWindow(window,Path.Combine(output,"transition-details.png"));}catch(Exception error){failure=error;}finally{window.Close();finished=true;}};Call(form,"ShowTransitionFitDetails");await Wait(()=>finished);if(failure!=null)throw failure;
        string frozenPath=Path.Combine(output,"exported-frozen-reference.chr0");var frozenInput=generated.TransitionFit.Inputs.Single(i=>i.Role=="entry");Call(form,"ExportFrozenTransitionInput",generated.TransitionFit,frozenInput,frozenPath);Check(string.Equals(FighterProject.HashFile(frozenPath),frozenInput.Sha256,StringComparison.OrdinalIgnoreCase),"Exporting a frozen reference writes its exact preserved CHR0 bytes to a new file");
        finished=false;form.TransitionFitReady=async window=>{try{var options=window.ReadOptions();Check(options.Entry==generated.TransitionFit.Inputs.Single(i=>i.Role=="entry").Entry&&options.EntrySamples==samples&&options.Bones.SequenceEqual(new[]{boneName}),"Reopening fitting restores exact reference, range and bone-mask settings");Check(window.Candidate==null&&!options.BindingReviewed,"Reopened settings prepare from the current animation without promoting an old receipt or bypassing live binding review");await CaptureWindow(window,Path.Combine(output,"transition-settings-reopened.png"));}catch(Exception error){failure=error;}finally{window.Close();finished=true;}};Call(form,"ShowTransitionFit");await Wait(()=>finished);if(failure!=null)throw failure;
        await CaptureWindow(form,Path.Combine(output,"transition-result-viewer.png"));
        await SlotWorkflow(form,project,output);form.SaveFighterProject(Path.Combine(output,"fit-slots.fpstudio"));using(var reopened=FighterProject.Open(Path.Combine(output,"fit-slots.fpstudio")))Check(reopened.AnimationSlots.Single().Protected,"Custom slot assignment and protection survive fresh fitted-project reopen");
        Check((string)Call(form,"NativeControlHash",model,".mdl0")==nativeHash&&(string)Call(form,"NativeControlHash",source,".chr0")==sourceHash,"Fitting and Undo/Redo leave native model, binds, weights and source animation bytes unchanged");
        Check(FighterProject.HashFile(modelPath)==modelHash&&FighterProject.HashFile(motionPath)==motionHash,"Supplied Mario/Snake or procedural model and motion containers remain immutable");
        ((AnimationControlClient)Field(form,"_controlEngine")).Dispose();project.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(editor);form.Dispose();
        // Exercise the full existing real native pointer/ring/curve workflow on
        // the fitted output itself, with exact fresh CHR0 and project reopening.
        await ControlGizmos(modelPath,fittedPath,output);
    }
}
