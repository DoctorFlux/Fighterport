using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

internal static partial class Program
{
    static void NativeDenseCodecChecks(CHR0Node source, string sourceFile, string stage)
    {
        Directory.CreateDirectory(stage);
        var raw = DenseReadCompletePayloads(File.ReadAllBytes(sourceFile));
        var tracks = source.Children.OfType<CHR0EntryNode>().ToDictionary(e => e.Name, StringComparer.Ordinal);
        string hash = FighterProject.HashFile(sourceFile), content = NativeAnimationOptimization.SourceContentKey(source);
        double maximum = 0; int samples = 0, retained = 0;
        foreach (var payload in raw)
        {
            var entry = tracks[payload.Bone]; var array = entry.KeyArrays[payload.Channel];
            var storage = PreciseCHR0EntryNode.CaptureStorage(entry)?[payload.Channel];
            if (storage != null && storage.IsDense && storage.Matches(array)) retained++;
            bool exactSamples = true;
            for (int frame = 0; frame < array.FrameLimit; frame++)
            {
                exactSamples &= ReplayFloatBits(array.GetFrameValue(frame)) == ReplayFloatBits(payload.EncodedValues[frame]);
                if (frame == array.FrameLimit - 1) continue;
                foreach (float fraction in new[] { .125f, .25f, .375f, .5f, .625f, .75f, .875f })
                {
                    float expected = payload.EncodedValues[frame] + fraction * (payload.EncodedValues[frame + 1] - payload.EncodedValues[frame]);
                    maximum = Math.Max(maximum, Math.Abs((double)array.GetFrameValue(frame + fraction) - expected)); samples++;
                }
            }
            Check(exactSamples, "Codec: exact original dense sample bits / " + payload.Bone + " / " + payload.Channel);
        }
        Check(retained == raw.Length, "Codec: every complete original dense channel passes exact bounded storage capture; no reconstructed-tangent rejection");
        Check(maximum <= .0001, "Codec: all independent fractional dense scalar witnesses agree within native floating-point roundoff");
        string path = Path.Combine(stage, "source-snapshot.chr0");
        using (var copy = NativeAnimationOptimization.CopySource(source)) copy.ExportChecked(path);
        using (var reopened = NodeFactory.FromFile(null, path) as CHR0Node)
            Check(reopened != null && NativeAnimationOptimization.SourceContentKey(reopened) == content,
                "Codec: independent source snapshot preserves native evaluation coefficients and complete metadata through reopen");
        Check(FighterProject.HashFile(sourceFile) == hash && NativeAnimationOptimization.SourceContentKey(source) == content,
            "Codec: original bytes and live source data remain unchanged");
        File.WriteAllText(Path.Combine(stage, "codec.json"), new JavaScriptSerializer().Serialize(new {
            DenseChannels = raw.Length, CapturedDenseChannels = retained, FractionalSamples = samples,
            MaximumScalarRoundoff = maximum, OriginalBytes = new FileInfo(sourceFile).Length,
            SourceSnapshotBytes = new FileInfo(path).Length,
            FramePolicy = "Existing visible native interval; complete encoded terminal retained separately",
            ActualGameExecutionVerified = false }));
    }
}
