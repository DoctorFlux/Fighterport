using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using NativeMatrix = BrawlLib.Internal.Matrix;
using NativeVector = BrawlLib.Internal.Vector3;

namespace BrawlCrate.Studio.Retargeting
{
    // Measurements only. A caller must separately review and freeze the exact
    // rig/skin digest before admitting this as anatomical evidence.
    public sealed class CranialPatchEvidence
    {
        public int RootJointIndex { get; }
        public string RootJointName { get; }
        public bool DirectNeckInfluence { get; }
        public int InfluencedVertices { get; }
        public int MajorityVertices { get; }
        public double WeightedVertexMass { get; }
        public Vector3 BindCentroid { get; }
        public Vector3 BindCentroidInNeckLocal { get; }
        public float BindRmsRadius { get; }
        public float BindMaximumRadius { get; }

        internal CranialPatchEvidence(int root, string name, bool direct, int influenced, int majority,
            double mass, Vector3 centroid, Vector3 local, float rms, float maximum)
        {
            RootJointIndex = root; RootJointName = name; DirectNeckInfluence = direct;
            InfluencedVertices = influenced; MajorityVertices = majority; WeightedVertexMass = mass;
            BindCentroid = centroid; BindCentroidInNeckLocal = local;
            BindRmsRadius = rms; BindMaximumRadius = maximum;
        }
    }

    public sealed class CranialAnchorProposal
    {
        // These are conservative admission limits for a *reviewable proposal*,
        // not an automatic anatomical mapping or a retarget-quality tolerance.
        public const int MaximumDecodedVertices = 250000;
        public const int MinimumMajorityVertices = 16;
        public const double MinimumWeightedVertexMass = 16;
        public const double MinimumDirectNeckDominance = 4;
        public const float MaximumWeightSumDeviation = 0.01f;

        public bool Available => RejectionReason == null;
        public string RejectionReason { get; }
        public int NeckIndex { get; }
        public string NeckBoneName { get; }
        // Semantic bind-rig/skinned-point digest, independent of object and
        // vertex storage order. Recompute it on the current native model before
        // using CandidateVertexIndices; the indices themselves are session-local.
        public string RigAndSkinDigest { get; }
        public int DecodedVertexCount { get; }
        public int RepresentedWeightCount { get; }
        public Vector3 BindLocalPoint { get; }
        public float Radius { get; }
        public IReadOnlyList<CranialPatchEvidence> CandidatePatches { get; }
        public IReadOnlyList<int> CandidateVertexIndices { get; }

        internal CranialAnchorProposal(string reason, int neck, string neckName, string digest,
            int vertices, int weights, Vector3 local, float radius,
            CranialPatchEvidence[] patches, int[] candidateVertices)
        {
            RejectionReason = reason; NeckIndex = neck; NeckBoneName = neckName;
            RigAndSkinDigest = digest; DecodedVertexCount = vertices; RepresentedWeightCount = weights;
            BindLocalPoint = local; Radius = radius;
            CandidatePatches = Array.AsReadOnly(patches ?? new CranialPatchEvidence[0]);
            CandidateVertexIndices = Array.AsReadOnly(candidateVertices ?? new int[0]);
        }
    }

    public sealed class CranialHeadSkinMeasurement
    {
        public bool Available => RejectionReason == null;
        public string RejectionReason { get; }
        public int HeadIndex { get; }
        public string HeadBoneName { get; }
        public int MajorityVertices { get; }
        public double WeightedVertexMass { get; }
        public Vector3 BindLocalPoint { get; }
        public float Radius { get; }

        internal CranialHeadSkinMeasurement(string reason, int index, string name,
            int majority, double mass, Vector3 local, float radius)
        {
            RejectionReason = reason; HeadIndex = index; HeadBoneName = name;
            MajorityVertices = majority; WeightedVertexMass = mass;
            BindLocalPoint = local; Radius = radius;
        }
    }

    public static partial class BrawlRetargeter
    {
        private sealed class CranialWeightedPoint
        {
            internal Vector3 Position;
            internal float Mass;
        }

        private sealed class CranialPatchAccumulator
        {
            internal int Joint;
            internal string Name;
            internal int Influenced, Majority;
            internal double Mass, SumX, SumY, SumZ;
            internal readonly List<CranialWeightedPoint> Points = new List<CranialWeightedPoint>();
            internal readonly List<int> MajorityVertexIndices = new List<int>();

