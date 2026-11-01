using HSDRaw.Common.Animation;
using System;
using System.Collections.Generic;

namespace HSDRaw.Tools
{
    public static class AnimationKeyCompressorFast
    {
        private struct Segment
        {
            public int Start;
            public int End;
            public float MaxError;
            public int MaxIndex;
        }

        public static void CompressTrackFast(FOBJ_Player player)
        {
            int frameCount = player.FrameCount;

            // 1. Cache values
            float[] values = new float[frameCount + 1];
            for (int i = 0; i <= frameCount; i++)
                values[i] = player.GetValue(i);

            float epsilon = GetEpsilonForTrack(player.JointTrackType);

            // 2. Precompute tangents (central difference)
            float[] tangents = new float[frameCount + 1];
            for (int i = 0; i <= frameCount; i++)
            {
                float prev = i > 0 ? values[i - 1] : values[i];
                float next = i < frameCount ? values[i + 1] : values[i];
                tangents[i] = (next - prev) * 0.5f;
            }

            // 3. Initial segment
            var segments = new SimplePriorityQueue<Segment>();

            var first = BuildSegment(0, frameCount, values, tangents);
            segments.Enqueue(first, -first.MaxError);

            var kept = new SortedSet<int> { 0, frameCount };

            // 4. Split until error satisfied
            while (segments.Count > 0)
            {
                var seg = segments.Dequeue();

                if (seg.MaxError <= epsilon)
                    continue;

                int split = seg.MaxIndex;
                kept.Add(split);

                var left = BuildSegment(seg.Start, split, values, tangents);
                var right = BuildSegment(split, seg.End, values, tangents);

                if (left.End - left.Start > 1)
                    segments.Enqueue(left, -left.MaxError);

                if (right.End - right.Start > 1)
                    segments.Enqueue(right, -right.MaxError);
            }

            // 5. Build final keys (already sorted!)
            var newKeys = new List<FOBJKey>(kept.Count);
            foreach (int i in kept)
            {
                newKeys.Add(new FOBJKey
                {
                    Frame = i,
                    Value = values[i],
                    InterpolationType = GXInterpolationType.HSD_A_OP_SPL,
                    Tan = tangents[i]
                });
            }

            player.Keys = newKeys;
        }

        private static Segment BuildSegment(int start, int end, float[] values, float[] tangents)
        {
            float maxError = 0;
            int maxIndex = -1;

            for (int i = start + 1; i < end; i++)
            {
                float t = (float)(i - start) / (end - start);

                float approx = Hermite(
                    values[start],
                    values[end],
                    tangents[start] * (end - start),
                    tangents[end] * (end - start),
                    t
                );

                float err = Math.Abs(values[i] - approx);

                if (err > maxError)
                {
                    maxError = err;
                    maxIndex = i;
                }
            }

            return new Segment
            {
                Start = start,
                End = end,
                MaxError = maxError,
                MaxIndex = maxIndex
            };
        }

        private static float Hermite(float p0, float p1, float m0, float m1, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return (2 * t3 - 3 * t2 + 1) * p0 +
                   (t3 - 2 * t2 + t) * m0 +
                   (-2 * t3 + 3 * t2) * p1 +
                   (t3 - t2) * m1;
        }

        private static float GetEpsilonForTrack(JointTrackType type)
        {
            switch (type)
            {
                case JointTrackType.HSD_A_J_TRAX:
                case JointTrackType.HSD_A_J_TRAY:
                case JointTrackType.HSD_A_J_TRAZ:
                    return 0.01f;

                case JointTrackType.HSD_A_J_ROTX:
                case JointTrackType.HSD_A_J_ROTY:
                case JointTrackType.HSD_A_J_ROTZ:
                    return 0.0087f;

                case JointTrackType.HSD_A_J_SCAX:
                case JointTrackType.HSD_A_J_SCAY:
                case JointTrackType.HSD_A_J_SCAZ:
                    return 0.01f;

                default:
                    return 0.001f;
            }
        }
    }
    public class SimplePriorityQueue<T>
    {
        private List<(T item, float priority)> heap = new List<(T item, float priority)>();

        public int Count => heap.Count;

        public void Enqueue(T item, float priority)
        {
            heap.Add((item, priority));
            HeapifyUp(heap.Count - 1);
        }

        public T Dequeue()
        {
            var root = heap[0].item;
            heap[0] = heap[heap.Count - 1];
            heap.RemoveAt(heap.Count - 1);
            HeapifyDown(0);
            return root;
        }

        private void HeapifyUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heap[i].priority >= heap[parent].priority)
                    break;

                (heap[i], heap[parent]) = (heap[parent], heap[i]);
                i = parent;
            }
        }

        private void HeapifyDown(int i)
        {
            int last = heap.Count - 1;

            while (true)
            {
                int left = i * 2 + 1;
                int right = i * 2 + 2;
                int smallest = i;

                if (left <= last && heap[left].priority < heap[smallest].priority)
                    smallest = left;

                if (right <= last && heap[right].priority < heap[smallest].priority)
                    smallest = right;

                if (smallest == i)
                    break;

                (heap[i], heap[smallest]) = (heap[smallest], heap[i]);
                i = smallest;
            }
        }
    }
}
