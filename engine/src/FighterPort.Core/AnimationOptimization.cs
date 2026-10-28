using System.Numerics;

namespace FighterPort.Core;

public enum AnimationOptimizationMode { Exact, HighQuality, SmallFile }

// Raw native XYZ Euler/SRT bind channels, supplied by the owning adapter. A
// quaternion-derived rest pose cannot identify authored full Euler turns.
public sealed record AnimationOptimizationBindPose(string Bone, float[] Channels);

public sealed record AnimationOptimizationChannelRule(string Bone, int Channel = -1)
{
    public bool ExactOnly { get; init; }
    public bool PreserveStructure { get; init; }
    public double[] MandatoryFrames { get; init; } = [];
    public ProtectedInterval[] ProtectedRanges { get; init; } = [];
}

public sealed record AnimationOptimizationPairSite(string Bone, int Channel, double Frame);

public sealed record AnimationOptimizationSettings
{
    public AnimationOptimizationMode Mode { get; init; } = AnimationOptimizationMode.Exact;
    // Explicit offline permission. Existing callers retain original tangents;
    // native export enables this only for a reviewed Error-bounded operation.
    public bool EnableSpanRefitting { get; init; }
    public double BindBoundsDiagonal { get; init; }
    double? scaleValueBound, scaleSpeedBound, eulerValueBound, eulerSpeedBound, translationCap, translationSpeedCap;
    public double ScaleValueBound { get => scaleValueBound ?? (Mode == AnimationOptimizationMode.SmallFile ? .0001 : 1e-5); init => scaleValueBound = value; }
    public double ScaleSpeedBoundPerFrame { get => scaleSpeedBound ?? (Mode == AnimationOptimizationMode.SmallFile ? .0001 : 1e-5); init => scaleSpeedBound = value; }
    public double EulerValueBoundDegrees { get => eulerValueBound ?? (Mode == AnimationOptimizationMode.SmallFile ? .1 : .01); init => eulerValueBound = value; }
    public double EulerSpeedBoundDegreesPerFrame { get => eulerSpeedBound ?? (Mode == AnimationOptimizationMode.SmallFile ? .1 : .05); init => eulerSpeedBound = value; }
    public double TranslationValueFraction { get; init; } = 1e-4;
    public double TranslationSpeedFractionPerFrame { get; init; } = 1e-4;
    public double TranslationAbsoluteCap { get => translationCap ?? (Mode == AnimationOptimizationMode.SmallFile ? .002 : .0002); init => translationCap = value; }
    public double TranslationSpeedAbsoluteCapPerFrame { get => translationSpeedCap ?? (Mode == AnimationOptimizationMode.SmallFile ? .002 : .0002); init => translationSpeedCap = value; }
    public string[] ExactOnlyBones { get; init; } = [];
    public string[] PreserveStructureBones { get; init; } = [];
    public double[] MandatoryFrames { get; init; } = [];
    // These are fitting joins, not authored pins or arbitrary mandatory points.
    // SmallFile keeps authored knot times/sides but permits bounded numeric
    // changes. Explicit MandatoryFrames and protected ranges remain exact.
    public double[] FitJoinFrames { get; init; } = [];
    // Explicit operation-owned provenance. Empty for imported/authored curves.
    public double[] GeneratedPairFrames { get; init; } = [];
    public AnimationOptimizationPairSite[] GeneratedPairSites { get; init; } = [];
    public ProtectedInterval[] ProtectedRanges { get; init; } = [];
    public AnimationOptimizationChannelRule[] ChannelRules { get; init; } = [];
    public AnimationOptimizationBindPose[] BindPoses { get; init; } = [];
    public long MaximumWork { get; init; } = 2_000_000;
    public int MaximumCandidateSpans { get; init; } = 100_000;
    public int MaximumProbeFrames { get; init; } = 200_000;
    public int MaximumSubdivisionDepth { get; init; } = 16;
}

public sealed record AnimationOptimizationChannelReport(string Bone, int Channel, int BeforeKeys, int AfterKeys,
    string Policy, int CandidateSpans, int AcceptedMerges, int PolynomialIntervals,
    double CertifiedValueErrorBound, double CertifiedSpeedErrorBound, double ValueBound, double SpeedBound,
    string? RetainedReason)
{
    public int RefitCandidateSpans { get; init; }
    public int AcceptedRefitSpans { get; init; }
    public int RefitTangents { get; init; }
}

public sealed record AnimationOptimizationReport(string Version, AnimationOptimizationMode Mode,
    int InputTracks, int OutputTracks, long InputKeys, long OutputKeys, long RemovedUnmatchedKeys,
    int CandidateSpans, int AcceptedMerges, long Work, bool WorkLimitReached, bool CandidateLimitReached,
    bool ProbeLimitReached, double[] ProbeFrames, AnimationOptimizationChannelReport[] Channels)
{
    public int RefitCandidateSpans { get; init; }
    public int AcceptedRefitSpans { get; init; }
    public int RefitTangents { get; init; }
    public bool RefitWorkLimited { get; init; }
    public bool LegacyPhaseLimited { get; init; }
    public long RemovedKeys => InputKeys - OutputKeys;
    public long ReducedMatchedKeys => RemovedKeys - RemovedUnmatchedKeys;
    public string Claim => "Continuous rational scalar polynomial/value-speed bounds only; floating evaluation roundoff, native encode/reopen, cumulative orientation/world/skin/contact quality and actual file size require independent caller validation.";
}

