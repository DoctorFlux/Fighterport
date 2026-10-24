using System.Numerics;

namespace FighterPort.Core;

// Offline projection of shared native Hermite derivatives. Integer poses,
// exterior keys, native timebase and authored anchors are fixed throughout.
internal static class ContactCurveProjection
{
    internal const int EvaluationBudget=30000,MaximumSweeps=16;
    internal static (int Evaluations,int Sweeps) Refine(Scene scene,AnimationClip clip,BoneTrack[] changed,ContactFrame[] contacts,ControlRigSetup rig,int begin,int end,float positionBound,float angleBound)
    {
        int evaluations=0,sweeps=0;float angleRadians=angleBound*MathF.PI/180;
        var probes=Enumerable.Range(begin*4,(end-begin)*4+1).Where(i=>i%4!=0).Select(i=>i*.25).Select(f=>(Frame:f,Pins:contacts.Where(c=>c.Contains(f)).SelectMany(c=>c.Pins).ToArray())).Where(p=>p.Pins.Length>0).ToArray();
        if(probes.Length==0)return(0,0);
        var curves=changed.SelectMany(t=>Enumerable.Range(3,3).Select(c=>t.Channels[c]!)).ToArray();
        var knots=Enumerable.Range(begin+1,end-begin-1).Where(frame=>probes.Any(p=>Math.Abs(p.Frame-frame)<1)).ToArray();
        double[] Residual((double Frame,ContactPin[] Pins)[] selected)
        {
            var result=new List<double>();
            foreach(var probe in selected)
            {
                if(++evaluations>EvaluationBudget)throw new InvalidOperationException("Native contact tangent projection exhausted its fixed evaluation budget; no result was applied.");
                var pose=AnimationService.Evaluate(scene,clip,probe.Frame);
                foreach(var pin in probe.Pins)
                {
                    var goal=pin.EditorMotion?.Resolve(scene,pin,probe.Frame,clip.Rate)??pin;
                    var delta=(Vector3.Transform(goal.LocalPoint,pose.World[goal.Bone])-goal.Position)/positionBound;result.AddRange([delta.X,delta.Y,delta.Z]);
                    if(goal.HoldRotation){var q=Quaternion.Normalize(Quaternion.Concatenate(Quaternion.Inverse(goal.Rotation),PoseGeometry.Rotation(pose.World[goal.Bone])));float sign=(q.W<0?-2:2)/angleRadians;result.AddRange([q.X*sign,q.Y*sign,q.Z*sign]);}
                }
            }
            return result.ToArray();
        }
        double Cost(double[] values)=>values.Sum(v=>v*v);
        bool Accepted()
        {
            foreach(var probe in probes){if(++evaluations>EvaluationBudget)return false;var pose=AnimationService.Evaluate(scene,clip,probe.Frame);foreach(var pin in probe.Pins){var goal=pin.EditorMotion?.Resolve(scene,pin,probe.Frame,clip.Rate)??pin;if(Vector3.Distance(Vector3.Transform(goal.LocalPoint,pose.World[goal.Bone]),goal.Position)>positionBound||goal.HoldRotation&&ContactAuthoring.AngleError(PoseGeometry.Rotation(pose.World[goal.Bone]),goal.Rotation)>angleBound)return false;}}return true;
        }
        for(;sweeps<MaximumSweeps&&!Accepted();sweeps++)
        {
            bool improved=false;
            for(int block=0;block<knots.Length;block+=3)
            {
                var frames=knots.Skip(block).Take(4).ToArray();var selected=probes.Where(p=>frames.Any(frame=>Math.Abs(p.Frame-frame)<1)).ToArray();
                var variables=frames.SelectMany(frame=>curves.Select(c=>(Curve:c,Index:Array.FindIndex(c.Keys,k=>k.Frame==frame)))).ToArray();if(variables.Any(v=>v.Index<0))throw new InvalidDataException("Missing owned contact derivative knot.");
                var basis=variables.Select(v=>v.Curve.Keys[v.Index]).ToArray();
                void Put(double[] increments){for(int i=0;i<variables.Length;i++){float delta=(float)(increments[i]*180/Math.PI);variables[i].Curve.Keys[variables[i].Index]=basis[i] with{InTangent=basis[i].InTangent+delta,OutTangent=basis[i].OutTangent+delta};}}
                int n=variables.Length;var zero=new double[n];var error=Residual(selected);var jac=new double[error.Length,n];const double epsilon=.001;
                for(int j=0;j<n;j++){var offset=new double[n];offset[j]=epsilon;Put(offset);var plus=Residual(selected);offset[j]=-epsilon;Put(offset);var minus=Residual(selected);for(int r=0;r<error.Length;r++)jac[r,j]=(plus[r]-minus[r])/(2*epsilon);}
                Put(zero);double cost=Cost(error);
                foreach(double damping in new[]{.00001,.0001,.001})
                {
                    var normal=new double[n,n];var rhs=new double[n];for(int j=0;j<n;j++){for(int k=0;k<n;k++)for(int r=0;r<error.Length;r++)normal[j,k]+=jac[r,j]*jac[r,k];normal[j,j]+=damping*damping;for(int r=0;r<error.Length;r++)rhs[j]-=jac[r,j]*error[r];}
                    var delta=ContactSolver.LinearSolve(normal,rhs);if(delta is null)continue;double maximum=delta.Select(Math.Abs).Max();if(maximum>.25)for(int j=0;j<n;j++)delta[j]*=.25/maximum;
                    bool accepted=false;foreach(double fraction in new[]{1d,.5,.25,.125}){Put(delta.Select(v=>v*fraction).ToArray());if(Cost(Residual(selected))<cost-1e-14){accepted=true;improved=true;break;}}
                    if(accepted)break;Put(zero);
                }
            }
            if(!improved)break;
        }
        return(evaluations,sweeps);
    }
}
