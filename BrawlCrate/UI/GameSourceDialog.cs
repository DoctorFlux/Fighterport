using BrawlCrate.Studio;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    // Location selection is game-neutral; only an available adapter may index
    // resources. A saved location is never represented as decoded game support.
    internal sealed class GameSourceDialog : Form
    {
        private readonly ComboBox saved=new ComboBox{Name="gameSourceSaved",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        private readonly LibraryCatalogue catalogue;
        private readonly ComboBox baseSource=new ComboBox{Name="gameSourceBase",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        private readonly Label baseLabel=new Label{Text="Base Melee source (only for partial mod folders)",AutoSize=true,Dock=DockStyle.Fill};
        private readonly ComboBox game=new ComboBox{Name="gameSourceGame",DropDownStyle=ComboBoxStyle.DropDown,Dock=DockStyle.Fill,MaxLength=128};
        private readonly ComboBox kind=new ComboBox{Name="gameSourceKind",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        private readonly TextBox path=new TextBox{Name="discFilePath",Dock=DockStyle.Fill,MaxLength=32767};
        private readonly TextBox build=new TextBox{Name="discBuild",Dock=DockStyle.Fill,MaxLength=240,Text="Build unknown"};
        private readonly Label status=new Label{Name="discValidation",Dock=DockStyle.Fill,AutoSize=true};
        private readonly Button save=new WorkspaceButton{Name="discContinue",AutoSize=true,Padding=new Padding(12,6,12,6)};
        private readonly Button bt3Inspect=new WorkspaceButton{Name="bt3InspectDisc",Text="Inspect BT3 characters and costumes...",AutoSize=true};
        internal LibraryGameRequest Request {get;private set;}
        internal bool PathOnly {get;private set;}
        internal string ExistingSourceId {get;private set;}
        internal bool SourcesChanged {get;private set;}
        private sealed class SavedChoice
        {
            internal LibraryGameLocation Location;
            public override string ToString()=>Location.Game+" · "+Location.Label+" · "+Location.GameVersion+" · "+(Location.IsDirectory?"folder / SD":"disc / file")+" · "+(Location.Indexed?"indexed":"saved")+" · "+Location.Path;
        }
        internal GameSourceDialog(LibraryGameLocation[] sources,AnimationLibrary library=null,Action beforeManage=null)
        {
            catalogue=library?.Read()??new LibraryCatalogue();
            Name="GameSourceDialog";Text="Add game source";ShowInTaskbar=false;
            AutoScaleMode=AutoScaleMode.None;AutoScroll=true;Font=WorkspaceAppearance.Font;
            StartPosition=FormStartPosition.CenterParent;MinimizeBox=MaximizeBox=false;
            ClientSize=new Size(Math.Min(1100,WorkspaceAppearance.Pixels(800)),Math.Min(820,WorkspaceAppearance.Pixels(560)));
            MinimumSize=new Size(640,510);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=14,Padding=new Padding(14),MinimumSize=new Size(0,Font.Height*25+110)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            for(int i=0;i<14;i++)layout.RowStyles.Add(new RowStyle(i==12?SizeType.Percent:SizeType.AutoSize,i==12?100:0));
            layout.Controls.Add(new Label{Text="Choose a saved source, a disc image, or a game/install/SD folder. Manage saved sources to rename, edit or remove separate installations.",AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,0,0,10)},0,0);
            saved.AccessibleName="Saved game sources";saved.Items.Add("New game source...");
            saved.Items.AddRange((sources??new LibraryGameLocation[0]).Select(s=>new SavedChoice{Location=s}).ToArray());saved.SelectedIndex=0;
            var savedRow=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,RowCount=1,Margin=Padding.Empty};savedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));savedRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var manage=new WorkspaceButton{Name="gameSourceManage",Text="Manage...",AutoSize=true,Enabled=library!=null};
            manage.AccessibleDescription="See all saved sources. Rename separate Project M installations, edit paths or remove entries without deleting their files.";
            manage.Click+=(s,e)=>{
                if(library==null)return;beforeManage?.Invoke();long generation=library.Read().Generation;string selectedId=(saved.SelectedItem as SavedChoice)?.Location.Id;
                using(var window=new SavedGameSourcesDialog(library))window.ShowDialog(this);
                var updated=library.Read();if(updated.Generation==generation)return;SourcesChanged=true;
                saved.Items.Clear();saved.Items.Add("New game source...");saved.Items.AddRange(updated.GameLocations.Select(l=>new SavedChoice{Location=l}).ToArray());
                var previous=saved.Items.OfType<SavedChoice>().SingleOrDefault(c=>c.Location.Id==selectedId);saved.SelectedItem=(object)previous??saved.Items[0];ResetStatus();
            };
            savedRow.Controls.Add(saved,0,0);savedRow.Controls.Add(manage,1,0);layout.Controls.Add(savedRow,0,1);
            var identity=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,RowCount=2,Margin=new Padding(0,8,0,8)};
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));
            identity.Controls.Add(new Label{Text="Game (choose or type)",AutoSize=true,Dock=DockStyle.Fill},0,0);
            identity.Controls.Add(new Label{Text="Build / version",AutoSize=true,Dock=DockStyle.Fill},1,0);
            game.Items.AddRange(AnimationLibrary.PlannedGames);game.Text="Brawl";game.AccessibleName="Source game";build.AccessibleName="Source build or version";
            identity.Controls.Add(game,0,1);identity.Controls.Add(build,1,1);layout.Controls.Add(identity,0,2);
            layout.Controls.Add(new Label{Text="Source type",AutoSize=true,Dock=DockStyle.Fill},0,3);
            kind.Items.AddRange(new object[]{"Disc image / game file","Game / install / SD folder"});kind.SelectedIndex=0;kind.AccessibleName="Disc image or game install or SD folder";layout.Controls.Add(kind,0,4);
            layout.Controls.Add(new Label{Text="Location",AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,8,0,0)},0,5);
            path.AccessibleName="Game disc image or installation path";layout.Controls.Add(path,0,6);
            var pickers=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};
            var file=new WorkspaceButton{Name="discBrowseFile",Text="Choose disc / file...",AutoSize=true};
            var folder=new WorkspaceButton{Name="discBrowseFolder",Text="Choose game folder...",AutoSize=true};
            file.Click+=(s,e)=>{using(var choose=new OpenFileDialog{Title="Choose game disc image or file",Filter="Game images / files|*.iso;*.wbfs;*.rvz;*.gcm;*.chd;*.bin;*.cue;*.img;*.raw;*.xci;*.nsp;*.pak;*.arc|All files|*.*",CheckFileExists=true,FileName=File.Exists(path.Text)?path.Text:""})if(choose.ShowDialog(this)==DialogResult.OK){kind.SelectedIndex=0;path.Text=choose.FileName;}};
            folder.Click+=(s,e)=>{using(var choose=new FolderBrowserDialog{Description="Choose the game's installation or extracted game folder",SelectedPath=Directory.Exists(path.Text)?path.Text:""})if(choose.ShowDialog(this)==DialogResult.OK){kind.SelectedIndex=1;path.Text=choose.SelectedPath;}};
            pickers.Controls.AddRange(new Control[]{file,folder});layout.Controls.Add(pickers,0,7);
            status.MinimumSize=new Size(0,Font.Height*3+12);status.AccessibleName="Game source support and saved location status";layout.Controls.Add(status,0,8);
            bt3Inspect.AccessibleDescription="Read exact BT3 forms, costumes and action members from the original ISO; inspect native skeleton and pose tracks. Mesh preview and retargeting are not supported yet.";
            bt3Inspect.Click+=(s,e)=>InspectBt3();layout.Controls.Add(bt3Inspect,0,11);
            var cancel=new WorkspaceButton{Name="discCancel",Text="Cancel",AutoSize=true,DialogResult=DialogResult.Cancel,Padding=new Padding(12,6,12,6)};
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,FlowDirection=FlowDirection.RightToLeft};actions.Controls.AddRange(new Control[]{cancel,save});layout.Controls.Add(actions,0,13);
            baseSource.Items.Add("None — complete source");baseSource.Items.AddRange((sources??new LibraryGameLocation[0]).Where(l=>l.Game=="Melee"&&l.Indexed&&l.BaseSourceId==null).Select(l=>new SavedChoice{Location=l}).ToArray());baseSource.SelectedIndex=0;
            layout.Controls.Add(baseLabel,0,9);layout.Controls.Add(baseSource,0,10);baseSource.SelectedIndexChanged+=(s,e)=>ResetStatus();
            Controls.Add(layout);AcceptButton=save;CancelButton=cancel;
            saved.SelectedIndexChanged+=(s,e)=>{if(saved.SelectedItem is SavedChoice choice){var location=choice.Location;game.Text=location.Game;build.Text=location.GameVersion;kind.SelectedIndex=location.IsDirectory?1:0;path.Text=location.Path;baseSource.SelectedItem=(object)baseSource.Items.OfType<SavedChoice>().SingleOrDefault(b=>b.Location.Id==location.BaseSourceId)??baseSource.Items[0];}ResetStatus();};
            foreach(var field in new Control[]{game,build,path})field.TextChanged+=(s,e)=>ResetStatus();kind.SelectedIndexChanged+=(s,e)=>ResetStatus();
            save.Click+=(s,e)=>Continue();
            if(saved.Items.Count>1)saved.SelectedIndex=saved.Items.Count-1;
            ResetStatus();DarkTheme.Apply(this);Shown+=(s,e)=>path.Focus();
            foreach(var button in new Button[]{file,folder,manage,cancel})
            {button.Font=Font;button.MinimumSize=new Size(TextRenderer.MeasureText(button.Text,Font).Width+24,Font.Height+16);}
            int tab=0;foreach(var control in new Control[]{saved,game,build,kind,path,file,folder,baseSource,bt3Inspect,save,cancel})control.TabIndex=tab++;
        }
        private string GameName=>AnimationLibrary.PlannedGames.FirstOrDefault(g=>string.Equals(g,game.Text.Trim(),StringComparison.OrdinalIgnoreCase))??game.Text.Trim();
        private bool GameReaderAvailable=>NativeGameAdapters.ForGame(GameName)!=null;
        private bool CanIndex=>GameReaderAvailable&&(kind.SelectedIndex==1||string.IsNullOrWhiteSpace(path.Text)||NativeGameAdapters.ForGame(GameName).ImageExtensions.Contains(System.IO.Path.GetExtension(path.Text.Trim().Trim('"')),StringComparer.OrdinalIgnoreCase));
        private LibraryGameLocation ExistingSource=>(saved.SelectedItem as SavedChoice)?.Location is LibraryGameLocation location&&
            (location.Indexed||location.Game==Bt3DiscInventory.Game)&&
            location.Game==GameName&&location.GameVersion==build.Text.Trim()&&location.BaseSourceId==(GameName=="Melee"&&kind.SelectedIndex==1?(baseSource.SelectedItem as SavedChoice)?.Location.Id:null)&&
            string.Equals(location.Path,path.Text.Trim().Trim('"'),StringComparison.OrdinalIgnoreCase)&&location.IsDirectory==(kind.SelectedIndex==1)?location:null;
        private void ResetStatus()
        {
            baseSource.Visible=baseLabel.Visible=GameName=="Melee"&&kind.SelectedIndex==1;
            bt3Inspect.Visible=GameName==Bt3DiscInventory.Game;
            bt3Inspect.Enabled=bt3Inspect.Visible&&kind.SelectedIndex==0&&File.Exists(path.Text.Trim().Trim('"'));
            Request=null;PathOnly=false;ExistingSourceId=null;status.ForeColor=ForeColor;
            save.Text=ExistingSource!=null?(GameName==Bt3DiscInventory.Game?"Browse saved disc":"Open saved library"):CanIndex?"Add to library":"Save source";
            save.Font=Font;save.MinimumSize=new Size(TextRenderer.MeasureText(save.Text,Font).Width+28,Font.Height+20);
            status.Text=ExistingSource!=null?(GameName==Bt3DiscInventory.Game?"Browse this saved BT3 disc directly: form, costume and action rows appear in the library. Native mesh preview and retargeting remain unavailable.":"This source is already indexed. Open its character and animation library without extracting the game again."):GameName==Bt3DiscInventory.Game?"BT3 USA ISO: inspect exact forms and costumes plus native skeleton/pose tracks directly from the disc. The saved disc can be browsed in the library; mesh/texture preview, retargeting and writing are not qualified yet.":CanIndex&&GameName=="Melee"?"Melee reader: ISO/GCM, extracted game or mod folders. Partial folders use the explicitly selected base. Native source preview; authoring/export qualify separately.":CanIndex?"Brawl reader available: ISO/WBFS or an extracted fighter folder. Adds all characters, animations and compatible costumes; original files stay read-only.":GameReaderAvailable?"This source format cannot be read yet. Save its location for later; the Brawl reader currently accepts ISO/WBFS and extracted fighter folders.":"This game's reader is not available yet. Save its disc/file or install-folder location now; animations will become available when its adapter is added.";
            if(saved.SelectedItem is SavedChoice choice&&(!File.Exists(choice.Location.Path)&&!Directory.Exists(choice.Location.Path)))status.Text="Saved location is unavailable. Locate its disc/file or folder to use it again. "+status.Text;
        }
        private void InspectBt3()
        {
            try
            {
                string source=AnimationLibrary.OrdinaryPath(path.Text.Trim().Trim('"'));
                var inventory=LibraryWork.Run(this,"Inspecting BT3 disc",(cancel,progress)=>
                {progress.Report("Reading ISO9660 and AFS tables directly; no game files are extracted...");return Bt3DiscInventory.Inspect(source,cancel);});
                if(inventory!=null)using(var window=new Bt3DiscInventoryDialog(inventory))window.ShowDialog(this);
            }
            catch(Exception error)when(error is InvalidDataException||error is IOException||error is UnauthorizedAccessException||error is ArgumentException)
            {status.ForeColor=Color.FromArgb(246,174,116);status.Text=error.Message;}
        }
        private void Continue()
        {
            try
            {
                var request=new LibraryGameRequest{Game=GameName,GameVersion=build.Text.Trim(),BaseSourceId=GameName=="Melee"&&kind.SelectedIndex==1?(baseSource.SelectedItem as SavedChoice)?.Location.Id:null,Path=AnimationLibrary.OrdinaryPath(path.Text.Trim().Trim('"'))};
                AnimationLibrary.NoLinks(request.Path);
                if(string.IsNullOrWhiteSpace(request.Game)||request.Game.Length>128||string.IsNullOrWhiteSpace(request.GameVersion)||request.GameVersion.Length>240)throw new InvalidDataException("Enter the game and build/version, or explicitly mark the build unknown.");
                if(ExistingSource!=null){ExistingSourceId=ExistingSource.Id;Request=request;DialogResult=DialogResult.OK;return;}
                bool directory=Directory.Exists(request.Path);
                if((kind.SelectedIndex==1&&!directory)||(kind.SelectedIndex==0&&!File.Exists(request.Path)))throw new InvalidDataException(kind.SelectedIndex==1?"Choose an existing game/install folder.":"Choose an existing disc image or game file.");
                if(CanIndex)
                {
                    var checkedSource=LibraryWork.Run(this,"Checking game source",(cancel,progress)=>{progress.Report("Checking the game header or native source folders; no full-image hash or fighter extraction yet...");NativeGameAdapters.ForGame(request.Game).Validate(request,catalogue,cancel);return request;});
                    if(checkedSource==null){status.Text="Source check cancelled. No source was added.";return;}
                    Request=checkedSource;
                }
                else
                {
                    if(request.Game==Bt3DiscInventory.Game)
                    {
                        var inventory=LibraryWork.Run(this,"Checking BT3 disc",(cancel,progress)=>
                        {progress.Report("Checking the exact PS2 disc layout before saving its location...");return Bt3DiscInventory.Inspect(request.Path,cancel);});
                        if(inventory==null)return;
                    }
                    Request=request;PathOnly=true;
                }
                DialogResult=DialogResult.OK;
            }
            catch(Exception error)when(error is InvalidDataException||error is IOException||error is InvalidOperationException||error is ArgumentException||error is NotSupportedException||error is UnauthorizedAccessException||error is System.Security.SecurityException||error is FormatException||error is OverflowException)
            {status.ForeColor=Color.FromArgb(246,174,116);status.Text=error.Message;}
        }
    }
}
