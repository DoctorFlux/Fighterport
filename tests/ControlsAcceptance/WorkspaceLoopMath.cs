using BrawlCrate.Studio;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Linq;
using NativeVector = BrawlLib.Internal.Vector3;

internal static partial class Program
{
    // Frozen before implementation. Expectations below use original authored
    // constants, the Hermite polynomial and independent double-precision matrices.
    const double LoopPositionTolerance = .0002;
    const double LoopScaleTolerance = .00001;
    const double LoopOrientationTolerance = .01;
    static MDL0BoneNode LoopBone(string name, float tx = 7, float ty = -3, float tz = 4)
        => new MDL0BoneNode { Name = name, BindState = new FrameState(new NativeVector(-2, 0, 3), new NativeVector(10, 20, 30), new NativeVector(tx, ty, tz)) };
    static void LoopKey(CHR0EntryNode track, int channel, int time, float value, float tangent = 0)
        => track.SetKeyframe(channel, time, value, true, true)._tangent = tangent;
    static bool LoopNear(double actual, double expected, double tolerance) => !double.IsNaN(actual) && Math.Abs(actual - expected) <= tolerance;
    static void WorkspaceLoopMath()
    {
        using (var bind = new CHR0Node { Name = "Procedural bind", FrameCount = 5, Loop = true })
        using (var bone = LoopBone("Unfamiliar deform"))
        {
            var snapshot = bone.BindState; int authored = 0;
            bone.PropertyChanged += sender => authored++;
            bind.PropertyChanged += sender => authored++;
            var r = NativeLoopInspection.CaptureBones(new[] { bone }, bind, null);
            Check(r.Bones.Single().Endpoints.Valid && r.Bones.Single().Endpoints.TranslationDistance == 0 && LoopNear(r.Bones[0].Endpoints.RotationDegrees, 0, LoopOrientationTolerance) && r.Bones[0].Endpoints.ScaleMaximum == 0, "Loop math: absent track keeps authentic nonneutral bind pose, including mirrored/zero scale");
            Check(r.Bones[0].Classification == "Classification unavailable" && r.Notices.Any(n => n.Contains("classification unavailable")), "Loop math: unavailable reviewed bindings are explicit and never inferred from names");
            Check(r.SafeJumps.SequenceEqual(new[] { true, true, true }) && r.Bones[0].Details.Contains("T (7, -3, 4)") && r.Bones[0].Details.Contains("S (-2, 0, 3)"), "Loop math: bind samples and all three legal loop positions are reported");
            Check(authored == 0 && Enumerable.Range(0, 9).All(c => bone.BindState[c] == snapshot[c]) && bind.Children.Count == 0, "Loop math: capture emits no authored resource events and never changes native bind/track ownership");
            var absent = bind.CreateEntry(bone.Name); absent.UseModelTranslate = true;
            r = NativeLoopInspection.CaptureBones(new[] { bone }, bind, new HashSet<string>());
            Check(r.Bones[0].Classification == "Other bones" && r.Bones[0].Endpoints.TranslationDistance == 0 && r.Bones[0].Details.Contains("Translation Y: absent; bind -3"), "Loop math: present empty channels retain bind components instead of native array defaults");
        }

        using (var clip = new CHR0Node { Name = "Authored roots and turns", FrameCount = 5, Loop = true })
        using (var root = LoopBone("arbitrary control", 0, 0, 0))
        using (var body = LoopBone("arbitrary body"))
        {
            root.AddChild(body);
            var rt = clip.CreateEntry(root.Name); LoopKey(rt, 6, 0, 0); LoopKey(rt, 6, 4, 100); LoopKey(rt, 6, 5, 0);
            var bt = clip.CreateEntry(body.Name);
            LoopKey(bt, 6, 0, 1, 2); LoopKey(bt, 6, 4, 4, -1); LoopKey(bt, 6, 5, 10, 3);
            LoopKey(bt, 3, 0, 0); LoopKey(bt, 3, 4, 720); LoopKey(bt, 3, 5, 360);
            LoopKey(bt, 0, 0, -2); LoopKey(bt, 0, 4, 0); LoopKey(bt, 0, 5, -1);
            var r = NativeLoopInspection.CaptureBones(new[] { root, body }, clip, new HashSet<string> { root.Name });
            var row = r.Bones[1];
            Check(r.Bones[0].Classification == "Protected motion control" && r.Bones[1].Classification == "Other bones", "Loop math: only the supplied reviewed binding classifies protected motion controls, without inferring other anatomy");
            Check(LoopNear(r.Bones[0].Endpoints.TranslationDistance, 100, LoopPositionTolerance) && LoopNear(row.Endpoints.TranslationDistance, 3, LoopPositionTolerance) && LoopNear(row.Closing.TranslationDistance, 9, LoopPositionTolerance), "Loop math: authored parent travel stays separate from body-local endpoint and closure translation");
            Check(LoopNear(row.Endpoints.RotationDegrees, 0, LoopOrientationTolerance) && LoopNear(row.Closing.RotationDegrees, 0, LoopOrientationTolerance) && row.Endpoints.Details.Contains("raw ΔEuler° (720, 0, 0)") && row.Closing.Details.Contains("raw ΔEuler° (-360, 0, 0)"), "Loop math: full authored turns remain visible while shortest orientation treats them as equivalent");
            Check(LoopNear(row.Endpoints.ScaleMaximum, 2, LoopScaleTolerance) && LoopNear(row.Closing.ScaleMaximum, 1, LoopScaleTolerance) && row.Details.Contains("Translation Y: absent; bind -3"), "Loop math: nonuniform, negative and zero scale use component differences with absent-channel bind fallback");
            Check(row.Details.Contains("value 4, tangent -1") && row.Details.Contains("value 10, tangent 3") && row.Details.Contains("display 6 / key time 5"), "Loop math: explicit closing sample and authored boundary values/tangents retain native frame indices");
        }

        using (var clip = new CHR0Node { Name = "Independent Hermite wrap", FrameCount = 5, Loop = true })
        using (var bone = LoopBone("body"))
        {
            // Native loop limit is N+1=6. Wrap segment is t3=6,m=-2 to
            // t1+6=2,m=1, span4. H(.75)=1.6875; H(.25)=4.0625; H(.5)=2.5.
            var track = clip.CreateEntry(bone.Name); LoopKey(track, 6, 1, 2, 1); LoopKey(track, 6, 3, 6, -2);
            var r = NativeLoopInspection.CaptureBones(new[] { bone }, clip, new HashSet<string>());
            Check(LoopNear(r.Bones[0].Endpoints.TranslationDistance, 2.375, LoopPositionTolerance) && LoopNear(r.Bones[0].Closing.TranslationDistance, .8125, LoopPositionTolerance), "Loop math: independent cubic Hermite wrap expectations verify first/last/interpolated closing samples");
            Check(r.Bones[0].Details.Contains("T (1.6875, -3, 4)") && r.Bones[0].Details.Contains("T (4.0625, -3, 4)") && r.Bones[0].Details.Contains("T (2.5, -3, 4)"), "Loop math: sparse loop samples use native N+1 span, without FPS or invented endpoint alignment");
            Check(r.Bones[0].Details.Contains("no authored key; lower outgoing") && r.Bones[0].Details.Contains("time 3, value 6, tangent -2"), "Loop math: interpolated boundaries identify neighboring authored key sides instead of fabricated boundary keys");
        }

        using (var clip = new CHR0Node { Name = "Paired boundary sides", FrameCount = 5, Loop = true })
        using (var bone = LoopBone("paired"))
        {
            var track = clip.CreateEntry(bone.Name);
            LoopKey(track, 6, 0, 1, 10); LoopKey(track, 6, 0, 20, -3);
            LoopKey(track, 6, 4, 7, 2); LoopKey(track, 6, 4, 30, 9);
            LoopKey(track, 6, 5, 11, -2); LoopKey(track, 6, 5, 13, 5);
            var r = NativeLoopInspection.CaptureBones(new[] { bone }, clip, null); var row = r.Bones[0];
            Check(LoopNear(row.Endpoints.TranslationDistance, 6, LoopPositionTolerance) && LoopNear(row.Closing.TranslationDistance, 10, LoopPositionTolerance), "Loop math: native posing reads incoming values at every paired exact boundary");
            Check(row.Details.Contains("incoming [time 0, value 1, tangent 10]; outgoing [time 0, value 20, tangent -3] (paired keys)") && row.Details.Contains("incoming [time 4, value 7, tangent 2]; outgoing [time 4, value 30, tangent 9]") && row.Details.Contains("outgoing [time 5, value 13, tangent 5]"), "Loop math: authored incoming/outgoing paired values and tangent sides remain distinct");
        }

        foreach (bool loop in new[] { false, true })
        using (var clip = new CHR0Node { Name = "Single frame", FrameCount = 1, Loop = loop })
        using (var bone = LoopBone("one"))
        {
            var track = clip.CreateEntry(bone.Name); LoopKey(track, 6, 0, 10); if (loop) LoopKey(track, 6, 1, 12);
            var r = NativeLoopInspection.CaptureBones(new[] { bone }, clip, null);
            Check(r.Frames == 1 && r.Bones[0].Endpoints.TranslationDistance == 0 && (loop ? r.Bones[0].Closing.TranslationDistance == 2 : r.Bones[0].Closing == null) && r.SafeJumps[2] == loop, "Loop math: one-frame " + (loop ? "loop retains independent closing key" : "non-loop has no closing sample or jump"));
        }
        using (var clip = new CHR0Node { Name = "Two frames", FrameCount = 2, Loop = false })
        using (var bone = LoopBone("two"))
        {
            var track = clip.CreateEntry(bone.Name);
            LoopKey(track, 6, 0, 1); LoopKey(track, 6, 1, 4); LoopKey(track, 7, 0, -3); LoopKey(track, 7, 1, 1);
            LoopKey(track, 0, 0, -2); LoopKey(track, 0, 1, 2); LoopKey(track, 1, 0, 0); LoopKey(track, 1, 1, -5);
            var r = NativeLoopInspection.CaptureBones(new[] { bone }, clip, null);
            Check(LoopNear(r.Bones[0].Endpoints.TranslationDistance, 5, LoopPositionTolerance) && LoopNear(r.Bones[0].Endpoints.ScaleMaximum, 5, LoopScaleTolerance) && r.Bones[0].Closing == null && r.SafeJumps.SequenceEqual(new[] { true, true, false }), "Loop math: two-frame Euclidean translation and maximum absolute scale metrics match independent 3-4-5 expectations");
        }

        foreach (var rotations in new[] {
            new[] { new double[] { 30, 40, 50 }, new double[] { -10, 20, -30 } },
            new[] { new double[] { 179, 0, 0 }, new double[] { -179, 0, 0 } },
            new[] { new double[] { 0, 90, 0 }, new double[] { 180, 90, 180 } } })
        using (var clip = new CHR0Node { Name = "Independent orientation", FrameCount = 2 })
        using (var bone = LoopBone("orientation"))
        {
            var track = clip.CreateEntry(bone.Name);
            for (int c = 0; c < 3; c++) { LoopKey(track, c + 3, 0, (float)rotations[0][c]); LoopKey(track, c + 3, 1, (float)rotations[1][c]); }
            double expected = LoopIndependentAngle(rotations[0], rotations[1]);
            var r = NativeLoopInspection.CaptureBones(new[] { bone }, clip, null);
            Check(LoopNear(r.Bones[0].Endpoints.RotationDegrees, expected, LoopOrientationTolerance), "Loop math: shortest native Euler orientation matches independent double matrix oracle " + string.Join(",", rotations[0]) + " → " + string.Join(",", rotations[1]));
        }

        using (var clip = new CHR0Node { Name = "Bindings", FrameCount = 2 })
        using (var bone = LoopBone("CaseBody"))
        using (var duplicate = LoopBone("casebody"))
        {
            var track = clip.CreateEntry("CASEBODY"); LoopKey(track, 6, 0, 0); LoopKey(track, 6, 1, 2);
            var unmatched = clip.CreateEntry("not a target bone"); LoopKey(unmatched, 6, 0, 123);
            var r = NativeLoopInspection.CaptureBones(new[] { bone }, clip, null);
            Check(r.Bones[0].Endpoints.TranslationDistance == 2 && r.Notices.Any(n => n.Contains("Unmatched animation track: not a target bone")), "Loop math: native case-insensitive matching reports unmatched tracks without substitution");
            r = NativeLoopInspection.CaptureBones(new[] { bone, duplicate }, clip, null);
            Check(r.Bones.All(b => !b.Endpoints.Valid && b.Issue.Contains("Ambiguous")) && !r.SafeJumps[0] && r.Bones[0].Identity != r.Bones[1].Identity, "Loop math: duplicate native bone names remain distinct identities and block ambiguous measurements/jumps");
            var duplicateTrack = new CHR0EntryNode { Name = "casebody" }; clip.AddChild(duplicateTrack); duplicateTrack.Name = "casebody";
            r = NativeLoopInspection.CaptureBones(new[] { bone }, clip, null);
            Check(!r.Bones[0].Endpoints.Valid && r.Bones[0].Issue.Contains("Ambiguous"), "Loop math: duplicate native track bindings never silently choose the first result");
        }

        foreach (int invalid in new[] { 0, 1, 2, 3 })
        using (var clip = new CHR0Node { Name = "Invalid numeric data", FrameCount = 2 })
        using (var bone = LoopBone("invalid"))
        {
            var track = clip.CreateEntry(bone.Name); LoopKey(track, 6, 0, 1); LoopKey(track, 6, 1, 2);
            if (invalid == 0) track.GetKeyframe(6, 1)._value = float.NaN;
            if (invalid == 1) track.GetKeyframe(6, 0)._tangent = float.PositiveInfinity;
            if (invalid == 2) { var state = bone.BindState; state[1] = float.NegativeInfinity; bone.BindState = state; }
            if (invalid == 3) track.GetKeyframe(6, 1)._index = 8;
            var r = NativeLoopInspection.CaptureBones(new[] { bone }, clip, null);
            Check(!r.Bones[0].Endpoints.Valid && r.Bones[0].Issue.Length > 0 && !r.SafeJumps[1] && r.Notices.Any(n => n.Contains(invalid == 3 ? "invalid authored key index" : "finite")), "Loop math: non-finite value/tangent/bind or invalid key index is explicit, not scored as a seam " + invalid);
        }

        using (var clip = new CHR0Node { Name = "Inspection bound", FrameCount = 2 })
        using (var bone = LoopBone("bound"))
        {
            var track = clip.CreateEntry(bone.Name); var count = track.Keyframes[0].GetType().GetField("_keyCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            count.SetValue(track.Keyframes[0], NativeLoopInspection.KeyLimit + 1);
            bool bounded = false; try { NativeLoopInspection.CaptureBones(new[] { bone }, clip, null); } catch (System.IO.InvalidDataException e) { bounded = e.Message.Contains("two million"); }
            finally { count.SetValue(track.Keyframes[0], 0); }
            Check(bounded, "Loop math: two-million-key limit rejects before sampling/allocating authored key arrays");
        }
    }

    static double LoopIndependentAngle(double[] a, double[] b)
    {
        // Independent conventional Rz*Ry*Rx column-vector matrix. Relative
        // rotation angle derives from trace(A^T B), not production quaternions.
        var ma = LoopIndependentMatrix(a); var mb = LoopIndependentMatrix(b);
        double trace = 0; for (int i = 0; i < 3; i++) for (int k = 0; k < 3; k++) trace += ma[k, i] * mb[k, i];
        return Math.Acos(Math.Max(-1, Math.Min(1, (trace - 1) / 2))) * 180 / Math.PI;
    }
    static double[,] LoopIndependentMatrix(double[] e)
    {
        double x = e[0] * Math.PI / 180, y = e[1] * Math.PI / 180, z = e[2] * Math.PI / 180;
        double cx = Math.Cos(x), sx = Math.Sin(x), cy = Math.Cos(y), sy = Math.Sin(y), cz = Math.Cos(z), sz = Math.Sin(z);
        return new[,] {
            { cz * cy, cz * sy * sx - sz * cx, cz * sy * cx + sz * sx },
            { sz * cy, sz * sy * sx + cz * cx, sz * sy * cx - cz * sx },
            { -sy, cy * sx, cy * cx } };
    }
}
