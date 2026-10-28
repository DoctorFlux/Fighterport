using FighterPort.Core;
using System.Text.Json;

// Independent polynomial fixtures. Native serialization, complete hierarchy,
// skin and contact qualification remain the owning adapter's separate gates.
internal static class SpanRefittingChecks
{
    public static void Run(Action<bool, string> check)
    {
        AnalyticOpportunity(check);
        AdjacentSpanSides(check);
        Protections(check);
        PairedSidesAndLoop(check);
        FractionalEligibility(check);
        ScaleAndSparseChannels(check);
        WindingAndIdentity(check);
        LimitsAndCancellation(check);
    }

    static AnimationOptimizationSettings Bounds() => new()
    {
        Mode = AnimationOptimizationMode.SmallFile, BindBoundsDiagonal = 1,
        EulerValueBoundDegrees = .005, EulerSpeedBoundDegreesPerFrame = .1,
        EnableSpanRefitting = true
    };
    static Curve Dense(float step = 1, float origin = 0) => new(Enumerable.Range(0, 17)
        .Select(i => new Key(i, origin + i * step, .98f * step, .98f * step)).ToArray());
    static BoneTrack Track(string bone, int channel, Curve curve)
    {
        var channels = new Curve?[9]; channels[channel] = curve; return new(bone, channels);
    }
    static AnimationClip Clip(params BoneTrack[] tracks) => new(Guid.NewGuid(), "Independent span/tangent fixture", 17, new(60), false, tracks);
    static Curve Output(AnimationOptimizationResult result, string bone = "detail", int channel = 3) => result.Tracks.Single(t => t.Bone == bone).Channels[channel]!;
    static bool Bits(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
    static bool SameKey(Key a, Key b) => BitConverter.DoubleToInt64Bits(a.Frame) == BitConverter.DoubleToInt64Bits(b.Frame)
        && Bits(a.Value, b.Value) && Bits(a.InTangent, b.InTangent) && Bits(a.OutTangent, b.OutTangent);
    // One native scalar knot owns one slope. A split incoming/outgoing side
    // therefore costs two rows; logical Core key count alone can mislead.
    static int NativeRows(Curve curve) => curve.Keys.Sum(k => Bits(k.InTangent, k.OutTangent) ? 1 : 2);

    // Deliberately expanded double polynomial, separate from Curve.Sample's
    // float implementation and the optimizer's normalized Bernstein arithmetic.
    static (double Value, double Speed) Polynomial(Curve curve, double time)
    {
        if (curve.Keys.Length == 1) return (curve.Keys[0].Value, 0);
        Key a, b;
        if (time < curve.Keys[0].Frame || time > curve.Keys[^1].Frame)
        {
            if (curve.LoopFrameLimit is not { } limit) return (time < curve.Keys[0].Frame ? curve.Keys[0].Value : curve.Keys[^1].Value, 0);
            a = curve.Keys[^1]; b = curve.Keys[0];
            if (time < b.Frame) a = a with { Frame = a.Frame - limit }; else b = b with { Frame = b.Frame + limit };
        }
        else
        {
            int next = 0; while (next < curve.Keys.Length && curve.Keys[next].Frame <= time) next++;
            if (next == curve.Keys.Length) return (curve.Keys[^1].Value, curve.Keys[^1].InTangent);
            a = curve.Keys[next - 1]; b = curve.Keys[next];
        }
        double h = b.Frame - a.Frame, x = (time - a.Frame) / h;
        double c0 = a.Value, c1 = h * a.OutTangent;
        double c2 = 3 * ((double)b.Value - a.Value) - h * (2 * (double)a.OutTangent + b.InTangent);
        double c3 = 2 * ((double)a.Value - b.Value) + h * ((double)a.OutTangent + b.InTangent);
        return (c0 + x * (c1 + x * (c2 + x * c3)), (c1 + x * (2 * c2 + x * 3 * c3)) / h);
    }

    static (double Value, double Speed) Withheld(Curve source, Curve candidate, double end = 16)
    {
        double value = 0, speed = 0;
        for (int i = 0; i < 4097; i++)
        {
            double t = end * (i + .137) / 4097;
            var a = Polynomial(source, t); var b = Polynomial(candidate, t);
            value = Math.Max(value, Math.Abs(a.Value - b.Value)); speed = Math.Max(speed, Math.Abs(a.Speed - b.Speed));
        }
        return (value, speed);
    }

    static void AnalyticOpportunity(Action<bool, string> check)
    {
        var original = Dense(); var clip = Clip(Track("detail", 3, original));
        var settings = Bounds(); string frozen = JsonSerializer.Serialize(clip, ProjectStore.Json);
        double delta = 1d - .98f;
        // On every unit interval p(t)=t-delta*u*(1-u)*(1-2u).
        // Its distance to the line is delta*sqrt(3)/18; derivative distance
        // is delta. Keeping .98 endpoint slopes on a 16-frame span instead
        // gives error 16*delta*3/32 at native frame4 (an original exact key).
        double optimalLineError = delta * Math.Sqrt(3) / 18;
        var line = new Curve([new(0, 0, 1, 1), new(16, 16, 1, 1)]);
        var retainedSlopes = new Curve([original.Keys[0], original.Keys[^1]]);
        check(optimalLineError < .001925 && delta < .020001 && 16 * delta * 3 / 32 > .02999
            && Math.Abs(Polynomial(retainedSlopes, 4).Value - 4) > settings.EulerValueBoundDegrees,
            "Refitting: independent expanded polynomials establish a legal line and a failing original-tangent long span before optimization");
        var lineTracks = new[] { clip.Tracks[0] with { Channels = clip.Tracks[0].Channels.Select((c, i) => i == 3 ? line : c).ToArray() } };
        var lineCertificate = AnimationOptimization.CertifyCandidate(clip, lineTracks, settings, targetBoneNames: ["detail"]);
        check(lineCertificate.Accepted && lineCertificate.MaximumValueBound >= optimalLineError && lineCertificate.MaximumSpeedBound >= delta,
            "Refitting: the independently specified line passes continuous value and derivative bounds against all seventeen original native keys");
        var deletion = AnimationOptimization.Optimize(clip, ["detail"], settings with { EnableSpanRefitting = false });
        var refitted = AnimationOptimization.Optimize(clip, ["detail"], settings);
        var output = Output(refitted); var certificate = AnimationOptimization.CertifyCandidate(clip, refitted.Tracks, settings, targetBoneNames: ["detail"]);
        check(NativeRows(output) < NativeRows(Output(deletion)) && output.Keys.Length < original.Keys.Length
            && refitted.Report.AcceptedRefitSpans > 0 && refitted.Report.RefitTangents > 0,
            "Refitting: explicit tangent fitting beats deletion-only native scalar-row cost, including required split-side storage");
        var actual = Withheld(original, output);
        check(certificate.Accepted && actual.Value <= settings.EulerValueBoundDegrees && actual.Speed <= settings.EulerSpeedBoundDegreesPerFrame
            && certificate.MaximumValueBound + 1e-12 >= actual.Value && certificate.MaximumSpeedBound + 1e-12 >= actual.Speed,
            "Refitting: independent withheld double-polynomial values and derivatives remain below the cumulative rational certificate");
        check(frozen == JsonSerializer.Serialize(clip, ProjectStore.Json) && output.Keys[0].Frame == 0 && output.Keys[^1].Frame == 16
            && output.Keys[0].Value == 0 && output.Keys[^1].Value == 16 && output.LoopFrameLimit == original.LoopFrameLimit,
            "Refitting: frozen source, endpoint times/values and loop metadata remain unchanged");
        var exact = AnimationOptimization.Optimize(clip, ["detail"], settings with { Mode = AnimationOptimizationMode.Exact });
        check(exact.Report.AcceptedRefitSpans == 0 && exact.Report.RefitTangents == 0
            && exact.Tracks.SelectMany(t => t.Channels.OfType<Curve>()).All(c => c.Keys.All(k => Bits(k.InTangent, .98f) && Bits(k.OutTangent, .98f))),
            "Refitting: enabling the search never turns Exact into approximate tangent generation");
    }

    static void Protections(Action<bool, string> check)
    {
        var original = Dense(); var clip = Clip(Track("detail", 3, original)); var settings = Bounds();
        foreach (var protectedSettings in new[]
        {
            settings with { ExactOnlyBones = ["detail"] },
            settings with { PreserveStructureBones = ["detail"] },
            settings with { ChannelRules = [new("detail", 3) { ExactOnly = true }] },
            settings with { ChannelRules = [new("detail", 3) { PreserveStructure = true }] }
        })
        {
            var result = AnimationOptimization.Optimize(clip, ["detail"], protectedSettings);
            check(result.Report.AcceptedRefitSpans == 0 && ReferenceEquals(Output(result), original),
                "Refitting: source-owned root, authored pin/attachment and channel-specific exact/structural contracts override the refit permission");
        }
        var joined = AnimationOptimization.Optimize(clip, ["detail"], settings with { MandatoryFrames = [8] });
        check(Output(joined).Keys.Where(k => k.Frame == 8).SequenceEqual(original.Keys.Where(k => k.Frame == 8))
            && AnimationOptimization.CertifyCandidate(clip, joined.Tracks, settings with { MandatoryFrames = [8] }, targetBoneNames: ["detail"]).Accepted,
            "Refitting: an exact fitting join retains every original endpoint value and tangent tuple");
        var rangedSettings = settings with { ProtectedRanges = [new(6.25, 9.75, "Independent authored contact/middle")] };
        var ranged = AnimationOptimization.Optimize(clip, ["detail"], rangedSettings); var rangedOutput = Output(ranged);
        check(Enumerable.Range(0, 65).All(i => Bits(original.Sample(6.25 + 3.5 * i / 64), rangedOutput.Sample(6.25 + 3.5 * i / 64)))
            && AnimationOptimization.CertifyCandidate(clip, ranged.Tracks, rangedSettings, targetBoneNames: ["detail"]).Accepted,
            "Refitting: a protected fractional contact/middle interval retains its complete native float sampling, not only integer keys");
        var interiorSettings = settings with { MandatoryFrames = [.5] };
        var interior = AnimationOptimization.Optimize(clip, ["detail"], interiorSettings);
        check(Enumerable.Range(0, 65).All(i => Bits(original.Sample(i / 64d), Output(interior).Sample(i / 64d)))
            && AnimationOptimization.CertifyCandidate(clip, interior.Tracks, interiorSettings, targetBoneNames: ["detail"]).Accepted,
            "Refitting: a mandatory point inside an existing piece protects that original polynomial and its neighboring endpoint slopes");
    }

    static void AdjacentSpanSides(Action<bool, string> check)
    {
        var original = new Curve(Enumerable.Range(0, 17).Select(i => new Key(i,
            i <= 8 ? i : 8 + 1.02f * (i - 8), i == 8 ? 1 : .98f, i == 8 ? 1 : .98f)).ToArray());
        var clip = Clip(Track("detail", 3, original)); var settings = Bounds();
        string frozen = JsonSerializer.Serialize(clip, ProjectStore.Json);
        // A constructive independent solution consists of two straight spans:
        // slope1 until frame8, then1.02. Each unit source piece differs only
        // by its bounded endpoint slopes. Source value and speed differences
        // are checked independently below; no global smooth tangent is assumed.
        var constructed = new Curve([new(0, 0, 1, 1), new(8, 8, 1, 1), new(8, 8, 1.02f, 1.02f),
            new(16, original.Keys[^1].Value, 1.02f, 1.02f)]);
        var constructedTracks = new[] { Track("detail", 3, constructed) };
        var independent = Withheld(original, constructed);
        var legacy = AnimationOptimization.Optimize(clip, ["detail"], settings with { EnableSpanRefitting = false });
        var fitted = AnimationOptimization.Optimize(clip, ["detail"], settings); var output = Output(fitted);
        var newPairs = output.Keys.GroupBy(k => k.Frame).Where(group => group.Count() == 2
            && original.Keys.Count(k => k.Frame == group.Key) == 1).ToArray();
        check(independent.Value <= .005 && independent.Speed <= .1 &&
            AnimationOptimization.CertifyCandidate(clip, constructedTracks, settings, targetBoneNames: ["detail"]).Accepted &&
            fitted.Report.AcceptedRefitSpans >= 2 && NativeRows(output) < NativeRows(Output(legacy)) &&
            newPairs.Any(group => !Bits(group.First().OutTangent, group.Last().InTangent)),
            "Refitting: adjacent accepted spans beat deletion-only storage and expose distinct shared-knot sides under independently feasible piecewise-linear bounds");
        var actual = Withheld(original, output);
        check(newPairs.All(group => group.All(k => Bits(k.InTangent, k.OutTangent))
                && group.Select(k => BitConverter.SingleToInt32Bits(k.Value)).Distinct().Count() == 1) &&
            AnimationOptimization.CertifyCandidate(clip, fitted.Tracks, settings, targetBoneNames: ["detail"]).Accepted &&
            actual.Value <= .005 && actual.Speed <= .1 && frozen == JsonSerializer.Serialize(clip, ProjectStore.Json),
            "Refitting: neighboring fits expand shared incoming/outgoing sides into legal native paired rows and certify the complete immutable original motion");
        var loop = original with { LoopFrameLimit = 18 }; var loopClip = Clip(Track("detail", 3, loop)) with { Loop = true };
        string loopFrozen = JsonSerializer.Serialize(loopClip, ProjectStore.Json);
        var loopResult = AnimationOptimization.Optimize(loopClip, ["detail"], settings); var loopOutput = Output(loopResult);
        check(loopResult.Report.AcceptedRefitSpans >= 2 && loopOutput.Keys.GroupBy(k => k.Frame).Any(group => group.Count() == 2)
            && loopOutput.Keys.All(k => Bits(k.InTangent, k.OutTangent))
            && Bits(loopOutput.Keys[0].InTangent, loop.Keys[0].InTangent)
            && Bits(loopOutput.Keys[^1].OutTangent, loop.Keys[^1].OutTangent)
            && new[] { -.137, 16.137, 17.713, 18d }.All(t => Bits(loop.Sample(t), loopOutput.Sample(t)))
            && AnimationOptimization.CertifyCandidate(loopClip, loopResult.Tracks, settings, targetBoneNames: ["detail"]).Accepted
            && loopFrozen == JsonSerializer.Serialize(loopClip, ProjectStore.Json),
            "Refitting: adjacent looped spans retain legal paired output and the exact original outside endpoint/wrap tangents without mutating the loop master");
    }

    static void PairedSidesAndLoop(Action<bool, string> check)
    {
        var keys = Dense().Keys; var before = keys.Take(8); var after = keys.Skip(9);
        var corner = new Curve(before.Concat(new[] { keys[8], new Key(8, 8, 1.02f, 1.02f) }).Concat(after).ToArray());
        var clip = Clip(Track("detail", 3, corner)); var settings = Bounds() with { EulerValueBoundDegrees = .01 };
        var unknown = AnimationOptimization.Optimize(clip, ["detail"], settings);
        check(Output(unknown).Keys.Where(k => k.Frame == 8).SequenceEqual(corner.Keys.Where(k => k.Frame == 8)),
            "Refitting: unknown authored paired sides remain exact even with equal values and an explicit bounded refit policy");
        var generatedSettings = settings with { GeneratedPairSites = [new("detail", 3, 8)] };
        var generated = AnimationOptimization.Optimize(clip, ["detail"], generatedSettings);
        check(AnimationOptimization.CertifyCandidate(clip, generated.Tracks, generatedSettings, targetBoneNames: ["detail"]).Accepted
            && Withheld(corner, Output(generated)).Value <= .01,
            "Refitting: reviewed generated corners remain certified against both original Hermite sides and the immutable baseline");
        var jump = corner with { Keys = corner.Keys.Select((k, i) => i == 9 ? k with { Value = 8.01f } : k).ToArray() };
        var jumpClip = Clip(Track("detail", 3, jump));
        var jumpResult = AnimationOptimization.Optimize(jumpClip, ["detail"], generatedSettings);
        check(Output(jumpResult).Keys.Where(k => k.Frame == 8).SequenceEqual(jump.Keys.Where(k => k.Frame == 8)),
            "Refitting: generated provenance never authorizes smoothing a real paired value discontinuity");
        float minusZero = BitConverter.Int32BitsToSingle(int.MinValue);
        var signed = Dense() with { Keys = Dense().Keys.Select((k, i) => i == 8 ? k with { InTangent = minusZero, OutTangent = minusZero } : k).ToArray() };
        var signedResult = AnimationOptimization.Optimize(Clip(Track("detail", 3, signed)), ["detail"], settings);
        check(Output(signedResult).Keys.Where(k => k.Frame == 8).Single() is var kept && Bits(kept.InTangent, minusZero) && Bits(kept.OutTangent, minusZero),
            "Refitting: signed-zero authored tangent evidence survives rather than being normalized by the fit");

        var looped = Dense() with { LoopFrameLimit = 18 }; var loopClip = Clip(Track("detail", 3, looped)) with { Loop = true };
        var loopResult = AnimationOptimization.Optimize(loopClip, ["detail"], settings); var loopOutput = Output(loopResult);
        check(loopOutput.LoopFrameLimit == 18 && Bits(loopOutput.Keys[0].InTangent, looped.Keys[0].InTangent)
            && Bits(loopOutput.Keys[^1].OutTangent, looped.Keys[^1].OutTangent)
            && new[] { -.137, 16d, 16.137, 17.713, 18d }.All(t => Bits(looped.Sample(t), loopOutput.Sample(t))),
            "Refitting: untouched outside endpoint sides preserve native looping/closing behavior, including terminal samples");
        foreach (int count in new[] { 1, 2 })
        {
            var shortCurve = new Curve(Dense().Keys.Take(count).ToArray());
            var shortClip = Clip(Track("detail", 3, shortCurve)) with { SampleCount = count };
            var result = AnimationOptimization.Optimize(shortClip, ["detail"], settings);
            check(Output(result).Keys.SequenceEqual(shortCurve.Keys) && result.Report.AcceptedRefitSpans == 0,
                "Refitting: a one/two-frame input has no invented reduction or terminal frame / count=" + count);
        }
    }

    static void ScaleAndSparseChannels(Action<bool, string> check)
    {
        var settings = Bounds() with { ScaleValueBound = 1e-6, ScaleSpeedBoundPerFrame = .0001 };
        foreach (float sign in new[] { 1f, -1f })
        {
            var source = Dense(sign / 4096, sign); var clip = Clip(Track("detail", 0, source));
            var result = AnimationOptimization.Optimize(clip, ["detail"], settings);
            check(AnimationOptimization.CertifyCandidate(clip, result.Tracks, settings, targetBoneNames: ["detail"]).Accepted
                && Enumerable.Range(0, 513).All(i => Math.Sign(Output(result, channel: 0).Sample(i / 32d)) == Math.Sign(sign)),
                "Refitting: stable positive/mirrored scale retains its sign and declared dimensionless bounds / sign=" + sign);
        }
        var channels = new Curve?[9]; channels[0] = new([new(0, -1, .125f, .125f), new(8, 0, .125f, .125f), new(16, 1, .125f, .125f)]);
        channels[3] = Dense(); var crossing = Clip(new BoneTrack("detail", channels));
        var exact = AnimationOptimization.Optimize(crossing, ["detail"], settings);
        check(ReferenceEquals(Output(exact), channels[3]) && exact.Report.AcceptedRefitSpans == 0,
            "Refitting: singular/sign-changing scale keeps the complete bone exact-only, including its rotation channels");
        channels = new Curve?[9]; channels[0] = new([new(0, 2)]); channels[2] = new([new(0, -.5f)]); channels[3] = Dense();
        var sparse = Clip(new BoneTrack("detail", channels), new BoneTrack("empty", new Curve?[9]));
        var output = AnimationOptimization.Optimize(sparse, ["empty", "detail", "no-authored-track"], settings);
        check(output.Tracks.Single(t => t.Bone == "detail").Channels[1] is null
            && output.Tracks.Single(t => t.Bone == "empty").Channels.All(c => c is null)
            && output.Tracks.All(t => t.Bone != "no-authored-track") && Output(output, channel: 0).Keys[0].Value == 2 && Output(output, channel: 2).Keys[0].Value == -.5f,
            "Refitting: nonuniform/mirrored bind fallback stays sparse without normalizing scale or materializing absent axes/bones");
    }

    static void FractionalEligibility(Action<bool, string> check)
    {
        var fractional = new Curve(Enumerable.Range(0, 17).Select(i => new Key(i / 2d, i / 2f, .98f, .98f)).ToArray());
        var clip = Clip(Track("detail", 3, fractional)) with { SampleCount = 9 };
        var settings = Bounds() with { EulerValueBoundDegrees = .0015 };
        var result = AnimationOptimization.Optimize(clip, ["detail"], settings); var output = Output(result);
        var times = fractional.Keys.Select(k => BitConverter.DoubleToInt64Bits(k.Frame)).ToHashSet();
        check(result.Report.AcceptedRefitSpans > 0 && output.Keys.All(k => times.Contains(BitConverter.DoubleToInt64Bits(k.Frame)))
            && AnimationOptimization.CertifyCandidate(clip, result.Tracks, settings, targetBoneNames: ["detail"]).Accepted
            && Withheld(fractional, output, 8).Value <= settings.EulerValueBoundDegrees,
            "Refitting: authentic half-frame times remain on their original exact native addresses while the complete fractional motion certifies");
        var nonNative = fractional with { Keys = fractional.Keys.Select((k, i) => i == 1 ? k with { Frame = .1 } : k).ToArray() };
        var nonNativeResult = AnimationOptimization.Optimize(Clip(Track("detail", 3, nonNative)), ["detail"], Bounds());
        check(ReferenceEquals(Output(nonNativeResult), nonNative) && nonNativeResult.Report.AcceptedRefitSpans == 0,
            "Refitting: a non-native double knot is preserved without snapping times to a representable encoding grid");
        var held = fractional with { Interpolation = Interpolation.Step };
        var heldResult = AnimationOptimization.Optimize(Clip(Track("detail", 3, held)), ["detail"], Bounds());
        check(ReferenceEquals(Output(heldResult), held) && heldResult.Report.AcceptedRefitSpans == 0,
            "Refitting: held interpolation is not silently replaced by a smooth Hermite tangent fit");
    }

    static void WindingAndIdentity(Action<bool, string> check)
    {
        var source = Dense(45); var clip = Clip(Track("detail", 5, source));
        var settings = Bounds() with { EulerValueBoundDegrees = .1, EulerSpeedBoundDegreesPerFrame = .1 };
        var result = AnimationOptimization.Optimize(clip, ["detail"], settings); var output = Output(result, channel: 5);
        check(output.Keys[^1].Value == 720 && Polynomial(output, 8).Value > 359.9
            && AnimationOptimization.CertifyCandidate(clip, result.Tracks, settings, targetBoneNames: ["detail"]).Accepted,
            "Refitting: full 720-degree winding remains raw Euler motion rather than a shortest-orientation shortcut");
        var body = Track("detail", 3, Dense()); var root = Track("reviewed source control", 6, Dense(1f / 16));
        var original = Clip(body, root); var originalSettings = Bounds() with { ExactOnlyBones = [root.Bone] };
        var first = AnimationOptimization.Optimize(original, [root.Bone, body.Bone], originalSettings);
        var renamed = original with { Tracks = [root with { Bone = "unfamiliar control identity" }, body with { Bone = "unfamiliar body identity" }] };
        var renamedSettings = originalSettings with { ExactOnlyBones = ["unfamiliar control identity"] };
        var other = AnimationOptimization.Optimize(renamed, ["unfamiliar body identity", "unfamiliar control identity"], renamedSettings);
        check(Output(first).Keys.SequenceEqual(Output(other, "unfamiliar body identity").Keys)
            && first.Tracks.Single(t => t.Bone == root.Bone).Channels[6]!.Keys.SequenceEqual(root.Channels[6]!.Keys)
            && other.Tracks.Single(t => t.Bone == "unfamiliar control identity").Channels[6]!.Keys.SequenceEqual(root.Channels[6]!.Keys),
            "Refitting: rename/storage permutation leaves coefficients identical while explicitly reviewed source-control motion stays exact");
        var again = AnimationOptimization.Optimize(original, [root.Bone, body.Bone], originalSettings);
        check(JsonSerializer.Serialize(first.Report) == JsonSerializer.Serialize(again.Report)
            && first.Tracks.SelectMany(t => t.Channels.OfType<Curve>()).SelectMany(c => c.Keys)
                .SequenceEqual(again.Tracks.SelectMany(t => t.Channels.OfType<Curve>()).SelectMany(c => c.Keys)),
            "Refitting: identical immutable inputs produce deterministic coefficient, work and search receipts");
    }

    static void LimitsAndCancellation(Action<bool, string> check)
    {
        var original = Dense(); var clip = Clip(Track("detail", 3, original)); var settings = Bounds();
        foreach (var limited in new[] { settings with { MaximumWork = 0 }, settings with { MaximumCandidateSpans = 0 } })
        {
            var result = AnimationOptimization.Optimize(clip, ["detail"], limited);
            check(ReferenceEquals(Output(result), original) && result.Report.AcceptedRefitSpans == 0
                && (result.Report.WorkLimitReached || result.Report.CandidateLimitReached),
                "Refitting: exhausted shared proof/span budgets return unchanged source rather than an unvalidated new fit");
        }
        var budget = settings with { MaximumWork = 2000, MaximumCandidateSpans = 3 };
        var partial = AnimationOptimization.Optimize(clip, ["detail"], budget);
        check(partial.Report.Work <= 2000 && partial.Report.CandidateSpans <= 3
            && AnimationOptimization.CertifyCandidate(clip, partial.Tracks, settings, targetBoneNames: ["detail"]).Accepted,
            "Refitting: partial search never exceeds the original shared ceilings and any retained smaller curve still certifies against the master");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); bool stopped = false;
        try { AnimationOptimization.Optimize(clip, ["detail"], settings, cancellation.Token); } catch (OperationCanceledException) { stopped = true; }
        check(stopped && original.Keys.Length == 17 && original.Keys.All(k => Bits(k.InTangent, .98f) && Bits(k.OutTangent, .98f)),
            "Refitting: cancellation publishes no result and leaves every original key/tangent untouched");
        var invalid = clip with { Tracks = [Track("detail", 3, new([new(0, 0), new(1, float.PositiveInfinity)]))] };
        bool rejected = false; try { AnimationOptimization.Optimize(invalid, ["detail"], settings); } catch (ArgumentException) { rejected = true; }
        check(rejected, "Refitting: nonfinite authored values reject before fitting or publication");
    }
}
