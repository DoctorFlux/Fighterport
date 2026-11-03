using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // A procedural CHR0 on a real rig proves that identical-rig transfer does
    // not require a humanoid role track or discard authored helper channels.
    static int SameRigHelperOnly(string modelPath, string output)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Use a new or empty output directory.");
        Directory.CreateDirectory(output);
        string inputHash = FighterProject.HashFile(modelPath);
        using (var sourceFile = NodeFactory.FromFile(null, modelPath))
        using (var targetFile = NodeFactory.FromFile(null, modelPath))
        {
            MDL0Node Model(ResourceNode root) => Walk(root).OfType<MDL0Node>()
                .Where(model => model.Name.IndexOf("shadow", StringComparison.OrdinalIgnoreCase) < 0)
                .OrderByDescending(model => model.NumVertices).First();
            var sourceModel = Model(sourceFile);
            var targetModel = Model(targetFile);
            var source = BrawlRetargeter.Capture(sourceModel);
            var target = BrawlRetargeter.Capture(targetModel);
            var rolePlan = new HumanoidRetargeter(source.Rig, target.Rig);
            var mapped = rolePlan.Mapping.Select(pair => pair.SourceIndex).ToArray();
            var helpers = Enumerable.Range(0, source.Bones.Length)
                .Where(index => !mapped.Contains(index) && source.Rig.Joints[index].Parent >= 0)
                .Take(2).ToArray();
            if (helpers.Length != 2)
                throw new InvalidDataException("The model needs two structurally unmatched helper joints for this fixture.");
            var fixedBone = source.Bones[helpers[0]];
            var movingBone = source.Bones[helpers[1]];
            string sourceOwner = NativeOwnership(sourceModel), targetOwner = NativeOwnership(targetModel);
            using (var clip = new CHR0Node { Name = "Procedural helper-only motion", Version = 4, FrameCount = 6, Loop = false })
            {
                var fixedEntry = clip.CreateEntry(fixedBone.Name);
                fixedEntry.Keyframes.PreserveInterpolation = true;
                fixedEntry.UseModelScale = true;
                fixedEntry.UseModelRotate = true;
                fixedEntry.UseModelTranslate = false;
                fixedEntry.ScaleCompensateApply = fixedBone.SegScaleCompApply;
                fixedEntry.ScaleCompensateParent = fixedBone.SegScaleCompParent;
                fixedEntry.ClassicScaleOff = !fixedBone.ClassicScale;
                for (int axis = 0; axis < 3; axis++)
                    fixedEntry.SetKeyframe(6 + axis, 0, fixedBone.BindState[6 + axis] + (axis == 0 ? .125f : 0), true)._tangent = 0;

                var movingEntry = clip.CreateEntry(movingBone.Name);
                movingEntry.Keyframes.PreserveInterpolation = true;
                movingEntry.UseModelScale = true;
                movingEntry.UseModelRotate = false;
                movingEntry.UseModelTranslate = true;
                movingEntry.ScaleCompensateApply = movingBone.SegScaleCompApply;
                movingEntry.ScaleCompensateParent = movingBone.SegScaleCompParent;
                movingEntry.ClassicScaleOff = !movingBone.ClassicScale;
                for (int axis = 0; axis < 3; axis++)
                {
                    int channel = 3 + axis;
                    movingEntry.SetKeyframe(channel, 0, movingBone.BindState[channel] + (axis == 1 ? 10f : 0), true)._tangent = axis == 1 ? 1.25f : 0;
                    if (axis == 1)
                        movingEntry.SetKeyframe(channel, 5, movingBone.BindState[channel] - 7f, true)._tangent = -.5f;
                }
                string sourceContent = NativeAnimationOptimization.ContentKey(clip);
                string sourceSnapshot = NativeAnimationOptimization.SourceContentKey(clip);
                string resultPath = Path.Combine(output, "helper-only.chr0");
                float maximumWorldError = 0;
                bool reopenedVerified = false;
                using (var result = BrawlRetargeter.Bake(source, target, clip, true,
                    groundMode: GroundContactMode.Auto, keyframeMode: RetargetKeyframeMode.Dense))
                {
                    Check(result.Plan.Mapping.Length == source.Bones.Length && result.IgnoredSourceTracks == 0 &&
                        result.Animation.Children.Count == 2 && result.OptimizationReport.StartsWith("Exact native rig:", StringComparison.Ordinal),
                        "Auto mode retains both authored non-role tracks through the exact structural rig path");
                    Check(NativeAnimationOptimization.ContentKey(result.Animation) == sourceContent &&
                        rolePlan.SourceMotionControls.All(index => result.Animation.FindChild(source.Bones[index].Name, false) == null),
                        "Exact-rig preparation preserves helper flags, fixed value, animated Hermite keys and absent source controls");
                    result.Animation.ExportChecked(resultPath);
                    using (var decoded = NodeFactory.FromFile(null, resultPath) as CHR0Node)
                    {
                        Check(decoded != null && decoded.Children.Count == 2 &&
                            NativeAnimationOptimization.ContentKey(decoded) == sourceContent,
                            "Fresh native CHR0 reopen retains every authored helper entry, flag, key and tangent");
                        reopenedVerified = true;
                        foreach (float frame in new[] { 0f, .25f, 2.5f, 4.75f, 5f })
                        {
                            var original = BrawlRetargeter.Sample(source, clip, frame);
                            var reopened = BrawlRetargeter.Sample(target, decoded, frame);
                            float maximum = 0;
                            for (int bone = 0; bone < source.Bones.Length; bone++)
                                maximum = Math.Max(maximum, Vector3.Distance(original.Point(bone), reopened.Point(bone)));
                            maximumWorldError = Math.Max(maximumWorldError, maximum);
                            Check(maximum <= 1e-5f,
                                "The decoded helper-only result preserves all joint positions at fractional time " + frame +
                                " (maximum " + maximum + ")");
                        }
                    }
                }
                bool groundedRejected = false;
                try
                {
                    using (var grounded = BrawlRetargeter.Bake(source, target, clip, true,
                        groundMode: GroundContactMode.Grounded, keyframeMode: RetargetKeyframeMode.Dense))
                    {
                        Check(!grounded.OptimizationReport.StartsWith("Exact native rig:", StringComparison.Ordinal),
                            "Explicit Grounded mode must not silently use the uncorrected exact-copy path");
                    }
                }
                catch (InvalidDataException error) when (error.Message.Contains("no mapped skeletal channels"))
                {
                    groundedRejected = true;
                }
                Check(groundedRejected, "A helper-only clip requires a mapped body role for explicit Grounded correction");
                bool unchanged = NativeOwnership(sourceModel) == sourceOwner && NativeOwnership(targetModel) == targetOwner &&
                    NativeAnimationOptimization.ContentKey(clip) == sourceContent &&
                    NativeAnimationOptimization.SourceContentKey(clip) == sourceSnapshot && FighterProject.HashFile(modelPath) == inputHash;
                Check(unchanged, "Auto copy and rejected Grounded correction leave the source clip and both complete models unchanged");
                File.WriteAllText(Path.Combine(output, "helper-only.json"), new JavaScriptSerializer().Serialize(new
                {
                    InputModelSha256 = inputHash, SourceRig = source.Fingerprint, FixedBone = fixedBone.Name,
                    AnimatedBone = movingBone.Name, FixedChannel = "TX", FixedValue = fixedEntry.GetFrameValue(6, 0),
                    FixedFlags = new { fixedEntry.UseModelScale, fixedEntry.UseModelRotate, fixedEntry.UseModelTranslate },
                    AnimatedChannel = "RY", AnimatedKeys = movingEntry.KeyArrays[4].Entries.Select(key => new
                        { Time = key._index, Value = key._value, Tangent = key._tangent }).ToArray(),
                    AnimatedFlags = new { movingEntry.UseModelScale, movingEntry.UseModelRotate, movingEntry.UseModelTranslate },
                    SourceContent = sourceContent, ResultSha256 = FighterProject.HashFile(resultPath),
                    AutoCopied = true, ReopenedVerified = reopenedVerified, MaximumWorldError = maximumWorldError,
                    GroundedRejected = groundedRejected, SourceAndModelsUnchanged = unchanged, Frames = clip.FrameCount, FractionalSamples = 5
                }));
            }
        }
        return 0;
    }
}
