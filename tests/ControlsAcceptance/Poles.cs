using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.Internal;
using BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task PoleHandles(ModelForm form,ControlPin ik,string output)
    {
        var editor=form.FighterEditor;var model=(MDL0Node)editor.TargetModel;var clip=editor.SelectedCHR0;
        var pole=editor.AnimationHandles.Single(p=>p.Id=="pole:"+ik.Bone);
        Check(pole.RootName!=null&&pole.MiddleName!=null,"Every reviewed IK chain exposes a separate pole handle using authentic bone names");
        var end=model._linker.BoneCache.Single(b=>b.Name==pole.BoneName);var middle=model._linker.BoneCache.Single(b=>b.Name==pole.MiddleName);
        var oldMiddle=middle.Matrix.GetPoint();var oldEnd=end.Matrix.GetPoint();
        editor.SelectedAnimationPin=null;editor.ControlType=TransformType.Translation;
        var point=(Vector3)Call(editor,"PinPosition",pole);var viewport=editor.ModelPanel.CurrentViewport;var screen=viewport.Camera.Project(point);
        Call(editor,"modelPanel1_MouseDown",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,1,(int)Math.Round(screen._x),(int)Math.Round(screen._y),0));
        await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));
        Check(editor.SelectedControlHandle.Id==pole.Id&&editor.SelectedBone==null,"The displayed viewport pole diamond picks its control independently of the underlying bone");
        Call(editor,"modelPanel1_MouseMove",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,0,(int)Math.Round(screen._x)+27,(int)Math.Round(screen._y)-18,0));
        Call(editor,"modelPanel1_MouseUp",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,1,(int)Math.Round(screen._x)+27,(int)Math.Round(screen._y)-18,0));await Wait(()=>Field(form,"_pinGesture")==null);
        Check((middle.Matrix.GetPoint()-oldMiddle).TrueDistance()>.0001,"Native viewport pole dragging changes the reviewed limb bend");
        Check((end.Matrix.GetPoint()-oldEnd).TrueDistance()<.005,"Pole dragging retains the IK effector position without replacing the skeleton");
        var changed=middle.Matrix.GetPoint();editor.Undo();await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));await Await<ControlReply>(Call(form,"LoadControls"));
        Check((middle.Matrix.GetPoint()-oldMiddle).TrueDistance()<.002,"Pole bend Undo restores the original native pose");
        editor.Redo();await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));await Await<ControlReply>(Call(form,"LoadControls"));
        Check((middle.Matrix.GetPoint()-changed).TrueDistance()<.002,"Pole bend Redo restores the authored native pose");
        editor.SelectControlHandle(editor.AnimationHandles.Single(p=>p.Id==pole.Id));Call(editor,"ShowControlInterpolation");
        Check(ReferenceEquals(editor.AnimationChannelTarget,clip.FindChild(pole.MiddleName,false)),"Pole selection opens the authored middle-joint bend curves in the original interpolation editor");
        string path=Path.Combine(output,"pole-controls.chr0");clip.Export(path);
        using(var fresh=NodeFactory.FromFile(null,path) as CHR0Node)Check(NativeDifference(clip,fresh)==null,"Fresh CHR0 decode preserves every pole-authored native key and tangent");
        await CaptureWindow(form,Path.Combine(output,"viewport-pole.png"));
        form.SaveFighterProject(Path.Combine(output,"pole-controls.fpstudio"));
        using(var fresh=FighterProject.Open(Path.Combine(output,"pole-controls.fpstudio")))
            Check(fresh.AnimationControls.Count>0,"The saved project retains reviewed control setup alongside pole-authored native animation bytes");
    }
}
