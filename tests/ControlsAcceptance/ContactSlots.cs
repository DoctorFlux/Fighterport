using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static string ContactTrackKey(CHR0Node clip,string[] names)
    {
        int Bits(float value)=>BitConverter.ToInt32(BitConverter.GetBytes(value),0);
        return string.Join("\n",names.Select(name=>{var entry=clip.Children.OfType<CHR0EntryNode>().SingleOrDefault(e=>e.Name==name);return name+":"+(entry==null?"missing":entry.UseModelScale+"/"+entry.UseModelRotate+"/"+entry.UseModelTranslate+"/"+entry.ScaleCompensateApply+"/"+entry.ScaleCompensateParent+"/"+entry.ClassicScaleOff+"/"+string.Join(";",entry.KeyArrays.Select(a=>string.Join(",",a.Entries.Select(k=>k._index+":"+Bits(k._value)+":"+Bits(k._tangent))))));}));
    }
    static async Task ContactTransitionProject(string modelPath,string projectPath,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Contact transition acceptance requires the private desktop.");
        var project=FighterProject.Open(projectPath);var form=new ModelForm();form.InitializeFighterProject(project);form.Show();var editor=form.FighterEditor;
        await Wait(()=>editor.TargetModel!=null&&!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        var native=(ResourceNode)Call(form,"FighterRoot",project.Animations.Last(a=>a.Retarget?.Algorithm==HumanoidRetargeter.Algorithm&&a.TransitionFit==null));var chosen=Walk(native).OfType<CHR0Node>().Single();editor.leftPanel.listAnims.SelectedItems.Clear();foreach(ListViewItem row in editor.leftPanel.listAnims.Items)row.Selected=ReferenceEquals(row.Tag,chosen);editor.SelectedCHR0=chosen;editor.SetFrame(1);editor.FrameFighterModel();await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        string result=await ContactTransitions(form,project,output);
        ((AnimationControlClient)Field(form,"_controlEngine")).Dispose();project.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(editor);form.Dispose();GC.Collect();GC.WaitForPendingFinalizers();
        await ControlGizmos(modelPath,result,output);
    }
    static async Task<string> ContactTransitions(ModelForm form,FighterProject project,string output)
    {
        var editor=form.FighterEditor;var model=(MDL0Node)editor.TargetModel;var source=editor.SelectedCHR0;var asset=(FighterAsset)Call(form,"SelectedFighterAsset");
        var protectedBones=NativeTransitionContacts.Resolve(model,asset,true);Check(protectedBones.Length>0,"Original Next support regions expose complete native skin and ancestor dependencies");
        var free=model._linker.BoneCache.First(b=>!protectedBones.Contains(b.Name)&&source.Children.Any(c=>c.Name==b.Name));
        string sourceHash=(string)Call(form,"NativeControlHash",source,".chr0"),selection=(string)Call(form,"SelectedFighterAnimationKey");string snapshot=Path.Combine(output,"contact-source.chr0");source.Export(snapshot);FighterAsset referenceAsset;
        using(var reference=(CHR0Node)NodeFactory.FromFile(null,snapshot))
        {
            reference.Name="ContactReference";var track=reference.Children.OfType<CHR0EntryNode>().Single(e=>e.Name==free.Name);
            while(track.KeyArrays[3].KeyCount>0)track.KeyArrays[3].Remove(track.KeyArrays[3].Entries.First()._index);track.SetKeyframe(3,0,free.BindState[3]+12,true,true)._tangent=0;track.SetKeyframe(3,reference.FrameCount-1,free.BindState[3]+12,true,true)._tangent=0;track.UseModelRotate=false;
            referenceAsset=project.CreateAnimation("ContactReference.chr0",reference.Export);project.Animations.Add(referenceAsset);
        }
        project.SelectedAnimation=selection;project.Frame=1;Call(form,"RebuildTransitionScene");await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        Exception failure=null;bool finished=false;int count=project.Animations.Count;
        form.TransitionFitReady=async window=>
        {
            try
            {
                        SimpleControl<CheckBox>(window,"transition_saveAsNew").Checked=true;
                ((CheckBox)window.Controls.Find("transition_matchReferenceControls",true).Single()).Checked=false;
                ((CheckBox)window.Controls.Find("transition_boundaryContactRecovery",true).Single()).Checked=false;
                foreach(string side in new[]{"entry","exit"}){var choices=(ComboBox)window.Controls.Find("transition_"+side,true).Single();choices.SelectedIndex=choices.Items.Cast<object>().Select((v,i)=>new{Value=v as TransitionClipChoice,Index=i}).Single(v=>v.Value?.Asset==referenceAsset).Index;}
                ((CheckBox)window.Controls.Find("transition_binding",true).Single()).Checked=true;var protection=(CheckBox)window.Controls.Find("transition_contacts",true).Single();protection.Checked=true;
                var mask=(CheckedListBox)window.Controls.Find("transitionBones",true).Single();for(int i=0;i<mask.Items.Count;i++)mask.SetItemChecked(i,protectedBones.Contains(Convert.ToString(mask.Items[i]))||Convert.ToString(mask.Items[i])==free.Name);
                ((Button)window.Controls.Find("prepareTransition",true).Single()).PerformClick();await Wait(()=>!window.Busy);var report=(TextBox)window.Controls.Find("transitionReport",true).Single();File.WriteAllText(Path.Combine(output,"contact-transition-report.txt"),report.Text);
                Check(window.Candidate!=null&&window.PreparedReport.Contains("contact-trajectory-preserve-2"),"Real native contact-preserving preparation succeeds on the original Next result");
                Check(project.Animations.Count==count&&window.PreparedReport.Contains("excluded from endpoint matching"),"Contact exclusions are reported before explicit independent import");
                Check(ContactTrackKey(source,protectedBones)==ContactTrackKey(window.Candidate,protectedBones),"Every protected native key, tangent, duplicate side and transform flag remains exact");
                var changed=window.Candidate.Children.OfType<CHR0EntryNode>().Single(e=>e.Name==free.Name);Check(Math.Abs(changed.GetFrameValue(3,0)-free.BindState[3]-12)<.0001f,"Unprotected native branch still matches the requested contact-aware transition endpoint");
                var rig=BrawlRetargeter.Capture(model,asset.Retarget.Mapping.ToDictionary(m=>m.Role,m=>m.Target));
                var supports=rig.Rig.Surfaces.Values.Where(s=>s.Role.EndsWith(".foot",StringComparison.Ordinal)).ToArray();
                bool Equal(CHR0Node a,CHR0Node b)=>Enumerable.Range(0,17).All(i=>{float time=(source.FrameCount-1)*i/16f;var before=BrawlRetargeter.Sample(rig,a,time);var after=BrawlRetargeter.Sample(rig,b,time);return supports.All(s=>s.Points.All(p=>p.Evaluate(before)==p.Evaluate(after)));});
                Check(Equal(source,window.Candidate),"Deforming native foot support geometry stays identical at independent fractional times");
                ((FlowLayoutPanel)protection.Parent).ScrollControlIntoView(protection);await CaptureWindow(window,Path.Combine(output,"contact-transition-review.png"));((TrackBar)window.Controls.Find("transitionFrame",true).Single()).Value=source.FrameCount;await CaptureWindow(window,Path.Combine(output,"contact-transition-exit.png"));
                ((CheckBox)window.Controls.Find("reviewTransition",true).Single()).Checked=true;((Button)window.Controls.Find("applyTransition",true).Single()).PerformClick();Check(project.Animations.Count==count+1,"Reviewed contact-preserving fit publishes an independent native animation");
            }
            catch(Exception error){failure=error;window.Close();}finally{finished=true;}
        };
        Call(form,"ShowTransitionFit");await Wait(()=>finished);if(failure!=null)throw failure;await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        var generated=project.Animations.Last();Check(generated.TransitionFit.Algorithm=="contact-trajectory-preserve-2"&&generated.Retarget.Algorithm==HumanoidRetargeter.Algorithm,"Independent result retains both original Next provenance and explicit contact-protection receipt");
        editor.Undo();Check(project.Animations.Count==count,"Contact-preserving import is one native Undo transaction");editor.Redo();Check(project.Animations.Count==count+1,"Contact-preserving Redo restores the same prepared result without a solver");await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        string result=Path.Combine(output,"contact-fitted.chr0");form.ExportFighterResource(result);
        using(var reopened=(CHR0Node)NodeFactory.FromFile(null,result))Check(ContactTrackKey(source,protectedBones)==ContactTrackKey(reopened,protectedBones),"Fresh CHR0 decode retains the exact contact-path keys and native semantics");
        await SlotWorkflow(form,project,output);string saved=Path.Combine(output,"contact-slots.fpstudio");form.SaveFighterProject(saved);
        using(var reopened=FighterProject.Open(saved))Check(reopened.Animations.Last().TransitionFit.Algorithm=="contact-trajectory-preserve-2"&&reopened.AnimationSlots.Single().AnimationKey==project.AnimationSlots.Single().AnimationKey,"Fresh native project retains contact protection and exact slot assignment together");
        Check((string)Call(form,"NativeControlHash",source,".chr0")==sourceHash,"Contact-preserving fitting and slot operations leave the original Next result immutable");
        return result;
    }
    static async Task SlotWorkflow(ModelForm form,FighterProject project,string output)
    {
        string key=(string)Call(form,"SelectedFighterAnimationKey");Exception failure=null;bool finished=false;
        form.AnimationSlotsReady=async window=>
        {
            try
            {
                var name=(TextBox)window.Controls.Find("slotName",true).Single();name.Text="Grounded / Entry";((CheckBox)window.Controls.Find("slotRequired",true).Single()).Checked=true;((Button)window.Controls.Find("addSlot",true).Single()).PerformClick();
                Check(project.AnimationSlots.Count==1&&project.AnimationSlots[0].Required,"Native slot window creates an explicit required custom slot");
                var choice=(ComboBox)window.Controls.Find("slotClip",true).Single();choice.SelectedIndex=choice.Items.Cast<TransitionClipChoice>().Select((c,i)=>new{c,i}).Single(v=>v.c.Key==key).i;((Button)window.Controls.Find("assignSlot",true).Single()).PerformClick();
                Check(project.AnimationSlots[0].AnimationKey==key&&(string)Call(form,"AnimationSlotStatus",project.AnimationSlots[0])=="Assigned · exact native revision","Slot assignment uses the exact selected resource rather than a matching animation name");
                ((Button)window.Controls.Find("protectSlot",true).Single()).PerformClick();((Button)window.Controls.Find("clearSlot",true).Single()).PerformClick();
                Check(project.AnimationSlots[0].Protected&&project.AnimationSlots[0].AnimationKey==key&&((Label)window.Controls.Find("slotReport",true).Single()).Text.Contains("Unlock"),"Protected slots reject clearing until explicitly unlocked");
                await CaptureWindow(window,Path.Combine(output,"animation-slots.png"));
            }
            catch(Exception error){failure=error;}finally{window.Close();finished=true;}
        };
        Call(form,"ShowAnimationSlots");await Wait(()=>finished);if(failure!=null)throw failure;
        form.FighterEditor.Undo();Check(!project.AnimationSlots[0].Protected,"Slot protection has native Undo");form.FighterEditor.Undo();Check(project.AnimationSlots[0].AnimationKey==null,"Slot assignment Undo restores the unassigned required state");form.FighterEditor.Redo();form.FighterEditor.Redo();Check(project.AnimationSlots[0].Protected&&project.AnimationSlots[0].AnimationKey==key,"Slot Redo restores the exact assignment and protection");
        var clip=form.FighterEditor.SelectedCHR0;var entry=clip.Children.OfType<CHR0EntryNode>().First(e=>e.KeyArrays.Any(a=>a.KeyCount>0));var nativeKey=entry.KeyArrays.First(a=>a.KeyCount>0).Entries.First();float old=nativeKey._value;nativeKey._value+=.125f;clip.SignalPropertyChange();
        Check(((string)Call(form,"AnimationSlotStatus",project.AnimationSlots[0])).Contains("Animation changed"),"Later pin or curve edits invalidate the slot's reviewed content hash");nativeKey._value=old;clip.SignalPropertyChange();
        var missing=project.AnimationSlots[0].Copy();missing.AnimationKey="missing/exact/resource";Check((string)Call(form,"AnimationSlotStatus",missing)=="Missing animation","Missing slot resources remain explicit instead of selecting another clip");
        bool duplicate=false;try{NativeAnimationSlot.Validate(new[]{project.AnimationSlots[0],project.AnimationSlots[0].Copy()});}catch(InvalidDataException){duplicate=true;}Check(duplicate,"Duplicate native custom slots reject independently of the window");
    }
}
