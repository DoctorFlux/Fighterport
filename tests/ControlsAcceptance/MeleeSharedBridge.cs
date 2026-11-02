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
        foreach(string actor in new[]{"Mario","Fox"})
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
                foreach(bool forward in new[]{true,false})
                {
                    var donor=forward?melee:native;var target=forward?native:melee;string label=actor+(forward?"-melee-to-brawl":"-brawl-to-melee");
                    using(var result=await Task.Run(()=>BrawlRetargeter.Bake(donor.Model,target.Model,donor.Animation,true,GroundContactMode.Auto,RetargetKeyframeMode.SourceChannelTimes)))
                    {
                        string file=Path.Combine(output,label+".chr0");result.Animation.ExportChecked(file);
                        using(var reopened=NodeFactory.FromFile(null,file) as CHR0Node)Check(reopened!=null&&NativeAnimationOptimization.SourceContentKey(reopened)==NativeAnimationOptimization.SourceContentKey(result.Animation),"Cross-game idle retains the finished master through serialization: "+label);
                        Check(result.SourceRig==BrawlRetargeter.SkeletonFingerprint(donor.Model)&&result.TargetRig==BrawlRetargeter.SkeletonFingerprint(target.Model),"Cross-game idle records exact source/target identities: "+label);
                        using(var project=target.Reference.Game=="Melee"?MeleeTargetDocument.Prepare(target,library.Read(),CancellationToken.None):ModelForm.PrepareLibraryTarget(target))
                        {var revision=project.CreateLibraryRevision(donor,result);project.Animations.Add(revision);project.Save(Path.Combine(output,label+".fpstudio"));Check(project.Animations.Contains(revision),"Native target owns an independent source-bound result: "+label);}
                    }
                }
                Check(MeleeNative.RigHash(melee.MeleeModel)==original,"Cross-game preparation never mutates native binds or weights: "+actor);
            }
        }
    }
    static object CallStaticNativeRig(MDL0Node model,CHR0Node clip)=>typeof(ModelForm).GetMethod("NativeEngineRig",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{model,clip});
}
