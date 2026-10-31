using BrawlCrate.Studio;
using BrawlLib.SSBB.ResourceNodes;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace BrawlCrate.Studio.Retargeting
{
    // Offline proof for the existing Next transform equations, not inference
    // from sampled equal endpoints. Corrections change rotations and pelvis
    // translation only; all local scales and other local translations retain
    // their affine source-channel dependence. Never author protected roots here.
    internal sealed class NativeRetargetConstants
    {
        internal const string Policy="native-source-constant-components-1";
        readonly CHR0Node source;
        readonly string content;
        readonly int[] parents;
        readonly bool[] translations,scales;
        NativeRetargetConstants(HumanoidRetargeter plan,CHR0Node clip,bool[] translations,bool[] scales)
        {
            source=clip;content=NativeAnimationOptimization.SourceContentKey(clip);
            parents=plan.Target.Joints.Select(j=>j.Parent).ToArray();this.translations=translations;this.scales=scales;
        }
        internal bool Translation(int bone)=>translations[bone];
        internal bool Scale(int bone)=>scales[bone];
        internal void Validate(int[] hierarchy)
        {
            if(!parents.SequenceEqual(hierarchy)||NativeAnimationOptimization.SourceContentKey(source)!=content)
                throw new InvalidDataException("Native constant proof dependencies changed; no retarget result is available.");
        }
        internal static NativeRetargetConstants Capture(HumanoidRetargeter plan,CHR0Node clip,CancellationToken cancellation)
        {
            var entries=clip.Children.OfType<CHR0EntryNode>().ToArray();
            if(entries.Select(e=>e.Name).Distinct(StringComparer.Ordinal).Count()!=entries.Length)throw new InvalidDataException("Ambiguous native source entries cannot prove constant channels.");
            long inspected=0;
            foreach(var entry in entries)foreach(var array in entry.KeyArrays)
            {
                cancellation.ThrowIfCancellationRequested();inspected+=array.KeyCount;
                if(inspected>2000000)return null; // conservative ordinary bake fallback
            }
            var byName=entries.ToDictionary(e=>e.Name,StringComparer.Ordinal);
            bool Constant(CHR0EntryNode entry,int group)
            {
                if(entry==null)return true; // authentic source bind fallback
                for(int axis=0;axis<3;axis++)
                {
                    var keys=entry.KeyArrays[group*3+axis].Entries.ToArray();
                    if(keys.Length==0)continue;
                    int bits=Bits(keys[0]._value);
                    // Preserve signed-zero distinctions and uncertain native
                    // interpolation semantics instead of assuming equivalence.
                    if(bits==int.MinValue)return false;
                    foreach(var key in keys)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if(!Finite(key._index)||!Finite(key._value)||!Finite(key._tangent))throw new InvalidDataException("Invalid native constant-proof input.");
                        if(Bits(key._value)!=bits||Bits(key._tangent)!=0)return false;
                    }
                }
                return true;
            }
            var translations=new bool[plan.Target.Joints.Length];var scales=new bool[translations.Length];
            foreach(var map in plan.Mapping)
            {
                cancellation.ThrowIfCancellationRequested();
                if(plan.TargetMotionControls.Contains(map.TargetIndex))continue;
                byName.TryGetValue(map.Source,out var entry);
                translations[map.TargetIndex]=map.TargetIndex!=plan.Target.Roles["pelvis"]&&Constant(entry,2);
                scales[map.TargetIndex]=Constant(entry,0);
            }
            return new NativeRetargetConstants(plan,clip,translations,scales);
        }
        static int Bits(float v)=>BitConverter.ToInt32(BitConverter.GetBytes(v),0);
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
    }
}
