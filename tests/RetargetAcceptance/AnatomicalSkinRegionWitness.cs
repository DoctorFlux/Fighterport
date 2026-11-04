using BrawlCrate.Studio.Retargeting;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

// Read-only fixture diagnostic. These regions do not enter Rig.Surfaces or
// contact inference; a low point alone is not an authored floor contact.
internal static partial class Program
{
    private sealed class AnatomicalRegions
    {
        internal string[] Region;
        internal string[] DominantBone;
        internal string[] NativeObject;
        internal bool[] Accessory;
        internal bool[] DirectHeadSkin;
        internal Vertex3[] Vertices;
    }

    private static AnatomicalRegions PartitionNativeSkin(BrawlRig captured, MDL0Node model,
        IReadOnlyCollection<int> reviewedSourceCranial)
    {
        Rig rig = captured.Rig;
        MDL0ObjectNode[] objects = model.PolygonList.OfType<MDL0ObjectNode>().ToArray();
        Vertex3[] vertices = objects.SelectMany(obj => obj.Vertices).ToArray();
        if (vertices.Length == 0 || vertices.Length > 250000)
            throw new InvalidDataException("The read-only region witness requires a bounded native mesh.");
        var names = captured.Bones.Select((bone, index) => new {bone.Name, index})
            .ToDictionary(item => item.Name, item => item.index, StringComparer.Ordinal);
        var cranial = reviewedSourceCranial == null ? new HashSet<int>() :
            new HashSet<int>(reviewedSourceCranial);
        var result = new AnatomicalRegions {
            Region = new string[vertices.Length], DominantBone = new string[vertices.Length],
            NativeObject = objects.SelectMany(obj => obj.Vertices.Select(vertex => obj.Name)).ToArray(),
            Accessory = new bool[vertices.Length], DirectHeadSkin = new bool[vertices.Length],
            Vertices = vertices
        };
        string[] anatomical = {"pelvis", "spine", "chest", "neck", "head",
            "left.shoulder", "right.shoulder", "left.upperArm", "right.upperArm",
            "left.lowerArm", "right.lowerArm", "left.upperLeg", "right.upperLeg",
            "left.lowerLeg", "right.lowerLeg"};
        var anatomicalSet = new HashSet<string>(anatomical, StringComparer.Ordinal);
        // Match the current capture's nearest mapped ancestor rule. Keep the
        // individual groups here rather than collapsing four trunk groups.
        string[] nearest = new string[captured.Bones.Length];
        for (int i = 0; i < nearest.Length; i++)
        {
            int joint = i;
            while (joint >= 0 && nearest[i] == null)
            {
                nearest[i] = rig.Roles.Keys.FirstOrDefault(role => rig.Roles[role] == joint);
                joint = rig.Joints[joint].Parent;
            }
            nearest[i] = nearest[i] ?? "";
        }
        for (int i = 0; i < vertices.Length; i++)
        {
            var weights = vertices[i].GetMatrixNode()?.Weights;
            if (weights == null || weights.Count == 0)
            {
                result.Region[i] = "unrepresented";
                continue;
            }
            var positive = weights.Where(weight => weight.Bone is MDL0BoneNode && weight.Weight > 0)
                .Select(weight => new {Bone = names[weight.Bone.Name], weight.Weight})
                .OrderByDescending(weight => weight.Weight).ToArray();
            if (positive.Length == 0) { result.Region[i] = "unrepresented"; continue; }
            result.DominantBone[i] = captured.Bones[positive[0].Bone].Name;
            result.Accessory[i] = positive.Any(weight => weight.Weight > .01f &&
                BrawlRetargeter.IsAccessoryBranch(rig, weight.Bone));
            result.DirectHeadSkin[i] = rig.Roles.TryGetValue("head", out int head) &&
                positive.Where(weight => weight.Bone == head).Sum(weight => weight.Weight) >= .5f;
            if (cranial.Contains(i)) { result.Region[i] = "reviewed-cranial-skin"; continue; }
            if (result.Accessory[i]) { result.Region[i] = "unmapped-cranial-accessory"; continue; }
            var majority = positive.GroupBy(weight => nearest[weight.Bone])
                .Select(group => new {Role = group.Key, Weight = group.Sum(item => item.Weight)})
                .OrderByDescending(group => group.Weight).FirstOrDefault();
            result.Region[i] = majority != null && majority.Weight >= .35f &&
                anatomicalSet.Contains(majority.Role) ? majority.Role : "other";
        }
        return result;
    }

