using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Frozen independent byte/motion oracle, not a retarget-solver fixture:
    // seventeen retained scalar knots cost 212 bytes in I12 and 84 in I4.
    // The linear dyadic values and derivatives are exactly representable in
    // both formats. Source key markers alone therefore do not establish size.
    static void NativeRetargetPayloadChecks(MDL0Node model, string output)
    {
        string fingerprint = BrawlRetargeter.SkeletonFingerprint(model);
        var rig = BrawlRetargeter.Capture(model);
        var plan = new HumanoidRetargeter(rig.Rig, rig.Rig);
        string body = rig.Bones[rig.Rig.Roles["pelvis"]].Name;
        string control = rig.Bones[plan.TargetMotionControls.First()].Name;
        string stage = Path.Combine(output, "retarget-payload"); Directory.CreateDirectory(stage);
        var json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
        foreach (bool loop in new[] { false, true })
        using (var master = PayloadScheduleFixture(body, control, loop, false, false, out var ignored))
        using (var source = NativeAnimationOptimization.Copy(master))
        {
            string masterKey = NativeAnimationOptimization.ContentKey(master);
            string sourceKey = NativeAnimationOptimization.SourceContentKey(source);
            string baseline = Path.Combine(stage, "same-markers-" + loop + "-i12.chr0");
            SourceKeyArtifactExport(master, baseline);
            foreach (var mode in new[] { RetargetKeyframeMode.SourceBoneTimes, RetargetKeyframeMode.SourceChannelTimes })
            using (var result = new RetargetResult { Animation = NativeAnimationOptimization.Copy(master), Plan = plan })
            {
                RetargetKeyframes.Prepare(result, rig, rig, source, mode, CancellationToken.None, null);
                string path = Path.Combine(stage, "same-markers-" + loop + "-" + mode + ".chr0");
                SourceKeyArtifactExport(result.Animation, path);
                using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
                {
                    var entry = decoded?.FindChild(body, false) as CHR0EntryNode;
                    var storage = entry == null ? null : PreciseCHR0EntryNode.CaptureStorage(entry);
                    Check(entry != null && entry.KeyArrays[3].Entries.Select(k => k._index).SequenceEqual(Enumerable.Range(0, 17).Select(i => (float)i)),
                        mode + " / loop=" + loop + ": every donor body key time remains despite compact curve storage");
                    Check(storage != null && storage.Format(1) == AnimDataFormat.I4 && storage[3].ByteCount == 16 + 4 * 17 &&
                        8 + 12 * entry.KeyArrays[3].KeyCount == 212,
                        mode + ": exactly the same seventeen scalar knots use 84 actual I4 payload bytes instead of 212 I12 bytes");
                    SourceKeysSameTrack((CHR0EntryNode)master.FindChild(body, false), entry,
                        mode + ": compact serialization preserves every body value and tangent bit, with no key-count reduction");
                    SourceKeysSameTrack((CHR0EntryNode)master.FindChild(control, false), decoded.FindChild(control, false) as CHR0EntryNode,
                        mode + ": source-owned root motion retains every original ordered tuple");
                    Check(decoded.FrameCount == master.FrameCount && decoded.Loop == loop && decoded.Version == master.Version &&
                        decoded.OriginalPath == master.OriginalPath && new FileInfo(path).Length < new FileInfo(baseline).Length &&
                        new FileInfo(path).Length == result.KeyframeReport.ResultBytes && result.KeyframeReport.ResultBytes <= result.KeyframeReport.DenseBytes,
                        mode + ": complete independently reopened bytes shrink without changing native metadata or terminal timing");
                    double oracle = PayloadLineOracle(entry, 16);
                    var measured = SourceKeyQualityMeasure(model, master, decoded);
                    PayloadCheckStrictQuality(measured, mode + " / exact packing");
                    Check(oracle <= .000001 && result.KeyframeReport.ScalarCertified && result.KeyframeReport.QualityVerified,
                        mode + ": 4097 independent dyadic line samples agree within native floating roundoff, separate from coefficient identity and sampled hierarchy/skin evidence");
                    result.KeyframeReport.Validate();
                    File.WriteAllText(Path.Combine(stage, "same-markers-" + loop + "-" + mode + ".json"), json.Serialize(new
                    {
                        FrozenOracle = "RX(t)=t/8, dRX/dt=1/8; seventeen retained native knot times 0..16",
                        OriginalStreamBytes = 212, PackedStreamBytes = storage[3].ByteCount,
                        OriginalPhysicalBytes = new FileInfo(baseline).Length, PackedPhysicalBytes = new FileInfo(path).Length,
                        OracleError = oracle, WithheldNative = measured, Report = result.KeyframeReport
                    }));
                }
            }
            using (var result = new RetargetResult { Animation = NativeAnimationOptimization.Copy(master), Plan = plan })
            using (var cancel = new CancellationTokenSource())
            {
                bool stopped = false;
                try
                {
                    RetargetKeyframes.Prepare(result, rig, rig, source, RetargetKeyframeMode.SourceChannelTimes, cancel.Token,
                        (p, message) => { if (p >= 92) cancel.Cancel(); });
                }
                catch (OperationCanceledException) { stopped = true; }
                Check(stopped && result.KeyframeReport == null && result.DenseAnimation == null &&
                    NativeAnimationOptimization.ContentKey(result.Animation) == masterKey,
                    "Retarget payload cancellation publishes no partial storage result or changed finished master");
            }
            Check(NativeAnimationOptimization.ContentKey(master) == masterKey && NativeAnimationOptimization.SourceContentKey(source) == sourceKey,
                "Curve payload search leaves the finished target master and donor schedule unchanged");
        }
        PayloadGeneratedPairChecks(model, rig, plan, body, control, stage);
        PayloadGroupEligibilityChecks(stage);
        PayloadExactStorageCopyChecks(stage);
        model.ApplyCHR(null, 0);
        Check(BrawlRetargeter.SkeletonFingerprint(model) == fingerprint,
            "Retarget payload tests preserve authentic hierarchy, rest transforms and target rig identity");
    }

    static void PayloadGeneratedPairChecks(MDL0Node model, BrawlRig rig, HumanoidRetargeter plan,
        string body, string control, string stage)
    {
        string rigKey = NativeGeneratedPairRegistry.TryRigKey(model, CancellationToken.None);
        foreach (string authority in new[] { "known-generated", "unknown", "signed-zero" })
        using (var master = PayloadScheduleFixture(body, control, false, true, authority == "signed-zero", out var evidence))
        using (var source = PayloadScheduleFixture(body, control, false, false, false, out var ignored))
        using (var result = new RetargetResult
        {
            Animation = NativeAnimationOptimization.Copy(master), Plan = plan, GeneratedPairRigKey = rigKey,
            GeneratedPairEvidence = authority == "unknown" ? new NativeGeneratedPairEvidence[0] : evidence
        })
        {
            string key = NativeAnimationOptimization.ContentKey(master);
            RetargetKeyframes.Prepare(result, rig, rig, source, RetargetKeyframeMode.SourceChannelTimes, CancellationToken.None, null);
            string path = Path.Combine(stage, "paired-payload-" + authority + ".chr0"); SourceKeyArtifactExport(result.Animation, path);
            using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
            {
                var before = (CHR0EntryNode)master.FindChild(body, false);
                var after = (CHR0EntryNode)decoded.FindChild(body, false);
                var captured = PreciseCHR0EntryNode.CaptureStorage(after);
                var unreviewedChoices = NativeCompactAnimationStorage.CreateAlternatives(before, 1, false, false, new int[0],
                    result.KeyframeReport.LengthScale);
                var reviewedSites = authority == "unknown" ? new NativeGeneratedPairSite[0] : evidence.Where(e => e.Eligible && e.SameContext(before))
                    .Select(e => e.Site()).ToArray();
                var strictChoices = NativeCompactAnimationStorage.CreateAlternatives(before, 1, false, false, new int[0],
                    result.KeyframeReport.LengthScale, false, new int[0], CancellationToken.None, reviewedSites);
                var exactChoices = NativeCompactAnimationStorage.CreateAlternatives(before, 1, true, true, new int[0],
                    result.KeyframeReport.LengthScale);
                File.WriteAllText(Path.Combine(stage, "paired-payload-" + authority + ".json"),
                    new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Serialize(new
                    {
                        Authority = authority, Report = result.KeyframeReport,
                        DecodedRotationFormat = (captured?.Format(1) ?? AnimDataFormat.I12).ToString(),
                        StrictProposals = strictChoices.Select(p => new { Format = p.Format.ToString(), p.BeforeBytes, p.AfterBytes, p.ChangedCoefficients }).ToArray(),
                        UnreviewedProposals = unreviewedChoices.Select(p => new { Format = p.Format.ToString(), p.BeforeBytes, p.AfterBytes, p.ChangedCoefficients }).ToArray(),
                        ExactProposals = exactChoices.Select(p => new { Format = p.Format.ToString(), p.BeforeBytes, p.AfterBytes, p.ChangedCoefficients }).ToArray(),
                        Evidence = evidence.Select(e => new { e.Time, e.Eligible, MasterContextMatches = e.SameContext(before),
                            OrderedMasterSidesMatch = e.Matches(before.KeyArrays[e.Channel].Entries.First(k => k._index == e.Time),
                                before.KeyArrays[e.Channel].Entries.Last(k => k._index == e.Time)) }).ToArray(),
                        Before = before.KeyArrays[3].Entries.Select(k => new { Frame = k._index, Value = k._value, Tangent = k._tangent }).ToArray(),
                        After = after.KeyArrays[3].Entries.Select(k => new { Frame = k._index, Value = k._value, Tangent = k._tangent }).ToArray()
                    }));
                Check(after.KeyArrays[3].Entries.Select(k => k._index).SequenceEqual(before.KeyArrays[3].Entries.Select(k => k._index)),
                    authority + ": compact retarget storage retains the complete ordered native paired membership and times");
                if (authority == "known-generated")
                {
                    var storage = PreciseCHR0EntryNode.CaptureStorage(after);
                    Check(storage != null && storage[3] != null && storage[3].Format != AnimDataFormat.I12 &&
                        result.KeyframeReport.ResultBytes < result.KeyframeReport.DenseBytes,
                        "Known generated continuous sides admit smaller decoded payloads under the original strict target-motion budget");
                    Check(PayloadLineOracle(after, 16) <= .00001,
                        "Generated-side quantization retains the independently known near-linear motion at 4097 withheld dyadic samples");
                }
                else
                    SourceKeysSameTrack(before, after,
                        authority + ": unverified paired sides and signed-zero distinctions retain all original float bits");
                SourceKeysSameTrack((CHR0EntryNode)master.FindChild(control, false), (CHR0EntryNode)decoded.FindChild(control, false),
                    authority + ": body payload changes never approximate own-source root motion");
                PayloadCheckStrictQuality(SourceKeyQualityMeasure(model, master, decoded), authority + " / cumulative payload");
                Check(result.KeyframeReport.ResultBytes <= result.KeyframeReport.DenseBytes,
                    authority + ": failed or ineligible packed candidates cannot grow the verified reference file");
                result.KeyframeReport.Validate();
            }
            Check(NativeAnimationOptimization.ContentKey(master) == key,
                authority + ": pair authority never changes the immutable finished target baseline");
        }
    }

    static CHR0Node PayloadScheduleFixture(string body, string control, bool loop, bool pairs, bool signedZero,
        out NativeGeneratedPairEvidence[] evidence)
    {
        var clip = new CHR0Node { Name = "Independent payload oracle", FrameCount = loop ? 16 : 17, Loop = loop,
            Version = 5, OriginalPath = "procedural/payload-oracle" };
        var entry = clip.CreateEntry(body); entry.UseModelRotate = false; entry.Keyframes.PreserveInterpolation = true;
        var known = new List<NativeGeneratedPairEvidence>();
        for (int at = 0; at <= 16; at++)
        {
            bool paired = pairs && at > 0 && at < 16;
            var first = entry.Keyframes.SetFrameValue(3, at, at / 8f, true);
            first._tangent = paired ? .125f - 1f / 65536 : .125f;
            if (paired)
            {
                var second = entry.Keyframes.SetFrameValue(3, at, at / 8f, true);
                second._tangent = .125f + 1f / 65536;
                if (signedZero && at == 8) first._tangent = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
                known.Add(new NativeGeneratedPairEvidence(entry, 3, first, second));
            }
        }
        entry.Keyframes.SetFrameValue(4, 0, 0, true)._tangent = 0;
        entry.Keyframes.SetFrameValue(5, 0, 0, true)._tangent = 0;
        // Canonical native group slots are explicit; this does not rely on the
        // writer inventing absent Y/Z translation channels during serialization.
        var root = clip.CreateEntry(control); root.UseModelTranslate = false; root.Keyframes.PreserveInterpolation = true;
        for (int at = 0; at <= 16; at++) root.Keyframes.SetFrameValue(6, at, at / 8f, true)._tangent = .125f;
        root.Keyframes.SetFrameValue(7, 0, 0, true)._tangent = 0;
        root.Keyframes.SetFrameValue(8, 0, 0, true)._tangent = 0;
        PreciseCHR0EntryNode.Promote(clip); evidence = known.ToArray(); return clip;
    }

    static double PayloadLineOracle(CHR0EntryNode entry, int last)
    {
        double error = 0;
        for (int i = 0; i <= last * 256; i++)
        {
            float time = i / 256f;
            error = Math.Max(error, Math.Abs(entry.GetFrameValue(3, time) - time / 8d));
        }
        return error;
    }

    static void PayloadCheckStrictQuality(SourceKeyQualityMeasurements measured, string label)
    {
        double translation = Math.Min(.0002, measured.Length * .0001);
        Check(measured.Euler <= .01 && measured.Orientation <= .01 && measured.Scale <= .00001 &&
            measured.Translation <= translation && measured.EulerDerivative <= .05 && measured.ScaleDerivative <= .00001 &&
            measured.TranslationDerivative <= translation && measured.World <= measured.Length * .0001 && measured.Skin <= measured.Length * .0001,
            label + ": independent non-quarter native witnesses satisfy one combined local, derivative, hierarchy and all-skin budget");
    }

    static void PayloadGroupEligibilityChecks(string stage)
    {
        PayloadReviewedPairProposalChecks();
        foreach (float fractional in new[] { 8.5f, 8.01f })
        using (var clip = PayloadGroupFixture(17, new[] { 0f, 8f, 16f }, new[] { 0f, fractional, 16f }, .125f))
        {
            var entry = (CHR0EntryNode)clip.Children[0];
            string before = NativeAnimationOptimization.ContentKey(clip);
            var proposals = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, true, false, new int[0], 1);
            Check(proposals.All(p => p.Format != AnimDataFormat.I4),
                "A fractional Y axis vetoes I4 for the entire rotation group, even when X has integer donor markers");
            if (fractional == 8.5f)
            {
                Check(proposals.Length == 1 && proposals[0].Format == AnimDataFormat.I6 && proposals[0].Curves.Take(2).All(c => c.Format == AnimDataFormat.I6),
                    "A half-frame axis permits one legal shared I6 format; independent per-axis formats are never assumed");
                using (var independent = NativeAnimationOptimization.Copy(clip))
                {
                    NativeCompactAnimationStorage.ApplyGroup(independent, proposals[0]);
                    string path = Path.Combine(stage, "group-fractional-i6.chr0"); SourceKeyArtifactExport(independent, path);
                    using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
                        Check(NativeAnimationOptimization.ContentKey(decoded) == before,
                            "Shared-format fractional payloads independently reopen with exact native time/value/tangent tuples");
                }
                using (var stale = NativeAnimationOptimization.Copy(clip))
                {
                    ((CHR0EntryNode)stale.Children[0]).KeyArrays[3].Entries.First()._value += .25f;
                    string edit = NativeAnimationOptimization.ContentKey(stale); bool rejected = false;
                    try { NativeCompactAnimationStorage.ApplyGroup(stale, proposals[0]); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected && NativeAnimationOptimization.ContentKey(stale) == edit,
                        "Changed finished-master coefficients reject stale group plans without overwriting the intervening edit");
                }
            }
            else Check(proposals.Length == 0,
                "One off-1/32 axis vetoes the complete I4/I6 group rather than rounding native key times");
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel(); bool stopped = false;
                try { NativeCompactAnimationStorage.CreateAlternatives(entry, 1, true, false, new int[0], 1, cancel.Token); }
                catch (OperationCanceledException) { stopped = true; }
                Check(stopped && NativeAnimationOptimization.ContentKey(clip) == before,
                    "Cancelled group analysis writes no payload and preserves the source master");
            }
            Check(NativeAnimationOptimization.ContentKey(clip) == before,
                "Group-wide packing and rejected stale proposals leave the original snapshot unchanged");
        }
        foreach (float last in new[] { 255f, 256f, 1023f + 31f / 32, 1024f })
        using (var clip = PayloadGroupFixture((int)Math.Ceiling(last) + 1, new[] { 0f, 8f, last }, null, .125f))
        {
            var proposals = NativeCompactAnimationStorage.CreateAlternatives((CHR0EntryNode)clip.Children[0], 1, true, false, new int[0], 1);
            Check(proposals.Any(p => p.Format == AnimDataFormat.I4) == (last <= 255) &&
                proposals.Any(p => p.Format == AnimDataFormat.I6) == (last <= 1023f + 31f / 32),
                "Native packed index boundaries preserve exact frame " + last + " or reject its complete group without clamping");
        }
        using (var clip = PayloadGroupFixture(17, new[] { 0f, 8f, 16f }, null, 128))
            Check(NativeCompactAnimationStorage.CreateAlternatives((CHR0EntryNode)clip.Children[0], 1, false, false, new int[0], 1).Length == 0,
                "Native signed tangent overflow keeps precise I12 instead of pretending smaller packed coefficients are legal");
    }

    static CHR0Node PayloadGroupFixture(int count, float[] x, float[] y, float tangent)
    {
        var clip = new CHR0Node { Name = "Independent group eligibility", FrameCount = count, Version = 5 };
        var entry = clip.CreateEntry("structural-fixture-joint"); entry.UseModelRotate = false; entry.Keyframes.PreserveInterpolation = true;
        for (int i = 0; i < x.Length; i++) entry.Keyframes.SetFrameValue(3, x[i], i / 8f, true)._tangent = tangent;
        if (y == null) entry.Keyframes.SetFrameValue(4, 0, 0, true)._tangent = 0;
        else for (int i = 0; i < y.Length; i++) entry.Keyframes.SetFrameValue(4, y[i], i / 4f, true)._tangent = tangent;
        entry.Keyframes.SetFrameValue(5, 0, 0, true)._tangent = 0;
        PreciseCHR0EntryNode.Promote(clip); return clip;
    }

    static void PayloadReviewedPairProposalChecks()
    {
        using (var master = PayloadScheduleFixture("reviewed-body", "exact-motion", false, true, false, out var evidence))
        {
            var entry = (CHR0EntryNode)master.Children[0];
            var sites = evidence.Select(e => e.Site()).ToArray();
            string content = NativeAnimationOptimization.SourceContentKey(master), storage = NativeAnimationOptimization.StorageKey(master);
            var unreviewed = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], 1);
            var reviewed = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], 1,
                false, new int[0], CancellationToken.None, sites);
            Check(unreviewed.Length == 0 && reviewed.Length == 2 && reviewed.All(p => !p.IsExact &&
                p.Curves[0].Times.SequenceEqual(entry.KeyArrays[3].Entries.Select(k => k._index)) &&
                p.Curves[0].Tangents.Zip(entry.KeyArrays[3].Entries, (value, key) => Math.Abs((double)value - key._tangent) <= 1d / 65536).All(x => x)),
                "Reviewed generated C0 pairs offer strict-budget I4/I6 proposals without changing membership, zero policy or motion limits");
            foreach (string failure in new[] { "missing-site", "duplicate-claim", "wrong-bone", "wrong-channel", "wrong-time" })
            {
                var claims = sites.Select(s => new NativeGeneratedPairSite { Bone = s.Bone, Channel = s.Channel, Frame = s.Frame }).ToList();
                int index = claims.FindIndex(s => s.Frame == 8);
                if (failure == "missing-site") claims.RemoveAt(index);
                if (failure == "duplicate-claim") claims.Add(new NativeGeneratedPairSite { Bone = claims[index].Bone, Channel = claims[index].Channel, Frame = claims[index].Frame });
                if (failure == "wrong-bone") claims[index].Bone = "unreviewed-other-body";
                if (failure == "wrong-channel") claims[index].Channel = 4;
                if (failure == "wrong-time") claims[index].Frame = 8.5f;
                Check(NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], 1,
                    false, new int[0], CancellationToken.None, claims.ToArray()).Length == 0,
                    "Reviewed pair proposal / " + failure + ": an unqualified site keeps both original sides exact");
            }
            foreach (string protection in new[] { "exact-policy", "exact-group", "mandatory-key", "fit-join" })
                Check(NativeCompactAnimationStorage.CreateAlternatives(entry, 1, protection == "exact-policy", protection == "exact-group",
                    protection == "mandatory-key" ? new[] { 8 } : new int[0], 1, false,
                    protection == "fit-join" ? new[] { 8 } : new int[0], CancellationToken.None, sites).Length == 0,
                    "Reviewed pair proposal / " + protection + ": writer evidence cannot override exact coefficient protections");
            foreach (string failure in new[] { "signed-zero", "discontinuity" })
            using (var changed = NativeAnimationOptimization.CopySource(master))
            {
                var track = (CHR0EntryNode)changed.Children[0];
                var sides = track.KeyArrays[3].Entries.Where(k => k._index == 8).ToArray();
                if (failure == "signed-zero") sides[0]._tangent = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
                else sides[1]._value += .001f;
                Check(NativeCompactAnimationStorage.CreateAlternatives(track, 1, false, false, new int[0], 1,
                    false, new int[0], CancellationToken.None, sites).Length == 0,
                    "Reviewed pair proposal / " + failure + ": a site cannot authorize signed-zero loss or a native jump");
            }
            Check(NativeAnimationOptimization.SourceContentKey(master) == content && NativeAnimationOptimization.StorageKey(master) == storage,
                "Reviewed-pair proposal generation preserves the full-quality native source and storage plans");
        }
    }

    static void PayloadExactStorageCopyChecks(string stage)
    {
        // Nonzero scaling metadata is intentionally source-snapshot-only. This
        // verifies storage-copy preservation without broadening the target
        // optimizer's separate transform-semantics qualification.
        foreach (int version in new[] { 4, 5 }) foreach (int rule in new[] { 0, 2 })
        {
            string label = "v" + version + "-scaling" + rule;
            string original = Path.Combine(stage, "exact-storage-original-" + label + ".chr0");
            using (var fixture = PayloadScheduleFixture("body-fixture", "motion-fixture", false, true, false, out var evidence))
            {
                fixture.Version = version;
                if (version == 5) fixture.UserEntries.Add(new UserDataClass { Name = "Exact storage marker",
                    DataType = UserValueType.Int, Entries = new[] { "17", "23" } });
                var entry = (CHR0EntryNode)fixture.Children[0];
                entry.UseModelScale = true; entry.UseModelTranslate = true;
                entry.ScaleCompensateApply = true; entry.ScaleCompensateParent = true; entry.ClassicScaleOff = true;
                fixture.ExportChecked(original);
            }
            byte[] bytes = File.ReadAllBytes(original); int offset = version == 4 ? 0x24 : 0x28;
            bytes[offset] = bytes[offset + 1] = bytes[offset + 2] = 0; bytes[offset + 3] = (byte)rule;
            File.WriteAllBytes(original, bytes); string originalHash = FighterProject.HashFile(original);
            using (var source = NodeFactory.FromFile(null, original) as CHR0Node)
            {
                var live = (CHR0EntryNode)source.Children[0];
                live.Keyframes.PreserveInterpolation = false; live.SignalPropertyChange();
                string content = NativeAnimationOptimization.SourceContentKey(source), storage = NativeAnimationOptimization.StorageKey(source);
                bool dirty = source.IsDirty, branch = source.IsBranch;
                string beforePath = Path.Combine(stage, "exact-storage-before-" + label + ".chr0");
                SourceKeyArtifactExport(source, beforePath);
                using (var prepared = NativeAnimationOptimization.PrepareExactStorageCopy(source, stage, CancellationToken.None))
                {
                    Check(prepared.Candidate != null && !ReferenceEquals(prepared.Candidate, source) &&
                        NativeAnimationOptimization.SourceContentKey(prepared.Candidate) == content && prepared.Groups > 0 &&
                        prepared.AfterBytes < prepared.BeforeBytes && prepared.BeforeBytes == new FileInfo(beforePath).Length,
                        "Exact storage copy / " + label + ": an independent smaller copy preserves complete source metadata and coefficients");
                    string afterPath = Path.Combine(stage, "exact-storage-after-" + label + ".chr0");
                    SourceKeyArtifactExport(prepared.Candidate, afterPath);
                    using (var decoded = NodeFactory.FromFile(null, afterPath) as CHR0Node)
                    {
                        Check(decoded != null && NativeAnimationOptimization.SourceContentKey(decoded) == content &&
                            MetadataRead32(File.ReadAllBytes(afterPath), offset) == rule &&
                            new FileInfo(afterPath).Length == prepared.AfterBytes && prepared.AfterBytes <= prepared.BeforeBytes,
                            "Exact storage copy / " + label + ": actual exported bytes independently reopen all metadata, paired sides, flags and scaling rules");
                        SourceKeysSameTrack(live, (CHR0EntryNode)decoded.Children[0],
                            "Exact storage copy / " + label + ": both authored incoming/outgoing tangent sides remain bit-exact");
                        var decodedFlags = (CHR0EntryNode)decoded.Children[0];
                        Check(decodedFlags.UseModelScale && decodedFlags.UseModelTranslate && decodedFlags.ScaleCompensateApply &&
                            decodedFlags.ScaleCompensateParent && decodedFlags.ClassicScaleOff,
                            "Exact storage copy / " + label + ": model-transform and scale-compensation flags survive the real writer");
                        using (var repeated = NativeAnimationOptimization.PrepareExactStorageCopy(decoded, stage, CancellationToken.None))
                        {
                            Check(NativeAnimationOptimization.SourceContentKey(repeated.Candidate) == content &&
                                repeated.AfterBytes <= repeated.BeforeBytes && repeated.BeforeBytes == prepared.AfterBytes,
                                "Exact storage copy / " + label + ": repeated preparation cannot grow or cumulatively quantize a compact copy");
                            string resaved = Path.Combine(stage, "exact-storage-resaved-" + label + ".chr0");
                            SourceKeyArtifactExport(repeated.Candidate, resaved);
                            Check(File.ReadAllBytes(resaved).SequenceEqual(File.ReadAllBytes(afterPath)),
                                "Exact storage copy / " + label + ": an unchanged compact candidate has deterministic repeated native bytes");
                        }
                    }
                }
                using (var cancel = new CancellationTokenSource())
                {
                    cancel.Cancel(); bool stopped = false;
                    try { using (var ignored = NativeAnimationOptimization.PrepareExactStorageCopy(source, stage, cancel.Token)) { } }
                    catch (OperationCanceledException) { stopped = true; }
                    Check(stopped, "Exact storage copy / " + label + ": cancelled preparation returns no candidate");
                }
                Check(NativeAnimationOptimization.SourceContentKey(source) == content && NativeAnimationOptimization.StorageKey(source) == storage &&
                    source.IsDirty == dirty && source.IsBranch == branch && !live.Keyframes.PreserveInterpolation && FighterProject.HashFile(original) == originalHash,
                    "Exact storage copy / " + label + ": success, repeated saves and cancellation preserve the live source, dirty/editor state and original file bytes");
            }
        }
    }
}
