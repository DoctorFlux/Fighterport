using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.Internal;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static async Task CharacterWorkspace(string modelPath,string motionPath,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Workspace acceptance requires the private desktop.");
        string modelHash=FighterProject.HashFile(modelPath),motionHash=FighterProject.HashFile(motionPath);
        var startup=MainForm.StartViewerApplication();var form=(ModelForm)Field(MainForm.Instance,"_fighterForm");var project=form.FighterProject;project.Name="Original Workspace Scene";Call(form,"ImportFighterModel",modelPath);Call(form,"ImportFighterAnimations",new[]{new[]{motionPath}});
        var editor=form.FighterEditor;var clip=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First();foreach(ListViewItem item in editor.leftPanel.listAnims.Items)item.Selected=ReferenceEquals(item.Tag,clip);editor.SetFrame(1);editor.FrameFighterModel();
        await Task.Delay(150);await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));var loaded=await Await<ControlReply>(Call(form,"LoadControls"));editor.SelectedBone=((MDL0Node)editor.TargetModel)._linker.BoneCache.First();await(Task)Call(form,"AttachControlPin",false);var pin=editor.AnimationPins.Single();
        Call(form,"BeginControlGesture",pin);await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));var local=loaded.Pose[pin.Bone];Call(form,"MoveControlGesture",new[]{pin.Position[0]+2,pin.Position[1],pin.Position[2],0f,0f,0f,1f,local[0],local[1],local[2]});Call(form,"EndControlGesture");await Wait(()=>Field(form,"_pinGesture")==null);
        string checkpoint=Path.Combine(output,"native-checkpoint.fpstudio");form.SaveFighterProject(checkpoint);string checkpointHash=FighterProject.HashFile(checkpoint);string originalManifest=project.ManifestPath;
        project.AnimationSlots.Add(new NativeAnimationSlot{Id="Reviewed entry",Required=true,Protected=true,AnimationKey=(string)Call(form,"SelectedFighterAnimationKey"),AnimationSha256=(string)Call(form,"NativeControlHash",editor.SelectedCHR0,".chr0"),ModelSha256=(string)Call(form,"NativeControlHash",editor.TargetModel,".mdl0")});
        var workspace=(NativeWorkspaceSession)Field(form,"_characterWorkspace");await workspace.Send(new{Operation="workspace-create",Name="Original Character",Path=Path.Combine(output,"Character")});string workspacePath=workspace.Info.Path,characterId=workspace.Info.Id;
        Check(workspace.Info.Scenes.Length==0&&!workspace.Info.Dirty,"Native workspace creates and saves an empty Core character without inventing a skeleton");
        await workspace.Send(new{Operation="workspace-target",Name="Target A",Game="Brawl",Platform="Wii",Build="Original procedural fixture",Mode="Animation editing"});await workspace.Send(new{Operation="workspace-target",Name="Target B",Game="Future configuration",Platform="Unresolved",Build="Unresolved",Mode="Planning"});
        string a=workspace.Info.Targets[0].Id,b=workspace.Info.Targets[1].Id;Check(a!=b,"Native workspace retains independent game/build/target identities without claiming a future adapter");
        await(Task)Call(form,"AddCurrentToWorkspace",(object)null);string shared=workspace.Info.ActiveScene;
        Check(workspace.Info.Scenes.Length==1&&workspace.Info.Scenes[0].Target==null,"Add Current Model captures a shared native scene and its animation/control document");
        Check(project.ManifestPath==originalManifest&&FighterProject.HashFile(checkpoint)==checkpointHash,"Workspace snapshot preserves the ordinary native Save destination and its committed descriptor");
        await workspace.Send(new{Operation="workspace-copy",SceneId=shared,TargetId=a});string sceneA=workspace.Info.ActiveScene;await workspace.Send(new{Operation="workspace-copy",SceneId=shared,TargetId=b});string sceneB=workspace.Info.ActiveScene;await workspace.Send(new{Operation="workspace-save"});
        Check(new[]{shared,sceneA,sceneB}.Distinct().Count()==3&&workspace.Info.Scenes.Single(s=>s.Id==sceneA).DerivedFrom==shared,"Shared assignment creates independent target scenes with retained revision ancestry");
        form.BindWorkspaceScene(sceneA);pin=editor.AnimationPins.Single();Call(form,"BeginControlGesture",pin);await Wait(()=>Field(form,"_pinGesture")!=null&&(bool)Field(Field(form,"_pinGesture"),"Ready"));var frame=editor.SelectedControlTransform.Value;frame._translate._x+=3;Call(form,"MoveControlGesture",BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.ControlHandleGoal(frame));Call(form,"EndControlGesture");await Wait(()=>Field(form,"_pinGesture")==null);
        await(Task)Call(form,"SaveCharacterWorkspace");Check(!(bool)Field(form,"_fighterDirty"),"Successful matching workspace save marks the captured native editor revision clean");
        await workspace.Send(new{Operation="workspace-open",Path=workspacePath});Check(workspace.Info.Id==characterId&&workspace.Info.Targets.Length==2&&workspace.Info.Scenes.Length==3,"Actual character descriptor reopens with both targets and all independent scenes");
        var json=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=64*1024*1024,RecursionLimit=200};
        var descriptor=json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(workspacePath));
        foreach(var value in (System.Collections.IEnumerable)descriptor["Scenes"])
        {
            var scene=(System.Collections.Generic.Dictionary<string,object>)value;var content=json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(workspacePath),(string)scene["RevisionPath"])));
            var slots=((System.Collections.IEnumerable)content["Slots"]).Cast<System.Collections.Generic.Dictionary<string,object>>().Where(s=>s.ContainsKey("EditorOwner")&&(string)s["EditorOwner"]=="Next.CustomSlots").ToArray();
            Check(slots.Length==1&&(string)slots[0]["Id"]=="custom:Reviewed entry"&&slots[0]["Animation"]!=null&&(bool)slots[0]["Protected"],"Saved Core workspace scene retains the exact native custom slot, clip identity and protection");
        }
        foreach(var pair in new[]{new{Scene=shared,Target=(string)null,X=2f},new{Scene=sceneA,Target=a,X=5f},new{Scene=sceneB,Target=b,X=2f}})
        {
            await workspace.Send(new{Operation="workspace-select",TargetId=pair.Target,SceneId=pair.Scene});var exported=await workspace.Send(new{Operation="workspace-export",Output=Path.Combine(output,"native-"+pair.Scene)});
            string manifest=NativeWorkspaceSession.ExtractPackage(exported.Package,Path.Combine(output,"reopen-"+pair.Scene));using(var fresh=FighterProject.Open(manifest))
            {
                Check(fresh.AnimationSlots.Single().Id=="Reviewed entry"&&fresh.AnimationSlots.Single().Protected,"Independent native workspace package preserves custom slot metadata");
                Check(fresh.AnimationControls.Any(c=>c.Json.Contains(pin.Id)),"Workspace native package preserves saved pin identity for scene "+pair.X);
                using(var native=NodeFactory.FromFile(null,fresh.Animations.Single().WorkingPath))
                {
                    var motion=Walk(native).OfType<CHR0Node>().Single(c=>c.Name==clip.Name);var track=(CHR0EntryNode)motion.FindChild(pin.BoneName,false);Check(Math.Abs(track.GetFrameValue(6,0)-loaded.Pose[pin.Bone][6]-pair.X)<.0001,"Fresh native workspace scene retains independent root translation "+pair.X);
                }
            }
        }
        Call(form,"ShowCharacterWorkspace");var tree=(TreeView)Field(form,"_workspaceTree");Check(tree.Nodes.Count==3&&tree.Nodes.Cast<TreeNode>().Sum(n=>n.Nodes.Count)==3,"Actual dark workspace tree displays shared models and both target scopes");
        editor.FrameFighterModel();await CaptureWindow((Form)Field(form,"_workspaceWindow"),Path.Combine(output,"character-workspace.png"));await CaptureWindow(form,Path.Combine(output,"character-workspace-viewer.png"));
        string bad=Path.Combine(output,"bad-package.zip");using(var zip=ZipFile.Open(bad,ZipArchiveMode.Create)){using(var writer=new StreamWriter(zip.CreateEntry("project.fpstudio.assets/../../escape").Open()))writer.Write("invalid");}bool rejected=false;try{NativeWorkspaceSession.ExtractPackage(bad,Path.Combine(output,"bad-output"));}catch(InvalidDataException){rejected=true;}Check(rejected&&!File.Exists(Path.Combine(output,"escape")),"Native editor package rejects paths escaping its extraction folder");
        Check(FighterProject.HashFile(modelPath)==modelHash&&FighterProject.HashFile(motionPath)==motionHash&&FighterProject.HashFile(checkpoint)==checkpointHash,"Workspace edits preserve source model/motion bytes and the independent native checkpoint");
        bool wrongTarget=false;try{Call(form,"ProtectCharacterWorkspaceOutput",Path.Combine(Path.GetDirectoryName(workspacePath),"Targets",Guid.Parse(b).ToString("N"),"Native","test.chr0"));}catch(System.Reflection.TargetInvocationException e){wrongTarget=e.InnerException is IOException;}Check(wrongTarget,"Native export guard rejects a destination in another target's workspace");
        Call(form,"WorkspaceAction",new Func<Task>(()=>(Task)Call(form,"OpenWorkspaceScene",b,sceneB)));await Wait(()=>form.IsDisposed);
        var opened=(ModelForm)Field(MainForm.Instance,"_fighterForm");await Task.Delay(150);await Wait(()=>!(bool)Field(opened,"_controlBusy")&&!(bool)Field(opened,"_controlSetupQueued"));var reopenedControls=await Await<ControlReply>(Call(opened,"LoadControls"));
        Check(!ReferenceEquals(opened,form)&&opened.FighterEditor.SelectedCHR0!=null&&reopenedControls.Pins.Single().Id==pin.Id,"Actual Open Selected replaces the viewer and restores the independently saved target's clip and pin");
        var openedRoot=((MDL0Node)opened.FighterEditor.TargetModel)._linker.BoneCache.Single(n=>n.Name==pin.BoneName);Check(Math.Abs(openedRoot.FrameState._translate._x-loaded.Pose[pin.Bone][6]-2)<.0001,"Actual target switch restores target B's independent animation rather than target A's edits");
        await CaptureWindow(opened,Path.Combine(output,"opened-target-viewer.png"));
        ((AnimationControlClient)Field(opened,"_controlEngine")).Dispose();workspace.Dispose();opened.FighterProject.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(opened.FighterEditor);opened.Dispose();startup.Dispose();GC.Collect();GC.WaitForPendingFinalizers();
    }
}
