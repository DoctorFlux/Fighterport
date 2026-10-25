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
            menu.DropDownItems.Add("Retarget Options...",null,(s,e)=>ShowFighterRetargetOptions());
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
            using(var dialog=new Form{Text="Retarget Options",ClientSize=new Size(450,172),FormBorderStyle=FormBorderStyle.FixedDialog,
                StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false,ShowInTaskbar=false})
            {
                var mode=new ComboBox{Name="retargetGroundMode",AccessibleName="Ground contact mode",DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new Rectangle(130,14,300,26)};
                mode.Items.AddRange(new object[]{"Auto — detect source contact evidence","Grounded — analyze floor contacts","Airborne — keep floor correction off","Source contacts — preserve small lifts"});
                mode.SelectedIndex=(int)_fighterProject.GroundMode;
                var apply=new Button{Text="Apply",DialogResult=DialogResult.OK,Bounds=new Rectangle(250,128,86,28)};
                var cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(344,128,86,28)};
                dialog.Controls.AddRange(new Control[]{new Label{Text="Ground contacts",Bounds=new Rectangle(14,18,116,22)},mode,
                    new Label{Text="Retargeting bakes FK, proportions and limb IK into native keys.\r\nAuto detects support from source geometry and movement.\r\nPlayback reads the saved keys; no live contact solver runs.",Bounds=new Rectangle(14,54,420,64)},apply,cancel});
                dialog.AcceptButton=apply;dialog.CancelButton=cancel;
                DarkTheme.Apply(dialog);
                if(dialog.ShowDialog(this)==DialogResult.OK)
                {
                    _fighterProject.GroundMode=(GroundContactMode)mode.SelectedIndex;
                    MarkFighterChanged();UpdateFighterStatus();
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
            text.AppendLine("Reach clamps: " + p.ClampedLimbSamples + " sampled limbs · Largest residual: " + p.MaximumLimbResidual.ToString("0.000") + " units");
            text.AppendLine(p.FloorContactsEnabled ? "Ground contact samples: " + p.PlantedFootSamples + " feet · " + p.PlantedHandSamples + " hands · " + p.PlantedBodySamples + " body · " + p.LockedContactSamples + " stationary locks" : "Floor correction disabled for this motion.");
            text.AppendLine("Solver contact residual: " + p.MaximumContactResidual.ToString("0.000") + " · Pelvis adjustment: " + p.MaximumPelvisCorrection.ToString("0.000") + " units");
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
            _fighterProject.ProtectOutputPath(destination);
            if (!(modelEditControl1.TargetModel is MDL0Node target) || BrawlRetargeter.Capture(target).Fingerprint != asset.RigIdentity)
                throw new InvalidOperationException("Open the target model this motion was made for before exporting a game PAC.");
            using (var dialog = new OpenFileDialog {Title = "Choose Target Motion PAC Template", Filter = "Brawl motion PAC (*.pac)|*.pac"})
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return true;
                FighterAsset template = _fighterProject.StageAsset(dialog.FileName, "animation");
                BrawlRetargeter.ExportMotionPac(template.WorkingPath, destination, target, clip);
            }
            RecordLibraryExport(node, destination);
            _fighterStatus.Text = "Exported and reopened " + Path.GetFileName(destination) + " · Target slot: " + clip.Name + " · Original PAC preserved";
            return true;
        }

    }
}
