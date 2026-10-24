using System.Numerics;
using System.Text.Json;
using FighterPort.Core;

// A diagnostic for the existing independently authored RetargetChecks fixture.
// Fixture indices define the original test anatomy; they never enter production
// retarget decisions. No measured contact failure is converted into a test pass.
internal static class NativeReleaseProbe
{
    public static async Task RunAsync(string directory,Action<bool,string> check)
    {
        Directory.CreateDirectory(directory);
        await CheckVelocityInitialization(directory,check);
        var original=ProceduralFixture.Create();
        var alternate=ProceduralFixture.Create(1.4f);
        var bones=alternate.Scene.Bones.Select((bone,i)=>bone with{Name=$"Joint_{i:00}"}).ToArray();
        bones[4]=bones[4] with{Rest=bones[4].Rest with{Rotation=new(0,0,25)}};
        bones[5]=bones[5] with{Rest=bones[5].Rest with{Rotation=new(0,0,-25),Translation=Vector3.Transform(bones[5].Rest.Translation,Matrix4x4.CreateRotationZ(-25*MathF.PI/180))}};
        bones[7]=bones[7] with{Rest=bones[7].Rest with{Rotation=new(0,0,90)}};
        bones[8]=bones[8] with{Rest=bones[8].Rest with{Rotation=new(0,0,-90),Translation=Vector3.Transform(bones[8].Rest.Translation,Matrix4x4.CreateRotationZ(-MathF.PI/2))}};
        bones=bones.Select((bone,i)=>i is 11 or 14?bone with{Rest=bone.Rest with{Translation=bone.Rest.Translation*.6f}}:i is 12 or 15?bone with{Rest=bone.Rest with{Translation=bone.Rest.Translation*1.1f}}:bone).ToArray();
        var targetScene=alternate.Scene with{Bones=bones,SkeletonRevision=Guid.NewGuid()};
        var target=original with{Id=Guid.NewGuid(),Scene=targetScene,Clips=[]};
        var sourceRig=SurfaceContactChecks.Profile(original.Scene);
        var targetRig=SurfaceContactChecks.Profile(targetScene);
        var sourceLocal=original.Scene.Bones.Select(bone=>bone.Rest).ToArray();
        foreach(var chain in sourceRig.Chains)
            sourceLocal[chain.Middle]=sourceLocal[chain.Middle] with{Rotation=new(chain.Role.StartsWith("Foot",StringComparison.Ordinal)?20:0,0,chain.Role.StartsWith("Hand",StringComparison.Ordinal)?35:0)};
        var tracks=sourceLocal.Select((transform,i)=>new BoneTrack(original.Scene.Bones[i].Name,transform.Channels().Select(value=>(Curve?)new Curve([new(0,value),new(23,value)])).ToArray())).ToArray();
        var constant=new AnimationClip(Guid.NewGuid(),"Original support / ledge",24,new(60),false,tracks);
        var sourcePose=AnimationService.Evaluate(original.Scene,constant,0);
        var pins=sourceRig.Chains.Select(chain=>ContactSolver.Capture(original.Scene,sourcePose,sourceRig,chain.Role,chain.Role.StartsWith("Hand",StringComparison.Ordinal)?"Ledge":"Floor")).ToArray();
        var donor=original with{Clips=[constant],Contacts=[new ContactFrame(Guid.NewGuid(),constant.Id,0,sourceRig.SkeletonFingerprint,pins){EndFrame=7}]};
        var moving=constant with{Id=Guid.NewGuid(),Tracks=tracks.Select((track,i)=>i==0?track with{Channels=track.Channels.Select((curve,ch)=>ch==7?new Curve([new(0,0),new(7,0),new(8,.1f),new(23,.1f)]):curve).ToArray()}:track).ToArray()};
        var json=new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false,WriteIndented=true};
        string sourceChecksum=AnimationControls.DeformChecksum(original.Scene),targetChecksum=AnimationControls.DeformChecksum(targetScene);
        foreach(var clip in new[]{constant,moving})
        {
            string label=clip.Id==constant.Id?"constant":"moving-release";
            var source=donor with{Clips=[clip],Contacts=donor.Contacts.Select(contact=>contact with{Clip=clip.Id}).ToArray()};
            string before=JsonSerializer.Serialize(source,ProjectStore.Json);
            var request=new RetargetRequest(source,clip,sourceRig,target,targetRig,RootMotionPolicy.Preserve,true,default,null,null,null,null,null,null,null);
            var sourceDescriptor=RigDescriptor.Prepare(source.Scene,sourceRig);
            var targetDescriptor=RigDescriptor.Prepare(targetScene,targetRig);
            var baseline=new BaselineMotionTransfer(request,sourceDescriptor,targetDescriptor);
            var analysis=SourceMotionAnalysis.Analyze(request);
            var plan=TargetMotionPlan.Build(request,sourceDescriptor,targetDescriptor,baseline,analysis);
            var solution=TemporalRetargetSolver.Solve(plan);
            string path=Path.Combine(directory,label+"-release-stages.json");
            if(solution.Value is not {} solved)
            {
                await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new{Fixture=label,Pipeline=RetargetPipeline.Version,Stage="Temporal unresolved",solution.Diagnostics,Claim="No native curve fit or output was produced."},json));
                check(before==JsonSerializer.Serialize(source,ProjectStore.Json),"Release diagnostic preserves unresolved source "+label);
                continue;
            }
            plan=plan with{Clearance=solved.EffectiveClearance};
            if(plan.Clearance is {} clearance)
                plan=plan with{Graph=plan.Graph with{Relations=plan.Graph.Relations.Select(contact=>contact with{Pins=contact.Pins.Select(pin=>pin.Clearance is null?pin:pin with{Clearance=clearance}).ToArray()}).ToArray()}};
            var initial=NativeCurveBaker.Fit(plan,solved,false,evaluationBudget:1);
            var final=NativeCurveBaker.Fit(plan,solved,false);
            AnimationClip Clip(BoneTrack[] values)=>new(Guid.NewGuid(),label,plan.Samples,plan.Rate,plan.Loop,values);
            var initialClip=Clip(initial.Tracks);
            var finalClip=Clip(final.Tracks);
            // This global zero-slope candidate is a representability diagnostic,
            // not a proposed production policy or an accepted output. It keeps
            // every integer value/time while isolating shared-tangent spill.
            var zeroClip=Clip(initial.Tracks.Select(track=>track with{Channels=track.Channels.Select(curve=>curve is null?null:curve with{Keys=curve.Keys.Select(key=>key with{InTangent=0,OutTangent=0}).ToArray()}).ToArray()}).ToArray());
            EvaluatedPose Pose(Transform[] local,double frame)=>AnimationService.Evaluate(targetScene,null,0,local.Select((value,index)=>(value,index)).ToDictionary(pair=>pair.index,pair=>pair.value)) with{Seconds=frame/plan.Rate.FramesPerSecond};
            static double Angle(Quaternion a,Quaternion b)
            {
                var delta=Quaternion.Normalize(a*Quaternion.Inverse(b));
                return 2*Math.Atan2(new Vector3(delta.X,delta.Y,delta.Z).Length(),Math.Abs(delta.W));
            }
            object Contacts(IEnumerable<ContactPin> selected,EvaluatedPose pose)=>selected.Select(pin=>new
            {
                pin.Role,pin.Bone,pin.HoldRotation,
                Point=Vector3.Transform(pin.LocalPoint,pose.World[pin.Bone]),
                Goal=ContactGeometry.Target(pin,pose),
                // Exact fixture uses authored point anchors with no skin region.
                // This direct distance does not use the production residual.
                PositionError=Vector3.Distance(Vector3.Transform(pin.LocalPoint,pose.World[pin.Bone]),ContactGeometry.Target(pin,pose)),
                RotationError=pin.HoldRotation?Angle(PoseGeometry.Rotation(pose.World[pin.Bone]),ContactGeometry.TargetRotation(pin,pose)):0
            }).ToArray();
            object Measure(double frame,EvaluatedPose pose,EvaluatedPose reference)=>new
            {
                Constraints=TargetMotionConstraints.MaximumNormalizedViolation(plan,frame,pose),
                Contacts=Contacts(plan.Pins(frame),pose),
                Action=PreparedMotionAction.Create(targetDescriptor,baseline.Mapping,reference).Measure(pose),
                Bones=pose.Local.Select((local,bone)=>new{Bone=bone,Local=local,World=pose.World[bone].Translation,Orientation=PoseGeometry.Rotation(pose.World[bone])}).ToArray()
            };
            var rows=new List<object>();
            // All integer/quarter samples, including the release endpoint; extra
            // eighth probes and immediate one-sided boundary witnesses near6..8.
            var frames=solved.Frames.Concat(Enumerable.Range(40,33).Select(i=>i/8d)).Concat(new[]{6.9999,7.0001}).Distinct().Order().ToArray();
            foreach(double frame in frames)
            {
                var raw=Pose(baseline.Evaluate(plan.SourceFrame(frame)),frame);
                var reference=Pose(plan.Reference(frame),frame);
                int sample=Array.BinarySearch(solved.Frames,frame);
                rows.Add(new
                {
                    Frame=frame,SourceFrame=plan.SourceFrame(frame),ActiveContacts=plan.Pins(frame).Length,
                    Source=Contacts(source.Contacts.Where(contact=>contact.Contains(plan.SourceFrame(frame))).SelectMany(contact=>contact.Pins),AnimationService.Evaluate(source.Scene,clip,plan.SourceFrame(frame))),
                    Baseline=Measure(frame,raw,reference),
                    PlanReference=Measure(frame,reference,reference),
                    Solved=sample>=0?Measure(frame,Pose(solved.Poses[sample],frame),reference):null,
                    SolvedMeaning=sample>=0?"Exact temporal collocation":"No solver sample at this time; no interpolation substituted",
                    InitialCurve=Measure(frame,AnimationService.Evaluate(targetScene,initialClip,frame),reference),
                    FinalCurve=Measure(frame,AnimationService.Evaluate(targetScene,finalClip,frame),reference),
                    ZeroSlopeDiagnostic=Measure(frame,AnimationService.Evaluate(targetScene,zeroClip,frame),reference)
                });
            }
            await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new
            {
                Fixture=label,Pipeline=RetargetPipeline.Version,Temporal=solved.Receipt,Initial=initial.Receipt,Final=final.Receipt,
                FixtureEvidence="Exact constant-source and moving-release constructions from RetargetChecks plus Program.cs alternate rig; authored four point/orientation contacts have inclusive interval0..7.",
                Claim="Stage diagnostics only. Core Hermite evaluation is not native encoding/reopen, displayed geometry, full-contact acceptance or continuous-time proof. Zero slopes are a diagnostic candidate only.",
                plan.PositionBound,RotationBound=RetargetAcceptance.RotationRadians,Intervals=plan.Graph.Relations,
                Keys=final.Tracks.Select((track,index)=>new{track.Bone,Channels=track.Channels.Select((curve,channel)=>new{Channel=channel,Initial=initial.Tracks[index].Channels[channel]!.Keys.Where(key=>key.Frame>=5&&key.Frame<=9),Final=curve!.Keys.Where(key=>key.Frame>=5&&key.Frame<=9)})}),
                Frames=rows
            },json));
            check(plan.Pins(7).Length==pins.Length&&plan.Pins(7.0001).Length==0,"Release diagnostic retains authored inclusive endpoint and immediate release: "+label);
            check(final.Tracks.Zip(initial.Tracks).All(pair=>pair.First.Channels.Zip(pair.Second.Channels).All(ch=>ch.First!.Keys.Select(key=>key.Frame).SequenceEqual(ch.Second!.Keys.Select(key=>key.Frame)))),
                "Native coefficient diagnostic preserves every integer key time: "+label);
            int root=targetDescriptor.Binding.Roles["RootMotion"],body=targetDescriptor.Binding.Roles.GetValueOrDefault("Hips",-1);
            check(final.Tracks[root].Channels.Zip(initial.Tracks[root].Channels).All(pair=>pair.First!.Keys.SequenceEqual(pair.Second!.Keys))
                &&final.Tracks.Zip(initial.Tracks).All(pair=>Enumerable.Range(0,3).All(channel=>pair.First.Channels[channel]!.Keys.SequenceEqual(pair.Second.Channels[channel]!.Keys)))
                &&Enumerable.Range(0,targetScene.Bones.Length).Where(bone=>bone!=body).All(bone=>Enumerable.Range(6,3).All(channel=>final.Tracks[bone].Channels[channel]!.Keys.SequenceEqual(initial.Tracks[bone].Channels[channel]!.Keys))),
                "Joint coefficient diagnostic preserves actor root, authored scale and non-body translation: "+label);
            check(before==JsonSerializer.Serialize(source,ProjectStore.Json),"Release diagnostic leaves its authentic source unchanged: "+label);
            Console.WriteLine("RELEASE STAGES "+label+": "+path);
        }
        check(sourceChecksum==AnimationControls.DeformChecksum(original.Scene)&&targetChecksum==AnimationControls.DeformChecksum(targetScene),"Release diagnostic preserves source/target rest anatomy and weights");
    }

    static async Task CheckVelocityInitialization(string directory,Action<bool,string> check)
    {
        var original=ProceduralFixture.Create();var rig=SurfaceContactChecks.Profile(original.Scene);
        int body=rig.BodyBone!.Value;
        var rest=AnimationService.Evaluate(original.Scene,null,0);
        var rows=new List<object>();
        foreach(int fps in new[]{30,60})
        foreach(bool moving in new[]{false,true})
        foreach(bool exclusive in new[]{false,true})
        {
            var sourceClip=new AnimationClip(Guid.NewGuid(),"Independent fixed/moving support derivative",4,new(fps),false,[]);
            var source=original with{Clips=[sourceClip],Contacts=[]};
            var request=new RetargetRequest(source,sourceClip,rig,source with{Clips=[]},rig,RootMotionPolicy.Preserve,false,default,null,null,null,null,null,null,null);
            var descriptor=RigDescriptor.Prepare(source.Scene,rig);
            var baseline=new BaselineMotionTransfer(request,descriptor,descriptor);
            var plan=TargetMotionPlan.Build(request,descriptor,descriptor,baseline,SourceMotionAnalysis.Analyze(request));
            // Independently authored object translates at exactly one native
            // distance unit per second. Both output rates share that motion.
            var reference=moving?new ContactReference(Guid.NewGuid(),"Measured translating support","Original constant-velocity fixture")
            {
                GeometryKnown=true,Motion=[new(0,Vector3.Zero,Quaternion.Identity),new(1,Vector3.UnitX,Quaternion.Identity)]
            }:null;
            var pin=new ContactPin(Guid.NewGuid(),"Existing body anchor",body,Vector3.Zero,rest.World[body].Translation,PoseGeometry.Rotation(rest.World[body]),true,"Authored point"){Reference=reference};
            plan=plan with{Graph=new([new ContactFrame(Guid.NewGuid(),sourceClip.Id,0,rig.SkeletonFingerprint,[pin]){EndFrame=1,EndExclusive=exclusive}],MotionContactCoverage.Required,[])};
            float expected=moving?1f/fps:0;
            var input=source.Scene.Bones.Select((bone,index)=>new BoneTrack(bone.Name,Enumerable.Range(0,9).Select(channel=>(Curve?)new Curve(Enumerable.Range(0,4).Select(frame=>
            {
                float value=bone.Rest.Channels()[channel]+(index==body&&channel==6?expected*frame:0);
                float slope=index==body&&channel==6?expected:0;
                // Only the interior derivative is perturbed; the other keys
                // already satisfy the independently specified support motion.
                if(index==body&&frame==1&&channel==6)slope=-.04f;
                if(index==body&&frame==1&&channel==7)slope=.05f;
                if(index==body&&frame==1&&channel==5)slope=.2f;
                return new Key(frame,value,slope,slope);
            }).ToArray())).ToArray())).ToArray();
            var result=NativeContactVelocity.Initialize(plan,input,evaluationBudget:10000);
            var channels=result.Tracks[body].Channels;
            check(result.Receipt.AcceptedKeys>0,$"Contact velocity initializer changes the deliberately invalid endpoint derivative: moving={moving}, rate={fps}, exclusive={exclusive}");
            check(Math.Abs(channels[6]!.Keys[1].OutTangent-expected)<.00002f&&Math.Abs(channels[7]!.Keys[1].OutTangent)<.00002f&&Math.Abs(channels[5]!.Keys[1].OutTangent)<.002f,
                $"Contact derivative follows independent fixed/one-unit-per-second support and held orientation: moving={moving}, rate={fps}, exclusive={exclusive}");
            check(input.Zip(result.Tracks).All(pair=>pair.First.Channels.Zip(pair.Second.Channels).All(channel=>channel.First!.Keys.Zip(channel.Second!.Keys).All(key=>key.First.Frame==key.Second.Frame&&key.First.Value==key.Second.Value))),
                $"Velocity initialization preserves all native integer values and times: moving={moving}, rate={fps}");
            check(input[0].Channels.Zip(result.Tracks[0].Channels).All(pair=>pair.First!.Keys.SequenceEqual(pair.Second!.Keys))&&input.Zip(result.Tracks).All(pair=>Enumerable.Range(0,3).All(channel=>pair.First.Channels[channel]!.Keys.SequenceEqual(pair.Second.Channels[channel]!.Keys))),
                $"Velocity initialization preserves actor-root and every authored scale channel: moving={moving}, rate={fps}");
            check(result.Receipt.PoseEvaluations<=result.Receipt.EvaluationBudget,"Contact velocity work obeys its declared budget");
            check(plan.Pins(1).Length==(exclusive?0:1)&&plan.Pins(1.0001).Length==0,"Incoming derivative preparation does not extend or alter the authored contact interval");
            var isolated=plan with{Graph=plan.Graph with{Relations=plan.Graph.Relations.Select(contact=>contact with{Frame=1,EndFrame=1,EndExclusive=false}).ToArray()}};
            var isolatedResult=NativeContactVelocity.Initialize(isolated,input,evaluationBudget:1000);
            check(isolatedResult.Receipt.AcceptedKeys==0&&isolatedResult.Tracks==input,"An isolated point constraint does not invent a persistent velocity interval");
            var onset=plan with{Graph=plan.Graph with{Relations=plan.Graph.Relations.Select(contact=>contact with{Frame=1,EndFrame=3,EndExclusive=false}).ToArray()}};
            var onsetResult=NativeContactVelocity.Initialize(onset,input,evaluationBudget:10000);
            check(onsetResult.Receipt.AcceptedKeys>0&&Math.Abs(onsetResult.Tracks[body].Channels[6]!.Keys[1].OutTangent-expected)<.00002f&&onset.Pins(.9999).Length==0,
                "A continuous contact onset uses its outgoing derivative without inventing an earlier contact interval");
            if(exclusive)
            {
                var incompatible=input.Select(track=>track with{Channels=track.Channels.Select(curve=>curve is null?null:curve with{Keys=curve.Keys.ToArray()}).ToArray()}).ToArray();
                var curve=incompatible[body].Channels[6]!;curve.Keys[1]=curve.Keys[1] with{Value=curve.Keys[1].Value+.2f};
                var incompatibleResult=NativeContactVelocity.Initialize(plan,incompatible,evaluationBudget:10000);
                check(incompatibleResult.Receipt.Reasons.Keys.Any(reason=>reason.StartsWith("Incoming end-exclusive key value",StringComparison.Ordinal))
                    &&incompatibleResult.Tracks[body].Channels[6]!.Keys[1]==incompatible[body].Channels[6]!.Keys[1],
                    "An incompatible incoming endpoint is reported and its integer value/derivative is not silently repaired");
            }
            rows.Add(new{Moving=moving,Rate=fps,EndExclusive=exclusive,ExpectedNativeXPerFrame=expected,ActualNativeXPerFrame=channels[6]!.Keys[1].OutTangent,ActualNativeYPerFrame=channels[7]!.Keys[1].OutTangent,ActualNativeZDegreesPerFrame=channels[5]!.Keys[1].OutTangent,result.Receipt});
        }
        var evidenceJson=new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false,WriteIndented=true};
        string evidence=JsonSerializer.Serialize(rows,evidenceJson);
        using(var parsed=JsonDocument.Parse(evidence))
            check(parsed.RootElement.GetArrayLength()==8&&parsed.RootElement.EnumerateArray().All(row=>row.TryGetProperty("ExpectedNativeXPerFrame",out _)&&row.TryGetProperty("Receipt",out _)),
                "Independent derivative receipts preserve their measured values instead of empty anonymous records");
        await File.WriteAllTextAsync(Path.Combine(directory,"independent-contact-velocity.json"),evidence);
        var limitClip=new AnimationClip(Guid.NewGuid(),"Independent fixed-joint conflict",4,new(60),false,[]);
        var limitSource=original with{Clips=[limitClip],Contacts=[]};
        var limitRequest=new RetargetRequest(limitSource,limitClip,rig,limitSource with{Clips=[]},rig,RootMotionPolicy.Preserve,false,default,null,null,null,null,null,null,null);
        var limitDescriptor=RigDescriptor.Prepare(original.Scene,rig);var limitBaseline=new BaselineMotionTransfer(limitRequest,limitDescriptor,limitDescriptor);
        var limitPlan=TargetMotionPlan.Build(limitRequest,limitDescriptor,limitDescriptor,limitBaseline,SourceMotionAnalysis.Analyze(limitRequest));
        Quaternion Turn(float degrees)=>Quaternion.CreateFromAxisAngle(Vector3.UnitZ,degrees*MathF.PI/180);
        var oscillating=new ContactReference(Guid.NewGuid(),"Independent rotating grip","Original piecewise-linear angular support")
        {
            GeometryKnown=true,Motion=[new(0,Vector3.Zero,Turn(0)),new(.5/60,Vector3.Zero,Turn(1)),new(1d/60,Vector3.Zero,Turn(0)),new(1.5/60,Vector3.Zero,Turn(-1)),new(2d/60,Vector3.Zero,Turn(0))]
        };
        var orientationPin=new ContactPin(Guid.NewGuid(),"Reviewed fixed axis",body,Vector3.Zero,rest.World[body].Translation,Quaternion.Identity,true,"Independent grip"){Reference=oscillating};
        limitPlan=limitPlan with
        {
            Controls=rig with{JointLimits=[new(body,new(-180,-180,0),new(180,180,0),"Independently fixed native Z rotation in fixture",true)]},
            Graph=new([new ContactFrame(Guid.NewGuid(),limitClip.Id,0,rig.SkeletonFingerprint,[orientationPin]){EndFrame=2}],MotionContactCoverage.Required,[])
        };
        var limitTracks=original.Scene.Bones.Select(bone=>new BoneTrack(bone.Name,bone.Rest.Channels().Select(value=>(Curve?)new Curve(Enumerable.Range(0,4).Select(frame=>new Key(frame,value)).ToArray())).ToArray())).ToArray();
        var limited=NativeContactVelocity.Initialize(limitPlan,limitTracks,evaluationBudget:10000);
        check(limited.Receipt.AcceptedKeys==0&&limited.Receipt.Reasons.ContainsKey("Contact velocity candidate exceeded reviewed native joint limits")&&limited.Tracks==limitTracks,
            "Required rotating support cannot make initialization introduce fractional motion on an independently fixed native joint axis");
        await File.WriteAllTextAsync(Path.Combine(directory,"independent-contact-velocity-limit.json"),JsonSerializer.Serialize(limited.Receipt,evidenceJson));
    }
}
