using BrawlCrate.Studio.Retargeting;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BrawlCrate.Studio
{
    internal static class NativeTransitionContacts
    {
        internal static string[] Resolve(MDL0Node model,FighterAsset asset,bool preserve)
        {
            var proof=asset.Retarget;
            if(proof==null||!(proof.PlantedFootSamples>0||proof.PlantedHandSamples>0||proof.PlantedBodySamples>0||proof.LockedContactSamples>0||proof.SuspendedSupportEnabled))return new string[0];
            if(!preserve)throw new NotSupportedException("This retargeted clip has baked support contacts. Enable Preserve existing contact trajectories. Repositioning or releasing support still needs a coupled contact adapter. Original animation and pins remain unchanged.");
            if(proof.Algorithm!=HumanoidRetargeter.Algorithm||!string.IsNullOrEmpty(proof.EngineReceipt)||asset.RigIdentity!=BrawlRetargeter.SkeletonFingerprint(model))throw new InvalidDataException("Support protection requires the exact original Next target rig and its reviewed native mapping.");
            // Reuse the original native support regions and ALL their skin
            // influences. Counts do not identify individual intervals, so freeze
            // both sides for the whole clip rather than inventing a timeline.
            var captured=BrawlRetargeter.Capture(model,proof.Mapping.ToDictionary(m=>m.Role,m=>m.Target));
            var roles=captured.Rig.Surfaces.Keys.Where(role=>proof.PlantedBodySamples>0||proof.LockedContactSamples>0&&proof.PlantedFootSamples==0&&proof.PlantedHandSamples==0||role.EndsWith(".foot",StringComparison.Ordinal)&&proof.PlantedFootSamples>0||role.EndsWith(".hand",StringComparison.Ordinal)&&(proof.PlantedHandSamples>0||proof.SuspendedSupportEnabled)).ToArray();
            if(roles.Length==0)throw new InvalidDataException("No complete native support regions were available. Keep this result unchanged until its context is reviewed.");
            var bones=new HashSet<string>(StringComparer.Ordinal);
            foreach(string role in roles)
            {
                foreach(int index in captured.Rig.Surfaces[role].Points.SelectMany(p=>p.Weights).Where(w=>w.Weight>0).Select(w=>w.Joint).Concat(captured.Rig.Roles.TryGetValue(role,out int joint)?new[]{joint}:new int[0]))
                    for(var bone=captured.Bones[index];bone!=null;bone=bone.Parent as MDL0BoneNode)bones.Add(bone.Name);
            }
            if(bones.Count==0)throw new InvalidDataException("Native support regions have no verified deformation dependencies.");
            return captured.Bones.Where(b=>bones.Contains(b.Name)).Select(b=>b.Name).ToArray();
        }
    }
}