public sealed record AnimationOptimizationResult(BoneTrack[] Tracks, string[] RemovedUnmatchedTracks,
    AnimationOptimizationReport Report)
{
    public string[] RemovedBindTracks { get; init; } = [];
    // This independent candidate retains the deletion-only search result, so
    // native byte/geometry validation can prefer it over a larger refitted copy.
    public BoneTrack[]? PreRefitTracks { get; init; }
}

// Offline representation reduction, never a pose or contact generator.
// Explicit bounded refitting can propose native scalar tangents. Every new
// span is compared to the immutable input, not to a previously reduced curve.
public static partial class AnimationOptimization
{
    public const string Version = "native-hermite-rational-span-refit-2";
    public const int MaximumInputKeys = 2_000_000;

    public static AnimationOptimizationResult Optimize(AnimationClip baseline,
        IReadOnlyCollection<string> targetBoneNames, AnimationOptimizationSettings? settings = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(targetBoneNames);
        settings ??= new();
        ValidateSettings(settings);
        cancellation.ThrowIfCancellationRequested();
        baseline.Rate.Validate();
        if (baseline.SampleCount < 1 || baseline.Tracks.Select(t => t.Bone).Distinct(StringComparer.Ordinal).Count() != baseline.Tracks.Length)
            throw new ArgumentException("Optimization needs positive timing and unique exact native track identities.");
        var targets = new HashSet<string>(targetBoneNames, StringComparer.Ordinal);
        if (targets.Count != targetBoneNames.Count || targets.Any(string.IsNullOrEmpty))
            throw new ArgumentException("Optimization needs unique nonempty exact target bone identities.", nameof(targetBoneNames));
        if (settings.BindPoses.Any(bind => !targets.Contains(bind.Bone)))
            throw new ArgumentException("Bind cleanup data must belong to the exact target skeleton.", nameof(settings));
        var generatedSites = settings.GeneratedPairSites.GroupBy(site => (site.Bone, site.Channel))
            .ToDictionary(group => group.Key, group => group.Select(site => site.Frame).Distinct().ToArray());
        long inputKeys = 0;
        foreach (var track in baseline.Tracks)
        {
            cancellation.ThrowIfCancellationRequested();
            if (track.Channels.Length != 9) throw new ArgumentException("Native optimization needs nine sparse transform channels.");
            foreach (var curve in track.Channels.OfType<Curve>())
            {
                inputKeys += curve.Keys.LongLength;
                if (inputKeys > MaximumInputKeys) throw new NotSupportedException("Optimization exceeds its two-million input-key resource bound.");
                ValidateCurve(curve);
            }
            inputKeys += track.Rotation?.Keys.LongLength ?? 0;
            if (inputKeys > MaximumInputKeys) throw new NotSupportedException("Optimization exceeds its two-million input-key resource bound.");
        }
        var removed = baseline.Tracks.Where(t => !targets.Contains(t.Bone)).Select(t => t.Bone).ToArray();
        long unmatchedKeys = baseline.Tracks.Where(t => !targets.Contains(t.Bone)).Sum(CountKeys);
        var tracks = baseline.Tracks.Where(t => targets.Contains(t.Bone)).ToArray();
        var budget = new Budget(settings, cancellation);
        var exactBones = new HashSet<string>(settings.ExactOnlyBones, StringComparer.Ordinal);
        var protectedBones = new HashSet<string>(settings.PreserveStructureBones, StringComparer.Ordinal);
        var states = new List<ChannelState>();
        foreach (var track in tracks.OrderBy(t => t.Bone, StringComparer.Ordinal))
        {
            cancellation.ThrowIfCancellationRequested();
            bool singular = settings.Mode != AnimationOptimizationMode.Exact && track.Channels.Take(3).OfType<Curve>().Any(c => MayHaveSingularScale(c, cancellation));
            for (int channel = 0; channel < 9; channel++)
            {
                if (track.Channels[channel] is not { } curve) continue;
                var rules = settings.ChannelRules.Where(r => r.Bone == track.Bone && (r.Channel < 0 || r.Channel == channel)).ToArray();
                bool exact = settings.Mode == AnimationOptimizationMode.Exact || exactBones.Contains(track.Bone) || singular || rules.Any(r => r.ExactOnly);
                bool preserve = protectedBones.Contains(track.Bone) || rules.Any(r => r.PreserveStructure);
                var mandatory = ExactFrames(settings,rules).Concat(settings.FitJoinFrames).Distinct().Order().ToArray();
                var ranges = settings.ProtectedRanges.Concat(rules.SelectMany(r => r.ProtectedRanges)).ToArray();
                double valueBound = exact ? 0 : channel < 3 ? settings.ScaleValueBound : channel < 6 ? settings.EulerValueBoundDegrees
                    : TranslationBound(settings,false);
                double speedBound = exact ? 0 : channel < 3 ? settings.ScaleSpeedBoundPerFrame : channel < 6 ? settings.EulerSpeedBoundDegreesPerFrame
                    : TranslationBound(settings,true);
                if (!exact && (valueBound <= 0 || speedBound <= 0))
                    throw new ArgumentException("The declared authentic bind scale is too small to represent a positive translation tolerance.");
                var state = new ChannelState(track.Bone, channel, curve, exact, valueBound, speedBound, mandatory, ranges,
                    settings.Mode == AnimationOptimizationMode.SmallFile && !exact ? settings.GeneratedPairFrames.Concat(
                        generatedSites.GetValueOrDefault((track.Bone, channel), [])).Distinct().ToArray() : []);
                if (preserve) state.Reason = "Protected native curve structure.";
                else if (track.Rotation is not null) state.Reason = "Quaternion-owned track retained without native conversion.";
                else if (curve.Interpolation != Interpolation.Hermite) state.Reason = "Non-Hermite interpolation retained without conversion.";
                else if (curve.Keys.Any(k => k.Frame < 0 || k.Frame > int.MaxValue || (double)(float)k.Frame != k.Frame)) state.Reason = "Out-of-range or non-native knot times retained without rounding.";
                else if (curve.Keys.Length < 2 && curve.Keys[0].Frame == 0) state.Reason = "Single native key retained.";
                states.Add(state);
            }
        }

        bool refit = settings.EnableSpanRefitting && settings.Mode != AnimationOptimizationMode.Exact
            && states.Any(s => s.Reason is null && !s.Exact);
        var queue = new PriorityQueue<Candidate, Priority>();
        bool legacyPhaseLimited = false;
        if (refit) budget.StartPhase(Math.Max(1, settings.MaximumWork * 3 / 5), Math.Max(1, settings.MaximumCandidateSpans * 3 / 5));
        try
        {
            foreach (var state in states.Where(s => s.Reason is null))
            {
                budget.Check();
                // A native fixed channel has no timed tangent. Only the exact
                // +zero-slope constant identity permits this representation.
                if (state.CanCollapseConstant())
                {
                    budget.Charge(state.Keys.Length);
                    state.CollapseConstant();
                    budget.Merges += state.Keys.Length - 1;
                    continue;
                }
                for (int i = 1; i < state.Keys.Length - 1; i++) Enqueue(state, i);
            }
            while (queue.TryDequeue(out var candidate, out _))
            {
                budget.Check();
                var state = candidate.State;
                if (!state.Alive[candidate.Middle] || state.Previous[candidate.Middle] != candidate.Left || state.Next[candidate.Middle] != candidate.Right) continue;
                budget.Charge(1);
                state.Remove(candidate.Middle, candidate.ValueError, candidate.SpeedError);
                budget.Merges++;
                Enqueue(state, candidate.Left);
                Enqueue(state, candidate.Right);
            }
        }
        catch (PhaseLimitReachedException) { legacyPhaseLimited = true; }
        catch (LimitReachedException) { }
        finally { budget.EndPhase(); }

        var byChannel = states.ToDictionary(s => (s.Bone, s.Channel));
        BoneTrack[] Materialize() => tracks.Select(t =>
        {
            bool changed = false;
            var channels = t.Channels.Select((curve, channel) =>
            {
                if (curve is null) return null;
                var next = byChannel[(t.Bone, channel)].Output();
                changed |= !ReferenceEquals(curve, next);
                return next;
            }).ToArray();
            return changed ? t with { Channels = channels } : t;
        }).ToArray();
        BoneTrack[]? beforeRefit = refit ? Materialize() : null;
        bool refitWorkLimited = refit && (budget.WorkLimited || budget.CandidateLimited);
        if (refit && !budget.WorkLimited && !budget.CandidateLimited)
        {
            // Generation and the caller's decoded-codec certificates share one
            // operation allowance. Leave its final fifth for those certificates;
            // this phase limit grants neither more work nor looser error bounds.
            budget.StartPhase(Math.Max(1, settings.MaximumWork * 4 / 5), settings.MaximumCandidateSpans);
            try { RefitSpans(states, budget); }
            catch (PhaseLimitReachedException) { refitWorkLimited = true; }
            catch (LimitReachedException) { refitWorkLimited = true; }
            finally { budget.EndPhase(); }
        }
        tracks = Materialize();
        // The native adapter permits only tracks whose resource flags also
        // allow removal. All authored channels must be exactly bind-constant;
        // this never removes a model bone or guesses a role from its name.
        var bindPoses = settings.BindPoses.ToDictionary(b => b.Bone, StringComparer.Ordinal);
        var originalTracks = baseline.Tracks.ToDictionary(t => t.Bone, StringComparer.Ordinal);
        var removedBind = tracks.Where(t => t.Rotation is null && bindPoses.TryGetValue(t.Bone, out var bind)
            && !protectedBones.Contains(t.Bone) && settings.ProtectedRanges.Length == 0
            && !settings.ChannelRules.Any(r => r.Bone == t.Bone && (r.PreserveStructure || r.ProtectedRanges.Length != 0))
            && originalTracks[t.Bone].Channels.Select((curve, channel) => curve is null || IsBindConstant(curve, bind.Channels[channel])).All(v => v))
            .Select(t => t.Bone).ToArray();
        var removedBindSet = removedBind.ToHashSet(StringComparer.Ordinal);
        tracks = tracks.Where(t => !removedBindSet.Contains(t.Bone)).ToArray();
        // Both independent alternatives contribute extrema/near-key witnesses;
        // the caller must not reuse a refitted-only probe set for the legacy copy.
        var probes = BuildProbes(baseline, beforeRefit is null ? tracks : tracks.Concat(beforeRefit).ToArray(), settings, cancellation, out bool probeLimited);
        cancellation.ThrowIfCancellationRequested();
        if (beforeRefit is not null)
            beforeRefit = beforeRefit.Where(t => !removedBindSet.Contains(t.Bone)).ToArray();
        return new(tracks, removed, new(Version, settings.Mode, baseline.Tracks.Length, tracks.Length,
            inputKeys, tracks.Sum(CountKeys), unmatchedKeys, budget.Candidates, budget.Merges, budget.Work,
            budget.WorkLimited, budget.CandidateLimited, probeLimited, probes, states.Select(s => s.Report()).ToArray())
            {
                RefitCandidateSpans = states.Sum(s => s.RefitCandidates), AcceptedRefitSpans = states.Sum(s => s.RefitSpans),
                RefitTangents = states.Sum(s => s.RefitTangents), RefitWorkLimited = refitWorkLimited, LegacyPhaseLimited = legacyPhaseLimited
            })
            { RemovedBindTracks = removedBind, PreRefitTracks = states.Any(s => s.RefitSpans > 0) ? beforeRefit : null };

        void Enqueue(ChannelState state, int middle)
        {
            budget.Check();
            if (middle <= 0 || middle >= state.Keys.Length - 1 || !state.Alive[middle] || state.Mandatory[middle]) return;
            int left = state.Previous[middle], right = state.Next[middle];
            if (left < 0 || right < 0 || !state.AllowsSpan(left, right)) return;
            budget.BeginCandidate();
            state.Candidates++;
            if (!TestSpan(state, left, right, budget, out var value, out var speed)) return;
            double score = state.Exact ? 0 : Math.Max(value / state.ValueBound, speed / state.SpeedBound);
            queue.Enqueue(new(state, middle, left, right, value, speed), new(score, state.Bone, state.Channel, state.Keys[middle].Frame));
        }
    }

