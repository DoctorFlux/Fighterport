using BrawlCrate.Studio.Retargeting;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    // This dialog owns controls only; its caller owns every frozen native asset.
    // Playback evaluates already prepared curves and never loads or solves.
    internal sealed class RetargetKeyframeReviewDialog:Form
    {
        internal readonly FittingModelView DenseView,ResultView;
        readonly CHR0Node dense,candidate;
        readonly TrackBar frame;
        readonly Button play=new Button{Name="sourceKeysPlay",Text="Play preview",AutoSize=true};
        readonly Label position=new Label{AutoSize=true,Padding=new Padding(0,6,0,0)};
        readonly Timer clock=new Timer{Interval=33};
        readonly Stopwatch elapsed=new Stopwatch();
        readonly TableLayoutPanel transport,views,layout;
        readonly FlowLayoutPanel actions;
        readonly TextBox report;readonly Label identity,hint;
        readonly Dictionary<Control,Control> focus=new Dictionary<Control,Control>();
        int first;

        internal RetargetKeyframeReviewDialog(ResourceNode denseRoot,MDL0Node denseModel,ResourceNode resultRoot,MDL0Node resultModel,
            RetargetResult prepared,ModelPanelViewport viewer,int initialFrame,string project)
        {
            dense=prepared.DenseAnimation??throw new ArgumentException("A frozen dense Next reference is required.");candidate=prepared.Animation;
            Name="retargetKeyframeReview";Text="Review retarget keyframes";Font=WorkspaceAppearance.Font;AutoScaleMode=AutoScaleMode.None;
            ClientSize=new Size(1060,730);MinimumSize=new Size(760,610);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
            identity=new Label{Name="sourceKeysIdentity",Text=project+" · "+resultModel.Name+" · "+candidate.Name+" · "+candidate.FrameCount+" frames",AutoSize=true,Font=WorkspaceAppearance.BoldFont};
            hint=new Label{Name="sourceKeysHint",Text=prepared.KeyframeMode==RetargetKeyframeMode.Interpolated?
                "Fewer native Hermite keys are checked against the finished dense Next result, independently of the donor key schedule. Review the measured motion before adding it.":
                "Compare the changed curves before adding a target animation. Source key layout does not guarantee identical motion.",AutoSize=true};
            AccessibleDescription="F6 and Shift+F6 move between read-only native previews, timeline, measured report and review actions. Escape cancels without adding an animation.";
            DenseView=new FittingModelView(denseModel,denseRoot,"Original dense Next · read-only",true,viewer){Dock=DockStyle.Fill};
            ResultView=new FittingModelView(resultModel,resultRoot,RetargetKeyframes.ModeLabel(prepared.KeyframeMode)+" · prepared result",false,viewer){Dock=DockStyle.Fill};
            DenseView.Heading.Height=ResultView.Heading.Height=Math.Max(38,Font.Height*2+12);
            views=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));views.Controls.Add(DenseView,0,0);views.Controls.Add(ResultView,1,0);
            frame=new TrackBar{Name="sourceKeysFrame",Dock=DockStyle.Fill,Minimum=1,Maximum=dense.FrameCount,Value=Math.Max(1,Math.Min(dense.FrameCount,initialFrame)),TickStyle=TickStyle.None,AccessibleName="Retarget review frame"};
            transport=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=3};transport.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));transport.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));transport.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));transport.Controls.Add(play,0,0);transport.Controls.Add(frame,1,0);transport.Controls.Add(position,2,0);
            report=new TextBox{Name="sourceKeysReport",ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,Text=prepared.KeyframeReport.Describe()+"\r\n"+prepared.OptimizationReport,AccessibleName="Measured source-key motion differences"};
            var review=new CheckBox{Name="sourceKeysReviewed",Text="I reviewed the motion differences",AutoSize=true};
            var use=new Button{Name="sourceKeysUse",Text="Use this result",AutoSize=true,Enabled=false,DialogResult=DialogResult.OK};
            var cancel=new Button{Name="sourceKeysCancel",Text="Cancel",AutoSize=true,DialogResult=DialogResult.Cancel};
            use.AccessibleDescription="Add this independently measured retarget key layout. Dense contact statistics do not validate its changed curves. Save the project afterward.";
            review.CheckedChanged+=(s,e)=>use.Enabled=review.Checked;
            actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};actions.Controls.AddRange(new Control[]{review,use,cancel});
            layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(8)};
            for(int row=0;row<6;row++)layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles[2]=new RowStyle(SizeType.Percent,70);layout.RowStyles[4]=new RowStyle(SizeType.Percent,30);
            layout.Controls.Add(identity,0,0);layout.Controls.Add(hint,0,1);layout.Controls.Add(views,0,2);layout.Controls.Add(transport,0,3);layout.Controls.Add(report,0,4);layout.Controls.Add(actions,0,5);Controls.Add(layout);
            AcceptButton=use;CancelButton=cancel;
            frame.ValueChanged+=(s,e)=>Pose();play.Click+=(s,e)=>{if(clock.Enabled){Stop();return;}first=frame.Value;elapsed.Restart();clock.Start();play.Text="Pause preview";};
            clock.Tick+=(s,e)=>frame.Value=1+(first-1+(int)(elapsed.Elapsed.TotalSeconds*60))%frame.Maximum;
            Action resize=()=>{int width=Math.Max(100,ClientSize.Width-24);identity.MaximumSize=hint.MaximumSize=new Size(width,0);frame.Height=Math.Max(38,Font.Height+18);};
            Resize+=(s,e)=>resize();
            Shown+=(s,e)=>{resize();DenseView.ShownFrame();ResultView.ShownFrame();Pose();play.Focus();};
            FormClosing+=(s,e)=>Stop();FormClosed+=(s,e)=>clock.Dispose();
            DarkTheme.Apply(this);WorkspaceAppearance.SizeDialog(this);ApplyTextSize();resize();
        }
        internal void ApplyTextSize()
        {
            Font=WorkspaceAppearance.Font;foreach(var control in Children(this))control.Font=Font;identity.Font=WorkspaceAppearance.BoldFont;
            DenseView.Heading.Height=ResultView.Heading.Height=Math.Max(38,Font.Height*2+12);frame.Height=Math.Max(38,Font.Height+18);
            int width=Math.Max(100,ClientSize.Width-24);identity.MaximumSize=hint.MaximumSize=new Size(width,0);PerformLayout();
        }
        void Pose(){DenseView.Pose(dense,frame.Value);ResultView.Pose(candidate,frame.Value);position.Text=frame.Value+" / "+frame.Maximum;}
        void Stop(){clock.Stop();elapsed.Stop();play.Text="Play preview";}
        protected override bool ProcessCmdKey(ref Message message,Keys key)
        {
            if(key==Keys.F6||key==(Keys.Shift|Keys.F6))
            {
                Control[] regions={DenseView.Viewport,ResultView.Viewport,transport,report,actions};int current=Array.FindIndex(regions,c=>c.ContainsFocus);
                if(current>=0)focus[regions[current]]=Children(regions[current]).FirstOrDefault(c=>c.Focused)??regions[current];
                int step=key==Keys.F6?1:-1;
                for(int i=1;i<=regions.Length;i++)
                {
                    var next=regions[(Math.Max(0,current)+i*step+regions.Length*2)%regions.Length];if(!next.Visible||!next.Enabled)continue;
                    if(focus.TryGetValue(next,out var previous)&&previous.CanFocus&&previous.Visible&&previous.Enabled)previous.Focus();
                    else if(!next.Focus())next.SelectNextControl(null,true,true,true,false);return true;
                }
                return true;
            }
            return base.ProcessCmdKey(ref message,key);
        }
        static IEnumerable<Control> Children(Control root){foreach(Control child in root.Controls){yield return child;foreach(var nested in Children(child))yield return nested;}}
    }
}
