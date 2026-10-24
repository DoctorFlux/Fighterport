using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

internal static partial class Program
{
    static async Task ProjectCompatibility(string[] args,string output)
    {
        string nativePath=args[Array.IndexOf(args,"--saved-project")+1],legacyPath=args[Array.IndexOf(args,"--legacy-project")+1];string before=FighterProject.HashFile(nativePath),legacyHash=FighterProject.HashFile(legacyPath);
        var project=FighterProject.Open(nativePath);var saved=project.AnimationControls.Select(s=>s.Json).ToArray();var form=new ModelForm();form.InitializeFighterProject(project);form.Show();await Task.Delay(150);await Wait(()=>!(bool)Field(form,"_controlBusy")&&!(bool)Field(form,"_controlSetupQueued"));
        var loaded=await Await<ControlReply>(Call(form,"LoadControls"));Check(loaded.Pins.Length>0&&loaded.Pins.All(p=>saved.Any(s=>s.Contains(p.Id))),"Earlier preview's saved native pin identities load through legacy control-key compatibility");
        Check(form.FighterEditor.SelectedCHR0!=null,"Earlier preview's animation selection restores in the actual native viewer");
        string newKey=(string)Call(form,"ControlKey");Check(newKey.Contains("|@root")&&project.AnimationControls.Any(c=>c.Key==newKey),"Legacy name-based control state gains an exact native identity key without erasing its original state");
        string copy=Path.Combine(output,"compatible.fpstudio");form.SaveFighterProject(copy);using(var reopened=FighterProject.Open(copy))Check(reopened.AnimationControls.Any(c=>c.Key==newKey&&loaded.Pins.All(p=>c.Json.Contains(p.Id))),"Migrated native control keys and pin state survive a fresh project reopen");
        var workspace=(NativeWorkspaceSession)Field(form,"_characterWorkspace");await workspace.Send(new{Operation="workspace-open",Path=legacyPath});Check(workspace.Info.Scenes.Length>0,"Native workspace reads an older standalone scene/character descriptor");
        var scene=workspace.Info.Scenes.First();await workspace.Send(new{Operation="workspace-select",TargetId=scene.Target,SceneId=scene.Id});var reply=await workspace.Send(new{Operation="workspace-export",Output=Path.Combine(output,"legacy-native")});
        var converted=(FighterProject)typeof(ModelForm).GetMethod("CreateLegacyWorkspaceProject",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{reply,scene.Name});var second=new ModelForm();second.InitializeFighterProject(converted);second.Show();await Task.Delay(150);await Wait(()=>!(bool)Field(second,"_controlBusy")&&!(bool)Field(second,"_controlSetupQueued"));var legacyControls=await Await<ControlReply>(Call(second,"LoadControls"));
        Check(second.FighterEditor.TargetModel!=null&&second.FighterEditor.SelectedCHR0!=null&&legacyControls.Ok,"Legacy standalone native scene opens in Next's actual renderer and restores its saved control setup");
        var originalIds=workspace.Info.ClipIds.OrderBy(id=>id).ToArray();
        Check(converted.WorkspaceClips.Count==originalIds.Length&&converted.WorkspaceClips.Select(c=>c.Id).OrderBy(id=>id).SequenceEqual(originalIds),"Every legacy native clip keeps its exact Core identity independently of container filenames and display names");
        second.BindWorkspaceScene(scene.Id);var snapshot=Call(second,"CaptureWorkspaceScene");
        var updated=await workspace.Send(new{Operation="workspace-update",Model=Field(snapshot,"Model"),Package=Field(snapshot,"Package"),Motions=Field(snapshot,"Motions"),ClipKey=Field(snapshot,"ClipKey"),Frame=Field(snapshot,"Frame")});
        Check(updated.Diagnostics.Any(d=>d.Contains("cached Core geometry")),"A legacy geometry-cache mismatch requires fresh authentic native identity proof and an explicit preservation diagnostic");
        Check(workspace.Info.ClipIds.OrderBy(id=>id).SequenceEqual(originalIds),"Saving the native revision preserves the legacy clip catalogue without duplicates or lost identities");
        string upgraded=Path.Combine(output,"upgraded","character.fighterport");await workspace.Send(new{Operation="workspace-save",Path=upgraded});await workspace.Send(new{Operation="workspace-open",Path=upgraded});
        Check(workspace.Info.ClipIds.OrderBy(id=>id).SequenceEqual(originalIds),"An upgraded character copy fresh-reopens with all exact legacy animation identities");
        var nativeModel=(MDL0Node)second.FighterEditor.TargetModel;var vertices=((MDL0VertexNode)nativeModel.VertexList[0]).Vertices;vertices[0]._x+=1;((MDL0VertexNode)nativeModel.VertexList[0]).Vertices=vertices;
        var changed=Call(second,"CaptureWorkspaceScene");bool refused=false;
        try{await workspace.Send(new{Operation="workspace-update",Model=Field(changed,"Model"),Package=Field(changed,"Package"),Motions=Field(changed,"Motions")});}catch(InvalidOperationException e){refused=e.Message.Contains("geometry changed");}
        Check(refused,"Actual changed native geometry still rejects instead of acquiring the legacy-cache preservation path");
        Check(FighterProject.HashFile(nativePath)==before&&FighterProject.HashFile(legacyPath)==legacyHash,"Opening/migrating both project types leaves both earlier source descriptors unchanged");
        second.FighterEditor.FrameFighterModel();await CaptureWindow(second,Path.Combine(output,"legacy-native-viewer.png"));await CaptureWindow(form,Path.Combine(output,"earlier-project-viewer.png"));
        ((AnimationControlClient)Field(second,"_controlEngine")).Dispose();converted.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(second.FighterEditor);second.Dispose();((AnimationControlClient)Field(form,"_controlEngine")).Dispose();workspace.Dispose();project.Dispose();BrawlCrate.UI.Model_Previewer.ModelEditControl.ModelEditControl.Instances.Remove(form.FighterEditor);form.Dispose();GC.Collect();GC.WaitForPendingFinalizers();
    }
}
