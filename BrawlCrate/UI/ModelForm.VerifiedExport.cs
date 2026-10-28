using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        internal Action<string> ExportCandidateWritten;
        // Immutable operation snapshot. It never serializes/rebuilds the live node.
        private sealed class Chr0ExportSnapshot : IDisposable
        {
            internal CHR0Node Clip;
            internal string ExpectedContent;
            internal Action Validate;
            internal CHR0Node Candidate;
            internal CHR0Node Master;
            internal string MasterContent;
            internal FrozenExportOptimizationContext Optimization;
            internal string PreparedPolicyKey, OptimizationReport, ByteReport;
            internal string EffectiveExportPolicy = "Exact/reference";
            internal NativeExportOptimizationPolicy RequestedExportPolicy;
            internal string DestinationPath, DestinationHash;
            internal string PreferenceWarning;
            internal string PreparedEncodedSha256;
            internal long PreparedEncodedBytes;
            internal long MasterBytes, ExportBytes;
            internal long PreparationMilliseconds;
            public void Dispose() { if (!ReferenceEquals(Candidate, Master)) Candidate?.Dispose(); Master?.Dispose(); }
        }

        private static string ExportContent(CHR0Node clip)
        {
            var metadata = new JavaScriptSerializer().Serialize(new {
                clip.Name, clip.Version, clip.FrameCount, clip.Loop, ScalingRule = NativeAnimationOptimization.SourceScalingRule(clip), OriginalPath = clip.OriginalPath ?? "",
                Users = clip.UserEntries.Cast<UserDataClass>().Select(u => new { u.Name, Type = (int)u.DataType, u.Entries }).ToArray(),
                Order = clip.Children.Select(c => c.Name).ToArray()
            });
            return metadata + NativeControlSnapshot.Capture(clip, clip.Children.Select(c => c.Name)).ContentKey + "|native:" +
                NativeAnimationOptimization.SourceContentKey(clip) + "|storage:" + NativeAnimationOptimization.StorageKey(clip);
        }

        private FighterAsset RegisteredAnimationOwner(CHR0Node clip)
        {
            if (clip == null) return null;
            FighterAsset result = null;
            foreach (var asset in RegisteredFighterAssets())
                if (_fighterRoots.TryGetValue(asset.Id, out var root) && ReferenceEquals(root, clip.RootNode))
                {
                    if (result != null) throw new InvalidOperationException("The exact native animation has more than one registered owner.");
                    result = asset;
                }
            return result;
        }

        private Chr0ExportSnapshot FreezeWorkspaceExport()
        {
            var clip = modelEditControl1.TargetAnimation as CHR0Node;
            return FreezeOwnedWorkspaceExport(RegisteredAnimationOwner(clip), clip, true);
        }

        private Chr0ExportSnapshot FreezeOwnedWorkspaceExport(FighterAsset asset, CHR0Node clip, bool requireSelection, Func<bool> ownBusy = null)
        {
            var target = modelEditControl1.TargetModel as MDL0Node;
            RequireSavedAnimationExportTarget(target);
            bool Busy() => _controlBusy || _retargetBusy || _pinGesture != null || _workspaceBusy && ownBusy?.Invoke() != true && !ReferenceEquals(_activeSingleExport?.Clip, clip);
            bool Registered() => asset != null && RegisteredFighterAssets().Contains(asset) && _fighterRoots.TryGetValue(asset.Id, out var owner) &&
                ReferenceEquals(owner, clip?.RootNode) && DiscoverFighterAnimations(owner).Any(n => ReferenceEquals(n, clip));
            if (Busy() || target == null || clip == null || !OwnsFighterRoot(clip) || !Registered())
                throw new InvalidOperationException("Select an animation on the current target model.");
            if (asset?.Retarget?.ReviewDraft == true)
                throw new InvalidOperationException("This advanced review draft is blocked and cannot be exported as an accepted result.");
            var project = _fighterProject;
            var root = clip.RootNode;
            string content = ExportContent(clip), rig = BrawlRetargeter.SkeletonFingerprint(target);
            var provenance = ReviewClipRecipe(asset, clip);
            if (asset?.Retarget != null && rig != asset.RigIdentity)
                throw new InvalidOperationException("The target rig changed. Review the animation on its original target before export.");
            var context = FreezeExportOptimizationContext(target, asset, clip);
            // Keep the immutable full-quality master apart from any export-only
            // reduction. Repeated policy changes always start from this master.
            var copy = NativeAnimationOptimization.CopySource(clip);
            return new Chr0ExportSnapshot { Clip = clip, ExpectedContent = ExportContent(copy), Candidate = copy, Master = copy, MasterContent = ExportContent(copy), Optimization = context, Validate = () => {
                RequireSavedAnimationExportTarget(target);
                if (Busy() || !ReferenceEquals(project, _fighterProject) || !Registered() ||
                    !ReferenceEquals(target, modelEditControl1.TargetModel) || requireSelection && !ReferenceEquals(clip, modelEditControl1.TargetAnimation) ||
                    !ReferenceEquals(root, clip.RootNode) || !OwnsFighterRoot(clip) || content != ExportContent(clip) ||
                    rig != BrawlRetargeter.SkeletonFingerprint(target) ||
                    provenance != ReviewClipRecipe(asset, clip))
                    throw new InvalidOperationException("Export dependencies changed. Select Export again to review the current target and animation.");
                context.Validate();
            } };
        }

        private void ProtectVerifiedExport(string destination)
        {
            ProtectCharacterWorkspaceOutput(destination);
            (_library ?? (_library = new AnimationLibrary())).ProtectOutputPath(destination);
            _fighterProject.ProtectOutputPath(destination, true);
            if (_fighterAssets.Values.Any(a => !a.Generated && string.Equals(Path.GetFullPath(a.SourcePath), destination, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Choose a new output file. Original imported sources are protected.");
        }

        private void ExportWorkspaceSnapshot(Chr0ExportSnapshot frozen, string destination)
        {
            {
                if (!string.Equals(Path.GetExtension(destination), ".chr0", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Verified export supports native Brawl CHR0. Other native routes are qualified separately.");
                destination = Path.GetFullPath(destination);
                frozen.Validate(); ProtectVerifiedExport(destination);
                if (!string.Equals(destination, frozen.DestinationPath, StringComparison.OrdinalIgnoreCase)) CaptureWorkspaceExportDestination(frozen, destination);
                string priorHash = frozen.DestinationHash;
                if (priorHash != (File.Exists(destination) ? FighterProject.HashFile(destination) : null))
                    throw new IOException("Destination changed while export was prepared. Existing file was kept.");
                string temporary = Path.Combine(Path.GetDirectoryName(destination), "." + Guid.NewGuid().ToString("N") + ".chr0");
                try
                {
                    frozen.Candidate.ExportChecked(temporary);
                    ExportCandidateWritten?.Invoke(temporary);
                    using (var decoded = NodeFactory.FromFile(null, temporary) as CHR0Node)
                        if (decoded == null || ExportContent(decoded) != frozen.ExpectedContent)
                            throw new InvalidDataException("Candidate verification failed: native metadata, timing, keys, tangents or flags differ. Existing destination was kept.");
                    var bytes = NativeChr0SizeReport.Read(temporary);
                    if (frozen.MasterBytes > 0 && bytes.FileBytes > frozen.MasterBytes)
                        throw new InvalidDataException("The final verified export file is larger than its frozen target master. Existing destination was kept.");
                    string candidateHash = FighterProject.HashFile(temporary);
                    if (frozen.PreparedEncodedSha256 != null && (bytes.FileBytes != frozen.PreparedEncodedBytes || candidateHash != frozen.PreparedEncodedSha256))
                        throw new InvalidDataException("The final export file differs from the independently qualified prepared bytes. Existing destination was kept.");
                    frozen.Validate(); ProtectVerifiedExport(destination);
                    string nowHash = File.Exists(destination) ? FighterProject.HashFile(destination) : null;
                    if (priorHash != nowHash) throw new IOException("Destination changed during export. Existing file was kept.");
                    if (priorHash == null) File.Move(temporary, destination);
                    else File.Replace(temporary, destination, null);
                    frozen.DestinationHash = candidateHash;
                    RecordLibraryExport(frozen.Clip, destination);
                    frozen.ExportBytes = bytes.FileBytes; frozen.ByteReport = bytes.Describe();
                    long masterBytes = frozen.MasterBytes > 0 ? frozen.MasterBytes : bytes.FileBytes;
                    bool fallback = frozen.RequestedExportPolicy == NativeExportOptimizationPolicy.ErrorBounded && !frozen.EffectiveExportPolicy.StartsWith("ErrorBounded", StringComparison.Ordinal);
                    string policy = frozen.EffectiveExportPolicy.StartsWith("ErrorBounded", StringComparison.Ordinal) ? "Error-bounded" : "Exact/reference";
                    _fighterStatus.Text = "CHR0 " + masterBytes + " → " + bytes.FileBytes + " bytes (" + (masterBytes - bytes.FileBytes) + " saved) · " + policy + (fallback ? " fallback" : "") + " · " + Path.GetFileName(destination) + ". Master unchanged.";
                    if (frozen.PreferenceWarning != null) _fighterStatus.Text += " Export settings could not be saved.";
                    string report = _fighterStatus.Text + Environment.NewLine + frozen.ByteReport + Environment.NewLine + frozen.OptimizationReport + Environment.NewLine + frozen.PreferenceWarning;
                    _fighterStatus.ToolTipText = _fighterStatus.AccessibleDescription = BoundedExportReport(report);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        private void CaptureWorkspaceExportDestination(Chr0ExportSnapshot frozen, string destination)
        {
            frozen.Validate(); destination = Path.GetFullPath(destination); ProtectVerifiedExport(destination);
            frozen.DestinationPath = destination;
            frozen.DestinationHash = File.Exists(destination) ? FighterProject.HashFile(destination) : null;
        }

        private static string BoundedExportReport(string report) => report == null || report.Length <= 8192 ? report : report.Substring(0, 8128) + "\r\nAdditional per-bone details omitted from this compact tooltip.";
    }
}
