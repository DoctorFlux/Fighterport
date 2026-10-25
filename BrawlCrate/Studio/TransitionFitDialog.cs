using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace BrawlCrate.Studio
{
    internal sealed class TransitionFitDialog:Form
    {
        internal readonly FittingModelView SourceView,TargetView;
        internal Func<TransitionFitOptions,Task<CHR0Node>> Prepare;
        internal Action<TransitionFitOptions,CHR0Node> Apply;
        internal Action CancelWork;
        internal string PreparedReport;
        internal bool Busy {get;private set;}
        internal CHR0Node Candidate {get;private set;}
        readonly CHR0Node source;readonly TransitionClipChoice[] clips;int generation;bool loading;
        readonly Dictionary<string,Control> inputs=new Dictionary<string,Control>();
        readonly FlowLayoutPanel fields=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(8)};
        readonly TextBox report=new TextBox{Name="transitionReport",Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false};
        readonly Button prepare=new Button{Name="prepareTransition",Text="Prepare Boundary Fit",AutoSize=true},apply=new Button{Name="applyTransition",Text="Add Independent Animation",AutoSize=true,Enabled=false},cancel=new Button{Name="cancelTransition",Text="Cancel Work",AutoSize=true,Enabled=false};
        readonly CheckBox review=new CheckBox{Name="reviewTransition",Text="I reviewed the result and listed limits",AutoSize=true};
        readonly TrackBar frame;readonly Timer clock=new Timer{Interval=33};readonly Button play=new Button{Text="Play Preview",AutoSize=true};readonly Label frameText=new Label{AutoSize=true,Padding=new Padding(0,6,0,0)};
        readonly CheckedListBox bones=new CheckedListBox{Name="transitionBones",Width=306,Height=155,CheckOnClick=true};
        string preparedOptions;
        readonly Stopwatch previewTime=new Stopwatch();int previewStart;
        internal TransitionFitDialog(ResourceNode sourceRoot,MDL0Node sourceModel,ResourceNode targetRoot,MDL0Node targetModel,CHR0Node motion,TransitionClipChoice[] choices,ModelPanelViewport viewer,IEnumerable<ResourceNode> references,string pinReview=null,bool preserveContactsByDefault=false)
        {
            loading=true;source=motion;clips=choices;Text="Fit Animation Boundaries / Transitions";ClientSize=new Size(1240,820);MinimumSize=new Size(1040,720);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
            SourceView=new FittingModelView(sourceModel,sourceRoot,"SOURCE · existing native animation · read-only",true,viewer,references){Dock=DockStyle.Fill};
            TargetView=new FittingModelView(targetModel,targetRoot,"RESULT / REFERENCE · existing native rig",false,viewer,references){Dock=DockStyle.Fill};
            var views=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));views.Controls.Add(SourceView,0,0);views.Controls.Add(TargetView,1,0);
            frame=new TrackBar{Name="transitionFrame",Dock=DockStyle.Fill,Minimum=1,Maximum=motion.FrameCount,Value=1,TickStyle=TickStyle.None};
            var transport=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};transport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));transport.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));transport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,85));transport.Controls.Add(play,0,0);transport.Controls.Add(frame,1,0);transport.Controls.Add(frameText,2,0);
            var right=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3};right.RowStyles.Add(new RowStyle(SizeType.Percent,68));right.RowStyles.Add(new RowStyle(SizeType.Absolute,42));right.RowStyles.Add(new RowStyle(SizeType.Percent,32));right.Controls.Add(views,0,0);right.Controls.Add(transport,0,1);right.Controls.Add(report,0,2);
            var left=new Panel{Dock=DockStyle.Left,Width=344};left.Controls.Add(fields);
            Label("Fixed-duration native boundary fit",true);Label("Fit selected component curves inside the existing first/last samples. Middle keys and duration stay protected. Donor clips must be retargeted first. Contact preservation retains original bone/skin paths. Optional hip / leg exit recovery is limited to the explicitly reviewed final window.");
            Label("New independent animation name");var name=new TextBox{Name="transitionName",Width=306,Text=motion.Name+" (fit)"};inputs["name"]=name;fields.Controls.Add(name);name.TextChanged+=(s,e)=>Changed();
            foreach(string side in new[]{"entry","exit"})
            {
                Label(side=="entry"?"Entry reference":"Exit reference",true);var choice=new ComboBox{Name="transition_"+side,DropDownStyle=ComboBoxStyle.DropDownList,Width=306};choice.Items.Add("Disabled");choice.Items.AddRange(choices);choice.SelectedIndex=side=="entry"?Math.Max(1,Array.FindIndex(choices,c=>c.Clip!=motion)+1):0;inputs[side]=choice;fields.Controls.Add(choice);
                Number(side+"Frame","Reference frame (1-based, fractional)",1,100000,3).Value=1;
                Number(side+"Samples","Existing samples to fit",2,Math.Max(2,motion.FrameCount-1),0).Value=Math.Max(2,Math.Min(6,(motion.FrameCount-1)/3));
                Check(side+"Frozen","Freeze reference pose (zero endpoint velocity)").Checked=true;Number(side+"Rate","Moving reference speed",.001m,4,3).Value=1;
                var preview=new Button{Name="preview_"+side,Text="Preview "+side+" reference",AutoSize=true};string selected=side;preview.Click+=(s,e)=>PreviewReference(selected);fields.Controls.Add(preview);
                choice.SelectedIndexChanged+=(s,e)=>{Changed();UpdateReference(selected);};((NumericUpDown)inputs[side+"Frame"]).ValueChanged+=(s,e)=>PreviewReference(selected);((CheckBox)inputs[side+"Frozen"]).CheckedChanged+=(s,e)=>((NumericUpDown)inputs[selected+"Rate"]).Enabled=!B(selected+"Frozen");((NumericUpDown)inputs[side+"Rate"]).Enabled=false;
            }
            Label("Components / existing native bones",true);Check("rotation","Match rotation channels").Checked=true;Check("translation","Match translation channels");Check("scale","Match scale channels");Check("root","Preserve translation on skeleton roots").Checked=true;Check("nearest","Use equivalent Euler angles nearest the original join (off keeps authored turns)").Checked=true;
            Check("contacts","Preserve existing contact trajectories (default: complete affected bone paths)").Checked=preserveContactsByDefault;
            Check("exitContactRecovery","Allow hip / leg contact recovery on a non-looping exit (opt-in)");
            Label("Recovery fits reviewed hip / leg paths only after the original exit join, to the exact frozen first exit frame. Earlier contact curves and protected motion controls remain unchanged. Fixed or overlapping user pins block conflicting recovery; no new support or root alignment is inferred.");
            Label("Current animation pin protections",true);
            fields.Controls.Add(new TextBox{Name="transitionPinReview",Width=306,Height=88,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,Text=pinReview??"Current pin setup is unavailable. Set up this exact animation's controls before contact recovery.",AccessibleName="Exact current animation pins and protection review"});
            var names=sourceModel._linker.BoneCache.Select(b=>b.Name).ToArray();var animated=new HashSet<string>(motion.Children.Select(c=>c.Name));foreach(string bone in names)bones.Items.Add(bone,animated.Contains(bone));fields.Controls.Add(bones);bones.ItemCheck+=(s,e)=>Changed();
            var all=new Button{Text="All Existing Bones",AutoSize=true};all.Click+=(s,e)=>{for(int i=0;i<bones.Items.Count;i++)bones.SetItemChecked(i,true);};fields.Controls.Add(all);
            Label("Protected frames: 1-based inclusive ranges, e.g. 10-25; 40-45");var protect=new TextBox{Name="transitionProtected",Width=306};inputs["protected"]=protect;fields.Controls.Add(protect);protect.TextChanged+=(s,e)=>Changed();
            Check("binding","I reviewed these clips' native bones, bind space, units and listed pin protections");Label("Loop flag is retained. Inspect its seam. This authors FK curves; contacts, clearance, joint limits and game-state behavior are not validated.");
            var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new Padding(6)};var close=new Button{Text="Close",AutoSize=true};actions.Controls.AddRange(new Control[]{prepare,cancel,review,apply,close});Controls.Add(right);Controls.Add(left);Controls.Add(actions);
            prepare.Click+=async(s,e)=>await PrepareFit();apply.Click+=(s,e)=>{try{if(Busy||Candidate==null||!review.Checked||preparedOptions!=Key(ReadOptions()))return;Apply(ReadOptions(),Candidate);Close();}catch(Exception error){report.Text=error.Message;Discard();}};cancel.Click+=(s,e)=>{generation++;CancelWork?.Invoke();SetBusy(false);Discard();report.Text="Preparation cancelled; no animation changed.";};
            review.CheckedChanged+=(s,e)=>{apply.Enabled=!Busy&&Candidate!=null&&review.Checked;if(review.Checked&&Candidate!=null)ShowFrame();};
            frame.ValueChanged+=(s,e)=>ShowFrame();play.Click+=(s,e)=>{if(Busy)return;if(clock.Enabled)Stop();else{previewStart=frame.Value;previewTime.Restart();clock.Start();play.Text="Pause Preview";}};clock.Tick+=(s,e)=>frame.Value=1+(previewStart-1+(int)(previewTime.Elapsed.TotalSeconds*60))%frame.Maximum;close.Click+=(s,e)=>Close();
            FormClosing+=(s,e)=>{generation++;CancelWork?.Invoke();Stop();};FormClosed+=(s,e)=>{clock.Dispose();Discard();};
            loading=false;UpdateReference("entry");UpdateReference("exit");ShowFrame();report.Text="Choose exact native references, review bindings, and prepare a separate result. No retargeter or contact solver runs during preview playback.";DarkTheme.Apply(this);Shown+=(s,e)=>{SourceView.ShownFrame();TargetView.ShownFrame();};
        }
        void Label(string text,bool title=false)=>fields.Controls.Add(new Label{Text=text,Width=306,AutoSize=true,MaximumSize=new Size(306,0),Margin=new Padding(0,8,0,4),Font=title?new Font(Font,FontStyle.Bold):Font});
        NumericUpDown Number(string key,string text,decimal min,decimal max,int places){Label(text);var value=new NumericUpDown{Name="transition_"+key,Width=306,Minimum=min,Maximum=max,DecimalPlaces=places,Increment=places==0?1:.01m};inputs[key]=value;fields.Controls.Add(value);value.ValueChanged+=(s,e)=>Changed();return value;}
        CheckBox Check(string key,string text){var value=new CheckBox{Name="transition_"+key,Text=text,Width=306,AutoSize=true,MaximumSize=new Size(306,0)};inputs[key]=value;fields.Controls.Add(value);value.CheckedChanged+=(s,e)=>Changed();return value;}
        bool B(string key)=>((CheckBox)inputs[key]).Checked;double N(string key)=>(double)((NumericUpDown)inputs[key]).Value;
        TransitionClipChoice Choice(string side)=>((ComboBox)inputs[side]).SelectedItem as TransitionClipChoice;
        static string Key(TransitionFitOptions value)=>new JavaScriptSerializer().Serialize(value);
        internal TransitionFitOptions ReadOptions()
        {
            var ranges=new List<TransitionProtected>();string text=((TextBox)inputs["protected"]).Text;
            foreach(string item in text.Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries)){var split=item.Trim().Split('-');if(split.Length!=2||!double.TryParse(split[0],NumberStyles.Float,CultureInfo.CurrentCulture,out double a)||!double.TryParse(split[1],NumberStyles.Float,CultureInfo.CurrentCulture,out double b)||a<1||b<a||b>source.FrameCount)throw new InvalidOperationException("Protected ranges must be 1-based inclusive frames within this clip, separated by semicolons.");ranges.Add(new TransitionProtected{Start=a-1,End=b-1});}
            return new TransitionFitOptions{Name=((TextBox)inputs["name"]).Text,Entry=Choice("entry")?.Key,Exit=Choice("exit")?.Key,EntryFrame=N("entryFrame")-1,ExitFrame=N("exitFrame")-1,EntrySamples=(int)N("entrySamples"),ExitSamples=(int)N("exitSamples"),EntryFrozen=B("entryFrozen"),ExitFrozen=B("exitFrozen"),EntryRate=N("entryRate"),ExitRate=N("exitRate"),Bones=bones.CheckedItems.Cast<string>().ToArray(),Components=(B("scale")?1:0)|(B("rotation")?2:0)|(B("translation")?4:0),Protected=ranges.ToArray(),PreserveRootTranslation=B("root"),NearestEquivalentAngles=B("nearest"),BindingReviewed=B("binding"),PreserveContacts=B("contacts"),ExitContactRecovery=B("exitContactRecovery")};
        }
        void Changed(){if(loading)return;generation++;if(Busy){CancelWork?.Invoke();SetBusy(false);}Discard();report.Text="Settings changed; prepare a new result.";}
        internal void RestoreOptions(TransitionFitOptions value)
        {
            loading=true;
            void Set(string key,double v){var n=(NumericUpDown)inputs[key];n.Value=Math.Max(n.Minimum,Math.Min(n.Maximum,(decimal)v));}
            foreach(string side in new[]{"entry","exit"}){var choice=(ComboBox)inputs[side];string key=side=="entry"?value.Entry:value.Exit;choice.SelectedIndex=Math.Max(0,Array.FindIndex(clips,c=>c.Key==key)+1);UpdateReference(side);Set(side+"Frame",(side=="entry"?value.EntryFrame:value.ExitFrame)+1);Set(side+"Samples",side=="entry"?value.EntrySamples:value.ExitSamples);Set(side+"Rate",side=="entry"?value.EntryRate:value.ExitRate);((CheckBox)inputs[side+"Frozen"]).Checked=side=="entry"?value.EntryFrozen:value.ExitFrozen;}
            ((CheckBox)inputs["rotation"]).Checked=(value.Components&2)!=0;((CheckBox)inputs["scale"]).Checked=(value.Components&1)!=0;((CheckBox)inputs["translation"]).Checked=(value.Components&4)!=0;((CheckBox)inputs["root"]).Checked=value.PreserveRootTranslation;((CheckBox)inputs["nearest"]).Checked=value.NearestEquivalentAngles;((CheckBox)inputs["contacts"]).Checked=value.PreserveContacts;((CheckBox)inputs["exitContactRecovery"]).Checked=value.ExitContactRecovery;((CheckBox)inputs["binding"]).Checked=false;
            for(int i=0;i<bones.Items.Count;i++)bones.SetItemChecked(i,value.Bones.Contains(Convert.ToString(bones.Items[i])));((TextBox)inputs["protected"]).Text=string.Join("; ",value.Protected.Select(p=>(p.Start+1).ToString(CultureInfo.CurrentCulture)+"-"+(p.End+1).ToString(CultureInfo.CurrentCulture)));
            loading=false;Discard();report.Text="Saved fitting settings restored. This prepares from the CURRENT animation, including later pin edits. Review live reference bindings again; missing live references are disabled. Frozen originals remain available in Transition Fit Details.";
        }
        void UpdateReference(string side){var choice=Choice(side);var value=(NumericUpDown)inputs[side+"Frame"];value.Maximum=Math.Max(1,choice?.Clip.FrameCount??1);if(value.Value>value.Maximum)value.Value=value.Maximum;}
        void PreviewReference(string side){if(loading||Busy)return;var choice=Choice(side);if(choice==null)return;Stop();review.Checked=false;TargetView.Pose(choice.Clip,(float)N(side+"Frame"));TargetView.Heading.Text="REFERENCE · "+side+" · "+choice.Clip.Name+" · frame "+N(side+"Frame");TargetView.ShownFrame();}
        void SetBusy(bool value){Busy=value;prepare.Enabled=play.Enabled=frame.Enabled=!value;cancel.Enabled=value;apply.Enabled=!value&&Candidate!=null&&review.Checked;}
        void Stop(){clock.Stop();previewTime.Stop();play.Text="Play Preview";}
        void Discard(){Stop();TargetView.Pose(null,0);Candidate?.Dispose();Candidate=null;preparedOptions=null;review.Checked=false;apply.Enabled=false;TargetView.Heading.Text="RESULT · prepare a new independent animation";}
        async Task PrepareFit()
        {
            if(Busy)return;Discard();int token=++generation;SetBusy(true);report.Text="Preparing fixed-duration native FK curves offline...";
            try{var options=ReadOptions();var result=await Prepare(options);if(IsDisposed||token!=generation){result?.Dispose();return;}Candidate=result;preparedOptions=Key(options);report.Text=PreparedReport;TargetView.Heading.Text="RESULT · native fitted animation · review before adding";frame.Value=1;ShowFrame();SourceView.ShownFrame();TargetView.ShownFrame();}
            catch(Exception error){if(!IsDisposed&&token==generation)report.Text=error.Message;}
            finally{if(!IsDisposed&&token==generation)SetBusy(false);}
        }
        void ShowFrame(){SourceView.Pose(source,frame.Value);TargetView.Pose(Candidate,Candidate==null?0:frame.Value);if(Candidate!=null)TargetView.Heading.Text="RESULT · native fitted animation · review before adding";frameText.Text=frame.Value+" / "+source.FrameCount;}
    }
}
