using BrawlCrate.Studio.Retargeting;
using BrawlLib.Internal;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace BrawlCrate.Studio
{
    internal sealed class SceneFittingDialog:Form
    {
        internal readonly FittingModelView SourceView,TargetView;
        internal Func<SceneFittingOptions,string,string,Task<SceneFittingReply>> Prepare,LoadReview;
        internal Func<string,Task<SceneFittingReply>> ReadContext;
        internal Func<RetargetResult> OriginalNext;
        internal Action<bool> ReviewCoordinates;internal Action CancelWork;internal Func<SceneFittingOptions,SceneFittingReply,string,string,Task> SaveReview;
        internal bool Busy {get;private set;}internal SceneFittingReply Reply {get;private set;}
        internal string ContextPath,ContextClip;
        readonly CHR0Node original;CHR0Node candidate;RetargetResult baseline;
        bool loading,advancedCandidate;int generation;
        readonly FlowLayoutPanel fields=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(8)};
        readonly Dictionary<string,Control> inputs=new Dictionary<string,Control>();
        readonly TextBox report=new TextBox{Name="fittingReport",Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false};
        readonly Button prepare=new Button{Name="prepareFitting",Text="Prepare Advanced Draft",AutoSize=true},cancel=new Button{Name="cancelFitting",Text="Cancel Work",AutoSize=true,Enabled=false},save=new Button{Name="saveFittingReview",Text="Save Review Settings",AutoSize=true},apply=new Button{Name="applyFitting",Text="Apply Advanced Result",AutoSize=true,Enabled=false};
        readonly TrackBar frame=new TrackBar{Name="fittingFrame",Minimum=1,Maximum=1,Value=1,Dock=DockStyle.Fill,TickStyle=TickStyle.None};
        readonly Timer clock=new Timer{Interval=33};readonly Button play=new Button{Name="fittingPlay",Text="Play Preview",AutoSize=true};readonly Label frameLabel=new Label{AutoSize=true,Padding=new Padding(0,7,0,0)};
        readonly ComboBox contexts=new ComboBox{Name="fittingContextClip",DropDownStyle=ComboBoxStyle.DropDownList,Width=306};
        internal SceneFittingDialog(ResourceNode sourceRoot,MDL0Node source,CHR0Node motion,ResourceNode targetRoot,MDL0Node target,SceneFittingOptions settings,ModelPanelViewport viewer,IEnumerable<ResourceNode> targetReferences=null)
        {
            loading=true;
            Text="Advanced Scene / Body Fitting";ClientSize=new Size(1240,820);MinimumSize=new Size(1000,700);ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;original=motion;
            SourceView=new FittingModelView(source,sourceRoot,"SOURCE · authentic library motion · read-only",true,viewer){Dock=DockStyle.Fill};TargetView=new FittingModelView(target,targetRoot,"TARGET · native pose · read-only review",false,viewer,targetReferences){Dock=DockStyle.Fill};
            var views=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));views.Controls.Add(SourceView,0,0);views.Controls.Add(TargetView,1,0);
            var right=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3};right.RowStyles.Add(new RowStyle(SizeType.Percent,63));right.RowStyles.Add(new RowStyle(SizeType.Absolute,42));right.RowStyles.Add(new RowStyle(SizeType.Percent,37));right.Controls.Add(views,0,0);
            var transport=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};transport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,115));transport.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));transport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,85));transport.Controls.Add(play,0,0);transport.Controls.Add(frame,1,0);transport.Controls.Add(frameLabel,2,0);right.Controls.Add(transport,0,1);right.Controls.Add(report,0,2);
            var left=new Panel{Dock=DockStyle.Left,Width=344};left.Controls.Add(fields);
            Label("Existing-rig body / scene review",true);Label("Original Next remains the normal retargeter. Advanced drafts keep unresolved motion, collision and contact gates; saving settings does not apply animation.");
            Choice("scene","Supplied support geometry",new[]{"No supplied geometry","Flat floor","Floor + 90° ledge"});Choice("surface","Support phase mode",new[]{"Automatic ground / edge phases","Ground throughout","Edge throughout","Free / actor controlled"});
            Optional("sourceFloor","Source floor (native units)",-1000000,1000000,4);Optional("targetFloor","Target floor (native units)",-1000000,1000000,4);
            Number("sourceEdge","Source edge offset",-1000000,1000000,4);Number("targetEdge","Target edge offset",-1000000,1000000,4);Check("reverse","Platform opposite reviewed forward");
            Choice("root","Root trajectory",new[]{"Preserve root trajectory","In place (bone draft)","Actor controlled (bone draft)"});Check("automatic","Fit body-relative reach / scene · draft");Check("overlap","Allow incidental body overlap (still reported)");
            Optional("tolerance","Limb tolerance (%; auto = rig profile)",.0001m,100,4);
            Label("Authored locks and inferred contacts remain unvalidated. This is scene guidance, not native stage output. SCN0 remains inactive.");
            foreach(var spec in new[]{new[]{"arm","Arm spacing (degrees)","0"},new[]{"stance","Stance width (%)","100"},new[]{"pelvis","Pelvis height (% leg)","0"},new[]{"lean","Body lean (degrees)","0"},new[]{"reach","Free-hand reach (%)","100"}}){var control=Number(spec[0],spec[1],-1000,1000,2);control.Value=decimal.Parse(spec[2]);control.Enabled=false;}
            Label("Body preference controls await measured calibration; neutral values are retained.");
            Label("Export timing (source seconds)",true);Number("start","Trim start",0,100000,4);Optional("end","Trim end (auto = whole clip)",.0001m,100000,4);Number("speed","Export speed",.25m,2,3);Check("losses","I reviewed the listed source channel losses");
            Label("Exact companion source context",true);var context=new Button{Name="chooseFittingContext",Text="Open Companion Project...",AutoSize=true};fields.Controls.Add(context);fields.Controls.Add(contexts);var clear=new Button{Text="Use Native Context Only",AutoSize=true};fields.Controls.Add(clear);
            var coords=new FlowLayoutPanel{AutoSize=true,Width=310};foreach(bool src in new[]{true,false}){bool chosen=src;var button=new Button{Text=src?"Review Source Axes...":"Review Target Axes...",AutoSize=true};button.Click+=async(s,e)=>{if(Busy)return;try{ReviewCoordinates?.Invoke(chosen);await Reload();}catch(Exception error){if(!IsDisposed)report.Text=error.Message;}};coords.Controls.Add(button);}fields.Controls.Add(coords);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new Padding(6)};var next=new Button{Name="previewOriginalNext",Text="Preview Original Next",AutoSize=true};var close=new Button{Text="Close",AutoSize=true};actions.Controls.AddRange(new Control[]{prepare,cancel,next,save,apply,close});Controls.Add(right);Controls.Add(left);Controls.Add(actions);
            prepare.Click+=async(s,e)=>await Run(true);cancel.Click+=(s,e)=>Cancel();save.Click+=async(s,e)=>{if(Busy)return;int token=++generation;SetBusy(true);try{await SaveReview(ReadOptions(),Reply,ContextPath,ContextClip);if(!IsDisposed&&token==generation)report.AppendText("\r\nReview settings saved; animation and native rig unchanged.");}catch(Exception error){if(!IsDisposed&&token==generation)report.Text=error.Message;}finally{if(!IsDisposed&&token==generation)SetBusy(false);}};
            close.Click+=(s,e)=>Close();play.Click+=(s,e)=>{if(Busy)return;if(clock.Enabled)Stop();else{clock.Start();play.Text="Pause Preview";}};clock.Tick+=(s,e)=>{frame.Value=frame.Value>=frame.Maximum?1:frame.Value+1;};frame.ValueChanged+=(s,e)=>ShowFrame();
            next.Click+=(s,e)=>{if(Busy)return;try{Stop();ClearCandidate();baseline=OriginalNext?.Invoke();if(baseline==null)return;candidate=baseline.Animation;advancedCandidate=false;TargetView.Heading.Text="TARGET · original Next baseline · read-only";frame.Maximum=Math.Max(1,candidate.FrameCount);frame.Value=1;ShowFrame();TargetView.ShownFrame();report.Text="Original Next baseline. No result was imported; advanced settings are not used by this comparison.\r\nOmitted tracks: "+string.Join(", ",baseline.IgnoredSourceBones);}catch(Exception error){report.Text=error.Message;}};
            context.Click+=async(s,e)=>{if(Busy)return;using(var dialog=new OpenFileDialog{Title="Exact Source Companion Context",Filter="FighterPort core project (*.fighterport)|*.fighterport"})if(dialog.ShowDialog(this)==DialogResult.OK)await LoadContext(dialog.FileName);};
            clear.Click+=async(s,e)=>{if(Busy)return;loading=true;ContextPath=ContextClip=null;contexts.Items.Clear();loading=false;InvalidateReview();await Reload();};contexts.SelectedIndexChanged+=async(s,e)=>{if(loading||Busy)return;ContextClip=(contexts.SelectedItem as FittingContextClip)?.Id;InvalidateReview();await Reload();};
            FormClosing+=(s,e)=>{generation++;CancelWork?.Invoke();Stop();};FormClosed+=(s,e)=>{clock.Dispose();ClearCandidate();};
            SetOptions(settings??new SceneFittingOptions());DarkTheme.Apply(this);Shown+=async(s,e)=>{SourceView.ShownFrame();TargetView.ShownFrame();if(string.IsNullOrEmpty(ContextPath))await Reload();else await LoadContext(ContextPath,ContextClip);};
        }
        void Label(string text,bool title=false){fields.Controls.Add(new Label{Text=text,Width=306,AutoSize=true,MaximumSize=new Size(306,0),Margin=new Padding(0,8,0,4),Font=title?new Font(Font,FontStyle.Bold):Font});}
        ComboBox Choice(string key,string text,string[] choices){Label(text);var box=new ComboBox{Name="fitting_"+key,DropDownStyle=ComboBoxStyle.DropDownList,Width=306};box.Items.AddRange(choices);inputs[key]=box;fields.Controls.Add(box);box.SelectedIndexChanged+=(s,e)=>InvalidateReview();return box;}
        CheckBox Check(string key,string text){var box=new CheckBox{Name="fitting_"+key,Text=text,Width=306,AutoSize=true,MaximumSize=new Size(306,0)};inputs[key]=box;fields.Controls.Add(box);box.CheckedChanged+=(s,e)=>InvalidateReview();return box;}
        NumericUpDown Number(string key,string text,decimal min,decimal max,int decimals){Label(text);var number=new NumericUpDown{Name="fitting_"+key,Minimum=min,Maximum=max,DecimalPlaces=decimals,Width=306,Increment=decimals>0?.01m:1};inputs[key]=number;fields.Controls.Add(number);number.ValueChanged+=(s,e)=>InvalidateReview();return number;}
        void Optional(string key,string text,decimal min,decimal max,int decimals){var auto=Check(key+"Auto","Automatic: "+text);var number=Number(key,text,min,max,decimals);auto.CheckedChanged+=(s,e)=>number.Enabled=!auto.Checked;}
        decimal N(string key)=>((NumericUpDown)inputs[key]).Value;bool B(string key)=>((CheckBox)inputs[key]).Checked;int I(string key)=>((ComboBox)inputs[key]).SelectedIndex;
        float? OptionalValue(string key)=>B(key+"Auto")?(float?)null:(float)N(key);
        internal SceneFittingOptions ReadOptions()=>new SceneFittingOptions{AutomaticFitting=B("automatic"),AllowOverlap=B("overlap"),RootPolicy=I("root"),Scene=I("scene"),SurfaceMode=I("surface"),SourceFloor=OptionalValue("sourceFloor"),TargetFloor=OptionalValue("targetFloor"),SourceEdge=(float)N("sourceEdge"),TargetEdge=(float)N("targetEdge"),ReverseEdge=B("reverse"),LimbTolerancePercent=OptionalValue("tolerance"),TrimStartSeconds=(double)N("start"),TrimEndSeconds=B("endAuto")?(double?)null:(double)N("end"),ExportSpeed=(double)N("speed"),LossesReviewed=B("losses")};
        internal void SetOptions(SceneFittingOptions o)
        {
            loading=true;((CheckBox)inputs["automatic"]).Checked=o.AutomaticFitting;((CheckBox)inputs["overlap"]).Checked=o.AllowOverlap;((CheckBox)inputs["reverse"]).Checked=o.ReverseEdge;((CheckBox)inputs["losses"]).Checked=o.LossesReviewed;
            foreach(var p in new[]{Tuple.Create("scene",o.Scene),Tuple.Create("surface",o.SurfaceMode),Tuple.Create("root",o.RootPolicy)})((ComboBox)inputs[p.Item1]).SelectedIndex=p.Item2;
            void Set(string key,decimal value){var number=(NumericUpDown)inputs[key];number.Value=Math.Max(number.Minimum,Math.Min(number.Maximum,value));}
            void Opt(string key,double? value){((CheckBox)inputs[key+"Auto"]).Checked=!value.HasValue;Set(key,(decimal)(value??0));((NumericUpDown)inputs[key]).Enabled=value.HasValue;}
            Opt("sourceFloor",o.SourceFloor);Opt("targetFloor",o.TargetFloor);Opt("tolerance",o.LimbTolerancePercent);Opt("end",o.TrimEndSeconds);Set("sourceEdge",(decimal)o.SourceEdge);Set("targetEdge",(decimal)o.TargetEdge);Set("start",(decimal)o.TrimStartSeconds);Set("speed",(decimal)o.ExportSpeed);loading=false;UpdateSupport();
        }
        void UpdateSupport(){if(loading)return;var options=ReadOptions();SourceView.Options=TargetView.Options=options;SourceView.Viewport.Invalidate();TargetView.Viewport.Invalidate();}
        void InvalidateReview(){if(loading)return;generation++;if(Busy){CancelWork?.Invoke();SetBusy(false);}Stop();Reply=null;ClearCandidate();apply.Enabled=false;TargetView.Heading.Text="TARGET · settings changed · prepare a new review";report.Text="Settings changed. Prepared results are stale; no animation was applied.";UpdateSupport();}
        void SetBusy(bool value){Busy=value;prepare.Enabled=save.Enabled=play.Enabled=!value;cancel.Enabled=value;frame.Enabled=!value;}
        void Stop(){clock.Stop();play.Text="Play Preview";}
        void Cancel(){generation++;CancelWork?.Invoke();SetBusy(false);Stop();ClearCandidate();Reply=null;report.Text="Advanced preparation cancelled. No animation was changed.";}
        internal Task Reload()=>Run(false);
        internal async Task LoadContext(string path,string selectedClip=null)
        {
            if(Busy)return;InvalidateReview();int token=++generation;SetBusy(true);
            try{var value=await ReadContext(path);if(IsDisposed||token!=generation)return;loading=true;ContextPath=path;contexts.Items.Clear();contexts.Items.AddRange(value.ContextClips??new FittingContextClip[0]);int index=Array.FindIndex(value.ContextClips??new FittingContextClip[0],c=>c.Id==selectedClip);contexts.SelectedIndex=index>=0?index:contexts.Items.Count>0?0:-1;ContextClip=(contexts.SelectedItem as FittingContextClip)?.Id;loading=false;report.Text=value.Context;SetBusy(false);await Reload();}
            catch(Exception error){if(!IsDisposed&&token==generation)report.Text=error.Message;}
            finally{loading=false;if(!IsDisposed&&token==generation)SetBusy(false);}
        }
        async Task Run(bool prepareDraft)
        {
            if(Busy)return;Stop();ClearCandidate();int token=++generation;SetBusy(true);report.Text=prepareDraft?"Preparing offline body / scene review...":"Reading authentic source context...";
            try
            {
                var reply=await (prepareDraft?Prepare:LoadReview)(ReadOptions(),ContextPath,ContextClip);if(IsDisposed||token!=generation)return;Reply=reply;
                SourceView.Up=reply.SourceUp;SourceView.Forward=reply.SourceForward;TargetView.Up=reply.TargetUp;TargetView.Forward=reply.TargetForward;UpdateSupport();
                report.Text=reply.Algorithm+" · "+reply.State+" · review only; Apply unavailable\r\n"+reply.Context+"\r\n"+(reply.MissingSourceBones?.Length>0?"Keyed channels absent from the authentic source: "+string.Join(", ",reply.MissingSourceBones)+"\r\nNo source tracks were removed or rebound.\r\n":"")+string.Join("\r\n",(reply.Diagnostics??new SceneFittingIssue[0]).Select(d=>d.Code+": "+d.Message))+"\r\n"+string.Join("\r\n",reply.Losses??new string[0])+"\r\n"+reply.Summary;
                if(reply.PreviewTracks?.Length>0){candidate=Proxy(reply);advancedCandidate=true;TargetView.Heading.Text="TARGET · advanced draft · sampled review proxy · Apply blocked";frame.Maximum=Math.Max(1,candidate.FrameCount);frame.Value=1;ShowFrame();TargetView.ShownFrame();}
                else{TargetView.Heading.Text="TARGET · no prepared advanced candidate · native pose";frame.Maximum=Math.Max(1,original.FrameCount);frame.Value=1;ShowFrame();}
                SourceView.ShownFrame();TargetView.ShownFrame();
                apply.Enabled=false;
            }
            catch(Exception error){if(!IsDisposed&&token==generation){Reply=null;report.Text=error.Message;}}
            finally{if(!IsDisposed&&token==generation)SetBusy(false);}
        }
        CHR0Node Proxy(SceneFittingReply reply)
        {
            var clip=new CHR0Node{Name="Review proxy",FrameCount=reply.TargetFrames,Loop=reply.PreviewLoop};
            foreach(var track in reply.PreviewTracks){var bone=TargetView.Model._linker.BoneCache.Single(b=>b.Name==track.Bone);var entry=new CHR0EntryNode{Name=track.Bone};clip.AddChild(entry);for(int group=0;group<3;group++){bool keyed=track.Channels.Skip(group*3).Take(3).Any(c=>c.Length>0);if(!keyed)continue;for(int channel=group*3;channel<group*3+3;channel++){var keys=track.Channels[channel];if(keys.Length==0)entry.SetKeyframe(channel,0,bone.BindState[channel],true,true);else foreach(var key in keys)entry.SetKeyframe(channel,key.Frame,key.Value,true,true);}}}
            return clip;
        }
        void ShowFrame(){float target=frame.Value,source=advancedCandidate?(float)(ReadOptions().TrimStartSeconds*60+(target-1)*ReadOptions().ExportSpeed+1):target;SourceView.Pose(original,Math.Max(1,Math.Min(original.FrameCount,source)));TargetView.Pose(candidate,candidate==null?0:target);frameLabel.Text=frame.Value+" / "+frame.Maximum;}
        void ClearCandidate(){if(baseline!=null){baseline.Dispose();baseline=null;}else candidate?.Dispose();candidate=null;advancedCandidate=false;TargetView.Pose(null,0);}
    }
}
