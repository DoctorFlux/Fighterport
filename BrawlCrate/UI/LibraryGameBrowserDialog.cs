using BrawlCrate.Studio;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal sealed class LibraryGameBrowserDialog:Form
    {
        private readonly CheckedListBox characters=new CheckedListBox();
        internal LibraryGameBrowserDialog(LibraryGameRequest request,string[] entries)
        {
            Text="Browse Game Source";Name="LibraryGameBrowserDialog";ClientSize=new Size(540,455);StartPosition=FormStartPosition.CenterParent;
            Controls.Add(new Label{Text=request.Game+" · "+request.GameVersion+Environment.NewLine+"Choose characters. Every native motion resource in their packages will be indexed; only the selected character folders are cached from a disc.",Bounds=new Rectangle(14,12,512,64)});
            characters.Name="libraryGameCharacters";characters.CheckOnClick=true;characters.Bounds=new Rectangle(14,82,512,255);characters.Items.AddRange(entries);Controls.Add(characters);
            var all=new Button{Name="libraryGameAll",Text="Select All",Bounds=new Rectangle(14,345,100,28)};
            all.Click+=(s,e)=>{for(int i=0;i<characters.Items.Count;i++)characters.SetItemChecked(i,true);};
            var none=new Button{Text="Clear",Bounds=new Rectangle(122,345,100,28)};none.Click+=(s,e)=>{for(int i=0;i<characters.Items.Count;i++)characters.SetItemChecked(i,false);};
            var confirm=new CheckBox{Name="libraryGameApprove",Text="I confirm this original game/build and its native character folders.",Bounds=new Rectangle(14,380,512,26)};
            var add=new Button{Name="libraryGameImport",Text="Add Selected Characters",Bounds=new Rectangle(235,416,185,28)};
            add.Click+=(s,e)=>{
                if(characters.CheckedItems.Count==0||!confirm.Checked){MessageBox.Show(this,"Select characters and confirm the original game/build first.");return;}
                request.Characters=characters.CheckedItems.Cast<string>().ToArray();request.ApproveBuild=true;DialogResult=DialogResult.OK;
            };
            var close=new Button{Text="Cancel",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(428,416,98,28)};
            Controls.AddRange(new Control[]{all,none,confirm,add,close});CancelButton=close;
        }
    }
}
