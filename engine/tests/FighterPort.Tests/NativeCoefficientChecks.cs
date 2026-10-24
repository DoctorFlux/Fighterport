using System.Numerics;
using System.Text.Json;
using FighterPort.Core;

// Original independent representation fixture. Its analytic destination is a
// constant-rate turn about a measured support frame, exactly representable by
// integer Hermite values and shared slopes. No game asset or native codec is
// used; fresh adapter encoding remains a separate acceptance gate.
internal static class NativeCoefficientChecks
{
    public static async Task RunAsync(string directory,Action<bool,string> check)
    {
        Directory.CreateDirectory(directory);
        var small=ProceduralFixture.Create(.6f);var large=ProceduralFixture.Create(1.6f);
        var smallRig=SurfaceContactChecks.Profile(small.Scene);var largeRig=SurfaceContactChecks.Profile(large.Scene);
        large=large with{Scene=large.Scene with{SkeletonRevision=Guid.NewGuid(),Bones=large.Scene.Bones.Select((bone,i)=>bone with{Name=$"NativeBranch_{i}"}).ToArray()}};
        largeRig=largeRig with{SkeletonFingerprint=AnimationControls.Fingerprint(large.Scene)};
        string smallProof=AnimationControls.DeformChecksum(small.Scene),largeProof=AnimationControls.DeformChecksum(large.Scene);
        var rows=new List<object>();
        foreach(bool reverse in new[]{false,true})
        {
            var source=reverse?large:small;var target=reverse?small:large;
            var sourceRig=reverse?largeRig:smallRig;var targetRig=reverse?smallRig:largeRig;
            int body=targetRig.BodyBone!.Value,sourceBody=sourceRig.BodyBone!.Value;
            const int samples=8;const float degreesPerFrame=3;
            var sourceChannels=new Curve?[9];sourceChannels[5]=new([new(0,0,degreesPerFrame,degreesPerFrame),new(samples-1,(samples-1)*degreesPerFrame,degreesPerFrame,degreesPerFrame)]);
            var clip=new AnimationClip(Guid.NewGuid(),"Original analytic support turn",samples,new(60),false,[new(source.Scene.Bones[sourceBody].Name,sourceChannels)]);
            source=source with{Clips=[clip],Contacts=[]};target=target with{Clips=[],Contacts=[]};
            string beforeSource=JsonSerializer.Serialize(source,ProjectStore.Json);
            var request=new RetargetRequest(source,clip,sourceRig,target,targetRig,RootMotionPolicy.Preserve,true,default,null,null,null,null,null,null,null);
            var sourceDescriptor=RigDescriptor.Prepare(source.Scene,sourceRig);var targetDescriptor=RigDescriptor.Prepare(target.Scene,targetRig);
            var baseline=new BaselineMotionTransfer(request,sourceDescriptor,targetDescriptor);
            var plan=TargetMotionPlan.Build(request,sourceDescriptor,targetDescriptor,baseline,SourceMotionAnalysis.Analyze(request));
            var rest=AnimationService.Evaluate(target.Scene,null,0);
            var origin=rest.World[body].Translation;
            var mesh=target.Scene.Meshes[0];int vertexIndex=Array.FindIndex(mesh.Vertices,v=>v.Weights.X==1&&(int)v.Joints.X==body);
            var vertex=mesh.Vertices[vertexIndex];var localPoint=vertex.Position-origin;
            Quaternion Turn(double frame)=>Quaternion.CreateFromAxisAngle(Vector3.UnitZ,(float)frame*degreesPerFrame*MathF.PI/180);
            var reference=new ContactReference(Guid.NewGuid(),"Independently measured rotating support","Original analytic 180-degrees/second frame")
            {
                GeometryKnown=true,Motion=[new(0,origin,Turn(0)),new((samples-1)/60d,origin,Turn(samples-1))]
            };
            var pin=new ContactPin(Guid.NewGuid(),"Authored material point",body,Vector3.Zero,localPoint,Quaternion.Identity,true,"Measured rigid support")
            {
                Reference=reference,SurfacePoints=[new(vertex.Position,vertex.Joints,vertex.Weights){Mesh=0,Vertex=vertexIndex}]
            };
            plan=plan with{Graph=new([new ContactFrame(Guid.NewGuid(),clip.Id,0,targetRig.SkeletonFingerprint,[pin]){EndFrame=samples-1}],MotionContactCoverage.Required,[])};
            var frames=Enumerable.Range(0,(samples-1)*TemporalRetargetSolver.Substeps+1).Select(i=>i/(double)TemporalRetargetSolver.Substeps).ToArray();
            // Deliberately invalid values AND slopes are an initialization,
            // not a second expected-contact oracle. A slope-only method cannot
            // correct the independently wrong integer orientations.
            var poses=frames.Select(frame=>
            {
                var local=target.Scene.Bones.Select(bone=>bone.Rest).ToArray();
                float error=(float)(7+.025*Math.Pow(frame-3.5,3));
                local[body]=local[body] with{Rotation=new(0,0,(float)frame*degreesPerFrame+error)};
                return local;
            }).ToArray();
            var seed=new SolvedTargetMotion(frames,poses,new("Independently perturbed coefficient initialization",frames.Length,0,0,0,0,0,0,"Not accepted poses"));
            string seedProof=JsonSerializer.Serialize(seed,ProjectStore.Json);
            var initial=NativeCurveBaker.Fit(plan,seed,false,evaluationBudget:1);
            var fitted=NativeCurveBaker.Fit(plan,seed,false,evaluationBudget:100000);
            var output=new AnimationClip(Guid.NewGuid(),"Prepared coefficient result",samples,new(60),false,fitted.Tracks);
            double maximumPosition=0,maximumRotation=0,worstPositionFrame=0,worstRotationFrame=0;
            // Withheld 1/32 samples are independent of the eighth-fit lattice.
            foreach(double frame in Enumerable.Range(0,(samples-1)*32+1).Select(i=>i/32d).Concat([.07031,1.3333,4.7777,6.9999]))
            {
                var actual=AnimationService.Evaluate(target.Scene,output,frame);
                var expected=origin+Vector3.Transform(localPoint,Turn(frame));
                double position=Vector3.Distance(AnimationService.Skin(vertex,actual),expected);
                var delta=Quaternion.Normalize(PoseGeometry.Rotation(actual.World[body])*Quaternion.Inverse(Turn(frame)));
                double rotation=2*Math.Atan2(new Vector3(delta.X,delta.Y,delta.Z).Length(),Math.Abs(delta.W));
                if(position>maximumPosition){maximumPosition=position;worstPositionFrame=frame;}
                if(rotation>maximumRotation){maximumRotation=rotation;worstRotationFrame=frame;}
            }
            // Persist the measured failure as well as a passing case. The
            // harness's Check throws, so evidence must precede its assertions.
            var evidenceOptions=new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false,WriteIndented=true};
            await File.WriteAllTextAsync(Path.Combine(directory,$"coefficient-candidate-{(reverse?"reverse":"forward")}.json"),JsonSerializer.Serialize(new
            {
                Claim="Measured candidate before assertions; not a success receipt.",Reverse=reverse,
                MaximumPosition=maximumPosition,PositionBound=plan.PositionBound,WorstPositionFrame=worstPositionFrame,
                MaximumRotation=maximumRotation,RotationBound=RetargetAcceptance.RotationRadians,WorstRotationFrame=worstRotationFrame,
                Initial=initial.Receipt,Final=fitted.Receipt,
                InitialBody=initial.Tracks[body],FinalBody=fitted.Tracks[body],
                Probes=Enumerable.Range(0,(samples-1)*8+1).Select(i=>i/8d).Select(frame=>new
                {
                    Frame=frame,ExpectedZDegrees=frame*degreesPerFrame,
                    Actual=AnimationService.Evaluate(target.Scene,output,frame).Local[body],
                    Constraint=TargetMotionConstraints.MaximumNormalizedViolation(plan,frame,AnimationService.Evaluate(target.Scene,output,frame))
                }).ToArray()
            },evidenceOptions));
            check(fitted.Receipt.KeyValueUpdates>0&&initial.Tracks[body].Channels[5]!.Keys.Zip(fitted.Tracks[body].Channels[5]!.Keys).Any(pair=>pair.First.Value!=pair.Second.Value),
                $"Joint coefficient restoration changes invalid integer values on unequal renamed native rigs, reverse={reverse}");
            check(initial.Tracks[body].Channels[5]!.Keys.Zip(fitted.Tracks[body].Channels[5]!.Keys).Any(pair=>pair.First.OutTangent!=pair.Second.OutTangent),
                $"Joint coefficient restoration also changes shared slopes, reverse={reverse}");
            check(maximumPosition<=plan.PositionBound&&maximumRotation<=RetargetAcceptance.RotationRadians,
                $"Independent rotating material point/orientation satisfy frozen bounds at integers and withheld fractions, reverse={reverse}; position={maximumPosition:G9}, rotation={maximumRotation:G9}");
            check(initial.Tracks.Zip(fitted.Tracks).All(pair=>pair.First.Channels.Zip(pair.Second.Channels).All(channel=>channel.First!.Keys.Select(key=>key.Frame).SequenceEqual(channel.Second!.Keys.Select(key=>key.Frame))&&channel.Second.Keys.All(key=>key.InTangent==key.OutTangent))),
                "Coefficient restoration preserves every integer time, channel and the single shared native derivative");
            int root=targetDescriptor.Binding.Roles["RootMotion"];
            check(initial.Tracks[root].Channels.Zip(fitted.Tracks[root].Channels).All(pair=>pair.First!.Keys.SequenceEqual(pair.Second!.Keys))
                &&initial.Tracks.Zip(fitted.Tracks).All(pair=>Enumerable.Range(0,3).All(channel=>pair.First.Channels[channel]!.Keys.SequenceEqual(pair.Second.Channels[channel]!.Keys)))
                &&Enumerable.Range(0,target.Scene.Bones.Length).Where(bone=>bone!=body).All(bone=>Enumerable.Range(6,3).All(channel=>initial.Tracks[bone].Channels[channel]!.Keys.SequenceEqual(fitted.Tracks[bone].Channels[channel]!.Keys))),
                "Root, authored scale and non-body translation coefficients remain immutable");
            check(output.SampleCount==samples&&output.Rate==clip.Rate&&!output.Loop&&output.LastKeyFrame==samples-1&&plan.Pins(samples-1).Length==1&&plan.Pins(samples-1+1d/1024).Length==0,
                "Representation restoration does not retime, add a closing sample or extend contact intervals");
            check(fitted.Receipt.PoseEvaluations<=fitted.Receipt.EvaluationBudget&&!fitted.Receipt.BudgetExhausted,
                "Joint values/slopes and derivative initialization share one bounded native work budget");
            var repeated=NativeCurveBaker.Fit(plan,seed,false,evaluationBudget:100000);
            check(JsonSerializer.Serialize(repeated.Tracks,ProjectStore.Json)==JsonSerializer.Serialize(fitted.Tracks,ProjectStore.Json),
                "Identical prepared coefficient requests are deterministic and independent of prior playback");
            bool cancelled=false;
            using(var cancellation=new CancellationTokenSource())
            {
                cancellation.Cancel();
                try{NativeCurveBaker.Fit(plan,seed,false,cancellation.Token,evaluationBudget:10000);}
                catch(OperationCanceledException){cancelled=true;}
            }
            check(cancelled,"Cancellation stops the real coefficient preparation before publishing a result");
            check(seedProof==JsonSerializer.Serialize(seed,ProjectStore.Json)&&beforeSource==JsonSerializer.Serialize(source,ProjectStore.Json),
                "Joint coefficient restoration does not mutate its initialization or authentic source");
            rows.Add(new{Reverse=reverse,MaximumPosition=maximumPosition,PositionBound=plan.PositionBound,WorstPositionFrame=worstPositionFrame,MaximumRotation=maximumRotation,RotationBound=RetargetAcceptance.RotationRadians,WorstRotationFrame=worstRotationFrame,fitted.Receipt});

            // This exact contradiction is independently known: the actor root
            // is fixed by policy, but the required point asks it to move .1.
            var impossiblePin=new ContactPin(Guid.NewGuid(),"Fixed actor conflict",root,Vector3.Zero,rest.World[root].Translation+new Vector3(.1f,0,0),Quaternion.Identity,false,"Authored fixed point");
            var incompatible=plan with{Graph=new([new ContactFrame(Guid.NewGuid(),clip.Id,0,targetRig.SkeletonFingerprint,[impossiblePin]){EndFrame=samples-1}],MotionContactCoverage.Required,[])};
            var blocked=NativeCurveBaker.Fit(incompatible,seed,false,evaluationBudget:10000);
            var blockedClip=output with{Tracks=blocked.Tracks};
            var blockedPose=AnimationService.Evaluate(target.Scene,blockedClip,2.25);
            check(blocked.Receipt.ImmutableConstraintWindows>0&&blocked.Receipt.KeyValueUpdates==0
                &&Vector3.Distance(blockedPose.World[root].Translation,impossiblePin.Position)>plan.PositionBound,
                "An independently contradictory fixed-root pin remains unresolved instead of shifting the actor or reporting a feasible curve");
        }
        check(smallProof==AnimationControls.DeformChecksum(small.Scene)&&largeProof==AnimationControls.DeformChecksum(large.Scene),
            "Both original native hierarchies, rests and full retained skin weights are unchanged");
        var json=new JsonSerializerOptions(ProjectStore.Json){IgnoreReadOnlyProperties=false,WriteIndented=true};
        await File.WriteAllTextAsync(Path.Combine(directory,"independent-native-coefficients.json"),JsonSerializer.Serialize(new
        {
            Version=NativeCurveBaker.Version,Claim="Core integer/shared-Hermite coefficient restoration only; not native encoding, automatic relationship detection, full action optimization or game acceptance.",
            Cases=rows
        },json));
    }
}
