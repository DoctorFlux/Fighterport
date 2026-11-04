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
                        var earlyHandBody = ReviewedEarlyHandBodyWitness(
                            source, target, sourceClip, result, fresh);
                        receipt["EarlyHandBodyWitness"] = earlyHandBody;
                        double palm=Convert.ToDouble(earlyHandBody["MaximumNormalizedEarlyHandClearanceError"]);
                        double forearm=Convert.ToDouble(earlyHandBody["MaximumNormalizedEarlyLowerArmClearanceError"]);
                        double trunk=Convert.ToDouble(earlyHandBody["MaximumExtraPelvisDescent"]);
                        double leg=Convert.ToDouble(earlyHandBody["TargetLegLength"]);
                        receipt["EarlyHandBodySampleGate"] = new {
                            Scope="existing proposed early hand/forearm/trunk bounds at sampled native times; guide is not a verified game floor",
                            PalmArmFractionBound=.05, ForearmArmFractionBound=.10,
                            TrunkLegFractionBound=.01,
                            MaximumPalmArmFraction=palm, MaximumForearmArmFraction=forearm,
                            MaximumExtraTrunkNativeUnits=trunk, TargetLegNativeUnits=leg,
                            Passed=palm<=.05&&forearm<=.10&&trunk<=.01*leg,
                            SourceGuideOrContactReviewed=false
                        };
                        receipt["ShoulderSeamWitness"] = ReviewedShoulderSeamWitness(
                            source, sourceModel, sourceClip, target, targetModel, fresh);
                        receipt["AnatomicalSkinRegionWitness"] = NativeSkinRegionWitness(
                            source, sourceModel, sourceClip, proposal.CandidateVertexIndices,
                            target, targetModel, fresh, result.Plan);
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

    private static Dictionary<string, object> ReviewedEarlyHandBodyWitness(BrawlRig source, BrawlRig target,
        CHR0Node clip, RetargetResult result, CHR0Node reopened)
    {
        if (result.Plan == null || result.Contacts == null)
            throw new InvalidDataException("The early support witness needs the original plan and contact tracks.");
        string[] hands = {"left.hand", "right.hand"};
        if (hands.Any(role => !source.Rig.Surfaces.ContainsKey(role) ||
            !target.Rig.Surfaces.ContainsKey(role)))
            throw new InvalidDataException("An authentic hand patch is missing from the early support witness.");
        float ArmLength(Rig rig, string side) =>
            Vector3.Distance(rig.Rest.Point(side + ".upperArm"), rig.Rest.Point(side + ".lowerArm")) +
            Vector3.Distance(rig.Rest.Point(side + ".lowerArm"), rig.Rest.Point(side + ".hand"));
        float LegLength(Rig rig, string side) =>
            Vector3.Distance(rig.Rest.Point(side + ".upperLeg"), rig.Rest.Point(side + ".lowerLeg")) +
            Vector3.Distance(rig.Rest.Point(side + ".lowerLeg"), rig.Rest.Point(side + ".foot"));
        var armLengths = hands.ToDictionary(role => role, role => new
        {
            Source = ArmLength(source.Rig, role.Split('.')[0]),
            Target = ArmLength(target.Rig, role.Split('.')[0])
        });
        if (armLengths.Values.Any(length => !RigMath.Finite(length.Source) ||
            !RigMath.Finite(length.Target) || length.Source <= 0 || length.Target <= 0))
            throw new InvalidDataException("The early support witness has no positive arm scale.");
        float targetLegLength = (LegLength(target.Rig, "left") +
            LegLength(target.Rig, "right")) * .5f;
        if (!RigMath.Finite(targetLegLength) || targetLegLength <= 0)
            throw new InvalidDataException("The early body witness has no positive target leg scale.");
        float sourceRestPelvisClearance = source.Rig.Surfaces["pelvis"].Clearance(source.Rig.Rest);
        float targetRestPelvisClearance = target.Rig.Surfaces["pelvis"].Clearance(target.Rig.Rest);

        int last = clip.Loop ? clip.FrameCount : clip.FrameCount - 1;
        int quarterIntervals = Math.Min(last * 4, 12 * 4);
        int[] supported = new int[hands.Length];
        int[] consecutive = new int[hands.Length];
        int[] longest = new int[hands.Length];
        double maxNativeHandPatchDelta = 0;
        double maxNormalizedEarlyHandClearanceError = 0;
        double maxNormalizedEarlyLowerArmClearanceError = 0;
        double maxExtraPelvisDescent = 0;
        bool inventedEarlyHandSupport = false;
        bool inventedEarlyLowerArmSupport = false;
        var rows = new List<object>();
        for (int index = 0; index <= quarterIntervals; index++)
        {
            float time = index / 4f;
            RigPose sourcePose = BrawlRetargeter.Sample(source, clip, time);
            float before = Math.Max(0, time - .25f), after = Math.Min(last, time + .25f);
            float deltaTime = Math.Max(.25f, after - before);
            RigPose sourceBefore = BrawlRetargeter.Sample(source, clip, before);
            RigPose sourceAfter = BrawlRetargeter.Sample(source, clip, after);
            RetargetPose rawResult = result.Plan.Evaluate(sourcePose);
            RigPose raw = rawResult.Pose;
            RigPose solved = result.Contacts.Evaluate(time).Pose;
            RigPose native = BrawlRetargeter.Sample(target, reopened, time);
            var handRows = new List<object>();
            for (int h = 0; h < hands.Length; h++)
            {
                string role = hands[h];
                string side = role.Split('.')[0];
                string upperRole = side + ".upperArm";
                int sourceUpper = source.Rig.Roles[upperRole];
                int targetUpper = target.Rig.Roles[upperRole];
                int sourceUpperParent = source.Rig.Joints[sourceUpper].Parent;
                int targetUpperParent = target.Rig.Joints[targetUpper].Parent;
                Vector3 sourceUpperPoint = sourcePose.Point(upperRole);
                Vector3 rawUpperPoint = raw.Point(upperRole);
                Vector3 rawHandGoal = rawResult.Goals[role];
                float rawUpperToGoal = Vector3.Distance(rawUpperPoint, rawHandGoal);
                float rawArmLength = Vector3.Distance(raw.Point(upperRole),
                    raw.Point(side + ".lowerArm")) +
                    Vector3.Distance(raw.Point(side + ".lowerArm"), raw.Point(role));
                float sourceHeadProximity = Vector3.Distance(sourcePose.Point(role),
                    sourcePose.CranialPoint()) / armLengths[role].Source;
                float headBlendWeight = RigMath.Clamp((.95f - sourceHeadProximity) / .4f, 0, 1);
                var sourcePatch = source.Rig.Surfaces[role].Sample(sourcePose);
                var rawPatch = target.Rig.Surfaces[role].Sample(raw);
                var solvedPatch = target.Rig.Surfaces[role].Sample(solved);
                var nativePatch = target.Rig.Surfaces[role].Sample(native);
                SkinPoint sourceMarker = source.Rig.Surfaces[role].Marker(sourcePose);
                Vector3 sourceVelocity = (sourceMarker.Evaluate(sourceAfter) -
                    sourceMarker.Evaluate(sourceBefore)) / deltaTime;
                float sourceVerticalSpeed = Math.Abs(Vector3.Dot(sourceVelocity,
                    source.Rig.GroundNormal));
                float sourcePlanarSpeed = (sourceVelocity -
                    source.Rig.GroundNormal *
                    Vector3.Dot(sourceVelocity, source.Rig.GroundNormal)).Length();
                ContactGoal goal = result.Contacts.At(role, time);
                if (time >= 5 && time <= 12)
                {
                    maxNormalizedEarlyHandClearanceError = Math.Max(
                        maxNormalizedEarlyHandClearanceError,
                        Math.Abs(sourcePatch.Clearance / armLengths[role].Source -
                                 nativePatch.Clearance / armLengths[role].Target));
                    inventedEarlyHandSupport |= goal.Support > .001f || goal.Lock > .001f;
                }
                if (goal.Support > .99f)
                {
                    supported[h]++;
                    consecutive[h]++;
                    longest[h] = Math.Max(longest[h], consecutive[h]);
                }
                else consecutive[h] = 0;
                maxNativeHandPatchDelta = Math.Max(maxNativeHandPatchDelta,
                    Vector3.Distance(solvedPatch.Point, nativePatch.Point));
                handRows.Add(new
                {
                    Role = role,
                    SourceClearance = sourcePatch.Clearance,
                    SourceClearancePerArmLength = sourcePatch.Clearance / armLengths[role].Source,
                    SourceFlatness = sourcePatch.Flatness,
                    SourceTrackedPlanarSpeed = sourcePlanarSpeed,
                    SourceTrackedVerticalSpeed = sourceVerticalSpeed,
                    SourcePoint = sourcePatch.Point,
                    SourceUpperArmJoint = sourceUpperPoint,
                    SourceRestUpperArmJoint = source.Rig.Rest.Point(upperRole),
                    SourceChestJoint = sourcePose.Point("chest"),
                    SourceRestChestJoint = source.Rig.Rest.Point("chest"),
                    SourceChestToUpperArm = sourceUpperPoint - sourcePose.Point("chest"),
                    SourceHandJoint = sourcePose.Point(role),
                    SourceRestHandJoint = source.Rig.Rest.Point(role),
                    SourceUpperArmParent = sourceUpperParent < 0 ? null :
                        source.Rig.Joints[sourceUpperParent].Name,
                    SourceUpperArmParentJoint = sourceUpperParent < 0 ? Vector3.Zero :
                        sourcePose.World[sourceUpperParent].Translation,
                    SourceParentToUpperArm = sourceUpperParent < 0 ? Vector3.Zero :
                        sourceUpperPoint - sourcePose.World[sourceUpperParent].Translation,
                    RawTargetUpperArmJoint = rawUpperPoint,
                    TargetRestUpperArmJoint = target.Rig.Rest.Point(upperRole),
                    RawTargetChestJoint = raw.Point("chest"),
                    TargetRestChestJoint = target.Rig.Rest.Point("chest"),
                    RawTargetChestToUpperArm = rawUpperPoint - raw.Point("chest"),
                    RawTargetHandJoint = raw.Point(role),
                    TargetRestHandJoint = target.Rig.Rest.Point(role),
                    SolvedTargetHandJoint = solved.Point(role),
                    ReopenedTargetHandJoint = native.Point(role),
                    RawTargetArmGoal = rawHandGoal,
                    RawTargetShoulderToGoal = rawUpperToGoal,
                    RawTargetPosedArmLength = rawArmLength,
                    RawGoalBeyondArmReach = rawUpperToGoal > rawArmLength,
                    SourceHandToCranialPerArm = sourceHeadProximity,
                    HeadGoalBlendWeight = headBlendWeight,
                    TargetUpperArmParent = targetUpperParent < 0 ? null :
                        target.Rig.Joints[targetUpperParent].Name,
                    TargetUpperArmParentMapped = result.Plan.Mapping.Any(map =>
                        map.TargetIndex == targetUpperParent),
                    RawTargetParentJoint = targetUpperParent < 0 ? Vector3.Zero :
                        raw.World[targetUpperParent].Translation,
                    RawTargetParentToUpperArm = targetUpperParent < 0 ? Vector3.Zero :
                        rawUpperPoint - raw.World[targetUpperParent].Translation,
                    RawTargetParentLocalRotation = targetUpperParent < 0 ? Quaternion.Identity :
                        raw.Local[targetUpperParent].Rotation,
                    TargetParentBindLocalRotation = targetUpperParent < 0 ? Quaternion.Identity :
                        target.Rig.Joints[targetUpperParent].Bind.Rotation,
                    goal.Support, goal.Lock,
                    RawTargetClearance = rawPatch.Clearance,
                    SolvedTargetClearance = solvedPatch.Clearance,
                    ReopenedTargetClearance = nativePatch.Clearance,
                    ReopenedClearancePerArmLength = nativePatch.Clearance / armLengths[role].Target,
                    RawTargetPoint = rawPatch.Point,
                    SolvedTargetPoint = solvedPatch.Point,
                    ReopenedTargetPoint = nativePatch.Point
                });
            }
            Vector3 sourceCranial = sourcePose.CranialPoint();
            Vector3 rawCranial = raw.CranialPoint();
            Vector3 solvedCranial = solved.CranialPoint();
            Vector3 nativeCranial = native.CranialPoint();
            float Height(Rig rig, Vector3 point) =>
                Vector3.Dot(point, rig.GroundNormal) - rig.GroundHeight;
            string[] body = {"pelvis", "left.lowerArm", "right.lowerArm"};
            float sourcePelvisClearance = source.Rig.Surfaces["pelvis"].Clearance(sourcePose);
            float nativePelvisClearance = target.Rig.Surfaces["pelvis"].Clearance(native);
            float expectedPelvisLowerEnvelope = targetRestPelvisClearance +
                (sourcePelvisClearance - sourceRestPelvisClearance) * result.Plan.LegScale;
            if (time >= 5 && time <= 12)
            {
                maxExtraPelvisDescent = Math.Max(maxExtraPelvisDescent,
                    expectedPelvisLowerEnvelope - nativePelvisClearance);
                foreach (string side in new[] {"left", "right"})
                {
                    string role = side + ".lowerArm";
                    var sourceArmPatch = source.Rig.Surfaces[role];
                    var targetArmPatch = target.Rig.Surfaces[role];
                    var scale = armLengths[side + ".hand"];
                    maxNormalizedEarlyLowerArmClearanceError = Math.Max(
                        maxNormalizedEarlyLowerArmClearanceError,
                        Math.Abs(sourceArmPatch.Clearance(sourcePose) / scale.Source -
                                 targetArmPatch.Clearance(native) / scale.Target));
                    ContactGoal armGoal = result.Contacts.At(role, time);
                    inventedEarlyLowerArmSupport |= armGoal.Support > .001f ||
                        armGoal.Lock > .001f;
                }
            }
            rows.Add(new
            {
                Time = time,
                Hands = handRows,
                SourceCranialHeight = Height(source.Rig, sourceCranial),
                RawTargetCranialHeight = Height(target.Rig, rawCranial),
                SolvedTargetCranialHeight = Height(target.Rig, solvedCranial),
                ReopenedTargetCranialHeight = Height(target.Rig, nativeCranial),
                SourcePelvisJointHeight = Height(source.Rig, sourcePose.Point("pelvis")),
                RawTargetPelvisJointHeight = Height(target.Rig, raw.Point("pelvis")),
                SolvedTargetPelvisJointHeight = Height(target.Rig, solved.Point("pelvis")),
                ReopenedTargetPelvisJointHeight = Height(target.Rig, native.Point("pelvis")),
                ExpectedPelvisLowerEnvelope = expectedPelvisLowerEnvelope,
                Body = body.Where(role => source.Rig.Surfaces.ContainsKey(role) &&
                    target.Rig.Surfaces.ContainsKey(role)).Select(role => new
                {
                    Role = role,
                    SourceClearance = source.Rig.Surfaces[role].Clearance(sourcePose),
                    RawTargetClearance = target.Rig.Surfaces[role].Clearance(raw),
                    SolvedTargetClearance = target.Rig.Surfaces[role].Clearance(solved),
                    ReopenedTargetClearance = target.Rig.Surfaces[role].Clearance(native)
                }).ToArray()
            });
        }
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            {"Scope", "read-only source-relative early hand/body diagnostic; no floor or gameplay contact inferred"},
            {"QuarterFrames", rows.Count},
            {"LastTime", quarterIntervals / 4f},
            {"SourceGroundHeight", source.Rig.GroundHeight},
            {"TargetGroundHeight", target.Rig.GroundHeight},
            {"SourceRestPelvisJointHeight", Vector3.Dot(source.Rig.Rest.Point("pelvis"),
                source.Rig.GroundNormal) - source.Rig.GroundHeight},
            {"TargetRestPelvisJointHeight", Vector3.Dot(target.Rig.Rest.Point("pelvis"),
                target.Rig.GroundNormal) - target.Rig.GroundHeight},
            {"SourceRestPelvisPatchClearance", sourceRestPelvisClearance},
            {"TargetRestPelvisPatchClearance", targetRestPelvisClearance},
            {"RestBodyJointPositions", new[] {"pelvis", "spine", "chest", "neck"}
                .Where(role => source.Rig.Roles.ContainsKey(role) &&
                    target.Rig.Roles.ContainsKey(role)).Select(role => new
                {
                    Role = role,
                    Source = source.Rig.Rest.Point(role),
                    Target = target.Rig.Rest.Point(role)
                }).ToArray()},
            {"TargetLegLength", targetLegLength},
            {"LegScale", result.Plan.LegScale},
            {"ArmScale", result.Plan.ArmScale},
            {"FloorEnabled", result.Contacts.FloorEnabled},
            {"SourceAndTargetArmLengths", armLengths},
            {"ContactProposalPlantedHandSamples", result.Contacts.PlantedHandSamples},
            {"ContactProposalFullSupportQuarterSamples", hands.Select((role, i) => new
            {
                Role = role, Count = supported[i], LongestRun = longest[i]
            }).ToArray()},
            {"MaximumSolvedToReopenedHandPatchPoint", maxNativeHandPatchDelta},
            {"MaximumNormalizedEarlyHandClearanceError", maxNormalizedEarlyHandClearanceError},
            {"InventedEarlyHandSupportOrLock", inventedEarlyHandSupport},
            {"MaximumNormalizedEarlyLowerArmClearanceError", maxNormalizedEarlyLowerArmClearanceError},
            {"InventedEarlyLowerArmSupportOrLock", inventedEarlyLowerArmSupport},
            {"MaximumExtraPelvisDescent", maxExtraPelvisDescent},
            {"Rows", rows},
            {"FiniteSampledCoverageOnly", true}
        };
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
