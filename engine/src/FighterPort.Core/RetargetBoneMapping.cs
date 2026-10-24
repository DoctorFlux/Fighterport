using System.Numerics;
using System.Text.Json.Serialization;

namespace FighterPort.Core;

public record RetargetBonePair(string Role,int Source,int Target,string Evidence)
{
    public bool DetailChannels {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public RetargetBindingEvidence? Binding {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public RetargetBoneAlias[]? Aliases {get;init;}
}
public record RetargetBoneAlias(string Role,string Evidence,RetargetBindingEvidence? Binding);
public record RetargetBindingEvidence(string Origin,string Confidence,bool Supplied,bool Reviewed,string SourceEvidence,string TargetEvidence)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public bool RequiresReview {get;init;}
}
public record RetargetUnmappedBone(int Bone,string Name,string Reason);
public record RetargetMappingReport(RetargetBonePair[] Bones,RetargetUnmappedBone[] SourceOnly,RetargetUnmappedBone[] TargetOnly);

// Body semantics establish anchors. Extend them through compatible native branches;
// matching names alone never authorize a cross-skeleton channel transfer.
public static class RetargetBoneMapping
{
    public const string Version="native-channel-correspondence-4";
    // Conservative candidate bounds frozen before evaluating the fixtures.
    // These are geometric filters, not calibrated semantic probabilities.
    const float BranchDirectionCosine=.9659258f; // 15 degrees.
    const float SkinCentroidDistance=.25f; // In each bone's own rest-branch lengths.
    public static RetargetMappingReport Build(Scene source,ControlRigSetup sourceRig,Scene target,ControlRigSetup targetRig)
    {
        // Reports and numerical transfer must consume the same reviewed roles.
        // The historical characterization helper also included unreviewed roles
        // and required two legs even for a completely supplied non-humanoid rig.
        var a=RigDescriptor.Prepare(source,sourceRig);var b=RigDescriptor.Prepare(target,targetRig);
        return Build(source,sourceRig,target,targetRig,a.Binding.Roles,b.Binding.Roles,a.Basis,b.Basis);
    }
    internal static RetargetMappingReport Build(Scene source,ControlRigSetup sourceRig,Scene target,ControlRigSetup targetRig,
        IReadOnlyDictionary<string,int> sourceRoles,IReadOnlyDictionary<string,int> targetRoles,Matrix4x4 sourceBasis,Matrix4x4 targetBasis)
    {
        var pairs=new List<RetargetBonePair>();var usedSource=new HashSet<int>();var usedTarget=new HashSet<int>();
        void Add(string role,int s,int t,string evidence,RetargetBindingEvidence binding)
        {
            if(s<0||s>=source.Bones.Length||t<0||t>=target.Bones.Length)throw new InvalidDataException($"Mapping '{role}' references a missing native bone: source {s}, target {t}.");
            var sourceOwner=pairs.FirstOrDefault(p=>p.Source==s);var targetOwner=pairs.FirstOrDefault(p=>p.Target==t);
            if(sourceOwner is not null||targetOwner is not null)
            {
                if(sourceOwner?.Target==t&&targetOwner?.Source==s)
                {
                    // One native channel owner can have several semantic
                    // descriptions. Their insertion order must not erase the
                    // root/body translation policy or exclude body motion from
                    // action checks. Keep every supplied role/evidence record.
                    var aliases=(sourceOwner.Aliases??[new(sourceOwner.Role,sourceOwner.Evidence,sourceOwner.Binding)])
                        .Append(new(role,evidence,binding)).OrderBy(a=>RolePriority(a.Role)).ThenBy(a=>a.Role,StringComparer.Ordinal).ToArray();
                    var canonical=aliases[0];
                    var strongest=aliases.OrderByDescending(a=>a.Binding?.Reviewed==true).ThenByDescending(a=>a.Binding?.Supplied==true)
                        .ThenBy(a=>RolePriority(a.Role)).ThenBy(a=>a.Role,StringComparer.Ordinal).First().Binding;
                    var merged=strongest is null?null:strongest with
                    {
                        SourceEvidence=string.Join(" | ",aliases.Select(a=>a.Role+": "+(a.Binding?.SourceEvidence??a.Evidence))),
                        TargetEvidence=string.Join(" | ",aliases.Select(a=>a.Role+": "+(a.Binding?.TargetEvidence??a.Evidence))),
                        RequiresReview=!aliases.Any(a=>a.Binding?.Reviewed==true)&&aliases.Any(a=>a.Binding?.RequiresReview==true)
                    };
                    pairs[pairs.IndexOf(sourceOwner)]=sourceOwner with
                    {
                        Role=canonical.Role,Evidence=canonical.Evidence+"; semantic aliases: "+string.Join(", ",aliases.Select(a=>a.Role)),
                        DetailChannels=aliases.All(a=>IsDetailRole(a.Role)),Binding=merged,Aliases=aliases
                    };
                    return;
                }
                var owner=sourceOwner??targetOwner!;
                throw new InvalidDataException($"Conflicting bone correspondence '{role}' ({s} → {t}) and '{owner.Role}' ({owner.Source} → {owner.Target}). One-to-one channel ownership must be reviewed; no supplied mapping was silently discarded.");
            }
            usedSource.Add(s);usedTarget.Add(t);pairs.Add(new(role,s,t,evidence){Binding=binding,DetailChannels=IsDetailRole(role)});
        }
        foreach(var role in sourceRoles.Where(p=>targetRoles.ContainsKey(p.Key)))
        {
            var sourceEvidence=Evidence(sourceRig,role.Key,role.Value);var targetEvidence=Evidence(targetRig,role.Key,targetRoles[role.Key]);
            bool reviewed=sourceEvidence.Reviewed&&targetEvidence.Reviewed;
            Add(role.Key,role.Value,targetRoles[role.Key],"Semantic correspondence with bind-axis conversion",
                new(sourceEvidence.Supplied&&targetEvidence.Supplied?"Supplied semantic binding":"Structural/candidate semantic binding",reviewed?"Reviewed":"Uncalibrated",sourceEvidence.Supplied&&targetEvidence.Supplied,reviewed,sourceEvidence.Text,targetEvidence.Text));
        }
        var sourceBind=AnimationService.Evaluate(source,null,0);var targetBind=AnimationService.Evaluate(target,null,0);var conversion=Matrix4x4.Transpose(sourceBasis)*targetBasis;
        var targets=target.Bones.Select((bone,i)=>(bone.Name,Index:i)).GroupBy(p=>p.Name,StringComparer.Ordinal).Where(g=>g.Count()==1).ToDictionary(g=>g.Key,g=>g.Single().Index,StringComparer.Ordinal);
        var sourceNames=source.Bones.GroupBy(bone=>bone.Name,StringComparer.Ordinal).Where(g=>g.Count()==1).Select(g=>g.Key).ToHashSet(StringComparer.Ordinal);
        bool identical=source.Bones.SequenceEqual(target.Bones);
        var skipped=new Dictionary<int,string>();int Depth(int i){int depth=0;for(;i>=0;i=source.Bones[i].Parent)depth++;return depth;}
        foreach(int s in Enumerable.Range(0,source.Bones.Length).OrderBy(Depth))
        {
            if(usedSource.Contains(s))continue;string name=source.Bones[s].Name;
            if(!sourceNames.Contains(name)||!targets.TryGetValue(name,out int t)){skipped[s]="No unique corresponding name/semantic role; review an explicit mapping";continue;}
            if(usedTarget.Contains(t)){skipped[s]="Corresponding target already belongs to another semantic role";continue;}
            if(!identical&&(sourceRig.PreservedHelperBones.Contains(s)||targetRig.PreservedHelperBones.Contains(t))){skipped[s]="Native helper requires a reviewed cross-rig correspondence";continue;}
            int sp=source.Bones[s].Parent,tp=target.Bones[t].Parent;
            if(!identical&&(sp<0||tp<0||!pairs.Any(p=>p.Source==sp&&p.Target==tp))){skipped[s]="Same name has a different/unmapped parent branch";continue;}
            var sd=sp<0?Vector3.Zero:sourceBind.World[s].Translation-sourceBind.World[sp].Translation;var td=tp<0?Vector3.Zero:targetBind.World[t].Translation-targetBind.World[tp].Translation;
            float sl=sd.Length(),tl=td.Length();
            if(!identical&&(sl<.00001f||tl<.00001f)){skipped[s]="Coincident/selector bone needs reviewed semantic evidence";continue;}
            if(!identical&&(tl/sl is <.05f or >20f||Vector3.Dot(Vector3.Normalize(Vector3.TransformNormal(sd,conversion)),td/tl)<.25f)){skipped[s]="Bind branch direction/length is incompatible; review the correspondence";continue;}
            string evidence=identical?"Exact same native transform graph":"Unique name supported by mapped direct parent, compatible bind branch geometry and reviewed model axes";
            Add("Shared."+name,s,t,evidence,new(identical?"Exact graph correspondence":"Inferred named branch",identical?"Exact graph":"Uncalibrated",false,identical,evidence,evidence));
        }
        var targetSkipped=new Dictionary<int,string>();
        // Discover detailed branches only below an already reviewed semantic
        // anchor. This does not discover the body rig, unequal chain segments,
        // selector semantics or derived helpers. Inferred descendants remain a
        // reviewable candidate until saved as explicit reviewed role bindings.
        var sourceBranches=DescribeBranches(source,sourceBind,sourceBasis,sourceRig.PreservedHelperBones);
        var targetBranches=DescribeBranches(target,targetBind,targetBasis,targetRig.PreservedHelperBones);
        var anchors=new Queue<RetargetBonePair>(pairs.Where(p=>p.Binding?.Reviewed==true));
        while(anchors.TryDequeue(out var anchor))
        {
            var ss=Enumerable.Range(0,source.Bones.Length).Where(i=>source.Bones[i].Parent==anchor.Source&&!usedSource.Contains(i)).ToArray();
            var tt=Enumerable.Range(0,target.Bones.Length).Where(i=>target.Bones[i].Parent==anchor.Target&&!usedTarget.Contains(i)).ToArray();
            bool Eligible(BranchDescription branch,HashSet<int> owned,out string reason)
            {
                reason=branch.IneligibleReason;
                if(reason.Length==0&&branch.Descendants.Any(owned.Contains))reason="Subtree crosses an existing correspondence; unequal segmentation requires a reviewed channel-distribution policy";
                return reason.Length==0;
            }
            var candidates=new Dictionary<int,int[]>();
            foreach(int s in ss)
            {
                if(!Eligible(sourceBranches[s],usedSource,out string reason)){skipped[s]=reason;continue;}
                candidates[s]=tt.Where(t=>Eligible(targetBranches[t],usedTarget,out _)&&Compatible(sourceBranches[s],targetBranches[t])).ToArray();
            }
            var owners=tt.ToDictionary(t=>t,t=>candidates.Where(p=>p.Value.Contains(t)).Select(p=>p.Key).ToArray());
            foreach(int t in tt)
            {
                if(!Eligible(targetBranches[t],usedTarget,out string reason))targetSkipped[t]=reason;
                else if(owners[t].Length>1)targetSkipped[t]=$"Ambiguous structural branch: {owners[t].Length} source candidates satisfy the same topology/geometry/skin criteria; review correspondence";
            }
            // Freeze the candidate graph before accepting any edge. Neither
            // iteration/storage order nor a greedy first match resolves ties.
            foreach(var (s,targetsForSource) in candidates)
            {
                if(targetsForSource.Length==1&&owners[targetsForSource[0]].Length==1)
                {
                    int t=targetsForSource[0];
                    string evidence="Unique mutual candidate below a reviewed parent: equal rooted native topology, weighted rest skin, compatible branch direction and normalized skin centroid; semantic identity requires review";
                    Add("Shared.Structural",s,t,evidence,new("Inferred structural branch","Uncalibrated",false,false,evidence,evidence){RequiresReview=true});
                    anchors.Enqueue(pairs[^1]);
                }
                else if(targetsForSource.Length>0)
                {
                    skipped[s]=$"Ambiguous structural branch: {targetsForSource.Length} admissible target candidate(s) with competing ownership; review correspondence";
                    foreach(int t in targetsForSource)targetSkipped[t]="Ambiguous structural branch: correspondence is not uniquely mutual; review correspondence";
                }
                else skipped[s]="No compatible weighted topology/geometry branch below the reviewed parent; review an explicit correspondence";
            }
        }
        return new(pairs.OrderBy(p=>p.Target).ToArray(),Enumerable.Range(0,source.Bones.Length).Where(i=>!usedSource.Contains(i)).Select(i=>new RetargetUnmappedBone(i,source.Bones[i].Name,skipped.GetValueOrDefault(i,"No compatible target semantic role"))).ToArray(),Enumerable.Range(0,target.Bones.Length).Where(i=>!usedTarget.Contains(i)).Select(i=>new RetargetUnmappedBone(i,target.Bones[i].Name,targetSkipped.GetValueOrDefault(i,"Target-specific/unreviewed bone retains its native local rest channels and parent hierarchy"))).ToArray());
    }
    sealed record BranchDescription(string Topology,int[] Descendants,Vector3 Direction,Vector3 SkinCentroid,string IneligibleReason);
    static BranchDescription[] DescribeBranches(Scene scene,EvaluatedPose rest,Matrix4x4 basis,int[] preservedHelpers)
    {
        int count=scene.Bones.Length;var children=Enumerable.Range(0,count).Select(i=>Enumerable.Range(0,count).Where(j=>scene.Bones[j].Parent==i).ToArray()).ToArray();
        var weights=new double[count];var centroids=new Vector3[count];var helperSet=preservedHelpers.ToHashSet();
        foreach(var mesh in scene.Meshes)foreach(var vertex in mesh.Vertices)
        {
            // Rest evaluated geometry respects the scene's actual skinning;
            // influence magnitudes are evidence only and are never rewritten.
            var position=AnimationService.Skin(vertex,rest);
            for(int slot=0;slot<4;slot++)if(vertex.Weights[slot]>0)
            {
                int bone=(int)vertex.Joints[slot];float weight=vertex.Weights[slot];
                weights[bone]+=weight;centroids[bone]+=position*weight;
            }
        }
        var offsets=Enumerable.Range(0,count).Select(i=>scene.Bones[i].Parent<0?Vector3.Zero:rest.World[i].Translation-rest.World[scene.Bones[i].Parent].Translation).ToArray();
        float scale=offsets.Select(p=>p.Length()).DefaultIfEmpty(0).Max();
        var canonical=Matrix4x4.Transpose(basis);var result=new BranchDescription[count];
        BranchDescription Describe(int bone)
        {
            if(result[bone] is {} cached)return cached;
            var descendants=children[bone].SelectMany(i=>new[]{i}.Concat(Describe(i).Descendants)).ToArray();
            string topology="("+string.Concat(children[bone].Select(i=>Describe(i).Topology).Order(StringComparer.Ordinal))+")";
            string reason=helperSet.Contains(bone)||descendants.Any(helperSet.Contains)?"Native helper branch requires a reviewed correspondence":
                weights[bone]<=0?"No direct skin influence evidence; unweighted selectors/sockets require reviewed correspondence":
                offsets[bone].Length()<=scale*1e-7f?"Coincident rest branch requires reviewed semantic evidence":"";
            var direction=Vector3.Zero;var centroid=Vector3.Zero;
            if(reason.Length==0)
            {
                float length=offsets[bone].Length();direction=Vector3.Normalize(Vector3.TransformNormal(offsets[bone],canonical));
                var relative=Vector3.TransformNormal(centroids[bone]/(float)weights[bone]-rest.World[bone].Translation,canonical)/length;
                // A common branch frame uses reviewed model axes, never native
                // bone labels or their local Euler conventions.
                var seed=Math.Abs(Vector3.Dot(direction,Vector3.UnitY))<.9f?Vector3.UnitY:Vector3.UnitX;
                var side=Vector3.Normalize(seed-direction*Vector3.Dot(seed,direction));var normal=Vector3.Cross(direction,side);
                centroid=new(Vector3.Dot(relative,direction),Vector3.Dot(relative,side),Vector3.Dot(relative,normal));
            }
            return result[bone]=new(topology,descendants,direction,centroid,reason);
        }
        for(int i=0;i<count;i++)Describe(i);return result;
    }
    static bool Compatible(BranchDescription source,BranchDescription target)=>source.Topology==target.Topology&&
        Vector3.Dot(source.Direction,target.Direction)>=BranchDirectionCosine&&
        Vector3.Distance(source.SkinCentroid,target.SkinCentroid)<=SkinCentroidDistance;
    static bool IsDetailRole(string role)=>role.StartsWith("Finger.",StringComparison.Ordinal)||role.StartsWith("Shared.",StringComparison.Ordinal)||
        role.StartsWith("Channel.",StringComparison.Ordinal)||role.StartsWith("Grip.",StringComparison.Ordinal)||role=="ThrowAnchor";
    // These are existing semantic policies, not native bone/name exceptions.
    // Other custom roles retain their identity/evidence in the alias collection.
    static int RolePriority(string role)=>role=="RootMotion"?0:role=="Hips"?1:IsDetailRole(role)?3:2;
    static (bool Supplied,bool Reviewed,string Text) Evidence(ControlRigSetup setup,string role,int bone)
    {
        var explicitRole=setup.BoneRoles.FirstOrDefault(r=>r.Reviewed&&r.SemanticRole==role&&r.Bone==bone);
        if(explicitRole is not null)return(true,true,explicitRole.Evidence);
        if(role=="Hips"&&setup.BodyBone==bone)return(false,false,"BodyBone supplies a body candidate but records no reviewed role origin; automatic setup and an explicit user binding cannot be distinguished");
        foreach(var chain in setup.Chains)
        {
            string side=chain.Role[(chain.Role.LastIndexOf('.')+1)..];
            string[] roles=chain.Role.StartsWith("Hand.",StringComparison.Ordinal)?["UpperArm."+side,"Elbow."+side,"Wrist."+side]:
                chain.Role.StartsWith("Foot.",StringComparison.Ordinal)?["UpperLeg."+side,"Knee."+side,"Ankle."+side]:[];
            int[] bones=[chain.Root,chain.Middle,chain.Effector];
            for(int i=0;i<roles.Length;i++)if(roles[i]==role&&bones[i]==bone)return(chain.Reviewed,chain.Reviewed,chain.Evidence);
        }
        return(false,false,"Derived from current native hierarchy; no reviewed per-role binding is recorded");
    }
}
