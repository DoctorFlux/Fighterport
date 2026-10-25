using FighterPort.Core;
using System.Numerics;
using System.Text.Json;

internal static class BoundaryReferenceControlChecks
{
    public static void Run(Action<bool,string> check)
    {
        foreach(bool entry in new[]{false,true})foreach(bool renamed in new[]{false,true})
        {
            string[] names=renamed?["frame","travel","facing","pitch","deform"]:["TopN","TransN","YRotN","XRotN","HipN"];
            var scene=new Scene("Independent reviewed reference controls",names.Select((n,i)=>new Bone(n,i-1,i==3?new(Vector3.One,Vector3.Zero,new(0,12,0)):Transform.Identity)).ToArray(),[]);
            Curve?[] Channels(float height,Vector3 angles)=>[new([new(0,1),new(12,1)]),null,null,new([new(0,angles.X),new(12,angles.X)]),new([new(0,angles.Y),new(12,angles.Y)]),new([new(0,angles.Z),new(12,angles.Z)]),new([new(0,0),new(12,0)]),new([new(0,height),new(12,height)]),new([new(0,0,1,1),new(12,12,1,1)])];
            var source=new AnimationClip(Guid.NewGuid(),"Authored action",13,new(60),false,names.Select((n,i)=>new BoneTrack(n,Channels(i==3?13:0,i==3?new(166,141,175):Vector3.Zero))).ToArray());
            var reference=source with{Id=Guid.NewGuid(),Name="First reference",Tracks=names.Select((n,i)=>new BoneTrack(n,Channels(i==3?12:0,i==3?new(-2,-46,-1):Vector3.Zero))).ToArray()};
            string fingerprint=AnimationControls.Fingerprint(scene);
            string[] roles=["SceneRoot","RootMotion","RootOrientation","BodyOrientation","Hips"];
            var setup=new ControlRigSetup(Guid.NewGuid(),fingerprint,[],[]){BodyBone=4,BoneRoles=roles.Select((r,i)=>new RigMapping(r,i,"Independent ancestor hierarchy and exact reviewed bind axes",true)).ToArray()};
            var project=new FighterProject(1,Guid.NewGuid(),"Reviewed controls",scene,[source,reference]){ControlRigs=[setup]};
            var settings=new BoundaryFitSettings(5,5,TransformComponents.All,names,[]){MatchReferenceControls=true};
            var anchor=new BoundaryAnchor(reference,fingerprint,0);
            string before=JsonSerializer.Serialize(project,ProjectStore.Json);
            var fit=BoundaryFitting.Prepare(project,source,entry?anchor:null,entry?null:anchor,settings);int endpoint=entry?0:12,join=entry?4:8;
            check(ReferenceEquals(fit.Clip.Tracks.Single(t=>t.Bone==names[1]),source.Tracks[1]),"Reviewed motion-position S/R/T and tangent objects stay exact / "+entry+" renamed="+renamed);
            check(fit.Clip.Sample(scene.Bones[1],12).Translation.Z==12,"Nonzero authored travel is retained even at the first-reference exit");
            check(Math.Abs(fit.Clip.Sample(scene.Bones[3],endpoint).Translation.Y-12)<.00003f&&Vector3.Distance(fit.Clip.Sample(scene.Bones[3],endpoint).Rotation,new(178,226,179))<.00003f,"Reviewed orientation control fits reference local height and nearest coupled rotation at either boundary");
            check(Math.Abs(fit.Clip.Sample(scene.Bones[0],endpoint).Translation.Z)<.00003f,"Explicit reviewed control policy fits scene-root translation despite ordinary root-translation preservation");
            var expected=AnimationService.Evaluate(scene,reference,0,new Dictionary<int,Transform>{{1,source.Sample(scene.Bones[1],endpoint)}});
            var actual=AnimationService.Evaluate(scene,fit.Clip,endpoint);
            check(actual.World.Select((m,i)=>Vector3.Distance(m.Translation,expected.World[i].Translation)<.0002f).All(v=>v),"Independent reference world placement keeps only the motion-position control source-authored");
            check(Enumerable.Range(0,129).All(i=>{double at=entry?join+(12-join)*i/128d:join*i/128d;return scene.Bones.All(b=>Vector3.Distance(source.Sample(b,at).Translation,fit.Clip.Sample(b,at).Translation)<.00003f&&Vector3.Distance(source.Sample(b,at).Rotation,fit.Clip.Sample(b,at).Rotation)<.0001f);}),"Original action and every control outside the boundary window remain unchanged");
            check(fit.Receipt.MatchedReferenceControls.Order().SequenceEqual(new[]{names[0],names[2],names[3]}.Order())&&fit.Receipt.ReferenceRootPolicy==BoundaryReferenceControls.Policy,"Receipt identifies fitted controls and the retained motion-position placement policy");
            var legacy=BoundaryFitting.Prepare(project,source,entry?anchor:null,entry?null:anchor,settings with{MatchReferenceControls=false});
            check(source.Tracks.Take(4).All(t=>ReferenceEquals(t,legacy.Clip.Tracks.Single(v=>v.Bone==t.Bone))),"Disabled option preserves the previous all-control policy exactly");
            bool Reject(FighterProject p,BoundaryFitSettings o){try{BoundaryFitting.Prepare(p,source,entry?anchor:null,entry?null:anchor,o);return false;}catch(Exception e)when(e is InvalidDataException or NotSupportedException){return true;}}
            check(Reject(project with{ControlRigs=[setup with{BoneRoles=setup.BoneRoles.Where(r=>r.SemanticRole!="RootMotion").ToArray()}]},settings),"Missing motion-position review rejects even for recognizable native names");
            check(Reject(project with{ControlRigs=[setup with{BoneRoles=[..setup.BoneRoles,new("RootMotion",2,"Ambiguous",true)]}]},settings),"Ambiguous control roles reject without selecting a substitute");
            check(Reject(project,settings with{Bones=[names[4]]}),"Incomplete reference-control mask rejects");
            check(Reject(project,settings with{PreserveContacts=true,ContactBones=[names[4]]}),"Preserved support dependencies block ordinary control fitting without explicit recovery");
            var permuted=BoundaryFitting.Prepare(project with{ControlRigs=[setup with{BoneRoles=setup.BoneRoles.Reverse().ToArray()}]},source with{Tracks=source.Tracks.Reverse().ToArray()},entry?anchor:null,entry?null:anchor,settings with{Bones=names.Reverse().ToArray()});
            check(scene.Bones.All(b=>Vector3.Distance(permuted.Clip.Sample(b,endpoint).Translation,fit.Clip.Sample(b,endpoint).Translation)<.00003f),"Role, track and mask order do not select a different control policy");
            check(JsonSerializer.Serialize(project,ProjectStore.Json)==before,"Control fitting preserves the complete original document");
        }
    }
}
