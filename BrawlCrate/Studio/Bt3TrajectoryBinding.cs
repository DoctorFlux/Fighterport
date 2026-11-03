using BrawlCrate.Studio.Retargeting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace BrawlCrate.Studio
{
    // BT3's three native controls precede the deforming pelvis. They match
    // the actor, pitch and facing controls, not the outer TopN container.
    // Check that exact native chain before transferring any local animation.
    internal static class Bt3TrajectoryBinding
    {
        internal const string Policy="bt3-native-actor-pitch-facing-controls-v2";

        internal static IReadOnlyDictionary<int,int> Prepare(Bt3SourceRig source,Rig target)
        {
            if(source==null||target==null)throw new ArgumentNullException(source==null?nameof(source):nameof(target));
            int[] Ancestors(Rig rig)
            {
                var list=new List<int>();
                if(!rig.Roles.TryGetValue("pelvis",out int pelvis))
                    throw new InvalidDataException("The exact rig has no reviewed pelvis control.");
                for(int i=rig.Joints[pelvis].Parent;i>=0;i=rig.Joints[i].Parent)list.Add(i);
                list.Reverse();return list.ToArray();
            }
            var sourcePath=Ancestors(source.Rig);var targetPath=Ancestors(target);
            var bones=source.Scene.Skeleton.Bones;
            if(sourcePath.Length!=3||bones[sourcePath[0]].Id!=0||bones[sourcePath[1]].Id!=1||bones[sourcePath[2]].Id!=2)
                throw new InvalidDataException("The BT3 actor/pitch/facing control chain differs from the reviewed native layout.");
            var roles=new[]{"motion","pivot","turn"};
            var targets=new int[roles.Length];
            for(int i=0;i<roles.Length;i++)
                if(!target.Roles.TryGetValue(roles[i],out targets[i])||Array.IndexOf(targetPath,targets[i])<0)
                    throw new InvalidDataException("The Brawl target lacks a reviewed "+roles[i]+" control in its root chain.");
            if(targets.Distinct().Count()!=3||Array.IndexOf(targetPath,targets[0])>=Array.IndexOf(targetPath,targets[1])||
                Array.IndexOf(targetPath,targets[1])>=Array.IndexOf(targetPath,targets[2]))
                throw new InvalidDataException("The target actor/pitch/facing controls are not a distinct ordered chain.");
            bool Authored(int index)
            {
                var bone=bones[index];
                var track=source.Motion.Tracks.SingleOrDefault(t=>t.Bone==bone.Id);
                if(track==null)return false;
                // A packed identity quaternion can decode a fraction of a
                // millidegree off identity. That is codec noise, not actor
                // motion. Real translations and rotations still bind locally.
                return track.Keys.Any(key=>(!track.RotationOnly&&
                        Vector3.DistanceSquared(key.Translation,bone.Rest)>1e-10f)||
                    QuaternionAngle(key.Rotation)>0.001);
            }
            return Enumerable.Range(0,3).Where(i=>Authored(sourcePath[i]))
                .ToDictionary(i=>sourcePath[i],i=>targets[i]);
        }

        private static double QuaternionAngle(Quaternion value)
        {
            var normalized=Quaternion.Normalize(value);
            return 2*Math.Acos(Math.Min(1,Math.Abs(normalized.W)))*180/Math.PI;
        }
    }
}
