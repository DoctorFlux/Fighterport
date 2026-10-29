using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.Internal;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Freeze this oracle before evaluating the production search. A zero-tangent
    // three-knot pulse has peak a and maximum speed 1.5*a. The pelvis pulse fits
    // the .01/.005 local value ceilings but its .00735-degree/frame speed is
    // above .0001 radians/frame. Each independent leaf's .0045-degree/frame
    // pulse remains below that native basis derivative bound. A global .25
    // scalar precision retains all three peaks; selective repair can retain the
    // pelvis/ancestor wire while leaving an unrelated leaf at source endpoints.
    static void NativeSelectiveRetargetChecks(MDL0Node model, string output, IEnumerable<string> originalPaths)
    {
        const float bodyAmplitude = .0049f, leafAmplitude = .003f;
        string stage = Path.Combine(output, "selective-retarget"); Directory.CreateDirectory(stage);
        var json = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };
        var originalHashes = originalPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(path => path, FighterProject.HashFile);
        model.ApplyCHR(null, 0);
        var rig = BrawlRetargeter.Capture(model); var plan = new HumanoidRetargeter(rig.Rig, rig.Rig);
        var body = rig.Bones[rig.Rig.Roles["pelvis"]];
        var controls = new HashSet<string>(plan.TargetMotionControls.Select(index => rig.Bones[index].Name), StringComparer.Ordinal);
        var root = rig.Bones[plan.TargetMotionControls.First()];
        bool DescendsFromBody(MDL0BoneNode bone)
        {
            for (var current = bone.Parent as MDL0BoneNode; current != null; current = current.Parent as MDL0BoneNode)
                if (ReferenceEquals(current, body)) return true;
            return false;
        }
        MDL0BoneNode BodyBranch(MDL0BoneNode bone)
        {
            var branch = bone;
            while (branch.Parent is MDL0BoneNode parent && !ReferenceEquals(parent, body)) branch = parent;
            return branch;
        }
        var leaves = rig.Bones.Where(bone => !controls.Contains(bone.Name) && DescendsFromBody(bone) &&
            !bone.Children.OfType<MDL0BoneNode>().Any() &&
            Enumerable.Range(0, 9).All(channel => ReplayFloatBits(bone.BindState[channel]) != int.MinValue) &&
            Enumerable.Range(1, 2).All(column =>
            {
                double norm = Math.Sqrt(Enumerable.Range(0, 3).Sum(row => Math.Pow(bone.BindMatrix[column * 4 + row], 2)));
                return norm >= .9 && norm <= 1.1;
            })).GroupBy(BodyBranch).Select(branch => branch.First()).Take(2).ToArray();
        Check(leaves.Length == 2 && !controls.Contains(body.Name),
            "Selective retarget oracle uses the reviewed pelvis role and two independent native leaf branches, without character or bone-name cases");
        string skeleton = BrawlRetargeter.SkeletonFingerprint(model), modelState = DirtyModelMemory(model), skinKey = SelectiveRetargetSkinKey(model);
        try
        {
            using (var dense = SelectiveRetargetFixture(root, body, leaves, bodyAmplitude, leafAmplitude))
            using (var source = NativeAnimationOptimization.Copy(dense))
            using (var coarse = NativeAnimationOptimization.Copy(dense))
            using (var restoredBody = NativeAnimationOptimization.Copy(dense))
            {
                foreach (var bone in new[] { body }.Concat(leaves))
                {
                    ((CHR0EntryNode)source.FindChild(bone.Name, false)).KeyArrays[3].Remove(1);
                    ((CHR0EntryNode)coarse.FindChild(bone.Name, false)).KeyArrays[3].Remove(1);
                }
                foreach (var leaf in leaves) ((CHR0EntryNode)restoredBody.FindChild(leaf.Name, false)).KeyArrays[3].Remove(1);
                string denseKey = NativeAnimationOptimization.SourceContentKey(dense), denseStorage = NativeAnimationOptimization.StorageKey(dense);
                string sourceKey = NativeAnimationOptimization.SourceContentKey(source), sourceStorage = NativeAnimationOptimization.StorageKey(source);
                string denseFile = Path.Combine(stage, "independent-full-dense.chr0"); SourceKeyArtifactExport(dense, denseFile);
                long denseBytes = new FileInfo(denseFile).Length;
                var coarseLocal = SourceKeyQualityMeasure(model, dense, coarse);
                var coarseNative = SelectiveRetargetNativeMeasure(model, dense, coarse);
                var repairedNative = SelectiveRetargetNativeMeasure(model, dense, restoredBody);
                double actualBody = ((CHR0EntryNode)dense.FindChild(body.Name, false)).GetFrameValue(3, 1) - (double)body.BindState[3];
                var actualLeaves = leaves.Select(leaf => ((CHR0EntryNode)dense.FindChild(leaf.Name, false)).GetFrameValue(3, 1) - (double)leaf.BindState[3]).ToArray();
                string failureMetric = null, failureBone = null, failureMessage = null;
                double? failureError = null, failureLimit = null;
                try
                {
                    using (var ignored = NativeAnimationOptimization.Prepare(model, dense,
                        coarse.Children.OfType<CHR0EntryNode>().Select(OptimizationTrack).ToArray(),
                        new NativeAnimationOptimizationOptions { ExactOnly = false, LengthScale = coarseNative.Length,
                            TargetBones = rig.Bones.Select(bone => bone.Name).ToArray(), ExactBones = controls.ToArray(),
                            Budget = new NativeAnimationOptimizationBudget() }, stage)) { }
                }
                catch (InvalidDataException error) when (error.Data.Contains("OptimizationQuality"))
                {
                    failureMetric = Convert.ToString(error.Data["Metric"]); failureBone = Convert.ToString(error.Data["Bone"]);
                    failureError = Convert.ToDouble(error.Data["Error"]); failureLimit = Convert.ToDouble(error.Data["Limit"]);
                    failureMessage = error.Message;
                }
                double rootOracle = 0, pulseOracle = 0;
                foreach (float time in SelectiveRetargetTimes())
                {
                    double u = time <= 1 ? time : 2 - time, shape = u * u * (3 - 2 * u);
                    rootOracle = Math.Max(rootOracle, Math.Abs(((CHR0EntryNode)dense.FindChild(root.Name, false)).GetFrameValue(6, time) - time / 8d));
                    foreach (var bone in new[] { body }.Concat(leaves))
                    {
                        double amplitude = ReferenceEquals(bone, body) ? actualBody : actualLeaves[Array.IndexOf(leaves, bone)];
                        pulseOracle = Math.Max(pulseOracle, Math.Abs(((CHR0EntryNode)dense.FindChild(bone.Name, false)).GetFrameValue(3, time) - bone.BindState[3] - amplitude * shape));
                    }
                }
                // Persist the actual reason and geometry before any expectation
                // assertion, so fixture/platform contradictions remain reviewable.
                File.WriteAllText(Path.Combine(stage, "frozen-oracle-diagnostics.json"), json.Serialize(new {
                    FrozenBodyAmplitude = bodyAmplitude, FrozenLeafAmplitude = leafAmplitude,
                    AnalyticBodySpeed = 1.5 * bodyAmplitude, AnalyticLeafSpeed = 1.5 * leafAmplitude,
                    NativeRadiansPerFrameCeiling = .0001, NativeWorldCeiling = .0001 * coarseNative.Length,
                    ActualBodyAmplitude = actualBody, ActualLeafAmplitudes = actualLeaves, Body = body.Name,
                    Leaves = leaves.Select(leaf => leaf.Name).ToArray(), Root = root.Name,
                    CoarseLocal = coarseLocal, CoarseNative = coarseNative, RestoredBodyNative = repairedNative,
                    FailureMetric = failureMetric, FailureBone = failureBone, FailureError = failureError,
                    FailureLimit = failureLimit, FailureMessage = failureMessage, RootLineOracle = rootOracle, PulseOracle = pulseOracle,
                    DenseBytes = denseBytes, DenseSha256 = FighterProject.HashFile(denseFile), DenseContent = denseKey,
                    Claim = "Independent authored Hermite functions and withheld native matrix/all-skin witnesses; no OS input or screen capture."
                }));
                Check(actualBody > .004 && actualBody < .005 && actualLeaves.All(amplitude => amplitude > .0025 && amplitude < .005) &&
                    rootOracle <= .000001 && pulseOracle <= .00003 && coarseLocal.Euler < .005 && coarseLocal.EulerDerivative < .05,
                    "Selective retarget oracle freezes three sub-.005-degree Hermite pulses, exact root line, and independently known scalar value/derivative bounds");
                Check(coarseNative.Length > 0 && coarseNative.WorldDerivative > .0001 * coarseNative.Length &&
                    failureMetric == "world forward derivative" && rig.Bones.Any(bone => bone.Name == failureBone &&
                        (ReferenceEquals(bone, body) || DescendsFromBody(bone))) && failureError > failureLimit,
                    "Selective retarget oracle independently observes a native forward-derivative contradiction on the pelvis hierarchy before production selective repair");
                Check(repairedNative.World <= .0001 * repairedNative.Length && repairedNative.Skin <= .0001 * repairedNative.Length &&
                    repairedNative.WorldDerivative <= .0001 * repairedNative.Length && repairedNative.SkinDerivative <= .0001 * repairedNative.Length,
                    "Selective retarget oracle independently confirms that restoring the pelvis alone leaves both leaf reductions within full native geometry and derivative bounds");

                foreach (var mode in new[] { RetargetKeyframeMode.SourceBoneTimes, RetargetKeyframeMode.SourceChannelTimes })
                using (var result = new RetargetResult { Animation = NativeAnimationOptimization.Copy(dense), Plan = plan })
                {
                    RetargetKeyframes.Prepare(result, rig, rig, source, mode, CancellationToken.None, null);
                    string path = Path.Combine(stage, "selective-" + mode + ".chr0"); SourceKeyArtifactExport(result.Animation, path);
                    using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
                    {
                        if (decoded == null) throw new InvalidDataException("Selective retarget output did not independently reopen.");
                        var measured = SourceKeyQualityMeasure(model, dense, decoded);
                        var native = SelectiveRetargetNativeMeasure(model, dense, decoded);
                        var finalBody = (CHR0EntryNode)decoded.FindChild(body.Name, false);
                        var finalLeaves = leaves.Select(leaf => (CHR0EntryNode)decoded.FindChild(leaf.Name, false)).ToArray();
                        bool leafReduced = finalLeaves.Any(entry => entry != null && !entry.KeyArrays[3].Entries.Any(key => key._index == 1));
                        long bytes = new FileInfo(path).Length;
                        long? heldBytes = SelectiveRetargetHeldBytes(result.KeyframeReport.QualityNotes);
                        File.WriteAllText(Path.Combine(stage, "selective-" + mode + ".json"), json.Serialize(new {
                            Mode = mode.ToString(), FrozenOracle = "zero-tangent 0/a/0 at 0/1/2; pelvis a=.0049, independent leaves a=.003; source RX seeds 0/2; root TX=t/8",
                            Body = body.Name, Leaves = leaves.Select(leaf => leaf.Name).ToArray(), Root = root.Name,
                            DenseBytes = denseBytes, ResultBytes = bytes, HeldBaselineBytesFromNotes = heldBytes,
                            DenseSha256 = FighterProject.HashFile(denseFile), ResultSha256 = FighterProject.HashFile(path),
                            NativeMeasured = measured, IndependentNative = native, AtLeastOneLeafMiddleKeyRemoved = leafReduced,
                            BodyTimes = finalBody?.KeyArrays[3].Entries.Select(key => key._index).ToArray(),
                            LeafTimes = finalLeaves.Select(entry => entry?.KeyArrays[3].Entries.Select(key => key._index).ToArray()).ToArray(),
                            Report = result.KeyframeReport
                        }));
                        Check(finalBody != null && finalBody.KeyArrays[3].Entries.Any(key => key._index == 1) &&
                            Math.Abs(finalBody.GetFrameValue(3, 1) - body.BindState[3] - actualBody) <= .00003 && leafReduced,
                            mode + ": selective native repair keeps the required pelvis peak while an unrelated eligible leaf omits frame one");
                        foreach (var bone in new[] { body }.Concat(leaves))
                        {
                            var entry = (CHR0EntryNode)decoded.FindChild(bone.Name, false);
                            Check(entry != null && new[] { 0f, 2f }.All(time => entry.KeyArrays[3].Entries.Any(key => key._index == time)),
                                mode + ": independently authored source RX endpoint seeds survive / " + bone.Name);
                        }
                        Check(measured.Euler > 0 && measured.Euler <= .01 && measured.Orientation <= .01 && measured.Scale <= .00001 &&
                            measured.Translation <= Math.Min(.0002, .0001 * measured.Length) && measured.EulerDerivative <= .05 &&
                            measured.ScaleDerivative <= .00001 && measured.TranslationDerivative <= Math.Min(.0002, .0001 * measured.Length) &&
                            native.World <= .0001 * native.Length && native.Skin <= .0001 * native.Length &&
                            native.WorldDerivative <= .0001 * native.Length && native.SkinDerivative <= .0001 * native.Length,
                            mode + ": independent withheld native fractions retain strict local, forward-matrix and every-skin-vertex value/derivative bounds");
                        Check(result.KeyframeReport.ScalarCertified && result.KeyframeReport.QualityVerified && !result.KeyframeReport.DenseFallback &&
                            result.KeyframeReport.DenseContent == NativeAnimationOptimization.ContentKey(dense) &&
                            bytes == result.KeyframeReport.ResultBytes && bytes <= denseBytes && result.KeyframeReport.DenseBytes == denseBytes &&
                            heldBytes.HasValue && bytes < heldBytes.Value,
                            mode + ": a fresh independently encoded result stays within the same full Dense bytes and strictly improves any recorded held baseline");
                        foreach (var entry in dense.Children.OfType<CHR0EntryNode>())
                        {
                            var actual = decoded.FindChild(entry.Name, false) as CHR0EntryNode;
                            Check(actual != null && actual.UseModelScale == entry.UseModelScale && actual.UseModelRotate == entry.UseModelRotate &&
                                actual.UseModelTranslate == entry.UseModelTranslate && actual.ScaleCompensateApply == entry.ScaleCompensateApply &&
                                actual.ScaleCompensateParent == entry.ScaleCompensateParent && actual.ClassicScaleOff == entry.ClassicScaleOff,
                                mode + ": original native model/scale-semantics flags survive / " + entry.Name);
                        }
                        SourceKeysSameTrack((CHR0EntryNode)dense.FindChild(root.Name, false), (CHR0EntryNode)decoded.FindChild(root.Name, false),
                            mode + ": moving protected root retains every ordered key/value/tangent bit including canonical fixed Y/Z");
                        result.KeyframeReport.Validate();
                    }
                }

                foreach (var mode in new[] { RetargetKeyframeMode.SourceBoneTimes, RetargetKeyframeMode.SourceChannelTimes })
                using (var cancellation = new CancellationTokenSource())
                using (var result = new RetargetResult { Animation = NativeAnimationOptimization.Copy(dense), Plan = plan })
                {
                    string before = NativeAnimationOptimization.SourceContentKey(result.Animation), message = null;
                    bool stopped = false;
                    try
                    {
                        RetargetKeyframes.Prepare(result, rig, rig, source, mode, cancellation.Token,
                            (progress, text) => { if (progress >= 96) { message = text; cancellation.Cancel(); } });
                    }
                    catch (OperationCanceledException) { stopped = true; }
                    bool unchanged = NativeAnimationOptimization.SourceContentKey(result.Animation) == before;
                    File.WriteAllText(Path.Combine(stage, "late-cancel-" + mode + ".json"), json.Serialize(new {
                        Stopped = stopped, ProgressMessage = message, AnimationUnchanged = unchanged,
                        ReportPublished = result.KeyframeReport != null, DensePublished = result.DenseAnimation != null
                    }));
                    Check(stopped && message != null && unchanged && result.DenseAnimation == null && result.KeyframeReport == null,
                        mode + ": cancellation at the selective inner progress publishes no partial report, reference or animation");
                }
                Check(NativeAnimationOptimization.SourceContentKey(dense) == denseKey && NativeAnimationOptimization.StorageKey(dense) == denseStorage &&
                    NativeAnimationOptimization.SourceContentKey(source) == sourceKey && NativeAnimationOptimization.StorageKey(source) == sourceStorage,
                    "Selective retarget success and late cancellation retain exact immutable Dense/source coefficients, native flags and storage");
            }
        }
        finally { model.ApplyCHR(null, 0); }
        Check(BrawlRetargeter.SkeletonFingerprint(model) == skeleton && DirtyModelMemory(model) == modelState && SelectiveRetargetSkinKey(model) == skinKey &&
            originalHashes.All(pair => FighterProject.HashFile(pair.Key) == pair.Value),
            "Selective retarget preparation retains original source/model files, hierarchy, authentic binds and every native skin coefficient");
    }

    static CHR0Node SelectiveRetargetFixture(MDL0BoneNode root, MDL0BoneNode body, MDL0BoneNode[] leaves, float bodyAmplitude, float leafAmplitude)
    {
        var clip = new CHR0Node { Name = "Independent selective target oracle", Version = 5, FrameCount = 3,
            Loop = false, OriginalPath = "procedural/selective-target" };
        foreach (var bone in new[] { root, body }.Concat(leaves))
        {
            var entry = clip.CreateEntry(bone.Name); entry.Keyframes.PreserveInterpolation = true;
            entry.UseModelScale = entry.UseModelRotate = entry.UseModelTranslate = false;
            entry.ScaleCompensateApply = bone.SegScaleCompApply; entry.ScaleCompensateParent = bone.SegScaleCompParent;
            entry.ClassicScaleOff = !bone.ClassicScale;
            for (int channel = 0; channel < 9; channel++) entry.Keyframes.SetFrameValue(channel, 0, bone.BindState[channel], true)._tangent = 0;
            if (ReferenceEquals(bone, root))
            {
                entry.KeyArrays[6].Remove(0);
                for (int frame = 0; frame < 3; frame++) entry.Keyframes.SetFrameValue(6, frame, frame / 8f, true)._tangent = .125f;
                foreach (int channel in new[] { 7, 8 }) entry.KeyArrays[channel].GetKeyframe(0)._value = 0;
            }
            else
            {
                for (int frame = 1; frame < 3; frame++)
                    entry.Keyframes.SetFrameValue(3, frame, bone.BindState[3] + (frame == 1 ? ReferenceEquals(bone, body) ? bodyAmplitude : leafAmplitude : 0), true)._tangent = 0;
            }
        }
        PreciseCHR0EntryNode.Promote(clip); return clip;
    }

    static float[] SelectiveRetargetTimes()
    {
        var times = new SortedSet<float>(Enumerable.Range(0, 65).Select(index => index / 32f));
        for (int frame = 0; frame < 2; frame++) foreach (float fraction in new[] { .1732051f, .4142136f, .7320508f }) times.Add(frame + fraction);
        return times.ToArray();
    }

    sealed class SelectiveRetargetNativeMeasurements
    {
        public double Length, World, Skin, WorldDerivative, SkinDerivative, WorstWorldDerivativeFrame;
        public string WorstWorldDerivativeBone;
        public int Samples, Bones, SkinVertices;
    }

    // Independent fixed withheld fractions, complete native forward matrices,
    // and every original weighted vertex. This does not call production quality
    // validation or use its adaptive witnesses/accepted maxima as the oracle.
    static SelectiveRetargetNativeMeasurements SelectiveRetargetNativeMeasure(MDL0Node model, CHR0Node before, CHR0Node after)
    {
        var bones = model.AllBones.ToArray();
        var vertices = model.Objects.OfType<MDL0ObjectNode>().SelectMany(mesh => mesh.Vertices ?? new List<Vertex3>()).ToArray();
        var result = new SelectiveRetargetNativeMeasurements { Bones = bones.Length, SkinVertices = vertices.Length };
        model.ApplyCHR(null, 0); var bind = vertices.Select(vertex => vertex.WeightedPosition).ToArray();
        result.Length = Math.Sqrt(Math.Pow((double)bind.Max(point => point._x) - bind.Min(point => point._x), 2) +
            Math.Pow((double)bind.Max(point => point._y) - bind.Min(point => point._y), 2) + Math.Pow((double)bind.Max(point => point._z) - bind.Min(point => point._z), 2));
        double[] previousWorld = null, previousSkin = null; float previousTime = 0;
        foreach (float time in SelectiveRetargetTimes())
        {
            model.ApplyCHR(before, time + 1);
            var world = bones.Select(bone => bone._frameMatrix).ToArray(); var skin = vertices.Select(vertex => vertex.WeightedPosition).ToArray();
            model.ApplyCHR(after, time + 1);
            var worldError = new double[bones.Length * 12]; var skinError = new double[vertices.Length * 3];
            for (int bone = 0; bone < bones.Length; bone++) for (int column = 0; column < 4; column++)
            {
                double squared = 0;
                for (int row = 0; row < 3; row++)
                {
                    double difference = bones[bone]._frameMatrix[column * 4 + row] - (double)world[bone][column * 4 + row];
                    if (column < 3) difference *= result.Length;
                    worldError[bone * 12 + column * 3 + row] = difference; squared += difference * difference;
                }
                result.World = Math.Max(result.World, Math.Sqrt(squared));
            }
            for (int vertex = 0; vertex < vertices.Length; vertex++)
            {
                var point = vertices[vertex].WeightedPosition;
                skinError[vertex * 3] = point._x - (double)skin[vertex]._x;
                skinError[vertex * 3 + 1] = point._y - (double)skin[vertex]._y;
                skinError[vertex * 3 + 2] = point._z - (double)skin[vertex]._z;
                result.Skin = Math.Max(result.Skin, SelectiveRetargetNorm(skinError, vertex * 3));
            }
            if (previousWorld != null && time - previousTime >= 1f / 64)
            {
                double span = time - previousTime;
                for (int bone = 0; bone < bones.Length; bone++) for (int column = 0; column < 4; column++)
                {
                    int offset = bone * 12 + column * 3;
                    double error = SelectiveRetargetDifference(worldError, previousWorld, offset) / span;
                    if (error > result.WorldDerivative) { result.WorldDerivative = error; result.WorstWorldDerivativeBone = bones[bone].Name; result.WorstWorldDerivativeFrame = time; }
                }
                for (int vertex = 0; vertex < vertices.Length; vertex++)
                {
                    int offset = vertex * 3;
                    result.SkinDerivative = Math.Max(result.SkinDerivative, SelectiveRetargetDifference(skinError, previousSkin, offset) / span);
                }
                previousWorld = worldError; previousSkin = skinError; previousTime = time;
            }
            else if (previousWorld == null) { previousWorld = worldError; previousSkin = skinError; previousTime = time; }
            result.Samples++;
        }
        model.ApplyCHR(null, 0); return result;
    }

    static double SelectiveRetargetNorm(double[] values, int offset) => Math.Sqrt(values[offset] * values[offset] +
        values[offset + 1] * values[offset + 1] + values[offset + 2] * values[offset + 2]);

    static double SelectiveRetargetDifference(double[] a, double[] b, int offset) => Math.Sqrt(
        Math.Pow(a[offset] - b[offset], 2) + Math.Pow(a[offset + 1] - b[offset + 1], 2) + Math.Pow(a[offset + 2] - b[offset + 2], 2));

    static string SelectiveRetargetSkinKey(MDL0Node model)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                foreach (var mesh in model.Objects.OfType<MDL0ObjectNode>())
                {
                    var vertices = mesh.Vertices ?? new List<Vertex3>(); writer.Write(vertices.Count);
                    foreach (var vertex in vertices)
                    {
                        writer.Write(ReplayFloatBits(vertex._position._x)); writer.Write(ReplayFloatBits(vertex._position._y)); writer.Write(ReplayFloatBits(vertex._position._z));
                        var node = vertex.GetMatrixNode(); writer.Write(node != null); writer.Write(node?.NodeIndex ?? -1);
                        var weights = node?.Weights; writer.Write(weights?.Count ?? 0);
                        if (weights != null) foreach (var weight in weights) { writer.Write(weight.Bone?.Name ?? ""); writer.Write(ReplayFloatBits(weight.Weight)); }
                    }
                }
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream.ToArray()));
        }
    }

    static long? SelectiveRetargetHeldBytes(string notes)
    {
        var match = Regex.Match(notes ?? "", @"held baseline (\d+) complete bytes", RegexOptions.IgnoreCase);
        return match.Success && long.TryParse(match.Groups[1].Value, out long value) ? value : (long?)null;
    }
}
