using BrawlLib;
using BrawlLib.Internal;
using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;

internal static partial class Program
{
    // Literal wire expectations are independent of the native encoder. I6
    // frame is signed S10.5 (NW4R Frm48); I12 frame is an IEEE single.
    static unsafe void NativeFractionalTimeChecks(string stage)
    {
        Directory.CreateDirectory(stage);
        byte[] literal = { 0x00, 0x21, 0x12, 0x34, 0xff, 0x80 };
        fixed (byte* data = literal)
        {
            object entry = Marshal.PtrToStructure((IntPtr) data, FractionalI6Type);
            Check(FractionalI6Property(entry, "FrameIndex") == 1.03125f && FractionalI6Property(entry, "Tangent") == -.5f,
                "Fractional native: literal I6 low frame bits retain 1/32 timing and signed tangent");
        }
        var maximum = FractionalI6(1023.96875f, 7, -.5f);
        using (var encoded = new UnsafeBuffer(6))
        {
            Marshal.StructureToPtr(maximum, encoded.Address, false);
            byte* at = (byte*) encoded.Address;
            Check(at[0] == 0x7f && at[1] == 0xff && FractionalI6Property(maximum, "FrameIndex") == 1023.96875f,
                "Fractional native: positive signed I6 maximum is 1023+31/32");
        }
        foreach (float rejected in new[] { 1024f, 2047f, 2048f, .01f, float.NaN, float.PositiveInfinity })
            Check(FractionalRejects(() => { var ignored = FractionalI6(rejected, 0, 0); }),
                "Fractional native: I6 rejects unrepresentable time instead of unsigned wrap / " + rejected);
        literal[0] = 0x80; literal[1] = 0x00;
        fixed (byte* data = literal)
            Check(FractionalI6Property(Marshal.PtrToStructure((IntPtr) data, FractionalI6Type), "FrameIndex") == -1024f,
                "Fractional native: the high I6 bit decodes as sign, never positive frame 1024");
        literal[0] = 0xff; literal[1] = 0xe0;
        fixed (byte* data = literal)
            Check(FractionalI6Property(Marshal.PtrToStructure((IntPtr) data, FractionalI6Type), "FrameIndex") == -1f,
                "Fractional native: former unsigned frame 2047 is correctly decoded as minus one");

        var array = new KeyframeArray(5);
        array.SetFrameValue(0, 2, true)._tangent = 1;
        array.SetFrameValue(.5f, 4, true)._tangent = -2;
        array.SetFrameValue(.5f, 8, true)._tangent = 3;
        array.SetFrameValue(2.25f, 6, true)._tangent = -1;
        Check(array.GetKeyframe(.5f)._value == 4 && array.GetFrameValue(.5f) == 4 && array.GetFrameValue(.5f, true) == 8,
            "Fractional native: paired keys retain exact same-time incoming and outgoing sides");
        Check(Math.Abs(array.GetFrameValue(.25f) - FractionalHermite(2, 1, 4, -2, .5, .25)) < .000001 &&
            Math.Abs(array.GetFrameValue(1.375f) - FractionalHermite(8, 3, 6, -1, 1.75, .875)) < .000001,
            "Fractional native: scalar Hermite evaluation uses actual fractional interval lengths");
        array.Insert(1);
        Check(array.GetKeyframe(.5f) != null && array.GetKeyframe(3.25f) != null && array.KeyCount == 4,
            "Fractional native: integral insertion preserves fractional offsets and paired sides");
        array.Delete(1);
        Check(array.GetKeyframe(2.25f) != null && array.KeyCount == 4,
            "Fractional native: integral deletion shifts subsequent fractional knots without truncation");
        array.Delete(0);
        Check(array.KeyCount == 1 && array.GetKeyframe(1.25f) != null,
            "Fractional native: deleting a frame removes its entire half-open time cell");
        Check(array.Remove(1.25f) != null && array.KeyCount == 0 && array.GetKeyframe(-1) == null && array.Remove(-1) == null,
            "Fractional native: exact-time removal never exposes or removes the default sentinel");
        Check(FractionalRejects(() => array.SetFrameValue(-.03125f, 3)) &&
            FractionalRejects(() => array.SetFrameValue(float.NaN, 3)),
            "Fractional native: active authored knots reject negative and nonfinite times");

        var loop = new KeyframeArray(5) { Loop = true };
        loop.SetFrameValue(.5f, 2, true)._tangent = 1;
        loop.SetFrameValue(3.5f, 6, true)._tangent = -2;
        Check(Math.Abs(loop.GetFrameValue(4.25f) - FractionalHermite(6, -2, 2, 1, 2, .75)) < .000001 &&
            Math.Abs(loop.GetFrameValue(.25f) - FractionalHermite(6, -2, 2, 1, 2, 1.75)) < .000001,
            "Fractional native: sparse loop seam retains the native wrap context and real knot spans");

        var indexed = IndexedCHR0CurveStorage.Create(AnimDataFormat.I6,
            new[] { 0f, .03125f, 1.03125f, 1023.96875f }, new[] { 0, 1, 4, 8 }, new[] { 0, 128, -128, 0 }, 2, .25f);
        Check(indexed.Times.SequenceEqual(new[] { 0f, .03125f, 1.03125f, 1023.96875f }) && indexed.Values[2] == 3 && indexed.Tangents[2] == -.5f,
            "Fractional native: immutable I6 storage retains all exact native times and coefficients");
        Check(FractionalRejects(() => { var ignored = indexed.Frames; }) &&
            FractionalRejects(() => IndexedCHR0CurveStorage.Create(AnimDataFormat.I4, new[] { 0f, .5f }, new[] { 0, 1 }, new[] { 0, 0 }, 0, 1)),
            "Fractional native: integral legacy storage access and I4 reject fractional times explicitly");
        var integerStorage = IndexedCHR0CurveStorage.Create(AnimDataFormat.I6, new[] { 0, 17, 1023 }, new[] { 0, 1, 2 }, new[] { 0, 1, -1 }, 0, 1);
        Check(integerStorage.Frames.SequenceEqual(new[] { 0, 17, 1023 }),
            "Fractional native: existing integer storage API retains exact integer behavior");

        FractionalLiteralDecode(AnimDataFormat.I6, new[] { 0f, 1.03125f, 2.5f }, new[] { 2f, 3f, 5f }, new[] { 0f, -.5f, .25f });
        FractionalLiteralDecode(AnimDataFormat.I12, new[] { 0f, 1.125f, 1.125f, 2.5f }, new[] { 2f, 3f, 8f, 5f }, new[] { 0f, -.5f, .75f, .25f });
        // Reversing this ordinary finite time through an x86 float temporary
        // produces a signaling NaN bit pattern. Native time bytes must survive.
        FractionalLiteralDecode(AnimDataFormat.I12, new[] { 0f, BitConverter.ToSingle(BitConverter.GetBytes(0x3f80817f), 0), 2.5f },
            new[] { 2f, 3f, 5f }, new[] { 0f, -.5f, .25f });
        foreach (int version in new[] { 4, 5 })
        {
            FractionalRoundtrip(version, stage);
            FractionalIntegerRoundtrip(version, stage);
        }
    }

