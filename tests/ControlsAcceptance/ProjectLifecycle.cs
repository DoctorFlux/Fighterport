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
    static string LifecycleContent(CHR0Node clip)=>new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(new{clip.Name,clip.FrameCount,clip.Loop,Entries=clip.Children.OfType<CHR0EntryNode>().OrderBy(e=>e.Name).Select(e=>new{e.Name,e.UseModelScale,e.UseModelRotate,e.UseModelTranslate,e.ScaleCompensateApply,e.ScaleCompensateParent,e.ClassicScaleOff,Keys=e.KeyArrays.Select(a=>a.Entries.Select(k=>new{k._index,k._value,k._tangent}).ToArray()).ToArray()}).ToArray()});
    static async Task ProjectLifecycle(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Project lifecycle acceptance requires the private desktop.");
        var originals=new[]{args[0],args[1]}.Distinct().ToDictionary(p=>p,FighterProject.HashFile);
        using(var startup=MainForm.StartViewerApplication())
        {
            var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
            SimpleToolbarItem(form,"workspaceNewProject").PerformClick();
            Check(form.IsDisposed,"New project immediately replaces an untouched clean project");
            form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
            form.WindowState=FormWindowState.Normal;form.ClientSize=new Size(1600,950);
            Call(form,"ImportFighterModel",args[0]);Call(form,"ImportFighterAnimations",new[]{new[]{args[1]}});
            var editor=form.FighterEditor;editor.SelectedCHR0=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First(c=>c.Name=="Wait1");editor.SetFrame(3);
            await SimpleControlsReady(form);
            var setup=await Await<ControlReply>(Call(form,"LoadControls"));
            var model=(MDL0Node)editor.TargetModel;var clip=editor.SelectedCHR0;
            int body=setup.Roles.Single(r=>r.Role=="Hips").Bone.Value;
            string bodyName=setup.BoneNames[body];
            string originalModel=(string)Call(form,"NativeControlHash",model,".mdl0");
            Check(setup.ProtectedMotionBones.Length>0 && !setup.ProtectedMotionBones.Contains(body),"Reviewed body remains editable below separate protected native controls");
            foreach(int control in setup.ProtectedMotionBones)
            {
                string name=setup.BoneNames[control];
                Check(!editor.AnimationHandles.Any(h=>h.BoneName==name),"Protected control has no manual FK handle: "+name);
                editor.SelectedBone=model._linker.BoneCache.Single(b=>b.Name==name);
                string before=LifecycleContent(clip);
                var box=editor.chr0Editor._transBoxes[3];box.Value+=7;editor.chr0Editor.BoxChangedCreateUndo(box,EventArgs.Empty);
                Check(LifecycleContent(clip)==before,"Native transform fields cannot synthesize protected control keys: "+name);
                bool rejected=false;
                try{await ((AnimationControlClient)Field(form,"_controlEngine")).Send(new{Operation="pin",Bone=control,Frame=2});}
                catch(InvalidOperationException){rejected=true;}
                Check(rejected,"Worker independently rejects protected control posing: "+name);
            }
            editor.SelectedBone=model._linker.BoneCache.Single(b=>b.Name==bodyName);
            await (Task)Call(form,"AttachControlPin",false);
            var pin=editor.AnimationPins.Single(p=>p.BoneName==bodyName);
            string beforeBody=LifecycleContent(clip);
            Call(form,"BeginControlGesture",pin);await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));
            var pose=setup.Pose[body];
            Call(form,"MoveControlGesture",new[]{pin.Position[0]+.25f,pin.Position[1],pin.Position[2],pin.Rotation[0],pin.Rotation[1],pin.Rotation[2],pin.Rotation[3],pose[0],pose[1],pose[2]});
            Call(form,"EndControlGesture");await Wait(()=>Field(form,"_pinGesture")==null);
            string changed=LifecycleContent(clip);
            Check(changed!=beforeBody,"Body control edit commits native animation keys");
            Check(setup.ProtectedMotionBones.All(i=>editor.CanEditAnimationBone(setup.BoneNames[i])==false),"Protected controls stay guarded after native gesture publication");
            editor.Undo();await SimpleControlsReady(form);
            Check(LifecycleContent(clip)==beforeBody,"Body edit Undo restores exact native channels");
            editor.Redo();await SimpleControlsReady(form);
            Check(LifecycleContent(clip)==changed,"Body edit Redo restores exact native channels");
            editor.SetFrame(3);await SimpleControlsReady(form);
            editor.FrameFighterModel();await CaptureSimpleWorkspace(form,Path.Combine(output,"lifecycle-edited-body.png"));

            await Wait(()=>SimpleToolbarItem(form,"workspaceNewProject").Enabled);
            bool prompted=false;
            using(FlowModal("unsavedProjectCancel",dialog=>{prompted=true;CaptureDialog(dialog,output,"lifecycle-keep-editing.png");SimpleControl<Button>(dialog,"unsavedProjectCancel").PerformClick();}))
                SimpleToolbarItem(form,"workspaceNewProject").PerformClick();
            Console.WriteLine("TRACE after New prompted="+prompted+" disposed="+form.IsDisposed+" sameEditor="+ReferenceEquals(form.FighterEditor,editor)+" sameClip="+ReferenceEquals(editor.SelectedCHR0,clip)+" frame="+editor.CurrentFrame+" sameContent="+(LifecycleContent(clip)==changed));
            Check(prompted&&!form.IsDisposed&&ReferenceEquals(form.FighterEditor,editor)&&ReferenceEquals(editor.SelectedCHR0,clip)&&editor.CurrentFrame==3&&
                LifecycleContent(clip)==changed,"Keep editing cancels New and preserves document, frame and native edits");

            using(FlowModal("unsavedProjectSave",dialog=>
            {
                using(FlowModal("workspaceFilePath",path=>SimpleControl<Button>(path,"workspaceFileCancel").PerformClick()))
                    SimpleControl<Button>(dialog,"unsavedProjectSave").PerformClick();
                Check(dialog.Visible&&SimpleControl<Label>(dialog,"unsavedProjectStatus").Text.Contains("cancelled"),"Cancelled Save leaves the unsaved prompt and old project open");
                SimpleControl<Button>(dialog,"unsavedProjectCancel").PerformClick();
            })) SimpleToolbarItem(form,"workspaceNewProject").PerformClick();
            Check(!form.IsDisposed&&LifecycleContent(clip)==changed,"Cancelled Save preserves native changes");

            string saved=Path.Combine(output,"lifecycle.fpstudio");
            using(FlowModal("workspaceFilePath",dialog=>
            {
                SimpleControl<TextBox>(dialog,"workspaceFilePath").Text=Path.Combine(output,"wrong-extension.txt");
                Check(!SimpleControl<Button>(dialog,"workspaceFileAccept").Enabled,"Save dialog refuses an invalid project filename without closing");
                CaptureDialog(dialog,output,"lifecycle-save-path.png");
                FlowAcceptFile(dialog,saved);
            })) Check((bool)Call(form,"SaveFighterFromUi",false),"Actual untitled Save completes through the application-owned path dialog");
            // A real sharing violation exercises failed persistence without
            // substituting a fake save callback or altering original inputs.
            Call(form,"MarkFighterChanged");
            using(var locked=new FileStream(saved,FileMode.Open,FileAccess.Read,FileShare.None))
            using(FlowModal("unsavedProjectSave",dialog=>
            {
                SimpleControl<Button>(dialog,"unsavedProjectSave").PerformClick();
                Check(dialog.Visible&&SimpleControl<Label>(dialog,"unsavedProjectStatus").Text.StartsWith("Could not save"),"Failed project save keeps the decision open with its error");
                CaptureDialog(dialog,output,"lifecycle-save-failed.png");SimpleControl<Button>(dialog,"unsavedProjectCancel").PerformClick();
            })) SimpleToolbarItem(form,"workspaceNewProject").PerformClick();
            Check(!form.IsDisposed&&(bool)Field(form,"_fighterDirty")&&LifecycleContent(clip)==changed,"Failed save retains dirty current native document");
            editor.Undo();await SimpleControlsReady(form);
            Check(LifecycleContent(clip)==beforeBody,"Undo remains functional after cancelled and failed replacements");
            editor.Redo();await SimpleControlsReady(form);
            Check(LifecycleContent(clip)==changed,"Redo remains functional after cancelled and failed replacements");
            editor.SetFrame(3);await SimpleControlsReady(form);
            Check((string)Call(form,"NativeControlHash",model,".mdl0")==originalModel,"Body edits and failed/cancelled saves preserve authentic model binds and hierarchy");
            using(FlowModal("unsavedProjectSave",dialog=>SimpleControl<Button>(dialog,"unsavedProjectSave").PerformClick()))
                SimpleToolbarItem(form,"workspaceNewProject").PerformClick();
            Check(form.IsDisposed,"Successful Save finishes before New replaces the old viewer");
            form=(ModelForm)Field(MainForm.Instance,"_fighterForm");await Task.Delay(150);
            Check(form.FighterEditor.TargetModel==null&&!(bool)Field(form,"_fighterDirty"),"New opens a clean empty project");
            Check(MainForm.Instance.OpenFighterProject(saved,form),"Actual project open restores the saved native document");
            form=(ModelForm)Field(MainForm.Instance,"_fighterForm");await Wait(()=>form.FighterEditor.TargetModel!=null);await SimpleControlsReady(form);
            Check(form.FighterEditor.CurrentFrame==3&&LifecycleContent(form.FighterEditor.SelectedCHR0)==changed,"Project reopen preserves edited animation and timeline frame");
            form.WindowState=FormWindowState.Normal;form.ClientSize=new Size(1600,950);form.FighterEditor.FrameFighterModel();
            await CaptureSimpleWorkspace(form,Path.Combine(output,"lifecycle-reopened.png"));
            Call(form,"MarkFighterChanged");
            using(FlowModal("unsavedProjectCancel",dialog=>SimpleControl<Button>(dialog,"unsavedProjectCancel").PerformClick()))
                Check(!MainForm.Instance.OpenFighterProject(saved,form),"Keep editing also cancels a validated Open replacement");
            Check(!form.IsDisposed,"Cancelled Open keeps the current native viewer");
            using(FlowModal("unsavedProjectDiscard",dialog=>{CaptureDialog(dialog,output,"lifecycle-discard.png");SimpleControl<Button>(dialog,"unsavedProjectDiscard").PerformClick();}))
                SimpleToolbarItem(form,"workspaceNewProject").PerformClick();
            Check(form.IsDisposed,"Discard explicitly allows New without another native-root prompt");
            var final=(ModelForm)Field(MainForm.Instance,"_fighterForm");Call(final,"MarkFighterChanged");
            using(FlowModal("unsavedProjectCancel",dialog=>SimpleControl<Button>(dialog,"unsavedProjectCancel").PerformClick()))final.Close();
            Check(!final.IsDisposed,"Keep editing cancels window Close");
            ((AnimationControlClient)Field(final,"_controlEngine")).Dispose();final.Dispose();
        }
        Check(originals.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Lifecycle and body editing leave original model and motion bytes immutable");
    }
}
