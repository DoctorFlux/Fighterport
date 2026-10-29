using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static partial class Program
{
    // This invokes the existing native animation-list callback immediately
    // below BrawlCrate's SaveFileDialog. No OS dialog/input automation is used.
    static async Task WorkspaceNativeAnimationExport(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Native animation export qualification requires a private desktop.");
        string previousPreferences = Environment.GetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT");
        string previousLibrary = Environment.GetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT");
        Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", Path.Combine(output, "isolated-export-settings"));
        Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", Path.Combine(output, "isolated-library"));
        var originals = new[] { args[0], args[1] }.Distinct().ToDictionary(path => path, FighterProject.HashFile);
        var preferences = new NativeExportOptimizationPreferences();
        var bounded = new NativeExportOptimizationSettings { Policy = NativeExportOptimizationPolicy.ErrorBounded, SearchEffort = NativeExportSearchEffort.Thorough };
        preferences.Save(bounded);
        Check(preferences.LastError == null && preferences.Read().StableKey == bounded.StableKey,
            "Native Export fixture retains saved Error-bounded/Thorough preferences to prove ordinary export ignores them");
        var form = new ModelForm(); var files = new List<object>();
        bool preparationWindow = false;
        string diagnosticPhase = "setup";
        int diagnosticExceptions = 0;
        // The real callback deliberately displays caught user-facing errors.
        // Record a bounded first-chance trace before that modal boundary so
        // background failures remain diagnosable without desktop interaction.
        EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> diagnostic = (sender, error) => {
            try
            {
                string detail = error.Exception.ToString();
                if (detail.IndexOf("BrawlCrate.UI.ModelForm", StringComparison.Ordinal) < 0 ||
                    System.Threading.Interlocked.Increment(ref diagnosticExceptions) > 16) return;
                if (detail.Length > 8192) detail = detail.Substring(0, 8192);
                lock (files) File.AppendAllText(Path.Combine(output, "native-export-first-chance.log"),
                    DateTime.UtcNow.ToString("o") + " | " + diagnosticPhase + "\r\n" + detail + "\r\n");
            }
            catch { } // Diagnostic I/O never replaces or suppresses the actual exception.
        };
        AppDomain.CurrentDomain.FirstChanceException += diagnostic;
        using (var observer = new Timer { Interval = 20 })
        {
            observer.Tick += (s, e) => preparationWindow |= Application.OpenForms.Cast<Form>().Any(window => window.Name == "workspaceExportPreparation");
            observer.Start();
            try
            {
                form.InitializeFighterProject(FighterProject.Create("Plain native animation Export regression"));
                form.Show(); form.ClientSize = new Size(1700, 950);
                Call(form, "ImportFighterModel", args[0]); Call(form, "ImportFighterAnimations", new[] { new[] { args[1] } });
                var editor = form.FighterEditor;
                var clip = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().First(item => item.Name == "Wait1");
                editor.SelectedCHR0 = clip; await SimpleControlsReady(form);
                var setup = await Await<ControlReply>(Call(form, "LoadControls"));
                var model = (MDL0Node)editor.TargetModel;
                editor.SetFrame(3); editor.SelectedBone = model.AllBones.Single(bone => bone.Name == setup.BoneNames[setup.Roles.Single(role => role.Role == "Hips").Bone.Value]);
                string pristine = LifecycleContent(clip);
                var box = editor.CHR0Editor._transBoxes[3]; box.Text = (editor.SelectedBone.FrameState._rotate._x + 1.25f).ToString(); Call(box, "Apply");
                await SimpleControlsReady(form);
                string edited = LifecycleContent(clip);
                Check(edited != pristine && editor._undoSaves.Count > 0 && form.FighterProject.ManifestPath == null,
                    "Ordinary context Export fixture contains an unsaved native body edit before project Save");
                var roots = setup.ProtectedMotionBones.Select(index => setup.BoneNames[index]).ToArray();
                string controls = RecoveryKeys(clip, roots);
                string modelHash = (string)Call(form, "NativeControlHash", model, ".mdl0");
                int prepares = form.OptimizationPrepares, nativeChecks = form.OptimizationNativeChecks;
                var nativeExport = editor.FighterNativeExportRequested;
                Check(nativeExport != null, "Original animation-list Export retains its actual application callback and output protections");
                Check(FileFilters.CHR0Export.Contains("*.chr0") && FileFilters.CHR0Export.Contains("*.anim"),
                    "Native CHR0 export retains BrawlCrate's CHR0 and Maya ANIM file choices");
                var siblings = OptimizationUntouchedBytes(form, clip);

                string State() => new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Serialize(new {
                    Content = NativeAnimationOptimization.SourceContentKey(clip), Storage = NativeAnimationOptimization.StorageKey(clip),
                    Identity = OptimizationNativeIdentity(clip), Target = ReferenceEquals(editor.TargetModel, model), Selected = ReferenceEquals(editor.SelectedCHR0, clip),
                    Bone = editor.SelectedBone?.Name, Frame = editor.CurrentFrame, Camera = CompactCamera(form),
                    Undo = editor._undoSaves.Count, Redo = editor._redoSaves.Count, editor._saveIndex, editor.AwaitingRedoSave,
                    ClipDirty = clip.IsDirty, RootDirty = clip.RootNode.IsDirty, Model = DirtyModelMemory(model), ProjectDirty = (bool)Field(form, "_fighterDirty"),
                    Assets = form.FighterProject.Animations.Count
                });
                void NoAnalysis(string label)
                {
                    Check(!preparationWindow && !Application.OpenForms.Cast<Form>().Any(window => window.Name == "workspaceExportPreparation") &&
                        form.OptimizationPrepares == prepares && form.OptimizationNativeChecks == nativeChecks,
                        label + ": ordinary native Export opens no optimization progress window and performs zero optimization/geometry analysis");
                    Check(preferences.Read().StableKey == bounded.StableKey, label + ": ordinary Export does not rewrite explicit optimizer preferences");
                }
                void PolicyDiagnostic(string phase)
                {
                    var entries = clip.Children.OfType<CHR0EntryNode>().Select(entry => {
                        var precise = entry as PreciseCHR0EntryNode;
                        var capture = PreciseCHR0EntryNode.CaptureStorageForWrite(entry);
                        return new {
                            entry.Name, Type = entry.GetType().FullName, ParentIsFixture = ReferenceEquals(entry.Parent, clip),
                            WorkingBytes = entry.WorkingUncompressed.Length, OriginalBytes = entry.UncompressedSource.Length,
                            WorkingAddress = ((IntPtr)entry.WorkingUncompressed.Address).ToInt64(), OriginalAddress = ((IntPtr)entry.UncompressedSource.Address).ToInt64(),
                            entry.IsDirty, entry.IsBranch, entry.UseModelScale, entry.UseModelRotate, entry.UseModelTranslate,
                            entry.ScaleCompensateApply, entry.ScaleCompensateParent, entry.ClassicScaleOff,
                            entry.Keyframes.FrameLimit, entry.Keyframes.Loop,
                            ExplicitPrecisePolicy = precise != null, ExplicitNullPolicy = precise != null && precise.IndexedStorage == null,
                            CaptureAccepted = capture.Accepted, capture.Diagnostic,
                            Groups = Enumerable.Range(0, 3).Select(group => new {
                                Group = group, RetainedMatches = precise?.IndexedStorage?.MatchesGroup(entry.Keyframes, group) ?? false,
                                Curves = Enumerable.Range(group * 3, 3).Select(channel => precise?.IndexedStorage?[channel]?.ContentKey).ToArray()
                            }).ToArray()
                        };
                    }).ToArray();
                    File.WriteAllText(Path.Combine(output, "native-export-policy-" + phase + ".json"),
                        new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 }.Serialize(new {
                            Phase = phase, SelectedIsFixture = ReferenceEquals(editor.SelectedCHR0, clip),
                            SelectedName = editor.SelectedCHR0?.Name, FixtureName = clip.Name,
                            FixtureIdentity = OptimizationNativeIdentity(clip),
                            SelectedIdentity = editor.SelectedCHR0 == null ? null : OptimizationNativeIdentity(editor.SelectedCHR0),
                            FixtureParentType = clip.Parent?.GetType().FullName, RootType = clip.RootNode.GetType().FullName,
                            ClipWorkingBytes = clip.WorkingUncompressed.Length, ClipOriginalBytes = clip.UncompressedSource.Length,
                            ClipWorkingAddress = ((IntPtr)clip.WorkingUncompressed.Address).ToInt64(), ClipOriginalAddress = ((IntPtr)clip.UncompressedSource.Address).ToInt64(),
                            RootWorkingBytes = clip.RootNode.WorkingUncompressed.Length, RootOriginalBytes = clip.RootNode.UncompressedSource.Length,
                            RootWorkingAddress = ((IntPtr)clip.RootNode.WorkingUncompressed.Address).ToInt64(), RootOriginalAddress = ((IntPtr)clip.RootNode.UncompressedSource.Address).ToInt64(),
                            clip.IsDirty, clip.IsBranch, clip.FrameCount, clip.Loop, clip.Version, clip.NativeScalingRule,
                            Frame = editor.CurrentFrame, Undo = editor._undoSaves.Count, Redo = editor._redoSaves.Count,
                            Entries = entries, Scope = "Read-only diagnostic; no Export, Rebuild, storage adoption, solving or acceptance inference."
                        }));
                }
                void Publish(CHR0Node member, string path, string label)
                {
                    diagnosticPhase = label;
                    File.AppendAllText(Path.Combine(output, "native-export-phases.log"), DateTime.UtcNow.ToString("o") + " | begin " + label + "\r\n");
                    string state = State(), expected = NativeAnimationOptimization.SourceContentKey(member), storage = NativeAnimationOptimization.StorageKey(member);
                    nativeExport(member, path);
                    File.AppendAllText(Path.Combine(output, "native-export-phases.log"), DateTime.UtcNow.ToString("o") + " | returned " + label + "\r\n");
                    Check(File.Exists(path), label + ": actual native animation-list callback writes its chosen CHR0");
                    using (var reopened = NodeFactory.FromFile(null, path) as CHR0Node)
                        Check(reopened != null && NativeAnimationOptimization.SourceContentKey(reopened) == expected &&
                            NativeAnimationOptimization.StorageKey(reopened) == storage,
                            label + ": fresh native decoder retains all current keys, paired sides, tangents, flags, metadata and compact storage");
                    Check(State() == state && RecoveryKeys(clip, roots) == controls,
                        label + ": exact current member, target/bone/frame/camera, dirty state, root motion and native Undo/Redo remain unchanged");
                    NoAnalysis(label);
                    files.Add(new { Label = label, Name = Path.GetFileName(path), Bytes = new FileInfo(path).Length, SHA256 = FighterProject.HashFile(path) });
                }

                string destination = Path.Combine(output, "native-current-before-save.chr0");
                PolicyDiagnostic("before-first-export");
                Publish(clip, destination, "Current edited animation");
                editor.Undo(); PolicyDiagnostic("after-undo-sync"); await SimpleControlsReady(form); PolicyDiagnostic("after-undo-ready"); Check(LifecycleContent(clip) == pristine, "Native body Undo works immediately after ordinary Export");
                editor.Redo(); PolicyDiagnostic("after-redo-sync"); await SimpleControlsReady(form); PolicyDiagnostic("after-redo-ready"); Check(LifecycleContent(clip) == edited, "Native body Redo works immediately after ordinary Export");
                Publish(clip, destination, "Existing destination already confirmed by native picker");
                var sibling = Walk(clip.RootNode).OfType<CHR0Node>().First(item => !ReferenceEquals(item, clip) && item.Name == "Fall");
                Publish(sibling, Path.Combine(output, "native-nonselected-fall.chr0"), "Explicit owned sibling export without temporary selection");
                Check((string)Call(form, "NativeControlHash", model, ".mdl0") == modelHash,
                    "Current/nonselected native exports preserve authentic model hierarchy, binds and every skin coefficient");

                // Only the model-picker boundary is substituted. The payload
                // is written by the actual native Maya converter, not a fake.
                string maya = Path.Combine(output, "native-current.anim"), expectedMaya = Path.Combine(output, "native-direct-writer.anim");
                using (var expected = NativeAnimationOptimization.CopySource(clip))
                    BrawlLib.Wii.Animations.AnimFormat.Serialize(expected, expectedMaya, model);
                string beforeMaya = State(); int conversions = 0;
                form.NativeAnimationConversionWriter = (copy, path) => {
                    conversions++;
                    Check(!ReferenceEquals(copy, clip) && NativeAnimationOptimization.SourceContentKey(copy) == NativeAnimationOptimization.SourceContentKey(clip),
                        "Maya conversion receives an independent exact current native animation rather than an optimization baseline");
                    BrawlLib.Wii.Animations.AnimFormat.Serialize(copy, path, model);
                };
                nativeExport(clip, maya);
                Check(conversions == 1 && File.Exists(maya) && File.ReadAllBytes(maya).SequenceEqual(File.ReadAllBytes(expectedMaya)) && State() == beforeMaya,
                    "Native context Maya output matches the actual native converter byte-for-byte without binary CHR0 reopen rejection or document mutation");
                string mayaHash = FighterProject.HashFile(maya);
                form.NativeAnimationConversionWriter = (copy, path) => { };
                nativeExport(clip, maya);
                Check(FighterProject.HashFile(maya) == mayaHash && State() == beforeMaya,
                    "Cancelled native conversion publishes no file and preserves an existing Maya destination and native state");
                form.NativeAnimationConversionWriter = (copy, path) => { File.WriteAllText(path, "partial conversion"); throw new IOException("Procedural conversion failure"); };
                bool conversionFailed = false;
                try { Call(form, "ExportFighterNode", clip, maya); }
                catch (TargetInvocationException error) { conversionFailed = error.InnerException is IOException; }
                form.NativeAnimationConversionWriter = null;
                Check(conversionFailed && FighterProject.HashFile(maya) == mayaHash && !Directory.GetFiles(output, ".*.anim").Any() && State() == beforeMaya,
                    "Failed native conversion removes its partial candidate and leaves an existing destination unchanged");
                NoAnalysis("Native Maya conversion/cancel/failure");

                string savedName = model.Name; model.Name += "__pending-native-model-edit";
                Check(model.IsDirty && model.HasChanged, "Native model rename supplies a real unsaved target dependency");
                Publish(clip, Path.Combine(output, "native-with-dirty-target.chr0"), "Animation export with unsaved model edit");
                Check(model.IsDirty && model.Name == savedName + "__pending-native-model-edit",
                    "Plain animation export never rebuilds or clears the unsaved model in order to hash/optimize it");
                model.Name = savedName;

                string successful = FighterProject.HashFile(destination);
                using (var locked = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    string state = State(); bool failed = false;
                    try { Call(form, "ExportFighterNode", clip, destination); }
                    catch (TargetInvocationException error) { failed = error.InnerException is IOException; }
                    Check(failed && State() == state, "Locked ordinary-export publication fails without changing live native state");
                }
                Check(FighterProject.HashFile(destination) == successful && !Directory.GetFiles(output, ".*.chr0").Any(),
                    "Failed ordinary-export publication preserves the existing destination and removes its temporary file");
                foreach (string protectedPath in new[] { args[1], form.FighterProject.Animations.First().WorkingPath })
                {
                    string hash = FighterProject.HashFile(protectedPath), state = State(); bool rejected = false;
                    try { Call(form, "ExportFighterNode", clip, protectedPath); }
                    catch (TargetInvocationException error) { rejected = error.InnerException is InvalidOperationException; }
                    Check(rejected && FighterProject.HashFile(protectedPath) == hash && State() == state,
                        "Ordinary Export retains original-source and project-working-file protections");
                }
                OptimizationCheckUntouchedBytes(form, clip, siblings, "ordinary native animation Export", Path.Combine(output, "native-export-unchanged-siblings.json"));
                foreach (int text in new[] { 100, 175 }) foreach (int width in new[] { 1050, 1700 })
                {
                    Call(form, "SetWorkspaceTextSize", text); form.ClientSize = new Size(width, 950); await Task.Delay(60);
                    await CaptureSimpleWorkspace(form, Path.Combine(output, "native-export-current-" + width + "-" + text + ".png"));
                }
                NoAnalysis("Completed native CHR0 workflow");
                Check(originals.All(item => FighterProject.HashFile(item.Key) == item.Value), "Ordinary context exports preserve every supplied original model and animation archive");
                await NativeAnimationExportWithoutModel(output, files);
                File.WriteAllText(Path.Combine(output, "native-animation-export-evidence.json"), new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 }.Serialize(new {
                    ActualNativeCallback = true, PhysicalFilePicker = false, OverwriteConfirmationAtPicker = false, CancelledNativePicker = false,
                    NativeCHR0 = true, MayaANIMConverter = true, MayaANIMPicker = false, AutomaticOptimization = false, OptimizationPrepares = form.OptimizationPrepares - prepares,
                    OptimizationNativeChecks = form.OptimizationNativeChecks - nativeChecks, PreparationWindow = preparationWindow, Files = files,
                    PrivateDesktop, OSInputInjected = false, ScreenCopied = false, HumanUsability = false, GameAcceptance = false,
                    Scope = "Existing post-picker native context callback; OS picker/cancel/overwrite interaction and Maya model chooser remain unverified." }));
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= diagnostic;
                form.NativeAnimationConversionWriter = null;
                observer.Stop(); SimpleDisposeForm(form);
                Environment.SetEnvironmentVariable("FIGHTERPORT_EXPORT_PREFERENCES_ROOT", previousPreferences);
                Environment.SetEnvironmentVariable("FIGHTERPORT_LIBRARY_ROOT", previousLibrary);
            }
        }
    }

    static async Task NativeAnimationExportWithoutModel(string output, List<object> files)
    {
        string input = Path.Combine(output, "native-procedural-source.chr0");
        using (var source = new CHR0Node { Name = "Native precise metadata", Version = 5, FrameCount = 9, Loop = true, OriginalPath = "procedural/original" })
        {
            source.UserEntries.Add(new UserDataClass { Name = "note", DataType = UserValueType.String, Entries = new[] { "native export metadata" } });
            var entry = source.CreateEntry("Unmatched attachment"); entry.Keyframes.PreserveInterpolation = true;
            entry.UseModelTranslate = true; entry.ScaleCompensateApply = true;
            entry.Keyframes.SetFrameValue(3, 0, 0, true)._tangent = 80;
            entry.Keyframes.SetFrameValue(3, 4.125f, 330, true)._tangent = 70;
            entry.Keyframes.SetFrameValue(3, 4.125f, 330, true)._tangent = 90;
            entry.Keyframes.SetFrameValue(3, 9, 720, true)._tangent = 80;
            source.CreateEntry("Absent groups").UseModelRotate = true;
            PreciseCHR0EntryNode.Promote(source); source.ExportChecked(input);
        }
        var form = new ModelForm();
        try
        {
            form.InitializeFighterProject(FighterProject.Create("Native model-less animation Export")); form.Show();
            Call(form, "ImportFighterAnimations", new[] { new[] { input } });
            var editor = form.FighterEditor;
            var clip = editor._openedFiles.SelectMany(Walk).OfType<CHR0Node>().Single(); editor.SelectedCHR0 = clip;
            await SimpleControlsReady(form);
            string content = NativeAnimationOptimization.SourceContentKey(clip), storage = NativeAnimationOptimization.StorageKey(clip);
            int prepares = form.OptimizationPrepares, checks = form.OptimizationNativeChecks, undo = editor._saveIndex;
            string destination = Path.Combine(output, "native-procedural-without-model.chr0");
            Check(editor.TargetModel == null, "Native model-less fixture has no inferred or selected MDL0 dependency");
            editor.FighterNativeExportRequested(clip, destination);
            using (var decoded = NodeFactory.FromFile(null, destination) as CHR0Node)
                Check(decoded != null && NativeAnimationOptimization.SourceContentKey(decoded) == content && NativeAnimationOptimization.StorageKey(decoded) == storage,
                    "Native context Export without a target preserves v5 metadata, sparse/absent groups, unknown attachment, full turns and paired tangents");
            Check(ReferenceEquals(editor.SelectedCHR0, clip) && NativeAnimationOptimization.SourceContentKey(clip) == content &&
                NativeAnimationOptimization.StorageKey(clip) == storage && editor._saveIndex == undo && editor.TargetModel == null &&
                form.OptimizationPrepares == prepares && form.OptimizationNativeChecks == checks,
                "Model-less ordinary Export changes no selection/content/Undo and performs no optimization or model inference");
            files.Add(new { Label = "No target / v5 procedural metadata", Name = Path.GetFileName(destination), Bytes = new FileInfo(destination).Length, SHA256 = FighterProject.HashFile(destination) });
        }
        finally { SimpleDisposeForm(form); }
    }
}
