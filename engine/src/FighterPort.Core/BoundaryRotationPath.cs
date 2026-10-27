using System.Numerics;

namespace FighterPort.Core;

// Offline orientation-aware choice among native XYZ Hermite paths. No world
// matrix decomposition: native binds, negative/zero scale and hierarchy stay owned.
// Compare physical angular travel, not component distance, across equivalent
// XYZ endpoint families. Original native keys/tangents stay exact.
internal static class BoundaryRotationPath
{
    internal const string Version="native-orientation-path-1";
    internal sealed class Budget { public long Witnesses; public double Travel,LegacyTravel; }
    readonly record struct V(double X,double Y,double Z)
    {
        public static V operator +(V a,V b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static V operator -(V a,V b)=>new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static V operator *(V a,double s)=>new(a.X*s,a.Y*s,a.Z*s);
        public double Length=>Math.Sqrt(X*X+Y*Y+Z*Z);
    }
    const double Rad=Math.PI/180;
    static double Speed(V e,V slope)
    {
        // Body angular speed for native Rz*Ry*Rx quaternion composition.
        double x=slope.X*Rad,y=slope.Y*Rad,z=slope.Z*Rad;
        return Math.Sqrt(Math.Max(0,x*x+y*y+z*z-2*x*z*Math.Sin(e.Y*Rad)));
    }
    internal static (Vector3 Values,bool Alternate) Choose(Vector3 reference,Vector3 join,Vector3 referenceSlope,Vector3 joinSlope,int span,bool entry,Budget budget)
    {
        V r=new(reference.X,reference.Y,reference.Z),j=new(join.X,join.Y,join.Z),rs=new(referenceSlope.X,referenceSlope.Y,referenceSlope.Z),js=new(joinSlope.X,joinSlope.Y,joinSlope.Z);
        if(new[]{r,j,rs,js}.Any(v=>!double.IsFinite(v.X)||!double.IsFinite(v.Y)||!double.IsFinite(v.Z)))throw new InvalidDataException("Native rotation paths require finite poses and tangents.");
        double Near(double v,double toward)=>v+360*Math.Round((toward-v)/360,MidpointRounding.ToEven);
        V Primary(bool other)=>other?new(Near(r.X+180,j.X),Near(180-r.Y,j.Y),Near(r.Z+180,j.Z)):new(Near(r.X,j.X),Near(r.Y,j.Y),Near(r.Z,j.Z));
        V primary=Primary(false),secondary=Primary(true);
        bool legacyOther=(secondary-j).Length<(primary-j).Length;
        V nearest=legacyOther?secondary:primary;
        if((nearest-j).Length<1e-8&&rs.Length==0&&js.Length==0)return(new((float)nearest.X,(float)nearest.Y,(float)nearest.Z),legacyOther);
        int steps=Math.Max(64,span*16);
        budget.Witnesses+=55L*(steps+1);
        if(budget.Witnesses>2000000)throw new NotSupportedException("Smooth rotation review exceeds its two-million-evaluation budget. Reduce the fit ranges or selected bones.");
        // Simpson integration of |body angular velocity| in native frame units.
        // It observes the exact Hermite curve (including the authored join side),
        // never a quaternion curve which integer native keys cannot reproduce.
        (double Travel,double Peak) Measure(V candidate,bool alternate)
        {
            V slope=alternate?new(rs.X,-rs.Y,rs.Z):rs;
            V a=entry?candidate:j,b=entry?j:candidate,da=entry?slope:js,db=entry?js:slope;
            double sum=0,peak=0;
            for(int i=0;i<=steps;i++)
            {
                double u=i/(double)steps,u2=u*u,u3=u2*u;
                V e=a*(2*u3-3*u2+1)+da*(span*(u3-2*u2+u))+b*(-2*u3+3*u2)+db*(span*(u3-u2));
                V d=(a*(6*u2-6*u)+b*(-6*u2+6*u))*(1d/span)+da*(3*u2-4*u+1)+db*(3*u2-2*u);
                double speed=Speed(e,d);peak=Math.Max(peak,speed);
                sum+=(i==0||i==steps?1:i%2==0?2:4)*speed;
            }
            double result=sum*span/(3d*steps)/Rad;
            if(!double.IsFinite(result))throw new InvalidDataException("Native rotation path has nonfinite angular travel.");
            return(result,peak/Rad);
        }
        V best=legacyOther?secondary:primary;bool bestOther=legacyOther;
        var baseline=Measure(best,bestOther);
        double legacy=baseline.Travel,bestTravel=legacy,bestDistance=(best-j).Length;
        // Complete coupled families and neighboring whole turns. Fixed order,
        // physical-travel ties retain the nearest original family deterministically.
        foreach(bool alternate in new[]{false,true})
        {
            V basis=Primary(alternate);
            foreach(int x in new[]{0,-1,1})foreach(int y in new[]{0,-1,1})foreach(int z in new[]{0,-1,1})
            {
                V candidate=basis+new V(x*360,y*360,z*360);
                var measured=Measure(candidate,alternate);double travel=measured.Travel,distance=(candidate-j).Length;
                if(measured.Peak<=baseline.Peak+.0001&&(travel<bestTravel-1e-5||Math.Abs(travel-bestTravel)<=1e-5&&distance<bestDistance-1e-5))
                {best=candidate;bestOther=alternate;bestTravel=travel;bestDistance=distance;}
            }
        }
        budget.Travel+=bestTravel;budget.LegacyTravel+=legacy;
        var value=new Vector3((float)best.X,(float)best.Y,(float)best.Z);
        if(!float.IsFinite(value.X)||!float.IsFinite(value.Y)||!float.IsFinite(value.Z))throw new InvalidDataException("Selected native rotation is outside the channel range.");
        return(value,bestOther);
    }
    internal static void Refine(Curve?[] channels,int from,int to,Budget budget)
    {
        if(to-from<2)return;
        if(channels.Skip(3).Take(3).Any(c=>c!.Keys.Where(k=>k.Frame==from).Select(k=>k.Value).Distinct().Count()>1||c.Keys.Where(k=>k.Frame==to).Select(k=>k.Value).Distinct().Count()>1))return;
        int middle=from+(to-from)/2,span=to-from,steps=Math.Max(64,span*16);
        budget.Witnesses+=steps+1;
        if(budget.Witnesses>2000000)throw new NotSupportedException("Smooth rotation review exceeds its two-million-evaluation budget. Reduce the fit ranges or selected bones.");
        Vector3 At(double t)=>new(channels[3]!.Sample(t),channels[4]!.Sample(t),channels[5]!.Sample(t));
        Quaternion Orientation(Vector3 e)=>Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(new Transform(Vector3.One,e,Vector3.Zero).Matrix));
        var qa=Orientation(At(from));var qb=Orientation(At(to));var near=At(middle);
        Vector3 Guide(double t)
        {
            float u=(float)((t-from)/span),s=u*u*(3-2*u);
            return AnimationControls.Euler(Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(qa,qb,s)),near);
        }
        var point=Guide(middle);const double h=.001;
        var slope=(Guide(middle+h)-Guide(middle-h))*(float)(.5/h);
        if(new[]{point.X,point.Y,point.Z,slope.X,slope.Y,slope.Z}.Any(v=>!float.IsFinite(v)))return;
        (double Travel,double Peak) Measure(Curve?[] candidate)
        {
            double sum=0,peak=0;
            for(int i=0;i<=steps;i++)
            {
                double t=from+span*i/(double)steps;
                V e=new(candidate[3]!.Sample(t),candidate[4]!.Sample(t),candidate[5]!.Sample(t));
                V d=new(BoundaryFitting.Derivative(candidate[3],t),BoundaryFitting.Derivative(candidate[4],t),BoundaryFitting.Derivative(candidate[5],t));
                // At the right endpoint measure the interval's incoming side.
                if(i==steps)d=new(BoundaryFitting.Derivative(candidate[3],t,false),BoundaryFitting.Derivative(candidate[4],t,false),BoundaryFitting.Derivative(candidate[5],t,false));
                double speed=Speed(e,d)/Rad;peak=Math.Max(peak,speed);
                sum+=(i==0||i==steps?1:i%2==0?2:4)*speed;
            }
            return(sum*span/(3d*steps),peak);
        }
        var baseline=Measure(channels);var best=channels;var bestMeasure=baseline;
        double geodesic=2*Math.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(qa,qb)),0,1))/Rad;
        if(baseline.Travel<=geodesic+.001)return;
        budget.Witnesses+=54L*(steps+1);
        if(budget.Witnesses>2000000)throw new NotSupportedException("Smooth rotation review exceeds its two-million-evaluation budget. Reduce the fit ranges or selected bones.");
        float Near(double v,float target)=>(float)(v+360*Math.Round((target-v)/360,MidpointRounding.ToEven));
        foreach(bool alternate in new[]{false,true})
        {
            Vector3 basis=alternate?new(Near(point.X+180d,near.X),Near(180d-point.Y,near.Y),Near(point.Z+180d,near.Z)):new(Near(point.X,near.X),Near(point.Y,near.Y),Near(point.Z,near.Z));
            var derivative=alternate?new Vector3(slope.X,-slope.Y,slope.Z):slope;
            foreach(int x in new[]{0,-1,1})foreach(int y in new[]{0,-1,1})foreach(int z in new[]{0,-1,1})
            {
                var value=basis+new Vector3(x*360,y*360,z*360);var candidate=channels.ToArray();
                for(int c=3;c<6;c++)candidate[c]=channels[c]!.Put(new(middle,value[c-3],derivative[c-3],derivative[c-3]));
                var measured=Measure(candidate);
                if(double.IsFinite(measured.Travel)&&double.IsFinite(measured.Peak)&&measured.Travel<bestMeasure.Travel-.001&&measured.Peak<=baseline.Peak+.0001){best=candidate;bestMeasure=measured;}
            }
        }
        if(!ReferenceEquals(best,channels))
        {
            for(int c=3;c<6;c++)channels[c]=best[c];
            budget.Travel+=bestMeasure.Travel-baseline.Travel;
        }
    }
}
