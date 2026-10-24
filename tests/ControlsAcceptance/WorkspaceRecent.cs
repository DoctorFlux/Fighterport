using BrawlCrate.Studio;
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
    static void RecentStorageCases(string output)
    {
        string folder=Path.Combine(output,"storage-cases");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"history.json");
        var history=new WorkspaceRecentProjects(file);
        Check(history.Paths.Length==0&&!File.Exists(file),"Absent history starts empty without writing settings");
        for(int i=0;i<12;i++)history.Record(Path.Combine(folder,"project-"+i+".fpstudio"));
        Check(history.Paths.Length==10&&history.Paths[0].EndsWith("project-11.fpstudio")&&history.Paths[9].EndsWith("project-2.fpstudio"),"Recent storage has an independently specified ten-entry most-recent-first bound");
        string known=history.Paths[4];history.Record(known.ToUpperInvariant());
        Check(history.Paths.Length==10&&history.Paths[0].Equals(known,StringComparison.OrdinalIgnoreCase)&&history.Paths.Count(p=>p.Equals(known,StringComparison.OrdinalIgnoreCase))==1,"Canonical case-insensitive duplicates move to the front without duplicate entries");
        var reloaded=new WorkspaceRecentProjects(file);
        Check(reloaded.Paths.SequenceEqual(history.Paths),"A fresh store reads the exact ordered history from disk");
        var detached=history.Paths;detached[0]="changed";
        Check(history.Paths[0]!="changed","Consumers cannot mutate stored history through returned arrays");
        history.Record("relative.fpstudio");history.Record(Path.Combine(folder,"original.pac"));history.Record(null);
        Check(history.Paths.SequenceEqual(reloaded.Paths),"Relative paths, native game assets and empty entries cannot enter recent projects");
        string bytes=File.ReadAllText(file);
        using(var locked=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            history.Record(Path.Combine(folder,"session-only.fpstudio"));
            Check(history.LastError!=null&&history.Paths[0].EndsWith("session-only.fpstudio"),"Locked history reports a session-only update without throwing");
        }
        Check(File.ReadAllText(file)==bytes&&Directory.GetFiles(folder,"*.tmp").Length==0,"Failed atomic replacement preserves previous disk history and cleans its temporary file");
        history.Record(known);
        Check(history.LastError==null&&new WorkspaceRecentProjects(file).Paths.SequenceEqual(history.Paths),"A later history update recovers disk persistence after the lock is released");
        history.Forget(known);
        Check(!new WorkspaceRecentProjects(file).Paths.Contains(known,StringComparer.OrdinalIgnoreCase),"Forgetting a recent item persists its removal");
        history.Clear();Check(new WorkspaceRecentProjects(file).Paths.Length==0,"Clear persists an empty recent list");
        File.WriteAllText(file,"{ broken history");
        var corrupt=new WorkspaceRecentProjects(file);
        Check(corrupt.Paths.Length==0&&corrupt.LastError!=null&&File.ReadAllText(file)=="{ broken history","Corrupt history degrades to empty without rewriting evidence on read");
        File.WriteAllText(file,new string('x',65537));
        Check(new WorkspaceRecentProjects(file).LastError!=null,"Oversized history is bounded before parsing");
        File.WriteAllText(file,"[null,\"relative.fpstudio\"]");
        Check(new WorkspaceRecentProjects(file).Paths.Length==0,"Malformed individual history entries are ignored");
        string blocked=Path.Combine(folder,"not-a-directory");File.WriteAllText(blocked,"keep");
        var denied=new WorkspaceRecentProjects(Path.Combine(blocked,"history.json"));denied.Record(known);
        Check(denied.LastError!=null&&denied.Paths.Length==1&&File.ReadAllText(blocked)=="keep","Unavailable history directory keeps session navigation without changing the blocking file");
    }

    static async Task WorkspaceRecent(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Recent-project acceptance requires the private desktop.");
        RecentStorageCases(output);
        var originals=new[]{args[0],args[1]}.Distinct().ToDictionary(p=>p,FighterProject.HashFile);
        var history=WorkspaceRecentProjects.Current;
        string historyFile=Path.Combine(output,"workspace-state","recent-projects.json");
        Check(history.Paths.Length==0&&!File.Exists(historyFile),"Background app history starts isolated from the user's real recent projects");
        string first=Path.Combine(output,"first","Same name.fpstudio"),second=Path.Combine(output,"second","Same name.fpstudio");
        Directory.CreateDirectory(Path.GetDirectoryName(first));Directory.CreateDirectory(Path.GetDirectoryName(second));
        ModelForm form=null;
        try
        {
            using(var startup=MainForm.StartViewerApplication())
            {
                form=(ModelForm)Field(MainForm.Instance,"_fighterForm");form.WindowState=FormWindowState.Normal;form.ClientSize=new Size(1050,650);
                using(FlowModal("workspaceRecentProjects",dialog=>
                {
                    Check(!SimpleControl<ComboBox>(dialog,"workspaceRecentProjects").Enabled&&SimpleControl<TextBox>(dialog,"workspaceFilePath").Focused,"Empty recent history leaves ordinary path entry focused and usable");
                    CaptureDialog(dialog,output,"recent-empty-100.png");SimpleControl<Button>(dialog,"workspaceFileCancel").PerformClick();
                }))SimpleToolbarItem(form,"workspaceOpenProject").PerformClick();
                Check(!(bool)Field(form,"_fighterDirty")&&history.Paths.Length==0,"Cancelling empty recent navigation leaves the clean project and history untouched");
                Call(form,"ImportFighterModel",args[0]);Call(form,"ImportFighterAnimations",new[]{new[]{args[1]}});
                var editor=form.FighterEditor;editor.SelectedCHR0=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First(c=>c.Name=="Wait1");editor.SetFrame(3);await SimpleControlsReady(form);
                var setup=await Await<ControlReply>(Call(form,"LoadControls"));var model=(MDL0Node)editor.TargetModel;var clip=editor.SelectedCHR0;
                string originalModel=(string)Call(form,"NativeControlHash",model,".mdl0");
                string controls=WalkthroughMotionControls(clip,setup.ProtectedMotionBones.Select(i=>setup.BoneNames[i]));
                editor.SelectedBone=model._linker.BoneCache.Single(b=>b.Name==setup.BoneNames[setup.Roles.Single(r=>r.Role=="Hips").Bone.Value]);
                string before=LifecycleContent(clip);float value=editor.SelectedBone.FrameState._rotate._x;
                var field=editor.CHR0Editor._transBoxes[3];field.Text=(value+2).ToString(System.Globalization.CultureInfo.CurrentCulture);Call(field,"Apply");await SimpleControlsReady(form);
                string edited=LifecycleContent(clip);Check(edited!=before&&editor.CanUndo,"Recent-project navigation starts with a real native body edit and Undo");
                var priorEntry=clip.Children.OfType<CHR0EntryNode>().Single(e=>e.Name==editor.SelectedBone.Name);
                Call(editor,"interpolationEditorToolStripMenuItem_Click",null,EventArgs.Empty);
                editor.InterpolationEditor.SetTarget(priorEntry);editor.KeyframePanel.TargetSequence=priorEntry;
                form.SaveFighterProject(first);
                Check(editor.InterpolationEditor._targetNode is CHR0EntryNode curve&&curve.Parent==clip&&editor.KeyframePanel.TargetSequence.Parent==clip,"Project Save rebinds both native curve and keyframe panels to the exact saved bone entry");
                await CaptureWindow(editor._interpolationForm,Path.Combine(output,"recent-saved-curve.png"));
                editor._interpolationForm.Hide();
                form.SaveFighterProject(second);
                Check(history.Paths.SequenceEqual(new[]{second,first})&&new WorkspaceRecentProjects(historyFile).Paths.SequenceEqual(history.Paths),"Successful native Save As records both same-named projects by full path on disk");
                string firstHash=FighterProject.HashFile(first),secondHash=FighterProject.HashFile(second);
                Call(form,"MarkFighterChanged");
                int undoCount=editor._undoSaves.Count;
                foreach(int text in new[]{100,200})
                {
                    Call(form,"SetWorkspaceTextSize",text);
                    using(FlowModal("workspaceRecentProjects",dialog=>
                    {
                        var paths=SimpleControl<ComboBox>(dialog,"workspaceRecentProjects");
                        Check(paths.Items.Cast<string>().SequenceEqual(new[]{second,first})&&paths.SelectedIndex==-1,"Recent dialog shows distinct full paths without preselecting a document at "+text+"%");
                        paths.SelectedIndex=1;
                        Check(SimpleControl<TextBox>(dialog,"workspaceFilePath").Text==first&&SimpleControl<Button>(dialog,"workspaceFileAccept").Enabled&&!form.IsDisposed,"Selecting a recent project fills its exact path without opening it at "+text+"%");
                        Check(LifecycleContent(clip)==edited&&editor.CurrentFrame==3&&editor._undoSaves.Count==undoCount&&WalkthroughMotionControls(clip,setup.ProtectedMotionBones.Select(i=>setup.BoneNames[i]))==controls,"Recent selection preserves native clip/frame/Undo and protected controls at "+text+"%");
                        CaptureDialog(dialog,output,"recent-known-"+text+".png");
                        Check((bool)Call(dialog,"ProcessDialogKey",Keys.Escape),"Recent path dialog retains ordinary Escape cancellation at "+text+"%");
                    }))SimpleToolbarItem(form,"workspaceOpenProject").PerformClick();
                    Check(ReferenceEquals(KeyboardFocus(form),SimpleToolbarItem(form,"workspaceOpenProject")),"Cancelling recent navigation restores the invoking command at "+text+"%");
                }
                string[] savedHistory=history.Paths;
                using(FlowModal("unsavedProjectCancel",dialog=>SimpleControl<Button>(dialog,"unsavedProjectCancel").PerformClick()))
                using(FlowModal("workspaceRecentProjects",dialog=>
                {
                    SimpleControl<ComboBox>(dialog,"workspaceRecentProjects").SelectedItem=first;
                    SimpleControl<Button>(dialog,"workspaceFileAccept").PerformClick();
                }))SimpleToolbarItem(form,"workspaceOpenProject").PerformClick();
                Check(!form.IsDisposed&&history.Paths.SequenceEqual(savedHistory)&&LifecycleContent(clip)==edited&&editor.CanUndo,"Keep editing cancels recent-project replacement without reordering history or losing native Undo");
                string invalid=Path.Combine(output,"broken.fpstudio");File.WriteAllText(invalid,"invalid project");
                Check(!MainForm.Instance.TryOpenFighterProject(invalid,out string error)&&!string.IsNullOrEmpty(error)&&!form.IsDisposed&&history.Paths.SequenceEqual(savedHistory)&&ReferenceEquals(editor.SelectedCHR0,clip),"Real native project validation rejects corrupt input before replacement and history publication");
                using(var locked=new FileStream(second,FileMode.Open,FileAccess.Read,FileShare.None))
                {
                    bool failed=false;try{form.SaveFighterProject(second);}catch(IOException){failed=true;}
                    Check(failed&&history.Paths.SequenceEqual(savedHistory)&&new WorkspaceRecentProjects(historyFile).Paths.SequenceEqual(savedHistory),"A real failed project Save never publishes a recent-history update");
                }
                Check(LifecycleContent(clip)==edited&&editor.CanUndo,"Failed Save keeps authored native content and Undo available during recent navigation");
                using(var locked=new FileStream(historyFile,FileMode.Open,FileAccess.Read,FileShare.None))
                {
                    form.SaveFighterProject(second);
                    Check(!(bool)Field(form,"_fighterDirty")&&history.LastError!=null&&((ToolStripStatusLabel)Field(form,"_fighterStatus")).Text.Contains("Could not save"),"A successful project Save remains successful when only history persistence fails, with a visible status explanation");
                    form.ClientSize=new Size(1600,950);editor.FrameFighterModel();await CaptureSimpleWorkspace(form,Path.Combine(output,"recent-history-write-failed.png"));
                }
                form.SaveFighterProject(second);Check(history.LastError==null,"Successful Save recovers recent history after its lock is released");
                typeof(ModelForm).GetField("_retargetBusy",Private).SetValue(form,true);
                try
                {
                    Call(form,"UpdateSimpleWorkspace");
                    Check(!SimpleToolbarItem(form,"workspaceOpenProject").Enabled,"Open project including recent navigation is disabled while a retarget is busy");
                    bool guarded=false;try{Call(form,"RunSimpleWorkspaceAction","open-project");}catch(System.Reflection.TargetInvocationException ex){guarded=ex.InnerException is InvalidOperationException;}
                    Check(guarded,"Programmatic recent-project invocation retains the workspace busy guard");
                }
                finally { typeof(ModelForm).GetField("_retargetBusy",Private).SetValue(form,false);Call(form,"UpdateSimpleWorkspace"); }
                File.Move(first,first+".moved");
                using(FlowModal("workspaceRecentProjects",dialog=>
                {
                    var paths=SimpleControl<ComboBox>(dialog,"workspaceRecentProjects");paths.SelectedItem=first;
                    Check(!SimpleControl<Button>(dialog,"workspaceFileAccept").Enabled&&SimpleControl<Label>(dialog,"workspaceFileValidation").Text.Contains("not found"),"A moved recent project shows the normal unavailable-path status and cannot be opened");
                    paths.Focus();Check((bool)Call(dialog,"ProcessDialogKey",Keys.Tab)&&SimpleControl<Button>(dialog,"workspaceForgetRecent").Focused,"Recent selector Tab reaches Forget selected");
                    Check((bool)Call(dialog,"ProcessDialogKey",Keys.Tab)&&SimpleControl<Button>(dialog,"workspaceClearRecent").Focused,"Recent history Tab reaches Clear history");
                    CaptureDialog(dialog,output,"recent-missing-200.png");
                    SimpleControl<Button>(dialog,"workspaceForgetRecent").PerformClick();
                    Check(!history.Paths.Contains(first)&&File.Exists(first+".moved")&&FighterProject.HashFile(first+".moved")==firstHash,"Forget removes only history and leaves the moved project bytes untouched");
                    SimpleControl<Button>(dialog,"workspaceClearRecent").PerformClick();
                    Check(history.Paths.Length==0&&SimpleControl<TextBox>(dialog,"workspaceFilePath").Focused&&File.Exists(second),"Clear affects only history and restores path-entry focus");
                    SimpleControl<Button>(dialog,"workspaceFileCancel").PerformClick();
                }))SimpleToolbarItem(form,"workspaceOpenProject").PerformClick();
                File.Move(first+".moved",first);
                Check(new WorkspaceRecentProjects(historyFile).Paths.Length==0&&FighterProject.HashFile(first)==firstHash&&FighterProject.HashFile(second)==secondHash,"Forget/Clear persist across a fresh store without deleting or rewriting either project");
                Check(LifecycleContent(clip)==edited&&editor.CurrentFrame==3&&editor.CanUndo&&WalkthroughMotionControls(clip,setup.ProtectedMotionBones.Select(i=>setup.BoneNames[i]))==controls,"Missing-path and history management preserve exact native work, frame, Undo and root controls");
                editor.Undo();await SimpleControlsReady(form);Check(LifecycleContent(clip)==before,"Exact body Undo remains valid after recent selection, failed saves and history management");
                editor.Redo();await SimpleControlsReady(form);Check(LifecycleContent(clip)==edited,"Exact body Redo remains valid after recent navigation");
                form.SaveFighterProject(second);
                Call(form,"MarkFighterChanged");
                using(FlowModal("unsavedProjectDiscard",dialog=>SimpleControl<Button>(dialog,"unsavedProjectDiscard").PerformClick()))
                using(FlowModal("workspaceRecentProjects",dialog=>FlowAcceptFile(dialog,first)))SimpleToolbarItem(form,"workspaceOpenProject").PerformClick();
                Check(form.IsDisposed,"Confirmed project replacement still uses the existing native lifecycle");
                form=(ModelForm)Field(MainForm.Instance,"_fighterForm");await Wait(()=>form.FighterEditor.TargetModel!=null);await SimpleControlsReady(form);
                File.WriteAllText(Path.Combine(output,"expected-edited.json"),edited);
                Check(form.FighterEditor.SelectedCHR0!=null,"Saved native clip selection resolves on actual reopen");
                File.WriteAllText(Path.Combine(output,"actual-reopened.json"),LifecycleContent(form.FighterEditor.SelectedCHR0));
                Check(form.FighterProject.ManifestPath==first&&form.FighterEditor.CurrentFrame==3&&LifecycleContent(form.FighterEditor.SelectedCHR0)==edited,"Actual Open restores the exact same-named saved project, authored clip and frame");
                Check(history.Paths.SequenceEqual(new[]{first,second})&&new WorkspaceRecentProjects(historyFile).Paths.SequenceEqual(history.Paths),"Only the successful native Open moves the exact saved path to the front on disk");
                form.ClientSize=new Size(1600,950);form.FighterEditor.FrameFighterModel();await CaptureSimpleWorkspace(form,Path.Combine(output,"recent-reopened.png"));
                Check((string)Call(form,"NativeControlHash",form.FighterEditor.TargetModel,".mdl0")==originalModel&&originals.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Recent-project operations preserve authentic model data and all supplied original bytes");
                File.WriteAllText(Path.Combine(output,"recent-scope.txt"),"Application handlers and isolated actual history disk I/O on a private desktop. No OS input injection or active-screen capture. Physical input/DPI, human usability and concurrent-process history merging remain unverified. No project schema or solver/root policy change.");
            }
        }
        finally { if(form!=null&&!form.IsDisposed)SimpleDisposeForm(form); }
    }
}
