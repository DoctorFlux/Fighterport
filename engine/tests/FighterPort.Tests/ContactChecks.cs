using System.Numerics;
using System.Text.Json;
using FighterPort.Core;

internal static class ContactChecks
{
    public static async Task RunAsync(FighterProject original,Scene alternate,string directory,Action<bool,string> check)
    {
        foreach(var scene in new[]{original.Scene,alternate})
        {
            string label=scene==original.Scene?"original":"renamed/proportioned";var setup=AnimationControls.SetUp(scene);int body=ContactSolver.BodyBone(scene,setup);check(body>=0,$"{label}: body control is inferred from shared leg ancestry without native bone names");
            var basePose=AnimationService.Evaluate(scene,null,0);var source=JsonSerializer.Serialize(scene,ProjectStore.Json);var checksum=AnimationControls.DeformChecksum(scene);
            var feet=setup.Chains.Where(c=>c.Role.StartsWith("Foot.",StringComparison.Ordinal)).Select(c=>ContactSolver.Capture(scene,basePose,setup,c.Role,"Original floor")).ToArray();
            var pelvis=basePose.Local[body] with{Translation=basePose.Local[body].Translation-new Vector3(0,.1f,.025f),Rotation=basePose.Local[body].Rotation+new Vector3(0,4,0)};
            var solved=ContactSolver.Solve(scene,basePose,setup,new Dictionary<int,Transform>{{body,pelvis}},feet);check(solved.Value is not null,$"{label}: moving/rotating pelvis preserves both planted feet");
            var pose=AnimationService.Evaluate(scene,null,0,solved.Value!.Transforms);check(feet.All(p=>Vector3.Distance(pose.World[p.Bone].Translation,p.Position)<.0002f&&Math.Abs(Quaternion.Dot(PoseGeometry.Rotation(pose.World[p.Bone]),p.Rotation))>.99999f),$"{label}: independent world foot positions and orientations remain fixed");
            check(pose.Local[body]==pelvis,$"{label}: contact solve preserves the requested pelvis FK transform exactly");
            var impossible=ContactSolver.Solve(scene,basePose,setup,new Dictionary<int,Transform>{{body,pelvis with{Translation=pelvis.Translation+new Vector3(0,10,0)}}},feet);
            check(impossible.Value is null&&impossible.Diagnostics.Any(d=>d.Code=="CONTACT_CONFLICT"&&feet.All(p=>d.Message.Contains(p.Id.ToString()))),$"{label}: impossible planted pose reports every exact conflicting anchor without stretching");
            var bent=basePose.Local.Select((t,i)=>(t,i)).ToDictionary(x=>x.i,x=>x.t);
            foreach(var chain in setup.Chains){var r=bent[chain.Middle].Rotation;r.Z+=chain.Role.StartsWith("Hand",StringComparison.Ordinal)?35:0;r.X+=chain.Role.StartsWith("Foot",StringComparison.Ordinal)?20:0;bent[chain.Middle]=bent[chain.Middle] with{Rotation=r};}
            var supported=AnimationService.Evaluate(scene,null,0,bent);var all=setup.Chains.Select(c=>ContactSolver.Capture(scene,supported,setup,c.Role,c.Role.StartsWith("Hand",StringComparison.Ordinal)?"Ledge / hand support":"Floor")).ToArray();
            var lowered=supported.Local[body] with{Translation=supported.Local[body].Translation-new Vector3(0,.025f,0)};var four=ContactSolver.Solve(scene,supported,setup,new Dictionary<int,Transform>{{body,lowered}},all);
            check(four.Value is not null&&four.Value.PositionError<.0002f,$"{label}: hand/ledge anchors and planted feet coexist during a body edit");
            var seat=new ContactPin(Guid.NewGuid(),"Seat",body,new(0,-.08f,-.03f),Vector3.Transform(new(0,-.08f,-.03f),supported.World[body]),PoseGeometry.Rotation(supported.World[body]),true,"Seat surface");
            var chest=supported.Local[2] with{Rotation=supported.Local[2].Rotation+new Vector3(0,2,0)};
            var seated=ContactSolver.Solve(scene,supported,setup,new Dictionary<int,Transform>{{2,chest}},[seat,..all],true);
            check(seated.Value is not null&&seated.Value.Transforms[2]==chest&&seated.Value.PositionError<.0002f,$"{label}: actual chest FK retains seat/body support alongside hands and feet");
            var lyingLocal=bent.ToDictionary();lyingLocal[0]=lyingLocal[0] with{Rotation=new(0,0,90)};var lying=AnimationService.Evaluate(scene,null,0,lyingLocal);var lyingPins=setup.Chains.Select(c=>ContactSolver.Capture(scene,lying,setup,c.Role,"Rotated body support")).ToArray();
            var lyingBody=lying.Local[body] with{Translation=lying.Local[body].Translation-new Vector3(0,.01f,0)};
            check(ContactSolver.Solve(scene,lying,setup,new Dictionary<int,Transform>{{body,lyingBody}},lyingPins).Value is not null,$"{label}: body editing in a lying pose retains hand/foot supports without assuming standing world-Y limbs");
            check(checksum==AnimationControls.DeformChecksum(scene)&&source==JsonSerializer.Serialize(scene,ProjectStore.Json),$"{label}: all contact operations preserve skeleton, helpers, rest transforms and weights");
            var doc=new EditorDocument(original with{Scene=scene,Clips=[],ControlRigs=[setup]});doc.CreateAnimation("Authored supported pose",60,new(60),false);doc.Seek(12);doc.PinControls(setup,feet.Select(p=>p.Role).ToArray());var before=doc.Evaluate();
            doc.KeyTransforms(solved.Value.Transforms,"Body with planted feet");doc.Undo();check(doc.Evaluate().Local.SequenceEqual(before.Local),$"{label}: entire body/contact bake undoes as one transaction");doc.Redo();
            var file=Path.Combine(directory,label.Replace('/','-')+"-contacts.fighterport");await ProjectStore.SaveAsync(doc.Project,file);var loaded=new EditorDocument(await ProjectStore.LoadAsync(file));loaded.Seek(12);check(loaded.CurrentContacts.Length==2&&loaded.Evaluate().Local[body]==pelvis,$"{label}: saved contact anchors and actual body keys reopen together");loaded.ReleaseContacts();check(loaded.CurrentContacts.Length==0,$"{label}: releasing contacts permits airborne edits");
        }
        var s=original.Scene;var rig=AnimationControls.SetUp(s);var chainScaled=rig.Chains.Single(c=>c.Role=="Hand.R");var scaled=s.Bones.Select(b=>b.Rest).ToArray();scaled[chainScaled.Root]=scaled[chainScaled.Root] with{Scale=new(1.12f,.9f,1.04f)};
        var input=AnimationService.Evaluate(s,null,0,scaled.Select((v,i)=>(v,i)).ToDictionary(x=>x.i,x=>x.v));var desired=scaled.Select((v,i)=>(v,i)).ToDictionary(x=>x.i,x=>x.v);desired[chainScaled.Root]=desired[chainScaled.Root] with{Rotation=desired[chainScaled.Root].Rotation+new Vector3(4,7,3)};desired[chainScaled.Middle]=desired[chainScaled.Middle] with{Rotation=desired[chainScaled.Middle].Rotation+new Vector3(0,9,12)};
        var expected=AnimationService.Evaluate(s,null,0,desired);var goal=new ContactPin(Guid.NewGuid(),chainScaled.Role,chainScaled.Effector,Vector3.Zero,expected.World[chainScaled.Effector].Translation,PoseGeometry.Rotation(expected.World[chainScaled.Effector]),true,"Independent reachable scaled pose");var result=ContactSolver.Solve(s,input,rig,new Dictionary<int,Transform>(),[goal]);
        check(result.Value is not null&&result.Value.PositionError<.0002f,"Damped contact correction reaches an independently constructed positive nonuniform-scale pose without normalizing native channels");
    }
}
