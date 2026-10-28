using System.Numerics;
using System.Text.Json.Serialization;

namespace FighterPort.Core;

// Native coefficients are authoritative: retain order, zeros and original bits.
// The old four-slot fields remain a read-compatible prefix, never a truncation
// policy for newer scenes.
public readonly record struct SkinInfluence(int Joint, float Weight);

public static class SkinInfluences
{
    public const int MaximumPerVertex = 65536;
    public static int Count(SkinInfluence[] all) => all.Length == 0 ? 4 : all.Length;
    public static float Joint(Vector4 prefix, SkinInfluence[] all, int slot) => all.Length == 0 ? prefix[slot] : all[slot].Joint;
    public static float Weight(Vector4 prefix, SkinInfluence[] all, int slot) => all.Length == 0 ? prefix[slot] : all[slot].Weight;
    public static bool Valid(Vector4 joints, Vector4 weights, SkinInfluence[] all, int bones)
    {
        if (all is null || all.Length > MaximumPerVertex) return false;
        for (int i = 0; i < Count(all); i++)
        {
            float joint = Joint(joints, all, i), weight = Weight(weights, all, i);
            if (!float.IsFinite(joint) || joint != MathF.Truncate(joint) || joint < 0 || joint >= bones || !float.IsFinite(weight) || weight < 0) return false;
            if (all.Length > 0 && i < 4 && (BitConverter.SingleToInt32Bits(joints[i]) != BitConverter.SingleToInt32Bits(joint) ||
                BitConverter.SingleToInt32Bits(weights[i]) != BitConverter.SingleToInt32Bits(weight))) return false;
        }
        return true;
    }
}
