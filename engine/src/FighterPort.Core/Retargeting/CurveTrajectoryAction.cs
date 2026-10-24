using System.Numerics;

namespace FighterPort.Core;

public record CurveActionReceipt(string Version,int PoseEvaluations,int AcceptedUpdates,int RestorationUpdates,bool BudgetExhausted,
    double AcceptedWindowActionReduction,string GeometryScope)
{
    public int Sweeps {get;init;}
}
internal record CurveActionFit(BoneTrack[] Tracks,CurveActionReceipt Receipt);

// Offline optimization of connected destination-curve coefficients. A proposal
// changes four keys and their shared derivatives. Its entire affected halo is
// restored and tested before publication; independently solved poses are never
// blended back into the result.
internal static class CurveTrajectoryAction
{
    public const string Version="source-priority-skin-path-observation-hermite-5";
    public const double CorrectionWeight=.01,VelocityWeight=.00005,AccelerationWeight=.00000001;
    const double Difference=.001,MaximumStep=.1;
    static readonly double[][] Hermite=[[1,0,-3,2],[0,1,-2,1],[0,0,3,-2],[0,0,-1,1]];
    sealed class BudgetReached:Exception {}
    readonly record struct Channel(int Track,int Bone,int Index);
    readonly record struct Parameter(Channel Channel,int Key,bool Value);
    public static CurveActionFit Refine(TargetMotionPlan plan,BoneTrack[] input,BoneTrack[] baseline,
        CancellationToken cancellation,int evaluationBudget)
    {
        int evaluations=0,updates=0,restorations=0,sweeps=0;var current=input;
        double actionReduction=0;
        string scope="All retained native skin; prepared skeletal geometry, no dynamic visibility mask";
        CurveActionFit Result(bool exhausted)=>new(current,new(Version,evaluations,updates,restorations,exhausted,actionReduction,scope){Sweeps=sweeps});
        void Charge(){cancellation.ThrowIfCancellationRequested();if(evaluations>=evaluationBudget)throw new BudgetReached();evaluations++;}
        var scene=plan.Rig.Scene;int keys=input[0].Channels[0]!.Keys.Length;
        if(keys<2||evaluationBudget<1)return Result(evaluationBudget<1);
        int root=plan.Rig.Binding.Roles["RootMotion"],body=plan.Rig.Binding.Roles.GetValueOrDefault("Hips",-1);
        var preserved=plan.Controls.PreservedHelperBones.ToHashSet();float scale=plan.Rig.CharacteristicLength;
        var eligible=new HashSet<int>();
        void Include(int bone){for(;bone>=0&&bone!=root;bone=scene.Bones[bone].Parent)if(!preserved.Contains(bone)&&!plan.Rig.IsActorAncestor(bone))eligible.Add(bone);}
        foreach(var pair in plan.Baseline.Mapping.Bones)Include(pair.Target);
        foreach(var pin in plan.Graph.Relations.SelectMany(c=>c.Pins))Include(pin.Bone);
        var channels=eligible.OrderBy(b=>plan.Rig.CanonicalBoneRanks[b]).SelectMany(b=>Enumerable.Range(3,b==body?6:3)
            .Select(ch=>new Channel(Array.FindIndex(input,t=>t.Bone==scene.Bones[b].Name),b,ch))).Where(c=>c.Track>=0).ToArray();
        if(channels.Length==0)return Result(false);
        var template=new AnimationClip(Guid.NewGuid(),"Prepared trajectory evaluation",plan.Samples,plan.Rate,plan.Loop,input);
        EvaluatedPose Evaluate(BoneTrack[] tracks,double frame){Charge();return AnimationService.Evaluate(scene,template with{Tracks=tracks},frame) with{Seconds=frame/plan.Rate.FramesPerSecond};}
        var actions=new Dictionary<double,PreparedTrajectoryAction>();
        PreparedTrajectoryAction Action(double frame)
        {
            if(actions.TryGetValue(frame,out var prepared))return prepared;
            Charge();var local=plan.Reference(frame);
            var reference=AnimationService.Evaluate(scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
            prepared=PreparedTrajectoryAction.Create(plan,frame,reference);actions.Add(frame,prepared);scope=prepared.VisibilityPolicy;
            return prepared;
        }
        static double Coefficient(Parameter p,double frame,int count)
        {
            int segment=Math.Min((int)frame,count-2);double u=frame-segment;
            if(p.Key==segment)return p.Value?2*u*u*u-3*u*u+1:u*u*u-2*u*u+u;
            if(p.Key==segment+1)return p.Value?-2*u*u*u+3*u*u:u*u*u-u*u;
            return 0;
        }
        BoneTrack[] Changed(Parameter[] parameters,double[] delta,double fraction)
        {
            var tracks=current.ToArray();
            foreach(var track in parameters.Select((p,i)=>(P:p,I:i)).GroupBy(p=>p.P.Channel.Track))
            {
                var curves=current[track.Key].Channels.ToArray();
                foreach(var channel in track.GroupBy(p=>p.P.Channel.Index))
                {
                    var curve=curves[channel.Key]!;var values=curve.Keys.ToArray();
                    foreach(var p in channel)
                    {
                        float value=(float)(fraction*delta[p.I]*(channel.Key>=6?scale:180/Math.PI));var key=values[p.P.Key];
                        values[p.P.Key]=p.P.Value?key with{Value=key.Value+value}:key with{InTangent=key.InTangent+value,OutTangent=key.OutTangent+value};
                    }
                    curves[channel.Key]=curve with{Keys=values};
                }
                tracks[track.Key]=tracks[track.Key] with{Channels=curves};
            }
            return tracks;
        }
        double[,] Gram()
        {
            var gram=new double[4,4];double fps=plan.Rate.FramesPerSecond;
            for(int a=0;a<4;a++)for(int b=0;b<4;b++)for(int derivative=0;derivative<3;derivative++)
            {
                double weight=derivative==0?CorrectionWeight/fps:derivative==1?VelocityWeight*fps:AccelerationWeight*fps*fps*fps;
                for(int i=derivative;i<4;i++)for(int j=derivative;j<4;j++)
                {
                    double x=Hermite[a][i],y=Hermite[b][j];for(int k=0;k<derivative;k++){x*=i-k;y*=j-k;}
                    gram[a,b]+=weight*x*y/(i+j-2*derivative+1);
                }
            }
            return gram;
        }
        var gram=Gram();
        double Regularization(BoneTrack[] tracks,int first,int last)
        {
            double result=0;
            foreach(var channel in channels)for(int segment=first;segment<=last;segment++)
            {
                double unit=channel.Index>=6?scale:180/Math.PI;
                var curve=tracks[channel.Track].Channels[channel.Index]!;var original=baseline[channel.Track].Channels[channel.Index]!;
                var a=curve.Keys[segment];var b=curve.Keys[segment+1];var x=original.Keys[segment];var y=original.Keys[segment+1];
                double[] q=[(a.Value-x.Value)/unit,(a.OutTangent-x.OutTangent)/unit,(b.Value-y.Value)/unit,(b.InTangent-y.InTangent)/unit];
                for(int i=0;i<4;i++)for(int j=0;j<4;j++)result+=q[i]*gram[i,j]*q[j];
            }
            return result;
        }
        bool Limits(IEnumerable<EvaluatedPose> poses)=>poses.All(p=>(plan.Controls.JointLimits??[]).All(l=>Enumerable.Range(0,3).All(a=>p.Local[l.Bone].Rotation[a]>=l.MinimumDegrees[a]-.0001f&&p.Local[l.Bone].Rotation[a]<=l.MaximumDegrees[a]+.0001f)));
        try
        {
            for(int sweep=0;sweep<16;sweep++)
            {
                sweeps=sweep+1;
                bool changed=false;
                // Canonical deterministic traversal. Each window contains all
                // joints' shared coefficients, with fixed outside boundaries.
                for(int segment=0;segment<keys-1;segment++)
                {
                    int firstKey=Math.Max(0,segment-1),lastKey=Math.Min(keys-1,segment+2);
                    int first=Math.Max(0,firstKey-1),last=Math.Min(keys-2,lastKey);
                    var boundaries=plan.Graph.Relations.SelectMany(c=>new[]{c.Frame,c.EndFrame??c.Frame})
                        .SelectMany(f=>new[]{f-1d/1024,f,f+1d/1024}).Where(f=>f>=first&&f<=last+1);
                    var frames=Enumerable.Range(0,(last-first+1)*8+1).Select(i=>first+i/8d).Concat(boundaries).Distinct().Order().ToArray();
                    var poses=frames.Select(f=>Evaluate(current,f)).ToArray();var objectives=frames.Select(Action).ToArray();
                    double weight=1d/(8*plan.Rate.FramesPerSecond);
                    double oldAction=poses.Select((p,i)=>objectives[i].Cost(p)).Sum()*weight;
                    double oldGeometry=poses.Select((p,i)=>objectives[i].GeometricCost(p)).Sum()*weight;
                    // A source-derived window already at floating-point action
                    // resolution needs no finite-difference search. This is a
                    // work guard only; independent constraints/native checks
                    // still run, and it does not mark coverage or feasibility.
                    if(oldAction<=1e-14*frames.Length*weight)continue;
                    var parameters=Enumerable.Range(firstKey,lastKey-firstKey+1).SelectMany(k=>channels.SelectMany(c=>new[]{new Parameter(c,k,true),new Parameter(c,k,false)})).ToArray();
                    var gradient=new double[parameters.Length];var diagonal=new double[parameters.Length];
                    for(int f=0;f<frames.Length;f++)
                    {
                        var residual=objectives[f].Residuals(poses[f]);
                        foreach(var channel in channels)
                        {
                            var local=poses[f].Local.ToArray();var t=local[channel.Bone];
                            if(channel.Index>=6){var p=t.Translation;p[channel.Index-6]+=(float)(Difference*scale);local[channel.Bone]=t with{Translation=p};}
                            else{var r=t.Rotation;r[channel.Index-3]+=(float)(Difference*180/Math.PI);local[channel.Bone]=t with{Rotation=r};}
                            Charge();var shifted=AnimationService.Evaluate(scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t)) with{Seconds=frames[f]/plan.Rate.FramesPerSecond};
                            var plus=objectives[f].Residuals(shifted);double g=0,h=0;
                            for(int row=0;row<residual.Length;row++){double d=(plus[row]-residual[row])/Difference;g+=2*residual[row]*d;h+=2*d*d;}
                            for(int j=0;j<parameters.Length;j++)if(parameters[j].Channel==channel)
                            {double c=Coefficient(parameters[j],frames[f],keys);gradient[j]+=weight*c*g;diagonal[j]+=weight*c*c*h;}
                        }
                    }
                    // Exact continuous-time Gram integrals keep correction
                    // velocity/acceleration in seconds, independent of FPS.
                    foreach(var channel in channels)for(int s=first;s<=last;s++)
                    {
                        double unit=channel.Index>=6?scale:180/Math.PI;
                        var curve=current[channel.Track].Channels[channel.Index]!;var original=baseline[channel.Track].Channels[channel.Index]!;
                        double[] q=[(curve.Keys[s].Value-original.Keys[s].Value)/unit,(curve.Keys[s].OutTangent-original.Keys[s].OutTangent)/unit,
                            (curve.Keys[s+1].Value-original.Keys[s+1].Value)/unit,(curve.Keys[s+1].InTangent-original.Keys[s+1].InTangent)/unit];
                        for(int j=0;j<parameters.Length;j++)if(parameters[j].Channel==channel)
                        {
                            int component=parameters[j].Key==s?(parameters[j].Value?0:1):parameters[j].Key==s+1?(parameters[j].Value?2:3):-1;
                            if(component<0)continue;
                            for(int k=0;k<4;k++)gradient[j]+=2*gram[component,k]*q[k];diagonal[j]+=2*gram[component,component];
                        }
                    }
                    var direction=gradient.Select((g,i)=>-g/Math.Max(diagonal[i],1e-8)).ToArray();
                    double maximum=direction.Select(Math.Abs).DefaultIfEmpty(0).Max();if(maximum<1e-7)continue;
                    if(maximum>MaximumStep)for(int j=0;j<direction.Length;j++)direction[j]*=MaximumStep/maximum;
                    var before=frames.Select((f,i)=>TargetMotionConstraints.MaximumNormalizedViolation(plan,f,poses[i])).ToArray();
                    double worst=Math.Max(1,before.Max()),oldRegularization=Regularization(current,first,last);
                    for(double fraction=1;fraction>=1d/64;fraction*=.5)
                    {
                        var trial=Changed(parameters,direction,fraction);
                        int remaining=evaluationBudget-evaluations;if(remaining<frames.Length*2)throw new BudgetReached();
                        // Restore the SAME polynomial coefficients, retaining
                        // the outside halo. No contact interval or anchor moves.
                        var restored=NativeCurveProjection.Refine(plan,trial,cancellation,Math.Min(remaining/2,12000),segment,2,preferred:baseline);
                        evaluations+=restored.Receipt.PoseEvaluations;restorations+=restored.Receipt.AcceptedUpdates;
                        trial=restored.Tracks;var candidates=frames.Select(f=>Evaluate(trial,f)).ToArray();
                        if(!Limits(candidates))continue;
                        var violations=frames.Select((f,i)=>TargetMotionConstraints.MaximumNormalizedViolation(plan,f,candidates[i])).ToArray();
                        if(violations.Any(v=>!double.IsFinite(v))||violations.Max()>worst+1e-9||violations.Where((v,i)=>before[i]<=1&&v>1).Any())continue;
                        double nextAction=candidates.Select((p,i)=>objectives[i].Cost(p)).Sum()*weight;
                        double nextGeometry=candidates.Select((p,i)=>objectives[i].GeometricCost(p)).Sum()*weight;
                        // Optional observations may improve alongside the
                        // action, or in its free directions (such as permitted
                        // body translation). They cannot pay for increased
                        // source-relative shape/orientation distortion.
                        if(nextGeometry>oldGeometry+1e-12)continue;
                        if(nextAction>=oldAction-1e-12||nextAction+Regularization(trial,first,last)>=oldAction+oldRegularization-1e-12)continue;
                        current=trial;updates++;changed=true;actionReduction+=oldAction-nextAction;break;
                    }
                }
                if(!changed)break;
            }
        }
        catch(BudgetReached){return Result(true);}
        return Result(false);
    }
}
