using BrawlCrate.Studio.Retargeting;
using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;

internal static partial class Program
{
    private static void RotationSpanPrecisionChecks()
    {
        var fractions = Enumerable.Range(1, 15).Select(i => i / 16d).ToArray();
        var first = new[] { new[] { 743d, 89.98, -33d }, new[] { -17d, 44d, 21d }, new[] { 61d, -38d, -22d } };
        var last = new[] { new[] { 775d, 91.3, -8d }, new[] { 24d, 27d, 43d }, new[] { 77d, -19d, 16d } };
        var truth = new[] { 34d, 25, 1.4, 1.1, 24, 26, 42, 36, -18, -12, 23, 20, 18, 14, 21, 17, 40, 36 };
        var seed = truth.Select((value, i) => value + (i % 2 == 0 ? 8 : -7)).ToArray();
        var parents = fractions.Select(t => Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1, -2, 3)), (float)(t * .31)))).ToArray();
        var goals = fractions.Select((t, i) => SpanIndependentWorld(first, last, truth, parents[i], t, false)).ToArray();
        var saved = seed.ToArray(); var firstSaved = first.Select(v => v.ToArray()).ToArray();
        var fit = RotationSpanFitter.Solve(first, last, seed, parents, goals, fractions, CancellationToken.None, maximumIterations: 32);
        Check(fit.HasImprovement && fit.MaximumDegrees < .01 && fit.MaximumDegrees < fit.InitialMaximumDegrees,
            "Analytic SO3 minimax fitting improves a coupled three-bone native interval across a pole and full-turn branch without changing endpoint values or adding knots",
            new { fit.InitialMaximumDegrees, fit.MaximumDegrees, fit.Iterations, fit.Evaluations });
        Check(seed.SequenceEqual(saved) && first.Zip(firstSaved, (a, b) => a.SequenceEqual(b)).All(v => v) && fit.Parameters.Length == seed.Length,
            "Rotation span fitting preserves the caller's coefficient arrays and fixed native key membership");
        double withheld = 0;
        for (int i = 1; i < 256; i++)
        {
            double t = i / 256d;
            var parent = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1, -2, 3)), (float)(t * .31)));
            var goal = SpanIndependentWorld(first, last, truth, parent, t, false);
            var native = SpanIndependentWorld(first, last, fit.Parameters, parent, t, true);
            withheld = Math.Max(withheld, SpanAngle(goal, native));
        }
        Check(withheld < .03, "Independent withheld native-float FK samples retain the procedural coupled orientation inside the fractional bound", new { withheld });
        Check(fit.Evaluations <= 512 && fit.FeasibilityChecks <= 64 && fit.Iterations <= 32,
            "SO3 span search retains finite evaluation, geometry-query and iteration budgets");
        var chainGoals = fractions.Select((t, i) => Enumerable.Range(1, first.Length)
            .Select(count => SpanIndependentWorld(first.Take(count).ToArray(), last.Take(count).ToArray(), truth, parents[i], t, false)).ToArray()).ToArray();
        var whole = RotationSpanFitter.Solve(first, last, seed, parents, chainGoals, fractions, CancellationToken.None);
        Check(whole.HasImprovement && whole.MaximumDegrees < .01 && whole.FinalFeasible,
            "All-chain minimax includes every changed native joint rather than hiding parent rotation error behind an accurate endpoint",
            new { whole.InitialMaximumDegrees, whole.MaximumDegrees, whole.Work, whole.Iterations });
        var exhausted = RotationSpanFitter.Solve(first, last, seed, parents, goals, fractions, CancellationToken.None, maximumWork: 1);
        Check(exhausted.WorkLimitReached && !exhausted.HasImprovement && !exhausted.Converged && exhausted.Parameters.SequenceEqual(seed) && exhausted.Work <= 1,
            "Uncertain or exhausted scalar projection work returns unchanged coefficients without a convergence claim");

        // Direct finite differences independently differentiate the complete
        // quaternion composition and Log residual, not the analytic Jacobian.
        var helper = typeof(RotationSpanFitter);
        var quaternion = helper.GetNestedType("DQuaternion", BindingFlags.NonPublic);
        var from = quaternion.GetMethod("From", BindingFlags.NonPublic | BindingFlags.Static);
        Array ParentArray(Quaternion[] values)
        {
            var result = Array.CreateInstance(quaternion, values.Length);
            for (int i = 0; i < values.Length; i++) result.SetValue(from.Invoke(null, new object[] { values[i] }), i);
            return result;
        }
        var evaluate = helper.GetMethod("Evaluate", BindingFlags.NonPublic | BindingFlags.Static);
        object Evaluation(double[] values, bool jacobian) => evaluate.Invoke(null, new object[] { first, last, values, ParentArray(parents), ParentArray(goals), fractions, jacobian, CancellationToken.None, null });
        var linear = Evaluation(seed, true);
        var jac = (double[][][])linear.GetType().GetField("Jacobian", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(linear);
        double derivativeError = 0;
        for (int variable = 0; variable < seed.Length; variable++)
        {
            const double step = .001;
            var plus = seed.ToArray(); plus[variable] += step; var minus = seed.ToArray(); minus[variable] -= step;
            var a = Evaluation(plus, false); var b = Evaluation(minus, false);
            var ar = (double[][])a.GetType().GetField("Residual", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(a);
            var br = (double[][])b.GetType().GetField("Residual", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(b);
            for (int sample = 0; sample < fractions.Length; sample++) for (int component = 0; component < 3; component++)
                derivativeError = Math.Max(derivativeError, Math.Abs(jac[sample][variable][component] - (ar[sample][component] - br[sample][component]) / (2 * step)));
        }
        Check(derivativeError < 1e-8, "Analytic native-Euler tangent Jacobian agrees with independent central differentiation through all coupled parent rotations and SO3 Log", new { derivativeError });

        int checks = 0;
        var rejected = RotationSpanFitter.Solve(first, last, seed, parents, goals, fractions, CancellationToken.None,
            feasible: candidate => { checks++; return false; }, maximumIterations: 2);
        Check(!rejected.HasImprovement && rejected.Parameters.SequenceEqual(seed) && checks > 0 && checks <= 64,
            "Hard native geometry rejection returns the original interval rather than trading its constraints for lower rotation error");
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel(); bool stopped = false;
            try { RotationSpanFitter.Solve(first, last, seed, parents, goals, fractions, cancellation.Token); }
            catch (OperationCanceledException) { stopped = true; }
            Check(stopped && seed.SequenceEqual(saved), "Cancelling SO3 fitting does not publish changed coefficients");
        }
        var turnFirst = new[] { new[] { 0d, 0d, 0d } }; var turnLast = new[] { new[] { 720d, 0d, 0d } };
        var turn = new[] { 720d, 720d, 0d, 0d, 0d, 0d };
        var turnParents = fractions.Select(_ => Quaternion.Identity).ToArray();
        var turnGoals = fractions.Select(t => SpanIndependentWorld(turnFirst, turnLast, turn, Quaternion.Identity, t, false)).ToArray();
        var preserved = RotationSpanFitter.Solve(turnFirst, turnLast, turn, turnParents, turnGoals, fractions, CancellationToken.None);
        Check(!preserved.HasImprovement && preserved.Parameters.SequenceEqual(turn),
            "An already accurate native 720-degree interval keeps its authored unwrapped scalar path");
        var invalidGeometry = RotationSpanFitter.Solve(turnFirst, turnLast, turn, turnParents, turnGoals, fractions, CancellationToken.None, feasible: _ => false);
        Check(!invalidGeometry.Converged && !invalidGeometry.FinalFeasible && !invalidGeometry.HasImprovement,
            "An accurate orientation alone cannot report convergence when the native geometry callback rejects its initial state");
        bool invalid = false;
        try
        {
            var huge = new[] { new[] { 1.7e308, 0d, 0d } };
            RotationSpanFitter.Solve(huge, huge, new[] { 1.7e308, -1.7e308, 0d, 0d, 0d, 0d },
                new[] { Quaternion.Identity }, new[] { Quaternion.Identity }, new[] { .5 }, CancellationToken.None);
        }
        catch (ArgumentException) { invalid = true; }
        Check(invalid, "Overflowing finite Hermite inputs are rejected explicitly rather than passing invalid SO3 measurements");
    }

    private static Quaternion SpanIndependentWorld(double[][] first, double[][] last, double[] tangents,
        Quaternion parent, double t, bool native)
    {
        double h0 = 2 * t * t * t - 3 * t * t + 1, h1 = 1 - h0, a = t * t * t - 2 * t * t + t, b = t * t * t - t * t;
        var world = parent;
        for (int bone = 0; bone < first.Length; bone++)
        {
            var euler = Enumerable.Range(0, 3).Select(axis => h0 * first[bone][axis] + h1 * last[bone][axis] +
                a * tangents[bone * 6 + axis * 2] + b * tangents[bone * 6 + axis * 2 + 1]).ToArray();
            Quaternion local;
            if (native) local = BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3((float)euler[0], (float)euler[1], (float)euler[2]));
            else
            {
                double x = euler[0] * Math.PI / 360, y = euler[1] * Math.PI / 360, z = euler[2] * Math.PI / 360;
                double sx = Math.Sin(x), cx = Math.Cos(x), sy = Math.Sin(y), cy = Math.Cos(y), sz = Math.Sin(z), cz = Math.Cos(z);
                local = new Quaternion((float)(sx * cy * cz - cx * sy * sz), (float)(cx * sy * cz + sx * cy * sz),
                    (float)(cx * cy * sz - sx * sy * cz), (float)(cx * cy * cz + sx * sy * sz));
            }
            world = Quaternion.Normalize(world * local);
        }
        return world;
    }

    private static double SpanAngle(Quaternion a, Quaternion b)
    {
        var delta = Quaternion.Normalize(Quaternion.Inverse(a) * b);
        return 2 * Math.Atan2(Math.Sqrt((double)delta.X * delta.X + (double)delta.Y * delta.Y + (double)delta.Z * delta.Z), Math.Abs(delta.W)) * 180 / Math.PI;
    }
}
