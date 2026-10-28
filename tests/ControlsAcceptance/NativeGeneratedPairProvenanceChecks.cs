using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

internal static partial class Program
{
    static void NativeGeneratedPairProvenanceChecks(string output)
    {
        Directory.CreateDirectory(output);
        string rig = new string('A', 64), otherRig = new string('B', 64);
        CHR0Node Fixture(string name, out NativeGeneratedPairEvidence[] known)
        {
            var clip = new CHR0Node { Name = name, Version = 4, FrameCount = 5, Loop = false };
            var result = new List<NativeGeneratedPairEvidence>();
            foreach (string bone in new[] { "existing body", "reviewed motion control", "unknown authored pair" })
            {
                var entry = clip.CreateEntry(bone);
                // A present native rotation group stores all three axis cells.
                // Freeze its fixed siblings explicitly, as an imported CHR0
                // or the independently reopened retarget master already does.
                entry.Keyframes.SetFrameValue(4, 0, 0, true)._tangent = 0;
                entry.Keyframes.SetFrameValue(5, 0, 0, true)._tangent = 0;
                entry.Keyframes.SetFrameValue(3, 0, 0, true)._tangent = 0;
                var first = entry.Keyframes.SetFrameValue(3, 2, 1, true); first._tangent = .02f;
                var second = entry.Keyframes.SetFrameValue(3, 2, 1, true); second._tangent = .04f;
                entry.Keyframes.SetFrameValue(3, 4, 2, true)._tangent = 0;
                if (bone != "unknown authored pair") result.Add(new NativeGeneratedPairEvidence(entry, 3, first, second));
            }
            var body = (CHR0EntryNode)clip.Children[0];
            body.KeyArrays[4].Remove(0);
            var a = body.Keyframes.SetFrameValue(4, 1.125f, 3, true); a._tangent = .01f;
            var b = body.Keyframes.SetFrameValue(4, 1.125f, 3, true); b._tangent = .03f;
            result.Add(new NativeGeneratedPairEvidence(body, 4, a, b));
            PreciseCHR0EntryNode.Promote(clip); known = result.ToArray(); return clip;
        }
        var registry = new NativeGeneratedPairRegistry();
        using (var source = Fixture("procedural writer provenance", out var known))
        {
            string original = NativeAnimationOptimization.SourceContentKey(source);
            Check(registry.Register(source, rig, known).Length == 3 && registry.ClipCount == 1 && registry.SiteCount == 3,
                "Generated pair provenance: only explicitly captured ordered writer tuples enter the bounded session registry");
            var all = registry.Freeze(source, rig, new string[0], new int[0], out string available);
            Check(all.Length == 3 && all.Any(site => site.Channel == 4 && ReplayFloatBits(site.Frame) == ReplayFloatBits(1.125f)) &&
                all.All(site => site.Bone != "unknown authored pair") && available.Contains("session"),
                "Generated pair provenance: exact fractional sites survive typed freezing while an identical-looking authored pair remains unknown");
            Check(registry.Resolve(source, otherRig).Length == 0 && registry.Resolve(source, "not a fingerprint").Length == 0,
                "Generated pair provenance: a different target rig or invalid identity cannot reuse the session evidence");
            using (var copy = NativeAnimationOptimization.CopySource(source))
                Check(registry.Resolve(copy, rig).Length == 3, "Generated pair provenance: independent owned copies resolve only through the same exact native content and rig");

            var body = (CHR0EntryNode)source.Children[0]; var authored = body.KeyArrays[3].Entries.Where(k => k._index == 2).First();
            float old = authored._tangent; authored._tangent = .021f;
            Check(registry.Resolve(source, rig).Length == 0, "Generated pair provenance: a manual coefficient edit invalidates the whole exact-content lookup");
            authored._tangent = old;
            Check(registry.Resolve(source, rig).Length == 3 && NativeAnimationOptimization.SourceContentKey(source) == original,
                "Generated pair provenance: restoring the exact previous content permits session evidence recovery as native Undo does");
            using (var fitted = NativeAnimationOptimization.CopySource(source))
            {
                var changed = (CHR0EntryNode)fitted.Children[0];
                changed.KeyArrays[4].Entries.First()._tangent = .011f;
                Check(registry.Carry(source, fitted, rig).Length == 2 && registry.Resolve(fitted, rig).All(site => site.Channel != 4),
                    "Generated pair provenance: fitting carries only bit-identical known pairs and excludes every changed side");
                changed.ScaleCompensateApply = !changed.ScaleCompensateApply;
                Check(registry.Carry(source, fitted, rig).All(site => site.Bone != changed.Name),
                    "Generated pair provenance: changed native semantic flags cannot inherit that track's pair evidence");
            }
            Check(registry.Freeze(source, rig, new[] { "reviewed motion control" }, new int[0], out _).Length == 2,
                "Generated pair provenance: reviewed exact motion controls override known generated sites");
            Check(registry.Freeze(source, rig, new[] { "existing body" }, new int[0], out _).All(site => site.Bone != "existing body"),
                "Generated pair provenance: explicit protected pin/contact dependencies override known generated sites");
            Check(registry.Freeze(source, rig, new string[0], new[] { 2 }, out _).Length == 1 &&
                registry.Freeze(source, rig, new[] { "existing body" }, new[] { 2 }, out _).Length == 0,
                "Generated pair provenance: exact fitting boundaries remain protected before the shared reducer sees any eligible sites");

            string path = Path.Combine(output, "exact-session-source.chr0"); source.ExportChecked(path);
            using (var reopened = (CHR0Node)NodeFactory.FromFile(null, path))
            {
                Check(NativeAnimationOptimization.SourceContentKey(reopened) == original,
                    "Generated pair provenance: independent native reopen preserves every original channel, time, value, tangent and semantic flag before evidence lookup");
                Check(registry.Resolve(reopened, rig).Length == 3 && new NativeGeneratedPairRegistry().Resolve(reopened, rig).Length == 0,
                    "Generated pair provenance: same-process exact reopen resolves; a fresh process-session registry conservatively preserves unknown pairs");
            }
            using (var renamed = NativeAnimationOptimization.CopySource(source))
            {
                renamed.Name += " manually renamed";
                Check(registry.Resolve(renamed, rig).Length == 0 && registry.Carry(source, renamed, rig).Length == 0,
                    "Generated pair provenance: manual identity/timing changes do not silently rebase historical writer evidence");
            }
            using (var signed = NativeAnimationOptimization.CopySource(source))
            {
                var entry = (CHR0EntryNode)signed.Children[0];
                foreach (var key in entry.KeyArrays[5].Entries.ToArray()) entry.KeyArrays[5].Remove(key._index);
                float negativeZero = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
                var first = entry.Keyframes.SetFrameValue(5, negativeZero, 1, true); first._tangent = negativeZero;
                var second = entry.Keyframes.SetFrameValue(5, negativeZero, 1, true); second._tangent = .01f;
                var zeroEvidence = new NativeGeneratedPairEvidence(entry, 5, first, second);
                registry.Register(signed, rig, new[] { zeroEvidence });
                Check(registry.Resolve(signed, rig).Length == 1 && registry.Freeze(signed, rig, new string[0], new int[0], out _).Length == 0,
                    "Generated pair provenance: signed-zero tuple evidence is retained exactly but never enables approximate paired-side reduction");
            }
            var bounded = new NativeGeneratedPairRegistry(2, 2);
            Check(bounded.Register(source, rig, known).Length == 0 && bounded.SiteCount == 0 && bounded.ClipCount == 0,
                "Generated pair provenance: over-budget site capture retains unknown protections without partial registration");
            using (var second = Fixture("second bounded record", out var secondKnown))
            using (var third = Fixture("third bounded record", out var thirdKnown))
            {
                bounded.Register(source, rig, known.Take(1)); bounded.Register(second, rig, secondKnown.Take(1)); bounded.Register(third, rig, thirdKnown.Take(1));
                Check(bounded.SiteCount == 2 && bounded.ClipCount == 2 && bounded.Resolve(source, rig).Length == 0 && bounded.Resolve(third, rig).Length == 1,
                    "Generated pair provenance: bounded session eviction cannot silently reconstruct missing provenance from current pairs");
            }
            bool cancelled = false; var stop = new CancellationTokenSource(); stop.Cancel();
            try { registry.Register(source, rig, known, stop.Token); } catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && NativeAnimationOptimization.SourceContentKey(source) == original,
                "Generated pair provenance: cancellation leaves the owned full-quality animation and its historical content unchanged");
        }
    }
}
