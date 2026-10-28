using System.Numerics;
using System.Text.Json;
using FighterPort.Core;
using FighterPort.Adapters.Brawl;

internal static class CompleteSkinChecks
{
    internal static async Task Run(string[] models, Action<bool,string> check)
    {
        var legacy=new Vertex(Vector3.Zero,Vector3.UnitY,Vector2.Zero,Vector4.Zero,new(1,0,0,0),Vector4.One);
        check(!JsonSerializer.Serialize(legacy,ProjectStore.Json).Contains("Influences"),"Legacy four-slot scene/contact serialization omits the additive influence field, retaining old dependency encodings");
        foreach (int count in new[]{5,9,17})
        {
            var bones=Enumerable.Range(0,count).Select(i=>new Bone("joint-"+i,-1,Transform.Identity)).ToArray();
            var all=Enumerable.Range(0,count).Select(i=>new SkinInfluence(i,i==count-1?.19999998807907105f:.8f/(count-1))).ToArray();
            var v=new Vertex(new(2,-3,4),Vector3.UnitY,Vector2.Zero,new(0,1,2,3),new(all[0].Weight,all[1].Weight,all[2].Weight,all[3].Weight),Vector4.One){Influences=all};
            var scene=new Scene("complete",bones,[new("mesh",[v],[0,0,0])]);scene.Validate();
            var tracks=bones.Select((b,i)=>new BoneTrack(b.Name,Enumerable.Range(0,9).Select(c=>c==6?new Curve([new Key(0,i+1)],Interpolation.Hermite):null).ToArray())).ToArray();
            var clip=new AnimationClip(Guid.NewGuid(),"translate",1,new(60),false,tracks);
            var pose=AnimationService.Evaluate(scene,clip,0);var expected=Vector3.Zero;
            for(int i=0;i<count;i++)expected+=(v.Position+new Vector3(i+1,0,0))*all[i].Weight;
            check(Vector3.Distance(AnimationService.Skin(v,pose),expected)<.00001f,$"All {count} ordered original influence coefficients contribute to native geometry without normalization");
            var point=new SkinSurfacePoint(v.Position,v.Joints,v.Weights){Influences=all};
            check(ContactGeometry.Valid(point,count)&&Vector3.Distance(ContactGeometry.Point(point,pose),expected)<.00001f,$"All {count} influences survive contact/surface evaluation");
            var setup=new ControlRigSetup(Guid.NewGuid(),AnimationControls.Fingerprint(scene),[],[])
                {BodyBone=0,Axes=new(Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ),BoneRoles=[new("Hips",0,"Original complete-weight fixture",true)]};
            var rig=RigDescriptor.Prepare(scene,setup);
            var differential=new NativePoseDifferentials(rig,pose,0,[count-1]);
            var expectedDerivative=new Vector3(-v.Position.Y,v.Position.X,0)*all[^1].Weight;
            check(Vector3.Distance(differential.Point(point,5),expectedDerivative)<.00001f,$"Influence {count} contributes its independently derived native skin rotation derivative");
            var round=JsonSerializer.Deserialize<Scene>(JsonSerializer.Serialize(scene,ProjectStore.Json),ProjectStore.Json)!;round.Validate();
            check(round.Meshes[0].Vertices[0].Influences.SequenceEqual(all),$"All {count} influence identities/order/float coefficients survive storage");
            var changed=(SkinInfluence[])all.Clone();changed[^1]=changed[^1] with{Weight=MathF.BitIncrement(changed[^1].Weight)};
            var revised=scene with{Meshes=[scene.Meshes[0] with{Vertices=[v with{Influences=changed}]}]};
            check(AnimationControls.DeformChecksum(scene)!=AnimationControls.DeformChecksum(revised),$"Influence {count} participates in immutable geometry dependency fingerprints");
            check(!SkinInfluences.Valid(v.Joints,v.Weights with{X=MathF.BitIncrement(v.Weights.X)},all,count),"Conflicting legacy-prefix transport is rejected rather than silently choosing/truncating data");
        }
        foreach(string model in models)
        {
            var adapter=new BrawlAdapter(Environment.GetEnvironmentVariable("FIGHTERPORT_BRAWL_WORKER")??throw new InvalidOperationException("Worker required"));
            var project=await adapter.ImportModelAsync(model);project.Scene.Validate();
            var vertices=project.Scene.Meshes.SelectMany(m=>m.Vertices).Where(v=>v.InfluenceCount>4).ToArray();
            check(vertices.Length>0,"Authentic target imports complete native influences beyond four");
            foreach(var v in vertices)
            {
                check(v.Influences.Length==5&&v.Influences.All(w=>BitConverter.SingleToInt32Bits(w.Weight)==unchecked((int)0x3E4CCCCC)),"Authentic Sonic five-way coefficients retain exact source bits and order");
            }
        }
    }
}
