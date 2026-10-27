using BrawlCrate.Studio;
using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal static partial class Program
{
    // Independent adapter checks: authored linear/constant functions and deliberate
    // contradictions are constructed here, without calling the Core reducer.
    static void NativeOptimizationChecks(string modelPath, string output)
    {
        string stage = Path.Combine(output, "native-optimization-checks"); Directory.CreateDirectory(stage);
        using (var root = NodeFactory.FromFile(null, modelPath))
        {
            var model = OptimizationWalk(root).OfType<MDL0Node>().OrderByDescending(m => m.NumVertices).First();
            var names = model.AllBones.Select(b => b.Name).ToArray(); string bone = names[0];
            var options = new NativeAnimationOptimizationOptions { ExactOnly = true, TargetBones = names,
                LengthScale = NativeAnimationOptimization.GetBindBoundsDiagonal(model), ProbeFrames = new[] { .19, 1.63, 3.89, 5 - .0001 } };
            Check(options.LengthScale > 0, "Native optimizer: target scale comes from actual independent native bind geometry");
            using (var before = OptimizationFixture(bone, "Exact independent linear fixture"))
            {
                var source = (CHR0EntryNode)before.Children[0];
                OptimizationReplace(source, 6, Enumerable.Range(0, 5).Select(i => OptimizationKey(i, i * .125f, .125f)).ToArray());
                var donor = OptimizationFixed(before, "Unmatched donor-only fixture", new float[] { 1, 1, 1, 40, 0, 0, 0, 0, 0 });
                var reduced = OptimizationTrack(source); reduced.Channels[6] = new[] { OptimizationKey(0, 0, .125f), OptimizationKey(4, .5f, .125f) };
                string prior = NativeAnimationOptimization.ContentKey(before);
                using (var result = NativeAnimationOptimization.Prepare(model, before, new[] { reduced }, options, stage))
                {
                    Check(result.HasChanges && result.Candidate != null, "Native optimizer: equivalent authored linear reduction produces independent native candidate");
                    Check(result.AfterBytes < result.BeforeBytes && result.AfterKeys < result.BeforeKeys && result.AfterTracks == 1 && result.BeforeTracks == 2,
                        "Native optimizer: actual precise CHR0 bytes/keys shrink and only absent-target donor track is omitted");
                    var candidate = (CHR0EntryNode)result.Candidate.Children[0];
                    Check(candidate.Name == bone && candidate.ScaleCompensateApply && candidate.ScaleCompensateParent && candidate.ClassicScaleOff &&
                        !candidate.UseModelScale && !candidate.UseModelRotate && !candidate.UseModelTranslate,
                        "Native optimizer: all six semantic flags and surviving native order retained");
                    Check(result.Candidate.Name == before.Name && result.Candidate.Version == 5 && result.Candidate.FrameCount == 5 && !result.Candidate.Loop &&
                        result.Candidate.OriginalPath == before.OriginalPath && result.Candidate.UserEntries[0].Entries.SequenceEqual(new[] { "Unchanged independent native metadata" }),
                        "Native optimizer: CHR0 v5 timing/path/user metadata independently reopens exactly");
                    Check(result.Samples >= 57 && result.MaximumEulerError == 0 && result.MaximumScaleError == 0 && result.MaximumLocalTranslationError == 0 &&
                        result.MaximumWorldBoneError == 0 && result.MaximumSkinError == 0 && result.MaximumTranslationDerivativeError < 1e-12,
                        "Native optimizer: withheld/Core/adaptive samples measure every native bone and full skin with independently expected zero linear error");
                    Check(result.Report.Contains("native samples") && result.MaximumTranslationDerivativeError < 1e-12,
                        "Native optimizer: Core's non-loop end-exclusive N-.0001 witness verifies the final held display interval");
                    Check(result.ContentKey == NativeAnimationOptimization.ContentKey(result.Candidate) && result.EncodedSha256.Length == 64,
                        "Native optimizer: independent decoded candidate retains full content key and encoded byte hash");
                    File.WriteAllText(Path.Combine(stage, "linear-report.txt"), result.Report);
                }
                Check(prior == NativeAnimationOptimization.ContentKey(before) && before.Children[1] == donor,
                    "Native optimizer: original snapshot, metadata, donor track and native curves never mutate");

                options.ProtectedBones = new[] { bone };
                OptimizationReject(() => NativeAnimationOptimization.Prepare(model, before, new[] { reduced }, options, stage), "protected", "Native optimizer: protected raw keys cannot be reduced");
                options.ProtectedBones = new[] { donor.Name };
                OptimizationReject(() => NativeAnimationOptimization.Prepare(model, before, new[] { reduced }, options, stage), "absent", "Native optimizer: protected absent-target track is a binding contradiction");
                options.ProtectedBones = new string[0]; options.MandatoryFrames = new[] { 2 };
                OptimizationReject(() => NativeAnimationOptimization.Prepare(model, before, new[] { reduced }, options, stage), "mandatory", "Native optimizer: mandatory native fit knot/tangent cannot disappear");
                options.MandatoryFrames = new int[0]; options.OriginalStoredBytes = 1;
                OptimizationReject(() => NativeAnimationOptimization.Prepare(model, before, new[] { reduced }, options, stage), "larger", "Native optimizer: recorded clean stored-resource byte cap independently rejects growth");
                options.OriginalStoredBytes = null;
                var tampered = OptimizationTrack(source); tampered.Channels[6] = new[] { OptimizationKey(0, 1, 0), OptimizationKey(4, 1, 0) };
                OptimizationReject(() => NativeAnimationOptimization.Prepare(model, before, new[] { tampered }, options, stage), "local translation", "Native optimizer: independently encoded but tampered motion fails native pose quality gate");
                var badEncoding = OptimizationTrack(source); badEncoding.Channels[0] = new[] { OptimizationKey(2, 1, 0) };
                OptimizationReject(() => NativeAnimationOptimization.Prepare(model, before, new[] { badEncoding }, options, stage), "frame zero", "Native optimizer: noncanonical fixed-channel storage cannot silently discard key time");
                var cancelled = new CancellationTokenSource(); cancelled.Cancel(); bool cancellation = false;
                try { using (var ignored = NativeAnimationOptimization.Prepare(model, before, new[] { reduced }, options, stage, cancelled.Token)) { } }
                catch (OperationCanceledException) { cancellation = true; }
                Check(cancellation && prior == NativeAnimationOptimization.ContentKey(before), "Native optimizer: cancellation returns no candidate and preserves baseline");
            }

            using (var before = OptimizationFixture(bone, "Mirrored zero-scale constant fixture"))
            {
                var source = (CHR0EntryNode)before.Children[0];
                source.UseModelScale = true; source.UseModelRotate = true; source.UseModelTranslate = true;
                float[] values = { -2, 0, 3, 360, 0, 0, .125f, .25f, .5f };
                for (int channel = 0; channel < 9; channel++) OptimizationReplace(source, channel,
                    Enumerable.Range(0, 5).Select(i => OptimizationKey(i, values[channel], 0)).ToArray());
                var reduced = OptimizationTrack(source);
                for (int channel = 0; channel < 9; channel++) reduced.Channels[channel] = new[] { OptimizationKey(0, values[channel], 0) };
                using (var result = NativeAnimationOptimization.Prepare(model, before, new[] { reduced }, options, stage))
                {
                    Check(result.HasChanges && result.AfterKeys == 9 && result.AfterBytes < result.BeforeBytes,
                        "Native optimizer: exact zero/nonzero constant channels collapse to canonical fixed values");
                    Check(result.MaximumScaleError <= .00001 && result.MaximumOrientationError <= .01 && result.MaximumScaleDerivativeError < 1e-12,
                        "Native optimizer: mirrored/nonuniform/zero native scales and full Euler turns survive without world decomposition");
                    var native = (CHR0EntryNode)result.Candidate.Children[0];
                    Check(native.UseModelScale && native.UseModelRotate && native.UseModelTranslate,
                        "Native optimizer: authored model-fallback semantic flags survive independently of viewport evaluation");
                    File.WriteAllText(Path.Combine(stage, "mirrored-constant-report.txt"), result.Report);
                }
                var lostTurn = OptimizationTrack(source); lostTurn.Channels[3] = new[] { OptimizationKey(0, 0, 0) };
                OptimizationReject(() => NativeAnimationOptimization.Prepare(model, before, new[] { lostTurn }, options, stage), "Euler", "Native optimizer: equal endpoint orientation cannot erase an authored full turn");
            }

            using (var before = OptimizationFixture(bone, "Equal knots nonconstant cubic"))
            {
                var source = (CHR0EntryNode)before.Children[0];
                OptimizationReplace(source, 6, new[] { OptimizationKey(0, 0, 1), OptimizationKey(4, 0, -1) });
                var flattened = OptimizationTrack(source); flattened.Channels[6] = new[] { OptimizationKey(0, 0, 0) };
                OptimizationReject(() => NativeAnimationOptimization.Prepare(model, before, new[] { flattened }, options, stage), "derivative", "Native optimizer: equal-valued Hermite knots with nonzero tangents cannot be treated as constant");
            }

            using (var before = OptimizationFixture(bone, "Native scale sharing contradiction"))
            {
                var source = (CHR0EntryNode)before.Children[0];
                for (int channel = 0; channel < 3; channel++) OptimizationReplace(source, channel,
                    new[] { OptimizationKey(0, 1, channel), OptimizationKey(4, 2, -channel) });
                OptimizationReject(() => NativeAnimationOptimization.Prepare(model, before, new ControlTrack[0], options, stage), "baseline", "Native optimizer: independent I12 reopen rejects scale-isotropy tangent loss");
            }

            using (var before = OptimizationFixture(bone, "Candidate scale sharing contradiction"))
            {
                var changed = OptimizationTrack((CHR0EntryNode)before.Children[0]);
                for (int channel = 0; channel < 3; channel++) changed.Channels[channel] = new[] { OptimizationKey(0, 1, channel), OptimizationKey(4, 2, -channel) };
                OptimizationReject(() => NativeAnimationOptimization.Prepare(model, before, new[] { changed }, options, stage), "encode changed",
                    "Native optimizer: candidate-only scale tangent sharing fails independent encode verification");
            }

            using (var before = OptimizationFixture(bone, "Native explicit loop closing sample"))
            {
                before.Loop = true; var source = (CHR0EntryNode)before.Children[0];
                OptimizationReplace(source, 6, Enumerable.Range(0, 6).Select(i => OptimizationKey(i, i * .125f, .125f)).ToArray());
                var reduced = OptimizationTrack(source); reduced.Channels[6] = new[] { OptimizationKey(0, 0, .125f), OptimizationKey(5, .625f, .125f) };
                options.MandatoryFrames = new[] { 0, 5 };
                using (var result = NativeAnimationOptimization.Prepare(model, before, new[] { reduced }, options, stage))
                    Check(result.HasChanges && result.Candidate.Loop && result.Candidate.FrameCount == 5 && result.MaximumSkinError <= options.LengthScale * .0001,
                        "Native optimizer: original loop flag/frame-limit and explicit native closing sample remain exact");
                options.MandatoryFrames = new int[0];
            }

            using (var before = OptimizationFixture(bone, "Missing high quality target geometry scale"))
            {
                double length = options.LengthScale; options.ExactOnly = false; options.LengthScale = 0;
                OptimizationReject(() => NativeAnimationOptimization.Prepare(model, before, new ControlTrack[0], options, stage), "positive verified",
                    "Native optimizer: high quality mode cannot guess an unavailable geometry scale");
                options.ExactOnly = true; options.LengthScale = length;
            }

            using (var before = OptimizationFixture(bone, "Actual candidate growth"))
            {
                var source = (CHR0EntryNode)before.Children[0]; var grown = OptimizationTrack(source);
                for (int channel = 0; channel < 9; channel++) grown.Channels[channel] = Enumerable.Range(0, 5).Select(i => OptimizationKey(i, channel < 3 ? 1 : 0, 0)).ToArray();
                OptimizationReject(() => NativeAnimationOptimization.Prepare(model, before, new[] { grown }, options, stage), "larger", "Native optimizer: actual encoded growth rejects even when posed values are equivalent");
                options.OriginalStoredBytes = 1;
                using (var result = NativeAnimationOptimization.Prepare(model, before, new[] { OptimizationTrack(source) }, options, stage))
                    Check(!result.HasChanges && result.Candidate == null && result.Report.Contains("No useful saving"), "Native optimizer: unchanged curves are an explicit no-op even when original packed encoding was smaller");
                options.OriginalStoredBytes = null;
            }

            using (var before = OptimizationFixture(bone, "Paired native knot fixture"))
            {
                var source = (CHR0EntryNode)before.Children[0]; OptimizationReplace(source, 6,
                    new[] { OptimizationKey(0, 0, 0), OptimizationKey(2, 1, 0), OptimizationKey(2, 2, 0), OptimizationKey(4, 3, 0) });
                var changed = OptimizationTrack(source); changed.Channels[6] = new[] { OptimizationKey(0, 0, 0), OptimizationKey(2, 1, 0), OptimizationKey(4, 3, 0) };
                OptimizationReject(() => NativeAnimationOptimization.Prepare(model, before, new[] { changed }, options, stage), "discontinuous", "Native optimizer: native incoming/outgoing duplicate knot sides remain distinct and protected");
            }
            int vertexCount = model.Objects.OfType<MDL0ObjectNode>().Sum(o => o.Vertices?.Count ?? 0);
            var sharedBudget = new NativeAnimationOptimizationBudget();
            sharedBudget.Consume(names.Length, vertexCount); sharedBudget.Consume(names.Length, vertexCount);
            sharedBudget.Require(3, names.Length, vertexCount, "independent retry preflight");
            Check(sharedBudget.Samples == 2 && sharedBudget.PoseEvaluations == 4 && sharedBudget.BoneVisits == 4L * names.Length && sharedBudget.VertexVisits == 4L * vertexCount,
                "Native optimizer: shared aggregate counts logical witnesses and both native evaluations; preflight does not consume work");
            foreach (string counter in new[] { "Samples", "BoneVisits", "VertexVisits" })
            {
                var exhausted = new NativeAnimationOptimizationBudget();
                if (counter == "Samples") exhausted.Samples = NativeAnimationOptimizationBudget.MaximumSamples;
                if (counter == "BoneVisits") exhausted.BoneVisits = NativeAnimationOptimizationBudget.MaximumBoneVisits;
                if (counter == "VertexVisits") exhausted.VertexVisits = NativeAnimationOptimizationBudget.MaximumVertexVisits;
                bool rejected = false;
                try { exhausted.Consume(names.Length, vertexCount); }
                catch (InvalidDataException error)
                {
                    rejected = (string)error.Data["OptimizationBudget"] == counter && !error.Data.Contains("OptimizationQuality") &&
                        Convert.ToInt64(error.Data["RequiredTotal"]) > Convert.ToInt64(error.Data["Limit"]) &&
                        error.Message.Contains("total required") && error.Message.Contains("already consumed");
                }
                Check(rejected, "Native optimizer: exhausted " + counter + " budget reports actual required/consumed counts without being a retryable quality failure");
            }
            NativeOptimizationQualityChecks(model, stage, names, options.LengthScale);
            NativeOptimizationPreparationChecks(model, stage, names, options.LengthScale);
            NativeOptimizationNextBakeChecks(modelPath, model, stage);
            NativeOptimizationResourceSizeChecks(model, stage, names, options.LengthScale);
            NativeOptimizationMetadataChecks(names[0], stage);
            Check(!Directory.GetFiles(stage, "optimization-*.chr0").Any(), "Native optimizer: success/reject/cancel verification files are cleaned without retained input copies");
        }
    }

    static NativeAnimationOptimizationOptions OptimizationQualityOptions(string[] names, double length, bool quality, NativeAnimationOptimizationBudget budget = null)
    {
        return new NativeAnimationOptimizationOptions { ExactOnly = !quality, LengthScale = length, TargetBones = names, Budget = budget,
            ProbeFrames = new[] { .19, 1.63, 3.89, 5 - .0001,
                BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(2d) - 1), BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(2d) + 1),
                (double)BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(2f), 0) - 1), 0),
                (double)BitConverter.ToSingle(BitConverter.GetBytes(BitConverter.ToInt32(BitConverter.GetBytes(2f), 0) + 1), 0) } };
    }

    static void NativeOptimizationQualityChecks(MDL0Node model, string stage, string[] names, double length)
    {
        var options = OptimizationQualityOptions(names, length, true);
        using (var before = OptimizationFixture(names[0], "Independent accepted nonzero approximation"))
        {
            var entry = (CHR0EntryNode)before.Children[0];
            OptimizationReplace(entry, 6, new[] { OptimizationKey(0, 0, 0), OptimizationKey(2, .00005f, 0), OptimizationKey(4, 0, 0) });
            var reduced = OptimizationTrack(entry); reduced.Channels[6] = new[] { OptimizationKey(0, 0, 0), OptimizationKey(4, 0, 0) };
            string original = NativeAnimationOptimization.ContentKey(before);
            using (var result = NativeAnimationOptimization.Prepare(model, before, new[] { reduced }, options, stage))
            {
                Check(result.HasChanges && result.AfterKeys < result.BeforeKeys && result.AfterBytes < result.BeforeBytes && result.MaximumLocalTranslationError > 0 && result.MaximumSkinError > 0,
                    "Native HQ: retained-endpoint approximation produces real nonzero local/full-skin errors and actual native savings");
                Check(result.MaximumLocalTranslationError <= Math.Min(.0002, .0001 * length) && result.MaximumWorldBoneError <= .0001 * length && result.MaximumSkinError <= .0001 * length &&
                    result.MaximumTranslationDerivativeError <= Math.Min(.0002, .0001 * length) && result.MaximumSkinDerivativeError <= .0001 * length,
                    "Native HQ: independently authored small pulse passes unchanged frozen native value and derivative bounds");
                Check(result.Samples > 80 && result.Report.Contains("1/64") && NativeAnimationOptimization.ContentKey(before) == original,
                    "Native HQ: double/float-adjacent knot witnesses and non-loop end hold do not create spurious tiny-gap derivatives or mutate the source");
                File.WriteAllText(Path.Combine(stage, "hq-nonzero-report.txt"), result.Report);
            }
        }

        using (var before = OptimizationFixture(names[0], "Independent derivative-only native contradiction"))
        {
            var entry = (CHR0EntryNode)before.Children[0];
            OptimizationReplace(entry, 6, new[] { OptimizationKey(0, 0, .0003f), OptimizationKey(2, 0, .0003f), OptimizationKey(4, 0, .0003f) });
            var reduced = OptimizationTrack(entry); reduced.Channels[6] = new[] { OptimizationKey(0, 0, .0003f), OptimizationKey(4, 0, .0003f) };
            // For equal endpoint slopes m and zero values, max |H| is
            // |m|*span/(6*sqrt(3)). The sum bounds this difference everywhere.
            double independentValueBound = .0003 * (2 + 4) / (6 * Math.Sqrt(3));
            Check(independentValueBound < Math.Min(.0002, .0001 * length), "Native HQ: independent Hermite extrema prove the derivative-only witness remains inside its translation value bound");
            var budget = new NativeAnimationOptimizationBudget(); options.Budget = budget;
            bool rejected = false;
            try { using (var result = NativeAnimationOptimization.Prepare(model, before, new[] { reduced }, options, stage)) { } }
            catch (InvalidDataException error)
            {
                rejected = Equals(error.Data["OptimizationQuality"], true) && ((string)error.Data["Metric"]).Contains("derivative") &&
                    error.Data["Bone"] != null && error.Data["Frame"] is double && budget.Samples > 0 && Convert.ToInt64(error.Data["BudgetSamples"]) == budget.Samples;
                File.WriteAllText(Path.Combine(stage, "hq-derivative-rejection.txt"), error.Message);
            }
            Check(rejected, "Native HQ: derivative-only failure carries exact native bone/frame/metric evidence and consumes shared operation work");
        }

        var deepest = model.AllBones.OrderByDescending(OptimizationDepth).First();
        using (var before = new CHR0Node { Name = "Independent cumulative hierarchy amplification", FrameCount = 33 })
        {
            var tracks = new List<ControlTrack>();
            for (var bone = deepest; bone != null; bone = bone.Parent as MDL0BoneNode)
            {
                var entry = before.CreateEntry(bone.Name);
                for (int channel = 0; channel < 9; channel++) OptimizationReplace(entry, channel, new[] { OptimizationKey(0, bone.BindState[channel], 0) });
                for (int channel = 0; channel < 3; channel++) OptimizationReplace(entry, channel,
                    new[] { OptimizationKey(0, 1, 0), OptimizationKey(16, 1.0000095f, 0), OptimizationKey(32, 1, 0) });
                var reduced = OptimizationTrack(entry);
                for (int channel = 0; channel < 3; channel++) reduced.Channels[channel] = new[] { OptimizationKey(0, 1, 0), OptimizationKey(32, 1, 0) };
                tracks.Add(reduced);
            }
            double scalarError = 1.0000095f - 1d;
            Check(scalarError < .00001 && scalarError * 1.5 / 16 < .00001 && OptimizationDepth(deepest) >= 11,
                "Native HQ: each independently authored hierarchy scale error/speed passes scalar limits before cumulative verification");
            var cumulativeOptions = OptimizationQualityOptions(names, length, true); cumulativeOptions.Budget = new NativeAnimationOptimizationBudget();
            bool rejected = false;
            try { using (var result = NativeAnimationOptimization.Prepare(model, before, tracks.ToArray(), cumulativeOptions, stage)) { } }
            catch (InvalidDataException error)
            {
                rejected = Equals(error.Data["OptimizationQuality"], true) && ((string)error.Data["Metric"]).StartsWith("world", StringComparison.Ordinal) &&
                    cumulativeOptions.Budget.Samples > 0 && Convert.ToDouble(error.Data["Error"]) > Convert.ToDouble(error.Data["Limit"]);
                File.WriteAllText(Path.Combine(stage, "hq-cumulative-rejection.txt"), error.Message);
            }
            Check(rejected, "Native HQ: scalar-safe errors across a structural native parent chain fail cumulative world/basis or derivative verification");
        }
    }

    static int OptimizationDepth(MDL0BoneNode bone)
    { int result = 0; for (; bone != null; bone = bone.Parent as MDL0BoneNode) result++; return result; }

    static void NativeOptimizationPreparationChecks(MDL0Node model, string stage, string[] names, double length)
    {
        using (var before = OptimizationFixture(names[0], "Independent coordinator ownership and retry fixture"))
        {
            var entry = (CHR0EntryNode)before.Children[0];
            OptimizationReplace(entry, 6, new[] { OptimizationKey(0, 0, 0), OptimizationKey(2, .00005f, 0), OptimizationKey(4, 0, 0) });
            OptimizationFixed(before, "Unmatched coordinator fixture", new float[] { 1, 1, 1, 0, 0, 0, 0, 0, 0 });
            var exactTrack = OptimizationTrack(entry); var qualityTrack = OptimizationTrack(entry);
            qualityTrack.Channels[6] = new[] { OptimizationKey(0, 0, 0), OptimizationKey(4, 0, 0) };
            var tampered = OptimizationTrack(entry); tampered.Channels[6] = new[] { OptimizationKey(0, 1, 0), OptimizationKey(4, 1, 0) };
            string content = NativeAnimationOptimization.ContentKey(before);
            long currentBytes; string bytePath = Path.Combine(stage, "coordinator-current.chr0");
            try { using (var copy = NativeAnimationOptimization.Copy(before)) copy.ExportChecked(bytePath); currentBytes = new FileInfo(bytePath).Length; }
            finally { if (File.Exists(bytePath)) File.Delete(bytePath); }
            var calls = new List<string>(); var budget = new NativeAnimationOptimizationBudget();
            NativeAnimationOptimizationResult exact = null, quality = null;
            using (var choice = NativeAnimationOptimizationPreparation.Prepare(true, currentBytes, (high, scale) =>
            {
                calls.Add(high ? "HQ " + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Exact");
                if (!high) return Task.FromResult(exact = NativeAnimationOptimization.Prepare(model, before, new[] { exactTrack }, OptimizationQualityOptions(names, length, false, budget), stage));
                if (scale == 1) return Task.FromResult(NativeAnimationOptimization.Prepare(model, before, new[] { tampered }, OptimizationQualityOptions(names, length, true, budget), stage));
                return Task.FromResult(quality = NativeAnimationOptimization.Prepare(model, before, new[] { qualityTrack }, OptimizationQualityOptions(names, length, true, budget), stage));
            }, CancellationToken.None).GetAwaiter().GetResult())
            {
                Check(calls.SequenceEqual(new[] { "Exact", "HQ 1", "HQ 0.5" }) && choice.HighQuality && choice.PrecisionScale == .5 && ReferenceEquals(choice.Result, quality),
                    "HQ coordinator: Exact precedes strict precision retries and stops at the first independently verified HQ result");
                Check(exact.Candidate == null && quality.Candidate != null && budget.Samples > exact.Samples + quality.Samples &&
                    quality.BudgetSamples == budget.Samples && quality.BudgetBoneVisits == budget.BoneVisits && quality.BudgetVertexVisits == budget.VertexVisits,
                    "HQ coordinator: replacing Exact disposes its candidate while failed/accepted attempts share charged native counters");
                File.WriteAllText(Path.Combine(stage, "hq-coordinator-retry.txt"), choice.Report);
            }
            Check(quality.Candidate == null, "HQ coordinator: disposing the selected choice releases its independently verified native candidate");

            calls.Clear(); budget = new NativeAnimationOptimizationBudget(); exact = null;
            var grown = OptimizationTrack(entry);
            for (int channel = 0; channel < 9; channel++) grown.Channels[channel] = Enumerable.Range(0, 5).Select(i => OptimizationKey(i, channel < 3 ? 1 : 0, 0)).ToArray();
            using (var choice = NativeAnimationOptimizationPreparation.Prepare(true, currentBytes, (high, scale) =>
            {
                calls.Add(high ? "HQ " + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Exact");
                if (!high) return Task.FromResult(exact = NativeAnimationOptimization.Prepare(model, before, new[] { exactTrack }, OptimizationQualityOptions(names, length, false, budget), stage));
                return Task.FromResult(NativeAnimationOptimization.Prepare(model, before, new[] { scale == 1 ? grown : tampered }, OptimizationQualityOptions(names, length, true, budget), stage));
            }, CancellationToken.None).GetAwaiter().GetResult())
            {
                Check(calls.SequenceEqual(new[] { "Exact", "HQ 1", "HQ 0.5", "HQ 0.25", "HQ 0.125" }) && !choice.HighQuality && ReferenceEquals(choice.Result, exact) && exact.Candidate != null,
                    "HQ coordinator: native size rejection and all remaining quality rejections retain the fully verified Exact fallback");
                Check(budget.Samples == exact.Samples + 3 && choice.Report.Contains("larger") && choice.Report.IndexOf("native", StringComparison.OrdinalIgnoreCase) >= 0,
                    "HQ coordinator: encoded-size failure consumes no pose budget while each native quality failure remains charged");
            }

            calls.Clear(); budget = new NativeAnimationOptimizationBudget(); exact = null; quality = null;
            var largerQuality = OptimizationTrack(entry); largerQuality.Channels[6] = qualityTrack.Channels[6];
            for (int channel = 0; channel < 3; channel++) largerQuality.Channels[channel] = new[] { OptimizationKey(0, 1, 0), OptimizationKey(4, 1, 0) };
            using (var choice = NativeAnimationOptimizationPreparation.Prepare(true, currentBytes, (high, scale) =>
            {
                calls.Add(high ? "HQ" : "Exact");
                if (!high) return Task.FromResult(exact = NativeAnimationOptimization.Prepare(model, before, new[] { exactTrack }, OptimizationQualityOptions(names, length, false, budget), stage));
                return Task.FromResult(quality = NativeAnimationOptimization.Prepare(model, before, new[] { largerQuality }, OptimizationQualityOptions(names, length, true, budget), stage));
            }, CancellationToken.None).GetAwaiter().GetResult())
            {
                Check(quality.AfterBytes > exact.AfterBytes && quality.AfterBytes < currentBytes && ReferenceEquals(choice.Result, exact) && !choice.HighQuality,
                    "HQ coordinator: a fully verified HQ result cannot replace a smaller verified Exact fallback");
                Check(calls.SequenceEqual(new[] { "Exact", "HQ" }) && quality.Candidate == null && exact.Candidate != null,
                    "HQ coordinator: first completed HQ verification stops retries and disposes its unused larger candidate");
            }

            calls.Clear(); budget = new NativeAnimationOptimizationBudget(); exact = null;
            using (var choice = NativeAnimationOptimizationPreparation.Prepare(true, currentBytes, (high, scale) =>
            {
                calls.Add(high ? "HQ" : "Exact");
                if (!high) return Task.FromResult(exact = NativeAnimationOptimization.Prepare(model, before, new[] { exactTrack }, OptimizationQualityOptions(names, length, false, budget), stage));
                budget.Require(NativeAnimationOptimizationBudget.MaximumSamples + 1, names.Length, 0, "independent coordinator exhausted retry");
                throw new Exception("The exhausted budget must reject before preparing another result.");
            }, CancellationToken.None).GetAwaiter().GetResult())
                Check(calls.SequenceEqual(new[] { "Exact", "HQ" }) && ReferenceEquals(choice.Result, exact) && exact.Candidate != null && budget.Samples == exact.Samples,
                    "HQ coordinator: exhausted aggregate budget stops retries and retains only the already verified Exact fallback");

            foreach (bool cancel in new[] { false, true })
            {
                var cancellation = new CancellationTokenSource(); exact = null; bool rejected = false;
                try
                {
                    using (var choice = NativeAnimationOptimizationPreparation.Prepare(true, currentBytes, (high, scale) =>
                    {
                        if (!high) return Task.FromResult(exact = NativeAnimationOptimization.Prepare(model, before, new[] { exactTrack }, OptimizationQualityOptions(names, length, false), stage));
                        if (cancel) { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); }
                        throw new InvalidDataException("Independent untagged native format/dependency failure");
                    }, cancellation.Token).GetAwaiter().GetResult()) { }
                }
                catch (OperationCanceledException) { rejected = cancel; }
                catch (InvalidDataException) { rejected = !cancel; }
                Check(rejected && exact != null && exact.Candidate == null,
                    "HQ coordinator: " + (cancel ? "cancellation" : "untagged failure") + " propagates and disposes the held Exact result without presenting fallback output");
                cancellation.Dispose();
            }
            Check(NativeAnimationOptimization.ContentKey(before) == content, "HQ coordinator: retry, fallback, budget, failure and cancellation leave the immutable native input unchanged");
        }

        using (var before = OptimizationFixture(names[0], "Exact no-op remains eligible for HQ"))
        {
            var entry = (CHR0EntryNode)before.Children[0]; OptimizationReplace(entry, 6,
                new[] { OptimizationKey(0, 0, 0), OptimizationKey(2, .00005f, 0), OptimizationKey(4, 0, 0) });
            var reduced = OptimizationTrack(entry); reduced.Channels[6] = new[] { OptimizationKey(0, 0, 0), OptimizationKey(4, 0, 0) };
            var calls = new List<bool>();
            using (var choice = NativeAnimationOptimizationPreparation.Prepare(true, long.MaxValue, (quality, scale) =>
            {
                calls.Add(quality); return Task.FromResult(NativeAnimationOptimization.Prepare(model, before,
                    new[] { quality ? reduced : OptimizationTrack(entry) }, OptimizationQualityOptions(names, length, quality), stage));
            }, CancellationToken.None).GetAwaiter().GetResult())
                Check(calls.SequenceEqual(new[] { false, true }) && choice.HighQuality && choice.Result?.Candidate != null,
                    "HQ coordinator: an honest Exact no-op still permits independently verified approximate reduction");
        }
    }

    static IEnumerable<ResourceNode> OptimizationWalk(ResourceNode node)
    { yield return node; foreach (var child in node.Children) foreach (var nested in OptimizationWalk(child)) yield return nested; }
    static ControlKey OptimizationKey(int frame, float value, float tangent) => new ControlKey { Frame = frame, Value = value, Tangent = tangent };
    static ControlTrack OptimizationTrack(CHR0EntryNode entry) => new ControlTrack { Bone = entry.Name,
        Channels = entry.KeyArrays.Select(a => a.Entries.Select(k => OptimizationKey(k._index, k._value, k._tangent)).ToArray()).ToArray() };
    static void OptimizationReplace(CHR0EntryNode entry, int channel, ControlKey[] keys)
    {
        var array = entry.KeyArrays[channel]; while (array.KeyCount > 0) array.Remove(array.Entries.First()._index);
        foreach (var key in keys) entry.Keyframes.SetFrameValue(channel, key.Frame, key.Value, true)._tangent = key.Tangent;
    }
    static CHR0EntryNode OptimizationFixed(CHR0Node clip, string name, float[] values)
    {
        var entry = clip.CreateEntry(name); for (int channel = 0; channel < 9; channel++) OptimizationReplace(entry, channel, new[] { OptimizationKey(0, values[channel], 0) });
        entry.Keyframes.PreserveInterpolation = true; return entry;
    }
    static CHR0Node OptimizationFixture(string bone, string name)
    {
        var clip = new CHR0Node { Name = name, Version = 5, FrameCount = 5, Loop = false, OriginalPath = "procedural/native-optimizer" };
        clip.UserEntries.Add(new UserDataClass { Name = "Original metadata", DataType = UserValueType.String, Entries = new[] { "Unchanged independent native metadata" } });
        var entry = OptimizationFixed(clip, bone, new float[] { 1, 1, 1, 0, 0, 0, 0, 0, 0 });
        entry.ScaleCompensateApply = true; entry.ScaleCompensateParent = true; entry.ClassicScaleOff = true;
        return clip;
    }
    static void OptimizationReject(Func<NativeAnimationOptimizationResult> prepare, string diagnostic, string name)
    {
        bool rejected = false;
        try { using (var result = prepare()) { } }
        catch (InvalidDataException error) { rejected = error.Message.IndexOf(diagnostic, StringComparison.OrdinalIgnoreCase) >= 0; if (!rejected) throw; }
        Check(rejected, name);
    }
}
