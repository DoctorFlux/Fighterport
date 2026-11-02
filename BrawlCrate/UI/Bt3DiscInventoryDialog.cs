using BrawlCrate.Studio;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    // Original-disc inventory only. A motion-bank member is not a decoded
    // animation and cannot be sent to the native viewer or retargeter.
    internal sealed class Bt3DiscInventoryDialog:Form
    {
        private readonly Bt3DiscInventory inventory;
        private readonly TextBox search=new TextBox{Name="bt3CharacterSearch",Dock=DockStyle.Fill};
        private readonly ListBox characters=new ListBox{Name="bt3Characters",Dock=DockStyle.Fill};
        private readonly ListBox costumes=new ListBox{Name="bt3Costumes",Dock=DockStyle.Fill};
        private readonly ListBox motions=new ListBox{Name="bt3MotionBanks",Dock=DockStyle.Fill};
        private readonly Button export=new WorkspaceButton{Name="bt3ExportOriginal",Text="Export original pack...",AutoSize=true};
        private sealed class Choice
        {
            internal Bt3DiscInventory.Bt3Resource Resource;
            internal string Label;
            public override string ToString()=>Label;
        }
        internal Bt3DiscInventoryDialog(Bt3DiscInventory inventory)
        {
            this.inventory=inventory??throw new ArgumentNullException(nameof(inventory));
            Name="Bt3DiscInventoryDialog";Text="BT3 original disc inventory";Font=WorkspaceAppearance.Font;
            AutoScaleMode=AutoScaleMode.None;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
            ClientSize=new Size(Math.Min(1050,WorkspaceAppearance.Pixels(850)),Math.Min(740,WorkspaceAppearance.Pixels(580)));
            MinimumSize=new Size(670,440);
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=5,Padding=new Padding(12)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,62));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,60));layout.RowStyles.Add(new RowStyle(SizeType.Percent,40));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var notice=new Label{Dock=DockStyle.Fill,AutoSize=true,Text="SLUS-21678 · Character slots, costume/model packs and two raw motion banks are read directly from the ISO. Motion curves, textures and models are not decoded yet; preview, retarget and native writing are unavailable."};
            layout.SetColumnSpan(notice,2);layout.Controls.Add(notice,0,0);
            layout.Controls.Add(new Label{Text="Find character slot or disc label",AutoSize=true},0,1);
            var left=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            left.Controls.Add(search,0,0);left.Controls.Add(characters,0,1);
            layout.Controls.Add(left,0,2);layout.SetRowSpan(left,2);
            var right=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1};
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));right.RowStyles.Add(new RowStyle(SizeType.Percent,40));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));right.RowStyles.Add(new RowStyle(SizeType.Percent,60));
            right.Controls.Add(new Label{Text="Costume / model packs (native pmdl verified)",AutoSize=true},0,0);
            right.Controls.Add(costumes,0,1);right.Controls.Add(new Label{Text="Motion bank members (opaque native bytes; not animation names)",AutoSize=true},0,2);
            right.Controls.Add(motions,0,3);layout.Controls.Add(right,1,2);layout.SetRowSpan(right,2);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft};
            var close=new WorkspaceButton{Name="bt3Close",Text="Close",DialogResult=DialogResult.OK,AutoSize=true};
            footer.Controls.Add(close);footer.Controls.Add(export);layout.Controls.Add(footer,0,4);layout.SetColumnSpan(footer,2);
            Controls.Add(layout);AcceptButton=close;CancelButton=close;
            search.TextChanged+=(s,e)=>RefreshCharacters();characters.SelectedIndexChanged+=(s,e)=>RefreshResources();
            costumes.SelectedIndexChanged+=(s,e)=>{if(costumes.SelectedIndex>=0)motions.ClearSelected();UpdateExport();};
            motions.SelectedIndexChanged+=(s,e)=>{if(motions.SelectedIndex>=0)costumes.ClearSelected();UpdateExport();};
            export.Click+=(s,e)=>ExportSelected();RefreshCharacters();UpdateExport();DarkTheme.Apply(this);Shown+=(s,e)=>search.Focus();
        }
        private void RefreshCharacters()
        {
            string term=search.Text.Trim();int prior=(characters.SelectedItem as Bt3DiscInventory.Bt3FighterSlot)?.Slot??-1;
            characters.BeginUpdate();characters.Items.Clear();
            foreach(var fighter in inventory.Fighters.Where(f=>term.Length==0||f.Display.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0))characters.Items.Add(fighter);
            characters.EndUpdate();
            characters.SelectedItem=characters.Items.OfType<Bt3DiscInventory.Bt3FighterSlot>().FirstOrDefault(f=>f.Slot==prior)??characters.Items.Cast<object>().FirstOrDefault();
        }
        private void RefreshResources()
        {
            costumes.Items.Clear();motions.Items.Clear();
            if(characters.SelectedItem is Bt3DiscInventory.Bt3FighterSlot fighter)
            {
                foreach(var resource in fighter.Costumes)
                    costumes.Items.Add(new Choice{Resource=resource,Label="Costume "+(resource.AfsIndex-(Bt3DiscInventory.FighterAfsBase+fighter.Slot*10))+" · "+resource.Identity+" · "+resource.Bytes+" bytes"});
                for(int bank=0;bank<2;bank++)foreach(var resource in fighter.MotionMembers[bank])
                    motions.Items.Add(new Choice{Resource=resource,Label="Bank "+bank+" · member "+resource.PackIndex.ToString("D3")+" · "+resource.Identity+" · "+resource.Bytes+" bytes"});
            }
            UpdateExport();
        }
        private void UpdateExport()=>export.Enabled=(costumes.SelectedItem??motions.SelectedItem) is Choice;
        private void ExportSelected()
        {
            var selected=(costumes.SelectedItem??motions.SelectedItem) as Choice;if(selected==null)return;
            using(var dialog=new SaveFileDialog{Title="Export exact original BT3 pack bytes",Filter="Original binary pack|*.bin|All files|*.*",FileName="bt3-"+selected.Resource.AfsIndex.ToString("D5")+(selected.Resource.PackIndex<0?"":"-"+selected.Resource.PackIndex.ToString("D3"))+".bin",OverwritePrompt=false})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try
                {
                    var hash=inventory.ExportOriginal(selected.Resource,dialog.FileName);
                    MessageBox.Show(this,"Original bytes exported from "+selected.Resource.Identity+".\nSHA-256: "+hash,"BT3 original export",MessageBoxButtons.OK,MessageBoxIcon.Information);
                }
                catch(Exception error)when(error is IOException||error is InvalidDataException||error is UnauthorizedAccessException||error is OperationCanceledException)
                {MessageBox.Show(this,error.Message,"BT3 original export",MessageBoxButtons.OK,MessageBoxIcon.Error);}
            }
        }
    }
}