    static long CountKeys(BoneTrack track) => track.Channels.OfType<Curve>().Sum(c => c.Keys.LongLength) + (track.Rotation?.Keys.LongLength ?? 0);

    static void ValidateSettings(AnimationOptimizationSettings settings)
    {
        if (!Enum.IsDefined(settings.Mode) || settings.MaximumWork < 0 || settings.MaximumCandidateSpans < 0 || settings.MaximumProbeFrames < 1
            || settings.MaximumSubdivisionDepth < 0 || settings.MaximumSubdivisionDepth > 32)
            throw new ArgumentException("Optimization needs finite nonnegative work limits and subdivision depth 0..32.");
        if (settings.Mode != AnimationOptimizationMode.Exact && (!double.IsFinite(settings.BindBoundsDiagonal) || settings.BindBoundsDiagonal <= 0))
            throw new ArgumentException("Approximate optimization needs a positive finite authentic bind-bounds diagonal.");
        double[] positive = [settings.ScaleValueBound, settings.ScaleSpeedBoundPerFrame, settings.EulerValueBoundDegrees,
            settings.EulerSpeedBoundDegreesPerFrame, settings.TranslationValueFraction, settings.TranslationSpeedFractionPerFrame,
            settings.TranslationAbsoluteCap, settings.TranslationSpeedAbsoluteCapPerFrame];
        if (positive.Any(v => !double.IsFinite(v) || v <= 0)) throw new ArgumentException("Optimization tolerances must be positive finite declared bounds.");
        if(settings.Mode==AnimationOptimizationMode.SmallFile && (settings.EulerValueBoundDegrees>.1 || settings.EulerSpeedBoundDegreesPerFrame>.1 ||
            settings.ScaleValueBound>.0001 || settings.ScaleSpeedBoundPerFrame>.0001 || settings.TranslationAbsoluteCap>.002 ||
            settings.TranslationSpeedAbsoluteCapPerFrame>.002 || settings.MaximumWork>2_000_000 || settings.MaximumCandidateSpans>100_000))
            throw new ArgumentException("Small-file construction cannot exceed its frozen scalar ceilings or existing operation budgets.");
        void Frames(double[] frames) { if (frames.Any(f => !double.IsFinite(f) || f < 0)) throw new ArgumentException("Protected frames must be finite and nonnegative."); }
        void Ranges(ProtectedInterval[] ranges)
        {
            if (ranges.Any(r => !double.IsFinite(r.Start) || !double.IsFinite(r.End) || r.Start < 0 || r.End < r.Start))
                throw new ArgumentException("Protected ranges must be finite ordered intervals.");
        }
        Frames(settings.MandatoryFrames); Frames(settings.FitJoinFrames); Frames(settings.GeneratedPairFrames); Ranges(settings.ProtectedRanges);
        if (settings.GeneratedPairSites.Any(site => string.IsNullOrEmpty(site.Bone) || site.Channel < 0 || site.Channel > 8 || !double.IsFinite(site.Frame) || site.Frame < 0))
            throw new ArgumentException("Generated pair provenance requires exact bone/channel identities and finite frame times.");
        foreach (var rule in settings.ChannelRules)
        {
            if (string.IsNullOrEmpty(rule.Bone) || rule.Channel < -1 || rule.Channel > 8) throw new ArgumentException("A channel rule needs an exact bone identity and channel -1..8.");
            Frames(rule.MandatoryFrames); Ranges(rule.ProtectedRanges);
        }
        if (settings.BindPoses.Any(b => string.IsNullOrEmpty(b.Bone) || b.Channels.Length != 9 || b.Channels.Any(v => !float.IsFinite(v)))
            || settings.BindPoses.Select(b => b.Bone).Distinct(StringComparer.Ordinal).Count() != settings.BindPoses.Length)
            throw new ArgumentException("Bind cleanup needs unique exact bone identities and nine finite raw native bind channels.");
    }