    private static object NativeSkinRegionWitness(BrawlRig source, MDL0Node sourceModel,
        CHR0Node sourceClip, IReadOnlyCollection<int> sourceCranialIndices,
        BrawlRig target, MDL0Node targetModel, CHR0Node reopened,
        HumanoidRetargeter plan)
    {
        AnatomicalRegions sourceRegions = PartitionNativeSkin(source, sourceModel, sourceCranialIndices);
        AnatomicalRegions targetRegions = PartitionNativeSkin(target, targetModel, null);
        string[] trunkRoles = {"pelvis", "spine", "chest", "neck"};
        int TrunkCount(AnatomicalRegions regions) =>
            regions.Region.Count(role => trunkRoles.Contains(role, StringComparer.Ordinal));
        int targetHeadCount = targetRegions.Region.Count(role => role == "head");
        if (!target.Rig.Surfaces.TryGetValue("head", out ContactSurface targetHead) ||
            targetHead.Points.Length != targetHeadCount ||
            source.Rig.Surfaces["pelvis"].Points.Length != TrunkCount(sourceRegions) ||
            target.Rig.Surfaces["pelvis"].Points.Length != TrunkCount(targetRegions))
            throw new InvalidDataException("The read-only native skin partition differs from captured body surfaces.");
        if (sourceRegions.Vertices.Length != Vertices(sourceModel).Length ||
            targetRegions.Vertices.Length != Vertices(targetModel).Length)
            throw new InvalidDataException("Native region and evaluated vertex order disagree.");
        string[] regions = {"pelvis", "spine", "chest", "neck", "head",
            "reviewed-cranial-skin", "unmapped-cranial-accessory", "other"};
        var times = new[] {5f, 8f, 11f};
        object Measure(BrawlRig rig, AnatomicalRegions partition, Vector3[] bind, Vector3[] points)
        {
            double Height(Vector3 point) =>
                (double)Vector3.Dot(point, rig.Rig.GroundNormal) - rig.Rig.GroundHeight;
            object Lowest(int[] indices)
            {
                if (indices.Length == 0) return null;
                int index = indices.OrderBy(i => Height(points[i])).First();
                return new {
                    Index = index,
                    NativeObject = partition.NativeObject[index],
                    DominantBone = partition.DominantBone[index],
                    BindSignedHeight = Height(bind[index]),
                    PosedSignedHeight = Height(points[index]),
                    AccessoryExcludedFromBodySurface = partition.Accessory[index]
                };
            }
            var entries = regions.Select(region =>
            {
                int[] indices = Enumerable.Range(0, points.Length)
                    .Where(i => partition.Region[i] == region).ToArray();
                return new {
                    Region = region, Count = indices.Length,
                    MinimumSignedHeight = indices.Length == 0 ? (double?)null : indices.Min(i => Height(points[i])),
                    OnePercentileSignedHeight = indices.Length == 0 ? (double?)null :
                        indices.Select(i => Height(points[i])).OrderBy(height => height)
                            .ElementAt(Math.Min(indices.Length - 1, indices.Length / 100)),
                    LowestPoint = Lowest(indices)
                };
            }).ToArray();
            int[] directHead = Enumerable.Range(0, points.Length)
                .Where(i => partition.DirectHeadSkin[i] && !partition.Accessory[i]).ToArray();
            return new {
                Regions = entries,
                DirectMappedHeadSkinCount = directHead.Length,
                DirectMappedHeadSkinOnePercentileSignedHeight = directHead.Length == 0 ? (double?)null :
                    directHead.Select(i => Height(points[i])).OrderBy(height => height)
                        .ElementAt(Math.Min(directHead.Length - 1, directHead.Length / 100)),
                DirectMappedHeadSkinLowestPoint = Lowest(directHead),
                WholeMeshLowestPoint = Lowest(Enumerable.Range(0, points.Length).ToArray())
            };
        }
        try
        {
            sourceModel.ApplyCHR(null, 0);
            targetModel.ApplyCHR(null, 0);
            Vector3[] sourceBindPoints = Vertices(sourceModel);
            Vector3[] targetBindPoints = Vertices(targetModel);
            object sourceBind = Measure(source, sourceRegions, sourceBindPoints, sourceBindPoints);
            object targetBind = Measure(target, targetRegions, targetBindPoints, targetBindPoints);
            var rows = new List<object>();
            foreach (float time in times)
            {
                sourceModel.ApplyCHR(sourceClip, time + 1);
                targetModel.ApplyCHR(reopened, time + 1);
                Vector3[] sourcePoints = Vertices(sourceModel);
                Vector3[] targetPoints = Vertices(targetModel);
                int lowest = Enumerable.Range(0, targetPoints.Length)
                    .OrderBy(i => Vector3.Dot(targetPoints[i], target.Rig.GroundNormal)).First();
                rows.Add(new {
                    Time = time,
                    Source = Measure(source, sourceRegions, sourceBindPoints, sourcePoints),
                    Target = Measure(target, targetRegions, targetBindPoints, targetPoints),
                    TargetLowestVertex = lowest,
                    TargetLowestRegion = targetRegions.Region[lowest],
                    TargetLowestDominantNativeBone = targetRegions.DominantBone[lowest],
                    TargetLowestIsAccessory = targetRegions.Accessory[lowest],
                    TargetLowestDirectMappedHeadSkin = targetRegions.DirectHeadSkin[lowest]
                });
            }
            return new {
                Scope = "read-only native skinned-vertex regions; no new contact roles or floor claims",
                SourceVertexCount = sourceRegions.Vertices.Length,
                TargetVertexCount = targetRegions.Vertices.Length,
                SourceReviewedCranialCount = sourceCranialIndices?.Count ?? 0,
                TargetHeadRegionMatchesProductionSurface = true,
                TrunkRegionsMatchProductionAggregates = true,
                LegScale = plan.LegScale,
                SourceBind = sourceBind, TargetBind = targetBind,
                Rows = rows,
                ExploratoryMappedTrunkRotation = MappedTrunkRotationWitness(
                    target, targetModel, reopened, plan, targetRegions),
                FiniteSampledCoverageOnly = true
            };
        }
        finally
        {
            sourceModel.ApplyCHR(null, 0);
            targetModel.ApplyCHR(null, 0);
        }
    }

