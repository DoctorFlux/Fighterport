using BrawlLib.Internal;
using BrawlLib.Internal.IO;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace BrawlCrate.Studio
{
    // Operation-owned visual copies only. A fitting preview does not need to
    // rebuild any animation/container in the authoring document to find textures.
    internal static unsafe class NativeFittingPreviewSnapshot
    {
        const int MaximumContextBytes = 512 * 1024 * 1024, MaximumNodes = 100000, MaximumDepth = 128;
        sealed class Visual { internal ResourceNode Node; internal int[] Path; }

        internal static bool HasVisualDependencies(ResourceNode root, CancellationToken token = default(CancellationToken))
            => Visuals(root, token).Count != 0;

        internal static bool HasTextureDependencies(ResourceNode root, CancellationToken token = default(CancellationToken))
            => Visuals(root, token).Any(v => v.Node is TEX0Node || v.Node is PLT0Node);

        internal static void WriteRawContext(ResourceNode root, string path, MDL0Node selectedModel,
            string modelPath, CancellationToken token = default(CancellationToken))
        {
            var visual = Visuals(root, token); RequireCleanVisuals(visual, selectedModel);
            if (visual.Count == 0) throw Unavailable("this resource has no native model, texture or palette dependencies");
            if (root is MDL0Node)
            {
                if (!ReferenceEquals(root, selectedModel)) throw Unavailable("a standalone reference model has no current independent model snapshot");
                RequireFile(modelPath); File.Copy(modelPath, path, true); token.ThrowIfCancellationRequested(); return;
            }
            if (root is TEX0Node || root is PLT0Node)
            {
                // A standalone BRES leaf may reference strings beyond its
                // resource-size field. Clone the complete independent file map,
                // then invoke the standalone writer on that COPY only.
                var source = root.WorkingUncompressed;
                if (root.IsCompressed || source.Map == null || source.Address != source.Map.Address || source.Map.Length < source.Length ||
                    source.Map.Length < 1 || source.Map.Length > MaximumContextBytes)
                    throw Unavailable("the complete standalone texture/palette bytes cannot be independently bounded");
                var map = FileMap.FromTempFile(source.Map.Length); ResourceNode independent = null;
                try
                {
                    Memory.Move(map.Address, source.Map.Address, (uint)source.Map.Length);
                    independent = NodeFactory.FromSource(null, new DataSource(map), root.GetType(), false);
                    if (independent == null) throw Unavailable("the standalone texture/palette copy could not be decoded");
                    independent.Name = root.Name; independent.ExportChecked(path);
                }
                finally { if (independent != null) independent.Dispose(); else map.Dispose(); }
                token.ThrowIfCancellationRequested(); return;
            }
            if (!(root is ARCNode) && !(root is BRRESNode))
                throw Unavailable("this rawless or unsupported container has no verified independent visual-context copy");
            int bytes = root.WorkingSource.Length;
            if (bytes < 1 || bytes > MaximumContextBytes) throw Unavailable("native visual context exceeds the bounded 512 MiB snapshot or has no complete bytes");
            // The stream overload copies WorkingSource. The string overload
            // calls Rebuild and can silently change live sibling CHR0 storage.
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read)) root.Export(stream);
            token.ThrowIfCancellationRequested();
            if (new FileInfo(path).Length != bytes) throw Unavailable("the complete raw native context was not copied");
        }

        internal static ResourceNode OpenContext(ResourceNode original, string path, MDL0Node selectedModel,
            string modelPath, CancellationToken token = default(CancellationToken))
        {
            var live = Visuals(original, token); RequireCleanVisuals(live, selectedModel); RequireFile(path);
            var copy = NodeFactory.FromFile(null, path) ?? throw Unavailable("the independent native visual context could not be decoded");
            try
            {
                // Container/standalone root names can derive from the staged
                // filename rather than bytes; retain the current display name.
                copy.Name = original.Name;
                var frozen = Visuals(copy, token);
                if (copy.GetType() != original.GetType() || live.Count != frozen.Count ||
                    !live.Select(v => Key(v.Path)).SequenceEqual(frozen.Select(v => Key(v.Path)), StringComparer.Ordinal))
                    throw Unavailable("native visual member structure changed since the raw context was saved");
                foreach (var member in live)
                {
                    token.ThrowIfCancellationRequested(); ResourceNode a = original, b = copy;
                    for (int depth = 0; ; depth++)
                    {
                        bool selected = depth == member.Path.Length && ReferenceEquals(a, selectedModel);
                        if (a.GetType() != b.GetType() || !selected && Context(a) != Context(b))
                            throw Unavailable("a visual member's exact numeric path, type or native ancestor context changed");
                        if (depth == member.Path.Length) break;
                        int index = member.Path[depth];
                        if (index < 0 || index >= a.Children.Count || index >= b.Children.Count)
                            throw Unavailable("a visual member's numeric path is unavailable");
                        a = a.Children[index]; b = b.Children[index];
                    }
                    if (ReferenceEquals(member.Node, selectedModel))
                    {
                        RequireFile(modelPath);
                        // Replace on the COPY from the pre-existing standalone
                        // model snapshot. Never Replace(ResourceNode) from live
                        // input: that overload invokes its native exporter.
                        b.Replace(modelPath); b.Name = selectedModel.Name;
                    }
                    else if ((original is ARCNode || original is BRRESNode) && !SameRawLeaf(member.Node, b, token))
                        throw Unavailable("a clean visual member's exact bytes or duplicate-member order differs from the raw context");
                }
                token.ThrowIfCancellationRequested(); var result = copy; copy = null; return result;
            }
            finally { copy?.Dispose(); }
        }

        static List<Visual> Visuals(ResourceNode root, CancellationToken token)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            var result = new List<Visual>(); var seen = new HashSet<ResourceNode>();
            var pending = new Stack<Tuple<ResourceNode, int[]>>(); pending.Push(Tuple.Create(root, new int[0]));
            while (pending.Count != 0)
            {
                token.ThrowIfCancellationRequested(); var item = pending.Pop(); var node = item.Item1; var path = item.Item2;
                if (!seen.Add(node) || seen.Count > MaximumNodes || path.Length > MaximumDepth)
                    throw Unavailable("native visual traversal exceeds 100000 nodes / 128 levels or has ambiguous ownership");
                if (node is MDL0Node || node is TEX0Node || node is PLT0Node)
                { result.Add(new Visual { Node = node, Path = path }); continue; }
                // CHR0 and every other nonvisual BRES resource are opaque to
                // this operation; no key collections or animation writers run.
                if (node is BRESEntryNode) continue;
                var children = node.Children;
                if (children.Count > MaximumNodes - seen.Count - pending.Count)
                    throw Unavailable("native visual traversal exceeds the bounded pending-node budget");
                for (int i = children.Count - 1; i >= 0; i--)
                    pending.Push(Tuple.Create(children[i], path.Concat(new[] { i }).ToArray()));
            }
            return result;
        }

        static void RequireCleanVisuals(List<Visual> visual, MDL0Node selectedModel)
        {
            foreach (var member in visual)
                if (!ReferenceEquals(member.Node, selectedModel) &&
                    (member.Node.HasChanged || member.Node.IsBranch || member.Node.IsDirty))
                    throw Unavailable("an unsaved texture, palette or additional model would otherwise be replaced by stale raw bytes");
        }
        static string Key(int[] path) => string.Join("/", path);
        static string Context(ResourceNode node) => node.GetType().FullName + "|" + node.Name +
            (node is ARCEntryNode arc ? "|" + arc.FileType + "|" + arc.FileIndex + "|" + arc.GroupID + "|" + arc.RedirectIndex + "|" + (arc.RedirectNode?.Index ?? -1) : "") +
            (node is ARCNode container ? "|" + container.IsFighter + "|" + container.IsStage + "|" + container.IsSubspace + "|" + container.IsPair : "") +
            (node is BRESGroupNode group ? "|" + group.Type : "");
        static bool SameRawLeaf(ResourceNode original, ResourceNode copy, CancellationToken token)
        {
            var a = original.WorkingUncompressed; var b = copy.WorkingUncompressed;
            if (a.Length < 1 || a.Length != b.Length || a.Length > MaximumContextBytes) return false;
            byte* left = (byte*)a.Address, right = (byte*)b.Address;
            for (int i = 0; i < a.Length; i++)
            { if ((i & 65535) == 0) token.ThrowIfCancellationRequested(); if (left[i] != right[i]) return false; }
            return true;
        }
        static void RequireFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || new FileInfo(path).Length < 1 || new FileInfo(path).Length > MaximumContextBytes)
                throw Unavailable("the current independent visual snapshot is missing or exceeds 512 MiB");
        }
        static InvalidOperationException Unavailable(string reason)
            => new InvalidOperationException("Save texture/palette/context edits before fitting: " + reason + ". The authoring resources were not rebuilt.");
    }
}