    static double[] ExactFrames(AnimationOptimizationSettings settings,AnimationOptimizationChannelRule[] rules) =>
        settings.MandatoryFrames.Concat(settings.Mode==AnimationOptimizationMode.SmallFile ? [] : settings.FitJoinFrames)
            .Concat(rules.SelectMany(r=>r.MandatoryFrames)).Distinct().ToArray();

    static double TranslationBound(AnimationOptimizationSettings settings,bool derivative) => settings.Mode==AnimationOptimizationMode.SmallFile
        ? derivative ? settings.TranslationSpeedAbsoluteCapPerFrame : settings.TranslationAbsoluteCap
        : derivative ? Math.Min(settings.TranslationSpeedAbsoluteCapPerFrame,settings.TranslationSpeedFractionPerFrame*settings.BindBoundsDiagonal)
            : Math.Min(settings.TranslationAbsoluteCap,settings.TranslationValueFraction*settings.BindBoundsDiagonal);

    static void ValidateCurve(Curve curve)
    {
        if (curve.Keys.Length == 0 || curve.LoopFrameLimit is <= 0) throw new ArgumentException("Optimization needs nonempty native curves and positive loop limits.");
        for (int i = 0; i < curve.Keys.Length; i++)
        {
            var k = curve.Keys[i];
            if (!double.IsFinite(k.Frame) || !float.IsFinite(k.Value) || !float.IsFinite(k.InTangent) || !float.IsFinite(k.OutTangent)
                || i > 0 && k.Frame < curve.Keys[i - 1].Frame)
                throw new ArgumentException("Native knots must be ordered finite times, values and tangents.");
        }
    }

