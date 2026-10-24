using System.Numerics;
using System.Text.Json.Serialization;

namespace FighterPort.Core;

public record SceneEndpointGeometryReceipt(int SourceEndpoint,int TargetEndpoint,string[] SourceRoles,
    int SourceVertices,int TargetVertices,int SourceWeightedBones,int TargetWeightedBones,
    double SourceInfluenceArea,double TargetInfluenceArea,int SourceZeroAreaVertices,int TargetZeroAreaVertices)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public double? SourceRestRadius {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public double? TargetRestRadius {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public double? SourceEffectiveRadius {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public double? TargetEffectiveRadius {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public double? SourceAffinityRadius {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public double? SourceSoftMinimumBiasBound {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public double? TargetSoftMinimumBiasBound {get;init;}
}

public record SceneBodyFitReceipt(string Version,bool Enabled,int Evaluations,int AppliedProposals,int Unchanged,int Unsupported,int NonImproving,
    float MaximumCorrection,float MaximumInitialGap,float MaximumFinalGap,double WorstSourceFrame,int SourcePenetrationObservations)
{
    public string SourcePenetrationPolicy {get;init;}="DoNotCopySourcePenetration";
    public int SourceBody {get;init;}=-1;
    public int TargetBody {get;init;}=-1;
    public float MaximumSourcePenetration {get;init;}
    public string SourcePenetrationUnits {get;init;}="Source native model units";
    public int SourceRegions {get;init;}
    public int TargetRegions {get;init;}
    public int SourceSensors {get;init;}
    public int TargetSensors {get;init;}
    public float MaximumOmittedRestDistance {get;init;}
    public float MaximumSoftMinimumBias {get;init;}
    public float MaximumInitialPenetration {get;init;}
    public float MaximumFinalPenetration {get;init;}
    public int ClearanceClippedEvaluations {get;init;}
    public int ClearanceInfeasibleEvaluations {get;init;}
    public int UnmovableClearanceVertices {get;init;}
    public int ClearanceVertices {get;init;}
    public int FullSkinClearanceEvaluations {get;init;}
    public float MaximumClearanceAdjustment {get;init;}
    public string ClearancePolicy {get;init;}="Full default-visible overlap is measured, not constrained; no contact claim";
    public BodyVolumePolicy? VolumePolicy {get;init;}
    public string SourceWeightVersion {get;init;}="original-area-global-nearness-1";
    public double RobustScale {get;init;}
    public long? SensorWorkBudget {get;init;}
    public long? EstimatedSensorWork {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public ArticulatedSceneReceipt? Articulated {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? TranslationProposals {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? SourceEdgeAnchorPolicy {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public bool? FeatureGapMeasured {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? EndpointGeometryVersion {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public SceneEndpointGeometryReceipt[]? EndpointGeometry {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? PreparedObjectives {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? EndpointResidualPolicy {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public double? EndpointHingeTransitionWidth {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? EndpointDistanceVersion {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? EndpointDistancePolicy {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? EndpointSourceAffinityVersion {get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public string? EndpointFootprintVersion {get;init;}
    public string SourceAreaMeasure {get;init;}="Rest triangle-lumped material area evaluated on authentic posed/morphed geometry; not measured contact area";
    public string PositionUnits {get;init;}="Target native model units";
    public string Status {get;init;}="Review";
    public string[] OpenGates {get;init;}=[];
}

// Offline soft scene registration, not contact detection or a constraint solver.
// Reviewed native rotations and body translation can share one soft objective;
// explicit strict-volume mode retains the proven translation-only projection.
// A source-selected proximity observation is never a plant, grip or world lock.
public sealed class SceneBodyFitting
{
    public const string Version="native-soft-scene-body-16";
    public const string EndpointFootprintVersion="source-near-edge-material-extent-1";
    public const string EndpointGeometryVersion="native-descendant-material-closure-1";
    public const string EndpointDistanceVersion="native-descendant-material-distance-4";
    public const string EndpointSourceAffinityVersion="endpoint-and-global-source-nearness-1";
    public const int MaximumIterations=8,MaximumBacktracks=8;
    // Offline CPU-work ceiling, separate from geometric acceptance. Original
    // material sampling adds work; the prior2b ceiling rejected every supplied
    // corner clip before sampling. Jobs remain bounded and cancellable.
    public const long MaximumSensorWork=8000000000;
    const double PriorFraction=1e-8,SoftMinimumRadiusFraction=.005;
    public const double SourceNearnessScaleFraction=.05,RobustScaleFraction=.05;
    // Frozen soft-prior conditioning scale in target native units. This is
    // neither a collision allowance nor a changed contact acceptance bound.
    public const double EndpointHingeTransitionFraction=.001;
    public const double EndpointDistanceWeight=.25,EndpointRadiusGuardFraction=.01,EndpointTemperatureFraction=.005;
    readonly RetargetRequest request;
    readonly RigDescriptor source,target;
    readonly int sourceBody=-1,targetBody=-1;
    readonly Pair[] pairs=[];
    readonly BodySceneSensor[] sourceOriginal=[];
    readonly Vector3 sourceUp,targetUp,sourceInward,targetInward;
    readonly float sourceFloor,targetFloor,sourceEdgeOffset,targetEdgeOffset,length,fileRatio;
    readonly bool edge;
    readonly BodyVolumePolicy volumePolicy;
    readonly int sourceSensors,targetSensors,sourceRegionCount;
    readonly ClearanceSample[] clearance=[];
    readonly ArticulatedSceneFitting? articulated;
    readonly EndpointPair[] endpoints=[];
    int evaluations,improved,unchanged,unsupported,nonImproving,sourcePenetrations;
    int clearanceClipped,clearanceInfeasible,unmovableVertices,clearanceEvaluations;
    float maximumCorrection,maximumInitialGap,maximumFinalGap,maximumSourcePenetration,maximumBias;
    float maximumInitialPenetration,maximumFinalPenetration,maximumClearanceAdjustment;
    double worstSourceFrame;
    long? estimatedSensorWork;
    int translationProposals;
    int gapEvaluations;
    int preparedObjectives;
    sealed record Pair(BodySceneRegion Source,BodySceneRegion Target,double Weight);
    sealed record PreparedPair(Pair Pair,Sensor[] Source,Sensor[] Target);
    sealed record EndpointPair(int SourceEnd,int TargetEnd,string[] SourceRoles,
        BodySceneSensor[] Source,BodySceneSensor[] Target,int SourceWeightedBones,int TargetWeightedBones,
        double SourceRestRadius,double TargetRestRadius,double SourceRadius,double TargetRadius);
    sealed record PreparedEndpoint(EndpointPair Pair,Sensor[] Source,Sensor[] Target);
    sealed record SpatialPreparation(EvaluatedPose Pose,List<Observation> Observations,PreparedEndpoint[] Endpoints);
    sealed record Sensor(Vector3 Position,double Area,float Alpha,SkinSurfacePoint Point);
    sealed record ClearanceSample(SkinSurfacePoint Point,float Alpha);
    enum Feature {Top,Wall,Edge,Solid}
    sealed record Observation(Feature Feature,Sensor[] Target,double Weight,double Desired,double DesiredOther,double Temperature);
    readonly record struct Evaluation(double Cost,double GX,double GY,double GZ,double XX,double XY,double XZ,double YY,double YZ,double ZZ,float Gap,float Bias);
    public bool Enabled {get;}
    public bool ChangesGeometryIdentity {get;}
    public bool PreservesExactCurves=>!ChangesGeometryIdentity;
    public ArticulatedSceneFitting? Articulation=>articulated;
    public Diagnostic[] Diagnostics {get;}

    public SceneBodyFitting(RetargetRequest request,RigDescriptor source,RigDescriptor target,BaselineMotionTransfer baseline)
    {
        this.request=request;this.source=source;this.target=target;
        volumePolicy=(request.Fitting??new()).VolumePolicy;
        sourceUp=new(source.Basis.M21,source.Basis.M22,source.Basis.M23);targetUp=new(target.Basis.M21,target.Basis.M22,target.Basis.M23);
        length=target.CharacteristicLength;fileRatio=baseline.FileUnitRatio;
        var diagnostics=new List<Diagnostic>();
        void Review(string message)=>diagnostics.Add(new("SCENE_BODY_CONTEXT_REVIEW",message,false));
        if(!(request.Fitting??new()).AutomaticFitting){Diagnostics=[];return;}
        if(request.Ground is not {ExplicitSurface:true,SourceFloor:{} sf,TargetFloor:{} tf} ground)
        {Review("Soft body/scene registration requires explicitly supplied source and target geometry. No floor was inferred.");Diagnostics=diagnostics.ToArray();return;}
        sourceFloor=sf;targetFloor=tf;
        if(!float.IsFinite(sf)||!float.IsFinite(tf)||!float.IsFinite(length)||length<=0||!float.IsFinite(fileRatio)||fileRatio<=0)
        {Review("Scene heights, target body extent and declared file-unit conversion must be finite and positive where applicable.");Diagnostics=diagnostics.ToArray();return;}
        if((ground.SourceEdge is null)!=(ground.TargetEdge is null))
        {Review("Source and target need corresponding supplied edge geometry; an unmatched edge was not replaced by an infinite floor.");Diagnostics=diagnostics.ToArray();return;}
        if(ground.SourceEdge is {} se&&ground.TargetEdge is {} te)
        {
            se.Validate(sourceUp);te.Validate(targetUp);edge=true;sourceInward=se.Inward;targetInward=te.Inward;sourceEdgeOffset=se.Offset;targetEdgeOffset=te.Offset;
            if(Vector3.Dot(baseline.ConvertDirection(se.Inward),te.Inward)<.9999f)
            {Review("Supplied scene edges disagree under the reviewed rig-coordinate conversion.");Diagnostics=diagnostics.ToArray();return;}
        }
        if(!source.Binding.Roles.TryGetValue("Hips",out sourceBody)||!target.Binding.Roles.TryGetValue("Hips",out targetBody)||
            sourceBody==source.Binding.Roles["RootMotion"]||targetBody==target.Binding.Roles["RootMotion"]||
            !PoseGeometry.Ancestor(source.Scene,source.Binding.Roles["RootMotion"],sourceBody)||!PoseGeometry.Ancestor(target.Scene,target.Binding.Roles["RootMotion"],targetBody))
        {Review("Scene body translation needs reviewed native body bones separate from the actor channels; actor travel was not edited.");Diagnostics=diagnostics.ToArray();return;}
        bool Trusted(RetargetBonePair p)=>p.Binding is {Supplied:true,Reviewed:true,RequiresReview:false};
        if(!baseline.Mapping.Bones.Any(p=>p.Source==sourceBody&&p.Target==targetBody&&Trusted(p)))
        {Review("The source/target body correspondence is not supplied and reviewed.");Diagnostics=diagnostics.ToArray();return;}
        // Mapping reports retain destination-native storage order for channel
        // ownership. Numerical rows instead use the reviewed topology/geometry
        // canonical order: even identical residual rows summed in a different
        // native-index order can choose a different float-sensitive GN path.
        var correspondences=baseline.Mapping.Bones.Where(Trusted).OrderBy(p=>source.CanonicalBoneRanks[p.Source])
            .ThenBy(p=>target.CanonicalBoneRanks[p.Target]).ToArray();
        var a=BodySceneRegions.Prepare(source,sourceBody,request.Cancellation).ToDictionary(r=>r.Bone);
        var b=BodySceneRegions.Prepare(target,targetBody,request.Cancellation).ToDictionary(r=>r.Bone);
        sourceRegionCount=a.Count;sourceOriginal=a.Values.SelectMany(r=>r.OriginalSensors).ToArray();
        var sourceEnds=source.Binding.Chains.Where(c=>c.Reviewed).Select(c=>c.Bones[^1]).ToHashSet();
        var targetEnds=target.Binding.Chains.Where(c=>c.Reviewed).Select(c=>c.Bones[^1]).ToHashSet();
        // An endpoint need not have a direct native weight. Every material
        // vertex under its existing native descendants (fingers, toes, helpers
        // or unfamiliar branches) still belongs to its geometry. Merge by
        // material identity, not location, preserving all original influences.
        BodySceneSensor[] Subtree(RigDescriptor rig,Dictionary<int,BodySceneRegion> regions,int end)
        {
            var material=new Dictionary<(int Mesh,int Vertex),BodySceneSensor>();
            foreach(var region in regions.Values.Where(r=>PoseGeometry.Ancestor(rig.Scene,end,r.Bone)))
                foreach(var sensor in region.OriginalSensors)
                {
                    request.Cancellation.ThrowIfCancellationRequested();var key=(sensor.Point.Mesh,sensor.Point.Vertex);
                    if(material.TryGetValue(key,out var prior))
                        material[key]=prior with{Area=prior.Area+sensor.Area};
                    else material.Add(key,sensor);
                }
            // Each region's area already includes its actual direct native
            // influence mass; summing those masses once is the subtree measure.
            // Zero-area geometry is retained for the separate min-clearance
            // screen, never assigned an invented area or normalized weight.
            return material.OrderBy(p=>p.Key.Mesh).ThenBy(p=>p.Key.Vertex).Select(p=>p.Value).ToArray();
        }
        double RestRadius(RigDescriptor rig,BodySceneSensor[] material)
        {
            if(material.Length==0)return 0;
            var points=new Vector3[material.Length];double x=0,y=0,z=0;
            for(int i=0;i<points.Length;i++)
            {
                if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();var p=ContactGeometry.Point(material[i].Point,rig.Rest);
                if(!PoseGeometry.Finite(p))throw new NotSupportedException("Endpoint rest material geometry must be finite; no native bind was changed.");
                points[i]=p;x+=p.X;y+=p.Y;z+=p.Z;
            }
            x/=points.Length;y/=points.Length;z/=points.Length;double square=0;
            for(int i=0;i<points.Length;i++)
            {
                if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();double dx=points[i].X-x,dy=points[i].Y-y,dz=points[i].Z-z;
                square=Math.Max(square,dx*dx+dy*dy+dz*dz);
            }
            return Math.Sqrt(square);
        }
        endpoints=correspondences.Where(p=>sourceEnds.Contains(p.Source)&&targetEnds.Contains(p.Target)&&
            PoseGeometry.Ancestor(source.Scene,sourceBody,p.Source)&&PoseGeometry.Ancestor(target.Scene,targetBody,p.Target))
            .Select(p=>
            {
                var sourceMaterial=Subtree(source,a,p.Source);var targetMaterial=Subtree(target,b,p.Target);
                double sourceRadius=RestRadius(source,sourceMaterial),targetRadius=RestRadius(target,targetMaterial);
                return new EndpointPair(p.Source,p.Target,
                source.Binding.Chains.Where(c=>c.Reviewed&&c.Bones[^1]==p.Source).Select(c=>c.Role).Distinct().Order(StringComparer.Ordinal).ToArray(),
                sourceMaterial,targetMaterial,
                a.Keys.Count(bone=>PoseGeometry.Ancestor(source.Scene,p.Source,bone)),b.Keys.Count(bone=>PoseGeometry.Ancestor(target.Scene,p.Target,bone)),
                sourceRadius,targetRadius,Math.Max(sourceRadius,source.CharacteristicLength*EndpointRadiusGuardFraction),Math.Max(targetRadius,length*EndpointRadiusGuardFraction));
            })
            .Where(p=>p.Source.Length>0&&p.Target.Length>0).ToArray();
        var mapped=correspondences.Where(p=>a.ContainsKey(p.Source)&&b.ContainsKey(p.Target)&&
            double.IsFinite(a[p.Source].Area)&&a[p.Source].Area>0&&double.IsFinite(b[p.Target].Area)&&b[p.Target].Area>0).ToArray();
        double area=mapped.Sum(p=>a[p.Source].Area);
        if(!double.IsFinite(area)||area<0||area==0&&endpoints.Length==0)
        {Review("No reviewed corresponding source/target skin regions have nonzero surface area.");Diagnostics=diagnostics.ToArray();return;}
        pairs=area>0?mapped.Select(p=>new Pair(a[p.Source],b[p.Target],a[p.Source].Area/area)).ToArray():[];
        sourceSensors=sourceOriginal.Length;targetSensors=pairs.Sum(p=>p.Target.Sensors.Length);
        if(!pairs.SelectMany(p=>p.Target.Sensors).Concat(endpoints.SelectMany(p=>p.Target)).Any(p=>p.BodyInfluence>0))
        {Review("Mapped target geometry has no influence from the permitted body translation channel.");Diagnostics=diagnostics.ToArray();return;}
        Enabled=true;
        articulated=new(target,targetBody,baseline.Mapping,request.TargetRig.JointLimits??[],request.Cancellation);
        var belowBody=Enumerable.Range(0,target.Scene.Bones.Length).Select(bone=>PoseGeometry.Ancestor(target.Scene,targetBody,bone)).ToArray();
        clearance=target.SkinPoints.Where(p=>p.Mesh>=0&&DefaultVisible(target.Scene.Meshes[p.Mesh])).Select(p=>
        {
            float alpha=0;for(int slot=0;slot<4;slot++)if(p.Weights[slot]>0&&belowBody[(int)p.Joints[slot]])alpha+=p.Weights[slot];
            return new ClearanceSample(p,alpha);
        }).ToArray();
        // Exact graph identity alone says nothing about differing body volumes.
        // Conservative geometry/context identity is safe only without active
        // companion layers or differing native default visibility.
        bool GeometryIdentity()
        {
            if(source.Scene.Meshes.Length!=target.Scene.Meshes.Length)return false;
            var corresponding=baseline.Mapping.Bones.ToDictionary(p=>p.Source,p=>p.Target);
            foreach(var (sourceMesh,targetMesh) in source.Scene.Meshes.Zip(target.Scene.Meshes))
            {
                if(sourceMesh.Vertices.Length!=targetMesh.Vertices.Length||!sourceMesh.Indices.SequenceEqual(targetMesh.Indices)||DefaultVisible(sourceMesh)!=DefaultVisible(targetMesh))return false;
                for(int i=0;i<sourceMesh.Vertices.Length;i++)
                {
                    if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();var x=sourceMesh.Vertices[i];var y=targetMesh.Vertices[i];
                    if(x.Position!=y.Position||x.Weights!=y.Weights)return false;
                    for(int slot=0;slot<4;slot++)if(x.Weights[slot]>0&&corresponding[(int)x.Joints[slot]]!=(int)y.Joints[slot])return false;
                }
            }
            return true;
        }
        ChangesGeometryIdentity=!(baseline.ExactRig&&GeometryIdentity()&&sf==tf&&
            ground.SourceEdge==ground.TargetEdge&&sourceUp==targetUp&&request.Source.NativeLayers.Length==0&&request.Target.NativeLayers.Length==0);
        diagnostics.Add(new("SCENE_BODY_FITTING_REVIEW","Soft source-selected skin/scene proximity fits reviewed native rotations and permitted body translation; strict-volume mode retains translation-only projection. Contacts, temporal continuity, action and full clearance remain unvalidated; actor travel is preserved.",false));
        Diagnostics=diagnostics.ToArray();
    }

    public void ValidateEvaluationBudget(int references,int probes,int extraCosts=0)
    {
        request.Cancellation.ThrowIfCancellationRequested();
        if(references<0||probes<0||extraCosts<0)throw new ArgumentOutOfRangeException(nameof(references));
        if(!Enabled||!ChangesGeometryIdentity)return;
        long sourceScreen=endpoints.Sum(p=>(long)p.Source.Length),targetScreen=endpoints.Sum(p=>(long)p.Target.Length);
        if(sourceScreen>int.MaxValue||targetScreen>int.MaxValue)throw new NotSupportedException("Full endpoint geometry exceeds the supported material-reference count before sampling.");
        if(volumePolicy==BodyVolumePolicy.ReportOverlap)articulated?.ValidateEvaluationBudget(references,probes,targetSensors,edge?4:1,(int)targetScreen,endpoints.Length,extraCosts);
        long queries=(long)references+probes,features=edge?4:1;
        // Initial actual source/target skin, at most8 linearizations and64
        // candidate costs, and final actual target-skin verification. A skin
        // sample's complete four-influence body response is cached exactly.
        // Count actual skin samples plus feature-position evaluations. Source
        // preparation needs four passes per feature (global proximity, global
        // area mass, mapped area mass and the source observation). Positions are
        // skinned only once even when several native influences reference them.
        // Each candidate caches one field evaluation per target sensor; moment
        // accumulation reuses those values. Final affine verification samples
        // both the accepted and original real target skin.
        // Full endpoint closure screens positions/fields at every candidate
        // and may need columns for EVERY violating original material vertex.
        // Articulation's shared32b preflight counts that full-column worst case.
        long screen=volumePolicy==BodyVolumePolicy.ReportOverlap?(edge?4L:2L)*sourceScreen+(edge?2L:1L)*(74L+extraCosts)*targetScreen:0;
        long work=2L*sourceSensors+3L*targetSensors+features*(4L*sourceSensors+(74L+extraCosts)*targetSensors)+4L*clearance.Length+screen;
        estimatedSensorWork=work>0&&queries>long.MaxValue/work?long.MaxValue:queries*work;
        if(work>0&&queries>MaximumSensorWork/work)
            throw new NotSupportedException($"Soft scene/body preparation requires at most {estimatedSensorWork} feature/sensor work units; its offline resource limit is {MaximumSensorWork}. No pose was sampled and no anatomy or contact contradiction is implied.");
        long bones=source.Scene.Bones.Length+4L*target.Scene.Bones.Length;
        if((long)references*bones>PoseMotionReconstruction.MaximumCombinedBonePoses||(long)probes*(bones+target.Scene.Bones.Length)>PoseMotionReconstruction.MaximumCombinedBonePoses)
            throw new NotSupportedException("Soft scene/body fitting exceeds its combined extra-target-world evaluation budget before sampling.");
    }

    public Transform[] Fit(EvaluatedPose authentic,Transform[] local,Func<int,bool>? sourceMeshVisible=null)
    {
        request.Cancellation.ThrowIfCancellationRequested();evaluations++;
        if(local.Length!=target.Scene.Bones.Length||authentic.Local.Length!=source.Scene.Bones.Length)throw new ArgumentException("Scene fitting needs the prepared source and target native graphs.");
        if(!Enabled||!ChangesGeometryIdentity){unchanged++;return local.ToArray();}
        var prepared=PrepareSpatial(authentic,local,sourceMeshVisible);var pose=prepared.Pose;var observations=prepared.Observations;
        // Direct fitting and trajectory fitting consume the same frozen source
        // observations; only their optimizer and temporal organization differ.
        if(volumePolicy==BodyVolumePolicy.ReportOverlap&&articulated is {Enabled:true})
        {
            if(observations.Count==0&&prepared.Endpoints.Length==0){unsupported++;return local.ToArray();}
            var articulatedResult=articulated.Fit(local,BuildObjective(prepared));RecordPose(local,articulatedResult);return articulatedResult;
        }
        if(observations.Count==0){unsupported++;return local.ToArray();}
        Vector3 delta=Vector3.Zero;var initial=Evaluate(observations,delta);var current=initial;
        for(int iteration=0;iteration<MaximumIterations;iteration++)
        {
            request.Cancellation.ThrowIfCancellationRequested();var step=Solve(current);
            if(!PoseGeometry.Finite(step)){Record(initial,initial,authentic.Seconds);unsupported++;return local.ToArray();}
            if(step.LengthSquared()<=length*length*1e-16f)break;
            if(step.Length()>length)step*=length/step.Length();
            bool accepted=false;
            for(int backtrack=0;backtrack<MaximumBacktracks;backtrack++)
            {
                request.Cancellation.ThrowIfCancellationRequested();var proposed=delta+step;var candidate=Evaluate(observations,proposed);
                if(candidate.Cost<current.Cost){delta=proposed;current=candidate;accepted=true;break;}step*=.5f;
            }
            if(!accepted)break;
        }
        // Both saved volume policies measure every default-visible material
        // vertex. Only the explicit strict policy constrains this volume; the
        // default reports overlap without raising the body for a protrusion.
        var points=new Vector3[clearance.Length];var depths=new float[clearance.Length];float beforeDepth=0;
        for(int i=0;i<clearance.Length;i++)
        {
            if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();points[i]=ContactGeometry.Point(clearance[i].Point,pose);
            if(!PoseGeometry.Finite(points[i]))throw new NotSupportedException("Full target clearance geometry is nonfinite.");
            depths[i]=Depth(points[i]);beforeDepth=Math.Max(beforeDepth,depths[i]);
        }
        clearanceEvaluations++;maximumInitialPenetration=Math.Max(maximumInitialPenetration,beforeDepth);
        var softDelta=delta;
        bool strict=volumePolicy==BodyVolumePolicy.RequireVertexClearance;
        if(strict&&!ProjectClearance(points,depths,ref delta))
        {
            Record(initial,initial,authentic.Seconds);maximumFinalPenetration=Math.Max(maximumFinalPenetration,beforeDepth);clearanceInfeasible++;return local.ToArray();
        }
        if(delta!=softDelta){clearanceClipped++;maximumClearanceAdjustment=Math.Max(maximumClearanceAdjustment,Vector3.Distance(delta,softDelta));}
        current=Evaluate(observations,delta);
        if(delta==Vector3.Zero){Record(initial,initial,authentic.Seconds);maximumFinalPenetration=Math.Max(maximumFinalPenetration,beforeDepth);unchanged++;return local.ToArray();}
        int parent=target.Scene.Bones[targetBody].Parent;
        if(parent<0||!Matrix4x4.Invert(pose.World[parent],out var inverse)){Record(initial,initial,authentic.Seconds);maximumFinalPenetration=Math.Max(maximumFinalPenetration,beforeDepth);unsupported++;return local.ToArray();}
        var result=local.ToArray();result[targetBody]=local[targetBody] with{Translation=local[targetBody].Translation+Vector3.TransformNormal(delta,inverse)};
        if(!result[targetBody].Channels().All(float.IsFinite)){Record(initial,initial,authentic.Seconds);maximumFinalPenetration=Math.Max(maximumFinalPenetration,beforeDepth);unsupported++;return local.ToArray();}
        // Reevaluate the real native transforms/skin once at the accepted point;
        // partial-body influences and parent-scale conversion must agree with
        // the exact affine translation response used by the line search.
        var actual=Pose(result);float mismatch=0,afterDepth=0;bool worsened=false,invalidGeometry=false;
        foreach(var pair in pairs)foreach(var sensor in pair.Target.Sensors)
        {
            request.Cancellation.ThrowIfCancellationRequested();if(sensor.Point.Mesh>=0&&!DefaultVisible(target.Scene.Meshes[sensor.Point.Mesh]))continue;
            mismatch=Math.Max(mismatch,Vector3.Distance(ContactGeometry.Point(sensor.Point,actual),ContactGeometry.Point(sensor.Point,pose)+sensor.BodyInfluence*delta));
        }
        for(int i=0;i<clearance.Length;i++)
        {
            if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();var point=ContactGeometry.Point(clearance[i].Point,actual);
            if(!PoseGeometry.Finite(point)){invalidGeometry=true;break;}
            float depth=Depth(point);afterDepth=Math.Max(afterDepth,depth);if(depth>depths[i])worsened=true;
        }
        if(invalidGeometry)
        {
            Record(initial,initial,authentic.Seconds);maximumFinalPenetration=Math.Max(maximumFinalPenetration,beforeDepth);unsupported++;return local.ToArray();
        }
        if(strict&&worsened)
        {
            Record(initial,initial,authentic.Seconds);maximumFinalPenetration=Math.Max(maximumFinalPenetration,beforeDepth);clearanceInfeasible++;return local.ToArray();
        }
        if(!float.IsFinite(mismatch)||mismatch>length*1e-5f){Record(initial,initial,authentic.Seconds);maximumFinalPenetration=Math.Max(maximumFinalPenetration,beforeDepth);unsupported++;return local.ToArray();}
        maximumFinalPenetration=Math.Max(maximumFinalPenetration,afterDepth);
        // Explicit strict clearance projection may increase a soft action/scene
        // cost. Preserve that distinction from an improving soft proposal.
        if(current.Cost>=initial.Cost)nonImproving++;
        Record(initial,current,authentic.Seconds);improved++;translationProposals++;maximumCorrection=Math.Max(maximumCorrection,delta.Length());return result;
    }

    SpatialPreparation PrepareSpatial(EvaluatedPose authentic,Transform[] incoming,Func<int,bool>? sourceMeshVisible)
    {
        var pose=Pose(incoming);var observations=new List<Observation>();var preparedPairs=new List<PreparedPair>();
        var features=edge?new[]{Feature.Top,Feature.Wall,Feature.Edge,Feature.Solid}:new[]{Feature.Top};
        var closest=features.ToDictionary(f=>f,_=>double.PositiveInfinity);
        var sourceMass=features.ToDictionary(f=>f,_=>0d);
        var sourceWorld=new Dictionary<(int Mesh,int Vertex),Vector3>();var targetWorld=new Dictionary<(int Mesh,int Vertex),Vector3>();
        bool SourceVisible(int mesh)=>sourceMeshVisible?.Invoke(mesh)??(mesh>=0&&mesh<source.Scene.Meshes.Length&&DefaultVisible(source.Scene.Meshes[mesh]));
        var allSource=Sensors(sourceOriginal,authentic,SourceVisible,sourceWorld);
        foreach(var feature in features)
        {
            int index=0;foreach(var sensor in allSource)
            {
                if((index++&255)==0)request.Cancellation.ThrowIfCancellationRequested();
                closest[feature]=Math.Min(closest[feature],SourceDistance(sensor,feature));
            }
        }
        double sigma=source.CharacteristicLength*SourceNearnessScaleFraction;
        foreach(var feature in features)
        {
            int index=0;foreach(var sensor in allSource)
            {
                if((index++&255)==0)request.Cancellation.ThrowIfCancellationRequested();
                double distance=SourceDistance(sensor,feature)-closest[feature];
                sourceMass[feature]+=sensor.Area*Math.Exp(-distance*distance/(2*sigma*sigma));
            }
        }
        int sourceIndex=0;foreach(var sensor in allSource)
        {
            if((sourceIndex++&255)==0)request.Cancellation.ThrowIfCancellationRequested();
            double y=Vector3.Dot(sensor.Position,sourceUp)-sourceFloor,z=edge?Vector3.Dot(sensor.Position,sourceInward)-sourceEdgeOffset:0;
            double penetration=edge?Math.Max(0,Math.Min(-y,z)):Math.Max(0,-y);
            if(penetration>0){sourcePenetrations++;maximumSourcePenetration=Math.Max(maximumSourcePenetration,(float)penetration);}
        }
        foreach(var pair in pairs)
        {
            request.Cancellation.ThrowIfCancellationRequested();
            var a=Sensors(pair.Source.OriginalSensors,authentic,SourceVisible,sourceWorld);
            var b=Sensors(pair.Target.Sensors,pose,p=>p>=0&&p<target.Scene.Meshes.Length&&DefaultVisible(target.Scene.Meshes[p]),targetWorld);
            if(a.Length==0||b.Length==0)continue;
            preparedPairs.Add(new(pair,a,b));
        }
        foreach(var prepared in preparedPairs)
        {
            var pair=prepared.Pair;var a=prepared.Source;var b=prepared.Target;
            double radius=Math.Max(pair.Source.Radius,source.CharacteristicLength*.001f),targetRadius=Math.Max(pair.Target.Radius,length*.001f);
            double temperature=Math.Max(targetRadius*SoftMinimumRadiusFraction,length*1e-7);
            foreach(var feature in features)
            {
                double mass=0;
                int index=0;foreach(var sensor in a)
                {
                    if((index++&255)==0)request.Cancellation.ThrowIfCancellationRequested();
                    double distance=SourceDistance(sensor,feature)-closest[feature];
                    mass+=sensor.Area*Math.Exp(-distance*distance/(2*sigma*sigma));
                }
                if(mass<=0||!double.IsFinite(mass)||sourceMass[feature]<=0||!double.IsFinite(sourceMass[feature]))continue;
                double weight=mass/sourceMass[feature];
                if(feature==Feature.Edge)
                {
                    double nearestDistance=a.Min(s=>SourceDistance(s,Feature.Edge));double total=0,sy=0,sz=0;
                    foreach(var sensor in a){double distance=SourceDistance(sensor,Feature.Edge)-nearestDistance;
                        double w=sensor.Area*Math.Exp(-distance/sigma);total+=w;
                        double sampleY=Vector3.Dot(sensor.Position,sourceUp)-sourceFloor,sampleZ=Vector3.Dot(sensor.Position,sourceInward)-sourceEdgeOffset;
                        if(sampleY<0&&sampleZ>0){if(-sampleY<sampleZ)sampleY=0;else sampleZ=0;}
                        sy+=w*sampleY;sz+=w*sampleZ;}
                    // The edge is one shared geometric feature with two normal
                    // coordinates, not two unrelated hand targets.
                    double y=sy/total,z=sz/total;
                    observations.Add(new(feature,b,weight,y*fileRatio,z*fileRatio,temperature));
                }
                else
                {
                    var value=SoftFeature(a,Vector3.Zero,feature,sourceUp,sourceInward,sourceFloor,sourceEdgeOffset,edge,
                        Math.Max(radius*SoftMinimumRadiusFraction,source.CharacteristicLength*1e-7));
                    maximumBias=Math.Max(maximumBias,value.Bias*fileRatio);
                    // Finite feature distances are unsigned and continuous.
                    // Only the separate continuous solid SDF (or infinite
                    // floor) is signed and invokes source-defect projection.
                    bool signed=feature==Feature.Solid||!edge;
                    observations.Add(new(feature,b,weight,(signed?Math.Max(0,value.Value):value.Value)*fileRatio,0,temperature));
                }
            }
        }
        var preparedEndpoints=volumePolicy==BodyVolumePolicy.ReportOverlap&&articulated is {Enabled:true}
            ?endpoints.Select(p=>new PreparedEndpoint(p,
                Sensors(p.Source,authentic,SourceVisible,sourceWorld,true),
                Sensors(p.Target,pose,m=>m>=0&&m<target.Scene.Meshes.Length&&DefaultVisible(target.Scene.Meshes[m]),targetWorld,true)))
                .Where(p=>p.Source.Length>0&&p.Target.Length>0).ToArray():[];
        return new(pose,observations,preparedEndpoints);
    }

    Func<EvaluatedPose,NativePoseDifferentials?,ArticulatedResiduals> BuildObjective(SpatialPreparation prepared)
    {
        var observations=prepared.Observations;
        var free=prepared.Endpoints.Select(p=>
        {
            var fields=p.Source.Select(s=>FeatureValue(s,Vector3.Zero,Feature.Solid,sourceUp,sourceInward,sourceFloor,sourceEdgeOffset,edge)).ToArray();
            double gap=fields.Min(f=>f.Value),minimum=MaterialMinimum(fields,EndpointTemperature(p.Pair.SourceRadius,source.CharacteristicLength,fields.Length)).Value;
            // Local body volume is not evidence of scene attachment. A broad
            // free hand must not gain floor affinity merely from its size.
            // Reuse the existing target-independent global source band as a
            // cap; target proportions still affect only the fit/normalization.
            double affinity=Math.Min(p.Pair.SourceRadius,source.CharacteristicLength*SourceNearnessScaleFraction);
            double positive=Math.Max(0,minimum),confidence=Math.Exp(-.5*Math.Pow(positive/affinity,2));
            double weight=EndpointDistanceWeight/Math.Max(1,prepared.Endpoints.Length)*confidence*Math.Pow(length/p.Pair.TargetRadius,2);
            var footprint=edge?EndpointEdgeObservation(p.Source.Select(s=>(double)Vector3.Dot(s.Position,sourceUp)-sourceFloor).ToArray(),
                p.Source.Select(s=>(double)Vector3.Dot(s.Position,sourceInward)-sourceEdgeOffset).ToArray(),affinity):(Desired:0d,Confidence:0d);
            return(Target:p.Target,Lower:Math.Min(Math.Max(0,gap)*fileRatio,length*.05),
                Desired:positive*fileRatio,Weight:weight,Temperature:EndpointTemperature(p.Pair.TargetRadius,length,p.Target.Length),
                Footprint:footprint.Desired*fileRatio,FootprintWeight:EndpointDistanceWeight/Math.Max(1,prepared.Endpoints.Length)*footprint.Confidence*Math.Pow(length/p.Pair.TargetRadius,2));
        }).ToArray();
        ArticulatedResiduals Residuals(EvaluatedPose candidate,NativePoseDifferentials? differential)
            {
                int variables=differential?.Variables??0;var derivatives=new Dictionary<(int,int),Vector3[]>();var cache=new Dictionary<(int,int),Vector3>();
                void EnsureColumns(Sensor sensor)
                {
                    var key=(sensor.Point.Mesh,sensor.Point.Vertex);
                    if(differential is not null&&!derivatives.ContainsKey(key))
                        derivatives.Add(key,Enumerable.Range(0,variables).Select(v=>differential.Point(sensor.Point,v)).ToArray());
                }
                Sensor[] Current(Sensor[] sensors,bool columns=true)
                {
                    var points=new Sensor[sensors.Length];
                    for(int i=0;i<sensors.Length;i++)
                    {
                        if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();var sensor=sensors[i];var key=(sensor.Point.Mesh,sensor.Point.Vertex);
                        if(!cache.TryGetValue(key,out var position)){position=ContactGeometry.Point(sensor.Point,candidate);cache.Add(key,position);}
                        if(!PoseGeometry.Finite(position))throw new NotSupportedException("Prepared endpoint skin produced a nonfinite material vertex.");
                        if(columns)EnsureColumns(sensor);points[i]=sensor with{Position=position};
                    }
                    return points;
                }
                var result=new List<double>();var gradients=new List<double[]>();double robust=source.CharacteristicLength*RobustScaleFraction*fileRatio;
                double Derivative(Sensor s,FieldSample f,int variable)
                {var d=derivatives[(s.Point.Mesh,s.Point.Vertex)][variable];return f.DX*d.X+f.DY*d.Y+f.DZ*d.Z;}
                void Add(double residual,double weight,double[] derivative,bool robustLoss=true)
                {
                    double root=robustLoss?Math.Sqrt(1+residual*residual/(robust*robust)):1;
                    double scale=Math.Sqrt(weight*2/(root+1))/length;
                    double slope=robustLoss?scale*(1-residual*residual/(2*robust*robust*root*(root+1))):scale;
                    result.Add(residual*scale);gradients.Add(derivative.Select(d=>d*slope).ToArray());
                }
                foreach(var observation in observations)
                {
                    request.Cancellation.ThrowIfCancellationRequested();var sensors=Current(observation.Target);double residual;var gradient=new double[variables];
                    if(observation.Feature!=Feature.Edge)
                    {
                        var fields=sensors.Select(s=>FeatureValue(s with{Alpha=1},Vector3.Zero,observation.Feature,targetUp,targetInward,targetFloor,targetEdgeOffset,edge)).ToArray();
                        double minimum=fields.Min(f=>f.Value),sum=0,area=0;var weights=new double[sensors.Length];
                        for(int i=0;i<sensors.Length;i++){weights[i]=sensors[i].Area*Math.Exp(-(fields[i].Value-minimum)/observation.Temperature);sum+=weights[i];area+=sensors[i].Area;}
                        residual=minimum-observation.Temperature*Math.Log(sum/area)-observation.Desired;
                        for(int i=0;i<sensors.Length;i++)for(int v=0;v<variables;v++)gradient[v]+=weights[i]/sum*Derivative(sensors[i],fields[i],v);
                    }
                    else
                    {
                        var terms=EdgeNeighborhood(sensors,Vector3.Zero,targetUp,targetInward,targetFloor,targetEdgeOffset,source.CharacteristicLength*SourceNearnessScaleFraction*fileRatio);
                        double cy=terms.Sum(t=>t.Weight*t.Y),cz=terms.Sum(t=>t.Weight*t.Z),edgeY=cy-observation.Desired,edgeZ=cz-observation.DesiredOther;
                        residual=Math.Sqrt(edgeY*edgeY+edgeZ*edgeZ);
                        if(residual>0)for(int i=0;i<sensors.Length;i++)for(int v=0;v<variables;v++)
                        {
                            var d=derivatives[(sensors[i].Point.Mesh,sensors[i].Point.Vertex)][v];
                            double dy=targetUp.X*d.X+targetUp.Y*d.Y+targetUp.Z*d.Z,dz=targetInward.X*d.X+targetInward.Y*d.Y+targetInward.Z*d.Z;
                            double dd=terms[i].Distance>0?(terms[i].Y*dy+terms[i].Z*dz)/terms[i].Distance:0;
                            double q=-dd/(source.CharacteristicLength*SourceNearnessScaleFraction*fileRatio);
                            gradient[v]+=terms[i].Weight*(edgeY*(dy+(terms[i].Y-cy)*q)+edgeZ*(dz+(terms[i].Z-cz)*q))/residual;
                        }
                    }
                    Add(residual,observation.Weight,gradient);
                }
                // One actual material/SDF pass is shared by symmetric standoff
                // and the independent one-sided C2/L4 lower-clearance rows.
                var endpointFields=free.Select(f=>
                {
                    var sensors=Current(f.Target,false);var fields=new FieldSample[sensors.Length];
                    for(int i=0;i<sensors.Length;i++)
                    {
                        if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();
                        fields[i]=FeatureValue(sensors[i] with{Alpha=1},Vector3.Zero,Feature.Solid,targetUp,targetInward,targetFloor,targetEdgeOffset,edge);
                    }
                    return(End:f,Sensors:sensors,Fields:fields);
                }).ToArray();
                foreach(var endpoint in endpointFields)
                {
                    var f=endpoint.End;var sensors=endpoint.Sensors;var fields=endpoint.Fields;
                    var minimum=MaterialMinimum(fields,f.Temperature);var gradient=new double[variables];
                    if(f.Weight>0)for(int i=0;i<sensors.Length;i++)
                    {
                        if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();EnsureColumns(sensors[i]);
                        for(int v=0;v<variables;v++)gradient[v]+=minimum.Weights[i]*Derivative(sensors[i],fields[i],v);
                    }
                    // Source geometry supplies an observed standoff, not a
                    // floor plant or world lock. A far-away source region has
                    // low confidence; source defects are not copied as goals.
                    Add(minimum.Value-f.Desired,f.Weight,gradient,false);
                }
                if(edge)foreach(var endpoint in endpointFields)
                {
                    var f=endpoint.End;var sensors=endpoint.Sensors;
                    var coordinates=sensors.Select(s=>(double)Vector3.Dot(s.Position,targetInward)-targetEdgeOffset).ToArray();
                    var extent=MaterialMinimum(coordinates,f.Temperature);var gradient=new double[variables];
                    if(f.FootprintWeight>0)for(int i=0;i<sensors.Length;i++)
                    {
                        if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();EnsureColumns(sensors[i]);
                        for(int v=0;v<variables;v++)gradient[v]+=extent.Weights[i]*Vector3.Dot(derivatives[(sensors[i].Point.Mesh,sensors[i].Point.Vertex)][v],targetInward);
                    }
                    // Preserve source signed overhang: source-near geometry
                    // supplies a soft extent goal, never an inside-only pin.
                    Add(extent.Value-f.Footprint,f.FootprintWeight,gradient,false);
                }
                // Positive authentic clearance is a soft free-extremity prior,
                // not a plant/lock or permission to infer a contact schedule.
                // The L4 aggregate has no discrete target MinBy tie: coplanar
                // points with different rotation levers all contribute smooth
                // derivatives. Every original material vertex, including an
                // orphan with zero triangle area, remains a screening sample.
                // This equal-material-point measure is NOT posed area or
                // tessellation invariant. Its norm never dilutes one isolated
                // finger violation through a /point-count normalization.
                foreach(var endpoint in endpointFields)
                {
                    var f=endpoint.End;var sensors=endpoint.Sensors;var fields=endpoint.Fields;
                    var raw=fields.Select(field=>f.Lower-field.Value).ToArray();double width=length*EndpointHingeTransitionFraction;
                    var softened=raw.Select(h=>EndpointHinge(h,width)).ToArray();var hinges=softened.Select(h=>h.Value).ToArray();double maximum=hinges.Max();var gradient=new double[variables];
                    double residual=maximum>0?maximum*Math.Pow(hinges.Sum(h=>Math.Pow(h/maximum,4)),.25):0;
                    if(residual>0)for(int i=0;i<sensors.Length;i++)
                    {
                        if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();if(hinges[i]<=0)continue;
                        EnsureColumns(sensors[i]);double factor=Math.Pow(hinges[i]/residual,3)*softened[i].Slope;
                        for(int v=0;v<variables;v++)gradient[v]-=factor*Derivative(sensors[i],fields[i],v);
                    }
                    Add(residual,.25/Math.Max(1,free.Length),gradient,false);
                }
                var jacobian=new double[result.Count,variables];for(int i=0;i<result.Count;i++)for(int v=0;v<variables;v++)jacobian[i,v]=gradients[i][v];
                return new(result.ToArray(),jacobian);
            }
        return Residuals;
    }

    internal static (double Desired,double Confidence) EndpointEdgeObservation(double[] heights,double[] inward,double affinity)
    {
        if(heights.Length==0||heights.Length!=inward.Length||!double.IsFinite(affinity)||affinity<=0||heights.Any(v=>!double.IsFinite(v))||inward.Any(v=>!double.IsFinite(v)))
            throw new ArgumentException("Edge material observations need paired finite geometry and a positive source affinity.");
        double extent=inward.Min(),top=Enumerable.Range(0,heights.Length).Min(i=>Math.Sqrt(heights[i]*heights[i]+Math.Pow(Math.Min(0,inward[i]),2)));
        return(extent,Math.Exp(-.5*(Math.Pow(top/affinity,2)+Math.Pow(extent/affinity,2))));
    }

    internal static double EndpointTemperature(double radius,double length,int points)
    {
        if(!double.IsFinite(radius)||!double.IsFinite(length)||radius<=0||length<=0||points<1)throw new ArgumentException("Endpoint geometry needs finite positive scales and material count.");
        // Retain the independently checked geometric conditioning scale.
        // A tightly sharpened log-partition failed unit/axis covariance; it
        // must not be promoted by relaxing the existing acceptance bounds.
        return radius*EndpointTemperatureFraction;
    }
    internal static (double Value,double[] Weights) MaterialMinimum(double[] values,double temperature)
    {
        double minimum=values.Min();var probabilities=values.Select(f=>Math.Exp(-(f-minimum)/temperature)).ToArray();
        double sum=probabilities.Sum();for(int i=0;i<probabilities.Length;i++)probabilities[i]/=sum;
        // Boltzmann-weighted material distance removes the log(N/active-mass)
        // offset. It is still a soft estimate, not an exact vertex minimum.
        double value=Enumerable.Range(0,values.Length).Sum(i=>probabilities[i]*values[i]);
        var gradient=Enumerable.Range(0,values.Length).Select(i=>probabilities[i]*(1-(values[i]-value)/temperature)).ToArray();
        return(value,gradient);
    }
    static (double Value,double[] Weights) MaterialMinimum(FieldSample[] fields,double temperature)=>MaterialMinimum(fields.Select(f=>f.Value).ToArray(),temperature);

    // A raw positive hinge has an O(1) GN column immediately below clearance
    // and zero immediately above it. This C2 transition makes both that column
    // and its slope vanish at the boundary while retaining every deeper value
    // EXACTLY. It conditions a soft objective; raw geometry is still measured.
    static (double Value,double Slope) EndpointHinge(double h,double width)
    {
        if(h<=0)return(0,0);if(h>=width)return(h,1);
        double t=h/width,t2=t*t;
        return(width*t2*t*(6+t*(-8+3*t)),t2*(18+t*(-32+15*t)));
    }

    // Freezes authentic source observations only. The returned evaluator has
    // no per-pose solve, playback history or mutable warm-start dependency.
    public Func<EvaluatedPose,NativePoseDifferentials?,ArticulatedResiduals>? PrepareObjective(
        EvaluatedPose authentic,Transform[] incoming,Func<int,bool>? sourceVisible=null)
    {
        request.Cancellation.ThrowIfCancellationRequested();ValidateGraphs(authentic,incoming);
        if(!Enabled||!ChangesGeometryIdentity||volumePolicy!=BodyVolumePolicy.ReportOverlap||articulated is not {Enabled:true})return null;
        var prepared=PrepareSpatial(authentic,incoming,sourceVisible);
        if(prepared.Observations.Count==0&&prepared.Endpoints.Length==0)return null;
        preparedObjectives++;return BuildObjective(prepared);
    }

    void ValidateGraphs(EvaluatedPose authentic,Transform[] local)
    {
        if(local.Length!=target.Scene.Bones.Length||authentic.Local.Length!=source.Scene.Bones.Length)
            throw new ArgumentException("Scene fitting needs the prepared source and target native graphs.");
    }

    // Records one actually published collocation pose, not a successful solve.
    // Full default-visible material overlap remains a measured review result.
    public void RecordPreparedPose(EvaluatedPose authentic,Transform[] input,Transform[] result)
    {
        request.Cancellation.ThrowIfCancellationRequested();ValidateGraphs(authentic,input);ValidateGraphs(authentic,result);evaluations++;
        RecordPose(input,result);
    }
    void RecordPose(Transform[] input,Transform[] result)
    {
        if(!Enabled||!ChangesGeometryIdentity){unchanged++;return;}
        var before=Pose(input);var after=Pose(result);float initialDepth=0,finalDepth=0;
        for(int i=0;i<clearance.Length;i++)
        {
            if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();var sample=clearance[i];
            var a=ContactGeometry.Point(sample.Point,before);var b=ContactGeometry.Point(sample.Point,after);
            if(!PoseGeometry.Finite(a)||!PoseGeometry.Finite(b))throw new NotSupportedException("Prepared scene geometry is nonfinite.");
            initialDepth=Math.Max(initialDepth,Depth(a));finalDepth=Math.Max(finalDepth,Depth(b));
        }
        clearanceEvaluations++;maximumInitialPenetration=Math.Max(maximumInitialPenetration,initialDepth);maximumFinalPenetration=Math.Max(maximumFinalPenetration,finalDepth);
        if(!input.SequenceEqual(result))
        {
            improved++;if(input[targetBody].Translation!=result[targetBody].Translation)translationProposals++;
            maximumCorrection=Math.Max(maximumCorrection,Vector3.Distance(before.World[targetBody].Translation,after.World[targetBody].Translation));
        }
        else unchanged++;
    }

    float Depth(Vector3 point)
    {
        float y=Vector3.Dot(point,targetUp)-targetFloor;
        return edge?Math.Max(0,Math.Min(-y,Vector3.Dot(point,targetInward)-targetEdgeOffset)):Math.Max(0,-y);
    }
    bool ProjectClearance(Vector3[] points,float[] depths,ref Vector3 delta)
    {
        if(!edge)
        {
            double lower=double.NegativeInfinity;
            for(int i=0;i<points.Length;i++)
            {
                if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();float alpha=clearance[i].Alpha;
                double y=Vector3.Dot(points[i],targetUp)-targetFloor;
                if(alpha<=0){if(y<0){unmovableVertices++;return false;}continue;}
                lower=Math.Max(lower,-y/alpha);
            }
            double requested=Vector3.Dot(delta,targetUp);
            if(requested<lower)
            {
                // Outward numerical margin is conservative geometry, not a
                // relaxed collision tolerance or a support/contact offset.
                double outward=length*1e-6;delta+=targetUp*(float)((lower-requested+outward)/targetUp.LengthSquared());
            }
            return PoseGeometry.Finite(delta)&&delta.Length()<=MaximumIterations*length;
        }
        // This offline optimization step is not an exported time segment. A
        // feasible endpoint must not be discarded because a tiny transverse
        // component would first deepen an already intersecting starting point.
        // Actual native skin is still checked again at the returned pose.
        bool endpointFeasible=true;
        for(int i=0;i<points.Length;i++)
        {
            if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();var point=points[i]+clearance[i].Alpha*delta;
            if(!PoseGeometry.Finite(point)||Depth(point)>depths[i]){endpointFeasible=false;break;}
        }
        if(endpointFeasible)return PoseGeometry.Finite(delta)&&delta.Length()<=MaximumIterations*length;
        double fraction=1,up=Vector3.Dot(delta,targetUp),inward=Vector3.Dot(delta,targetInward);
        for(int i=0;i<points.Length;i++)
        {
            if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();double alpha=clearance[i].Alpha;
            if(alpha<=0){if(depths[i]>0)unmovableVertices++;continue;}
            double y=Vector3.Dot(points[i],targetUp)-targetFloor,z=Vector3.Dot(points[i],targetInward)-targetEdgeOffset;
            // A vertex is forbidden to enter y<-initialDepth,z>initialDepth.
            // Both half-space inequalities are linear along this body step.
            // This also protects already intersecting vertices from worsening.
            double lo=0,hi=fraction;
            bool Interval(double value,double velocity)
            {
                if(velocity==0)return value>0;
                double boundary=-value/velocity;if(velocity>0)lo=Math.Max(lo,boundary);else hi=Math.Min(hi,boundary);return lo<hi;
            }
            if(Interval(-y-depths[i],-alpha*up)&&Interval(z-depths[i],alpha*inward)&&lo<hi&&hi>0&&lo<fraction)
                fraction=Math.Max(0,lo);
        }
        if(fraction<1)
        {
            double outward=length*1e-6/Math.Max(delta.Length(),length*1e-12f);
            delta*=Math.Max(0,MathF.BitDecrement((float)Math.Max(0,fraction-outward)));
        }
        return PoseGeometry.Finite(delta)&&delta.Length()<=MaximumIterations*length;
    }

    void Record(Evaluation initial,Evaluation final,double seconds)
    {
        gapEvaluations++;
        maximumInitialGap=Math.Max(maximumInitialGap,initial.Gap);maximumBias=Math.Max(maximumBias,final.Bias);
        if(final.Gap>=maximumFinalGap){maximumFinalGap=final.Gap;worstSourceFrame=seconds*request.Clip.Rate.FramesPerSecond;}
    }

    static bool DefaultVisible(Mesh mesh)=>mesh.DrawCalls.Length==0||mesh.DrawCalls.Any(d=>d.DefaultVisible);
    double SourceDistance(Sensor sensor,Feature feature)
    {
        if(feature!=Feature.Edge)return Math.Abs(FeatureValue(sensor,Vector3.Zero,feature,sourceUp,sourceInward,sourceFloor,sourceEdgeOffset,edge).Value);
        double y=Vector3.Dot(sensor.Position,sourceUp)-sourceFloor,z=Vector3.Dot(sensor.Position,sourceInward)-sourceEdgeOffset;return Math.Sqrt(y*y+z*z);
    }
    Sensor[] Sensors(BodySceneSensor[] sensors,EvaluatedPose pose,Func<int,bool> visible,Dictionary<(int Mesh,int Vertex),Vector3>? worldCache=null,bool keepZeroArea=false)
    {
        var result=new List<Sensor>();int index=0;foreach(var sensor in sensors)
        {
            if((index++&255)==0)request.Cancellation.ThrowIfCancellationRequested();if(sensor.Point.Mesh>=0&&!visible(sensor.Point.Mesh))continue;
            if(sensor.Area==0&&!keepZeroArea)continue; // Screening retains zero-area material geometry, never fake quadrature mass.
            var key=(sensor.Point.Mesh,sensor.Point.Vertex);Vector3 point;
            if(worldCache is not null&&worldCache.TryGetValue(key,out var cached))point=cached;
            else{point=ContactGeometry.Point(sensor.Point,pose);worldCache?.Add(key,point);}
            if(!PoseGeometry.Finite(point)||!double.IsFinite(sensor.Area)||sensor.Area<0||!float.IsFinite(sensor.BodyInfluence))throw new NotSupportedException("Scene skin samples must have finite geometry, nonnegative quadrature area and a finite body influence.");
            result.Add(new(point,sensor.Area,sensor.BodyInfluence,sensor.Point));
        }
        return result.ToArray();
    }
    readonly record struct FieldValue(double Value,double DX,double DY,double DZ,float Bias);
    readonly record struct FieldSample(double Value,double DX,double DY,double DZ);
    readonly record struct EdgeTerm(double Y,double Z,double Distance,double Weight);
    static EdgeTerm[] EdgeNeighborhood(Sensor[] sensors,Vector3 delta,Vector3 up,Vector3 inward,double floor,double offset,double temperature)
    {
        var terms=sensors.Select(s=>{var p=s.Position+s.Alpha*delta;double y=Vector3.Dot(p,up)-floor,z=Vector3.Dot(p,inward)-offset;return new EdgeTerm(y,z,Math.Sqrt(y*y+z*z),s.Area);}).ToArray();
        double minimum=terms.Min(t=>t.Distance),sum=terms.Sum(t=>t.Weight*Math.Exp(-(t.Distance-minimum)/temperature));
        return terms.Select(t=>t with{Weight=t.Weight*Math.Exp(-(t.Distance-minimum)/temperature)/sum}).ToArray();
    }
    static double SolidDistance(double y,double z)=>y>0&&z<0?Math.Sqrt(y*y+z*z):Math.Max(y,-z);
    static FieldSample FeatureValue(Sensor sensor,Vector3 delta,Feature feature,Vector3 up,Vector3 inward,double floor,double offset,bool edge)
    {
        var point=sensor.Position+sensor.Alpha*delta;
        double y=Vector3.Dot(point,up)-floor,z=edge?Vector3.Dot(point,inward)-offset:0;
        if(!edge)return new(y,sensor.Alpha*up.X,sensor.Alpha*up.Y,sensor.Alpha*up.Z);
        if(feature==Feature.Solid&&!(y>0&&z<0))return y>=-z?
            new(y,sensor.Alpha*up.X,sensor.Alpha*up.Y,sensor.Alpha*up.Z):
            new(-z,-sensor.Alpha*inward.X,-sensor.Alpha*inward.Y,-sensor.Alpha*inward.Z);
        // Nonnegative distances to the actual finite half-plane features are
        // continuous at the edge. Signing each clipped feature independently
        // would introduce a false jump when hanging crosses its footprint.
        double vy=feature==Feature.Wall?Math.Max(y,0):y,vz=feature==Feature.Top?Math.Min(z,0):z;
        double distance=Math.Sqrt(vy*vy+vz*vz);
        if(distance==0)return new(0,0,0,0);
        double a=sensor.Alpha*vy/distance,b=sensor.Alpha*vz/distance;
        return new(distance,a*up.X+b*inward.X,a*up.Y+b*inward.Y,a*up.Z+b*inward.Z);
    }
    FieldValue SoftFeature(Sensor[] sensors,Vector3 delta,Feature feature,Vector3 up,Vector3 inward,double floor,double offset,bool edge,double temperature)
    {
        var fields=new FieldSample[sensors.Length];for(int i=0;i<sensors.Length;i++)
        {
            if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();fields[i]=FeatureValue(sensors[i],delta,feature,up,inward,floor,offset,edge);
        }
        double minimum=fields.Min(f=>f.Value),sum=0,area=0,dx=0,dy=0,dz=0;
        for(int i=0;i<sensors.Length;i++)
        {
            if((i&255)==0)request.Cancellation.ThrowIfCancellationRequested();
            var sensor=sensors[i];var field=fields[i];
            double w=sensor.Area*Math.Exp(-(field.Value-minimum)/temperature);sum+=w;area+=sensor.Area;dx+=w*field.DX;dy+=w*field.DY;dz+=w*field.DZ;
        }
        double value=minimum-temperature*Math.Log(sum/area);return new(value,dx/sum,dy/sum,dz/sum,(float)(value-minimum));
    }
    Evaluation Evaluate(List<Observation> observations,Vector3 delta)
    {
        double inv=1d/(length*length),prior=PriorFraction*inv,cost=prior*delta.LengthSquared(),gx=delta.X*prior,gy=delta.Y*prior,gz=delta.Z*prior;
        double xx=prior,xy=0,xz=0,yy=prior,yz=0,zz=prior;float gap=0,bias=0;
        double robustScale=source.CharacteristicLength*RobustScaleFraction*fileRatio,robustScaleSquared=robustScale*robustScale;
        void Add(double dx,double dy,double dz,double residual,double weight,bool robust=true)
        {
            // A pseudo-Huber consensus bounds the influence of a minority
            // geometric mismatch. Its derivative is exact; the positive IRLS
            // matrix is a GN approximation, not a feasibility certificate.
            // The rationalized loss avoids subtracting nearly equal values.
            double w=weight*inv,square=residual*residual,root=robust?Math.Sqrt(1+square/robustScaleSquared):1;
            cost+=w*(robust?2*square/(root+1):square);w/=root;
            double g=w*residual;gx+=dx*g;gy+=dy*g;gz+=dz*g;
            xx+=w*dx*dx;xy+=w*dx*dy;xz+=w*dx*dz;yy+=w*dy*dy;yz+=w*dy*dz;zz+=w*dz*dz;gap=Math.Max(gap,(float)Math.Abs(residual));
        }
        foreach(var observation in observations)
        {
            request.Cancellation.ThrowIfCancellationRequested();
            if(observation.Feature!=Feature.Edge)
            {
                var value=SoftFeature(observation.Target,delta,observation.Feature,targetUp,targetInward,targetFloor,targetEdgeOffset,edge,observation.Temperature);
                bias=Math.Max(bias,value.Bias);Add(value.DX,value.DY,value.DZ,value.Value-observation.Desired,observation.Weight);
            }
            else
            {
                // Source and target use the SAME continuous edge statistic.
                // Matching a source centroid to one target vertex cannot even
                // reproduce an exactly translated triangle; it is not used.
                double temperature=source.CharacteristicLength*SourceNearnessScaleFraction*fileRatio;
                var data=EdgeNeighborhood(observation.Target,delta,targetUp,targetInward,targetFloor,targetEdgeOffset,temperature);
                double cy=data.Sum(t=>t.Weight*t.Y),cz=data.Sum(t=>t.Weight*t.Z),a=cy-observation.Desired,b=cz-observation.DesiredOther;
                var dy=Vector3.Zero;var dz=Vector3.Zero;
                for(int i=0;i<data.Length;i++)
                {
                    double alpha=observation.Target[i].Alpha;var t=data[i];
                    var dd=t.Distance>0?(targetUp*(float)(alpha*t.Y)+targetInward*(float)(alpha*t.Z))/(float)t.Distance:Vector3.Zero;
                    dy+=(targetUp*(float)alpha-dd*(float)((t.Y-cy)/temperature))*(float)t.Weight;
                    dz+=(targetInward*(float)alpha-dd*(float)((t.Z-cz)/temperature))*(float)t.Weight;
                }
                double square=a*a+b*b,root=Math.Sqrt(1+square/robustScaleSquared),previous=cost;float previousGap=gap;
                double robustWeight=observation.Weight/root;
                Add(dy.X,dy.Y,dy.Z,a,robustWeight,false);Add(dz.X,dz.Y,dz.Z,b,robustWeight,false);
                cost=previous+observation.Weight*inv*2*square/(root+1);gap=Math.Max(previousGap,(float)Math.Sqrt(square));
            }
        }
        return new(cost,gx,gy,gz,xx,xy,xz,yy,yz,zz,gap,bias);
    }
    static Vector3 Solve(Evaluation v)
    {
        // A plane leaves two prior-only directions. Cholesky avoids taking a
        // determinant of this nearly rank-one matrix under rotated coordinates.
        double a=Math.Sqrt(v.XX),b=v.XY/a,c=v.XZ/a,d=Math.Sqrt(v.YY-b*b),e=(v.YZ-b*c)/d,f=Math.Sqrt(v.ZZ-c*c-e*e);
        if(!double.IsFinite(a)||!double.IsFinite(d)||!double.IsFinite(f)||a<=0||d<=0||f<=0)return new(float.NaN);
        double u=-v.GX/a,w=(-v.GY-b*u)/d,t=(-v.GZ-c*u-e*w)/f;
        double z=t/f,y=(w-e*z)/d,x=(u-b*y-c*z)/a;return new((float)x,(float)y,(float)z);
    }
    EvaluatedPose Pose(Transform[] local)=>AnimationService.Evaluate(target.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t));
    public SceneBodyFitReceipt Receipt()=>new(Version,Enabled,evaluations,improved,unchanged,unsupported,nonImproving,maximumCorrection,maximumInitialGap,maximumFinalGap,worstSourceFrame,sourcePenetrations)
    {
        MaximumSourcePenetration=maximumSourcePenetration,SourceRegions=sourceRegionCount,TargetRegions=pairs.Length,SourceSensors=sourceSensors,TargetSensors=targetSensors,
        SourceBody=sourceBody,TargetBody=targetBody,
        MaximumInitialPenetration=maximumInitialPenetration,MaximumFinalPenetration=maximumFinalPenetration,ClearanceClippedEvaluations=clearanceClipped,
        ClearanceInfeasibleEvaluations=clearanceInfeasible,UnmovableClearanceVertices=unmovableVertices,ClearanceVertices=clearance.Length,MaximumClearanceAdjustment=maximumClearanceAdjustment,
        FullSkinClearanceEvaluations=clearanceEvaluations,VolumePolicy=volumePolicy,RobustScale=source.CharacteristicLength*RobustScaleFraction*fileRatio,
        SensorWorkBudget=MaximumSensorWork,EstimatedSensorWork=estimatedSensorWork,
        Articulated=articulated?.Receipt(),
        TranslationProposals=translationProposals,
        EndpointGeometryVersion=EndpointGeometryVersion,PreparedObjectives=preparedObjectives,
        EndpointResidualPolicy="L4 norm of C2 positive original-material clearance hinges, exact beyond .001 target characteristic length; soft prior only, no point-count dilution, posed-area, tessellation-invariance or contact-bound claim",
        EndpointHingeTransitionWidth=Enabled?length*EndpointHingeTransitionFraction:null,
        EndpointDistanceVersion=EndpointDistanceVersion,
        EndpointSourceAffinityVersion=EndpointSourceAffinityVersion,
        EndpointFootprintVersion=edge?EndpointFootprintVersion:null,
        EndpointDistancePolicy="Visible source and default-visible target use matching full-material Boltzmann-weighted distances at existing .005 rest-radius temperature, removing the log-count offset without sharpening the field. Source defects projected to zero; exact target derivatives include probability changes. Either estimate can differ from actual vertex minima; independent raw-minimum/full-skin acceptance is still required. No plant/lock, continuous collision or contact acceptance claim",
        EndpointGeometry=endpoints.Select(p=>new SceneEndpointGeometryReceipt(p.SourceEnd,p.TargetEnd,p.SourceRoles.ToArray(),
            p.Source.Length,p.Target.Length,p.SourceWeightedBones,p.TargetWeightedBones,p.Source.Sum(s=>s.Area),p.Target.Sum(s=>s.Area),
            p.Source.Count(s=>s.Area==0),p.Target.Count(s=>s.Area==0))
            {SourceRestRadius=p.SourceRestRadius,TargetRestRadius=p.TargetRestRadius,SourceEffectiveRadius=p.SourceRadius,TargetEffectiveRadius=p.TargetRadius,
             SourceAffinityRadius=Math.Min(p.SourceRadius,source.CharacteristicLength*SourceNearnessScaleFraction)}).ToArray(),
        SourceEdgeAnchorPolicy="Matched area-weighted soft-edge neighborhood centroids; source defects projected per material sample",FeatureGapMeasured=gapEvaluations>0,
        ClearancePolicy=volumePolicy==BodyVolumePolicy.RequireVertexClearance?
            "Full default-visible skin; no new or worsened supplied-solid penetration; no contact claim":
            "Full default-visible overlap is measured, not constrained; no contact claim",
        MaximumOmittedRestDistance=pairs.SelectMany(p=>new[]{p.Source.MaximumOmittedRestDistance*fileRatio,p.Target.MaximumOmittedRestDistance}).DefaultIfEmpty(0).Max(),MaximumSoftMinimumBias=maximumBias,
        OpenGates=["soft scene proximity only; no contact modes or locks","source penetration is not copied as support","global source nearness and robust consensus are uncalibrated design settings","quadrature and soft-minimum bias","local-rest-radius standoff confidence/temperature and discrete material density bias are uncalibrated","full descendant material coverage is not a certified clearance or action solve","matched soft-edge neighborhoods are pose-local observations; temporal continuity is not verified","reported volume overlap is not clearance or contact validation","full active target geometry and native validation","coordinated articulated poses do not establish temporal or action acceptance","finite ledge and contact acceptance"]
    };
}
