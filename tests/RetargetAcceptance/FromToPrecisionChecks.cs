using BrawlCrate.Studio.Retargeting;
using System;
using System.Numerics;

internal static partial class Program
{
    private static void FromToPrecisionChecks()
    {
        // Literal axes and analytic planar rotations exercise both sides of
        // the former identity deadband without using the implementation's dot
        // or acos-based error calculation as the independent expectation.
        foreach (double degrees in new[] { .001, .079, .0809, .0810, .0811, .082, 1, 90, 179.999 })
            foreach (double scale in new[] { .125, 17.0 })
            {
                double radians = degrees * Math.PI / 180;
                var from = new Vector3((float)scale, 0, 0);
                var to = new Vector3((float)(scale * Math.Cos(radians)), (float)(scale * Math.Sin(radians)), 0);
                var actual = RigMath.FromTo(from, to);
                double angle = 2 * Math.Atan2(Math.Sqrt((double)actual.X * actual.X + (double)actual.Y * actual.Y + (double)actual.Z * actual.Z), Math.Abs(actual.W)) * 180 / Math.PI;
                var transformed = Vector3.Transform(Vector3.UnitX, actual);
                double directionError = Math.Sqrt(Math.Pow(transformed.X - Math.Cos(radians), 2) + Math.Pow(transformed.Y - Math.Sin(radians), 2) + transformed.Z * transformed.Z);
                Check(Math.Abs(angle - degrees) < .00003 && directionError < .000001,
                    "Precise from/to preserves analytic small, threshold-adjacent and near-antiparallel rotations at different vector scales",
                    new { degrees, scale, angle, directionError });
                var reverse = RigMath.FromTo(to, from);
                Check(SpanAngle(Quaternion.Normalize(reverse * actual), Quaternion.Identity) < .00003,
                    "Precise from/to inverse composition retains the same shortest rotation without a direction-dependent deadband");
            }
        foreach (var from in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Normalize(new Vector3(1, -2, 3)) })
        {
            var opposite = RigMath.FromTo(from, -from);
            Check(Vector3.Distance(Vector3.Transform(from, opposite), -from) < .000001 &&
                SpanAngle(RigMath.FromTo(from, from), Quaternion.Identity) < .000001,
                "Precise from/to retains deterministic exact-antiparallel handling and exact parallel identity");
        }
        bool invalid = false; try { RigMath.FromTo(Vector3.Zero, Vector3.UnitX); } catch (System.IO.InvalidDataException) { invalid = true; }
        Check(invalid, "Precise from/to retains the collapsed-direction failure boundary");
    }
}
