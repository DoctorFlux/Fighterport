using System.Numerics;

namespace FighterPort.Core;

// Derivatives of the owned native evaluator, not rigid-body approximations.
// System.Numerics uses row vectors: local=S*Rx*Ry*Rz*T,
// world=local*parentWorld, skin=inverseRestWorld*world. Rotation parameters are
// native local Euler radians; the first three parameters are body translation
// along the supplied orthonormal frame (world XYZ by default), in units of
// the prepared rig's CharacteristicLength. Native binds are never normalized.
public sealed class NativePoseDifferentials
{
    public const string Version="native-srt-analytic-differentials-2";
    readonly Matrix4x4[][] world,skin;
    readonly Vector3[][]? geometry;
    readonly CancellationToken cancellation;
    readonly int count;
    public int Variables {get;}

    public NativePoseDifferentials(RigDescriptor rig,EvaluatedPose pose,int body,int[] rotationBones,CancellationToken token=default,Matrix4x4? translationBasis=null)
    {
        ArgumentNullException.ThrowIfNull(rig);ArgumentNullException.ThrowIfNull(pose);ArgumentNullException.ThrowIfNull(rotationBones);
        cancellation=token;token.ThrowIfCancellationRequested();count=rig.Scene.Bones.Length;
        if(pose.Local.Length!=count||pose.World.Length!=count||pose.Skin.Length!=count)
            throw new ArgumentException("Native pose derivatives require the complete prepared local, world and skin graph.",nameof(pose));
        if(body<0||body>=count)throw new ArgumentOutOfRangeException(nameof(body));
        if(rotationBones.Distinct().Count()!=rotationBones.Length||rotationBones.Any(b=>b<0||b>=count))
            throw new ArgumentException("Rotation variables must reference distinct existing native bones.",nameof(rotationBones));
        float length=rig.CharacteristicLength;
        if(!float.IsFinite(length)||length<=0)throw new NotSupportedException("Native pose derivative length must be finite and positive.");
        var basis=translationBasis??Matrix4x4.Identity;
        var axes=new[]{new Vector3(basis.M11,basis.M12,basis.M13),new Vector3(basis.M21,basis.M22,basis.M23),new Vector3(basis.M31,basis.M32,basis.M33)};
        if(!Finite(basis)||axes.Any(v=>Math.Abs(v.LengthSquared()-1)>.0001f)||
            Math.Abs(Vector3.Dot(axes[0],axes[1]))>.0001f||Math.Abs(Vector3.Dot(axes[0],axes[2]))>.0001f||Math.Abs(Vector3.Dot(axes[1],axes[2]))>.0001f)
            throw new NotSupportedException("Native translation derivative variables need a finite orthonormal frame; no native transform was normalized.");
        Variables=checked(3+3*rotationBones.Length);geometry=pose.GeometryPositions;
        var locals=new Matrix4x4[count];var inverseRest=new Matrix4x4[count];
        var rotationLocal=new Matrix4x4[rotationBones.Length][];
        var rotationAt=Enumerable.Repeat(-1,count).ToArray();
        for(int i=0;i<rotationBones.Length;i++)rotationAt[rotationBones[i]]=i;
        for(int bone=0;bone<count;bone++)
        {
            token.ThrowIfCancellationRequested();var value=pose.Local[bone];
            if(!value.Channels().All(float.IsFinite)||value.Scale.X<=0||value.Scale.Y<=0||value.Scale.Z<=0)
                throw new NotSupportedException("Native analytic derivatives require finite channels and positive authored scale; no scale was changed.");
            locals[bone]=value.Matrix;
            if(!Finite(pose.World[bone])||!Matrix4x4.Invert(rig.Rest.World[bone],out inverseRest[bone])||!Finite(inverseRest[bone]))
                throw new NotSupportedException("Native analytic derivatives cannot interpret nonfinite world or singular inverse-rest transforms.");
            int rotation=rotationAt[bone];if(rotation<0)continue;
            float x=value.Rotation.X*MathF.PI/180,y=value.Rotation.Y*MathF.PI/180,z=value.Rotation.Z*MathF.PI/180;
            var scale=Matrix4x4.CreateScale(value.Scale);var translation=Matrix4x4.CreateTranslation(value.Translation);
            var rx=Matrix4x4.CreateRotationX(x);var ry=Matrix4x4.CreateRotationY(y);var rz=Matrix4x4.CreateRotationZ(z);
            float sx=MathF.Sin(x),cx=MathF.Cos(x),sy=MathF.Sin(y),cy=MathF.Cos(y),sz=MathF.Sin(z),cz=MathF.Cos(z);
            var dx=new Matrix4x4(0,0,0,0,0,-sx,cx,0,0,-cx,-sx,0,0,0,0,0);
            var dy=new Matrix4x4(-sy,0,-cy,0,0,0,0,0,cy,0,-sy,0,0,0,0,0);
            var dz=new Matrix4x4(-sz,cz,0,0,-cz,-sz,0,0,0,0,0,0,0,0,0,0);
            rotationLocal[rotation]=[scale*dx*ry*rz*translation,scale*rx*dy*rz*translation,scale*rx*ry*dz*translation];
        }
        int bodyParent=rig.Scene.Bones[body].Parent;
        var parentInverse=Matrix4x4.Identity;
        if(bodyParent>=0&&(!Matrix4x4.Invert(pose.World[bodyParent],out parentInverse)||!Finite(parentInverse)))
            throw new NotSupportedException("Native body translation derivatives require an invertible actual parent frame.");
        var translationLocal=new Matrix4x4[3];
        for(int axis=0;axis<3;axis++)
        {
            var direction=axes[axis];
            var local=Vector3.TransformNormal(direction*length,parentInverse);
            // dT has homogeneous bottom-right zero. Using CreateTranslation
            // here would incorrectly add the unperturbed transform derivative.
            translationLocal[axis]=new(0,0,0,0,0,0,0,0,0,0,0,0,local.X,local.Y,local.Z,0);
        }
        world=new Matrix4x4[Variables][];skin=new Matrix4x4[Variables][];
        for(int variable=0;variable<Variables;variable++)
        {
            token.ThrowIfCancellationRequested();var derivativeWorld=new Matrix4x4[count];var derivativeSkin=new Matrix4x4[count];
            int changed=variable<3?body:rotationBones[(variable-3)/3];
            var derivativeLocal=variable<3?translationLocal[variable]:rotationLocal[(variable-3)/3][(variable-3)%3];
            for(int bone=0;bone<count;bone++)
            {
                if((bone&31)==0)token.ThrowIfCancellationRequested();int parent=rig.Scene.Bones[bone].Parent;
                var derivative=bone==changed?derivativeLocal*(parent<0?Matrix4x4.Identity:pose.World[parent]):default;
                if(parent>=0)derivative+=locals[bone]*derivativeWorld[parent];
                derivativeWorld[bone]=derivative;derivativeSkin[bone]=inverseRest[bone]*derivative;
                if(!Finite(derivative)||!Finite(derivativeSkin[bone]))
                    throw new NotSupportedException("Native analytic derivatives exceed the finite transform range; no native channel was normalized.");
            }
            world[variable]=derivativeWorld;skin[variable]=derivativeSkin;
        }
    }