    sealed class ChannelState
    {
        public readonly string Bone;
        public readonly int Channel;
        public readonly Curve Original;
        public readonly Key[] Keys;
        public readonly bool Exact;
        public readonly double ValueBound, SpeedBound;
        public readonly bool[] Alive, Mandatory;
        public readonly int[] Previous, Next;
        public readonly double[] Frames;
        public readonly ProtectedInterval[] Ranges;
        public readonly HashSet<double> GeneratedPairs;
        public int Candidates, Merges, Intervals;
        public int RefitCandidates, RefitSpans;
        public int RefitTangents => Enumerable.Range(0, Keys.Length).Where(i => Alive[i] && RefitChanged[i])
            .Sum(i => (Bits(Keys[i].InTangent) == Bits(Original.Keys[i].InTangent) ? 0 : 1)
                + (Bits(Keys[i].OutTangent) == Bits(Original.Keys[i].OutTangent) ? 0 : 1));
        public readonly bool[] RefitChanged;
        public double ValueError, SpeedError;
        public string? Reason;
        bool constant;
        public ChannelState(string bone, int channel, Curve curve, bool exact, double valueBound, double speedBound, double[] frames, ProtectedInterval[] ranges, double[] generatedPairs)
        {
            Bone = bone; Channel = channel; Original = curve; Keys = curve.Keys.ToArray(); Exact = exact; ValueBound = valueBound; SpeedBound = speedBound;
            RefitChanged = new bool[Keys.Length];
            Frames = frames; Ranges = ranges;
            GeneratedPairs = generatedPairs.ToHashSet();
            var protectedPairs = Keys.GroupBy(k => k.Frame).Where(g => g.Count() > 1 &&
                (!GeneratedPairs.Contains(g.Key) || g.Any(HasSignedZero) || g.Select(k => Bits(k.Value)).Distinct().Count() > 1)).Select(g => g.Key).ToHashSet();
            Alive = Enumerable.Repeat(true, Keys.Length).ToArray();
            Previous = Enumerable.Range(-1, Keys.Length).ToArray();
            Next = Enumerable.Range(1, Keys.Length).ToArray(); Next[^1] = -1;
            Mandatory = Keys.Select((k, i) => i == 0 || i == Keys.Length - 1 || HasSignedZero(k)
                || Bits(k.InTangent) != Bits(k.OutTangent)
                || protectedPairs.Contains(k.Frame)
                || frames.Contains(k.Frame) || ranges.Any(r => k.Frame >= r.Start && k.Frame <= r.End)).ToArray();
        }
        public bool CanCollapseConstant()
        {
            // A proved constant has no authored timing dependency. Native fixed
            // storage represents it at frame zero, including shifted constants.
            // Mandatory fitting joins protect the exact value and slope; this
            // identity preserves those at every time without redundant knots.
            return Ranges.Length == 0 && IsExactConstant(Original);
        }
        public void CollapseConstant()
        {
            constant = true; Merges = Keys.Length - 1;
            for (int i = 1; i < Alive.Length; i++) Alive[i] = false;
        }
        public bool AllowsSpan(int left, int right)
        {
            double a = Keys[left].Frame, b = Keys[right].Frame;
            return b > a && !Frames.Any(f => f > a && f < b) && !Ranges.Any(r =>
                Math.Max(a, r.Start) < Math.Min(b, r.End) || r.Start == r.End && r.Start > a && r.Start < b);
        }
        public void Remove(int middle, double value, double speed)
        {
            int left = Previous[middle], right = Next[middle];
            Alive[middle] = false; Next[left] = right; Previous[right] = left; Merges++;
            ValueError = Math.Max(ValueError, value); SpeedError = Math.Max(SpeedError, speed);
        }
        public Curve Output() => !constant && Merges == 0 && RefitSpans == 0 ? Original : Original with { Keys = constant
            ? [Keys[0].Frame == 0 ? Keys[0] : Keys[0] with { Frame = 0 }]
            : Keys.SelectMany((key, i) => !Alive[i] ? [] : RefitChanged[i] && Bits(key.InTangent) != Bits(key.OutTangent)
                ? new[] { key with { OutTangent = key.InTangent }, key with { InTangent = key.OutTangent } } : new[] { key }).ToArray() };
        public AnimationOptimizationChannelReport Report() => new(Bone, Channel, Original.Keys.Length, Output().Keys.Length,
            Exact ? "Exact rational polynomial identity" : "Rational Bernstein value/speed bounds", Candidates, Merges, Intervals,
            ValueError, SpeedError, ValueBound, SpeedBound, Reason)
            { RefitCandidateSpans = RefitCandidates, AcceptedRefitSpans = RefitSpans, RefitTangents = RefitTangents };
    }

