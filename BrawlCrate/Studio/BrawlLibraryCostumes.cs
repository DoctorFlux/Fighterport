using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BrawlCrate.Studio
{
    /// <summary>An immutable view choice; it never changes a clip's registered association.</summary>
    public sealed class LibraryCostumeOption
    {
        public string ModelId { get; internal set; }
        public string Name { get; internal set; }
        public string Variant { get; internal set; }
        public string Label { get; internal set; }
        public bool IsCanonical { get; internal set; }
        // Compatibility of recorded native identities only. Open independently
        // checks current source availability, locks, hashes and native decoding.
        public bool Available { get; internal set; }
        public string UnavailableReason { get; internal set; }
        public override string ToString() => Label;
    }

    internal static class BrawlLibraryCostumes
    {
        private const int MaximumChoices = 256;
        private static readonly Regex CostumeFile = new Regex(@"^(?<family>Fit.+?)(?<slot>[0-9]{2})(?:_[A-Za-z]{2})?\.(pac|pcs)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static bool NativeBrawl(LibraryModel model, LibraryMount modelMount, LibraryAnimation clip, LibraryMount motionMount) =>
            model != null && modelMount.Provider == BrawlLibrarySource.Provider && motionMount.Provider == BrawlLibrarySource.Provider &&
            model.RestInterpretation == "brawl-mdl0-bind-v1" && clip.Channel == "CHR0 skeletal" &&
            (clip.NativeVersion == 4 || clip.NativeVersion == 5) && clip.FrameCount >= 1 && clip.FrameCount <= 4096;

        private static string CharacterFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string[] parts = path.Split('/');
            return parts.Length >= 3 && parts[0] == "fighter" ? parts[0] + "/" + parts[1] : null;
        }

        private static bool SameInterpretation(LibraryMount a, LibraryMount b) =>
            a.Game == b.Game && a.GameVersion == b.GameVersion && a.Platform == b.Platform && a.Provider == b.Provider;

        private static bool SameSourceSet(LibraryCatalogue data, LibraryModel canonical, LibraryMount canonicalMount,
            LibraryModel candidate, LibraryMount candidateMount, LibraryAnimation clip, LibraryMount motionMount)
        {
            if (!SameInterpretation(canonicalMount, candidateMount) || !SameInterpretation(canonicalMount, motionMount) ||
                canonical.Character != candidate.Character || canonical.RestInterpretation != candidate.RestInterpretation) return false;
            if (canonicalMount.GameSourceId == null)
            {
                // Ordinary files have no authoritative roster folder. Only an
                // explicitly registered model/motion association can supply an
                // alternative; matching a character label is not ownership.
                return candidateMount.GameSourceId == null && motionMount.GameSourceId == null &&
                    candidate.MotionMountIds.Contains(clip.MountId);
            }
            var location = data.GameLocations.SingleOrDefault(l => l.Id == canonicalMount.GameSourceId);
            if (location == null || !location.Indexed ||
                (location.Provider != "brawl-folder-v1" && location.Provider != "dolphin-brawl-disc-v1") ||
                candidateMount.GameSourceId != location.Id || motionMount.GameSourceId != location.Id) return false;
            string folder = CharacterFolder(canonicalMount.VirtualPath);
            return folder != null && CharacterFolder(candidateMount.VirtualPath) == folder && CharacterFolder(motionMount.VirtualPath) == folder;
        }

        internal static LibraryCostumeOption[] Choices(LibraryCatalogue data, LibraryAnimation clip)
        {
            var canonical = data.Models.SingleOrDefault(m => m.Id == clip.ModelId);
            if (canonical == null) return new LibraryCostumeOption[0];
            var mounts = data.Mounts.ToDictionary(m => m.Id);
            var canonicalMount = mounts[canonical.MountId]; var motionMount = mounts[clip.MountId];
            if (!NativeBrawl(canonical, canonicalMount, clip, motionMount)) return new LibraryCostumeOption[0];
            var canonicalFile = CostumeFile.Match(Path.GetFileName(canonicalMount.VirtualPath ?? canonicalMount.LocalPath));
            var result = new List<LibraryCostumeOption>();
            foreach (var model in data.Models)
            {
                bool original = model.Id == canonical.Id;
                var mount = mounts[model.MountId];
                if (!original)
                {
                    if (!model.HasGeometry || model.Shadow || !SameSourceSet(data, canonical, canonicalMount, model, mount, clip, motionMount)) continue;
                    if (canonicalMount.GameSourceId != null)
                    {
                        // Brawl's filename convention inventories costumes. It
                        // never proves rig compatibility: the exact reviewed
                        // hierarchy and bind fingerprint below is separate.
                        var file = CostumeFile.Match(Path.GetFileName(mount.VirtualPath ?? mount.LocalPath));
                        if (!canonicalFile.Success || !file.Success ||
                            !string.Equals(file.Groups["family"].Value, canonicalFile.Groups["family"].Value, StringComparison.OrdinalIgnoreCase)) continue;
                    }
                }
                string reason = null;
                if (!original && model.NativeVersion != canonical.NativeVersion)
                    reason = "Different native model version; costume compatibility needs review.";
                else if (!original && model.SkeletonSha256 != canonical.SkeletonSha256)
                    reason = "Different native hierarchy or bind transforms; costume compatibility needs review.";
                string variant = model.Variant ?? Path.GetFileName(mount.VirtualPath ?? mount.LocalPath);
                string label = variant + " · " + model.Name + (original ? " (original association)" : "");
                if (reason != null) label += " (needs review)";
                if (result.Count >= MaximumChoices)
                    throw new InvalidDataException("This source contains more than 256 costume model candidates for one native animation. Narrow the source inventory and review its duplicate/ambiguous models.");
                result.Add(new LibraryCostumeOption { ModelId = model.Id, Name = model.Name, Variant = variant,
                    Label = label, IsCanonical = original, Available = reason == null, UnavailableReason = reason });
            }
            foreach (var group in result.GroupBy(c => c.Label, StringComparer.Ordinal).Where(g => g.Count() > 1))
                foreach (var choice in group) choice.Label += " [" + choice.ModelId.Substring(0, 8) + "]";
            return result.OrderByDescending(c => c.IsCanonical).ThenBy(c => c.Variant, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.ModelId, StringComparer.Ordinal).ToArray();
        }

        internal static string AssociationEvidence(LibraryAnimation clip, LibraryModel model) =>
            clip.AssociationEvidence + "; selected Brawl costume " + model.Id +
            "; exact same native source/build ownership, hierarchy and bind fingerprint; original costume geometry and weights retained";
    }
}
