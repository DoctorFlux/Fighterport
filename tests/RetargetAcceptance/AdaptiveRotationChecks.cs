using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Linq;
using System.Numerics;
using System.Threading;

internal static partial class Program
{
    private static void AdaptiveRotationPrecisionChecks()
    {
        using (var native = new CHR0Node { Name = "Anonymous fractional master", Version = 4, FrameCount = 2 })
        {
            var root = native.CreateEntry("Opaque motion control");
            root.SetKeyframe(4, 0, 0, true, true)._tangent = 720;
            root.SetKeyframe(4, 1, 720, true, true)._tangent = 720;
            string RootIdentity() => string.Join(";", root.KeyArrays[4].Entries.Select(k => k._index + ":" + k._value + ":" + k._tangent));
            string original = RootIdentity();
            Quaternion Frozen(int bone, float time) => BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(0, root.GetFrameValue(4, time), 0));
            AdaptiveRotationPose Sample(float time)
            {
                float value = (float)Math.Sqrt(time);
                var euler = new Vector3(80 * value, 89.99f + time * .02f, -137);
                var parent = Frozen(0, time);
                return new AdaptiveRotationPose { EulerDegrees = new[] { new Vector3(0, root.GetFrameValue(4, time), 0), euler },
                    WorldRotations = new[] { parent, Quaternion.Normalize(parent * BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(euler.X, euler.Y, euler.Z))) },
                    Translations = new[] { new Vector3(4 * time, 0, 0), new Vector3(.004f * value, 1, 0) },
                    Scales = new[] { Vector3.One, new Vector3(1, 1 + .001f * value, 1) } };
            }
            var bake = AdaptiveRotationCurveBaker.Bake(new[] { -1, 0 }, new[] { false, true }, 1, Sample, Frozen, CancellationToken.None);
            Check(bake.Converged && !bake.WorkLimitReached && bake.UnresolvedIntervals == 0,
                "Adaptive full-quality native baking resolves a square-root extension that a fixed integer Hermite cell cannot represent", new { bake.Work, bake.SampleCount, bake.MaximumOrientationDegrees, bake.MaximumTranslationError, bake.MaximumScaleError });
            Check(bake.Tracks.Length == 1 && bake.Tracks[0].Bone == 1 && RootIdentity() == original,
                "Adaptive curve baking never authors or rewrites the frozen source-owned motion control");
            var track = bake.Tracks.Single();
            Check(track.Rotation.Any(k => k.Time != Math.Truncate(k.Time)) && !bake.AllTimesOnI6Grid,
                "Difficult native curvature gets real fractional knots, including precise float times when the 1/32 grid is insufficient");
            foreach (var curve in new[] { track.Rotation, track.Translation, track.Scale })
                Check(curve.First().Time == 0 && curve.Last().Time == 1,
                    "Adaptive native curves retain the exact original integer endpoints and animation duration");
            Check(track.Rotation.First().Value == Sample(0).EulerDegrees[1] && track.Rotation.Last().Value == Sample(1).EulerDegrees[1],
                "Adaptive rotation refinement does not change frozen native integer values or authored Euler branches");
            var body = native.CreateEntry("Opaque existing body bone"); body.Keyframes.PreserveInterpolation = true;
            void Emit(AdaptiveVectorKey[] curve, int group)
            {
                foreach (var key in curve) for (int c = 0; c < 3; c++)
                {
                    float Value(Vector3 v) => c == 0 ? v.X : c == 1 ? v.Y : v.Z;
                    body.SetKeyframe(group * 3 + c, key.Time, Value(key.Value), true, true)._tangent = Value(key.IncomingSlope);
                    if (BitConverter.ToInt32(BitConverter.GetBytes(Value(key.IncomingSlope)), 0) != BitConverter.ToInt32(BitConverter.GetBytes(Value(key.OutgoingSlope)), 0))
                        body.SetKeyframe(group * 3 + c, key.Time, Value(key.Value), true, true)._tangent = Value(key.OutgoingSlope);
                }
            }
            Emit(track.Rotation, 1); Emit(track.Translation, 2); Emit(track.Scale, 0);
            double angle = 0, translation = 0, scaleError = 0;
            for (int i = 0; i <= 512; i++)
            {
                // These samples are independent of both the fit and its
                // near-boundary/withheld acceptance probes.
                float time = i / 512f; var target = Sample(time);
                var local = BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(body.GetFrameValue(3, time), body.GetFrameValue(4, time), body.GetFrameValue(5, time)));
                angle = Math.Max(angle, SpanAngle(Quaternion.Normalize(Frozen(0, time) * local), target.WorldRotations[1]));
                var position = new Vector3(body.GetFrameValue(6, time), body.GetFrameValue(7, time), body.GetFrameValue(8, time));
                translation = Math.Max(translation, Vector3.Distance(position, target.Translations[1]));
                for (int c = 0; c < 3; c++) scaleError = Math.Max(scaleError, Math.Abs(body.GetFrameValue(c, time) - (c == 0 ? target.Scales[1].X : c == 1 ? target.Scales[1].Y : target.Scales[1].Z)));
            }
            Check(angle <= .025 && translation <= .0002 && scaleError <= .00001,
                "Independent native float evaluation of adaptive SRT retains the declared full-quality rotation, translation and scale bounds", new { angle, translation, scaleError });
            Check(bake.NativeKeyUpperBound <= 2000000 && bake.Work <= 2000000,
                "Adaptive fractional master preparation preserves the shared native key and work limits");
            var exhausted = AdaptiveRotationCurveBaker.Bake(new[] { -1, 0 }, new[] { false, true }, 1, Sample, Frozen, CancellationToken.None, maximumWork: 1);
            Check(exhausted.WorkLimitReached && !exhausted.Converged && RootIdentity() == original,
                "An exhausted adaptive bake cannot present a partial or unmeasured master as converged");
            AdaptiveRotationPose Inconsistent(float time) => new AdaptiveRotationPose
            {
                EulerDegrees = new[] { Vector3.Zero, Vector3.Zero },
                WorldRotations = new[] { Quaternion.Identity, Quaternion.Identity }
            };
            var fixedMismatch = AdaptiveRotationCurveBaker.Bake(new[] { -1, 0 }, new[] { false, true }, 1, Inconsistent,
                (bone, time) => Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)(Math.PI / 2)), CancellationToken.None);
            Check(!fixedMismatch.Converged && !fixedMismatch.WorkLimitReached && fixedMismatch.UnresolvedIntervals == 1 &&
                fixedMismatch.MaximumFixedEndpointOrientationDegrees > 89 && fixedMismatch.AttemptedSegments == 1 && fixedMismatch.Work < 1000,
                "An inconsistent frozen parent at a fixed integer pose is reported without recursively spending the operation budget",
                new { fixedMismatch.Work, fixedMismatch.MaximumFixedEndpointOrientationDegrees });
            Check(fixedMismatch.FirstUnresolvedBone == 1 && fixedMismatch.FirstUnresolvedChannel == "Rotation" &&
                fixedMismatch.FirstUnresolvedFirstFrame == 0 && fixedMismatch.FirstUnresolvedLastFrame == 1 && fixedMismatch.FirstUnresolvedError > 89,
                "An unresolved native interval retains the exact first failing bone, channel and time range for its diagnostic");
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel(); bool stopped = false;
                try { AdaptiveRotationCurveBaker.Bake(new[] { -1, 0 }, new[] { false, true }, 1, Sample, Frozen, cancellation.Token); }
                catch (OperationCanceledException) { stopped = true; }
                Check(stopped && RootIdentity() == original, "Adaptive master cancellation preserves the exact native source control");
            }
        }
        foreach (int pathCase in new[] { 0, 1, 2 })
        {
            bool fullTurns=pathCase==1;
            Vector3 TrueEuler(float time)
            {
                if (fullTurns) return new Vector3(0, 0, 720 * time);
                double width=pathCase==2?.00005:.01;
                double offset=time-(pathCase==2?.822447:.5),radius=Math.Sqrt(offset*offset+width*width);
                float x=(float)(Math.Atan2(width,offset)*180/Math.PI);
                return new Vector3(x,(float)(-90+radius*(pathCase==2?.4:20)),-x);
            }
            var beginning = TrueEuler(0); var ending = TrueEuler(1);
            var aliasedEnd = new Vector3(ending.X + 180, -180 - ending.Y, ending.Z - 180);
            AdaptiveRotationPose Solved(float time)
            {
                var exact = TrueEuler(time);
                var rotation = BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(exact.X, exact.Y, exact.Z));
                var guide = fullTurns ? exact : BrawlRetargeter.EulerNear(rotation, Vector3.Lerp(beginning, aliasedEnd, time));
                return new AdaptiveRotationPose { EulerDegrees = new[] { guide }, LocalRotations = new[] { rotation }, WorldRotations = new[] { rotation } };
            }
            var continuous = AdaptiveRotationCurveBaker.Bake(new[] { -1 }, new[] { true }, 1, Solved,
                (bone, time) => Quaternion.Identity, CancellationToken.None, generatedEulerContinuation: true);
            Check(continuous.Converged && !continuous.WorkLimitReached && (pathCase==2 || (fullTurns ? continuous.IntegerEulerRebranches == 0 : continuous.IntegerEulerRebranches == 1)),
                pathCase==2 ? "Off-midpoint near-pole motion retains its independent orientation reference without an imposed Euler-family change" :
                fullTurns ? "Generated Euler continuation retains an intentional 720-degree body path and its original integer coefficients" :
                "Generated Euler continuation follows a smooth near-pole excursion instead of the aliased guide's opposite integer family",
                new { continuous.Work, continuous.SampleCount, continuous.IntegerEulerRebranches, continuous.MaximumOrientationDegrees });
            using (var encoded = new CHR0Node { Name = "Independent continuous generated body", Version = 4, FrameCount = 2 })
            {
                var entry = encoded.CreateEntry("Anonymous existing body"); entry.Keyframes.PreserveInterpolation = true;
                var keys = continuous.Tracks.Single().Rotation;
                foreach (var key in keys) for (int ch = 0; ch < 3; ch++)
                {
                    float Value(Vector3 v) => ch == 0 ? v.X : ch == 1 ? v.Y : v.Z;
                    entry.SetKeyframe(ch + 3, key.Time, Value(key.Value), true, true)._tangent = Value(key.IncomingSlope);
                    if (BitConverter.ToInt32(BitConverter.GetBytes(Value(key.IncomingSlope)), 0) != BitConverter.ToInt32(BitConverter.GetBytes(Value(key.OutgoingSlope)), 0))
                        entry.SetKeyframe(ch + 3, key.Time, Value(key.Value), true, true)._tangent = Value(key.OutgoingSlope);
                }
                double maximum = 0;
                foreach(float time in Enumerable.Range(0,4097).Select(i=>i/4096f).Concat(new[]{.8224468f,.822447f,.8224478f,.137f,.371f,.623f}))
                {
                    var actual = BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(entry.GetFrameValue(3, time), entry.GetFrameValue(4, time), entry.GetFrameValue(5, time)));
                    maximum = Math.Max(maximum, SpanAngle(actual, Solved(time).WorldRotations[0]));
                }
                Check(maximum <= .025 && SpanAngle(BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(keys.First().Value.X, keys.First().Value.Y, keys.First().Value.Z)), Solved(0).WorldRotations[0]) < .0001 &&
                    SpanAngle(BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(keys.Last().Value.X, keys.Last().Value.Y, keys.Last().Value.Z)), Solved(1).WorldRotations[0]) < .0001,
                    "Independent native evaluation retains generated-continuation integer physical poses and withheld fractional trajectory", new { fullTurns, maximum });
                if (fullTurns) Check(keys.Last().Value.Z == 720 && keys.First().Value.Z == 0,
                    "Generated Euler continuation never reduces a full authored turn modulo 360 degrees");
            }
        }
    }
}
