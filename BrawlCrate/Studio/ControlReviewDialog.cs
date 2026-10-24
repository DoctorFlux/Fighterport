using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    internal sealed class ControlReviewDialog:Form
    {
        private readonly ControlReply _loaded;
        private readonly int[] _parents;
        private readonly ComboBox _role=new ComboBox{Name="reviewIkRole",Dock=DockStyle.Fill};
        private readonly ComboBox[] _joints=Enumerable.Range(0,3).Select(i=>new ComboBox{Name="reviewIkJoint"+i,Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList}).ToArray();
        private readonly NumericUpDown[] _pole=Enumerable.Range(0,3).Select(i=>new NumericUpDown{Name="reviewIkPole"+i,DecimalPlaces=4,Minimum=-1,Maximum=1,Increment=.05m,Width=120}).ToArray();
        private readonly CheckBox _applyChain=new CheckBox{Name="reviewApplyIk",Text="Save this IK chain",AutoSize=true};
        private readonly DataGridView _roles=new DataGridView{Name="reviewSharedBones",Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
        private readonly HashSet<string> _changedRoles=new HashSet<string>(StringComparer.Ordinal);
        private readonly Label _message=new Label{Dock=DockStyle.Bottom,Height=45,Padding=new Padding(8),AutoEllipsis=true};
        private bool _sync;
        private sealed class Choice {public int Index{get;set;}public string Name{get;set;}public override string ToString()=>Name;}
        internal object Review {get;private set;}
        internal ControlReviewDialog(MDL0Node model,ControlReply loaded)
        {
            _loaded=loaded;_parents=loaded.BoneNames.Select(n=>Array.IndexOf(loaded.BoneNames,model._linker.BoneCache.Single(b=>b.Name==n).Parent?.Name)).ToArray();
            Text="Review Animation Controls";ClientSize=new Size(650,560);MinimumSize=new Size(540,430);ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
            var note=new Label{Dock=DockStyle.Top,Height=55,Padding=new Padding(10),Text="Choose existing joints and shared bones. The model keeps its skeleton, bind data and weights.\nAmbiguous limbs stay unassigned until reviewed."};
            var tabs=new ReviewTabs{Dock=DockStyle.Fill};var limbs=new TabPage("IK chains");var body=new TabPage("Shared bones");tabs.TabPages.AddRange(new[]{limbs,body});
            var grid=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(12)};grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            AddField(grid,"IK role",_role);for(int i=0;i<3;i++)AddField(grid,new[]{"Upper joint","Elbow / knee","Hand / ankle"}[i],_joints[i]);
            var xyz=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=false};xyz.Controls.AddRange(_pole);AddField(grid,"Straight-limb pole",xyz);AddField(grid,"",_applyChain);
            var explanation=new Label{Dock=DockStyle.Top,Height=70,Padding=new Padding(12),Text="The pole is a direction in the model's bind coordinates. Choose the side that the elbow or knee should bend toward when the limb is straight.\nIK requires direct parent-child joints; other existing bones remain available for FK."};limbs.Controls.Add(explanation);limbs.Controls.Add(grid);
            _role.Items.AddRange(new[]{"Hand.L","Hand.R","Foot.L","Foot.R"}.Concat((loaded.Chains??new ControlChain[0]).Select(c=>c.Role)).Distinct(StringComparer.Ordinal).Cast<object>().ToArray());
            _joints[0].Items.AddRange(Enumerable.Range(0,loaded.BoneNames.Length).Select(ChoiceFor).Cast<object>().ToArray());
            _role.SelectedIndexChanged+=(s,e)=>SelectRole();_joints[0].SelectedIndexChanged+=(s,e)=>{if(!_sync){Populate(1);_applyChain.Checked=true;}};_joints[1].SelectedIndexChanged+=(s,e)=>{if(!_sync){Populate(2);_applyChain.Checked=true;}};_joints[2].SelectedIndexChanged+=(s,e)=>{if(!_sync)_applyChain.Checked=true;};foreach(var p in _pole)p.ValueChanged+=(s,e)=>{if(!_sync)_applyChain.Checked=true;};
            _roles.Columns.Add(new DataGridViewTextBoxColumn{Name="Role",HeaderText="Shared role",ReadOnly=true});
            var choices=new[]{new Choice{Index=-1,Name="Unassigned"}}.Concat(Enumerable.Range(0,loaded.BoneNames.Length).Select(ChoiceFor)).ToList();
            _roles.Columns.Add(new DataGridViewComboBoxColumn{Name="Bone",HeaderText="Existing bone",DataSource=choices,DisplayMember="Name",ValueMember="Index",ValueType=typeof(int),FlatStyle=FlatStyle.Flat});
            _roles.Columns.Add(new DataGridViewTextBoxColumn{Name="Evidence",HeaderText="Evidence",ReadOnly=true});
            _roles.Columns.Add(new DataGridViewCheckBoxColumn{Name="Confirm",HeaderText="Confirm",FillWeight=45});
            _sync=true;foreach(string name in new[]{"Hips","Spine","Chest","Clavicle.L","Clavicle.R","Neck","Head"}.Concat((loaded.Roles??new ControlRole[0]).Select(r=>r.Role)).Distinct(StringComparer.Ordinal))AddRole(name);_sync=false;
            _roles.CurrentCellDirtyStateChanged+=(s,e)=>{if(_roles.IsCurrentCellDirty)_roles.CommitEdit(DataGridViewDataErrorContexts.Commit);};
            _roles.CellValueChanged+=(s,e)=>{if(!_sync&&e.RowIndex>=0&&(e.ColumnIndex==1||e.ColumnIndex==3)){var row=_roles.Rows[e.RowIndex];string name=(string)row.Cells[0].Value;if(e.ColumnIndex==1)row.Cells[3].Value=true;if(Equals(row.Cells[3].Value,true)){_changedRoles.Add(name);row.Cells[2].Value="Reviewed selection";}else _changedRoles.Remove(name);}};
            var extra=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new Padding(6)};var nameBox=new TextBox{Name="reviewNewRole",Width=280,MaxLength=128};var add=new Button{Name="reviewAddRole",Text="Add shared role",AutoSize=true};add.Click+=(s,e)=>{string name=nameBox.Text.Trim();if(name.Length==0)return;if(_roles.Rows.Cast<DataGridViewRow>().Any(r=>(string)r.Cells[0].Value==name)){_message.Text="That shared role is already listed.";return;}AddRole(name);_roles.Rows[_roles.RowCount-1].Cells[3].Value=true;nameBox.Clear();};extra.Controls.AddRange(new Control[]{nameBox,add});body.Controls.Add(_roles);body.Controls.Add(extra);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel,AutoSize=true};var save=new Button{Name="reviewSave",Text="Save Reviewed Controls",AutoSize=true};save.Click+=(s,e)=>SaveReview();actions.Controls.AddRange(new Control[]{cancel,save});CancelButton=cancel;AcceptButton=save;
            Controls.Add(tabs);Controls.Add(_message);Controls.Add(actions);Controls.Add(note);tabs.BringToFront();_role.SelectedIndex=0;DarkTheme.Apply(this);
        }
        private Choice ChoiceFor(int i)=>new Choice{Index=i,Name=i+": "+_loaded.BoneNames[i]};
        private static void AddField(TableLayoutPanel grid,string text,Control control){int row=grid.RowCount++;grid.RowStyles.Add(new RowStyle(SizeType.Absolute,38));grid.Controls.Add(new Label{Text=text,AutoSize=true,Anchor=AnchorStyles.Left},0,row);grid.Controls.Add(control,1,row);}
        private int Joint(int i)=>(_joints[i].SelectedItem as Choice)?.Index??-1;
        private void Populate(int index,int preferred=-1)
        {
            bool sync=_sync;_sync=true;try{_joints[index].Items.Clear();int parent=Joint(index-1);_joints[index].Items.AddRange(Enumerable.Range(0,_parents.Length).Where(i=>parent>=0&&_parents[i]==parent).Select(ChoiceFor).Cast<object>().ToArray());_joints[index].SelectedIndex=-1;for(int i=0;i<_joints[index].Items.Count;i++)if(((Choice)_joints[index].Items[i]).Index==preferred)_joints[index].SelectedIndex=i;if(_joints[index].SelectedIndex<0&&_joints[index].Items.Count>0)_joints[index].SelectedIndex=0;if(index==1)Populate(2);}finally{_sync=sync;}
        }
        private void SelectRole()
        {
            _sync=true;try{var chain=(_loaded.Chains??new ControlChain[0]).FirstOrDefault(c=>c.Role==_role.Text);_joints[0].SelectedIndex=chain?.Root??-1;Populate(1,chain?.Middle??-1);Populate(2,chain?.Effector??-1);for(int i=0;i<3;i++)_pole[i].Value=(decimal)(chain?.Pole?[i]??(i==2?1:0));_applyChain.Checked=false;_message.Text=chain==null?"Choose only a limb you can identify.":(chain.Reviewed?"Reviewed · ":"Suggested · ")+chain.Evidence;}finally{_sync=false;}
        }
        private void AddRole(string name){var role=(_loaded.Roles??new ControlRole[0]).FirstOrDefault(r=>r.Role==name);_roles.Rows.Add(name,role?.Bone??-1,role==null?"Unassigned":(role.Reviewed?"Reviewed · ":"Suggested · ")+role.Evidence,false);}
        private void SaveReview()
        {
            _roles.EndEdit();if(!_applyChain.Checked&&_changedRoles.Count==0){_message.Text="Choose a chain or shared bone to review.";return;}
            var joints=Enumerable.Range(0,3).Select(Joint).ToArray();var pole=_pole.Select(p=>(float)p.Value).ToArray();
            if(_applyChain.Checked&&(_role.Text.Trim().Length==0||joints.Any(i=>i<0)||pole.All(v=>v==0))){_message.Text="Choose three existing joints and a nonzero pole direction.";return;}
            var roles=_roles.Rows.Cast<DataGridViewRow>().Where(r=>_changedRoles.Contains((string)r.Cells[0].Value)).ToDictionary(r=>(string)r.Cells[0].Value,r=>(int)r.Cells[1].Value<0?(int?)null:(int)r.Cells[1].Value);
            Review=new{Role=_applyChain.Checked?_role.Text.Trim():null,Bones=joints,Pole=pole,Roles=roles,Evidence="Explicit user review of existing native joints and bind-coordinate pole"};DialogResult=DialogResult.OK;Close();
        }
        private sealed class ReviewTabs:TabControl
        {
            protected override void WndProc(ref Message message)
            {
                base.WndProc(ref message);
                if(IsDisposed||Disposing||TabCount==0||(message.Msg!=0x000f&&message.Msg!=0x0317&&message.Msg!=0x0318)||(message.Msg!=0x000f&&message.WParam==IntPtr.Zero))return;
                // Native owner drawing covers tabs, but the unused header strip
                // still follows the system light color. Preserve native hit tests
                // and paint just that unused area, including native print captures.
                var last=GetTabRect(TabCount-1);var blank=new Rectangle(last.Right+1,0,Math.Max(0,ClientSize.Width-last.Right-1),Math.Max(0,DisplayRectangle.Top-1));
                using(var graphics=message.Msg==0x000f?Graphics.FromHwnd(Handle):Graphics.FromHdc(message.WParam))
                using(var brush=new SolidBrush(DarkTheme.Surface))graphics.FillRectangle(brush,blank);
            }
        }
    }
}
