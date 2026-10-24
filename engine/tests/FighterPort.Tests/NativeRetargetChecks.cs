using System.Numerics;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class NativeRetargetChecks
{
    public static async Task RunAsync(BrawlAdapter adapter,FighterProject source,FighterProject target,string output,Action<bool,string> check,Action<float,float,float,string> near,bool ground=false)
    {
        var a=BrawlControlProfiles.SetUp(source);var b=BrawlControlProfiles.SetUp(target);var clip=source.Clips.Single(c=>c.Source?.NativeName=="Wait1");var template=target.Clips.Single(c=>c.Source?.NativeName=="Wait1");target=target with{Clips=[template],ControlRigs=[b]};
        string sourceHash=AnimationControls.DeformChecksum(source.Scene),targetHash=AnimationControls.DeformChecksum(target.Scene);var sourceFiles=new[]{clip.Source!.Resource,clip.Source.Model!,template.Source!.Resource,template.Source.Model!}.DistinctBy(r=>r.Path).ToArray();
        var baked=RetargetService.Bake(source,clip,a,target,b,ground:ground?new():null);check(baked.Value is not null,"Cross-rig transfer uses measured target proportions and reviewed native semantic profiles: "+string.Join("; ",baked.Diagnostics.Where(d=>d.Error).Select(d=>d.Message)));var value=baked.Value!;
        if(ground)check(value.Grounding?.DetectedIntervals>0&&value.Contacts.SelectMany(c=>c.Pins).All(p=>p.SurfacePoints.Length>0),"Native unannotated idle detects real skin-surface supports and produces target-side constraints");
        check(value.Clip.Source?.Resource==clip.Source.Resource&&value.Clip.NativeTarget?.Resource==template.Source.Resource,"Retargeted native donor identity remains separate from the target CHR0 output basis");
        var project=RetargetService.Apply(target,value);await ProjectStore.SaveAsync(project,Path.Combine(output,"mario-to-snake.fighterport"));string native=Path.Combine(output,"mario-to-snake-"+Guid.NewGuid().ToString("N")+".chr0");await adapter.ExportAsync(project,value.Clip,native);
        double[] times=[0,.125,7.75,12,23.375,value.Clip.LastSampleFrame];var fresh=await adapter.RunAsync(new("import",template.Source.Model!.Path,native,null,null,[],times));var opened=fresh.Project!;
        check(opened.Clips[0].SampleCount==value.Clip.SampleCount&&opened.Clips[0].Loop==value.Clip.Loop&&opened.Clips[0].Rate==value.Clip.Rate,"New target-native CHR0 retains the retargeted sample count, timing and loop flag");
        float bones=0,geometry=0;string worst="";var vertices=opened.Scene.Meshes.SelectMany(m=>m.Vertices).ToArray();for(int f=0;f<times.Length;f++)
        {
            var expected=AnimationService.Evaluate(target.Scene,value.Clip,times[f]);var actual=AnimationService.Evaluate(opened.Scene,opened.Clips[0],times[f]);
            for(int j=0;j<opened.Scene.Bones.Length;j++){var nativePoint=fresh.ExpectedBonePositions![f][j];float error=Vector3.Distance(expected.World[j].Translation,new(nativePoint[0],nativePoint[1],nativePoint[2]));if(error>bones){bones=error;worst=$"{opened.Scene.Bones[j].Name}, frame {times[f]}";}}
            for(int v=0;v<vertices.Length;v++){var nativePoint=fresh.ExpectedVertices![f][v];geometry=Math.Max(geometry,Vector3.Distance(AnimationService.Skin(vertices[v],actual),new(nativePoint[0],nativePoint[1],nativePoint[2])));}
        }
        if(bones>.005f){Console.WriteLine("Worst native retarget pose: "+worst);foreach(double time in times){var x=AnimationService.Evaluate(target.Scene,value.Clip,time);var y=AnimationService.Evaluate(opened.Scene,opened.Clips[0],time);var different=Enumerable.Range(0,x.Local.Length).Select(i=>(Name:target.Scene.Bones[i].Name,Error:x.Local[i].Channels().Zip(y.Local[i].Channels()).Max(p=>Math.Abs(p.First-p.Second)))).OrderByDescending(p=>p.Error).Take(4);Console.WriteLine(time+": "+string.Join("; ",different.Select(p=>p.Name+" local "+p.Error)));}}
        near(bones,0,.005f,"Retargeted target poses agree with independently reopened native matrices at withheld fractional times");near(geometry,0,.0003f,"Retargeted native geometry independently agrees with the owned skinned evaluator");
        if(ground)
        {
            float nativeContactError=0;int compared=0;
            for(int f=0;f<times.Length;f++)foreach(var pin in value.Contacts.Where(c=>c.Contains(times[f])).SelectMany(c=>c.Pins))
            {
                var indices=pin.SurfacePoints.Select(p=>Array.FindIndex(vertices,v=>Vector3.Distance(v.Position,p.Position)<.00001f&&v.Joints==p.Joints&&v.Weights==p.Weights)).ToArray();check(indices.All(i=>i>=0),"Native support corresponds to actual independently decoded target vertices");
                var at=indices.Aggregate(Vector3.Zero,(p,i)=>{var point=fresh.ExpectedVertices![f][i];return p+new Vector3(point[0],point[1],point[2]);})/indices.Length;
                var delta=at-pin.Position;if(pin.PlaneNormal is {} n)delta=n*Vector3.Dot(delta,n);nativeContactError=Math.Max(nativeContactError,delta.Length());compared++;
            }
            check(compared>0&&nativeContactError<RigCharacterization.Describe(target.Scene,b).LegLength*.002f,$"Fresh CHR0 native skin samples retain target ground anchors (max {nativeContactError:G6})");
        }
        check(sourceHash==AnimationControls.DeformChecksum(source.Scene)&&targetHash==AnimationControls.DeformChecksum(opened.Scene)&&sourceFiles.All(r=>ProjectStore.Hash(r.Path)==r.Sha256),"Cross-rig native export retains both original skeletons/weights and all original source/target hashes");
        await ProjectStore.SaveAsync(project,Path.Combine(output,"mario-to-snake.fighterport"));
        var procedural=ProceduralFixture.Create();var quaternionClip=new AnimationClip(Guid.NewGuid(),"Quaternion donor 30 fps",25,new(30),false,[new(procedural.Scene.Bones[0].Name,new Curve?[9]){Rotation=new([new(0,Quaternion.Identity),new(24,Quaternion.CreateFromAxisAngle(Vector3.UnitY,.6f))])}]);
        var quaternionSource=procedural with{Clips=[quaternionClip]};var converted=RetargetService.Bake(quaternionSource,quaternionClip,AnimationControls.SetUp(procedural.Scene),target,b).Value!;
        check(converted.Clip.SampleCount==50&&converted.Clip.Rate==new RationalRate(60)&&converted.Source.Clip.Rate==new RationalRate(30),"Quaternion source on original model converts to native target 60-fps Euler channels while retaining authentic source timing");
        string convertedPath=Path.Combine(output,"quaternion-to-native-"+Guid.NewGuid().ToString("N")+".chr0");await adapter.ExportAsync(RetargetService.Apply(target,converted),converted.Clip,convertedPath);
        double[] conversionTimes=[0,.125,13,23.375,48,49];var convertedFresh=await adapter.RunAsync(new("import",template.Source.Model.Path,convertedPath,null,null,[],conversionTimes));float convertedError=0;
        for(int f=0;f<conversionTimes.Length;f++){var expected=AnimationService.Evaluate(target.Scene,converted.Clip,conversionTimes[f]);for(int j=0;j<target.Scene.Bones.Length;j++){var point=convertedFresh.ExpectedBonePositions![f][j];convertedError=Math.Max(convertedError,Vector3.Distance(expected.World[j].Translation,new(point[0],point[1],point[2])));}}
        near(convertedError,0,.005f,"Cross-representation/timebase target output survives genuine native CHR0 export and independent fractional pose sampling");
        check(convertedFresh.Project!.Clips[0].SampleCount==50&&convertedFresh.Project.Clips[0].Rate==new RationalRate(60)&&sourceFiles.All(r=>ProjectStore.Hash(r.Path)==r.Sha256),"Cross-format native reopening preserves output duration and original private source hashes");
        var loopClip=procedural.Clips[0];var looped=RetargetService.Bake(procedural,loopClip,AnimationControls.SetUp(procedural.Scene),target,b).Value!;string loopPath=Path.Combine(output,"loop-target-"+Guid.NewGuid().ToString("N")+".chr0");await adapter.ExportAsync(RetargetService.Apply(target,looped),looped.Clip,loopPath);
        double[] loopTimes=[0,.125,58.75,59,59.25,59.75,60];var loopFresh=await adapter.RunAsync(new("import",template.Source.Model.Path,loopPath,null,null,[],loopTimes));float loopError=0;
        for(int f=0;f<loopTimes.Length;f++){var expected=AnimationService.Evaluate(target.Scene,looped.Clip,loopTimes[f]);for(int j=0;j<target.Scene.Bones.Length;j++){var point=loopFresh.ExpectedBonePositions![f][j];loopError=Math.Max(loopError,Vector3.Distance(expected.World[j].Translation,new(point[0],point[1],point[2])));}}
        near(loopError,0,.005f,"Loop closing-key and final fractional poses survive independent native target sampling");
        check(loopFresh.Project!.Clips[0].Loop&&loopFresh.Project.Clips[0].SampleCount==60&&loopFresh.Project.Clips[0].LastKeyFrame==60&&loopFresh.Project.Clips[0].LastSampleFrame==59,"Native loop reopening retains 60 displayed samples and a separate duration-time closing key");
    }
}
