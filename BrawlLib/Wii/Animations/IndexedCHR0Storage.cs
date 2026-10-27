using BrawlLib.Internal;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace BrawlLib.Wii.Animations
{
    // An immutable native indexed curve. Its encoded payload is reused only when
    // every decoded knot still matches the current authoring data exactly.
    // Editing a coefficient therefore falls back to I12, never re-quantizes it.
    public sealed class IndexedCHR0CurveStorage
    {
        private readonly byte[] data;
        public AnimDataFormat Format { get; }
        public int KeyCount { get; }
        public int ByteCount => data.Length;
        public string ContentKey { get; }
        public float Base => ReadFloat(data, 12);
        public float Step => ReadFloat(data, 8);
        public float FrameScale => ReadFloat(data, 4);
        public int[] Frames => Enumerable.Range(0, KeyCount).Select(Frame).ToArray();
        public float[] Values => Enumerable.Range(0, KeyCount).Select(Value).ToArray();
        public float[] Tangents => Enumerable.Range(0, KeyCount).Select(Tangent).ToArray();

        private IndexedCHR0CurveStorage(AnimDataFormat format, byte[] bytes)
        {
            Format = format; data = bytes; KeyCount = Read16(data, 0);
            using (var hash = SHA256.Create()) ContentKey = ((int)format).ToString() + ":" + BitConverter.ToString(hash.ComputeHash(data)).Replace("-", "");
        }

        public static IndexedCHR0CurveStorage Create(AnimDataFormat format, int[] frames,
            int[] values, int[] tangents, float valueBase, float valueStep)
        {
            if (format != AnimDataFormat.I4 && format != AnimDataFormat.I6 || frames == null || values == null || tangents == null ||
                frames.Length < 2 || frames.Length > ushort.MaxValue || values.Length != frames.Length || tangents.Length != frames.Length ||
                !Finite(valueBase) || !Finite(valueStep)) throw new ArgumentException("Invalid indexed CHR0 curve.");
            int stride = format == AnimDataFormat.I4 ? 4 : 6;
            var bytes = new byte[(16 + frames.Length * stride + 3) & ~3];
            Write16(bytes, 0, frames.Length);
            int span = frames[frames.Length - 1] - frames[0];
            WriteFloat(bytes, 4, span == 0 ? 0 : .99999994f / span);
            WriteFloat(bytes, 8, valueStep); WriteFloat(bytes, 12, valueBase);
            int previous = -1, duplicates = 0;
            for (int i = 0; i < frames.Length; i++)
            {
                int frameLimit = format == AnimDataFormat.I4 ? 255 : 2047;
                int valueLimit = format == AnimDataFormat.I4 ? 4095 : 65535;
                int tangentMin = format == AnimDataFormat.I4 ? -2048 : -32768;
                int tangentMax = format == AnimDataFormat.I4 ? 2047 : 32767;
                if (frames[i] < 0 || frames[i] > frameLimit || frames[i] < previous || values[i] < 0 || values[i] > valueLimit || tangents[i] < tangentMin || tangents[i] > tangentMax)
                    throw new ArgumentException("An indexed CHR0 knot exceeds its native representation.");
                duplicates = previous == frames[i] ? duplicates + 1 : 1;
                if (duplicates > 2) throw new ArgumentException("A native CHR0 curve has at most two knot sides.");
                int at = 16 + i * stride;
                if (format == AnimDataFormat.I4) Write32(bytes, at, (uint)((frames[i] << 24) | (values[i] << 12) | (tangents[i] & 4095)));
                else { Write16(bytes, at, frames[i] << 5); Write16(bytes, at + 2, values[i]); Write16(bytes, at + 4, tangents[i] & 65535); }
                previous = frames[i];
            }
            var result = new IndexedCHR0CurveStorage(format, bytes);
            for (int i = 0; i < result.KeyCount; i++) if (!Finite(result.Value(i))) throw new ArgumentException("Indexed CHR0 decoding produced a nonfinite value.");
            return result;
        }

        internal static IndexedCHR0CurveStorage FromEncoded(AnimDataFormat format, byte[] bytes)
        {
            if (format != AnimDataFormat.I4 && format != AnimDataFormat.I6 || bytes == null || bytes.Length < 16) return null;
            int count = Read16(bytes, 0), stride = format == AnimDataFormat.I4 ? 4 : 6;
            if (count < 2 || bytes.Length != ((16 + count * stride + 3) & ~3) || !Finite(ReadFloat(bytes, 4)) || !Finite(ReadFloat(bytes, 8)) || !Finite(ReadFloat(bytes, 12))) return null;
            var result = new IndexedCHR0CurveStorage(format, (byte[])bytes.Clone());
            int previous = -1, duplicates = 0;
            for (int i = 0; i < count; i++)
            {
                if (format == AnimDataFormat.I6 && (Read16(bytes, 16 + i * 6) & 31) != 0) return null;
                int frame = result.Frame(i);
                duplicates = frame == previous ? duplicates + 1 : 1;
                if (frame < previous || duplicates > 2 || !Finite(result.Value(i))) return null;
                previous = frame;
            }
            return result;
        }

        public bool Matches(KeyframeArray array)
        {
            if (array == null || array.KeyCount != KeyCount) return false;
            int index = 0;
            foreach (var key in array.Entries)
            {
                if (key._index != Frame(index) || Bits(key._value) != Bits(Value(index)) || Bits(key._tangent) != Bits(Tangent(index))) return false;
                index++;
            }
            return true;
        }

        internal unsafe void Write(VoidPtr destination)
        {
            byte* output = (byte*)destination;
            for (int i = 0; i < data.Length; i++) output[i] = data[i];
        }
        private int Frame(int index) => Format == AnimDataFormat.I4 ? data[16 + index * 4] : Read16(data, 16 + index * 6) >> 5;
        private int QuantizedValue(int index) => Format == AnimDataFormat.I4 ? (int)(Read32(data, 16 + index * 4) >> 12) & 4095 : Read16(data, 18 + index * 6);
        private float Value(int index) => Base + QuantizedValue(index) * Step;
        private float Tangent(int index) => Format == AnimDataFormat.I4 ? ((int)(Read32(data, 16 + index * 4) << 20) >> 20) / 32f : unchecked((short)Read16(data, 20 + index * 6)) / 256f;
        internal static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static int Read16(byte[] bytes, int at) => bytes[at] << 8 | bytes[at + 1];
        private static uint Read32(byte[] bytes, int at) => (uint)bytes[at] << 24 | (uint)bytes[at + 1] << 16 | (uint)bytes[at + 2] << 8 | bytes[at + 3];
        private static float ReadFloat(byte[] bytes, int at) => BitConverter.ToSingle(BitConverter.GetBytes(Read32(bytes, at)), 0);
        private static void Write16(byte[] bytes, int at, int value) { bytes[at] = (byte)(value >> 8); bytes[at + 1] = (byte)value; }
        private static void Write32(byte[] bytes, int at, uint value) { bytes[at] = (byte)(value >> 24); bytes[at + 1] = (byte)(value >> 16); bytes[at + 2] = (byte)(value >> 8); bytes[at + 3] = (byte)value; }
        private static void WriteFloat(byte[] bytes, int at, float value) => Write32(bytes, at, unchecked((uint)Bits(value)));
    }

    public sealed class IndexedCHR0EntryStorage
    {
        private readonly IndexedCHR0CurveStorage[] curves;
        public IndexedCHR0EntryStorage(IndexedCHR0CurveStorage[] channels)
        {
            if (channels == null || channels.Length != 9) throw new ArgumentException("Indexed CHR0 storage requires nine channel slots.");
            curves = (IndexedCHR0CurveStorage[])channels.Clone();
            for (int group = 0; group < 3; group++)
            {
                var formats = curves.Skip(group * 3).Take(3).Where(c => c != null).Select(c => c.Format).Distinct().ToArray();
                if (formats.Length > 1) throw new ArgumentException("Native indexed formats are shared by all varying axes of an SRT group.");
            }
        }
        public IndexedCHR0CurveStorage this[int channel] => curves[channel];
        public bool HasIndexedCurves => curves.Any(c => c != null);
        public int CurveCount => curves.Count(c => c != null);
        public string ContentKey => string.Join("|", curves.Select(c => c?.ContentKey ?? "I12"));
        public AnimDataFormat Format(int group) => curves.Skip(group * 3).Take(3).FirstOrDefault(c => c != null)?.Format ?? AnimDataFormat.I12;
        public bool MatchesGroup(KeyframeCollection keys, int group)
        {
            bool any = false;
            for (int axis = 0; axis < 3; axis++)
            {
                int channel = group * 3 + axis;
                if (keys[channel].KeyCount <= 1) continue;
                if (curves[channel] == null || !curves[channel].Matches(keys[channel])) return false;
                any = true;
            }
            return any;
        }

        public static unsafe IndexedCHR0EntryStorage Capture(CHR0EntryNode entry)
        {
            if (entry == null) return null;
            var data = entry.WorkingUncompressed;
            if (data.Address == null) return null;
            byte* start = (byte*)data.Address;
            var spans = new List<Tuple<ulong, long>>(); var seen = new HashSet<ResourceNode>();
            void Add(DataSource source)
            {
                if (source.Map != null && source.Map.Address != null && source.Map.Length > 0) spans.Add(Tuple.Create(new UIntPtr((byte*)source.Map.Address).ToUInt64(), (long)source.Map.Length));
                else if (source.Address != null && source.Length > 0) spans.Add(Tuple.Create(new UIntPtr((byte*)source.Address).ToUInt64(), (long)source.Length));
            }
            for (ResourceNode owner = entry; owner != null && seen.Add(owner); owner = owner.Parent) { Add(owner.UncompressedSource); Add(owner.WorkingUncompressed); }
            bool Contains(byte* pointer, long bytes) { ulong address = new UIntPtr(pointer).ToUInt64(); return bytes >= 0 && spans.Any(s => address >= s.Item1 && address - s.Item1 <= (ulong)s.Item2 && (ulong)bytes <= (ulong)s.Item2 - (address - s.Item1)); }
            if (!Contains(start, 8)) return null;
            uint Raw32(byte* p) => (uint)p[0] << 24 | (uint)p[1] << 16 | (uint)p[2] << 8 | p[3];
            AnimationCode code = Raw32(start + 4); int column = 0;
            var curves = new IndexedCHR0CurveStorage[9];
            for (int group = 0; group < 3; group++)
            {
                if (!code.GetExists(group)) continue;
                int axes = group == 0 && code.GetIsIsotropic(group) ? 1 : 3;
                for (int axis = 0; axis < axes; axis++, column++)
                {
                    int channel = group * 3 + axis; byte* cell = start + 8 + column * 4;
                    if (!Contains(cell, 4)) return null;
                    if (code.GetIsFixed(channel)) continue;
                    AnimDataFormat format = code.GetFormat(group);
                    if (format != AnimDataFormat.I4 && format != AnimDataFormat.I6) continue;
                    byte* encoded = start + unchecked((int)Raw32(cell));
                    if (!Contains(encoded, 16)) return null;
                    int count = encoded[0] << 8 | encoded[1], stride = format == AnimDataFormat.I4 ? 4 : 6;
                    int length = (16 + count * stride + 3) & ~3;
                    if (!Contains(encoded, length)) return null;
                    var bytes = new byte[length]; for (int i = 0; i < length; i++) bytes[i] = encoded[i];
                    var curve = IndexedCHR0CurveStorage.FromEncoded(format, bytes);
                    if (curve == null || !curve.Matches(entry.KeyArrays[channel])) continue;
                    if (axes == 1)
                    { for (int component = 0; component < 3; component++) if (curve.Matches(entry.KeyArrays[component])) curves[component] = curve; }
                    else curves[channel] = curve;
                }
            }
            var result = new IndexedCHR0EntryStorage(curves); return result.HasIndexedCurves ? result : null;
        }
    }
}
