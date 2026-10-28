using BrawlLib.Wii.Animations;
using System.Linq;

namespace BrawlCrate.Studio
{
    internal static partial class NativeAnimationOptimization
    {
        // Independent native proof: a fixed channel preserves every join's
        // authored value and +zero slope only for the complete raw constant
        // function. Identical constant sides are redundant; negative zero,
        // differing values and nonzero tangents remain ineligible.
        static bool ExactFixedChannel(KeyframeEntry[] original, ControlKey[] candidate)
        {
            if (original.Length == 0 || candidate.Length != 1 || candidate[0].Frame != 0 ||
                !Finite(original[0]._value) || Bits(original[0]._value) == int.MinValue ||
                Bits(candidate[0].Value) != Bits(original[0]._value) || Bits(candidate[0].Tangent) != 0)
                return false;
            return Bits(candidate[0].Frame) == 0 && original.All(k => Bits(k._index) != int.MinValue && Bits(k._value) == Bits(original[0]._value) && Bits(k._tangent) == 0);
        }
    }
}
