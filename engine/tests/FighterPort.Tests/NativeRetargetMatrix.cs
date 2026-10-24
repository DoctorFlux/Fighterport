using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class NativeRetargetMatrix
{
    public static async Task RunAsync(BrawlAdapter adapter,string marioPath,string snakePath,string output,Action<bool,string> check,bool explicitFloor=false,bool ledge=false)
    {
        var mario=await ProjectStore.LoadAsync(marioPath);var snake=await ProjectStore.LoadAsync(snakePath);var receipts=new List<object>();
        string[] names=["Wait1","DownWaitU","DownWaitD","DownAttackU","DownAttackD","DownStandD","DownStandU","DownBoundU","DownBoundD","CliffWait","CliffClimbSlow","CliffClimbQuick","CliffAttackSlow","CliffAttackQuick","Attack11","SlipWait"];
        if(Environment.GetEnvironmentVariable("FIGHTERPORT_RETARGET_CASES") is {} filter)names=filter.Split(',');
        int passed=0,conflicts=0,unresolved=0,unsupported=0,invalid=0,nativeFailures=0;foreach(var (from,to,source,target) in new[]{("Snake","Mario",snake,mario),("Mario","Snake",mario,snake)})
        {
            var a=BrawlControlProfiles.SetUp(source);var b=BrawlControlProfiles.SetUp(target);var template=target.Clips.Single(c=>c.Source?.NativeName=="Wait1");var receiver=target with{Clips=[template],ControlRigs=[b]};
            var support=new GroundContactSettings{ExplicitSurface=explicitFloor||ledge,SourceFloor=explicitFloor||ledge?0:null,TargetFloor=explicitFloor||ledge?0:null,SourceEdge=ledge?new(a.Axes!.Forward,0):null,TargetEdge=ledge?new(b.Axes!.Forward,0):null};
            var coverage=RetargetBoneMapping.Build(source.Scene,a,target.Scene,b);var fingers=coverage.Bones.Where(m=>m.Role.StartsWith("Finger.")).ToArray();check(fingers.Length==20,$"{from} → {to}: both hands include all twenty reviewed native finger joints");
            var fingerClip=source.Clips.Single(c=>c.Source?.NativeName=="Wait1");var fingerFk=RetargetService.Bake(source,fingerClip,a,receiver,b,useContacts:false).Value??throw new Exception("Detailed FK transfer failed");
            check(RetargetDependencies.SourceKey(source,fingerClip,a,support)==RetargetDependencies.SourceKey(source,fingerClip,a,support with{TargetFloor=123,TargetEdge=new(b.Axes!.Forward,17)}),$"{from} → {to}: changing target floor/ledge coordinates cannot rewrite authentic source analysis dependencies");
            foreach(double frame in new[]{0d,Math.Floor(fingerClip.LastSampleFrame/2),fingerClip.LastSampleFrame})
            {
                var src=AnimationService.Evaluate(source.Scene,fingerClip,frame);var dst=AnimationService.Evaluate(target.Scene,fingerFk.Clip,frame);
                check(fingers.All(m=>Math.Abs(Quaternion.Dot(PoseGeometry.Rotation((Transform.Identity with{Rotation=src.Local[m.Source].Rotation}).Matrix),PoseGeometry.Rotation((Transform.Identity with{Rotation=dst.Local[m.Target].Rotation}).Matrix)))>.99999f),$"{from} → {to}: all finger curls preserve independently expected native local orientations at {frame}");
            }
            string sourceRig=AnimationControls.DeformChecksum(source.Scene),targetRig=AnimationControls.DeformChecksum(target.Scene);var inputs=ProjectStore.SourceReferences(source).Concat(ProjectStore.SourceReferences(receiver)).DistinctBy(r=>r.Path).Select(r=>(r.Path,Hash:ProjectStore.Hash(r.Path))).ToArray();
            foreach(string name in names)
            {
                Console.WriteLine($"TEST: {RetargetBackendIdentity.CurrentId} / {from} → {to} / {name}");var watch=Stopwatch.StartNew();var clip=source.Clips.Single(c=>c.Source?.NativeName==name);var baked=RetargetService.Bake(source,clip,a,receiver,b,ground:support);
                if(baked.Value is not {} value)
                {
                    string outcome;
                    if(baked.Diagnostics.Any(d=>d.Code=="PROVEN_INCOMPATIBLE")){conflicts++;outcome="contradictory";}
                    else if(baked.Diagnostics.Any(d=>d.Code=="RETARGET_UNSUPPORTED")){unsupported++;outcome="unsupported";}
                    else if(baked.Diagnostics.Any(d=>d.Code=="RETARGET_INPUT")){invalid++;outcome="invalid-input";}
                    else{unresolved++;outcome="unresolved";}
                    receipts.Add(new{From=from,To=to,Clip=name,Status=outcome,Seconds=watch.Elapsed.TotalSeconds,baked.Diagnostics,baked.LostChannels});Console.WriteLine("REVIEW: "+string.Join("; ",baked.Diagnostics.Select(d=>d.Message)));await Save();continue;
                }
                bool contactsSuspended=baked.Diagnostics.Any(d=>d.Code=="RETARGET_CONTACTS_DISABLED_REVIEW");
                string phaseCoverage=contactsSuspended?"unavailable-contact-reconstruction-disabled":value.Grounding is null?"unavailable-no-supplied-surface-context":"measured-detector-phases";
                if(value.Contacts.Length>0&&clip.Rate==value.Clip.Rate)check(value.Contacts.All(c=>c.Frame==Math.Truncate(c.Frame)&&c.EndFrame==Math.Truncate(c.EndFrame??c.Frame)),$"{from} → {to} {name}: unchanged timing keeps emitted contact boundaries on their exact source frames");
                var project=RetargetService.Apply(receiver,value);string native=Path.Combine(output,$"{from}-to-{to}-{name}-{Guid.NewGuid():N}.chr0");await adapter.ExportAsync(project,value.Clip,native);
                // Independent fixture annotations: these supplied get-up/attack
                // clips finish above the platform, unlike the hanging idle.
                // Names select test inputs only; no solver rule uses them.
                if(!contactsSuspended&&ledge&&name is "CliffClimbSlow" or "CliffAttackSlow")check(value.Grounding?.Phases.Any(p=>clip.LastSampleFrame>=p.Start&&clip.LastSampleFrame<p.End+1&&p.Mode!=SupportSurfaceMode.Edge)==true,$"{from} → {to} {name}: the annotated above-platform ending exits hanging mode");
                check(fingers.All(m=>value.Clip.Tracks.Any(t=>t.Bone==target.Scene.Bones[m.Target].Name)),$"{from} → {to} {name}: emitted native target includes every mapped finger channel");
                double last=value.Clip.LastSampleFrame;double[] times=new[]{0,.125,Math.Min(3.2,last),Math.Min(7+1d/3,last),Math.Min(3.375,last),Math.Min(7.75,last),Math.Min(Math.Floor(last/2)+.375,last),Math.Max(0,last-.25),last}.Concat(new[]{13.8107,38.9002,59.9999}).Where(f=>f>=0&&f<value.Clip.SampleCount).Distinct().Order().ToArray();
                var fresh=await adapter.RunAsync(new("import",template.Source!.Model!.Path,native,null,null,[],times));var reopened=fresh.Project!;var vertices=reopened.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();float boneError=0,skinError=0,nativeEvaluatorError=0;var differences=new List<object>();
                for(int f=0;f<times.Length;f++)
                {
                    var pose=AnimationService.Evaluate(target.Scene,value.Clip,times[f]);var decoded=AnimationService.Evaluate(reopened.Scene,reopened.Clips[0],times[f]);for(int i=0;i<pose.World.Length;i++){var p=fresh.ExpectedBonePositions![f][i];boneError=Math.Max(boneError,Vector3.Distance(pose.World[i].Translation,new(p[0],p[1],p[2])));float error=pose.Local[i].Channels().Zip(decoded.Local[i].Channels()).Max(p=>Math.Abs(p.First-p.Second));if(error>.000001f)differences.Add(new{Frame=times[f],Bone=target.Scene.Bones[i].Name,Error=error,Expected=pose.Local[i],Decoded=decoded.Local[i]});}
                    for(int v=0;v<vertices.Length;v++){var p=fresh.ExpectedVertices![f][v];var expected=new Vector3(p[0],p[1],p[2]);skinError=Math.Max(skinError,Vector3.Distance(AnimationService.Skin(vertices[v],pose),expected));nativeEvaluatorError=Math.Max(nativeEvaluatorError,Vector3.Distance(AnimationService.Skin(vertices[v],decoded),expected));}
                }
                var grounding=value.Grounding;float? floor=grounding?.TargetFloor,sourceBelow=grounding is null?null:0,targetBelow=grounding is null?null:0;
                float maxGroundDepth=0,maxContact=0,adaptedSupport=0;double worstGroundFrame=0;int groundSamples=0,edgeSamples=0;
                var ownVertices=target.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();var sourceVertices=source.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();
                for(double f=0;f<=last;f+=.25)
                {
                    var pose=AnimationService.Evaluate(target.Scene,value.Clip,f);
                    if(grounding is not null)
                    {
                        float min=ownVertices.Min(v=>Vector3.Dot(AnimationService.Skin(v,pose),b.Axes!.Up));targetBelow=Math.Max(targetBelow!.Value,grounding.TargetFloor-min);
                        var sourcePose=AnimationService.Evaluate(source.Scene,clip,f);sourceBelow=Math.Max(sourceBelow!.Value,grounding.SourceFloor-sourceVertices.Min(v=>Vector3.Dot(AnimationService.Skin(v,sourcePose),a.Axes!.Up)));
                    }
                    var active=value.Contacts.Where(c=>c.Contains(f)).SelectMany(c=>c.Pins).ToArray();foreach(var pin in active){float error=ContactGeometry.Residual(pin,pose).Length();if(pin.SoftSupport)adaptedSupport=Math.Max(adaptedSupport,error);else maxContact=Math.Max(maxContact,error);}
                    var phase=grounding?.Phases.FirstOrDefault(p=>f>=p.Start&&f<p.End+1);
                    if(phase?.Mode==SupportSurfaceMode.Edge)edgeSamples++;
                    if(grounding is not null&&(grounding.ExplicitSurface||phase?.Mode==SupportSurfaceMode.Ground)){groundSamples++;var geometry=new SupportSurface(b.Axes!.Up,grounding.TargetFloor,grounding.TargetEdge);float depth=ownVertices.Max(v=>geometry.Penetration(AnimationService.Skin(v,pose)));if(depth>maxGroundDepth){maxGroundDepth=depth;worstGroundFrame=f;}}
                }
                var (exactNative,nativeComparisons,nativeDifferences)=ExactCurves(value.Clip,reopened.Clips[0],reopened.Scene);
                if(!exactNative)await File.WriteAllTextAsync(native+".expected-clip.json",JsonSerializer.Serialize(value.Clip,ProjectStore.Json));
                bool numeric=exactNative&&boneError<.005f&&skinError<.0003f&&reopened.Clips[0].SampleCount==clip.SampleCount&&reopened.Clips[0].Loop==clip.Loop&&reopened.Clips[0].Rate==clip.Rate;
                var baselineResult=RetargetService.Bake(source,clip,a,receiver,b,useContacts:false);
                var actionBaseline=baselineResult.Value??throw new Exception("Action baseline transfer failed: "+string.Join("; ",baselineResult.Diagnostics.Select(d=>d.Code+": "+d.Message)));
                var nativeAction=RetargetAcceptance.MeasureAction(reopened.Scene,reopened.Clips[0],f=>AnimationService.Evaluate(target.Scene,actionBaseline.Clip,f),coverage.Bones.Where(m=>!m.DetailChannels).Select(m=>m.Target),rig:b,bodyFittingAt:f=>RetargetAcceptance.BodyFittingAt(value,f));
                if(numeric){check(true,$"{from} → {to} {name}: genuine native reopen verifies all-bone/skin/timing ({boneError:G5} / {skinError:G5})");passed++;}else{nativeFailures++;Console.WriteLine($"FAIL: native pose/skin write {boneError:G7} / {skinError:G7}; decoded skin evaluator {nativeEvaluatorError:G7}");}
                var contactValidation=RetargetAcceptance.Measure(target,b,value);var nativeContactValidation=RetargetAcceptance.Measure(reopened,b,value with{Clip=reopened.Clips[0] with{Retarget=value.Clip.Retarget}});float tolerance=RigCharacterization.Describe(target.Scene,b).LegLength*.002f;string status=!numeric?"native-validation-failed":contactsSuspended?"native-passed-pose-draft-contact-suspended":!contactValidation.Passed||!nativeContactValidation.Passed?"native-passed-contact-review":value.Clip.Retarget!.ActionFidelity?.Passed==false||!nativeAction.Passed?"native-passed-action-review":baked.Diagnostics.Any(d=>d.Code is "UNSUPPORTED_SURFACE_CONTEXT" or "EDGE_EXTENT_REVIEW" or "INFERRED_CLEARANCE_CONTEXT" or "LOW_BODY_CONTACT_ADAPTATION")?"native-passed-context-review":adaptedSupport>tolerance?"native-passed-support-adapted":"passed";
                object? edgeWitness=null;
                if(ledge&&floor is {} edgeFloor)
                {
                    // Direct deformed geometry witnesses at the two ends. Do
                    // not use the detector's own residual as the contact oracle.
                    var regions=SurfaceContacts.Regions(reopened.Scene,b);var up=b.Axes!.Up;var inward=support.TargetEdge!.Inward;
                    object Geometry(EvaluatedPose pose)=>regions.Where(r=>r.Role.StartsWith("Hand.")||r.Role.StartsWith("Foot.")).Select(r=>new{r.Role,CornerDistance=r.Points.Min(p=>{var v=ContactGeometry.Point(p,pose);return MathF.Sqrt(MathF.Pow(Vector3.Dot(v,up)-edgeFloor,2)+MathF.Pow(Vector3.Dot(v,inward)-support.TargetEdge.Offset,2));}),MinimumHeight=r.Points.Min(p=>Vector3.Dot(ContactGeometry.Point(p,pose),up)-edgeFloor)}).ToArray();
                    int hips=RigDescriptor.Prepare(target.Scene,b).Binding.Roles["Hips"];
                    edgeWitness=new{First=Geometry(AnimationService.Evaluate(reopened.Scene,reopened.Clips[0],0)),RawFirst=Geometry(AnimationService.Evaluate(target.Scene,actionBaseline.Clip,0)),Last=Geometry(AnimationService.Evaluate(reopened.Scene,reopened.Clips[0],last)),HipsFirst=AnimationService.Evaluate(reopened.Scene,reopened.Clips[0],0).World[hips].Translation,HipsRaw=AnimationService.Evaluate(target.Scene,actionBaseline.Clip,0).World[hips].Translation,FirstEdgePins=value.Contacts.Where(c=>c.Contains(0)).SelectMany(c=>c.Pins).Count(p=>p.EdgeSupport),FinalGroundPins=value.Contacts.Where(c=>c.Contains(last)).SelectMany(c=>c.Pins).Count(p=>!p.EdgeSupport)};
                }
                if(status=="passed"&&!RetargetAcceptance.Ready(contactValidation,baked.Diagnostics,baked.LostChannels,true))status="native-passed-context-review";
                object? floorWitness=null;
                if(nativeContactValidation.Penetration>0&&grounding is not null)
                {
                    double frame=nativeContactValidation.WorstFloorFrame;var decodedPose=AnimationService.Evaluate(reopened.Scene,reopened.Clips[0],frame);var surface=new SupportSurface(b.Axes!.Up,grounding.TargetFloor,grounding.TargetEdge);
                    floorWitness=reopened.Scene.Meshes.SelectMany(m=>m.Vertices.Select((v,index)=>new{Frame=frame,Mesh=m.Name,m.Material,Vertex=index,Position=AnimationService.Skin(v,decodedPose),Depth=surface.Penetration(AnimationService.Skin(v,decodedPose)),DefaultVisible=m.DrawCalls.Length==0||m.DrawCalls.Any(d=>d.DefaultVisible)})).OrderByDescending(p=>p.Depth).First();
                }
                receipts.Add(new{Backend=RetargetBackendIdentity.CurrentId,From=from,To=to,Clip=name,Status=status,PhaseCoverage=phaseCoverage,ContactValidation=contactValidation,NativeContactValidation=nativeContactValidation,NativeFloorWitness=floorWitness,EdgeWitness=edgeWitness,ActionFidelity=value.Clip.Retarget!.ActionFidelity,NativeActionFidelity=value.Clip.Retarget.ActionFidelity is null?null:nativeAction,NativeReconstructionPoseAgreement=nativeAction,ActionCoverage=value.Clip.Retarget.ActionFidelity is null?"uncalibrated-pose-rewrite":"measured-action-review",Seconds=watch.Elapsed.TotalSeconds,Native=Path.GetFileName(native),Samples=clip.SampleCount,SourceRate=clip.Rate,ExactNativeKeys=exactNative,NativeKeyComparisons=nativeComparisons,ExactNativeDifferences=nativeDifferences,NativeSampleFrames=times,NativeBoneError=boneError,NativeSkinError=skinError,NativeEvaluatorError=nativeEvaluatorError,Differences=differences,ContactError=value.Contacts.SelectMany(c=>c.Pins).Any()?(float?)maxContact:null,AdaptedSupportSeparation=value.Contacts.SelectMany(c=>c.Pins).Any(p=>p.SoftSupport)?(float?)adaptedSupport:null,GroundPenetration=groundSamples>0?(float?)maxGroundDepth:null,WorstGroundFrame=groundSamples>0?(double?)worstGroundFrame:null,GroundSamples=groundSamples,EdgeSamples=edgeSamples,IntentionalSourceBelowPlane=sourceBelow,TargetBelowPlane=targetBelow,Grounding=value.Grounding,BoneMapping=value.Clip.Retarget!.BoneMapping,Pipeline=value.Clip.Retarget.Pipeline,Reconstruction=value.Clip.Retarget.Pipeline?.Reconstruction,Stages=value.Clip.Retarget.Pipeline?.Stages,Intervals=value.Contacts.Select(c=>new{c.Frame,c.EndFrame,Pins=c.Pins.Select(p=>new{p.Role,p.EdgeSupport,p.Mode,p.EdgeInward,p.SoftSupport,p.Position})}),baked.Diagnostics,baked.LostChannels});
                string contactReport=contactsSuspended?"unavailable":value.Contacts.SelectMany(c=>c.Pins).Any()?$"{maxContact:G6}":"not measured";
                Console.WriteLine($"RESULT: {status}, contact {contactReport}, ground depth {maxGroundDepth:G6} at {worstGroundFrame}, edge samples {edgeSamples}");await Save();
            }
            check(sourceRig==AnimationControls.DeformChecksum(source.Scene)&&targetRig==AnimationControls.DeformChecksum(target.Scene)&&inputs.All(p=>ProjectStore.Hash(p.Path)==p.Hash),$"{from} → {to}: complete animation matrix preserves source bytes and both native deform rigs");
        }
        check(passed>0,"At least one genuine transfer completed; unresolved/unsupported/contradictory results remain separate");Console.WriteLine($"Matrix: {passed} native passes, {unresolved} unresolved, {unsupported} unsupported, {conflicts} proven contradictions, {invalid} invalid inputs, {nativeFailures} native failures. See retarget-matrix.json.");check(nativeFailures==0,"All emitted native curves satisfy the independent native oracle");
        async Task Save()=>await File.WriteAllTextAsync(Path.Combine(output,"retarget-matrix.json"),JsonSerializer.Serialize(new{Backend=RetargetBackendIdentity.CurrentId,Solver=RetargetPipeline.Version,ExplicitFloor=explicitFloor,Ledge=ledge,NativePassed=passed,Conflicts=conflicts,Unresolved=unresolved,Unsupported=unsupported,InvalidInputs=invalid,NativeFailures=nativeFailures,Cases=receipts,Claim="Native data validation only. Pose-transfer drafts retain suspended contact reconstruction and uncalibrated action quality; no Apply-ready retarget or game-test claim."},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
    }
    internal static (bool Exact,int Comparisons,object[] Differences) ExactCurves(AnimationClip expected,AnimationClip actual,Scene scene)
    {
        bool exact=true;int compared=0;var differences=new List<object>();int Bits(float value)=>BitConverter.SingleToInt32Bits(value);
        foreach(var track in expected.Tracks)for(int channel=0;channel<9;channel++)if(track.Channels[channel] is {} curve)
        {
            var decoded=actual.Tracks.SingleOrDefault(t=>t.Bone==track.Bone)?.Channels[channel];
            if(curve.Keys.All(k=>Bits(k.Value)==Bits(curve.Keys[0].Value)&&k.InTangent==0&&k.OutTangent==0))
            {
                float value=decoded?.Sample(0)??scene.Bones.Single(b=>b.Name==track.Bone).Rest.Channels()[channel];
                if(Bits(value)!=Bits(curve.Keys[0].Value)&&differences.Count<32)differences.Add(new{track.Bone,Channel=channel,Kind="constant",Expected=curve.Keys[0].Value,Actual=value,ExpectedBits=Bits(curve.Keys[0].Value),ActualBits=Bits(value)});exact&=Bits(value)==Bits(curve.Keys[0].Value);compared++;continue;
            }
            exact&=decoded is not null&&decoded.Keys.Length==curve.Keys.Length;if(decoded is null||decoded.Keys.Length!=curve.Keys.Length){if(differences.Count<32)differences.Add(new{track.Bone,Channel=channel,Kind="key-count",Expected=curve.Keys.Length,Actual=decoded?.Keys.Length});continue;}
            for(int key=0;key<curve.Keys.Length;key++)
            {
                var a=decoded.Keys[key];var e=curve.Keys[key];compared++;
                bool same=a.Frame==e.Frame&&Bits(a.Value)==Bits(e.Value)&&Bits(a.InTangent)==Bits(e.InTangent)&&Bits(a.OutTangent)==Bits(e.OutTangent);exact&=same;if(!same&&differences.Count<32)differences.Add(new{track.Bone,Channel=channel,Key=key,Expected=e,Actual=a,ExpectedBits=new[]{Bits(e.Value),Bits(e.InTangent),Bits(e.OutTangent)},ActualBits=new[]{Bits(a.Value),Bits(a.InTangent),Bits(a.OutTangent)}});
            }
        }
        return(exact,compared,differences.ToArray());
    }

}
