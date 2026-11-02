using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    internal sealed class NativeActionDestinationDialog:Form
    {
        readonly NativeAnimationMember[] actions;
        readonly ComboBox choice;
        internal NativeAnimationMember SelectedAction=>choice.SelectedIndex<0?null:actions[choice.SelectedIndex];

        internal NativeActionDestinationDialog(string clipName,NativeAnimationMember[] members)
        {
            if(members==null||members.Length==0)throw new ArgumentException("No owned native actions are available.",nameof(members));
            actions=members.OrderBy(m=>m.Name,StringComparer.Ordinal).ThenBy(m=>m.MotionKey,StringComparer.Ordinal).ToArray();
            Name="nativeActionDestination";Text="Choose native destination action";StartPosition=FormStartPosition.CenterParent;
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
            ClientSize=new Size(470,150);AutoScaleMode=AutoScaleMode.Font;
            var label=new Label{Left=12,Top=12,Width=446,Height=38,Text="Export "+clipName+" into a new native action file. Choose the action slot to replace in that copy:"};
            choice=new ComboBox{Name="nativeActionChoice",Left=12,Top=55,Width=446,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Exact native destination action"};
            choice.Items.AddRange(actions.Select(m=>(object)(m.Name+"  ["+m.MotionKey+"]")).ToArray());
            var export=new Button{Name="nativeActionConfirm",Text="Export copy",Left=254,Top=105,Width=100,DialogResult=DialogResult.OK};
            var cancel=new Button{Name="nativeActionCancel",Text="Cancel",Left=363,Top=105,Width=95,DialogResult=DialogResult.Cancel};
            export.Enabled=false;choice.SelectedIndexChanged+=(sender,args)=>export.Enabled=choice.SelectedIndex>=0;
            Controls.AddRange(new Control[]{label,choice,export,cancel});AcceptButton=export;CancelButton=cancel;
        }
    }
}
