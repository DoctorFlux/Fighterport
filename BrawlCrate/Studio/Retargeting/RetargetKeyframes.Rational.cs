using BrawlLib.Wii.Animations;
using System;
using System.Collections.Generic;
using System.Linq;
using BigInteger = System.Numerics.BigInteger;

namespace BrawlCrate.Studio.Retargeting
{
    internal static partial class RetargetKeyframes
    {
        // Same normalized Hermite/Bernstein construction as the owned Core
        // reducer. Exact binary input coefficients and native float times are
        // rational; an uncertain/budget-limited hull retains dense keys.
        static SourceRational[] QualityBernstein(KeyframeEntry a, KeyframeEntry b, float first, float last)
        {
            SourceRational start = SourceRational.FromFloat(a._index);
            SourceRational dt = SourceRational.FromFloat(b._index) - start;
            var av = SourceRational.FromFloat(a._value); var bv = SourceRational.FromFloat(b._value);
            var am = dt * SourceRational.FromFloat(a._tangent); var bm = dt * SourceRational.FromFloat(b._tangent);
            var c0 = av; var c1 = am; var c2 = -3 * av + 3 * bv - 2 * am - bm; var c3 = 2 * av - 2 * bv + am + bm;
            SourceRational alpha = (SourceRational.FromFloat(first) - start) / dt;
            SourceRational beta = (SourceRational.FromFloat(last) - SourceRational.FromFloat(first)) / dt;
            var d0 = c0 + alpha * (c1 + alpha * (c2 + alpha * c3));
            var d1 = beta * (c1 + alpha * (2 * c2 + 3 * alpha * c3));
            var d2 = beta * beta * (c2 + 3 * alpha * c3); var d3 = beta * beta * beta * c3;
            return new[] { d0, d0 + d1 / 3, d0 + 2 * d1 / 3 + d2 / 3, d0 + d1 + d2 + d3 };
        }

        static bool QualitySignedScale(KeyframeEntry[] keys, QualityProofBudget proof)
        {
            if (keys.Any(k => k._value == 0)) return false;
            for (int i = 1; i < keys.Length; i++)
            {
                if (!proof.Charge(4)) return false;
                if (keys[i]._index <= keys[i - 1]._index) return false;
                var hull = QualityBernstein(keys[i - 1], keys[i], keys[i - 1]._index, keys[i]._index);
                if (!hull.All(v => v > SourceRational.Zero) && !hull.All(v => v < SourceRational.Zero)) return false;
            }
            return true;
        }

        static bool QualitySpan(KeyframeEntry[] keys, int left, int right, double valueLimit, double speedLimit, bool scale,
            QualityProofBudget proof, out double valueError, out double speedError)
        {
            valueError = speedError = 0;
            if (!proof.Span()) return false;
            if (scale && valueLimit > 0)
            {
                if (!proof.Charge(4)) return false;
                var hull = QualityBernstein(keys[left], keys[right], keys[left]._index, keys[right]._index);
                if (!hull.All(v => v > SourceRational.Zero) && !hull.All(v => v < SourceRational.Zero)) return false;
            }
            for (int i = left; i < right; i++)
            {
                if (!proof.Charge(8)) return false;
                var a = keys[i]; var b = keys[i + 1];
                if (b._index <= a._index) return false;
                if (proof.Probes.Count < 10000)
                {
                    proof.Probe(a._index);
                    proof.Probe(a._index + ((double)b._index - a._index) * .381966011250105);
                    proof.Probe(a._index + ((double)b._index - a._index) * .618033988749895);
                    proof.Probe(b._index);
                }
                var baseline = QualityBernstein(a, b, a._index, b._index);
                var candidate = QualityBernstein(keys[left], keys[right], a._index, b._index);
                var difference = baseline.Zip(candidate, (x, y) => x - y).ToArray();
                if (!QualityBound(difference, SourceRational.FromDouble(valueLimit), proof, out double value)) return false;
                SourceRational dt = SourceRational.FromFloat(b._index) - SourceRational.FromFloat(a._index);
                var derivative = Enumerable.Range(0, 3).Select(c => 3 * (difference[c + 1] - difference[c]) / dt).ToArray();
                if (!QualityBound(derivative, SourceRational.FromDouble(speedLimit), proof, out double speed)) return false;
                valueError = Math.Max(valueError, value); speedError = Math.Max(speedError, speed);
            }
            return true;
        }

