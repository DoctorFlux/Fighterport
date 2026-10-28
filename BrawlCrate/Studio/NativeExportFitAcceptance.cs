using BrawlLib.Internal;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace BrawlCrate.Studio
{
    // Offline admission for plain first-pose endpoint fits only. Owns its model
    // and frozen inputs; never applies an animation to the live editor model.
    // This proves boundary constraints, not contacts or the transition interior.
    internal sealed class NativeExportFitAcceptance : IDisposable
    {
        private sealed class Pose
        {
            internal float[][] Local;
            internal float[] World, Skin;
        }
        private sealed class Boundary
        {
            internal int Frame;
            internal Pose Master;
        }
        private readonly List<ResourceNode> owned = new List<ResourceNode>();
        private readonly List<Boundary> boundaries = new List<Boundary>();
        private readonly Dictionary<string, CHR0Node> inputs = new Dictionary<string, CHR0Node>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> rootTracks = new Dictionary<string, string>(StringComparer.Ordinal);
        private MDL0Node model;
        private MDL0BoneNode[] bones;
        private Vertex3[] vertices;
        private string masterContent;
        private CHR0Node masterReference;
        private int frames;
        private bool loop;
        internal bool Eligible { get; private set; }
        internal string Reason { get; private set; }
        internal string Report { get; private set; }
        internal double MaximumInitialRatio { get; private set; }

        internal static NativeExportFitAcceptance Prepare(string frozenModelPath, CHR0Node master, NativeTransitionFit fit,
            string[] reviewedMotionBones, string sourceMotionBone, bool bindingVerified, bool constraintsAbsent,
            string stage, CancellationToken token)
        {
            var result = new NativeExportFitAcceptance();
            try { result.PrepareCore(frozenModelPath, master, fit, reviewedMotionBones, sourceMotionBone, bindingVerified, constraintsAbsent, stage, token); }
            catch (OperationCanceledException) { result.Dispose(); throw; }
            catch (Exception error) when (error is InvalidDataException || error is InvalidOperationException || error is ArgumentException || error is IOException || error is NotSupportedException)
            {
                result.Reason = "Plain-fit absolute boundary acceptance unavailable: " + error.Message;
                result.Report = result.Reason; result.Eligible = false; result.Dispose();
            }
            return result;
        }

        private void PrepareCore(string modelPath, CHR0Node master, NativeTransitionFit fit, string[] motionBones,
            string sourceMotionBone, bool bindingVerified, bool constraintsAbsent, string stage, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Require(master != null && fit != null, "the exact fitted master and recipe are required");
            Require(bindingVerified && motionBones != null, "reviewed exact-rig body and motion bindings are required");
            Require(constraintsAbsent, "pins, contacts and baked support require separate absolute acceptance");
            fit.Validate();
            Require(new[] { "fixed-duration-fk-hermite-1", "fixed-duration-fk-hermite-2", "contact-trajectory-preserve-1", "contact-trajectory-preserve-2" }.Contains(fit.Algorithm),
                "recovery, floor and other fitting algorithms remain exact-only");
            var json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024, RecursionLimit = 100 };
            var options = json.Deserialize<TransitionFitOptions>(fit.Options);
            Require(options != null && options.BindingReviewed && !options.ExitContactRecovery && !options.BoundaryContactRecovery && !options.PreventFootDipping && !options.FloorReviewed,
                "the recipe must be an explicitly reviewed plain endpoint fit");
            Require(options.Components > 0 && (options.Components & ~7) == 0 && options.Bones != null && options.Bones.Length > 0 &&
                options.Bones.Distinct(StringComparer.Ordinal).Count() == options.Bones.Length, "the exact fitted component and bone selection is unavailable");
            Require(options.Entry != null || options.Exit != null, "the recipe has no endpoint");
            Require((options.Entry == null || options.EntryFrozen && options.EntryFrame == 0) && (options.Exit == null || options.ExitFrozen && options.ExitFrame == 0),
                "only frozen first reference poses are qualified");
            Require(options.Name == master.Name, "the fitted member name no longer matches the recorded recipe");
            var receiptRoot = json.DeserializeObject(fit.Receipt) as IDictionary<string, object>;
            var receipt = Field(receiptRoot, "Receipt") as IDictionary<string, object>;
            Require(receipt != null, "the saved fitting receipt is unavailable");
            var retainedContacts = Names(Field(receipt, "ProtectedContactBones"));
            Require(!options.PreserveContacts || retainedContacts != null && retainedContacts.Length == 0,
                "the recipe does not prove an empty preserved-contact set");
            Require(retainedContacts == null || retainedContacts.Length == 0, "recorded contact dependencies remain exact-only");
            Require(File.Exists(modelPath) && new FileInfo(modelPath).Length <= 256L * 1024 * 1024 &&
                string.Equals(FighterProject.HashFile(modelPath), fit.ModelSha256, StringComparison.OrdinalIgnoreCase), "the target model differs from the frozen fitting rig");
            model = NodeFactory.FromFile(null, modelPath) as MDL0Node;
            Require(model != null, "the frozen native target could not be independently reopened"); owned.Add(model);
            model.Populate();
            bones = model.AllBones.ToArray();
            Require(bones.Length > 0 && bones.Length <= 128 && bones.Select(b => b.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == bones.Length,
                "native target bindings are empty, ambiguous or outside the qualified adapter");
            var names = new HashSet<string>(bones.Select(b => b.Name), StringComparer.Ordinal);
            Require(options.Bones.All(names.Contains) && motionBones.All(names.Contains) && motionBones.Distinct(StringComparer.Ordinal).Count() == motionBones.Length,
                "fitted or motion-control names do not resolve uniquely on the frozen target");
            var matched = options.MatchReferenceControls ? Names(Field(receipt, "MatchedReferenceControls")) : new string[0];
            Require(matched != null && matched.All(motionBones.Contains) && matched.All(options.Bones.Contains) &&
                (!options.MatchReferenceControls || !string.IsNullOrEmpty(sourceMotionBone) && motionBones.Contains(sourceMotionBone) && !matched.Contains(sourceMotionBone)),
                "reference-control matching lacks reviewed retained motion-position evidence");
            var retained = new HashSet<string>(motionBones.Except(matched), StringComparer.Ordinal);
            frames = master.FrameCount; loop = master.Loop;
            Require(frames >= 3 && frames <= 100000, "the fitted master has invalid timing");
            int entryJoin = options.Entry == null ? 0 : options.EntrySamples - 1, exitJoin = options.Exit == null ? frames - 1 : frames - options.ExitSamples;
            Require(entryJoin >= 0 && exitJoin < frames && entryJoin < exitJoin &&
                (options.Entry == null || options.EntrySamples >= 2 && options.EntrySamples < frames) &&
                (options.Exit == null || options.ExitSamples >= 2 && options.ExitSamples < frames), "fitting windows are invalid or overlap");
            Directory.CreateDirectory(stage);
            foreach (var input in fit.Inputs)
            {
                token.ThrowIfCancellationRequested();
                string path = Path.Combine(stage, "fit-acceptance-" + input.Role + "-" + Guid.NewGuid().ToString("N") + ".chr0");
                File.WriteAllBytes(path, Convert.FromBase64String(input.Snapshot));
                NativeChr0SizeReport.Read(path); // Bound native offsets/counts before the unsafe resource decoder.
                var clip = NodeFactory.FromFile(null, path) as CHR0Node;
                Require(clip != null, "a frozen " + input.Role + " input did not reopen as CHR0"); owned.Add(clip);
                Require(clip.Name == input.Name && clip.FrameCount == input.Samples && (clip.Version == 4 || clip.Version == 5), "a frozen input's native identity or timing is inconsistent");
                CheckClip(clip); inputs.Add(input.Role, clip);
            }
            Require(inputs["source"].FrameCount == frames && inputs["source"].Loop == loop, "the original fit source timing differs from the master");
            foreach (string role in new[] { "entry", "exit" })
            {
                string key = role == "entry" ? options.Entry : options.Exit;
                var input = fit.Inputs.SingleOrDefault(i => i.Role == role);
                Require(key == null ? input == null : input != null && input.Entry == key, "the exact " + role + " reference identity differs from its frozen selection");
            }
            if (options.ExistingTracksOnly)
                Require(options.Bones.All(name => inputs["source"].Children.OfType<CHR0EntryNode>().Any(e => e.Name == name && e.KeyArrays.Any(a => a.KeyCount > 0))),
                    "the existing-track selection differs from the frozen source");
            CheckClip(master); masterReference = master; masterContent = NativeAnimationOptimization.SourceContentKey(master);
            foreach (string root in retained)
            {
                string key = Track(master, root);
                Require(key == Track(inputs["source"], root), "a source-owned motion control differs from the frozen attack: " + root);
                rootTracks.Add(root, key);
            }
            vertices = model.Objects.OfType<MDL0ObjectNode>().SelectMany(o => o.Vertices ?? new List<Vertex3>()).ToArray();
            Require(vertices.Length <= 2000000, "the native mesh exceeds the bounded endpoint adapter");
            double length = NativeAnimationOptimization.GetBindBoundsDiagonal(model);
            Require(Finite(length) && length > 0, "finite positive target geometry is required for full-model boundary acceptance");
            foreach (string role in new[] { "entry", "exit" })
            {
                if (!inputs.TryGetValue(role, out var reference)) continue;
                int time = role == "entry" ? 0 : frames - 1;
                var original = Capture(master, time, token); var anchor = Capture(reference, 0, token);
                using (var expected = new CHR0Node { Name = "Independent plain-fit boundary", Version = 4, FrameCount = 1 })
                {
                    for (int b = 0; b < bones.Length; b++)
                    {
                        var values = (float[])original.Local[b].Clone();
                        if (options.Bones.Contains(bones[b].Name) && !retained.Contains(bones[b].Name))
                            for (int c = 0; c < 9; c++)
                                if ((options.Components & (c < 3 ? 1 : c < 6 ? 2 : 4)) != 0 &&
                                    !(c >= 6 && options.PreserveRootTranslation && !(bones[b].Parent is MDL0BoneNode) && !matched.Contains(bones[b].Name)))
                                    values[c] = anchor.Local[b][c];
                        var entry = expected.CreateEntry(bones[b].Name);
                        for (int c = 0; c < 9; c++) entry.Keyframes.SetFrameValue(c, 0, values[c], true)._tangent = 0;
                    }
                    var wanted = Capture(expected, 0, token);
                    double ratio = Difference(original, wanted, length);
                    MaximumInitialRatio = Math.Max(MaximumInitialRatio, ratio);
                    Require(ratio <= .5, "the current " + role + " boundary does not have demonstrated strict-tolerance headroom against its frozen reference");
                }
            }
            foreach (int time in new[] { 0, frames - 1, entryJoin, exitJoin }.Concat(loop ? new[] { frames } : new int[0]).Distinct())
                boundaries.Add(new Boundary { Frame = time, Master = Capture(master, time, token) });
            Require(masterContent == NativeAnimationOptimization.SourceContentKey(master), "the frozen master changed during boundary preparation");
            Eligible = true; Reason = null;
            Report = "Plain frozen-pose fit: original reference identities/hashes and retained motion controls verified. Initial strict-boundary ratio " +
                MaximumInitialRatio.ToString("G7", System.Globalization.CultureInfo.InvariantCulture) + " (admission maximum 0.5). " + boundaries.Count +
                " endpoint/join/closing poses frozen. Bounded export must retain their complete native local/world/skin float outputs exactly; interior scalar and geometry certificates remain separately required. No contact or game acceptance.";
        }

        internal bool CheckCandidate(CHR0Node candidate, CancellationToken token, out string reason)
        {
            reason = Reason;
            if (!Eligible) return false;
            try
            {
                token.ThrowIfCancellationRequested(); CheckClip(candidate);
                Require(masterReference != null && NativeAnimationOptimization.SourceContentKey(masterReference) == masterContent, "the frozen master dependency changed");
                Require(candidate.FrameCount == frames && candidate.Loop == loop, "candidate timing changed");
                foreach (var root in rootTracks) Require(Track(candidate, root.Key) == root.Value, "a retained motion-control curve changed: " + root.Key);
                foreach (var boundary in boundaries)
                {
                    var actual = Capture(candidate, boundary.Frame, token);
                    Require(Same(actual, boundary.Master), "complete native endpoint/join pose changed at display frame " + (boundary.Frame + 1));
                }
                Require(NativeAnimationOptimization.SourceContentKey(masterReference) == masterContent, "the frozen master changed during candidate verification");
                reason = null; return true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) when (error is InvalidDataException || error is InvalidOperationException || error is ArgumentException || error is NotSupportedException)
            { reason = "Plain-fit boundary verification rejected the bounded candidate: " + error.Message; return false; }
        }

        private Pose Capture(CHR0Node clip, int time, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); model.ApplyCHR(clip, time + 1);
            var result = new Pose { Local = bones.Select(b => Enumerable.Range(0, 9).Select(c => b.FrameState[c]).ToArray()).ToArray(),
                World = new float[bones.Length * 16], Skin = new float[vertices.Length * 3] };
            for (int b = 0; b < bones.Length; b++) for (int c = 0; c < 16; c++) result.World[b * 16 + c] = bones[b]._frameMatrix[c];
            for (int v = 0; v < vertices.Length; v++)
            {
                if ((v & 1023) == 0) token.ThrowIfCancellationRequested();
                var p = vertices[v].WeightedPosition; result.Skin[v * 3] = p._x; result.Skin[v * 3 + 1] = p._y; result.Skin[v * 3 + 2] = p._z;
            }
            Require(result.Local.All(a => a.All(v => Finite(v))) && result.World.All(v => Finite(v)) && result.Skin.All(v => Finite(v)), "native boundary evaluation produced invalid numbers");
            return result;
        }

        private double Difference(Pose actual, Pose expected, double length)
        {
            double ratio = 0, local = Math.Min(.0002, length * .0001), world = length * .0001;
            for (int b = 0; b < bones.Length; b++)
            {
                for (int c = 0; c < 3; c++) ratio = Math.Max(ratio, Math.Abs(actual.Local[b][c] - expected.Local[b][c]) / .00001);
                ratio = Math.Max(ratio, Distance(actual.Local[b], expected.Local[b], 6, 3) / local);
                var a = Matrix.TransformMatrix(new Vector3(1), new Vector3(actual.Local[b][3], actual.Local[b][4], actual.Local[b][5]), new Vector3());
                var e = Matrix.TransformMatrix(new Vector3(1), new Vector3(expected.Local[b][3], expected.Local[b][4], expected.Local[b][5]), new Vector3());
                double squared = 0; for (int col = 0; col < 3; col++) for (int row = 0; row < 3; row++) squared += Math.Pow(a[col * 4 + row] - e[col * 4 + row], 2);
                ratio = Math.Max(ratio, 2 * Math.Asin(Math.Min(1, Math.Sqrt(squared) / (2 * Math.Sqrt(2)))) * 180 / Math.PI / .01);
                for (int col = 0; col < 4; col++) ratio = Math.Max(ratio, Distance(actual.World, expected.World, b * 16 + col * 4, 3) * (col < 3 ? length : 1) / world);
            }
            for (int v = 0; v < vertices.Length; v++) ratio = Math.Max(ratio, Distance(actual.Skin, expected.Skin, v * 3, 3) / world);
            return ratio;
        }
        private static double Distance(float[] a, float[] b, int start, int count)
        { double sum = 0; for (int c = start; c < start + count; c++) sum += Math.Pow((double)a[c] - b[c], 2); return Math.Sqrt(sum); }
        private static bool Same(Pose a, Pose b) => a.Local.Length == b.Local.Length && a.Local.SelectMany(x => x).Select(Bits).SequenceEqual(b.Local.SelectMany(x => x).Select(Bits)) &&
            a.World.Select(Bits).SequenceEqual(b.World.Select(Bits)) && a.Skin.Select(Bits).SequenceEqual(b.Skin.Select(Bits));
        private static string Track(CHR0Node clip, string bone)
        {
            var entries = clip.Children.OfType<CHR0EntryNode>().Where(e => e.Name == bone).ToArray();
            Require(entries.Length <= 1, "ambiguous native motion track"); if (entries.Length == 0) return "absent";
            var track = entries[0]; return string.Join(",", new[] { track.UseModelScale, track.UseModelRotate, track.UseModelTranslate, track.ScaleCompensateApply, track.ScaleCompensateParent, track.ClassicScaleOff }) + ":" +
                string.Join(";", track.KeyArrays.Select(a => string.Join(",", a.Entries.Select(k => Bits(k._index) + "/" + Bits(k._value) + "/" + Bits(k._tangent)))));
        }
        private static void CheckClip(CHR0Node clip)
        {
            Require(clip != null && (clip.Version == 4 || clip.Version == 5), "the native animation adapter is unavailable");
            Require(clip.FrameCount > 0 && clip.FrameCount <= 100000, "native timing is invalid");
            Require(clip.Children.OfType<CHR0EntryNode>().Select(e => e.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == clip.Children.Count,
                "native track bindings are ambiguous");
            Require(clip.Children.OfType<CHR0EntryNode>().Sum(e => e.KeyArrays.Sum(a => (long)a.KeyCount)) <= 2000000, "the native key budget is exceeded");
            long actual = 0;
            foreach (var entry in clip.Children.OfType<CHR0EntryNode>()) foreach (var array in entry.KeyArrays)
            {
                float previous = -1; int paired = 0, count = 0;
                foreach (var key in array.Entries)
                {
                    Require(++actual <= 2000000 && key._index >= 0 && key._index < clip.FrameCount + (clip.Loop ? 1 : 0) && key._index >= previous &&
                        Finite(key._index) && Finite(key._value) && Finite(key._tangent), "a native curve has invalid or unbounded coefficients");
                    paired = key._index == previous ? paired + 1 : 1;
                    Require(paired <= 2, "a native knot has ambiguous paired sides"); previous = key._index; count++;
                }
                Require(count == array.KeyCount, "a native curve count is inconsistent");
            }
        }
        private static string[] Names(object value) => value is IEnumerable sequence && !(value is string) ? sequence.Cast<object>().Select(Convert.ToString).ToArray() : null;
        private static object Field(IDictionary<string, object> node, string name) => node?.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
        private static void Require(bool value, string reason) { if (!value) throw new InvalidDataException(reason + "."); }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        public void Dispose() { for (int i = owned.Count - 1; i >= 0; i--) owned[i].Dispose(); owned.Clear(); }
    }
}
