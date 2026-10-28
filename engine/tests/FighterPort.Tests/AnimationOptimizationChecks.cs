using FighterPort.Core;
using System.Numerics;
using System.Text.Json;

internal static class AnimationOptimizationChecks
{
    public static void Run(Action<bool, string> check)
    {
        NativeFractionalWireChecks.Run(check);
        ExactPolynomials(check);
        NativeStructure(check);
        HighQualityBounds(check);
        ProtectionAndEligibility(check);
        LimitsAndDeterminism(check);
        AnimationConstantAndCertificateChecks.Run(check);
        ScaleOptimizationBoundaryChecks.Run(check);
        SmallFileOptimizationChecks.Run(check);
        SpanRefittingChecks.Run(check);
    }

    static AnimationClip Clip(params BoneTrack[] tracks) => new(Guid.NewGuid(), "Independent native polynomial", 9, new(60000, 1001), false, tracks);
    static BoneTrack Track(string bone, int channel, Curve curve)
    {
        var channels = new Curve?[9]; channels[channel] = curve; return new(bone, channels);
    }
    static AnimationOptimizationSettings Quality(double length = 1) => new() { Mode = AnimationOptimizationMode.HighQuality, BindBoundsDiagonal = length };
    static Curve Reduced(AnimationOptimizationResult result, int channel, string bone = "detail") => result.Tracks.Single(t => t.Bone == bone).Channels[channel]!;
    static bool SameBits(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);

    static void ExactPolynomials(Action<bool, string> check)
    {
        // Expanded independent polynomial: p(t)=3+2t-t^2+t^3,
        // p'(t)=2-2t+3t^2. Every datum is exactly representable here.
        double P(double t) => 3 + 2 * t - t * t + t * t * t;
        double D(double t) => 2 - 2 * t + 3 * t * t;
        var curve = new Curve(Enumerable.Range(0, 9).Select(i => new Key(i, (float)P(i), (float)D(i), (float)D(i))).ToArray());
        var baseline = Clip(Track("detail", 6, curve)); string before = JsonSerializer.Serialize(baseline, ProjectStore.Json);
        var result = AnimationOptimization.Optimize(baseline, new[] { "detail" }); var output = Reduced(result, 6);
        check(output.Keys.Length == 2 && ReferenceEquals(output.Keys[0], curve.Keys[0]) && ReferenceEquals(output.Keys[1], curve.Keys[^1]),
            "Exact optimization removes seven cubic subdivision knots while retaining authentic endpoint objects/tangents");
        check(result.Report.Channels.Single().CertifiedValueErrorBound == 0 && result.Report.Channels.Single().CertifiedSpeedErrorBound == 0 && result.Report.ReducedMatchedKeys == 7,
            "Independent expanded cubic passes exact rational value and derivative identity with a separate reduction receipt");
        double error = Enumerable.Range(0, 513).Max(i => Math.Abs(output.Sample(8 * (i + .137) / 513) - P(8 * (i + .137) / 513)));
        check(error < .0002 && before == JsonSerializer.Serialize(baseline, ProjectStore.Json),
            "Withheld independent cubic samples quantify native float roundoff while the input clip stays immutable");

        var constant = new Curve(Enumerable.Range(0, 9).Select(i => new Key(i, 7.25f)).ToArray(), LoopFrameLimit: 10);
        var fixedResult = AnimationOptimization.Optimize(Clip(Track("detail", 0, constant)), new[] { "detail" }); var fixedCurve = Reduced(fixedResult, 0);
        check(fixedCurve.Keys.Length == 1 && ReferenceEquals(fixedCurve.Keys[0], constant.Keys[0]) && fixedCurve.LoopFrameLimit == 10 && fixedCurve.Sample(9.73) == 7.25f,
            "Useful nonzero Hermite constants become one fixed native key without dropping the authentic loop limit");
        foreach (bool looped in new[] { false, true })
        {
            var shifted = new Curve(Enumerable.Range(1, 5).Select(i => new Key(i, 7.25f)).ToArray(), LoopFrameLimit: looped ? 8 : null);
            var shiftedResult = AnimationOptimization.Optimize(Clip(Track("detail", 0, shifted)) with { Loop = looped }, new[] { "detail" });
            var shiftedCurve = Reduced(shiftedResult, 0);
            check(shiftedCurve.Keys.Length == 1 && shiftedCurve.Keys[0].Frame == 0 && shiftedCurve.Keys[0].Value == shifted.Keys[0].Value
                && shiftedCurve.LoopFrameLimit == shifted.LoopFrameLimit && new[] { 0d, .371, 1, 3.137, 5, 7.733 }.All(f => shiftedCurve.Sample(f) == 7.25f),
                "A proved shifted constant becomes one native frame-zero key without changing its held or looping value / loop=" + looped);
        }

        var spin = new Curve(Enumerable.Range(0, 9).Select(i => new Key(i, 90 * i, 90, 90)).ToArray());
        var turnResult = AnimationOptimization.Optimize(Clip(Track("detail", 5, spin)), new[] { "detail" }); var turns = Reduced(turnResult, 5);
        check(turns.Keys.Length == 2 && turns.Keys[^1].Value == 720 && Math.Abs(turns.Sample(3.137) - 90 * 3.137) < .0001,
            "Exact reduction retains two authored complete Euler turns at independently chosen fractional times");

        var near = new Curve([new(0, 1), new(1, 1.000001f), new(2, 1)]);
        var retained = AnimationOptimization.Optimize(Clip(Track("detail", 6, near)), new[] { "detail" });
        check(ReferenceEquals(Reduced(retained, 6), near) && retained.Report.AcceptedMerges == 0,
            "Exact mode retains an arbitrarily small intentional pulse instead of rounding it into a constant");
    }