    static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
    static bool HasSignedZero(Key key) => BitConverter.DoubleToInt64Bits(key.Frame) == long.MinValue ||
        Bits(key.Value) == int.MinValue || Bits(key.InTangent) == int.MinValue || Bits(key.OutTangent) == int.MinValue;
    static bool IsExactConstant(Curve curve) => curve.Interpolation == Interpolation.Hermite
        && !curve.Keys.Any(HasSignedZero)
        && curve.Keys.All(k => Bits(k.Value) == Bits(curve.Keys[0].Value) && Bits(k.InTangent) == 0 && Bits(k.OutTangent) == 0);
    static bool IsExactFixedRepresentation(Curve original, Curve candidate) => IsExactConstant(original)
        && candidate.Keys.Length == 1 && candidate.Keys[0].Frame == 0
        && Bits(candidate.Keys[0].Value) == Bits(original.Keys[0].Value)
        && Bits(candidate.Keys[0].InTangent) == 0 && Bits(candidate.Keys[0].OutTangent) == 0;
    static bool IsBindConstant(Curve curve, float bind) => curve.Interpolation == Interpolation.Hermite
        && !curve.Keys.Any(HasSignedZero) && Bits(bind) != int.MinValue
        && curve.Keys.All(k => Bits(k.Value) == Bits(bind) && Bits(k.InTangent) == 0 && Bits(k.OutTangent) == 0);
    sealed record Candidate(ChannelState State, int Middle, int Left, int Right, double ValueError, double SpeedError);
    readonly record struct Priority(double Score, string Bone, int Channel, double Frame) : IComparable<Priority>
    {
        public int CompareTo(Priority other)
        {
            int c = Score.CompareTo(other.Score); if (c != 0) return c;
            c = StringComparer.Ordinal.Compare(Bone, other.Bone); if (c != 0) return c;
            c = Channel.CompareTo(other.Channel); return c != 0 ? c : Frame.CompareTo(other.Frame);
        }
    }
    sealed class LimitReachedException : Exception;
    sealed class PhaseLimitReachedException : Exception;
    sealed class Budget(AnimationOptimizationSettings settings, CancellationToken cancellation)
    {
        public long Work;
        public int Candidates, Merges;
        public bool WorkLimited, CandidateLimited;
        long? phaseWork;
        int? phaseCandidates;
        public int Depth => settings.MaximumSubdivisionDepth;
        public void Check() => cancellation.ThrowIfCancellationRequested();
        public void StartPhase(long work, int candidates) { phaseWork = work; phaseCandidates = candidates; }
        public void EndPhase() { phaseWork = null; phaseCandidates = null; }
        public void Charge(long amount)
        {
            Check();
            if (amount > settings.MaximumWork - Work) { WorkLimited = true; throw new LimitReachedException(); }
            if (phaseWork is { } limit && amount > limit - Work) throw new PhaseLimitReachedException();
            Work += amount;
        }
        public void BeginCandidate()
        {
            Check();
            if (Candidates >= settings.MaximumCandidateSpans) { CandidateLimited = true; throw new LimitReachedException(); }
            if (phaseCandidates is { } limit && Candidates >= limit) throw new PhaseLimitReachedException();
            Charge(1); Candidates++;
        }
    }

    static bool TestSpan(ChannelState state, int left, int right, Budget budget, out double valueError, out double speedError)
    {
        valueError = speedError = 0;
        return TestSpan(state, left, right, state.Keys[left], state.Keys[right], budget, out valueError, out speedError);
    }

    static bool TestSpan(ChannelState state, int left, int right, Key a, Key b, Budget budget, out double valueError, out double speedError)
    {
        valueError = speedError = 0;
        if (!state.Exact && state.Channel < 3)
        {
            // Small absolute scale error does not preserve nonsingularity.
            // Retained endpoint slopes can cross zero over a longer span even
            // when every original piece has a strictly signed Bernstein hull.
            // Uncertain hulls retain their keys; exact polynomial identities
            // continue through the existing exact-only comparison below.
            budget.Charge(4);
            var scale = Bernstein(a, b, a.Frame, b.Frame);
            if (!scale.All(v => v > Rational.Zero) && !scale.All(v => v < Rational.Zero)) return false;
        }
        for (int i = left; i < right; i++)
        {
            budget.Charge(1); state.Intervals++;
            var p = state.Original.Keys[i]; var q = state.Original.Keys[i + 1];
            if (p.Frame == q.Frame)
            {
                if (!state.GeneratedPairs.Contains(p.Frame) || Bits(p.Value) != Bits(q.Value) || HasSignedZero(p) || HasSignedZero(q)) return false;
                continue; // Zero-length continuous generated corner; both adjacent polynomials are still compared to the original.
            }
            var source = Bernstein(p, q, p.Frame, q.Frame);
            var candidate = Bernstein(a, b, p.Frame, q.Frame);
            var difference = source.Zip(candidate, (x, y) => x - y).ToArray();
            if (state.Exact)
            {
                if (difference.Any(x => !x.IsZero)) return false;
                continue;
            }
            if (!Certify(difference, Rational.FromDouble(state.ValueBound), budget, out double value)) return false;
            Rational dt = Rational.FromDouble(q.Frame) - Rational.FromDouble(p.Frame);
            var derivative = Enumerable.Range(0, 3).Select(j => 3 * (difference[j + 1] - difference[j]) / dt).ToArray();
            if (!Certify(derivative, Rational.FromDouble(state.SpeedBound), budget, out double speed)) return false;
            valueError = Math.Max(valueError, value); speedError = Math.Max(speedError, speed);
        }
        return true;
    }

