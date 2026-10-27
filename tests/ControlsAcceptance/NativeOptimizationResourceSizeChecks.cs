using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;

internal static partial class Program
{
    static void NativeOptimizationResourceSizeChecks(MDL0Node model, string stage, string[] names, double length)
    {
        string containerPath = Path.Combine(stage, "compact-resource.brres"), precisePath = Path.Combine(stage, "precise-resource.chr0");
        using (var container = new BRRESNode { Name = "Independent compact native container" })
        {
            var fixture = OptimizationFixture(names[0], "Compact native resource fixture"); fixture.FrameCount = 17;
            var entry = (CHR0EntryNode)fixture.Children[0]; entry.Keyframes.PreserveInterpolation = false;
            OptimizationReplace(entry, 3, Enumerable.Range(0, 17).Select(i => OptimizationKey(i, i * .125f, .125f)).ToArray());
            container.GetOrCreateFolder<CHR0Node>().AddChild(fixture); container.Export(containerPath);
        }
        string originalHash = FighterProject.HashFile(containerPath);
        using (var owner = NodeFactory.FromFile(null, containerPath))
        {
            var compact = OptimizationWalk(owner).OfType<CHR0Node>().Single();
            long stored = compact.WorkingUncompressed.Length;
            long offset = ((IntPtr)compact.WorkingUncompressed.Address).ToInt64() - ((IntPtr)owner.WorkingUncompressed.Address).ToInt64();
            byte[] containerBytes = File.ReadAllBytes(containerPath); int header = checked((int)offset);
            long declared = ((long)containerBytes[header + 4] << 24) | ((long)containerBytes[header + 5] << 16) |
                ((long)containerBytes[header + 6] << 8) | containerBytes[header + 7];
            Check(!compact.IsDirty && !compact.IsBranch && offset >= 0 && offset + declared <= containerBytes.Length && declared == stored,
                "Native size: clean embedded CHR0 block length independently matches its big-endian header size and BRRES byte range");
            var track = OptimizationTrack((CHR0EntryNode)compact.Children[0]); track.Channels[3] = track.Channels[3].Where(k => k.Frame != 8).ToArray();
            var options = OptimizationQualityOptions(names, length, true);
            using (var verified = NativeAnimationOptimization.Prepare(model, compact, new[] { track }, options, stage))
            {
                Check(verified.AfterKeys < verified.BeforeKeys && verified.AfterBytes < verified.BeforeBytes &&
                    verified.CandidateResourceBytes < verified.BeforeResourceBytes && verified.CandidateResourceBytes > stored && verified.CandidateResourceBytes < verified.AfterBytes,
                    "Native size: fewer I12 keys and smaller current export can still grow the actual compact source resource block");
                File.WriteAllText(Path.Combine(stage, "compact-resource-unit-diagnostic.txt"), "Clean original embedded block " + stored +
                    "; current precise block " + verified.BeforeResourceBytes + "; candidate block " + verified.CandidateResourceBytes +
                    "; standalone candidate " + verified.AfterBytes + " bytes. External strings belong only to the standalone total.");
            }
            var budget = new NativeAnimationOptimizationBudget(); options.Budget = budget; options.OriginalStoredResourceBytes = stored;
            bool refused = false;
            try { using (var result = NativeAnimationOptimization.Prepare(model, compact, new[] { track }, options, stage)) { } }
            catch (InvalidDataException error)
            {
                refused = Equals(error.Data["OptimizationSize"], true) && !error.Data.Contains("OptimizationQuality") &&
                    Convert.ToInt64(error.Data["OriginalInputResourceBytes"]) == stored && Convert.ToInt64(error.Data["CandidateResourceBytes"]) > stored &&
                    Convert.ToInt64(error.Data["AfterBytes"]) < Convert.ToInt64(error.Data["BeforeBytes"]) && budget.Samples == 0;
            }
            Check(refused, "Native size: actual compact embedded-block growth is refused before pose work even when the standalone candidate shrinks");

            using (var copy = NativeAnimationOptimization.Copy(compact)) copy.ExportChecked(precisePath);
            using (var preciseOwner = NodeFactory.FromFile(null, precisePath))
            {
                var precise = (CHR0Node)preciseOwner;
                var preciseOptions = OptimizationQualityOptions(names, length, true);
                preciseOptions.OriginalStoredResourceBytes = precise.WorkingUncompressed.Length;
                preciseOptions.OriginalStoredBytes = new FileInfo(precisePath).Length;
                using (var verified = NativeAnimationOptimization.Prepare(model, precise, new[] { track }, preciseOptions, stage))
                    Check(verified.Candidate != null && verified.CandidateResourceBytes <= preciseOptions.OriginalStoredResourceBytes.Value &&
                        verified.AfterBytes > preciseOptions.OriginalStoredResourceBytes.Value && verified.OriginalInputResourceBytes == preciseOptions.OriginalStoredResourceBytes,
                        "Native size: matching resource-block caps accept smaller payloads despite larger standalone totals from external strings");
            }
            options.OriginalStoredResourceBytes = 0; bool invalid = false;
            try { using (var result = NativeAnimationOptimization.Prepare(model, compact, new[] { track }, options, stage)) { } }
            catch (InvalidDataException error) { invalid = !error.Data.Contains("OptimizationSize") && !error.Data.Contains("OptimizationQuality"); }
            Check(invalid && FighterProject.HashFile(containerPath) == originalHash,
                "Native size: invalid block metadata is a terminal input error and compact original bytes remain unchanged");
        }
    }
}
