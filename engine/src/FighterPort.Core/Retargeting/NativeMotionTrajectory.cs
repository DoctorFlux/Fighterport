using System.Text.Json.Serialization;

namespace FighterPort.Core;

public record NativeTrajectoryFrame(double Frame,NativeSceneObjective Objective);
public record TemporalBodyFittingReceipt(string Version,string ObjectiveVersion,int Knots,int Collocations,int VariablesPerKnot,
    int Iterations,int AcceptedSteps,int RejectedTrials,int LinearIterations,double InitialCost,double FinalCost,
    double? MaximumCorrectionVelocity,double? MaximumCorrectionAcceleration,string Status,string[] OpenGates)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? BoundaryPolicy {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? DerivativeMeasure {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public long? EstimatedNormalEntries {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public long? EstimatedLinearWork {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public long? NormalEntryBudget {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public long? LinearWorkBudget {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? StepPolicy {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? DampingPolicy {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public double? FinalDamping {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public double? MinimumAcceptedReductionRatio {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? LinearSolverPolicy {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? LinearIterationLimit {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? LinearIterationLimitStops {get;init;}
}

// Joint optimization of a shared native correction field. Neighboring source
// times contribute to the SAME unknown controls during optimization. This is
// neither smoothing separately solved poses nor a playback-history warm start.
public sealed class NativeMotionTrajectory
{
    public const string Version="coupled-native-correction-trajectory-6";
    public const int MaximumIterations=8,MaximumBacktracks=8,MaximumLinearIterations=64;
    public const double VelocityTimeSeconds=.035,AccelerationTimeSeconds=.01;
    public const long MaximumParameters=100000,MaximumNormalEntries=4000000,MaximumLinearWork=2000000000;
    readonly NativeTrajectoryFrame[] frames;
    readonly Basis[] bases;
    readonly TemporalTerm[] temporal;
    readonly double[] parameters;
    readonly int knots,variables,displayKnots,linearIterationLimit;
    readonly double fps;
    readonly bool held,periodic;
    readonly CancellationToken cancellation;
    int iterations,accepted,rejected,linearIterations,linearLimitStops;
    double initialCost,finalCost;
    double damping=double.NaN;
    double? minimumAcceptedRatio;
    string status="NotCertified";
    sealed record Basis(int[] Knots,double[] Weights);
    sealed record TemporalTerm(int[] Knots,double[] Weights);
    sealed record Block(Basis Basis,double[,] Normal);
    public NativeMotionTrajectory(NativeTrajectoryFrame[] frames,int knots,double framesPerSecond,bool held,CancellationToken cancellation=default,bool periodic=false)
    {
        this.frames=frames;displayKnots=knots;this.periodic=periodic&&knots>1;this.knots=this.periodic?knots-1:knots;fps=framesPerSecond;this.held=held;this.cancellation=cancellation;
        cancellation.ThrowIfCancellationRequested();
        if(knots<1||!double.IsFinite(fps)||fps<=0||frames.Length<1||frames.Any(f=>f.Frame<0||f.Frame>knots-1||!double.IsFinite(f.Frame))||
            frames.Where((f,i)=>i>0&&f.Frame<=frames[i-1].Frame).Any())
            throw new ArgumentException("A prepared trajectory requires finite native times and a positive actual timebase.");
        variables=frames[0].Objective.Variables;
        if(frames.Any(f=>f.Objective.Variables!=variables))throw new ArgumentException("Trajectory collocations must share the same reviewed native variables.");
        linearIterationLimit=PlanBudget(displayKnots,variables,frames.Length,this.periodic).Limit;
        parameters=new double[checked(this.knots*variables)];bases=frames.Select(f=>At(f.Frame)).ToArray();
        var terms=new List<TemporalTerm>();
        // Seconds, not editor-frame counts. Penalize corrections only: authored
        // fast turns/impacts remain in the frozen incoming motion. Held channels
        // use their original discrete semantics rather than a smoothing bridge.
        if(!held&&this.knots>1)
        {
            int edges=this.periodic?this.knots:this.knots-1,centres=this.periodic?this.knots:Math.Max(0,this.knots-2);
            double velocity=VelocityTimeSeconds*fps/Math.Sqrt(edges);
            for(int k=0;k<edges;k++)terms.Add(new([k,(k+1)%this.knots],[-velocity,velocity]));
            double acceleration=AccelerationTimeSeconds*AccelerationTimeSeconds*fps*fps/Math.Sqrt(Math.Max(1,centres));
            for(int i=0;i<centres;i++){int k=this.periodic?i:i+1;terms.Add(new([(k+this.knots-1)%this.knots,k,(k+1)%this.knots],[acceleration,-2*acceleration,acceleration]));}
        }
        temporal=terms.ToArray();Solve();
    }
    public static void ValidateBudget(int nativeKeys,int variables,int collocations,bool periodic=false)
        =>_ =PlanBudget(nativeKeys,variables,collocations,periodic);
    internal static (int Limit,long Work) PlanBudget(int nativeKeys,int variables,int collocations,bool periodic=false)
    {
        if(nativeKeys<1||variables<3||variables>3+3*ArticulatedSceneFitting.MaximumRotationBones||(variables-3)%3!=0||collocations<1)
            throw new ArgumentOutOfRangeException(nameof(nativeKeys));
        long controls=periodic&&nativeKeys>1?nativeKeys-1:nativeKeys;
        if(controls*variables>MaximumParameters||((long)collocations+2*controls)*variables*variables>MaximumNormalEntries)
            throw new NotSupportedException("Coupled native trajectory exceeds its declared parameter/normal/preconditioner memory budget before preparation.");
        long fixedWork=LinearWork(controls,variables,collocations,0),perStep=LinearWork(controls,variables,collocations,1)-fixedWork;
        long limit=fixedWork>MaximumLinearWork?0:Math.Min(MaximumLinearIterations,(MaximumLinearWork-fixedWork)/Math.Max(1,perStep));
        if(limit<1)
            throw new NotSupportedException("Coupled native trajectory exceeds its declared matrix-free and block-factor operation budget before preparation.");
        return((int)limit,LinearWork(controls,variables,collocations,(int)limit));
    }
    public double[] EvaluateParameters(double frame)
    {
        cancellation.ThrowIfCancellationRequested();
        if(!double.IsFinite(frame)||frame<0||frame>displayKnots-1+1e-9)throw new ArgumentOutOfRangeException(nameof(frame));
        return Gather(parameters,At(Math.Clamp(frame,0,displayKnots-1)));
    }
    void Solve()
    {
        initialCost=finalCost=Cost(parameters);
        if(!double.IsFinite(initialCost))throw new NotSupportedException("Prepared source-time observations are nonfinite or outside reviewed native limits.");
        for(int iteration=0;iteration<MaximumIterations;iteration++)
        {
            cancellation.ThrowIfCancellationRequested();iterations=iteration+1;
            var blocks=new List<Block>();var gradient=new double[parameters.Length];var preconditioner=new NativeTrajectoryPreconditioner(knots,variables);
            double normalization=1d/frames.Length;
            for(int q=0;q<frames.Length;q++)
            {
                cancellation.ThrowIfCancellationRequested();var evaluation=frames[q].Objective.Evaluate(Gather(parameters,bases[q]),true);
                var h=new double[variables,variables];var g=new double[variables];
                for(int row=0;row<evaluation.Residual.Length;row++)
                {
                    for(int a=0;a<variables;a++)
                    {
                        double ja=evaluation.Jacobian[row,a]*normalization;if(ja==0)continue;
                        g[a]+=ja*evaluation.Residual[row];
                        for(int b=0;b<=a;b++)h[a,b]+=ja*evaluation.Jacobian[row,b];
                    }
                }
                for(int a=0;a<variables;a++)for(int b=0;b<a;b++)h[b,a]=h[a,b];
                blocks.Add(new(bases[q],h));Scatter(gradient,g,bases[q]);
                preconditioner.Add(bases[q].Knots,bases[q].Weights,h);
            }
            foreach(var term in temporal)
            {
                var basis=new Basis(term.Knots,term.Weights);Scatter(gradient,Gather(parameters,basis),basis);
                preconditioner.Add(term.Knots,term.Weights);
            }
            if(double.IsNaN(damping))damping=Math.Max(1e-6,.001*preconditioner.MaximumDiagonal);
            double currentDamping=damping;
            double[] Multiply(double[] value)
            {
                var result=value.Select(v=>v*currentDamping).ToArray();
                foreach(var block in blocks)
                {
                    var local=Gather(value,block.Basis);var product=new double[variables];
                    for(int a=0;a<variables;a++)for(int b=0;b<variables;b++)product[a]+=block.Normal[a,b]*local[b];
                    Scatter(result,product,block.Basis);
                }
                foreach(var term in temporal){var basis=new Basis(term.Knots,term.Weights);Scatter(result,Gather(value,basis),basis);}
                return result;
            }
            if(!preconditioner.Factor(currentDamping,cancellation)){status="LinearSolveStoppedNotCertified";break;}
            var step=Pcg(Multiply,gradient.Select(v=>-v).ToArray(),preconditioner.Solve);
            if(step is null){status="LinearSolveStoppedNotCertified";break;}
            // One shared line direction is essential for a coupled system.
            // Independently clipping its joint/body blocks can turn a descent
            // direction into ascent and purchase unrelated pose changes.
            LimitStep(step,variables);
            double directional=2*Dot(gradient,step);
            if(!double.IsFinite(directional)||directional>=0){status="NonDescentDirectionNotCertified";break;}
            bool changed=false;
            for(int backtrack=0;backtrack<MaximumBacktracks;backtrack++)
            {
                cancellation.ThrowIfCancellationRequested();var candidate=parameters.Zip(step,(a,b)=>a+b).ToArray();
                double cost=Cost(candidate);
                // Damping belongs to the proposed step, never the objective or
                // an acceptance tolerance. Compare actual reduction against
                // the undamped local residual/temporal quadratic model.
                var product=Multiply(step);double predicted=-directional-Dot(step,product)+currentDamping*Dot(step,step);
                double ratio=predicted>0?(finalCost-cost)/predicted:double.NaN;
                if(double.IsFinite(cost)&&double.IsFinite(ratio)&&ratio>0&&cost<finalCost&&cost<=finalCost+.0001*directional)
                {
                    Array.Copy(candidate,parameters,parameters.Length);finalCost=cost;accepted++;changed=true;
                    minimumAcceptedRatio=minimumAcceptedRatio is{} previous?Math.Min(previous,ratio):ratio;
                    damping=AdaptDamping(currentDamping,ratio);break;
                }
                rejected++;for(int i=0;i<step.Length;i++)step[i]*=.5;directional*=.5;
            }
            if(!changed)damping=Math.Min(currentDamping*4,1e12);
        }
    }
    internal static double AdaptDamping(double current,double ratio)
        =>Math.Clamp(current*Math.Max(1d/3,1-Math.Pow(2*ratio-1,3)),1e-12,1e12);
    static long LinearWork(long controls,int n,int observations,int iterations)
    {
        long dimension=controls*n,width=Math.Min(dimension,2L*n),backtracks=MaximumBacktracks;
        return MaximumIterations*(observations*(long)n*n*(iterations+backtracks+6)+observations*8L*n*(iterations+backtracks)+
            dimension*width*(width+1)/2+iterations*(2*dimension*width+8*dimension)+backtracks*dimension*320);
    }
    double Cost(double[] candidate)
    {
        double cost=0;
        // Check interpolated bounds too. An admissible knot does not guarantee
        // admissible cubic extrema. Withheld native/geometry checks stay separate.
        for(int i=0;i<=Math.Max(0,(displayKnots-1)*16);i++)
            if(!NativeSceneObjective.ParametersBounded(Gather(candidate,At(i/16d))))return double.PositiveInfinity;
        for(int q=0;q<frames.Length;q++)
        {
            cancellation.ThrowIfCancellationRequested();var local=Gather(candidate,bases[q]);
            if(!frames[q].Objective.Admissible(local))return double.PositiveInfinity;
            cost+=frames[q].Objective.Evaluate(local,false).Cost/frames.Length;
        }
        foreach(var term in temporal)cost+=Gather(candidate,new(term.Knots,term.Weights)).Sum(v=>v*v);
        return cost;
    }
    double[]? Pcg(Func<double[],double[]> multiply,double[] rhs,Func<double[],double[]> precondition)
    {
        var x=new double[rhs.Length];var r=rhs.ToArray();var z=precondition(r);var p=z.ToArray();double rz=Dot(r,z),initial=Dot(r,r);
        if(!double.IsFinite(rz)||initial==0)return initial==0?x:null;
        bool converged=false;
        for(int iteration=0;iteration<linearIterationLimit;iteration++)
        {
            cancellation.ThrowIfCancellationRequested();linearIterations++;var ap=multiply(p);double denominator=Dot(p,ap);
            if(!double.IsFinite(denominator)||denominator<=0)return null;
            double alpha=rz/denominator;
            for(int i=0;i<x.Length;i++){x[i]+=alpha*p[i];r[i]-=alpha*ap[i];}
            if(Dot(r,r)<=initial*1e-10){converged=true;break;}
            z=precondition(r);double next=Dot(r,z);
            if(!double.IsFinite(next))return null;double beta=next/rz;rz=next;
            for(int i=0;i<p.Length;i++)p[i]=z[i]+beta*p[i];
        }
        if(!converged)linearLimitStops++;
        return x.All(double.IsFinite)?x:null;
    }
    Basis At(double frame)
    {
        if(knots==1)return new([0],[1]);
        if(periodic)frame%=knots;
        if(held)return new([(int)Math.Min(knots-1,Math.Floor(frame))],[1]);
        int segment=periodic?(int)Math.Floor(frame):Math.Min(knots-2,(int)Math.Floor(frame));double u=frame-segment,u2=u*u,u3=u2*u;
        var weights=new SortedDictionary<int,double>();
        void Add(int key,double weight){weights[key]=weights.GetValueOrDefault(key)+weight;}
        void Slope(int key,double factor)
        {
            if(periodic){Add((key+knots-1)%knots,-factor*.5);Add((key+1)%knots,factor*.5);}
            else if(key==0){Add(0,-factor);Add(1,factor);}
            else if(key==knots-1){Add(key-1,-factor);Add(key,factor);}
            else{Add(key-1,-factor*.5);Add(key+1,factor*.5);}
        }
        Add(segment,2*u3-3*u2+1);Add((segment+1)%knots,-2*u3+3*u2);
        Slope(segment,u3-2*u2+u);Slope((segment+1)%knots,u3-u2);
        return new(weights.Keys.ToArray(),weights.Values.ToArray());
    }
    double[] Gather(double[] global,Basis basis)
    {
        var result=new double[variables];
        for(int j=0;j<basis.Knots.Length;j++)for(int v=0;v<variables;v++)result[v]+=basis.Weights[j]*global[basis.Knots[j]*variables+v];
        return result;
    }
    void Scatter(double[] global,double[] local,Basis basis)
    {for(int j=0;j<basis.Knots.Length;j++)for(int v=0;v<variables;v++)global[basis.Knots[j]*variables+v]+=basis.Weights[j]*local[v];}
    static double Dot(double[] a,double[] b){double result=0;for(int i=0;i<a.Length;i++)result+=a[i]*b[i];return result;}
    internal static double LimitStep(double[] step,int variables)
    {
        if(variables<3||(variables-3)%3!=0||step.Length%variables!=0||step.Any(v=>!double.IsFinite(v)))throw new ArgumentException("A coupled step needs complete finite native variable blocks.");
        double scale=1;
        for(int k=0;k<step.Length;k+=variables)
        {
            double translation=Math.Sqrt(Enumerable.Range(0,3).Sum(a=>step[k+a]*step[k+a]));
            if(translation>.25)scale=Math.Min(scale,.25/translation);
            for(int b=3;b<variables;b+=3)
            {
                double angle=Math.Sqrt(Enumerable.Range(0,3).Sum(a=>step[k+b+a]*step[k+b+a]));
                if(angle>ArticulatedSceneFitting.MaximumStepRadians)scale=Math.Min(scale,ArticulatedSceneFitting.MaximumStepRadians/angle);
            }
        }
        for(int i=0;i<step.Length;i++)step[i]*=scale;
        return scale;
    }
    public TemporalBodyFittingReceipt Receipt()
    {
        double velocity=0,acceleration=0;
        if(!held)for(int v=0;v<variables;v++)
        {
            var bounds=CorrectionDerivativeBounds(Enumerable.Range(0,knots).Select(k=>parameters[k*variables+v]).ToArray(),fps,periodic);
            velocity=Math.Max(velocity,bounds.Velocity);acceleration=Math.Max(acceleration,bounds.Acceleration);
        }
        return new(Version,NativeSceneObjective.Version,knots,frames.Length,variables,iterations,accepted,rejected,linearIterations,
            initialCost,finalCost,held?null:velocity,held?null:acceleration,status,["sampled soft objectives, not required contacts","sampled parameter bounds, independently rechecked on every query","native/subframe full-skin representation","body/action/style and continuous clearance","finite scene/held-out anatomy"])
            {BoundaryPolicy=periodic?"Periodic correction values and first derivatives; authentic source seam retained":"Open source interval; explicit closing sample remains distinct",
             DerivativeMeasure="Exact scalar-component extrema of the prepared cubic correction in normalized reviewed-rig-axis translation units/second or Euler radians/second; held jumps are unrated/null. Not full-body action or contact velocity.",
             StepPolicy="One globally scaled GN direction under unchanged body/joint step caps; descent and Armijo 0.0001 decrease required, eight backtracks; not a convergence certificate",
             DampingPolicy="LM step damping starts at max(1e-6,0.001*maximum native normal diagonal); actual/undamped-predicted decrease adapts it between existing bounded iterations; objective and fidelity bounds unchanged",
             FinalDamping=double.IsFinite(damping)?damping:null,MinimumAcceptedReductionRatio=minimumAcceptedRatio,
             EstimatedNormalEntries=((long)frames.Length+2L*knots)*variables*variables,NormalEntryBudget=MaximumNormalEntries,
             LinearSolverPolicy=$"Full matrix-free normal operator; SPD strongest-adjacent-pair partial-Gram band preconditioner, complete native pose blocks, full seam links; at most{linearIterationLimit} PCG iterations within the unchanged work budget (configured maximum64); not a feasibility certificate",
             LinearIterationLimit=linearIterationLimit,LinearIterationLimitStops=linearLimitStops,
             EstimatedLinearWork=LinearWork(knots,variables,frames.Length,linearIterationLimit),LinearWorkBudget=MaximumLinearWork};
    }
    internal static (double Velocity,double Acceleration) CorrectionDerivativeBounds(double[] keys,double fps,bool periodic=false)
    {
        if(keys.Length<1||keys.Any(v=>!double.IsFinite(v))||!double.IsFinite(fps)||fps<=0)throw new ArgumentException("Cubic derivative bounds need finite keys and a positive timebase.");
        double velocity=0,acceleration=0;
        double Slope(int k)=>periodic?(keys[(k+1)%keys.Length]-keys[(k+keys.Length-1)%keys.Length])*.5:k==0?keys[1]-keys[0]:k==keys.Length-1?keys[k]-keys[k-1]:(keys[k+1]-keys[k-1])*.5;
        for(int k=0;k<(periodic&&keys.Length>1?keys.Length:keys.Length-1);k++)
        {
            int next=(k+1)%keys.Length;
            double a=2*keys[k]-2*keys[next]+Slope(k)+Slope(next);
            double b=-3*keys[k]+3*keys[next]-2*Slope(k)-Slope(next),c=Slope(k);
            double local=Math.Max(Math.Abs(c),Math.Abs(3*a+2*b+c));
            double extremum=a!=0?-b/(3*a):double.NaN;
            if(extremum>0&&extremum<1)local=Math.Max(local,Math.Abs(3*a*extremum*extremum+2*b*extremum+c));
            velocity=Math.Max(velocity,local*fps);
            acceleration=Math.Max(acceleration,Math.Max(Math.Abs(2*b),Math.Abs(6*a+2*b))*fps*fps);
        }
        return(velocity,acceleration);
    }

}
