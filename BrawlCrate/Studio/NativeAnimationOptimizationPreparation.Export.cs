using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BrawlCrate.Studio
{
    internal static partial class NativeAnimationOptimizationPreparation
    {
        // Inputs are independently native-qualified owned copies. The caller
        // retains them until this search returns: a larger precise encoding can
        // produce a smaller legal codec result. This helper grants no proof.
        internal static async Task<NativeAnimationOptimizationResult> SearchVerifiedExportAlternatives(
            IReadOnlyList<NativeAnimationOptimizationResult> precise,
            Func<NativeAnimationOptimizationResult, CancellationToken, Task<NativeAnimationOptimizationResult>> encode,
            Func<bool> allowanceAvailable, Action<string> note, CancellationToken cancellation)
        {
            if (precise == null || precise.Count == 0 || precise.Any(value => value?.Candidate == null) || encode == null || allowanceAvailable == null)
                throw new ArgumentException("Export search needs complete independently verified alternatives and existing allowance.");
            bool Smaller(NativeAnimationOptimizationResult candidate, NativeAnimationOptimizationResult current) =>
                candidate.AfterBytes < current.AfterBytes || candidate.AfterBytes == current.AfterBytes && candidate.AfterKeys < current.AfterKeys;
            var order = precise.OrderBy(value => value.AfterBytes).ThenBy(value => value.AfterKeys).ToArray();
            var best = order[0];
            try
            {
                foreach (var original in order)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (!allowanceAvailable())
                    { note?.Invoke("Remaining verified curve alternative codec searches skipped: shared allowance exhausted; best verified candidate retained."); break; }
                    NativeAnimationOptimizationResult packed = null;
                    try
                    {
                        packed = await encode(original, cancellation).ConfigureAwait(false);
                        cancellation.ThrowIfCancellationRequested();
                        if (packed?.Candidate == null) throw new InvalidDataException("Codec search returned no independently verified complete candidate.");
                        if (precise.Contains(packed))
                        { packed = null; throw new InvalidDataException("Codec search must return an independent owned result, not a retained precise alternative."); }
                        note?.Invoke("Deferred codec search for " + original.AfterBytes + "-byte precise alternative: verified " + packed.AfterBytes + " bytes.");
                        if (Smaller(packed, best))
                        {
                            if (!precise.Contains(best)) best.Dispose();
                            best = packed; packed = null;
                        }
                    }
                    catch (InvalidDataException error) when (error.Data.Contains("OptimizationQuality") || error.Data.Contains("OptimizationSize") || error.Data.Contains("OptimizationBudget"))
                    {
                        note?.Invoke("Deferred codec search: " + error.Message + "; best independently verified curve alternative retained.");
                        if (error.Data.Contains("OptimizationBudget")) break;
                    }
                    finally { packed?.Dispose(); }
                }
                return best;
            }
            catch { if (!precise.Contains(best)) best.Dispose(); throw; }
        }
    }
}
