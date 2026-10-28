using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace FighterPort.Core;

public record ChainCalibration(float UpperLength,float LowerLength,Vector3 UpperLocalAxis,Vector3 LowerLocalAxis,Vector3 PoleInParentSpace);
public record ControlChain(string Role,int Root,int Middle,int Effector,Vector3 PoleDirection,string Evidence,bool Reviewed=false)
{
    public ChainCalibration? Calibration {get;init;}
}
public record ControlSetupProvenance(string ProfileId,int Revision,SourceBuildIdentity Source,string Coordinates,string Evidence);
public record ControlRigSetup(Guid Id,string SkeletonFingerprint,ControlChain[] Chains,Diagnostic[] Diagnostics,string SolverVersion="two-bone-1")
{
    public ControlSetupProvenance? Provenance {get;init;}
    public RigMapping[] BoneRoles {get;init;}=[];
    // Explicit unassignment differs from an older setup that has not reviewed
    // the role yet. Preserve this decision when profiles/inference are reused.
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string[]? UnassignedBoneRoles {get;init;}
    public int[] PreservedHelperBones {get;init;}=[];
    public int? BodyBone {get;init;}
    public RigAxes? Axes {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public RigCoordinateProfile? Coordinates {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public RigChainBinding[]? VirtualChains {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public NativeJointLimit[]? JointLimits {get;init;}
    public float? GroundPlaneHeight {get;init;}
    // Legacy serialized calibration fields remain readable for provenance.
    // Retarget detection uses the global/explicit recipe policy, never these
    // rig-specific numerical overrides.
    public float? GroundLimbToleranceFraction {get;init;}
    public float? GroundFootToleranceFraction {get;init;}
}
public record IkSolution(Dictionary<int,Transform> Transforms,Vector3 Achieved,float Error);

public static class AnimationControls
{
    public static string Fingerprint(Scene scene)
    {
        using var stream=new MemoryStream();using(var w=new BinaryWriter(stream,Encoding.UTF8,true))
            foreach(var b in scene.Bones){w.Write(b.Name);w.Write(b.Parent);foreach(var x in b.Rest.Channels())w.Write(x);}
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    public static string DeformChecksum(Scene scene)
    {
        using var stream=new MemoryStream();using(var w=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            w.Write(Fingerprint(scene));foreach(var mesh in scene.Meshes)foreach(var v in mesh.Vertices)
            {w.Write(v.Position.X);w.Write(v.Position.Y);w.Write(v.Position.Z);if(v.Influences.Length>0){w.Write(int.MinValue);w.Write(v.InfluenceCount);}for(int i=0;i<v.InfluenceCount;i++){w.Write(v.JointAt(i));w.Write(v.WeightAt(i));}}
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    public static ControlRigSetup SetUp(Scene scene,IEnumerable<ControlRigSetup>? saved=null)
    {
        var fingerprint=Fingerprint(scene);var exact=saved?.FirstOrDefault(s=>s.SkeletonFingerprint==fingerprint);
        if(exact is not null)return exact;
        var pose=AnimationService.Evaluate(scene,null,0);var diagnostics=new List<Diagnostic>();var result=new List<ControlChain>();
        var children=Enumerable.Range(0,scene.Bones.Length).Select(i=>Enumerable.Range(0,scene.Bones.Length).Where(j=>scene.Bones[j].Parent==i).ToArray()).ToArray();
        var weighted=scene.Meshes.SelectMany(m=>m.Vertices).SelectMany(v=>Enumerable.Range(0,v.InfluenceCount).Where(i=>v.WeightAt(i)>0).Select(i=>(int)v.JointAt(i))).ToHashSet();
        string Normal(int i)=>new(scene.Bones[i].Name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        int NameScore(int i,string family)=>family switch{
            "hand"=>Normal(i).Contains("hand")||Normal(i).Contains("wrist")?8:0,
            "foot"=>Normal(i).Contains("foot")||Normal(i).Contains("ankle")?8:0,
            _=>0};
        // Branch geometry and symmetry establish torso candidates. Names only rank structurally valid chains.
        for(int branch=0;branch<children.Length;branch++)
        {
            var starts=children[branch];if(starts.Length<3)continue;var origin=pose.World[branch].Translation;
            var lateral=starts.Where(c=>Math.Abs(pose.World[c].Translation.X-origin.X)>.015f).ToArray();
            foreach(var side in new[]{-1,1})
            {
                var candidates=new List<(ControlChain Chain,float Score)>();
                foreach(int start in lateral.Where(c=>(pose.World[c].Translation.X-origin.X)*side>0))
                {
                    foreach(int middle in children[start])foreach(int end in children[middle])
                    {
                        var a=pose.World[start].Translation;var b=pose.World[middle].Translation;var c=pose.World[end].Translation;
                        float l1=Vector3.Distance(a,b),l2=Vector3.Distance(b,c);if(l1<.0001f||l2<.0001f||l1/l2 is <.15f or >6f)continue;
                        if(!weighted.Contains(start)&&!weighted.Contains(middle))continue;
                        var d=c-a;bool leg=d.Y<-Math.Abs(d.X)*.8f;bool arm=Math.Abs(d.X)>Math.Abs(d.Y)*.8f;
                        if(!leg&&!arm)continue;
                        string family=leg?"foot":"hand";string role=(leg?"Foot":"Hand")+(side<0?".L":".R");
                        // Require the opposite branch to have comparable bind geometry; this avoids lone prop chains.
                        bool symmetric=lateral.Any(other=>other!=start&&(pose.World[other].Translation.X-origin.X)*side<0&&Math.Abs((pose.World[other].Translation-origin).Length()-(a-origin).Length())<Math.Max(.02f,(a-origin).Length()*.3f));
                        if(!symmetric)continue;
                        float score=NameScore(end,family)+(weighted.Contains(end)?1:0)+(children[end].Length==0?1:0);
                        var evidence=$"Parent/child chain; bind lengths {l1:G4}/{l2:G4}; symmetric branch; skin influence evidence; names supporting only";
                        candidates.Add((new(role,start,middle,end,Vector3.UnitZ,evidence),score));
                    }
                }
                foreach(var group in candidates.GroupBy(x=>x.Chain.Role))
                {
                    var ranked=group.OrderByDescending(x=>x.Score).ToArray();
                    if(ranked.Length>1&&Math.Abs(ranked[0].Score-ranked[1].Score)<.01f){diagnostics.Add(new("AMBIGUOUS_CHAIN",$"Review {group.Key}: {string.Join(", ",ranked.Select(x=>scene.Bones[x.Chain.Root].Name+" → "+scene.Bones[x.Chain.Effector].Name))}",false));continue;}
                    if(!result.Any(x=>x.Role==ranked[0].Chain.Role))result.Add(ranked[0].Chain);
                }
            }
        }
        if(result.Count==0)diagnostics.Add(new("NO_UNAMBIGUOUS_CHAIN","No unambiguous weighted three-bone limbs found. FK remains available; manually map only the desired chain.",false));
        return new(Guid.NewGuid(),fingerprint,result.Select(c=>Calibrate(scene,pose,c)).ToArray(),diagnostics.ToArray());
    }
    static ControlChain Calibrate(Scene scene,EvaluatedPose bind,ControlChain chain)
    {
        int parent=scene.Bones[chain.Root].Parent;var inverse=Matrix4x4.Identity;
        if(parent>=0&&!Matrix4x4.Invert(bind.World[parent],out inverse))throw new InvalidDataException("Singular control parent bind.");
        var pole=Vector3.Normalize(chain.PoleDirection);
        return chain with{PoleDirection=pole,Calibration=new(
            Vector3.Distance(bind.World[chain.Root].Translation,bind.World[chain.Middle].Translation),
            Vector3.Distance(bind.World[chain.Middle].Translation,bind.World[chain.Effector].Translation),
            Vector3.Normalize(scene.Bones[chain.Middle].Rest.Translation),Vector3.Normalize(scene.Bones[chain.Effector].Rest.Translation),
            Vector3.Normalize(Vector3.TransformNormal(pole,inverse)))};
    }
    public static OperationResult<ControlRigSetup> ReviewChain(Scene scene,ControlRigSetup setup,string role,int root,int middle,int effector,Vector3 pole,string evidence)
    {
        OperationResult<ControlRigSetup> Fail(string code,string message)=>new(null,[new(code,message)],[]);
        if(setup.SkeletonFingerprint!=Fingerprint(scene))return Fail("RIG_CHANGED","Review belongs to a different skeleton/rest revision. Set up the current rig first.");
        if(string.IsNullOrWhiteSpace(role)||string.IsNullOrWhiteSpace(evidence))return Fail("REVIEW_EVIDENCE","A role and mapping evidence are required.");
        if(new[]{root,middle,effector}.Any(i=>i<0||i>=scene.Bones.Length))return Fail("INVALID_CHAIN","Choose three existing bones.");
        if(scene.Bones[middle].Parent!=root||scene.Bones[effector].Parent!=middle)return Fail("CHAIN_HELPERS","Choose a direct parent-child limb. Intervening helper solvers are not implemented; no hierarchy will be changed.");
        if(!Finite(pole)||pole.LengthSquared()<1e-10f)return Fail("INVALID_POLE","Choose a finite nonzero pole direction in bind-model coordinates.");
        if(setup.Chains.Any(c=>c.Role!=role&&c.Effector==effector))return Fail("DUPLICATE_EFFECTOR","This effector already belongs to another role; correct that role first.");
        var bind=AnimationService.Evaluate(scene,null,0);
        if(Vector3.DistanceSquared(bind.World[root].Translation,bind.World[middle].Translation)<1e-10f||Vector3.DistanceSquared(bind.World[middle].Translation,bind.World[effector].Translation)<1e-10f)return Fail("ZERO_LENGTH","A deform segment has zero bind length.");
        var chain=Calibrate(scene,bind,new(role,root,middle,effector,pole,evidence,true));
        var next=setup with{Id=Guid.NewGuid(),Chains=setup.Chains.Where(c=>c.Role!=role).Append(chain).OrderBy(c=>c.Role,StringComparer.Ordinal).ToArray(),
            PreservedHelperBones=setup.PreservedHelperBones.Where(i=>i!=root&&i!=middle&&i!=effector).ToArray(),
            Diagnostics=setup.Diagnostics.Where(d=>d.Code!="NO_UNAMBIGUOUS_CHAIN"&&!(d.Code=="AMBIGUOUS_CHAIN"&&d.Message.Contains(role,StringComparison.Ordinal))).ToArray()};
        if(role is "Hand.L" or "Hand.R" or "Foot.L" or "Foot.R")
        {
            string side=role[^1..];string[] names=role.StartsWith("Hand",StringComparison.Ordinal)?["UpperArm.","Elbow.","Wrist."]:["UpperLeg.","Knee.","Ankle."];
            var mapped=names.Select((name,i)=>new RigMapping(name+side,new[]{root,middle,effector}[i],evidence,true)).ToArray();
            var unassigned=next.UnassignedBoneRoles?.Where(r=>!mapped.Any(m=>m.SemanticRole==r)).ToArray();
            next=next with{BoneRoles=next.BoneRoles.Where(m=>!mapped.Any(n=>n.SemanticRole==m.SemanticRole)).Concat(mapped).ToArray(),
                UnassignedBoneRoles=unassigned is {Length:>0}?unassigned:null};
        }
        return new(next,[],[]);
    }
    public static OperationResult<ControlRigSetup> ReviewBoneRole(Scene scene,ControlRigSetup setup,string role,int? bone,string evidence)
    {
        OperationResult<ControlRigSetup> Fail(string code,string message)=>new(null,[new(code,message)],[]);
        if(setup.SkeletonFingerprint!=Fingerprint(scene))return Fail("RIG_CHANGED","The role review belongs to another skeleton/rest revision.");
        role=role.Trim();
        if(role.Length==0||string.IsNullOrWhiteSpace(evidence))return Fail("REVIEW_EVIDENCE","A semantic role and review evidence are required.");
        if(bone is {} selected&&(selected<0||selected>=scene.Bones.Length))return Fail("INVALID_BONE","Choose an existing native bone, or explicitly leave the role unassigned.");
        // Limb semantics also exist in the saved IK chain. Editing only one
        // representation would make posing and retargeting disagree.
        foreach(var chain in setup.Chains)
        {
            string side=chain.Role[(chain.Role.LastIndexOf('.')+1)..];
            string[] names=chain.Role.StartsWith("Hand.",StringComparison.Ordinal)?["UpperArm."+side,"Elbow."+side,"Wrist."+side]:
                chain.Role.StartsWith("Foot.",StringComparison.Ordinal)?["UpperLeg."+side,"Knee."+side,"Ankle."+side]:[];
            int index=Array.IndexOf(names,role);
            if(index>=0&&bone!=new[]{chain.Root,chain.Middle,chain.Effector}[index])
                return Fail("CHAIN_ROLE_CONFLICT",$"'{role}' is owned by IK chain '{chain.Role}'. Review that chain to change its joints; FK/IK and retargeting must share the same binding.");
        }
        if(role=="Hips"&&bone is {} body&&setup.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).Any(c=>!PoseGeometry.Ancestor(scene,body,c.Root)))
            return Fail("BODY_HIERARCHY","The reviewed body must be an ancestor of the configured leg roots. Choose the existing shared body ancestor or review the limb mappings.");
        var previous=setup.BoneRoles.Where(r=>r.SemanticRole==role).ToArray();
        bool explicitlyUnassigned=setup.UnassignedBoneRoles?.Contains(role,StringComparer.Ordinal)==true;
        if(bone is {} same&&previous is [{Reviewed:true} existing]&&existing.Bone==same&&!explicitlyUnassigned&&(role!="Hips"||setup.BodyBone is null||setup.BodyBone==same)||
            bone is null&&previous.Length==0&&explicitlyUnassigned&&(role!="Hips"||setup.BodyBone is null))
            return new(setup,[],[]);
        var roles=setup.BoneRoles.Where(r=>r.SemanticRole!=role).ToList();
        if(bone is {} value)roles.Add(new(role,value,evidence,true));
        var unassigned=(setup.UnassignedBoneRoles??[]).ToHashSet(StringComparer.Ordinal);
        if(bone is null)unassigned.Add(role);else unassigned.Remove(role);
        var diagnostics=setup.Diagnostics.Where(d=>d.Code!="BONE_ROLE_UNASSIGNED"||!d.Message.StartsWith(role+":",StringComparison.Ordinal)).ToList();
        if(bone is null)diagnostics.Add(new("BONE_ROLE_UNASSIGNED",role+": explicitly unassigned; inference/profile reuse must not restore this binding.",false));
        return new(setup with
        {
            Id=Guid.NewGuid(),BoneRoles=roles.ToArray(),BodyBone=role=="Hips"?bone:setup.BodyBone,
            UnassignedBoneRoles=unassigned.Count>0?unassigned.Order(StringComparer.Ordinal).ToArray():null,Diagnostics=diagnostics.ToArray()
        },[],[]);
    }
    public static int ResolveBodyBone(Scene scene,ControlRigSetup setup)
    {
        if(setup.UnassignedBoneRoles?.Contains("Hips",StringComparer.Ordinal)==true)return -1;
        var roles=setup.BoneRoles.Where(r=>r.Reviewed&&r.SemanticRole=="Hips").Select(r=>r.Bone).Distinct().ToArray();
        if(roles.Length>1||roles.Length==1&&setup.BodyBone is {} explicitBody&&explicitBody!=roles[0])return -1;
        var legs=setup.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).ToArray();
        int? candidate=roles.Length==1?roles[0]:setup.BodyBone;
        if(candidate is {} body)return body>=0&&body<scene.Bones.Length&&legs.All(c=>PoseGeometry.Ancestor(scene,body,c.Root))?body:-1;
        // Retain historical structural inference only when no role decision
        // exists. A reviewed torso-only rig does not require invented legs.
        if(legs.Length<2)return -1;
        for(int i=scene.Bones[legs[0].Root].Parent;i>=0;i=scene.Bones[i].Parent)
            if(legs.All(c=>PoseGeometry.Ancestor(scene,i,c.Root)))return i;
        return -1;
    }
    public static int[] ProtectedMotionBones(Scene scene,ControlRigSetup setup)
    {
        int body=ResolveBodyBone(scene,setup);
        var result=new List<int>();
        if(body<0)return []; // No role claim can be inferred from a name alone.
        for(int i=scene.Bones[body].Parent;i>=0;i=scene.Bones[i].Parent)result.Add(i);
        return result.ToArray();
    }
    public static Vector3 DefaultPole(Scene scene,EvaluatedPose pose,ControlChain chain)
    {
        var root=pose.World[chain.Root].Translation;var mid=pose.World[chain.Middle].Translation;var end=pose.World[chain.Effector].Translation;
        float length=Vector3.Distance(root,mid)+Vector3.Distance(mid,end);var axis=end-root;
        var fallback=chain.PoleDirection;int parent=scene.Bones[chain.Root].Parent;
        if(chain.Calibration is {} calibration)fallback=Vector3.TransformNormal(calibration.PoleInParentSpace,parent<0?Matrix4x4.Identity:pose.World[parent]);
        if(axis.LengthSquared()>1e-10f)
        {
            axis=Vector3.Normalize(axis);var bend=mid-root-axis*Vector3.Dot(mid-root,axis);
            // Preserve an existing bend; a straight limb uses the calibrated parent-relative pole.
            if(bend.Length()>length*.001f)fallback=bend;
            fallback-=axis*Vector3.Dot(fallback,axis);
            if(fallback.LengthSquared()<1e-10f)fallback=Vector3.Cross(axis,Math.Abs(axis.Y)<.9f?Vector3.UnitY:Vector3.UnitX);
        }
        return root+Vector3.Normalize(fallback)*length;
    }
    public static OperationResult<IkSolution> Solve(Scene scene,EvaluatedPose input,ControlRigSetup setup,ControlChain chain,Vector3 goal,Vector3 pole)
    {
        OperationResult<IkSolution> Fail(string code,string message)=>new(null,[new(code,message)],[]);
        if(setup.SkeletonFingerprint!=Fingerprint(scene))return Fail("RIG_CHANGED","The saved control setup belongs to a different skeleton/rest revision.");
        if(!setup.Chains.Any(c=>c.Role==chain.Role&&c.Root==chain.Root&&c.Middle==chain.Middle&&c.Effector==chain.Effector))return Fail("UNMAPPED_CHAIN","The selected chain is not part of this saved setup.");
        if(chain.Root<0||chain.Middle<0||chain.Effector<0||chain.Root>=scene.Bones.Length||chain.Middle>=scene.Bones.Length||chain.Effector>=scene.Bones.Length)return Fail("INVALID_CHAIN","Control bone indices are invalid.");
        if(scene.Bones[chain.Middle].Parent!=chain.Root || scene.Bones[chain.Effector].Parent!=chain.Middle)return Fail("CHAIN_HELPERS","This solver currently requires direct parent-child links; helper/twist-chain solving needs an explicit profile.");
        if(!Finite(goal)||!Finite(pole))return Fail("INVALID_GOAL","Goal/pole must be finite.");
        for(int i=chain.Effector;i>=0;i=scene.Bones[i].Parent)
        {
            var s=input.Local[i].Scale;
            if(Math.Abs(s.X-s.Y)>.0001f||Math.Abs(s.Y-s.Z)>.0001f||s.X<=0)return Fail("NONUNIFORM_SCALE","IK for nonuniform or nonpositive ancestor scale requires a separate verified solver. FK remains available.");
        }
        var a=input.World[chain.Root].Translation;var b=input.World[chain.Middle].Translation;var c=input.World[chain.Effector].Translation;
        float first=Vector3.Distance(a,b),second=Vector3.Distance(b,c),distance=Vector3.Distance(a,goal);
        if(first<1e-6f||second<1e-6f)return Fail("ZERO_LENGTH","Selected chain has a zero-length deform segment.");
        if(distance>first+second+1e-5f||distance<Math.Abs(first-second)+1e-5f)return Fail("UNREACHABLE_GOAL",$"Goal distance {distance:G6} is outside [{Math.Abs(first-second):G6}, {first+second:G6}]. No bone stretching or bind change was applied.");
        var direction=Vector3.Normalize(goal-a);var bend=pole-a-direction*Vector3.Dot(pole-a,direction);
        if(bend.LengthSquared()<1e-10f)
        {
            var fallback=DefaultPole(scene,input,chain)-a;bend=fallback-direction*Vector3.Dot(fallback,direction);
            if(bend.LengthSquared()<1e-10f){var basis=Math.Abs(direction.Y)<.9f?Vector3.UnitY:Vector3.UnitX;bend=basis-direction*Vector3.Dot(basis,direction);}
        }
        bend=Vector3.Normalize(bend);float along=(first*first-second*second+distance*distance)/(2*distance);float height=MathF.Sqrt(Math.Max(0,first*first-along*along));
        var desiredMiddle=a+direction*along+bend*height;
        var local=input.Local.Select((v,i)=>(v,i)).ToDictionary(x=>x.i,x=>x.v);
        local[chain.Root]=RotateWorld(scene,input,chain.Root,b-a,desiredMiddle-a);
        var afterRoot=AnimationService.Evaluate(scene,null,0,local);var newB=afterRoot.World[chain.Middle].Translation;var newC=afterRoot.World[chain.Effector].Translation;
        local[chain.Middle]=RotateWorld(scene,afterRoot,chain.Middle,newC-newB,goal-newB);
        var solved=AnimationService.Evaluate(scene,null,0,local);var actual=solved.World[chain.Effector].Translation;float error=Vector3.Distance(actual,goal);
        if(error>Math.Max(first+second,1)*.0001f)return Fail("SOLVE_ERROR",$"IK residual {error:G6} exceeds this solver's tolerance. Original pose retained.");
        return new(new(new(){{chain.Root,local[chain.Root]},{chain.Middle,local[chain.Middle]}},actual,error),[],[]);
    }
    static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
    static Transform RotateWorld(Scene scene,EvaluatedPose pose,int bone,Vector3 from,Vector3 to)
    {
        Quaternion delta=DirectionRotation(from,to);
        Matrix4x4.Decompose(pose.World[bone],out _,out var worldRotation,out _);int parent=scene.Bones[bone].Parent;
        Quaternion parentRotation=Quaternion.Identity;if(parent>=0)Matrix4x4.Decompose(pose.World[parent],out _,out parentRotation,out _);
        var local=Matrix4x4.CreateFromQuaternion(worldRotation)*Matrix4x4.CreateFromQuaternion(delta)*Matrix4x4.Transpose(Matrix4x4.CreateFromQuaternion(parentRotation));
        return pose.Local[bone] with {Rotation=Euler(local,pose.Local[bone].Rotation)};
    }
    internal static Quaternion DirectionRotation(Vector3 from,Vector3 to)
    {
        from=Vector3.Normalize(from);to=Vector3.Normalize(to);
        var cross=Vector3.Cross(from,to);
        double sine=Math.Sqrt((double)cross.X*cross.X+(double)cross.Y*cross.Y+(double)cross.Z*cross.Z);
        double cosine=Math.Clamp((double)from.X*to.X+(double)from.Y*to.Y+(double)from.Z*to.Z,-1,1);
        // Preserve small real corrections. A dot-product dead zone can exceed
        // the existing endpoint tolerance, especially near full extension.
        if(sine>0)
        {
            var axis=new Vector3((float)(cross.X/sine),(float)(cross.Y/sine),(float)(cross.Z/sine));
            return Quaternion.CreateFromAxisAngle(axis,(float)Math.Atan2(sine,cosine));
        }
        if(cosine>=0)return Quaternion.Identity;
        return Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(from,Math.Abs(from.Y)<.9f?Vector3.UnitY:Vector3.UnitX)),MathF.PI);
    }
    public static Vector3 Euler(Matrix4x4 m,Vector3 near)
    {
        // Convert rotation coefficients through ONE coherent double-precision
        // orthogonal matrix. Independently rounded float entries near a Y
        // quarter turn amplify X/Z ratio errors; asin also loses Y resolution.
        // This projects a computed rotation only, never native binds or scale.
        var q=Quaternion.CreateFromRotationMatrix(m);
        double qx=q.X,qy=q.Y,qz=q.Z,qw=q.W,norm=Math.Sqrt(qx*qx+qy*qy+qz*qz+qw*qw);
        if(!double.IsFinite(norm)||norm==0)throw new ArgumentException("Euler conversion needs a finite rotation matrix.");
        qx/=norm;qy/=norm;qz/=norm;qw/=norm;
        double m11=1-2*(qy*qy+qz*qz),m12=2*(qx*qy+qz*qw),m13=2*(qx*qz-qy*qw),
            m22=1-2*(qz*qz+qx*qx),m23=2*(qy*qz+qx*qw),m32=2*(qy*qz-qx*qw),m33=1-2*(qy*qy+qx*qx);
        double cosine=Math.Sqrt(m11*m11+m12*m12),y=Math.Atan2(-m13,cosine),x,z;
        if(cosine>1e-10){x=Math.Atan2(m23,m33);z=Math.Atan2(m12,m11);}else{x=Math.Atan2(-m32,m22);z=0;}
        var degrees=new Vector3((float)(x*180/Math.PI),(float)(y*180/Math.PI),(float)(z*180/Math.PI));
        Vector3 Align(Vector3 d)=>new(d.X+360*MathF.Round((near.X-d.X)/360),d.Y+360*MathF.Round((near.Y-d.Y)/360),d.Z+360*MathF.Round((near.Z-d.Z)/360));
        var a=Align(degrees);var b=Align(new(degrees.X+180,180-degrees.Y,degrees.Z+180));return Vector3.DistanceSquared(a,near)<=Vector3.DistanceSquared(b,near)?a:b;
    }
}
