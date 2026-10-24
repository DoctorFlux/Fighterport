namespace FighterPort.Core;

// Original bounded path-structured minimax fit. Observations couple adjacent
// shared native derivatives; integer values and time are never variables.
internal static class SharedSlopeMinimax
{
    internal readonly record struct Equation(int Segment,double A,double B,double Right);
    internal record Result(double[] Slopes,int Calls,long Work,double FittingError)
    {public bool WorkLimitReached {get;init;}}
    public const string Version="integer-shared-slope-envelopes-4";
    public const int MaximumCalls=32;
    public const long MaximumWork=100000000;
    readonly record struct Strip(double Lo,double Hi,double D);
    readonly record struct Line(double Slope,double Intercept,double Start=double.NegativeInfinity);
    sealed class WorkLimitException:Exception;
    public static Result Fit(int count,Equation[] rows,double[] seed,ref long remaining,CancellationToken cancellation,double stopError=0)
    {
        cancellation.ThrowIfCancellationRequested();
        if(remaining<0||!double.IsFinite(stopError)||stopError<0||count<2||seed.Length!=count||seed.Any(v=>!double.IsFinite(v)||Math.Abs(v)>float.MaxValue)||rows.Any(r=>r.Segment<0||r.Segment>=count-1||
            !double.IsFinite(r.A)||!double.IsFinite(r.B)||!double.IsFinite(r.Right)||r.A<=0||r.B>0))
            throw new ArgumentException("Shared native slopes need finite interior Hermite equations and representable paired derivatives.");
        long available=remaining,spent=0;
        var best=seed.ToArray();double upper=rows.Select(r=>Math.Abs(r.A*best[r.Segment]+r.B*best[r.Segment+1]-r.Right)).DefaultIfEmpty(0).Max(),lower=0;
        int calls=0;bool limited=false;
        void Charge(long amount)
        {
            if(amount>available-spent)throw new WorkLimitException();
            spent+=amount;
        }
        try
        {
            Charge(rows.LongLength+count);
            var buckets=Enumerable.Range(0,count-1).Select(_=>new List<Equation>()).ToArray();
            foreach(var row in rows)buckets[row.Segment].Add(row);
            var groups=buckets.Select(g=>g.ToArray()).ToArray();
            double Error(double[] slopes)
            {
                Charge(rows.LongLength);return rows.Select(r=>Math.Abs(r.A*slopes[r.Segment]+r.B*slopes[r.Segment+1]-r.Right)).DefaultIfEmpty(0).Max();
            }
            for(int iteration=0;iteration<MaximumCalls;iteration++)
            {
                if(upper<=stopError)break;
                cancellation.ThrowIfCancellationRequested();double trial=(lower+upper)*.5;
                calls++;var fitted=Feasible(trial);
                if(fitted is null){lower=trial;continue;}
                double error=Error(fitted);
                if(error<upper){best=fitted;upper=error;}else lower=trial;
            }

            double[]? Feasible(double tolerance)
            {
                Charge(3L*count);double cap=float.MaxValue;
                var lo=Enumerable.Repeat(-cap,count).ToArray();var hi=Enumerable.Repeat(cap,count).ToArray();var saved=new Strip[count-1][];
                for(int s=0;s<count-1;s++)
                {
                    cancellation.ThrowIfCancellationRequested();Charge(groups[s].LongLength+1);
                    var strips=groups[s].Select(r=>new Strip((r.Right-tolerance)/r.A,(r.Right+tolerance)/r.A,-r.B/r.A))
                        .Append(new Strip(lo[s],hi[s],0)).ToArray();saved[s]=strips;
                    var interval=Project(strips,cap);if(interval is null)return null;
                    var (low,high)=interval.Value;
                    if(low>high||!double.IsFinite(low)||!double.IsFinite(high))return null;
                    lo[s+1]=low;hi[s+1]=high;
                }
                // Reuse stored expressions and choose an interior value rather
                // than repeatedly landing on rounded strip boundaries.
                double Interior(double value,double low,double high)=>value>low&&value<high?value:low*.5+high*.5;
                var slopes=new double[count];slopes[^1]=Interior(seed[^1],lo[^1],hi[^1]);
                for(int s=count-2;s>=0;s--)
                {
                    cancellation.ThrowIfCancellationRequested();Charge(saved[s].LongLength);double low=lo[s],high=hi[s];
                    foreach(var strip in saved[s]){low=Math.Max(low,strip.Lo+strip.D*slopes[s+1]);high=Math.Min(high,strip.Hi+strip.D*slopes[s+1]);}
                    if(low>high||!double.IsFinite(low)||!double.IsFinite(high))return null;
                    slopes[s]=Interior(seed[s],low,high);
                }
                return slopes;
            }

            // max(lower lines) <= min(upper lines). Their two affine envelopes
            // have at most2N pieces; sweeping the merged breakpoints replaces
            // all N^2 strip pairs without changing the convex feasible set.
            (double Low,double High)? Project(Strip[] strips,double cap)
            {
                if(strips.Any(s=>double.IsPositiveInfinity(s.Lo)||double.IsNegativeInfinity(s.Hi)))return null;
                var lower=Envelope(strips.Where(s=>double.IsFinite(s.Lo)).Select(s=>new Line(s.D,s.Lo)).ToArray());
                var upper=Envelope(strips.Where(s=>double.IsFinite(s.Hi)).Select(s=>new Line(-s.D,-s.Hi)).ToArray());
                int a=0,b=0;while(a+1<lower.Length&&lower[a+1].Start<=-cap)a++;
                while(b+1<upper.Length&&upper[b+1].Start<=-cap)b++;
                double start=-cap,low=double.PositiveInfinity,high=double.NegativeInfinity;
                while(start<=cap)
                {
                    cancellation.ThrowIfCancellationRequested();Charge(1);
                    double nextA=a+1<lower.Length?lower[a+1].Start:double.PositiveInfinity;
                    double nextB=b+1<upper.Length?upper[b+1].Start:double.PositiveInfinity;
                    double end=Math.Min(cap,Math.Min(nextA,nextB)),m=lower[a].Slope+upper[b].Slope,c=lower[a].Intercept+upper[b].Intercept;
                    double left=start,right=end;
                    if(m>0)right=Math.Min(right,-c/m);else if(m<0)left=Math.Max(left,-c/m);else if(c>0)left=double.PositiveInfinity;
                    if(left<=right){low=Math.Min(low,left);high=Math.Max(high,right);}
                    if(end>=cap)break;
                    if(nextA==end)a++;if(nextB==end)b++;start=end;
                }
                return low<=high?(low,high):null;
            }
            Line[] Envelope(Line[] input)
            {
                // Introsort and hull work are conservatively charged before
                // allocation; the old quadratic worst-case charge is gone.
                Charge(checked((long)input.Length*(4*(int)Math.Ceiling(Math.Log2(Math.Max(2,input.Length)))+3)));
                var ordered=input.OrderBy(l=>l.Slope).ThenByDescending(l=>l.Intercept).ToArray();var hull=new List<Line>();
                foreach(var line in ordered)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if(hull.Count>0&&line.Slope==hull[^1].Slope)continue;
                    double start=double.NegativeInfinity;
                    while(hull.Count>0)
                    {
                        start=(hull[^1].Intercept-line.Intercept)/(line.Slope-hull[^1].Slope);
                        if(double.IsNaN(start))throw new NotSupportedException("Native minimax envelope produced an undefined intersection; no fit was published.");
                        if(start>hull[^1].Start)break;hull.RemoveAt(hull.Count-1);
                    }
                    if(hull.Count==0)start=double.NegativeInfinity;
                    hull.Add(line with{Start=start});
                }
                return hull.ToArray();
            }
        }
        catch(WorkLimitException){limited=true;}
        finally{remaining=available-spent;}
        return new(best,calls,spent,upper){WorkLimitReached=limited};
    }
}
