using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.Internal;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private ToolStripButton _fighterRetarget;
        private ToolStripItem _fighterRetargetDetails;
        private readonly Dictionary<string, string> _fighterRetargetSources = new Dictionary<string, string>();

        private FighterAsset SelectedFighterAsset() => _fighterProject.Animations.FirstOrDefault(a =>
            _fighterRoots.TryGetValue(a.Id, out ResourceNode root) && ReferenceEquals(root, modelEditControl1.TargetAnimation?.RootNode));

        private void InitializeFighterRetarget(ToolStrip toolbar, ToolStripDropDownButton menu)
        {
            _fighterRetarget = AddFighterButton(toolbar, "Retarget to This Model", "fighterRetarget", ChooseFighterRetarget);
            _fighterRetarget.ToolTipText = "Next retargeting: source skeleton mapping, proportions, limb IK and baked ground contacts.";
            _fighterRetarget.AccessibleName = "Retarget to This Model";
            _fighterRetargetDetails = menu.DropDownItems.Add("Retarget Details...", null, (s,e) => ShowFighterRetargetDetails());
            menu.DropDownItems.Add("Retarget Options...",null,(s,e)=>RunFighterAction(ShowFighterRetargetOptions));
            menu.DropDownItems.Add("Advanced Scene / Body Fitting...",null,(s,e)=>RunFighterAction(OpenAdvancedSceneFitting));
            AddFighterButton(toolbar,"Fit / Transitions...","fighterTransitionFit",()=>RunFighterAction(ShowTransitionFit));
            menu.DropDownItems.Add("Fit start and end...",null,(s,e)=>RunFighterAction(ShowTransitionFit));
            menu.DropDownItems.Add("Transition Fit Details / Frozen References...",null,(s,e)=>RunFighterAction(ShowTransitionFitDetails));
            menu.DropDownItems.Add("Animation Slots / Assign to State...",null,(s,e)=>RunFighterAction(ShowAnimationSlots));
            menu.DropDownItems.Add("Contact Placement / Release...",null,(s,e)=>OpenContactPlacement());
            menu.DropDownItems.Add("Review Skeleton Mapping...",null,(s,e)=>RunFighterAction(()=>
            {
                if(!(modelEditControl1.TargetModel is MDL0Node model))throw new InvalidOperationException("Open a model first.");
                CaptureFighterRig(model,true);
            }));
            var groundView=new ToolStripMenuItem("Ground Contact View");
            groundView.DropDownItems.Add("Three-quarter",null,(s,e)=>modelEditControl1.QueueGroundContactView());
            groundView.DropDownItems.Add("Low side",null,(s,e)=>modelEditControl1.QueueGroundContactView(1));
            groundView.DropDownItems.Add("Opposite side",null,(s,e)=>modelEditControl1.QueueGroundContactView(2));
            menu.DropDownItems.Add(groundView);
            menu.DropDownItems.Add("Change Retarget Source...", null, (s,e) =>
            {
                FighterAsset asset = SelectedFighterAsset();
                if (asset != null) { _fighterRetargetSources.Remove(asset.Id); ChooseFighterRetarget(true); }
            });
            _fighterUndo.Text = "Undo";
            _fighterUndo.ToolTipText = "Undo the latest animation/control edit, or the last import when there are no native edits.";
        }

        private void ChooseFighterRetarget() => ChooseFighterRetarget(false);
        private void ShowFighterRetargetOptions()
        {
            if(_retargetBusy||_controlBusy||_workspaceBusy)throw new InvalidOperationException("Finish the current edit before changing retarget options.");
            using(var dialog=new Form{Text="Retarget Options",ClientSize=new Size(590,Math.Max(246,WorkspaceAppearance.Font.Height*11+60)),MinimumSize=new Size(500,270),Font=WorkspaceAppearance.Font,AutoScaleMode=AutoScaleMode.None,
                StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false,ShowInTaskbar=false})
            {
                var mode=new ComboBox{Name="retargetGroundMode",AccessibleName="Ground contact mode",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
                mode.Items.AddRange(new object[]{"Auto — detect source contact evidence","Grounded — analyze floor contacts","Airborne — keep floor correction off","Source contacts — preserve small lifts"});
                mode.SelectedIndex=(int)_fighterProject.GroundMode;
                var keys=new ComboBox{Name="retargetKeyframeMode",AccessibleName="Retarget keyframes",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
                keys.Items.AddRange(new object[]{"Dense Next (default)","Source bone keys","Source channel keys","Interpolated keys"});keys.SelectedIndex=(int)_retargetKeyframeMode;
                keys.AccessibleDescription="Dense keeps original Next. Source bone/channel keys start from donor schedules. Interpolated keys may replace even a source key on every frame with fewer native Hermite keys, after motion checks. Protected source-owned controls remain exact in every mode.";
                var hint=new Label{Name="retargetKeyframeHint",AutoSize=true,Dock=DockStyle.Fill};
                Action update=()=>hint.Text=keys.SelectedIndex==0?"Original Next: complete target-space motion, baked at every frame. Playback reads saved native keys; no solver runs.":
                    keys.SelectedIndex==1?"Start with each source bone's combined key times. Add target keys where needed to preserve motion; review the verified result before adding it.":
                    keys.SelectedIndex==2?"Start with individual source channel times. Keep needed target axes and add keys where motion needs them; the final counts can differ from the source.":
                    "Use fewer native Hermite keys where they reproduce the finished Next motion, even when the source keys every frame. Donor key times are not required; review the measured result.";
                keys.SelectedIndexChanged+=(s,e)=>update();update();
                var apply=new Button{Name="retargetOptionsApply",Text="Apply",DialogResult=DialogResult.OK,AutoSize=true};
                var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel,AutoSize=true};
                var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.RightToLeft};actions.Controls.Add(cancel);actions.Controls.Add(apply);
                var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=5,Padding=new Padding(10)};
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
                for(int row=0;row<5;row++)layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles[3]=new RowStyle(SizeType.Percent,100);
                layout.Controls.Add(new Label{Text="Ground contacts",AutoSize=true,Padding=new Padding(0,6,6,0)},0,0);layout.Controls.Add(mode,1,0);
                layout.Controls.Add(new Label{Text="Keyframes",AutoSize=true,Padding=new Padding(0,6,6,0)},0,1);layout.Controls.Add(keys,1,1);
                layout.Controls.Add(hint,0,2);layout.SetColumnSpan(hint,2);
                var protection=new Label{AutoSize=true,Dock=DockStyle.Fill,Text="Source-owned motion controls retain their existing exact local curves in every mode. Keyframe choice is session-only; the result keeps its chosen recipe."};layout.Controls.Add(protection,0,3);layout.SetColumnSpan(protection,2);
                layout.Controls.Add(actions,0,4);layout.SetColumnSpan(actions,2);dialog.Controls.Add(layout);
                dialog.Resize+=(s,e)=>hint.MaximumSize=protection.MaximumSize=new Size(Math.Max(200,dialog.ClientSize.Width-30),0);
                dialog.AcceptButton=apply;dialog.CancelButton=cancel;
                DarkTheme.Apply(dialog);WorkspaceAppearance.SizeDialog(dialog);
                hint.MaximumSize=protection.MaximumSize=new Size(Math.Max(200,dialog.ClientSize.Width-30),0);
                if(dialog.ShowDialog(this)==DialogResult.OK)
                {
                    bool groundChanged=_fighterProject.GroundMode!=(GroundContactMode)mode.SelectedIndex;
                    _fighterProject.GroundMode=(GroundContactMode)mode.SelectedIndex;
                    _retargetKeyframeMode=(RetargetKeyframeMode)keys.SelectedIndex;
                    if(groundChanged)MarkFighterChanged();UpdateFighterStatus();
                }
            }
        }
        private void ChooseFighterRetarget(bool chooseAgain)
        {
            RunFighterAction(() =>
            {
                if (!(modelEditControl1.TargetModel is MDL0Node target) || !(modelEditControl1.TargetAnimation is CHR0Node))
                    throw new NotSupportedException("Open a Brawl target model and select a CHR0 body animation first.");
                CaptureFighterRig(target); // Characterize/review the actual target before choosing its donor.
                FighterAsset asset = SelectedFighterAsset();
                if (asset == null || asset.Retarget != null) throw new InvalidOperationException("Select the original source animation to retarget.");
                _fighterRetargetSources.TryGetValue(asset.Id, out string path);
                if (path == null && !chooseAgain)
                    path = _fighterProject.Animations.LastOrDefault(a => a.Retarget?.SourceAnimationId == asset.Id)?.Retarget.SourceModelPath;
                if (chooseAgain || string.IsNullOrEmpty(path) || !File.Exists(path))
                    using (var dialog = new OpenFileDialog {Title = "Choose Animation Source Model", Filter = "Brawl source model (*.pac;*.pcs;*.brres;*.mdl0)|*.pac;*.pcs;*.brres;*.mdl0"})
                    {
                        if (dialog.ShowDialog(this) != DialogResult.OK) return;
                        path = dialog.FileName;
                    }
                Cursor previous = Cursor; Cursor = Cursors.WaitCursor;
                try { RetargetFighterAnimation(path); }
                finally { Cursor = previous; }
            });
        }

        internal FighterAsset RetargetFighterAnimation(string sourceModelPath)
        {
            if(_controlBusy||_pinGesture!=null)throw new InvalidOperationException("Finish the current animation-control edit before retargeting.");
            if (!(modelEditControl1.TargetModel is MDL0Node target) || !(modelEditControl1.TargetAnimation is CHR0Node clip))
                throw new NotSupportedException("Retargeting requires a target MDL0 and a selected CHR0 body animation.");
            FighterAsset motion = SelectedFighterAsset();
            if (motion == null || motion.Retarget != null) throw new InvalidOperationException("Select the original source animation to retarget.");
            modelEditControl1.StopAnim();
            int frame = Math.Max(1, modelEditControl1.CurrentFrame);
            FighterAsset donor = _fighterProject.StageAsset(sourceModelPath, "model");
            using (ResourceNode donorRoot = NodeFactory.FromFile(null, donor.WorkingPath))
            {
                if (donorRoot == null) throw new InvalidDataException("The source model could not be decoded.");
                var models = ModelPanel.CollectModels(donorRoot);
                if (models.Count == 0) throw new InvalidDataException("No supported source model was found.");
                MDL0Node source = ChooseFighterModel(models, null) as MDL0Node;
                if (source == null) throw new NotSupportedException("Choose a native Brawl MDL0 source model.");
                using (RetargetResult result = BakeWithProgress(source, target, clip))
                {
                    if(result==null)return null;
                    string label = clip.Name + " (" + result.Plan.Source.Profile + " to " + result.Plan.Target.Profile + ").chr0";
                    FighterAsset generated = _fighterProject.CreateAnimation(label, result.Animation.ExportChecked);
                    generated.RigIdentity = result.TargetRig;
                    generated.Retarget = new RetargetProvenance {
                        Algorithm = HumanoidRetargeter.Algorithm, MotionControlPolicy = HumanoidRetargeter.MotionControlPolicy, SourceModelPath = donor.SourcePath, SourceModelSha256 = donor.SourceSha256,
                        SourceRig = result.SourceRig, SourceAnimationId = motion.Id, SourceClip = clip.Name + "|" + clip.TreePath,
                        FromProfile = result.Plan.Source.Profile, ToProfile = result.Plan.Target.Profile,
                        LegScale = result.Plan.LegScale, ArmScale = result.Plan.ArmScale,
                        MaximumLimbResidual = result.MaximumLimbResidual, ClampedLimbSamples = result.ClampedLimbSamples,
                        FloorContactsEnabled = result.Contacts.FloorEnabled, PlantedFootSamples = result.Contacts.PlantedFootSamples,
                        GroundMode = _fighterProject.GroundMode,
                        SuspendedSupportEnabled=result.Contacts.Suspended!=null,
                        PlantedHandSamples = result.Contacts.PlantedHandSamples, LockedContactSamples = result.Contacts.LockedSamples,
                        PlantedBodySamples = result.Contacts.PlantedBodySamples,
                        MaximumContactResidual = result.MaximumContactResidual, MaximumPelvisCorrection = result.MaximumPelvisCorrection,
                        IgnoredSourceBones = result.IgnoredSourceBones,
                        Mapping = result.Plan.Mapping.Select(m => new RetargetBoneBinding {Role = m.Role, Source = m.Source, Target = m.Target}).ToList()
                    };
                    generated.Retarget.RecordKeyframeEvidence(result);
                    ResourceNode root = FighterRoot(generated); // Prove native decoding before changing the visible project.
                    RememberFighterImport();
                    _fighterProject.Animations.Add(generated);
                    _fighterProject.SelectedAnimation = FighterAnimationKey(generated,root);
                    _fighterProject.Frame = Math.Min(frame, result.Animation.FrameCount);
                    _fighterRetargetSources[motion.Id] = sourceModelPath;
                    RebuildFighterScene();
                    if(result.Contacts.FloorEnabled)modelEditControl1.QueueGroundContactView();
                    else modelEditControl1.QueueFrameFighterModel();
                    MarkFighterChanged(); UpdateFighterStatus();
                    return generated;
                }
            }
        }

        private void UpdateFighterRetargetStatus()
        {
            if (_fighterRetarget == null) return;
            FighterAsset asset = SelectedFighterAsset();
            _fighterRetarget.Enabled = modelEditControl1.TargetModel is MDL0Node && modelEditControl1.TargetAnimation is CHR0Node && asset != null && asset.Retarget == null;
            _fighterRetargetDetails.Enabled = asset?.Retarget != null;
        }
        private void AppendFighterRetargetStatus()
        {
            RetargetProvenance p = SelectedFighterAsset()?.Retarget;
            if (p != null) _fighterStatus.Text += " · " + p.FromProfile + " → " + p.ToProfile + " · " + p.Mapping.Count + " mapped bones"+(p.ReviewDraft?" · REVIEW DRAFT":"");
        }
        private void ShowFighterRetargetDetails()
        {
            RetargetProvenance p = SelectedFighterAsset()?.Retarget;
            if (p == null) return;
            var text = new StringBuilder();
            text.AppendLine(p.FromProfile + " → " + p.ToProfile + " · " + p.Mapping.Count + " mapped bones");
            text.AppendLine("Retargeter: "+p.Algorithm);
            if(p.KeyframeMode!=RetargetKeyframeMode.Dense)
            {
                text.AppendLine("Historical prepared retargeting evidence — later native edits/optimization do not rewrite this receipt.");
                text.AppendLine(p.ReadKeyframeReport().Describe());
                text.AppendLine("The original dense Next solver statistics are retained separately as historical reference evidence.");
            }
            if(p.ReviewDraft)text.AppendLine("REVIEW DRAFT — automatic contacts and motion/contact acceptance remain unresolved.");
            if(!string.IsNullOrEmpty(p.EngineReceipt))
            {
                text.AppendLine("Source model: "+Path.GetFileName(p.SourceModelPath));
                text.AppendLine("Body animation only. Automatic contacts are disabled; native output is separate from motion quality and game testing.");
                try
                {
                    var report=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=8*1024*1024,RecursionLimit=200}.Deserialize<RetargetReportView>(p.EngineReceipt);
                    if(report.Validation!=null)text.AppendLine("Measured penetration: "+report.Validation.Penetration.ToString("G5")+" / "+report.Validation.PenetrationBound.ToString("G5")+"; separation: "+report.Validation.Separation.ToString("G5")+" / "+report.Validation.PositionBound.ToString("G5"));
                    foreach(var issue in report.Diagnostics??new RetargetIssueView[0])text.AppendLine("• "+issue.Message);
                    foreach(var loss in report.Losses??new string[0])text.AppendLine("Channel loss: "+loss);
                }
                catch(ArgumentException){text.AppendLine("The saved detailed report could not be read; its original data remains in the project.");}
            }
            else
            {
            text.AppendLine("Leg ratio: " + p.LegScale.ToString("0.000") + " · Arm ratio: " + p.ArmScale.ToString("0.000"));
            text.AppendLine("Source model: " + Path.GetFileName(p.SourceModelPath));
            text.AppendLine("Ground mode: "+p.GroundMode);
            if(p.KeyframeMode==RetargetKeyframeMode.Dense)
            {
            text.AppendLine("Reach clamps: " + p.ClampedLimbSamples + " sampled limbs · Largest residual: " + p.MaximumLimbResidual.ToString("0.000") + " units");
            text.AppendLine(p.FloorContactsEnabled ? "Ground contact samples: " + p.PlantedFootSamples + " feet · " + p.PlantedHandSamples + " hands · " + p.PlantedBodySamples + " body · " + p.LockedContactSamples + " stationary locks" : "Floor correction disabled for this motion.");
            text.AppendLine("Solver contact residual: " + p.MaximumContactResidual.ToString("0.000") + " · Pelvis adjustment: " + p.MaximumPelvisCorrection.ToString("0.000") + " units");
            }
            else text.AppendLine("Changed key-layout curves have no qualified contact paths. Original dense reference contact residual: "+p.DenseReferenceStatistics.MaximumContactResidual.ToString("0.000")+" (historical only).");
            text.AppendLine("Target-only helpers keep their own bind transforms. Root motion is baked and follows in preview.");
            text.AppendLine("Body/skeletal channels only. Facial, material and visibility motions are not transferred.");
            text.AppendLine("Inspect the generated motion and contacts before use; native saving does not establish game or general motion-quality acceptance.");
            foreach(string bone in p.IgnoredSourceBones??new string[0])text.AppendLine("Untransferred source track: "+bone);
            }
            text.AppendLine();
            foreach (RetargetBoneBinding bone in p.Mapping) text.AppendLine(bone.Source + " → " + bone.Target + "  (" + bone.Role + ")");
            using (var form = new Form {Text = "Retarget Details", Size = new Size(680, 530), StartPosition = FormStartPosition.CenterParent})
            {
                form.Controls.Add(new TextBox {Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical,
                    Text = text.ToString(), Font = new Font(FontFamily.GenericMonospace, 9)});
                var save=new Button{Text="Save Result As...",Dock=DockStyle.Bottom,Height=30};
                save.Click+=(s,e)=>
                {
                    using(var dialog=new SaveFileDialog{Title="Save Retargeted Result",Filter="Native animation (*.chr0)|*.chr0|Fighter project (*.fpstudio)|*.fpstudio",DefaultExt="chr0",AddExtension=true,FileName=modelEditControl1.TargetAnimation.Name+".chr0"})
                    {
                        if(dialog.ShowDialog(form)!=DialogResult.OK)return;
                        RunFighterAction(()=>{if(Path.GetExtension(dialog.FileName).Equals(".fpstudio",StringComparison.OrdinalIgnoreCase))SaveFighterProject(dialog.FileName);else ExportFighterResource(dialog.FileName);});
                    }
                };
                form.Controls.Add(save);form.Controls.SetChildIndex(save,form.Controls.Count-1);DarkTheme.Apply(form);
                form.Shown+=(s,e)=>{var report=form.Controls.OfType<TextBox>().Single();report.SelectionStart=0;report.SelectionLength=0;};
                form.ShowDialog(this);
            }
        }

        private sealed class RetargetIssueView {public string Message {get;set;}}
        private sealed class RetargetValidationView {public float Penetration {get;set;}public float PenetrationBound {get;set;}public float Separation {get;set;}public float PositionBound {get;set;}}
        private sealed class RetargetReportView {public RetargetValidationView Validation {get;set;}public RetargetIssueView[] Diagnostics {get;set;}public string[] Losses {get;set;}}

        private bool TryExportFighterRetargetPac(ResourceNode node, string destination)
        {
            FighterAsset asset = _fighterProject.Animations.FirstOrDefault(a =>
                _fighterRoots.TryGetValue(a.Id, out ResourceNode root) && ReferenceEquals(root, node?.RootNode));
            if (!(node is CHR0Node clip) || asset?.Retarget == null || !string.Equals(Path.GetExtension(destination), ".pac", StringComparison.OrdinalIgnoreCase)) return false;
            RequireSavedAnimationExportTarget(modelEditControl1.TargetModel as MDL0Node);
            destination = Path.GetFullPath(destination);
            ProtectVerifiedExport(destination);
            if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("Choose a new PAC filename. Existing destinations are protected.");
            if (!(modelEditControl1.TargetModel is MDL0Node target) || BrawlRetargeter.SkeletonFingerprint(target) != asset.RigIdentity)
                throw new InvalidOperationException("Open the target model this motion was made for before exporting a game PAC.");
            using (var dialog = new OpenFileDialog {Title = "Choose Target Motion PAC Template", Filter = "Brawl motion PAC (*.pac)|*.pac"})
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return true;
                string template = Path.GetFullPath(dialog.FileName), templateHash = FighterProject.HashFile(template);
                modelEditControl1.StopAnim(); FinishControlCurveEdit();
                using (var frozen = FreezeOwnedWorkspaceExport(asset, clip, false))
                {
                    var settings = new NativeExportOptimizationPreferences().Read();
                    PrepareWorkspaceExportWithProgress(frozen, settings, this);
                    void Validate()
                    {
                        frozen.Validate(); ProtectVerifiedExport(destination);
                        if (FighterProject.HashFile(template) != templateHash) throw new IOException("The target motion template changed. Export again from its current contents.");
                    }
                    Validate();
                    BrawlRetargeter.ExportMotionPac(template, destination, target, frozen.Candidate, Validate, frozen.PreparedEncodedSha256, null);
                    RecordLibraryExport(node, destination);
                    _fighterStatus.Text = "Exported verified " + Path.GetFileName(destination) + " · " + new FileInfo(destination).Length +
                        " container bytes · CHR0 " + frozen.MasterBytes + " → " + frozen.ExportBytes + " bytes · " + frozen.EffectiveExportPolicy + " · master and template preserved";
                }
            }
            return true;
        }

    }
}
