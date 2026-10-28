using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal static partial class Program
{
    static Task NativeSmallFileQualityChecks(string modelPath, string stage)
    {
        Directory.CreateDirectory(stage);
        using (var owner = NodeFactory.FromFile(null, modelPath))
        {
            var model = OptimizationWalk(owner).OfType<MDL0Node>().OrderByDescending(m => m.NumVertices).First();
            model.ApplyCHR(null, 0); var bones = model.AllBones.ToArray();
            var root = bones.First(b => !(b.Parent is MDL0BoneNode));
            var leaf = bones.First(b => !b.Children.OfType<MDL0BoneNode>().Any() && !ReferenceEquals(b, root) &&
                Enumerable.Range(0, 9).All(c => ReplayFloatBits(b.BindState[c]) != int.MinValue) &&
                Enumerable.Range(0, 3).All(c => b.BindState[c] >= .01 && b.BindState[c] <= 10) &&
                Math.Abs(b.BindState[3]) < 720 && Math.Abs(b.BindState[6]) < 100 &&
                Enumerable.Range(1, 2).All(c => {
                    double norm = Math.Sqrt(Enumerable.Range(0, 3).Sum(r => Math.Pow(b.BindMatrix[c * 4 + r], 2)));
                    return norm >= .9 && norm <= 1.1;
                }));
            double length = NativeAnimationOptimization.GetBindBoundsDiagonal(model);
            NativeAnimationOptimizationOptions Options(bool small, bool exact = false) => new NativeAnimationOptimizationOptions {
                SmallFile = small, ExactOnly = exact, LengthScale = length, TargetBones = bones.Select(b => b.Name).ToArray(),
                ProtectedBones = new[] { root.Name }, ExactBones = new[] { root.Name }, Budget = new NativeAnimationOptimizationBudget() };

            foreach (int channel in new[] { 3, 6, 0 })
            {
                float amplitude = channel == 3 ? .03f : channel == 6 ? (float)Math.Min(.0012, .0004 * length) : .00004f;
                using (var baseline = SmallFileFixture(root, leaf, channel, amplitude))
                {
                    var proposed = SmallFileFlatten(baseline, root.Name, channel);
                    string content = NativeAnimationOptimization.ContentKey(baseline), storage = NativeAnimationOptimization.StorageKey(baseline);
                    bool strictRejected = false;
                    try { using (var ignored = NativeAnimationOptimization.Prepare(model, baseline, proposed, Options(false), stage)) { } }
                    catch (InvalidDataException error) { strictRejected = error.Data.Contains("OptimizationQuality"); }
                    Check(strictRejected, "Small file profile: existing High quality still rejects the independently authored larger pulse on native channel " + channel);
                    using (var result = NativeAnimationOptimization.Prepare(model, baseline, proposed, Options(true), stage))
                    {
                        var measured = SourceKeyQualityMeasure(model, baseline, result.Candidate);
                        Check(result.HasChanges && result.AfterBytes < result.BeforeBytes && result.Report.Contains("Small file ceilings") &&
                            measured.Euler <= .1 && measured.Orientation <= .1 && measured.Translation <= .002 && measured.Scale <= .0001 &&
                            measured.EulerDerivative <= .1 && measured.TranslationDerivative <= .002 && measured.ScaleDerivative <= .0001 &&
                            measured.World <= .001 * measured.Length && measured.Skin <= .001 * measured.Length,
                            "Small file profile: the explicit mode accepts real nonzero reduction under independent frozen value/derivative/native hierarchy/skin bounds on channel " + channel);
                        Check(channel == 3 ? measured.Euler > .01 : channel == 6 ? measured.Translation > Math.Min(.0002, .0001 * measured.Length) : measured.Scale > .00001,
                            "Small file profile: accepted quality is genuinely distinct from the unchanged stricter mode on channel " + channel);
                        SourceKeysSameTrack((CHR0EntryNode)baseline.FindChild(root.Name, false), (CHR0EntryNode)result.Candidate.FindChild(root.Name, false),
                            "Small file profile: source-owned protected motion retains every exact native tuple");
                        File.WriteAllText(Path.Combine(stage, "small-file-channel-" + channel + ".json"), new JavaScriptSerializer().Serialize(new {
                            Passed = true, NativeMeasured = measured, result.BeforeBytes, result.AfterBytes, result.BeforeKeys, result.AfterKeys, result.Report }));
                    }
                    bool exactStillStrict = false;
                    try { using (var ignored = NativeAnimationOptimization.Prepare(model, baseline, proposed, Options(true, true), stage)) { } }
                    catch (InvalidDataException error) { exactStillStrict = error.Data.Contains("OptimizationQuality"); }
                    Check(exactStillStrict, "Small file profile: ExactOnly wins over the additive SmallFile option and preserves existing fallback bounds");
                    Check(NativeAnimationOptimization.ContentKey(baseline) == content && NativeAnimationOptimization.StorageKey(baseline) == storage,
                        "Small file profile: rejected/accepted preparation preserves the immutable native input and its storage");
                }
            }

            using (var baseline = SmallFileFixture(root, leaf, 3, .03f))
            {
                var proposed = SmallFileFlatten(baseline, root.Name, 3);
                var body = proposed.Single(t => t.Bone == leaf.Name);
                foreach (var key in body.Channels[3]) key.Value += .02f;
                var fit = Options(true); fit.FitJoinFrames = new[] { 0, 4 };
                using (var result = NativeAnimationOptimization.Prepare(model, baseline, proposed, fit, stage))
                {
                    var output = (CHR0EntryNode)result.Candidate.FindChild(leaf.Name, false);
                    Check(output.KeyArrays[3].Entries.Select(k => k._index).SequenceEqual(new[] { 0f, 4f }) && result.MaximumEulerError > .01 && result.MaximumEulerError <= .1,
                        "Small file fit joins retain native key times/counts while allowing only explicitly bounded numeric changes");
                }
                var explicitMandatory = Options(true); explicitMandatory.MandatoryFrames = new[] { 0, 4 };
                SmallFileReject(() => NativeAnimationOptimization.Prepare(model, baseline, proposed, explicitMandatory, stage), "mandatory",
                    "Small file does not reinterpret explicit mandatory knots as relaxed fitting joins");
                var strictFit = Options(false); strictFit.FitJoinFrames = new[] { 0, 4 };
                SmallFileReject(() => NativeAnimationOptimization.Prepare(model, baseline, proposed, strictFit, stage), "mandatory",
                    "Existing High quality keeps fitting join values and tangents exact even through the additive FitJoinFrames interface");
                var removedJoin = SelectiveReductionClone(proposed); removedJoin.Single(t => t.Bone == leaf.Name).Channels[3] = new[] { OptimizationKey(0, body.Channels[3][0].Value, 0) };
                SmallFileReject(() => NativeAnimationOptimization.Prepare(model, baseline, removedJoin, fit, stage), "mandatory",
                    "Small file cannot remove an authored fitting join time to hide its error");
                var pinned = Options(true); pinned.ProtectedBones = new[] { root.Name, leaf.Name };
                SmallFileReject(() => NativeAnimationOptimization.Prepare(model, baseline, proposed, pinned, stage), "protected",
                    "Small file cannot change protected pin/contact dependency tracks even when numeric errors fit the wider mode");
                var badRoot = SelectiveReductionClone(proposed); badRoot.Single(t => t.Bone == root.Name).Channels[6][0].Value += .001f;
                SmallFileReject(() => NativeAnimationOptimization.Prepare(model, baseline, badRoot, fit, stage), "protected",
                    "Small file cannot change authored source-owned motion during a fitting join");
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel(); bool stopped = false;
                    try { using (var ignored = NativeAnimationOptimization.Prepare(model, baseline, proposed, fit, stage, cancellation.Token)) { } }
                    catch (OperationCanceledException) { stopped = true; }
                    Check(stopped, "Small file cancellation returns no partially verified candidate");
                }
                var exhausted = Options(true); exhausted.Budget.Samples = NativeAnimationOptimizationBudget.MaximumSamples;
                bool budget = false;
                try { using (var ignored = NativeAnimationOptimization.Prepare(model, baseline, proposed, exhausted, stage)) { } }
                catch (InvalidDataException error) { budget = error.Data.Contains("OptimizationBudget"); }
                Check(budget, "Small file retains the same aggregate native verification resource caps");
            }

            using (var baseline = SmallFileFixture(root, leaf, 3, .03f))
            {
                ((CHR0EntryNode)baseline.FindChild(leaf.Name, false)).KeyArrays[3].GetKeyframe(2)._value = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
                var proposed = SmallFileFlatten(baseline, root.Name, 3);
                SmallFileReject(() => NativeAnimationOptimization.Prepare(model, baseline, proposed, Options(true), stage), "mandatory",
                    "Small file preserves authored signed-zero knots independently of ordinary numeric tolerances");
            }
            using (var baseline = SmallFileFixture(root, leaf, 3, .03f))
            {
                ((CHR0EntryNode)baseline.FindChild(leaf.Name, false)).Keyframes.SetFrameValue(3, 2, .015f, true)._tangent = 0;
                var proposed = SmallFileFlatten(baseline, root.Name, 3);
                SmallFileReject(() => NativeAnimationOptimization.Prepare(model, baseline, proposed, Options(true), stage), "discontinuous",
                    "Small file preserves both paired-key sides rather than smoothing a native discontinuity");
            }
            SmallFileStorageChecks(stage, length);
            NativeSmallFileZeroGridChecks(stage, length);
        }
        return Task.CompletedTask;
    }

    static CHR0Node SmallFileFixture(MDL0BoneNode root, MDL0BoneNode leaf, int channel, float amplitude)
    {
        var clip = new CHR0Node { Name = "Independent Small file profile", Version = 5, FrameCount = 5, OriginalPath = "independent-small-file" };
        foreach (var bone in new[] { root, leaf })
        {
            var entry = clip.CreateEntry(bone.Name); entry.UseModelScale = entry.UseModelRotate = entry.UseModelTranslate = false;
            entry.ScaleCompensateApply = bone.SegScaleCompApply; entry.ScaleCompensateParent = bone.SegScaleCompParent; entry.ClassicScaleOff = !bone.ClassicScale;
            for (int c = 0; c < 9; c++) for (int at = 0; at < 5; at++) entry.Keyframes.SetFrameValue(c, at, bone.BindState[c], true)._tangent = 0;
            entry.Keyframes.PreserveInterpolation = true;
        }
        var rootEntry = (CHR0EntryNode)clip.FindChild(root.Name, false);
        for (int at = 0; at < 5; at++) { rootEntry.KeyArrays[6].GetKeyframe(at)._value += at * .00002f; rootEntry.KeyArrays[6].GetKeyframe(at)._tangent = .00002f; }
        var body = (CHR0EntryNode)clip.FindChild(leaf.Name, false);
        foreach (int at in new[] { 1, 3 }) body.KeyArrays[channel].GetKeyframe(at)._value += amplitude;
        PreciseCHR0EntryNode.Promote(clip); return clip;
    }

    static ControlTrack[] SmallFileFlatten(CHR0Node clip, string protectedRoot, int varyingChannel)
    {
        var tracks = clip.Children.OfType<CHR0EntryNode>().Select(OptimizationTrack).ToArray();
        foreach (var track in tracks.Where(t => t.Bone != protectedRoot))
            for (int channel = 0; channel < 9; channel++) track.Channels[channel] = channel == varyingChannel ?
                new[] { OptimizationKey(0, track.Channels[channel][0].Value, 0), OptimizationKey(4, track.Channels[channel][0].Value, 0) } :
                new[] { OptimizationKey(0, track.Channels[channel][0].Value, 0) };
        return tracks;
    }

    static void SmallFileReject(Func<NativeAnimationOptimizationResult> operation, string fragment, string message)
    {
        bool rejected = false;
        try { using (var ignored = operation()) { } }
        catch (InvalidDataException error) { rejected = error.Message.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0; }
        Check(rejected, message);
    }

    static void SmallFileStorageChecks(string stage, double length)
    {
        using (var source = CompactFixture(48, 3, Enumerable.Range(0, 16).Select(i => i * 3).ToArray(),
            at => .0012345f + at * 50.1371397f + (at % 2 == 0 ? 0 : .07f), at => 1))
        {
            var entry = (CHR0EntryNode)source.Children[0]; string content = NativeAnimationOptimization.ContentKey(source);
            var old = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], length);
            var same = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], length, false, new int[0]);
            Check(string.Join(";", old.Select(p => p.Format + ":" + p.Curves[0].ContentKey)) == string.Join(";", same.Select(p => p.Format + ":" + p.Curves[0].ContentKey)),
                "Small file storage overload preserves the exact behavior of every existing High quality caller");
            var small = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], length, true, new int[0]);
            var i4 = small.FirstOrDefault(p => p.Format == AnimDataFormat.I4);
            Check(i4 != null && !i4.IsExact && !old.Any(p => p.Format == AnimDataFormat.I4),
                "Small file storage can propose a genuinely smaller indexed encoding that the unchanged stricter mode rejects");
            var original = entry.KeyArrays[3].Entries.ToArray(); var values = i4.Curves[0].Values;
            int changed = Enumerable.Range(0, original.Length).First(i => ReplayFloatBits(original[i]._value) != ReplayFloatBits(values[i]));
            // This independently authored fixture uses integral 0,3,...,45
            // schedule; retain the mandatory-frame API's integral test input.
            int frame = changed * 3;
            Check(original[changed]._index == frame, "Small file fixture retains its independently authored integral timing schedule");
            var fit = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new int[0], length, true, new[] { frame });
            Check(fit.Any(p => p.Format == AnimDataFormat.I4) && fit.All(p => p.Curves[0].Times.SequenceEqual(original.Select(k => k._index))),
                "Small file indexed proposals retain all authored fit times while permitting bounded coefficient changes");
            var mandatory = NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, false, new[] { frame }, length, true, new int[0]);
            Check(mandatory.All(p => ReplayFloatBits(p.Curves[0].Values[changed]) == ReplayFloatBits(original[changed]._value) &&
                ReplayFloatBits(p.Curves[0].Tangents[changed]) == ReplayFloatBits(original[changed]._tangent)),
                "Small file indexed proposals keep explicit mandatory values and tangents exact");
            Check(NativeCompactAnimationStorage.CreateAlternatives(entry, 1, false, true, new int[0], length, true, new int[0]).All(p => p.IsExact),
                "Small file indexed proposals cannot approximate exact source-owned or protected groups");
            Check(NativeCompactAnimationStorage.CreateAlternatives(entry, 1, true, false, new int[0], length, true, new int[0]).All(p => p.IsExact),
                "Small file storage cannot override ExactOnly even through its additive profile overload");
            Check(NativeAnimationOptimization.ContentKey(source) == content,
                "Small file storage alternatives never mutate original coefficients, native flags or timing");
        }
    }
}
