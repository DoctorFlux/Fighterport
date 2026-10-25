using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.Linq;
using NativeVector = BrawlLib.Internal.Vector3;

internal static partial class Program
{
    static unsafe NativeVector[] ReferencePreviewNormals(MDL0Node model)
    {
        var result=new List<NativeVector>();
        foreach(var polygon in model.PolygonList.OfType<MDL0ObjectNode>())
        {
            var manager=polygon._manager;
            if(manager?._faceData[1]==null||manager._indices==null||manager._vertices==null)continue;
            var indices=(ushort*)manager._indices.Address;var normals=(NativeVector*)manager._faceData[1].Address;
            // Independent observation of the native stream's normal transform,
            // including its unbound-face fallback. No renderer capture needed.
            for(int i=0;i<manager._pointCount;i++)
            {
                int vertex=indices[i];var normal=normals[i];
                result.Add(vertex<manager._vertices.Count?normal*manager._vertices[vertex].GetMatrix().GetRotationMatrix():normal);
            }
        }
        return result.ToArray();
    }
    static void ReferencePreviewLegacyPose(MDL0Node model,CHR0Node reference,float referenceFrame,CHR0Node source,float sourceFrame,string[] retained)
    {
        // Frozen previous implementation: skin the reference pose, substitute
        // source-local controls, then skin again. This is deliberately separate
        // from the optimized production preview method.
        model.ApplyCHR(reference,referenceFrame);
        foreach(string name in retained)
        {
            var bone=model._linker.BoneCache.Single(b=>b.Name==name);var pose=bone.BindState;
            var track=source.Children.OfType<CHR0EntryNode>().SingleOrDefault(t=>string.Equals(t.Name,name,StringComparison.OrdinalIgnoreCase));
            if(track!=null)for(int channel=0;channel<9;channel++)if(track.KeyArrays[channel].KeyCount>0)pose[channel]=track.GetFrameValue(channel,sourceFrame-1);
            pose.CalcTransforms();bone.FrameState=pose;
        }
        model.WeightMeshes();
    }
    static void ReferencePreviewOptimizedEquivalence(FittingModelView view,MDL0Node authoringModel,CHR0Node source,CHR0Node reference,string[] retained,bool sparse)
    {
        var ownerBones=authoringModel._linker.BoneCache;
        var ownerPoses=ownerBones.Select(b=>b.FrameState).ToArray();
        var ownerMatrices=ownerBones.Select(b=>b.Matrix).ToArray();
        var ownerVertices=authoringModel.PolygonList.OfType<MDL0ObjectNode>().Where(p=>p.Vertices!=null).SelectMany(p=>p.Vertices).ToArray();
        var ownerPositions=ownerVertices.Select(v=>v.WeightedPosition).ToArray();
        var vertices=view.Model.PolygonList.OfType<MDL0ObjectNode>().Where(p=>p.Vertices!=null).SelectMany(p=>p.Vertices).ToArray();
        Check(vertices.Length>0&&!view.Model._dontUpdateMesh,"Reference preview equivalence observes complete authentic skin geometry with mesh updates enabled");
        foreach(float referenceFrame in new[]{1f,Math.Min(reference.FrameCount,2.25f)}.Distinct())
        foreach(float frame in new[]{1f,1.37f,4.73f,9f})
        {
            ReferencePreviewLegacyPose(view.Model,reference,referenceFrame,source,frame,retained);
            var poses=view.Model._linker.BoneCache.Select(b=>b.FrameState).ToArray();
            var matrices=view.Model._linker.BoneCache.Select(b=>b.Matrix).ToArray();
            var positions=vertices.Select(v=>v.WeightedPosition).ToArray();
            var normals=ReferencePreviewNormals(view.Model);int skins=view.ReferencePreviewSkinPasses,samples=view.ReferencePreviewSamples;
            view.PoseBodyReference(reference,referenceFrame,source,frame,retained);
            string sample=" at "+frame+" / reference="+referenceFrame+" / retained="+retained.Length+" / sparse="+sparse;
            Check(view.Model._linker.BoneCache.Select((b,i)=>Enumerable.Range(0,9).All(c=>b.FrameState[c]==poses[i][c])&&Enumerable.Range(0,16).All(c=>b.Matrix[c]==matrices[i][c])).All(v=>v),"Single-skin reference preview preserves exact previous native local and world poses"+sample);
            Check(vertices.Select((v,i)=>v.WeightedPosition.Equals(positions[i])).All(v=>v)&&ReferencePreviewNormals(view.Model).SequenceEqual(normals),"Single-skin reference preview preserves every previous weighted vertex and transformed normal"+sample);
            Check(view.ReferencePreviewSkinPasses==skins+1&&view.ReferencePreviewSamples==samples+1&&!view.Model._dontUpdateMesh,"Reference composition performs one final skin pass and restores native mesh policy"+sample);
        }
        Check(ownerBones.Select((b,i)=>Enumerable.Range(0,9).All(c=>b.FrameState[c]==ownerPoses[i][c])&&Enumerable.Range(0,16).All(c=>b.Matrix[c]==ownerMatrices[i][c])).All(v=>v)&&ownerVertices.Select((v,i)=>v.WeightedPosition.Equals(ownerPositions[i])).All(v=>v),"Reference preview equivalence leaves the complete live authoring pose and skin untouched / retained="+retained.Length+" / sparse="+sparse);
        int before=view.ReferencePreviewSkinPasses;view.Model._dontUpdateMesh=true;
        try{view.PoseBodyReference(reference,1,source,1,retained);Check(view.Model._dontUpdateMesh&&view.ReferencePreviewSkinPasses==before,"Reference preview preserves an existing mesh-suppression flag");}
        finally{view.Model._dontUpdateMesh=false;}
    }
    static void WorkspaceRootReferenceSampling(FittingModelView view, MDL0Node authoringModel, CHR0Node reference, string[] roots,string[] alternateRoots=null)
    {
        foreach (bool sparse in new[] { false, true })
        using (var source = new CHR0Node { Name = "Original procedural own-root reference probe", FrameCount = 9, Loop = false })
        {
            var authoredRoots=roots.Union(alternateRoots??roots,StringComparer.Ordinal).ToArray();
            for (int i = 0; i < authoredRoots.Length; i++)
            {
                var bone = authoringModel._linker.BoneCache.Single(b => b.Name == authoredRoots[i]);
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
            ReferencePreviewOptimizedEquivalence(view,authoringModel,source,reference,roots,sparse);
            if(alternateRoots!=null&&!alternateRoots.SequenceEqual(roots))ReferencePreviewOptimizedEquivalence(view,authoringModel,source,reference,alternateRoots,sparse);
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
