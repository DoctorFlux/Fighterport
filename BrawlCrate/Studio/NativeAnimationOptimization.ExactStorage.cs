using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace BrawlCrate.Studio
{
    internal sealed class NativeExactStorageCopyResult : IDisposable
    {
        internal CHR0Node Candidate;
        internal long BeforeBytes, AfterBytes;
        internal int Groups;
        internal string Report;
        public void Dispose() { Candidate?.Dispose(); Candidate = null; }
    }

    internal static partial class NativeAnimationOptimization
    {
        // Storage-only proposals: every native time/value/tangent bit remains
        // unchanged. The owner must still serialize/reopen the complete copy
        // and compare its actual bytes before accepting these plans.
        internal static int ApplyExactStorageChoices(CHR0Node candidate, int[] mandatoryFrames,
            int[] fitJoinFrames, double lengthScale, CancellationToken token)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            token.ThrowIfCancellationRequested();
            var entries = candidate.Children.OfType<CHR0EntryNode>().ToArray();
            if (entries.Length != candidate.Children.Count || entries.Sum(e => e.KeyArrays.Sum(a => (long)a.KeyCount)) > MaximumKeys)
                throw new InvalidDataException("Exact storage requires bounded native CHR0 entry data.");
            // Capture raw imported payloads before replacing any native entry:
            // their terminal boundaries are resolved from the complete owner.
            var retained = entries.Select(entry => new { Entry = entry, Storage = PreciseCHR0EntryNode.CaptureStorage(entry) }).ToArray();
            if (retained.Any(item => item.Storage == null && IndexedCHR0EntryStorage.HasEncodedDenseRotation(item.Entry)))
                throw new InvalidDataException("The complete imported baked terminal payload is unavailable; exact storage retains the reference.");
            int groups = 0;
            foreach (var item in retained)
            {
                var entry = item.Entry is PreciseCHR0EntryNode ? item.Entry : PreciseCHR0EntryNode.RestoreStorage(item.Entry, item.Storage);
                for (int group = 0; group < 3; group++)
                {
                    token.ThrowIfCancellationRequested();
                    var imported = PreciseCHR0EntryNode.CaptureStorage(entry);
                    // Imported baked rotations own their full terminal payload.
                    // Never replace that storage with an indexed projection.
                    if (group == 1 && imported != null && imported.MatchesGroup(entry.Keyframes, group) &&
                        Enumerable.Range(3, 3).Any(c => imported[c] != null && imported[c].IsDense)) continue;
                    var proposal = NativeCompactAnimationStorage.CreateAlternatives(entry, group, true, true,
                        mandatoryFrames, lengthScale, false, fitJoinFrames, token).FirstOrDefault();
                    if (proposal == null) continue;
                    NativeCompactAnimationStorage.ApplyGroup(candidate, proposal); groups++;
                }
            }
            return groups;
        }

        // Authoring workflows may retain a smaller representation only when
        // the finished numeric master is identical after real serialization.
        // Approximation and export-only policy remain separate operations.
        internal static NativeExactStorageCopyResult PrepareExactStorageCopy(CHR0Node source,
            string stageDirectory, CancellationToken token)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            token.ThrowIfCancellationRequested();
            if (source.Version != 4 && source.Version != 5)
                throw new NotSupportedException("Exact compact storage qualifies native CHR0 v4/v5 only.");
            if (source.Children.OfType<CHR0EntryNode>().Sum(e => e.KeyArrays.Sum(a => (long)a.KeyCount)) > MaximumKeys)
                throw new InvalidDataException("Exact compact storage exceeds the native two-million-key operation budget.");
            string content = SourceContentKey(source), storage = StorageKey(source);
            void Current()
            {
                token.ThrowIfCancellationRequested();
                if (SourceContentKey(source) != content || StorageKey(source) != storage)
                    throw new InvalidOperationException("The finished animation changed during exact storage preparation.");
            }
            Directory.CreateDirectory(stageDirectory);
            string referencePath = Path.Combine(stageDirectory, "exact-storage-reference-" + Guid.NewGuid().ToString("N") + ".chr0");
            string candidatePath = Path.Combine(stageDirectory, "exact-storage-candidate-" + Guid.NewGuid().ToString("N") + ".chr0");
            CHR0Node reference = null, candidate = null;
            try
            {
                reference = CopySource(source);
                if (SourceContentKey(reference) != content || StorageKey(reference) != storage)
                    throw new InvalidDataException("The exact storage reference did not retain the complete master and imported payloads.");
                reference.ExportChecked(referencePath);
                long beforeResource;
                using (var decoded = NodeFactory.FromFile(null, referencePath) as CHR0Node)
                {
                    if (decoded == null || SourceContentKey(decoded) != content || StorageKey(decoded) != storage)
                        throw new InvalidDataException("The exact storage reference did not freshly reopen with all native data intact.");
                    beforeResource = ResourceBlockBytes(referencePath, decoded);
                }
                long beforeBytes = new FileInfo(referencePath).Length;
                string report = "Exact native storage unchanged; the finished master remains intact.";
                int groups = 0;
                long afterBytes = beforeBytes;
                try
                {
                    Current(); candidate = CopySource(reference);
                    groups = ApplyExactStorageChoices(candidate, new int[0], new int[0], 0, token);
                    if (SourceContentKey(candidate) != content)
                        throw new InvalidDataException("An exact native storage proposal changed master coefficients or metadata.");
                    string candidateStorage = StorageKey(candidate);
                    if (candidateStorage != storage)
                    {
                        candidate.ExportChecked(candidatePath);
                        long afterResource;
                        using (var decoded = NodeFactory.FromFile(null, candidatePath) as CHR0Node)
                        {
                            if (decoded == null || SourceContentKey(decoded) != content || StorageKey(decoded) != candidateStorage)
                                throw new InvalidDataException("The exact storage candidate did not freshly reopen with identical native data and complete payloads.");
                            afterResource = ResourceBlockBytes(candidatePath, decoded);
                        }
                        afterBytes = new FileInfo(candidatePath).Length;
                        if (afterBytes < beforeBytes && afterResource <= beforeResource)
                        {
                            Current();
                            var result = new NativeExactStorageCopyResult { Candidate = candidate, BeforeBytes = beforeBytes,
                                AfterBytes = afterBytes, Groups = groups,
                                Report = "Exact native storage: " + beforeBytes + " → " + afterBytes + " bytes; " + groups + " proposed S/R/T groups. Master coefficients are unchanged." };
                            candidate = null; return result;
                        }
                        report = "Exact native storage retained the smaller finished-master reference; compact plans provided no file/resource saving.";
                    }
                }
                catch (Exception error) when (!(error is OperationCanceledException) &&
                    (error is InvalidDataException || error is InvalidOperationException || error is NotSupportedException || error is IOException || error is OverflowException))
                {
                    Current();
                    report = "Exact native storage reference retained: " + error.Message;
                }
                Current();
                var unchanged = new NativeExactStorageCopyResult { Candidate = reference, BeforeBytes = beforeBytes,
                    AfterBytes = beforeBytes, Groups = 0, Report = report };
                reference = null; return unchanged;
            }
            finally
            {
                candidate?.Dispose(); reference?.Dispose();
                foreach (string path in new[] { referencePath, candidatePath })
                    try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
            }
        }
    }
}
