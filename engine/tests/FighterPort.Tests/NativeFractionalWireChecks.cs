using FighterPort.Adapters.Brawl;
using FighterPort.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class NativeFractionalWireChecks
{
    public static void Run(Action<bool, string> check)
    {
        var options = new JsonSerializerOptions(ProjectStore.Json);
        var converterType = typeof(BrawlAdapter).Assembly.GetType("FighterPort.Adapters.Brawl.NativeKeyJsonConverter", true)!;
        options.Converters.Insert(0, (JsonConverter)Activator.CreateInstance(converterType, nonPublic: true)!);
        // This is the legacy x86 serializer's decimal spelling, rather than the
        // exact widened binary single. The adapter must reconstruct the single.
        var fractional = JsonSerializer.Deserialize<Key>("{\"Frame\":0.142857149,\"Value\":2,\"InTangent\":3,\"OutTangent\":3}", options)!;
        check(fractional.Frame == (double)(1f / 7) && fractional.Value == 2 && fractional.InTangent == 3,
            "Native fractional wire reconstructs the authentic single frame from legacy decimal JSON before widening to Core double time");
        foreach (string json in new[]
        {
            "{\"Frame\":0,\"FrameBits\":-2147483648,\"Value\":0}",
            "{\"FrameBits\":-2147483648,\"Frame\":0,\"Value\":0}"
        })
        {
            var signed = JsonSerializer.Deserialize<Key>(json, options)!;
            check(BitConverter.DoubleToInt64Bits(signed.Frame) == long.MinValue,
                "Native fractional wire retains signed-zero knot time independently of JSON field order");
        }
        var integer = JsonSerializer.Deserialize<Key>("{\"Frame\":17,\"Value\":0,\"ValueBits\":-2147483648,\"InTangent\":2,\"OutTangent\":2}", options)!;
        check(integer.Frame == 17 && BitConverter.SingleToInt32Bits(integer.Value) == int.MinValue && integer.OutTangent == 2,
            "Native fractional wire remains compatible with original integer-time responses and exact scalar bit payloads");
        foreach (float time in new[] { 0f, 1f / 7, 1023.96875f, BitConverter.Int32BitsToSingle(int.MinValue) })
        {
            var edit = new NativeEdit("reviewed bone", 3, time, 12, -.5f);
            using var wire = JsonDocument.Parse(JsonSerializer.Serialize(edit, ProjectStore.Json));
            check(wire.RootElement.GetProperty("FrameBits").GetInt32() == BitConverter.SingleToInt32Bits(time),
                "Native fractional edits carry exact native frame bits without changing integer identities / " + time);
        }
        var keys = new[] { 0f, .125f, .5f, 1f, 1.5f, 2f, 2.5f }.Select(time => new Key(time, 2 * time, 2, 2)).ToArray();
        var channels = new Curve?[9]; channels[3] = new Curve(keys);
        var clip = new AnimationClip(Guid.NewGuid(), "dyadic fractional line", 4, new(60), false,
            [new BoneTrack("existing native bone", channels)]);
        var reduced = AnimationOptimization.Optimize(clip, ["existing native bone"]);
        var result = reduced.Tracks.Single().Channels[3]!;
        double error = Enumerable.Range(0, 101).Max(i => Math.Abs(result.Sample(i * 2.5 / 100) - i * 5.0 / 100));
        check(result.Keys.Length == 2 && result.Keys[0].Frame == 0 && result.Keys[^1].Frame == 2.5 &&
            reduced.Report.Channels.Single().CertifiedValueErrorBound == 0 && error < .00001,
            "Exact Core Hermite cleanup certifies native fractional-time knots without rounding their endpoint or interval lengths; " +
            "separate native scalar roundoff=" + error + "; keys=" + result.Keys.Length);

        double signedTime = BitConverter.Int64BitsToDouble(long.MinValue);
        var signedChannels = new Curve?[9]; signedChannels[3] = new Curve([new(signedTime, 0), new(1, 0), new(2, 0)]);
        var signedClip = clip with { Tracks = [new BoneTrack("existing native bone", signedChannels)] };
        var signedSettings = new AnimationOptimizationSettings { BindPoses = [new("existing native bone", [1,1,1,0,0,0,0,0,0])] };
        var retained = AnimationOptimization.Optimize(signedClip, ["existing native bone"], signedSettings);
        var signedResult = retained.Tracks.Single().Channels[3]!;
        check(BitConverter.DoubleToInt64Bits(signedResult.Keys[0].Frame) == long.MinValue && signedResult.Keys.Length >= 2,
            "A negative-zero native time remains structural evidence; constant and bind cleanup cannot replace or remove its authored knot");
        var changedChannels = (Curve?[])signedChannels.Clone();
        changedChannels[3] = new Curve(signedChannels[3]!.Keys.Select(k => k with { Frame = k.Frame == 0 ? 0d : k.Frame }).ToArray());
        var changed = new BoneTrack("existing native bone", changedChannels);
        check(!AnimationOptimization.CertifyCandidate(signedClip, [changed], signedSettings).Accepted &&
            AnimationOptimization.CertifyCandidate(signedClip, retained.Tracks, signedSettings).Accepted,
            "Independent scalar certification rejects changing only the signed-zero time bit and accepts its retained original metadata");
    }
}
