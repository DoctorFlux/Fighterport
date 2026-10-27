namespace FighterPort.Core;

public sealed record AnimationOptimizationCertificate(bool Accepted, long Work, int Intervals,
    bool WorkLimitReached, double MaximumValueBound, double MaximumSpeedBound, string Reason,
    AnimationOptimizationChannelReport[] Channels)
{
    public double[] ProbeFrames { get; init; } = [];
}

public static partial class AnimationOptimization
{
    // Certifies an independently decoded representation, including changed
    // coefficients. Each interval belongs to one polynomial on BOTH curves;
    // comparing just knot values misses Hermite overshoot and quantized slopes.
    public static AnimationOptimizationCertificate CertifyCandidate(AnimationClip baseline, BoneTrack[] candidate,
        AnimationOptimizationSettings settings, CancellationToken cancellation = default,
        IReadOnlyCollection<string>? targetBoneNames = null)
    {
        ArgumentNullException.ThrowIfNull(baseline); ArgumentNullException.ThrowIfNull(candidate);
        ValidateSettings(settings); cancellation.ThrowIfCancellationRequested();
        if (baseline.SampleCount < 1 || baseline.Tracks.Select(t => t.Bone).Distinct(StringComparer.Ordinal).Count() != baseline.Tracks.Length
            || candidate.Select(t => t.Bone).Distinct(StringComparer.Ordinal).Count() != candidate.Length)
            throw new ArgumentException("A scalar certificate needs unique native track identities and positive timing.");
        long count = 0, inputCount = baseline.Tracks.Sum(CountKeys), outputCount = candidate.Sum(CountKeys);
        if (inputCount > MaximumInputKeys || outputCount > MaximumInputKeys)
            throw new NotSupportedException("Each native certificate input must stay within the two-million-key operation limit.");
        foreach (var track in baseline.Tracks.Concat(candidate))
        {
            if (track.Channels.Length != 9 || track.Rotation is not null)
                throw new NotSupportedException("A native scalar certificate needs nine sparse Hermite channels without quaternion conversion.");
            foreach (var curve in track.Channels.OfType<Curve>())
            {
                count += curve.Keys.LongLength; ValidateCurve(curve);
                if (curve.Interpolation != Interpolation.Hermite || count > 2L * MaximumInputKeys)
                    throw new NotSupportedException("The Hermite certificate exceeded its native input scope or key budget.");
            }
        }
        var originals = baseline.Tracks.ToDictionary(t => t.Bone, StringComparer.Ordinal);
        var outputs = candidate.ToDictionary(t => t.Bone, StringComparer.Ordinal);
        if (outputs.Keys.Any(name => !originals.ContainsKey(name)))
            return new(false, 0, 0, false, 0, 0, "Certification cannot introduce a native track.", []);
        var binds = settings.BindPoses.ToDictionary(b => b.Bone, StringComparer.Ordinal);
        var targets = targetBoneNames?.ToHashSet(StringComparer.Ordinal);
        foreach (var omitted in baseline.Tracks.Where(t => !outputs.ContainsKey(t.Bone)))
        {
            bool protectedTrack = settings.PreserveStructureBones.Contains(omitted.Bone, StringComparer.Ordinal)
                || settings.ProtectedRanges.Length != 0
                || settings.ChannelRules.Any(r => r.Bone == omitted.Bone && (r.PreserveStructure || r.ProtectedRanges.Length != 0));
            bool bindConstant = !protectedTrack && binds.TryGetValue(omitted.Bone, out var bind)
                && omitted.Channels.Select((curve, channel) => curve is null || IsBindConstant(curve, bind.Channels[channel])).All(v => v);
            if (protectedTrack || targets?.Contains(omitted.Bone) != false && !bindConstant)
                return new(false, 0, 0, false, 0, 0, "A removed native target track is not proven bind-constant or has structural protections.", []);
        }
        var budget = new Budget(settings, cancellation);
        var exactBones = settings.ExactOnlyBones.ToHashSet(StringComparer.Ordinal);
        var protectedBones = settings.PreserveStructureBones.ToHashSet(StringComparer.Ordinal);
        var reports = new List<AnimationOptimizationChannelReport>(); int intervals = 0;
        double maximumValue = 0, maximumSpeed = 0;
        string reason = "Continuous rational Hermite value/derivative certificate passed; native floating and cumulative geometry checks remain separate.";
        try
        {
            foreach (var sourceTrack in baseline.Tracks)
            {
                if (!outputs.TryGetValue(sourceTrack.Bone, out var outputTrack)) continue;
                bool singular = sourceTrack.Channels.Take(3).Concat(outputTrack.Channels.Take(3)).OfType<Curve>().Any(c => MayHaveSingularScale(c, cancellation));
                for (int channel = 0; channel < 9; channel++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var source = sourceTrack.Channels[channel]; var output = outputTrack.Channels[channel];
                    if (source is null && output is null) continue;
                    if (source is null || output is null || source.LoopFrameLimit != output.LoopFrameLimit)
                        return Failed("Indexed storage changed sparse channel membership or its native loop limit.");
                    var rules = settings.ChannelRules.Where(r => r.Bone == sourceTrack.Bone && (r.Channel < 0 || r.Channel == channel)).ToArray();
                    bool exact = settings.Mode == AnimationOptimizationMode.Exact || exactBones.Contains(sourceTrack.Bone) || singular || rules.Any(r => r.ExactOnly);
                    bool preserve = protectedBones.Contains(sourceTrack.Bone) || rules.Any(r => r.PreserveStructure);
                    var frames = ExactFrames(settings,rules);
                    var ranges = settings.ProtectedRanges.Concat(rules.SelectMany(r => r.ProtectedRanges)).ToArray();
                    var incoming = source.Keys.GroupBy(k => k.Frame).ToDictionary(g => g.Key, g => g.ToArray());
                    var outgoing = output.Keys.GroupBy(k => k.Frame).ToDictionary(g => g.Key, g => g.ToArray());
                    // A raw +zero-slope constant has an exact fixed-storage
                    // representation at all protected join times. Structural
                    // and authored interval protections still retain tuples.
                    bool fixedConstant = !preserve && ranges.Length == 0 && IsExactFixedRepresentation(source, output);
                    // Earlier proved reduction may have removed interior knots.
                    // Recheck the complete composed result against the original,
                    // while retaining all protected authored sides and tuple bits.
                    for (int k = 0; k < source.Keys.Length; k++)
                    {
                        var a = source.Keys[k];
                        budget.Charge(1);
                        bool paired = k > 0 && source.Keys[k - 1].Frame == a.Frame || k + 1 < source.Keys.Length && source.Keys[k + 1].Frame == a.Frame;
                        bool protectedKey = preserve || paired || HasSignedZero(a) || !fixedConstant && frames.Contains(a.Frame)
                            || ranges.Any(r => a.Frame >= r.Start && a.Frame <= r.End);
                        bool fitTime=settings.Mode==AnimationOptimizationMode.SmallFile&&!fixedConstant&&settings.FitJoinFrames.Contains(a.Frame);
                        if (!protectedKey&&!fitTime) continue;
                        var required = incoming[a.Frame];
                        var actual = outgoing.GetValueOrDefault(a.Frame, []);
                        if (required.Length != actual.Length || protectedKey&&required.Where((key, side) =>
                            Bits(key.Value) != Bits(actual[side].Value) || Bits(key.InTangent) != Bits(actual[side].InTangent)
                            || Bits(key.OutTangent) != Bits(actual[side].OutTangent)).Any())
                            return Failed("Indexed storage changed a protected native key, paired side, signed zero or index.");
                    }
                    double valueBound = exact ? 0 : channel < 3 ? settings.ScaleValueBound : channel < 6 ? settings.EulerValueBoundDegrees
                        : TranslationBound(settings,false);
                    double speedBound = exact ? 0 : channel < 3 ? settings.ScaleSpeedBoundPerFrame : channel < 6 ? settings.EulerSpeedBoundDegreesPerFrame
                        : TranslationBound(settings,true);
                    double end = Math.Max(baseline.SampleCount, Math.Max(source.Keys[^1].Frame, output.Keys[^1].Frame));
                    var breaks = source.Keys.Concat(output.Keys).Select(k => k.Frame).Where(f => f >= 0 && f <= end).Append(0).Append(end).Distinct().Order().ToArray();
                    double valueMaximum = 0, speedMaximum = 0; int channelIntervals = 0;
                    // Protection points are additional value witnesses, not new
                    // polynomial breaks. Splitting at a mandatory interior
                    // frame would hide that it protects the original segment.
                    var points = breaks.Concat(frames).Concat(settings.FitJoinFrames).Concat(ranges.SelectMany(r => new[] { r.Start, r.End }))
                        .Where(f => f >= 0 && f <= end).Distinct().Order();
                    foreach (double point in points)
                    {
                        budget.Charge(1);
                        var difference = At(source, point) - At(output, point);
                        bool protectedPoint = frames.Contains(point) || ranges.Any(r => point >= r.Start && point <= r.End);
                        if (difference.Abs() > Rational.FromDouble(protectedPoint ? 0 : valueBound)) return Failed("A native exact-time value exceeds its frozen scalar bound or exact point protection.");
                        valueMaximum = Math.Max(valueMaximum, difference.Abs().ToUpperDouble());
                    }
                    for (int i = 1; i < breaks.Length; i++)
                    {
                        double first = breaks[i - 1], last = breaks[i]; if (last <= first) continue;
                        budget.Charge(1); intervals++; channelIntervals++;
                        var difference = Piece(source, first, last).Zip(Piece(output, first, last), (a, b) => a - b).ToArray();
                        Rational dt = Rational.FromDouble(last) - Rational.FromDouble(first);
                        var derivative = Enumerable.Range(0, 3).Select(j => 3 * (difference[j + 1] - difference[j]) / dt).ToArray();
                        // A sparse protected range can contain no source knot.
                        // Its polynomial must still be identical, as must the
                        // segment containing a strict interior protected point.
                        // Merely touching an authored join does not make the
                        // unprotected adjacent polynomial exact-only.
                        bool protectedInterval = frames.Any(f => f > first && f < last) || ranges.Any(r =>
                            Math.Max(first, r.Start) < Math.Min(last, r.End) || r.Start == r.End && r.Start > first && r.Start < last);
                        if (!Certify(difference, Rational.FromDouble(protectedInterval ? 0 : valueBound), budget, out double value)
                            || !Certify(derivative, Rational.FromDouble(protectedInterval ? 0 : speedBound), budget, out double speed))
                            return Failed("A continuous Hermite value/derivative bound failed or could not be proved within subdivision limits.");
                        valueMaximum = Math.Max(valueMaximum, value); speedMaximum = Math.Max(speedMaximum, speed);
                    }
                    maximumValue = Math.Max(maximumValue, valueMaximum); maximumSpeed = Math.Max(maximumSpeed, speedMaximum);
                    reports.Add(new(sourceTrack.Bone, channel, source.Keys.Length, output.Keys.Length,
                        exact ? "Exact decoded coefficient identity" : "Continuous decoded-coefficient Bernstein bounds", 0, 0,
                        channelIntervals, valueMaximum, speedMaximum, valueBound, speedBound, null));
                }
            }
            var probes = BuildProbes(baseline, candidate, settings, cancellation, out bool probeLimited);
            if (probeLimited) return Failed("The complete decoded-candidate witness set exceeds the existing native verification probe budget.");
            return new(true, budget.Work, intervals, false, maximumValue, maximumSpeed, reason, reports.ToArray()) { ProbeFrames = probes };
        }
        catch (LimitReachedException) { return Failed("Continuous indexed-storage proof exhausted its operation budget; candidate retained precise storage."); }

        AnimationOptimizationCertificate Failed(string why) => new(false, budget.Work, intervals, budget.WorkLimited,
            maximumValue, maximumSpeed, why, reports.ToArray());
    }

