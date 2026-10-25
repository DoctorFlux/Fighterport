using BrawlCrate.Studio;
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
        clip.Children.OfType<CHR0EntryNode>().Where(t => names.Contains(t.Name, StringComparer.Ordinal)).Select(t => new {
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
                var roots = setup.ProtectedMotionBones.Select(i => setup.BoneNames[i]).ToArray();
                var contacts = NativeTransitionContacts.Resolve(model, sourceAsset, true);
                var body = model._linker.BoneCache.Where(b => (bool)Call(form, "CanEditBodyBone", b.Name)).Select(b => b.Name).ToArray();
                string original = SetNativeContent(source), referenceOriginal = SetNativeContent(reference), rootKeys = RecoveryKeys(source, roots);
                int samples = int.Parse(Arg("--exit-samples")), join = source.FrameCount - samples;
                int originalCount = form.FighterProject.Animations.Count;
                void Configure(TransitionFitDialog window, bool recover)
                {
                    SimpleControl<ComboBox>(window, "transition_entry").SelectedIndex = 0;
                    var exit = SimpleControl<ComboBox>(window, "transition_exit");
                    exit.SelectedIndex = exit.Items.Cast<object>().Select((item, i) => new { Item = item as TransitionClipChoice, Index = i }).Single(i => ReferenceEquals(i.Item?.Clip, reference)).Index;
                    SimpleControl<NumericUpDown>(window, "transition_exitFrame").Value = 1;
                    SimpleControl<NumericUpDown>(window, "transition_exitSamples").Value = samples;
                    SimpleControl<CheckBox>(window, "transition_exitFrozen").Checked = true;
                    foreach (string key in new[] { "scale", "rotation", "translation", "root", "nearest", "binding" }) SimpleControl<CheckBox>(window, "transition_" + key).Checked = true;
                    SimpleControl<CheckBox>(window, "transition_contacts").Checked = contacts.Length > 0;
                    SimpleControl<CheckBox>(window, "transition_exitContactRecovery").Checked = recover;
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
                        Check(!window.ReadOptions().ExitContactRecovery, "Contact recovery is an explicit opt-in; whole-clip preservation remains the default");
                        Configure(window, contacts.Length > 0); var prepare = SimpleControl<Button>(window, "prepareTransition");
                        var report = SimpleControl<TextBox>(window, "transitionReport");
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
                        fitted = window.Candidate;
                        Check(form.FighterProject.Animations.Count == originalCount && !SimpleControl<Button>(window, "applyTransition").Enabled, "Preparation requires explicit loss/result review before project publication");
                        Check(model._linker.BoneCache.All(b => RecoverySamePose(RecoverySample(fitted, b, fitted.FrameCount - 1), RecoverySample(reference, b, 0))), "Every local endpoint including hip/leg and protected controls matches authentic reference frame one");
                        Check(rootKeys == RecoveryKeys(fitted, roots), "Recovery retains every protected-root key bit, tangent and native flag");
                        Check(RecoveryRetainedKeysExact(source, fitted, 0, join), "Every retained source-prefix key and paired side keeps exact value/tangent bits and scale-compensation flags");
                        Check(Enumerable.Range(0, join * 4 + 1).All(i => model._linker.BoneCache.All(b => RecoverySamePose(RecoverySample(source, b, i / 4f), RecoverySample(fitted, b, i / 4f)))), "All pre-recovery local poses remain unchanged at independent quarter frames");
                        model.ApplyCHR(reference, 1); var expected = model._linker.BoneCache.Select(b => b.Matrix).ToArray();
                        model.ApplyCHR(fitted, fitted.FrameCount);
                        Check(model._linker.BoneCache.Select((b, i) => Enumerable.Range(0, 16).All(c => Math.Abs((double)b.Matrix[c] - expected[i][c]) <= LoopPositionTolerance)).All(v => v), "Native evaluated world matrices match first reference pose without root or camera compensation");
                        editor.UpdateModel();
                        File.WriteAllText(Path.Combine(output, "exit-recovery-report.txt"), report.Text);
                        SimpleControl<TrackBar>(window, "transitionFrame").Value = source.FrameCount;
                        await CaptureWindow(window, Path.Combine(output, "exit-recovery-native-review.png"));
                        Check(SetNativeContent(source) == original && SetNativeContent(reference) == referenceOriginal, "Review and renderer sampling preserve exact original attack and native reference");
                        SimpleControl<CheckBox>(window, "reviewTransition").Checked = true; SimpleControl<Button>(window, "applyTransition").PerformClick(); fitted = editor.SelectedCHR0;
                    }
                    catch (Exception e) { failure = e; window.Close(); } finally { finished = true; }
                };
                Call(form, "ShowTransitionFit"); await Wait(() => finished); if (failure != null) throw failure; await SimpleControlsReady(form);
                var generated = form.FighterProject.Animations.Last(); string content = SetNativeContent(editor.SelectedCHR0);
                Check(generated.TransitionFit.Algorithm == (contacts.Length > 0 ? "contact-exit-recovery-1" : "fixed-duration-fk-hermite-1") && generated.TransitionFit.Options.Contains("ExitFrozen\":true"), "Independent result records frozen-first-frame policy and the exact recovery algorithm");
                generated.TransitionFit.Validate();
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
