using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    // Drives the actual modal controls on the harness's private desktop. This
    // fixture review is session-only; it does not manufacture user approval or
    // claim that a transfer has passed motion/contact acceptance.
    static async Task WorkspaceCranialReview(string[] args, string output)
    {
        if (!PrivateDesktop || !args.Contains("--target-model"))
            throw new InvalidOperationException("Cranial review needs a private desktop and an exact target model.");
        string sourcePath = args[0], motionPath = args[1];
        int targetArg = Array.IndexOf(args, "--target-model");
        if (targetArg + 1 >= args.Length || args[targetArg + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("The exact target model path is missing.");
        string targetPath = args[targetArg + 1];
        string clipName = args.Contains("--clip") ? args[Array.IndexOf(args, "--clip") + 1] : "AttackS3S";
        string sourceHash = FighterProject.HashFile(sourcePath);
        string motionHash = FighterProject.HashFile(motionPath);
        string targetHash = FighterProject.HashFile(targetPath);
        var project = FighterProject.Create("Private cranial mapping review");
        ModelForm form = null;
        try
        {
            form = new ModelForm();
            form.InitializeFighterProject(project);
            form.Show();
            form.ClientSize = new Size(1600, 900);
            Call(form, "ImportFighterModel", targetPath);
            await SimpleControlsReady(form);
            MDL0Node target = (MDL0Node)form.FighterEditor.TargetModel;
            string targetBefore = (string)Call(form, "NativeControlHash", target, ".mdl0");
            Check(target != null && target.AllBones.Count > 0 && form.FighterEditor.ModelPanel.Visible,
                "Exact target remains in the full native Model Previewer");

            using (ResourceNode sourceRoot = NodeFactory.FromFile(null, sourcePath))
            using (ResourceNode motionRoot = NodeFactory.FromFile(null, motionPath))
            {
                MDL0Node source = Walk(sourceRoot).OfType<MDL0Node>()
                    .OrderByDescending(model => model.NumVertices).First();
                CHR0Node clip = Walk(motionRoot).OfType<CHR0Node>().Single(node => node.Name == clipName);
                CranialAnchorProposal proposal = BrawlRetargeter.ProposeCranialAnchor(source,
                    BrawlRetargeter.Characterize(source));
                Check(proposal.Available && proposal.CandidatePatches.Any(p => !p.DirectNeckInfluence) &&
                    proposal.CandidateVertexIndices.Count > 0,
                    "Exact source has reviewable direct-neck skin and separately measured child branches");

                bool modalSeen = false, evidenceSeen = false, checkedReview = false;
                Exception modalError = null;
                BrawlRig reviewed = null;
                using (Modal("Skeleton Mapping", dialog =>
                {
                    try
                    {
                        modalSeen = true;
                        var grid = SimpleControl<DataGridView>(dialog, "skeletonMapping");
                        var head = grid.Rows.Cast<DataGridViewRow>().Single(row =>
                            string.Equals(Convert.ToString(row.Cells[0].Value), "head", StringComparison.Ordinal));
                        var neck = grid.Rows.Cast<DataGridViewRow>().Single(row =>
                            string.Equals(Convert.ToString(row.Cells[0].Value), "neck", StringComparison.Ordinal));
                        var inspect = SimpleControl<Button>(dialog, "inspectCranialSkin");
                        var use = SimpleControl<CheckBox>(dialog, "useCranialSkin");
                        Check(inspect.Enabled && !use.Enabled && !use.Checked &&
                            Convert.ToString(neck.Cells[1].Value) == proposal.NeckBoneName &&
                            string.IsNullOrWhiteSpace(Convert.ToString(head.Cells[1].Value)),
                            "Fresh modal requires evidence inspection before accepting one physical cranial joint");

                        using (Modal("Review native cranial skin", evidence =>
                        {
                            try
                            {
                                var details = evidence.Controls.OfType<TextBox>().Single().Text;
                                Check(details.Contains(proposal.RigAndSkinDigest) &&
                                    details.Contains("DIRECT NECK") && details.Contains("child branch") &&
                                    proposal.CandidatePatches.Where(p => !p.DirectNeckInfluence)
                                        .All(p => details.Contains(p.RootJointName)),
                                    "Evidence dialog shows exact skin digest and all competing child patches");
                                CaptureDialog(evidence, output, "cranial-skin-evidence.png");
                                evidenceSeen = true;
                                evidence.Controls.OfType<Button>().Single(button => button.Text == "Close").PerformClick();
                            }
                            catch (Exception error) { modalError = error; evidence.Close(); }
                        })) inspect.PerformClick();
                        if (modalError != null) throw modalError;
                        Check(evidenceSeen && use.Enabled && !use.Checked,
                            "The review checkbox unlocks only after the evidence dialog is opened");
                        use.Checked = true;
                        checkedReview = true;
                        Check(string.IsNullOrWhiteSpace(Convert.ToString(head.Cells[1].Value)) &&
                            Convert.ToString(neck.Cells[1].Value) == proposal.NeckBoneName &&
                            proposal.CandidatePatches.Where(p => !p.DirectNeckInfluence)
                                .All(p => Convert.ToString(head.Cells[1].Value) != p.RootJointName),
                            "No quill or child branch is mapped as a separate head bone");
                        CaptureDialog(dialog, output, "cranial-skeleton-mapping.png");
                        SimpleControl<Button>(dialog, "applySkeletonMap").PerformClick();
                    }
                    catch (Exception error) { modalError = error; dialog.Close(); }
                })) reviewed = (BrawlRig)Call(form, "CaptureFighterRig", source, true, false);
                if (modalError != null) throw modalError;
                Check(modalSeen && evidenceSeen && checkedReview &&
                    reviewed.CranialReview?.RigAndSkinDigest == proposal.RigAndSkinDigest &&
                    reviewed.Rig.CranialAnchor != null && !reviewed.Rig.Roles.ContainsKey("head") &&
                    reviewed.Rig.CranialAnchor.ParentJoint == reviewed.Rig.Roles["neck"],
                    "Use Mapping admits the reviewed skin once, without an invented head joint");

                var targetRig = BrawlRetargeter.Capture(target);
                BrawlRetargeter.PrepareCranialPair(reviewed, targetRig);
                var plan = new HumanoidRetargeter(reviewed.Rig, targetRig.Rig);
                int sourceNeck = reviewed.Rig.Roles["neck"];
                Check(plan.Mapping.Count(map => map.SourceIndex == sourceNeck) == 1 &&
                    !plan.Mapping.Any(map => map.Role == "head") &&
                    plan.Mapping.All(map => !proposal.CandidatePatches
                        .Where(p => !p.DirectNeckInfluence)
                        .Any(p => map.SourceIndex == p.RootJointIndex && map.Role == "head")),
                    "The neutral retarget plan maps the physical neck once and never maps a quill as head");
                float sampleTime = Math.Min(1.25f, clip.FrameCount - 1);
                var sampled = BrawlRetargeter.Sample(reviewed, clip, sampleTime);
                Check(RigMath.Finite(sampled.CranialPoint()) &&
                    sampled.World.Length == reviewed.Bones.Length &&
                    reviewed.CranialReview.RigAndSkinDigest == proposal.RigAndSkinDigest,
                    "Native source animation sampling retains the reviewed cranial point at a fractional frame");
                Check((string)Call(form, "NativeControlHash", target, ".mdl0") == targetBefore &&
                    FighterProject.HashFile(sourcePath) == sourceHash &&
                    FighterProject.HashFile(motionPath) == motionHash &&
                    FighterProject.HashFile(targetPath) == targetHash,
                    "Modal review and source sampling preserve all original assets and target binds/weights");
                File.WriteAllText(Path.Combine(output, "cranial-workspace.json"),
                    new JavaScriptSerializer().Serialize(new
                    {
                        SourceModelSha256 = sourceHash, SourceMotionSha256 = motionHash,
                        TargetModelSha256 = targetHash, Clip = clipName,
                        proposal.NeckBoneName, proposal.RigAndSkinDigest,
                        ChildPatches = proposal.CandidatePatches.Where(p => !p.DirectNeckInfluence)
                            .Select(p => p.RootJointName).ToArray(),
                        ModalSeen = modalSeen, EvidenceSeen = evidenceSeen,
                        ExplicitReviewChecked = checkedReview, SourceSampleTime = sampleTime,
                        PrivateDesktop, OSInputInjected = false, ScreenCopied = false,
                        RetargetBakePerformed = false, HumanUsabilityVerified = false
                    }));
            }
        }
        finally
        {
            if (form != null && !form.IsDisposed) SimpleDisposeForm(form);
            project.Dispose();
        }
    }
}
