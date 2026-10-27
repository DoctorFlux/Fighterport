using System;
using System.Reflection;
using System.Linq;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Models;
using BrawlLib.Internal;
using BrawlLib.Wii.Animations;

// Derived curves default to I12. Explicit immutable, independently qualified
// storage plans retain their complete payload and decoded native coefficients.
public sealed unsafe class PreciseCHR0EntryNode : CHR0EntryNode
{
    public IndexedCHR0EntryStorage IndexedStorage { get; private set; }
    public static IndexedCHR0EntryStorage CaptureStorage(CHR0EntryNode entry)
    {
        // An explicit precise policy, including null after native Undo, is
        // authoritative. Its old working buffer may still contain a matching
        // indexed payload from an earlier Save; never re-adopt that stale plan.
        var precise = entry as PreciseCHR0EntryNode;
        return precise != null ? precise.IndexedStorage : IndexedCHR0EntryStorage.Capture(entry);
    }

    // Explicit immutable storage plans are separate from the curve coefficients.
    // A mismatched plan is ignored by the writer, preserving I12 as the default.
    public static CHR0EntryNode RestoreStorage(CHR0EntryNode entry, IndexedCHR0EntryStorage storage)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        var precise = entry as PreciseCHR0EntryNode;
        if (precise == null)
        {
            precise = MakePrecise(entry);
            int index = entry.Index; var parent = entry.Parent;
            if (parent == null) throw new InvalidOperationException("Storage policy requires an owned native animation entry.");
            entry.Remove(); parent.InsertChild(precise, index);
        }
        precise.IndexedStorage = storage; precise.SignalPropertyChange(); return precise;
    }

    private static PreciseCHR0EntryNode MakePrecise(CHR0EntryNode source)
    {
        var precise=new PreciseCHR0EntryNode{Name=source.Name,UseModelScale=source.UseModelScale,UseModelRotate=source.UseModelRotate,
            UseModelTranslate=source.UseModelTranslate,ScaleCompensateApply=source.ScaleCompensateApply,
            ScaleCompensateParent=source.ScaleCompensateParent,ClassicScaleOff=source.ClassicScaleOff};
        var keys=typeof(CHR0EntryNode).GetField("_keyframes",BindingFlags.NonPublic|BindingFlags.Instance)
            ??throw new NotSupportedException("Pinned CHR0 key collection field is unavailable.");
        keys.SetValue(precise,source.Keyframes); return precise;
    }
    // Retain the original CHR0 and its native metadata. Only the child encoder
    // policy changes; key collections and all exposed semantic flags survive.
    public static void Promote(CHR0Node clip)
    {
        foreach(CHR0EntryNode source in clip.Children.ToArray())
        {
            if(source is PreciseCHR0EntryNode)continue;
            var precise=MakePrecise(source);
            int index=source.Index;source.Remove();clip.InsertChild(precise,index);
        }
    }
    static FieldInfo Field(string name)=>typeof(CHR0EntryNode).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)??throw new NotSupportedException("Pinned CHR0 writer field unavailable: "+name);
    public override int OnCalculateSize(bool force)
    {
        base.OnCalculateSize(force);var code=(AnimationCode)Field("_code").GetValue(this);int bytes=0;
        for(int group=0;group<3;group++)
        {
            if(!code.GetExists(group))continue;
            int channels=group==0&&code.GetIsIsotropic(group)?1:3;bool varying=false;
            bool packed=IndexedStorage!=null&&IndexedStorage.MatchesGroup(Keyframes,group);
            if(packed&&channels==1&&(!IndexedStorage[group*3].Matches(KeyArrays[group*3+1])||!IndexedStorage[group*3].Matches(KeyArrays[group*3+2])))packed=false;
            AnimDataFormat format=packed?IndexedStorage.Format(group):AnimDataFormat.I12;
            for(int axis=0;axis<channels;axis++)
            {
                int ch=group*3+axis;
                // A one-frame imported baked payload can still own a distinct
                // encoded terminal sample. Preserve its variable table cell.
                if(packed&&IndexedStorage[ch]!=null&&IndexedStorage[ch].IsDense)code.SetIsFixed(ch,false);
                if(code.GetIsFixed(ch))continue;varying=true;int count=(int)KeyArrays[ch].GetType().GetField("_keyCount",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(KeyArrays[ch]);bytes+=packed?IndexedStorage[ch].ByteCount:8+count*12;
            }
            if(varying)code.SetFormat(group,format);
        }
        Field("_code").SetValue(this,code);Field("_dataLen").SetValue(this,bytes);return bytes+(int)Field("_entryLen").GetValue(this);
    }
    public override void OnRebuild(VoidPtr address,int length,bool force)
    {
        // The historical baked writer advances by the native decoded sample
        // count (N for non-loop clips). The format stores N+1 samples regardless
        // of looping. Explicit baked plans therefore own this narrow layout;
        // indexed/precise-only entries retain their original writer path.
        if(IndexedStorage!=null&&IndexedStorage.MatchesGroup(Keyframes,1)&&
            (IndexedStorage.Format(1)==AnimDataFormat.L1||IndexedStorage.Format(1)==AnimDataFormat.L2||IndexedStorage.Format(1)==AnimDataFormat.L4))
        { RebuildExplicitDense(address);return; }
        base.OnRebuild(address,length,force);var code=(AnimationCode)Field("_code").GetValue(this);byte* table=(byte*)address+8;
        for(int group=0;group<3;group++)if(code.GetExists(group))
        {
            int columns=group==0&&code.GetIsIsotropic(group)?1:3;
            for(int axis=0;axis<columns;axis++,table+=4)
            {
                int channel=group*3+axis;var array=KeyArrays[channel];var root=(KeyframeEntry)array.GetType().GetField("_keyRoot",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(array);
                if(code.GetIsFixed(channel)){WriteFloat(table,root._next._value);continue;}
                byte* data=(byte*)address+*(bint*)table;
                if(code.GetFormat(group)!=AnimDataFormat.I12)
                {
                    if(IndexedStorage==null||!IndexedStorage.MatchesGroup(Keyframes,group)||IndexedStorage[channel]==null||IndexedStorage[channel].Format!=code.GetFormat(group))
                        throw new InvalidOperationException("The native indexed storage plan changed during serialization.");
                    IndexedStorage[channel].Write(data);continue;
                }
                int span=root._prev._index-root._next._index;WriteFloat(data+4,span==0?0:.99999994f/span);byte* row=data+8;
                for(var key=root._next;key!=root;key=key._next,row+=12){WriteFloat(row,key._index);WriteFloat(row+4,key._value);WriteFloat(row+8,key._tangent);}
            }
        }
    }
    private void RebuildExplicitDense(VoidPtr address)
    {
        RegenCode();var code=(AnimationCode)Field("_code").GetValue(this);
        byte* table=(byte*)address+8;byte* data=(byte*)(VoidPtr)Field("_dataAddr").GetValue(this);
        if(data==null)throw new InvalidOperationException("Explicit baked storage requires its owned native resource layout.");
        *(buint*)address=0;*(buint*)((byte*)address+4)=(uint)code;
        for(int group=0;group<3;group++)if(code.GetExists(group))
        {
            int columns=group==0&&code.GetIsIsotropic(group)?1:3;
            for(int axis=0;axis<columns;axis++,table+=4)
            {
                int channel=group*3+axis;var array=KeyArrays[channel];
                var root=(KeyframeEntry)array.GetType().GetField("_keyRoot",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(array);
                if(code.GetIsFixed(channel)){WriteFloat(table,root._next._value);continue;}
                *(bint*)table=(int)(data-(byte*)address);
                if(code.GetFormat(group)!=AnimDataFormat.I12)
                {
                    if(!IndexedStorage.MatchesGroup(Keyframes,group)||IndexedStorage[channel]==null||IndexedStorage[channel].Format!=code.GetFormat(group))
                        throw new InvalidOperationException("The explicit storage plan changed during serialization.");
                    IndexedStorage[channel].Write(data);data+=IndexedStorage[channel].ByteCount;continue;
                }
                *(bushort*)data=(ushort)array.KeyCount;*(bushort*)(data+2)=0;
                int span=root._prev._index-root._next._index;WriteFloat(data+4,span==0?0:.99999994f/span);byte* row=data+8;
                for(var key=root._next;key!=root;key=key._next,row+=12){WriteFloat(row,key._index);WriteFloat(row+4,key._value);WriteFloat(row+8,key._tangent);}
                data=row;
            }
        }
    }
    static void WriteFloat(byte* p,float value){uint bits=*(uint*)&value;p[0]=(byte)(bits>>24);p[1]=(byte)(bits>>16);p[2]=(byte)(bits>>8);p[3]=(byte)bits;}
}
