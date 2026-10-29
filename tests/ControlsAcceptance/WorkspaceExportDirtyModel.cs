using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal static partial class Program
{
    private sealed class DirtyModelProgress : IProgress<string>
    {
        internal Action<string> Reported;
        public void Report(string value) => Reported?.Invoke(value);
    }

    // Read native backing bytes and authored fields without Export, Rebuild or
    // NativeControlHash. The latter would itself mask this regression.
    static string DirtyModelMemory(MDL0Node model)
    {
        byte[] bytes = new byte[model.WorkingUncompressed.Length];
        if (bytes.Length != 0) Marshal.Copy((IntPtr)model.WorkingUncompressed.Address, bytes, 0, bytes.Length);
        string backing;
        using (var sha = SHA256.Create()) backing = BitConverter.ToString(sha.ComputeHash(bytes));
        return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Serialize(new {
            model.Name, Rig = NativeGeneratedPairRegistry.RigKey(model), Backing = backing,
            Nodes = Walk(model.RootNode).Select(node => new { node.Name, node.TreePath, node.HasChanged, node.IsDirty, node.IsBranch,
                Length = node.WorkingUncompressed.Length, Address = ((IntPtr)node.WorkingUncompressed.Address).ToInt64() }).ToArray()
        });
    }

    static async Task WorkspaceExportDirtyModel(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Dirty-model export qualification requires the private desktop.");
        const string message = "Save target model changes before optimizing/exporting animation; animation edits may remain unsaved.";
        string previousPreferences = Environment.GetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT");
        string previousLibrary = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", Path.Combine(output, "isolated-export-settings"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "isolated-library"));
        var originals = new[] { args[0], args[1] }.Distinct().ToDictionary(path => path, FighterProject.HashFile);
        var phases = new List<object>();
        var form = new ModelForm();
        try
        {
            form.InitializeFighterProject(FighterProject.Create("Dirty target model export boundary"));
            form.Show();
            Call(form, "ImportFighterModel", args[0]);
            Call(form, "ImportFighterAnimations", new[] { new[] { args[1] } });
            var editor = form.FighterEditor;
            var clip = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First(c => c.Name == "Wait1");
            editor.SelectedCHR0 = clip;
            await SimpleControlsReady(form); await Await<ControlReply>(Call(form, "LoadControls"));
            editor.SetFrame(Math.Min(3, clip.FrameCount));
            var model = (MDL0Node)editor.TargetModel;
            var asset = (FighterAsset)Call(form, "SelectedFighterAsset");
            Call(form, "LoadGameSlotProfile", asset);
            var slot = form.FighterProject.AnimationSlots.Single(s => s.Id == "Wait1");
            string assignedKey = (string)Call(form, "SelectedFighterAnimationKey");
            Call(form, "AssignAnimationSlot", slot.Id, assignedKey);
            slot = form.FighterProject.AnimationSlots.Single(s => s.Id == "Wait1");
            Check(slot.AnimationKey == assignedKey && !string.IsNullOrEmpty(slot.AnimationKey),
                "Dirty-model status checks use the current exact assigned slot after the native metadata transaction replaces its rows");
            clip.Loop = !clip.Loop; clip.Loop = !clip.Loop;
            Call(form, "AddSelectedBatchReviewClip"); Call(form, "MarkBatchReview");
            Check(!model.IsDirty && clip.IsDirty, "Dirty-model fixture begins with a clean target and genuinely unsaved native animation edits");
            using (var clean = (IDisposable)Call(form, "FreezeWorkspaceExport"))
                Check(clean != null && clip.IsDirty && !model.IsDirty, "A clean target still permits freezing the unsaved current animation without clearing its dirty state");

            string State()
            {
                return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Serialize(new {
                    Model = DirtyModelMemory(model), Content = NativeAnimationOptimization.SourceContentKey(clip), Storage = NativeAnimationOptimization.StorageKey(clip),
                    Identity = OptimizationNativeIdentity(clip), TargetSame = ReferenceEquals(editor.TargetModel, model), SelectedSame = ReferenceEquals(editor.SelectedCHR0, clip),
                    AnimationSame = ReferenceEquals(editor.TargetAnimation, clip), Bone = editor.SelectedBone?.Name, Frame = editor.CurrentFrame,
                    Undo = editor._undoSaves.Select(item => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item)).ToArray(),
                    Redo = editor._redoSaves.Select(item => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item)).ToArray(),
                    SaveIndex = editor._saveIndex, AwaitingRedoSave = editor.AwaitingRedoSave,
                    Camera = CompactCamera(form), ClipDirty = clip.IsDirty, ProjectDirty = (bool)Field(form, "_fighterDirty"),
                    Slots = form.FighterProject.AnimationSlots, Reviews = ReviewRows(form).Select(row => new { Key = Field(row, "Key"), Approved = ReviewApproved(row),
                        Content = Field(row, "Content"), TargetContent = Field(row, "TargetContent"), Recipe = Field(row, "Recipe") }).ToArray()
                });
            }
            Exception Inner(Exception error)
            {
                while (error is TargetInvocationException invocation && invocation.InnerException != null) error = invocation.InnerException;
                return error;
            }
            void Record(string phase, string before, Exception failure)
            {
                var error = failure == null ? null : Inner(failure);
                Check(error is InvalidOperationException && error.Message == message, phase + " rejects the unsaved target with the explicit Save-model boundary");
                Check(before == State() && model.IsDirty, phase + " leaves model backing/fields/all native dirty flags, animation, selections, frame, camera and exact Undo/Redo entries unchanged");
                phases.Add(new { Phase = phase, ModelDirty = model.IsDirty, AnimationDirty = clip.IsDirty, ExpectedFailure = error.Message });
                File.WriteAllText(Path.Combine(output, "dirty-model-phases.json"), new JavaScriptSerializer().Serialize(phases));
            }
            void Reject(string phase, Action operation)
            {
                string before = State(); Exception failure = null;
                try { operation(); } catch (Exception error) { failure = error; }
                Record(phase, before, failure);
            }
            async Task RejectAsync(string phase, Func<Task> operation)
            {
                string before = State(); Exception failure = null;
                try { await operation(); } catch (Exception error) { failure = error; }
                Record(phase, before, failure);
            }
            void SaveModel(string phase)
            {
                string name = model.Name;
                form.SaveFighterProject(Path.Combine(output, phase + ".fpstudio"));
                Check(!model.IsDirty && model.Name == name, "Ordinary project Save remains available and commits the exact pending target model: " + phase);
                clip.Loop = !clip.Loop; clip.Loop = !clip.Loop;
                Call(form, "MarkBatchReview");
            }

            string destination = Path.Combine(output, "must-not-publish.chr0");
            string initialName = model.Name;
            model.Name = initialName + "__unsaved_target";
            Check(model.HasChanged && model.IsDirty && model.Name != initialName, "An actual native model rename creates a pending MDL0 edit");
            string pendingModel = DirtyModelMemory(model);
            Call(form, "SyncBatchReview");
            Check(!ReviewRows(form).Any(ReviewApproved) && DirtyModelMemory(model) == pendingModel,
                "Passive review synchronization invalidates pending-target approvals without serializing or clearing the native model");
            string beforeStatus = State();
            Check((string)Call(form, "AnimationSlotStatus", slot) == "Save target model changes · review assignment" && State() == beforeStatus,
                "Passive slot status requests Save without hashing or clearing a pending model");
            var reviewRows = ReviewRows(form);
            var priorRows = Array.CreateInstance(reviewRows[0].GetType(), reviewRows.Length);
            for (int i = 0; i < reviewRows.Length; i++) priorRows.SetValue(reviewRows[i], i);
            beforeStatus = State();
            Call(form, "RestoreBatchReviewAfterSave", priorRows);
            Check(State() == beforeStatus && !ReviewRows(form).Any(ReviewApproved),
                "Passive saved-review restoration preserves the pending model and leaves approvals invalid rather than hashing it");
            Reject("Explicit review approval", () => Call(form, "MarkBatchReview"));
            Reject("Explicit slot assignment", () => Call(form, "AssignAnimationSlot", slot.Id, (string)Call(form, "SelectedFighterAnimationKey")));
            Reject("Direct reviewed-export validation", () => Call(form, "ValidateBatchReviewRows"));
            form.OptimizationReady = window => window.Close(); // Never leave an unexpected modal open on regression.
            Reject("Standalone Analyze entry", () => Call(form, "ShowAnimationOptimization"));
            form.OptimizationReady = null;
            Reject("Single current export freeze", () => { using (var unused = (IDisposable)Call(form, "FreezeWorkspaceExport")) { } });
            Reject("Explicit member export freeze", () => { using (var unused = (IDisposable)Call(form, "FreezeOwnedWorkspaceExport", asset, clip, false, null)) { } });
            Reject("Protection-context freeze", () => Call(form, "FreezeExportOptimizationContext", model, asset, clip, null));
            Reject("Reviewed set before approval validation", () => { using (var unused = (IDisposable)Call(form, "FreezeWorkspaceSet")) { } });
            Reject("Native slot before status/hash", () => Call(form, "ExportGameSlot", slot.Id, destination));
            string ordinary = Path.Combine(output, "ordinary-native-dirty-target.chr0");
            string ordinaryState = State(); int ordinaryPrepares = form.OptimizationPrepares, ordinaryChecks = form.OptimizationNativeChecks;
            Check((bool)Call(form, "TryExportOwnedAnimationCopy", clip, ordinary), "Ordinary native animation export does not require a saved target model");
            using (var decoded = NodeFactory.FromFile(null, ordinary) as CHR0Node)
                Check(decoded != null && NativeAnimationOptimization.SourceContentKey(decoded) == NativeAnimationOptimization.SourceContentKey(clip) &&
                    NativeAnimationOptimization.StorageKey(decoded) == NativeAnimationOptimization.StorageKey(clip),
                    "Dirty-target ordinary native export retains the exact current animation rather than optimizing against a model");
            Check(State() == ordinaryState && model.IsDirty && form.OptimizationPrepares == ordinaryPrepares && form.OptimizationNativeChecks == ordinaryChecks,
                "Ordinary native export preserves unsaved model backing/fields/flags, native history and constraints without optimization");
            Reject("Direct workspace export", () => form.ExportSimpleWorkspaceResult(destination));
            Check(!File.Exists(destination), "Early dirty-model rejections publish no animation file");

            SaveModel("saved-before-late-validation");
            using (var frozen = (IDisposable)Call(form, "FreezeWorkspaceExport"))
            using (var set = (IDisposable)Call(form, "FreezeWorkspaceSet"))
            {
                model.Name += "__changed_after_freeze";
                Reject("Frozen member validation", () => ((Action)Field(frozen, "Validate"))());
                Reject("Frozen protection-context validation", () => ((Action)Field(Field(frozen, "Optimization"), "Validate"))());
                Reject("Frozen reviewed-set validation", () => ((Action)Field(set, "Validate"))());
                await RejectAsync("Prepared copy revalidation", () => (Task)Call(form, "PrepareOptimizedExportSnapshot", frozen, new NativeExportOptimizationSettings(), CancellationToken.None, null));
                Reject("Single publication revalidation", () => Call(form, "ExportWorkspaceSnapshot", frozen, destination));
                await RejectAsync("Set publication revalidation", () => (Task)Call(form, "ExportWorkspaceSet", set, Path.Combine(output, "must-not-publish-set"), CancellationToken.None, null));
                Check(!File.Exists(destination) && !Directory.Exists(Path.Combine(output, "must-not-publish-set")), "Late dirty-model validation publishes neither an animation nor a set");
            }

            SaveModel("saved-before-preparation-callback");
            using (var frozen = (IDisposable)Call(form, "FreezeWorkspaceExport"))
            {
                bool callback = false; string pendingState = null;
                var progress = new DirtyModelProgress { Reported = text => { if (callback) return; callback = true; model.Name += "__changed_during_prepare"; pendingState = State(); } };
                Exception failure = null;
                try { await (Task)Call(form, "PrepareOptimizedExportSnapshot", frozen, new NativeExportOptimizationSettings(), CancellationToken.None, progress); }
                catch (Exception error) { failure = error; }
                Check(callback && pendingState != null, "The target changes after preparation starts and before target snapshot serialization");
                Record("Preparation callback before model serialization", pendingState, failure);
            }

            SaveModel("saved-before-publication-callback");
            using (var frozen = (IDisposable)Call(form, "FreezeWorkspaceExport"))
            {
                bool callback = false; string pendingState = null;
                form.ExportCandidateWritten = path => { callback = true; model.Name += "__changed_before_publish"; pendingState = State(); };
                Exception failure = null;
                try { Call(form, "ExportWorkspaceSnapshot", frozen, destination); }
                catch (Exception error) { failure = error; }
                finally { form.ExportCandidateWritten = null; }
                Check(callback && pendingState != null, "The target changes after an independent candidate is written and before publication");
                Record("Candidate callback before publication", pendingState, failure);
                Check(!File.Exists(destination), "A target edit immediately before publication leaves the destination nonexistent");
            }
            Check(originals.All(pair => FighterProject.HashFile(pair.Key) == pair.Value), "Dirty-model guards and explicit working-project saves leave immutable original inputs unchanged");
        }
        finally
        {
            form.OptimizationReady = null; form.ExportCandidateWritten = null;
            if (!form.IsDisposed) SimpleDisposeForm(form);
            Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", previousPreferences);
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previousLibrary);
        }
    }
}
