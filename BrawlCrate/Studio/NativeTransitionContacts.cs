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
        internal static bool HasBakedSupport(FighterAsset asset)
        {
            var proof=asset.Retarget;
            return proof!=null&&(proof.PlantedFootSamples>0||proof.PlantedHandSamples>0||proof.PlantedBodySamples>0||proof.LockedContactSamples>0||proof.SuspendedSupportEnabled);
        }
        internal static string[] Resolve(MDL0Node model,FighterAsset asset,bool preserve)
        {
            var proof=asset.Retarget;
            if(!HasBakedSupport(asset))return new string[0];
            if(!preserve)throw new NotSupportedException("This retargeted clip has baked support contacts. Enable Preserve existing contact trajectories. Reviewed hip / leg recovery can be explicitly enabled inside explicitly selected start/end windows; general support repositioning still needs a coupled contact adapter. Original animation and pins remain unchanged.");
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

        internal static string[] ReviewExitRecovery(TransitionFitOptions options,int frames,bool loop,string[] contactBones,ISet<string> protectedMotion,bool currentBinding)
        {
            if(options.PreventFootDipping&&(!options.ExitContactRecovery&&!options.BoundaryContactRecovery||!options.FloorReviewed||double.IsNaN(options.FloorHeight)||double.IsInfinity(options.FloorHeight)||float.IsInfinity((float)options.FloorHeight)))throw new InvalidOperationException("Preventing extra foot dipping requires exit contact recovery and explicit review of the finite native floor plane and authentic source/reference penetration baseline.");
            if(!options.ExitContactRecovery&&!options.BoundaryContactRecovery)return new string[0];
            if(options.ExitContactRecovery&&options.BoundaryContactRecovery)throw new InvalidOperationException("Use either legacy exit recovery or start/end recovery.");
            if(loop)throw new NotSupportedException("Exit contact recovery requires a non-looping attack. Stored-loop wrap adaptation remains separate work.");
            if(!currentBinding||protectedMotion==null)throw new InvalidOperationException("Set up the current animation controls and review this exact native rig before enabling exit contact recovery.");
            if(options.BoundaryContactRecovery&&(!options.PreserveContacts||options.Components!=7||options.Entry==null&&options.Exit==null||options.Entry!=null&&(!options.EntryFrozen||options.EntryFrame!=0||options.EntrySamples<2||options.EntrySamples>=frames)||options.Exit!=null&&(!options.ExitFrozen||options.ExitFrame!=0||options.ExitSamples<2||options.ExitSamples>=frames)||(options.Entry==null?0:options.EntrySamples-1)>=(options.Exit==null?frames-1:frames-options.ExitSamples)))throw new InvalidOperationException("Start/end recovery requires non-overlapping ranges, all components and the frozen first frame of each target pose.");
            if(options.ExitContactRecovery&&(!options.PreserveContacts||options.Entry!=null||options.Exit==null||!options.ExitFrozen||options.ExitFrame!=0||options.Components!=7||options.ExitSamples<2||options.ExitSamples>=frames))
                throw new InvalidOperationException("Exit contact recovery requires preserved contacts, no entry fit, all scale/rotation/translation components and the frozen first frame of an exact exit reference.");
            var released=contactBones.Where(name=>!protectedMotion.Contains(name)).ToArray();
            if(released.Length==0)throw new InvalidOperationException("No reviewed body contact paths are available for exit recovery. Use ordinary FK fitting without contact recovery.");
            if(options.Bones==null||released.Any(name=>!options.Bones.Contains(name,StringComparer.Ordinal)))throw new InvalidOperationException("Review and select every affected contact body bone before exit recovery. Protected motion controls remain excluded. Required body paths: "+string.Join(", ",released));
            return released;
        }
    }
}
