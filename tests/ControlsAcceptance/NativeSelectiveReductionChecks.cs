using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;

internal static partial class Program
{
    static void NativeSelectiveReductionChecks(string modelPath, string stage)
    {
        Directory.CreateDirectory(stage);
        using (var owner = NodeFactory.FromFile(null, modelPath))
        {
            var model = OptimizationWalk(owner).OfType<MDL0Node>().OrderByDescending(m => m.NumVertices).First();
            model.ApplyCHR(null, 0); var bones = model.AllBones.ToArray();
            var root = bones.First(b => !(b.Parent is MDL0BoneNode));
            var leaves = bones.Where(b => !b.Children.OfType<MDL0BoneNode>().Any() && !ReferenceEquals(b, root) &&
                Enumerable.Range(0, 9).All(c => ReplayFloatBits(b.BindState[c]) != int.MinValue) &&
                Enumerable.Range(1, 2).All(c => {
                    double norm = Math.Sqrt(Enumerable.Range(0, 3).Sum(r => Math.Pow(b.BindMatrix[c * 4 + r], 2)));
                    return norm >= .9 && norm <= 1.1;
                })).Take(2).ToArray();
            Check(leaves.Length == 2, "Selective reduction fixture uses two distinct native leaf branches with independently bounded bind basis lengths");
            string bad = leaves[0].Name, good = leaves[1].Name;
            double length = NativeAnimationOptimization.GetBindBoundsDiagonal(model);
            using (var baseline = new CHR0Node { Name = "Independent selective reduction fixture", Version = 5, FrameCount = 5, OriginalPath = "independent-fixture" })
            {
                foreach (var bone in new[] { root, leaves[0], leaves[1] })
                {
                    var entry = baseline.CreateEntry(bone.Name);
                    entry.ScaleCompensateApply = bone.SegScaleCompApply; entry.ScaleCompensateParent = bone.SegScaleCompParent;
                    entry.ClassicScaleOff = !bone.ClassicScale; entry.Keyframes.PreserveInterpolation = true;
                    for (int c = 0; c < 9; c++) for (int at = 0; at < 5; at++)
                        entry.Keyframes.SetFrameValue(c, at, bone.BindState[c], true)._tangent = 0;
                }
                var rootEntry = (CHR0EntryNode)baseline.FindChild(root.Name, false);
                for (int at = 0; at < 5; at++) { rootEntry.KeyArrays[6].GetKeyframe(at)._value += at * .00002f; rootEntry.KeyArrays[6].GetKeyframe(at)._tangent = .00002f; }
                var badEntry = (CHR0EntryNode)baseline.FindChild(bad, false); var goodEntry = (CHR0EntryNode)baseline.FindChild(good, false);
                foreach (int at in new[] { 1, 3 }) { badEntry.KeyArrays[3].GetKeyframe(at)._value += .009f; goodEntry.KeyArrays[3].GetKeyframe(at)._value += .00025f; }
                double badPulse = badEntry.GetFrameValue(3, 1) - (double)badEntry.GetFrameValue(3, 0);
                double goodPulse = goodEntry.GetFrameValue(3, 1) - (double)goodEntry.GetFrameValue(3, 0);
                Check(badPulse > 0 && badPulse < .01 && 1.5 * badPulse < .05 && goodPulse > 0 && goodPulse < badPulse,
                    "Selective reduction fixture: independently authored zero-tangent Hermite pulses obey scalar value/derivative bounds, but only the larger branch should fail collective native quality");
                PreciseCHR0EntryNode.Promote(baseline);
                var exact = baseline.Children.OfType<CHR0EntryNode>().Select(OptimizationTrack).ToArray();
                foreach (var track in exact.Where(t => t.Bone != root.Name))
                    for (int c = 0; c < 9; c++) if (c != 3) track.Channels[c] = new[] { OptimizationKey(0, track.Channels[c][0].Value, 0) };
                var proposed = SelectiveReductionClone(exact);
                foreach (var track in proposed.Where(t => t.Bone != root.Name))
                    track.Channels[3] = new[] { OptimizationKey(0, track.Channels[3][0].Value, 0), OptimizationKey(4, track.Channels[3][0].Value, 0) };
                string content = NativeAnimationOptimization.ContentKey(baseline), storage = NativeAnimationOptimization.StorageKey(baseline);
                var json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };
                string exactBefore = SelectiveReductionWireFingerprint(exact), proposalBefore = SelectiveReductionWireFingerprint(proposed);
                NativeAnimationOptimizationOptions Options(bool exactOnly = false) => new NativeAnimationOptimizationOptions {
                    ExactOnly = exactOnly, LengthScale = length, TargetBones = bones.Select(b => b.Name).ToArray(),
                    ProtectedBones = new[] { root.Name }, ExactBones = new[] { root.Name }, MandatoryFrames = new[] { 0, 4 },
                    ProbeFrames = new[] { .1732051, .4142136, 1.7320508, 3.6190339, 4.9999 }, Budget = new NativeAnimationOptimizationBudget() };
                using (var exactVerified = NativeAnimationOptimization.Prepare(model, baseline, exact, Options(true), stage))
                {
                    Check(exactVerified.Candidate != null && exactVerified.AfterKeys == 71 && exactVerified.MaximumEulerError == 0,
                        "Selective reduction fixture: same-baseline exact constant cleanup independently verifies 71 keys without altering either pulse or protected motion");
                    bool rejected = false;
                    try { using (var ignored = NativeAnimationOptimization.Prepare(model, baseline, proposed, Options(), stage)) { } }
                    catch (InvalidDataException error) { rejected = error.Data.Contains("OptimizationQuality") && Convert.ToString(error.Data["Bone"]) == bad; }
                    Check(rejected, "Selective reduction fixture: complete initial HQ candidate really fails the larger native branch, rather than relying on a mocked rejection");

                    var options = Options();
                    using (var selected = NativeAnimationOptimization.PrepareReduction(model, baseline, proposed, exact, options, stage))
                    {
                        var badResult = (CHR0EntryNode)selected.Candidate.FindChild(bad, false);
                        var goodResult = (CHR0EntryNode)selected.Candidate.FindChild(good, false);
                        Check(selected.AfterKeys == 68 && selected.AfterBytes < exactVerified.AfterBytes &&
                            badResult.KeyArrays[3].KeyCount == 5 && goodResult.KeyArrays[3].KeyCount == 2,
                            "Selective reduction restores only the failing native branch and actual ancestors while retaining the independently eligible branch's smaller HQ curve");
                        Check(selected.MaximumEulerError > 0 && selected.MaximumEulerError <= .01 && selected.MaximumOrientationError <= .01 &&
                            selected.MaximumSkinError <= .0001 * length && selected.Report.Contains("Selective High quality reduction: 2 complete native attempts"),
                            "Selective reduction final report identifies real nonzero bounded quality, complete native revalidation and the actual two-attempt composition");
                        SourceKeysSameTrack(rootEntry, (CHR0EntryNode)selected.Candidate.FindChild(root.Name, false),
                            "Selective reduction retains every authored protected root tuple and mandatory boundary");
                        var measured = SourceKeyQualityMeasure(model, baseline, selected.Candidate);
                        Check(measured.Euler > 0 && measured.Euler <= .01 && measured.Orientation <= .01 && measured.EulerDerivative <= .05 &&
                            measured.Translation <= Math.Min(.0002, .0001 * measured.Length) && measured.Scale <= .00001 &&
                            measured.World <= .0001 * measured.Length && measured.Skin <= .0001 * measured.Length,
                            "Selective reduction independent withheld subframes confirm the complete composed animation, including root travel and every native bone/skin vertex");
                        Check(options.Budget.Samples > selected.Samples && selected.BudgetSamples == options.Budget.Samples &&
                            selected.BudgetBoneVisits == options.Budget.BoneVisits && selected.BudgetVertexVisits == options.Budget.VertexVisits,
                            "Selective reduction keeps one aggregate witness/bone/skin budget across the failed and accepted complete attempts");
                        File.WriteAllText(Path.Combine(stage, "selective-reduction-report.txt"), selected.Report);
                        File.WriteAllText(Path.Combine(stage, "selective-reduction-witnesses.json"), json.Serialize(new {
                            Passed = true, BadBranch = bad, EligibleBranch = good, BadPulse = badPulse, EligiblePulse = goodPulse,
                            AnalyticalEligibleDerivativeBound = 1.5 * goodPulse, NativeMeasured = measured,
                            selected.AfterKeys, selected.AfterBytes, ExactKeys = exactVerified.AfterKeys, ExactBytes = exactVerified.AfterBytes,
                            selected.BudgetSamples, selected.BudgetBoneVisits, selected.BudgetVertexVisits }));
                    }
                }
                using (var selected = NativeAnimationOptimization.PrepareReduction(model, baseline, proposed, null, Options(), stage))
                {
                    var badResult = (CHR0EntryNode)selected.Candidate.FindChild(bad, false);
                    var goodResult = (CHR0EntryNode)selected.Candidate.FindChild(good, false);
                    Check(badResult.KeyArrays.Sum(c => c.KeyCount) == 45 && goodResult.KeyArrays[3].KeyCount == 2 && selected.AfterKeys == 100,
                        "Selective reduction without an exact wire restores complete original branch coefficients while retaining unrelated verified approximation");
                }
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel(); bool stopped = false;
                    try { using (var ignored = NativeAnimationOptimization.PrepareReduction(model, baseline, proposed, exact, Options(), stage, cancellation.Token)) { } }
                    catch (OperationCanceledException) { stopped = true; }
                    Check(stopped, "Selective reduction cancellation publishes no candidate before preparation");
                }
                var exhausted = Options(); exhausted.Budget.Samples = NativeAnimationOptimizationBudget.MaximumSamples - 1;
                bool budgetRejected = false;
                try { using (var ignored = NativeAnimationOptimization.PrepareReduction(model, baseline, proposed, exact, exhausted, stage)) { } }
                catch (InvalidDataException error) { budgetRejected = error.Data.Contains("OptimizationBudget"); }
                Check(budgetRejected && exhausted.Budget.Samples == NativeAnimationOptimizationBudget.MaximumSamples - 1,
                    "Selective reduction does not reset or bypass an exhausted shared verification budget");
                var protectedBranch = Options(); protectedBranch.ProtectedBones = new[] { root.Name, bad };
                bool protectionRejected = false;
                try { using (var ignored = NativeAnimationOptimization.PrepareReduction(model, baseline, proposed, exact, protectedBranch, stage)) { } }
                catch (InvalidDataException error) { protectionRejected = error.Message.IndexOf("protected", StringComparison.OrdinalIgnoreCase) >= 0; }
                Check(protectionRejected && protectedBranch.Budget.Samples == 0,
                    "Selective reduction does not reinterpret an authored structural protection failure as permission to repair or approximate it");
                Check(NativeAnimationOptimization.ContentKey(baseline) == content && NativeAnimationOptimization.StorageKey(baseline) == storage &&
                    SelectiveReductionWireFingerprint(exact) == exactBefore && SelectiveReductionWireFingerprint(proposed) == proposalBefore,
                    "Selective reduction success, rejection, fallback, cancellation and exhaustion preserve the original native data and both immutable scalar-certified wires");
            }
        }
    }

    static ControlTrack[] SelectiveReductionClone(ControlTrack[] tracks) => tracks.Select(track => new ControlTrack { Bone = track.Bone,
        Channels = track.Channels.Select(channel => channel.Select(key => OptimizationKey(key.Frame, key.Value, key.Tangent)).ToArray()).ToArray() }).ToArray();

    static string SelectiveReductionWireFingerprint(ControlTrack[] tracks)
    {
        using (var memory = new MemoryStream())
        {
            using (var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true))
            {
                writer.Write(tracks.Length);
                foreach (var track in tracks)
                {
                    writer.Write(track.Bone); writer.Write(track.Channels.Length);
                    foreach (var channel in track.Channels)
                    {
                        writer.Write(channel.Length);
                        foreach (var key in channel) { writer.Write(key.Frame); writer.Write(ReplayFloatBits(key.Value)); writer.Write(ReplayFloatBits(key.Tangent)); }
                    }
                }
            }
            using (var hash = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(memory.ToArray()));
        }
    }
}
