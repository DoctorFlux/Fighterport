using FighterPort.Core;
using System.Numerics;
using System.Text.Json;

internal static class BoundaryExistingTrackChecks
{
    public static async Task RunAsync(string dir,Action<bool,string> check)
    {
        foreach(bool renamed in new[]{false,true})
        {
            string[] names=renamed?["a","z","q","j","s","k"]:["global","body","finger","unweighted_tail","empty","reference_only"];
            var scene=new Scene("Independent existing-track scope",names.Select((n,i)=>new Bone(n,i==0?-1:0,i==1?new(Vector3.One,new(0,7,0),new(2,3,4)):Transform.Identity)).ToArray(),[]);
            Curve?[] Axis(int axis,float value){var c=new Curve?[9];c[axis]=new([new(0,value),new(12,value)]);return c;}
            var source=new AnimationClip(Guid.NewGuid(),"Action",13,new(60),false,[new(names[3],Axis(0,2)),new(names[0],Axis(6,9)),new(names[4],new Curve?[9]),new(names[1],Axis(6,2)),new(names[2],Axis(3,3))]);
            var reference=source with{Id=Guid.NewGuid(),Revision=Guid.NewGuid(),Name="First pose",Tracks=names.Select(n=>new BoneTrack(n,Axis(3,20))).Reverse().ToArray()};
            string fingerprint=AnimationControls.Fingerprint(scene);
            var project=new FighterProject(1,Guid.NewGuid(),"Scoped",scene,[source,reference]){ControlRigs=[new(Guid.NewGuid(),fingerprint,[],[]){BodyBone=1}]};
            var anchor=new BoundaryAnchor(reference,fingerprint,0);string before=JsonSerializer.Serialize(project,ProjectStore.Json);
            var settings=new BoundaryFitSettings(4,4,TransformComponents.Rotation,[names[1],names[2],names[3]],[]){ExistingTracksOnly=true};
            var fit=BoundaryFitting.Prepare(project,source,anchor,anchor,settings);
            check(fit.Clip.Tracks.Select(t=>t.Bone).SequenceEqual(source.Tracks.Select(t=>t.Bone))&&fit.Changed.All(t=>source.Tracks.Single(s=>s.Bone==t.Bone).HasKeys),"Existing-track fitting retains exact track membership and order / renamed="+renamed);
            check(fit.Clip.Tracks.Single(t=>t.Bone==names[1]).Channels[3]!.Sample(0)==20&&fit.Clip.Tracks.Single(t=>t.Bone==names[1]).Channels[4]!.Sample(0)==7,"Bone-level scope can fit selected rotation axes on an already translation-keyed bone / "+renamed);
            check(fit.Changed.Any(t=>t.Bone==names[2])&&fit.Changed.Any(t=>t.Bone==names[3]),"Keyed finger/helper/unweighted bones stay eligible without name heuristics / "+renamed);
            check(ReferenceEquals(fit.Clip.Tracks.Single(t=>t.Bone==names[0]),source.Tracks.Single(t=>t.Bone==names[0]))&&ReferenceEquals(fit.Clip.Tracks.Single(t=>t.Bone==names[4]),source.Tracks.Single(t=>t.Bone==names[4])),"Source-owned controls and empty unselected tracks remain exact original objects / "+renamed);
            var body=fit.Clip.Tracks.Single(t=>t.Bone==names[1]);
            check(Enumerable.Range(0,193).All(i=>Math.Abs(body.Channels[3]!.Sample(3+i/32d))<.00003f)&&fit.Receipt.Settings.ExistingTracksOnly&&fit.Receipt.Limits.Any(t=>t.Contains("no new bone tracks",StringComparison.Ordinal)),"Scoped boundaries retain the independent protected-middle expectation and report their exact membership policy / "+renamed);
            bool Reject(BoundaryFitSettings value,string name){try{BoundaryFitting.Prepare(project,source,anchor,anchor,value);return false;}catch(InvalidDataException error){return error.Message.Contains(name,StringComparison.Ordinal);}}
            check(Reject(settings with{Bones=[names[4]]},names[4])&&Reject(settings with{Bones=[names[5]]},names[5]),"Empty and reference-only bone tracks reject with their exact identities / "+renamed);
            var broad=BoundaryFitting.Prepare(project,source,anchor,anchor,settings with{ExistingTracksOnly=false,Bones=[names[5]]});
            check(broad.Clip.Tracks.Any(t=>t.Bone==names[5])&&!new BoundaryFitSettings(4,4,TransformComponents.All,names,[]).ExistingTracksOnly,"Full fitting remains the unchanged default and can author missing target tracks / "+renamed);
            check(Reject(settings with{Components=TransformComponents.All,PreserveContacts=true,BoundaryContactRecovery=true,ContactBones=[names[5]]},names[5]),"Unkeyed contact recovery dependency is named and blocked rather than silently added / "+renamed);
            check(JsonSerializer.Serialize(project,ProjectStore.Json)==before,"Scoped success and rejected dependency edits preserve the complete original document / "+renamed);
            string path=Path.Combine(dir,"existing-tracks-"+renamed+".fighterport");await ProjectStore.SaveAsync(project with{Clips=[fit.Clip]},path);var reopened=await ProjectStore.LoadAsync(path);
            check(reopened.Clips.Single().Transition!.CurveFit!.Settings.ExistingTracksOnly&&reopened.Clips.Single().Tracks.Select(t=>t.Bone).SequenceEqual(source.Tracks.Select(t=>t.Bone)),"Scoped fit recipe and exact membership survive normal project save/reopen / "+renamed);
        }
        string[] controls=["space","travel","facing","pitch","body"];
        var rig=new Scene("Reviewed missing reference control",controls.Select((n,i)=>new Bone(n,i-1,Transform.Identity)).ToArray(),[]);
        Curve?[] Keyed(){var c=new Curve?[9];c[3]=new([new(0,0),new(12,5)]);return c;}
        var action=new AnimationClip(Guid.NewGuid(),"Action",13,new(60),false,controls.Select((n,i)=>new BoneTrack(n,i==2?new Curve?[9]:Keyed())).ToArray());
        var pose=action with{Id=Guid.NewGuid(),Tracks=controls.Select(n=>new BoneTrack(n,Keyed())).ToArray()};string fp=AnimationControls.Fingerprint(rig);
        string[] roles=["SceneRoot","RootMotion","RootOrientation","BodyOrientation","Hips"];
        var setup=new ControlRigSetup(Guid.NewGuid(),fp,[],[]){BodyBone=4,BoneRoles=roles.Select((r,i)=>new RigMapping(r,i,"Reviewed exact control hierarchy",true)).ToArray()};
        var reviewed=new FighterProject(1,Guid.NewGuid(),"Reviewed",rig,[action,pose]){ControlRigs=[setup]};
        bool rejected=false;try{BoundaryFitting.Prepare(reviewed,action,new(pose,fp,0),null,new(4,4,TransformComponents.All,[controls[0],controls[3],controls[4]],[]){ExistingTracksOnly=true,MatchReferenceControls=true});}
        catch(InvalidDataException error){rejected=error.Message.Contains("unkeyed reference controls",StringComparison.Ordinal)&&error.Message.Contains(controls[2],StringComparison.Ordinal);}
        check(rejected,"Unkeyed reviewed reference control rejects by exact role binding instead of inventing its animation track");
    }
}
