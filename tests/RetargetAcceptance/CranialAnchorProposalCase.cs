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
    // Read-only proposal and actual native MDL0 export/reopen witness. The
    // exported fixture remains under the caller's ignored local output folder.
    private static int CranialAnchorProposalCase(string modelPath, string output)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Choose a new or empty output directory.");
        Directory.CreateDirectory(output);
        string before = FighterProject.HashFile(modelPath);
        using (ResourceNode sourceFile = NodeFactory.FromFile(null, modelPath))
        {
            MDL0Node model = CranialMainModel(sourceFile);
            var characterization = BrawlRetargeter.Characterize(model);
            CranialAnchorProposal proposal = BrawlRetargeter.ProposeCranialAnchor(model, characterization);
            string exportedPath = Path.Combine(output, "native-model-reopen.mdl0");
            model.Export(exportedPath);
            using (ResourceNode reopenedFile = NodeFactory.FromFile(null, exportedPath))
            {
                MDL0Node reopened = CranialMainModel(reopenedFile);
                var reopenedMap = BrawlRetargeter.Characterize(reopened);
                CranialAnchorProposal second = BrawlRetargeter.ProposeCranialAnchor(reopened, reopenedMap);
                bool digestApplicable = proposal.RigAndSkinDigest != null;
                bool sameDigest = string.Equals(proposal.RigAndSkinDigest, second.RigAndSkinDigest, StringComparison.Ordinal);
                bool sameCandidate = proposal.Available == second.Available &&
                    proposal.NeckBoneName == second.NeckBoneName &&
                    Vector3.Distance(proposal.BindLocalPoint, second.BindLocalPoint) <= 0.000001f &&
                    Math.Abs(proposal.Radius - second.Radius) <= 0.000001f &&
                    proposal.CandidateVertexIndices.Count == second.CandidateVertexIndices.Count;
                var rig = BrawlRetargeter.Capture(model);
                CranialHeadSkinMeasurement directHead = BrawlRetargeter.MeasureMappedHeadSkin(model, rig);
                float? headPatchRms = null;
                int headPatchPoints = 0;
                if (rig.Rig.Surfaces.TryGetValue("head", out ContactSurface headSurface))
                {
                    Vector3[] points = headSurface.Points.Select(p => p.Evaluate(rig.Rig.Rest)).ToArray();
                    headPatchPoints = points.Length;
                    if (points.Length > 0)
                    {
                        Vector3 centroid = points.Aggregate(Vector3.Zero, (sum, p) => sum + p) / points.Length;
                        headPatchRms = (float)Math.Sqrt(points.Average(p => Vector3.DistanceSquared(p, centroid)));
                    }
                }
                string after = FighterProject.HashFile(modelPath);
                bool passed = before == after && sameDigest && sameCandidate;
                var receipt = new
                {
                    Passed = passed,
                    ModelSha256 = before,
                    OriginalUnchanged = before == after,
                    Model = model.Name,
                    ProposalAvailable = proposal.Available,
                    proposal.RejectionReason,
                    proposal.NeckIndex,
                    proposal.NeckBoneName,
                    proposal.RigAndSkinDigest,
                    proposal.DecodedVertexCount,
                    proposal.RepresentedWeightCount,
                    proposal.BindLocalPoint,
                    proposal.Radius,
                    CandidateVertices = proposal.CandidateVertexIndices.Count,
                    Patches = proposal.CandidatePatches.Select(p => new
                    {
                        p.RootJointIndex, p.RootJointName, p.DirectNeckInfluence, p.InfluencedVertices,
                        p.MajorityVertices, p.WeightedVertexMass, p.BindCentroid,
                        p.BindCentroidInNeckLocal, p.BindRmsRadius, p.BindMaximumRadius
                    }).ToArray(),
                    ReopenedDigest = second.RigAndSkinDigest,
                    RigAndSkinDigestApplicable = digestApplicable,
                    RigAndSkinDigestStable = digestApplicable && sameDigest,
                    ReopenedEquivalent = sameDigest && sameCandidate,
                    LegacyHeadRadius = rig.Rig.HeadRadius,
                    DirectHeadSkinRmsRadius = directHead.Available ? (float?)directHead.Radius : null,
                    DirectHeadSkinMass = directHead.WeightedVertexMass,
                    DirectHeadMajorityVertices = directHead.MajorityVertices,
                    DirectHeadSkinUnavailable = directHead.RejectionReason,
                    HeadPatchRmsRadius = headPatchRms,
                    HeadPatchPoints = headPatchPoints,
                    ProvisionalJointHead = rig.ProvisionalHeadForExactNativeCopy
                };
                File.WriteAllText(Path.Combine(output, "cranial-anchor-proposal.json"),
                    new JavaScriptSerializer {MaxJsonLength = 16000000}.Serialize(receipt));
                if (!passed) throw new InvalidDataException("The cranial proposal changed across native export/reopen or its input was modified.");
                Console.WriteLine("Cranial proposal: " + (proposal.Available ? "reviewable" : proposal.RejectionReason) +
                    "; native model export/reopen proposal state stable: " + (sameDigest && sameCandidate) +
                    (digestApplicable ? "; skin digest stable: " + sameDigest : "; no candidate digest"));
                return 0;
            }
        }
    }

    private static MDL0Node CranialMainModel(ResourceNode root)
    {
        MDL0Node[] models = Walk(root).OfType<MDL0Node>().OrderByDescending(m => m.NumVertices).ToArray();
        if (models.Length == 0 || models[0].NumVertices <= 0 ||
            models.Length > 1 && models[1].NumVertices == models[0].NumVertices)
            throw new InvalidDataException("Choose an exact native model with one unambiguous highest-vertex body member.");
        return models[0];
    }
}