    static void NativeStructure(Action<bool, string> check)
    {
        var pair = new Curve([new(0, 0, 1, 1), new(1, 1, 1, 1), new(2, 2, 1, 1), new(2, 10, 1, 1), new(3, 11, 1, 1), new(4, 12, 1, 1)]);
        var result = AnimationOptimization.Optimize(Clip(Track("detail", 6, pair)), new[] { "detail" }); var output = Reduced(result, 6);
        check(output.Keys.Length == 4 && output.Keys.Where(k => k.Frame == 2).SequenceEqual(pair.Keys.Where(k => k.Frame == 2)) && output.Sample(2) == 2 && Math.Abs(output.Sample(2.0001) - 10.0001) < .00001,
            "Paired incoming/outgoing values and derivative sides survive exact reduction on both neighboring segments");
        check(result.Report.ProbeFrames.Contains(2) && result.Report.ProbeFrames.Contains((double)MathF.BitDecrement(2)) && result.Report.ProbeFrames.Contains((double)MathF.BitIncrement(2)),
            "The native validation probe report includes exact and native-float-adjacent paired boundary addresses");

        float minusZero = BitConverter.Int32BitsToSingle(int.MinValue);
        var signed = new Curve([new(0, 4), new(1, 4, minusZero, minusZero), new(2, 4)]);
        var signedResult = AnimationOptimization.Optimize(Clip(Track("detail", 6, signed)), new[] { "detail" });
        check(ReferenceEquals(Reduced(signedResult, 6), signed) && SameBits(Reduced(signedResult, 6).Keys[1].OutTangent, minusZero),
            "Signed-zero native key/tangent payload remains structural evidence rather than disappearing in rational arithmetic");
        var split = new Curve([new(0, 0), new(1, 1, 2, -2), new(2, 0)]);
        var splitResult = AnimationOptimization.Optimize(Clip(Track("detail", 6, split)), new[] { "detail" });
        check(ReferenceEquals(Reduced(splitResult, 6), split), "Distinct incoming/outgoing tangents at a unique native time remain mandatory");

        var loop = new Curve([new(1, 1, 1, 1), new(2, 2, 1, 1), new(3, 3, 1, 1)], LoopFrameLimit: 6);
        var loopResult = AnimationOptimization.Optimize(Clip(Track("detail", 6, loop)) with { Loop = true }, new[] { "detail" }); var reducedLoop = Reduced(loopResult, 6);
        check(reducedLoop.Keys.Length == 2 && reducedLoop.LoopFrameLimit == 6 && new[] { 0, .173, 3, 3.127, 4.219, 5.713, 6d }.All(f => SameBits(loop.Sample(f), reducedLoop.Sample(f))),
            "Keeping authentic first/last keys leaves the actual native wrap bridge unchanged before and after the keyed interval");

        var fractional = new Curve([new(0, 0, 1, 1), new(.5, .5f, 1, 1), new(1, 1, 1, 1)]);
        var held = fractional with { Interpolation = Interpolation.Step };
        var a = AnimationOptimization.Optimize(Clip(Track("detail", 6, fractional)), new[] { "detail" });
        var b = AnimationOptimization.Optimize(Clip(Track("detail", 6, held)), new[] { "detail" });
        var nonNative = new Curve([new(0, 0, 1, 1), new(.1, .1f, 1, 1), new(1, 1, 1, 1)]);
        var c = AnimationOptimization.Optimize(Clip(Track("detail", 6, nonNative)), new[] { "detail" });
        check(Reduced(a, 6).Keys.Length == 2 && a.Report.Channels.Single().CertifiedValueErrorBound == 0 &&
            ReferenceEquals(Reduced(b, 6), held) && ReferenceEquals(Reduced(c, 6), nonNative),
            "Exactly representable native fractional times receive rational cleanup; held and non-native double times remain unchanged without conversion");
    }

