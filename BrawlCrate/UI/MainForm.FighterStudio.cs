using BrawlCrate.Studio;
using System;
using System.IO;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    public partial class MainForm
    {
        private ModelForm _fighterForm;
        private bool _replacingFighter;
        internal static ApplicationContext StartViewerApplication()
        {
            var context=new ApplicationContext();
            Instance.FormClosed+=(s,e)=>context.ExitThread();
            if(!Instance.NewFighterProject()){context.Dispose();throw new InvalidOperationException("The model viewer could not start.");}
            return context;
        }

        private void InitializeFighterMenu()
        {
            ToolStripMenuItem fighter = new ToolStripMenuItem("Fighter") {Name = "fighterMenu"};
            fighter.DropDownItems.Add(new ToolStripMenuItem("New Fighter", null,
                (s, e) => NewFighterProject()) {Name = "newFighterMenu"});
            fighter.DropDownItems.Add(new ToolStripMenuItem("Open Fighter...", null,
                (s, e) => ChooseFighterProject()) {Name = "openFighterMenu"});
            menuStrip1.Items.Insert(0, fighter);
            Shown += (s, e) =>
            {
                if (_fighterForm != null && !_fighterForm.IsDisposed)
                {
                    Hide();
                    _fighterForm.Activate();
                }
            };
        }

        internal bool NewFighterProject()
        {
            return StartFighterProject(FighterProject.Create("New Fighter"));
        }

        // Preparation owns its independent staged copies before the current
        // viewer is asked to save/discard/cancel. The ordinary project lifecycle
        // still owns replacement and takes ownership only after opening.
        internal bool OpenLibraryFighterProject(FighterProject project, out ModelForm opened)
        {
            opened = null;
            if (project == null) throw new ArgumentNullException(nameof(project));
            ModelForm.ValidateFighterProjectAssets(project);
            if (!StartFighterProject(project)) return false;
            opened = _fighterForm;
            return true;
        }
        internal bool OpenWorkspaceScene(FighterProject project,string scene)
        {
            if(!StartFighterProject(project))return false;_fighterForm.BindWorkspaceScene(scene);return true;
        }

        internal bool ChooseFighterProject(IWin32Window owner = null)
        {
            using (OpenFileDialog dialog = new OpenFileDialog
                   {Title = "Open Fighter", Filter = "FighterPort Studio project (*.fpstudio)|*.fpstudio"})
            {
                return dialog.ShowDialog(owner ?? this) == DialogResult.OK && OpenFighterProject(dialog.FileName, owner);
            }
        }

        internal bool OpenFighterProject(string path, IWin32Window owner = null)
        {
            bool opened = TryOpenFighterProject(path, out string error);
            if (error != null) MessageBox.Show(owner ?? this, error, "Could not open fighter", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return opened;
        }

        internal bool TryOpenFighterProject(string path, out string error)
        {
            error = null;
            FighterProject project = null;
            try
            {
                project = FighterProject.Open(path);
                ModelForm.ValidateFighterProjectAssets(project);
                bool opened = StartFighterProject(project);
                if (opened) WorkspaceRecentProjects.Current.Record(project.ManifestPath);
                return opened;
            }
            catch (Exception ex)
            {
                project?.Dispose();
                error = ex.Message;
                return false;
            }
        }

        internal bool TrySaveFighterResource(BrawlLib.SSBB.ResourceNodes.ResourceNode root, bool saveAs, out bool result)
        {
            result = false;
            if (_fighterForm == null || _fighterForm.IsDisposed || !_fighterForm.OwnsFighterRoot(root))
            {
                return false;
            }

            result = _fighterForm.SaveOwnedFighterResource(root, saveAs);
            return true;
        }

        internal bool IsBorrowedFighterResource(Model_Previewer.ModelEditControl.ModelEditControl editor,
            BrawlLib.SSBB.ResourceNodes.ResourceNode node)
        {
            return _fighterForm != null && !_fighterForm.IsDisposed && _fighterForm.FighterEditor != editor &&
                _fighterForm.RetainsFighterRoot(node);
        }

        internal bool ValidateFighterOutputDestination(string path, BrawlLib.SSBB.ResourceNodes.ResourceNode resource = null,
            bool folder = false)
        {
            if (_fighterForm == null || _fighterForm.IsDisposed || !_fighterForm.OwnsFighterRoot(resource ?? Program.RootNode))
            {
                return true;
            }

            try
            {
                _fighterForm.ProtectFighterNativeOutput(path, folder);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(_fighterForm, ex.Message, "Protected fighter source", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
        }

        private bool StartFighterProject(FighterProject project)
        {
            _replacingFighter=true;
            try
            {
            if (_fighterForm != null && !_fighterForm.IsDisposed)
            {
                _fighterForm.Close();
                if (_fighterForm != null && !_fighterForm.IsDisposed)
                {
                    project.Dispose();
                    return false;
                }
            }

            // Native resource documents keep their own unsaved-change handling.
            if (!Program.Close())
            {
                project.Dispose();
                return false;
            }

            ModelForm form = new ModelForm();
            form.InitializeFighterProject(project);
            _fighterForm = form;
            form.FormClosed += (s, e) =>
            {
                if (_fighterForm == form)
                {
                    _fighterForm = null;
                    if(!_replacingFighter) BeginInvoke(new Action(Close));
                }
            };
            form.Show(this);
            return true;
            }
            finally { _replacingFighter=false; }
        }
    }
}
