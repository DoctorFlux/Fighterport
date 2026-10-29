using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal static partial class Program
{
    sealed class WritePathEdit
    {
        public string Bone;
        public int Channel;
        public float Frame, Before, After, Tangent;
        public int FrameBits, ValueBits, TangentBits;
    }
    sealed class WritePathState
    {
        public string Header, Fingerprint, StorageKey;
        public WritePathEntry[] Entries;
        public WritePathStoredGroup[] RetainedGroups;
    }
    sealed class WritePathEntry
    {
        public string Name;
        public int SemanticFlags;
        public int[] FrameLimits;
        public bool[] Loops;
        public uint[][][] Channels;
    }
    sealed class WritePathStoredGroup
    {
        public string Bone, Format;
        public int Group;
        public uint LayoutFlags;
        public string[] ScalarPayloadKeys;
    }
    sealed class WritePathQualification
    {
        public string Route;
        public bool Completed, MetadataExact, ScalarsAndContextsExact, MatchingCompactPayloadsRetained;
        public bool SourceContentRetained = true, CopySourceBackingAndDirtyRetained = true, AuthoringScopeRetained = true;
        public int OriginalCompactGroups, RequiredCompactGroups;
        public string[] ChangedCompactGroups;
    }

    // Every route receives a fresh native owner and writes
    // new ignored artifacts. A saved file may grow or lose decoded content;
    // those outcomes are reported, never relabelled as successful validation.
    // The opt-in retention gate qualifies only the explicit application/worker
    // routes below. Legacy native routes remain diagnostic, including one-key.
    static async Task NativeChr0WritePath(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Native write-path diagnostics require the private desktop.");
        string modelPath = Path.GetFullPath(args[0]), motionPath = Path.GetFullPath(args[1]);
        if (Directory.EnumerateDirectories(output).Any() || File.Exists(Path.Combine(output, "write-path-results.json")))
            throw new IOException("Write-path diagnostics require a fresh output directory; previous evidence is retained.");
        var json = new JavaScriptSerializer { MaxJsonLength = 128 * 1024 * 1024, RecursionLimit = 200 };
        var inputs = new[] { modelPath, motionPath }.Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(path => path, FighterProject.HashFile, StringComparer.OrdinalIgnoreCase);
        string clipSelector = null, clipName; int clipAt = Array.IndexOf(args, "--clip");
        if (clipAt >= 0)
        {
            if (clipAt + 1 >= args.Length || args[clipAt + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("--clip requires an exact native name or root/n/... identity.");
            clipSelector = args[clipAt + 1];
        }
        var records = new List<object>();
        bool requireRetainedStorage = args.Contains("--require-retained-storage");
        var qualifications = new List<WritePathQualification>();
        WritePathEdit edit;
        string originalIdentity;
        int expectedOriginalCompactGroups, capturedOriginalCompactGroups;
        using (var modelRoot = NodeFactory.FromFile(null, modelPath))
        using (var motionRoot = NodeFactory.FromFile(null, motionPath))
        {
            var model = Walk(modelRoot).OfType<MDL0Node>().OrderByDescending(node => node.NumVertices).First();
            var original = WritePathClip(motionRoot, clipSelector, Path.Combine(output, "available-clips.json")); clipName = original.Name;
            originalIdentity = WritePathIdentity(original);
            expectedOriginalCompactGroups = original.Children.OfType<CHR0EntryNode>().Sum(entry => {
                var storage = PreciseCHR0EntryNode.CaptureStorage(entry);
                return storage == null ? 0 : Enumerable.Range(0, 3).Count(group => storage.MatchesGroup(entry.Keyframes, group));
            });
            capturedOriginalCompactGroups = WritePathCaptureStoredGroups(original).Length;
            var rig = BrawlRetargeter.Capture(model);
            int bodyIndex = rig.Rig.Roles["pelvis"];
            var policy = new HumanoidRetargeter(rig.Rig, rig.Rig);
            Check(!policy.TargetMotionControls.Contains(bodyIndex), "Write-path diagnostic chooses an existing body role outside protected motion controls");
            var entry = original.Children.OfType<CHR0EntryNode>().Single(node => node.Name == rig.Bones[bodyIndex].Name);
            int channel = Enumerable.Range(3, 3).First(index => entry.KeyArrays[index].KeyCount > 0);
            var key = entry.KeyArrays[channel].Entries.First();
            edit = new WritePathEdit { Bone = entry.Name, Channel = channel, Frame = key._index,
                Before = key._value, After = key._value + .25f, Tangent = key._tangent,
                FrameBits = unchecked((int)Chr0ForensicBits(key._index)), ValueBits = unchecked((int)Chr0ForensicBits(key._value + .25f)),
                TangentBits = unchecked((int)Chr0ForensicBits(key._tangent)) };
            Check(!float.IsNaN(edit.After) && !float.IsInfinity(edit.After) && Chr0ForensicBits(edit.Before) != Chr0ForensicBits(edit.After),
                "Write-path single-key value change is finite and independently distinguishable");
            WritePathSaveNative(json, output, "original", original);
            var rootBytes = WritePathBytes(motionRoot); var memberBytes = WritePathBytes(original);
            File.WriteAllBytes(Path.Combine(output, "original-root-uncompressed.bin"), rootBytes);
            File.WriteAllBytes(Path.Combine(output, "original-member-uncompressed.bin"), memberBytes);
            long offset = (long)(IntPtr)original.WorkingUncompressed.Address - (long)(IntPtr)motionRoot.WorkingUncompressed.Address;
            bool bounded = offset >= 0 && offset <= rootBytes.Length && memberBytes.Length <= rootBytes.Length - offset;
            WritePathJson(json, Path.Combine(output, "original-resource-location.json"), new {
                Input = motionPath, InputSha256 = inputs[motionPath], InputBytes = new FileInfo(motionPath).Length,
                Clip = clipName, Identity = originalIdentity, RootNodeType = motionRoot.GetType().FullName,
                RootUncompressedBytes = rootBytes.Length, RootUncompressedSha256 = WritePathHash(rootBytes),
                MemberBytes = memberBytes.Length, MemberSha256 = WritePathHash(memberBytes),
                MemberOffsetWithinRoot = bounded ? (long?)offset : null, MemberRangeWithinRoot = bounded,
                MemberByteSliceMatches = bounded && rootBytes.Skip(checked((int)offset)).Take(memberBytes.Length).SequenceEqual(memberBytes),
                OriginalIsStandalone = ReferenceEquals(original, motionRoot),
                Scope = "A raw PAC member is not a relocated standalone file: names may point into its containing native string table."
            });
        }

        void Local(string route, bool change, Func<CHR0Node, CHR0Node> prepare)
        {
            string folder = Path.Combine(output, route); Directory.CreateDirectory(folder);
            var watch = Stopwatch.StartNew();
            try
            {
                using (var root = NodeFactory.FromFile(null, motionPath))
                {
                    var source = WritePathClip(root, originalIdentity);
                    var original = WritePathCapture(source, json);
                    if (change) WritePathApplyOne(source, edit);
                    var expected = WritePathCapture(source, json); WritePathSaveNative(json, folder, "expected", source);
                    string sourceRaw = Chr0ForensicBackingHash(source); bool dirty = source.IsDirty;
                    CHR0Node candidate = prepare(source);
                    try
                    {
                        WritePathSaveNative(json, folder, "prepared", candidate);
                        string path = Path.Combine(folder, "saved.chr0"); candidate.ExportChecked(path);
                        using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
                        {
                            if (decoded == null) throw new InvalidDataException("Saved write-path output was not a CHR0.");
                            WritePathSaveNative(json, folder, "decoded", decoded);
                            var actual = WritePathCapture(decoded, json);
                            if (!route.StartsWith("native-", StringComparison.Ordinal))
                            {
                                var qualification = WritePathQualify(route, original, expected, actual);
                                qualification.SourceContentRetained = WritePathCapture(source, json).Fingerprint == expected.Fingerprint;
                                qualification.CopySourceBackingAndDirtyRetained = !route.StartsWith("copy-source-", StringComparison.Ordinal) ||
                                    sourceRaw == Chr0ForensicBackingHash(source) && dirty == source.IsDirty;
                                qualifications.Add(qualification);
                            }
                            records.Add(new { Route = route, Status = "SavedAndReopened", Edited = change, Seconds = watch.Elapsed.TotalSeconds,
                                Output = path, OutputBytes = new FileInfo(path).Length, OutputSha256 = FighterProject.HashFile(path),
                                Expected = expected.Fingerprint, Decoded = actual.Fingerprint,
                                Difference = WritePathDifference(expected, actual),
                                MatchingOriginalCompactStorage = WritePathRetainedGroups(original, expected, actual),
                                SourceOwnerContentRetained = WritePathCapture(source, json).Fingerprint == expected.Fingerprint,
                                SourceOwnerRawBufferRetained = sourceRaw == Chr0ForensicBackingHash(source),
                                SourceOwnerDirtyBefore = dirty, SourceOwnerDirtyAfter = source.IsDirty,
                                SharedLiveDocumentUsed = false, OptimizationRequested = false });
                        }
                    }
                    finally { if (!ReferenceEquals(candidate, source)) candidate.Dispose(); }
                }
            }
            catch (Exception error) { records.Add(WritePathFailure(route, folder, watch, error)); }
        }

        Local("native-no-edit", false, clip => clip);
        Local("native-one-key", true, clip => clip);
        Local("copy-source-no-edit", false, NativeAnimationOptimization.CopySource);
        Local("copy-source-one-key", true, NativeAnimationOptimization.CopySource);
        Local("promote-no-edit", false, clip => { PreciseCHR0EntryNode.Promote(clip); return clip; });
        Local("promote-one-key", true, clip => { PreciseCHR0EntryNode.Promote(clip); return clip; });
        Local("snapshot-restore-no-edit", false, WritePathRestoreSnapshot);
        Local("snapshot-restore-one-key", true, WritePathRestoreSnapshot);

        foreach (bool change in new[] { false, true })
        {
            string route = change ? "native-worker-one-key" : "native-worker-no-edit";
            string folder = Path.Combine(output, route); Directory.CreateDirectory(folder);
            var watch = Stopwatch.StartNew();
            try
            {
                WritePathState original, expected;
                using (var root = NodeFactory.FromFile(null, motionPath))
                {
                    var source = WritePathClip(root, originalIdentity); original = WritePathCapture(source, json);
                    if (change) WritePathApplyOne(source, edit);
                    expected = WritePathCapture(source, json); WritePathSaveNative(json, folder, "expected", source);
                }
                string saved = Path.Combine(folder, "saved.chr0");
                var edits = change ? new[] { new { edit.Bone, edit.Channel, edit.Frame, edit.FrameBits, Value = edit.After,
                    edit.ValueBits, edit.Tangent, edit.TangentBits } } : new object[0];
                var request = new { Operation = "export", Model = modelPath, Motion = motionPath, Entry = originalIdentity,
                    Output = saved, Edits = edits, Samples = new double[0], RewriteChannels = false, ReplaceTracks = false };
                var worker = await WritePathRunWorker(request, folder, json);
                using (var decoded = NodeFactory.FromFile(null, saved) as CHR0Node)
                {
                    if (decoded == null) throw new InvalidDataException("The native worker output did not reopen as CHR0.");
                    WritePathSaveNative(json, folder, "decoded", decoded);
                    var actual = WritePathCapture(decoded, json);
                    qualifications.Add(WritePathQualify(route, original, expected, actual));
                    records.Add(new { Route = route, Status = "SavedAndReopened", Edited = change, Seconds = watch.Elapsed.TotalSeconds,
                        Output = saved, OutputBytes = new FileInfo(saved).Length, OutputSha256 = FighterProject.HashFile(saved),
                        Worker = worker, Difference = WritePathDifference(expected, actual),
                        MatchingOriginalCompactStorage = WritePathRetainedGroups(original, expected, actual),
                        Scope = "Actual BrawlWorker export request, not contact fitting/solver acceptance.", OptimizationRequested = false });
                }
            }
            catch (Exception error) { records.Add(WritePathFailure(route, folder, watch, error)); }
        }

        // These two routes exercise the current EngineHost -> worker import ->
        // NativeTrack -> frontend snapshot writer boundary. The one-body gesture
        // can legitimately return a complete changed track; its observed edits
        // are diagnosed instead of pretending it is a scalar-only native edit.
        string controlInput = Path.Combine(output, "copy-source-no-edit", "saved.chr0");
        foreach (bool change in new[] { false, true })
        {
            string route = change ? "control-worker-body-edit" : "control-worker-no-op";
            string folder = Path.Combine(output, route); Directory.CreateDirectory(folder);
            var watch = Stopwatch.StartNew();
            try
            {
                if (!File.Exists(controlInput)) throw new InvalidDataException("The recorded independent selected-clip control input is unavailable.");
                using (var client = new AnimationControlClient())
                using (var root = NodeFactory.FromFile(null, controlInput))
                {
                    var clip = WritePathClip(root, clipName); var original = WritePathCapture(clip, json);
                    var loaded = await client.Send(new { Operation = "load", Model = modelPath, Motion = controlInput, Frame = edit.Frame });
                    int body = loaded.BodyBone ?? -1;
                    if (body < 0 || loaded.BoneNames[body] != edit.Bone) throw new InvalidDataException("The current worker did not resolve the same existing body role.");
                    WritePathJson(json, Path.Combine(folder, "load-reply.json"), loaded);
                    await client.Send(new { Operation = "begin", Bone = body, Pin = "fk:" + body, Frame = edit.Frame });
                    if (change)
                    {
                        var values = loaded.Pose[body].ToArray(); values[edit.Channel] += .25f;
                        await client.Send(new { Operation = "preview", Values = values });
                    }
                    var committed = await client.Send(new { Operation = "commit" });
                    WritePathJson(json, Path.Combine(folder, "commit-reply.json"), committed);
                    WritePathSaveNative(json, folder, "before-apply", clip);
                    WritePathApplySnapshot(clip, committed.Tracks ?? new ControlTrack[0]);
                    var prepared = WritePathCapture(clip, json); WritePathSaveNative(json, folder, "prepared", clip);
                    string saved = Path.Combine(folder, "saved.chr0"); clip.ExportChecked(saved);
                    using (var decoded = NodeFactory.FromFile(null, saved) as CHR0Node)
                    {
                        if (decoded == null) throw new InvalidDataException("The control-snapshot output did not reopen as CHR0.");
                        WritePathSaveNative(json, folder, "decoded", decoded);
                        var actual = WritePathCapture(decoded, json);
                        var qualification = WritePathQualify(route, original, prepared, actual);
                        qualification.AuthoringScopeRetained = WritePathAuthoringScope(original, prepared, change,
                            committed.Tracks?.Select(track => track.Bone).ToArray() ?? new string[0]);
                        qualifications.Add(qualification);
                        records.Add(new { Route = route, Status = "SavedAndReopened", Edited = change, Seconds = watch.Elapsed.TotalSeconds,
                            Output = saved, OutputBytes = new FileInfo(saved).Length, OutputSha256 = FighterProject.HashFile(saved),
                            ControlInput = controlInput, ControlInputSha256 = FighterProject.HashFile(controlInput),
                            committed.Committed, ReturnedTracks = committed.Tracks?.Select(track => track.Bone).ToArray(),
                            AuthoringDifference = WritePathDifference(original, prepared),
                            SerializationDifference = WritePathDifference(prepared, actual),
                            MatchingOriginalCompactStorage = WritePathRetainedGroups(original, prepared, actual),
                            Scope = "Actual FK/no-op control transport and NativeControlSnapshot.Apply; no contact or transition solver was requested.",
                            OptimizationRequested = false });
                    }
                }
            }
            catch (Exception error) { records.Add(WritePathFailure(route, folder, watch, error)); }
        }

        // Persist every observation before asserting qualification. A rejected
        // route must leave its actual bytes, metadata and failure receipt.
        WritePathJson(json, Path.Combine(output, "write-path-results.json"), new {
            Schema = "fighterport-native-write-path-1", DiagnosticOnly = true, PipelineAcceptanceClaimed = false,
            RetainedStorageQualificationRequested = requireRetainedStorage, RetainedStorageQualification = qualifications,
            ExpectedOriginalCompactGroups = expectedOriginalCompactGroups, CapturedOriginalCompactGroups = capturedOriginalCompactGroups,
            OriginalInputs = inputs.Select(pair => new { Path = pair.Key, Sha256Before = pair.Value, Sha256After = FighterProject.HashFile(pair.Key) }).ToArray(),
            Clip = clipName, RequestedClipSelector = clipSelector, OriginalIdentity = originalIdentity, SingleNativeEdit = edit, Records = records,
            PrivateDesktop = true, OSInputInjected = false, ScreenCopied = false, SharedLiveDocumentUsed = false,
            OptimizationRequested = false, Runtime = Environment.Version.ToString(), PointerBytes = IntPtr.Size,
            AppSha256 = FighterProject.HashFile(typeof(ModelForm).Assembly.Location), NativeSha256 = FighterProject.HashFile(typeof(CHR0Node).Assembly.Location),
            Limits = "Size growth and legacy decoded losses are observations, not passing quality gates. The opt-in gate requires exact decoded metadata/scalars and unchanged original matching compact payloads only in explicitly qualified application/worker routes. Opaque scaling-rule retention is metadata evidence, not proof of nonzero-mode motion support. I12 FrameScale is not included in decoded fingerprints or immutable compact-group plans: its raw changes remain separately reported by byte forensics, with runtime significance unqualified. Prepared-node raw format flags can be stale until the size/rebuild pass: only the original encoded input and freshly saved physical output are authoritative codec layouts."
        });
        Check(records.Count == 12, "Write-path diagnostic records all twelve actual attempted routes, including explicit failures or decoded losses");
        Check(inputs.All(pair => FighterProject.HashFile(pair.Key) == pair.Value), "Every original model/motion source hash survives all diagnostic writer and worker routes");
        if (requireRetainedStorage)
        {
            Check(expectedOriginalCompactGroups > 0 && capturedOriginalCompactGroups == expectedOriginalCompactGroups,
                "The actual fixture supplies positive original compact coverage and every eligible group has independently owner-bounded encoded layout evidence");
            string[] qualifiedRoutes = { "copy-source-no-edit", "copy-source-one-key", "promote-no-edit", "promote-one-key",
                "snapshot-restore-no-edit", "snapshot-restore-one-key", "native-worker-no-edit", "native-worker-one-key",
                "control-worker-no-op", "control-worker-body-edit" };
            foreach (string route in qualifiedRoutes)
            {
                var qualification = qualifications.SingleOrDefault(item => item.Route == route);
                Check(qualification != null && qualification.Completed, route + " completes actual native serialization and fresh reopen");
                Check(qualification.MetadataExact, route + " retains exact duration, loop, version, original path, scaling rule and user metadata");
                Check(qualification.ScalarsAndContextsExact, route + " retains expected ordered scalar IEEE tuples, entry order, semantic flags and native channel contexts");
                Check(qualification.MatchingCompactPayloadsRetained, route + " retains every unchanged original matching compact group payload, fixed slot and layout flag");
                if (!route.StartsWith("native-worker-", StringComparison.Ordinal) && !route.StartsWith("control-worker-", StringComparison.Ordinal))
                    Check(qualification.SourceContentRetained, route + " preserves its independently owned expected source content");
                if (route.StartsWith("copy-source-", StringComparison.Ordinal))
                    Check(qualification.CopySourceBackingAndDirtyRetained, route + " preserves independent source backing bytes and dirty state");
                if (route.StartsWith("control-worker-", StringComparison.Ordinal))
                    Check(qualification.AuthoringScopeRetained, route + " preserves original metadata and every track outside its explicitly returned FK edit set");
            }
        }
    }

    static CHR0Node WritePathClip(ResourceNode root, string selector, string availableFile = null)
    {
        if (root == null) throw new InvalidDataException("Native diagnostic input did not load.");
        var clips = Walk(root).OfType<CHR0Node>().ToArray();
        bool identity = selector == "root" || selector != null && selector.StartsWith("root/", StringComparison.Ordinal);
        var matches = selector == null ? clips : clips.Where(clip => identity ? WritePathIdentity(clip) == selector : clip.Name == selector).ToArray();
        if (matches.Length == 1) return matches[0];
        if (availableFile != null) WritePathAvailableClips(clips, matches, selector, availableFile);
        string reason = matches.Length == 0 ? "No native CHR0 matches" : "The native CHR0 selector is ambiguous for";
        throw new InvalidDataException(reason + " '" + (selector ?? "<unspecified>") + "' (" + matches.Length + " matches). " +
            "Choose one exact --clip root/n/... identity; no first matching name is selected." +
            (availableFile == null ? "" : " Inspect " + availableFile + "."));
    }
    static void WritePathAvailableClips(CHR0Node[] clips, CHR0Node[] matches, string selector, string path)
    {
        const int maximumRows = 512, maximumText = 1024;
        string Bounded(string value) => value == null || value.Length <= maximumText ? value : value.Substring(0, maximumText);
        // Prioritize ambiguous matches so the requested exact member remains
        // discoverable even when a large resource needs a truncated catalogue.
        var listed = matches.Concat(clips).Distinct().Take(maximumRows).Select(clip => {
            string contentKey = null, error = null;
            try { contentKey = NativeAnimationOptimization.SourceContentKey(clip); }
            catch (Exception failure) { error = Bounded(failure.GetType().Name + ": " + failure.Message); }
            string identity = WritePathIdentity(clip);
            return new { Name = Bounded(clip.Name), Identity = Bounded(identity), Frames = clip.FrameCount,
                EntryCount = clip.Children.Count, ContentKey = Bounded(contentKey), ContentKeyError = error,
                NameTruncated = clip.Name != null && clip.Name.Length > maximumText, IdentityTruncated = identity.Length > maximumText };
        }).ToArray();
        var json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024, RecursionLimit = 20 };
        WritePathJson(json, path, new { Schema = "fighterport-native-write-path-available-clips-1", Selector = Bounded(selector),
            TotalClipCount = clips.Length, MatchingClipCount = matches.Length, MaximumRows = maximumRows,
            Truncated = clips.Length > listed.Length, Clips = listed,
            Scope = "Read-only native identity discovery; no candidate, export or first-name substitution was attempted." });
    }
    static string WritePathIdentity(ResourceNode resource)
    {
        var parts = new List<int>();
        while (resource.Parent != null) { parts.Add(resource.Index); resource = resource.Parent; }
        parts.Reverse(); return "root" + (parts.Count == 0 ? "" : "/" + string.Join("/", parts));
    }
    static void WritePathApplyOne(CHR0Node clip, WritePathEdit edit)
    {
        var entry = clip.Children.OfType<CHR0EntryNode>().Single(node => node.Name == edit.Bone);
        var key = entry.KeyArrays[edit.Channel].Entries.First(k => Chr0ForensicBits(k._index) == unchecked((uint)edit.FrameBits));
        if (Chr0ForensicBits(key._value) != Chr0ForensicBits(edit.Before) || Chr0ForensicBits(key._tangent) != unchecked((uint)edit.TangentBits))
            throw new InvalidDataException("The independent one-key diagnostic baseline changed.");
        key._value = edit.After; entry.SignalPropertyChange();
    }
    static CHR0Node WritePathRestoreSnapshot(CHR0Node clip)
    {
        var snapshot = typeof(ModelForm).GetNestedType("NativeControlSnapshot", BindingFlags.NonPublic)
            ?? throw new NotSupportedException("Pinned native control snapshot type is unavailable.");
        object captured = snapshot.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { clip, clip.Children.Select(entry => entry.Name).ToArray() });
        snapshot.GetMethod("Restore", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(captured, new object[] { clip });
        return clip;
    }
    static void WritePathApplySnapshot(CHR0Node clip, ControlTrack[] tracks)
        => typeof(ModelForm).GetNestedType("NativeControlSnapshot", BindingFlags.NonPublic)
            .GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { clip, tracks });
    static WritePathState WritePathCapture(CHR0Node clip, JavaScriptSerializer json)
    {
        var state = new WritePathState {
            Header = json.Serialize(new { clip.Name, clip.Version, clip.FrameCount, clip.Loop, clip.OriginalPath,
                ScalingRule = NativeAnimationOptimization.SourceScalingRule(clip),
                Users = clip.UserEntries.Cast<BrawlLib.SSBB.UserDataClass>().Select(user => new { user.Name, Type = (int)user.DataType, user.Entries }).ToArray() }),
            StorageKey = NativeAnimationOptimization.StorageKey(clip),
            RetainedGroups = WritePathCaptureStoredGroups(clip),
            Entries = clip.Children.OfType<CHR0EntryNode>().Select(entry => new WritePathEntry {
                Name = entry.Name, SemanticFlags = (entry.UseModelScale ? 1 : 0) | (entry.UseModelRotate ? 2 : 0) | (entry.UseModelTranslate ? 4 : 0) |
                    (entry.ScaleCompensateApply ? 8 : 0) | (entry.ScaleCompensateParent ? 16 : 0) | (entry.ClassicScaleOff ? 32 : 0),
                FrameLimits = entry.KeyArrays.Select(array => array.FrameLimit).ToArray(), Loops = entry.KeyArrays.Select(array => array.Loop).ToArray(),
                Channels = entry.KeyArrays.Select(array => array.Entries.Select(key => new[] { Chr0ForensicBits(key._index), Chr0ForensicBits(key._value), Chr0ForensicBits(key._tangent) }).ToArray()).ToArray()
            }).ToArray()
        };
        state.Fingerprint = WritePathHash(Encoding.UTF8.GetBytes(json.Serialize(new { state.Header, state.Entries })));
        return state;
    }
    static WritePathStoredGroup[] WritePathCaptureStoredGroups(CHR0Node clip)
    {
        var groups = new List<WritePathStoredGroup>();
        foreach (var entry in clip.Children.OfType<CHR0EntryNode>())
        {
            var storage = PreciseCHR0EntryNode.CaptureStorage(entry);
            // Only actual original/fresh encoded members have authoritative
            // layout flags. Newly prepared entries may have stale/no backing.
            var backing = entry.WorkingUncompressed;
            if (storage == null || backing.Address == null) continue;
            // CHR0 leaf DataSources normally have length zero: the complete
            // native owner/map establishes the table bound. Skipping those
            // leaves would make compact-payload acceptance vacuous.
            if (!WritePathHasOwnerBytes(entry, (IntPtr)backing.Address, 8))
                throw new InvalidDataException("Retained compact layout flags have no bounded native owner.");
            byte[] header = new byte[8]; Marshal.Copy((IntPtr)backing.Address, header, 0, 8);
            uint flags = (uint)header[4] << 24 | (uint)header[5] << 16 | (uint)header[6] << 8 | header[7];
            for (int group = 0; group < 3; group++)
            {
                if (!storage.MatchesGroup(entry.Keyframes, group)) continue;
                uint formatMask = group == 0 ? 3u << 25 : group == 1 ? 7u << 27 : 3u << 30;
                uint mask = (1u << (22 + group)) | (1u << (4 + group)) | (7u << (13 + group * 3)) | formatMask;
                groups.Add(new WritePathStoredGroup { Bone = entry.Name, Group = group, Format = storage.Format(group).ToString(),
                    LayoutFlags = flags & mask,
                    ScalarPayloadKeys = Enumerable.Range(group * 3, 3).Select(channel => storage[channel]?.ContentKey ??
                        "fixed:" + string.Join(",", entry.KeyArrays[channel].Entries.Select(key => Chr0ForensicBits(key._value).ToString("X8")))).ToArray() });
            }
        }
        return groups.ToArray();
    }
    static bool WritePathHasOwnerBytes(ResourceNode node, IntPtr pointer, long count)
    {
        if (pointer == IntPtr.Zero || count < 0) return false;
        ulong Address(IntPtr value) => IntPtr.Size == 4 ? unchecked((uint)value.ToInt32()) : unchecked((ulong)value.ToInt64());
        ulong address = Address(pointer);
        bool Contains(DataSource source)
        {
            IntPtr start; long length;
            if (source.Map != null && source.Map.Address != null && source.Map.Length > 0)
            { start = (IntPtr)source.Map.Address; length = source.Map.Length; }
            else { start = (IntPtr)source.Address; length = source.Length; }
            if (start == IntPtr.Zero || length < 1) return false;
            ulong begin = Address(start);
            return address >= begin && address - begin <= (ulong)length && (ulong)count <= (ulong)length - (address - begin);
        }
        var seen = new HashSet<ResourceNode>();
        for (var owner = node; owner != null && seen.Add(owner); owner = owner.Parent)
            if (Contains(owner.UncompressedSource) || Contains(owner.WorkingUncompressed)) return true;
        return false;
    }
    static bool WritePathGroupUnchanged(WritePathState before, WritePathState after, string bone, int group)
    {
        var a = before.Entries.SingleOrDefault(entry => entry.Name == bone);
        var b = after.Entries.SingleOrDefault(entry => entry.Name == bone);
        if (a == null || b == null || a.SemanticFlags != b.SemanticFlags) return false;
        return Enumerable.Range(group * 3, 3).All(channel => a.FrameLimits[channel] == b.FrameLimits[channel] &&
            a.Loops[channel] == b.Loops[channel] && WritePathChannelEqual(a.Channels[channel], b.Channels[channel]));
    }
    static bool WritePathChannelEqual(uint[][] a, uint[][] b)
        => a.Length == b.Length && a.SelectMany(row => row).SequenceEqual(b.SelectMany(row => row));
    static WritePathStoredGroup[] WritePathRequiredGroups(WritePathState original, WritePathState expected)
        => original.RetainedGroups.Where(group => WritePathGroupUnchanged(original, expected, group.Bone, group.Group)).ToArray();
    static string[] WritePathChangedStoredGroups(WritePathState original, WritePathState expected, WritePathState actual)
        => WritePathRequiredGroups(original, expected).Where(group => {
            var other = actual.RetainedGroups.SingleOrDefault(item => item.Bone == group.Bone && item.Group == group.Group);
            return other == null || group.Format != other.Format || group.LayoutFlags != other.LayoutFlags ||
                !group.ScalarPayloadKeys.SequenceEqual(other.ScalarPayloadKeys);
        }).Select(group => group.Bone + ":" + new[] { "S", "R", "T" }[group.Group]).ToArray();
    static object WritePathRetainedGroups(WritePathState original, WritePathState expected, WritePathState actual)
    {
        var required = WritePathRequiredGroups(original, expected); var changed = WritePathChangedStoredGroups(original, expected, actual);
        return new { OriginalCompactGroups = original.RetainedGroups.Length, RequiredCompactGroups = required.Length,
            RequiredGroups = required.Select(group => group.Bone + ":" + new[] { "S", "R", "T" }[group.Group]).ToArray(),
            ChangedGroups = changed, Retained = changed.Length == 0,
            Scope = "Complete existing immutable compact payloads (including their retained padding and terminal samples), fixed scalar values and native group layout flags. Relocated offsets and I12 FrameScale are outside this assertion." };
    }
    static WritePathQualification WritePathQualify(string route, WritePathState original, WritePathState expected, WritePathState actual)
    {
        var changed = WritePathChangedStoredGroups(original, expected, actual);
        return new WritePathQualification { Route = route, Completed = true, MetadataExact = expected.Header == actual.Header,
            ScalarsAndContextsExact = expected.Fingerprint == actual.Fingerprint,
            OriginalCompactGroups = original.RetainedGroups.Length, RequiredCompactGroups = WritePathRequiredGroups(original, expected).Length,
            ChangedCompactGroups = changed, MatchingCompactPayloadsRetained = changed.Length == 0 };
    }
    static bool WritePathAuthoringScope(WritePathState original, WritePathState prepared, bool changed, string[] returnedTracks)
    {
        if (original.Header != prepared.Header || !original.Entries.Select(entry => entry.Name).SequenceEqual(prepared.Entries.Select(entry => entry.Name))) return false;
        if (!changed) return original.Fingerprint == prepared.Fingerprint;
        if (returnedTracks.Any(name => !original.Entries.Any(entry => entry.Name == name))) return false;
        foreach (var entry in original.Entries)
        {
            var other = prepared.Entries.Single(node => node.Name == entry.Name);
            if (entry.SemanticFlags != other.SemanticFlags) return false;
            if (!returnedTracks.Contains(entry.Name) && !Enumerable.Range(0, 3).All(group => WritePathGroupUnchanged(original, prepared, entry.Name, group))) return false;
        }
        return true;
    }
    static object WritePathDifference(WritePathState before, WritePathState after)
    {
        long fields = 0; int channels = 0, flags = 0, contexts = 0;
        var details = new List<object>();
        bool order = before.Entries.Select(entry => entry.Name).SequenceEqual(after.Entries.Select(entry => entry.Name));
        var added = after.Entries.Where(entry => !before.Entries.Any(prior => prior.Name == entry.Name)).ToArray();
        var removed = before.Entries.Where(entry => !after.Entries.Any(next => next.Name == entry.Name)).ToArray();
        foreach (var entry in added)
        {
            channels += entry.Channels.Count(array => array.Length != 0); fields += entry.Channels.Sum(array => (long)array.Length * 3);
            if (details.Count < 200) details.Add(new { Bone = entry.Name, AddedEntry = true });
        }
        foreach (var entry in before.Entries)
        {
            var other = after.Entries.SingleOrDefault(node => node.Name == entry.Name);
            if (other == null) { channels += 9; if (details.Count < 200) details.Add(new { Bone = entry.Name, MissingEntry = true }); continue; }
            if (entry.SemanticFlags != other.SemanticFlags) flags++;
            for (int channel = 0; channel < 9; channel++)
            {
                if (entry.FrameLimits[channel] != other.FrameLimits[channel] || entry.Loops[channel] != other.Loops[channel]) contexts++;
                var a = entry.Channels[channel]; var b = other.Channels[channel];
                long changed = Math.Abs(a.Length - b.Length) * 3L;
                for (int row = 0; row < Math.Min(a.Length, b.Length); row++) for (int value = 0; value < 3; value++) if (a[row][value] != b[row][value]) changed++;
                if (changed > 0) { channels++; fields += changed; if (details.Count < 200) details.Add(new { Bone = entry.Name, Channel = channel, BeforeRows = a.Length, AfterRows = b.Length, DifferentOrderedTupleFields = changed }); }
            }
        }
        return new { ExactDecodedContent = before.Fingerprint == after.Fingerprint, HeaderAndUserMetadataIdentical = before.Header == after.Header,
            EntryOrderIdentical = order, EntryCountBefore = before.Entries.Length, EntryCountAfter = after.Entries.Length,
            AddedEntries = added.Select(entry => entry.Name).ToArray(), RemovedEntries = removed.Select(entry => entry.Name).ToArray(),
            ChangedSemanticFlagEntries = flags, ChangedChannelContexts = contexts, ChangedScalarChannels = channels,
            DifferentOrderedTupleFields = fields, StorageKeyIdentical = before.StorageKey == after.StorageKey, Details = details,
            Scope = "Strict ordered float-bit comparison; no tolerance, pose/contact acceptance or I12 FrameScale equivalence is implied." };
    }
    static void WritePathSaveNative(JavaScriptSerializer json, string folder, string label, CHR0Node clip)
    {
        WritePathJson(json, Path.Combine(folder, label + "-native.json"), Chr0ForensicProjection(clip));
        WritePathJson(json, Path.Combine(folder, label + "-semantic.json"), WritePathCapture(clip, json));
    }
    static void WritePathJson(JavaScriptSerializer json, string path, object value) => File.WriteAllText(path, json.Serialize(value), new UTF8Encoding(false));
    static byte[] WritePathBytes(ResourceNode node)
    {
        var data = node.WorkingUncompressed;
        if (data.Length < 0 || data.Length > FighterProject.MaximumAssetBytes) throw new InvalidDataException("Diagnostic native backing exceeds the existing asset budget.");
        var bytes = new byte[data.Length]; if (bytes.Length > 0) Marshal.Copy((IntPtr)data.Address, bytes, 0, bytes.Length); return bytes;
    }
    static string WritePathHash(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    static object WritePathFailure(string route, string folder, Stopwatch watch, Exception error)
    {
        if (error is TargetInvocationException invocation && invocation.InnerException != null) error = invocation.InnerException;
        string output = Path.Combine(folder, "saved.chr0");
        return new { Route = route, Status = "ExplicitFailure", Seconds = watch.Elapsed.TotalSeconds,
            Error = error.GetType().FullName + ": " + error.Message, OutputExists = File.Exists(output),
            Output = output, Scope = "Failure is diagnostic evidence and is not a qualified save/export result." };
    }
    static async Task<object> WritePathRunWorker(object request, string folder, JavaScriptSerializer json)
    {
        string engine = Environment.GetEnvironmentVariable("FIGHTERPORT_ENGINE_HOST")
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine", "FighterPort.EngineHost.exe");
        string worker = Path.Combine(Path.GetDirectoryName(engine), "worker", "FighterPort.BrawlWorker.exe");
        if (!File.Exists(worker)) throw new FileNotFoundException("The qualified worker is unavailable.", worker);
        string input = Path.Combine(folder, "worker-request.json"), response = Path.Combine(folder, "worker-response.json");
        WritePathJson(json, input, request);
        var start = new ProcessStartInfo(worker, "\"" + input + "\" \"" + response + "\"") {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        using (var process = Process.Start(start))
        {
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            bool exited = await Task.Run(() => process.WaitForExit(120000));
            if (!exited) { process.Kill(); throw new TimeoutException("The diagnostic worker exceeded the existing two-minute transport timeout."); }
            File.WriteAllText(Path.Combine(folder, "worker-stdout.txt"), await stdout, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, "worker-stderr.txt"), await stderr, new UTF8Encoding(false));
            if (process.ExitCode != 0 || !File.Exists(response)) throw new InvalidDataException("The native worker failed; inspect its recorded request and stderr.");
            return new { Executed = true, Path = worker, Sha256 = FighterProject.HashFile(worker), ExitCode = process.ExitCode,
                Request = input, RequestSha256 = FighterProject.HashFile(input), Response = response, ResponseSha256 = FighterProject.HashFile(response),
                SamplesRequested = 0, DesktopInputOrCaptureRequested = false };
        }
    }
}
