using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class Program
{
    static void NativeRootPairOracleChecks()
    {
        using (var original = new CHR0Node { Name = "independent root pair oracle", FrameCount = 5 })
        {
            var entry = original.CreateEntry("reviewed motion control");
            for (int c = 0; c < 9; c++) entry.Keyframes.SetFrameValue(c, 0, c < 3 ? 1 : 0, true)._tangent = 0;
            entry.KeyArrays[3].Remove(0); entry.KeyArrays[7].Remove(0);
            entry.Keyframes.SetFrameValue(3, 0, 0, true)._tangent = 0;
            entry.Keyframes.SetFrameValue(3, 2, 1, true)._tangent = .25f;
            entry.Keyframes.SetFrameValue(3, 2, 1, true)._tangent = .5f;
            entry.Keyframes.SetFrameValue(3, 4, 2, true)._tangent = 0;
            foreach (float time in new[] { 0f, 1, 2, 3, 4 }) entry.Keyframes.SetFrameValue(7, time, time == 0 || time == 4 ? 0 : 2, true)._tangent = 0;
            PreciseCHR0EntryNode.Promote(original); entry = (CHR0EntryNode)original.Children[0];
            using (var candidate = NativeAnimationOptimization.CopySource(original))
            {
                var after = (CHR0EntryNode)candidate.Children[0]; after.KeyArrays[7].Remove(2);
                Check(ReplayPairedChannelMatches(entry, after, 3, 2),
                    "Root pair oracle: exact ordered rotation sides survive an unrelated redundant translation-plateau singleton removal");
                Check(!ReplayKnotMatches(original, candidate, 2, new HashSet<string>(new[] { entry.Name })),
                    "Root pair oracle: the separate explicit mandatory-join oracle still rejects removing a channel's required singleton");
                Check(Enumerable.Range(0, 129).All(i => ReplayFloatBits(entry.GetFrameValue(7, 1 + i / 64f)) ==
                    ReplayFloatBits(after.GetFrameValue(7, 1 + i / 64f))),
                    "Root pair oracle: independently known constant plateau preserves native values at every withheld fractional witness");
                var first = after.KeyArrays[3].Entries.First(k => k._index == 2); float tangent = first._tangent;
                first._tangent = .251f;
                Check(!ReplayPairedChannelMatches(entry, after, 3, 2), "Root pair oracle: one changed incoming side remains a real failure");
                first._tangent = tangent;
                after.KeyArrays[3].Remove(2);
                Check(!ReplayPairedChannelMatches(entry, after, 3, 2), "Root pair oracle: one removed distinct native side remains a real failure");
            }
            using (var constant = NativeAnimationOptimization.CopySource(original))
            using (var collapsed = NativeAnimationOptimization.CopySource(original))
            {
                var before = (CHR0EntryNode)constant.Children[0]; var after = (CHR0EntryNode)collapsed.Children[0];
                foreach (var key in before.KeyArrays[3].Entries.ToArray()) before.KeyArrays[3].Remove(key._index);
                foreach (var key in after.KeyArrays[3].Entries.ToArray()) after.KeyArrays[3].Remove(key._index);
                before.Keyframes.SetFrameValue(3, 0, 1, true)._tangent = 0;
                before.Keyframes.SetFrameValue(3, 2, 1, true)._tangent = 0;
                before.Keyframes.SetFrameValue(3, 2, 1, true)._tangent = 0;
                before.Keyframes.SetFrameValue(3, 4, 1, true)._tangent = 0;
                after.Keyframes.SetFrameValue(3, 0, 1, true)._tangent = 0;
                Check(ReplayPairedChannelMatches(before, after, 3, 2),
                    "Root pair oracle: independent exact constant proof permits canonical collapse of identical paired sides");
                var signed = before.KeyArrays[3].Entries.First(k => k._index == 2);
                signed._tangent = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
                Check(!ReplayPairedChannelMatches(before, after, 3, 2),
                    "Root pair oracle: an authored negative-zero tangent cannot disappear through the exact-constant exception");
                signed._tangent = 0; signed._index = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);
                Check(!ReplayPairedChannelMatches(before, after, 3, 0),
                    "Root pair oracle: an authored negative-zero knot time cannot disappear through the exact-constant exception");
            }
        }
    }
}
