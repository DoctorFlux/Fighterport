using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private sealed class LibraryTargetContext
        {
            internal string CatalogueDirectory, ClipId, ModelId;
            internal byte[] TemporaryIndex, TemporaryPreferences;
            internal LibraryRegistration Registration;
        }
        private LibraryTargetContext _libraryTargetPreview;
        private AnimationLibrary _libraryTargetPreviewCatalogue;
        private bool _libraryTargetPreviewQueued;

        // Full original containers preserve native companion animations and
        // texture/material references. Never pass a borrowed library node into
        // the editable scene, select by name, or create a retarget provenance.
        internal static FighterProject PrepareLibraryTarget(LibrarySourceLease source)
        {
            if (source?.Reference == null || source.Model == null || source.Animation == null ||
                source.ModelLock == null || source.MotionLock == null)
                throw new InvalidDataException("Open a verified library model and animation first.");
            var reference = source.Reference;
            var project = FighterProject.Create(source.Model.Name);
            try
            {
                var modelAsset = project.StageAsset(source.ModelLock.Name, "model");
                var motionAsset = project.StageAsset(source.MotionLock.Name, "animation");
                if (modelAsset.SourceSha256 != reference.ModelContainerSha256 ||
                    motionAsset.SourceSha256 != reference.MotionContainerSha256)
                    throw new InvalidDataException("The original model or motion container changed while preparing its independent target copy.");
                using (var modelRoot = NodeFactory.FromFile(null, modelAsset.WorkingPath))
                using (var motionRoot = NodeFactory.FromFile(null, motionAsset.WorkingPath))
                {
                    if (modelRoot == null || motionRoot == null)
                        throw new InvalidDataException("The staged native model or motion container could not be opened.");
                    var model = BrawlLibrarySource.Resolve(modelRoot, reference.ModelKey) as MDL0Node;
                    var clip = BrawlLibrarySource.Resolve(motionRoot, reference.MotionKey) as CHR0Node;
                    if (model == null || clip == null ||
                        BrawlLibrarySource.ResourceHash(model) != reference.ModelResourceSha256 ||
                        BrawlRetargeter.SkeletonFingerprint(model) != reference.SourceRig ||
                        BrawlLibrarySource.ResourceHash(clip) != reference.MotionResourceSha256 ||
                        model.Name != source.Model.Name || model.Version != source.Model.Version ||
                        clip.Name != source.Animation.Name || clip.Version != reference.NativeVersion ||
                        clip.FrameCount != reference.FrameCount || clip.Loop != reference.Loop)
                        throw new InvalidDataException("The independent target does not match the exact selected library model, skeleton, animation or timing.");
                    // Existing projects persist model TreePath identities.
                    // Refuse an ambiguous path before closing the old target;
                    // a first-by-name fallback could silently select a sibling.
                    if (ModelPanel.CollectModels(modelRoot).Count(candidate =>
                        ((ResourceNode)candidate).TreePath == model.TreePath) != 1)
                        throw new InvalidDataException("This container has duplicate model paths. Its exact selected model cannot be reopened safely by the current project format.");
                    project.Model = modelAsset;
                    project.ModelNodePath = model.TreePath;
                    project.Animations.Add(motionAsset);
                    project.SelectedAnimation = FighterAnimationKey(motionAsset, clip);
                    project.Frame = 1;
                }
                ValidateFighterProjectAssets(project);
                return project;
            }
            catch { project.Dispose(); throw; }
        }

        private static byte[] ReadLibraryTargetMetadata(string path, int limit)
        {
            AnimationLibrary.NoLinks(path);
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (file.Length < 1 || file.Length > limit)
                    throw new InvalidDataException("The temporary source catalogue exceeds its metadata bound.");
                var bytes = new byte[checked((int)file.Length)];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = file.Read(bytes, offset, bytes.Length - offset);
                    if (read == 0) throw new EndOfStreamException("The source catalogue changed while it was captured.");
                    offset += read;
                }
                return bytes;
            }
        }

        private LibraryTargetContext CaptureLibraryTargetContext(LibrarySourceLease source)
        {
            var result = new LibraryTargetContext {
                CatalogueDirectory = _library.DirectoryPath,
                ClipId = source.Reference.ClipId, ModelId = source.Reference.ModelId
            };
            if (!_sessionSourceCatalogues.Contains(_library)) return result;
            // An old window's worker directory is deleted during replacement.
            // Hold bounded metadata in memory until the NEW window can place
            // it beneath its own worker lifetime. No project schema is added.
            result.TemporaryIndex = ReadLibraryTargetMetadata(_library.IndexPath, 64 * 1024 * 1024);
            string preferences = Path.Combine(_library.DirectoryPath, "preferences.json");
            if (File.Exists(preferences))
            {
                try { result.TemporaryPreferences = ReadLibraryTargetMetadata(preferences, 2 * 1024 * 1024); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is InvalidDataException)
                { result.TemporaryPreferences = null; }
            }
            result.Registration = new LibraryRegistration {
                ModelPath = source.ModelLock.Name, MotionPath = source.MotionLock.Name,
                ModelKey = source.Reference.ModelKey, Character = source.Model.Name,
                Game = source.Reference.Game, GameVersion = source.Reference.GameVersion,
                BuildEvidence = source.Reference.BuildEvidence, ApproveAssociation = true
            };
            return result;
        }

        private void OpenLibraryAsTarget()
        {
            if (SimpleWorkspaceBusy) throw new InvalidOperationException("Finish or cancel the current operation before opening another target.");
            var item = LibrarySelection;
            if (item == null || !SimpleWorkspaceSourceReady)
                throw new InvalidOperationException("Select an available original library model and animation first.");
            var originalProject = _fighterProject;
            var catalogue = _library;
            string clipId = item.Clip.Id;
            string modelId = SelectedLibraryCostume?.ModelId ?? item.Model?.Id;
            bool SelectionIsCurrent() => !IsDisposed && ReferenceEquals(originalProject, _fighterProject) &&
                ReferenceEquals(catalogue, _library) && LibrarySelection?.Clip.Id == clipId &&
                (SelectedLibraryCostume?.ModelId ?? LibrarySelection?.Model?.Id) == modelId;
            FighterProject prepared = null;
            LibraryTargetContext context = null;
            PauseSourceOperation();
            _workspaceBusy = true; UpdateSimpleWorkspace();
            try
            {
                using (var source = LoadSelectedLibrarySource())
                {
                    if (source == null) return;
                    if (!SelectionIsCurrent() || source.Reference.ClipId != clipId || source.Reference.ModelId != modelId)
                        throw new InvalidOperationException("The source or current project changed. Choose the exact library item again.");
                    prepared = LibraryWork.Run(this, "Preparing Editable Library Target", (cancel, progress) =>
                    {
                        FighterProject candidate = null;
                        try
                        {
                            cancel.ThrowIfCancellationRequested();
                            progress.Report("Copying and verifying the exact original model and motion containers...");
                            candidate = PrepareLibraryTarget(source);
                            cancel.ThrowIfCancellationRequested();
                            return candidate;
                        }
                        catch { candidate?.Dispose(); throw; }
                    });
                    if (prepared == null) return;
                    if (!SelectionIsCurrent())
                        throw new InvalidOperationException("The source or current project changed during preparation. Choose the exact library item again.");
                    context = CaptureLibraryTargetContext(source);
                }
            }
            catch { prepared?.Dispose(); throw; }
            finally
            {
                _workspaceBusy = false;
                if (!IsDisposed) UpdateSimpleWorkspace();
            }
            try
            {
                if (!SelectionIsCurrent())
                    throw new InvalidOperationException("The source or current project changed. The prepared target was not opened.");
                // The existing Save/Discard/Cancel decision happens only after
                // the selected pair has been copied and independently decoded.
                if (MainForm.Instance.OpenLibraryFighterProject(prepared, out var opened))
                {
                    prepared = null; // New ModelForm now owns its project.
                    opened.BeginLibraryTargetPreview(context);
                }
            }
            finally { prepared?.Dispose(); }
        }

        private void BeginLibraryTargetPreview(LibraryTargetContext context)
        {
            try
            {
                if (_libraryPane == null) BuildLibraryPane();
                AnimationLibrary catalogue;
                if (context.TemporaryIndex != null)
                {
                    string directory = Path.Combine(_controlStage, "opened-source-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory, "catalogue.xml"), context.TemporaryIndex);
                    if (context.TemporaryPreferences != null)
                        File.WriteAllBytes(Path.Combine(directory, "preferences.json"), context.TemporaryPreferences);
                    catalogue = new AnimationLibrary(directory);
                    catalogue.Read();
                    _sessionSourceLibrary = catalogue; _sessionSourceRegistration = context.Registration;
                    _sessionSourceCatalogues.Add(catalogue);
                }
                else catalogue = string.Equals(_sharedSourceLibrary.DirectoryPath, context.CatalogueDirectory, StringComparison.OrdinalIgnoreCase)
                    ? _sharedSourceLibrary : new AnimationLibrary(context.CatalogueDirectory);
                _libraryTargetPreview = context; _libraryTargetPreviewCatalogue = catalogue;
                ActivateSourceCatalogue(catalogue, context.ClipId);
            }
            catch (Exception error)
            {
                _libraryTargetPreview = null;
                _fighterStatus.Text = "Original source preview unavailable: " + error.Message + " The independent target remains editable.";
            }
        }

        // Called by the ordinary presentation update after the exact async tree
        // selection and initial controls preparation settle. No pending request
        // means no work; ordinary scrubbing/playback never loads source data.
        private void CompleteLibraryTargetPreview()
        {
            var context = _libraryTargetPreview;
            if (context == null || _libraryTargetPreviewQueued || SimpleWorkspaceBusy || modelEditControl1.Playing ||
                _libraryReplacingTree || _librarySearchTimer?.Enabled == true || LibrarySelection?.Clip.Id != context.ClipId ||
                !ReferenceEquals(_libraryTargetPreviewCatalogue, _library)) return;
            _libraryTargetPreviewQueued = true;
            BeginInvoke(new Action(() =>
            {
                _libraryTargetPreviewQueued = false;
                if (IsDisposed || _libraryTargetPreview != context) return;
                if (SimpleWorkspaceBusy) return; // Busy completion updates presentation again.
                _libraryTargetPreview = null;
                if (modelEditControl1.Playing || !ReferenceEquals(_libraryTargetPreviewCatalogue, _library) ||
                    LibrarySelection?.Clip.Id != context.ClipId) return;
                try
                {
                    SelectLibraryCostume(context.ModelId);
                    PreviewLibraryOriginal();
                }
                catch (Exception error)
                { _fighterStatus.Text = "Original source preview unavailable: " + error.Message + " The independent target remains editable."; }
            }));
        }
    }
}
