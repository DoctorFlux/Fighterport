using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.Internal;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task RetargetNext(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Native modal verification requires the isolated desktop.");
        string sourcePath=args[Array.IndexOf(args,"--retarget-source")+1],clipName=args[Array.IndexOf(args,"--clip")+1];
        var inputHashes=new[]{args[0],args[1],sourcePath}.Distinct().ToDictionary(p=>p,FighterProject.HashFile);
        var project=FighterProject.Create("Native Next retarget and pin verification");
        var form=new ModelForm();form.InitializeFighterProject(project);form.Show();
        Call(form,"ImportFighterModel",args[0]);Call(form,"ImportFighterAnimations",new[]{new[]{args[1]}});
        var editor=form.FighterEditor;
        await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        var requested=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c=>c.Name==clipName);
        editor.leftPanel.listAnims.SelectedItems.Clear();var requestedItem=editor.leftPanel.listAnims.Items.Cast<ListViewItem>().Single(i=>ReferenceEquals(i.Tag,requested));requestedItem.Selected=true;requestedItem.Focused=true;editor.SetFrame(1);
        await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        await Await<ControlReply>(Call(form,"LoadControls"));
        Check(editor.SelectedCHR0==requested,"Retargeting uses the clip selected through the actual native animation list");
        var original=editor.SelectedCHR0;var target=(MDL0Node)editor.TargetModel;
        string targetHash=(string)Call(form,"NativeControlHash",target,".mdl0");
        using(var timer=Modal("Retarget Options",dialog=>
        {
            var mode=dialog.Controls.OfType<ComboBox>().Single();
            Check(mode.Enabled&&mode.Items.Count==4&&mode.Location==new Point(130,14)&&mode.Width==300,"Next's four ground-contact modes retain their original field offsets");
            mode.SelectedIndex=(int)GroundContactMode.SourceContacts;CaptureDialog(dialog,output,"retarget-options.png");
            dialog.Controls.OfType<Button>().Single(b=>b.Text=="Apply").PerformClick();
        }))Call(form,"ShowFighterRetargetOptions");
        Check(project.GroundMode==GroundContactMode.SourceContacts,"Retarget Options applies the native project setting");project.GroundMode=GroundContactMode.Auto;
        using(var timer=Modal("Skeleton Mapping",dialog=>
        {
            var grid=dialog.Controls.OfType<DataGridView>().Single();Check(grid.Rows.Count>=17,"Native Skeleton Mapping exposes the actual humanoid roles");CaptureDialog(dialog,output,"skeleton-mapping.png");
            dialog.Controls.OfType<Button>().Single(b=>b.Text=="Use Mapping").PerformClick();
        }))Call(form,"CaptureFighterRig",target,true);
        Check(!Application.OpenForms.Cast<Form>().Any(f=>f.Text.StartsWith("Rig Coordinates")),"Native retarget mapping does not require the separate engine coordinate workflow");
        using(var sourceRoot=NodeFactory.FromFile(null,sourcePath))
        {
            var source=Walk(sourceRoot).ToList().OfType<MDL0Node>().OrderByDescending(m=>m.AllBones.Count).First();
            var captured=BrawlRetargeter.Capture(source);
            using(var expected=BrawlRetargeter.Bake(captured,BrawlRetargeter.Capture(target),original))
            {
                string expectedPath=Path.Combine(output,"reference-native.chr0");expected.Animation.Export(expectedPath);
                bool progressSeen=false;
                FighterAsset generated;
                using(var timer=Modal("Retargeting Animation",dialog=>
                {
                    progressSeen=true;Check(dialog.Controls.OfType<ProgressBar>().Single().Style!=ProgressBarStyle.Marquee,"Original Next retargeting exposes real solver progress");CaptureDialog(dialog,output,"retarget-progress.png");
                }))generated=form.RetargetFighterAnimation(sourcePath);
                Check(generated!=null&&generated.Retarget.Algorithm==HumanoidRetargeter.Algorithm&&string.IsNullOrEmpty(generated.Retarget.EngineReceipt),"The actual retarget action uses Next's original native retargeter");
                Check(progressSeen&&FighterProject.HashFile(generated.WorkingPath)==FighterProject.HashFile(expectedPath),"Native UI bake matches the original Next retargeter byte for byte");
                Check(generated.Retarget.Mapping.Count>=17&&editor.SelectedCHR0.FrameCount==original.FrameCount&&editor.SelectedCHR0.Loop==original.Loop,"Published native result preserves timing and reviewed target bone mapping");
            }
            await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
            Check(ReferenceEquals(Call(form,"SelectedFighterAsset"),project.Animations.Last()),"Native viewer selects the generated target animation before pin authoring");
            var selectedResult=editor.SelectedCHR0;editor.leftPanel.UpdateAnimations();
            Check(ReferenceEquals(editor.SelectedCHR0,selectedResult),"Refreshing same-named source and retargeted animations retains the exact target revision");
            await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
            var clip=editor.SelectedCHR0;
            using(var timer=Modal("Retarget Details",dialog=>
            {
                var save=dialog.Controls.OfType<Button>().Single(b=>b.Text=="Save Result As...");var text=dialog.Controls.OfType<TextBox>().Single();
                Check(save.Visible&&dialog.ClientRectangle.Contains(save.Bounds)&&!save.Bounds.IntersectsWith(text.Bounds),"Retarget Details visibly exposes Save Result As below its report");CaptureDialog(dialog,output,"retarget-details.png");dialog.Close();
            }))Call(form,"ShowFighterRetargetDetails");
            editor.SelectedBone=target._linker.BoneCache.First();await(Task)Call(form,"AttachControlPin",false);
            var loaded=await Await<ControlReply>(Call(form,"LoadControls"));var pin=editor.AnimationPins.Single();
            var before=loaded.Pose[pin.Bone];float angle=(float)Math.PI/12;
            Call(form,"BeginControlGesture",pin);await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));
            var q=new System.Numerics.Quaternion(pin.Rotation[0],pin.Rotation[1],pin.Rotation[2],pin.Rotation[3]);q=System.Numerics.Quaternion.Normalize(System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,angle)*q);
            Call(form,"MoveControlGesture",new[]{pin.Position[0]+.5f,pin.Position[1],pin.Position[2],q.X,q.Y,q.Z,q.W,before[0],before[1],before[2]});Call(form,"EndControlGesture");await Wait(()=>Field(form,"_pinGesture")==null);
            var bone=target._linker.BoneCache.Single(b=>b.Name==pin.BoneName);var moved=bone.FrameState;
            Check(Math.Abs(moved._translate._x-before[6]-.5f)<.0002f&&Math.Abs(moved._rotate._y-before[4]-15)<.002f,"Moving and rotating a pin on the native retarget result saves the expected animation channels");
            editor.SetFrame(Math.Min(8,clip.FrameCount));editor.SetFrame(1);Check(Math.Abs(bone.FrameState._translate._x-moved._translate._x)<.0001f,"Retargeted pin pose survives timeline changes");
            editor.Undo();await Wait(()=>!(bool)Field(form,"_controlBusy"));Check(Math.Abs(bone.FrameState._translate._x-before[6])<.0002f,"Pin Undo restores the native retarget result");
            editor.Redo();await Wait(()=>!(bool)Field(form,"_controlBusy"));Check(Math.Abs(bone.FrameState._rotate._y-moved._rotate._y)<.0002f,"Pin Redo restores the authored rotation on the retarget result");
            string saved=Path.Combine(output,"retarget-pin-edited.chr0");form.ExportFighterResource(saved);
            using(var reopened=NodeFactory.FromFile(null,saved) as CHR0Node)
            {
                var track=reopened.Children.OfType<CHR0EntryNode>().Single(n=>n.Name==pin.BoneName);
                Check(Math.Abs(track.GetFrameValue(6,0)-moved._translate._x)<.0001f&&Math.Abs(track.GetFrameValue(4,0)-moved._rotate._y)<.0001f,"Fresh CHR0 decode retains moved and rotated pin keys on the retargeted animation");
            }
            string manifest=Path.Combine(output,"retarget-pins.fpstudio");form.SaveFighterProject(manifest);
            using(var reopened=FighterProject.Open(manifest))
            {
                Check(reopened.AnimationControls.Count>0&&reopened.Animations.Last().Retarget.Algorithm==HumanoidRetargeter.Algorithm,"Fresh Next project read retains native retarget provenance and independent pin state");
                Check(reopened.Warnings.Count==0,"Saved native retarget project still resolves its protected originals");
                var reopenedForm=new ModelForm();reopenedForm.InitializeFighterProject(reopened);
                reopenedForm.Show();
                await Wait(()=>!(bool)Field(reopenedForm,"_controlBusy")&&!(bool)Field(reopenedForm,"_controlSetupQueued")&&reopenedForm.FighterEditor.AnimationPins.Any(p=>p.Id==pin.Id));
                var reopenedBone=((MDL0Node)reopenedForm.FighterEditor.TargetModel)._linker.BoneCache.Single(b=>b.Name==pin.BoneName);
                Check(reopenedForm.FighterEditor.AnimationPins.Any(p=>p.Id==pin.Id)&&Math.Abs(reopenedBone.FrameState._translate._x-moved._translate._x)<.0001f&&Math.Abs(reopenedBone.FrameState._rotate._y-moved._rotate._y)<.0001f,"Reopening the saved project in the actual native viewer restores the pin and its moved/rotated animation pose");
                ((AnimationControlClient)Field(reopenedForm,"_controlEngine")).Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(reopenedForm.FighterEditor);reopenedForm.Dispose();
            }
            await CaptureCheckpoint();editor.FrameFighterModel();editor.ModelPanel.Capture();editor.ModelPanel.Invalidate();editor.ModelPanel.Update();
            using(var image=editor.ModelPanel.GetScreenshot(editor.ModelPanel.ClientRectangle,false))image.Save(Path.Combine(output,"retarget-pin-model.png"));
            Check((string)Call(form,"NativeControlHash",target,".mdl0")==targetHash,"Retargeting and pin edits preserve target native model/binds/weights");
            using(var probe=new CHR0Node{Name="OriginalTrackOwnershipProbe",FrameCount=2})
            {
                var entry=probe.CreateEntry(captured.Bones[captured.Rig.Roles["pelvis"]].Name);entry.SetKeyframe(3,0,0,true,true);entry.SetKeyframe(3,1,3,true,true);
                probe.CreateEntry("OriginalUnownedAccessoryTrack").SetKeyframe(4,0,12,true,true);
                using(var native=BrawlRetargeter.Bake(captured,BrawlRetargeter.Capture(target),probe,groundMode:GroundContactMode.Airborne))
                    Check(native.IgnoredSourceBones.Contains("OriginalUnownedAccessoryTrack")&&!native.Animation.Children.Any(c=>c.Name=="OriginalUnownedAccessoryTrack"),"Original Next accepts mapped body channels and reports excluded foreign/accessory tracks explicitly");
                using(var ready=OnRetargetReady(form,dialog=>dialog.Controls.OfType<Button>().Single().PerformClick()))
                    Check(Call(form,"BakeWithProgress",source,target,probe)==null,"Cancelling Next's native progress window publishes no result");
                bool stale=false;using(var ready=OnRetargetReady(form,dialog=>project.GroundMode=GroundContactMode.Airborne))
                {
                    try{using(var result=(RetargetResult)Call(form,"BakeWithProgress",source,target,probe)){} }catch(System.Reflection.TargetInvocationException e){stale=e.InnerException is InvalidOperationException&&e.InnerException.Message.Contains("inputs changed");}
                }
                Check(stale,"Changed native retarget settings reject stale asynchronous publication");project.GroundMode=GroundContactMode.Auto;
            }
        }
        var library=new AnimationLibrary(Path.Combine(output,"isolated-library"));
        var modelRecord=BrawlLibrarySource.InspectModels(sourcePath).First(m=>!m.Shadow);
        library.Register(BrawlLibrarySource.Prepare(new LibraryRegistration{ModelPath=sourcePath,ModelKey=modelRecord.ResourceKey,MotionPath=args[1],Character=modelRecord.Name,GameVersion="Local supplied native fixture",BuildEvidence="Local supplied source pair; no independent game-build or disc-membership verification.",ApproveAssociation=true}));
        var item=library.Search(clipName,0,out _).Single(i=>i.Clip.Name==clipName&&i.Model!=null);
        Call(form,"BuildLibraryPane");form.GetType().GetField("_library",Private).SetValue(form,library);
        var tree=(TreeView)Field(form,"_libraryList");var node=new TreeNode(clipName){Tag=item};tree.Nodes.Add(node);tree.SelectedNode=node;
        using(var timer=Modal("Retargeting Animation",dialog=>{}))Call(form,"UseLibraryOnTarget");
        var linked=project.Animations.Last();
        Check(linked.Retarget.Algorithm==HumanoidRetargeter.Algorithm&&linked.Retarget.Library.ClipId==item.Clip.Id&&linked.Retarget.Library.ModelContainerSha256==inputHashes[sourcePath],"The actual source-bound library action also uses Next's original retargeter and retains its authentic source reference");
        File.Copy(linked.WorkingPath,Path.Combine(output,"library-native.chr0"));
        using(var expected=NodeFactory.FromFile(null,Path.Combine(output,"reference-native.chr0")) as CHR0Node)
        using(var actual=NodeFactory.FromFile(null,linked.WorkingPath) as CHR0Node)
        {
            var difference=NativeDifference(expected,actual);File.WriteAllText(Path.Combine(output,"library-native-comparison.txt"),difference??"All native keys, tangents, semantic flags and timing agree exactly.");
            Check(difference==null,"Source-bound library retarget preserves every original native key, tangent, semantic flag and timing value");
        }
        await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        form.SaveFighterProject(Path.Combine(output,"library-retarget.fpstudio"));
        using(var reopened=FighterProject.Open(Path.Combine(output,"library-retarget.fpstudio")))Check(reopened.Animations.Last().Retarget.Library.ClipId==item.Clip.Id,"Source-bound native retarget result and provenance survive project save/reopen");
        bool transitionFinished=false;Exception transitionFailure=null;int beforeTransition=project.Animations.Count;
        form.TransitionFitReady=async window=>{try{((CheckBox)window.Controls.Find("transition_binding",true).Single()).Checked=true;((Button)window.Controls.Find("prepareTransition",true).Single()).PerformClick();await Wait(()=>!window.Busy);var report=(TextBox)window.Controls.Find("transitionReport",true).Single();Check(window.Candidate==null&&report.Text.Contains("baked support contacts")&&project.Animations.Count==beforeTransition,"FK-only fitting blocks the actual Next result's baked support contacts instead of breaking them after IK");await CaptureWindow(window,Path.Combine(output,"transition-contact-conflict.png"));}catch(Exception error){transitionFailure=error;}finally{window.Close();transitionFinished=true;}};
        Call(form,"ShowTransitionFit");await Wait(()=>transitionFinished);if(transitionFailure!=null)throw transitionFailure;
        string combinedOutput=null;if(args.Contains("--combined-stress")){combinedOutput=await ContactTransitions(form,project,output);await NativePaintLifetime(form,output);}
        Check(inputHashes.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Every supplied source container remains byte-identical");
        Call(form,"DisposeAnimationLibrary");
        ((AnimationControlClient)Field(form,"_controlEngine")).Dispose();project.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(editor);form.Dispose();
        if(combinedOutput!=null)await ControlGizmos(args[0],combinedOutput,output);
    }
    static Timer Modal(string title,Action<Form> action)
    {
        var timer=new Timer{Interval=60};timer.Tick+=(s,e)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Visible&&f.Text.StartsWith(title));if(dialog==null)return;timer.Stop();action(dialog);};timer.Start();return timer;
    }
    sealed class Subscription:IDisposable{readonly Action release;internal Subscription(Action action){release=action;}public void Dispose()=>release();}
    static IDisposable OnRetargetReady(ModelForm owner,Action<Form> action){owner.RetargetDialogReady+=action;return new Subscription(()=>owner.RetargetDialogReady-=action);}
    static void CaptureDialog(Form form,string output,string name)
    {
        using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,name));}
    }
    static string NativeDifference(CHR0Node expected,CHR0Node actual)
    {
        if(expected.Name!=actual.Name||expected.FrameCount!=actual.FrameCount||expected.Loop!=actual.Loop||expected.Children.Count!=actual.Children.Count)return "Native animation identity/timing/count differs: "+expected.Name+" / "+actual.Name+"; "+expected.FrameCount+" / "+actual.FrameCount;
        foreach(var a in expected.Children.OfType<CHR0EntryNode>())
        {
            var b=actual.Children.OfType<CHR0EntryNode>().SingleOrDefault(n=>n.Name==a.Name);if(b==null)return "Missing target track "+a.Name;
            if(a.UseModelScale!=b.UseModelScale||a.UseModelRotate!=b.UseModelRotate||a.UseModelTranslate!=b.UseModelTranslate||a.ScaleCompensateApply!=b.ScaleCompensateApply||a.ScaleCompensateParent!=b.ScaleCompensateParent||a.ClassicScaleOff!=b.ClassicScaleOff)return "Native semantic flags differ at "+a.Name;
            for(int c=0;c<9;c++)
            {
                var ak=a.KeyArrays[c].Entries.Select(k=>Tuple.Create(k._index,k._value,k._tangent)).ToArray();var bk=b.KeyArrays[c].Entries.Select(k=>Tuple.Create(k._index,k._value,k._tangent)).ToArray();
                if(!ak.SequenceEqual(bk)){var flags=typeof(CHR0EntryNode).GetField("_code",Private);return "Native keys differ at "+a.Name+" channel "+c+" type "+a.GetType().Name+" flags "+flags.GetValue(a)+" / "+flags.GetValue(b)+": "+string.Join(";",ak.Select(k=>k.ToString()))+" / "+string.Join(";",bk.Select(k=>k.ToString()));}
            }
        }
        return null;
    }
}
