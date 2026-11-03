using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Web.Script.Serialization;

internal static partial class Program
{
    sealed class WitnessMesh
    {
        public SkinPoint[] Points;
        public int TotalVertices;
        public Rig Rig;
    }

    static WitnessMesh CaptureWholeMesh(BrawlRig rig, MDL0Node model)
    {
        if (rig.Rig.NativeEvaluation != null)
        {
            var data = rig.Rig.NativeEvaluation;
            return new WitnessMesh { Rig = rig.Rig, TotalVertices = data.Vertices.Length,
                Points = data.Vertices.Select(vertex => new SkinPoint { Native = vertex }).ToArray() };
        }
        var indices = rig.Bones.Select((bone, index) => new { bone.Name, Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.Ordinal);
        var points = new List<SkinPoint>(); int total = 0;
        foreach (var obj in model.PolygonList.OfType<MDL0ObjectNode>())
        foreach (var vertex in obj.Vertices)
        {
            total++;
            var matrix = vertex.GetMatrixNode();
            if (matrix?.Weights == null) continue;
            var bind = matrix is MDL0BoneNode primary ? primary.BindMatrix * vertex.Position : vertex.Position;
            var weights = matrix.Weights.Where(weight => weight.Bone is MDL0BoneNode && weight.Weight > 0)
                .Select(weight => new SkinWeight { Joint = indices[weight.Bone.Name], Weight = weight.Weight,
                    Local = BrawlRetargeter.Vector(weight.Bone.InverseBindMatrix * bind) }).ToArray();
            if (weights.Length > 0) points.Add(new SkinPoint { Weights = weights });
        }
        return new WitnessMesh { Rig = rig.Rig, TotalVertices = total, Points = points.ToArray() };
    }

    static Tuple<double, int> WholeMeshMinimum(WitnessMesh mesh, RigPose pose)
    {
        double minimum = double.PositiveInfinity; int vertex = -1;
        for (int i = 0; i < mesh.Points.Length; i++)
        {
            Vector3 point = mesh.Points[i].Evaluate(pose);
            if (!RigMath.Finite(point)) throw new InvalidDataException("The independent whole-mesh pose has a non-finite vertex.");
            double height = Vector3.Dot(point, mesh.Rig.GroundNormal) - mesh.Rig.GroundHeight;
            if (height < minimum) { minimum = height; vertex = i; }
        }
        if (vertex < 0) throw new InvalidDataException("No whole-mesh vertices can be evaluated.");
        return Tuple.Create(minimum, vertex);
    }

    static object CaptureMeshFloorSeries(CHR0Node clip, int totalVertices, int evaluatedVertices,
        Func<float, Tuple<double, int>> minimumAt)
    {
        int last = clip.Loop ? clip.FrameCount : clip.FrameCount - 1;
        // Ordinary clips cover every integer. Long clips keep an explicit,
        // deterministic 512-frame diagnostic cap rather than implying full coverage.
        int[] integers = last < 512 ? Enumerable.Range(0, last + 1).ToArray() :
            Enumerable.Range(0, 128).Concat(Enumerable.Range(last - 127, 128))
                .Concat(Enumerable.Range(0, 256).Select(i => (int)Math.Round(i * last / 255d)))
                .Distinct().OrderBy(i => i).ToArray();
        var samples = new List<Tuple<float, double, int>>();
        foreach (int frame in integers)
        {
            var minimum = minimumAt(frame);
            samples.Add(Tuple.Create((float)frame, minimum.Item1, minimum.Item2));
        }
        var lowest = samples.OrderBy(sample => sample.Item2).Take(4).ToArray();
        var refinement = new SortedSet<float>();
        foreach (var minimum in lowest)
            foreach (float offset in new[] { -.75f, -.5f, -.25f, -.137f, .137f, .25f, .371f, .5f, .623f, .75f })
            {
                float time = minimum.Item1 + offset;
                if (time >= 0 && time <= last && time != (int)time) refinement.Add(time);
            }
        foreach (float time in refinement)
        {
            var minimum = minimumAt(time);
            samples.Add(Tuple.Create(time, minimum.Item1, minimum.Item2));
        }
        var worst = samples.OrderBy(sample => sample.Item2).First();
        return new { FloorReference = "captured rig ground normal and height", TotalVertices = totalVertices,
            EvaluatedVertices = evaluatedVertices, AllVerticesCovered = totalVertices == evaluatedVertices,
            IntegerFrames = last + 1, IntegerSamples = integers.Length, AllIntegerFramesCovered = integers.Length == last + 1,
            RefinedIntegerMinima = lowest.Select(sample => new { Time = sample.Item1, Height = sample.Item2, Vertex = sample.Item3 }).ToArray(),
            FractionalRefinementSamples = refinement.Count, TotalSampledTimes = samples.Count,
            MinimumSignedHeight = worst.Item2, WorstTime = worst.Item1, WorstVertex = worst.Item3,
            FiniteSampledCoverageOnly = true };
    }

    static object CapturePoseMeshFloor(BrawlRig rig, MDL0Node model, CHR0Node clip, Func<float, RigPose> poseAt)
    {
        WitnessMesh mesh = CaptureWholeMesh(rig, model);
        return CaptureMeshFloorSeries(clip, mesh.TotalVertices, mesh.Points.Length,
            time => WholeMeshMinimum(mesh, poseAt(time)));
    }

    static object CaptureNativeMeshFloor(BrawlRig rig, MDL0Node model, CHR0Node clip)
    {
        try
        {
            int count = model.PolygonList.OfType<MDL0ObjectNode>().Sum(obj => obj.Vertices.Count);
            return CaptureMeshFloorSeries(clip, count, count, time =>
            {
                model.ApplyCHR(clip, time + 1);
                Vector3[] vertices = Vertices(model);
                double minimum = double.PositiveInfinity; int worst = -1;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (!RigMath.Finite(vertices[i])) throw new InvalidDataException("The native whole mesh has a non-finite vertex.");
                    double height = Vector3.Dot(vertices[i], rig.Rig.GroundNormal) - rig.Rig.GroundHeight;
                    if (height < minimum) { minimum = height; worst = i; }
                }
                return Tuple.Create(minimum, worst);
            });
        }
        finally { model.ApplyCHR(null, 0); }
    }

