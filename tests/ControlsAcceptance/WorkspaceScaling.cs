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
    static bool InClient(Control control,Control owner)
    {
        var bounds=owner.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
        return owner.ClientRectangle.Contains(bounds);
    }
    static async Task WorkspaceDialogScaling(string[] args,string output)
    {
        using(var project=FighterProject.Create("Large workspace dialog review"))
        using(var form=new ModelForm())
        {
            form.InitializeFighterProject(project);form.Show();await Task.Delay(150);
            Call(form,"SetWorkspaceTextSize",175);
            using(var dialog=new WorkspaceFileDialog("Open Fighter Project","Resume a saved Fighterport project in the full native Model Previewer.","Fighterport project (*.fpstudio)|*.fpstudio","Open project"))
            using(FlowModal("workspaceFilePath",shown=>
            {
                var browse=SimpleControl<Button>(shown,"workspaceFileBrowse");
                Console.WriteLine("TRACE dialog browse "+browse.Bounds+" parent "+browse.Parent.ClientRectangle);
                Check(InClient(browse,browse.Parent),"Browse fits the scaled picker row");
                CaptureDialog(shown,output,"dialog-path-175.png");
                SimpleControl<Button>(shown,"workspaceFileCancel").PerformClick();
            }))dialog.ShowDialog(form);
            using(var dialog=new UnsavedProjectDialog(project.Name,()=>Task.FromResult("Could not save: this is a layout-only error-text fixture.")))
            using(FlowModal("unsavedProjectSave",shown=>
            {
                Check(new[]{"unsavedProjectSave","unsavedProjectDiscard","unsavedProjectCancel"}.All(name=>InClient(SimpleControl<Button>(shown,name),shown)),"All scaled unsaved decisions remain in the dialog");
                SimpleControl<Button>(shown,"unsavedProjectSave").PerformClick();
                CaptureDialog(shown,output,"dialog-unsaved-error-175.png");
                SimpleControl<Button>(shown,"unsavedProjectCancel").PerformClick();
            }))dialog.ShowDialog(form);
            Call(form,"SetWorkspaceTextSize",100);SimpleDisposeForm(form);
        }
    }
    static async Task WorkspaceScaling(string[] args,string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Scaling capture requires the private desktop.");
        string previousLibrary=Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",Path.Combine(output,"isolated-library"));
        var originals=new[]{args[0],args[1]}.Distinct().ToDictionary(p=>p,FighterProject.HashFile);
        ModelForm form=null;
        try
        {
            using(var startup=MainForm.StartViewerApplication())
            {
                form=(ModelForm)Field(MainForm.Instance,"_fighterForm");
                form.WindowState=FormWindowState.Normal;form.ClientSize=new Size(1320,850);
                await Task.Delay(150);await SimpleControlsReady(form);
                // Exercise the optional guided surface explicitly; the normal
                // Next layout and its direct toolbar have dedicated coverage.
                Call(form,"SetSimpleWorkspaceStage",0);
                Call(form,"ImportFighterModel",args[0]);Call(form,"ImportFighterAnimations",new[]{new[]{args[1]}});
                var editor=form.FighterEditor;
                editor.SelectedCHR0=editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First(c=>c.Name=="Wait1");editor.SetFrame(3);
                await SimpleControlsReady(form);
                var setup=await Await<ControlReply>(Call(form,"LoadControls"));
                await Task.Delay(100);editor.SetFrame(3);await SimpleControlsReady(form);
                var model=(MDL0Node)editor.TargetModel;var clip=editor.SelectedCHR0;var viewport=editor.ModelPanel;
                var nativeFont=editor.Font;var bones=editor.rightPanel;var animations=editor.leftPanel;
                string modelHash=(string)Call(form,"NativeControlHash",model,".mdl0");string content=LifecycleContent(clip);
                bool undo=editor.CanUndo;
                // A synthetic long caption tests wrapping and ellipsis without
                // changing source identities or actual game asset names.
                form.FighterProject.Name="Workspace scaling and long project title review";
                int sequence=0;
                foreach(int percent in new[]{100,125,150,175,100,175})
                {
                    var sizeMenu=(ToolStripDropDownButton)SimpleToolbarItem(form,"workspaceTextSize");
                    sizeMenu.DropDownItems.Cast<ToolStripMenuItem>().Single(i=>i.Name=="workspaceText"+percent).PerformClick();
                    Check(WorkspaceAppearance.TextPercent==percent && sizeMenu.DropDownItems.Cast<ToolStripMenuItem>().Count(i=>i.Checked)==1,
                        "Actual text-size menu selects exactly one preset: "+percent);
                    foreach(var size in new[]{new Size(1050,650),new Size(1320,850),new Size(1600,950)})
                    {
                        form.ClientSize=size;form.PerformLayout();await Task.Delay(40);
                        string state=percent+" at "+size.Width+"x"+size.Height;
                        var task=SimpleControl<Control>(form,"workspaceTaskPane");
                        if(!task.Visible)SimpleToolbarItem(form,"workspaceWorkflow").PerformClick();
                        if(task.Dock==DockStyle.None)
                        {
                            var hit=task.Parent.GetChildAtPoint(new Point(task.Right-8,task.Top+8));
                            if(hit!=task)
                            {
                                Console.WriteLine("TRACE overlay failure "+state+" task="+task.Bounds+" body="+task.Parent.ClientRectangle+" childIndex="+task.Parent.Controls.GetChildIndex(task)+" hit="+hit?.Name+" cache="+Field(form,"_workspaceLayoutSize"));
                                await CaptureSimpleWorkspace(form,Path.Combine(output,"scaling-overlay-failure.png"));
                            }
                            Check(hit==task,"Compact workflow is the actual frontmost control for interaction: "+state);
                        }
                        Check(task.Visible && InClient(SimpleControl<Button>(form,"workspacePrimaryAction"),task),
                            "Primary task action remains visible above the scrolling details: "+state);
                        Check(editor.Width>=760 && editor.ModelPanel.Width>250 && ReferenceEquals(editor.ModelPanel,viewport) &&
                            ReferenceEquals(editor.rightPanel,bones) && ReferenceEquals(editor.leftPanel,animations) && editor.Font.Equals(nativeFont),
                            "Same full native editor and original fonts retain usable room: "+state);
                        foreach(string name in new[]{"workspaceWorkflow","workspaceSourceLibrary","workspaceFocusEditor","workspaceMoreTools","workspaceOpenProject","workspaceNewProject","workspaceTextSize"})
                        {
                            var item=SimpleToolbarItem(form,name);
                            Check(item.Visible && item.Placement==ToolStripItemPlacement.Main && item.Owner.ClientRectangle.Contains(item.Bounds),
                                "Essential command remains visible: "+name+" "+state);
                        }
                        foreach(string stage in new[]{"Source","Target","Preview","Export"})
                        {
                            var button=SimpleToolbarItem(form,"workspaceStage"+stage);button.PerformClick();
                            Check(button.Owner.ClientRectangle.Contains(button.Bounds) && SimpleControl<Control>(form,"workspaceTaskPane").Visible,
                                "Stage selection opens its workflow in a compact window: "+stage+" "+state);
                        }
                        SimpleToolbarItem(form,"workspaceFocusEditor").PerformClick();
                        Check(!task.Visible && editor.Visible,"Focus editor recovers the full native editing area: "+state);
                        Call(form,"SetSimpleWorkspaceStage",3);
                        Check(task.Visible,"Explicit workflow guide restores tasks at every size: "+state);
                        Console.WriteLine("TRACE preserve "+state+" target="+ReferenceEquals(editor.TargetModel,model)+" clip="+ReferenceEquals(editor.SelectedCHR0,clip)+" frame="+editor.CurrentFrame+" undo="+editor.CanUndo+" expectedUndo="+undo);
                        Check(ReferenceEquals(editor.TargetModel,model)&&ReferenceEquals(editor.SelectedCHR0,clip)&&editor.CurrentFrame==3&&editor.CanUndo==undo,
                            "Scaling, stage and focus changes preserve the exact document/frame/Undo: "+state);
                        Check(setup.ProtectedMotionBones.All(i=>editor.CanEditAnimationBone(setup.BoneNames[i])==false),
                            "Source-only motion controls remain guarded: "+state);
                    }
                    if(percent==100||percent==150||percent==175)
                    {
                        form.ClientSize=new Size(1050,650);Call(form,"SetSimpleWorkspaceStage",2);editor.FrameFighterModel();
                        await CaptureSimpleWorkspace(form,Path.Combine(output,"scaling-"+percent+"-compact-"+sequence+".png"));
                    }
                    sequence++;
                }
                Check(LifecycleContent(clip)==content && (string)Call(form,"NativeControlHash",model,".mdl0")==modelHash,
                    "All scale/resize/focus/stage operations leave native keys and model bytes unchanged");

                form.ClientSize=new Size(1050,650);Call(form,"SetSimpleWorkspaceStage",0);
                Check(FlowRegisterSource(form,args[0],args[1],true),"Actual Add source remains reachable from the compact workflow");
                await FlowLibrarySettled(form);
                var library=(AnimationLibrary)Field(form,"_library");
                await FlowSelectSource(form,library,"Wait1");
                for(int pass=0;pass<2;pass++)
                {
                    foreach(int percent in new[]{100,150,175})
                    {
                        Call(form,"SetWorkspaceTextSize",percent);
                        if(!SimpleControl<Control>(form,"animationLibraryPane").Visible)SimpleToolbarItem(form,"workspaceSourceLibrary").PerformClick();
                        var pane=SimpleControl<Panel>(form,"animationLibraryPane");
                        var list=SimpleControl<TreeView>(form,"libraryClips");
                        var clear=SimpleControl<Button>(form,"libraryClearSearch");
                        Check(clear.Width-12>=TextRenderer.MeasureText(clear.Text,clear.Font).Width,"Clear search caption fits without clipping: "+percent+" pass="+pass);
                        if(!(pane.Visible && list.Height>=140 && editor.Width>=760))
                        {
                            var catalogue=(TableLayoutPanel)Field(form,"_sourceCatalogueLayout");
                            var scroll=(Panel)Field(form,"_sourceCatalogueScroll");
                            Console.WriteLine("TRACE library allocation failure percent="+percent+" pass="+pass+" pane="+pane.Bounds+
                                " editorWidth="+editor.Width+" listHeight="+list.Height+" listMargin="+list.Margin+" catalogueHeight="+catalogue.Height+
                                " rows="+string.Join(",",catalogue.RowStyles.Cast<RowStyle>().Select(row=>row.SizeType+":"+row.Height))+
                                " catalogueScroll="+scroll.ClientSize);
                            await CaptureSimpleWorkspace(form,Path.Combine(output,"scaling-library-failure.png"));
                        }
                        Check(pane.Visible && list.Height>=140 && editor.Width>=760,"Large library text retains a usable tree and full native editor: "+percent+" pass="+pass);
                        pane.ScrollControlIntoView(SimpleControl<Button>(form,"libraryUseOnTarget"));
                        Check(InClient(SimpleControl<Button>(form,"libraryUseOnTarget"),pane),"Library actions can be scrolled into view: "+percent+" pass="+pass);
                        if(pass==0)
                        {
                            pane.ScrollControlIntoView(list);
                            await CaptureSimpleWorkspace(form,Path.Combine(output,"scaling-library-"+percent+".png"));
                        }
                        SimpleToolbarItem(form,"workspaceWorkflow").PerformClick();
                        Check(!pane.Visible && SimpleControl<Control>(form,"workspaceTaskPane").Visible,
                            "Workflow and library drawers can be recovered without a hidden command: "+percent+" pass="+pass);
                    }
                }
                Call(form,"SetWorkspaceTextSize",175);
                SimpleToolbarItem(form,"workspaceSourceLibrary").PerformClick();await FlowLibrarySettled(form);
                SimpleControl<Button>(form,"libraryPreviewOriginal").PerformClick();
                await Wait(()=>Field(form,"_libraryLease")!=null);
                var auditionPane=SimpleControl<Panel>(form,"animationLibraryPane");
                var auditionView=SimpleControl<Control>(form,"libraryOriginalViewport");
                auditionPane.ScrollControlIntoView(auditionView);
                Check(InClient(auditionView,auditionPane),"Read-only source audition remains reachable with 175% library text");
                Check(ReferenceEquals(editor.TargetModel,model)&&ReferenceEquals(editor.SelectedCHR0,clip)&&editor.CurrentFrame==3,
                    "Scrolling and rendering the scaled source audition preserve the editable target identity/frame");
                await CaptureSimpleWorkspace(form,Path.Combine(output,"scaling-source-audition-175.png"));
                SimpleToolbarItem(form,"workspaceWorkflow").PerformClick();
                bool pathShown=false;
                using(FlowModal("workspaceFilePath",dialog=>
                {
                    pathShown=true;
                    var accept=SimpleControl<Button>(dialog,"workspaceFileAccept");
                    var cancel=SimpleControl<Button>(dialog,"workspaceFileCancel");
                    var browse=SimpleControl<Button>(dialog,"workspaceFileBrowse");
                    Console.WriteLine("TRACE browse bounds="+browse.Bounds+" parent="+browse.Parent.ClientRectangle);
                    Check(InClient(browse,browse.Parent),"Scaled Browse button fits its actual picker row");
                    Check(dialog.Font.Size>=15.5f && InClient(accept,dialog)&&InClient(cancel,dialog),"Project path dialog scales with reachable actions");
                    CaptureDialog(dialog,output,"scaling-project-dialog-175.png");cancel.PerformClick();
                }))SimpleToolbarItem(form,"workspaceOpenProject").PerformClick();
                Check(pathShown&&ReferenceEquals(editor.SelectedCHR0,clip)&&editor.CurrentFrame==3,"Cancelling the scaled project dialog retains native selection/frame");
                using(FlowModal("unsavedProjectCancel",dialog=>
                {
                    Check(dialog.Font.Size>=15.5f && new[]{"unsavedProjectSave","unsavedProjectDiscard","unsavedProjectCancel"}.All(name=>InClient(SimpleControl<Button>(dialog,name),dialog)),
                        "Scaled unsaved dialog keeps Save/Discard/Keep editing visible");
                    CaptureDialog(dialog,output,"scaling-unsaved-dialog-175.png");SimpleControl<Button>(dialog,"unsavedProjectCancel").PerformClick();
                }))SimpleToolbarItem(form,"workspaceNewProject").PerformClick();
                Check(!form.IsDisposed&&ReferenceEquals(editor.SelectedCHR0,clip)&&LifecycleContent(clip)==content,"Keep editing from a scaled dialog preserves the current native work");
                Check(originals.All(p=>FighterProject.HashFile(p.Key)==p.Value),"Scaling/usability checks preserve all original fixture bytes");
                string saved=Path.Combine(output,"scaled-project.fpstudio");form.SaveFighterProject(saved);
                Check(MainForm.Instance.NewFighterProject(),"New project succeeds after the current scaled project is saved");
                form=(ModelForm)Field(MainForm.Instance,"_fighterForm");await Task.Delay(100);
                Call(form,"SetSimpleWorkspaceStage",0);
                Check(SimpleToolbarItem(form,"workspaceTextSize").Text.Contains("175%"),"Workspace text preference survives New project without entering the fighter document");
                Call(form,"SetWorkspaceTextSize",100);
            }
        }
        finally
        {
            if(form!=null&&!form.IsDisposed){Call(form,"DisposeAnimationLibrary");SimpleDisposeForm(form);}
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",previousLibrary);
        }
    }
}
