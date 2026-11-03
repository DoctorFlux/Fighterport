using System;
using System.IO;
using System.Linq;

namespace BrawlCrate.Studio.Retargeting
{
    // A session review is bound to the exact native bind rig and represented
    // skinned vertices. It does not create a joint or alter the MDL0.
    public sealed class CranialAnchorReview
    {
        public string RigAndSkinDigest { get; }
        public string NeckBoneName { get; }
        public string Evidence { get; }

        public CranialAnchorReview(string rigAndSkinDigest, string neckBoneName, string evidence)
        {
            if (string.IsNullOrEmpty(rigAndSkinDigest) || rigAndSkinDigest.Length != 64 ||
                rigAndSkinDigest.Any(c => !Uri.IsHexDigit(c)) || string.IsNullOrWhiteSpace(neckBoneName) ||
                string.IsNullOrWhiteSpace(evidence) || evidence.Length > 4096)
                throw new InvalidDataException("The cranial review needs an exact rig/skin digest, neck identity and bounded human evidence.");
            RigAndSkinDigest = rigAndSkinDigest.ToLowerInvariant();
            NeckBoneName = neckBoneName;
            Evidence = evidence;
        }
    }

    public static partial class BrawlRetargeter
    {
        // Run only during offline preparation. Both the native bake and its
        // independent witness use the same measured target head point/radius.
        internal static CranialSurfaceMetric PrepareCranialPair(BrawlRig source, BrawlRig target)
        {
            if (source?.CranialReview == null) return null;
            if (target?.NativeModel == null || target.NativeModel.NativeEditingBackend != null)
                throw new InvalidDataException("A reviewed virtual cranial source needs a target with comparable native skinned head evidence.");
            CranialHeadSkinMeasurement measured = MeasureMappedHeadSkin(target.NativeModel, target);
            if (!measured.Available)
                throw new InvalidDataException("The target head skin cannot be compared with the reviewed source cranial patch: " + measured.RejectionReason);
            return new CranialSurfaceMetric(measured.HeadIndex,measured.BindLocalPoint,measured.Radius,
                "Exact native target direct-head skin on rig " + target.Fingerprint);
        }
    }
}
