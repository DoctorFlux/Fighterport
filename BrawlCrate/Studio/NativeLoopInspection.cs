using BrawlCrate.Studio.Retargeting;
using BrawlLib.Modeling;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BrawlCrate.Studio
{
    // Endpoint observations only. These objects neither own nor pose native resources.
    internal sealed class NativeLoopDifference
    {
        internal readonly bool Valid;
        internal readonly double TranslationDistance, RotationDegrees, ScaleMaximum;
        internal readonly string Details;
        internal NativeLoopDifference(bool valid, double translation, double rotation, double scale, string details)
        { Valid = valid; TranslationDistance = translation; RotationDegrees = rotation; ScaleMaximum = scale; Details = details; }
    }

    internal sealed class NativeLoopBone
    {
        internal readonly string Name, Identity, Classification, Issue, Details;
        internal readonly NativeLoopDifference Endpoints, Closing;
        internal NativeLoopBone(string name, string identity, string classification, string issue,
            NativeLoopDifference endpoints, NativeLoopDifference closing, string details)
        { Name = name; Identity = identity; Classification = classification; Issue = issue; Endpoints = endpoints; Closing = closing; Details = details; }
    }

    internal sealed class NativeLoopReport
    {
        internal readonly int Frames;
        internal readonly bool Loop;
        internal readonly NativeLoopBone[] Bones;
        internal readonly string[] Notices;
        internal readonly bool[] SafeJumps;
        internal NativeLoopReport(int frames, bool loop, NativeLoopBone[] bones, string[] notices, bool[] safeJumps)
        { Frames = frames; Loop = loop; Bones = bones; Notices = notices; SafeJumps = safeJumps; }
    }

    internal static class NativeLoopInspection
    {
        internal const int KeyLimit = 2000000;
        static readonly string[] Channels = { "Scale X", "Scale Y", "Scale Z", "Rotation X", "Rotation Y", "Rotation Z", "Translation X", "Translation Y", "Translation Z" };
        static readonly StringComparer NativeNames = StringComparer.OrdinalIgnoreCase;
        sealed class Track
        {
            internal CHR0EntryNode Node;
            internal KeyframeEntry[][] Keys;
            internal readonly HashSet<string> Issues = new HashSet<string>(StringComparer.Ordinal);
        }

        internal static NativeLoopReport Capture(MDL0Node model, CHR0Node clip, ISet<string> protectedNames)
        {
            if (model == null) throw new InvalidOperationException("Choose the current native target before inspection.");
            return CaptureBones(model.BoneCache.OfType<MDL0BoneNode>().ToArray(), clip, protectedNames);
        }

        // The same implementation accepts original procedural bones without a renderer/model owner.
        internal static NativeLoopReport CaptureBones(MDL0BoneNode[] bones, CHR0Node clip, ISet<string> protectedNames)
        {
            if (clip == null) throw new InvalidOperationException("Choose a project-owned CHR0 before inspection.");
            if (bones == null || bones.Length == 0) throw new InvalidOperationException("The native target has no bones to inspect.");
            int frames = clip.FrameCount;
            if (frames < 1 || frames == int.MaxValue) throw new InvalidDataException("The animation has an invalid frame count.");
            var tracks = clip.Children.OfType<CHR0EntryNode>().Select(t => new Track { Node = t }).ToArray();
            // Check declared counts before allocating or sampling. Scan actual entries as well.
            long keys = 0;
            foreach (var t in tracks) foreach (var a in t.Node.Keyframes._keyArrays)
            {
                if (a.KeyCount < 0 || (keys += a.KeyCount) > KeyLimit)
                    throw new InvalidDataException("Loop inspection is limited to two million authored keys.");
            }
            keys = 0;
            foreach (var t in tracks)
            {
                t.Keys = new KeyframeEntry[9][];
                for (int c = 0; c < 9; c++)
                {
                    var list = new List<KeyframeEntry>();
                    float prior = -1; int paired = 0;
                    foreach (var key in t.Node.Keyframes[c].Entries)
                    {
                        if (++keys > KeyLimit) throw new InvalidDataException("Loop inspection is limited to two million authored keys.");
                        if (key._index < 0 || key._index >= (long)frames + (clip.Loop ? 1 : 0) || key._index < prior)
                            t.Issues.Add(Channels[c] + " has an invalid authored key index.");
                        paired = key._index == prior ? paired + 1 : 1;
                        if (paired > 2) t.Issues.Add(Channels[c] + " has more than two keys at one index.");
                        if (!Finite(key._value) || !Finite(key._tangent))
                            t.Issues.Add(Channels[c] + " has a non-finite authored value or tangent.");
                        prior = key._index; list.Add(key);
                    }
                    if (list.Count != t.Node.Keyframes[c].KeyCount)
                        t.Issues.Add(Channels[c] + " has an inconsistent native key count.");
                    t.Keys[c] = list.ToArray();
                }
            }
            var notices = new List<string> { "Local native units, degrees and frames. Endpoint differences are observations, not quality scores. Raw Euler deltas retain full turns." };
            if (protectedNames == null) notices.Add("Protected/body classification unavailable: no valid reviewed binding is available.");
            var byBone = bones.GroupBy(b => b.Name ?? "", NativeNames).ToDictionary(g => g.Key, g => g.ToArray(), NativeNames);
            var byTrack = tracks.GroupBy(t => t.Node.Name ?? "", NativeNames).ToDictionary(g => g.Key, g => g.ToArray(), NativeNames);
            foreach (var t in tracks)
            {
                if (!byBone.ContainsKey(t.Node.Name ?? ""))
                    notices.Add("Unmatched animation track: " + t.Node.Name + " [track " + t.Node.Index + "].");
                foreach (string issue in t.Issues.Distinct()) notices.Add("Track " + t.Node.Name + ": " + issue);
            }
            var result = new List<NativeLoopBone>();
            var safe = new[] { true, true, clip.Loop };
            for (int index = 0; index < bones.Length; index++)
            {
                var bone = bones[index]; string name = bone.Name ?? "";
                Track[] matches; byTrack.TryGetValue(name, out matches);
                string issue = "";
                bool ambiguous = byBone[name].Length != 1 || (matches != null && matches.Length != 1) || name.Contains("/") || name.Length == 0;
                if (ambiguous) issue = "Ambiguous native bone/track binding; measurements unavailable.";
                var track = matches != null && matches.Length == 1 ? matches[0] : null;
                if (track != null && track.Issues.Count != 0) issue = Join(issue, string.Join(" ", track.Issues.Distinct()));
                string classification = protectedNames == null || ambiguous ? "Classification unavailable" : protectedNames.Contains(name) ? "Protected motion control" : "Other bones";
                var samples = new FrameState[3]; var valid = new bool[3];
                for (int position = 0; position < (clip.Loop ? 3 : 2); position++)
                {
                    int time = position == 0 ? 0 : position == 1 ? frames - 1 : frames;
                    samples[position] = bone.BindState;
                    if (!ambiguous && track != null && track.Issues.Count == 0)
                        for (int c = 0; c < 9; c++)
                            if (track.Keys[c].Length > 0) samples[position][c] = track.Node.GetFrameValue(c, time);
                    valid[position] = issue.Length == 0 && SampleFinite(samples[position]);
                    if (!valid[position]) safe[position] = false;
                    if (!SampleFinite(samples[position])) issue = Join(issue, "Non-finite native sample at display frame " + (time + 1) + ".");
                }
                var endpoint = Difference(samples[0], samples[1], valid[0] && valid[1], "First → Last");
                var closing = clip.Loop ? Difference(samples[2], samples[0], valid[2] && valid[0], "Closing → First") : null;
                var details = new StringBuilder();
                details.AppendLine("First (display 1 / key time 0): " + Sample(samples[0]));
                details.AppendLine("Last (display " + frames + " / key time " + (frames - 1) + "): " + Sample(samples[1]));
                if (clip.Loop) details.AppendLine("Closing (display " + (frames + 1) + " / key time " + frames + "): " + Sample(samples[2]));
                details.AppendLine(endpoint.Details); if (closing != null) details.AppendLine(closing.Details);
                if (ambiguous) details.AppendLine("No ambiguous name binding was selected for this report.");
                else if (track == null) details.AppendLine("No matching track: every local component retains authentic bind data.");
                else
                {
                    details.AppendLine("Native local evaluation starts from bind data and overrides only channels containing keys. Values at paired indices use the incoming side, as in native posing.");
                    for (int c = 0; c < 9; c++)
                    {
                        var authored = track.Keys[c];
                        if (authored.Length == 0) { details.AppendLine(Channels[c] + ": absent; bind " + F(bone.BindState[c]) + "."); continue; }
                        details.AppendLine(Channels[c] + ": " + authored.Length + " authored keys.");
                        foreach (int time in (clip.Loop ? new[] { 0, frames - 1, frames } : new[] { 0, frames - 1 }).Distinct())
                            details.AppendLine("  Display " + (time + 1) + " / key time " + time + ": " + Boundary(authored, time));
                    }
                }
                result.Add(new NativeLoopBone(name, "bone[" + index + "] " + BonePath(bone), classification, issue, endpoint, closing, details.ToString()));
                if (issue.Length != 0) notices.Add(name + " [bone " + index + "]: " + issue);
            }
            return new NativeLoopReport(frames, clip.Loop, result.ToArray(), notices.ToArray(), safe);
        }

        static string BonePath(MDL0BoneNode bone)
        {
            var parts = new List<string>(); ResourceNode node = bone;
            while (node is MDL0BoneNode) { parts.Add((node.Name ?? "<unnamed>") + "[" + node.Index + "]"); node = node.Parent; }
            parts.Reverse(); return string.Join("/", parts);
        }
        static string Boundary(KeyframeEntry[] keys, int time)
        {
            var at = keys.Where(k => k._index == time).ToArray();
            if (at.Length != 0) return "authored " + KeySides(at);
            var before = keys.LastOrDefault(k => k._index < time);
            var after = keys.FirstOrDefault(k => k._index > time);
            return "no authored key; lower outgoing " + Key(before) + "; upper incoming " + Key(after) +
                "; first incoming " + Key(keys[0]) + "; last outgoing " + Key(keys[keys.Length - 1]) + ".";
        }
        static string KeySides(KeyframeEntry[] keys) => "incoming " + Key(keys[0]) + "; outgoing " + Key(keys[keys.Length - 1]) + (keys.Length > 1 ? " (paired keys)." : " (one key, shared tangent).");
        static string Key(KeyframeEntry key) => key == null ? "none" : "[time " + key._index + ", value " + F(key._value) + ", tangent " + F(key._tangent) + "]";
        static string Join(string first, string second) => first.Length == 0 ? second : first + " " + second;
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static bool SampleFinite(FrameState state) { for (int c = 0; c < 9; c++) if (!Finite(state[c])) return false; return true; }
        static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        static string Vector(FrameState state, int offset) => "(" + F(state[offset]) + ", " + F(state[offset + 1]) + ", " + F(state[offset + 2]) + ")";
        static string Sample(FrameState state) => "T " + Vector(state, 6) + " / Euler° " + Vector(state, 3) + " / S " + Vector(state, 0);
        static NativeLoopDifference Difference(FrameState from, FrameState to, bool valid, string label)
        {
            var delta = new double[9]; for (int c = 0; c < 9; c++) delta[c] = (double)to[c] - from[c];
            string details = label + " local ΔT " + Deltas(delta, 6) + ", raw ΔEuler° " + Deltas(delta, 3) + ", ΔS " + Deltas(delta, 0) + ".";
            if (!valid) return new NativeLoopDifference(false, double.NaN, double.NaN, double.NaN, details + " Measurement unavailable; see binding/numeric issue.");
            var a = BrawlRetargeter.Rotation(from.Rotate); var b = BrawlRetargeter.Rotation(to.Rotate);
            double normA = Math.Sqrt((double)a.X * a.X + (double)a.Y * a.Y + (double)a.Z * a.Z + (double)a.W * a.W);
            double normB = Math.Sqrt((double)b.X * b.X + (double)b.Y * b.Y + (double)b.Z * b.Z + (double)b.W * b.W);
            double dot = Math.Abs(((double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z + (double)a.W * b.W) / (normA * normB));
            double angle = 2 * Math.Acos(Math.Min(1, dot)) * 180 / Math.PI;
            if (!Finite(angle)) return new NativeLoopDifference(false, double.NaN, double.NaN, double.NaN, details + " Native orientation is non-finite.");
            return new NativeLoopDifference(true, Math.Sqrt(delta[6] * delta[6] + delta[7] * delta[7] + delta[8] * delta[8]), angle,
                Math.Max(Math.Abs(delta[0]), Math.Max(Math.Abs(delta[1]), Math.Abs(delta[2]))), details);
        }
        static string Deltas(double[] delta, int offset) => "(" + F(delta[offset]) + ", " + F(delta[offset + 1]) + ", " + F(delta[offset + 2]) + ")";
    }
}
