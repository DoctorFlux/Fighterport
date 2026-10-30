using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal static partial class Program
{
    sealed class FloatDenseCoreReply { public FloatDenseCoreProof Certificate { get; set; } }
    sealed class FloatDenseCoreProof
    {
        public bool Accepted { get; set; } public bool WorkLimitReached { get; set; }
        public long Work { get; set; } public string Reason { get; set; }
        public string FailedBone { get; set; } public int? FailedChannel { get; set; }
        public double[] ProbeFrames { get; set; }
    }

    // Execute only on the private desktop. The irregular float samples
    // distinguish physical L4 storage
    // from quantizers, while an actual Core certificate and native cumulative
    // gate remain mandatory. Raw-file decoding is corroborated separately by
    // tools/chr0_forensics.py; no external editor/game claim is made here.
    static async Task NativeDenseFloatChecks(MDL0Node model, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("L4 export-copy checks require the private desktop.");
        await NativeExportSearchOrderChecks();
        string stage = Path.Combine(output, "l4-export-copy"); Directory.CreateDirectory(stage);
        var json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024, RecursionLimit = 200 };
        var rig = BrawlRetargeter.Capture(model); var policy = new HumanoidRetargeter(rig.Rig, rig.Rig);
        var body = rig.Bones[rig.Rig.Roles["pelvis"]]; var root = rig.Bones[policy.TargetMotionControls.First()];
        string nativeModel = Path.Combine(stage, "independent-target.mdl0"); model.ExportChecked(nativeModel);
        string fingerprint = BrawlRetargeter.SkeletonFingerprint(model), geometry = DirtyModelMemory(model);
        float negativeZero = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
        var samples = new[] { negativeZero, .25f, 720f, -1f };
        var frozen = IndexedCHR0CurveStorage.CreateFloatDense(samples, 3); samples[1] = 123;
        var values = frozen.EncodedValues; values[2] = 123;
        Check(frozen.Format == AnimDataFormat.L4 && frozen.ByteCount == 16 && frozen.NativeSampleCount == 3 && frozen.EncodedSampleCount == 4 &&
            ReplayFloatBits(frozen.EncodedValues[0]) == int.MinValue && frozen.EncodedValues[1] == .25f && frozen.EncodedValues[2] == 720 && frozen.EncodedValues[3] == -1,
            "L4 factory freezes all physical samples, winding, signed zero and terminal data without caller aliases");
        foreach (var invalid in new[] { new[] { 0f }, new[] { 0f, float.NaN }, new[] { 0f, float.PositiveInfinity } })
        {
            bool rejected = false; try { IndexedCHR0CurveStorage.CreateFloatDense(invalid, 1); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "L4 factory rejects wrong sample counts and nonfinite input before storage creation");
        }
        try
        {
            foreach (int version in new[] { 4, 5 })
            using (var master = FloatDenseExportFixture(body, root, version))
            {
                string content = NativeAnimationOptimization.SourceContentKey(master), storage = NativeAnimationOptimization.StorageKey(master);
                var entry = (CHR0EntryNode)master.FindChild(body.Name, false);
                var sites = FloatDensePairSites(master);
                var limits = new NativeExportOptimizationSettings { Policy = NativeExportOptimizationPolicy.ErrorBounded,
                    RotationDegrees = 1e-8, RotationDerivative = 1e-8 };
                NativeCompactStorageGroup[] Propose(bool exact, NativeGeneratedPairSite[] evidence, NativeDenseProofBudget proof = null)
                    => NativeCompactAnimationStorage.CreateFloatDenseAlternatives(entry, entry, exact, new[] { 0, 16 }, 2000000,
                        proof ?? new NativeDenseProofBudget(), limits.RotationDegrees, limits.RotationDerivative, evidence);
                var proposal = Propose(false, sites).Single();
                var expandedSites = sites.Concat(Enumerable.Range(0, 64).Select(i => new NativeGeneratedPairSite {
                    Bone = "unrelated evidence " + i, Channel = 3, Frame = .5f })).ToArray();
                var indexedEvidence = NativeDensePairEvidenceIndex.Create(expandedSites, new NativeDenseProofBudget(), CancellationToken.None);
                var indexedProposal = NativeCompactAnimationStorage.CreateFloatDenseAlternatives(entry, entry, false, new[] { 0, 16 }, 2000000,
                    new NativeDenseProofBudget(), limits.RotationDegrees, limits.RotationDerivative, indexedEvidence.ForBone(entry.Name)).Single();
                var fullProposal = Propose(false, expandedSites).Single();
                Check(indexedProposal.Curves[0].ContentKey == fullProposal.Curves[0].ContentKey,
                    "Per-bone pair indexing yields exactly the same L4 payload permission as the complete permutable evidence list");
                var duplicateSites = expandedSites.Concat(sites.Where(site => site.Bone == entry.Name && site.Channel == 3).Take(1)).Reverse().ToArray();
                var conflictingIndex = NativeDensePairEvidenceIndex.Create(duplicateSites, new NativeDenseProofBudget(), CancellationToken.None);
                Check(Propose(false, duplicateSites).Length == 0 && NativeCompactAnimationStorage.CreateFloatDenseAlternatives(entry, entry, false,
                    new[] { 0, 16 }, 2000000, new NativeDenseProofBudget(), limits.RotationDegrees, limits.RotationDerivative,
                    conflictingIndex.ForBone(entry.Name)).Length == 0,
                    "Indexed duplicate/conflicting claims remain rejected exactly like the full evidence, independently of storage order");
                FloatDenseCompanionAndFormatChecks(master, body.Name, root.Name, proposal, sites, version, stage);
                Check(proposal.Format == AnimDataFormat.L4 && proposal.AfterBytes == 72 && proposal.BeforeBytes > 72 &&
                    proposal.Curves[0].NativeSampleCount == 17 && proposal.Curves[0].EncodedSampleCount == 18 &&
                    ReplayFloatBits(proposal.Curves[0].EncodedValues[16]) == ReplayFloatBits(proposal.Curves[0].EncodedValues[17]),
                    "L4 v" + version + " freezes 17 native points, 18 physical floats and the exact held terminal at a 72-byte payload cost");
                Check(Propose(true, sites).Length == 0 && Propose(false, new NativeGeneratedPairSite[0]).Length == 0 &&
                    Propose(false, sites.Concat(sites).ToArray()).Length == 0,
                    "L4 exact roots/protections, unknown paired sides and conflicting generated claims remain ineligible");
                var exhausted = new NativeDenseProofBudget(amount => false);
                Check(Propose(false, sites, exhausted).Length == 0 && exhausted.Exhausted,
                    "L4 shared proof exhaustion retains the existing representation without an uncertified candidate");
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel(); bool stopped = false;
                    try { NativeCompactAnimationStorage.CreateFloatDenseAlternatives(entry, entry, false, new int[0], 2000000,
                        new NativeDenseProofBudget(), .1, .1, sites, cancelled.Token); } catch (OperationCanceledException) { stopped = true; }
                    Check(stopped, "L4 cancellation occurs before proposal publication");
                }
                using (var candidate = NativeAnimationOptimization.CopySource(master))
                {
                    NativeCompactAnimationStorage.ApplyGroup(candidate, proposal);
                    string file = Path.Combine(stage, "v" + version + "-literal-l4.chr0"); candidate.ExportChecked(file);
                    using (var fresh = (CHR0Node)NodeFactory.FromFile(null, file))
                    {
                        var stored = PreciseCHR0EntryNode.CaptureStorage((CHR0EntryNode)fresh.FindChild(body.Name, false));
                        Check(stored.Format(1) == AnimDataFormat.L4 && stored[3].ContentKey == proposal.Curves[0].ContentKey &&
                            NativeAnimationOptimization.SourceContentKey(fresh) == content,
                            "L4 fresh native decode retains complete physical payload and every same-master coefficient/flag/metadata field");
                        FloatDenseHeldTerminalProjectionChecks(master, fresh, body.Name, version);
                        var adopted = (CHR0EntryNode)fresh.FindChild(body.Name, false);
                        Check(NativeCompactAnimationStorage.CreateFloatDenseAlternatives(adopted, adopted, false, new int[0], 2000000,
                            new NativeDenseProofBudget(), .1, .1, sites).Length == 0,
                            "An existing imported L4 payload remains retention-only instead of becoming a cumulative resampling baseline");
                        SourceKeysSameTrack((CHR0EntryNode)master.FindChild(root.Name, false), (CHR0EntryNode)fresh.FindChild(root.Name, false),
                            "L4 body export retains every source-owned root time/value/tangent tuple");
                    }
                    ((CHR0EntryNode)candidate.FindChild(body.Name, false)).KeyArrays[3].Entries.First()._value += .1f;
                    bool stale = false; try { NativeCompactAnimationStorage.ApplyGroup(candidate, proposal); } catch (InvalidOperationException) { stale = true; }
                    Check(stale, "L4 stale source-group identity is rejected before applying its immutable plan");
                }
                using (var looping = NativeAnimationOptimization.CopySource(master))
                {
                    looping.Loop = true; var animated = (CHR0EntryNode)looping.FindChild(body.Name, false);
                    Check(NativeCompactAnimationStorage.CreateFloatDenseAlternatives(animated, animated, false, new int[0], 2000000,
                        new NativeDenseProofBudget(), .1, .1, sites).Length == 0,
                        "New L4 approximation leaves unqualified looping/wrap semantics unchanged");
                }
                using (var single = NativeAnimationOptimization.CopySource(master))
                {
                    single.FrameCount = 1; var animated = (CHR0EntryNode)single.FindChild(body.Name, false);
                    Check(NativeCompactAnimationStorage.CreateFloatDenseAlternatives(animated, animated, false, new int[0], 2000000,
                        new NativeDenseProofBudget(), .1, .1, sites).Length == 0,
                        "One-frame native clips are preserved rather than newly sampled by the L4 export search");
                }
                using (var signed = NativeAnimationOptimization.CopySource(master))
                {
                    var animated = (CHR0EntryNode)signed.FindChild(body.Name, false); animated.KeyArrays[3].Entries.First()._tangent = negativeZero;
                    Check(NativeCompactAnimationStorage.CreateFloatDenseAlternatives(animated, animated, false, new int[0], 2000000,
                        new NativeDenseProofBudget(), .1, .1, sites).Length == 0,
                        "Authored signed-zero distinctions remain in precise native storage");
                }
                using (var client = new AnimationControlClient())
                {
                    long work = 0; int sequence = 0;
                    var denseBudget = new NativeDenseProofBudget(amount => { if (amount < 0 || amount > 2000000 - work) return false; work += amount; return true; });
                    async Task<NativeCompactCurveCertificate> Certify(CHR0Node candidate, CancellationToken token)
                    {
                        token.ThrowIfCancellationRequested();
                        if (work >= 2000000) return new NativeCompactCurveCertificate { BudgetExhausted = true, Report = "Shared test proof budget exhausted." };
                        string input = Path.Combine(stage, "v" + version + "-core-" + sequence + "-input.json");
                        string result = Path.Combine(stage, "v" + version + "-core-" + sequence++ + "-output.json");
                        File.WriteAllText(input, json.Serialize(new { master.Name, SampleCount = master.FrameCount, master.Loop,
                            HighQuality = true, SmallFile = true, ExplicitBounds = limits, LengthScale = NativeAnimationOptimization.GetBindBoundsDiagonal(model),
                            Tracks = RefitNativeWire(master), CandidateTracks = RefitNativeWire(candidate), GeneratedPairSites = sites,
                            ExactBones = new[] { root.Name }, ProtectedBones = new[] { root.Name }, MandatoryFrames = new[] { 0, 16 },
                            ProtectedRanges = new object[0], ProtectionKnown = true, MaximumWork = 2000000 - work, MaximumCandidateSpans = 100000 }));
                        var reply = await client.Send(new { Operation = "animation-optimize-prepare", Model = nativeModel,
                            Optimization = new { InputPath = input, OutputPath = result } });
                        var proof = json.Deserialize<FloatDenseCoreReply>(File.ReadAllText(result))?.Certificate;
                        if (reply.OptimizationPath != result || proof == null || proof.Work < 0 || proof.Work > 2000000 - work)
                            throw new InvalidDataException("Invalid actual Core certificate/budget receipt.");
                        work += proof.Work;
                        return new NativeCompactCurveCertificate { Accepted = proof.Accepted, BudgetExhausted = proof.WorkLimitReached,
                            Report = proof.Reason, ProbeFrames = proof.ProbeFrames ?? new double[0], FailedBone = proof.FailedBone, FailedChannel = proof.FailedChannel };
                    }
                    NativeAnimationOptimizationOptions Options() => new NativeAnimationOptimizationOptions { ExactOnly = false, AllowGeneratedPairReduction = true,
                        ExportSettings = limits.Clone(), LengthScale = NativeAnimationOptimization.GetBindBoundsDiagonal(model),
                        TargetBones = rig.Bones.Select(b => b.Name).ToArray(), ExactBones = new[] { root.Name }, ProtectedBones = new[] { root.Name },
                        MandatoryFrames = new[] { 0, 16 }, GeneratedPairSites = sites, Budget = new NativeAnimationOptimizationBudget() };
                    var options = Options();
                    var sharedAttempts = new NativeCompactAttemptBudget();
                    using (var precise = NativeAnimationOptimization.Prepare(model, master, master.Children.OfType<CHR0EntryNode>().Select(OptimizationTrack).ToArray(), options, stage))
                    using (var packed = await NativeAnimationOptimization.PrepareCompact(model, master, master, precise, options, stage, Certify, denseBudget, CancellationToken.None, sharedAttempts))
                    {
                        File.WriteAllText(Path.Combine(stage, "v" + version + "-compact-before-size-assert.json"), json.Serialize(new
                        {
                            ReferenceBytes = precise.AfterBytes, PackedBytes = packed.AfterBytes,
                            ReferenceBlockBytes = precise.CandidateResourceBytes, PackedBlockBytes = packed.CandidateResourceBytes,
                            ReferenceContent = precise.ContentKey, PackedContent = packed.ContentKey,
                            ReferenceStorage = precise.StorageKey, PackedStorage = packed.StorageKey,
                            packed.HasChanges, packed.UsedApproximateStorage, packed.Report, Options = options,
                            SharedScalarWork = work, DenseProofWork = denseBudget.Work, DenseProofExhausted = denseBudget.Exhausted,
                            Groups = packed.Candidate.Children.OfType<CHR0EntryNode>().Select(node => new {
                                node.Name, Storage = PreciseCHR0EntryNode.CaptureStorage(node)?.ContentKey,
                                RotationFormat = PreciseCHR0EntryNode.CaptureStorage(node)?.Format(1).ToString(),
                                Keys = node.KeyArrays.Select(array => array.KeyCount).ToArray() }).ToArray()
                        }));
                        Check(packed.AfterBytes < precise.AfterBytes && packed.CandidateResourceBytes <= precise.CandidateResourceBytes &&
                            packed.Candidate.Children.OfType<CHR0EntryNode>().Any(e => PreciseCHR0EntryNode.CaptureStorage(e)?.Format(1) == AnimDataFormat.L4),
                            "L4 export-copy search retains a smaller whole native file after real Core, fresh decode and cumulative all-skin verification");
                        Check(sharedAttempts.Used > 0 && sharedAttempts.Used <= 24 && packed.Report.Contains("Early complete L4 prefix") && packed.Report.Contains("L4 phase ledger"),
                            "Actual bounded native search attempts L4 early, records eligibility/work and consumes the unchanged shared complete-attempt account");
                        var measured = SourceKeyQualityMeasure(model, master, packed.Candidate);
                        Check(measured.Euler <= limits.RotationDegrees && measured.Orientation <= limits.RotationDegrees &&
                            measured.Translation <= limits.Translation && measured.Scale <= limits.Scale &&
                            measured.World <= limits.GeometryRelative * options.LengthScale && measured.Skin <= limits.GeometryRelative * options.LengthScale,
                            "L4 withheld native witnesses independently compare the same finished master and every original skin influence");
                        string first = Path.Combine(stage, "v" + version + "-verified.chr0"); packed.Candidate.ExportChecked(first);
                        using (var decoded = (CHR0Node)NodeFactory.FromFile(null, first))
                        {
                            PreciseCHR0EntryNode.Promote(decoded);
                            string second = Path.Combine(stage, "v" + version + "-resaved.chr0"); decoded.ExportChecked(second);
                            using (var reopened = (CHR0Node)NodeFactory.FromFile(null, second))
                                Check(NativeAnimationOptimization.SourceContentKey(reopened) == NativeAnimationOptimization.SourceContentKey(packed.Candidate) &&
                                    NativeAnimationOptimization.StorageKey(reopened) == NativeAnimationOptimization.StorageKey(packed.Candidate),
                                    "L4 export/reopen/save/reopen preserves the full decoded master and terminal storage plan");
                        }
                        File.WriteAllText(Path.Combine(stage, "v" + version + "-size-quality.json"), json.Serialize(new {
                            ReferenceBytes = precise.AfterBytes, ExportBytes = packed.AfterBytes, packed.BeforeKeys, packed.AfterKeys,
                            packed.MaximumEulerError, packed.MaximumOrientationError, packed.MaximumSkinError, packed.MaximumWorldBoneError,
                            Withheld = measured, SharedScalarWork = work, packed.Report,
                            Scope = "Procedural real-Core/native evidence only. Independent physical parser and actual games/editors remain separate." }));
                    }
                    // Deliberately deny the smaller L4 candidate. The same
                    // pipeline must return its previously verified incumbent.
                    var rejectOptions = Options();
                    using (var precise = NativeAnimationOptimization.Prepare(model, master, master.Children.OfType<CHR0EntryNode>().Select(OptimizationTrack).ToArray(), rejectOptions, stage))
                    using (var fallback = await NativeAnimationOptimization.PrepareCompact(model, master, master, precise, rejectOptions, stage,
                        (candidate, token) => Task.FromResult(new NativeCompactCurveCertificate {
                            Accepted = NativeAnimationOptimization.SourceContentKey(candidate) == content &&
                                !candidate.Children.OfType<CHR0EntryNode>().Any(e => PreciseCHR0EntryNode.CaptureStorage(e)?.Format(1) == AnimDataFormat.L4),
                            Report = "Independent deliberately failing candidate-quality oracle; retain the qualified prior representation." }), new NativeDenseProofBudget()))
                        Check(fallback.AfterBytes <= precise.AfterBytes && NativeAnimationOptimization.SourceContentKey(fallback.Candidate) == content &&
                            !fallback.Candidate.Children.OfType<CHR0EntryNode>().Any(e => PreciseCHR0EntryNode.CaptureStorage(e)?.Format(1) == AnimDataFormat.L4),
                            "Failed L4 candidate quality retains a fully verified no-larger incumbent without touching the master");
                    var legacyOptions = Options(); legacyOptions.ExportSettings = null; legacyOptions.ExactOnly = true;
                    using (var precise = NativeAnimationOptimization.Prepare(model, master, master.Children.OfType<CHR0EntryNode>().Select(OptimizationTrack).ToArray(), legacyOptions, stage))
                    using (var legacy = await NativeAnimationOptimization.PrepareCompact(model, master, master, precise, legacyOptions, stage, Certify, new NativeDenseProofBudget()))
                        Check(NativeAnimationOptimization.SourceContentKey(legacy.Candidate) == content &&
                            !legacy.Report.Contains("Early complete L4 prefix") && !legacy.Candidate.Children.OfType<CHR0EntryNode>().Any(node =>
                                PreciseCHR0EntryNode.CaptureStorage(node)?.Format(1) == AnimDataFormat.L4),
                            "Null export settings and Exact authoring paths retain native indexed behavior without the bounded-export scheduler or L4 permission");
                }
                Check(NativeAnimationOptimization.SourceContentKey(master) == content && NativeAnimationOptimization.StorageKey(master) == storage,
                    "L4 factory/search/failure preserve the exact full-quality master and its original storage policy");
            }
            FloatDenseEndpointAndFractionalChecks(stage);
        }
        finally { model.ApplyCHR(null, 0); }
        Check(BrawlRetargeter.SkeletonFingerprint(model) == fingerprint && DirtyModelMemory(model) == geometry,
            "L4 tests preserve authentic native hierarchy, binds and every original skin coefficient");
    }

    static CHR0Node FloatDenseExportFixture(MDL0BoneNode body, MDL0BoneNode root, int version)
    {
        var clip = new CHR0Node { Name = "Independent L4 export-copy fixture", Version = version, FrameCount = 17, Loop = false, OriginalPath = "procedural/l4-export" };
        foreach (var bone in new[] { body, root })
        {
            var entry = clip.CreateEntry(bone.Name); entry.Keyframes.PreserveInterpolation = true;
            entry.ScaleCompensateApply = bone.SegScaleCompApply; entry.ScaleCompensateParent = bone.SegScaleCompParent; entry.ClassicScaleOff = !bone.ClassicScale;
            for (int channel = 0; channel < 9; channel++) entry.Keyframes.SetFrameValue(channel, 0, bone.BindState[channel], true)._tangent = 0;
            if (ReferenceEquals(bone, root))
            {
                foreach (int channel in new[] { 6, 7, 8 }) entry.KeyArrays[channel].Entries.First()._value = 0;
                for (int frame = 0; frame < 17; frame++) entry.Keyframes.SetFrameValue(6, frame, frame / 8f, true)._tangent = .125f;
            }
            else
            {
                float[] offsets = { 0, .001234567f, .003456789f, .002345678f, .005678912f, .004567891f,
                    .006789123f, .008912345f, .007891234f, .009123456f, .011234567f, .010345678f,
                    .012456789f, .014567891f, .013678912f, .015789123f, .016891234f };
                entry.Keyframes._keyArrays[3] = DenseCHR0Projection.Create(offsets.Select(value => bone.BindState[3] + value).ToArray(), 17);
            }
        }
        PreciseCHR0EntryNode.Promote(clip); return clip;
    }
    static NativeGeneratedPairSite[] FloatDensePairSites(CHR0Node clip) => clip.Children.OfType<CHR0EntryNode>()
        .SelectMany(entry => Enumerable.Range(0, 9).SelectMany(channel => entry.KeyArrays[channel].Entries.GroupBy(key => key._index)
            .Where(group => group.Count() == 2).Select(group => new NativeGeneratedPairSite { Bone = entry.Name, Channel = channel, Frame = group.Key }))).ToArray();

    static void FloatDenseCompanionAndFormatChecks(CHR0Node master, string body, string root,
        NativeCompactStorageGroup proposal, NativeGeneratedPairSite[] sites, int version, string stage)
    {
        float negativeZero = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
        foreach (string variant in new[] { "timed-at-three", "nonzero-tangent", "negative-zero-time", "negative-zero-tangent" })
        using (var bad = NativeAnimationOptimization.CopySource(master))
        {
            var entry = (CHR0EntryNode)bad.FindChild(body, false); var companion = entry.KeyArrays[4].Entries.Single();
            if (variant == "timed-at-three") companion._index = 3;
            if (variant == "nonzero-tangent") companion._tangent = .125f;
            if (variant == "negative-zero-time") companion._index = negativeZero;
            if (variant == "negative-zero-tangent") companion._tangent = negativeZero;
            Check(NativeCompactAnimationStorage.CreateFloatDenseAlternatives(entry, entry, false, new[] { 0, 16 }, 2000000,
                new NativeDenseProofBudget(), .1, .1, sites).Length == 0,
                "L4 v" + version + ": dense RX plus noncanonical fixed RY cannot claim a whole-group L4 plan / " + variant);
            // Manufacturing the old invalid claim must fail too, even when
            // numerical RX samples happen to match the original master.
            var channels = new IndexedCHR0CurveStorage[9]; channels[3] = proposal.Curves[0];
            PreciseCHR0EntryNode.RestoreStorage(entry, new IndexedCHR0EntryStorage(channels));
            string content = NativeAnimationOptimization.SourceContentKey(bad); bool rejected = false;
            try { NativeAnimationOptimization.CaptureFloatDenseExportPlans(bad, master); }
            catch (InvalidDataException error) { rejected = Equals(error.Data["OptimizationQuality"], true); }
            Check(rejected && NativeAnimationOptimization.SourceContentKey(bad) == content,
                "L4 v" + version + ": format-claim capture rejects a stale whole-R plan without mutating the candidate / " + variant);
        }
        using (var candidate = NativeAnimationOptimization.CopySource(master))
        {
            NativeCompactAnimationStorage.ApplyGroup(candidate, proposal);
            var required = NativeAnimationOptimization.CaptureFloatDenseExportPlans(candidate, master);
            string l4 = Path.Combine(stage, "v" + version + "-format-claim-l4.chr0"); candidate.ExportChecked(l4);
            using (var fresh = (CHR0Node)NodeFactory.FromFile(null, l4))
            {
                NativeAnimationOptimization.RequireFloatDenseExportPlans(fresh, required);
                Check(required != null, "L4 v" + version + ": a fresh complete native whole-R L4 plan satisfies its frozen format claim");
                // Add an independently exact compact root group to the claim,
                // then reject the earlier L4 file that lacks that chosen plan.
                var rootEntry = (CHR0EntryNode)candidate.FindChild(root, false);
                var exact = NativeCompactAnimationStorage.CreateAlternatives(rootEntry, 2, true, true, new int[0], 1).First();
                NativeCompactAnimationStorage.ApplyGroup(candidate, exact);
                var complete = NativeAnimationOptimization.CaptureFloatDenseExportPlans(candidate, master);
                bool incomplete = false;
                try { NativeAnimationOptimization.RequireFloatDenseExportPlans(fresh, complete); }
                catch (InvalidDataException error) { incomplete = Equals(error.Data["OptimizationQuality"], true); }
                Check(incomplete, "L4 v" + version + ": matching R payloads alone cannot hide loss of another explicitly chosen native storage plan");
            }
            string i12 = Path.Combine(stage, "v" + version + "-format-claim-i12.chr0");
            using (var fallback = NativeAnimationOptimization.CopySource(master)) fallback.ExportChecked(i12);
            using (var fresh = (CHR0Node)NodeFactory.FromFile(null, i12))
            {
                bool hasDense = fresh.Children.OfType<CHR0EntryNode>().Any(entry => {
                    var stored = PreciseCHR0EntryNode.CaptureStorage(entry);
                    return stored != null && Enumerable.Range(3, 3).Any(channel => stored[channel]?.IsDense == true);
                });
                Check(!hasDense && NativeAnimationOptimization.SourceContentKey(fresh) == NativeAnimationOptimization.SourceContentKey(candidate),
                    "L4 v" + version + ": the simulated I12 fallback has equal decoded motion but contains no actual dense storage");
                bool rejected = false;
                try { NativeAnimationOptimization.RequireFloatDenseExportPlans(fresh, required); }
                catch (InvalidDataException error) { rejected = Equals(error.Data["OptimizationQuality"], true); }
                Check(rejected, "L4 v" + version + ": every new L4 claim is checked after reopen even when HasDenseStorage is false");
            }
        }
        // A fixed -zero VALUE is physically representable; its time/tangent
        // are canonical +zero and its exact value bits must survive the slot.
        using (var baseline = NativeAnimationOptimization.CopySource(master))
        {
            var entry = (CHR0EntryNode)baseline.FindChild(body, false); entry.KeyArrays[4].Entries.Single()._value = negativeZero;
            var negativeValue = NativeCompactAnimationStorage.CreateFloatDenseAlternatives(entry, entry, false, new[] { 0, 16 }, 2000000,
                new NativeDenseProofBudget(), .1, .1, sites).Single();
            using (var candidate = NativeAnimationOptimization.CopySource(baseline))
            {
                NativeCompactAnimationStorage.ApplyGroup(candidate, negativeValue);
                var plans = NativeAnimationOptimization.CaptureFloatDenseExportPlans(candidate, baseline);
                string path = Path.Combine(stage, "v" + version + "-fixed-negative-zero-value.chr0"); candidate.ExportChecked(path);
                using (var fresh = (CHR0Node)NodeFactory.FromFile(null, path))
                {
                    NativeAnimationOptimization.RequireFloatDenseExportPlans(fresh, plans);
                    var fixedKey = ((CHR0EntryNode)fresh.FindChild(body, false)).KeyArrays[4].Entries.Single();
                    Check(ReplayFloatBits(fixedKey._value) == int.MinValue && ReplayFloatBits(fixedKey._index) == 0 && ReplayFloatBits(fixedKey._tangent) == 0,
                        "L4 v" + version + ": a canonical fixed companion retains its authored -zero value without claiming a timed/sloped fixed axis");
                }
            }
        }
    }

    static void FloatDenseEndpointAndFractionalChecks(string stage)
    {
        using (var clip = new CHR0Node { Name = "Unused native endpoint-side oracle", Version = 4, FrameCount = 3 })
        {
            var entry = clip.CreateEntry("anonymous rotation joint");
            entry.Keyframes.SetFrameValue(3, 0, 0, true)._tangent = 12;
            entry.Keyframes.SetFrameValue(3, 0, 0, true)._tangent = 1;
            entry.Keyframes.SetFrameValue(3, 1, 1, true)._tangent = 1;
            entry.Keyframes.SetFrameValue(3, 2, 2, true)._tangent = 1;
            var curve = IndexedCHR0CurveStorage.CreateFloatDense(new[] { 0f, 1f, 2f, 2f }, 3);
            var site = new NativeGeneratedPairSite { Bone = entry.Name, Channel = 3, Frame = 0 };
            Check(NativeCompactAnimationStorage.DenseLinearWithinBoundsPartitioned(entry.KeyArrays[3].Entries.ToArray(), entry.KeyArrays[3], curve.EncodedValues,
                .1, .1, CancellationToken.None, new NativeDenseProofBudget()) &&
                !NativeCompactAnimationStorage.FloatDenseExportCurveWithin(entry, 3, curve, new int[0], new[] { site }, .1, .1, new NativeDenseProofBudget(), CancellationToken.None),
                "L4 separately protects an unused authored first-incoming tangent even when every positive-duration interval is an exact line");
            entry.KeyArrays[3].Entries.First()._tangent = 1;
            entry.Keyframes.SetFrameValue(3, 2, 2, true)._tangent = -12;
            var finalSite = new NativeGeneratedPairSite { Bone = entry.Name, Channel = 3, Frame = 2 };
            Check(NativeCompactAnimationStorage.DenseLinearWithinBoundsPartitioned(entry.KeyArrays[3].Entries.ToArray(), entry.KeyArrays[3], curve.EncodedValues,
                .1, .1, CancellationToken.None, new NativeDenseProofBudget()) &&
                !NativeCompactAnimationStorage.FloatDenseExportCurveWithin(entry, 3, curve, new int[0], new[] { site, finalSite }, .1, .1, new NativeDenseProofBudget(), CancellationToken.None),
                "L4 separately protects the unused final-outgoing tangent as well as the physical terminal hold");
        }
        using (var clip = new CHR0Node { Name = "Fractional hidden hump oracle", Version = 5, FrameCount = 2 })
        {
            var entry = clip.CreateEntry("anonymous rotation joint");
            foreach (float time in new[] { 0f, .125f, .25f, .375f, .5f, 1f }) entry.Keyframes.SetFrameValue(3, time, time == .25f ? 1 : 0, true)._tangent = 0;
            Check(!NativeCompactAnimationStorage.DenseLinearWithinBoundsPartitioned(entry.KeyArrays[3].Entries.ToArray(), entry.KeyArrays[3], new[] { 0f, 0f, 0f },
                .1, .1, CancellationToken.None, new NativeDenseProofBudget()),
                "L4 compares every original fractional breakpoint so an overshoot cannot hide before an integer midpoint");
        }
        using (var clip = new CHR0Node { Name = "Already sparse native curve", Version = 4, FrameCount = 17 })
        {
            var entry = clip.CreateEntry("anonymous rotation joint");
            entry.Keyframes.SetFrameValue(3, 0, 0, true)._tangent = 1f / 4096;
            entry.Keyframes.SetFrameValue(3, 16, 16f / 4096, true)._tangent = 1f / 4096;
            Check(NativeCompactAnimationStorage.CreateFloatDenseAlternatives(entry, entry, false, new int[0], 2000000,
                new NativeDenseProofBudget(), .1, .1, new NativeGeneratedPairSite[0]).Length == 0,
                "Two sparse I12 rows already beat 18 L4 floats; fewer keys and actual payload cost both retain the smaller native curve");
        }
    }

    static void FloatDenseHeldTerminalProjectionChecks(CHR0Node master, CHR0Node freshlyDecoded, string bone, int version)
    {
        string content = NativeAnimationOptimization.SourceContentKey(freshlyDecoded), storage = NativeAnimationOptimization.StorageKey(freshlyDecoded);
        var entry = (CHR0EntryNode)freshlyDecoded.FindChild(bone, false);
        var encoded = PreciseCHR0EntryNode.CaptureStorage(entry)[3];
        var physical = encoded.EncodedValues; int last = freshlyDecoded.FrameCount - 1;
        float incoming = physical[last] - physical[last - 1];
        var channels = new HashSet<string>(StringComparer.Ordinal) { bone + "\0" + 3 };
        var nativeLast = ((CHR0EntryNode)master.FindChild(bone, false)).KeyArrays[3].Entries.Where(key => key._index == last).ToArray();
        Check(incoming != 0 && nativeLast.Length == 1 && ReplayFloatBits(nativeLast[0]._tangent) == ReplayFloatBits(incoming) &&
            ReplayFloatBits(physical[last + 1]) == ReplayFloatBits(physical[last]),
            "L4 v" + version + ": unchanged nonflat fixture has a nonzero native incoming endpoint and a separately held physical terminal");
        using (var legacy = NativeCompactAnimationStorage.CreateLinearProjection(NativeAnimationOptimization.CopySource(freshlyDecoded), channels))
        using (var verified = NativeCompactAnimationStorage.CreateLinearProjection(NativeAnimationOptimization.CopySource(freshlyDecoded), channels, channels))
        {
            var oldArray = ((CHR0EntryNode)legacy.FindChild(bone, false)).KeyArrays[3];
            var array = ((CHR0EntryNode)verified.FindChild(bone, false)).KeyArrays[3];
            var oldSides = oldArray.Entries.Where(key => key._index == last).ToArray();
            var sides = array.Entries.Where(key => key._index == last).ToArray();
            Check(oldArray.Entries.Last()._index == last + 1 && oldSides.Length == 2 &&
                ReplayFloatBits(oldSides[0]._tangent) == ReplayFloatBits(incoming) && ReplayFloatBits(oldSides[1]._tangent) == 0,
                "L4 v" + version + ": default full-physical projection retains its historical explicit outgoing endpoint side");
            Check(array.FrameLimit == freshlyDecoded.FrameCount && array.Entries.Last()._index == last && sides.Length == 1 &&
                ReplayFloatBits(sides[0]._value) == ReplayFloatBits(nativeLast[0]._value) &&
                ReplayFloatBits(sides[0]._tangent) == ReplayFloatBits(nativeLast[0]._tangent),
                "L4 v" + version + ": only verified held L4 uses the native incoming endpoint side without changing its authored tuple");
            Check(Enumerable.Range(0, (last + 1) * 4 + 1).Select(i => i * .25f).All(time =>
                Math.Abs(array.GetFrameValue(time) - DenseSampleLinear(physical, time)) < 1e-8 &&
                Math.Abs(oldArray.GetFrameValue(time) - DenseSampleLinear(physical, time)) < 1e-8),
                "L4 v" + version + ": native-clamped and full-physical projections match independent linear values through the complete N+1 held sample");
            Check(Math.Abs((array.GetFrameValue(last) - array.GetFrameValue(last - .25f)) * 4d - incoming) < 1e-8 &&
                Math.Abs((oldArray.GetFrameValue(last) - oldArray.GetFrameValue(last - .25f)) * 4d - incoming) < 1e-8 &&
                array.GetFrameValue(last + .25f) == array.GetFrameValue(last) && oldArray.GetFrameValue(last + .25f) == oldArray.GetFrameValue(last),
                "L4 v" + version + ": both value functions retain the same left derivative and zero right derivative at the nonflat endpoint");
        }
        Check(NativeAnimationOptimization.SourceContentKey(freshlyDecoded) == content && NativeAnimationOptimization.StorageKey(freshlyDecoded) == storage &&
            encoded.EncodedValues.Length == freshlyDecoded.FrameCount + 1 && encoded.ContentKey == PreciseCHR0EntryNode.CaptureStorage(entry)[3].ContentKey,
            "L4 v" + version + ": diagnostic endpoint representation never changes the complete immutable N+1 physical payload or decoded candidate");

        foreach (string variant in new[] { "unverified", "unheld", "loop", "L1", "L2", "wrong-native-count", "one-frame" })
        {
            int nativeCount = variant == "one-frame" ? 1 : 3;
            var literal = variant == "L1" || variant == "L2" ?
                IndexedCHR0CurveStorage.CreateDense(variant == "L1" ? AnimDataFormat.L1 : AnimDataFormat.L2, new[] { 0, 1, 2, 2 }, 3, 0, 1) :
                IndexedCHR0CurveStorage.CreateFloatDense(variant == "one-frame" ? new[] { 2f, 2f } :
                    variant == "unheld" ? new[] { 0f, 1f, 2f, 3f } : new[] { 0f, 1f, 2f, 2f }, nativeCount);
            using (var subject = DenseOwnedClip(variant == "wrong-native-count" ? 4 : nativeCount, variant == "loop", version, literal))
            {
                var subjectEntry = (CHR0EntryNode)subject.Children[0]; var retained = new IndexedCHR0CurveStorage[9]; retained[3] = literal;
                PreciseCHR0EntryNode.RestoreStorage(subjectEntry, new IndexedCHR0EntryStorage(retained));
                var selected = new HashSet<string>(StringComparer.Ordinal) { subjectEntry.Name + "\0" + 3 };
                NativeCompactAnimationStorage.CreateLinearProjection(subject, selected, variant == "unverified" ? null : selected);
                Check(subjectEntry.KeyArrays[3].Entries.Last()._index == literal.EncodedSampleCount - 1,
                    "L4 v" + version + ": verified-new-L4 endpoint shortcut leaves legacy/unqualified projection unchanged / " + variant);
            }
        }
    }

    // Call from each existing authentic finished-target workflow case after
    // retarget/fitting/contact metadata is registered normally. This invokes
    // the real frozen export context, Core proof, native geometry/contact gates
    // and atomic publication; unavailable protection context may fall back.
    static async Task NativeDenseFloatAuthenticExportCopy(ModelForm form, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Authentic L4 export checks require the private desktop.");
        Directory.CreateDirectory(output); var editor = form.FighterEditor; var clip = editor.SelectedCHR0;
        string content = NativeAnimationOptimization.SourceContentKey(clip), storage = NativeAnimationOptimization.StorageKey(clip);
        var model = editor.TargetModel; var bone = editor.SelectedBone; int frame = editor.CurrentFrame, undo = editor._undoSaves.Count, redo = editor._redoSaves.Count;
        bool dirty = clip.IsDirty, rootDirty = clip.RootNode.IsDirty;
        var settings = new NativeExportOptimizationSettings { Policy = NativeExportOptimizationPolicy.ErrorBounded };
        using (var frozen = (IDisposable)Call(form, "FreezeOwnedWorkspaceExport", Call(form, "SelectedFighterAsset"), clip, true, null))
        {
            await (Task)Call(form, "PrepareOptimizedExportSnapshot", frozen, settings, CancellationToken.None, null);
            string path = Path.Combine(output, "finished-target-export.chr0"); Call(form, "ExportWorkspaceSnapshot", frozen, path);
            var candidate = (CHR0Node)Field(frozen, "Candidate");
            using (var fresh = (CHR0Node)NodeFactory.FromFile(null, path))
                Check(NativeAnimationOptimization.SourceContentKey(fresh) == NativeAnimationOptimization.SourceContentKey(candidate) &&
                    NativeAnimationOptimization.StorageKey(fresh) == NativeAnimationOptimization.StorageKey(candidate) &&
                    new FileInfo(path).Length <= (long)Field(frozen, "MasterBytes") && FighterProject.HashFile(path) == (string)Field(frozen, "PreparedEncodedSha256"),
                    "Authentic L4 export boundary publishes only the same independently decoded verified no-larger finished-target candidate");
            File.WriteAllText(Path.Combine(output, "size-quality-report.txt"), (string)Field(frozen, "OptimizationReport"));
        }
        Check(ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.TargetModel, model) && ReferenceEquals(editor.SelectedBone, bone) &&
            editor.CurrentFrame == frame && editor._undoSaves.Count == undo && editor._redoSaves.Count == redo && clip.IsDirty == dirty && clip.RootNode.IsDirty == rootDirty &&
            NativeAnimationOptimization.SourceContentKey(clip) == content && NativeAnimationOptimization.StorageKey(clip) == storage,
            "Authentic independent export preserves the current full-quality master, native selection/frame and Undo; normal successful export receipts remain separate");
    }
}
