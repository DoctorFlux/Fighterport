using System.Numerics;

namespace FighterPort.Core;

public record NativeChainReachResult(Transform[] Local,string Status,float PositionError,float ChainLength,
    float MaximumCorrectionDegrees,float DistalOrientationError,int Iterations,bool HasBendEvidence,string? Reason=null)
{
    public float InitialShapeHintError {get;init;}
    public float MaximumShapeHintError {get;init;}
}

// Offline kinematics on a complete native path. Position projection never
// writes translations, scale, binds, weights or a replacement deform graph.
// Constraints/contacts and action acceptance are separate from this solve.
public static class NativeChainReach
{
    public const string Version="native-path-fabrik-2";
    public const int MaximumIterations=32;
    public static NativeChainReachResult Solve(RigDescriptor rig,Transform[] input,int[] path,Vector3 goal,
        Vector3? bendDirection,NativeJointLimit[]? limits=null,CancellationToken cancellation=default,Vector3[]? shapeHints=null)
    {
        cancellation.ThrowIfCancellationRequested();
        if(input.Length!=rig.Scene.Bones.Length)throw new ArgumentException("Reach pose belongs to another native graph.");
        return SolvePrepared(rig,input,path,goal,bendDirection,
            AnimationService.Evaluate(rig.Scene,null,0,input.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t)),limits,cancellation,shapeHints);
    }
    internal static NativeChainReachResult SolvePrepared(RigDescriptor rig,Transform[] input,int[] path,Vector3 goal,
        Vector3? bendDirection,EvaluatedPose pose,NativeJointLimit[]? limits=null,CancellationToken cancellation=default,Vector3[]? shapeHints=null)
    {
        cancellation.ThrowIfCancellationRequested();
        var scene=rig.Scene;
        if(input.Length!=scene.Bones.Length||path.Length<2||path.Distinct().Count()!=path.Length||!PoseGeometry.Finite(goal))
            throw new ArgumentException("Reach needs a finite goal, one transform per native bone and a distinct native chain.");
        for(int j=0;j<path.Length;j++)
            if(path[j]<0||path[j]>=input.Length||j>0&&scene.Bones[path[j]].Parent!=path[j-1])
                throw new ArgumentException("Reach requires the complete ordered native ancestor path.");
        if(shapeHints is not null&&(shapeHints.Length!=path.Length||shapeHints.Any(p=>!PoseGeometry.Finite(p))))
            throw new ArgumentException("Source shape hints need one finite point per native path joint.");
        var local=input.ToArray();
        var points=path.Select(b=>pose.World[b].Translation).ToArray();
        var lengths=points.Zip(points.Skip(1),(a,b)=>Vector3.Distance(a,b)).ToArray();
        float length=lengths.Sum(),error=Vector3.Distance(points[^1],goal);
        float shapeError=shapeHints is null?0:Enumerable.Range(1,path.Length-2).Select(i=>Vector3.Distance(points[i],shapeHints[i])).DefaultIfEmpty(0).Max();
        bool evidence=bendDirection is {} bend&&PoseGeometry.Finite(bend)&&bend.LengthSquared()>1e-12f;
        NativeChainReachResult Unchanged(string status,string? reason=null)=>new(input.ToArray(),status,error,length,0,0,0,evidence,reason){InitialShapeHintError=shapeError,MaximumShapeHintError=shapeError};
        bool OutsideLimits(Transform[] values)=>(limits??[]).Any(limit=>path.Contains(limit.Bone)&&Enumerable.Range(0,3).Any(a=>values[limit.Bone].Rotation[a]<limit.MinimumDegrees[a]-1e-5f||values[limit.Bone].Rotation[a]>limit.MaximumDegrees[a]+1e-5f));
        if(!float.IsFinite(length)||length<1e-7f)return Unchanged("Unsupported","The native path has no finite positive reach.");
        float tolerance=length*1e-6f;
        if(error<=tolerance&&shapeError<=length*.0001f)
            return OutsideLimits(input)?Unchanged("LimitConflict","The already matched native pose exceeds supplied reviewed joint limits; positional agreement is not a limit pass."):Unchanged("AlreadyMatched");
        // Rigid position projection is only claimed under positive uniform
        // ancestor scales. Affine/sheared kinematics needs another proof.
        for(int b=path[^1];b>=0;b=scene.Bones[b].Parent)
        {
            var scale=input[b].Scale;
            // Uniformity is relative to authored scale, not an absolute
            // model-unit epsilon: tiny values can still contain a reflection
            // or large anisotropy while preserving ordinary world reach.
            float scaleMagnitude=Math.Max(scale.X,Math.Max(scale.Y,scale.Z));
            if(!PoseGeometry.Finite(scale)||scale.X<=0||scale.Y<=0||scale.Z<=0||
                scaleMagnitude-Math.Min(scale.X,Math.Min(scale.Y,scale.Z))>1e-6f*scaleMagnitude)
                return Unchanged("Unsupported","Nonuniform/nonpositive native ancestor scale needs a verified affine reach policy; no scale was changed.");
        }
        var chainRotations=path.Select(b=>PoseGeometry.RotationMatrix(pose.World[b])).ToArray();
        int rootParent=scene.Bones[path[0]].Parent;
        var rootParentRotation=rootParent<0?Matrix4x4.Identity:PoseGeometry.RotationMatrix(pose.World[rootParent]);
        var root=points[0];float distance=Vector3.Distance(root,goal),minimum=Math.Max(0,2*lengths.Max()-length);
        if(distance>length+tolerance||distance<minimum-tolerance)
            return Unchanged("Unreachable",$"Requested native reach {distance:G9} is outside [{minimum:G9}, {length:G9}]; the chain was not stretched or the goal moved.");
        Vector3 Unit(Vector3 v,Vector3 fallback)=>v.LengthSquared()>1e-16f?Vector3.Normalize(v):fallback;
        var axis=Unit(goal-root,Unit(points[^1]-root,new(rig.Basis.M11,rig.Basis.M12,rig.Basis.M13)));
        var preferred=evidence?bendDirection!.Value:Vector3.Zero;
        preferred-=axis*Vector3.Dot(preferred,axis);
        if(preferred.LengthSquared()<1e-12f)
        {
            // A straight source has no observed bend side. A deterministic
            // reviewed coordinate frame can seed a draft, never establish
            // anatomical correctness or an accepted pole by itself.
            var axes=new[]{new Vector3(rig.Basis.M11,rig.Basis.M12,rig.Basis.M13),new Vector3(rig.Basis.M21,rig.Basis.M22,rig.Basis.M23),new Vector3(rig.Basis.M31,rig.Basis.M32,rig.Basis.M33)};
            var seed=axes.MinBy(v=>Math.Abs(Vector3.Dot(v,axis)));
            preferred=seed-axis*Vector3.Dot(seed,axis);evidence=false;
        }
        preferred=Vector3.Normalize(preferred);
        Vector3 Bend(Vector3[] p,Vector3 direction)
        {
            var result=Vector3.Zero;
            for(int j=1;j<p.Length-1;j++)
            {
                var offset=p[j]-p[0];float mass=(lengths[j-1]+lengths[j])*.5f;
                result+=(offset-direction*Vector3.Dot(offset,direction))*mass;
            }
            return result;
        }
        var desired=points.ToArray();
        if(shapeHints is not null)for(int j=1;j<desired.Length-1;j++)desired[j]=shapeHints[j];
        // Bend is a length-weighted offset (units squared). Its squared
        // degeneracy test must scale as length^4, not length^2; otherwise a
        // change of file units can select a different articulated branch.
        float bendResolution=length*length*length*length;
        if(!float.IsFinite(bendResolution))return Unchanged("Unsupported","Native units exceed the verified finite bend-resolution range.");
        if(shapeHints is not null)
        {
            // Use the same arc-mass measure on the full source shape and its
            // projection. Different segmentation must not rotate an already
            // feasible nonplanar hint merely because two pole summaries used
            // different weights.
            var hintBend=Bend(desired,axis);
            if(hintBend.LengthSquared()>bendResolution*1e-10f){preferred=Vector3.Normalize(hintBend);evidence=true;}
        }
        // A complete native path can contain only two nonzero geometric
        // segments, with any number of coincident native helpers. Its chord
        // has an exact circle solution. Seed that geometry directly instead
        // of spending iterations near a folded or fully extended singularity.
        // Topology, local channels and helpers are still reconstructed below;
        // this is not a requirement for three adjacent humanoid bones.
        var active=Enumerable.Range(0,lengths.Length).Where(i=>lengths[i]>0).ToArray();
        if(active.Length==2)
        {
            double first=lengths[active[0]],second=lengths[active[1]];
            double chord=distance;
            double along=chord>0?(first*first-second*second+chord*chord)/(2*chord):0;
            double radialSquared=first*first-along*along;
            // Reachability was checked above. A roundoff-sized negative near
            // extension may be clamped, but no goal is moved and no length is
            // changed; reconstructed geometry still receives the same bounds.
            double radius=Math.Sqrt(Math.Max(0,radialSquared));
            var middle=root+axis*(float)along+preferred*(float)radius;
            for(int j=0;j<desired.Length;j++)
                desired[j]=j<=active[0]?root:j<=active[1]?middle:goal;
        }
        if(desired.Length>2&&Bend(desired,axis).LengthSquared()<bendResolution*1e-10f&&distance<length-tolerance)
        {
            float arc=0,amplitude=.5f*MathF.Sqrt(Math.Max(0,length*length-distance*distance));
            for(int j=1;j<desired.Length-1;j++)
            {
                arc+=lengths[j-1];float u=arc/length;
                desired[j]=root+(goal-root)*u+preferred*(amplitude*MathF.Sin(MathF.PI*u));
            }
        }
        int iterations=0;
        for(;iterations<MaximumIterations;iterations++)
        {
            cancellation.ThrowIfCancellationRequested();desired[^1]=goal;
            for(int j=desired.Length-2;j>=0;j--)
                desired[j]=desired[j+1]+Unit(desired[j]-desired[j+1],-axis)*lengths[j];
            desired[0]=root;
            for(int j=0;j<desired.Length-1;j++)
                desired[j+1]=desired[j]+Unit(desired[j+1]-desired[j],axis)*lengths[j];
            var line=Unit(desired[^1]-root,axis);var actual=Bend(desired,line);
            var wanted=preferred-line*Vector3.Dot(preferred,line);
            if(actual.LengthSquared()>bendResolution*1e-14f&&wanted.LengthSquared()>1e-14f)
            {
                actual=Vector3.Normalize(actual);wanted=Vector3.Normalize(wanted);
                float turn=MathF.Atan2(Vector3.Dot(line,Vector3.Cross(actual,wanted)),Math.Clamp(Vector3.Dot(actual,wanted),-1,1));
                var rotation=Matrix4x4.CreateFromAxisAngle(line,turn);
                for(int j=1;j<desired.Length-1;j++)desired[j]=root+Vector3.TransformNormal(desired[j]-root,rotation);
            }
            if(Vector3.Distance(desired[^1],goal)<=tolerance){iterations++;break;}
        }
        // Reconstruct rotations along the existing path. The from/to swing
        // retains axial twist; the distal world orientation is then restored
        // so fingers/sockets retain their incoming motion, not a second copy.
        var originalEnd=chainRotations[^1];
        for(int j=0;j<path.Length-1;j++)
        {
            cancellation.ThrowIfCancellationRequested();
            if(lengths[j]<=1e-7f)continue;
            var from=Unit(points[j+1]-points[j],axis);var to=Unit(desired[j+1]-desired[j],axis);
            var swing=FromTo(from,to,preferred);
            int bone=path[j],parent=scene.Bones[bone].Parent;
            var world=chainRotations[j]*swing;
            var parentRotation=j>0?chainRotations[j-1]:rootParentRotation;
            local[bone]=input[bone] with{Rotation=AnimationControls.Euler(world*Matrix4x4.Transpose(parentRotation),input[bone].Rotation)};
            var pivot=points[j];
            for(int k=j;k<path.Length;k++)
            {
                points[k]=pivot+Vector3.TransformNormal(points[k]-pivot,swing);
                chainRotations[k]*=swing;
            }
        }
        int end=path[^1];
        local[end]=input[end] with{Rotation=AnimationControls.Euler(originalEnd*Matrix4x4.Transpose(chainRotations[^2]),input[end].Rotation)};
        if(OutsideLimits(local))return Unchanged("LimitConflict","The proposed local rotation exceeds supplied reviewed native limits; the limit was not relaxed.");
        var actualWorld=rootParent<0?Matrix4x4.Identity:pose.World[rootParent];
        float finalShapeError=0;
        for(int j=0;j<path.Length;j++)
        {
            actualWorld=local[path[j]].Matrix*actualWorld;
            if(shapeHints is not null&&j>0&&j<path.Length-1)finalShapeError=Math.Max(finalShapeError,Vector3.Distance(actualWorld.Translation,shapeHints[j]));
        }
        float maximum=path.Select(b=>Angle((Transform.Identity with{Rotation=input[b].Rotation}).Matrix,(Transform.Identity with{Rotation=local[b].Rotation}).Matrix)).Max();
        error=Vector3.Distance(actualWorld.Translation,goal);
        float distal=Angle(originalEnd,PoseGeometry.RotationMatrix(actualWorld));
        return new(local,error<=length*.0001f?"Solved":"NonConverged",error,length,maximum,distal,iterations,evidence,
            evidence?null:"No nondegenerate source/rest shape evidence; a reviewed coordinate axis supplied only a deterministic draft seed.")
        {InitialShapeHintError=shapeError,MaximumShapeHintError=finalShapeError};
    }
    static Matrix4x4 FromTo(Vector3 from,Vector3 to,Vector3 bend)
    {
        float dot=Math.Clamp(Vector3.Dot(from,to),-1,1);
        var cross=Vector3.Cross(from,to);float sine=cross.Length();
        if(sine<1e-10f&&dot>0)return Matrix4x4.Identity;
        if(sine<1e-7f&&dot<0)
        {
            var axis=Vector3.Cross(from,bend);
            if(axis.LengthSquared()<1e-12f)throw new NotSupportedException("Antiparallel reach has no resolved geometric bend axis.");
            return Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(axis),MathF.PI);
        }
        return Matrix4x4.CreateFromAxisAngle(cross/sine,MathF.Atan2(sine,dot));
    }
    static float Angle(Matrix4x4 a,Matrix4x4 b)
    {
        var delta=Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(b*Matrix4x4.Transpose(a)));
        return 2*MathF.Atan2(new Vector3(delta.X,delta.Y,delta.Z).Length(),Math.Abs(delta.W))*180/MathF.PI;
    }
}
