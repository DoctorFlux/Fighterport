using FighterPort.Core;
using System.Numerics;
using System.Text.Json;
internal static class ContactAuthoringChecks
{
    public static async Task RunAsync(string output,Action<bool,string> check)
    {
        var scene=new Scene("Original bent chain",[new("origin",-1,Transform.Identity),new("body",0,Transform.Identity),new("upper",1,Transform.Identity),new("middle",2,new(Vector3.One,new(0,0,60),Vector3.UnitX)),new("end",3,new(Vector3.One,Vector3.Zero,Vector3.UnitX))],[]);
        var rig=AnimationControls.ReviewChain(scene,AnimationControls.SetUp(scene),"Hand.L",2,3,4,Vector3.UnitY,"Original procedural reviewed direct chain").Value!;
        Curve?[] RotZ(float a,float b){var curves=new Curve?[9];curves[5]=new([new(0,a,(b-a)/23,(b-a)/23),new(23,b,(b-a)/23,(b-a)/23)]);return curves;}
        var clip=new AnimationClip(Guid.NewGuid(),"Original action",24,new(60),false,[new("upper",RotZ(0,8))]);var project=new FighterProject(1,Guid.NewGuid(),"Contact authoring",scene,[clip]){ControlRigs=[rig]};string before=JsonSerializer.Serialize(project,ProjectStore.Json);
        var options=new ContactBakeOptions("Place","end",6,11,4,6,[0,0,.025f],[0,0,2]);
        var placed=ContactAuthoring.Prepare(project,clip,rig,options,["origin","body"]);
        check(placed.Clip.Id!=clip.Id&&placed.Clip.SampleCount==24&&placed.Clip.Rate==clip.Rate&&placed.Clip.Loop==clip.Loop,"Contact placement creates an independent revision without changing duration or native timebase");
        check(placed.Contacts.Single().Frame==6&&placed.Contacts.Single().EndFrame==11,"Authored contact stores the explicit hold interval separately from approach and release");
        var pin=placed.Contacts.Single().Pins.Single();bool holds=true;for(double frame=6;frame<=11;frame+=.25){var pose=AnimationService.Evaluate(scene,placed.Clip,frame);holds&=Vector3.Distance(pose.World[4].Translation,pin.Position)<=placed.Receipt.PositionBound&&PoseGeometry.RotationDifference(PoseGeometry.Rotation(pose.World[4]),pin.Rotation)<=.25f;}
        check(holds,"Independent quarter-frame checks meet frozen position/orientation bounds for a placed contact");
        float exteriorError=Enumerable.Range(0,9).Max(i=>Vector3.Distance(AnimationService.Evaluate(scene,clip,i/8d).World[4].Translation,AnimationService.Evaluate(scene,placed.Clip,i/8d).World[4].Translation));Console.WriteLine("Analytic split exterior float error: "+exteriorError);check(exteriorError<.000003f,"Contact placement preserves the earlier original action within independent float split precision");
        check(JsonSerializer.Serialize(project,ProjectStore.Json)==before&&AnimationControls.Fingerprint(scene)==rig.SkeletonFingerprint,"Contact placement preserves original documents, hierarchy, rests and helpers");
        var releasing=project with{Clips=[placed.Clip],Contacts=placed.Contacts};var released=ContactAuthoring.Prepare(releasing,placed.Clip,rig,options with{Action="Release",Start=9,End=9,Blend=4},["origin","body"],clip);
        check(!released.Contacts.Any(c=>c.Contains(10)&&c.Pins.Any(p=>p.Bone==4)),"Release removes the selected future editor constraint instead of retaining an invisible lock");
        check(released.Contacts.Any(c=>c.Contains(8)&&c.Pins.Any(p=>p.Bone==4)),"Release retains the exact earlier authored contact interval");
        check(Enumerable.Range(14,10).All(frame=>Vector3.Distance(AnimationService.Evaluate(scene,released.Clip,frame).World[4].Translation,AnimationService.Evaluate(scene,clip,frame).World[4].Translation)<.0001f),"Release returns the selected chain into the frozen original animation after its blend");
        bool Reject(Action action){try{action();return false;}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or NotSupportedException){return true;}}
        check(Reject(()=>ContactAuthoring.Prepare(project,clip,rig,options with{Offset=[1000,0,0]},[])),"Unreachable fixed contact placement rejects without changing native bones");
        check(Reject(()=>ContactAuthoring.Prepare(project,clip,rig,options with{Start=1},[])),"A contact approach cannot extend before the original action");
        check(Reject(()=>ContactAuthoring.Prepare(project,clip,rig,options with{Action="Release"},[],clip)),"Release cannot invent a contact timeline from a plain clip");
        var protectedProject=project with{Moves=[new(Guid.NewGuid(),"Protected action","",[new(8,9,"Gameplay event")])]};check(Reject(()=>ContactAuthoring.Prepare(protectedProject,clip,rig,options,[])),"Contact placement rejects protected gameplay event intersections");
        Directory.CreateDirectory(output);string path=Path.Combine(output,"contact-authoring.fighterport");await ProjectStore.SaveAsync(releasing,path);var fresh=await ProjectStore.LoadAsync(path);check(fresh.Contacts.Single().Pins.Single().Position==pin.Position&&fresh.Contacts.Single().Pins.Single().HoldRotation,"Core project reopen retains fixed contact position and orientation policy");
        var singular=scene with{Bones=scene.Bones.Select((b,i)=>i==3?b with{Rest=b.Rest with{Rotation=new(175,-90,-175)}}:i==4?b with{Rest=b.Rest with{Translation=Vector3.UnitY}}:b).ToArray()};
        var singularRig=AnimationControls.ReviewChain(singular,AnimationControls.SetUp(singular),"Hand.L",2,3,4,Vector3.UnitY,"Original procedural Euler-singularity fixture").Value!;
        var singularProject=project with{Scene=singular,ControlRigs=[singularRig]};var singularPose=AnimationService.Evaluate(singular,clip,6);var inward=(singularPose.World[2].Translation-singularPose.World[4].Translation)*.01f;
        var singularResult=ContactAuthoring.Prepare(singularProject,clip,singularRig,options with{Offset=[inward.X,inward.Y,inward.Z],Angles=[0,0,0]},["origin","body"]);
        check(singularResult.Receipt.CurveEvaluations<=30000&&Enumerable.Range(0,21).All(i=>Vector3.Distance(AnimationService.Evaluate(singular,singularResult.Clip,6+i*.25).World[4].Translation,singularResult.Contacts.Single().Pins.Single().Position)<=singularResult.Receipt.PositionBound),"A procedural native Euler singularity retains contact bounds with bounded offline shared-tangent fitting");
        var worldResult=ContactAuthoring.Prepare(project,clip,rig,options with{Angles=[12,17,9]},["origin","body"]);var worldExpected=PoseGeometry.RotationMatrix(AnimationService.Evaluate(scene,clip,6).World[4])*Matrix4x4.CreateFromYawPitchRoll(17*MathF.PI/180,12*MathF.PI/180,9*MathF.PI/180);
        check(PoseGeometry.RotationDifference(worldResult.Contacts.Single().Pins.Single().Rotation,PoseGeometry.Rotation(worldExpected))<.04f,"Contact angle offsets compose in world axes, independently checked by ordered rotation matrices");
    }
}
