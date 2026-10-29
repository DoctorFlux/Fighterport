using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    public static partial class BrawlRetargeter
    {
        public const string TrackMembershipPolicy = "shared-mapped-bones-v1";

        // The native bake uses temporary dense bind rows for its contact sampler.
        // They are not output animation tracks. Only a source/target mapping can
        // authorize an authored target entry; identical names are not evidence
        // for a new role, and reviewed differently named equivalents remain valid.
        internal static void RestrictOutputToSharedBones(RetargetResult result, BrawlRig target, CancellationToken cancellation)
        {
            int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value),0);
            bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
            var mapped = new HashSet<string>(result.Plan.Mapping.Select(m => m.Target), StringComparer.Ordinal);
            var bones = target.Bones.ToDictionary(b => b.Name, StringComparer.Ordinal);
            var omitted = new List<string>();
            var remove = new List<CHR0EntryNode>();
            foreach (var entry in result.Animation.Children.OfType<CHR0EntryNode>())
            {
                cancellation.ThrowIfCancellationRequested();
                if (mapped.Contains(entry.Name)) continue;
                if (!bones.TryGetValue(entry.Name, out var bone))
                    throw new InvalidDataException("Retarget output contains an entry outside its target rig: " + entry.Name);
                if (entry.ScaleCompensateApply != bone.SegScaleCompApply || entry.ScaleCompensateParent != bone.SegScaleCompParent || entry.ClassicScaleOff != !bone.ClassicScale)
                    throw new InvalidDataException("A target-only entry has different native transform flags: " + entry.Name + ". No retarget result was imported.");
                // No tolerance, role-name guess or key-value-only test: zero
                // tangents and authentic rest values prove each temporary local
                // Hermite function constant, including its loop closing interval.
                // An unexpected generated correction is a failed preparation,
                // never silent motion loss or permission to key target-only bones.
                for (int channel = 0; channel < 9; channel++)
                    if (!Finite(bone.BindState[channel]) || entry.KeyArrays[channel].Entries.Any(k =>
                        !Finite(k._index) || Bits(k._index) == int.MinValue || !Finite(k._value) || !Finite(k._tangent) ||
                        Bits(k._value) != Bits(bone.BindState[channel]) || Bits(k._tangent) != 0))
                        throw new InvalidDataException("A target-only bone acquired non-bind motion during preparation: " + entry.Name + ". No retarget result was imported.");
                remove.Add(entry); omitted.Add(entry.Name);
            }
            cancellation.ThrowIfCancellationRequested();
            foreach (var entry in remove) entry.Remove();
            result.OmittedTargetBones = omitted.ToArray();
            result.OptimizationReport = (result.OptimizationReport ?? "") + "\r\nTrack membership: " + TrackMembershipPolicy +
                ". " + omitted.Count + " target-only bones remain unanimated at native local rest; animated parents still carry them.\r\n" +
                (omitted.Count == 0 ? "" : "Omitted target-only entries: " + string.Join(", ", omitted) + ".\r\n") +
                "Mapped body-proportion corrections remain; lack of an authored local source key is not proof that a mapped target needs no derived motion.";
        }
    }
}
