using FighterPort.Core;
using System.Numerics;
using System.Text.Json;

internal static class ReviewedContactSetChecks
{
    public static void Run(Action<bool,string> check)
    {
        bool Reject(Action action){try{action();return false;}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or NotSupportedException){return true;}}
        foreach(string variant in new[]{"original","renamed","unequal proportions","permuted mesh storage"})
        {
            bool renamed=variant=="renamed";
            var fixture=ProceduralFixture.Create(variant=="unequal proportions"?1.7f:1);var scene=fixture.Scene;
            if(renamed)scene=scene with{Bones=scene.Bones.Select((b,i)=>b with{Name="ReviewedJoint_"+i}).ToArray()};
            if(variant=="unequal proportions")scene=scene with{Bones=scene.Bones.Select((b,i)=>b with{Rest=b.Rest with{Translation=b.Rest.Translation*(i is 11 or 14?.7f:i is 12 or 15?1.3f:1)}}).ToArray()};
            if(variant=="permuted mesh storage")scene=scene with{Meshes=scene.Meshes.Reverse().Select(m=>m with{Vertices=m.Vertices.Reverse().ToArray(),Indices=m.Indices.Select(i=>(uint)(m.Vertices.Length-1-i)).ToArray()}).ToArray()};
            var rig=SurfaceContactChecks.Profile(scene);
            foreach(var chain in rig.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).ToArray())
                rig=AnimationControls.ReviewChain(scene,rig,chain.Role,chain.Root,chain.Middle,chain.Effector,Vector3.UnitZ,"Original procedural reviewed foot geometry").Value
                    ??throw new Exception("Procedural chain review failed.");
            var master=new AnimationClip(Guid.NewGuid(),"Unlabelled stationary motion",24,new(60),false,[]);
            var project=fixture with{Scene=scene,Clips=[master],ControlRigs=[rig],Contacts=[]};
            var pose=AnimationService.Evaluate(scene,master,0);
            var regions=SurfaceContacts.Regions(scene,rig).Where(r=>rig.Chains.Any(c=>c.Reviewed&&c.Effector==r.Bone&&c.Role.StartsWith("Foot.",StringComparison.Ordinal))).ToArray();
            float floor=regions.SelectMany(r=>r.Points).Min(p=>ContactGeometry.Point(p,pose).Y);
            var settings=new GroundContactSettings{SurfaceMode=SupportSurfaceMode.Ground,SourceFloor=floor,TargetFloor=floor,ExplicitSurface=true};
            string original=JsonSerializer.Serialize(project,ProjectStore.Json);
            var context=ReviewedContactSets.Suggest(project,master,rig,settings,[]);
            check(context.Ranges.Length==2,"Whole-foot geometry suggests two ranges on "+variant+" fixture without action-name rules");
            check(context.Ranges.All(r=>!r.Reviewed&&r.Region.Length>=r.Patch.Length&&r.AnchorPoints.Length==r.Patch.Length),"Suggestions retain complete skin regions and anchors but never imply approval");
            check(Reject(()=>ReviewedContactSets.Prepare(project,master,rig,context)),"Unreviewed suggested contacts cannot prepare an applicable animation");
            var reviewed=context with{Ranges=context.Ranges.Select(r=>r with{Reviewed=true}).ToArray()};
            var baked=ReviewedContactSets.Prepare(project,master,rig,reviewed);
            check(baked.Receipt.MaximumDrift<=baked.Receipt.PositionBound&&baked.Receipt.MaximumPenetration<=baked.Receipt.PositionBound&&baked.Receipt.MaximumOrientationError<=.25f,
                "Simultaneous stationary feet pass complete skin-patch and native Hermite bounds");
            check(baked.Clip.SampleCount==master.SampleCount&&baked.Clip.Rate==master.Rate&&baked.Clip.Loop==master.Loop,"Contact sets preserve native duration, rate and loop state");
            check(JsonSerializer.Serialize(project,ProjectStore.Json)==original,"Offline contact preparation leaves source document, skeleton and all weights immutable");
            var duplicate=reviewed with{Ranges=reviewed.Ranges.Append(reviewed.Ranges[0] with{Id=Guid.NewGuid().ToString("N")}).ToArray()};
            check(Reject(()=>ReviewedContactSets.ValidateContext(scene,master,rig,duplicate,true)),"Conflicting same-foot windows reject while simultaneous opposite feet remain legal");
            check(Reject(()=>ReviewedContactSets.ValidateContext(scene,master with{SampleCount=25},rig,reviewed,true)),"Changed animation timing invalidates the exact contact baseline");
            var wrongRegion=reviewed with{Ranges=reviewed.Ranges.Select((r,i)=>i==0?r with{Region=r.Region.Skip(1).ToArray()}:r).ToArray()};
            check(Reject(()=>ReviewedContactSets.ValidateContext(scene,master,rig,wrongRegion,true)),"Removing native skin-region evidence cannot make contact validation easier");
            var wrongDependencies=reviewed with{Ranges=reviewed.Ranges.Select((r,i)=>i==0?r with{Dependencies=r.Dependencies.Skip(1).ToArray()}:r).ToArray()};
            check(Reject(()=>ReviewedContactSets.ValidateContext(scene,master,rig,wrongDependencies,true)),"Complete ancestor and full-skin dependencies cannot be dropped from a reviewed contact");
            check(Reject(()=>ReviewedContactSets.Prepare(project,master,rig,reviewed with{ProtectedIntervals=[new(0,23,"Protected action")]})),"Suggestions cannot release an exact protected action/fitting middle");
            check(Reject(()=>ReviewedContactSets.ValidateContext(scene,master,rig,reviewed with{Ranges=reviewed.Ranges.Select((r,i)=>i==0?r with{Id="not-a-range"}:r).ToArray()},true)),"Invalid range identities reject before constructing native contact pins");
            check(Reject(()=>ReviewedContactSets.Validate(scene,baked.Clip,master,rig,reviewed,baked.Receipt.PositionBound*2,.25f,[7])),"A saved receipt cannot silently relax the limb-scaled contact bound");
            check(Reject(()=>ReviewedContactSets.Suggest(project,master,rig,settings with{ExplicitSurface=false},[])),"Missing reviewed support geometry stays unavailable");
            var fresh=JsonSerializer.Deserialize<ReviewedContactContext>(JsonSerializer.Serialize(reviewed,ProjectStore.Json),ProjectStore.Json)!;
            ReviewedContactSets.ValidateContext(scene,master with{Id=Guid.NewGuid(),Revision=Guid.NewGuid()},rig,fresh,true);
            check(ReviewedContactSets.ContentKey(fresh)==ReviewedContactSets.ContentKey(reviewed),"Complete contact context roundtrips without confusing fresh resource IDs with changed motion");
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;
            try{ReviewedContactSets.Prepare(project,master,rig,reviewed,cancelled.Token);}catch(OperationCanceledException){stopped=true;}
            check(stopped,"Cancelled contact preparation never returns a partially validated candidate");
            // Opposed anchors exceed the combined two-leg reach even when the
            // reviewed body may translate. A common translation is not, by
            // itself, a mathematical proof of unreachability.
            var unreachable=reviewed with{Ranges=reviewed.Ranges.Select((r,i)=>r with{Anchor=r.Anchor+new Vector3(i==0?1000:-1000,0,0),AnchorPoints=r.AnchorPoints.Select(p=>p+new Vector3(i==0?1000:-1000,0,0)).ToArray()}).ToArray()};
            check(Reject(()=>ReviewedContactSets.Prepare(project,master,rig,unreachable)),"Contradictory or unreachable anchors reject without root compensation or anatomy changes");
            if(variant=="original")
            {
                var approved=reviewed.Ranges[0];
                check(Reject(()=>ReviewedContactSets.SetRange(scene,master,rig,reviewed,approved.Role,approved.Start,approved.End,approved.Blend,approved.AnchorFrame,Vector3.UnitY,true)),"A floor contact cannot silently become a floating anchor through an Advanced offset");
                check(Reject(()=>ReviewedContactSets.ValidateContext(scene,master,rig,reviewed with{Ranges=reviewed.Ranges.Select((r,i)=>i==0?r with{AnchorPoints=r.AnchorPoints.Select((p,j)=>j==0?p+Vector3.UnitX:p).ToArray()}:r).ToArray()},true)),"A persisted contact cannot replace native patch shape with arbitrary point anchors");
                check(Reject(()=>ReviewedContactSets.ValidateContext(scene,master,rig,reviewed with{ShapeAdaptationPolicy="Release all helpers"},true)),"Unknown persisted adaptation policies reject without enabling new body dependencies");
                int effector=rig.Chains.First(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).Effector;
                var witness=AnimationService.Evaluate(scene,null,0,new Dictionary<int,Transform>{{effector,scene.Bones[effector].Rest with{Rotation=new(23.137f,17.271f,11.419f)}}});
                var actualOrientation=PoseGeometry.Rotation(witness.World[effector]);
                // Positive scalar multiples represent the SAME orientation.
                // This near-unit target reproduces a bounded float norm error
                // deterministically instead of searching platform-specific dots.
                var orientation=actualOrientation*.9999995f;
                check(2*MathF.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(actualOrientation,orientation)),0,1))>.001f,"Independent equivalent-quaternion fixture reproduces the raw dot/acos rejection floor");
                var fixedValues=witness.Local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t);
                var exactPin=new ContactPin(Guid.NewGuid(),"Independent fixed orientation",effector,Vector3.Zero,witness.World[effector].Translation,orientation,true,"Original procedural geometry");
                var exactResult=ContactSolver.Solve(scene,witness,rig,fixedValues,[exactPin]);
                check(exactResult.Value!=null&&exactResult.Value.RotationError<.00001f,"A fully fixed equivalent-orientation contact measures zero error without changing the pose");
                var retainedSeed=ContactSolver.Solve(scene,witness,rig,new Dictionary<int,Transform>(),[exactPin],referencePose:witness,preserveFeasibleSeed:true);
                check(retainedSeed.Value!=null&&retainedSeed.Value.Transforms.Count==0,"A feasible reviewed seed retains its exact pose instead of restarting redundant-joint IK");
                var rotated=exactPin with{Rotation=Quaternion.Concatenate(orientation,Quaternion.CreateFromAxisAngle(Vector3.UnitY,.002f))};
                check(ContactSolver.Solve(scene,witness,rig,fixedValues,[rotated]).Value==null,"A real .002-radian fixed orientation contradiction still fails the unchanged .001-radian solver gate");
                check(ContactSolver.Solve(scene,witness,rig,fixedValues,[rotated],preserveFeasibleSeed:true).Value==null,"Coherent-seed initialization cannot admit a genuinely infeasible orientation");
                check(ContactSolver.Solve(scene,witness,rig,fixedValues,[rotated],initializeChains:false).Value==null,"Subframe projection still rejects fixed infeasible contacts without an analytic IK restart");
                EvaluatedPose? rejectedTrial=null;
                var rejectedResult=ContactSolver.Solve(scene,witness,rig,fixedValues,[rotated],initializeChains:false,onRejectedTrial:p=>rejectedTrial=p);
                check(rejectedResult.Value is null&&rejectedTrial is not null,"Observing an unvalidated trial never changes the strict solver's failure result");
                var originalLocal=witness.Local[0];rejectedTrial!.Local[0]=originalLocal with{Translation=originalLocal.Translation+Vector3.One};
                check(witness.Local[0]==originalLocal,"Rejected-trial observers own copied arrays and cannot mutate the solver input pose");
                var lowChannels=new Curve?[9];float bodyHeight=scene.Bones[1].Rest.Translation.Y;
                lowChannels[7]=new([new(0,bodyHeight-.01f)]);
                var lowMaster=master with{Tracks=[new(scene.Bones[1].Name,lowChannels)]};var lowProject=project with{Clips=[lowMaster]};
                var lowContext=ReviewedContactSets.Suggest(lowProject,lowMaster,rig,settings,[]) with{Ranges=[]};
                foreach(var chain in rig.Chains.Where(c=>c.Reviewed&&c.Role.StartsWith("Foot.",StringComparison.Ordinal)))
                    lowContext=ReviewedContactSets.SetRange(scene,lowMaster,rig,lowContext,chain.Role,4,18,3,4,Vector3.Zero,true);
                bool fixedBoundary=false;try{ReviewedContactSets.Prepare(lowProject,lowMaster,rig,lowContext);}catch(InvalidOperationException error){fixedBoundary=error.Message.Contains("fixed penetrated contact boundary",StringComparison.Ordinal);}
                check(fixedBoundary,"A fixed penetrated boundary rejects before refinement instead of silently moving roots or relaxing floor clearance");
                lowChannels[7]=new([new(0,bodyHeight-.01f),new(1,bodyHeight-.01f),new(1,bodyHeight),new(21,bodyHeight),new(23,bodyHeight)]);
                var pairedMaster=lowMaster with{Tracks=[new(scene.Bones[1].Name,lowChannels)]};var pairedProject=project with{Clips=[pairedMaster]};
                var pairedContext=ReviewedContactSets.Suggest(pairedProject,pairedMaster,rig,settings,[]) with{Ranges=[]};
                foreach(var chain in rig.Chains.Where(c=>c.Reviewed&&c.Role.StartsWith("Foot.",StringComparison.Ordinal)))
                    pairedContext=ReviewedContactSets.SetRange(scene,pairedMaster,rig,pairedContext,chain.Role,4,18,3,4,Vector3.Zero,true);
                var pairedResult=ReviewedContactSets.Prepare(pairedProject,pairedMaster,rig,pairedContext);
                check(ReviewedContactSets.ContentKey(pairedMaster.Tracks)==ReviewedContactSets.ContentKey(pairedResult.Clip.Tracks),"Boundary preflight preserves an existing paired incoming/outgoing jump instead of classifying it as a new continuous-boundary contradiction");
                lowChannels[7]=new([new(0,bodyHeight-.01f),new(1,bodyHeight-.01f),new(21,bodyHeight),new(23,bodyHeight)]);
                var earlyMaster=lowMaster with{Tracks=[new(scene.Bones[1].Name,lowChannels)]};var earlyProject=project with{Clips=[earlyMaster]};
                var earlyContext=ReviewedContactSets.Suggest(earlyProject,earlyMaster,rig,settings,[]) with{Ranges=[]};
                foreach(var chain in rig.Chains.Where(c=>c.Reviewed&&c.Role.StartsWith("Foot.",StringComparison.Ordinal)))
                    earlyContext=ReviewedContactSets.SetRange(scene,earlyMaster,rig,earlyContext,chain.Role,4,18,3,4,Vector3.Zero,true);
                earlyContext=earlyContext with{FittingMiddle=new(0,1,"Explicit procedural fitting middle"),EarlierBodyCorrectionStart=0};
                var earlierResult=ReviewedContactSets.Prepare(earlyProject,earlyMaster,rig,earlyContext);
                check(earlierResult.Receipt.FirstChanged==0&&earlierResult.Receipt.MaximumDrift<=earlierResult.Receipt.PositionBound&&earlierResult.Receipt.MaximumPenetration<=earlierResult.Receipt.PositionBound,
                    "Explicit earlier body review establishes unchanged floor-contact bounds from a previously penetrated boundary");
                check(AnimationService.Evaluate(scene,earlyMaster,0).Local.SequenceEqual(AnimationService.Evaluate(scene,earlierResult.Clip,0).Local),"Earlier body review preserves the complete original first pose exactly");
                check(Reject(()=>ReviewedContactSets.Prepare(earlyProject,earlyMaster,rig,earlyContext with{ProtectedIntervals=[new(0,10,"Authored protected action")]})),"Earlier fitting-middle review cannot release a separately authored protected action");
                var pin=ContactSolver.Capture(scene,AnimationService.Evaluate(scene,earlyMaster,0),rig,"Foot.L");
                var authored=new ContactFrame(Guid.NewGuid(),earlyMaster.Id,0,rig.SkeletonFingerprint,[pin]){EndFrame=1};
                check(Reject(()=>ReviewedContactSets.Prepare(earlyProject with{Contacts=[authored]},earlyMaster,rig,earlyContext with{OriginalContacts=[authored]})),"Earlier body review cannot override an authored pin or its dependency closure");
                var channels=new Curve?[9];channels[7]=new([new(0,scene.Bones[1].Rest.Translation.Y),new(11,scene.Bones[1].Rest.Translation.Y+.004f),new(23,scene.Bones[1].Rest.Translation.Y)]);
                var moving=master with{Tracks=[new(scene.Bones[1].Name,channels)]};var movingProject=project with{Clips=[moving]};
                var movingContext=ReviewedContactSets.Suggest(movingProject,moving,rig,settings,[]);
                // Detection may classify this moving patch as uncertain/rolling;
                // explicit review supplies ranges without changing thresholds.
                movingContext=movingContext with{Ranges=[]};
                foreach(var chain in rig.Chains.Where(c=>c.Reviewed&&c.Role.StartsWith("Foot.",StringComparison.Ordinal)))
                    movingContext=ReviewedContactSets.SetRange(scene,moving,rig,movingContext,chain.Role,4,18,3,4,Vector3.Zero,true);
                check(Reject(()=>ReviewedContactSets.Prepare(movingProject,moving,rig,movingContext)),"A split sparse exterior that changes native float evaluation rejects rather than weakening exact preservation");
                channels[7]=new([new(0,scene.Bones[1].Rest.Translation.Y),new(1,scene.Bones[1].Rest.Translation.Y),new(11,scene.Bones[1].Rest.Translation.Y+.004f),new(21,scene.Bones[1].Rest.Translation.Y),new(23,scene.Bones[1].Rest.Translation.Y)]);
                moving=moving with{Tracks=[new(scene.Bones[1].Name,channels)]};movingProject=project with{Clips=[moving]};
                movingContext=ReviewedContactSets.Suggest(movingProject,moving,rig,settings,[]) with{Ranges=[]};
                foreach(var chain in rig.Chains.Where(c=>c.Reviewed&&c.Role.StartsWith("Foot.",StringComparison.Ordinal)))
                    movingContext=ReviewedContactSets.SetRange(scene,moving,rig,movingContext,chain.Role,4,18,3,4,Vector3.Zero,true);
                var corrected=ReviewedContactSets.Prepare(movingProject,moving,rig,movingContext,progress:Console.WriteLine);
                check(corrected.Changed.Length>0,"Coupled full-foot correction changes actual body curves rather than validating an unchanged pose");
                check(corrected.Receipt.MaximumDrift<=corrected.Receipt.PositionBound&&corrected.Receipt.MaximumPenetration<=corrected.Receipt.PositionBound,
                    "Corrected moving body retains simultaneous full-patch anchors after native interpolation");
                var tampered=corrected.Clip with{Tracks=corrected.Clip.Tracks.Select(t=>t.Bone==scene.Bones[1].Name?t with{Channels=t.Channels.Select((c,i)=>i==7?new Curve([new(0,scene.Bones[1].Rest.Translation.Y+.5f)]):c).ToArray()}:t).ToArray()};
                check(Reject(()=>ReviewedContactSets.Validate(scene,tampered,moving,rig,movingContext,corrected.Receipt.PositionBound,.25f,[])),"Fresh validation independently supplies approach/release/exterior witnesses even when receipt witnesses are empty");
                var foot=rig.Chains.First(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).Effector;
                var extra=new Vertex(new(0,floor-.1f,0),Vector3.UnitY,Vector2.Zero,new(foot,3,0,0),new(.25f,.75f,0,0),Vector4.One);
                var fullScene=scene with{Meshes=scene.Meshes.Append(new Mesh("Independent low foot-influence witness",[extra],[])).ToArray()};
                var fullProject=project with{Scene=fullScene};var fullContext=ReviewedContactSets.Suggest(fullProject,master,rig,settings,[]) with{Ranges=[]};
                foreach(var chain in rig.Chains.Where(c=>c.Reviewed&&c.Role.StartsWith("Foot.",StringComparison.Ordinal)))
                    fullContext=ReviewedContactSets.SetRange(fullScene,master,rig,fullContext,chain.Role,4,18,3,4,Vector3.Zero,true);
                bool soleContradiction=false;
                try{ReviewedContactSets.Prepare(fullProject,master,rig,fullContext);}catch(InvalidOperationException error){soleContradiction=error.Message.Contains("frozen full-sole anchor contradicts",StringComparison.Ordinal);}
                check(soleContradiction,"Full-foot preflight proves a minority-influence sole/floor contradiction before spending the solve budget");
            }
            if(variant!="original")MovingVariant(project,master,rig,settings,check);
        }
    }

    static void MovingVariant(FighterProject project,AnimationClip master,ControlRigSetup rig,GroundContactSettings settings,Action<bool,string> check)
    {
        var scene=project.Scene;int body=AnimationControls.ResolveBodyBone(scene,rig);
        var channels=new Curve?[9];float rest=scene.Bones[body].Rest.Translation.Y;
        channels[7]=new([new(0,rest),new(1,rest),new(11,rest+.004f),new(21,rest),new(23,rest)]);
        var moving=master with{Tracks=[new(scene.Bones[body].Name,channels)]};var input=project with{Clips=[moving]};
        var context=ReviewedContactSets.Suggest(input,moving,rig,settings,[]) with{Ranges=[]};
        foreach(var chain in rig.Chains.Where(c=>c.Reviewed&&c.Role.StartsWith("Foot.",StringComparison.Ordinal)))
            context=ReviewedContactSets.SetRange(scene,moving,rig,context,chain.Role,4,18,3,4,Vector3.Zero,true);
        var result=ReviewedContactSets.Prepare(input,moving,rig,context);
        check(result.Changed.Length>0&&result.Receipt.MaximumDrift<=result.Receipt.PositionBound&&result.Receipt.MaximumPenetration<=result.Receipt.PositionBound,
            "Moving renamed/unequal/permuted recovery meets unchanged simultaneous contact bounds");
        check(result.Receipt.PoseEvaluations<=2000,"Small moving reviewed trajectories stay well below the unchanged 30,000-pose budget");
        check(new[]{0d,23d}.All(t=>AnimationService.Evaluate(scene,moving,t).Local.SequenceEqual(AnimationService.Evaluate(scene,result.Clip,t).Local)),
            "Moving renamed/unequal/permuted recovery retains exact native start and end poses");
    }
}
