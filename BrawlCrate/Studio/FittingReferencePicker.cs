using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    internal sealed class FittingLibraryItem
    {
        internal AnimationLibrary Library;internal LibrarySearchItem Item;
        public override string ToString()=>Item.Clip.Name+" · "+Item.Clip.SourceCharacter+" · "+Item.Motion.Game+" · "+Item.Motion.GameVersion+" ["+Item.Clip.Id.Substring(0,8)+"]";
    }
    internal sealed class FittingReferencePicker:Form
    {
        readonly FittingLibraryItem[] all;FittingLibraryItem[] found;int page;
        readonly TextBox search=new TextBox{Name="fittingReferenceSearch",Dock=DockStyle.Fill};
        readonly ListBox list=new ListBox{Name="fittingReferenceList",Dock=DockStyle.Fill,HorizontalScrollbar=true,IntegralHeight=false};
        readonly Label count=new Label{AutoSize=true};
        internal FittingLibraryItem Selection=>list.SelectedItem as FittingLibraryItem;
        internal FittingReferencePicker(FittingLibraryItem[] rows)
        {
            all=rows;Text="Choose this model’s reference animation";ClientSize=new Size(940,560);MinimumSize=new Size(640,400);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,Padding=new Padding(10)};
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label{Text="Verified native animations for the current model. Choose one reference pose; frame 1 is used by default.",AutoSize=true},0,0);layout.Controls.Add(search,0,1);layout.Controls.Add(list,0,2);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var previous=new Button{Text="Previous",AutoSize=true};var next=new Button{Text="Next",AutoSize=true};
            var use=new Button{Name="fittingReferenceUse",Text="Use reference",AutoSize=true,Enabled=false};var close=new Button{Text="Cancel",AutoSize=true,DialogResult=DialogResult.Cancel};
            actions.Controls.AddRange(new Control[]{count,previous,next,use,close});layout.Controls.Add(actions,0,3);Controls.Add(layout);
            void ShowPage(){list.BeginUpdate();try{list.Items.Clear();list.Items.AddRange(found.Skip(page*200).Take(200).ToArray());}finally{list.EndUpdate();}count.Text=found.Length+" matches · page "+(page+1);previous.Enabled=page>0;next.Enabled=(page+1)*200<found.Length;use.Enabled=false;}
            search.TextChanged+=(s,e)=>{var words=LibraryQuery.Words(search.Text);found=all.Where(i=>LibraryQuery.WordsMatch(i.ToString(),words)).ToArray();page=0;ShowPage();};
            previous.Click+=(s,e)=>{page--;ShowPage();};next.Click+=(s,e)=>{page++;ShowPage();};list.SelectedIndexChanged+=(s,e)=>use.Enabled=Selection!=null;
            use.Click+=(s,e)=>{if(Selection!=null){DialogResult=DialogResult.OK;Close();}};list.DoubleClick+=(s,e)=>use.PerformClick();AcceptButton=use;CancelButton=close;
            found=all;ShowPage();DarkTheme.Apply(this);WorkspaceAppearance.SizeDialog(this);Shown+=(s,e)=>search.Focus();
        }
    }
}
