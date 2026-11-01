using HSDRaw;
using HSDRaw.Common;
using HSDRaw.Common.Animation;
using HSDRaw.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace FighterPort.Adapters.Melee
{
    public sealed class MeleeMember
    {
        public int Offset { get; internal set; }
        public int Length { get; internal set; }
        public string Sha256 { get; internal set; }
        public HSDRawFile Native { get; internal set; }
        internal byte[] Bytes;
        public byte[] CopyOriginalBytes() => (byte[])Bytes.Clone();
    }
    public sealed class MeleeNativeModel
    {
        public string Symbol { get; internal set; }
        public HSD_JOBJ Root { get; internal set; }
        public MeleeMember Member { get; internal set; }
        public string Key => Member.Offset + "/" + Symbol;
    }
    public sealed class MeleeNativeMotion
    {
        public string Symbol { get; internal set; }
        public MeleeMember Member { get; internal set; }
        public HSD_FigaTree Figatree { get; internal set; }
        public HSD_AnimJoint AnimJoint { get; internal set; }
        public float LastFrame { get; internal set; }
        public string Key => Member.Offset + "/" + Symbol;
    }
    public static class MeleeNative
    {
        // Validate the raw pointer envelope before invoking the upstream parser.
        public static void ValidateDat(byte[] data)
        {
            if (data == null || data.Length < 32 || data.Length > MeleeSource.MaximumAssetBytes || MeleeSource.U32(data, 0) != data.Length)
                throw new InvalidDataException("Invalid HSD DAT physical size.");
            long reloc = 32L + MeleeSource.U32(data, 4), count = MeleeSource.U32(data, 8), roots = MeleeSource.U32(data, 12), refs = MeleeSource.U32(data, 16);
            long symbols = reloc + 4 * count, strings = symbols + 8 * (roots + refs);
            if (reloc < 32 || reloc > data.Length || count > 2000000 || roots + refs > 100000 || strings > data.Length)
                throw new InvalidDataException("Invalid HSD DAT table bounds.");
            var pointers = new HashSet<uint>();
            for (long i = 0; i < count; i++)
            {
                uint offset = MeleeSource.U32(data, checked((int)(reloc + 4 * i)));
                if ((offset & 3) != 0 || offset > reloc - 36 || !pointers.Add(offset)) throw new InvalidDataException("Invalid or duplicated HSD relocation.");
                uint target = MeleeSource.U32(data, checked((int)(32 + offset)));
                if (target >= reloc - 32) throw new InvalidDataException("HSD pointer target is outside the data section.");
            }
            for (long i = 0; i < roots + refs; i++)
            {
                int at = checked((int)(symbols + i * 8)); long target = MeleeSource.U32(data, at), name = strings + MeleeSource.U32(data, at + 4);
                if (target >= reloc - 32 || name < strings || name >= data.Length) throw new InvalidDataException("Invalid HSD root/reference entry.");
                int end = Array.IndexOf(data, (byte)0, (int)name);
                if (end < 0 || end - name > 4096) throw new InvalidDataException("Invalid HSD root/reference symbol.");
            }
        }
        public static IReadOnlyList<MeleeMember> ReadMembers(byte[] source, CancellationToken cancellation = default)
        {
            if (source == null || source.Length < 32 || source.Length > MeleeSource.MaximumAssetBytes) throw new InvalidDataException("Invalid native Melee resource size.");
            var members = new List<MeleeMember>(); int offset = 0;
            while (offset < source.Length)
            {
                cancellation.ThrowIfCancellationRequested();
                if (source.Length - offset < 32 || MeleeSource.U32(source, offset) == 0)
                {
                    if (source.Skip(offset).Any(b => b != 0)) throw new InvalidDataException("Unrecognized nonzero native archive tail.");
                    break;
                }
                int size = checked((int)MeleeSource.U32(source, offset));
                if (size < 32 || size > source.Length - offset) throw new InvalidDataException("Truncated native Melee archive member.");
                var bytes = new byte[size]; Buffer.BlockCopy(source, offset, bytes, 0, size); ValidateDat(bytes);
                var native = new HSDRawFile(bytes);
                members.Add(new MeleeMember { Offset = offset, Length = size, Bytes = bytes, Sha256 = MeleeSource.Sha256(bytes), Native = native });
                if (members.Count > 100000) throw new InvalidDataException("Native archive member budget exceeded.");
                int next = checked((offset + size + 31) & ~31);
                // Alignment bytes are opaque native data, not necessarily zero.
                // ReadMembers never rewrites the parent archive; its original bytes remain authoritative.
                offset = next;
            }
            if (members.Count == 0) throw new InvalidDataException("No valid HSD members.");
            return members;
        }
        public static IEnumerable<MeleeNativeModel> Models(MeleeMember member)
        {
            foreach (var root in member.Native.Roots)
                if (root.Data is HSD_JOBJ jobj) yield return new MeleeNativeModel { Symbol = root.Name, Root = jobj, Member = member };
        }
        public static IEnumerable<MeleeNativeMotion> Motions(MeleeMember member)
        {
            foreach (var root in member.Native.Roots)
            {
                if (root.Data is HSD_FigaTree fig)
                {
                    Finite(fig.FrameCount);
                    if (fig.FrameCount < 0 || fig.FrameCount > 10000000 || fig.NodeCount > 100000) throw new InvalidDataException("Invalid Figatree dimensions.");
                    yield return new MeleeNativeMotion { Symbol = root.Name, Member = member, Figatree = fig, LastFrame = fig.FrameCount };
                }
                else if (root.Data is HSD_AnimJoint anim)
                {
                    float last = 0;
                    foreach (var joint in AnimJoints(anim)) if (joint.AOBJ != null) { Finite(joint.AOBJ.EndFrame); last = Math.Max(last, joint.AOBJ.EndFrame); }
                    yield return new MeleeNativeMotion { Symbol = root.Name, Member = member, AnimJoint = anim, LastFrame = last };
                }
            }
        }
        public static HSD_JOBJ[] Joints(HSD_JOBJ root)
        {
            var result = new List<HSD_JOBJ>(); var seen = new HashSet<HSDStruct>();
            var pending = new Stack<HSD_JOBJ>(); if (root != null) pending.Push(root);
            while (pending.Count > 0)
            {
                var joint = pending.Pop();
                if (!seen.Add(joint._s) || result.Count >= 100000) throw new InvalidDataException("Cyclic or oversized native joint graph.");
                foreach (float value in new[] { joint.SX, joint.SY, joint.SZ, joint.RX, joint.RY, joint.RZ, joint.TX, joint.TY, joint.TZ }) Finite(value);
                result.Add(joint); if (joint.Next != null) pending.Push(joint.Next); if (joint.Child != null) pending.Push(joint.Child);
            }
            return result.ToArray();
        }
        public static HSD_AnimJoint[] AnimJoints(HSD_AnimJoint root)
        {
            var result = new List<HSD_AnimJoint>(); var seen = new HashSet<HSDStruct>(); var pending = new Stack<HSD_AnimJoint>(); if (root != null) pending.Push(root);
            while (pending.Count > 0)
            {
                var joint = pending.Pop(); if (!seen.Add(joint._s) || result.Count >= 100000) throw new InvalidDataException("Cyclic or oversized native animation hierarchy.");
                result.Add(joint); if (joint.Next != null) pending.Push(joint.Next); if (joint.Child != null) pending.Push(joint.Child);
            }
            return result.ToArray();
        }
        public static string RigHash(MeleeNativeModel model)
        {
            // Includes native flags, inverse transforms, authentic local values and links.
            var joints = Joints(model.Root); var ids = joints.Select((j,i) => new { j._s, Index = i }).ToDictionary(j => j._s, j => j.Index);
            using (var output = new MemoryStream()) using (var writer = new BinaryWriter(output))
            {
                foreach (var joint in joints)
                {
                    writer.Write((uint)joint.Flags); writer.Write(joint.Child == null ? -1 : ids[joint.Child._s]); writer.Write(joint.Next == null ? -1 : ids[joint.Next._s]);
                    foreach (float value in new[] { joint.SX, joint.SY, joint.SZ, joint.RX, joint.RY, joint.RZ, joint.TX, joint.TY, joint.TZ }) writer.Write(value);
                    var inverse = joint.InverseWorldTransform?._s.GetData() ?? new byte[0]; writer.Write(inverse.Length); writer.Write(inverse);
                }
                writer.Flush(); return MeleeSource.Sha256(output.ToArray());
            }
        }
        public static void Finite(float value) { if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite native Melee value."); }
    }
}