    static bool FractionalRejects(Action operation)
    {
        try { operation(); return false; }
        catch (ArgumentException) { return true; }
        catch (NotSupportedException) { return true; }
        catch (TargetInvocationException error) when (error.InnerException is ArgumentException || error.InnerException is NotSupportedException) { return true; }
    }

    static Type FractionalI6Type => typeof(KeyframeArray).Assembly.GetType("BrawlLib.Wii.Animations.I6Entry", true);
    static object FractionalI6(float time, int value, float tangent) => Activator.CreateInstance(FractionalI6Type, new object[] { time, value, tangent });
    static float FractionalI6Property(object entry, string name) => (float) FractionalI6Type.GetProperty(name).GetValue(entry);

    static double FractionalHermite(double first, double firstTangent, double last, double lastTangent, double span, double offset)
    {
        double t = offset / span, t2 = t * t, t3 = t2 * t;
        return (2 * t3 - 3 * t2 + 1) * first + (t3 - 2 * t2 + t) * span * firstTangent +
            (-2 * t3 + 3 * t2) * last + (t3 - t2) * span * lastTangent;
    }

    static unsafe void FractionalLiteralDecode(AnimDataFormat format, float[] times, float[] values, float[] tangents)
    {
        int stride = format == AnimDataFormat.I6 ? 6 : 12;
        int header = format == AnimDataFormat.I6 ? 16 : 8;
        var bytes = new byte[32 + header + stride * times.Length];
        var code = AnimationCode.Default;
        code.HasRotation = true; code.IsRotationIsotropic = false;
        code.IsRotationXFixed = false; code.IsRotationYFixed = true; code.IsRotationZFixed = true;
        code.RotationDataFormat = format;
        FractionalWrite32(bytes, 4, (uint) code);
        FractionalWrite32(bytes, 8, 32); // Relative RX curve pointer; RY/RZ fixed zero.
        FractionalWrite16(bytes, 32, times.Length);
        FractionalWriteFloat(bytes, 36, 1f / (times[times.Length - 1] - times[0]));
        if (format == AnimDataFormat.I6) { FractionalWriteFloat(bytes, 40, .25f); FractionalWriteFloat(bytes, 44, 2); }
        for (int i = 0; i < times.Length; i++)
        {
            int at = 32 + header + i * stride;
            if (format == AnimDataFormat.I6)
            {
                FractionalWrite16(bytes, at, (int) (times[i] * 32));
                FractionalWrite16(bytes, at + 2, (int) ((values[i] - 2) / .25f));
                FractionalWrite16(bytes, at + 4, (int) (tangents[i] * 256));
            }
            else
            {
                FractionalWriteFloat(bytes, at, times[i]); FractionalWriteFloat(bytes, at + 4, values[i]); FractionalWriteFloat(bytes, at + 8, tangents[i]);
            }
        }
        using (var buffer = new UnsafeBuffer(bytes.Length))
        using (var parent = new CHR0Node { Name = "literal fractional " + format, FrameCount = 4 })
        {
            Marshal.Copy(bytes, 0, buffer.Address, bytes.Length);
            var entry = new CHR0EntryNode { Name = "literal bone" };
            entry.Initialize(parent, buffer.Address, bytes.Length);
            var actual = entry.KeyArrays[3].Entries.ToArray();
            Check(actual.Length == times.Length && actual.Select(k => k._index).SequenceEqual(times) &&
                actual.Select(k => k._value).SequenceEqual(values) && actual.Select(k => k._tangent).SequenceEqual(tangents),
                "Fractional native: independently constructed complete " + format + " entry decodes exact times, values, tangents and paired sides");
        }
    }

