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
    // The review text is supplied by the caller for an exact private fixture.
    // This runner never infers a head joint, changes a model, or admits a
    // production mapping on behalf of a user. Its success is bounded native
    // evidence, not general retarget or game-runtime acceptance.
    private static int ReviewedCranialRetargetCase(string sourcePath, string targetPath,
        string motionPath, string manifestPath, string output, string reviewEvidence)
    {
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new InvalidDataException("Use a fresh reviewed-cranial evidence directory.");
        Directory.CreateDirectory(output);
        var json = new JavaScriptSerializer {MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 100};
        var receipt = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            {"Schema", "fighterport-reviewed-cranial-retarget-case-1"},
            {"AcceptedScope", "exact private native fixture; finite motion, full-skin and contact witnesses"},
            {"GameRuntimeVerified", false},
            {"GeneralRetargetQualityVerified", false},
            {"ReviewOrigin", "explicit test-fixture caller evidence; no production approval persisted"},
            {"Stage", "input"}
        };
        var watch = Stopwatch.StartNew();
        string[] paths = {sourcePath, targetPath, motionPath, manifestPath};
        Dictionary<string, string> hashes = null;
        string sourceOwnership = null, targetOwnership = null, sourceContent = null;
        MDL0Node sourceModel = null, targetModel = null;
        CHR0Node sourceClip = null;
        bool passed = false;
        try
        {
            if (new FileInfo(manifestPath).Length > 16384)
                throw new InvalidDataException("Unbounded exact fixture manifest.");
            var fixture = json.Deserialize<TransferFixture>(File.ReadAllText(manifestPath));
            hashes = paths.Distinct().ToDictionary(path => path, FighterProject.HashFile);
            if (fixture == null || fixture.Schema != "fighterport-authentic-export-fixture-1" ||
                !Same(hashes[sourcePath], fixture.SourceModelSha256) ||
                !Same(hashes[targetPath], fixture.TargetModelSha256) ||
                !Same(hashes[motionPath], fixture.SourceClipSha256))
                throw new InvalidDataException("Exact fixture input hashes differ.");
            receipt["FixtureManifestSha256"] = hashes[manifestPath];
            receipt["SourceModelSha256"] = hashes[sourcePath];
            receipt["TargetModelSha256"] = hashes[targetPath];
            receipt["SourceMotionSha256"] = hashes[motionPath];

            using (var sourceRoot = NodeFactory.FromFile(null, sourcePath))
            using (var targetRoot = NodeFactory.FromFile(null, targetPath))
            using (var motionRoot = NodeFactory.FromFile(null, motionPath))
            {
                sourceModel = Walk(sourceRoot).OfType<MDL0Node>().Single(model =>
                    Same(BrawlRetargeter.SkeletonFingerprint(model), fixture.SourceRigFingerprint));
                targetModel = Walk(targetRoot).OfType<MDL0Node>().Single(model =>
                    Same(BrawlRetargeter.SkeletonFingerprint(model), fixture.TargetRigFingerprint));
                string Member(ResourceNode node) => node.Parent == null ? "root" : Member(node.Parent) + "/" + node.Index;
                sourceClip = Walk(motionRoot).OfType<CHR0Node>().Single(clip =>
                    clip.Name == fixture.SourceClipName && Member(clip) == fixture.SourceNativeMember);
                if (sourceClip.FrameCount != fixture.SourceFrames ||
                    sourceClip.Version != fixture.SourceVersion || sourceClip.Loop != fixture.SourceLoop)
                    throw new InvalidDataException("Exact native member timing differs.");
                sourceOwnership = NativeOwnership(sourceModel);
                targetOwnership = NativeOwnership(targetModel);
                sourceContent = NativeAnimationOptimization.SourceContentKey(sourceClip);
                try
                {
                receipt["SourceMember"] = Member(sourceClip);
                receipt["SourceClip"] = sourceClip.Name;
                receipt["SourceFrames"] = sourceClip.FrameCount;
                receipt["SourceVersion"] = sourceClip.Version;
                receipt["SourceLoop"] = sourceClip.Loop;
                receipt["SourceAuthoredEntries"] = sourceClip.Children.OfType<CHR0EntryNode>().Count();
                receipt["SourceAuthoredKeys"] = sourceClip.Children.OfType<CHR0EntryNode>()
                    .Sum(entry => entry.KeyArrays.Sum(array => (long)array.KeyCount));

                receipt["Stage"] = "cranial proposal";
                var characterization = BrawlRetargeter.Characterize(sourceModel);
                var proposal = BrawlRetargeter.ProposeCranialAnchor(sourceModel, characterization);
                receipt["CranialProposal"] = new
                {
                    proposal.Available, proposal.RejectionReason, proposal.NeckIndex,
                    proposal.NeckBoneName, proposal.RigAndSkinDigest,
                    proposal.DecodedVertexCount, proposal.RepresentedWeightCount,
                    proposal.BindLocalPoint, proposal.Radius,
                    CandidateVertices = proposal.CandidateVertexIndices.Count
                };
                if (!proposal.Available)
                    throw new InvalidDataException("The exact source has no reviewable cranial patch: " + proposal.RejectionReason);
                var review = new CranialAnchorReview(proposal.RigAndSkinDigest,
                    proposal.NeckBoneName, reviewEvidence);
                receipt["ReviewEvidence"] = review.Evidence;
                var source = BrawlRetargeter.Capture(sourceModel, null, null, null, review);
                var target = BrawlRetargeter.Capture(targetModel);
                int bakeCount = BrawlRetargeter.DiagnosticBakes;
                string changedDigest = (proposal.RigAndSkinDigest[0] == '0' ? "1" : "0") +
                    proposal.RigAndSkinDigest.Substring(1);
                bool staleDigestRejected = ReviewedCaptureRejects(sourceModel,
                    new CranialAnchorReview(changedDigest, proposal.NeckBoneName, reviewEvidence));
                bool wrongNeckRejected = ReviewedCaptureRejects(sourceModel,
                    new CranialAnchorReview(proposal.RigAndSkinDigest,
                        proposal.NeckBoneName + "-different", reviewEvidence));
                receipt["StaleSkinDigestRejectedBeforeBake"] = staleDigestRejected;
                receipt["WrongNeckRejectedBeforeBake"] = wrongNeckRejected;
                if (!staleDigestRejected || !wrongNeckRejected ||
                    BrawlRetargeter.DiagnosticBakes != bakeCount ||
                    Directory.EnumerateFiles(output, "*.chr0").Any())
                    throw new InvalidDataException("Changed cranial review evidence was not rejected before Bake or output.");
                if (source.ProvisionalHeadForExactNativeCopy || source.Rig.CranialAnchor == null ||
                    source.Rig.Roles.ContainsKey("head") || !target.Rig.Roles.ContainsKey("head") ||
                    source.Rig.CranialAnchor.ParentJoint != source.Rig.Roles["neck"])
                    throw new InvalidDataException("The source virtual surface or target real-head contract was lost.");
                CranialSurfaceMetric cranialTarget = BrawlRetargeter.PrepareCranialPair(source, target);
                var plan = new HumanoidRetargeter(source.Rig, target.Rig,
                    null, null, "detail.finger", cranialTarget);
                int sourceNeck = source.Rig.Roles["neck"], targetHead = target.Rig.Roles["head"];
                if (plan.Mapping.Count(map => map.SourceIndex == sourceNeck) != 1 ||
                    plan.Mapping.Any(map => map.Role == "head" || map.TargetIndex == targetHead))
                    throw new InvalidDataException("The reviewed neck must map once; a branch must not map as the target head.");
                receipt["SourceRig"] = source.Fingerprint;
                receipt["TargetRig"] = target.Fingerprint;
                receipt["NeckMappedOnce"] = true;
                receipt["TargetHeadDirectlyMapped"] = false;
                receipt["MappingCount"] = plan.Mapping.Length;
                receipt["SourceUnmapped"] = plan.UnmappedSource;
                receipt["TargetUnmapped"] = plan.UnmappedTarget;

                float[] times = TransferWitnessTimes(sourceClip);
                receipt["FiniteWitnessTimes"] = times;
                receipt["WitnessCoverage"] = "deterministic endpoints, midpoint and withheld fractional times; mesh-floor helper adds all integer frames and refinements when available";
                receipt["Stage"] = "source, mapping and solver preflight";
                object preflight = CaptureTransferPreflight(source, target, sourceModel, targetModel,
                    sourceClip, times, cranialTarget);
                receipt["SourceMappingSolver"] = preflight;
                if (ReviewedWitnessFailed(json, preflight))
                    throw new InvalidDataException("The source/mapping/solver preflight has failed samples.");
                File.WriteAllText(Path.Combine(output, "preflight.json"), json.Serialize(receipt));

                receipt["Stage"] = "full-quality native bake";
                using (var result = BrawlRetargeter.Bake(source, target, sourceClip,
                    groundMode: GroundContactMode.Auto,
                    progress: (percent, message) => Console.WriteLine(watch.ElapsedMilliseconds +
                        " ms " + percent + "% " + message)))
                {
                    receipt["IgnoredSourceTracks"] = result.IgnoredSourceTracks;
                    receipt["IgnoredSourceBones"] = result.IgnoredSourceBones;
                    receipt["MaximumLimbResidual"] = result.MaximumLimbResidual;
                    receipt["MaximumContactResidual"] = result.MaximumContactResidual;
                    receipt["MaximumPelvisCorrection"] = result.MaximumPelvisCorrection;
                    receipt["ClampedLimbSamples"] = result.ClampedLimbSamples;
                    object contacts = CaptureTransferContacts(source, target, targetModel,
                        sourceClip, result, times);
                    receipt["ContactCorrection"] = contacts;
                    if (ReviewedWitnessFailed(json, contacts))
                        throw new InvalidDataException("A full-quality contact witness failed.");

                    string outputPath = Path.Combine(output, "reviewed-full-quality-target.chr0");
                    result.Animation.ExportChecked(outputPath);
                    receipt["Stage"] = "fresh native reopen and complete target witness";
                    using (var fresh = NodeFactory.FromFile(null, outputPath) as CHR0Node)
                    {
                        if (fresh == null ||
                            NativeAnimationOptimization.ContentKey(fresh) !=
                            NativeAnimationOptimization.ContentKey(result.Animation))
                            throw new InvalidDataException("The full-quality target changed on fresh native reopen.");
                        receipt["FreshReopenSameContent"] = true;
                        receipt["OutputSha256"] = FighterProject.HashFile(outputPath);
                        receipt["OutputBytes"] = new FileInfo(outputPath).Length;
                        receipt["BakedToReopened"] = CompareTransferNative(target, targetModel,
                            result.Animation, fresh, times, result.Contacts);
                        receipt["BakedToReopenedFiniteOnly"] = true;
                        receipt["WorstWholeMeshFloorVertex"] = ReviewedWorstFloorVertex(
                            source, sourceModel, sourceClip, target, targetModel, fresh);
                        var trajectory = ReviewedCranialTrajectory(target, targetModel,
                            sourceClip, fresh, result.Contacts, result.Plan);
                        receipt["TrajectoryQuality"] = trajectory;
                        if (!trajectory.Passed)
                            throw new InvalidDataException("The unchanged eighth-frame, withheld-fractional and reviewed contact trajectory limits were not met.");
                    }
                }
                passed = true;
                }
                finally
                {
                    sourceModel.ApplyCHR(null, 0);
                    targetModel.ApplyCHR(null, 0);
                    receipt["SourceNativeOwnershipUnchanged"] = sourceOwnership == NativeOwnership(sourceModel);
                    receipt["TargetNativeOwnershipUnchanged"] = targetOwnership == NativeOwnership(targetModel);
                    receipt["SourceClipUnchanged"] = sourceContent ==
                        NativeAnimationOptimization.SourceContentKey(sourceClip);
                }
            }
        }
        catch (Exception error)
        {
            receipt["Failure"] = WitnessError(error);
            if (error.Data.Contains("NativeCurveWitness"))
                receipt["NativeCurveWitness"] = Convert.ToString(error.Data["NativeCurveWitness"]);
        }
        finally
        {
            if (hashes != null)
                receipt["OriginalFilesUnchanged"] = hashes.All(pair => FighterProject.HashFile(pair.Key) == pair.Value);
            receipt["Milliseconds"] = watch.ElapsedMilliseconds;
            passed = passed && !receipt.ContainsKey("Failure") &&
                receipt.ContainsKey("SourceNativeOwnershipUnchanged") && (bool)receipt["SourceNativeOwnershipUnchanged"] &&
                receipt.ContainsKey("TargetNativeOwnershipUnchanged") && (bool)receipt["TargetNativeOwnershipUnchanged"] &&
                receipt.ContainsKey("SourceClipUnchanged") && (bool)receipt["SourceClipUnchanged"] &&
                receipt.ContainsKey("OriginalFilesUnchanged") && (bool)receipt["OriginalFilesUnchanged"];
            receipt["PassedBoundedNativeCase"] = passed;
            File.WriteAllText(Path.Combine(output, "reviewed-cranial-retarget.json"), json.Serialize(receipt));
        }
        return passed ? 0 : 1;
    }

    private static bool ReviewedWitnessFailed(JavaScriptSerializer json, object witness)
    {
        string serialized = json.Serialize(witness);
        return serialized.Contains("\"Failure\":") || serialized.Contains("\"Available\":false");
    }

    private static bool ReviewedCaptureRejects(MDL0Node source, CranialAnchorReview review)
    {
        try { BrawlRetargeter.Capture(source, null, null, null, review); return false; }
        catch (InvalidDataException) { return true; }
    }

    private sealed class ReviewedTrajectoryReport
    {
        public bool Passed { get; set; }
        public int Samples { get; set; }
        public int ContactSamples { get; set; }
        public int AllOriginalEighthFrames { get; set; }
        public int WithheldFractionalSamples { get; set; }
        public double MaximumAnchorOrientationDegrees { get; set; }
        public double MaximumAnchorTranslation { get; set; }
        public double MaximumAnchorScale { get; set; }
        public double MaximumAnchorWorldBonePosition { get; set; }
        public double RootLocalError { get; set; }
        public double NativeBoneAgreement { get; set; }
        public double NativeSurfaceAgreement { get; set; }
        public double MaximumSupportedPenetration { get; set; }
        public double MaximumNativeSupportedPenetration { get; set; }
        public double MaximumPlanarLockDrift { get; set; }
        public double MaximumSupportOrientationDegrees { get; set; }
        public double MaximumIntendedSupportOrientationDegrees { get; set; }
        public double ContactPositionBound { get; set; }
        public int NativeContactPatchVertexCount { get; set; }
        public bool FullSkinComparisonPerformed { get; set; }
        public bool FiniteSamplesOnly { get; set; }
    }

    private static object ReviewedWorstFloorVertex(BrawlRig source, MDL0Node sourceModel,
        CHR0Node sourceClip, BrawlRig target, MDL0Node targetModel, CHR0Node saved)
    {
        int last = sourceClip.Loop ? sourceClip.FrameCount : sourceClip.FrameCount - 1;
        int[] frames = last < 512 ? Enumerable.Range(0, last + 1).ToArray() :
            Enumerable.Range(0, 128).Concat(Enumerable.Range(last - 127, 128))
                .Concat(Enumerable.Range(0, 256)
                    .Select(i => (int)Math.Round(i * last / 255d)))
                .Distinct().OrderBy(i => i).ToArray();
        float worstTime = 0;
        int worstVertex = -1;
        double minimum = double.PositiveInfinity;
        Vector3 posedTarget = Vector3.Zero;
        try
        {
            foreach (int frame in frames)
            {
                targetModel.ApplyCHR(saved, frame + 1);
                Vector3[] vertices = Vertices(targetModel);
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (!RigMath.Finite(vertices[i]))
                        throw new InvalidDataException("A native whole-skin vertex is non-finite.");
                    double signedHeight = Vector3.Dot(vertices[i], target.Rig.GroundNormal) -
                        target.Rig.GroundHeight;
                    if (signedHeight >= minimum) continue;
                    minimum = signedHeight;
                    worstTime = frame;
                    worstVertex = i;
                    posedTarget = vertices[i];
                }
            }
            if (worstVertex < 0)
                throw new InvalidDataException("No complete target skin vertex was evaluated.");
            var vertex = targetModel.PolygonList.Cast<MDL0ObjectNode>()
                .SelectMany(obj => obj.Vertices).ElementAt(worstVertex);
            var weights = vertex.GetMatrixNode()?.Weights;
            if (weights == null || weights.Count == 0 || weights.Count > 64)
                throw new InvalidDataException("The worst target vertex has missing or unbounded native skin weights.");
            var completeWeights = weights.Select(weight => new
            {
                Bone = (weight.Bone as MDL0BoneNode)?.Name,
                weight.Weight
            }).ToArray();
            if (completeWeights.Any(weight => weight.Bone == null ||
                !RigMath.Finite(weight.Weight)))
                throw new InvalidDataException("The worst vertex has an unknown or non-finite influence.");
            targetModel.ApplyCHR(null, 0);
            Vector3 bindTarget = Vertices(targetModel)[worstVertex];

            sourceModel.ApplyCHR(sourceClip, worstTime + 1);
            Vector3[] sourceVertices = Vertices(sourceModel);
            double sourceMinimum = double.PositiveInfinity;
            int sourceWorstVertex = -1;
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                if (!RigMath.Finite(sourceVertices[i]))
                    throw new InvalidDataException("A source native skin vertex is non-finite.");
                double height = Vector3.Dot(sourceVertices[i], source.Rig.GroundNormal) -
                    source.Rig.GroundHeight;
                if (height >= sourceMinimum) continue;
                sourceMinimum = height;
                sourceWorstVertex = i;
            }
            return new
            {
                Time = worstTime,
                TargetVertex = worstVertex,
                TargetVertexCount = targetModel.PolygonList.Cast<MDL0ObjectNode>()
                    .Sum(obj => obj.Vertices.Count),
                TargetBindWorld = bindTarget,
                TargetAnimatedWorld = posedTarget,
                TargetSignedFloorHeight = minimum,
                TargetFloorNormal = target.Rig.GroundNormal,
                TargetGroundHeight = target.Rig.GroundHeight,
                Influences = completeWeights,
                RepresentedInfluenceCount = completeWeights.Length,
                RepresentedWeightSum = completeWeights.Sum(weight => weight.Weight),
                SourceWorstVertexAtSameTime = sourceWorstVertex,
                SourceWorstSignedFloorHeightAtSameTime = sourceMinimum,
                SourceFloorNormal = source.Rig.GroundNormal,
                SourceGroundHeight = source.Rig.GroundHeight,
                IntegerFramesEvaluated = frames.Length,
                AllIntegerFramesEvaluated = frames.Length == last + 1,
                FiniteSamplesOnly = true,
                Scope = "global skinned-vertex floor minimum, including unsupported attack intervals; diagnostic, not a planted-contact failure"
            };
        }
        finally
        {
            sourceModel.ApplyCHR(null, 0);
            targetModel.ApplyCHR(null, 0);
        }
    }

    // Same numeric gates as --verify-trajectory, with the already-reviewed
    // source rig and contact solution supplied instead of recapturing a
    // provisional head. Whole-skin master/reopen comparison is recorded
    // separately by CompareTransferNative at its stated diagnostic times.
    private static ReviewedTrajectoryReport ReviewedCranialTrajectory(BrawlRig target,
        MDL0Node targetModel, CHR0Node sourceClip, CHR0Node saved,
        ContactMotion contacts, HumanoidRetargeter plan)
    {
        int last = sourceClip.Loop ? sourceClip.FrameCount : sourceClip.FrameCount - 1;
        var times = new SortedSet<float>(Enumerable.Range(0, last * 8 + 1)
            .Select(index => index / 8f));
        foreach (int frame in Enumerable.Range(0, last))
            foreach (float fraction in new[] {.137f, .371f, .623f})
                times.Add(frame + fraction);
        var nativeSurfaces = target.Rig.Surfaces.Keys.ToDictionary(role => role, role =>
            targetModel.PolygonList.Cast<MDL0ObjectNode>().SelectMany(obj => obj.Vertices)
                .Where(vertex => NativeSurfaceMember(vertex, target, role,
                    target.Bones[target.Rig.Roles[role]])).ToArray());
        double contactBound = new[] {"left", "right"}.Min(side =>
            Vector3.Distance(target.Rig.Rest.Point(side + ".upperLeg"),
                target.Rig.Rest.Point(side + ".lowerLeg")) +
            Vector3.Distance(target.Rig.Rest.Point(side + ".lowerLeg"),
                target.Rig.Rest.Point(side + ".foot"))) * .001;
        var report = new ReviewedTrajectoryReport
        {
            AllOriginalEighthFrames = last * 8 + 1,
            WithheldFractionalSamples = last * 3,
            ContactPositionBound = contactBound,
            NativeContactPatchVertexCount = nativeSurfaces.Sum(pair => pair.Value.Length),
            FullSkinComparisonPerformed = false,
            FiniteSamplesOnly = true
        };
        try
        {
            foreach (float time in times)
            {
                RigPose expected = contacts.Evaluate(time).Pose;
                RigPose actual = BrawlRetargeter.Sample(target, saved, time);
                var intended = contacts.EndOrientationReference(time);
                targetModel.ApplyCHR(saved, time + 1);
                bool originalAnchor = time * 8 == (int)(time * 8);
                report.Samples++;
                for (int bone = 0; bone < target.Bones.Length; bone++)
                {
                    if (originalAnchor)
                    {
                        report.MaximumAnchorOrientationDegrees = Math.Max(report.MaximumAnchorOrientationDegrees,
                            SpanAngle(actual.Orientation[bone], expected.Orientation[bone]));
                        report.MaximumAnchorTranslation = Math.Max(report.MaximumAnchorTranslation,
                            Vector3.Distance(actual.Local[bone].Translation, expected.Local[bone].Translation));
                        report.MaximumAnchorScale = Math.Max(report.MaximumAnchorScale,
                            Vector3.Distance(actual.Local[bone].Scale, expected.Local[bone].Scale));
                        report.MaximumAnchorWorldBonePosition = Math.Max(report.MaximumAnchorWorldBonePosition,
                            Vector3.Distance(actual.Point(bone), expected.Point(bone)));
                    }
                    if (plan.TargetMotionControls.Contains(bone))
                        report.RootLocalError = Math.Max(report.RootLocalError,
                            Math.Max(SpanAngle(actual.Local[bone].Rotation, expected.Local[bone].Rotation),
                                Vector3.Distance(actual.Local[bone].Translation,
                                    expected.Local[bone].Translation)));
                    report.NativeBoneAgreement = Math.Max(report.NativeBoneAgreement,
                        Vector3.Distance(actual.Point(bone),
                            BrawlRetargeter.Vector(target.Bones[bone].Matrix.GetPoint())));
                }
                foreach (var pair in target.Rig.Surfaces)
                {
                    var goal = contacts.At(pair.Key, time);
                    if (goal.Support <= .99f) continue;
                    report.ContactSamples++;
                    if (pair.Key.EndsWith(".foot", StringComparison.Ordinal) ||
                        pair.Key.EndsWith(".hand", StringComparison.Ordinal))
                    {
                        int joint = target.Rig.Roles[pair.Key];
                        report.MaximumSupportOrientationDegrees = Math.Max(
                            report.MaximumSupportOrientationDegrees,
                            SpanAngle(actual.Orientation[joint], expected.Orientation[joint]));
                        if (!intended.TryGetValue(pair.Key, out Quaternion goalOrientation))
                            throw new InvalidDataException("Supported end has no intended orientation.");
                        report.MaximumIntendedSupportOrientationDegrees = Math.Max(
                            report.MaximumIntendedSupportOrientationDegrees,
                            SpanAngle(actual.Orientation[joint], goalOrientation));
                    }
                    double low = pair.Value.Points.Min(point =>
                        Vector3.Dot(point.Evaluate(actual), target.Rig.GroundNormal)) -
                        target.Rig.GroundHeight;
                    report.MaximumSupportedPenetration = Math.Max(
                        report.MaximumSupportedPenetration, Math.Max(0, -low));
                    var nativePoints = nativeSurfaces[pair.Key];
                    if (nativePoints.Length > 0)
                    {
                        double nativeLow = nativePoints.Min(vertex =>
                            Vector3.Dot(BrawlRetargeter.Vector(vertex.WeightedPosition),
                                target.Rig.GroundNormal)) - target.Rig.GroundHeight;
                        report.NativeSurfaceAgreement = Math.Max(report.NativeSurfaceAgreement,
                            Math.Abs(nativeLow - low));
                        report.MaximumNativeSupportedPenetration = Math.Max(
                            report.MaximumNativeSupportedPenetration, Math.Max(0, -nativeLow));
                    }
                    if (goal.Marker != null && goal.Lock > .99f)
                    {
                        Vector3 drift = goal.Marker.Evaluate(actual) - goal.Anchor;
                        drift -= target.Rig.GroundNormal *
                            Vector3.Dot(drift, target.Rig.GroundNormal);
                        report.MaximumPlanarLockDrift = Math.Max(report.MaximumPlanarLockDrift,
                            drift.Length());
                    }
                }
            }
        }
        finally { targetModel.ApplyCHR(null, 0); }
        report.Passed = report.MaximumAnchorOrientationDegrees <= .025 &&
            report.MaximumAnchorTranslation <= .0002 &&
            report.MaximumAnchorScale <= .00001 &&
            report.RootLocalError < .0001 &&
            report.NativeBoneAgreement < .01 &&
            report.NativeSurfaceAgreement < .01 &&
            report.MaximumSupportedPenetration <= contactBound &&
            report.MaximumNativeSupportedPenetration <= contactBound &&
            report.MaximumPlanarLockDrift <= contactBound &&
            report.MaximumSupportOrientationDegrees <= .25 &&
            report.MaximumIntendedSupportOrientationDegrees <= .25;
        return report;
    }
}
