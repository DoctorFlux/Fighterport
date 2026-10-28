using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private static long GeneratedAnimationBytes(CHR0Node clip, string stage)
        {
            string path=Path.Combine(stage,Guid.NewGuid().ToString("N")+".chr0");
            try { using(var copy=NativeAnimationOptimization.Copy(clip)) copy.ExportChecked(path); return new FileInfo(path).Length; }
            finally { if(File.Exists(path)) File.Delete(path); }
        }
        private sealed class FrozenOptimizationAnalysis : IDisposable
        {
            internal NativeAnimationOptimizationChoice Choice;
            internal OptimizationOutput Output;
            internal NativeAnimationOptimizationBudget Budget;
            internal long Work;internal int Spans;internal string[] RetainedDense;
            public void Dispose()=>Choice?.Dispose();
        }
        // Standalone and workflow exports use this same offline pipeline.
        // Models and animations passed here are independently owned snapshots.
        private async Task<FrozenOptimizationAnalysis> PrepareFrozenOptimization(MDL0Node model, CHR0Node before, CHR0Node baseline,
            string modelPath,string controls,string stage,IEnumerable<string> exactBones,IEnumerable<string> protection,
            int[] fitJoins,TransitionProtected[] ranges,bool protectionKnown,bool highQuality,bool smallFile,bool compact,
            long? storedBytes,long? storedResourceBytes,Action current,CancellationToken token,float[] generatedPairFrames=null,NativeGeneratedPairSite[] generatedPairSites=null,NativeExportOptimizationSettings exportSettings=null)
        {
            exportSettings=exportSettings?.Clone(); exportSettings?.Validate();
            bool exactFitJoins=exportSettings!=null;
            var json=new JavaScriptSerializer{MaxJsonLength=256*1024*1024,RecursionLimit=200};
            var validationModel=model;double length=NativeAnimationOptimization.GetBindBoundsDiagonal(model);
            var exact=new HashSet<string>(exactBones,StringComparer.Ordinal);var protectedBones=new HashSet<string>(protection,StringComparer.Ordinal);
            var mandatory=new HashSet<int>(fitJoins);
            var generatedPairs=(generatedPairFrames??new float[0]).Distinct().OrderBy(frame=>frame).ToArray();
            var generatedSites=(generatedPairSites??new NativeGeneratedPairSite[0]).Select(site=>new NativeGeneratedPairSite{Bone=site.Bone,Channel=site.Channel,Frame=site.Frame}).ToArray();
            foreach(var bone in model.AllBones)if(Enumerable.Range(0,3).Any(c=>bone.BindState[c]==0))exact.Add(bone.Name);
            AnimationControlClient client=null;
            using(var registration=token.Register(()=>client?.Dispose()))
            try
            {
                long work = 0; int spans = 0;
                var budget = new NativeAnimationOptimizationBudget();
                var referenceSamples = new NativeOptimizationReferenceSamples(validationModel, baseline);
                // Dense proposals and Core certificates share the
                // existing operation work ceiling across all retries.
                var denseProofBudget = new NativeDenseProofBudget(amount =>
                {
                    token.ThrowIfCancellationRequested();
                    if (amount < 0 || amount > 2000000 - work) return false;
                    work += amount;
                    return true;
                });
                var receipts = new Dictionary<NativeAnimationOptimizationResult, OptimizationOutput>();
                ControlTrack[] exactReductionTracks = null;
                var wire = OptimizationWireTracks(baseline);
                var nativeBones = model.AllBones.ToDictionary(b => b.Name, StringComparer.Ordinal);
                // An imported baked rotation owns a complete stored
                // terminal sample and an interpolation context beyond
                // the native projection. Preserve it as a whole; other
                // eligible target tracks can still be optimized.
                var retainedDense = baseline.Children.OfType<CHR0EntryNode>()
                    .Where(entry => nativeBones.ContainsKey(entry.Name))
                    .Where(entry =>
                    {
                        var storage = PreciseCHR0EntryNode.CaptureStorage(entry);
                        return storage != null && storage.MatchesGroup(entry.Keyframes, 1) &&
                            Enumerable.Range(3, 3).Any(channel => storage[channel]?.IsDense == true);
                    }).Select(entry => entry.Name).ToArray();
                var analysisProtected = new HashSet<string>(protectedBones, StringComparer.Ordinal);
                analysisProtected.UnionWith(retainedDense);
                var bindPoses = baseline.Children.OfType<CHR0EntryNode>().Select(entry => new { Entry = entry, Bone = nativeBones.TryGetValue(entry.Name, out var bound) ? bound : null })
                    .Where(pair => NativeAnimationOptimization.CanRemoveBindTrack(pair.Entry, pair.Bone))
                    .Select(pair => new { pair.Bone.Name, Bits = Enumerable.Range(0, 9).Select(c => OptimizationFloatBits(pair.Bone.BindState[c])).ToArray() })
                    .Select(pair => new { Bone = pair.Name, pair.Bits }).ToArray();
                async Task<NativeAnimationOptimizationResult> Attempt(bool approximate, double precision)
                {
                    token.ThrowIfCancellationRequested(); current();
                    if (work >= 2000000 || spans >= 100000)
                    {
                        var exhausted = new InvalidDataException("Aggregate Core proof budget exhausted; only an already verified candidate can be offered.");
                        exhausted.Data["OptimizationBudget"] = "Core work/candidate spans"; throw exhausted;
                    }
                    string inputPath = Path.Combine(stage, "input-" + Guid.NewGuid().ToString("N") + ".json"), outputPath = Path.Combine(stage, "output-" + Guid.NewGuid().ToString("N") + ".json");
                    try
                    {
                        var input = new { baseline.Name, SampleCount = baseline.FrameCount, baseline.Loop, HighQuality = approximate, SmallFile = approximate && smallFile, ExplicitBounds = exportSettings, GeneratedPairFrames = generatedPairs, GeneratedPairSites = generatedSites, LengthScale = length,
                            PrecisionScale = precision, MaximumWork = 2000000 - work, MaximumCandidateSpans = 100000 - spans,
                            Tracks = wire, ExactBones = exact.ToArray(), ProtectedBones = analysisProtected.ToArray(), MandatoryFrames = approximate && smallFile && !exactFitJoins ? new int[0] : mandatory.OrderBy(f => f).ToArray(),
                            FitJoinFrames = approximate && smallFile && !exactFitJoins ? mandatory.OrderBy(f => f).ToArray() : new int[0],
                            ProtectedRanges = ranges, ProtectionKnown = protectionKnown, BindPoses = bindPoses };
                        await Task.Run(() => { token.ThrowIfCancellationRequested(); File.WriteAllText(inputPath, json.Serialize(input)); }, token);
                        token.ThrowIfCancellationRequested(); current();
                        if (client == null) client = new AnimationControlClient(); token.ThrowIfCancellationRequested(); var session = client; OptimizationPrepares++;
                        var reply = await session.Send(new { Operation = "animation-optimize-prepare", Model = modelPath, State = controls, Optimization = new { InputPath = inputPath, OutputPath = outputPath } });
                        token.ThrowIfCancellationRequested(); current();
                        if (reply.OptimizationPath != outputPath || new FileInfo(outputPath).Length > 256L * 1024 * 1024) throw new InvalidDataException("Invalid bounded optimization reply.");
                        var output = await Task.Run(() => json.Deserialize<OptimizationOutput>(File.ReadAllText(outputPath)), token);
                        if (output?.Tracks == null || output.Report == null || output.Report.Work < 0 || output.Report.Work > 2000000 - work ||
                            output.Report.CandidateSpans < 0 || output.Report.CandidateSpans > 100000 - spans) throw new InvalidDataException("Invalid aggregate optimization proof receipt.");
                        work += output.Report.Work; spans += output.Report.CandidateSpans;
                        if (output.Report.ProbeLimitReached)
                        {
                            var exhausted = new InvalidDataException("Complete Core witness coverage exceeds the operation budget. No partial candidate can be offered.");
                            exhausted.Data["OptimizationBudget"] = "Core witness coverage"; throw exhausted;
                        }
                        var tracks = output.Tracks.Select(t => new ControlTrack { Bone = t.Bone, Channels = t.Channels.Select(c => c.Select(k => new ControlKey { Frame = OptimizationTime(k), Value = OptimizationFloat(k.ValueBits), Tangent = OptimizationFloat(k.TangentBits) }).ToArray()).ToArray() }).ToArray();
                        // Retain proof/protection receipts, not a second
                        // raw-key object graph for every retry.
                        output.Tracks = null;
                        var options = new NativeAnimationOptimizationOptions { ExactOnly = !approximate, SmallFile = approximate && smallFile, ExportSettings = exportSettings, GeneratedPairFrames = generatedPairs, GeneratedPairSites = generatedSites, LengthScale = length, OriginalStoredBytes = storedBytes, OriginalStoredResourceBytes = storedResourceBytes,
                            TargetBones = model.AllBones.Select(b => b.Name).ToArray(), ProtectedBones = output.ProtectedBones, ExactBones = output.ExactBones,
                            BindPoseCleanupBones = output.RemovedBindTracks, DeferSizeValidation = compact,
                            MandatoryFrames = approximate && smallFile && !exactFitJoins ? new int[0] : mandatory.ToArray(),
                            FitJoinFrames = approximate && smallFile && !exactFitJoins ? mandatory.ToArray() : new int[0], ProbeFrames = output.Report.ProbeFrames, Budget = budget, ReferenceSamples = referenceSamples,
                            // Thorough explores more selective repairs under
                            // the SAME shared proof/native work budgets. It
                            // does not increase any motion error allowance.
                            ReductionAttemptLimit=exportSettings?.SearchEffort==NativeExportSearchEffort.Thorough?8:2 };
                        OptimizationNativeChecks++;
                        var verified = await Task.Run(() => approximate
                            ? NativeAnimationOptimization.PrepareReduction(validationModel, baseline, tracks, exactReductionTracks, options, stage, token)
                            : NativeAnimationOptimization.Prepare(validationModel, baseline, tracks, options, stage, token), token);
                        // Reuse the already allocated, independently
                        // verified Exact wire for partial HQ fallback.
                        // Its keys remain immutable within this Analyze.
                        if (!approximate) exactReductionTracks = tracks;
                        if (compact)
                        {
                            // Packing has its own frozen-baseline
                            // certificate. A rejected approximate
                            // reduction must not prevent bounded
                            // storage of a verified Exact result.
                            // Always search lossless storage for the initial
                            // Exact candidate, including an Error-bounded
                            // operation. Approximate retries must never hide a
                            // smaller, already verified Exact fallback.
                            bool approximateStorage = highQuality && approximate;
                            options.ExactOnly = !approximateStorage;
                            options.SmallFile = approximateStorage && smallFile;
                            options.MandatoryFrames = approximateStorage && smallFile && !exactFitJoins ? new int[0] : mandatory.ToArray();
                            options.FitJoinFrames = approximateStorage && smallFile && !exactFitJoins ? mandatory.ToArray() : new int[0];
                            using (var precise = verified)
                            {
                                async Task<NativeCompactCurveCertificate> Certificate(CHR0Node decoded, CancellationToken proofToken)
                                {
                                    proofToken.ThrowIfCancellationRequested();
                                    if (work >= 2000000) return new NativeCompactCurveCertificate { BudgetExhausted = true, Report = "The shared two-million-work continuous proof budget is exhausted." };
                                    string certificateInput = Path.Combine(stage, "certificate-" + Guid.NewGuid().ToString("N") + ".json"), certificateOutput = Path.Combine(stage, "certificate-" + Guid.NewGuid().ToString("N") + ".json");
                                    try
                                    {
                                        var certificateRequest = new { baseline.Name, SampleCount = baseline.FrameCount, baseline.Loop, HighQuality = approximateStorage, SmallFile = approximateStorage && smallFile, ExplicitBounds = exportSettings, GeneratedPairFrames = generatedPairs, GeneratedPairSites = generatedSites, LengthScale = length,
                                            PrecisionScale = 1, MaximumWork = 2000000 - work, MaximumCandidateSpans = 100000 - spans,
                                            Tracks = wire, CandidateTracks = OptimizationWireTracks(decoded), BindPoses = bindPoses,
                                            ExactBones = output.ExactBones, ProtectedBones = output.ProtectedBones, MandatoryFrames = approximateStorage && smallFile && !exactFitJoins ? new int[0] : mandatory.ToArray(),
                                            FitJoinFrames = approximateStorage && smallFile && !exactFitJoins ? mandatory.ToArray() : new int[0], ProtectedRanges = ranges, ProtectionKnown = protectionKnown };
                                        File.WriteAllText(certificateInput, json.Serialize(certificateRequest));
                                        var response = await session.Send(new { Operation = "animation-optimize-prepare", Model = modelPath, State = controls, Optimization = new { InputPath = certificateInput, OutputPath = certificateOutput } });
                                        proofToken.ThrowIfCancellationRequested();
                                        if (response.OptimizationPath != certificateOutput || new FileInfo(certificateOutput).Length > 256L * 1024 * 1024) throw new InvalidDataException("Invalid compact scalar proof response.");
                                        var proof = json.Deserialize<OptimizationCertificateOutput>(File.ReadAllText(certificateOutput))?.Certificate;
                                        if (proof == null || proof.Work < 0 || proof.Work > 2000000 - work) throw new InvalidDataException("Invalid shared compact scalar proof budget receipt.");
                                        work += proof.Work;
                                        return new NativeCompactCurveCertificate { Accepted = proof.Accepted, BudgetExhausted = proof.WorkLimitReached, FailedBone = proof.FailedBone, FailedChannel = proof.FailedChannel,
                                            ProbeFrames = proof.ProbeFrames ?? new double[0], Report = proof.Reason + "\r\n" + string.Join("\r\n", (proof.Channels ?? new OptimizationChannelInfo[0]).Select(c => c.Bone + " / " + c.Channel + ": continuous value ≤ " + c.CertifiedValueErrorBound.ToString("G6") + "; derivative ≤ " + c.CertifiedSpeedErrorBound.ToString("G6"))) };
                                    }
                                    finally { foreach (string file in new[] { certificateInput, certificateOutput }) if (File.Exists(file)) File.Delete(file); }
                                }
                                verified = await Task.Run(() => NativeAnimationOptimization.PrepareCompact(validationModel, baseline, before, precise, options, stage, Certificate, denseProofBudget, token), token);
                            }
                        }
                        try { token.ThrowIfCancellationRequested(); current(); receipts.Add(verified, output); return verified; }
                        catch { verified.Dispose(); throw; }
                    }
                    finally
                    {
                        foreach (string generated in new[] { inputPath, outputPath })
                            try { if (File.Exists(generated)) File.Delete(generated); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    }
                }
                long currentBytes=GeneratedAnimationBytes(before,stage);
                var choice=await NativeAnimationOptimizationPreparation.Prepare(highQuality,currentBytes,Attempt,token,exploreSelectiveFallbacks:true,
                    preferTightPrecision:exportSettings?.SearchEffort==NativeExportSearchEffort.Thorough||generatedPairs.Length>0||generatedSites.Length>0);
                try
                {
                    token.ThrowIfCancellationRequested();current();
                    return new FrozenOptimizationAnalysis{Choice=choice,Output=choice.Result==null?null:receipts[choice.Result],Budget=budget,Work=work,Spans=spans,RetainedDense=retainedDense};
                }
                catch{choice.Dispose();throw;}
            }
            finally{client?.Dispose();}
        }
    }
}
