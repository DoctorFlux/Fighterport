namespace FighterPort.Core;

public record NativeCurveFitReceipt(string Version,int PoseEvaluations,int EvaluationBudget,int AcceptedUpdates,int Sweeps,bool BudgetExhausted)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeContactVelocityReceipt? ContactVelocity {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int KeyValueUpdates {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int ImmutableConstraintWindows {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int SharedBasisWindows {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string,int>? RestorationReasons {get;init;}
}
public record NativeCurveFit(BoneTrack[] Tracks,NativeCurveFitReceipt Receipt);

// Destination integer-key, single-tangent Hermite contract. Prepared poses seed
// the coefficients; permitted key values and shared slopes are then restored
// together against geometry. Key times and native structural policies stay fixed.
// The existing adapter owns actual encoding/compression and fresh native reopen.
public static class NativeCurveBaker
{
    public const string Version="native-constraint-active-set-source-prior-42";
    public const int PoseEvaluationBudget=2000000,NativeBoneEvaluationBudget=16000000;
    public static int DefaultBudget(int bones)=>Math.Min(PoseEvaluationBudget,NativeBoneEvaluationBudget/Math.Max(1,bones));
    internal static BoneTrack[] Initialize(TargetMotionPlan plan,SolvedTargetMotion solved,bool held,CancellationToken cancellation)
    {
        int keys=plan.Samples+(plan.Closing?1:0),step=TemporalRetargetSolver.Substeps;
        if(solved.Poses.Length!=(keys-1)*step+1||solved.Frames.Length!=solved.Poses.Length
            ||solved.Frames.Where((frame,i)=>frame!=i/(double)step).Any())
            throw new ArgumentException("Curve initialization needs the declared quarter-frame lattice and complete closing sample.",nameof(solved));
        var tracks=Enumerable.Range(0,plan.Rig.Scene.Bones.Length).Select(bone=>new BoneTrack(plan.Rig.Scene.Bones[bone].Name,Enumerable.Range(0,9).Select(channel=>
        {
            cancellation.ThrowIfCancellationRequested();
            var values=Enumerable.Range(0,keys).Select(k=>solved.Poses[k*step][bone].Channels()[channel]).ToArray();
            var diagonal=new double[keys];var upper=new double[keys];var rhs=new double[keys];
            if(!held)for(int segment=0;segment<keys-1;segment++)for(int i=1;i<step;i++)
            {
                double u=i/(double)step,a=u*u*u-2*u*u+u,b=u*u*u-u*u;
                double baseValue=(2*u*u*u-3*u*u+1)*values[segment]+(-2*u*u*u+3*u*u)*values[segment+1];
                double target=solved.Poses[segment*step+i][bone].Channels()[channel]-baseValue;
                diagonal[segment]+=a*a;diagonal[segment+1]+=b*b;upper[segment]+=a*b;rhs[segment]+=a*target;rhs[segment+1]+=b*target;
            }
            // A tiny declared regularizer resolves unconstrained one-key cases.
            for(int i=0;i<keys;i++)diagonal[i]+=1e-12;
            for(int i=1;i<keys;i++){double factor=upper[i-1]/diagonal[i-1];diagonal[i]-=factor*upper[i-1];rhs[i]-=factor*rhs[i-1];}
            var slopes=new double[keys];for(int i=keys-1;i>=0;i--)slopes[i]=(rhs[i]-(i+1<keys?upper[i]*slopes[i+1]:0))/diagonal[i];
            return (Curve?)new Curve(values.Select((value,k)=>new Key(k,value,held?0:(float)slopes[k],held?0:(float)slopes[k])).ToArray(),held?Interpolation.Step:Interpolation.Hermite);
        }).ToArray())).ToArray();
        return tracks;
    }
    public static NativeCurveFit Fit(TargetMotionPlan plan,SolvedTargetMotion solved,bool held,CancellationToken cancellation=default,int? evaluationBudget=null)
    {
        int budget=evaluationBudget??DefaultBudget(plan.Rig.Scene.Bones.Length);
        if(budget<1||budget>PoseEvaluationBudget)throw new ArgumentOutOfRangeException(nameof(evaluationBudget));
        var tracks=Initialize(plan,solved,held,cancellation);
        if(held)return new(tracks,new(Version,0,budget,0,0,false));
        // Reserve the same bounded native work budget for constraint-velocity
        // initialization and later actual-skin projection. A one-evaluation
        // diagnostic still exposes the unmodified least-squares initialization.
        var initialized=NativeContactVelocity.Initialize(plan,tracks,cancellation,Math.Min(30000,budget/4));
        var projected=NativeCurveProjection.Refine(plan,initialized.Tracks,cancellation,budget-initialized.Receipt.PoseEvaluations);
        return projected with{Receipt=projected.Receipt with
        {
            PoseEvaluations=projected.Receipt.PoseEvaluations+initialized.Receipt.PoseEvaluations,
            EvaluationBudget=budget,
            ContactVelocity=initialized.Receipt
        }};
    }
}
