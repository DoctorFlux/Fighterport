using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.Diagnostics;
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
        string libraryBefore=Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        string exportBefore=Environment.GetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",Path.Combine(output,"isolated-library"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT",Path.Combine(output,"isolated-export-preferences"));
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
                int directHead = targetRig.Rig.Roles["head"];
                var mappedTarget = new System.Collections.Generic.HashSet<int>(plan.Mapping.Select(map => map.TargetIndex));
                bool CranialDescendant(int joint)
                {
                    for (int parent = joint; parent >= 0; parent = targetRig.Rig.Joints[parent].Parent)
                        if (parent == directHead) return true;
                    return false;
                }
                var headGroups = targetRig.Rig.Surfaces["head"].Points
                    .GroupBy(point => RetargetReviewDiagnostics.ClassifyCranialSkinPoint(
                        point, directHead, mappedTarget, CranialDescendant))
                    .ToDictionary(group => group.Key, group => group.Count());
                Check(headGroups.ContainsKey(0) && headGroups.ContainsKey(1),
                    "Authentic target head-joint skin stays distinct from target-only cranial accessory skin");
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
                // The prepared native result is reviewed before import in both
                // dense and sparse modes. These are private-desktop modal
                // events, with no OS input injection and no document Apply.
                var reviewModes = new System.Collections.Generic.List<object>();
                int undoBefore=form.FighterEditor._saveIndex;
                int frameBefore=form.FighterEditor.CurrentFrame;
                var animationBefore=form.FighterEditor.TargetAnimation;
                foreach(var mode in new[]{RetargetKeyframeMode.Dense,RetargetKeyframeMode.SourceChannelTimes})
                {
                    await CaptureCheckpoint();
                    typeof(ModelForm).GetField("_retargetKeyframeMode",Private).SetValue(form,mode);
                    bool seen=false,directHeadCue=false,accessoryCue=false,handPoseCue=false;
                    long diagnosticMilliseconds=-1;Exception reviewFailure=null;
                    form.RetargetKeyframeReviewReady=window=>
                    {
                        try
                        {
                            seen=true;
                            diagnosticMilliseconds=((RetargetKeyframeReviewDialog)window).ReviewDiagnosticMilliseconds;
                            var cue=SimpleControl<Label>(window,"sourceKeysReviewCue");
                            var report=SimpleControl<TextBox>(window,"sourceKeysReport");
                            var useResult=SimpleControl<Button>(window,"sourceKeysUse");
                            Check(cue.Visible && cue.Text.Contains("unverified") &&
                                report.Text.Contains("bind-sole guide") && report.Text.Contains("No automatic lift") && !useResult.Enabled,
                                mode+": actual native review distinguishes the bind-sole guide from a verified floor and requires explicit review");
                            Check(report.Text.StartsWith("REVIEW NEEDED",StringComparison.Ordinal) &&
                                report.WordWrap && report.ScrollBars==ScrollBars.Vertical,
                                mode+": hand and clearance warnings appear first and wrap within the review report");
                            directHeadCue=report.Text.Contains("Direct head-joint skin");
                            accessoryCue=report.Text.Contains("Head-area skin with target-only descendant influence");
                            Check(directHeadCue && accessoryCue,
                                mode+": clearance report separates direct target head skin from target-only accessory skin");
                            handPoseCue=report.Text.Contains("Unreviewed left.hand skin pose differs") &&
                                report.Text.Contains("Unreviewed right.hand skin pose differs") &&
                                report.Text.Contains("source hand itself reaches") &&
                                report.Text.Contains("not a reviewed floor or planted contact");
                            Check(handPoseCue,
                                mode+": the exact Sonic forward tilt reports both unreviewed hand-pose gaps and source guide crossing");
                            Check((mode!=RetargetKeyframeMode.Dense || ((RetargetKeyframeReviewDialog)window).DenseView==null) &&
                                ((RetargetKeyframeReviewDialog)window).ResultView.Viewport.Visible,
                                mode+": review retains the native target viewport without a competing Dense comparison pane");
                            CaptureRetargetReview((RetargetKeyframeReviewDialog)window,
                                Path.Combine(output,"retarget-clearance-"+mode+".png"));
                            SimpleControl<Button>(window,"sourceKeysCancel").PerformClick();
                        }
                        catch(Exception error){reviewFailure=error;window.Close();}
                    };
                    var bakeTimer=Stopwatch.StartNew();
                    var cancelled=(RetargetResult)Call(form,"BakeWithProgress",source,target,clip);
                    bakeTimer.Stop();
                    form.RetargetKeyframeReviewReady=null;
                    if(reviewFailure!=null)throw reviewFailure;
                    Check(seen && cancelled==null && ReferenceEquals(form.FighterEditor.TargetModel,target) &&
                        ReferenceEquals(form.FighterEditor.TargetAnimation,animationBefore) &&
                        form.FighterEditor._saveIndex==undoBefore && form.FighterEditor.CurrentFrame==frameBefore &&
                        (string)Call(form,"NativeControlHash",target,".mdl0")==targetBefore &&
                        FighterProject.HashFile(sourcePath)==sourceHash && FighterProject.HashFile(motionPath)==motionHash,
                        mode+": cancelling reviewed retarget leaves the native target, frame, Undo and source bytes unchanged");
                    reviewModes.Add(new{Mode=mode.ToString(),CueShown=seen,CancelledWithoutImport=true,
                        DirectHeadCue=directHeadCue,TargetOnlyAccessoryCue=accessoryCue,
                        UnreviewedHandPoseCue=handPoseCue,
                        ReviewDiagnosticMilliseconds=diagnosticMilliseconds,
                        FullPreparationMilliseconds=bakeTimer.ElapsedMilliseconds});
                }
                // A separate positive path presses the actual library Use
                // command. It must import through the existing project Undo
                // transaction after the same reviewed native result returns.
                typeof(ModelForm).GetField("_retargetKeyframeMode",Private).SetValue(form,RetargetKeyframeMode.SourceChannelTimes);
                var registration=UseModelRegistration(sourcePath,motionPath,"reviewed native source");
                typeof(ModelForm).GetMethod("LoadSessionSource",Private,null,
                    new[]{typeof(LibraryRegistration)},null).Invoke(form,new object[]{registration});
                await LibraryQuerySettled(form);
                var item=await UseModelSelectSource(form,(AnimationLibrary)Field(form,"_library"),clipName);
                bool useSeen=false;Exception useFailure=null;
                form.RetargetKeyframeReviewReady=window=>
                {
                    try
                    {
                        useSeen=true;
                        var cue=SimpleControl<Label>(window,"sourceKeysReviewCue");
                        var reviewedCheck=SimpleControl<CheckBox>(window,"sourceKeysReviewed");
                        var use=SimpleControl<Button>(window,"sourceKeysUse");
                        Check(cue.Visible && !use.Enabled,
                            "Use on This Model requires the same unverified clearance review before import");
                        reviewedCheck.Checked=true;
                        Check(use.Enabled,"Explicit review enables the prepared native result");
                        use.PerformClick();
                    }
                    catch(Exception error){useFailure=error;window.Close();}
                };
                var useTimer=Stopwatch.StartNew();
                SimpleControl<Button>(form,"libraryUseOnTarget").PerformClick();
                useTimer.Stop();
                form.RetargetKeyframeReviewReady=null;
                if(useFailure!=null)throw useFailure;
                await SimpleControlsReady(form);
                var imported=form.FighterEditor.SelectedCHR0;
                var asset=project.Animations.Single();
                Check(useSeen && imported!=null && asset.Retarget.Library.ClipId==item.Clip.Id &&
                    ReferenceEquals(form.FighterEditor.TargetModel,target) && imported.FrameCount==clip.FrameCount,
                    "Reviewed Use imports the exact non-idle target revision into the original native editor");
                string content=NativeAnimationOptimization.ContentKey(imported);
                using(var reopened=NodeFactory.FromFile(null,asset.WorkingPath) as CHR0Node)
                    Check(reopened!=null && NativeAnimationOptimization.ContentKey(reopened)==content &&
                        asset.Retarget.ReadKeyframeReport().ProtectedControls.Length>0 &&
                        (string)Call(form,"NativeControlHash",target,".mdl0")==targetBefore,
                        "Imported target reopens with exact native content, protected root evidence and unchanged target binds/weights");
                Call(form,"UndoFighterImport");await SimpleControlsReady(form);
                Check(project.Animations.Count==0 &&
                    (string)Call(form,"NativeControlHash",form.FighterEditor.TargetModel,".mdl0")==targetBefore &&
                    FighterProject.HashFile(sourcePath)==sourceHash && FighterProject.HashFile(motionPath)==motionHash,
                    "One existing project Undo removes the reviewed import without changing original assets or target model");
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
                        RetargetBakePerformed = true, RetargetReviewModes=reviewModes,
                        RetargetImportedAndUndone=true,ImportedContentKey=content,
                        UseElapsedMilliseconds=useTimer.ElapsedMilliseconds,
                        OffscreenReviewCapture=true, HumanUsabilityVerified = false
                    }));
            }
        }
        finally
        {
            if (form != null && !form.IsDisposed) SimpleDisposeForm(form);
            project.Dispose();
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT",libraryBefore);
            Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT",exportBefore);
        }
    }

    // WinForms DrawToBitmap leaves GL viewports blank. Overlay each native
    // ModelPanel's offscreen framebuffer as in the existing source-key review.
    static void CaptureRetargetReview(RetargetKeyframeReviewDialog window,string path)
    {
        using(var bitmap=new Bitmap(window.Width,window.Height))
        {
            window.DrawToBitmap(bitmap,new Rectangle(Point.Empty,window.Size));
            foreach(var view in new[]{window.DenseView,window.ResultView}.Where(view=>view!=null))
            {
                var panel=view.Viewport;
                panel.Capture();panel.Invalidate();panel.Update();
                using(var screenshot=panel.GetScreenshot(panel.ClientRectangle,false))
                using(var graphics=Graphics.FromImage(bitmap))
                {
                    var point=window.PointToClient(panel.PointToScreen(Point.Empty));
                    int border=(window.Width-window.ClientSize.Width)/2;
                    graphics.DrawImageUnscaled(screenshot,point.X+border,
                        point.Y+window.Height-window.ClientSize.Height-border);
                }
            }
            bitmap.Save(path);
        }
    }
}
