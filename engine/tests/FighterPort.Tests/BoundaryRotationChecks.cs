using FighterPort.Core;
using System.Numerics;
using System.Text.Json;

internal static class BoundaryRotationChecks
{
    public static void Run(Action<bool,string> check)
    {
        var fixtures=new[]{
            (Join:new Vector3(166,141,175),Reference:new Vector3(-2,-46,-1),Expected:new Vector3(178,226,179)),
            (Join:new Vector3(170,0,0),Reference:new Vector3(-170,0,0),Expected:new Vector3(190,0,0)),
            (Join:new Vector3(-170,0,0),Reference:new Vector3(170,0,0),Expected:new Vector3(-190,0,0)),
            (Join:new Vector3(886,-579,895),Reference:new Vector3(-2,-46,-1),Expected:new Vector3(898,-494,899)),
            (Join:new Vector3(190,90,200),Reference:new Vector3(10,90,20),Expected:new Vector3(190,90,200)),
            (Join:new Vector3(190,270,200),Reference:new Vector3(10,-90,20),Expected:new Vector3(190,270,200)),
            (Join:new Vector3(90,90,90),Reference:Vector3.Zero,Expected:Vector3.Zero)
        };
        int id=0;
        foreach(var f in fixtures)foreach(bool entry in new[]{false,true})foreach(bool nearest in new[]{false,true})
        {
            string bone="deform_"+(++id),root="control_"+(997-id);
            var scene=new Scene("Independent native XYZ endpoint",[new(root,-1,Transform.Identity),new(bone,0,Transform.Identity)],[]);
            Curve?[] Channels(Vector3 value,Vector3 slope)=>[null,null,null,new([new(0,value.X,slope.X,slope.X),new(12,value.X+12*slope.X,slope.X,slope.X)]),new([new(0,value.Y,slope.Y,slope.Y),new(12,value.Y+12*slope.Y,slope.Y,slope.Y)]),new([new(0,value.Z,slope.Z,slope.Z),new(12,value.Z+12*slope.Z,slope.Z,slope.Z)]),null,null,null];
            var controls=Channels(new(720,-360,1080),new(2,3,-4));
            var source=new AnimationClip(Guid.NewGuid(),"Original authored rotations",13,new(60),false,[new(bone,Channels(f.Join,Vector3.Zero)),new(root,controls)]);
            var reference=new AnimationClip(Guid.NewGuid(),"Independent first pose",13,new(120),false,[new(bone,Channels(f.Reference,Vector3.Zero))]);
            string fingerprint=AnimationControls.Fingerprint(scene);
            var setup=new ControlRigSetup(Guid.NewGuid(),fingerprint,[],[]){BodyBone=1};
            var project=new FighterProject(1,Guid.NewGuid(),"Exact authored action",scene,[source,reference]){ControlRigs=[setup]};
            var settings=new BoundaryFitSettings(5,5,TransformComponents.Rotation,[root,bone],[],false,nearest);
            var anchor=new BoundaryAnchor(reference,fingerprint,0);
            string before=JsonSerializer.Serialize(project,ProjectStore.Json);
            var result=BoundaryFitting.Prepare(project,source,entry?anchor:null,entry?null:anchor,settings);
            var actual=result.Clip.Tracks.Single(t=>t.Bone==bone);int at=entry?0:12,join=entry?4:8;
            var expected=nearest?f.Expected:f.Reference;
            check(Vector3.Distance(result.Clip.Sample(scene.Bones[1],at).Rotation,expected)<.00003f,"Joint Euler family and whole-turn endpoint expectation / "+id+" entry="+entry+" nearest="+nearest);
            var expectedMatrix=new Transform(Vector3.One,f.Reference,Vector3.Zero).Matrix;
            var matrix=result.Clip.Sample(scene.Bones[1],at).Matrix;
            check(MatrixError(matrix,expectedMatrix)<.00001f,"Equivalent endpoint retains independent native XYZ orientation / "+id);
            check(Enumerable.Range(0,129).All(i=>{double t=entry?join+(12-join)*i/128d:join*i/128d;return MatrixError(result.Clip.Sample(scene.Bones[1],t).Matrix,source.Sample(scene.Bones[1],t).Matrix)<.00001f;}),"The authored action beyond the entry/exit join remains unchanged / "+id);
            check(Enumerable.Range(3,3).All(c=>{var kept=source.Tracks.Single(t=>t.Bone==bone).Channels[c]!.Keys.Where(k=>entry?k.Frame>=join:k.Frame<=join).ToArray();return kept.SequenceEqual(actual.Channels[c]!.Keys.Where(k=>kept.Any(v=>v.Frame==k.Frame)));}),"Retained authored rotation keys and tangent bits are exact / "+id);
            check(ReferenceEquals(result.Clip.Tracks.Single(t=>t.Bone==root),source.Tracks.Single(t=>t.Bone==root))&&JsonSerializer.Serialize(project,ProjectStore.Json)==before,"Body branch selection preserves every source-owned control and input document / "+id);
            check(actual.Channels.Skip(3).Take(3).All(c=>c!.Keys.Single(k=>k.Frame==at).InTangent==0&&c.Keys.Single(k=>k.Frame==at).OutTangent==0),"Frozen equivalent endpoints retain zero tangent / "+id);
            if(id<=4&&nearest){var midpoint=result.Clip.Sample(scene.Bones[1],entry?2:10).Rotation;check(Vector3.Distance(midpoint,(expected+f.Join)*.5f)<.00003f,"Independent half-window Hermite expectation without a manufactured full turn / "+id);}
        }
        MovingAndSparse(check);
    }
    static void MovingAndSparse(Action<bool,string> check)
    {
        var join=new Vector3(166,141,175);var endpoint=new Vector3(-2,-46,-1);var expected=new Vector3(178,226,179);
        var scene=new Scene("Sparse and moving native XYZ",[new("outer",-1,Transform.Identity),new("body",0,new(Vector3.One,endpoint,Vector3.Zero))],[]);
        Curve?[] sourceChannels=[null,null,null,new([new(0,166),new(4,166),new(4,166),new(8,166),new(8,166),new(12,166)]),new([new(0,141),new(12,141)]),new([new(0,175),new(12,175)]),null,null,null];
        Curve?[] referenceChannels=[null,null,null,new([new(0,-2,2,2),new(12,22,2,2)]),new([new(0,-46,3,3),new(12,-10,3,3)]),new([new(0,-1,5,5),new(12,59,5,5)]),null,null,null];
        var source=new AnimationClip(Guid.NewGuid(),"Original",13,new(60),false,[new("body",sourceChannels)]);var reference=new AnimationClip(Guid.NewGuid(),"Moving",13,new(120),false,[new("body",referenceChannels)]);
        var project=new FighterProject(1,Guid.NewGuid(),"Moving",scene,[source,reference]);string fingerprint=AnimationControls.Fingerprint(scene);
        var options=new BoundaryFitSettings(5,5,TransformComponents.Rotation,["body"],[]);
        foreach(bool entry in new[]{false,true})
        {
            var anchor=new BoundaryAnchor(reference,fingerprint,0,false,.5);
            var fit=BoundaryFitting.Prepare(project,source,entry?anchor:null,entry?null:anchor,options);var track=fit.Clip.Tracks.Single();int at=entry?0:12;
            var slopes=track.Channels.Skip(3).Take(3).Select(c=>c!.Keys.Single(k=>k.Frame==at).InTangent).ToArray();
            check(slopes.SequenceEqual(new[]{2f,-3f,5f}),"Coupled-family moving reference transforms the middle-axis derivative and both timebases / entry="+entry);
            check(Vector3.Distance(fit.Clip.Sample(scene.Bones[1],entry?2:10).Rotation,(join+expected)*.5f+(entry?1:-1)*new Vector3(1,-1.5f,2.5f))<.00003f,"Independent moving-reference Hermite midpoint / entry="+entry);
            var permuted=BoundaryFitting.Prepare(project with{Scene=scene with{Bones=scene.Bones.ToArray()},Clips=[reference,source]},source with{Tracks=source.Tracks.Reverse().ToArray()},entry?anchor:null,entry?null:anchor,options with{Bones=options.Bones.Reverse().ToArray()});
            check(track.Channels.Zip(permuted.Clip.Tracks.Single(t=>t.Bone=="body").Channels).All(p=>p.First is null?p.Second is null:p.First.Keys.SequenceEqual(p.Second!.Keys)),"Track/mask/document storage permutation leaves the shared rotation algorithm identical / entry="+entry);
            var authored=BoundaryFitting.Prepare(project,source,entry?anchor:null,entry?null:anchor,options with{NearestEquivalentAngles=false});
            check(authored.Clip.Tracks.Single().Channels.Skip(3).Take(3).Select(c=>c!.Keys.Single(k=>k.Frame==at).InTangent).SequenceEqual(new[]{2f,3f,5f}),"Disabling equivalent-angle fitting preserves authored moving-reference tangent directions / entry="+entry);
            int unchangedJoin=entry?8:4;check(sourceChannels[3]!.Keys.Where(k=>k.Frame==unchangedJoin).SequenceEqual(track.Channels[3]!.Keys.Where(k=>k.Frame==unchangedJoin)),"Paired authored keys remain exact outside the fitted window / entry="+entry);
            var sparseReference=reference with{Tracks=[new("body",new Curve?[9])]};
            anchor=new(sparseReference,fingerprint,0);fit=BoundaryFitting.Prepare(project,source,entry?anchor:null,entry?null:anchor,options);
            check(Vector3.Distance(fit.Clip.Sample(scene.Bones[1],at).Rotation,expected)<.00003f,"Absent reference axes use authentic bind rotation before joint branch choice / entry="+entry);
            var bindScene=scene with{Bones=[scene.Bones[0],scene.Bones[1] with{Rest=new(Vector3.One,join,Vector3.Zero)}]};
            var absentSource=source with{Tracks=[new("body",new Curve?[9])]};
            var bindAnchor=new BoundaryAnchor(reference,AnimationControls.Fingerprint(bindScene),0);
            var bindFit=BoundaryFitting.Prepare(project with{Scene=bindScene},absentSource,entry?bindAnchor:null,entry?null:bindAnchor,options);
            check(Vector3.Distance(bindFit.Clip.Sample(bindScene.Bones[1],at).Rotation,expected)<.00003f&&MatrixError(bindFit.Clip.Sample(bindScene.Bones[1],entry?12:0).Matrix,bindScene.Bones[1].Rest.Matrix)<.00001f,"Absent source axes retain authentic bind fallback beyond the fitted entry/exit / entry="+entry);
        }
    }
    static float MatrixError(Matrix4x4 a,Matrix4x4 b)=>new[]{a.M11-b.M11,a.M12-b.M12,a.M13-b.M13,a.M21-b.M21,a.M22-b.M22,a.M23-b.M23,a.M31-b.M31,a.M32-b.M32,a.M33-b.M33}.Max(Math.Abs);
}
