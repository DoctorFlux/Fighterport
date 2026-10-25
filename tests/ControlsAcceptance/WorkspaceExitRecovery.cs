using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    static FrameState RecoverySample(CHR0Node clip, MDL0BoneNode bone, float time)
    {
        var pose = bone.BindState;
        var track = clip.Children.OfType<CHR0EntryNode>().SingleOrDefault(t => t.Name == bone.Name);
        if (track != null) for (int c = 0; c < 9; c++) if (track.KeyArrays[c].KeyCount > 0) pose[c] = track.GetFrameValue(c, time);
        return pose;
    }
    static bool RecoverySamePose(FrameState a, FrameState b) =>
        Enumerable.Range(0, 3).All(c => Math.Abs(a[c] - b[c]) <= LoopScaleTolerance) &&
        Enumerable.Range(6, 3).Sum(c => Math.Pow((double)a[c] - b[c], 2)) <= LoopPositionTolerance * LoopPositionTolerance &&
        LoopIndependentAngle(Enumerable.Range(3, 3).Select(c => (double)a[c]).ToArray(), Enumerable.Range(3, 3).Select(c => (double)b[c]).ToArray()) <= LoopOrientationTolerance;
    static string RecoveryKeys(CHR0Node clip, string[] names) => new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 32000000 }.Serialize(
        clip.Children.OfType<CHR0EntryNode>().Where(t => names.Contains(t.Name, StringComparer.Ordinal)).OrderBy(t => t.Name, StringComparer.Ordinal).Select(t => new {
            t.Name, t.UseModelScale, t.UseModelRotate, t.UseModelTranslate, t.ScaleCompensateApply, t.ScaleCompensateParent, t.ClassicScaleOff,
            Keys = t.KeyArrays.Select(a => a.Entries.Select(k => new { k._index, Value = BitConverter.ToInt32(BitConverter.GetBytes(k._value), 0), Tangent = BitConverter.ToInt32(BitConverter.GetBytes(k._tangent), 0) }).ToArray()).ToArray()
        }).ToArray());
    static bool RecoveryRetainedKeysExact(CHR0Node source, CHR0Node fitted, int first, int last)
    {
        foreach (var before in source.Children.OfType<CHR0EntryNode>())
        {
            var after = fitted.Children.OfType<CHR0EntryNode>().SingleOrDefault(t => t.Name == before.Name);
            if (after == null || before.ScaleCompensateApply != after.ScaleCompensateApply || before.ScaleCompensateParent != after.ScaleCompensateParent || before.ClassicScaleOff != after.ClassicScaleOff) return false;
            for (int c = 0; c < 9; c++)
            {
                var keys = before.KeyArrays[c].Entries.Where(k => k._index >= first && k._index <= last).ToArray();
                var times = keys.Select(k => k._index).ToArray();
                var actual = after.KeyArrays[c].Entries.Where(k => times.Contains(k._index)).ToArray();
                if (keys.Length != actual.Length || !keys.Select((k, i) => k._index == actual[i]._index && BitConverter.ToInt32(BitConverter.GetBytes(k._value), 0) == BitConverter.ToInt32(BitConverter.GetBytes(actual[i]._value), 0) && BitConverter.ToInt32(BitConverter.GetBytes(k._tangent), 0) == BitConverter.ToInt32(BitConverter.GetBytes(actual[i]._tangent), 0)).All(v => v)) return false;
            }
        }
        return true;
    }

    static async Task WorkspaceExitRecovery(string[] args, string output)
    {
        if (!PrivateDesktop || !args.Contains("--retarget-source") || !args.Contains("--target-motion")) throw new InvalidOperationException("Exit recovery requires a private desktop and authentic source/target inputs.");
        string Arg(string name) => args[Array.IndexOf(args, name) + 1];
        var inputs = new[] { args[0], args[1], Arg("--retarget-source"), Arg("--target-motion") }.Distinct().ToDictionary(p => p, FighterProject.HashFile);
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "library"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_PREFERENCES", Path.Combine(output, "preferences"));
        using (var startup = MainForm.StartViewerApplication())
        {
            var form = (ModelForm)Field(MainForm.Instance, "_fighterForm");
            try
            {
                form.ClientSize = new Size(1600, 950); await SimpleControlsReady(form);
                Call(form, "ImportFighterModel", args[0]); Call(form, "ImportFighterAnimations", new[] { new[] { Arg("--target-motion") } }); await SimpleControlsReady(form);
                var editor = form.FighterEditor; var model = (MDL0Node)editor.TargetModel;
                var reference = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c => c.Name == Arg("--reference-clip"));
                Call(form, "BuildLibraryPane"); var library = (AnimationLibrary)Field(form, "_library");
                var donorModel = BrawlLibrarySource.InspectModels(Arg("--retarget-source")).First(m => !m.Shadow);
                library.Register(BrawlLibrarySource.Prepare(new LibraryRegistration { ModelPath = Arg("--retarget-source"), ModelKey = donorModel.ResourceKey, MotionPath = args[1], Character = donorModel.Name, GameVersion = "Local fixture; game build unverified", ApproveAssociation = true }));
                var donor = library.Browse(Arg("--clip")).Single(i => i.Clip.Name == Arg("--clip") && i.Model != null);
                var sourceAsset = await ComparisonRevision(form, library, donor.Clip.Id, null); var source = editor.SelectedCHR0;
                var setup = await Await<ControlReply>(Call(form, "LoadControls"));
                var roots = args.Contains("--reference-controls")?setup.Roles.Where(r=>r.Reviewed&&r.Role=="RootMotion").Select(r=>setup.BoneNames[r.Bone.Value]).ToArray():setup.ProtectedMotionBones.Select(i => setup.BoneNames[i]).ToArray();
                bool floorRecovery = args.Contains("--workspace-foot-recovery");
                if (args.Contains("--moving-root-fixture"))
                {
                    // A separately authored procedural witness, never a production
                    // exception: authentic AttackS3S returns to zero before recovery.
                    var moving = source.Children.OfType<CHR0EntryNode>().First(t => roots.Contains(t.Name) && t.KeyArrays[8].KeyCount > 2);
                    var channel = moving.KeyArrays[8]; while (channel.KeyCount > 0) channel.Remove(channel.Entries.First()._index);
                    moving.SetKeyframe(8, 0, 0, true, true)._tangent = 1.25f / (source.FrameCount - 1);
                    moving.SetKeyframe(8, source.FrameCount - 1, 1.25f, true, true)._tangent = 1.25f / (source.FrameCount - 1);
                    moving.SignalPropertyChange(); source.SignalPropertyChange(); editor.UpdateModel();
                    Check(moving.GetFrameValue(8, source.FrameCount - 1) == 1.25f, "Procedural source-authored protected control stays displaced and moving at the final frame");
                }
                var contacts = NativeTransitionContacts.Resolve(model, sourceAsset, true);
                var body = model._linker.BoneCache.Where(b => (bool)Call(form, "CanEditBodyBone", b.Name)).Select(b => b.Name).ToArray();
                string original = SetNativeContent(source), referenceOriginal = SetNativeContent(reference), rootKeys = RecoveryKeys(source, roots);
                int samples = int.Parse(Arg("--exit-samples")), join = source.FrameCount - samples;
                int originalCount = form.FighterProject.Animations.Count;
                void Configure(TransitionFitDialog window, bool recover)
                {
                    SimpleControl<CheckBox>(window, "transition_matchReferenceControls").Checked = args.Contains("--reference-controls");
                    SimpleControl<ComboBox>(window, "transition_entry").SelectedIndex = 0;
                    var exit = SimpleControl<ComboBox>(window, "transition_exit");
                    exit.SelectedIndex = exit.Items.Cast<object>().Select((item, i) => new { Item = item as TransitionClipChoice, Index = i }).Single(i => ReferenceEquals(i.Item?.Clip, reference)).Index;
                    SimpleControl<NumericUpDown>(window, "transition_exitFrame").Value = 1;
                    SimpleControl<NumericUpDown>(window, "transition_exitSamples").Value = samples;
                    SimpleControl<CheckBox>(window, "transition_exitFrozen").Checked = true;
                    foreach (string key in new[] { "scale", "rotation", "translation", "root", "nearest", "binding" }) SimpleControl<CheckBox>(window, "transition_" + key).Checked = true;
                    SimpleControl<CheckBox>(window, "transition_contacts").Checked = contacts.Length > 0;
                    SimpleControl<CheckBox>(window, "transition_exitContactRecovery").Checked = recover;
                    if (floorRecovery)
                    {
                        SimpleControl<CheckBox>(window, "transition_preventFootDipping").Checked = true;
                        SimpleControl<NumericUpDown>(window, "transition_floorHeight").Value = 0;
                        SimpleControl<CheckBox>(window, "transition_floorReviewed").Checked = true;
                    }
                    var mask = SimpleControl<CheckedListBox>(window, "transitionBones");
                    for (int i = 0; i < mask.Items.Count; i++) mask.SetItemChecked(i, body.Contains(Convert.ToString(mask.Items[i])));
                }
                Exception failure = null; bool finished = false;
                if (contacts.Length > 0)
                {
                    editor.SelectedBone = model._linker.BoneCache.Single(b => b.Name == setup.BoneNames[setup.Roles.Single(r => r.Role == "Hips").Bone.Value]);
                    editor.SetFrame(source.FrameCount - 2); await (Task)Call(form, "AttachControlPin", false); await SimpleControlsReady(form);
                    string pins = (string)Call(form, "ControlState", (string)Call(form, "ControlKey"));
                    form.TransitionFitReady = async window =>
                    {
                        try { Configure(window, true); SimpleControl<Button>(window, "prepareTransition").PerformClick(); await Wait(() => !window.Busy);
                            Check(window.Candidate == null && SimpleControl<TextBox>(window, "transitionReport").Text.ToLowerInvariant().Contains("pin"), "Recovery rejects a fixed existing-bone pin instead of silently releasing it"); }
                        catch (Exception e) { failure = e; } finally { window.Close(); finished = true; }
                    };
                    Call(form, "ShowTransitionFit"); await Wait(() => finished); if (failure != null) throw failure;
                    Check((string)Call(form, "ControlState", (string)Call(form, "ControlKey")) == pins && SetNativeContent(source) == original, "Rejected recovery preserves exact pin metadata and native content");
                    editor.Undo(); await SimpleControlsReady(form); Check(editor.AnimationPins.Length == 0, "Native pin Undo restores the unpinned source for an independently prepared recovery");
                }
                finished = false; CHR0Node fitted = null;
                form.TransitionFitReady = async window =>
                {
                    try
                    {
                        if(args.Contains("--reference-controls"))Check(window.ReadOptions().MatchReferenceControls,"Exact reviewed control roles enable the new reference-control policy on a new fit");
                        Check(!window.ReadOptions().ExitContactRecovery, "Contact recovery is an explicit opt-in; whole-clip preservation remains the default");
                        Configure(window, contacts.Length > 0);
                        if(args.Contains("--reference-controls"))
                        {
                            var match=SimpleControl<CheckBox>(window,"transition_matchReferenceControls");
                            foreach(string key in new[]{"matchReferenceControls","nearest","contacts","exitContactRecovery"})
                            {
                                var action=SimpleControl<CheckBox>(window,"transition_"+key);
                                Check(TextRenderer.MeasureText(action.Text,action.Font).Width+20<=action.ClientSize.Width,"Complete native transition option caption remains visible: "+key);
                            }
                            ((FlowLayoutPanel)match.Parent).ScrollControlIntoView(match);
                            await CaptureWindow(window,Path.Combine(output,"reference-control-options.png"));
                        }
                        var prepare = SimpleControl<Button>(window, "prepareTransition");
                        var report = SimpleControl<TextBox>(window, "transitionReport");
                        if (args.Contains("--moving-root-fixture"))
                        {
                            int mainFrame = editor.CurrentFrame, undoCount = editor._undoSaves.Count, redoCount = editor._redoSaves.Count;
                            int captures = BrawlRetargeter.DiagnosticCaptures, bakes = BrawlRetargeter.DiagnosticBakes;
                            var selectedBone = editor.SelectedBone;
                            WorkspaceRootReferenceSampling(window.TargetView, model, reference, roots);
                            SimpleControl<TrackBar>(window, "transitionFrame").Value = source.FrameCount;
                            SimpleControl<Button>(window, "preview_exit").PerformClick();
                            Check(model._linker.BoneCache.Where(b => roots.Contains(b.Name)).All(b => RecoverySamePose(window.TargetView.Model._linker.BoneCache.Single(v => v.Name == b.Name).FrameState, RecoverySample(source, b, source.FrameCount - 1))), "Reference preview uses the attack's own protected controls instead of the frozen reference root values");
                            Check(model._linker.BoneCache.Where(b => !roots.Contains(b.Name)).All(b => RecoverySamePose(window.TargetView.Model._linker.BoneCache.Single(v => v.Name == b.Name).FrameState, RecoverySample(reference, b, 0))), "Reference preview keeps the exact frozen first body pose under attack-root placement");
                            foreach (int frame in new[] { 1, source.FrameCount / 2, source.FrameCount })
                            {
                                SimpleControl<TrackBar>(window, "transitionFrame").Value = frame;
                                Check(window.TargetView.Model._linker.BoneCache.Where(b => roots.Contains(b.Name)).All(b => RecoverySamePose(b.FrameState, RecoverySample(source, b, frame - 1))), "Scrubbing reference-body preview retains attack-owned controls at display frame " + frame);
                            }
                            var play = (Button)Field(window, "play"); play.PerformClick(); await Task.Delay(90); play.PerformClick();
                            int playingFrame = SimpleControl<TrackBar>(window, "transitionFrame").Value;
                            Check(window.TargetView.Model._linker.BoneCache.All(b => RecoverySamePose(b.FrameState, RecoverySample(roots.Contains(b.Name) ? source : reference, b, roots.Contains(b.Name) ? playingFrame - 1 : 0))), "Reference preview playback follows attack controls while the first body pose remains frozen");
                            Check(BrawlRetargeter.DiagnosticCaptures == captures && BrawlRetargeter.DiagnosticBakes == bakes, "Reference scrubbing and playback perform no rig capture or retarget bake");
                            Check(editor.CurrentFrame == mainFrame && ReferenceEquals(editor.SelectedBone, selectedBone) && editor._undoSaves.Count == undoCount && editor._redoSaves.Count == redoCount && SetNativeContent(source) == original && SetNativeContent(reference) == referenceOriginal, "Reference inspection preserves main native selection, frame, Undo and both exact owned clips");
                            SimpleControl<TrackBar>(window, "transitionFrame").Value = source.FrameCount;
                            await CaptureWindow(window, Path.Combine(output,"reference-body-source-roots.png"));
                            editor.UpdateModel();
                        }
                        if (floorRecovery)
                        {
                            SimpleControl<CheckBox>(window, "transition_floorReviewed").Checked = false; prepare.PerformClick(); await Wait(() => !window.Busy);
                            Check(window.Candidate == null && report.Text.ToLowerInvariant().Contains("review"), "Floor correction rejects an unreviewed plane and authentic penetration baseline");
                            SimpleControl<CheckBox>(window, "transition_floorReviewed").Checked = true;
                            SimpleControl<NumericUpDown>(window, "transition_floorHeight").Value = 1;
                            Check(!window.ReadOptions().FloorReviewed, "Changing floor height clears its prior explicit review");
                            SimpleControl<NumericUpDown>(window, "transition_floorHeight").Value = 0;
                            SimpleControl<CheckBox>(window, "transition_floorReviewed").Checked = true;
                        }
                        if (contacts.Length > 0)
                        {
                            SimpleControl<NumericUpDown>(window, "transition_exitFrame").Value = 2; prepare.PerformClick(); await Wait(() => !window.Busy);
                            Check(window.Candidate == null, "Recovery rejects a reference phase beyond its first native frame");
                            SimpleControl<NumericUpDown>(window, "transition_exitFrame").Value = 1; SimpleControl<CheckBox>(window, "transition_exitFrozen").Checked = false; prepare.PerformClick(); await Wait(() => !window.Busy);
                            Check(window.Candidate == null, "Recovery rejects moving reference playback instead of claiming a frozen first-pose match");
                            SimpleControl<CheckBox>(window, "transition_exitFrozen").Checked = true;
                            SimpleControl<TextBox>(window, "transitionProtected").Text = source.FrameCount + "-" + source.FrameCount; prepare.PerformClick(); await Wait(() => !window.Busy);
                            Check(window.Candidate == null && report.Text.ToLowerInvariant().Contains("protected"), "Recovery retains explicit protected-frame conflicts");
                            SimpleControl<TextBox>(window, "transitionProtected").Clear();
                        }
                        prepare.PerformClick(); await Wait(() => !window.Busy); Check(window.Candidate != null, "The native workflow prepares a separate exact first-pose exit candidate: " + report.Text);
                        if(args.Contains("--reference-controls"))
                        {
                            SimpleControl<CheckBox>(window,"transition_matchReferenceControls").Checked=false;
                            Check(window.Candidate==null&&form.FighterProject.Animations.Count==originalCount,"Changing reference-control policy invalidates the prepared candidate before Apply");
                            SimpleControl<CheckBox>(window,"transition_matchReferenceControls").Checked=true;
                            if(floorRecovery){Check(!window.ReadOptions().FloorReviewed,"Changing control placement clears the previous plane/baseline review");SimpleControl<CheckBox>(window,"transition_floorReviewed").Checked=true;}
                            prepare.PerformClick();await Wait(()=>!window.Busy);
                            Check(window.Candidate!=null,"New control-policy review prepares a fresh independently validated result");
                        }
                        fitted = window.Candidate;
                        Check(form.FighterProject.Animations.Count == originalCount && !SimpleControl<Button>(window, "applyTransition").Enabled, "Preparation requires explicit loss/result review before project publication");
                        Check(model._linker.BoneCache.All(b => RecoverySamePose(RecoverySample(fitted, b, fitted.FrameCount - 1), RecoverySample(roots.Contains(b.Name) ? source : reference, b, roots.Contains(b.Name) ? source.FrameCount - 1 : 0))), "Every body endpoint matches authentic reference frame one while protected controls retain their own source endpoint");
                        Check(rootKeys == RecoveryKeys(fitted, roots), "Recovery retains every protected-root key bit, tangent and native flag");
                        Check(RecoveryRetainedKeysExact(source, fitted, 0, join), "Every retained source-prefix key and paired side keeps exact value/tangent bits and scale-compensation flags");
                        Check(Enumerable.Range(0, join * 4 + 1).All(i => model._linker.BoneCache.All(b => RecoverySamePose(RecoverySample(source, b, i / 4f), RecoverySample(fitted, b, i / 4f)))), "All pre-recovery local poses remain unchanged at independent quarter frames");
                        using (var aligned = RecoveryReferenceUnderSourceRoots(model, source, reference, roots))
                        {
                        model.ApplyCHR(aligned, source.FrameCount); var expected = model._linker.BoneCache.Select(b => b.Matrix).ToArray();
                        model.ApplyCHR(fitted, fitted.FrameCount);
                        Check(model._linker.BoneCache.Select((b, i) => Enumerable.Range(0, 16).All(c => Math.Abs((double)b.Matrix[c] - expected[i][c]) <= LoopPositionTolerance)).All(v => v), "Native world matrices match reference first body pose under unchanged source-authored root placement");
                        if (floorRecovery) RecoveryVerifyNativeFloor(model, source, fitted, aligned, setup, join, output);
                        }
                        editor.UpdateModel();
                        File.WriteAllText(Path.Combine(output, "exit-recovery-report.txt"), report.Text);
                        SimpleControl<TrackBar>(window, "transitionFrame").Value = source.FrameCount;
                        await CaptureWindow(window, Path.Combine(output, "exit-recovery-native-review.png"));
                        if (floorRecovery)
                        {
                            var floor = SimpleControl<CheckBox>(window, "transition_preventFootDipping");
                            var fields = (FlowLayoutPanel)floor.Parent; fields.ScrollControlIntoView(floor);
                            var height = SimpleControl<NumericUpDown>(window, "transition_floorHeight");
                            var reviewed = SimpleControl<CheckBox>(window, "transition_floorReviewed");
                            fields.ScrollControlIntoView(reviewed); fields.PerformLayout();
                            await CaptureWindow(window, Path.Combine(output, "foot-floor-options-native.png"));
                            Check(fields.ClientRectangle.Contains(floor.Bounds) && fields.ClientRectangle.Contains(height.Bounds) && fields.ClientRectangle.Contains(reviewed.Bounds), "Explicit floor controls and baseline review remain visible together in the native scroll panel");
                            Check(TextRenderer.MeasureText(reviewed.Text, reviewed.Font).Width + 18 <= reviewed.ClientSize.Width, "The complete plane and penetration-baseline consent fits beside its native checkbox");
                            reviewed.Focus();
                        }
                        Check(SetNativeContent(source) == original && SetNativeContent(reference) == referenceOriginal, "Review and renderer sampling preserve exact original attack and native reference");
                        SimpleControl<CheckBox>(window, "reviewTransition").Checked = true; SimpleControl<Button>(window, "applyTransition").PerformClick(); fitted = editor.SelectedCHR0;
                    }
                    catch (Exception e) { failure = e; window.Close(); } finally { finished = true; }
                };
                Call(form, "ShowTransitionFit"); await Wait(() => finished); if (failure != null) throw failure; await SimpleControlsReady(form);
                var generated = form.FighterProject.Animations.Last(); string content = SetNativeContent(editor.SelectedCHR0);
                Check(generated.TransitionFit.Algorithm == (floorRecovery ? "contact-floor-recovery-2" : contacts.Length > 0 ? "contact-exit-recovery-3" : "fixed-duration-fk-hermite-2") && generated.TransitionFit.Options.Contains("ExitFrozen\":true"), "Independent result records frozen-first-frame policy and the exact recovery algorithm");
                generated.TransitionFit.Validate();
                if(args.Contains("--reference-controls"))
                {
                    string savedOptions=generated.TransitionFit.Options;
                    var settings=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<TransitionFitOptions>(savedOptions);
                    settings.MatchReferenceControls=false;generated.TransitionFit.Options=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(settings);
                    bool rejected=false;try{generated.TransitionFit.Validate();}catch(InvalidDataException){rejected=true;}finally{generated.TransitionFit.Options=savedOptions;}
                    Check(rejected,"Saved recipe rejects mismatched reference-control options and receipt");
                }
                if (floorRecovery)
                {
                    WorkspaceFootReceiptValidation(generated.TransitionFit);
                    string receipt = generated.TransitionFit.Receipt;
                    generated.TransitionFit.Receipt = receipt.Replace(args.Contains("--reference-controls")?"per-region min(0, original, authentic reference under retained motion-position control)":"per-region min(0, original, authentic reference body under source-root placement)", "unreviewed baseline");
                    bool rejected = false; try { generated.TransitionFit.Validate(); } catch (InvalidDataException) { rejected = true; }
                    generated.TransitionFit.Receipt = receipt;
                    Check(rejected, "Saved recovery validation binds the explicit floor baseline rather than accepting a changed clearance claim");
                    foreach (int text in new[] { 100, 200 }) foreach (int width in new[] { 1050, 1700 })
                    {
                        form.ClientSize = new Size(width, 950); Call(form, "SetWorkspaceTextSize", text); await SimpleControlsReady(form);
                        await CaptureSimpleWorkspace(form, Path.Combine(output, "foot-recovery-workspace-" + width + "-" + text + ".png"));
                        Check(SetNativeContent(editor.SelectedCHR0) == content, "Workspace size/text changes preserve exact recovered clip at " + width + "/" + text);
                    }
                }
                editor.Undo(); await SimpleControlsReady(form); Check(form.FighterProject.Animations.Count == originalCount && ReferenceEquals(editor.SelectedCHR0, source), "One native Undo removes recovery and restores the exact original attack");
                editor.Redo(); await SimpleControlsReady(form); Check(SetNativeContent(editor.SelectedCHR0) == content, "Native Redo restores exact recovery curves without solving again");
                Call(form, "AddSelectedBatchReviewClip"); Call(form, "MarkBatchReview");
                var reviewRow = ReviewRows(form).Single();
                Check(ReviewApproved(reviewRow), "Recovered clip can be reviewed against its exact frozen recovery recipe");
                var frozen = Call(form, "FreezeWorkspaceExport"); string savedReceipt = generated.TransitionFit.Receipt;
                generated.TransitionFit.Receipt += "\n";
                bool staleRecipe = false, staleApproval = false;
                try { ((Action)frozen.GetType().GetField("Validate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).GetValue(frozen))(); } catch (InvalidOperationException) { staleRecipe = true; }
                try { Call(form, "ValidateBatchReviewRows"); } catch (System.Reflection.TargetInvocationException e) { staleApproval = e.InnerException is InvalidOperationException; }
                ((IDisposable)frozen).Dispose(); generated.TransitionFit.Receipt = savedReceipt;
                Check(staleRecipe && staleApproval && !ReviewApproved(reviewRow), "Changed recovery permission/receipt invalidates both frozen export and session review without native content edits");
                Call(form, "MarkBatchReview");
                string exported = Path.Combine(output, "recovery-before-save.chr0"); form.ExportSimpleWorkspaceResult(exported);
                using (var decoded = NodeFactory.FromFile(null, exported) as CHR0Node) Check(decoded != null && SetNativeContent(decoded) == content, "Verified export before project Save preserves exact endpoint keys and metadata");
                string projectPath = Path.Combine(output, "exit-recovery.fpstudio"); form.SaveFighterProject(projectPath);
                using (var fresh = FighterProject.Open(projectPath)) { var saved = fresh.Animations.Single(a => a.Id == generated.Id); saved.TransitionFit.Validate(); Check(saved.TransitionFit.Options == generated.TransitionFit.Options && saved.TransitionFit.Receipt == generated.TransitionFit.Receipt, "Fresh project reopen retains exact explicit recovery policy and frozen references"); }
                Check(inputs.All(i => FighterProject.HashFile(i.Key) == i.Value), "Both authentic model/motion inputs remain byte-for-byte immutable");
            }
            finally { if (!form.IsDisposed) SimpleDisposeForm(form); }
        }
    }
}
