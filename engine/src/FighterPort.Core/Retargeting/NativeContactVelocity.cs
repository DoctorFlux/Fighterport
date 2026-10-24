using System.Numerics;

namespace FighterPort.Core;

public record NativeContactVelocityReceipt(string Version,int PoseEvaluations,int EvaluationBudget,int EligibleKeys,int AcceptedKeys,
    int RejectedKeys,int SkippedKeys,bool BudgetExhausted,Dictionary<string,int> Reasons,double MaximumInitialVelocity,double MaximumAcceptedVelocity)
{
    public string VelocityUnits {get;init;}="Euclidean norm of required contact derivatives per native frame: position divided by characteristic length, orientation in radians. Numerical initialization evidence, not an acceptance tolerance.";
}
public record NativeContactVelocityResult(BoneTrack[] Tracks,NativeContactVelocityReceipt Receipt);

// Initialize the destination's single shared Hermite derivative on the tangent
// space of existing hard contacts. This changes no integer value, time, source
// channel policy or fixed socket. It is an offline seed, never acceptance.
public static class NativeContactVelocity
{
    public const string Version="required-contact-relative-velocity-1";
    const double ConfigurationStep=.001,FrameStep=.01,MaximumChannelCorrection=4;
    static readonly double[] Interior=[.125,.25,.375,.5,.625,.75,.875];
    readonly record struct Variable(int Bone,int Track,int Channel,double Unit);
    sealed class BudgetReached:Exception {}

