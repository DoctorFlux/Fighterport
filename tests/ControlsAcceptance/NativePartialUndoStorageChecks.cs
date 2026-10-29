using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.Internal;
using BrawlLib.Internal.IO;
using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Animations;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

internal static partial class Program
{
    // Reproduce the ownership transition made by native single-bone Undo:
    // one imported entry becomes precise/unbacked while its sampled siblings
    // still refer to the original raw resource. The raw dictionary, not the
    // mutable managed child list, defines their original payload boundaries.
    static void NativePartialUndoStorageChecks(string output)
    {
        if(!PrivateDesktop)throw new InvalidOperationException("Partial native Undo storage qualification requires the private desktop.");
        Directory.CreateDirectory(output);
        var snapshotType=typeof(ModelForm).GetNestedType("NativeControlSnapshot",BindingFlags.NonPublic);
        var capture=snapshotType.GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static);
        var restore=snapshotType.GetMethod("Restore",BindingFlags.NonPublic|BindingFlags.Instance);
        foreach(int version in new[]{4,5})
        {
            string path=PartialUndoStorageFixture(output,version),fileHash=FighterProject.HashFile(path);
            using(var clip=NodeFactory.FromFile(null,path) as CHR0Node)
            {
                var edited=(CHR0EntryNode)clip.Children[0];var sibling=(CHR0EntryNode)clip.Children[1];
                var siblingAddress=sibling.WorkingUncompressed.Address;
                var siblingPlan=PreciseCHR0EntryNode.CaptureStorage(sibling);
                string siblingPayload=siblingPlan.ContentKey,original=NativeAnimationOptimization.SourceContentKey(clip);
                Check(!(edited is PreciseCHR0EntryNode)&&!(sibling is PreciseCHR0EntryNode)&&sibling.WorkingUncompressed.Length==0&&
                    siblingPlan[3].EncodedValues[8]==7&&siblingPlan[3].EncodedValues[7]==5,
                    "Partial Undo v"+version+": two authentic raw entries own a distinct hidden ninth sample and normal zero-length child sources");
                var before=capture.Invoke(null,new object[]{clip,new[]{edited.Name}});
                EditDenseInterior(edited);
                var after=capture.Invoke(null,new object[]{clip,new[]{edited.Name}});
                string changed=NativeAnimationOptimization.SourceContentKey(clip);
                Check(changed!=original&&PreciseCHR0EntryNode.CaptureStorage(edited)==null,
                    "Partial Undo v"+version+": native-style interior edit has a separate post-edit snapshot without an adopted old sampled plan");
                foreach(bool undo in new[]{true,false})
                {
                    restore.Invoke(undo?before:after,new object[]{clip});
                    edited=(CHR0EntryNode)clip.Children[0];
                    string content=NativeAnimationOptimization.SourceContentKey(clip);
                    Check(edited is PreciseCHR0EntryNode&&edited.WorkingUncompressed.Address==null&&ReferenceEquals(sibling,clip.Children[1])&&
                        !(sibling is PreciseCHR0EntryNode)&&sibling.WorkingUncompressed.Address==siblingAddress&&content==(undo?original:changed),
                        "Partial Undo v"+version+": actual snapshot "+(undo?"Undo":"Redo")+" changes only the edited entry's ownership/policy and exact intended native content");
                    var retained=PreciseCHR0EntryNode.CaptureStorage(sibling);
                    var write=PreciseCHR0EntryNode.CaptureStorageForWrite(sibling);
                    Check(retained!=null&&retained.ContentKey==siblingPayload&&write.Accepted&&!write.UsesDenseI12Fallback&&
                        write.Storage.ContentKey==siblingPayload&&retained[3].EncodedValues[8]==7,
                        "Partial Undo v"+version+": untouched sampled sibling remains bounded by original dictionary tables after "+(undo?"Undo":"Redo"));
                    string exported=Path.Combine(output,"partial-undo-v"+version+(undo?"-undo":"-redo")+".chr0");
                    using(var copy=NativeAnimationOptimization.CopySource(clip))copy.ExportChecked(exported);
                    using(var fresh=NodeFactory.FromFile(null,exported) as CHR0Node)
                        Check(NativeAnimationOptimization.SourceContentKey(fresh)==content&&
                            PreciseCHR0EntryNode.CaptureStorage((CHR0EntryNode)fresh.Children[1]).ContentKey==siblingPayload,
                            "Partial Undo v"+version+": independent export/reopen preserves current native motion plus the untouched complete sampled terminal");
                    Check(ReferenceEquals(sibling,clip.Children[1])&&sibling.WorkingUncompressed.Address==siblingAddress&&
                        NativeAnimationOptimization.SourceContentKey(clip)==content,
                        "Partial Undo v"+version+": read-only storage capture and export staging do not replace live siblings or alter authored keys");
                }
                File.WriteAllText(Path.Combine(output,"partial-undo-v"+version+"-receipt.json"),new JavaScriptSerializer().Serialize(new
                {
                    Version=version,OriginalSha256=fileHash,OriginalContent=original,EditedContent=changed,
                    UntouchedPayload=siblingPayload,UntouchedHiddenTerminal=7,
                    Scope="Existing single-entry UI snapshot Undo/Redo primitives, immutable raw-dictionary layout and native fresh reopen. No new codec, quality limit or game/runtime compatibility claim."
                }));
            }
            PartialUndoRawVersionChecks(path,version);
            foreach(string invalid in new[]{"dictionary-count","dictionary-entry-offset","curve-offset","declared-span","unrepresented-entry"}.Concat(version==5?new[]{"user-data-offset"}:new string[0]))
                PartialUndoRejectMalformedLayout(path,version,invalid);
            Check(FighterProject.HashFile(path)==fileHash,"Partial Undo v"+version+": all ownership and malformed-layout cases preserve the exact original native file");
        }
    }

    static string PartialUndoStorageFixture(string output,int version)
    {
        string seed=AuthoredStorageFixture(output,version),path=Path.Combine(output,"partial-undo-original-v"+version+".chr0");
        using(var source=NodeFactory.FromFile(null,seed) as CHR0Node)
        using(var owned=NativeAnimationOptimization.CopySource(source))
        {
            if(version==5)owned.UserEntries.Add(new UserDataClass{Name="fixture-boundary",DataType=UserValueType.Int,Entries=new[]{"17"}});
            var first=(CHR0EntryNode)owned.Children.Single();first.Name="FixtureEdited";
            var second=owned.CreateEntry("FixtureUntouched");
            second.UseModelScale=first.UseModelScale;second.UseModelRotate=first.UseModelRotate;second.UseModelTranslate=first.UseModelTranslate;
            second.ScaleCompensateApply=first.ScaleCompensateApply;second.ScaleCompensateParent=first.ScaleCompensateParent;second.ClassicScaleOff=first.ClassicScaleOff;
            for(int channel=0;channel<9;channel++)foreach(var key in first.KeyArrays[channel].Entries)
                second.Keyframes.SetFrameValue(channel,key._index,key._value,true)._tangent=key._tangent;
            second.Keyframes.PreserveInterpolation=true;
            PreciseCHR0EntryNode.RestoreStorage(second,PreciseCHR0EntryNode.CaptureStorage(first));
            owned.ExportChecked(path);
        }
        return path;
    }

    static void PartialUndoRawVersionChecks(string path,int version)
    {
        using(var clip=NodeFactory.FromFile(null,path) as CHR0Node)
        {
            var sibling=(CHR0EntryNode)clip.Children[1];var expected=PreciseCHR0EntryNode.CaptureStorage(sibling);
            Check(expected!=null&&(version!=5||clip.UserEntries.Count==1),
                "Partial Undo raw v"+version+": independently written source has its expected complete layout and raw user-data boundary");
            byte[] original=new byte[clip.WorkingUncompressed.Length];Marshal.Copy((IntPtr)clip.WorkingUncompressed.Address,original,0,original.Length);
            clip.Version=version==4?5:4;bool dirty=clip.IsDirty;
            var actual=PreciseCHR0EntryNode.CaptureStorage(sibling);
            byte[] after=new byte[original.Length];Marshal.Copy((IntPtr)clip.WorkingUncompressed.Address,after,0,after.Length);
            Check(actual!=null&&actual.ContentKey==expected.ContentKey&&actual[3].EncodedValues[8]==7&&clip.IsDirty==dirty&&original.SequenceEqual(after),
                "Partial Undo raw v"+version+" / managed v"+clip.Version+": read-only layout uses raw header semantics, retaining its complete terminal without interpreting a v4 name as v5 user data");
        }
    }

    static unsafe void PartialUndoRejectMalformedLayout(string path,int version,string invalid)
    {
        // NodeFactory.FromFile uses a read-only fixture view. Mutation belongs
        // only to this independent, explicitly writable temporary map. Decode
        // all valid native children/keys before changing its raw layout fields.
        using(var clip=PartialUndoWritableFixture(path))
        {
            var sibling=(CHR0EntryNode)clip.Children[1];var owner=sibling.Parent;
            string content=NativeAnimationOptimization.SourceContentKey(clip),authored=LifecycleContent(clip);bool dirty=clip.IsDirty;
            var address=clip.WorkingUncompressed.Address;byte[] bytes=new byte[clip.WorkingUncompressed.Length];
            Marshal.Copy((IntPtr)address,bytes,0,bytes.Length);
            int dictionary=(int)DenseRead32(bytes,0x10),target;
            int value;
            if(invalid=="dictionary-count"){target=dictionary+4;value=-1;}
            else if(invalid=="dictionary-entry-offset"){target=dictionary+0x18+0x0c;value=0x7ffffff0;}
            else if(invalid=="curve-offset")
            {
                int entry=dictionary+(int)DenseRead32(bytes,dictionary+0x18+0x0c);
                target=entry+8;value=0x7ffffff0;
            }
            else if(invalid=="declared-span"){target=4;value=version==4?0x28:0x2c;}
            else if(invalid=="user-data-offset"){target=0x14;value=bytes.Length-4;}
            else
            {
                // A second raw entry pointing at another valid table is not
                // evidence that this current sibling belongs to that layout.
                target=dictionary+0x18+0x10+0x0c;value=DenseRead32(bytes,dictionary+0x18+0x0c);
            }
            int saved=DenseRead32(bytes,target);DenseWrite32(bytes,target,value);
            Marshal.Copy(bytes,0,(IntPtr)address,bytes.Length);
            try
            {
                var result=PreciseCHR0EntryNode.CaptureStorageForWrite(sibling);
                bool failed=false;
                try{using(var copy=NativeAnimationOptimization.CopySource(clip)) { }}
                catch(InvalidDataException){failed=true;}
                catch(NotSupportedException){failed=true;}
                var after=new byte[bytes.Length];Marshal.Copy((IntPtr)address,after,0,after.Length);
                Check(!result.Accepted&&result.Storage==null&&failed&&ReferenceEquals(sibling.Parent,owner)&&
                    LifecycleContent(clip)==authored&&clip.IsDirty==dirty&&after.SequenceEqual(bytes),
                    "Partial Undo v"+version+": bounded raw layout rejects "+invalid+" without publishing or changing native content/ownership/dirty state");
            }
            finally
            {
                DenseWrite32(bytes,target,saved);Marshal.Copy(bytes,0,(IntPtr)address,bytes.Length);
            }
            Check(PreciseCHR0EntryNode.CaptureStorageForWrite(sibling).Accepted&&NativeAnimationOptimization.SourceContentKey(clip)==content,
                "Partial Undo v"+version+": restoring the literal layout restores qualification and complete native content after "+invalid);
        }
    }

    static CHR0Node PartialUndoWritableFixture(string path)
    {
        byte[] bytes=File.ReadAllBytes(path);var map=FileMap.FromTempFile(bytes.Length);
        try
        {
            Marshal.Copy(bytes,0,(IntPtr)map.Address,bytes.Length);
            var clip=NodeFactory.FromSource(null,new DataSource(map),typeof(CHR0Node),false) as CHR0Node;
            if(clip==null)throw new InvalidDataException("The valid independent writable CHR0 fixture could not be decoded.");
            return clip;
        }
        catch{map.Dispose();throw;}
    }
}
