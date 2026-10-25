using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Linq;

internal static partial class Program
{
    static void WorkspaceRootReferenceSampling(FittingModelView view, MDL0Node authoringModel, CHR0Node reference, string[] roots)
    {
        foreach (bool sparse in new[] { false, true })
        using (var source = new CHR0Node { Name = "Original procedural own-root reference probe", FrameCount = 9, Loop = false })
        {
            for (int i = 0; i < roots.Length; i++)
            {
                var bone = authoringModel._linker.BoneCache.Single(b => b.Name == roots[i]);
                if (sparse && i == 0) continue;
                var track = source.CreateEntry(bone.Name);
                for (int channel = 0; channel < 9; channel++)
                {
                    if (sparse && channel % 3 != 0) continue;
                    float first = bone.BindState[channel], last = channel < 3 ? first * (1.05f + i * .02f) : first + (channel < 6 ? 7.5f * (i + 1) : .3f * (i + 1));
                    track.SetKeyframe(channel, 0, first, true, true)._tangent = (last - first) / 8;
                    track.SetKeyframe(channel, 8, last, true, true)._tangent = (last - first) / 8;
                }
            }
            string original = SetNativeContent(source), frozenReference = SetNativeContent(reference);
            using (var oracle = RecoveryReferenceUnderSourceRoots(authoringModel, source, reference, roots))
            foreach (float frame in new[] { 1, 1.37f, 4.73f, 9 })
            {
                view.PoseBodyReference(reference, 1, source, frame, roots);
                Check(view.Model._linker.BoneCache.All(b => RecoverySamePose(b.FrameState, RecoverySample(roots.Contains(b.Name) ? source : reference, b, roots.Contains(b.Name) ? frame - 1 : 0))), "Reference-body preview follows every source-owned S/R/T channel and bind fallback at " + frame + " / sparse=" + sparse);
                authoringModel.ApplyCHR(oracle, frame);
                Check(view.Model._linker.BoneCache.All(b => {
                    var expected = authoringModel._linker.BoneCache.Single(v => v.Name == b.Name).Matrix;
                    return Enumerable.Range(0, 16).All(c => Math.Abs((double)b.Matrix[c] - expected[c]) <= LoopPositionTolerance);
                }), "Independent native CHR0 world matrices agree with reference-body/source-root composition at " + frame + " / sparse=" + sparse);
            }
            Check(SetNativeContent(source) == original && SetNativeContent(reference) == frozenReference, "Reference preview leaves exact procedural source-root curves and authentic reference channels unchanged / sparse=" + sparse);
            foreach(var track in source.Children.OfType<CHR0EntryNode>())track.Name=track.Name.ToLowerInvariant();
            authoringModel.ApplyCHR(source,4.73f);
            var nativeRoots=authoringModel._linker.BoneCache.Where(b=>roots.Contains(b.Name)).ToDictionary(b=>b.Name,b=>b.FrameState);
            view.PoseBodyReference(reference,1,source,4.73f,roots);
            Check(view.Model._linker.BoneCache.Where(b=>roots.Contains(b.Name)).All(b=>RecoverySamePose(b.FrameState,nativeRoots[b.Name])),"Reference controls retain native case-insensitive CHR0 track binding / sparse="+sparse);

        }
    }
}
