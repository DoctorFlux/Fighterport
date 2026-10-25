using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

internal static partial class Program
{
    static async Task LoopInspectionNative(ModelForm form, string output)
    {
        var editor = form.FighterEditor;
        await SimpleControlsReady(form);
        form.ClientSize = new System.Drawing.Size(1700, 950); Call(form, "SetWorkspaceTextSize", 100);
        var setup = await Await<ControlReply>(Call(form, "LoadControls"));
        string bodyName = setup.BoneNames[setup.Roles.Single(role => role.Role == "Hips").Bone.Value];
        string protectedName = setup.ProtectedMotionBones == null ? null : setup.ProtectedMotionBones.Select(index => setup.BoneNames[index]).FirstOrDefault();
        var priorAssets = form.FighterProject.Animations.ToArray();
        var priorRows = ReviewRows(form).ToArray();
        var priorClips = priorRows.Select(row => (CHR0Node)Field(row, "Clip")).ToArray();
        var priorContents = priorClips.Select(SetNativeContent).ToArray();
        string input = Path.Combine(output, "procedural-loop-inspection.chr0");
        using (var fixture = new CHR0Node { Name = "Procedural donor-free loop", Version = 5, FrameCount = 2, Loop = true, OriginalPath = "procedural/loop-inspection" })
        {
            fixture.UserEntries.Add(new BrawlLib.SSBB.UserDataClass { Name = "fixture", DataType = BrawlLib.SSBB.UserValueType.String, Entries = new[] { "Original endpoint fixture" } });
            var body = fixture.CreateEntry(bodyName);
            LoopKey(body, 3, 0, 0, .125f); LoopKey(body, 3, 1, 360, -.25f); LoopKey(body, 3, 2, 720, .5f);
            body.ScaleCompensateApply = true;
            if (protectedName != null)
            {
                var motion = fixture.CreateEntry(protectedName);
                LoopKey(motion, 6, 0, 0, .125f); LoopKey(motion, 6, 1, 5, -.25f); LoopKey(motion, 6, 2, 0, .5f);
            }
            LoopKey(fixture.CreateEntry("Unmatched original procedural track"), 3, 0, 42, .75f);
            PreciseCHR0EntryNode.Promote(fixture); fixture.ExportChecked(input);
        }
        string inputHash = FighterProject.HashFile(input);
        Call(form, "ImportFighterAnimations", new[] { new[] { input } });
        await SimpleControlsReady(form);
        var clip = editor.SelectedCHR0;
        var nativeAsset = form.FighterProject.Animations.Single(asset => !priorAssets.Contains(asset));
        Check(nativeAsset.Retarget == null && clip.Name == "Procedural donor-free loop" && clip.FrameCount == 2 && clip.Loop,
            "Loop native: original procedural CHR0 imports without any donor or retarget recipe");
        await Await<ControlReply>(Call(form, "LoadControls"));
        editor.SelectedBone = ((MDL0Node)editor.TargetModel)._linker.BoneCache.Single(bone => bone.Name == bodyName);
        editor.SetFrame(2); await SimpleControlsReady(form);
        Call(form, "AddSelectedBatchReviewClip"); Call(form, "MarkBatchReview");
        var row = ReviewRows(form).Single(item => ReferenceEquals(Field(item, "Clip"), clip));
        Check(ReviewApproved(row), "Loop native: donor-free clip can be reviewed before inspection and project Save");
        var target = editor.TargetModel; var selectedBone = editor.SelectedBone; var children = clip.Children.ToArray();
        string exact = SetNativeContent(clip); int undo = editor._undoSaves.Count;
        bool dirty = (bool)Field(form, "_fighterDirty"), clipDirty = clip.IsDirty;
        int hashes = FighterProject.DiagnosticFileHashes;
        Call(form, "OpenLoopInspection");
        var report = (NativeLoopReport)Field(form, "_loopInspectionReport");
        var bodyReport = report.Bones.Single(bone => bone.Name == bodyName);
        Check(report.Frames == 2 && report.Loop && report.SafeJumps.All(safe => safe) &&
            LoopNear(bodyReport.Endpoints.RotationDegrees, 0, LoopOrientationTolerance) && LoopNear(bodyReport.Closing.RotationDegrees, 0, LoopOrientationTolerance) &&
            bodyReport.Endpoints.Details.Contains("raw ΔEuler° (360, 0, 0)") && bodyReport.Closing.Details.Contains("raw ΔEuler° (-720, 0, 0)"),
            "Loop native: actual target report retains independent 0/360/720 full turns and equivalent endpoint orientations");
        Check(bodyReport.Details.Contains("time 0, value 0, tangent 0.125") && bodyReport.Details.Contains("time 1, value 360, tangent -0.25") && bodyReport.Details.Contains("time 2, value 720, tangent 0.5") &&
            report.Notices.Any(notice => notice.Contains("Unmatched animation track: Unmatched original procedural track")),
            "Loop native: authored boundary tangents and unmatched track are exposed on an imported exact CHR0");
        if (protectedName != null)
        {
            var protectedRow = report.Bones.Single(bone => bone.Name == protectedName);
            Check(protectedRow.Classification == "Protected motion control" && LoopNear(protectedRow.Endpoints.TranslationDistance, 5, LoopPositionTolerance) &&
                LoopNear(bodyReport.Endpoints.TranslationDistance, 0, LoopPositionTolerance),
                "Loop native: reviewed source-local motion-control travel remains separate from unchanged body-local translation");
        }
        Check(editor.CurrentFrame == 2 && ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.TargetModel, target) && ReferenceEquals(editor.SelectedBone, selectedBone) &&
            exact == SetNativeContent(clip) && clip.Children.SequenceEqual(children) && editor._undoSaves.Count == undo && dirty == (bool)Field(form, "_fighterDirty") && clipDirty == clip.IsDirty && ReviewApproved(row) && FighterProject.DiagnosticFileHashes == hashes,
            "Loop native: opening inspection before Save preserves exact document, selection, frame, child ownership, Undo, dirty flags and review approval without hashes");
        Call(form, "JumpLoopInspection", 2);
        Check(editor.CurrentFrame == 3 && LoopNear(editor.SelectedBone.FrameState._rotate._x, 720, LoopOrientationTolerance),
            "Loop native: explicit Closing frame seeks display N+1 and native viewport evaluates authored key time N");
        Call(form, "JumpLoopInspection", 0); Check(editor.CurrentFrame == 1 && editor.SelectedBone.FrameState._rotate._x == 0,
            "Loop native: First explicitly seeks native display frame one");
        Call(form, "JumpLoopInspection", 1); Check(editor.CurrentFrame == 2 && editor.SelectedBone.FrameState._rotate._x == 360,
            "Loop native: Last explicitly seeks native display frame N without changing playback policy");
        Check(exact == SetNativeContent(clip) && clip.Children.SequenceEqual(children) && editor._undoSaves.Count == undo && ReviewApproved(row) && ReferenceEquals(editor.SelectedBone, selectedBone),
            "Loop native: all explicit boundary jumps preserve exact authored data, bone selection, native Undo and session approval");
        SimpleControl<ComboBox>(form, "loopInspectionPairs").SelectedIndex = 1;
        var table = SimpleControl<DataGridView>(form, "loopInspectionBones");
        table.CurrentCell = table.Rows.Cast<DataGridViewRow>().Single(item => ((NativeLoopBone)item.Tag).Name == bodyName).Cells[0];
        Call(form, "RestoreWorkspaceFocus", SimpleControl<TextBox>(form, "loopInspectionDetails"));
        SimpleControl<TextBox>(form, "loopInspectionDetails").Select(0, 0);
        editor.FrameFighterModel();
        await CaptureSimpleWorkspace(form, Path.Combine(output, "loop-native-donor-free.png"));
        string export = Path.Combine(output, "procedural-loop-before-save.chr0");
        form.ExportSimpleWorkspaceResult(export);
        using (var decoded = NodeFactory.FromFile(null, export) as CHR0Node)
            Check(decoded != null && SetNativeContent(decoded) == exact, "Loop native: independently decoded precise export before Save retains the inspected full-turn curves and metadata");
        string saved = Path.Combine(output, "loop-native-reopen.fpstudio"); form.SaveFighterProject(saved);
        Check(SetNativeContent(clip) == exact && ReviewApproved(row), "Loop native: normal project Save preserves inspected exact content and existing session review approval");
        await SimpleControlsReady(form); int cleanFrame = editor.CurrentFrame;
        Call(form, "RefreshLoopInspection");
        Check(!(bool)Field(form, "_fighterDirty") && editor.CurrentFrame == cleanFrame && editor._undoSaves.Count == undo && SetNativeContent(clip) == exact && ReviewApproved(row),
            "Loop native: explicit Refresh of a clean saved document preserves clean state, exact content, frame, Undo and approval");
        using (var fresh = FighterProject.Open(saved))
        using (var reopened = new ModelForm())
        {
            reopened.InitializeFighterProject(fresh); reopened.Show(); await Task.Delay(150); await SimpleControlsReady(reopened);
            reopened.ClientSize = new System.Drawing.Size(1700, 950); Call(reopened, "SetWorkspaceTextSize", 100);
            Check(Field(reopened, "_loopInspectionReport") == null && ReviewRows(reopened).Length == 0 && reopened.FighterEditor.SelectedCHR0.Name == clip.Name,
                "Loop native: fresh project reopen has the saved selected CHR0 but no persisted report, review list or approval");
            Call(reopened, "OpenLoopInspection");
            Check(!(bool)Field(reopened, "_fighterDirty"), "Loop native: opening inspection never dirties a clean freshly reopened project");
            var reopenedReport = (NativeLoopReport)Field(reopened, "_loopInspectionReport");
            var reopenedBody = reopenedReport.Bones.Single(bone => bone.Name == bodyName);
            Check(SetNativeContent(reopened.FighterEditor.SelectedCHR0) == exact && reopenedReport.Frames == 2 && reopenedReport.Loop &&
                reopenedBody.Endpoints.Details.Contains("raw ΔEuler° (360, 0, 0)") && reopenedBody.Closing.Details.Contains("raw ΔEuler° (-720, 0, 0)"),
                "Loop native: fresh donor-free reopen reproduces exact full-turn endpoint observations without inherited state");
            Call(reopened, "JumpLoopInspection", 2);
            Check(reopened.FighterEditor.CurrentFrame == 3, "Loop native: explicit closing jump remains available after project reopen");
            reopened.FighterEditor.FrameFighterModel();
            SimpleControl<ComboBox>(reopened, "loopInspectionPairs").SelectedIndex = 1;
            var reopenedTable = SimpleControl<DataGridView>(reopened, "loopInspectionBones");
            reopenedTable.CurrentCell = reopenedTable.Rows.Cast<DataGridViewRow>().Single(item => ((NativeLoopBone)item.Tag).Name == bodyName).Cells[0];
            Call(reopened, "RestoreWorkspaceFocus", SimpleControl<TextBox>(reopened, "loopInspectionDetails"));
            var authoredDetails = SimpleControl<TextBox>(reopened, "loopInspectionDetails");
            Check(authoredDetails.Text.Contains(reopenedBody.Identity) && authoredDetails.Text.Contains("Rotation X:"), "Loop native: selected body row details display its exact identity and authored rotation keys after reopen");
            authoredDetails.Select(authoredDetails.Text.IndexOf("Rotation X:", StringComparison.Ordinal), 0); authoredDetails.ScrollToCaret();
            await CaptureSimpleWorkspace(reopened, Path.Combine(output, "loop-native-reopened.png"));
            foreach (var prior in priorAssets)
            {
                var original = fresh.Animations.Single(asset => asset.Id == prior.Id);
                reopened.FighterEditor.SelectedCHR0 = (CHR0Node)Call(reopened, "FighterRoot", original);
                await SimpleControlsReady(reopened); reopened.FighterEditor.SetFrame(0);
                Call(reopened, "OpenLoopInspection");
                var nextReport = (NativeLoopReport)Field(reopened, "_loopInspectionReport");
                int index = Array.FindIndex(priorAssets, asset => asset.Id == prior.Id);
                Check(nextReport != null && !nextReport.Loop && nextReport.Frames == priorClips[index].FrameCount &&
                    SetNativeContent(reopened.FighterEditor.SelectedCHR0) == priorContents[index] && reopened.FighterEditor.CurrentFrame == 0 && ReviewRows(reopened).Length == 0,
                    "Loop native: saved original Next result reopens and inspects exact authored content at bind frame without persisted approval");
            }
            SimpleDisposeForm(reopened);
        }
        Call(form, "SelectBatchReview", ReviewRows(form).Single(item => ReferenceEquals(Field(item, "Clip"), clip)));
        await SimpleControlsReady(form); Call(form, "RemoveBatchReview"); Call(form, "UndoFighterImport"); await SimpleControlsReady(form);
        Check(form.FighterProject.Animations.SequenceEqual(priorAssets) && ReviewRows(form).Length == priorRows.Length &&
            priorClips.Select(SetNativeContent).SequenceEqual(priorContents) && FighterProject.HashFile(input) == inputHash,
            "Loop native: only the procedural review row/import is removed; original batch assets, body edits and immutable input survive");
    }
}