            internal void Add(int vertexIndex, Vector3 point, float weight)
            {
                if (weight <= 0) return;
                Influenced++; if (weight >= 0.5f) { Majority++; MajorityVertexIndices.Add(vertexIndex); }
                Mass += weight; SumX += point.X * (double)weight;
                SumY += point.Y * (double)weight; SumZ += point.Z * (double)weight;
                Points.Add(new CranialWeightedPoint {Position = point, Mass = weight});
            }

            internal CranialPatchEvidence Describe(int neck, MDL0BoneNode[] bones)
            {
                Vector3 centroid = Mass > 0 ? new Vector3((float)(SumX / Mass), (float)(SumY / Mass),
                    (float)(SumZ / Mass)) : Vector3.Zero;
                double squared = 0; float maximum = 0;
                foreach (CranialWeightedPoint point in Points)
                {
                    float distance = Vector3.Distance(point.Position, centroid);
                    squared += point.Mass * (double)distance * distance;
                    maximum = Math.Max(maximum, distance);
                }
                var native = new NativeVector(centroid.X, centroid.Y, centroid.Z);
                Vector3 local = Mass > 0 ? Vector(bones[neck].InverseBindMatrix * native) : Vector3.Zero;
                return new CranialPatchEvidence(Joint, Name, Joint == neck, Influenced, Majority,
                    Mass, centroid, local, Mass > 0 ? (float)Math.Sqrt(squared / Mass) : 0, maximum);
            }
        }

