using BrawlLib.SSBB;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
namespace BrawlCrate.Studio
{
    // Stable native joint membership is independent of display names and storage
    // ordering. These editor-only IDs never enter an exported native game model.
    internal static class NativeJointIdentity
    {
        private const string Key="Fighterport.NativeJoint";
        internal static void Stamp(MDL0BoneNode bone,string provider,string rig,int joint)
        {bone.UserEntries.Add(new UserDataClass{Name=Key,DataType=UserValueType.String,Entries=new[]{provider+"|"+rig+"|"+joint.ToString(CultureInfo.InvariantCulture)}});bone.SignalPropertyChange();}
        internal static MDL0BoneNode[] Bind(MDL0Node model,string provider,string rig,int count)
        {
            model.Children.ToArray();model.BoneGroup?.Children.ToArray();var result=new MDL0BoneNode[count];
            foreach(var bone in model.AllBones)
            {
                var values=bone.UserEntries.Cast<UserDataClass>().Where(u=>u.Name==Key).ToArray();
                if(values.Length!=1||values[0].Entries.Length!=1)throw new InvalidDataException("An editable native joint lost its exact membership identity: "+bone.Name+"; entries "+bone.UserEntries.Count+"; "+string.Join(";",bone.UserEntries.Cast<UserDataClass>().Select(u=>u.Name+"="+string.Join(",",u.Entries))));
                var parts=values[0].Entries[0].Split('|');int id;
                if(parts.Length!=3||parts[0]!=provider||parts[1]!=rig||!int.TryParse(parts[2],NumberStyles.None,CultureInfo.InvariantCulture,out id)||id<0||id>=count||result[id]!=null)throw new InvalidDataException("Native joint membership is missing, changed or ambiguous.");
                result[id]=bone;
            }
            if(result.Any(b=>b==null))throw new InvalidDataException("An authentic native joint is missing from the editable target.");return result;
        }
    }
}
