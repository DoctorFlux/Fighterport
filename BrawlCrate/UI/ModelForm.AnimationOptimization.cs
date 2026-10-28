using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
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
        // Enabled only by the separately qualified second increment.
        private const bool HighQualityOptimizationQualified = true;
        internal Action<AnimationOptimizationDialog> OptimizationReady;
        internal int OptimizationPrepares { get; private set; }
        internal int OptimizationNativeChecks { get; private set; }
        internal int OptimizationApplies { get; private set; }
        private const long OptimizationCacheKeyLimit = 2000000, OptimizationCacheByteLimit = 256L * 1024 * 1024;
        private long _optimizationCacheClock;
        private Dictionary<OptimizationCacheKey, OptimizationSessionBaseline> _optimizationBaselines = new Dictionary<OptimizationCacheKey, OptimizationSessionBaseline>();
        private OptimizationSessionBaseline _optimizationBaseline;
        internal int OptimizationCachedClips => _optimizationBaselines.Count;
        internal long OptimizationCachedKeys => _optimizationBaselines.Values.Sum(b => b.BaselineKeys);
        internal long OptimizationCachedBytes => _optimizationBaselines.Values.Sum(b => b.RetainedBytes);
        private sealed class OptimizationCacheKey : IEquatable<OptimizationCacheKey>
        {
            internal readonly FighterProject Project; internal readonly MDL0Node Model; internal readonly CHR0Node Clip;
            internal OptimizationCacheKey(FighterProject project, MDL0Node model, CHR0Node clip) { Project = project; Model = model; Clip = clip; }
            public bool Equals(OptimizationCacheKey other) => other != null && ReferenceEquals(Project, other.Project) && ReferenceEquals(Model, other.Model) && ReferenceEquals(Clip, other.Clip);
            public override bool Equals(object other) => Equals(other as OptimizationCacheKey);
            public override int GetHashCode() => unchecked((System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Project) * 397 ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Model)) * 397 ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Clip));
        }
        private sealed class OptimizationSessionBaseline : IDisposable
        {
            internal FighterProject Project; internal MDL0Node Model; internal CHR0Node Clip, Baseline;
            internal string ModelContent, Protection, ProtectionHash, OutputContent, OutputStorage, BaselineHash, Report, MarkerReport;
            internal long BaselineKeys, BaselineBytes, LastUse;
            internal bool HighQuality, SmallFile, CompactStorage;
            private static long TextBytes(string value) => value == null ? 0 : checked(2L * value.Length);
            internal long MarkerBytes => TextBytes(ModelContent) + TextBytes(ProtectionHash) + TextBytes(OutputContent) + TextBytes(OutputStorage) + TextBytes(BaselineHash) + TextBytes(MarkerReport);
            internal long RetainedBytes => BaselineBytes + TextBytes(ModelContent) + TextBytes(Protection) + TextBytes(ProtectionHash) + TextBytes(OutputContent) + TextBytes(OutputStorage) + TextBytes(BaselineHash) + TextBytes(Report) + (ReferenceEquals(Report, MarkerReport) ? 0 : TextBytes(MarkerReport));
            internal void Evict()
            {
                Baseline?.Dispose(); Baseline = null; BaselineKeys = BaselineBytes = 0;
                Protection = null; Report = MarkerReport;
            }
            public void Dispose() { Baseline?.Dispose(); Baseline = null; BaselineKeys = BaselineBytes = 0; }
        }
        private sealed class OptimizationCacheCommit : IDisposable
        {
            internal Dictionary<OptimizationCacheKey, OptimizationSessionBaseline> Entries;
            internal OptimizationSessionBaseline Pending, Replaced;
            internal OptimizationSessionBaseline[] Evictions;
            public void Dispose() { Pending?.Dispose(); Pending = null; }
        }
        private sealed class OptimizationKeyBits { public float Frame { get; set; } public int? FrameBits { get; set; } public int ValueBits { get; set; } public int TangentBits { get; set; } }
        private sealed class OptimizationTrackBits
        {
            public string Bone { get; set; } public OptimizationKeyBits[][] Channels { get; set; }
            public int FrameLimit { get; set; } public bool Loop { get; set; }
        }
        private sealed class OptimizationChannelInfo
        {
            public string Bone { get; set; } public int Channel { get; set; } public int BeforeKeys { get; set; } public int AfterKeys { get; set; }
            public string Policy { get; set; } public double CertifiedValueErrorBound { get; set; } public double CertifiedSpeedErrorBound { get; set; }
            public string RetainedReason { get; set; }
        }
        private sealed class OptimizationReportInfo
        {
            public string Version { get; set; }
            public long InputKeys { get; set; } public long OutputKeys { get; set; } public long Work { get; set; }
            public int CandidateSpans { get; set; } public int AcceptedMerges { get; set; }
            public bool WorkLimitReached { get; set; } public bool CandidateLimitReached { get; set; } public bool ProbeLimitReached { get; set; }
            public double[] ProbeFrames { get; set; } public OptimizationChannelInfo[] Channels { get; set; } public string Claim { get; set; }
        }
        private sealed class OptimizationOutput
        {
            public OptimizationTrackBits[] Tracks { get; set; } public string[] RemovedUnmatchedTracks { get; set; }
            public string[] RemovedBindTracks { get; set; } = new string[0];
            public string[] ExactBones { get; set; } public string[] ProtectedBones { get; set; } public OptimizationReportInfo Report { get; set; }
        }
        private sealed class OptimizationCertificateInfo
        {
            public bool Accepted { get; set; } public long Work { get; set; } public bool WorkLimitReached { get; set; }
            public string Reason { get; set; } public OptimizationChannelInfo[] Channels { get; set; }
            public double[] ProbeFrames { get; set; }
            public string FailedBone { get; set; } public int? FailedChannel { get; set; }
        }
        private sealed class OptimizationCertificateOutput { public OptimizationCertificateInfo Certificate { get; set; } }
        private static int OptimizationFloatBits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static float OptimizationFloat(int bits) => BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
        private static float OptimizationTime(OptimizationKeyBits key)
        {
            if (key == null) throw new InvalidDataException("Missing native optimization knot.");
            float time = key.FrameBits.HasValue ? OptimizationFloat(key.FrameBits.Value) : key.Frame;
            if (float.IsNaN(time) || float.IsInfinity(time) || time < 0 || float.IsNaN(key.Frame) || float.IsInfinity(key.Frame) ||
                key.FrameBits.HasValue && OptimizationFloatBits(key.Frame) != key.FrameBits.Value && !(time == 0 && key.Frame == 0))
                throw new InvalidDataException("Optimization knot time bits disagree with its finite native frame value.");
            return time;
        }
        private static OptimizationTrackBits[] OptimizationWireTracks(CHR0Node clip) => clip.Children.OfType<CHR0EntryNode>().Select(t => new OptimizationTrackBits {
            Bone = t.Name, FrameLimit = t.Keyframes.FrameLimit, Loop = t.Keyframes.Loop,
            Channels = t.KeyArrays.Select(c => c.Entries.Select(k => new OptimizationKeyBits { Frame = k._index, FrameBits = OptimizationFloatBits(k._index), ValueBits = OptimizationFloatBits(k._value), TangentBits = OptimizationFloatBits(k._tangent) }).ToArray()).ToArray()
        }).ToArray();
        private static IEnumerable<ResourceNode> OptimizationWalk(ResourceNode root)
        { yield return root; foreach (var child in root.Children) foreach (var node in OptimizationWalk(child)) yield return node; }
        private static string OptimizationTextHash(string value)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value ?? ""))).Replace("-", "").ToLowerInvariant();
        }
        private void ClearOptimizationBaseline()
        {
            foreach (var entry in _optimizationBaselines.Values) entry.Dispose();
            _optimizationBaselines.Clear(); _optimizationBaseline = null;
        }
        private void RemoveOptimizationBaseline(OptimizationCacheKey key)
        {
            if (!_optimizationBaselines.TryGetValue(key, out var entry)) return;
            _optimizationBaselines.Remove(key); if (ReferenceEquals(_optimizationBaseline, entry)) _optimizationBaseline = null; entry.Dispose();
        }
        private OptimizationCacheCommit PrepareOptimizationCache(OptimizationCacheKey key, OptimizationSessionBaseline previous,
            CHR0Node original, string modelContent, string protection, string outputContent, string outputStorage, bool quality, bool compact, string report, string stage)
            => PrepareOptimizationCacheMode(key, previous, original, modelContent, protection, outputContent, outputStorage, quality, false, compact, report, stage);

        private OptimizationCacheCommit PrepareOptimizationCacheMode(OptimizationCacheKey key, OptimizationSessionBaseline previous,
            CHR0Node original, string modelContent, string protection, string outputContent, string outputStorage, bool quality, bool smallFile, bool compact, string report, string stage)
        {
            var pending = new OptimizationSessionBaseline { Project = key.Project, Model = key.Model, Clip = key.Clip,
                ModelContent = modelContent, Protection = protection, ProtectionHash = OptimizationTextHash(protection), OutputContent = outputContent,
                OutputStorage = outputStorage, HighQuality = quality, SmallFile = smallFile, CompactStorage = compact, Report = report, LastUse = ++_optimizationCacheClock,
                BaselineHash = previous?.BaselineHash ?? NativeAnimationOptimization.ContentKey(original) };
            try
            {
                pending.MarkerReport = "This unchanged clip retains its original session baseline identity, but its native snapshot was evicted under the shared 2000000-key / 256 MiB session cache budget.\r\n" +
                    "Original baseline SHA-256: " + pending.BaselineHash + "\r\nCurrent optimized content SHA-256: " + outputContent +
                    "\r\nRepeated " + (smallFile ? "Small file" : quality ? "High quality" : "Exact") + " optimization is a no-op. Exact cleanup remains available; another quality calculation requires the retained original snapshot or an explicit project reopen/authored edit establishing a new baseline.";
                // Allocate, serialize and measure the new cache before live Apply.
                // An evicted marker must never silently become a new baseline.
                if (original != null)
                {
                    pending.Baseline = NativeAnimationOptimization.Copy(original);
                    pending.BaselineKeys = pending.Baseline.Children.OfType<CHR0EntryNode>().Sum(e => (long)e.KeyArrays.Sum(a => a.KeyCount));
                    string path = Path.Combine(stage, "retained-baseline-" + Guid.NewGuid().ToString("N") + ".chr0");
                    try { pending.Baseline.ExportChecked(path); pending.BaselineBytes = new FileInfo(path).Length; }
                    finally { if (File.Exists(path)) File.Delete(path); }
                }
                else pending.Evict();
                var entries = new Dictionary<OptimizationCacheKey, OptimizationSessionBaseline>(_optimizationBaselines);
                entries.TryGetValue(key, out var replaced); entries[key] = pending;
                // Minimal identity markers are never evicted. Refuse before Apply
                // if even their complete accounting cannot fit the shared cap.
                if (entries.Values.Sum(e => e.MarkerBytes) > OptimizationCacheByteLimit)
                    throw new InvalidOperationException("Optimization session identity tracking exceeds its 256 MiB budget. Reopen the project before applying another optimized result.");
                long keys = entries.Values.Sum(e => e.BaselineKeys), bytes = entries.Values.Sum(e => e.RetainedBytes);
                var evictions = new List<OptimizationSessionBaseline>();
                foreach (var entry in entries.Values.Where(e => !ReferenceEquals(e, pending)).OrderBy(e => e.LastUse))
                {
                    if (keys <= OptimizationCacheKeyLimit && bytes <= OptimizationCacheByteLimit) break;
                    long markerBytes = entry.MarkerBytes;
                    if (entry.Baseline == null && entry.RetainedBytes <= markerBytes) continue;
                    evictions.Add(entry); keys -= entry.BaselineKeys; bytes -= entry.RetainedBytes - markerBytes;
                }
                if (keys > OptimizationCacheKeyLimit || bytes > OptimizationCacheByteLimit)
                {
                    long beforeBytes = pending.RetainedBytes; keys -= pending.BaselineKeys; pending.Evict(); bytes -= beforeBytes - pending.RetainedBytes;
                }
                if (keys > OptimizationCacheKeyLimit || bytes > OptimizationCacheByteLimit) throw new InvalidOperationException("Optimization session cache cannot retain complete identity markers within its existing resource budget.");
                return new OptimizationCacheCommit { Entries = entries, Pending = pending, Replaced = replaced, Evictions = evictions.ToArray() };
            }
            catch { pending.Dispose(); throw; }
        }
        private void CommitOptimizationCache(OptimizationCacheCommit commit)
        {
            foreach (var entry in commit.Evictions) entry.Evict();
            commit.Replaced?.Dispose();
            _optimizationBaselines = commit.Entries; _optimizationBaseline = commit.Pending; commit.Pending = null;
        }

        private static void WriteOptimizationPreviewRoot(ResourceNode root, string destination, CancellationToken token)
        {
            const string save = "Save target model changes before optimizing/exporting animation; animation edits may remain unsaved.";
            token.ThrowIfCancellationRequested();
            // Dirty animation siblings are deliberately allowed. Rebuilding their
            // shared container would serialize them and clear live native flags.
            // Pending render resources cannot be represented by old backing bytes.
            if (root == null) throw new InvalidOperationException(save);
            var source = root.WorkingUncompressed;
            if (source.Length <= 0 || source.Length > FighterProject.MaximumAssetBytes || (IntPtr)source.Address == IntPtr.Zero)
                throw new InvalidOperationException(save);
            if (OptimizationWalk(root).Any(node =>
                (node is MDL0Node || node is TEX0Node || node is PLT0Node) && node.IsDirty))
                throw new InvalidOperationException(save);
            byte[] buffer = new byte[Math.Min(256 * 1024, source.Length)];
            using (var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, FileOptions.SequentialScan))
            {
                for (int offset = 0; offset < source.Length;)
                {
                    token.ThrowIfCancellationRequested();
                    int count = Math.Min(buffer.Length, source.Length - offset);
                    System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add((IntPtr)source.Address, offset), buffer, 0, count);
                    file.Write(buffer, 0, count); offset += count;
                    if (offset % (4 * 1024 * 1024) == 0) Thread.Sleep(1);
                }
            }
            token.ThrowIfCancellationRequested();
        }

        private void ShowAnimationOptimization()
        {
            var editor=modelEditControl1;
            var model=editor.TargetModel as MDL0Node??throw new InvalidOperationException("Open a native target model first.");
            RequireSavedAnimationExportTarget(model);
            var source=editor.SelectedCHR0??throw new InvalidOperationException("Select the target Model Previewer's current CHR0 animation.");
            if(editor.TargetAnimation!=source||!OwnsFighterRoot(source))throw new InvalidOperationException("Select the current project-owned target animation.");
            if(_controlBusy||_controlSetupQueued||_retargetBusy||_workspaceBusy||_pinGesture!=null||_curvePending||editor.AwaitingRedoSave)
                throw new InvalidOperationException("Finish the current animation/control edit before optimizing.");
            var project=_fighterProject;
            var asset=RegisteredFighterAssets().SingleOrDefault(item=>_fighterRoots.TryGetValue(item.Id,out var owner)&&ReferenceEquals(owner,source.RootNode))
                ??throw new InvalidOperationException("Register this target animation in the project first.");
            if(asset.Retarget?.ReviewDraft==true)throw new InvalidOperationException("An unvalidated retarget draft cannot be promoted by optimization.");
            CancelLibraryAutoPreview();editor.StopAnim();_libraryTimer?.Stop();if(_libraryPlay!=null)_libraryPlay.Text="Play";
            string content=NativeAnimationOptimization.SourceContentKey(source),storage=NativeAnimationOptimization.StorageKey(source);
            string modelContent=NativeControlHash(model,".mdl0"),modelIdentity=WorkspaceNativeIdentity(model);
            string stage=Path.Combine(_controlStage,"optimization-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
            var roots=new List<ResourceNode>();CancellationTokenSource cancellation=null;
            Chr0ExportSnapshot prepared=null;string preparedSettings=null,preparedContent=null;
            var preferences=new NativeExportOptimizationPreferences();
            using(var before=NativeAnimationOptimization.CopySource(source))
            {
                void Current()
                {
                    RequireSavedAnimationExportTarget(model);
                    if(IsDisposed||_fighterProject!=project||WorkspaceNativeIdentity(model)!=modelIdentity||editor.TargetModel!=model||editor.SelectedCHR0!=source||editor.TargetAnimation!=source||!OwnsFighterRoot(source)||
                        NativeAnimationOptimization.SourceContentKey(source)!=content||NativeAnimationOptimization.StorageKey(source)!=storage||
                        NativeControlHash(model,".mdl0")!=modelContent||_controlBusy||_retargetBusy||_pinGesture!=null||_curvePending)
                        throw new InvalidOperationException("The current target animation or its dependencies changed. Analyze again; the stale export copy was discarded.");
                }
                try
                {
                    string rootPath=Path.Combine(stage,"model-root"+(string.IsNullOrEmpty(Path.GetExtension(model.RootNode.FilePath))?".brres":Path.GetExtension(model.RootNode.FilePath)));
                    WriteOptimizationPreviewRoot(model.RootNode,rootPath,CancellationToken.None);
                    ResourceNode PreviewRoot(){var root=NodeFactory.FromFile(null,rootPath)??throw new InvalidDataException("The independent model snapshot could not reopen.");roots.Add(root);return root;}
                    MDL0Node PreviewModel(ResourceNode root)
                    {
                        var matches=OptimizationWalk(root).OfType<MDL0Node>().Where(item=>WorkspaceNativeIdentity(item)==modelIdentity).ToArray();
                        if(matches.Length!=1||NativeControlHash(matches[0],".mdl0")!=modelContent)
                            throw new InvalidOperationException("Save target model changes before optimizing/exporting animation; animation edits may remain unsaved.");
                        return matches[0];
                    }
                    var originalRoot=PreviewRoot();var resultRoot=PreviewRoot();
                    using(var window=new AnimationOptimizationDialog(originalRoot,PreviewModel(originalRoot),resultRoot,PreviewModel(resultRoot),before,
                        editor.ModelPanel.CurrentViewport,roots,model.Name+" · "+source.Name+" · "+source.FrameCount+" frames",HighQualityOptimizationQualified))
                    {
                        window.CancelWork=()=>cancellation?.Cancel();
                        window.Prepare=async requestedQuality=>
                        {
                            Current();prepared?.Dispose();prepared=null;
                            cancellation?.Dispose();cancellation=new CancellationTokenSource();var token=cancellation.Token;
                            var settings=window.ReadExportSettings();string uiSettingsKey=settings.StableKey;
                            // Legacy background probes can select a policy without
                            // adding a third product mode. Production uses the UI.
                            if(window.TestHighQuality.HasValue)settings.Policy=requestedQuality?NativeExportOptimizationPolicy.ErrorBounded:NativeExportOptimizationPolicy.Exact;
                            settings.Validate();preferences.Save(settings);
                            Chr0ExportSnapshot frozen=null;
                            try
                            {
                                frozen=FreezeWorkspaceExport();frozen.PreferenceWarning=preferences.LastError;_activeSingleExport=frozen;_workspaceBusy=true;UpdateFighterStatus();OptimizationPrepares++;
                                // The exact same admission, curve/codec search,
                                // fit/contact gates and fresh native decoder used
                                // by single/set/slot export. No live Apply path.
                                await PrepareOptimizedExportSnapshot(frozen,settings,token);
                                token.ThrowIfCancellationRequested();Current();frozen.Validate();
                                if(window.ReadExportSettings().StableKey!=uiSettingsKey)throw new InvalidOperationException("Optimization settings changed during Analyze. Analyze again.");
                                OptimizationNativeChecks++;
                                prepared=frozen;frozen=null;preparedSettings=uiSettingsKey;preparedContent=ExportContent(prepared.Candidate);
                                long beforeKeys=prepared.Master.Children.OfType<CHR0EntryNode>().Sum(entry=>entry.KeyArrays.Sum(channel=>(long)channel.KeyCount));
                                long afterKeys=prepared.Candidate.Children.OfType<CHR0EntryNode>().Sum(entry=>entry.KeyArrays.Sum(channel=>(long)channel.KeyCount));
                                bool saving=prepared.ExportBytes<prepared.MasterBytes;
                                window.PreparedCanApply=true;
                                window.PreparedSummary="Keys "+beforeKeys+" → "+afterKeys+" · native CHR0 "+prepared.MasterBytes+" → "+prepared.ExportBytes+
                                    " bytes · "+prepared.EffectiveExportPolicy+". "+(saving?"Review the decoded copy, then export.":"No useful size reduction; the verified reference copy is retained.");
                                window.PreparedReport="Finished target master SHA-256: "+OptimizationTextHash(ExportContent(prepared.Master))+"\r\n"+
                                    prepared.OptimizationReport+"\r\n"+prepared.ByteReport+"\r\nThe editable master, native Undo, recipes and source resources remain unchanged.";
                                return NativeAnimationOptimization.CopySource(prepared.Candidate);
                            }
                            finally
                            {
                                frozen?.Dispose();_activeSingleExport=null;_workspaceBusy=false;UpdateFighterStatus();
                            }
                        };
                        window.Apply=(quality,candidate)=>
                        {
                            Current();
                            if(prepared==null||preparedSettings!=window.ReadExportSettings().StableKey||preparedContent!=ExportContent(candidate))
                                throw new InvalidOperationException("Analyze the current animation again before exporting.");
                            prepared.Validate();
                            // The prepared snapshot retains its own frozen
                            // settings, fit admission and all stale guards.
                            ExportFrozenPreparedOptimization(prepared,window);
                            OptimizationApplies++;UpdateFighterStatus();
                        };
                        window.Shown+=(sender,args)=>OptimizationReady?.Invoke(window);
                        window.ShowDialog(this);
                    }
                }
                finally
                {
                    cancellation?.Cancel();cancellation?.Dispose();prepared?.Dispose();
                    foreach(var root in roots)root.Dispose();
                    string resolved=Path.GetFullPath(stage),parent=Path.GetFullPath(_controlStage);
                    if(Path.GetDirectoryName(resolved)==parent&&Path.GetFileName(resolved).StartsWith("optimization-",StringComparison.Ordinal))
                        try{Directory.Delete(resolved,true);}catch(IOException){}catch(UnauthorizedAccessException){}
                }
            }
        }
    }
}
