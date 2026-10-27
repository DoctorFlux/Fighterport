using BrawlLib.Internal;
using BrawlLib.Modeling;
using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace BrawlCrate.Studio
{
    // These inputs belong to one frozen operation. In particular, LengthScale is
    // reviewed target geometry, never a guessed one-unit fallback.
    internal sealed class NativeAnimationOptimizationOptions
    {
        public bool ExactOnly { get; set; }
        public double LengthScale { get; set; }
        public long? OriginalStoredBytes { get; set; }
        public long? OriginalStoredResourceBytes { get; set; }
        public string[] TargetBones { get; set; } = new string[0];
        public string[] ProtectedBones { get; set; } = new string[0];
        public string[] ExactBones { get; set; } = new string[0];
        public int[] MandatoryFrames { get; set; } = new int[0];
        public double[] ProbeFrames { get; set; } = new double[0];
        public string[] BindPoseCleanupBones { get; set; } = new string[0];
        // Internal composition only: the complete indexed candidate must still
        // pass current/original byte caps before it can be returned to the UI.
        public bool DeferSizeValidation { get; set; }
        public NativeAnimationOptimizationBudget Budget { get; set; }
    }

    // Operation-owned aggregate for sequential quality attempts. These are the
    // existing PoseMotionReconstruction operation caps: logical probes, combined
    // bone poses and skin evaluations. Every probe evaluates baseline + candidate.
    internal sealed class NativeAnimationOptimizationBudget
    {
        public const long MaximumSamples = 100000;
        public const long MaximumBoneVisits = 10000000;
        public const long MaximumVertexVisits = 1000000000;
        public long Samples { get; set; }
        public long BoneVisits { get; set; }
        public long VertexVisits { get; set; }
        public long PoseEvaluations => checked(Samples * 2);
        private readonly object sync = new object();

        internal void Require(long witnesses, int bones, int vertices, string phase)
        {
            lock (sync)
            {
                if (witnesses < 0 || bones < 0 || vertices < 0 || Samples < 0 || BoneVisits < 0 || VertexVisits < 0)
                    throw new InvalidDataException("Native optimization resource counters must be nonnegative.");
                Check("Samples", Samples, witnesses, MaximumSamples, phase);
                Check("BoneVisits", BoneVisits, checked(witnesses * bones * 2), MaximumBoneVisits, phase);
                Check("VertexVisits", VertexVisits, checked(witnesses * vertices * 2), MaximumVertexVisits, phase);
            }
        }
        internal void Consume(int bones, int vertices)
        {
            lock (sync)
            {
                Require(1, bones, vertices, "next complete native witness");
                Samples++; BoneVisits += 2L * bones; VertexVisits += 2L * vertices;
            }
        }
        private static void Check(string name, long consumed, long additional, long limit, string phase)
        {
            long total = checked(consumed + additional);
            if (total <= limit) return;
            var error = new InvalidDataException("Complete native optimization verification exceeds " + name + " during " + phase +
                ": requires " + additional + " additional, already consumed " + consumed + ", total required " + total + ", limit " + limit +
                ". Baseline and candidate bone/skin evaluations are counted; no partial result can be applied.");
            error.Data["OptimizationBudget"] = name; error.Data["RequiredAdditional"] = additional;
            error.Data["Consumed"] = consumed; error.Data["RequiredTotal"] = total; error.Data["Limit"] = limit; error.Data["Phase"] = phase;
            throw error;
        }
    }

    internal sealed class NativeAnimationOptimizationResult : IDisposable
    {
        public CHR0Node Candidate { get; internal set; }
        public string ContentKey { get; internal set; }
        public string StorageKey { get; internal set; }
        public string EncodedSha256 { get; internal set; }
        public string Report { get; internal set; }
        public long BeforeBytes { get; internal set; }
        public long AfterBytes { get; internal set; }
        public long BeforeResourceBytes { get; internal set; }
        public long CandidateResourceBytes { get; internal set; }
        public long? OriginalInputResourceBytes { get; internal set; }
        public int BeforeKeys { get; internal set; }
        public int AfterKeys { get; internal set; }
        public int BeforeTracks { get; internal set; }
        public int AfterTracks { get; internal set; }
        public bool HasChanges { get; internal set; }
        public bool UsedApproximateStorage { get; internal set; }
        public string ReductionSelectionReport { get; internal set; }
        public double WorstFrame { get; internal set; }
        public string WorstBone { get; internal set; }
        public string WorstMetric { get; internal set; }
        public int Samples { get; internal set; }
        public long BudgetSamples { get; internal set; }
        public long BudgetBoneVisits { get; internal set; }
        public long BudgetVertexVisits { get; internal set; }
        public double MaximumEulerError { get; internal set; }
        public double MaximumOrientationError { get; internal set; }
        public double MaximumScaleError { get; internal set; }
        public double MaximumLocalTranslationError { get; internal set; }
        public double MaximumWorldBoneError { get; internal set; }
        public double MaximumSkinError { get; internal set; }
        public double MaximumEulerDerivativeError { get; internal set; }
        public double MaximumOrientationDerivativeError { get; internal set; }
        public double MaximumScaleDerivativeError { get; internal set; }
        public double MaximumTranslationDerivativeError { get; internal set; }
        public double MaximumWorldDerivativeError { get; internal set; }
        public double MaximumSkinDerivativeError { get; internal set; }
        public void Dispose() { Candidate?.Dispose(); Candidate = null; }
    }

    // Adapter-only final gate. Core owns curve reduction and continuous scalar
    // proofs. This verifies independent native serialization and cumulative native
    // evaluation, using caller-owned snapshots exclusively, never a live document.
    internal static partial class NativeAnimationOptimization
    {
        private const int MaximumKeys = 2000000;
        private const int MaximumBones = 4096;
        private const double RotationLimit = .01;
        private const double ScaleLimit = .00001;
        private const double RotationDerivativeLimit = .05;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static CHR0Node Copy(CHR0Node source)
            => CopyNativeSnapshot(source, false);

        internal static CHR0Node CopySource(CHR0Node source)
            => CopyNativeSnapshot(source, true);

        private static CHR0Node CopyNativeSnapshot(CHR0Node source, bool sourceSnapshot)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ValidateMetadataPolicy(source, sourceSnapshot);
            CHR0Node copy = sourceSnapshot ? new NativeSourceSnapshotNode(SourceScalingRule(source)) : new CHR0Node();
            copy.Name = source.Name; copy.Version = source.Version; copy.FrameCount = source.FrameCount;
            copy.Loop = source.Loop; copy.OriginalPath = source.OriginalPath;
            try
            {
                foreach (UserDataClass user in source.UserEntries)
                    copy.UserEntries.Add(new UserDataClass { Name = user.Name, DataType = user.DataType, Entries = user.Entries.ToArray() });
                foreach (CHR0EntryNode sourceEntry in source.Children)
                {
                    var entry = copy.CreateEntry(sourceEntry.Name);
                    WriteFlags(sourceEntry, entry);
                    entry.Keyframes.FrameLimit = sourceEntry.Keyframes.FrameLimit;
                    entry.Keyframes.Loop = sourceEntry.Keyframes.Loop;
                    for (int channel = 0; channel < 9; channel++)
                        foreach (var key in sourceEntry.KeyArrays[channel].Entries)
                            entry.Keyframes.SetFrameValue(channel, key._index, key._value, true)._tangent = key._tangent;
                    entry.Keyframes.PreserveInterpolation = true;
                    var storage = PreciseCHR0EntryNode.CaptureStorage(sourceEntry);
                    if (storage != null) PreciseCHR0EntryNode.RestoreStorage(entry, storage);
                }
                PreciseCHR0EntryNode.Promote(copy);
                return copy;
            }
            catch { copy.Dispose(); throw; }
        }

        // Fixed native storage has no time field. A live editor can nevertheless
        // hold one +zero-slope key at a later time. Canonicalize only that proved
        // constant on an operation-owned export copy; Undo keeps the live knot.
        public static CHR0Node CopyForStorageVerification(CHR0Node source)
        {
            var copy = Copy(source);
            try
            {
                foreach (CHR0EntryNode entry in copy.Children)
                for (int channel = 0; channel < 9; channel++)
                {
                    var array = entry.KeyArrays[channel]; if (array.KeyCount != 1) continue;
                    var key = array.Entries.Single();
                    if (key._index == 0 || Bits(key._tangent) != 0 || Bits(key._value) == int.MinValue) continue;
                    float value = key._value; array.Remove(key._index);
                    entry.Keyframes.SetFrameValue(channel, 0, value, true)._tangent = 0;
                    entry.SignalPropertyChange();
                }
                return copy;
            }
            catch { copy.Dispose(); throw; }
        }

        public static double GetBindBoundsDiagonal(MDL0Node modelSnapshot)
        {
            if (modelSnapshot == null) throw new ArgumentNullException(nameof(modelSnapshot));
            modelSnapshot.ApplyCHR(null, 0);
            var vertices = modelSnapshot.Objects.OfType<MDL0ObjectNode>().SelectMany(o => o.Vertices ?? new List<Vertex3>()).ToArray();
            if (vertices.Length == 0) return 0;
            double minX = double.PositiveInfinity, minY = minX, minZ = minX;
            double maxX = double.NegativeInfinity, maxY = maxX, maxZ = maxX;
            foreach (var vertex in vertices)
            {
                var p = vertex.WeightedPosition;
                if (!Finite(p._x) || !Finite(p._y) || !Finite(p._z)) throw new InvalidDataException("Target bind geometry contains a non-finite position.");
                minX = Math.Min(minX, p._x); minY = Math.Min(minY, p._y); minZ = Math.Min(minZ, p._z);
                maxX = Math.Max(maxX, p._x); maxY = Math.Max(maxY, p._y); maxZ = Math.Max(maxZ, p._z);
            }
            double x = maxX - minX, y = maxY - minY, z = maxZ - minZ;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        public static string ContentKey(CHR0Node clip)
            => NativeContentKey(clip, false);

        internal static string SourceContentKey(CHR0Node clip)
            => SourceScalingRule(clip) + ":" + NativeContentKey(clip, true);

        private static string NativeContentKey(CHR0Node clip, bool sourceSnapshot)
        {
            ValidateMetadataPolicy(clip, sourceSnapshot);
            using (var buffer = new MemoryStream())
            {
                using (var writer = new BinaryWriter(buffer, Encoding.UTF8, true))
                {
                    writer.Write(clip.Name ?? ""); writer.Write(clip.Version); writer.Write(clip.FrameCount);
                    writer.Write(clip.Loop); writer.Write(clip.OriginalPath ?? ""); writer.Write(clip.UserEntries.Count);
                    foreach (UserDataClass user in clip.UserEntries)
                    {
                        writer.Write(user.Name ?? ""); writer.Write((int)user.DataType); writer.Write(user.Entries.Length);
                        foreach (string value in user.Entries) writer.Write(value ?? "");
                    }
                    writer.Write(clip.Children.Count);
                    foreach (CHR0EntryNode entry in clip.Children)
                    {
                        writer.Write(entry.Name ?? ""); foreach (bool flag in Flags(entry)) writer.Write(flag);
                        writer.Write(entry.Keyframes.FrameLimit); writer.Write(entry.Keyframes.Loop);
                        foreach (var channel in entry.KeyArrays)
                        {
                            writer.Write(channel.KeyCount);
                            foreach (var key in channel.Entries)
                            { writer.Write(key._index); writer.Write(Bits(key._value)); writer.Write(Bits(key._tangent)); }
                        }
                    }
                }
                using (var hash = SHA256.Create()) return Hex(hash.ComputeHash(buffer.ToArray()));
            }
        }

        public static string StorageKey(CHR0Node clip) => string.Join(";", clip.Children.OfType<CHR0EntryNode>().Select(entry =>
        {
            var storage = PreciseCHR0EntryNode.CaptureStorage(entry);
            return entry.Name + ":" + string.Join("|", Enumerable.Range(0, 9).Select(channel =>
                storage != null && storage.MatchesGroup(entry.Keyframes, channel / 3) ? storage[channel]?.ContentKey ?? "I12" : "I12"));
        }));

        public static bool CanRemoveBindTrack(CHR0EntryNode entry, MDL0BoneNode bone)
        {
            if (entry == null || bone == null || entry.Name != bone.Name || entry.ScaleCompensateApply != bone.SegScaleCompApply ||
                entry.ScaleCompensateParent != bone.SegScaleCompParent || entry.ClassicScaleOff != !bone.ClassicScale) return false;
            for (int channel = 0; channel < 9; channel++)
            {
                float bind = bone.BindState[channel];
                if (!Finite(bind) || Bits(bind) == int.MinValue) return false;
                var keys = entry.KeyArrays[channel].Entries.ToArray();
                if (keys.Select(k => k._index).Distinct().Count() != keys.Length || keys.Any(k =>
                    Bits(k._value) != Bits(bind) || Bits(k._value) == int.MinValue || Bits(k._tangent) != 0)) return false;
            }
            return true;
        }

        public static NativeAnimationOptimizationResult Prepare(MDL0Node modelSnapshot, CHR0Node beforeSnapshot,
            ControlTrack[] proposedTracks, NativeAnimationOptimizationOptions options, string stageDirectory,
            CancellationToken token = default(CancellationToken))
        {
            if (modelSnapshot == null || beforeSnapshot == null || options == null) throw new ArgumentNullException("Optimization requires independent native snapshots and frozen options.");
            token.ThrowIfCancellationRequested();
            if (!Finite(options.LengthScale) || options.LengthScale < 0 || !options.ExactOnly && options.LengthScale <= 0)
                throw new InvalidDataException("High quality optimization requires a positive verified target geometry scale; exact mode does not guess one.");
            if (options.OriginalStoredResourceBytes.HasValue && options.OriginalStoredResourceBytes.Value <= 0)
                throw new InvalidDataException("The original native resource-block size must be positive; invalid size metadata cannot be retried as a quality failure.");
            if (beforeSnapshot.Version != 4 && beforeSnapshot.Version != 5) throw new NotSupportedException("Optimization supports native Brawl CHR0 version 4 or 5.");
            var targets = Set(options.TargetBones); var protectedBones = Set(options.ProtectedBones); var exactBones = Set(options.ExactBones);
            var bones = modelSnapshot.AllBones.ToArray();
            if (bones.Length == 0 || bones.Length > MaximumBones || bones.Select(b => b.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != bones.Length)
                throw new InvalidDataException("Native target bones are missing, ambiguous or exceed the optimization budget.");
            if (!targets.SetEquals(bones.Select(b => b.Name))) throw new InvalidDataException("Frozen optimization membership differs from the native target skeleton.");
            var originalEntries = beforeSnapshot.Children.OfType<CHR0EntryNode>().ToArray();
            if (originalEntries.Length != beforeSnapshot.Children.Count || originalEntries.Select(e => e.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != originalEntries.Length)
                throw new InvalidDataException("Native animation has ambiguous bone entries.");
            if (originalEntries.Any(e => !targets.Contains(e.Name) && bones.Any(b => string.Equals(b.Name, e.Name, StringComparison.OrdinalIgnoreCase))))
                throw new InvalidDataException("A native track has a differently cased target binding. Review its exact target identity before optimization; it cannot be removed as an unused track.");
            int beforeKeys = CountKeys(beforeSnapshot);
            if (beforeKeys > MaximumKeys || beforeSnapshot.FrameCount < 1) throw new InvalidDataException("Animation exceeds the native key budget or has no samples.");
            var tracks = proposedTracks ?? new ControlTrack[0];
            if (tracks.Any(t => t == null || t.Bone == null) || tracks.Select(t => t.Bone).Distinct(StringComparer.Ordinal).Count() != tracks.Length)
                throw new InvalidDataException("Optimized tracks contain missing or duplicate identities.");
            var proposedNames = Set(tracks.Select(t => t.Bone).ToArray());
            var bindCleanup = Set(options.BindPoseCleanupBones);
            var mandatory = new HashSet<int>(options.MandatoryFrames ?? new int[0]);
            if (mandatory.Any(frame => frame < 0 || frame > beforeSnapshot.FrameCount)) throw new InvalidDataException("A mandatory optimization frame lies outside native timing.");
            string beforeContent = ContentKey(beforeSnapshot);
            string prefix = Path.Combine(Path.GetFullPath(stageDirectory), "optimization-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            string beforePath = prefix + "-before.chr0", afterPath = prefix + "-after.chr0";
            CHR0Node candidate = null, decoded = null; long beforeResourceBytes = 0;
            try
            {
                using (var before = CopyForStorageVerification(beforeSnapshot))
                {
                    string storedBaselineContent = ContentKey(before);
                    before.ExportChecked(beforePath);
                    using (var reopened = NodeFactory.FromFile(null, beforePath) as CHR0Node)
                    {
                        if (reopened == null || ContentKey(reopened) != storedBaselineContent)
                            throw new InvalidDataException("The precise native baseline did not retain every metadata field, flag, knot and tangent. Optimization was discarded.");
                        beforeResourceBytes = ResourceBlockBytes(beforePath, reopened);
                    }
                }
                token.ThrowIfCancellationRequested();
                candidate = Copy(beforeSnapshot);
                foreach (CHR0EntryNode entry in candidate.Children.ToArray())
                    if (!targets.Contains(entry.Name) || !proposedNames.Contains(entry.Name))
                    {
                        if (targets.Contains(entry.Name))
                        {
                            if (!bindCleanup.Contains(entry.Name) || protectedBones.Contains(entry.Name) ||
                                !CanRemoveBindTrack(entry, bones.Single(b => b.Name == entry.Name)))
                                throw new InvalidDataException("A removed target track is not an eligible exact bind-pose constant: " + entry.Name);
                        }
                        else if (protectedBones.Contains(entry.Name) || exactBones.Contains(entry.Name)) throw new InvalidDataException("A protected native track is absent from the selected target: " + entry.Name);
                        entry.Remove(); entry.Dispose();
                    }
                foreach (var track in tracks)
                {
                    token.ThrowIfCancellationRequested();
                    if (!targets.Contains(track.Bone)) continue;
                    var sourceEntry = originalEntries.SingleOrDefault(e => e.Name == track.Bone);
                    if (sourceEntry == null) throw new InvalidDataException("Optimization cannot introduce a new native bone track: " + track.Bone);
                    var entry = candidate.Children.OfType<CHR0EntryNode>().Single(e => e.Name == track.Bone);
                    ValidateChannels(track, beforeSnapshot.FrameCount, beforeSnapshot.Loop);
                    if (protectedBones.Contains(track.Bone) && !SameChannels(sourceEntry, track.Channels))
                        throw new InvalidDataException("Optimization changed a protected native track: " + track.Bone);
                    for (int channel = 0; channel < 9; channel++)
                    {
                        var old = sourceEntry.KeyArrays[channel].Entries.ToArray();
                        bool fixedConstant = ExactFixedChannel(old, track.Channels[channel]);
                        foreach (int frame in mandatory.Concat(old.GroupBy(k => k._index).Where(g => g.Count() > 1).Select(g => g.Key)).Distinct())
                        {
                            var required = old.Where(k => k._index == frame).ToArray();
                            if (required.Length == 0) continue;
                            var actual = track.Channels[channel].Where(k => k.Frame == frame).ToArray();
                            if (!fixedConstant && (required.Length != actual.Length || required.Where((k, i) => Bits(k._value) != Bits(actual[i].Value) || Bits(k._tangent) != Bits(actual[i].Tangent)).Any()))
                                throw new InvalidDataException("Optimization changed a mandatory or discontinuous native knot: " + track.Bone + " at " + frame);
                        }
                        var array = entry.KeyArrays[channel];
                        while (array.KeyCount > 0) array.Remove(array.Entries.First()._index);
                        foreach (var key in track.Channels[channel]) entry.Keyframes.SetFrameValue(channel, key.Frame, key.Value, true)._tangent = key.Tangent;
                    }
                    entry.Keyframes.PreserveInterpolation = true; entry.SignalPropertyChange();
                }
                if (CountKeys(candidate) > MaximumKeys) throw new InvalidDataException("Optimized native candidate exceeds the key budget.");
                string expected = ContentKey(candidate); candidate.ExportChecked(afterPath);
                decoded = NodeFactory.FromFile(null, afterPath) as CHR0Node;
                if (decoded == null || ContentKey(decoded) != expected)
                    throw new InvalidDataException("Optimized native encode changed metadata, entry order, flags, knots or tangents (including scale-axis sharing). No candidate can be applied.");
                var result = new NativeAnimationOptimizationResult { BeforeBytes = new FileInfo(beforePath).Length,
                    AfterBytes = new FileInfo(afterPath).Length, BeforeKeys = beforeKeys, AfterKeys = CountKeys(decoded),
                    BeforeTracks = beforeSnapshot.Children.Count, AfterTracks = decoded.Children.Count, ContentKey = expected, StorageKey = StorageKey(decoded),
                    EncodedSha256 = FighterProject.HashFile(afterPath), HasChanges = beforeContent != expected,
                    BeforeResourceBytes = beforeResourceBytes, CandidateResourceBytes = ResourceBlockBytes(afterPath, decoded), OriginalInputResourceBytes = options.OriginalStoredResourceBytes };
                if (!result.HasChanges)
                {
                    result.Report = "No useful saving: the optimized native animation is unchanged. No changes need to be applied.\r\n" +
                        result.BeforeKeys + " keys, " + result.BeforeTracks + " tracks, " + result.BeforeBytes + " bytes in the independently verified current precise CHR0 export." +
                        "\r\nCurrent precise resource block: " + result.BeforeResourceBytes + " bytes; external strings excluded." +
                        (options.OriginalStoredResourceBytes.HasValue ? " Clean original input resource block: " + options.OriginalStoredResourceBytes.Value + " bytes. No resource rewrite is proposed." : "");
                    return result;
                }
                bool resourceGrowth = result.CandidateResourceBytes > result.BeforeResourceBytes ||
                    options.OriginalStoredResourceBytes.HasValue && result.CandidateResourceBytes > options.OriginalStoredResourceBytes.Value;
                if (!options.DeferSizeValidation && (result.AfterBytes > result.BeforeBytes || options.OriginalStoredBytes.HasValue && result.AfterBytes > options.OriginalStoredBytes.Value || resourceGrowth))
                {
                    var growth = new InvalidDataException(resourceGrowth ? "Optimized CHR0 resource block would be larger: candidate " + result.CandidateResourceBytes +
                        " bytes, current precise block " + result.BeforeResourceBytes + (options.OriginalStoredResourceBytes.HasValue ? ", clean original input block " + options.OriginalStoredResourceBytes.Value : "") +
                        ". External/shared string tables are excluded from every resource-block count. No changes were applied." :
                        "Optimized CHR0 would be larger than the current native bytes. No changes were applied.");
                    growth.Data["OptimizationSize"] = true; growth.Data["BeforeBytes"] = result.BeforeBytes; growth.Data["AfterBytes"] = result.AfterBytes;
                    growth.Data["BeforeResourceBytes"] = result.BeforeResourceBytes; growth.Data["CandidateResourceBytes"] = result.CandidateResourceBytes;
                    if (options.OriginalStoredBytes.HasValue) growth.Data["OriginalStoredBytes"] = options.OriginalStoredBytes.Value;
                    if (options.OriginalStoredResourceBytes.HasValue) growth.Data["OriginalInputResourceBytes"] = options.OriginalStoredResourceBytes.Value;
                    throw growth;
                }
                Evaluate(modelSnapshot, bones, beforeSnapshot, decoded, options, result, token);
                if (ContentKey(beforeSnapshot) != beforeContent) throw new InvalidOperationException("The operation's baseline snapshot changed during native verification.");
                result.Report = BuildReport(result, options);
                result.Candidate = decoded; decoded = null;
                return result;
            }
            finally
            {
                decoded?.Dispose(); candidate?.Dispose();
                if (File.Exists(beforePath)) File.Delete(beforePath);
                if (File.Exists(afterPath)) File.Delete(afterPath);
            }
        }

        private static void Evaluate(MDL0Node model, MDL0BoneNode[] bones, CHR0Node before, CHR0Node after,
            NativeAnimationOptimizationOptions options, NativeAnimationOptimizationResult result, CancellationToken token)
        {
            var vertices = model.Objects.OfType<MDL0ObjectNode>().SelectMany(o => o.Vertices ?? new List<Vertex3>()).ToArray();
            var budget = options.Budget ?? new NativeAnimationOptimizationBudget();
            var sampleTimes = new SortedSet<double> { 0, Math.Max(0, before.FrameCount - 1) };
            // Ordinary non-loop preview includes the final held display interval
            // up to (but excluding) N. Core's N-.0001 witness must be verified,
            // alongside the actual last authored sample N-1. Loop N is its
            // separately retained explicit closing sample.
            double end = before.Loop ? before.FrameCount : Math.Max(0, before.FrameCount - .0001);
            sampleTimes.Add(end);
            budget.Require(checked((long)Math.Ceiling(end) * 7 + 1), bones.Length, vertices.Length, "mandatory base-probe estimate");
            // Integer and quarter frames are supplemented by deterministic held-out
            // fractions. Both sides of native duplicate knots are sampled separately.
            for (int frame = 0; frame < end; frame++)
                foreach (double fraction in new[] { 0d, .137, .25, .371, .5, .733, .75 }) sampleTimes.Add(frame + fraction);
            foreach (int frame in options.MandatoryFrames ?? new int[0]) if (frame <= end) sampleTimes.Add(frame);
            foreach (double frame in options.ProbeFrames ?? new double[0])
            {
                if (!Finite(frame) || frame < 0 || frame > end) throw new InvalidDataException("A frozen Core verification probe lies outside native timing.");
                sampleTimes.Add(frame);
            }
            var discontinuities = new HashSet<double>(before.Children.OfType<CHR0EntryNode>().SelectMany(e => e.KeyArrays)
                .SelectMany(a => a.Entries.GroupBy(k => k._index).Where(g => g.Count() > 1).Select(g => (double)g.Key)));
            foreach (double knot in discontinuities)
            {
                if (knot > 0) sampleTimes.Add(knot - 1d / 1024);
                if (knot < end) sampleTimes.Add(knot + 1d / 1024);
            }
            long minimumWitnesses = sampleTimes.Count; double? previousTime = null;
            foreach (double time in sampleTimes)
            {
                if (previousTime.HasValue)
                {
                    double middle = (previousTime.Value + time) * .5;
                    if (middle > previousTime.Value && middle < time) minimumWitnesses++;
                }
                previousTime = time;
            }
            budget.Require(minimumWitnesses, bones.Length, vertices.Length, "complete base probes and mandatory adaptive-midpoint estimate");
            var exact = Set(options.ExactBones); var protectedBones = Set(options.ProtectedBones);
            double length = options.LengthScale;
            double localLimit = length > 0 ? Math.Min(.0002, length * .0001) : .0002;
            double worldLimit = length > 0 ? length * .0001 : .0002;
            var context = new EvaluationContext { Model = model, Bones = bones, Vertices = vertices, Before = before, After = after,
                Options = options, Result = result, Token = token, LocalLimit = localLimit, WorldLimit = worldLimit,
                Exact = exact, Protected = protectedBones, Discontinuities = discontinuities,
                BeforeCurves = Curves(before), AfterCurves = Curves(after), Budget = budget };
            ErrorSample previous = null, derivativeAnchor = null;
            foreach (double time in sampleTimes)
            {
                token.ThrowIfCancellationRequested(); var sample = context.Sample(time);
                if (previous != null) context.Interval(previous, sample, 0);
                if (derivativeAnchor == null || !context.Smooth(derivativeAnchor, sample)) derivativeAnchor = sample;
                else if (sample.Time - derivativeAnchor.Time >= 1d / 64)
                {
                    // Dense Core extrema/near-knot witnesses must not remove all
                    // resolvable derivative checks by splitting every adjacency.
                    context.DerivativeChord(derivativeAnchor, sample); derivativeAnchor = sample;
                }
                previous = sample;
            }
        }

        private sealed class ErrorSample
        {
            internal double Time, Ratio;
            internal double[] Local, Rotation, World, Skin;
        }

        private sealed class EvaluationContext
        {
            internal MDL0Node Model; internal MDL0BoneNode[] Bones; internal Vertex3[] Vertices;
            internal CHR0Node Before, After; internal NativeAnimationOptimizationOptions Options;
            internal NativeAnimationOptimizationResult Result; internal CancellationToken Token;
            internal double LocalLimit, WorldLimit, WorstRatio;
            internal HashSet<string> Exact, Protected; internal HashSet<double> Discontinuities;
            internal Dictionary<string, NativeCurve[]> BeforeCurves, AfterCurves;
            internal NativeAnimationOptimizationBudget Budget;

            internal ErrorSample Sample(double time)
            {
                Token.ThrowIfCancellationRequested(); Budget.Consume(Bones.Length, Vertices.Length); Result.Samples++;
                Result.BudgetSamples = Budget.Samples; Result.BudgetBoneVisits = Budget.BoneVisits; Result.BudgetVertexVisits = Budget.VertexVisits;
                var sample = new ErrorSample { Time = time, Local = new double[Bones.Length * 9], Rotation = new double[Bones.Length * 9],
                    World = new double[Bones.Length * 12], Skin = new double[Vertices.Length * 3] };
                Model.ApplyCHR(Before, (float)time + 1);
                for (int bone = 0; bone < Bones.Length; bone++)
                {
                    var state = Bones[bone].FrameState;
                    for (int channel = 0; channel < 9; channel++) sample.Local[bone * 9 + channel] = state[channel];
                    var rotation = Matrix.TransformMatrix(new Vector3(1), state.Rotate, new Vector3());
                    for (int column = 0; column < 3; column++) for (int row = 0; row < 3; row++)
                        sample.Rotation[bone * 9 + column * 3 + row] = rotation[column * 4 + row];
                    for (int column = 0; column < 4; column++) for (int row = 0; row < 3; row++)
                        sample.World[bone * 12 + column * 3 + row] = Bones[bone]._frameMatrix[column * 4 + row];
                }
                for (int vertex = 0; vertex < Vertices.Length; vertex++) Store(sample.Skin, vertex * 3, Vertices[vertex].WeightedPosition);
                Model.ApplyCHR(After, (float)time + 1);
                for (int bone = 0; bone < Bones.Length; bone++)
                {
                    string name = Bones[bone].Name; var state = Bones[bone].FrameState;
                    double translationDerivativeSquared = 0;
                    for (int channel = 0; channel < 9; channel++)
                    {
                        int index = bone * 9 + channel; double difference = state[channel] - sample.Local[index]; sample.Local[index] = difference;
                        double limit = channel < 3 ? ScaleLimit : channel < 6 ? RotationLimit : LocalLimit;
                        double error = Math.Abs(difference);
                        if (channel < 3) Result.MaximumScaleError = Math.Max(Result.MaximumScaleError, error);
                        else if (channel < 6) Result.MaximumEulerError = Math.Max(Result.MaximumEulerError, error);
                        else Result.MaximumLocalTranslationError = Math.Max(Result.MaximumLocalTranslationError, error);
                        Observe(sample, error, limit, name, channel < 3 ? "local scale" : channel < 6 ? "Euler rotation" : "local translation");
                        var beforeCurve = BeforeCurves.TryGetValue(name, out var beforeChannels) ? beforeChannels[channel] : null;
                        var afterCurve = AfterCurves.TryGetValue(name, out var afterChannels) ? afterChannels[channel] : null;
                        double derivativeDifference = (afterCurve?.Derivative(time) ?? 0) - (beforeCurve?.Derivative(time) ?? 0);
                        double derivativeError = Math.Abs(derivativeDifference);
                        double derivativeLimit = channel < 3 ? ScaleLimit : channel < 6 ? RotationDerivativeLimit : LocalLimit;
                        if (channel < 3) Result.MaximumScaleDerivativeError = Math.Max(Result.MaximumScaleDerivativeError, derivativeError);
                        else if (channel < 6) Result.MaximumEulerDerivativeError = Math.Max(Result.MaximumEulerDerivativeError, derivativeError);
                        else Result.MaximumTranslationDerivativeError = Math.Max(Result.MaximumTranslationDerivativeError, derivativeError);
                        if (channel >= 6) translationDerivativeSquared += derivativeDifference * derivativeDifference;
                        Observe(sample, derivativeError, derivativeLimit, name, channel < 3 ? "native scale derivative" : channel < 6 ? "native Euler derivative" : "native translation derivative");
                    }
                    double translationError = Norm(sample.Local, bone * 9 + 6);
                    Result.MaximumLocalTranslationError = Math.Max(Result.MaximumLocalTranslationError, translationError);
                    Observe(sample, translationError, LocalLimit, name, "local translation vector");
                    double translationDerivativeError = Math.Sqrt(translationDerivativeSquared);
                    Result.MaximumTranslationDerivativeError = Math.Max(Result.MaximumTranslationDerivativeError, translationDerivativeError);
                    Observe(sample, translationDerivativeError, LocalLimit, name, "native translation vector derivative");
                    var rotation = Matrix.TransformMatrix(new Vector3(1), state.Rotate, new Vector3()); double squared = 0;
                    for (int column = 0; column < 3; column++) for (int row = 0; row < 3; row++)
                    {
                        int index = bone * 9 + column * 3 + row;
                        double difference = rotation[column * 4 + row] - sample.Rotation[index]; sample.Rotation[index] = difference; squared += difference * difference;
                    }
                    double angle = 2 * Math.Asin(Math.Min(1, Math.Sqrt(squared) / (2 * Math.Sqrt(2)))) * 180 / Math.PI;
                    Result.MaximumOrientationError = Math.Max(Result.MaximumOrientationError, angle); Observe(sample, angle, RotationLimit, name, "local orientation");
                    for (int column = 0; column < 4; column++)
                    {
                        double errorSquared = 0;
                        for (int row = 0; row < 3; row++)
                        {
                            int index = bone * 12 + column * 3 + row; double difference = Bones[bone]._frameMatrix[column * 4 + row] - sample.World[index];
                            // A forward matrix basis is compared directly. Negative,
                            // nonuniform and zero scales never need decomposition.
                            if (column < 3) difference *= Options.LengthScale > 0 ? Options.LengthScale : 1;
                            sample.World[index] = difference; errorSquared += difference * difference;
                        }
                        double error = Math.Sqrt(errorSquared); Result.MaximumWorldBoneError = Math.Max(Result.MaximumWorldBoneError, error);
                        Observe(sample, error, WorldLimit, name, column == 3 ? "world bone position" : "world forward basis");
                    }
                }
                for (int vertex = 0; vertex < Vertices.Length; vertex++)
                {
                    var position = Vertices[vertex].WeightedPosition; int index = vertex * 3;
                    sample.Skin[index] = position._x - sample.Skin[index]; sample.Skin[index + 1] = position._y - sample.Skin[index + 1]; sample.Skin[index + 2] = position._z - sample.Skin[index + 2];
                    double error = Norm(sample.Skin, index); Result.MaximumSkinError = Math.Max(Result.MaximumSkinError, error);
                    Observe(sample, error, WorldLimit, "vertex " + vertex, "full skin position");
                }
                return sample;
            }

            internal void Interval(ErrorSample left, ErrorSample right, int depth)
            {
                Token.ThrowIfCancellationRequested(); double span = right.Time - left.Time;
                if (span <= 0) return;
                // Core supplies adjacent float/double representable neighbors of
                // knots as value witnesses. Native time is float32; dividing
                // rounding noise by those tiny gaps is not a motion derivative.
                // Keep every value witness, and measure cumulative derivative
                // chords only across resolvable smooth native-frame intervals.
                if (Smooth(left, right) && span >= 1d / 64) DerivativeChord(left, right);
                // Sample every interval midpoint once, then refine intervals near a
                // frozen bound. Budget exhaustion is a failure, never a partial pass.
                if (depth < 1 || depth < 3 && Math.Max(left.Ratio, right.Ratio) > .2)
                {
                    double middleTime = (left.Time + right.Time) * .5;
                    if (middleTime <= left.Time || middleTime >= right.Time) return;
                    var middle = Sample(middleTime);
                    Interval(left, middle, depth + 1); Interval(middle, right, depth + 1);
                }
            }

            internal bool Smooth(ErrorSample left, ErrorSample right) =>
                !Discontinuities.Any(t => t > left.Time && t <= right.Time || t == left.Time);

            internal void DerivativeChord(ErrorSample left, ErrorSample right)
            {
                Token.ThrowIfCancellationRequested(); double span = right.Time - left.Time;
                if (span < 1d / 64 || !Smooth(left, right)) return;
                for (int bone = 0; bone < Bones.Length; bone++)
                {
                    for (int column = 0; column < 4; column++)
                    {
                        double error = DifferenceNorm(left.World, right.World, bone * 12 + column * 3) / span;
                        Result.MaximumWorldDerivativeError = Math.Max(Result.MaximumWorldDerivativeError, error);
                        Observe(right, error, WorldLimit, Bones[bone].Name, "world forward derivative");
                    }
                    double rotationSquared = 0;
                    for (int element = 0; element < 9; element++)
                    {
                        int index = bone * 9 + element; double difference = (right.Rotation[index] - left.Rotation[index]) / span;
                        rotationSquared += difference * difference;
                    }
                    double orientationDerivative = Math.Sqrt(rotationSquared / 2) * 180 / Math.PI;
                    Result.MaximumOrientationDerivativeError = Math.Max(Result.MaximumOrientationDerivativeError, orientationDerivative);
                    Observe(right, orientationDerivative, RotationDerivativeLimit, Bones[bone].Name, "local orientation derivative");
                }
                for (int vertex = 0; vertex < Vertices.Length; vertex++)
                {
                    double error = DifferenceNorm(left.Skin, right.Skin, vertex * 3) / span;
                    Result.MaximumSkinDerivativeError = Math.Max(Result.MaximumSkinDerivativeError, error);
                    Observe(right, error, WorldLimit, "vertex " + vertex, "full skin derivative");
                }
            }

            private void Observe(ErrorSample sample, double error, double limit, string bone, string metric)
            {
                if (!Finite(limit) || limit <= 0) throw new InvalidDataException("Invalid frozen native optimization bound for " + metric + " at " + bone + ".");
                if (!Finite(error)) throw QualityFailure(sample, error, limit, bone, metric);
                double ratio = error / limit; sample.Ratio = Math.Max(sample.Ratio, ratio);
                if (ratio > WorstRatio) { WorstRatio = ratio; Result.WorstFrame = sample.Time; Result.WorstBone = bone; Result.WorstMetric = metric; }
                if (ratio > 1) throw QualityFailure(sample, error, limit, bone, metric);
            }

            private InvalidDataException QualityFailure(ErrorSample sample, double error, double limit, string bone, string metric)
            {
                var failure = new InvalidDataException("Native optimization rejected: " + metric + " at " + bone + ", frame " + sample.Time.ToString("G9", Invariant) +
                    " = " + error.ToString("G9", Invariant) + ", limit " + limit.ToString("G9", Invariant) + ". No changes were applied.");
                failure.Data["OptimizationQuality"] = true; failure.Data["Bone"] = bone; failure.Data["Frame"] = sample.Time;
                failure.Data["Metric"] = metric; failure.Data["Error"] = error; failure.Data["Limit"] = limit;
                failure.Data["BudgetSamples"] = Budget.Samples; failure.Data["BudgetBoneVisits"] = Budget.BoneVisits; failure.Data["BudgetVertexVisits"] = Budget.VertexVisits;
                return failure;
            }
        }

        private static string BuildReport(NativeAnimationOptimizationResult result, NativeAnimationOptimizationOptions options)
        {
            return (options.ExactOnly ? "Exact curve reduction; native float rounding checked." : "High quality curve reduction; frozen native error bounds checked.") +
                "\r\nCHR0: " + result.BeforeBytes + " → " + result.AfterBytes + " bytes; " + result.BeforeKeys + " → " + result.AfterKeys + " keys; " + result.BeforeTracks + " → " + result.AfterTracks + " tracks." +
                "\r\nNative resource block: " + result.BeforeResourceBytes + " → " + result.CandidateResourceBytes + " bytes, verified against each exported CHR0 header size; external/shared string tables excluded." +
                "\r\nIndependent I12 decode preserves every candidate key, tangent, semantic flag, entry order and metadata field." +
                "\r\n" + result.Samples + " native samples: integer/quarter/withheld fractions, loop endpoints, mandatory knots and bounded adaptive midpoints; all native bones and skinned vertices." +
                "\r\nAggregate native work: " + result.BudgetSamples + "/" + NativeAnimationOptimizationBudget.MaximumSamples + " logical witnesses (" + (result.BudgetSamples * 2) + " baseline/candidate pose evaluations), " +
                    result.BudgetBoneVisits + "/" + NativeAnimationOptimizationBudget.MaximumBoneVisits + " bone visits, " + result.BudgetVertexVisits + "/" + NativeAnimationOptimizationBudget.MaximumVertexVisits + " skin-vertex visits." +
                "\r\nMaximum errors: Euler " + G(result.MaximumEulerError) + "°, orientation " + G(result.MaximumOrientationError) + "°, scale " + G(result.MaximumScaleError) + ", local translation " + G(result.MaximumLocalTranslationError) + ", world/basis " + G(result.MaximumWorldBoneError) + ", skin " + G(result.MaximumSkinError) + "." +
                "\r\nDerivative error/frame: Euler " + G(result.MaximumEulerDerivativeError) + "°, orientation " + G(result.MaximumOrientationDerivativeError) + "°, scale " + G(result.MaximumScaleDerivativeError) + ", translation " + G(result.MaximumTranslationDerivativeError) + ", world " + G(result.MaximumWorldDerivativeError) + ", skin " + G(result.MaximumSkinDerivativeError) + "." +
                "\r\nScalar derivatives use decoded native Hermite coefficients. Cumulative orientation/world/skin derivative chords use smooth intervals at least 1/64 native frame apart; near-knot value witnesses remain included. Sampled cumulative geometry/derivatives are not a continuous-time certificate." +
                (options.OriginalStoredBytes.HasValue ? "\r\nRecorded clean standalone file size: " + options.OriginalStoredBytes.Value + " bytes; candidate cannot exceed either this size or the current precise export." : "") +
                (options.OriginalStoredResourceBytes.HasValue ? "\r\nClean original input resource block: " + options.OriginalStoredResourceBytes.Value + " bytes; candidate resource block cannot exceed it. This is not a PAC alignment/compression size claim." : "") +
                (options.LengthScale > 0 ? "\r\nVerified target length scale " + G(options.LengthScale) + "." : "\r\nNo nonzero target length scale; exact-mode native rounding uses the existing absolute 0.0002 translation/position bound.") +
                "\r\nWorst measured gate: " + (result.WorstMetric ?? "zero error") + " · " + (result.WorstBone ?? "all") + " · frame " + G(result.WorstFrame) + "." +
                "\r\nThese are standalone CHR0 byte counts. PAC/BRRES alignment or compressed container savings require a separate actual export. Contacts/game behavior are not newly accepted.";
        }

        private static void ValidateChannels(ControlTrack track, int frames, bool loop)
        {
            if (track.Channels == null || track.Channels.Length != 9 || track.Channels.Any(c => c == null)) throw new InvalidDataException("Native optimization needs all nine channels for " + track.Bone);
            foreach (var channel in track.Channels)
            {
                int previous = -1, duplicates = 0;
                foreach (var key in channel)
                {
                    if (key == null || key.Frame < 0 || key.Frame > (loop ? frames : frames - 1) || key.Frame < previous || !Finite(key.Value) || !Finite(key.Tangent)) throw new InvalidDataException("Invalid optimized native key at " + track.Bone);
                    duplicates = key.Frame == previous ? duplicates + 1 : 1;
                    if (duplicates > 2) throw new InvalidDataException("Native channels permit at most two sides at a knot."); previous = key.Frame;
                }
                if (channel.Length == 1 && (channel[0].Frame != 0 || Bits(channel[0].Tangent) != Bits(0f))) throw new InvalidDataException("A fixed native channel must use frame zero and zero tangent.");
            }
        }

        private sealed class NativeCurve
        {
            internal ControlKey[] Keys; internal int Limit; internal bool Loop;
            internal double Derivative(double time)
            {
                if (Keys.Length < 2) return 0;
                ControlKey first = Keys[0], last = Keys[Keys.Length - 1], left = null, right = null;
                double offset, span;
                if (time < first.Frame || time > last.Frame)
                {
                    if (!Loop) return 0;
                    left = last; right = first; span = Limit - last.Frame + first.Frame;
                    offset = time > last.Frame && time < Limit ? time - last.Frame : Limit - last.Frame + time;
                }
                else
                {
                    int low = 0, high = Keys.Length;
                    while (low < high) { int middle = (low + high) / 2; if (Keys[middle].Frame <= time) low = middle + 1; else high = middle; }
                    int index = Math.Max(0, low - 1);
                    if (index == Keys.Length - 1) return Keys[index].Tangent;
                    left = Keys[index]; right = Keys[index + 1]; span = right.Frame - left.Frame; offset = time - left.Frame;
                }
                if (span <= 0) return 0;
                double t = offset / span;
                return (6 * t * t - 6 * t) * left.Value / span + (3 * t * t - 4 * t + 1) * left.Tangent +
                    (-6 * t * t + 6 * t) * right.Value / span + (3 * t * t - 2 * t) * right.Tangent;
            }
        }
        private static Dictionary<string, NativeCurve[]> Curves(CHR0Node clip)
        {
            return clip.Children.OfType<CHR0EntryNode>().ToDictionary(e => e.Name, e => e.KeyArrays.Select(a => new NativeCurve {
                Keys = a.Entries.Select(k => new ControlKey { Frame = k._index, Value = k._value, Tangent = k._tangent }).ToArray(), Limit = a.FrameLimit, Loop = a.Loop }).ToArray(), StringComparer.Ordinal);
        }

        private static bool SameChannels(CHR0EntryNode source, ControlKey[][] channels)
        {
            return Enumerable.Range(0, 9).All(c => { var original = source.KeyArrays[c].Entries.ToArray(); return original.Length == channels[c].Length && original.Where((k, i) => k._index != channels[c][i].Frame || Bits(k._value) != Bits(channels[c][i].Value) || Bits(k._tangent) != Bits(channels[c][i].Tangent)).Any() == false; });
        }
        private static bool[] Flags(CHR0EntryNode entry) => new[] { entry.UseModelScale, entry.UseModelRotate, entry.UseModelTranslate, entry.ScaleCompensateApply, entry.ScaleCompensateParent, entry.ClassicScaleOff };
        private static void WriteFlags(CHR0EntryNode source, CHR0EntryNode target)
        { target.UseModelScale = source.UseModelScale; target.UseModelRotate = source.UseModelRotate; target.UseModelTranslate = source.UseModelTranslate; target.ScaleCompensateApply = source.ScaleCompensateApply; target.ScaleCompensateParent = source.ScaleCompensateParent; target.ClassicScaleOff = source.ClassicScaleOff; }
        private static int CountKeys(CHR0Node clip) => clip.Children.OfType<CHR0EntryNode>().Sum(e => e.KeyArrays.Sum(a => a.KeyCount));
        private static HashSet<string> Set(IEnumerable<string> values) => new HashSet<string>(values ?? new string[0], StringComparer.Ordinal);
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static string Hex(byte[] value) => BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();
        private static string G(double value) => value.ToString("G9", Invariant);
        private static long ResourceBlockBytes(string path, CHR0Node decoded)
        {
            var header = new byte[8];
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Read(header, 0, header.Length) != header.Length || header[0] != 'C' || header[1] != 'H' || header[2] != 'R' || header[3] != '0')
                    throw new InvalidDataException("Independent native resource-size verification requires a complete CHR0 header.");
                long declared = ((long)header[4] << 24) | ((long)header[5] << 16) | ((long)header[6] << 8) | header[7];
                if (declared < 8 || declared > input.Length || declared != decoded.WorkingUncompressed.Length)
                    throw new InvalidDataException("Independent CHR0 header size and freshly decoded native resource block disagree.");
                return declared;
            }
        }
        private static double Norm(double[] values, int index) => Math.Sqrt(values[index] * values[index] + values[index + 1] * values[index + 1] + values[index + 2] * values[index + 2]);
        private static double DifferenceNorm(double[] a, double[] b, int index)
        { double x = a[index] - b[index], y = a[index + 1] - b[index + 1], z = a[index + 2] - b[index + 2]; return Math.Sqrt(x * x + y * y + z * z); }
        private static void Store(double[] values, int index, Vector3 value) { values[index] = value._x; values[index + 1] = value._y; values[index + 2] = value._z; }
    }
}
