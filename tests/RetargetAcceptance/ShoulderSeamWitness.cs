using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

internal static partial class Program
{
    // Read-only native geometry evidence. Bind-neighbor pairs are selected on
    // each rig independently; different meshes have no invented vertex map.
    private static object ReviewedShoulderSeamWitness(BrawlRig source, MDL0Node sourceModel,
        CHR0Node sourceClip, BrawlRig target, MDL0Node targetModel, CHR0Node targetClip)
    {
        float[] times = Enumerable.Range(0, 49).Select(index => index / 4f).ToArray();
        NativeSeamReport sourceReport = NativeShoulderSeamMotion(source, sourceModel, sourceClip, times);
        NativeSeamReport targetReport = NativeShoulderSeamMotion(target, targetModel, targetClip, times);
        var candidateComparisons = sourceReport.Sides.Join(targetReport.Sides, left => left.Side,
            right => right.Side, (left, right) =>
            {
                double allowance = left.MaximumChangePerArmLength +
                    Math.Max(0, right.MaximumBindGapPerArmLength - left.MaximumBindGapPerArmLength) + .01;
                return new
                {
                    left.Side,
                    SourceMotionPerSourceArm = left.MaximumChangePerArmLength,
                    BindGeometryAllowancePerTargetArm = Math.Max(0,
                        right.MaximumBindGapPerArmLength - left.MaximumBindGapPerArmLength),
                    MeasurementMarginPerTargetArm = .01,
                    MaximumAllowedChangePerTargetArm = allowance,
                    ObservedChangePerTargetArm = right.MaximumChangePerArmLength,
                    BelowCandidateThreshold = left.PairCount > 0 && right.PairCount > 0 &&
                        right.MaximumChangePerArmLength <= allowance
                };
            }).ToArray();
        return new
        {
            Scope = "read-only native bind-neighbor shoulder seam motion; no cross-mesh vertex correspondence or floor contact inferred",
            Limitations = "nearest bind neighbors are a diagnostic proxy, not authored attachment pairs; different rigs have different vertex sets and skin geometry; quarter-frame samples do not establish continuous motion or actual game behavior",
            Source = sourceReport,
            Target = targetReport,
            CandidateComparisons = candidateComparisons,
            CandidateThreshold = "Diagnostic only, not an accepted bound. Per side: source maximum absolute seam-gap change/source arm length + positive target-minus-source maximum bind-neighbor gap/own arm length + 0.01 target arm length margin. Pair selection and morphology allowance require independent qualification before this could gate acceptance.",
            ProceduralPairChecks = CheckProceduralSeamPairs(),
            QuarterFrames = times.Length,
            LastTime = times[times.Length - 1],
            FiniteSampledCoverageOnly = true,
            GameRuntimeVerified = false
        };
    }

    private sealed class NativeSeamPair
    {
        internal int ArmVertex, TorsoVertex;
        internal float BindGap;
    }

    private static NativeSeamPair[] SelectNativeSeamPairs(Vector3[] bind, int[][] influences,
        int[] parents, int arm, int[] trunk, Vector3 pivot, float armLength)
    {
        bool Descendant(int joint)
        {
            for (int current = joint; current >= 0; current = parents[current])
                if (current == arm) return true;
            return false;
        }
        int[] nearArm = Enumerable.Range(0, bind.Length).Where(index =>
            influences[index].Any(Descendant) &&
            Vector3.Distance(bind[index], pivot) <= armLength * .35f).ToArray();
        int[] nearTrunk = Enumerable.Range(0, bind.Length).Where(index =>
            !influences[index].Any(Descendant) &&
            influences[index].Any(joint => trunk.Contains(joint)) &&
            Vector3.Distance(bind[index], pivot) <= armLength * .35f).ToArray();
        if ((long)nearArm.Length * nearTrunk.Length > 2000000)
            throw new InvalidDataException("The read-only shoulder pair search exceeded its two-million-distance budget.");
        var pairs = new List<NativeSeamPair>();
        foreach (int armVertex in nearArm)
        {
            int nearest = -1;
            float best = armLength * .2f;
            foreach (int trunkVertex in nearTrunk)
            {
                float distance = Vector3.Distance(bind[armVertex], bind[trunkVertex]);
                if (!(distance < best)) continue;
                nearest = trunkVertex;
                best = distance;
            }
            if (nearest >= 0)
                pairs.Add(new NativeSeamPair
                {
                    ArmVertex = armVertex, TorsoVertex = nearest, BindGap = best
                });
        }
        return pairs.OrderBy(pair => pair.BindGap).ThenBy(pair => pair.ArmVertex)
            .Take(512).ToArray();
    }

