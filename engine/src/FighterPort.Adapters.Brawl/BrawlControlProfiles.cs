using System.Numerics;
using FighterPort.Core;

namespace FighterPort.Adapters.Brawl;

// Reviewed mappings are gated by exact names, hierarchy and rest SRT fingerprints.
// No native assets, deform skeletons or weights are embedded in these profiles.
public static class BrawlControlProfiles
{
    public static ControlRigSetup SetUp(FighterProject project)
    {
        var scene=project.Scene;var fingerprint=AnimationControls.Fingerprint(scene);
        var saved=project.ControlRigs.FirstOrDefault(s=>s.SkeletonFingerprint==fingerprint);
        if(saved is not null&&(saved.Provenance is not null||saved.Chains.Any(c=>c.Reviewed)))return saved.Provenance?.ProfileId is "brawl.mario.mdl0v9.supplied-rig" or "brawl.snake.mdl0v9.supplied-rig"?WithDetails(scene,saved):saved;
        bool native=project.SourceModels.Any(m=>m.Resource.Format=="MDL0"&&m.Resource.Version==9);
        string? profile=native?fingerprint switch{
            "9696894FD7E5D9AE8A208B6216709CAE5BE7C8EEAEFBEC67396304C8577B49A8"=>"brawl.mario.mdl0v9.supplied-rig",
            "7E2F1D577E1DE0375088BFBC05F69A145002B27839F066E25C458782FFEF059F"=>"brawl.snake.mdl0v9.supplied-rig",
            _=>null}:null;
        if(profile is null)return AnimationControls.SetUp(scene,project.ControlRigs);
        int Bone(string name)=>Array.FindIndex(scene.Bones,b=>b.Name==name);
        var bind=AnimationService.Evaluate(scene,null,0);
        var weighted=scene.Meshes.SelectMany(m=>m.Vertices).SelectMany(v=>Enumerable.Range(0,v.InfluenceCount).Where(i=>v.WeightAt(i)>0).Select(i=>(int)v.JointAt(i))).ToHashSet();
        var setup=new ControlRigSetup(Guid.NewGuid(),fingerprint,[],[]);
        foreach(var side in new[]{"L","R"})foreach(var family in new[]{"Hand","Foot"})
        {
            string[] names=family=="Hand"?[side+"ShoulderJ",side+"ArmJ",side+"HandN"]:[side+"LegJ",side+"KneeJ",side+"FootJ"];
            var chain=names.Select(Bone).ToArray();
            // Fingerprint alone never grants support for a mesh with missing limb bindings.
            if(chain.Any(i=>i<0||!weighted.Contains(i)) || (bind.World[chain[2]].Translation.X>0)!=(side=="L"))
                return AnimationControls.SetUp(scene) with{Diagnostics=[new("PROFILE_SKIN_OR_SIDE","Known skeleton has incompatible skin influences or bind-side geometry; review the mapping.",false)]};
            var review=AnimationControls.ReviewChain(scene,setup,family+"."+side,chain[0],chain[1],chain[2],family=="Hand"?-Vector3.UnitZ:Vector3.UnitZ,
                "Reviewed exact native rig: direct anatomical chain, weighted joints, mirrored bind geometry and native local axes; helper siblings retain their own channels.");
            if(review.Value is null)throw new InvalidDataException(string.Join("; ",review.Diagnostics.Select(d=>d.Message)));
            setup=review.Value;
        }
        var roles=new List<RigMapping>();
        void Role(string role,string name){int i=Bone(name);if(i>=0)roles.Add(new(role,i,"Reviewed native hierarchy, bind geometry and exact rest fingerprint",true));}
        Role("RootMotion","TransN");Role("RootOrientation","YRotN");Role("Hips","HipN");Role("Spine",Bone("WaistN")>=0?"WaistN":"BodyN");Role("Chest","BustN");Role("Neck","NeckN");Role("Head","HeadN");
        foreach(var side in new[]{"L","R"})
        {
            foreach(var (role,suffix) in new[]{("Clavicle","ShoulderN"),("UpperArm","ShoulderJ"),("Elbow","ArmJ"),("Wrist","HandN"),("UpperLeg","LegJ"),("Knee","KneeJ"),("Ankle","FootJ"),("Toe","ToeN")})Role(role+"."+side,side+suffix);
        }
        string[] helperNames=profile.Contains("mario",StringComparison.Ordinal)?["LFootE","RFootE","LHandNb","RHandNb","WaistNb"]:["LShoulderJb","RShoulderJb","LArmJb","RArmJb"];
        return WithDetails(scene,setup with{BoneRoles=roles.ToArray(),Axes=new(-Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),PreservedHelperBones=helperNames.Select(Bone).Where(i=>i>=0).ToArray(),
            Provenance=new(profile,1,new("Brawl","Wii","Supplied MDL0 v9 rig; disc/build revision unestablished"),
                "Native model space: +Y up, named left at +X, front +Z; right limbs retain native 180-degree local rotations",
                "Reviewed supplied rig topology, weighted bind geometry, axes and helper branches. Exact fingerprints gate reuse; no game-wide alias inference.")});
    }
    static ControlRigSetup WithDetails(Scene scene,ControlRigSetup setup)
    {
        // These annotations are valid only for the two exact reviewed rest/hierarchy hashes.
        if(setup.SkeletonFingerprint!=AnimationControls.Fingerprint(scene)||setup.SkeletonFingerprint is not ("9696894FD7E5D9AE8A208B6216709CAE5BE7C8EEAEFBEC67396304C8577B49A8" or "7E2F1D577E1DE0375088BFBC05F69A145002B27839F066E25C458782FFEF059F"))return setup;
        var roles=setup.BoneRoles.ToList();int Bone(string name)=>Array.FindIndex(scene.Bones,b=>b.Name==name);
        void Role(string role,string name){int i=Bone(name);if(i>=0&&setup.UnassignedBoneRoles?.Contains(role,StringComparer.Ordinal)!=true&&!roles.Any(r=>r.SemanticRole==role))roles.Add(new(role,i,"Reviewed exact native fingerprint, parent branch, mirrored bind geometry and local axes",true));}
        Role("SceneRoot","TopN");Role("BodyOrientation","XRotN");Role("ThrowAnchor","ThrowN");Role("Channel.YellowEyes","EyeYellowM");
        foreach(string side in new[]{"L","R"})
        {
            Role("Grip."+side,side+"HaveN");
            foreach(var (finger,suffix) in new[]{("Index","1st"),("Middle","2nd"),("Ring","3rd"),("Little","4th"),("Thumb","Thumb")})
            {
                int palm=Bone(side+"HandN"),first=Bone(side+suffix+"Na"),second=Bone(side+suffix+"Nb");
                if(palm<0||first<0||second<0||scene.Bones[first].Parent!=palm||scene.Bones[second].Parent!=first)continue;
                Role($"Finger.{finger}.Proximal.{side}",side+suffix+"Na");Role($"Finger.{finger}.Distal.{side}",side+suffix+"Nb");
            }
        }
        return setup with{BoneRoles=roles.ToArray(),GroundFootToleranceFraction=setup.GroundFootToleranceFraction,Provenance=setup.Provenance is {} provenance?provenance with{Revision=4}:null};
    }
}
