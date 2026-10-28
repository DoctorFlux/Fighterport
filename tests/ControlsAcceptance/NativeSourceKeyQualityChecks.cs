using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlLib.Internal;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Independent fixtures freeze the former sparse-axis contradiction and an
    // interior Hermite witness before inspecting the source-seeded algorithm.
    // These are operation-owned native models, not live documents or game edits.
    static void SourceKeyQualityChecks(MDL0Node model, string output)
    {
        string fingerprint = BrawlRetargeter.SkeletonFingerprint(model);
        var rig = BrawlRetargeter.Capture(model);
        SourceKeyQualityFixtures(model, rig, output, "native-order");

        var oldNames = rig.Bones.ToDictionary(b => b, b => b.Name);
        try
        {
            for (int i = 0; i < rig.Bones.Length; i++) rig.Bones[i].Name = "joint_" + ((i * 37 + 19) % 997).ToString("D3");
            var order = new List<int>();
            void Visit(int parent)
            {
                foreach (int i in Enumerable.Range(0, rig.Bones.Length).Where(i => rig.Rig.Joints[i].Parent == parent).Reverse())
                { order.Add(i); Visit(i); }
            }
            Visit(-1);
            var newIndex = order.Select((old, index) => new { old, index }).ToDictionary(p => p.old, p => p.index);
            var joints = order.Select(old => new RigJoint(rig.Bones[old].Name,
                rig.Rig.Joints[old].Parent < 0 ? -1 : newIndex[rig.Rig.Joints[old].Parent], rig.Rig.Joints[old].Bind)).ToArray();
            var roles = rig.Rig.Roles.ToDictionary(p => p.Key, p => newIndex[p.Value]);
            var unfamiliar = new BrawlRig { Bones = order.Select(i => rig.Bones[i]).ToArray(),
                Rig = new Rig("Independent anonymous rig", "reviewed structural fixture", joints, roles, rig.Rig.HeadRadius, rig.Rig.CoordinateUp, rig.Rig.CoordinateFrame),
                Fingerprint = BrawlRetargeter.SkeletonFingerprint(model) };
            Check(order.Count == rig.Bones.Length && order.Where((old, index) => old != index).Any() &&
                unfamiliar.Bones.All(b => b.Name.StartsWith("joint_", StringComparison.Ordinal)),
                "Quality fixture uses anonymous names and a different parent-before-child sibling storage order");
            SourceKeyQualityFixtures(model, unfamiliar, output, "anonymous-permuted");
        }
        finally { foreach (var pair in oldNames) pair.Key.Name = pair.Value; model.ApplyCHR(null, 0); }
        Check(BrawlRetargeter.SkeletonFingerprint(model) == fingerprint,
            "Source-key quality fixtures restore every authentic bone name, hierarchy and raw bind transform");
    }

    static void SourceKeyQualityFixtures(MDL0Node model, BrawlRig rig, string output, string label)
    {
        var plan = new HumanoidRetargeter(rig.Rig, rig.Rig);
        string body = rig.Bones[rig.Rig.Roles["pelvis"]].Name, head = rig.Bones[rig.Rig.Roles["head"]].Name;
        var controls = new HashSet<string>(plan.TargetMotionControls.Select(i => rig.Bones[i].Name), StringComparer.Ordinal);

        using (var dense = SourceKeyQualityDense(rig, 3))
        using (var source = SourceKeyQualitySource(rig, 3, new[] { body }))
        {
            var track = (CHR0EntryNode)dense.FindChild(body, false);
            track.KeyArrays[3].GetKeyframe(1)._tangent = 8;
            float bind = rig.Bones[rig.Rig.Roles["pelvis"]].BindState[3];
            Check(Math.Abs(track.GetFrameValue(3, .5f) - bind + 1) < .000001,
                label + ": independent Hermite pulse is -1 degree at half a frame despite equal authored knot values");
            SourceKeyQualityRun(model, rig, plan, dense, source, output, label + "-fractional-pulse", result =>
            {
                var repaired = (CHR0EntryNode)result.Animation.FindChild(body, false);
                Check(repaired != null && Math.Abs(repaired.GetFrameValue(3, .5f) - bind + 1) <= .01,
                    label + ": source-seeded repair preserves the hand-derived fractional pulse instead of treating equal values as constant");
            });
        }

        using (var dense = SourceKeyQualityDense(rig, 5))
        using (var source = SourceKeyQualitySource(rig, 5, new[] { body }))
        {
            var bodyTrack = (CHR0EntryNode)dense.FindChild(body, false);
            var headTrack = (CHR0EntryNode)dense.FindChild(head, false);
            for (int at = 0; at < 5; at++)
            {
                bodyTrack.KeyArrays[6].GetKeyframe(at)._value += at * 2;
                bodyTrack.KeyArrays[6].GetKeyframe(at)._tangent = 2;
                headTrack.KeyArrays[5].GetKeyframe(at)._value += at * 2;
                headTrack.KeyArrays[5].GetKeyframe(at)._tangent = 2;
            }
            SourceKeyQualityRun(model, rig, plan, dense, source, output, label + "-missing-derived-motion", result =>
            {
                var bodyResult = (CHR0EntryNode)result.Animation.FindChild(body, false);
                var headResult = (CHR0EntryNode)result.Animation.FindChild(head, false);
                Check(bodyResult != null && headResult != null &&
                    Math.Abs(bodyResult.GetFrameValue(6, 4) - bodyResult.GetFrameValue(6, 0) - 8) <= .0002 &&
                    Math.Abs(headResult.GetFrameValue(5, 4) - headResult.GetFrameValue(5, 0) - 8) <= .01,
                    label + ": required target translation and a target track without an authored source track are restored from frozen dense motion");
            });
        }

        using (var dense = SourceKeyQualityDense(rig, 3))
        using (var source = SourceKeyQualitySource(rig, 3, new[] { body }))
        {
            // Exactly representable 1/1024-degree pulse: this is below every
            // frozen local/native bound, so retaining source endpoints should
            // give a real, measured nonzero approximation rather than only
            // qualifying identity fallback or exact polynomial fixtures.
            ((CHR0EntryNode)dense.FindChild(body, false)).KeyArrays[3].GetKeyframe(1)._value += 1f / 1024;
            SourceKeyQualityRun(model, rig, plan, dense, source, output, label + "-bounded-small-pulse", result =>
            {
                var repaired = (CHR0EntryNode)result.Animation.FindChild(body, false);
                var measured = SourceKeyQualityMeasure(model, dense, result.Animation);
                Check(repaired != null && !repaired.KeyArrays[3].Entries.Any(k => k._index == 1) && measured.Euler > 0 && measured.Euler <= .01 &&
                    result.KeyframeReport.MaximumEulerError > 0,
                    label + ": a proved sub-limit pulse gives genuine bounded nonzero approximation without an unnecessary middle key");
            });
        }

        using (var dense = SourceKeyQualityDense(rig, 3))
        using (var source = SourceKeyQualitySource(rig, 3, rig.Bones.Where(b => !controls.Contains(b.Name)).Select(b => b.Name)))
        using (var flat = SourceKeyQualityDense(rig, 3))
        {
            foreach (CHR0EntryNode track in dense.Children)
                if (!controls.Contains(track.Name)) track.KeyArrays[3].GetKeyframe(1)._value += .009f;
            var unguarded = SourceKeyQualityMeasure(model, dense, flat);
            Check(unguarded.Euler < .01 && Math.Max(unguarded.World, unguarded.Skin) > .0001 * unguarded.Length,
                label + ": sub-limit local rotations collectively exceed the frozen native hierarchy/skin bound, so independent channel acceptance would be wrong");
            SourceKeyQualityRun(model, rig, plan, dense, source, output, label + "-collective-hierarchy", null);
        }

        using (var dense = SourceKeyQualityDense(rig, 3))
        using (var source = SourceKeyQualitySource(rig, 3, new[] { body }))
        {
            var track = (CHR0EntryNode)dense.FindChild(body, false);
            foreach (int at in Enumerable.Range(0, 3))
            {
                track.KeyArrays[0].GetKeyframe(at)._value = 1 - at;
                track.KeyArrays[1].GetKeyframe(at)._value = 2;
                track.KeyArrays[2].GetKeyframe(at)._value = -1;
            }
            SourceKeyQualityRun(model, rig, plan, dense, source, output, label + "-singular-mirrored-scale", result =>
            {
                var repaired = (CHR0EntryNode)result.Animation.FindChild(body, false);
                Check(repaired != null && repaired.KeyArrays[0].Entries.Select(k => k._index).SequenceEqual(new[] { 0f, 1f, 2f }) &&
                    repaired.KeyArrays[0].Entries.Select(k => ReplayFloatBits(k._value)).SequenceEqual(track.KeyArrays[0].Entries.Select(k => ReplayFloatBits(k._value))),
                    label + ": missing source scale cannot normalize, smooth or approximate the authored zero crossing and mirrored scale");
            });
        }
    }

    static CHR0Node SourceKeyQualityDense(BrawlRig rig, int frames)
    {
        var clip = new CHR0Node { Name = "Independent quality reference", FrameCount = frames };
        foreach (var bone in rig.Bones)
        {
            var track = clip.CreateEntry(bone.Name); track.Keyframes.PreserveInterpolation = true;
            for (int channel = 0; channel < 9; channel++) for (int at = 0; at < frames; at++)
                track.Keyframes.SetFrameValue(channel, at, bone.BindState[channel], true)._tangent = 0;
        }
        return clip;
    }

    static CHR0Node SourceKeyQualitySource(BrawlRig rig, int frames, IEnumerable<string> names)
    {
        var source = new CHR0Node { Name = "Independent authored seed schedule", FrameCount = frames };
        foreach (string name in names)
        {
            var track = source.CreateEntry(name); track.Keyframes.PreserveInterpolation = true;
            for (int channel = 3; channel < 6; channel++) foreach (int at in new[] { 0, frames - 1 })
                track.Keyframes.SetFrameValue(channel, at, 0, true)._tangent = 0;
        }
        return source;
    }

    static void SourceKeyQualityRun(MDL0Node model, BrawlRig rig, HumanoidRetargeter plan, CHR0Node dense, CHR0Node source,
        string output, string label, Action<RetargetResult> specific)
    {
        string baseline = NativeAnimationOptimization.ContentKey(dense), donor = NativeAnimationOptimization.SourceContentKey(source);
        foreach (var mode in new[] { RetargetKeyframeMode.SourceBoneTimes, RetargetKeyframeMode.SourceChannelTimes })
        using (var result = new RetargetResult { Animation = NativeAnimationOptimization.Copy(dense), Plan = plan })
        {
            RetargetKeyframes.Prepare(result, rig, rig, source, mode, CancellationToken.None, null);
            var measured = SourceKeyQualityMeasure(model, dense, result.Animation);
            double local = Math.Min(.0002, .0001 * measured.Length), world = .0001 * measured.Length;
            Check(result.KeyframeReport.Version == "source-key-quality-v2" && result.KeyframeReport.ScalarCertified && result.KeyframeReport.QualityVerified,
                label + " / " + mode + ": complete scalar certificate and cumulative native verification are required");
            Check(measured.Euler <= .01 && measured.Orientation <= .01 && measured.Translation <= local && measured.Scale <= .00001 &&
                measured.World <= world && measured.Skin <= world && measured.EulerDerivative <= .05 &&
                measured.TranslationDerivative <= local && measured.ScaleDerivative <= .00001,
                label + " / " + mode + ": independent withheld native subframes obey local orientation, raw turns, derivatives, hierarchy and every skin vertex");
            Check(result.KeyframeReport.ResultBytes <= result.KeyframeReport.DenseBytes && result.Animation.Children.All(t => rig.Bones.Any(b => b.Name == t.Name)),
                label + " / " + mode + ": complete candidate does not grow or add bones absent from the native target");
            foreach (int index in plan.TargetMotionControls)
                SourceKeysSameTrack((CHR0EntryNode)dense.FindChild(rig.Bones[index].Name, false),
                    (CHR0EntryNode)result.Animation.FindChild(rig.Bones[index].Name, false), label + " / " + mode + ": protected source-owned controls retain exact native tuples");
            specific?.Invoke(result);
            File.WriteAllText(Path.Combine(output, "source-key-quality-" + label + "-" + mode + ".json"),
                new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 }.Serialize(new { Passed = true, NativeMeasured = measured, Report = result.KeyframeReport }));
        }
        Check(NativeAnimationOptimization.ContentKey(dense) == baseline && NativeAnimationOptimization.SourceContentKey(source) == donor,
            label + ": quality preparation preserves its frozen dense baseline and exact authored source");
    }

    sealed class SourceKeyQualityMeasurements
    {
        public double Length, Euler, Orientation, Translation, Scale, World, Skin, EulerDerivative, TranslationDerivative, ScaleDerivative;
        public int Samples;
    }

    static SourceKeyQualityMeasurements SourceKeyQualityMeasure(MDL0Node model, CHR0Node before, CHR0Node after)
    {
        var measured = new SourceKeyQualityMeasurements(); var bones = model.AllBones.ToArray();
        var vertices = model.Objects.OfType<MDL0ObjectNode>().SelectMany(o => o.Vertices ?? new List<Vertex3>()).ToArray();
        model.ApplyCHR(null, 0); var bind = vertices.Select(v => v.WeightedPosition).ToArray();
        measured.Length = Math.Sqrt(Math.Pow(bind.Max(v => v._x) - bind.Min(v => v._x), 2) +
            Math.Pow(bind.Max(v => v._y) - bind.Min(v => v._y), 2) + Math.Pow(bind.Max(v => v._z) - bind.Min(v => v._z), 2));
        var times = new SortedSet<float>(Enumerable.Range(0, before.FrameCount).Select(i => (float)i));
        times.Add(before.Loop ? before.FrameCount : before.FrameCount - .0001f);
        for (int frame = 0; frame < before.FrameCount - 1; frame++) foreach (float fraction in new[] { .1732051f, .4142136f, .7320508f }) times.Add(frame + fraction);
        foreach (float time in times)
        {
            model.ApplyCHR(before, time + 1); var local = bones.Select(b => b.FrameState).ToArray();
            var positions = bones.Select(b => b._frameMatrix.GetPoint()).ToArray(); var skin = vertices.Select(v => v.WeightedPosition).ToArray();
            model.ApplyCHR(after, time + 1);
            for (int i = 0; i < bones.Length; i++)
            {
                var current = bones[i].FrameState; double orientation = 0, translation = 0;
                var a = Matrix.RotationMatrix(local[i].Rotate); var b = Matrix.RotationMatrix(current.Rotate);
                for (int c = 0; c < 3; c++)
                {
                    measured.Scale = Math.Max(measured.Scale, Math.Abs(current[c] - (double)local[i][c]));
                    measured.Euler = Math.Max(measured.Euler, Math.Abs(current[c + 3] - (double)local[i][c + 3]));
                    translation += Math.Pow(current[c + 6] - (double)local[i][c + 6], 2);
                    for (int r = 0; r < 3; r++) orientation += Math.Pow(a[c * 4 + r] - (double)b[c * 4 + r], 2);
                }
                measured.Orientation = Math.Max(measured.Orientation, 2 * Math.Asin(Math.Min(1, Math.Sqrt(orientation) / (2 * Math.Sqrt(2)))) * 180 / Math.PI);
                measured.Translation = Math.Max(measured.Translation, Math.Sqrt(translation));
                measured.World = Math.Max(measured.World, SourceKeyDistance(positions[i], bones[i]._frameMatrix.GetPoint()));
                var oldTrack = (CHR0EntryNode)before.FindChild(bones[i].Name, false); var newTrack = (CHR0EntryNode)after.FindChild(bones[i].Name, false);
                for (int channel = 0; channel < 9; channel++)
                {
                    double difference = Math.Abs(SourceKeyDerivative(oldTrack, channel, time) - SourceKeyDerivative(newTrack, channel, time));
                    if (channel < 3) measured.ScaleDerivative = Math.Max(measured.ScaleDerivative, difference);
                    else if (channel < 6) measured.EulerDerivative = Math.Max(measured.EulerDerivative, difference);
                    else measured.TranslationDerivative = Math.Max(measured.TranslationDerivative, difference);
                }
            }
            for (int i = 0; i < vertices.Length; i++) measured.Skin = Math.Max(measured.Skin, SourceKeyDistance(skin[i], vertices[i].WeightedPosition));
            measured.Samples++;
        }
        model.ApplyCHR(null, 0); return measured;
    }

    static double SourceKeyDistance(Vector3 a, Vector3 b) => Math.Sqrt(Math.Pow(a._x - (double)b._x, 2) + Math.Pow(a._y - (double)b._y, 2) + Math.Pow(a._z - (double)b._z, 2));

    static bool SourceKeyRawConstant(KeyframeArray curve) => curve.KeyCount == 0 || curve.Entries.Select(k => k._index).Distinct().Count() == curve.KeyCount &&
        curve.Entries.All(k => ReplayFloatBits(k._value) == ReplayFloatBits(curve.Entries.First()._value) && ReplayFloatBits(k._value) != int.MinValue && ReplayFloatBits(k._tangent) == 0);

    static double SourceKeyDerivative(CHR0EntryNode track, int channel, float time)
    {
        if (track == null || (channel < 3 ? track.UseModelScale : channel < 6 ? track.UseModelRotate : track.UseModelTranslate)) return 0;
        var keys = track.KeyArrays[channel].Entries.ToArray(); if (keys.Length < 2 || time < keys[0]._index || time > keys[keys.Length - 1]._index) return 0;
        int right = Array.FindIndex(keys, k => k._index > time); if (right < 0) right = keys.Length - 1;
        int left = right - 1; while (left > 0 && keys[left]._index == keys[right]._index) left--;
        if (left < 0 || keys[left]._index == keys[right]._index) return 0;
        double span = keys[right]._index - keys[left]._index, u = (time - keys[left]._index) / span;
        return (6 * u * u - 6 * u) * keys[left]._value / span + (3 * u * u - 4 * u + 1) * keys[left]._tangent +
            (-6 * u * u + 6 * u) * keys[right]._value / span + (3 * u * u - 2 * u) * keys[right]._tangent;
    }
}
