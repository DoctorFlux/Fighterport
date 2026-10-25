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
        readonly CHR0Node source;readonly TransitionClipChoice[] clips;readonly string[] protectedMotion,referenceControls;string referenceSide;int generation;bool loading,syncRanges;
        readonly Label ranges=new Label{Name="transitionRanges",AutoSize=true,MaximumSize=new Size(306,0)};
        readonly Button advancedToggle=new Button{Name="transitionAdvanced",Text="Advanced",AutoSize=true};
        readonly FlowLayoutPanel advanced=new FlowLayoutPanel{Name="transitionAdvancedFields",FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoSize=true,Visible=false};
        readonly ToolTip hints=new ToolTip();
        readonly Dictionary<string,Control> inputs=new Dictionary<string,Control>();
        readonly FlowLayoutPanel fields=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(8)};
        readonly TextBox report=new TextBox{Name="transitionReport",Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false};
        readonly Button prepare=new Button{Name="prepareTransition",Text="Preview fit",AutoSize=true},apply=new Button{Name="applyTransition",Text="Add animation",AutoSize=true,Enabled=false},cancel=new Button{Name="cancelTransition",Text="Cancel",AutoSize=true,Enabled=false};
        readonly CheckBox review=new CheckBox{Name="reviewTransition",Text="I reviewed the preview and limits",AutoSize=true};
        readonly TrackBar frame;readonly Timer clock=new Timer{Interval=33};readonly Button play=new Button{Text="Play Preview",AutoSize=true};readonly Label frameText=new Label{AutoSize=true,Padding=new Padding(0,6,0,0)};
        readonly CheckedListBox bones=new CheckedListBox{Name="transitionBones",Width=306,Height=155,CheckOnClick=true};
        string preparedOptions;Action layoutFields;TableLayoutPanel transportLayout,contentLayout;FlowLayoutPanel footer;
        readonly Stopwatch previewTime=new Stopwatch();int previewStart;
        internal TransitionFitDialog(ResourceNode sourceRoot,MDL0Node sourceModel,ResourceNode targetRoot,MDL0Node targetModel,CHR0Node motion,TransitionClipChoice[] choices,ModelPanelViewport viewer,IEnumerable<ResourceNode> references,string pinReview=null,bool preserveContactsByDefault=false,IEnumerable<string> protectedMotionControls=null,IEnumerable<string> referenceFitControls=null)
        {
            loading=true;source=motion;clips=choices;protectedMotion=protectedMotionControls?.Distinct(StringComparer.Ordinal).ToArray();referenceControls=referenceFitControls?.Distinct(StringComparer.Ordinal).ToArray()??new string[0];Text="Fit start and end";AutoScaleMode=AutoScaleMode.None;Font=WorkspaceAppearance.Font;ClientSize=new Size(1240,820);MinimumSize=new Size(920,640);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
            SourceView=new FittingModelView(sourceModel,sourceRoot,"Original attack · read-only",true,viewer,references){Dock=DockStyle.Fill};
            TargetView=new FittingModelView(targetModel,targetRoot,"Fitted result",false,viewer,references){Dock=DockStyle.Fill};
            var views=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));views.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));views.Controls.Add(SourceView,0,0);views.Controls.Add(TargetView,1,0);
            frame=new TrackBar{Name="transitionFrame",Dock=DockStyle.Fill,Minimum=1,Maximum=motion.FrameCount,Value=1,TickStyle=TickStyle.None};
            var transport=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};transport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,WorkspaceAppearance.Pixels(120)));transport.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));transport.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,WorkspaceAppearance.Pixels(85)));transport.Controls.Add(play,0,0);transport.Controls.Add(frame,1,0);transport.Controls.Add(frameText,2,0);transportLayout=transport;
            var right=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3};right.RowStyles.Add(new RowStyle(SizeType.Percent,68));right.RowStyles.Add(new RowStyle(SizeType.Absolute,Math.Max(42,Font.Height+20)));right.RowStyles.Add(new RowStyle(SizeType.Percent,32));right.Controls.Add(views,0,0);right.Controls.Add(transport,0,1);right.Controls.Add(report,0,2);contentLayout=right;
            SourceView.Heading.Height=TargetView.Heading.Height=Math.Max(38,Font.Height*2+12);
            var left=new Panel{Dock=DockStyle.Left,Width=344};left.Controls.Add(fields);
            Label("Fit start and end",true);Label("Use a target pose at either end. The middle stays unchanged.");
            Label("New independent animation name");var name=new TextBox{Name="transitionName",Width=306,Text=motion.Name+" (fit)"};inputs["name"]=name;fields.Controls.Add(name);name.TextChanged+=(s,e)=>Changed();
            foreach(string side in new[]{"entry","exit"})
            {
                Label(side=="entry"?"Start pose":"End pose",true).Name="transition_"+side+"Label";var choice=new ComboBox{Name="transition_"+side,DropDownStyle=ComboBoxStyle.DropDownList,Width=306};choice.Items.Add(side=="entry"?"Keep original start":"Keep original end");choice.Items.AddRange(choices);choice.SelectedIndex=0;inputs[side]=choice;fields.Controls.Add(choice);
                Number(side+"Frame","Reference frame (1-based, fractional)",1,100000,3).Value=1;
                Number(side+"Samples",side=="entry"?"Attack reached at frame":"End transition samples (inclusive)",2,Math.Max(2,motion.FrameCount-1),0).Value=Math.Max(2,Math.Min(6,(motion.FrameCount-1)/3));
                Check(side+"Frozen","Freeze reference pose (zero endpoint velocity)").Checked=true;Number(side+"Rate","Moving reference speed",.001m,4,3).Value=1;
                var preview=new Button{Name="preview_"+side,Text=side=="entry"?"Show start pose":"Show end pose",AutoSize=true};string selected=side;preview.Click+=(s,e)=>PreviewReference(selected);fields.Controls.Add(preview);
                choice.SelectedIndexChanged+=(s,e)=>{Changed();UpdateReference(selected);UpdateRanges();};((NumericUpDown)inputs[side+"Frame"]).ValueChanged+=(s,e)=>PreviewReference(selected);((CheckBox)inputs[side+"Frozen"]).CheckedChanged+=(s,e)=>((NumericUpDown)inputs[selected+"Rate"]).Enabled=!B(selected+"Frozen");((NumericUpDown)inputs[side+"Rate"]).Enabled=false;
            }
            var exitStart=Number("exitStart","Recovery starts at frame",2,Math.Max(2,motion.FrameCount-1),0);
            exitStart.Value=motion.FrameCount-((NumericUpDown)inputs["exitSamples"]).Value+1;
            exitStart.ValueChanged+=(s,e)=>{if(syncRanges)return;syncRanges=true;((NumericUpDown)inputs["exitSamples"]).Value=source.FrameCount-exitStart.Value+1;syncRanges=false;UpdateRanges();};
            ((NumericUpDown)inputs["exitSamples"]).ValueChanged+=(s,e)=>{if(syncRanges)return;syncRanges=true;exitStart.Value=source.FrameCount-((NumericUpDown)inputs["exitSamples"]).Value+1;syncRanges=false;UpdateRanges();};
            ((NumericUpDown)inputs["entrySamples"]).ValueChanged+=(s,e)=>UpdateRanges();
            var both=new Button{Name="transitionBoth",Text="Use for both ends",AutoSize=true};
            both.Click+=(s,e)=>{if(Busy)return;((ComboBox)inputs["exit"]).SelectedIndex=((ComboBox)inputs["entry"]).SelectedIndex;((NumericUpDown)inputs["exitFrame"]).Value=((NumericUpDown)inputs["entryFrame"]).Value;((CheckBox)inputs["exitFrozen"]).Checked=((CheckBox)inputs["entryFrozen"]).Checked;};inputs["both"]=both;fields.Controls.Add(both);
            fields.Controls.Add(ranges);
            Check("boundaryContactRecovery","Adapt hips and legs in transitions").Checked=preserveContactsByDefault;
            Label("Components / existing native bones",true);Check("rotation","Match rotation channels").Checked=true;Check("translation","Match translation channels").Checked=true;Check("scale","Match scale channels").Checked=true;Check("root","Preserve translation on skeleton roots").Checked=true;var equivalents=Check("nearest","Nearest equivalent XYZ angles (180/360)");equivalents.Checked=true;equivalents.AccessibleDescription="Choose a complete orientation-equivalent XYZ triplet near the original join for start and end fitting. Coupled 180-degree changes preserve the pose; individual 360-degree turns are also considered. Reviewed motion-position curves stay unchanged; other controls follow the selected boundary policy.";
            var matchControls=Check("matchReferenceControls","Fit reference controls; retain attack travel");
            matchControls.Enabled=referenceControls.Length>0;matchControls.Checked=matchControls.Enabled;
            matchControls.AccessibleDescription="Boundary-only fitting of reviewed ancestor controls to the reference pose, including their local height and orientation. The reviewed motion-position control retains all attack S/R/T. Missing exact roles disable this option. This explicit policy also allows fitting scene-root translation.";
            Label(referenceControls.Length>0?"Reference controls: "+string.Join(", ",referenceControls)+". Motion-position control remains source-authored.":"Reference control fitting unavailable: review exact motion-position and ancestor-control roles first.");
            Check("contacts","Preserve existing contact trajectories").Checked=preserveContactsByDefault;
            Check("exitContactRecovery","Allow reviewed hip / leg exit recovery");
            Label("Hip / leg adaptation changes reviewed support paths only inside enabled transitions on a non-looping attack. Each reference uses its first frozen pose. Fixed pins and overlapping timed contacts block conflicting changes; the motion-position control keeps the attack curves.");
            var preventDipping=Check("preventFootDipping","Prevent extra foot dipping in transitions");
            var floorHeight=Number("floorHeight","Reviewed native floor height (Y)",-100000,100000,4);floorHeight.Value=0;
            var floorReviewed=Check("floorReviewed","I reviewed the plane and penetration baseline");
            matchControls.CheckedChanged+=(s,e)=>floorReviewed.Checked=false;
            Label("The floor guard prevents additional dipping beyond the original motion or authentic first reference pose at the same selected control policy. It preserves authored endpoint penetration; it does not promise absolute zero penetration. Review the plane and this baseline explicitly before preparing.");
            void FloorState(){preventDipping.Enabled=B("exitContactRecovery")||B("boundaryContactRecovery");floorHeight.Enabled=floorReviewed.Enabled=preventDipping.Enabled&&preventDipping.Checked;}
            ((CheckBox)inputs["exitContactRecovery"]).CheckedChanged+=(s,e)=>{if(!B("exitContactRecovery")&&!B("boundaryContactRecovery"))preventDipping.Checked=false;FloorState();};
            ((CheckBox)inputs["boundaryContactRecovery"]).CheckedChanged+=(s,e)=>{if(B("boundaryContactRecovery"))((CheckBox)inputs["exitContactRecovery"]).Checked=false;else if(!B("exitContactRecovery"))preventDipping.Checked=false;FloorState();};
            ((CheckBox)inputs["exitContactRecovery"]).CheckedChanged+=(s,e)=>{if(B("exitContactRecovery"))((CheckBox)inputs["boundaryContactRecovery"]).Checked=false;};
            preventDipping.CheckedChanged+=(s,e)=>{floorReviewed.Checked=false;FloorState();};
            floorHeight.ValueChanged+=(s,e)=>floorReviewed.Checked=false;FloorState();
            foreach(string side in new[]{"entry","exit"})
            {
                ((ComboBox)inputs[side]).SelectedIndexChanged+=(s,e)=>floorReviewed.Checked=false;
                ((NumericUpDown)inputs[side+"Frame"]).ValueChanged+=(s,e)=>floorReviewed.Checked=false;
                ((NumericUpDown)inputs[side+"Samples"]).ValueChanged+=(s,e)=>floorReviewed.Checked=false;
                ((CheckBox)inputs[side+"Frozen"]).CheckedChanged+=(s,e)=>floorReviewed.Checked=false;
            }
            Label("Current animation pin protections",true);
            fields.Controls.Add(new TextBox{Name="transitionPinReview",Width=306,Height=88,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,Text=pinReview??"Current pin setup is unavailable. Set up this exact animation's controls before contact recovery.",AccessibleName="Exact current animation pins and protection review"});
            var names=sourceModel._linker.BoneCache.Select(b=>b.Name).ToArray();var animated=new HashSet<string>(motion.Children.Select(c=>c.Name));foreach(string bone in names)bones.Items.Add(bone,protectedMotion!=null?!protectedMotion.Contains(bone,StringComparer.Ordinal):animated.Contains(bone));fields.Controls.Add(bones);bones.ItemCheck+=(s,e)=>Changed();
            var all=new Button{Text="All Existing Bones",AutoSize=true};all.Click+=(s,e)=>{for(int i=0;i<bones.Items.Count;i++)bones.SetItemChecked(i,true);};fields.Controls.Add(all);
            Label("Protected frames: 1-based inclusive ranges, e.g. 10-25; 40-45");var protect=new TextBox{Name="transitionProtected",Width=306};inputs["protected"]=protect;fields.Controls.Add(protect);protect.TextChanged+=(s,e)=>Changed();
            Check("binding","I reviewed the target poses and pin protections");Label("Loop flag is retained. Inspect its seam. This authors FK curves; contacts, clearance, joint limits and game-state behavior are not validated.");
            // Keep the everyday flow short. Preserve all native options and names
            // under an explicit disclosure, rather than introducing a second mode.
            var basic=new HashSet<string>{"transition_entryLabel","transition_entry","transition_entrySamplesLabel","transition_entrySamples","preview_entry","transition_exitLabel","transition_exit","transition_exitStartLabel","transition_exitStart","preview_exit","transitionBoth","transitionRanges","transition_boundaryContactRecovery","transition_binding"};
            var allFields=fields.Controls.Cast<Control>().ToArray();
            // Title and introduction remain visible; everything else is classified
            // explicitly by control identity so translated captions do not matter.
            foreach(var control in allFields.Skip(2).Where(c=>!basic.Contains(c.Name)))advanced.Controls.Add(control);
            var ordered=new[]{"transition_entryLabel","transition_entry","transition_entrySamplesLabel","transition_entrySamples","preview_entry","transitionBoth","transition_exitLabel","transition_exit","transition_exitStartLabel","transition_exitStart","preview_exit","transitionRanges","transition_boundaryContactRecovery","transition_binding"};
            for(int i=0;i<ordered.Length;i++){var c=fields.Controls.Cast<Control>().Single(v=>v.Name==ordered[i]);fields.Controls.SetChildIndex(c,i+2);}
            // FlowLayout caches fixed-height native editor requests across font
            // changes. A plain slot gives those editors their actual current
            // height, keeping every enlarged label and editor in its own row.
            foreach(var container in new[]{fields,advanced})foreach(Control editor in container.Controls.Cast<Control>().Where(c=>c is ComboBox||c is UpDownBase).ToArray())
            {
                int index=container.Controls.GetChildIndex(editor);var slot=new Panel{Name="slot_"+editor.Name,Width=editor.Width,Height=editor.Height,Margin=editor.Margin,Tag=editor,TabStop=false};
                editor.MaximumSize=editor.MinimumSize=Size.Empty;editor.Margin=Padding.Empty;editor.Dock=DockStyle.Fill;slot.Controls.Add(editor);container.Controls.Add(slot);container.Controls.SetChildIndex(slot,index);
                editor.Enter+=(s,e)=>fields.ScrollControlIntoView(slot);
            }
            fields.Controls.Add(advancedToggle);fields.Controls.Add(advanced);
            advancedToggle.Click+=(s,e)=>{advanced.Visible=!advanced.Visible;advancedToggle.Text=advanced.Visible?"Hide advanced":"Advanced";fields.ScrollControlIntoView(advancedToggle);if(Candidate!=null)ShowPreparedReport();};
            hints.SetToolTip((Control)inputs["entrySamples"],"Inclusive native display frame where the original attack resumes. This join keeps its original keys and tangents.");
            hints.SetToolTip(exitStart,"Inclusive native display frame where recovery begins. This join keeps the original attack pose and tangents.");
            hints.SetToolTip(both,"Copy the selected start reference to the end. Only the chosen reference pose is used, not idle playback.");
            hints.SetToolTip((Control)inputs["boundaryContactRecovery"],"Explicitly adapt reviewed hips and legs inside the chosen windows. Middle curves, fixed pins and attack motion-position tracks stay protected. Review the preview and limits before adding.");
            ((Control)inputs["binding"]).AccessibleDescription="Review exact native bone bindings, bind space, units and all current pin protections in Advanced. Donor clips must already be retargeted to this target.";
            advancedToggle.AccessibleDescription="Show component masks, native bones, exact reference timing, protected frames, contact policy and floor review. Collapsing changes no settings.";
            foreach(Control c in fields.Controls){c.TabIndex=fields.Controls.GetChildIndex(c);c.Enter+=(s,e)=>fields.ScrollControlIntoView(c);}
            void LayoutFields()
            {
                int width=Math.Max(250,Math.Min(360,ClientSize.Width/3));left.Width=width;int content=width-fields.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-6;
                fields.SuspendLayout();advanced.SuspendLayout();
                foreach(Control c in fields.Controls.Cast<Control>().Concat(advanced.Controls.Cast<Control>()))
                {
                    if(c is Panel slot&&slot.Tag is Control editor)
                    {editor.Font=Font;editor.MaximumSize=editor.MinimumSize=Size.Empty;slot.MaximumSize=new Size(content,100000);slot.Width=content;slot.Height=Math.Max(editor.Height,editor.PreferredSize.Height);continue;}
                    // Fixed-height native editors force MaximumSize.Height to
                    // zero. A width-only maximum makes FlowLayout allocate zero
                    // height even though the control restores its native height.
                    if(c is ComboBox||c is UpDownBase||c is TextBox)
                    {c.MaximumSize=Size.Empty;c.MinimumSize=Size.Empty;c.Width=content;continue;}
                    c.MaximumSize=new Size(content,100000);
                    if(c is Label||c is CheckBox||c is Button)
                    {
                        int inset=c is CheckBox?Font.Height+8:c is Button?20:0;
                        int textWidth=Math.Max(80,content-inset);var measured=TextRenderer.MeasureText(c.Text,c.Font,new Size(textWidth,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding);
                        c.AutoSize=false;c.Width=c is Button?Math.Min(content,measured.Width+24):content;c.Height=measured.Height+(c is Button?16:8);
                    }
                    else {c.Width=content;if(c is NumericUpDown||c is ComboBox||c is TextBox box&&!box.Multiline)c.MinimumSize=new Size(0,c.Font.Height+10);}
                }
                advanced.Width=content;advanced.ResumeLayout(true);fields.ResumeLayout(true);fields.PerformLayout();
            }
            foreach(Control c in advanced.Controls){c.TabIndex=advanced.Controls.GetChildIndex(c);c.Enter+=(s,e)=>fields.ScrollControlIntoView(c);}
            layoutFields=LayoutFields;Resize+=(s,e)=>LayoutFields();LayoutFields();
            var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new Padding(6)};var close=new Button{Text="Close",AutoSize=true};actions.Controls.AddRange(new Control[]{prepare,cancel,review,apply,close});Controls.Add(right);Controls.Add(left);Controls.Add(actions);footer=actions;
            prepare.Click+=async(s,e)=>await PrepareFit();apply.Click+=(s,e)=>{try{if(Busy||Candidate==null||!review.Checked||preparedOptions!=Key(ReadOptions()))return;Apply(ReadOptions(),Candidate);Close();}catch(Exception error){report.Text=error.Message;Discard();}};cancel.Click+=(s,e)=>{generation++;CancelWork?.Invoke();SetBusy(false);Discard();report.Text="Preparation cancelled; no animation changed.";};
            review.CheckedChanged+=(s,e)=>{apply.Enabled=!Busy&&Candidate!=null&&review.Checked;if(review.Checked&&Candidate!=null){referenceSide=null;ShowFrame();}};
            frame.ValueChanged+=(s,e)=>ShowFrame();play.Click+=(s,e)=>{if(Busy)return;if(clock.Enabled)Stop();else{previewStart=frame.Value;previewTime.Restart();clock.Start();play.Text="Pause Preview";}};clock.Tick+=(s,e)=>frame.Value=1+(previewStart-1+(int)(previewTime.Elapsed.TotalSeconds*60))%frame.Maximum;close.Click+=(s,e)=>Close();CancelButton=close;
            FormClosing+=(s,e)=>{generation++;CancelWork?.Invoke();Stop();};FormClosed+=(s,e)=>{clock.Dispose();hints.Dispose();Discard();};
            loading=false;UpdateReference("entry");UpdateReference("exit");UpdateRanges();ShowFrame();report.Text="Choose exact native references, review bindings, and prepare a separate result. No retargeter or contact solver runs during preview playback.";DarkTheme.Apply(this);ApplyTextSize();Shown+=(s,e)=>{SourceView.ShownFrame();TargetView.ShownFrame();};
        }
        internal void ApplyTextSize()
        {
            Font=WorkspaceAppearance.Font;
            foreach(Control c in fields.Controls.Cast<Control>().Concat(advanced.Controls.Cast<Control>()))c.Font=c.Font.Bold?WorkspaceAppearance.BoldFont:Font;
            SourceView.Heading.Font=TargetView.Heading.Font=Font;SourceView.Heading.Height=TargetView.Heading.Height=Math.Max(38,Font.Height*2+12);
            transportLayout.ColumnStyles[0].Width=WorkspaceAppearance.Pixels(120);transportLayout.ColumnStyles[2].Width=WorkspaceAppearance.Pixels(85);
            contentLayout.RowStyles[1].Height=Math.Max(42,Font.Height+20);layoutFields?.Invoke();
        }
        void ShowPreparedReport()
        {
            report.WordWrap=!advanced.Visible;report.Text=advanced.Visible?PreparedReport:"Fit ready · "+source.FrameCount+" frames\r\n"+ranges.Text+"\r\n\r\nReview the preview before adding an independent animation. "+(B("preventFootDipping")?"Reviewed floor guard applied; contacts are not locked.":"Foot contact and clearance are not verified by this fit.")+"\r\nAdvanced shows the full result and limits.";
        }
        readonly Dictionary<Control,Control> regionFocus=new Dictionary<Control,Control>();
        protected override bool ProcessCmdKey(ref Message message,Keys keyData)
        {
            if(keyData==Keys.F6||keyData==(Keys.Shift|Keys.F6))
            {
                Control[] regions={fields,SourceView.Viewport,TargetView.Viewport,report,footer};
                int at=Array.FindIndex(regions,c=>c.ContainsFocus);if(at>=0){Control focused=regions[at];while(focused is ContainerControl container&&container.ActiveControl!=null)focused=container.ActiveControl;var descendants=Descendants(regions[at]);regionFocus[regions[at]]=descendants.FirstOrDefault(c=>c.Focused)??focused;}
                int direction=keyData==Keys.F6?1:-1;
                for(int step=1;step<=regions.Length;step++)
                {var next=regions[(Math.Max(0,at)+direction*step+regions.Length*2)%regions.Length];if(!next.Visible||!next.Enabled)continue;if(regionFocus.TryGetValue(next,out var previous)&&previous.CanFocus&&previous.Visible&&previous.Enabled)previous.Focus();else if(!next.Focus())next.SelectNextControl(null,true,true,true,false);return true;}
            }
            return base.ProcessCmdKey(ref message,keyData);
        }
        static IEnumerable<Control> Descendants(Control root){foreach(Control child in root.Controls){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
        Label Label(string text,bool title=false){var label=new Label{Text=text,Width=306,AutoSize=true,MaximumSize=new Size(306,0),Margin=new Padding(0,8,0,4),Font=title?WorkspaceAppearance.BoldFont:Font};fields.Controls.Add(label);return label;}
        NumericUpDown Number(string key,string text,decimal min,decimal max,int places){Label(text).Name="transition_"+key+"Label";var value=new NumericUpDown{Name="transition_"+key,Width=306,Minimum=min,Maximum=max,DecimalPlaces=places,Increment=places==0?1:.01m};inputs[key]=value;fields.Controls.Add(value);value.ValueChanged+=(s,e)=>Changed();return value;}
        CheckBox Check(string key,string text){var value=new CheckBox{Name="transition_"+key,Text=text,Width=306,AutoSize=true,MaximumSize=new Size(306,0)};inputs[key]=value;fields.Controls.Add(value);value.CheckedChanged+=(s,e)=>Changed();return value;}
        bool B(string key)=>((CheckBox)inputs[key]).Checked;double N(string key)=>(double)((NumericUpDown)inputs[key]).Value;
        TransitionClipChoice Choice(string side)=>((ComboBox)inputs[side]).SelectedItem as TransitionClipChoice;
        static string Key(TransitionFitOptions value)=>new JavaScriptSerializer().Serialize(value);
        internal TransitionFitOptions ReadOptions()
        {
            var ranges=new List<TransitionProtected>();string text=((TextBox)inputs["protected"]).Text;
            foreach(string item in text.Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries)){var split=item.Trim().Split('-');if(split.Length!=2||!double.TryParse(split[0],NumberStyles.Float,CultureInfo.CurrentCulture,out double a)||!double.TryParse(split[1],NumberStyles.Float,CultureInfo.CurrentCulture,out double b)||a<1||b<a||b>source.FrameCount)throw new InvalidOperationException("Protected ranges must be 1-based inclusive frames within this clip, separated by semicolons.");ranges.Add(new TransitionProtected{Start=a-1,End=b-1});}
            return new TransitionFitOptions{Name=((TextBox)inputs["name"]).Text,Entry=Choice("entry")?.Key,Exit=Choice("exit")?.Key,EntryFrame=N("entryFrame")-1,ExitFrame=N("exitFrame")-1,EntrySamples=(int)N("entrySamples"),ExitSamples=(int)N("exitSamples"),EntryFrozen=B("entryFrozen"),ExitFrozen=B("exitFrozen"),EntryRate=N("entryRate"),ExitRate=N("exitRate"),Bones=bones.CheckedItems.Cast<string>().Concat(B("matchReferenceControls")?referenceControls:new string[0]).Distinct(StringComparer.Ordinal).ToArray(),MatchReferenceControls=B("matchReferenceControls"),Components=(B("scale")?1:0)|(B("rotation")?2:0)|(B("translation")?4:0),Protected=ranges.ToArray(),PreserveRootTranslation=B("root"),NearestEquivalentAngles=B("nearest"),BindingReviewed=B("binding"),PreserveContacts=B("contacts"),ExitContactRecovery=B("exitContactRecovery"),BoundaryContactRecovery=B("boundaryContactRecovery"),PreventFootDipping=B("preventFootDipping"),FloorHeight=N("floorHeight"),FloorReviewed=B("floorReviewed")};
        }
        void UpdateRanges()
        {
            if(!inputs.ContainsKey("exitStart"))return;
            bool entry=Choice("entry")!=null,exit=Choice("exit")!=null;int first=entry?(int)N("entrySamples"):1,last=exit?(int)N("exitStart"):source.FrameCount;
            inputs["both"].Enabled=entry;inputs["entrySamples"].Enabled=entry;inputs["exitStart"].Enabled=exit;
            ranges.Text=(entry?"Start: 1–"+first+"\r\n":"")+"Attack unchanged: "+first+"–"+last+(exit?"\r\nEnd: "+last+"–"+source.FrameCount:"")+"\r\nTotal: "+source.FrameCount+" frames · join frames shared"+(first>=last?"\r\nTransitions overlap; shorten a range.":"");
            ranges.AccessibleDescription=ranges.Text+". Inclusive native display frames; unchanged join poses belong to both adjacent sections.";
        }
        void Changed(){if(loading)return;generation++;if(Busy){CancelWork?.Invoke();SetBusy(false);}Discard();report.Text="Settings changed; prepare a new result.";}
        internal void RestoreOptions(TransitionFitOptions value)
        {
            loading=true;
            void Set(string key,double v){var n=(NumericUpDown)inputs[key];n.Value=Math.Max(n.Minimum,Math.Min(n.Maximum,(decimal)v));}
            foreach(string side in new[]{"entry","exit"}){var choice=(ComboBox)inputs[side];string key=side=="entry"?value.Entry:value.Exit;choice.SelectedIndex=Math.Max(0,Array.FindIndex(clips,c=>c.Key==key)+1);UpdateReference(side);Set(side+"Frame",(side=="entry"?value.EntryFrame:value.ExitFrame)+1);Set(side+"Samples",side=="entry"?value.EntrySamples:value.ExitSamples);Set(side+"Rate",side=="entry"?value.EntryRate:value.ExitRate);((CheckBox)inputs[side+"Frozen"]).Checked=side=="entry"?value.EntryFrozen:value.ExitFrozen;}
            ((CheckBox)inputs["matchReferenceControls"]).Checked=value.MatchReferenceControls&&referenceControls.Length>0;
            ((CheckBox)inputs["rotation"]).Checked=(value.Components&2)!=0;((CheckBox)inputs["scale"]).Checked=(value.Components&1)!=0;((CheckBox)inputs["translation"]).Checked=(value.Components&4)!=0;((CheckBox)inputs["root"]).Checked=value.PreserveRootTranslation;((CheckBox)inputs["nearest"]).Checked=value.NearestEquivalentAngles;((CheckBox)inputs["contacts"]).Checked=value.PreserveContacts;((CheckBox)inputs["boundaryContactRecovery"]).Checked=value.BoundaryContactRecovery;((CheckBox)inputs["exitContactRecovery"]).Checked=value.ExitContactRecovery;((CheckBox)inputs["binding"]).Checked=false;
            ((CheckBox)inputs["preventFootDipping"]).Checked=value.PreventFootDipping;Set("floorHeight",value.FloorHeight);((CheckBox)inputs["floorReviewed"]).Checked=false;
            for(int i=0;i<bones.Items.Count;i++)bones.SetItemChecked(i,value.Bones.Contains(Convert.ToString(bones.Items[i])));((TextBox)inputs["protected"]).Text=string.Join("; ",value.Protected.Select(p=>(p.Start+1).ToString(CultureInfo.CurrentCulture)+"-"+(p.End+1).ToString(CultureInfo.CurrentCulture)));
            loading=false;UpdateRanges();Discard();report.Text="Saved fitting settings restored. This prepares from the CURRENT animation, including later pin edits. Review live reference bindings again; missing live references are disabled. Frozen originals remain available in Transition Fit Details.";
        }
        void UpdateReference(string side){var choice=Choice(side);var value=(NumericUpDown)inputs[side+"Frame"];value.Maximum=Math.Max(1,choice?.Clip.FrameCount??1);if(value.Value>value.Maximum)value.Value=value.Maximum;}
        void PreviewReference(string side)
        {
            if(loading||Busy)return;var choice=Choice(side);if(choice==null)return;Stop();review.Checked=false;
            if(protectedMotion==null){referenceSide=null;TargetView.Pose(null,0);TargetView.Heading.Text="REFERENCE BODY · reviewed controls unavailable";report.Text="Set Up Animation Controls for this exact animation before previewing a reference body beneath its source motion controls. No reference root values were substituted.";return;}
            referenceSide=side;ShowFrame();TargetView.ShownFrame();
        }
        void SetBusy(bool value){Busy=value;prepare.Enabled=play.Enabled=frame.Enabled=!value;cancel.Enabled=value;apply.Enabled=!value&&Candidate!=null&&review.Checked;}
        void Stop(){clock.Stop();previewTime.Stop();play.Text="Play Preview";}
        void Discard(){Stop();referenceSide=null;TargetView.Pose(null,0);Candidate?.Dispose();Candidate=null;preparedOptions=null;review.Checked=false;apply.Enabled=false;TargetView.Heading.Text="Fitted result · preview to begin";}
        async Task PrepareFit()
        {
            if(Busy)return;Discard();int token=++generation;SetBusy(true);report.Text="Preparing fixed-duration native FK curves offline...";
            try{var options=ReadOptions();var result=await Prepare(options);if(IsDisposed||token!=generation){result?.Dispose();return;}Candidate=result;preparedOptions=Key(options);ShowPreparedReport();TargetView.Heading.Text="Fitted result · review before adding";frame.Value=1;ShowFrame();SourceView.ShownFrame();TargetView.ShownFrame();}
            catch(Exception error){if(!IsDisposed&&token==generation)report.Text=error.Message;}
            finally{if(!IsDisposed&&token==generation)SetBusy(false);}
        }
        void ShowFrame()
        {
            SourceView.Pose(source,frame.Value);
            if(referenceSide!=null)
            {
                var choice=Choice(referenceSide);
                TargetView.PoseBodyReference(choice.Clip,(float)N(referenceSide+"Frame"),source,frame.Value,B("matchReferenceControls")?protectedMotion.Except(referenceControls,StringComparer.Ordinal):protectedMotion);
                TargetView.Heading.Text="REFERENCE BODY · "+choice.Clip.Name+" frame "+N(referenceSide+"Frame")+" · attack controls frame "+frame.Value;
            }
            else {TargetView.Pose(Candidate,Candidate==null?0:frame.Value);if(Candidate!=null)TargetView.Heading.Text="Fitted result · review before adding";}
            frameText.Text=frame.Value+" / "+source.FrameCount;
        }
    }
}