    public Matrix4x4 World(int bone,int variable)
    {
        Check(bone,variable);return world[variable][bone];
    }
    public Vector3 Joint(int bone,int variable)
    {
        Check(bone,variable);return world[variable][bone].Translation;
    }
    public Vector3 Point(SkinSurfacePoint point,int variable)
    {
        ArgumentNullException.ThrowIfNull(point);CheckVariable(variable);
        var position=geometry is not null&&point.Mesh>=0&&point.Mesh<geometry.Length&&point.Vertex>=0&&point.Vertex<geometry[point.Mesh].Length?
            geometry[point.Mesh][point.Vertex]:point.Position;
        if(!PoseGeometry.Finite(position))throw new ArgumentException("Native skin derivative point must be finite.",nameof(point));
        var result=Vector3.Zero;
        for(int slot=0;slot<point.InfluenceCount;slot++)
        {
            float weight=point.WeightAt(slot),joint=point.JointAt(slot);
            if(!float.IsFinite(weight)||weight<0)throw new ArgumentException("Native skin derivative weights must be finite and nonnegative.",nameof(point));
            if(weight==0)continue;
            if(!float.IsFinite(joint)||joint!=MathF.Truncate(joint)||joint<0||joint>=count)
                throw new ArgumentException("Native skin derivative influence must reference an existing native bone.",nameof(point));
            result+=Vector3.Transform(position,skin[variable][(int)joint])*weight;
        }
        return result;
    }
    void Check(int bone,int variable)
    {
        if(bone<0||bone>=count)throw new ArgumentOutOfRangeException(nameof(bone));CheckVariable(variable);
    }
    void CheckVariable(int variable)
    {
        cancellation.ThrowIfCancellationRequested();if(variable<0||variable>=Variables)throw new ArgumentOutOfRangeException(nameof(variable));
    }
    static bool Finite(Matrix4x4 m)=>float.IsFinite(m.M11)&&float.IsFinite(m.M12)&&float.IsFinite(m.M13)&&float.IsFinite(m.M14)&&
        float.IsFinite(m.M21)&&float.IsFinite(m.M22)&&float.IsFinite(m.M23)&&float.IsFinite(m.M24)&&
        float.IsFinite(m.M31)&&float.IsFinite(m.M32)&&float.IsFinite(m.M33)&&float.IsFinite(m.M34)&&
        float.IsFinite(m.M41)&&float.IsFinite(m.M42)&&float.IsFinite(m.M43)&&float.IsFinite(m.M44);
}
