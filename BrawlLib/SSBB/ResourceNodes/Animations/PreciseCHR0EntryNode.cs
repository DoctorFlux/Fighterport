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
    private sealed class SerializationPlan
    {
        internal readonly AnimationCode Code;
        internal readonly int EntryBytes, DataBytes, FrameLimit, Flags;
        internal readonly string Name;
        internal readonly bool Loop;
        internal readonly IndexedCHR0EntryStorage Storage;
        internal readonly int[][] Coefficients;
        internal SerializationPlan(PreciseCHR0EntryNode entry, AnimationCode code, int entryBytes, int dataBytes)
        {
            Code=code; EntryBytes=entryBytes; DataBytes=dataBytes; Name=entry.Name;
            FrameLimit=entry.Keyframes.FrameLimit; Loop=entry.Keyframes.Loop; Flags=entry.SemanticFlags(); Storage=entry.IndexedStorage;
            Coefficients=entry.KeyArrays.Select(array=>array.Entries.SelectMany(key=>new[]{Bits(key._index),Bits(key._value),Bits(key._tangent)}).ToArray()).ToArray();
        }
        internal bool Matches(PreciseCHR0EntryNode entry)
        {
            if(Name!=entry.Name||FrameLimit!=entry.Keyframes.FrameLimit||Loop!=entry.Keyframes.Loop||Flags!=entry.SemanticFlags()||!ReferenceEquals(Storage,entry.IndexedStorage))return false;
            for(int channel=0;channel<9;channel++)
            {
                var expected=Coefficients[channel];int index=0;
                foreach(var key in entry.KeyArrays[channel].Entries)
                {
                    if(index+3>expected.Length||expected[index++]!=Bits(key._index)||expected[index++]!=Bits(key._value)||expected[index++]!=Bits(key._tangent))return false;
                }
                if(index!=expected.Length)return false;
            }
            return true;
        }
    }
    private SerializationPlan _serializationPlan;
    private static int Bits(float value)=>BitConverter.ToInt32(BitConverter.GetBytes(value),0);
    private int SemanticFlags()=>(UseModelScale?1:0)|(UseModelRotate?2:0)|(UseModelTranslate?4:0)|(ScaleCompensateApply?8:0)|(ScaleCompensateParent?16:0)|(ClassicScaleOff?32:0);
    private bool ExactIsotropicScale()
    {
        var a=KeyArrays[0].Entries.ToArray();
        for(int axis=1;axis<3;axis++)
        {
            var b=KeyArrays[axis].Entries.ToArray();if(a.Length!=b.Length)return false;
            for(int i=0;i<a.Length;i++)if(Bits(a[i]._index)!=Bits(b[i]._index)||Bits(a[i]._value)!=Bits(b[i]._value)||Bits(a[i]._tangent)!=Bits(b[i]._tangent))return false;
        }
        return true;
    }
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
        // The legacy heuristic compares only scale times/values, which can
        // merge different Hermite functions or signed-zero data. This precise
        // policy requires complete binary coefficient identity before sharing.
        int entryBytes=(int)Field("_entryLen").GetValue(this);
        if(code.GetExists(0)&&code.GetIsIsotropic(0)&&!ExactIsotropicScale())
        {code.SetIsIsotropic(0,false);entryBytes=checked(entryBytes+8);}
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
                var only=KeyArrays[ch].Entries.Take(2).ToArray();
                // Fixed slots have no time/tangent fields. Retain a deliberately
                // timed or nonzero-slope singleton as legal I12 instead.
                if(only.Length==1&&(Bits(only[0]._index)!=0||Bits(only[0]._tangent)!=0))
                {
                    code.SetIsFixed(ch,false);
                    // Isotropic scale is represented by one shared slot, with
                    // the native reader/writer consulting the Z fixed bit.
                    if(channels==1){code.SetIsFixed(ch+1,false);code.SetIsFixed(ch+2,false);}
                }
                // A one-frame imported baked payload can still own a distinct
                // encoded terminal sample. Preserve its variable table cell.
                if(packed&&IndexedStorage[ch]!=null&&IndexedStorage[ch].IsDense)code.SetIsFixed(ch,false);
                if(code.GetIsFixed(ch))continue;varying=true;int count=(int)KeyArrays[ch].GetType().GetField("_keyCount",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(KeyArrays[ch]);
                if(!packed && count>ushort.MaxValue)throw new NotSupportedException("This exact curve exceeds the native indexed knot-count field; retain its dense storage or reduce it before export.");
                bytes+=packed?IndexedStorage[ch].ByteCount:8+count*12;
            }
            if(varying)code.SetFormat(group,format);
        }
        Field("_code").SetValue(this,code);Field("_entryLen").SetValue(this,entryBytes);Field("_dataLen").SetValue(this,bytes);
        _serializationPlan=new SerializationPlan(this,code,entryBytes,bytes);
        return checked(bytes+entryBytes);
    }
    public override void OnRebuild(VoidPtr address,int length,bool force)
    {
        var plan=_serializationPlan;
        if(plan==null||!plan.Matches(this)||(length!=plan.EntryBytes&&length!=plan.EntryBytes+plan.DataBytes))
            throw new InvalidOperationException("CHR0 curves, flags or storage changed between size planning and writing.");
        // The immutable plan, not a second codec search, controls this write.
        Field("_code").SetValue(this,plan.Code);Field("_entryLen").SetValue(this,plan.EntryBytes);Field("_dataLen").SetValue(this,plan.DataBytes);
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
                float span=root._prev._index-root._next._index;WriteFloat(data+4,span==0?0:.99999994f/span);byte* row=data+8;
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
                float span=root._prev._index-root._next._index;WriteFloat(data+4,span==0?0:.99999994f/span);byte* row=data+8;
                for(var key=root._next;key!=root;key=key._next,row+=12){WriteFloat(row,key._index);WriteFloat(row+4,key._value);WriteFloat(row+8,key._tangent);}
                data=row;
            }
        }
    }
    static void WriteFloat(byte* p,float value){uint bits=*(uint*)&value;p[0]=(byte)(bits>>24);p[1]=(byte)(bits>>16);p[2]=(byte)(bits>>8);p[3]=(byte)bits;}
}
