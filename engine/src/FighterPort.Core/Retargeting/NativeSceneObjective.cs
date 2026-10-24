using System.Numerics;

namespace FighterPort.Core;

public record NativeSceneEvaluation(double Cost,double[] Residual,double[,] Jacobian,Transform[] Local);

// Immutable source-time observation. The variables reference existing native
// channels; none of the terms creates a contact, changes rest anatomy or edits
// actor travel. The same object is used by the coupled offline trajectory fit.
public sealed class NativeSceneObjective
{
    public const string Version="native-relative-action-objective-5";
    public const double ActionOrientationPrior=.2,ActionShapePrior=.2,ReachPrior=.1;
    readonly RigDescriptor rig;
    readonly int body;
    readonly int[] rotations,points,ends;
    readonly NativeJointLimit[] limits;
    readonly Transform[] input;
    readonly EvaluatedPose initial;
    readonly EvaluatedPose actionReference;
    readonly Matrix4x4 parentInverse;
    readonly Func<EvaluatedPose,NativePoseDifferentials?,ArticulatedResiduals> observations;
    readonly CancellationToken cancellation;
    readonly bool action;
    readonly BodyRelativeReachGoal[] reachGoals;
    readonly BodySurfaceReachObservation[] surfaceGoals;
    public int Variables=>3+3*rotations.Length;
    public NativeSceneObjective(RigDescriptor rig,int body,int[] rotations,int[] points,int[] ends,NativeJointLimit[] limits,
        Transform[] input,Func<EvaluatedPose,NativePoseDifferentials?,ArticulatedResiduals> observations,CancellationToken cancellation,Transform[]? actionReference=null,BodyRelativeReachGoal[]? reachGoals=null,BodySurfaceReachObservation[]? surfaceGoals=null)
    {
        this.rig=rig;this.body=body;this.rotations=rotations.ToArray();this.points=points.ToArray();this.ends=ends.ToArray();this.limits=limits;
        this.input=input.ToArray();this.observations=observations;this.cancellation=cancellation;action=true;this.reachGoals=reachGoals??[];this.surfaceGoals=surfaceGoals??[];
        cancellation.ThrowIfCancellationRequested();initial=Pose(input);this.actionReference=actionReference is null?initial:Pose(actionReference);int parent=rig.Scene.Bones[body].Parent;
        if(body==rig.Binding.Roles["RootMotion"]||rotations.Any(b=>!PoseGeometry.Ancestor(rig.Scene,body,b)))
            throw new NotSupportedException("Native action variables must remain under the reviewed existing body; actor and body-parent rotations cannot be solver variables.");
        if(parent<0||!Matrix4x4.Invert(initial.World[parent],out parentInverse))
            throw new NotSupportedException("Native scene preparation needs an invertible existing body parent; no bind or actor was normalized.");
        if(input.Any(t=>!t.Channels().All(float.IsFinite)||t.Scale.X<=0||t.Scale.Y<=0||t.Scale.Z<=0))
            throw new NotSupportedException("Native trajectory preparation retains positive finite authored scale; reflected/singular transforms need an explicit representation policy.");
        if(!WithinLimits(input))throw new NotSupportedException("The incoming native pose is outside supplied reviewed limits; this does not prove incompatible anatomy.");
    }
    public Transform[] Apply(double[] x)
    {
        cancellation.ThrowIfCancellationRequested();
        if(x.Length!=Variables||x.Any(v=>!double.IsFinite(v)))throw new ArgumentException("Native correction parameters must be paired and finite.");
        var local=input.ToArray();float length=rig.CharacteristicLength;
        var move=Translation(x)*length;
        local[body]=local[body] with{Translation=local[body].Translation+Vector3.TransformNormal(move,parentInverse)};
        for(int i=0;i<rotations.Length;i++)local[rotations[i]]=local[rotations[i]] with
            {Rotation=local[rotations[i]].Rotation+new Vector3((float)x[3+3*i],(float)x[4+3*i],(float)x[5+3*i])*(180/MathF.PI)};
        return local;
    }
    public bool Admissible(double[] x)=>ParametersBounded(x)&&WithinLimits(Apply(x));
    public static bool ParametersBounded(double[] x)
    {
        if(x.Length<3||x.Any(v=>!double.IsFinite(v))||x.Take(3).Sum(v=>v*v)>4)return false;
        for(int i=3;i<x.Length;i+=3)if(x.Skip(i).Take(3).Sum(v=>v*v)>ArticulatedSceneFitting.MaximumOffsetRadians*ArticulatedSceneFitting.MaximumOffsetRadians)return false;
        return true;
    }
    public NativeSceneEvaluation Evaluate(double[] x,bool derivatives)
    {
        var local=Apply(x);var pose=Pose(local);int n=Variables;float length=rig.CharacteristicLength;
        var differential=derivatives?new NativePoseDifferentials(rig,pose,body,rotations,cancellation,rig.Basis):null;
        var observed=observations(pose,differential);var values=new List<double>(observed.Values);var rows=new List<double[]>();
        if(derivatives)for(int r=0;r<observed.Values.Length;r++)rows.Add(Enumerable.Range(0,n).Select(v=>observed.Jacobian[r,v]).ToArray());
        void Add(double value,Func<int,double> derivative){values.Add(value);if(derivatives)rows.Add(Enumerable.Range(0,n).Select(derivative).ToArray());}
        double rp=Math.Sqrt(ArticulatedSceneFitting.RotationPrior/Math.Max(1,rotations.Length)),tp=Math.Sqrt(ArticulatedSceneFitting.TranslationPrior);
        for(int i=0;i<3;i++){int k=i;Add(x[i]*tp,v=>v==k?tp:0);}
        for(int i=3;i<n;i++){int k=i;Add(x[i]*rp,v=>v==k?rp:0);}
        var move=Translation(x)*length;
        double sp=Math.Sqrt(ArticulatedSceneFitting.ShapePrior/Math.Max(1,points.Length))/length;
        foreach(int b in points)
        {
            var difference=pose.World[b].Translation-initial.World[b].Translation-move;
            for(int a=0;a<3;a++){int axis=a;Add(difference[a]*sp,v=>(differential!.Joint(b,v)[axis]-(v<3?TranslationAxis(v)[axis]*length:0))*sp);}
        }
        // Terminal frames carry actual action orientation, independently of
        // how many interior native joints happen to parameterize a limb.
        double op=Math.Sqrt(ActionOrientationPrior/Math.Max(1,ends.Length*3));
        foreach(int b in ends)Orientation(b,-1,op);
        if(action)
        {
            // The one reviewed body frame has its own source-action mass.
            // Dividing it by all native limb/helper joints made this preference
            // weaker merely because a compatible rig had more segmentation.
            Orientation(body,rig.Binding.Roles["RootMotion"],Math.Sqrt(ActionOrientationPrior/3),actionReferencePose:this.actionReference);
            // Preserve every selected native frame relative to its closest
            // selected ancestor (or the unmodified actor). This includes body,
            // torso, shoulders and interior joints, not just terminal hands.
            var selected=rotations.ToHashSet();double weight=Math.Sqrt(ActionOrientationPrior/Math.Max(1,rotations.Length*3));
            foreach(int b in rotations)
            {
                int anchor=rig.Scene.Bones[b].Parent;
                while(anchor>=0&&!selected.Contains(anchor)&&anchor!=rig.Binding.Roles["RootMotion"])anchor=rig.Scene.Bones[anchor].Parent;
                Orientation(b,anchor,weight,actionReferencePose:this.actionReference);
            }
            double shapeWeight=Math.Sqrt(ActionShapePrior/Math.Max(1,points.Length));
            var bodyFrame=PoseGeometry.RotationMatrix(pose.World[body]);var inverse=Matrix4x4.Transpose(bodyFrame);
            var initialInverse=Matrix4x4.Transpose(PoseGeometry.RotationMatrix(this.actionReference.World[body]));
            var bodyDerivatives=derivatives?Enumerable.Range(0,n).Select(v=>Matrix4x4.Transpose(RotationDerivative(pose.World[body],differential!.World(body,v)))).ToArray():[];
            foreach(int b in points.Where(p=>p!=body))
            {
                float scale=Math.Max(Vector3.Distance(rig.Rest.World[b].Translation,rig.Rest.World[body].Translation),length*.01f);
                var displacement=pose.World[b].Translation-pose.World[body].Translation;
                var relative=Vector3.TransformNormal(displacement,inverse);
                var desired=Vector3.TransformNormal(this.actionReference.World[b].Translation-this.actionReference.World[body].Translation,initialInverse);
                double factor=shapeWeight/scale;
                for(int a=0;a<3;a++)
                {
                    int axis=a;Add((relative[a]-desired[a])*factor,v=>
                        (Vector3.TransformNormal(differential!.Joint(b,v)-differential.Joint(body,v),inverse)+
                         Vector3.TransformNormal(displacement,bodyDerivatives[v]))[axis]*factor);
                }
            }
        }
        foreach(var goal in reachGoals)
        {
            int anchor=goal.Binding.TargetAnchor;
            var inverse=Matrix4x4.Transpose(PoseGeometry.RotationMatrix(pose.World[anchor]));
            var initialInverse=Matrix4x4.Transpose(PoseGeometry.RotationMatrix(initial.World[anchor]));
            var inverseDerivatives=derivatives?Enumerable.Range(0,n).Select(v=>Matrix4x4.Transpose(RotationDerivative(pose.World[anchor],differential!.World(anchor,v)))).ToArray():[];
            var hints=goal.JointHints.Append(new(goal.Binding.TargetBones[^1],1,goal.Position)).GroupBy(h=>h.TargetBone).Select(g=>g.Last()).ToArray();
            double factor=Math.Sqrt(ReachPrior/Math.Max(1,reachGoals.Length*hints.Length))/Math.Max(goal.Binding.TargetRestLength,length*.01f);
            foreach(var hint in hints)
            {
                var displacement=pose.World[hint.TargetBone].Translation-pose.World[anchor].Translation;
                var actual=Vector3.TransformNormal(displacement,inverse);
                var desired=Vector3.TransformNormal(hint.Position-initial.World[anchor].Translation,initialInverse);
                for(int a=0;a<3;a++)
                {
                    int axis=a;Add((actual[a]-desired[a])*factor,v=>(
                        Vector3.TransformNormal(differential!.Joint(hint.TargetBone,v)-differential.Joint(anchor,v),inverse)+
                        Vector3.TransformNormal(displacement,inverseDerivatives[v]))[axis]*factor);
                }
            }
        }
        foreach(var goal in surfaceGoals)
        {
            cancellation.ThrowIfCancellationRequested();
            var offset=goal.Endpoint.Evaluate(pose)-goal.Anchor.Evaluate(pose);
            var inverse=Matrix4x4.Transpose(PoseGeometry.RotationMatrix(pose.World[goal.TargetAnchor]));
            var actual=Vector3.TransformNormal(offset,inverse);double factor=Math.Sqrt(goal.Weight)/goal.Length;
            var gradients=derivatives?Enumerable.Range(0,n).Select(v=>
                Vector3.TransformNormal(goal.Endpoint.Derivative(differential!,v)-goal.Anchor.Derivative(differential!,v),inverse)+
                Vector3.TransformNormal(offset,Matrix4x4.Transpose(RotationDerivative(pose.World[goal.TargetAnchor],differential!.World(goal.TargetAnchor,v))))).ToArray():[];
            for(int a=0;a<3;a++){int axis=a;Add((actual[a]-goal.DesiredLocal[a])*factor,v=>gradients[v][axis]*factor);}
        }
        var residual=values.ToArray();var jacobian=new double[derivatives?residual.Length:0,derivatives?n:0];
        if(derivatives)for(int r=0;r<residual.Length;r++)for(int v=0;v<n;v++)jacobian[r,v]=rows[r][v];
        return new(residual.Sum(v=>v*v),residual,jacobian,local);

        void Orientation(int bone,int anchor,double factor,EvaluatedPose? actionReferencePose=null)
        {
            var reference=actionReferencePose??initial;
            var before=PoseGeometry.RotationMatrix(reference.World[bone]);var current=PoseGeometry.RotationMatrix(pose.World[bone]);
            var inverse=anchor<0?Matrix4x4.Identity:Matrix4x4.Transpose(PoseGeometry.RotationMatrix(pose.World[anchor]));
            var desired=before*(anchor<0?Matrix4x4.Identity:Matrix4x4.Transpose(PoseGeometry.RotationMatrix(reference.World[anchor])));
            var relative=current*inverse;
            var gradients=derivatives?Enumerable.Range(0,n).Select(v=>RotationDerivative(pose.World[bone],differential!.World(bone,v))*inverse+
                (anchor<0?default(Matrix4x4):current*Matrix4x4.Transpose(RotationDerivative(pose.World[anchor],differential.World(anchor,v))))).ToArray():[];
            for(int a=0;a<3;a++)for(int c=0;c<3;c++)
            {
                int axis=a,component=c;var unit=a==0?Vector3.UnitX:a==1?Vector3.UnitY:Vector3.UnitZ;
                Add((Vector3.TransformNormal(unit,relative)-Vector3.TransformNormal(unit,desired))[c]*factor,
                    v=>Vector3.TransformNormal(axis==0?Vector3.UnitX:axis==1?Vector3.UnitY:Vector3.UnitZ,gradients[v])[component]*factor);
            }
        }
    }
    Vector3 Translation(double[] x)=>Vector3.TransformNormal(new((float)x[0],(float)x[1],(float)x[2]),rig.Basis);
    Vector3 TranslationAxis(int axis)=>axis==0?new(rig.Basis.M11,rig.Basis.M12,rig.Basis.M13):axis==1?new(rig.Basis.M21,rig.Basis.M22,rig.Basis.M23):new(rig.Basis.M31,rig.Basis.M32,rig.Basis.M33);
    public static Matrix4x4 RotationDerivative(Matrix4x4 world,Matrix4x4 derivative)
    {
        var x=new Vector3(world.M11,world.M12,world.M13);var y=new Vector3(world.M21,world.M22,world.M23);
        var dx=new Vector3(derivative.M11,derivative.M12,derivative.M13);var dy=new Vector3(derivative.M21,derivative.M22,derivative.M23);
        var a=Vector3.Normalize(x);var da=(dx-a*Vector3.Dot(a,dx))/x.Length();
        var orth=y-a*Vector3.Dot(a,y);var b=Vector3.Normalize(orth);
        var delta=dy-da*Vector3.Dot(a,y)-a*(Vector3.Dot(da,y)+Vector3.Dot(a,dy));
        var db=(delta-b*Vector3.Dot(b,delta))/orth.Length();
        var cross=Vector3.Cross(a,b);var z=Vector3.Normalize(cross);var dz=Vector3.Cross(da,b)+Vector3.Cross(a,db);
        dz=(dz-z*Vector3.Dot(z,dz))/cross.Length();
        return new(da.X,da.Y,da.Z,0,db.X,db.Y,db.Z,0,dz.X,dz.Y,dz.Z,0,0,0,0,0);
    }
    EvaluatedPose Pose(Transform[] local)=>AnimationService.Evaluate(rig.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
    bool WithinLimits(Transform[] pose)=>limits.All(l=>Enumerable.Range(0,3).All(a=>pose[l.Bone].Rotation[a]>=l.MinimumDegrees[a]&&pose[l.Bone].Rotation[a]<=l.MaximumDegrees[a]));
}