    static void HighQualityBounds(Action<bool, string> check)
    {
        var pulse = new Curve([new(0, 0), new(1, .00004f), new(2, 0)]);
        var baseline = Clip(Track("detail", 6, pulse)); var result = AnimationOptimization.Optimize(baseline, new[] { "detail" }, Quality());
        check(Reduced(result, 6).Keys.Length == 2 && result.Report.Channels.Single().CertifiedValueErrorBound <= .0001 && result.Report.Channels.Single().CertifiedSpeedErrorBound <= .0001,
            "High-quality translation reduction certifies both value and per-frame speed against the immutable native pulse");
        double maximum = Enumerable.Range(0, 2049).Max(i => Math.Abs(pulse.Sample(i / 1024d) - Reduced(result, 6).Sample(i / 1024d)));
        check(maximum <= result.Report.Channels.Single().CertifiedValueErrorBound + 1e-11,
            "An independent dense sample lattice lies below the rational Bernstein translation certificate");
        var tiny = new Curve([new(0, 0), new(1, 5e-41f), new(2, 0)]);
        var tinyResult = AnimationOptimization.Optimize(Clip(Track("detail", 6, tiny)), new[] { "detail" }, Quality(1e-36));
        check(Reduced(tinyResult, 6).Keys.Length == 2 && tinyResult.Report.Channels.Single().CertifiedValueErrorBound is > 0 and < 1e-40,
            "Subnormal native float motion receives a positive conservative certificate without a subnormal iteration walk");
        var large = new Curve([new(0, 0), new(1, .001f), new(2, 0)]);
        var capped = AnimationOptimization.Optimize(Clip(Track("detail", 6, large)), new[] { "detail" }, Quality(100));
        check(ReferenceEquals(Reduced(capped, 6), large) && capped.Report.Channels.Single().ValueBound == .0002,
            "The absolute .0002 native-unit cap remains stricter than proportional translation tolerance on a large target");

        var scale = new Curve([new(0, 1), new(1, 1.000004f), new(2, 1)]);
        var scaleResult = AnimationOptimization.Optimize(Clip(Track("detail", 0, scale)), new[] { "detail" }, Quality());
        check(Reduced(scaleResult, 0).Keys.Length == 2 && scaleResult.Report.Channels.Single().ValueBound == 1e-5,
            "Dimensionless scale uses its independent declared value and speed bounds");

        // With zero values, the left cubic is m*(u^3-u^2). Its
        // independent peak magnitude is 4*m/27 at u=2/3, not a keyed value.
        var hidden = new Curve([new(0, 0), new(1, 0, .06f, .06f), new(2, 0)]);
        var settings = Quality() with { EulerValueBoundDegrees = .009, EulerSpeedBoundDegreesPerFrame = .1 };
        var accepted = AnimationOptimization.Optimize(Clip(Track("detail", 3, hidden)), new[] { "detail" }, settings);
        var rejected = AnimationOptimization.Optimize(Clip(Track("detail", 3, hidden)), new[] { "detail" }, settings with { EulerValueBoundDegrees = .008 });
        var unsubdivided = AnimationOptimization.Optimize(Clip(Track("detail", 3, hidden)), new[] { "detail" }, settings with { MaximumSubdivisionDepth = 0 });
        check(Reduced(accepted, 3).Keys.Length == 2 && accepted.Report.Channels.Single().CertifiedValueErrorBound >= 4 * (double).06f / 27
            && ReferenceEquals(Reduced(rejected, 3), hidden) && ReferenceEquals(Reduced(unsubdivided, 3), hidden),
            "Rational subdivision accepts the independently derived 4m/27 overshoot only within its real bound; uncertain hulls retain keys");
        var speed = AnimationOptimization.Optimize(Clip(Track("detail", 3, hidden)), new[] { "detail" }, settings with { EulerSpeedBoundDegreesPerFrame = .05 });
        check(ReferenceEquals(Reduced(speed, 3), hidden), "Derivative error independently blocks a value-sufficient Euler simplification");

        var winding = new Curve([new(0, 0), new(1, 360), new(2, 0)]);
        var windingResult = AnimationOptimization.Optimize(Clip(Track("detail", 5, winding)), new[] { "detail" }, Quality());
        check(ReferenceEquals(Reduced(windingResult, 5), winding), "Equivalent endpoint orientations cannot conceal an authored 360-degree intermediate Euler turn");

        var dense = new Curve(Enumerable.Range(0, 25).Select(i => new Key(i, i + (i % 2 == 0 ? .003f : -.003f), 1, 1)).ToArray());
        var cumulative = AnimationOptimization.Optimize(Clip(Track("detail", 3, dense)) with { SampleCount = 25 }, new[] { "detail" }, Quality());
        double accumulated = Enumerable.Range(0, 8193).Max(i => Math.Abs(dense.Sample(24 * i / 8192d) - Reduced(cumulative, 3).Sample(24 * i / 8192d)));
        check(Reduced(cumulative, 3).Keys.Length < dense.Keys.Length && accumulated < .01001,
            "Repeated span merges remain bounded against the original dense motion instead of accumulating per-removal error");
    }

