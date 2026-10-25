using BrawlCrate.Studio;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

internal static partial class Program
{
    static async Task NativePairedKeys(string modelPath,string output)
    {
        string name;int body;
        using(var setupClient=new AnimationControlClient())
        {
            var rig=await setupClient.Send(new{Operation="load",Model=modelPath,Frame=0});body=rig.BodyBone??-1;
            if(body<0)throw new InvalidOperationException("Paired-key editing needs a reviewed body binding.");
            name=rig.BoneNames[body];
        }
        string input=Path.Combine(output,"original-paired.chr0");
        using(var clip=new CHR0Node{Name="OriginalPairedKeys",FrameCount=5,Loop=false})
        {
            var entry=clip.CreateEntry(name);entry.SetKeyframe(6,0,0,true,true)._tangent=0;entry.SetKeyframe(6,2,2,true,true)._tangent=.25f;entry.SetKeyframe(6,2,7,true,true)._tangent=-.5f;entry.SetKeyframe(6,4,9,true,true)._tangent=.125f;entry.SetKeyframe(7,0,0,true,true);entry.SetKeyframe(8,0,0,true,true);PreciseCHR0EntryNode.Promote(clip);clip.Export(input);
        }
        using(var client=new AnimationControlClient())
        {
            var loaded=await client.Send(new{Operation="load",Model=modelPath,Motion=input,Frame=2});var values=loaded.Pose[body].ToArray();values[6]+=1;
            await client.Send(new{Operation="begin",Bone=body,Pin="fk:"+body,Frame=2});await client.Send(new{Operation="preview",Values=values});var result=await client.Send(new{Operation="commit"});
            var pair=result.Tracks.Single(t=>t.Bone==name).Channels[6].Where(k=>k.Frame==2).ToArray();
            Check(pair.Length==2&&pair[0].Value==3&&pair[1].Value==7&&pair[0].Tangent==.25f&&pair[1].Tangent==-.5f,"Control commit preserves a native discontinuity's outgoing value and both exact tangents");
            using(var clip=NodeFactory.FromFile(null,input) as CHR0Node)
            {
                var snapshot=typeof(ModelForm).GetNestedType("NativeControlSnapshot",BindingFlags.NonPublic);snapshot.GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{clip,result.Tracks});string saved=Path.Combine(output,"edited-paired.chr0");clip.Export(saved);
                using(var fresh=NodeFactory.FromFile(null,saved) as CHR0Node){var entries=((CHR0EntryNode)fresh.FindChild(name,false)).KeyArrays[6].Entries.Where(k=>k._index==2).ToArray();Check(entries.Length==2&&entries[0]._value==3&&entries[1]._value==7&&entries[0]._tangent==.25f&&entries[1]._tangent==-.5f,"Fresh native decode preserves both sides of the control-edited key pair");}
            }
        }
    }
}
