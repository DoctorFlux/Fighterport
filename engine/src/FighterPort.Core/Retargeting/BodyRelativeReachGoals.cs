using System.Numerics;

namespace FighterPort.Core;

// Bindings reference complete native paths; they do not construct deform bones.
public record BodyRelativeReachBinding(string Role,int[] SourceBones,int[] TargetBones,
    int SourceAnchor,int TargetAnchor,float SourceRestLength,float TargetRestLength,string Evidence)
{
    // Both lengths are already in their own native units. This ratio contains
    // the file-unit ratio once; callers must not multiply it by that ratio again.
    public float NativeLengthRatio=>TargetRestLength/SourceRestLength;
}
public record BodyRelativeReachJointHint(int TargetBone,float ArcFraction,Vector3 Position);
public record BodyRelativeReachGoal(BodyRelativeReachBinding Binding,Vector3 Position,Vector3 RootPosition,
    Vector3 BendDirection,bool HasBendEvidence,BodyRelativeReachJointHint[] JointHints,
    float SourceExtension,float RequestedDistance)
{
    public bool SourceBendObserved {get;init;}
    public string BendProvenance {get;init;}="Unresolved";
}

// Offline source-relative pose objectives only. No floor, contact detector,
// world pin, preferred action name, IK solve or state from a previous evaluation.
public sealed class BodyRelativeReachGoals
{
    public const string Version="parent-frame-rest-relative-reach-1";
    readonly RigDescriptor source,target;
    readonly Prepared[] prepared;
    sealed record Prepared(BodyRelativeReachBinding Binding,float[] SourceArc,float[] TargetArc,
        Matrix4x4 SourceRestInverse,Matrix4x4 TargetRestInverse,Matrix4x4 TargetRootRestInverse,Matrix4x4 RestConversion);
    public BodyRelativeReachBinding[] Bindings {get;}
    public Diagnostic[] Diagnostics {get;}

    public BodyRelativeReachGoals(RigDescriptor source,RigDescriptor target,RetargetMappingReport mapping)
    {
        this.source=source;this.target=target;
        var diagnostics=new List<Diagnostic>();var result=new List<Prepared>();
        var conversion=Matrix4x4.Transpose(source.Basis)*target.Basis;
        bool Trusted(RetargetBonePair p)=>p.Binding is {Supplied:true,Reviewed:true,RequiresReview:false};
        bool Corresponds(int a,int b)=>mapping.Bones.Any(p=>p.Source==a&&p.Target==b&&Trusted(p));
        foreach(var a in source.Binding.Chains.OrderBy(c=>c.Role,StringComparer.Ordinal))
        {
            if(source.Binding.Chains.Count(c=>c.Role==a.Role)!=1){Review(a.Role,"Source chain role is ambiguous.");continue;}
            var matches=target.Binding.Chains.Where(c=>c.Role==a.Role).ToArray();
            if(matches.Length!=1){Review(a.Role,"A unique corresponding target chain is not supplied.");continue;}
            var b=matches[0];
            if(!a.Reviewed||!b.Reviewed){Review(a.Role,"Both complete native chain paths require reviewed bindings.");continue;}
            if(!Corresponds(a.Bones[0],b.Bones[0])||!Corresponds(a.Bones[^1],b.Bones[^1]))
            {Review(a.Role,"Source/target chain roots and endpoints need supplied reviewed correspondence.");continue;}
            var ancestors=mapping.Bones.Where(p=>Trusted(p)&&p.Source!=a.Bones[0]&&p.Target!=b.Bones[0]&&
                PoseGeometry.Ancestor(source.Scene,p.Source,a.Bones[0])&&PoseGeometry.Ancestor(target.Scene,p.Target,b.Bones[0])).ToArray();
            var nearest=ancestors.Where(p=>!ancestors.Any(q=>q!=p&&
                PoseGeometry.Ancestor(source.Scene,p.Source,q.Source)&&PoseGeometry.Ancestor(target.Scene,p.Target,q.Target))).ToArray();
            if(nearest.Length!=1){Review(a.Role,"No unique consistent mapped ancestor above both chain roots is supplied.");continue;}
            var anchor=nearest[0];var sa=Arc(source.Rest,a.Bones);var ta=Arc(target.Rest,b.Bones);
            float sl=sa[^1],tl=ta[^1];
            if(!float.IsFinite(sl)||!float.IsFinite(tl)||sl<=0||tl<=0||!float.IsFinite(tl/sl))
            {Review(a.Role,"Rest chain geometry has no finite positive transferable length.");continue;}
            var binding=new BodyRelativeReachBinding(a.Role,a.Bones.ToArray(),b.Bones.ToArray(),anchor.Source,anchor.Target,sl,tl,
                "Reviewed native paths and endpoint correspondence; nearest consistent mapped ancestor; rest-arc soft shape hints");
            var si=Matrix4x4.Transpose(source.RestRotations[anchor.Source]);var ti=Matrix4x4.Transpose(target.RestRotations[anchor.Target]);
            RequireInvertible(Linear(source.Rest.World[a.Bones[0]]),"source rest chain root");
            var targetRootRelative=Linear(target.Rest.World[b.Bones[0]])*ti;
            result.Add(new(binding,sa.Select(v=>v/sl).ToArray(),ta.Select(v=>v/tl).ToArray(),si,ti,RequireInvertible(targetRootRelative,"target rest chain root"),
                source.RestRotations[anchor.Source]*conversion*ti));
        }
        prepared=result.ToArray();Bindings=prepared.Select(p=>p.Binding).ToArray();Diagnostics=diagnostics.ToArray();
        void Review(string role,string reason)=>diagnostics.Add(new("RIG_BINDING_REVIEW","Body-relative reach '"+role+"': "+reason+" Baseline native channels are retained; no chain was guessed.",false));
    }

