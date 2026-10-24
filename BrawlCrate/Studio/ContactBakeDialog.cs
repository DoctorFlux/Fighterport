using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    internal sealed class ContactBakeDialog:Form
    {
        internal readonly FittingModelView SourceView,TargetView;
        internal Func<NativeContactOptions,Task<CHR0Node>> Prepare;
        internal Action<NativeContactOptions,CHR0Node> Apply;
        internal Action CancelWork;
        internal string PreparedReport;
        internal bool Busy {get;private set;}internal CHR0Node Candidate {get;private set;}
        readonly Dictionary<string,NumericUpDown> values=new Dictionary<string,NumericUpDown>();readonly ComboBox action=new ComboBox{Name="contactAction",Width=306,DropDownStyle=ComboBoxStyle.DropDownList},bone=new ComboBox{Name="contactBone",Width=306,DropDownStyle=ComboBoxStyle.DropDownList};
        readonly CheckBox orientation=new CheckBox{Name="contactOrientation",Text="Hold captured world orientation",AutoSize=true,Checked=true},review=new CheckBox{Name="contactReviewed",Text="I reviewed this result and its limits",AutoSize=true};
        readonly Button prepare=new Button{Name="prepareContact",Text="Prepare Contact Edit",AutoSize=true},add=new Button{Name="addContact",Text="Add Independent Animation",AutoSize=true,Enabled=false},cancel=new Button{Name="cancelContact",Text="Cancel Work",AutoSize=true,Enabled=false};
        readonly TextBox report=new TextBox{Name="contactReport",ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill};readonly TrackBar frame;readonly CHR0Node source;int generation;bool loading=true;
        internal ContactBakeDialog(ResourceNode a,MDL0Node am,ResourceNode b,MDL0Node bm,CHR0Node clip,string[] effectors,ModelPanelViewport settings,IEnumerable<ResourceNode> references)
        {
            source=clip;Text="Contact Placement / Release";ClientSize=new Size(1240,820);MinimumSize=new Size(1060,700);ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;
            SourceView=new FittingModelView(am,a,"SOURCE · current animation",true,settings,references){Dock=DockStyle.Fill};TargetView=new FittingModelView(bm,b,"RESULT · prepare and review",false,settings,references){Dock=DockStyle.Fill};
            var views=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));views.Controls.Add(SourceView,0,0);views.Controls.Add(TargetView,1,0);
            frame=new TrackBar{Name="contactFrame",Minimum=1,Maximum=clip.FrameCount,Value=1,Dock=DockStyle.Fill,TickStyle=TickStyle.None};var right=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3};right.RowStyles.Add(new RowStyle(SizeType.Percent,70));right.RowStyles.Add(new RowStyle(SizeType.Absolute,38));right.RowStyles.Add(new RowStyle(SizeType.Percent,30));right.Controls.Add(views,0,0);right.Controls.Add(frame,0,1);right.Controls.Add(report,0,2);
            var fields=new FlowLayoutPanel{Dock=DockStyle.Left,Width=344,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(8)};
            void Label(string text)=>fields.Controls.Add(new Label{Text=text,AutoSize=true,Width=306,MaximumSize=new Size(306,0),Margin=new Padding(0,8,0,4)});
            void Number(string key,string text,decimal minimum,decimal maximum,decimal initial,int decimals=0){Label(text);var n=new NumericUpDown{Name="contact_"+key,Width=306,Minimum=minimum,Maximum=maximum,DecimalPlaces=decimals,Value=Math.Max(minimum,Math.Min(maximum,initial)),Increment=decimals==0?1:.01m};values[key]=n;fields.Controls.Add(n);n.ValueChanged+=(s,e)=>Changed();}
            Label("Reviewed existing-chain contact authoring. Place approaches a fixed world anchor, holds it, then returns to the current motion. Release blends the selected chain back into its frozen original motion and removes its later editor constraint. No skeleton or skin changes.");
            action.Items.AddRange(new object[]{"Place","Release"});action.SelectedIndex=0;fields.Controls.Add(action);Label("Existing reviewed IK effector");bone.Items.AddRange(effectors);if(effectors.Length>0)bone.SelectedIndex=0;fields.Controls.Add(bone);
            Number("start","Hold start / release start (1-based)",1,clip.FrameCount,7);Number("end","Hold end (inclusive)",1,clip.FrameCount,Math.Min(13,clip.FrameCount-4));Number("blend","Approach / release blend samples",2,120,4);Number("anchor","Capture world anchor from frame",1,clip.FrameCount,7);
            foreach(string axis in new[]{"x","y","z"})Number(axis,"World position offset "+axis.ToUpperInvariant(),-10000,10000,0,4);
            foreach(string axis in new[]{"rx","ry","rz"})Number(axis,"World rotation offset "+axis.Substring(1).ToUpperInvariant()+" (degrees)",-3600,3600,0,3);
            fields.Controls.Add(orientation);Label("Only the selected reviewed chain can change. Other known native support paths stay fixed. Unsupported surfaces/soft-context constraints or native subframe error reject the result. Scrubbing previews native keys without a solver.");
            var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new Padding(6)};var close=new Button{Text="Close",AutoSize=true};actions.Controls.AddRange(new Control[]{prepare,cancel,review,add,close});Controls.Add(right);Controls.Add(fields);Controls.Add(actions);
            action.SelectedIndexChanged+=(s,e)=>Changed();bone.SelectedIndexChanged+=(s,e)=>Changed();orientation.CheckedChanged+=(s,e)=>Changed();frame.ValueChanged+=(s,e)=>{SourceView.Pose(source,frame.Value);TargetView.Pose(Candidate,Candidate==null?0:frame.Value);};review.CheckedChanged+=(s,e)=>add.Enabled=!Busy&&Candidate!=null&&review.Checked;
            prepare.Click+=async(s,e)=>{if(Busy)return;Discard();int token=++generation;Busy=true;prepare.Enabled=false;cancel.Enabled=true;report.Text="Preparing contact keys offline...";try{var result=await Prepare(ReadOptions());if(IsDisposed||token!=generation){result?.Dispose();return;}Candidate=result;report.Text=PreparedReport;frame.Value=1;SourceView.Pose(source,1);TargetView.Pose(Candidate,1);SourceView.ShownFrame();TargetView.ShownFrame();}catch(Exception error){if(!IsDisposed&&token==generation)report.Text=error.Message;}finally{if(!IsDisposed&&token==generation){Busy=false;prepare.Enabled=true;cancel.Enabled=false;}}};
            cancel.Click+=(s,e)=>{generation++;CancelWork?.Invoke();Busy=false;prepare.Enabled=true;cancel.Enabled=false;Discard();report.Text="Cancelled; no animation changed.";};add.Click+=(s,e)=>{try{if(Candidate!=null&&review.Checked){Apply(ReadOptions(),Candidate);Close();}}catch(Exception error){report.Text=error.Message;Discard();}};close.Click+=(s,e)=>Close();FormClosing+=(s,e)=>{generation++;CancelWork?.Invoke();};FormClosed+=(s,e)=>Discard();
            loading=false;DarkTheme.Apply(this);Shown+=(s,e)=>{SourceView.Pose(source,1);SourceView.ShownFrame();TargetView.ShownFrame();};
        }
        internal NativeContactOptions ReadOptions()=>new NativeContactOptions{Action=Convert.ToString(action.SelectedItem),Bone=Convert.ToString(bone.SelectedItem),Start=(int)values["start"].Value-1,End=Math.Max((int)values["start"].Value-1,(int)values["end"].Value-1),Blend=(int)values["blend"].Value,AnchorFrame=(int)values["anchor"].Value-1,Offset=new[]{(float)values["x"].Value,(float)values["y"].Value,(float)values["z"].Value},Angles=new[]{(float)values["rx"].Value,(float)values["ry"].Value,(float)values["rz"].Value},HoldRotation=orientation.Checked};
        void Changed(){if(loading)return;generation++;CancelWork?.Invoke();Busy=false;prepare.Enabled=true;cancel.Enabled=false;Discard();report.Text="Settings changed; prepare again.";}
        void Discard(){TargetView.Pose(null,0);Candidate?.Dispose();Candidate=null;review.Checked=false;add.Enabled=false;}
    }
}
