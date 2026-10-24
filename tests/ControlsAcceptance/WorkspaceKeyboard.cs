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
    // Invoke the application's actual key-routing methods, never SendKeys,
    // SendInput, PostMessage or an OS keyboard hook on the user's desktop.
    static bool WorkspaceKey(ModelForm form, Keys key) => (bool)Call(form,"ProcessCmdKey",default(Message),key);
    static object KeyboardFocus(ModelForm form) => Call(form,"CurrentWorkspaceFocus");
    static bool KeyboardRegion(ModelForm form, string name)
    {
        var root=SimpleControl<Control>(form,name);
        return root.ContainsFocus;
    }
    static void FocusWorkspaceItem(ModelForm form,string name) => Call(form,"RestoreWorkspaceFocus",SimpleToolbarItem(form,name));
    static void ButtonSpace(Button button)
    {
        button.Focus();
        Call(button,"OnKeyDown",new KeyEventArgs(Keys.Space));
        Call(button,"OnKeyUp",new KeyEventArgs(Keys.Space));
    }
    static async Task WorkspaceKeyboard(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Keyboard acceptance requires the private desktop.");
        string previous=Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",Path.Combine(output,"isolated-library"));
        var originals=new[]{args[0],args[1]}.Distinct().ToDictionary(p=>p,FighterProject.HashFile);
        ModelForm form=null;
        try
        {
            using(var startup=MainForm.StartViewerApplication())
            {
                form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
                form.WindowState=FormWindowState.Normal;form.ClientSize=new Size(1600,950);
                await Task.Delay(150);await SimpleControlsReady(form);
                Call(form,"ImportFighterModel",args[0]);Call(form,"ImportFighterAnimations",new[]{new[]{args[1]}});
                var editor=form.FighterEditor;
                editor.SelectedCHR0=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First(c=>c.Name=="Wait1");
                await SimpleControlsReady(form);var setup=await Await<ControlReply>(Call(form,"LoadControls"));
                editor.SetFrame(3);await SimpleControlsReady(form);
                var model=(MDL0Node)editor.TargetModel;var clip=editor.SelectedCHR0;
                string content=LifecycleContent(clip),modelHash=(string)Call(form,"NativeControlHash",model,".mdl0");
                bool undo=editor.CanUndo;
                Check(Field(form,"_library")==null && editor.TargetModel!=null,"Native model and clip editing needs no registered donor");

                foreach(int percent in new[]{100,200})
                foreach(var size in new[]{new Size(1050,650),new Size(1600,950)})
                {
                    Call(form,"SetWorkspaceTextSize",percent);form.ClientSize=size;
                    SimpleToolbarItem(form,"workspaceStagePreview").PerformClick();
                    Call(form,"RestoreWorkspaceFocus",editor.ModelPanel);
                    string state=percent+"% "+size;
                    foreach(string region in new[]{"fighterToolbar","workspaceActions","workspaceNavigation","workspaceTaskPane"})
                    {
                        Check(WorkspaceKey(form,Keys.F6),"F6 is handled: "+region+" "+state);
                        Check(KeyboardRegion(form,region),"F6 enters the expected visible region: "+region+" "+state);
                    }
                    Check(WorkspaceKey(form,Keys.F6)&&editor.ContainsFocus,"F6 returns to the same full native editor: "+state);
                    foreach(string region in new[]{"workspaceTaskPane","workspaceNavigation","workspaceActions","fighterToolbar"})
                        Check(WorkspaceKey(form,Keys.Shift|Keys.F6)&&KeyboardRegion(form,region),"Shift+F6 reverses regions: "+region+" "+state);
                    Check(WorkspaceKey(form,Keys.Shift|Keys.F6)&&editor.ContainsFocus,"Reverse cycle returns to the native editor: "+state);

                    FocusWorkspaceItem(form,"workspaceTextSize");
                    Check(WorkspaceKey(form,Keys.F6)&&KeyboardRegion(form,"workspaceNavigation"),"F6 leaves a selected toolbar item: "+state);
                    Check(WorkspaceKey(form,Keys.Shift|Keys.F6)&&ReferenceEquals(KeyboardFocus(form),SimpleToolbarItem(form,"workspaceTextSize")),"Returning to a region remembers its selected command: "+state);
                    FocusWorkspaceItem(form,"fighterName");
                    Check(WorkspaceKey(form,Keys.Tab)&&ReferenceEquals(KeyboardFocus(form),SimpleToolbarItem(form,"fighterSave")),"Tab leaves the hosted project-name field for Save: "+state);
                    Check(WorkspaceKey(form,Keys.Shift|Keys.Tab)&&SimpleToolbarItem(form,"fighterName") is ToolStripControlHost host&&host.Control.ContainsFocus,"Shift+Tab restores the hosted name field: "+state);

                    SimpleToolbarItem(form,"workspaceStagePreview").PerformClick();
                    var primary=SimpleControl<Button>(form,"workspacePrimaryAction");
                    Check(primary.Focused,"Selecting a stage focuses its primary action: "+state);
                    Check(WorkspaceKey(form,Keys.Tab)&&SimpleControl<LinkLabel>(form,"workspaceSourceSummary").Focused,"Task Tab order starts with primary action then source summary: "+state);
                    Check(WorkspaceKey(form,Keys.Shift|Keys.Tab)&&primary.Focused,"Task reverse Tab returns to primary: "+state);
                    var details=SimpleControl<Button>(form,"workspaceDetailsAction");details.Focus();
                    Check(InClient(details,SimpleControl<Control>(form,"workspaceTaskScroll")),"Keyboard focus scrolls task actions into view: "+state);
                    await CaptureSimpleWorkspace(form,Path.Combine(output,"keyboard-task-"+percent+"-"+size.Width+".png"));
                    Check(WorkspaceKey(form,Keys.Escape)&&!SimpleControl<Control>(form,"workspaceTaskPane").Visible,"Escape closes focused tasks: "+state);
                    Check(ReferenceEquals(KeyboardFocus(form),SimpleToolbarItem(form,"workspaceStagePreview")),"Closing tasks returns to the stage invoker: "+state);
                    Check(WorkspaceKey(form,Keys.F6)&&editor.ContainsFocus,"Region traversal skips hidden tasks: "+state);
                    Check(!WorkspaceKey(form,Keys.Escape)&&editor.ContainsFocus,"Workspace Escape leaves native editor cancellation untouched: "+state);
                    SimpleToolbarItem(form,"workspaceWorkflow").PerformClick();
                    Check(SimpleControl<Control>(form,"workspaceTaskPane").ContainsFocus,"Workflow explicitly recovers and focuses tasks: "+state);
                    SimpleToolbarItem(form,"workspaceFocusEditor").PerformClick();
                    Check(editor.ModelPanel.ContainsFocus,"Focus editor focuses the original viewport: "+state);
                    SimpleToolbarItem(form,"workspaceFocusEditor").PerformClick();
                    Check(SimpleControl<Control>(form,"workspaceTaskPane").ContainsFocus,"Show workflow focuses the recovered task: "+state);

                    Check(ReferenceEquals(editor.TargetModel,model)&&ReferenceEquals(editor.SelectedCHR0,clip)&&editor.CurrentFrame==3&&editor.CanUndo==undo&&LifecycleContent(clip)==content,
                        "Keyboard navigation preserves exact native document/clip/frame/Undo/keys: "+state);
                    Check(setup.ProtectedMotionBones.All(i=>!editor.CanEditAnimationBone(setup.BoneNames[i])),"Keyboard navigation preserves source-only motion-control guards: "+state);
                }

                var frameField=editor.PlaybackPanel.numFrameIndex;frameField.Focus();
                Console.WriteLine("TRACE frame field enabled="+frameField.Enabled+" visible="+frameField.Visible+" focus="+frameField.ContainsFocus+" actual="+KeyboardFocus(form));
                WorkspaceKey(form,Keys.F6);
                for(int hop=0;hop<6&&!editor.ContainsFocus;hop++) WorkspaceKey(form,Keys.F6);
                Console.WriteLine("TRACE returned native focus="+KeyboardFocus(form));
                Check(frameField.ContainsFocus,"Returning to the native region remembers its exact frame field");
                using(var dialog=new WorkspaceFileDialog("Native focus recovery", "Cancel without changing native editing focus.", "Model|*.pac", "Open"))
                using(FlowModal("workspaceFileCancel",shown=>Call(shown,"ProcessDialogKey",Keys.Escape)))dialog.ShowDialog(form);
                Check(frameField.ContainsFocus,"Cancelling a dialog invoked from the native editor restores the exact native field");
                var toolbar=(ToolStrip)Field(form,"_simpleWorkspaceToolbar");
                toolbar.Enabled=false;Call(form,"RestoreWorkspaceFocus",editor.ModelPanel);
                Check(WorkspaceKey(form,Keys.F6)&&KeyboardRegion(form,"workspaceActions"),"F6 skips a disabled region");toolbar.Enabled=true;
                Call(form,"SetSimpleWorkspaceStage",0);
                Check(FlowRegisterSource(form,args[0],args[1],true),"First-use source registration remains reachable through task actions");
                await FlowLibrarySettled(form);
                var library=(AnimationLibrary)Field(form,"_library");await FlowSelectSource(form,library,"Wait1");
                SimpleToolbarItem(form,"workspaceSourceLibrary").PerformClick();
                if(!SimpleControl<Control>(form,"animationLibraryPane").Visible)SimpleToolbarItem(form,"workspaceSourceLibrary").PerformClick();
                var search=SimpleControl<TextBox>(form,"librarySearch");
                Check(search.Focused,"Opening the library focuses search");
                Check(!SimpleControl<Control>(form,"workspaceTaskPane").Visible && editor.Width>=888,"At 200% the library yields task space to the intact native editor");
                Check(WorkspaceKey(form,Keys.Tab)&&SimpleControl<Button>(form,"libraryClearSearch").Focused,"Library Tab proceeds from search to Clear");
                Check(WorkspaceKey(form,Keys.Tab)&&SimpleControl<TreeView>(form,"libraryClips").Focused,"Library Tab proceeds from Clear to source tree");
                Check(WorkspaceKey(form,Keys.Shift|Keys.Tab)&&SimpleControl<Button>(form,"libraryClearSearch").Focused,"Library reverse Tab returns to Clear");
                Check(WorkspaceKey(form,Keys.Shift|Keys.Tab)&&search.Focused,"Library reverse Tab returns to search");
                foreach(Keys key in new[]{Keys.A,Keys.Space,Keys.Control|Keys.A,Keys.Control|Keys.Z})
                    Check(!WorkspaceKey(form,key),"Workspace adds no typing/editing interception: "+key);
                search.Text="Wait";await FlowLibrarySettled(form);
                ButtonSpace(SimpleControl<Button>(form,"libraryClearSearch"));
                Check(search.Text=="","Ordinary focused Button Space handlers activate Clear search");await FlowLibrarySettled(form);
                search.Focus();await CaptureSimpleWorkspace(form,Path.Combine(output,"keyboard-library-200.png"));
                Check(WorkspaceKey(form,Keys.Escape)&&!SimpleControl<Control>(form,"animationLibraryPane").Visible,"Escape closes the focused source library");
                Check(ReferenceEquals(KeyboardFocus(form),SimpleToolbarItem(form,"workspaceSourceLibrary")),"Library close restores its toolbar invoker");
                SimpleToolbarItem(form,"workspaceSourceLibrary").PerformClick();await FlowLibrarySettled(form);
                await FlowSelectSource(form,library,"Wait1");
                Check(((TreeView)Field(form,"_libraryList")).SelectedNode?.Tag is LibrarySearchItem,"Known registered source is reused without another association dialog");
                Check(WorkspaceKey(form,Keys.Escape),"Reused library closes through its keyboard route");

                bool shown=false;
                using(FlowModal("workspaceFilePath",dialog=>
                {
                    shown=true;var path=SimpleControl<TextBox>(dialog,"workspaceFilePath");path.Focus();
                    Check(!WorkspaceKey(form,Keys.F6)&&path.Focused,"A modal dialog prevents owner-region keyboard routing");
                    Check((bool)Call(dialog,"ProcessDialogKey",Keys.Tab)&&SimpleControl<Button>(dialog,"workspaceFileBrowse").Focused,"File dialog Tab reaches Browse");
                    Check((bool)Call(dialog,"ProcessDialogKey",Keys.Tab)&&SimpleControl<Button>(dialog,"workspaceFileCancel").Focused,"Invalid-file Tab skips disabled Open and reaches Cancel");
                    CaptureDialog(dialog,output,"keyboard-path-dialog-200.png");
                    Check((bool)Call(dialog,"ProcessDialogKey",Keys.Escape),"File dialog retains its normal Escape cancellation");
                }))SimpleToolbarItem(form,"workspaceOpenProject").PerformClick();
                Check(shown&&ReferenceEquals(KeyboardFocus(form),SimpleToolbarItem(form,"workspaceOpenProject")),"Cancelled project dialog restores its invoking toolbar command");

                using(FlowModal("unsavedProjectCancel",dialog=>
                {
                    var cancel=SimpleControl<Button>(dialog,"unsavedProjectCancel");
                    Check(cancel.Focused,"Unsaved decisions initially focus Keep editing");
                    Check((bool)Call(dialog,"ProcessDialogKey",Keys.Tab)&&SimpleControl<Button>(dialog,"unsavedProjectSave").Focused,"Unsaved Tab wraps to Save in declared order");
                    Check((bool)Call(dialog,"ProcessDialogKey",Keys.Tab)&&SimpleControl<Button>(dialog,"unsavedProjectDiscard").Focused,"Unsaved Tab proceeds to Discard");
                    CaptureDialog(dialog,output,"keyboard-unsaved-dialog-200.png");
                    Check((bool)Call(dialog,"ProcessDialogKey",Keys.Escape),"Unsaved Escape keeps editing");
                }))SimpleToolbarItem(form,"workspaceNewProject").PerformClick();
                Check(!form.IsDisposed&&ReferenceEquals(KeyboardFocus(form),SimpleToolbarItem(form,"workspaceNewProject")),"Keep editing restores New project command focus without replacing the document");
                using(var dialog=new WorkspaceFileDialog("Keyboard file handler", "Accept an existing path without changing the native document.", "Model|*.pac", "Open", args[0]))
                using(FlowModal("workspaceFilePath", shownDialog=>
                {
                    SimpleControl<TextBox>(shownDialog,"workspaceFilePath").Focus();
                    Check((bool)Call(shownDialog,"ProcessDialogKey",Keys.Enter),"Normal dialog Enter accepts a valid path");
                }))
                    Check(dialog.ShowDialog(form)==DialogResult.OK && dialog.SelectedPath==Path.GetFullPath(args[0]),"Enter returns the exact chosen file without invoking another document operation");
                using(FlowModal("unsavedProjectSave",dialog=>
                {
                    var save=SimpleControl<Button>(dialog,"unsavedProjectSave");save.Focus();
                    using(FlowModal("workspaceFilePath",path=>
                    {
                        Check((bool)Call(path,"ProcessDialogKey",Keys.Escape),"Nested Save path keeps normal Escape cancellation");
                    }))ButtonSpace(save);
                    Check(dialog.Visible&&save.Focused&&SimpleControl<Label>(dialog,"unsavedProjectStatus").Text.Contains("cancelled"),"Cancelled nested Save returns focus to the Save decision and keeps the project open");
                    Check((bool)Call(dialog,"ProcessDialogKey",Keys.Escape),"Unsaved decision remains keyboard-cancellable after nested Save");
                }))SimpleToolbarItem(form,"workspaceNewProject").PerformClick();
                using(var stale=new Button())
                {
                    form.Controls.Add(stale);stale.Focus();
                    Call(form,"WithWorkspaceInvoker",stale,new Action(()=>Call(form,"SetSimpleWorkspaceStage",2)));
                    stale.Dispose();
                    Check(WorkspaceKey(form,Keys.Escape)&&editor.ModelPanel.ContainsFocus,"A disposed drawer invoker falls back to the original native viewport");
                }
                Check(ReferenceEquals(editor.TargetModel,model)&&ReferenceEquals(editor.SelectedCHR0,clip)&&editor.CurrentFrame==3&&LifecycleContent(clip)==content&&editor.CanUndo==undo,
                    "All registration, reuse and dialog keyboard cases preserve the existing native work");
                Check((string)Call(form,"NativeControlHash",model,".mdl0")==modelHash&&originals.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Keyboard workflow preserves original fixture bytes and authentic target model data");
                File.WriteAllText(Path.Combine(output,"keyboard-scope.txt"),"Application key-routing/focus and standard control handlers on private desktop. No OS input injection. Actual physical keyboard/mouse, monitor DPI and human task outcomes remain unverified.");
                Call(form,"SetWorkspaceTextSize",100);
            }
        }
        finally
        {
            if(form!=null&&!form.IsDisposed){Call(form,"DisposeAnimationLibrary");SimpleDisposeForm(form);}
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",previous);
        }
    }
}