    // Exact binary floats and knot times become rationals. Coefficient equality
    // is therefore an identity test, including useful nonzero constant curves.
    static Rational[] Bernstein(Key a, Key b, double first, double last)
    {
        Rational dt = Rational.FromDouble(b.Frame) - Rational.FromDouble(a.Frame);
        Rational av = Rational.FromFloat(a.Value), bv = Rational.FromFloat(b.Value);
        Rational am = dt * Rational.FromFloat(a.OutTangent), bm = dt * Rational.FromFloat(b.InTangent);
        Rational c0 = av, c1 = am, c2 = -3 * av + 3 * bv - 2 * am - bm, c3 = 2 * av - 2 * bv + am + bm;
        Rational alpha = (Rational.FromDouble(first) - Rational.FromDouble(a.Frame)) / dt;
        Rational beta = (Rational.FromDouble(last) - Rational.FromDouble(first)) / dt;
        Rational d0 = c0 + alpha * (c1 + alpha * (c2 + alpha * c3));
        Rational d1 = beta * (c1 + alpha * (2 * c2 + 3 * alpha * c3));
        Rational d2 = beta * beta * (c2 + 3 * alpha * c3), d3 = beta * beta * beta * c3;
        return [d0, d0 + d1 / 3, d0 + 2 * d1 / 3 + d2 / 3, d0 + d1 + d2 + d3];
    }

    static bool Certify(Rational[] coefficients, Rational bound, Budget budget, out double upperBound)
    {
        Rational maximum = Rational.Zero;
        var stack = new Stack<(Rational[] Values, int Depth)>(); stack.Push((coefficients, 0));
        while (stack.TryPop(out var item))
        {
            budget.Charge(item.Values.Length);
            if (item.Values[0].Abs() > bound || item.Values[^1].Abs() > bound) { upperBound = 0; return false; }
            Rational hull = item.Values.Select(v => v.Abs()).Max();
            if (hull <= bound) { if (hull > maximum) maximum = hull; continue; }
            if (item.Depth >= budget.Depth) { upperBound = 0; return false; }
            var (left, right) = Split(item.Values);
            stack.Push((right, item.Depth + 1)); stack.Push((left, item.Depth + 1));
        }
        upperBound = maximum.ToUpperDouble(); return true;
    }

    static (Rational[] Left, Rational[] Right) Split(Rational[] values)
    {
        int n = values.Length; var current = values.ToArray(); var left = new Rational[n]; var right = new Rational[n];
        left[0] = current[0]; right[^1] = current[^1];
        for (int level = 1; level < n; level++)
        {
            for (int i = 0; i < n - level; i++) current[i] = (current[i] + current[i + 1]) / 2;
            left[level] = current[0]; right[n - level - 1] = current[n - level - 1];
        }
        return (left, right);
    }

    static bool MayHaveSingularScale(Curve curve, CancellationToken cancellation)
    {
        for (int i = 0; i < curve.Keys.Length; i++)
        {
            if ((i & 127) == 0) cancellation.ThrowIfCancellationRequested();
            if (curve.Keys[i].Value == 0) return true;
        }
        if (curve.Keys.Length < 2) return false;
        for (int i = 1; i < curve.Keys.Length; i++)
        {
            if ((i & 127) == 0) cancellation.ThrowIfCancellationRequested();
            var a = curve.Keys[i - 1]; var b = curve.Keys[i];
            if (b.Frame == a.Frame) { if (Math.Sign(a.Value) != Math.Sign(b.Value)) return true; continue; }
            var values = curve.Interpolation == Interpolation.Hermite ? Bernstein(a, b, a.Frame, b.Frame)
                : [Rational.FromFloat(a.Value), Rational.FromFloat(b.Value)];
            if (!values.All(v => v > Rational.Zero) && !values.All(v => v < Rational.Zero)) return true;
        }
        if (curve.LoopFrameLimit is { } limit && limit + curve.Keys[0].Frame > curve.Keys[^1].Frame)
        {
            var a = curve.Keys[^1]; var b = curve.Keys[0] with { Frame = limit + curve.Keys[0].Frame };
            var values = curve.Interpolation == Interpolation.Hermite ? Bernstein(a, b, a.Frame, b.Frame)
                : [Rational.FromFloat(a.Value), Rational.FromFloat(b.Value)];
            if (!values.All(v => v > Rational.Zero) && !values.All(v => v < Rational.Zero)) return true;
        }
        return false;
    }

