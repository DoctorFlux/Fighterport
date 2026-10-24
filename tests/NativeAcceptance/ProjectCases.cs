using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class Program
{
    private static void StorageCases(string realModel, string realMotion, string output)
    {
        Directory.CreateDirectory(output);
        string emptyPath = Path.Combine(output, "Empty.fpstudio");
        using (var empty = FighterProject.Create("Empty Fighter")) empty.Save(emptyPath);
        using (var empty = FighterProject.Open(emptyPath))
            Check(empty.Model == null && empty.Animations.Count == 0, "A new fighter saves and reopens before model or animation import");

        string modelOnlyPath = Path.Combine(output, "ModelOnly.fpstudio");
        using (var project = FighterProject.Create("Model Only Mario"))
        {
            project.Model = project.StageAsset(realModel, "model");
            Check(!string.Equals(project.Model.WorkingPath, realModel, StringComparison.OrdinalIgnoreCase) && Hash(project.Model.WorkingPath) == Hash(realModel),
                "Import creates an independent byte-identical working copy of the real source");
            project.Save(modelOnlyPath);
        }
        using (var reopened = FighterProject.Open(modelOnlyPath))
            Check(reopened.Model != null && reopened.Animations.Count == 0 && reopened.Warnings.Count == 0,
                "A model-only fighter reopens without requiring animations or companion manifests");

        string manifest = Path.Combine(output, "MarioAcceptance.fpstudio");
        string expectedEditedBone;
        float expectedEditedValue;
        using (var project = FighterProject.Create("Mario Acceptance"))
        {
            project.Model = project.StageAsset(realModel, "model");
            FighterAsset motions = project.StageAsset(realMotion, "animation");
            project.Animations.Add(motions);
            using (ResourceNode native = NodeFactory.FromFile(null, motions.WorkingPath))
            {
                native.Populate();
                CHR0Node clip = Walk(native).OfType<CHR0Node>().First(c => c.Name == "Attack11");
                string directMotion = Path.Combine(output, "Attack11.chr0");
                clip.Export(directMotion);
                project.Animations.Add(project.StageAsset(directMotion, "animation"));
                CHR0EntryNode entry = clip.Children.OfType<CHR0EntryNode>().First();
                expectedEditedBone = entry.Name;
                expectedEditedValue = entry.GetFrameValue(5, 0) + 7;
                entry.SetKeyframe(5, 0, expectedEditedValue);
                native.Export(motions.WorkingPath);
            }
            project.SelectedAnimation = "Attack11";
            project.ModelNodePath = "FitMario00";
            project.Frame = 9;
            project.Save(manifest);
            Check(project.Model.SourceSha256 == Hash(realModel) && motions.SourceSha256 == Hash(realMotion) && motions.ContentSha256 != motions.SourceSha256,
                "Native edits update saved working content while retaining the original source identity");
            string firstSave = Hash(manifest);
            project.Save(manifest);
            Check(File.Exists(manifest) && Hash(manifest) == firstSave, "Saving an existing fighter atomically preserves a coherent descriptor");
            MustReject(() => project.ProtectOutputPath(realModel), "Native export refuses an original model filename");
            MustReject(() => project.ProtectOutputPath(realMotion), "Native export refuses an original motion filename");
            MustReject(() => project.ProtectOutputPath(project.Model.WorkingPath), "Native export refuses the managed working copy");
            MustReject(() => project.ProtectOutputPath(Path.Combine(output, project.Model.RelativePath.Replace('/', Path.DirectorySeparatorChar))), "Native export refuses the saved asset copy");
            MustReject(() => project.ProtectOutputPath(manifest), "Native export refuses the project descriptor");
            string existing = Path.Combine(output, "Existing.chr0");
            File.WriteAllText(existing, "must remain unchanged");
            MustReject(() => project.ProtectOutputPath(existing), "Native export requires a fresh filename and preserves existing files");
            Check(File.ReadAllText(existing) == "must remain unchanged", "Rejected native output leaves the destination untouched");
            project.ProtectOutputPath(Path.Combine(output, "Fresh-native.chr0"));
            Check(true, "Native export accepts a fresh filename outside managed assets");
        }

        using (var project = FighterProject.Open(manifest))
        {
            Check(project.Name == "Mario Acceptance" && project.TargetGame == "Brawl" && project.Frame == 9 && project.SelectedAnimation == "Attack11" && project.Animations.Count == 2,
                "Fresh project objects restore fighter metadata, selected animation, frame and separate motion registrations");
            Check(project.RegisteredAssets().All(a => File.Exists(a.WorkingPath) && Hash(a.WorkingPath) == a.ContentSha256),
                "Fresh open verifies and independently stages every saved model and motion file");
            using (ResourceNode savedMotion = NodeFactory.FromFile(null, project.Animations[0].WorkingPath))
            {
                savedMotion.Populate();
                CHR0Node clip = Walk(savedMotion).OfType<CHR0Node>().First(c => c.Name == "Attack11");
                Check(Math.Abs(((CHR0EntryNode)clip.FindChild(expectedEditedBone, false)).GetFrameValue(5, 0) - expectedEditedValue) < 0.03,
                    "Saved fighter project restores the edited native animation from its working copy");
            }
        }
        VerifyFreshProcess(manifest);
        RejectionCases(manifest, output);
        SourceAvailabilityCases(realModel, output);
        SavedContentCases(realModel, output);
        FailedSavePreservesCommittedRevision(realModel, output);
    }

    private static void VerifyFreshProcess(string manifest)
    {
        var start = new ProcessStartInfo
        {
            FileName = typeof(Program).Assembly.Location,
            Arguments = "--reopen \"" + manifest.Replace("\"", "\\\"") + "\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using (Process child = Process.Start(start))
        {
            if (!child.WaitForExit(20000))
            {
                child.Kill();
                throw new TimeoutException("Fresh-process project open did not complete within 20 seconds.");
            }
            string standardOutput = child.StandardOutput.ReadToEnd(), standardError = child.StandardError.ReadToEnd();
            Check(child.ExitCode == 0 && standardOutput.Contains("FRESH_PROCESS_PASS"), "A separate fresh executable process reopens the saved project and verifies all asset hashes", new { child.ExitCode, Output = standardOutput.Trim(), Error = standardError.Trim() });
        }
    }

    private static int ReopenChild(string[] args)
    {
        using (FighterProject project = FighterProject.Open(args[1]))
        {
            if (project.Name != "Mario Acceptance" || project.SelectedAnimation != "Attack11" || project.Frame != 9 || project.Animations.Count != 2 || project.Model == null)
                throw new InvalidDataException("Fresh-process metadata did not survive.");
            foreach (FighterAsset asset in project.RegisteredAssets())
                if (Hash(asset.WorkingPath) != asset.ContentSha256) throw new InvalidDataException("Fresh-process saved content mismatch.");
            Console.WriteLine("FRESH_PROCESS_PASS");
            return 0;
        }
    }

    private static void RejectionCases(string goodManifest, string output)
    {
        int caseNumber = 0;
        Action<string, string, Action<XDocument>> reject = (name, expectedMessage, edit) =>
        {
            // Keep test filenames short: detailed prose belongs in the receipt, and
            // content-addressed native assets need room for an atomic-copy suffix.
            string path = Path.Combine(output, "Rejected-" + (++caseNumber).ToString("00") + ".fpstudio");
            // Save As first: each negative case starts with its own valid asset folder.
            // Merely renaming a descriptor would reject even an unmodified path.
            using (var good = FighterProject.Open(goodManifest)) good.Save(path);
            using (var good = FighterProject.Open(path)) { }
            XDocument xml = XDocument.Load(path);
            edit(xml);
            xml.Save(path);
            MustReject(() => { using (FighterProject ignored = FighterProject.Open(path)) { } }, name, expectedMessage);
        };
        reject("Unsupported schema is rejected", "Unsupported fighter project version", xml => xml.Root.SetAttributeValue("SchemaVersion", "999"));
        reject("Parent traversal in saved asset paths is rejected", "saved asset path is unsafe", xml => xml.Descendants("RelativePath").First().Value = "../outside.pac");
        reject("Absolute saved asset paths are rejected", "saved asset path is unsafe", xml => xml.Descendants("RelativePath").First().Value = Path.GetFullPath(goodManifest));
        reject("Paths outside this descriptor's asset folder are rejected", "inside this fighter project's asset folder", xml => xml.Descendants("RelativePath").First().Value = "Unrelated.assets/model.pac");
        reject("Malformed saved preview metadata is rejected", "saved preview selection is invalid", xml => xml.Descendants("Frame").First().Value = "-1");
        string oversized = Path.Combine(output, "Oversized.fpstudio");
        File.WriteAllText(oversized, File.ReadAllText(goodManifest) + "<!--" + new string('x', 1024 * 1024) + "-->", new UTF8Encoding(false));
        MustReject(() => { using (FighterProject ignored = FighterProject.Open(oversized)) { } }, "Project descriptors over one MiB are rejected before deserialization", "document limit");
        string dtd = Path.Combine(output, "Dtd.fpstudio");
        string plain = File.ReadAllText(goodManifest);
        int declarationEnd = plain.IndexOf("?>", StringComparison.Ordinal);
        plain = declarationEnd >= 0 ? plain.Substring(declarationEnd + 2) : plain;
        File.WriteAllText(dtd, "<!DOCTYPE FighterPortStudio [<!ENTITY unwanted 'value'>]>" + plain, new UTF8Encoding(false));
        MustReject(() => { using (FighterProject ignored = FighterProject.Open(dtd)) { } }, "XML DTD declarations are rejected", "DTD");
    }

    private static void SourceAvailabilityCases(string realModel, string output)
    {
        string folder = Path.Combine(output, "OwnedSource");
        Directory.CreateDirectory(folder);
        string copy = Path.Combine(folder, "LocalModel.pac");
        File.Copy(realModel, copy);
        string manifest = Path.Combine(output, "SourceAvailability.fpstudio");
        using (var project = FighterProject.Create("Recoverable Mario"))
        {
            project.Model = project.StageAsset(copy, "model");
            project.Save(manifest);
        }
        string hash = Hash(copy);
        string held = copy + ".held";
        // These are explicitly owned disposable copies beneath this run's new output directory.
        File.Move(copy, held);
        using (var reopened = FighterProject.Open(manifest))
            Check(reopened.Warnings.Any(w => w.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0) && Hash(reopened.Model.WorkingPath) == hash,
                "A missing original source reports a warning while the saved native model remains available");
        File.Move(held, copy);
        File.WriteAllBytes(copy, new byte[] { 0x42, 0x61, 0x64 });
        using (var reopened = FighterProject.Open(manifest))
            Check(reopened.Warnings.Any(w => w.IndexOf("changed", StringComparison.OrdinalIgnoreCase) >= 0) && Hash(reopened.Model.WorkingPath) == hash,
                "A replaced original source reports a warning and cannot silently replace saved project content");
    }

    private static void SavedContentCases(string realModel, string output)
    {
        foreach (string mode in new[] { "Missing", "Replaced" })
        {
            string manifest = Path.Combine(output, mode + "SavedAsset.fpstudio");
            string savedPath;
            using (var project = FighterProject.Create(mode + " Saved Asset"))
            {
                project.Model = project.StageAsset(realModel, "model");
                project.Save(manifest);
                savedPath = Path.Combine(output, project.Model.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            }
            // Only mutate a copy inside this test-owned project, never the provided source.
            if (mode == "Missing") File.Move(savedPath, savedPath + ".held");
            else File.WriteAllBytes(savedPath, new byte[] { 0x42, 0x61, 0x64 });
            MustReject(() => { using (FighterProject ignored = FighterProject.Open(manifest)) { } }, mode + " saved native content is rejected instead of using unverified data",
                mode == "Missing" ? "required project or source file is missing" : "replaced or damaged");
        }
    }

    private static void FailedSavePreservesCommittedRevision(string realModel, string output)
    {
        string manifest = Path.Combine(output, "LockedSave.fpstudio");
        using (var project = FighterProject.Create("Locked Save Mario"))
        {
            project.Model = project.StageAsset(realModel, "model");
            project.Save(manifest);
            string committedHash = project.Model.ContentSha256;
            string committedRelative = project.Model.RelativePath;
            string descriptorHash = Hash(manifest);
            using (ResourceNode native = NodeFactory.FromFile(null, project.Model.WorkingPath))
            {
                native.Populate();
                Walk(native).OfType<MDL0Node>().First(m => m.Name == "FitMario00").Name = "FitMario00Edited";
                native.Export(project.Model.WorkingPath);
            }
            string editedHash = Hash(project.Model.WorkingPath);
            Check(editedHash != committedHash, "A real native model edit creates a distinct pending working revision");
            using (var locked = new FileStream(manifest, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                MustReject(() => project.Save(manifest), "A descriptor locked against replacement causes save to fail");
                Check(Hash(manifest) == descriptorHash, "Failed atomic replacement preserves the complete previously committed descriptor bytes");
                Check(project.Model.ContentSha256 == committedHash && project.Model.RelativePath == committedRelative,
                    "Failed save restores in-memory asset identity and relative path to the committed revision");
                Check(Hash(project.Model.WorkingPath) == editedHash, "Failed save preserves the unsaved native staging edit for retry");
                using (var oldRevision = FighterProject.Open(manifest))
                    Check(oldRevision.Model.ContentSha256 == committedHash && Hash(oldRevision.Model.WorkingPath) == committedHash,
                        "Fresh open during a failed save still resolves the previously committed native revision");
            }
            project.Save(manifest);
            using (var retried = FighterProject.Open(manifest))
            {
                Check(retried.Model.ContentSha256 == editedHash && Hash(retried.Model.WorkingPath) == editedHash,
                    "Retry after unlocking commits and reopens the preserved edited native revision");
                using (ResourceNode native = NodeFactory.FromFile(null, retried.Model.WorkingPath))
                {
                    native.Populate();
                    Check(Walk(native).OfType<MDL0Node>().Any(m => m.Name == "FitMario00Edited"),
                        "The retried saved revision fresh-decodes the actual native model edit");
                }
            }
        }
    }
}
