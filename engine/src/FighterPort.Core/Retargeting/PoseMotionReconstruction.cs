using System.Numerics;
using System.Text.Json.Serialization;

namespace FighterPort.Core;

// This receipt rates representation of transferred motion, not contact or action quality.
public record PoseMotionReceipt(string Version,int ReferenceSamples,int WithheldSamples,
    float MaximumSkinError,float SkinBound,float MaximumRotationRadians,float RotationBound,
    float MaximumChannelError,double WorstSkinFrame,double WorstRotationFrame,bool Passed,
    bool ExactNativeCurves,int KeyValueUpdates)
{
    public float MaximumTranslationError {get;init;}
    public float TranslationBound {get;init;}
    public float MaximumScaleError {get;init;}
    public float ScaleBound {get;init;}
    public float MaximumEulerChannelDegrees {get;init;}
    public float EulerChannelBoundDegrees {get;init;}
    public double WorstChannelFrame {get;init;}
    public int UnsupportedScaleSamples {get;init;}
    public string ProbeCoverage {get;init;}="";
    // Nullable so old receipts do not acquire invented refinement evidence.
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public float? InitialMaximumSkinError {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public float? InitialMaximumRotationRadians {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public float? InitialMaximumTranslationError {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public float? InitialMaximumScaleError {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public float? InitialMaximumEulerChannelDegrees {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? RefinementPasses {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? RefinedChannels {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public bool? RefinedOutputSelected {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? RefinementPolicy {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? AddedFractionalKeys {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? AdaptiveFittingSamples {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? ReservedWithheldSamples {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? MinimaxCalls {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public long? MinimaxWork {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? MinimaxLimitedChannels {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public bool? CandidatePassed {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public float? CandidateMaximumSkinError {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public float? CandidateMaximumRotationRadians {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public float? CandidateMaximumTranslationError {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public float? CandidateMaximumScaleError {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public float? CandidateMaximumEulerChannelDegrees {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? CandidateAddedFractionalKeys {get;init;}
}
public record PoseMotionCurves(BoneTrack[] Tracks,PoseMotionReceipt Receipt);

// Offline only. Integer pose values are immutable. Shared Hermite derivatives
// and explicit dyadic fractional knots may represent smooth subframe curvature;
// discontinuous/unrepresentable references retain failed independent bounds.
// No contact detector, pose optimizer, floor shift or playback state.
public static class PoseMotionReconstruction
{
    public const string Version="fixed-pose-native-fractional-refinement-7";
    public const int Substeps=16;
    public const float SkinScaleFraction=.0001f,RotationBound=.001f;
    public const float ScaleBound=.0001f,EulerChannelBoundDegrees=RotationBound*180/MathF.PI;
    public const int MaximumProbes=100000,MaximumBonePoses=2000000;
    public const int MaximumRefinementPasses=6;
    public const long MaximumCurveCollocations=100000000;
    // Offline resource policy, not a fidelity/contact tolerance. The36-file
    // Mario/Snake receipt2 corpus peaks at1,617,707 combined validation bone
    // poses and262,961,062 skin calls (both reference and output counted).
    public const long MaximumCombinedBonePoses=10000000,MaximumSkinEvaluations=1000000000;
    static long VertexCount(Scene scene)=>scene.Meshes.Sum(m=>(long)m.Vertices.Length);
    static int ProbeBudget(int sourceBones,int targetBones,long vertices)
    {
        long limit=Math.Min(MaximumProbes,MaximumBonePoses/targetBones);
        // Both candidate curves share the same independently evaluated reference.
        // Include one extra world pose for the optional reach preparation too.
        limit=Math.Min(limit,MaximumCombinedBonePoses/(sourceBones+4L*targetBones));
        if(vertices>0)limit=Math.Min(limit,vertices>MaximumSkinEvaluations/3?0:MaximumSkinEvaluations/(3*vertices));
        return (int)limit;
    }
    internal static void ValidateResourceBudget(Scene source,Scene target,int samples,bool closing,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        long keys=(long)samples+(closing?1:0),references=(keys-1)*Substeps+1;
        if(samples<1||keys<1||keys>100000||source.Bones.Length<1||target.Bones.Length<1||references*target.Bones.Length>MaximumBonePoses)
            throw new NotSupportedException("Pose reconstruction exceeds its declared offline key/target-bone-pose resource budget before sampling.");
        if(references*(source.Bones.Length+(long)target.Bones.Length)>MaximumCombinedBonePoses||
           references*(source.Bones.Length+4L*target.Bones.Length)>MaximumCombinedBonePoses)
            throw new NotSupportedException("Pose reconstruction exceeds its combined source/target bone-work resource budget before sampling.");
        // The fit lattice is also mandatory validation coverage. Reject its
        // predictable full-skin cost before preparing a pose or fitting a key.
        if(references>ProbeBudget(source.Bones.Length,target.Bones.Length,VertexCount(target)))
            throw new NotSupportedException("Pose reconstruction exceeds its full-skin validation resource budget before sampling; reference and both candidate output skin evaluations are counted.");
    }
    public static PoseMotionCurves Build(RetargetRequest request,RigDescriptor rig,BaselineMotionTransfer transfer,
        RetargetTiming timing,RationalRate rate,int samples,bool closing,bool held,bool exact,BodyReachFitting? bodyReach=null)
    {
        ValidateResourceBudget(request.Source.Scene,rig.Scene,samples,closing,request.Cancellation);
        int keys=samples+(closing?1:0),last=keys-1;
        // Native knots/extrema can add many probes beyond the fit lattice.
        // Their complete resource budget is known before coefficient fitting.
        var probes=ValidationProbes(request,rig,timing,rate,samples,last);
        long lattice=checked(last*Substeps+1L),sourceBones=request.Source.Scene.Bones.Length,targetBones=rig.Scene.Bones.Length;
        if(lattice*(sourceBones+2*targetBones)+(long)probes.Count*(sourceBones+4*targetBones)>MaximumCombinedBonePoses)
            throw new NotSupportedException("Pose reconstruction exceeds its aggregate prepared/reference/candidate bone-work resource budget before sampling.");
        if(!held&&!exact&&(long)probes.Count*rig.Scene.Bones.Length*9*(MaximumRefinementPasses+1)>MaximumCurveCollocations)
            throw new NotSupportedException("Adaptive native reconstruction exceeds its bounded curve-collocation resource budget before sampling.");
        bodyReach?.PrepareTrajectory(timing,rate,samples,closing,held,exact);
        bodyReach?.ValidateEvaluationBudget(checked(last*Substeps+1),probes.Count);
        Transform[] Motion(double sourceFrame,Vector3[]? near)=>bodyReach?.Evaluate(sourceFrame,near)??transfer.Evaluate(sourceFrame,near);
        var prepared=new Transform[checked(last*Substeps+1)][];Vector3[]? winding=null;
        double SourceFrame(double f)=>timing.SourceFrame(f,request.Clip.Rate.FramesPerSecond,rate.FramesPerSecond);
        for(int i=0;i<prepared.Length;i++)
        {
            request.Cancellation.ThrowIfCancellationRequested();
            var pose=Motion(SourceFrame(i/(double)Substeps),winding);
            if(pose.Any(t=>!t.Channels().All(float.IsFinite)))throw new NotSupportedException("Transferred motion contains nonfinite native channels.");
            prepared[i]=pose;winding=pose.Select(t=>t.Rotation).ToArray();
        }
        var initial=exact?transfer.IdentityTracks():Enumerable.Range(0,rig.Scene.Bones.Length).Select(bone=>new BoneTrack(rig.Scene.Bones[bone].Name,
            Enumerable.Range(0,9).Select(channel=>(Curve?)LatticeCurve(prepared,bone,channel,keys,held,request.Cancellation)).ToArray())).ToArray();
        var times=probes.ToArray();var referenceLocals=new Transform[times.Length][];
        var reserved=ReservedProbes(samples,last).Order().ToArray();
        bool IsReserved(double frame)
        {
            int at=Array.BinarySearch(reserved,frame);if(at>=0)return true;at=~at;
            // Rate conversion can move an otherwise equal time by an ulp.
            // Do not admit a nextafter copy of a withheld point into the fit.
            double epsilon=Math.Max(1e-12,Math.Abs(frame)*1e-12);
            return at<reserved.Length&&Math.Abs(reserved[at]-frame)<=epsilon||at>0&&Math.Abs(reserved[at-1]-frame)<=epsilon;
        }
        var fittingIndices=Enumerable.Range(0,times.Length).Where(i=>!IsReserved(times[i])).ToArray();
        var fittingTimes=fittingIndices.Select(i=>times[i]).ToArray();
        Vector3[]? validationWinding=null;
        for(int i=0;i<times.Length;i++)
        {
            request.Cancellation.ThrowIfCancellationRequested();
            // These are independent native knots/extrema/withheld times, not
            // samples of the candidate curve. Retain chronological winding.
            referenceLocals[i]=Motion(SourceFrame(times[i]),validationWinding);
            validationWinding=referenceLocals[i].Select(t=>t.Rotation).ToArray();
        }
        BoneTrack[] refined=initial;int passes=0,refinedChannels=0,minimaxCalls=0,minimaxLimited=0;
        long minimaxRemaining=SharedSlopeMinimax.MaximumWork;
        if(!held&&!exact)
        {
            refined=initial.ToArray();
            foreach(int bone in Enumerable.Range(0,initial.Length).OrderBy(b=>rig.CanonicalBoneRanks[b]))
            {
            var track=initial[bone];refined[bone]=track with{Channels=track.Channels.Select((curve,channel)=>
            {
                request.Cancellation.ThrowIfCancellationRequested();
                float limit=channel<3?ScaleBound:channel<6?EulerChannelBoundDegrees:rig.CharacteristicLength*SkinScaleFraction;
                var values=fittingIndices.Select(i=>referenceLocals[i][bone].Channels()[channel]).ToArray();
                var fit=RefineCurve(curve!,fittingTimes,values,limit,ref minimaxRemaining,request.Cancellation);
                var knots=RefineFractionalKnots(fit.Curve,fittingTimes,values,limit,request.Cancellation);
                minimaxCalls+=fit.MinimaxCalls;if(fit.MinimaxLimited)minimaxLimited++;passes=Math.Max(passes,fit.Passes);if(fit.Improved||knots.Improved)refinedChannels++;return knots.Curve;
            }).ToArray()};
            }
        }
        if(refined.SelectMany(t=>t.Channels.OfType<Curve>()).SelectMany(c=>c.Keys).Any(k=>!float.IsFinite(k.Value)||!float.IsFinite(k.InTangent)||!float.IsFinite(k.OutTangent)))
            throw new NotSupportedException("Fixed-pose reconstruction produced a nonfinite native coefficient; no invalid output was accepted.");
        var initialOutput=new AnimationClip(Guid.NewGuid(),"initial representation probe",samples,rate,request.Clip.Loop,initial);
        var refinedOutput=initialOutput with{Tracks=refined};
        float bound=rig.CharacteristicLength*SkinScaleFraction;
        var before=new Rating();var after=new Rating();bool candidate=refinedChannels>0;
        var vertices=rig.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();
        for(int i=0;i<times.Length;i++)
        {
            request.Cancellation.ThrowIfCancellationRequested();
            double frame=times[i];var local=referenceLocals[i];
            var reference=AnimationService.Evaluate(rig.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
            var original=AnimationService.Evaluate(rig.Scene,initialOutput,frame);
            var proposed=candidate?AnimationService.Evaluate(rig.Scene,refinedOutput,frame):original;
            before.Channels(local,original,frame,bound);if(candidate)after.Channels(local,proposed,frame,bound);
            for(int vertexIndex=0;vertexIndex<vertices.Length;vertexIndex++)
            {
                if((vertexIndex&255)==0)request.Cancellation.ThrowIfCancellationRequested();
                var vertex=vertices[vertexIndex];var expected=AnimationService.Skin(vertex,reference);
                before.Skin(Vector3.Distance(expected,AnimationService.Skin(vertex,original)),frame);
                if(candidate)after.Skin(Vector3.Distance(expected,AnimationService.Skin(vertex,proposed)),frame);
            }
        }
        // A smaller channel residual is insufficient if propagated geometry or
        // another rated channel becomes worse. Select using the independent
        // full representation gate, never the fit's own objective value.
        bool useRefined=candidate&&after.NoWorseThan(before)&&after.Quality(bound)<before.Quality(bound);var rating=useRefined?after:before;
        return new(useRefined?refined:initial,new(Version,prepared.Length,probes.Count,rating.SkinError,bound,rating.Rotation,RotationBound,rating.ChannelError,rating.WorstSkin,rating.WorstRotation,
            rating.Passed(bound),exact,0)
        {
            MaximumTranslationError=rating.Translation,TranslationBound=bound,MaximumScaleError=rating.Scale,ScaleBound=ScaleBound,
            MaximumEulerChannelDegrees=rating.Euler,EulerChannelBoundDegrees=EulerChannelBoundDegrees,WorstChannelFrame=rating.WorstChannel,
            UnsupportedScaleSamples=rating.UnsupportedScale,
            InitialMaximumSkinError=before.SkinError,InitialMaximumRotationRadians=before.Rotation,InitialMaximumTranslationError=before.Translation,
            InitialMaximumScaleError=before.Scale,InitialMaximumEulerChannelDegrees=before.Euler,RefinementPasses=passes,RefinedChannels=refinedChannels,RefinedOutputSelected=useRefined,
            AdaptiveFittingSamples=fittingTimes.Length,ReservedWithheldSamples=times.Length-fittingTimes.Length,
            MinimaxCalls=minimaxCalls,MinimaxWork=SharedSlopeMinimax.MaximumWork-minimaxRemaining,MinimaxLimitedChannels=minimaxLimited,
            CandidatePassed=candidate?after.Passed(bound):null,CandidateMaximumSkinError=candidate?after.SkinError:null,
            CandidateMaximumRotationRadians=candidate?after.Rotation:null,CandidateMaximumTranslationError=candidate?after.Translation:null,
            CandidateMaximumScaleError=candidate?after.Scale:null,CandidateMaximumEulerChannelDegrees=candidate?after.Euler:null,
            CandidateAddedFractionalKeys=candidate?refined.Sum(t=>t.Channels.OfType<Curve>().Sum(c=>c.Keys.Count(k=>k.Frame!=Math.Truncate(k.Frame)))):null,
            AddedFractionalKeys=useRefined?refined.Sum(t=>t.Channels.OfType<Curve>().Sum(c=>c.Keys.Count(k=>k.Frame!=Math.Truncate(k.Frame)))):0,
            RefinementPolicy="Six shared-derivative passes, at most32 convex-envelope minimax calls per failing curve under100m aggregate work, stop at half scalar bound or report work-limited channels; canonical rig order; then dyadic fractional-knot fitting. Immutable integral values, independent non-worsening full-skin/channel selection; limits are not convergence or native support",
            ProbeCoverage="Fit lattice, mapped native keys/extrema/quarters/adjacent samples and source-ancestry refinement; five separate withheld fractions per interval and end-exclusive sample never enter adaptive collocation; actual full-skin/orientation/channel rating; sampled evidence, not continuous-time proof"
        });
    }

    sealed class Rating
    {
        public float SkinError,Rotation,ChannelError,Translation,Scale,Euler;public double WorstSkin,WorstRotation,WorstChannel,ChannelViolation;public int UnsupportedScale;
        public double Quality(float bound)=>UnsupportedScale>0?double.PositiveInfinity:new[]{SkinError/bound,Rotation/RotationBound,Translation/bound,Scale/ScaleBound,Euler/EulerChannelBoundDegrees}.Max();
        public bool NoWorseThan(Rating other)=>SkinError<=other.SkinError&&Rotation<=other.Rotation&&Translation<=other.Translation&&Scale<=other.Scale&&Euler<=other.Euler&&UnsupportedScale<=other.UnsupportedScale;
        public bool Passed(float bound)=>SkinError<=bound&&Rotation<=RotationBound&&Translation<=bound&&Scale<=ScaleBound&&Euler<=EulerChannelBoundDegrees&&UnsupportedScale==0;
        public void Channels(Transform[] local,EvaluatedPose baked,double frame,float bound)
        {
            for(int bone=0;bone<local.Length;bone++)
            {
                var expected=PoseGeometry.Rotation((Transform.Identity with{Rotation=local[bone].Rotation}).Matrix);
                var actual=PoseGeometry.Rotation((Transform.Identity with{Rotation=baked.Local[bone].Rotation}).Matrix);
                var delta=Quaternion.Normalize(actual*Quaternion.Inverse(expected));
                float angle=2*MathF.Atan2(new Vector3(delta.X,delta.Y,delta.Z).Length(),Math.Abs(delta.W));
                if(!float.IsFinite(angle))throw new NotSupportedException("Native reconstruction evaluation produced a nonfinite orientation.");
                if(angle>Rotation){Rotation=angle;WorstRotation=frame;}
                var a=local[bone].Channels();var b=baked.Local[bone].Channels();
                if(baked.Local[bone].Scale.X<=0||baked.Local[bone].Scale.Y<=0||baked.Local[bone].Scale.Z<=0)UnsupportedScale++;
                for(int c=0;c<9;c++)
                {
                    float error=Math.Abs(a[c]-b[c]);if(!float.IsFinite(error))throw new NotSupportedException("Native reconstruction evaluation produced a nonfinite channel.");
                    ChannelError=Math.Max(ChannelError,error);float limit=c<3?ScaleBound:c<6?EulerChannelBoundDegrees:bound;
                    if(c<3)Scale=Math.Max(Scale,error);else if(c<6)Euler=Math.Max(Euler,error);else Translation=Math.Max(Translation,error);
                    double violation=error/limit;if(violation>ChannelViolation){ChannelViolation=violation;WorstChannel=frame;}
                }
            }
        }
        public void Skin(float error,double frame)
        {
            if(!float.IsFinite(error))throw new NotSupportedException("Native reconstruction evaluation produced nonfinite skinned geometry.");
            if(error>SkinError){SkinError=error;WorstSkin=frame;}
        }
    }

    static Curve LatticeCurve(Transform[][] prepared,int bone,int channel,int keys,bool held,CancellationToken cancellation)
    {
        var values=Enumerable.Range(0,keys).Select(k=>prepared[k*Substeps][bone].Channels()[channel]).ToArray();
        if(held)return new(values.Select((v,k)=>new Key(k,v)).ToArray(),Interpolation.Step);
        var rows=new List<Collocation>();
        for(int segment=0;segment<keys-1;segment++)for(int j=1;j<Substeps;j++)
        {
            if((segment&255)==0)cancellation.ThrowIfCancellationRequested();
            rows.Add(Collocation.At(segment+j/(double)Substeps,prepared[segment*Substeps+j][bone].Channels()[channel],values));
        }
        var slopes=SolveSlopes(values.Length,rows.ToArray(),Enumerable.Repeat(1d,rows.Count).ToArray(),cancellation);
        return MakeCurve(values,slopes);
    }

    internal record AdaptiveCurveFit(Curve Curve,int Passes,bool Improved,double InitialError,double Error)
    {public int MinimaxCalls {get;init;}public bool MinimaxLimited {get;init;}}
    readonly record struct Collocation(int Segment,double A,double B,double Fixed,double Value,double Frame)
    {
        public static Collocation At(double frame,double value,float[] keys)
        {
            int segment=(int)Math.Floor(frame);double u=frame-segment,u2=u*u,v=1-u,v2=v*v;
            // Factored basis retains the actual signs at nextafter probes:
            // subtracting cubic powers can cancel an interior coefficient to0.
            return new(segment,u*v2,-u2*v,v2*(1+2*u)*keys[segment]+u2*(3-2*u)*keys[segment+1],value,frame);
        }
    }
    static Curve MakeCurve(float[] values,double[] slopes)=>new(values.Select((v,k)=>new Key(k,v,(float)slopes[k],(float)slopes[k])).ToArray());
    static double[] SolveSlopes(int count,Collocation[] rows,double[] weights,CancellationToken cancellation)
    {
        var diagonal=new double[count];var upper=new double[count];var rhs=new double[count];
        double normalization=weights.DefaultIfEmpty(1).Max();if(normalization<=0||!double.IsFinite(normalization))throw new NotSupportedException("Adaptive native curve weights are nonfinite.");
        for(int i=0;i<rows.Length;i++)
        {
            if((i&255)==0)cancellation.ThrowIfCancellationRequested();var row=rows[i];int s=row.Segment;double w=weights[i]/normalization,residual=row.Value-row.Fixed;
            diagonal[s]+=w*row.A*row.A;diagonal[s+1]+=w*row.B*row.B;upper[s]+=w*row.A*row.B;
            rhs[s]+=w*row.A*residual;rhs[s+1]+=w*row.B*residual;
        }
        // One derivative per native knot, shared across both segments. These
        // are the only variables; regularization chooses zero at isolated knots.
        for(int k=0;k<count;k++){if((k&255)==0)cancellation.ThrowIfCancellationRequested();diagonal[k]+=1e-12;}
        for(int k=1;k<count;k++){if((k&255)==0)cancellation.ThrowIfCancellationRequested();double factor=upper[k-1]/diagonal[k-1];diagonal[k]-=factor*upper[k-1];rhs[k]-=factor*rhs[k-1];}
        var slopes=new double[count];for(int k=count-1;k>=0;k--){if((k&255)==0)cancellation.ThrowIfCancellationRequested();slopes[k]=(rhs[k]-(k<count-1?upper[k]*slopes[k+1]:0))/diagonal[k];}
        if(slopes.Any(v=>!double.IsFinite(v)||v>float.MaxValue||v<-float.MaxValue))throw new NotSupportedException("Adaptive native derivatives are not finite representable floats.");
        return slopes;
    }
    internal static AdaptiveCurveFit RefineCurve(Curve initial,double[] frames,float[] values,float bound,CancellationToken cancellation=default)
    {
        long remaining=SharedSlopeMinimax.MaximumWork;
        return RefineCurve(initial,frames,values,bound,ref remaining,cancellation);
    }
    internal static AdaptiveCurveFit RefineCurve(Curve initial,double[] frames,float[] values,float bound,ref long remaining,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();
        if(frames.Length!=values.Length||!float.IsFinite(bound)||bound<=0)throw new ArgumentException("Curve refinement needs paired samples and a finite positive bound.");
        if(frames.Length>MaximumProbes)throw new NotSupportedException("Adaptive native curve refinement exceeds its finite collocation budget.");
        for(int i=0;i<frames.Length;i++)
        {
            if((i&255)==0)cancellation.ThrowIfCancellationRequested();
            if(!double.IsFinite(frames[i])||!float.IsFinite(values[i])||i>0&&frames[i]<=frames[i-1])throw new ArgumentException("Adaptive curve samples must be finite and strictly time ordered.");
        }
        if(initial.Interpolation!=Interpolation.Hermite||initial.Keys.Length<2)return new(initial,0,false,0,0);
        if(initial.Keys.Where((k,i)=>k.Frame!=i||k.InTangent!=k.OutTangent).Any())throw new ArgumentException("Adaptive reconstruction requires unique integral native knots with one shared derivative.");
        var keys=initial.Keys.Select(k=>k.Value).ToArray();int last=keys.Length-1;
        var rows=frames.Select((frame,i)=>(frame,i)).Where(p=>p.frame>0&&p.frame<last&&p.frame!=Math.Truncate(p.frame)).Select(p=>Collocation.At(p.frame,values[p.i],keys)).ToArray();
        if(rows.Length==0)return new(initial,0,false,0,0);
        if(rows.Any(r=>!double.IsFinite(r.Value)||!double.IsFinite(r.Frame)))throw new NotSupportedException("Adaptive reconstruction samples are nonfinite.");
        var weights=new double[rows.Length];
        for(int i=0;i<rows.Length;i++)
        {
            if((i&255)==0)cancellation.ThrowIfCancellationRequested();var row=rows[i];
            // Temporal quadrature prevents clusters of nextafter/native-adjacent
            // probes from acquiring artificial mass in the initial LS fit.
            double previous=i>0&&rows[i-1].Segment==row.Segment?rows[i-1].Frame:row.Segment;
            double next=i+1<rows.Length&&rows[i+1].Segment==row.Segment?rows[i+1].Frame:row.Segment+1;
            weights[i]=Math.Max(double.Epsilon,(next-previous)/2);
        }
        double Error(Curve curve)=>rows.Max(r=>Math.Abs(r.Value-curve.Sample(r.Frame)));
        double initialError=Error(initial),bestError=initialError;var best=initial;int passes=0;
        // A scalar fit is not a propagated skin objective. Refitting already
        // sufficient channels can move their errors into the same phase and
        // make an otherwise better scalar candidate deform the body worse.
        // Retain their exact original coefficients; withheld full-skin/channel
        // validation still rates every channel, including these untouched ones.
        if(initialError<=bound)return new(initial,0,false,initialError,initialError);
        var current=initial;
        for(int pass=0;pass<MaximumRefinementPasses;pass++)
        {
            cancellation.ThrowIfCancellationRequested();current=MakeCurve(keys,SolveSlopes(keys.Length,rows,weights,cancellation));passes++;
            double maximum=Error(current);if(maximum<bestError){best=current;bestError=maximum;}
            if(maximum==0)break;
            // Lawson iteration approximates the maximum residual while keeping
            // the solve linear and banded. No private character/action tuning.
            for(int i=0;i<rows.Length;i++)
            {
                if((i&255)==0)cancellation.ThrowIfCancellationRequested();double residual=Math.Abs(rows[i].Value-current.Sample(rows[i].Frame));
                weights[i]*=Math.Max(1e-6,residual/maximum);
            }
        }
        int calls=0;bool limited=false;
        if(bestError>bound)
        {
            var fit=SharedSlopeMinimax.Fit(keys.Length,rows.Select(r=>new SharedSlopeMinimax.Equation(r.Segment,r.A,r.B,r.Value-r.Fixed)).ToArray(),
                best.Keys.Select(k=>(double)k.OutTangent).ToArray(),ref remaining,cancellation,bound*.5);
            calls=fit.Calls;limited=fit.WorkLimitReached;var candidate=MakeCurve(keys,fit.Slopes);double error=Error(candidate);
            if(error<bestError){best=candidate;bestError=error;}
        }
        return new(best,passes,!ReferenceEquals(best,initial),initialError,bestError){MinimaxCalls=calls,MinimaxLimited=limited};
    }

    internal static AdaptiveCurveFit RefineFractionalKnots(Curve initial,double[] frames,float[] values,float bound,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();
        if(frames.Length!=values.Length||bound<=0||!float.IsFinite(bound)||frames.Any(t=>!double.IsFinite(t))||values.Any(v=>!float.IsFinite(v)))
            throw new ArgumentException("Fractional native refinement requires paired finite times/values and a positive bound.");
        if(frames.Length>MaximumProbes)throw new NotSupportedException("Fractional native refinement exceeds the unchanged probe budget.");
        for(int i=1;i<frames.Length;i++)if(frames[i]<=frames[i-1])throw new ArgumentException("Fractional native collocations must be strictly ordered.");
        if(initial.Interpolation!=Interpolation.Hermite||initial.Keys.Length<2)return new(initial,0,false,0,0);
        int last=initial.Keys.Length-1;
        if(initial.Keys.Where((k,i)=>k.Frame!=i||k.InTangent!=k.OutTangent).Any())throw new ArgumentException("Fractional refinement starts from immutable integer native knots.");
        var rows=frames.Select((t,i)=>(Time:t,Value:values[i])).Where(p=>p.Time>0&&p.Time<last&&p.Time!=Math.Truncate(p.Time)).ToArray();
        if(rows.Length==0)return new(initial,0,false,0,0);
        double Error(Curve c)=>rows.Max(p=>Math.Abs(c.Sample(p.Time)-p.Value));
        double before=Error(initial);if(before<=bound)return new(initial,0,false,before,before);
        var bad=rows.Where(p=>Math.Abs(initial.Sample(p.Time)-p.Value)>bound*.5).Select(p=>(int)Math.Floor(p.Time)).ToHashSet();
        var knots=initial.Keys.ToDictionary(k=>k.Frame,k=>k.Value);
        foreach(var row in rows)
        {
            cancellation.ThrowIfCancellationRequested();
            if(bad.Contains((int)Math.Floor(row.Time))&&row.Time*Substeps==Math.Truncate(row.Time*Substeps))knots.TryAdd(row.Time,row.Value);
        }
        if(knots.Count==initial.Keys.Length)return new(initial,0,false,before,before);
        if(knots.Count>MaximumProbes)throw new NotSupportedException("Fractional native keys exceed the unchanged per-curve sample budget.");
        var ordered=knots.OrderBy(p=>p.Key).ToArray();var times=ordered.Select(p=>p.Key).ToArray();var coefficients=new List<Collocation>();var weights=new List<double>();
        for(int i=0;i<rows.Length;i++)
        {
            cancellation.ThrowIfCancellationRequested();var row=rows[i];int segment=Array.BinarySearch(times,row.Time);if(segment>=0)continue;segment=(~segment)-1;
            double dt=times[segment+1]-times[segment],u=(row.Time-times[segment])/dt,u2=u*u,u3=u2*u;
            coefficients.Add(new(segment,(u3-2*u2+u)*dt,(u3-u2)*dt,(2*u3-3*u2+1)*ordered[segment].Value+(-2*u3+3*u2)*ordered[segment+1].Value,row.Value,row.Time));
            double previous=i>0?rows[i-1].Time:0,next=i+1<rows.Length?rows[i+1].Time:last;
            weights.Add(Math.Max(double.Epsilon,(next-previous)/2));
        }
        var slopes=SolveSlopes(times.Length,coefficients.ToArray(),weights.ToArray(),cancellation);
        // A grid knot with no strictly interior collocation uses its local
        // divided difference, instead of a zero derivative from an empty row.
        var covered=coefficients.SelectMany(r=>new[]{r.Segment,r.Segment+1}).ToHashSet();
        for(int i=0;i<slopes.Length;i++)if(!covered.Contains(i))
        {int a=Math.Max(0,i-1),b=Math.Min(times.Length-1,i+1);slopes[i]=(ordered[b].Value-ordered[a].Value)/(times[b]-times[a]);}
        var curve=new Curve(ordered.Select((p,i)=>new Key(p.Key,p.Value,(float)slopes[i],(float)slopes[i])).ToArray());
        double after=Error(curve);return after<before?new(curve,1,true,before,after):new(initial,1,false,before,before);
    }

    static HashSet<double> ReservedProbes(int samples,int last)
    {
        var result=new HashSet<double>{Math.Max(last,samples-.0001)};
        for(int segment=0;segment<last;segment++)
        {
            foreach(double u in new[]{.2,1d/3,.381966011250105,.618033988749895,.8})result.Add(segment+u);
            for(int substep=0;substep<Substeps;substep++)result.Add(segment+(substep+.381966011250105)/Substeps);
        }
        return result;
    }

    static SortedSet<double> ValidationProbes(RetargetRequest request,RigDescriptor rig,RetargetTiming timing,RationalRate rate,int samples,int last)
    {
        double end=Math.Max(last,samples-.0001);int limit=ProbeBudget(request.Source.Scene.Bones.Length,rig.Scene.Bones.Length,VertexCount(rig.Scene));
        if(limit<1)throw new NotSupportedException("Native reconstruction has no positive probe budget for this rig.");
        var probes=new SortedSet<double>();
        void Add(double frame)
        {
            request.Cancellation.ThrowIfCancellationRequested();
            if(!double.IsFinite(frame))throw new NotSupportedException("A native validation time is nonfinite.");
            if(frame<0||frame>end)return;
            probes.Add(frame);if(probes.Count>limit)throw new NotSupportedException($"Native reconstruction exceeds its finite {limit}-probe validation budget; no incomplete representation pass was issued.");
        }
        void Native(double sourceFrame,bool adjacent=false)
        {
            double frame=timing.TargetFrame(sourceFrame,request.Clip.Rate.FramesPerSecond,rate.FramesPerSecond);Add(frame);
            if(adjacent&&frame>=0&&frame<=end)
            {
                Add(Math.BitDecrement(frame));Add(Math.BitIncrement(frame));
                Add(frame-1e-6);Add(frame+1e-6);
            }
        }
        void Segment(Key a,Key b,Interpolation interpolation)
        {
            double dt=b.Frame-a.Frame;if(dt<=0)return;
            foreach(double u in new[]{.25,.5,.75})Native(a.Frame+dt*u);
            if(interpolation!=Interpolation.Hermite)return;
            double cubic=2d*a.Value-2d*b.Value+dt*(a.OutTangent+b.InTangent);
            double quadratic=-3d*a.Value+3d*b.Value-dt*(2d*a.OutTangent+b.InTangent);
            double linear=dt*a.OutTangent;
            void Root(double u){if(double.IsFinite(u)&&u>0&&u<1)Native(a.Frame+dt*u,true);}
            double qa=3*cubic,qb=2*quadratic;
            if(qa==0){if(qb!=0)Root(-linear/qb);return;}
            double discriminant=qb*qb-4*qa*linear;if(discriminant<0)return;
            double root=Math.Sqrt(discriminant);Root((-qb-root)/(2*qa));Root((-qb+root)/(2*qa));
        }
        Add(0);Add(last);Add(end);
        for(int segment=0;segment<last;segment++)
        {
            foreach(double u in new[]{.2,1d/3,.381966011250105,.618033988749895,.8})Add(segment+u);
            // Potential fractional output intervals receive an interior fitting
            // midpoint and an independently withheld non-dyadic probe. They
            // never share a fitting observation, even after new keys appear.
            for(int substep=0;substep<2*Substeps;substep++)Add(segment+substep/(double)(2*Substeps));
            for(int substep=0;substep<Substeps;substep++)
            {
                Add(segment+(substep+.211324865405187)/Substeps); // fitting, distinct from reserved probes
                Add(segment+(substep+.381966011250105)/Substeps); // withheld only
            }
        }
        foreach(var track in request.Clip.Tracks)
        {
            foreach(var curve in track.Channels.OfType<Curve>())
            {
                foreach(var key in curve.Keys)Native(key.Frame,true);
                for(int i=1;i<curve.Keys.Length;i++)Segment(curve.Keys[i-1],curve.Keys[i],curve.Interpolation);
                if(curve.LoopFrameLimit is {} closure&&curve.Keys.Length>1&&closure+curve.Keys[0].Frame>curve.Keys[^1].Frame)
                    Segment(curve.Keys[^1],curve.Keys[0] with{Frame=closure+curve.Keys[0].Frame},curve.Interpolation);
            }
            if(track.Rotation is {} quaternion)
            {
                foreach(var key in quaternion.Keys)Native(key.Frame,true);
                for(int i=1;i<quaternion.Keys.Length;i++)foreach(double u in new[]{.25,.5,.75})Native(quaternion.Keys[i-1].Frame+(quaternion.Keys[i].Frame-quaternion.Keys[i-1].Frame)*u);
            }
        }
        // Every Euler component is monotonic between its known extrema. Bound
        // its source-ancestry angular travel before following the reference
        // branch, including unweighted helpers. Quaternion intervals retain
        // their real spherical/held semantics, not an Euler shortest-path guess.
        var scene=request.Source.Scene;var byBone=request.Clip.Tracks.ToDictionary(t=>t.Bone,StringComparer.Ordinal);
        double Motion(double first,double second)
        {
            var accumulated=new double[scene.Bones.Length];double maximum=0;
            double a=timing.SourceFrame(first,request.Clip.Rate.FramesPerSecond,rate.FramesPerSecond),b=timing.SourceFrame(second,request.Clip.Rate.FramesPerSecond,rate.FramesPerSecond);
            for(int bone=0;bone<scene.Bones.Length;bone++)
            {
                double local=0;
                if(byBone.TryGetValue(scene.Bones[bone].Name,out var track))
                {
                    if(track.Rotation is {} quaternion)
                    {
                        if(quaternion.Interpolation!=QuaternionInterpolation.Step)
                        {
                            var delta=Quaternion.Normalize(quaternion.Sample(b)*Quaternion.Inverse(quaternion.Sample(a)));
                            local=2*Math.Atan2(new Vector3(delta.X,delta.Y,delta.Z).Length(),Math.Abs(delta.W))*180/Math.PI;
                        }
                    }
                    else for(int channel=3;channel<6;channel++)if(track.Channels[channel] is {} curve&&curve.Interpolation!=Interpolation.Step)local+=Math.Abs((double)curve.Sample(b)-curve.Sample(a));
                }
                if(!double.IsFinite(local))throw new NotSupportedException("Source angular validation evidence is nonfinite.");
                accumulated[bone]=local+(scene.Bones[bone].Parent<0?0:accumulated[scene.Bones[bone].Parent]);maximum=Math.Max(maximum,accumulated[bone]);
            }
            return maximum;
        }
        var intervals=new Stack<(double First,double Last,int Depth)>();var ordered=probes.ToArray();
        for(int i=1;i<ordered.Length;i++)intervals.Push((ordered[i-1],ordered[i],0));
        while(intervals.TryPop(out var interval))
        {
            request.Cancellation.ThrowIfCancellationRequested();
            if(Motion(interval.First,interval.Last)<=90)continue;
            double middle=interval.First+(interval.Last-interval.First)/2;
            if(interval.Depth>=40||middle<=interval.First||middle>=interval.Last)
                throw new NotSupportedException("Source winding cannot be resolved inside the bounded native validation interval; no turn loss was accepted.");
            Add(middle);intervals.Push((interval.First,middle,interval.Depth+1));intervals.Push((middle,interval.Last,interval.Depth+1));
        }
        return probes;
    }
}
