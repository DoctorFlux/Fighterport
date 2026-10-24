using System.Numerics;

namespace FighterPort.Core;

public record NativeJointLimit(int Bone,Vector3 MinimumDegrees,Vector3 MaximumDegrees,string Evidence,bool Reviewed);
public record TemporalSolveReceipt(string Version,int Frames,int Variables,int ProjectionIterations,int TemporalIterations,
    double InitialCorrectionCost,double FinalCorrectionCost,float MaximumNormalizedConstraint,string Coverage)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public long PoseEvaluations {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ConstraintMeasure {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int FeasibleStallExits {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public int CollisionStallRefinements {get;init;}
}
public record SolvedTargetMotion(double[] Frames,Transform[][] Poses,TemporalSolveReceipt Receipt)
{
    public ContactClearance? EffectiveClearance {get;init;}
}

// Projected optimization of the correction field over the whole clip. Neighbour
// derivatives participate in every update; accepted poses satisfy the constraints
// again. This is neither pose blending nor a playback-history warm start.
public static class TemporalRetargetSolver
{
    public const string Version="coupled-action-bounded-skin-25";
    public const double VelocitySecondsSquared=.00005,AccelerationSecondsFourth=.00000001;
    // Frozen initial design weight, not a contact acceptance tolerance. Required
    // constraints are independently projected and measured under unchanged bounds.
    public const double InferredSupportObjectiveWeight=25;
    // Global preference weights, not acceptance tolerances. Preserve the
    // established coordinate prior and add the mean geometric action cost
    // without multiplying it by the rig's number of solver variables.
    public const double RotationPriorWeight=1,BodyTranslationPriorWeight=1,ActionObjectiveWeight=1;
    const int ProjectionBudget=64,TemporalBudget=12;
    public const int Substeps=4;
    readonly record struct Variable(int Bone,int Channel);
    public static OperationResult<SolvedTargetMotion> Solve(TargetMotionPlan plan,CancellationToken cancellation=default)
    {
        var scene=plan.Rig.Scene;int count=(plan.Samples-(plan.Closing?0:1))*Substeps+1;var times=Enumerable.Range(0,count).Select(f=>f/(double)Substeps).ToArray();
        if(count>4096)return new(null,[new("TEMPORAL_BUDGET","Whole-clip fitting currently supports up to 4096 prepared samples; no truncated solve was run.")],[]);
        var source=new Transform[count][];Vector3[]? winding=null;
        for(int f=0;f<count;f++){source[f]=plan.Reference(times[f],winding);winding=source[f].Select(t=>t.Rotation).ToArray();}
        var joints=new SortedSet<int>();var helpers=plan.Controls.PreservedHelperBones.ToHashSet();int root=plan.Rig.Binding.Roles["RootMotion"];
        void AddAncestors(int bone){for(;bone>=0&&bone!=root;bone=scene.Bones[bone].Parent)if(!helpers.Contains(bone)&&!plan.Rig.IsActorAncestor(bone))joints.Add(bone);}
        foreach(var pin in plan.Graph.Relations.SelectMany(c=>c.Pins))
        {
            AddAncestors(pin.Bone);
            foreach(var point in pin.SurfacePoints.Concat(pin.SurfaceRegion).Concat(pin.Reference?.SurfaceTripod??[]))for(int j=0;j<4;j++)if(point.Weights[j]>0)AddAncestors((int)point.Joints[j]);
            if(pin.Reference?.Bone is {} attachment)AddAncestors(attachment);
        }
        if(plan.Clearance is not null)foreach(var point in plan.Rig.SkinPoints)for(int j=0;j<4;j++)if(point.Weights[j]>0)AddAncestors((int)point.Joints[j]);
        var variables=joints.OrderBy(b=>plan.Rig.CanonicalBoneRanks[b]).SelectMany(b=>Enumerable.Range(0,3).Select(c=>new Variable(b,c))).ToList();
        int body=plan.Rig.Binding.Roles.GetValueOrDefault("Hips",-1);if(body>=0&&body!=root)variables.AddRange(Enumerable.Range(3,3).Select(c=>new Variable(body,c)));
        double CoordinateWeight(int j)=>variables[j].Channel<3?RotationPriorWeight:BodyTranslationPriorWeight;
        var limits=plan.Controls.JointLimits??[];foreach(var limit in limits)if(!limit.Reviewed||string.IsNullOrWhiteSpace(limit.Evidence)||limit.Bone<0||limit.Bone>=scene.Bones.Length||!PoseGeometry.Finite(limit.MinimumDegrees)||!PoseGeometry.Finite(limit.MaximumDegrees)||limit.MinimumDegrees.X>limit.MaximumDegrees.X||limit.MinimumDegrees.Y>limit.MaximumDegrees.Y||limit.MinimumDegrees.Z>limit.MaximumDegrees.Z)throw new InvalidDataException("Joint limits require reviewed target-native bounds and provenance.");
        float scale=plan.Rig.CharacteristicLength;int projections=0,feasibleStalls=0,collisionStallRefinements=0;long poseEvaluations=0;
        var q=Enumerable.Range(0,count).Select(_=>new double[variables.Count]).ToArray();
        EvaluatedPose Pose(int frame,double[] values)
        {
            poseEvaluations++;
            var local=source[frame].ToArray();for(int j=0;j<variables.Count;j++)
            {
                var v=variables[j];var t=local[v.Bone];if(v.Channel<3){var rotation=t.Rotation;rotation[v.Channel]+=(float)values[j]*180/MathF.PI;local[v.Bone]=t with{Rotation=rotation};}
                else{var translation=t.Translation;translation[v.Channel-3]+=(float)values[j]*scale;local[v.Bone]=t with{Translation=translation};}
            }
            return AnimationService.Evaluate(scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t)) with{Seconds=times[frame]/plan.Rate.FramesPerSecond};
        }
        void Limit(int frame,double[] values)
        {
            for(int j=0;j<variables.Count;j++)if(variables[j].Channel<3&&limits.FirstOrDefault(l=>l.Bone==variables[j].Bone) is {} limit)
            {
                var v=variables[j];double original=source[frame][v.Bone].Rotation[v.Channel];double degrees=original+values[j]*180/Math.PI;values[j]=(Math.Clamp(degrees,limit.MinimumDegrees[v.Channel],limit.MaximumDegrees[v.Channel])-original)*Math.PI/180;
            }
        }
        int[] ActiveClearance(int frame,double[] values)=>TargetMotionConstraints.ActiveClearance(plan,times[frame],Pose(frame,values));
        double[] Residual(int frame,double[] values,int[]? clearanceIndices=null,ContactPin[]? pins=null,TargetMotionConstraints.ClearanceCut[]? cuts=null,SkinSurfacePoint? bodyPoint=null)=>TargetMotionConstraints.Residual(plan,times[frame],Pose(frame,values),clearanceIndices,pins,cuts,bodyPoint);
        var observations=times.Select(t=>plan.Pins(t).Where(p=>p.SoftSupport&&!p.RequiredSupport).ToArray()).ToArray();
        HashSet<int> Influences(IEnumerable<ContactPin> pins,IEnumerable<SkinSurfacePoint>? geometry=null)
        {
            var affected=new HashSet<int>();
            void Bone(int bone){for(;bone>=0;bone=scene.Bones[bone].Parent)affected.Add(bone);}
            void Point(SkinSurfacePoint point){for(int j=0;j<4;j++)if(point.Weights[j]>0)Bone((int)point.Joints[j]);}
            foreach(var pin in pins)
            {
                if(pin.HoldRotation||pin.SurfacePoints.Length==0&&pin.SurfaceRegion.Length==0)Bone(pin.Bone);
                foreach(var point in pin.SurfacePoints.Concat(pin.SurfaceRegion).Concat(pin.Reference?.SurfaceTripod??[]))Point(point);
                if(pin.Reference?.Bone is {} attachment)Bone(attachment);
            }
            foreach(var point in geometry??[])Point(point);return affected;
        }
        var observationInfluences=observations.Select(p=>Influences(p)).ToArray();
        var actions=source.Select(local=>PreparedMotionAction.Create(plan.Rig,plan.Baseline.Mapping,
            AnimationService.Evaluate(scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t)))).ToArray();
        double ActionCost(int frame,double[] values)=>ActionObjectiveWeight*actions[frame].Cost(Pose(frame,values));
        double ObservationCost(int frame,double[] values)
        {
            if(observations[frame].Length==0)return 0;var pose=Pose(frame,values);
            return ContactObservationObjective.Cost(observations[frame],pose,scale);
        }
        static double Cost(double[] r)=>r.Sum(v=>v*v);
        bool Feasible(int frame,double[] values)=>TargetMotionConstraints.MaximumNormalizedViolation(plan,times[frame],Pose(frame,values))<=1;
        bool Project(int frame,double[] values,bool fixedBodyTranslation=false)
        {
            Limit(frame,values);
            for(int iteration=0;iteration<ProjectionBudget;iteration++)
            {
                cancellation.ThrowIfCancellationRequested();projections++;var active=ActiveClearance(frame,values);var residual=Residual(frame,values,active);double cost=Cost(residual);
                if(residual.All(r=>Math.Abs(r)<=.5)&&Residual(frame,values).All(r=>Math.Abs(r)<=.5))
                {
                    var refined=CollisionProxyRefinement.Refine(plan,times[frame],Pose(frame,values));
                    if(refined==plan.Clearance)return Feasible(frame,values);
                    plan=plan with{Clearance=refined};continue;
                }
                if(variables.Count==0)return Feasible(frame,values);
                var linearPins=TargetMotionConstraints.LinearizationPins(plan,times[frame],Pose(frame,values));
                var linearCuts=TargetMotionConstraints.LinearizationCuts(plan,Pose(frame,values),active);
                var linearBody=TargetMotionConstraints.LinearizationBodyPoint(plan,times[frame],Pose(frame,values));
                var collisionPoints=plan.Clearance is {} sparsePlane?linearCuts.Where(c=>c.Active).Select(c=>sparsePlane.Points[c.Index]):Enumerable.Empty<SkinSurfacePoint>();
                bool bodySupport=plan.HasGround(times[frame])&&plan.Grounding is {} g&&SurfaceContacts.BodySupportedAt(g.BodySupportPhases,g.BodySupportedThroughout,g.BodySupportedAtClosing,plan.SourceFrame(times[frame]),plan.AuthenticSourceSamples);
                var affected=Influences(linearPins.Where(p=>!p.SoftSupport||p.RequiredSupport),collisionPoints.Concat(bodySupport&&linearBody is not null?[linearBody]:[]));
                var jacobian=new double[residual.Length,variables.Count];const double step=.001;
                for(int j=0;j<variables.Count;j++){if(!affected.Contains(variables[j].Bone)||fixedBodyTranslation&&variables[j].Bone==body&&variables[j].Channel>=3)continue;double old=values[j];values[j]=old+step;var shifted=Residual(frame,values,active,linearPins,linearCuts,linearBody);values[j]=old;for(int row=0;row<residual.Length;row++)jacobian[row,j]=(shifted[row]-residual[row])/step;}
                // Matrix-free damped normal solve; memory is O(rows*variables),
                // not a dense whole-clip matrix. CG iteration order is fixed.
                var rhs=new double[variables.Count];for(int j=0;j<variables.Count;j++)for(int row=0;row<residual.Length;row++)rhs[j]-=jacobian[row,j]*residual[row];
                double[] Multiply(double[] x){var y=new double[x.Length];var tmp=new double[residual.Length];for(int row=0;row<tmp.Length;row++)for(int j=0;j<x.Length;j++)tmp[row]+=jacobian[row,j]*x[j];for(int j=0;j<x.Length;j++){y[j]=x[j]*.01;for(int row=0;row<tmp.Length;row++)y[j]+=jacobian[row,j]*tmp[row];}return y;}
                var delta=ConjugateGradient(rhs,Multiply,Math.Min(96,variables.Count*2));double largest=delta.Select(Math.Abs).DefaultIfEmpty().Max();if(largest>.15)for(int j=0;j<delta.Length;j++)delta[j]*=.15/largest;
                bool improved=false;for(double alpha=1;alpha>=1d/128;alpha/=2){var trial=values.Zip(delta,(a,b)=>a+alpha*b).ToArray();Limit(frame,trial);if(Cost(Residual(frame,trial,active))<cost-1e-10){Array.Copy(trial,values,values.Length);improved=true;break;}}
                // A zero numerical step is not a constraint contradiction.
                // Retain the preferred half-bound convergence target above,
                // but use the actual frozen vector/full-skin bounds at a stall,
                // just as at budget exhaustion. Diagonal componentwise errors
                // outside that bound are rejected by the independent measure.
                if(!improved)
                {
                    bool feasible=Feasible(frame,values);
                    if(feasible){feasibleStalls++;return true;}
                    // A valid residual between the preferred half bound and
                    // the full bound can stall the sparse objective while an
                    // omitted deformed vertex still penetrates. Add that
                    // actual constraint before declaring non-convergence.
                    // This consumes the existing bounded projection budget.
                    var refined=CollisionProxyRefinement.Refine(plan,times[frame],Pose(frame,values));
                    if(refined!=plan.Clearance){plan=plan with{Clearance=refined};collisionStallRefinements++;continue;}
                    return false;
                }
            }
            return Feasible(frame,values);
        }
        void Seed(int f,bool analyticLimbs=false)
        {
            var pins=plan.Pins(times[f]);
            if(f>0)
            {
                // Contact onset/release is a boundary of the same action, not
                // a reason to restart from an unrelated pose branch. Carry the
                // canonical offline correction across that boundary, then
                // project the NEW constraints and optimize the coupled field.
                // No old contact is retained after its authored interval ends.
                Array.Copy(q[f-1],q[f],variables.Count);
                if(source[f].SequenceEqual(source[f-1])&&pins.Select(p=>p.Id).SequenceEqual(plan.Pins(times[f-1]).Select(p=>p.Id)))return;
            }
            if(body>=0&&body!=root)
            {
                var supported=pins.Where(p=>(!p.SoftSupport||p.RequiredSupport)&&PoseGeometry.Ancestor(scene,body,p.Bone)).ToArray();
                if(supported.Length>0)
                {
                    // A common body displacement is a seed, never a released pin
                    // or a change to root travel/rest lengths. Final constraints
                    // and temporal regularization still decide the accepted pose.
                    var pose=Pose(f,q[f]);var shift=-supported.Select(p=>ContactGeometry.Residual(p,pose)).Aggregate(Vector3.Zero,(a,b)=>a+b)/supported.Length;int parent=scene.Bones[body].Parent;
                    if(parent>=0&&Matrix4x4.Invert(pose.World[parent],out var inverseParent))shift=Vector3.TransformNormal(shift,inverseParent);
                    for(int j=0;j<variables.Count;j++)if(variables[j].Bone==body&&variables[j].Channel>=3)q[f][j]+=shift[variables[j].Channel-3]/scale;
                }
            }
            if(body>=0&&body!=root&&plan.HasGround(times[f])&&plan.Clearance is {} plane)
            {
                // Start on the feasible side of the fixed support plane. This
                // moves existing body channels, never the floor, binds or lengths.
                // Edge/airborne phases do not receive this initialization.
                var pose=Pose(f,q[f]);var deepest=plane.Points.Select(p=>ContactGeometry.Point(p,pose)).OrderByDescending(p=>plane.Surface.Penetration(p)).FirstOrDefault();float depth=plane.Surface.Penetration(deepest);
                if(depth>0)
                {
                    var shift=plane.Surface.Correction(deepest);int parent=scene.Bones[body].Parent;
                    if(parent>=0&&Matrix4x4.Invert(pose.World[parent],out var inverseParent))shift=Vector3.TransformNormal(shift,inverseParent);
                    for(int j=0;j<variables.Count;j++)if(variables[j].Bone==body&&variables[j].Channel>=3)q[f][j]+=shift[variables[j].Channel-3]/scale;
                }
            }
            // Analytic same-rig IK is an optional seed for an actually adjacent
            // two-segment chain. The solver above still supports every chain
            // length and enforces all constraints jointly after initialization.
            var feet=pins.Where(p=>p.Role.StartsWith("Foot.",StringComparison.Ordinal)&&!p.SoftSupport&&p.PlaneNormal is not null).ToArray();
            if(body>=0&&body!=root&&feet.Length>0&&plan.HasGround(times[f]))
            {
                var pose=Pose(f,q[f]);var up=new Vector3(plan.Rig.Basis.M21,plan.Rig.Basis.M22,plan.Rig.Basis.M23);float gap=feet.Max(p=>Vector3.Dot(ContactGeometry.Residual(p,pose),up));var shift=-up*gap;int parent=scene.Bones[body].Parent;
                if(parent>=0&&Matrix4x4.Invert(pose.World[parent],out var inv))shift=Vector3.TransformNormal(shift,inv);
                for(int j=0;j<variables.Count;j++)if(variables[j].Bone==body&&variables[j].Channel>=3)q[f][j]+=shift[variables[j].Channel-3]/scale;
            }
            foreach(var chain in analyticLimbs?plan.Controls.Chains:[])
            {
                if(scene.Bones[chain.Middle].Parent!=chain.Root||scene.Bones[chain.Effector].Parent!=chain.Middle)continue;
                var pin=pins.FirstOrDefault(p=>p.Bone==chain.Effector&&!p.SoftSupport);if(pin is null)continue;
                var pose=Pose(f,q[f]);var goal=pose.World[chain.Effector].Translation-ContactGeometry.Residual(pin,pose);
                var reference=AnimationService.Evaluate(scene,null,0,source[f].Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
                var pole=AnimationControls.DefaultPole(scene,reference,chain)+pose.World[chain.Root].Translation-reference.World[chain.Root].Translation;
                var seed=AnimationControls.Solve(scene,pose,plan.Controls,chain,goal,pole);
                if(seed.Value is not {} value)continue;
                for(int j=0;j<variables.Count;j++)if(variables[j].Channel<3&&value.Transforms.TryGetValue(variables[j].Bone,out var t))q[f][j]=(t.Rotation[variables[j].Channel]-source[f][variables[j].Bone].Rotation[variables[j].Channel])*Math.PI/180;
            }
            Limit(f,q[f]);
        }
        bool GeometrySeeds(int frame,ref double[] values)
        {
            if(!plan.HasGround(times[frame])||plan.Clearance is not {} plane)return false;
            var best=values.ToArray();double bestCost=Cost(Residual(frame,best));
            if(body>=0&&body!=root&&plane.Edge is {} edge)
            {
                // A corner has separate outside and above feasible branches.
                // The shortest correction of just one colliding vertex can
                // leave a larger neighbouring surface inside the platform.
                // Try a whole-skin outside seed, then re-enforce every pin.
                var pose=Pose(frame,best);
                float outward=plan.Rig.SkinPoints.Select(p=>ContactGeometry.Point(p,pose)).Where(p=>Vector3.Dot(p,plane.Normal)<plane.Height).Select(p=>Vector3.Dot(p,edge.Inward)-edge.Offset).DefaultIfEmpty().Max();
                if(outward>0)
                {
                    var trial=best.ToArray();var shift=-edge.Inward*outward;int parent=scene.Bones[body].Parent;
                    if(parent>=0&&Matrix4x4.Invert(pose.World[parent],out var inv))shift=Vector3.TransformNormal(shift,inv);
                    for(int j=0;j<variables.Count;j++)if(variables[j].Bone==body&&variables[j].Channel>=3)trial[j]+=shift[variables[j].Channel-3]/scale;
                    if(Project(frame,trial,true)){values=trial;return true;}
                    double cost=Cost(Residual(frame,trial));if(cost<bestCost){best=trial;bestCost=cost;}
                }
            }
            // A flat/symmetric skin patch can have a zero first derivative even
            // when another bend branch clears the support. Search deterministic
            // perturbations of the actual colliding skin's influence ancestors.
            // No character, clip name, native bone name or prescribed pose enters.
            for(int sweep=0;sweep<2;sweep++)
            {
                cancellation.ThrowIfCancellationRequested();var pose=Pose(frame,best);var influences=new HashSet<int>();
                foreach(var point in plane.Points.OrderBy(p=>plane.Surface.Clearance(ContactGeometry.Point(p,pose))).Take(8))for(int k=0;k<4;k++)if(point.Weights[k]>0)
                    for(int bone=(int)point.Joints[k];bone>=0&&bone!=root;bone=scene.Bones[bone].Parent)influences.Add(bone);
                foreach(int index in Enumerable.Range(0,variables.Count).Where(j=>variables[j].Channel<3&&influences.Contains(variables[j].Bone)))
                {
                    double[]? improved=null;double candidateCost=bestCost;
                    foreach(double angle in new[]{-.001,.001,-.01,.01,-.1,.1,-.5,.5,-1d,1d,-1.5,1.5})
                    {
                        cancellation.ThrowIfCancellationRequested();var trial=best.ToArray();trial[index]+=angle;Limit(frame,trial);double cost=Cost(Residual(frame,trial));
                        if(cost<candidateCost-1e-9){improved=trial;candidateCost=cost;}
                    }
                    if(improved is not null){best=improved;bestCost=candidateCost;}
                }
                var projected=best.ToArray();if(Project(frame,projected)){values=projected;return true;}
                double projectionCost=Cost(Residual(frame,projected));if(projectionCost<bestCost){best=projected;bestCost=projectionCost;}
            }
            values=best;return false;
        }
        for(int f=0;f<count;f++)
        {
            cancellation.ThrowIfCancellationRequested();Seed(f);
            if(!Project(f,q[f]))
            {
                // A failed seed is not a geometric contradiction. Try the
                // independent baseline, then the prior canonical correction.
                q[f]=new double[variables.Count];bool feasible=Project(f,q[f]);
                if(!feasible&&f>0){q[f]=q[f-1].ToArray();feasible=Project(f,q[f]);}
                if(!feasible){q[f]=new double[variables.Count];Seed(f,true);feasible=Project(f,q[f]);}
                if(!feasible)feasible=GeometrySeeds(f,ref q[f]);
                if(!feasible)
                {
                    var pose=Pose(f,q[f]);string issues=string.Join("; ",plan.Pins(times[f]).Where(p=>!p.SoftSupport||p.RequiredSupport).Select(p=>$"{p.Role}: position {ContactGeometry.Residual(p,pose).Length():G9}"));
                    if(plan.HasGround(times[f])&&plan.Clearance is {} failedPlane)
                    {
                        float proxyDepth=failedPlane.Points.Select(p=>failedPlane.Surface.Penetration(ContactGeometry.Point(p,pose))).DefaultIfEmpty().Max();
                        var witness=plan.Rig.SkinPoints.Concat(failedPlane.Points).MaxBy(p=>failedPlane.Surface.Penetration(ContactGeometry.Point(p,pose)));
                        float depth=witness is null?0:failedPlane.Surface.Penetration(ContactGeometry.Point(witness,pose));
                        bool bodyRequired=plan.Grounding is {} g&&SurfaceContacts.BodySupportedAt(g.BodySupportPhases,g.BodySupportedThroughout,g.BodySupportedAtClosing,plan.SourceFrame(times[f]),plan.AuthenticSourceSamples);
                        string support=bodyRequired&&plan.BodySupportPoints.Length>0?$"; required torso surface gap {Math.Abs(plan.BodySupportPoints.Min(p=>Vector3.Dot(ContactGeometry.Point(p,pose),failedPlane.Normal))-failedPlane.Height):G9}":"";
                        issues+=$"; fixed-plane full-skin penetration {depth:G9}, sparse proxy penetration {proxyDepth:G9} at height {failedPlane.Height:G9}{support}";
                        if(witness is not null)issues+=$"; deepest mesh {witness.Mesh}, vertex {witness.Vertex}, bones {string.Join('/',Enumerable.Range(0,4).Where(i=>witness.Weights[i]>0).Select(i=>(int)witness.Joints[i]))}";
                    }
                    return new(null,[new("TEMPORAL_NONCONVERGENCE",$"Constraint projection did not converge at target sample {times[f]:G12}, source {plan.SourceFrame(times[f]):G12}; maximum normalized full constraint violation {TargetMotionConstraints.MaximumNormalizedViolation(plan,times[f],pose):G9}; maximum normalized sparse residual component {Residual(f,q[f]).Select(Math.Abs).DefaultIfEmpty().Max():G9}; {issues}. This is not proof of incompatible anatomy; no skeleton stretching or contact release was used.")],[]);
                }
            }
        }
        double dt=1/(plan.Rate.FramesPerSecond*Substeps),vWeight=VelocitySecondsSquared/(dt*dt),aWeight=AccelerationSecondsFourth/Math.Pow(dt,4);
        double Objective(double[][] field)
        {
            double cost=0;for(int f=0;f<count;f++){cost+=ActionCost(f,field[f])+ObservationCost(f,field[f]);for(int j=0;j<variables.Count;j++){cost+=CoordinateWeight(j)*field[f][j]*field[f][j];if(f>0)cost+=vWeight*Math.Pow(field[f][j]-field[f-1][j],2);if(f>1)cost+=aWeight*Math.Pow(field[f][j]-2*field[f-1][j]+field[f-2][j],2);}}return cost;
        }
        double initial=Objective(q),best=initial;int temporal=0;
        for(;temporal<TemporalBudget;temporal++)
        {
            cancellation.ThrowIfCancellationRequested();var gradient=q.Select(r=>r.Select((x,j)=>2*CoordinateWeight(j)*x).ToArray()).ToArray();
            for(int f=0;f<count;f++)for(int j=0;j<variables.Count;j++)
            {
                cancellation.ThrowIfCancellationRequested();const double h=.0001;double old=q[f][j];q[f][j]=old+h;double plus=ActionCost(f,q[f]);q[f][j]=old-h;double minus=ActionCost(f,q[f]);q[f][j]=old;gradient[f][j]+=(plus-minus)/(2*h);
            }
            for(int f=0;f<count;f++)if(observations[f].Length>0)for(int j=0;j<variables.Count;j++)
            {
                if(!observationInfluences[f].Contains(variables[j].Bone))continue;
                cancellation.ThrowIfCancellationRequested();const double h=.0001;double old=q[f][j];q[f][j]=old+h;double plus=ObservationCost(f,q[f]);q[f][j]=old-h;double minus=ObservationCost(f,q[f]);q[f][j]=old;gradient[f][j]+=(plus-minus)/(2*h);
            }
            for(int f=1;f<count;f++)for(int j=0;j<variables.Count;j++){double d=2*vWeight*(q[f][j]-q[f-1][j]);gradient[f][j]+=d;gradient[f-1][j]-=d;}
            for(int f=2;f<count;f++)for(int j=0;j<variables.Count;j++){double d=2*aWeight*(q[f][j]-2*q[f-1][j]+q[f-2][j]);gradient[f][j]+=d;gradient[f-1][j]-=2*d;gradient[f-2][j]+=d;}
            // Retain the established derivative-scaled step while evaluating
            // the additional geometric action objective independently. Every
            // proposed pose is projected onto the unchanged required bounds.
            bool accepted=false;
            for(double alpha=.2/(1+4*vWeight+16*aWeight);alpha>=.00001;alpha/=2)
            {
                var candidate=q.Select((row,f)=>row.Select((x,j)=>x-alpha*gradient[f][j]).ToArray()).ToArray();bool feasible=true;
                for(int f=0;f<count;f++)if(!Project(f,candidate[f])){feasible=false;break;}
                double cost=feasible?Objective(candidate):double.PositiveInfinity;if(cost<best-1e-10){q=candidate;best=cost;accepted=true;break;}
            }
            if(!accepted)
            {
                // One difficult pose must not veto every other temporal
                // update. Minimize the SAME coupled objective in deterministic
                // blocks, re-projecting and validating each proposed pose.
                // Both neighbouring velocity and acceleration terms participate;
                // this is not a smoothing pass over independently solved poses.
                double LocalCost(int frame,double[] value)
                {
                    double[] At(int f)=>f==frame?value:q[f];
                    double cost=ActionCost(frame,value)+ObservationCost(frame,value)+value.Select((x,j)=>CoordinateWeight(j)*x*x).Sum();
                    for(int end=Math.Max(1,frame);end<=Math.Min(count-1,frame+1);end++)for(int j=0;j<variables.Count;j++)cost+=vWeight*Math.Pow(At(end)[j]-At(end-1)[j],2);
                    for(int end=Math.Max(2,frame);end<=Math.Min(count-1,frame+2);end++)for(int j=0;j<variables.Count;j++)cost+=aWeight*Math.Pow(At(end)[j]-2*At(end-1)[j]+At(end-2)[j],2);
                    return cost;
                }
                var order=temporal%2==0?Enumerable.Range(0,count):Enumerable.Range(0,count).Reverse();
                foreach(int frame in order)
                {
                    cancellation.ThrowIfCancellationRequested();double before=LocalCost(frame,q[frame]);
                    double start=.2/(1+4*vWeight+16*aWeight);
                    for(int attempt=0;attempt<3;attempt++)
                    {
                        double alpha=start/Math.Pow(2,attempt);var trial=q[frame].Select((x,j)=>x-alpha*gradient[frame][j]).ToArray();
                        if(!Project(frame,trial))continue;double after=LocalCost(frame,trial);
                        if(after<before-1e-10){q[frame]=trial;accepted=true;break;}
                    }
                }
                if(accepted)best=Objective(q);
            }
            if(!accepted)break;
        }
        var poses=Enumerable.Range(0,count).Select(f=>Pose(f,q[f]).Local).ToArray();
        // Choose an equivalent native Euler branch after solving. Track the
        // source's unwrapped increment so intentional turns are retained rather
        // than replaced by a shortest-path rotation across destination keys.
        for(int f=1;f<count;f++)for(int bone=0;bone<poses[f].Length;bone++)
        {
            var t=poses[f][bone];var near=poses[f-1][bone].Rotation+source[f][bone].Rotation-source[f-1][bone].Rotation;
            poses[f][bone]=t with{Rotation=AnimationControls.Euler((Transform.Identity with{Rotation=t.Rotation}).Matrix,near)};
        }
        float worst=(float)Enumerable.Range(0,count).Select(f=>TargetMotionConstraints.MaximumNormalizedViolation(plan,times[f],Pose(f,q[f]))).DefaultIfEmpty().Max();
        return new(new(times,poses,new(Version,count,variables.Count,projections,temporal,initial,best,worst,"Prepared quarter samples with actual-skin collision refinement; native curve/withheld acceptance is separate"){PoseEvaluations=poseEvaluations,ConstraintMeasure="Required vector norms, quaternion angle and full evaluated skin / unchanged frozen bounds",FeasibleStallExits=feasibleStalls,CollisionStallRefinements=collisionStallRefinements}){EffectiveClearance=plan.Clearance},[],[]);
    }
    internal static double[] ConjugateGradient(double[] rhs,Func<double[],double[]> multiply,int budget)
    {
        var x=new double[rhs.Length];var r=rhs.ToArray();var p=r.ToArray();double rr=r.Sum(v=>v*v);
        for(int i=0;i<budget&&rr>1e-14;i++){var ap=multiply(p);double denominator=p.Zip(ap,(a,b)=>a*b).Sum();if(denominator<=1e-30)break;double alpha=rr/denominator;for(int j=0;j<x.Length;j++){x[j]+=alpha*p[j];r[j]-=alpha*ap[j];}double next=r.Sum(v=>v*v);for(int j=0;j<x.Length;j++)p[j]=r[j]+next/rr*p[j];rr=next;}return x;
    }
}
