using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;

internal static partial class Program
{
    static void NativeDenseWriterChecks(string stage)
    {
        Directory.CreateDirectory(stage);
        foreach (var format in new[] { AnimDataFormat.L1, AnimDataFormat.L2 })
        foreach (int version in new[] { 4, 5 }) foreach (bool loop in new[] { false, true }) foreach (int frames in new[] { 1, 2, 5 })
        {
            int count = frames + (loop ? 1 : 0); var samples = Enumerable.Range(0, frames + 1).Select(i => i * i).ToArray();
            var curve = IndexedCHR0CurveStorage.CreateDense(format, samples, count, -.5f, .125f);
            Check(curve.IsDense && curve.KeyCount == count && curve.EncodedSampleCount == frames + 1 && curve.Frames.SequenceEqual(Enumerable.Range(0, count)) &&
                curve.EncodedValues.SequenceEqual(samples.Select(q => -.5f + q * .125f)) && curve.Values.Length == count &&
                curve.ByteCount == ((8 + (frames + 1) * (format == AnimDataFormat.L1 ? 1 : 2) + 3) & ~3),
                "Dense factory " + format + " v" + version + " " + frames + "/" + loop + ": immutable complete payload count/bytes are distinct from the native projected key count");
            using (var clip = DenseOwnedClip(frames, loop, version, curve))
            {
                var entry = (CHR0EntryNode)clip.Children[0];
                if (count == 1)
                {
                    Check(NativeCompactAnimationStorage.CreateDenseAlternatives(entry, entry, false, new int[0], 2000000).Length == 0,
                        "Dense writer: one-frame non-loop keeps fixed storage rather than forcing an unnecessary dense representation");
                    continue;
                }
                string precise = Path.Combine(stage, "before-" + format + "-" + version + "-" + frames + "-" + loop + ".chr0"); clip.ExportChecked(precise);
                string content = NativeAnimationOptimization.ContentKey(clip);
                var channels = new IndexedCHR0CurveStorage[9]; channels[3] = curve;
                entry = PreciseCHR0EntryNode.RestoreStorage(entry, new IndexedCHR0EntryStorage(channels));
                string packed = Path.Combine(stage, "packed-" + format + "-" + version + "-" + frames + "-" + loop + ".chr0"); clip.ExportChecked(packed);
                var raw = DenseReadCompletePayloads(File.ReadAllBytes(packed)).Single();
                using (var reopened = NodeFactory.FromFile(null, packed) as CHR0Node)
                {
                    var restored = (CHR0EntryNode)reopened.Children[0]; var storage = PreciseCHR0EntryNode.CaptureStorage(restored);
                    Check(raw.Format == (int)format && raw.HeaderDuration == frames && raw.EncodedValues.SequenceEqual(curve.EncodedValues) &&
                        NativeAnimationOptimization.ContentKey(reopened) == content && storage?[3]?.ContentKey == curve.ContentKey &&
                        storage[3].Matches(restored.KeyArrays[3]) && new FileInfo(packed).Length < new FileInfo(precise).Length,
                        "Dense writer " + format + " v" + version + " " + frames + "/" + loop + ": independent complete-byte reader and native reopen retain all metadata/flags/coefficients and actually shrink the file");
                    string saved = Path.Combine(stage, "resaved-" + format + "-" + version + "-" + frames + "-" + loop + ".chr0");
                    PreciseCHR0EntryNode.RestoreStorage(restored, storage); reopened.ExportChecked(saved);
                    Check(File.ReadAllBytes(saved).SequenceEqual(File.ReadAllBytes(packed)),
                        "Dense writer " + format + ": an unchanged capture/save retains the complete encoded terminal sample and byte-identical file");
                }
                PreciseCHR0EntryNode.RestoreStorage(entry, null);
                string reset = Path.Combine(stage, "cleared-" + format + "-" + version + "-" + frames + "-" + loop + ".chr0"); clip.ExportChecked(reset);
                using (var decoded = NodeFactory.FromFile(null, reset) as CHR0Node)
                    Check(NativeAnimationOptimization.ContentKey(decoded) == content && DenseReadCompletePayloads(File.ReadAllBytes(reset)).Length == 0 &&
                        new FileInfo(reset).Length == new FileInfo(precise).Length,
                        "Dense writer " + format + ": explicit policy clear restores precise coefficients/storage after a packed rebuild without resurrecting the old dense bytes");
            }
        }

        var immutableInput = new[] { 0, 1, 3, 7 }; var immutable = IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1, immutableInput, 3, 0, 1);
        immutableInput[1] = 255; var escapedValues = immutable.Values; escapedValues[1] = 999; var escapedTangents = immutable.Tangents; escapedTangents[1] = 999;
        var differentTerminal = IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1, new[] { 0, 1, 3, 8 }, 3, 0, 1);
        Check(immutable.Values.SequenceEqual(new[] { 0f, 1f, 3f }) && immutable.Tangents.SequenceEqual(new[] { 0f, 1f, 2f }) &&
            immutable.EncodedValues[3] == 7 && immutable.ContentKey != differentTerminal.ContentKey,
            "Dense immutable identity owns its input/exposed arrays and includes the terminal sample even when native projected coefficients are identical");
        using (var clip = DenseOwnedClip(3, false, 4, immutable))
        {
            var entry = (CHR0EntryNode)clip.Children[0]; var channels = new IndexedCHR0CurveStorage[9]; channels[3] = immutable;
            entry = PreciseCHR0EntryNode.RestoreStorage(entry, new IndexedCHR0EntryStorage(channels));
            clip.FrameCount = 8;
            Check(!immutable.Matches(entry.KeyArrays[3]), "Dense implicit-timing guard rejects a previously matching payload after the native duration grows");
            string expected = NativeAnimationOptimization.ContentKey(clip), file = Path.Combine(stage, "duration-edit-precise-fallback.chr0"); clip.ExportChecked(file);
            using (var decoded = NodeFactory.FromFile(null, file) as CHR0Node)
                Check(NativeAnimationOptimization.ContentKey(decoded) == expected && DenseReadCompletePayloads(File.ReadAllBytes(file)).Length == 0,
                    "Dense duration edit exports its authored native keys through precise fallback rather than emitting too few dense samples under a larger header");
        }
        using (var clip = DenseOwnedClip(3, false, 4, immutable))
        {
            var entry = (CHR0EntryNode)clip.Children[0]; var channels = new IndexedCHR0CurveStorage[9]; channels[3] = immutable;
            entry = PreciseCHR0EntryNode.RestoreStorage(entry, new IndexedCHR0EntryStorage(channels)); clip.Loop = true;
            Check(!immutable.Matches(entry.KeyArrays[3]), "Dense implicit-timing guard rejects stale native-projection counts after a stored-loop edit");
        }
        var signed = IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1, new[] { 1, 0, 0 }, 2, 1, .0001f);
        Check(ReplayFloatBits(signed.Tangents[1]) == int.MinValue,
            "Dense factory independently matches the frozen native negative-zero tangent generated by a rounded tiny negative slope");
        var winding = IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1, new[] { 0, 1, 2 }, 3, 0, 360);
        Check(winding.EncodedValues.SequenceEqual(new[] { 0f, 360f, 720f }),
            "Dense immutable storage retains raw authored full turns; approval of a fast interval remains a separate quality/protection decision");
        foreach (var invalid in new Action[] {
            () => IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.I12, new[] { 0, 1 }, 2, 0, 1),
            () => IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L4, new[] { 0, 1 }, 2, 0, 1),
            () => IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1, new[] { 0 }, 1, 0, 1),
            () => IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1, new[] { 0, 1, 2, 3 }, 1, 0, 1),
            () => IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1, new[] { 0, 256 }, 2, 0, 1),
            () => IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L2, new[] { 0, 65536 }, 2, 0, 1),
            () => IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1, new[] { 0, 1 }, 2, float.NaN, 1),
            () => IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1, new[] { 0, 1 }, 2, 0, float.PositiveInfinity) })
        { bool rejected = false; try { invalid(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Dense factory rejects illegal format/count/range/nonfinite input before any native write"); }
        var illegalChannels = new IndexedCHR0CurveStorage[9]; illegalChannels[6] = immutable;
        bool illegalGroup = false; try { new IndexedCHR0EntryStorage(illegalChannels); } catch (ArgumentException) { illegalGroup = true; }
        Check(illegalGroup, "Dense storage cannot attach L1 to a native translation/scale format field");
        NativeDenseProposalChecks(stage);
        NativeDenseImportedL4Checks(stage);
        NativeDenseFinalGuardChecks(stage);
    }

    static void NativeDenseFinalGuardChecks(string stage)
    {
        using (var source = CompactFixture(3, 3, new[] { 0, 1, 2 }, i => i * .0001f, i => 0))
        {
            source.Loop = false; var entry = (CHR0EntryNode)source.Children[0]; var curve = entry.KeyArrays[3];
            var encoded = IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1, new[] { 0, 1, 2, 2 }, 3, 0, .0001f);
            var budget = new NativeDenseProofBudget(); string key = budget.ProofKey(entry, 3, encoded, .1, .1, CancellationToken.None);
            Check(NativeCompactAnimationStorage.DenseLinearWithinBounds(curve.Entries.ToArray(), curve, encoded.EncodedValues, .1, .1, CancellationToken.None, budget, key) && budget.Work > 0,
                "Dense proof cache fixture first establishes a real bounded continuous certificate against its complete original baseline");
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel(); bool stopped = false;
                try { NativeCompactAnimationStorage.DenseLinearWithinBounds(curve.Entries.ToArray(), curve, encoded.EncodedValues, .1, .1, cancellation.Token, budget, key); }
                catch (OperationCanceledException) { stopped = true; }
                Check(stopped, "Dense proof cancellation is checked before returning a previously cached successful certificate");
            }
        }

        // This is an interval-level numerical stress, not a publishable short
        // clip: the valid sparse source spans65534 frames, and we deliberately
        // ask the scalar primitive about only its first unit interval. Large
        // global coefficients cancel to small local values/derivatives there.
        var sparse = new KeyframeArray(65535);
        sparse.SetFrameValue(0, 0, true)._tangent = .001f;
        float farValue = (float)(65534d * 100000000d / 3);
        sparse.SetFrameValue(65534, farValue, true)._tangent = 100000000f;
        var firstInterval = new[] { 0f, sparse.GetFrameValue(1) };
        var independent = DenseLinearDifference(new[] { 0d, 65534d }, new[] { 0d, (double)farValue }, new[] { (double).001f, 100000000d }, firstInterval);
        Check(independent.MaximumValueError < .1 && independent.MaximumDerivativeError < .1,
            "Dense roundoff fixture independently has small first-interval Hermite-versus-linear value/derivative errors despite huge cancelling original coefficients");
        Check(!NativeCompactAnimationStorage.DenseLinearWithinBounds(sparse.Entries.ToArray(), sparse, firstInterval, .1, .1, CancellationToken.None),
            "Dense floating-point proof conservatively retains indexed fallback when original coefficient magnitude makes a tiny local residual uncertain");

        foreach (var format in new[] { AnimDataFormat.L1, AnimDataFormat.L2, AnimDataFormat.L4 })
        {
            var one = IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1, new[] { 1, 2 }, 1, 0, .125f);
            string path = Path.Combine(stage, "all-fixed-inert-format-" + format + ".chr0");
            using (var seed = DenseOwnedClip(1, false, 4, one)) seed.ExportChecked(path);
            byte[] bytes = File.ReadAllBytes(path); int group = DenseRead32(bytes, 0x10), entryAt = group + DenseRead32(bytes, group + 0x18 + 12);
            uint code = unchecked((uint)DenseRead32(bytes, entryAt + 4));
            if ((code & (7u << 16)) != (7u << 16)) throw new InvalidDataException("Inert-format fixture must own three inline fixed rotation values.");
            DenseWrite32(bytes, entryAt + 4, unchecked((int)((code & ~(7u << 27)) | (uint)format << 27))); File.WriteAllBytes(path, bytes);
            string fileHash = FighterProject.HashFile(path);
            using (var imported = NodeFactory.FromFile(null, path) as CHR0Node)
            using (var copy = NativeAnimationOptimization.Copy(imported))
                Check(!IndexedCHR0EntryStorage.HasEncodedDenseRotation((CHR0EntryNode)imported.Children[0]) &&
                    NativeAnimationOptimization.ContentKey(copy) == NativeAnimationOptimization.ContentKey(imported) && FighterProject.HashFile(path) == fileHash,
                    "All-fixed rotation ignores inert " + format + " format bits instead of inventing a missing imported dense payload or blocking a safe snapshot");
        }
    }

    static void NativeDenseImportedL4Checks(string stage)
    {
        foreach (int version in new[] { 4, 5 }) foreach (int frames in new[] { 1, 3 })
        {
            float[] values = Enumerable.Range(0, frames + 1).Select(i => i == frames ? 7f : i * .001f).ToArray();
            string path = DenseLiteralCompleteL4(stage, "complete-imported-l4-" + version + "-" + frames, version, frames, values);
            using (var imported = NodeFactory.FromFile(null, path) as CHR0Node)
            {
                var entry = (CHR0EntryNode)imported.Children[0]; var storage = PreciseCHR0EntryNode.CaptureStorage(entry);
                Check(storage?[3]?.Format == AnimDataFormat.L4 && storage[3].EncodedValues.SequenceEqual(values) && storage[3].KeyCount == frames &&
                    imported.Name == "Independent compact storage fixture" && imported.OriginalPath == "original native metadata",
                    "Imported L4 v" + version + " / " + frames + " frames: complete float terminal payload and historical native prefix are captured without proposing new L4 storage");
                using (var snapshot = NativeAnimationOptimization.Copy(imported))
                {
                    string exported = Path.Combine(stage, "retained-imported-l4-" + version + "-" + frames + ".chr0"); snapshot.ExportChecked(exported);
                    var raw = DenseReadCompletePayloads(File.ReadAllBytes(exported)).Single();
                    using (var reopened = NodeFactory.FromFile(null, exported) as CHR0Node)
                        Check(raw.Format == 6 && raw.EncodedValues.SequenceEqual(values) && NativeAnimationOptimization.ContentKey(reopened) == NativeAnimationOptimization.ContentKey(imported) &&
                            PreciseCHR0EntryNode.CaptureStorage((CHR0EntryNode)reopened.Children[0])?[3]?.ContentKey == storage[3].ContentKey,
                            "Imported L4 v" + version + ": snapshot/export/reopen retains the complete authored terminal sample and native metadata even for a one-frame non-loop");
                }
                Check(NativeCompactAnimationStorage.CreateDenseAlternatives(entry, entry, false, new int[0], 2000000).Length == 0,
                    "Imported L4 is retention-only and cannot be silently replaced by a new clamp-based L1/L2 proposal");
            }
        }
        string one = DenseLiteralL1(stage, "one-frame-imported-l1", 4, 1, true, 0, 1, new byte[] { 2, 7 });
        byte[] bytes = File.ReadAllBytes(one); DenseWrite32(bytes, 0x20, 0); bytes = DenseTrimLiteralEnvelope(bytes, 10); File.WriteAllBytes(one, bytes);
        using (var imported = NodeFactory.FromFile(null, one) as CHR0Node)
        using (var snapshot = NativeAnimationOptimization.Copy(imported))
        {
            string saved = Path.Combine(stage, "one-frame-retained-l1.chr0"); snapshot.ExportChecked(saved);
            var raw = DenseReadCompletePayloads(File.ReadAllBytes(saved)).Single();
            Check(raw.HeaderDuration == 1 && !raw.Loop && raw.Format == 4 && raw.EncodedValues.SequenceEqual(new[] { 2f, 7f }) &&
                NativeAnimationOptimization.ContentKey(snapshot) == NativeAnimationOptimization.ContentKey(imported),
                "Imported one-frame L1 retains its explicit hidden terminal sample through native snapshot/save instead of being treated as discardable fixed storage");
        }
    }

    static string DenseLiteralCompleteL4(string stage, string label, int version, int frames, float[] values)
    {
        if (values.Length != frames + 1) throw new ArgumentException("Literal L4 requires complete N+1 floats.");
        string path = DenseLiteralL1(stage, label, version, frames, true, 0, 1, new byte[values.Length]); var bytes = File.ReadAllBytes(path);
        int group = DenseRead32(bytes, 0x10), entry = group + DenseRead32(bytes, group + 0x18 + 12), curve = entry + DenseRead32(bytes, entry + 8);
        uint code = unchecked((uint)DenseRead32(bytes, entry + 4)); DenseWrite32(bytes, entry + 4, unchecked((int)((code & ~(7u << 27)) | 6u << 27)));
        DenseWrite32(bytes, version == 4 ? 0x20 : 0x24, 0);
        for (int i = 0; i < values.Length; i++) DenseWrite32(bytes, curve + i * 4, ReplayFloatBits(values[i]));
        bytes = DenseTrimLiteralEnvelope(bytes, values.Length * 4); File.WriteAllBytes(path, bytes);
        Check(DenseReadCompletePayloads(bytes).Single().EncodedValues.SequenceEqual(values),
            "Literal L4 " + label + ": complete N+1 float bytes are independently identified without using the new storage factory");
        return path;
    }

    static byte[] DenseTrimLiteralEnvelope(byte[] bytes, int payloadLength)
    {
        int version = DenseRead32(bytes, 8), group = DenseRead32(bytes, 0x10), entry = group + DenseRead32(bytes, group + 0x18 + 12), curve = entry + DenseRead32(bytes, entry + 8);
        int oldStrings = (DenseRead32(bytes, 4) + 3) & ~3, resource = curve + payloadLength, strings = (resource + 3) & ~3, removed = oldStrings - strings;
        if (removed < 0 || oldStrings > bytes.Length) throw new InvalidDataException("Literal payload cannot fit its bounded source envelope.");
        var result = new byte[bytes.Length - removed]; Array.Copy(bytes, 0, result, 0, resource); Array.Copy(bytes, oldStrings, result, strings, bytes.Length - oldStrings);
        DenseWrite32(result, 4, resource);
        foreach (int field in new[] { version == 4 ? 0x14 : 0x18, version == 4 ? 0x18 : 0x1C, group + 0x18 + 8, entry })
        { int previous = DenseRead32(result, field); if (previous != 0) DenseWrite32(result, field, previous - removed); }
        return result;
    }

    static CHR0Node DenseOwnedClip(int frames, bool loop, int version, IndexedCHR0CurveStorage curve)
    {
        var clip = new CHR0Node { Name = "Independent complete dense candidate", Version = version, FrameCount = frames, Loop = loop, OriginalPath = "native metadata retained" };
        var entry = clip.CreateEntry("Anonymous rotation channel"); entry.UseModelScale = entry.UseModelTranslate = true; entry.UseModelRotate = false;
        entry.ScaleCompensateApply = true; entry.ScaleCompensateParent = false; entry.ClassicScaleOff = true;
        entry.Keyframes.SetFrameValue(4, 0, 0, true)._tangent = entry.Keyframes.SetFrameValue(5, 0, 0, true)._tangent = 0;
        var values = curve.Values; var tangents = curve.Tangents;
        for (int i = 0; i < values.Length; i++) entry.Keyframes.SetFrameValue(3, i, values[i], true)._tangent = tangents[i];
        entry.Keyframes.PreserveInterpolation = true; PreciseCHR0EntryNode.Promote(clip); return clip;
    }

    static void NativeDenseProposalChecks(string stage)
    {
        using (var baseline = CompactFixture(12, 3, Enumerable.Range(0, 12).ToArray(), i => .0012345f + i * .0001f, i => 0))
        {
            baseline.Loop = false; var entry = (CHR0EntryNode)baseline.Children[0]; string original = NativeAnimationOptimization.ContentKey(baseline);
            var proposals = NativeCompactAnimationStorage.CreateDenseAlternatives(entry, entry, false, new int[0], 2000000);
            Check(proposals.Any(p => p.Format == AnimDataFormat.L1) && proposals.All(p => p.Group == 1 && p.Curves[0].EncodedSampleCount == 13 && p.AfterBytes < p.BeforeBytes),
                "Dense proposals prepare genuinely smaller L1/L2 rotation candidates for a generic independent low-motion curve");
            foreach (var proposal in proposals)
            {
                var source = entry.KeyArrays[3].Entries.ToArray(); var measured = DenseLinearDifference(source.Select(k => (double)k._index).ToArray(), source.Select(k => (double)k._value).ToArray(),
                    source.Select(k => (double)k._tangent).ToArray(), proposal.Curves[0].EncodedValues);
                Check(measured.MaximumValueError <= .1 && measured.MaximumDerivativeError <= .1 && proposal.Curves[0].EncodedValues.Last() == proposal.Curves[0].Values.Last(),
                    "Dense proposal " + proposal.Format + ": independent analytic extrema verify continuous unwrapped linear value/derivative and the complete non-loop clamped terminal sample");
            }
            Check(NativeCompactAnimationStorage.CreateDenseAlternatives(entry, entry, true, new int[0], 2000000).Length == 0 &&
                NativeCompactAnimationStorage.CreateDenseAlternatives(entry, entry, false, new int[0], 1).Length == 0,
                "Dense proposals cannot approximate protected source/pin groups or exceed the remaining native key budget");
            var mandatory = NativeCompactAnimationStorage.CreateDenseAlternatives(entry, entry, false, new[] { 5 }, 2000000);
            Check(mandatory.All(p => ReplayFloatBits(p.Curves[0].Values[5]) == ReplayFloatBits(entry.KeyArrays[3].GetKeyframe(5)._value) && ReplayFloatBits(p.Curves[0].Tangents[5]) == 0),
                "Dense proposals retain explicitly mandatory values and tangent bits exactly rather than relaxing them with ordinary fitting joins");
            using (var cancellation = new CancellationTokenSource()) { cancellation.Cancel(); bool stopped = false; try { NativeCompactAnimationStorage.CreateDenseAlternatives(entry, entry, false, new int[0], 2000000, cancellation.Token); } catch (OperationCanceledException) { stopped = true; } Check(stopped, "Dense proposal cancellation stops before publication or mutation"); }
            long reserved = 0; var exhausted = new NativeDenseProofBudget(units => { reserved += units; return false; });
            Check(NativeCompactAnimationStorage.CreateDenseAlternatives(entry, entry, false, new int[0], 2000000, CancellationToken.None, exhausted).Length == 0 && exhausted.Exhausted && reserved > 0,
                "Dense proposal proof work reserves from the caller's shared cap and returns no unchecked result when that cap is exhausted");
            using (var stale = NativeAnimationOptimization.Copy(baseline))
            {
                ((CHR0EntryNode)stale.Children[0]).KeyArrays[3].GetKeyframe(3)._value += .001f;
                string changed = NativeAnimationOptimization.ContentKey(stale); bool rejected = false;
                try { NativeCompactAnimationStorage.ApplyGroup(stale, proposals.First()); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && NativeAnimationOptimization.ContentKey(stale) == changed,
                    "Dense proposal Apply rejects a changed coefficient dependency before writing any prepared storage or replacing authored keys");
            }
            using (var candidate = NativeAnimationOptimization.Copy(baseline))
            {
                NativeCompactAnimationStorage.ApplyGroup(candidate, proposals.First()); var channels = new HashSet<string>(StringComparer.Ordinal) { entry.Name + "\0" + 3 };
                using (var projection = NativeCompactAnimationStorage.CreateLinearProjection(NativeAnimationOptimization.Copy(candidate), channels))
                {
                    var projected = (CHR0EntryNode)projection.Children[0]; var values = proposals.First().Curves[0].EncodedValues;
                    Check(Enumerable.Range(0, 12).SelectMany(i => new[] { i + .1732051f, i + .7320508f }).All(t =>
                        Math.Abs(projected.KeyArrays[3].GetFrameValue(t) - DenseSampleLinear(values, t)) < 1e-8),
                        "Dense read-only paired-Hermite projection matches independently sampled linear playback at withheld fractions including the terminal interval");
                }
                Check(PreciseCHR0EntryNode.CaptureStorage((CHR0EntryNode)candidate.Children[0])?[3]?.IsDense == true,
                    "Dense linear verification projection does not mutate or replace the encoded candidate's storage policy");
            }
            Check(NativeAnimationOptimization.ContentKey(baseline) == original, "Dense proposals and separate linear verification preserve every original authored native coefficient");
            entry.KeyArrays[3].GetKeyframe(3)._tangent = NegativeZero();
            Check(NativeCompactAnimationStorage.CreateDenseAlternatives(entry, entry, false, new int[0], 2000000).Length == 0,
                "Dense proposals cannot erase an authored negative-zero tangent on an otherwise eligible increasing curve");
        }
        using (var paired = CompactFixture(12, 3, new[] { 0, 4, 4, 8 }, i => .001f + i * .0001f, i => 0))
        { var entry = (CHR0EntryNode)paired.Children[0]; Check(NativeCompactAnimationStorage.CreateDenseAlternatives(entry, entry, false, new int[0], 2000000).Length == 0, "Dense proposals preserve both paired-key sides instead of smoothing a discontinuity"); }
        var importedCurve = IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1, new[] { 0, 1, 3, 255 }, 3, 0, .01f);
        using (var imported = DenseOwnedClip(3, false, 4, importedCurve))
        {
            var channels = new IndexedCHR0CurveStorage[9]; channels[3] = importedCurve;
            var entry = PreciseCHR0EntryNode.RestoreStorage((CHR0EntryNode)imported.Children[0], new IndexedCHR0EntryStorage(channels));
            Check(NativeCompactAnimationStorage.CreateDenseAlternatives(entry, entry, false, new int[0], 2000000).Length == 0 &&
                PreciseCHR0EntryNode.CaptureStorage(entry)[3].EncodedValues[3] == 2.55f,
                "Dense proposals retain an imported complete dense payload whose authored terminal differs from the native clamped pose");
        }
    }

    static void NativeDenseImportedExactChecks(string modelPath, string stage)
    {
        Directory.CreateDirectory(stage);
        using (var owner = NodeFactory.FromFile(null, modelPath))
        {
            var model = OptimizationWalk(owner).OfType<MDL0Node>().OrderByDescending(m => m.NumVertices).First();
            var bone = model.AllBones.First(b => !b.Children.OfType<MDL0BoneNode>().Any());
            foreach (bool l4 in new[] { false, true })
            {
            var importedCurve = IndexedCHR0CurveStorage.CreateDense(AnimDataFormat.L1, new[] { 0, 0, 0, 255 }, 3, 1, .01f);
            string l4Path = l4 ? DenseLiteralCompleteL4(stage, "noncompact-exact-hidden-l4-terminal", 4, 3, new[] { 1f, 1f, 1f, 7f }) : null;
            using (var imported = l4 ? NodeFactory.FromFile(null, l4Path) as CHR0Node : DenseOwnedClip(3, false, 4, importedCurve))
            {
                var entry = (CHR0EntryNode)imported.Children[0]; entry.Name = bone.Name;
                if (!l4) { var channels = new IndexedCHR0CurveStorage[9]; channels[3] = importedCurve; entry = PreciseCHR0EntryNode.RestoreStorage(entry, new IndexedCHR0EntryStorage(channels)); }
                string original = NativeAnimationOptimization.ContentKey(imported), storage = NativeAnimationOptimization.StorageKey(imported);
                var options = new NativeAnimationOptimizationOptions { ExactOnly = true, LengthScale = NativeAnimationOptimization.GetBindBoundsDiagonal(model), TargetBones = model.AllBones.Select(b => b.Name).ToArray() };
                using (var noChange = NativeAnimationOptimization.Prepare(model, imported, imported.Children.OfType<CHR0EntryNode>().Select(OptimizationTrack).ToArray(), options, stage))
                    Check(!noChange.HasChanges && noChange.BeforeBytes == noChange.AfterBytes,
                        "Imported " + (l4 ? "L4" : "L1") + " default Exact: a genuine no-op preserves complete encoded identity even when native projection does not observe its authored terminal motion");
                var cleanup = imported.Children.OfType<CHR0EntryNode>().Select(OptimizationTrack).ToArray();
                cleanup[0].Channels[3] = new[] { OptimizationKey(0, 1, 0) };
                bool rejected = false;
                try { using (var ignored = NativeAnimationOptimization.Prepare(model, imported, cleanup, options, stage)) { } }
                catch (InvalidDataException error) { rejected = error.Data.Contains("OptimizationDenseRetention"); }
                Check(rejected && NativeAnimationOptimization.ContentKey(imported) == original && NativeAnimationOptimization.StorageKey(imported) == storage &&
                    PreciseCHR0EntryNode.CaptureStorage(entry)[3].EncodedValues.Last() > 3.5f,
                    "Imported " + (l4 ? "L4" : "L1") + " default Exact: native-constant cleanup cannot erase a complete authored terminal payload through the noncompact path, and rejection preserves all input bytes/policies");
            }
            }
        }
    }

    static void NativeDenseEncodedFormatChecks(string stage)
    {
        Directory.CreateDirectory(stage);
        foreach (int version in new[] { 4, 5 }) foreach (bool loop in new[] { false, true })
        {
            // This second fixture set explicitly authors the complete N+1
            // encoded samples. The initial 34 checks remain historical native
            // decoder characterization, not encoded-format qualification.
            string path = DenseLiteralL1(stage, "complete-" + version + "-" + loop, version, 3, true, 0, 1, new byte[] { 0, 1, 3, 7 });
            byte[] bytes = File.ReadAllBytes(path); DenseWrite32(bytes, version == 4 ? 0x20 : 0x24, loop ? 1 : 0); File.WriteAllBytes(path, bytes);
            var raw = DenseReadCompletePayloads(bytes).Single();
            Check(raw.HeaderDuration == 3 && raw.EncodedValues.SequenceEqual(new[] { 0f, 1f, 3f, 7f }) && raw.RequiredPayloadBytes == 12 && raw.Format == 4,
                "Complete L1 v" + version + " / loop=" + loop + ": an independent raw reader consumes all N+1 encoded values, including the deliberate nonzero terminal sample");
            using (var clip = NodeFactory.FromFile(null, path) as CHR0Node)
            {
                var curve = ((CHR0EntryNode)clip.Children[0]).KeyArrays[3];
                Check(curve.KeyCount == (loop ? 4 : 3) && curve.GetFrameValue(3) == (loop ? 7 : 3),
                    "Complete L1 v" + version + " / loop=" + loop + ": full encoded shape is distinguished from the existing native N-or-N+1 decoder projection");
                if (!loop) Check(DenseSampleLinear(raw.EncodedValues, 2.5) == 5 && curve.GetFrameValue(2.5f) == 3,
                    "Complete non-loop L1 v" + version + ": the complete terminal interval can differ from native clamping and must not be hidden by native-only verification");
            }
        }

        var sameIntegers = DenseLinearDifference(new[] { 0d, 1d, 2d }, new[] { 0d, 1d, 3d }, new[] { 0d, 1d, 2d }, new[] { 0f, 1f, 3f, 3f });
        Check(Math.Abs(sameIntegers.MaximumValueError - 4d / 27) < 1e-12 && Math.Abs(sameIntegers.MaximumDerivativeError - 1) < 1e-12,
            "Independent dense proof: identical integer values still have exactly 4/27 Hermite-versus-linear error and one-unit/frame derivative error between knots");
        var badTerminal = DenseLinearDifference(new[] { 0d, 1d, 2d }, new[] { 0d, 1d, 3d }, new[] { 0d, 1d, 2d }, new[] { 0f, 1f, 3f, 7f });
        Check(Math.Abs(badTerminal.MaximumValueError - 4) < 1e-12 && Math.Abs(badTerminal.MaximumDerivativeError - 4) < 1e-12,
            "Independent dense proof: an ignored non-loop terminal sample produces independently known four-unit final-interval value and derivative errors");
        var derivative = DenseLinearDifference(new[] { 0d, 1d }, new[] { 0d, 0d }, new[] { .2d, -.2d }, new[] { 0f, 0f, 0f });
        Check(Math.Abs(derivative.MaximumValueError - .05) < 1e-12 && Math.Abs(derivative.MaximumDerivativeError - .2) < 1e-12,
            "Independent dense proof: a value-safe fractional hump still violates the unchanged Small derivative ceiling");
        var exact = DenseLinearDifference(new[] { 0d, 2d }, new[] { 0d, 2d }, new[] { 1d, 1d }, new[] { 0f, 1f, 2f, 2f });
        Check(exact.MaximumValueError < 1e-12 && exact.MaximumDerivativeError < 1e-12 && exact.Intervals == 3,
            "Independent dense proof: sparse linear Hermite and complete dense linear samples agree exactly, including the clamped final interval");
        File.WriteAllText(Path.Combine(stage, "complete-payload-and-linear-expectations.json"), new JavaScriptSerializer().Serialize(new {
            CompleteEncodedSamples = "N+1 regardless loop", NativeDecodedSamples = "N non-loop / N+1 loop, preserved historical behavior",
            IndependentAnalyticPowerBasisExtrema = true, SameIntegerSamples = sameIntegers, WrongTerminal = badTerminal, DerivativeOnly = derivative,
            EncodingShapeQualified = true, ActualGameExecutionVerified = false }));
    }

    sealed class DenseRawPayload
    {
        public string Bone; public int Channel, Format, HeaderDuration, NativeDecodedCount, RequiredPayloadBytes, Offset, ResourceBytes;
        public bool Loop; public float[] EncodedValues; public int[] EncodedIntegers;
    }

    static DenseRawPayload[] DenseReadCompletePayloads(byte[] bytes)
    {
        int version = DenseRead32(bytes, 8), countAt = version == 4 ? 0x1C : version == 5 ? 0x20 : throw new InvalidDataException("Complete dense reader requires CHR0 v4/v5.");
        if (bytes.Length < countAt + 12 || bytes[0] != 'C' || bytes[1] != 'H' || bytes[2] != 'R' || bytes[3] != '0') throw new InvalidDataException("Complete dense reader requires a bounded CHR0 header.");
        int duration = bytes[countAt] << 8 | bytes[countAt + 1], resource = DenseRead32(bytes, 4), group = DenseRead32(bytes, 0x10), rows = DenseRead32(bytes, group + 4);
        bool loop = DenseRead32(bytes, countAt + 4) != 0;
        if (duration < 1 || resource < countAt + 12 || resource > bytes.Length || rows < 0 || rows > 65536) throw new InvalidDataException("Complete dense reader rejected bounded header counts.");
        var result = new List<DenseRawPayload>(); var pointers = new List<int>();
        for (int row = 0; row < rows; row++)
        {
            int entry = checked(group + DenseRead32(bytes, checked(group + 0x18 + row * 16 + 12)));
            uint code = unchecked((uint)DenseRead32(bytes, entry + 4)); int column = entry + 8;
            int nameAt = checked(entry + DenseRead32(bytes, entry)), end = nameAt;
            if (nameAt < 0 || nameAt >= bytes.Length) throw new InvalidDataException("Complete dense reader rejected a resource name pointer.");
            while (end < bytes.Length && bytes[end] != 0) end++;
            if (end == bytes.Length) throw new InvalidDataException("Complete dense reader rejected an unterminated resource name.");
            string name = System.Text.Encoding.UTF8.GetString(bytes, nameAt, end - nameAt);
            for (int component = 0; component < 3; component++)
            {
                if ((code & 1u << (22 + component)) == 0) continue;
                int format = (int)(code >> (component == 0 ? 25 : component == 1 ? 27 : 30) & (component == 1 ? 7u : 3u));
                int axes = (code & 1u << (4 + component)) != 0 ? 1 : 3;
                for (int axis = 0; axis < axes; axis++, column += 4)
                {
                    int channel = component * 3 + axis;
                    if ((code & 1u << (13 + channel)) != 0) { DenseRead32(bytes, column); continue; }
                    int at = checked(entry + DenseRead32(bytes, column)); pointers.Add(at);
                    if (format != 4 && format != 5 && format != 6) continue;
                    if (component != 1) throw new InvalidDataException("A dense codec was written to an illegal native scale/translation format field.");
                    int count = duration + 1, stride = format == 4 ? 1 : format == 5 ? 2 : 4, header = format == 6 ? 0 : 8, size = checked(header + count * stride);
                    if (at < 0 || at > resource - size) throw new InvalidDataException("Complete N+1 dense payload exceeds the resource boundary.");
                    float step = format == 6 ? 1 : BitConverter.ToSingle(BitConverter.GetBytes(DenseRead32(bytes, at)), 0), valueBase = format == 6 ? 0 : BitConverter.ToSingle(BitConverter.GetBytes(DenseRead32(bytes, at + 4)), 0);
                    if (float.IsNaN(step) || float.IsInfinity(step) || float.IsNaN(valueBase) || float.IsInfinity(valueBase)) throw new InvalidDataException("Complete dense payload has nonfinite quantization metadata.");
                    var quantized = Enumerable.Range(0, count).Select(i => stride == 1 ? bytes[at + 8 + i] : stride == 2 ? bytes[at + 8 + i * 2] << 8 | bytes[at + 9 + i * 2] : DenseRead32(bytes, at + i * 4)).ToArray();
                    var values = quantized.Select(q => format == 6 ? BitConverter.ToSingle(BitConverter.GetBytes(q), 0) : valueBase + q * step).ToArray();
                    if (values.Any(v => float.IsNaN(v) || float.IsInfinity(v))) throw new InvalidDataException("Complete dense payload decodes a nonfinite sample.");
                    result.Add(new DenseRawPayload { Bone = name, Channel = channel, Format = format, HeaderDuration = duration, NativeDecodedCount = duration + (loop ? 1 : 0),
                        RequiredPayloadBytes = size, Offset = at, ResourceBytes = resource, Loop = loop, EncodedValues = values, EncodedIntegers = quantized });
                }
            }
        }
        foreach (var curve in result)
        {
            int next = pointers.Where(p => p > curve.Offset).DefaultIfEmpty(resource).Min();
            if (curve.Offset + curve.RequiredPayloadBytes > next) throw new InvalidDataException("Complete N+1 dense payload overlaps another native curve.");
        }
        return result.ToArray();
    }

    static double DenseSampleLinear(float[] values, double time)
    {
        if (time <= 0) return values[0]; if (time >= values.Length - 1) return values[values.Length - 1];
        int first = (int)Math.Floor(time); return values[first] + (values[first + 1] - (double)values[first]) * (time - first);
    }

    sealed class DenseAnalyticBounds { public double MaximumValueError, MaximumDerivativeError; public int Intervals; public double[] Witnesses; }
    // Independent power-basis extrema, deliberately separate from the product's
    // normalized rational Bernstein certificate and its subdivision strategy.
    static DenseAnalyticBounds DenseLinearDifference(double[] frames, double[] values, double[] slopes, float[] dense)
    {
        int duration = dense.Length - 1; var result = new DenseAnalyticBounds(); var witnesses = new SortedSet<double>();
        var breaks = frames.Concat(Enumerable.Range(0, duration + 1).Select(i => (double)i)).Where(f => f >= 0 && f <= duration).Distinct().OrderBy(f => f).ToArray();
        for (int interval = 1; interval < breaks.Length; interval++)
        {
            double first = breaks[interval - 1], last = breaks[interval], dt = last - first, middle = (first + last) / 2;
            int right = Array.FindIndex(frames, f => f > middle); double y0, y1, m0, m1;
            if (middle < frames[0]) { y0 = y1 = values[0]; m0 = m1 = 0; }
            else if (right < 0) { y0 = y1 = values[values.Length - 1]; m0 = m1 = 0; }
            else
            {
                int left = right - 1; double span = frames[right] - frames[left];
                double a = 2 * (values[left] - values[right]) + span * (slopes[left] + slopes[right]);
                double b = 3 * (values[right] - values[left]) - span * (2 * slopes[left] + slopes[right]);
                double c = span * slopes[left], d = values[left];
                double Value(double time) { double u = (time - frames[left]) / span; return ((a * u + b) * u + c) * u + d; }
                double Derivative(double time) { double u = (time - frames[left]) / span; return ((3 * a * u + 2 * b) * u + c) / span; }
                y0 = Value(first); y1 = Value(last); m0 = Derivative(first); m1 = Derivative(last);
            }
            double p3 = 2 * (y0 - y1) + dt * (m0 + m1), p2 = 3 * (y1 - y0) - dt * (2 * m0 + m1);
            double p1 = dt * m0 - (DenseSampleLinear(dense, last) - DenseSampleLinear(dense, first)), p0 = y0 - DenseSampleLinear(dense, first);
            var probes = new List<double> { 0, 1 };
            if (Math.Abs(p3) > 1e-30)
            {
                double discriminant = 4 * p2 * p2 - 12 * p3 * p1;
                if (discriminant >= 0) { probes.Add((-2 * p2 + Math.Sqrt(discriminant)) / (6 * p3)); probes.Add((-2 * p2 - Math.Sqrt(discriminant)) / (6 * p3)); }
                probes.Add(-p2 / (3 * p3));
            }
            else if (Math.Abs(p2) > 1e-30) probes.Add(-p1 / (2 * p2));
            foreach (double u in probes.Where(u => u >= 0 && u <= 1))
            {
                result.MaximumValueError = Math.Max(result.MaximumValueError, Math.Abs(((p3 * u + p2) * u + p1) * u + p0));
                result.MaximumDerivativeError = Math.Max(result.MaximumDerivativeError, Math.Abs((3 * p3 * u * u + 2 * p2 * u + p1) / dt)); witnesses.Add(first + dt * u);
            }
            result.Intervals++;
        }
        result.Witnesses = witnesses.ToArray(); return result;
    }

    // These fixtures patch literal L1 header/sample bytes into an independently
    // encoded CHR0 envelope. They do not call the proposed dense-storage writer
    // or its tangent generator, so reader expectations are frozen separately.
    static void NativeDenseStorageChecks(string stage)
    {
        Directory.CreateDirectory(stage);
        var formatBits = new AnimationCode();
        formatBits.SetFormat(0, AnimDataFormat.L1); formatBits.SetFormat(1, AnimDataFormat.L1); formatBits.SetFormat(2, AnimDataFormat.L1);
        Check(formatBits.GetFormat(0) != AnimDataFormat.L1 && formatBits.GetFormat(1) == AnimDataFormat.L1 && formatBits.GetFormat(2) != AnimDataFormat.L1,
            "Literal L1: only the native rotation format field can represent dense codecs; scale/translation cannot be given a truncated L1 flag");
        foreach (int version in new[] { 4, 5 })
        {
            string path = DenseLiteralL1(stage, "literal-three-v" + version, version, 3, false, 0, 1, new byte[] { 0, 1, 3 });
            using (var clip = NodeFactory.FromFile(null, path) as CHR0Node)
            {
                var entry = (CHR0EntryNode)clip.Children[0]; var curve = entry.KeyArrays[3];
                Check(clip.Version == version && clip.FrameCount == 3 && !clip.Loop && curve.FrameLimit == 3 &&
                    curve.Entries.Select(k => k._index).SequenceEqual(new[] { 0, 1, 2 }) &&
                    curve.Entries.Select(k => k._value).SequenceEqual(new[] { 0f, 1f, 3f }) &&
                    curve.Entries.Select(k => k._tangent).SequenceEqual(new[] { 0f, 1f, 2f }),
                    "Literal L1 v" + version + ": native decode expands exact integer samples with first-zero and backward-slope tangents");
                Check(Math.Abs(curve.GetFrameValue(.5f) - .375f) < 1e-7 && Math.Abs(curve.GetFrameValue(1.5f) - 1.875f) < 1e-7 &&
                    Math.Abs(curve.GetFrameValue(.5f) - .5f) > .1f && Math.Abs(curve.GetFrameValue(1.5f) - 2f) > .1f,
                    "Literal L1 v" + version + ": frozen fractional expectations prove native Hermite sampling differs from merely connecting stored samples linearly");
                Check(entry.KeyArrays[0].KeyCount == 0 && entry.KeyArrays[1].KeyCount == 0 && entry.KeyArrays[2].KeyCount == 0 &&
                    entry.KeyArrays[4].KeyCount == 1 && entry.KeyArrays[5].KeyCount == 1 &&
                    Enumerable.Range(6, 3).All(c => entry.KeyArrays[c].KeyCount == 0) &&
                    entry.UseModelScale && !entry.UseModelRotate && entry.UseModelTranslate && entry.ScaleCompensateApply &&
                    !entry.ScaleCompensateParent && entry.ClassicScaleOff,
                    "Literal L1 v" + version + ": sparse/fixed axes and all six native semantic flags remain separate from the varying rotation codec");
                Check(clip.Name == "Independent compact storage fixture" && clip.OriginalPath == "original native metadata" &&
                    curve.GetFrameValue(2.9999f) == 3,
                    "Literal L1 v" + version + ": native metadata and non-loop final held interval survive independently of fractional interpolation");
            }
        }

        string rounded = DenseLiteralL1(stage, "rounded-slopes", 4, 3, false, 0, .0004f, new byte[] { 0, 1, 3 });
        using (var clip = NodeFactory.FromFile(null, rounded) as CHR0Node)
        {
            var curve = ((CHR0EntryNode)clip.Children[0]).KeyArrays[3];
            Check(ReplayFloatBits(curve.GetKeyframe(1)._tangent) == ReplayFloatBits(0) &&
                ReplayFloatBits(curve.GetKeyframe(2)._tangent) == ReplayFloatBits(.001f),
                "Literal L1: sequential decoded slope generation rounds to three decimal places, independently of the value quantization step");
            // Span 0..1 has two zero tangents: smoothstep(.25) * .0004.
            Check(Math.Abs(curve.GetFrameValue(.25f) - .0000625f) < 1e-10,
                "Literal L1: a withheld quarter-frame has the independently calculated rounded-tangent Hermite value");
        }

        foreach (bool loop in new[] { false, true }) foreach (int frames in new[] { 1, 2 })
        {
            byte[] samples = Enumerable.Range(0, frames + (loop ? 1 : 0)).Select(i => (byte)(2 + i * 3)).ToArray();
            string path = DenseLiteralL1(stage, "tiny-" + frames + "-" + loop, 4, frames, loop, 0, 1, samples);
            using (var clip = NodeFactory.FromFile(null, path) as CHR0Node)
            {
                var curve = ((CHR0EntryNode)clip.Children[0]).KeyArrays[3];
                Check(clip.FrameCount == frames && clip.Loop == loop && curve.FrameLimit == samples.Length && curve.KeyCount == samples.Length &&
                    curve.Entries.Select(k => k._value).SequenceEqual(samples.Select(v => (float)v)),
                    "Literal L1: " + frames + "-frame " + (loop ? "loop" : "non-loop") + " payload count follows native N plus its stored closing sample only when looping");
                if (loop)
                {
                    Check(curve.GetFrameValue(frames) == samples[frames] && curve.GetFrameValue(frames) != curve.GetFrameValue(0),
                        "Literal L1: " + frames + "-frame stored closing pose is preserved and is not silently replaced by the first pose");
                    if (frames == 1) Check(Math.Abs(curve.GetFrameValue(.5f) - 3.125f) < 1e-7,
                        "Literal L1: one-frame loop's explicit closing sample retains its independently expected Hermite half-frame");
                    else Check(Math.Abs(curve.GetFrameValue(1.5f) - 6.5f) < 1e-7,
                        "Literal L1: two-frame loop's closing interval retains its independently expected Hermite half-frame");
                }
                else Check(curve.GetFrameValue(frames - .0001f) == samples.Last(),
                    "Literal L1: " + frames + "-frame non-loop keeps the ordinary native held end interval");
            }
        }

        string turns = DenseLiteralL1(stage, "unwrapped-turns", 5, 3, false, 0, 360, new byte[] { 0, 1, 2 });
        using (var clip = NodeFactory.FromFile(null, turns) as CHR0Node)
        {
            var curve = ((CHR0EntryNode)clip.Children[0]).KeyArrays[3];
            Check(curve.GetFrameValue(2) == 720 && Math.Abs(curve.GetFrameValue(.5f) - 135) < 1e-6 && Math.Abs(curve.GetFrameValue(1.5f) - 540) < 1e-6,
                "Literal L1 retains raw 720-degree winding and native subframe motion instead of quaternion-shortest-path wrapping");
        }
        string minusZero = DenseLiteralL1(stage, "zero-value-arithmetic", 4, 2, false, NegativeZero(), 0, new byte[] { 0, 1 });
        using (var clip = NodeFactory.FromFile(null, minusZero) as CHR0Node)
            Check(((CHR0EntryNode)clip.Children[0]).KeyArrays[3].Entries.All(k => ReplayFloatBits(k._value) == 0),
                "Literal L1: base minus-zero plus positive-zero sample arithmetic decodes +zero, so coefficient sign cannot be assumed from its header");
        string minusSlope = DenseLiteralL1(stage, "negative-zero-slope", 4, 2, false, 1, .0001f, new byte[] { 1, 0 });
        using (var clip = NodeFactory.FromFile(null, minusSlope) as CHR0Node)
            Check(ReplayFloatBits(((CHR0EntryNode)clip.Children[0]).KeyArrays[3].GetKeyframe(1)._tangent) == int.MinValue,
                "Literal L1: rounded negative slopes produce an observable negative-zero tangent which an immutable storage snapshot must retain");

        File.WriteAllText(Path.Combine(stage, "literal-l1-expectations.json"), new JavaScriptSerializer().Serialize(new {
            IndependentRawPayload = true, L1RotationOnly = true, NativeFractionalInterpolation = "Hermite after sequential three-decimal backward slopes",
            ThreeSamples = new[] { 0, 1, 3 }, NativeTangents = new[] { 0, 1, 2 }, AtHalf = .375, AtOneAndHalf = 1.875,
            LoopSampleCount = "N+1, including explicit stored closing sample", OriginalSourceBytesMutable = false,
            LiteralFixturePadding = "Existing precise envelope retained only for bounded raw-reader fixtures; no filesize saving is claimed for hand-patched envelopes.",
            ActualGameInterpolationVerified = false }));
    }

    static string DenseLiteralL1(string stage, string label, int version, int frames, bool loop, float valueBase, float valueStep, byte[] samples)
    {
        if (samples.Length != frames + (loop ? 1 : 0) || samples.Length < 1) throw new ArgumentException("Literal L1 sample count does not match its frozen native timing.");
        int reserve = Math.Max(2, samples.Length);
        string path = Path.Combine(stage, label + ".chr0");
        using (var seed = CompactFixture(reserve, 3, Enumerable.Range(0, reserve).ToArray(), i => i, i => 0))
        {
            seed.Version = version;
            var entry = (CHR0EntryNode)seed.Children[0]; entry.UseModelScale = entry.UseModelTranslate = true; entry.UseModelRotate = false;
            entry.ScaleCompensateApply = true; entry.ScaleCompensateParent = false; entry.ClassicScaleOff = true;
            seed.ExportChecked(path);
        }
        byte[] bytes = File.ReadAllBytes(path);
        int group = DenseRead32(bytes, 0x10);
        if (DenseRead32(bytes, group + 4) != 1) throw new InvalidDataException("The literal fixture needs exactly one independently encoded entry.");
        int entryOffset = checked(group + DenseRead32(bytes, group + 0x18 + 12));
        uint code = unchecked((uint)DenseRead32(bytes, entryOffset + 4));
        if ((code & (1u << 22 | 1u << 24)) != 0 || (code & 1u << 23) == 0 || (code & 1u << 16) != 0)
            throw new InvalidDataException("The literal fixture requires absent S/T groups and a varying first rotation axis.");
        int curve = checked(entryOffset + DenseRead32(bytes, entryOffset + 8));
        int reservedBytes = checked(8 + reserve * 12), payloadBytes = (8 + samples.Length + 3) & ~3;
        if (curve < 0 || curve > bytes.Length - reservedBytes || payloadBytes > reservedBytes) throw new InvalidDataException("Literal L1 payload escaped its independently allocated envelope.");
        DenseWrite32(bytes, entryOffset + 4, unchecked((int)((code & ~(7u << 27)) | 4u << 27)));
        int countAt = version == 4 ? 0x1C : 0x20; bytes[countAt] = (byte)(frames >> 8); bytes[countAt + 1] = (byte)frames;
        DenseWrite32(bytes, version == 4 ? 0x20 : 0x24, loop ? 1 : 0);
        Array.Clear(bytes, curve, reservedBytes); DenseWrite32(bytes, curve, ReplayFloatBits(valueStep)); DenseWrite32(bytes, curve + 4, ReplayFloatBits(valueBase));
        Array.Copy(samples, 0, bytes, curve + 8, samples.Length); File.WriteAllBytes(path, bytes);
        Check((DenseRead32(bytes, entryOffset + 4) >> 27 & 7) == 4 && payloadBytes == ((8 + frames + (loop ? 1 : 0) + 3) & ~3),
            "Literal L1 " + label + ": hand-written rotation format/header/sample payload and four-byte alignment are independently identified");
        return path;
    }

    static int DenseRead32(byte[] bytes, int at)
    {
        if (at < 0 || at > bytes.Length - 4) throw new InvalidDataException("Literal fixture pointer is outside its byte buffer.");
        return unchecked((int)((uint)bytes[at] << 24 | (uint)bytes[at + 1] << 16 | (uint)bytes[at + 2] << 8 | bytes[at + 3]));
    }
    static void DenseWrite32(byte[] bytes, int at, int value)
    { DenseRead32(bytes, at); bytes[at] = (byte)(value >> 24); bytes[at + 1] = (byte)(value >> 16); bytes[at + 2] = (byte)(value >> 8); bytes[at + 3] = (byte)value; }
}
