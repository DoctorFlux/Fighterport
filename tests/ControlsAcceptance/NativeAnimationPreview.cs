using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

internal static partial class Program
{
    // Read-only native preview of an explicit exported resource. No retarget,
    // pins, fit, optimization or replacement model is run by a capture request.
    static async Task NativeAnimationPreview(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Native animation preview requires an isolated desktop.");
        var originals=new[]{args[0],args[1]}.ToDictionary(p=>p,FighterProject.HashFile);
        var project=FighterProject.Create("Read-only exported native animation review");
        var form=new ModelForm();
        try
        {
            form.InitializeFighterProject(project);form.Show();
            Call(form,"ImportFighterModel",args[0]);await SimpleControlsReady(form);
            var existing=form.FighterEditor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().ToArray();
            Call(form,"ImportFighterAnimations",new[]{new[]{args[1]}});await SimpleControlsReady(form);
            var editor=form.FighterEditor;var model=(MDL0Node)editor.TargetModel;
            var clip=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c=>!existing.Contains(c));
            editor.SelectedCHR0=clip;editor.ConfigureFighterView();editor.FrameFighterModel();
            string modelBefore=(string)Call(form,"NativeControlHash",model,".mdl0");
            string clipBefore=NativeAnimationOptimization.SourceContentKey(clip);
            int undo=editor._saveIndex;bool dirty=(bool)Field(form,"_fighterDirty");
            foreach(int frame in new[]{1,Math.Max(1,clip.FrameCount/2),clip.FrameCount}.Distinct())
            {
                editor.SetFrame(frame);await SimpleControlsReady(form);
                await CaptureSimpleWorkspace(form,Path.Combine(output,"native-frame-"+frame+".png"));
                Check(ReferenceEquals(editor.SelectedCHR0,clip)&&ReferenceEquals(editor.TargetModel,model),"Read-only native preview retains exact clip and target at display frame "+frame);
            }
            Check(NativeAnimationOptimization.SourceContentKey(clip)==clipBefore&&
                (string)Call(form,"NativeControlHash",model,".mdl0")==modelBefore&&
                editor._saveIndex==undo&&(bool)Field(form,"_fighterDirty")==dirty,
                "Read-only native preview preserves content, all model weights/binds, native Undo and dirty state");
            Check(originals.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Native animation preview leaves every original/export file byte unchanged");
        }
        finally
        {
            ((AnimationControlClient)Field(form,"_controlEngine"))?.Dispose();
            BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(form.FighterEditor);
            form.Dispose();project.Dispose();
        }
    }
}
