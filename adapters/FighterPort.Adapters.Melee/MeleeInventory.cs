using HSDRaw.Common;
using HSDRaw.Common.Animation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
namespace FighterPort.Adapters.Melee
{
    public sealed class MeleeInventoryModel
    {
        public string File, Key, Symbol, Sha256, RigHash, Actor;
        public int Joints;
    }
    public sealed class MeleeInventoryMotion
    {
        public string File, Key, Symbol, Sha256, Actor, Kind, ModelKey;
        public float LastFrame;
    }
    public sealed class MeleeInventoryFile
    {
        public string Path, Sha256;
        public int Bytes;
    }
    public sealed class MeleeInventory
    {
        public readonly List<MeleeInventoryModel> Models = new List<MeleeInventoryModel>();
        public readonly List<MeleeInventoryMotion> Motions = new List<MeleeInventoryMotion>();
        public readonly List<MeleeInventoryFile> Files = new List<MeleeInventoryFile>();
        public readonly List<string> Issues = new List<string>();
        public string Sha256 { get; private set; }
        // This is HSD's exported symbol linkage grammar, not a character-name map.
        public static string ModelActor(string symbol) => symbol.EndsWith("_Share_joint", StringComparison.Ordinal) ? symbol.Substring(0, symbol.Length - "_Share_joint".Length) : null;
        public static string MotionActor(string symbol)
        { int split = symbol.IndexOf("_Share_ACTION_", StringComparison.Ordinal); return split > 0 ? symbol.Substring(0, split) : null; }
        public static MeleeInventory Read(MeleeSource source, CancellationToken cancellation = default, Action<int,string> progress = null)
        {
            var result = new MeleeInventory(); var files = source.Files; int index = 0;
            foreach (var file in files)
            {
                cancellation.ThrowIfCancellationRequested(); progress?.Invoke(++index * 90 / files.Count, "Reading Melee native resources " + index + "/" + files.Count + ": " + file.Path);
                byte[] bytes = source.Read(file.Path); string hash = MeleeSource.Sha256(bytes);
                result.Files.Add(new MeleeInventoryFile { Path = file.Path, Sha256 = hash, Bytes = bytes.Length });
                try
                {
                    foreach (var member in MeleeNative.ReadMembers(bytes, cancellation))
                    {
                        foreach (var model in MeleeNative.Models(member))
                        {
                            string actor = ModelActor(model.Symbol);
                            // Fighter library: retain other resources in the file manifest, without mislabelling stage objects as fighters.
                            if (actor == null) continue;
                            result.Models.Add(new MeleeInventoryModel { File = file.Path, Key = model.Key, Symbol = model.Symbol, Actor = actor, Sha256 = member.Sha256, RigHash = MeleeNative.RigHash(model), Joints = MeleeNative.Joints(model.Root).Length });
                        }
                        foreach (var motion in MeleeNative.Motions(member))
                        {
                            string actor = MotionActor(motion.Symbol); if (actor == null) continue;
                            result.Motions.Add(new MeleeInventoryMotion { File = file.Path, Key = motion.Key, Symbol = motion.Symbol, Actor = actor, Sha256 = member.Sha256, Kind = motion.Figatree != null ? "Figatree" : "AnimJoint", LastFrame = motion.LastFrame });
                        }
                    }
                }
                catch (Exception error) when (error is InvalidDataException || error is EndOfStreamException || error is ArgumentException || error is IndexOutOfRangeException || error is OverflowException)
                { result.Issues.Add(file.Path + ": " + error.Message); }
            }
            foreach (var motion in result.Motions)
            {
                var exact = result.Models.Where(m => m.Actor == motion.Actor).ToArray();
                // Duplicate root symbols remain distinct resources. Never guess between them.
                if (exact.Length == 1) motion.ModelKey = exact[0].File + "|" + exact[0].Key;
                else result.Issues.Add(motion.File + ": " + motion.Symbol + (exact.Length == 0 ? " has no exact native model symbol association." : " has ambiguous native model symbol associations."));
            }
            result.Sha256 = MeleeSource.Sha256(Encoding.UTF8.GetBytes(string.Join("\n", result.Files.Select(f => f.Path + "\0" + f.Bytes + "\0" + f.Sha256))));
            return result;
        }
    }
}
