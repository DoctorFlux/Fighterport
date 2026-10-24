using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace BrawlCrate.UI
{
    internal partial class ModelForm
    {
        private sealed class FrozenSetItem
        {
            internal Chr0ExportSnapshot Snapshot;
            internal string Key, Filename, SourceId, ClipName;
            internal int Frames;
        }
        private sealed class FrozenAnimationSet : IDisposable
        {
            internal readonly List<FrozenSetItem> Items = new List<FrozenSetItem>();
            internal string ProjectName, TargetName, TargetIdentity, TargetSha256;
            internal Action Validate;
            public void Dispose() { foreach (var item in Items) item.Snapshot.Dispose(); }
        }
        private sealed class PreparedAnimationSet : IDisposable
        {
            internal string Temporary, ReceiptHash;
            internal readonly Dictionary<string, string> Hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            internal readonly List<string> Files = new List<string>();
            public void Dispose()
            {
                // Delete only files this operation created. Never recursively
                // traverse a destination or an unexpected directory entry.
                foreach (string file in Files) try { if (File.Exists(file)) File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                try { if (Temporary != null && Directory.Exists(Temporary)) Directory.Delete(Temporary, false); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
        private FrozenAnimationSet _activeSetExport;
        internal Action<string> SetExportCandidateWritten;
        internal Action SetExportBeforePublish;

        private static string SetExportFilename(string name, string key)
        {
            string stem = new string(name.Select(c => char.IsControl(c) || Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
            if (stem.Length == 0) stem = "Animation";
            if (stem.Length > 80) stem = stem.Substring(0, 80).TrimEnd('.');
            using (var sha = SHA256.Create())
                return stem + "-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "").Substring(0, 12).ToLowerInvariant() + ".chr0";
        }
        private FrozenAnimationSet FreezeWorkspaceSet()
        {
            var rows = ValidateBatchReviewRows();
            long keys = rows.Sum(r => r.Clip.Children.OfType<CHR0EntryNode>().Sum(e => e.KeyArrays.Sum(c => (long)c.KeyCount)));
            if (keys > 2000000) throw new InvalidOperationException("This set exceeds two million native keys. Use a smaller review set.");
            var project = _fighterProject; var target = (MDL0Node)modelEditControl1.TargetModel;
            string targetContent = rows[0].TargetContent;
            var set = new FrozenAnimationSet { ProjectName = project.Name, TargetName = target.Name,
                TargetIdentity = FighterAnimationKey(project.Model, target), TargetSha256 = targetContent };
            try
            {
                foreach (var row in rows)
                {
                    var asset = ReviewAsset(row);
                    var snapshot = FreezeOwnedWorkspaceExport(asset, row.Clip, false, () => ReferenceEquals(_activeSetExport, set));
                    set.Items.Add(new FrozenSetItem { Snapshot = snapshot, Key = row.Key, ClipName = row.Clip.Name,
                        Frames = row.Clip.FrameCount, Filename = SetExportFilename(row.Clip.Name, row.Key), SourceId = asset.Retarget?.Library?.ClipId });
                }
                ValidateSetFilenames(set);
                set.Validate = () =>
                {
                    if (_controlBusy || _retargetBusy || _pinGesture != null || _workspaceBusy && !ReferenceEquals(_activeSetExport, set) ||
                        !ReferenceEquals(project, _fighterProject) || !ReferenceEquals(target, modelEditControl1.TargetModel) || project.Name != set.ProjectName ||
                        !_batchReviewRows.SequenceEqual(rows) || project.Model == null || FighterAnimationKey(project.Model, target) != set.TargetIdentity)
                        throw new InvalidOperationException("Reviewed set dependencies changed. Review the current clips and open Export reviewed set again.");
                    bool changedTarget = NativeControlHash(target, ".mdl0") != targetContent;
                    foreach (var row in rows)
                        if (changedTarget || !ReviewRowPresent(row) || row.Recipe != ReviewRecipe(ReviewAsset(row)) || row.Content != ExportContent(row.Clip)) row.Approved = false;
                    if (rows.Any(r => !r.Approved))
                    {
                        _batchReviewDirty = true; SyncBatchReview();
                        throw new InvalidOperationException("Reviewed content, target or recipe changed. Review the current clips before exporting.");
                    }
                    foreach (var item in set.Items) item.Snapshot.Validate();
                };
                return set;
            }
            catch { set.Dispose(); throw; }
        }
        private static void ValidateSetFilenames(FrozenAnimationSet set)
        {
            if (set.Items.Any(i => Path.GetFileName(i.Filename) != i.Filename || !i.Filename.EndsWith(".chr0", StringComparison.OrdinalIgnoreCase)) ||
                set.Items.Select(i => i.Filename).Distinct(StringComparer.OrdinalIgnoreCase).Count() != set.Items.Count)
                throw new InvalidOperationException("Proposed set filenames collide or are invalid. Rename the affected animation and review it again.");
        }
        private void ValidateSetDestination(FrozenAnimationSet set, string destination)
        {
            ValidateSetFilenames(set);
            destination = AnimationLibrary.OrdinaryPath(destination);
            AnimationLibrary.NoLinks(destination);
            if (File.Exists(destination) || Directory.Exists(destination) || !Directory.Exists(Path.GetDirectoryName(destination)))
                throw new IOException("Choose a new, nonexistent set folder within an existing folder. Existing destinations are preserved.");
            ProtectVerifiedExport(destination);
            ProtectVerifiedExport(Path.Combine(destination, "fighterport-set.json"));
            foreach (var item in set.Items) ProtectVerifiedExport(Path.Combine(destination, item.Filename));
        }
        private PreparedAnimationSet PrepareWorkspaceSet(FrozenAnimationSet set, string destination, CancellationToken token, IProgress<string> progress)
        {
            var prepared = new PreparedAnimationSet { Temporary = Path.Combine(Path.GetDirectoryName(destination), ".fighterport-set-" + Guid.NewGuid().ToString("N")) };
            try
            {
                token.ThrowIfCancellationRequested();
                if (Directory.Exists(prepared.Temporary) || File.Exists(prepared.Temporary)) throw new IOException("The staging location is unavailable.");
                Directory.CreateDirectory(prepared.Temporary);
                int count = 0;
                foreach (var item in set.Items)
                {
                    token.ThrowIfCancellationRequested();
                    progress?.Report("Writing and verifying " + (++count) + " / " + set.Items.Count + ": " + item.ClipName);
                    string path = Path.Combine(prepared.Temporary, item.Filename); prepared.Files.Add(path);
                    item.Snapshot.Candidate.ExportChecked(path);
                    SetExportCandidateWritten?.Invoke(path);
                    token.ThrowIfCancellationRequested();
                    using (var decoded = NodeFactory.FromFile(null, path) as CHR0Node)
                        if (decoded == null || ExportContent(decoded) != item.Snapshot.ExpectedContent)
                            throw new InvalidDataException("Set candidate verification failed: native metadata, timing, keys, tangents or flags differ. No set was published.");
                    prepared.Hashes.Add(item.Key, FighterProject.HashFile(path));
                }
                token.ThrowIfCancellationRequested();
                var receipt = new { Schema = 1, Format = "Brawl CHR0", Project = set.ProjectName, Target = set.TargetName, set.TargetIdentity, set.TargetSha256,
                    Clips = set.Items.Select(i => new { Identity = i.Key, Name = i.ClipName, i.Frames, i.Filename, SourceClipId = i.SourceId, Sha256 = prepared.Hashes[i.Key] }).ToArray() };
                string manifest = Path.Combine(prepared.Temporary, "fighterport-set.json"); prepared.Files.Add(manifest);
                byte[] bytes = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(receipt));
                using (var file = new FileStream(manifest, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                prepared.ReceiptHash = FighterProject.HashFile(manifest);
                return prepared;
            }
            catch { prepared.Dispose(); throw; }
        }
        private static void RecheckPreparedSet(FrozenAnimationSet set, PreparedAnimationSet prepared, CancellationToken token)
        {
            var entries = Directory.EnumerateFileSystemEntries(prepared.Temporary).Take(prepared.Files.Count + 1).ToArray();
            if (entries.Length != prepared.Files.Count || !new HashSet<string>(entries, StringComparer.OrdinalIgnoreCase).SetEquals(prepared.Files))
                throw new IOException("Staged set contents changed. No set was published.");
            foreach (var item in set.Items)
            {
                token.ThrowIfCancellationRequested(); string path = Path.Combine(prepared.Temporary, item.Filename); AnimationLibrary.NoLinks(path);
                if (FighterProject.HashFile(path) != prepared.Hashes[item.Key]) throw new IOException("A verified candidate changed before publication. No set was published.");
            }
            if (FighterProject.HashFile(Path.Combine(prepared.Temporary, "fighterport-set.json")) != prepared.ReceiptHash)
                throw new IOException("The set receipt changed. No set was published.");
        }
        private async Task ExportWorkspaceSet(FrozenAnimationSet set, string destination, CancellationToken token, IProgress<string> progress)
        {
            set.Validate(); destination = AnimationLibrary.OrdinaryPath(destination); ValidateSetDestination(set, destination);
            _activeSetExport = set; _workspaceBusy = true; UpdateSimpleWorkspace();
            PreparedAnimationSet prepared = null;
            try
            {
                // The worker owns only frozen native candidates. Modal cancellation
                // waits for this task before releasing those resources.
                prepared = await Task.Run(() => PrepareWorkspaceSet(set, destination, token, progress));
                token.ThrowIfCancellationRequested(); set.Validate(); ValidateSetDestination(set, destination);
                SetExportBeforePublish?.Invoke();
                await Task.Run(() => RecheckPreparedSet(set, prepared, token));
                token.ThrowIfCancellationRequested(); set.Validate(); ValidateSetDestination(set, destination);
                Directory.Move(prepared.Temporary, destination);
                // Commit point passed. Paths in the staging cleanup no longer
                // exist; never roll back or delete the published directory.
                foreach (var item in set.Items)
                {
                    var asset = _fighterProject.Animations.FirstOrDefault(a => a.Id == _batchReviewRows.Single(r => r.Key == item.Key).AssetId);
                    if (asset?.Retarget?.Library != null)
                    {
                        asset.Retarget.Library.LastExportSha256 = prepared.Hashes[item.Key];
                        asset.Retarget.Library.LastExportFormat = "CHR0"; MarkFighterChanged();
                    }
                }
                _fighterStatus.Text = "Verified and exported " + set.Items.Count + " animations to " + destination;
            }
            finally
            {
                prepared?.Dispose(); _activeSetExport = null; _workspaceBusy = false; UpdateSimpleWorkspace();
                if (_controlReloadRequested) QueueControlSetup();
            }
        }
        private void ChooseWorkspaceSetExport()
        {
            using (var set = FreezeWorkspaceSet())
            using (var dialog = new WorkspaceSetExportDialog(set.ProjectName, set.TargetName, set.TargetIdentity, set.TargetSha256,
                set.Items.Select(i => i.ClipName + " · " + i.Frames + " frames → " + i.Filename + Environment.NewLine + "  Exact result: " + i.Key).ToArray()))
            {
                dialog.ValidateDestination = path => ValidateSetDestination(set, path);
                dialog.Export = (path, token, progress) => ExportWorkspaceSet(set, path, token, progress);
                dialog.ShowDialog(this);
            }
        }
    }
}
