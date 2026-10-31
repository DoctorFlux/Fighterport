using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    internal sealed class AdaptiveRotationPose
    {
        // EulerDegrees is an already unwrapped authored branch supplied by the
        // solved-pose sampler. Quaternion orientation alone cannot identify
        // full authored scalar turns.
        internal Vector3[] EulerDegrees { get; set; }
        internal Quaternion[] WorldRotations { get; set; }
        internal Quaternion[] LocalRotations { get; set; }
        internal Vector3[] Translations { get; set; }
        internal Vector3[] Scales { get; set; }
    }

    internal sealed class AdaptiveVectorKey
    {
        internal float Time { get; set; }
        internal Vector3 Value { get; set; }
        internal Vector3 IncomingSlope { get; set; }
        internal Vector3 OutgoingSlope { get; set; }
    }

    internal sealed class AdaptiveTransformTrack
    {
        internal int Bone { get; set; }
        internal AdaptiveVectorKey[] Rotation { get; set; }
        internal AdaptiveVectorKey[] Translation { get; set; }
        internal AdaptiveVectorKey[] Scale { get; set; }
    }

    internal sealed class AdaptiveRotationBake
    {
        internal AdaptiveTransformTrack[] Tracks { get; set; }
        internal bool Converged { get; set; }
        internal bool WorkLimitReached { get; set; }
        internal int UnresolvedIntervals { get; set; }
        internal int SampleCount { get; set; }
        internal long Work { get; set; }
        internal long NativeKeyUpperBound { get; set; }
        internal bool AllTimesOnI6Grid { get; set; }
        internal double MaximumOrientationDegrees { get; set; }
        internal double MaximumTranslationError { get; set; }
        internal double MaximumScaleError { get; set; }
        internal int LastBone { get; set; } = -1;
        internal string LastChannel { get; set; }
        internal float LastFirstFrame { get; set; }
        internal float LastLastFrame { get; set; }
        internal int LastDepth { get; set; }
        internal int DeepestSubdivision { get; set; }
        internal int AttemptedSegments { get; set; }
        internal double LastMeasuredError { get; set; }
        internal double MaximumFixedEndpointOrientationDegrees { get; set; }
        internal int FirstUnresolvedBone { get; set; } = -1;
        internal string FirstUnresolvedChannel { get; set; }
        internal float FirstUnresolvedFirstFrame { get; set; }
        internal float FirstUnresolvedLastFrame { get; set; }
        internal int FirstUnresolvedDepth { get; set; }
        internal double FirstUnresolvedError { get; set; }
        internal int IntegerEulerRebranches { get; set; }
        internal string FailureWitness { get; set; }
        internal int ProvenConstantGroups { get; set; }
        internal int WindowCount { get; set; }
        internal long MaximumWindowWork { get; set; }
        internal float[] ValidationTimes { get; set; }
    }

    // A full-quality native master bake. No quantization, file-size reduction,
    // source-control rewriting or native resource ownership occurs here.
    // Authored integer values remain fixed by default. Explicit generated-Euler
    // continuation can replace an aliased internal Euler family while retaining
    // every physical integer pose and the sampled winding of a NEW retarget.
    // Failed Hermite intervals get actual dyadic
    // interior knots, first on the native I6 1/32 grid, then finer float times
    // (requiring precise I12) when that grid cannot represent the solved motion.
    internal static class AdaptiveRotationCurveBaker
    {
        private const long MaximumNativeKeys = 2000000;
        private const int MaximumDepth = 20;
        private static readonly float[] Training = Enumerable.Range(1, 7).Select(i => i / 8f).ToArray();
        // Independent fractions and near-boundary witnesses expose steep IK
        // extension/pole curvature that an eighth-frame training grid misses.
        private static readonly float[] Validation = Training.Concat(new[]
        { 0f, 1f / 256, 1f / 32, .211f, .457f, .913f, 31f / 32, 255f / 256, 1f }).Distinct().OrderBy(x => x).ToArray();

        internal static AdaptiveRotationBake Bake(int[] parents, bool[] editable, float lastFrame,
            Func<float, AdaptiveRotationPose> sample, Func<int, float, Quaternion> frozenLocal,
            CancellationToken cancellation, double toleranceDegrees = .025,
            long maximumWork = 2000000, double translationTolerance = .0002, double scaleTolerance = .00001,
            Func<long, bool> reserveSharedWork = null, bool generatedEulerContinuation = false,float[] mandatoryTimes=null,NativeRetargetConstants invariants=null,Vector3[] initialEuler=null,float firstFrame=0)
        {
            if (parents == null || editable == null || parents.Length == 0 || parents.Length > 512 ||
                editable.Length != parents.Length || sample == null || frozenLocal == null || !Finite(lastFrame) ||
                lastFrame < 0 || lastFrame != Math.Truncate(lastFrame) || lastFrame > 65535 || !Finite(firstFrame)||firstFrame<0||firstFrame>lastFrame||firstFrame!=Math.Truncate(firstFrame)||maximumWork < 0 || maximumWork > 2000000 ||
                !Finite(toleranceDegrees) || toleranceDegrees <= 0 || !Finite(translationTolerance) || translationTolerance <= 0 ||
                !Finite(scaleTolerance) || scaleTolerance <= 0)
                throw new ArgumentException("Adaptive native baking needs a bounded topological skeleton, fixed integer domain and finite declared tolerances.");
            var hierarchy = parents.ToArray(); var change = editable.ToArray();
            for (int bone = 0; bone < hierarchy.Length; bone++)
                if (hierarchy[bone] < -1 || hierarchy[bone] >= bone) throw new ArgumentException("Adaptive rotation baking requires an acyclic topological hierarchy.");
            var operation = new Operation(hierarchy, change, lastFrame, sample, frozenLocal, cancellation,
                toleranceDegrees, translationTolerance, scaleTolerance, maximumWork, reserveSharedWork, generatedEulerContinuation,mandatoryTimes,invariants,initialEuler,firstFrame);
            return operation.Run();
        }

        private sealed class WorkExhausted : Exception { }
        private sealed class UnrepresentableInterval : Exception { }
        private enum ChannelKind { Rotation, Translation, Scale }
        private sealed class EulerAnchor
        {
            internal float Time;
            internal Vector3 Euler;
        }
        private sealed class Segment
        {
            internal float First, Last;
            internal Vector3 Start, End, Outgoing, Incoming;
            internal Vector3 At(float time)
            {
                float offset = time - First, span = Last - First;
                return new Vector3(Hermite(Start.X, End.X, Outgoing.X, Incoming.X, offset, span),
                    Hermite(Start.Y, End.Y, Outgoing.Y, Incoming.Y, offset, span),
                    Hermite(Start.Z, End.Z, Outgoing.Z, Incoming.Z, offset, span));
            }
        }

        private sealed class Operation
        {
            private readonly int[] parents;
            private readonly bool[] editable;
            private readonly float last;
            private readonly float first;
            private readonly Func<float, AdaptiveRotationPose> sampler;
            private readonly Func<int, float, Quaternion> frozen;
            private readonly CancellationToken token;
            private readonly double orientationTolerance, translationTolerance, scaleTolerance;
            private readonly long maximumWork;
            private readonly Func<long, bool> reserve;
            private readonly bool generatedContinuation;
            private readonly double[] hierarchyBudgets;
            private readonly float[] mandatory;
            private readonly NativeRetargetConstants invariants;
            private readonly Vector3[] initialEuler;
            private readonly HashSet<float> validationTimes=new HashSet<float>();
            private readonly Dictionary<int, AdaptiveRotationPose> samples = new Dictionary<int, AdaptiveRotationPose>();
            private readonly Dictionary<long, Quaternion> worlds = new Dictionary<long, Quaternion>();
            private readonly Segment[][] rotations;
            private readonly List<EulerAnchor>[] continuations;
            private readonly AdaptiveRotationBake result = new AdaptiveRotationBake { AllTimesOnI6Grid = true };
            private readonly List<AdaptiveTransformTrack> tracks = new List<AdaptiveTransformTrack>();
            private long work, keys;

            internal Operation(int[] parents, bool[] editable, float last, Func<float, AdaptiveRotationPose> sampler,
                Func<int, float, Quaternion> frozen, CancellationToken token, double orientationTolerance,
                double translationTolerance, double scaleTolerance, long maximumWork, Func<long, bool> reserve, bool generatedContinuation,float[] mandatoryTimes,NativeRetargetConstants invariants,Vector3[] initialEuler,float firstFrame)
            {
                this.parents = parents; this.editable = editable; this.last = last; this.sampler = sampler; this.frozen = frozen;
                first=firstFrame;
                this.token = token; this.orientationTolerance = orientationTolerance; this.translationTolerance = translationTolerance;
                this.scaleTolerance = scaleTolerance; this.maximumWork = maximumWork; this.reserve = reserve;
                this.generatedContinuation = generatedContinuation;
                this.invariants=invariants;invariants?.Validate(parents);
                if(initialEuler!=null&&(initialEuler.Length!=parents.Length||initialEuler.Any(v=>!Finite(v.X)||!Finite(v.Y)||!Finite(v.Z))))throw new ArgumentException("Invalid generated Euler continuation boundary.");
                this.initialEuler=initialEuler?.ToArray();
                rotations = new Segment[parents.Length][];
                continuations = new List<EulerAnchor>[parents.Length];
                var levels=new int[parents.Length];
                for(int bone=0;bone<parents.Length;bone++)levels[bone]=(parents[bone]<0?0:levels[parents[bone]])+(editable[bone]?1:0);
                int maximumLevel=Math.Max(1,levels.Max());
                // Reserve headroom for descendant interpolation and native
                // float reconstruction. Giving every ancestor the entire world
                // budget leaves a constant child no remaining error allowance;
                // adding child knots cannot repair that inherited plateau.
                hierarchyBudgets=levels.Select(level=>orientationTolerance*.99*level/maximumLevel).ToArray();
                if(mandatoryTimes!=null&&(mandatoryTimes.Length>2000000||mandatoryTimes.Any(time=>!Finite(time)||time<first||time>last)))throw new ArgumentException("Invalid bounded native reference breakpoints.");
                var points=(mandatoryTimes??new float[0]).Distinct().OrderBy(time=>time).ToArray();
                mandatory=points.Concat(points.Skip(1).Select((end,index)=>points[index]+(end-points[index])*.5f)).Distinct().OrderBy(time=>time).ToArray();
            }

            internal AdaptiveRotationBake Run()
            {
                try
                {
                    for (int bone = 0; bone < parents.Length; bone++)
                    {
                        token.ThrowIfCancellationRequested(); if (!editable[bone]) continue;
                        if (generatedContinuation) PrepareContinuation(bone);
                        var rotation = Build(bone, ChannelKind.Rotation); rotations[bone] = rotation.ToArray();
                        var origin = Sample(first);
                        var translation = origin.Translations == null ? null : Build(bone, ChannelKind.Translation);
                        var scale = origin.Scales == null ? null : Build(bone, ChannelKind.Scale);
                        var track = new AdaptiveTransformTrack { Bone = bone, Rotation = Keys(rotation, Value(bone, first, ChannelKind.Rotation),first),
                            Translation = translation == null ? null : Keys(translation, origin.Translations[bone],first),
                            Scale = scale == null ? null : Keys(scale, origin.Scales[bone],first) };
                        tracks.Add(track);
                        foreach (var curve in new[] { track.Rotation, track.Translation, track.Scale }.Where(c => c != null))
                        {
                            foreach (var key in curve)
                            {
                                Charge(1); keys += 3;
                                if (Bits(key.IncomingSlope.X) != Bits(key.OutgoingSlope.X)) keys++;
                                if (Bits(key.IncomingSlope.Y) != Bits(key.OutgoingSlope.Y)) keys++;
                                if (Bits(key.IncomingSlope.Z) != Bits(key.OutgoingSlope.Z)) keys++;
                                if (keys > MaximumNativeKeys) throw new WorkExhausted();
                                if ((double)key.Time * 32 != Math.Truncate((double)key.Time * 32)) result.AllTimesOnI6Grid = false;
                            }
                        }
                    }
                    result.Converged = result.UnresolvedIntervals == 0;
                }
                catch (WorkExhausted) { result.WorkLimitReached = true; result.Converged = false; }
                catch (UnrepresentableInterval) { result.Converged = false; }
                token.ThrowIfCancellationRequested();
                invariants?.Validate(parents);
                result.Tracks = tracks.ToArray(); result.Work = work; result.SampleCount = samples.Count; result.NativeKeyUpperBound = keys;
                result.ValidationTimes=validationTimes.OrderBy(t=>t).ToArray();result.WindowCount=1;result.MaximumWindowWork=work;
                return result;
            }

            private List<Segment> Build(int bone, ChannelKind kind)
            {
                var segments = new List<Segment>();
                if(kind==ChannelKind.Translation&&invariants?.Translation(bone)==true||kind==ChannelKind.Scale&&invariants?.Scale(bone)==true)
                {
                    Charge(1);result.ProvenConstantGroups++;
                    return segments; // Keys emits the unchanged first value once.
                }
                if (last == first)
                {
                    result.LastBone = bone; result.LastChannel = kind.ToString(); result.LastFirstFrame = first; result.LastLastFrame = first; result.LastDepth = 0;
                    var one = new Segment { First = first, Last = first, Start = Value(bone, first, kind), End = Value(bone, first, kind) };
                    Record(kind, Measure(bone, one, kind));
                }
                for (int frame = (int)first; frame < last; frame++) Refine(bone, frame, frame + 1, kind, 0, segments);
                return segments;
            }

            private void Refine(int bone, float first, float end, ChannelKind kind, int depth, List<Segment> output)
            {
                result.LastBone = bone; result.LastChannel = kind.ToString(); result.LastFirstFrame = first; result.LastLastFrame = end;
                result.LastDepth = depth; result.DeepestSubdivision = Math.Max(result.DeepestSubdivision, depth); result.AttemptedSegments++;
                Charge(1);
                var segment = Fit(bone, first, end, kind);
                double maximum = Measure(bone, segment, kind);
                result.LastMeasuredError = maximum;
                double tolerance = kind == ChannelKind.Rotation ? hierarchyBudgets[bone] : kind == ChannelKind.Translation ? translationTolerance : scaleTolerance;
                // Original integer poses are contractual, so additional knots
                // cannot correct an inconsistent inherited parent/frozen root
                // at either fixed boundary. Report that contradiction rather
                // than recursively exhausting the budget beside that boundary.
                bool fixedBoundaryFailure = false;
                if (kind == ChannelKind.Rotation)
                    foreach (float time in new[] { first, end })
                        if (time == Math.Truncate(time))
                        {
                            double endpoint = Angle(Quaternion.Normalize(World(parents[bone], time) * Rotation(segment.At(time))), Sample(time).WorldRotations[bone]);
                            result.MaximumFixedEndpointOrientationDegrees = Math.Max(result.MaximumFixedEndpointOrientationDegrees, endpoint);
                            fixedBoundaryFailure |= endpoint > tolerance;
                        }
                float middle = first + (end - first) * .5f;
                if (maximum > tolerance && !fixedBoundaryFailure && depth < MaximumDepth && middle > first && middle < end)
                {
                    Refine(bone, first, middle, kind, depth + 1, output);
                    Refine(bone, middle, end, kind, depth + 1, output);
                    return;
                }
                Record(kind, maximum);
                output.Add(segment);
                // Prevent an oversized partial candidate even before native
                // per-axis paired sides are materialized.
                if ((long)output.Count * 6 + keys > MaximumNativeKeys) throw new WorkExhausted();
            }

            private void Record(ChannelKind kind, double maximum)
            {
                double tolerance = kind == ChannelKind.Rotation ? orientationTolerance : kind == ChannelKind.Translation ? translationTolerance : scaleTolerance;
                if (!Finite(maximum) || maximum > tolerance)
                {
                    result.UnresolvedIntervals++;
                    if (result.FirstUnresolvedBone < 0)
                    {
                        result.FirstUnresolvedBone = result.LastBone; result.FirstUnresolvedChannel = result.LastChannel;
                        result.FirstUnresolvedFirstFrame = result.LastFirstFrame; result.FirstUnresolvedLastFrame = result.LastLastFrame;
                        result.FirstUnresolvedDepth = result.LastDepth; result.FirstUnresolvedError = maximum;
                    }
                }
                if (kind == ChannelKind.Rotation) result.MaximumOrientationDegrees = Math.Max(result.MaximumOrientationDegrees, maximum);
                else if (kind == ChannelKind.Translation) result.MaximumTranslationError = Math.Max(result.MaximumTranslationError, maximum);
                else result.MaximumScaleError = Math.Max(result.MaximumScaleError, maximum);
                // A terminal failed interval already makes the complete
                // master unavailable. Continuing cannot repair that interval.
                if(!Finite(maximum)||maximum>tolerance)throw new UnrepresentableInterval();
            }

            private Segment Fit(int bone, float first, float end, ChannelKind kind)
            {
                var result = new Segment { First = first, Last = end, Start = Value(bone, first, kind), End = Value(bone, end, kind) };
                double aa = 0, ab = 0, bb = 0; var ar = new double[3]; var br = new double[3];
                var constant = Enumerable.Range(0, 3).Select(c => Bits((float)Component(result.Start, c)) == Bits((float)Component(result.End, c))).ToArray();
                foreach (float time in Training.Select(fraction=>first+(end-first)*fraction).Concat(Mandatory(first,end)).Distinct())
                {
                    Charge(1);
                    if (time <= first || time >= end) continue;
                    double t = (time - first) / (double)(end - first), t2 = t * t, t3 = t2 * t;
                    double a = t3 - 2 * t2 + t, b = t3 - t2, h0 = 2 * t3 - 3 * t2 + 1, h1 = 1 - h0;
                    var value = Value(bone, time, kind);
                    aa += a * a; ab += a * b; bb += b * b;
                    for (int channel = 0; channel < 3; channel++)
                    {
                        double residual = Component(value, channel) - h0 * Component(result.Start, channel) - h1 * Component(result.End, channel);
                        constant[channel] &= Bits((float)Component(value, channel)) == Bits((float)Component(result.Start, channel));
                        ar[channel] += a * residual; br[channel] += b * residual;
                    }
                }
                double determinant = aa * bb - ab * ab, span = end - first;
                if (determinant <= 1e-20) return result;
                var outgoing = new float[3]; var incoming = new float[3];
                for (int channel = 0; channel < 3; channel++)
                {
                    outgoing[channel] = constant[channel] ? 0 : (float)((ar[channel] * bb - br[channel] * ab) / determinant / span);
                    incoming[channel] = constant[channel] ? 0 : (float)((br[channel] * aa - ar[channel] * ab) / determinant / span);
                    if (!Finite(outgoing[channel]) || !Finite(incoming[channel]))
                        throw new ArgumentException("Solved native derivatives overflowed their precise float representation.");
                }
                result.Outgoing = new Vector3(outgoing[0], outgoing[1], outgoing[2]);
                result.Incoming = new Vector3(incoming[0], incoming[1], incoming[2]);
                return result;
            }

            private double Measure(int bone, Segment segment, ChannelKind kind)
            {
                double maximum = 0;
                result.FailureWitness=null;
                foreach (float time in Validation.Select(fraction=>segment.First+(segment.Last-segment.First)*fraction).Concat(Mandatory(segment.First,segment.Last)).Distinct())
                {
                    Charge(1);
                    validationTimes.Add(time);
                    var evaluated = segment.At(time); double error;
                    if (kind == ChannelKind.Rotation)
                    {
                        var actual = Quaternion.Normalize(World(parents[bone], time) * Rotation(evaluated));
                        error = Angle(actual, Sample(time).WorldRotations[bone]);
                    }
                    else
                    {
                        var delta = evaluated - Value(bone, time, kind);
                        error = kind == ChannelKind.Translation ? delta.Length() : Math.Max(Math.Abs(delta.X), Math.Max(Math.Abs(delta.Y), Math.Abs(delta.Z)));
                    }
                    if (!Finite(error)) throw new ArgumentException("Adaptive native evaluation produced a nonfinite error.");
                    if(kind==ChannelKind.Rotation && error>=maximum && error>orientationTolerance)
                    {
                        var desired=Sample(time);var parent=World(parents[bone],time);
                        result.FailureWitness="time="+time.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"; actualEuler="+evaluated+
                            "; expectedEuler="+desired.EulerDegrees[bone]+"; localError="+Angle(Rotation(evaluated),Local(desired,bone))+
                            "; parentError="+(parents[bone]<0?0:Angle(parent,desired.WorldRotations[parents[bone]]));
                    }
                    maximum = Math.Max(maximum, error);
                }
                return maximum;
            }

            private IEnumerable<float> Mandatory(float first,float last)
            {
                int low=0,high=mandatory.Length;
                while(low<high){int mid=(low+high)/2;if(mandatory[mid]<=first)low=mid+1;else high=mid;}
                for(int i=low;i<mandatory.Length&&mandatory[i]<last;i++)yield return mandatory[i];
            }

            private Vector3 Value(int bone, float time, ChannelKind kind)
            {
                var desired = Sample(time);
                if (kind == ChannelKind.Translation) return desired.Translations[bone];
                if (kind == ChannelKind.Scale) return desired.Scales[bone];
                if(generatedContinuation&&initialEuler!=null&&time==first)return initialEuler[bone];
                // Follow the sampler's continuous authored local chart. Tiny
                // native parent approximation must not be converted through a
                // fresh inverse-parent Euler extraction at every sample: near
                // a pole, that extraction can create an artificial chart spin
                // despite an almost unchanged physical orientation. Actual
                // hierarchy-world error is still checked by Measure, and only
                // additional native knots can satisfy that unchanged bound.
                if (!generatedContinuation) return desired.EulerDegrees[bone];
                var anchors = continuations[bone];
                int low = 0, high = anchors.Count - 1;
                while (low < high) { int mid = (low + high + 1) / 2; if (anchors[mid].Time <= time) low = mid; else high = mid - 1; }
                Vector3 reference = anchors[low].Euler;
                if (low + 1 < anchors.Count && time != anchors[low].Time)
                    reference = Vector3.Lerp(reference, anchors[low + 1].Euler,
                        (time - anchors[low].Time) / (anchors[low + 1].Time - anchors[low].Time));
                var value = BrawlRetargeter.EulerNear(Local(desired, bone), reference);
                // Keep exact supplied coefficients whenever continuation has
                // not changed their family/winding. Only newly generated
                // aliases may be rebranched, never imported authored curves.
                if (MaximumComponent(value - desired.EulerDegrees[bone]) < .0005) value = desired.EulerDegrees[bone];
                return value;
            }

            private Quaternion Local(AdaptiveRotationPose pose, int bone) => pose.LocalRotations == null
                ? Rotation(pose.EulerDegrees[bone]) : pose.LocalRotations[bone];

            private void PrepareContinuation(int bone)
            {
                var anchors = new List<EulerAnchor> { new EulerAnchor { Time = first, Euler = initialEuler==null?Sample(first).EulerDegrees[bone]:initialEuler[bone] } };
                continuations[bone] = anchors;
                // A sequential lift is independent of asynchronous/adaptive
                // query order. Step doubling prevents an eighth-frame chart
                // guide from taking a shortcut across a near-pole excursion.
                for (int cell = (int)first*8; cell < last * 8; cell++)
                    TraceContinuation(bone, cell / 8f, (cell + 1) / 8f, anchors[anchors.Count - 1].Euler, 0, anchors);
                for (int frame = (int)first; frame <= last; frame++)
                {
                    var original = Sample(frame).EulerDegrees[bone];
                    if (MaximumComponent(Value(bone, frame, ChannelKind.Rotation) - original) >= .0005) result.IntegerEulerRebranches++;
                }
            }

            private void TraceContinuation(int bone, float first, float end, Vector3 previous, int depth, List<EulerAnchor> output)
            {
                result.LastBone = bone; result.LastChannel = "Euler continuation"; result.LastFirstFrame = first;
                result.LastLastFrame = end; result.LastDepth = depth;
                result.DeepestSubdivision = Math.Max(result.DeepestSubdivision, depth);
                Charge(3);
                float middle = first + (end - first) * .5f;
                var lastPose = Sample(end);
                var direct = BrawlRetargeter.EulerNear(Local(lastPose, bone), previous);
                if (middle <= first || middle >= end)
                {
                    output.Add(new EulerAnchor { Time = end, Euler = direct }); return;
                }
                var mid = BrawlRetargeter.EulerNear(Local(Sample(middle), bone), previous);
                var through = BrawlRetargeter.EulerNear(Local(lastPose, bone), mid);
                double guideTravel = MaximumComponent(lastPose.EulerDegrees[bone] - Sample(first).EulerDegrees[bone]);
                // A near-pole excursion can lie between the midpoint and an
                // endpoint. Endpoint branch agreement alone misses it, then
                // nearest-family extraction switches at an arbitrary time.
                // Check the physical orientation of both half-span guides;
                // refine the guide without changing the solved reference.
                bool illConditioned = new[] {previous,mid,through}.Any(v=>Math.Abs(Math.Cos(v.Y*Math.PI/180))<.05);
                bool guideFailure=false;
                if(illConditioned)
                {
                    foreach(var half in new[]{Tuple.Create(first,middle,previous,mid),Tuple.Create(middle,end,mid,through)})
                    {
                        Charge(1);float at=half.Item1+(half.Item2-half.Item1)*.5f;
                        if(at<=half.Item1||at>=half.Item2)continue;
                        var reference=Vector3.Lerp(half.Item3,half.Item4,.5f);
                        guideFailure|=Angle(Rotation(reference),Local(Sample(at),bone))>hierarchyBudgets[bone]*.25;
                    }
                }
                // A generated guide may jump by 180 degrees when it changes
                // equivalent Euler families. That alias must not force endless
                // subdivision; a larger raw step can carry real full winding
                // which quaternion endpoints alone cannot identify.
                bool refine = MaximumComponent(direct - through) > .01 || MaximumComponent(mid - previous) > 45 ||
                    MaximumComponent(through - mid) > 45 || guideTravel > 270 || guideFailure;
                if (refine && depth < MaximumDepth)
                {
                    TraceContinuation(bone, first, middle, previous, depth + 1, output);
                    TraceContinuation(bone, middle, end, output[output.Count - 1].Euler, depth + 1, output);
                }
                else
                {
                    if (refine)
                    {
                        result.LastBone = bone; result.LastChannel = "Euler continuation"; result.LastFirstFrame = first;
                        result.LastLastFrame = end; result.LastDepth = depth;
                        Record(ChannelKind.Rotation, double.PositiveInfinity);
                    }
                    output.Add(new EulerAnchor { Time = middle, Euler = mid });
                    output.Add(new EulerAnchor { Time = end, Euler = through });
                }
            }

            private Quaternion World(int bone, float time)
            {
                if (bone < 0) return Quaternion.Identity;
                long key = ((long)bone << 32) | (uint)Bits(time);
                if (worlds.TryGetValue(key, out var known)) return known;
                Charge(1);
                Quaternion local;
                if (!editable[bone]) local = frozen(bone, time);
                else
                {
                    var curve = rotations[bone];
                    if (curve == null) throw new InvalidOperationException("Adaptive curves must be completed in topological order.");
                    if (curve.Length == 0) local = Rotation(Sample(first).EulerDegrees[bone]);
                    else
                    {
                        int low = 0, high = curve.Length - 1;
                        while (low < high) { int middle = (low + high) / 2; if (time > curve[middle].Last) low = middle + 1; else high = middle; }
                        local = Rotation(curve[low].At(time));
                    }
                }
                if (!Finite(local.X) || !Finite(local.Y) || !Finite(local.Z) || !Finite(local.W) || local.LengthSquared() == 0)
                    throw new ArgumentException("Frozen native local rotations must be finite nonzero quaternions.");
                var world = Quaternion.Normalize(World(parents[bone], time) * local); worlds.Add(key, world); return world;
            }

            private AdaptiveRotationPose Sample(float time)
            {
                int key = Bits(time);
                if (samples.TryGetValue(key, out var value)) return value;
                Charge(parents.Length * 4L);
                value = sampler(time);
                if (value == null || value.EulerDegrees == null || value.WorldRotations == null ||
                    value.EulerDegrees.Length != parents.Length || value.WorldRotations.Length != parents.Length ||
                    value.LocalRotations != null && value.LocalRotations.Length != parents.Length ||
                    value.Translations != null && value.Translations.Length != parents.Length || value.Scales != null && value.Scales.Length != parents.Length)
                    throw new ArgumentException("Solved native pose samples must identify every existing target bone.");
                if (samples.TryGetValue(Bits(first), out var origin) &&
                    ((origin.Translations == null) != (value.Translations == null) || (origin.Scales == null) != (value.Scales == null) ||
                     (origin.LocalRotations == null) != (value.LocalRotations == null)))
                    throw new ArgumentException("Optional native scale/translation sampling must remain consistent for the complete operation.");
                if(origin!=null&&invariants!=null)
                    for(int bone=0;bone<parents.Length;bone++)
                    {
                        bool Same(Vector3 a,Vector3 b)=>Bits(a.X)==Bits(b.X)&&Bits(a.Y)==Bits(b.Y)&&Bits(a.Z)==Bits(b.Z);
                        if(invariants.Translation(bone)&&value.Translations!=null&&!Same(value.Translations[bone],origin.Translations[bone])||
                            invariants.Scale(bone)&&value.Scales!=null&&!Same(value.Scales[bone],origin.Scales[bone]))
                            throw new InvalidOperationException("Solved motion violated its proven native constant-component contract; no candidate is available.");
                    }
                if (value.EulerDegrees.Any(v => !Finite(v.X) || !Finite(v.Y) || !Finite(v.Z)) ||
                    value.WorldRotations.Any(q => !Finite(q.X) || !Finite(q.Y) || !Finite(q.Z) || !Finite(q.W) || q.LengthSquared() == 0) ||
                    value.LocalRotations != null && value.LocalRotations.Any(q => !Finite(q.X) || !Finite(q.Y) || !Finite(q.Z) || !Finite(q.W) || q.LengthSquared() == 0) ||
                    value.Translations != null && value.Translations.Any(v => !Finite(v.X) || !Finite(v.Y) || !Finite(v.Z)) ||
                    value.Scales != null && value.Scales.Any(v => !Finite(v.X) || !Finite(v.Y) || !Finite(v.Z)))
                    throw new ArgumentException("Solved native pose samples must contain finite transforms.");
                value = new AdaptiveRotationPose { EulerDegrees = value.EulerDegrees.ToArray(), WorldRotations = value.WorldRotations.ToArray(),
                    LocalRotations = value.LocalRotations?.ToArray(),
                    Translations = value.Translations?.ToArray(), Scales = value.Scales?.ToArray() };
                samples.Add(key, value); return value;
            }

            private void Charge(long amount)
            {
                token.ThrowIfCancellationRequested();
                if (amount < 0 || amount > maximumWork - work || reserve != null && !reserve(amount)) throw new WorkExhausted();
                work += amount;
            }
        }

        private static AdaptiveVectorKey[] Keys(List<Segment> segments, Vector3 first,float firstTime=0)
        {
            if (segments.Count == 0) return new[] { new AdaptiveVectorKey { Time = firstTime, Value = first } };
            var keys = new List<AdaptiveVectorKey> { new AdaptiveVectorKey { Time = segments[0].First, Value = segments[0].Start,
                IncomingSlope = segments[0].Outgoing, OutgoingSlope = segments[0].Outgoing } };
            foreach (var segment in segments)
            {
                keys[keys.Count - 1].OutgoingSlope = segment.Outgoing;
                keys.Add(new AdaptiveVectorKey { Time = segment.Last, Value = segment.End, IncomingSlope = segment.Incoming, OutgoingSlope = segment.Incoming });
            }
            return keys.ToArray();
        }

        private static Quaternion Rotation(Vector3 value) => BrawlRetargeter.Rotation(new BrawlLib.Internal.Vector3(value.X, value.Y, value.Z));
        private static double Component(Vector3 value, int channel) => channel == 0 ? value.X : channel == 1 ? value.Y : value.Z;
        private static double MaximumComponent(Vector3 value) => Math.Max(Math.Abs(value.X), Math.Max(Math.Abs(value.Y), Math.Abs(value.Z)));
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static double Angle(Quaternion actual, Quaternion desired)
        {
            var delta = Quaternion.Normalize(Quaternion.Inverse(desired) * actual);
            return 2 * Math.Atan2(Math.Sqrt((double)delta.X * delta.X + (double)delta.Y * delta.Y + (double)delta.Z * delta.Z), Math.Abs(delta.W)) * 180 / Math.PI;
        }
        private static float Hermite(float first, float last, float outgoing, float incoming, float offset, float span)
        {
            if (offset == 0) return first; if (offset == span) return last;
            float time = offset / span, inverse = time - 1;
            // Keep the native float evaluator's operation order.
            return first + offset * inverse * (inverse * outgoing + time * incoming) +
                time * time * (3 - 2 * time) * (last - first);
        }
    }
}
