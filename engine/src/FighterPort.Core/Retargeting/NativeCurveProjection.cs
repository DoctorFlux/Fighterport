using System.Numerics;

namespace FighterPort.Core;

// Restore permitted integer values and shared Hermite slopes in the SAME
// destination representation. Four editable keys have immutable outer
// value/slope halos; every affected segment and integer is checked. Prepared
// poses are seeds, not extra constraints that prevent a representable fit.
// This bounded restoration is not a whole-clip action optimization or acceptance.
internal static class NativeCurveProjection
{
    static readonly double[] InteriorProbes=[.125,.25,.375,.5,.625,.75,.875];
    sealed class WorkBudgetReached:Exception {}
    readonly record struct Variable(int Key,int Track,int Bone,int Channel,bool Value);
    public static NativeCurveFit Refine(TargetMotionPlan plan,BoneTrack[] input,CancellationToken cancellation,int evaluationBudget,int? onlySegment=null,int maximumSweeps=8,int keyRadius=1,BoneTrack[]? preferred=null)
    {
        int evaluations=0,updates=0,sweeps=0,valueUpdates=0,immutableWindows=0,sharedBasisWindows=0;var current=input;
        var reasons=new Dictionary<string,int>(StringComparer.Ordinal);
        void Reason(string reason)=>reasons[reason]=reasons.GetValueOrDefault(reason)+1;
        NativeCurveFit Result(bool exhausted)=>new(current,new(NativeCurveBaker.Version,evaluations,evaluationBudget,updates,sweeps,exhausted){KeyValueUpdates=valueUpdates,ImmutableConstraintWindows=immutableWindows,SharedBasisWindows=sharedBasisWindows,RestorationReasons=reasons.Count==0?null:reasons});
        void Charge(){cancellation.ThrowIfCancellationRequested();if(evaluations>=evaluationBudget)throw new WorkBudgetReached();evaluations++;}
        int count=input[0].Channels[0]!.Keys.Length;if(count<2||plan.Graph.Relations.Length==0&&plan.Clearance is null)return Result(false);
        // Select active cuts from actual skin at every native window. Temporal
        // proxy capacity is not a lifetime cache for native interpolation cuts.
        // Jacobians still use a bounded active set; final acceptance checks all
        // vertices independently, including newly exposed violators.
        if(plan.Clearance is {} clearance)plan=plan with{Clearance=clearance with{Points=plan.Rig.SkinPoints}};
        var scene=plan.Rig.Scene;int root=plan.Rig.Binding.Roles["RootMotion"],body=plan.Rig.Binding.Roles.GetValueOrDefault("Hips",-1);var helpers=plan.Controls.PreservedHelperBones.ToHashSet();var bones=new SortedSet<int>();
        void Add(int bone){for(;bone>=0&&bone!=root;bone=scene.Bones[bone].Parent)if(!helpers.Contains(bone)&&!plan.Rig.IsActorAncestor(bone))bones.Add(bone);}
        var template=new AnimationClip(Guid.NewGuid(),"Native fit evaluation",plan.Samples,plan.Rate,plan.Loop,current);
        EvaluatedPose Evaluate(BoneTrack[] tracks,double frame){Charge();return AnimationService.Evaluate(scene,template with{Tracks=tracks},frame) with{Seconds=frame/plan.Rate.FramesPerSecond};}
        static double Cost(double[] r)=>r.Sum(x=>x*x);
        double Coefficient(Variable variable,double frame)
        {
            int start=Math.Min((int)frame,count-2);double u=frame-start;
            if(variable.Key==start)return variable.Value?2*u*u*u-3*u*u+1:u*u*u-2*u*u+u;
            if(variable.Key==start+1)return variable.Value?-2*u*u*u+3*u*u:u*u*u-u*u;
            return 0;
        }
        try
        {
        for(int sweep=0;sweep<maximumSweeps;sweep++)
        {
            sweeps=sweep+1;bool changed=false;
            // Spend bounded work across the action, starting at the largest
            // residual. A long first window must not starve every later frame.
            var segments=onlySegment is {} selected?new[]{selected}:Enumerable.Range(0,count-1);
            var order=segments.Select(segment=>(Segment:segment,Error:InteriorProbes.Prepend(0).Append(1).Max(u=>TargetMotionConstraints.MaximumNormalizedViolation(plan,segment+u,Evaluate(current,segment+u))))).OrderByDescending(s=>s.Error).ThenBy(s=>s.Segment).ToArray();
            foreach(var window in order)
            {
                int segment=window.Segment;
                cancellation.ThrowIfCancellationRequested();
                // Values and slopes affect the neighbouring segments. Keep
                // all their integer/eighth samples and actual relationship
                // boundaries, with explicit one-sided probes at 1/1024 frame.
                // No interval or contact is extended by adding these samples.
                int firstKey=Math.Max(0,segment-keyRadius),lastKey=Math.Min(count-1,segment+1+keyRadius);
                int first=Math.Max(0,firstKey-1),last=Math.Min(count-2,lastKey);
                var boundaries=plan.Graph.Relations.SelectMany(contact=>new[]{contact.Frame,contact.EndFrame??contact.Frame})
                    .SelectMany(frame=>new[]{frame-1d/1024,frame,frame+1d/1024}).Where(frame=>frame>=first&&frame<=last+1);
                var probes=Enumerable.Range(first,last-first+1).SelectMany(s=>InteriorProbes.Prepend(0).Append(1).Select(u=>s+u)).Concat(boundaries).Distinct().Order();
                var prepared=probes.Select(f=>(Frame:f,Pose:Evaluate(current,f))).ToArray();
                var windowPlan=plan;
                var samples=prepared.Select(s=>{var active=TargetMotionConstraints.ActiveClearance(windowPlan,s.Frame,s.Pose);return(s.Frame,s.Pose,Active:active,Pins:TargetMotionConstraints.LinearizationPins(windowPlan,s.Frame,s.Pose),Cuts:TargetMotionConstraints.LinearizationCuts(windowPlan,s.Pose,active),Body:TargetMotionConstraints.LinearizationBodyPoint(windowPlan,s.Frame,s.Pose));}).ToArray();
                if(samples.All(s=>TargetMotionConstraints.MaximumNormalizedViolation(windowPlan,s.Frame,s.Pose)<=.5))continue;
                // Select correction channels through actual skin influences in
                // this window; no fixed head/spine list or prescribed anatomy.
                bones.Clear();
                // A rolling relationship may select a different material point
                // after a correction. Retain all of its actual native skin
                // influences in the editable set, not just the current witness.
                foreach(var pin in samples.SelectMany(s=>windowPlan.Pins(s.Frame)).Where(p=>!p.SoftSupport||p.RequiredSupport))
                {
                    Add(pin.Bone);
                    foreach(var point in pin.SurfacePoints.Concat(pin.SurfaceRegion).Concat(pin.Reference?.SurfaceTripod??[]))for(int j=0;j<point.InfluenceCount;j++)if(point.WeightAt(j)>0)Add((int)point.JointAt(j));
                    if(pin.Reference?.Bone is {} attachment)Add(attachment);
                }
                foreach(var point in samples.Any(s=>s.Body is not null)?windowPlan.BodySupportPoints:[])for(int j=0;j<point.InfluenceCount;j++)if(point.WeightAt(j)>0)Add((int)point.JointAt(j));
                if(windowPlan.Clearance is {} plane)foreach(int index in samples.SelectMany(s=>s.Active).Distinct())for(int j=0;j<plane.Points[index].InfluenceCount;j++)if(plane.Points[index].WeightAt(j)>0)Add((int)plane.Points[index].JointAt(j));
                var editable=bones.OrderBy(b=>plan.Rig.CanonicalBoneRanks[b]).SelectMany(b=>Enumerable.Range(3,b==body?6:3).Select(ch=>(Bone:b,Channel:ch,Track:Array.FindIndex(input,t=>t.Bone==scene.Bones[b].Name)))).Where(v=>v.Track>=0).ToArray();
                var variables=Enumerable.Range(firstKey,lastKey-firstKey+1).SelectMany(key=>editable.SelectMany(v=>new[]{new Variable(key,v.Track,v.Bone,v.Channel,true),new Variable(key,v.Track,v.Bone,v.Channel,false)})).ToArray();
                if(variables.Length==0){immutableWindows++;continue;}
                var q=new double[variables.Length];float scale=plan.Rig.CharacteristicLength;
                EvaluatedPose At(double frame,Transform[] baseline,double[] values)
                {
                    Charge();var local=baseline.ToArray();
                    for(int j=0;j<values.Length;j++)
                    {
                        var v=variables[j];double coefficient=Coefficient(v,frame);if(coefficient==0)continue;var t=local[v.Bone];
                        if(v.Channel>=6){var p=t.Translation;p[v.Channel-6]+=(float)(coefficient*values[j]*scale);local[v.Bone]=t with{Translation=p};}
                        else{var r=t.Rotation;r[v.Channel-3]+=(float)(coefficient*values[j]*180/Math.PI);local[v.Bone]=t with{Rotation=r};}
                    }
                    return AnimationService.Evaluate(scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t)) with{Seconds=frame/plan.Rate.FramesPerSecond};
                }
                bool Limits(EvaluatedPose[] poses)=>(plan.Controls.JointLimits??[]).Length==0||poses.All(p=>plan.Controls.JointLimits!.All(l=>Enumerable.Range(0,3).All(a=>p.Local[l.Bone].Rotation[a]>=l.MinimumDegrees[a]-.0001f&&p.Local[l.Bone].Rotation[a]<=l.MaximumDegrees[a]+.0001f)));
                // Keep the derivative's active cuts fixed, but recompute the
                // lowest actual skin points for candidate acceptance. A tangent
                // must not appear better just by moving another vertex through
                // the support. This covers the full prepared skin per sample.
                double[] Merit(EvaluatedPose[] poses)=>samples.SelectMany((s,i)=>TargetMotionConstraints.Residual(windowPlan,s.Frame,poses[i],TargetMotionConstraints.ActiveClearance(windowPlan,s.Frame,poses[i]))).ToArray();
                BoneTrack[] Coefficients(double[] values)
                {
                    // Copy only changed channel arrays. Candidate acceptance
                    // samples these actual float32 coefficients, not the local
                    // linearization approximation used for Jacobians.
                    var tracks=current.ToArray();
                    foreach(var group in variables.Select((v,index)=>(Variable:v,Index:index)).GroupBy(v=>v.Variable.Track))
                    {
                        int track=group.Key;var channels=current[track].Channels.ToArray();
                        foreach(var channelGroup in group.GroupBy(v=>v.Variable.Channel))
                        {
                            int channel=channelGroup.Key;var curve=channels[channel]!;var keys=curve.Keys.ToArray();
                            foreach(var item in channelGroup)
                            {
                                var v=item.Variable;var key=keys[v.Key];float d=(float)(values[item.Index]*(channel>=6?scale:180/Math.PI));
                                keys[v.Key]=v.Value?key with{Value=key.Value+d}:key with{InTangent=key.InTangent+d,OutTangent=key.OutTangent+d};
                            }
                            channels[channel]=curve with{Keys=keys};
                        }
                        tracks[track]=tracks[track] with{Channels=channels};
                    }
                    return tracks;
                }
                // The artistic review threshold is NOT an anatomical joint
                // limit. Required contacts have priority here; geometric action
                // is optimized separately and the unchanged final review gate
                // still blocks an overly distorted result from Apply.
                var beforePoses=samples.Select(s=>s.Pose).ToArray();double initial=Cost(Merit(beforePoses));
                var beforeViolations=samples.Select(s=>TargetMotionConstraints.MaximumNormalizedViolation(windowPlan,s.Frame,s.Pose)).ToArray();
                double restorationBound=Math.Max(1,beforeViolations.Max());
                BoneTrack[]? retained=null;double retainedCost=initial;
                for(int iteration=0;iteration<3;iteration++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    // Freeze material witnesses/faces only for this Jacobian.
                    // Reusing a penetration cut after it cleared the surface
                    // turns a one-sided inequality into a false equality and
                    // asks non-coplanar body skin to flatten onto the floor.
                    // Rolling/body-support witnesses must follow the current
                    // evaluated geometry for the same reason. At() still uses
                    // the original window pose plus q, never q applied twice.
                    var linear=samples.Select(s=>
                    {
                        var pose=At(s.Frame,s.Pose.Local,q);var active=TargetMotionConstraints.ActiveClearance(windowPlan,s.Frame,pose);
                        return(s.Frame,Pose:pose,Active:active,Pins:TargetMotionConstraints.LinearizationPins(windowPlan,s.Frame,pose),Cuts:TargetMotionConstraints.LinearizationCuts(windowPlan,pose,active),Body:TargetMotionConstraints.LinearizationBodyPoint(windowPlan,s.Frame,pose));
                    }).ToArray();
                    var error=linear.SelectMany(s=>TargetMotionConstraints.Residual(windowPlan,s.Frame,s.Pose,s.Active,s.Pins,s.Cuts,s.Body)).ToArray();if(error.All(r=>Math.Abs(r)<=.4))break;
                    // A larger native graph need not lose its entire window
                    // merely because independent value/slope columns exceed
                    // the dense budget. Assemble the existing shared-value
                    // temporal basis directly, without allocating those fine
                    // columns first. Outside halos and every candidate skin
                    // probe retain the identical acceptance contract.
                    bool sharedBasis=(long)error.Length*variables.Length>2000000;
                    var basis=sharedBasis?variables.Select((v,i)=>(V:v,I:i)).Where(p=>p.V.Value).GroupBy(p=>(p.V.Bone,p.V.Channel)).Select(g=>g.Select(p=>p.I).ToArray()).ToArray():Enumerable.Range(0,variables.Length).Select(i=>new[]{i}).ToArray();
                    int n=basis.Length;
                    if((long)error.Length*n>2000000){Reason("Shared temporal Jacobian resource bound reached");break;}
                    if(sharedBasis){sharedBasisWindows++;Reason("Direct shared-value temporal basis used within dense resource bound");}
                    double[] Expand(double[] values){var expanded=new double[variables.Length];for(int j=0;j<n;j++)foreach(int index in basis[j])expanded[index]=values[j];return expanded;}
                    var jacobian=new double[error.Length,n];const double epsilon=.001;
                    // The same local channel derivative drives endpoint values
                    // and slopes. Differentiate a pose once, then apply exact
                    // Hermite coefficients instead of evaluating the whole
                    // neighborhood again for every coefficient.
                    int offset=0,sampleIndex=0;var satisfiedRows=new List<int>();
                    foreach(var sample in linear)
                    {
                        var pose=sample.Pose;var residual=TargetMotionConstraints.Residual(windowPlan,sample.Frame,pose,sample.Active,sample.Pins,sample.Cuts,sample.Body);
                        foreach(var channel in editable)
                        {
                            var columns=Enumerable.Range(0,n).Where(j=>variables[basis[j][0]].Bone==channel.Bone&&variables[basis[j][0]].Channel==channel.Channel).Select(j=>(Index:j,Coefficient:basis[j].Sum(index=>Coefficient(variables[index],sample.Frame)))).Where(c=>c.Coefficient!=0).ToArray();if(columns.Length==0)continue;
                            var local=pose.Local.ToArray();var t=local[channel.Bone];if(channel.Channel>=6){var p=t.Translation;p[channel.Channel-6]+=(float)(epsilon*scale);local[channel.Bone]=t with{Translation=p};}else{var r=t.Rotation;r[channel.Channel-3]+=(float)(epsilon*180/Math.PI);local[channel.Bone]=t with{Rotation=r};}
                            Charge();var shifted=AnimationService.Evaluate(scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t)) with{Seconds=sample.Frame/plan.Rate.FramesPerSecond};
                            var plus=TargetMotionConstraints.Residual(windowPlan,sample.Frame,shifted,sample.Active,sample.Pins,sample.Cuts,sample.Body);foreach(var column in columns)for(int row=0;row<residual.Length;row++)jacobian[offset+row,column.Index]=(plus[row]-residual[row])/epsilon*column.Coefficient;
                        }
                        if(beforeViolations[sampleIndex++]<=1)satisfiedRows.AddRange(Enumerable.Range(offset,residual.Length));
                        offset+=residual.Length;
                    }
                    var rhs=new double[n];for(int j=0;j<n;j++)for(int row=0;row<error.Length;row++)rhs[j]-=jacobian[row,j]*error[row];
                    double[] Multiply(double[] x){var tmp=new double[error.Length];for(int row=0;row<tmp.Length;row++)for(int j=0;j<n;j++)tmp[row]+=jacobian[row,j]*x[j];var y=new double[n];for(int j=0;j<n;j++){y[j]=x[j]*.01;for(int row=0;row<tmp.Length;row++)y[j]+=jacobian[row,j]*tmp[row];}return y;}
                    var delta=TemporalRetargetSolver.ConjugateGradient(rhs,Multiply,Math.Min(128,n*2));
                    IEnumerable<(double[] Step,bool Priority)> Directions()
                    {
                        if(preferred is not null)
                        {
                            var source=basis.Select(group=>group.Average(index=>
                            {
                                var v=variables[index];double unit=v.Channel>=6?scale:180/Math.PI;
                                var original=preferred[v.Track].Channels[v.Channel]!.Keys[v.Key];var value=current[v.Track].Channels[v.Channel]!.Keys[v.Key];
                                return ((double)(v.Value?original.Value:original.OutTangent)-(v.Value?value.Value:value.OutTangent))/unit-q[index];
                            })).ToArray();
                            // Continue from a source-proximal large-gap solve
                            // towards actual feasibility as the physical gap
                            // closes. This is a global numerical work policy,
                            // not a contact tolerance or anatomy exception.
                            double gap=error.Select(Math.Abs).DefaultIfEmpty(0).Max()*plan.PositionBound/scale;
                            double damping=Math.Max(1e-10,.01*Math.Min(1,gap*gap));
                            Reason("Source-tethered native correction proposed");
                            yield return(Expand(FeasibleCurveDirection.SourceTether(jacobian,error,source,damping,cancellation)),satisfiedRows.Count>0);
                        }
                        if(sharedBasis)
                        {
                            var coarse=FeasibleCurveDirection.Solve(jacobian,error,satisfiedRows,cancellation);
                            if(preferred is not null)
                            {
                                var preference=basis.Select(group=>group.Average(index=>
                                {
                                    var v=variables[index];double unit=v.Channel>=6?scale:180/Math.PI;
                                    return ((double)preferred[v.Track].Channels[v.Channel]!.Keys[v.Key].Value-current[v.Track].Channels[v.Channel]!.Keys[v.Key].Value)/unit-q[index];
                                })).ToArray();
                                yield return(Expand(FeasibleCurveDirection.Toward(jacobian,coarse,preference,cancellation)),satisfiedRows.Count>0);
                            }
                            yield return(Expand(coarse),satisfiedRows.Count>0);
                            yield return(Expand(delta),false);
                            yield break;
                        }
                        // A coarse temporal basis INSIDE this connected window
                        // moves shared values together before independent key/
                        // slope coefficients refine them. This improves the
                        // conditioning of persistent relationships without a
                        // whole-action orientation offset or phase-name rules.
                        var groups=variables.Select((v,i)=>(V:v,I:i)).Where(p=>p.V.Value).GroupBy(p=>(p.V.Bone,p.V.Channel)).ToArray();
                        var reduced=new double[error.Length,groups.Length];
                        for(int g=0;g<groups.Length;g++)foreach(var p in groups[g])for(int row=0;row<error.Length;row++)reduced[row,g]+=jacobian[row,p.I];
                        var shared=FeasibleCurveDirection.Solve(reduced,error,satisfiedRows,cancellation);
                        if(preferred is not null)
                        {
                            var preference=groups.Select(group=>group.Average(p=>
                            {
                                var v=p.V;double unit=v.Channel>=6?scale:180/Math.PI;
                                return ((double)preferred[v.Track].Channels[v.Channel]!.Keys[v.Key].Value-current[v.Track].Channels[v.Channel]!.Keys[v.Key].Value)/unit-q[p.I];
                            })).ToArray();
                            var guided=FeasibleCurveDirection.Toward(reduced,shared,preference,cancellation);var proposal=new double[n];
                            for(int g=0;g<groups.Length;g++)foreach(var p in groups[g])proposal[p.I]=guided[g];
                            yield return(proposal,satisfiedRows.Count>0);
                        }
                        var expanded=new double[n];
                        for(int g=0;g<groups.Length;g++)foreach(var p in groups[g])expanded[p.I]=shared[g];
                        yield return(expanded,satisfiedRows.Count>0);
                        yield return(delta,false);
                        if(satisfiedRows.Count>0)yield return(FeasibleCurveDirection.Solve(jacobian,error,satisfiedRows,cancellation),true);
                    }
                    bool improved=false;
                    foreach(var direction in Directions())
                    {
                    if(direction.Step.Any(d=>!double.IsFinite(d)))continue;
                    double cap=1;for(int j=0;j<variables.Length;j++)cap=Math.Min(cap,.5/Math.Max(1e-12,Math.Abs(direction.Step[j])));var step=direction.Step.Select(d=>d*cap).ToArray();
                    for(double alpha=1;alpha>=(direction.Priority?1d/4096:1d/128);alpha/=2)
                    {
                        var trial=q.Zip(step,(a,b)=>a+alpha*b).ToArray();var candidate=Coefficients(trial);
                        var poses=samples.Select(s=>Evaluate(candidate,s.Frame)).ToArray();
                        if(!Limits(poses)){Reason("Candidate exceeds reviewed joint limits");continue;}
                        double cost=Cost(Merit(poses));
                        if(!double.IsFinite(cost)||cost>=retainedCost-1e-10){Reason("Candidate does not reduce finite required-constraint residual cost");continue;}
                        var violations=samples.Select((s,i)=>TargetMotionConstraints.MaximumNormalizedViolation(windowPlan,s.Frame,poses[i])).ToArray();
                        // Restoration may redistribute error among already
                        // failing samples of this connected curve window.
                        // Freezing every bad sample separately blocks a valid
                        // joint polynomial correction. Passing samples retain
                        // the actual unchanged bound; window worst error must
                        // not grow and total required residual cost must fall.
                        if(violations.Any(value=>!double.IsFinite(value))||violations.Max()>restorationBound+1e-9)
                        {Reason("Candidate increases the window worst required-constraint violation");continue;}
                        if(violations.Where((value,i)=>beforeViolations[i]<=1&&value>1).Any())
                        {Reason("Candidate makes a previously feasible sample fail its frozen bound");continue;}
                        q=trial;retained=candidate;retainedCost=cost;improved=true;if(direction.Priority)Reason("Accepted feasibility-priority direction");break;
                    }
                    if(improved)break;
                    }
                    if(!improved)break;
                }
                if(retained is null)
                {
                    // Symmetric/flat skin can have a stationary first-order
                    // direction despite a viable bend. Test bounded deterministic
                    // branches on ACTUAL affected skin ancestry, in this same
                    // curve window. No bone/action/model labels select a seed.
                    var points=new List<SkinSurfacePoint>();
                    if(windowPlan.Clearance is {} surface)foreach(var sample in samples)
                        points.AddRange(sample.Cuts.Where(c=>c.Active).Select(c=>surface.Points[c.Index]));
                    foreach(var sample in samples){if(sample.Body is {} supported)points.Add(supported);points.AddRange(sample.Pins.SelectMany(p=>p.SurfacePoints.Concat(p.SurfaceRegion)));}
                    double Leverage(int bone)=>points.Sum(p=>Enumerable.Range(0,p.InfluenceCount).Where(i=>p.WeightAt(i)>0&&PoseGeometry.Ancestor(scene,bone,(int)p.JointAt(i))).Sum(i=>(double)p.WeightAt(i)));
                    int Depth(int bone){int depth=0;for(;bone>=0;bone=scene.Bones[bone].Parent)depth++;return depth;}
                    var seeds=editable.Where(c=>c.Channel<6).Select(c=>(Channel:c,Score:Leverage(c.Bone)))
                        .Where(c=>c.Score>0).OrderByDescending(c=>c.Score).ThenByDescending(c=>Depth(c.Channel.Bone))
                        .ThenBy(c=>plan.Rig.CanonicalBoneRanks[c.Channel.Bone]).ThenBy(c=>c.Channel.Channel).Take(9).ToArray();
                    double best=initial;
                    foreach(var seed in seeds)foreach(double angle in new[]{-.25,.25,-.75,.75,-1.25,1.25})
                    {
                        var delta=new double[variables.Length];for(int j=0;j<delta.Length;j++)if(variables[j].Value&&variables[j].Bone==seed.Channel.Bone&&variables[j].Channel==seed.Channel.Channel)delta[j]=angle;
                        var candidate=Coefficients(delta);var poses=samples.Select(s=>Evaluate(candidate,s.Frame)).ToArray();if(!Limits(poses))continue;
                        double cost=Cost(Merit(poses));if(!double.IsFinite(cost)||cost>=best-1e-10)continue;
                        var violations=samples.Select((s,i)=>TargetMotionConstraints.MaximumNormalizedViolation(windowPlan,s.Frame,poses[i])).ToArray();
                        if(violations.Any(v=>!double.IsFinite(v))||violations.Max()>restorationBound+1e-9||violations.Where((v,i)=>beforeViolations[i]<=1&&v>1).Any())continue;
                        retained=candidate;best=cost;Reason("Accepted deterministic skin-ancestry seed");
                    }
                }
                if(retained is null)continue;
                valueUpdates+=variables.Count(v=>v.Value&&retained[v.Track].Channels[v.Channel]!.Keys[v.Key].Value!=current[v.Track].Channels[v.Channel]!.Keys[v.Key].Value);
                current=retained;changed=true;updates++;
            }
            if(!changed)break;
        }
        }
        catch(WorkBudgetReached){return Result(true);}
        return Result(false);
    }
}
