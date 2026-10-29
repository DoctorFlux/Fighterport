using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using BrawlLib.Wii.Models;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // A reviewed authoring operation creates a new master. This fixture's old
    // L1 payload has a distinct hidden terminal (7); the explicit new end pose
    // is 6.25 at native time 7. Compression is not allowed to make that choice.
    // Undo must restore the complete old payload, not just its visible prefix.
    static void NativeAuthoredStorageChecks(string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Native authoring-storage qualification requires the private desktop.");
        Directory.CreateDirectory(output);
        var snapshotType=typeof(ModelForm).GetNestedType("NativeControlSnapshot",BindingFlags.NonPublic);
        var capture=snapshotType.GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static);
        var write=snapshotType.GetMethod("Write",BindingFlags.NonPublic|BindingFlags.Static);
        var restore=snapshotType.GetMethod("Restore",BindingFlags.NonPublic|BindingFlags.Instance);
        foreach(int version in new[]{4,5})
        {
            string sourcePath=AuthoredStorageFixture(output,version),sourceHash=FighterProject.HashFile(sourcePath);
            using(var source=NodeFactory.FromFile(null,sourcePath) as CHR0Node)
            using(var master=NativeAnimationOptimization.CopySource(source))
            {
                var sourceEntry=(CHR0EntryNode)source.Children.Single();var entry=(CHR0EntryNode)master.Children.Single();
                string original=NativeAnimationOptimization.SourceContentKey(master),originalStorage=NativeAnimationOptimization.StorageKey(master);
                string sourceContent=NativeAnimationOptimization.SourceContentKey(source),sourceStorage=NativeAnimationOptimization.StorageKey(source);
                bool sourceDirty=source.IsDirty;var sourceOwner=sourceEntry.Parent;
                var oldPlan=PreciseCHR0EntryNode.CaptureStorage(entry);
                Check(oldPlan[3].IsDense&&oldPlan[3].EncodedValues[8]==7&&oldPlan[3].EncodedValues[7]==5&&
                    oldPlan[0].Format==AnimDataFormat.I4&&oldPlan[6].Format==AnimDataFormat.I4,
                    "Authoring storage v"+version+": independent original has distinct hidden terminal and compact scale/translation groups");
                string scale=oldPlan[0].ContentKey,translation=oldPlan[6].ContentKey;
                var before=capture.Invoke(null,new object[]{master,new[]{entry.Name}});
                var authored=OptimizationTrack(entry).Channels;
                foreach(var key in authored[3].Where(k=>k.Frame==7)){key.Value=6.25f;key.Tangent=0;}
                write.Invoke(null,new object[]{entry,authored});
                var newPlan=PreciseCHR0EntryNode.CaptureStorage(entry);
                Check(newPlan!=null&&newPlan[3]==null&&newPlan[0].ContentKey==scale&&newPlan[6].ContentKey==translation&&
                    oldPlan[3].EncodedValues[8]==7&&sourceEntry.KeyArrays[3].Entries.Last()._value==5,
                    "Authoring storage v"+version+": explicit changed rotation releases only its obsolete payload; unchanged S/T and original hidden terminal remain exact");
                string edited=NativeAnimationOptimization.SourceContentKey(master),editedStorage=NativeAnimationOptimization.StorageKey(master);
                var after=capture.Invoke(null,new object[]{master,new[]{entry.Name}});
                string result=Path.Combine(output,"authoring-v"+version+"-result.chr0");master.ExportChecked(result);
                AuthoredStorageReopen(result,edited,editedStorage,scale,translation,false,version,"new master");
                Check(entry.GetFrameValue(3,7)==6.25f,"Authoring storage v"+version+": new full-quality master owns the explicitly authored final pose");
                restore.Invoke(before,new object[]{master});
                Check(NativeAnimationOptimization.SourceContentKey(master)==original&&NativeAnimationOptimization.StorageKey(master)==originalStorage&&
                    PreciseCHR0EntryNode.CaptureStorage((CHR0EntryNode)master.Children.Single())[3].ContentKey==oldPlan[3].ContentKey,
                    "Authoring storage v"+version+": existing snapshot restore recovers exact original tuples, flags and full dense plan");
                string undone=Path.Combine(output,"authoring-v"+version+"-undo.chr0");master.ExportChecked(undone);
                AuthoredStorageReopen(undone,original,originalStorage,scale,translation,true,version,"Undo primitive");
                restore.Invoke(after,new object[]{master});
                Check(NativeAnimationOptimization.SourceContentKey(master)==edited&&NativeAnimationOptimization.StorageKey(master)==editedStorage,
                    "Authoring storage v"+version+": Redo primitive recovers the new master and explicit rotation I12 policy without solving");
                string redone=Path.Combine(output,"authoring-v"+version+"-redo.chr0");master.ExportChecked(redone);
                AuthoredStorageReopen(redone,edited,editedStorage,scale,translation,false,version,"Redo primitive");
                foreach(bool cancel in new[]{true,false})
                {
                    bool discarded=false;string unpublished=Path.Combine(output,"authoring-v"+version+(cancel?"-cancelled":"-failed")+".chr0");
                    try
                    {
                        using(var prepared=NativeAnimationOptimization.CopySource(source))
                        {
                            var preparedEntry=(CHR0EntryNode)prepared.Children.Single();
                            write.Invoke(null,new object[]{preparedEntry,authored});
                            if(cancel)throw new OperationCanceledException("Controlled prepared-copy cancellation before publication.");
                            throw new InvalidDataException("Controlled prepared-copy failure before publication.");
                        }
                    }
                    catch(OperationCanceledException){discarded=cancel;}
                    catch(InvalidDataException){discarded=!cancel;}
                    Check(discarded&&!File.Exists(unpublished)&&NativeAnimationOptimization.SourceContentKey(source)==sourceContent&&
                        NativeAnimationOptimization.StorageKey(source)==sourceStorage&&source.IsDirty==sourceDirty&&ReferenceEquals(sourceEntry.Parent,sourceOwner)&&
                        FighterProject.HashFile(sourcePath)==sourceHash,
                        "Authoring storage v"+version+": discarded "+(cancel?"cancelled":"failed")+" independent preparation leaves original content/storage/ownership/dirty state and bytes unchanged");
                }
                File.WriteAllText(Path.Combine(output,"authoring-v"+version+"-receipt.json"),new JavaScriptSerializer().Serialize(new
                {
                    Version=version,OriginalSha256=sourceHash,OriginalHiddenTerminal=7,NewAuthoredFinalPose=6.25,
                    BeforeContent=original,AfterContent=edited,BeforeStorage=originalStorage,AfterStorage=editedStorage,
                    ScalePayload=scale,TranslationPayload=translation,ResultSha256=FighterProject.HashFile(result),
                    UndoSha256=FighterProject.HashFile(undone),RedoSha256=FighterProject.HashFile(redone),
                    Scope="Existing UI snapshot/write/restore authoring primitives and native fresh-reopen only. Actual native Undo commands, solver/contact acceptance and asynchronous cancellation remain covered by their separate FitReferences/Contact/Apply suites. Compression must not invoke this authoring reset."
                }));
            }
            Check(FighterProject.HashFile(sourcePath)==sourceHash,"Authoring storage v"+version+": all operations preserve the exact immutable original native file");
        }
    }

    static string AuthoredStorageFixture(string output,int version)
    {
        string seed=EditedDenseMixedFixture(output,"authoring-seed-v"+version,version,true,true);
        string path=Path.Combine(output,"authoring-original-v"+version+".chr0");
        using(var source=NodeFactory.FromFile(null,seed) as CHR0Node)
        using(var owned=NativeAnimationOptimization.CopySource(source))
        {
            var entry=(CHR0EntryNode)owned.Children.Single();var previous=PreciseCHR0EntryNode.CaptureStorage(entry);
            var plans=Enumerable.Range(0,9).Select(c=>previous[c]).ToArray();entry.UseModelScale=false;
            var scale=IndexedCHR0CurveStorage.Create(AnimDataFormat.I4,Enumerable.Range(0,8).ToArray(),Enumerable.Range(0,8).ToArray(),Enumerable.Repeat(4,8).ToArray(),1,.125f);
            for(int axis=0;axis<3;axis++)
            {
                for(int frame=0;frame<8;frame++)entry.Keyframes.SetFrameValue(axis,frame,1+frame*.125f,true)._tangent=.125f;
                plans[axis]=scale;
            }
            PreciseCHR0EntryNode.RestoreStorage(entry,new IndexedCHR0EntryStorage(plans));owned.ExportChecked(path);
        }
        return path;
    }

    static void AuthoredStorageReopen(string path,string content,string storage,string scale,string translation,bool expectDense,int version,string phase)
    {
        using(var fresh=NodeFactory.FromFile(null,path) as CHR0Node)
        {
            Check(fresh!=null&&NativeAnimationOptimization.SourceContentKey(fresh)==content&&NativeAnimationOptimization.StorageKey(fresh)==storage,
                "Authoring storage v"+version+" "+phase+": fresh resource retains complete metadata, native tuples, flags and intended storage");
            var plan=PreciseCHR0EntryNode.CaptureStorage((CHR0EntryNode)fresh.Children.Single());
            Check(plan?[0]?.ContentKey==scale&&plan[6]?.ContentKey==translation&&
                (expectDense?plan[3]?.IsDense==true&&plan[3].EncodedValues[8]==7:plan[3]==null&&!IndexedCHR0EntryStorage.HasEncodedDenseRotation((CHR0EntryNode)fresh.Children.Single())),
                "Authoring storage v"+version+" "+phase+": original S/T payload bytes survive and rotation has the expected complete-terminal or I12 policy");
        }
    }
}
