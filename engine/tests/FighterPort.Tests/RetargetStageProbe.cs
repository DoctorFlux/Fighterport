using System.Numerics;
using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class RetargetStageProbe
{
    public static async Task SourceWitnesses(string sourcePath,string output,Action<bool,string> check)
    {
        // Independent annotations from the user's displayed authentic source,
        // not from the detector or target solver. Names select fixture data only.
        var source=await ProjectStore.LoadAsync(sourcePath);var rig=BrawlControlProfiles.SetUp(source);
        var clip=source.Clips.Single(c=>c.Source?.NativeName=="CliffClimbSlow");var before=AnimationControls.DeformChecksum(source.Scene);
        var up=rig.Axes!.Up;var surface=new SupportSurface(up,0,new(rig.Axes.Forward,0));
        var regions=SurfaceContacts.Regions(source.Scene,rig);var analysis=SurfaceContacts.Detect(source,clip,rig,new(){ExplicitSurface=true,SourceFloor=0,SourceEdge=surface.Edge});
        (double Frame,string Role,bool Contact,bool Top)[] labels=[(0,"Hand.L",true,false),(0,"Hand.R",true,false),(13.8107,"Hand.L",true,true),(13.8107,"Hand.R",true,false),(38.9002,"Hand.R",true,true),(59.9999,"Hand.L",false,false),(59.9999,"Hand.R",false,false)];
        var measurements=labels.Select(label=>
        {
            var pose=AnimationService.Evaluate(source.Scene,clip,label.Frame);var region=regions.Single(r=>r.Role==label.Role);
            var pins=analysis.Contacts.Where(c=>c.Contains(label.Frame)).SelectMany(c=>c.Pins).Where(p=>p.Role==label.Role).ToArray();
            return new{label.Frame,label.Role,ExpectedContact=label.Contact,ExpectedTop=label.Top,TopSeparation=region.Points.Min(p=>surface.TopOffset(ContactGeometry.Point(p,pose)).Length()),Detected=pins.Select(p=>new{p.EdgeSupport,p.Mode,p.Surface}).ToArray(),Matches=label.Contact?pins.Any(p=>!label.Top||!p.EdgeSupport):pins.Length==0};
        }).ToArray();
        await File.WriteAllTextAsync(Path.Combine(output,"source-witnesses.json"),JsonSerializer.Serialize(new{Detector=SurfaceContacts.Version,Evidence="User-provided authentic-source screenshots at 13.8107, 38.9002 and 59.9999; focused positive and released-hand labels only, not full mode/interval coverage.",Measurements=measurements,Intervals=analysis.Contacts},new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false}));
        foreach(var row in measurements)check(row.Matches,$"Authentic source at {row.Frame}: {row.Role} {(row.ExpectedContact?row.ExpectedTop?"remains supported on the top":"retains observed support":"is released")}");
        check(before==AnimationControls.DeformChecksum(source.Scene),"Source witness analysis preserves the authentic native deform rig");
    }
    public static async Task Run(string sourcePath,string targetPath,string name,string output,bool ledge,Action<bool,string> check,bool planOnly=false,bool temporalOnly=false)
    {
        var json=new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false,WriteIndented=true};
        var source=await ProjectStore.LoadAsync(sourcePath);var target=await ProjectStore.LoadAsync(targetPath);
        var sr=BrawlControlProfiles.SetUp(source);var tr=BrawlControlProfiles.SetUp(target);var clip=source.Clips.Single(c=>c.Source?.NativeName==name);
        var receiver=target with{Clips=[target.Clips.Single(c=>c.Source?.NativeName=="Wait1")],ControlRigs=[tr]};
        var ground=new GroundContactSettings{ExplicitSurface=true,SourceFloor=0,TargetFloor=0,SourceEdge=ledge?new(sr.Axes!.Forward,0):null,TargetEdge=ledge?new(tr.Axes!.Forward,0):null};
        var request=new RetargetRequest(source,clip,sr,receiver,tr,RootMotionPolicy.Preserve,true,default,null,ground,null,null,null,null,null);
        string sourceRig=AnimationControls.DeformChecksum(source.Scene),targetRig=AnimationControls.DeformChecksum(target.Scene);
        var a=RigDescriptor.Prepare(source.Scene,sr);var b=RigDescriptor.Prepare(target.Scene,tr);var baseline=new BaselineMotionTransfer(request,a,b);var analysis=SourceMotionAnalysis.Analyze(request);var plan=TargetMotionPlan.Build(request,a,b,baseline,analysis);
        var sourceRegions=SurfaceContacts.Regions(source.Scene,sr);var targetRegions=SurfaceContacts.Regions(target.Scene,tr);
        object Regions(SupportRegion[] regions,EvaluatedPose pose,Vector3 up)=>regions.Where(r=>r.Role.StartsWith("Hand.")||r.Role is "Hips" or "Chest").Select(r=>new{r.Role,Low=SurfaceContacts.Patch(r,pose,up,.01f).Select(p=>ContactGeometry.Point(p,pose)).Aggregate(Vector3.Zero,(x,y)=>x+y)/SurfaceContacts.Patch(r,pose,up,.01f).Length,TopSeparation=ledge?r.Points.Min(p=>new SupportSurface(up,0,new(Vector3.UnitZ,0)).TopOffset(ContactGeometry.Point(p,pose)).Length()):r.Points.Min(p=>Math.Abs(Vector3.Dot(ContactGeometry.Point(p,pose),up))),Corner=ledge?ContactGeometry.Point(SurfaceContacts.EdgePatch(r,pose,new(up,0,new(Vector3.UnitZ,0)))[0],pose):Vector3.Zero,Centre=r.Points.Select(p=>ContactGeometry.Point(p,pose)).Aggregate(Vector3.Zero,(x,y)=>x+y)/r.Points.Length}).ToArray();
        await File.WriteAllTextAsync(Path.Combine(output,"geometry.json"),JsonSerializer.Serialize(new{SourceAxes=sr.Axes,TargetAxes=tr.Axes,SourceLeg=RigCharacterization.Describe(source.Scene,sr).LegLength,Frames=Enumerable.Range(0,9).Select(i=>(double)i).Concat(new[]{13.8107,38.9002,clip.PreviewEndFrame}).Where(f=>f<=clip.PreviewEndFrame).Append(clip.LastSampleFrame).Distinct().Select(f=>new{Frame=f,Source=Regions(sourceRegions,AnimationService.Evaluate(source.Scene,clip,f),sr.Axes!.Up),TargetBaseline=Regions(targetRegions,AnimationService.Evaluate(target.Scene,null,0,baseline.Evaluate(f).Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t)),tr.Axes!.Up)})},json));
        await File.WriteAllTextAsync(Path.Combine(output,"anchors.json"),JsonSerializer.Serialize(new{Clip=name,baseline.FileUnitRatio,baseline.RootAnatomicalRatio,baseline.PelvisHeightAnatomicalRatio,baseline.LateralAnatomicalRatio,Source=analysis.Interactions.Relations.Select(c=>new{c.Frame,c.EndFrame,Pins=c.Pins.Select(p=>new{p.Role,p.Position,p.EdgeSupport,p.HoldRotation,Patch=p.SurfacePoints.Length})}),Target=plan.Graph.Relations.Select(c=>new{c.Frame,c.EndFrame,Pins=c.Pins.Select(p=>new{p.Role,p.Position,p.EdgeSupport,p.HoldRotation,Patch=p.SurfacePoints.Length})}),SourcePhases=analysis.SurfaceAnalysis?.Ground.Phases,TargetPhases=plan.Grounding?.Phases},json));
        var authenticGeometry=new PreparedGeometry(source,clip);
        EvaluatedPose LocalPose(Transform[] local,double frame)=>AnimationService.Evaluate(target.Scene,null,0,local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t)) with{Seconds=frame/plan.Rate.FramesPerSecond};
        object PoseWitness(RigDescriptor descriptor,SupportRegion[] regions,EvaluatedPose pose,int[] bones,SupportSurface surface)
        {
            var points=descriptor.SkinPoints.Select(p=>(Sample:p,Position:ContactGeometry.Point(p,pose))).ToArray();
            var deepest=points.Length==0?default:points.MaxBy(p=>surface.Penetration(p.Position));
            return new
            {
                Bones=bones.Select(i=>new{Bone=i,Local=pose.Local[i],WorldPosition=pose.World[i].Translation,WorldOrientation=PoseGeometry.Rotation(pose.World[i])}).ToArray(),
                Skin=new{MaximumPenetration=points.Length>0?surface.Penetration(deepest.Position):0,Mesh=points.Length>0?deepest.Sample.Mesh:-1,Vertex=points.Length>0?deepest.Sample.Vertex:-1,Position=deepest.Position},
                Regions=regions.Select(region=>
                {
                    var actual=region.Points.Select(p=>(Sample:p,Position:ContactGeometry.Point(p,pose))).ToArray();
                    var low=actual.MinBy(p=>Vector3.Dot(p.Position,surface.Normal));var top=actual.MinBy(p=>surface.TopOffset(p.Position).LengthSquared());
                    return new{region.Role,region.Bone,Vertices=actual.Length,Centre=actual.Aggregate(Vector3.Zero,(sum,p)=>sum+p.Position)/actual.Length,
                        Underside=new{low.Sample.Mesh,low.Sample.Vertex,low.Position,Height=Vector3.Dot(low.Position,surface.Normal)-surface.Height},
                        NearestTop=new{top.Sample.Mesh,top.Sample.Vertex,top.Position,Separation=surface.TopOffset(top.Position).Length()}};
                }).ToArray()
            };
        }
        var sourceSurface=new SupportSurface(sr.Axes!.Up,ground.SourceFloor!.Value,ground.SourceEdge);
        var targetSurface=new SupportSurface(tr.Axes!.Up,ground.TargetFloor!.Value,ground.TargetEdge);
        var sourceBones=baseline.Mapping.Bones.Select(p=>p.Source).Distinct().Order().ToArray();
        var targetBones=baseline.Mapping.Bones.Select(p=>p.Target).Distinct().Order().ToArray();
        var witnessFrames=Enumerable.Range(0,plan.Samples).Select(f=>(double)f)
            .Concat(plan.Closing?new[]{(double)plan.Samples}:[])
            .Concat(new[]{13.8107,38.9002,59.9999})
            .Where(f=>(f>=0&&f<plan.Samples)||(plan.Closing&&f==plan.Samples)).Distinct().Order().ToArray();
        var coefficientStages=new Dictionary<string,BoneTrack[]>(StringComparer.Ordinal);
        async Task WriteWitnesses(PreparedTargetTrajectory? motion,AnimationClip? initialClip=null,AnimationClip? finalClip=null)
        {
            object Snapshot(EvaluatedPose pose)=>PoseWitness(b,targetRegions,pose,targetBones,targetSurface);
            var rows=new List<object>();
            foreach(double frame in witnessFrames)
            {
                double sourceFrame=plan.SourceFrame(frame);var raw=LocalPose(baseline.Evaluate(sourceFrame),frame);var prepared=LocalPose(plan.Reference(frame),frame);
                object? solvedWitness=null;
                if(motion is not null)
                {
                    var curve=new AnimationClip(Guid.NewGuid(),name,plan.Samples,plan.Rate,plan.Loop,motion.Tracks);
                    solvedWitness=new
                    {
                        Evaluation="Direct evaluation of authoritative destination trajectory coefficients; no diagnostic pose interpolation or second bake",
                        Pose=Snapshot(AnimationService.Evaluate(target.Scene,curve,frame))
                    };
                }
                int body=b.Binding.Roles.GetValueOrDefault("Hips",b.Binding.Roles["RootMotion"]);
                rows.Add(new
                {
                    TargetFrame=frame,TargetSeconds=frame/plan.Rate.FramesPerSecond,SourceFrame=sourceFrame,SourceSeconds=sourceFrame/clip.Rate.FramesPerSecond,
                    Source=PoseWitness(a,sourceRegions,authenticGeometry.Evaluate(sourceFrame),sourceBones,sourceSurface),
                    Baseline=Snapshot(raw),PlanReference=Snapshot(prepared),ReferenceBodyDisplacement=prepared.World[body].Translation-raw.World[body].Translation,
                    Contacts=plan.Pins(frame).Select(pin=>new
                    {
                        pin.Role,pin.Bone,pin.Mode,pin.EdgeSupport,pin.SoftSupport,pin.RequiredSupport,pin.HoldRotation,pin.Position,pin.Provenance,
                        MaterialPatch=pin.SurfacePoints.Select(p=>new{p.Mesh,p.Vertex}).ToArray(),RollingRegionVertices=pin.SurfaceRegion.Length,
                        BaselinePoint=ContactGeometry.Point(pin,raw),ReferencePoint=ContactGeometry.Point(pin,prepared),
                        BaselineResidual=ContactGeometry.Residual(pin,raw),ReferenceResidual=ContactGeometry.Residual(pin,prepared)
                    }).ToArray(),
                    CoefficientStages=coefficientStages.Select(s=>new{Stage=s.Key,Pose=Snapshot(AnimationService.Evaluate(target.Scene,new AnimationClip(Guid.NewGuid(),name,plan.Samples,plan.Rate,plan.Loop,s.Value),frame))}).ToArray(),
                    Solved=solvedWitness,InitialCurve=initialClip is null?null:Snapshot(AnimationService.Evaluate(target.Scene,initialClip,frame)),
                    FinalCurve=finalClip is null?null:Snapshot(AnimationService.Evaluate(target.Scene,finalClip,frame))
                });
            }
            await File.WriteAllTextAsync(Path.Combine(output,"pose-witnesses.json"),JsonSerializer.Serialize(new
            {
                Clip=name,Pipeline=RetargetPipeline.Version,SourceRate=clip.Rate,TargetRate=plan.Rate,plan.Samples,plan.Closing,
                Mapping=baseline.Mapping.Bones.Select(p=>new{p.Role,p.Source,SourceName=source.Scene.Bones[p.Source].Name,SourceParent=source.Scene.Bones[p.Source].Parent,p.Target,TargetName=target.Scene.Bones[p.Target].Name,TargetParent=target.Scene.Bones[p.Target].Parent,p.DetailChannels,p.Evidence}),
                SourceEvaluation="Authentic prepared source skeleton plus preview-selected geometry-affecting layers",
                TargetEvaluation="Target skeletal poses against preserved target skin geometry; destination companion animation not evaluated by this diagnostic",
                NativeDecode="Not performed by this stage probe; Core destination-curve evaluation is separate from fresh worker encoding/reopen and displayed GPU geometry",
                Claim="Stage witnesses only. Solver/output share authoritative destination coefficients. No native encoding/display or complete motion acceptance claim.",Frames=rows
            },json));
        }
        if(planOnly)
        {
            await WriteWitnesses(null);
            await File.WriteAllTextAsync(Path.Combine(output,"stages.json"),JsonSerializer.Serialize(new{Clip=name,Stage="Plan only",Pipeline=RetargetPipeline.Version,plan.Diagnostics,Claim="Temporal solve and native output were not run; authentic source, baseline and plan witnesses only."},json));return;
        }
        var solution=RetargetTrajectorySolver.Solve(plan,false,trace:(stage,tracks)=>coefficientStages.Add(stage,tracks));
        if(solution.Value is not {} solved)
        {
            await WriteWitnesses(null);
            await File.WriteAllTextAsync(Path.Combine(output,"stages.json"),JsonSerializer.Serialize(new{Clip=name,Stage="Trajectory unresolved",Pipeline=RetargetPipeline.Version,solution.Diagnostics,Claim="Source/baseline/plan witnesses retained; no destination output was prepared."},json));
            check(sourceRig==AnimationControls.DeformChecksum(source.Scene)&&targetRig==AnimationControls.DeformChecksum(target.Scene),"Unresolved trajectory stage preserves both native rigs");return;
        }
        plan=plan with{Clearance=solved.EffectiveClearance};
        AnimationClip Motion(BoneTrack[] tracks)=>new(Guid.NewGuid(),name,plan.Samples,plan.Rate,plan.Loop,tracks);
        var initial=Motion(solved.InitialTracks);var final=Motion(solved.Tracks);
        await WriteWitnesses(solved,initial,final);
        var samples=new List<object>();double worstInitial=0,worstFinal=0;float initialDepth=0,finalDepth=0;
        foreach(double frame in solved.Frames)
        {
            float Depth(EvaluatedPose pose)=>plan.HasGround(frame)?b.SkinPoints.Select(p=>targetSurface.Penetration(ContactGeometry.Point(p,pose))).DefaultIfEmpty(0).Max():0;
            var first=AnimationService.Evaluate(target.Scene,initial,frame);var last=AnimationService.Evaluate(target.Scene,final,frame);
            float aDepth=Depth(first),bDepth=Depth(last);if(aDepth>initialDepth){initialDepth=aDepth;worstInitial=frame;}if(bDepth>finalDepth){finalDepth=bDepth;worstFinal=frame;}
            if(frame==Math.Truncate(frame))samples.Add(new{Frame=frame,InitialCurveDepth=aDepth,FinalCurveDepth=bDepth});
        }
        foreach(int bone in Enumerable.Range(0,target.Scene.Bones.Length).Where(b.IsActorAncestor))
        {
            var before=initial.Tracks.Single(t=>t.Bone==target.Scene.Bones[bone].Name);var after=final.Tracks.Single(t=>t.Bone==before.Bone);
            check(before.Channels.Zip(after.Channels).All(p=>p.First!.Keys.SequenceEqual(p.Second!.Keys)),"Trajectory fitting preserves actor-ancestor keys and tangents: "+before.Bone);
        }
        EvaluatedPose Reference(double frame)=>LocalPose(plan.Reference(frame),frame);
        bool BodyFitting(double f)=>plan.Grounding is {} g&&SurfaceContacts.BodySupportedAt(g.BodySupportPhases,g.BodySupportedThroughout,g.BodySupportedAtClosing,plan.SourceFrame(f),plan.AuthenticSourceSamples);
        var measuredBones=baseline.Mapping.Bones.Where(m=>!m.DetailChannels).Select(m=>m.Target);
        var initialAction=RetargetAcceptance.MeasureAction(target.Scene,initial,Reference,measuredBones,rig:tr,bodyFittingAt:BodyFitting);
        var finalAction=RetargetAcceptance.MeasureAction(target.Scene,final,Reference,measuredBones,rig:tr,bodyFittingAt:BodyFitting);
        await File.WriteAllTextAsync(Path.Combine(output,"stages.json"),JsonSerializer.Serialize(new
        {
            Clip=name,Stage=temporalOnly?"Trajectory-only probe":"Destination trajectory",Pipeline=RetargetPipeline.Version,Trajectory=solved.Receipt,solution.Diagnostics,
            InitialCurve=new{Depth=initialDepth,Frame=worstInitial},FinalCurve=new{Depth=finalDepth,Frame=worstFinal},
            Action=new{Initial=initialAction,Final=finalAction},Samples=samples,
            Claim="Core trajectory curves and full-skin/action measurements. No fresh native encoding/reopen or displayed acceptance; old --temporal-only flag now selects this same normal trajectory."
        },json));
        check(solved.Frames.Select((frame,i)=>AnimationService.Evaluate(target.Scene,final,frame).Local.SequenceEqual(solved.Poses[i])).All(x=>x),"Prepared poses evaluate exactly from final coefficients without another bake");
        check(sourceRig==AnimationControls.DeformChecksum(source.Scene)&&targetRig==AnimationControls.DeformChecksum(target.Scene),"Trajectory stage preserves authentic source and target deform data");
        Console.WriteLine($"TRAJECTORY {name}: initial depth {initialDepth:G7} at {worstInitial}; final {finalDepth:G7} at {worstFinal}; action {finalAction.MaximumDegrees:G7} degrees; contacts remain separately rated");
    }
}
