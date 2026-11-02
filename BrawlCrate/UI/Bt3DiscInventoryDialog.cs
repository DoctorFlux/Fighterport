using BrawlCrate.Studio;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    // Read-only original-disc inspection. Native pose tracks are decoded on
    // demand; mesh/texture rendering and target editing remain unqualified.
    internal sealed class Bt3DiscInventoryDialog:Form
    {
        private readonly Bt3DiscInventory inventory;
        private readonly TextBox search=new TextBox{Name="bt3CharacterSearch",Dock=DockStyle.Fill};
        private readonly ListBox characters=new ListBox{Name="bt3Characters",Dock=DockStyle.Fill};
        private readonly ListBox costumes=new ListBox{Name="bt3Costumes",Dock=DockStyle.Fill};
        private readonly ListBox motions=new ListBox{Name="bt3MotionBanks",Dock=DockStyle.Fill};
        private readonly Label status=new Label{Name="bt3NativeStatus",Dock=DockStyle.Fill,AutoSize=true};
        private readonly Button inspect=new WorkspaceButton{Name="bt3InspectMotion",Text="Inspect selected form + costume + action",AutoSize=true};
        private readonly Button export=new WorkspaceButton{Name="bt3ExportOriginal",Text="Export original pack...",AutoSize=true};
        private Choice exportChoice;
        private sealed class Choice
        {
            internal Bt3DiscInventory.Bt3Resource Resource;
            internal int Action=-1;
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
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=6,Padding=new Padding(12)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,62));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,60));layout.RowStyles.Add(new RowStyle(SizeType.Percent,40));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var notice=new Label{Dock=DockStyle.Fill,AutoSize=true,Text="SLUS-21678 · Choose a character/form, its costume and an action separately. Native skeleton and pose tracks can be inspected. Mesh/texture preview, retargeting and native writing are not yet available."};
            layout.SetColumnSpan(notice,2);layout.Controls.Add(notice,0,0);
            layout.Controls.Add(new Label{Text="Find character / form (exact disc slot)",AutoSize=true},0,1);
            var left=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            left.Controls.Add(search,0,0);left.Controls.Add(characters,0,1);
            layout.Controls.Add(left,0,2);layout.SetRowSpan(left,2);
            var right=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1};
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));right.RowStyles.Add(new RowStyle(SizeType.Percent,40));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));right.RowStyles.Add(new RowStyle(SizeType.Percent,60));
            right.Controls.Add(new Label{Text="Costume (model and textures for the selected form)",AutoSize=true},0,0);
            right.Controls.Add(costumes,0,1);right.Controls.Add(new Label{Text="Native action (0 = idle; other names not yet verified)",AutoSize=true},0,2);
            right.Controls.Add(motions,0,3);layout.Controls.Add(right,1,2);layout.SetRowSpan(right,2);
            status.Text="Choose a form, costume and action, then inspect the native pose data.";
            layout.Controls.Add(status,0,4);layout.SetColumnSpan(status,2);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft};
            var close=new WorkspaceButton{Name="bt3Close",Text="Close",DialogResult=DialogResult.OK,AutoSize=true};
            footer.Controls.Add(close);footer.Controls.Add(export);footer.Controls.Add(inspect);layout.Controls.Add(footer,0,5);layout.SetColumnSpan(footer,2);
            Controls.Add(layout);AcceptButton=close;CancelButton=close;
            search.TextChanged+=(s,e)=>RefreshCharacters();characters.SelectedIndexChanged+=(s,e)=>RefreshResources();
            costumes.SelectedIndexChanged+=(s,e)=>{if(costumes.SelectedItem is Choice c)exportChoice=c;UpdateActions();};
            motions.SelectedIndexChanged+=(s,e)=>{if(motions.SelectedItem is Choice c)exportChoice=c;UpdateActions();};
            inspect.Click+=(s,e)=>InspectSelected();export.Click+=(s,e)=>ExportSelected();
            RefreshCharacters();UpdateActions();DarkTheme.Apply(this);Shown+=(s,e)=>search.Focus();
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
            exportChoice=null;costumes.Items.Clear();motions.Items.Clear();
            if(characters.SelectedItem is Bt3DiscInventory.Bt3FighterSlot fighter)
            {
                foreach(var resource in fighter.Costumes)
                    costumes.Items.Add(new Choice{Resource=resource,Label="Costume "+(resource.AfsIndex-(Bt3DiscInventory.FighterAfsBase+fighter.Slot*10))+" · "+resource.Identity+" · "+resource.Bytes+" bytes"});
                for(int bank=0;bank<2;bank++)foreach(var resource in fighter.MotionMembers[bank])
                    motions.Items.Add(new Choice{Resource=resource,Action=bank==0&&resource.PackIndex>0&&resource.PackIndex<=0x19E?resource.PackIndex-1:-1,
                        Label=bank==0&&resource.PackIndex>0&&resource.PackIndex<=0x19E?
                            (resource.PackIndex==1?"Idle · action 000":"Action "+(resource.PackIndex-1).ToString("D3"))+" · "+resource.Identity+" · "+resource.Bytes+" bytes":
                            "Bank "+bank+" · member "+resource.PackIndex.ToString("D3")+" · "+resource.Identity+" · "+resource.Bytes+" bytes"});
                if(costumes.Items.Count>0)costumes.SelectedIndex=0;
                if(fighter.MotionMembers[0].Length>1)motions.SelectedIndex=1;
            }
            UpdateActions();
        }
        private void UpdateActions()
        {
            export.Enabled=exportChoice!=null;
            inspect.Enabled=characters.SelectedItem is Bt3DiscInventory.Bt3FighterSlot&&
                costumes.SelectedItem is Choice&&motions.SelectedItem is Choice selected&&selected.Action>=0;
            status.Text="Selection changed. Inspect the exact form, costume and action to verify native pose data.";
        }
        private void InspectSelected()
        {
            var fighter=characters.SelectedItem as Bt3DiscInventory.Bt3FighterSlot;
            var costume=costumes.SelectedItem as Choice;var action=motions.SelectedItem as Choice;
            if(fighter==null||costume==null||action==null||action.Action<0)return;
            try
            {
                string report=LibraryWork.Run(this,"Inspecting native BT3 pose",(cancel,progress)=>
                {
                    progress.Report("Reading the selected original model and action directly from the ISO...");
                    cancel.ThrowIfCancellationRequested();
                    var model=inventory.ModelMember(fighter,costume.Resource);
                    var rig=Bt3NativeAssetReader.ReadSkeleton(inventory.ReadOwned(model,4*1024*1024));
                    var clip=Bt3NativeAssetReader.ReadMotion(inventory.ReadOwned(action.Resource,4*1024*1024),action.Action);
                    if(!clip.Tracks.All(t=>rig.Bones.Any(b=>b.Id==t.Bone)))
                        throw new InvalidDataException("This action references a bone absent from the selected costume.");
                    cancel.ThrowIfCancellationRequested();
                    return fighter.Display+" · "+costume.Label.Split('·')[0].Trim()+" · action "+action.Action+
                        " · "+rig.Bones.Length+" bones · "+clip.Tracks.Length+" tracks · "+
                        clip.Tracks.Sum(t=>t.Keys.Length)+" keys · last native frame "+clip.Length+
                        ". Pose data verified; mesh and texture preview remain unavailable.";
                });
                if(report!=null)status.Text=report;
            }
            catch(Exception error)when(error is InvalidDataException||error is IOException||error is UnauthorizedAccessException)
            {status.Text=error.Message;}
        }
        private void ExportSelected()
        {
            var selected=exportChoice;if(selected==null)return;
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
