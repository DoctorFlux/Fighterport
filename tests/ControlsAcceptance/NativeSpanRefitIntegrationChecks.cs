using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal static partial class Program
{
    sealed class RefitNativeKeyBits
    {
        public float Frame { get; set; } public int? FrameBits { get; set; }
        public int ValueBits { get; set; } public int TangentBits { get; set; }
    }
    sealed class RefitNativeTrackBits
    {
        public string Bone { get; set; } public RefitNativeKeyBits[][] Channels { get; set; }
        public int FrameLimit { get; set; } public bool Loop { get; set; }
    }
    sealed class RefitNativeReport
    {
        public long Work { get; set; } public int CandidateSpans { get; set; }
        public int AcceptedRefitSpans { get; set; } public int RefitTangents { get; set; }
        public int RefitCandidateSpans { get; set; } public double[] ProbeFrames { get; set; }
    }
    sealed class RefitNativeOutput
    {
        public RefitNativeTrackBits[] Tracks { get; set; } public RefitNativeTrackBits[] PreRefitTracks { get; set; }
        public RefitNativeReport Report { get; set; }
    }
    sealed class RefitNativeCertificate
    {
        public bool Accepted { get; set; } public bool WorkLimitReached { get; set; }
        public double MaximumValueBound { get; set; } public double MaximumSpeedBound { get; set; }
        public string Reason { get; set; }
    }
    sealed class RefitNativeCertificateOutput { public RefitNativeCertificate Certificate { get; set; } }

    static RefitNativeTrackBits[] RefitNativeWire(CHR0Node clip) => clip.Children.OfType<CHR0EntryNode>().Select(entry => new RefitNativeTrackBits {
        Bone = entry.Name, FrameLimit = entry.Keyframes.FrameLimit, Loop = entry.Keyframes.Loop,
        Channels = entry.KeyArrays.Select(array => array.Entries.Select(key => new RefitNativeKeyBits {
            Frame = key._index, FrameBits = ReplayFloatBits(key._index), ValueBits = ReplayFloatBits(key._value), TangentBits = ReplayFloatBits(key._tangent)
        }).ToArray()).ToArray()
    }).ToArray();
    static float RefitNativeFloat(int bits) => BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
    static ControlTrack[] RefitNativeTracks(RefitNativeTrackBits[] wire) => wire.Select(track => new ControlTrack {
        Bone = track.Bone, Channels = track.Channels.Select(channel => channel.Select(key => {
            float frame = key.FrameBits.HasValue ? RefitNativeFloat(key.FrameBits.Value) : key.Frame;
            if (float.IsNaN(frame) || float.IsInfinity(frame) || ReplayFloatBits(frame) != ReplayFloatBits(key.Frame))
                throw new InvalidDataException("Refit test reply changed its explicit native time bits.");
            return OptimizationKey(frame, RefitNativeFloat(key.ValueBits), RefitNativeFloat(key.TangentBits));
        }).ToArray()).ToArray()
    }).ToArray();

    static async Task NativeSpanRefitIntegrationChecks(string modelPath, string stage)
    {
        Directory.CreateDirectory(stage); string originalHash = FighterProject.HashFile(modelPath);
        var json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 200 };
        using (var owner = NodeFactory.FromFile(null, modelPath))
        {
            var model = OptimizationWalk(owner).OfType<MDL0Node>().OrderByDescending(m => m.NumVertices).First();
            model.ApplyCHR(null, 0); var bones = model.AllBones.ToArray();
            var root = bones.First(bone => !(bone.Parent is MDL0BoneNode));
            var leaf = bones.First(bone => !ReferenceEquals(bone, root) && !bone.Children.OfType<MDL0BoneNode>().Any()
                && Enumerable.Range(0, 9).All(c => ReplayFloatBits(bone.BindState[c]) != int.MinValue)
                && Enumerable.Range(1, 2).All(axis => {
                    double norm = Math.Sqrt(Enumerable.Range(0, 3).Sum(row => Math.Pow(bone.BindMatrix[axis * 4 + row], 2)));
                    return norm >= .9 && norm <= 1.1;
                }));
            string nativeModel = Path.Combine(stage, "independent-model.mdl0"); model.ExportChecked(nativeModel);
            double length = NativeAnimationOptimization.GetBindBoundsDiagonal(model);
            using (var baseline = new CHR0Node { Name = "Independent generated span and tangent fixture", Version = 5,
                FrameCount = 17, Loop = false, OriginalPath = "procedural/span-tangent-refit" })
            {
                foreach (var bone in new[] { root, leaf })
                {
                    var entry = baseline.CreateEntry(bone.Name);
                    entry.UseModelScale = entry.UseModelRotate = entry.UseModelTranslate = false;
                    entry.ScaleCompensateApply = bone.SegScaleCompApply; entry.ScaleCompensateParent = bone.SegScaleCompParent;
                    entry.ClassicScaleOff = !bone.ClassicScale; entry.Keyframes.PreserveInterpolation = true;
                    for (int c = 0; c < 9; c++) for (int frame = 0; frame < 17; frame++)
                        entry.Keyframes.SetFrameValue(c, frame, bone.BindState[c], true)._tangent = 0;
                    for (int frame = 0; frame < 17; frame++)
                    {
                        int channel = ReferenceEquals(bone, root) ? 6 : 3;
                        var key = entry.KeyArrays[channel].GetKeyframe(frame);
                        key._value += ReferenceEquals(bone, root) ? frame * .00002f : frame;
                        key._tangent = ReferenceEquals(bone, root) ? .00002f : .98f;
                    }
                }
                PreciseCHR0EntryNode.Promote(baseline);
                string content = NativeAnimationOptimization.SourceContentKey(baseline), storage = NativeAnimationOptimization.StorageKey(baseline);
                string modelState = DirtyModelMemory(model);
                var wire = RefitNativeWire(baseline);
                var limits = new NativeExportOptimizationSettings { Policy = NativeExportOptimizationPolicy.ErrorBounded,
                    SearchEffort = NativeExportSearchEffort.Thorough, RotationDegrees = .005, RotationDerivative = .1 };
                limits.Validate();
                object Input(bool enabled, RefitNativeTrackBits[] candidate = null) => new {
                    baseline.Name, SampleCount = baseline.FrameCount, baseline.Loop, HighQuality = true, SmallFile = true,
                    LengthScale = length, Tracks = wire, CandidateTracks = candidate, ExactBones = new[] { root.Name },
                    ProtectedBones = new[] { root.Name }, MandatoryFrames = new int[0], ProtectedRanges = new object[0],
                    ProtectionKnown = true, ExplicitBounds = limits, PrecisionScale = 1, MaximumWork = 2000000,
                    MaximumCandidateSpans = 100000, EnableSpanRefitting = enabled
                };
                using (var client = new AnimationControlClient())
                {
                    async Task<RefitNativeOutput> Produce(bool enabled, string label)
                    {
                        string input = Path.Combine(stage, label + "-input.json"), output = Path.Combine(stage, label + "-output.json");
                        File.WriteAllText(input, json.Serialize(Input(enabled)));
                        string hash = FighterProject.HashFile(input);
                        var reply = await client.Send(new { Operation = "animation-optimize-prepare", Model = nativeModel,
                            Optimization = new { InputPath = input, OutputPath = output } }).ConfigureAwait(false);
                        Check(reply.OptimizationPath == output && FighterProject.HashFile(input) == hash,
                            "Native refitting: actual worker preparation preserves the frozen scalar input / " + label);
                        return json.Deserialize<RefitNativeOutput>(File.ReadAllText(output));
                    }
                    var deletion = await Produce(false, "deletion-only").ConfigureAwait(false);
                    var fitted = await Produce(true, "span-refit").ConfigureAwait(false);
                    Check(deletion.Report != null && fitted.Report != null && deletion.Report.AcceptedRefitSpans == 0 &&
                        fitted.Report.AcceptedRefitSpans > 0 && fitted.Report.RefitTangents > 0 && fitted.Report.Work <= 2000000 && fitted.Report.CandidateSpans <= 100000,
                        "Native refitting: real Core worker exercises explicit refitting and unchanged shared proof/span ceilings");
                    var sharedBudget = new NativeAnimationOptimizationBudget();
                    NativeAnimationOptimizationOptions Options(NativeExportOptimizationSettings selectedLimits = null) => new NativeAnimationOptimizationOptions {
                        SmallFile = true, ExportSettings = selectedLimits ?? limits, LengthScale = length,
                        TargetBones = bones.Select(bone => bone.Name).ToArray(), ProtectedBones = new[] { root.Name }, ExactBones = new[] { root.Name },
                        ProbeFrames = fitted.Report.ProbeFrames ?? new double[0], Budget = sharedBudget
                    };
                    var deletionTracks = RefitNativeTracks(deletion.Tracks); var refitTracks = RefitNativeTracks(fitted.Tracks);
                    using (var deleted = NativeAnimationOptimization.Prepare(model, baseline, deletionTracks, Options(), stage))
                    using (var verified = NativeAnimationOptimization.Prepare(model, baseline, refitTracks, Options(), stage))
                    {
                        Check(verified.AfterBytes < deleted.AfterBytes && verified.AfterKeys < deleted.AfterKeys && verified.AfterBytes <= verified.BeforeBytes,
                            "Native refitting: complete precise native serialization really beats the same-master deletion-only bytes and stored scalar rows");
                        Check(verified.Candidate.Name == baseline.Name && verified.Candidate.Version == baseline.Version &&
                            verified.Candidate.FrameCount == baseline.FrameCount && verified.Candidate.Loop == baseline.Loop &&
                            verified.Candidate.OriginalPath == baseline.OriginalPath,
                            "Native refitting: CHR0 metadata, duration, native version and loop state survive fresh serialization/reopen");
                        SourceKeysSameTrack((CHR0EntryNode)baseline.FindChild(root.Name, false),
                            (CHR0EntryNode)verified.Candidate.FindChild(root.Name, false),
                            "Native refitting: moving source-owned root controls retain every native time/value/tangent tuple");
                        var measured = SourceKeyQualityMeasure(model, baseline, verified.Candidate);
                        Check(measured.Euler > 0 && measured.Euler <= limits.RotationDegrees && measured.Orientation <= limits.RotationDegrees &&
                            measured.EulerDerivative <= limits.RotationDerivative && measured.Translation <= limits.Translation && measured.Scale <= limits.Scale &&
                            measured.World <= limits.GeometryRelative * length && measured.Skin <= limits.GeometryRelative * length && verified.Samples >= 65,
                            "Native refitting: withheld native subframes independently bound complete hierarchy and all skinned vertices against the unchanged finished master");
                        string firstFile = Path.Combine(stage, "refitted-first-export.chr0"); verified.Candidate.ExportChecked(firstFile);
                        using (var fresh = (CHR0Node)NodeFactory.FromFile(null, firstFile))
                        {
                            string secondFile = Path.Combine(stage, "refitted-resaved.chr0"); fresh.ExportChecked(secondFile);
                            using (var second = (CHR0Node)NodeFactory.FromFile(null, secondFile))
                                Check(NativeAnimationOptimization.SourceContentKey(second) == NativeAnimationOptimization.SourceContentKey(verified.Candidate) &&
                                    NativeAnimationOptimization.StorageKey(second) == NativeAnimationOptimization.StorageKey(verified.Candidate),
                                    "Native refitting: export/reopen/save/reopen retains every fitted coefficient, flag and native storage policy");
                            string decodedInput = Path.Combine(stage, "decoded-certificate-input.json"), decodedOutput = Path.Combine(stage, "decoded-certificate-output.json");
                            File.WriteAllText(decodedInput, json.Serialize(Input(true, RefitNativeWire(fresh))));
                            var reply = await client.Send(new { Operation = "animation-optimize-prepare", Model = nativeModel,
                                Optimization = new { InputPath = decodedInput, OutputPath = decodedOutput } }).ConfigureAwait(false);
                            var certificate = json.Deserialize<RefitNativeCertificateOutput>(File.ReadAllText(decodedOutput)).Certificate;
                            Check(reply.OptimizationPath == decodedOutput && certificate.Accepted && !certificate.WorkLimitReached &&
                                certificate.MaximumValueBound <= limits.RotationDegrees && certificate.MaximumSpeedBound <= limits.RotationDerivative,
                                "Native refitting: a separately requested continuous certificate validates fresh decoded native output against the original uncompressed master");
                        }
                        var tighter = limits.Clone(); tighter.GeometryRelative = 1e-7;
                        bool collectiveRejected = false;
                        try { using (var ignored = NativeAnimationOptimization.Prepare(model, baseline, refitTracks, Options(tighter), stage)) { } }
                        catch (InvalidDataException error) { collectiveRejected = error.Data.Contains("OptimizationQuality"); }
                        Check(collectiveRejected,
                            "Native refitting: a scalar-certified fit cannot bypass a tighter saved complete-model error ceiling");
                        Check(sharedBudget.Samples > verified.Samples && verified.MaximumSkinError <= limits.GeometryRelative * length,
                            "Native refitting: independent byte controls and complete candidates consume one shared native work budget without resetting error against an approximation");
                        File.WriteAllText(Path.Combine(stage, "native-span-refit-evidence.json"), json.Serialize(new {
                            Passed = true, Root = root.Name, Body = leaf.Name, Limits = limits, LengthScale = length,
                            SourceContent = content, DeletionOnlyBytes = deleted.AfterBytes, RefitBytes = verified.AfterBytes,
                            DeletionOnlyKeys = deleted.AfterKeys, RefitKeys = verified.AfterKeys, Core = fitted.Report,
                            NativeMeasured = measured, verified.Samples, SharedSamples = sharedBudget.Samples,
                            SharedBoneVisits = sharedBudget.BoneVisits, SharedVertexVisits = sharedBudget.VertexVisits,
                            ModelAndAllSkinWeightsUnchanged = DirtyModelMemory(model) == modelState,
                            Claim = "Continuous scalar and sampled complete native checks; no physical input, absolute contact or game acceptance."
                        }));
                    }
                }
                Check(NativeAnimationOptimization.SourceContentKey(baseline) == content && NativeAnimationOptimization.StorageKey(baseline) == storage &&
                    DirtyModelMemory(model) == modelState && FighterProject.HashFile(modelPath) == originalHash,
                    "Native refitting: preparation, native rejection, publication/reopen and repeated proof preserve the full master, authentic hierarchy/binds and every original skin coefficient");
            }
        }
        await NativeSpanRefitPairGuardChecks(modelPath, Path.Combine(stage, "paired-protections")).ConfigureAwait(false);
    }

    static async Task NativeSpanRefitPairGuardChecks(string modelPath, string stage)
    {
        Directory.CreateDirectory(stage);
        var json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 200 };
        using (var owner = NodeFactory.FromFile(null, modelPath))
        {
            var model = OptimizationWalk(owner).OfType<MDL0Node>().OrderByDescending(m => m.NumVertices).First();
            model.ApplyCHR(null, 0); var bones = model.AllBones.ToArray();
            var root = bones.First(b => !(b.Parent is MDL0BoneNode));
            var body = bones.First(b => !ReferenceEquals(b, root) && !b.Children.OfType<MDL0BoneNode>().Any()
                && Enumerable.Range(0, 9).All(c => ReplayFloatBits(b.BindState[c]) != int.MinValue));
            string modelFile = Path.Combine(stage, "pair-model.mdl0"); model.ExportChecked(modelFile);
            using (var source = new CHR0Node { Name = "Independent paired refit guards", Version = 5, FrameCount = 5,
                Loop = false, OriginalPath = "procedural/paired-refit-protections" })
            {
                foreach (var bone in new[] { root, body })
                {
                    var entry = source.CreateEntry(bone.Name); entry.Keyframes.PreserveInterpolation = true;
                    entry.UseModelScale = entry.UseModelRotate = entry.UseModelTranslate = false;
                    entry.ScaleCompensateApply = bone.SegScaleCompApply; entry.ScaleCompensateParent = bone.SegScaleCompParent; entry.ClassicScaleOff = !bone.ClassicScale;
                    for (int c = 0; c < 9; c++) for (int t = 0; t < 5; t++) entry.Keyframes.SetFrameValue(c, t, bone.BindState[c], true)._tangent = 0;
                }
                var originalBody = (CHR0EntryNode)source.FindChild(body.Name, false);
                OptimizationReplace(originalBody, 3, new[] {
                    OptimizationKey(0, body.BindState[3], 1), OptimizationKey(2, body.BindState[3] + 2, 1),
                    OptimizationKey(2, body.BindState[3] + 2, 1.02f), OptimizationKey(4, body.BindState[3] + 4, 1) });
                var originalRoot = (CHR0EntryNode)source.FindChild(root.Name, false);
                for (int t = 0; t < 5; t++) { originalRoot.KeyArrays[6].GetKeyframe(t)._value += t * .00002f; originalRoot.KeyArrays[6].GetKeyframe(t)._tangent = .00002f; }
                PreciseCHR0EntryNode.Promote(source);
                string content = NativeAnimationOptimization.SourceContentKey(source), storage = NativeAnimationOptimization.StorageKey(source);
                var limits = new NativeExportOptimizationSettings { Policy = NativeExportOptimizationPolicy.ErrorBounded,
                    RotationDegrees = .005, RotationDerivative = .1 };
                var site = new NativeGeneratedPairSite { Bone = body.Name, Channel = 3, Frame = 2 };
                double length = NativeAnimationOptimization.GetBindBoundsDiagonal(model);
                NativeAnimationOptimizationOptions Options() => new NativeAnimationOptimizationOptions {
                    SmallFile = true, AllowSpanRefitting = true, ExportSettings = limits, LengthScale = length,
                    TargetBones = bones.Select(b => b.Name).ToArray(), ExactBones = new[] { root.Name }, ProtectedBones = new[] { root.Name },
                    GeneratedPairSites = new[] { site }, Budget = new NativeAnimationOptimizationBudget()
                };
                ControlTrack[] Proposal(bool single)
                {
                    var tracks = source.Children.OfType<CHR0EntryNode>().Select(OptimizationTrack).ToArray();
                    var track = tracks.Single(t => t.Bone == body.Name);
                    for (int c = 0; c < 9; c++) if (c != 3) track.Channels[c] = new[] { track.Channels[c][0] };
                    var original = track.Channels[3];
                    track.Channels[3] = single
                        ? new[] { original[0], OptimizationKey(2, original[1].Value, 1.01f), original[3] }
                        : new[] { original[0], OptimizationKey(2, original[1].Value, 1.002f), OptimizationKey(2, original[2].Value, 1.018f), original[3] };
                    return tracks;
                }
                RefitNativeTrackBits[] Wire(ControlTrack[] tracks) => tracks.Select(track => new RefitNativeTrackBits {
                    Bone = track.Bone, FrameLimit = source.FrameCount, Loop = false,
                    Channels = track.Channels.Select(channel => channel.Select(key => new RefitNativeKeyBits {
                        Frame = key.Frame, FrameBits = ReplayFloatBits(key.Frame), ValueBits = ReplayFloatBits(key.Value), TangentBits = ReplayFloatBits(key.Tangent)
                    }).ToArray()).ToArray()
                }).ToArray();
                using (var client = new AnimationControlClient())
                {
                    foreach (bool single in new[] { true, false })
                    {
                        var proposed = Proposal(single); string label = single ? "averaged-single-side" : "changed-two-sides";
                        string input = Path.Combine(stage, label + "-certificate-input.json"), output = Path.Combine(stage, label + "-certificate-output.json");
                        File.WriteAllText(input, json.Serialize(new {
                            source.Name, SampleCount = source.FrameCount, source.Loop, HighQuality = true, SmallFile = true, EnableSpanRefitting = true,
                            LengthScale = length, Tracks = RefitNativeWire(source), CandidateTracks = Wire(proposed),
                            ExactBones = new[] { root.Name }, ProtectedBones = new[] { root.Name }, MandatoryFrames = new int[0],
                            ProtectedRanges = new object[0], GeneratedPairSites = new[] { site }, ProtectionKnown = true, ExplicitBounds = limits
                        }));
                        await client.Send(new { Operation = "animation-optimize-prepare", Model = modelFile,
                            Optimization = new { InputPath = input, OutputPath = output } }).ConfigureAwait(false);
                        var certificate = json.Deserialize<RefitNativeCertificateOutput>(File.ReadAllText(output)).Certificate;
                        Check(certificate.Accepted && !certificate.WorkLimitReached,
                            "Native paired refitting: actual continuous Core proof accepts the independently authored legal slope proposal / " + label);
                        using (var verified = NativeAnimationOptimization.Prepare(model, source, proposed, Options(), stage))
                        {
                            var result = (CHR0EntryNode)verified.Candidate.FindChild(body.Name, false);
                            var actual = result.KeyArrays[3].Entries.Where(k => k._index == 2).ToArray();
                            Check(actual.Length == (single ? 1 : 2) && actual.Select(k => ReplayFloatBits(k._tangent))
                                .SequenceEqual(proposed.Single(t => t.Bone == body.Name).Channels[3].Where(k => k.Frame == 2).Select(k => ReplayFloatBits(k.Tangent))),
                                "Native paired refitting: typed generated provenance and explicit permission retain the new one/two-sided fitted slopes through precise encoding / " + label);
                            SourceKeysSameTrack(originalRoot, (CHR0EntryNode)verified.Candidate.FindChild(root.Name, false),
                                "Native paired refitting: changed body corner never changes source-owned root travel");
                        }
                    }
                }
                void Reject(CHR0Node baseline, ControlTrack[] proposed, NativeAnimationOptimizationOptions options, string label)
                {
                    bool rejected = false;
                    try { using (var ignored = NativeAnimationOptimization.Prepare(model, baseline, proposed, options, stage)) { } }
                    catch (InvalidDataException error)
                    {
                        rejected = !error.Data.Contains("OptimizationQuality") && !error.Data.Contains("OptimizationBudget") &&
                            (error.Message.IndexOf("mandatory", StringComparison.OrdinalIgnoreCase) >= 0 || error.Message.IndexOf("protected", StringComparison.OrdinalIgnoreCase) >= 0
                                || error.Message.IndexOf("discontinuous", StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                    Check(rejected, "Native paired refitting: structural provenance guard rejects " + label + " before a numerical quality escape");
                }
                var proposal = Proposal(true);
                var disabled = Options(); disabled.AllowSpanRefitting = false; Reject(source, proposal, disabled, "an averaged tangent without explicit refitting permission");
                var unknown = Options(); unknown.GeneratedPairSites = new NativeGeneratedPairSite[0]; Reject(source, proposal, unknown, "unknown authored pair sides");
                var frameWide = Options(); frameWide.GeneratedPairSites = new NativeGeneratedPairSite[0]; frameWide.GeneratedPairFrames = new[] { 2f };
                Reject(source, proposal, frameWide, "frame-wide evidence without exact typed bone/channel provenance");
                var wrongChannel = Options(); wrongChannel.GeneratedPairSites = new[] { new NativeGeneratedPairSite { Bone = body.Name, Channel = 6, Frame = 2 } };
                Reject(source, proposal, wrongChannel, "a typed site for a different scalar channel");
                var exact = Options(); exact.ExactBones = new[] { root.Name, body.Name }; Reject(source, proposal, exact, "an exact source-owned body/control curve");
                var pin = Options(); pin.ProtectedBones = new[] { root.Name, body.Name }; Reject(source, proposal, pin, "an authored pin/contact/attachment dependency");
                var mandatory = Options(); mandatory.MandatoryFrames = new[] { 2 }; Reject(source, proposal, mandatory, "an exact mandatory knot");
                var join = Options(); join.FitJoinFrames = new[] { 2 }; Reject(source, proposal, join, "a fitting join");
                using (var jump = NativeAnimationOptimization.CopySource(source))
                {
                    ((CHR0EntryNode)jump.FindChild(body.Name, false)).KeyArrays[3].Entries.Where(k => k._index == 2).Last()._value += .001f;
                    Reject(jump, proposal, Options(), "a real equal-time value discontinuity");
                }
                using (var signed = NativeAnimationOptimization.CopySource(source))
                {
                    var pair = ((CHR0EntryNode)signed.FindChild(body.Name, false)).KeyArrays[3].Entries.Where(k => k._index == 2).ToArray();
                    pair[0]._tangent = RefitNativeFloat(int.MinValue); pair[1]._tangent = .02f;
                    var signedProposal = SelectiveReductionClone(proposal); signedProposal.Single(t => t.Bone == body.Name).Channels[3].Single(k => k.Frame == 2).Tangent = .01f;
                    Reject(signed, signedProposal, Options(), "signed-zero authored evidence");
                }
                Check(NativeAnimationOptimization.SourceContentKey(source) == content && NativeAnimationOptimization.StorageKey(source) == storage,
                    "Native paired refitting: accepted typed cases and every denied protection leave the original native pair, flags and full source animation unchanged");
            }
        }
    }
}
