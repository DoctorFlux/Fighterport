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
        private readonly ComboBox game=new ComboBox{Name="gameSourceGame",DropDownStyle=ComboBoxStyle.DropDown,Dock=DockStyle.Fill,MaxLength=128};
        private readonly ComboBox kind=new ComboBox{Name="gameSourceKind",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
        private readonly TextBox path=new TextBox{Name="discFilePath",Dock=DockStyle.Fill,MaxLength=32767};
        private readonly TextBox build=new TextBox{Name="discBuild",Dock=DockStyle.Fill,MaxLength=240,Text="Build unknown"};
        private readonly Label status=new Label{Name="discValidation",Dock=DockStyle.Fill,AutoSize=true};
        private readonly Button save=new WorkspaceButton{Name="discContinue",AutoSize=true,Padding=new Padding(12,6,12,6)};
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
            Name="GameSourceDialog";Text="Add game source";ShowInTaskbar=false;
            AutoScaleMode=AutoScaleMode.None;AutoScroll=true;Font=WorkspaceAppearance.Font;
            StartPosition=FormStartPosition.CenterParent;MinimizeBox=MaximizeBox=false;
            ClientSize=new Size(Math.Min(1100,WorkspaceAppearance.Pixels(800)),Math.Min(820,WorkspaceAppearance.Pixels(560)));
            MinimumSize=new Size(640,510);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=12,Padding=new Padding(14),MinimumSize=new Size(0,Font.Height*25+110)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            for(int i=0;i<12;i++)layout.RowStyles.Add(new RowStyle(i==10?SizeType.Percent:SizeType.AutoSize,i==10?100:0));
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
            var cancel=new WorkspaceButton{Name="discCancel",Text="Cancel",AutoSize=true,DialogResult=DialogResult.Cancel,Padding=new Padding(12,6,12,6)};
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,FlowDirection=FlowDirection.RightToLeft};actions.Controls.AddRange(new Control[]{cancel,save});layout.Controls.Add(actions,0,11);
            Controls.Add(layout);AcceptButton=save;CancelButton=cancel;
            saved.SelectedIndexChanged+=(s,e)=>{if(saved.SelectedItem is SavedChoice choice){var location=choice.Location;game.Text=location.Game;build.Text=location.GameVersion;kind.SelectedIndex=location.IsDirectory?1:0;path.Text=location.Path;}ResetStatus();};
            foreach(var field in new Control[]{game,build,path})field.TextChanged+=(s,e)=>ResetStatus();kind.SelectedIndexChanged+=(s,e)=>ResetStatus();
            save.Click+=(s,e)=>Continue();
            if(saved.Items.Count>1)saved.SelectedIndex=saved.Items.Count-1;
            ResetStatus();DarkTheme.Apply(this);Shown+=(s,e)=>path.Focus();
            foreach(var button in new Button[]{file,folder,manage,cancel})
            {button.Font=Font;button.MinimumSize=new Size(TextRenderer.MeasureText(button.Text,Font).Width+24,Font.Height+16);}
            int tab=0;foreach(var control in new Control[]{saved,game,build,kind,path,file,folder,save,cancel})control.TabIndex=tab++;
        }
        private string GameName=>AnimationLibrary.PlannedGames.FirstOrDefault(g=>string.Equals(g,game.Text.Trim(),StringComparison.OrdinalIgnoreCase))??game.Text.Trim();
        private bool GameReaderAvailable=>new[]{"Brawl","Project M","Project+"}.Contains(GameName,StringComparer.Ordinal);
        private bool CanIndex=>GameReaderAvailable&&(kind.SelectedIndex==1||string.IsNullOrWhiteSpace(path.Text)||
            path.Text.Trim().Trim('"').EndsWith(".iso",StringComparison.OrdinalIgnoreCase)||path.Text.Trim().Trim('"').EndsWith(".wbfs",StringComparison.OrdinalIgnoreCase));
        private LibraryGameLocation ExistingSource=>(saved.SelectedItem as SavedChoice)?.Location is LibraryGameLocation location&&location.Indexed&&
            location.Game==GameName&&location.GameVersion==build.Text.Trim()&&
            string.Equals(location.Path,path.Text.Trim().Trim('"'),StringComparison.OrdinalIgnoreCase)&&location.IsDirectory==(kind.SelectedIndex==1)?location:null;
        private void ResetStatus()
        {
            Request=null;PathOnly=false;ExistingSourceId=null;status.ForeColor=ForeColor;
            save.Text=ExistingSource!=null?"Open saved library":CanIndex?"Add to library":"Save source";
            save.Font=Font;save.MinimumSize=new Size(TextRenderer.MeasureText(save.Text,Font).Width+28,Font.Height+20);
            status.Text=ExistingSource!=null?"This source is already indexed. Open its character and animation library without extracting the game again.":CanIndex?"Brawl reader available: ISO/WBFS or an extracted fighter folder. Adds all characters, animations and compatible costumes; original files stay read-only.":GameReaderAvailable?"This source format cannot be read yet. Save its location for later; the Brawl reader currently accepts ISO/WBFS and extracted fighter folders.":"This game's reader is not available yet. Save its disc/file or install-folder location now; animations will become available when its adapter is added.";
            if(saved.SelectedItem is SavedChoice choice&&(!File.Exists(choice.Location.Path)&&!Directory.Exists(choice.Location.Path)))status.Text="Saved location is unavailable. Locate its disc/file or folder to use it again. "+status.Text;
        }
        private void Continue()
        {
            try
            {
                var request=new LibraryGameRequest{Game=GameName,GameVersion=build.Text.Trim(),Path=AnimationLibrary.OrdinaryPath(path.Text.Trim().Trim('"'))};
                AnimationLibrary.NoLinks(request.Path);
                if(string.IsNullOrWhiteSpace(request.Game)||request.Game.Length>128||string.IsNullOrWhiteSpace(request.GameVersion)||request.GameVersion.Length>240)throw new InvalidDataException("Enter the game and build/version, or explicitly mark the build unknown.");
                if(ExistingSource!=null){ExistingSourceId=ExistingSource.Id;Request=request;DialogResult=DialogResult.OK;return;}
                bool directory=Directory.Exists(request.Path);
                if((kind.SelectedIndex==1&&!directory)||(kind.SelectedIndex==0&&!File.Exists(request.Path)))throw new InvalidDataException(kind.SelectedIndex==1?"Choose an existing game/install folder.":"Choose an existing disc image or game file.");
                if(CanIndex)
                {
                    var checkedSource=LibraryWork.Run(this,"Checking game source",(cancel,progress)=>{progress.Report("Checking the Brawl header or extracted fighter folders; no full-image hash or fighter extraction yet...");BrawlDiscDialog.ValidateRequest(request,cancel);return request;});
                    if(checkedSource==null){status.Text="Source check cancelled. No source was added.";return;}
                    Request=checkedSource;
                }
                else{Request=request;PathOnly=true;}
                DialogResult=DialogResult.OK;
            }
            catch(Exception error)when(error is InvalidDataException||error is IOException||error is InvalidOperationException||error is ArgumentException||error is NotSupportedException||error is UnauthorizedAccessException||error is System.Security.SecurityException||error is FormatException||error is OverflowException)
            {status.ForeColor=Color.FromArgb(246,174,116);status.Text=error.Message;}
        }
    }
}
