using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;

namespace BrawlCrate.Studio
{
    // Created only for an operation-owned, frozen validation model and clip.
    // This is not a playback cache or a source of document state. Float storage
    // retains the exact native evaluator outputs; callers receive fresh doubles
    // because the quality evaluator overwrites its arrays with differences.
    internal sealed class NativeOptimizationReferenceSamples
    {
        internal const long MaximumBytes = 128L * 1024 * 1024;
        private readonly MDL0Node model;
        private readonly string content;
        private readonly Dictionary<int, float[]> samples = new Dictionary<int, float[]>();
        internal long Bytes { get; private set; }
        internal long Hits { get; private set; }

        internal NativeOptimizationReferenceSamples(MDL0Node model, CHR0Node baseline)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            content = NativeAnimationOptimization.SourceContentKey(baseline);
        }

        internal void Validate(MDL0Node frozenModel, CHR0Node baseline)
        {
            if (!ReferenceEquals(model, frozenModel) || content != NativeAnimationOptimization.SourceContentKey(baseline))
                throw new InvalidDataException("Frozen native reference samples belong to a different model or animation. No candidate was validated.");
        }

        private static int Key(double time) => BitConverter.ToInt32(BitConverter.GetBytes((float)time + 1), 0);

        internal bool TryRead(double time, double[][] destination)
        {
            if (!samples.TryGetValue(Key(time), out var value)) return false;
            int size = 0; foreach (var array in destination) size = checked(size + array.Length);
            if (size != value.Length) throw new InvalidDataException("Frozen native reference geometry changed.");
            int index = 0;
            foreach (var array in destination) for (int i = 0; i < array.Length; i++) array[i] = value[index++];
            Hits++; return true;
        }

        internal void Retain(double time, double[][] source)
        {
            int key = Key(time); if (samples.ContainsKey(key)) return;
            int size = 0; foreach (var array in source) size = checked(size + array.Length);
            // Include a conservative per-entry dictionary/array allowance.
            long bytes = checked((long)size * sizeof(float) + 128);
            if (bytes > MaximumBytes - Bytes || samples.Count >= 100000) return;
            var value = new float[size]; int index = 0;
            foreach (var array in source) foreach (double item in array)
            {
                float native = (float)item;
                if ((double)native != item) throw new InvalidDataException("Reference sample is not an exact native float output.");
                value[index++] = native;
            }
            samples.Add(key, value); Bytes += bytes;
        }
    }
}
