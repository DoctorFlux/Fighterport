using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Independent dyadic oracle: a small translation-free rotation line and
    // a full 720-degree line. Tiny, distinct one-sided tangents deliberately
    // reproduce adaptive writer pairs without depending on the retarget solver.
    static void NativePairedSourceKeyChecks(MDL0Node model, string output)
    {
        var rig = BrawlRetargeter.Capture(model);
        var plan = new HumanoidRetargeter(rig.Rig, rig.Rig);
        string body = rig.Bones[rig.Rig.Roles["pelvis"]].Name;
        string control = rig.Bones[plan.TargetMotionControls.First()].Name;
        string rigKey = NativeGeneratedPairRegistry.TryRigKey(model, CancellationToken.None);
        string stage = Path.Combine(output, "paired-source-schedules"); Directory.CreateDirectory(stage);
        var json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
        foreach (bool loop in new[] { false, true })
        using (var dense = PairedScheduleMaster(body, control, loop, out var evidence))
        using (var source = SourceKeyQualitySource(rig, dense.FrameCount, new[] { body }))
        {
            source.Loop = loop;
            // The donor has only first/last keys even though the finished
            // target oracle has pairs on every intermediate integer + 1.125.
            var seed = (CHR0EntryNode)source.FindChild(body, false);
            foreach (var array in seed.KeyArrays) while (array.KeyCount > 0) array.Remove(array.Entries.First()._index);
            for (int c = 3; c < 6; c++) foreach (int at in new[] { 0, 16 }) seed.Keyframes.SetFrameValue(c, at, 0, true)._tangent = 0;
            string masterKey = NativeAnimationOptimization.ContentKey(dense), sourceKey = NativeAnimationOptimization.SourceContentKey(source);
            foreach (var mode in new[] { RetargetKeyframeMode.SourceBoneTimes, RetargetKeyframeMode.SourceChannelTimes, RetargetKeyframeMode.Interpolated })
            using (var result = new RetargetResult { Animation = NativeAnimationOptimization.Copy(dense), Plan = plan,
                GeneratedPairRigKey = rigKey, GeneratedPairEvidence = evidence })
            {
                RetargetKeyframes.Prepare(result, rig, rig, source, mode, CancellationToken.None, null);
                var reduced = (CHR0EntryNode)result.Animation.FindChild(body, false);
                Check(reduced.KeyArrays[3].KeyCount == 2 && reduced.KeyArrays[4].KeyCount == 2 && reduced.KeyArrays[5].KeyCount == 1,
                    mode + " / loop=" + loop + ": known continuous generated pairs interpolate across the entire sparse 0-to-16 donor gap");
                Check(reduced.KeyArrays[3].Entries.Select(k => k._index).SequenceEqual(new[] { 0f, 16f }) &&
                    reduced.GetFrameValue(4, 16) - reduced.GetFrameValue(4, 0) == 720,
                    mode + ": source endpoint times and the complete native Euler turn survive without modulo wrapping");
                double oracle = 0;
                for (int n = 0; n <= 4096; n++)
                {
                    float time = n / 256f;
                    oracle = Math.Max(oracle, Math.Abs(reduced.GetFrameValue(3, time) - time / 64d));
                    oracle = Math.Max(oracle, Math.Abs(reduced.GetFrameValue(4, time) - 45d * time));
                }
                var measured = SourceKeyQualityMeasure(model, dense, result.Animation);
                Check(oracle <= .00001 && measured.Euler <= .01 && measured.Orientation <= .01 && measured.EulerDerivative <= .05 &&
                    measured.World <= .0001 * measured.Length && measured.Skin <= .0001 * measured.Length,
                    mode + ": 4097 independent line witnesses and withheld complete native hierarchy/skin probes obey the strict retarget bounds");
                SourceKeysSameTrack((CHR0EntryNode)dense.FindChild(control, false), (CHR0EntryNode)result.Animation.FindChild(control, false),
                    mode + ": source-owned motion control retains all 17 original keys exactly");
                Check(result.KeyframeReport.ScalarCertificateWork > 0 && result.KeyframeReport.ResultBytes < result.KeyframeReport.DenseBytes &&
                    result.Animation.Loop == loop && result.Animation.FrameCount == dense.FrameCount && !result.KeyframeReport.DenseFallback,
                    mode + ": paired interpolation is certified, freshly serialized, smaller and retains native closing timing");
                File.WriteAllText(Path.Combine(stage, "paired-" + loop + "-" + mode + ".json"), json.Serialize(new { OracleError = oracle, Measured = measured, Report = result.KeyframeReport }));
                if (!loop && mode == RetargetKeyframeMode.SourceChannelTimes)
                    PairedScheduleNativeGuards(model, dense, result.Animation, body, control, evidence.Select(e => e.Site()).ToArray(), stage);
            }
            // Writer evidence is authority only when every contextual field and
            // ordered float bit still matches. Each failure keeps that pair.
            foreach (string failure in new[] { "unknown", "wrong-rig", "changed-tangent", "changed-flags", "signed-zero", "jump" })
            using (var result = new RetargetResult { Animation = NativeAnimationOptimization.Copy(dense), Plan = plan,
                GeneratedPairRigKey = failure == "wrong-rig" ? new string('0', 64) : rigKey,
                GeneratedPairEvidence = failure == "unknown" ? new NativeGeneratedPairEvidence[0] : evidence })
            {
                var track = (CHR0EntryNode)result.Animation.FindChild(body, false);
                var sides = track.KeyArrays[3].Entries.Where(k => k._index == 8).ToArray();
                if (failure == "changed-tangent") sides[0]._tangent += 1f / 8192;
                if (failure == "changed-flags") track.ScaleCompensateApply = !track.ScaleCompensateApply;
                if (failure == "signed-zero") sides[0]._tangent = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
                if (failure == "jump") sides[1]._value += .25f;
                float[] exact = sides.SelectMany(k => new[] { k._value, k._tangent }).ToArray();
                RetargetKeyframes.Prepare(result, rig, rig, source, RetargetKeyframeMode.SourceChannelTimes, CancellationToken.None, null);
                var actual = ((CHR0EntryNode)result.Animation.FindChild(body, false)).KeyArrays[3].Entries.Where(k => k._index == 8).ToArray();
                Check(actual.Length == 2 && actual.SelectMany(k => new[] { k._value, k._tangent }).Zip(exact, (a, b) => ReplayFloatBits(a) == ReplayFloatBits(b)).All(x => x),
                    "Generated pair interpolation / " + failure + " / loop=" + loop + ": unknown, stale, signed-zero or discontinuous sides remain ordered and exact");
            }
            using (var result = new RetargetResult { Animation = NativeAnimationOptimization.Copy(dense), Plan = plan, GeneratedPairRigKey = rigKey, GeneratedPairEvidence = evidence })
            using (var cancel = new CancellationTokenSource())
            {
                bool rejected = false;
                try { RetargetKeyframes.Prepare(result, rig, rig, source, RetargetKeyframeMode.SourceChannelTimes, cancel.Token, (p, m) => { if (p >= 92) cancel.Cancel(); }); }
                catch (OperationCanceledException) { rejected = true; }
                Check(rejected && result.KeyframeReport == null && result.DenseAnimation == null && NativeAnimationOptimization.ContentKey(result.Animation) == masterKey,
                    "Paired interpolation cancellation publishes no partial result or modified master");
            }
            Check(NativeAnimationOptimization.ContentKey(dense) == masterKey && NativeAnimationOptimization.SourceContentKey(source) == sourceKey,
                "Paired interpolation preserves the immutable complete target and sparse donor references");
        }
        PairedScheduleEndpointSides(rig, plan, body, control, rigKey, stage);
    }

    static void PairedScheduleEndpointSides(BrawlRig rig, HumanoidRetargeter plan, string body, string control, string rigKey, string stage)
    {
        foreach (bool loop in new[] { false, true })
        using (var dense = PairedScheduleMaster(body, control, loop, out var interiorEvidence))
        using (var source = new CHR0Node { Name = "Independent endpoint seed schedule", FrameCount = dense.FrameCount, Loop = loop })
        {
            var entry = (CHR0EntryNode)dense.FindChild(body, false);
            var seed = source.CreateEntry(body); seed.Keyframes.PreserveInterpolation = true;
            var evidence = interiorEvidence.ToList();
            foreach (int channel in new[] { 3, 4 }) foreach (float time in new[] { 0f, 16f })
            {
                float slope = channel == 3 ? 1f / 64 : 45;
                var first = entry.KeyArrays[channel].GetKeyframe(time);
                // Keep the used outgoing-first / incoming-last slope linear;
                // the outer incoming/outgoing sides are deliberately distinct.
                first._tangent = time == 0 ? slope - .25f : slope;
                var second = entry.Keyframes.SetFrameValue(channel, time, first._value, true);
                second._tangent = time == 0 ? slope : slope + .5f;
                evidence.Add(new NativeGeneratedPairEvidence(entry, channel, first, second));
                seed.Keyframes.SetFrameValue(channel, time, time * slope, true)._tangent = slope;
            }
            seed.Keyframes.SetFrameValue(5, 0, 13.25f, true)._tangent = 0;
            string master = NativeAnimationOptimization.ContentKey(dense);
            foreach (var mode in new[] { RetargetKeyframeMode.SourceChannelTimes, RetargetKeyframeMode.Interpolated })
            using (var result = new RetargetResult { Animation = NativeAnimationOptimization.Copy(dense), Plan = plan,
                GeneratedPairRigKey = rigKey, GeneratedPairEvidence = evidence.ToArray() })
            {
                RetargetKeyframes.Prepare(result, rig, rig, source, mode, CancellationToken.None, null);
                var reduced = (CHR0EntryNode)result.Animation.FindChild(body, false);
                foreach (int channel in new[] { 3, 4 })
                {
                    Check(reduced.KeyArrays[channel].KeyCount == 4 && reduced.KeyArrays[channel].Entries.All(k => k._index == 0 || k._index == 16),
                        mode + " / loop=" + loop + ": interior generated pairs reduce while both endpoint groups retain two sides");
                    foreach (float time in new[] { 0f, 16f })
                    {
                        var expected = entry.KeyArrays[channel].Entries.Where(k => k._index == time).ToArray();
                        var actual = reduced.KeyArrays[channel].Entries.Where(k => k._index == time).ToArray();
                        Check(actual.Length == 2 && actual.Zip(expected, (a, b) => ReplayFloatBits(a._index) == ReplayFloatBits(b._index) &&
                            ReplayFloatBits(a._value) == ReplayFloatBits(b._value) && ReplayFloatBits(a._tangent) == ReplayFloatBits(b._tangent)).All(x => x),
                            mode + ": the complete ordered first/last native value and tangent tuples survive fresh encoding / time=" + time);
                    }
                    foreach (float time in new[] { -.25f, 0, 16, 16.25f, 16.75f })
                        Check(ReplayFloatBits(entry.KeyArrays[channel].GetFrameValue(time)) == ReplayFloatBits(reduced.KeyArrays[channel].GetFrameValue(time)),
                            mode + " / loop=" + loop + ": native outer hold/loop-closing evaluation remains exact at " + time);
                }
                Check(result.Animation.FrameCount == dense.FrameCount && result.Animation.Loop == loop &&
                    result.KeyframeReport.ScalarCertified && result.KeyframeReport.QualityVerified && result.KeyframeReport.ResultBytes < result.KeyframeReport.DenseBytes,
                    mode + ": complete endpoint sides and terminal metadata coexist with certified smaller native storage");
                File.WriteAllText(Path.Combine(stage, "endpoint-pairs-" + loop + "-" + mode + ".json"),
                    new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 }.Serialize(result.KeyframeReport));
            }
            Check(NativeAnimationOptimization.ContentKey(dense) == master, "Endpoint-pair qualification does not mutate the independent finished master");
        }
    }

    static CHR0Node PairedScheduleMaster(string body, string control, bool loop, out NativeGeneratedPairEvidence[] evidence)
    {
        var clip = new CHR0Node { Name = "Independent generated pair schedule", FrameCount = loop ? 16 : 17, Loop = loop, Version = 5 };
        var entry = clip.CreateEntry(body); entry.UseModelRotate = false; entry.Keyframes.PreserveInterpolation = true;
        var known = new List<NativeGeneratedPairEvidence>();
        float[] times = Enumerable.Range(0, 17).Select(n => (float)n).Concat(new[] { 1.125f }).OrderBy(t => t).ToArray();
        foreach (int c in new[] { 3, 4 }) foreach (float at in times)
        {
            float slope = c == 3 ? 1f / 64 : 45;
            float perturbation = c == 3 ? 1f / 65536 : 1f / 16384;
            var first = entry.Keyframes.SetFrameValue(c, at, at * slope, true);
            first._tangent = at == 0 || at == 16 ? slope : slope - perturbation;
            if (at == 0 || at == 16) continue;
            var second = entry.Keyframes.SetFrameValue(c, at, at * slope, true); second._tangent = slope + perturbation;
            known.Add(new NativeGeneratedPairEvidence(entry, c, first, second));
        }
        entry.Keyframes.SetFrameValue(5, 0, 13.25f, true)._tangent = 0;
        var motion = clip.CreateEntry(control); motion.UseModelTranslate = false; motion.Keyframes.PreserveInterpolation = true;
        for (int at = 0; at <= 16; at++) motion.Keyframes.SetFrameValue(6, at, at / 8f, true)._tangent = .125f;
        foreach (int c in new[] { 7, 8 }) motion.Keyframes.SetFrameValue(c, 0, 0, true)._tangent = 0;
        PreciseCHR0EntryNode.Promote(clip); evidence = known.ToArray(); return clip;
    }

    static void PairedScheduleNativeGuards(MDL0Node model, CHR0Node master, CHR0Node candidate, string body, string control,
        NativeGeneratedPairSite[] sites, string stage)
    {
        var tracks = candidate.Children.OfType<CHR0EntryNode>().Select(entry => new ControlTrack { Bone = entry.Name,
            Channels = entry.KeyArrays.Select(array => array.Entries.Select(k => new ControlKey { Frame = k._index, Value = k._value, Tangent = k._tangent }).ToArray()).ToArray() }).ToArray();
        foreach (string guard in new[] { "default", "mandatory", "join", "protected", "exact" })
        {
            bool rejected = false;
            try
            {
                using (var verified = NativeAnimationOptimization.Prepare(model, master, tracks, new NativeAnimationOptimizationOptions {
                    AllowGeneratedPairReduction = guard != "default", GeneratedPairSites = sites, LengthScale = NativeAnimationOptimization.GetBindBoundsDiagonal(model),
                    TargetBones = model.AllBones.Select(b => b.Name).ToArray(), ExactBones = guard == "exact" ? new[] { body, control } : new[] { control },
                    ProtectedBones = guard == "protected" ? new[] { body } : new string[0],
                    MandatoryFrames = guard == "mandatory" ? new[] { 8 } : new int[0], FitJoinFrames = guard == "join" ? new[] { 8 } : new int[0]
                }, stage, CancellationToken.None)) { }
            }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Strict paired reduction: " + guard + " policy rejects omitted exact pair sides before any publication");
        }
    }

    static void SourceKeyAuthenticGapEvidence(CHR0Node source, RetargetResult original, RetargetResult result, string output)
    {
        var protectedControls = new HashSet<string>(result.KeyframeReport.ProtectedControls, StringComparer.Ordinal);
        var sources = source.Children.OfType<CHR0EntryNode>().ToDictionary(e => e.Name, StringComparer.Ordinal);
        var dense = result.DenseAnimation.Children.OfType<CHR0EntryNode>().ToDictionary(e => e.Name, StringComparer.Ordinal);
        var projected = result.Animation.Children.OfType<CHR0EntryNode>().ToDictionary(e => e.Name, StringComparer.Ordinal);
        var known = new Dictionary<(string Bone, int Channel, float Frame), NativeGeneratedPairEvidence>();
        foreach (var group in (original.GeneratedPairEvidence ?? new NativeGeneratedPairEvidence[0]).GroupBy(e => (e.Bone, e.Channel, e.Time)))
        {
            if (group.Count() != 1) continue; var claim = group.First();
            if (!dense.TryGetValue(claim.Bone, out var entry) || !claim.Eligible || !claim.SameContext(entry)) continue;
            // This report is fixture-sized; index the verified pairs once for
            // counting instead of treating equal-looking values as provenance.
            known.Add((claim.Bone, claim.Channel, claim.Time), claim);
        }
        bool KnownPair(CHR0EntryNode entry, int channel, IGrouping<float, BrawlLib.Wii.Animations.KeyframeEntry> group)
        {
            if (entry == null || !known.TryGetValue((entry.Name, channel, group.Key), out var claim)) return false;
            var sides = group.ToArray();
            return sides.Length == 2 && claim.SameContext(entry) && claim.Matches(sides[0], sides[1]);
        }
        var allSourceTimes = new HashSet<float>();
        var rows = new List<SourceKeyGapRow>();
        foreach (var mapping in result.Plan.Mapping.Where(m => !protectedControls.Contains(m.Target)))
        {
            if (!dense.TryGetValue(mapping.Target, out var reference)) continue;
            sources.TryGetValue(mapping.Source, out var donor); projected.TryGetValue(mapping.Target, out var candidate);
            for (int channel = 0; channel < 9; channel++)
            {
                var sourceTimes = new HashSet<float>(donor?.KeyArrays[channel].Entries.Select(k => k._index) ?? Enumerable.Empty<float>());
                foreach (float time in sourceTimes) allSourceTimes.Add(time);
                var before = reference.KeyArrays[channel].Entries.ToArray();
                var after = candidate?.KeyArrays[channel].Entries.ToArray() ?? new BrawlLib.Wii.Animations.KeyframeEntry[0];
                var beforeGroups = before.GroupBy(k => k._index).ToArray(); var afterGroups = after.GroupBy(k => k._index).ToArray();
                rows.Add(new SourceKeyGapRow { SourceBone = mapping.Source, TargetBone = mapping.Target, Channel = channel,
                    SourceHasTrack = donor != null, SourceDistinctTimes = sourceTimes.Count,
                    DenseTimeGroups = beforeGroups.Length, ResultTimeGroups = afterGroups.Length,
                    DenseKnotSides = before.Length, ResultKnotSides = after.Length,
                    DenseAbsentSourceTimeGroups = beforeGroups.Count(g => !sourceTimes.Contains(g.Key)), ResultAbsentSourceTimeGroups = afterGroups.Count(g => !sourceTimes.Contains(g.Key)),
                    DenseAbsentSourceKnotSides = before.Count(k => !sourceTimes.Contains(k._index)), ResultAbsentSourceKnotSides = after.Count(k => !sourceTimes.Contains(k._index)),
                    DensePairedGroups = beforeGroups.Count(g => g.Count() == 2), ResultPairedGroups = afterGroups.Count(g => g.Count() == 2),
                    DenseEligibleWriterPairGroups = beforeGroups.Count(g => KnownPair(reference, channel, g)),
                    ResultEligibleWriterPairGroups = afterGroups.Count(g => KnownPair(candidate, channel, g)) });
            }
        }
        File.WriteAllText(Path.Combine(output, "source-keys-authentic-" + result.KeyframeMode + "-source-gaps.json"),
            new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Serialize(new {
                Version = "authentic-source-gap-observation-v1", Mode = result.KeyframeMode.ToString(), Source = source.Name,
                SourceContent = NativeAnimationOptimization.SourceContentKey(source), DenseContent = result.KeyframeReport.DenseContent, ResultContent = result.KeyframeReport.ResultContent,
                RootControlsExcluded = protectedControls.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
                MappedNonRootChannels = rows.Count, DistinctSourceTimesAcrossMappedChannels = allSourceTimes.Count,
                MappedSourceChannelTimeGroups = rows.Sum(r => r.SourceDistinctTimes),
                DenseAbsentSourceTimeGroups = rows.Sum(r => r.DenseAbsentSourceTimeGroups), ResultAbsentSourceTimeGroups = rows.Sum(r => r.ResultAbsentSourceTimeGroups),
                DenseAbsentSourceKnotSides = rows.Sum(r => r.DenseAbsentSourceKnotSides), ResultAbsentSourceKnotSides = rows.Sum(r => r.ResultAbsentSourceKnotSides),
                DensePairedGroups = rows.Sum(r => r.DensePairedGroups), ResultPairedGroups = rows.Sum(r => r.ResultPairedGroups),
                DenseEligibleWriterPairGroups = rows.Sum(r => r.DenseEligibleWriterPairGroups), ResultEligibleWriterPairGroups = rows.Sum(r => r.ResultEligibleWriterPairGroups),
                Note = "Observed native time groups and ordered knot-side counts for mapped non-root channels. Remaining target keys absent from the donor schedule may be necessary target-space quality repairs or canonical fixed axes. These counts do not assert universal donor schedule fidelity or contact quality.",
                Channels = rows }));
    }

    sealed class SourceKeyGapRow
    {
        public string SourceBone, TargetBone;
        public bool SourceHasTrack;
        public int Channel, SourceDistinctTimes, DenseTimeGroups, ResultTimeGroups, DenseKnotSides, ResultKnotSides,
            DenseAbsentSourceTimeGroups, ResultAbsentSourceTimeGroups, DenseAbsentSourceKnotSides, ResultAbsentSourceKnotSides,
            DensePairedGroups, ResultPairedGroups, DenseEligibleWriterPairGroups, ResultEligibleWriterPairGroups;
    }
}
