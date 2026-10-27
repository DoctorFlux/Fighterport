using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal static partial class Program
{
    static async Task NativeCompactRuntimeChecks(string modelPath, string stage)
    {
        Directory.CreateDirectory(stage);
        using (var owner = NodeFactory.FromFile(null, modelPath))
        {
            var model = OptimizationWalk(owner).OfType<MDL0Node>().OrderByDescending(m => m.NumVertices).First();
            var names = model.AllBones.Select(b => b.Name).ToArray(); double length = NativeAnimationOptimization.GetBindBoundsDiagonal(model);
            using (var baseline = CompactFixture(20, 3, Enumerable.Range(0, 16).ToArray(), i => i * .125f, i => .125f))
            {
                baseline.Children[0].Name = names.Last(); string key = NativeAnimationOptimization.ContentKey(baseline);
                var options = new NativeAnimationOptimizationOptions { ExactOnly = true, TargetBones = names, LengthScale = length, Budget = new NativeAnimationOptimizationBudget() };
                using (var precise = NativeAnimationOptimization.Prepare(model, baseline, new[] { OptimizationTrack((CHR0EntryNode)baseline.Children[0]) }, options, stage))
                {
                    Check(!precise.HasChanges && precise.Candidate == null, "Compact runtime: unchanged precise preparation is an explicit semantic no-op");
                    int calls = 0;
                    using (var packed = await NativeAnimationOptimization.PrepareCompact(model, baseline, baseline, precise, options, stage,
                        (candidate, token) => { calls++; return Task.FromResult(new NativeCompactCurveCertificate { Accepted = NativeAnimationOptimization.ContentKey(candidate) == key,
                            Report = "Independent exact coefficient identity.", ProbeFrames = new[] { .333, 4.777, 15.125 } }); }))
                    {
                        Check(calls == 0 && packed.HasChanges && packed.ContentKey == key && packed.StorageKey != NativeAnimationOptimization.StorageKey(baseline) && packed.AfterBytes < packed.BeforeBytes,
                            "Compact runtime: format-only optimization independently decodes unchanged coefficients and carries the qualified precise proof without repeating solver work");
                        Check(!packed.UsedApproximateStorage && packed.MaximumEulerError == 0 && packed.MaximumWorldBoneError == 0 && packed.MaximumSkinError == 0 && packed.Candidate != null,
                            "Compact runtime: exact indexed storage retains zero collective native pose/skin error");
                        options.OriginalStoredBytes = packed.AfterBytes; options.OriginalStoredResourceBytes = packed.CandidateResourceBytes;
                        using (var repeat = await NativeAnimationOptimization.PrepareCompact(model, baseline, packed.Candidate, precise, options, stage,
                            (candidate, token) => Task.FromResult(new NativeCompactCurveCertificate { Accepted = NativeAnimationOptimization.ContentKey(candidate) == key, Report = "Independent exact coefficient identity." })))
                            Check(!repeat.HasChanges && repeat.AfterBytes == packed.AfterBytes && repeat.StorageKey == packed.StorageKey,
                                "Compact runtime: reoptimizing an already packed current animation does not reapply a format-only result or grow it");
                        options.OriginalStoredBytes = null; options.OriginalStoredResourceBytes = null;
                    }
                    using (var rejected = await NativeAnimationOptimization.PrepareCompact(model, baseline, baseline, precise, options, stage,
                        (candidate, token) => Task.FromResult(new NativeCompactCurveCertificate { Accepted = false, Report = "Independent deliberate proof rejection." })))
                        Check(rejected.HasChanges && rejected.ContentKey == key && rejected.Candidate != null,
                            "Compact runtime: bit-exact storage reuses existing proof and never asks an unnecessary approximation certificate");
                    var exhausted = new NativeAnimationOptimizationOptions { ExactOnly = true, TargetBones = names, LengthScale = length,
                        Budget = new NativeAnimationOptimizationBudget { Samples = NativeAnimationOptimizationBudget.MaximumSamples } };
                    using (var fallback = await NativeAnimationOptimization.PrepareCompact(model, baseline, baseline, precise, exhausted, stage,
                        (candidate, token) => Task.FromResult(new NativeCompactCurveCertificate { Accepted = true, Report = "Independent exact coefficient identity." })))
                        Check(fallback.HasChanges && fallback.BudgetSamples == NativeAnimationOptimizationBudget.MaximumSamples && fallback.MaximumEulerError == 0,
                            "Compact runtime: bit-exact packing remains available after the solver budget is consumed because every decoded tuple retains its existing proof");
                }
                Check(NativeAnimationOptimization.ContentKey(baseline) == key, "Compact runtime: successful, failed, repeated and cancelled packing preserve exact original content");
            }

            using (var baseline = CompactFixture(20, 3, Enumerable.Range(0, 16).ToArray(), i => .0012345f + i * .1371397f, i => .1234567f))
            {
                baseline.Children[0].Name = names.Last(); string key = NativeAnimationOptimization.ContentKey(baseline);
                var options = new NativeAnimationOptimizationOptions { ExactOnly = false, TargetBones = names, LengthScale = length, Budget = new NativeAnimationOptimizationBudget() };
                using (var precise = NativeAnimationOptimization.Prepare(model, baseline, new[] { OptimizationTrack((CHR0EntryNode)baseline.Children[0]) }, options, stage))
                using (var packed = await NativeAnimationOptimization.PrepareCompact(model, baseline, baseline, precise, options, stage,
                    (candidate, token) => Task.FromResult(CompactIndependentBound(baseline, candidate))))
                {
                    Check(packed.HasChanges && packed.ContentKey != key && packed.AfterBytes < packed.BeforeBytes && packed.Report.Contains("accepted quantized groups: 1") &&
                        packed.MaximumEulerError > 0 && packed.MaximumEulerError <= .01 && packed.MaximumOrientationError <= .01 && packed.MaximumWorldBoneError <= .0001 * length && packed.MaximumSkinError <= .0001 * length,
                        "Compact runtime: independently bounded quantization shrinks actual bytes and passes cumulative native Euler/orientation/hierarchy/skin limits");
                    Check(packed.Report.Contains("Independent analytical Hermite"), "Compact runtime: continuous scalar and sampled native claims remain separately identified");
                    Check(packed.UsedApproximateStorage, "Compact runtime: accepted approximate storage is explicitly identified independently of curve reduction");
                    using (var choice = await NativeAnimationOptimizationPreparation.Prepare(true, precise.BeforeBytes, (reduced, precision) =>
                    {
                        if (reduced)
                        {
                            var rejected = new InvalidDataException("Independent rejected Hermite reduction; valid storage remains available.");
                            rejected.Data["OptimizationQuality"] = true; throw rejected;
                        }
                        return Task.FromResult(new NativeAnimationOptimizationResult { Candidate = NativeAnimationOptimization.Copy(packed.Candidate),
                            HasChanges = packed.HasChanges, AfterBytes = packed.AfterBytes, AfterKeys = packed.AfterKeys,
                            UsedApproximateStorage = packed.UsedApproximateStorage });
                    }, CancellationToken.None))
                        Check(choice.Result != null && choice.HighQuality && choice.Result.UsedApproximateStorage && choice.Report.Contains("Exact cleanup + bounded storage") &&
                            choice.Report.Contains("Chosen: verified High quality candidate") && !choice.Report.Contains("Chosen: verified Exact fallback"),
                            "Compact runtime: valid quantized storage on exact curves remains High quality when subsequent reduction is rejected; it is never labelled Exact fallback");
                }
                using (var precise = NativeAnimationOptimization.Prepare(model, baseline, new[] { OptimizationTrack((CHR0EntryNode)baseline.Children[0]) }, options, stage))
                using (var rejected = await NativeAnimationOptimization.PrepareCompact(model, baseline, baseline, precise, options, stage,
                    (candidate, token) => Task.FromResult(new NativeCompactCurveCertificate { Accepted = false, Report = "Independent deliberate whole-batch rejection." })))
                    Check(!rejected.HasChanges && rejected.Report.Contains("lossless/precise fallback"),
                        "Compact runtime: rejected quantized batches preserve the verified precise result and explicitly report conservative fallback");
                using (var precise = NativeAnimationOptimization.Prepare(model, baseline, new[] { OptimizationTrack((CHR0EntryNode)baseline.Children[0]) }, options, stage))
                using (var cancel = new CancellationTokenSource())
                {
                    bool stopped = false;
                    try
                    {
                        using (var ignored = await NativeAnimationOptimization.PrepareCompact(model, baseline, baseline, precise, options, stage,
                            (candidate, token) => { cancel.Cancel(); token.ThrowIfCancellationRequested(); return Task.FromResult(new NativeCompactCurveCertificate()); }, cancel.Token)) { }
                    }
                    catch (OperationCanceledException) { stopped = true; }
                    Check(stopped && NativeAnimationOptimization.ContentKey(baseline) == key,
                        "Compact runtime: cancellation during approximation certification publishes no result and preserves the owned baseline");
                }
                using (var precise = NativeAnimationOptimization.Prepare(model, baseline, new[] { OptimizationTrack((CHR0EntryNode)baseline.Children[0]) }, options, stage))
                {
                    var exhausted = new NativeAnimationOptimizationOptions { ExactOnly = false, TargetBones = names, LengthScale = length,
                        Budget = new NativeAnimationOptimizationBudget { Samples = NativeAnimationOptimizationBudget.MaximumSamples } };
                    using (var fallback = await NativeAnimationOptimization.PrepareCompact(model, baseline, baseline, precise, exhausted, stage,
                        (candidate, token) => Task.FromResult(CompactIndependentBound(baseline, candidate))))
                        Check(!fallback.HasChanges && fallback.Report.Contains("shared operation budget") && fallback.BudgetSamples == NativeAnimationOptimizationBudget.MaximumSamples,
                            "Compact runtime: quantization budget exhaustion returns a fully verified unchanged fallback and reports its consumed work");
                }
            }
            await NativeCompactSelectiveRuntimeChecks(model, names, length, stage);
        }
        Check(!Directory.GetDirectories(stage, "compact-*").Any(), "Compact runtime: independent candidate staging is cleaned after success, rejection and cancellation");
    }

    static void CompactAddFixtureTrack(CHR0Node clip, string bone)
    {
        var entry = clip.CreateEntry(bone);
        entry.Keyframes.SetFrameValue(4, 0, 0, true)._tangent = 0;
        entry.Keyframes.SetFrameValue(5, 0, 0, true)._tangent = 0;
        for (int i = 0; i < 16; i++) entry.Keyframes.SetFrameValue(3, i, .0012345f + i * .1371397f, true)._tangent = .1234567f;
        entry.Keyframes.PreserveInterpolation = true; PreciseCHR0EntryNode.Promote(clip);
    }

    static bool CompactBoneCoefficientsEqual(CHR0Node original, CHR0Node result, string bone)
    {
        var left = original.Children.OfType<CHR0EntryNode>().Single(e => e.Name == bone);
        var right = result.Children.OfType<CHR0EntryNode>().Single(e => e.Name == bone);
        for (int channel = 0; channel < 9; channel++)
        {
            var a = left.KeyArrays[channel].Entries.ToArray(); var b = right.KeyArrays[channel].Entries.ToArray();
            if (a.Length != b.Length || a.Where((k, i) => k._index != b[i]._index || ReplayFloatBits(k._value) != ReplayFloatBits(b[i]._value) ||
                ReplayFloatBits(k._tangent) != ReplayFloatBits(b[i]._tangent)).Any()) return false;
        }
        return true;
    }

    static async Task NativeCompactSelectiveRuntimeChecks(MDL0Node model, string[] names, double length, string stage)
    {
        using (var baseline = CompactFixture(20, 3, Enumerable.Range(0, 16).ToArray(), i => .0012345f + i * .1371397f, i => .1234567f))
        {
            string eligible = names.Last(), blocked = names[names.Length - 2]; baseline.Children[0].Name = eligible;
            CompactAddFixtureTrack(baseline, blocked); string original = NativeAnimationOptimization.ContentKey(baseline);
            var tracks = baseline.Children.OfType<CHR0EntryNode>().Select(OptimizationTrack).ToArray();
            NativeAnimationOptimizationOptions Options() => new NativeAnimationOptimizationOptions { ExactOnly = false, TargetBones = names,
                LengthScale = length, Budget = new NativeAnimationOptimizationBudget() };
            var options = Options(); int calls = 0;
            using (var precise = NativeAnimationOptimization.Prepare(model, baseline, tracks, options, stage))
            using (var packed = await NativeAnimationOptimization.PrepareCompact(model, baseline, baseline, precise, options, stage,
                (candidate, token) =>
                {
                    calls++;
                    return Task.FromResult(CompactBoneCoefficientsEqual(baseline, candidate, blocked) ? CompactIndependentBound(baseline, candidate) :
                        new NativeCompactCurveCertificate { Accepted = false, Report = "Independent protected fixture branch rejects changed coefficients." });
                }))
            {
                Check(packed.UsedApproximateStorage && packed.HasChanges && packed.AfterBytes < packed.BeforeBytes && calls >= 4 && calls <= 24 &&
                    packed.Report.Contains("accepted quantized groups: 1") && packed.Report.Contains("selective cumulative attempts:"),
                    "Compact selective: a rejected complete batch retains a useful independently verified subset under the unchanged attempt and aggregate budgets");
                Check(CompactBoneCoefficientsEqual(baseline, packed.Candidate, blocked) && !CompactBoneCoefficientsEqual(baseline, packed.Candidate, eligible) &&
                    packed.MaximumOrientationError <= .01 && packed.MaximumSkinError <= .0001 * length,
                    "Compact selective: the failing branch stays precise while the complete cumulative native hierarchy and skin are verified against the immutable original");
            }

            options = Options(); calls = 0;
            using (var precise = NativeAnimationOptimization.Prepare(model, baseline, tracks, options, stage))
            using (var limited = await NativeAnimationOptimization.PrepareCompact(model, baseline, baseline, precise, options, stage,
                (candidate, token) =>
                {
                    calls++;
                    if (calls >= 4) return Task.FromResult(new NativeCompactCurveCertificate { BudgetExhausted = true, Report = "Independent exhausted scalar partition budget." });
                    return Task.FromResult(CompactBoneCoefficientsEqual(baseline, candidate, blocked) ? CompactIndependentBound(baseline, candidate) :
                        new NativeCompactCurveCertificate { Accepted = false, Report = "Independent branch rejection." });
                }))
                Check(limited.UsedApproximateStorage && limited.HasChanges && limited.Report.Contains("shared operation budget") &&
                    CompactBoneCoefficientsEqual(baseline, limited.Candidate, blocked) && !CompactBoneCoefficientsEqual(baseline, limited.Candidate, eligible),
                    "Compact selective: budget exhaustion after a successful subset returns only that completely verified retained-baseline candidate");

            options = Options(); calls = 0;
            using (var precise = NativeAnimationOptimization.Prepare(model, baseline, tracks, options, stage))
            using (var cancel = new CancellationTokenSource())
            {
                bool stopped = false;
                try
                {
                    using (var ignored = await NativeAnimationOptimization.PrepareCompact(model, baseline, baseline, precise, options, stage,
                        (candidate, token) =>
                        {
                            calls++; if (calls >= 4) { cancel.Cancel(); token.ThrowIfCancellationRequested(); }
                            return Task.FromResult(CompactBoneCoefficientsEqual(baseline, candidate, blocked) ? CompactIndependentBound(baseline, candidate) :
                                new NativeCompactCurveCertificate { Accepted = false, Report = "Independent branch rejection." });
                        }, cancel.Token)) { }
                }
                catch (OperationCanceledException) { stopped = true; }
                Check(stopped && calls == 4 && NativeAnimationOptimization.ContentKey(baseline) == original,
                    "Compact selective: cancellation after a verified subset returns no operation result and preserves the original native clip");
            }
            Check(NativeAnimationOptimization.ContentKey(baseline) == original, "Compact selective: successful, partial and cancelled searches never mutate their frozen input");
        }

        using (var baseline = CompactFixture(20, 3, Enumerable.Range(0, 16).ToArray(), i => .0012345f + i * .1371397f, i => .1234567f))
        {
            baseline.Children[0].Name = names[0]; foreach (string name in names.Skip(1).Take(19)) CompactAddFixtureTrack(baseline, name);
            string original = NativeAnimationOptimization.ContentKey(baseline); int calls = 0;
            var options = new NativeAnimationOptimizationOptions { ExactOnly = false, TargetBones = names, LengthScale = length, Budget = new NativeAnimationOptimizationBudget() };
            using (var precise = NativeAnimationOptimization.Prepare(model, baseline, baseline.Children.OfType<CHR0EntryNode>().Select(OptimizationTrack).ToArray(), options, stage))
            using (var packed = await NativeAnimationOptimization.PrepareCompact(model, baseline, baseline, precise, options, stage,
                (candidate, token) => { calls++; return Task.FromResult(new NativeCompactCurveCertificate { Accepted = false, Report = "Independent rejection of every proposed group." }); }))
                Check(calls == 24 && !packed.HasChanges && !packed.UsedApproximateStorage && packed.Report.Contains("attempt limit") &&
                    NativeAnimationOptimization.ContentKey(baseline) == original,
                    "Compact selective: large all-rejected searches stop at 24 complete attempts and return the fully verified unchanged result");
        }
    }

    // Independent inequality: on a Hermite span, |h00|+|h01|=1,
    // |h10|+|h11|<=1/4; derivatives give bounds 3/span and 1.
    // These procedural fixtures preserve every knot and need no reducer code.
    static NativeCompactCurveCertificate CompactIndependentBound(CHR0Node baseline, CHR0Node candidate)
    {
        bool accepted = baseline.Children.Count == candidate.Children.Count; double maximumValue = 0, maximumDerivative = 0;
        for (int entry = 0; accepted && entry < baseline.Children.Count; entry++)
        for (int channel = 0; accepted && channel < 9; channel++)
        {
            var left = ((CHR0EntryNode)baseline.Children[entry]).KeyArrays[channel].Entries.ToArray();
            var right = ((CHR0EntryNode)candidate.Children[entry]).KeyArrays[channel].Entries.ToArray();
            if (left.Length != right.Length || left.Where((k, i) => k._index != right[i]._index).Any()) { accepted = false; break; }
            double valueError = left.Select((k, i) => Math.Abs((double)k._value - right[i]._value)).DefaultIfEmpty(0).Max();
            double tangentError = left.Select((k, i) => Math.Abs((double)k._tangent - right[i]._tangent)).DefaultIfEmpty(0).Max();
            if (left.Length == 0) continue;
            var spans = left.Skip(1).Select((k, i) => k._index - left[i]._index).Where(s => s > 0).ToList();
            if (baseline.Loop && left.Length > 1) spans.Add(((CHR0EntryNode)baseline.Children[entry]).Keyframes.FrameLimit - left.Last()._index + left.First()._index);
            double value = valueError + spans.DefaultIfEmpty(0).Max() * tangentError * .25;
            double derivative = (spans.Count == 0 ? 0 : 3d / spans.Min() * valueError) + tangentError;
            double valueLimit = channel >= 3 && channel < 6 ? .01 : .00001, derivativeLimit = channel >= 3 && channel < 6 ? .05 : .00001;
            maximumValue = Math.Max(maximumValue, value); maximumDerivative = Math.Max(maximumDerivative, derivative);
            if (value > valueLimit || derivative > derivativeLimit) accepted = false;
        }
        return new NativeCompactCurveCertificate { Accepted = accepted, Report = "Independent analytical Hermite basis value/derivative bounds: " + maximumValue + "/" + maximumDerivative,
            ProbeFrames = new[] { .333, 4.777, 15.125, 18.625 } };
    }
}
