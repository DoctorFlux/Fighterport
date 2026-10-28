using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace BrawlCrate.Studio
{
    // Transient writer evidence, never a project receipt or a name-based guess.
    // Both ordered native sides, including every IEEE sign bit, are frozen at
    // the exact point where the adaptive writer creates the pair.
    internal sealed class NativeGeneratedPairEvidence
    {
        internal readonly string Bone;
        internal readonly int Channel, TimeBits, FirstValueBits, FirstTangentBits, SecondValueBits, SecondTangentBits, EntryFlags, FrameLimit;
        internal readonly bool Loop;
        internal NativeGeneratedPairEvidence(CHR0EntryNode entry, int channel, KeyframeEntry first, KeyframeEntry second)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Name) || channel < 0 || channel > 8 || first == null || second == null ||
                ReferenceEquals(first, second) || !ReferenceEquals(first._next, second) ||
                Bits(first._index) != Bits(second._index)) throw new ArgumentException("Exact writer-created pair sides are required.");
            Bone = entry.Name; Channel = channel; TimeBits = Bits(first._index); EntryFlags = Flags(entry);
            FrameLimit = entry.Keyframes.FrameLimit; Loop = entry.Keyframes.Loop;
            FirstValueBits = Bits(first._value); FirstTangentBits = Bits(first._tangent);
            SecondValueBits = Bits(second._value); SecondTangentBits = Bits(second._tangent);
        }
        internal float Time => BitConverter.ToSingle(BitConverter.GetBytes(TimeBits), 0);
        internal bool Matches(KeyframeEntry first, KeyframeEntry second) => first != null && second != null &&
            TimeBits == Bits(first._index) && TimeBits == Bits(second._index) &&
            FirstValueBits == Bits(first._value) && FirstTangentBits == Bits(first._tangent) &&
            SecondValueBits == Bits(second._value) && SecondTangentBits == Bits(second._tangent);
        internal bool SameContext(CHR0EntryNode entry) => entry.Name == Bone && Flags(entry) == EntryFlags &&
            entry.Keyframes.FrameLimit == FrameLimit && entry.Keyframes.Loop == Loop;
        internal bool Eligible => TimeBits != int.MinValue && FirstValueBits != int.MinValue && SecondValueBits != int.MinValue &&
            FirstTangentBits != int.MinValue && SecondTangentBits != int.MinValue && FirstValueBits == SecondValueBits &&
            FirstTangentBits != SecondTangentBits;
        internal NativeGeneratedPairSite Site() => new NativeGeneratedPairSite { Bone = Bone, Channel = Channel, Frame = Time };
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static int Flags(CHR0EntryNode entry) => (entry.UseModelScale ? 1 : 0) | (entry.UseModelRotate ? 2 : 0) |
            (entry.UseModelTranslate ? 4 : 0) | (entry.ScaleCompensateApply ? 8 : 0) | (entry.ScaleCompensateParent ? 16 : 0) | (entry.ClassicScaleOff ? 32 : 0);
    }

    // One bounded process-session registry. Internal copies and same-process
    // reopen can recover evidence by exact content + exact native rig. Manual
    // edits invalidate the lookup; Undo can recover a retained previous entry.
    // Starting another process intentionally establishes no such evidence.
    internal sealed class NativeGeneratedPairRegistry
    {
        internal const int MaximumClips = 32, MaximumSites = 2000000;
        internal static readonly NativeGeneratedPairRegistry Session = new NativeGeneratedPairRegistry();
        private sealed class Record
        {
            internal NativeGeneratedPairEvidence[] Sites;
            internal LinkedListNode<string> Order;
        }
        private readonly object gate = new object();
        private readonly Dictionary<string, Record> records = new Dictionary<string, Record>(StringComparer.Ordinal);
        private readonly LinkedList<string> order = new LinkedList<string>();
        private readonly int clipLimit, siteLimit;
        private int sites;
        internal int ClipCount { get { lock (gate) return records.Count; } }
        internal int SiteCount { get { lock (gate) return sites; } }
        internal NativeGeneratedPairRegistry(int maximumClips = MaximumClips, int maximumSites = MaximumSites)
        {
            if (maximumClips < 1 || maximumClips > MaximumClips || maximumSites < 1 || maximumSites > MaximumSites)
                throw new ArgumentOutOfRangeException("Session pair evidence must retain the existing bounded limits.");
            clipLimit = maximumClips; siteLimit = maximumSites;
        }

        internal static string TryRigKey(MDL0Node model, CancellationToken token = default(CancellationToken))
        {
            try { return RigKey(model, token); }
            catch (InvalidDataException) { return null; }
        }
        internal static string RigKey(MDL0Node model, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            if (model == null) throw new ArgumentNullException(nameof(model));
            var bones = model.AllBones.ToArray();
            if (bones.Length == 0 || bones.Length > 4096 || bones.Select(b => b.Name).Distinct(StringComparer.Ordinal).Count() != bones.Length)
                throw new InvalidDataException("Generated-pair provenance needs a bounded unambiguous exact native rig.");
            var indices = bones.Select((bone, index) => new { bone, index }).ToDictionary(pair => pair.bone, pair => pair.index);
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                    foreach (var bone in bones)
                    {
                        token.ThrowIfCancellationRequested();
                        writer.Write(bone.Name); writer.Write(bone.Parent is MDL0BoneNode parent && indices.TryGetValue(parent, out int index) ? index : -1);
                        for (int i = 0; i < 9; i++) writer.Write(Bits(bone.BindState[i]));
                        for (int i = 0; i < 16; i++) { writer.Write(Bits(bone.BindMatrix[i])); writer.Write(Bits(bone.InverseBindMatrix[i])); }
                        writer.Write(bone.SegScaleCompApply); writer.Write(bone.SegScaleCompParent); writer.Write(bone.ClassicScale);
                    }
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "");
            }
        }

        internal NativeGeneratedPairEvidence[] Register(CHR0Node clip, string rigKey,
            IEnumerable<NativeGeneratedPairEvidence> known, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            if (!ValidRigKey(rigKey) || !Bounded(clip)) return new NativeGeneratedPairEvidence[0];
            var captured = (known ?? Enumerable.Empty<NativeGeneratedPairEvidence>()).Take(siteLimit + 1).ToArray();
            if (captured.Length > siteLimit) return new NativeGeneratedPairEvidence[0];
            var retained = MatchKnown(clip, captured, token);
            if (retained.Length == 0) return retained;
            token.ThrowIfCancellationRequested();
            string key = Identity(clip, rigKey);
            lock (gate)
            {
                if (records.TryGetValue(key, out var previous))
                { sites -= previous.Sites.Length; order.Remove(previous.Order); records.Remove(key); }
                while (records.Count >= clipLimit || sites + retained.Length > siteLimit)
                {
                    string oldest = order.First.Value; var evicted = records[oldest];
                    sites -= evicted.Sites.Length; records.Remove(oldest); order.RemoveFirst();
                }
                records.Add(key, new Record { Sites = retained, Order = order.AddLast(key) }); sites += retained.Length;
            }
            return (NativeGeneratedPairEvidence[])retained.Clone();
        }

        internal NativeGeneratedPairEvidence[] Resolve(CHR0Node clip, string rigKey, CancellationToken token = default(CancellationToken))
        {
            token.ThrowIfCancellationRequested();
            if (!ValidRigKey(rigKey) || !Bounded(clip)) return new NativeGeneratedPairEvidence[0];
            string key = Identity(clip, rigKey);
            lock (gate)
            {
                if (!records.TryGetValue(key, out var record)) return new NativeGeneratedPairEvidence[0];
                order.Remove(record.Order); record.Order = order.AddLast(key);
                return (NativeGeneratedPairEvidence[])record.Sites.Clone();
            }
        }

        internal NativeGeneratedPairEvidence[] Carry(CHR0Node input, CHR0Node output, string rigKey,
            CancellationToken token = default(CancellationToken)) => input == null || output == null || input.Name != output.Name ||
            input.FrameCount != output.FrameCount || input.Loop != output.Loop ? new NativeGeneratedPairEvidence[0] :
            Register(output, rigKey, Resolve(input, rigKey, token), token);

        internal NativeGeneratedPairSite[] Freeze(CHR0Node clip, string rigKey, IEnumerable<string> protectedBones,
            IEnumerable<int> mandatoryFrames, out string report, CancellationToken token = default(CancellationToken))
        {
            var retained = Resolve(clip, rigKey, token);
            var excluded = new HashSet<string>(protectedBones ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var mandatory = new HashSet<float>((mandatoryFrames ?? Enumerable.Empty<int>()).Select(f => (float)f));
            var eligible = retained.Where(site => site.Eligible && !excluded.Contains(site.Bone) && !mandatory.Contains(site.Time)).ToArray();
            report = retained.Length == 0
                ? "No exact session writer evidence for this finished animation and native rig; unknown/imported paired sides remain protected. Evidence is session-only."
                : eligible.Length + " exact session-generated pair sites frozen for bounded export; " + (retained.Length - eligible.Length) +
                    " signed-zero, motion/pin/contact or mandatory-boundary sites remain protected. Unknown authored pairs remain protected. Evidence is session-only.";
            return eligible.Select(site => site.Site()).ToArray();
        }

        private static NativeGeneratedPairEvidence[] MatchKnown(CHR0Node clip, NativeGeneratedPairEvidence[] known, CancellationToken token)
        {
            var wanted = known.Where(site => site != null).GroupBy(site => (site.Bone, site.Channel))
                .ToDictionary(group => group.Key, group => group.GroupBy(site => site.TimeBits)
                    .Where(time => time.Count() == 1).ToDictionary(time => time.Key, time => time.Single()));
            var retained = new List<NativeGeneratedPairEvidence>();
            foreach (var entry in clip.Children.OfType<CHR0EntryNode>())
            for (int channel = 0; channel < 9; channel++)
            {
                token.ThrowIfCancellationRequested();
                if (!wanted.TryGetValue((entry.Name, channel), out var times)) continue;
                foreach (var pair in entry.KeyArrays[channel].Entries.GroupBy(key => key._index))
                {
                    token.ThrowIfCancellationRequested();
                    var rows = pair.Take(3).ToArray();
                    if (rows.Length == 2 && times.TryGetValue(Bits(rows[0]._index), out var evidence) && evidence.SameContext(entry) && evidence.Matches(rows[0], rows[1])) retained.Add(evidence);
                }
            }
            return retained.ToArray();
        }
        private static bool Bounded(CHR0Node clip) => clip != null && clip.Children.Count <= 4096 &&
            clip.Children.OfType<CHR0EntryNode>().Select(e => e.Name).Distinct(StringComparer.Ordinal).Count() == clip.Children.Count &&
            clip.Children.OfType<CHR0EntryNode>().Sum(entry => entry.KeyArrays.Sum(array => (long)array.KeyCount)) <= MaximumSites;
        private static bool ValidRigKey(string key) => key != null && key.Length == 64 && key.All(Uri.IsHexDigit);
        private static string Identity(CHR0Node clip, string rigKey) => rigKey.ToUpperInvariant() + ":" + NativeAnimationOptimization.SourceContentKey(clip);
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
    }
}
