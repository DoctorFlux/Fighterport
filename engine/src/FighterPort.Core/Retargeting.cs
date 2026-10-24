using System.Numerics;

namespace FighterPort.Core;

public record RigAxes(Vector3 Right,Vector3 Up,Vector3 Forward);
public record SourceAssetSnapshot(Guid Id,Scene Scene,AnimationClip Clip,ControlRigSetup Controls,SourceModelRevision[] Models)
{
    public NativeAnimationLayer[] Layers {get;init;}=[];
    public NativeResourceEntry[] Resources {get;init;}=[];
    public SourceBuildIdentity? Build {get;init;}
    public bool BuildVerified {get;init;}
    public string? Provider {get;init;}
    public string? AssociationEvidence {get;init;}
    public Guid? LibraryRegistrationId {get;init;}
    public ContactFrame[] Contacts {get;init;}=[];
    public SourceFileProof[] SourceFiles {get;init;}=[];
    public RetargetSourceAnalysis? Analysis {get;init;}
    public SourceMotionAnalysis? MotionAnalysis {get;init;}
}
public record RetargetBake(AnimationClip Clip,ContactFrame[] Contacts,SourceAssetSnapshot Source,float MaximumContactError,float MotionScale)
{
    public SourceAssetSnapshot[] SourceDependencies {get;init;}=[];
    public GroundContactReport? Grounding {get;init;}
}
public record Characterization(int Body,int MotionRoot,Dictionary<string,int> Roles,Matrix4x4 Basis,float LegLength);

public static class RigCharacterization
{
    public static Characterization Describe(Scene scene,ControlRigSetup setup)
    {
        if(setup.SkeletonFingerprint!=AnimationControls.Fingerprint(scene))throw new InvalidOperationException("Saved rig characterization belongs to a changed skeleton/rest revision.");
        int body=ContactSolver.BodyBone(scene,setup);if(body<0)throw new InvalidOperationException("Review the pelvis/body and limb mappings before proportion-aware transfer.");
        var roles=setup.BoneRoles.ToDictionary(r=>r.SemanticRole,r=>r.Bone);roles["Hips"]=body;
        foreach(var chain in setup.Chains)
        {
            string side=chain.Role[(chain.Role.LastIndexOf('.')+1)..];string[] names=chain.Role.StartsWith("Hand.",StringComparison.Ordinal)?["UpperArm.","Elbow.","Wrist."]:["UpperLeg.","Knee.","Ankle."];
            foreach(var (name,index) in names.Zip(new[]{chain.Root,chain.Middle,chain.Effector}))roles[name+side]=index;
        }
        var arms=setup.Chains.Where(c=>c.Role.StartsWith("Hand.",StringComparison.Ordinal)).ToArray();
        if(!roles.ContainsKey("Chest")&&arms.Length>=2)for(int i=scene.Bones[arms[0].Root].Parent;i>=0;i=scene.Bones[i].Parent)if(arms.All(c=>PoseGeometry.Ancestor(scene,i,c.Root))){roles["Chest"]=i;break;}
        int motion=roles.GetValueOrDefault("RootMotion",-1);if(motion<0){motion=body;while(scene.Bones[motion].Parent>=0)motion=scene.Bones[motion].Parent;}roles["RootMotion"]=motion;
        var bind=AnimationService.Evaluate(scene,null,0);var legs=setup.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).ToArray();
        if(!legs.Any(c=>c.Role=="Foot.L")||!legs.Any(c=>c.Role=="Foot.R"))throw new InvalidOperationException("This transfer requires characterized left/right legs; partial/non-humanoid transfer needs a reviewed profile.");
        var left=legs.Single(c=>c.Role=="Foot.L");var right=legs.Single(c=>c.Role=="Foot.R");
        var axis=setup.Axes;var up=axis?.Up??(roles.TryGetValue("Chest",out int chest)?bind.World[chest].Translation-bind.World[body].Translation:Vector3.UnitY);up=Vector3.Normalize(up);
        var r=axis?.Right??(bind.World[right.Root].Translation-bind.World[left.Root].Translation);r=Vector3.Normalize(r-up*Vector3.Dot(r,up));
        var f=axis?.Forward??legs[0].PoleDirection;f-=up*Vector3.Dot(f,up)+r*Vector3.Dot(f,r);if(f.LengthSquared()<.000001f)f=Vector3.Cross(r,up);f=Vector3.Normalize(f);
        if(!PoseGeometry.Finite(r)||!PoseGeometry.Finite(up)||!PoseGeometry.Finite(f))throw new InvalidOperationException("Body axes are ambiguous; review the rig coordinate profile.");
        var basis=new Matrix4x4(r.X,r.Y,r.Z,0,up.X,up.Y,up.Z,0,f.X,f.Y,f.Z,0,0,0,0,1);
        float length=legs.Average(c=>Vector3.Distance(bind.World[c.Root].Translation,bind.World[c.Middle].Translation)+Vector3.Distance(bind.World[c.Middle].Translation,bind.World[c.Effector].Translation));
        return new(body,motion,roles,basis,length);
    }
}

public static class RetargetService
{
    public const string Version=RetargetPipeline.Version;
    public static OperationResult<RetargetBake> Bake(FighterProject donor,AnimationClip source,ControlRigSetup sourceRig,FighterProject target,ControlRigSetup targetRig,
        RootMotionPolicy rootPolicy=RootMotionPolicy.Preserve,bool useContacts=true,CancellationToken cancellation=default,RationalRate? targetRate=null,GroundContactSettings? ground=null,FittingSettings? fitting=null,RetargetTiming? timing=null,RetargetSourceAnalysis? analysis=null,ContactIntervalEdit[]? contactEdits=null,RetargetSupportBinding[]? supportBindings=null)
        =>RetargetPipeline.Bake(new(donor,source,sourceRig,target,targetRig,rootPolicy,useContacts,cancellation,targetRate,ground,fitting,timing,analysis,contactEdits,supportBindings));
    public static FighterProject Apply(FighterProject target,RetargetBake bake)=>target with{Clips=target.Clips.Append(bake.Clip).ToArray(),Contacts=target.Contacts.Concat(bake.Contacts).ToArray(),LibrarySources=target.LibrarySources.Concat(bake.SourceDependencies).Append(bake.Source).DistinctBy(s=>s.Id).ToArray()};
}
