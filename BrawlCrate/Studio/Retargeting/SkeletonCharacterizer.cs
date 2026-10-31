using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace BrawlCrate.Studio.Retargeting
{
    public sealed class SkeletonCharacterization
    {
        public Dictionary<string,int> Roles { get; } = new Dictionary<string,int>();
        public Dictionary<string,string> Evidence { get; } = new Dictionary<string,string>();
        public List<string> Warnings { get; } = new List<string>();
        public Dictionary<string,string> Unavailable { get; } = new Dictionary<string,string>();
        public string[] Missing => SkeletonCharacterizer.CoreRoles.Where(r=>!Roles.ContainsKey(r)&&!Unavailable.ContainsKey(r)).ToArray();
    }

    // Anatomy is shared across characters and formats. Names contribute evidence;
    // hierarchy and bind-space geometry supply the fallback and validate the map.
    // No character identity or animation name participates in characterization.
    public static class SkeletonCharacterizer
    {
        public static readonly string[] CoreRoles = new[]{"pelvis","spine","chest","neck","head"}
            .Concat(new[]{"left","right"}.SelectMany(s=>new[]{"upperArm","lowerArm","hand","upperLeg","lowerLeg","foot"}.Select(p=>s+"."+p))).ToArray();
        private static readonly Dictionary<string,string[]> Names = new Dictionary<string,string[]> {
            {"root",new[]{"root","topn","armature","skeleton"}},
            {"motion",new[]{"transn","motion","trajectory","global"}},
            {"pivot",new[]{"xrotn"}},{"turn",new[]{"yrotn"}},
            {"pelvis",new[]{"pelvis","hips","hip","hipn","cog"}},
            {"spine",new[]{"spine","spine1","spine01","waist","waistn","body","bodyn","abdomen"}},
            {"chest",new[]{"chest","upperchest","bustn","spine2","spine02","spine3","spine03","thorax"}},
            {"neck",new[]{"neck","neckn","neck1","neck01"}},{"head",new[]{"head","headn","head1","head01","skull"}},
            {"shoulder",new[]{"shouldern","clavicle","collar","shoulder"}},
            {"upperArm",new[]{"upperarm","uparm","arm","shoulderj"}},
            {"lowerArm",new[]{"forearm","lowerarm","elbow","armj"}},
            {"hand",new[]{"hand","handn","wrist"}},
            {"upperLeg",new[]{"upperleg","upleg","thigh","legj"}},
            {"lowerLeg",new[]{"lowerleg","leg","calf","shin","knee","kneej"}},
            {"foot",new[]{"foot","footj","ankle"}},{"toe",new[]{"toe","toes","toebase","ball","toen"}},
            {"prop",new[]{"have","haven","weapon","hold","handattach"}},{"throw",new[]{"throw","thrown"}}
        };
        private static string Canonical(string name,out string side)
        {
            string value=name.Split(':','|','/','\\').Last();side=null;
            value=Regex.Replace(value,@"^(mixamorig|bip\d*|bip)[_ .-]*","",RegexOptions.IgnoreCase);
            if(Regex.IsMatch(value,@"^left",RegexOptions.IgnoreCase)){side="left";value=Regex.Replace(value,"^left","",RegexOptions.IgnoreCase);}
            else if(Regex.IsMatch(value,@"^right",RegexOptions.IgnoreCase)){side="right";value=Regex.Replace(value,"^right","",RegexOptions.IgnoreCase);}
            else if(Regex.IsMatch(value,@"^[lL](?=[A-Z0-9_. -])")){side="left";value=value.Substring(1);}
            else if(Regex.IsMatch(value,@"^[rR](?=[A-Z0-9_. -])")){side="right";value=value.Substring(1);}
            else if(Regex.IsMatch(value,@"[_. -]left$",RegexOptions.IgnoreCase)){side="left";value=Regex.Replace(value,@"[_. -]left$","",RegexOptions.IgnoreCase);}
            else if(Regex.IsMatch(value,@"[_. -]right$",RegexOptions.IgnoreCase)){side="right";value=Regex.Replace(value,@"[_. -]right$","",RegexOptions.IgnoreCase);}
            else if(Regex.IsMatch(value,@"[_. -][lL]$")){side="left";value=value.Substring(0,value.Length-2);}
            else if(Regex.IsMatch(value,@"[_. -][rR]$")){side="right";value=value.Substring(0,value.Length-2);}
            return Regex.Replace(value.ToLowerInvariant(),"[^a-z0-9]","");
        }
        private static string Finger(string name)
        {
            string[] fingers={"(?:index|1st)","(?:middle|2nd)","(?:ring|3rd)","(?:little|pinky|4th)","thumb"};
            for(int i=0;i<fingers.Length;i++)
            {
                var match=Regex.Match(name,"^(?:hand)?"+fingers[i]+"(?:n)?(a|b|01|02|1|2|proximal|intermediate)$");
                if(match.Success)return "finger"+i+"."+(new[]{"a","01","1","proximal"}.Contains(match.Groups[1].Value)?"a":"b");
            }
            return null;
        }
        public static SkeletonCharacterization Analyze(IEnumerable<RigJoint> skeleton)
        {
            RigJoint[] joints=skeleton.ToArray();
            var neutral=new Rig("Characterization","Humanoid",joints,new Dictionary<string,int>(),1);
            var result=new SkeletonCharacterization();
            var candidates=new Dictionary<string,List<int>>();
            for(int i=0;i<joints.Length;i++)
            {
                string name=Canonical(joints[i].Name,out string side),role=side==null?null:Finger(name);
                if(role==null)role=Names.Where(p=>p.Value.Contains(name)).Select(p=>p.Key).FirstOrDefault();
                if(role==null)continue;
                bool limb=role.Contains("Arm")||role.Contains("Leg")||new[]{"shoulder","hand","foot","toe","prop"}.Contains(role)||role.StartsWith("finger",StringComparison.Ordinal);
                if(limb && side==null)continue;
                role=limb?side+"."+role:role;
                if(!candidates.ContainsKey(role))candidates.Add(role,new List<int>());
                candidates[role].Add(i);
            }
            foreach(var pair in candidates)
            {
                if(pair.Value.Count==1){result.Roles.Add(pair.Key,pair.Value[0]);result.Evidence.Add(pair.Key,"name + hierarchy validation");}
                else result.Warnings.Add("Several bones suggest "+pair.Key+"; review the proposed hierarchy/geometry mapping.");
            }
            InferGeometry(neutral,result);
            foreach(string side in new[]{"left","right"})if(HasTerminalArm(neutral,result.Roles,side))
            {
                string role=side+".hand",reason="No distinct distal joint: forearm FK retained; hand IK/contact unavailable.";
                result.Unavailable.Add(role,reason);result.Evidence[role]=reason;
            }
            foreach(string missing in result.Missing)result.Warnings.Add("Assign "+missing+" before retargeting.");
            return result;
        }
        private static void InferGeometry(Rig rig,SkeletonCharacterization result)
        {
            var p=rig.Rest.World.Select(m=>m.Translation).ToArray();int count=p.Length;
            var children=Enumerable.Range(0,count).Select(i=>Enumerable.Range(0,count).Where(j=>rig.Joints[j].Parent==i).ToArray()).ToArray();
            Func<int,int,bool> desc=(j,a)=>{while(j>=0){if(j==a)return true;j=rig.Joints[j].Parent;}return false;};
            Func<int,int[]> subtree=i=>Enumerable.Range(0,count).Where(j=>desc(j,i)).ToArray();
            float height=Math.Max(0.001f,p.Max(v=>v.Y)-p.Min(v=>v.Y));
            Func<int,int,bool> legBranch=(parent,child)=>subtree(child).Length>=3 &&
                subtree(child).Min(j=>p[j].Y)<p[parent].Y-height*0.2f && subtree(child).Max(j=>p[j].Y)<p[parent].Y+height*0.1f;
            Action<string,int> put=(role,index)=>{if(index>=0 && !result.Roles.ContainsKey(role) && !result.Roles.Values.Contains(index)){result.Roles.Add(role,index);result.Evidence.Add(role,"bind geometry + hierarchy (review orientation)");}};
            if(!result.Roles.ContainsKey("pelvis"))
            {
                var possible=Enumerable.Range(0,count).Where(i=>children[i].Count(c=>legBranch(i,c))>=2 &&
                    children[i].Any(c=>subtree(c).Max(j=>p[j].Y)>p[i].Y+height*0.2f)).ToArray();
                if(possible.Length==1)put("pelvis",possible[0]);
                else if(possible.Length>1)put("pelvis",possible.OrderByDescending(i=>p[i].Y).First());
            }
            if(!result.Roles.TryGetValue("pelvis",out int pelvis))return;
            int root=pelvis;while(rig.Joints[root].Parent>=0)root=rig.Joints[root].Parent;
            if(root!=pelvis)put("root",root);
            var legBranches=children[pelvis].Where(c=>legBranch(pelvis,c))
                .OrderBy(c=>subtree(c).Min(j=>p[j].Y)).Take(2).ToArray();
            Vector3 lateral=Vector3.UnitX;
            if(result.Roles.TryGetValue("left.upperLeg",out int l) && result.Roles.TryGetValue("right.upperLeg",out int r))lateral=p[l]-p[r];
            else if(legBranches.Length==2) {lateral=p[legBranches[0]]-p[legBranches[1]];if(Math.Abs(lateral.X)>Math.Abs(lateral.Z)?lateral.X<0:lateral.Z<0)lateral=-lateral;}
            lateral.Y=0;if(lateral.LengthSquared()<0.000001f)lateral=Vector3.UnitX;lateral=Vector3.Normalize(lateral);
            foreach(int branch in legBranches)
            {
                string side=SideOfBranch(result,rig,branch,lateral,p,pelvis);
                int leaf=subtree(branch).OrderBy(i=>p[i].Y).ThenByDescending(i=>Depth(rig,i)).First();
                int[] path=Path(rig,branch,leaf);
                if(path.Length<3)continue;
                int foot=path.Skip(2).OrderBy(i=>Math.Abs(p[i].Y-(p[leaf].Y+height*0.035f))).ThenBy(i=>Depth(rig,i)).First();
                int[] leg=Path(rig,branch,foot);if(leg.Length<3)continue;
                int knee=leg.Skip(1).Take(leg.Length-2).OrderBy(i=>Math.Abs(p[i].Y-(p[branch].Y+p[foot].Y)*0.5f)).First();
                put(side+".upperLeg",branch);put(side+".lowerLeg",knee);put(side+".foot",foot);
                var toe=children[foot].Where(i=>Vector3.Distance(p[i],p[foot])>height*0.005f).OrderByDescending(i=>Vector3.Distance(p[i],p[foot])).ToArray();
                if(toe.Length>0)put(side+".toe",toe[0]);
            }
            if(!result.Roles.ContainsKey("chest"))
            {
                var possible=subtree(pelvis).Where(i=>p[i].Y>p[pelvis].Y+height*0.05f && children[i].Length>=3 &&
                    children[i].Count(c=>subtree(c).Any(j=>Math.Abs(Vector3.Dot(p[j]-p[i],lateral))>height*0.18f))>=2).ToArray();
                if(possible.Length>0)put("chest",possible.OrderBy(i=>p[i].Y).First());
            }
            if(!result.Roles.TryGetValue("chest",out int chest))return;
            int[] trunk=Path(rig,pelvis,chest);
            if(trunk.Length>2)put("spine",trunk[1]);
            var headBranch=children[chest].Where(c=>subtree(c).Max(i=>p[i].Y)>p[chest].Y+height*0.08f)
                .OrderBy(c=>subtree(c).Average(i=>Math.Abs(Vector3.Dot(p[i]-p[chest],lateral)))).ToArray();
            if(headBranch.Length>0)
            {
                int tip=subtree(headBranch[0]).OrderByDescending(i=>p[i].Y).First();int[] path=Path(rig,headBranch[0],tip);
                if(path.Length>=2)
                {
                    int[] heads=path.Skip(1).Where(i=>children[i].Length>=2).ToArray();
                    put("neck",path[0]);put("head",heads.Length>0?heads[0]:path[Math.Max(1,path.Length-2)]);
                }
            }
            foreach(int branch in children[chest].Where(c=>subtree(c).Any(j=>Math.Abs(Vector3.Dot(p[j]-p[chest],lateral))>height*0.18f)))
            {
                string side=SideOfBranch(result,rig,branch,lateral,p,chest);
                var hands=subtree(branch).Where(i=>children[i].Length>=3).OrderBy(i=>Depth(rig,i)).ToArray();
                int hand=hands.Length>0?hands[0]:subtree(branch).OrderByDescending(i=>Math.Abs(Vector3.Dot(p[i]-p[chest],lateral))).First();
                int[] path=Path(rig,branch,hand);if(path.Length<3)continue;
                int upper=path.Length>=4?path[1]:path[0];
                int[] arm=Path(rig,upper,hand);if(arm.Length<3)continue;
                int elbow=arm.Skip(1).Take(arm.Length-2).OrderBy(i=>Vector3.Distance(p[i],(p[upper]+p[hand])*0.5f)).First();
                if(upper!=branch)put(side+".shoulder",branch);
                put(side+".upperArm",upper);put(side+".lowerArm",elbow);put(side+".hand",hand);
            }
            if(result.Evidence.Any(e=>e.Value.StartsWith("bind geometry",StringComparison.Ordinal)))
                result.Warnings.Add("Geometry inferred some roles. Check left/right, spine, joints and the source rest pose in Review Skeleton Mapping.");
        }
        private static string SideOfBranch(SkeletonCharacterization map,Rig rig,int branch,Vector3 lateral,Vector3[] positions,int center)
        {
            foreach(string side in new[]{"left","right"})foreach(var role in map.Roles.Where(r=>r.Key.StartsWith(side+".",StringComparison.Ordinal)))
            {int node=role.Value;while(node>=0){if(node==branch)return side;node=rig.Joints[node].Parent;}}
            return Vector3.Dot(positions[branch]-positions[center],lateral)>=0?"left":"right";
        }
        private static int Depth(Rig rig,int bone){int n=0;while((bone=rig.Joints[bone].Parent)>=0)n++;return n;}
        private static int[] Path(Rig rig,int ancestor,int child)
        {var path=new List<int>();while(child>=0){path.Add(child);if(child==ancestor){path.Reverse();return path.ToArray();}child=rig.Joints[child].Parent;}return new int[0];}
        // A genuinely terminal forearm is different from an unassigned wrist.
        // Only exact existing hierarchy/rest evidence permits this capability
        // limitation. A distinct distal descendant still requires mapping review.
        internal static bool HasTerminalArm(Rig rig,IDictionary<string,int> roles,string side)
        {
            if(roles.ContainsKey(side+".hand")||!roles.TryGetValue(side+".upperArm",out int upper)||!roles.TryGetValue(side+".lowerArm",out int lower))return false;
            if(upper<0||lower<0||upper>=rig.Joints.Length||lower>=rig.Joints.Length||!rig.Ancestors[lower][upper]||upper==lower)return false;
            float span=Vector3.Distance(rig.Rest.Point(upper),rig.Rest.Point(lower));
            if(!RigMath.Finite(span)||span<0.00001f)return false;
            // Exact zero local offsets, not a scale-dependent proximity guess.
            // Even a tiny distinct descendant remains an unresolved binding.
            return Enumerable.Range(0,rig.Joints.Length).Where(i=>i!=lower&&rig.Ancestors[i][lower])
                .All(i=>rig.Joints[i].Bind.Translation==Vector3.Zero);
        }
        public static void Validate(RigJoint[] joints,IDictionary<string,int> roles)
        {
            var rig=new Rig("Validation","Humanoid",joints,roles,1);
            var missing=CoreRoles.Where(r=>!roles.ContainsKey(r)&&!(r.EndsWith(".hand",StringComparison.Ordinal)&&HasTerminalArm(rig,roles,r.Split('.')[0]))).ToArray();
            if(missing.Length>0)throw new InvalidDataException("Skeleton mapping needs: "+string.Join(", ",missing)+". Use Review Skeleton Mapping.");
            foreach(string side in new[]{"left","right"})foreach(string[] chain in new[]{new[]{"upperArm","lowerArm","hand"},new[]{"upperLeg","lowerLeg","foot"}})
                for(int i=1;i<chain.Length;i++)
                {
                    if(!roles.ContainsKey(side+"."+chain[i]))continue; // proven unavailable end only
                    int ancestor=roles[side+"."+chain[i-1]],node=roles[side+"."+chain[i]];float distance=Vector3.Distance(rig.Rest.Point(ancestor),rig.Rest.Point(node));
                    while(node>=0 && node!=ancestor)node=joints[node].Parent;
                    if(node!=ancestor || distance<0.00001f)throw new InvalidDataException("Invalid mapped limb hierarchy or zero-length segment: "+side+"."+chain[i]);
                }
            foreach(var pair in new[]{new[]{"pelvis","spine"},new[]{"spine","chest"},new[]{"chest","neck"},new[]{"neck","head"}})
            {int node=roles[pair[1]];while(node>=0&&node!=roles[pair[0]])node=joints[node].Parent;if(node<0)throw new InvalidDataException("Invalid torso hierarchy at "+pair[1]);}
        }
    }
}