        // A Brawl MDL0 may skin the visible head mostly to the neck itself and
        // place several animated appendages directly below it. Report the
        // direct-neck skin and every immediate neck-child subtree independently;
        // never promote a highest/tallest appendage to a head joint here.
        public static CranialAnchorProposal ProposeCranialAnchor(MDL0Node model,
            SkeletonCharacterization characterization)
        {
            if (model == null || characterization == null)
                return CranialRejected("An exact native model and its fresh characterization are required.");
            MDL0BoneNode[] bones = model.AllBones.ToArray();
            if (!characterization.Roles.TryGetValue("neck", out int neck) || neck < 0 || neck >= bones.Length)
                return CranialRejected("No reviewed structural neck joint is available.");
            if (characterization.Roles.ContainsKey("head"))
                return CranialRejected("This rig already has a distinct mapped head; a virtual cranial anchor is unnecessary.", neck, bones[neck].Name);
            if (bones.Length == 0 || bones.Length > 512 || bones.Any(b => b == null))
                return CranialRejected("The native skeleton is missing or exceeds the supported 512-joint analysis limit.", neck, bones[neck].Name);
            if (bones.Any(b => Enumerable.Range(0, 9).Any(c => !RigMath.Finite(b.BindState[c])) ||
                !CranialFiniteMatrix(b.BindMatrix) || !CranialFiniteMatrix(b.InverseBindMatrix)))
                return CranialRejected("A native bind or inverse-bind transform is non-finite.", neck, bones[neck].Name);
            if (model.NativeEditingBackend != null)
                return CranialRejected("This proposal requires an original MDL0 skin, not a foreign native editing backend.", neck, bones[neck].Name);

            var names = bones.Select((bone, index) => new {bone.Name, Index = index}).ToArray();
            if (names.Any(x => string.IsNullOrEmpty(x.Name)) || names.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != bones.Length)
                return CranialRejected("Bone names are missing or duplicated; exact skin associations cannot be frozen.", neck, bones[neck].Name);
            var byBone = bones.Select((bone, index) => new {Bone = bone, Index = index})
                .ToDictionary(x => x.Bone, x => x.Index);
            int[] parent = bones.Select(b => b.Parent is MDL0BoneNode p && byBone.TryGetValue(p, out int index) ? index : -1).ToArray();
            var children = Enumerable.Range(0, bones.Length).Where(i => parent[i] == neck).ToArray();
            var branches = new int[bones.Length];
            for (int i = 0; i < branches.Length; i++)
            {
                int current = i;
                while (current >= 0 && parent[current] >= 0 && parent[current] != neck) current = parent[current];
                branches[i] = i == neck ? neck : current >= 0 && parent[current] == neck ? current : -1;
            }
            var groups = new Dictionary<int, CranialPatchAccumulator> {
                [neck] = new CranialPatchAccumulator {Joint = neck, Name = bones[neck].Name}
            };
            foreach (int child in children)
                groups.Add(child, new CranialPatchAccumulator {Joint = child, Name = bones[child].Name});

            var objects = model.PolygonList?.OfType<MDL0ObjectNode>().ToArray();
            if (objects == null || objects.Any(o => o.Vertices == null && o.FaceCount > 0))
                return CranialRejected("Decoded native mesh vertices are unavailable; reload the original model.", neck, bones[neck].Name);
            int vertexCount = objects.Sum(o => o.Vertices?.Count ?? 0);
            if (vertexCount < CranialAnchorProposal.MinimumMajorityVertices ||
                vertexCount > CranialAnchorProposal.MaximumDecodedVertices)
                return CranialRejected("The decoded mesh is empty, too small, or exceeds the bounded proposal limit of " +
                    CranialAnchorProposal.MaximumDecodedVertices + " vertices.", neck, bones[neck].Name);

            var vertexSignatures = new List<string>(vertexCount);
            int representedWeights = 0, flattened = 0;
            string invalid = null;
            foreach (MDL0ObjectNode obj in objects)
                if (obj.Vertices != null) foreach (Vertex3 vertex in obj.Vertices)
                {
                    if (vertex == null) { invalid = "A decoded native vertex is missing."; break; }
                    var matrix = vertex.GetMatrixNode();
                    if (matrix?.Weights == null || matrix.Weights.Count == 0)
                    { invalid = "A native vertex has no represented skin weights."; break; }
                    NativeVector bind = matrix is MDL0BoneNode primary ? primary.BindMatrix * vertex.Position : vertex.Position;
                    Vector3 position = Vector(bind);
                    if (!RigMath.Finite(position)) { invalid = "A native bind vertex is non-finite."; break; }
                    var vertexWeights = new List<Tuple<string, float, int>>(matrix.Weights.Count);
                    double total = 0;
                    var membership = new Dictionary<int, float>();
                    foreach (var weight in matrix.Weights)
                    {
                        if (!(weight.Bone is MDL0BoneNode bone) || !byBone.TryGetValue(bone, out int index) ||
                            !RigMath.Finite(weight.Weight) || weight.Weight < 0 || weight.Weight > 1)
                        { invalid = "A native vertex has an unknown or invalid skin influence."; break; }
                        vertexWeights.Add(Tuple.Create(bone.Name, weight.Weight, index));
                        representedWeights++; total += weight.Weight;
                        int branch = branches[index];
                        if (branch >= 0) membership[branch] = membership.TryGetValue(branch, out float prior) ? prior + weight.Weight : weight.Weight;
                    }
                    if (invalid != null) break;
                    if (Math.Abs(total - 1) > CranialAnchorProposal.MaximumWeightSumDeviation)
                    { invalid = "A native vertex's represented skin weights do not sum to one within the frozen proposal bound."; break; }
                    foreach (var pair in membership) groups[pair.Key].Add(flattened, position, pair.Value);
                    vertexSignatures.Add(CranialVertexSignature(position, vertexWeights));
                    flattened++;
                }
            if (invalid != null)
                return CranialRejected(invalid, neck, bones[neck].Name, null, flattened, representedWeights);

            string digest = CranialSkinDigest(bones, parent, vertexSignatures);
            CranialPatchEvidence[] patches = groups.Values.Select(g => g.Describe(neck, bones))
                .OrderByDescending(p => p.WeightedVertexMass).ThenBy(p => p.RootJointName, StringComparer.Ordinal).ToArray();
            CranialPatchEvidence direct = patches.First(p => p.DirectNeckInfluence);
            double other = patches.Where(p => !p.DirectNeckInfluence).Select(p => p.WeightedVertexMass).DefaultIfEmpty(0).Max();
            string rejection = null;
            if (direct.MajorityVertices < CranialAnchorProposal.MinimumMajorityVertices ||
                direct.WeightedVertexMass < CranialAnchorProposal.MinimumWeightedVertexMass)
                rejection = "Direct-neck skin is too small for a reviewed cranial proposal.";
            else if (other > 0 && direct.WeightedVertexMass < other * CranialAnchorProposal.MinimumDirectNeckDominance)
                rejection = "Several neck-child skin regions have comparable mass; a central cranial patch cannot be proposed without review.";
            else if (!RigMath.Finite(direct.BindCentroidInNeckLocal) || !RigMath.Finite(direct.BindRmsRadius) ||
                direct.BindRmsRadius <= 0 || direct.BindCentroid.Y <= Vector(bones[neck].BindMatrix.GetPoint()).Y)
                rejection = "Direct-neck skin has no finite, positive-radius centroid above the native neck bind position.";
            int[] candidate = groups[neck].MajorityVertexIndices.ToArray();
            return new CranialAnchorProposal(rejection, neck, bones[neck].Name, digest, vertexCount,
                representedWeights, direct.BindCentroidInNeckLocal, direct.BindRmsRadius, patches, candidate);
        }

