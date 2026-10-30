using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    internal sealed class ReviewedContactsDialog:Form
    {
        internal readonly FittingModelView SourceView,TargetView;
        internal Func<float,Task<ControlReply>> Suggest;
        internal Func<string,ReviewedContactEdit[],int?,Task<CHR0Node>> Prepare;
        internal Action<CHR0Node> Apply;
        internal Action CancelWork;
        internal CHR0Node Candidate {get;private set;}
        internal bool Busy {get;private set;}
        internal string PreparedReport;
        readonly CHR0Node source;
        readonly NumericUpDown floor=new NumericUpDown{Name="reviewedContactFloor",Minimum=-100000,Maximum=100000,DecimalPlaces=4,Increment=.01m,Width=110};
        readonly CheckBox plane=new CheckBox{Name="reviewedContactPlane",Text="I reviewed this ground height",AutoSize=true};
        readonly Button suggest=new Button{Name="reviewedContactSuggest",Text="Suggest",AutoSize=true};
        readonly Button preview=new Button{Name="reviewedContactPreview",Text="Preview",AutoSize=true};
        readonly Button apply=new Button{Name="reviewedContactApply",Text="Apply",AutoSize=true};
        readonly Button cancel=new Button{Name="reviewedContactCancel",Text="Cancel",AutoSize=true};
        readonly Button close=new Button{Name="reviewedContactClose",Text="Close",AutoSize=true};
        readonly Button advanced=new Button{Name="reviewedContactAdvanced",Text="Advanced",AutoSize=true};
        readonly CheckBox earlier=new CheckBox{Name="reviewedContactEarlier",Text="I reviewed earlier body correction",AutoSize=true,Visible=false};
        readonly NumericUpDown correctionFirst=new NumericUpDown{Name="reviewedContactCorrectionFirst",Minimum=1,Value=1,Width=75,Visible=false};
        readonly Label correctionLabel=new Label{Text="Begin at frame",AutoSize=true,Visible=false,Margin=new Padding(3,8,3,0)};
        internal int? EarlierCorrectionStart=>earlier.Checked?(int)correctionFirst.Value-1:(int?)null;
        readonly Button add=new Button{Name="reviewedContactAdd",Text="Add range",AutoSize=true};
        readonly Button remove=new Button{Name="reviewedContactRemove",Text="Remove range",AutoSize=true};
        readonly ComboBox limb=new ComboBox{Name="reviewedContactLimb",DropDownStyle=ComboBoxStyle.DropDownList,Width=110};
        readonly DataGridView rows=new DataGridView{Name="reviewedContactRanges",Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
        readonly TextBox report=new TextBox{Name="reviewedContactReport",Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
        readonly TrackBar frame=new TrackBar{Name="reviewedContactFrame",Dock=DockStyle.Fill,Minimum=1,TickStyle=TickStyle.None};
        readonly FlowLayoutPanel footer=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};
        readonly ToolTip hints=new ToolTip();
        readonly Dictionary<Control,Control> focus=new Dictionary<Control,Control>();
        string context,fullReport;int generation;bool loading,details;
        string[] originalRows=new string[0];

        internal ReviewedContactsDialog(ResourceNode originalRoot,MDL0Node originalModel,ResourceNode resultRoot,MDL0Node resultModel,
            CHR0Node snapshot,string[] effectors,ModelPanelViewport settings,IEnumerable<ResourceNode> references)
        {
            Name="reviewedContactsDialog";Text="Fix foot contacts";AutoScaleMode=AutoScaleMode.None;Font=WorkspaceAppearance.Font;
            ClientSize=new Size(1080,790);MinimumSize=new Size(850,600);ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
            source=snapshot;frame.Maximum=snapshot.FrameCount;correctionFirst.Maximum=snapshot.FrameCount;
            SourceView=new FittingModelView(originalModel,originalRoot,"Current animation · read-only",true,settings,references){Dock=DockStyle.Fill};
            TargetView=new FittingModelView(resultModel,resultRoot,"Reviewed result",false,settings,references){Dock=DockStyle.Fill};
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(8)};
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,54));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,29));layout.RowStyles.Add(new RowStyle(SizeType.Percent,17));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var surface=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};
            surface.Controls.AddRange(new Control[]{new Label{Text="Ground height",AutoSize=true,Margin=new Padding(3,8,3,0)},floor,plane,limb});
            limb.Items.AddRange(effectors);if(limb.Items.Count>0)limb.SelectedIndex=0;
            surface.Controls.AddRange(new Control[]{add,remove,advanced,earlier,correctionLabel,correctionFirst});layout.Controls.Add(surface,0,0);
            var views=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            views.Controls.Add(SourceView,0,0);views.Controls.Add(TargetView,1,0);layout.Controls.Add(views,0,1);layout.Controls.Add(frame,0,2);
            rows.Columns.Add(new DataGridViewCheckBoxColumn{Name="Reviewed",HeaderText="Review",FillWeight=55});
            foreach(string column in new[]{"Limb","First","Last","Blend","Anchor","X","Y","Z"})rows.Columns.Add(new DataGridViewTextBoxColumn{Name=column,HeaderText=column,FillWeight=column=="Limb"?110:65,ReadOnly=column=="Limb",Visible=column!="X"&&column!="Y"&&column!="Z"});
            layout.Controls.Add(rows,0,3);layout.Controls.Add(report,0,4);footer.Controls.AddRange(new Control[]{suggest,preview,apply,cancel,close});layout.Controls.Add(footer,0,5);Controls.Add(layout);
            hints.SetToolTip(suggest,"Analyze the frozen current animation offline. Suggested ranges still need review. F6 changes region.");
            hints.SetToolTip(preview,"Prepare and freshly verify an independent native result. The current animation stays unchanged.");
            hints.SetToolTip(apply,"Apply the reviewed result to this animation with one native Undo command.");
            hints.SetToolTip(rows,"Frames are native display frames. Check Review only after checking limb, range and anchor. Tab moves between cells.");
            hints.SetToolTip(earlier,"Review changes to permitted hips/legs before contact, including inferred support dependencies. The chosen first pose, roots and authored pins stay exact.");earlier.AccessibleDescription=hints.GetToolTip(earlier);
            foreach(Control action in new Control[]{suggest,preview,apply,add,remove,close})action.AccessibleDescription=hints.GetToolTip(action);
            suggest.Click+=async(s,e)=>await SuggestNow();preview.Click+=async(s,e)=>await PrepareNow();
            apply.Click+=(s,e)=>{try{if(!Busy&&Candidate!=null){Apply(Candidate);Close();}}catch(Exception error){InvalidateResult();report.Text=error.Message;}};
            cancel.Click+=(s,e)=>{generation++;CancelWork?.Invoke();SetBusy(false);InvalidateResult();report.Text="Cancelled; the animation is unchanged.";};
            close.Click+=(s,e)=>Close();CancelButton=close;
            floor.ValueChanged+=(s,e)=>ResetContext();plane.CheckedChanged+=(s,e)=>ResetContext();
            rows.CurrentCellDirtyStateChanged+=(s,e)=>{if(rows.IsCurrentCellDirty)rows.CommitEdit(DataGridViewDataErrorContexts.Commit);};
            rows.CellValueChanged+=(s,e)=>{if(!loading){if(e.RowIndex>=0&&e.ColumnIndex>=0&&rows.Columns[e.ColumnIndex].Name!="Reviewed"){loading=true;try{rows.Rows[e.RowIndex].Cells["Reviewed"].Value=false;}finally{loading=false;}}generation++;CancelWork?.Invoke();SetBusy(false);InvalidateResult();UpdateActions();}};
            rows.DataError+=(s,e)=>{e.ThrowException=false;report.Text="Use finite native frame numbers and offsets.";InvalidateResult();};
            add.Click+=(s,e)=>{if(context==null||limb.SelectedItem==null)return;loading=true;try{rows.Rows.Add(false,limb.SelectedItem,5,Math.Max(5,source.FrameCount-4),2,5,0,0,0);}finally{loading=false;}InvalidateResult();UpdateActions();};
            remove.Click+=(s,e)=>{if(rows.SelectedRows.Count>0){rows.Rows.Remove(rows.SelectedRows[0]);InvalidateResult();UpdateActions();}};
            advanced.Click+=(s,e)=>{details=!details;foreach(string axis in new[]{"X","Y","Z"})rows.Columns[axis].Visible=details;earlier.Visible=correctionLabel.Visible=correctionFirst.Visible=details;advanced.Text=details?"Less":"Advanced";if(fullReport!=null)report.Text=details?fullReport:fullReport.Split(new[]{"\r\n"},StringSplitOptions.None)[0];};
            earlier.CheckedChanged+=(s,e)=>{if(loading)return;generation++;CancelWork?.Invoke();SetBusy(false);InvalidateResult();};
            correctionFirst.ValueChanged+=(s,e)=>{if(loading)return;earlier.Checked=false;generation++;CancelWork?.Invoke();SetBusy(false);InvalidateResult();};
            frame.ValueChanged+=(s,e)=>{SourceView.Pose(source,frame.Value);TargetView.Pose(Candidate??source,frame.Value);};
            FormClosing+=(s,e)=>{generation++;CancelWork?.Invoke();};FormClosed+=(s,e)=>{InvalidateResult();hints.Dispose();};
            Shown+=(s,e)=>{SourceView.Pose(source,1);TargetView.Pose(source,1);SourceView.ShownFrame();TargetView.ShownFrame();plane.Focus();};
            report.Text="Review the ground plane, suggest ranges, then check the contacts you want to correct. Uncertain ranges can be added manually.";
            DarkTheme.Apply(this);UpdateActions();
        }
        async Task SuggestNow()
        {
            if(Busy||!plane.Checked)return;int epoch=++generation;InvalidateResult();SetBusy(true);report.Text="Reading full native foot geometry…";
            var surface=new SceneFittingOptions{Scene=1,SourceFloor=(float)floor.Value,TargetFloor=(float)floor.Value};SourceView.Options=TargetView.Options=surface;
            try
            {
                var reply=await Suggest((float)floor.Value);if(IsDisposed||epoch!=generation)return;
                context=reply.ContactContextJson;originalRows=(reply.ContactRanges??new ReviewedContactRow[0]).Select(r=>r.Id).ToArray();loading=true;try{rows.Rows.Clear();foreach(var row in reply.ContactRanges??new ReviewedContactRow[0])
                    {int i=rows.Rows.Add(false,row.Role,row.Start+1,row.End+1,row.Blend,row.AnchorFrame+1,0,0,0);rows.Rows[i].Tag=row.Id;}}
                finally{loading=false;}
                report.Text=(rows.Rows.Count==0?"No confident stationary ranges. Add and review a range manually.":"Suggestions are unapproved. Review each range before Preview.")+"\r\n"+string.Join("\r\n",reply.Diagnostics??new string[0]);
            }
            catch(Exception error){if(!IsDisposed&&epoch==generation)report.Text=error.Message;}
            finally{if(!IsDisposed&&epoch==generation)SetBusy(false);}
        }
        async Task PrepareNow()
        {
            if(Busy||context==null)return;rows.EndEdit();int epoch=++generation;InvalidateResult();SetBusy(true);report.Text="Preparing and verifying contact curves…";
            try{var candidate=await Prepare(context,ReadEdits(),EarlierCorrectionStart);if(IsDisposed||epoch!=generation){candidate?.Dispose();return;}Candidate=candidate;TargetView.Pose(candidate,frame.Value);TargetView.ShownFrame();fullReport=PreparedReport;report.Text=details?fullReport:fullReport.Split(new[]{"\r\n"},StringSplitOptions.None)[0];}
            catch(Exception error){if(!IsDisposed&&epoch==generation)report.Text=error.Message;}
            finally{if(!IsDisposed&&epoch==generation)SetBusy(false);}
        }
        internal ReviewedContactEdit[] ReadEdits()
        {
            int Number(DataGridViewRow row,string column)=>int.Parse(Convert.ToString(row.Cells[column].Value,CultureInfo.InvariantCulture),CultureInfo.InvariantCulture);
            var edits=rows.Rows.Cast<DataGridViewRow>().Select(row=>new ReviewedContactEdit{Id=row.Tag as string,Role=Convert.ToString(row.Cells["Limb"].Value),Start=Number(row,"First")-1,End=Number(row,"Last")-1,Blend=Number(row,"Blend"),AnchorFrame=Number(row,"Anchor")-1,
                Offset=new[]{"X","Y","Z"}.Select(axis=>float.Parse(Convert.ToString(row.Cells[axis].Value,CultureInfo.InvariantCulture),CultureInfo.InvariantCulture)).ToArray(),Reviewed=Convert.ToBoolean(row.Cells["Reviewed"].Value)}).ToArray();
            return edits.Concat(originalRows.Where(id=>!edits.Any(e=>e.Id==id)).Select(id=>new ReviewedContactEdit{Id=id,Remove=true})).ToArray();
        }
        void ResetContext(){generation++;CancelWork?.Invoke();SetBusy(false);context=null;loading=true;try{rows.Rows.Clear();earlier.Checked=false;}finally{loading=false;}InvalidateResult();UpdateActions();}
        void SetBusy(bool busy){Busy=busy;floor.Enabled=plane.Enabled=rows.Enabled=limb.Enabled=advanced.Enabled=add.Enabled=remove.Enabled=earlier.Enabled=correctionFirst.Enabled=!busy;cancel.Enabled=busy;UpdateActions();}
        void UpdateActions(){suggest.Enabled=!Busy&&plane.Checked;preview.Enabled=!Busy&&context!=null&&rows.Rows.Cast<DataGridViewRow>().Any(r=>Convert.ToBoolean(r.Cells["Reviewed"].Value));apply.Enabled=!Busy&&Candidate!=null;}
        void InvalidateResult(){TargetView.Pose(source,frame.Value);Candidate?.Dispose();Candidate=null;UpdateActions();}
        protected override bool ProcessCmdKey(ref Message message,Keys keys)
        {
            if(keys==Keys.F6||keys==(Keys.Shift|Keys.F6))
            {
                Control[] regions={floor.Parent,SourceView.Viewport,TargetView.Viewport,rows,report,footer};int at=Array.FindIndex(regions,c=>c.ContainsFocus);
                if(at>=0)focus[regions[at]]=FocusedChild(regions[at])??regions[at];int direction=keys==Keys.F6?1:-1;
                for(int step=1;step<=regions.Length;step++){var next=regions[(Math.Max(0,at)+direction*step+regions.Length*2)%regions.Length];if(!next.Visible||!next.Enabled)continue;
                    if(focus.TryGetValue(next,out var prior)&&prior.CanFocus&&prior.Visible&&prior.Enabled)prior.Focus();else if(!next.Focus())next.SelectNextControl(null,true,true,true,false);return true;}
                return true;
            }
            return base.ProcessCmdKey(ref message,keys);
        }
        static Control FocusedChild(Control root){foreach(Control child in root.Controls){if(child.Focused)return child;var nested=FocusedChild(child);if(nested!=null)return nested;}return null;}
    }
}
