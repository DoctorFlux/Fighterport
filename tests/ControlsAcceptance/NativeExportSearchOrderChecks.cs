using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal static partial class Program
{
    // Orchestration-only numeric fixtures. These results deliberately contain
    // no motion and do not manufacture native quality/codec evidence.
    static async Task NativeExportSearchOrderChecks()
    {
        NativeAnimationOptimizationResult Result(string name, int bytes) => new NativeAnimationOptimizationResult {
            Candidate = new CHR0Node { Name = name, Version = 4, FrameCount = 1 }, AfterBytes = bytes, AfterKeys = bytes };
        InvalidDataException Failure(string category) { var e = new InvalidDataException(category); e.Data[category] = true; return e; }
        foreach (string scenario in new[] { "larger-precise-smaller-codec", "first-fails-last-wins", "later-budget-fallback", "no-allowance", "no-useful-codec" })
        using (var first = Result("small precise", 100))
        using (var last = Result("larger precise", 120))
        {
            var visits = new List<string>(); var notes = new List<string>(); NativeAnimationOptimizationResult rejected = null;
            var selected = await NativeAnimationOptimizationPreparation.SearchVerifiedExportAlternatives(new[] { last, first },
                (value, token) =>
                {
                    Check(first.Candidate != null && last.Candidate != null, "Deferred export keeps both native-qualified curve alternatives alive during codec search");
                    visits.Add(value.Candidate.Name);
                    if (scenario == "first-fails-last-wins" && ReferenceEquals(value, first)) throw Failure("OptimizationQuality");
                    if (scenario == "later-budget-fallback" && ReferenceEquals(value, last)) throw Failure("OptimizationBudget");
                    if (scenario == "no-useful-codec") { rejected = Result("same bytes", ReferenceEquals(value, first) ? 100 : 120); return Task.FromResult(rejected); }
                    return Task.FromResult(Result("independent verified encoding", ReferenceEquals(value, first) ? 95 : 80));
                }, () => scenario != "no-allowance", notes.Add, CancellationToken.None);
            try
            {
                int expected = scenario == "later-budget-fallback" ? 95 : scenario == "no-allowance" || scenario == "no-useful-codec" ? 100 : 80;
                Check(selected.AfterBytes == expected && first.Candidate != null && last.Candidate != null &&
                    (scenario == "no-allowance" ? visits.Count == 0 : visits[0] == "small precise"),
                    "Deferred export retains the smallest actually verified visited result without renewing allowance / " + scenario);
                if (scenario == "larger-precise-smaller-codec" || scenario == "first-fails-last-wins")
                    Check(visits.Count == 2 && visits[1] == "larger precise", "The last/larger precise alternative still gets a codec opportunity after earlier success/failure");
                if (scenario == "no-useful-codec") Check(rejected.Candidate == null, "Non-winning independent codec copies are disposed while precise fallbacks stay owned");
            }
            finally { if (!ReferenceEquals(selected, first) && !ReferenceEquals(selected, last)) selected.Dispose(); }
        }
        using (var precise = Result("cancelled operation retains caller-owned master", 100))
        using (var cancellation = new CancellationTokenSource())
        {
            NativeAnimationOptimizationResult pending = null; bool stopped = false;
            try { await NativeAnimationOptimizationPreparation.SearchVerifiedExportAlternatives(new[] { precise }, (value, token) => {
                pending = Result("not published", 50); cancellation.Cancel(); return Task.FromResult(pending);
            }, () => true, null, cancellation.Token); } catch (OperationCanceledException) { stopped = true; }
            Check(stopped && precise.Candidate != null && pending.Candidate == null, "Cancellation discards an independent pending codec result and preserves native-qualified alternatives");
        }
        var attempts = new NativeCompactAttemptBudget(); attempts.Consume(22); attempts.Consume(2); bool rejectedAttempt = false;
        try { attempts.Consume(1); } catch (InvalidDataException) { rejectedAttempt = true; }
        Check(attempts.Used == 24 && attempts.Remaining == 0 && rejectedAttempt, "All deferred alternatives share the same unchanged24 complete-attempt ceiling");
        var evidence = new[] {
            new NativeGeneratedPairSite { Bone = "anonymous", Channel = 3, Frame = .5f },
            new NativeGeneratedPairSite { Bone = "anonymous", Channel = 3, Frame = .5f },
            new NativeGeneratedPairSite { Bone = "another", Channel = 4, Frame = 1 },
            new NativeGeneratedPairSite { Bone = "anonymous", Channel = 3, Frame = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0) } };
        var work = new NativeDenseProofBudget(); var index = NativeDensePairEvidenceIndex.Create(evidence, work, CancellationToken.None);
        evidence[0].Frame = 9;
        Check(work.Work == 4 && index.ForBone("anonymous").Length == 3 && index.ForBone("anonymous")[0].Frame == .5f &&
            index.ForBone("anonymous")[1].Frame == .5f && ReplayFloatBits(index.ForBone("anonymous")[2].Frame) == int.MinValue && index.ForBone("missing").Length == 0,
            "Pair index charges the global scan once and freezes exact duplicate/conflicting/signed-zero evidence without name inference");
        bool exhausted = false;
        try { NativeDensePairEvidenceIndex.Create(evidence, new NativeDenseProofBudget(amount => false), CancellationToken.None); }
        catch (InvalidDataException error) { exhausted = error.Data.Contains("OptimizationBudget"); }
        Check(exhausted, "Incomplete pair-index preparation is a shared-budget failure, never partially published permission");
        var capWork = new NativeDenseProofBudget(); bool capped = false;
        try { NativeDensePairEvidenceIndex.Create(new NativeGeneratedPairSite[2000001], capWork, CancellationToken.None); }
        catch (InvalidDataException) { capped = true; }
        Check(capped && capWork.Work == 0, "Pair indexing retains the global two-million-record cap before allocating grouped evidence");
    }
}