    // Feasibility probe, not a solve or contact acceptance. Only one mapped
    // trunk joint rotates at a time. The world guide is measured, never used
    // as an inferred gameplay floor or an automatic objective.
    private static object MappedTrunkRotationWitness(BrawlRig target, MDL0Node model,
        CHR0Node reopened, HumanoidRetargeter plan, AnatomicalRegions regions)
    {
        const float time = 5;
        Rig rig = target.Rig;
        RigPose original = BrawlRetargeter.Sample(target, reopened, time);
        model.ApplyCHR(reopened, time + 1);
        Vector3[] native = Vertices(model);
        WitnessMesh mesh = CaptureWholeMesh(target, model);
        if (mesh.Points.Length != native.Length)
            throw new InvalidDataException("The complete native skin is unavailable for the rotation probe.");
        int hatIndex = Enumerable.Range(0, native.Length)
            .OrderBy(i => Vector3.Dot(native[i], rig.GroundNormal)).First();
        int[] headIndices = Enumerable.Range(0, native.Length)
            .Where(i => regions.DirectHeadSkin[i] && !regions.Accessory[i]).ToArray();
        if (headIndices.Length == 0)
            throw new InvalidDataException("The mapped native head skin is unavailable.");
        double Height(Vector3 point) => Vector3.Dot(point, rig.GroundNormal) - rig.GroundHeight;
        double headNative = headIndices.Min(i => Height(native[i]));
        double headRig = headIndices.Min(i => Height(mesh.Points[i].Evaluate(original)));
        double hatNative = Height(native[hatIndex]);
        double hatRig = Height(mesh.Points[hatIndex].Evaluate(original));
        string[] hands = {"left.hand", "right.hand"};
        Vector3[] originalHands = hands.Select(role => rig.Surfaces[role].Sample(original).Point).ToArray();
        Quaternion frame = RigMath.BodyFrame(rig);
        var axes = new[] {
            new {Name = "body-right", Vector = Vector3.Transform(Vector3.UnitX, frame)},
            new {Name = "body-up", Vector = Vector3.Transform(Vector3.UnitY, frame)},
            new {Name = "body-forward", Vector = Vector3.Transform(Vector3.UnitZ, frame)}
        };
        var candidates = new List<object>();
        foreach (string role in new[] {"spine", "chest", "neck"})
        {
            if (!rig.Roles.TryGetValue(role, out int joint) ||
                !plan.Mapping.Any(map => map.TargetIndex == joint)) continue;
            foreach (var axis in axes)
            {
                double bestMinimum = double.NegativeInfinity;
                object best = null;
                for (int degrees = -45; degrees <= 45; degrees += 5)
                {
                    RigPose changed = new RigPose(rig, original.Local);
                    Quaternion rotation = Quaternion.CreateFromAxisAngle(axis.Vector,
                        degrees * (float)Math.PI / 180f) * original.Orientation[joint];
                    changed.SetWorldRotation(joint, rotation);
                    double hatHeight = Height(mesh.Points[hatIndex].Evaluate(changed));
                    double headHeight = headIndices.Min(i => Height(mesh.Points[i].Evaluate(changed)));
                    double handShift = hands.Select((hand, index) =>
                        Vector3.Distance(originalHands[index], rig.Surfaces[hand].Sample(changed).Point)).Max();
                    double lowerEnvelope = Math.Min(hatHeight, headHeight);
                    if (lowerEnvelope <= bestMinimum) continue;
                    bestMinimum = lowerEnvelope;
                    best = new {
                        Role = role, Axis = axis.Name, Degrees = degrees,
                        HatSignedGuideHeight = hatHeight,
                        DirectHeadSkinMinimumSignedGuideHeight = headHeight,
                        MaximumHandPatchDisplacement = handShift
                    };
                }
                candidates.Add(best);
            }
        }
        object bestTwoAxisNeck = null;
        object firstTwoAxisGuideClearance = null;
        if (rig.Roles.TryGetValue("neck", out int neckJoint) &&
            plan.Mapping.Any(map => map.TargetIndex == neckJoint))
        {
            double bestMinimum = double.NegativeInfinity;
            double smallestClearingAngle = double.PositiveInfinity;
            foreach (int rightDegrees in Enumerable.Range(-3, 7).Select(i => i * 15))
            foreach (int forwardDegrees in Enumerable.Range(-3, 7).Select(i => i * 15))
            {
                RigPose changed = new RigPose(rig, original.Local);
                Quaternion rotation = Quaternion.CreateFromAxisAngle(axes[0].Vector,
                    rightDegrees * (float)Math.PI / 180f) *
                    Quaternion.CreateFromAxisAngle(axes[2].Vector,
                    forwardDegrees * (float)Math.PI / 180f) * original.Orientation[neckJoint];
                changed.SetWorldRotation(neckJoint, rotation);
                double hatHeight = Height(mesh.Points[hatIndex].Evaluate(changed));
                double headHeight = headIndices.Min(i => Height(mesh.Points[i].Evaluate(changed)));
                double lowerEnvelope = Math.Min(hatHeight, headHeight);
                double angle = Math.Sqrt(rightDegrees * rightDegrees +
                    forwardDegrees * forwardDegrees);
                var observation = new {
                    RightDegrees = rightDegrees, ForwardDegrees = forwardDegrees,
                    EuclideanAxisParameterMagnitudeDegrees = angle,
                    HatSignedGuideHeight = hatHeight,
                    DirectHeadSkinMinimumSignedGuideHeight = headHeight,
                    LowerEnvelopeSignedGuideHeight = lowerEnvelope,
                    MaximumHandPatchDisplacement = hands.Select((hand, index) =>
                        Vector3.Distance(originalHands[index],
                            rig.Surfaces[hand].Sample(changed).Point)).Max()
                };
                if (lowerEnvelope > bestMinimum)
                {
                    bestMinimum = lowerEnvelope;
                    bestTwoAxisNeck = observation;
                }
                if (lowerEnvelope >= 0 && angle < smallestClearingAngle)
                {
                    smallestClearingAngle = angle;
                    firstTwoAxisGuideClearance = observation;
                }
            }
        }
        return new {
            Time = time,
            Scope = "single mapped-joint rotation sweeps, native skin evaluator, exploratory ±45 degrees in 5-degree steps; no authored orientation or contact approval",
            HatVertex = hatIndex,
            HatNativeSignedGuideHeight = hatNative,
            HatPoseEvaluatorSignedGuideHeight = hatRig,
            DirectHeadSkinNativeMinimumSignedGuideHeight = headNative,
            DirectHeadSkinPoseEvaluatorMinimumSignedGuideHeight = headRig,
            MappedRotationCandidates = candidates,
            BestTwoAxisMappedNeckWithin45 = bestTwoAxisNeck,
            SmallestGridAngleWithHatAndHeadAboveGuide = firstTwoAxisGuideClearance,
            NoCandidateAppliedToNativeDocument = true,
            FiniteSamplesOnly = true
        };
    }
}