        // The virtual source radius is the weighted RMS of a joint's direct
        // skin. Use the *same measurement* for a target with a real head joint;
        // the legacy AABB-top-to-head-joint HeadRadius is a different metric.
        // This method observes only; a caller must still review both rigs.
        public static CranialHeadSkinMeasurement MeasureMappedHeadSkin(MDL0Node model, BrawlRig rig)
        {
            if (model == null || rig?.Rig == null || rig.Bones == null ||
                !rig.Rig.Roles.TryGetValue("head", out int head) || rig.ProvisionalHeadForExactNativeCopy ||
                head < 0 || head >= rig.Bones.Length)
                return CranialHeadRejected("A real reviewed head joint on the exact MDL0 rig is required.");
            MDL0BoneNode[] bones = model.AllBones.ToArray();
            if (bones.Length != rig.Bones.Length || head >= bones.Length ||
                !string.Equals(BrawlRetargeter.SkeletonFingerprint(model), rig.Fingerprint, StringComparison.Ordinal) ||
                !ReferenceEquals(bones[head], rig.Bones[head]))
                return CranialHeadRejected("The supplied rig does not own this exact native model.");
            if (!CranialFiniteMatrix(bones[head].BindMatrix) || !CranialFiniteMatrix(bones[head].InverseBindMatrix))
                return CranialHeadRejected("The mapped head bind or inverse bind is non-finite.", head, bones[head].Name);
            var objects = model.PolygonList?.OfType<MDL0ObjectNode>().ToArray();
            if (objects == null || objects.Any(o => o.Vertices == null && o.FaceCount > 0))
                return CranialHeadRejected("Decoded native mesh vertices are unavailable.", head, bones[head].Name);
            int vertexCount = objects.Sum(o => o.Vertices?.Count ?? 0);
            if (vertexCount < CranialAnchorProposal.MinimumMajorityVertices ||
                vertexCount > CranialAnchorProposal.MaximumDecodedVertices)
                return CranialHeadRejected("The decoded mesh is outside the bounded head-skin analysis limit.", head, bones[head].Name);
            var patch = new CranialPatchAccumulator {Joint = head, Name = bones[head].Name};
            var byBone = bones.Select((bone, index) => new {Bone = bone, Index = index})
                .ToDictionary(x => x.Bone, x => x.Index);
            int flattened = 0;
            foreach (MDL0ObjectNode obj in objects)
                if (obj.Vertices != null) foreach (Vertex3 vertex in obj.Vertices)
                {
                    if (vertex == null) return CranialHeadRejected("A decoded native vertex is missing.", head, bones[head].Name);
                    var matrix = vertex.GetMatrixNode();
                    if (matrix?.Weights == null || matrix.Weights.Count == 0)
                        return CranialHeadRejected("A native vertex has no represented skin weights.", head, bones[head].Name);
                    NativeVector bind = matrix is MDL0BoneNode primary ? primary.BindMatrix * vertex.Position : vertex.Position;
                    Vector3 position = Vector(bind);
                    if (!RigMath.Finite(position)) return CranialHeadRejected("A native bind vertex is non-finite.", head, bones[head].Name);
                    double sum = 0; float direct = 0;
                    foreach (var weight in matrix.Weights)
                    {
                        if (!(weight.Bone is MDL0BoneNode bone) || !byBone.ContainsKey(bone) ||
                            !RigMath.Finite(weight.Weight) || weight.Weight < 0 || weight.Weight > 1)
                            return CranialHeadRejected("A native vertex has an unknown or invalid skin influence.", head, bones[head].Name);
                        sum += weight.Weight;
                        if (ReferenceEquals(bone, bones[head])) direct += weight.Weight;
                    }
                    if (Math.Abs(sum - 1) > CranialAnchorProposal.MaximumWeightSumDeviation)
                        return CranialHeadRejected("A native vertex's skin weights do not sum to one within the frozen proposal bound.", head, bones[head].Name);
                    patch.Add(flattened++, position, direct);
                }
            CranialPatchEvidence evidence = patch.Describe(head, bones);
            string reason = null;
            if (evidence.MajorityVertices < CranialAnchorProposal.MinimumMajorityVertices ||
                evidence.WeightedVertexMass < CranialAnchorProposal.MinimumWeightedVertexMass)
                reason = "Direct-head skin is too small for a comparable radius measurement.";
            else if (!RigMath.Finite(evidence.BindCentroidInNeckLocal) ||
                !RigMath.Finite(evidence.BindRmsRadius) || evidence.BindRmsRadius <= 0)
                reason = "Direct-head skin has no finite positive-radius bind patch.";
            return new CranialHeadSkinMeasurement(reason, head, bones[head].Name,
                evidence.MajorityVertices, evidence.WeightedVertexMass,
                evidence.BindCentroidInNeckLocal, evidence.BindRmsRadius);
        }