    private static object CheckProceduralSeamPairs()
    {
        // Geometry and influence IDs are procedural. Names and storage order
        // cannot affect the selector, and absent trunk context is unavailable.
        Vector3[] bind =
        {
            new Vector3(.10f, 0, 0), new Vector3(.15f, .1f, 0),
            new Vector3(.2f, .3f, 0), new Vector3(0, 0, 0),
            new Vector3(0, .1f, 0), new Vector3(0, .3f, 0)
        };
        int[][] influences =
        {
            new[] {3}, new[] {4}, new[] {3}, new[] {1}, new[] {2}, new[] {1}
        };
        int[] parents = {-1, 0, 1, 2, 3, 4};
        NativeSeamPair[] original = SelectNativeSeamPairs(bind, influences, parents,
            3, new[] {1, 2}, Vector3.Zero, 2);
        int[] oldToNew = {4, 1, 5, 0, 3, 2};
        int[] shuffledParents = new int[parents.Length];
        for (int old = 0; old < parents.Length; old++)
            shuffledParents[oldToNew[old]] = parents[old] < 0 ? -1 : oldToNew[parents[old]];
        int[] vertexOrder = {5, 1, 3, 0, 4, 2};
        NativeSeamPair[] reordered = SelectNativeSeamPairs(
            vertexOrder.Select(index => bind[index] * 1.5f).ToArray(),
            vertexOrder.Select(index => influences[index].Select(joint => oldToNew[joint]).ToArray()).ToArray(),
            shuffledParents, oldToNew[3], new[] {oldToNew[1], oldToNew[2]}, Vector3.Zero, 3);
        NativeSeamPair[] missing = SelectNativeSeamPairs(bind,
            influences.Select((array, index) => index >= 3 ? new int[0] : array).ToArray(),
            parents, 3, new[] {1, 2}, Vector3.Zero, 2);
        // A target can have an extra shoulder link and anisotropic proportions.
        int[] twoShoulderParents = {-1, 0, 1, 6, 3, 4, 2};
        Vector3[] unequalBind = bind.Select(point => new Vector3(
            point.X * 1.6f, point.Y * 1.3f, point.Z * 1.1f)).ToArray();
        NativeSeamPair[] twoShoulders = SelectNativeSeamPairs(unequalBind,
            influences, twoShoulderParents, 3, new[] {1, 2, 6}, Vector3.Zero, 3.2f);
        bool sameNormalizedGaps = original.Length == reordered.Length &&
            original.Zip(reordered, (a, b) => Math.Abs(a.BindGap / 2 - b.BindGap / 3) < 1e-6f).All(equal => equal);
        if (original.Length != 3 || !sameNormalizedGaps ||
            twoShoulders.Length != 3 || missing.Length != 0)
            throw new InvalidDataException("Procedural shoulder seam pair selection changed under bone/vertex reordering, unequal proportions, an extra shoulder link, or absent context.");
        return new
        {
            Pass = true,
            OriginalPairs = original.Length,
            RenamedAndReorderedUnequalRigPairs = reordered.Length,
            ExtraShoulderAnisotropicRigPairs = twoShoulders.Length,
            MissingTrunkContextPairs = missing.Length,
            NormalizedBindGapInvariant = sameNormalizedGaps,
            Scope = "synthetic indices, reordered vertices and joint IDs, uniform and anisotropic unequal proportions, extra shoulder link, absent trunk influence; native asset semantics separately tested"
        };
    }

    private sealed class NativeSeamSideReport
    {
        public string Side { get; set; }
        public float ArmLength { get; set; }
        public int PairCount { get; set; }
        public double MaximumBindGap { get; set; }
        public double MaximumBindGapPerArmLength { get; set; }
        public double MaximumAnimatedGap { get; set; }
        public double MaximumAbsoluteChangeFromBind { get; set; }
        public double MeanAbsoluteChangeFromBind { get; set; }
        public double MaximumAnimatedGapPerArmLength { get; set; }
        public double MaximumChangePerArmLength { get; set; }
        public double MaximumQuarterGapStepPerArmLength { get; set; }
    }

