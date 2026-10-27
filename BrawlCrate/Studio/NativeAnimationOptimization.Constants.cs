using BrawlLib.Wii.Animations;
using System.Linq;

namespace BrawlCrate.Studio
{
    internal static partial class NativeAnimationOptimization
    {
        // Independent native proof: a fixed channel preserves every join's
        // authored value and +zero slope only for the complete raw constant
        // function. Paired sides, negative zero and nonzero tangents remain
        // ineligible. Prepare independently reopens the exact candidate tuples.
        static bool ExactFixedChannel(KeyframeEntry[] original, ControlKey[] candidate)
        {
            if (original.Length == 0 || candidate.Length != 1 || candidate[0].Frame != 0 ||
                !Finite(original[0]._value) || Bits(original[0]._value) == int.MinValue ||
                Bits(candidate[0].Value) != Bits(original[0]._value) || Bits(candidate[0].Tangent) != 0)
                return false;
            return original.Select(k => k._index).Distinct().Count() == original.Length &&
                original.All(k => Bits(k._value) == Bits(original[0]._value) && Bits(k._tangent) == 0);
        }
    }
}