    static Rational At(Curve curve, double frame)
    {
        int exact = UpperKey(curve.Keys, frame) - 1;
        if (exact >= 0 && curve.Keys[exact].Frame == frame)
        {
            while (exact > 0 && curve.Keys[exact - 1].Frame == frame) exact--;
            return Rational.FromFloat(curve.Keys[exact].Value);
        }
        return Piece(curve, frame, frame)[0];
    }

    static int UpperKey(Key[] keys, double frame)
    {
        int first = 0, last = keys.Length;
        while (first < last) { int middle = first + (last - first) / 2; if (keys[middle].Frame <= frame) first = middle + 1; else last = middle; }
        return first;
    }

    static Rational[] Piece(Curve curve, double first, double last)
    {
        var keys = curve.Keys;
        Rational[] Constant(float value) { var v = Rational.FromFloat(value); return [v, v, v, v]; }
        if (keys.Length == 1) return Constant(keys[0].Value);
        double middle = first + (last - first) / 2;
        if (middle < keys[0].Frame || middle > keys[^1].Frame)
        {
            if (curve.LoopFrameLimit is not { } limit) return Constant(middle < keys[0].Frame ? keys[0].Value : keys[^1].Value);
            var a = keys[^1]; var b = keys[0];
            if (middle < b.Frame) a = a with { Frame = a.Frame - limit };
            else b = b with { Frame = b.Frame + limit };
            if (b.Frame <= a.Frame) throw new ArgumentException("A loop wrap must have positive native interpolation length.");
            return Bernstein(a, b, first, last);
        }
        int next = UpperKey(keys, middle);
        if (next == 0 || next == keys.Length) return Constant(next == 0 ? keys[0].Value : keys[^1].Value);
        return Bernstein(keys[next - 1], keys[next], first, last);
    }
}
