using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    internal sealed class RotationSpanFit
    {
        internal double[] Parameters { get; set; }
        internal double InitialMaximumDegrees { get; set; }
        internal double MaximumDegrees { get; set; }
        internal bool HasImprovement { get; set; }
        internal bool Converged { get; set; }
        internal int Iterations { get; set; }
        internal int Evaluations { get; set; }
        internal int FeasibilityChecks { get; set; }
        internal long Work { get; set; }
        internal bool WorkLimitReached { get; set; }
        internal bool InitialFeasible { get; set; }
        internal bool FinalFeasible { get; set; }
    }

    // Fits the representation of a solved pose, not a different pose solver.
    // Values, times, native membership and the choice of editable bones belong
    // to the caller. There are six slope variables per chain bone, ordered
    // [X-out, X-in, Y-out, Y-in, Z-out, Z-in]. No keys are created here.
    // This is one adjacent native-frame interval: fractions are normalized to
    // (0,1), and slope parameters use degrees per native frame.
    internal static class RotationSpanFitter
    {
        private const double Radians = Math.PI / 180;
        private const int MaximumEvaluations = 512, MaximumFeasibilityChecks = 64;

        internal static RotationSpanFit Solve(double[][] firstEuler, double[][] lastEuler,
            double[] initialTangents, Quaternion[] fixedParentWorld, Quaternion[] desiredEndWorld,
            double[] fractions, CancellationToken cancellation, Func<double[], bool> feasible = null,
            int maximumIterations = 32, double toleranceDegrees = .005, long maximumWork = 2000000,
            Func<long, bool> reserveSharedWork = null)
            => SolveCore(firstEuler, lastEuler, initialTangents, fixedParentWorld,
                desiredEndWorld?.Select(q => new[] { q }).ToArray(), false, fractions, cancellation, feasible,
                maximumIterations, toleranceDegrees, maximumWork, reserveSharedWork);

        // Desired rotations are [sample][chain bone]. The maximum covers every
        // changed joint, so an accurate endpoint cannot hide a worse parent.
        internal static RotationSpanFit Solve(double[][] firstEuler, double[][] lastEuler,
            double[] initialTangents, Quaternion[] fixedParentWorld, Quaternion[][] desiredChainWorld,
            double[] fractions, CancellationToken cancellation, Func<double[], bool> feasible = null,
            int maximumIterations = 32, double toleranceDegrees = .005, long maximumWork = 2000000,
            Func<long, bool> reserveSharedWork = null)
            => SolveCore(firstEuler, lastEuler, initialTangents, fixedParentWorld, desiredChainWorld, true,
                fractions, cancellation, feasible, maximumIterations, toleranceDegrees, maximumWork, reserveSharedWork);

        private static RotationSpanFit SolveCore(double[][] firstEuler, double[][] lastEuler,
            double[] initialTangents, Quaternion[] fixedParentWorld, Quaternion[][] desiredWorld, bool allBones,
            double[] fractions, CancellationToken cancellation, Func<double[], bool> feasible,
            int maximumIterations, double toleranceDegrees, long maximumWork, Func<long, bool> reserveSharedWork)
        {
            if (firstEuler == null || lastEuler == null || initialTangents == null || fixedParentWorld == null ||
                desiredWorld == null || fractions == null || firstEuler.Length == 0 || firstEuler.Length > 512 ||
                lastEuler.Length != firstEuler.Length || initialTangents.Length != firstEuler.Length * 6 ||
                fractions.Length == 0 || fractions.Length > 256 || fixedParentWorld.Length != fractions.Length ||
                desiredWorld.Length != fractions.Length || maximumIterations < 1 || maximumIterations > 64 ||
                !Finite(toleranceDegrees) || toleranceDegrees <= 0 || maximumWork < 0 || maximumWork > 2000000)
                throw new ArgumentException("A rotation span needs a bounded chain, fixed XYZ endpoints and corresponding interior samples.");
            var first = firstEuler.Select(v => v?.ToArray()).ToArray();
            var last = lastEuler.Select(v => v?.ToArray()).ToArray();
            var times = fractions.ToArray(); var current = initialTangents.ToArray();
            if (first.Concat(last).Any(v => v == null || v.Length != 3 || v.Any(x => !Finite(x))) ||
                current.Any(x => !Finite(x)) || times.Any(t => !Finite(t) || t <= 0 || t >= 1))
                throw new ArgumentException("Rotation span inputs must be finite, with samples strictly between its fixed endpoints.");
            var parents = fixedParentWorld.Select(DQuaternion.From).ToArray();
            if (desiredWorld.Any(row => row == null || row.Length != (allBones ? first.Length : 1)))
                throw new ArgumentException("Desired rotation samples must identify every selected chain joint.");
            var goals = desiredWorld.Select(row => row.Select(DQuaternion.From).ToArray()).ToArray();
            var observed = allBones ? Enumerable.Range(0, first.Length).ToArray() : new[] { first.Length - 1 };
            var result = new RotationSpanFit { Parameters = current.ToArray(), InitialMaximumDegrees = double.PositiveInfinity };
            var work = new WorkBudget(maximumWork, cancellation, reserveSharedWork);
            double maximum = double.PositiveInfinity;
            try
            {
            var original = EvaluateTargets(first, last, current, parents, goals, observed, times, true, cancellation, work);
            result.Evaluations++;
            maximum = original.Maximum;
            result.InitialMaximumDegrees = maximum / Radians;
            result.MaximumDegrees = result.InitialMaximumDegrees;
            result.InitialFeasible = feasible == null;
            if (feasible != null)
            {
                result.FeasibilityChecks++; cancellation.ThrowIfCancellationRequested();
                result.InitialFeasible = feasible(current.ToArray());
            }
            if (!result.InitialFeasible) return result;
            double trust = 32;
            for (int iteration = 0; iteration < maximumIterations && result.Evaluations < MaximumEvaluations; iteration++)
            {
                cancellation.ThrowIfCancellationRequested(); result.Iterations = iteration + 1;
                if (maximum <= toleranceDegrees * Radians) break;
                var linear = iteration == 0 ? original : EvaluateTargets(first, last, current, parents, goals, observed, times, true, cancellation, work);
                if (iteration != 0) result.Evaluations++;
                bool accepted = false;
                // Seek a smaller maximum, rather than a weighted compromise
                // between arbitrary residual units. Infeasible/uncertain linear
                // models simply approach the current verified maximum.
                double target = Math.Max(toleranceDegrees * Radians, maximum * .5);
                for (int attempt = 0; attempt < 6 && !accepted && result.Evaluations < MaximumEvaluations && result.FeasibilityChecks < MaximumFeasibilityChecks - 1; attempt++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (FeasibleLinearStep(linear, target, trust, current.Length, cancellation, work, out var step))
                    {
                        for (double scale = 1; scale >= 1d / 128 && result.Evaluations < MaximumEvaluations; scale *= .5)
                        {
                            var trial = current.Select((value, i) => value + step[i] * scale).ToArray();
                            var measured = EvaluateTargets(first, last, trial, parents, goals, observed, times, false, cancellation, work);
                            result.Evaluations++;
                            if (!Finite(measured.Maximum) || measured.Maximum >= maximum - 1e-12) continue;
                            if (feasible != null)
                            {
                                if (result.FeasibilityChecks >= MaximumFeasibilityChecks - 1) break;
                                result.FeasibilityChecks++; cancellation.ThrowIfCancellationRequested();
                                if (!feasible(trial.ToArray())) continue;
                            }
                            current = trial; maximum = measured.Maximum; accepted = true;
                            if (scale == 1) trust = Math.Min(256, trust * 1.5);
                            break;
                        }
                    }
                    target = (target + maximum) * .5;
                }
                if (!accepted)
                {
                    trust *= .25;
                    if (trust < 1e-4 || result.FeasibilityChecks >= MaximumFeasibilityChecks - 1) break;
                }
            }
            }
            catch (WorkExhausted) { result.WorkLimitReached = true; }
            finally { result.Work = work.Work; }
            cancellation.ThrowIfCancellationRequested();
            result.Parameters = current.ToArray(); result.MaximumDegrees = maximum / Radians;
            result.HasImprovement = Finite(result.InitialMaximumDegrees) && maximum / Radians + 1e-10 < result.InitialMaximumDegrees;
            result.FinalFeasible = result.InitialFeasible && feasible == null;
            if (result.InitialFeasible && feasible != null)
            {
                result.FeasibilityChecks++;
                result.FinalFeasible = feasible(current.ToArray());
                if (!result.FinalFeasible)
                {
                    result.Parameters = initialTangents.ToArray(); result.MaximumDegrees = result.InitialMaximumDegrees;
                    result.HasImprovement = false;
                }
            }
            result.Converged = result.FinalFeasible && result.MaximumDegrees <= toleranceDegrees;
            return result;
        }

        private sealed class Linearization
        {
            internal double[][] Residual;
            internal double[][][] Jacobian;
            internal double Maximum;
        }

        private static Linearization Evaluate(double[][] first, double[][] last, double[] parameters,
            DQuaternion[] parents, DQuaternion[] goals, double[] times, bool jacobian, CancellationToken token, WorkBudget work = null)
            => EvaluateTargets(first, last, parameters, parents, goals.Select(q => new[] { q }).ToArray(),
                new[] { first.Length - 1 }, times, jacobian, token, work);

        private static Linearization EvaluateTargets(double[][] first, double[][] last, double[] parameters,
            DQuaternion[] parents, DQuaternion[][] goals, int[] observed, double[] times, bool jacobian, CancellationToken token, WorkBudget work)
        {
            int rows = checked(times.Length * observed.Length);
            var result = new Linearization { Residual = new double[rows][],
                Jacobian = jacobian ? new double[rows][][] : null };
            for (int sample = 0; sample < times.Length; sample++)
            {
                token.ThrowIfCancellationRequested(); double t = times[sample], t2 = t * t, t3 = t2 * t;
                double h0 = 2 * t3 - 3 * t2 + 1, h1 = -2 * t3 + 3 * t2, a = t3 - 2 * t2 + t, b = t3 - t2;
                var world = parents[sample]; var axes = jacobian ? new DVector[first.Length * 3] : null;
                var worlds = new DQuaternion[first.Length];
                for (int bone = 0; bone < first.Length; bone++)
                {
                    work?.Charge(jacobian ? 96 : 32);
                    double x = h0 * first[bone][0] + h1 * last[bone][0] + a * parameters[bone * 6] + b * parameters[bone * 6 + 1];
                    double y = h0 * first[bone][1] + h1 * last[bone][1] + a * parameters[bone * 6 + 2] + b * parameters[bone * 6 + 3];
                    double z = h0 * first[bone][2] + h1 * last[bone][2] + a * parameters[bone * 6 + 4] + b * parameters[bone * 6 + 5];
                    if (!Finite(x) || !Finite(y) || !Finite(z)) throw new ArgumentException("Native Hermite evaluation overflowed a finite rotation input.");
                    var rz = DQuaternion.Axis(2, z * Radians); var ry = DQuaternion.Axis(1, y * Radians);
                    if (jacobian)
                    {
                        // Native Euler order is Rz Ry Rx. Spatial derivatives
                        // are parent*Rz*Ry*X, parent*Rz*Y and parent*Z.
                        axes[bone * 3] = (world * rz * ry).Rotate(new DVector(1, 0, 0));
                        axes[bone * 3 + 1] = (world * rz).Rotate(new DVector(0, 1, 0));
                        axes[bone * 3 + 2] = world.Rotate(new DVector(0, 0, 1));
                    }
                    world = world * rz * ry * DQuaternion.Axis(0, x * Radians);
                    worlds[bone] = world;
                }
                for (int target = 0; target < observed.Length; target++)
                {
                work?.Charge(parameters.Length * 8L);
                int row = sample * observed.Length + target, observedBone = observed[target];
                var goal = goals[sample][target];
                var error = (goal.Inverse * worlds[observedBone]).Log();
                if (!Finite(error.Norm)) throw new ArgumentException("Native rotation composition produced an invalid SO3 sample.");
                result.Residual[row] = error.Array;
                result.Maximum = Math.Max(result.Maximum, error.Norm);
                if (!jacobian) continue;
                var columns = Enumerable.Range(0, parameters.Length).Select(_ => new double[3]).ToArray();
                for (int bone = 0; bone <= observedBone; bone++) for (int axis = 0; axis < 3; axis++)
                {
                    var direction = goal.Inverse.Rotate(axes[bone * 3 + axis]);
                    var derivative = InverseLeftJacobian(error, direction) * Radians;
                    columns[bone * 6 + axis * 2] = (derivative * a).Array;
                    columns[bone * 6 + axis * 2 + 1] = (derivative * b).Array;
                }
                result.Jacobian[row] = columns;
                }
            }
            return result;
        }

        private static DVector InverseLeftJacobian(DVector error, DVector direction)
        {
            double theta = error.Norm, theta2 = theta * theta;
            double coefficient = theta < 1e-4 ? 1d / 12 + theta2 / 720 + theta2 * theta2 / 30240 :
                (1 - .5 * theta / Math.Tan(.5 * theta)) / theta2;
            var cross = DVector.Cross(error, direction);
            return direction - cross * .5 + DVector.Cross(error, cross) * coefficient;
        }

        private sealed class Halfspace
        {
            internal double[] Row;
            internal int Axis = -1;
            internal double Sign, Bound, Multiplier;
            internal double At(double[] value) => Axis >= 0 ? Sign * value[Axis] : Row.Select((v, i) => v * value[i]).Sum();
            internal void Move(double[] value, double amount)
            {
                if (Axis >= 0) value[Axis] -= amount * Sign;
                else for (int i = 0; i < value.Length; i++) value[i] -= amount * Row[i];
            }
        }

        // The convex local model has ||r + J delta|| <= maximum. Exchange
        // supporting halfspaces until every actual norm satisfies that bound.
        // An analytic linear least-squares seed accelerates Hildreth's dual
        // projections; it is not an acceptance objective. Every retained step
        // still meets the maximum-norm target and finite box trust region.
        // Budget exhaustion means uncertain,
        // never a proof of infeasibility or permission to relax the target.
        private static bool FeasibleLinearStep(Linearization linear, double maximum, double trust,
            int variables, CancellationToken token, WorkBudget work, out double[] step)
        {
            var seed = LinearSeed(linear, variables, work); double largest = seed.Select(Math.Abs).Max();
            if (largest > trust) for (int i = 0; i < variables; i++) seed[i] *= trust / largest;
            step = seed.ToArray(); var constraints = new List<Halfspace>();
            for (int i = 0; i < variables; i++)
            {
                constraints.Add(new Halfspace { Axis = i, Sign = 1, Bound = trust });
                constraints.Add(new Halfspace { Axis = i, Sign = -1, Bound = trust });
            }
            for (int exchange = 0; exchange < 12; exchange++)
            {
                token.ThrowIfCancellationRequested(); bool satisfied = true;
                for (int sample = 0; sample < linear.Residual.Length; sample++)
                {
                    work.Charge(checked(variables * 6L));
                    var r = linear.Residual[sample]; var j = linear.Jacobian[sample];
                    var value = new double[3]; Array.Copy(r, value, 3);
                    for (int v = 0; v < variables; v++) for (int c = 0; c < 3; c++) value[c] += j[v][c] * step[v];
                    double norm = Math.Sqrt(value.Sum(x => x * x));
                    if (norm <= maximum + 1e-11) continue;
                    satisfied = false; var row = new double[variables]; double bound = maximum;
                    for (int c = 0; c < 3; c++)
                    {
                        double normal = value[c] / norm; bound -= normal * r[c];
                        for (int v = 0; v < variables; v++) row[v] += normal * j[v][c];
                    }
                    double length = Math.Sqrt(row.Sum(x => x * x));
                    if (length < 1e-14) return false;
                    for (int v = 0; v < variables; v++) row[v] /= length;
                    constraints.Add(new Halfspace { Row = row, Bound = bound / length });
                }
                if (satisfied) return true;
                // Restart the quadratic projection around the same analytic
                // seed when cuts are added; dual multipliers must restart too.
                Array.Copy(seed, step, step.Length);
                foreach (var constraint in constraints) constraint.Multiplier = 0;
                for (int sweep = 0; sweep < 128; sweep++)
                {
                    double largestMove = 0;
                    foreach (var constraint in constraints)
                    {
                        work.Charge(constraint.Axis >= 0 ? 2 : variables * 2L);
                        token.ThrowIfCancellationRequested();
                        double next = Math.Max(0, constraint.Multiplier + constraint.At(step) - constraint.Bound);
                        double change = next - constraint.Multiplier; constraint.Multiplier = next;
                        constraint.Move(step, change); largestMove = Math.Max(largestMove, Math.Abs(change));
                    }
                    if (largestMove < 1e-9 && Satisfied(constraints, step, 1e-9, work)) break;
                }
                if (step.Any(x => !Finite(x)) || !Satisfied(constraints, step, 1e-7, work)) return false;
            }
            return false;
        }

        private static double[] LinearSeed(Linearization linear, int variables, WorkBudget work)
        {
            work.Charge(checked((long)linear.Residual.Length * variables * variables * 3 + (long)variables * variables * variables));
            var normal = new double[variables, variables]; var rhs = new double[variables];
            for (int sample = 0; sample < linear.Residual.Length; sample++) for (int a = 0; a < variables; a++)
            {
                var ja = linear.Jacobian[sample][a]; var r = linear.Residual[sample];
                for (int c = 0; c < 3; c++) rhs[a] -= ja[c] * r[c];
                for (int b = 0; b <= a; b++)
                {
                    var jb = linear.Jacobian[sample][b]; double value = 0;
                    for (int c = 0; c < 3; c++) value += ja[c] * jb[c];
                    normal[a, b] += value; if (a != b) normal[b, a] += value;
                }
            }
            double maximum = 0;
            for (int a = 0; a < variables; a++) maximum = Math.Max(maximum, normal[a, a]);
            if (maximum <= 0 || !Finite(maximum)) return new double[variables];
            for (int a = 0; a < variables; a++) normal[a, a] += maximum * 1e-8;
            for (int column = 0; column < variables; column++)
            {
                int pivot = column;
                for (int row = column + 1; row < variables; row++) if (Math.Abs(normal[row, column]) > Math.Abs(normal[pivot, column])) pivot = row;
                if (Math.Abs(normal[pivot, column]) < maximum * 1e-14) return new double[variables];
                if (pivot != column)
                {
                    for (int c = column; c < variables; c++) { double saved = normal[column, c]; normal[column, c] = normal[pivot, c]; normal[pivot, c] = saved; }
                    double value = rhs[column]; rhs[column] = rhs[pivot]; rhs[pivot] = value;
                }
                for (int row = column + 1; row < variables; row++)
                {
                    double factor = normal[row, column] / normal[column, column];
                    for (int c = column + 1; c < variables; c++) normal[row, c] -= factor * normal[column, c];
                    rhs[row] -= factor * rhs[column];
                }
            }
            var result = new double[variables];
            for (int row = variables - 1; row >= 0; row--)
            {
                double value = rhs[row]; for (int c = row + 1; c < variables; c++) value -= normal[row, c] * result[c];
                result[row] = value / normal[row, row];
                if (!Finite(result[row])) return new double[variables];
            }
            return result;
        }

        private static bool Satisfied(List<Halfspace> constraints, double[] point, double tolerance, WorkBudget work)
        {
            foreach (var constraint in constraints)
            {
                work.Charge(constraint.Axis >= 0 ? 1 : point.Length);
                if (constraint.At(point) > constraint.Bound + tolerance) return false;
            }
            return true;
        }

        private sealed class WorkExhausted : Exception { }
        private sealed class WorkBudget
        {
            private readonly long maximum;
            private readonly CancellationToken token;
            private readonly Func<long, bool> reserve;
            internal long Work { get; private set; }
            internal WorkBudget(long maximum, CancellationToken token, Func<long, bool> reserve)
            { this.maximum = maximum; this.token = token; this.reserve = reserve; }
            internal void Charge(long amount)
            {
                token.ThrowIfCancellationRequested();
                if (amount < 0 || amount > maximum - Work || reserve != null && !reserve(amount)) throw new WorkExhausted();
                Work += amount;
            }
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private readonly struct DVector
        {
            internal readonly double X, Y, Z;
            internal DVector(double x, double y, double z) { X = x; Y = y; Z = z; }
            internal double[] Array => new[] { X, Y, Z };
            internal double Norm => Math.Sqrt(X * X + Y * Y + Z * Z);
            internal static DVector Cross(DVector a, DVector b) => new DVector(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
            public static DVector operator +(DVector a, DVector b) => new DVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static DVector operator -(DVector a, DVector b) => new DVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static DVector operator *(DVector a, double b) => new DVector(a.X * b, a.Y * b, a.Z * b);
        }
        private readonly struct DQuaternion
        {
            internal readonly double X, Y, Z, W;
            internal DQuaternion(double x, double y, double z, double w) { X = x; Y = y; Z = z; W = w; }
            internal static DQuaternion From(Quaternion value)
            {
                double length = Math.Sqrt((double)value.X * value.X + (double)value.Y * value.Y + (double)value.Z * value.Z + (double)value.W * value.W);
                if (!Finite(length) || length == 0) throw new ArgumentException("Rotation samples need finite nonzero quaternions.");
                return new DQuaternion(value.X / length, value.Y / length, value.Z / length, value.W / length);
            }
            internal static DQuaternion Axis(int axis, double angle)
            {
                double s = Math.Sin(angle * .5), c = Math.Cos(angle * .5);
                return new DQuaternion(axis == 0 ? s : 0, axis == 1 ? s : 0, axis == 2 ? s : 0, c);
            }
            internal DQuaternion Inverse => new DQuaternion(-X, -Y, -Z, W);
            internal DVector Rotate(DVector value)
            {
                var v = new DVector(X, Y, Z); var cross = DVector.Cross(v, value) * 2;
                return value + cross * W + DVector.Cross(v, cross);
            }
            internal DVector Log()
            {
                double sign = W < 0 ? -1 : 1, length = Math.Sqrt(X * X + Y * Y + Z * Z);
                double scale = length < 1e-15 ? 2 * sign : 2 * Math.Atan2(length, Math.Abs(W)) * sign / length;
                return new DVector(X * scale, Y * scale, Z * scale);
            }
            public static DQuaternion operator *(DQuaternion a, DQuaternion b) => new DQuaternion(
                a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
                a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
                a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
                a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);
        }
    }
}
