using FighterPort.Core;

internal static class ScaleOptimizationBoundaryChecks
{
    public static void Run(Action<bool, string> check)
    {
        AnimationClip Clip(Curve scale)
        {
            var channels = new Curve?[9]; channels[0] = scale;
            return new(Guid.NewGuid(), "Independent scale sign boundary", 3, new(60,1), false, [new("arbitrary scale identity", channels)]);
        }
        BoneTrack Track(Curve scale) => Clip(scale).Tracks[0];
        var quality = new AnimationOptimizationSettings { Mode = AnimationOptimizationMode.HighQuality, BindBoundsDiagonal = 1 };
        float e = MathF.ScaleB(1f, -20), slope = 2.5f * e;
        var original = new Curve([new(0,e,-slope,-slope), new(1,e,0,0), new(2,e,slope,slope)]);
        var changed = new Curve([original.Keys[0], original.Keys[2]]);
        // Each original interval has positive Bernstein controls; its minimum
        // control is e-slope/3=e/6. The two-frame candidate is independently
        // p(t)=e-slope*t+(slope/2)*t^2, with p(1)=-e/4.
        check(e - slope / 3d > 0 && (double)e - slope / 2d == -(double)e / 4,
            "Scale boundary: independent positive source hull and negative longer-span midpoint establish the counterexample");
        var output = AnimationOptimization.Optimize(Clip(original), ["arbitrary scale identity"], quality);
        check(output.Tracks.Single().Channels[0]!.Keys.SequenceEqual(original.Keys) && output.Report.AcceptedMerges == 0,
            "Scale boundary: HQ retains the middle positive key instead of introducing a new zero crossing within scalar tolerances");
        check(!AnimationOptimization.CertifyCandidate(Clip(original), [Track(changed)], quality).Accepted,
            "Scale boundary: decoded candidate certification rejects a newly singular scale despite sub-tolerance coefficient errors");

        var negative = new Curve(original.Keys.Select(k => new Key(k.Frame,-k.Value,
            k.InTangent == 0 ? 0 : -k.InTangent, k.OutTangent == 0 ? 0 : -k.OutTangent)).ToArray());
        var negativeCandidate = new Curve([negative.Keys[0],negative.Keys[2]]);
        var negativeOutput = AnimationOptimization.Optimize(Clip(negative), ["arbitrary scale identity"], quality);
        check(negativeOutput.Tracks.Single().Channels[0]!.Keys.SequenceEqual(negative.Keys) &&
            !AnimationOptimization.CertifyCandidate(Clip(negative), [Track(negativeCandidate)], quality).Accepted,
            "Scale boundary: mirrored negative scale retains its sign instead of acquiring a positive lobe");

        var fixedNegative = new Curve([new(0,-2f),new(1,-2f),new(2,-2f)]);
        var fixedOutput = AnimationOptimization.Optimize(Clip(fixedNegative), ["arbitrary scale identity"], quality).Tracks.Single().Channels[0]!;
        check(fixedOutput.Keys.Length == 1 && fixedOutput.Keys[0].Frame == 0 && fixedOutput.Keys[0].Value == -2f,
            "Scale boundary: ordinary fixed negative scale still receives exact constant storage without normalization");
        var positive = new Curve([new(0,2f,1f,1f),new(1,3f,1f,1f),new(2,4f,1f,1f)]);
        var positiveOutput = AnimationOptimization.Optimize(Clip(positive), ["arbitrary scale identity"], quality).Tracks.Single().Channels[0]!;
        check(positiveOutput.Keys.Length == 2 && positiveOutput.Sample(.731) > 0 && positiveOutput.Sample(1.619) > 0,
            "Scale boundary: a strictly positive equivalent span remains reducible under unchanged quality limits");

        var crossing = new Curve([new(0,1f,-1f,-1f),new(1,0f,-1f,-1f),new(2,-1f,-1f,-1f)]);
        var crossingOutput = AnimationOptimization.Optimize(Clip(crossing), ["arbitrary scale identity"], quality);
        check(crossingOutput.Report.Channels.Single().Policy.StartsWith("Exact",StringComparison.Ordinal) &&
            crossingOutput.Report.Channels.Single().CertifiedValueErrorBound == 0 &&
            Math.Abs(crossingOutput.Tracks.Single().Channels[0]!.Sample(.371) - crossing.Sample(.371)) < 1e-6 &&
            Math.Abs(crossingOutput.Tracks.Single().Channels[0]!.Sample(1.733) - crossing.Sample(1.733)) < 1e-6,
            "Scale boundary: an originally sign-changing scale remains exact-only and is not normalized or smoothed");

        // Exact polynomial identity is still allowed if a coarser Bernstein
        // hull merely becomes uncertain. This positive parabola has original
        // subdivision controls above zero, but endpoint controls below zero.
        var uncertain = new Curve([new(0,1.125f,-2f,-2f),new(1,.125f,0,0),new(2,1.125f,2f,2f)]);
        var exact = AnimationOptimization.Optimize(Clip(uncertain), ["arbitrary scale identity"], quality with { Mode = AnimationOptimizationMode.Exact });
        var exactCurve = exact.Tracks.Single().Channels[0]!;
        check(exactCurve.Keys.Length == 2 && exact.Report.Channels.Single().Policy.StartsWith("Exact",StringComparison.Ordinal) &&
            Math.Abs(exactCurve.Sample(.371)-uncertain.Sample(.371)) < 1e-6 && Math.Abs(exactCurve.Sample(1.733)-uncertain.Sample(1.733)) < 1e-6,
            "Scale boundary: Exact mode still reduces a positive polynomial identity even when its merged hull is conservative");
    }
}
