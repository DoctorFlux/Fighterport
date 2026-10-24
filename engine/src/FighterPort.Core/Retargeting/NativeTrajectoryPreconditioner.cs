namespace FighterPort.Core;

// SPD partial-Gram preconditioner. Each observation retains its strongest
// adjacent temporal pair and all remaining principal pose blocks. The FULL
// operator/constraints, including periodic seam links, remain unchanged.
internal sealed class NativeTrajectoryPreconditioner
{
    readonly int variables,dimension,width;
    readonly double[,] band;
    public NativeTrajectoryPreconditioner(int controls,int variables)
    {this.variables=variables;dimension=checked(controls*variables);width=Math.Min(dimension,2*variables);band=new double[dimension,width];}
    public double MaximumDiagonal=>Enumerable.Range(0,dimension).Max(i=>band[i,0]);
    public void Add(int[] knots,double[] weights,double[,]? normal=null)
    {
        if(knots.Distinct().Count()!=knots.Length)
        {
            var combined=knots.Select((k,i)=>(Knot:k,Weight:weights[i])).GroupBy(p=>p.Knot).OrderBy(g=>g.Key).ToArray();
            weights=combined.Select(g=>g.Sum(p=>p.Weight)).ToArray();knots=combined.Select(g=>g.Key).ToArray();
        }
        for(int j=0;j<knots.Length;j++)for(int a=0;a<variables;a++)for(int b=normal is null?a:0;b<=a;b++)
            band[knots[j]*variables+a,a-b]+=weights[j]*weights[j]*(normal is null?1:normal[a,b]);
        int left=-1,right=-1;double strongest=0;
        for(int a=0;a<knots.Length;a++)for(int b=a+1;b<knots.Length;b++)
            if(Math.Abs(knots[a]-knots[b])==1&&Math.Abs(weights[a]*weights[b])>strongest)
            {left=a;right=b;strongest=Math.Abs(weights[a]*weights[b]);}
        if(left<0)return;
        int lo=Math.Min(knots[left],knots[right]),hi=Math.Max(knots[left],knots[right]);double product=weights[left]*weights[right];
        for(int a=0;a<variables;a++)for(int b=normal is null?a:0;b<(normal is null?a+1:variables);b++)
        {int row=hi*variables+a,column=lo*variables+b;band[row,row-column]+=product*(normal is null?1:normal[a,b]);}
    }
    public bool Factor(double damping,CancellationToken cancellation)
    {
        for(int row=0;row<dimension;row++)band[row,0]+=damping;
        for(int row=0;row<dimension;row++)
        {
            cancellation.ThrowIfCancellationRequested();int first=Math.Max(0,row-width+1);
            for(int column=first;column<=row;column++)
            {
                double value=band[row,row-column];
                for(int k=first;k<column;k++)value-=band[row,row-k]*band[column,column-k];
                if(row==column){if(!double.IsFinite(value)||value<=0)return false;band[row,0]=Math.Sqrt(value);}
                else band[row,row-column]=value/band[column,0];
            }
        }
        return true;
    }
    public double[] Solve(double[] value)
    {
        var forward=new double[dimension];var result=new double[dimension];
        for(int row=0;row<dimension;row++)
        {
            double sum=value[row];for(int k=Math.Max(0,row-width+1);k<row;k++)sum-=band[row,row-k]*forward[k];
            forward[row]=sum/band[row,0];
        }
        for(int row=dimension-1;row>=0;row--)
        {
            double sum=forward[row];for(int k=row+1;k<Math.Min(dimension,row+width);k++)sum-=band[k,k-row]*result[k];
            result[row]=sum/band[row,0];
        }
        return result;
    }
}
