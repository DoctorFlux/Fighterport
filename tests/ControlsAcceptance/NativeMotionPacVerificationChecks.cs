using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.Internal.Windows.Controls.Model_Panel;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Authentic native container qualification, independent of the outer UI.
    // The master/template stay read-only; only operation-owned copies are saved.
    static void NativeMotionPacVerificationChecks(string modelPath, string templatePath, string output)
    {
        Directory.CreateDirectory(output);
        string modelHash = FighterProject.HashFile(modelPath), templateHash = FighterProject.HashFile(templatePath);
        using (var modelRoot = NodeFactory.FromFile(null, modelPath))
        using (var template = NodeFactory.FromFile(null, templatePath))
        {
            var model = ModelPanel.CollectModels(modelRoot).OfType<MDL0Node>().First();
            Check(template is ARCNode, "PAC fixture is an authentic native ARC motion container");
            var original = Walk(template).OfType<CHR0Node>().Single(clip => clip.Name == "Wait1");
            using (var candidate = NativeAnimationOptimization.CopySource(original))
            {
                string content = NativeAnimationOptimization.SourceContentKey(candidate), storage = NativeAnimationOptimization.StorageKey(candidate);
                string sourceContent = NativeAnimationOptimization.SourceContentKey(original), sourceStorage = NativeAnimationOptimization.StorageKey(original);
                bool dirty = candidate.IsDirty, originalDirty = original.IsDirty, modelDirty = model.IsDirty;
                var siblings = MotionPacFixtureSiblings(template, original, output);
                string destination = Path.Combine(output, "verified-motion-copy.pac"); int validations = 0;
                BrawlRetargeter.ExportMotionPac(templatePath, destination, model, candidate, () => validations++);
                Check(validations == 2 && File.Exists(destination), "PAC writer revalidates frozen dependencies immediately before new-file publication");
                using (var decoded = NodeFactory.FromFile(null, destination))
                {
                    var saved = Walk(decoded).OfType<CHR0Node>().Single(clip => clip.Name == original.Name);
                    Check(NativeAnimationOptimization.SourceContentKey(saved) == content && NativeAnimationOptimization.StorageKey(saved) == storage,
                        "Fresh PAC decode preserves complete prepared animation metadata, times, values, tangents, flags and storage");
                    Check(MotionPacFixtureEqual(siblings, MotionPacFixtureSiblings(decoded, saved, output)),
                        "Fresh PAC decode preserves all untouched CHR0 siblings, standalone native resource bytes and indexed structure");
                    string resaved = Path.Combine(output, "verified-motion-resaved.pac"); decoded.Merge(); decoded.ExportChecked(resaved);
                    using (var reopened = NodeFactory.FromFile(null, resaved))
                    {
                        var member = Walk(reopened).OfType<CHR0Node>().Single(clip => clip.Name == original.Name);
                        Check(NativeAnimationOptimization.SourceContentKey(member) == content && NativeAnimationOptimization.StorageKey(member) == storage &&
                            MotionPacFixtureEqual(siblings, MotionPacFixtureSiblings(reopened, member, output)),
                            "PAC reopen/resave retains complete inserted curves and every untouched sibling");
                    }
                }
                Check(candidate.IsDirty == dirty && NativeAnimationOptimization.SourceContentKey(candidate) == content && NativeAnimationOptimization.StorageKey(candidate) == storage &&
                    original.IsDirty == originalDirty && NativeAnimationOptimization.SourceContentKey(original) == sourceContent && NativeAnimationOptimization.StorageKey(original) == sourceStorage && model.IsDirty == modelDirty,
                    "PAC serialization leaves independent prepared input, original native master and target model unchanged");

                string existing = Path.Combine(output, "existing-destination.pac"); File.WriteAllBytes(existing, new byte[] { 31, 41, 59, 26 });
                string existingHash = FighterProject.HashFile(existing);
                Check(MotionPacFixtureRejected(() => BrawlRetargeter.ExportMotionPac(templatePath, existing, model, candidate)) && FighterProject.HashFile(existing) == existingHash,
                    "PAC export rejects an existing destination without replacing its bytes");

                string stale = Path.Combine(output, "changed-dependency.pac"); int staleChecks = 0;
                Check(MotionPacFixtureRejected(() => BrawlRetargeter.ExportMotionPac(templatePath, stale, model, candidate,
                    () => { if (++staleChecks == 2) throw new InvalidOperationException("Frozen dependency changed."); })) && !File.Exists(stale),
                    "PAC publication aborts a changed frozen dependency after complete staging");

                string damaged = Path.Combine(output, "damaged-candidate.pac");
                Check(MotionPacFixtureRejected(() => BrawlRetargeter.ExportMotionPac(templatePath, damaged, model, candidate, null,
                    path => File.WriteAllBytes(path, new byte[4]))) && !File.Exists(damaged),
                    "Independent PAC reopen rejects a damaged staged candidate before publication");

                // Force precise I12 only on an independent fixture copy. This
                // header is deliberately not represented by decoded key tuples
                // or the older I12 StorageKey marker.
                using (var i12 = NativeAnimationOptimization.CopySource(candidate))
                {
                    foreach (var entry in i12.Children.OfType<CHR0EntryNode>().ToArray()) PreciseCHR0EntryNode.RestoreStorage(entry, null);
                    string reference = Path.Combine(output, "qualified-I12-reference.chr0"); i12.ExportChecked(reference);
                    string referenceHash = FighterProject.HashFile(reference);
                    using (var qualified = (CHR0Node)NodeFactory.FromFile(null, reference))
                    {
                        string raw = NativeChr0RawStorage.Signature(qualified), qualifiedContent = NativeAnimationOptimization.SourceContentKey(qualified), qualifiedStorage = NativeAnimationOptimization.StorageKey(qualified);
                        string rawDestination = Path.Combine(output, "qualified-I12-motion.pac");
                        BrawlRetargeter.ExportMotionPac(templatePath, rawDestination, model, qualified, null, referenceHash, null);
                        using (var savedRoot = NodeFactory.FromFile(null, rawDestination))
                            Check(NativeChr0RawStorage.Signature(Walk(savedRoot).OfType<CHR0Node>().Single(clip => clip.Name == qualified.Name)) == raw,
                                "PAC insertion preserves every raw qualified I12 header/payload/fixed-slot/padding byte while normalizing only relocation pointers");
                        string wrongHashDestination = Path.Combine(output, "wrong-qualified-hash.pac");
                        Check(MotionPacFixtureRejected(() => BrawlRetargeter.ExportMotionPac(templatePath, wrongHashDestination, model, qualified, null, new string('0', 64), null)) && !File.Exists(wrongHashDestination),
                            "PAC insertion rejects a standalone copy whose physical SHA differs from central qualification");
                        foreach (bool reservedHeader in new[] { false, true })
                        {
                            string damagedHeader = Path.Combine(output, reservedHeader ? "damaged-I12-reserved-header.pac" : "damaged-I12-frameScale.pac");
                            bool decodedIdentityUnchanged = false;
                            Check(MotionPacFixtureRejected(() => BrawlRetargeter.ExportMotionPac(templatePath, damagedHeader, model, qualified, null, referenceHash, path => {
                                CorruptMotionPacI12Header(path, qualified.Name, reservedHeader);
                                using (var changedRoot = NodeFactory.FromFile(null, path))
                                {
                                    var changedMember = Walk(changedRoot).OfType<CHR0Node>().Single(clip => clip.Name == qualified.Name);
                                    decodedIdentityUnchanged = NativeAnimationOptimization.SourceContentKey(changedMember) == qualifiedContent && NativeAnimationOptimization.StorageKey(changedMember) == qualifiedStorage;
                                }
                            })) && decodedIdentityUnchanged && !File.Exists(damagedHeader),
                                "PAC raw verification rejects changed I12 " + (reservedHeader ? "reserved header" : "frameScale") + " bytes even when every decoded tuple and StorageKey remains unchanged");
                        }
                    }
                }

                string raced = Path.Combine(output, "appeared-destination.pac"); int raceChecks = 0;
                Check(MotionPacFixtureRejected(() => BrawlRetargeter.ExportMotionPac(templatePath, raced, model, candidate,
                    () => { if (++raceChecks == 2) File.WriteAllBytes(raced, new byte[] { 2, 7, 18, 28 }); })) &&
                    File.ReadAllBytes(raced).SequenceEqual(new byte[] { 2, 7, 18, 28 }),
                    "PAC publication preserves a destination created while candidate verification was running");

                string changedPrepared = Path.Combine(output, "changed-prepared-copy.pac"); int preparedChecks = 0;
                using (var editedCandidate = NativeAnimationOptimization.CopySource(candidate))
                    Check(MotionPacFixtureRejected(() => BrawlRetargeter.ExportMotionPac(templatePath, changedPrepared, model, editedCandidate,
                        () => { if (++preparedChecks == 2) editedCandidate.Name += " changed"; })) && !File.Exists(changedPrepared),
                        "PAC publication rejects a prepared animation modified during the final dependency callback");

                string changedTemplate = Path.Combine(output, "mutable-template.pac"), rejectedTemplate = Path.Combine(output, "changed-template-output.pac");
                File.Copy(templatePath, changedTemplate); int templateChecks = 0;
                Check(MotionPacFixtureRejected(() => BrawlRetargeter.ExportMotionPac(changedTemplate, rejectedTemplate, model, candidate,
                    () => { if (++templateChecks == 2) using (var stream = new FileStream(changedTemplate, FileMode.Append)) stream.WriteByte(0); })) && !File.Exists(rejectedTemplate),
                    "PAC publication rejects a template whose bytes changed immediately before commit");

                Check(!Directory.EnumerateFiles(output, ".fp-*").Any(), "PAC success and every rejected publication remove operation-owned staging files");
                Check(FighterProject.HashFile(modelPath) == modelHash && FighterProject.HashFile(templatePath) == templateHash,
                    "Authentic model and original motion PAC bytes remain immutable through success and all failures");
                File.WriteAllText(Path.Combine(output, "pac-verification.json"), new JavaScriptSerializer().Serialize(new {
                    TargetInputSha256 = modelHash, TemplateInputSha256 = templateHash, MasterContent = content, MasterStorage = storage,
                    PublishedContainerBytes = new FileInfo(destination).Length, UntouchedEntries = siblings.Count,
                    ActualContainerBytesMeasured = true, ContainerCompressionRatioClaimed = false,
                    OriginalFilesPreserved = true, LiveDocumentMutated = false, OSInputInjected = false }));
            }
        }
    }

    static bool MotionPacFixtureRejected(Action action)
    {
        try { action(); return false; }
        catch (InvalidDataException) { return true; }
        catch (IOException) { return true; }
        catch (InvalidOperationException) { return true; }
        catch (NotSupportedException) { return true; }
    }

    static bool MotionPacFixtureEqual(Dictionary<string, string> expected, Dictionary<string, string> actual)
        => expected.Count == actual.Count && expected.All(item => actual.TryGetValue(item.Key, out string value) && value == item.Value);

    static Dictionary<string, string> MotionPacFixtureSiblings(ResourceNode root, CHR0Node selected, string output)
    {
        string IndexPath(ResourceNode node) { var indices = new Stack<int>(); while (node.Parent != null) { indices.Push(node.Index); node = node.Parent; } return "/" + string.Join("/", indices); }
        string selectedPath = IndexPath(selected); var report = new Dictionary<string, string>();
        foreach (var node in Walk(root))
        {
            string path = IndexPath(node);
            if (path == selectedPath || path.StartsWith(selectedPath + "/", StringComparison.Ordinal)) continue;
            string value = node.GetType().FullName + "|" + node.Name + "|" + node.Children.Count + "|" + node.Compression;
            if (node is ARCEntryNode entry && node.Parent is ARCNode)
                value += "|" + entry.FileType + "|" + entry.FileIndex + "|" + entry.GroupID + "|" + entry.RedirectIndex;
            if (node is CHR0Node clip) value += "|" + NativeAnimationOptimization.SourceContentKey(clip) + "|" + NativeAnimationOptimization.StorageKey(clip) + "|" + NativeChr0RawStorage.Signature(clip);
            else if (node is BRESEntryNode)
            {
                string file = Path.Combine(output, ".fixture-sibling-" + Guid.NewGuid().ToString("N") + ".bin");
                try { node.ExportChecked(file); value += "|" + FighterProject.HashFile(file); }
                finally { if (File.Exists(file)) File.Delete(file); }
            }
            report.Add(path, value);
        }
        return report;
    }

    static unsafe void CorruptMotionPacI12Header(string path, string clipName, bool reservedHeader)
    {
        int resourceOffset;
        using (var root = NodeFactory.FromFile(null, path))
        {
            var clip = Walk(root).OfType<CHR0Node>().Single(item => item.Name == clipName);
            resourceOffset = checked((int)((byte*)clip.WorkingUncompressed.Address - (byte*)root.WorkingUncompressed.Address));
        }
        var bytes = File.ReadAllBytes(path);
        int Word(int at) => unchecked((int)((uint)bytes[at] << 24 | (uint)bytes[at + 1] << 16 | (uint)bytes[at + 2] << 8 | bytes[at + 3]));
        int group = checked(resourceOffset + Word(resourceOffset + 16)), count = Word(group + 4);
        for (int row = 0; row < count; row++)
        {
            int entry = checked(group + Word(group + 24 + row * 16 + 12)), slot = entry + 8; uint code = unchecked((uint)Word(entry + 4));
            for (int g = 0; g < 3; g++)
            {
                if ((code & (1u << (22 + g))) == 0) continue;
                bool isotropic = (code & (1u << (4 + g))) != 0;
                int format = g == 0 ? (int)(code >> 25) & 3 : g == 1 ? (int)(code >> 27) & 7 : (int)(code >> 30) & 3;
                for (int axis = 0; axis < (isotropic ? 1 : 3); axis++, slot += 4)
                {
                    int channel = 3 * g + (isotropic ? 2 : axis);
                    if (format != 3 || (code & (1u << (13 + channel))) != 0) continue;
                    int stream = checked(entry + Word(slot));
                    // I12: ushort count, ushort reserved, float frameScale.
                    bytes[stream + (reservedHeader ? 3 : 7)] ^= 1;
                    File.WriteAllBytes(path, bytes); return;
                }
            }
        }
        throw new InvalidOperationException("The independent precise fixture did not contain an I12 stream.");
    }
}