    static void ProtectionAndEligibility(Action<bool, string> check)
    {
        var linear = new Curve(Enumerable.Range(0, 5).Select(i => new Key(i, 10 + i, 1, 1)).ToArray());
        var source = Clip(Track("detail", 6, linear), Track("donor-only", 3, linear), new BoneTrack("sparse", new Curve?[9]));
        var result = AnimationOptimization.Optimize(source, new[] { "detail", "sparse", "existing-without-track" });
        check(result.Tracks.Select(t => t.Bone).SequenceEqual(new[] { "detail", "sparse" }) && result.RemovedUnmatchedTracks.SequenceEqual(new[] { "donor-only" })
            && result.Report.RemovedUnmatchedKeys == 5 && result.Tracks[1].Channels.All(c => c is null) && result.Tracks.All(t => t.Bone != "existing-without-track"),
            "Every exact target track is eligible while absent-target tracks are reported/removed and sparse bind fallback is never authored");

        var protectedResult = AnimationOptimization.Optimize(source, new[] { "detail", "donor-only", "sparse" }, new()
        {
            PreserveStructureBones = ["donor-only"], ChannelRules = [new("detail", 6) { MandatoryFrames = [2] }]
        });
        check(ReferenceEquals(Reduced(protectedResult, 3, "donor-only"), linear) && Reduced(protectedResult, 6).Keys.Select(k => k.Frame).SequenceEqual(new double[] { 0, 2, 4 }),
            "Protected native structures stay object-identical and mandatory fit endpoints/joins retain their original knots");
        var ranged = AnimationOptimization.Optimize(Clip(Track("detail", 6, linear)), new[] { "detail" }, new()
        {
            ProtectedRanges = [new(1.5, 2.5, "Independent protected hold")]
        });
        check(ReferenceEquals(Reduced(ranged, 6), linear), "Protected continuous ranges prevent any merged span from changing their original segment structure");
        var missingJoin = AnimationOptimization.Optimize(Clip(Track("detail", 6, linear)), new[] { "detail" }, new() { MandatoryFrames = [.5] });
        check(Reduced(missingJoin, 6).Keys.Any(k => k.Frame == 1) && Reduced(missingJoin, 6).Keys.All(k => k.Frame == Math.Truncate(k.Frame)),
            "A protected reference inside a segment retains its surrounding original segment without manufacturing fractional native knots");

        var pulse = new Curve([new(0, 0), new(1, .00004f), new(2, 0)]);
        var exactOnly = AnimationOptimization.Optimize(Clip(Track("detail", 6, pulse)), new[] { "detail" }, Quality() with { ExactOnlyBones = ["detail"] });
        check(ReferenceEquals(Reduced(exactOnly, 6), pulse), "Source-owned control or contact dependency rules can require exact curves inside high-quality mode");
        var singularChannels = new Curve?[9]; singularChannels[0] = new([new(0, 1), new(1, 0), new(2, 1)]); singularChannels[6] = pulse;
        var singular = AnimationOptimization.Optimize(Clip(new BoneTrack("detail", singularChannels)), new[] { "detail" }, Quality());
        check(ReferenceEquals(Reduced(singular, 6), pulse) && singular.Report.Channels.All(c => c.Policy.StartsWith("Exact", StringComparison.Ordinal)),
            "Potentially singular animated scale makes every channel of that native bone exact-only");
        var mirroredChannels = new Curve?[9]; mirroredChannels[0] = new([new(0, -1)]); mirroredChannels[6] = pulse;
        var mirrored = AnimationOptimization.Optimize(Clip(new BoneTrack("detail", mirroredChannels)), new[] { "detail" }, Quality());
        check(Reduced(mirrored, 6).Keys.Length == 2 && Reduced(mirrored, 0).Keys[0].Value == -1,
            "A stable mirrored scale is preserved without applying an unrelated positive-scale reconstruction restriction");
    }