    public BodyRelativeReachGoal[] Evaluate(EvaluatedPose sourcePose,EvaluatedPose targetPose)
    {
        if(sourcePose.Local.Length!=source.Scene.Bones.Length||sourcePose.World.Length!=source.Scene.Bones.Length||
           targetPose.Local.Length!=target.Scene.Bones.Length||targetPose.World.Length!=target.Scene.Bones.Length)
            throw new ArgumentException("Reach goals require poses belonging to the prepared source and target native graphs.");
        // Match BaselineMotionTransfer's rotation convention. Positions retain
        // native scale/translation; the direction frames contain rotations only.
        var sr=Rotations(source.Scene,sourcePose);var tr=Rotations(target.Scene,targetPose);
        return prepared.Select(p=>Goal(p,sourcePose,targetPose,sr,tr)).ToArray();
    }

    BodyRelativeReachGoal Goal(Prepared p,EvaluatedPose sp,EvaluatedPose tp,Matrix4x4[] sr,Matrix4x4[] tr)
    {
        var b=p.Binding;int sroot=b.SourceBones[0],troot=b.TargetBones[0];
        var sourceRoot=sp.World[sroot].Translation;var targetRoot=tp.World[troot].Translation;
        var sourceInverse=Matrix4x4.Transpose(sr[b.SourceAnchor]);var targetRotation=tr[b.TargetAnchor];
        RequireInvertible(Linear(sp.World[sroot]),"animated source chain root");
        var targetRootRelative=Linear(tp.World[troot])*Matrix4x4.Transpose(targetRotation);
        RequireInvertible(targetRootRelative,"animated target chain root");
        var residualMotion=p.TargetRootRestInverse*targetRootRelative;
        Vector3 Objective(Vector3 sourcePoint,Vector3 sourceRestPoint,Vector3 targetRestPoint)
        {
            var moved=Vector3.TransformNormal(sourcePoint-sourceRoot,sourceInverse);
            var rested=Vector3.TransformNormal(sourceRestPoint-source.Rest.World[sroot].Translation,p.SourceRestInverse);
            var targetRest=Vector3.TransformNormal(targetRestPoint-target.Rest.World[troot].Translation,p.TargetRestInverse);
            // All vectors below use target-anchor coordinates and row vectors.
            // g = r*u_pose*M + (v_rest-r*u_rest*M)*D_root, then R_anchor(t).
            // Q_root=L_root*R_anchor^-1 and D_root=Q_root(rest)^-1*Q_root(t),
            // where L_root is the actual native world linear transform. The
            // anchor is rotation-only; authored root/ancestor scale is retained
            // in the residual's motion rather than normalized or applied twice.
            // Rotating the rest-shape residual is essential: a rigid source
            // root turn must rotate the target's own bent rest geometry, rather
            // than making a feasible four-unit chain demand sqrt(26) units.
            var restMapped=Vector3.TransformNormal(rested,p.RestConversion)*b.NativeLengthRatio;
            var local=Vector3.TransformNormal(moved,p.RestConversion)*b.NativeLengthRatio+
                Vector3.TransformNormal(targetRest-restMapped,residualMotion);
            return targetRoot+Vector3.TransformNormal(local,targetRotation);
        }
        var endpoint=Objective(sp.World[b.SourceBones[^1]].Translation,source.Rest.World[b.SourceBones[^1]].Translation,target.Rest.World[b.TargetBones[^1]].Translation);
        var hints=Enumerable.Range(1,b.TargetBones.Length-2).Select(i=>
        {
            float fraction=p.TargetArc[i];
            return new BodyRelativeReachJointHint(b.TargetBones[i],fraction,Objective(
                At(sp,b.SourceBones,p.SourceArc,fraction),At(source.Rest,b.SourceBones,p.SourceArc,fraction),target.Rest.World[b.TargetBones[i]].Translation));
        }).ToArray();
        var span=endpoint-targetRoot;Vector3 bend=Vector3.Zero;
        if(span.LengthSquared()>0)
        {
            var axis=Vector3.Normalize(span);
            foreach(var hint in hints)
            {
                var d=hint.Position-targetRoot;var transverse=d-axis*Vector3.Dot(d,axis);
                bend+=transverse*(hint.ArcFraction*(1-hint.ArcFraction));
            }
        }
        // An aggregate can be ambiguous for a straight or cancelling/nonplanar
        // path. Individual shape hints remain available; no global-axis pole is invented.
        bool evidence=bend.Length()>b.TargetRestLength*1e-6f;
        if(!PoseGeometry.Finite(endpoint)||hints.Any(h=>!PoseGeometry.Finite(h.Position))||!PoseGeometry.Finite(bend))
            throw new NotSupportedException("The supplied native pose produces nonfinite body-relative reach geometry.");
        var sourceSpan=sp.World[b.SourceBones[^1]].Translation-sourceRoot;
        var sourceAxis=sourceSpan.LengthSquared()>1e-16f?Vector3.Normalize(sourceSpan):Vector3.Zero;
        bool sourceBent=b.SourceBones.Skip(1).Take(b.SourceBones.Length-2).Any(bone=>
        {
            var offset=sp.World[bone].Translation-sourceRoot;
            return (offset-sourceAxis*Vector3.Dot(offset,sourceAxis)).Length()>b.SourceRestLength*1e-6f;
        });
        return new(b,endpoint,targetRoot,evidence?Vector3.Normalize(bend):Vector3.Zero,evidence,hints,
            sourceSpan.Length()/b.SourceRestLength,span.Length())
        {SourceBendObserved=sourceBent,BendProvenance=!evidence?"No unique aggregate bend":sourceBent?"Source-pose geometry with reviewed target rest-shape residual":"Reviewed target rest-shape prior; source path has no observed bend"};
    }

