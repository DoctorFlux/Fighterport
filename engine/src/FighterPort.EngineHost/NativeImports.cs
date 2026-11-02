using FighterPort.Core;
using FighterPort.NativeEvaluation;
using System.Numerics;

sealed partial class ControlSession
{
    async Task<object> NativeSamples(Request request)
    {
        var p=await ImportNative(request.Model!,request.Motion,request.NativeRig);var frames=request.SampleFrames??[0];
        if(frames.Length>32)throw new InvalidDataException("Native sample probe budget exceeded.");
        float[] V(Vector3 v)=>[v.X,v.Y,v.Z];
        float[] M(Matrix4x4 m)=>[m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
        return new{Ok=true,NativeSamples=frames.Select(frame=>
        {
            var pose=AnimationService.Evaluate(p.Scene,p.Clips.SingleOrDefault(),frame);
            return new{Frame=frame,World=pose.World.Select(M).ToArray(),Surface=p.Scene.Meshes.SelectMany(mesh=>mesh.Vertices.Select(v=>V(AnimationService.Skin(v,pose)))).ToArray()};
        }).ToArray()};
    }
    async Task<FighterProject> ImportNative(string model,string? motion,NativeRigData? data)
    {
        var project=string.IsNullOrEmpty(motion)?await adapter.ImportModelAsync(model):await adapter.ImportAsync(model,motion);
        if(data is null)return project;
        if(data.Joints.Length!=project.Scene.Bones.Length||data.Vertices.Length==0||data.Vertices.Length>500000||data.Roles.Values.Any(i=>i<0||i>=data.Joints.Length))throw new InvalidDataException("Native evaluation data does not match the frozen model.");
        var vertices=data.Vertices.Select(v=>
        {
            var bones=v.Joints.Length>0?v.Joints:[v.Skin.SharedJoint>=0&&v.Skin.MatrixIndex==1?v.Skin.SharedJoint:v.Skin.Owner];var weights=v.Joints.Length>0?v.Weights:[1f];
            var influence=bones.Select((b,i)=>new SkinInfluence(b,weights[i])).ToArray();
            var prefixBones=Vector4.Zero;var prefixWeights=Vector4.Zero;for(int i=0;i<Math.Min(4,bones.Length);i++){prefixBones[i]=bones[i];prefixWeights[i]=weights[i];}
            return new Vertex(v.Position,v.Normal,Vector2.Zero,prefixBones,prefixWeights,Vector4.One){Influences=influence,NativeSkin=v};
        }).ToArray();
        var policies=new NativeRigData{Identity=data.Identity,Joints=data.Joints,Roles=data.Roles,Vertices=[]};
        var scene=project.Scene with{NativeEvaluation=policies,Meshes=[new Mesh("native-deform",vertices,Enumerable.Range(0,vertices.Length).Select(i=>(uint)i).ToArray())]};
        scene.Validate();return project with{Scene=scene};
    }
    static BindingReview NativeBinding(FighterProject project)
    {
        var native=project.Scene.NativeEvaluation!;var roles=new Dictionary<string,string>();
        var mapping=new Dictionary<string,string>{{"pelvis","Hips"},{"spine","Spine"},{"chest","Chest"},{"neck","Neck"},{"head","Head"},{"root","RootMotion"}};
        foreach(string side in new[]{"left","right"})foreach(var part in new[]{("upperArm","UpperArm"),("lowerArm","Elbow"),("hand","Wrist"),("upperLeg","UpperLeg"),("lowerLeg","Knee"),("foot","Ankle")})mapping.Add(side+"."+part.Item1,part.Item2+"."+(side=="left"?"L":"R"));
        foreach(var pair in mapping)if(native.Roles.TryGetValue(pair.Key,out int id))roles.Add(pair.Value,project.Scene.Bones[id].Name);
        var rest=AnimationService.Evaluate(project.Scene,null,0);Vector3 up=Vector3.UnitY;
        var right=rest.World[native.Roles["left.upperArm"]].Translation-rest.World[native.Roles["right.upperArm"]].Translation;right=Vector3.Normalize(right-up*Vector3.Dot(right,up));var forward=Vector3.Normalize(Vector3.Cross(right,up));
        float[] V(Vector3 v)=>[v.X,v.Y,v.Z];
        return new(AnimationControls.Fingerprint(project.Scene),roles,V(right),V(up),V(forward),"Unambiguous native role tables and authentic bind geometry; adapter-declared +Y up");
    }
}
