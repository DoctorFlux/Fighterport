using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    public static partial class BrawlRetargeter
    {
        private static void BakeAdaptiveNativeMaster(BrawlRig source, BrawlRig target, CHR0Node sourceClip,
            RetargetResult result, bool correction, int last, List<float[][]> branches,List<AdaptiveRotationPose> solvedSamples,int subdivisions,
            CancellationToken cancellation, Action<int, string> progress)
        {
            var output = result.Animation;
            var generatedPairs = new List<NativeGeneratedPairEvidence>();
            result.GeneratedPairRigKey = NativeGeneratedPairRegistry.TryRigKey(target.Bones[0].Model,cancellation);
            var entries = output.Children.OfType<CHR0EntryNode>().ToDictionary(e => e.Name, StringComparer.Ordinal);
            var mapped = new HashSet<int>(result.Plan.Mapping.Select(m => m.TargetIndex));
            int[] parents = target.Rig.Joints.Select(j => j.Parent).ToArray();
            bool[] editable = Enumerable.Range(0, parents.Length).Select(i => mapped.Contains(i) && !result.Plan.TargetMotionControls.Contains(i)).ToArray();
            Vector3 Branch(int bone, float time)
            {
                double at = Math.Max(0, Math.Min(branches.Count - 1, time * subdivisions));
                int first = (int)Math.Floor(at), second = Math.Min(branches.Count - 1, first + 1);
                var a = branches[first][bone]; var b = branches[second][bone];
                return Vector3.Lerp(new Vector3(a[3], a[4], a[5]), new Vector3(b[3], b[4], b[5]), (float)(at - first));
            }
            RetargetCorrectionTrajectory trajectory=correction&&result.Contacts.FloorEnabled&&result.Contacts.Suspended==null&&result.Contacts.BodySolveIterations>0
                ?new RetargetCorrectionTrajectory(target.Rig,solvedSamples.ToArray(),subdivisions,editable,
                    time=>result.Plan.Evaluate(Sample(source,sourceClip,time),false).Pose,Branch,cancellation):null;
            AdaptiveRotationPose SampleMaster(float time)
            {
                cancellation.ThrowIfCancellationRequested();
                float index=time*subdivisions;
                if(index>=0&&index<solvedSamples.Count&&index==Math.Truncate(index))return solvedSamples[(int)index];
                if(trajectory!=null)return trajectory.Evaluate(time);
                var posed = correction ? result.Contacts.Evaluate(time) : result.Plan.Evaluate(Sample(source, sourceClip, time), false);
                return new AdaptiveRotationPose
                {
                    EulerDegrees = posed.Pose.Local.Select((local, i) => EulerNear(local.Rotation, Branch(i, time))).ToArray(),
                    WorldRotations = (Quaternion[])posed.Pose.Orientation.Clone(),
                    LocalRotations = posed.Pose.Local.Select(local => local.Rotation).ToArray(),
                    Translations = posed.Pose.Local.Select(local => local.Translation).ToArray(),
                    Scales = posed.Pose.Local.Select(local => local.Scale).ToArray()
                };
            }
            Quaternion FrozenLocal(int bone, float time)
            {
                var bind = target.Bones[bone].BindState;
                entries.TryGetValue(target.Bones[bone].Name, out var entry);
                float Value(int channel) => entry != null && entry.KeyArrays[channel].KeyCount > 0 ? entry.GetFrameValue(channel, time) : bind[channel];
                return Rotation(new BrawlLib.Internal.Vector3(Value(3), Value(4), Value(5)));
            }
            progress?.Invoke(68, "Preparing fractional native master curves...");
            var bake = AdaptiveRotationCurveBaker.Bake(parents, editable, last, SampleMaster, FrozenLocal, cancellation, generatedEulerContinuation:true);
            if (!bake.Converged)
                throw new InvalidDataException("The full-quality native master could not represent the solved motion within its fractional bake bounds. No retarget result was imported. Unresolved intervals: " + bake.UnresolvedIntervals + "; work " + bake.Work + "; samples " + bake.SampleCount + "; completed tracks " + bake.Tracks.Length + "; first unresolved " + bake.FirstUnresolvedBone + "/" + bake.FirstUnresolvedChannel + " at " + bake.FirstUnresolvedFirstFrame + ".." + bake.FirstUnresolvedLastFrame + " depth " + bake.FirstUnresolvedDepth + " error " + bake.FirstUnresolvedError + "; current bone " + bake.LastBone + " / " + bake.LastChannel + "; interval " + bake.LastFirstFrame + ".." + bake.LastLastFrame + "; depth " + bake.LastDepth + "; measured " + bake.LastMeasuredError + "; fixed endpoint " + bake.MaximumFixedEndpointOrientationDegrees + (bake.WorkLimitReached ? "; operation budget reached." : "."));
            foreach (var track in bake.Tracks)
            {
                cancellation.ThrowIfCancellationRequested();
                var entry = entries[target.Bones[track.Bone].Name];
                void Write(AdaptiveVectorKey[] curve, int firstChannel)
                {
                    if (curve == null) return;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        var array = entry.KeyArrays[firstChannel + axis];
                        while (array.KeyCount > 0) array.Remove(array.Entries.First()._index);
                        float Component(Vector3 value) => axis == 0 ? value.X : axis == 1 ? value.Y : value.Z;
                        foreach (var key in curve)
                        {
                            float value = Component(key.Value), incoming = Component(key.IncomingSlope), outgoing = Component(key.OutgoingSlope);
                            var first = array.SetFrameValue(key.Time, value, true); first._tangent = incoming;
                            if (BitConverter.ToInt32(BitConverter.GetBytes(incoming), 0) != BitConverter.ToInt32(BitConverter.GetBytes(outgoing), 0))
                            {
                                var second = array.SetFrameValue(key.Time, value, true); second._tangent = outgoing;
                                generatedPairs.Add(new NativeGeneratedPairEvidence(entry, firstChannel + axis, first, second));
                            }
                        }
                    }
                }
                Write(track.Scale, 0); Write(track.Rotation, 3); Write(track.Translation, 6);
                entry.Keyframes.PreserveInterpolation = true;
            }
            PreciseCHR0EntryNode.Promote(output);
            result.GeneratedPairEvidence = generatedPairs.ToArray();
            result.OptimizationReport = "Full-quality fractional native master: " + bake.IntegerEulerRebranches + " generated integer Euler rebranches (physical poses retained); " + bake.SampleCount + " solved sample times, " + bake.Work +
                " bounded bake work. Maximum sampled world orientation " + bake.MaximumOrientationDegrees.ToString("G7", System.Globalization.CultureInfo.InvariantCulture) +
                " degrees; local translation " + bake.MaximumTranslationError.ToString("G7", System.Globalization.CultureInfo.InvariantCulture) +
                "; local scale " + bake.MaximumScaleError.ToString("G7", System.Globalization.CultureInfo.InvariantCulture) +
                ". Source-owned control curves retained. "+(trajectory==null?"Direct solved-pose reference.":"Correction trajectory: "+RetargetCorrectionTrajectory.Policy+"; every original eighth-frame pose retained; source FK remains continuously evaluated. Raw independent subframe contact optima are not its motion reference.")+
                " These bake checks are sampled, not continuous or game acceptance. Export size optimization is separate.";
        }
    }
}
