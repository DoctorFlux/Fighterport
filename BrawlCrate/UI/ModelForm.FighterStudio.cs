using BrawlCrate.Studio;
using BrawlCrate.UI.Model_Previewer.ModelEditControl;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.Internal.Windows.Controls.ModelViewer.MainWindowBase;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private FighterProject _fighterProject;
        private readonly Dictionary<string, ResourceNode> _fighterRoots = new Dictionary<string, ResourceNode>();
        private readonly Dictionary<string, FighterAsset> _fighterAssets = new Dictionary<string, FighterAsset>();
        private readonly Stack<ImportState> _fighterImportUndo = new Stack<ImportState>();
        private HashSet<ResourceNode> _fighterClosingRoots;
        private bool _fighterRestoring;
        private bool _fighterDirty;
        private ToolStripTextBox _fighterName;
        private ToolStripButton _fighterUndo;
        private ToolStripButton _fighterPlay;
        private ToolStripButton _fighterRest;
        private ToolStripButton _fighterExport;
        private ToolStripStatusLabel _fighterStatus;

        internal FighterProject FighterProject => _fighterProject;
        internal ModelEditControl FighterEditor => modelEditControl1;

        internal static void ValidateFighterProjectAssets(FighterProject project)
        {
            foreach (FighterAsset asset in project.RegisteredAssets())
            {
                using (ResourceNode root = NodeFactory.FromFile(null, asset.WorkingPath))
                {
                    if (root == null || asset.Role == "model" && ModelPanel.CollectModels(root).Count == 0 ||
                        asset.Role == "animation" && !DiscoverFighterAnimations(root).Any())
                    {
                        throw new InvalidDataException("The saved fighter asset has no supported " + asset.Role + ": " +
                            Path.GetFileName(asset.SourcePath));
                    }
                }
            }
        }

        internal void InitializeFighterProject(FighterProject project)
        {
            _fighterProject = project ?? throw new ArgumentNullException(nameof(project));
            _fighterDirty = string.IsNullOrEmpty(project.ManifestPath) &&
                (project.RegisteredAssets().Any() || project.AnimationControls.Any() || project.Name != "New Fighter");
            ClientSize = new Size(1320, 850);
            MinimumSize = new Size(1050, 650);
            StartPosition = FormStartPosition.CenterScreen;

            ToolStrip toolbar = new ToolStrip {Name = "fighterToolbar", Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden};
            ToolStripDropDownButton file = new ToolStripDropDownButton("Fighter") {Name = "fighterProjectMenu"};
            file.DropDownItems.Add("New Fighter", null, (s, e) => MainForm.Instance.NewFighterProject());
            file.DropDownItems.Add("Open Fighter...", null, (s, e) => MainForm.Instance.ChooseFighterProject(this));
            file.DropDownItems.Add("Save As...", null, (s, e) => SaveFighterFromUi(true));
            toolbar.Items.Add(file);
            _fighterName = new ToolStripTextBox {Name = "fighterName", Text = project.Name, AutoSize = false, Width = 145, MaxLength = 120};
            _fighterName.AccessibleName = _fighterName.TextBox.AccessibleName = "Fighter name";
            _fighterName.TextChanged += (s, e) =>
            {
                _fighterProject.Name = string.IsNullOrWhiteSpace(_fighterName.Text) ? "New Fighter" : _fighterName.Text.Trim();
                MarkFighterChanged();
                UpdateFighterStatus();
            };
            toolbar.Items.Add(_fighterName);
            toolbar.Items.Add(new ToolStripSeparator());
            AddFighterButton(toolbar, "Open Model...", "fighterOpenModel", ChooseFighterModel);
            AddFighterButton(toolbar, "Add Animations...", "fighterAddAnimations", ChooseFighterAnimations);
            _fighterPlay = AddFighterButton(toolbar, "Play / Pause", "fighterPlay", () =>
            {
                modelEditControl1.TogglePlay();
                UpdateFighterStatus();
            });
            _fighterRest = AddFighterButton(toolbar, "Rest Pose", "fighterRestPose", () =>
            {
                modelEditControl1.StopAnim();
                modelEditControl1.SetFrame(0);
                MarkFighterChanged();
            });
            AddFighterButton(toolbar, "Save", "fighterSave", () => SaveFighterFromUi());
            _fighterExport = AddFighterButton(toolbar, "Export...", "fighterExport", ChooseFighterExport);
            _fighterUndo = AddFighterButton(toolbar, "Undo", "fighterUndoImport", ()=>{if(modelEditControl1.CanUndo)modelEditControl1.Undo();else UndoFighterImport();});
            modelEditControl1.FighterUndoStateChanged=UpdateFighterStatus;
            AddFighterButton(toolbar, "Resources", "fighterResources", () => ShowFighterResources(null));
            InitializeFighterRetarget(toolbar, file);
            InitializeAnimationLibrary(toolbar, file);
            InitializeAnimationControls(toolbar);
            InitializeCharacterWorkspace(toolbar);
            Controls.Add(toolbar);
            toolbar.BringToFront();

            StatusStrip status = new StatusStrip {Name = "fighterStatusStrip", Dock = DockStyle.Bottom};
            _fighterStatus = new ToolStripStatusLabel {Spring = true, TextAlign = ContentAlignment.MiddleLeft};
            status.Items.Add(_fighterStatus);
            Controls.Add(status);
            status.BringToFront();
            // Dock layout processes siblings in reverse order: allocate both bars before the fill editor.
            modelEditControl1.BringToFront();
            InitializeSimpleWorkspace(toolbar, status);
            PerformLayout();

            modelEditControl1.FighterOpenRequested = (path, models, animations, etc) => RunFighterAction(() =>
            {
                if (models)
                {
                    ImportFighterModel(path);
                }
                else if (animations)
                {
                    ImportFighterAnimations(new[] {path});
                }
                else
                {
                    throw new InvalidOperationException("Choose Open Model or Add Animations for this fighter.");
                }
            });
            modelEditControl1.FighterSaveRequested = () => SaveFighterFromUi();
            modelEditControl1.CaptureBoneAnimationUndo = CaptureNativeBoneAnimationUndo;
            modelEditControl1.FighterResourcesRequested = ShowFighterResources;
            modelEditControl1.FighterOwnsFile = OwnsFighterRoot;
            modelEditControl1.FighterFileSaveRequested = SaveOwnedFighterResource;
            modelEditControl1.FighterNativeExportRequested = (node, path) => RunFighterAction(() => ExportFighterNode(node, path));
            modelEditControl1.FighterCaptureRig=CaptureFighterPreviewRig;
            modelEditControl1.FighterAllowsCompanionChannels = clip => !_fighterProject.Animations.Any(a =>
                a.Retarget != null && _fighterRoots.TryGetValue(a.Id, out ResourceNode root) && ReferenceEquals(root, clip.RootNode));
            modelEditControl1.FighterAnimationLabel = clip =>
            {
                FighterAsset asset = _fighterProject.Animations.FirstOrDefault(a => a.Retarget != null &&
                    _fighterRoots.TryGetValue(a.Id, out ResourceNode root) && ReferenceEquals(root, clip.RootNode));
                return asset == null ? clip.Name : clip.Name + " → " + asset.Retarget.ToProfile;
            };
            modelEditControl1.FighterResourceLabel = root =>
            {
                FighterAsset asset = _fighterAssets.Values.FirstOrDefault(a =>
                    _fighterRoots.TryGetValue(a.Id, out ResourceNode loaded) && ReferenceEquals(loaded, root));
                return asset == null ? root.Name : Path.GetFileName(asset.SourcePath);
            };
            modelEditControl1.leftPanel.listAnims.SelectedIndexChanged += (s, e) =>
            {
                if (!_fighterRestoring)
                {
                    _fighterProject.SelectedAnimation = SelectedFighterAnimationKey();
                    MarkFighterChanged();
                    UpdateFighterStatus();
                    if(SelectedFighterAsset()?.Retarget?.FloorContactsEnabled==true)modelEditControl1.QueueGroundContactView();
                }
            };
            modelEditControl1.PlaybackPanel.numFrameIndex.ValueChanged += (s, e) =>
            {
                MarkFighterChanged();
            };
            UpdateFighterStatus();
        }

        private static ToolStripButton AddFighterButton(ToolStrip toolbar, string text, string name, Action action)
        {
            ToolStripButton button = new ToolStripButton(text) {Name = name, DisplayStyle = ToolStripItemDisplayStyle.Text};
            button.Click += (s, e) =>
            {
                var form = toolbar.FindForm() as ModelForm;
                if (form == null) action(); else form.WithWorkspaceInvoker(button, action);
            };
            toolbar.Items.Add(button);
            return button;
        }

        private void ShowFighterProject()
        {
            _fighterRestoring = true;
            try
            {
                modelEditControl1.ModelPanel.Capture();
                ReadSettings();
                modelEditControl1.ConfigureFighterView();
                RunFighterAction(RebuildFighterScene);
                ResumeSimpleWorkspace();
                MainForm.Instance.Hide();
                BeginInvoke(new Action(() =>
                {
                    modelEditControl1.leftPanel.ConfigureFighterLayout();
                    if(SelectedFighterAsset()?.Retarget?.FloorContactsEnabled==true)modelEditControl1.QueueGroundContactView();
                    else modelEditControl1.QueueFrameFighterModel();
                }));
            }
            finally
            {
                _fighterRestoring = false;
            }

            UpdateFighterStatus();
            if (_fighterProject.Warnings.Count > 0)
            {
                MessageBox.Show(this, string.Join(Environment.NewLine, _fighterProject.Warnings),
                    "Fighter source information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private List<FighterAsset> RegisteredFighterAssets()
        {
            List<FighterAsset> assets = new List<FighterAsset>();
            if (_fighterProject.Model != null)
            {
                assets.Add(_fighterProject.Model);
            }

            assets.AddRange(_fighterProject.Animations);
            return assets;
        }

        private ResourceNode FighterRoot(FighterAsset asset)
        {
            if (_fighterRoots.TryGetValue(asset.Id, out ResourceNode root))
            {
                return root;
            }

            root = NodeFactory.FromFile(null, asset.WorkingPath);
            if (root == null)
            {
                throw new InvalidDataException($"BrawlCrate could not recognize {Path.GetFileName(asset.SourcePath)}.");
            }

            try
            {
                if (asset.Role == "model" && ModelPanel.CollectModels(root).Count == 0)
                {
                    throw new InvalidDataException($"No supported model was found in {Path.GetFileName(asset.SourcePath)}.");
                }

                if (asset.Role == "animation" && !DiscoverFighterAnimations(root).Any())
                {
                    throw new InvalidDataException($"No supported native animations were found in {Path.GetFileName(asset.SourcePath)}. This milestone reads Brawl CHR0 and related native channels in supported containers.");
                }

                _fighterRoots.Add(asset.Id, root);
                _fighterAssets[asset.Id] = asset;
                return root;
            }
            catch
            {
                root.Dispose();
                throw;
            }
        }

        private static IEnumerable<NW4RAnimationNode> DiscoverFighterAnimations(ResourceNode root)
        {
            if (root is NW4RAnimationNode animation)
            {
                yield return animation;
                yield break;
            }

            switch (root.ResourceFileType)
            {
                case ResourceType.ARC:
                case ResourceType.MRG:
                case ResourceType.U8:
                case ResourceType.U8Folder:
                case ResourceType.BRES:
                case ResourceType.BRESGroup:
                    foreach (ResourceNode child in root.Children)
                    {
                        foreach (NW4RAnimationNode clip in DiscoverFighterAnimations(child))
                        {
                            yield return clip;
                        }
                    }

                    break;
            }
        }

        internal void ImportFighterModel(string sourcePath)
        {
            FighterAsset asset = _fighterProject.StageAsset(sourcePath, "model");
            ResourceNode root = FighterRoot(asset);
            RememberFighterImport();
            _fighterProject.SelectedAnimation = SelectedFighterAnimationKey();
            _fighterProject.Frame = modelEditControl1.CurrentFrame;
            _fighterProject.Model = asset;
            _fighterProject.ModelNodePath = ((ResourceNode) ChooseFighterModel(ModelPanel.CollectModels(root), null)).TreePath;
            RebuildFighterScene();
            modelEditControl1.QueueFrameFighterModel();
            MarkFighterChanged();
        }

        internal void ImportFighterAnimations(IEnumerable<string> sourcePaths)
        {
            List<FighterAsset> assets = new List<FighterAsset>();
            foreach (string path in sourcePaths)
            {
                FighterAsset asset = _fighterProject.StageAsset(path, "animation");
                FighterRoot(asset);
                assets.Add(asset);
            }

            if (assets.Count == 0)
            {
                return;
            }

            if (_fighterProject.Animations.Count + assets.Count > 256)
            {
                throw new InvalidOperationException("A fighter can register at most 256 animation files.");
            }

            RememberFighterImport();
            _fighterProject.Animations.AddRange(assets);
            List<NW4RAnimationNode> clips = assets.SelectMany(a => DiscoverFighterAnimations(FighterRoot(a))).ToList();
            NW4RAnimationNode first = ChooseFighterPreviewAnimation(clips);
            FighterAsset selectedAsset = assets.First(a => ReferenceEquals(FighterRoot(a), first.RootNode));
            _fighterProject.SelectedAnimation = FighterAnimationKey(selectedAsset,first);
            _fighterProject.Frame = 1;
            RebuildFighterScene();
            modelEditControl1.QueueFrameFighterModel();
            MarkFighterChanged();
        }

        private NW4RAnimationNode ChooseFighterPreviewAnimation(List<NW4RAnimationNode> clips)
        {
            if (modelEditControl1.TargetModel != null)
            {
                HashSet<string> bones = new HashSet<string>(modelEditControl1.TargetModel.BoneCache.Select(b => b.Name),
                    StringComparer.Ordinal);
                List<CHR0Node> matching = clips.OfType<CHR0Node>().Where(c => c.Children.Any(n => bones.Contains(n.Name))).ToList();
                CHR0Node idle = matching.FirstOrDefault(c => c.Name == "Wait1" && c.Children.All(n => bones.Contains(n.Name)));
                if (idle != null)
                {
                    return idle;
                }

                CHR0Node compatible = matching.OrderByDescending(c => c.Children.All(n => bones.Contains(n.Name)))
                    .ThenByDescending(c => c.Children.Count(n => bones.Contains(n.Name))).FirstOrDefault();
                if (compatible != null)
                {
                    return compatible;
                }
            }

            return clips[0];
        }

        private static IModel ChooseFighterModel(List<IModel> models, string savedPath)
        {
            IModel selected = models.FirstOrDefault(m => ((ResourceNode) m).TreePath == savedPath);
            if (selected != null)
            {
                return selected;
            }

            return models.FirstOrDefault(m => ((ResourceNode) m).Name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) < 0 &&
                m.Objects.Length > 0) ?? models[0];
        }

        private void RebuildFighterScene()
        {
            bool wasRestoring = _fighterRestoring;
            _fighterRestoring = true;
            try
            {
                List<FighterAsset> assets = RegisteredFighterAssets();
                // Parse every registration before replacing the current live scene.
                foreach (FighterAsset asset in assets)
                {
                    FighterRoot(asset);
                }

                if (Program.RootNode != null && _fighterRoots.Values.Contains(Program.RootNode.RootNode) &&
                    !assets.Any(a => ReferenceEquals(FighterRoot(a), Program.RootNode.RootNode)))
                {
                    Program.RootNode = null;
                    Program._rootPath = null;
                }

                modelEditControl1.StopAnim();
                modelEditControl1.Updating = true;
                for (int i = 0; i < ModelEditorBase.AnimTypeList.Length; i++)
                {
                    modelEditControl1.SetAnimation((NW4RAnimType) i, null);
                }

                modelEditControl1.TargetModel = null;
                modelEditControl1._targetModels.Clear();
                modelEditControl1.ModelPanel.ClearAll();
                modelEditControl1._openedFiles.Clear();
                foreach (FighterAsset asset in assets)
                {
                    ResourceNode root = FighterRoot(asset);
                    modelEditControl1._openedFiles.Add(root);
                    modelEditControl1.ModelPanel.AddReference(root);
                }

                if (_fighterProject.Model != null)
                {
                    // A costume PAC can also contain shadow/companion models. Render only the chosen fighter.
                    IModel selected = ChooseFighterModel(ModelPanel.CollectModels(FighterRoot(_fighterProject.Model)),
                        _fighterProject.ModelNodePath);
                    _fighterProject.ModelNodePath = ((ResourceNode) selected).TreePath;
                    modelEditControl1.AppendTarget(selected);
                    modelEditControl1.TargetModel = selected;
                    modelEditControl1.EditingAll = false;
                    modelEditControl1.ModelPanel.RefreshReferences();
                }

                modelEditControl1.Updating = false;
                NW4RAnimationNode selectedAnimation = null;
                foreach (FighterAsset asset in assets)
                {
                    var animations=DiscoverFighterAnimations(FighterRoot(asset)).ToArray();
                    selectedAnimation=animations.FirstOrDefault(n=>FighterAnimationKey(asset,n)==_fighterProject.SelectedAnimation);
                    if(selectedAnimation==null)
                    {
                        var legacy=animations.Where(n=>asset.Id+"|"+n.TreePath==_fighterProject.SelectedAnimation).ToArray();
                        if(legacy.Length==1)selectedAnimation=legacy[0];
                    }
                    if (selectedAnimation != null)
                    {
                        break;
                    }
                }

                if (selectedAnimation != null)
                {
                    int type = Array.IndexOf(ModelEditorBase.AnimTypeList, selectedAnimation.GetType());
                    modelEditControl1.TargetAnimType = (NW4RAnimType) type;
                }

                modelEditControl1.leftPanel.UpdateAnimations();
                if (selectedAnimation != null)
                {
                    foreach (ListViewItem item in modelEditControl1.leftPanel.listAnims.Items)
                    {
                        item.Selected = ReferenceEquals(item.Tag, selectedAnimation);
                        if (item.Selected) item.EnsureVisible();
                    }

                    modelEditControl1.TargetAnimation = selectedAnimation;
                    modelEditControl1.SetFrame(Math.Max(0, Math.Min(_fighterProject.Frame, selectedAnimation.FrameCount)));
                }
                else
                {
                    modelEditControl1.TargetAnimation = null;
                    modelEditControl1.SetFrame(0);
                }

                modelEditControl1.StopAnim();
                modelEditControl1.UpdateModel();
                modelEditControl1.ModelPanel.Invalidate();
            }
            finally
            {
                modelEditControl1.Updating = false;
                _fighterRestoring = wasRestoring;
            }
            UpdateFighterStatus();
        }

        private string SelectedFighterAnimationKey()
        {
            NW4RAnimationNode animation = modelEditControl1.TargetAnimation;
            if (animation == null)
            {
                return null;
            }

            foreach (FighterAsset asset in RegisteredFighterAssets())
            {
                if (_fighterRoots.TryGetValue(asset.Id, out ResourceNode root) && ReferenceEquals(animation.RootNode, root))
                {
                    return FighterAnimationKey(asset,animation);
                }
            }

            return null;
        }
        private static string FighterAnimationKey(FighterAsset asset,ResourceNode animation)=>asset.Id+"|@"+WorkspaceNativeIdentity(animation);

        internal void SaveFighterProject(string manifestPath)
        {
            ProtectCharacterWorkspaceOutput(manifestPath);
            if(_pinGesture!=null)throw new InvalidOperationException("Finish or cancel the active pin gesture before saving.");
            FinishControlCurveEdit();
            MarkFighterChanged();
            _fighterDirty = true;
            int savedFrame = modelEditControl1.CurrentFrame;
            modelEditControl1.StopAnim();
            modelEditControl1.SetFrame(savedFrame);
            _fighterProject.SelectedAnimation = SelectedFighterAnimationKey();
            _fighterProject.Frame = modelEditControl1.CurrentFrame;
            if (modelEditControl1.TargetModel is ResourceNode model)
            {
                _fighterProject.ModelNodePath = model.TreePath;
            }

            var approvedBeforeSave = _batchReviewRows.Where(r => r.Approved).ToArray();
            foreach (FighterAsset asset in RegisteredFighterAssets())
            {
                ResourceNode root = FighterRoot(asset);
                // Native field edits also need the existing precise encoder on
                // project Save. Default recompression can round untouched keys
                // in the edited clip, including protected motion controls.
                foreach (var clip in DiscoverFighterAnimations(root).OfType<CHR0Node>()
                    .Where(c => c.IsDirty || c.IsBranch).ToArray())
                {
                    var previous = clip.Children.OfType<CHR0EntryNode>().ToArray();
                    PreciseCHR0EntryNode.Promote(clip);
                    modelEditControl1.RebindSavedCurveTargets(clip, previous);
                }
                if (root.IsDirty)
                {
                    root.Merge();
                }

                // Capture the live resource even if a native editor already merged its dirty state.
                root.ExportChecked(asset.WorkingPath);
                root._origPath = asset.WorkingPath;
                if (ReferenceEquals(root, Program.RootNode))
                {
                    Program._rootPath = asset.WorkingPath;
                }
            }

            _fighterProject.Save(manifestPath);
            foreach (FighterAsset asset in RegisteredFighterAssets())
            {
                FighterRoot(asset).IsDirty = false;
            }

            _fighterDirty = false;
            RestoreBatchReviewAfterSave(approvedBeforeSave);
            UpdateFighterStatus();
            WorkspaceRecentProjects.Current.Record(_fighterProject.ManifestPath);
            if (WorkspaceRecentProjects.Current.LastError != null) _fighterStatus.Text = WorkspaceRecentProjects.Current.LastError;
        }

        private string _fighterSaveError;
        private bool SaveFighterFromUi(bool saveAs = false)
        {
            if(!saveAs&&_workspaceSceneId!=null){WorkspaceAction(SaveCharacterWorkspace);return false;}
            try
            {
                _fighterSaveError = null;
                string path = saveAs ? null : _fighterProject.ManifestPath;
                if (string.IsNullOrEmpty(path))
                {
                    using (var dialog = new WorkspaceFileDialog("Save project", "Choose where to save this project's editable copies.",
                        "Fighterport project (*.fpstudio)|*.fpstudio", "Save", null, true))
                    {
                        dialog.CommitSelection = destination =>
                        {
                            if (File.Exists(destination) && !string.Equals(destination, _fighterProject.ManifestPath, StringComparison.OrdinalIgnoreCase))
                                throw new IOException("A project already exists here. Choose a new filename to preserve it.");
                            SaveFighterProject(destination);
                        };
                        if (dialog.ShowDialog(this) != DialogResult.OK)
                        {
                            _fighterSaveError = "Save cancelled. Your current project remains open.";
                            return false;
                        }
                        return true;
                    }
                }

                SaveFighterProject(path);
                return true;
            }
            catch (Exception ex)
            {
                _fighterSaveError = "Could not save: " + ex.Message;
                _fighterStatus.Text = _fighterSaveError;
                return false;
            }
        }

        private void ChooseFighterModel()
        {
            using (OpenFileDialog dialog = new OpenFileDialog
                   {Title = "Open Model", Filter = "Brawl model resources (*.pac;*.pcs;*.brres;*.mdl0)|*.pac;*.pcs;*.brres;*.mdl0|All files (*.*)|*.*"})
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    RunFighterAction(() => ImportFighterModel(dialog.FileName));
                }
            }
        }

        private void ChooseFighterAnimations()
        {
            using (OpenFileDialog dialog = new OpenFileDialog
                   {Title = "Add Animations", Multiselect = true,
                       Filter = "Brawl animations (*.pac;*.pcs;*.brres;*.chr0;*.srt0;*.pat0;*.vis0;*.shp0;*.clr0;*.scn0)|*.pac;*.pcs;*.brres;*.chr0;*.srt0;*.pat0;*.vis0;*.shp0;*.clr0;*.scn0|All files (*.*)|*.*"})
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    RunFighterAction(() => ImportFighterAnimations(dialog.FileNames));
                }
            }
        }

        private void ChooseFighterExport()
        {
            ChooseFighterExport(null);
        }

        private bool ChooseFighterExport(ResourceNode requestedNode)
        {
            ResourceNode node = requestedNode ?? modelEditControl1.TargetAnimation ??
                (_fighterProject.Model == null ? null : FighterRoot(_fighterProject.Model));
            if (node == null)
            {
                return false;
            }

            string extension = node is NW4RAnimationNode ? "." + node.ResourceFileType.ToString().ToLowerInvariant() :
                Path.GetExtension(node.RootNode._origPath);
            bool retargeted = node is CHR0Node && SelectedFighterAsset()?.Retarget != null;
            using (SaveFileDialog dialog = new SaveFileDialog
                   {Title = "Export New Native Output", Filter = retargeted ? "Native animation (*.chr0)|*.chr0|Brawl motion PAC (*.pac)|*.pac" : "Native resource (*" + extension + ")|*" + extension,
                       FileName = node.Name + extension, DefaultExt = extension.TrimStart('.'), AddExtension = true})
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        ExportFighterNode(node, dialog.FileName);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, "Could not export native resource", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

            return false;
        }

        internal void ExportFighterResource(string destination)
        {
            ResourceNode node = modelEditControl1.TargetAnimation ??
                (_fighterProject.Model == null ? null : FighterRoot(_fighterProject.Model));
            ExportFighterNode(node, destination);
        }

        private void ExportFighterNode(ResourceNode node, string destination)
        {
            ProtectCharacterWorkspaceOutput(destination);
            (_library??(_library=new AnimationLibrary())).ProtectOutputPath(destination);
            if (TryExportOwnedAnimationCopy(node, destination)) return;
            if (TryExportFighterRetargetPac(node, destination)) return;
            if (node != null && node.RootNode.IsDirty)
            {
                MarkFighterChanged();
            }

            _fighterProject.ProtectOutputPath(destination);
            string fullDestination = Path.GetFullPath(destination);
            if (_fighterAssets.Values.Any(a => !a.Generated && string.Equals(Path.GetFullPath(a.SourcePath), fullDestination,
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Choose a new output file. Original imported sources are protected.");
            }

            if (node == null)
            {
                throw new InvalidOperationException("Open a model or select an animation before exporting.");
            }

            node.ExportChecked(fullDestination);
            using (ResourceNode reopened = NodeFactory.FromFile(null, fullDestination))
            {
                if (reopened == null || reopened.ResourceFileType != node.ResourceFileType ||
                    node is NW4RAnimationNode clip && (!(reopened is NW4RAnimationNode saved) || saved.FrameCount != clip.FrameCount))
                {
                    throw new InvalidDataException("The output was written, but fresh native decoding did not validate it.");
                }
            }

            RecordLibraryExport(node, fullDestination);
            _fighterStatus.Text = "Exported and reopened " + Path.GetFileName(fullDestination) + ". Reference native route; export optimization is not qualified for this container/context.";
        }

        internal bool OwnsFighterRoot(ResourceNode root)
        {
            return root != null && RegisteredFighterAssets().Any(a =>
                _fighterRoots.TryGetValue(a.Id, out ResourceNode loaded) && ReferenceEquals(loaded, root.RootNode));
        }

        internal bool RetainsFighterRoot(ResourceNode root)
        {
            return root != null && _fighterRoots.Values.Contains(root.RootNode);
        }

        private bool CloseBorrowedFighterViews(HashSet<ResourceNode> roots)
        {
            foreach (ModelEditControl editor in ModelEditControl.Instances.ToList())
            {
                if (editor == modelEditControl1 || !editor._openedFiles.Any(r => r != null && roots.Contains(r.RootNode)))
                {
                    continue;
                }

                Form view = editor.FindForm();
                if (view != null && !view.IsDisposed)
                {
                    view.Close();
                    if (!view.IsDisposed)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        internal bool SaveOwnedFighterResource(ResourceNode root, bool saveAs)
        {
            return saveAs ? ChooseFighterExport(root) : SaveFighterFromUi();
        }

        internal void ProtectFighterNativeOutput(string path, bool folder)
        {
            ProtectCharacterWorkspaceOutput(path);
            if (RegisteredFighterAssets().Any(a => FighterRoot(a).IsDirty))
            {
                MarkFighterChanged();
            }

            if (folder)
            {
                if (File.Exists(path) || Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
                {
                    throw new InvalidOperationException("Choose a new or empty folder for native export. Existing files and original sources are protected.");
                }
            }
            else
            {
                _fighterProject.ProtectOutputPath(path);
            }
        }

        private void ShowFighterResources(ResourceNode node)
        {
            ResourceNode root = node?.RootNode ?? (_fighterProject.Model == null ?
                modelEditControl1.TargetAnimation?.RootNode : FighterRoot(_fighterProject.Model));
            if (root != null)
            {
                Program._rootPath = root._origPath;
                Program.RootNode = root;
            }

            MainForm.Instance.Show();
            MainForm.Instance.Activate();
        }

        private void RememberFighterImport()
        {
            if (!CloseBorrowedFighterViews(new HashSet<ResourceNode>(_fighterRoots.Values)))
            {
                throw new InvalidOperationException("The import was cancelled because a native view is still using this fighter.");
            }

            _fighterImportUndo.Push(new ImportState
            {
                Model = _fighterProject.Model,
                Animations = _fighterProject.Animations.ToList(),
                Selection = SelectedFighterAnimationKey(),
                ModelPath = _fighterProject.ModelNodePath,
                Frame = modelEditControl1.CurrentFrame
            });
        }

        private void UndoFighterImport()
        {
            if (_fighterImportUndo.Count == 0)
            {
                return;
            }

            if (!CloseBorrowedFighterViews(new HashSet<ResourceNode>(_fighterRoots.Values)))
            {
                return;
            }

            ImportState state = _fighterImportUndo.Pop();
            _fighterProject.Model = state.Model;
            _fighterProject.Animations.Clear();
            _fighterProject.Animations.AddRange(state.Animations);
            _fighterProject.SelectedAnimation = state.Selection;
            _fighterProject.ModelNodePath = state.ModelPath;
            _fighterProject.Frame = state.Frame;
            RebuildFighterScene();
            modelEditControl1.QueueFrameFighterModel();
            MarkFighterChanged();
        }

        private void FighterModelChanged()
        {
            if (!_fighterRestoring && modelEditControl1.TargetModel is ResourceNode model)
            {
                _fighterProject.ModelNodePath = model.TreePath;
                MarkFighterChanged();
            }

            UpdateFighterStatus();
        }

        private void MarkFighterChanged()
        {
            if (_fighterRestoring || _fighterProject == null)
            {
                return;
            }

            if (!_fighterDirty)
            {
                _fighterDirty = true;
                Text = _fighterProject.Name + " * — FighterPort Studio V2";
            }
        }

        private void UpdateFighterStatus()
        {
            if (_fighterProject == null || _fighterStatus == null)
            {
                return;
            }

            NW4RAnimationNode clip = modelEditControl1.TargetAnimation;
            bool hasModel = modelEditControl1.TargetModel != null;
            _fighterPlay.Enabled = hasModel && clip != null;
            _fighterRest.Enabled = hasModel;
            _fighterExport.Enabled = hasModel || clip != null;
            _fighterUndo.Enabled = modelEditControl1.CanUndo || _fighterImportUndo.Count > 0;
            UpdateFighterRetargetStatus();
            if (_libraryPane?.Visible == true && !ReferenceEquals(_libraryLastTarget,modelEditControl1.TargetModel)) UpdateLibrarySelection();
            Text = _fighterProject.Name + (_fighterDirty ? " *" : "") + " — FighterPort Studio V2";
            if (!hasModel)
            {
                _fighterStatus.Text = "Open a model to start. Model-only fighters can be saved.";
            }
            else if (clip == null)
            {
                _fighterStatus.Text = "Model: " + modelEditControl1.TargetModel + " · Add animations, then select a clip to preview.";
            }
            else
            {
                _fighterStatus.Text = "Model: " + modelEditControl1.TargetModel + " · " + clip.Name + " · " + clip.FrameCount +
                    " frames · " + (clip.Loop ? "Loop" : "No loop") + " · " + (modelEditControl1.Playing ? "Playing" : "Paused");
                if (clip is CHR0Node && !clip.Children.Any(n => modelEditControl1.TargetModel.BoneCache.Any(b => b.Name == n.Name)))
                {
                    _fighterStatus.Text += " · No matching bones. This clip needs retargeting before it can move this model.";
                }
                AppendFighterRetargetStatus();
            }
            UpdateSimpleWorkspace();
        }

        private void RunFighterAction(Action action)
        {
            try
            {
                action();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "FighterPort Studio V2", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool PrepareFighterClose()=>PrepareFighterDocumentChange(true);

        private bool PrepareFighterDocumentChange(bool closing)
        {
            int frame = modelEditControl1.CurrentFrame;
            modelEditControl1.StopAnim();
            modelEditControl1.SetFrame(frame);
            bool contentChanged;
            try
            {
                contentChanged = RegisteredFighterAssets().Any(a =>
                    _fighterRoots.TryGetValue(a.Id, out ResourceNode root) && root.IsDirty ||
                    !string.Equals(BrawlCrate.Studio.FighterProject.HashFile(a.WorkingPath), _workspaceSceneId!=null&&_workspaceSavedAssets!=null&&_workspaceSavedAssets.TryGetValue(a.Id,out var committed)?committed:a.ContentSha256,
                        StringComparison.OrdinalIgnoreCase));
            }
            catch (IOException)
            {
                contentChanged = true;
            }
            if (_fighterDirty || contentChanged)
            {
                using (var dialog = new UnsavedProjectDialog(_fighterProject.Name, async () =>
                {
                    if (_workspaceSceneId == null)
                        return SaveFighterFromUi() ? null : _fighterSaveError ?? "Save cancelled. Your current project remains open.";
                    _workspaceBusy = true;
                    modelEditControl1.Enabled = false;
                    try { await SaveCharacterWorkspace(); return null; }
                    catch (Exception error) { return "Could not save: " + error.Message; }
                    finally { _workspaceBusy = false; modelEditControl1.Enabled = true; RefreshCharacterWorkspace(); }
                }))
                {
                    var choice = dialog.ShowDialog(this);
                    if (choice != DialogResult.Yes && choice != DialogResult.No) return false;
                }
            }

            if (!CloseBorrowedFighterViews(new HashSet<ResourceNode>(_fighterRoots.Values)))
            {
                return false;
            }

            if(!closing)return true;
            _fighterClosingRoots = new HashSet<ResourceNode>(modelEditControl1._openedFiles.Where(r => r != null));
            // All live roots are staged copies. The fighter prompt above owns their save/discard decision.
            foreach (ResourceNode root in _fighterClosingRoots)
            {
                root.IsDirty = false;
            }

            if (_fighterRoots.Values.Contains(Program.RootNode))
            {
                Program.RootNode = null;
                Program._rootPath = null;
            }

            return true;
        }

        private void FinishFighterClose()
        {
            DisposeAnimationLibrary();
            foreach (ResourceNode root in _fighterRoots.Values.Distinct())
            {
                if (!_fighterClosingRoots.Contains(root))
                {
                    root.Dispose();
                }
            }

            _fighterRoots.Clear();
            _fighterProject.Dispose();
        }

        private sealed class ImportState
        {
            internal FighterAsset Model;
            internal List<FighterAsset> Animations;
            internal string Selection;
            internal string ModelPath;
            internal int Frame;
        }
    }
}