    static double[] BuildProbes(AnimationClip baseline, BoneTrack[] output, AnimationOptimizationSettings settings,
        CancellationToken cancellation, out bool limited)
    {
        var times = new SortedSet<double>(); bool hit = false;
        double end = Math.Max(baseline.PreviewEndFrame, Math.Max(baseline.LastKeyFrame, baseline.Loop ? baseline.SampleCount : 0));
        void Add(double value)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!double.IsFinite(value) || value < 0 || value > end) return;
            if (times.Contains(value)) return;
            if (times.Count == settings.MaximumProbeFrames) { hit = true; return; }
            times.Add(value);
        }
        Add(0); Add(baseline.LastSampleFrame); Add(baseline.PreviewEndFrame);
        if (baseline.Loop) Add(baseline.SampleCount);
        foreach (double f in settings.MandatoryFrames.Concat(settings.FitJoinFrames).Concat(settings.ChannelRules.SelectMany(r => r.MandatoryFrames))) Add(f);
        foreach (var range in settings.ProtectedRanges.Concat(settings.ChannelRules.SelectMany(r => r.ProtectedRanges))) { Add(range.Start); Add(range.End); }
        var retainedBones = output.Select(t => t.Bone).ToHashSet(StringComparer.Ordinal);
        foreach (var track in baseline.Tracks.Where(t => retainedBones.Contains(t.Bone)).Concat(output))
        {
            foreach (var curve in track.Channels.OfType<Curve>())
            {
                foreach (var key in curve.Keys)
                {
                    Add(key.Frame); Add(Math.BitDecrement(key.Frame)); Add(Math.BitIncrement(key.Frame));
                    float native = (float)key.Frame; Add(MathF.BitDecrement(native)); Add(MathF.BitIncrement(native));
                }
                for (int i = 1; i < curve.Keys.Length; i++) Segment(curve.Keys[i - 1], curve.Keys[i], curve.Interpolation);
                if (curve.LoopFrameLimit is { } limit && limit + curve.Keys[0].Frame > curve.Keys[^1].Frame)
                    Segment(curve.Keys[^1], curve.Keys[0] with { Frame = limit + curve.Keys[0].Frame }, curve.Interpolation, limit);
            }
            if (track.Rotation is { } rotation) foreach (var key in rotation.Keys) Add(key.Frame);
        }
        limited = hit; return times.ToArray();

        void Segment(Key a, Key b, Interpolation interpolation, int? wrap = null)
        {
            double dt = b.Frame - a.Frame; if (dt <= 0) return;
            void At(double frame) => Add(wrap is { } limit && frame > limit ? frame - limit : frame);
            foreach (double u in new[] { .2, .25, 1d / 3, .381966011250105, .5, .618033988749895, .75, .8 }) At(a.Frame + dt * u);
            if (interpolation != Interpolation.Hermite) return;
            double c3 = 2d * a.Value - 2d * b.Value + dt * (a.OutTangent + (double)b.InTangent);
            double c2 = -3d * a.Value + 3d * b.Value - dt * (2d * a.OutTangent + b.InTangent), c1 = dt * a.OutTangent;
            void Root(double u) { if (double.IsFinite(u) && u > 0 && u < 1) At(a.Frame + dt * u); }
            double qa = 3 * c3, qb = 2 * c2;
            if (qa == 0) { if (qb != 0) Root(-c1 / qb); return; }
            double discriminant = qb * qb - 4 * qa * c1; if (discriminant < 0) return;
            double q = -.5 * (qb + Math.CopySign(Math.Sqrt(discriminant), qb));
            if (q == 0) Root(-qb / (2 * qa)); else { Root(q / qa); Root(c1 / q); }
        }
    }

    // All source numbers are finite IEEE binary floats/doubles. Rational
    // arithmetic makes proof decisions independent of cancellation or rounded
    // extrema; subdivision resource limits simply retain the original keys.
    readonly struct Rational : IComparable<Rational>
    {
        readonly BigInteger numerator, denominator;
        public static Rational Zero => new(BigInteger.Zero, BigInteger.One);
        public bool IsZero => numerator.IsZero;
        Rational(BigInteger n, BigInteger d)
        {
            if (d.IsZero) throw new DivideByZeroException();
            if (d.Sign < 0) { n = -n; d = -d; }
            if (n.IsZero) { numerator = BigInteger.Zero; denominator = BigInteger.One; return; }
            var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d); numerator = n / gcd; denominator = d / gcd;
        }
        public static Rational FromFloat(float value) => FromDouble(value);
        public static Rational FromDouble(double value)
        {
            if (!double.IsFinite(value)) throw new ArgumentException("A rational coefficient must be finite.");
            long bits = BitConverter.DoubleToInt64Bits(value); int exponent = (int)((bits >> 52) & 0x7ff); long fraction = bits & 0xfffffffffffffL;
            BigInteger n = exponent == 0 ? fraction : fraction | (1L << 52); int power = exponent == 0 ? -1074 : exponent - 1023 - 52;
            if (bits < 0) n = -n;
            return power >= 0 ? new(n << power, BigInteger.One) : new(n, BigInteger.One << -power);
        }
        public Rational Abs() => new(BigInteger.Abs(numerator), denominator);
        public double ToUpperDouble()
        {
            if (IsZero) return 0;
            // Scale numerator/denominator independently. Truncating both by
            // the same shift can turn a small nonzero numerator into zero and
            // require an unbounded walk through subnormal floating values.
            var n = BigInteger.Abs(numerator);
            int ns = (int)Math.Max(0, n.GetBitLength() - 54), ds = (int)Math.Max(0, denominator.GetBitLength() - 54);
            double value = Math.ScaleB((double)(n >> ns) / (double)(denominator >> ds), ns - ds);
            if (!double.IsFinite(value)) return double.PositiveInfinity;
            value = Math.Abs(value);
            // Check against the rational after conversion rather than assuming
            // one floating division rounded in the conservative direction.
            while (FromDouble(value) < Abs()) value = Math.BitIncrement(value);
            return value;
        }
        public int CompareTo(Rational other) => (numerator * other.denominator).CompareTo(other.numerator * denominator);
        public static implicit operator Rational(int value) => new(value, BigInteger.One);
        public static Rational operator +(Rational a, Rational b) => new(a.numerator * b.denominator + b.numerator * a.denominator, a.denominator * b.denominator);
        public static Rational operator -(Rational a, Rational b) => new(a.numerator * b.denominator - b.numerator * a.denominator, a.denominator * b.denominator);
        public static Rational operator *(Rational a, Rational b) => new(a.numerator * b.numerator, a.denominator * b.denominator);
        public static Rational operator /(Rational a, Rational b) => new(a.numerator * b.denominator, a.denominator * b.numerator);
        public static bool operator >(Rational a, Rational b) => a.CompareTo(b) > 0;
        public static bool operator <(Rational a, Rational b) => a.CompareTo(b) < 0;
        public static bool operator >=(Rational a, Rational b) => a.CompareTo(b) >= 0;
        public static bool operator <=(Rational a, Rational b) => a.CompareTo(b) <= 0;
    }
}
