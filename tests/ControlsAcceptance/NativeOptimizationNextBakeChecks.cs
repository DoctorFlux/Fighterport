using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using NativeVector = BrawlLib.Internal.Vector3;

internal static partial class Program
{
    // The original Next Bake receives a real articulated, animated donor-only
    // branch, rather than an unmatched CHR0 label on a rig that lacks the bones.
    // Native helpers live exclusively on an independently loaded test clone.
    static void NativeOptimizationNextBakeChecks(string modelPath, MDL0Node targetModel, string stage)
    {
        const string firstIdentity = "q7_auxiliary_segment", secondIdentity = "q7_auxiliary_terminal";
        string sourcePath = Path.Combine(stage, "next-procedural-source-base.mdl0");
        string targetBeforePath = Path.Combine(stage, "next-target-before.mdl0"), targetAfterPath = Path.Combine(stage, "next-target-after.mdl0");
        string outputPath = Path.Combine(stage, "next-target-only.chr0");
        string originalFileHash = FighterProject.HashFile(modelPath);
        targetModel.Export(targetBeforePath); targetModel.Export(sourcePath);
        string targetFileHash = FighterProject.HashFile(targetBeforePath), targetBind = BrawlRetargeter.SkeletonFingerprint(targetModel);
        using (var sourceOwner = NodeFactory.FromFile(null, sourcePath))
        {
            var sourceModel = OptimizationWalk(sourceOwner).OfType<MDL0Node>().Single();
            var authenticSource = BrawlRetargeter.Capture(sourceModel); var target = BrawlRetargeter.Capture(targetModel);
            int parent = authenticSource.Rig.Roles["pelvis"], firstIndex = authenticSource.Bones.Length;
            var first = new MDL0BoneNode { Name = firstIdentity, BoneIndex = firstIndex,
                BindState = new FrameState(new NativeVector(1), new NativeVector(), new NativeVector(0, 0, -2)) };
            var second = new MDL0BoneNode { Name = secondIdentity, BoneIndex = firstIndex + 1,
                BindState = new FrameState(new NativeVector(1), new NativeVector(), new NativeVector(0, 0, -1)) };
            authenticSource.Bones[parent].AddChild(first); first.AddChild(second);
            first.BindMatrix = authenticSource.Bones[parent].BindMatrix * first.BindState._transform;
            first.InverseBindMatrix = first.BindState._iTransform * authenticSource.Bones[parent].InverseBindMatrix;
            second.BindMatrix = first.BindMatrix * second.BindState._transform;
            second.InverseBindMatrix = second.BindState._iTransform * first.InverseBindMatrix;
            var joints = authenticSource.Rig.Joints.Concat(new[] {
                new RigJoint(firstIdentity, parent, new JointTransform(new Vector3(0, 0, -2), Quaternion.Identity, Vector3.One)),
                new RigJoint(secondIdentity, firstIndex, new JointTransform(new Vector3(0, 0, -1), Quaternion.Identity, Vector3.One))
            }).ToArray();
            var rig = new Rig(authenticSource.Rig.Name, authenticSource.Rig.Profile, joints, authenticSource.Rig.Roles, authenticSource.Rig.HeadRadius) {
                GroundNormal = authenticSource.Rig.GroundNormal, GroundHeight = authenticSource.Rig.GroundHeight };
            foreach (var surface in authenticSource.Rig.Surfaces) rig.Surfaces.Add(surface.Key, surface.Value);
            var source = new BrawlRig { Rig = rig, Bones = authenticSource.Bones.Concat(new[] { first, second }).ToArray(),
                Fingerprint = BrawlRetargeter.SkeletonFingerprint(sourceModel) };
            using (var clip = new CHR0Node { Name = "Independent native articulated extension", FrameCount = 5 })
            {
                var mapped = OptimizationFixed(clip, authenticSource.Bones[parent].Name,
                    Enumerable.Range(0, 9).Select(c => authenticSource.Bones[parent].BindState[c]).ToArray());
                float bodyRotation = authenticSource.Bones[parent].BindState[5];
                OptimizationReplace(mapped, 5, new[] { OptimizationKey(0, bodyRotation, 0), OptimizationKey(2, bodyRotation + 3, 0), OptimizationKey(4, bodyRotation, 0) });
                var firstTrack = OptimizationFixed(clip, firstIdentity, Enumerable.Range(0, 9).Select(c => first.BindState[c]).ToArray());
                var secondTrack = OptimizationFixed(clip, secondIdentity, Enumerable.Range(0, 9).Select(c => second.BindState[c]).ToArray());
                OptimizationReplace(firstTrack, 4, new[] { OptimizationKey(0, 0, 0), OptimizationKey(2, 30, 0), OptimizationKey(4, -20, 0) });
                OptimizationReplace(secondTrack, 3, new[] { OptimizationKey(0, 0, 0), OptimizationKey(2, -25, 0), OptimizationKey(4, 45, 0) });
                string sourceContent = NativeAnimationOptimization.ContentKey(clip), sourceBind = BrawlRetargeter.SkeletonFingerprint(sourceModel);
                Check(source.Bones.Length == target.Bones.Length + 2 && source.Rig.Joints[firstIndex].Parent == parent && source.Rig.Joints[firstIndex + 1].Parent == firstIndex &&
                    sourceModel.AllBones.Contains(first) && sourceModel.AllBones.Contains(second) && !target.Rig.Joints.Any(j => j.Name == firstIdentity || j.Name == secondIdentity),
                    "Original Next: neutral donor-only serial branch exists in the actual native source and source rig, and is absent from the target");
                Check(!sourceModel.Objects.OfType<MDL0ObjectNode>().SelectMany(o => o.Vertices).Any(v => v.GetMatrixNode().Weights.Any(w => ReferenceEquals(w.Bone, first) || ReferenceEquals(w.Bone, second))),
                    "Original Next: procedural donor-only branch has no mesh weights and cannot rely on geometry eligibility to suppress copied tracks");
                var start = BrawlRetargeter.Sample(source, clip, 0); var middle = BrawlRetargeter.Sample(source, clip, 2);
                Check(Vector3.Distance(start.Point(firstIndex + 1), middle.Point(firstIndex + 1)) > .1f &&
                    RigMath.RotationError(start.Local[firstIndex].Rotation, middle.Local[firstIndex].Rotation) > .1f,
                    "Original Next: source sampling proves the real donor-only branch is animated and moves its terminal joint");
                using (var baked = BrawlRetargeter.Bake(source, target, clip, false, groundMode: GroundContactMode.Airborne))
                {
                    var targetNames = target.Bones.Select(b => b.Name).ToArray();
                    Check(baked.Animation.Children.Count > 0 && baked.Animation.Children.All(t => targetNames.Contains(t.Name)) &&
                        !baked.Animation.Children.Any(t => t.Name == firstIdentity || t.Name == secondIdentity) &&
                        baked.IgnoredSourceTracks == 2 && baked.IgnoredSourceBones.Contains(firstIdentity) && baked.IgnoredSourceBones.Contains(secondIdentity),
                        "Original Next: Bake itself emits target-native bones only and explicitly excludes both animated donor-only bones without optimization");
                    var targetStart = BrawlRetargeter.Sample(target, baked.Animation, 0); var targetMiddle = BrawlRetargeter.Sample(target, baked.Animation, 2);
                    int targetBody = target.Rig.Roles["pelvis"];
                    double bodyDegrees = RigMath.RotationError(targetStart.Local[targetBody].Rotation, targetMiddle.Local[targetBody].Rotation) * 180 / Math.PI;
                    Check(Math.Abs(bodyDegrees - 3) < .01,
                        "Original Next: target mapped body retains the independently authored three-degree motion while excluding the donor-only branch");
                    baked.Animation.Export(outputPath);
                    using (var reopened = NodeFactory.FromFile(null, outputPath) as CHR0Node)
                        Check(reopened != null && reopened.FrameCount == clip.FrameCount && reopened.Loop == clip.Loop &&
                            reopened.Children.All(t => targetNames.Contains(t.Name)) && !reopened.Children.Any(t => t.Name == firstIdentity || t.Name == secondIdentity),
                            "Original Next: independently decoded native bake retains target-only tracks and original timing without a cleanup pass");
                }
                targetModel.Export(targetAfterPath);
                Check(NativeAnimationOptimization.ContentKey(clip) == sourceContent && BrawlRetargeter.SkeletonFingerprint(sourceModel) == sourceBind &&
                    BrawlRetargeter.SkeletonFingerprint(targetModel) == targetBind && FighterProject.HashFile(targetAfterPath) == targetFileHash && FighterProject.HashFile(modelPath) == originalFileHash,
                    "Original Next: articulated donor sampling/Bake leaves source curves, test-clone binds, target model and original game file immutable");
            }
        }
    }
}
