using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BrawlCrate.Studio
{
    public sealed class EngineRigReview
    {
        public string Fingerprint {get;set;} public string[] Roles {get;set;} public string[] Bones {get;set;}
        public float[] Right {get;set;} public float[] Up {get;set;} public float[] Forward {get;set;} public string Evidence {get;set;}
        public string[] NativeRoles {get;set;} public string[] NativeBones {get;set;}
        internal object Wire()=>new{Fingerprint,Roles=Roles.Zip(Bones,(r,b)=>new{r,b}).ToDictionary(p=>p.r,p=>p.b),Right,Up,Forward,Evidence};
        internal static string FingerprintOf(MDL0Node model)
        {
            Action<ResourceNode> populate=null;populate=n=>{foreach(var child in n.Children)populate(child);};populate(model);
            var ordered=new List<MDL0BoneNode>();Action<MDL0BoneNode> add=null;add=b=>{if(ordered.Contains(b))return;if(b.Parent is MDL0BoneNode parent)add(parent);ordered.Add(b);};foreach(var bone in model.AllBones)add(bone);
            var bones=ordered.ToArray();using(var stream=new MemoryStream())
            {
                if(bones.Length==0)throw new InvalidDataException("Populate the existing native rig before reviewing it.");
                var transport=new System.Web.Script.Serialization.JavaScriptSerializer();
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))foreach(var bone in bones){writer.Write(bone.Name);writer.Write(Array.IndexOf(bones,bone.Parent as MDL0BoneNode));for(int i=0;i<9;i++){float value=bone.BindState[i];writer.Write(float.Parse(transport.Serialize(value),System.Globalization.CultureInfo.InvariantCulture));}}
                using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","");
            }
        }
    }
}
