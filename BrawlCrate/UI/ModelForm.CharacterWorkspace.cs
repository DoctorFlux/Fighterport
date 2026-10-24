using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private readonly NativeWorkspaceSession _characterWorkspace=NativeWorkspaceSession.Shared;
        private string _workspaceSceneId,_workspaceModelId;
        private Dictionary<string,string> _workspaceSavedAssets;
        private bool _workspaceBusy,_workspaceReplacing;
        private Form _workspaceWindow;
        private TreeView _workspaceTree;
        private ToolStripDropDownButton _workspaceButton;
        private sealed class WorkspaceChoice{internal string Target,Scene;}
        internal void BindWorkspaceScene(string scene){_workspaceSceneId=scene;_workspaceModelId=_fighterProject.Model?.Id;RefreshCharacterWorkspace();}
        private void InitializeCharacterWorkspace(ToolStrip toolbar)
        {
            _workspaceButton=new ToolStripDropDownButton("Workspace"){Name="fighterWorkspace"};
            _workspaceButton.DropDownItems.Add("Character and Targets...",null,(s,e)=>ShowCharacterWorkspace());
            _workspaceButton.DropDownItems.Add("Save Character Workspace",null,(s,e)=>WorkspaceAction(SaveCharacterWorkspace));
            _workspaceButton.DropDownItems.Add("Save Character Workspace As...",null,(s,e)=>WorkspaceAction(()=>SaveCharacterWorkspaceCopy(true)));
            toolbar.Items.Add(_workspaceButton);
        }
        private async void WorkspaceAction(Func<Task> action)
        {
            if(_workspaceBusy||_controlBusy||_retargetBusy){_fighterStatus.Text="Finish the current edit before changing the workspace.";return;}
            _workspaceBusy=true;modelEditControl1.StopAnim();modelEditControl1.Enabled=false;
            try{await action();}
            catch(Exception e){if(!IsDisposed)_fighterStatus.Text="Workspace: "+e.Message;}
            finally{_workspaceBusy=false;if(!IsDisposed){modelEditControl1.Enabled=true;RefreshCharacterWorkspace();if(_controlReloadRequested)QueueControlSetup();}}
        }
        private sealed class WorkspaceSnapshot
        {
            internal string Model,Package,DocumentHash,ClipKey;internal int Frame;internal object[] Motions,Slots;internal FighterProject Owner;internal string ModelId;internal Dictionary<string,string> Assets;
        }
        private WorkspaceSnapshot CaptureWorkspaceScene()
        {
            if(_pinGesture!=null)throw new InvalidOperationException("Finish or cancel the control gesture first.");
            FinishControlCurveEdit();var model=modelEditControl1.TargetModel as MDL0Node??throw new InvalidOperationException("Open a native model to add this scene.");
            string stage=_characterWorkspace.NewFolder(),modelPath=Path.Combine(stage,"model.mdl0"),packageFolder=Path.Combine(stage,"document"),package=Path.Combine(stage,"editor.zip");Directory.CreateDirectory(packageFolder);
            model.ExportChecked(modelPath);var motions=new List<object>();var hashes=new Dictionary<string,string>();
            foreach(var asset in RegisteredFighterAssets())
            {
                var root=FighterRoot(asset);root.ExportChecked(asset.WorkingPath);
                hashes.Add(asset.Id,FighterProject.HashFile(asset.WorkingPath));
                var clips=DiscoverFighterAnimations(root).OfType<CHR0Node>().ToArray();if(clips.Length==0)continue;
                string path=Path.Combine(stage,asset.Id+Path.GetExtension(asset.WorkingPath));root.ExportChecked(path);
                motions.Add(new{Path=path,Clips=clips.Select(c=>new{Entry=WorkspaceNativeIdentity(c),Name=c.Name,Key=FighterAnimationKey(asset,c),Id=_fighterProject.WorkspaceClips.SingleOrDefault(link=>link.Key==FighterAnimationKey(asset,c))?.Id}).ToArray()});
            }
            _fighterProject.SelectedAnimation=SelectedFighterAnimationKey();_fighterProject.Frame=modelEditControl1.CurrentFrame;_fighterProject.ModelNodePath=model.TreePath;
            _fighterProject.SaveCopy(Path.Combine(packageFolder,"project.fpstudio"));ZipFile.CreateFromDirectory(packageFolder,package,CompressionLevel.Optimal,false);
            return new WorkspaceSnapshot{Model=modelPath,Package=package,Motions=motions.ToArray(),Slots=_fighterProject.AnimationSlots.Select(s=>(object)new{Id=s.Id,ResourceKey=s.AnimationKey,s.Required,s.Protected,ProfileKey=s.ProfileId==null?null:s.ProfileId+":"+s.NativeEntry,ReviewStatus=AnimationSlotStatus(s)}).ToArray(),Owner=_fighterProject,ModelId=_fighterProject.Model.Id,DocumentHash=_fighterProject.DocumentIdentity(),Assets=hashes,ClipKey=SelectedFighterAnimationKey(),Frame=Math.Max(0,modelEditControl1.CurrentFrame-1)};
        }
        private async Task AddCurrentToWorkspace(string target)
        {
            if(_characterWorkspace.Info==null)throw new InvalidOperationException("Create or open a character workspace first.");
            var snapshot=CaptureWorkspaceScene();await _characterWorkspace.Send(new{Operation="workspace-select",TargetId=target});
            await _characterWorkspace.Send(new{Operation="workspace-add",Model=snapshot.Model,Package=snapshot.Package,Motions=snapshot.Motions,Slots=snapshot.Slots,Name=snapshot.Owner.Name,snapshot.ClipKey,snapshot.Frame});
            await _characterWorkspace.Send(new{Operation="workspace-save"});
            if(!IsDisposed&&ReferenceEquals(_fighterProject,snapshot.Owner)&&_fighterProject.Model?.Id==snapshot.ModelId){BindWorkspaceScene(_characterWorkspace.Info.ActiveScene);MarkWorkspaceSnapshotSaved(snapshot);}
            _fighterStatus.Text="Independent character scene saved. Native sources and control state are preserved.";
        }
        private static string WorkspaceNativeIdentity(ResourceNode node)
        {
            var parts=new List<string>();while(node.Parent!=null){parts.Add(node.Index.ToString(System.Globalization.CultureInfo.InvariantCulture));node=node.Parent;}parts.Reverse();return "root"+(parts.Count==0?"":"/"+string.Join("/",parts));
        }
        private Task SaveCharacterWorkspace()=>SaveCharacterWorkspaceCopy(false);
        private async Task SaveCharacterWorkspaceCopy(bool copy)
        {
            if(_characterWorkspace.Info==null)throw new InvalidOperationException("Create or open a character workspace first.");
            WorkspaceSnapshot committed=null;
            if(_workspaceSceneId!=null)
            {
                if(_workspaceModelId!=_fighterProject.Model?.Id)throw new InvalidOperationException("This model changed. Use Add Current Model to save it as an independent scene.");
                var scene=_characterWorkspace.Info.Scenes.Single(s=>s.Id==_workspaceSceneId);var snapshot=CaptureWorkspaceScene();
                await _characterWorkspace.Send(new{Operation="workspace-select",TargetId=scene.Target,SceneId=scene.Id});
                var reply=await _characterWorkspace.Send(new{Operation="workspace-update",Model=snapshot.Model,Package=snapshot.Package,Motions=snapshot.Motions,Slots=snapshot.Slots,snapshot.ClipKey,snapshot.Frame});
                _fighterProject.Warnings.AddRange(reply.Diagnostics??new string[0]);
                committed=snapshot;
            }
            string destination=null;
            if(copy||_characterWorkspace.Info.RequiresUpgradeCopy)using(var dialog=new SaveFileDialog{Title="Save character workspace copy",Filter="FighterPort character (*.fighterport)|*.fighterport",DefaultExt="fighterport",AddExtension=true,FileName=_characterWorkspace.Info.Name+".fighterport"}){if(dialog.ShowDialog(this)!=DialogResult.OK)throw new OperationCanceledException("The current workspace remains open.");destination=dialog.FileName;}
            await _characterWorkspace.Send(new{Operation="workspace-save",Path=destination});
            if(committed!=null)
            {
                MarkWorkspaceSnapshotSaved(committed);
            }
            _fighterStatus.Text="Character workspace saved with independent target revisions and native editor state.";
        }
        private void MarkWorkspaceSnapshotSaved(WorkspaceSnapshot committed)
        {
            bool current=!IsDisposed&&ReferenceEquals(_fighterProject,committed.Owner)&&_fighterProject.DocumentIdentity()==committed.DocumentHash;
            if(current)foreach(var asset in RegisteredFighterAssets()){FighterRoot(asset).ExportChecked(asset.WorkingPath);if(!committed.Assets.TryGetValue(asset.Id,out var hash)||FighterProject.HashFile(asset.WorkingPath)!=hash){current=false;break;}}
            if(!current)throw new InvalidOperationException("The workspace snapshot saved; newer native edits remain unsaved in this viewer.");
            _workspaceSavedAssets=committed.Assets;_fighterDirty=false;foreach(var root in _fighterRoots.Values)root.IsDirty=false;UpdateFighterStatus();
        }
        private async Task OpenWorkspaceScene(string target,string scene)
        {
            if(scene==null)throw new InvalidOperationException("Select a model under Shared Character or a target.");
            if(_workspaceSceneId!=null)await SaveCharacterWorkspace();
            await _characterWorkspace.Send(new{Operation="workspace-select",TargetId=target,SceneId=scene});
            string output=Path.Combine(_characterWorkspace.NewFolder(),"preview");var reply=await _characterWorkspace.Send(new{Operation="workspace-export",Output=output});FighterProject project;
            if(reply.Package!=null)project=FighterProject.Open(NativeWorkspaceSession.ExtractPackage(reply.Package,Path.Combine(output,"unpacked")));
            else
            {
                project=CreateLegacyWorkspaceProject(reply,_characterWorkspace.Info.Scenes.Single(s=>s.Id==scene).Name);
            }
            try{ValidateFighterProjectAssets(project);}catch{project.Dispose();throw;}
            // Existing native close/save handling retains the outgoing viewer.
            _workspaceReplacing=true;
            try{MainForm.Instance.OpenWorkspaceScene(project,scene);}finally{_workspaceReplacing=false;}
        }
        private void RefreshCharacterWorkspace()
        {
            var info=_characterWorkspace.Info;if(_workspaceButton!=null)_workspaceButton.Text=info==null?"Workspace":"Workspace · "+info.Name+(info.Dirty?" *":"");
            if(_workspaceTree==null||_workspaceTree.IsDisposed)return;
            string selected=(_workspaceTree.SelectedNode?.Tag as WorkspaceChoice)?.Scene;_workspaceTree.BeginUpdate();
            try
            {
                _workspaceTree.Nodes.Clear();if(info==null){_workspaceTree.Nodes.Add("Create or open a character workspace.");return;}
                var shared=new TreeNode("Shared Character"){Tag=new WorkspaceChoice()};_workspaceTree.Nodes.Add(shared);
                foreach(var scene in info.Scenes.Where(s=>s.Target==null))shared.Nodes.Add(new TreeNode(scene.ToString()){Tag=new WorkspaceChoice{Scene=scene.Id}});
                foreach(var target in info.Targets){var group=new TreeNode(target.Name+" · "+target.Build.Game){Tag=new WorkspaceChoice{Target=target.Id}};_workspaceTree.Nodes.Add(group);foreach(var scene in info.Scenes.Where(s=>s.Target==target.Id))group.Nodes.Add(new TreeNode(scene.ToString()){Tag=new WorkspaceChoice{Target=target.Id,Scene=scene.Id}});group.Expand();}
                shared.Expand();_workspaceTree.SelectedNode=WorkspaceNodes(_workspaceTree.Nodes).FirstOrDefault(n=>(n.Tag as WorkspaceChoice)?.Scene==(selected??_workspaceSceneId)&&((n.Tag as WorkspaceChoice)?.Scene)!=null)??shared;
            }
            finally{_workspaceTree.EndUpdate();}
        }
        private static IEnumerable<TreeNode> WorkspaceNodes(TreeNodeCollection nodes){foreach(TreeNode node in nodes){yield return node;foreach(var child in WorkspaceNodes(node.Nodes))yield return child;}}
        private void ShowCharacterWorkspace()
        {
            if(_workspaceWindow!=null&&!_workspaceWindow.IsDisposed){_workspaceWindow.Show();return;}
            var window=new Form{Text="Character Workspace",ClientSize=new System.Drawing.Size(650,490),MinimumSize=new System.Drawing.Size(520,350),ShowInTaskbar=false};
            var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(6)};
            Action<string,Func<Task>> button=(text,action)=>{var b=new Button{Text=text,AutoSize=true};b.Click+=(s,e)=>WorkspaceAction(action);actions.Controls.Add(b);};
            button("New Character...",async()=>{using(var folder=new FolderBrowserDialog{Description="Choose a new empty character folder"}){if(folder.ShowDialog(window)!=DialogResult.OK)return;if(_characterWorkspace.Info!=null&&(_workspaceSceneId!=null||_characterWorkspace.Info.Dirty))await SaveCharacterWorkspace();string name=Path.GetFileName(folder.SelectedPath);await _characterWorkspace.Send(new{Operation="workspace-create",Name=name,Path=folder.SelectedPath});_workspaceSceneId=null;}});
            button("Open Character...",async()=>{using(var file=new OpenFileDialog{Title="Open character workspace",Filter="FighterPort character (*.fighterport)|*.fighterport"}){if(file.ShowDialog(window)!=DialogResult.OK)return;if(_characterWorkspace.Info!=null&&(_workspaceSceneId!=null||_characterWorkspace.Info.Dirty))await SaveCharacterWorkspace();await _characterWorkspace.Send(new{Operation="workspace-open",Path=file.FileName});_workspaceSceneId=null;}});
            button("Add Target...",()=>AddWorkspaceTarget(window));
            button("Add Current Model",()=>AddCurrentToWorkspace((_workspaceTree.SelectedNode?.Tag as WorkspaceChoice)?.Target));
            button("Open Selected",()=>{var choice=_workspaceTree.SelectedNode?.Tag as WorkspaceChoice;return OpenWorkspaceScene(choice?.Target,choice?.Scene);});
            button("Copy Shared to Target...",()=>CopyWorkspaceScene(window));
            button("Save Workspace",SaveCharacterWorkspace);
            button("Save Workspace As...",()=>SaveCharacterWorkspaceCopy(true));
            _workspaceTree=new TreeView{Dock=DockStyle.Fill,HideSelection=false,Name="characterWorkspaceTree"};_workspaceTree.NodeMouseDoubleClick+=(s,e)=>{if(e.Node.Tag is WorkspaceChoice choice&&choice.Scene!=null)WorkspaceAction(()=>OpenWorkspaceScene(choice.Target,choice.Scene));};
            var note=new Label{Dock=DockStyle.Bottom,Height=60,Padding=new Padding(8),Text="Shared models create independent copies for each target. Native controls and animation curves save with the scene.\nGame adapters and build readiness remain specific to each configuration."};
            window.Controls.Add(_workspaceTree);window.Controls.Add(note);window.Controls.Add(actions);_workspaceTree.BringToFront();_workspaceWindow=window;RefreshCharacterWorkspace();DarkTheme.Apply(window);window.Show(this);
        }
        private async Task AddWorkspaceTarget(Form owner)
        {
            if(_characterWorkspace.Info==null)throw new InvalidOperationException("Create or open a character workspace first.");
            using(var dialog=new Form{Text="New Target",ClientSize=new System.Drawing.Size(420,265),FormBorderStyle=FormBorderStyle.FixedDialog,ShowInTaskbar=false,StartPosition=FormStartPosition.CenterParent})
            {
                var grid=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10),ColumnCount=2,RowCount=6};grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));var values=new List<TextBox>();
                foreach(string label in new[]{"Target name","Game","Platform","Build / version","Mode"}){int row=values.Count;grid.Controls.Add(new Label{Text=label,AutoSize=true,Anchor=AnchorStyles.Left},0,row);var value=new TextBox{Dock=DockStyle.Fill};values.Add(value);grid.Controls.Add(value,1,row);}
                var save=new Button{Text="Add Target",DialogResult=DialogResult.OK,AutoSize=true};grid.Controls.Add(save,1,5);dialog.AcceptButton=save;dialog.Controls.Add(grid);DarkTheme.Apply(dialog);if(dialog.ShowDialog(owner)!=DialogResult.OK)return;
                await _characterWorkspace.Send(new{Operation="workspace-target",Name=values[0].Text,Game=values[1].Text,Platform=values[2].Text,Build=values[3].Text,Mode=values[4].Text});
                if(!_characterWorkspace.Info.RequiresUpgradeCopy)await _characterWorkspace.Send(new{Operation="workspace-save"});
            }
        }
        private async Task CopyWorkspaceScene(Form owner)
        {
            var choice=_workspaceTree.SelectedNode?.Tag as WorkspaceChoice;if(choice?.Scene==null||choice.Target!=null)throw new InvalidOperationException("Select a shared character model to copy.");
            if(_workspaceSceneId==choice.Scene)await SaveCharacterWorkspace();
            var info=_characterWorkspace.Info;if(info.Targets.Length==0)throw new InvalidOperationException("Add a target first.");
            using(var dialog=new Form{Text="Assign Independent Target Copy",ClientSize=new System.Drawing.Size(390,105),FormBorderStyle=FormBorderStyle.FixedDialog,ShowInTaskbar=false,StartPosition=FormStartPosition.CenterParent})
            {
                var targets=new ComboBox{Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,DataSource=info.Targets};var assign=new Button{Dock=DockStyle.Bottom,Text="Create Independent Copy",DialogResult=DialogResult.OK};dialog.Controls.Add(targets);dialog.Controls.Add(assign);dialog.AcceptButton=assign;DarkTheme.Apply(dialog);if(dialog.ShowDialog(owner)!=DialogResult.OK)return;
                await _characterWorkspace.Send(new{Operation="workspace-copy",SceneId=choice.Scene,TargetId=((NativeWorkspaceTarget)targets.SelectedItem).Id});await _characterWorkspace.Send(new{Operation="workspace-save"});
            }
        }
    }
}
