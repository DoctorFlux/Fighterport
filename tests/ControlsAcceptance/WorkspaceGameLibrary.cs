using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml.Serialization;

internal static partial class Program
{
    // Frozen witnesses: disc/library choices never change the current native
    // document; opening a library pair copies complete containers, selects the
    // exact native identity (including duplicate names/costumes), performs no
    // retarget bake, and replaces only after the existing close decision.
    static async Task GameLibraryWait(Func<bool> ready, string message, int seconds = 60)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!ready())
        {
            if (DateTime.UtcNow >= deadline) throw new TimeoutException(message);
            await Task.Delay(40);
        }
    }
    static async Task GameLibrarySelect(ModelForm form, string clipId)
    {
        var search = (TextBox)Field(form, "_librarySearch"); search.Text = "Wait1";
        ((Timer)Field(form, "_librarySearchTimer")).Stop(); Call(form, "RefreshLibrary", clipId);
        var tree = (TreeView)Field(form, "_libraryList");
        await GameLibraryWait(() => !(bool)Field(form, "_libraryReplacingTree") &&
            !((Timer)Field(form, "_librarySearchTimer")).Enabled &&
            AllNodes(tree.Nodes).Any(n => (n.Tag as LibrarySearchItem)?.Clip.Id == clipId), "Exact source tree selection did not settle.");
        tree.SelectedNode = AllNodes(tree.Nodes).Single(n => (n.Tag as LibrarySearchItem)?.Clip.Id == clipId);
        Call(form, "UpdateSimpleWorkspace");
    }
    sealed class GameLibraryDocument
    {
        readonly ModelForm form;
        readonly object model, clip, bone, viewport;
        readonly int frame, undo, redo;
        readonly bool dirty;
        readonly string content, modelContent;
        internal GameLibraryDocument(ModelForm value)
        {
            form = value; var editor = value.FighterEditor;
            model = editor.TargetModel; clip = editor.SelectedCHR0; bone = editor.SelectedBone; viewport = editor.ModelPanel;
            frame = editor.CurrentFrame; undo = editor._undoSaves.Count; redo = editor._redoSaves.Count;
            dirty = (bool)Field(form, "_fighterDirty"); content = LifecycleContent(editor.SelectedCHR0);
            modelContent = (string)Call(form, "NativeControlHash", model, ".mdl0");
        }
        internal void CheckPreserved(string phase)
        {
            var editor = form.FighterEditor;
            Check(!form.IsDisposed && ReferenceEquals(model, editor.TargetModel) && ReferenceEquals(clip, editor.SelectedCHR0) &&
                ReferenceEquals(bone, editor.SelectedBone) && ReferenceEquals(viewport, editor.ModelPanel) &&
                frame == editor.CurrentFrame && undo == editor._undoSaves.Count && redo == editor._redoSaves.Count &&
                dirty == (bool)Field(form, "_fighterDirty") && content == LifecycleContent(editor.SelectedCHR0) &&
                modelContent == (string)Call(form, "NativeControlHash", model, ".mdl0"),
                "Game-library navigation preserves exact native target, clip, bone, frame, Undo, dirty state and model: " + phase);
        }
    }
    static void GameLibraryPublishMetadata(AnimationLibrary library, LibraryCatalogue data)
    {
        // Test-only immutable catalogue generation: authentic asset bytes and
        // canonical associations remain unchanged. This is a cache witness,
        // not evidence for a second physical costume.
        data.Generation++;
        string candidate = Path.Combine(library.DirectoryPath, "metadata-fixture.tmp");
        using (var stream = File.Create(candidate)) new XmlSerializer(typeof(LibraryCatalogue)).Serialize(stream, data);
        File.Move(candidate, Path.Combine(library.DirectoryPath, "catalogue-" + data.Generation.ToString("D20") + ".xml"));
    }
    static async Task GameLibraryCostumeRefresh(ModelForm form, AnimationLibrary library, string clipId)
    {
        await LibraryQuerySettled(form); await SimpleControlsReady(form);
        var state = new GameLibraryDocument(form);
        var combo = SimpleControl<ComboBox>(form, "libraryCostume");
        string[] originalIds = combo.Items.Cast<LibraryCostumeOption>().Select(c => c.ModelId).ToArray();
        var data = library.Read(); var clip = data.Clips.Single(c => c.Id == clipId);
        var canonical = data.Models.Single(m => m.Id == clip.ModelId);
        var alias = new LibraryModel { Id = Guid.NewGuid().ToString("N"), MountId = canonical.MountId,
            Character = canonical.Character, Name = canonical.Name, Variant = "Procedural metadata refresh witness",
            ResourceKey = canonical.ResourceKey, ResourcePath = canonical.ResourcePath, ResourceSha256 = canonical.ResourceSha256,
            SkeletonSha256 = canonical.SkeletonSha256, RestInterpretation = canonical.RestInterpretation,
            NativeVersion = canonical.NativeVersion, HasGeometry = canonical.HasGeometry, Shadow = canonical.Shadow,
            MotionMountIds = new List<string>(canonical.MotionMountIds) };
        int hashes = FighterProject.DiagnosticFileHashes, opens = AnimationLibrary.DiagnosticOpens,
            bakes = BrawlRetargeter.DiagnosticBakes, captures = BrawlRetargeter.DiagnosticCaptures;
        data.Models.Add(alias); GameLibraryPublishMetadata(library, data);
        Call(form, "RefreshLibrary", clipId); await LibraryQuerySettled(form);
        var tree = (TreeView)Field(form, "_libraryList");
        Check((tree.SelectedNode?.Tag as LibrarySearchItem)?.Clip.Id == clipId &&
            combo.Items.Count == originalIds.Length + 1 && combo.Items.Cast<LibraryCostumeOption>().Any(c => c.ModelId == alias.Id && c.Available),
            "Actual Refresh retains the exact source clip and discovers new compatible costume metadata without changing its association");
        combo.SelectedItem = combo.Items.Cast<LibraryCostumeOption>().Single(c => c.ModelId == alias.Id);
        data = library.Read(); data.Models.RemoveAll(m => m.Id == alias.Id); GameLibraryPublishMetadata(library, data);
        Call(form, "RefreshLibrary", clipId); await LibraryQuerySettled(form);
        Check((tree.SelectedNode?.Tag as LibrarySearchItem)?.Clip.Id == clipId &&
            originalIds.SequenceEqual(combo.Items.Cast<LibraryCostumeOption>().Select(c => c.ModelId)) &&
            (combo.SelectedItem as LibraryCostumeOption)?.ModelId == canonical.Id && library.Read().Clips.Single(c => c.Id == clipId).ModelId == canonical.Id,
            "Refreshing a retained clip removes stale costume options and safely restores its exact canonical model when a browser choice disappears");
        Check(FighterProject.DiagnosticFileHashes == hashes && AnimationLibrary.DiagnosticOpens == opens &&
            BrawlRetargeter.DiagnosticBakes == bakes && BrawlRetargeter.DiagnosticCaptures == captures && Field(form, "_libraryLease") == null,
            "Costume metadata refresh and enumeration perform no source hashing, native opening/decoding, retarget capture or bake");
        state.CheckPreserved("added and removed costume metadata for a retained source clip");
    }
    static async Task GameLibraryCostumeLayouts(ModelForm form, string output, bool requireMultiple = true)
    {
        var state = new GameLibraryDocument(form);
        var lease = Field(form, "_libraryLease");
        int originalText = WorkspaceAppearance.TextPercent;
        Size originalSize = form.ClientSize;
        try
        {
            foreach (int text in new[] { 100, 175 }) foreach (int width in new[] { 1050, 1700 })
            {
                form.ClientSize = new Size(width, 950); Call(form, "SetWorkspaceTextSize", text);
                Call(form, "UpdateSimpleWorkspace"); await Task.Delay(75);
                var pane = (Panel)Field(form, "_libraryPane");
                var source = (ModelPanel)Field(form, "_libraryViewport");
                var target = form.FighterEditor.ModelPanel;
                var costume = SimpleControl<ComboBox>(form, "libraryCostume");
                var play = (Button)Field(form, "_libraryPlay");
                var use = SimpleControl<Button>(form, "libraryUseOnTarget");
                var fit = SimpleControl<Button>(form, "sourceFitTarget");
                var controls = new Control[] { costume, source, play, use, fit };
                var intersections = controls.SelectMany((a, i) => controls.Skip(i + 1).Select(b => new {
                    First = a.Name, Second = b.Name,
                    FirstBounds = a.RectangleToScreen(a.ClientRectangle).ToString(),
                    SecondBounds = b.RectangleToScreen(b.ClientRectangle).ToString(),
                    Overlap = a.RectangleToScreen(a.ClientRectangle).IntersectsWith(b.RectangleToScreen(b.ClientRectangle))
                })).ToArray();
                bool overlap = intersections.Any(pair => pair.Overlap);
                var center = target.PointToScreen(new Point(target.ClientSize.Width / 2, target.ClientSize.Height / 2));
                string captureStem = "disc-costume-source-target-" + width + "-" + text;
                File.WriteAllText(Path.Combine(output, captureStem + ".json"), new JavaScriptSerializer().Serialize(new {
                    Width = width, TextPercent = text, FormClient = form.ClientRectangle.ToString(),
                    PaneBounds = pane.Bounds.ToString(), PaneScreen = pane.RectangleToScreen(pane.ClientRectangle).ToString(),
                    SourceHeight = source.Height, SourceAllowsSelection = source.CurrentViewport.AllowSelection,
                    CostumeCount = costume.Items.Count, RequireMultipleCostumes = requireMultiple,
                    TargetBounds = target.Bounds.ToString(), TargetCenter = center.ToString(),
                    TargetCenterCovered = pane.RectangleToScreen(pane.ClientRectangle).Contains(center),
                    Overlap = overlap, Intersections = intersections,
                    Controls = controls.Select(control => new {
                        control.Name, control.Text, control.Visible, control.Enabled, control.Width, control.Height,
                        Bounds = control.Bounds.ToString(), Client = control.ClientRectangle.ToString(),
                        Screen = control.RectangleToScreen(control.ClientRectangle).ToString(),
                        InPane = InClient(control, pane), Parent = control.Parent.Name, ParentClient = control.Parent.ClientRectangle.ToString(),
                        FontName = control.Font.Name, FontSize = control.Font.Size, FontHeight = control.Font.Height,
                        CaptionWidth = TextRenderer.MeasureText(control == play ? "Pause" : control.Text, control.Font).Width,
                        CaptionAvailableWidth = control.Width - 12,
                        CaptionFits = control.Width - 12 >= TextRenderer.MeasureText(control == play ? "Pause" : control.Text, control.Font).Width
                    }).ToArray()
                }));
                // Capture before gates so the first failing compact case is
                // reviewable with its exact measured native layout.
                form.FighterEditor.FrameFighterModel(); Call(form, "FrameLibraryOriginal");
                await CaptureSimpleWorkspace(form, Path.Combine(output, captureStem + ".png"));
                Check(controls.All(control => control.Visible && InClient(control, pane)) &&
                    costume.Height >= costume.Font.Height + 4 && costume.Items.Count >= (requireMultiple ? 2 : 1),
                    "Selected costume, original viewport and pinned source commands fit the source pane at " + width + "/" + text);
                Check(!overlap && source.Height >= 120 && !source.CurrentViewport.AllowSelection &&
                    play.Width - 12 >= TextRenderer.MeasureText("Pause", play.Font).Width &&
                    new[] { use, fit }.All(button => button.Width - 12 >= TextRenderer.MeasureText(button.Text, button.Font).Width),
                    "Costume row and pinned preview/play/use/fit controls do not overlap and command captions fit at " + width + "/" + text);
                Check(!pane.RectangleToScreen(pane.ClientRectangle).Contains(center) && target.Width >= 250 && target.Height >= 200 &&
                    ReferenceEquals(lease, Field(form, "_libraryLease")),
                    "Costume source pane leaves the full native target viewport usable and retains its verified source at " + width + "/" + text);
                state.CheckPreserved("costume source layout " + width + "/" + text);
            }
        }
        finally
        {
            form.ClientSize = originalSize; Call(form, "SetWorkspaceTextSize", originalText); Call(form, "UpdateSimpleWorkspace");
        }
    }
    static async Task GameLibraryRepairCostume(ModelForm form, AnimationLibrary library, string clipId, string modelId, string output)
    {
        var data=library.Read();var model=data.Models.Single(m=>m.Id==modelId);var mount=data.Mounts.Single(m=>m.Id==model.MountId);
        var state=new GameLibraryDocument(form);Call(form,"PauseSourceOperation");Call(form,"ReleaseLibraryPreview");
        Check(mount.OriginPath!=null&&mount.LocalPath==mount.OriginPath,
            "New disc metadata points to the original image rather than an extracted costume copy");
        string origin=mount.OriginPath;
        try
        {
            // An isolated metadata fixture makes one costume's original image
            // unavailable. Never move or alter the user's actual disc image.
            mount.OriginPath=Path.Combine(output,"missing-original-disc.wbfs");GameLibraryPublishMetadata(library,data);
            bool rejected=false;
            try{using((LibrarySourceLease)Call(form,"LoadSelectedLibrarySource")){} }
            catch(TargetInvocationException error)when(error.InnerException is InvalidOperationException||error.InnerException is IOException){rejected=true;}
            Check(rejected&&SimpleControl<Button>(form,"libraryRepairSource").Enabled&&!SimpleControl<Button>(form,"libraryUseOnTarget").Enabled,
                "An unavailable original disc disables unsafe costume preview/transfer and exposes source repair");
            state.CheckPreserved("missing original disc");
            await CaptureSimpleWorkspace(form,Path.Combine(output,"disc-original-source-missing.png"));
        }
        finally{mount.OriginPath=origin;GameLibraryPublishMetadata(library,data);}
        Call(form,"RefreshSavedGameSourceUi",library);
        await GameLibrarySelect(form,clipId);Call(form,"SelectLibraryCostume",modelId);
        SimpleControl<Button>(form,"libraryPreviewOriginal").PerformClick();
        var source=(LibrarySourceLease)Field(form,"_libraryLease");
        Check(source.Reference.ModelId==modelId&&source.Reference.ClipId==clipId&&source.Reference.ModelResourceSha256==model.ResourceSha256&&source.Reference.SourceRig==model.SkeletonSha256,
            "Restoring the original-source link recovers the exact costume without changing its canonical association");
        Check(!Directory.Exists(Path.Combine(library.DirectoryPath,"NativeCache"))||!Directory.EnumerateFiles(Path.Combine(library.DirectoryPath,"NativeCache"),"*",SearchOption.AllDirectories).Any(),
            "Complete disc indexing retains no extracted fighter tree in the library");
        state.CheckPreserved("recovered direct original source");
    }
    static async Task<ModelForm> GameLibraryOpenTarget(ModelForm form, AnimationLibrary library,
        string clipId, string modelId, string output, bool exerciseFailedSave)
    {
        var source = library.Read().Clips.Single(c => c.Id == clipId);
        var modelRecord = library.Read().Models.Single(m => m.Id == modelId);
        var data = library.Read();
        string modelContainer = data.Mounts.Single(m => m.Id == modelRecord.MountId).ContainerSha256;
        string motionContainer = data.Mounts.Single(m => m.Id == source.MountId).ContainerSha256;
        string exactModel = modelRecord.ResourceSha256, exactRig = modelRecord.SkeletonSha256;
        string expectedNative;
        using (var original = library.Open(clipId, modelId))
        {
            expectedNative = LifecycleContent(original.Animation);
            using (var prepared = ModelForm.PrepareLibraryTarget(original))
            {
                Check(prepared.Model.ContentSha256 == modelContainer && prepared.Animations.Single().ContentSha256 == motionContainer &&
                    prepared.Animations.Single().Retarget == null && !prepared.Animations.Single().Generated,
                    "Library target preparation copies both complete authentic containers and does not invent a retargeted result");
                using (var root = NodeFactory.FromFile(null, prepared.Animations.Single().WorkingPath))
                    Check(Walk(root).OfType<NW4RAnimationNode>().Count() == Walk(original.MotionRoot).OfType<NW4RAnimationNode>().Count(),
                        "Opening a library target preserves every native companion animation in its original motion container");
            }
            string hash = original.Reference.ModelResourceSha256;
            original.Reference.ModelResourceSha256 = new string('0', 64);
            bool rejected = false;
            try { using (ModelForm.PrepareLibraryTarget(original)) { } }
            catch (InvalidDataException) { rejected = true; }
            original.Reference.ModelResourceSha256 = hash;
            Check(rejected, "A damaged frozen model identity rejects target preparation before replacing the current document");
        }
        await ReviewBodyEdit(form, 2); await SimpleControlsReady(form);
        var before = new GameLibraryDocument(form);
        var originalPreview = Field(form, "_libraryLease");
        int bakes = BrawlRetargeter.DiagnosticBakes;
        bool prompted = false;
        using (FlowModal("unsavedProjectCancel", dialog => {
            prompted = true; CaptureDialog(dialog, output, "library-target-cancel.png");
            SimpleControl<Button>(dialog, "unsavedProjectCancel").PerformClick();
        })) NextItem(form, "nextOpenLibraryTarget").PerformClick();
        Check(prompted && BrawlRetargeter.DiagnosticBakes == bakes && ReferenceEquals(originalPreview, Field(form, "_libraryLease")),
            "Opening an editable target asks the existing unsaved decision, and cancellation retains the read-only original without baking");
        before.CheckPreserved("cancelled target open");
        if (exerciseFailedSave)
        {
            string saved = Path.Combine(output, "old-target.fpstudio"); form.SaveFighterProject(saved);
            Call(form, "MarkFighterChanged"); before = new GameLibraryDocument(form);
            using (var locked = new FileStream(saved, FileMode.Open, FileAccess.Read, FileShare.None))
            using (FlowModal("unsavedProjectSave", dialog => {
                SimpleControl<Button>(dialog, "unsavedProjectSave").PerformClick();
                Check(dialog.Visible && SimpleControl<Label>(dialog, "unsavedProjectStatus").Text.StartsWith("Could not save"),
                    "A failed save keeps the old target and existing unsaved decision open");
                CaptureDialog(dialog, output, "library-target-save-failed.png");
                SimpleControl<Button>(dialog, "unsavedProjectCancel").PerformClick();
            })) NextItem(form, "nextOpenLibraryTarget").PerformClick();
            before.CheckPreserved("failed save during target replacement");
        }
        using (FlowModal("unsavedProjectDiscard", dialog => SimpleControl<Button>(dialog, "unsavedProjectDiscard").PerformClick()))
            NextItem(form, "nextOpenLibraryTarget").PerformClick();
        Check(!form.IsDisposed&&ReferenceEquals(form,Field(MainForm.Instance,"_fighterForm")), "Explicit Discard opens the fully prepared library pair inside the same native viewer");
        var opened = (ModelForm)Field(MainForm.Instance, "_fighterForm");
        opened.WindowState = FormWindowState.Normal; opened.ClientSize = new Size(1700, 950);
        await GameLibraryWait(() => opened.FighterEditor.SelectedCHR0 != null, "The exact target animation did not open.");
        await SimpleControlsReady(opened);
        await GameLibraryWait(() => Field(opened, "_libraryLease") is LibrarySourceLease lease &&
            lease.Reference.ClipId == clipId && lease.Reference.ModelId == modelId, "The original selected model/costume did not reappear beside the independent target.");
        var target = (MDL0Node)opened.FighterEditor.TargetModel; var clip = opened.FighterEditor.SelectedCHR0;
        var borrowed = (LibrarySourceLease)Field(opened, "_libraryLease");
        Check(LifecycleContent(clip) == expectedNative && clip.FrameCount == source.FrameCount && clip.Loop == source.Loop &&
            BrawlLibrarySource.ResourceHash(target) == exactModel && BrawlRetargeter.SkeletonFingerprint(target) == exactRig &&
            opened.FighterProject.Animations.Single().ContentSha256 == motionContainer && opened.FighterProject.Model.ContentSha256 == modelContainer,
            "The editable target opens the exact selected native model/costume and animation identity, keys, tangents, flags, timing and complete containers");
        Check(!ReferenceEquals(target, borrowed.Model) && !ReferenceEquals(clip, borrowed.Animation) &&
            !((ModelPanel)Field(opened, "_libraryViewport")).CurrentViewport.AllowSelection &&
            opened.FighterProject.Animations.Single().Retarget == null && BrawlRetargeter.DiagnosticBakes == bakes,
            "Original source and editable target have separate native ownership; opening a target never runs the retargeter");
        opened.FighterEditor.FrameFighterModel(); Call(opened, "FrameLibraryOriginal");
        await CaptureSimpleWorkspace(opened, Path.Combine(output, "library-target-original-and-owned.png"));
        string baseline = LifecycleContent(clip), originalContent = LifecycleContent(borrowed.Animation);
        await ReviewBodyEdit(opened, 3); string edited = LifecycleContent(clip);
        Check(edited != baseline && LifecycleContent(borrowed.Animation) == originalContent,
            "Native body editing changes only the independent target and leaves the displayed original animation untouched");
        opened.FighterEditor.Undo(); await SimpleControlsReady(opened);
        Check(LifecycleContent(clip) == baseline, "The opened library target uses existing native Undo for exact authored curves");
        opened.FighterEditor.Redo(); await SimpleControlsReady(opened);
        Check(LifecycleContent(clip) == edited, "Native Redo restores the exact independent library-target edit");
        string projectPath = Path.Combine(output, "library-owned-target.fpstudio"); opened.SaveFighterProject(projectPath);
        using (var saved = FighterProject.Open(projectPath))
        using (var root = NodeFactory.FromFile(null, saved.Animations.Single().WorkingPath))
        {
            var savedClip = (CHR0Node)BrawlLibrarySource.Resolve(root, source.ResourceKey);
            Check(LifecycleContent(savedClip) == edited && saved.SelectedAnimation == opened.FighterProject.SelectedAnimation,
                "Project save and fresh native reopen retain the exact selected library identity and edited animation");
        }
        string exported = Path.Combine(output, "edited-library-target.chr0"); opened.ExportSimpleWorkspaceResult(exported);
        using (var saved = NodeFactory.FromFile(null, exported) as CHR0Node)
            Check(NativeDifference(clip, saved) == null, "Verified export preserves every native channel and metadata field from the independently edited target");
        await CaptureSimpleWorkspace(opened, Path.Combine(output, "library-target-edited.png"));
        return opened;
    }

    static async Task WorkspaceLibraryTarget(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Library target acceptance requires a private desktop.");
        string previous = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "shared"));
        var originals = new[] { args[0], args[1] }.Distinct().ToDictionary(p => p, FighterProject.HashFile);
        ModelForm form = null;
        try
        {
            string files = Path.Combine(output, "source-files"); Directory.CreateDirectory(files);
            string model = Path.Combine(files, Path.GetFileName(args[0])), motion = Path.Combine(files, Path.GetFileName(args[1]));
            File.Copy(args[0], model); File.Copy(args[1], motion);
            string modified = Path.Combine(files, "duplicate-motion.pac");
            using (var root = NodeFactory.FromFile(null, motion))
            {
                var clips = Walk(root).OfType<CHR0Node>().ToArray();
                var first = clips.Single(c => c.Name == "Wait1");
                var second = clips.First(c => c.Name == "Wait2"); second.Name = first.Name;
                Check(NativeDifference(first, second) != null, "Procedural duplicate-name fixture has observably different native content behind the same clip label");
                root.Export(modified);
            }
            File.Copy(modified, motion, true);
            using (var startup = MainForm.StartViewerApplication())
            {
                form = (ModelForm)Field(MainForm.Instance, "_fighterForm");
                form.WindowState = FormWindowState.Normal; form.ClientSize = new Size(1700, 950);
                Call(form, "ImportFighterModel", args[0]); Call(form, "ImportFighterAnimations", new[] { new[] { args[1] } }); await SimpleControlsReady(form);
                string sharedBefore = NextLibraryFiles(new AnimationLibrary());
                var info = BrawlLibrarySource.InspectModels(model).First(m => !m.Shadow && m.HasGeometry);
                var request = new LibraryRegistration { ModelPath = model, ModelKey = info.ResourceKey, MotionPath = motion,
                    Character = info.Name, GameVersion = "Procedural duplicate-name native fixture; build unverified",
                    BuildEvidence = "Copied native inputs; only a second clip display name was changed for identity testing.", ApproveAssociation = true };
                typeof(ModelForm).GetMethod("LoadSessionSource", Private, null, new[] { typeof(LibraryRegistration) }, null).Invoke(form, new object[] { request });
                var library = (AnimationLibrary)Field(form, "_library");
                var choices = library.Browse("Wait1").Where(i => i.Clip.Name == "Wait1" && i.Clip.Channel == "CHR0 skeletal" && i.Model != null).OrderBy(i => i.Clip.ResourceKey, StringComparer.Ordinal).ToArray();
                Check(choices.Length == 2 && choices[0].Clip.Id != choices[1].Clip.Id, "Duplicate names retain distinct exact source resource identities");
                var chosen = choices[1]; await GameLibrarySelect(form, chosen.Clip.Id);
                await GameLibraryCostumeRefresh(form, library, chosen.Clip.Id);
                Call(form, "SelectLibraryCostume", chosen.Model.Id); SimpleControl<Button>(form, "libraryPreviewOriginal").PerformClick();
                await GameLibraryCostumeLayouts(form, output, requireMultiple: false);
                string oldTemporaryDirectory = library.DirectoryPath;
                form = await GameLibraryOpenTarget(form, library, chosen.Clip.Id, chosen.Model.Id, output, true);
                var newLibrary = (AnimationLibrary)Field(form, "_library");
                Check(ReferenceEquals(library,newLibrary)&&string.Equals(oldTemporaryDirectory, newLibrary.DirectoryPath, StringComparison.OrdinalIgnoreCase) &&
                    ReferenceEquals(newLibrary, Field(form, "_sessionSourceLibrary")) && newLibrary.Read().Clips.Any(c => c.Id == chosen.Clip.Id) &&
                    NextLibraryFiles(new AnimationLibrary()) == sharedBefore,
                    "Opening temporary files as an editable target retains the same session catalogue without registering the shared library");
                Check(originals.All(p => FighterProject.HashFile(p.Key) == p.Value), "Library target tests leave every supplied original model and motion byte unchanged");
            }
        }
        finally
        {
            if (form != null && !form.IsDisposed) SimpleDisposeForm(form);
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previous);
        }
    }

    static async Task WorkspaceGameLibrary(string[] args, string output)
    {
        if (!PrivateDesktop || !args.Contains("--disc-image"))
            throw new InvalidOperationException("Game-library acceptance requires a private desktop and an actual disc image.");
        string image = args[Array.IndexOf(args, "--disc-image") + 1];
        string previous = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "L"));
        string discHash;
        if(args.Contains("--verified-library-index"))
        {
            string verified=args[Array.IndexOf(args,"--verified-library-index")+1];LibraryCatalogue prior;
            using(var stream=File.OpenRead(verified))prior=(LibraryCatalogue)new XmlSerializer(typeof(LibraryCatalogue)).Deserialize(stream);
            discHash=prior.GameLocations.Single(l=>l.Indexed).ContainerSha256;
        }
        else discHash=FighterProject.HashFile(image);
        var originals = new[] { args[0], args[1] }.Distinct().ToDictionary(p => p, FighterProject.HashFile);
        ModelForm form = null;
        try
        {
            using (var startup = MainForm.StartViewerApplication())
            {
                form = (ModelForm)Field(MainForm.Instance, "_fighterForm");
                form.WindowState = FormWindowState.Normal; form.ClientSize = new Size(1700, 950);
                Call(form, "ImportFighterModel", args[0]); Call(form, "ImportFighterAnimations", new[] { new[] { args[1] } }); await SimpleControlsReady(form);
                await ReviewBodyEdit(form, 2); var before = new GameLibraryDocument(form);
                int bakes = BrawlRetargeter.DiagnosticBakes; var shared = new AnimationLibrary();
                string indexBefore = NextLibraryFiles(shared);
                using (FlowModal("discFilePath", dialog => {
                    SimpleControl<TextBox>(dialog, "discFilePath").Text = Path.Combine(output, "missing.wbfs");
                    SimpleControl<Button>(dialog, "discContinue").PerformClick();
                    Check(dialog.Visible && !string.IsNullOrWhiteSpace(SimpleControl<Label>(dialog, "discValidation").Text),
                        "Direct Add Brawl disc explains an invalid path inline without registering or replacing a target");
                    CaptureDialog(dialog, output, "disc-invalid.png");
                    Check(dialog.Controls.Find("discToolPath",true).Length==0 && dialog.Controls.Find("discBrowseTool",true).Length==0,
                        "Add Brawl has no external reader, key-file or emulator setup control");
                    string bad=Path.Combine(output,"damaged.iso");File.WriteAllBytes(bad,new byte[4096]);
                    SimpleControl<TextBox>(dialog,"discFilePath").Text=bad;
                    SimpleControl<Button>(dialog,"discContinue").PerformClick();
                    Check(dialog.Visible&&!string.IsNullOrWhiteSpace(SimpleControl<Label>(dialog,"discValidation").Text),
                        "The built-in reader reports a damaged image inline without invoking another application");
                    CaptureDialog(dialog,output,"disc-damaged.png");
                    SimpleControl<Button>(dialog, "discCancel").PerformClick();
                })) NextItem(form, "nextAddBrawlDisc").PerformClick();
                before.CheckPreserved("cancelled/invalid disc and reader choice");
                Check(NextLibraryFiles(shared) == indexBefore && BrawlRetargeter.DiagnosticBakes == bakes,
                    "Cancelled disc selection changes neither catalogue bytes nor native retarget results");
                Call(form,"SetWorkspaceTextSize",175);
                try
                {
                    using(FlowModal("discFilePath",dialog=>{
                        Check(dialog.Controls.Find("discToolPath",true).Length==0&&InClient(SimpleControl<Button>(dialog,"discContinue"),dialog)&&InClient(SimpleControl<Button>(dialog,"discCancel"),dialog),
                            "The large-text source chooser retains contained actions without external-tool setup");
                        CaptureDialog(dialog,output,"disc-chooser-175.png");SimpleControl<Button>(dialog,"discCancel").PerformClick();
                    }))NextItem(form,"nextAddBrawlDisc").PerformClick();
                    before.CheckPreserved("cancelled large-text native source chooser");
                }
                finally {Call(form,"SetWorkspaceTextSize",100);}
                Action<Form> chooseDisc = dialog => {
                    SimpleControl<TextBox>(dialog, "discFilePath").Text = image;
                    SimpleControl<TextBox>(dialog, "discBuild").Text = "Supplied Brawl disc; build identity verified from native header";
                    CaptureDialog(dialog, output, "disc-selection.png");
                    SimpleControl<Button>(dialog, "discContinue").PerformClick();
                    if (dialog.DialogResult != DialogResult.OK)
                        throw new InvalidOperationException("Valid Brawl source preflight failed: " + SimpleControl<Label>(dialog, "discValidation").Text);
                };
                bool sawPicker=false;
                using(FlowModal("libraryGameCharacters",dialog=>{sawPicker=true;throw new InvalidOperationException("Adding a disc must not ask for characters.");}))
                using(FlowModal("discFilePath",chooseDisc))NextItem(form,"nextAddBrawlDisc").PerformClick();
                Check(!sawPicker,"Add Brawl indexes the full disc without any character-selection dialog");
                var library = (AnimationLibrary)Field(form, "_library"); await LibraryQuerySettled(form);
                var data = library.Read(); var location = data.GameLocations.Single(l => l.Indexed);
                Check(location.Provider == BrawlDiscImage.Provider && location.ContainerSha256 == discHash && location.ToolSha256 == null && location.ToolPath == null &&
                    data.Clips.Count > 15000 && data.Mounts.Count>900 && new[]{"mario","snake","captain","kirby","wario","zelda"}.All(character=>data.Mounts.Any(m=>m.VirtualPath.StartsWith("fighter/"+character+"/",StringComparison.Ordinal))) && data.Mounts.Any(m=>m.VirtualPath=="fighter/Fighter.pac"),
                    "One Add indexes the complete fighter roster, costumes and shared fighter package with exact native image provenance");
                before.CheckPreserved("successful read-only disc indexing");
                using(FlowModal("librarySourceTabs",dialog=>{
                    var tabs=SimpleControl<TabControl>(dialog,"librarySourceTabs");tabs.SelectedIndex=1;
                    SimpleControl<TextBox>(dialog,"libraryLocationPath").Text=image;
                    using(FlowModal("libraryWorkCancel",work=>{SimpleControl<Button>(work,"libraryWorkCancel").PerformClick();}))
                        SimpleControl<Button>(dialog,"libraryBrowseGame").PerformClick();
                    Check(dialog.Controls.Find("libraryGameCharacters",true).Length==0,"Source management uses the complete library without character selection");
                    CaptureDialog(dialog,output,"disc-source-management.png");SimpleControl<Button>(dialog,"librarySourcesClose").PerformClick();
                }))NextItem(form,"nextLibrarySources").PerformClick();
                await LibraryQuerySettled(form);before.CheckPreserved("cancelled direct disc browse through source management");

                Check(BrawlRetargeter.DiagnosticBakes == bakes, "Disc browsing and indexing perform no animation retarget bake");
                var item = library.Browse("snake Wait1").Single(i => i.Model != null && i.Clip.Name == "Wait1" && i.Clip.Channel == "CHR0 skeletal" && i.Model.Character == "snake");
                await GameLibrarySelect(form, item.Clip.Id);
                var costumes = library.GetCostumes(item.Clip.Id);
                Check(costumes.Count(c => c.Available) > 1 && costumes.Any(c => c.IsCanonical),
                    "The actual disc exposes canonical and alternative compatible character costumes without rewriting its clip association");
                var canonicalCostume = costumes.Single(c => c.IsCanonical);
                var selected = costumes.First(c => c.Available && !c.IsCanonical && !string.Equals(
                    Path.GetFileNameWithoutExtension(c.Variant), Path.GetFileNameWithoutExtension(canonicalCostume.Variant), StringComparison.OrdinalIgnoreCase));
                Check(Path.GetFileNameWithoutExtension(selected.Variant) != Path.GetFileNameWithoutExtension(canonicalCostume.Variant),
                    "Authentic costume UI fixture selects a different palette slot, not the same costume's alternate PAC/PCS container");
                string catalogueHash = FighterProject.HashFile(library.IndexPath);
                Call(form, "SelectLibraryCostume", selected.ModelId); SimpleControl<Button>(form, "libraryPreviewOriginal").PerformClick();
                var original = (LibrarySourceLease)Field(form, "_libraryLease");
                Check(original.Reference.ModelId == selected.ModelId && original.Reference.ClipId == item.Clip.Id &&
                    original.Reference.GameSourceSha256 == discHash && !((ModelPanel)Field(form, "_libraryViewport")).CurrentViewport.AllowSelection &&
                    FighterProject.HashFile(library.IndexPath) == catalogueHash && library.Read().Clips.Single(c => c.Id == item.Clip.Id).ModelId == item.Clip.ModelId,
                    "Source preview uses the selected exact costume and original disc identity while the canonical association and catalogue remain immutable");
                before.CheckPreserved("costume preview selection");
                form.FighterEditor.FrameFighterModel(); Call(form, "FrameLibraryOriginal");
                await CaptureSimpleWorkspace(form, Path.Combine(output, "disc-source-and-current-target.png"));
                await GameLibraryCostumeLayouts(form, output);
                int readerOpens=BrawlDiscImage.DiagnosticOpens,sourceHashes=FighterProject.DiagnosticFileHashes,sourceOpens=AnimationLibrary.DiagnosticOpens,sourceBakes=BrawlRetargeter.DiagnosticBakes;
                for(int sample=0;sample<24;sample++){typeof(ModelForm).GetField("_libraryTime",Private).SetValue(form,sample*(original.Animation.FrameCount-1)/23.0);Call(form,"RenderLibraryFrame");}
                Check(BrawlDiscImage.DiagnosticOpens==readerOpens&&FighterProject.DiagnosticFileHashes==sourceHashes&&AnimationLibrary.DiagnosticOpens==sourceOpens&&BrawlRetargeter.DiagnosticBakes==sourceBakes,
                    "Prepared source animation preview performs no disc opening, file hashing, native source loading or retarget baking");

                await GameLibraryRepairCostume(form, library, item.Clip.Id, selected.ModelId, output);
                var target = (MDL0Node)form.FighterEditor.TargetModel;
                string expectedPath = Path.Combine(output, "expected-selected-costume.chr0");
                using (var source = library.Open(item.Clip.Id, selected.ModelId))
                using (var expected = BrawlRetargeter.Bake(source.Model, target, source.Animation, groundMode: form.FighterProject.GroundMode)) expected.Animation.Export(expectedPath);
                int count = form.FighterProject.Animations.Count;
                using (OnRetargetReady(form, dialog => { })) SimpleControl<Button>(form, "libraryUseOnTarget").PerformClick();
                await SimpleControlsReady(form);
                using (var expected = NodeFactory.FromFile(null, expectedPath) as CHR0Node)
                    Check(form.FighterProject.Animations.Count == count + 1 && NativeDifference(expected, form.FighterEditor.SelectedCHR0) == null &&
                        form.FighterProject.Animations.Last().Retarget.Library.ModelId == selected.ModelId,
                        "Use as source retargets the selected disc costume through original Next into the existing current target with exact native output and provenance");
                Call(form, "SelectLibraryCostume", costumes.Single(c => c.IsCanonical).ModelId);
                Call(form, "OpenLinkedComparison");
                Check(((LibrarySourceLease)Field(form, "_comparisonLease")).Reference.ModelId == selected.ModelId,
                    "Recorded comparison retains the transferred costume even after the source browser chooses another one");
                Call(form, "CloseLinkedComparison"); Call(form, "ToggleAnimationLibrary");
                await GameLibrarySelect(form, item.Clip.Id); Call(form, "SelectLibraryCostume", selected.ModelId);
                SimpleControl<Button>(form, "libraryPreviewOriginal").PerformClick();
                form = await GameLibraryOpenTarget(form, library, item.Clip.Id, selected.ModelId, output, false);
                Check(FighterProject.HashFile(image) == discHash && originals.All(p => FighterProject.HashFile(p.Key) == p.Value),
                    "Disc indexing, costume preview, retargeting, editable-target opening and save/export leave the original image and supplied model/motion bytes unchanged");
            }
        }
        finally
        {
            File.WriteAllText(Path.Combine(output, "game-library.json"), new JavaScriptSerializer().Serialize(new {
                DiscSha256 = discHash, ReaderProvider = BrawlDiscImage.Provider, ExternalReaderRequired = false, PrivateDesktop, OSInputInjected = false, ScreenCopied = false,
                PhysicalInputVerified = false, MonitorDpiVerified = false, GameRuntimeVerified = false
            }));
            if (form != null && !form.IsDisposed) SimpleDisposeForm(form);
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previous);
        }
    }
}
