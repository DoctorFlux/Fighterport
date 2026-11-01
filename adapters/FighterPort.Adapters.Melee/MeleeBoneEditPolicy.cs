using HSDRaw;
using HSDRaw.Melee;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace FighterPort.Adapters.Melee
{
    // Native common part_to_joint metadata, not display-name or character rules.
    // ftparts.c numbers ordinary, non-instance joints in preorder; equal table/
    // native counts exclude skipped-part layouts. Conflicting tables stay closed.
    public sealed class MeleeBoneEditPolicy
    {
        public bool Available {get;private set;}
        public int BodyJoint {get;private set;}=-1;
        public int[] ProtectedJoints {get;private set;}=new int[0];
        public string Evidence {get;private set;}
        private bool[] editable;
        public bool CanEdit(int joint)=>Available&&joint>=0&&joint<editable.Length&&editable[joint];
        public static MeleeBoneEditPolicy Create(MeleeNativeModel model,SBM_ftLoadCommonData common)
        {
            var result=new MeleeBoneEditPolicy{Evidence="Native common bone-role context is missing or ambiguous."};
            var joints=MeleeNative.Joints(model.Root);var ids=joints.Select((j,i)=>new{j._s,Index=i}).ToDictionary(j=>j._s,j=>j.Index);
            var parents=Enumerable.Repeat(-1,joints.Length).ToArray();
            for(int i=0;i<joints.Length;i++)for(var child=joints[i].Child;child!=null;child=child.Next)parents[ids[child._s]]=i;
            bool Ancestor(int ancestor,int child){for(int i=child;i>=0;i=parents[i])if(i==ancestor)return true;return false;}
            var candidates=new List<int[]>();
            if(common?.BoneTables==null)return result;
            if(common.BoneTables.Length>256)throw new InvalidDataException("Native common bone-table budget exceeded.");
            foreach(var table in common.BoneTables.Array)
            {
                if(table==null||table.BoneCount!=joints.Length||table._s.References.Count==0)continue;
                int[] roles={table.Top,table.Trans,table.XRot,table.YRot,table.Hip};
                if(roles.Any(i=>i<0||i>=joints.Length)||roles.Take(4).Contains(roles[4])||roles.Take(4).Any(root=>!Ancestor(root,roles[4])))continue;
                candidates.Add(roles);
            }
            var distinct=candidates.GroupBy(r=>string.Join(",",r)).Select(g=>g.First()).ToArray();
            if(distinct.Length!=1)return result;
            int body=distinct[0][4];var protectedSet=new HashSet<int>(distinct[0].Take(4));
            for(int i=parents[body];i>=0;i=parents[i])protectedSet.Add(i);
            result.BodyJoint=body;result.ProtectedJoints=protectedSet.OrderBy(i=>i).ToArray();
            result.editable=Enumerable.Range(0,joints.Length).Select(i=>Ancestor(body,i)&&!protectedSet.Contains(i)).ToArray();
            result.Available=true;result.Evidence="Exact native common part_to_joint roles agree for this joint count and hierarchy; all body ancestors remain protected. Shared IK/rig solving is separately unqualified.";return result;
        }
        public static MeleeBoneEditPolicy Read(MeleeNativeModel model,byte[] commonBytes)
        {
            var roots=MeleeNative.ReadMembers(commonBytes).SelectMany(m=>m.Native.Roots).Where(r=>r.Data is SBM_ftLoadCommonData).ToArray();
            if(roots.Length!=1)throw new InvalidDataException("Exact native common-data root is unavailable.");
            return Create(model,(SBM_ftLoadCommonData)roots[0].Data);
        }
    }
}
