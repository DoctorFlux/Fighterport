using System.Numerics;
using System.Runtime.CompilerServices;

namespace FighterPort.Core;

// Virtual bindings reference native array identities. They never add deform bones.
public record RigChainBinding(string Role,int[] Bones,string Evidence,bool Reviewed);
public record RigCoordinateProfile(float UnitsPerMeter,string Evidence);
public record SemanticRigBinding(Dictionary<string,int> Roles,RigChainBinding[] Chains,string Evidence);
public record RigDescriptor(Scene Scene,string SkeletonFingerprint,string DeformChecksum,EvaluatedPose Rest,
    Matrix4x4[] RestRotations,Matrix4x4 Basis,SemanticRigBinding Binding,float UnitsPerMeter,Diagnostic[] Diagnostics)
{
    public const string Version="native-rig-descriptor-2";
    // Prepared once for offline collision refinement; playback never builds or
    // optimizes this set. Include unclassified skin, not only named regions.
    public SkinSurfacePoint[] SkinPoints {get;init;}=[];
    public int[] CanonicalBoneRanks {get;init;}=[];
    static readonly ConditionalWeakTable<Scene,ConditionalWeakTable<ControlRigSetup,RigDescriptor>> Prepared=new();
    public float ChainLength(string role)
    {
        var chain=Binding.Chains.SingleOrDefault(c=>c.Role==role);
        return chain is null?0:chain.Bones.Zip(chain.Bones.Skip(1),(a,b)=>Vector3.Distance(Rest.World[a].Translation,Rest.World[b].Translation)).Sum();
    }
    // Channels above the reviewed anatomical body belong to actor placement,
    // not to body fitting. Preserve them regardless of their native names or
    // how many separate rotation/translation helpers a format uses.
    public bool IsActorAncestor(int bone)=>Binding.Roles.TryGetValue("Hips",out int body)&&bone!=body&&PoseGeometry.Ancestor(Scene,bone,body);
    public float CharacteristicLength
    {
        get{float length=Binding.Chains.Select(c=>ChainLength(c.Role)).DefaultIfEmpty(0).Max();if(length>0)return length;float extent=Rest.World.Select(m=>Vector3.Distance(m.Translation,Rest.World[0].Translation)).DefaultIfEmpty(0).Max();return extent>0?extent:1;}
    }
    public static RigDescriptor Prepare(Scene scene,ControlRigSetup setup)
        =>Prepared.GetValue(scene,static _=>new()).GetValue(setup,s=>Build(scene,s));
    static RigDescriptor Build(Scene scene,ControlRigSetup setup)
    {
        scene.Validate();string fingerprint=AnimationControls.Fingerprint(scene);
        if(fingerprint!=setup.SkeletonFingerprint)throw new InvalidDataException("Rig bindings refer to another native skeleton/rest revision.");
        var rotations=new Matrix4x4[scene.Bones.Length];
        for(int i=0;i<scene.Bones.Length;i++)
        {
            var bone=scene.Bones[i];var s=bone.Rest.Scale;
            if(!bone.Rest.Channels().All(float.IsFinite)||s.X<=0||s.Y<=0||s.Z<=0)throw new NotSupportedException($"Native bone {i} '{bone.Name}' has a reflected, singular or nonfinite rest transform. A reviewed affine policy is required; the bind was not normalized.");
            var local=(Transform.Identity with{Rotation=bone.Rest.Rotation}).Matrix;
            rotations[i]=local*(bone.Parent<0?Matrix4x4.Identity:rotations[bone.Parent]);
        }
        var rest=AnimationService.Evaluate(scene,null,0);
        foreach(var limit in setup.JointLimits??[])
            if(!limit.Reviewed||string.IsNullOrWhiteSpace(limit.Evidence)||limit.Bone<0||limit.Bone>=scene.Bones.Length||!PoseGeometry.Finite(limit.MinimumDegrees)||!PoseGeometry.Finite(limit.MaximumDegrees)||Enumerable.Range(0,3).Any(a=>limit.MinimumDegrees[a]>limit.MaximumDegrees[a]))
                throw new InvalidDataException("Joint limits require reviewed target-native bounds and provenance; source extrema are not hard limits.");
        var unassigned=(setup.UnassignedBoneRoles??[]).ToHashSet(StringComparer.Ordinal);
        if(unassigned.Contains("RootMotion"))throw new InvalidDataException("The actor/root-motion binding was explicitly left unassigned. Review an existing native actor bone before retargeting.");
        var roles=new Dictionary<string,int>(StringComparer.Ordinal);
        void Role(string name,int bone){if(bone<0||bone>=scene.Bones.Length)throw new InvalidDataException($"Role '{name}' references missing bone {bone}.");if(roles.TryGetValue(name,out int old)&&old!=bone)throw new InvalidDataException($"Role '{name}' has ambiguous native bindings.");roles[name]=bone;}
        foreach(var mapping in setup.BoneRoles.Where(r=>r.Reviewed&&!unassigned.Contains(r.SemanticRole)))Role(mapping.SemanticRole,mapping.Bone);
        if(!unassigned.Contains("Hips")&&setup.BodyBone is {} body)Role("Hips",body);
        var chains=new List<RigChainBinding>();
        foreach(var chain in setup.Chains)
        {
            var path=new List<int>();int bone=chain.Effector;
            while(bone>=0){path.Add(bone);if(bone==chain.Root)break;bone=scene.Bones[bone].Parent;}
            path.Reverse();if(path.Count<2||path[0]!=chain.Root||!path.Contains(chain.Middle))throw new InvalidDataException($"Chain '{chain.Role}' is not an ordered native ancestor path.");
            chains.Add(new(chain.Role,path.ToArray(),chain.Evidence,chain.Reviewed));
            string side=chain.Role[(chain.Role.LastIndexOf('.')+1)..];
            if(chain.Role.StartsWith("Hand.",StringComparison.Ordinal)){Role("UpperArm."+side,chain.Root);Role("Elbow."+side,chain.Middle);Role("Wrist."+side,chain.Effector);}
            else if(chain.Role.StartsWith("Foot.",StringComparison.Ordinal)){Role("UpperLeg."+side,chain.Root);Role("Knee."+side,chain.Middle);Role("Ankle."+side,chain.Effector);}
        }
        foreach(var chain in setup.VirtualChains??[])
        {
            if(chain.Bones.Length<2||chain.Bones.Distinct().Count()!=chain.Bones.Length)throw new InvalidDataException($"Virtual chain '{chain.Role}' must contain distinct native bones.");
            for(int i=0;i<chain.Bones.Length;i++)if(chain.Bones[i]<0||chain.Bones[i]>=scene.Bones.Length||i>0&&scene.Bones[chain.Bones[i]].Parent!=chain.Bones[i-1])throw new InvalidDataException($"Virtual chain '{chain.Role}' is not a complete ordered native path.");
            chains.RemoveAll(c=>c.Role==chain.Role);chains.Add(chain);
        }
        int root=roles.GetValueOrDefault("RootMotion",setup.BodyBone??0);while(scene.Bones[root].Parent>=0)root=scene.Bones[root].Parent;
        if(!roles.ContainsKey("RootMotion"))Role("RootMotion",root);
        var axes=setup.Axes??throw new InvalidDataException("Retargeting needs reviewed rig axes; bone names do not establish coordinates.");
        var right=Vector3.Normalize(axes.Right);var up=Vector3.Normalize(axes.Up);var forward=Vector3.Normalize(axes.Forward);
        if(!PoseGeometry.Finite(right)||!PoseGeometry.Finite(up)||!PoseGeometry.Finite(forward)||Math.Abs(Vector3.Dot(right,up))>.0001f||Math.Abs(Vector3.Dot(right,forward))>.0001f||Math.Abs(Vector3.Dot(up,forward))>.0001f||Math.Abs(Vector3.Dot(Vector3.Cross(right,up),forward))<.9999f)throw new NotSupportedException("Rig axes must specify a finite orthonormal frame; skewed coordinates need an explicit affine policy.");
        var basis=new Matrix4x4(right.X,right.Y,right.Z,0,up.X,up.Y,up.Z,0,forward.X,forward.Y,forward.Z,0,0,0,0,1);
        float units=setup.Coordinates?.UnitsPerMeter??1;
        if(!float.IsFinite(units)||units<=0)throw new InvalidDataException("File units per metre must be finite and positive.");
        var diagnostics=new List<Diagnostic>();
        if(basis.GetDeterminant()<0)diagnostics.Add(new("LEFT_HANDED_COORDINATES","The reviewed coordinate frame is left-handed. Changes of basis retain that reflection as matrices; rotations are conjugated, never converted from a reflection quaternion.",false));
        if(setup.Coordinates is null)diagnostics.Add(new("UNIT_CONTEXT","Physical file units are unspecified; anatomical transfers use declared model units. No metre-based scene or actor compatibility is claimed.",false));
        if(chains.Any(c=>!c.Reviewed))diagnostics.Add(new("RIG_BINDING_REVIEW","Unreviewed virtual/control-chain bindings require focused review.",false));
        return new(scene,fingerprint,AnimationControls.DeformChecksum(scene),rest,rotations,basis,new(roles,chains.ToArray(),setup.Provenance?.Evidence??"Exact saved native rig bindings"),units,diagnostics.ToArray())
        {SkinPoints=scene.Meshes.SelectMany((m,mi)=>m.Vertices.Select((v,vi)=>new SkinSurfacePoint(v.Position,v.Joints,v.Weights){Influences=v.Influences,Mesh=mi,Vertex=vi})).ToArray(),CanonicalBoneRanks=CanonicalRanks(scene,roles)};
    }
    static int[] CanonicalRanks(Scene scene,Dictionary<string,int> roles)
    {
        // Stable algebra/seed traversal under valid native storage permutations.
        // Keys contain supplied semantic bindings, actual rest topology and skin
        // incidence, never native names, character identity or asset hashes.
        int count=scene.Bones.Length;var first=Enumerable.Repeat(long.MaxValue,count).ToArray();long vertex=0;
        foreach(var mesh in scene.Meshes)foreach(var point in mesh.Vertices)
        {
            for(int slot=0;slot<point.InfluenceCount;slot++)if(point.WeightAt(slot)>0){int bone=(int)point.JointAt(slot);first[bone]=Math.Min(first[bone],vertex);}
            vertex++;
        }
        var children=Enumerable.Range(0,count).Select(b=>Enumerable.Range(0,count).Where(c=>scene.Bones[c].Parent==b).ToArray()).ToArray();var keys=new string[count];
        string Key(int bone)
        {
            if(keys[bone] is {} cached)return cached;
            string semantic=string.Join('/',roles.Where(p=>p.Value==bone).Select(p=>p.Key).Order(StringComparer.Ordinal));
            string rest=string.Join(',',scene.Bones[bone].Rest.Channels().Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            return keys[bone]=semantic+"|"+rest+"|"+first[bone].ToString(System.Globalization.CultureInfo.InvariantCulture)+"["+string.Join(';',children[bone].Select(Key).Order(StringComparer.Ordinal))+"]";
        }
        var rank=new int[count];int next=0;
        void Visit(int bone){rank[bone]=next++;foreach(int child in children[bone].OrderBy(Key,StringComparer.Ordinal))Visit(child);}
        foreach(int root in Enumerable.Range(0,count).Where(b=>scene.Bones[b].Parent<0).OrderBy(Key,StringComparer.Ordinal))Visit(root);
        return rank;
    }
}