    static void FractionalRoundtrip(int version, string stage)
    {
        using (var original = new CHR0Node { Name = "fractional fixture", Version = version, FrameCount = 5, Loop = true, OriginalPath = "independent metadata" })
        {
            var entry = original.CreateEntry("body bone");
            entry.UseModelTranslate = true; entry.ScaleCompensateParent = true; entry.ClassicScaleOff = true;
            float[] times = { 0, 1f / 7, 1.125f, 1.125f, 3.75f, 5 };
            float[] values = { 0, -1, 720, 721, 10, 0 };
            float[] tangents = { 2, -.125f, 1, -1, .25f, 2 };
            for (int i = 0; i < times.Length; i++) entry.Keyframes.SetFrameValue(3, times[i], values[i], true)._tangent = tangents[i];
            entry.Keyframes.SetFrameValue(4, 0, 0, true);
            entry.Keyframes.SetFrameValue(5, 0, 0, true);
            entry.Keyframes.PreserveInterpolation = true;
            var sibling = original.CreateEntry("untouched sibling");
            sibling.Keyframes.SetFrameValue(6, 0, BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0), true);
            sibling.Keyframes.SetFrameValue(7, 0, 4, true); sibling.Keyframes.SetFrameValue(8, 0, 0, true);
            sibling.Keyframes.PreserveInterpolation = true;
            PreciseCHR0EntryNode.Promote(original);
            entry = (CHR0EntryNode) original.Children[0];
            string before = FractionalTuples(entry);
            string path = Path.Combine(stage, "fractional-v" + version + ".chr0");
            original.ExportChecked(path);
            int originalFrames = original.FrameCount;
            Check(FractionalRejects(() => original.Resize(8)) && FractionalRejects(() => original.MergeWith(original)) &&
                FractionalRejects(() => original.Append(original)) && FractionalRejects(() => original.Reverse(false)) &&
                FractionalRejects(() => original.Port(null, null)) && original.FrameCount == originalFrames && FractionalTuples(entry) == before,
                "Fractional native: optional integer-only Resize/Merge/Append/Reverse/Port reject before mutation / v" + version);
            using (var reopened = (CHR0Node) NodeFactory.FromFile(null, path))
            {
                var result = (CHR0EntryNode) reopened.Children[0];
                Check(reopened.Version == version && reopened.FrameCount == 5 && reopened.Loop && reopened.Name == original.Name &&
                    reopened.OriginalPath == original.OriginalPath && result.Name == entry.Name && result.UseModelTranslate == entry.UseModelTranslate &&
                    result.ScaleCompensateParent == entry.ScaleCompensateParent && result.ClassicScaleOff == entry.ClassicScaleOff &&
                    FractionalTuples(result) == before,
                    "Fractional native: precise CHR0 v" + version + " save/reopen preserves complete times, sides, turns, tangents and metadata");
                double error = 0;
                for (int sample = 0; sample <= 1024; sample++)
                {
                    float time = 5f * sample / 1024;
                    error = Math.Max(error, Math.Abs((double) result.GetFrameValue(3, time) - entry.GetFrameValue(3, time)));
                }
                var reopenedSibling = (CHR0EntryNode) reopened.Children[1];
                Check(error == 0 && FractionalTuples(entry) == before &&
                    BitConverter.ToInt32(BitConverter.GetBytes(reopenedSibling.GetKeyframe(6, 0)._value), 0) == int.MinValue,
                    "Fractional native: reopened fractional sampling is exact, original content unchanged, and sibling signed zero preserved / v" + version);
            }
        }
    }

    static void FractionalIntegerRoundtrip(int version, string stage)
    {
        using (var original = new CHR0Node { Name = "integer fixture", Version = version, FrameCount = 5, Loop = true, OriginalPath = "unchanged integer metadata" })
        {
            var entry = original.CreateEntry("all native channels");
            entry.ScaleCompensateApply = true; entry.ScaleCompensateParent = true; entry.ClassicScaleOff = true;
            for (int channel = 0; channel < 9; channel++)
            {
                foreach (int time in new[] { 0, 1, 3, 5 })
                {
                    float value = channel < 3 ? 1 + channel * .25f + time * .125f : channel < 6 ? time * 180 : time * .125f;
                    entry.SetKeyframe(channel, time, value, true, true)._tangent = channel < 3 ? .125f : channel < 6 ? 180 : .125f;
                }
            }
            entry.SetKeyframe(4, 3, 541, true, true)._tangent = -90;
            entry.Keyframes.PreserveInterpolation = true; PreciseCHR0EntryNode.Promote(original);
            entry = (CHR0EntryNode) original.Children[0];
            string before = FractionalTuples(entry);
            string firstPath = Path.Combine(stage, "integer-v" + version + "-first.chr0");
            string secondPath = Path.Combine(stage, "integer-v" + version + "-reopened.chr0");
            original.ExportChecked(firstPath);
            using (var reopened = (CHR0Node) NodeFactory.FromFile(null, firstPath))
            {
                var result = (CHR0EntryNode) reopened.Children[0];
                Check(FractionalTuples(result) == before && result.GetKeyframe(4, 3).Second != null &&
                    result.GetFrameValue(3, 2.5f) == 450 && result.GetFrameValue(6, 2.5f) == .3125f,
                    "Fractional native: unchanged integral APIs preserve every channel, paired sides and analytical full-turn samples / v" + version);
                PreciseCHR0EntryNode.Promote(reopened);
                reopened.ExportChecked(secondPath);
                Check(File.ReadAllBytes(firstPath).SequenceEqual(File.ReadAllBytes(secondPath)) && FractionalTuples(entry) == before,
                    "Fractional native: complete unchanged integral precise CHR0 remains byte-identical through reopen and rewrite / v" + version);
            }
        }
    }

    static string FractionalTuples(CHR0EntryNode entry) => string.Join("|", entry.KeyArrays.Select(array =>
        string.Join(";", array.Entries.Select(k => string.Join(",", new[] { k._index, k._value, k._tangent }
            .Select(value => BitConverter.ToInt32(BitConverter.GetBytes(value), 0).ToString("X8")))))));
    static void FractionalWrite16(byte[] bytes, int at, int value) { bytes[at] = (byte) (value >> 8); bytes[at + 1] = (byte) value; }
    static void FractionalWrite32(byte[] bytes, int at, uint value) { bytes[at] = (byte) (value >> 24); bytes[at + 1] = (byte) (value >> 16); bytes[at + 2] = (byte) (value >> 8); bytes[at + 3] = (byte) value; }
    static void FractionalWriteFloat(byte[] bytes, int at, float value) => FractionalWrite32(bytes, at, unchecked((uint) BitConverter.ToInt32(BitConverter.GetBytes(value), 0)));
}
