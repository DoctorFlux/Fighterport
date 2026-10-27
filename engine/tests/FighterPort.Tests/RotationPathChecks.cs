using FighterPort.Core;
using FighterPort.Adapters.Brawl;
using System.Numerics;
using System.Text.Json;

internal static class RotationPathChecks
{
    public static void Run(string output,Action<bool,string> check)
    {
        // Independent orientation expectations: native XYZ matrices and BCL
        // spherical interpolation. Production integrates analytic body angular speed.
        var cases=new[]{
            (new Vector3(170,0,0),new Vector3(-170,0,0)),
            (new Vector3(94.51721f,-72.76334f,-168.3286f),new Vector3(-.078132f,-.081514f,-.5721804f)),
            (new Vector3(269.0419f,-83.35701f,-330.3212f),new Vector3(-.078132f,-.081514f,-.5721804f)),
            (new Vector3(886,-579,895),new Vector3(-2,-46,-1)),
            (new Vector3(190,90,200),new Vector3(10,90,20)),
            (new Vector3(190,270,200),new Vector3(10,-90,20)),
            (new Vector3(90,90,90),Vector3.Zero),
            (new Vector3(30,89.999f,10),new Vector3(30,90.001f,10))
        };
        int id=0;var observations=new List<object>();
        foreach(var (join,reference) in cases)foreach(bool entry in new[]{true,false})
        {
            id++;string name="joint_"+id;const int span=12,last=36;
            var rest=new Transform(new(-1,0,3),reference,new(2,-3,4));
            var scene=new Scene("Rotation-only independent fixture",[new("root",-1,Transform.Identity),new(name,0,rest)],[]);
            Curve?[] Channels(Vector3 v)=>[null,null,null,new([new(0,v.X),new(12,v.X),new(24,v.X),new(36,v.X)]),new([new(0,v.Y),new(36,v.Y)]),new([new(0,v.Z),new(36,v.Z)]),null,null,null];
            var source=new AnimationClip(Guid.NewGuid(),"Arbitrary",37,new(60),false,[new(name,Channels(join))]);
            var donor=new AnimationClip(Guid.NewGuid(),"Pose",37,new(60),false,[]);
            var project=new FighterProject(1,Guid.NewGuid(),"Native",scene,[source,donor]);
            var anchor=new BoundaryAnchor(donor,AnimationControls.Fingerprint(scene),0);
            var settings=new BoundaryFitSettings(13,13,TransformComponents.Rotation,[name],[]){OptimizeRotationPaths=true};
            string before=JsonSerializer.Serialize(project,ProjectStore.Json);
            var fit=BoundaryFitting.Prepare(project,source,entry?anchor:null,entry?null:anchor,settings);
            var legacy=BoundaryFitting.Prepare(project,source,entry?anchor:null,entry?null:anchor,settings with{OptimizeRotationPaths=false});
            double maximum=0,arc=0,oldArc=0;Quaternion previous=default,oldPrevious=default;
            for(int i=0;i<=span*32;i++)
            {
                float u=i/(span*32f),s=u*u*(3-2*u);double frame=(entry?0:last-span)+span*u;
                var a=Orientation(entry?reference:join);var b=Orientation(entry?join:reference);
                var expected=Quaternion.Slerp(a,b,s);var actual=Orientation(fit.Clip.Sample(scene.Bones[1],frame).Rotation);
                maximum=Math.Max(maximum,Degrees(expected,actual));var old=Orientation(legacy.Clip.Sample(scene.Bones[1],frame).Rotation);if(i>0){arc+=Degrees(previous,actual);oldArc+=Degrees(oldPrevious,old);}previous=actual;oldPrevious=old;
            }
            if(id<=2)check(maximum<.01,"Independent shortest-axis smoothstep oracle at 1/32 frame / "+id);
            check(arc<=oldArc+.01,"Dense independent native matrices show no added rotation travel / "+id+" old="+oldArc+" new="+arc);
            check(Degrees(Orientation(fit.Clip.Sample(scene.Bones[1],entry?0:last).Rotation),Orientation(reference))<.01,"Exact reference endpoint orientation / "+id);
            check(fit.Clip.Tracks[0].Channels.Skip(3).Take(3).All(c=>c!.Keys.Length<=6),"Native curves need no dense key bake / "+id);
            check(fit.Receipt.RotationInterpolation=="native-orientation-path-1"&&fit.Receipt.RotationPathDegrees<=fit.Receipt.LegacyRotationPathDegrees+.0001,"Versioned measured native rotation-path receipt / "+id);
            check(fit.Clip.Tracks.Single().Channels.Skip(3).Take(3).All(c=>c!.Keys.All(k=>k.Frame==Math.Truncate(k.Frame))),"All fitted keys have native integral indices / "+id);
            check(Enumerable.Range(0,9).Where(c=>c<3||c>5).All(c=>ReferenceEquals(source.Tracks[0].Channels[c],fit.Clip.Tracks[0].Channels[c]))&&JsonSerializer.Serialize(project,ProjectStore.Json)==before,"Authentic binds, scales, translations and immutable input remain exact / "+id);
            var renamedScene=scene with{Bones=[scene.Bones[0],scene.Bones[1] with{Name="unfamiliar"}]};
            var renamedSource=source with{Tracks=[source.Tracks[0] with{Bone="unfamiliar"}]};
            var renamed=BoundaryFitting.Prepare(project with{Scene=renamedScene,Clips=[donor,renamedSource]},renamedSource,entry?anchor with{SkeletonFingerprint=AnimationControls.Fingerprint(renamedScene)}:null,entry?null:anchor with{SkeletonFingerprint=AnimationControls.Fingerprint(renamedScene)},settings with{Bones=["unfamiliar"]});
            check(fit.Clip.Tracks[0].Channels.Zip(renamed.Clip.Tracks[0].Channels).All(p=>p.First is null?p.Second is null:p.First.Keys.SequenceEqual(p.Second!.Keys)),"Renamed bone and storage permutation preserve numerical output / "+id);
            observations.Add(new{id,Maximum=maximum,Arc=arc,OldArc=oldArc,Measured=fit.Receipt.RotationPathDegrees});
        }
        // Screenshot values independently verify the coupled family; raw Euler
        // distance is not an orientation error or a reason to rotate a root.
        check(Degrees(Orientation(new(-.078132f,-.081514f,-.5721804f)),Orientation(new(179.9219f,-179.9185f,-180.5722f)))<.01,"User Fall screenshot triplets are the same orientation within rounding");
        MovingAndSafety(check);
        File.WriteAllText(Path.Combine(output,"rotation-path-observations.json"),JsonSerializer.Serialize(observations,new JsonSerializerOptions{WriteIndented=true}));
    }
    static void MovingAndSafety(Action<bool,string> check)
    {
        var scene=new Scene("Generic moving references",[new("outer",-1,Transform.Identity),new("deform",0,Transform.Identity)],[]);
        var root=new BoneTrack("outer",[null,null,null,new([new(0,20),new(20,380)]),null,null,new([new(0,9),new(20,99)]),null,null]);
        var channels=new Curve?[9];channels[3]=new([new(0,166,2,2),new(5,176,2,2),new(5,176,3,3),new(15,196,4,4),new(15,196,2,2),new(20,206,2,2)]);channels[4]=new([new(0,141),new(20,141)]);channels[5]=new([new(0,175),new(20,175)]);
        var source=new AnimationClip(Guid.NewGuid(),"Original",21,new(60),false,[root,new("deform",channels)]);
        var referenceChannels=new Curve?[9];referenceChannels[3]=new([new(0,-2,2,2),new(20,38,2,2)]);referenceChannels[4]=new([new(0,-46,3,3),new(20,14,3,3)]);referenceChannels[5]=new([new(0,-1,5,5),new(20,99,5,5)]);
        var reference=source with{Rate=new(120),Tracks=[new("deform",referenceChannels)]};
        string fingerprint=AnimationControls.Fingerprint(scene);var anchor=new BoundaryAnchor(reference,fingerprint,0,false,.5);
        var project=new FighterProject(1,Guid.NewGuid(),"Generic",scene,[source,reference]){ControlRigs=[new(Guid.NewGuid(),fingerprint,[],[]){BodyBone=1}]};
        var settings=new BoundaryFitSettings(6,6,TransformComponents.Rotation,["outer","deform"],[]){OptimizeRotationPaths=true};
        string before=JsonSerializer.Serialize(project,ProjectStore.Json);
        foreach(bool entry in new[]{true,false})
        {
            var fit=BoundaryFitting.Prepare(project,source,entry?anchor:null,entry?null:anchor,settings);int end=entry?0:20,join=entry?5:15;
            var track=fit.Clip.Tracks.Single(t=>t.Bone=="deform");
            check(channels.Skip(3).Take(3).Zip(track.Channels.Skip(3).Take(3)).All(p=>{var kept=p.First!.Keys.Where(k=>entry?k.Frame>=join:k.Frame<=join).ToArray();return kept.SequenceEqual(p.Second!.Keys.Where(k=>kept.Any(v=>v.Frame==k.Frame)));}),"Moving/paired native joins and every original retained tangent are exact / "+entry);
            check(ReferenceEquals(fit.Clip.Tracks.Single(t=>t.Bone=="outer"),root),"Reviewed protected parent S/R/T stay source-authored / "+entry);
            var point=fit.Clip.Sample(scene.Bones[1],end).Rotation;
            const double h=.002;
            Vector3 forward=point+new Vector3(BoundaryFitting.Derivative(track.Channels[3],end,entry),BoundaryFitting.Derivative(track.Channels[4],end,entry),BoundaryFitting.Derivative(track.Channels[5],end,entry))*(float)h;
            Vector3 expected=new(-2,-46,-1);expected+=new Vector3(2,3,5)*(float)h;
            check(Degrees(Orientation(forward),Orientation(expected))<.001,"Moving reference angular velocity respects alternate families and both timebases / "+entry);
            check(track.Channels.Skip(3).Take(3).All(c=>c!.Keys.GroupBy(k=>k.Frame).All(g=>g.Count()<=2)),"Native paired-key representation remains bounded / "+entry);
        }
        bool Reject(BoundaryFitSettings options,BoundaryAnchor pose)
        {try{BoundaryFitting.Prepare(project,source,pose,null,options);return false;}catch(Exception e)when(e is InvalidDataException or NotSupportedException){return true;}}
        check(Reject(settings with{NearestEquivalentAngles=false},anchor),"Contradictory smooth/authored-turn options refuse before publication");
        var invalid=reference with{Tracks=[new("deform",new Curve?[]{null,null,null,new([new(0,float.NaN)]),null,null,null,null,null})]};
        check(Reject(settings,anchor with{Clip=invalid}),"Nonfinite reference rotation refuses without editing source");
        var large=source with{SampleCount=100000};
        bool budget=false;try{BoundaryFitting.Prepare(project,large,anchor,null,settings with{EntrySamples=5000});}catch(NotSupportedException e){budget=e.Message.Contains("budget");}
        check(budget&&JsonSerializer.Serialize(project,ProjectStore.Json)==before,"Excessive offline review refuses within its budget and preserves every input");
        var turns=reference with{Tracks=[new("deform",new Curve?[]{null,null,null,new([new(0,380)]),null,null,null,null,null})]};
        var legacy=BoundaryFitting.Prepare(project,source,anchor with{Clip=turns,Frozen=true},null,settings with{OptimizeRotationPaths=false,NearestEquivalentAngles=false});
        check(legacy.Clip.Sample(scene.Bones[1],0).Rotation.X==380,"Explicit authored-turn mode keeps full reference turns");
    }
    static Quaternion Orientation(Vector3 e)=>Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationX(e.X*MathF.PI/180)*Matrix4x4.CreateRotationY(e.Y*MathF.PI/180)*Matrix4x4.CreateRotationZ(e.Z*MathF.PI/180)));
    public static void Native(string sourceFile,string referenceFile,string output,Action<bool,string> check)
    {
        FighterProject Read(string path)=>JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("Project").Deserialize<FighterProject>(ProjectStore.Json)!;
        var project=Read(sourceFile);var source=project.Clips.Single();var reference=Read(referenceFile).Clips.Single();
        project=project with{ControlRigs=[BrawlControlProfiles.SetUp(project)]};
        var anchor=new BoundaryAnchor(reference,AnimationControls.Fingerprint(project.Scene),0);
        var options=new BoundaryFitSettings(5,10,TransformComponents.All,project.Scene.Bones.Select(b=>b.Name).ToArray(),[]){MatchReferenceControls=true,OptimizeRotationPaths=true};
        var fit=BoundaryFitting.Prepare(project,source,anchor,anchor,options);
        var legacy=BoundaryFitting.Prepare(project,source,anchor,anchor,options with{OptimizeRotationPaths=false});
        var rows=new List<object>();int improved=0;
        foreach(var bone in project.Scene.Bones)
        {
            (double Arc,double Peak) Measure(AnimationClip clip,int from,int to)
            {
                var previous=Orientation(clip.Sample(bone,from).Rotation);double sum=0,peak=0;
                for(int i=1;i<=(to-from)*128;i++){var q=Orientation(clip.Sample(bone,from+i/128d).Rotation);double angle=Degrees(previous,q);sum+=angle;peak=Math.Max(peak,angle*128);previous=q;}return(sum,peak);
            }
            var oe=Measure(legacy.Clip,0,4);var ne=Measure(fit.Clip,0,4);var ox=Measure(legacy.Clip,34,43);var nx=Measure(fit.Clip,34,43);
            double oldEntry=oe.Arc,newEntry=ne.Arc,oldExit=ox.Arc,newExit=nx.Arc;
            check(newEntry<=oldEntry+.03&&newExit<=oldExit+.03,"Authentic dense native orientation travel never increases / "+bone.Name);
            if(newEntry<oldEntry-.05||newExit<oldExit-.05)improved++;
            check(ne.Peak<=oe.Peak+.05&&nx.Peak<=ox.Peak+.05,"Authentic dense native peak angular speed never increases / "+bone.Name);
            check(Enumerable.Range(0,121).All(i=>Degrees(Orientation(source.Sample(bone,4+i*.25).Rotation),Orientation(fit.Clip.Sample(bone,4+i*.25).Rotation))<.01),"Authentic original middle orientations remain exact / "+bone.Name);
            rows.Add(new{bone.Name,OldEntry=oldEntry,NewEntry=newEntry,OldExit=oldExit,NewExit=newExit,OldEntryPeak=oe.Peak,NewEntryPeak=ne.Peak,OldExitPeak=ox.Peak,NewExitPeak=nx.Peak,Endpoint=fit.Clip.Sample(bone,43).Rotation,Reference=reference.Sample(bone,0).Rotation});
        }
        check(improved>0,"Authentic boundary paths improve without changing endpoints or the action middle");
        File.WriteAllText(Path.Combine(output,"native-rotation-paths.json"),JsonSerializer.Serialize(new{Improved=improved,Rows=rows},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
    }
    static double Degrees(Quaternion a,Quaternion b)
    {
        var q=Quaternion.Normalize(Quaternion.Inverse(a)*b);
        return 2*Math.Atan2(Math.Sqrt((double)q.X*q.X+(double)q.Y*q.Y+(double)q.Z*q.Z),Math.Abs(q.W))*180/Math.PI;
    }
}
