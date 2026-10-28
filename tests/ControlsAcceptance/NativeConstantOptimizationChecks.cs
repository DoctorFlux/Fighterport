using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;

internal static partial class Program
{
    static void NativeConstantOptimizationChecks(string modelPath, string stage)
    {
        Directory.CreateDirectory(stage);
        using (var root = NodeFactory.FromFile(null, modelPath))
        {
            var model = OptimizationWalk(root).OfType<MDL0Node>().OrderByDescending(m => m.NumVertices).First();
            var names = model.AllBones.Select(b => b.Name).ToArray();
            var bone = model.AllBones.Last(b => Enumerable.Range(0,9).All(c => BitConverter.ToInt32(BitConverter.GetBytes(b.BindState[c]),0) != int.MinValue));
            using (var before = OptimizationFixture(bone.Name,"Native bind-constant cleanup"))
            {
                var entry = (CHR0EntryNode)before.Children[0];
                entry.ScaleCompensateApply = bone.SegScaleCompApply; entry.ScaleCompensateParent = bone.SegScaleCompParent; entry.ClassicScaleOff = !bone.ClassicScale;
                for (int c=0;c<9;c++) OptimizationReplace(entry,c,Enumerable.Range(0,5).Select(f=>OptimizationKey(f,bone.BindState[c],0)).ToArray());
                Check(NativeAnimationOptimization.CanRemoveBindTrack(entry,bone),"Native constants: exact raw bind values and model scale semantics permit removing the animation track");
                string original=NativeAnimationOptimization.ContentKey(before);
                var options=new NativeAnimationOptimizationOptions{ExactOnly=true,TargetBones=names,BindPoseCleanupBones=new[]{bone.Name},LengthScale=NativeAnimationOptimization.GetBindBoundsDiagonal(model)};
                using(var result=NativeAnimationOptimization.Prepare(model,before,new ControlTrack[0],options,stage))
                {
                    Check(result.HasChanges && result.AfterTracks==0 && result.AfterKeys==0 && result.AfterBytes<result.BeforeBytes,
                        "Native constants: full bind track removal independently reopens as an actually smaller empty-track CHR0");
                    Check(result.MaximumEulerError==0 && result.MaximumScaleError==0 && result.MaximumLocalTranslationError==0 && result.MaximumWorldBoneError==0 && result.MaximumSkinError==0 && result.Samples>0,
                        "Native constants: bind fallback retains every sampled native bone and skinned vertex exactly");
                }
                Check(NativeAnimationOptimization.ContentKey(before)==original,"Native constants: removing candidate tracks never changes the original native clip");
                options.BindPoseCleanupBones=new string[0];
                OptimizationReject(()=>NativeAnimationOptimization.Prepare(model,before,new ControlTrack[0],options,stage),"eligible exact bind","Native constants: unreviewed target-track omission is blocked independently by the adapter");
                options.BindPoseCleanupBones=new[]{bone.Name}; options.ProtectedBones=new[]{bone.Name};
                OptimizationReject(()=>NativeAnimationOptimization.Prepare(model,before,new ControlTrack[0],options,stage),"eligible exact bind","Native constants: structural pin/contact protections block even bind-equivalent removal");
                options.ProtectedBones=new string[0]; options.MandatoryFrames=new[]{0,4};
                using(var result=NativeAnimationOptimization.Prepare(model,before,new ControlTrack[0],options,stage))
                    Check(result.AfterTracks==0 && result.MaximumEulerError==0 && result.MaximumWorldBoneError==0 && result.MaximumSkinError==0 && result.Samples>0,
                        "Native constants: independent raw bind and model-flag proof preserves fitting endpoint values and zero slopes without a redundant track");
                options.MandatoryFrames=new int[0];
                entry.ScaleCompensateApply=!bone.SegScaleCompApply;
                Check(!NativeAnimationOptimization.CanRemoveBindTrack(entry,bone),"Native constants: different animation scale-compensation flags keep the track even with bind-valued keys");
                OptimizationReject(()=>NativeAnimationOptimization.Prepare(model,before,new ControlTrack[0],options,stage),"eligible exact bind","Native constants: resource-flag changes cannot disappear behind a local-pose comparison");
                entry.ScaleCompensateApply=bone.SegScaleCompApply;
                OptimizationReplace(entry,3,Enumerable.Range(0,5).Select(f=>OptimizationKey(f,bone.BindState[3]+360,0)).ToArray());
                Check(!NativeAnimationOptimization.CanRemoveBindTrack(entry,bone),"Native constants: a full constant Euler turn differs from raw bind Euler and is preserved");
                OptimizationReplace(entry,3,Enumerable.Range(0,5).Select(f=>OptimizationKey(f,bone.BindState[3],f == 2 ? .1f : 0)).ToArray());
                Check(!NativeAnimationOptimization.CanRemoveBindTrack(entry,bone),"Native constants: equal values with nonzero tangent movement are not mistaken for bind pose");
                OptimizationReplace(entry,3,new[]{OptimizationKey(0,bone.BindState[3],BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue),0)),OptimizationKey(4,bone.BindState[3],0)});
                Check(!NativeAnimationOptimization.CanRemoveBindTrack(entry,bone),"Native constants: authored signed-zero tangents remain protected data");
            }
            using (var before = OptimizationFixture(bone.Name,"Native single shifted constant cleanup"))
            {
                var entry = (CHR0EntryNode)before.Children[0];
                OptimizationReplace(entry,3,new[]{OptimizationKey(4,.5f,0)});
                string original = NativeAnimationOptimization.ContentKey(before);
                using (var encodedCopy = NativeAnimationOptimization.CopyForStorageVerification(before))
                    Check(((CHR0EntryNode)encodedCopy.Children[0]).KeyArrays[3].Entries.Single()._index == 4 &&
                        entry.KeyArrays[3].Entries.Single()._index == 4 && NativeAnimationOptimization.ContentKey(before) == original,
                        "Native constants: reference storage verification retains the authored single-knot time and the unchanged original");
                var track = OptimizationTrack(entry); track.Channels[3] = new[]{OptimizationKey(0,.5f,0)};
                var options = new NativeAnimationOptimizationOptions { ExactOnly = true, TargetBones = names,
                    LengthScale = NativeAnimationOptimization.GetBindBoundsDiagonal(model), Budget = new NativeAnimationOptimizationBudget() };
                using (var result = NativeAnimationOptimization.Prepare(model,before,new[]{track},options,stage))
                {
                    Check(result.HasChanges && result.Candidate != null && ((CHR0EntryNode)result.Candidate.Children[0]).KeyArrays[3].Entries.Single()._index == 0 &&
                        result.MaximumEulerError == 0 && result.MaximumWorldBoneError == 0 && result.MaximumSkinError == 0 && result.Samples > 0,
                        "Native constants: a single shifted constant independently becomes the first-frame fixed key with identical native poses and skin");
                    using (var packed = NativeAnimationOptimization.PrepareCompact(model,before,before,result,options,stage,
                        (candidate,token) => System.Threading.Tasks.Task.FromResult(new NativeCompactCurveCertificate { Accepted = true,
                            Report = "Independent zero-slope constant identity." })).GetAwaiter().GetResult())
                        Check(packed.HasChanges && packed.ContentKey == result.ContentKey && packed.AfterBytes <= packed.BeforeBytes &&
                            ((CHR0EntryNode)packed.Candidate.Children[0]).KeyArrays[3].Entries.Single()._index == 0,
                            "Native constants: compact preparation measures actual fixed storage while retaining raw current identity for the canonicalizing edit");
                }
                Check(entry.KeyArrays[3].Entries.Single()._index == 4 && NativeAnimationOptimization.ContentKey(before) == original,
                    "Native constants: analysis and native size verification retain the original shifted knot for document Undo");
            }
            NativeConstantJoinChecks(model,bone,names,stage);
        }
    }

    static void NativeConstantJoinChecks(MDL0Node model,MDL0BoneNode bone,string[] names,string stage)
    {
        using(var before=OptimizationFixture(bone.Name,"Independent generated continuous corner"))
        {
            before.FrameCount=5;before.Loop=false;var entry=(CHR0EntryNode)before.Children[0];
            OptimizationReplace(entry,3,new[]{OptimizationKey(0,0,1),OptimizationKey(2,2,1),OptimizationKey(2,2,1.02f),OptimizationKey(4,4,1)});
            var track=OptimizationTrack(entry);track.Channels[3]=new[]{OptimizationKey(0,0,1),OptimizationKey(4,4,1)};
            var generated=new NativeAnimationOptimizationOptions{SmallFile=true,ExactOnly=false,TargetBones=names,
                LengthScale=NativeAnimationOptimization.GetBindBoundsDiagonal(model),GeneratedPairSites=new[]{new NativeGeneratedPairSite{Bone=bone.Name,Channel=3,Frame=2}}};
            using(var result=NativeAnimationOptimization.Prepare(model,before,new[]{track},generated,stage))
                Check(result.HasChanges&&result.AfterBytes<result.BeforeBytes&&result.MaximumEulerError<.01&&result.MaximumSkinError<generated.LengthScale*.001,
                    "Generated native corner reduction independently reopens and verifies the complete target hierarchy and skin");
            generated.ExactBones=new[]{bone.Name};
            OptimizationReject(()=>NativeAnimationOptimization.Prepare(model,before,new[]{track},generated,stage),"mandatory or discontinuous",
                "Generated provenance cannot remove a source-owned exact native control side");
            generated.ExactBones=new string[0];generated.GeneratedPairSites=new NativeGeneratedPairSite[0];
            OptimizationReject(()=>NativeAnimationOptimization.Prepare(model,before,new[]{track},generated,stage),"mandatory or discontinuous",
                "Imported native authored pairs remain protected without exact operation-owned provenance");
        }
        foreach(bool loop in new[]{false,true})
        using(var before=OptimizationFixture(bone.Name,"Independent exact fitting-join constant"))
        {
            before.Version=loop?4:5; before.FrameCount=44; before.Loop=loop;
            if(before.Version==4) before.UserEntries.Clear();
            var entry=(CHR0EntryNode)before.Children[0];int[] joins={0,4,34,43};
            for(int c=0;c<9;c++) OptimizationReplace(entry,c,joins.Select(frame=>OptimizationKey(frame,c<3?1:c==3?720:0,0)).ToArray());
            string original=NativeAnimationOptimization.ContentKey(before);
            var track=OptimizationTrack(entry);
            for(int c=0;c<9;c++) track.Channels[c]=new[]{OptimizationKey(0,c<3?1:c==3?720:0,0)};
            var options=new NativeAnimationOptimizationOptions{ExactOnly=true,TargetBones=names,MandatoryFrames=joins,
                LengthScale=NativeAnimationOptimization.GetBindBoundsDiagonal(model)};
            using(var result=NativeAnimationOptimization.Prepare(model,before,new[]{track},options,stage))
            {
                Check(result.HasChanges&&result.AfterKeys==9&&result.AfterBytes<result.BeforeBytes&&result.MaximumEulerError==0&&
                    result.MaximumScaleError==0&&result.MaximumLocalTranslationError==0&&result.MaximumWorldBoneError==0&&result.MaximumSkinError==0&&result.Samples>0,
                    "Native constants: all SRT fitting joins collapse to smaller fixed storage while independently retaining exact native poses and skin / loop="+loop);
                Check(joins.All(frame=>ReplayKnotMatches(before,result.Candidate,frame,new System.Collections.Generic.HashSet<string>(new[]{bone.Name}),model)),
                    "Native constants: independent join oracle proves the original raw function and decoded fixed first-frame representation / loop="+loop);
            }
            Check(NativeAnimationOptimization.ContentKey(before)==original,
                "Native constants: fitting-join preparation never edits the original keys, timing, loop or metadata");
            entry.Keyframes.SetFrameValue(3,4,720,true)._tangent=0;
            using(var redundant=NativeAnimationOptimization.Prepare(model,before,new[]{track},options,stage))
                Check(redundant.HasChanges&&redundant.AfterKeys==9&&redundant.MaximumEulerError==0&&redundant.MaximumSkinError==0,
                    "Native constants: a redundant identical paired side collapses through independent encoding and exact native pose/skin verification");
            options.ProtectedBones=new[]{bone.Name};
            OptimizationReject(()=>NativeAnimationOptimization.Prepare(model,before,new[]{track},options,stage),"protected native track",
                "Native constants: structural pin/contact protection retains authored constant join tuples");
            options.ProtectedBones=new string[0];
            OptimizationReplace(entry,3,joins.Select(frame=>OptimizationKey(frame,720,frame==4?.001f:0)).ToArray());
            OptimizationReject(()=>NativeAnimationOptimization.Prepare(model,before,new[]{track},options,stage),"mandatory or discontinuous",
                "Native constants: equal values with real Hermite movement cannot use constant fitting-join substitution");
            OptimizationReplace(entry,3,joins.Select(frame=>OptimizationKey(frame,frame==4?720.00006f:720,0)).ToArray());
            OptimizationReject(()=>NativeAnimationOptimization.Prepare(model,before,new[]{track},options,stage),"mandatory or discontinuous",
                "Native constants: a near-constant attack pulse cannot lose its exact authored fitting join");
        }
    }
}
