using System.Numerics;

namespace FighterPort.Core;

public enum MorphAttribute { Position, Normal, Color0, Color1 }
public enum MorphNormalPolicy { NormalizeBeforeSkin, NativeWeightedRoundTrip }
// Native array indices survive facepoint seams and shared arrays. Bind matrices are deduplicated.
public record MorphTopology(NativeResourceRef Basis,Vector4[] BaseValues,int[] Indices,int[] TransformIndices,Matrix4x4[] BindMatrices);
public record MorphTarget(string Name,Curve Weight,Vector4[] Values)
{
    public NativeResourceRef? Native {get;init;}
    public Vector4[]? LocalValues {get;init;}
    public bool GeometryEdited {get;init;}
}
public record MorphBinding(string Entry,int Mesh,MorphAttribute Attribute,MorphTarget[] Targets)
{
    public bool ByteColors {get;init;}
    public MorphNormalPolicy NormalPolicy {get;init;}
    public NativeResourceRef? Native {get;init;}
    public MorphTopology? Topology {get;init;}
}
public record MorphRange(int Mesh,int FirstVertex,int VertexCount);

// Prepared model-space values. Native lookup/decoding happens before construction, never while sampling.
// Each output vertex has position, normal and two colors (14 floats); skin/UV data stays immutable.
public sealed class MorphEvaluation
{
    public const int Stride=14;
    readonly float[] original;
    readonly (MorphBinding Binding,int First,float[] Weights)[] bindings;
    readonly Vertex[] vertices;readonly Vector3[] normalBlend;readonly Matrix4x4[] lastSkin,inverseSkin;readonly bool[] inverseReady;readonly bool nativeNormals;
    readonly (int First,int Count)[] roundTripNormals;
    double lastFrame=double.NaN;
    public float[] VertexData {get;}
    public MorphRange[] Ranges {get;}
    public int BindingCount=>bindings.Length;
    public long Revision {get;private set;}
    public MorphEvaluation(Scene scene,NativeAnimationLayer layer)
    {
        Validate(scene,layer);int[] starts=new int[scene.Meshes.Length];int count=0;
        for(int i=0;i<starts.Length;i++){starts[i]=count;count+=scene.Meshes[i].Vertices.Length;}
        vertices=scene.Meshes.SelectMany(m=>m.Vertices).ToArray();normalBlend=vertices.Select(v=>v.Normal).ToArray();lastSkin=new Matrix4x4[scene.Bones.Length];inverseSkin=new Matrix4x4[scene.Bones.Length];inverseReady=new bool[scene.Bones.Length];nativeNormals=layer.Morphs.Any(b=>b.Attribute==MorphAttribute.Normal&&b.NormalPolicy==MorphNormalPolicy.NativeWeightedRoundTrip);
        original=new float[count*Stride];int v=0;
        foreach(var mesh in scene.Meshes)foreach(var p in mesh.Vertices)
        {
            int o=v++*Stride;original[o]=p.Position.X;original[o+1]=p.Position.Y;original[o+2]=p.Position.Z;
            original[o+3]=p.Normal.X;original[o+4]=p.Normal.Y;original[o+5]=p.Normal.Z;
            for(int c=0;c<4;c++){original[o+6+c]=p.Color[c];original[o+10+c]=p.Color1[c];}
        }
        VertexData=(float[])original.Clone();
        bindings=layer.Morphs.Select(b=>(b,starts[b.Mesh],new float[b.Targets.Length])).ToArray();
        roundTripNormals=layer.Morphs.Where(b=>b.Attribute==MorphAttribute.Normal&&b.NormalPolicy==MorphNormalPolicy.NativeWeightedRoundTrip).Select(b=>(starts[b.Mesh],scene.Meshes[b.Mesh].Vertices.Length)).ToArray();
        Ranges=layer.Morphs.Select(b=>b.Mesh).Distinct().Order().Select(m=>new MorphRange(m,starts[m],scene.Meshes[m].Vertices.Length)).ToArray();
    }
    public static void Validate(Scene scene,NativeAnimationLayer layer)
    {
        if(layer.Morphs.GroupBy(b=>(b.Mesh,b.Attribute)).Any(g=>g.Count()>1))throw new InvalidDataException("Ambiguous morph bindings on the same mesh attribute.");
        long values=0;
        foreach(var b in layer.Morphs)
        {
            MorphGeometryEditing.Validate(b);
            if(b.Mesh<0||b.Mesh>=scene.Meshes.Length||!Enum.IsDefined(b.Attribute)||!Enum.IsDefined(b.NormalPolicy)||b.Targets.Length>32)throw new InvalidDataException("Invalid/over-budget morph binding.");
            foreach(var t in b.Targets)
            {
                values+=t.Values.Length;if(values>2_000_000||t.Values.Length!=scene.Meshes[b.Mesh].Vertices.Length)throw new InvalidDataException("Morph target does not match prepared mesh facepoints or exceeds the prepared-data budget.");
                if(t.Values.Any(p=>!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z)||!float.IsFinite(p.W)))throw new InvalidDataException("Nonfinite morph value.");
                if(t.Weight.Keys.Length==0||t.Weight.Keys.Any(k=>!double.IsFinite(k.Frame)||!float.IsFinite(k.Value)||!float.IsFinite(k.InTangent)||!float.IsFinite(k.OutTangent))||!t.Weight.Keys.Select(k=>k.Frame).SequenceEqual(t.Weight.Keys.Select(k=>k.Frame).Order()))throw new InvalidDataException("Invalid morph weight curve.");
            }
        }
    }
    public bool Sample(double frame,EvaluatedPose? pose=null)
    {
        if(!double.IsFinite(frame))throw new ArgumentOutOfRangeException(nameof(frame));bool changed=frame!=lastFrame;bool skinChanged=nativeNormals&&pose is not null&&!lastSkin.SequenceEqual(pose.Skin);if(!changed&&!skinChanged)return false;
        if(changed)
        {
        foreach(var range in Ranges)Array.Copy(original,range.FirstVertex*Stride,VertexData,range.FirstVertex*Stride,range.VertexCount*Stride);
        foreach(var (binding,first,weights) in bindings)
        {
            float sum=0;for(int i=0;i<weights.Length;i++){weights[i]=binding.Targets[i].Weight.Sample(frame);sum+=weights[i];}
            int channel=binding.Attribute switch{MorphAttribute.Position=>0,MorphAttribute.Normal=>3,MorphAttribute.Color0=>6,_=>10};
            bool color=channel>=6;int components=color?4:3;int count=binding.Targets.Length==0?0:binding.Targets[0].Values.Length;
            for(int v=0;v<count;v++)
            {
                int o=(first+v)*Stride+channel;var value=new Vector4(original[o],original[o+1],original[o+2],color?original[o+3]:0)*(1-sum);
                for(int t=0;t<weights.Length;t++)value+=binding.Targets[t].Values[v]*weights[t];
                if(!float.IsFinite(value.LengthSquared()))throw new InvalidDataException($"Nonfinite evaluated morph: {binding.Entry} at {frame:G9}.");
                if(binding.Attribute==MorphAttribute.Normal){float length=new Vector3(value.X,value.Y,value.Z).Length();if(length>0)value/=length;}
                if(binding.Attribute==MorphAttribute.Normal)normalBlend[first+v]=new(value.X,value.Y,value.Z);
                for(int c=0;c<components;c++)VertexData[o+c]=color&&binding.ByteColors?unchecked((byte)(int)(value[c]*255))/255f:value[c];
            }
        }
        }
        if(nativeNormals&&pose is not null)
        {
            // Native weighted normals use a sum of individual inverses, not inverse(blended skin).
            // Retain that forward/reverse result before the final GPU skinning pass.
            Array.Clear(inverseReady);
            foreach(var (first,count) in roundTripNormals)
            for(int i=first;i<first+count;i++)
            {
                var vertex=vertices[i];var forward=default(Matrix4x4);var reverse=default(Matrix4x4);
                for(int j=0;j<vertex.InfluenceCount;j++)if(vertex.WeightAt(j)!=0)
                {
                    int bone=(int)vertex.JointAt(j);if(!inverseReady[bone]){if(!Matrix4x4.Invert(pose.Skin[bone],out inverseSkin[bone]))throw new InvalidDataException("Singular animated skin transform for morph normals.");inverseReady[bone]=true;}
                    forward+=pose.Skin[bone]*vertex.WeightAt(j);reverse+=inverseSkin[bone]*vertex.WeightAt(j);
                }
                var normal=Vector3.TransformNormal(normalBlend[i],forward);if(normal.LengthSquared()>0)normal=Vector3.Normalize(normal);normal=Vector3.TransformNormal(normal,reverse);int offset=i*Stride+3;VertexData[offset]=normal.X;VertexData[offset+1]=normal.Y;VertexData[offset+2]=normal.Z;
            }
            pose.Skin.CopyTo(lastSkin,0);
        }
        lastFrame=frame;Revision++;return true;
    }
    public Vertex Vertex(int index,Vertex source)
    {
        int o=index*Stride;return source with{Position=new(VertexData[o],VertexData[o+1],VertexData[o+2]),Normal=new(VertexData[o+3],VertexData[o+4],VertexData[o+5]),Color=new(VertexData[o+6],VertexData[o+7],VertexData[o+8],VertexData[o+9]),Color1=new(VertexData[o+10],VertexData[o+11],VertexData[o+12],VertexData[o+13])};
    }
}
