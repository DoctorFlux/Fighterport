using System.Numerics;
using FighterPort.Core;

internal static class ContactReferenceChecks
{
    static SkinSurfacePoint Point(Vertex v)=>new(v.Position,v.Joints,v.Weights);
    static Vertex Hand(Scene scene,int bone)=>scene.Meshes.SelectMany(m=>m.Vertices).First(v=>(int)v.Joints.X==bone&&v.Normal==Vector3.UnitZ);
    public static async Task RunAsync(string directory,Action<bool,string> check)
    {
        var original=ProceduralFixture.Create();var scene=original.Scene;var rig=SurfaceContactChecks.Profile(scene);var rest=AnimationService.Evaluate(scene,null,0);string checksum=AnimationControls.DeformChecksum(scene);
        var platform=new ContactReference(Guid.NewGuid(),"Original moving platform","Authored rigid planar support and trajectory"){GeometryKnown=true,Motion=[new(0,Vector3.Zero,Quaternion.Identity),new(.4,new(.04f,.02f,0),Quaternion.CreateFromAxisAngle(Vector3.UnitY,.04f))]};
        var feet=rig.Chains.Where(c=>c.Role.StartsWith("Foot.")).Select(c=>ContactSolver.Capture(scene,rest,rig,c.Role) with{Reference=platform,Position=rest.World[c.Effector].Translation,Mode=ContactMode.Plant}).ToArray();
        float drift=0;foreach(double seconds in new[]{.4,.125,0,.275,.4})
        {
            var input=rest with{Seconds=seconds};var result=ContactSolver.Solve(scene,input,rig,new Dictionary<int,Transform>(),feet,true,96);check(result.Value is not null,$"Moving/rotating support solves body and both existing legs at {seconds:G4}s: "+string.Join("; ",result.Diagnostics.Select(d=>d.Message)));var pose=AnimationService.Evaluate(scene,null,0,result.Value!.Transforms) with{Seconds=seconds};
            float u=(float)(seconds/.4);var frame=Matrix4x4.CreateRotationY(.04f*u)*Matrix4x4.CreateTranslation(new Vector3(.04f,.02f,0)*u);Matrix4x4.Invert(frame,out var inverse);
            foreach(var pin in feet)drift=Math.Max(drift,Vector3.Distance(Vector3.Transform(pose.World[pin.Bone].Translation,inverse),pin.Position));
            check(pose.World[0]==rest.World[0],"Moving support correction preserves the fixed actor/root bone");
        }
        check(drift<.0002f,"Independent inverse-platform measurement keeps both planted anchors stable in support coordinates");
        var left=rig.Chains.Single(c=>c.Role=="Hand.L");var right=rig.Chains.Single(c=>c.Role=="Hand.R");var handVertices=new[]{Hand(scene,left.Effector),Hand(scene,right.Effector)};
        var bar=new ContactReference(Guid.NewGuid(),"Original two-handed bar","Known two fixed grip frames on one authored rigid item"){GeometryKnown=true,Motion=[new(0,Vector3.Zero,Quaternion.Identity),new(11d/60,new(0,-.01f,.02f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,.01f))],Grips=[new("Left",handVertices[0].Position,Quaternion.Identity),new("Right",handVertices[1].Position,Quaternion.Identity)]};
        var handPins=new[]{left,right}.Select((chain,i)=>new ContactPin(Guid.NewGuid(),chain.Role,chain.Effector,Vector3.Zero,new(999),Quaternion.Identity,true,"Shared bar"){Reference=bar,Socket=i==0?"Left":"Right",SurfacePoints=[Point(handVertices[i])],Mode=ContactMode.Grip}).ToArray();
        var floor=rig.Chains.Where(c=>c.Role.StartsWith("Foot.")).Select(c=>ContactSolver.Capture(scene,rest,rig,c.Role)).ToArray();var all=handPins.Concat(floor).ToArray();var values=new List<Transform[]>();float gripError=0;
        for(int frame=0;frame<12;frame++)
        {
            double seconds=frame/60d;var result=ContactSolver.Solve(scene,rest with{Seconds=seconds},rig,new Dictionary<int,Transform>(),all,true,96);check(result.Value is not null,"Shared-item body solve preserves both palm grips and ground pins at sample "+frame+": "+string.Join("; ",result.Diagnostics.Select(d=>d.Message)));var pose=AnimationService.Evaluate(scene,null,0,result.Value!.Transforms) with{Seconds=seconds};values.Add(pose.Local);
            float u=frame/11f;var world=Matrix4x4.CreateRotationY(.01f*u)*Matrix4x4.CreateTranslation(0,-.01f*u,.02f*u);for(int i=0;i<2;i++)gripError=Math.Max(gripError,Vector3.Distance(AnimationService.Skin(handVertices[i],pose),Vector3.Transform(handVertices[i].Position,world)));
        }
        check(gripError<.0002f,"Both actual skinned palms match independent frames on the same moving/aiming item simultaneously");
        check(ContactGeometry.Target(handPins[0] with{Position=Vector3.Zero},rest)==ContactGeometry.Target(handPins[0] with{Position=new(100)},rest),"Fixed shared grip sockets cannot be moved by ordinary independent hand-spacing positions");
        var clip=new AnimationClip(Guid.NewGuid(),"Original shared-item motion",12,new(60),false,scene.Bones.Select((b,bone)=>new BoneTrack(b.Name,Enumerable.Range(0,9).Select(ch=>(Curve?)new Curve(values.Select((v,f)=>{float at=v[bone].Channels()[ch];int low=Math.Max(0,f-1),high=Math.Min(11,f+1);float tangent=(values[high][bone].Channels()[ch]-values[low][bone].Channels()[ch])/(high-low);return new Key(f,at,tangent,tangent);}).ToArray())).ToArray())).ToArray());
        var source=original with{Clips=[clip],Contacts=[new ContactFrame(Guid.NewGuid(),clip.Id,0,rig.SkeletonFingerprint,all){EndFrame=11}],NativeLayers=[]};var target=ProceduralFixture.Create(1.2f) with{Clips=[],NativeLayers=[]};var targetRig=SurfaceContactChecks.Profile(target.Scene);var targetHands=targetRig.Chains.Where(c=>c.Role.StartsWith("Hand.")).ToArray();
        var binding=new RetargetSupportBinding(bar.Id,bar with{Id=Guid.NewGuid()},ContactTimeDomain.SourceSeconds,"Reviewed original bar, sockets and target palm patches"){TargetDeformChecksum=AnimationControls.DeformChecksum(target.Scene),TargetPatches=targetHands.Select(c=>new ReviewedTargetPatch(c.Role,[Point(Hand(target.Scene,c.Effector))])).ToArray()};
        var missing=RetargetService.Bake(source,clip,rig,target,targetRig);check(missing.Value is null&&missing.Diagnostics.Any(d=>d.Message.Contains("reviewed target support")),"Cross-rig interactions require exact reviewed target bindings instead of inventing a weapon from hand positions");
        var baked=RetargetService.Bake(source,clip,rig,target,targetRig,fitting:new(){ArmSpacingDegrees=15,FreeHandReach=.8f},supportBindings:[binding]);check(baked.Value is not null,"Unequal-arm retargeting retains the shared item and both hard grip frames: "+string.Join("; ",baked.Diagnostics.Select(d=>d.Message)));
        var bake=baked.Value!;float worst=0;foreach(double f in new[]{.125,3.375,7.75,10.875}){var pose=AnimationService.Evaluate(target.Scene,bake.Clip,f);foreach(var pin in bake.Contacts.SelectMany(c=>c.Pins))worst=Math.Max(worst,ContactGeometry.Residual(pin,pose).Length());}
        check(worst<.0003f,"Baked ordinary target keys retain both skin grips and ground pins at withheld fractional times under artistic offsets");
        string path=Path.Combine(directory,"shared-item-retarget.fighterport");await ProjectStore.SaveAsync(RetargetService.Apply(target,bake),path);var reopened=await ProjectStore.LoadAsync(path);check(reopened.Clips[0].Retarget!.SupportBindings.Single().Target.Grips.Length==2&&reopened.Contacts.SelectMany(c=>c.Pins).Count(p=>p.Reference?.Id==binding.Target.Id)==2,"One shared item identity, fixed sockets and reviewed target patches survive save/reopen");
        var face=scene.Meshes[0].Vertices.Select((v,i)=>(v,i)).Where(p=>(int)p.v.Joints.X==2&&p.v.Normal==Vector3.UnitZ).Take(3).ToArray();var vertices=(Vertex[])scene.Meshes[0].Vertices.Clone();vertices[face[2].i]=vertices[face[2].i] with{Joints=new(2,1,0,0),Weights=new(.5f,.5f,0,0)};var mixedScene=scene with{Meshes=[scene.Meshes[0] with{Vertices=vertices}]};
        var self=new ContactReference(Guid.NewGuid(),"Torso touch","Reviewed barycentric anchor on actual deforming torso triangle"){SurfaceTripod=face.Select(p=>Point(vertices[p.i])).ToArray(),SurfaceAnchor=new(.25f,.25f,.5f),GeometryKnown=true};
        var edit=new Dictionary<int,Transform>{{2,scene.Bones[2].Rest with{Rotation=new(0,20,0)}}};var torsoPose=AnimationService.Evaluate(mixedScene,null,0,edit);var selfPin=handPins[0] with{Reference=self,Socket=null,Position=new(0,0,.01f),HoldRotation=false};var touch=ContactSolver.Solve(mixedScene,torsoPose,rig,edit,[selfPin],false,96);
        check(touch.Value is not null,"Hand self-contact solves against a deforming surface while preserving explicit torso FK");var retained=torsoPose.Local.Select((t,i)=>(t,i)).ToDictionary(p=>p.i,p=>p.t);foreach(var change in touch.Value!.Transforms)retained[change.Key]=change.Value;var touching=AnimationService.Evaluate(mixedScene,null,0,retained);var a=AnimationService.Skin(vertices[face[0].i],touching);var b=AnimationService.Skin(vertices[face[1].i],touching);var c=AnimationService.Skin(vertices[face[2].i],touching);var expected=a*.25f+b*.25f+c*.5f+Vector3.Normalize(Vector3.Cross(b-a,c-a))*.01f;
        check(Vector3.Distance(AnimationService.Skin(handVertices[0],touching),expected)<.0002f,"Independent actual blended-skin barycentric point verifies self-contact rather than a rigid pelvis anchor");
        check(AnimationControls.DeformChecksum(scene)==checksum,"Moving supports, shared grips and self-contact leave the original deform skeleton/rest/weights unchanged");
    }
}
