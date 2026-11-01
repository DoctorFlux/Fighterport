using BrawlCrate.Studio;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private void ShowMeleeSourceParts()
        {
            if(SimpleWorkspaceBusy||_libraryLease?.MeleeRenderer?.Parts==null)return;
            CancelLibraryAutoPreview();PauseSourceOperation();var renderer=_libraryLease.MeleeRenderer;var parts=renderer.Parts;
            int[] before=Enumerable.Range(0,parts.GroupCount).Select(parts.Choice).ToArray();
            using(var dialog=BuildMeleeSourcePartsDialog())
            {
                bool keep=false;
                try{keep=dialog.ShowDialog(this)==DialogResult.OK;}
                finally
                {
                    if(!keep)for(int i=0;i<before.Length;i++)parts.Select(i,before[i]);
                    renderer.RefreshParts();_libraryViewport.Invalidate();
                }
            }
            if(_sourcePartsButton.CanFocus)_sourcePartsButton.Focus();
        }
        private Form BuildMeleeSourcePartsDialog()
        {
            var renderer=_libraryLease.MeleeRenderer;var parts=renderer.Parts;
            var dialog=new Form{Name="nativeSourceParts",Text="Source model parts",ClientSize=new Size(Math.Max(410,WorkspaceAppearance.Font.Height*23),Math.Max(370,WorkspaceAppearance.Font.Height*19)),MinimumSize=new Size(340,250),StartPosition=FormStartPosition.CenterParent,Font=WorkspaceAppearance.Font,MinimizeBox=false,MaximizeBox=false};
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(10)};
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,Math.Max(74,WorkspaceAppearance.Font.Height*5)));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,Math.Max(42,WorkspaceAppearance.Font.Height+22)));
            layout.Controls.Add(new Label{Dock=DockStyle.Fill,Text="Choose native part variants for this source preview. The initial first-variant view is an inspection choice. Game-script visibility is not simulated.",AutoSize=false},0,0);
            int rowHeight=Math.Max(34,WorkspaceAppearance.Font.Height+12);
            int rowCount=Enumerable.Range(0,parts.GroupCount).Count(i=>parts.VariantCount(i)>0);
            var rows=new TableLayoutPanel{Dock=DockStyle.Top,Height=rowHeight*rowCount,ColumnCount=2,RowCount=rowCount};rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));
            int nextRow=0;
            for(int group=0;group<parts.GroupCount;group++)
            {
                int index=group;if(parts.VariantCount(group)==0)continue;
                int row=nextRow++;rows.RowStyles.Add(new RowStyle(SizeType.Absolute,Math.Max(34,WorkspaceAppearance.Font.Height+12)));
                rows.Controls.Add(new Label{Text="Part group "+(group+1),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row);
                var choice=new ComboBox{Name="nativePartGroup"+group,Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,TabIndex=group};choice.Items.Add("Hidden");
                for(int variant=0;variant<parts.VariantCount(group);variant++)choice.Items.Add("Variant "+(variant+1));choice.SelectedIndex=parts.Choice(group)+1;
                choice.SelectedIndexChanged+=(s,e)=>{parts.Select(index,choice.SelectedIndex-1);renderer.RefreshParts();_libraryViewport.Invalidate();};rows.Controls.Add(choice,1,row);
            }
            var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};scroll.Controls.Add(rows);layout.Controls.Add(scroll,0,1);var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};
            var cancel=new Button{Text="Cancel",Name="cancelNativeParts",DialogResult=DialogResult.Cancel,AutoSize=true};var keep=new Button{Text="Keep preview",Name="keepNativeParts",DialogResult=DialogResult.OK,AutoSize=true};
            actions.Controls.Add(cancel);actions.Controls.Add(keep);layout.Controls.Add(actions,0,2);dialog.Controls.Add(layout);dialog.AcceptButton=keep;dialog.CancelButton=cancel;return dialog;
        }
    }
}
