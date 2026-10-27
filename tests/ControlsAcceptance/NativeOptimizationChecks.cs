using BrawlCrate.Studio;
using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

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
            Check(!Directory.GetFiles(stage, "optimization-*.chr0").Any(), "Native optimizer: success/reject/cancel verification files are cleaned without retained input copies");
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
