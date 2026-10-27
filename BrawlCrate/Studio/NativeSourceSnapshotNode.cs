using BrawlLib.Internal;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.SSBB.Types.Animations;

namespace BrawlCrate.Studio
{
    // An operation-owned donor snapshot. The legacy CHR0 node has no managed
    // scaling-rule property and resets the field during rebuild. Carry it only
    // here, preserving original source metadata without changing target writing
    // or claiming a new native scaling/evaluation adapter.
    internal sealed unsafe class NativeSourceSnapshotNode : CHR0Node
    {
        internal int ScalingRule { get; }
        internal NativeSourceSnapshotNode(int scalingRule) { ScalingRule = scalingRule; }

        public override void OnRebuild(VoidPtr address, int length, bool force)
        {
            base.OnRebuild(address, length, force);
            if (Version == 4) ((CHR0v4_3*)address)->_scalingRule = ScalingRule;
            else ((CHR0v5*)address)->_scalingRule = ScalingRule;
        }
    }
}