        static bool QualityBound(SourceRational[] coefficients, SourceRational limit, QualityProofBudget proof, out double error)
        {
            error = 0; var maximum = SourceRational.Zero;
            var stack = new Stack<Tuple<SourceRational[], int>>(); stack.Push(Tuple.Create(coefficients, 0));
            while (stack.Count > 0)
            {
                var item = stack.Pop(); var values = item.Item1;
                if (!proof.Charge(values.Length)) return false;
                if (values[0].Abs() > limit || values[values.Length - 1].Abs() > limit) return false;
                var hull = values.Select(v => v.Abs()).Max();
                if (hull <= limit) { if (hull > maximum) maximum = hull; continue; }
                if (item.Item2 >= 8) return false;
                var current = values.ToArray(); var left = new SourceRational[values.Length]; var right = new SourceRational[values.Length];
                left[0] = current[0]; right[right.Length - 1] = current[current.Length - 1];
                for (int level = 1; level < values.Length; level++)
                {
                    for (int i = 0; i < values.Length - level; i++) current[i] = (current[i] + current[i + 1]) / 2;
                    left[level] = current[0]; right[values.Length - level - 1] = current[values.Length - level - 1];
                }
                stack.Push(Tuple.Create(right, item.Item2 + 1)); stack.Push(Tuple.Create(left, item.Item2 + 1));
            }
            error = maximum.UpperDouble(); return true;
        }

        readonly struct SourceRational : IComparable<SourceRational>
        {
            readonly BigInteger numerator, denominator;
            internal static SourceRational Zero => new SourceRational(BigInteger.Zero, BigInteger.One);
            SourceRational(BigInteger n, BigInteger d)
            {
                if (d.IsZero) throw new DivideByZeroException();
                if (d.Sign < 0) { n = -n; d = -d; }
                if (n.IsZero) { numerator = BigInteger.Zero; denominator = BigInteger.One; return; }
                var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d); numerator = n / gcd; denominator = d / gcd;
            }
            internal static SourceRational FromFloat(float value) => FromDouble(value);
            internal static SourceRational FromDouble(double value)
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) throw new System.IO.InvalidDataException("Source-key proof requires finite coefficients.");
                long bits = BitConverter.DoubleToInt64Bits(value); int exponent = (int)((bits >> 52) & 0x7ff); long fraction = bits & 0xfffffffffffffL;
                BigInteger n = exponent == 0 ? fraction : fraction | (1L << 52); int power = exponent == 0 ? -1074 : exponent - 1023 - 52;
                if (bits < 0) n = -n;
                return power >= 0 ? new SourceRational(n << power, BigInteger.One) : new SourceRational(n, BigInteger.One << -power);
            }
            internal SourceRational Abs() => new SourceRational(BigInteger.Abs(numerator), denominator);
            internal double UpperDouble()
            {
                if (numerator.IsZero) return 0;
                // Native float coefficients and ushort frame spans stay within
                // double range. Fail closed rather than understate a huge hull.
                double value = (double)BigInteger.Abs(numerator) / (double)denominator;
                if (double.IsNaN(value) || double.IsInfinity(value)) return double.PositiveInfinity;
                while (FromDouble(value) < Abs()) value = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) + 1);
                return value;
            }
            public int CompareTo(SourceRational other) => (numerator * other.denominator).CompareTo(other.numerator * denominator);
            public static implicit operator SourceRational(int value) => new SourceRational(value, BigInteger.One);
            public static SourceRational operator +(SourceRational a, SourceRational b) => new SourceRational(a.numerator * b.denominator + b.numerator * a.denominator, a.denominator * b.denominator);
            public static SourceRational operator -(SourceRational a, SourceRational b) => new SourceRational(a.numerator * b.denominator - b.numerator * a.denominator, a.denominator * b.denominator);
            public static SourceRational operator *(SourceRational a, SourceRational b) => new SourceRational(a.numerator * b.numerator, a.denominator * b.denominator);
            public static SourceRational operator /(SourceRational a, SourceRational b) => new SourceRational(a.numerator * b.denominator, a.denominator * b.numerator);
            public static bool operator >(SourceRational a, SourceRational b) => a.CompareTo(b) > 0;
            public static bool operator <(SourceRational a, SourceRational b) => a.CompareTo(b) < 0;
            public static bool operator >=(SourceRational a, SourceRational b) => a.CompareTo(b) >= 0;
            public static bool operator <=(SourceRational a, SourceRational b) => a.CompareTo(b) <= 0;
        }
    }
}
