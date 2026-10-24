using System.Numerics;

namespace FighterPort.Core;

// System.Numerics uses row vectors: local=S*Rx*Ry*Rz*T, world=local*parent.
// C maps source model directions to target model directions (it may reflect).
// R_target = R_target_rest * C^-1 * R_source_rest^-1 * R_source_pose * C.
// Conjugation, unlike converting C to a quaternion, preserves proper rotations.
public sealed class BaselineMotionTransfer
{
    public const string Version="actor-frame-rest-relative-transfer-5";
    readonly RetargetRequest request;readonly RigDescriptor source,target;
    readonly Matrix4x4 conversion,inverse;readonly Dictionary<int,RetargetBonePair> byTarget;
    readonly record struct BodyFrame(int Source,int Target,Matrix4x4 SourceRestInverse,Matrix4x4 TargetRestInverse);
    readonly Dictionary<int,BodyFrame> bodyFrames=[];
    public RetargetMappingReport Mapping {get;}
    public float FileUnitRatio {get;}
    public float RootAnatomicalRatio {get;}
    public float PelvisAnatomicalRatio {get;}
    public float PelvisHeightAnatomicalRatio {get;}
    public float LateralAnatomicalRatio {get;}
    public float RootNativeRatio=>FileUnitRatio*RootAnatomicalRatio;
    public Vector3 ConvertDirection(Vector3 direction)=>Vector3.TransformNormal(direction,conversion);
    public Quaternion ConvertBoneOrientation(int sourceBone,int targetBone,Quaternion sourceWorldOrientation)
        =>Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(target.RestRotations[targetBone]*inverse*Matrix4x4.Transpose(source.RestRotations[sourceBone])*Matrix4x4.CreateFromQuaternion(sourceWorldOrientation)*conversion));
    public bool ExactRig {get;}
    public BaselineMotionTransfer(RetargetRequest request,RigDescriptor source,RigDescriptor target)
    {
        this.request=request;this.source=source;this.target=target;
        foreach(var rig in new[]{source,target})
            if(rig.Binding.Roles.TryGetValue("Hips",out int body)&&
               !PoseGeometry.Ancestor(rig.Scene,rig.Binding.Roles["RootMotion"],body))
                throw new NotSupportedException("The reviewed actor binding must be an ancestor of the body (or the same native bone); unrelated actor/body bindings cannot be composed.");
        conversion=Matrix4x4.Transpose(source.Basis)*target.Basis;inverse=Matrix4x4.Transpose(conversion);
        Mapping=RetargetBoneMapping.Build(source.Scene,request.SourceRig,target.Scene,request.TargetRig,source.Binding.Roles,target.Binding.Roles,source.Basis,target.Basis);
        byTarget=Mapping.Bones.ToDictionary(p=>p.Target);FileUnitRatio=target.UnitsPerMeter/source.UnitsPerMeter;
        foreach(var body in Mapping.Bones.Where(p=>p.Role=="Hips"))
        {
            // A native actor can have several translation/rotation helpers.
            // Strip the deepest corresponding pre-body frame, not just one
            // named root channel. All candidates are actual mapped ancestors.
            var ancestors=Mapping.Bones.Where(p=>p.Source!=body.Source&&p.Target!=body.Target&&
                PoseGeometry.Ancestor(source.Scene,p.Source,body.Source)&&PoseGeometry.Ancestor(target.Scene,p.Target,body.Target)).ToArray();
            var deepest=ancestors.Where(p=>!ancestors.Any(q=>q!=p&&PoseGeometry.Ancestor(source.Scene,p.Source,q.Source)&&PoseGeometry.Ancestor(target.Scene,p.Target,q.Target))).ToArray();
            if(deepest.Length!=1)throw new NotSupportedException("Body transport requires a consistent corresponding actor-ancestor order; review the supplied native bindings.");
            var frame=deepest[0];
            if(!Matrix4x4.Invert(source.Rest.World[frame.Source],out var sourceInverse)||!Matrix4x4.Invert(target.Rest.World[frame.Target],out var targetInverse))
                throw new NotSupportedException("A singular rest actor frame prevents body transport; native transforms were not normalized.");
            bodyFrames.Add(body.Target,new(frame.Source,frame.Target,sourceInverse,targetInverse));
        }
        var bySource=Mapping.Bones.ToDictionary(p=>p.Source,p=>p.Target);
        // Mathematical identity of the supplied native transform graph, not a
        // character name/hash shortcut. It remains valid under an isomorphic
        // bone renaming/storage permutation with complete supplied bindings.
        ExactRig=conversion==Matrix4x4.Identity&&FileUnitRatio==1&&source.Scene.Bones.Length==target.Scene.Bones.Length&&Mapping.Bones.Length==source.Scene.Bones.Length&&Mapping.Bones.All(p=>
        {
            var a=source.Scene.Bones[p.Source];var b=target.Scene.Bones[p.Target];return a.Rest==b.Rest&&(a.Parent<0?b.Parent<0:bySource.TryGetValue(a.Parent,out int parent)&&parent==b.Parent);
        });
        float legs(RigDescriptor d){var lengths=d.Binding.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).Select(c=>d.ChainLength(c.Role)/d.UnitsPerMeter).Where(x=>x>0).ToArray();return lengths.Length>0?lengths.Average():d.CharacteristicLength/d.UnitsPerMeter;}
        float torso(RigDescriptor d)=>d.Binding.Roles.TryGetValue("Hips",out int h)&&d.Binding.Roles.TryGetValue("Chest",out int c)&&Vector3.Distance(d.Rest.World[h].Translation,d.Rest.World[c].Translation)>1e-6f?Vector3.Distance(d.Rest.World[h].Translation,d.Rest.World[c].Translation)/d.UnitsPerMeter:d.CharacteristicLength/d.UnitsPerMeter;
        RootAnatomicalRatio=legs(target)/legs(source);PelvisAnatomicalRatio=torso(target)/torso(source);
        float? width(RigDescriptor rig)
        {
            var left=rig.Binding.Chains.SingleOrDefault(c=>c.Role=="Hand.L");var right=rig.Binding.Chains.SingleOrDefault(c=>c.Role=="Hand.R");
            if(left is null||right is null)return null;float distance=Vector3.Distance(rig.Rest.World[left.Bones[0]].Translation,rig.Rest.World[right.Bones[0]].Translation)/rig.UnitsPerMeter;
            return distance>1e-6f?distance:null;
        }
        LateralAnatomicalRatio=width(source) is {} sw&&width(target) is {} tw?tw/sw:RootAnatomicalRatio;
        float? supportHeight(RigDescriptor descriptor,ControlRigSetup controls)
        {
            if(!descriptor.Binding.Roles.TryGetValue("Hips",out int hips)||!controls.Chains.Any(c=>c.Role=="Foot.L")||!controls.Chains.Any(c=>c.Role=="Foot.R"))return null;
            var up=new Vector3(descriptor.Basis.M21,descriptor.Basis.M22,descriptor.Basis.M23);
            var soles=SurfaceContacts.Regions(descriptor.Scene,controls).Where(r=>r.Role.StartsWith("Foot.",StringComparison.Ordinal)).SelectMany(r=>r.Points).ToArray();
            if(soles.Length==0)return null;
            float floor=soles.Min(p=>Vector3.Dot(ContactGeometry.Point(p,descriptor.Rest),up));
            float height=(Vector3.Dot(descriptor.Rest.World[hips].Translation,up)-floor)/descriptor.UnitsPerMeter;
            return height>1e-6f?height:null;
        }
        // Vertical body displacement is measured against rest support height.
        // A torso-length ratio can put short-legged targets below a fixed floor
        // when the source lowers its pelvis. This is independent of clip names.
        var sourceHeight=supportHeight(source,request.SourceRig);var targetHeight=supportHeight(target,request.TargetRig);
        PelvisHeightAnatomicalRatio=sourceHeight is {} sh&&targetHeight is {} th?th/sh:PelvisAnatomicalRatio;
    }
    public Transform[] Evaluate(double sourceFrame,Vector3[]? windingReference=null)
        =>EvaluatePose(AnimationService.Evaluate(source.Scene,request.Clip,sourceFrame),windingReference);
    public Transform[] EvaluatePose(EvaluatedPose pose,Vector3[]? windingReference=null)
    {
        request.Cancellation.ThrowIfCancellationRequested();
        if(pose.Local.Length!=source.Scene.Bones.Length)throw new ArgumentException("Prepared pose belongs to another source skeleton.");
        var sourceRotations=new Matrix4x4[source.Scene.Bones.Length];
        for(int i=0;i<sourceRotations.Length;i++)
        {
            var value=pose.Local[i];if(value.Scale.X<=0||value.Scale.Y<=0||value.Scale.Z<=0||!PoseGeometry.Finite(value.Scale))throw new NotSupportedException($"Animated reflected/singular scale on source bone {i} '{source.Scene.Bones[i].Name}' at source seconds {pose.Seconds:G12} needs an explicit affine policy.");
            int parent=source.Scene.Bones[i].Parent;sourceRotations[i]=(Transform.Identity with{Rotation=value.Rotation}).Matrix*(parent<0?Matrix4x4.Identity:sourceRotations[parent]);
        }
        int sourceRoot=source.Binding.Roles["RootMotion"];
        var actor=pose.World[sourceRoot].Translation-source.Rest.World[sourceRoot].Translation;
        var up=new Vector3(source.Basis.M21,source.Basis.M22,source.Basis.M23);
        var removed=request.RootPolicy switch{RootMotionPolicy.InPlace=>actor-up*Vector3.Dot(actor,up),RootMotionPolicy.ActorControlled=>actor,_=>Vector3.Zero};
        var result=new Transform[target.Scene.Bones.Length];var world=new Matrix4x4[result.Length];var rotations=new Matrix4x4[result.Length];
        for(int t=0;t<result.Length;t++)
        {
            int tp=target.Scene.Bones[t].Parent;var rest=target.Scene.Bones[t].Rest;var value=rest;
            if(byTarget.TryGetValue(t,out var pair))
            {
                int s=pair.Source,sp=source.Scene.Bones[s].Parent;var sr=source.Scene.Bones[s].Rest;
                var desired=target.RestRotations[t]*inverse*Matrix4x4.Transpose(source.RestRotations[s])*sourceRotations[s]*conversion;
                var local=desired*(tp<0?Matrix4x4.Identity:Matrix4x4.Transpose(rotations[tp]));
                var euler=ExactRig?pose.Local[s].Rotation:AnimationControls.Euler(local,windingReference?[t]??rest.Rotation);
                // Native parent scales/rotations already affect bind geometry.
                // Local offset lengths alone give the wrong ratio under unequal
                // nonuniform parents, even with identical local translations.
                float sourceLength=sp<0?sr.Translation.Length():Vector3.Distance(source.Rest.World[s].Translation,source.Rest.World[sp].Translation),targetLength=tp<0?rest.Translation.Length():Vector3.Distance(target.Rest.World[t].Translation,target.Rest.World[tp].Translation);
                float translationRatio=sourceLength>1e-6f&&targetLength>1e-6f?targetLength/sourceLength:RootNativeRatio;
                var parentRest=sp<0?Matrix4x4.Identity:source.Rest.World[sp];var targetParent=tp<0?Matrix4x4.Identity:target.Rest.World[tp];
                if(!Matrix4x4.Invert(targetParent,out var inverseParent))throw new InvalidDataException("Target native parent bind is singular.");
                var offset=Vector3.TransformNormal(Vector3.TransformNormal(pose.Local[s].Translation-sr.Translation,parentRest),conversion)*translationRatio;
                value=rest with{Rotation=euler,Translation=rest.Translation+Vector3.TransformNormal(offset,inverseParent),Scale=rest.Scale*pose.Local[s].Scale/sr.Scale};
                if(pair.Role is "RootMotion" or "Hips")
                {
                    Vector3 desiredPosition;
                    if(pair.Role=="RootMotion")
                        desiredPosition=target.Rest.World[t].Translation+Vector3.TransformNormal(actor-removed,conversion)*RootNativeRatio;
                    else
                    {
                        // Row-vector derivation: b_a(t)=b_world(t)*A_source(t)^-1.
                        // Subtract b_a(rest), map that residual through the rest
                        // actor frames, then compose the ACTUAL target actor.
                        // In particular, turning an actor rotates its target's
                        // own rest body offset; it is not anatomical body motion.
                        // Scaling a turned world displacement about fixed up
                        // incorrectly changed that radius for unequal bodies.
                        var frame=bodyFrames[t];
                        if(!Matrix4x4.Invert(pose.World[frame.Source],out var sourceActorInverse))
                            throw new NotSupportedException("A singular animated actor frame prevents body transport; native transforms were not normalized.");
                        var sourceBody=Vector3.Transform(pose.World[s].Translation,sourceActorInverse);
                        var sourceRestBody=Vector3.Transform(source.Rest.World[s].Translation,frame.SourceRestInverse);
                        var restDisplacement=Vector3.TransformNormal(sourceBody-sourceRestBody,source.Rest.World[frame.Source]);
                        var vertical=up*Vector3.Dot(restDisplacement,up);
                        var horizontal=restDisplacement-vertical;
                        var anatomical=horizontal*PelvisAnatomicalRatio+vertical*PelvisHeightAnatomicalRatio;
                        var targetDisplacement=Vector3.TransformNormal(Vector3.TransformNormal(anatomical,conversion)*FileUnitRatio,frame.TargetRestInverse);
                        var targetRestBody=Vector3.Transform(target.Rest.World[t].Translation,frame.TargetRestInverse);
                        desiredPosition=Vector3.Transform(targetRestBody+targetDisplacement,world[frame.Target]);
                    }
                    if(tp>=0){if(!Matrix4x4.Invert(world[tp],out var inv))throw new InvalidDataException("Animated target parent is singular.");desiredPosition=Vector3.Transform(desiredPosition,inv);}
                    value=value with{Translation=desiredPosition};
                }
                // Exact graph reuse retains the authored local channels bit for
                // bit, including native Euler turns and nonuniform scale. This
                // is structural identity, not a model/profile exception.
                if(ExactRig&&request.RootPolicy==RootMotionPolicy.Preserve)value=pose.Local[s];
            }
            result[t]=value;rotations[t]=(Transform.Identity with{Rotation=value.Rotation}).Matrix*(tp<0?Matrix4x4.Identity:rotations[tp]);world[t]=value.Matrix*(tp<0?Matrix4x4.Identity:world[tp]);
        }
        return result;
    }
    public BoneTrack[] IdentityTracks()
    {
        var names=Mapping.Bones.ToDictionary(p=>source.Scene.Bones[p.Source].Name,p=>target.Scene.Bones[p.Target].Name,StringComparer.Ordinal);
        return request.Clip.Tracks.Select(t=>t with{Bone=names[t.Bone],Channels=t.Channels.Select(c=>c is null?null:c with{Keys=c.Keys.ToArray()}).ToArray(),Rotation=t.Rotation is {} q?q with{Keys=q.Keys.ToArray()}:null}).ToArray();
    }
}
