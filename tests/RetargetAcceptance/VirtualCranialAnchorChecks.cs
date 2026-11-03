using BrawlCrate.Studio.Retargeting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // These synthetic rigs use reviewed role indices but opaque names. The
    // source has several neck children and no separate mapped head. The
    // destination has its own genuine head joint. Neither branch names nor
    // their storage positions select the anchor or influence solver policy.
    private static Rig VirtualCranialSource(bool permuted, Vector3 point, float radius, string evidence,
        int? parentOverride = null, bool retainHeadRole = false)
    {
        Rig baseRig = CoordinateFixture("opaque-source-", 1f, 2f, 0, Quaternion.Identity,
            declared: true, permuted: permuted).Rig;
        var roles = new Dictionary<string, int>(baseRig.Roles);
        if (!retainHeadRole) roles.Remove("head");
        int neck = roles["neck"];
        var joints = baseRig.Joints.ToList();
        var branches = new[]
        {
            new Vector3(0.8f, 3.6f, -2.2f),
            new Vector3(-0.7f, 2.8f, 2.4f)
        };
        if (permuted) Array.Reverse(branches);
        foreach (Vector3 offset in branches)
            joints.Add(new RigJoint("opaque-branch-" + joints.Count, neck,
                new JointTransform(offset, Quaternion.Identity, Vector3.One)));
        var anchor = new VirtualCranialAnchor(parentOverride ?? neck, point, radius, evidence);
        return new Rig("opaque-source", "reviewed procedural cranial surface", joints, roles, radius,
            baseRig.CoordinateUp, baseRig.CoordinateFrame, baseRig.NativeEvaluation, anchor);
    }

    private static bool RejectsCranial(Action action)
    {
        try { action(); return false; }
        catch (InvalidDataException) { return true; }
        catch (ArgumentException) { return true; }
    }

    private static Rig OnePhysicalCranialJointSource(bool permuted, string evidence)
    {
        Rig original = CoordinateFixture("opaque-single-cranial-", 1f, 2f, 0,
            Quaternion.Identity, declared: true, permuted: permuted).Rig;
        int removed = original.Roles["neck"], physical = original.Roles["head"];
        int chest = original.Roles["chest"];
        int Shift(int index) => index < 0 ? index : index > removed ? index - 1 : index;
        var joints = original.Joints.Select((joint, index) => new {joint, index})
            .Where(item => item.index != removed)
            .Select(item => new RigJoint(item.joint.Name,
                Shift(item.joint.Parent == removed ? chest : item.joint.Parent), item.joint.Bind)).ToArray();
        var roles = original.Roles.Where(role => role.Key != "neck" && role.Key != "head")
            .ToDictionary(role => role.Key, role => Shift(role.Value));
        roles.Add("neck", Shift(physical));
        var anchor = new VirtualCranialAnchor(roles["neck"], new Vector3(0, .6f, .2f), 1f, evidence);
        return new Rig("opaque-single-cranial", "reviewed one-joint cranial source", joints,
            roles, 1f, original.CoordinateUp, original.CoordinateFrame, original.NativeEvaluation, anchor);
    }

    private static int VirtualCranialAnchorChecks(string output)
    {
        Directory.CreateDirectory(output);
        const string reviewed = "procedural reviewed neck-local majority-skin surface";
        var findings = new List<object>();
        foreach (bool permuted in new[] { false, true })
        {
            Rig source = VirtualCranialSource(permuted, new Vector3(0, 2, 2), 1f, reviewed);
            Rig target = CoordinateFixture("opaque-target-", 1.7f, 1f, 0, Quaternion.Identity,
                declared: true, permuted: !permuted).Rig;
            int neck = source.Roles["neck"], targetNeck = target.Roles["neck"];
            int targetHead = target.Roles["head"];
            var plan = new HumanoidRetargeter(source, target);
            Check(!source.Roles.ContainsKey("head") && source.CranialAnchor.ParentJoint == neck &&
                source.CranialAnchor.ReviewedEvidence == reviewed,
                "A reviewed neck-local skin anchor is separate from a native head joint and retained across storage order",
                new { permuted, neck });
            bool admitted = true;
            try { SkeletonCharacterizer.Validate(source.Joints, source.Roles, cranialAnchor: source.CranialAnchor); }
            catch (InvalidDataException) { admitted = false; }
            Check(admitted, "Reviewed virtual cranial anatomy passes the actual source mapping admission", new { permuted });
            Check(RejectsCranial(() => SkeletonCharacterizer.Validate(source.Joints, source.Roles)),
                "The same source without a reviewed cranial anchor still requires head review",
                new { permuted });
            Check(plan.Mapping.Count(m => m.SourceIndex == neck) == 1 &&
                plan.Mapping.Count(m => m.TargetIndex == targetNeck) == 1 &&
                plan.Mapping.Single(m => m.SourceIndex == neck).Role == "neck" &&
                !plan.Mapping.Any(m => m.Role == "head" || m.TargetIndex == targetHead),
                "Source neck motion maps once while the destination's real head stays outside direct FK mapping",
                new { permuted, sourceNeck = neck, targetNeck, targetHead });
            Check(source.Joints.Skip(source.Joints.Length - 2).All(j => j.Parent == neck) &&
                source.Joints.Skip(source.Joints.Length - 2).All(j => plan.UnmappedSource.Contains(j.Name)),
                "Opaque accessory branches remain unmapped rather than becoming the head",
                new { permuted });

            // This is a fractional-time pose: the source authored neck and arm
            // rotations are interpolated at t=.375 from 0 to 1. Neither the
            // source pose nor the independent analytic point uses target FK.
            float time = .375f;
            var local = source.Joints.Select(j => j.Bind).ToArray();
            var authoredNeck = Quaternion.Slerp(Quaternion.Identity,
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, .96f), time);
            local[neck].Rotation = authoredNeck;
            Vector3 neckDelta = new Vector3(.16f, .24f, -.11f) * time;
            local[neck].Translation += neckDelta;
            int upperArm = source.Roles["left.upperArm"];
            local[upperArm].Rotation = Quaternion.Slerp(Quaternion.Identity,
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 2.1f), time);
            var sourcePose = new RigPose(source, local);
            Vector3 expectedNeckLocal = Vector3.Transform(source.CranialAnchor.BindLocalPoint,
                authoredNeck) + local[neck].Translation;
            Vector3 expectedCranial = Vector3.Transform(expectedNeckLocal,
                sourcePose.World[source.Joints[neck].Parent]);
            Check(Vector3.Distance(sourcePose.CranialPoint(), expectedCranial) < .00002f,
                "Fractional cranial point follows the reviewed neck-local position through the native pose matrix",
                new { permuted, time, sourcePoint = sourcePose.CranialPoint(), expectedCranial });

            var uncorrected = plan.Evaluate(sourcePose, false).Pose;
            Check(RigMath.RotationError(uncorrected.Local[targetNeck].Rotation, authoredNeck) < .0002f &&
                Vector3.Distance(uncorrected.Local[targetNeck].Translation,
                    target.Joints[targetNeck].Bind.Translation + neckDelta * plan.LegScale) < .0002f &&
                Vector3.Distance(uncorrected.Local[targetHead].Translation,
                    target.Joints[targetHead].Bind.Translation) < .00002f &&
                RigMath.RotationError(uncorrected.Local[targetHead].Rotation,
                    target.Joints[targetHead].Bind.Rotation) < .0002f,
                "Neck rotation and translation transfer once; unmapped target head retains its own bind-local transform",
                new { permuted, time, plan.LegScale });

            // Bring the hand near the source cranial surface at a second
            // fractional sample so the reviewed anchor actually influences the
            // hand goal instead of merely passing an unreachable far-hand test.
            time = .875f;
            local = source.Joints.Select(j => j.Bind).ToArray();
            local[neck].Rotation = Quaternion.Slerp(Quaternion.Identity,
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, .42f), time);
            local[upperArm].Rotation = Quaternion.Slerp(Quaternion.Identity,
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 2.4f), time);
            sourcePose = new RigPose(source, local);
            RetargetPose corrected = plan.Evaluate(sourcePose, true);
            RigPose preIk = plan.Evaluate(sourcePose, false).Pose;
            int sourceHand = source.Roles["left.hand"], targetUpperArm = target.Roles["left.upperArm"];
            int targetHand = target.Roles["left.hand"];
            Vector3 sourceHead = sourcePose.CranialPoint();
            Vector3 sourceHandPoint = sourcePose.Point(sourceHand);
            float armLength = Vector3.Distance(source.Rest.Point("left.upperArm"), source.Rest.Point("left.lowerArm")) +
                Vector3.Distance(source.Rest.Point("left.lowerArm"), source.Rest.Point("left.hand"));
            float near = Vector3.Distance(sourceHandPoint, sourceHead) / armLength;
            float blend = Math.Max(0, Math.Min(1, (.95f - near) / .4f));
            Vector3 restResidual = target.Rest.Point(targetHand) - target.Rest.Point(targetUpperArm) -
                Vector3.Transform(source.Rest.Point("left.hand") - source.Rest.Point("left.upperArm"),
                    plan.Calibration) * plan.ArmScale;
            Quaternion rootDelta = Quaternion.Normalize(plan.Calibration *
                (sourcePose.Orientation[upperArm] * Quaternion.Inverse(source.Rest.Orientation[upperArm])) *
                Quaternion.Inverse(plan.Calibration));
            Vector3 baseGoal = preIk.Point(targetUpperArm) +
                Vector3.Transform(sourceHandPoint - sourcePose.Point("left.upperArm"),
                    plan.Calibration) * plan.ArmScale + Vector3.Transform(restResidual, rootDelta);
            Vector3 headGoal = preIk.CranialPoint() +
                Vector3.Transform(sourceHandPoint - sourceHead, plan.Calibration) * plan.HeadScale;
            Vector3 expectedGoal = Vector3.Lerp(baseGoal, headGoal, blend);
            Check(blend > .05f && Vector3.Distance(baseGoal, headGoal) > .001f &&
                corrected.Goals.TryGetValue("left.hand", out Vector3 actualGoal) &&
                Vector3.Distance(actualGoal, expectedGoal) < .001f,
                "Fractional close-hand goal uses reviewed source cranial surface and target's real head",
                new { permuted, time, near, blend, baseGoal, headGoal, expectedGoal,
                    actualGoal = corrected.Goals.TryGetValue("left.hand", out var observed) ? observed : Vector3.Zero });
            findings.Add(new { permuted, sourceNeck = neck, targetNeck, targetHead,
                firstFraction = .375f, handFraction = time, near, blend,
                sourceCranial = sourceHead, targetCranial = corrected.Pose.CranialPoint() });

            Check(RejectsCranial(() => VirtualCranialSource(permuted, new Vector3(0, 2, 2), 1f,
                reviewed, parentOverride: source.Roles["chest"])),
                "An anchor parent other than the reviewed neck is rejected", new { permuted });
            Check(RejectsCranial(() => VirtualCranialSource(permuted,
                new Vector3(float.NaN, 2, 2), 1f, reviewed)),
                "Nonfinite cranial anchor coordinates are rejected", new { permuted });
            Check(RejectsCranial(() => VirtualCranialSource(permuted, new Vector3(0, 2, 2),
                float.NaN, reviewed)) &&
                RejectsCranial(() => VirtualCranialSource(permuted, new Vector3(0, 2, 2),
                0, reviewed)),
                "Nonfinite and nonpositive cranial radii are rejected", new { permuted });
            Check(RejectsCranial(() => VirtualCranialSource(permuted, new Vector3(0, 2, 2),
                1f, "")),
                "Missing review evidence cannot authorize virtual cranial mapping", new { permuted });
            Check(RejectsCranial(() => VirtualCranialSource(permuted, new Vector3(0, 2, 2),
                1f, reviewed, retainHeadRole: true)),
                "Virtual cranial anchor cannot coexist with a direct source head role", new { permuted });
            int sourceAppendage = source.Joints.Length - 1;
            Check(RejectsCranial(() => new HumanoidRetargeter(source, target, null,
                new Dictionary<int, int> { { sourceAppendage, targetHead } }, "detail.exact")),
                "Unrelated source detail cannot be routed into target head behind the virtual-anchor review",
                new { permuted });

            Rig single = OnePhysicalCranialJointSource(permuted, reviewed);
            bool singleAdmitted = true;
            try { SkeletonCharacterizer.Validate(single.Joints, single.Roles, cranialAnchor: single.CranialAnchor); }
            catch (InvalidDataException) { singleAdmitted = false; }
            var singlePlan = new HumanoidRetargeter(single, target);
            Check(singleAdmitted && !single.Roles.ContainsKey("head") &&
                singlePlan.Mapping.Count(map => map.SourceIndex == single.Roles["neck"]) == 1 &&
                !singlePlan.Mapping.Any(map => map.Role == "head"),
                "A single physical cranial joint can serve semantic neck and head surface once when reviewed",
                new { permuted, physicalJoint = single.Roles["neck"] });
            Check(RejectsCranial(() => SkeletonCharacterizer.Validate(single.Joints, single.Roles)),
                "A single physical cranial joint without reviewed skin evidence remains unavailable",
                new { permuted });
        }

        var original = CoordinateFixture("opaque-real-head-", 1f, .4f, 0,
            Quaternion.Identity).Rig;
        var ordinary = new HumanoidRetargeter(original, original);
        Check(ordinary.Mapping.Any(m => m.Role == "head" &&
            m.SourceIndex == original.Roles["head"] && m.TargetIndex == original.Roles["head"]) &&
            Vector3.Distance(original.Rest.CranialPoint(), original.Rest.Point("head")) < .00002f,
            "An ordinary real-head rig retains its original Next mapping and native head point");
        var receipt = new { Passed = true, Checks, Findings = findings };
        File.WriteAllText(Path.Combine(output, "virtual-cranial-anchor.json"),
            new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 }.Serialize(receipt));
        return 0;
    }
}
