using BrawlCrate.Studio;
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
using System.Windows.Forms;

internal static partial class Program
{
    static string SharedOptimizationBacking(ResourceNode node)
    {
        byte[] bytes = new byte[node.WorkingUncompressed.Length];
        if (bytes.Length > 0) Marshal.Copy((IntPtr)node.WorkingUncompressed.Address, bytes, 0, bytes.Length);
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
    }

    static async Task WorkspaceOptimizationSharedContainer(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Shared-container optimization qualification requires the private desktop.");
        const string saveMessage = "Save target model changes before optimizing/exporting animation; animation edits may remain unsaved.";
        string previousPreferences = Environment.GetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT");
        string previousLibrary = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", Path.Combine(output, "isolated-export-settings"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "isolated-library"));
        var originals = new[] { args[0], args[1] }.Distinct().ToDictionary(path => path, FighterProject.HashFile);
        string sharedFile = Path.Combine(output, "authentic-shared-fixture.brres");
        var expectedFixture = new Dictionary<string, string>();
        // These are independent fixture owners. Compose the model BRRES and
        // animations only; texture archives in sibling PAC entries are omitted.
        // This case qualifies ownership/curves, not material appearance. Other
        // full-PAC captures cover textures. Export cannot mutate the live document.
        using (var modelRoot = NodeFactory.FromFile(null, args[0]))
        using (var motionRoot = NodeFactory.FromFile(null, args[1]))
        {
            var model = Walk(modelRoot).OfType<MDL0Node>().First(m => { m.Populate(); return m.Objects.Length > 0 && m.Name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) < 0; });
            ResourceNode parent = model.Parent;
            while (parent != null && !(parent is BRRESNode)) parent = parent.Parent;
            var brres = parent as BRRESNode ?? throw new InvalidOperationException("The shared fixture needs an authentic BRRES model/texture container.");
            var group = brres.GetOrCreateFolder<CHR0Node>();
            foreach (var pair in new[] { new { Original = "Wait1", Name = "SharedSelected" }, new { Original = "Fall", Name = "SharedSibling" } })
            {
                var source = Walk(motionRoot).OfType<CHR0Node>().First(c => c.Name == pair.Original);
                var copy = NativeAnimationOptimization.CopySource(source); copy.Name = pair.Name;
                group.AddChild(copy);
                expectedFixture.Add(pair.Name, NativeAnimationOptimization.SourceContentKey(copy));
            }
            brres.ExportChecked(sharedFile);
        }
        using (var fixture = NodeFactory.FromFile(null, sharedFile))
            Check(expectedFixture.All(pair => NativeAnimationOptimization.SourceContentKey(Walk(fixture).OfType<CHR0Node>().Single(c => c.Name == pair.Key)) == pair.Value),
                "The independently composed shared BRRES preserves complete authentic source animation coefficients, flags and metadata");
        string fixtureHash = FighterProject.HashFile(sharedFile);
        var form = new ModelForm();
        try
        {
            form.InitializeFighterProject(FighterProject.Create("Shared model and unsaved animation ownership")); form.Show();
            Call(form, "ImportFighterModel", sharedFile);
            var editor = form.FighterEditor;
            var model = (MDL0Node)editor.TargetModel;
            var selected = Walk(model.RootNode).OfType<CHR0Node>().Single(c => c.Name == "SharedSelected");
            var sibling = Walk(model.RootNode).OfType<CHR0Node>().Single(c => c.Name == "SharedSibling");
            await SimpleControlsReady(form);
            editor.leftPanel.listAnims.SelectedItems.Clear();
            var selectedItem = editor.leftPanel.listAnims.Items.Cast<ListViewItem>().Single(item => ReferenceEquals(item.Tag, selected));
            selectedItem.Selected = true; selectedItem.Focused = true; editor.SetFrame(1);
            await SimpleControlsReady(form); await Await<ControlReply>(Call(form, "LoadControls"));
            Check(ReferenceEquals(editor.SelectedCHR0, selected) && ReferenceEquals(editor.TargetAnimation, selected),
                "Shared-container qualification selects the exact owned clip through the actual native animation list after setup settles");
            editor.SetFrame(Math.Min(3, selected.FrameCount));
            Check(ReferenceEquals(model.RootNode, selected.RootNode) && ReferenceEquals(model.RootNode, sibling.RootNode),
                "The actual project owns the target and both animations through one model-role BRRES registration");
            void Edit(CHR0Node clip, float amount)
            {
                var entry = clip.Children.OfType<CHR0EntryNode>().First(e => e.KeyArrays.Any(c => c.KeyCount > 0));
                var key = entry.KeyArrays.First(c => c.KeyCount > 0).Entries.First(); key._value += amount; entry.SignalPropertyChange();
            }
            Edit(selected, .03125f); Edit(sibling, .0625f);
            Check(!model.IsDirty && selected.IsDirty && sibling.IsDirty && model.RootNode.IsDirty,
                "Selected and unselected authentic animations have real unsaved key edits while the target model stays clean");
            string State() => new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Serialize(new {
                Model = DirtyModelMemory(model), RootBacking = SharedOptimizationBacking(model.RootNode),
                Clips = new[] { selected, sibling }.Select(clip => new { clip.Name, Identity = OptimizationNativeIdentity(clip),
                    Content = NativeAnimationOptimization.SourceContentKey(clip), Storage = NativeAnimationOptimization.StorageKey(clip), Backing = SharedOptimizationBacking(clip),
                    clip.HasChanged, clip.IsDirty, clip.IsBranch }).ToArray(),
                TargetSame = ReferenceEquals(editor.TargetModel, model), SelectedSame = ReferenceEquals(editor.SelectedCHR0, selected),
                AnimationSame = ReferenceEquals(editor.TargetAnimation, selected), Bone = editor.SelectedBone?.Name, Frame = editor.CurrentFrame, Camera = CompactCamera(form),
                Undo = editor._undoSaves.Select(item => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item)).ToArray(),
                Redo = editor._redoSaves.Select(item => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item)).ToArray(),
                SaveIndex = editor._saveIndex, editor.AwaitingRedoSave, ProjectDirty = (bool)Field(form, "_fighterDirty")
            });
            string baseline = State();
            void Unchanged(string phase) => Check(State() == baseline,
                "Shared-container " + phase + " preserves exact selected/sibling master content, storage, backing, dirty flags, ownership, selection, frame, camera and Undo/Redo");
            bool finished = false; Exception failure = null;
            string exported = Path.Combine(output, "shared-selected-export.chr0");
            form.OptimizationReady = async window =>
            {
                try
                {
                    Unchanged("opening original/result previews");
                    Check(!ReferenceEquals(window.SourceView.Model, model) && !ReferenceEquals(window.TargetView.Model, model) &&
                        !ReferenceEquals(window.SourceView.Model, window.TargetView.Model), "Shared-container preview models have two independent native owners");
                    SimpleControl<ComboBox>(window, "exportOptimizationPolicy").SelectedIndex = 0;
                    await window.Analyze();
                    Check(window.Candidate != null && window.PreparedCanApply, "Standalone Exact Analyze prepares the unsaved selected animation from a shared container: " +
                        (window.PreparedReport ?? SimpleControl<Label>(window, "optimizationSummary").Text + Environment.NewLine + SimpleControl<TextBox>(window, "optimizationReport").Text));
                    Unchanged("Analyze before project Save");
                    string expected = NativeAnimationOptimization.SourceContentKey(window.Candidate), storage = NativeAnimationOptimization.StorageKey(window.Candidate);
                    await CaptureOptimization(window, Path.Combine(output, "shared-container-analysis.png"));
                    using (FlowModal("workspaceFilePath", dialog => {
                        SimpleControl<TextBox>(dialog, "workspaceFilePath").Text = exported;
                        SimpleControl<Button>(dialog, "workspaceFileAccept").PerformClick();
                    })) SimpleControl<Button>(window, "optimizeApply").PerformClick();
                    Check(File.Exists(exported), "The actual standalone chooser publishes an optimized copy before saving either shared animation");
                    using (var decoded = NodeFactory.FromFile(null, exported) as CHR0Node)
                        Check(decoded != null && NativeAnimationOptimization.SourceContentKey(decoded) == expected && NativeAnimationOptimization.StorageKey(decoded) == storage,
                            "Shared-container publication independently reopens with the complete prepared content/storage identity");
                    Unchanged("export before project Save");
                }
                catch (Exception error) { failure = error; }
                finally { if (!window.IsDisposed) window.Close(); finished = true; }
            };
            try { Call(form, "ShowAnimationOptimization"); await GameLibraryWait(() => finished, "Shared-container optimization modal did not complete.", 180); }
            finally { form.OptimizationReady = null; }
            if (failure != null) throw failure;
            Unchanged("closing previews");

            string currentExport = Path.Combine(output, "shared-current-verified.chr0");
            form.ExportSimpleWorkspaceResult(currentExport);
            using (var decoded = NodeFactory.FromFile(null, currentExport) as CHR0Node)
                Check(decoded != null && NativeAnimationOptimization.SourceContentKey(decoded) == NativeAnimationOptimization.SourceContentKey(selected),
                    "Current verified export resolves the exact model-role animation owner and preserves every unsaved native coefficient before Save");
            Unchanged("current verified export before Save");
            Call(form, "AddSelectedBatchReviewClip");
            var rows = ReviewRows(form);
            Check(rows.Length == 1 && (string)Field(rows[0], "AssetId") == form.FighterProject.Model.Id,
                "Add selected review resolves the exact registered model-role owner without inventing an animation registration");
            Call(form, "MarkBatchReview");
            Check(ReviewApproved(rows[0]), "The exact dirty model-role animation can be explicitly reviewed while its target model is clean");
            string setDirectory = Path.Combine(output, "shared-reviewed-set");
            using (var set = (IDisposable)Call(form, "FreezeWorkspaceSet"))
                await (Task)Call(form, "ExportWorkspaceSet", set, setDirectory, CancellationToken.None, null);
            using (var decoded = NodeFactory.FromFile(null, Directory.GetFiles(setDirectory, "*.chr0").Single()) as CHR0Node)
                Check(decoded != null && NativeAnimationOptimization.SourceContentKey(decoded) == NativeAnimationOptimization.SourceContentKey(selected),
                    "Reviewed set export publishes the exact shared model-role member before Save without selecting or modifying its sibling");
            Unchanged("review and reviewed-set export before Save");

            void RejectUnchanged(string phase, Action operation)
            {
                string before = State(); Exception error = null;
                try { operation(); } catch (Exception caught) { error = caught is TargetInvocationException invocation ? invocation.InnerException : caught; }
                Check(error is InvalidOperationException && error.Message == saveMessage && State() == before,
                    phase + " requests ordinary Save without rebuilding the live model, animations or sibling resources");
            }
            var snapshot = form.GetType().GetMethod("WriteOptimizationPreviewRoot", BindingFlags.Static | BindingFlags.NonPublic);
            using (var missing = new BRRESNode())
                RejectUnchanged("Missing raw preview backing", () => snapshot.Invoke(null, new object[] { missing, Path.Combine(output, "missing-preview.brres"), CancellationToken.None }));
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel(); bool cancelled = false;
                try { snapshot.Invoke(null, new object[] { model.RootNode, Path.Combine(output, "cancelled-preview.brres"), cancel.Token }); }
                catch (TargetInvocationException error) { cancelled = error.InnerException is OperationCanceledException; }
                Check(cancelled && !File.Exists(Path.Combine(output, "cancelled-preview.brres")), "Raw preview copying honors cancellation before creating output");
            }
            var texture = Walk(model.RootNode).OfType<TEX0Node>().FirstOrDefault();
            if (texture != null)
            {
                texture.Name += "__pending_texture";
                RejectUnchanged("Pending render resource", () => Call(form, "ShowAnimationOptimization"));
                // Restore through an ordinary working-project Save; no dirty-bit
                // reset or hash is used to hide the pending texture edit.
                texture.Name = texture.Name.Substring(0, texture.Name.Length - "__pending_texture".Length);
                form.SaveFighterProject(Path.Combine(output, "saved-after-texture-check.fpstudio"));
            }
            // Explicit model-only native serialization is fixture setup for a
            // valid clean MDL0 whose enclosing BRRES backing is now stale.
            model.Name += "__model_only_revision";
            model.ExportChecked(Path.Combine(output, "model-only-working-revision.mdl0"));
            Check(!model.IsDirty && model.RootNode.IsDirty, "A model-only native save can leave an enclosing container's backing stale");
            RejectUnchanged("Stale raw preview backing", () => Call(form, "ShowAnimationOptimization"));
            Check(FighterProject.HashFile(sharedFile) == fixtureHash && originals.All(pair => FighterProject.HashFile(pair.Key) == pair.Value),
                "Shared-container preparation, publication and explicit working saves preserve the original authentic inputs and composed source fixture");
            File.WriteAllText(Path.Combine(output, "shared-container-evidence.json"), new JavaScriptSerializer().Serialize(new {
                FixtureSha256 = fixtureHash, ExportBytes = new FileInfo(exported).Length, SharedRoot = true, DirtySelectedAndSiblingPreservedBeforeSave = true,
                RawBackingSnapshot = true, ExportBeforeProjectSave = true, ActiveDesktopCapture = false, OSInputInjected = false
            }));
        }
        finally
        {
            form.OptimizationReady = null; if (!form.IsDisposed) SimpleDisposeForm(form);
            Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", previousPreferences);
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previousLibrary);
        }
    }
}
