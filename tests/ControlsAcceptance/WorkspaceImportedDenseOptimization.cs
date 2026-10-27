using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    // A deliberately different stored terminal sample makes this an authored
    // imported-dense fixture, rather than a new clamp-based codec proposal.
    static async Task WorkspaceImportedDenseOptimization(ModelForm form, FighterProject project,
        MDL0Node model, string denseBoneName, string output)
    {
        string stage = Path.Combine(output, "imported-dense-workflow"); Directory.CreateDirectory(stage);
        var editor = form.FighterEditor;
        var denseBone = model.AllBones.Single(b => b.Name == denseBoneName);
        var otherBone = model.AllBones.First(b => b.Name != denseBoneName && b.Parent is MDL0BoneNode &&
            !b.Children.OfType<MDL0BoneNode>().Any());
        string input = Path.Combine(stage, "mixed-imported-dense-and-precise.chr0");
        var encoded = IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1,
            Enumerable.Repeat(0, 16).Concat(new[] { 255 }).ToArray(), 16, 1, .01f);
        using (var seed = DenseOwnedClip(16, false, 4, encoded))
        {
            seed.Name = "Independent mixed imported dense workflow";
            var dense = (CHR0EntryNode)seed.Children[0]; dense.Name = denseBone.Name;
            dense.ScaleCompensateApply = denseBone.SegScaleCompApply; dense.ScaleCompensateParent = denseBone.SegScaleCompParent;
            dense.ClassicScaleOff = !denseBone.ClassicScale;
            var channels = new IndexedCHR0CurveStorage[9]; channels[3] = encoded;
            PreciseCHR0EntryNode.RestoreStorage(dense, new IndexedCHR0EntryStorage(channels));
            var precise = seed.CreateEntry(otherBone.Name);
            precise.UseModelScale = precise.UseModelTranslate = true; precise.UseModelRotate = false;
            precise.ScaleCompensateApply = otherBone.SegScaleCompApply; precise.ScaleCompensateParent = otherBone.SegScaleCompParent;
            precise.ClassicScaleOff = !otherBone.ClassicScale;
            for (int axis = 4; axis < 6; axis++) precise.Keyframes.SetFrameValue(axis, 0, otherBone.BindState[axis], true)._tangent = 0;
            for (int frame = 0; frame < 16; frame++) precise.Keyframes.SetFrameValue(3, frame, .5f + frame / 128f, true)._tangent = 1f / 128;
            precise.Keyframes.PreserveInterpolation = true; PreciseCHR0EntryNode.Promote(seed); seed.ExportChecked(input);
        }
        string originalHash = FighterProject.HashFile(input);
        var rawInput = DenseReadCompletePayloads(File.ReadAllBytes(input)).Single();
        Check(rawInput.EncodedValues.Length == 17 && rawInput.EncodedValues[15] == 1 && rawInput.EncodedValues[16] > 3.5f,
            "Imported dense workflow independently identifies the complete non-loop terminal sample beyond the native constant projection");
        Call(form, "ImportFighterAnimations", new[] { new[] { input } }); await SimpleControlsReady(form);
        var clip = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(c => c.Name == "Independent mixed imported dense workflow");
        editor.SelectedCHR0 = clip; await SimpleControlsReady(form); editor.SelectedBone = denseBone; editor.SetFrame(3); editor.UpdateModel();
        string before = SetNativeContent(clip), beforeStorage = NativeAnimationOptimization.StorageKey(clip), camera = CompactCamera(form);
        string denseKey = PreciseCHR0EntryNode.CaptureStorage(clip.Children.OfType<CHR0EntryNode>().Single(e => e.Name == denseBone.Name))[3].ContentKey;
        var siblings = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Where(c => !ReferenceEquals(c, clip)).ToDictionary(c => c, SetNativeContent);
        int beforeKeys = OptimizationKeyCount(clip), frameBefore = editor.CurrentFrame, assets = project.Animations.Count, cursor = editor._saveIndex;
        string result = null, resultStorage = null;
        bool DenseRetained(CHR0Node animation)
        {
            var entry = animation.Children.OfType<CHR0EntryNode>().Single(e => e.Name == denseBone.Name);
            return PreciseCHR0EntryNode.CaptureStorage(entry)?[3]?.ContentKey == denseKey && entry.KeyArrays[3].KeyCount == 16;
        }
        async Task Open(Func<AnimationOptimizationDialog, Task> exercise)
        {
            bool finished = false; Exception failure = null;
            form.OptimizationReady = async window =>
            {
                try { await exercise(window); } catch (Exception error) { failure = error; }
                finally { if (!window.IsDisposed) window.Close(); finished = true; }
            };
            Call(form, "ShowAnimationOptimization");
            await GameLibraryWait(() => finished, "Mixed imported-dense optimization did not complete.", 180);
            form.OptimizationReady = null; if (failure != null) throw failure;
            await SimpleControlsReady(form);
        }
        await Open(async window =>
        {
            Check(!window.ReadHighQuality() && !window.ReadCompactStorage(), "Mixed imported-dense optimization retains the ordinary Exact defaults");
            await window.Analyze();
            string report = SimpleControl<TextBox>(window, "optimizationReport").Text;
            File.WriteAllText(Path.Combine(stage, "mixed-exact-report.txt"), report);
            Check(window.Candidate != null && window.PreparedCanApply && DenseRetained(window.Candidate) &&
                window.Candidate.Children.OfType<CHR0EntryNode>().Single(e => e.Name == otherBone.Name).KeyArrays[3].KeyCount == 2 && OptimizationKeyCount(window.Candidate) < beforeKeys,
                "Exact Analyze preserves the complete imported dense bone while independently reducing the other target bone's linear I12 knots");
            Check(report.Contains("Imported rotation tracks retained with their complete stored data: " + denseBone.Name),
                "Imported-dense protection is explained as stored rotation data rather than an invented authored pin");
            Check(SetNativeContent(clip) == before && NativeAnimationOptimization.StorageKey(clip) == beforeStorage && editor._saveIndex == cursor && editor.CurrentFrame == frameBefore,
                "Mixed imported-dense Analyze preserves the live animation, storage, playhead and native Undo");
            result = SetNativeContent(window.Candidate); resultStorage = NativeAnimationOptimization.StorageKey(window.Candidate);
            SimpleControl<Button>(window, "optimizationDetails").PerformClick();
            await CaptureOptimization(window, Path.Combine(stage, "mixed-imported-dense-ready.png"));
            SimpleControl<Button>(window, "optimizeApply").PerformClick();
            Check(window.DialogResult == DialogResult.OK, "Mixed imported-dense Exact Apply completes successfully");
        });
        Check(ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.TargetModel, model) && ReferenceEquals(editor.SelectedBone, denseBone) &&
            editor.CurrentFrame == frameBefore && project.Animations.Count == assets && CompactCamera(form) == camera && SetNativeContent(clip) == result &&
            NativeAnimationOptimization.StorageKey(clip) == resultStorage && DenseRetained(clip) && editor._saveIndex == Math.Min((int)editor._allowedUndos, cursor + 1),
            "Mixed Apply changes one native command while preserving exact current member, model, bone, frame, camera and complete imported payload");
        Check(OptimizationRetainedBaseline(form) == before, "Mixed Apply retains the original mixed animation as its identified session baseline");
        editor.Undo(); await SimpleControlsReady(form);
        Check(SetNativeContent(clip) == before && NativeAnimationOptimization.StorageKey(clip) == beforeStorage && DenseRetained(clip),
            "One native Undo restores every I12 knot and the complete authored dense storage");
        editor.Redo(); await SimpleControlsReady(form);
        Check(SetNativeContent(clip) == result && NativeAnimationOptimization.StorageKey(clip) == resultStorage && DenseRetained(clip),
            "One native Redo restores the reviewed mixed result without recreating or dropping imported samples");
        int prepares = form.OptimizationPrepares;
        await Open(async window =>
        {
            await window.Analyze();
            Check(window.Candidate != null && !window.PreparedCanApply && form.OptimizationPrepares == prepares && DenseRetained(window.Candidate) && OptimizationRetainedBaseline(form) == before,
                "Repeated Exact analysis uses the mixed result cache and retains its original session baseline without another engine pass");
        });
        await Open(async window =>
        {
            SimpleControl<ComboBox>(window, "optimizationMode").SelectedIndex = 1; await window.Analyze();
            File.WriteAllText(Path.Combine(stage, "mixed-hq-reanalysis-report.txt"), SimpleControl<TextBox>(window, "optimizationReport").Text);
            Check(window.Candidate != null && DenseRetained(window.Candidate) && form.OptimizationPrepares > prepares && OptimizationRetainedBaseline(form) == before &&
                SetNativeContent(clip) == result && NativeAnimationOptimization.StorageKey(clip) == resultStorage,
                "HQ reanalysis scans the retained original mixed baseline, preserves imported data and leaves the live Exact result unchanged");
        });
        string export = Path.Combine(stage, "mixed-optimized-before-save.chr0"); form.ExportSimpleWorkspaceResult(export);
        using (var reopened = NodeFactory.FromFile(null, export) as CHR0Node)
        {
            var raw = DenseReadCompletePayloads(File.ReadAllBytes(export)).Single();
            Check(SetNativeContent(reopened) == result && NativeAnimationOptimization.StorageKey(reopened) == resultStorage && DenseRetained(reopened) && raw.EncodedValues.SequenceEqual(rawInput.EncodedValues) &&
                new FileInfo(export).Length < new FileInfo(input).Length,
                "Verified export before Save independently reopens a smaller mixed animation with every encoded dense sample intact");
        }
        string saved = Path.Combine(stage, "mixed-current.fpstudio"), identity = OptimizationNativeIdentity(clip); form.SaveFighterProject(saved);
        using (var fresh = FighterProject.Open(saved)) using (var reopened = new ModelForm())
        {
            reopened.InitializeFighterProject(fresh); reopened.Show();
            await GameLibraryWait(() => reopened.FighterEditor.TargetModel is MDL0Node && reopened.FighterEditor.SelectedCHR0 != null && OptimizationNativeIdentity(reopened.FighterEditor.SelectedCHR0) == identity,
                "Mixed project did not restore its exact selected member.", 60); await SimpleControlsReady(reopened);
            var restored = reopened.FighterEditor.SelectedCHR0;
            Check(SetNativeContent(restored) == result && NativeAnimationOptimization.StorageKey(restored) == resultStorage && DenseRetained(restored),
                "Project Save and fresh reopen preserve the exact mixed native result and complete imported dense terminal payload");
            SimpleDisposeForm(reopened);
        }
        Check(siblings.All(p => SetNativeContent(p.Key) == p.Value) && FighterProject.HashFile(input) == originalHash,
            "Mixed imported-dense optimization, native Undo, cache, export and reopen preserve every sibling animation and original source file");
    }
}
