using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BrawlCrate.Studio
{
    internal sealed class NativeAnimationOptimizationChoice : IDisposable
    {
        internal NativeAnimationOptimizationResult Result;
        internal bool HighQuality;
        internal double PrecisionScale;
        internal string Report;
        public void Dispose() { Result?.Dispose(); Result = null; }
    }

    // Preparation owns each completed candidate until a single choice is returned.
    // Attempts use one immutable baseline; callers share proof/native work counters.
    internal static class NativeAnimationOptimizationPreparation
    {
        internal static async Task<NativeAnimationOptimizationChoice> Prepare(bool highQuality, long currentBytes,
            Func<bool, double, Task<NativeAnimationOptimizationResult>> attempt, CancellationToken cancellation,
            bool exploreSelectiveFallbacks=false, bool preferTightPrecision=false)
        {
            if (currentBytes < 0 || attempt == null) throw new ArgumentException("Invalid native optimization preparation.");
            var choice = new NativeAnimationOptimizationChoice();
            var notes = new List<string>();
            try
            {
                foreach (double scale in highQuality ? (preferTightPrecision ? new[] { 0d, .125, .25, .5, 1d } : new[] { 0d, 1d, .5, .25, .125 }) : new[] { 0d })
                {
                    cancellation.ThrowIfCancellationRequested();
                    bool approximate = scale > 0;
                    string label = approximate ? "High quality scalar precision " + scale.ToString("G", System.Globalization.CultureInfo.InvariantCulture) : "Exact cleanup";
                    NativeAnimationOptimizationResult verified = null;
                    try
                    {
                        verified = await attempt(approximate, approximate ? scale : 1);
                        cancellation.ThrowIfCancellationRequested();
                        if (verified == null) throw new InvalidDataException("Native optimization returned no completed verification receipt.");
                        if (verified.AfterBytes > currentBytes)
                        {
                            if (!highQuality) throw new InvalidDataException("The verified animation would grow relative to the current export. No changes were applied.");
                            notes.Add(label + ": rejected; larger than the current animation.");
                            continue;
                        }
                        bool useful = verified.Candidate != null && verified.HasChanges;
                        bool selective=!string.IsNullOrEmpty(verified.ReductionSelectionReport);
                        notes.Add(label + (verified.UsedApproximateStorage ? " + bounded storage" : "") + ": fully verified, " + verified.AfterKeys + " keys / " + verified.AfterBytes + " bytes" + (useful ? "." : "; unchanged.") );
                        if (useful && (choice.Result == null || verified.AfterBytes < choice.Result.AfterBytes ||
                            verified.AfterBytes == choice.Result.AfterBytes && verified.AfterKeys < choice.Result.AfterKeys))
                        {
                            choice.Result?.Dispose(); choice.Result = verified; verified = null;
                            choice.HighQuality = approximate || choice.Result.UsedApproximateStorage; choice.PrecisionScale = approximate ? scale : 1;
                        }
                        // A tight-first generated search establishes a safe
                        // fallback, then uses its remaining SHARED budget to
                        // seek a smaller verified candidate. First success is
                        // not proof that the file cannot be reduced further.
                        if (approximate && !preferTightPrecision && (!exploreSelectiveFallbacks || !selective)) break;
                    }
                    catch (InvalidDataException error) when (highQuality &&
                        (error.Data.Contains("OptimizationQuality") || error.Data.Contains("OptimizationSize") || error.Data.Contains("OptimizationBudget")))
                    {
                        notes.Add(label + ": " + error.Message);
                        if (error.Data.Contains("OptimizationBudget")) break;
                    }
                    finally { verified?.Dispose(); }
                }
                cancellation.ThrowIfCancellationRequested();
                choice.Report = string.Join("\r\n", notes) + "\r\nChosen: " + (choice.Result == null ? "unchanged current animation" :
                    choice.HighQuality ? "verified High quality candidate" : highQuality ? "verified Exact fallback" : "verified Exact candidate") + ".";
                return choice;
            }
            catch { choice.Dispose(); throw; }
        }
    }
}
