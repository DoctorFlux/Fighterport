namespace FighterPort.Core;

// Linearized feasibility priority, not a weighted substitute for hard pins.
// Restore satisfied rows towards zero, then search their numerical nullspace
// for reduction of the other residuals. Nonlinear evaluated-curve acceptance
// remains mandatory: a linear step never proves contact feasibility.
public static class FeasibleCurveDirection
{
    // A proximal correction towards the SOURCE coefficients, not just a small
    // incremental step towards the last already-distorted candidate. Damping
    // is relative to this window's actual Jacobian scale. Required feasibility
    // is still checked on the resulting native curves, never inferred from
    // this regularized least-squares objective.
    public static double[] SourceTether(double[,] jacobian,double[] error,double[] preferred,double relativeDamping,CancellationToken cancellation=default)
    {
        int rows=jacobian.GetLength(0),n=jacobian.GetLength(1);
        if(error.Length!=rows||preferred.Length!=n||!double.IsFinite(relativeDamping)||relativeDamping<=0)
            throw new ArgumentException("Source tether needs matching dimensions and finite positive relative damping.");
        if(n==0)return [];
        double trace=0;var right=new double[n];
        for(int row=0;row<rows;row++)for(int j=0;j<n;j++)
        {double value=jacobian[row,j];trace+=value*value;right[j]-=value*error[row];}
        double damping=Math.Max(1e-12,relativeDamping*trace/n);
        for(int j=0;j<n;j++)right[j]+=damping*preferred[j];
        double[] Multiply(double[] x)
        {
            cancellation.ThrowIfCancellationRequested();var projected=new double[rows];
            for(int row=0;row<rows;row++)for(int j=0;j<n;j++)projected[row]+=jacobian[row,j]*x[j];
            var result=new double[n];
            for(int j=0;j<n;j++){result[j]=damping*x[j];for(int row=0;row<rows;row++)result[j]+=jacobian[row,j]*projected[row];}
            return result;
        }
        return TemporalRetargetSolver.ConjugateGradient(right,Multiply,Math.Min(256,n*4));
    }
    // Add a source-preferred motion only in the numerical nullspace of ALL
    // supplied required rows. Unlike a large soft weight, this cannot trade a
    // linearized contact improvement for a nicer preferred pose. Real curves
    // still undergo the independent nonlinear skin/vector acceptance below.
    public static double[] Toward(double[,] jacobian,double[] primary,double[] preferred,CancellationToken cancellation=default)
    {
        int rows=jacobian.GetLength(0),n=jacobian.GetLength(1);
        if(primary.Length!=n||preferred.Length!=n)throw new ArgumentException("Preferred direction dimensions do not match.");
        static double Dot(double[] a,double[] b){double result=0;for(int j=0;j<a.Length;j++)result+=a[j]*b[j];return result;}
        var basis=new List<double[]>();const double resolution=8*1.1920928955078125e-7/.001;
        for(int row=0;row<rows;row++)
        {
            cancellation.ThrowIfCancellationRequested();var original=new double[n];for(int j=0;j<n;j++)original[j]=jacobian[row,j];
            double norm=Math.Sqrt(Dot(original,original));if(norm==0)continue;var v=original.ToArray();
            for(int pass=0;pass<2;pass++)foreach(var q in basis){double d=Dot(v,q);for(int j=0;j<n;j++)v[j]-=d*q[j];}
            double length=Math.Sqrt(Dot(v,v));if(length<=norm*resolution)continue;
            // No partially protected nullspace when the bounded factorization
            // cannot represent another independent required direction.
            if(basis.Count>=256)return primary.ToArray();
            for(int j=0;j<n;j++)v[j]/=length;basis.Add(v);if(basis.Count==n)return primary.ToArray();
        }
        var extra=preferred.Zip(primary,(a,b)=>a-b).ToArray();
        for(int pass=0;pass<2;pass++)foreach(var q in basis){double d=Dot(extra,q);for(int j=0;j<n;j++)extra[j]-=d*q[j];}
        // Dependent float32 rows may still expose roundoff directions. Reject
        // such a preference instead of claiming protection from a partial QR.
        double size=Math.Sqrt(Dot(extra,extra));
        for(int row=0;row<rows;row++)
        {
            double value=0,norm=0;for(int j=0;j<n;j++){value+=jacobian[row,j]*extra[j];norm+=jacobian[row,j]*jacobian[row,j];}
            if(Math.Abs(value)>Math.Max(1e-12,Math.Sqrt(norm)*size*1e-8))return primary.ToArray();
        }
        return primary.Zip(extra,(a,b)=>a+b).ToArray();
    }
    public static double[] Solve(double[,] jacobian,double[] error,IReadOnlyList<int> satisfiedRows,CancellationToken cancellation=default)
    {
        int rows=jacobian.GetLength(0),n=jacobian.GetLength(1);
        if(error.Length!=rows||satisfiedRows.Any(r=>r<0||r>=rows))throw new ArgumentException("Linear constraint dimensions do not match.");
        var basis=new List<double[]>();var origin=new double[n];
        const double rankResolution=8*1.1920928955078125e-7/.001;
        static double Dot(double[] a,double[] b){double value=0;for(int j=0;j<a.Length;j++)value+=a[j]*b[j];return value;}
        foreach(int row in satisfiedRows)
        {
            cancellation.ThrowIfCancellationRequested();if(basis.Count>=Math.Min(n,256))break;
            var a=new double[n];for(int j=0;j<n;j++)a[j]=jacobian[row,j];double norm=Dot(a,a);if(norm==0)continue;
            var v=a.ToArray();
            // Reorthogonalize to keep duplicate/highly dependent skin rows
            // from manufacturing a spurious degree of freedom.
            for(int pass=0;pass<2;pass++)foreach(var q in basis){double d=Dot(v,q);for(int j=0;j<n;j++)v[j]-=d*q[j];}
            double length=Math.Sqrt(Dot(v,v));if(length<=Math.Sqrt(norm)*rankResolution)continue;
            for(int j=0;j<n;j++)v[j]/=length;
            double correction=(-error[row]-Dot(a,origin))/length;
            for(int j=0;j<n;j++)origin[j]+=v[j]*correction;
            basis.Add(v);
        }
        double[] Project(double[] x)
        {
            var result=x.ToArray();foreach(var q in basis){double d=Dot(result,q);for(int j=0;j<n;j++)result[j]-=d*q[j];}return result;
        }
        var rhs=new double[n];
        for(int row=0;row<rows;row++)
        {
            double residual=error[row];for(int j=0;j<n;j++)residual+=jacobian[row,j]*origin[j];
            for(int j=0;j<n;j++)rhs[j]-=jacobian[row,j]*residual;
        }
        rhs=Project(rhs);
        double[] Multiply(double[] x)
        {
            cancellation.ThrowIfCancellationRequested();var projected=Project(x);var tmp=new double[rows];
            for(int row=0;row<rows;row++)for(int j=0;j<n;j++)tmp[row]+=jacobian[row,j]*projected[j];
            var y=new double[n];for(int j=0;j<n;j++){y[j]=.01*projected[j];for(int row=0;row<rows;row++)y[j]+=jacobian[row,j]*tmp[row];}return Project(y);
        }
        var step=Project(TemporalRetargetSolver.ConjugateGradient(rhs,Multiply,Math.Min(128,n*2)));
        for(int j=0;j<n;j++)step[j]+=origin[j];return step;
    }
}
