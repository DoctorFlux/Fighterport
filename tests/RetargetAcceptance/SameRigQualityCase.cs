using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // The fixture names select private test data, never a retargeting policy.
    // This oracle compares the finished native output to the original motion
    // only because the two complete native rigs are independently identical.
    static int SameRigQualityCase(string modelPath, string motionPath, string manifestPath, string output, bool bake,
        RetargetKeyframeMode mode = RetargetKeyframeMode.Dense)
    {
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new InvalidDataException("Use a new or empty same-rig evidence directory.");
        Directory.CreateDirectory(output);
        if (new FileInfo(manifestPath).Length > 16384) throw new InvalidDataException("Unbounded fixture manifest.");
        var serializer = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
        var fixture = serializer.Deserialize<TransferFixture>(File.ReadAllText(manifestPath));
        string modelHash = FighterProject.HashFile(modelPath), motionHash = FighterProject.HashFile(motionPath), manifestHash = FighterProject.HashFile(manifestPath);
        if (fixture.Schema != "fighterport-authentic-export-fixture-1" || !Same(modelHash, fixture.SourceModelSha256) ||
            !Same(motionHash, fixture.SourceClipSha256)) throw new InvalidDataException("Exact same-rig fixture hashes differ.");
        using (var sourceFile = NodeFactory.FromFile(null, modelPath))
        using (var targetFile = NodeFactory.FromFile(null, modelPath))
        using (var motionFile = NodeFactory.FromFile(null, motionPath))
        {
            MDL0Node ExactModel(ResourceNode root) => Walk(root).OfType<MDL0Node>()
                .Single(model => Same(BrawlRetargeter.SkeletonFingerprint(model), fixture.SourceRigFingerprint));
            string Member(ResourceNode node) => node.Parent == null ? "root" : Member(node.Parent) + "/" + node.Index;
            var sourceModel = ExactModel(sourceFile); var targetModel = ExactModel(targetFile);
            var source = BrawlRetargeter.Capture(sourceModel); var target = BrawlRetargeter.Capture(targetModel);
            if (!Same(source.Fingerprint, target.Fingerprint) || source.Bones.Length != target.Bones.Length ||
                source.Bones.Where((bone, i) => bone.Name != target.Bones[i].Name).Any())
                throw new InvalidDataException("This fixture does not contain the same native rig twice.");
            var clip = Walk(motionFile).OfType<CHR0Node>().Single(c => c.Name == fixture.SourceClipName && Member(c) == fixture.SourceNativeMember);
            if (clip.FrameCount != fixture.SourceFrames || clip.Version != fixture.SourceVersion || clip.Loop != fixture.SourceLoop)
                throw new InvalidDataException("Exact same-rig clip timing differs.");
            string beforeSource = NativeOwnership(sourceModel), beforeTarget = NativeOwnership(targetModel);
            string beforeClip = NativeAnimationOptimization.SourceContentKey(clip);
            var plan = new HumanoidRetargeter(source.Rig, target.Rig);
            var mapped = new HashSet<string>(plan.Mapping.Select(pair => pair.Source), StringComparer.Ordinal);
            var ignoredAnimated = clip.Children.OfType<CHR0EntryNode>()
                .Where(entry => !mapped.Contains(entry.Name) && entry.KeyArrays.Any(array => array.KeyCount > 0))
                .Select(entry => new { entry.Name, Keys = entry.KeyArrays.Sum(array => array.KeyCount) }).ToArray();
            var retainedUnmapped = ignoredAnimated;
            int retainedMappings = plan.Mapping.Length;
            int last = clip.Loop ? clip.FrameCount : clip.FrameCount - 1;
            var times = new SortedSet<float>(Enumerable.Range(0, last * 8 + 1).Select(i => i / 8f));
            foreach (int frame in Enumerable.Range(0, last)) { times.Add(frame + .37f); times.Add(frame + .73f); }
            foreach (var key in clip.Children.OfType<CHR0EntryNode>().SelectMany(entry => entry.KeyArrays).SelectMany(array => array.Entries))
                if (key._index >= 0 && key._index <= last) times.Add(key._index);
            if (times.Count > 20000) throw new InvalidDataException("Same-rig quality sample budget exceeded.");
            double planLocal = 0, planWorld = 0; string planBone = null; float planTime = 0;
            foreach (float time in times)
            {
                var original = BrawlRetargeter.Sample(source, clip, time);
                var solved = plan.Evaluate(original).Pose;
                for (int bone = 0; bone < source.Bones.Length; bone++)
                {
                    double local = Math.Max(Vector3.Distance(original.Local[bone].Translation, solved.Local[bone].Translation),
                        Math.Max(Vector3.Distance(original.Local[bone].Scale, solved.Local[bone].Scale), Angle(original.Local[bone].Rotation, solved.Local[bone].Rotation)));
                    double world = Vector3.Distance(original.Point(bone), solved.Point(bone));
                    if (local > planLocal) { planLocal = local; planBone = source.Bones[bone].Name; planTime = time; }
                    planWorld = Math.Max(planWorld, world);
                }
            }
            var watch = Stopwatch.StartNew();
            double localTranslation = 0, localScale = 0, localRotation = 0, worldBone = 0, skin = 0, contact = 0, root = 0;
            string worstBone = null; float worstTime = 0; long bytes = 0; bool reopened = false, resaved = false;
            string failure = null; RetargetKeyframeReport keyReport = null;
            if (bake)
            {
                try
                {
                    using (var result = BrawlRetargeter.Bake(source, target, clip, keyframeMode: mode))
                    {
                        keyReport = result.KeyframeReport;
                        retainedMappings = result.Plan.Mapping.Length;
                        var retained = new HashSet<string>(result.Plan.Mapping.Select(pair => pair.Source), StringComparer.Ordinal);
                        retainedUnmapped = clip.Children.OfType<CHR0EntryNode>()
                            .Where(entry => !retained.Contains(entry.Name) && entry.KeyArrays.Any(array => array.KeyCount > 0))
                            .Select(entry => new { entry.Name, Keys = entry.KeyArrays.Sum(array => array.KeyCount) }).ToArray();
                        string file = Path.Combine(output, "same-rig-target.chr0");
                        result.Animation.ExportChecked(file); bytes = new FileInfo(file).Length;
                        using (var decoded = NodeFactory.FromFile(null, file) as CHR0Node)
                        {
                            if (decoded == null || decoded.FrameCount != clip.FrameCount || decoded.Version != clip.Version || decoded.Loop != clip.Loop)
                                throw new InvalidDataException("Same-rig native output did not reopen with original timing and version.");
                            reopened = true;
                            foreach (float time in times)
                            {
                                var original = BrawlRetargeter.Sample(source, clip, time);
                                var actual = BrawlRetargeter.Sample(target, decoded, time);
                                for (int bone = 0; bone < source.Bones.Length; bone++)
                                {
                                    localTranslation = Math.Max(localTranslation, Vector3.Distance(original.Local[bone].Translation, actual.Local[bone].Translation));
                                    localScale = Math.Max(localScale, Vector3.Distance(original.Local[bone].Scale, actual.Local[bone].Scale));
                                    localRotation = Math.Max(localRotation, Angle(original.Local[bone].Rotation, actual.Local[bone].Rotation));
                                    double position = Vector3.Distance(original.Point(bone), actual.Point(bone));
                                    if (position > worldBone) { worldBone = position; worstBone = source.Bones[bone].Name; worstTime = time; }
                                    if (plan.SourceMotionControls.Contains(bone))
                                        root = Math.Max(root, Math.Max(Vector3.Distance(original.Local[bone].Translation, actual.Local[bone].Translation),
                                            Angle(original.Local[bone].Rotation, actual.Local[bone].Rotation)));
                                }
                                foreach (string role in source.Rig.Surfaces.Keys.Intersect(target.Rig.Surfaces.Keys))
                                {
                                    var a = source.Rig.Surfaces[role].Sample(original);
                                    var b = target.Rig.Surfaces[role].Sample(actual);
                                    contact = Math.Max(contact, Math.Max(Math.Abs(a.Clearance - b.Clearance), Vector3.Distance(a.Point, b.Point)));
                                }
                                sourceModel.ApplyCHR(clip, time + 1); targetModel.ApplyCHR(decoded, time + 1);
                                var originalSkin = Vertices(sourceModel); var targetSkin = Vertices(targetModel);
                                if (originalSkin.Length != targetSkin.Length) throw new InvalidDataException("The identical native rigs have different vertex inventories.");
                                for (int vertex = 0; vertex < originalSkin.Length; vertex++)
                                    skin = Math.Max(skin, Vector3.Distance(originalSkin[vertex], targetSkin[vertex]));
                            }
                            string copy = Path.Combine(output, "same-rig-resaved.chr0"); decoded.ExportChecked(copy);
                            using (var again = NodeFactory.FromFile(null, copy) as CHR0Node)
                                resaved = again != null && NativeAnimationOptimization.ContentKey(again) == NativeAnimationOptimization.ContentKey(decoded);
                        }
                    }
                }
                catch (Exception error) { failure = error.ToString(); }
            }
            bool owners = beforeSource == NativeOwnership(sourceModel) && beforeTarget == NativeOwnership(targetModel) &&
                beforeClip == NativeAnimationOptimization.SourceContentKey(clip) && Same(modelHash, FighterProject.HashFile(modelPath)) &&
                Same(motionHash, FighterProject.HashFile(motionPath)) && Same(manifestHash, FighterProject.HashFile(manifestPath));
            double lengthScale = NativeAnimationOptimization.GetBindBoundsDiagonal(targetModel);
            double translationBound = mode == RetargetKeyframeMode.Dense ? .00001 : Math.Min(.0002, .0001 * lengthScale);
            double geometryBound = mode == RetargetKeyframeMode.Dense ? .00001 : .0001 * lengthScale;
            double rotationBound = mode == RetargetKeyframeMode.Dense ? .0001 : .01;
            bool pass = bake && failure == null && reopened && resaved && owners && retainedUnmapped.Length == 0 &&
                (mode == RetargetKeyframeMode.Dense || keyReport != null && keyReport.QualityVerified && keyReport.ScalarCertified) &&
                localTranslation <= translationBound && localScale <= .00001 && localRotation <= rotationBound && worldBone <= geometryBound &&
                skin <= geometryBound && contact <= geometryBound && root <= .00001;
            File.WriteAllText(Path.Combine(output, "same-rig-quality.json"), serializer.Serialize(new
            {
                Passed = pass, AuditOnly = !bake, SourceModelSha256 = modelHash, SourceMotionSha256 = motionHash,
                ManifestSha256 = manifestHash, SourceRig = source.Fingerprint, Clip = clip.Name, Member = Member(clip), Mode = mode.ToString(),
                clip.FrameCount, clip.Version, clip.Loop, Samples = times.Count, RoleMappedBones = plan.Mapping.Length,
                RoleUnmappedEntries = ignoredAnimated, RetainedMappedBones = retainedMappings,
                RetainedUnmappedEntries = retainedUnmapped, RolePlanMaximumLocal = planLocal, RolePlanWorstBone = planBone,
                RolePlanWorstTime = planTime, RolePlanMaximumWorld = planWorld, Reopened = reopened, Resaved = resaved,
                StandaloneBytes = bytes, MaximumLocalTranslation = localTranslation, MaximumLocalScale = localScale,
                MaximumLocalRotationDegrees = localRotation, MaximumWorldBone = worldBone, MaximumSkinnedVertex = skin,
                MaximumContactPatch = contact, MaximumRootControl = root, WorstBone = worstBone, WorstTime = worstTime,
                SourceAndTargetUnchanged = owners, LengthScale = lengthScale, TranslationBound = translationBound,
                GeometryBound = geometryBound, RotationBoundDegrees = rotationBound,
                KeyReport = keyReport, Milliseconds = watch.ElapsedMilliseconds, Failure = failure
            }));
            if (!bake) return 0;
            if (!pass) throw new InvalidDataException("Same-rig native quality failed; see same-rig-quality.json.");
            return 0;
        }
    }

    static double Angle(Quaternion a, Quaternion b)
    {
        double dot = Math.Abs((double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z + (double)a.W * b.W);
        double aa = (double)a.X * a.X + (double)a.Y * a.Y + (double)a.Z * a.Z + (double)a.W * a.W;
        double bb = (double)b.X * b.X + (double)b.Y * b.Y + (double)b.Z * b.Z + (double)b.W * b.W;
        return 2 * Math.Acos(Math.Min(1, dot / Math.Sqrt(aa * bb))) * 180 / Math.PI;
    }
}