    static float[] Arc(EvaluatedPose pose,int[] path)
    {
        var arc=new float[path.Length];for(int i=1;i<path.Length;i++)arc[i]=arc[i-1]+Vector3.Distance(pose.World[path[i-1]].Translation,pose.World[path[i]].Translation);return arc;
    }
    static Matrix4x4 Linear(Matrix4x4 world)=>new(world.M11,world.M12,world.M13,0,world.M21,world.M22,world.M23,0,
        world.M31,world.M32,world.M33,0,0,0,0,1);
    static Matrix4x4 RequireInvertible(Matrix4x4 linear,string context)
    {
        if(!Matrix4x4.Invert(linear,out var inverse)||
            !new[]{inverse.M11,inverse.M12,inverse.M13,inverse.M21,inverse.M22,inverse.M23,inverse.M31,inverse.M32,inverse.M33}.All(float.IsFinite))
            throw new NotSupportedException("Body-relative reach cannot interpret a singular or nonfinite "+context+" linear transform; no native scale or rest transform was normalized.");
        return inverse;
    }
    static Vector3 At(EvaluatedPose pose,int[] path,float[] arc,float fraction)
    {
        if(fraction<=0)return pose.World[path[0]].Translation;if(fraction>=1)return pose.World[path[^1]].Translation;
        for(int i=1;i<path.Length;i++)if(arc[i]>=fraction&&arc[i]>arc[i-1])
            return Vector3.Lerp(pose.World[path[i-1]].Translation,pose.World[path[i]].Translation,(fraction-arc[i-1])/(arc[i]-arc[i-1]));
        return pose.World[path[^1]].Translation;
    }
    static Matrix4x4[] Rotations(Scene scene,EvaluatedPose pose)
    {
        var rotations=new Matrix4x4[scene.Bones.Length];for(int i=0;i<rotations.Length;i++)
            rotations[i]=(Transform.Identity with{Rotation=pose.Local[i].Rotation}).Matrix*(scene.Bones[i].Parent<0?Matrix4x4.Identity:rotations[scene.Bones[i].Parent]);
        return rotations;
    }
}
