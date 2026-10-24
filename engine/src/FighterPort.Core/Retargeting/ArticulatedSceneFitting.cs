using System.Numerics;
using System.Text.Json.Serialization;

namespace FighterPort.Core;

public record ArticulatedResiduals(double[] Values,double[,] Jacobian);
public record ArticulatedSceneReceipt(string Version,int Evaluations,int Improved,int Unsupported,int Variables,int MaximumIterations,
    double MaximumInitialCost,double MaximumFinalCost,float MaximumCorrectionDegrees,string[] OpenGates)
{
    public int[] ModifiedRotationBones {get;init;}=[];
    public string[] UnsupportedReasons {get;init;}=[];
    public string CorrectionMeasure {get;init;}="Euclidean norm of native local Euler XYZ parameter offsets in degrees; not a measured quaternion or action angle";
    public double? RotationOffsetBoundDegrees {get;init;}
    public string? WorkBudgetVersion {get;init;}
    public long? ResidualWorkBudget {get;init;}
    public long? BoneWorkBudget {get;init;}
    public long? EstimatedResidualWork {get;init;}
    public long? EstimatedBoneWork {get;init;}
    public long? EstimatedPoseEvaluations {get;init;}
    public long? EstimatedSkinColumns {get;init;}
    public long? EstimatedAlgebraWork {get;init;}
    public long? EstimatedMatrixProducts {get;init;}
    public string? WorkUnits {get;init;}
    // Absent historical evidence stays unknown rather than manufacturing
    // convergence or zero stop counts for an older saved result.
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? TerminationEvidenceVersion {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public long? AcceptedSteps {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public long? RejectedTrials {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public long? NoAcceptedLowerCostStops {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public long? IterationLimitStops {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public long? LinearSolveStops {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? ConvergenceStatus {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? OptimizationScope {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public bool? ActualBodyTranslationDerived {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int[]? SelectedRotationBones {get;init;}
}

// A bounded offline soft objective over the EXISTING native joints. All scene
// observations are frozen from one authentic source evaluation. No contacts,
// named actions, actor edits, rest changes or history-dependent warm starts.
public sealed class ArticulatedSceneFitting
{
    public const string Version="coordinated-native-scene-6";
    public const string WorkBudgetVersion="analytic-differential-skin-columns-4";
    public const string TerminationEvidenceVersion="native-euler-lm-stops-2";
    public const int IterationLimit=8,BacktrackLimit=8,MaximumRotationBones=32;
    public const long MaximumResidualWork=32000000000,MaximumBoneWork=500000000;
    public const double RotationPrior=.002,ShapePrior=.02,OrientationPrior=.01,TranslationPrior=.01;
    public const double MaximumStepRadians=Math.PI/24,MaximumOffsetRadians=Math.PI/4;
    readonly RigDescriptor rig;
    readonly int body;
    readonly int[] bones,shape,ends;
    readonly NativeJointLimit[] limits;
    readonly CancellationToken cancellation;
    int evaluations,improved,unsupported,maximumIterations,independentFits;
    double maximumInitial,maximumFinal;
    float maximumCorrection;
    long? estimatedResidualWork,estimatedBoneWork,estimatedPoseEvaluations,estimatedSkinColumns,estimatedAlgebraWork,estimatedMatrixProducts;
    long acceptedSteps,rejectedTrials,noAcceptedLowerCostStops,iterationLimitStops,linearSolveStops;
    bool actualBodyTranslationDerived;
    readonly HashSet<int> modified=[];
    readonly HashSet<string> reasons=new(StringComparer.Ordinal);
    public bool Enabled=>bones.Length>0;
    public int Variables=>3+bones.Length*3;
    public ArticulatedSceneFitting(RigDescriptor rig,int body,RetargetMappingReport mapping,NativeJointLimit[] limits,CancellationToken cancellation)
    {
        this.rig=rig;this.body=body;this.limits=limits;this.cancellation=cancellation;
        cancellation.ThrowIfCancellationRequested();
        var trusted=mapping.Bones.Where(p=>p.Binding is{Supplied:true,Reviewed:true,RequiresReview:false}).Select(p=>p.Target).ToHashSet();
        var selected=new HashSet<int>();var points=new HashSet<int>();var terminals=new HashSet<int>();
        foreach(var chain in rig.Binding.Chains.Where(c=>c.Reviewed&&c.Bones.All(trusted.Contains)))
        {
            cancellation.ThrowIfCancellationRequested();
            if(!PoseGeometry.Ancestor(rig.Scene,body,chain.Bones[0]))continue;
            terminals.Add(chain.Bones[^1]);foreach(int b in chain.Bones)points.Add(b);
            // Distal rotations affect the actual sole/palm and helper skin even
            // though they do not move the endpoint joint itself.
            foreach(int b in chain.Bones)for(int p=b;p>=0&&PoseGeometry.Ancestor(rig.Scene,body,p);p=rig.Scene.Bones[p].Parent)
                if(trusted.Contains(p))selected.Add(p);
        }
        // Source/body surface goals can move reviewed weighted branches which
        // are not ancestors of a limb. Let those actual native joints and their
        // trusted paths participate too; anatomy names do not select DOFs.
        var chainRoots=rig.Binding.Chains.Where(c=>c.Reviewed&&c.Bones.All(trusted.Contains)).Select(c=>c.Bones[0]).ToArray();
        var weighted=rig.Scene.Meshes.SelectMany(m=>m.Vertices).SelectMany(v=>Enumerable.Range(0,4).Where(s=>v.Weights[s]>0).Select(s=>(int)v.Joints[s])).ToHashSet();
        // Preserve the existing missing-articulation capability boundary:
        // weighted body roles alone do not establish a reviewed complete rig.
        if(selected.Count>0)foreach(int bone in trusted.Where(b=>weighted.Contains(b)&&PoseGeometry.Ancestor(rig.Scene,body,b)&&!chainRoots.Any(r=>PoseGeometry.Ancestor(rig.Scene,r,b))))
        {
            cancellation.ThrowIfCancellationRequested();
            for(int p=bone;p>=0&&PoseGeometry.Ancestor(rig.Scene,body,p);p=rig.Scene.Bones[p].Parent)
                if(trusted.Contains(p))selected.Add(p);
        }
        // Reject before sampling instead of silently dropping coordinated DOFs
        // and entering the caller's translation-only compatibility path.
        if(selected.Count>MaximumRotationBones)
            throw new NotSupportedException($"Coordinated scene preparation selects {selected.Count} existing native rotation bones; its offline resource limit is {MaximumRotationBones}. No pose was sampled, no joint was discarded and no anatomy contradiction is implied.");
        bones=selected.OrderBy(b=>rig.CanonicalBoneRanks[b]).ToArray();
        shape=points.OrderBy(b=>rig.CanonicalBoneRanks[b]).ToArray();ends=terminals.OrderBy(b=>rig.CanonicalBoneRanks[b]).ToArray();
    }
    public void ValidateEvaluationBudget(int references,int probes,int sensors,int features,int screeningSensors=0,int screeningEnds=0,int extraCosts=0)
    {
        cancellation.ThrowIfCancellationRequested();
        if(references<0)throw new ArgumentOutOfRangeException(nameof(references));
        if(probes<0)throw new ArgumentOutOfRangeException(nameof(probes));
        if(sensors<0)throw new ArgumentOutOfRangeException(nameof(sensors));
        if(features<1)throw new ArgumentOutOfRangeException(nameof(features));
        if(screeningSensors<0||screeningEnds<0||extraCosts<0)throw new ArgumentOutOfRangeException(nameof(screeningSensors));
        if(!Enabled)return;
        long count=(long)references+probes,n=Variables,boneCount=rig.Scene.Bones.Length;
        // One initial world pose, one initial cost, then at most one analytic
        // linearization and BacktrackLimit candidate costs per iteration.
        // There are no +/-perturbed FK evaluations per variable.
        long costs=1L+IterationLimit*(1L+BacktrackLimit)+extraCosts,poses=5+costs;
        // Scene observations have one region per native bone, with at most one
        // extra free-extremity field per region. Matrix/point columns are cached
        // and shared between feature residuals. Count every actual influence,
        // including four transforms for each weighted point/derivative column.
        long fields=(long)features+1,regions=Math.Min((long)sensors,boneCount);
        long rows=Sum(Product(regions,fields),Product(features>1?3:2,screeningEnds),n,6L*shape.Length,9L*ends.Length,9L*bones.Length,9);
        long pointValues=Product(costs,sensors,Sum(4,fields));
        long pointColumns=Product(IterationLimit,sensors,n,Sum(4,fields));
        long priorColumns=Product(IterationLimit,n,Sum(n,3L*shape.Length,9L*ends.Length));
        long algebra=Product(IterationLimit,Sum(Product(rows,Sum(Product(n,n+1)/2,n)),Product(n,n,n),Product(BacktrackLimit,n,n)));
        // Keep the existing residual/skin and native-bone ceilings separate.
        // Report scalar Gram/Cholesky work independently: those operations did
        // not define the preexisting residual-work unit or its fixed ceiling.
        long screening=Sum(Product(costs,screeningSensors,features>1?6:5),Product(IterationLimit,screeningSensors,n,features>1?6:5));
        long actionColumns=Product(IterationLimit,n,Sum(9L*bones.Length,3L*shape.Length,9));
        long residualWork=Sum(pointValues,pointColumns,priorColumns,screening,actionColumns);
        // Preserve the existing bone-evaluation unit: one evaluated native
        // bone (or cached analytic bone/column), not one scalar/matrix multiply.
        // Report the separate matrix-product bound transparently; folding it
        // into this ceiling would silently redefine the old resource policy.
        long boneWork=Sum(Product(poses,boneCount),Product(IterationLimit,Sum(Product(boneCount,n+2),3L*bones.Length)));
        long matrixProducts=Sum(Product(8,poses,boneCount),Product(IterationLimit,Sum(Product(boneCount,Sum(3*n,2)),18L*bones.Length,2*n)),2*costs);
        estimatedResidualWork=Product(count,residualWork);estimatedBoneWork=Product(count,boneWork);
        estimatedPoseEvaluations=Product(count,poses);estimatedSkinColumns=Product(count,IterationLimit,Sum(sensors,screeningSensors),n);
        estimatedAlgebraWork=Product(count,algebra);
        estimatedMatrixProducts=Product(count,matrixProducts);
        if(estimatedResidualWork>MaximumResidualWork||estimatedBoneWork>MaximumBoneWork)
            throw new NotSupportedException($"Coordinated scene preparation estimates {estimatedResidualWork} residual/skin-column work units (limit {MaximumResidualWork}) and {estimatedBoneWork} native bone/column work units (limit {MaximumBoneWork}) before sampling. This is an offline resource limit, not an anatomy contradiction.");
    }
    public Transform[] Fit(Transform[] input,Func<EvaluatedPose,NativePoseDifferentials?,ArticulatedResiduals> sceneResiduals)
    {
        cancellation.ThrowIfCancellationRequested();evaluations++;independentFits++;
        if(!Enabled){unsupported++;return input.ToArray();}
        var scene=rig.Scene;var initial=Pose(input);int parent=scene.Bones[body].Parent;
        if(parent<0||!Matrix4x4.Invert(initial.World[parent],out var parentInverse))return Reject("The existing body parent is absent or singular; no transform was normalized.");
        foreach(int b in bones)for(int p=b;p>=0;p=scene.Bones[p].Parent)
        {
            var s=input[p].Scale;
            // Analytic derivatives evaluate actual native affine skinning;
            // unlike rigid FABRIK this does not require uniform authored scale.
            // Keep the positive finite representation boundary explicit.
            if(!PoseGeometry.Finite(s)||s.X<=0||s.Y<=0||s.Z<=0)return Reject("Nonpositive/nonfinite authored scale needs an explicit affine representation policy; scales were retained.");
        }
        if(!WithinLimits(input))return Reject("The incoming native pose is outside supplied reviewed limits; this is not a proof of incompatible anatomy.");
        var objective=CreateObjective(input,sceneResiduals);
        float length=rig.CharacteristicLength;var parameters=new double[Variables];
        var (current,residual,local,_)=Evaluate(parameters,false);double first=current;int iterations=0;
        if(!double.IsFinite(current))return Reject();
        bool stopped=false;double damping=double.NaN;
        for(int iteration=0;iteration<IterationLimit;iteration++)
        {
            cancellation.ThrowIfCancellationRequested();iterations=iteration+1;
            int n=parameters.Length;var (_,r,_,jacobian)=Evaluate(parameters,true);residual=r;var columns=new double[n][];
            for(int column=0;column<n;column++)columns[column]=Enumerable.Range(0,r.Length).Select(row=>jacobian[row,column]).ToArray();
            var normal=new double[n,n];var gradient=new double[n];
            for(int a=0;a<n;a++)
            {
                gradient[a]=-Dot(columns[a],residual);
                for(int b=0;b<=a;b++)normal[a,b]=normal[b,a]=Dot(columns[a],columns[b]);
            }
            if(double.IsNaN(damping))damping=Math.Max(1e-6,.001*Enumerable.Range(0,n).Max(i=>normal[i,i]));
            var damped=(double[,])normal.Clone();for(int a=0;a<n;a++)damped[a,a]+=damping;
            var step=Solve(damped,gradient);if(step is null){linearSolveStops++;stopped=true;break;}
            NativeMotionTrajectory.LimitStep(step,n);double directional=-2*Dot(gradient,step);
            if(!double.IsFinite(directional)||directional>=0){noAcceptedLowerCostStops++;stopped=true;break;}
            bool accepted=false;
            for(int backtrack=0;backtrack<BacktrackLimit;backtrack++)
            {
                cancellation.ThrowIfCancellationRequested();var candidate=parameters.Zip(step,(a,b)=>a+b).ToArray();
                bool bounded=candidate.Take(3).Sum(v=>v*v)<=4;
                for(int i=3;i<candidate.Length;i+=3)bounded&=candidate.Skip(i).Take(3).Sum(v=>v*v)<=MaximumOffsetRadians*MaximumOffsetRadians;
                if(bounded)
                {
                    var (cost,candidateResidual,p,_)=Evaluate(candidate,false);
                    double quadratic=0;for(int a=0;a<n;a++)for(int b=0;b<n;b++)quadratic+=step[a]*normal[a,b]*step[b];
                    double predicted=-directional-quadratic,ratio=predicted>0?(current-cost)/predicted:double.NaN;
                    if(double.IsFinite(cost)&&double.IsFinite(ratio)&&ratio>0&&cost<current&&cost<=current+.0001*directional&&WithinLimits(p))
                    {parameters=candidate;current=cost;residual=candidateResidual;local=p;accepted=true;acceptedSteps++;damping=NativeMotionTrajectory.AdaptDamping(damping,ratio);break;}
                }
                rejectedTrials++;
                for(int i=0;i<step.Length;i++)step[i]*=.5;directional*=.5;
            }
            if(!accepted){noAcceptedLowerCostStops++;stopped=true;break;}
        }
        if(!stopped)iterationLimitStops++;
        maximumInitial=Math.Max(maximumInitial,first);maximumFinal=Math.Max(maximumFinal,current);maximumIterations=Math.Max(maximumIterations,iterations);
        if(current<first){improved++;for(int i=3;i<parameters.Length;i+=3){maximumCorrection=Math.Max(maximumCorrection,(float)(Math.Sqrt(parameters.Skip(i).Take(3).Sum(v=>v*v))*180/Math.PI));if(local[bones[(i-3)/3]].Rotation!=input[bones[(i-3)/3]].Rotation)modified.Add(bones[(i-3)/3]);}return local;}
        return input.ToArray();

        Transform[] Reject(string reason="The numeric scene objective is nonfinite or has inconsistent residual dimensions."){unsupported++;reasons.Add(reason);return input.ToArray();}
        (double Cost,double[] Residual,Transform[] Local,double[,] Jacobian) Evaluate(double[] x,bool derivatives)
        {
            var result=objective.Evaluate(x,derivatives);
            return(result.Cost,result.Residual,result.Local,result.Jacobian);
        }
    }

    public NativeSceneObjective CreateObjective(Transform[] input,Func<EvaluatedPose,NativePoseDifferentials?,ArticulatedResiduals> residuals,Transform[]? actionReference=null,BodyRelativeReachGoal[]? reachGoals=null,BodySurfaceReachObservation[]? surfaceGoals=null)
        =>new(rig,body,bones,shape,ends,limits,input,residuals,cancellation,actionReference,reachGoals,surfaceGoals);
    public void ValidateSurfaceBudget(int collocations,BodySurfaceReachGoals surface)
    {
        cancellation.ThrowIfCancellationRequested();long costs=3L+IterationLimit*(1L+BacktrackLimit),n=Variables;
        long extra=checked(collocations*checked(8*surface.MaterialPoints+surface.WorkspaceTerms+costs*surface.MomentTerms+IterationLimit*n*surface.MomentTerms+3L*costs*surface.Relationships));
        estimatedResidualWork=checked((estimatedResidualWork??0)+extra);
        estimatedAlgebraWork=checked((estimatedAlgebraWork??0)+collocations*IterationLimit*3L*surface.Relationships*n*(n+1)/2);
        if(estimatedResidualWork>MaximumResidualWork)throw new NotSupportedException("Body-surface observations exceed the existing combined residual-work resource ceiling before source-time sampling; no rig or relationship was discarded.");
    }
    public Transform[] ApplyCorrection(Transform[] input,double[] parameters)
    {
        if(parameters.Length!=Variables||!NativeSceneObjective.ParametersBounded(parameters))
            throw new NotSupportedException("Prepared native trajectory correction exceeds its recorded parameter representation bounds.");
        var result=CreateObjective(input,(_,_)=>new([],new double[0,0])).Apply(parameters);
        if(!WithinLimits(result))throw new NotSupportedException("Prepared native trajectory violates supplied reviewed joint limits at this sample.");
        RecordModified(input,result);return result;
    }
    public void RecordTrajectory(Transform[] input,Transform[] result,double initial,double final)
    {
        evaluations++;maximumInitial=Math.Max(maximumInitial,initial);maximumFinal=Math.Max(maximumFinal,final);
        if(!input.SequenceEqual(result))improved++;RecordModified(input,result);
    }
    void RecordModified(Transform[] input,Transform[] result)
    {
        // ApplyCorrection also visits independent fractional reconstruction
        // samples. A real native T change there owns a derived channel even
        // when every collocation addition rounded back to its incoming value.
        actualBodyTranslationDerived|=input[body].Translation!=result[body].Translation;
        foreach(int bone in bones)if(result[bone].Rotation!=input[bone].Rotation)
        {modified.Add(bone);maximumCorrection=Math.Max(maximumCorrection,Vector3.Distance(result[bone].Rotation,input[bone].Rotation));}
    }
    static Vector3[] OrientationDerivative(Matrix4x4 w,Matrix4x4 d)
    {
        var x=new Vector3(w.M11,w.M12,w.M13);var y=new Vector3(w.M21,w.M22,w.M23);
        var dx=new Vector3(d.M11,d.M12,d.M13);var dy=new Vector3(d.M21,d.M22,d.M23);
        var a=Vector3.Normalize(x);var da=(dx-a*Vector3.Dot(a,dx))/x.Length();
        var orth=y-a*Vector3.Dot(a,y);var b=Vector3.Normalize(orth);
        var delta=dy-da*Vector3.Dot(a,y)-a*(Vector3.Dot(da,y)+Vector3.Dot(a,dy));
        var db=(delta-b*Vector3.Dot(b,delta))/orth.Length();
        // PoseGeometry normalizes this final cross product too. Retain its
        // actual normalization derivative even when roundoff leaves the first
        // two Gram-Schmidt rows only approximately orthogonal/unit length.
        var cross=Vector3.Cross(a,b);var z=Vector3.Normalize(cross);
        var dz=Vector3.Cross(da,b)+Vector3.Cross(a,db);
        return[da,db,(dz-z*Vector3.Dot(z,dz))/cross.Length()];
    }
    EvaluatedPose Pose(Transform[] local)=>AnimationService.Evaluate(rig.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
    bool WithinLimits(Transform[] pose)=>limits.All(l=>Enumerable.Range(0,3).All(a=>pose[l.Bone].Rotation[a]>=l.MinimumDegrees[a]&&pose[l.Bone].Rotation[a]<=l.MaximumDegrees[a]));
    static double Dot(double[] a,double[] b){double sum=0;for(int i=0;i<a.Length;i++)sum+=a[i]*b[i];return sum;}
    static long Product(params long[] values)
    {
        if(values.Any(v=>v==0))return 0;long result=1;
        foreach(long value in values){if(result>long.MaxValue/value)return long.MaxValue;result*=value;}return result;
    }
    static long Sum(params long[] values)
    {
        long result=0;foreach(long value in values){if(result>long.MaxValue-value)return long.MaxValue;result+=value;}return result;
    }
    static double[]? Solve(double[,] a,double[] b)
    {
        int n=b.Length;var l=new double[n,n];
        for(int i=0;i<n;i++)for(int j=0;j<=i;j++)
        {double s=a[i,j];for(int k=0;k<j;k++)s-=l[i,k]*l[j,k];if(i==j){if(!double.IsFinite(s)||s<=0)return null;l[i,j]=Math.Sqrt(s);}else l[i,j]=s/l[j,j];}
        var y=new double[n];for(int i=0;i<n;i++){double s=b[i];for(int j=0;j<i;j++)s-=l[i,j]*y[j];y[i]=s/l[i,i];}
        var x=new double[n];for(int i=n-1;i>=0;i--){double s=y[i];for(int j=i+1;j<n;j++)s-=l[j,i]*x[j];x[i]=s/l[i,i];}return x;
    }
    public ArticulatedSceneReceipt Receipt()=>new(Version,evaluations,improved,unsupported,Variables,maximumIterations,maximumInitial,maximumFinal,maximumCorrection,
        ["soft geometry/action objective, not contacts","per-pose solve, not temporal continuity proof","native curves and withheld skin validation","finite geometry and held-out anatomy"])
        {SelectedRotationBones=bones.ToArray(),ModifiedRotationBones=modified.OrderBy(b=>rig.CanonicalBoneRanks[b]).ToArray(),UnsupportedReasons=reasons.Order(StringComparer.Ordinal).ToArray(),
         WorkBudgetVersion=WorkBudgetVersion,ResidualWorkBudget=MaximumResidualWork,BoneWorkBudget=MaximumBoneWork,
         EstimatedResidualWork=estimatedResidualWork,EstimatedBoneWork=estimatedBoneWork,EstimatedPoseEvaluations=estimatedPoseEvaluations,EstimatedSkinColumns=estimatedSkinColumns,
         EstimatedAlgebraWork=estimatedAlgebraWork,
         EstimatedMatrixProducts=estimatedMatrixProducts,
         OptimizationScope=independentFits>0?"per-pose":evaluations>0?"shared-trajectory-collocations":null,
         ActualBodyTranslationDerived=actualBodyTranslationDerived,
         TerminationEvidenceVersion=independentFits>0?TerminationEvidenceVersion:null,AcceptedSteps=independentFits>0?acceptedSteps:null,RejectedTrials=independentFits>0?rejectedTrials:null,
         NoAcceptedLowerCostStops=independentFits>0?noAcceptedLowerCostStops:null,IterationLimitStops=independentFits>0?iterationLimitStops:null,LinearSolveStops=independentFits>0?linearSolveStops:null,
         ConvergenceStatus=independentFits>0?"NotCertified":null,
         RotationOffsetBoundDegrees=MaximumOffsetRadians*180/Math.PI,
         WorkUnits="Residual/weighted-skin columns and native bone/column evaluations have separate existing ceilings; scalar algebra and matrix products are reported separately. These are conservative operation bounds, not measured CPU time or contact/action acceptance"};
}
