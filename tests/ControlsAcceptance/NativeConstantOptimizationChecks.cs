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
                OptimizationReject(()=>NativeAnimationOptimization.Prepare(model,before,new ControlTrack[0],options,stage),"eligible exact bind","Native constants: exact fitting endpoints retain their authored animation track");
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
                    Check(((CHR0EntryNode)encodedCopy.Children[0]).KeyArrays[3].Entries.Single()._index == 0 &&
                        entry.KeyArrays[3].Entries.Single()._index == 4 && NativeAnimationOptimization.ContentKey(before) == original,
                        "Native constants: storage verification canonicalizes a single zero-slope key only on its independent copy");
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
        }
    }
}
