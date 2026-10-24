using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.Internal;
using BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static Point RingPoint(ModelForm form, float degrees)
    {
        var editor=form.FighterEditor;var pin=editor.SelectedControlHandle;var model=(MDL0Node)editor.TargetModel;
        var center=model._linker.BoneCache.Single(b=>b.Name==pin.BoneName).Matrix.GetPoint();var viewport=editor.ModelPanel.CurrentViewport;
        float radius=ModelEditorBase.OrbRadius(center,viewport.Camera);double angle=degrees*Math.PI/180;
        var point=viewport.Camera.Project(center+new Vector3(radius*(float)Math.Cos(angle),radius*(float)Math.Sin(angle),0));return new Point((int)Math.Round(point._x),(int)Math.Round(point._y));
    }
    static async Task RotateNativeRing(ModelForm form,float degrees=30)
    {
        var editor=form.FighterEditor;editor.ControlType=TransformType.Rotation;editor._coordinateTypes[1]=ModelEditorBase.CoordinateType.World;
        var a=RingPoint(form,25);var b=RingPoint(form,25+degrees);
        Call(editor,"modelPanel1_MouseDown",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,1,a.X,a.Y,0));
        await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));
        Check(editor._selectedBone==null,"Native rotation ring targets the control identity independently of bone selection");
        Call(editor,"modelPanel1_MouseMove",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,0,b.X,b.Y,0));
        Call(editor,"modelPanel1_MouseUp",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,1,b.X,b.Y,0));
        await Wait(()=>Field(form,"_pinGesture")==null);editor.UpdateModel();
    }
    static async Task ControlGizmos(string modelPath,string motionPath,string output)
    {
        NativeSingleBits();
        await NativePairedKeys(modelPath,output);
        if(!PrivateDesktop)throw new InvalidOperationException("Gizmo qualification requires the private desktop.");
        var project=FighterProject.Create("Native control curves");var form=new ModelForm();form.InitializeFighterProject(project);form.Show();
        Call(form,"ImportFighterModel",modelPath);Call(form,"ImportFighterAnimations",new[]{new[]{motionPath}});
        var editor=form.FighterEditor;var clip=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().FirstOrDefault(c=>c.Name=="Wait1")??editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First();
        foreach(ListViewItem item in editor.leftPanel.listAnims.Items)item.Selected=ReferenceEquals(item.Tag,clip);
        editor.SetFrame(1);editor.FrameFighterModel();await Task.Delay(150);await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        var loaded=await Await<ControlReply>(Call(form,"LoadControls"));
        Check(editor.AnimationHandles.Count(h=>h.Id.StartsWith("fk:"))==loaded.BoneNames.Length,"Every authentic bone has an independent FK control without donor-specific exceptions");
        Check(editor.AnimationHandles.Any(h=>h.Id.StartsWith("ik:")),"Reviewed compatible limb chains expose IK controls automatically");
        Call(form,"ShowAnimationControls");var tree=(TreeView)Field(form,"_controlTree");var root=editor.AnimationHandles.First(h=>h.Id=="fk:0");
        editor.SelectedBone=((MDL0Node)editor.TargetModel)._linker.BoneCache.Single(b=>b.Name==root.BoneName);
        Check(editor.SelectedAnimationPin==root.Id&&editor.SelectedBone==null,"Original bone picking routes to FK controls when control selection is enabled");
        tree.SelectedNode=AllNodes(tree.Nodes).Single(n=>(n.Tag as ControlPin)?.Id==root.Id);
        Check(editor.SelectedAnimationPin==root.Id&&editor.SelectedBone==null,"Native hierarchical control list selects an FK handle without selecting a bone");
        editor.ControlType=TransformType.Translation;
        var before=((MDL0Node)editor.TargetModel)._linker.BoneCache.Single(b=>b.Name==root.BoneName).FrameState;
        var screen=(Vector3)Call(editor,"PinScreen",root,editor.ModelPanel.CurrentViewport);int x=(int)Math.Round(screen._x),y=(int)Math.Round(screen._y);
        Call(editor,"modelPanel1_MouseDown",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,1,x,y,0));await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));
        Call(editor,"modelPanel1_MouseMove",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,0,x+18,y-9,0));Call(editor,"modelPanel1_MouseUp",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,1,x+18,y-9,0));await Wait(()=>Field(form,"_pinGesture")==null);
        var nativeRoot=((MDL0Node)editor.TargetModel)._linker.BoneCache.Single(b=>b.Name==root.BoneName);
        Check((nativeRoot.FrameState._translate-before._translate).TrueDistance()>.01,"Native FK handle movement commits to existing animation channels");
        var moved=nativeRoot.FrameState;await RotateNativeRing(form);
        var expectedRotation=Matrix.RotationMatrix(new Vector3(0,0,30))*Matrix.RotationMatrix(moved._rotate);var actualRotation=Matrix.RotationMatrix(nativeRoot.FrameState._rotate);
        float rotationDifference=Enumerable.Range(0,12).Max(i=>Math.Abs(expectedRotation[i]-actualRotation[i]));
        File.WriteAllText(Path.Combine(output,"rotation-diagnostic.txt"),"Before "+moved._rotate+" after "+nativeRoot.FrameState._rotate+" matrix error "+rotationDifference+" status "+((ToolStripStatusLabel)Field(form,"_fighterStatus")).Text);
        Check(rotationDifference<.02f,"Native control uses the actual Z rotation ring with expected world orientation, including native Euler singularities");
        await(Task)Call(form,"AttachControlPin",false);var pin=editor.AnimationPins.Single();editor.SelectControlHandle(pin);
        Check(editor.SelectedBone==null&&pin.BoneName==root.BoneName,"A selected FK control can receive an independently selectable saved pin");
        var nativeFields=editor.CHR0Editor._transBoxes;float oldX=nativeRoot.FrameState._translate._x;
        nativeFields[6].Text=(oldX+.5f).ToString(System.Globalization.CultureInfo.CurrentCulture);Call(nativeFields[6],"Apply");await Wait(()=>Field(form,"_pinGesture")==null);
        Check(Math.Abs(nativeRoot.FrameState._translate._x-oldX-.5f)<.0001,"Original transform textbox moves the selected pin and commits native keys");
        nativeFields[0].Text=(1.2f).ToString(System.Globalization.CultureInfo.CurrentCulture);Call(nativeFields[0],"Apply");await Wait(()=>Field(form,"_pinGesture")==null);
        Check(Math.Abs(nativeRoot.FrameState._scale._x-1.2f)<.0001,"Original scale textbox edits a selected pin through the shared transaction");
        for(int axis=1;axis<3;axis++){nativeFields[axis].Text=(1.2f).ToString(System.Globalization.CultureInfo.CurrentCulture);Call(nativeFields[axis],"Apply");await Wait(()=>Field(form,"_pinGesture")==null);}
        Check((nativeRoot.FrameState._scale-new Vector3(1.2f)).TrueDistance()<.0001,"All three original scale fields commit independently without replacing deform bones");
        Call(editor,"ShowControlInterpolation");var curves=editor.InterpolationEditor;
        Check(ReferenceEquals(curves._targetNode,clip.FindChild(root.BoneName,false)),"Selecting a pin opens its existing native channel in the original interpolation editor");
        var entry=(CHR0EntryNode)curves._targetNode;int last=Math.Min(12,clip.FrameCount-1);if(last<2)throw new InvalidOperationException("Fixture needs at least three frames.");
        while(entry.KeyArrays[6].KeyCount>0)entry.KeyArrays[6].Remove(entry.KeyArrays[6].Entries.First()._index);entry.SetKeyframe(6,0,0,true,true);entry.SetKeyframe(6,last,8,true,true);entry.GetKeyframe(6,0)._tangent=0;entry.GetKeyframe(6,last)._tangent=0;curves.SetTarget(null);curves.SetTarget(entry);curves.SelectedMode=6;curves.SelectedKeyframe=entry.GetKeyframe(6,0);
        float oldTangent=entry.GetKeyframe(6,0)._tangent;
        var tangent=(BrawlLib.Internal.Windows.Controls.NumericInputBox)Field(curves,"numInTan");tangent.Text=(1.25f).ToString(System.Globalization.CultureInfo.CurrentCulture);Call(tangent,"Apply");Application.DoEvents();await Task.Delay(50);
        Check(Math.Abs(entry.GetKeyframe(6,0)._tangent-1.25f)<.00001,"Original interpolation textbox edits the selected pin's native tangent");
        float t=.25f;float expected=(2*t*t*t-3*t*t+1)*0+(t*t*t-2*t*t+t)*last*1.25f+(-2*t*t*t+3*t*t)*8;
        Check(Math.Abs(entry.GetFrameValue(6,last*t)-expected)<.0001f,"Pin-authored channel evaluates independent cubic Hermite interpolation between keyed frames");
        editor.Undo();Check(Math.Abs(entry.GetKeyframe(6,0)._tangent-oldTangent)<.00001,"Curve tangent Undo restores exact prior coefficients");editor.Redo();Check(Math.Abs(entry.GetKeyframe(6,0)._tangent-1.25f)<.00001,"Curve tangent Redo restores edited coefficients");
        editor.SetFrame(2);Check(ReferenceEquals(curves._targetNode,entry),"Scrubbing keeps the pin interpolation target and native curve selection");
        await Await<ControlReply>(Call(form,"LoadControls"));Check(Math.Abs(entry.GetKeyframe(6,0)._tangent-1.25f)<.00001,"Reloading controls retains authored native curve tangents");
        string path=Path.Combine(output,"control-curves.chr0");clip.Export(path);
        using(var fresh=NodeFactory.FromFile(null,path) as CHR0Node){var track=(CHR0EntryNode)fresh.FindChild(root.BoneName,false);Check(Math.Abs(track.GetKeyframe(6,0)._tangent-1.25f)<.00001&&Math.Abs(track.GetFrameValue(6,last*t)-expected)<.0002,"Fresh CHR0 decode preserves pin curve tangent and interpolated subframe value");}
        var ik=editor.AnimationHandles.First(h=>h.Id.StartsWith("ik:"));tree.SelectedNode=AllNodes(tree.Nodes).Single(n=>(n.Tag as ControlPin)?.Id==ik.Id);
        Check(editor.SelectedControlHandle.Id==ik.Id&&editor.SelectedBone==null,"Hierarchical IK selection remains independent of native bone selection");
        var currentSetup=await Await<ControlReply>(Call(form,"LoadControls"));var chain=currentSetup.Chains.Single(c=>c.Effector==ik.Bone);var model=(MDL0Node)editor.TargetModel;
        var effector=model._linker.BoneCache.Single(b=>b.Name==ik.BoneName);var chainRoot=model._linker.BoneCache.Single(b=>b.Name==currentSetup.BoneNames[chain.Root]);
        var ikFrame=editor.SelectedControlTransform.Value;ikFrame._translate=effector.Matrix.GetPoint()*.96f+chainRoot.Matrix.GetPoint()*.04f;
        Call(form,"BeginControlGesture",ik);await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));Call(form,"MoveControlGesture",BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.ControlHandleGoal(ikFrame));Call(form,"EndControlGesture");await Wait(()=>Field(form,"_pinGesture")==null);
        Check((effector.Matrix.GetPoint()-ikFrame._translate).TrueDistance()<.002f,"Unpinned IK control solves a reviewed native chain and saves its end-effector position");
        Check(editor.AnimationPins.Length==1,"Editing an IK control creates native animation keys without silently creating a persistent pin");
        string ikPath=Path.Combine(output,"ik-control.chr0");clip.Export(ikPath);using(var fresh=NodeFactory.FromFile(null,ikPath) as CHR0Node){string difference=NativeDifference(clip,fresh);File.WriteAllText(Path.Combine(output,"ik-native-comparison.txt"),difference??"Exact native match");Check(difference==null,"Fresh native decode preserves all IK control keys, tangents and semantic flags");}
        await PoleHandles(form,ik,output);editor.SelectControlHandle(editor.AnimationPins.Single());editor.ControlType=TransformType.Scale;editor._coordinateTypes[2]=ModelEditorBase.CoordinateType.World;
        var pivot=nativeRoot.Matrix.GetPoint();var viewport=editor.ModelPanel.CurrentViewport;float radius=ModelEditorBase.OrbRadius(pivot,viewport.Camera);var scaleA=viewport.Camera.Project(pivot+new Vector3(.2f*radius,.2f*radius,0));var scaleB=viewport.Camera.Project(pivot+new Vector3(.3f*radius,.3f*radius,0));var oldScale=nativeRoot.FrameState._scale;
        Call(editor,"modelPanel1_MouseDown",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,1,(int)Math.Round(scaleA._x),(int)Math.Round(scaleA._y),0));await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));
        Call(editor,"modelPanel1_MouseMove",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,0,(int)Math.Round(scaleB._x),(int)Math.Round(scaleB._y),0));Call(editor,"modelPanel1_MouseUp",editor.ModelPanel,new MouseEventArgs(MouseButtons.Left,1,(int)Math.Round(scaleB._x),(int)Math.Round(scaleB._y),0));await Wait(()=>Field(form,"_pinGesture")==null);
        var scaleRatio=nativeRoot.FrameState._scale/oldScale;Check(scaleRatio._x>1.1f&&Math.Abs(scaleRatio._x-scaleRatio._y)<.0001&&Math.Abs(scaleRatio._y-scaleRatio._z)<.0001,"Original uniform scale triangle transforms the pin about its offset pivot and saves all three native scale channels");
        editor.ControlType=TransformType.Rotation;editor.RefreshControlCurveTarget();editor.InterpolationEditor.SelectedMode=6;editor.InterpolationEditor.SelectedKeyframe=entry.GetKeyframe(6,0);
        await CaptureWindow(form,Path.Combine(output,"control-workspace-viewer.png"));await CaptureWindow((Form)Field(form,"_controlsWindow"),Path.Combine(output,"control-workspace.png"));await CaptureWindow(editor._interpolationForm,Path.Combine(output,"control-interpolation.png"));
        editor._interpolationForm?.Dispose();((Form)Field(form,"_controlsWindow"))?.Dispose();((AnimationControlClient)Field(form,"_controlEngine")).Dispose();project.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(editor);form.Dispose();GC.Collect();GC.WaitForPendingFinalizers();
    }
    static System.Collections.Generic.IEnumerable<TreeNode> AllNodes(TreeNodeCollection nodes){foreach(TreeNode n in nodes){yield return n;foreach(var c in AllNodes(n.Nodes))yield return c;}}
    static unsafe void NativeSingleBits()
    {
        foreach(uint original in new[]{0xc048ae7fu,0x3f80a07fu,0xbf80b0ffu,0x3f800000u,0x80000000u})
        {
            uint native=(original>>24)|((original>>8)&0xff00)|((original<<8)&0xff0000)|(original<<24);
            bfloat value=default(bfloat);*(uint*)&value=native;float decoded=value;bfloat encoded=decoded;
            Check(*(uint*)&decoded==original&&*(uint*)&encoded==native,"Native endian float round-trip preserves exact finite bits "+original.ToString("X8")+" even when encoded bytes resemble a signalling NaN");
        }
    }
    static async Task CaptureWindow(Form form,string path)
    {
        await CaptureCheckpoint();
        using(var bitmap=new Bitmap(form.Width,form.Height))
        {
            form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
            if(form is ModelForm viewer)
            {
                var panel=viewer.FighterEditor.ModelPanel;panel.Capture();panel.Invalidate();panel.Update();
                using(var modelImage=panel.GetScreenshot(panel.ClientRectangle,false))using(var graphics=Graphics.FromImage(bitmap))
                {
                    var point=form.PointToClient(panel.PointToScreen(Point.Empty));int border=(form.Width-form.ClientSize.Width)/2;graphics.DrawImageUnscaled(modelImage,point.X+border,point.Y+form.Height-form.ClientSize.Height-border);
                }
            }
            else if(form is SceneFittingDialog||form is TransitionFitDialog||form is ContactBakeDialog)
            {
                var views=form is SceneFittingDialog fitting?new[]{fitting.SourceView,fitting.TargetView}:form is ContactBakeDialog contact?new[]{contact.SourceView,contact.TargetView}:new[]{((TransitionFitDialog)form).SourceView,((TransitionFitDialog)form).TargetView};
                foreach(var view in views)
                {
                    var panel=view.Viewport;panel.Capture();panel.Invalidate();panel.Update();
                    using(var modelImage=panel.GetScreenshot(panel.ClientRectangle,false))using(var graphics=Graphics.FromImage(bitmap))
                    {
                        var point=form.PointToClient(panel.PointToScreen(Point.Empty));int border=(form.Width-form.ClientSize.Width)/2;graphics.DrawImageUnscaled(modelImage,point.X+border,point.Y+form.Height-form.ClientSize.Height-border);
                    }
                }
            }
            else if(form is BrawlLib.Internal.Windows.Forms.InterpolationForm interpolation)
            {
                var panel=interpolation._interpolationEditor.interpolationViewer;panel.Capture();panel.Invalidate();panel.Update();
                using(var graph=new Bitmap(panel.Width,panel.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    var data=graph.LockBits(new Rectangle(Point.Empty,graph.Size),System.Drawing.Imaging.ImageLockMode.WriteOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    try{OpenTK.Graphics.OpenGL.GL.ReadBuffer(OpenTK.Graphics.OpenGL.ReadBufferMode.Front);OpenTK.Graphics.OpenGL.GL.ReadPixels(0,0,panel.Width,panel.Height,OpenTK.Graphics.OpenGL.PixelFormat.Bgra,OpenTK.Graphics.OpenGL.PixelType.UnsignedByte,data.Scan0);}
                    finally{graph.UnlockBits(data);}
                    graph.RotateFlip(RotateFlipType.RotateNoneFlipY);var point=form.PointToClient(panel.PointToScreen(Point.Empty));int border=(form.Width-form.ClientSize.Width)/2;
                    using(var graphics=Graphics.FromImage(bitmap))graphics.DrawImageUnscaled(graph,point.X+border,point.Y+form.Height-form.ClientSize.Height-border);
                }
            }
            bitmap.Save(path);
        }
    }
}
