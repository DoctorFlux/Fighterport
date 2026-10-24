using BrawlCrate.Studio;
using System.Drawing;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private void CreateControlAnimationFromUi()
        {
            using(var dialog=new Form{Text="Create Animation From Pose",ClientSize=new Size(390,165),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false})
            {
                var grid=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=2,RowCount=4};grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
                var name=new TextBox{Text="NewAnimation",Dock=DockStyle.Fill,MaxLength=120};var count=new NumericUpDown{Minimum=1,Maximum=65535,Value=60,Dock=DockStyle.Fill};
                grid.Controls.Add(new Label{Text="Animation name",AutoSize=true},0,0);grid.Controls.Add(name,1,0);grid.Controls.Add(new Label{Text="Frames",AutoSize=true},0,1);grid.Controls.Add(count,1,1);
                var note=new Label{Text="Native CHR0 uses 60 frames per second.\nThe current pose and controls are retained.",AutoSize=true};grid.Controls.Add(note,0,2);grid.SetColumnSpan(note,2);
                var create=new Button{Text="Create",DialogResult=DialogResult.OK,AutoSize=true};grid.Controls.Add(create,1,3);dialog.Controls.Add(grid);dialog.AcceptButton=create;DarkTheme.Apply(dialog);
                if(dialog.ShowDialog(this)==DialogResult.OK)ControlAction(()=>CreateNamedControlAnimation(name.Text,(int)count.Value));
            }
        }
    }
}
