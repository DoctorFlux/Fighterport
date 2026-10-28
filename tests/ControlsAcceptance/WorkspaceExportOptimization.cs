using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;

internal static partial class Program
{
    static async Task WorkspaceExportOptimization(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Export-copy qualification requires the private desktop.");
        string previousPreferences = Environment.GetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT");
        string previousLibrary = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", Path.Combine(output, "isolated-export-settings"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "isolated-library"));
        try
        {
            string preferencesPath = Path.Combine(output, "preferences-fixture", "export.json");
            var preferences = new NativeExportOptimizationPreferences(preferencesPath);
            Check(preferences.Read().Policy == NativeExportOptimizationPolicy.Exact, "New export settings default to Exact");
            var limits = new NativeExportOptimizationSettings { Policy = NativeExportOptimizationPolicy.ErrorBounded, SearchEffort = NativeExportSearchEffort.Thorough,
                RotationDegrees = .05, Translation = .001, Scale = .00005, GeometryRelative = .0005, RotationDerivative = .05, TranslationDerivative = .001, ScaleDerivative = .00005 };
            preferences.Save(limits); var roundTrip = preferences.Read();
            Check(roundTrip.StableKey == limits.StableKey && preferences.LastError == null, "Atomic local preferences retain explicit tighter limits and independent effort");
            Check(new NativeExportOptimizationPreferences(Path.Combine(output, "separate-preferences", "export.json")).Read().Policy == NativeExportOptimizationPolicy.Exact,
                "Preference instances and background-test paths are isolated");
            File.WriteAllText(preferencesPath, "{not valid JSON");
            Check(preferences.Read().Policy == NativeExportOptimizationPolicy.Exact && preferences.LastError != null, "Corrupt preferences conservatively select Exact");
            File.WriteAllText(preferencesPath, new string('x', 16385));
            Check(preferences.Read().Policy == NativeExportOptimizationPolicy.Exact && preferences.LastError != null, "Oversized preferences are bounded and select Exact");
            limits.Translation = .0021; bool rejected = false; try { preferences.Save(limits); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Saved settings cannot silently relax an approved ceiling");
            using (var controls = new WorkspaceExportOptimizationOptions(new NativeExportOptimizationSettings()))
            {
                int changes = 0; controls.SettingsChanged += (s, e) => changes++;
                SimpleControl<ComboBox>(controls, "exportOptimizationPolicy").SelectedIndex = 1;
                SimpleControl<ComboBox>(controls, "exportOptimizationEffort").SelectedIndex = 1;
                SimpleControl<NumericUpDown>(controls, "exportOptimizationLimit0").Value = .05m;
                var read = controls.Read();
                Check(changes == 3 && read.Policy == NativeExportOptimizationPolicy.ErrorBounded && read.SearchEffort == NativeExportSearchEffort.Thorough && read.RotationDegrees == .05,
                    "Policy, effort and explicit-limit changes each invalidate a prepared preview");
            }
            using (var startup = MainForm.StartViewerApplication())
            {
                var form = (ModelForm)Field(MainForm.Instance, "_fighterForm");
                try
                {
                    Call(form, "ImportFighterModel", args[0]); Call(form, "ImportFighterAnimations", new[] { new[] { args[1] } });
                    var editor = form.FighterEditor;
                    var clip = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First(c => c.Name == "Wait1");
                    editor.SelectedCHR0 = clip; editor.SetFrame(3); await SimpleControlsReady(form); await Await<ControlReply>(Call(form, "LoadControls")); editor.SetFrame(3);
                    string master = SetNativeContent(clip), masterStorage = NativeAnimationOptimization.StorageKey(clip);
                    var target = editor.TargetModel; var bone = editor.SelectedBone; int frame = editor.CurrentFrame, undo = editor._undoSaves.Count;
                    bool dirty = clip.IsDirty; var siblings = OptimizationUntouchedBytes(form, clip);
                    bool expectedDirty = dirty;
                    var statePhases = new List<object>();
                    void Phase(string name)
                    {
                        string content = SetNativeContent(clip), storage = NativeAnimationOptimization.StorageKey(clip);
                        string Hash(string text) { using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", ""); }
                        statePhases.Add(new { Phase = name, ContentSame = content == master, ContentSha256 = Hash(content), StorageSame = storage == masterStorage, StorageSha256 = Hash(storage),
                            TargetSame = ReferenceEquals(editor.TargetModel, target), ClipSame = ReferenceEquals(editor.SelectedCHR0, clip), BoneSame = ReferenceEquals(editor.SelectedBone, bone),
                            OriginalBone = bone?.Name, ActualBone = editor.SelectedBone?.Name, ExpectedFrame = frame, ActualFrame = editor.CurrentFrame, ExpectedUndo = undo, ActualUndo = editor._undoSaves.Count,
                            Redo = editor._redoSaves.Count, InitialDirty = dirty, ActualDirty = clip.IsDirty });
                        File.WriteAllText(Path.Combine(output, "state-phases.json"), new JavaScriptSerializer().Serialize(statePhases));
                    }
                    Phase("initial");
                    if (Environment.GetEnvironmentVariable("FIGHTERPORT_EXPORT_NAVIGATION_PROBE") == "1")
                    {
                        File.WriteAllText(Path.Combine(output, "diagnostic-only.txt"), "Navigation/setup isolation only. This run is excluded from export acceptance.");
                        clip.Loop = !clip.Loop; clip.Loop = !clip.Loop; Phase("diagnostic-stale-loop-restored");
                        bool probeDirty = clip.IsDirty;
                        var other = Walk(clip.RootNode).OfType<CHR0Node>().First(c => !ReferenceEquals(c, clip) && c.Name == "Fall");
                        editor.SelectedCHR0 = other; await SimpleControlsReady(form); await Await<ControlReply>(Call(form, "LoadControls")); Phase("diagnostic-sibling-setup");
                        editor.SelectedCHR0 = clip; await SimpleControlsReady(form); await Await<ControlReply>(Call(form, "LoadControls")); editor.SetFrame(frame); editor.SelectedBone = bone;
                        Phase("diagnostic-original-setup-restored");
                        Check(SetNativeContent(clip) == master && NativeAnimationOptimization.StorageKey(clip) == masterStorage && ReferenceEquals(editor.TargetModel, target) &&
                            ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.SelectedBone, bone) && editor.CurrentFrame == frame && editor._undoSaves.Count == undo && clip.IsDirty == probeDirty,
                            "DIAGNOSTIC ONLY: explicit native clip selection/setup retains the initial state without any export analysis");
                        return;
                    }
                    NativeExportFitAcceptanceChecks(args[0], Path.Combine(output, "plain-fit-acceptance"));
                    NativeMotionPacVerificationChecks(args[0], args[1], Path.Combine(output, "motion-pac"));
                    using (var flagged = NativeAnimationOptimization.CopySource(clip))
                    {
                        var entry = flagged.Children.OfType<CHR0EntryNode>().First(e => ((MDL0Node)target).AllBones.Any(b => b.Name == e.Name));
                        entry.ScaleCompensateApply = !((MDL0Node)target).AllBones.Single(b => b.Name == entry.Name).SegScaleCompApply;
                        var reasons = (string[])form.GetType().GetMethod("ExportNativeSemanticLimitations", Private | BindingFlags.Static).Invoke(null, new object[] { (MDL0Node)target, flagged });
                        Check(reasons.Any(reason => reason.Contains("flags differ")), "Mismatched native animation/model scale flags explicitly disable bounded runtime qualification");
                    }
                    string referencePath = Path.Combine(output, "finished-target-reference.chr0"); form.ExportSimpleWorkspaceResult(referencePath);
                    long referenceBytes = new FileInfo(referencePath).Length;
                    object frozen = Call(form, "FreezeWorkspaceExport");
                    try
                    {
                        var originalContext = Field(frozen, "Optimization");
                        Check((bool)Field(originalContext, "ProtectionKnown"), "Persisted reviewed native body/motion controls qualify ordinary clips without an unrelated retarget coordinate review");
                        string storedControls = (string)Field(originalContext, "Controls");
                        object Camel(object value)
                        {
                            if (value is Dictionary<string, object> dictionary) return dictionary.ToDictionary(pair => char.ToLowerInvariant(pair.Key[0]) + pair.Key.Substring(1), pair => Camel(pair.Value));
                            if (value is IEnumerable sequence && !(value is string)) return sequence.Cast<object>().Select(Camel).ToArray();
                            return value;
                        }
                        var transport = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 200 };
                        var camel = (Dictionary<string, object>)Camel(transport.DeserializeObject(storedControls));
                        var parser = form.GetType().GetMethod("ReadExportProtectionContext", Private | BindingFlags.Static);
                        object[] contextArgs = { (MDL0Node)target, transport.Serialize(camel), false, null };
                        Check((bool)parser.Invoke(null, contextArgs) && !(bool)contextArgs[2], "Camel-case persisted control bindings and complete empty constraints qualify identically");
                        camel["authoringContacts"] = new object[] { new Dictionary<string, object>() };
                        contextArgs = new object[] { (MDL0Node)target, transport.Serialize(camel), false, null };
                        Check((bool)parser.Invoke(null, contextArgs) && (bool)contextArgs[2], "Nonempty camel-case contact collections are detected before any bounded export");
                        camel.Remove("contacts"); contextArgs = new object[] { (MDL0Node)target, transport.Serialize(camel), false, null };
                        Check(!(bool)parser.Invoke(null, contextArgs), "Missing constraint context cannot qualify bounded export");
                        var exact = new NativeExportOptimizationSettings();
                        await (Task)Call(form, "PrepareOptimizedExportSnapshot", frozen, exact, CancellationToken.None, null);
                        Phase("exact-prepared");
                        var candidate = (CHR0Node)Field(frozen, "Candidate");
                        Check(SetNativeContent((CHR0Node)Field(frozen, "Master")) == master, "Compression reference is the same finished target master, never a donor or prior export");
                        string first = Path.Combine(output, "exact-copy.chr0"); Call(form, "ExportWorkspaceSnapshot", frozen, first);
                        Check(new FileInfo(first).Length <= referenceBytes && NativeChr0SizeReport.Read(first).AccountedBytes == new FileInfo(first).Length,
                            "Actual independent standalone byte accounting rejects growth relative to finished master");
                        Check((string)Field(frozen, "ByteReport") != null && (string)Field(frozen, "OptimizationReport") != null, "Export copy reports actual bytes, requested policy and qualification/fallback");
                        long preparationMilliseconds = (long)Field(frozen, "PreparationMilliseconds");
                        Check(preparationMilliseconds >= 0 && ((string)Field(frozen, "OptimizationReport")).Contains("Preparation wall time:"),
                            "Export preparation records wall time including search, encoding and independent validation");
                        string candidateContent = SetNativeContent(candidate);
                        await (Task)Call(form, "PrepareOptimizedExportSnapshot", frozen, exact, CancellationToken.None, null);
                        Check(ReferenceEquals(candidate, Field(frozen, "Candidate")) && SetNativeContent(candidate) == candidateContent,
                            "Repeated unchanged export reuses the frozen preparation without cumulative approximation");
                        Check((long)Field(frozen, "PreparationMilliseconds") == preparationMilliseconds,
                            "Cached unchanged export retains the original preparation duration instead of reporting a new search");
                        string second = Path.Combine(output, "exact-copy-repeat.chr0"); Call(form, "ExportWorkspaceSnapshot", frozen, second);
                        Phase("exact-published-twice");
                        Check(FighterProject.HashFile(first) == FighterProject.HashFile(second), "Repeated export is deterministic and uses one unchanged master");
                        string beforeGrowthHash = FighterProject.HashFile(first); bool growthRejected = false;
                        form.ExportCandidateWritten = path => { using (var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None)) file.SetLength(file.Length + (long)Field(frozen, "MasterBytes") + 1); };
                        try { Call(form, "ExportWorkspaceSnapshot", frozen, first); } catch (TargetInvocationException error) { growthRejected = error.InnerException is InvalidDataException; }
                        finally { form.ExportCandidateWritten = null; }
                        Check(growthRejected && FighterProject.HashFile(first) == beforeGrowthHash, "Final physical no-growth gate rejects appended bytes after candidate write and preserves an existing destination");
                        if ((long)Field(frozen, "MasterBytes") > new FileInfo(first).Length)
                        {
                            bool changedBytesRejected = false;
                            form.ExportCandidateWritten = path => { using (var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None)) file.WriteByte(0); };
                            try { Call(form, "ExportWorkspaceSnapshot", frozen, first); } catch (TargetInvocationException error) { changedBytesRejected = error.InnerException is InvalidDataException; }
                            finally { form.ExportCandidateWritten = null; }
                            Check(changedBytesRejected && FighterProject.HashFile(first) == beforeGrowthHash, "Final prepared-byte identity rejects an appended byte even below the finished-master budget");
                        }
                        using (var decoded = NodeFactory.FromFile(null, first) as CHR0Node)
                            Check(decoded != null && NativeDifference(candidate, decoded) == null, "Published copy independently decodes to every prepared key, tangent, flag and time");
                        using (FlowModal("workspaceFilePath", dialog => SimpleControl<Button>(dialog, "workspaceFileCancel").PerformClick()))
                            Call(form, "ExportPreparedOptimization", candidate, null);
                        Phase("standalone-cancelled");
                        Check(SetNativeContent(clip) == master && editor._undoSaves.Count == undo, "Cancelling standalone prepared-copy export preserves editable master and Undo");
                        string standalone = Path.Combine(output, "standalone-prepared-copy.chr0");
                        using (FlowModal("workspaceFilePath", dialog => { SimpleControl<TextBox>(dialog, "workspaceFilePath").Text = standalone; SimpleControl<Button>(dialog, "workspaceFileAccept").PerformClick(); }))
                            Call(form, "ExportPreparedOptimization", candidate, null);
                        Phase("standalone-published");
                        Check(File.Exists(standalone) && FighterProject.HashFile(standalone) == FighterProject.HashFile(first), "Standalone prepared result uses the same verified publication boundary without live Apply");
                        Check(clip.IsDirty == dirty, "Analysis, repeated publication and standalone cancellation preserve native master dirty state");
                        string prior = FighterProject.HashFile(first); clip.Loop = !clip.Loop;
                        bool stale = false; try { Call(form, "ExportWorkspaceSnapshot", frozen, first); } catch (TargetInvocationException error) { stale = error.InnerException is InvalidOperationException; }
                        clip.Loop = !clip.Loop;
                        expectedDirty = clip.IsDirty;
                        Phase("stale-loop-restored");
                        Check(stale && FighterProject.HashFile(first) == prior, "Stale master dependencies preserve an existing destination");
                    }
                    finally { ((IDisposable)frozen).Dispose(); }
                    object renamedSlot = Call(form, "FreezeWorkspaceExport");
                    try
                    {
                        string slotName = "Longer same-slot reference name for independent native byte comparison";
                        Call(form, "PrepareSlotMetadataExport", renamedSlot, slotName, clip.Version);
                        var slotMaster = (CHR0Node)Field(renamedSlot, "Master");
                        var slotCandidate = (CHR0Node)Field(renamedSlot, "Candidate");
                        Check(slotMaster.Name == slotName && slotCandidate.Name == slotName && slotMaster.Version == clip.Version,
                            "Slot publication freezes its name/version-adjusted master separately from the editable animation");
                        string slotPath = Path.Combine(output, "renamed-slot-reference.chr0"); Call(form, "ExportWorkspaceSnapshot", renamedSlot, slotPath);
                        Check(new FileInfo(slotPath).Length == (long)Field(renamedSlot, "MasterBytes") && FighterProject.HashFile(slotPath) == (string)Field(renamedSlot, "PreparedEncodedSha256") && SetNativeContent(clip) == master,
                            "Slot metadata uses the correct same-name reference and reseals the exact prepared bytes without changing its source master");
                    }
                    finally { ((IDisposable)renamedSlot).Dispose(); }
                    foreach (string component in new[] { "value", "tangent" })
                    {
                        object damaged = Call(form, "FreezeWorkspaceExport");
                        try
                        {
                            var candidate = (CHR0Node)Field(damaged, "Candidate");
                            foreach (var entry in candidate.Children.OfType<CHR0EntryNode>().ToArray()) PreciseCHR0EntryNode.RestoreStorage(entry, null);
                            var contentMethod = form.GetType().GetMethod("ExportContent", Private | BindingFlags.Static);
                            damaged.GetType().GetField("ExpectedContent", Private).SetValue(damaged, (string)contentMethod.Invoke(null, new object[] { candidate }));
                            var snapshotType = form.GetType().GetNestedType("NativeControlSnapshot", BindingFlags.NonPublic);
                            string SnapshotKey() { var snapshot = snapshotType.GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { candidate, candidate.Children.Select(c => c.Name).ToArray() }); return (string)snapshotType.GetProperty("ContentKey", Private).GetValue(snapshot); }
                            string beforeBits = SnapshotKey();
                            string controlPath = Path.Combine(output, "signed-zero-control-" + component + ".chr0"); Call(form, "ExportWorkspaceSnapshot", damaged, controlPath);
                            var item = candidate.Children.OfType<CHR0EntryNode>().SelectMany(entry => entry.KeyArrays.SelectMany(array => array.Entries.Select(key => new { Entry = entry, Key = key })))
                                .First(pair => ReplayFloatBits(component == "value" ? pair.Key._value : pair.Key._tangent) == 0);
                            float negativeZero = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
                            if (component == "value") item.Key._value = negativeZero; else item.Key._tangent = negativeZero;
                            item.Entry.SignalPropertyChange();
                            Check(SnapshotKey() != beforeBits, "Native Undo/fitting snapshot equality distinguishes a signed-zero " + component + " edit");
                            string protectedPath = Path.Combine(output, "exact-copy.chr0"), priorHash = FighterProject.HashFile(protectedPath); bool rejectedBits = false;
                            try { Call(form, "ExportWorkspaceSnapshot", damaged, protectedPath); } catch (TargetInvocationException error) { rejectedBits = error.InnerException is InvalidDataException; }
                            Check(rejectedBits && FighterProject.HashFile(protectedPath) == priorHash, "Independent export verification rejects a damaged signed-zero " + component + " bit and preserves an existing destination");
                        }
                        finally { ((IDisposable)damaged).Dispose(); }
                    }
                    object bounded = Call(form, "FreezeWorkspaceExport");
                    try
                    {
                        var explicitBounds = new NativeExportOptimizationSettings { Policy = NativeExportOptimizationPolicy.ErrorBounded,
                            RotationDegrees = .05, Translation = .001, Scale = .00005, GeometryRelative = .0005, RotationDerivative = .05, TranslationDerivative = .001, ScaleDerivative = .00005 };
                        await (Task)Call(form, "PrepareOptimizedExportSnapshot", bounded, explicitBounds, CancellationToken.None, null);
                        Phase("bounded-prepared");
                        Check((string)Field(bounded, "PreparedPolicyKey") == explicitBounds.StableKey && SetNativeContent((CHR0Node)Field(bounded, "Master")) == master,
                            "Error-bounded export freezes explicit tighter limits against the same finished target master");
                        var context = Field(bounded, "Optimization");
                        Check((bool)Field(context, "ProtectionKnown") && string.IsNullOrWhiteSpace((string)Field(context, "BoundedUnavailable")), "Ordinary imported animation exercises the actual explicit bounded backend with reviewed native protections");
                        Check(((string)Field(bounded, "OptimizationReport")).Contains("raw Euler/local orientation 0.05°") && ((string)Field(bounded, "OptimizationReport")).Contains("translation 0.001"),
                            "Bounded receipt reports the exact frozen tighter numeric ceilings");
                        if (!string.IsNullOrWhiteSpace((string)Field(context, "BoundedUnavailable")))
                            Check((string)Field(bounded, "EffectiveExportPolicy") == "Exact" && ((string)Field(bounded, "OptimizationReport")).Contains("Exact fallback"),
                                "Missing absolute/review context uses explicit Exact fallback rather than claiming bounded contact qualification");
                        string boundedPath = Path.Combine(output, "explicit-bounded-copy.chr0"); Call(form, "ExportWorkspaceSnapshot", bounded, boundedPath);
                        Check(new FileInfo(boundedPath).Length <= referenceBytes && SetNativeContent(clip) == master, "Explicit bounded policy cannot grow output or replace the master");
                    }
                    finally { ((IDisposable)bounded).Dispose(); }
                    string controlKey = (string)Call(form, "ControlKey");
                    var savedState = form.FighterProject.AnimationControls.Single(s => s.Key == controlKey);
                    string savedStateJson = savedState.Json;
                    try
                    {
                        var transport = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 200 };
                        var state = transport.Deserialize<Dictionary<string, object>>(savedStateJson);
                        state.Remove("AuthoringContacts"); state["authoringContacts"] = new object[] { new Dictionary<string, object>() };
                        savedState.Json = transport.Serialize(state);
                        object protectedExport = Call(form, "FreezeWorkspaceExport");
                        try
                        {
                            var context = Field(protectedExport, "Optimization");
                            Check(((string)Field(context, "BoundedUnavailable")).Contains("authored pin/contact"), "A nonempty camel-case contact collection disables bounded export before preparation");
                            await (Task)Call(form, "PrepareOptimizedExportSnapshot", protectedExport, new NativeExportOptimizationSettings { Policy = NativeExportOptimizationPolicy.ErrorBounded }, CancellationToken.None, null);
                            Check((string)Field(protectedExport, "EffectiveExportPolicy") == "Exact" && SetNativeContent(clip) == master,
                                "Unknown absolute contact context retains the exact finished master rather than treating a relative error gate as contact acceptance");
                        }
                        finally { ((IDisposable)protectedExport).Dispose(); }
                    }
                    finally { savedState.Json = savedStateJson; Phase("contact-state-restored"); }
                    var siblingClip = Walk(clip.RootNode).OfType<CHR0Node>().First(c => !ReferenceEquals(c, clip) && c.Name == "Fall");
                    Phase("before-explicit-sibling-selection");
                    editor.SelectedCHR0 = siblingClip; await SimpleControlsReady(form); await Await<ControlReply>(Call(form, "LoadControls"));
                    Phase("after-explicit-sibling-setup");
                    var siblingAsset = (FighterAsset)Call(form, "SelectedFighterAsset"); string siblingMaster = SetNativeContent(siblingClip);
                    editor.SelectedCHR0 = clip; await SimpleControlsReady(form); await Await<ControlReply>(Call(form, "LoadControls")); editor.SetFrame(frame); editor.SelectedBone = bone;
                    Phase("after-explicit-return-and-setup");
                    object nonselected = Call(form, "FreezeOwnedWorkspaceExport", siblingAsset, siblingClip, false, null);
                    try
                    {
                        var context = Field(nonselected, "Optimization");
                        var roots = (string[])Field(context, "ExactBones");
                        Check((bool)Field(context, "ProtectionKnown") && roots.Length > 0, "A nonselected owned clip resolves its own persisted reviewed roots for set export");
                        await (Task)Call(form, "PrepareOptimizedExportSnapshot", nonselected, new NativeExportOptimizationSettings { Policy = NativeExportOptimizationPolicy.ErrorBounded }, CancellationToken.None, null);
                        Phase("nonselected-prepared");
                        var prepared = (CHR0Node)Field(nonselected, "Candidate");
                        var model = (MDL0Node)target;
                        float RootSample(CHR0Node animation, string name, int channel, float time)
                        {
                            var entry = animation.Children.OfType<CHR0EntryNode>().SingleOrDefault(c => c.Name == name);
                            return entry != null && entry.KeyArrays[channel].KeyCount > 0 ? entry.KeyArrays[channel].GetFrameValue(time) : model.AllBones.Single(b => b.Name == name).BindState[channel];
                        }
                        bool exactRoots = true;
                        foreach (string root in roots) for (int channel = 0; channel < 9; channel++) for (int subframe = 0; subframe <= (siblingClip.FrameCount - 1) * 4; subframe++)
                            if (Math.Abs(RootSample(siblingClip, root, channel, subframe / 4f) - RootSample(prepared, root, channel, subframe / 4f)) > .00001) exactRoots = false;
                        Check(exactRoots && SetNativeContent(siblingClip) == siblingMaster && ReferenceEquals(editor.SelectedCHR0, clip),
                            "Nonselected set-style export retains source-owned local root motion and never temporarily selects or edits that clip");
                        Call(form, "ExportWorkspaceSnapshot", nonselected, Path.Combine(output, "nonselected-Fall-copy.chr0"));
                        Phase("nonselected-published");
                    }
                    finally { ((IDisposable)nonselected).Dispose(); }
                    string ordinaryReference = Path.Combine(output, "ordinary-nonselected-reference.chr0");
                    Check((bool)Call(form, "TryExportOwnedAnimationCopy", siblingClip, ordinaryReference), "Nonselected ordinary owned CHR0 uses the independent reference publication route");
                    using (var decoded = (CHR0Node)NodeFactory.FromFile(null, ordinaryReference))
                        Check(NativeAnimationOptimization.SourceContentKey(decoded) == NativeAnimationOptimization.SourceContentKey(siblingClip) && SetNativeContent(siblingClip) == siblingMaster,
                            "Unknown intended-model reference export preserves every exact native tuple, metadata and live ordinary clip");
                    object ordinaryFrozen = Call(form, "FreezeOwnedReferenceExport", siblingAsset, siblingClip);
                    try
                    {
                        Call(form, "PrepareReferenceExportSnapshot", ordinaryFrozen, new NativeExportOptimizationSettings { Policy = NativeExportOptimizationPolicy.ErrorBounded });
                        Check((string)Field(ordinaryFrozen, "EffectiveExportPolicy") == "Exact/reference", "Owned unknown-model context never silently infers a bounded target");
                        string prior = FighterProject.HashFile(ordinaryReference); bool changedLoop = siblingClip.Loop;
                        siblingClip.Loop = !changedLoop; bool staleReference = false;
                        try { Call(form, "ExportWorkspaceSnapshot", ordinaryFrozen, ordinaryReference); } catch (TargetInvocationException error) { staleReference = error.InnerException is InvalidOperationException; }
                        siblingClip.Loop = changedLoop;
                        Check(staleReference && FighterProject.HashFile(ordinaryReference) == prior, "Stale nonselected reference source preserves an existing destination");
                        ((CHR0Node)Field(ordinaryFrozen, "Candidate")).Loop = !changedLoop; bool corruptReference = false;
                        try { Call(form, "ExportWorkspaceSnapshot", ordinaryFrozen, ordinaryReference); } catch (TargetInvocationException error) { corruptReference = error.InnerException is InvalidDataException; }
                        Check(corruptReference && FighterProject.HashFile(ordinaryReference) == prior, "Corrupt nonselected reference candidate cannot replace an existing destination");
                    }
                    finally { ((IDisposable)ordinaryFrozen).Dispose(); }
                    object raced = Call(form, "FreezeWorkspaceExport");
                    try
                    {
                        string racedPath = Path.Combine(output, "created-during-preparation.chr0");
                        Call(form, "CaptureWorkspaceExportDestination", raced, racedPath); File.WriteAllText(racedPath, "external owner");
                        string protectedHash = FighterProject.HashFile(racedPath); bool racedRejected = false;
                        try { Call(form, "ExportWorkspaceSnapshot", raced, racedPath); } catch (TargetInvocationException error) { racedRejected = error.InnerException is IOException; }
                        Check(racedRejected && FighterProject.HashFile(racedPath) == protectedHash, "A destination created during preparation is never overwritten");
                    }
                    finally { ((IDisposable)raced).Dispose(); }
                    using (var cancellation = new CancellationTokenSource())
                    {
                        object cancelled = Call(form, "FreezeWorkspaceExport");
                        try
                        {
                            cancellation.Cancel(); bool stopped = false;
                            try { await (Task)Call(form, "PrepareOptimizedExportSnapshot", cancelled, new NativeExportOptimizationSettings(), cancellation.Token, null); }
                            catch (OperationCanceledException) { stopped = true; }
                            Check(stopped && (string)Field(cancelled, "PreparedPolicyKey") == null, "Cancelled preparation publishes no partially qualified candidate");
                        }
                        finally { ((IDisposable)cancelled).Dispose(); }
                    }
                    Phase("after-destination-race-and-cancel");
                    Check(SetNativeContent(clip) == master && NativeAnimationOptimization.StorageKey(clip) == masterStorage && ReferenceEquals(editor.TargetModel, target) &&
                        ReferenceEquals(editor.SelectedCHR0, clip) && ReferenceEquals(editor.SelectedBone, bone) && editor.CurrentFrame == frame && editor._undoSaves.Count == undo && clip.IsDirty == expectedDirty,
                        "All export analysis/publication/cancellation retains exact master, storage, target, clip, bone, frame and native Undo");
                    editor.TargetModel = null; await Wait(() => !(bool)Field(form, "_controlBusy"));
                    try
                    {
                        string noTarget = Path.Combine(output, "ordinary-no-target-reference.chr0");
                        Check((bool)Call(form, "TryExportOwnedAnimationCopy", siblingClip, noTarget), "Owned ordinary animation exports independently when no target viewport model is active");
                        using (var decoded = (CHR0Node)NodeFactory.FromFile(null, noTarget))
                            Check(NativeAnimationOptimization.SourceContentKey(decoded) == NativeAnimationOptimization.SourceContentKey(siblingClip), "Target-free reference export retains exact native metadata and coefficients without model inference");
                    }
                    finally { editor.TargetModel = target; editor.SelectedCHR0 = clip; await SimpleControlsReady(form); editor.SetFrame(frame); editor.SelectedBone = bone; }
                    // The temporary stale-loop exercise legitimately touched dirty
                    // state; establish and check a fresh unchanged export boundary.
                    bool currentDirty = clip.IsDirty; form.ExportSimpleWorkspaceResult(Path.Combine(output, "ordinary-reference-save.chr0"));
                    Check(clip.IsDirty == currentDirty && SetNativeContent(clip) == master, "Ordinary native reference save retains full-quality master and dirty state");
                    OptimizationCheckUntouchedBytes(form, clip, siblings, "export-copy qualification", Path.Combine(output, "unchanged-siblings.json"));
                    bool protectedOriginal = false; try { Call(form, "ProtectVerifiedExport", Path.GetFullPath(args[1])); } catch (TargetInvocationException error) { protectedOriginal = error.InnerException is InvalidOperationException; }
                    Check(protectedOriginal, "Original motion input remains protected across export-copy routes");
                    foreach (int text in new[] { 100, 125, 150, 175 }) foreach (int width in new[] { 1050, 1700 })
                    {
                        form.ClientSize = new Size(width, 950); Call(form, "SetWorkspaceTextSize", text);
                        using (FlowModal("workspaceFilePath", dialog => {
                            Check(SimpleControl<ComboBox>(dialog, "exportOptimizationPolicy").Items.Count == 2, "Export has exactly two explicit policies at " + width + "/" + text);
                            var policy = SimpleControl<ComboBox>(dialog, "exportOptimizationPolicy");
                            Check(policy.ClientSize.Width >= TextRenderer.MeasureText("Error-bounded", policy.Font).Width + SystemInformation.VerticalScrollBarWidth + 8,
                                "The complete bounded policy caption fits its measured text size at " + width + "/" + text);
                            CaptureDialog(dialog, output, "export-copy-" + width + "-" + text + ".png");
                            SimpleControl<ComboBox>(dialog, "exportOptimizationPolicy").SelectedIndex = 1;
                            var link = SimpleControl<LinkLabel>(dialog, "exportOptimizationLimits"); Call(link, "OnLinkClicked", new LinkLabelLinkClickedEventArgs(link.Links[0]));
                            Check(SimpleControl<NumericUpDown>(dialog, "exportOptimizationLimit6").Visible, "Expanded explicit value/derivative limits remain available at " + width + "/" + text);
                            CaptureDialog(dialog, output, "export-copy-expanded-" + width + "-" + text + ".png");
                            SimpleControl<Button>(dialog, "workspaceFileCancel").PerformClick();
                        })) Call(form, "ChooseSimpleWorkspaceExport");
                        using (var dialog = new WorkspaceSetExportDialog(form.FighterProject.Name, ((MDL0Node)target).Name, "current reviewed native target", "verified local target content",
                            new[] { clip.Name + " · " + clip.FrameCount + " frames → current-clip.chr0" }))
                        using (FlowModal("setExportPath", window => {
                            SimpleControl<ComboBox>(window, "exportOptimizationPolicy").SelectedIndex = 1;
                            var link = SimpleControl<LinkLabel>(window, "exportOptimizationLimits"); Call(link, "OnLinkClicked", new LinkLabelLinkClickedEventArgs(link.Links[0]));
                            Check(SimpleControl<NumericUpDown>(window, "exportOptimizationLimit6").Visible, "Set export exposes the same explicit limits at " + width + "/" + text);
                            CaptureDialog(window, output, "set-export-expanded-" + width + "-" + text + ".png");
                            SimpleControl<Button>(window, "setExportCancel").PerformClick();
                        })) dialog.ShowDialog(form);
                    }
                    string manifest = Path.Combine(output, "full-quality-master.fpstudio");
                    form.SaveFighterProject(manifest);
                    Check(SetNativeContent(clip) == master && ReferenceEquals(editor.SelectedCHR0, clip) && editor.CurrentFrame == frame && editor._undoSaves.Count == undo,
                        "Ordinary project Save preserves every full-quality master key and native selection/frame/Undo instead of saving the optimized export");
                    var savedAsset = (FighterAsset)Call(form, "SelectedFighterAsset");
                    string firstSaveBytes = FighterProject.HashFile(savedAsset.WorkingPath);
                    form.SaveFighterProject(manifest);
                    Check(SetNativeContent(clip) == master && FighterProject.HashFile(savedAsset.WorkingPath) == firstSaveBytes,
                        "Ordinary project resave retains exact master content and deterministic complete native motion-file bytes");
                    Check(MainForm.Instance.OpenFighterProject(manifest, form), "Full-quality master project reopens through the normal project lifecycle");
                    form = (ModelForm)Field(MainForm.Instance, "_fighterForm");
                    await Wait(() => form.FighterEditor.TargetModel != null && form.FighterEditor.SelectedCHR0 != null); await SimpleControlsReady(form);
                    Check(SetNativeContent(form.FighterEditor.SelectedCHR0) == master && form.FighterEditor.CurrentFrame == frame,
                        "Reopen restores the full-quality master and playhead rather than an optimized delivery copy");
                }
                finally { SimpleDisposeForm(form); }
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", previousPreferences);
            Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previousLibrary);
        }
    }
}
