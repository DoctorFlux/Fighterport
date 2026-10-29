using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // This route only invokes the native reader. It never exports, rebuilds,
    // promotes, poses, optimizes or changes the input asset. The independent
    // Python parser is deliberately absent from this coefficient oracle.
    static Task NativeChr0Forensics(string[] args, string output)
    {
        if (!PrivateDesktop) throw new InvalidOperationException("Native CHR0 forensics requires the private desktop.");
        string model = Path.GetFullPath(args[0]), motion = Path.GetFullPath(args[1]);
        string modelBefore = FighterProject.HashFile(model), motionBefore = FighterProject.HashFile(motion);
        var json = new JavaScriptSerializer { MaxJsonLength = 128 * 1024 * 1024, RecursionLimit = 100 };
        string first, second, backingBefore, backingAfter; int entries, keys, retained;
        using (var clip = NodeFactory.FromFile(null, motion) as CHR0Node)
        {
            if (clip == null) throw new InvalidDataException("Forensics Motion must be a direct standalone native CHR0 file.");
            backingBefore = Chr0ForensicBackingHash(clip);
            first = json.Serialize(Chr0ForensicProjection(clip));
            backingAfter = Chr0ForensicBackingHash(clip);
            entries = clip.Children.Count;
            keys = clip.Children.OfType<CHR0EntryNode>().Sum(entry => entry.KeyArrays.Sum(array => array.KeyCount));
            retained = clip.Children.OfType<CHR0EntryNode>().Sum(entry => PreciseCHR0EntryNode.CaptureStorage(entry)?.CurveCount ?? 0);
            Check(backingAfter == backingBefore && !clip.IsDirty && clip.Children.All(entry => !entry.IsDirty),
                "Native CHR0 forensics reads complete curves and storage metadata without changing resource bytes or dirty state");
            Check(clip.Children.OfType<CHR0EntryNode>().All(entry => entry.KeyArrays.Length == 9 &&
                entry.KeyArrays.All(array => array.KeyCount == array.Entries.Count())),
                "Native CHR0 forensics records all nine native arrays with every ordered key side, including sampled projections and isotropic aliases");
        }
        using (var clip = NodeFactory.FromFile(null, motion) as CHR0Node)
        {
            if (clip == null) throw new InvalidDataException("The second fresh native CHR0 read failed.");
            second = json.Serialize(Chr0ForensicProjection(clip));
        }
        string modelAfter = FighterProject.HashFile(model), motionAfter = FighterProject.HashFile(motion);
        Check(first == second, "Two independent fresh native CHR0 owners produce identical deterministic coefficient bits, semantic flags and storage metadata");
        Check(modelBefore == modelAfter && motionBefore == motionAfter,
            "Native CHR0 forensics preserves exact original model-context and animation file hashes without serialization");
        // Keep the decoded payload directly addressable for a bit-for-bit
        // comparison. JSON integer uint values preserve every IEEE coefficient,
        // including signed zero and non-finite payloads, without float formatting.
        File.WriteAllText(Path.Combine(output, "native-chr0-forensics.json"), "{" +
            "\"ReadOnly\":true,\"NativeReader\":\"NodeFactory/CHR0Node.KeyArrays\"," +
            "\"WriterUsed\":false,\"ModelLoadedOrPosed\":false,\"PrivateDesktop\":true," +
            "\"OSInputInjected\":false,\"ScreenCopied\":false," +
            "\"Runtime\":" + json.Serialize(new { ClrVersion = Environment.Version.ToString(),
                PointerBytes = IntPtr.Size, Is64BitProcess = Environment.Is64BitProcess,
                NativeAssemblySha256 = FighterProject.HashFile(typeof(CHR0Node).Assembly.Location),
                ArithmeticInterpretation = "Observed decoded bits; no universal runtime arithmetic assumption" }) + "," +
            "\"Model\":" + json.Serialize(new { Path = model, Sha256Before = modelBefore, Sha256After = modelAfter }) + "," +
            "\"Motion\":" + json.Serialize(new { Path = motion, FileBytes = new FileInfo(motion).Length,
                Sha256Before = motionBefore, Sha256After = motionAfter, BackingSha256Before = backingBefore, BackingSha256After = backingAfter }) + "," +
            "\"FreshReadsIdentical\":true,\"Projection\":" + first + "}");
        Console.WriteLine("FORENSICS " + entries + " entries / " + keys + " native scalar keys / " + retained + " retained compact scalar plans");
        return Task.CompletedTask;
    }

    static object Chr0ForensicProjection(CHR0Node clip)
    {
        string[] channelNames = { "SX", "SY", "SZ", "RX", "RY", "RZ", "TX", "TY", "TZ" };
        return new {
            clip.Name, clip.Version, Frames = clip.FrameCount, clip.Loop, clip.OriginalPath,
            BackingLength = clip.WorkingUncompressed.Length,
            Entries = clip.Children.OfType<CHR0EntryNode>().Select((entry, index) => {
                var flags = typeof(CHR0EntryNode).GetField("_code", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new NotSupportedException("Pinned native CHR0 flags field is unavailable.");
                var code = (AnimationCode)flags.GetValue(entry); var storage = PreciseCHR0EntryNode.CaptureStorage(entry);
                return new {
                    Index = index, entry.Name, RawFlags = (uint)code, RawFlagsHex = ((uint)code).ToString("x8", CultureInfo.InvariantCulture),
                    entry.UseModelScale, entry.UseModelRotate, entry.UseModelTranslate,
                    entry.ScaleCompensateApply, entry.ScaleCompensateParent, entry.ClassicScaleOff,
                    entry.Keyframes.PreserveInterpolation, entry.Keyframes.FrameLimit, entry.Keyframes.Loop,
                    StorageCaptureAvailable = storage != null, StorageContentKey = storage?.ContentKey,
                    Groups = Enumerable.Range(0, 3).Select(group => new {
                        Index = group, Name = new[] { "Scale", "Rotation", "Translation" }[group],
                        Exists = code.GetExists(group), Isotropic = code.GetIsIsotropic(group),
                        Format = code.GetFormat(group).ToString(), FormatCode = (int)code.GetFormat(group)
                    }).ToArray(),
                    Channels = Enumerable.Range(0, 9).Select(channel => {
                        var array = entry.KeyArrays[channel]; var retained = storage?[channel];
                        return new {
                            Index = channel, Name = channelNames[channel], GroupExists = code.GetExists(channel / 3),
                            FixedFlag = code.GetIsFixed(channel), array.FrameLimit, array.Loop, array.KeyCount,
                            Rows = array.Entries.Select((key, row) => new {
                                Index = row, TimeBits = Chr0ForensicBits(key._index), ValueBits = Chr0ForensicBits(key._value), TangentBits = Chr0ForensicBits(key._tangent),
                                TimeHex = Chr0ForensicHex(key._index), ValueHex = Chr0ForensicHex(key._value), TangentHex = Chr0ForensicHex(key._tangent)
                            }).ToArray(),
                            RetainedStorage = retained == null ? null : new {
                                Format = retained.Format.ToString(), FormatCode = (int)retained.Format, retained.IsDense,
                                retained.KeyCount, retained.NativeSampleCount, retained.EncodedSampleCount, retained.ByteCount, retained.ContentKey,
                                BaseBits = Chr0ForensicBits(retained.Base), StepBits = Chr0ForensicBits(retained.Step), FrameScaleBits = Chr0ForensicBits(retained.FrameScale),
                                MatchesNativeCurve = retained.Matches(array),
                                EncodedSampleValueBits = retained.IsDense ? retained.EncodedValues.Select(Chr0ForensicBits).ToArray() : null,
                                TerminalSampleValueBits = retained.IsDense ? (uint?)Chr0ForensicBits(retained.EncodedValues.Last()) : null,
                                TerminalSampleProjected = retained.IsDense ? (bool?)(retained.NativeSampleCount == retained.EncodedSampleCount) : null
                            }
                        };
                    }).ToArray()
                };
            }).ToArray()
        };
    }

    static uint Chr0ForensicBits(float value) => unchecked((uint)BitConverter.ToInt32(BitConverter.GetBytes(value), 0));
    static string Chr0ForensicHex(float value) => Chr0ForensicBits(value).ToString("x8", CultureInfo.InvariantCulture);

    static string Chr0ForensicBackingHash(ResourceNode resource)
    {
        var data = resource.WorkingUncompressed;
        if (data.Length < 0 || data.Length > FighterProject.MaximumAssetBytes) throw new InvalidDataException("Native forensic backing exceeds the asset limit.");
        var bytes = new byte[data.Length];
        if (bytes.Length > 0) Marshal.Copy((IntPtr)data.Address, bytes, 0, bytes.Length);
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
}