    private sealed class NativeSeamReport
    {
        public string Rig { get; set; }
        public int VertexCount { get; set; }
        public long FiniteNativeVertexSamples { get; set; }
        public NativeSeamSideReport[] Sides { get; set; }
        public List<object> Rows { get; set; }
        public string PairSelection { get; set; }
        public bool FiniteSampledCoverageOnly { get; set; }
    }

    private static NativeSeamReport NativeShoulderSeamMotion(BrawlRig captured, MDL0Node model,
        CHR0Node clip, float[] times)
    {
        var rig = captured.Rig;
        var vertices = model.PolygonList.Cast<MDL0ObjectNode>()
            .SelectMany(polygon => polygon.Vertices).ToArray();
        if (vertices.Length < 1 || vertices.Length > 200000)
            throw new InvalidDataException("Shoulder seam witness needs a bounded native vertex inventory.");
        var boneIndices = captured.Bones.Select((bone, index) => new {bone.Name, index})
            .ToDictionary(item => item.Name, item => item.index, StringComparer.Ordinal);
        int[][] influences = vertices.Select(vertex =>
        {
            var node = vertex.GetMatrixNode();
            if (node is MDL0BoneNode single)
                return boneIndices.TryGetValue(single.Name, out int index) ? new[] {index} : new int[0];
            return node?.Weights?.Where(weight => weight.Weight > 0 && weight.Bone is MDL0BoneNode)
                .Select(weight => boneIndices.TryGetValue(((MDL0BoneNode)weight.Bone).Name,
                    out int index) ? index : -1).Where(index => index >= 0).Distinct().ToArray()
                ?? new int[0];
        }).ToArray();
        model.ApplyCHR(null, 0);
        Vector3[] bind = Vertices(model);
        if (bind.Length != vertices.Length || bind.Any(point =>
            float.IsNaN(point.X) || float.IsNaN(point.Y) || float.IsNaN(point.Z) ||
            float.IsInfinity(point.X) || float.IsInfinity(point.Y) || float.IsInfinity(point.Z)))
            throw new InvalidDataException("The native shoulder bind mesh is invalid.");

        var sidePairs = new Dictionary<string, NativeSeamPair[]>(StringComparer.Ordinal);
        var lengths = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (string side in new[] {"left", "right"})
        {
            string upper = side + ".upperArm", lower = side + ".lowerArm", hand = side + ".hand";
            if (!rig.Roles.ContainsKey(upper) || !rig.Roles.ContainsKey(lower) ||
                !rig.Roles.ContainsKey(hand)) continue;
            int arm = rig.Roles[upper], parent = rig.Joints[arm].Parent;
            float length = Vector3.Distance(rig.Rest.Point(upper), rig.Rest.Point(lower)) +
                Vector3.Distance(rig.Rest.Point(lower), rig.Rest.Point(hand));
            if (!(length > 0) || float.IsNaN(length) || float.IsInfinity(length))
                throw new InvalidDataException("The shoulder seam has no finite positive arm scale.");
            lengths.Add(side, length);
            int[] trunk = new[] {"pelvis", "spine", "chest", "neck"}
                .Where(rig.Roles.ContainsKey).Select(role => rig.Roles[role])
                .Concat(parent < 0 ? new int[0] : new[] {parent}).Distinct().ToArray();
            Vector3 pivot = rig.Rest.Point(upper);
            int[] parents = rig.Joints.Select(joint => joint.Parent).ToArray();
            sidePairs.Add(side, SelectNativeSeamPairs(bind, influences, parents,
                arm, trunk, pivot, length));
        }

        var rows = new List<object>();
        var maximum = sidePairs.Keys.ToDictionary(side => side, side => 0d);
        var maximumFromBind = sidePairs.Keys.ToDictionary(side => side, side => 0d);
        var maximumQuarterStep = sidePairs.Keys.ToDictionary(side => side, side => 0d);
        var previousGaps = sidePairs.ToDictionary(side => side.Key,
            side => new double[side.Value.Length]);
        var totalFromBind = sidePairs.Keys.ToDictionary(side => side, side => 0d);
        var sampleCounts = sidePairs.Keys.ToDictionary(side => side, side => 0);
        int timeIndex = 0;
        try
        {
            foreach (float time in times)
            {
                model.ApplyCHR(clip, time + 1);
                Vector3[] posed = Vertices(model);
                if (posed.Length != bind.Length || posed.Any(point =>
                    float.IsNaN(point.X) || float.IsNaN(point.Y) || float.IsNaN(point.Z) ||
                    float.IsInfinity(point.X) || float.IsInfinity(point.Y) || float.IsInfinity(point.Z)))
                    throw new InvalidDataException("The native shoulder mesh produced invalid posed vertices.");
                var sideRows = new List<object>();
                foreach (var side in sidePairs)
                {
                    double maxGap = 0, maxChange = 0, maxStep = 0, sumGap = 0, sumChange = 0;
                    for (int pairIndex = 0; pairIndex < side.Value.Length; pairIndex++)
                    {
                        NativeSeamPair pair = side.Value[pairIndex];
                        double gap = Vector3.Distance(posed[pair.ArmVertex], posed[pair.TorsoVertex]);
                        double change = Math.Abs(gap - pair.BindGap);
                        if (timeIndex > 0)
                        {
                            double step = Math.Abs(gap - previousGaps[side.Key][pairIndex]);
                            maxStep = Math.Max(maxStep, step);
                            maximumQuarterStep[side.Key] = Math.Max(maximumQuarterStep[side.Key], step);
                        }
                        previousGaps[side.Key][pairIndex] = gap;
                        maxGap = Math.Max(maxGap, gap);
                        maxChange = Math.Max(maxChange, change);
                        sumGap += gap;
                        sumChange += change;
                        maximum[side.Key] = Math.Max(maximum[side.Key], gap);
                        maximumFromBind[side.Key] = Math.Max(maximumFromBind[side.Key], change);
                        totalFromBind[side.Key] += change;
                        sampleCounts[side.Key]++;
                    }
                    sideRows.Add(new
                    {
                        Side = side.Key, PairCount = side.Value.Length,
                        MaximumGap = maxGap,
                        MeanGap = side.Value.Length == 0 ? 0 : sumGap / side.Value.Length,
                        MaximumAbsoluteChangeFromBind = maxChange,
                        MeanAbsoluteChangeFromBind = side.Value.Length == 0 ? 0 :
                            sumChange / side.Value.Length,
                        MaximumGapPerArmLength = maxGap / lengths[side.Key],
                        MaximumChangePerArmLength = maxChange / lengths[side.Key],
                        MaximumQuarterGapStepPerArmLength = maxStep / lengths[side.Key]
                    });
                }
                rows.Add(new {Time = time, Sides = sideRows});
                timeIndex++;
            }
        }
        finally { model.ApplyCHR(null, 0); }
        return new NativeSeamReport
        {
            Rig = captured.Fingerprint,
            VertexCount = bind.Length,
            FiniteNativeVertexSamples = (long)bind.Length * times.Length,
            Sides = sidePairs.Select(side => new NativeSeamSideReport
            {
                Side = side.Key,
                ArmLength = lengths[side.Key],
                PairCount = side.Value.Length,
                MaximumBindGap = side.Value.Select(pair => (double)pair.BindGap).DefaultIfEmpty(0).Max(),
                MaximumBindGapPerArmLength = side.Value.Select(pair => (double)pair.BindGap)
                    .DefaultIfEmpty(0).Max() / lengths[side.Key],
                MaximumAnimatedGap = maximum[side.Key],
                MaximumAbsoluteChangeFromBind = maximumFromBind[side.Key],
                MeanAbsoluteChangeFromBind = sampleCounts[side.Key] == 0 ? 0 :
                    totalFromBind[side.Key] / sampleCounts[side.Key],
                MaximumAnimatedGapPerArmLength = maximum[side.Key] / lengths[side.Key],
                MaximumChangePerArmLength = maximumFromBind[side.Key] / lengths[side.Key],
                MaximumQuarterGapStepPerArmLength = maximumQuarterStep[side.Key] / lengths[side.Key]
            }).ToArray(),
            Rows = rows,
            PairSelection = "positive native skin influence; arm subtree versus local trunk/shoulder-parent influence; nearest bind neighbor within 0.2 arm length, at most 512 per side",
            FiniteSampledCoverageOnly = true
        };
    }
}