        internal static float MeasureMappedHeadSkinRadius(MDL0Node model, BrawlRig rig)
        {
            CranialHeadSkinMeasurement measured = MeasureMappedHeadSkin(model, rig);
            return measured.Available ? measured.Radius : 0;
        }

        private static CranialHeadSkinMeasurement CranialHeadRejected(string reason, int index = -1,
            string name = null) => new CranialHeadSkinMeasurement(reason, index, name, 0, 0, Vector3.Zero, 0);

        private static CranialAnchorProposal CranialRejected(string reason, int neck = -1, string name = null,
            string digest = null, int vertices = 0, int weights = 0) =>
            new CranialAnchorProposal(reason, neck, name, digest, vertices, weights, Vector3.Zero, 0,
                new CranialPatchEvidence[0], new int[0]);

        private static string CranialVertexSignature(Vector3 position, List<Tuple<string, float, int>> weights)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(position.X); writer.Write(position.Y); writer.Write(position.Z);
                writer.Write(weights.Count);
                foreach (var weight in weights.OrderBy(w => w.Item1, StringComparer.Ordinal).ThenBy(w => w.Item2))
                { writer.Write(weight.Item1); writer.Write(weight.Item2); }
                writer.Flush();
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "");
            }
        }

        private static string CranialSkinDigest(MDL0BoneNode[] bones, int[] parent, List<string> vertices)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write("fighterport-cranial-rig-skin-v1");
                writer.Write(bones.Length);
                foreach (int i in Enumerable.Range(0, bones.Length).OrderBy(i => bones[i].Name, StringComparer.Ordinal))
                {
                    writer.Write(bones[i].Name); writer.Write(parent[i] >= 0 ? bones[parent[i]].Name : "");
                    for (int channel = 0; channel < 9; channel++) writer.Write(bones[i].BindState[channel]);
                    CranialWriteMatrix(writer, bones[i].BindMatrix);
                    CranialWriteMatrix(writer, bones[i].InverseBindMatrix);
                }
                writer.Write(vertices.Count);
                foreach (string signature in vertices.OrderBy(s => s, StringComparer.Ordinal)) writer.Write(signature);
                writer.Flush();
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void CranialWriteMatrix(BinaryWriter writer, NativeMatrix m)
        {
            writer.Write(m.M11); writer.Write(m.M12); writer.Write(m.M13); writer.Write(m.M14);
            writer.Write(m.M21); writer.Write(m.M22); writer.Write(m.M23); writer.Write(m.M24);
            writer.Write(m.M31); writer.Write(m.M32); writer.Write(m.M33); writer.Write(m.M34);
            writer.Write(m.M41); writer.Write(m.M42); writer.Write(m.M43); writer.Write(m.M44);
        }

        private static bool CranialFiniteMatrix(NativeMatrix m) =>
            RigMath.Finite(m.M11) && RigMath.Finite(m.M12) && RigMath.Finite(m.M13) && RigMath.Finite(m.M14) &&
            RigMath.Finite(m.M21) && RigMath.Finite(m.M22) && RigMath.Finite(m.M23) && RigMath.Finite(m.M24) &&
            RigMath.Finite(m.M31) && RigMath.Finite(m.M32) && RigMath.Finite(m.M33) && RigMath.Finite(m.M34) &&
            RigMath.Finite(m.M41) && RigMath.Finite(m.M42) && RigMath.Finite(m.M43) && RigMath.Finite(m.M44);
    }
}
