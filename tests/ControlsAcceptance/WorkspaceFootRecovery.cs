using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Independent native oracle: constant authentic first BODY channels, with
    // original protected tracks restored exactly. ApplyCHR evaluates their FK;
    // assigning FrameState alone would not recalculate native world matrices.
    static CHR0Node RecoveryReferenceUnderSourceRoots(MDL0Node model, CHR0Node source, CHR0Node reference, string[] roots)
    {
        var result = new CHR0Node { Name = "Procedural first-body source-root oracle", FrameCount = source.FrameCount, Loop = source.Loop };
        foreach (var bone in model._linker.BoneCache)
        {
            if (roots.Contains(bone.Name)) continue;
            var state = RecoverySample(reference, bone, 0);
            var track = result.CreateEntry(bone.Name);
            for (int channel = 0; channel < 9; channel++) track.SetKeyframe(channel, 0, state[channel], true, true)._tangent = 0;
        }
        var snapshotType = typeof(ModelForm).GetNestedType("NativeControlSnapshot", BindingFlags.NonPublic);
        var frozen = snapshotType.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { source, roots });
        snapshotType.GetMethod("Restore", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(frozen, new object[] { result });
        Check(RecoveryKeys(source, roots) == RecoveryKeys(result, roots), "Independent reference-body oracle retains exact source-root tracks, paired keys, tangents and flags");
        return result;
    }

    static void RecoveryVerifyNativeFloor(MDL0Node model, CHR0Node source, CHR0Node fitted, CHR0Node alignedReference, ControlReply setup, int join, string output)
    {
        var vertices = model.PolygonList.OfType<MDL0ObjectNode>().Where(o => o.Vertices != null).SelectMany(o => o.Vertices).ToArray();
        var regions = new Dictionary<string, int[]>();
        foreach (var chain in setup.Chains.Where(c => c.Reviewed && (c.Role == "Foot.L" || c.Role == "Foot.R")))
        {
            var effector = model._linker.BoneCache.Single(b => b.Name == setup.BoneNames[chain.Effector]);
            bool Descends(MDL0BoneNode bone) { for (; bone != null; bone = bone.Parent as MDL0BoneNode) if (ReferenceEquals(bone, effector)) return true; return false; }
            regions[chain.Role] = vertices.Select((v, i) => new { Vertex = v, Index = i }).Where(item => item.Vertex.GetMatrixNode()?.Weights?.Any(w => w.Weight > 0 && w.Bone is MDL0BoneNode bone && Descends(bone)) == true).Select(item => item.Index).ToArray();
        }
        Check(regions.Count == 2 && regions.Values.All(v => v.Length > 0), "Independent native clearance uses every vertex with positive reviewed foot-path influence, without capping or output-dependent selection");
        Dictionary<string, double> Measure(CHR0Node clip, float time)
        {
            model.ResetToBindState(); model.ApplySHP(null, 0); model.ApplyCHR(clip, time + 1);
            return regions.ToDictionary(r => r.Key, r => r.Value.Min(i => (double)vertices[i].WeightedPosition._y));
        }
        var times = Enumerable.Range(join * 64, (source.FrameCount - 1 - join) * 64 + 1).Select(i => i / 64f)
            .Concat(Enumerable.Range(join, source.FrameCount - 1 - join).SelectMany(i => new[] { i + .37f, i + .73f, i + (float)(Math.Sqrt(2) - 1), i + (float)((Math.Sqrt(5) - 1) / 2) })).Distinct().OrderBy(t => t).ToArray();
        var rows = new List<object>(); double maximumAdded = 0;
        foreach (float time in times)
        {
            var original = Measure(source, time); var reference = Measure(alignedReference, time); var actual = Measure(fitted, time);
            foreach (var region in regions)
            {
                double baseline = Math.Min(0, Math.Min(original[region.Key], reference[region.Key]));
                double added = Math.Max(0, baseline - actual[region.Key]); maximumAdded = Math.Max(maximumAdded, added);
                rows.Add(new { Frame = time, Role = region.Key, Source = original[region.Key], ReferenceBodyAtSourceRoots = reference[region.Key], Baseline = baseline, Actual = actual[region.Key], AddedPenetration = added });
            }
        }
        File.WriteAllText(Path.Combine(output, "independent-native-floor.json"), new JavaScriptSerializer { MaxJsonLength = 32000000 }.Serialize(new { FrozenNativeVertexIdentities = regions, SampleCount = times.Length, MaximumAddedPenetration = maximumAdded, Tolerance = LoopPositionTolerance, Rows = rows, Coverage = "1/64 frames plus .37, .73 and irrational held-out fractions; sampled, not continuous-time certification" }));
        Check(maximumAdded <= LoopPositionTolerance, "Independent native skinning adds no penetration beyond source/reference baseline at 1/64 frames and held-out irrational samples");
    }
}
