using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    internal static partial class RetargetKeyframes
    {
        // Source times are seeds, not permission to omit target-space motion.
        // Every accepted span is compared to the immutable original dense curve;
        // no progressively approximated candidate becomes a new baseline.
        sealed class QualityProofBudget
        {
            internal long Work;
            internal int Spans;
            internal bool Limited;
            internal readonly SortedSet<double> Probes = new SortedSet<double>();
            internal readonly CancellationToken Token;
            internal QualityProofBudget(CancellationToken token) { Token = token; }
            internal void Probe(double frame) { if (Probes.Count < 10000) Probes.Add(frame); }
            internal bool Charge(int amount)
            {
                Token.ThrowIfCancellationRequested();
                if (amount > 2000000 - Work) { Limited = true; return false; }
                Work += amount; return true;
            }
            internal bool Span()
            {
                if (Spans >= 100000) { Limited = true; return false; }
                Spans++; return Charge(1);
            }
        }

        sealed class QualityWire
        {
            internal ControlTrack[] Tracks;
            internal string[] BindOmissions;
            internal int SeedKeys;
            internal readonly Dictionary<string, float[][]> SeedFrames = new Dictionary<string, float[][]>(StringComparer.Ordinal);
            internal double[] Value = new double[3], Derivative = new double[3];
            internal readonly List<string> RestoredTracks = new List<string>();
            internal readonly List<string> RestoredChannels = new List<string>();
        }

        // Identity proves equality, not valid numeric data. Run this bounded
        // input check before any missing-geometry or exhausted-budget fallback.
        static void ValidateDenseInput(CHR0Node dense,CancellationToken token)
        {
            if(dense.FrameCount<1||dense.FrameCount>ushort.MaxValue)
                throw new InvalidDataException("The dense reference timing exceeds native CHR0 frame limits.");
            int maximum=dense.Loop?dense.FrameCount:dense.FrameCount-1;
            long count=0;
            foreach(var node in dense.Children)
            {
                token.ThrowIfCancellationRequested();
                if(!(node is CHR0EntryNode entry)||entry.KeyArrays.Length!=9)
                    throw new InvalidDataException("The dense reference requires complete native bone channels.");
                foreach(var array in entry.KeyArrays)
                {
                    float previous=-1; int sides=0;
                    foreach(var key in array.Entries)
                    {
                        if((++count&255)==0)token.ThrowIfCancellationRequested();
                        if(count>2000000)throw new InvalidDataException("The dense reference exceeds the two-million-key operation limit.");
                        if(float.IsNaN(key._index)||float.IsInfinity(key._index)||key._index<0||key._index>maximum||key._index<previous)
                            throw new InvalidDataException("The dense reference has invalid native key timing.");
                        sides=key._index==previous?sides+1:1;previous=key._index;
                        if(sides>2)throw new InvalidDataException("The dense reference has more than two native paired-key sides.");
                        if(float.IsNaN(key._value)||float.IsInfinity(key._value)||float.IsNaN(key._tangent)||float.IsInfinity(key._tangent))
                            throw new InvalidDataException("The dense reference contains a non-finite authored key value or tangent.");
                    }
                }
            }
        }

        static CHR0Node QualityCandidate(RetargetResult result, BrawlRig target, CHR0Node sourceClip,
            RetargetKeyframeMode mode, RetargetKeyframeReport report, string folder,
            NativeAnimationOptimizationBudget nativeBudget, CancellationToken token, Action<int, string> progress)
        {
            var dense = result.Animation;
            var model = target.Bones.FirstOrDefault()?.Model;
            if (model == null) throw new InvalidDataException("Source-key quality requires the independently owned target model.");
            double length = NativeAnimationOptimization.GetBindBoundsDiagonal(model);
            if (double.IsNaN(length) || double.IsInfinity(length) || length < 0)
                throw new InvalidDataException("The target bind-geometry scale is not finite.");
            report.LengthScale = length;
            var proof = new QualityProofBudget(token);
            var controls = new HashSet<string>(report.ProtectedControls, StringComparer.Ordinal);
            // Bake creates evidence before this projection, but registers the
            // surviving result only afterwards. Freeze its exact ordered bits
            // here; neither a matching name nor equal-looking sides suffice.
            var generatedPairs = FreezeQualityGeneratedPairs(result, model, controls, token);
            if(mode==RetargetKeyframeMode.Interpolated)
                report.QualityNotes="Native Hermite target spans are selected independently of donor key schedules, even when the donor has a key on every frame. Exact protected controls, unknown paired sides, signed zeros and required terminal behavior remain. ";
            var sourceTracks = sourceClip.Children.OfType<CHR0EntryNode>().ToDictionary(t => t.Name, StringComparer.Ordinal);
            QualityWire lastWire = null, coarseWire = null;
            InvalidDataException coarseFailure = null;
            foreach (double precision in length > 0 ? new[] { 1d, .5, .25, .125, 0d } : new[] { 0d })
            {
                token.ThrowIfCancellationRequested();
                report.QualityAttempts++;
                // Use the existing fifth slot for a conservative candidate
                // before exact identity: preserve every paired channel as in
                // the prior native path, still allowing exact constants and
                // independently certified unpaired spans to save storage.
                // This shares all work/native caps; it is not looser motion.
                bool preservePairs = precision == 0 && length > 0 && generatedPairs.Length > 0;
                if (precision == 0 && !preservePairs)
                {
                    report.DenseFallback = true; report.SourceSeedKeys = DenseSeedCount(dense,lastWire);
                    report.AddedQualityKeys = Math.Max(0, Count(dense) - report.SourceSeedKeys);
                    report.RestoredTargetTracks = lastWire?.RestoredTracks.ToArray() ?? new string[0];
                    report.RestoredTargetChannels = lastWire?.RestoredChannels.ToArray() ?? new string[0];
                    report.ScalarCertified = report.QualityVerified = true; report.ScalarCertificateWork = proof.Work;
                    report.BoneVisits=nativeBudget.BoneVisits;report.VertexVisits=nativeBudget.VertexVisits;
                    report.QualityNotes = (report.QualityNotes ?? "") + "The complete dense reference was retained after bounded quality preparation; exact coefficient identity is the final scalar certificate. " +
                        (lastWire == null ? "Source seed analysis was skipped because positive finite bind geometry was unavailable. " : "") + "No approximation was published. The legacy separate MaximumWorldBasisError field is not measured or claimed in v2.";
                    return NativeAnimationOptimization.Copy(dense);
                }
                var attemptPairs = preservePairs ? new NativeGeneratedPairSite[0] : generatedPairs;
                var wire = QualityTracks(result, target, sourceTracks, mode, controls, attemptPairs, preservePairs ? 1 : precision, length, proof);
                lastWire = wire;
                if (coarseWire == null) coarseWire = wire;
                try
                {
                    progress?.Invoke(92, mode==RetargetKeyframeMode.Interpolated?"Verifying interpolated curves against the complete dense motion...":"Verifying source-seeded curves against the complete dense motion...");
                    var options = new NativeAnimationOptimizationOptions { ExactOnly = false, LengthScale = length,
                            TargetBones = target.Bones.Select(b => b.Name).ToArray(), ExactBones = controls.ToArray(),
                            AllowGeneratedPairReduction = !preservePairs, GeneratedPairSites = attemptPairs,
                            BindPoseCleanupBones = wire.BindOmissions, Budget = nativeBudget, ProbeFrames = proof.Probes.ToArray(),
                            CompactCandidateAttemptLimit = 6 };
                    using (var verified = NativeAnimationOptimization.Prepare(model, dense, wire.Tracks, options, folder, token))
                    {
                        // Source markers constrain curve preparation, but do not
                        // choose native payload storage. Pack the verified target
                        // copy under the SAME cumulative target-space limits.
                        // Its precise candidate is also the actual byte ceiling;
                        // the donor is never a compression/size reference.
                        NativeAnimationOptimizationResult packed = null;
                        string storageNote = "";
                        try
                        {
                            progress?.Invoke(95, "Checking native curve payloads against the finished target motion...");
                            var frozenPrecise = verified.Candidate ?? dense;
                            packed = NativeAnimationOptimization.PrepareCompact(model, dense, frozenPrecise, verified,
                                options, folder, (decoded, cancellation) => System.Threading.Tasks.Task.FromResult(
                                    QualityStorageCertificate(dense, frozenPrecise, decoded, proof, length, controls,
                                        attemptPairs, wire.BindOmissions)), token).GetAwaiter().GetResult();
                        }
                        catch (InvalidDataException error) when (error.Data.Contains("OptimizationQuality") ||
                            error.Data.Contains("OptimizationBudget") || error.Data.Contains("OptimizationSize"))
                        {
                            // A failed packing proposal cannot discard the
                            // already independently verified precise result.
                            storageNote = "Native payload search retained the verified precise candidate: " + error.Message + " ";
                        }
                        try
                        {
                        var heldBaseline = packed ?? verified;
                        string selectiveNote;
                        using (var selective = PrepareSelectiveQuality(model, dense, coarseWire, wire, coarseFailure,
                            heldBaseline, controls, generatedPairs, length, proof, nativeBudget, folder, token, progress,
                            out selectiveNote))
                        {
                        var final = selective?.Result ?? heldBaseline;
                        var finalWire = selective?.Wire ?? wire;
                        bool storageApproximation = selective?.StorageApproximation ?? (final.ContentKey != verified.ContentKey);
                        report.SourceSeedKeys = finalWire.SeedKeys;
                        var candidate = final.Candidate == null ? NativeAnimationOptimization.Copy(dense) : final.Candidate;
                        final.Candidate = null;
                        report.AddedQualityKeys = Math.Max(0, Count(candidate) - finalWire.SeedKeys);
                        report.ScalarCertified = report.QualityVerified = true; report.ScalarCertificateWork = proof.Work;
                        report.MaximumCertifiedScaleError = finalWire.Value[0]; report.MaximumCertifiedEulerError = finalWire.Value[1];
                        report.MaximumCertifiedTranslationError = finalWire.Value[2];
                        report.MaximumCertifiedScaleDerivativeError = finalWire.Derivative[0]; report.MaximumCertifiedEulerDerivativeError = finalWire.Derivative[1];
                        report.MaximumCertifiedTranslationDerivativeError = finalWire.Derivative[2];
                        if (storageApproximation)
                        {
                            // These are conservative certified ceilings for the
                            // cumulative decoded candidate, not the earlier
                            // precise candidate's now-stale measured maxima.
                            report.MaximumCertifiedScaleError = report.MaximumCertifiedScaleDerivativeError = .00001;
                            report.MaximumCertifiedEulerError = .01; report.MaximumCertifiedEulerDerivativeError = .05;
                            report.MaximumCertifiedTranslationError = report.MaximumCertifiedTranslationDerivativeError = Math.Min(.0002, .0001 * length);
                        }
                        report.RestoredTargetTracks = finalWire.RestoredTracks.ToArray(); report.RestoredTargetChannels = finalWire.RestoredChannels.ToArray();
                        report.OmittedTargetTracks = finalWire.BindOmissions;
                        report.DenseFallback = NativeAnimationOptimization.ContentKey(candidate) == report.DenseContent;
                        report.Samples=final.Samples;report.BoneVisits=nativeBudget.BoneVisits;report.VertexVisits=nativeBudget.VertexVisits;
                        report.MaximumEulerError=final.MaximumEulerError;report.MaximumOrientationError=final.MaximumOrientationError;
                        report.MaximumScaleError=final.MaximumScaleError;report.MaximumLocalTranslationError=final.MaximumLocalTranslationError;
                        report.MaximumWorldBoneError=final.MaximumWorldBoneError;report.MaximumSkinError=final.MaximumSkinError;
                        report.WorstFrame=final.WorstFrame;report.WorstBone=final.WorstBone;
                        report.QualityNotes = (report.QualityNotes ?? "") +
                            (preservePairs ? "The fifth bounded attempt retained complete original paired channels and accepted a conservative storage baseline before dense fallback; strict quality thresholds and the shared verification budget were unchanged. " : "") +
                            "Continuous raw Hermite value/derivative certificates and independent native cumulative checks passed. " +
                            (proof.Limited ? "Scalar proof budget was exhausted; uncertain spans retain their exact dense keys. " : "") +
                            "The world/basis distance combines native position and L-scaled forward-basis errors; the legacy separate MaximumWorldBasisError field is not measured or claimed in v2. " +
                            (storageApproximation ? "Certified scalar report fields are the unchanged cumulative strict ceilings; native maxima below are observed separately. " : "") +
                            storageNote + selectiveNote + "Native verification and actual-byte payload search: " + final.Report;
                        return candidate;
                        }
                        }
                        finally { packed?.Dispose(); }
                    }
                }
                catch (InvalidDataException error) when (error.Data.Contains("OptimizationQuality") || error.Data.Contains("OptimizationBudget") || error.Data.Contains("OptimizationSize"))
                {
                    if (ReferenceEquals(wire, coarseWire) && error.Data.Contains("OptimizationQuality")) coarseFailure = error;
                    report.QualityNotes = (report.QualityNotes ?? "") + (preservePairs ? "Conservative paired-channel baseline" : "Precision " + precision.ToString("G", CultureInfo.InvariantCulture)) + ": " + error.Message + "\r\n";
                    if (error.Data.Contains("OptimizationBudget")) break;
                }
            }
            report.DenseFallback = true; report.SourceSeedKeys = DenseSeedCount(dense,lastWire);
            report.AddedQualityKeys = Math.Max(0, Count(dense) - report.SourceSeedKeys);
            report.RestoredTargetTracks = lastWire?.RestoredTracks.ToArray() ?? new string[0];
            report.RestoredTargetChannels = lastWire?.RestoredChannels.ToArray() ?? new string[0];
            report.ScalarCertified = report.QualityVerified = true; report.ScalarCertificateWork = proof.Work;
            report.BoneVisits=nativeBudget.BoneVisits;report.VertexVisits=nativeBudget.VertexVisits;
            report.QualityNotes = (report.QualityNotes ?? "") + "The complete dense reference was retained after bounded candidates failed or the shared verification budget was exhausted. The legacy separate MaximumWorldBasisError field is not measured or claimed in v2.";
            return NativeAnimationOptimization.Copy(dense);
        }

        static int DenseSeedCount(CHR0Node dense,QualityWire wire)
        {
            if(wire==null)return 0;
            int count=0;
            foreach(var entry in dense.Children.OfType<CHR0EntryNode>())
                if(wire.SeedFrames.TryGetValue(entry.Name,out float[][] channels))
                    for(int c=0;c<9;c++)
                    {
                        var existing=entry.KeyArrays[c].Entries.GroupBy(k=>k._index).ToDictionary(g=>g.Key,g=>g.Count());
                        foreach(var group in channels[c].GroupBy(f=>f))
                            if(existing.TryGetValue(group.Key,out int sides))count=checked(count+Math.Min(sides,group.Count()));
                    }
            return count;
        }

        static NativeGeneratedPairSite[] FreezeQualityGeneratedPairs(RetargetResult result, MDL0Node model,
            HashSet<string> controls, CancellationToken token)
        {
            var evidence = result.GeneratedPairEvidence ?? new NativeGeneratedPairEvidence[0];
            if (evidence.Length == 0 || evidence.Length > 2000000 || string.IsNullOrEmpty(result.GeneratedPairRigKey) ||
                !string.Equals(result.GeneratedPairRigKey, NativeGeneratedPairRegistry.TryRigKey(model, token), StringComparison.Ordinal))
                return new NativeGeneratedPairSite[0];
            var claims = evidence.Where(e => e != null).GroupBy(e => (e.Bone, e.Channel, e.TimeBits))
                .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
            var sites = new List<NativeGeneratedPairSite>();
            foreach (var entry in result.Animation.Children.OfType<CHR0EntryNode>())
            {
                token.ThrowIfCancellationRequested();
                if (controls.Contains(entry.Name)) continue;
                for (int channel = 0; channel < 9; channel++)
                    foreach (var group in entry.KeyArrays[channel].Entries.GroupBy(k => k._index))
                    {
                        token.ThrowIfCancellationRequested();
                        var sides = group.ToArray();
                        // Ambiguous claims and mismatched context preserve the
                        // original sides, without quadratic rescans of curves.
                        if (sides.Length != 2 || !claims.TryGetValue((entry.Name, channel,
                            BitConverter.ToInt32(BitConverter.GetBytes(group.Key), 0)), out var known) ||
                            !known.Eligible || !known.SameContext(entry)) continue;
                        if (known.Matches(sides[0], sides[1])) sites.Add(known.Site());
                    }
            }
            return sites.ToArray();
        }

        static QualityWire QualityTracks(RetargetResult result, BrawlRig target, Dictionary<string, CHR0EntryNode> sourceTracks,
            RetargetKeyframeMode mode, HashSet<string> controls, NativeGeneratedPairSite[] generatedPairs, double precision, double length, QualityProofBudget proof)
        {
            var wire = new QualityWire(); var output = new List<ControlTrack>(); var omitted = new List<string>();
            bool interpolated=mode==RetargetKeyframeMode.Interpolated;
            var targetBones = target.Bones.ToDictionary(b => b.Name, StringComparer.Ordinal);
            var pairChannels = generatedPairs.GroupBy(p => (p.Bone, p.Channel)).ToDictionary(g => g.Key, g => new HashSet<float>(g.Select(p => p.Frame)));
            long planned = 0;
            foreach (var entry in result.Animation.Children.OfType<CHR0EntryNode>())
            {
                proof.Token.ThrowIfCancellationRequested();
                if (!targetBones.TryGetValue(entry.Name, out var bone)) continue;
                bool exact = controls.Contains(entry.Name);
                if (!exact && NativeAnimationOptimization.CanRemoveBindTrack(entry, bone)) { omitted.Add(entry.Name); continue; }
                var mapping = result.Plan.Mapping.SingleOrDefault(m => m.Target == entry.Name);
                CHR0EntryNode source = null;
                if (mapping != null) sourceTracks.TryGetValue(mapping.Source, out source);
                if (!interpolated && !exact && source == null) wire.RestoredTracks.Add(entry.Name);
                float[] union = interpolated || source == null ? new float[0] : source.KeyArrays.SelectMany(a => a.Entries.GroupBy(k => k._index)
                    .Select(g => new { Frame = g.Key, Count = g.Count() })).GroupBy(k => k.Frame).OrderBy(g => g.Key)
                    .SelectMany(g => Enumerable.Repeat(g.Key, g.Max(k => k.Count))).ToArray();
                var channels = new ControlKey[9][];var seedFrames=new float[9][];
                for (int channel = 0; channel < 9; channel++)
                {
                    float[] seeds = exact || interpolated || source == null ? new float[0] : mode == RetargetKeyframeMode.SourceBoneTimes ? union :
                        source.KeyArrays[channel].Entries.Select(k => k._index).ToArray();
                    if (!interpolated && !exact && seeds.Length == 0 && entry.KeyArrays[channel].KeyCount > 0)
                        wire.RestoredChannels.Add(entry.Name + " " + Channels[channel]);
                    if (!pairChannels.TryGetValue((entry.Name, channel), out var continuousPairs)) continuousPairs = new HashSet<float>();
                    channels[channel] = QualityCurve(entry.KeyArrays[channel], seeds, continuousPairs, channel, exact, precision, length, proof,
                        out float[] actualSeeds, out double value, out double derivative);
                    seedFrames[channel]=actualSeeds;
                    wire.SeedKeys = checked(wire.SeedKeys + actualSeeds.Length);
                    wire.Value[channel / 3] = Math.Max(wire.Value[channel / 3], value);
                    wire.Derivative[channel / 3] = Math.Max(wire.Derivative[channel / 3], derivative);
                    planned += channels[channel].Length;
                    if (planned > 2000000) throw new InvalidDataException("The source-seeded result exceeds the two-million-key operation limit.");
                }
                wire.SeedFrames.Add(entry.Name,seedFrames);
                output.Add(new ControlTrack { Bone = entry.Name, Channels = channels });
            }
            wire.Tracks = output.ToArray(); wire.BindOmissions = omitted.ToArray(); return wire;
        }

        static ControlKey CopyQualityKey(KeyframeEntry key) => new ControlKey { Frame = key._index, Value = key._value, Tangent = key._tangent };
        static bool PositiveZero(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0) == 0;
        static bool NegativeZero(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0) == int.MinValue;

        static ControlKey[] QualityCurve(KeyframeArray original, float[] sourceTimes, HashSet<float> continuousPairs, int channel, bool exact,
            double precision, double length, QualityProofBudget proof, out float[] seedFrames, out double valueError, out double derivativeError)
        {
            var keys = original.Entries.ToArray(); seedFrames = new float[0]; valueError = derivativeError = 0;
            if (keys.Length == 0) return new ControlKey[0];
            if (exact) { seedFrames = keys.Select(k=>k._index).ToArray(); return keys.Select(CopyQualityKey).ToArray(); }
            var sourceGroups = sourceTimes.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
            if (sourceGroups.Any(g => float.IsNaN(g.Key) || float.IsInfinity(g.Key) || g.Key < 0 || g.Value > 2)) throw new InvalidDataException("Source seed times or paired sides are invalid.");
            bool paired = keys.Select(k => k._index).Distinct().Count() != keys.Length || sourceGroups.Any(g => g.Value == 2);
            bool signedZero = keys.Any(k => NegativeZero(k._index) || NegativeZero(k._value) || NegativeZero(k._tangent));
            if (!paired && !signedZero && keys.All(k => SameBits(k._value, keys[0]._value) && PositiveZero(k._tangent)))
            { seedFrames = new[]{0f}; return new[] { new ControlKey { Frame = 0, Value = keys[0]._value, Tangent = 0 } }; }
            // A proven fixed master may already contain just its first key.
            // Source-bone timing can still request paired seeds from another
            // channel. Materialize the identical fixed function at those exact
            // sides; never invent a derivative or discard the paired contract.
            if(keys.Length==1&&!signedZero&&PositiveZero(keys[0]._tangent))
            {
                var seeds=new List<float>(sourceTimes);if(!seeds.Contains(0))seeds.Add(0);
                seedFrames=seeds.OrderBy(t=>t).ToArray();
                return seedFrames.Select(t=>new ControlKey{Frame=t,Value=keys[0]._value,Tangent=0}).ToArray();
            }
            var byTime = keys.GroupBy(k => k._index).ToDictionary(g => g.Key, g => g.ToArray());
            foreach (float time in sourceGroups.Keys)
                if (!byTime.ContainsKey(time)) throw new InvalidDataException("The immutable dense reference lacks a source seed knot at " + time + ".");
            // Keep the established conservative contract for an authored or
            // unknown pair anywhere on this channel, and for signed zeros.
            // Only wholly understood generated channels enter grouped proof.
            if (signedZero || byTime.Any(g => g.Value.Length > 1 && !continuousPairs.Contains(g.Key)))
            {
                var kept = new List<ControlKey>();
                foreach (var group in keys.GroupBy(k => k._index))
                {
                    var sides = group.Select(CopyQualityKey).ToArray(); kept.AddRange(sides);
                    if (sourceGroups.TryGetValue(group.Key, out int count) && count == 2 && sides.Length == 1)
                        kept.Add(CopyQualityKey(group.First()));
                }
                seedFrames = kept.Select(k => k.Frame).ToArray(); return kept.ToArray();
            }
            // A time group owns its incoming first and outgoing last side.
            // Unknown pairs, jumps and IEEE signed zeros were kept above;
            // known continuous generated pairs may disappear only as a whole.
            var groups = keys.Select((key, index) => new { key, index }).GroupBy(k => k.key._index)
                .Select(g => new { Time = g.Key, First = g.First().index, Last = g.Last().index }).ToArray();
            var retained = new SortedSet<int> { 0, groups.Length - 1 };
            for (int i = 0; i < groups.Length; i++)
            {
                var group = groups[i];
                if (sourceGroups.ContainsKey(group.Time) || group.First != group.Last && !continuousPairs.Contains(group.Time) ||
                    Enumerable.Range(group.First, group.Last - group.First + 1).Any(k => NegativeZero(keys[k]._index) || NegativeZero(keys[k]._value) || NegativeZero(keys[k]._tangent)))
                    retained.Add(i);
            }
            ControlKey[] GroupKeys(int index)
            {
                var group = groups[index];
                var sides = Enumerable.Range(group.First, group.Last - group.First + 1).Select(i => CopyQualityKey(keys[i])).ToList();
                if (sourceGroups.TryGetValue(group.Time, out int count) && count == 2 && sides.Count == 1) sides.Add(CopyQualityKey(keys[group.First]));
                return sides.ToArray();
            }
            seedFrames = retained.SelectMany(GroupKeys).Select(k => k.Frame).ToArray();
            double limit = (channel < 3 ? .00001 : channel < 6 ? .01 : Math.Min(.0002, .0001 * length)) * precision;
            double speed = (channel >= 3 && channel < 6 ? .05 : channel < 3 ? .00001 : Math.Min(.0002, .0001 * length)) * precision;
            bool scaleExact = channel < 3 && !QualitySignedScale(keys, proof);
            var pending = new Stack<Tuple<int, int>>(); var initial = retained.ToArray();
            for (int i = 1; i < initial.Length; i++) pending.Push(Tuple.Create(initial[i - 1], initial[i]));
            while (pending.Count > 0)
            {
                var span = pending.Pop();
                if (span.Item2 <= span.Item1 + 1) continue;
                if (QualitySpan(keys, groups[span.Item1].Last, groups[span.Item2].First, continuousPairs, scaleExact ? 0 : limit, scaleExact ? 0 : speed, channel < 3, proof,
                    out double value, out double derivative))
                { valueError = Math.Max(valueError, value); derivativeError = Math.Max(derivativeError, derivative); continue; }
                int middle = (span.Item1 + span.Item2) / 2; retained.Add(middle);
                pending.Push(Tuple.Create(span.Item1, middle)); pending.Push(Tuple.Create(middle, span.Item2));
            }
            return retained.SelectMany(GroupKeys).ToArray();
        }
    }
}
