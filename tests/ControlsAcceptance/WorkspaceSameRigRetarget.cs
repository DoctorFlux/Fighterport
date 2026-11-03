using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    // Use the real source-pane command on one exact Brawl rig. The numerical
    // source-versus-result oracle lives in RetargetAcceptance; this case guards
    // the native viewer, selection, timeline and Undo after the UI import.
    static async Task WorkspaceSameRigRetarget(string[] args, string output)
    {
        if (!PrivateDesktop || args.Length < 3)
            throw new InvalidOperationException("Same-rig workspace acceptance requires a private desktop, native model and motion files.");
        string modelPath = args[0], motionPath = args[1];
        string clipName = args.Contains("--clip") ? args[Array.IndexOf(args, "--clip") + 1] : "AttackAirHi";
        string libraryBefore = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        string exportBefore = Environment.GetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "isolated-library"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", Path.Combine(output, "isolated-export-preferences"));
        string modelHash = FighterProject.HashFile(modelPath), motionHash = FighterProject.HashFile(motionPath);
        var project = FighterProject.Create("Exact-rig native workspace regression");
        ModelForm form = null;
        try
        {
            form = new ModelForm(); form.InitializeFighterProject(project); form.Show();
            form.ClientSize = new Size(1600, 900);
            Call(form, "ImportFighterModel", modelPath); await SimpleControlsReady(form);
            var editor = form.FighterEditor;
            var target = (MDL0Node)editor.TargetModel;
            var viewport = editor.ModelPanel;
            var playback = editor.pnlPlayback;
            string targetBefore = (string)Call(form, "NativeControlHash", target, ".mdl0");
            Check(editor.SelectedCHR0 == null && target != null && viewport.Visible,
                "Exact-rig UI starts with the original native target and complete viewport");

            var request = UseModelRegistration(modelPath, motionPath, "same rig");
            typeof(ModelForm).GetMethod("LoadSessionSource", Private, null,
                new[] { typeof(LibraryRegistration) }, null).Invoke(form, new object[] { request });
            await LibraryQuerySettled(form);
            var library = (AnimationLibrary)Field(form, "_library");
            var item = await UseModelSelectSource(form, library, clipName);
            var source = (LibrarySourceLease)Field(form, "_libraryLease");
            string sourceBefore = NativeAnimationOptimization.SourceContentKey(source.Animation);
            string sourceRig = BrawlRetargeter.SkeletonFingerprint(source.Model);
            string targetRig = BrawlRetargeter.SkeletonFingerprint(target);
            Check(sourceRig == targetRig && item.Clip.Name == clipName && source.Reference.ClipId == item.Clip.Id,
                "Session source resolves the exact non-idle animation on the identical target rig");

            bool progress = false, reviewed = false;
            Exception reviewFailure = null;
            form.RetargetKeyframeReviewReady = raw =>
            {
                try
                {
                    reviewed = true;
                    var window = (RetargetKeyframeReviewDialog)raw;
                    Check(!SimpleControl<Button>(window, "sourceKeysUse").Enabled,
                        "Source-key result requires explicit review before native import");
                    SimpleControl<CheckBox>(window, "sourceKeysReviewed").Checked = true;
                    SimpleControl<Button>(window, "sourceKeysUse").PerformClick();
                }
                catch (Exception error) { reviewFailure = error; raw.Close(); }
            };
            using (OnRetargetReady(form, dialog => progress = true))
                SimpleControl<Button>(form, "libraryUseOnTarget").PerformClick();
            if (reviewFailure != null) throw reviewFailure;
            await SimpleControlsReady(form);
            form.RetargetKeyframeReviewReady = null;
            var clip = editor.SelectedCHR0;
            var asset = project.Animations.Single();
            Check(progress && reviewed && clip != null && clip.FrameCount > 5 &&
                ReferenceEquals(editor.TargetModel, target) && ReferenceEquals(editor.ModelPanel, viewport) &&
                ReferenceEquals(editor.pnlPlayback, playback) && asset.Retarget.Library.ClipId == item.Clip.Id,
                "Use on This Model imports one exact-rig result into the same responsive native editor");
            Check(NativeAnimationOptimization.SourceContentKey(source.Animation) == sourceBefore &&
                (string)Call(form, "NativeControlHash", target, ".mdl0") == targetBefore &&
                FighterProject.HashFile(modelPath) == modelHash && FighterProject.HashFile(motionPath) == motionHash,
                "Same-rig Use preserves source curves, original files, target binds and all skin weights");

            // Drive the actual native playback controls without OS input injection.
            playback.numFrameIndex.Value = 3;
            Check(editor.CurrentFrame == 3 && ReferenceEquals(editor.SelectedCHR0, clip),
                "Native timeline frame control seeks the imported animation");
            playback.btnNextFrame.PerformClick();
            Check(editor.CurrentFrame == 4 && ReferenceEquals(editor.SelectedCHR0, clip),
                "Native next-frame control remains responsive after Use");
            editor.FrameFighterModel();
            await CaptureSimpleWorkspace(form, Path.Combine(output, "same-rig-use-on-target.png"));

            var setup = await Await<ControlReply>(Call(form, "LoadControls"));
            var protectedNames = setup.ProtectedMotionBones.Select(index => setup.BoneNames[index]).ToArray();
            var body = clip.Children.OfType<CHR0EntryNode>()
                .Select(entry => target.AllBones.FirstOrDefault(bone => bone.Name == entry.Name))
                .FirstOrDefault(bone => bone != null && !protectedNames.Contains(bone.Name));
            Check(body != null, "Exact-rig result retains an editable animated target body bone");
            editor.SetFrame(3); editor.SelectedBone = body;
            string beforeEdit = SetNativeContent(clip);
            int undoBefore = editor._saveIndex;
            var field = editor.CHR0Editor._transBoxes[3];
            field.Text = (editor.SelectedBone.FrameState._rotate._x + 0.5f).ToString();
            Call(field, "Apply"); await SimpleControlsReady(form);
            string edited = SetNativeContent(clip);
            Check(edited != beforeEdit && editor._saveIndex == undoBefore + 1 &&
                ReferenceEquals(editor.TargetModel, target) && ReferenceEquals(editor.SelectedCHR0, clip),
                "Native body transform creates one editable Undo command on the selected result");
            editor.Undo(); await SimpleControlsReady(form);
            Check(SetNativeContent(clip) == beforeEdit && ReferenceEquals(editor.SelectedCHR0, clip),
                "Native Undo restores the exact imported animation without losing selection");
            editor.Redo(); await SimpleControlsReady(form);
            Check(SetNativeContent(clip) == edited && ReferenceEquals(editor.SelectedCHR0, clip),
                "Native Redo restores the body edit in the original Model Previewer");
            editor.Undo(); await SimpleControlsReady(form);
            Check(SetNativeContent(clip) == beforeEdit &&
                NativeAnimationOptimization.SourceContentKey(source.Animation) == sourceBefore &&
                FighterProject.HashFile(modelPath) == modelHash && FighterProject.HashFile(motionPath) == motionHash,
                "Native edit/Undo leaves immutable source assets and the original exact-rig result unchanged");
            var selectedBone = editor.SelectedBone;
            int selectedFrame = editor.CurrentFrame, undoIndex = editor._saveIndex;
            string projectPath = Path.Combine(output, "same-rig-project.fpstudio");
            form.SaveFighterProject(projectPath);
            using (var reopened = FighterProject.Open(projectPath))
            {
                var saved = reopened.Animations.Single(entry => entry.Id == asset.Id);
                saved.Retarget.ValidateKeyframeEvidence();
                using (var native = NodeFactory.FromFile(null, saved.WorkingPath))
                    Check(saved.Retarget.Mapping.Count() == asset.Retarget.Mapping.Count() &&
                        saved.Retarget.Library.ClipId == item.Clip.Id &&
                        saved.Retarget.Library.ModelId == source.Reference.ModelId &&
                        SetNativeContent(Walk(native).OfType<CHR0Node>().Single()) == beforeEdit,
                        "Project Save/reopen retains every exact-rig binding, source identity and native animation channel");
            }
            Check(ReferenceEquals(editor.TargetModel, target) && ReferenceEquals(editor.SelectedCHR0, clip) &&
                ReferenceEquals(editor.SelectedBone, selectedBone) && editor.CurrentFrame == selectedFrame &&
                editor._saveIndex == undoIndex && SetNativeContent(clip) == beforeEdit,
                "Project Save/reopen leaves the live native viewport, selection, playhead and Undo intact");
            File.WriteAllText(Path.Combine(output, "same-rig-workspace.json"),
                new JavaScriptSerializer().Serialize(new { Clip = clipName, SourceRig = sourceRig,
                    TargetRig = targetRig, SourceModelSha256 = modelHash, SourceMotionSha256 = motionHash,
                    Frames = clip.FrameCount, Mappings = asset.Retarget.Mapping.Count(),
                    UseProgressShown = progress, ReviewShown = reviewed,
                    NativeTimelineSeeked = true, NativeEditUndoRedo = true, OffscreenCapture = "same-rig-use-on-target.png" }));
        }
        finally
        {
            if (form != null)
            {
                form.RetargetKeyframeReviewReady = null;
                if (!form.IsDisposed) SimpleDisposeForm(form);
            }
            project.Dispose();
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", libraryBefore);
            Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", exportBefore);
        }
    }
}