    static void LimitsAndDeterminism(Action<bool, string> check)
    {
        var linear = new Curve(Enumerable.Range(0, 17).Select(i => new Key(i, i, 1, 1)).ToArray());
        var source = Clip(Track("detail", 6, linear));
        var zero = AnimationOptimization.Optimize(source, new[] { "detail" }, new() { MaximumWork = 0 });
        var candidates = AnimationOptimization.Optimize(source, new[] { "detail" }, new() { MaximumCandidateSpans = 0 });
        check(zero.Report.WorkLimitReached && zero.Report.Work == 0 && ReferenceEquals(Reduced(zero, 6), linear)
            && candidates.Report.CandidateLimitReached && ReferenceEquals(Reduced(candidates, 6), linear),
            "Zero proof/candidate budgets return an explicitly unfinished unchanged curve instead of accepting untested spans");
        var first = AnimationOptimization.Optimize(source, new[] { "detail" }); var second = AnimationOptimization.Optimize(source, new[] { "detail" });
        check(Reduced(first, 6).Keys.SequenceEqual(Reduced(second, 6).Keys) && JsonSerializer.Serialize(first.Report) == JsonSerializer.Serialize(second.Report),
            "Unchanged frozen inputs produce identical deterministic native reductions and work/probe receipts");
        var other = Track("unweighted-helper", 3, linear);
        var ordered = AnimationOptimization.Optimize(Clip(source.Tracks[0], other), new[] { "detail", "unweighted-helper" });
        var permuted = AnimationOptimization.Optimize(Clip(other, source.Tracks[0]), new[] { "unweighted-helper", "detail" });
        check(ordered.Tracks.All(t => t.Channels.OfType<Curve>().All(c => c.Keys.Length == 2))
            && ordered.Tracks.All(t => t.Channels.Single(c => c is not null)!.Keys.SequenceEqual(permuted.Tracks.Single(p => p.Bone == t.Bone).Channels.Single(c => c is not null)!.Keys)),
            "Storage permutation leaves detailed/helper bone reductions identical without requiring geometry or bone-name heuristics");
        var renamed = AnimationOptimization.Optimize(source with { Tracks = [source.Tracks[0] with { Bone = "unfamiliar-native-identity" }] }, new[] { "unfamiliar-native-identity" });
        check(renamed.Tracks[0].Channels[6]!.Keys.SequenceEqual(Reduced(first, 6).Keys), "Rename changes no polynomial or native output coefficient");
        var limited = AnimationOptimization.Optimize(source, new[] { "detail" }, new() { MaximumProbeFrames = 3 });
        check(limited.Report.ProbeLimitReached && limited.Report.ProbeFrames.Length == 3, "Truncated native-validation probe coverage is explicit and never presented as complete");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); bool cancelled = false;
        try { AnimationOptimization.Optimize(source, new[] { "detail" }, cancellation: cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
        check(cancelled && source.Tracks[0].Channels[6]!.Keys.Length == 17, "Cancellation leaves the frozen source curves unchanged");
        bool invalid = false;
        try { AnimationOptimization.Optimize(source, new[] { "detail" }, Quality(0)); } catch (ArgumentException) { invalid = true; }
        check(invalid, "High-quality mode rejects a missing/degenerate authentic bind scale rather than inventing a global unit tolerance");
        bool underflow = false;
        try { AnimationOptimization.Optimize(source, new[] { "detail" }, Quality(double.Epsilon)); } catch (ArgumentException) { underflow = true; }
        check(underflow, "Underflowed proportional tolerances reject before a zero-bound high-quality candidate can be scored");
        bool malformed = false;
        try { AnimationOptimization.Optimize(Clip(Track("detail", 6, new([new(0, float.NaN)]))), new[] { "detail" }); } catch (ArgumentException) { malformed = true; }
        check(malformed, "Nonfinite native coefficients fail before candidate publication");
    }
}
