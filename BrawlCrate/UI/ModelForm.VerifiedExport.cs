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
        // Immutable operation snapshot. It never serializes/rebuilds the live node.
        private sealed class Chr0ExportSnapshot : IDisposable
        {
            internal CHR0Node Clip;
            internal string ExpectedContent;
            internal Action Validate;
            internal CHR0Node Candidate;
            public void Dispose() => Candidate?.Dispose();
        }

        private static string ExportContent(CHR0Node clip)
        {
            var metadata = new JavaScriptSerializer().Serialize(new {
                clip.Name, clip.Version, clip.FrameCount, clip.Loop, OriginalPath = clip.OriginalPath ?? "",
                Users = clip.UserEntries.Cast<UserDataClass>().Select(u => new { u.Name, Type = (int)u.DataType, u.Entries }).ToArray(),
                Order = clip.Children.Select(c => c.Name).ToArray()
            });
            return metadata + NativeControlSnapshot.Capture(clip, clip.Children.Select(c => c.Name)).ContentKey;
        }

        private Chr0ExportSnapshot FreezeWorkspaceExport()
        {
            return FreezeOwnedWorkspaceExport(SelectedFighterAsset(), modelEditControl1.TargetAnimation as CHR0Node, true);
        }

        private Chr0ExportSnapshot FreezeOwnedWorkspaceExport(FighterAsset asset, CHR0Node clip, bool requireSelection, Func<bool> ownBusy = null)
        {
            var target = modelEditControl1.TargetModel as MDL0Node;
            bool Busy() => _controlBusy || _retargetBusy || _pinGesture != null || _workspaceBusy && ownBusy?.Invoke() != true;
            bool Registered() => asset != null && RegisteredFighterAssets().Contains(asset) && _fighterRoots.TryGetValue(asset.Id, out var owner) &&
                ReferenceEquals(owner, clip?.RootNode) && DiscoverFighterAnimations(owner).Any(n => ReferenceEquals(n, clip));
            if (Busy() || target == null || clip == null || !OwnsFighterRoot(clip) || !Registered())
                throw new InvalidOperationException("Select an animation on the current target model.");
            if (asset?.Retarget?.ReviewDraft == true)
                throw new InvalidOperationException("This advanced review draft is blocked and cannot be exported as an accepted result.");
            var project = _fighterProject;
            var root = clip.RootNode;
            string content = ExportContent(clip), rig = BrawlRetargeter.SkeletonFingerprint(target);
            string Provenance() => new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Serialize(new { asset.Retarget, asset.TransitionFit });
            string provenance = Provenance();
            if (asset?.Retarget != null && rig != asset.RigIdentity)
                throw new InvalidOperationException("The target rig changed. Review the animation on its original target before export.");
            var copy = new CHR0Node { Name = clip.Name, Version = clip.Version, FrameCount = clip.FrameCount,
                Loop = clip.Loop, OriginalPath = clip.OriginalPath };
            foreach (UserDataClass user in clip.UserEntries)
                copy.UserEntries.Add(new UserDataClass { Name = user.Name, DataType = user.DataType, Entries = user.Entries.ToArray() });
            NativeControlSnapshot.Capture(clip, clip.Children.Select(c => c.Name)).Restore(copy);
            PreciseCHR0EntryNode.Promote(copy);
            return new Chr0ExportSnapshot { Clip = clip, ExpectedContent = content, Candidate = copy, Validate = () => {
                if (Busy() || !ReferenceEquals(project, _fighterProject) || !Registered() ||
                    !ReferenceEquals(target, modelEditControl1.TargetModel) || requireSelection && !ReferenceEquals(clip, modelEditControl1.TargetAnimation) ||
                    !ReferenceEquals(root, clip.RootNode) || !OwnsFighterRoot(clip) || content != ExportContent(clip) ||
                    rig != BrawlRetargeter.SkeletonFingerprint(target) ||
                    provenance != Provenance())
                    throw new InvalidOperationException("Export dependencies changed. Select Export again to review the current target and animation.");
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
                string priorHash = File.Exists(destination) ? FighterProject.HashFile(destination) : null;
                string temporary = Path.Combine(Path.GetDirectoryName(destination), "." + Guid.NewGuid().ToString("N") + ".chr0");
                try
                {
                    frozen.Candidate.ExportChecked(temporary);
                    using (var decoded = NodeFactory.FromFile(null, temporary) as CHR0Node)
                        if (decoded == null || ExportContent(decoded) != frozen.ExpectedContent)
                            throw new InvalidDataException("Candidate verification failed: native metadata, timing, keys, tangents or flags differ. Existing destination was kept.");
                    frozen.Validate(); ProtectVerifiedExport(destination);
                    string nowHash = File.Exists(destination) ? FighterProject.HashFile(destination) : null;
                    if (priorHash != nowHash) throw new IOException("Destination changed during export. Existing file was kept.");
                    if (priorHash == null) File.Move(temporary, destination);
                    else File.Replace(temporary, destination, null);
                    RecordLibraryExport(frozen.Clip, destination);
                    _fighterStatus.Text = "Verified and exported " + Path.GetFileName(destination) + ". Original sources were preserved.";
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
    }
}
