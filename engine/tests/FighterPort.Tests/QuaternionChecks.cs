using System.Numerics;
using System.Text.Json;
using FighterPort.Core;

internal static class QuaternionChecks
{
    public static async Task RunAsync(FighterProject original,Scene targetScene,string directory,Action<bool,string> check)
    {
        // A second decoded representation, not a claim that a particular external file parser exists.
        var rotation=new QuaternionCurve([new(0,Quaternion.Identity),new(24,Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI/3))]);
        var clip=new AnimationClip(Guid.NewGuid(),"Original quaternion source",25,new(30),false,[new(original.Scene.Bones[0].Name,new Curve?[9]){Rotation=rotation}]);
        var source=original with{Clips=[clip]};var pose=AnimationService.Evaluate(source.Scene,clip,6.5);float angle=6.5f/24*MathF.PI/3;
        check(Vector3.Distance(Vector3.TransformNormal(Vector3.UnitZ,pose.World[0]),new(MathF.Sin(angle),0,MathF.Cos(angle)))<.00001f,"Quaternion source uses spherical fractional evaluation independent of Euler/native CHR0 channels");
        var target=original with{Scene=targetScene,Clips=[]};string before=JsonSerializer.Serialize(source,ProjectStore.Json);var result=RetargetService.Bake(source,clip,AnimationControls.SetUp(source.Scene),target,AnimationControls.SetUp(targetScene));
        check(result.Value is not null&&result.Value.Clip.Tracks.All(t=>t.Rotation is null)&&result.Value.Clip.Rate==new RationalRate(30),"Quaternion/30-fps source bakes to independent target Euler channels across renamed bones and rotated local binds");
        var baked=AnimationService.Evaluate(targetScene,result.Value!.Clip,6.5);check(Vector3.Distance(Vector3.TransformNormal(Vector3.UnitZ,baked.World[0]),new(MathF.Sin(angle),0,MathF.Cos(angle)))<.0001f,"Independent analytical rotation agrees after quaternion-to-target-Euler transfer at a withheld fractional sample");
        check(before==JsonSerializer.Serialize(source,ProjectStore.Json),"Cross-representation transfer preserves authentic source quaternion keys, rest axes and timebase");
        var modelRotation=Matrix4x4.CreateRotationZ(MathF.PI/2);var rotatedScene=source.Scene with{SkeletonRevision=Guid.NewGuid(),Bones=source.Scene.Bones.Select((bone,i)=>i==0?bone with{Rest=bone.Rest with{Rotation=new(0,0,90)}}:bone).ToArray(),Meshes=source.Scene.Meshes.Select(m=>m with{Vertices=m.Vertices.Select(v=>v with{Position=Vector3.Transform(v.Position,modelRotation),Normal=Vector3.TransformNormal(v.Normal,modelRotation)}).ToArray()}).ToArray()};
        var rotatedClip=clip with{Id=Guid.NewGuid(),Tracks=[new(source.Scene.Bones[0].Name,new Curve?[9]){Rotation=new([new(0,Quaternion.CreateFromRotationMatrix(modelRotation)),new(24,Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateRotationY(MathF.PI/3)*modelRotation))])}]};
        var reviewedAxes=AnimationControls.SetUp(source.Scene) with{SkeletonFingerprint=AnimationControls.Fingerprint(rotatedScene),Axes=new(Vector3.UnitY,-Vector3.UnitX,Vector3.UnitZ)};
        var rotatedBake=RetargetService.Bake(source with{Scene=rotatedScene,Clips=[rotatedClip]},rotatedClip,reviewedAxes,target,AnimationControls.SetUp(targetScene)).Value!;var rotatedPose=AnimationService.Evaluate(targetScene,rotatedBake.Clip,6.5);
        check(Vector3.Distance(Vector3.TransformNormal(Vector3.UnitZ,rotatedPose.World[0]),new(MathF.Sin(angle),0,MathF.Cos(angle)))<.0001f,"Reviewed model-coordinate rotation and rotated mesh binds transfer to target body axes without changing either deform rig");
        var resampled=RetargetService.Bake(source,clip,AnimationControls.SetUp(source.Scene),target,AnimationControls.SetUp(targetScene),targetRate:new(60)).Value!;
        var atTime=AnimationService.Evaluate(targetScene,resampled.Clip,13);
        check(resampled.Clip.SampleCount==50&&resampled.Clip.Rate==new RationalRate(60)&&resampled.Clip.DurationSeconds==clip.DurationSeconds&&Vector3.Distance(Vector3.TransformNormal(Vector3.UnitZ,atTime.World[0]),new(MathF.Sin(angle),0,MathF.Cos(angle)))<.0001f,"30-to-60-fps conversion preserves exact duration and evaluates source time rather than copying frame indices");
        check(RetargetService.Bake(source,clip,AnimationControls.SetUp(source.Scene),target,AnimationControls.SetUp(targetScene),targetRate:new(24)).Value is not null&&RetargetService.Bake(source,clip with{SampleCount=24},AnimationControls.SetUp(source.Scene),target,AnimationControls.SetUp(targetScene),targetRate:new(24)).Value is null,"Timebase conversion accepts integral target durations and reports fractional target sample-count conflicts");
        var edit=new EditorDocument(source);edit.Seek(4.25);edit.KeyTransform(0,edit.Evaluate().Local[0] with{Rotation=new(0,17,0)});check(Math.Abs(edit.Evaluate().Local[0].Rotation.Y-17)<.0001f,"FK key editing updates quaternion-backed source rotations rather than shadowing an ignored Euler curve");
        var loopSource=original.Clips[0];var loopBake=RetargetService.Bake(original,loopSource,AnimationControls.SetUp(original.Scene),target,AnimationControls.SetUp(targetScene)).Value!;
        check(loopBake.Clip.SampleCount==loopSource.SampleCount&&loopBake.Clip.LastKeyFrame==loopSource.SampleCount&&loopBake.Clip.LastSampleFrame==loopSource.SampleCount-1&&loopBake.Clip.DurationSeconds==loopSource.DurationSeconds,"Loop transfer keeps the authentic closing key separate from last sample, sample count and duration");
        string path=Path.Combine(directory,"quaternion-source.fighterport");await ProjectStore.SaveAsync(edit.Project,path);var reopened=await ProjectStore.LoadAsync(path);check(reopened.Clips[0].Tracks[0].Rotation!.Keys.Any(k=>k.Frame==4.25)&&reopened.Clips[0].Rate==new RationalRate(30),"Project reopen preserves fractional quaternion keys and the source timebase");
        var held=clip with{Id=Guid.NewGuid(),Tracks=[clip.Tracks[0] with{Rotation=rotation with{Interpolation=QuaternionInterpolation.Step}}]};var heldBake=RetargetService.Bake(source with{Clips=[held]},held,AnimationControls.SetUp(source.Scene),target,AnimationControls.SetUp(targetScene));
        check(heldBake.Value is not null&&heldBake.Value.Clip.Tracks.SelectMany(t=>t.Channels.OfType<Curve>()).All(c=>c.Interpolation==Interpolation.Step),"Held quaternion source poses remain stepped during cross-representation baking");
        try{ProjectStore.Validate(source with{Clips=[clip with{Tracks=[clip.Tracks[0] with{Rotation=new([new(0,default)])}]}]});check(false,"Invalid quaternion should be rejected");}catch(InvalidDataException){check(true,"Invalid zero quaternion channels are rejected before evaluation");}
    }
}