    // Diagnostic evidence only. These bounded, private receipts do not change
    // admission, solving, curve fitting, or the native acceptance thresholds.
    static float[] TransferWitnessTimes(CHR0Node clip)
    {
        int last = clip.Loop ? clip.FrameCount : clip.FrameCount - 1;
        var times = new SortedSet<float> { 0, last, last / 2f };
        foreach (int frame in new[] { 0, last / 2, Math.Max(0, last - 1) }.Distinct())
            foreach (float fraction in new[] { .137f, .371f, .623f })
                if (frame + fraction < last) times.Add(frame + fraction);
        return times.Take(12).ToArray();
    }

    static void WriteTransferWitness(string output, IDictionary<string, object> witness)
        => File.WriteAllText(Path.Combine(output, "stage-witness.json"),
            new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024, RecursionLimit = 80 }.Serialize(witness));

    static string WitnessError(Exception error)
    {
        string message = error.GetType().Name + ": " + error.Message;
        return message.Length <= 2048 ? message : message.Substring(0, 2048);
    }

    static object CaptureTransferPreflight(BrawlRig source, BrawlRig target, MDL0Node sourceModel, MDL0Node targetModel, CHR0Node clip, float[] times)
    {
        var plan = new HumanoidRetargeter(source.Rig, target.Rig);
        var samples = new List<object>();
        double maximumSourceNative = 0; float sourceNativeTime = 0; string sourceNativeBone = null;
        try
        {
            foreach (float time in times)
            {
                try
                {
                    var sourcePose = BrawlRetargeter.Sample(source, clip, time);
                    sourceModel.ApplyCHR(clip, time + 1);
                    double native = 0; string nativeBone = null;
                    for (int bone = 0; bone < source.Bones.Length; bone++)
                    {
                        double distance = Vector3.Distance(sourcePose.Point(bone), BrawlRetargeter.Vector(source.Bones[bone].Matrix.GetPoint()));
                        if (distance > native) { native = distance; nativeBone = source.Bones[bone].Name; }
                    }
                    if (native > maximumSourceNative) { maximumSourceNative = native; sourceNativeTime = time; sourceNativeBone = nativeBone; }
                    RetargetPose solved = plan.Evaluate(sourcePose);
                    var roles = new[] { "pelvis", "head", "left.hand", "right.hand", "left.foot", "right.foot" }
                        .Where(role => source.Rig.Roles.ContainsKey(role) && target.Rig.Roles.ContainsKey(role))
                        .Select(role => new { Role = role, Source = sourcePose.Point(role), Target = solved.Pose.Point(role) }).ToArray();
                    samples.Add(new { Time = time, SourceNativeMaximumBonePosition = native, SourceNativeWorstBone = nativeBone,
                        SolvedMaximumLimbResidual = solved.MaximumLimbResidual, solved.ClampedLimbs,
                        SolvedMaximumContactResidual = solved.MaximumContactResidual, solved.PelvisCorrection,
                        MappedRolePoints = roles,
                        SourceControls = plan.SourceMotionControls.Select(i => new { Bone = source.Bones[i].Name, Local = sourcePose.Local[i] }).ToArray(),
                        TargetControls = plan.TargetMotionControls.Select(i => new { Bone = target.Bones[i].Name, Local = solved.Pose.Local[i] }).ToArray() });
                }
                catch (Exception error) { samples.Add(new { Time = time, Failure = WitnessError(error) }); }
            }
        }
        finally { sourceModel.ApplyCHR(null, 0); }
        object sourceMeshFloor, rawTargetMeshFloor;
        try { sourceMeshFloor = CapturePoseMeshFloor(source, sourceModel, clip, time => BrawlRetargeter.Sample(source, clip, time)); }
        catch (Exception error) { sourceMeshFloor = new { Failure = WitnessError(error) }; }
        try { rawTargetMeshFloor = CapturePoseMeshFloor(target, targetModel, clip,
            time => plan.Evaluate(BrawlRetargeter.Sample(source, clip, time)).Pose); }
        catch (Exception error) { rawTargetMeshFloor = new { Failure = WitnessError(error) }; }
        object Characterization(MDL0Node model, BrawlRig rig)
        {
            var reviewed = BrawlRetargeter.Characterize(model);
            return new { RoleCount = reviewed.Roles.Count,
                Roles = reviewed.Roles.OrderBy(pair => pair.Key, StringComparer.Ordinal).Take(128)
                    .Select(pair => new { Role = pair.Key, Bone = rig.Bones[pair.Value].Name,
                        Evidence = reviewed.Evidence.TryGetValue(pair.Key, out string evidence) ? evidence : null }).ToArray(),
                RolesTruncated = reviewed.Roles.Count > 128, Warnings = reviewed.Warnings.Take(32).ToArray(),
                WarningsTruncated = reviewed.Warnings.Count > 32, reviewed.Missing };
        }
        object sourceReview, targetReview;
        try { sourceReview = Characterization(sourceModel, source); }
        catch (Exception error) { sourceReview = new { Failure = WitnessError(error) }; }
        try { targetReview = Characterization(targetModel, target); }
        catch (Exception error) { targetReview = new { Failure = WitnessError(error) }; }
        return new
        {
            MappingCount = plan.Mapping.Length,
            Mapping = plan.Mapping.Take(128).Select(map => new { map.Role, map.Source, map.Target, map.SourceIndex, map.TargetIndex,
                SourceParent = source.Rig.Joints[map.SourceIndex].Parent,
                TargetParent = target.Rig.Joints[map.TargetIndex].Parent }).ToArray(),
            MappingTruncated = plan.Mapping.Length > 128,
            SourceJointCount = source.Bones.Length, TargetJointCount = target.Bones.Length,
            SourceUnmappedCount = plan.UnmappedSource.Length, TargetUnmappedCount = plan.UnmappedTarget.Length,
            SourceUnmapped = plan.UnmappedSource.Take(64).ToArray(), TargetUnmapped = plan.UnmappedTarget.Take(64).ToArray(),
            plan.UnavailableEndEffectors, plan.TopologyAdaptations, plan.CalibrationEvidence, plan.Calibration,
            plan.LegScale, plan.ArmScale, plan.HeadScale,
            SourceNativeMaximumBonePosition = maximumSourceNative, SourceNativeWorstTime = sourceNativeTime,
            SourceNativeWorstBone = sourceNativeBone, Samples = samples,
            SourceMeshFloor = sourceMeshFloor, RawSolverTargetMeshFloor = rawTargetMeshFloor,
            SourceRoleEvidence = sourceReview, TargetRoleEvidence = targetReview
        };
    }

    static object CaptureTransferContacts(BrawlRig source, BrawlRig target, MDL0Node targetModel, CHR0Node clip, RetargetResult result, float[] times)
    {
        if (result.Contacts == null) return new { Available = false, Reason = "No contact result was returned." };
        var samples = new List<object>();
        double maximumCorrection = 0; float correctionTime = 0; string correctionBone = null;
        foreach (float time in times)
        {
            try
            {
                RigPose sourcePose = BrawlRetargeter.Sample(source, clip, time);
                var raw = result.Plan.Evaluate(sourcePose).Pose;
                var corrected = result.Contacts.Evaluate(time).Pose;
                double maximum = 0; string boneName = null;
                for (int bone = 0; bone < target.Bones.Length; bone++)
                {
                    double distance = Vector3.Distance(raw.Point(bone), corrected.Point(bone));
                    if (distance > maximum) { maximum = distance; boneName = target.Bones[bone].Name; }
                }
                if (maximum > maximumCorrection) { maximumCorrection = maximum; correctionTime = time; correctionBone = boneName; }
                var supports = target.Rig.Surfaces.Keys.OrderBy(role => role, StringComparer.Ordinal).Take(24)
                    .Select(role => { var goal = result.Contacts.At(role, time); var surface = target.Rig.Surfaces[role];
                        return new { Role = role, goal.Support, goal.Lock, goal.Anchor,
                            RawClearance = surface.Clearance(raw), CorrectedClearance = surface.Clearance(corrected),
                            RawPoint = raw.Point(role), CorrectedPoint = corrected.Point(role) }; }).ToArray();
                samples.Add(new { Time = time, MaximumBodyCorrection = maximum, WorstBone = boneName, Supports = supports });
            }
            catch (Exception error) { samples.Add(new { Time = time, Failure = WitnessError(error) }); }
        }
        object correctedMeshFloor;
        try { correctedMeshFloor = CapturePoseMeshFloor(target, targetModel, clip,
            time => result.Contacts.Evaluate(time).Pose); }
        catch (Exception error) { correctedMeshFloor = new { Failure = WitnessError(error) }; }
        return new { Available = true, MaximumBodyCorrection = maximumCorrection, WorstTime = correctionTime,
            WorstBone = correctionBone, result.MaximumContactResidual, result.MaximumLimbResidual,
            result.MaximumPelvisCorrection, result.ClampedLimbSamples, Samples = samples,
            CorrectedTargetMeshFloor = correctedMeshFloor };
    }

    // Both clips are evaluated on the SAME finished target model. The donor's
    // different proportions are never a compression or serialization baseline.
    static object CompareTransferNative(BrawlRig target, MDL0Node targetModel, CHR0Node master,
        CHR0Node candidate, float[] times, ContactMotion contacts = null)
    {
        double localTranslation = 0, localRotation = 0, localScale = 0, worldBone = 0,
            skin = 0, contactPoint = 0, contactClearance = 0, solverWorld = 0;
        float worstSkinTime = 0, worstBoneTime = 0; int worstVertex = -1; string worstBone = null;
        object masterFloor = null, candidateFloor = null;
        try
        {
            foreach (float time in times)
            {
                RigPose expected = BrawlRetargeter.Sample(target, master, time);
                RigPose actual = BrawlRetargeter.Sample(target, candidate, time);
                RigPose solved = contacts == null ? null : contacts.Evaluate(time).Pose;
                for (int bone = 0; bone < target.Bones.Length; bone++)
                {
                    localTranslation = Math.Max(localTranslation, Vector3.Distance(expected.Local[bone].Translation, actual.Local[bone].Translation));
                    localRotation = Math.Max(localRotation, SpanAngle(expected.Local[bone].Rotation, actual.Local[bone].Rotation));
                    localScale = Math.Max(localScale, Vector3.Distance(expected.Local[bone].Scale, actual.Local[bone].Scale));
                    double position = Vector3.Distance(expected.Point(bone), actual.Point(bone));
                    if (position > worldBone) { worldBone = position; worstBone = target.Bones[bone].Name; worstBoneTime = time; }
                    if (solved != null) solverWorld = Math.Max(solverWorld,
                        Vector3.Distance(solved.Point(bone), expected.Point(bone)));
                }
                foreach (string role in target.Rig.Surfaces.Keys)
                {
                    var surface = target.Rig.Surfaces[role];
                    var a = surface.Sample(expected); var b = surface.Sample(actual);
                    contactPoint = Math.Max(contactPoint, Vector3.Distance(a.Point, b.Point));
                    contactClearance = Math.Max(contactClearance, Math.Abs(a.Clearance - b.Clearance));
                }
                targetModel.ApplyCHR(master, time + 1);
                Vector3[] original = Vertices(targetModel);
                targetModel.ApplyCHR(candidate, time + 1);
                Vector3[] decoded = Vertices(targetModel);
                if (original.Length != decoded.Length) throw new InvalidDataException("Target vertex inventory changed between master and candidate.");
                for (int vertex = 0; vertex < original.Length; vertex++)
                {
                    double distance = Vector3.Distance(original[vertex], decoded[vertex]);
                    if (distance > skin) { skin = distance; worstSkinTime = time; worstVertex = vertex; }
                }
            }
            masterFloor = CaptureNativeMeshFloor(target, targetModel, master);
            candidateFloor = CaptureNativeMeshFloor(target, targetModel, candidate);
        }
        finally { targetModel.ApplyCHR(null, 0); }
        return new { Reference = "same finished target master", SampledTimes = times.Length,
            MaximumLocalTranslation = localTranslation, MaximumLocalRotationDegrees = localRotation,
            MaximumLocalScale = localScale, MaximumWorldBonePosition = worldBone, WorstBone = worstBone,
            WorstBoneTime = worstBoneTime, MaximumSkinnedVertexDisplacement = skin,
            WorstVertex = worstVertex, WorstVertexTime = worstSkinTime,
            MaximumContactPatchPoint = contactPoint, MaximumContactClearance = contactClearance,
            MaximumSolverToMasterWorldBone = solverWorld, MasterMeshFloor = masterFloor,
            CandidateMeshFloor = candidateFloor, FiniteSamplesOnly = true };
    }
}
