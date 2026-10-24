using System.Numerics;

namespace FighterPort.Core;

// Offline action prior: translate the existing body towards its observed
// support relationship before spending limb rotation to close the same gap.
// This is a regularized reference, never a replacement for hard constraints.
internal static class ContactMotionPrior
{
    // Global offline preparation budget, fixed independently of characters,
    // action labels and the acceptance bounds. This minimizes equal positional
    // relationship costs; it does not classify a translation-only compromise
    // as a feasible articulated pose.
    const int TranslationIterations=16;
    const float DifferenceStep=.0005f;
    const double Damping=1e-8;
    // Differencing float32 evaluated transforms cannot establish arbitrarily
    // small singular directions. Eight scalar roundoff units per difference
    // define this global numerical-rank policy, not a contact tolerance.
    const double RelativeSingularResolution=8*1.1920928955078125e-7/DifferenceStep;
    public static TargetMotionPlan Prepare(TargetMotionPlan plan,CancellationToken cancellation)
    {
        if(plan.Clearance is null&&!plan.Graph.Relations.SelectMany(c=>c.Pins).Any(p=>!p.SoftSupport||p.RequiredSupport))return plan;
        int body=plan.Rig.Binding.Roles.GetValueOrDefault("Hips",-1),root=plan.Rig.Binding.Roles["RootMotion"];
        if(body<0||body==root)return plan;
        int count=(plan.Samples-(plan.Closing?0:1))*TemporalRetargetSolver.Substeps+1;
        if(count>4096)throw new NotSupportedException("Body-support reference preparation supports at most 4096 samples; no truncated guide was prepared.");
        var desired=new Vector3[count];Vector3[]? winding=null;
        for(int f=0;f<count;f++)
        {
            cancellation.ThrowIfCancellationRequested();double time=f/(double)TemporalRetargetSolver.Substeps;
            var local=plan.Baseline.Evaluate(plan.SourceFrame(time),winding);winding=local.Select(t=>t.Rotation).ToArray();
            var pins=plan.Pins(time).Where(p=>!p.SoftSupport||p.RequiredSupport).ToArray();if(pins.Length==0&&!(plan.HasGround(time)&&plan.Clearance is not null))continue;
            desired[f]=Translation(plan,body,local,pins,time,cancellation);
        }
        double dt=1/(plan.Rate.FramesPerSecond*TemporalRetargetSolver.Substeps);
        double velocity=TemporalRetargetSolver.VelocitySecondsSquared/(dt*dt),acceleration=TemporalRetargetSolver.AccelerationSecondsFourth/Math.Pow(dt,4);
        double[] Multiply(double[] x)
        {
            var y=x.ToArray();
            for(int f=1;f<count;f++){double d=velocity*(x[f]-x[f-1]);y[f]+=d;y[f-1]-=d;}
            for(int f=2;f<count;f++){double d=acceleration*(x[f]-2*x[f-1]+x[f-2]);y[f]+=d;y[f-1]-=2*d;y[f-2]+=d;}
            return y;
        }
        var guide=new Vector3[count];
        for(int axis=0;axis<3;axis++)
        {
            cancellation.ThrowIfCancellationRequested();var values=TemporalRetargetSolver.ConjugateGradient(desired.Select(v=>(double)v[axis]).ToArray(),Multiply,Math.Min(128,count*2));
            for(int f=0;f<count;f++)guide[f][axis]=(float)values[f];
        }
        return plan with{BodyGuide=guide};
    }
    static Vector3 Translation(TargetMotionPlan plan,int body,Transform[] local,ContactPin[] pins,double frame,CancellationToken cancellation)
    {
        var scene=plan.Rig.Scene;float length=plan.Rig.CharacteristicLength;
        var transforms=local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t);
        var initial=AnimationService.Evaluate(scene,null,0,transforms);
        int parent=scene.Bones[body].Parent;var inverse=Matrix4x4.Identity;
        if(parent>=0&&!Matrix4x4.Invert(initial.World[parent],out inverse))
            throw new NotSupportedException("A singular animated parent prevents support-relative body translation.");
        double[] Residual(Vector3 normalizedShift)
        {
            // Evaluate real skin weights and attached reference frames. A
            // patch partly influenced by an actor ancestor need not move by
            // the full body displacement, and a self-contact target may move
            // with the body. Neither is assumed to be a rigid world point.
            var shift=Vector3.TransformNormal(normalizedShift*length,inverse);
            transforms[body]=local[body] with{Translation=local[body].Translation+shift};
            var pose=AnimationService.Evaluate(scene,null,0,transforms) with{Seconds=frame/plan.Rate.FramesPerSecond};
            var values=new List<double>(pins.Length*3);
            void Add(Vector3 value){value/=length;values.Add(value.X);values.Add(value.Y);values.Add(value.Z);}
            foreach(var pin in pins)
            {
                Add(ContactGeometry.Residual(pin,pose));
                // An exact anchor and its allowed region are separate
                // requirements. Their signed errors must never cancel.
                if(pin.AllowedSurface is not null&&ContactGeometry.Normal(pin,pose) is null)
                    Add(ContactGeometry.AllowedSurfaceResidual(pin,pose));
            }
            if(pins.Length==0&&plan.HasGround(frame)&&plan.Clearance is {} plane)
            {
                // Exact relationships determine the body reference first.
                // Do not move their translation compromise to satisfy a
                // conflicting clearance preference. The articulated solve
                // still enforces BOTH constraints independently afterward.
                // A collision/support relationship is real even when no
                // inferred observation qualifies as an authored point pin.
                // Use actual target skin and the supplied surface. Its finite
                // edge permits below-top hanging outside the solid; an
                // airborne pose already clear of it receives no floor shift.
                double penetration=plan.Rig.SkinPoints.Select(p=>(double)plane.Surface.Penetration(ContactGeometry.Point(p,pose))).DefaultIfEmpty(0).Max();
                values.Add(penetration/length);
                if(plan.Grounding is {} ground&&SurfaceContacts.BodySupportedAt(ground.BodySupportPhases,ground.BodySupportedThroughout,ground.BodySupportedAtClosing,plan.SourceFrame(frame),plan.AuthenticSourceSamples))
                {
                    if(plan.BodySupportPoints.Length==0)throw new NotSupportedException("Body support needs reviewed target surface geometry.");
                    if(plane.Edge is not null)Add(plan.BodySupportPoints.Select(p=>plane.Surface.TopOffset(ContactGeometry.Point(p,pose))).MinBy(v=>v.LengthSquared()));
                    else values.Add((plan.BodySupportPoints.Min(p=>Vector3.Dot(ContactGeometry.Point(p,pose),plane.Normal))-plane.Height)/length);
                }
            }
            return values.ToArray();
        }
        var current=Vector3.Zero;var residual=Residual(current);
        double cost=residual.Sum(x=>x*x);
        for(int iteration=0;iteration<TranslationIterations;iteration++)
        {
            cancellation.ThrowIfCancellationRequested();
            var columns=new double[3][];
            for(int axis=0;axis<3;axis++)
            {
                var shifted=current;shifted[axis]+=DifferenceStep;
                var next=Residual(shifted);
                columns[axis]=next.Zip(residual,(a,b)=>(a-b)/DifferenceStep).ToArray();
            }
            var matrix=new double[3,3];var right=new double[3];
            for(int a=0;a<3;a++)
            {
                right[a]=-columns[a].Zip(residual,(x,y)=>x*y).Sum();
                for(int b=0;b<3;b++)matrix[a,b]=columns[a].Zip(columns[b],(x,y)=>x*y).Sum();
            }
            var step=ObservableStep(matrix,right);
            if(!PoseGeometry.Finite(step))throw new InvalidDataException("Body-reference preparation produced a nonfinite translation.");
            bool accepted=false;
            for(float fraction=1;fraction>=1f/2048;fraction*=.5f)
            {
                var trial=current+step*fraction;var next=Residual(trial);double nextCost=next.Sum(x=>x*x);
                if(nextCost>=cost-Math.Max(1e-14,cost*1e-10))continue;
                current=trial;residual=next;cost=nextCost;accepted=true;break;
            }
            if(!accepted)break;
        }
        return current*length;
    }
    static Vector3 ObservableStep(double[,] matrix,double[] right)
    {
        // Three-dimensional symmetric eigensolve. Minimum-norm translation in
        // the observable span prevents roundoff on a free surface-tangent
        // direction from being amplified by tiny damping into body drift.
        var vectors=new double[3,3];for(int axis=0;axis<3;axis++)vectors[axis,axis]=1;
        for(int iteration=0;iteration<24;iteration++)
        {
            int p=0,q=1;
            for(int a=0;a<3;a++)for(int b=a+1;b<3;b++)
                if(Math.Abs(matrix[a,b])>Math.Abs(matrix[p,q])){p=a;q=b;}
            double diagonal=Math.Max(Math.Abs(matrix[0,0]),Math.Max(Math.Abs(matrix[1,1]),Math.Abs(matrix[2,2])));
            if(Math.Abs(matrix[p,q])<=Math.Max(1e-30,diagonal*1e-14))break;
            double tau=(matrix[q,q]-matrix[p,p])/(2*matrix[p,q]);
            double tangent=(tau>=0?1:-1)/(Math.Abs(tau)+Math.Sqrt(1+tau*tau));
            double cosine=1/Math.Sqrt(1+tangent*tangent),sine=tangent*cosine;
            double off=matrix[p,q];matrix[p,p]-=tangent*off;matrix[q,q]+=tangent*off;matrix[p,q]=matrix[q,p]=0;
            for(int axis=0;axis<3;axis++)
            {
                if(axis!=p&&axis!=q)
                {
                    double a=matrix[axis,p],b=matrix[axis,q];
                    matrix[axis,p]=matrix[p,axis]=cosine*a-sine*b;
                    matrix[axis,q]=matrix[q,axis]=sine*a+cosine*b;
                }
                double vp=vectors[axis,p],vq=vectors[axis,q];
                vectors[axis,p]=cosine*vp-sine*vq;vectors[axis,q]=sine*vp+cosine*vq;
            }
        }
        double largest=Math.Max(matrix[0,0],Math.Max(matrix[1,1],matrix[2,2]));
        double cutoff=largest*RelativeSingularResolution*RelativeSingularResolution;
        var result=new double[3];
        for(int column=0;column<3;column++)
        {
            double eigenvalue=matrix[column,column];if(eigenvalue<=cutoff||eigenvalue<=0)continue;
            double projected=0;for(int axis=0;axis<3;axis++)projected+=vectors[axis,column]*right[axis];
            projected/=eigenvalue+Damping;
            for(int axis=0;axis<3;axis++)result[axis]+=vectors[axis,column]*projected;
        }
        return new((float)result[0],(float)result[1],(float)result[2]);
    }
}
