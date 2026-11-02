using BrawlCrate.Studio;
using BrawlCrate.Studio.Retargeting;
using BrawlCrate.UI;
using BrawlLib.SSBB.ResourceNodes;
using FighterPort.Adapters.Melee;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
internal static partial class Program
{
    static async Task MeleeSharedBridge(string[] args,string output,AnimationLibrary library)
    {
        var disc=args[Array.IndexOf(args,"--bridge-brawl")+1];
        var brawlLibrary=library;string fixtureIndex=Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_BRAWL_LIBRARY");
        if(!string.IsNullOrEmpty(fixtureIndex))
        {brawlLibrary=new AnimationLibrary(fixtureIndex);Check(brawlLibrary.Read().GameLocations.Any(l=>l.Game=="Brawl"&&string.Equals(Path.GetFullPath(l.Path),Path.GetFullPath(disc),StringComparison.OrdinalIgnoreCase)),"Reused test catalogue identifies the exact supplied Brawl disc; native files are verified again on Open");}
        else
        {
            Console.WriteLine("TRACE indexing Brawl fixture");var brawl=await Task.Run(()=>BrawlGameSource.Prepare(new LibraryGameRequest{Game="Brawl",GameVersion="supplied regression disc",Path=disc,ApproveBuild=true},Path.Combine(output,"brawl-index"),CancellationToken.None,null));
            await Task.Run(()=>library.RegisterGame(brawl));
        }
        if(Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_SHARED_WORKFLOW")=="1")
        {await MeleeSharedWorkflow(output,library,brawlLibrary);return;}
        if(Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_SEQUENTIAL_USE")=="1")
        {await MeleeSequentialUse(output,library,brawlLibrary);return;}
        string previousProject=Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_PREVIOUS_PROJECT");
        if(!string.IsNullOrEmpty(previousProject))using(var previous=FighterProject.Open(previousProject))
            Check(previous.Animations.Any(asset=>asset.Retarget?.Algorithm=="bind-world-fk-body-contact-ik-v10"),"The changed contact algorithm reopens an existing v10 Next result without rewriting its historical provenance");
        foreach(string actor in new[]{"Mario","Fox"}.Where(name=>Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_BRIDGE_ACTOR")==null || name==Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_BRIDGE_ACTOR")))
        {
            var items=library.Browse("Wait1");var a=items.First(i=>i.Motion.Game=="Melee"&&i.Model?.Character==actor&&i.Clip.Name=="Wait1");var b=brawlLibrary.Browse("Wait1").First(i=>i.Motion.Game=="Brawl"&&i.Model?.Name=="Fit"+actor+"00"&&i.Clip.Name=="Wait1");
            using(var melee=library.Open(a.Clip.Id,a.Model.Id,CancellationToken.None))
            using(var native=brawlLibrary.Open(b.Clip.Id,b.Model.Id,CancellationToken.None))
            {
                var original=MeleeNative.RigHash(melee.MeleeModel);var rig=BrawlRetargeter.Capture(melee.Model);var projection=MeleeLibraryPreview.Report(melee.Model);var data=melee.Model.NativeEditingBackend.CaptureEvaluation();
                Check(rig.Fingerprint==original&&rig.Rig.NativeEvaluation!=null,"Shared rig retains the exact native identity and evaluation policy: "+actor);
                float[] times={0,.25f,3.381966f,Math.Min(12.75f,melee.Animation.FrameCount-1),melee.Animation.FrameCount-1};
                double maximum=0;
                foreach(float time in times)
                {
                    var expected=new MeleePose(melee.MeleeModel);expected.Sample(melee.MeleePlayback,time);var actual=BrawlRetargeter.Sample(rig,melee.Animation,time);
                    for(int joint=0;joint<actual.World.Length;joint++)maximum=Math.Max(maximum,Vector3.Distance(actual.Point(joint),expected.World[joint].Translation));
                    int vi=0;foreach(var mesh in projection.Meshes)foreach(var vertex in mesh.Vertices)
                    {
                        var point=data.Vertices[vi++];var matrix=FighterPort.NativeEvaluation.EvaluationMath.SkinMatrix(data.Joints,rig.Rig.Joints.Select(j=>j.Parent).ToArray(),actual.World,point.Skin,point.Joints,point.Weights);
                        maximum=Math.Max(maximum,Vector3.Distance(Vector3.Transform(point.Position,matrix),Vector3.Transform(vertex.NativePosition,expected.VertexMatrix(mesh,vertex))));
                    }
                }
                Check(maximum<.0002,"Independent native hierarchy/all-weight surface oracle at withheld fractional samples: "+actor+" max "+maximum.ToString("R"));
                string model=Path.Combine(output,actor+"-native.mdl0"),motion=Path.Combine(output,actor+"-native.chr0");melee.Model.ExportChecked(model);melee.Animation.ExportChecked(motion);
                using(var client=new AnimationControlClient())
                {
                    var samples=await client.Send(new{Operation="native-rig-samples",Model=model,Motion=motion,NativeRig=CallStaticNativeRig(melee.Model,melee.Animation),SampleFrames=times.Select(t=>(double)t).ToArray()});
                    double error=0;
                    for(int sample=0;sample<times.Length;sample++)
                    {
                        var expected=new MeleePose(melee.MeleeModel);expected.Sample(melee.MeleePlayback,times[sample]);int vi=0;
                        foreach(var mesh in projection.Meshes)foreach(var vertex in mesh.Vertices)
                        {var p=samples.NativeSamples[sample].Surface[vi++];error=Math.Max(error,Vector3.Distance(new Vector3(p[0],p[1],p[2]),Vector3.Transform(vertex.NativePosition,expected.VertexMatrix(mesh,vertex))));}
                    }
                    Check(error<.0002,"Owned engine shares the native evaluator and every influence: "+actor+" max "+error.ToString("R"));
                }
                using(var project=MeleeTargetDocument.Prepare(melee,library.Read(),CancellationToken.None))
                {
                    using(var root=NodeFactory.FromFile(null,project.Animations.Single().WorkingPath))
                    {int count=BrawlLibrarySource.Resources(root).Count(r=>r.Item1 is CHR0Node);Check(count==project.NativeTarget.Members.Length&&count>100,"Open as target retains the character animation set and exact native member identities: "+actor+" "+count+" / unavailable "+project.NativeTarget.UnavailableAnimations.Length);}
                    string allPath=Path.Combine(output,actor+"-all-animations.fpstudio");project.Save(allPath);
                    using(var reopened=FighterProject.Open(allPath))using(var savedRoot=NodeFactory.FromFile(null,reopened.Animations.Single().WorkingPath))
                        Check(reopened.NativeTarget.Members.Length==project.NativeTarget.Members.Length&&BrawlLibrarySource.Resources(savedRoot).Count(r=>r.Item1 is CHR0Node)==project.NativeTarget.Members.Length,"Project reopen retains every exact native animation member and association: "+actor);
                }
                foreach(bool forward in new[]{true,false}.Where(direction=>Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_BRIDGE_DIRECTION")==null || (direction?"melee-to-brawl":"brawl-to-melee")==Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_BRIDGE_DIRECTION")))
                {
                    var donor=forward?melee:native;var target=forward?native:melee;string label=actor+(forward?"-melee-to-brawl":"-brawl-to-melee");
                    using(var result=await Task.Run(()=>BrawlRetargeter.Bake(donor.Model,target.Model,donor.Animation,true,GroundContactMode.Auto,RetargetKeyframeMode.SourceChannelTimes)))
                    {
                        string file=Path.Combine(output,label+".chr0");result.Animation.ExportChecked(file);
                        using(var reopened=NodeFactory.FromFile(null,file) as CHR0Node)
                        {
                            Check(reopened!=null&&NativeAnimationOptimization.SourceContentKey(reopened)==NativeAnimationOptimization.SourceContentKey(result.Animation),"Cross-game idle retains the finished master through serialization: "+label);
                            TraceFootMotion(label,donor.Model,donor.Animation,target.Model,result,reopened);
                        }
                        Check(result.SourceRig==BrawlRetargeter.SkeletonFingerprint(donor.Model)&&result.TargetRig==BrawlRetargeter.SkeletonFingerprint(target.Model),"Cross-game idle records exact source/target identities: "+label);
                        using(var project=target.Reference.Game=="Melee"?MeleeTargetDocument.Prepare(target,library.Read(),CancellationToken.None):ModelForm.PrepareLibraryTarget(target))
                        {var revision=project.CreateLibraryRevision(donor,result);project.Animations.Add(revision);project.Save(Path.Combine(output,label+".fpstudio"));Check(project.Animations.Contains(revision),"Native target owns an independent source-bound result: "+label);}
                    }
                }
                Check(MeleeNative.RigHash(melee.MeleeModel)==original,"Cross-game preparation never mutates native binds or weights: "+actor);
            }
        }
        if(Environment.GetEnvironmentVariable("FIGHTERPORT_TEST_BRAWL_PAIR")=="1")
        {
            var items=brawlLibrary.Browse("Wait1");
            var mario=items.First(i=>i.Motion.Game=="Brawl"&&i.Model?.Name=="FitMario00"&&i.Clip.Name=="Wait1");
            var snake=items.First(i=>i.Motion.Game=="Brawl"&&i.Model?.Name=="FitSnake00"&&i.Clip.Name=="Wait1");
            using(var a=brawlLibrary.Open(mario.Clip.Id,mario.Model.Id,CancellationToken.None))
            using(var b=brawlLibrary.Open(snake.Clip.Id,snake.Model.Id,CancellationToken.None))
            foreach(bool forward in new[]{true,false})
            {
                var donor=forward?a:b;var target=forward?b:a;
                string label=forward?"Brawl-Mario-to-Snake":"Brawl-Snake-to-Mario";
                using(var result=await Task.Run(()=>BrawlRetargeter.Bake(donor.Model,target.Model,donor.Animation,true,GroundContactMode.Auto,RetargetKeyframeMode.SourceChannelTimes)))
                {
                    string file=Path.Combine(output,label+".chr0");result.Animation.ExportChecked(file);
                    using(var reopened=NodeFactory.FromFile(null,file) as CHR0Node)
                    {
                        Check(reopened!=null&&NativeAnimationOptimization.SourceContentKey(reopened)==NativeAnimationOptimization.SourceContentKey(result.Animation),"Unrelated Brawl rig pair reopens its exact native idle: "+label);
                        TraceFootMotion(label,donor.Model,donor.Animation,target.Model,result,reopened);
                    }
                }
            }
        }
    }
    static object CallStaticNativeRig(MDL0Node model,CHR0Node clip)=>typeof(ModelForm).GetMethod("NativeEngineRig",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{model,clip});
    static void TraceFootMotion(string label,MDL0Node source,CHR0Node original,MDL0Node target,RetargetResult result,CHR0Node reopened)
    {
        var sourceRig=BrawlRetargeter.Capture(source);var targetRig=BrawlRetargeter.Capture(target);
        int last=original.Loop?original.FrameCount:original.FrameCount-1;
        foreach(string role in new[]{"left.foot","right.foot"})
        {
            var sourceMarker=sourceRig.Rig.Surfaces[role].Marker(BrawlRetargeter.Sample(sourceRig,original,0));
            var targetMarker=targetRig.Rig.Surfaces[role].Marker(result.Contacts.Evaluate(0).Pose);
            Vector3? previousSource=null,previousSolved=null,previousOutput=null;
            double sourceStep=0,solvedStep=0,outputStep=0,representation=0;
            double sourceTravel=0,solvedTravel=0,outputTravel=0;
            double lockedSourceStep=0,lockedSolvedStep=0,lockedOutputStep=0;
            float maximumTime=0;float maximumLock=0,maximumSupport=0;
            Vector3? firstSource=null,firstSolved=null,firstOutput=null;
            int locks=0,supports=0;
            for(int i=0;i<=last*4;i++)
            {
                float time=i/4f;
                Vector3 s=sourceMarker.Evaluate(BrawlRetargeter.Sample(sourceRig,original,time));
                Vector3 p=targetMarker.Evaluate(result.Contacts.Evaluate(time).Pose);
                Vector3 o=targetMarker.Evaluate(BrawlRetargeter.Sample(targetRig,reopened,time));
                firstSource??=s;firstSolved??=p;firstOutput??=o;
                sourceTravel=Math.Max(sourceTravel,Vector3.Distance(firstSource.Value,s));
                solvedTravel=Math.Max(solvedTravel,Vector3.Distance(firstSolved.Value,p));
                outputTravel=Math.Max(outputTravel,Vector3.Distance(firstOutput.Value,o));
                var contact=result.Contacts.At(role,time);
                if(previousSource.HasValue){sourceStep=Math.Max(sourceStep,Vector3.Distance(previousSource.Value,s));solvedStep=Math.Max(solvedStep,Vector3.Distance(previousSolved.Value,p));double step=Vector3.Distance(previousOutput.Value,o);if(step>outputStep){outputStep=step;maximumTime=time;maximumLock=contact.Lock;maximumSupport=contact.Support;}
                    if(contact.Lock>.99f){lockedSourceStep=Math.Max(lockedSourceStep,Vector3.Distance(previousSource.Value,s));lockedSolvedStep=Math.Max(lockedSolvedStep,Vector3.Distance(previousSolved.Value,p));lockedOutputStep=Math.Max(lockedOutputStep,Vector3.Distance(previousOutput.Value,o));}}
                representation=Math.Max(representation,Vector3.Distance(p,o));
                if(contact.Lock>.99f)locks++;if(contact.Support>.99f)supports++;
                previousSource=s;previousSolved=p;previousOutput=o;
            }
            Console.WriteLine("FOOTTRACE "+label+" "+role+" sourceTravel="+sourceTravel.ToString("R")+" solvedTravel="+solvedTravel.ToString("R")+" outputTravel="+outputTravel.ToString("R")+" sourceStep="+sourceStep.ToString("R")+" solvedStep="+solvedStep.ToString("R")+" outputStep="+outputStep.ToString("R")+" maximumTime="+maximumTime.ToString("R")+" maximumLock="+maximumLock.ToString("R")+" maximumSupport="+maximumSupport.ToString("R")+" lockedSourceStep="+lockedSourceStep.ToString("R")+" lockedSolvedStep="+lockedSolvedStep.ToString("R")+" lockedOutputStep="+lockedOutputStep.ToString("R")+" representation="+representation.ToString("R")+" locks="+locks+" supports="+supports);
            if(outputStep>2*sourceStep && maximumSupport>.1f)
                for(int offset=-2;offset<=2;offset++)
                {
                    float time=Math.Max(0,Math.Min(last,maximumTime+offset*.25f));
                    var sourcePose=BrawlRetargeter.Sample(sourceRig,original,time);var solved=result.Contacts.Evaluate(time).Pose;
                    var goal=result.Contacts.At(role,time);
                    Console.WriteLine("FOOTDETAIL "+label+" "+role+" frame="+time.ToString("R")+" source="+sourceMarker.Evaluate(sourcePose)+" solved="+targetMarker.Evaluate(solved)+" lock="+goal.Lock.ToString("R")+" support="+goal.Support.ToString("R")+" anchor="+goal.Anchor+" contactMarker="+(goal.Marker==null?"none":goal.Marker.Evaluate(solved).ToString()));
                }
            if(supports==last*4+1&&sourceTravel<=0.2)
            {
                Check(locks==last*4+1,"A quiet sustained standing foot stays locked through both clip boundaries: "+label+" "+role);
                Check(outputStep<=Math.Max(sourceStep,0.005),"A quiet sustained standing foot has no quarter-frame step larger than its source patch or 0.005 native units: "+label+" "+role);
            }
        }
    }
}
