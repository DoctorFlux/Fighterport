using BrawlCrate.Studio;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal sealed class LibrarySourcesDialog : Form
    {
        private readonly AnimationLibrary library;
        private readonly ListBox sources=new ListBox(),locations=new ListBox();
        private readonly ComboBox game=new ComboBox();
        private readonly TextBox version=new TextBox(),path=new TextBox();
        private readonly Button undo=new Button();
        private LibraryCatalogue data;
        internal LibrarySourcesDialog(AnimationLibrary library,Action<IWin32Window,LibraryGameRequest> browse)
        {
            this.library=library;Text="Library Sources and Game Paths";Name="LibrarySourcesDialog";
            ClientSize=new Size(670,435);StartPosition=FormStartPosition.CenterParent;MinimizeBox=MaximizeBox=false;FormBorderStyle=FormBorderStyle.FixedDialog;
            var tabs=new TabControl{Dock=DockStyle.Fill,Name="librarySourceTabs"};var sourceTab=new TabPage("Registered Sources");var pathTab=new TabPage("Game / Build Paths");tabs.TabPages.AddRange(new[]{sourceTab,pathTab});
            sourceTab.Controls.Add(new Label{Text="Removing a source unregisters its library entries. Original files and saved target revisions remain intact.",Bounds=new Rectangle(12,12,625,36)});
            sources.Name="libraryRegisteredSources";sources.Bounds=new Rectangle(12,52,625,273);sourceTab.Controls.Add(sources);
            var remove=new Button{Name="libraryRemoveSource",Text="Remove Selected Source",Bounds=new Rectangle(12,341,180,30)};
            remove.Click+=(s,e)=>Run(()=>{if(sources.SelectedIndex<0)return;library.RemoveSource(data.Models[sources.SelectedIndex].Id);Reload();});
            undo.Name="librarySourcesUndo";undo.Text="Undo Last Change";undo.Bounds=new Rectangle(204,341,145,30);undo.Click+=(s,e)=>Run(()=>{library.UndoLastUpdate();Reload();});
            sourceTab.Controls.AddRange(new Control[]{remove,undo});
            pathTab.Controls.Add(new Label{Text="Save paths per game/build. Browse Brawl-family folders or ISO/WBFS/RVZ with your installed DolphinTool. Other games' native adapters remain pending.",Bounds=new Rectangle(12,12,625,36)});
            locations.Name="libraryGameLocations";locations.Bounds=new Rectangle(12,52,625,150);pathTab.Controls.Add(locations);
            locations.SelectedIndexChanged+=(s,e)=>{if(locations.SelectedIndex<0)return;var l=data.GameLocations[locations.SelectedIndex];game.Text=l.Game;version.Text=l.GameVersion;path.Text=l.Path;};
            game.Name="libraryLocationGame";game.AccessibleName="Game name (choose or type a new game)";game.DropDownStyle=ComboBoxStyle.DropDown;game.MaxLength=128;game.Bounds=new Rectangle(12,216,165,26);game.Items.AddRange(AnimationLibrary.PlannedGames);game.SelectedIndex=0;
            version.Name="libraryLocationBuild";version.AccessibleName="Game build version";version.Text="Build not supplied";version.MaxLength=240;version.Bounds=new Rectangle(187,216,450,26);
            path.Name="libraryLocationPath";path.AccessibleName="Game build path";path.Bounds=new Rectangle(12,254,625,26);path.MaxLength=32767;
            var folder=new Button{Name="libraryLocationFolder",Text="Choose Folder...",Bounds=new Rectangle(12,292,125,29)};
            folder.Click+=(s,e)=>{using(var choose=new FolderBrowserDialog{Description="Choose the game's installation or extracted build folder"})if(choose.ShowDialog(this)==DialogResult.OK)path.Text=choose.SelectedPath;};
            var file=new Button{Name="libraryLocationFile",Text="Choose ROM / SD / File...",Bounds=new Rectangle(148,292,175,29)};
            file.Click+=(s,e)=>{using(var choose=new OpenFileDialog{Title="Choose ROM, SD Card or Game Archive",Filter="ROMs / discs / SD images / archives|*.iso;*.wbfs;*.rvz;*.gcm;*.raw;*.img;*.xci;*.nsp;*.arc;*.pak|All files|*.*"})if(choose.ShowDialog(this)==DialogResult.OK)path.Text=choose.FileName;};
            var save=new Button{Name="libraryLocationSave",Text="Save Game Path",Bounds=new Rectangle(12,341,145,30)};
            save.Click+=(s,e)=>Run(()=>{library.SetGameLocation(game.Text.Trim(),version.Text.Trim(),path.Text.Trim());Reload();});
            var removePath=new Button{Name="libraryLocationRemove",Text="Remove Selected Path",Bounds=new Rectangle(169,341,175,30)};
            removePath.Click+=(s,e)=>Run(()=>{if(locations.SelectedIndex<0)return;library.RemoveGameLocation(data.GameLocations[locations.SelectedIndex].Id);Reload();});
            var index=new Button{Name="libraryBrowseGame",Text="Browse Native Files...",Bounds=new Rectangle(356,341,180,30)};
            game.TextChanged+=(s,e)=>index.Enabled=new[]{"Brawl","Project M","Project+"}.Contains(game.Text.Trim());
            index.Click+=(s,e)=>Run(()=>{
                string sourcePath=path.Text.Trim();string tool=data.GameLocations.FirstOrDefault(l=>l.Path==sourcePath&&l.Indexed)?.ToolPath;
                if(File.Exists(sourcePath)&&string.IsNullOrEmpty(tool))
                    using(var choose=new OpenFileDialog{Title="Select Installed DolphinTool",Filter="DolphinTool|DolphinTool.exe"})
                    {if(choose.ShowDialog(this)!=DialogResult.OK)return;tool=choose.FileName;}
                browse(this,new LibraryGameRequest{Game=game.Text.Trim(),GameVersion=version.Text.Trim(),Path=sourcePath,DolphinToolPath=tool});Reload();
            });
            pathTab.Controls.AddRange(new Control[]{game,version,path,folder,file,save,removePath,index});
            var close=new Button{Name="librarySourcesClose",Text="Close",Dock=DockStyle.Bottom,Height=29,DialogResult=DialogResult.OK};
            Controls.Add(tabs);Controls.Add(close);CancelButton=close;Reload();
        }
        private void Run(Action action)
        {try{action();}catch(Exception e){MessageBox.Show(this,e.Message,"Library Sources",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
        private void Reload()
        {
            data=library.Read();sources.Items.Clear();locations.Items.Clear();
            foreach(var model in data.Models){var mount=data.Mounts.Single(m=>m.Id==model.MountId);sources.Items.Add(model.Name+" — "+mount.Game+" / "+mount.GameVersion+" — "+Path.GetFileName(mount.LocalPath));}
            foreach(var location in data.GameLocations)locations.Items.Add(location.Game+" / "+location.GameVersion+" — "+location.Label+(location.Indexed?" (indexed native source)":" (path only)"));
            undo.Enabled=library.CanUndo;
        }
    }
}
