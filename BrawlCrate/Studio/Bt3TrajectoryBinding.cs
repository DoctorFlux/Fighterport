using BrawlCrate.Studio.Retargeting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio
{
    // Native BT3 owns its ancestor transforms. An unequal target control
    // chain may receive them only when their authored ownership and order are
    // unambiguous. This policy never infers a BT3 joint role from its name.
    internal static class Bt3TrajectoryBinding
    {
        internal const string Policy="bt3-native-ordered-source-controls-v1";

        internal static IReadOnlyDictionary<int,int> Prepare(Bt3SourceRig source,Rig target)
        {
            if(source==null||target==null)throw new ArgumentNullException(source==null?nameof(source):nameof(target));
            int[] Ancestors(Rig rig)
            {
                var list=new List<int>();
                for(int i=rig.Joints[rig.Roles["pelvis"]].Parent;i>=0;i=rig.Joints[i].Parent)list.Add(i);
                list.Reverse();return list.ToArray();
            }
            var sourcePath=Ancestors(source.Rig);var targetPath=Ancestors(target);
            var bones=source.Scene.Skeleton.Bones;
            var tracks=source.Motion.Tracks.ToDictionary(t=>t.Bone);
            var active=sourcePath.Where(i=>tracks.ContainsKey(bones[i].Id)).ToArray();
            if(active.Length>targetPath.Length)
                throw new InvalidDataException("BT3 authors more independent motion controls than this target's root chain can preserve.");
            bool MovingTranslation(int index)
            {
                var track=tracks[bones[index].Id];if(track.RotationOnly)return false;
                Vector3 bind=bones[index].Rest;
                return track.Keys.Any(k=>k.Translation!=bind)||
                    (track.Keys[0].Frame>0||track.Keys[track.Keys.Length-1].Frame<source.Motion.Length)&&bind!=Vector3.Zero;
            }
            int[] moving=active.Where(MovingTranslation).ToArray();
            if(moving.Length>1)
                throw new InvalidDataException("Multiple BT3 source controls author actor translation; review their separate target bindings before transfer.");
            var result=new Dictionary<int,int>();
            if(moving.Length==0)
            {
                for(int i=0;i<active.Length;i++)result.Add(active[i],targetPath[i]);
            }
            else
            {
                if(!target.Roles.TryGetValue("motion",out int targetMotion))
                    throw new InvalidDataException("The target rig has no reviewed actor-translation control for BT3 root motion.");
                int slot=Array.IndexOf(targetPath,targetMotion),pivot=Array.IndexOf(active,moving[0]);
                if(slot<0||pivot>slot||active.Length-pivot-1>targetPath.Length-slot-1)
                    throw new InvalidDataException("The BT3 root-control order cannot fit the target's actor-translation control without reordering authored motion.");
                for(int i=0;i<pivot;i++)result.Add(active[i],targetPath[i]);
                result.Add(moving[0],targetMotion);
                for(int i=pivot+1;i<active.Length;i++)result.Add(active[i],targetPath[slot+i-pivot]);
            }
            return result;
        }
    }
}
