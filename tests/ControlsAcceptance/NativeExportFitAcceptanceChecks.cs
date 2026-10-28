using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Synthetic native curves and exact reference identities on an independently
    // reopened fixture model; no retarget solver, library lookup or UI input.
    static void NativeExportFitAcceptanceChecks(string modelPath, string stage)
    {
        Directory.CreateDirectory(stage);
        var json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };
        using (var owner = NodeFactory.FromFile(null, modelPath))
        {
            var model = OptimizationWalk(owner).OfType<MDL0Node>().OrderByDescending(m => m.NumVertices).First();
            string targetPath = Path.Combine(stage, "frozen-target.mdl0"); model.Export(targetPath);
            var root = model.AllBones.First(b => !(b.Parent is MDL0BoneNode));
            var body = model.AllBones.First(b => b != root && Enumerable.Range(0, 3).All(c => b.BindState[c] != 0));
            using (var source = new CHR0Node { Name = "Independent fitted member", Version = 4, FrameCount = 6 })
            using (var reference = new CHR0Node { Name = "Duplicate library display name", Version = 4, FrameCount = 1 })
            {
                var motion = source.CreateEntry(root.Name); var original = source.CreateEntry(body.Name);
                var referenceMotion = reference.CreateEntry(root.Name); var pose = reference.CreateEntry(body.Name);
                for (int c = 0; c < 9; c++)
                {
                    float rootValue = root.BindState[c], attack = body.BindState[c] + (c == 3 ? 20 : 0);
                    foreach (int time in new[] { 0, 4, 5 })
                    {
                        motion.Keyframes.SetFrameValue(c, time, rootValue + (c == 6 ? time * .125f : 0), true)._tangent = 0;
                        original.Keyframes.SetFrameValue(c, time, attack, true)._tangent = 0;
                    }
                    referenceMotion.Keyframes.SetFrameValue(c, 0, rootValue + (c == 6 ? 100 : 0), true)._tangent = 0;
                    pose.Keyframes.SetFrameValue(c, 0, body.BindState[c] + (c == 3 ? 35 : 0), true)._tangent = 0;
                }
                PreciseCHR0EntryNode.Promote(source); PreciseCHR0EntryNode.Promote(reference);
                using (var master = NativeAnimationOptimization.CopySource(source))
                {
                    var fitted = master.Children.OfType<CHR0EntryNode>().Single(e => e.Name == body.Name);
                    for (int c = 0; c < 9; c++) fitted.KeyArrays[c].Entries.Last()._value = ((CHR0EntryNode)reference.Children[1]).GetFrameValue(c, 0);
                    string proofPath = Path.Combine(stage, "synthetic-fitted-master.chr0"); master.ExportChecked(proofPath);
                    TransitionNativeInput Freeze(CHR0Node clip, string role, string identity)
                    {
                        string path = Path.Combine(stage, "synthetic-" + role + ".chr0"); clip.ExportChecked(path);
                        return new TransitionNativeInput { Role = role, Name = clip.Name, Samples = clip.FrameCount, AssetId = "catalogue-" + role,
                            Entry = identity, Sha256 = FighterProject.HashFile(path), Snapshot = Convert.ToBase64String(File.ReadAllBytes(path)) };
                    }
                    var options = new TransitionFitOptions { Name = master.Name, Exit = "catalogue-B/exact-native-member-2", ExitFrame = 0,
                        ExitFrozen = true, ExitRate = 1, ExitSamples = 2, Components = 7, Bones = new[] { body.Name }, Protected = new TransitionProtected[0],
                        BindingReviewed = true, PreserveRootTranslation = true, NearestEquivalentAngles = true };
                    var recipe = new NativeTransitionFit { Algorithm = "fixed-duration-fk-hermite-2", ModelSha256 = FighterProject.HashFile(targetPath),
                        InitialResultSha256 = FighterProject.HashFile(proofPath), Options = json.Serialize(options),
                        Receipt = json.Serialize(new { Receipt = new { Version = "fixed-duration-fk-hermite-2", Settings = new { OptimizeRotationPaths = false, ExistingTracksOnly = false, MatchReferenceControls = false }, ProtectedContactBones = new string[0] } }),
                        Inputs = new[] { Freeze(source, "source", "owned-native-member-1"), Freeze(reference, "exit", options.Exit) } };
                    string masterBefore = NativeAnimationOptimization.SourceContentKey(master), sourceBefore = NativeAnimationOptimization.SourceContentKey(source);
                    NativeTransitionFit Copy() => json.Deserialize<NativeTransitionFit>(json.Serialize(recipe));
                    NativeExportFitAcceptance Prepare(NativeTransitionFit fit, bool known = true, bool empty = true) => NativeExportFitAcceptance.Prepare(targetPath, master,
                        fit, new[] { root.Name }, root.Name, known, empty, Path.Combine(stage, "acceptance"), CancellationToken.None);
                    using (var accepted = Prepare(recipe))
                    {
                        File.WriteAllText(Path.Combine(stage, "initial-boundary-admission.json"), json.Serialize(new { accepted.Eligible, accepted.Reason, accepted.Report, accepted.MaximumInitialRatio }));
                        Check(accepted.Eligible && accepted.MaximumInitialRatio == 0 && accepted.CheckCandidate(master, CancellationToken.None, out _),
                            "Plain-fit export: exact frozen first-pose body endpoint under original moving roots qualifies with complete native boundary headroom. " + accepted.Reason);
                        using (var unchanged = NativeAnimationOptimization.CopySource(master))
                            Check(accepted.CheckCandidate(unchanged, CancellationToken.None, out _),
                                "Plain-fit export: independently owned unchanged native candidate preserves every boundary/join local, world and skin output");
                        using (var changed = NativeAnimationOptimization.CopySource(master))
                        {
                            changed.Children.OfType<CHR0EntryNode>().Single(e => e.Name == body.Name).KeyArrays[3].Entries.Last()._value += .001f;
                            Check(!accepted.CheckCandidate(changed, CancellationToken.None, out string reason) && reason.Contains("pose changed"),
                                "Plain-fit export: one bounded-size rotation-channel perturbation at the endpoint is rejected by the exact absolute boundary gate");
                        }
                        using (var changed = NativeAnimationOptimization.CopySource(master))
                        {
                            changed.Children.OfType<CHR0EntryNode>().Single(e => e.Name == root.Name).KeyArrays[6].Entries.First()._tangent += .001f;
                            Check(!accepted.CheckCandidate(changed, CancellationToken.None, out string reason) && reason.Contains("motion-control"),
                                "Plain-fit export: retained source motion is checked across its complete curve even when endpoint positions stay unchanged");
                        }
                        var dependency = fitted.KeyArrays[3].Entries.Last(); float originalValue = dependency._value;
                        try
                        {
                            dependency._value += .001f;
                            Check(!accepted.CheckCandidate(master, CancellationToken.None, out string reason) && reason.Contains("dependency"),
                                "Plain-fit export: changed frozen master content invalidates the prepared absolute boundary proof");
                        }
                        finally { dependency._value = originalValue; }
                    }
                    var wrongIdentity = Copy(); wrongIdentity.Inputs[1].Entry = "catalogue-A/same-display-name-member-1";
                    using (var result = Prepare(wrongIdentity)) Check(!result.Eligible && result.Reason.Contains("identity"),
                        "Plain-fit export: duplicate library display names cannot substitute a different exact reference identity");
                    using (var other = NativeAnimationOptimization.CopySource(reference))
                    {
                        other.Children.OfType<CHR0EntryNode>().Single(e => e.Name == body.Name).KeyArrays[3].Entries.First()._value += 10;
                        var wrongPose = Copy(); wrongPose.Inputs[1] = Freeze(other, "exit", options.Exit);
                        using (var result = Prepare(wrongPose)) Check(!result.Eligible && result.Reason.Contains("boundary"),
                            "Plain-fit export: a different frozen reference with the same name and native key is rejected against the actual endpoint pose");
                    }
                    var wrongRig = Copy(); wrongRig.ModelSha256 = new string('a', 64);
                    using (var result = Prepare(wrongRig)) Check(!result.Eligible && result.Reason.Contains("rig"), "Plain-fit export: changed target fingerprint rejects absolute acceptance");
                    using (var result = Prepare(recipe, false)) Check(!result.Eligible, "Plain-fit export: missing reviewed bindings select conservative Exact fallback");
                    using (var result = Prepare(recipe, true, false)) Check(!result.Eligible, "Plain-fit export: actual pins or contact context cannot enter plain-fit bounded acceptance");
                    Check(NativeAnimationOptimization.SourceContentKey(master) == masterBefore && NativeAnimationOptimization.SourceContentKey(source) == sourceBefore &&
                        ((CHR0EntryNode)master.Children[0]).GetFrameValue(6, 5) == root.BindState[6] + .625f &&
                        ((CHR0EntryNode)reference.Children[0]).GetFrameValue(6, 0) == root.BindState[6] + 100,
                        "Plain-fit export: acceptance and failures preserve master/source curves and retain attack placement instead of the reference root position");
                }
            }
        }
    }
}
