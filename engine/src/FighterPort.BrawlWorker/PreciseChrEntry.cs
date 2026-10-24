using System;
using System.Reflection;
using System.Linq;
using BrawlLib.SSBB.ResourceNodes;
using BrawlLib.Wii.Models;
using BrawlLib.Internal;
using BrawlLib.Wii.Animations;

// Own native data policy: use float I12 keys for derived curves so dense L1/L4 encoding
// cannot regenerate different tangents on reopen. Pinned internal fields are audited.
internal sealed unsafe class PreciseChrEntry : CHR0EntryNode
{
    // Retain the original CHR0 and its native metadata. Only the child encoder
    // policy changes; key collections and all exposed semantic flags survive.
    public static void Promote(CHR0Node clip)
    {
        foreach(CHR0EntryNode source in clip.Children.ToArray())
        {
            if(source is PreciseChrEntry)continue;
            var precise=new PreciseChrEntry{Name=source.Name,UseModelScale=source.UseModelScale,UseModelRotate=source.UseModelRotate,
                UseModelTranslate=source.UseModelTranslate,ScaleCompensateApply=source.ScaleCompensateApply,
                ScaleCompensateParent=source.ScaleCompensateParent,ClassicScaleOff=source.ClassicScaleOff};
            var keys=typeof(CHR0EntryNode).GetField("_keyframes",BindingFlags.NonPublic|BindingFlags.Instance)
                ??throw new NotSupportedException("Pinned CHR0 key collection field is unavailable.");
            keys.SetValue(precise,source.Keyframes);
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
            for(int axis=0;axis<channels;axis++)
            {
                int ch=group*3+axis;if(code.GetIsFixed(ch))continue;varying=true;int count=(int)KeyArrays[ch].GetType().GetField("_keyCount",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(KeyArrays[ch]);bytes+=8+count*12;
            }
            if(varying)code.SetFormat(group,AnimDataFormat.I12);
        }
        Field("_code").SetValue(this,code);Field("_dataLen").SetValue(this,bytes);return bytes+(int)Field("_entryLen").GetValue(this);
    }
    public override void OnRebuild(VoidPtr address,int length,bool force)
    {
        base.OnRebuild(address,length,force);var code=(AnimationCode)Field("_code").GetValue(this);byte* table=(byte*)address+8;
        for(int group=0;group<3;group++)if(code.GetExists(group))
        {
            int columns=group==0&&code.GetIsIsotropic(group)?1:3;
            for(int axis=0;axis<columns;axis++,table+=4)
            {
                int channel=group*3+axis;var array=KeyArrays[channel];var root=(KeyframeEntry)array.GetType().GetField("_keyRoot",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(array);
                if(code.GetIsFixed(channel)){MorphFloatBytes.Write(table,root._next._value);continue;}
                if(code.GetFormat(group)!=AnimDataFormat.I12)throw new NotSupportedException("Derived CHR0 curves require float I12 storage.");
                byte* data=(byte*)address+*(bint*)table;int span=root._prev._index-root._next._index;MorphFloatBytes.Write(data+4,span==0?0:.99999994f/span);byte* row=data+8;
                for(var key=root._next;key!=root;key=key._next,row+=12){MorphFloatBytes.Write(row,key._index);MorphFloatBytes.Write(row+4,key._value);MorphFloatBytes.Write(row+8,key._tangent);}
            }
        }
    }
}