    public static NativeContactVelocityResult Initialize(TargetMotionPlan plan,BoneTrack[] input,CancellationToken cancellation=default,int evaluationBudget=30000)
    {
        if(evaluationBudget<0)throw new ArgumentOutOfRangeException(nameof(evaluationBudget));
        int evaluations=0,eligible=0,accepted=0,rejected=0,skipped=0;
        double maximumBefore=0,maximumAfter=0;bool exhausted=false;
        var reasons=new Dictionary<string,int>(StringComparer.Ordinal);var current=input;
        void Reason(string reason)=>reasons[reason]=reasons.GetValueOrDefault(reason)+1;
        void Charge(){cancellation.ThrowIfCancellationRequested();if(evaluations>=evaluationBudget)throw new BudgetReached();evaluations++;}
        var scene=plan.Rig.Scene;float scale=plan.Rig.CharacteristicLength;
        if(!float.IsFinite(scale)||scale<=0)throw new InvalidDataException("Contact velocity requires a finite geometric scale.");
        var trackIndices=Enumerable.Range(0,scene.Bones.Length).Select(bone=>Array.FindIndex(input,track=>track.Bone==scene.Bones[bone].Name)).ToArray();
        if(trackIndices.Any(index=>index<0)||input.Length==0)throw new InvalidDataException("Native contact velocity requires a complete destination track set.");
        int keys=input[0].Channels[0]!.Keys.Length,root=plan.Rig.Binding.Roles["RootMotion"],body=plan.Rig.Binding.Roles.GetValueOrDefault("Hips",-1);
        var helpers=plan.Controls.PreservedHelperBones.ToHashSet();
        var limits=plan.Controls.JointLimits??[];
        var actionBones=plan.Baseline.Mapping.Bones.Where(mapping=>!mapping.DetailChannels).Select(mapping=>mapping.Target).ToArray();
        var bodyBones=PoseGeometry.BodyAdjustmentBones(plan.Controls);
        var template=new AnimationClip(Guid.NewGuid(),"Contact velocity evaluation",plan.Samples,plan.Rate,plan.Loop,input);
        EvaluatedPose Evaluate(Transform[] local,double frame)
        {
            Charge();
            return AnimationService.Evaluate(scene,null,0,local.Select((value,index)=>(value,index)).ToDictionary(pair=>pair.index,pair=>pair.value)) with{Seconds=frame/plan.Rate.FramesPerSecond};
        }
        EvaluatedPose CurvePose(BoneTrack[] tracks,double frame){Charge();return AnimationService.Evaluate(scene,template with{Tracks=tracks},frame);}
        static double Norm(double[] values)=>Math.Sqrt(values.Sum(value=>value*value));
        double[] Residual(ContactPin pin,EvaluatedPose pose)
        {
            var position=ContactGeometry.Residual(pin,pose)/scale;
            if(!pin.HoldRotation)return [position.X,position.Y,position.Z];
            var delta=Quaternion.Normalize(PoseGeometry.Rotation(pose.World[pin.Bone])*Quaternion.Inverse(ContactGeometry.TargetRotation(pin,pose)));
            if(delta.W<0)delta=new(-delta.X,-delta.Y,-delta.Z,-delta.W);
            var axis=new Vector3(delta.X,delta.Y,delta.Z);float length=axis.Length();
            var angle=length<1e-9f?Vector3.Zero:axis*(2*MathF.Atan2(length,Math.Abs(delta.W))/length);
            return [position.X,position.Y,position.Z,angle.X,angle.Y,angle.Z];
        }
        double ActionDifference(double frame,EvaluatedPose pose,EvaluatedPose reference)
        {
            bool fitting=plan.Grounding is {} ground&&SurfaceContacts.BodySupportedAt(ground.BodySupportPhases,ground.BodySupportedThroughout,ground.BodySupportedAtClosing,plan.SourceFrame(frame),plan.AuthenticSourceSamples);
            double maximum=0;
            foreach(int bone in actionBones)
            {
                if(fitting&&bodyBones.Contains(bone))continue;
                var a=PoseGeometry.FittingRotation(scene,reference,bone,bodyBones,fitting);var b=PoseGeometry.FittingRotation(scene,pose,bone,bodyBones,fitting);
                double dot=(double)a.X*b.X+(double)a.Y*b.Y+(double)a.Z*b.Z+(double)a.W*b.W;
                double aa=(double)a.X*a.X+(double)a.Y*a.Y+(double)a.Z*a.Z+(double)a.W*a.W,bb=(double)b.X*b.X+(double)b.Y*b.Y+(double)b.Z*b.Z+(double)b.W*b.W;
                double difference=1-Math.Min(1,Math.Abs(dot)/Math.Sqrt(aa*bb));
                if(!double.IsFinite(difference))return double.PositiveInfinity;
                maximum=Math.Max(maximum,difference);
            }
            return maximum;
        }
        try
        {
            for(int key=0;key<keys;key++)
            {
                cancellation.ThrowIfCancellationRequested();
                var active=plan.Pins(key).Where(pin=>!pin.SoftSupport||pin.RequiredSupport).ToArray();
                // An end-exclusive interval still owns the incoming derivative
                // at its continuous limit. Its integer value is NOT moved or
                // marked active: an incompatible endpoint needs curve-aware
                // integer fitting, not an invented extension of the contact.
                var incoming=plan.Graph.Relations.Where(contact=>contact.EndExclusive&&contact.EndFrame==key&&contact.Frame<key)
                    .SelectMany(contact=>contact.Pins).Where(pin=>(!pin.SoftSupport||pin.RequiredSupport)&&!active.Any(other=>other.Id==pin.Id)).DistinctBy(pin=>pin.Id).ToArray();
                if(active.Length==0&&incoming.Length==0)continue;
                var local=Enumerable.Range(0,scene.Bones.Length).Select(bone=>Transform.From(input[trackIndices[bone]].Channels.Select(curve=>curve!.Keys[key].Value).ToArray())).ToArray();
                var pose=Evaluate(local,key);
                var selected=new List<(ContactPin Pin,bool Left,bool Right,double Step)>();
                foreach(var pin in active.Concat(incoming))
                {
                    var intervals=plan.Graph.Relations.Where(contact=>contact.Pins.Any(other=>other.Id==pin.Id)).ToArray();
                    double leftSpan=intervals.Where(contact=>contact.Frame<key&&(contact.EndFrame??contact.Frame)>=key).Select(contact=>key-contact.Frame).DefaultIfEmpty().Max();
                    double rightSpan=intervals.Where(contact=>contact.Frame<=key&&(contact.EndFrame??contact.Frame)>key).Select(contact=>(contact.EndFrame??contact.Frame)-key).DefaultIfEmpty().Max();
                    bool left=key>0&&leftSpan>0,right=key<keys-1&&rightSpan>0;
                    double step=Math.Min(FrameStep,Math.Min(left?leftSpan:double.PositiveInfinity,right?rightSpan:double.PositiveInfinity)*.25);
                    string? reason=pin.SurfaceRegion.Length>0||pin.AllowedSurface is not null?"Switching material region or bounded-surface derivative needs an explicit smooth correspondence"
                        :pin.Reference?.SurfaceTripod.Length>0?"Deforming support-frame derivative has not been calibrated"
                        :pin.Mode is ContactMode.Free or ContactMode.Approach or ContactMode.Impact or ContactMode.Release||!left&&!right?"Isolated acquisition/impact/release has no persistent velocity constraint"
                        :null;
                    if(reason is null&&incoming.Any(other=>other.Id==pin.Id))
                    {
                        bool position=ContactGeometry.Residual(pin,pose).Length()<=plan.PositionBound;
                        var endpointRotation=Quaternion.Normalize(PoseGeometry.Rotation(pose.World[pin.Bone])*Quaternion.Inverse(ContactGeometry.TargetRotation(pin,pose)));
                        bool orientation=!pin.HoldRotation||2*Math.Atan2(new Vector3(endpointRotation.X,endpointRotation.Y,endpointRotation.Z).Length(),Math.Abs(endpointRotation.W))<=RetargetAcceptance.RotationRadians;
                        if(!position||!orientation)reason="Incoming end-exclusive key value fails required contact; curve-aware integer fitting is needed";
                        if(pin.Reference is {Held:true} reference&&reference.Motion.Skip(1).Any(motion=>motion.Seconds==pose.Seconds))
                            reason="Incoming held-reference discontinuity has no continuous shared native derivative";
                    }
                    if(reason is not null){Reason(reason);continue;}
                    selected.Add((pin,left,right,step));
                }
                if(selected.Count==0){skipped++;continue;}
                eligible++;
                var slopes=Enumerable.Range(0,scene.Bones.Length).Select(bone=>current[trackIndices[bone]].Channels.Select(curve=>(double)curve!.Keys[key].OutTangent).ToArray()).ToArray();
                if(slopes.SelectMany(value=>value).Any(value=>!double.IsFinite(value))){skipped++;Reason("Nonfinite native derivative");continue;}
                var pins=new List<ContactPin>();var timeVelocity=new List<double>();
                foreach(var item in selected)
                {
                    var center=Residual(item.Pin,pose);
                    var before=item.Left?Residual(item.Pin,pose with{Seconds=(key-item.Step)/plan.Rate.FramesPerSecond}):center;
                    var after=item.Right?Residual(item.Pin,pose with{Seconds=(key+item.Step)/plan.Rate.FramesPerSecond}):center;
                    // Reference evaluation also has work/dependency cost, even
                    // though no second bone pose is necessary for explicit time.
                    Charge();Charge();
                    var left=center.Zip(before,(a,b)=>(a-b)/item.Step).ToArray();
                    var right=after.Zip(center,(a,b)=>(a-b)/item.Step).ToArray();
                    if(item.Left&&item.Right&&Norm(left.Zip(right,(a,b)=>a-b).ToArray())>.001*(1+Norm(left)+Norm(right)))
                    {Reason("Support-reference velocity is discontinuous at this native key");continue;}
                    pins.Add(item.Pin);timeVelocity.AddRange(item.Left&&item.Right?left.Zip(right,(a,b)=>(a+b)*.5):item.Left?left:right);
                }
                if(pins.Count==0){skipped++;continue;}
                var affected=new HashSet<int>();
                void Add(int bone){for(;bone>=0&&bone!=root;bone=scene.Bones[bone].Parent)if(!helpers.Contains(bone)&&!plan.Rig.IsActorAncestor(bone))affected.Add(bone);}
                foreach(var pin in pins)
                {
                    Add(pin.Bone);if(pin.Reference?.Bone is {} attachment)Add(attachment);
                    foreach(var point in pin.SurfacePoints.Concat(pin.Reference?.SurfaceTripod??[]))for(int i=0;i<4;i++)if(point.Weights[i]>0)Add((int)point.Joints[i]);
                }
                var variables=affected.OrderBy(bone=>plan.Rig.CanonicalBoneRanks[bone]).SelectMany(bone=>Enumerable.Range(3,bone==body?6:3).Select(channel=>new Variable(bone,trackIndices[bone],channel,channel>=6?scale:180/Math.PI))).ToArray();
                if(variables.Length==0){skipped++;Reason("No permitted native derivative channel affects this contact");continue;}
                double[] All(EvaluatedPose sample)=>pins.SelectMany(pin=>Residual(pin,sample)).ToArray();
                Transform[] Shift(double[][] velocity,double amount)=>local.Select((transform,bone)=>Transform.From(transform.Channels().Select((value,channel)=>(float)(value+velocity[bone][channel]*amount)).ToArray())).ToArray();
                double[] Velocity(double[][] velocity)
                {
                    double maximum=velocity.SelectMany((channels,bone)=>channels.Select((value,channel)=>Math.Abs(value)/(channel>=6?scale:channel>=3?180/Math.PI:1))).DefaultIfEmpty().Max();
                    double step=Math.Min(FrameStep,.001/Math.Max(.001,maximum));
                    var plus=All(Evaluate(Shift(velocity,step),key));var minus=All(Evaluate(Shift(velocity,-step),key));
                    return plus.Zip(minus,(a,b)=>(a-b)/(2*step)).Zip(timeVelocity,(a,b)=>a+b).ToArray();
                }
                var velocity=Velocity(slopes);double initialNorm=Norm(velocity);
                maximumBefore=Math.Max(maximumBefore,initialNorm);
                if(!double.IsFinite(initialNorm)){skipped++;Reason("Nonfinite contact derivative");continue;}
                if(initialNorm<1e-7){skipped++;Reason("Contact derivative already stationary within numerical seed resolution");continue;}
                int rows=velocity.Length,columns=variables.Length;var jacobian=new double[rows,columns];
                for(int column=0;column<columns;column++)
                {
                    var variable=variables[column];var plus=local.ToArray();var minus=local.ToArray();
                    var high=local[variable.Bone].Channels();var low=high.ToArray();
                    high[variable.Channel]+=(float)(ConfigurationStep*variable.Unit);low[variable.Channel]-=(float)(ConfigurationStep*variable.Unit);
                    plus[variable.Bone]=Transform.From(high);minus[variable.Bone]=Transform.From(low);
                    var a=All(Evaluate(plus,key));var b=All(Evaluate(minus,key));
                    for(int row=0;row<rows;row++)jacobian[row,column]=(a[row]-b[row])/(2*ConfigurationStep);
                }
                // Minimum-change velocity projection. Fixed root, authored
                // scale and non-body translation already contribute to RHS.
                var rhs=new double[columns];
                for(int column=0;column<columns;column++)for(int row=0;row<rows;row++)rhs[column]-=jacobian[row,column]*velocity[row];
                double[] Multiply(double[] values)
                {
                    var rowValues=new double[rows];for(int row=0;row<rows;row++)for(int column=0;column<columns;column++)rowValues[row]+=jacobian[row,column]*values[column];
                    var result=new double[columns];for(int column=0;column<columns;column++){result[column]=values[column]*1e-10;for(int row=0;row<rows;row++)result[column]+=jacobian[row,column]*rowValues[row];}return result;
                }
                var delta=TemporalRetargetSolver.ConjugateGradient(rhs,Multiply,Math.Min(256,columns*4));
                if(delta.Any(value=>!double.IsFinite(value)||Math.Abs(value)>MaximumChannelCorrection))
                {rejected++;Reason("Contact derivative projection exceeded finite per-channel initialization budget");continue;}
                var frames=Enumerable.Range(Math.Max(0,key-1),Math.Min(keys-2,key)-Math.Max(0,key-1)+1).SelectMany(segment=>Interior.Select(u=>segment+u)).ToArray();
                var references=frames.Select(frame=>Evaluate(plan.Reference(frame),frame)).ToArray();
                var oldPoses=frames.Select(frame=>CurvePose(current,frame)).ToArray();
                var oldViolations=frames.Select((frame,index)=>TargetMotionConstraints.MaximumNormalizedViolation(plan,frame,oldPoses[index])).ToArray();
                double oldWorst=oldViolations.DefaultIfEmpty().Max(),oldCost=oldViolations.Sum(value=>value*value);
                double actionBudget=1-Math.Cos(RetargetAcceptance.MaximumAutomaticCorrectionDegrees*Math.PI/360);
                var actionBounds=frames.Select((frame,index)=>Math.Max(actionBudget,ActionDifference(frame,oldPoses[index],references[index]))).ToArray();
                bool retained=false;
                for(double alpha=1;alpha>=1d/128;alpha/=2)
                {
                    var proposed=slopes.Select(channels=>channels.ToArray()).ToArray();
                    for(int column=0;column<columns;column++){var variable=variables[column];proposed[variable.Bone][variable.Channel]+=alpha*delta[column]*variable.Unit;}
                    double afterNorm=Norm(Velocity(proposed));
                    if(!double.IsFinite(afterNorm)||afterNorm>=initialNorm-1e-9)
                    {Reason("Contact velocity candidate did not improve the finite required-contact derivative");continue;}
                    var candidate=current.Select(track=>track with{Channels=track.Channels.Select(curve=>curve is null?null:curve with{Keys=curve.Keys.ToArray()}).ToArray()}).ToArray();
                    foreach(var variable in variables)
                    {
                        var curve=candidate[variable.Track].Channels[variable.Channel]!;var old=curve.Keys[key];float tangent=(float)proposed[variable.Bone][variable.Channel];
                        curve.Keys[key]=old with{InTangent=tangent,OutTangent=tangent};
                    }
                    var poses=frames.Select(frame=>CurvePose(candidate,frame)).ToArray();
                    if(poses.Any(sample=>limits.Any(limit=>Enumerable.Range(0,3).Any(axis=>sample.Local[limit.Bone].Rotation[axis]<limit.MinimumDegrees[axis]-.0001f||sample.Local[limit.Bone].Rotation[axis]>limit.MaximumDegrees[axis]+.0001f))))
                    {Reason("Contact velocity candidate exceeded reviewed native joint limits");continue;}
                    var violations=frames.Select((frame,index)=>TargetMotionConstraints.MaximumNormalizedViolation(plan,frame,poses[index])).ToArray();
                    if(violations.Any(value=>!double.IsFinite(value)))
                    {Reason("Contact velocity candidate had a nonfinite sampled constraint residual");continue;}
                    bool worseWorst=violations.DefaultIfEmpty().Max()>oldWorst+1e-8,worseCost=violations.Sum(value=>value*value)>oldCost+1e-8;
                    if(worseWorst||worseCost)
                    {
                        if(worseWorst)Reason("Contact velocity candidate worsened the sampled worst required-constraint residual");
                        if(worseCost)Reason("Contact velocity candidate worsened the sampled aggregate required-constraint residual");
                        continue;
                    }
                    if(frames.Where((frame,index)=>ActionDifference(frame,poses[index],references[index])>actionBounds[index]+1e-12).Any())
                    {Reason("Contact velocity candidate exceeded the sampled action-distortion bound");continue;}
                    current=candidate;accepted++;maximumAfter=Math.Max(maximumAfter,afterNorm);retained=true;break;
                }
                if(!retained){rejected++;Reason("Velocity seed did not preserve sampled hard-constraint/action merit");}
            }
        }
        catch(BudgetReached){exhausted=true;Reason("Contact velocity initialization work budget reached; retained prior bounded result");}
        return new(current,new(Version,evaluations,evaluationBudget,eligible,accepted,rejected,skipped,exhausted,reasons,maximumBefore,maximumAfter));
    }
}
