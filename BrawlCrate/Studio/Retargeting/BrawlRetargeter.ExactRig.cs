using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    public static partial class BrawlRetargeter
    {
        // Identical native skeletons need no proportion or contact correction.
        // Require the complete transform contract, rather than a matching name
        // or a semantic role subset. Other rigs retain the original Next bake.
        private static bool ExactNativeTransformRig(BrawlRig source, BrawlRig target, CHR0Node clip)
        {
            if (source.Rig.NativeEvaluation != null || target.Rig.NativeEvaluation != null ||
                source.Fingerprint != target.Fingerprint || source.Bones.Length != target.Bones.Length)
                return false;
            if (source.Rig.CoordinateUp != target.Rig.CoordinateUp ||
                source.Rig.CoordinateFrame != target.Rig.CoordinateFrame ||
                source.Rig.Roles.Count != target.Rig.Roles.Count ||
                source.Rig.Roles.Any(role => !target.Rig.Roles.TryGetValue(role.Key, out int joint) || joint != role.Value))
                return false;
            var names = new HashSet<string>(target.Bones.Select(b => b.Name), StringComparer.Ordinal);
            if (clip.Children.OfType<CHR0EntryNode>().Any(entry => !names.Contains(entry.Name))) return false;
            int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            for (int i = 0; i < source.Bones.Length; i++)
            {
                var a = source.Bones[i]; var b = target.Bones[i];
                if (a.Name != b.Name || (a.Parent as MDL0BoneNode)?.Name != (b.Parent as MDL0BoneNode)?.Name ||
                    a.SegScaleCompApply != b.SegScaleCompApply || a.SegScaleCompParent != b.SegScaleCompParent ||
                    a.ClassicScale != b.ClassicScale) return false;
                for (int channel = 0; channel < 9; channel++)
                    if (Bits(a.BindState[channel]) != Bits(b.BindState[channel])) return false;
                for (int element = 0; element < 16; element++)
                    if (Bits(a.BindMatrix[element]) != Bits(b.BindMatrix[element]) ||
                        Bits(a.InverseBindMatrix[element]) != Bits(b.InverseBindMatrix[element])) return false;
            }
            return true;
        }

        // Matching skeletons can have different costume meshes and support
        // surfaces. Keep the complete joint mapping in that case, while only
        // bypassing contact fitting when the full skinned geometry also agrees.
        private static bool ExactNativeSkin(BrawlRig source, BrawlRig target, CancellationToken cancellation)
        {
            var a = source.Bones.First().Model?.PolygonList?.OfType<MDL0ObjectNode>().ToArray();
            var b = target.Bones.First().Model?.PolygonList?.OfType<MDL0ObjectNode>().ToArray();
            if (a == null || b == null || a.Length != b.Length) return false;
            int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            for (int mesh = 0; mesh < a.Length; mesh++)
            {
                if (a[mesh].Name != b[mesh].Name || a[mesh].Vertices == null || b[mesh].Vertices == null ||
                    a[mesh].Vertices.Count != b[mesh].Vertices.Count) return false;
                for (int vertex = 0; vertex < a[mesh].Vertices.Count; vertex++)
                {
                    if ((vertex & 1023) == 0) cancellation.ThrowIfCancellationRequested();
                    var av = a[mesh].Vertices[vertex]; var bv = b[mesh].Vertices[vertex];
                    if (Bits(av.Position._x) != Bits(bv.Position._x) || Bits(av.Position._y) != Bits(bv.Position._y) ||
                        Bits(av.Position._z) != Bits(bv.Position._z)) return false;
                    var am = av.GetMatrixNode(); var bm = bv.GetMatrixNode();
                    if ((am as MDL0BoneNode)?.Name != (bm as MDL0BoneNode)?.Name) return false;
                    var aw = am?.Weights; var bw = bm?.Weights;
                    if ((aw?.Count ?? -1) != (bw?.Count ?? -1)) return false;
                    if (aw == null) continue;
                    for (int weight = 0; weight < aw.Count; weight++)
                        if ((aw[weight].Bone as MDL0BoneNode)?.Name != (bw[weight].Bone as MDL0BoneNode)?.Name ||
                            Bits(aw[weight].Weight) != Bits(bw[weight].Weight)) return false;
                }
            }
            return true;
        }

        private static HumanoidRetargeter ExactRigPlan(BrawlRig source, BrawlRig target)
        {
            var basic = new HumanoidRetargeter(source.Rig, target.Rig);
            var usedSource = new HashSet<int>(basic.Mapping.Select(m => m.SourceIndex));
            var usedTarget = new HashSet<int>(basic.Mapping.Select(m => m.TargetIndex));
            var details = new Dictionary<int, int>();
            for (int i = 0; i < source.Bones.Length; i++)
                if (!usedSource.Contains(i) && !usedTarget.Contains(i)) details.Add(i, i);
            var plan = new HumanoidRetargeter(source.Rig, target.Rig, null, details, "detail.exact");
            if (plan.Mapping.Count() != source.Bones.Length ||
                plan.Mapping.Any(map => map.SourceIndex != map.TargetIndex))
                throw new InvalidDataException("An exact native rig could not retain every corresponding joint.");
            return plan;
        }

        private static RetargetResult BakeExactNativeRig(BrawlRig source, BrawlRig target, CHR0Node clip,
            RetargetKeyframeMode mode, CancellationToken cancellation, Action<int, string> progress)
        {
            cancellation.ThrowIfCancellationRequested();
            if (source.Bones.Length > 4096 ||
                clip.Children.OfType<CHR0EntryNode>().Sum(entry => entry.KeyArrays.Sum(channel => (long)channel.KeyCount)) > 2000000)
                throw new InvalidDataException("The exact-rig transfer exceeds the native joint or two-million-key operation budget.");
            var plan = ExactRigPlan(source, target);
            string sourceContent = NativeAnimationOptimization.SourceContentKey(clip);
            CHR0Node copy = null;
            var result = new RetargetResult { Plan = plan, SourceRig = source.Fingerprint, TargetRig = target.Fingerprint };
            string folder = Path.Combine(Path.GetTempPath(), "Fighterport-exactrig-" + Guid.NewGuid().ToString("N"));
            try
            {
                progress?.Invoke(10, "Retaining all exact-rig native animation tracks...");
                copy = NativeAnimationOptimization.Copy(clip);
                if (NativeAnimationOptimization.ContentKey(copy) != NativeAnimationOptimization.ContentKey(clip))
                    throw new InvalidDataException("The exact-rig copy changed authored native animation content.");
                result.Animation = copy; copy = null;
                // No fitting is needed when the complete native transform rig
                // agrees. Report support as unmodified, without manufacturing
                // a corrected contact timeline or changing authored motion.
                int last = clip.Loop ? clip.FrameCount : clip.FrameCount - 1;
                result.Contacts = new ContactMotion(plan, time => Sample(source, clip, time), last, false, cancellation);
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, "exact-rig.chr0");
                result.Animation.ExportChecked(file);
                using (var decoded = NodeFactory.FromFile(null, file) as CHR0Node)
                    if (decoded == null || NativeAnimationOptimization.ContentKey(decoded) != NativeAnimationOptimization.ContentKey(result.Animation))
                        throw new InvalidDataException("The exact-rig copy changed after native serialization and fresh reopen.");
                if (NativeAnimationOptimization.SourceContentKey(clip) != sourceContent)
                    throw new InvalidOperationException("The source animation changed during exact-rig preparation.");
                result.OptimizationReport = "Exact native rig: all " + source.Bones.Length +
                    " structurally corresponding joints and every authored CHR0 track were retained in an independent master copy. " +
                    "No proportion, IK or contact correction was applied; native bytes were freshly reopened and semantic content verified. " +
                    (mode == RetargetKeyframeMode.Dense ? "The exported dense result retains that content." :
                        "The requested source-key result is a separately verified, error-bounded projection of that master.");
                result.GeneratedPairRigKey = NativeGeneratedPairRegistry.TryRigKey(target.Bones[0].Model, cancellation);
                if (mode != RetargetKeyframeMode.Dense)
                    RetargetKeyframes.Prepare(result, source, target, clip, mode, cancellation, progress);
                result.GeneratedPairEvidence = NativeGeneratedPairRegistry.Session.Register(result.Animation,
                    result.GeneratedPairRigKey, result.GeneratedPairEvidence, cancellation);
                progress?.Invoke(100, "Exact-rig animation ready");
                return result;
            }
            catch { result.Dispose(); throw; }
            finally
            {
                copy?.Dispose();
                string file = Path.Combine(folder, "exact-rig.chr0");
                if (File.Exists(file)) File.Delete(file);
                if (Directory.Exists(folder)) Directory.Delete(folder);
            }
        }
    }
}
