using BrawlCrate.Studio;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal sealed class SavedGameSourcesDialog : Form
    {
        readonly AnimationLibrary library;
        readonly DataGridView list=new DataGridView{Name="savedGameSources",Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,MultiSelect=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoGenerateColumns=false};
        readonly TextBox name=new TextBox{Name="savedSourceName",Dock=DockStyle.Fill,MaxLength=240};
        readonly ComboBox game=new ComboBox{Name="savedSourceGame",Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDown,MaxLength=128};
        readonly TextBox build=new TextBox{Name="savedSourceBuild",Dock=DockStyle.Fill,MaxLength=240},path=new TextBox{Name="savedSourcePath",Dock=DockStyle.Fill,MaxLength=32767};
        readonly Label status=new Label{Name="savedSourceStatus",Dock=DockStyle.Fill,AutoSize=true};
        readonly Button save=new WorkspaceButton{Name="savedSourceSave",Text="Save changes",AutoSize=true},remove=new WorkspaceButton{Name="savedSourceRemove",Text="Remove",AutoSize=true},undo=new WorkspaceButton{Name="savedSourceUndo",Text="Undo last change",AutoSize=true};
        readonly Button file=new WorkspaceButton{Name="savedSourceFile",Text="Choose disc / file...",AutoSize=true},folder=new WorkspaceButton{Name="savedSourceFolder",Text="Choose game / SD folder...",AutoSize=true};
        readonly TableLayoutPanel editor=new TableLayoutPanel{Name="savedSourceEditor",Dock=DockStyle.Fill,ColumnCount=2,RowCount=4,AutoSize=true};
        readonly FlowLayoutPanel actions=new FlowLayoutPanel{Name="savedSourceActions",Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};
        LibraryCatalogue data;string selected;bool loading;
        internal SavedGameSourcesDialog(AnimationLibrary library)
        {
            this.library=library;Name="SavedGameSourcesDialog";Text="Saved games and paths";AutoScaleMode=AutoScaleMode.None;Font=WorkspaceAppearance.Font;ShowInTaskbar=false;
            StartPosition=FormStartPosition.CenterParent;MinimizeBox=MaximizeBox=false;
            ClientSize=new Size(Math.Min(1280,WorkspaceAppearance.Pixels(900)),Math.Min(900,WorkspaceAppearance.Pixels(510)));MinimumSize=new Size(760,480);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(10)};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));for(int i=1;i<5;i++)layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            string[] columns={"Name","Game / mod","Build","Type","Location","State"};int[] weights={90,65,80,50,180,60};
            for(int i=0;i<columns.Length;i++)list.Columns.Add(new DataGridViewTextBoxColumn{Name="savedSourceColumn"+i,HeaderText=columns[i],AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=weights[i],SortMode=DataGridViewColumnSortMode.NotSortable});
            list.RowTemplate.Height=Font.Height+10;list.ColumnHeadersHeight=Font.Height+14;list.AccessibleDescription="All saved disc and installation sources. Select a row to rename, edit its path or remove it. Double-click focuses its instance name.";
            list.SelectionChanged+=(s,e)=>{if(!loading)SelectRow();};list.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0){name.Focus();name.SelectAll();}};
            list.CurrentCellChanged+=(s,e)=>{if(!loading)SelectRow();};
            layout.Controls.Add(list,0,0);
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,Math.Max(90,Font.Height*5)));editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            for(int i=0;i<4;i++)editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var identity=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,RowCount=1,Margin=Padding.Empty};identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));
            game.Items.AddRange(AnimationLibrary.PlannedGames);identity.Controls.Add(game,0,0);identity.Controls.Add(build,1,0);
            string[] labels={"Name","Game / build","Location"};Control[] inputs={name,identity,path};
            for(int i=0;i<3;i++){editor.Controls.Add(new Label{Text=labels[i],AutoSize=true,Anchor=AnchorStyles.Left},0,i);editor.Controls.Add(inputs[i],1,i);}
            var pickers=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Margin=Padding.Empty};pickers.Controls.AddRange(new Control[]{file,folder});editor.Controls.Add(pickers,1,3);layout.Controls.Add(editor,0,1);
            file.Click+=(s,e)=>{using(var choose=new OpenFileDialog{Title="Choose saved source disc or game file",Filter="Game images / files|*.iso;*.wbfs;*.rvz;*.gcm;*.chd;*.bin;*.cue;*.img;*.xci;*.nsp;*.pak;*.arc|All files|*.*",CheckFileExists=true,FileName=File.Exists(path.Text)?path.Text:""})if(choose.ShowDialog(this)==DialogResult.OK)path.Text=choose.FileName;};
            folder.Click+=(s,e)=>{using(var choose=new FolderBrowserDialog{Description="Choose game installation, extracted build or Project M / Project+ SD folder",SelectedPath=Directory.Exists(path.Text)?path.Text:""})if(choose.ShowDialog(this)==DialogResult.OK)path.Text=choose.SelectedPath;};
            layout.Controls.Add(new Label{Text="Remove unregisters this source and its library entries. Game files, cached files and saved projects are kept. Indexed paths are verified before relocation.",AutoSize=true,Dock=DockStyle.Fill,Margin=new Padding(0,8,0,6)},0,2);
            status.MinimumSize=new Size(0,Font.Height+8);layout.Controls.Add(status,0,3);
            var close=new WorkspaceButton{Name="savedSourceClose",Text="Close",AutoSize=true,DialogResult=DialogResult.Cancel};actions.Controls.AddRange(new Control[]{save,remove,undo,close});layout.Controls.Add(actions,0,4);
            save.Click+=(s,e)=>Run(Save);remove.Click+=(s,e)=>Run(()=>{if(selected==null)return;library.RemoveSavedGameSource(selected);Reload();status.Text="Source removed from the library. Its files and saved target projects were kept.";});
            undo.Click+=(s,e)=>Run(()=>{library.UndoLastUpdate();Reload();status.Text="Last library change undone.";});
            Controls.Add(layout);CancelButton=close;DarkTheme.Apply(this);Reload();
            foreach(var button in new[]{save,remove,undo,close,file,folder})
            {button.Font=Font;button.MinimumSize=new Size(TextRenderer.MeasureText(button.Text,Font).Width+24,Font.Height+18);}
            name.AccessibleDescription="Rename this installation instance without changing its source ID, native content or game/build identity. Multiple Project M installs can have distinct names.";
            path.AccessibleDescription="Edit the saved disc or install/SD path. Indexed content is independently verified before linked paths change.";
        }
        void Reload(string id=null)
        {
            data=library.Read();loading=true;
            try
            {
                list.Rows.Clear();foreach(var row in data.GameLocations.OrderBy(l=>l.Game,StringComparer.OrdinalIgnoreCase).ThenBy(l=>l.Label,StringComparer.OrdinalIgnoreCase))
                {
                    bool exists=row.IsDirectory?Directory.Exists(row.Path):File.Exists(row.Path);
                    int at=list.Rows.Add(row.Label,row.Game,row.GameVersion,row.IsDirectory?"Folder / SD":"Disc / file",row.Path,!exists?"Missing":row.Indexed?"Indexed":"Saved");
                    list.Rows[at].Tag=row.Id;list.Rows[at].Cells[4].ToolTipText=row.Path;
                    if(row.Id==id)list.CurrentCell=list.Rows[at].Cells[0];
                }
                if(id==null&&list.Rows.Count>0)list.CurrentCell=list.Rows[0].Cells[0];
                undo.Enabled=library.CanUndo;
            }
            finally{loading=false;}
            SelectRow();status.ForeColor=ForeColor;status.Text=data.GameLocations.Count+" saved sources. Add new ones with Add game source.";
        }
        void SelectRow()
        {
            selected=list.CurrentRow?.Tag as string;var source=data.GameLocations.SingleOrDefault(l=>l.Id==selected);
            save.Enabled=remove.Enabled=source!=null;editor.Enabled=source!=null;
            name.Text=source?.Label??"";game.Text=source?.Game??"";build.Text=source?.GameVersion??"";path.Text=source?.Path??"";
            game.Enabled=build.Enabled=source?.Indexed!=true;
            file.Enabled=source!=null&&(!source.Indexed||!source.IsDirectory);folder.Enabled=source!=null&&(!source.Indexed||source.IsDirectory);
        }
        void Save()
        {
            if(selected==null)return;var source=data.GameLocations.Single(l=>l.Id==selected);string id=selected;
            string chosenGame=game.Text.Trim(),chosenBuild=build.Text.Trim(),chosenPath=path.Text.Trim(),chosenName=name.Text.Trim();
            if(source.Indexed&&!string.Equals(source.Path,path.Text.Trim(),StringComparison.OrdinalIgnoreCase))
            {
                var completed=LibraryWork.Run(this,"Verifying saved game source",(cancel,progress)=>{library.EditSavedGameSource(id,chosenGame,chosenBuild,chosenPath,cancel,progress.Report,chosenName);return "saved";});
                if(completed==null){status.Text="Verification cancelled. The saved source was kept.";return;}
            }
            else library.EditSavedGameSource(id,chosenGame,chosenBuild,chosenPath,name:chosenName);
            Reload(id);status.Text="Saved source updated. Its exact source identity and associations were retained.";
        }
        void Run(Action action)
        {try{action();}catch(Exception error){status.ForeColor=Color.FromArgb(246,174,116);status.Text=error.Message;}}
        protected override bool ProcessCmdKey(ref Message message,Keys keys)
        {
            if(keys==(Keys.Control|Keys.S)){if(save.Enabled)save.PerformClick();return true;}
            if(keys==Keys.F6||keys==(Keys.Shift|Keys.F6))
            {
                Control[] regions={list,editor,actions};int current=Array.FindIndex(regions,r=>r.ContainsFocus);int direction=keys==Keys.F6?1:-1;
                for(int step=1;step<=regions.Length;step++){var next=regions[(Math.Max(0,current)+direction*step+regions.Length*2)%regions.Length];if(!next.Enabled||!next.Visible)continue;if(!next.Focus())next.SelectNextControl(null,true,true,true,false);return true;}
            }
            return base.ProcessCmdKey(ref message,keys);
        }
    }
}
