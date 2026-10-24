using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

internal static partial class Program
{
    static async Task ModelPoseAnimation(string modelPath,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Model-only verification requires the isolated desktop.");
        string sourceHash=FighterProject.HashFile(modelPath);
        var project=FighterProject.Create("Original model-only control continuity");var form=new ModelForm();form.InitializeFighterProject(project);form.Show();
        Call(form,"ImportFighterModel",modelPath);var editor=form.FighterEditor;
        await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        Check(project.Animations.Count==0&&editor.SelectedCHR0==null,"Model-only controls and pose authoring require no donor motion");
        var model=(MDL0Node)editor.TargetModel;var root=model._linker.BoneCache.First();
        editor.SelectedBone=root;await(Task)Call(form,"AttachControlPin",false);await(Task)Call(form,"AttachControlPin",true);
        var setup=await Await<ControlReply>(Call(form,"LoadControls"));var ids=editor.AnimationPins.Select(p=>p.Id).OrderBy(s=>s).ToArray();
        Check(ids.Length==5&&editor.AnimationPins.Count(p=>p.Soft)==4,"Model-only fixture has four soft limb pins and an arbitrary FK handle");
        var pin=editor.AnimationPins.Single(p=>!p.Soft);var before=setup.Pose[pin.Bone];float radians=(float)Math.PI/12;
        Call(form,"BeginControlGesture",pin);await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));
        Call(form,"MoveControlGesture",new[]{pin.Position[0]+1.5f,pin.Position[1]+.75f,pin.Position[2],0f,(float)Math.Sin(radians/2),0f,(float)Math.Cos(radians/2),before[0],before[1],before[2]});Call(form,"EndControlGesture");await Wait(()=>Field(form,"_pinGesture")==null);
        var posed=root.FrameState;
        Check(Math.Abs(posed._translate._x-before[6]-1.5f)<.0001f&&Math.Abs(posed._translate._y-before[7]-.75f)<.0001f&&Math.Abs(posed._rotate._y-15)<.001f,"Model-only pin gesture retains independently expected translation and rotation");
        await(Task)Call(form,"CreateControlAnimation");
        await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        var clip=editor.SelectedCHR0;
        Check(clip!=null&&clip.FrameCount==60&&project.Animations.Single().Generated,"Create Animation From Pose publishes a new native animation");
        Check(ids.SequenceEqual(editor.AnimationPins.Select(p=>p.Id).OrderBy(s=>s)),"Creating the first animation preserves existing FK and soft-pin identities");
        foreach(int frame in new[]{1,60})
        {
            editor.SetFrame(frame);editor.UpdateModel();
            Check(Math.Abs(root.FrameState._translate._x-posed._translate._x)<.0001f&&Math.Abs(root.FrameState._rotate._y-posed._rotate._y)<.0001f,"Created native pose and rotation persist at displayed frame "+frame);
        }
        var loaded=await Await<ControlReply>(Call(form,"LoadControls"));Check(loaded.Pins.Length==5&&loaded.Pins.Count(p=>p.Soft)==4,"FK and soft pins remain active through the new animation's last frame and engine reload");
        string path=Path.Combine(output,"created-from-pose.chr0");form.ExportFighterResource(path);
        using(var reopened=NodeFactory.FromFile(null,path) as CHR0Node)
        {
            var entry=reopened.Children.OfType<CHR0EntryNode>().Single(n=>n.Name==root.Name);
            Check(Math.Abs(entry.GetFrameValue(6,59)-posed._translate._x)<.0001f&&Math.Abs(entry.GetFrameValue(4,59)-posed._rotate._y)<.0001f,"Fresh native decode preserves the model-only pose's authored move and rotation");
        }
        string manifest=Path.Combine(output,"model-pose.fpstudio");form.SaveFighterProject(manifest);
        using(var reopened=FighterProject.Open(manifest))Check(reopened.AnimationControls.Count>=2&&ids.All(id=>reopened.AnimationControls.Count(s=>s.Json.Contains(id))>=2),"Project save retains independent original model-pose and new animation control states");
        Check(FighterProject.HashFile(modelPath)==sourceHash,"Model-only posing and animation creation preserve the original native model bytes");
        await CaptureCheckpoint();editor.FrameFighterModel();editor.ModelPanel.Capture();editor.ModelPanel.Invalidate();editor.ModelPanel.Update();
        using(var image=editor.ModelPanel.GetScreenshot(editor.ModelPanel.ClientRectangle,false))image.Save(Path.Combine(output,"created-pose-model.png"));
        using(var bitmap=new Bitmap(form.Width,form.Height))
        {
            form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
            using(var image=editor.ModelPanel.GetScreenshot(editor.ModelPanel.ClientRectangle,false))
            {
                var point=form.PointToClient(editor.ModelPanel.PointToScreen(Point.Empty));int border=(form.Width-form.ClientSize.Width)/2;
                using(var graphics=Graphics.FromImage(bitmap))graphics.DrawImageUnscaled(image,point.X+border,point.Y+form.Height-form.ClientSize.Height-border);
            }
            bitmap.Save(Path.Combine(output,"created-pose-viewer.png"));
        }
        ((AnimationControlClient)Field(form,"_controlEngine")).Dispose();project.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(editor);form.Dispose();
    }
}
